using System;
using System.Linq;
using CrucibleSux.Automation;
using CrucibleSux.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Ui;

/// <summary>The Run tab (queue, guard rails, start and stop, live status) and the History tab.</summary>
/// <param name="start">Starts the session, turning the original BossMod off first when needed.</param>
public sealed class SessionTabs(SessionRunner runner, ProgressionRunner journey, Session session, Configuration config, IPlayerState player, Action save, Action start)
{
    private int addBoard = 1;

    public void DrawRun()
    {
        if (runner.Current is { } o)
        {
            this.DrawLive(o);
            return;
        }
        if (journey.Running)
        {
            ImGui.TextWrapped("The Beastmaster journey is running and starts the boards it needs itself. Stop it (Journey tab or /sux stop) to run a session.");
            return;
        }

        if (runner.LastReport is { } report)
        {
            ImGui.TextColored(report.GoalReached ? ImGuiColors.HealerGreen : ImGuiColors.DalamudYellow, "Last session");
            foreach (var line in report.Lines())
                ImGui.TextWrapped(line);
            ImGui.Separator();
        }

        ImGui.TextWrapped("AutoDuty plays each board; Crucible SUX queues the boards, watches every run, stops a run that is stuck and reports back.");
        ImGui.Spacing();
        this.DrawQueue();
        ImGui.Spacing();

        var checks = runner.Checks(beforeSwitch: true);
        foreach (var check in checks)
            MainWindow.DrawCheck(check);

        ImGui.Spacing();
        using (ImRaii.Disabled(!Preflight.CanStart(checks)))
        {
            if (ImGui.Button("Start session"))
                start();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Unattended automation is against the game's terms of service.");
    }

    public void DrawHistory()
    {
        if (!player.IsLoaded || !config.Runs.TryGetValue(player.ContentId, out var history) || history.Count == 0)
        {
            ImGui.TextDisabled("No runs recorded for this character yet.");
            return;
        }

        foreach (var group in history.GroupBy(r => r.Board).OrderBy(g => g.Key))
        {
            int finished = group.Count(r => r.Outcome == RunOutcome.Finished);
            ImGui.TextUnformatted($"{Boards.ByNumber(group.Key).Name}: {group.Count()} run(s), {finished} finished, last {group.Max(r => r.StartedUtc).ToLocalTime():g}");
        }
        if (ImGui.Button("Clear this character's history") && ImGui.GetIO().KeyCtrl)
        {
            config.Runs.Remove(player.ContentId);
            save();
            return;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Hold Ctrl and click.");

        using var table = ImRaii.Table("##history", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH);
        if (!table)
            return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("When", ImGuiTableColumnFlags.WidthStretch, 1.4f);
        ImGui.TableSetupColumn("Board", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("Try", ImGuiTableColumnFlags.WidthFixed, 32f);
        ImGui.TableSetupColumn("Outcome", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Took", ImGuiTableColumnFlags.WidthStretch, 0.8f);
        ImGui.TableSetupColumn("Detail", ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableHeadersRow();
        for (int i = history.Count - 1; i >= 0; i--)
            Row(history[i]);
    }

    private void DrawLive(Orchestrator o)
    {
        var now = DateTime.UtcNow;
        ImGui.TextColored(ImGuiColors.HealerGreen, o.Status);
        if (o.Board is { } board)
        {
            ImGui.TextUnformatted($"Queue line {o.EntryIndex + 1} of {o.Queue.Count}: {board.Name}, run {o.RunNumber} of {o.Queue[o.EntryIndex].Runs}" +
                                  (o.Attempt > 0 ? $", attempt {o.Attempt}" : string.Empty));
        }
        string sinceChange = o.Phase == SessionPhase.InBoard ? $"; last change {Ago(now - o.LastProgress)} ago" : string.Empty;
        ImGui.TextDisabled($"Started {Ago(now - o.StartedUtc)} ago{sinceChange}.");
        ImGui.Spacing();
        if (ImGui.Button("Stop session"))
            runner.Stop("Stopped by the player.");
        ImGui.SameLine();
        ImGui.TextDisabled("or type /sux stop");

        if (o.Runs.Count == 0)
            return;
        ImGui.Spacing();
        using var table = ImRaii.Table("##runs", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH);
        if (!table)
            return;
        ImGui.TableSetupColumn("When", ImGuiTableColumnFlags.WidthStretch, 1.4f);
        ImGui.TableSetupColumn("Board", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("Try", ImGuiTableColumnFlags.WidthFixed, 32f);
        ImGui.TableSetupColumn("Outcome", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Took", ImGuiTableColumnFlags.WidthStretch, 0.8f);
        ImGui.TableSetupColumn("Detail", ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableHeadersRow();
        foreach (var run in o.Runs)
            Row(run);
    }

    private void DrawQueue()
    {
        ImGui.TextUnformatted("Queue");
        ImGui.Separator();
        int remove = -1;
        for (int i = 0; i < config.Queue.Count; i++)
        {
            var e = config.Queue[i];
            ImGui.TextUnformatted($"{i + 1}. {Boards.ByNumber(e.Board).Name}");
            ImGui.SameLine(300f);
            int runs = e.Runs;
            ImGui.SetNextItemWidth(90f);
            if (ImGui.InputInt($"run(s)##runs{i}", ref runs) && runs is >= 1 and <= 99)
            {
                config.Queue[i] = e with { Runs = runs };
                save();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##q{i}"))
                remove = i;
        }
        if (remove >= 0)
        {
            config.Queue.RemoveAt(remove);
            save();
        }
        if (config.Queue.Count == 0)
            ImGui.TextDisabled("Empty.");

        ImGui.SetNextItemWidth(260f);
        using (var combo = ImRaii.Combo("##addBoard", Boards.ByNumber(this.addBoard).Name))
        {
            if (combo)
            {
                foreach (var b in Boards.All)
                {
                    if (ImGui.Selectable(b.Name, b.Number == this.addBoard))
                        this.addBoard = b.Number;
                }
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Add"))
        {
            config.Queue.Add(new QueueEntry(this.addBoard, 1));
            save();
        }
        ImGui.SameLine();
        using (ImRaii.Disabled(session.Plan is null))
        {
            if (ImGui.Button("Fill with this sitting") && session.Plan is { } plan)
            {
                config.Queue = SessionQueue.FromPlan(plan);
                save();
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Every board you can attempt now, once each, in order: the One sitting tab's plan.");
    }

    private static void Row(RunRecord run)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(run.StartedUtc.ToLocalTime().ToString("g"));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Boards.ByNumber(run.Board).Name + (run.FirstClear ? " (first clear)" : string.Empty));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(run.Attempt.ToString());
        ImGui.TableNextColumn();
        ImGui.TextColored(run.Outcome == RunOutcome.Finished ? ImGuiColors.HealerGreen : ImGuiColors.DalamudYellow, SessionReport.Word(run.Outcome));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Ago(run.Duration));
        ImGui.TableNextColumn();
        ImGui.TextWrapped(run.Detail);
    }

    private static string Ago(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes}m {t.Seconds:00}s" : $"{Math.Max(0, (int)t.TotalSeconds)}s";
}
