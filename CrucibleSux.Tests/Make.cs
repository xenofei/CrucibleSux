using CrucibleSux.Core;

namespace CrucibleSux.Tests;

/// <summary>Builders for test characters and beasts.</summary>
internal static class Make
{
    /// <summary>A level 50 Beastmaster with every board's quest done, 30 beasts at rank 25 and item level 135.</summary>
    public static CharacterSnapshot Veteran() => Character(
        jobLevel: 50,
        itemLevel: 135,
        quests: Boards.All.Select(b => b.UnlockQuest).Append(CrucibleIds.Quests.BlazingTrails).ToDictionary(q => q, _ => QuestState.Complete),
        beasts: 30,
        rank: 25);

    public static CharacterSnapshot Character(
        int jobLevel = 30,
        int? itemLevel = 30,
        Dictionary<uint, QuestState>? quests = null,
        int[]? cleared = null,
        uint[]? achievements = null,
        int? beasts = 10,
        int? rank = 5)
    {
        var unlocked = beasts is int n ? Enumerable.Range(1, n).Select(i => (uint)i).ToHashSet() : null;
        var progress = rank is int r && unlocked is not null
            ? unlocked.ToDictionary(id => id, id => new BeastProgress(id, r, 0, false, DateTime.UnixEpoch))
            : new Dictionary<uint, BeastProgress>();
        return new CharacterSnapshot(
            jobLevel,
            itemLevel,
            quests ?? new Dictionary<uint, QuestState> { [CrucibleIds.Quests.IntoTheCrucible] = QuestState.Complete },
            (cleared ?? []).ToHashSet(),
            achievements?.ToHashSet(),
            unlocked,
            progress,
            0,
            0);
    }

    /// <summary>A beast with a linear grow table: rank r has every attribute at base + r * step.</summary>
    public static BeastInfo Beast(uint id, int str = 10, int @int = 5, int pr = 8, int mr = 8, int con = 10, int step = 4, ushort inflicts = 0, int ranks = 25) =>
        new(id, $"beast{id}", Kin.Beastkin, 0, 3,
            Enumerable.Range(1, ranks).Select(r => new BeastStats(str + r * step, @int + r * step, pr + r * step, mr + r * step, con + r * step)).ToList(),
            [100, 100, 100, 100, 100],
            inflicts);

    public static EnemyInfo Enemy(string name = "enemy", Element element = Element.Blunt, int str = 3, int @int = 3, int pr = 3, int mr = 3, int con = 3, ushort resist = 0) =>
        new(1, name, element, new EnemyRatings(str, @int, pr, mr, con), resist);
}
