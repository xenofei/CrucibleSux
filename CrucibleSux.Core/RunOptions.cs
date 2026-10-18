namespace CrucibleSux.Core;

/// <summary>What a board run aims for (docs/research/15-combat-findings.md §2).</summary>
public enum RunGoal
{
    /// <summary>Clear the board: AutoDuty may use items, rest at campsites and open treasure.</summary>
    Clear,

    /// <summary>
    /// Score: also skip what forfeits a bonus. No crucible items (Economic Ethos), no treasure, which can hold beast gear
    /// (Nameday Attire), and no campsite rest (Sleepless Survival). Riskier: AutoDuty then has no way to heal.
    /// </summary>
    Score,
}

/// <param name="TeamOfThree">Register only the 3 best beasts: Beast Squad III (1-3 beasts) is worth 4,250 on every board.</param>
/// <param name="Shop">Let AutoDuty buy from the board shop; any purchase forfeits Coin Clutcher (500).</param>
public sealed record RunOptions(bool TeamOfThree, bool Shop, RunGoal Goal)
{
    public static readonly RunOptions Default = new(TeamOfThree: true, Shop: false, Goal: RunGoal.Clear);
}

/// <summary>
/// The temporary AutoDuty settings a board run pushes. Names are AutoDuty's config paths at commit 4a0dfdc, rechecked at
/// 7e0ede4 (Configurations/ConfigurationProfileV2.cs); AutoDuty rejects the whole push when one name is unknown.
/// </summary>
public static class AutoDutyOverrides
{
    /// <summary>The smallest team AutoDuty's Recommended mode allows (it clamps its cap to 3-15).</summary>
    public const int SmallestTeam = 3;

    public static IReadOnlyDictionary<string, string> For(string rotationPlugin, RunOptions options)
    {
        bool score = options.Goal == RunGoal.Score;
        var o = new Dictionary<string, string>
        {
            ["Meta.DutyModeEnum"] = "Crucible",
            ["Meta.Crucible.Degree"] = "Standard",
            ["DutyConfig.AutoExitDuty"] = "true",
            ["DutyConfig.AutoManageRotationPluginState"] = "true",
            ["DutyConfig.AutoManageBossModAISettings"] = "true",
            ["DutyConfig.UsingAlternativeRotationPlugin"] = "false",
            ["DutyConfig.RotationPlugin"] = rotationPlugin,
            ["Meta.Crucible.Shop"] = Bool(options.Shop && !score),
        };
        if (options.TeamOfThree)
        {
            // Recommended mode takes the highest-ranked owned beasts up to this cap.
            o["Meta.Crucible.TeamMode"] = "Recommended";
            o["Meta.Crucible.RecommendedTeamCap"] = SmallestTeam.ToString();
        }
        if (score)
        {
            o["Meta.Crucible.Items"] = "false";
            o["Meta.Crucible.Treasure"] = "false";
            o["Meta.Crucible.Rest"] = "false";
        }
        return o;
    }

    private static string Bool(bool b) => b ? "true" : "false";
}
