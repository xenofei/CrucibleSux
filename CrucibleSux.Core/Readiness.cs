namespace CrucibleSux.Core;

public enum CheckStatus
{
    Pass,
    Warn,
    Unknown,
    Fail,
}

/// <summary>One readiness rule's outcome. <paramref name="Hint"/> says how to fix a Warn or Fail.</summary>
public sealed record ReadinessCheck(string Key, string Label, CheckStatus Status, string Detail, string? Hint = null);

public enum BoardVerdict
{
    /// <summary>Every check passes.</summary>
    Ready,

    /// <summary>It can be attempted, but a check warns or could not be read.</summary>
    Risky,

    /// <summary>A hard requirement is missing: the board cannot be entered yet.</summary>
    Blocked,
}

public sealed record BoardReadiness(BoardInfo Board, bool Cleared, BoardVerdict Verdict, IReadOnlyList<ReadinessCheck> Checks);

/// <summary>
/// The answer to "what can I do in one sitting": the boards in play order, each judged as if the boards before it in
/// the sitting were cleared, and the first board that stops the sitting.
/// </summary>
public sealed record SittingPlan(IReadOnlyList<BoardReadiness> Boards, BoardReadiness? StopsAt)
{
    public bool CoversEverything => this.StopsAt is null;
}

public static class ReadinessEvaluator
{
    /// <summary>
    /// How many of the strongest beasts are compared with the recommended rank: the three that fight a battle.
    /// </summary>
    public const int CoreTeam = 3;

    public static BoardReadiness Evaluate(BoardInfo board, CharacterSnapshot c) => Evaluate(board, c, c.ClearedBoards);

    /// <summary>The boards still to clear, in order, judged as one sitting.</summary>
    public static SittingPlan PlanSitting(CharacterSnapshot c)
    {
        var cleared = new HashSet<int>(c.ClearedBoards);
        var result = new List<BoardReadiness>();
        BoardReadiness? stop = null;
        foreach (var board in Boards.All)
        {
            if (c.ClearedBoards.Contains(board.Number))
                continue;
            var r = Evaluate(board, c, cleared);
            result.Add(r);
            if (r.Verdict == BoardVerdict.Blocked)
            {
                stop = r;
                break;
            }
            cleared.Add(board.Number);
        }
        return new SittingPlan(result, stop);
    }

    private static BoardReadiness Evaluate(BoardInfo board, CharacterSnapshot c, IReadOnlySet<int> cleared)
    {
        var checks = new List<ReadinessCheck>
        {
            JobLevel(board, c),
            UnlockQuest(board, c),
        };
        if (board.RequiredBeasts > 0)
            checks.Add(RequiredBeasts(board, c));
        if (board.RequiredBoard > 0)
            checks.Add(RequiredBoard(board, cleared));
        checks.Add(ItemLevel(board, c));
        checks.Add(Roster(board, c));
        checks.Add(Ranks(board, c));

        var verdict = checks.Any(x => x.Status == CheckStatus.Fail) ? BoardVerdict.Blocked
            : checks.Any(x => x.Status is CheckStatus.Warn or CheckStatus.Unknown) ? BoardVerdict.Risky
            : BoardVerdict.Ready;
        return new BoardReadiness(board, c.ClearedBoards.Contains(board.Number), verdict, checks);
    }

    private static ReadinessCheck JobLevel(BoardInfo b, CharacterSnapshot c) =>
        c.JobLevel >= b.JobLevel
            ? new("job-level", "Beastmaster level", CheckStatus.Pass, $"Level {c.JobLevel} (needs {b.JobLevel})")
            : new("job-level", "Beastmaster level", CheckStatus.Fail, $"Level {c.JobLevel}, needs {b.JobLevel}",
                $"Level Beastmaster to {b.JobLevel}: earlier boards and the job quests give EXP.");

    private static ReadinessCheck UnlockQuest(BoardInfo b, CharacterSnapshot c) => c.Quest(b.UnlockQuest) switch
    {
        QuestState.Complete => new("unlock-quest", "Unlock quest", CheckStatus.Pass, "Done"),
        QuestState.Accepted => new("unlock-quest", "Unlock quest", CheckStatus.Warn, "In progress",
            "The quest is accepted: talk to Lauda and pick the quest's option to run this board for it."),
        _ => new("unlock-quest", "Unlock quest", CheckStatus.Fail, "Not done",
            "Continue the Beastmaster job quests; this board opens with its quest."),
    };

