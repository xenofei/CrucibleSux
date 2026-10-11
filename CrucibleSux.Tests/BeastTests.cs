using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class BeastTests
{
    [Fact]
    public void Stats_scale_the_grow_row_by_the_beast_percentages()
    {
        var beast = Make.Beast(1) with { StatPercent = [100, 50, 100, 100, 91] };

        var s = beast.StatsAt(1);

        Assert.Equal(new BeastStats(14, 4, 12, 12, 12), s);
    }

    [Fact]
    public void Stats_clamp_the_rank_to_the_table()
    {
        var beast = Make.Beast(1, ranks: 25);

        Assert.Equal(beast.StatsAt(25), beast.StatsAt(99));
        Assert.Equal(beast.StatsAt(1), beast.StatsAt(0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(25)]
    public void Rank_is_recovered_from_the_shown_attributes(int rank)
    {
        var beast = Make.Beast(1);

        Assert.Equal(rank, RankEstimator.FromStats(beast, beast.StatsAt(rank)));
    }

    [Fact]
    public void One_attribute_off_by_one_still_matches_but_more_does_not()
    {
        var beast = Make.Beast(1);
        var s = beast.StatsAt(7);

        Assert.Equal(7, RankEstimator.FromStats(beast, s with { Strength = s.Strength + 1 }));
        Assert.Null(RankEstimator.FromStats(beast, s with { Strength = s.Strength + 1, Constitution = s.Constitution + 1 }));
        Assert.Null(RankEstimator.FromStats(beast, s with { Strength = s.Strength + 2 }));
    }

    [Fact]
    public void Ranks_with_equal_attributes_give_the_lowest_one()
    {
        var beast = Make.Beast(1, step: 0);

        Assert.Equal(1, RankEstimator.FromStats(beast, beast.StatsAt(1)));
    }
}
