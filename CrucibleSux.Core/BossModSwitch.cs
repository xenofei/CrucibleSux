namespace CrucibleSux.Core;

/// <summary>What the switch reads from Dalamud each tick.</summary>
/// <param name="RebornIpcReady">Something answers on the IPC names both BossMods register ("BossMod.*").</param>
public sealed record SwitchView(bool OriginalInstalled, bool OriginalLoaded, bool RebornLoaded, bool RebornIpcReady);

public enum SwitchPhase
{
    Idle,
    TurningOff,
    RebornUnloading,
    RebornLoading,
}

public enum SwitchOutcome
{
    Idle,
    Waiting,
    Ready,
    Failed,
}

/// <param name="Commands">Chat commands to send now, in order.</param>
public sealed record SwitchStep(SwitchOutcome Outcome, IReadOnlyList<string> Commands, string? Error = null);

/// <summary>
/// Turns the original BossMod off for a run and back on afterwards, through Dalamud's temporary toggles: they never change
/// the saved plugin settings and reset when the game restarts. Both BossMods register the same "BossMod.*" IPC names, and
/// Dalamud clears a name whenever any plugin unregisters it, so unloading the original leaves BossMod Reborn unreachable
/// for AutoDuty. When that happens the switch reloads BossMod Reborn once.
/// </summary>
public sealed class BossModSwitch(bool turnedOff = false)
{
    public const string Original = "BossMod";
    public const string Reborn = "BossModReborn";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private DateTime deadline;

    /// <summary>BossMod Reborn was turned off by the switch and has not come back yet.</summary>
    private bool rebornOff;

    public SwitchPhase Phase { get; private set; }

    /// <summary>The switch turned the original BossMod off and owes a <see cref="Restore"/>.</summary>
    public bool TurnedOff { get; private set; } = turnedOff;

    /// <summary>The last switch had to reload BossMod Reborn.</summary>
    public bool RebornReloaded { get; private set; }

    public static string Disable(string internalName) => $"/xldisableplugintemp {internalName}";

    public static string Enable(string internalName) => $"/xlenableplugintemp {internalName}";

    /// <summary>Starts turning the original BossMod off; returns the commands to send (none when it is not loaded or a switch is under way).</summary>
    public IReadOnlyList<string> Begin(SwitchView view, DateTime now)
    {
        if (this.Phase != SwitchPhase.Idle || !view.OriginalLoaded)
            return [];
        this.Phase = SwitchPhase.TurningOff;
        this.TurnedOff = true;
        this.RebornReloaded = false;
        this.deadline = now + Timeout;
        return [Disable(Original)];
    }

    public SwitchStep Tick(SwitchView view, DateTime now)
    {
        if (this.Phase == SwitchPhase.Idle)
            return new(SwitchOutcome.Idle, []);
        if (now >= this.deadline)
        {
            var phase = this.Phase;
            this.Phase = SwitchPhase.Idle;
            return new(SwitchOutcome.Failed, [], phase == SwitchPhase.TurningOff
                ? "The original BossMod did not turn off within 30 s. Dalamud switches a plugin only when it is in exactly one collection " +
                  "(see /xlplugins, Collections). Disable it yourself and start again."
                : "BossMod Reborn did not reload within 30 s, so AutoDuty cannot reach it. Reload it in /xlplugins and start again.");
        }

        switch (this.Phase)
        {
            case SwitchPhase.TurningOff when view.OriginalLoaded:
                return new(SwitchOutcome.Waiting, []);
            case SwitchPhase.TurningOff when view.RebornLoaded && !view.RebornIpcReady:
                this.Phase = SwitchPhase.RebornUnloading;
                this.rebornOff = true;
                this.RebornReloaded = true;
                return new(SwitchOutcome.Waiting, [Disable(Reborn)]);
            case SwitchPhase.TurningOff:
                this.Phase = SwitchPhase.Idle;
                return new(SwitchOutcome.Ready, []);
            case SwitchPhase.RebornUnloading when view.RebornLoaded:
                return new(SwitchOutcome.Waiting, []);
            case SwitchPhase.RebornUnloading:
                this.Phase = SwitchPhase.RebornLoading;
                return new(SwitchOutcome.Waiting, [Enable(Reborn)]);
            case SwitchPhase.RebornLoading when view.RebornLoaded && view.RebornIpcReady:
                this.Phase = SwitchPhase.Idle;
                this.rebornOff = false;
                return new(SwitchOutcome.Ready, []);
            default:
                return new(SwitchOutcome.Waiting, []);
        }
    }

    /// <summary>
    /// Turns back on what the switch turned off and is still off; returns the commands to send. A plugin the player turned
    /// back on (or uninstalled) meanwhile is left alone.
    /// </summary>
    public IReadOnlyList<string> Restore(SwitchView view)
    {
        var commands = new List<string>();
        if (this.rebornOff && !view.RebornLoaded)
            commands.Add(Enable(Reborn));
        if (this.TurnedOff && view.OriginalInstalled && !view.OriginalLoaded)
            commands.Add(Enable(Original));
        this.Phase = SwitchPhase.Idle;
        this.TurnedOff = false;
        this.rebornOff = false;
        return commands;
    }
}
