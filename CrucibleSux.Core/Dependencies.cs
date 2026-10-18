namespace CrucibleSux.Core;

public enum DependencyRole
{
    /// <summary>Walking in the overworld: to Lauda, the Kornago Merchant, capture spots.</summary>
    Navigation,

    /// <summary>The Beastmaster rotation. Exactly one engine should drive it.</summary>
    Rotation,

    /// <summary>Dodging boss mechanics in the battle arenas.</summary>
    Mechanics,

    /// <summary>Lauda's menus, team setup and moving across the board (AutoDuty's Crucible mode).</summary>
    BoardDriver,

    /// <summary>Teleporting to Central Shroud and capture zones.</summary>
    Travel,

    /// <summary>Advancing cutscene and NPC text.</summary>
    Dialogs,
}

/// <summary>Which BossMod the player uses for mechanics; the other one is turned off for a run.</summary>
public enum BossModChoice
{
    /// <summary>Not asked yet: counts as BossMod Reborn.</summary>
    Unset,
    Reborn,
    Original,
}

/// <param name="RepoHint">A substring of the plugin's repo URL that marks the build with the features needed (e.g. a fork).</param>
/// <param name="Conflict">Why this plugin must be disabled for the role: loaded, it fails the role even beside a good candidate.</param>
/// <param name="TestedVersion">The release Crucible SUX was last checked against; an older one warns.</param>
public sealed record DependencyInfo(
    string InternalName, string DisplayName, DependencyRole Role, string Purpose, string RepoUrl, string? RepoHint = null, string? Conflict = null,
    Version? TestedVersion = null);

/// <summary>A plugin as Dalamud reports it.</summary>
public sealed record InstalledPlugin(string InternalName, Version Version, bool IsLoaded, string? RepoUrl);

public sealed record DependencyStatus(DependencyInfo Info, InstalledPlugin? Installed, CheckStatus Status, string Detail);

public sealed record RoleStatus(DependencyRole Role, bool Required, CheckStatus Status, string Detail, IReadOnlyList<DependencyStatus> Candidates);

public static class DependencyCatalog
{
    public static readonly IReadOnlyList<DependencyInfo> All =
    [
        new("vnavmesh", "vnavmesh", DependencyRole.Navigation, "Pathing in the overworld", "https://github.com/awgil/ffxiv_navmesh", TestedVersion: new(1, 2, 3, 14)),
        new("RotationSolver", "Rotation Solver Reborn", DependencyRole.Rotation, "Beastmaster rotation (shipped 2026-09-13)", "https://github.com/FFXIV-CombatReborn/RotationSolverReborn",
            TestedVersion: new(7, 5, 7, 3)),
        new("BossModReborn", "BossMod Reborn", DependencyRole.Rotation, "Beastmaster rotation module (work in progress)", "https://github.com/FFXIV-CombatReborn/BossmodReborn",
            TestedVersion: new(7, 5, 7, 1)),
        new("WrathCombo", "Wrath Combo", DependencyRole.Rotation, "Rotation (no Beastmaster support seen yet)", "https://github.com/PunishXIV/WrathCombo"),
        new("BossModReborn", "BossMod Reborn", DependencyRole.Mechanics, "AI movement and dodging", "https://github.com/FFXIV-CombatReborn/BossmodReborn",
            TestedVersion: new(7, 5, 7, 1)),
        new("BossMod", "BossMod (original)", DependencyRole.Mechanics, "AI movement and dodging", "https://github.com/awgil/ffxiv_bossmod", TestedVersion: new(7, 5, 7, 0)),
        new("AutoDuty", "AutoDuty (erdelf)", DependencyRole.BoardDriver, "Lauda's menus, team setup, board movement", "https://github.com/erdelf/AutoDuty", "erdelf",
            TestedVersion: new(0, 0, 0, 389)),
        new("Lifestream", "Lifestream", DependencyRole.Travel, "Teleports", "https://github.com/NightmareXIV/Lifestream", TestedVersion: new(2, 6, 0, 7)),
        new("TextAdvance", "TextAdvance", DependencyRole.Dialogs, "Advancing NPC text", "https://github.com/NightmareXIV/TextAdvance", TestedVersion: new(3, 3, 0, 1)),
    ];

    private static readonly DependencyRole[] RequiredRoles = [DependencyRole.Navigation, DependencyRole.Rotation, DependencyRole.Mechanics, DependencyRole.BoardDriver];

    private const string UnusedBossMod = "Both BossMods answer AutoDuty under the same names, so only the one you use (Settings) may be loaded during a run";

    /// <summary>
    /// <see cref="All"/> for <paramref name="choice"/>: the BossMod the player does not use (<see cref="BossModSwitch.OffFor"/>)
    /// is a <see cref="DependencyInfo.Conflict"/> for mechanics.
    /// </summary>
    private static IReadOnlyList<DependencyInfo> For(BossModChoice choice)
    {
        string unused = BossModSwitch.OffFor(choice);
        return [.. All.Select(d => d.Role == DependencyRole.Mechanics && d.InternalName == unused ? d with { Conflict = UnusedBossMod } : d)];
    }

