using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrucibleSux.Core;
using CrucibleSux.Game;
using CrucibleSux.Integrations;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FateState = Dalamud.Game.ClientState.Fates.FateState;
using Mode = CrucibleSux.Integrations.RotationSolverIpc.Mode;

namespace CrucibleSux.Automation;

/// <summary>
/// The Beastmaster journey (docs/PLAN.md Phase 2b): asks the <see cref="ProgressionPlanner"/> for the next activity four
/// times a second and carries it out with the game calls in <see cref="GameActions"/>, vnavmesh for walking, Rotation Solver
/// Reborn for fighting and the session runner for boards. Progress is always read back from the game (quest sequence, beasts
/// owned, EXP, inventory), never assumed from what was sent. None of it has run in game yet.
/// </summary>
public sealed unsafe class ProgressionRunner : IDisposable
{
    public const uint BeastmasterWeapon = 47470;
    public const uint StarterCoffer = 52649;

    private static readonly TimeSpan TickEvery = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(15);

    /// <summary>Shorter than <see cref="StuckAfter"/>, so a hunt that finds nothing skips its beast before the run counts as stuck.</summary>
    private static readonly TimeSpan HuntFor = TimeSpan.FromMinutes(12);
    private static readonly TimeSpan SearchPointFor = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan IdleZoneFor = TimeSpan.FromSeconds(60);

    /// <summary>How long a zone left for having no FATEs is not chosen again for its measured rate.</summary>
    private static readonly TimeSpan ZoneCooldown = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan NobodyThereFor = TimeSpan.FromSeconds(30);
    private const int MaxFailures = 3;
    private const float TalkRange = 3f;
    private const float MeleeRange = 3f;

    /// <summary>Where to look around a spawn point, in yalms from it: mobs roam, and only nearby ones are loaded.</summary>
    private static readonly Vector2[] SearchPattern = [new(0, 0), new(40, 0), new(0, 40), new(-40, 0), new(0, -40)];

    private readonly ProgressionReader reader;
    private readonly GameActions game;
    private readonly Travel travel;
    private readonly Dialogue dialogue;
    private readonly NavmeshIpc nav;
    private readonly RotationSolverIpc rotation;
    private readonly SessionRunner boards;
    private readonly Configuration config;
    private readonly IDalamudPluginInterface pi;
    private readonly IClientState client;
    private readonly IFateTable fates;
    private readonly IObjectTable objects;
    private readonly IDataManager data;
    private readonly IChatGui chat;
    private readonly INotificationManager notifications;
    private readonly ExpMeter meter = new();
    private readonly HashSet<uint> skipped = [];

    private DateTime nextTick;
    private DateTime nextAct;
    private DateTime nextChase;
    private DateTime lastProgress;
    private string? key;
    private string fingerprint = string.Empty;
    private int failures;
    private bool notifiedWait;
    private int gearSetup;
    private int gearTries;

    /// <summary>The board of the session the journey started and still owns, or 0; survives <see cref="Reset"/>.</summary>
    private int boardRun;

    // Per activity; cleared by Reset.
    private int stepIndex;
    private int visits;
    private bool sawEvent;
    private bool borrowed;
    private bool escortArrived;
    private DateTime? since;
    private uint hunt;
    private DateTime huntStarted;
    private int searchPoint;
    private ulong prey;
    private ushort fate;
    private uint buyItem;
    private int buyBefore;
    private int buyTries;

    // Levelling zone rotation; kept across activities.
    private uint zone;
    private uint band;
    private uint leftIdle;
    private DateTime leftIdleAt;

    public ProgressionRunner(
        ProgressionReader reader,
        GameActions game,
        Travel travel,
        Dialogue dialogue,
        NavmeshIpc nav,
        RotationSolverIpc rotation,
        SessionRunner boards,
        Configuration config,
        IDalamudPluginInterface pi,
        IClientState client,
        IFateTable fates,
        IObjectTable objects,
        IDataManager data,
        IChatGui chat,
        INotificationManager notifications)
    {
        this.reader = reader;
        this.game = game;
        this.travel = travel;
        this.dialogue = dialogue;
        this.nav = nav;
        this.rotation = rotation;
        this.boards = boards;
        this.config = config;
        this.pi = pi;
        this.client = client;
        this.fates = fates;
        this.objects = objects;
        this.data = data;
        this.chat = chat;
        this.notifications = notifications;
        this.client.Logout += this.OnLogout;
    }

    public bool Running { get; private set; }

    /// <summary>What the runner is doing, for the Journey tab.</summary>
    public string Status { get; private set; } = "Not running.";

    /// <summary>Something only the player can do (assign a horn, answer a window); the run carries on once it is done.</summary>
    public string? Waiting { get; private set; }

    public Activity? Activity { get; private set; }

    /// <summary>Why the last run ended.</summary>
    public string? LastResult { get; private set; }

    private ProgressionOptions Options => new(this.config.ProgressQuests, this.config.ProgressBeasts, this.config.ProgressLeveling);

    public void Dispose()
    {
        this.client.Logout -= this.OnLogout;
        this.Stop("Crucible SUX was unloaded.");
    }

