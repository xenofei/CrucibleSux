using System.Numerics;
using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class AoeShapeTests
{
    private static readonly Vector2 O = Vector2.Zero;

    // Rotation 0 faces +Z, which is Vector2.Y here (X, Z).
    private const float FacingZ = 0f;

    [Fact]
    public void Shapes_come_from_the_action_row_and_its_omen()
    {
        // Pas de Seul's Blood Rain (46924): CastType 10, range 40, omen gl_sircle_4008ah1.
        Assert.Equal(new AoeShape(AoeKind.Donut, 40, Inner: 8), AoeShape.FromAction(10, 40, 0, "gl_sircle_4008ah1"));
        // Ogre piece's Scorching Smite (46912): CastType 13, range 40, omen gl_fan120_1bf.
        Assert.Equal(new AoeShape(AoeKind.Cone, 40, HalfAngleDegrees: 60), AoeShape.FromAction(13, 40, 0, "gl_fan120_1bf"));
        // Miteling's Silkscreen (46909): CastType 12, range 40, width 4.
        Assert.Equal(new AoeShape(AoeKind.Rect, 40, Width: 4), AoeShape.FromAction(12, 40, 4, "m0830_laser_dark_01k1"));
        Assert.Equal(new AoeShape(AoeKind.Circle, 6), AoeShape.FromAction(2, 6, 0, "general_1bf"));
        Assert.Equal(AoeKind.Cross, AoeShape.FromAction(11, 50, 10, "general_x02f").Kind);
        // Single-target casts and zero-range rows have no area.
        Assert.Equal(AoeKind.None, AoeShape.FromAction(1, 0, 0, null).Kind);
        Assert.Equal(AoeKind.None, AoeShape.FromAction(2, 0, 0, null).Kind);
    }

    [Fact]
    public void A_cone_without_an_angle_in_its_omen_counts_as_90_degrees()
    {
        Assert.Equal(45, AoeShape.FromAction(3, 10, 0, "general02f").HalfAngleDegrees);
    }

    [Fact]
    public void Circle_and_donut_margins_are_yalms_inside()
    {
        var circle = new AoeShape(AoeKind.Circle, 8);
        Assert.Equal(3, circle.Margin(O, FacingZ, new Vector2(5, 0)), 3);
        Assert.Equal(-2, circle.Margin(O, FacingZ, new Vector2(0, 10)), 3);

        var donut = new AoeShape(AoeKind.Donut, 40, Inner: 8);
        Assert.True(donut.Margin(O, FacingZ, new Vector2(0, 4)) < 0, "the donut's middle is safe");
        Assert.Equal(2, donut.Margin(O, FacingZ, new Vector2(0, 10)), 3);
    }

    [Fact]
    public void A_cone_hits_in_front_within_its_angle_only()
    {
        var cone = new AoeShape(AoeKind.Cone, 40, HalfAngleDegrees: 60);
        Assert.True(cone.Margin(O, FacingZ, new Vector2(0, 10)) > 0, "straight ahead");
        Assert.True(cone.Margin(O, FacingZ, new Vector2(8, 5)) > 0, "58 degrees off, inside 60");
        Assert.True(cone.Margin(O, FacingZ, new Vector2(10, 0)) < 0, "90 degrees off");
        Assert.True(cone.Margin(O, FacingZ, new Vector2(0, -10)) < 0, "behind");
        Assert.True(cone.Margin(O, FacingZ, new Vector2(0, 45)) < 0, "beyond its range");
    }

    [Fact]
    public void The_cone_follows_the_casters_rotation()
    {
        var cone = new AoeShape(AoeKind.Cone, 40, HalfAngleDegrees: 30);
        float facingX = MathF.PI / 2;
        Assert.True(cone.Margin(O, facingX, new Vector2(10, 0)) > 0);
        Assert.True(cone.Margin(O, facingX, new Vector2(0, 10)) < 0);
    }

    [Fact]
    public void A_rect_starts_at_the_caster_and_runs_forward()
    {
        var rect = new AoeShape(AoeKind.Rect, 40, Width: 4);
        Assert.Equal(2, rect.Margin(O, FacingZ, new Vector2(0, 20)), 3);
        Assert.Equal(-1, rect.Margin(O, FacingZ, new Vector2(3, 20)), 3);
        Assert.True(rect.Margin(O, FacingZ, new Vector2(0, -5)) < 0, "behind the caster");
    }

    [Fact]
    public void A_cross_covers_both_arms()
    {
        var cross = new AoeShape(AoeKind.Cross, 50, Width: 10);
        Assert.True(cross.Margin(O, FacingZ, new Vector2(0, 30)) > 0);
        Assert.True(cross.Margin(O, FacingZ, new Vector2(-30, 0)) > 0);
        Assert.True(cross.Margin(O, FacingZ, new Vector2(20, 20)) < 0, "between the arms");
    }

    [Fact]
    public void No_area_has_no_margin()
    {
        Assert.True(float.IsNaN(default(AoeShape).Margin(O, 0, Vector2.One)));
    }
}

