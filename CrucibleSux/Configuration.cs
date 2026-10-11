using System;
using System.Collections.Generic;
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
}

[Serializable]
public sealed class SavedBeast
{
    public int? Rank { get; set; }
    public int? Exp { get; set; }
    public bool RankIsEstimated { get; set; }
    public DateTime SeenUtc { get; set; }
}
