namespace CrucibleSux.Core;

/// <summary>One line of the session queue: run a board this many times.</summary>
public sealed record QueueEntry(int Board, int Runs);

public static class SessionQueue
{
    /// <summary>"Clear everything I can": the sitting plan's boards that can be attempted, once each, in play order.</summary>
    public static List<QueueEntry> FromPlan(SittingPlan plan) =>
        plan.Boards.Where(b => b.Verdict != BoardVerdict.Blocked).Select(b => new QueueEntry(b.Board.Number, 1)).ToList();
}
