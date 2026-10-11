using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrucibleSux.Core;
using CrucibleSux.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace CrucibleSux.Ui;

/// <summary>The dashboard: progress, the one-sitting check, beasts, team suggestions, companion plugins and settings.</summary>
public sealed class MainWindow : Window
{
    private readonly Session session;
    private readonly CrucibleData data;
    private readonly Configuration config;
    private readonly BestiaryWatcher bestiary;
    private readonly Action save;
    private readonly SessionTabs sessionTabs;
    private readonly JourneyTab journeyTab;
    private readonly LogTab logTab;
    private bool selectSettings;
    private bool missingOnly;
    private int teamBoard = 1;
    private (int Board, int Generation)? teamKey;
    private TeamPlan? team;

    public MainWindow(Session session, CrucibleData data, Configuration config, BestiaryWatcher bestiary, SessionTabs sessionTabs, JourneyTab journeyTab, LogTab logTab, Action save)
        : base("Crucible SUX###CrucibleSuxMain")
    {
        this.session = session;
        this.data = data;
        this.config = config;
        this.bestiary = bestiary;
        this.sessionTabs = sessionTabs;
        this.journeyTab = journeyTab;
        this.logTab = logTab;
        this.save = save;
        this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(560, 380) };
    }

    public void OpenSettings()
    {
        this.IsOpen = true;
        this.selectSettings = true;
    }

    public override void Draw()
    {
        using var bar = ImRaii.TabBar("##tabs");
        if (!bar)
            return;

        Tab("Overview", this.DrawOverview);
        Tab("One sitting", this.DrawSitting);
        Tab("Run", this.sessionTabs.DrawRun);
        Tab("History", this.sessionTabs.DrawHistory);
        Tab("Journey", this.journeyTab.Draw);
        Tab("Beasts", this.DrawBeasts);
        Tab("Teams", this.DrawTeams);
        Tab("Plugins", this.DrawPlugins);
        Tab("Log", this.logTab.Draw);
        if (ImGui.BeginTabItem("Settings", this.selectSettings ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
        {
            this.selectSettings = false;
            this.DrawSettings();
            ImGui.EndTabItem();
        }
        Tab("About", DrawAbout);
    }

    private static void Tab(string label, Action draw)
    {
        using var tab = ImRaii.TabItem(label);
        if (tab)
            draw();
    }

    private void DrawOverview()
    {
        if (this.session.Snapshot is not { } c || this.session.Progress is not { } p)
        {
            ImGui.TextDisabled("Log in to a character to read its Crucible progress.");
            return;
        }

        ImGui.TextUnformatted($"Beastmaster level {(c.JobLevel > 0 ? c.JobLevel.ToString() : "locked")}   Item level {c.ItemLevel?.ToString() ?? "unknown"}   " +
                              $"Beasts {c.UnlockedBeasts?.Count.ToString() ?? "?"}/{CrucibleIds.TotalBeasts}   Remnants: {c.FadedRemnants} faded, {c.BrightRemnants} bright");
        if (c.CompletedAchievements is null)
            ImGui.TextColored(ImGuiColors.DalamudYellow, "Achievements not loaded: open the Achievements window once so the game sends them.");
        if (c.UnlockedBeasts is null)
            ImGui.TextColored(ImGuiColors.DalamudYellow, "Beast list not loaded: open the Master's Bestiary once so the game sends it.");

        ImGui.Spacing();
        using (var table = ImRaii.Table("##boards", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH))
        {
            if (table)
            {
                ImGui.TableSetupColumn("Board", ImGuiTableColumnFlags.WidthStretch, 3f);
                foreach (var h in new[] { "Cleared", "Third degree", "Apex", "Legendary" })
                    ImGui.TableSetupColumn(h, ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableHeadersRow();
                foreach (var b in p.Boards)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{b.Board.Number}. {b.Board.Name}");
                    ImGui.TableNextColumn();
                    Mark(b.Cleared);
                    ImGui.TableNextColumn();
                    Mark(b.ThirdDegree);
                    ImGui.TableNextColumn();
                    Mark(b.Apex);
                    ImGui.TableNextColumn();
                    Mark(b.Legendary);
                }
            }
        }
        if (!p.DegreesUnlocked)
            ImGui.TextDisabled("Degrees and the rankings open with the quest \"Blazing Trails\" after the Second Master's Board.");

        ImGui.Spacing();
        ImGui.TextUnformatted("Achievements");
        ImGui.Separator();
        foreach (var goal in new[] { p.JobLevel, p.Befriended, p.Rank25 })
        {
            string next = goal.NextThreshold is int n ? $"next tier at {n}" : "all tiers done";
            string tiers = goal.TiersDone is int t ? $"{t}/{goal.Thresholds.Count} tiers" : "tiers unknown";
            ImGui.TextUnformatted($"{goal.Label}: {goal.Current?.ToString() ?? "?"} ({tiers}, {next})");
        }
        Line("Freeing the Beast", p.FreeingTheBeast);
        Line("Crucible Master", p.CrucibleMaster);
        Line("Unbroken Endgame", p.UnbrokenEndgame);
        int mates = p.KinMates.Count(k => k.Done == true);
        ImGui.TextUnformatted($"Kin mates: {(c.CompletedAchievements is null ? "?" : mates.ToString())}/{p.KinMates.Count}");
        if (c.CompletedAchievements is not null && mates < p.KinMates.Count)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"(missing: {string.Join(", ", p.KinMates.Where(k => k.Done == false).Select(k => k.Kin))})");
        }
    }

    private void DrawSitting()
    {
        if (this.session.Plan is not { } plan)
        {
            ImGui.TextDisabled("Log in to a character to check its readiness.");
            return;
        }

        if (plan.Boards.Count == 0)
        {
            ImGui.TextColored(ImGuiColors.HealerGreen, "Every board is cleared. The Overview tab lists the remaining scores and achievements.");
            return;
        }

        if (plan.CoversEverything)
            ImGui.TextColored(ImGuiColors.HealerGreen, $"You can attempt every remaining board ({plan.Boards.Count}) in one sitting.");
        else
            ImGui.TextColored(ImGuiColors.DalamudYellow, $"This sitting can cover {plan.Boards.Count - 1} board(s); it stops at the {plan.StopsAt!.Board.Name}.");
        ImGui.TextDisabled("Each board is judged as if the boards before it in this sitting were cleared.");

        foreach (var r in plan.Boards)
        {
            ImGui.Spacing();
            var (color, verdict) = r.Verdict switch
            {
                BoardVerdict.Ready => (ImGuiColors.HealerGreen, "Ready"),
                BoardVerdict.Risky => (ImGuiColors.DalamudYellow, "Risky"),
                _ => (ImGuiColors.DalamudRed, "Blocked"),
            };
            bool open;
            using (ImRaii.PushColor(ImGuiCol.Text, color))
                open = ImGui.CollapsingHeader($"{r.Board.Number}. {r.Board.Name}: {verdict}###sit{r.Board.Number}", r.Verdict == BoardVerdict.Ready ? ImGuiTreeNodeFlags.None : ImGuiTreeNodeFlags.DefaultOpen);
            if (!open)
                continue;
            foreach (var check in r.Checks)
                DrawCheck(check);
        }
    }

    /// <summary>One check as a coloured status word, its label and detail, and the hint when it is not a pass.</summary>
    internal static void DrawCheck(ReadinessCheck check)
    {
        ImGui.TextColored(StatusColor(check.Status), $"  {StatusWord(check.Status),-7}");
        ImGui.SameLine();
        ImGui.TextUnformatted($"{check.Label}: {check.Detail}");
        if (check.Hint is not null && check.Status != CheckStatus.Pass)
        {
            ImGui.Indent();
            ImGui.TextDisabled(check.Hint);
            ImGui.Unindent();
        }
    }

    private void DrawBeasts()
    {
        var c = this.session.Snapshot;
        ImGui.Checkbox("Only beasts not befriended", ref this.missingOnly);
        ImGui.SameLine();
        ImGui.TextDisabled("Ranks are read when you open a beast's page in the Master's Bestiary.");
        if (this.bestiary.LastRead is { } last)
            ImGui.TextDisabled($"Last read: {last}");

        using var table = ImRaii.Table("##beasts", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH);
        if (!table)
            return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28f);
        ImGui.TableSetupColumn("Beast", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("Kin", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Befriended", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Rank", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Where", ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableHeadersRow();

        foreach (var beast in this.data.Beasts)
        {
            bool? owned = c?.UnlockedBeasts?.Contains(beast.Id);
            if (this.missingOnly && owned != false)
                continue;
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(beast.Id.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(beast.Name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(beast.Kin.ToString());
            ImGui.TableNextColumn();
            Mark(owned);
            ImGui.TableNextColumn();
            if (c?.Beasts.GetValueOrDefault(beast.Id) is { Rank: int rank } seen)
                ImGui.TextUnformatted(seen.RankIsEstimated ? $"{rank} (est.)" : rank.ToString());
            else
                ImGui.TextDisabled(owned == true ? "not seen" : "-");
            ImGui.TableNextColumn();
            if (this.data.Locations.TryGetValue(beast.Id, out var where))
            {
                ImGui.TextUnformatted(where);
            }
            else
            {
                ImGui.TextDisabled("special");
                if (ImGui.IsItemHovered() && this.data.Descriptions.TryGetValue(beast.Id, out var text))
                    ImGui.SetTooltip(text);
            }
        }
    }

    private void DrawTeams()
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Experimental: picks are scored from the game's sheets alone and are suggestions, not tested strategies.");
        var c = this.session.Snapshot;
        if (c?.UnlockedBeasts is not { Count: > 0 } owned)
        {
            ImGui.TextDisabled("Needs the beast list: log in and open the Master's Bestiary once.");
            return;
        }

        ImGui.SetNextItemWidth(260f);
        using (var combo = ImRaii.Combo("Board", Boards.ByNumber(this.teamBoard).Name))
        {
            if (combo)
            {
                foreach (var b in Boards.All)
                {
                    if (ImGui.Selectable(b.Name, b.Number == this.teamBoard))
                        this.teamBoard = b.Number;
                }
            }
        }

        var board = Boards.ByNumber(this.teamBoard);
        if (this.teamKey != (board.Number, this.session.Generation))
        {
            this.teamKey = (board.Number, this.session.Generation);
            var battles = this.data.Battles.GetValueOrDefault(board.Number) ?? [];
            this.team = TeamRecommender.Recommend(board, battles, this.data.Beasts.Where(b => owned.Contains(b.Id)).ToList(), c.Beasts);
        }
        if (this.team is not { } plan)
            return;

        ImGui.TextUnformatted($"Bring {plan.Roster.Count} of {board.TeamSize} (ranks sync to {board.SyncRank}; unseen beasts count as rank {TeamRecommender.UnseenRank}):");
        ImGui.TextWrapped(string.Join(", ", plan.Roster.Select(r => $"{r.Beast.Name} ({r.Rank})")));
        foreach (var pick in plan.Battles)
        {
            ImGui.Spacing();
            string enemies = string.Join(", ", pick.Battle.Enemies.Select(e => e.Name).Distinct());
            if (!ImGui.CollapsingHeader($"{(pick.Battle.IsBoss ? "Boss" : $"Battle {pick.Battle.Index}")}: {enemies}###battle{pick.Battle.Index}"))
                continue;
            foreach (var s in pick.Beasts)
            {
                ImGui.BulletText($"{s.Beast.Name} (rank {s.Rank}, score {s.Score:0.#})");
                if (s.Reasons.Count > 0)
                {
                    ImGui.Indent();
                    ImGui.TextDisabled(string.Join("; ", s.Reasons));
                    ImGui.Unindent();
                }
            }
        }
    }

    private void DrawPlugins()
    {
        ImGui.TextWrapped("A session drives AutoDuty, which walks to Lauda, sets up the team, moves across the board and switches the rotation plugin and BossMod's AI on for each board.");
        foreach (var role in this.session.Dependencies)
        {
            ImGui.Spacing();
            ImGui.TextColored(StatusColor(role.Status), StatusWord(role.Status));
            ImGui.SameLine();
            ImGui.TextUnformatted($"{role.Role}{(role.Required ? string.Empty : " (optional)")}: {role.Detail}");
            ImGui.Indent();
            foreach (var cand in role.Candidates)
            {
                ImGui.TextColored(StatusColor(cand.Status), "•");
                ImGui.SameLine();
                ImGui.TextUnformatted($"{cand.Info.DisplayName}: {cand.Detail}");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{cand.Info.Purpose}\n{cand.Info.RepoUrl}");
            }
            ImGui.Unindent();
        }
    }

    private void DrawSettings()
    {
        ImGui.TextUnformatted("Sessions");
        int attempts = this.config.MaxAttempts;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.SliderInt("Tries per run", ref attempts, 1, 5))
        {
            this.config.MaxAttempts = attempts;
            this.save();
        }
        ImGui.TextDisabled("A run that fails outside the board is retried; inside a board, the session always stops.");
        int stuck = this.config.StuckMinutes;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.SliderInt("Stuck after (minutes)", ref stuck, 2, 15))
        {
            this.config.StuckMinutes = stuck;
            this.save();
        }
        ImGui.TextDisabled("Inside a board, a run with no movement, combat, HP or window change for this long is stopped.");

        ImGui.Spacing();
        ImGui.TextUnformatted("Board runs");
        bool three = this.config.RunTeamOfThree;
        if (ImGui.Checkbox("Register only the 3 best beasts", ref three))
        {
            this.config.RunTeamOfThree = three;
            this.save();
        }
        ImGui.TextDisabled("Beast Squad III (1-3 beasts) is the biggest score bonus on every board. Off: AutoDuty registers a full team, which clears more safely but scores less.");
        bool shop = this.config.RunShop;
        if (ImGui.Checkbox("Let AutoDuty buy from the board shop", ref shop))
        {
            this.config.RunShop = shop;
            this.save();
        }
        ImGui.TextDisabled("Any purchase forfeits Coin Clutcher (500).");
        bool score = this.config.RunGoal == RunGoal.Score;
        if (ImGui.Checkbox("Score mode: no items, treasure or campsite rest", ref score))
        {
            this.config.RunGoal = score ? RunGoal.Score : RunGoal.Clear;
            this.save();
        }
        if (score)
            ImGui.TextColored(ImGuiColors.DalamudYellow, "Riskier: AutoDuty then has no way to heal you or your beasts, and the shop stays closed.");
        else
            ImGui.TextDisabled("Keeps Economic Ethos, Nameday Attire and Sleepless Survival, at the cost of the ways AutoDuty recovers.");
        bool pick = this.config.PickTargets;
        if (ImGui.Checkbox("Kill dangerous adds first", ref pick))
        {
            this.config.PickTargets = pick;
            this.save();
        }
        ImGui.TextDisabled("Targets enemies that buff others (the Pas de Seul's mage) and adds with arena-wide attacks (the Ogre's wisps) first, from the game's enemy intel.");

        ImGui.Spacing();
        ImGui.TextUnformatted("Rankings");
        ImGui.TextDisabled("Automated runs should stay out of the rankings unless you opt a board in here. Crucible SUX cannot untick the option yet, so the Run tab warns when it matters.");
        foreach (var b in Boards.All)
        {
            bool on = this.config.RankingsOptIn.Contains(b.Number);
            if (ImGui.Checkbox($"{b.Name}##rank{b.Number}", ref on))
            {
                this.config.RankingsOptIn.Remove(b.Number);
                if (on)
                    this.config.RankingsOptIn.Add(b.Number);
                this.save();
            }
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Saved beast ranks");
        int characters = this.config.Characters.Count;
        ImGui.TextDisabled($"Ranks read from the Bestiary are kept for {characters} character(s).");
        if (ImGui.Button("Forget all saved ranks") && ImGui.GetIO().KeyCtrl)
        {
            this.config.Characters.Clear();
            this.save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Hold Ctrl and click. Ranks come back as you open beasts' pages again.");
    }

    private static void DrawAbout()
    {
        ImGui.TextUnformatted("Crucible SUX: Set-and-forget Unbroken eXecutor");
        ImGui.Spacing();
        ImGui.TextWrapped("Reads your progress, plans your sitting, and runs a queue of boards through AutoDuty, vnavmesh, a rotation plugin and BossMod Reborn. " +
                          "It watches every run, stops one that is stuck and reports what happened (docs/PLAN.md).");
        ImGui.Spacing();
        ImGui.TextWrapped("Unattended automation is against the game's terms of service. Use it at your own risk.");
        ImGui.Spacing();
        ImGui.TextDisabled("Commands: /sux opens this window; /sux check prints what this sitting can cover; /sux run starts the queue; " +
                           "/sux journey starts the Beastmaster journey; /sux stop stops either.");
    }

    private static void Mark(bool? done)
    {
        if (done is null)
            ImGui.TextDisabled("?");
        else if (done.Value)
            ImGui.TextColored(ImGuiColors.HealerGreen, "Yes");
        else
            ImGui.TextDisabled("No");
    }

    private static void Line(string label, bool? done)
    {
        ImGui.TextUnformatted($"{label}:");
        ImGui.SameLine();
        Mark(done);
    }

    private static Vector4 StatusColor(CheckStatus s) => s switch
    {
        CheckStatus.Pass => ImGuiColors.HealerGreen,
        CheckStatus.Warn => ImGuiColors.DalamudYellow,
        CheckStatus.Unknown => ImGuiColors.DalamudGrey,
        _ => ImGuiColors.DalamudRed,
    };

    private static string StatusWord(CheckStatus s) => s switch
    {
        CheckStatus.Pass => "OK",
        CheckStatus.Warn => "Warn",
        CheckStatus.Unknown => "Unknown",
        _ => "Missing",
    };
}
