using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrucibleSux.Core;
using CrucibleSux.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Automation;

/// <summary>
/// In a board's battles, targets the enemy the game's own enemy intel says dies first (<see cref="TargetPriority"/>): the
/// Pas de Seul's mage before the boss, the Ogre's wisps before the ogre. It only steps in when such an enemy is up and the
/// current target ranks lower; otherwise BossMod and the rotation plugin pick as usual. When something keeps switching
/// the target back, it backs off for a while instead of fighting it.
/// </summary>
public sealed class TargetPicker(
    Configuration config,
    IReadOnlyDictionary<uint, int> priorities,
    IClientState client,
    ICondition condition,
    IObjectTable objects,
    ITargetManager targets,
    CombatRecorder recorder)
{
    private const float Range = 40f;
    private const int FlipLimit = 4;
    private static readonly TimeSpan TickEvery = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan FlipWindow = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan BackOff = TimeSpan.FromSeconds(30);

    private readonly Queue<DateTime> flips = new();
    private ulong lastPicked;
    private DateTime nextTick;
    private DateTime pausedUntil;

    public void Update()
    {
        var now = DateTime.UtcNow;
        if (now < this.nextTick)
            return;
        this.nextTick = now + TickEvery;
        if (!config.PickTargets || now < this.pausedUntil || !condition[ConditionFlag.InCombat] || Boards.ByTerritory(client.TerritoryType) is null)
            return;
        if (objects.LocalPlayer is not { } me)
            return;

        var best = objects.OfType<IBattleNpc>()
            .Where(o => GameActions.IsHostile(o) && !o.IsDead && o.CurrentHp > 0 && o.IsTargetable && Vector3.Distance(o.Position, me.Position) <= Range)
            .Select(o => (Npc: o, Priority: priorities.GetValueOrDefault(o.NameId), Distance: Vector3.Distance(o.Position, me.Position)))
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Distance)
            .FirstOrDefault();
        // Nothing ranks above the rest: targeting stays with BossMod and the rotation plugin.
        if (best.Npc is null || best.Priority == 0)
            return;

        var current = targets.Target;
        int currentPriority = current is IBattleNpc n && GameActions.IsHostile(n) && !n.IsDead && n.CurrentHp > 0 ? priorities.GetValueOrDefault(n.NameId) : -1;
        if (current?.GameObjectId == best.Npc.GameObjectId || currentPriority >= best.Priority)
            return;

        // Picking the same enemy again means something switched away from it since; new adds do not count.
        while (this.flips.Count > 0 && now - this.flips.Peek() > FlipWindow)
            this.flips.Dequeue();
        if (best.Npc.GameObjectId == this.lastPicked)
        {
            if (this.flips.Count >= FlipLimit)
            {
                this.flips.Clear();
                this.pausedUntil = now + BackOff;
                recorder.Note(LogKind.Warn, $"Target picking paused for {BackOff.TotalSeconds:0}s: something else switched off {best.Npc.Name.TextValue} {FlipLimit} times in {FlipWindow.TotalSeconds:0}s.");
                return;
            }
            this.flips.Enqueue(now);
        }
        this.lastPicked = best.Npc.GameObjectId;
        targets.Target = best.Npc;
        recorder.Note(LogKind.Target, $"Crucible SUX targets {best.Npc.Name.TextValue} (priority {best.Priority}) instead of {current?.Name.TextValue ?? "nothing"}");
    }
}
