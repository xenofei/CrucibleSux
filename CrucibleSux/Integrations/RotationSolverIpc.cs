using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Integrations;

/// <summary>
/// Rotation Solver Reborn's IPC (RotationSolver/IPC/IPCProvider.cs, commit 3d86903): switches its auto-rotation on and
/// off around fights the progression runner starts. Its own Beastmaster rotation does the fighting.
/// </summary>
public sealed class RotationSolverIpc(IDalamudPluginInterface pi, IPluginLog log)
{
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
    private Mode? current;
    private bool freely;

    /// <summary>Sets the mode; repeated calls with the same mode are not resent.</summary>
    public bool Set(Mode mode, bool targetFreely = false)
    {
        try
        {
            if (this.freely != targetFreely)
            {
                (targetFreely ? this.targetFreelyOn : this.targetFreelyOff).InvokeAction();
                this.freely = targetFreely;
            }
            if (this.current != mode)
            {
                this.changeMode.InvokeAction(mode);
                this.current = mode;
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

    /// <summary>Turns the rotation off and drops the target override; safe to call when nothing was set.</summary>
    public void Release()
    {
        if (this.current is null or Mode.Off && !this.freely)
            return;
        this.Set(Mode.Off);
    }
}
