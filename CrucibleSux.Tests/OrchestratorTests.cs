using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class OrchestratorTests
{
    private const uint Lauda = CrucibleIds.LaudaTerritory;
    private static readonly BoardInfo First = Boards.ByNumber(1);
    private static readonly BoardInfo Second = Boards.ByNumber(2);
    private static readonly DateTime T0 = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Drives an orchestrator through ticks with a clock and a game state the test changes.</summary>
    private sealed class Game(params QueueEntry[] queue)
    {
        public DateTime Now = T0;
        public uint Territory = Lauda;
        public bool Running;
        public bool Result;
        public long Activity;
        public HashSet<int> Cleared = [];
        public readonly Orchestrator O = new(queue, OrchestratorSettings.Default, T0);

        public IReadOnlyList<Command> Tick(TimeSpan? after = null)
        {
            this.Now += after ?? TimeSpan.FromSeconds(1);
            return this.O.Tick(new Observation(this.Now, this.Territory, this.Running, this.Result, this.Activity, this.Cleared));
        }

        /// <summary>Starts a run, walks into the board, shows the result and walks out.</summary>
        public void PlayThrough(BoardInfo board, bool clear = true)
        {
            Assert.Equal(CommandKind.StartBoard, Assert.Single(this.Tick(TimeSpan.FromSeconds(10))).Kind);
            this.Running = true;
            this.Tick();
            this.Territory = board.Territory;
            this.Tick();
            this.Result = true;
            this.Activity++;
            this.Tick();
            this.Result = false;
            this.Territory = Lauda;
            if (clear)
                this.Cleared.Add(board.Number);
            this.Tick();
            this.Running = false;
            this.Tick();
        }
    }

    [Fact]
    public void A_queue_of_two_boards_runs_both_and_reaches_the_goal()
    {
        var g = new Game(new QueueEntry(1, 1), new QueueEntry(2, 1));

        g.PlayThrough(First);
        g.PlayThrough(Second);

        Assert.Equal(SessionPhase.Ended, g.O.Phase);
        Assert.True(g.O.Report!.GoalReached);
        Assert.Equal([RunOutcome.Finished, RunOutcome.Finished], g.O.Runs.Select(r => r.Outcome));
        Assert.All(g.O.Runs, r => Assert.True(r.FirstClear));
    }

    [Fact]
    public void Repeat_runs_of_a_cleared_board_are_not_first_clears()
    {
        var g = new Game(new QueueEntry(1, 2)) { Cleared = [1] };

        g.PlayThrough(First);
        Assert.Equal(SessionPhase.Waiting, g.O.Phase);
        Assert.Equal(2, g.O.RunNumber);
        g.PlayThrough(First);

        Assert.True(g.O.Report!.GoalReached);
        Assert.Equal(2, g.O.Runs.Count);
        Assert.All(g.O.Runs, r => Assert.False(r.FirstClear));
    }

    [Fact]
    public void It_does_not_start_while_the_executor_is_busy()
    {
        var g = new Game(new QueueEntry(1, 1)) { Running = true };

        Assert.Empty(g.Tick(TimeSpan.FromSeconds(10)));
        Assert.Equal(SessionPhase.Waiting, g.O.Phase);
    }

    [Fact]
    public void An_executor_that_never_starts_is_retried_then_given_up()
    {
        var g = new Game(new QueueEntry(1, 1));

        g.Tick(TimeSpan.FromSeconds(10));
        g.Tick(TimeSpan.FromSeconds(30));
        Assert.Equal(SessionPhase.Waiting, g.O.Phase);
        Assert.Equal(CommandKind.StartBoard, Assert.Single(g.Tick(TimeSpan.FromSeconds(10))).Kind);
        g.Tick(TimeSpan.FromSeconds(30));

        Assert.Equal(SessionPhase.Ended, g.O.Phase);
        Assert.False(g.O.Report!.GoalReached);
        Assert.Equal([1, 2], g.O.Runs.Select(r => r.Attempt));
        Assert.All(g.O.Runs, r => Assert.Equal(RunOutcome.NotStarted, r.Outcome));
        Assert.Contains("Gave up", g.O.Report.Ending);
    }

    [Fact]
    public void A_board_never_reached_is_stopped_by_the_watchdog_and_retried()
    {
        var g = new Game(new QueueEntry(1, 1));
        g.Tick(TimeSpan.FromSeconds(10));
        g.Running = true;
        g.Tick();

        var commands = g.Tick(TimeSpan.FromMinutes(7));

        Assert.Equal(CommandKind.StopExecutor, Assert.Single(commands).Kind);
        Assert.Equal(RunOutcome.TimedOut, g.O.Runs.Single().Outcome);
        Assert.Equal(SessionPhase.Waiting, g.O.Phase);
    }

    [Fact]
    public void Being_stuck_inside_a_board_stops_the_session()
    {
        var g = new Game(new QueueEntry(1, 1), new QueueEntry(2, 1));
        g.Tick(TimeSpan.FromSeconds(10));
        g.Running = true;
        g.Tick();
        g.Territory = First.Territory;
        g.Tick();

        Assert.Empty(g.Tick(TimeSpan.FromMinutes(3)));
        g.Activity++;
        Assert.Empty(g.Tick(TimeSpan.FromMinutes(3)));
        var commands = g.Tick(TimeSpan.FromMinutes(5));

        Assert.Equal(CommandKind.StopExecutor, Assert.Single(commands).Kind);
        Assert.Equal(SessionPhase.Ended, g.O.Phase);
        Assert.Equal(RunOutcome.TimedOut, g.O.Runs.Single().Outcome);
        Assert.Contains("still inside", g.O.Report!.Ending);
    }

    [Fact]
    public void AutoDuty_stopping_inside_a_board_ends_the_session_without_retrying()
    {
        var g = new Game(new QueueEntry(1, 1));
        g.Tick(TimeSpan.FromSeconds(10));
        g.Running = true;
        g.Tick();
        g.Territory = First.Territory;
        g.Tick();
        g.Running = false;

        Assert.Empty(g.Tick());

        Assert.Equal(SessionPhase.Ended, g.O.Phase);
        Assert.Equal(RunOutcome.Failed, g.O.Runs.Single().Outcome);
    }

    [Fact]
    public void Leaving_without_a_result_fails_and_the_retry_is_attempt_two()
    {
        var g = new Game(new QueueEntry(1, 1));
        g.Tick(TimeSpan.FromSeconds(10));
        g.Running = true;
        g.Tick();
        g.Territory = First.Territory;
        g.Tick();
        g.Territory = Lauda;
        g.Running = false;
        g.Tick();
        Assert.Equal(RunOutcome.Failed, g.O.Runs.Single().Outcome);

        g.PlayThrough(First, clear: false);

        Assert.Equal(RunOutcome.Finished, g.O.Runs[^1].Outcome);
        Assert.Equal(2, g.O.Runs[^1].Attempt);
        Assert.True(g.O.Report!.GoalReached);
    }

    [Fact]
    public void A_start_that_times_out_also_stops_the_executor()
    {
        var g = new Game(new QueueEntry(1, 1));
        g.Tick(TimeSpan.FromSeconds(10));

        Assert.Equal(CommandKind.StopExecutor, Assert.Single(g.Tick(TimeSpan.FromSeconds(30))).Kind);
    }

    [Fact]
    public void An_executor_that_stays_busy_between_runs_ends_the_session()
    {
        var g = new Game(new QueueEntry(1, 2)) { Cleared = [1] };
        g.PlayThrough(First);
        g.Running = true;

        Assert.Empty(g.Tick(TimeSpan.FromSeconds(10)));
        Assert.Equal(SessionPhase.Waiting, g.O.Phase);
        g.Tick(TimeSpan.FromMinutes(2));

        Assert.Equal(SessionPhase.Ended, g.O.Phase);
        Assert.Contains("kept running", g.O.Report!.Ending);
    }

    [Fact]
    public void After_the_result_a_stop_just_before_the_exit_is_still_a_clean_run()
    {
        var g = new Game(new QueueEntry(1, 1));
        g.Tick(TimeSpan.FromSeconds(10));
        g.Running = true;
        g.Tick();
        g.Territory = First.Territory;
        g.Result = true;
        g.Tick();
        g.Running = false;
        g.Tick();
        Assert.Equal(SessionPhase.InBoard, g.O.Phase);

        g.Territory = Lauda;
        g.Tick(TimeSpan.FromSeconds(5));

        Assert.True(g.O.Report!.GoalReached);
        Assert.Equal(RunOutcome.Finished, g.O.Runs.Single().Outcome);
    }

    [Fact]
    public void A_stop_inside_after_the_result_counts_the_run_but_ends_the_session()
    {
        var g = new Game(new QueueEntry(1, 2)) { Cleared = [1] };
        g.Tick(TimeSpan.FromSeconds(10));
        g.Running = true;
        g.Tick();
        g.Territory = First.Territory;
        g.Result = true;
        g.Tick();
        g.Running = false;
        g.Tick();

        g.Tick(TimeSpan.FromMinutes(1));

        Assert.Equal(RunOutcome.Finished, g.O.Runs.Single().Outcome);
        Assert.False(g.O.Report!.GoalReached);
        Assert.Contains("still inside", g.O.Report.Ending);
    }

    [Fact]
    public void Cancelling_while_leaving_stops_the_executor()
    {
        var g = new Game(new QueueEntry(1, 1));
        g.Tick(TimeSpan.FromSeconds(10));
        g.Running = true;
        g.Tick();
        g.Territory = First.Territory;
        g.Tick();
        g.Territory = Lauda;
        g.Tick();
        Assert.Equal(SessionPhase.Leaving, g.O.Phase);

        Assert.Equal(CommandKind.StopExecutor, Assert.Single(g.O.Cancel(g.Now, "Stopped.")).Kind);
        Assert.Equal(RunOutcome.Cancelled, g.O.Runs.Single().Outcome);
    }

    [Fact]
    public void A_refused_start_outside_the_starting_phase_is_ignored()
    {
        var g = new Game(new QueueEntry(1, 1));

        g.O.StartFailed(g.Now, "late");

        Assert.Empty(g.O.Runs);
        Assert.Equal(SessionPhase.Waiting, g.O.Phase);
    }

    [Fact]
    public void A_first_clear_counts_even_without_the_result_screen()
    {
        var g = new Game(new QueueEntry(1, 1));
        g.Tick(TimeSpan.FromSeconds(10));
        g.Running = true;
        g.Tick();
        g.Territory = First.Territory;
        g.Tick();
        g.Territory = Lauda;
        g.Cleared.Add(1);
        g.Running = false;
        g.Tick();

        var run = g.O.Runs.Single();
        Assert.Equal(RunOutcome.Finished, run.Outcome);
        Assert.True(run.FirstClear);
    }

    [Fact]
    public void AutoDuty_running_on_after_the_board_is_stopped()
    {
        var g = new Game(new QueueEntry(1, 1));
        g.Tick(TimeSpan.FromSeconds(10));
        g.Running = true;
        g.Tick();
        g.Territory = First.Territory;
        g.Result = true;
        g.Tick();
        g.Territory = Lauda;
        g.Tick();

        var commands = g.Tick(TimeSpan.FromMinutes(2));

        Assert.Equal(CommandKind.StopExecutor, Assert.Single(commands).Kind);
        Assert.Equal(RunOutcome.Finished, g.O.Runs.Single().Outcome);
        Assert.True(g.O.Report!.GoalReached);
    }

    [Fact]
    public void Cancelling_mid_run_records_it_and_stops_the_executor()
    {
        var g = new Game(new QueueEntry(1, 1));
        g.Tick(TimeSpan.FromSeconds(10));
        g.Running = true;
        g.Tick();

        var commands = g.O.Cancel(g.Now, "Stopped by the player.");

        Assert.Equal(CommandKind.StopExecutor, Assert.Single(commands).Kind);
        Assert.Equal(RunOutcome.Cancelled, g.O.Runs.Single().Outcome);
        Assert.Equal("Stopped by the player.", g.O.Report!.Ending);
        Assert.Empty(g.Tick());
    }

    [Fact]
    public void Cancelling_between_runs_leaves_the_executor_alone()
    {
        var g = new Game(new QueueEntry(1, 1));

        Assert.Empty(g.O.Cancel(g.Now, "Logged out."));
        Assert.Empty(g.O.Runs);
        Assert.Equal(SessionPhase.Ended, g.O.Phase);
    }

    [Fact]
    public void A_refused_start_ends_the_session()
    {
        var g = new Game(new QueueEntry(1, 1));
        g.Tick(TimeSpan.FromSeconds(10));

        g.O.StartFailed(g.Now, "settings rejected");

        Assert.Equal(SessionPhase.Ended, g.O.Phase);
        Assert.Equal(RunOutcome.NotStarted, g.O.Runs.Single().Outcome);
    }

    [Fact]
    public void An_empty_queue_is_rejected() =>
        Assert.Throws<ArgumentException>(() => new Orchestrator([new QueueEntry(1, 0)], OrchestratorSettings.Default, T0));

    [Fact]
    public void The_report_counts_outcomes_and_names_first_clears()
    {
        var runs = new[]
        {
            new RunRecord(T0, TimeSpan.FromMinutes(20), 1, 1, RunOutcome.Finished, true, ""),
            new RunRecord(T0, TimeSpan.FromMinutes(5), 2, 1, RunOutcome.Failed, false, ""),
        };
        var report = new SessionReport(T0, T0.AddMinutes(65), runs, false, "Gave up.");

        Assert.Equal(
            ["2 run(s) in 1h 05m: 1 finished, 1 failed.", "First clears: First Board of the Unbroken.", "Gave up."],
            report.Lines());
    }

    [Fact]
    public void The_queue_from_a_sitting_plan_skips_the_blocked_board()
    {
        var plan = ReadinessEvaluator.PlanSitting(Make.Character(jobLevel: 40, itemLevel: 35,
            quests: new() { [CrucibleIds.Quests.IntoTheCrucible] = QuestState.Complete, [CrucibleIds.Quests.Gobsmacked] = QuestState.Complete }));

        Assert.Equal([new QueueEntry(1, 1), new QueueEntry(2, 1)], SessionQueue.FromPlan(plan));
    }
}
