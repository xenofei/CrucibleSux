namespace CrucibleSux.Core;

public enum QuestState
{
    NotStarted,
    Accepted,
    Complete,
}

/// <summary>
/// Everything the readiness and progress rules read about one character, captured on the framework thread.
/// </summary>
/// <param name="JobLevel">Beastmaster level, 0 when the job is not unlocked.</param>
/// <param name="ItemLevel">Average item level of the Beastmaster gear (equipped when on the job, else its gear set); null when unknown.</param>
/// <param name="ClearedBoards">Board numbers whose instance content the game reports completed.</param>
/// <param name="CompletedAchievements">Completed Crucible achievements, or null while the achievement list is not loaded.</param>
/// <param name="UnlockedBeasts">Befriended beasts (XBMPet rows), or null while the beast list is not loaded.</param>
/// <param name="Beasts">Rank and EXP the plugin has seen in the Bestiary, by beast.</param>
public sealed record CharacterSnapshot(
    int JobLevel,
    int? ItemLevel,
    IReadOnlyDictionary<uint, QuestState> Quests,
    IReadOnlySet<int> ClearedBoards,
    IReadOnlySet<uint>? CompletedAchievements,
    IReadOnlySet<uint>? UnlockedBeasts,
    IReadOnlyDictionary<uint, BeastProgress> Beasts,
    int FadedRemnants,
    int BrightRemnants)
{
    public QuestState Quest(uint id) => this.Quests.TryGetValue(id, out var state) ? state : QuestState.NotStarted;

    /// <summary>The ranks of the befriended beasts the plugin has seen, best first.</summary>
    public IEnumerable<int> KnownRanks =>
        this.Beasts.Values
            .Where(b => b.Rank is not null && (this.UnlockedBeasts is null || this.UnlockedBeasts.Contains(b.Id)))
            .Select(b => b.Rank!.Value)
            .OrderDescending();
}
