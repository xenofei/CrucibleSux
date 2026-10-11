using System;
using System.Collections.Generic;
using CrucibleSux.Core;
using Dalamud.Configuration;

namespace CrucibleSux;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>
    /// Boards whose automated runs may tick "Participate in rankings". Empty by default: an unattended run never enters
    /// the rankings unless the player opts that board in (docs/PLAN.md §6).
    /// </summary>
    public List<int> RankingsOptIn { get; set; } = [];

    /// <summary>What the Bestiary has shown per character (by content id): rank and EXP per beast.</summary>
    public Dictionary<ulong, Dictionary<uint, SavedBeast>> Characters { get; set; } = [];

    /// <summary>The session queue, kept between game sessions.</summary>
    public List<QueueEntry> Queue { get; set; } = [];

    /// <summary>Tries per queued run before the session gives up.</summary>
    public int MaxAttempts { get; set; } = OrchestratorSettings.Default.MaxAttempts;

    /// <summary>Minutes without any change inside a board before the watchdog stops the run.</summary>
    public int StuckMinutes { get; set; } = (int)OrchestratorSettings.Default.StuckAfter.TotalMinutes;

    /// <summary>The Beastmaster journey's parts (docs/PLAN.md Phase 2b); each stays off until the player turns it on.</summary>
    public bool ProgressQuests { get; set; }

    public bool ProgressBeasts { get; set; }

    public bool ProgressLeveling { get; set; }

    /// <summary>Every run per character (by content id), oldest first, capped at <see cref="HistoryCap"/>.</summary>
    public Dictionary<ulong, List<RunRecord>> Runs { get; set; } = [];

    public const int HistoryCap = 500;
}

[Serializable]
public sealed class SavedBeast
{
    public int? Rank { get; set; }
    public int? Exp { get; set; }
    public bool RankIsEstimated { get; set; }
    public DateTime SeenUtc { get; set; }
}
