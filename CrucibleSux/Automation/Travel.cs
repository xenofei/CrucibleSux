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
            return TravelState.Arrived;
        }
        // On foot only: a mounted character stays mounted and rides on.
        if (!game.Mounted && distance > TeleportWithinBeyond && this.Aetheryte(territory, d) is { } near && near.Distance + TeleportSaves < distance)
            return this.Teleport(territory, d, now);

        if (nav.IsBusy || now < this.nextTry)
            return TravelState.Moving;
        this.nextTry = now + RetryEvery;
        if (!nav.IsReady)
            return TravelState.Moving;

        bool fly = this.CanFlyHere();
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
        if (!nav.MoveTo(d, fly: (fly && game.Mounted) || game.InFlight))
            Plugin.Log.Debug($"vnavmesh refused a path to {d}");
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
