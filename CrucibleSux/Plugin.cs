using System;
using System.Collections.Generic;
using System.Linq;
using CrucibleSux.Automation;
using CrucibleSux.Core;
using CrucibleSux.Diagnostics;
using CrucibleSux.Game;
using CrucibleSux.Integrations;
using CrucibleSux.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CrucibleSux;

/// <summary>
/// Crucible SUX, the Set-and-forget Unbroken eXecutor: progress, readiness, team suggestions and the companion-plugin
/// check (docs/PLAN.md phases 0-1), plus the session queue that runs boards through AutoDuty (phase 2).
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/sux";
    private const string LongCommand = "/cruciblesux";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IUnlockState UnlockState { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;
    [PluginService] internal static IFateTable FateTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;

    private readonly Configuration config;
    private readonly WindowSystem windowSystem = new("CrucibleSux");
    private readonly MainWindow mainWindow;
    private readonly BestiaryWatcher bestiary;
    private readonly Session session;
    private readonly AutoDutyIpc autoDuty;
    private readonly SessionRunner runner;
    private readonly ProgressionRunner journey;
    private readonly CombatRecorder recorder;
    private readonly TargetPicker picker;
    private readonly AggroController aggro;
    private readonly BossModGate bossMod;
    private readonly BossModPrompt bossModPrompt;

    /// <summary>Text "/sux report" copies on the next frame: the clipboard is only reachable while ImGui draws.</summary>
    private string? pendingCopy;

    public Plugin()
    {
        this.config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var data = new CrucibleData(DataManager);
        this.session = new Session(new CharacterReader(PlayerState, UnlockState, DataManager, this.config), new PluginInventory(PluginInterface, Log), this.config, Log);
        this.bestiary = new BestiaryWatcher(AddonLifecycle, PlayerState, Log, this.config, this.Save, data.Beasts);
        this.autoDuty = new AutoDutyIpc(PluginInterface, Log);
        // The recorder is created below, before any run can start.
        var bossModIpc = new BossModIpc(PluginInterface, Log, text => this.recorder!.Note(LogKind.Warn, text));
        this.runner = new SessionRunner(this.session, this.autoDuty, bossModIpc, this.config, this.Save,
            ClientState, Condition, ObjectTable, GameGui, PlayerState, ChatGui, NotificationManager);
        var game = new GameActions(ObjectTable, Condition, GameGui, PlayerState);
        var nav = new NavmeshIpc(PluginInterface, Log);
        this.journey = new ProgressionRunner(new ProgressionReader(PlayerState, DataManager), game, new Travel(game, nav, ClientState, DataManager),
            new Dialogue(game), nav, new RotationSolverIpc(PluginInterface, Log), this.runner, this.config, PluginInterface, ClientState, FateTable,
            ObjectTable, DataManager, ChatGui, NotificationManager);
        this.recorder = new CombatRecorder(this.config, data, PluginInterface, ClientState, Condition, ObjectTable, TargetManager, ChatGui, AddonLifecycle,
            DataManager, Log, this.autoDuty, nav, () => this.runner.Current is not null || this.journey.Running, this.LogHeader);
        this.picker = new TargetPicker(this.config, data.TargetPriorities, ClientState, Condition, ObjectTable, TargetManager, this.recorder);
        this.aggro = new AggroController(this.config, data, ClientState, Condition, ObjectTable, TargetManager, game, this.recorder);
        this.bossMod = new BossModGate(PluginInterface, CommandManager, ChatGui, Log, this.config, this.Save);
        this.bossModPrompt = new BossModPrompt(this.bossMod);
        var sessionTabs = new SessionTabs(this.runner, this.journey, this.session, this.config, PlayerState, this.Save, this.StartSession);
        this.mainWindow = new MainWindow(this.session, data, this.config, this.bestiary, sessionTabs,
            new JourneyTab(this.journey, this.config, this.Save, this.StartJourney), new LogTab(this.recorder, this.config, this.Save), this.Save);
        this.windowSystem.AddWindow(this.mainWindow);
        this.windowSystem.AddWindow(this.bossModPrompt);

        var help = new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open Crucible SUX. \"/sux check\" prints what you can clear in one sitting; \"/sux run\" starts the queue; " +
                          "\"/sux journey\" starts the Beastmaster journey; \"/sux stop\" stops either; " +
                          "\"/sux report\" copies the latest combat log summary for a bug report (\"/sux report full\" copies all of it).",
        };
        CommandManager.AddHandler(Command, help);
        CommandManager.AddHandler(LongCommand, new CommandInfo(this.OnCommand) { HelpMessage = "Same as /sux.", ShowInHelp = false });

        PluginInterface.UiBuilder.Draw += this.OnDraw;
        PluginInterface.UiBuilder.OpenMainUi += this.mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += this.mainWindow.OpenSettings;
        Framework.Update += this.OnUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= this.OnUpdate;
        PluginInterface.UiBuilder.OpenConfigUi -= this.mainWindow.OpenSettings;
        PluginInterface.UiBuilder.OpenMainUi -= this.mainWindow.Toggle;
        PluginInterface.UiBuilder.Draw -= this.OnDraw;
        CommandManager.RemoveHandler(LongCommand);
        CommandManager.RemoveHandler(Command);
        this.windowSystem.RemoveAllWindows();
        this.journey.Dispose();
        this.runner.Dispose();
        this.bossMod.Dispose();
        this.recorder.Dispose();
        this.bestiary.Dispose();
    }

    private void Save() => PluginInterface.SavePluginConfig(this.config);

    private void OnUpdate(IFramework framework)
    {
        this.session.Update();
        this.bossMod.Update(() => this.runner.Current is null && !this.journey.Running);
        if (this.bossMod.Asking is not null)
            this.bossModPrompt.IsOpen = true;
        this.runner.Update();
        this.journey.Update();
        this.picker.Update();
        this.aggro.Update();
        this.recorder.Update();
    }

    private void OnDraw()
    {
        this.windowSystem.Draw();
        if (this.pendingCopy is not { } text)
            return;
        this.pendingCopy = null;
        ImGui.SetClipboardText(text);
        ChatGui.Print($"[Crucible SUX] Copied ({text.Length:N0} characters). Paste it into your report.");
    }

    /// <summary>What every combat log starts with: versions, settings and the character's Crucible state, never its name.</summary>
    private IReadOnlyList<string> LogHeader(string title)
    {
        string mode = this.runner.Current is not null ? "session queue" : this.journey.Running ? "Beastmaster journey" : "played by hand (nothing running)";
        var lines = new List<string>
        {
            $"Crucible SUX {PluginInterface.Manifest.AssemblyVersion}, client language {ClientState.ClientLanguage}, mode: {mode}",
            $"Settings: {this.config.BoardRunOptions()}, kill dangerous adds first: {(this.config.PickTargets ? "on" : "off")}, " +
                $"share aggro: {(this.config.ShareAggro ? $"on (floors you {this.config.PlayerFloor}%, familiar {this.config.BeastFloor}%)" : "off")}, " +
                $"heal with potions: {(this.config.UseItems ? "on" : "off")}",
        };
        string inUse = this.config.MechanicsPlugin == BossModChoice.Unset ? "not chosen (counts as Reborn)" : this.config.MechanicsPlugin.ToString();
        if (this.bossMod.TurnedOff)
            inUse += $", {BossModSwitch.Name(this.bossMod.Off)} turned off for this run{(this.bossMod.Reloaded ? $", {BossModSwitch.Name(this.bossMod.Kept)} reloaded" : string.Empty)}";
        lines.Add($"BossMod in use: {inUse}");
        if (this.autoDuty.LastOverrides is { } pushed)
            lines.Add($"Last AutoDuty settings pushed: {string.Join(", ", pushed.Select(p => $"{p.Key}={p.Value}"))}");
        var plugins = this.session.Dependencies.SelectMany(r => r.Candidates)
            .Select(c => c.Installed is { IsLoaded: true } p ? $"{p.InternalName} {p.Version}" : null)
            .OfType<string>()
            .Distinct();
        lines.Add($"Plugins loaded: {string.Join(", ", plugins)}");
        if (this.session.Snapshot is { } s)
        {
            lines.Add($"Beastmaster level {s.JobLevel}, item level {s.ItemLevel?.ToString() ?? "?"}, beasts befriended {s.UnlockedBeasts?.Count.ToString() ?? "?"}, " +
                      $"best known ranks {string.Join(", ", s.KnownRanks.Take(5))}, boards cleared {string.Join(", ", s.ClearedBoards.Order())}");
        }
        return lines;
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "report":
            case "report full":
                // The latest board, not the short "outside" log a session records on the way out of it.
                if ((this.recorder.Logs.FirstOrDefault(l => l.Title != CombatRecorder.OutsideTitle) ?? this.recorder.Logs.FirstOrDefault()) is not { } latest)
                {
                    ChatGui.PrintError("[Crucible SUX] No combat log yet: run a board first (recording is in the Log tab).");
                    break;
                }
                this.pendingCopy = args.Contains("full", StringComparison.OrdinalIgnoreCase) ? latest.Full(DateTime.UtcNow) : latest.Summary(DateTime.UtcNow);
                break;
            case "check":
                this.PrintSitting();
                break;
            case "run":
                this.StartSession();
                break;
            case "journey":
                this.StartJourney();
                break;
            case "stop":
                if (this.runner.Current is null && !this.journey.Running)
                    ChatGui.Print("[Crucible SUX] Nothing is running.");
                // The journey first: it owns a board run it started.
                this.journey.Stop("Stopped with /sux stop.");
                this.runner.Stop("Stopped with /sux stop.");
                break;
            default:
                this.mainWindow.Toggle();
                break;
        }
    }

    private void StartSession()
    {
        if (this.runner.Current is not null)
        {
            ChatGui.Print("[Crucible SUX] A session is already running.");
            return;
        }
        if (this.journey.Running)
        {
            ChatGui.PrintError("[Crucible SUX] The journey is running and starts its own boards; \"/sux stop\" stops it.");
            return;
        }
        // Judged once the player has said which BossMod they use, as if the other one were off, so it is turned off only
        // for a session that can start.
        this.bossMod.Run("the session", () =>
        {
            this.session.Update(force: true);
            var checks = this.runner.Checks(fresh: true, beforeSwitch: true);
            bool ready = Preflight.CanStart(checks);
            if (!ready)
                PrintSessionProblems(checks);
            return ready;
        }, () =>
        {
            var started = this.runner.Start();
            if (this.runner.Current is null)
                PrintSessionProblems(started);
        });
    }

    private static void PrintSessionProblems(IReadOnlyList<ReadinessCheck> checks)
    {
        ChatGui.PrintError("[Crucible SUX] The session cannot start:");
        foreach (var check in checks.Where(c => c.Status == CheckStatus.Fail))
            ChatGui.PrintError($"[Crucible SUX] {check.Label}: {check.Detail}");
    }

    private void StartJourney()
    {
        if (this.journey.Running)
        {
            ChatGui.Print("[Crucible SUX] The journey is already running.");
            return;
        }
        this.bossMod.Run("the journey", () =>
        {
            var problems = this.journey.Checks(beforeSwitch: true);
            if (problems.Count > 0)
                PrintJourneyProblems(problems);
            return problems.Count == 0;
        }, () =>
        {
            if (!this.journey.Start())
                PrintJourneyProblems(this.journey.Checks());
        });
    }

    private static void PrintJourneyProblems(IReadOnlyList<string> problems)
    {
        ChatGui.PrintError("[Crucible SUX] The journey cannot start:");
        foreach (var problem in problems)
            ChatGui.PrintError($"[Crucible SUX] {problem}");
    }

    private void PrintSitting()
    {
        this.session.Update(force: true);
        if (this.session.Plan is not { } plan)
        {
            ChatGui.PrintError("[Crucible SUX] Log in to a character first.");
            return;
        }

        if (plan.Boards.Count == 0)
        {
            ChatGui.Print("[Crucible SUX] Every board is cleared.");
            return;
        }

        var playable = plan.Boards.Where(b => b.Verdict != BoardVerdict.Blocked).Select(b => b.Board.Number).ToList();
        string boards = playable.Count == 0 ? "none" : string.Join(", ", playable);
        ChatGui.Print($"[Crucible SUX] This sitting: boards {boards}.");
        if (plan.StopsAt is { } stop)
        {
            var reason = stop.Checks.First(c => c.Status == CheckStatus.Fail);
            ChatGui.Print($"[Crucible SUX] Stops at {stop.Board.Name}: {reason.Label}: {reason.Detail}.");
        }
    }
}
