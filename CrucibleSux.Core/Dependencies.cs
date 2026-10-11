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

/// <param name="RepoHint">A substring of the plugin's repo URL that marks the build with the features needed (e.g. a fork).</param>
/// <param name="Conflict">Why this plugin must be disabled for the role: loaded, it fails the role even beside a good candidate.</param>
public sealed record DependencyInfo(string InternalName, string DisplayName, DependencyRole Role, string Purpose, string RepoUrl, string? RepoHint = null, string? Conflict = null);

/// <summary>A plugin as Dalamud reports it.</summary>
public sealed record InstalledPlugin(string InternalName, Version Version, bool IsLoaded, string? RepoUrl);

public sealed record DependencyStatus(DependencyInfo Info, InstalledPlugin? Installed, CheckStatus Status, string Detail);

public sealed record RoleStatus(DependencyRole Role, bool Required, CheckStatus Status, string Detail, IReadOnlyList<DependencyStatus> Candidates);

public static class DependencyCatalog
{
    public static readonly IReadOnlyList<DependencyInfo> All =
    [
        new("vnavmesh", "vnavmesh", DependencyRole.Navigation, "Pathing in the overworld", "https://github.com/awgil/ffxiv_navmesh"),
        new("RotationSolver", "Rotation Solver Reborn", DependencyRole.Rotation, "Beastmaster rotation (shipped 2026-09-13)", "https://github.com/FFXIV-CombatReborn/RotationSolverReborn"),
        new("BossModReborn", "BossMod Reborn", DependencyRole.Rotation, "Beastmaster rotation module (work in progress)", "https://github.com/FFXIV-CombatReborn/BossmodReborn"),
        new("WrathCombo", "Wrath Combo", DependencyRole.Rotation, "Rotation (no Beastmaster support seen yet)", "https://github.com/PunishXIV/WrathCombo"),
        new("BossModReborn", "BossMod Reborn", DependencyRole.Mechanics, "AI movement and dodging", "https://github.com/FFXIV-CombatReborn/BossmodReborn"),
        new("BossMod", "BossMod (original)", DependencyRole.Mechanics, "Not used: Crucible SUX needs BossMod Reborn", "https://github.com/awgil/ffxiv_bossmod",
            Conflict: "It blocks movement while you cast, so the character cannot dodge, and it does not put the First Board's " +
                      "dangerous adds first. Disable it and use BossMod Reborn"),
        new("AutoDuty", "AutoDuty (erdelf)", DependencyRole.BoardDriver, "Lauda's menus, team setup, board movement", "https://github.com/erdelf/AutoDuty", "erdelf"),
        new("Lifestream", "Lifestream", DependencyRole.Travel, "Teleports", "https://github.com/NightmareXIV/Lifestream"),
        new("TextAdvance", "TextAdvance", DependencyRole.Dialogs, "Advancing NPC text", "https://github.com/NightmareXIV/TextAdvance"),
    ];

    private static readonly DependencyRole[] RequiredRoles = [DependencyRole.Navigation, DependencyRole.Rotation, DependencyRole.Mechanics, DependencyRole.BoardDriver];

    public static IReadOnlyList<RoleStatus> Evaluate(IEnumerable<InstalledPlugin> installed)
    {
        var byName = installed.GroupBy(p => p.InternalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.IsLoaded).First(), StringComparer.OrdinalIgnoreCase);

        var result = new List<RoleStatus>();
        foreach (var role in Enum.GetValues<DependencyRole>())
        {
            bool required = RequiredRoles.Contains(role);
            var candidates = All.Where(d => d.Role == role)
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
        return new(info, p, CheckStatus.Pass, $"Loaded ({p.Version})");
    }
}
