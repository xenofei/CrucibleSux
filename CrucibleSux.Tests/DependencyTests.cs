using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class DependencyTests
{
    private static InstalledPlugin P(string name, bool loaded = true, string? repo = null) => new(name, new Version(1, 0), loaded, repo);

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
    public void Both_BossMods_loaded_warns()
    {
        var s = DependencyCatalog.Evaluate(FullSet.Append(P("BossMod")));

        Assert.Equal(CheckStatus.Warn, Role(s, DependencyRole.Mechanics).Status);
    }

    [Fact]
    public void BossMod_Reborn_alone_covers_rotation_and_mechanics()
    {
        var s = DependencyCatalog.Evaluate(FullSet.Where(p => p.InternalName != "RotationSolver"));

        Assert.Equal("Uses BossMod Reborn", Role(s, DependencyRole.Rotation).Detail);
        Assert.Equal(CheckStatus.Pass, Role(s, DependencyRole.Mechanics).Status);
    }
}
