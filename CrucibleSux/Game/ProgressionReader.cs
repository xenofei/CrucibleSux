using System;
using System.Collections.Generic;
using System.Linq;
using CrucibleSux.Core;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace CrucibleSux.Game;

/// <summary>Captures what the progression planner reads. Framework thread only.</summary>
public sealed class ProgressionReader(IPlayerState player, IDataManager data)
{
    /// <summary>Blue Mage (limited) and Beastmaster itself do not count toward the unlock quest's level.</summary>
    private static readonly uint[] NotCounted = [36, CrucibleIds.BeastmasterJob];

    private static readonly uint[] Items =
    [
        .. BeastSources.All.Select(b => b.Gourd).Where(g => g != 0).Distinct(),
        CrucibleIds.FadedRemnant,
        CrucibleIds.BrightRemnant,
    ];

    private static readonly uint[] Quests = [.. QuestScripts.All.Select(q => q.Id), QuestScripts.UnlockPrerequisite];

    /// <summary>
    /// The state, or null while no character is loaded. <paramref name="beastsUnknown"/> is true when Beastmaster is unlocked
    /// but the game has not sent the beast list yet (it does when the Master's Bestiary is opened).
    /// </summary>
    public unsafe ProgressionState? Capture(out bool beastsUnknown)
    {
        beastsUnknown = false;
        if (!player.IsLoaded || player.ContentId == 0)
            return null;

        var jobs = data.GetExcelSheet<ClassJob>();
        int jobLevel = Math.Max(0, (int)player.GetClassJobLevel(jobs.GetRow(CrucibleIds.BeastmasterJob)));
        int highest = jobs.Where(j => j.Role > 0 && !NotCounted.Contains(j.RowId)).Select(j => (int)player.GetClassJobLevel(j)).DefaultIfEmpty(0).Max();

        var quests = new Dictionary<uint, QuestProgress>();
        var qm = QuestManager.Instance();
        foreach (uint id in Quests)
        {
            ushort shortId = (ushort)(id & 0xFFFF);
            quests[id] = QuestManager.IsQuestComplete(shortId) ? new(QuestState.Complete, 0)
                : qm != null && qm->IsQuestAccepted(shortId) ? new(QuestState.Accepted, QuestManager.GetQuestSequence(shortId))
                : new(QuestState.NotStarted, 0);
        }

        var owned = new HashSet<uint>();
        var xbm = XBMManager.Instance();
        if (xbm != null && xbm->State == XBMManager.DataState.Received)
        {
            for (uint id = 1; id <= CrucibleIds.TotalBeasts; id++)
            {
                if (xbm->IsPetUnlocked(id))
                    owned.Add(id);
            }
        }
        else
        {
            beastsUnknown = jobLevel > 0;
        }

        var inventory = InventoryManager.Instance();
        var counts = Items.ToDictionary(i => i, i => inventory == null ? 0 : inventory->GetInventoryItemCount(i));
        var cleared = Boards.All.Where(b => UIState.IsInstanceContentCompleted(b.InstanceContent)).Select(b => b.Number).ToHashSet();

        return new ProgressionState(jobLevel, highest, quests, owned, cleared, counts);
    }

    /// <summary>EXP into the current Beastmaster level.</summary>
    public int Experience() => player.IsLoaded ? player.GetClassJobExperience(data.GetExcelSheet<ClassJob>().GetRow(CrucibleIds.BeastmasterJob)) : 0;

    /// <summary>A quest's name as this client shows it.</summary>
    public string QuestName(uint quest) => data.GetExcelSheet<Quest>().GetRowOrDefault(quest)?.Name.ExtractText() ?? string.Empty;

    public string ItemName(uint item) => data.GetExcelSheet<Item>().GetRowOrDefault(item)?.Name.ExtractText() ?? string.Empty;
}
