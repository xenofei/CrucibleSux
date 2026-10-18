using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using CrucibleSux.Automation;
using CrucibleSux.Core;
using CrucibleSux.Game;
using CrucibleSux.Integrations;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using ClientBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using TerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace CrucibleSux.Diagnostics;

/// <summary>
/// Records what happens in a board (and while a session or the journey runs) into a <see cref="CombatLog"/> the player can
/// copy into a bug report: enemy casts with their area and where the player stood, HP and statuses, beasts, targets,
/// movement and vnavmesh paths, AutoDuty's state, windows and system messages. One log per board; the last
/// <see cref="KeepInMemory"/> stay in memory and every finished log is saved under the plugin's config folder. The
/// character's name never reaches a log: it is replaced with "&lt;you&gt;".
/// </summary>
public sealed unsafe class CombatRecorder : IDisposable
{
    public const string OutsideTitle = "Outside the boards";
    private const int KeepInMemory = 10;
    private const int KeepOnDisk = 30;
    private const float EnemyRange = 50f;
    private static readonly TimeSpan TickEvery = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan NavEvery = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan AutoDutyEvery = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MoveEvery = TimeSpan.FromSeconds(1);

    /// <summary>A hit that follows a cast's end within this long is put down to it.</summary>
    private static readonly TimeSpan HitWindow = TimeSpan.FromSeconds(1.5);

    private static readonly HashSet<string> OtherWindows = ["SelectYesno", "SelectString", "SelectIconString", "Talk", "ContentsFinderConfirm"];

    private readonly Configuration config;
    private readonly IDalamudPluginInterface pi;
    private readonly IClientState client;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly ITargetManager targets;
    private readonly IChatGui chat;
    private readonly IAddonLifecycle lifecycle;
    private readonly IPluginLog log;
    private readonly AutoDutyIpc autoDuty;
    private readonly NavmeshIpc nav;
    private readonly Func<bool> automationRunning;
    private readonly Func<string, IReadOnlyList<string>> header;
    private readonly ExcelSheet<Lumina.Excel.Sheets.Action> actions;
    private readonly ExcelSheet<TerritoryType> territories;
    private readonly IReadOnlyDictionary<uint, EnemyAbility> intel;
    private readonly IReadOnlyDictionary<uint, int> priorities;
    private readonly Dictionary<uint, (string Name, AoeShape Shape)> actionCache = [];
    private readonly List<CombatLog> finished = [];

    private readonly Dictionary<ulong, Cast> casts = [];
    private readonly List<(DateTime At, string Text)> recentEnds = [];
    private readonly Dictionary<ulong, (string Name, int HpPct, bool Dead)> beasts = [];
    private readonly Dictionary<ulong, string> enemies = [];
    private Dictionary<uint, string> statuses = [];

    private DateTime nextTick;
    private DateTime nextNav;
    private DateTime nextAutoDuty;
    private string[] nameParts = [];
    private uint territory;
    private bool inCombat;
    private int hpPct = -1;
    private bool dead;
    private ulong target;
    private Vector3 lastPos;
    private Vector3 lastMovePos;
    private DateTime lastMoveAt;
    private bool moving;
    private bool inside;
    private bool navBusy;
    private Vector3? navEnd;
    private bool? dutyRunning;

