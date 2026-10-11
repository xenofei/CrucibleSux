namespace CrucibleSux.Core;

/// <summary>Which parts of the Beastmaster journey the player has asked the plugin to do; each is off until they opt in.</summary>
public sealed record ProgressionOptions(bool Quests, bool Beasts, bool Leveling)
{
    public bool Any => this.Quests || this.Beasts || this.Leveling;
}

/// <summary>A job quest's state and, while it is accepted, the sequence the game reports.</summary>
public readonly record struct QuestProgress(QuestState State, byte Sequence);

/// <summary>Everything the planner reads about the character, captured on the framework thread.</summary>
/// <param name="JobLevel">Beastmaster level, 0 when the job is not unlocked.</param>
/// <param name="HighestCombatLevel">The highest Disciple of War or Magic level on any other job (for the unlock quest).</param>
/// <param name="Owned">Beasts (XBMPet rows) befriended.</param>
/// <param name="Inventory">Counts of the items the planner cares about: gourds and remnants.</param>
public sealed record ProgressionState(
    int JobLevel,
    int HighestCombatLevel,
    IReadOnlyDictionary<uint, QuestProgress> Quests,
    IReadOnlySet<uint> Owned,
    IReadOnlySet<int> ClearedBoards,
    IReadOnlyDictionary<uint, int> Inventory)
{
    public QuestProgress Quest(uint id) => this.Quests.TryGetValue(id, out var q) ? q : default;

    public bool IsComplete(uint quest) => this.Quest(quest).State == QuestState.Complete;

    public int Count(uint item) => this.Inventory.TryGetValue(item, out int n) ? n : 0;
}

public enum ActivityKind
{
    /// <summary>Carry out <see cref="Activity.Step"/> of <see cref="Activity.Quest"/> (or accept it when the step is null).</summary>
    Quest,

    /// <summary>Gain Beastmaster levels until <see cref="Activity.Level"/>.</summary>
    Level,

    /// <summary>Capture one of <see cref="Activity.Beasts"/> in the open world.</summary>
    Capture,

    /// <summary>Buy gourd <see cref="Activity.Item"/> from the Kornago merchant.</summary>
    BuyGourd,

    /// <summary>Use gourd <see cref="Activity.Item"/> from the inventory.</summary>
    UseGourd,

    /// <summary>Clear <see cref="Activity.Board"/> (for a quest, or for the remnants a gourd costs).</summary>
    RunBoard,

    /// <summary>Everything the player opted into is finished.</summary>
    Done,

    /// <summary>Nothing the player opted into can move forward; <see cref="Activity.Why"/> says what is missing.</summary>
    Blocked,
}

/// <summary>The one thing the plugin should do next, and why.</summary>
public sealed record Activity(
    ActivityKind Kind,
    string Why,
    uint Quest = 0,
    QuestStep? Step = null,
    int Level = 0,
    IReadOnlyList<uint>? Beasts = null,
    uint Item = 0,
    int Board = 0);

/// <summary>
/// Chooses the next activity on the Beastmaster journey (docs/PLAN.md, Phase 2b). Quests come first when opted into, and
/// whatever a quest step needs (levels, beasts, a board clear) is planned in front of it; then the remaining beasts; then
/// levels to 50. A need the player has not opted into blocks instead of being done behind their back.
/// </summary>
public static class ProgressionPlanner
{
    public const int MaxLevel = 50;

    public static Activity Next(ProgressionState s, ProgressionOptions o)
    {
        if (!o.Any)
            return new(ActivityKind.Blocked, "Nothing is turned on.");

        // A blocked quest (say, one waiting on a board the player clears by hand) must not stop the other options.
        var quest = o.Quests ? NextQuest(s, o) : null;
        if (quest is { Kind: not ActivityKind.Blocked })
            return quest;

        bool wantBeasts = o.Beasts && s.Owned.Count < CrucibleIds.TotalBeasts;
        bool wantLevels = o.Leveling && s.JobLevel < MaxLevel;
        if (s.JobLevel == 0 && (wantBeasts || wantLevels))
            return quest ?? new(ActivityKind.Blocked, "Beastmaster is not unlocked; turn on the job quests to unlock it.");

        var beasts = wantBeasts ? Collect(s, o, CrucibleIds.TotalBeasts, need: null) : null;
        if (beasts is { Kind: not ActivityKind.Blocked })
            return beasts;

        if (wantLevels)
            return new(ActivityKind.Level, $"Level {MaxLevel} is the cap.", Level: MaxLevel);

        return quest ?? beasts ?? new(ActivityKind.Done, "Everything turned on is finished.");
    }

