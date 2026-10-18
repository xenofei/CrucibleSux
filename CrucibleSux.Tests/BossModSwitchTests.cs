using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class BossModSwitchTests
{
    private static readonly DateTime T0 = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    private static SwitchView View(bool off = true, bool kept = true, bool ipc = true, bool installed = true) => new(installed, off, kept, ipc);

    [Fact]
    public void Begin_turns_the_original_off_temporarily()
    {
        var s = new BossModSwitch();

        Assert.Equal(["/xldisableplugintemp BossMod"], s.Begin(View(), T0));
        Assert.True(s.TurnedOff);
        Assert.Equal(SwitchPhase.TurningOff, s.Phase);
    }

    [Fact]
    public void Begin_does_nothing_when_the_original_is_not_loaded()
    {
        var s = new BossModSwitch();

        Assert.Empty(s.Begin(View(off: false), T0));
        Assert.False(s.TurnedOff);
        Assert.Equal(SwitchOutcome.Idle, s.Tick(View(off: false), T0).Outcome);
    }

    [Fact]
    public void Waits_while_the_original_is_still_loaded()
    {
        var s = new BossModSwitch();
        s.Begin(View(), T0);

        var step = s.Tick(View(), T0.AddSeconds(1));

        Assert.Equal(SwitchOutcome.Waiting, step.Outcome);
        Assert.Empty(step.Commands);
    }

    [Fact]
    public void Ready_without_a_reload_when_Reborn_still_answers()
    {
        var s = new BossModSwitch();
        s.Begin(View(), T0);

        // The shared IPC is judged only a moment after the original reports unloaded.
        Assert.Equal(SwitchOutcome.Waiting, s.Tick(View(off: false), T0.AddSeconds(1)).Outcome);
        var step = s.Tick(View(off: false), T0.AddSeconds(2));

        Assert.Equal(SwitchOutcome.Ready, step.Outcome);
        Assert.False(s.Reloaded);
        Assert.True(s.TurnedOff);
    }

    [Fact]
    public void Reloads_Reborn_when_the_shared_IPC_went_with_the_original()
    {
        var s = new BossModSwitch();
        s.Begin(View(), T0);

        Assert.Empty(s.Tick(View(off: false, ipc: false), T0.AddSeconds(1)).Commands);
        var off = s.Tick(View(off: false, ipc: false), T0.AddSeconds(2));
        Assert.Equal(["/xldisableplugintemp BossModReborn"], off.Commands);
        Assert.Equal(SwitchOutcome.Waiting, s.Tick(View(off: false, ipc: false), T0.AddSeconds(3)).Outcome);

        var on = s.Tick(View(off: false, kept: false, ipc: false), T0.AddSeconds(4));
        Assert.Equal(["/xlenableplugintemp BossModReborn"], on.Commands);
        Assert.Equal(SwitchOutcome.Waiting, s.Tick(View(off: false, ipc: false), T0.AddSeconds(5)).Outcome);

        Assert.Equal(SwitchOutcome.Ready, s.Tick(View(off: false), T0.AddSeconds(6)).Outcome);
        Assert.True(s.Reloaded);
    }

    [Fact]
    public void Gives_up_after_the_timeout_and_still_owes_a_restore()
    {
        var s = new BossModSwitch();
        s.Begin(View(), T0);

        var step = s.Tick(View(), T0 + BossModSwitch.Timeout);

        Assert.Equal(SwitchOutcome.Failed, step.Outcome);
        Assert.Contains("collection", step.Error);
        Assert.Equal(SwitchPhase.Idle, s.Phase);
        Assert.True(s.TurnedOff);
    }

    [Fact]
    public void Restore_turns_the_original_back_on()
    {
        var s = new BossModSwitch();
        s.Begin(View(), T0);
        s.Tick(View(off: false), T0.AddSeconds(1));

        Assert.Equal(["/xlenableplugintemp BossMod"], s.Restore(View(off: false)));
        Assert.False(s.TurnedOff);
    }

    [Fact]
    public void Restore_leaves_a_plugin_the_player_turned_back_on_or_removed()
    {
        var s = new BossModSwitch(turnedOff: true);
        Assert.Empty(s.Restore(View(off: true)));

        var removed = new BossModSwitch(turnedOff: true);
        Assert.Empty(removed.Restore(View(off: false, installed: false)));
    }

    [Fact]
    public void Restore_mid_reload_brings_Reborn_back_as_well()
    {
        var s = new BossModSwitch();
        s.Begin(View(), T0);
        s.Tick(View(off: false, ipc: false), T0.AddSeconds(1));
        s.Tick(View(off: false, ipc: false), T0.AddSeconds(2));

        Assert.Equal(["/xlenableplugintemp BossModReborn", "/xlenableplugintemp BossMod"], s.Restore(View(off: false, kept: false, ipc: false)));
    }

    [Fact]
    public void Nothing_to_restore_when_nothing_was_turned_off() =>
        Assert.Empty(new BossModSwitch().Restore(View(off: false)));

    [Fact]
    public void For_the_original_BossMod_it_turns_Reborn_off_and_reloads_the_original()
    {
        var s = new BossModSwitch(BossModSwitch.Reborn);
        Assert.Equal(BossModSwitch.Original, s.Kept);

        Assert.Equal(["/xldisableplugintemp BossModReborn"], s.Begin(View(), T0));
        Assert.Empty(s.Tick(View(off: false, ipc: false), T0.AddSeconds(1)).Commands);
        Assert.Equal(["/xldisableplugintemp BossMod"], s.Tick(View(off: false, ipc: false), T0.AddSeconds(2)).Commands);
        Assert.Equal(["/xlenableplugintemp BossMod"], s.Tick(View(off: false, kept: false, ipc: false), T0.AddSeconds(3)).Commands);
        Assert.Equal(SwitchOutcome.Ready, s.Tick(View(off: false), T0.AddSeconds(4)).Outcome);
        Assert.True(s.Reloaded);

        Assert.Equal(["/xlenableplugintemp BossModReborn"], s.Restore(View(off: false)));
    }
}
