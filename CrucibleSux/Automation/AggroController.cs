using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrucibleSux.Core;
using CrucibleSux.Diagnostics;
using CrucibleSux.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Automation;

/// <summary>
/// In a board's battles, shares the hits between the character and the familiar with Snarl and Challenge, by HP and by
/// the score still reachable (<see cref="AggroAdvisor"/>, docs/research/16-tactics.md). It leaves aggro to Rotation
/// Solver Reborn while an enemy has the mechanic its Beastmaster rotation Snarls for.
/// </summary>
public sealed class AggroController
{
    /// <summary>The First Board's first-battle mechanic (likely the Bone Knight's Forward Guard) that RSR Snarls for.</summary>
    public const uint RotationSnarlsFor = 2552;

    private const float Range = 25f;
    private static readonly TimeSpan TickEvery = TimeSpan.FromMilliseconds(250);

    private readonly Configuration config;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<BattleInfo>> battles;
    private readonly IReadOnlyDictionary<uint, EnemyAbility> intel;
    private readonly IClientState client;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly ITargetManager targets;
    private readonly GameActions game;
    private readonly CombatRecorder recorder;
    private readonly AggroAdvisor advisor = new();
    private readonly HashSet<ulong> knockedOut = [];

    private DateTime nextTick;
    private bool fighting;
    private ulong beastId;
    private uint territory;
    private bool leftToRotation;

    public AggroController(Configuration config, CrucibleData data, IClientState client, ICondition condition, IObjectTable objects,
        ITargetManager targets, GameActions game, CombatRecorder recorder)
    {
        this.config = config;
        this.battles = data.Battles;
        this.intel = data.Battles.Values.SelectMany(b => b).SelectMany(b => b.Enemies).SelectMany(e => e.Abilities)
            .DistinctBy(a => a.ActionId).ToDictionary(a => a.ActionId);
        this.client = client;
        this.condition = condition;
        this.objects = objects;
        this.targets = targets;
        this.game = game;
        this.recorder = recorder;
    }

    public void Update()
    {
        var now = DateTime.UtcNow;
        if (now < this.nextTick)
            return;
        this.nextTick = now + TickEvery;

        // KOs count for the whole board; each battle starts its damage meter afresh.
        if (this.client.TerritoryType != this.territory)
        {
            this.territory = this.client.TerritoryType;
            this.knockedOut.Clear();
        }
        bool combat = this.condition[ConditionFlag.InCombat];
        if (combat && !this.fighting)
        {
            this.advisor.Reset();
            this.leftToRotation = false;
        }
        this.fighting = combat;
        if (!this.config.ShareAggro || !combat || Boards.ByTerritory(this.territory) is not { } board || this.objects.LocalPlayer is not { } me)
            return;

        IBattleChara? beast = null;
        foreach (var o in this.objects)
        {
            if (o.OwnerId != me.EntityId || o is not IBattleChara b || o.GameObjectId == me.GameObjectId)
                continue;
            if (b.IsDead || b.CurrentHp == 0)
                this.knockedOut.Add(b.GameObjectId);
            else
                beast ??= b;
        }
        if (beast is null)
            return;
        if (beast.GameObjectId != this.beastId)
        {
            this.beastId = beast.GameObjectId;
            this.advisor.Meter.ResetBeast();
        }

        var enemies = this.objects.OfType<IBattleNpc>()
            .Where(o => GameActions.IsHostile(o) && !o.IsDead && o.CurrentHp > 0 && Vector3.Distance(o.Position, me.Position) <= Range)
            .ToList();
        if (enemies.Count == 0)
            return;
        if (enemies.Any(e => GameActions.HasStatus(e, RotationSnarlsFor)))
        {
            if (!this.leftToRotation)
                this.recorder.Note(LogKind.Beast, $"Aggro left to the rotation plugin: an enemy has status {RotationSnarlsFor}, which its Beastmaster rotation Snarls for");
            this.leftToRotation = true;
            return;
        }

        // The familiar tanks while it covers the character or an enemy attacks it.
        Actor? holder = GameActions.HasStatus(me, GameActions.Covered) || enemies.Any(e => e.TargetObjectId == beast.GameObjectId) ? Actor.Beast
            : enemies.Any(e => e.TargetObjectId == me.GameObjectId) ? Actor.Player
            : null;
        double playerHp = Pct(me);
        double beastHp = Pct(beast);
        this.advisor.Meter.Sample(Actor.Player, now, playerHp, holder == Actor.Player);
        this.advisor.Meter.Sample(Actor.Beast, now, beastHp, holder == Actor.Beast);

        // The enemy aimed at the actor giving up aggro, else the current target.
        ulong holderId = holder == Actor.Beast ? beast.GameObjectId : me.GameObjectId;
        var target = enemies.FirstOrDefault(e => e.TargetObjectId == holderId)
            ?? enemies.FirstOrDefault(e => e.GameObjectId == this.targets.Target?.GameObjectId);
        if (target is null)
            return;
        bool ready = this.game.CanUse(GameActions.Snarl, target.GameObjectId) || this.game.CanUse(GameActions.Challenge, target.GameObjectId);

        var cast = enemies
            .Where(e => e.IsCasting && this.intel.TryGetValue(e.CastActionId, out var a)
                && (a.Target == IntelTarget.HighestEnmity || (a.Shape == IntelShape.SingleTarget && a.Target is not (IntelTarget.Self or IntelTarget.Allies))))
            .Select(e => new PendingCast(this.intel[e.CastActionId].Name, e.TotalCastTime - e.CurrentCastTime))
            .MinBy(c => c.SecondsLeft);
        var present = enemies.Select(e => e.NameId).ToHashSet();
        bool final = this.battles.TryGetValue(board.Number, out var list) && list.Any(b => b.IsBoss && b.Enemies.Count > 0 && present.Contains(b.Enemies[0].NameId));
        bool recovery = this.config.RunGoal == RunGoal.Clear && !final;
        // Item use is not tracked yet; the aggro choice does not depend on Economic Ethos.
        var outlook = ScoreOutlook.Of(this.knockedOut.Count, beastHp < 100, playerHp < 100, itemUsed: false, recovery);

        var d = this.advisor.Decide(new AggroView(now, playerHp, beastHp, holder, ready, final, recovery, outlook, cast,
            this.config.PlayerFloor, this.config.BeastFloor));
        if (d.Action == AggroAction.None)
            return;
        uint action = d.Action == AggroAction.Snarl ? GameActions.Snarl : GameActions.Challenge;
        bool used = this.game.UseAction(action, target.GameObjectId);
        this.recorder.Note(used ? LogKind.Beast : LogKind.Warn, $"{(used ? string.Empty : "Could not use ")}{d.Action} on {target.Name.TextValue}: {d.Reason}");
    }

    private static double Pct(IBattleChara c) => c.MaxHp == 0 ? 0 : 100.0 * c.CurrentHp / c.MaxHp;
}
