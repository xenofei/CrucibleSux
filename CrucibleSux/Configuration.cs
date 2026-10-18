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

    /// <summary>The journey flees a fight when HP would fall below this (in 2 s at the rate it is dropping).</summary>
    public int FleeBelow { get; set; } = (int)ThreatPolicy.DefaultFleeBelow;

    /// <summary>Characters (by content id) that said "don't ask again" to the offer to unlock Beastmaster.</summary>
    public List<ulong> UnlockOfferDeclined { get; set; } = [];

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

    /// <summary>In a board, hand aggro between you and the familiar with Snarl and Challenge, by HP and score.</summary>
    public bool ShareAggro { get; set; } = true;

    /// <summary>Your HP% the aggro sharing keeps you above; dying expels you from the board.</summary>
    public int PlayerFloor { get; set; } = (int)AggroAdvisor.DefaultPlayerFloor;

    public int BeastFloor { get; set; } = (int)AggroAdvisor.DefaultBeastFloor;

    /// <summary>In a board battle, drink Beast Potions for you or the familiar when <see cref="HealAdvisor"/> says so.</summary>
    public bool UseItems { get; set; } = true;

    /// <summary>Record casts, hits, movement and windows while in a board or while a run is going, for bug reports.</summary>
    public bool RecordCombat { get; set; } = true;

    /// <summary>Turn the BossMod the player does not use off for a session or the journey without asking first.</summary>
    public bool TurnOffBossMod { get; set; }

    /// <summary>The BossMod the player uses; the other one is turned off for a run. Unset: asked when a session or the journey starts.</summary>
    public BossModChoice MechanicsPlugin { get; set; }

    /// <summary>The BossMod <see cref="BossModTurnedOffInProcess"/> is about; null from 0.2.3, which turned only the original off.</summary>
    public string? BossModTurnedOff { get; set; }

    /// <summary>
    /// The game process in which Crucible SUX turned a BossMod off and has not turned it back on yet, or 0. It
    /// survives a plugin reload; after a game restart Dalamud has already undone the temporary switch, so it is ignored.
    /// </summary>
    public int BossModTurnedOffInProcess { get; set; }

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
