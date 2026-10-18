using System.Numerics;
using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class DodgeTests
{
    private static readonly Vector2 O = Vector2.Zero;

    [Fact]
    public void Out_of_a_circle_by_the_shortest_way()
    {
        AoeThreat[] circle = [new(new AoeShape(AoeKind.Circle, 8), O, 0)];
        var me = new Vector2(3, 0);

        var spot = AoeDodge.SafeSpots(circle, me).First();

        Assert.False(AoeDodge.InDanger(circle, spot));
        // The nearest way out is straight away from the centre, about 8 + 1.5 - 3 yalms.
        Assert.InRange(Vector2.Distance(me, spot), 6f, 8.5f);
    }

    [Fact]
    public void Out_of_a_cone_sideways_rather_than_back()
    {
        // A 90-degree cone 20 y long, facing +Z (rotation 0); the character 6 y in front of the caster.
        AoeThreat[] cone = [new(new AoeShape(AoeKind.Cone, 20, HalfAngleDegrees: 45), O, 0)];
        var me = new Vector2(0, 6);

        var spot = AoeDodge.SafeSpots(cone, me).First();

        Assert.False(AoeDodge.InDanger(cone, spot));
        Assert.True(Vector2.Distance(me, spot) < 14f, "stepping sideways beats walking the 14 y out of its range");
    }

    [Fact]
    public void Nothing_to_do_when_safe_or_when_nowhere_in_reach_is()
    {
        AoeThreat[] circle = [new(new AoeShape(AoeKind.Circle, 5), O, 0)];
        Assert.Empty(AoeDodge.SafeSpots(circle, new Vector2(10, 0)));

        // An arena-wide attack: no spot within reach is out of it.
        AoeThreat[] arena = [new(new AoeShape(AoeKind.Circle, 60), O, 0)];
        Assert.Empty(AoeDodge.SafeSpots(arena, new Vector2(1, 0)));
    }
}