public class TargetPriorityTests
{
    private static EnemyInfo Enemy(uint id, params EnemyAbility[] abilities) =>
        new(id, $"enemy{id}", Element.Blunt, new EnemyRatings(1, 1, 1, 1, 1), 0, abilities);

    private static EnemyAbility A(IntelTarget target, IntelShape shape) => new(1, "ability", target, shape, 0);

    [Fact]
    public void Pas_de_Seul_mage_first_then_knight_with_the_boss()
    {
        // Board 1's boss battle as XBMBattleDetail row 6 lists it.
        var battle = new BattleInfo(1, 0, true,
        [
            Enemy(14541, A(IntelTarget.Self, IntelShape.CircleRing), A(IntelTarget.HighestEnmity, IntelShape.SingleTarget)),
            Enemy(14542, A(IntelTarget.Allies, IntelShape.SingleTarget), A(IntelTarget.Ground, IntelShape.Circle)),
            Enemy(14543, A(IntelTarget.None, IntelShape.Front)),
        ]);

        var p = TargetPriority.ByName([battle]);

        Assert.Equal(2, p[14542]);
        Assert.Equal(0, p[14541]);
        Assert.Equal(0, p[14543]);
    }

    [Fact]
    public void Adds_with_an_arena_wide_attack_come_before_their_main_enemy()
    {
        // The Ogre piece battle: the wisps' Scorched Earth is Universal.
        var battle = new BattleInfo(1, 5, false,
        [
            Enemy(14538, A(IntelTarget.None, IntelShape.Front), A(IntelTarget.Player, IntelShape.SingleTarget)),
            Enemy(14539, A(IntelTarget.None, IntelShape.Universal)),
        ]);

        var p = TargetPriority.ByName([battle]);

        Assert.Equal(1, p[14539]);
        Assert.Equal(0, p[14538]);
    }

    [Fact]
    public void A_main_enemys_own_arena_wide_attack_does_not_raise_it()
    {
        var battle = new BattleInfo(2, 2, false, [Enemy(14546, A(IntelTarget.None, IntelShape.Universal))]);

        Assert.Equal(0, TargetPriority.ByName([battle])[14546]);
    }
}

public class RunOptionsTests
{
    [Fact]
    public void The_default_takes_three_beasts_and_skips_the_shop()
    {
        var o = AutoDutyOverrides.For("RotationSolverReborn", RunOptions.Default);

        Assert.Equal("Recommended", o["Meta.Crucible.TeamMode"]);
        Assert.Equal("3", o["Meta.Crucible.RecommendedTeamCap"]);
        Assert.Equal("false", o["Meta.Crucible.Shop"]);
        Assert.Equal("RotationSolverReborn", o["DutyConfig.RotationPlugin"]);
        Assert.False(o.ContainsKey("Meta.Crucible.Items"), "a plain clear leaves items to AutoDuty");
    }