    /// <summary>What stops the journey from starting; empty when it can start.</summary>
    public IReadOnlyList<string> Checks()
    {
        var problems = new List<string>();
        if (this.game.Me is null)
            problems.Add("Log in to a character.");
        if (!this.Options.Any)
            problems.Add("Turn on at least one part of the journey.");
        if (!this.Loaded("vnavmesh"))
            problems.Add("vnavmesh must be installed and enabled: it does the walking.");
        if (!this.Loaded("RotationSolver"))
            problems.Add("Rotation Solver Reborn must be installed and enabled: it does the fighting.");
        if (this.boards.Current is not null)
            problems.Add("A board session is running; stop it first.");
        return problems;
    }

    /// <summary>What the journey would do next for the character as it stands (for the tab, while not running).</summary>
    public Activity? Preview()
    {
        var state = this.reader.Capture(out bool beastsUnknown);
        if (state is null)
            return null;
        if (beastsUnknown)
            return new(ActivityKind.Blocked, "Open the Master's Bestiary once so the game sends your beast list.");
        return this.Plan(state);
    }

    public bool Start()
    {
        if (this.Running || this.Checks().Count > 0)
            return false;
        this.Running = true;
        this.failures = 0;
        this.skipped.Clear();
        this.key = null;
        this.boardRun = 0;
        this.rotation.ForceOff();
        this.lastProgress = DateTime.UtcNow;
        this.LastResult = null;
        this.Status = "Starting.";
        Plugin.Log.Information($"Journey started: {this.Options}");
        this.chat.Print("[Crucible SUX] Beastmaster journey started. \"/sux stop\" stops it.");
        return true;
    }

    public void Stop(string reason, bool done = false)
    {
        if (!this.Running)
            return;
        this.Running = false;
        this.Reset();
        if (this.boardRun != 0 && this.boards.Current is not null)
            this.boards.Stop(reason);
        this.boardRun = 0;
        this.rotation.ForceOff();
        this.Status = reason;
        this.LastResult = reason;
        this.Waiting = null;
        this.Activity = null;
        Plugin.Log.Information($"Journey stopped: {reason}");
        this.chat.Print($"[Crucible SUX] Journey {(done ? "finished" : "stopped")}: {reason}");
        this.notifications.AddNotification(new Notification
        {
            Title = done ? "Crucible SUX: journey finished" : "Crucible SUX: journey stopped",
            Content = reason,
            Type = done ? NotificationType.Success : NotificationType.Warning,
            InitialDuration = TimeSpan.FromHours(12),
        });
    }

