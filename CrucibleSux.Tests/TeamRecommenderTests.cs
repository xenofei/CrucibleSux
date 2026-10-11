using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class TeamRecommenderTests
{
    private static readonly BoardInfo Board1 = Boards.ByNumber(1);

    [Fact]
    public void The_roster_is_capped_at_the_team_size_and_each_battle_gets_three()
    {
        var owned = Enumerable.Range(1, 14).Select(i => Make.Beast((uint)i)).ToList();
        var battles = new[] { new BattleInfo(1, 0, true, [Make.Enemy()]), new BattleInfo(1, 1, false, [Make.Enemy()]) };

        var plan = TeamRecommender.Recommend(Board1, battles, owned, new Dictionary<uint, BeastProgress>());

        Assert.Equal(Board1.TeamSize, plan.Roster.Count);
        Assert.All(plan.Battles, b => Assert.Equal(TeamRecommender.BattleTeam, b.Beasts.Count));
        Assert.All(plan.Battles.SelectMany(b => b.Beasts), s => Assert.Contains(plan.Roster, r => r.Beast.Id == s.Beast.Id));
    }

    [Fact]
    public void Ranks_are_synced_down_to_the_board()
    {
        var beast = Make.Beast(1);
        var progress = new Dictionary<uint, BeastProgress> { [1] = new(1, 25, 0, false, DateTime.UnixEpoch) };

        Assert.Equal(Board1.SyncRank, TeamRecommender.RankOf(beast, progress, Board1));
        Assert.Equal(TeamRecommender.UnseenRank, TeamRecommender.RankOf(Make.Beast(2), progress, Board1));
    }

    [Fact]
    public void A_physical_beast_is_preferred_against_low_physical_resistance()
    {
        var physical = Make.Beast(1, str: 30, @int: 5);
        var magical = Make.Beast(2, str: 5, @int: 30);
        var battle = new BattleInfo(1, 0, false, [Make.Enemy(pr: 1, mr: 5)]);

        Assert.True(TeamRecommender.Score(physical, 5, battle).Score > TeamRecommender.Score(magical, 5, battle).Score);
    }

    [Fact]
    public void A_status_the_enemy_resists_earns_nothing()
    {
        var sleeper = Make.Beast(1, inflicts: 0b1000_0000);
        var open = new BattleInfo(1, 0, false, [Make.Enemy(resist: 0)]);
        var immune = new BattleInfo(1, 0, false, [Make.Enemy(resist: 0b1000_0000)]);

        Assert.True(TeamRecommender.Score(sleeper, 5, open).Score > TeamRecommender.Score(sleeper, 5, immune).Score);
        Assert.Contains(TeamRecommender.Score(sleeper, 5, open).Reasons, r => r.Contains("status"));
    }
}
