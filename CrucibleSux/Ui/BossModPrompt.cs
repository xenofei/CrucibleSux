using CrucibleSux.Automation;
using CrucibleSux.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CrucibleSux.Ui;

/// <summary>
/// Asks which BossMod the player uses (once), and before the other one is turned off for a session or the journey. When
/// both are needed, answering the first counts for the second.
/// </summary>
public sealed class BossModPrompt(BossModGate gate) : Window("Crucible SUX: BossMod###CrucibleSuxBossMod", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
{
    private bool remember;

    public override void Draw()
    {
        if (gate.Asking is not { } what)
        {
            this.IsOpen = false;
            return;
        }
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 32f);
        if (gate.Choosing)
        {
            ImGui.TextUnformatted("Which BossMod do you use?");
            ImGui.Spacing();
            ImGui.TextWrapped("Both answer AutoDuty under the same names, so only one may be loaded during a run. Crucible SUX keeps the one you use, " +
                              $"turns the other off when {what} starts if it is loaded, and back on when {what} ends. You can change this in Settings.");
        }
        else
        {
            ImGui.TextWrapped($"You use {BossModSwitch.Name(gate.Kept)}, and {BossModSwitch.Name(gate.Off)} is loaded too. Both answer AutoDuty under the same " +
                              $"names, so only one may be loaded during a run. Crucible SUX can turn it off now and back on when {what} ends.");
        }
        ImGui.Spacing();
        ImGui.TextWrapped("It uses Dalamud's temporary switch: your plugin settings do not change, and restarting the game turns it back on as well. " +
                          "The BossMod you use may be reloaded once so that AutoDuty can reach it.");
        ImGui.PopTextWrapPos();
        ImGui.Spacing();
        ImGui.Checkbox("Turn the other BossMod off without asking next time", ref this.remember);
        ImGui.Spacing();
        if (gate.Choosing)
        {
            this.Choice(BossModChoice.Reborn, $"BossMod Reborn ({gate.State(BossModSwitch.Reborn)})");
            ImGui.SameLine();
            this.Choice(BossModChoice.Original, $"The original BossMod ({gate.State(BossModSwitch.Original)})");
        }
        else if (ImGui.Button($"Turn it off and start {what}"))
        {
            gate.Accept(this.remember);
            this.IsOpen = false;
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            gate.Decline();
            this.IsOpen = false;
        }
    }

    /// <summary>Closing the window (its X or Escape) counts as Cancel.</summary>
    public override void OnClose() => gate.Decline();

    private void Choice(BossModChoice choice, string label)
    {
        if (ImGui.Button(label))
        {
            gate.Choose(choice, this.remember);
            this.IsOpen = false;
        }
    }
}