    /// <summary>Called every framework tick; does its work four times a second.</summary>
    public void Update()
    {
        if (!this.Running)
            return;
        var now = DateTime.UtcNow;
        if (now < this.nextTick)
            return;
        this.nextTick = now + TickEvery;
        try
        {
            this.Tick(now);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "The journey failed");
            this.Stop($"Stopped after an error: {ex.Message}");
        }
    }

    private void Tick(DateTime now)
    {
        this.Waiting = null;
        if (this.boardRun != 0 && this.BoardTick(now))
            return;
        var state = this.reader.Capture(out bool beastsUnknown);
        if (state is null)
        {
            this.Stop("No character is loaded.");
            return;
        }
        if (beastsUnknown)
        {
            this.Wait(now, "Open the Master's Bestiary once so the game sends your beast list.");
            return;
        }

        var activity = this.Plan(state);
        string key = Key(activity);
        if (key != this.key)
        {
            if (this.key is not null)
                this.failures = 0;
            this.Reset();
            this.key = key;
            this.Activity = activity;
            this.lastProgress = now;
            Plugin.Log.Information($"Journey: {activity.Kind}: {activity.Why}");
        }

        switch (activity.Kind)
        {
            case ActivityKind.Done:
                this.Stop(activity.Why, done: true);
                return;
            case ActivityKind.Blocked:
                this.Stop(activity.Why);
                return;
        }

        // Only what the game shows counts as progress; waiting on the player (see Wait) also keeps the clock fresh. The check
        // comes before the windows and busy states below, so a dialogue or cutscene that never ends is caught too.
        string fp = JourneyRules.Progress(key, state, this.reader.Experience(), this.client.TerritoryType);
        if (fp != this.fingerprint)
        {
            this.fingerprint = fp;
            this.lastProgress = now;
        }
        if (now - this.lastProgress > StuckAfter)
        {
            this.Fail(now, $"Nothing changed for {StuckAfter.TotalMinutes} minutes ({activity.Why})");
            return;
        }

        // A board run belongs to AutoDuty, including its windows.
        if (activity.Kind != ActivityKind.RunBoard && this.dialogue.Tick(this.Expect(activity, state)))
        {
            if (this.dialogue.Waiting is { } w)
                this.Wait(now, w);
        }
        else if (!this.game.IsBusy)
        {
            switch (activity.Kind)
            {
                case ActivityKind.Quest:
                    this.DoQuest(now, activity, state);
                    break;
                case ActivityKind.Level:
                    this.DoLevel(now, state);
                    break;
                case ActivityKind.Capture:
                    this.DoCapture(now, activity, state);
                    break;
                case ActivityKind.BuyGourd:
                    this.DoBuy(now, activity, state);
                    break;
                case ActivityKind.UseGourd:
                    this.DoUseGourd(now, activity);
                    break;
                case ActivityKind.RunBoard:
                    this.DoBoard(now, activity);
                    break;
            }
        }

        if (this.Waiting is null)
            this.notifiedWait = false;
    }

    /// <summary>
    /// While a board the journey started runs, the session runner owns the game, whatever the plan would say meanwhile. Once
    /// it ends, a clear lets the plan move on, a stop by the player stops the journey, and anything else is a problem.
    /// Returns true while the tick belongs to the board.
    /// </summary>
    private bool BoardTick(DateTime now)
    {
        var board = Boards.ByNumber(this.boardRun);
        if (this.boards.Current is { } o)
        {
            // The session runner has its own watchdog inside the board.
            this.Say($"{board.Name} through AutoDuty: {o.Status}");
            this.lastProgress = now;
            return true;
        }

        this.boardRun = 0;
        // AutoDuty drove the rotation plugin during the board, so what this side last sent no longer holds.
        this.rotation.ForceOff();
        var report = this.boards.LastReport;
        string ending = report is null ? "." : $": {report.Ending}";
        switch (JourneyRules.Classify(report))
        {
            case BoardEnd.Cleared:
                this.failures = 0;
                this.lastProgress = now;
                return false;
            case BoardEnd.Stopped:
                this.Stop($"The {board.Name} session was stopped{ending}");
                return true;
            default:
                this.Fail(now, $"The {board.Name} run ended without a clear{ending}");
                return true;
        }
    }

    /// <summary>The planner's activity, minus the beasts this run has given up on.</summary>
    private Activity Plan(ProgressionState state)
    {
        var a = ProgressionPlanner.Next(state, this.Options);
        if (a.Kind != ActivityKind.Capture || this.skipped.Count == 0)
            return a;
        var left = a.Beasts!.Where(b => !this.skipped.Contains(b)).ToList();
        if (left.Count > 0)
            return a with { Beasts = left };
        string names = string.Join(", ", a.Beasts!.Select(b => BeastSources.ById(b).Name));
        return new(ActivityKind.Blocked, $"{a.Why} No {names} could be captured this run; try again later or capture one by hand.");
    }

    private static string Key(Activity a) =>
        $"{a.Kind}|{a.Quest}|{a.Step?.Sequence}|{a.Level}|{a.Item}|{a.Board}|{string.Join(",", a.Beasts ?? [])}";

    private DialogueExpect Expect(Activity a, ProgressionState s) => a.Kind switch
    {
        ActivityKind.Quest => new(Quest: this.reader.QuestName(a.Quest), Choice: a.Step?.Choice),
        ActivityKind.UseGourd => new(Yes: this.reader.ItemName(a.Item)),
        ActivityKind.BuyGourd => new(Choice: KornagoShop.For(Purchase(a, s).Currency).Name),
        _ => new(),
    };

    /// <summary>What to buy next for a gourd: the gourd, or first the Bright Remnants it is short of (2 Faded each).</summary>
    private static (uint Item, int Quantity, uint Currency) Purchase(Activity a, ProgressionState s)
    {
        var src = BeastSources.All.First(b => b.Gourd == a.Item);
        int bright = s.Count(CrucibleIds.BrightRemnant);
        return src.Currency == CrucibleIds.BrightRemnant && bright < src.Price
            ? (CrucibleIds.BrightRemnant, src.Price - bright, CrucibleIds.FadedRemnant)
            : (src.Gourd, 1, src.Currency);
    }

    /// <summary>Forgets the current activity's progress and lets go of movement and the rotation.</summary>
    private void Reset()
    {
        this.travel.Reset();
        // Chasing moves the player through vnavmesh directly, outside the trip Travel knows about.
        this.nav.Stop();
        this.rotation.Release();
        var shop = this.game.Addon("ShopExchangeItem");
        if (shop != null)
            shop->Close(true);
        this.stepIndex = 0;
        this.visits = 0;
        this.sawEvent = false;
        this.borrowed = false;
        this.escortArrived = false;
        this.since = null;
        this.hunt = 0;
        this.searchPoint = 0;
        this.prey = 0;
        this.fate = 0;
        this.buyItem = 0;
        this.buyTries = 0;
    }

    private void Say(string status) => this.Status = status;

    private void Wait(DateTime now, string what)
    {
        this.Waiting = what;
        this.Status = what;
        this.lastProgress = now;
        if (this.notifiedWait)
            return;
        this.notifiedWait = true;
        this.chat.Print($"[Crucible SUX] Waiting for you: {what}");
        this.notifications.AddNotification(new Notification { Title = "Crucible SUX needs you", Content = what, Type = NotificationType.Info });
    }

    private void Fail(DateTime now, string reason)
    {
        this.failures++;
        Plugin.Log.Warning($"Journey problem {this.failures}/{MaxFailures}: {reason}");
        if (this.hunt != 0)
            this.skipped.Add(this.hunt);
        if (this.failures >= MaxFailures)
        {
            this.Stop($"{reason} ({MaxFailures} problems in a row).");
            return;
        }
        this.chat.Print($"[Crucible SUX] {reason} Trying again.");
        this.Reset();
        this.lastProgress = now;
    }

    /// <summary>Throttles game calls that need time to take effect.</summary>
    private bool Ready(DateTime now, double seconds)
    {
        if (now < this.nextAct)
            return false;
        this.nextAct = now + TimeSpan.FromSeconds(seconds);
        return true;
    }

    private bool Loaded(string internalName) =>
        this.pi.InstalledPlugins.Any(p => p.IsLoaded && p.InternalName.Equals(internalName, StringComparison.OrdinalIgnoreCase));

    // --- Job ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// True once the character is a Beastmaster with its gear on. The first time (after the unlock quest) it does what the
    /// game's own tutorial does: equip the axe, save a gear set, open the starter coffer, equip the recommended gear and
    /// save the set again (docs/research/10-quest-engine-apis.md §5).
    /// </summary>
    private bool EnsureJob(DateTime now)
    {
        if (this.game.OnBeastmaster)
            return this.FinishGearSetup(now);
        if (this.game.InCombat || !this.Ready(now, 3))
            return false;
        this.Say("Changing to Beastmaster.");
        if (this.game.Mounted)
        {
            this.game.UseGeneral(GameActions.Dismount);
            return false;
        }
        if (this.game.EquipBeastmasterGearset())
            return false;
        if (this.game.EquipMainHand(BeastmasterWeapon))
        {
            this.gearSetup = 1;
            this.gearTries = 0;
            return false;
        }
        this.Stop("Beastmaster has no gear set, and its weapon is not in your inventory or armoury.");
        return false;
    }

    private bool FinishGearSetup(DateTime now)
    {
        if (this.gearSetup == 0)
            return true;
        if (!this.Ready(now, 2))
            return false;
        this.Say("Setting up Beastmaster's gear.");
        switch (this.gearSetup)
        {
            case 1:
                this.game.CreateGearset();
                this.gearSetup = 2;
                break;
            case 2:
                if (this.game.ItemCount(StarterCoffer) > 0 && ++this.gearTries <= 5)
                    this.game.UseItem(StarterCoffer);
                else
                    this.gearSetup = 3;
                break;
            case 3:
                if (this.game.EquipRecommended() || ++this.gearTries > 20)
                    this.gearSetup = 4;
                break;
            default:
                if (!this.game.UpdateBeastmasterGearset())
                    Plugin.Log.Warning("Journey: no Beastmaster gear set to save the recommended gear into.");
                this.gearSetup = 0;
                return true;
        }
        return false;
    }

    // --- Quests ---------------------------------------------------------------------------------------------------

    private void DoQuest(DateTime now, Activity a, ProgressionState s)
    {
        var q = QuestScripts.ById(a.Quest);
        if (!q.AnyJob && !this.EnsureJob(now))
            return;
        if (a.Step is not { } first)
        {
            this.Say($"Accepting {q.Name}.");
            this.Visit(now, q.Giver);
            return;
        }

        var steps = q.At(first.Sequence).ToList();
        var step = steps[this.stepIndex % steps.Count];
        this.Say($"{q.Name}: {Describe(step)}");
        if (step.Need?.Summoned is uint b and not 0 && !this.Summon(now, b))
            return;

        bool done = step.Kind switch
        {
            StepKind.Talk or StepKind.Interact => this.Visit(now, step.At),
            StepKind.UseEventItem => this.Visit(now, step.At, useItem: step.Arg),
            StepKind.Emote => this.Visit(now, step.At, emote: (ushort)step.Arg),
            StepKind.FightDummy => this.FightDummy(now, step.At),
            StepKind.TemperedRelease => this.Summon(now, step.Arg) && this.OnDummy(now, step.At, GameActions.TemperedRelease),
            StepKind.Borrow => this.Borrow(now, step, s),
            StepKind.Battle => this.Battle(now, step.At),
            StepKind.Escort => this.Escort(now, step),
            _ => false,
        };
        if (done)
        {
            // A sequence with several steps moves on only when all are done; the sequence itself says when.
            this.stepIndex++;
            this.travel.Reset();
            this.since = null;
            this.escortArrived = false;
        }
    }

    private static string Describe(QuestStep step) => step.Kind switch
    {
        StepKind.Talk => "talking to the next person.",
        StepKind.Interact => "examining the marked spot.",
        StepKind.UseEventItem => "handing over the item.",
        StepKind.Emote => "using the emote.",
        StepKind.FightDummy => "fighting the striking dummy.",
        StepKind.TemperedRelease => "Tempered Release on the dummy.",
        StepKind.Borrow => "Borrow on the dummy.",
        StepKind.Battle => "the quest battle.",
        StepKind.Escort => "the escort (on foot).",
        _ => step.Kind.ToString(),
    };

    /// <summary>
    /// Goes to <paramref name="at"/> and interacts with its object; true once an event has started and ended there. A bare
    /// position counts as done on arrival. With <paramref name="useItem"/>, the item is used on the object only after an
    /// interaction did not move the quest on (the NPC's own event usually takes it).
    /// </summary>
    private bool Visit(DateTime now, Spot at, uint useItem = 0, ushort emote = 0, bool mount = true)
    {
        if (this.game.InEvent)
        {
            this.sawEvent = true;
            return false;
        }
        if (this.sawEvent)
        {
            this.sawEvent = false;
            this.visits++;
            return true;
        }
        if (this.FightBack())
            return false;
        this.rotation.Release();

        var target = at.DataId == 0 ? null : this.game.Nearest(at.DataId);
        var point = target?.Position ?? new Vector3(at.X, at.Y, at.Z);
        var t = this.travel.Tick(at.Territory, point, target is null ? at.Radius : TalkRange, mount: mount);
        if (t == TravelState.Failed)
        {
            this.Fail(now, this.travel.Problem ?? "The destination cannot be reached.");
            return false;
        }
        if (t == TravelState.Moving)
            return false;
        if (target is null)
        {
            if (at.DataId == 0)
                return true;
            this.since ??= now;
            if (now - this.since > NobodyThereFor)
                this.Fail(now, $"Nobody to talk to at ({at.X:0}, {at.Z:0}) in {this.ZoneName(at.Territory)}.");
            return false;
        }
        if (this.game.Mounted)
        {
            if (this.Ready(now, 1.5))
                this.game.UseGeneral(GameActions.Dismount);
            return false;
        }
        if (!this.Ready(now, 2))
            return false;
        if (emote != 0)
            this.game.Emote(emote, target);
        else if (useItem != 0 && this.visits > 0)
            this.game.UseEventItem(useItem, target);
        else
            this.game.Interact(target);
        return false;
    }

    /// <summary>True when <paramref name="beast"/> is out. Assigning a beast to a horn has no known API, so that is asked of the player.</summary>
    private bool Summon(DateTime now, uint beast)
    {
        if (this.game.SummonedBeast() == beast)
            return true;
        int slot = this.game.HornOf(beast);
        if (slot < 0)
        {
            this.Wait(now, $"Put the {BeastSources.ById(beast).Name} on a Battlehorn in the Master's Bestiary. The journey carries on once it is there.");
            return false;
        }
        if (this.game.InCombat)
            return false;
        if (this.Ready(now, 2))
        {
            if (this.game.Mounted)
                this.game.UseGeneral(GameActions.Dismount);
            else
                this.game.UseAction(GameActions.Battlehorns[slot]);
        }
        return false;
    }

    /// <summary>The striking dummy nearest <paramref name="at"/>, walking there first; null until one is in reach.</summary>
    private IGameObject? Dummy(DateTime now, Spot at)
    {
        var dummy = this.game.Nearest(at.DataId);
        if (dummy is not null && this.Chase(now, dummy))
            return dummy;
        if (dummy is not null)
            return null;
        var t = this.travel.Tick(at.Territory, new Vector3(at.X, at.Y, at.Z), 10);
        if (t == TravelState.Failed)
            this.Fail(now, this.travel.Problem ?? "The striking dummies cannot be reached.");
        else if (t == TravelState.Arrived && (this.since ??= now) < now - NobodyThereFor)
            this.Fail(now, "No striking dummy found near the Bannock.");
        return null;
    }

    private bool FightDummy(DateTime now, Spot at)
    {
        if (this.Dummy(now, at) is not { } dummy)
            return false;
        this.game.Target(dummy);
        this.rotation.Set(Mode.Manual);
        return false;
    }

    /// <summary>Uses <paramref name="action"/> on a dummy; true once the game took it.</summary>
    private bool OnDummy(DateTime now, Spot at, uint action)
    {
        this.rotation.Release();
        if (this.Dummy(now, at) is not { } dummy || !this.Ready(now, 1.5))
            return false;
        this.game.Target(dummy);
        if (this.game.UseAction(action, dummy.GameObjectId))
            return true;
        if (!this.game.InCombat)
            this.game.StartAutoAttack(dummy);
        return false;
    }

    private bool Borrow(DateTime now, QuestStep step, ProgressionState s)
    {
        var kin = BeastSources.All.Where(b => b.Kin == step.Kin && s.Owned.Contains(b.Beast)).ToList();
        var horned = kin.FirstOrDefault(b => this.game.HornOf(b.Beast) >= 0);
        if (horned is null)
        {
            this.Wait(now, $"Put one of your {step.Kin} beasts ({string.Join(", ", kin.Select(b => b.Name))}) on a Battlehorn. The journey carries on once it is there.");
            return false;
        }
        if (!this.Summon(now, horned.Beast))
            return false;
        if (!this.borrowed)
        {
            this.borrowed = this.OnDummy(now, step.At, GameActions.Borrow);
            return false;
        }
        if (!this.OnDummy(now, step.At, step.Arg))
            return false;
        this.borrowed = false;
        return true;
    }

    private bool Battle(DateTime now, Spot at)
    {
        if (this.game.InCombat)
        {
            this.rotation.Set(Mode.Auto, targetFreely: true);
            return false;
        }
        var t = this.travel.Tick(at.Territory, new Vector3(at.X, at.Y, at.Z), at.Radius);
        if (t == TravelState.Failed)
            this.Fail(now, this.travel.Problem ?? "The battle cannot be reached.");
        else if (t == TravelState.Arrived)
        {
            if (this.game.Mounted && this.Ready(now, 1.5))
                this.game.UseGeneral(GameActions.Dismount);
            this.rotation.Set(Mode.Auto, targetFreely: true);
        }
        return false;
    }

    /// <summary>Walks (never rides) to the escort's next stop, fighting off what attacks, then talks to the escorted NPC.</summary>
    private bool Escort(DateTime now, QuestStep step)
    {
        if (this.FightBack())
            return false;
        this.rotation.Release();
        if (!this.escortArrived)
        {
            var t = this.travel.Tick(step.At.Territory, new Vector3(step.At.X, step.At.Y, step.At.Z), step.At.Radius, mount: false);
            if (t == TravelState.Failed)
                this.Fail(now, this.travel.Problem ?? "The escort's destination cannot be reached.");
            if (t != TravelState.Arrived)
                return false;
            // The talk below walks to the NPC; the stop must not pull the player back to the waypoint.
            this.escortArrived = true;
            this.travel.Reset();
        }
        return this.Visit(now, step.At with { DataId = step.Arg }, mount: false);
    }

    /// <summary>Stands and fights while something has the player in combat (Travel neither mounts nor teleports then); true while it does.</summary>
    private bool FightBack()
    {
        if (!this.game.InCombat)
            return false;
        if (this.nav.IsBusy)
            this.nav.Stop();
        this.rotation.Set(Mode.Auto, targetFreely: true);
        this.Say("Fighting off an attacker.");
        return true;
    }

    /// <summary>Walks into melee range of a moving target; true when there.</summary>
    private bool Chase(DateTime now, IGameObject target)
    {
        float distance = this.game.DistanceTo(target.Position) - target.HitboxRadius;
        if (distance <= MeleeRange)
        {
            if (this.nav.IsBusy)
                this.nav.Stop();
            if (!this.game.Mounted)
                return true;
            if (this.Ready(now, 1.5))
                this.game.UseGeneral(GameActions.Dismount);
            return false;
        }
        if (now >= this.nextChase)
        {
            this.nextChase = now + TimeSpan.FromSeconds(1);
            this.nav.MoveTo(target.Position);
        }
        return false;
    }

    // --- Beasts ---------------------------------------------------------------------------------------------------

    private void DoCapture(DateTime now, Activity a, ProgressionState s)
    {
        if (!this.EnsureJob(now))
            return;
        if (this.hunt == 0 || !a.Beasts!.Contains(this.hunt))
        {
            // Prefer a beast in this zone, then the planner's order.
            this.hunt = a.Beasts!.OrderBy(b => BeastSources.ById(b).Spawns.Any(sp => sp.Territory == this.client.TerritoryType) ? 0 : 1).First();
            this.huntStarted = now;
            this.searchPoint = 0;
            this.since = null;
            this.prey = 0;
        }
        var src = BeastSources.ById(this.hunt);
        if (now - this.huntStarted > HuntFor)
        {
            this.Fail(now, $"No {src.Name} captured in {HuntFor.TotalMinutes} minutes; it is skipped for this run.");
            return;
        }

        if (this.Prey(src, s.JobLevel) is not { } target)
        {
            this.prey = 0;
            if (this.FightBack())
                return;
            this.rotation.Release();
            this.Search(now, src);
            return;
        }

        this.prey = target.GameObjectId;
        this.since = null;
        int hp = target.MaxHp == 0 ? 100 : (int)(100L * target.CurrentHp / target.MaxHp);
        bool marked = GameActions.HasStatus(target, GameActions.InterestCaptured);
        var move = CaptureTactics.Decide(hp, s.JobLevel - target.Level, marked);
        this.Say($"Capturing a {src.Name}: {move switch { CaptureMove.Weaken => "wearing it down", CaptureMove.Capture => "using Capture", _ => "finishing it" }} (HP {hp}%).");
        if (!this.Chase(now, target))
            return;
        this.game.Target(target);
        switch (move)
        {
            case CaptureMove.Weaken:
                this.rotation.Release();
                if (!this.game.IsAutoAttacking && this.Ready(now, 1))
                    this.game.StartAutoAttack(target);
                break;
            case CaptureMove.Capture:
                this.rotation.Release();
                if (this.Ready(now, 1))
                    this.game.UseAction(GameActions.Capture, target.GameObjectId);
                break;
            default:
                this.rotation.Set(Mode.Manual);
                break;
        }
    }

    /// <summary>The wild form of <paramref name="src"/> to go after: the one already engaged, else the nearest free one.</summary>
    private IBattleNpc? Prey(BeastSource src, int level)
    {
        var me = this.game.Me;
        if (me is null)
            return null;
        // Free, or fighting the player or the player's beast (which often pulls it first); never someone else's.
        var ours = this.objects.Where(o => o.OwnerId == me.EntityId).Select(o => o.GameObjectId).Append(me.GameObjectId).ToHashSet();
        bool Fits(IBattleNpc o) =>
            GameActions.IsHostile(o) && src.WildNames.Contains(o.NameId) && o.Level <= level && o.CurrentHp > 0 && o.IsTargetable
            && (o.TargetObjectId == 0 || ours.Contains(o.TargetObjectId));
        var mobs = this.objects.OfType<IBattleNpc>().Where(Fits).ToList();
        return mobs.FirstOrDefault(o => o.GameObjectId == this.prey) ?? mobs.OrderBy(o => this.game.DistanceTo(o.Position)).FirstOrDefault();
    }

    /// <summary>Walks a small pattern around each spawn point in turn until the beast's wild form comes into view.</summary>
    private void Search(DateTime now, BeastSource src)
    {
        int points = src.Spawns.Count * SearchPattern.Length;
        if (this.searchPoint >= points * 2)
        {
            this.Fail(now, $"No {src.Name} found at its spawn points (the data may be wrong); it is skipped for this run.");
            return;
        }
        var spawn = src.Spawns[this.searchPoint / SearchPattern.Length % src.Spawns.Count];
        var offset = SearchPattern[this.searchPoint % SearchPattern.Length];
        var point = Travel.ToPoint(spawn) + new Vector3(offset.X, 0, offset.Y);
        this.Say($"Looking for a {src.Name} in {this.ZoneName(spawn.Territory)} ({spawn.MapX:0.0}, {spawn.MapY:0.0}).");

        var t = this.travel.Tick(spawn.Territory, point, 8, heightKnown: false);
        if (t == TravelState.Moving)
            return;
        if (t == TravelState.Failed && this.client.TerritoryType != spawn.Territory)
        {
            this.Fail(now, this.travel.Problem ?? $"{this.ZoneName(spawn.Territory)} cannot be reached.");
            return;
        }
        // Arrived, or this point is off the mesh: look a while, then try the next point.
        this.since ??= now;
        if (t == TravelState.Failed || now - this.since > SearchPointFor)
        {
            this.searchPoint++;
            this.since = null;
            this.travel.Reset();
        }
    }

    private void DoUseGourd(DateTime now, Activity a)
    {
        if (!this.EnsureJob(now))
            return;
        this.Say($"Using the {this.reader.ItemName(a.Item)}.");
        if (this.game.ItemCount(a.Item) > 0 && !this.game.InCombat && this.Ready(now, 3))
            this.game.UseItem(a.Item);
    }

    /// <summary>
    /// Buys a gourd from the Kornago merchant; Bright Remnants it is short of are first exchanged for Faded ones. The exchange
    /// dialog must name the item before it is confirmed, and the purchase counts only when the inventory shows it.
    /// </summary>
    private void DoBuy(DateTime now, Activity a, ProgressionState s)
    {
        var (item, quantity, currency) = Purchase(a, s);
        var shop = KornagoShop.For(currency);
        int index = KornagoShop.Position(shop.Lines, item, s.IsComplete);
        string name = this.reader.ItemName(item);
        if (name.Length == 0)
        {
            // The exchange is confirmed by finding this name in the dialog; an empty name would match anything.
            this.Stop($"Item {item} has no name in the game data, so the purchase cannot be checked.");
            return;
        }
        if (index < 0)
        {
            this.Stop($"The shop does not list the {name} yet.");
            return;
        }

        int have = this.game.ItemCount(item);
        if (this.buyItem != item)
        {
            this.buyItem = item;
            this.buyBefore = have;
            this.buyTries = 0;
        }
        if (have > this.buyBefore)
        {
            // Bought; the next tick plans again (the gourd next, or using it).
            var done = this.game.Addon("ShopExchangeItem");
            if (done != null)
                done->Close(true);
            this.buyItem = 0;
            return;
        }
        this.Say($"Buying {quantity} {name} from the Kornago merchant.");

        var dialog = this.game.Addon("ShopExchangeItemDialog");
        if (dialog != null)
        {
            if (!this.Ready(now, 1))
                return;
            if (GameActions.AllText(dialog).Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                GameActions.Callback(dialog, 0);
                return;
            }
            dialog->Close(true);
            this.Stop($"The exchange window did not name the {name}, so nothing was bought. The shop layout may have changed.");
            return;
        }
        var window = this.game.Addon("ShopExchangeItem");
        if (window != null)
        {
            if (!this.Ready(now, 2))
                return;
            if (++this.buyTries > 5)
            {
                this.Stop($"The shop did not sell the {name} after 5 tries.");
                return;
            }
            GameActions.Callback(window, 0, index, quantity);
            return;
        }
        this.Visit(now, KornagoShop.Spot);
    }

    // --- Boards ---------------------------------------------------------------------------------------------------

    /// <summary>Starts one run of the board through the session runner; <see cref="BoardTick"/> follows it from there.</summary>
    private void DoBoard(DateTime now, Activity a)
    {
        var board = Boards.ByNumber(a.Board);
        if (!this.EnsureJob(now))
            return;
        if (this.boards.Current is not null)
        {
            this.Wait(now, "A board session is already running; the journey continues when it ends.");
            return;
        }
        var checks = this.boards.Start([new QueueEntry(a.Board, 1)]);
        if (this.boards.Current is null)
        {
            var fail = checks.FirstOrDefault(c => c.Status == CheckStatus.Fail);
            this.Stop($"The {board.Name} cannot start{(fail is null ? "." : $": {fail.Label}: {fail.Detail}")}");
            return;
        }
        this.boardRun = a.Board;
        this.Say($"Starting the {board.Name} through AutoDuty.");
    }

    // --- Levelling ------------------------------------------------------------------------------------------------

    /// <summary>
    /// FATEs in the zones for the current level band (docs/research/09-leveling.md), best expected EXP per second first.
    /// The zone changes when it has had nothing to join for a minute, or when another zone of the band has measured
    /// clearly faster.
    /// </summary>
    private void DoLevel(DateTime now, ProgressionState s)
    {
        if (!this.EnsureJob(now))
            return;
        uint here = this.client.TerritoryType;
        this.meter.Record(now, here, s.JobLevel, this.reader.Experience());
        var zones = LevelingZones.For(s.JobLevel).ToList();
        if (zones[0] != this.band)
        {
            this.band = zones[0];
            this.zone = zones[0];
            this.meter.Reset();
        }

        if (here != this.zone)
        {
            this.Say($"Going to {this.ZoneName(this.zone)} for FATEs.");
            if (this.travel.Tick(this.zone, Vector3.Zero, float.MaxValue) == TravelState.Failed)
            {
                Plugin.Log.Information($"Journey: skipping {this.ZoneName(this.zone)}: {this.travel.Problem}");
                this.zone = JourneyRules.NextZone(zones, this.zone, this.meter.Rate);
                this.travel.Reset();
                if (this.zone == zones[0])
                    this.Fail(now, "No levelling zone for this level can be reached; attune the aetherytes there.");
            }
            return;
        }

        if (this.Fate(this.fate) is { State: FateState.Running } f && this.game.Me is { } me)
        {
            this.since = null;
            float distance = Vector2.Distance(new(f.Position.X, f.Position.Z), new(me.Position.X, me.Position.Z));
            if (distance > f.Radius * 0.6f && !this.game.InCombat)
            {
                this.Say($"Heading to the FATE \"{f.Name}\" (level {f.Level}, {f.Progress}%).");
                if (this.travel.Tick(here, f.Position, f.Radius * 0.4f) == TravelState.Failed)
                    this.fate = 0;
                return;
            }
            this.travel.Reset();
            if (this.game.Mounted)
            {
                if (this.Ready(now, 1.5))
                    this.game.UseGeneral(GameActions.Dismount);
                return;
            }
            var fm = FateManager.Instance();
            if (fm != null && s.JobLevel > f.MaxLevel && fm->SyncedFateId != f.FateId && this.Ready(now, 3))
                fm->LevelSync();
            this.rotation.Set(Mode.Auto, targetFreely: true);
            this.Say($"FATE \"{f.Name}\": {f.Progress}%.");
            return;
        }

        this.fate = 0;
        if (this.game.InCombat)
        {
            this.rotation.Set(Mode.Auto, targetFreely: true);
            this.Say("Finishing the fight.");
            return;
        }
        this.rotation.Release();

        uint avoid = now - this.leftIdleAt < ZoneCooldown ? this.leftIdle : 0;
        if (JourneyRules.BetterZone(zones, here, this.meter.Rate, avoid) is { } better)
        {
            this.zone = better;
            return;
        }
        if (FatePicker.Pick(this.Candidates(), s.JobLevel) is { } pick)
        {
            this.fate = pick.Id;
            this.travel.Reset();
            return;
        }
        this.since ??= now;
        this.Say($"Waiting for a FATE in {this.ZoneName(here)}.");
        if (now - this.since > IdleZoneFor)
        {
            this.zone = JourneyRules.NextZone(zones, here, this.meter.Rate);
            this.leftIdle = here;
            this.leftIdleAt = now;
            this.since = null;
        }
    }

    private IEnumerable<FateCandidate> Candidates()
    {
        for (int i = 0; i < this.fates.Length; i++)
        {
            var f = this.fates[i];
            if (f is null || f.State != FateState.Running)
                continue;
            yield return new FateCandidate(f.FateId, f.Level, f.MaxLevel, f.Progress, f.TimeRemaining, this.game.DistanceTo(f.Position), f.HasBonus, NeedsHandIn(f));
        }
    }

    /// <summary>A collection FATE: it asks for an item to be handed in.</summary>
    private static bool NeedsHandIn(IFate f) => f.Address != IntPtr.Zero && ((FateContext*)f.Address)->EventItem != 0;

    private IFate? Fate(ushort id)
    {
        if (id == 0)
            return null;
        for (int i = 0; i < this.fates.Length; i++)
        {
            if (this.fates[i] is { } f && f.FateId == id)
                return f;
        }
        return null;
    }

    private string ZoneName(uint territory) =>
        this.data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRowOrDefault(territory)?.PlaceName.ValueNullable?.Name.ExtractText() ?? $"zone {territory}";

    private void OnLogout(int type, int code) => this.Stop("Logged out.");
}
