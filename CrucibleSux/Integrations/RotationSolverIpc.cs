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

    /// <summary>A call that failed is not tried again sooner than this, so a broken RSR does not fill the log.</summary>
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);

    /// <summary>Resends after the first in this window are counted, not each written to the log.</summary>
    private static readonly TimeSpan ResendNoteEvery = TimeSpan.FromSeconds(30);

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
    private DateTime failedUntil;
    private string? lastFailure;
    private DateTime resendNotedAt;
    private int resendsUnnoted;
    private Mode? current;

    /// <summary>The target override last sent; null when unknown, so the next <see cref="Set"/> sends it.</summary>
    private bool? freely;

    /// <summary>The mode last sent to RSR, or null when it is unknown; for the combat log.</summary>
    public Mode? Requested => this.current;

    /// <summary>Sets the mode; repeated calls with the same mode are not resent (see <see cref="Reassert"/>).</summary>
    public bool Set(Mode mode, bool targetFreely = false)
    {
        var now = DateTime.UtcNow;
        if (now < this.failedUntil)
            return false;
        try
        {
            // RSR turns itself off on its own (between areas, after combat, on a class change): a fighting mode it does not
            // hold any more is sent again.
            if (this.current == mode && mode != Mode.Off && now - this.sentAt > ResendAfter && now >= this.nextCheck)
            {
                this.nextCheck = now + CheckEvery;
                if (this.Active() == false)
                {
                    this.NoteResend(now, mode);
                    this.current = null;
                    this.freely = null;
                }
            }
            // The mode first: an RSR build without the override still gets switched on.
            if (this.current != mode)
            {
                this.changeMode.InvokeAction(mode);
                this.current = mode;
                this.sentAt = now;
                note(mode == Mode.Off ? "Rotation Solver Reborn: Off" : $"Rotation Solver Reborn: {mode}{(targetFreely ? ", picking its own targets" : string.Empty)}");
            }
        }
        catch (Exception ex)
        {
            this.Failed(now, $"the {mode} call", ex);
            this.current = null;
            return false;
        }
        try
        {
            if (this.freely != targetFreely)
            {
                (targetFreely ? this.targetFreelyOn : this.targetFreelyOff).InvokeAction();
                this.freely = targetFreely;
            }
        }
        catch (Exception ex)
        {
            this.Failed(now, "the target override", ex);
            this.freely = targetFreely;
        }
        this.lastFailure = null;
        return true;
    }

    /// <summary>
    /// Turns the rotation off and drops the target override whatever this class last sent: the player or another plugin
    /// (AutoDuty during a board) may have changed them since.
    /// </summary>
    public void ForceOff()
    {
        this.current = null;
        this.freely = null;
        this.failedUntil = default;
        this.Set(Mode.Off);
    }

    /// <summary>
    /// Whether RSR's auto-rotation is on (its <c>AutorotationActive</c>, since August 2026); null when it does not answer
    /// (an older RSR).
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
    /// Forgets the mode and override last sent, so the next <see cref="Set"/> sends them again. RSR switches itself off on
    /// its own (30 s after combat, between areas), so this is called when a fight starts and after a zone change.
    /// </summary>
    public void Reassert()
    {
        this.current = null;
        this.freely = null;
    }

    /// <summary>Turns the rotation off and drops the target override; safe to call when nothing was set.</summary>
    public void Release()
    {
        if (this.current == Mode.Off && this.freely == false)
            return;
        this.Set(Mode.Off);
    }

    private void NoteResend(DateTime now, Mode mode)
    {
        if (now - this.resendNotedAt < ResendNoteEvery)
        {
            this.resendsUnnoted++;
            return;
        }
        string more = this.resendsUnnoted > 0 ? $" ({this.resendsUnnoted} more times in the last {ResendNoteEvery.TotalSeconds:0} s)" : string.Empty;
        note($"Rotation Solver Reborn is off though it was asked for {mode}: asking again{more}");
        this.resendNotedAt = now;
        this.resendsUnnoted = 0;
    }

    private void Failed(DateTime now, string what, Exception ex)
    {
        this.failedUntil = now + RetryAfter;
        string text = $"Rotation Solver Reborn did not take {what}: {ex.Message}";
        if (text == this.lastFailure)
            return;
        this.lastFailure = text;
        log.Warning(ex, "Rotation Solver Reborn IPC call failed");
        note(text);
    }
}
