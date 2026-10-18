namespace CrucibleSux.Core;

/// <summary>EXP per level (ParamGrow, game 2026.09.15; docs/data/bst-progression.md).</summary>
public static class ExpTable
{
    private static readonly int[] ToNextLevel =
    [
        300, 450, 630, 970, 1440, 1940, 3000, 3920, 4970, 5900,
        7430, 8620, 10200, 11300, 13100, 15200, 17400, 19600, 21900, 24300,
        27400, 30600, 33900, 37300, 40800, 49200, 54600, 61900, 65600, 68400,
        74000, 82700, 88700, 95000, 102000, 113000, 121000, 133000, 142000, 155000,
        163000, 171000, 179000, 187000, 195000, 214000, 229000, 244000, 259000,
    ];

    /// <summary>EXP from <paramref name="level"/> to the next; 0 at the cap.</summary>
    public static int ToNext(int level) => level >= 1 && level <= ToNextLevel.Length ? ToNextLevel[level - 1] : 0;

    /// <summary>EXP gained between two readings of (level, EXP into that level).</summary>
    public static long Gained(int fromLevel, int fromExp, int toLevel, int toExp)
    {
        if (toLevel < fromLevel || (toLevel == fromLevel && toExp < fromExp))
            return 0;
        if (toLevel == fromLevel)
            return toExp - fromExp;
        long sum = ToNext(fromLevel) - fromExp;
        for (int l = fromLevel + 1; l < toLevel; l++)
            sum += ToNext(l);
        return sum + toExp;
    }
}

/// <summary>A FATE as the picker sees it.</summary>
/// <param name="Progress">Percent complete, 0-100.</param>
/// <param name="Distance">Yalms from the player.</param>
/// <param name="NeedsHandIn">A collection FATE (it hands in items); the plugin does not run those.</param>
public sealed record FateCandidate(ushort Id, int Level, int MaxLevel, int Progress, long SecondsLeft, float Distance, bool Bonus, bool NeedsHandIn);

/// <summary>
/// The levelling route (docs/research/09-leveling.md): FATEs near the player's level, ordered by expected EXP per second.
/// FATE EXP follows the FATE's level, so the best FATE is the highest-level one the player can join without a penalty
/// (at or below their level), discounted by the time it takes to get there and finish it.
/// </summary>
public static class FatePicker
{
    /// <summary>Mounted travel speed assumed for ranking, in yalms per second.</summary>
    private const float TravelSpeed = 10f;

    /// <summary>Seconds a FATE is assumed to take from 0% when the player joins.</summary>
    private const float FullFateSeconds = 150f;

    /// <summary>Seconds of fighting a FATE needs after arrival before it ends, for the character to earn its credit.</summary>
    private const float MinShare = 15f;

    /// <summary>How far below the player's level a FATE may be before it is not worth the trip.</summary>
    public const int MaxLevelsBelow = 6;

    // Base FATE EXP by FATE level (wiki zone FATE tables, docs/research/09-leveling.md).
    private static readonly (int Level, int Exp)[] BaseExp =
        [(1, 100), (3, 240), (7, 560), (10, 855), (14, 1380), (21, 1800), (24, 1980), (30, 3120), (35, 3960), (40, 5390), (45, 5940), (49, 6960), (50, 7080)];

    /// <summary>Base EXP for a FATE of <paramref name="level"/>, interpolated between the known points.</summary>
    public static double ExpFor(int level)
    {
        if (level <= BaseExp[0].Level)
            return BaseExp[0].Exp;
        for (int i = 1; i < BaseExp.Length; i++)
        {
            var (l, e) = BaseExp[i];
            if (level <= l)
            {
                var (pl, pe) = BaseExp[i - 1];
                return pe + ((e - pe) * (double)(level - pl) / (l - pl));
            }
        }
        return BaseExp[^1].Exp;
    }

    /// <summary>Expected EXP per second for joining <paramref name="f"/> now, or 0 when it is not worth joining.</summary>
    public static double Score(FateCandidate f, int playerLevel)
    {
        if (f.NeedsHandIn || f.Level > playerLevel || f.Level < playerLevel - MaxLevelsBelow)
            return 0;
        float travel = f.Distance / TravelSpeed;
        float remaining = FullFateSeconds * (100 - f.Progress) / 100f;

        // Credit needs a share of the work: a FATE predicted to end before the player has fought in it gives nothing.
        if (f.SecondsLeft < travel + 30 || remaining < travel + MinShare)
            return 0;
        double exp = ExpFor(f.Level) * (f.Bonus ? 1.5 : 1);
        return exp / (travel + Math.Max(remaining, 30) + 10);
    }