    public CombatRecorder(
        Configuration config,
        CrucibleData data,
        IDalamudPluginInterface pi,
        IClientState client,
        ICondition condition,
        IObjectTable objects,
        ITargetManager targets,
        IChatGui chat,
        IAddonLifecycle lifecycle,
        IDataManager dataManager,
        IPluginLog log,
        AutoDutyIpc autoDuty,
        NavmeshIpc nav,
        Func<bool> automationRunning,
        Func<string, IReadOnlyList<string>> header)
    {
        this.config = config;
        this.pi = pi;
        this.client = client;
        this.condition = condition;
        this.objects = objects;
        this.targets = targets;
        this.chat = chat;
        this.lifecycle = lifecycle;
        this.log = log;
        this.autoDuty = autoDuty;
        this.nav = nav;
        this.automationRunning = automationRunning;
        this.header = header;
        this.actions = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        this.territories = dataManager.GetExcelSheet<TerritoryType>();
        this.intel = data.Battles.Values.SelectMany(b => b).SelectMany(b => b.Enemies).SelectMany(e => e.Abilities)
            .DistinctBy(a => a.ActionId).ToDictionary(a => a.ActionId);
        this.priorities = data.TargetPriorities;
        this.Folder = Path.Combine(pi.GetPluginConfigDirectory(), "logs");

        this.lifecycle.RegisterListener(AddonEvent.PostSetup, this.OnWindowOpen);
        this.lifecycle.RegisterListener(AddonEvent.PreFinalize, this.OnWindowClose);
        this.chat.ChatMessage += this.OnChat;
    }

    /// <summary>Where finished logs are saved.</summary>
    public string Folder { get; }

    /// <summary>The log being recorded, or null.</summary>
    public CombatLog? Current { get; private set; }

    /// <summary>The recording first, then finished logs, newest first.</summary>
    public IEnumerable<CombatLog> Logs => (this.Current is { } c ? [c] : Array.Empty<CombatLog>()).Concat(Enumerable.Reverse(this.finished));

    public void Dispose()
    {
        this.chat.ChatMessage -= this.OnChat;
        this.lifecycle.UnregisterListener(AddonEvent.PreFinalize, this.OnWindowClose);
        this.lifecycle.UnregisterListener(AddonEvent.PostSetup, this.OnWindowOpen);
        this.Finish(DateTime.UtcNow, "Crucible SUX was unloaded.");
    }

    /// <summary>Adds a line to the recording, if there is one. The character's name is removed first.</summary>
    public void Note(LogKind kind, string text, bool important = false) =>
        this.Current?.Add(DateTime.UtcNow, kind, this.Clean(text), important);

    /// <summary>Called every framework tick; samples ten times a second.</summary>
    public void Update()
    {
        var now = DateTime.UtcNow;
        if (now < this.nextTick)
            return;
        this.nextTick = now + TickEvery;

        var me = this.objects.LocalPlayer;
        var board = Boards.ByTerritory(this.client.TerritoryType);
        bool want = this.config.RecordCombat && me is not null && (board is not null || this.automationRunning());
        string title = board?.Name ?? OutsideTitle;
        if (this.Current is { } current && (!want || current.Title != title))
            this.Finish(now, want ? null : "Recording stopped: left the board, or nothing is running.");
        if (!want || me is null)
            return;
        var log = this.Current ?? this.Begin(now, title, me);

        try
        {
            this.Sample(now, log, me, board is not null);
        }
        catch (Exception ex)
        {
            // A broken sample must not stop the plugin's other work; it shows up in the log instead.
            this.log.Warning(ex, "Combat recorder sample failed");
            this.Note(LogKind.Warn, $"Recorder error: {ex.GetType().Name}: {ex.Message}");
            this.nextTick = now + TimeSpan.FromSeconds(5);
        }
    }

