namespace CrucibleSux.Core;

/// <param name="Character">The logged-in character, or null.</param>
/// <param name="OnBeastmaster">The current job is Beastmaster.</param>
/// <param name="InDuty">The character is inside a duty, a board included.</param>
/// <param name="ExecutorProblem">Why AutoDuty cannot take the run, or null when it can.</param>
/// <param name="TurnOff">
/// Conflicting plugins Crucible SUX turns off before the start; <paramref name="Dependencies"/> is then evaluated as if they were off.
/// </param>
public sealed record PreflightInput(
    CharacterSnapshot? Character,
    bool OnBeastmaster,
    bool InDuty,
    IReadOnlyList<RoleStatus> Dependencies,
    string? ExecutorProblem,
    IReadOnlyList<QueueEntry> Queue,
    IReadOnlyCollection<int> RankingsOptIn,
    IReadOnlyList<DependencyInfo>? TurnOff = null);

/// <summary>The guard rails checked before a session starts (docs/PLAN.md F12). Any Fail blocks the start.</summary>
public static class Preflight
{
    public static bool CanStart(IEnumerable<ReadinessCheck> checks) => checks.All(c => c.Status != CheckStatus.Fail);

    public static IReadOnlyList<ReadinessCheck> Check(PreflightInput input)
    {
        if (input.Character is not { } c)
            return [new("character", "Character", CheckStatus.Fail, "Not logged in")];

        var checks = new List<ReadinessCheck>
        {
            input.OnBeastmaster
                ? new("job", "Job", CheckStatus.Pass, "Beastmaster")
                : new("job", "Job", CheckStatus.Fail, "Not on Beastmaster", "Switch to Beastmaster before starting."),
            input.InDuty
                ? new("place", "Where you are", CheckStatus.Fail, "Inside a duty", "Finish or leave it first; a session starts from the open world.")
                : new("place", "Where you are", CheckStatus.Pass, "Open world"),
            Plugins(input.Dependencies),
            Rotation(input.Dependencies),
            input.ExecutorProblem is null
                ? new("executor", "AutoDuty", CheckStatus.Pass, "Ready")
                : new("executor", "AutoDuty", CheckStatus.Fail, input.ExecutorProblem),
        };
        if (input.TurnOff is { Count: > 0 } turnOff)
        {
            checks.Insert(3, new("turn-off", "Turned off for the run", CheckStatus.Warn,
                $"{string.Join(", ", turnOff.Select(d => d.DisplayName))} is loaded: it is turned off when the run starts and back on when it ends",
                "Crucible SUX asks first, unless Settings allows it. Restarting the game also turns it back on."));
        }

        if (input.Queue.Count == 0)
        {
            checks.Add(new("queue", "Queue", CheckStatus.Fail, "Empty", "Add a board, or fill the queue with the boards this sitting can cover."));
            return checks;
        }

        var cleared = new HashSet<int>(c.ClearedBoards);
        foreach (var number in input.Queue.Select(e => e.Board).Distinct())
        {
            var r = ReadinessEvaluator.Evaluate(Boards.ByNumber(number), c, cleared);
            checks.Add(Board(r));
            cleared.Add(number);
        }

        checks.Add(Rankings(c, input.Queue, input.RankingsOptIn));
        return checks;
    }

    private static ReadinessCheck Plugins(IReadOnlyList<RoleStatus> roles)
    {
        var missing = roles.Where(r => r.Required && r.Status == CheckStatus.Fail).Select(r => r.Role).ToList();
        if (missing.Count > 0)
            return new("plugins", "Companion plugins", CheckStatus.Fail, $"Missing: {string.Join(", ", missing)}", "The Plugins tab lists what to install.");
        var warned = roles.Where(r => r.Required && r.Status == CheckStatus.Warn).ToList();
        return warned.Count > 0
            ? new("plugins", "Companion plugins", CheckStatus.Warn, string.Join("; ", warned.Select(r => r.Detail)))
            : new("plugins", "Companion plugins", CheckStatus.Pass, "All required roles covered");
    }

    private static ReadinessCheck Rotation(IReadOnlyList<RoleStatus> roles) => DependencyCatalog.RotationEngine(roles) switch
    {
        null => new("rotation", "Rotation", CheckStatus.Fail, "No rotation plugin loaded", "Install Rotation Solver Reborn: it has a Beastmaster rotation."),
        { InternalName: "WrathCombo" } => new("rotation", "Rotation", CheckStatus.Fail, "Only Wrath Combo is loaded, and it has no Beastmaster rotation",
            "Install Rotation Solver Reborn, or enable BossMod Reborn's Beastmaster module."),
        { InternalName: "RotationSolver" } when WrathLoaded(roles) => new("rotation", "Rotation", CheckStatus.Warn,
            "Wrath Combo is loaded beside Rotation Solver Reborn, which lists it as breaking its rotations (the character may use no skills)",
            "Turn Wrath Combo off in /xlplugins while Crucible SUX runs."),
        var engine => new("rotation", "Rotation", CheckStatus.Pass, $"{engine.DisplayName} fights; AutoDuty switches it on in the board"),
    };

    /// <summary>Wrath Combo is loaded (as a rotation candidate that passes or warns).</summary>
    public static bool WrathLoaded(IEnumerable<RoleStatus> roles) =>
        roles.Where(r => r.Role == DependencyRole.Rotation).SelectMany(r => r.Candidates)
            .Any(c => c.Info.InternalName == "WrathCombo" && c.Installed is { IsLoaded: true });

    private static ReadinessCheck Board(BoardReadiness r)
    {
        string key = $"board-{r.Board.Number}";
        string label = r.Board.Name;
        if (r.Verdict == BoardVerdict.Blocked)
        {
            var fail = r.Checks.First(x => x.Status == CheckStatus.Fail);
            return new(key, label, CheckStatus.Fail, $"{fail.Label}: {fail.Detail}", fail.Hint);
        }
        var doubts = r.Checks.Where(x => x.Status is CheckStatus.Warn or CheckStatus.Unknown).ToList();
        return doubts.Count == 0
            ? new(key, label, CheckStatus.Pass, "Ready")
            : new(key, label, CheckStatus.Warn, string.Join("; ", doubts.Select(x => $"{x.Label}: {x.Detail}")), "The One sitting tab explains each warning.");
    }

    private static ReadinessCheck Rankings(CharacterSnapshot c, IReadOnlyList<QueueEntry> queue, IReadOnlyCollection<int> optIn)
    {
        if (c.Quest(CrucibleIds.Quests.BlazingTrails) != QuestState.Complete)
            return new("rankings", "Rankings", CheckStatus.Pass, "Not open on this character yet");
        var outside = queue.Select(e => e.Board).Distinct().Where(b => !optIn.Contains(b)).Select(b => Boards.ByNumber(b).Name).ToList();
        return outside.Count == 0
            ? new("rankings", "Rankings", CheckStatus.Pass, "Every queued board is opted in")
            : new("rankings", "Rankings", CheckStatus.Warn, $"Crucible SUX cannot untick \"Participate in rankings\" yet: {string.Join(", ", outside)}",
                "Untick it on the board screen yourself before starting, or opt the board in under Settings.");
    }
}
