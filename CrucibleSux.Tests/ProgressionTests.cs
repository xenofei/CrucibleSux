using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class ProgressionTests
{
    private static readonly ProgressionOptions All = new(true, true, true);

    private static readonly uint[] Capturable = [.. BeastSources.All.Where(b => b.CanCapture).Select(b => b.Beast)];

    private static ProgressionState State(
        int level = 1,
        uint[]? done = null,
        (uint Id, byte Seq)? accepted = null,
        IEnumerable<uint>? owned = null,
        int[]? boards = null,
        Dictionary<uint, int>? items = null,
        int otherJob = 100)
    {
        var quests = (done ?? []).ToDictionary(q => q, _ => new QuestProgress(QuestState.Complete, 0));
        quests[QuestScripts.UnlockPrerequisite] = new(QuestState.Complete, 0);
        if (accepted is { } a)
            quests[a.Id] = new(QuestState.Accepted, a.Seq);
        return new(level, otherJob, quests, (owned ?? [Beast.CuSith]).ToHashSet(), (boards ?? []).ToHashSet(), items ?? []);
    }

    /// <summary>Every job quest before <paramref name="quest"/>, complete.</summary>
    private static uint[] Before(uint quest) => [.. QuestScripts.All.Select(q => q.Id).TakeWhile(id => id != quest)];

    [Fact]
    public void Nothing_turned_on_is_blocked() =>
        Assert.Equal(ActivityKind.Blocked, ProgressionPlanner.Next(State(), new(false, false, false)).Kind);

    [Fact]
    public void A_new_player_accepts_the_unlock_quest()
    {
        var a = ProgressionPlanner.Next(State(level: 0, owned: []), All);

        Assert.Equal((ActivityKind.Quest, 71026u), (a.Kind, a.Quest));
        Assert.Null(a.Step);
    }

    [Fact]
    public void The_unlock_quest_waits_for_the_main_scenario()
    {
        var s = State(level: 0, owned: []);
        s = s with { Quests = new Dictionary<uint, QuestProgress>() };

        Assert.Contains("main scenario", ProgressionPlanner.Next(s, All).Why);
    }

    [Fact]
    public void Beasts_alone_cannot_unlock_the_job()
    {
        var a = ProgressionPlanner.Next(State(level: 0, owned: []), new(false, true, true));

        Assert.Equal(ActivityKind.Blocked, a.Kind);
        Assert.Contains("job quests", a.Why);
    }

    [Fact]
    public void An_accepted_quest_runs_the_step_for_its_sequence()
    {
        var a = ProgressionPlanner.Next(State(done: Before(71027), accepted: (71027, 1)), All);

        Assert.Equal(ActivityKind.Quest, a.Kind);
        Assert.Equal(1059322u, a.Step?.At.DataId);
    }

    [Fact]
    public void An_unknown_sequence_is_blocked_not_guessed()
    {
        var a = ProgressionPlanner.Next(State(done: Before(71027), accepted: (71027, 9)), new(true, false, false));

        Assert.Equal(ActivityKind.Blocked, a.Kind);
        Assert.Contains("sequence 9", a.Why);
    }

    [Fact]
    public void A_held_gourd_is_used_first() =>
        Assert.Equal(
            (ActivityKind.UseGourd, QuestScripts.CuSithGourd),
            Next(State(owned: [], items: new() { [QuestScripts.CuSithGourd] = 1 }), new(false, true, false)));

    [Fact]
    public void A_summon_step_uses_the_gourd_of_a_beast_not_yet_owned()
    {
        var s = State(done: Before(71027), accepted: (71027, 3), owned: [], items: new() { [QuestScripts.CuSithGourd] = 1 });

        Assert.Equal((ActivityKind.UseGourd, QuestScripts.CuSithGourd), Next(s, new(true, false, false)));
    }

    [Fact]
    public void A_summon_step_without_the_beast_or_its_gourd_is_blocked()
    {
        var a = ProgressionPlanner.Next(State(done: Before(71027), accepted: (71027, 3), owned: []), new(true, false, false));

        Assert.Equal(ActivityKind.Blocked, a.Kind);
        Assert.Contains("Cu Sith", a.Why);
    }

    [Fact]
    public void A_beast_count_step_captures_what_is_in_reach()
    {
        var a = ProgressionPlanner.Next(State(done: Before(71027), accepted: (71027, 255)), All);

        Assert.Equal(ActivityKind.Capture, a.Kind);
        Assert.Equal(new uint[] { 2, 10 }, a.Beasts!);
    }

    [Fact]
    public void A_level_step_levels_when_allowed_and_blocks_when_not()
    {
        var s = State(level: 10, done: Before(71028), accepted: (71028, 3));

        Assert.Equal((ActivityKind.Level, 14), (ProgressionPlanner.Next(s, All).Kind, ProgressionPlanner.Next(s, All).Level));
        Assert.Equal(ActivityKind.Blocked, ProgressionPlanner.Next(s, new(true, false, false)).Kind);
    }

    [Fact]
    public void A_named_beast_step_captures_that_beast()
    {
        var a = ProgressionPlanner.Next(State(level: 14, done: Before(71028), accepted: (71028, 5)), All);

        Assert.Equal(ActivityKind.Capture, a.Kind);
        Assert.Equal(new[] { Beast.Geshunpest }, a.Beasts!);
    }

    [Fact]
    public void A_kin_step_captures_any_beast_of_that_kin()
    {
        var a = ProgressionPlanner.Next(State(level: 22, done: Before(71029), accepted: (71029, 4), owned: [1, 5, 20]), All);

        Assert.Equal(ActivityKind.Capture, a.Kind);
        Assert.All(a.Beasts!, b => Assert.Contains(BeastSources.ById(b).Kin, new[] { Kin.Wavekin, Kin.Soulkin }));
    }

    [Fact]
    public void A_board_gate_runs_the_board()
    {
        var a = ProgressionPlanner.Next(State(level: 30, done: Before(71031), owned: Enumerable.Range(1, 20).Select(i => (uint)i)), All);

        Assert.Equal((ActivityKind.RunBoard, 1), (a.Kind, a.Board));
    }

    [Fact]
    public void A_blocked_quest_lets_the_other_options_run()
    {
        var a = ProgressionPlanner.Next(State(level: 10, done: Before(71028), accepted: (71028, 3)), new(true, true, false));

        Assert.Equal(ActivityKind.Capture, a.Kind);
    }

    [Fact]
    public void The_cheapest_gourd_is_bought_once_captures_are_done()
    {
        var s = State(
            level: 50,
            done: [.. QuestScripts.All.Select(q => q.Id)],
            owned: [Beast.CuSith, .. Capturable],
            items: new() { [CrucibleIds.FadedRemnant] = 100 });

        Assert.Equal((ActivityKind.BuyGourd, 49807u), Next(s, All));
    }

    [Fact]
    public void Bright_gourds_can_be_paid_in_faded_remnants()
    {
        var s = State(
            level: 50,
            done: [.. QuestScripts.All.Select(q => q.Id)],
            owned: BeastSources.All.Where(b => b.Currency != CrucibleIds.BrightRemnant).Select(b => b.Beast),
            items: new() { [CrucibleIds.FadedRemnant] = 70 });

        Assert.Equal((ActivityKind.BuyGourd, 49813u), Next(s, All));
    }

    [Fact]
    public void Short_of_remnants_farms_the_highest_cleared_board()
    {
        var s = State(
            level: 50,
            done: [.. QuestScripts.All.Select(q => q.Id)],
            owned: [Beast.CuSith, .. Capturable],
            boards: [1, 2, 3]);

        var a = ProgressionPlanner.Next(s, new(false, true, false));

        Assert.Equal((ActivityKind.RunBoard, 3), (a.Kind, a.Board));
    }

    [Fact]
    public void Everything_owned_at_fifty_is_done()
    {
        var s = State(level: 50, done: [.. QuestScripts.All.Select(q => q.Id)], owned: BeastSources.All.Select(b => b.Beast));

        Assert.Equal(ActivityKind.Done, ProgressionPlanner.Next(s, All).Kind);
    }

    [Fact]
    public void Leveling_alone_levels_to_the_cap()
    {
        var a = ProgressionPlanner.Next(State(level: 20), new(false, false, true));

        Assert.Equal((ActivityKind.Level, ProgressionPlanner.MaxLevel), (a.Kind, a.Level));
    }

    private static (ActivityKind, uint) Next(ProgressionState s, ProgressionOptions o)
    {
        var a = ProgressionPlanner.Next(s, o);
        return (a.Kind, a.Item);
    }
}

