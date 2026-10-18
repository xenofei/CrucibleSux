namespace CrucibleSux.Core;

/// <summary>What the switch reads from Dalamud each tick.</summary>
/// <param name="OffInstalled">The BossMod to turn off is installed.</param>
/// <param name="KeptIpcReady">Something answers on the IPC names both BossMods register ("BossMod.*").</param>
public sealed record SwitchView(bool OffInstalled, bool OffLoaded, bool KeptLoaded, bool KeptIpcReady);

public enum SwitchPhase
{
    Idle,
    TurningOff,
    KeptUnloading,
    KeptLoading,
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
/// Turns the BossMod the player does not use off for a run and back on afterwards, through Dalamud's temporary toggles:
/// they never change the saved plugin settings and reset when the game restarts. Both BossMods register the same
/// "BossMod.*" IPC names, and Dalamud clears a name whenever any plugin unregisters it, so unloading one leaves the kept
/// one unreachable for AutoDuty. When that happens the switch reloads the kept one once.
/// </summary>
/// <param name="off">The BossMod to turn off: <see cref="Original"/> or <see cref="Reborn"/>.</param>
public sealed class BossModSwitch(string off = BossModSwitch.Original, bool turnedOff = false)
{
    public const string Original = "BossMod";
    public const string Reborn = "BossModReborn";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private DateTime deadline;

    /// <summary>The kept BossMod was turned off by the switch and has not come back yet.</summary>
    private bool keptOff;

    /// <summary>The BossMod this switch turns off.</summary>
    public string Off { get; } = off;

    /// <summary>The BossMod the player uses, which stays on.</summary>
    public string Kept { get; } = off == Original ? Reborn : Original;

    public SwitchPhase Phase { get; private set; }

    /// <summary>The switch turned <see cref="Off"/> off and owes a <see cref="Restore"/>.</summary>
    public bool TurnedOff { get; private set; } = turnedOff;

    /// <summary>The last switch had to reload <see cref="Kept"/>.</summary>
    public bool Reloaded { get; private set; }

    public static string Disable(string internalName) => $"/xldisableplugintemp {internalName}";

    public static string Enable(string internalName) => $"/xlenableplugintemp {internalName}";

    /// <summary>
    /// How long after the turned-off BossMod reports unloaded the shared IPC is judged: Dalamud can report a plugin
    /// unloaded before its Dispose has unregistered the names.
    /// </summary>
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);

    private DateTime? offGoneAt;

    /// <summary>The BossMod a run turns off for <paramref name="choice"/>: the one the player does not use (the original while unset).</summary>
    public static string OffFor(BossModChoice choice) => choice == BossModChoice.Original ? Reborn : Original;

    /// <summary>The BossMod's name for messages: "the original BossMod" or "BossMod Reborn".</summary>
    public static string Name(string internalName) => internalName == Original ? "the original BossMod" : "BossMod Reborn";

    /// <summary>Starts turning <see cref="Off"/> off; returns the commands to send (none when it is not loaded or a switch is under way).</summary>
    public IReadOnlyList<string> Begin(SwitchView view, DateTime now)
    {
        if (this.Phase != SwitchPhase.Idle || !view.OffLoaded)
            return [];
        this.Phase = SwitchPhase.TurningOff;
        this.TurnedOff = true;
        this.Reloaded = false;
        this.deadline = now + Timeout;
        this.offGoneAt = null;
        return [Disable(this.Off)];
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
                ? $"Crucible SUX could not turn {Name(this.Off)} off within 30 s. Dalamud switches a plugin only when it is in exactly one " +
                  "collection (see /xlplugins, Collections). Disable it yourself and start again."
                : $"Crucible SUX could not reload {Name(this.Kept)} within 30 s, so AutoDuty cannot reach it. Reload it in /xlplugins and start again.");
        }

        if (this.Phase == SwitchPhase.TurningOff && !view.OffLoaded)
        {
            this.offGoneAt ??= now;
            if (now - this.offGoneAt < Settle)
                return new(SwitchOutcome.Waiting, []);
        }

        switch (this.Phase)
        {
            case SwitchPhase.TurningOff when view.OffLoaded:
                return new(SwitchOutcome.Waiting, []);
            case SwitchPhase.TurningOff when view.KeptLoaded && !view.KeptIpcReady:
                this.Phase = SwitchPhase.KeptUnloading;
                this.keptOff = true;
                this.Reloaded = true;
                return new(SwitchOutcome.Waiting, [Disable(this.Kept)]);
            case SwitchPhase.TurningOff:
                this.Phase = SwitchPhase.Idle;
                return new(SwitchOutcome.Ready, []);
            case SwitchPhase.KeptUnloading when view.KeptLoaded:
                return new(SwitchOutcome.Waiting, []);
            case SwitchPhase.KeptUnloading:
                this.Phase = SwitchPhase.KeptLoading;
                return new(SwitchOutcome.Waiting, [Enable(this.Kept)]);
            case SwitchPhase.KeptLoading when view.KeptLoaded && view.KeptIpcReady:
                this.Phase = SwitchPhase.Idle;
                this.keptOff = false;
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
        if (this.keptOff && !view.KeptLoaded)
            commands.Add(Enable(this.Kept));
        if (this.TurnedOff && view.OffInstalled && !view.OffLoaded)
            commands.Add(Enable(this.Off));
        this.Phase = SwitchPhase.Idle;
        this.TurnedOff = false;
        this.keptOff = false;
        return commands;
    }
}
