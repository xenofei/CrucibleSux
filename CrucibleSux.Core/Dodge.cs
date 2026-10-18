using System.Numerics;

namespace CrucibleSux.Core;

/// <summary>An enemy area attack being cast: its shape, where it sits and which way it faces.</summary>
public readonly record struct AoeThreat(AoeShape Shape, Vector2 Origin, float Rotation)
{
    /// <summary>
    /// Places a cast's area by its Action row's CastType, the same way for every enemy: circles on a target or the ground
    /// (2, 6, 7) sit where the cast aims; a circle around the caster (5), cones, rects and crosses start at the caster, face
    /// where it faces and reach past its hitbox; a charge (8) runs from the caster to where it aims. A donut sits where it
    /// aims, else on the caster. <paramref name="aim"/> is the cast's target location, or its target's position, or null.
    /// </summary>
    public static AoeThreat Of(AoeShape shape, byte castType, Vector2 caster, float facing, float hitbox, Vector2? aim)
    {
        switch (castType)
        {
            case 2 or 6 or 7 when aim is { } at:
                return new(shape, at, facing);
            case 8 when aim is { } to && Vector2.Distance(caster, to) > 0.5f:
                var d = to - caster;
                return new(shape with { Range = d.Length() }, caster, MathF.Atan2(d.X, d.Y));
            case 10 when aim is { } at:
                return new(shape, at, facing);
        }
        return new(shape with { Range = shape.Range + hitbox }, caster, facing);
    }
}

/// <summary>
/// Where to step so no area being cast covers the character: the nearest point clear of every one by
/// <see cref="Clearance"/>, from rings around the character out to <see cref="Reach"/>. It needs no timeline, only the casts
/// the game shows.
/// </summary>
public static class AoeDodge
{
    public const float Clearance = 1.5f;
    public const float Reach = 25f;
    private const float Step = 2f;
    private const int Directions = 24;

    /// <summary>True when any of <paramref name="threats"/> covers <paramref name="point"/> or comes within the clearance of it.</summary>
    public static bool InDanger(IEnumerable<AoeThreat> threats, Vector2 point) =>
        threats.Any(t => t.Shape.Kind != AoeKind.None && t.Shape.Margin(t.Origin, t.Rotation, point) > -Clearance);

    /// <summary>
    /// Safe points nearest first; empty when the character is already safe or nothing within reach is (an arena-wide
    /// attack). With <paramref name="near"/>, the points within <paramref name="within"/> of it come first, so a melee
    /// character steps aside and keeps hitting its target. The caller keeps the first one a path can reach.
    /// </summary>
    public static IEnumerable<Vector2> SafeSpots(IReadOnlyList<AoeThreat> threats, Vector2 me, Vector2? near = null, float within = 0)
    {
        if (!InDanger(threats, me))
            return [];
        var spots = Rings(me, Step, Reach).Where(p => !InDanger(threats, p));
        // OrderBy is stable: inside each group the rings keep their nearest-first order.
        return near is { } n ? spots.OrderBy(p => Vector2.Distance(p, n) <= within ? 0 : 1) : spots;
    }

    /// <summary>
    /// Points within <paramref name="within"/> of <paramref name="target"/> that no cast covers, nearest to the character
    /// first: where to walk to fight it while its areas are up. Empty when every such point is covered.
    /// </summary>
    public static IEnumerable<Vector2> Approach(IReadOnlyList<AoeThreat> threats, Vector2 me, Vector2 target, float within) =>
        Rings(target, 1f, within).Where(p => !InDanger(threats, p)).OrderBy(p => Vector2.Distance(p, me));

    private static IEnumerable<Vector2> Rings(Vector2 centre, float step, float reach)
    {
        for (float r = step; r <= reach; r += step)
        {
            for (int i = 0; i < Directions; i++)
            {
                float a = i * MathF.Tau / Directions;
                yield return centre + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * r);
            }
        }
    }
}
