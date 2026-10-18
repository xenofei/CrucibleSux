using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Integrations;

/// <summary>
/// vnavmesh pathing over its IPC (awgil/ffxiv_navmesh <c>IPCProvider.cs</c>). <c>Nav.PathfindCancelAll</c> reloads the mesh,
/// so a move is cancelled with <c>Path.Stop</c> only. vnavmesh never mounts; the caller mounts first.
/// </summary>
public sealed class NavmeshIpc(IDalamudPluginInterface pi, IPluginLog log)
{
    private readonly ICallGateSubscriber<bool> isReady = pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
    private readonly ICallGateSubscriber<bool> pathfindInProgress = pi.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
    private readonly ICallGateSubscriber<Vector3, bool, bool> moveTo = pi.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
    private readonly ICallGateSubscriber<Vector3, bool, float, bool> moveCloseTo = pi.GetIpcSubscriber<Vector3, bool, float, bool>("vnavmesh.SimpleMove.PathfindAndMoveCloseTo");
    private readonly ICallGateSubscriber<bool> isRunning = pi.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
    private readonly ICallGateSubscriber<object> stop = pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> nearestPoint = pi.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
    private readonly ICallGateSubscriber<List<Vector3>> waypoints = pi.GetIpcSubscriber<List<Vector3>>("vnavmesh.Path.ListWaypoints");

    public bool IsReady => this.Call(() => this.isReady.InvokeFunc(), false);

    /// <summary>True while a path is being computed or walked.</summary>
    public bool IsBusy => this.Call(() => this.pathfindInProgress.InvokeFunc() || this.isRunning.InvokeFunc(), false);

    public bool MoveTo(Vector3 destination, bool fly = false) => this.Call(() => this.moveTo.InvokeFunc(destination, fly), false);

    /// <summary>Like <see cref="MoveTo"/>, but the path ends once the player is within <paramref name="range"/> of the destination.</summary>
    public bool MoveCloseTo(Vector3 destination, float range, bool fly = false) => this.Call(() => this.moveCloseTo.InvokeFunc(destination, fly, range), false);

    public void Stop() => this.Call(() => { this.stop.InvokeAction(); return true; }, false);

    /// <summary>The walkable point nearest <paramref name="p"/>, searching far up and down (map coordinates carry no height).</summary>
    public Vector3? Floor(Vector3 p) => this.Call(() => this.nearestPoint.InvokeFunc(p, 10f, 1000f), null);

    /// <summary>The waypoints of the path being walked, whoever started it (AutoDuty too); empty when none or unknown.</summary>
    public IReadOnlyList<Vector3> Waypoints => this.Call<IReadOnlyList<Vector3>>(() => this.waypoints.InvokeFunc(), []);

    private T Call<T>(Func<T> f, T fallback)
    {
        try
        {
            return f();
        }
        catch (Exception ex)
        {
            log.Verbose(ex, "vnavmesh IPC call failed");
            return fallback;
        }
    }
}
