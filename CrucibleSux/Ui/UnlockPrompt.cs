using System;
using System.Collections.Generic;
using CrucibleSux.Automation;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace CrucibleSux.Ui;

/// <summary>
/// Offers to unlock Beastmaster when a character can take "Strangers in the Wood" and has not finished it: once per
/// character while the plugin is loaded, never again after "don't ask again", and only while nothing runs. The Journey
/// tab has the same start for later.
/// </summary>
/// <param name="start">Starts the journey in unlock-only mode, through the BossMod question when needed.</param>
public sealed class UnlockPrompt(ProgressionRunner journey, Configuration config, Action save, Action start)
    : Window("Crucible SUX: unlock Beastmaster###CrucibleSuxUnlock", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(5);

    private readonly HashSet<ulong> asked = [];
    private DateTime nextCheck;
    private bool dontAsk;

    /// <summary>Called every framework tick; <paramref name="idle"/> says nothing runs and the character is free.</summary>
    public void Update(ulong character, bool idle)
    {
        var now = DateTime.UtcNow;
        // The offer is for the character it was made to, and only while nothing else runs.
        if (this.IsOpen && (character != this.Character || journey.Running))
            this.IsOpen = false;
        if (this.IsOpen || character == 0 || !idle || now < this.nextCheck)
            return;
        this.nextCheck = now + CheckEvery;
        if (this.asked.Contains(character) || config.UnlockOfferDeclined.Contains(character) || !journey.CanUnlock())
            return;
        this.asked.Add(character);
        this.Character = character;
        this.dontAsk = false;
        this.IsOpen = true;
    }

    private ulong Character { get; set; }

    public override void Draw()
    {
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 32f);
        ImGui.TextWrapped("This character has not unlocked Beastmaster yet, and can: \"Strangers in the Wood\" is open to it in New Gridania.");
        ImGui.Spacing();
        ImGui.TextWrapped("Crucible SUX can pick the quest up and play it through, then switch to Beastmaster and set up its gear. " +
                          "It stops there; the rest of the journey (the other job quests, beasts, levels) stays as the Journey tab sets it.");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Untested in game. Unattended automation is against the game's terms of service.");
        ImGui.PopTextWrapPos();

        var problems = journey.Checks(beforeSwitch: true, unlockOnly: true);
        foreach (var problem in problems)
            ImGui.TextColored(ImGuiColors.DalamudRed, problem);
        ImGui.Spacing();
        ImGui.Checkbox("Don't ask again for this character", ref this.dontAsk);
        ImGui.Spacing();
        using (ImRaii.Disabled(problems.Count > 0))
        {
            if (ImGui.Button("Unlock Beastmaster now"))
            {
                this.IsOpen = false;
                start();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Not now"))
            this.IsOpen = false;
    }

    /// <summary>Closing the window any way records "don't ask again" when it is ticked.</summary>
    public override void OnClose()
    {
        if (!this.dontAsk || config.UnlockOfferDeclined.Contains(this.Character))
            return;
        config.UnlockOfferDeclined.Add(this.Character);
        save();
    }
}
