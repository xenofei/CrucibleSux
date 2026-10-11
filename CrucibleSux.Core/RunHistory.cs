namespace CrucibleSux.Core;

public enum RunOutcome
{
    /// <summary>The board was played to its result screen, or the game now reports it cleared.</summary>
    Finished,

    /// <summary>The run ended without a result screen: the executor gave up, or the board was left early.</summary>
    Failed,

    /// <summary>The watchdog stopped it: the board was never reached, or nothing changed for too long.</summary>
    TimedOut,

    /// <summary>The executor would not start the board.</summary>
    NotStarted,

    /// <summary>The player stopped the session.</summary>
    Cancelled,
}

/// <summary>One attempt at a board, as the run history stores it.</summary>
/// <param name="Attempt">1 for the first try at this run, 2 for its retry, and so on.</param>
/// <param name="FirstClear">The game reported the board cleared for the first time during this run.</param>
public sealed record RunRecord(DateTime StartedUtc, TimeSpan Duration, int Board, int Attempt, RunOutcome Outcome, bool FirstClear, string Detail);

/// <summary>What a session did, for the return report.</summary>
/// <param name="GoalReached">Every queued run finished.</param>
/// <param name="Ending">Why the session ended, in a sentence.</param>
public sealed record SessionReport(DateTime StartedUtc, DateTime EndedUtc, IReadOnlyList<RunRecord> Runs, bool GoalReached, string Ending)
{
    public IEnumerable<string> Lines()
    {
        var span = this.EndedUtc - this.StartedUtc;
        string duration = span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes:00}m" : $"{span.Minutes}m {span.Seconds:00}s";
        var counts = this.Runs.GroupBy(r => r.Outcome).OrderBy(g => g.Key).Select(g => $"{g.Count()} {Word(g.Key)}");
        yield return this.Runs.Count == 0
            ? $"No board was started ({duration})."
            : $"{this.Runs.Count} run(s) in {duration}: {string.Join(", ", counts)}.";

        var firsts = this.Runs.Where(r => r.FirstClear).Select(r => Boards.ByNumber(r.Board).Name).ToList();
        if (firsts.Count > 0)
            yield return $"First clears: {string.Join(", ", firsts)}.";
        yield return this.Ending;
    }

    public static string Word(RunOutcome outcome) => outcome switch
    {
        RunOutcome.Finished => "finished",
        RunOutcome.Failed => "failed",
        RunOutcome.TimedOut => "timed out",
        RunOutcome.NotStarted => "not started",
        _ => "cancelled",
    };
}
