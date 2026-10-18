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
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace CrucibleSux.Automation;

/// <summary>
/// In a board's battles, shares the hits between the character and the familiar with Snarl and Challenge, by HP and by
/// the score still reachable (<see cref="AggroAdvisor"/>, docs/research/16-tactics.md). It leaves aggro to Rotation
/// Solver Reborn while an enemy has the mechanic its Beastmaster rotation Snarls for. It also drinks the Beast Potions
/// <see cref="HealAdvisor"/> picks: AutoDuty heals only from the character's HP, which barely drops while a familiar
/// covers it, and never heals a familiar.
/// </summary>
public sealed unsafe class AggroController
{
    /// <summary>The First Board's first-battle mechanic (likely the Bone Knight's Forward Guard) that RSR Snarls for.</summary>
    public const uint RotationSnarlsFor = 2552;

    private const float Range = 25f;
    private static readonly TimeSpan TickEvery = TimeSpan.FromMilliseconds(250);

    /// <summary>Between two potions, so the first one's HP shows before the next is chosen (AutoDuty waits as long).</summary>
    private static readonly TimeSpan ItemGap = TimeSpan.FromSeconds(5);

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
    private DateTime nextItem;
    private int heldItems = -1;
    private bool itemUsed;
    private ulong? restoreTarget;
    private bool restorePending;
    private bool wasCooling;
    private DateTime ownUse;

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

        // A potion goes to the current target, which is put back one tick later if it still exists.
        if (this.restorePending)
        {
            this.restorePending = false;
            var back = this.restoreTarget is { } id ? this.objects.SearchById(id) : null;
            this.targets.Target = back is ICharacter { IsDead: true } ? null : back;
        }

        // KOs and item use count for the whole board; each battle starts its damage meter afresh.
        if (this.client.TerritoryType != this.territory)
        {
            this.territory = this.client.TerritoryType;
            this.knockedOut.Clear();
            this.heldItems = -1;
            this.itemUsed = false;
        }
        var director = Director();
        var potions = new List<HeldPotion>();
        if (director != null)
        {
            int held = 0;
            for (int i = 0; i < director->Inventory.Length; i++)
            {
                uint id = director->Inventory[i].ItemId;
                if (id == 0)
                    continue;
                held++;
                if (HealAdvisor.Potions.TryGetValue(id, out int pct))
                    potions.Add(new HeldPotion(i, pct));
            }
            // An item gone from the bag was used, by Crucible SUX or by AutoDuty: Economic Ethos is lost either way, and
            // the next potion waits so two plugins never drink for the same hit.
            if (this.heldItems >= 0 && held < this.heldItems)
            {
                this.itemUsed = true;
                this.nextItem = Max(this.nextItem, now + ItemGap);
            }
            this.heldItems = held;
        }
        bool combat = this.condition[ConditionFlag.InCombat];
        if (combat && !this.fighting)
        {
            this.advisor.Reset();
            this.leftToRotation = false;
        }
        this.fighting = combat;
        if (!(this.config.ShareAggro || this.config.UseItems) || !combat || Boards.ByTerritory(this.territory) is not { } board
            || this.objects.LocalPlayer is not { } me)
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
        if (beast is not null && beast.GameObjectId != this.beastId)
        {
            this.beastId = beast.GameObjectId;
            this.advisor.Meter.ResetBeast();
        }

        var enemies = this.objects.OfType<IBattleNpc>()
            .Where(o => GameActions.IsHostile(o) && !o.IsDead && o.CurrentHp > 0 && Vector3.Distance(o.Position, me.Position) <= Range)
            .ToList();
        bool rotationOwns = enemies.Any(e => GameActions.HasStatus(e, RotationSnarlsFor));
        if (rotationOwns && !this.leftToRotation)
            this.recorder.Note(LogKind.Beast, $"Aggro left to the rotation plugin: an enemy has status {RotationSnarlsFor}, which its Beastmaster rotation Snarls for");
        this.leftToRotation |= rotationOwns;

        // Who takes hits, per actor: the familiar while it covers the character or an enemy attacks it; the character
        // while an enemy attacks it and no Cover takes the hits.
        bool covered = GameActions.HasStatus(me, GameActions.Covered);
        bool beastTanking = beast is not null && (covered || enemies.Any(e => e.TargetObjectId == beast.GameObjectId));
        bool playerTanking = !covered && enemies.Any(e => e.TargetObjectId == me.GameObjectId);
        Actor? holder = beastTanking ? Actor.Beast : playerTanking ? Actor.Player : null;
        double playerHp = Pct(me);
        double? beastHp = beast is null ? null : Pct(beast);
        this.advisor.Meter.Sample(Actor.Player, now, playerHp, playerTanking);
        if (beastHp is { } sampled)
            this.advisor.Meter.Sample(Actor.Beast, now, sampled, beastTanking);

        // Snarl and Challenge share one cooldown; when it starts without us, another plugin handed off.
        bool cooling = !this.game.OffCooldown(GameActions.Snarl);
        if (cooling && !this.wasCooling && now - this.ownUse > TimeSpan.FromSeconds(1))
        {
            this.advisor.NoteHandoff(now);
            this.recorder.Note(LogKind.Beast, "Another plugin used Snarl or Challenge; waiting a window before handing off again");
        }
        this.wasCooling = cooling;

