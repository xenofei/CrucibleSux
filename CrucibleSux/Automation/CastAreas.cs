using System.Collections.Generic;
using System.Numerics;
using CrucibleSux.Core;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using ClientBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

namespace CrucibleSux.Automation;

/// <summary>
/// The area an enemy is casting, read the same way for every enemy and for both the dodger and the combat log: the shape
/// from the cast's Action row, its place from the cast (<see cref="AoeThreat.Of"/>).
/// </summary>
public sealed unsafe class CastAreas(IDataManager data, IObjectTable objects)
{
    private readonly ExcelSheet<Lumina.Excel.Sheets.Action> actions = data.GetExcelSheet<Lumina.Excel.Sheets.Action>();
    private readonly Dictionary<uint, (string Name, AoeShape Shape, byte CastType)> cache = [];

    /// <summary>The action's name and its area as the Action row describes it, before it is placed.</summary>
    public (string Name, AoeShape Shape) Info(uint id)
    {
        var (name, shape, _) = this.Row(id);
        return (name, shape);
    }

    /// <summary>The area <paramref name="npc"/> is casting, placed; null for a single-target cast or none.</summary>
    public AoeThreat? Read(IBattleNpc npc)
    {
        var (_, shape, castType) = this.Row(npc.CastActionId);
        if (shape.Kind == AoeKind.None || !npc.IsCasting)
            return null;
        var ci = ((ClientBattleChara*)npc.Address)->GetCastInfo();
        if (ci == null)
            return null;
        // The cast's own rotation reads 0 in this patch; the caster faces its frontal attacks, so its facing is used.
        Vector3 at = ci->TargetLocation;
        Vector2? aim = at != Vector3.Zero ? Flat(at)
            : objects.SearchById(npc.CastTargetObjectId) is { } target && target.GameObjectId != npc.GameObjectId ? Flat(target.Position)
            : null;
        return AoeThreat.Of(shape, castType, Flat(npc.Position), npc.Rotation, npc.HitboxRadius, aim);
    }

    private (string Name, AoeShape Shape, byte CastType) Row(uint id)
    {
        if (this.cache.TryGetValue(id, out var info))
            return info;
        info = this.actions.GetRowOrDefault(id) is { } a
            ? (a.Name.ExtractText(), AoeShape.FromAction(a.CastType, a.EffectRange, a.XAxisModifier, a.Omen.ValueNullable?.Path.ExtractText()), a.CastType)
            : ($"Action {id}", default, (byte)0);
        this.cache[id] = info;
        return info;
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
