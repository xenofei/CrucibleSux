using System;
using CrucibleSux.Automation;
using CrucibleSux.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;

namespace CrucibleSux.Ui;

/// <summary>The Journey tab: the three opt-in parts of the Beastmaster journey, start and stop, and what it is doing.</summary>
/// <param name="start">Starts the journey, turning the BossMod the player does not use off first when needed.</param>
/// <param name="startGoal">Starts it for a goal whatever the parts say: unlock the job, or go on to the Crucible.</param>
/// <param name="backgroundFpsLimited">The game's "limit frame rate when inactive" is on, which makes vnavmesh wander when not focused.</param>
public sealed class JourneyTab(ProgressionRunner journey, Configuration config, Action save, Action start, Action<JourneyGoal> startGoal, Func<bool> backgroundFpsLimited)
{
    private static readonly TimeSpan PreviewEvery = TimeSpan.FromSeconds(1);
    private DateTime nextPreview;
    private Activity? preview;
    private bool canUnlock;
    private bool canReachCrucible;

    public void Draw()
    {
        ImGui.TextWrapped("Takes a character from the Beastmaster unlock quest to level 50 with all 50 beasts. Each part is off until you turn it on.");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Untested in game: this was built from the game's data and public guides, not from play. Watch the first runs.");
        ImGui.Spacing();

        using (ImRaii.Disabled(journey.Running))
        {
            this.Option("Do the Beastmaster job quests", "All 13, from \"Strangers in the Wood\" to \"Blazing Trails\". Includes the captures, levels and boards each quest asks for; gourds are bought only with \"Collect every beast\" on.",
                config.ProgressQuests, v => config.ProgressQuests = v);
            this.Option("Collect every beast", "Captures wild beasts, uses gourds you hold, and buys the rest from the Kornago merchant with remnants (farming the highest cleared board when short).",
                config.ProgressBeasts, v => config.ProgressBeasts = v);
            this.Option("Level Beastmaster to 50", "FATEs in the zones that suit each level band, best expected EXP per second first; the zone changes when another has measured faster.",
                config.ProgressLeveling, v => config.ProgressLeveling = v);
        }
        ImGui.Spacing();
        ImGui.Separator();

        if (journey.Running)
        {
            ImGui.TextColored(ImGuiColors.HealerGreen, journey.Activity?.Why ?? "Starting.");
            ImGui.TextWrapped(journey.Status);
            if (journey.Waiting is { } waiting)
                ImGui.TextColored(ImGuiColors.DalamudYellow, $"Waiting for you: {waiting}");
            ImGui.Spacing();
            if (ImGui.Button("Stop journey"))
                journey.Stop("Stopped by the player.");
            ImGui.SameLine();
            ImGui.TextDisabled("or type /sux stop");
            return;
        }

        if (journey.LastResult is { } last)
            ImGui.TextWrapped($"Last run: {last}");
        var now = DateTime.UtcNow;
        if (now >= this.nextPreview)
        {
            this.nextPreview = now + PreviewEvery;
            this.preview = journey.Preview();
            this.canUnlock = journey.CanUnlock();
            this.canReachCrucible = journey.CanReachCrucible();
        }
        if (this.preview is { } next)
        {
            var color = next.Kind == ActivityKind.Blocked ? ImGuiColors.DalamudYellow : ImGuiColors.DalamudWhite;
            ImGui.TextColored(color, $"Next: {next.Why}");
        }

        var problems = journey.Checks(beforeSwitch: true);
        foreach (var problem in problems)
            ImGui.TextColored(ImGuiColors.DalamudRed, problem);
        if (journey.Checks().Count > problems.Count)
            ImGui.TextColored(ImGuiColors.DalamudYellow, "The BossMod you do not use is loaded: it is turned off when the journey starts and back on when it ends.");
        ImGui.Spacing();
        using (ImRaii.Disabled(problems.Count > 0))
        {
            if (ImGui.Button("Start journey"))
                start();
        }
        if (this.canUnlock)
        {
            ImGui.SameLine();
            if (ImGui.Button("Unlock Beastmaster only"))
                startGoal(JourneyGoal.Unlock);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Picks up \"Strangers in the Wood\", plays it through, switches to Beastmaster and sets up its gear, then stops.");
        }
        if (this.canUnlock || this.canReachCrucible)
        {
            ImGui.SameLine();
            if (ImGui.Button(this.canUnlock ? "Unlock and go on to the Crucible" : "Go on to the Crucible"))
                startGoal(JourneyGoal.Crucible);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Does the job quests, with the levels and beasts they ask for, until \"Into the Crucible\" opens the First Board, then stops.");
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Unattended automation is against the game's terms of service.");
        ImGui.Spacing();
        ImGui.TextDisabled("Needs vnavmesh and Rotation Solver Reborn; boards also need AutoDuty (see the Plugins tab).");
        if (backgroundFpsLimited())
            ImGui.TextColored(ImGuiColors.DalamudYellow, "The game limits its frame rate when not focused, so the character can turn back and forth while you alt-tab. Untick System Configuration > Display Settings > \"Limit frame rate when client is inactive\".");
        ImGui.TextDisabled("You will be asked to put beasts on Battlehorns yourself: the plugin cannot do that yet.");
    }

    private void Option(string label, string help, bool value, Action<bool> set)
    {
        if (ImGui.Checkbox(label, ref value))
        {
            set(value);
            save();
            this.nextPreview = DateTime.MinValue;
        }
        ImGui.Indent();
        ImGui.TextDisabled(help);
        ImGui.Unindent();
    }
}