    [Fact]
    public void A_full_roster_leaves_the_team_settings_alone()
    {
        var o = AutoDutyOverrides.For("BossMod", RunOptions.Default with { TeamOfThree = false, Shop = true });

        Assert.False(o.ContainsKey("Meta.Crucible.TeamMode"));
        Assert.False(o.ContainsKey("Meta.Crucible.RecommendedTeamCap"));
        Assert.Equal("true", o["Meta.Crucible.Shop"]);
    }

    [Fact]
    public void Score_mode_turns_off_everything_that_costs_a_bonus()
    {
        var o = AutoDutyOverrides.For("BossMod", RunOptions.Default with { Shop = true, Goal = RunGoal.Score });

        Assert.Equal("false", o["Meta.Crucible.Shop"]);
        Assert.Equal("false", o["Meta.Crucible.Items"]);
        Assert.Equal("false", o["Meta.Crucible.Treasure"]);
        Assert.Equal("false", o["Meta.Crucible.Rest"]);
    }
}

public class CombatLogTests
{
    private static readonly DateTime T0 = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_summary_keeps_counts_and_important_lines_within_a_discord_message()
    {
        var log = new CombatLog("First Board of the Unbroken", T0, ["Crucible SUX 0.2.1", "Settings: team of 3"]);
        for (int i = 0; i < 500; i++)
        {
            log.Add(T0.AddSeconds(i), LogKind.Cast, $"ogre piece casts Scorching Smite #{i}");
            log.Add(T0.AddSeconds(i + 0.5), LogKind.Hit, $"Scorching Smite #{i} ended with you 2.0 y inside");
        }
        log.End(T0.AddMinutes(10));

        string summary = log.Summary(T0.AddMinutes(20));

        Assert.True(summary.Length <= CombatLog.SummaryChars, $"{summary.Length} chars");
        Assert.Contains("First Board of the Unbroken", summary);
        Assert.Contains("Settings: team of 3", summary);
        Assert.Contains("500 enemy casts, 500 ended with you inside", summary);
        Assert.Contains("#0 ended with you", summary);
        Assert.Contains("#499 ended with you", summary);
        Assert.True(summary.IndexOf("more; copy the full log", StringComparison.Ordinal) < summary.IndexOf("#499", StringComparison.Ordinal),
            "the marker sits between the first lines and the newest");
        Assert.DoesNotContain("casts Scorching Smite", summary);
        Assert.Contains("more; copy the full log", summary);
        Assert.Contains("10m 00s", summary);
    }

    [Fact]
    public void The_full_log_has_every_line_with_its_time()
    {
        var log = new CombatLog("Journey", T0, []);
        log.Add(T0.AddMilliseconds(75_300), LogKind.Move, "moved 3.0 y");

        string full = log.Full(T0.AddSeconds(80));

        Assert.Contains("01:15.3 Move    moved 3.0 y", full);
        Assert.Contains("(still recording)", full);
    }

    [Fact]
    public void A_full_log_stops_storing_lines_but_keeps_counting()
    {
        var log = new CombatLog("Board", T0, []);
        for (int i = 0; i < CombatLog.LineCap + 10; i++)
            log.Add(T0, LogKind.Move, "step");

        Assert.Equal(CombatLog.LineCap + 1, log.Lines.Count);
        Assert.Equal(LogKind.Warn, log.Lines[^1].Kind);
        Assert.Equal(CombatLog.LineCap + 10, log.Count(LogKind.Move));
    }

    [Fact]
    public void An_ended_log_takes_no_more_lines()
    {
        var log = new CombatLog("Board", T0, []);
        log.End(T0);
        log.Add(T0, LogKind.Info, "late");

        Assert.Empty(log.Lines);
    }
}