    /// <summary>The next quest activity, or null when every job quest is complete.</summary>
    private static Activity? NextQuest(ProgressionState s, ProgressionOptions o)
    {
        foreach (var q in QuestScripts.All)
        {
            var p = s.Quest(q.Id);
            if (p.State == QuestState.Complete)
                continue;

            if (p.State == QuestState.Accepted)
            {
                var step = q.At(p.Sequence).FirstOrDefault();
                if (step is null)
                    return new(ActivityKind.Blocked, $"{q.Name} is at a step the plugin does not know (sequence {p.Sequence}).", q.Id);
                return Satisfy(s, o, step.Need, q.Name) ?? new(ActivityKind.Quest, $"{q.Name}, step {p.Sequence}.", q.Id, step);
            }

            if (q.AnyJob)
            {
                if (q.PreviousQuest != 0 && !s.IsComplete(q.PreviousQuest))
                    return new(ActivityKind.Blocked, $"{q.Name} needs the main scenario through \"The Ultimate Weapon\" first.", q.Id);
                if (s.HighestCombatLevel < q.Level)
                    return new(ActivityKind.Blocked, $"{q.Name} needs another combat job at level {q.Level}.", q.Id);
                return new(ActivityKind.Quest, $"Accept {q.Name}.", q.Id);
            }

            var need = (q.Accept ?? new StepNeed()) with { Level = Math.Max(q.Level, q.Accept?.Level ?? 0) };
            return Satisfy(s, o, need, q.Name) ?? new(ActivityKind.Quest, $"Accept {q.Name}.", q.Id);
        }
        return null;
    }

    /// <summary>What must happen before a step whose needs are unmet, or null when they are met.</summary>
    private static Activity? Satisfy(ProgressionState s, ProgressionOptions o, StepNeed? need, string forWhat)
    {
        if (need is null)
            return null;

        if (s.JobLevel < need.Level)
        {
            return o.Leveling
                ? new(ActivityKind.Level, $"{forWhat} needs level {need.Level}.", Level: need.Level)
                : new(ActivityKind.Blocked, $"{forWhat} needs Beastmaster level {need.Level}. Turn on levelling, or level by hand.");
        }

        // A beast that must be summoned must be owned first.
        var missing = (need.Owned ?? []).Append(need.Summoned).Where(b => b != 0 && !s.Owned.Contains(b)).ToList();
        foreach (var kin in need.OwnedKin ?? [])
        {
            if (!BeastSources.All.Any(b => b.Kin == kin && s.Owned.Contains(b.Beast)))
                missing.AddRange(BeastSources.All.Where(b => b.Kin == kin && b.CanCapture).Select(b => b.Beast));
        }
        missing = missing.Distinct().ToList();
        if (missing.Count > 0)
        {
            // A quest needs these particular beasts; getting them is part of the quest, so it does not need the beasts option.
            if (missing.Select(BeastSources.ById).FirstOrDefault(b => b.Gourd != 0 && s.Count(b.Gourd) > 0) is { } held)
                return new(ActivityKind.UseGourd, $"{forWhat} needs the {held.Name}.", Item: held.Gourd, Beasts: [held.Beast]);
            var reachable = missing.Where(b => BeastSources.ById(b).CanCapture && BeastSources.ById(b).MinLevel <= s.JobLevel).ToList();
            if (reachable.Count > 0)
                return new(ActivityKind.Capture, $"{forWhat} needs {Names(missing)}.", Beasts: reachable);
            var capturable = missing.Where(b => BeastSources.ById(b).CanCapture).ToList();
            if (capturable.Count == 0)
                return new(ActivityKind.Blocked, $"{forWhat} needs {Names(missing)}, and its gourd is not in your inventory.");
            int level = capturable.Select(b => BeastSources.ById(b).MinLevel).Min();
            return o.Leveling
                ? new(ActivityKind.Level, $"{forWhat} needs {Names(missing)}, which can be captured from level {level}.", Level: level)
                : new(ActivityKind.Blocked, $"{forWhat} needs {Names(missing)} (level {level}). Turn on levelling, or level by hand.");
        }

        if (s.Owned.Count < need.Beasts)
        {
            // The quest's beast count: collected even without the beasts option, since the quest cannot go on otherwise.
            return Collect(s, o, need.Beasts, forWhat);
        }

        if (need.Board != 0 && !s.ClearedBoards.Contains(need.Board))
            return new(ActivityKind.RunBoard, $"{forWhat} needs the {Boards.ByNumber(need.Board).Name} cleared.", Board: need.Board);

        return null;
    }

