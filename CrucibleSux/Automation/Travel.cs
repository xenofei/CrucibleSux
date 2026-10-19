using System;
using System.Linq;
using System.Numerics;
using CrucibleSux.Core;
using CrucibleSux.Integrations;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace CrucibleSux.Automation;

public enum TravelState
{
    Moving,
    Arrived,
    Failed,
}

/// <summary>
/// Gets the player to a place: teleports to the attuned aetheryte nearest the destination when it is in another zone, or
/// in this zone when that saves a long trip; mounts for long trips (a flying mount, and flies, where this character can
/// fly); and goes the last stretch with vnavmesh. Call <see cref="Tick"/> every frame until it stops returning
/// <see cref="TravelState.Moving"/>.
/// </summary>
public sealed unsafe class Travel(GameActions game, NavmeshIpc nav, IClientState client, IDataManager data)
{
    private const float MountBeyond = 40f;
    private const float RepathBeyond = 5f;

    /// <summary>A trip in this zone longer than this teleports when an aetheryte saves <see cref="TeleportSaves"/> of it.</summary>
    private const float TeleportWithinBeyond = 400f;
    private const float TeleportSaves = 300f;
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TeleportTimeout = TimeSpan.FromSeconds(40);

    private DateTime nextTry;
    private DateTime? teleportedAt;

    /// <summary>The last path asked for was a flight; one that ends short of the destination turns the trip to the ground.</summary>
    private bool flying;

    /// <summary>This trip goes on the ground: a flight to it stopped short (a ledge, an overhang, a no-fly pocket).</summary>
    private bool groundOnly;

    /// <summary>Where and when the character last made progress on this trip, to notice it wedged in place.</summary>
    private (DateTime At, Vector3 Pos)? progress;

    private int stuck;

    /// <summary>Wedged even on the ground: the trip teleports out to the aetheryte nearest the destination.</summary>
    private bool unstickTeleport;

    private static readonly TimeSpan StuckAfter = TimeSpan.FromSeconds(4);

    /// <summary>A trip that still does not move after this many teleports out, or refused paths, gives up.</summary>
    private const int UnsticksAllowed = 2;
    private const int RefusalsAllowed = 5;

    private DateTime lastTick;
    private int unsticks;
    private int refusals;
    private Vector3? resolved;
    private (uint Territory, Vector3 Point)? target;

    public string? Problem { get; private set; }

    /// <summary>Forgets the current trip and stops walking.</summary>
    public void Reset()
    {
        if (this.target is not null)
            nav.Stop();
        this.target = null;
        this.resolved = null;
        this.teleportedAt = null;
        this.flying = this.groundOnly = this.unstickTeleport = false;
        this.progress = null;
        this.stuck = 0;
        this.unsticks = 0;
        this.refusals = 0;
        this.Problem = null;
    }

    /// <summary>One step towards <paramref name="point"/> in <paramref name="territory"/>.</summary>
    /// <param name="heightKnown">False for map coordinates: the floor is looked up on the navmesh on arrival in the zone.</param>
    /// <param name="mount">False for escorts, where the escorted NPC must keep up.</param>
    public TravelState Tick(uint territory, Vector3 point, float radius, bool heightKnown = true, bool mount = true)
    {
        var now = DateTime.UtcNow;
        // A moving target (an NPC, a FATE) re-paths only once it has moved a few yalms; the walk below aims at the new point.
        if (this.target is not { } t || t.Territory != territory || Vector3.Distance(t.Point, point) > RepathBeyond)
        {
            this.Reset();
            this.target = (territory, point);
        }
        if (game.IsBusy || game.Me is null)
            return TravelState.Moving;

        if (client.TerritoryType != territory)
            return this.Teleport(territory, point, now);
        this.teleportedAt = null;

        var dest = heightKnown ? point : this.resolved ??= nav.Floor(point);
        if (dest is not { } d)
        {
            if (!nav.IsReady)
                return TravelState.Moving;
            return this.Fail($"No walkable ground near the destination in this zone ({point.X:0}, {point.Z:0}).");
        }

        float distance = heightKnown ? game.DistanceTo(d) : Flat(game.Me.Position, d);
        if (distance <= radius)
        {
            if (nav.IsBusy)
                nav.Stop();
            // Map coordinates are flat: a flight "arrives" high above the spot. Land there instead of hovering.
            if (!heightKnown && game.InFlight)
            {
                if (now >= this.nextTry)
                {
                    this.nextTry = now + RetryEvery;
                    game.UseGeneral(GameActions.Dismount);
                }
                return TravelState.Moving;
            }
            return TravelState.Arrived;
        }
        // Not getting anywhere (wedged under an overhang, against a rock): land first, then teleport out.
        bool paused = now - this.lastTick > TimeSpan.FromSeconds(1);
        this.lastTick = now;
        if (nav.IsReady && !nav.Pathfinding)
        {
            var here = game.Me.Position;
            if (paused || this.progress is not { } p || Vector3.Distance(p.Pos, here) >= 1f)
            {
                this.progress = (now, here);
            }
            else if (now - p.At > StuckAfter)
            {
                this.progress = (now, here);
                this.stuck++;
                if (nav.IsBusy)
                    nav.Stop();
                if (this.stuck == 1 && game.Mounted && !this.groundOnly)
                {
                    this.groundOnly = true;
                    Plugin.Trace("Travel: not moving; landing and going on the ground");
                }
                else if (this.unsticks >= UnsticksAllowed)
                {
                    return this.Fail($"Still stuck after teleporting out {this.unsticks} times: the destination may be unreachable from here.");
                }
                else
                {
                    this.unstickTeleport = true;
                    Plugin.Trace("Travel: still not moving; teleporting out");
                }
            }
        }
        else
        {
            this.progress = null;
        }
        if (this.unstickTeleport)
        {
            var state = this.Teleport(territory, d, now);
            if (this.teleportedAt is not null)
            {
                this.unstickTeleport = this.groundOnly = false;
                this.stuck = 0;
                this.unsticks++;
                this.progress = null;
            }
            return state;
        }

        // On foot only: a mounted character stays mounted and rides on.
        if (!game.Mounted && distance > TeleportWithinBeyond && this.Aetheryte(territory, d) is { } near && near.Distance + TeleportSaves < distance)
            return this.Teleport(territory, d, now);

        if (nav.IsBusy || now < this.nextTry)
            return TravelState.Moving;
        // A flight that ended here, short of the destination: the rest of this trip goes on the ground.
        if (this.flying && !this.groundOnly)
        {
            this.groundOnly = true;
            Plugin.Trace($"Travel: the flight stopped {distance:0} y short; going on the ground");
        }
        this.flying = false;
        this.nextTry = now + RetryEvery;
        if (!nav.IsReady)
            return TravelState.Moving;

        bool fly = !this.groundOnly && this.CanFlyHere();
        if (this.groundOnly && game.InFlight)
        {
            // Land first: a walking path cannot start in the air.
            game.UseGeneral(GameActions.Dismount);
            return TravelState.Moving;
        }
        if (mount && !game.Mounted && !game.InCombat && distance > MountBeyond && this.CanMountHere())
        {
            game.UseGeneral(fly ? GameActions.FlyingMountRoulette : GameActions.MountRoulette);
            return TravelState.Moving;
        }
        if (!mount && game.Mounted)
        {
            game.UseGeneral(GameActions.Dismount);
            return TravelState.Moving;
        }
        this.flying = (fly && game.Mounted) || game.InFlight;
        if (nav.MoveTo(d, fly: this.flying))
        {
            this.refusals = 0;
        }
        else if (++this.refusals == 1)
        {
            Plugin.Trace($"Travel: vnavmesh refused a path to {d}{(this.flying ? " (flying)" : string.Empty)}");
        }
        else if (this.refusals >= RefusalsAllowed)
        {
            return this.Fail($"vnavmesh refused {this.refusals} paths in a row to ({d.X:0}, {d.Z:0}): it may be off the navmesh.");
        }
        return TravelState.Moving;
    }

