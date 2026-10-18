using System.Numerics;

namespace CrucibleSux.Core;

/// <summary>An enemy area attack being cast: its shape, where it sits and which way it faces.</summary>
public readonly record struct AoeThreat(AoeShape Shape, Vector2 Origin, float Rotation);

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
    /// attack). The caller keeps the first one a path can reach.
    /// </summary>
    public static IEnumerable<Vector2> SafeSpots(IReadOnlyList<AoeThreat> threats, Vector2 me)
    {
        if (!InDanger(threats, me))
            yield break;
        for (float r = Step; r <= Reach; r += Step)
        {
            for (int i = 0; i < Directions; i++)
            {
                float a = i * MathF.Tau / Directions;
                var p = me + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                if (!InDanger(threats, p))
                    yield return p;
            }
        }
    }
}