    /// <summary>The next step towards owning <paramref name="target"/> beasts.</summary>
    private static Activity Collect(ProgressionState s, ProgressionOptions o, int target, string? need)
    {
        string why = need is null ? $"{s.Owned.Count} of {CrucibleIds.TotalBeasts} beasts owned." : $"{need} needs {target} beasts; {s.Owned.Count} owned.";
        var missing = BeastSources.All.Where(b => !s.Owned.Contains(b.Beast)).ToList();

        if (missing.FirstOrDefault(b => b.Gourd != 0 && s.Count(b.Gourd) > 0) is { } held)
            return new(ActivityKind.UseGourd, why, Item: held.Gourd, Beasts: [held.Beast]);

        var capturable = missing.Where(b => b.CanCapture && b.MinLevel <= s.JobLevel).Select(b => b.Beast).ToList();
        if (capturable.Count > 0)
            return new(ActivityKind.Capture, why, Beasts: capturable);

        // Spending remnants (and farming them) is the beasts option's call, never a side effect of the quests alone.
        var shop = missing.Where(b => b.IsSold && s.IsComplete(b.GourdQuest)).OrderBy(b => b.Price).ToList();
        if (o.Beasts && shop.FirstOrDefault(b => Affordable(s, b)) is { } buy)
            return new(ActivityKind.BuyGourd, why, Item: buy.Gourd, Beasts: [buy.Beast]);
        if (o.Beasts && shop.Count > 0 && FarmBoard(s) is { } board)
            return new(ActivityKind.RunBoard, $"{why} The {Names([shop[0].Beast])} gourd costs {shop[0].Price} {CurrencyName(shop[0].Currency)}; farming remnants.", Board: board);

        var later = missing.Where(b => b.CanCapture).OrderBy(b => b.MinLevel).FirstOrDefault();
        if (later is not null && o.Leveling)
            return new(ActivityKind.Level, $"{why} The next beast, the {Names([later.Beast])}, needs level {later.MinLevel}.", Level: later.MinLevel);

        string blocker = shop.Count > 0 && !o.Beasts
            ? $"buy the {Names([shop[0].Beast])} gourd by hand, or turn on \"Collect every beast\" so the plugin buys it"
            : later is not null
            ? $"the {Names([later.Beast])} needs level {later.MinLevel}"
            : missing.Any(b => b.IsSold && !s.IsComplete(b.GourdQuest))
                ? "the remaining gourds unlock with later job quests"
                : "no source is left that the plugin can reach";
        return new(ActivityKind.Blocked, $"{why} Next: {blocker}.");
    }

    /// <summary>Whether the gourd can be paid for now; Bright Remnants may also be bought with two Faded each.</summary>
    private static bool Affordable(ProgressionState s, BeastSource b)
    {
        int faded = s.Count(CrucibleIds.FadedRemnant);
        int bright = s.Count(CrucibleIds.BrightRemnant);
        return b.Currency == CrucibleIds.FadedRemnant
            ? faded >= b.Price
            : bright >= b.Price || (s.IsComplete(CrucibleIds.Quests.ABeastmastersPath) && bright + (faded / 2) >= b.Price);
    }

    /// <summary>The highest cleared board, the fastest remnants the character can safely farm; null when none is cleared.</summary>
    private static int? FarmBoard(ProgressionState s) => s.ClearedBoards.Count == 0 ? null : s.ClearedBoards.Max();

    private static string Names(IEnumerable<uint> beasts) => string.Join(", ", beasts.Distinct().Select(b => BeastSources.ById(b).Name));

    private static string CurrencyName(uint item) => item == CrucibleIds.BrightRemnant ? "Bright Remnants" : "Faded Remnants";
}