    private CombatLog Begin(DateTime now, string title, IGameObject me)
    {
        this.nameParts = [.. me.Name.TextValue.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => p.Length >= 3)];
        if (me.Name.TextValue.Length > 0)
            this.nameParts = [me.Name.TextValue, .. this.nameParts];
        var log = new CombatLog(title, now, [.. this.header(title).Select(this.Clean)]);
        this.Current = log;
        this.casts.Clear();
        this.recentEnds.Clear();
        this.beasts.Clear();
        this.enemies.Clear();
        this.statuses = [];
        this.territory = 0;
        this.inCombat = false;
        this.hpPct = -1;
        this.dead = false;
        this.target = 0;
        this.lastPos = this.lastMovePos = me.Position;
        this.lastMoveAt = now;
        this.moving = this.inside = this.navBusy = false;
        this.navEnd = null;
        this.dutyRunning = null;
        this.nextNav = this.nextAutoDuty = now;
        log.Add(now, LogKind.Move, $"Start at {P(me.Position)}");
        return log;
    }

    private void Finish(DateTime now, string? why)
    {
        if (this.Current is not { } log)
            return;
        if (why is not null)
            log.Add(now, LogKind.Info, why);
        log.End(now);
        this.Current = null;
        this.finished.Add(log);
        if (this.finished.Count > KeepInMemory)
            this.finished.RemoveRange(0, this.finished.Count - KeepInMemory);
        this.Save(log, now);
    }

    private void Save(CombatLog log, DateTime now)
    {
        try
        {
            Directory.CreateDirectory(this.Folder);
            string safe = string.Concat(log.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            File.WriteAllText(Path.Combine(this.Folder, $"{log.StartedUtc:yyyyMMdd-HHmmss} {safe}.txt"), log.Full(now));
            foreach (var old in new DirectoryInfo(this.Folder).GetFiles("*.txt").OrderByDescending(f => f.Name).Skip(KeepOnDisk))
                old.Delete();
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, "Could not save the combat log");
        }
    }

    private void Sample(DateTime now, CombatLog log, IBattleChara me, bool inBoard)
    {
        var myPos = Flat(me.Position);

        if (this.client.TerritoryType != this.territory)
        {
            this.territory = this.client.TerritoryType;
            string zone = this.territories.GetRowOrDefault(this.territory)?.PlaceName.ValueNullable?.Name.ExtractText() ?? "?";
            log.Add(now, LogKind.Info, $"Zone: {zone} (territory {this.territory})");
        }

        bool combat = this.condition[ConditionFlag.InCombat];
        if (combat != this.inCombat)
        {
            this.inCombat = combat;
            log.Add(now, LogKind.Combat, combat ? "In combat" : "Out of combat");
        }

        this.SampleCasts(now, log, me, myPos);
        this.SamplePlayer(now, log, me);
        this.SampleBeasts(now, log, me);
        if (inBoard)
            this.SampleEnemies(now, log, me);
        this.SampleTarget(now, log);
        this.SampleMovement(now, log, me, myPos);

        if (now >= this.nextNav)
        {
            this.nextNav = now + NavEvery;
            this.SampleNav(now, log, me);
        }
        if (now >= this.nextAutoDuty)
        {
            this.nextAutoDuty = now + AutoDutyEvery;
            bool? running = this.autoDuty.IsRunning();
            if (running != this.dutyRunning)
            {
                this.dutyRunning = running;
                log.Add(now, LogKind.Info, running switch { true => "AutoDuty running", false => "AutoDuty stopped", _ => "AutoDuty does not answer" });
            }
        }
    }

    private void SampleCasts(DateTime now, CombatLog log, IBattleChara me, Vector2 myPos)
    {
        var seen = new HashSet<ulong>();
        foreach (var obj in this.objects)
        {
            if (obj is not IBattleNpc npc || !GameActions.IsHostile(npc) || Vector3.Distance(npc.Position, me.Position) > EnemyRange)
                continue;
            if (!npc.IsCasting)
                continue;
            seen.Add(npc.GameObjectId);
            var ci = ((ClientBattleChara*)npc.Address)->GetCastInfo();
            if (ci == null)
                continue;

            if (this.casts.TryGetValue(npc.GameObjectId, out var cast) && cast.ActionId != npc.CastActionId)
            {
                this.EndCast(now, log, me, cast, cutShort: cast.Progress < cast.Total - 0.3f);
                cast = null;
            }
            if (cast is null)
            {
                var (name, shape) = this.ActionInfo(npc.CastActionId);
                cast = new Cast
                {
                    ActionId = npc.CastActionId,
                    Caster = npc.Name.TextValue,
                    Action = name,
                    Shape = shape,
                    Total = npc.TotalCastTime,
                    Target = this.objects.SearchById(npc.CastTargetObjectId)?.Name.TextValue ?? "nothing",
                };
                this.casts[npc.GameObjectId] = cast;
            }

            // Ground and target circles sit where the cast aims; cones, rects and crosses start at the caster.
            Vector3 aim = ci->TargetLocation;
            cast.Origin = cast.Shape.Kind is AoeKind.Circle or AoeKind.Donut && aim != Vector3.Zero ? Flat(aim) : Flat(npc.Position);
            cast.Rotation = cast.Shape.Kind is AoeKind.Circle or AoeKind.Donut ? npc.Rotation : ci->Rotation;
            cast.Progress = npc.CurrentCastTime;
            cast.Margin = cast.Shape.Margin(cast.Origin, cast.Rotation, myPos);

            if (!cast.Logged)
            {
                cast.Logged = true;
                string intelText = this.intel.TryGetValue(cast.ActionId, out var a) ? $", intel {a.Target}/{a.Shape}" : string.Empty;
                string where = float.IsNaN(cast.Margin)
                    ? $"on {this.Clean(cast.Target)}"
                    : $"at {P(cast.Origin)} facing {Deg(cast.Rotation)}, you {Inside(cast.Margin)}";
                log.Add(now, LogKind.Cast, Inv($"{cast.Caster} casts {cast.Action} #{cast.ActionId} ({cast.Total:0.0}s, {cast.Shape}{intelText}) {where}; caster {Vector2.Distance(Flat(npc.Position), myPos):0.0} y away"));
            }
        }

        foreach (var (id, cast) in this.casts.Where(c => !seen.Contains(c.Key)).ToList())
        {
            this.casts.Remove(id);
            this.EndCast(now, log, me, cast, cutShort: cast.Total > 0 && cast.Progress < cast.Total - 0.3f);
        }
        this.recentEnds.RemoveAll(e => now - e.At > HitWindow);
    }

    private void EndCast(DateTime now, CombatLog log, IBattleChara me, Cast cast, bool cutShort)
    {
        if (cutShort)
        {
            log.Add(now, LogKind.Resolve, Inv($"{cast.Action} from {cast.Caster} cut short at {cast.Progress:0.0}/{cast.Total:0.0}s"));
            return;
        }
        string state = Inv($"you at {P(me.Position)}, {(this.moving ? "moving" : "standing")}, vnavmesh {(this.navBusy ? "busy" : "idle")}");
        if (cast.Margin > 0)
            log.Add(now, LogKind.Hit, Inv($"{cast.Action} from {cast.Caster} went off with you {cast.Margin:0.0} y inside ({cast.Shape} at {P(cast.Origin)} facing {Deg(cast.Rotation)}); {state}"));
        else if (float.IsNaN(cast.Margin))
            log.Add(now, LogKind.Resolve, $"{cast.Action} from {cast.Caster} went off (on {this.Clean(cast.Target)})");
        else
            log.Add(now, LogKind.Resolve, Inv($"{cast.Action} from {cast.Caster} went off; you {-cast.Margin:0.0} y outside"));
        this.recentEnds.Add((now, cast.Margin > 0 ? $"{cast.Action}, which you stood in" : cast.Action));
    }

    private void SamplePlayer(DateTime now, CombatLog log, IBattleChara me)
    {
        int pct = me.MaxHp == 0 ? 0 : (int)(100L * me.CurrentHp / me.MaxHp);
        if (this.hpPct < 0)
        {
            this.hpPct = pct;
        }
        else if (Math.Abs(pct - this.hpPct) >= 3)
        {
            string after = pct < this.hpPct && this.recentEnds.Count > 0 ? $", just after {this.recentEnds[^1].Text}" : string.Empty;
            log.Add(now, LogKind.Hp, $"HP {this.hpPct}% -> {pct}% ({me.CurrentHp}/{me.MaxHp}){after}", important: this.hpPct - pct >= 20);
            this.hpPct = pct;
        }

        bool isDead = me.IsDead || me.CurrentHp == 0;
        if (isDead != this.dead)
        {
            this.dead = isDead;
            log.Add(now, isDead ? LogKind.Death : LogKind.Info, isDead ? $"You were KO'd at {P(me.Position)}" : "You are back up");
        }

        var current = new Dictionary<uint, string>();
        foreach (var s in me.StatusList)
        {
            if (s.StatusId == 0 || current.ContainsKey(s.StatusId))
                continue;
            if (this.statuses.TryGetValue(s.StatusId, out var known))
            {
                current[s.StatusId] = known;
                continue;
            }
            string name = s.GameData.ValueNullable?.Name.ExtractText() ?? $"status {s.StatusId}";
            current[s.StatusId] = name;
            bool bad = name.Contains("Vulnerability", StringComparison.OrdinalIgnoreCase) || name.Contains("Damage Down", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Doom", StringComparison.OrdinalIgnoreCase);
            log.Add(now, LogKind.Status, Inv($"You gain {name} #{s.StatusId} ({s.RemainingTime:0}s) from {this.Clean(s.SourceObject?.Name.TextValue ?? "?")}"), bad);
        }
        foreach (var (id, name) in this.statuses.Where(s => !current.ContainsKey(s.Key)))
            log.Add(now, LogKind.Status, $"You lose {name} #{id}");
        this.statuses = current;
    }

    private void SampleBeasts(DateTime now, CombatLog log, IBattleChara me)
    {
        var seen = new HashSet<ulong>();
        foreach (var obj in this.objects)
        {
            if (obj.OwnerId != me.EntityId || obj is not IBattleChara b || obj.GameObjectId == me.GameObjectId)
                continue;
            seen.Add(b.GameObjectId);
            int pct = b.MaxHp == 0 ? 0 : (int)(100L * b.CurrentHp / b.MaxHp);
            bool isDead = b.IsDead || b.CurrentHp == 0;
            string name = b.Name.TextValue;
            if (!this.beasts.TryGetValue(b.GameObjectId, out var old))
            {
                log.Add(now, LogKind.Beast, $"{name} is out ({pct}% HP)");
            }
            else if (isDead && !old.Dead)
            {
                log.Add(now, LogKind.Beast, $"{name} was KO'd at {P(b.Position)}", important: true);
            }
            else if (Math.Abs(pct - old.HpPct) >= 10)
            {
                log.Add(now, LogKind.Beast, $"{name} HP {old.HpPct}% -> {pct}%");
            }
            else
            {
                continue;
            }
            this.beasts[b.GameObjectId] = (name, pct, isDead);
        }
        foreach (var (id, gone) in this.beasts.Where(b => !seen.Contains(b.Key)).ToList())
        {
            this.beasts.Remove(id);
            log.Add(now, LogKind.Beast, $"{gone.Name} is gone");
        }
    }

    private void SampleEnemies(DateTime now, CombatLog log, IBattleChara me)
    {
        var seen = new HashSet<ulong>();
        foreach (var obj in this.objects)
        {
            if (obj is not IBattleNpc npc || !GameActions.IsHostile(npc) || npc.IsDead || npc.CurrentHp == 0)
                continue;
            seen.Add(npc.GameObjectId);
            if (this.enemies.ContainsKey(npc.GameObjectId))
                continue;
            string name = npc.Name.TextValue;
            this.enemies[npc.GameObjectId] = name;
            log.Add(now, LogKind.Info, Inv($"{name} appears (name #{npc.NameId}, priority {this.priorities.GetValueOrDefault(npc.NameId)}) at {P(npc.Position)}, {Vector3.Distance(npc.Position, me.Position):0.0} y away"));
        }
        foreach (var (id, name) in this.enemies.Where(e => !seen.Contains(e.Key)).ToList())
        {
            this.enemies.Remove(id);
            log.Add(now, LogKind.Info, $"{name} is defeated or gone");
        }
    }

    private void SampleTarget(DateTime now, CombatLog log)
    {
        var t = this.targets.Target;
        ulong id = t?.GameObjectId ?? 0;
        if (id == this.target)
            return;
        this.target = id;
        string what = t is null ? "nothing"
            : t is IBattleNpc n ? $"{n.Name.TextValue} (name #{n.NameId}, priority {this.priorities.GetValueOrDefault(n.NameId)})"
            : this.Clean(t.Name.TextValue);
        log.Add(now, LogKind.Target, $"Target is now {what}");
    }

    private void SampleMovement(DateTime now, CombatLog log, IBattleChara me, Vector2 myPos)
    {
        this.moving = Vector3.Distance(me.Position, this.lastPos) > 0.05f;
        this.lastPos = me.Position;

        var deepest = this.casts.Values.Where(c => c.Margin > 0).MaxBy(c => c.Margin);
        bool nowInside = deepest is not null;
        if (nowInside != this.inside)
        {
            this.inside = nowInside;
            string how = Inv($"at {P(me.Position)}, {(this.moving ? "moving" : "standing")}, vnavmesh {(this.navBusy ? "busy" : "idle")}");
            if (deepest is { } c)
            {
                // Walking into an area that is already marked is the "runs into damage" report.
                log.Add(now, LogKind.Move, Inv($"Inside {c.Action} ({c.Shape}, {c.Margin:0.0} y deep, {Math.Max(0, c.Total - c.Progress):0.0}s left) {how}"), important: this.moving);
            }
            else
            {
                log.Add(now, LogKind.Move, $"Out of every marked area {how}");
            }
        }

        if (Vector3.Distance(me.Position, this.lastMovePos) >= 1f && now - this.lastMoveAt >= MoveEvery)
        {
            float d = Vector2.Distance(Flat(this.lastMovePos), myPos);
            log.Add(now, LogKind.Move, Inv($"At {P(me.Position)}, {d:0.0} y in {(now - this.lastMoveAt).TotalSeconds:0.0}s, facing {Deg(me.Rotation)}"));
            this.lastMovePos = me.Position;
            this.lastMoveAt = now;
        }
    }

    private void SampleNav(DateTime now, CombatLog log, IBattleChara me)
    {
        bool busy = this.nav.IsBusy;
        if (busy != this.navBusy)
        {
            this.navBusy = busy;
            if (!busy)
            {
                log.Add(now, LogKind.Nav, $"vnavmesh idle at {P(me.Position)}");
                this.navEnd = null;
            }
        }
        if (!busy)
            return;
        var points = this.nav.Waypoints;
        if (points.Count == 0)
            return;
        var end = points[^1];
        if (this.navEnd is { } old && Vector3.Distance(old, end) < 1f)
            return;
        this.navEnd = end;
        float length = Vector3.Distance(me.Position, points[0]);
        for (int i = 1; i < points.Count; i++)
            length += Vector3.Distance(points[i - 1], points[i]);
        var danger = this.casts.Values
            .Select(c => (c, m: c.Shape.Margin(c.Origin, c.Rotation, Flat(end))))
            .Where(x => x.m > 0)
            .Select(x => Inv($"{x.c.Action} ({x.m:0.0} y deep, {Math.Max(0, x.c.Total - x.c.Progress):0.0}s left)"))
            .ToList();
        string verdict = danger.Count == 0 ? "outside every marked area" : $"INSIDE {string.Join(", ", danger)}";
        log.Add(now, LogKind.Nav, Inv($"vnavmesh path to {P(end)}: {points.Count} waypoints, {length:0.0} y; the end is {verdict}"), important: danger.Count > 0);
    }

    private (string Name, AoeShape Shape) ActionInfo(uint id)
    {
        if (this.actionCache.TryGetValue(id, out var info))
            return info;
        info = this.actions.GetRowOrDefault(id) is { } a
            ? (a.Name.ExtractText(), AoeShape.FromAction(a.CastType, a.EffectRange, a.XAxisModifier, a.Omen.ValueNullable?.Path.ExtractText()))
            : ($"Action {id}", default);
        this.actionCache[id] = info;
        return info;
    }

    private void OnWindowOpen(AddonEvent type, AddonArgs args)
    {
        string name = args.AddonName;
        if (this.Current is null || !(name.StartsWith("XBM", StringComparison.Ordinal) || OtherWindows.Contains(name)))
            return;
        string detail = string.Empty;
        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (addon != null && name == "SelectYesno")
                detail = ": " + GameActions.AllText(addon).Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            else if (addon != null && name is "SelectString" or "SelectIconString")
                detail = ": " + string.Join(" / ", GameActions.MenuEntries(addon, name == "SelectIconString"));
        }
        catch (Exception ex)
        {
            this.log.Verbose(ex, $"Could not read {name}");
        }
        this.Note(LogKind.Window, $"{name} opened{detail}");
    }

    private void OnWindowClose(AddonEvent type, AddonArgs args)
    {
        string name = args.AddonName;
        if (this.Current is not null && (name.StartsWith("XBM", StringComparison.Ordinal) || OtherWindows.Contains(name)))
            this.Note(LogKind.Window, $"{name} closed");
    }

    private void OnChat(IHandleableChatMessage message)
    {
        if (this.Current is null)
            return;
        // The low 7 bits are the chat type; battle messages carry who acted and who was hit separately.
        var type = (XivChatType)((int)message.LogKind & 0x7F);
        bool system = type is XivChatType.SystemMessage or XivChatType.SystemError or XivChatType.ErrorMessage or XivChatType.NPCDialogueAnnouncements;
        bool onYou = type is XivChatType.Damage or XivChatType.Miss or XivChatType.GainDebuff && message.TargetKind == XivChatRelationKind.LocalPlayer;
        bool enemyAction = type == XivChatType.Action && message.SourceKind is XivChatRelationKind.EngagedEnemy or XivChatRelationKind.UnengagedEnemy;
        // The character's and its familiar's own actions, to see which skills the rotation plugin really uses.
        bool ownAction = type == XivChatType.Action && message.SourceKind is XivChatRelationKind.LocalPlayer or XivChatRelationKind.PetOrCompanion;
        if (system || onYou || enemyAction || ownAction)
            this.Note(LogKind.Chat, $"[{type}] {message.Message.TextValue}");
    }

    private string Clean(string text)
    {
        foreach (string part in this.nameParts)
            text = text.Replace(part, "<you>", StringComparison.OrdinalIgnoreCase);
        return text;
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static string P(Vector3 v) => Inv($"({v.X:0.0}, {v.Z:0.0}, h{v.Y:0.0})");

    private static string P(Vector2 v) => Inv($"({v.X:0.0}, {v.Y:0.0})");

    private static string Deg(float rotation) => Inv($"{rotation * 180f / MathF.PI:0}°");

    private static string Inside(float margin) => margin > 0 ? Inv($"{margin:0.0} y inside") : Inv($"{-margin:0.0} y outside");

    private static string Inv(FormattableString s) => FormattableString.Invariant(s);

    private sealed class Cast
    {
        public uint ActionId { get; init; }

        public string Caster { get; init; } = string.Empty;

        public string Action { get; init; } = string.Empty;

        public AoeShape Shape { get; init; }

        public float Total { get; init; }

        public string Target { get; init; } = string.Empty;

        public Vector2 Origin { get; set; }

        public float Rotation { get; set; }

        public float Progress { get; set; }

        public float Margin { get; set; } = float.NaN;

        public bool Logged { get; set; }
    }
}
