namespace CrucibleSux.Core;

/// <summary>
/// One board of the Crucible. The sheet values (XBMContent, ContentFinderCondition) are from game 2026.09.15; the
/// gear target is the community's synced item level for the board (docs/research/03-optimal-builds.md), advisory only.
/// </summary>
/// <param name="Number">1-5, the XBMContent row.</param>
/// <param name="UnlockQuest">The job quest whose completion opens the board.</param>
/// <param name="RequiredBeasts">Beasts that must be befriended before the board opens, 0 when none.</param>
/// <param name="RequiredBoard">A board that must be cleared first, 0 when none.</param>
public sealed record BoardInfo(
    int Number,
    string Name,
    uint ContentFinderCondition,
    uint Territory,
    uint InstanceContent,
    int JobLevel,
    int MinItemLevel,
    int TargetItemLevel,
    int TeamSize,
    int RecommendedRank,
    int SyncRank,
    uint UnlockQuest,
    int RequiredBeasts,
    int RequiredBoard,
    uint ClearAchievement,
    uint ThirdDegreeAchievement,
    uint ApexAchievement,
    uint LegendaryAchievement)
{
    public bool IsMastersBoard => this.Number >= 4;
}

public static class Boards
{
    public static readonly IReadOnlyList<BoardInfo> All =
    [
        new(1, "First Board of the Unbroken", 1088, 1339, 56001, 30, 25, 25, 10, 3, 5,
            CrucibleIds.Quests.IntoTheCrucible, 0, 0, 4033, 4038, 4043, 4044),
        new(2, "Second Board of the Unbroken", 1089, 1340, 56002, 40, 35, 35, 12, 8, 10,
            CrucibleIds.Quests.Gobsmacked, 0, 0, 4034, 4039, 4045, 4046),
        new(3, "Third Board of the Unbroken", 1090, 1341, 56003, 50, 82, 100, 14, 13, 15,
            CrucibleIds.Quests.ABeastmastersPath, 0, 0, 4035, 4040, 4047, 4048),
        new(4, "First Master's Board", 1091, 1342, 56004, 50, 82, 120, 12, 18, 20,
            CrucibleIds.Quests.FreeForAll, 25, 0, 4036, 4041, 4049, 4050),
        new(5, "Second Master's Board", 1092, 1343, 56005, 50, 82, 135, 15, 23, 25,
            CrucibleIds.Quests.MasteryRematch, 30, 4, 4037, 4042, 4051, 4052),
    ];

    public static BoardInfo ByNumber(int number) =>
        All.FirstOrDefault(b => b.Number == number) ?? throw new ArgumentOutOfRangeException(nameof(number), number, "boards are numbered 1-5");

    public static BoardInfo? ByTerritory(uint territory) => All.FirstOrDefault(b => b.Territory == territory);
}