    private static ReadinessCheck RequiredBeasts(BoardInfo b, CharacterSnapshot c)
    {
        if (c.UnlockedBeasts is null)
            return new("required-beasts", "Beasts befriended", CheckStatus.Unknown, $"Needs {b.RequiredBeasts}; beast list not loaded",
                "Open the Master's Bestiary once so the game sends the beast list.");
        int n = c.UnlockedBeasts.Count;
        return n >= b.RequiredBeasts
            ? new("required-beasts", "Beasts befriended", CheckStatus.Pass, $"{n} (needs {b.RequiredBeasts})")
            : new("required-beasts", "Beasts befriended", CheckStatus.Fail, $"{n}, needs {b.RequiredBeasts}",
                $"Befriend {b.RequiredBeasts - n} more: the Beasts tab lists where each missing beast lives.");
    }

    private static ReadinessCheck RequiredBoard(BoardInfo b, IReadOnlySet<int> cleared)
    {
        var need = Boards.ByNumber(b.RequiredBoard);
        return cleared.Contains(need.Number)
            ? new("required-board", "Board cleared first", CheckStatus.Pass, need.Name)
            : new("required-board", "Board cleared first", CheckStatus.Fail, $"{need.Name} not cleared", $"Clear the {need.Name} first.");
    }

    private static ReadinessCheck ItemLevel(BoardInfo b, CharacterSnapshot c)
    {
        if (c.ItemLevel is not int il)
            return new("item-level", "Item level", CheckStatus.Unknown, $"Needs {b.MinItemLevel}; no Beastmaster gear set found",
                "Switch to Beastmaster once, or save a Beastmaster gear set.");
        if (il < b.MinItemLevel)
            return new("item-level", "Item level", CheckStatus.Fail, $"{il}, needs {b.MinItemLevel}",
                "Equip better Beastmaster gear; the Kornago Merchant upgrades it with remnants.");
        if (il < b.TargetItemLevel)
            return new("item-level", "Item level", CheckStatus.Warn, $"{il}; guides aim for {b.TargetItemLevel}",
                "Upgrade gear at the Kornago Merchant with Bright Remnants of Resilience.");
        return new("item-level", "Item level", CheckStatus.Pass, $"{il} (target {b.TargetItemLevel})");
    }

    private static ReadinessCheck Roster(BoardInfo b, CharacterSnapshot c)
    {
        if (c.UnlockedBeasts is null)
            return new("roster", "Roster size", CheckStatus.Unknown, $"Team of {b.TeamSize}; beast list not loaded",
                "Open the Master's Bestiary once so the game sends the beast list.");
        int n = c.UnlockedBeasts.Count;
        if (n == 0)
            return new("roster", "Roster size", CheckStatus.Fail, "No beasts befriended", "Befriend beasts with Capture before entering.");
        return n >= b.TeamSize
            ? new("roster", "Roster size", CheckStatus.Pass, $"{n} beasts for a team of {b.TeamSize}")
            : new("roster", "Roster size", CheckStatus.Warn, $"{n} beasts for a team of {b.TeamSize}",
                "A smaller team can still clear (and earns Beast Squad bonuses), but has fewer spares.");
    }

    private static ReadinessCheck Ranks(BoardInfo b, CharacterSnapshot c)
    {
        var top = c.KnownRanks.Take(CoreTeam).ToList();
        if (top.Count < CoreTeam)
            return new("ranks", "Beast ranks", CheckStatus.Unknown, $"Recommended rank {b.RecommendedRank}; {top.Count} beast ranks seen",
                "Open each beast's page in the Master's Bestiary so the plugin can read its rank.");
        int weakest = top[^1];
        return weakest >= b.RecommendedRank
            ? new("ranks", "Beast ranks", CheckStatus.Pass, $"Best {CoreTeam}: {string.Join("/", top)} (recommended {b.RecommendedRank})")
            : new("ranks", "Beast ranks", CheckStatus.Warn, $"Best {CoreTeam}: {string.Join("/", top)}, recommended {b.RecommendedRank}",
                $"Raise {CoreTeam} beasts to rank {b.RecommendedRank}: earlier boards give up to {CrucibleIds.MaxExpPerBoard} EXP per familiar.");
    }
}
