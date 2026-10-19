using System.Collections.Generic;
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

    /// <summary>The cast being stepped out of, or null.</summary>
    public string? Dodging { get; private set; }

    /// <summary>True while it moves the character out of an area; the caller does nothing else meanwhile.</summary>
    public bool Tick()
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
            return this.Clear();
        this.Dodging = name;
        // Keep heading for the spot already chosen while it stays clear.
        if (this.spot is { } chosen && !AoeDodge.InDanger(this.threats, Flat(chosen)))
        {
            if (!nav.IsBusy)
                nav.MoveTo(chosen);
            return true;
        }
        this.spot = null;
        // A melee character steps aside rather than away: spots that keep the target in reach come first.
        var target = me.TargetObject as IBattleNpc;
        var near = target is null ? (Vector2?)null : Flat(target.Position);
        int tried = 0;
        foreach (var p in AoeDodge.SafeSpots(this.threats, Flat(me.Position), near, target is null ? 0 : Reach + target.HitboxRadius))
        {
            if (++tried > SpotsTried)
                break;
            if (this.Reachable(p, me.Position.Y) is { } r)
            {
                this.spot = r;
                break;
            }
        }
        if (this.spot is not { } go)
            return this.Clear();
        Plugin.Trace($"Journey: stepping out of {name}");
        if (nav.IsBusy)
            nav.Stop();
        nav.MoveTo(go);
        return true;
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
