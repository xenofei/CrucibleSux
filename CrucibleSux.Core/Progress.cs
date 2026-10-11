namespace CrucibleSux.Core;

/// <summary>A board's milestones: cleared, under the third degree, Apex and Legendary. Null means not known yet.</summary>
public sealed record BoardProgress(BoardInfo Board, bool Cleared, bool? ThirdDegree, bool? Apex, bool? Legendary);

/// <summary>A tiered achievement (I-V) and how far the character is along it.</summary>
/// <param name="Current">The measured value (level, beasts), or null when it cannot be read.</param>
/// <param name="TiersDone">Completed tiers, or null while the achievement list is not loaded.</param>
public sealed record TieredGoal(string Label, int? Current, IReadOnlyList<int> Thresholds, int? TiersDone)
{
    /// <summary>The next threshold to reach, or null when every tier is done.</summary>
    public int? NextThreshold
    {
        get
        {
            int done = this.TiersDone ?? (this.Current is int v ? this.Thresholds.Count(t => v >= t) : 0);
            return done < this.Thresholds.Count ? this.Thresholds[done] : null;
        }
    }
}

public sealed record ProgressSummary(
    IReadOnlyList<BoardProgress> Boards,
    TieredGoal JobLevel,
    TieredGoal Befriended,
    TieredGoal Rank25,
    bool? FreeingTheBeast,
    bool? CrucibleMaster,
    bool? UnbrokenEndgame,
    IReadOnlyList<(Kin Kin, bool? Done)> KinMates,
    bool DegreesUnlocked)
{
    public int BoardsCleared => this.Boards.Count(b => b.Cleared);
}

public static class ProgressCalculator
{
    private static readonly int[] JobThresholds = [10, 20, 30, 40, 50];
    private static readonly int[] BeastThresholds = [10, 20, 30, 40, 50];

    public static ProgressSummary Summarize(CharacterSnapshot c)
    {
        var ach = c.CompletedAchievements;
        bool? Has(uint id) => ach is null ? null : ach.Contains(id);
        int? Tiers(uint[] ids) => ach is null ? null : ids.Count(ach.Contains);

        var boards = Boards.All
            .Select(b => new BoardProgress(
                b,
                c.ClearedBoards.Contains(b.Number) || ach?.Contains(b.ClearAchievement) == true,
                Has(b.ThirdDegreeAchievement),
                Has(b.ApexAchievement),
                Has(b.LegendaryAchievement)))
            .ToList();

        int? rank25 = c.UnlockedBeasts is null ? null : c.Beasts.Values.Count(b => b.Rank >= CrucibleIds.MaxRank && c.UnlockedBeasts.Contains(b.Id));
        var kinMates = Enumerable.Range(0, CrucibleIds.Achievements.KinMates.Length)
            .Select(i => ((Kin)(i + 1), Has(CrucibleIds.Achievements.KinMates[i])))
            .ToList();

        return new ProgressSummary(
            boards,
            new TieredGoal("Beastmaster level", c.JobLevel, JobThresholds, Tiers(CrucibleIds.Achievements.JobLevel)),
            new TieredGoal("Beasts befriended", c.UnlockedBeasts?.Count, BeastThresholds, Tiers(CrucibleIds.Achievements.Befriended)),
            new TieredGoal("Beasts at rank 25", rank25, BeastThresholds, Tiers(CrucibleIds.Achievements.Rank25)),
            Has(CrucibleIds.Achievements.FreeingTheBeast),
            Has(CrucibleIds.Achievements.CrucibleMaster),
            Has(CrucibleIds.Achievements.UnbrokenEndgame),
            kinMates,
            c.Quest(CrucibleIds.Quests.BlazingTrails) == QuestState.Complete);
    }
}
