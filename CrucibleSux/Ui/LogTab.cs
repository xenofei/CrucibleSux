using System;
using System.Linq;
using CrucibleSux.Core;
using CrucibleSux.Diagnostics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;

namespace CrucibleSux.Ui;

/// <summary>The combat logs: what is being recorded, the last few logs, and buttons to copy one for a bug report.</summary>
public sealed class LogTab(CombatRecorder recorder, Configuration config, Action save)
{
    private CombatLog? selected;
    private string? copied;

    public void Draw()
    {
        bool record = config.RecordCombat;
        if (ImGui.Checkbox("Record boards for bug reports", ref record))
        {
            config.RecordCombat = record;
            save();
        }
        ImGui.TextDisabled("While you are in a board, or a session or the journey runs: enemy casts and where you stood, HP, statuses, " +
                           "beasts, targets, movement, vnavmesh paths, AutoDuty and the Crucible windows. Your character's name is replaced with <you>.");

        if (recorder.Current is { } now)
            ImGui.TextColored(ImGuiColors.HealerGreen, $"Recording: {now.Title}, {now.Lines.Count} lines so far.");
        else
            ImGui.TextDisabled("Not recording.");

        var logs = recorder.Logs.ToList();
        if (logs.Count == 0)
        {
            ImGui.TextUnformatted("No logs yet. Run a board, then come back here.");
            return;
        }
        if (this.selected is null || !logs.Contains(this.selected))
            this.selected = logs[0];

        using (var table = ImRaii.Table("##logs", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH))
        {
            if (table)
            {
                ImGui.TableSetupColumn("Log", ImGuiTableColumnFlags.WidthStretch, 3f);
                ImGui.TableSetupColumn("Started");
                ImGui.TableSetupColumn("Length");
                ImGui.TableSetupColumn("Stood in");
                ImGui.TableSetupColumn("KOs");
                ImGui.TableHeadersRow();
                var utcNow = DateTime.UtcNow;
                foreach (var log in logs)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    string label = log.EndedUtc is null ? $"{log.Title} (recording)" : log.Title;
                    if (ImGui.Selectable($"{label}##{log.StartedUtc.Ticks}", ReferenceEquals(log, this.selected), ImGuiSelectableFlags.SpanAllColumns))
                        this.selected = log;
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(log.StartedUtc.ToLocalTime().ToString("HH:mm"));
                    ImGui.TableNextColumn();
                    var d = log.Duration(utcNow);
                    ImGui.TextUnformatted($"{(int)d.TotalMinutes}m {d.Seconds:00}s");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(log.Count(LogKind.Hit).ToString());
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(log.Count(LogKind.Death).ToString());
                }
            }
        }

        ImGui.Spacing();
        var pick = this.selected;
        if (ImGui.Button("Copy summary"))
            this.Copy(pick.Summary(DateTime.UtcNow), "Summary");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("The settings, counts and the important lines (hits, KOs, warnings); fits in one Discord message.");
        ImGui.SameLine();
        if (ImGui.Button("Copy full log"))
            this.Copy(pick.Full(DateTime.UtcNow), "Full log");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Every line. Discord turns a long paste into a file attachment; send it as it is.");
        if (this.copied is not null)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.HealerGreen, this.copied);
        }

        ImGui.TextDisabled("Paste it into your report. \"/sux report\" copies the latest summary from chat.");
        ImGui.TextDisabled($"Finished logs are also saved in {recorder.Folder}");
    }

    private void Copy(string text, string what)
    {
        ImGui.SetClipboardText(text);
        this.copied = $"{what} copied ({text.Length:N0} characters).";
    }
}
