using System;
using System.Linq;
using CrucibleSux.Automation;
using CrucibleSux.Core;
using CrucibleSux.Game;
using CrucibleSux.Integrations;
using CrucibleSux.Ui;
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

    private readonly Configuration config;
    private readonly WindowSystem windowSystem = new("CrucibleSux");
    private readonly MainWindow mainWindow;
    private readonly BestiaryWatcher bestiary;
    private readonly Session session;
    private readonly SessionRunner runner;

    public Plugin()
    {
        this.config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var data = new CrucibleData(DataManager);
        this.session = new Session(new CharacterReader(PlayerState, UnlockState, DataManager, this.config), new PluginInventory(PluginInterface, Log), Log);
        this.bestiary = new BestiaryWatcher(AddonLifecycle, PlayerState, Log, this.config, this.Save, data.Beasts);
        this.runner = new SessionRunner(this.session, new AutoDutyIpc(PluginInterface, Log), this.config, this.Save,
            ClientState, Condition, ObjectTable, GameGui, PlayerState, ChatGui, NotificationManager);
        var sessionTabs = new SessionTabs(this.runner, this.session, this.config, PlayerState, this.Save);
        this.mainWindow = new MainWindow(this.session, data, this.config, this.bestiary, sessionTabs, this.Save);
        this.windowSystem.AddWindow(this.mainWindow);

        var help = new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open Crucible SUX. \"/sux check\" prints what you can clear in one sitting; \"/sux run\" starts the queue; \"/sux stop\" stops it.",
        };
        CommandManager.AddHandler(Command, help);
        CommandManager.AddHandler(LongCommand, new CommandInfo(this.OnCommand) { HelpMessage = "Same as /sux.", ShowInHelp = false });

        PluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += this.mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += this.mainWindow.OpenSettings;
        Framework.Update += this.OnUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= this.OnUpdate;
        PluginInterface.UiBuilder.OpenConfigUi -= this.mainWindow.OpenSettings;
        PluginInterface.UiBuilder.OpenMainUi -= this.mainWindow.Toggle;
        PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        CommandManager.RemoveHandler(LongCommand);
        CommandManager.RemoveHandler(Command);
        this.windowSystem.RemoveAllWindows();
        this.runner.Dispose();
        this.bestiary.Dispose();
    }

    private void Save() => PluginInterface.SavePluginConfig(this.config);

    private void OnUpdate(IFramework framework)
    {
        this.session.Update();
        this.runner.Update();
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "check":
                this.PrintSitting();
                break;
            case "run":
                this.StartSession();
                break;
            case "stop":
                if (this.runner.Current is null)
                    ChatGui.Print("[Crucible SUX] No session is running.");
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
        var checks = this.runner.Start();
        if (this.runner.Current is not null)
            return;
        ChatGui.PrintError("[Crucible SUX] The session cannot start:");
        foreach (var check in checks.Where(c => c.Status == CheckStatus.Fail))
            ChatGui.PrintError($"[Crucible SUX] {check.Label}: {check.Detail}");
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
