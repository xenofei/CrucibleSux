namespace CrucibleSux.Core;

/// <summary>
/// The Bestiary shows a beast's attributes and EXP but no rank field the client exposes, so the rank is worked out from
/// the attributes: the rank whose computed attributes (<see cref="BeastInfo.StatsAt"/>) match what the page shows.
/// </summary>
public static class RankEstimator
{
    /// <summary>
    /// The rank whose attributes equal <paramref name="shown"/>, or null when no rank matches or two ranks do.
    /// One attribute may differ by one point (rounding), never more.
    /// </summary>
    public static int? FromStats(BeastInfo beast, BeastStats shown)
    {
        int? found = null;
        for (int rank = 1; rank <= beast.MaxRank; rank++)
        {
            if (!Matches(beast.StatsAt(rank), shown))
                continue;
            if (found is not null && beast.StatsAt(found.Value) != beast.StatsAt(rank))
                return null;
            found ??= rank;
        }
        return found;
    }

    private static bool Matches(BeastStats a, BeastStats b)
    {
        int[] d =
        [
            Math.Abs(a.Strength - b.Strength), Math.Abs(a.Intelligence - b.Intelligence),
            Math.Abs(a.PhysicalResistance - b.PhysicalResistance), Math.Abs(a.MagicalResistance - b.MagicalResistance),
            Math.Abs(a.Constitution - b.Constitution),
        ];
        return d.All(x => x <= 1) && d.Count(x => x == 1) <= 1;
    }
}
