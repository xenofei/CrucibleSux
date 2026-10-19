using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrucibleSux.Core;
using CrucibleSux.Integrations;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Automation;

/// <summary>
/// Steps out of enemy area attacks during the journey (<see cref="AoeDodge"/>): every hostile cast with an area, from any
/// enemy, placed by <see cref="CastAreas"/>; it walks to the nearest reachable spot clear of every one, keeping its target
/// in reach where it can, until they go off. Boards are left to BossMod's AI.
/// </summary>
public sealed class AoeDodger(IObjectTable objects, IDataManager data, NavmeshIpc nav, GameActions game)
{
    private const float Range = 50f;

    /// <summary>A cast about to go off is no longer worth stepping for.</summary>
    private const float TooLate = 0.2f;

    private const int SpotsTried = 8;

    /// <summary>The game's melee reach from the target's hitbox, less a margin, as the chase uses it.</summary>
    private const float Reach = 3f - 0.5f;

    private readonly CastAreas areas = new(data, objects);
    private readonly List<AoeThreat> threats = [];
    private Vector3? spot;
    private string? noSpotFor;
    private DateTime nextHead;

    /// <summary>The cast being stepped out of, or null.</summary>
    public string? Dodging { get; private set; }

    /// <summary>True while it moves the character out of an area; the caller does nothing else meanwhile.</summary>
    /// <param name="keepTarget">Prefer spots in reach of the target (fighting); false while fleeing from it.</param>
    public bool Tick(bool keepTarget = true)
    {
        this.threats.Clear();
        if (game.Me is not IBattleChara me || game.InFlight)
            return this.Clear();
        string? name = null;
        foreach (var o in objects)
        {
            if (o is not IBattleNpc npc || !GameActions.IsHostile(npc) || !npc.IsCasting || npc.TotalCastTime - npc.CurrentCastTime < TooLate
                || Vector3.Distance(npc.Position, me.Position) > Range || this.areas.Read(npc) is not { } threat)
                continue;
            this.threats.Add(threat);
            name ??= this.areas.Info(npc.CastActionId).Name;
        }

        if (!AoeDodge.InDanger(this.threats, Flat(me.Position)))
        {
            this.noSpotFor = null;
            return this.Clear();
        }
        this.Dodging = name;
        // Keep heading for the spot already chosen while it stays clear.
        if (this.spot is { } chosen && !AoeDodge.InDanger(this.threats, Flat(chosen)))
        {
            this.Head(chosen);
            return true;
        }
        this.spot = null;
        // A melee character steps aside rather than away: spots that keep the target in reach come first. If none of those
        // can be walked to, the nearest clear spot anywhere will do.
        var target = keepTarget ? me.TargetObject as IBattleNpc : null;
        if (target is not null)
            this.spot = this.FirstReachable(AoeDodge.SafeSpots(this.threats, Flat(me.Position), Flat(target.Position), Reach + target.HitboxRadius)
                .Where(p => Vector2.Distance(p, Flat(target.Position)) <= Reach + target.HitboxRadius), me.Position.Y);
        this.spot ??= this.FirstReachable(AoeDodge.SafeSpots(this.threats, Flat(me.Position)), me.Position.Y);
        if (this.spot is not { } go)
        {
            if (name != this.noSpotFor)
                Plugin.Trace($"Journey: inside {name} with no reachable spot clear of it ({this.threats.Count} areas being cast)");
            this.noSpotFor = name;
            return this.Clear();
        }
        this.noSpotFor = null;
        this.nextHead = default;
        this.Head(go);
        return true;
    }

    /// <summary>
    /// Walks to <paramref name="spot"/>, replacing any other path: one still being computed (a chase, a flight) is not
    /// cancelled by a stop, and would win, so the move is asked for again on later ticks until the path ends there.
    /// </summary>
    private void Head(Vector3 spot)
    {
        var path = nav.IsBusy ? nav.Waypoints : [];
        if (path.Count > 0 && Vector3.Distance(path[^1], spot) <= AoeDodge.Clearance)
            return;
        var now = DateTime.UtcNow;
        if (nav.Pathfinding || now < this.nextHead)
            return;
        this.nextHead = now + TimeSpan.FromSeconds(1);
        if (nav.IsBusy)
            nav.Stop();
        nav.MoveTo(spot);
    }

    private Vector3? FirstReachable(IEnumerable<Vector2> spots, float y)
    {
        foreach (var p in spots.Take(SpotsTried))
        {
            if (this.Reachable(p, y) is { } r)
                return r;
        }
        return null;
    }

    /// <summary>True while an enemy area is being cast near the character (as of the last <see cref="Tick"/>).</summary>
    public bool Active => this.threats.Count > 0;

    /// <summary>
    /// Where to stand to fight <paramref name="target"/> without walking into an area being cast: a reachable point in melee
    /// reach of it that no cast covers, nearest first. Null when none is clear; the caller then waits outside.
    /// </summary>
    public Vector3? SafeApproach(IGameObject target)
    {
        if (game.Me is not { } me)
            return null;
        int tried = 0;
        foreach (var p in AoeDodge.Approach(this.threats, Flat(me.Position), Flat(target.Position), Reach + target.HitboxRadius))
        {
            if (++tried > SpotsTried)
                break;
            if (this.Reachable(p, target.Position.Y) is { } r)
                return r;
        }
        return null;
    }

    /// <summary>The walkable point a path reaches at <paramref name="p"/>, when it is still clear of every area.</summary>
    private Vector3? Reachable(Vector2 p, float y) =>
        nav.Reachable(new Vector3(p.X, y, p.Y)) is { } r && Vector2.Distance(Flat(r), p) < AoeDodge.Clearance && !AoeDodge.InDanger(this.threats, Flat(r))
            ? r
            : null;

    private bool Clear()
    {
        this.spot = null;
        this.Dodging = null;
        return false;
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
