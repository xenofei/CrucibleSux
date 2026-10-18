using System;
using System.Collections.Generic;
using System.Linq;
using CrucibleSux.Core;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Automation;

/// <summary>
/// Starts a session or the journey only once the player has said which BossMod they use and the other one is off: asks
/// (which one, once; turning the other off, unless Settings allows it), turns the other one off through
/// <see cref="BossModSwitch"/>, and turns it back on once nothing runs any more.
/// </summary>
public sealed class BossModGate : IDisposable
{
    /// <summary>A name both BossMods register; it answers only while one of them has it registered.</summary>
    private const string SharedIpc = "BossMod.HasModuleByDataId";

    private static readonly TimeSpan TickEvery = TimeSpan.FromMilliseconds(250);

    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly Action save;
    private BossModSwitch @switch;

    private Func<bool>? ready;
    private Action? pending;
    private string what = string.Empty;
    private DateTime nextTick;

    public BossModGate(IDalamudPluginInterface pi, ICommandManager commands, IChatGui chat, IPluginLog log, Configuration config, Action save)
    {
        this.pi = pi;
        this.commands = commands;
        this.chat = chat;
        this.log = log;
        this.config = config;
        this.save = save;
        // Crucible SUX was reloaded while a BossMod was off: it is turned back on at the first idle tick. 0.2.3 turned
        // only the original off and did not record which.
        this.@switch = config.BossModTurnedOffInProcess == Environment.ProcessId
            ? new BossModSwitch(config.BossModTurnedOff ?? BossModSwitch.Original, turnedOff: true)
            : new BossModSwitch(BossModSwitch.OffFor(config.MechanicsPlugin));
    }

    /// <summary>What waits for the player's answer ("the session", "the journey"), or null.</summary>
    public string? Asking { get; private set; }

    /// <summary>The question is which BossMod the player uses (<see cref="Configuration.MechanicsPlugin"/> is unset).</summary>
    public bool Choosing { get; private set; }

    /// <summary>The BossMod the switch turns off: the one the player does not use.</summary>
    public string Off => this.@switch.Off;

    /// <summary>The BossMod the player uses.</summary>
    public string Kept => this.@switch.Kept;

    /// <summary><see cref="Off"/> is off because Crucible SUX turned it off.</summary>
    public bool TurnedOff => this.@switch.TurnedOff;

    public bool Reloaded => this.@switch.Reloaded;

    public void Dispose()
    {
        this.pending = null;
        this.Asking = null;
        if (this.@switch.TurnedOff)
            this.Restore();
    }

    /// <summary>
    /// Runs <paramref name="start"/> once the player has said which BossMod they use (asked when unset), <paramref name="ready"/>
    /// allows it, and the other BossMod is off. Turning it off is asked first, unless <see cref="Configuration.TurnOffBossMod"/>
    /// or the player has just answered which one they use. <paramref name="ready"/> tells the player itself why not.
    /// </summary>
    public void Run(string what, Func<bool> ready, Action start)
    {
        if (this.Asking is not null || this.@switch.Phase != SwitchPhase.Idle)
        {
            this.chat.PrintError("[Crucible SUX] Still busy with BossMod: answer the question or wait a moment.");
            return;
        }
        this.what = what;
        this.ready = ready;
        this.pending = start;
        if (this.config.MechanicsPlugin == BossModChoice.Unset)
        {
            this.Choosing = true;
            this.Asking = what;
            return;
        }
        this.Continue(consented: this.config.TurnOffBossMod);
    }

    /// <summary>The answer to "Which BossMod do you use?"; it also says yes to turning the other one off.</summary>
    public void Choose(BossModChoice choice, bool remember)
    {
        this.Asking = null;
        this.Choosing = false;
        this.config.MechanicsPlugin = choice;
        if (remember)
            this.config.TurnOffBossMod = true;
        this.save();
        this.log.Information($"BossMod in use: {choice}");
        this.Continue(consented: true);
    }

    public void Accept(bool remember)
    {
        this.Asking = null;
        if (remember)
        {
            this.config.TurnOffBossMod = true;
            this.save();
        }
        this.TurnOff();
    }

