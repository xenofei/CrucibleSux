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
    [Fact]
    public void Every_cast_type_is_placed_where_it_lands()
    {
        var caster = new Vector2(0, 0);
        var aim = new Vector2(10, 0);

        // A circle on a target or the ground sits where it aims; one around the caster reaches past its hitbox.
        Assert.Equal(aim, AoeThreat.Of(new AoeShape(AoeKind.Circle, 5), 2, caster, 0, 3, aim).Origin);
        var self = AoeThreat.Of(new AoeShape(AoeKind.Circle, 5), 5, caster, 0, 3, aim);
        Assert.Equal((caster, 8f), (self.Origin, self.Shape.Range));

        // A charge runs from the caster to its aim, whichever way the caster faces.
        var charge = AoeThreat.Of(new AoeShape(AoeKind.Rect, 40, Width: 4), 8, caster, 0, 3, aim);
        Assert.True(AoeDodge.InDanger([charge], new Vector2(9, 0)));
        Assert.False(AoeDodge.InDanger([charge], new Vector2(0, 9)));
        Assert.False(AoeDodge.InDanger([charge], new Vector2(14, 0)));

        // A cone faces where the caster faces: rotation pi/2 is +X.
        var cone = AoeThreat.Of(new AoeShape(AoeKind.Cone, 6, HalfAngleDegrees: 45), 3, caster, MathF.PI / 2, 1, null);
        Assert.True(AoeDodge.InDanger([cone], new Vector2(4, 0)));
        Assert.False(AoeDodge.InDanger([cone], new Vector2(-4, 0)));
    }

    [Fact]
    public void A_melee_character_steps_aside_and_stays_in_reach()
    {
        // A cone 10 y long facing +Z from a caster at the origin; the character 2 y in front of it, in melee range.
        AoeThreat[] cone = [new(new AoeShape(AoeKind.Cone, 10, HalfAngleDegrees: 45), O, 0)];
        var me = new Vector2(0, 2);

        var spot = AoeDodge.SafeSpots(cone, me, near: O, within: 3.5f).First();

        Assert.False(AoeDodge.InDanger(cone, spot));
        Assert.True(Vector2.Distance(spot, O) <= 3.5f, "the first spot keeps the caster in reach");
    }

    [Fact]
    public void The_approach_keeps_out_of_the_areas()
    {
        AoeThreat[] cone = [new(new AoeShape(AoeKind.Cone, 10, HalfAngleDegrees: 45), O, 0)];
        // Coming from in front of the caster, the way in is around the side of its cone.
        var spot = AoeDodge.Approach(cone, new Vector2(0, 15), O, 3.5f).First();
        Assert.False(AoeDodge.InDanger(cone, spot));

        AoeThreat[] around = [new(new AoeShape(AoeKind.Circle, 8), O, 0)];
        Assert.Empty(AoeDodge.Approach(around, new Vector2(0, 15), O, 3.5f));
    }
}
