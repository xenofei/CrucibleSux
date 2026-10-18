using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Integrations;

/// <summary>
/// Rotation Solver Reborn's IPC (RotationSolver/IPC/IPCProvider.cs, commit 3d86903): switches its auto-rotation on and
/// off around fights the progression runner starts. Its own Beastmaster rotation does the fighting.
/// </summary>
/// <param name="note">Writes a line into the combat log: each mode sent, so a log shows what RSR was asked to do.</param>
public sealed class RotationSolverIpc(IDalamudPluginInterface pi, IPluginLog log, Action<string> note)
{
    /// <summary>How long RSR may report itself off after being asked to fight before the mode is sent again.</summary>
    private static readonly TimeSpan ResendAfter = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(1);

    /// <summary>RSR's <c>StateCommandType</c> (RotationSolver.Basic/Data/RSCommandType.cs); sent by value over IPC.</summary>
    public enum Mode : byte
    {
        Off = 0,

        /// <summary>Picks its own targets.</summary>
        Auto = 1,

        /// <summary>Attacks only the current target.</summary>
        Manual = 3,
    }

    private readonly ICallGateSubscriber<Mode, object> changeMode = pi.GetIpcSubscriber<Mode, object>("RotationSolverReborn.ChangeOperatingMode");
    private readonly ICallGateSubscriber<object> targetFreelyOn = pi.GetIpcSubscriber<object>("RotationSolverReborn.EnableTargetFreelyOverride");
    private readonly ICallGateSubscriber<object> targetFreelyOff = pi.GetIpcSubscriber<object>("RotationSolverReborn.DisableTargetFreelyOverride");
    private readonly ICallGateSubscriber<bool> active = pi.GetIpcSubscriber<bool>("RotationSolverReborn.AutorotationActive");
    private DateTime sentAt;
    private DateTime nextCheck;
    private Mode? current;
    private bool freely;

    /// <summary>Sets the mode; repeated calls with the same mode are not resent (see <see cref="Reassert"/>).</summary>
    public bool Set(Mode mode, bool targetFreely = false)
    {
        try
        {
            if (this.freely != targetFreely)
            {
                (targetFreely ? this.targetFreelyOn : this.targetFreelyOff).InvokeAction();
                this.freely = targetFreely;
            }
            // RSR turns itself off on its own (between areas, after combat, on a class change): a fighting mode it does not
            // hold any more is sent again.
            var now = DateTime.UtcNow;
            if (this.current == mode && mode != Mode.Off && now - this.sentAt > ResendAfter && now >= this.nextCheck)
            {
                this.nextCheck = now + CheckEvery;
                if (this.Active() == false)
                {
                    note($"Rotation Solver Reborn is off though it was asked for {mode}: asking again");
                    this.current = null;
                }
            }
            if (this.current != mode)
            {
                this.changeMode.InvokeAction(mode);
                this.current = mode;
                this.sentAt = now;
                note(mode == Mode.Off ? "Rotation Solver Reborn: Off" : $"Rotation Solver Reborn: {mode}{(targetFreely ? ", picking its own targets" : string.Empty)}");
            }
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Rotation Solver Reborn IPC call failed");
            this.current = null;
            return false;
        }
    }

    /// <summary>
    /// Turns the rotation off and drops the target override whatever this class last sent: the player or another plugin
    /// (AutoDuty during a board) may have changed them since.
    /// </summary>
    public void ForceOff()
    {
        this.current = null;
        this.freely = true;
        this.Set(Mode.Off);
    }

    /// <summary>
    /// Whether RSR's auto-rotation is on (its <c>AutorotationActive</c>, since August 2026); null when it does not answer.
    /// </summary>
    public bool? Active()
    {
        try
        {
            return this.active.InvokeFunc();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Forgets the mode last sent, so the next <see cref="Set"/> sends it again. RSR switches itself off on its own (30 s
    /// after combat, between areas) and has no IPC to read its state back, so this is called when a fight starts and after
    /// a zone change.
    /// </summary>
    public void Reassert() => this.current = null;

    /// <summary>Turns the rotation off and drops the target override; safe to call when nothing was set.</summary>
    public void Release()
    {
        if (this.current is null or Mode.Off && !this.freely)
            return;
        this.Set(Mode.Off);
    }
}
