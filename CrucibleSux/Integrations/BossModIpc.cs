using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Integrations;

/// <summary>
/// BossMod's settings IPC, "BossMod.Configuration", which both BossMods register (read from awgil's fff7f6e and BossMod
/// Reborn's 343f764, 2026-10-18). Only the setting below is changed.
/// </summary>
/// <param name="warn">Adds a warning to the combat log.</param>
public sealed class BossModIpc(IDalamudPluginInterface pluginInterface, IPluginLog log, Action<string> warn)
{
    private readonly ICallGateSubscriber<List<string>, bool, List<string>> configuration =
        pluginInterface.GetIpcSubscriber<List<string>, bool, List<string>>("BossMod.Configuration");

    /// <summary>
    /// Sets ActionTweaksConfig.PreventMovingWhileCasting back to false, right after AutoDuty.Run. AutoDuty's "auto-manage
    /// BossMod AI" turns it on inside Run and does not set it again during the run; the original BossMod obeys it even while
    /// the rotation plugin fights, so the character could not dodge while casting. When the run stops, AutoDuty restores
    /// the player's own value. The original BossMod saves the change anyway; BossMod Reborn does not.
    /// </summary>
    public void AllowMovingWhileCasting()
    {
        try
        {
            var reply = this.configuration.InvokeFunc(["ActionTweaksConfig", "PreventMovingWhileCasting", "false"], false);
            log.Information($"BossMod.Configuration PreventMovingWhileCasting false: {string.Join(" | ", reply)}");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "BossMod.Configuration failed");
            warn($"BossMod did not take PreventMovingWhileCasting=false ({ex.GetType().Name}): the character may stand still while casting.");
        }
    }
}