    private TravelState Teleport(uint territory, Vector3 point, DateTime now)
    {
        if (this.teleportedAt is { } at)
        {
            if (now - at < TeleportTimeout)
                return TravelState.Moving;
            this.teleportedAt = null;
        }
        if (game.InCombat || now < this.nextTry)
            return TravelState.Moving;
        this.nextTry = now + RetryEvery;

        if (this.Aetheryte(territory, point) is not { Id: var aetheryte })
        {
            string zone = data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territory)?.PlaceName.ValueNullable?.Name.ExtractText() ?? territory.ToString();
            return this.Fail($"No attuned aetheryte in {zone}. Attune one there, then start again.");
        }
        if (game.Mounted)
        {
            game.UseGeneral(GameActions.Dismount);
            return TravelState.Moving;
        }
        if (nav.IsBusy)
            nav.Stop();
        var telepo = Telepo.Instance();
        if (telepo != null && telepo->Teleport(aetheryte, 0))
            this.teleportedAt = now;
        return TravelState.Moving;
    }

    /// <summary>The attuned aetheryte in <paramref name="territory"/> nearest <paramref name="point"/>, and how far from it it is.</summary>
    private (uint Id, float Distance)? Aetheryte(uint territory, Vector3 point)
    {
        var telepo = Telepo.Instance();
        if (telepo == null)
            return null;
        telepo->UpdateAetheryteList();
        var attuned = telepo->TeleportList.AsSpan().ToArray().Where(i => i.TerritoryId == territory).Select(i => i.AetheryteId).ToHashSet();
        if (attuned.Count == 0)
            return null;

        var sheet = data.GetExcelSheet<Aetheryte>();
        var levels = data.GetExcelSheet<Level>();
        return attuned
            .Select(id => (Id: id, Distance: sheet.GetRowOrDefault(id)?.Level[0].RowId is { } l and > 0 && levels.GetRowOrDefault(l) is { } lv
                ? Flat(new Vector3(lv.X, lv.Y, lv.Z), point)
                : float.MaxValue))
            .OrderBy(a => a.Distance)
            .First();
    }

    private bool CanMountHere() => data.GetExcelSheet<TerritoryType>().GetRowOrDefault(client.TerritoryType)?.Mount == true;

    /// <summary>This character can fly in this zone: its aether currents (for A Realm Reborn's zones, the main scenario) are done.</summary>
    private bool CanFlyHere()
    {
        uint set = data.GetExcelSheet<TerritoryType>().GetRowOrDefault(client.TerritoryType)?.AetherCurrentCompFlgSet.RowId ?? 0;
        var ps = PlayerState.Instance();
        return set != 0 && ps != null && ps->IsAetherCurrentZoneComplete(set);
    }

    private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));

    private TravelState Fail(string why)
    {
        this.Problem = why;
        return TravelState.Failed;
    }

    /// <summary>A beast spawn as a destination (map coordinates, height unknown).</summary>
    public static Vector3 ToPoint(Spawn s) => new(s.WorldX, 0, s.WorldZ);
}
