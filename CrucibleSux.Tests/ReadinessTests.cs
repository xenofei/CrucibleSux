using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class ReadinessTests
{
    [Fact]
    public void Boards_are_numbered_one_to_five_with_distinct_duties()
    {
        Assert.Equal([1, 2, 3, 4, 5], Boards.All.Select(b => b.Number));
        Assert.Equal(5, Boards.All.Select(b => b.ContentFinderCondition).Distinct().Count());
        Assert.Equal(Boards.ByNumber(3), Boards.ByTerritory(1341));
        Assert.Null(Boards.ByTerritory(CrucibleIds.LaudaTerritory));
    }

    [Fact]
    public void Low_job_level_blocks_the_board()
    {
        var r = ReadinessEvaluator.Evaluate(Boards.ByNumber(2), Make.Character(jobLevel: 35));

        Assert.Equal(BoardVerdict.Blocked, r.Verdict);
        Assert.Equal(CheckStatus.Fail, r.Checks.Single(c => c.Key == "job-level").Status);
    }

    [Fact]
    public void A_ready_character_passes_every_check_on_the_first_board()
    {
        var r = ReadinessEvaluator.Evaluate(Boards.ByNumber(1), Make.Character(jobLevel: 30, itemLevel: 30, beasts: 10, rank: 5));

        Assert.Equal(BoardVerdict.Ready, r.Verdict);
        Assert.All(r.Checks, c => Assert.Equal(CheckStatus.Pass, c.Status));
    }

    [Fact]
    public void An_accepted_unlock_quest_makes_the_board_risky_not_blocked()
    {
        var c = Make.Character(jobLevel: 40, itemLevel: 40, rank: 10, quests: new() { [CrucibleIds.Quests.Gobsmacked] = QuestState.Accepted });

        var r = ReadinessEvaluator.Evaluate(Boards.ByNumber(2), c);

        Assert.Equal(BoardVerdict.Risky, r.Verdict);
        Assert.Equal(CheckStatus.Warn, r.Checks.Single(x => x.Key == "unlock-quest").Status);
    }

    [Fact]
    public void Unseen_ranks_and_unloaded_beast_list_are_unknown_not_failures()
    {
        var r = ReadinessEvaluator.Evaluate(Boards.ByNumber(1), Make.Character(beasts: null, rank: null));

        Assert.Equal(BoardVerdict.Risky, r.Verdict);
        Assert.Equal(CheckStatus.Unknown, r.Checks.Single(x => x.Key == "roster").Status);
        Assert.Equal(CheckStatus.Unknown, r.Checks.Single(x => x.Key == "ranks").Status);
    }

    [Fact]
    public void Ranks_below_the_recommendation_warn()
    {
        var r = ReadinessEvaluator.Evaluate(Boards.ByNumber(3), Make.Veteran() with { Beasts = RankAll(30, 10) });

        Assert.Equal(CheckStatus.Warn, r.Checks.Single(x => x.Key == "ranks").Status);
    }

    [Fact]
    public void Item_level_below_the_minimum_fails_and_below_the_target_warns()
    {
        var b3 = Boards.ByNumber(3);
        Assert.Equal(CheckStatus.Fail, ReadinessEvaluator.Evaluate(b3, Make.Veteran() with { ItemLevel = 80 }).Checks.Single(x => x.Key == "item-level").Status);
        Assert.Equal(CheckStatus.Warn, ReadinessEvaluator.Evaluate(b3, Make.Veteran() with { ItemLevel = 90 }).Checks.Single(x => x.Key == "item-level").Status);
        Assert.Equal(CheckStatus.Pass, ReadinessEvaluator.Evaluate(b3, Make.Veteran() with { ItemLevel = 100 }).Checks.Single(x => x.Key == "item-level").Status);
    }

    [Fact]
    public void Masters_boards_need_their_beast_count()
    {
        var c = Make.Veteran() with { UnlockedBeasts = Enumerable.Range(1, 24).Select(i => (uint)i).ToHashSet() };

        var r = ReadinessEvaluator.Evaluate(Boards.ByNumber(4), c);

        Assert.Equal(BoardVerdict.Blocked, r.Verdict);
        Assert.Equal(CheckStatus.Fail, r.Checks.Single(x => x.Key == "required-beasts").Status);
    }

    [Fact]
    public void A_veteran_can_clear_everything_in_one_sitting()
    {
        var plan = ReadinessEvaluator.PlanSitting(Make.Veteran());

        Assert.True(plan.CoversEverything);
        Assert.Equal([1, 2, 3, 4, 5], plan.Boards.Select(b => b.Board.Number));
        // The Second Master's Board needs the First Master's cleared: the sitting counts it as cleared on the way.
        Assert.Equal(CheckStatus.Pass, plan.Boards[^1].Checks.Single(x => x.Key == "required-board").Status);
    }

    [Fact]
    public void The_sitting_skips_cleared_boards_and_stops_at_a_quest_gate()
    {
        var c = Make.Character(jobLevel: 50, itemLevel: 100, rank: 15, cleared: [1],
            quests: new() { [CrucibleIds.Quests.IntoTheCrucible] = QuestState.Complete, [CrucibleIds.Quests.Gobsmacked] = QuestState.Complete });

        var plan = ReadinessEvaluator.PlanSitting(c);

        Assert.False(plan.CoversEverything);
        Assert.Equal([2, 3], plan.Boards.Select(b => b.Board.Number));
        Assert.Equal(3, plan.StopsAt!.Board.Number);
    }

    private static Dictionary<uint, BeastProgress> RankAll(int count, int rank) =>
        Enumerable.Range(1, count).ToDictionary(i => (uint)i, i => new BeastProgress((uint)i, rank, 0, false, DateTime.UnixEpoch));
}