public class BeastSourceTests
{
    [Fact]
    public void Every_beast_has_one_row_in_order() =>
        Assert.Equal(Enumerable.Range(1, CrucibleIds.TotalBeasts).Select(i => (uint)i), BeastSources.All.Select(b => b.Beast));

    [Fact]
    public void Every_beast_can_be_obtained()
    {
        Assert.All(BeastSources.All, b => Assert.True(b.CanCapture || b.Gourd != 0, b.Name));
        Assert.All(BeastSources.All, b => Assert.NotEqual(Kin.Unknown, b.Kin));
        Assert.All(BeastSources.All.Where(b => b.CanCapture), b => Assert.NotEmpty(b.Spawns));
    }

    [Fact]
    public void Counts_match_the_sheets()
    {
        Assert.Equal(37, BeastSources.All.Count(b => b.CanCapture));
        Assert.Equal(15, BeastSources.All.Count(b => b.IsSold));
        Assert.Equal(16, BeastSources.All.Select(b => b.Gourd).Where(g => g != 0).Distinct().Count());
        Assert.All(BeastSources.All.Where(b => b.Gourd != 0), b => Assert.InRange(b.Gourd, 49805u, 49820u));
    }

    [Fact]
    public void The_whole_shop_costs_what_the_sheet_says()
    {
        Assert.Equal(125, BeastSources.All.Where(b => b.Currency == CrucibleIds.FadedRemnant).Sum(b => b.Price));
        Assert.Equal(290, BeastSources.All.Where(b => b.Currency == CrucibleIds.BrightRemnant).Sum(b => b.Price));
    }

