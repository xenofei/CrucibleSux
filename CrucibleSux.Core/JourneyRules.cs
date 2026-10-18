namespace CrucibleSux.Core;

/// <summary>How a board session the journey started ended.</summary>
public enum BoardEnd
{
    /// <summary>The run finished; the planner reads the cleared boards and remnants back from the game.</summary>
    Cleared,

    /// <summary>The player stopped the session (or logged out): the journey stops too rather than start it again.</summary>
    Stopped,

    /// <summary>The run failed, timed out or never started.</summary>
    Failed,
}

/// <summary>What a journey run aims for and where it stops.</summary>
public enum JourneyGoal
{
    /// <summary>The parts turned on in the Journey tab, until they are done.</summary>
    Parts,

    /// <summary>"Strangers in the Wood" and Beastmaster's gear setup, then stop.</summary>
    Unlock,

    /// <summary>The job quests, with the levels and beasts they ask for, until "Into the Crucible" opens the First Board.</summary>
    Crucible,
}

/// <summary>The journey runner's decisions that need no game: what counts as progress, how a board ended, where to level.</summary>
public static class JourneyRules
{
    /// <summary>
    /// Whether to offer to unlock Beastmaster: "Strangers in the Wood" is not finished, and the character can take it
    /// (Dawntrail registered, the main scenario through <see cref="QuestScripts.UnlockPrerequisite"/> and another combat job at
    /// its level).
    /// </summary>
    /// <summary>Beastmaster is unlocked, and "Into the Crucible", which opens the First Board, is not done yet.</summary>
    public static bool CanOfferCrucible(ProgressionState s) =>
        s.IsComplete(CrucibleIds.Quests.StrangersInTheWood) && !s.IsComplete(CrucibleIds.Quests.IntoTheCrucible);

    public static bool CanOfferUnlock(ProgressionState s)
    {
        var unlock = QuestScripts.ById(CrucibleIds.Quests.StrangersInTheWood);
        return !s.IsComplete(unlock.Id) && s.MaxExpansion >= CrucibleIds.Dawntrail && s.IsComplete(unlock.PreviousQuest)
            && s.HighestCombatLevel >= unlock.Level;
    }

    /// <summary>
    /// What the stuck check compares between ticks. It changes only when the game shows progress: a new activity or quest
    /// sequence (both in <paramref name="activityKey"/>), a level, EXP, a beast, an item, or a new zone.
    /// </summary>
    public static string Progress(string activityKey, ProgressionState s, int exp, uint territory) =>
        $"{activityKey}|{s.JobLevel}|{exp}|{s.Owned.Count}|{string.Join(",", s.Inventory.OrderBy(i => i.Key).Select(i => $"{i.Key}:{i.Value}"))}|{territory}";

    public static BoardEnd Classify(SessionReport? report) => report switch
    {
        null => BoardEnd.Failed,
        { GoalReached: true } => BoardEnd.Cleared,

        // A cancel records a Cancelled run, or none at all when it came before the run began.
        { Runs: [] } or { Runs: [.., { Outcome: RunOutcome.Cancelled }] } => BoardEnd.Stopped,
        _ => BoardEnd.Failed,
    };

    /// <summary>The next zone of the band to try: the one after <paramref name="current"/>, or the best measured once all are.</summary>
    public static uint NextZone(IReadOnlyList<uint> zones, uint current, Func<uint, double?> rate)
    {
        if (zones.Count > 1 && zones.All(z => rate(z) is not null))
            return zones.Where(z => z != current).OrderByDescending(rate).First();
        int i = zones.ToList().IndexOf(current);
        return zones[(i + 1) % zones.Count];
    }

    /// <summary>
    /// Another zone of the band whose measured EXP per hour beats <paramref name="here"/>'s by a third, if any.
    /// <paramref name="avoid"/> is a zone left recently for having nothing to do: its old rate must not pull the player back.
    /// </summary>
    public static uint? BetterZone(IReadOnlyList<uint> zones, uint here, Func<uint, double?> rate, uint avoid = 0)
    {
        if (rate(here) is not { } now)
            return null;
        var best = zones.Where(z => z != here && z != avoid && rate(z) > now * 1.33).OrderByDescending(rate).ToList();
        return best.Count > 0 ? best[0] : null;
    }
}
