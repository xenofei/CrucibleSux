using System;
using System.Collections.Generic;
using System.Linq;
using CrucibleSux.Core;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Automation;

/// <summary>
/// Starts a session or the journey only once the original BossMod is off: asks the player (unless Settings allows it),
/// turns it off through <see cref="BossModSwitch"/>, and turns it back on once nothing runs any more.
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
    private readonly BossModSwitch @switch;

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
        // Crucible SUX was reloaded while BossMod was off: it is turned back on at the first idle tick.
        this.@switch = new BossModSwitch(config.BossModTurnedOffInProcess == Environment.ProcessId);
    }

    /// <summary>What waits for the player's answer ("the session", "the journey"), or null.</summary>
    public string? Asking { get; private set; }

    /// <summary>The original BossMod is off because Crucible SUX turned it off.</summary>
    public bool TurnedOff => this.@switch.TurnedOff;

    public bool RebornReloaded => this.@switch.RebornReloaded;

    public void Dispose()
    {
        this.pending = null;
        this.Asking = null;
        if (this.@switch.TurnedOff)
            this.Restore();
    }

    /// <summary>
    /// Runs <paramref name="start"/> now when the original BossMod is not loaded. Otherwise asks the player first (or not,
    /// with <see cref="Configuration.TurnOffBossMod"/>), turns it off and runs <paramref name="start"/> once it is.
    /// </summary>
    public void Run(string what, Action start)
    {
        if (this.Asking is not null || this.@switch.Phase != SwitchPhase.Idle)
        {
            this.chat.PrintError("[Crucible SUX] Still busy with the original BossMod: answer the question or wait a moment.");
            return;
        }
        if (!this.View().OriginalLoaded)
        {
            start();
            return;
        }
        this.pending = start;
        this.what = what;
        if (this.config.TurnOffBossMod)
            this.Accept(remember: false);
        else
            this.Asking = what;
    }

    public void Accept(bool remember)
    {
        this.Asking = null;
        if (remember)
            this.config.TurnOffBossMod = true;
        var sent = this.@switch.Begin(this.View(), DateTime.UtcNow);
        if (sent.Count == 0)
        {
            // Already off (the player turned it off while the question was open).
            this.save();
            this.RunPending();
            return;
        }
        this.config.BossModTurnedOffInProcess = Environment.ProcessId;
        this.save();
        this.chat.Print($"[Crucible SUX] Turning the original BossMod off for {this.what}; it comes back on when {this.what} ends.");
        this.Send(sent);
    }

    public void Decline()
    {
        if (this.Asking is null)
            return;
        this.Asking = null;
        this.pending = null;
        this.chat.Print($"[Crucible SUX] Not started: the original BossMod is still loaded, and {this.what} needs it off.");
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
                this.log.Information($"The original BossMod is off{(this.@switch.RebornReloaded ? "; BossMod Reborn was reloaded" : string.Empty)}");
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
        this.save();
        if (sent.Any(c => c == BossModSwitch.Enable(BossModSwitch.Original)))
            this.chat.Print("[Crucible SUX] Turning the original BossMod back on.");
        this.Send(sent);
    }

    private SwitchView View()
    {
        bool originalInstalled = false, originalLoaded = false, rebornLoaded = false;
        try
        {
            foreach (var p in this.pi.InstalledPlugins)
            {
                if (p.InternalName == BossModSwitch.Original)
                {
                    originalInstalled = true;
                    originalLoaded |= p.IsLoaded;
                }
                else if (p.InternalName == BossModSwitch.Reborn)
                {
                    rebornLoaded |= p.IsLoaded;
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
        return new SwitchView(originalInstalled, originalLoaded, rebornLoaded, ipcReady);
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
