using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class ProgressTests
{
    [Fact]
    public void Achievements_unknown_until_the_list_loads()
    {
        var p = ProgressCalculator.Summarize(Make.Character(cleared: [1]));

        Assert.True(p.Boards[0].Cleared);
        Assert.Null(p.Boards[0].Legendary);
        Assert.Null(p.CrucibleMaster);
        Assert.Null(p.Befriended.TiersDone);
        Assert.Equal(1, p.BoardsCleared);
    }

    [Fact]
    public void Board_milestones_come_from_their_achievements()
    {
        var b2 = Boards.ByNumber(2);
        var p = ProgressCalculator.Summarize(Make.Character(achievements: [b2.ClearAchievement, b2.ApexAchievement]));

        var board = p.Boards.Single(x => x.Board.Number == 2);
        Assert.True(board.Cleared);
        Assert.True(board.Apex);
        Assert.False(board.Legendary);
        Assert.False(board.ThirdDegree);
    }

    [Fact]
    public void Tiered_goals_report_the_next_threshold()
    {
        var p = ProgressCalculator.Summarize(Make.Character(jobLevel: 34, beasts: 23, rank: 25, achievements: [4023, 4024, 4025, 4028, 4029]));

        Assert.Equal(40, p.JobLevel.NextThreshold);
        Assert.Equal(23, p.Befriended.Current);
        Assert.Equal(30, p.Befriended.NextThreshold);
        Assert.Equal(23, p.Rank25.Current);
    }

    [Fact]
    public void Degrees_unlock_with_Blazing_Trails()
    {
        Assert.False(ProgressCalculator.Summarize(Make.Character()).DegreesUnlocked);
        Assert.True(ProgressCalculator.Summarize(Make.Veteran()).DegreesUnlocked);
    }
}
