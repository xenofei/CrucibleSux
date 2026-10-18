using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class DependencyTests
{
    private static InstalledPlugin P(string name, bool loaded = true, string? repo = null, string version = "99.0") => new(name, new Version(version), loaded, repo);

    private static readonly InstalledPlugin[] FullSet =
    [
        P("vnavmesh"), P("RotationSolver"), P("BossModReborn"), P("AutoDuty", repo: "https://github.com/erdelf/AutoDuty"),
        P("Lifestream"), P("TextAdvance"),
    ];

    private static RoleStatus Role(IReadOnlyList<RoleStatus> s, DependencyRole role) => s.Single(x => x.Role == role);

    [Fact]
    public void The_recommended_set_passes_every_role()
    {
        var s = DependencyCatalog.Evaluate(FullSet);

        Assert.All(s, r => Assert.Equal(CheckStatus.Pass, r.Status));
        Assert.Equal("Uses Rotation Solver Reborn", Role(s, DependencyRole.Rotation).Detail);
    }

    [Fact]
    public void Unchosen_counts_as_BossMod_Reborn_so_the_original_alone_does_not_cover_mechanics()
    {
        var s = DependencyCatalog.Evaluate(FullSet.Select(p => p.InternalName == "BossModReborn" ? p with { InternalName = "BossMod" } : p));

        var mechanics = Role(s, DependencyRole.Mechanics);
        Assert.Equal(CheckStatus.Fail, mechanics.Status);
        Assert.Contains("only the one you use", mechanics.Detail);
    }

    [Fact]
    public void Choosing_the_original_BossMod_makes_Reborn_the_one_turned_off()
    {
        var installed = FullSet.Append(P("BossMod")).ToList();

        var before = DependencyCatalog.Evaluate(installed, BossModChoice.Original);
        Assert.Equal(CheckStatus.Fail, Role(before, DependencyRole.Mechanics).Status);
        Assert.Equal("BossModReborn", Assert.Single(DependencyCatalog.LoadedConflicts(before)).InternalName);

        var after = DependencyCatalog.Evaluate(DependencyCatalog.WithConflictsOff(installed, BossModChoice.Original), BossModChoice.Original);
        Assert.All(after, r => Assert.Equal(CheckStatus.Pass, r.Status));
        Assert.Equal("BossMod (original)", Role(after, DependencyRole.Mechanics).Detail);
        Assert.Equal("Uses Rotation Solver Reborn", Role(after, DependencyRole.Rotation).Detail);
    }

    [Fact]
    public void A_plugin_older_than_the_tested_release_warns_and_the_tested_one_passes()
    {
        var older = DependencyCatalog.Evaluate(FullSet.Select(p => p.InternalName == "AutoDuty" ? p with { Version = new Version(0, 0, 0, 388) } : p));
        var tested = DependencyCatalog.Evaluate(FullSet.Select(p => p.InternalName == "AutoDuty" ? p with { Version = new Version(0, 0, 0, 389) } : p));

        Assert.Equal(CheckStatus.Warn, Role(older, DependencyRole.BoardDriver).Status);
        Assert.Contains("older than the tested 0.0.0.389", Role(older, DependencyRole.BoardDriver).Detail);
        Assert.Equal(CheckStatus.Pass, Role(tested, DependencyRole.BoardDriver).Status);
    }

    [Fact]
    public void The_original_BossMod_installed_but_disabled_is_fine()
    {
        var s = DependencyCatalog.Evaluate(FullSet.Append(P("BossMod", loaded: false)));

        var mechanics = Role(s, DependencyRole.Mechanics);
        Assert.Equal(CheckStatus.Pass, mechanics.Status);
        Assert.Equal("BossMod Reborn", mechanics.Detail);
    }

    [Fact]
    public void A_missing_required_role_fails_and_a_missing_optional_one_warns()
    {
        var s = DependencyCatalog.Evaluate(FullSet.Where(p => p.InternalName is not ("vnavmesh" or "Lifestream")));

        Assert.Equal(CheckStatus.Fail, Role(s, DependencyRole.Navigation).Status);
        Assert.Equal(CheckStatus.Warn, Role(s, DependencyRole.Travel).Status);
    }

    [Fact]
    public void A_disabled_plugin_does_not_count()
    {
        var s = DependencyCatalog.Evaluate(FullSet.Select(p => p.InternalName == "vnavmesh" ? p with { IsLoaded = false } : p));

        Assert.Equal(CheckStatus.Fail, Role(s, DependencyRole.Navigation).Status);
        Assert.Equal(CheckStatus.Unknown, Role(s, DependencyRole.Navigation).Candidates.Single().Status);
    }

    [Fact]
    public void AutoDuty_from_another_repo_warns()
    {
        var s = DependencyCatalog.Evaluate(FullSet.Select(p => p.InternalName == "AutoDuty" ? p with { RepoUrl = "https://github.com/ffxivcode/AutoDuty" } : p));

        Assert.Equal(CheckStatus.Warn, Role(s, DependencyRole.BoardDriver).Status);
    }

    [Fact]
    public void The_original_BossMod_loaded_beside_Reborn_fails()
    {
        var s = DependencyCatalog.Evaluate(FullSet.Append(P("BossMod")));

        var mechanics = Role(s, DependencyRole.Mechanics);
        Assert.Equal(CheckStatus.Fail, mechanics.Status);
        Assert.Contains("only the one you use", mechanics.Detail);
    }

    [Fact]
    public void Turning_the_original_BossMod_off_leaves_Reborn_covering_mechanics()
    {
        var installed = FullSet.Append(P("BossMod")).ToList();

        Assert.Equal("BossMod", Assert.Single(DependencyCatalog.LoadedConflicts(DependencyCatalog.Evaluate(installed))).InternalName);
        var after = DependencyCatalog.Evaluate(DependencyCatalog.WithConflictsOff(installed));
        Assert.All(after, r => Assert.Equal(CheckStatus.Pass, r.Status));
        Assert.Empty(DependencyCatalog.LoadedConflicts(after));
    }

    [Fact]
    public void Turning_conflicts_off_leaves_other_plugins_alone() =>
        Assert.Equal(FullSet, DependencyCatalog.WithConflictsOff(FullSet));

    [Fact]
    public void BossMod_Reborn_alone_covers_rotation_and_mechanics()
    {
        var s = DependencyCatalog.Evaluate(FullSet.Where(p => p.InternalName != "RotationSolver"));

        Assert.Equal("Uses BossMod Reborn", Role(s, DependencyRole.Rotation).Detail);
        Assert.Equal(CheckStatus.Pass, Role(s, DependencyRole.Mechanics).Status);
    }
}