    public static IReadOnlyList<RoleStatus> Evaluate(IEnumerable<InstalledPlugin> installed, BossModChoice choice = BossModChoice.Unset)
    {
        var catalog = For(choice);
        var byName = installed.GroupBy(p => p.InternalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.IsLoaded).First(), StringComparer.OrdinalIgnoreCase);

        var result = new List<RoleStatus>();
        foreach (var role in Enum.GetValues<DependencyRole>())
        {
            bool required = RequiredRoles.Contains(role);
            var candidates = catalog.Where(d => d.Role == role)
                .Select(d => Status(d, byName.GetValueOrDefault(d.InternalName)))
                .ToList();
            var loaded = candidates.Where(c => c.Info.Conflict is null && c.Status is CheckStatus.Pass or CheckStatus.Warn).ToList();
            var conflicts = candidates.Where(c => c.Info.Conflict is not null && c.Status == CheckStatus.Fail).ToList();

            CheckStatus status;
            string detail;
            if (conflicts.Count > 0)
            {
                status = CheckStatus.Fail;
                detail = string.Join("; ", conflicts.Select(c => c.Detail));
            }
            else if (loaded.Count == 0)
            {
                status = required ? CheckStatus.Fail : CheckStatus.Warn;
                detail = required ? "None loaded: install and enable one of these." : "Optional, none loaded.";
            }
            else if (loaded.Any(c => c.Status == CheckStatus.Warn))
            {
                status = CheckStatus.Warn;
                detail = string.Join("; ", loaded.Where(c => c.Status == CheckStatus.Warn).Select(c => c.Detail));
            }
            else if (role == DependencyRole.Rotation)
            {
                // Catalog order is the preference order; the others' auto-rotation must stay off in the Crucible.
                status = CheckStatus.Pass;
                detail = $"Uses {loaded[0].Info.DisplayName}";
            }
            else
            {
                status = CheckStatus.Pass;
                detail = string.Join(", ", loaded.Select(c => c.Info.DisplayName));
            }
            result.Add(new RoleStatus(role, required, status, detail, candidates));
        }
        return result;
    }

    /// <summary>The loaded plugins that fail their role because they must be off (<see cref="DependencyInfo.Conflict"/>).</summary>
    public static IReadOnlyList<DependencyInfo> LoadedConflicts(IEnumerable<RoleStatus> roles) =>
        [.. roles.SelectMany(r => r.Candidates).Where(c => c.Info.Conflict is not null && c.Status == CheckStatus.Fail).Select(c => c.Info).Distinct()];

    /// <summary><paramref name="installed"/> as it will be once every conflicting plugin is turned off.</summary>
    public static IEnumerable<InstalledPlugin> WithConflictsOff(IEnumerable<InstalledPlugin> installed, BossModChoice choice = BossModChoice.Unset) =>
        installed.Select(p => p.IsLoaded && For(choice).Any(d => d.Conflict is not null && d.InternalName.Equals(p.InternalName, StringComparison.OrdinalIgnoreCase))
            ? p with { IsLoaded = false }
            : p);

    /// <summary>The rotation plugin a run hands combat to: the first loaded one in catalog order, or null.</summary>
    public static DependencyInfo? RotationEngine(IEnumerable<RoleStatus> roles) =>
        roles.FirstOrDefault(r => r.Role == DependencyRole.Rotation)?.Candidates
            .FirstOrDefault(c => c.Status is CheckStatus.Pass or CheckStatus.Warn)?.Info;

    private static DependencyStatus Status(DependencyInfo info, InstalledPlugin? p)
    {
        if (info.Conflict is not null)
        {
            return p is { IsLoaded: true }
                ? new(info, p, CheckStatus.Fail, $"{info.DisplayName} is loaded ({p.Version}). {info.Conflict}.")
                : new(info, p, CheckStatus.Pass, p is null ? "Not installed (good)" : "Installed but disabled (good)");
        }
        if (p is null)
            return new(info, null, CheckStatus.Fail, "Not installed");
        if (!p.IsLoaded)
            return new(info, p, CheckStatus.Unknown, $"Installed ({p.Version}) but disabled");
        if (info.RepoHint is not null && p.RepoUrl?.Contains(info.RepoHint, StringComparison.OrdinalIgnoreCase) != true)
            return new(info, p, CheckStatus.Warn, $"{info.DisplayName}: this build ({p.RepoUrl ?? "unknown repo"}) may lack Crucible support; the {info.RepoHint} build has it");
        if (info.TestedVersion is { } tested && p.Version < tested)
            return new(info, p, CheckStatus.Warn, $"{info.DisplayName} {p.Version} is older than the tested {tested}: update it in /xlplugins");
        return new(info, p, CheckStatus.Pass, $"Loaded ({p.Version})");
    }
}