    public static FateCandidate? Pick(IEnumerable<FateCandidate> fates, int playerLevel) =>
        fates.Select(f => (f, s: Score(f, playerLevel))).Where(x => x.s > 0).OrderByDescending(x => x.s).Select(x => x.f).FirstOrDefault();
}

/// <summary>
/// ARR zones whose FATEs suit a level band, from the wiki's per-zone FATE tables (Lumina cannot link a FATE to its zone),
/// rechecked 2026-10-18 (docs/research/09-leveling.md): Western La Noscea has nothing from 19 to 39, Coerthas nothing from
/// 41 to 44, and Southern Thanalan has the most FATEs at 45-49.
/// </summary>
public static class LevelingZones
{
    public const uint EastShroud = 152;
    public const uint EasternThanalan = 145;
    public const uint NorthernThanalan = 147;
    public const uint CoerthasCentralHighlands = 155;
    public const uint OuterLaNoscea = 180;

    /// <summary>Zones to FATE in at <paramref name="level"/>, best first; the runner rotates when one has nothing.</summary>
    public static IReadOnlyList<uint> For(int level) => level switch
    {
        < 10 => [BeastSources.CentralShroud, BeastSources.MiddleLaNoscea, BeastSources.WesternThanalan, BeastSources.LowerLaNoscea, BeastSources.CentralThanalan],
        < 16 => [BeastSources.CentralShroud, BeastSources.MiddleLaNoscea, EastShroud, BeastSources.WesternLaNoscea, EasternThanalan],
        < 22 => [EastShroud, EasternThanalan, BeastSources.WesternLaNoscea, BeastSources.UpperLaNoscea],
        < 30 => [BeastSources.SouthShroud, BeastSources.SouthernThanalan, EasternThanalan, BeastSources.UpperLaNoscea],
        < 36 => [BeastSources.EasternLaNoscea, OuterLaNoscea, BeastSources.SouthernThanalan, BeastSources.SouthShroud],
        < 41 => [CoerthasCentralHighlands, OuterLaNoscea, BeastSources.EasternLaNoscea],
        < 45 => [EastShroud, BeastSources.WesternLaNoscea, EasternThanalan, BeastSources.MorDhona, OuterLaNoscea],
        // Outer La Noscea's 45+ FATEs are mostly bosses, which take long alone.
        _ => [BeastSources.SouthernThanalan, NorthernThanalan, BeastSources.MorDhona, EastShroud, OuterLaNoscea],
    };
}

/// <summary>EXP per hour, measured per zone, so the runner can stay where levelling has actually been fastest.</summary>
public sealed class ExpMeter
{
    private readonly Dictionary<uint, (long Exp, double Seconds)> zones = [];
    private (DateTime At, uint Zone, int Level, int Exp)? last;

    /// <summary>Adds a reading. Time spent in a zone counts toward it whether or not EXP came in.</summary>
    public void Record(DateTime at, uint zone, int level, int exp)
    {
        if (this.last is { } l && l.Zone == zone && at > l.At && (at - l.At).TotalMinutes < 5)
        {
            var z = this.zones.GetValueOrDefault(zone);
            this.zones[zone] = (z.Exp + ExpTable.Gained(l.Level, l.Exp, level, exp), z.Seconds + (at - l.At).TotalSeconds);
        }
        this.last = (at, zone, level, exp);
    }

    /// <summary>EXP per hour in <paramref name="zone"/>; null until 10 minutes have been measured there.</summary>
    public double? Rate(uint zone) =>
        this.zones.TryGetValue(zone, out var z) && z.Seconds >= 600 ? z.Exp * 3600 / z.Seconds : null;

    /// <summary>Forgets the measurements (a new level band makes them stale).</summary>
    public void Reset()
    {
        this.zones.Clear();
        this.last = null;
    }
}

public enum CaptureMove
{
    /// <summary>Hit the target with auto-attack only, to bring its HP down without killing it.</summary>
    Weaken,

    /// <summary>Use Capture on the target.</summary>
    Capture,

    /// <summary>The target is marked (Interest Captured): kill it so the game rolls.</summary>
    Kill,
}

/// <summary>
/// When to use Capture (docs/research/08-beast-collection.md): the roll happens when the marked target dies, and a lower
/// HP when it is marked is said to help. Against a much lower-level target, auto-attacks alone can take most of its HP in
/// one hit, so Capture goes out earlier.
/// </summary>
public static class CaptureTactics
{
    public const int CaptureAtHpPercent = 40;

    public static CaptureMove Decide(int hpPercent, int levelGap, bool marked)
    {
        if (marked)
            return CaptureMove.Kill;
        int threshold = levelGap >= 10 ? 90 : levelGap >= 5 ? 60 : CaptureAtHpPercent;
        return hpPercent <= threshold ? CaptureMove.Capture : CaptureMove.Weaken;
    }
}
