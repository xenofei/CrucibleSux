using System;
using System.Collections.Generic;
using System.Linq;
using CrucibleSux.Core;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Integrations;

/// <summary>
/// AutoDuty (erdelf's fork) as the board executor, driven through its IPC only: AutoDuty has no license, so none of its
/// code is copied here. Calls and setting names were read from its source at commit 4a0dfdc (2026-10-09). A run pushes
/// temporary setting overrides, which AutoDuty restores when it stops, so the player's own AutoDuty settings survive.
/// </summary>
public sealed class AutoDutyIpc
{
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<bool> isStopped;
    private readonly ICallGateSubscriber<uint, bool> contentHasPath;
    private readonly ICallGateSubscriber<object, bool> pushOverrides;
    private readonly ICallGateSubscriber<bool> popOverrides;
    private readonly ICallGateSubscriber<uint, int, bool, object> run;
    private readonly ICallGateSubscriber<object> stop;
    private bool pushed;

    public AutoDutyIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.log = log;
        this.isStopped = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped");
        this.contentHasPath = pluginInterface.GetIpcSubscriber<uint, bool>("AutoDuty.ContentHasPath");
        this.pushOverrides = pluginInterface.GetIpcSubscriber<object, bool>("AutoDuty.PushConfigOverrides");
        this.popOverrides = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.PopConfigOverrides");
        this.run = pluginInterface.GetIpcSubscriber<uint, int, bool, object>("AutoDuty.Run");
        this.stop = pluginInterface.GetIpcSubscriber<object>("AutoDuty.Stop");
    }

    /// <summary>Null when AutoDuty answers, is idle and knows every board; otherwise what is wrong.</summary>
    public string? Problem(IEnumerable<BoardInfo> boards)
    {
        try
        {
            if (!this.isStopped.InvokeFunc())
                return "AutoDuty is running: stop it first.";
            var missing = boards.Where(b => !this.contentHasPath.InvokeFunc(b.Territory)).Select(b => b.Name).ToList();
            return missing.Count == 0 ? null : $"This AutoDuty build cannot run: {string.Join(", ", missing)}.";
        }
        catch (Exception ex)
        {
            return this.Explain(ex);
        }
    }

    /// <summary>Whether AutoDuty is running; null when it does not answer.</summary>
    public bool? IsRunning()
    {
        try
        {
            bool running = !this.isStopped.InvokeFunc();
            // Once a run is under way, AutoDuty restores the pushed settings itself when it stops.
            if (running)
                this.pushed = false;
            return running;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Starts one run of a board. Returns null on success, else why it could not start.</summary>
    /// <param name="rotationPlugin">The rotation plugin AutoDuty should switch on in the board: an AutoDuty RotationPlugin name.</param>
    public string? Start(BoardInfo board, string rotationPlugin)
    {
        var overrides = new Dictionary<string, string>
        {
            ["Meta.DutyModeEnum"] = "Crucible",
            ["Meta.Crucible.Degree"] = "Standard",
            ["DutyConfig.AutoExitDuty"] = "true",
            ["DutyConfig.AutoManageRotationPluginState"] = "true",
            ["DutyConfig.AutoManageBossModAISettings"] = "true",
            ["DutyConfig.UsingAlternativeRotationPlugin"] = "false",
            ["DutyConfig.RotationPlugin"] = rotationPlugin,
        };
        try
        {
            if (!this.pushOverrides.InvokeFunc(overrides))
                return "AutoDuty rejected the run settings; its settings may have changed since this Crucible SUX build.";
            this.pushed = true;
            // One loop, bare mode: AutoDuty's own before, between and after-loop actions (repairs, logout...) stay off.
            this.run.InvokeAction(board.Territory, 1, true);
            this.log.Information($"AutoDuty asked to run the {board.Name} (territory {board.Territory}) with {rotationPlugin}");
            return null;
        }
        catch (Exception ex)
        {
            this.PopOverrides();
            return this.Explain(ex);
        }
    }

    public void Stop()
    {
        try
        {
            this.stop.InvokeAction();
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, "AutoDuty.Stop failed");
        }
    }

    /// <summary>
    /// Restores AutoDuty's settings when this adapter pushed them and AutoDuty never ran: AutoDuty restores them itself
    /// when a run stops, and a pop restores every active override, another plugin's included.
    /// </summary>
    public void PopOverrides()
    {
        if (!this.pushed)
            return;
        this.pushed = false;
        try
        {
            this.popOverrides.InvokeFunc();
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, "AutoDuty.PopConfigOverrides failed");
        }
    }

    /// <summary>AutoDuty's RotationPlugin setting for a catalog rotation plugin, or null when AutoDuty cannot drive it here.</summary>
    public static string? RotationPluginName(DependencyInfo? engine) => engine?.InternalName switch
    {
        "RotationSolver" => "RotationSolverReborn",
        "BossModReborn" => "BossMod",
        _ => null,
    };

    private string Explain(Exception ex)
    {
        if (ex is IpcNotReadyError)
            return "AutoDuty is not loaded.";
        this.log.Warning(ex, "AutoDuty IPC call failed");
        return ex is IpcError ? "AutoDuty's IPC does not match this Crucible SUX build." : $"AutoDuty failed: {ex.Message}";
    }
}
