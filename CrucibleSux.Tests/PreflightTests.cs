using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class PreflightTests
{
    private static InstalledPlugin P(string name, string? repo = null) => new(name, new Version(99, 0), true, repo);

    private static readonly InstalledPlugin[] Plugins =
        [P("vnavmesh"), P("RotationSolver"), P("BossModReborn"), P("AutoDuty", "https://github.com/erdelf/AutoDuty")];

    private static PreflightInput Input(
        CharacterSnapshot? c = null,
        bool onBst = true,
        IEnumerable<InstalledPlugin>? plugins = null,
        string? executor = null,
        QueueEntry[]? queue = null,
        int[]? optIn = null) =>
        new(c ?? Make.Veteran(), onBst, false, DependencyCatalog.Evaluate(plugins ?? Plugins), executor, queue ?? [new QueueEntry(1, 1)], optIn ?? [1, 2, 3, 4, 5]);

    private static ReadinessCheck Get(IReadOnlyList<ReadinessCheck> checks, string key) => checks.Single(c => c.Key == key);

    [Fact]
    public void A_ready_veteran_can_start()
    {
        var checks = Preflight.Check(Input());

        Assert.All(checks, c => Assert.Equal(CheckStatus.Pass, c.Status));
        Assert.True(Preflight.CanStart(checks));
    }

    [Fact]
    public void Logged_out_fails_alone() =>
        Assert.Equal("character", Assert.Single(Preflight.Check(Input() with { Character = null })).Key);

    [Fact]
    public void Another_job_fails() =>
        Assert.Equal(CheckStatus.Fail, Get(Preflight.Check(Input(onBst: false)), "job").Status);

    [Fact]
    public void An_AutoDuty_problem_fails()
    {
        var checks = Preflight.Check(Input(executor: "AutoDuty is busy: stop it first."));

        Assert.Equal("AutoDuty is busy: stop it first.", Get(checks, "executor").Detail);
        Assert.False(Preflight.CanStart(checks));
    }

    [Fact]
    public void Wrath_alone_cannot_fight_as_Beastmaster()
    {
        var plugins = Plugins.Where(p => p.InternalName != "RotationSolver").Select(p => p.InternalName == "BossModReborn" ? P("BossMod") : p).Append(P("WrathCombo"));

        Assert.Equal(CheckStatus.Fail, Get(Preflight.Check(Input(plugins: plugins)), "rotation").Status);
    }

    [Fact]
    public void The_original_BossMod_loaded_blocks_the_session()
    {
        var checks = Preflight.Check(Input(plugins: Plugins.Append(P("BossMod"))));

        Assert.False(Preflight.CanStart(checks));
    }

    [Fact]
    public void The_original_BossMod_to_be_turned_off_warns_but_allows_the_start()
    {
        var installed = Plugins.Append(P("BossMod")).ToList();
        var input = Input() with
        {
            Dependencies = DependencyCatalog.Evaluate(DependencyCatalog.WithConflictsOff(installed)),
            TurnOff = DependencyCatalog.LoadedConflicts(DependencyCatalog.Evaluate(installed)),
        };

        var checks = Preflight.Check(input);

        Assert.True(Preflight.CanStart(checks));
        var turnOff = Get(checks, "turn-off");
        Assert.Equal(CheckStatus.Warn, turnOff.Status);
        Assert.Contains("BossMod (original)", turnOff.Detail);
    }

    [Fact]
    public void No_turn_off_row_when_nothing_conflicts() =>
        Assert.DoesNotContain(Preflight.Check(Input()), c => c.Key == "turn-off");

    [Fact]
    public void BossMod_Reborn_alone_can_fight()
    {
        var checks = Preflight.Check(Input(plugins: Plugins.Where(p => p.InternalName != "RotationSolver")));

        Assert.Equal(CheckStatus.Pass, Get(checks, "rotation").Status);
        Assert.Contains("BossMod Reborn", Get(checks, "rotation").Detail);
    }

    [Fact]
    public void Inside_a_duty_fails() =>
        Assert.Equal(CheckStatus.Fail, Get(Preflight.Check(Input() with { InDuty = true }), "place").Status);

    [Fact]
    public void A_missing_board_driver_fails() =>
        Assert.Equal(CheckStatus.Fail, Get(Preflight.Check(Input(plugins: Plugins.Where(p => p.InternalName != "AutoDuty"))), "plugins").Status);

    [Fact]
    public void An_empty_queue_fails() =>
        Assert.Equal(CheckStatus.Fail, Get(Preflight.Check(Input(queue: [])), "queue").Status);

    [Fact]
    public void Queued_boards_are_judged_in_order()
    {
        // Board 5 needs board 4 cleared first; queuing 4 before 5 satisfies that within the session.
        var c = Make.Character(jobLevel: 50, itemLevel: 135, quests: Boards.All.ToDictionary(b => b.UnlockQuest, _ => QuestState.Complete), beasts: 30, rank: 25);

        Assert.Equal(CheckStatus.Pass, Get(Preflight.Check(Input(c, queue: [new QueueEntry(4, 1), new QueueEntry(5, 1)])), "board-5").Status);
        Assert.Equal(CheckStatus.Fail, Get(Preflight.Check(Input(c, queue: [new QueueEntry(5, 1)])), "board-5").Status);
    }

    [Fact]
    public void Rankings_warn_for_boards_not_opted_in_once_they_are_open()
    {
        var check = Get(Preflight.Check(Input(queue: [new QueueEntry(1, 1), new QueueEntry(2, 1)], optIn: [2])), "rankings");

        Assert.Equal(CheckStatus.Warn, check.Status);
        Assert.Contains("First Board", check.Detail);
        Assert.DoesNotContain("Second Board", check.Detail);
    }

    [Fact]
    public void Rankings_pass_before_Blazing_Trails() =>
        Assert.Equal(CheckStatus.Pass, Get(Preflight.Check(Input(Make.Character(), optIn: [])), "rankings").Status);
}