    [Theory]
    [InlineData(21.5f, 0f)]
    [InlineData(1f, -1024f)]
    [InlineData(42f, 1024f)]
    public void Map_coordinates_convert_to_world(float map, float world)
    {
        Assert.Equal(world, Spawn.ToWorld(map), 2);
        Assert.Equal(map, Spawn.ToMap(world), 2);
    }

    [Fact]
    public void Quest_needs_name_real_beasts()
    {
        var needs = QuestScripts.All.SelectMany(q => q.Steps.Select(s => s.Need).Append(q.Accept)).OfType<StepNeed>();

        Assert.All(needs.SelectMany(n => (n.Owned ?? []).Append(n.Summoned)).Where(b => b != 0), b => BeastSources.ById(b));
        Assert.All(needs.SelectMany(n => n.OwnedKin ?? []), k => Assert.Contains(BeastSources.All, b => b.Kin == k && b.CanCapture));
    }

    [Fact]
    public void Every_quest_ends_with_a_turn_in() =>
        Assert.All(QuestScripts.All, q => Assert.Contains(q.Steps, s => s.Sequence == 255));

    [Fact]
    public void Every_sold_gourd_is_in_the_shop_for_its_currency_and_quest() =>
        Assert.All(BeastSources.All.Where(b => b.IsSold), b =>
            Assert.Contains(new ShopLine(b.Gourd, b.GourdQuest), KornagoShop.For(b.Currency).Lines));

    [Fact]
    public void Shop_positions_skip_lines_the_window_hides()
    {
        bool Upto34(uint q) => q is CrucibleIds.Quests.IntoTheCrucible or CrucibleIds.Quests.ABeastmastersPath;

        Assert.Equal(4, KornagoShop.Position(KornagoShop.Faded, 49810, Upto34));
        Assert.Equal(-1, KornagoShop.Position(KornagoShop.Faded, 49811, Upto34));
        Assert.Equal(7, KornagoShop.Position(KornagoShop.Faded, CrucibleIds.BrightRemnant, Upto34));
        Assert.Equal(11, KornagoShop.Position(KornagoShop.Faded, CrucibleIds.BrightRemnant, _ => true));
        Assert.Equal(-1, KornagoShop.Position(KornagoShop.Faded, CrucibleIds.BrightRemnant, _ => false));
    }
}
