using CrucibleSux.Automation;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CrucibleSux.Ui;

/// <summary>Asks before the original BossMod is turned off for a session or the journey.</summary>
public sealed class BossModPrompt(BossModGate gate) : Window("Crucible SUX: the original BossMod###CrucibleSuxBossMod", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
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
        ImGui.TextWrapped("The original BossMod is loaded. It blocks movement while you cast, so your character cannot dodge, " +
                          "and it can take the commands AutoDuty means for BossMod Reborn.");
        ImGui.Spacing();
        ImGui.TextWrapped($"Crucible SUX can turn it off now and back on when {what} ends. It uses Dalamud's temporary switch: " +
                          "your plugin settings do not change, and restarting the game turns it back on as well. " +
                          "BossMod Reborn may be reloaded once so that AutoDuty can reach it.");
        ImGui.PopTextWrapPos();
        ImGui.Spacing();
        ImGui.Checkbox("Do this without asking next time", ref this.remember);
        ImGui.Spacing();
        if (ImGui.Button($"Turn it off and start {what}"))
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
}
