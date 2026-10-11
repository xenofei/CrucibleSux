using System;
using System.Collections.Generic;
using System.Linq;
using CrucibleSux.Core;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using AchievementRow = Lumina.Excel.Sheets.Achievement;

namespace CrucibleSux.Game;

/// <summary>Captures the logged-in character's Crucible state into a <see cref="CharacterSnapshot"/>. Framework thread only.</summary>
public sealed class CharacterReader(IPlayerState player, IUnlockState unlocks, IDataManager data, Configuration config)
{
    /// <summary>The last equipped slot that counts toward item level (the left ring); the soul crystal (13) does not.</summary>
    private const int LastGearSlot = 12;

    /// <summary>The unused waist slot.</summary>
    private const int WaistSlot = 5;

    /// <summary>Main hand, off hand (the main hand counts twice without one) and the ten armour and accessory slots.</summary>
    private const int CountedSlots = 12;

    private static readonly uint[] TrackedQuests =
    [
        .. Boards.All.Select(b => b.UnlockQuest),
        CrucibleIds.Quests.StrangersInTheWood,
        CrucibleIds.Quests.MasterOfTheLhu,
        CrucibleIds.Quests.BondsUnbroken,
        CrucibleIds.Quests.BlazingTrails,
    ];

    private static readonly uint[] TrackedAchievements =
    [
        .. Boards.All.SelectMany(b => new[] { b.ClearAchievement, b.ThirdDegreeAchievement, b.ApexAchievement, b.LegendaryAchievement }),
        .. CrucibleIds.Achievements.JobLevel,
        .. CrucibleIds.Achievements.Befriended,
        .. CrucibleIds.Achievements.Rank25,
        .. CrucibleIds.Achievements.KinMates,
        .. CrucibleIds.Achievements.Rankings,
        CrucibleIds.Achievements.FreeingTheBeast,
        CrucibleIds.Achievements.CrucibleMaster,
        CrucibleIds.Achievements.UnbrokenEndgame,
    ];

    public ulong ContentId => player.IsLoaded ? player.ContentId : 0;

    /// <summary>The character's state, or null while no character is loaded.</summary>
    public unsafe CharacterSnapshot? Capture()
    {
        if (!player.IsLoaded || player.ContentId == 0)
            return null;

        var bst = data.GetExcelSheet<ClassJob>().GetRow(CrucibleIds.BeastmasterJob);
        int jobLevel = Math.Max(0, (int)player.GetClassJobLevel(bst));

        var quests = new Dictionary<uint, QuestState>();
        var qm = QuestManager.Instance();
        foreach (uint id in TrackedQuests)
        {
            ushort shortId = (ushort)(id & 0xFFFF);
            quests[id] = QuestManager.IsQuestComplete(shortId) ? QuestState.Complete
                : qm != null && qm->IsQuestAccepted(shortId) ? QuestState.Accepted
                : QuestState.NotStarted;
        }

        var cleared = Boards.All.Where(b => UIState.IsInstanceContentCompleted(b.InstanceContent)).Select(b => b.Number).ToHashSet();

        HashSet<uint>? achievements = null;
        if (unlocks.IsAchievementListLoaded)
        {
            var sheet = data.GetExcelSheet<AchievementRow>();
            achievements = TrackedAchievements.Where(id => sheet.TryGetRow(id, out var row) && unlocks.IsAchievementComplete(row)).ToHashSet();
        }

        HashSet<uint>? beasts = null;
        var xbm = XBMManager.Instance();
        if (xbm != null && xbm->State == XBMManager.DataState.Received)
        {
            beasts = [];
            for (uint id = 1; id <= CrucibleIds.TotalBeasts; id++)
            {
                if (xbm->IsPetUnlocked(id))
                    beasts.Add(id);
            }
        }

        var inventory = InventoryManager.Instance();
        int Count(uint item) => inventory == null ? 0 : inventory->GetInventoryItemCount(item);

        var progress = config.Characters.TryGetValue(player.ContentId, out var saved)
            ? saved.ToDictionary(kv => kv.Key, kv => new BeastProgress(kv.Key, kv.Value.Rank, kv.Value.Exp, kv.Value.RankIsEstimated, kv.Value.SeenUtc))
            : [];

        return new CharacterSnapshot(
            jobLevel,
            this.ItemLevel(),
            quests,
            cleared,
            achievements,
            beasts,
            progress,
            Count(CrucibleIds.FadedRemnant),
            Count(CrucibleIds.BrightRemnant));
    }

    /// <summary>
    /// The Beastmaster item level: the equipped gear's average while on the job, else the best saved Beastmaster gear
    /// set's (as the game keeps it). Null when neither is available.
    /// </summary>
    private unsafe int? ItemLevel()
    {
        if (player.ClassJob.RowId == CrucibleIds.BeastmasterJob)
        {
            var inventory = InventoryManager.Instance();
            var container = inventory == null ? null : inventory->GetInventoryContainer(InventoryType.EquippedItems);
            if (container != null && container->IsLoaded)
            {
                var items = data.GetExcelSheet<Item>();
                int Level(int slot)
                {
                    var item = container->GetInventorySlot(slot);
                    return item != null && item->ItemId != 0 && items.GetRowOrDefault(item->ItemId) is { } row ? (int)row.LevelItem.RowId : 0;
                }

                int mainHand = Level(0);
                int sum = mainHand + (Level(1) is var off and > 0 ? off : mainHand);
                for (int slot = 2; slot <= LastGearSlot; slot++)
                {
                    if (slot != WaistSlot)
                        sum += Level(slot);
                }
                if (mainHand > 0)
                    return sum / CountedSlots;
            }
        }

        var gearsets = RaptureGearsetModule.Instance();
        if (gearsets == null)
            return null;
        int best = 0;
        foreach (ref var entry in gearsets->Entries)
        {
            if (entry.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) && entry.ClassJob == CrucibleIds.BeastmasterJob)
                best = Math.Max(best, entry.ItemLevel);
        }
        return best > 0 ? best : null;
    }
}
