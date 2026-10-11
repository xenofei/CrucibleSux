using System;
using System.Linq;
using CrucibleSux.Core;
using CrucibleSux.Game;
using CrucibleSux.Ui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CrucibleSux;

/// <summary>
/// Crucible SUX, the Set-and-forget Unbroken eXecutor. This release is the read-only part: progress, readiness, team
/// suggestions and the companion-plugin check (docs/PLAN.md, phases 0-1). It sends no input to the game.
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

    private readonly Configuration config;
    private readonly WindowSystem windowSystem = new("CrucibleSux");
    private readonly MainWindow mainWindow;
    private readonly BestiaryWatcher bestiary;
    private readonly Session session;

    public Plugin()
    {
        this.config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var data = new CrucibleData(DataManager);
        this.session = new Session(new CharacterReader(PlayerState, UnlockState, DataManager, this.config), new PluginInventory(PluginInterface, Log), Log);
        this.bestiary = new BestiaryWatcher(AddonLifecycle, PlayerState, Log, this.config, this.Save, data.Beasts);
        this.mainWindow = new MainWindow(this.session, data, this.config, this.bestiary, this.Save);
        this.windowSystem.AddWindow(this.mainWindow);

        var help = new CommandInfo(this.OnCommand) { HelpMessage = "Open Crucible SUX. \"/sux check\" prints what you can clear in one sitting." };
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
        this.bestiary.Dispose();
    }

    private void Save() => PluginInterface.SavePluginConfig(this.config);

    private void OnUpdate(IFramework framework) => this.session.Update();

    private void OnCommand(string command, string args)
    {
        if (!args.Trim().Equals("check", StringComparison.OrdinalIgnoreCase))
        {
            this.mainWindow.Toggle();
            return;
        }

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
