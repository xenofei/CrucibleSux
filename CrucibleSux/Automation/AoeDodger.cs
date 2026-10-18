using System.Collections.Generic;
using System.Numerics;
using CrucibleSux.Core;
using CrucibleSux.Integrations;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using ClientBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

namespace CrucibleSux.Automation;

/// <summary>
/// Steps out of enemy area attacks during the journey (<see cref="AoeDodge"/>): reads each cast's area from its Action
/// row, as the combat recorder does, and walks to the nearest reachable spot clear of every one until they go off. Boards
/// are left to BossMod's AI.
/// </summary>
public sealed unsafe class AoeDodger(IObjectTable objects, IDataManager data, NavmeshIpc nav, GameActions game)
{
    private const float Range = 40f;

    /// <summary>A cast about to go off is no longer worth stepping for.</summary>
    private const float TooLate = 0.2f;

    private const int SpotsTried = 8;

    private readonly ExcelSheet<Lumina.Excel.Sheets.Action> actions = data.GetExcelSheet<Lumina.Excel.Sheets.Action>();
    private readonly Dictionary<uint, (string Name, AoeShape Shape)> cache = [];
    private Vector3? spot;

    /// <summary>The cast being stepped out of, or null.</summary>
    public string? Dodging { get; private set; }

    /// <summary>True while it moves the character out of an area; the caller does nothing else meanwhile.</summary>
    public bool Tick()
    {
        if (game.Me is not IBattleChara me || game.InFlight)
            return this.Clear();
        var threats = new List<AoeThreat>();
        string? name = null;
        foreach (var o in objects)
        {
            if (o is not IBattleNpc npc || !GameActions.IsHostile(npc) || !npc.IsCasting || npc.TotalCastTime - npc.CurrentCastTime < TooLate
                || Vector3.Distance(npc.Position, me.Position) > Range)
                continue;
            var ci = ((ClientBattleChara*)npc.Address)->GetCastInfo();
            if (ci == null)
                continue;
            var (action, shape) = this.Info(npc.CastActionId);
            if (shape.Kind == AoeKind.None)
                continue;
            // Ground and target circles sit where the cast aims; the rest start at the caster and reach past its hitbox.
            Vector3 aim = ci->TargetLocation;
            bool atAim = shape.Kind is AoeKind.Circle or AoeKind.Donut && aim != Vector3.Zero;
            if (!atAim)
                shape = shape with { Range = shape.Range + npc.HitboxRadius };
            threats.Add(new(shape, atAim ? Flat(aim) : Flat(npc.Position), shape.Kind is AoeKind.Circle or AoeKind.Donut ? npc.Rotation : ci->Rotation));
            name ??= action;
        }

        if (!AoeDodge.InDanger(threats, Flat(me.Position)))
            return this.Clear();
        this.Dodging = name;
        // Keep heading for the spot already chosen while it stays clear.
        if (this.spot is { } chosen && !AoeDodge.InDanger(threats, Flat(chosen)))
        {
            if (!nav.IsBusy)
                nav.MoveTo(chosen);
            return true;
        }
        this.spot = null;
        int tried = 0;
        foreach (var p in AoeDodge.SafeSpots(threats, Flat(me.Position)))
        {
            if (++tried > SpotsTried)
                break;
            if (nav.Reachable(new Vector3(p.X, me.Position.Y, p.Y)) is { } r && Vector2.Distance(Flat(r), p) < AoeDodge.Clearance && !AoeDodge.InDanger(threats, Flat(r)))
            {
                this.spot = r;
                break;
            }
        }
        if (this.spot is not { } go)
            return this.Clear();
        Plugin.Log.Information($"Journey: stepping out of {name}");
        if (nav.IsBusy)
            nav.Stop();
        nav.MoveTo(go);
        return true;
    }

    private bool Clear()
    {
        this.spot = null;
        this.Dodging = null;
        return false;
    }

    private (string Name, AoeShape Shape) Info(uint id)
    {
        if (this.cache.TryGetValue(id, out var info))
            return info;
        info = this.actions.GetRowOrDefault(id) is { } a
            ? (a.Name.ExtractText(), AoeShape.FromAction(a.CastType, a.EffectRange, a.XAxisModifier, a.Omen.ValueNullable?.Path.ExtractText()))
            : ($"action {id}", default);
        this.cache[id] = info;
        return info;
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