    public void Decline()
    {
        if (this.Asking is null)
            return;
        this.Asking = null;
        this.pending = null;
        this.chat.Print(this.Choosing
            ? $"[Crucible SUX] Not started: {this.what} needs to know which BossMod you use."
            : $"[Crucible SUX] Not started: {BossModSwitch.Name(this.Off)} is still loaded, and {this.what} needs it off.");
        this.Choosing = false;
    }

    /// <summary>Called every framework tick. <paramref name="idle"/> says nothing runs, so BossMod can come back on.</summary>
    public void Update(Func<bool> idle)
    {
        var now = DateTime.UtcNow;
        if (now < this.nextTick)
            return;
        this.nextTick = now + TickEvery;

        var step = this.@switch.Tick(this.View(), now);
        this.Send(step.Commands);
        switch (step.Outcome)
        {
            case SwitchOutcome.Ready:
                this.log.Information($"Turned {BossModSwitch.Name(this.Off)} off{(this.Reloaded ? $"; {BossModSwitch.Name(this.Kept)} was reloaded" : string.Empty)}");
                this.RunPending();
                break;
            case SwitchOutcome.Failed:
                this.pending = null;
                this.chat.PrintError($"[Crucible SUX] {step.Error}");
                break;
        }

        if (this.@switch.TurnedOff && this.@switch.Phase == SwitchPhase.Idle && this.Asking is null && this.pending is null && idle())
            this.Restore();
    }

    /// <summary>"loaded", "installed, disabled" or "not installed", for the question.</summary>
    public string State(string internalName)
    {
        try
        {
            var found = this.pi.InstalledPlugins.Where(p => p.InternalName == internalName).ToList();
            return found.Any(p => p.IsLoaded) ? "loaded" : found.Count > 0 ? "installed, disabled" : "not installed";
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    private void Continue(bool consented)
    {
        if (this.ready?.Invoke() != true)
        {
            this.pending = null;
            return;
        }
        // A switch that still owes a restore keeps its direction until then.
        if (!this.@switch.TurnedOff)
            this.@switch = new BossModSwitch(BossModSwitch.OffFor(this.config.MechanicsPlugin));
        if (!this.View().OffLoaded)
            this.RunPending();
        else if (consented)
            this.TurnOff();
        else
            this.Asking = this.what;
    }

    private void TurnOff()
    {
        var sent = this.@switch.Begin(this.View(), DateTime.UtcNow);
        if (sent.Count == 0)
        {
            // Already off (the player turned it off while the question was open).
            this.RunPending();
            return;
        }
        this.config.BossModTurnedOffInProcess = Environment.ProcessId;
        this.config.BossModTurnedOff = this.Off;
        this.save();
        this.chat.Print($"[Crucible SUX] Turning {BossModSwitch.Name(this.Off)} off for {this.what}; it comes back on when {this.what} ends.");
        this.Send(sent);
    }

    private void RunPending()
    {
        var start = this.pending;
        this.pending = null;
        start?.Invoke();
    }

    private void Restore()
    {
        var sent = this.@switch.Restore(this.View());
        this.config.BossModTurnedOffInProcess = 0;
        this.config.BossModTurnedOff = null;
        this.save();
        if (sent.Any(c => c == BossModSwitch.Enable(this.Off)))
            this.chat.Print($"[Crucible SUX] Turning {BossModSwitch.Name(this.Off)} back on.");
        this.Send(sent);
    }

    private SwitchView View()
    {
        bool offInstalled = false, offLoaded = false, keptLoaded = false;
        try
        {
            foreach (var p in this.pi.InstalledPlugins)
            {
                if (p.InternalName == this.Off)
                {
                    offInstalled = true;
                    offLoaded |= p.IsLoaded;
                }
                else if (p.InternalName == this.Kept)
                {
                    keptLoaded |= p.IsLoaded;
                }
            }
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, "The installed plugin list is unavailable");
        }

        bool ipcReady;
        try
        {
            ipcReady = this.pi.GetIpcSubscriber<uint, bool>(SharedIpc).HasFunction;
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"{SharedIpc} could not be checked");
            ipcReady = false;
        }
        return new SwitchView(offInstalled, offLoaded, keptLoaded, ipcReady);
    }

    private void Send(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            this.log.Information($"Sending {line}");
            if (!this.commands.ProcessCommand(line))
                this.chat.PrintError($"[Crucible SUX] Dalamud did not accept \"{line}\".");
        }
    }
}
