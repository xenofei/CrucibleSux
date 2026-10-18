using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class JourneyRulesTests
{
    private const uint A = 1;
    private const uint B = 2;
    private const uint C = 3;
    private static readonly uint[] Zones = [A, B, C];
    private static readonly DateTime T = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    private static ProgressionState State(int level = 10, int owned = 1, int faded = 0) =>
        new(level, 50, new Dictionary<uint, QuestProgress>(), Enumerable.Range(1, owned).Select(i => (uint)i).ToHashSet(), new HashSet<int>(),
            new Dictionary<uint, int> { [CrucibleIds.FadedRemnant] = faded });

    private static Func<uint, double?> Rates(params (uint Zone, double Rate)[] rates) =>
        z => rates.Where(r => r.Zone == z).Select(r => (double?)r.Rate).FirstOrDefault();

    private static SessionReport Report(bool goal, params RunOutcome[] runs) =>
        new(T, T.AddMinutes(10), [.. runs.Select(o => new RunRecord(T, TimeSpan.FromMinutes(5), 1, 1, o, false, string.Empty))], goal, "Ended.");

    [Fact]
    public void Unlocking_is_offered_only_to_a_character_who_can_take_the_quest_and_has_not_finished_it()
    {
        static ProgressionState Char(int combat, bool prerequisite, QuestState unlock, int expansion = CrucibleIds.Dawntrail) =>
            new(0, combat, new Dictionary<uint, QuestProgress>
            {
                [QuestScripts.UnlockPrerequisite] = new(prerequisite ? QuestState.Complete : QuestState.NotStarted, 0),
                [CrucibleIds.Quests.StrangersInTheWood] = new(unlock, 1),
            }, new HashSet<uint>(), new HashSet<int>(), new Dictionary<uint, int>(), expansion);

        Assert.True(JourneyRules.CanOfferUnlock(Char(50, true, QuestState.NotStarted)));
        Assert.True(JourneyRules.CanOfferUnlock(Char(90, true, QuestState.Accepted)));
        Assert.False(JourneyRules.CanOfferUnlock(Char(50, true, QuestState.Complete)));
        Assert.False(JourneyRules.CanOfferUnlock(Char(49, true, QuestState.NotStarted)));
        Assert.False(JourneyRules.CanOfferUnlock(Char(50, false, QuestState.NotStarted)));
        // Endwalker is the latest expansion registered: the game refuses the quest.
        Assert.False(JourneyRules.CanOfferUnlock(Char(90, true, QuestState.NotStarted, expansion: 4)));
    }

    [Fact]
    public void Progress_is_the_same_for_the_same_game_state() =>
        Assert.Equal(JourneyRules.Progress("k", State(), 100, 148), JourneyRules.Progress("k", State(), 100, 148));

    [Fact]
    public void Progress_changes_with_what_the_game_shows()
    {
        string before = JourneyRules.Progress("k", State(), 100, 148);

        Assert.NotEqual(before, JourneyRules.Progress("k2", State(), 100, 148));
        Assert.NotEqual(before, JourneyRules.Progress("k", State(level: 11), 100, 148));
        Assert.NotEqual(before, JourneyRules.Progress("k", State(), 101, 148));
        Assert.NotEqual(before, JourneyRules.Progress("k", State(owned: 2), 100, 148));
        Assert.NotEqual(before, JourneyRules.Progress("k", State(faded: 2), 100, 148));
        Assert.NotEqual(before, JourneyRules.Progress("k", State(), 100, 152));
    }

    [Fact]
    public void A_finished_board_is_a_clear_whatever_the_run_said() =>
        Assert.Equal(BoardEnd.Cleared, JourneyRules.Classify(Report(true, RunOutcome.Finished)));

    [Fact]
    public void A_player_stop_stops_the_journey()
    {
        Assert.Equal(BoardEnd.Stopped, JourneyRules.Classify(Report(false, RunOutcome.Cancelled)));
        Assert.Equal(BoardEnd.Stopped, JourneyRules.Classify(Report(false)));
    }

    [Fact]
    public void Anything_else_is_a_failed_run()
    {
        Assert.Equal(BoardEnd.Failed, JourneyRules.Classify(null));
        Assert.Equal(BoardEnd.Failed, JourneyRules.Classify(Report(false, RunOutcome.Failed)));
        Assert.Equal(BoardEnd.Failed, JourneyRules.Classify(Report(false, RunOutcome.TimedOut)));
        Assert.Equal(BoardEnd.Failed, JourneyRules.Classify(Report(false, RunOutcome.NotStarted)));
        Assert.Equal(BoardEnd.Failed, JourneyRules.Classify(Report(false, RunOutcome.Cancelled, RunOutcome.Failed)));
    }

    [Fact]
    public void Unmeasured_zones_rotate_in_order()
    {
        Assert.Equal(B, JourneyRules.NextZone(Zones, A, Rates((A, 100))));
        Assert.Equal(A, JourneyRules.NextZone(Zones, C, Rates()));
    }

    [Fact]
    public void Once_all_are_measured_the_best_other_zone_is_next() =>
        Assert.Equal(C, JourneyRules.NextZone(Zones, A, Rates((A, 500), (B, 100), (C, 200))));

    [Fact]
    public void A_zone_must_beat_this_one_by_a_third()
    {
        Assert.Null(JourneyRules.BetterZone(Zones, A, Rates((A, 100), (B, 130))));
        Assert.Equal(B, JourneyRules.BetterZone(Zones, A, Rates((A, 100), (B, 140))));
        Assert.Null(JourneyRules.BetterZone(Zones, A, Rates((B, 140))));
    }

    [Fact]
    public void A_zone_just_left_for_being_idle_does_not_pull_back()
    {
        // A was left after a minute without FATEs; its old rate must not undo that at once.
        var rates = Rates((A, 300), (B, 100), (C, 200));

        Assert.Equal(C, JourneyRules.BetterZone(Zones, B, rates, avoid: A));
        Assert.Null(JourneyRules.BetterZone(Zones, B, Rates((A, 300), (B, 100)), avoid: A));
    }
}
