using System;
using System.Collections.Generic;
using CrucibleSux.Automation;
using CrucibleSux.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace CrucibleSux.Ui;

/// <summary>
/// Offers to unlock Beastmaster when a character can take "Strangers in the Wood" and has not finished it, or to go on to
/// the Crucible when Beastmaster is unlocked and the First Board is not open yet: once per character while the plugin is
/// loaded, never again after "don't ask again", and only while nothing runs. The Journey tab has the same starts for later.
/// </summary>
/// <param name="start">Starts the journey for a goal.</param>
public sealed class UnlockPrompt(ProgressionRunner journey, Configuration config, Action save, Action<JourneyGoal> start)
    : Window("Crucible SUX: Beastmaster###CrucibleSuxUnlock", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(5);

    private readonly HashSet<ulong> asked = [];
    private DateTime nextCheck;
    private bool dontAsk;

    /// <summary>The offer is the unlock (otherwise going on to the Crucible).</summary>
    private bool unlock;

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
        if (this.asked.Contains(character) || config.UnlockOfferDeclined.Contains(character))
            return;
        this.unlock = journey.CanUnlock();
        if (!this.unlock && !journey.CanReachCrucible())
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
        if (this.unlock)
        {
            ImGui.TextWrapped("This character has not unlocked Beastmaster yet, and can: \"Strangers in the Wood\" is open to it in New Gridania.");
            ImGui.Spacing();
            ImGui.TextWrapped("Crucible SUX can pick the quest up and play it through, then switch to Beastmaster and set up its gear. " +
                              "It can also go on with the job quests, levelling and capturing the beasts they ask for, until " +
                              "\"Into the Crucible\" opens the First Board (Beastmaster level 30).");
        }
        else
        {
            ImGui.TextWrapped("This character has Beastmaster, but the Crucible is not open yet: \"Into the Crucible\" (level 30) opens the First Board.");
            ImGui.Spacing();
            ImGui.TextWrapped("Crucible SUX can do the job quests up to it, levelling and capturing the beasts they ask for, then stop.");
        }
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Untested in game. Unattended automation is against the game's terms of service.");
        ImGui.PopTextWrapPos();

        var crucible = journey.Checks(beforeSwitch: true, goal: JourneyGoal.Crucible);
        var problems = this.unlock ? journey.Checks(beforeSwitch: true, goal: JourneyGoal.Unlock) : crucible;
        foreach (var problem in problems)
            ImGui.TextColored(ImGuiColors.DalamudRed, problem);
        ImGui.Spacing();
        ImGui.Checkbox("Don't ask again for this character", ref this.dontAsk);
        ImGui.Spacing();
        if (this.unlock)
        {
            using (ImRaii.Disabled(problems.Count > 0))
            {
                if (ImGui.Button("Unlock Beastmaster"))
                    this.Start(JourneyGoal.Unlock);
            }
            ImGui.SameLine();
        }
        using (ImRaii.Disabled(crucible.Count > 0))
        {
            if (ImGui.Button(this.unlock ? "Unlock and go on to the Crucible" : "Go on to the Crucible"))
                this.Start(JourneyGoal.Crucible);
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

    private void Start(JourneyGoal goal)
    {
        this.IsOpen = false;
        start(goal);
    }
}
