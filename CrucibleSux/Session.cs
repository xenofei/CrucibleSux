using System;
using System.Collections.Generic;
using CrucibleSux.Core;
using CrucibleSux.Game;
using Dalamud.Plugin.Services;

namespace CrucibleSux;

/// <summary>
/// What the windows draw: the latest character snapshot and companion-plugin check, refreshed on the framework thread.
/// The derived views (progress, sitting plan) are computed once per snapshot.
/// </summary>
public sealed class Session(CharacterReader reader, PluginInventory plugins, Configuration config, IPluginLog log)
{
    private static readonly TimeSpan SnapshotEvery = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PluginsEvery = TimeSpan.FromSeconds(5);

    private DateTime nextSnapshot;
    private DateTime nextPlugins;
    private bool warned;

    public CharacterSnapshot? Snapshot { get; private set; }
    public ProgressSummary? Progress { get; private set; }
    public SittingPlan? Plan { get; private set; }
    public IReadOnlyList<RoleStatus> Dependencies { get; private set; } = [];

    /// <summary>Loaded plugins that must be off for a run (the BossMod the player does not use).</summary>
    public IReadOnlyList<DependencyInfo> Conflicts { get; private set; } = [];

    /// <summary><see cref="Dependencies"/> as they will be once <see cref="Conflicts"/> are turned off.</summary>
    public IReadOnlyList<RoleStatus> DependenciesWithConflictsOff { get; private set; } = [];

    /// <summary>Bumped whenever <see cref="Snapshot"/> changes, so views can cache what they derive from it.</summary>
    public int Generation { get; private set; }

    public void Update(bool force = false)
    {
        var now = DateTime.UtcNow;
        if (force || now >= this.nextPlugins)
        {
            this.nextPlugins = now + PluginsEvery;
            var installed = plugins.Read();
            this.Dependencies = DependencyCatalog.Evaluate(installed, config.MechanicsPlugin);
            this.Conflicts = DependencyCatalog.LoadedConflicts(this.Dependencies);
            this.DependenciesWithConflictsOff = this.Conflicts.Count == 0
                ? this.Dependencies
                : DependencyCatalog.Evaluate(DependencyCatalog.WithConflictsOff(installed, config.MechanicsPlugin), config.MechanicsPlugin);
        }

        if (!force && now < this.nextSnapshot)
            return;
        this.nextSnapshot = now + SnapshotEvery;
        try
        {
            var snapshot = reader.Capture();
            if (snapshot is null)
            {
                if (this.Snapshot is not null)
                    this.Set(null);
                return;
            }
            if (this.Snapshot is null || !Same(this.Snapshot, snapshot))
                this.Set(snapshot);
        }
        catch (Exception ex)
        {
            if (!this.warned)
            {
                this.warned = true;
                log.Warning(ex, "The character could not be read; the dashboard shows the last read");
            }
        }
    }

    private void Set(CharacterSnapshot? snapshot)
    {
        this.Snapshot = snapshot;
        this.Progress = snapshot is null ? null : ProgressCalculator.Summarize(snapshot);
        this.Plan = snapshot is null ? null : ReadinessEvaluator.PlanSitting(snapshot);
        this.Generation++;
    }

    /// <summary>Snapshots hold collections, so record equality would compare references; this compares the contents.</summary>
    private static bool Same(CharacterSnapshot a, CharacterSnapshot b) =>
        a.JobLevel == b.JobLevel && a.ItemLevel == b.ItemLevel && a.FadedRemnants == b.FadedRemnants && a.BrightRemnants == b.BrightRemnants
        && SameMap(a.Quests, b.Quests) && a.ClearedBoards.SetEquals(b.ClearedBoards)
        && SameSet(a.CompletedAchievements, b.CompletedAchievements) && SameSet(a.UnlockedBeasts, b.UnlockedBeasts)
        && SameMap(a.Beasts, b.Beasts);

    private static bool SameSet(IReadOnlySet<uint>? a, IReadOnlySet<uint>? b) => a is null ? b is null : b is not null && a.SetEquals(b);

    private static bool SameMap<TValue>(IReadOnlyDictionary<uint, TValue> a, IReadOnlyDictionary<uint, TValue> b)
    {
        if (a.Count != b.Count)
            return false;
        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other) || !EqualityComparer<TValue>.Default.Equals(value, other))
                return false;
        }
        return true;
    }
}