        var present = enemies.Select(e => e.NameId).ToHashSet();
        uint boss = this.battles.TryGetValue(board.Number, out var list)
            ? list.Where(b => b.IsBoss && b.Enemies.Count > 0 && present.Contains(b.Enemies[0].NameId)).Select(b => b.Enemies[0].NameId).FirstOrDefault()
            : 0;
        bool final = boss != 0;
        bool recovery = this.config.RunGoal == RunGoal.Clear && !final;
        // Habitual Hoarder (holding 10 crucible items) counts from the Second Board on; XBMContent gives it 0 on the First.
        int hoarder = board.Number >= 2 && this.heldItems == 10 ? ScoreOutlook.HabitualHoarderPoints : 0;
        var outlook = ScoreOutlook.Of(this.knockedOut.Count, BenchHurt(beast), playerHp < 100, this.itemUsed, recovery, hoarder);

        // The enemy aimed at the actor giving up aggro, else the current target.
        ulong holderId = holder == Actor.Beast && beast is not null ? beast.GameObjectId : me.GameObjectId;
        var target = enemies.FirstOrDefault(e => e.TargetObjectId == holderId)
            ?? enemies.FirstOrDefault(e => e.GameObjectId == this.targets.Target?.GameObjectId);
        if (this.config.ShareAggro && !rotationOwns && target is not null)
        {
            var cast = enemies
                .Where(e => e.IsCasting && this.intel.TryGetValue(e.CastActionId, out var a)
                    && (a.Target == IntelTarget.HighestEnmity || (a.Shape == IntelShape.SingleTarget && a.Target is not (IntelTarget.Self or IntelTarget.Allies))))
                .Select(e => new PendingCast(this.intel[e.CastActionId].Name, e.TotalCastTime - e.CurrentCastTime))
                .MinBy(c => c.SecondsLeft);
            var d = this.advisor.Decide(new AggroView(now, playerHp, beastHp, holder, !cooling, final, recovery, outlook, cast,
                this.config.PlayerFloor, this.config.BeastFloor, this.config.UseItems ? potions.Sum(p => p.Percent) : 0));
            uint action = d.Action == AggroAction.Snarl ? GameActions.Snarl : GameActions.Challenge;
            // Out of range or sight, the chosen action waits; potions below may still be needed meanwhile.
            if (d.Action != AggroAction.None && this.game.CanUse(action, target.GameObjectId) && this.game.UseAction(action, target.GameObjectId))
            {
                this.ownUse = now;
                this.recorder.Note(LogKind.Beast, $"{d.Action} on {target.Name.TextValue}: {d.Reason}");
                return;
            }
        }

        // Potions run whatever the familiar and the enemies are doing: a character alone at low HP needs them most.
        if (!this.config.UseItems || director == null || now < this.nextItem)
            return;
        bool playerSafe = playerHp - this.advisor.Meter.ProjectedLoss(Actor.Player) >= this.config.PlayerFloor;
        double? bossHp = final ? enemies.Where(e => e.NameId == boss).Select(e => (double?)Pct(e)).FirstOrDefault() : null;
        var heal = HealAdvisor.Decide(new HealView(potions, playerHp, beastHp,
            beastTanking ? this.advisor.Meter.ProjectedLoss(Actor.Beast) : 0,
            HandoffCanSave: this.config.ShareAggro && !rotationOwns && !cooling && target is not null && playerSafe,
            final, bossHp, outlook, this.config.PlayerFloor, this.config.BeastFloor));
        if (heal is null || (heal.Target == Actor.Beast && beast is null))
            return;
        // Whether it worked shows as the item leaving the bag on a later tick.
        this.nextItem = now + ItemGap;
        this.restoreTarget = this.targets.Target?.GameObjectId;
        this.restorePending = true;
        this.targets.SoftTarget = null;
        this.targets.Target = heal.Target == Actor.Beast ? beast : me;
        director->UseItem((uint)heal.Potion.Slot, 0);
        string who = heal.Target == Actor.Beast ? beast!.Name.TextValue : "you";
        this.recorder.Note(LogKind.Beast, Inv($"Crucible SUX drinks a {heal.Potion.Percent}% Beast Potion for {who} (you {playerHp:0}%, familiar {beastHp ?? 0:0}%): {heal.Reason}"));
    }

    /// <summary>The board's director, or null outside the Crucible.</summary>
    private static InstanceContentCrucibleOfTheUnbroken* Director()
    {
        var ef = EventFramework.Instance();
        var d = ef == null ? null : ef->GetInstanceContentDirector();
        return d != null && d->InstanceContentType == InstanceContentType.CrucibleOfTheUnbroken ? (InstanceContentCrucibleOfTheUnbroken*)d : null;
    }

    /// <summary>
    /// A team beast other than the familiar out is below full HP, from the board's team list (all 15 slots). The familiar
    /// out is the first slot whose HP matches it; which slot it is, is not read anywhere else.
    /// </summary>
    private static bool BenchHurt(IBattleChara? beast)
    {
        var agent = AgentXBMPetParty.Instance();
        if (agent == null)
            return false;
        bool skipped = beast is null;
        for (int i = 0; i < agent->PetMaxHealth.Length; i++)
        {
            uint max = agent->PetMaxHealth[i], hp = agent->PetCurrentHealth[i];
            if (!skipped && max == beast!.MaxHp && hp == beast.CurrentHp)
            {
                skipped = true;
                continue;
            }
            if (max > 0 && hp < max)
                return true;
        }
        return false;
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static string Inv(FormattableString s) => s.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static double Pct(IBattleChara c) => c.MaxHp == 0 ? 0 : 100.0 * c.CurrentHp / c.MaxHp;
}
