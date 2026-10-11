using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class LevelingTests
{
    private static FateCandidate F(int level, float distance = 100, int progress = 0, long left = 900, bool bonus = false, bool handIn = false, ushort id = 1) =>
        new(id, level, level + 5, progress, left, distance, bonus, handIn);

    [Fact]
    public void The_exp_table_sums_to_the_known_total()
    {
        Assert.Equal(3_589_670L, ExpTable.Gained(1, 0, 50, 0));
        Assert.Equal(573_870L, ExpTable.Gained(1, 0, 30, 0));
    }

    [Fact]
    public void Gained_counts_across_level_ups() =>
        Assert.Equal(200L + 450 + 50, ExpTable.Gained(1, 100, 3, 50));

    [Fact]
    public void Base_exp_interpolates_between_known_levels()
    {
        Assert.Equal(855, FatePicker.ExpFor(10));
        Assert.Equal(7080, FatePicker.ExpFor(50));
        Assert.InRange(FatePicker.ExpFor(12), 855, 1380);
    }

    [Fact]
    public void Fates_above_the_player_or_far_below_are_skipped()
    {
        Assert.Equal(0, FatePicker.Score(F(21), 20));
        Assert.Equal(0, FatePicker.Score(F(20 - FatePicker.MaxLevelsBelow - 1), 20));
        Assert.True(FatePicker.Score(F(20), 20) > 0);
    }

    [Fact]
    public void Hand_in_fates_and_nearly_over_fates_are_skipped()
    {
        Assert.Equal(0, FatePicker.Score(F(20, handIn: true), 20));
        Assert.Equal(0, FatePicker.Score(F(20, distance: 600, left: 40), 20));
    }

    [Fact]
    public void The_higher_level_fate_wins_at_equal_distance() =>
        Assert.Equal((ushort?)2, FatePicker.Pick([F(16, id: 1), F(20, id: 2)], 20)?.Id);

    [Fact]
    public void A_close_fate_beats_a_far_one_of_the_same_level() =>
        Assert.Equal((ushort?)1, FatePicker.Pick([F(20, distance: 50, id: 1), F(20, distance: 900, id: 2)], 20)?.Id);

    [Fact]
    public void Bonus_fates_are_preferred() =>
        Assert.Equal((ushort?)2, FatePicker.Pick([F(20, id: 1), F(20, bonus: true, id: 2)], 20)?.Id);

    [Fact]
    public void Every_level_has_zones() =>
        Assert.All(Enumerable.Range(1, 50), l => Assert.NotEmpty(LevelingZones.For(l)));

    [Fact]
    public void The_meter_reports_after_ten_minutes()
    {
        var m = new ExpMeter();
        var t = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i <= 10; i++)
            m.Record(t.AddMinutes(i), 134, 10, i * 100);

        Assert.Equal(6000, m.Rate(134));
        Assert.Null(m.Rate(135));
    }

    [Fact]
    public void The_meter_ignores_gaps_and_zone_changes()
    {
        var m = new ExpMeter();
        var t = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        m.Record(t, 134, 10, 0);
        m.Record(t.AddMinutes(30), 134, 10, 5000);

        Assert.Null(m.Rate(134));
    }

    [Theory]
    [InlineData(100, 0, false, CaptureMove.Weaken)]
    [InlineData(40, 0, false, CaptureMove.Capture)]
    [InlineData(85, 12, false, CaptureMove.Capture)]
    [InlineData(70, 6, false, CaptureMove.Weaken)]
    [InlineData(100, 0, true, CaptureMove.Kill)]
    public void Capture_goes_out_low_and_earlier_against_weak_targets(int hp, int gap, bool marked, CaptureMove expected) =>
        Assert.Equal(expected, CaptureTactics.Decide(hp, gap, marked));
}
