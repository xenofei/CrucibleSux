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

    /// <summary>Board runs register only the 3 best beasts (Beast Squad III) instead of AutoDuty's full team.</summary>
    public bool RunTeamOfThree { get; set; } = RunOptions.Default.TeamOfThree;

    /// <summary>Board runs may buy from the board shop (forfeits Coin Clutcher).</summary>
    public bool RunShop { get; set; } = RunOptions.Default.Shop;

    public RunGoal RunGoal { get; set; } = RunOptions.Default.Goal;

    /// <summary>In a board, target the enemy the game's intel says dies first (the Pas de Seul mage, the Ogre's wisps).</summary>
    public bool PickTargets { get; set; } = true;

    /// <summary>Record casts, hits, movement and windows while in a board or while a run is going, for bug reports.</summary>
    public bool RecordCombat { get; set; } = true;

    public RunOptions BoardRunOptions() => new(this.RunTeamOfThree, this.RunShop, this.RunGoal);
}

[Serializable]
public sealed class SavedBeast
{
    public int? Rank { get; set; }
    public int? Exp { get; set; }
    public bool RankIsEstimated { get; set; }
    public DateTime SeenUtc { get; set; }
}
