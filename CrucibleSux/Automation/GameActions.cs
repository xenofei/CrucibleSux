using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrucibleSux.Core;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace CrucibleSux.Automation;

/// <summary>
/// The game calls the progression runner makes (docs/research/10-quest-engine-apis.md). Framework thread only. None of
/// these has been run in game yet: each returns whether the client accepted the call, and the callers check the outcome
/// (a quest sequence, a status, an inventory count) rather than trusting the return value.
/// </summary>
public sealed unsafe class GameActions(IObjectTable objects, ICondition condition, IGameGui gameGui, IPlayerState player)
{
    public const uint MountRoulette = 9;
    public const uint Sprint = 4;
    public const uint FlyingMountRoulette = 24;
    public const uint Dismount = 23;
    public const uint Capture = 44880;
    public const uint TemperedRelease = 44890;
    public const uint Borrow = 44895;
    public const uint OneWithNature = 4601;
    public const uint InterestCaptured = 4626;

    /// <summary>Snarl (the familiar takes aggro and covers you) and Challenge (you take it back) share one 15 s cooldown.</summary>
    public const uint Snarl = 46751;
    public const uint Challenge = 46750;

    /// <summary>On the character while the familiar's Cover (2412) protects it.</summary>
    public const uint Covered = 2413;

    /// <summary>First, Second and Third Battlehorn: each summons the beast in that slot.</summary>
    public static readonly uint[] Battlehorns = [44881, 44892, 44894];

    /// <summary>The familiars' own BNpcName rows are 14407-14456, in XBMPet order (docs/data/bst-progression.md).</summary>
    public static uint FamiliarName(uint beast) => 14406 + beast;

    public IGameObject? Me => objects.LocalPlayer;

    public bool IsBusy =>
        condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51] || condition[ConditionFlag.Casting]
        || condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene78]
        || condition[ConditionFlag.Jumping];

    public bool InEvent => condition[ConditionFlag.OccupiedInQuestEvent] || condition[ConditionFlag.OccupiedInEvent]
        || condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.Occupied33] || condition[ConditionFlag.Occupied38];

    public bool InCombat => condition[ConditionFlag.InCombat];

    public bool Mounted => condition[ConditionFlag.Mounted];

    /// <summary>Airborne (or swimming under water): vnavmesh must be asked for a flying path, since the start is off the walking mesh.</summary>
    public bool InFlight => condition[ConditionFlag.InFlight] || condition[ConditionFlag.Diving];

    public float DistanceTo(Vector3 p) => this.Me is { } me ? Vector3.Distance(me.Position, p) : float.MaxValue;

    /// <summary>The nearest targetable object with this BaseId (an ENpc, EObj or BNpcBase row).</summary>
    public IGameObject? Nearest(uint baseId) =>
        objects.Where(o => o.BaseId == baseId && o.IsTargetable).OrderBy(o => this.DistanceTo(o.Position)).FirstOrDefault();

    public void Target(IGameObject o)
    {
        var ts = TargetSystem.Instance();
        if (ts != null)
            ts->Target = (GameObjectStruct*)o.Address;
    }

    /// <summary>Starts the object's event, as Questionable and TextAdvance do: target it, then interact without a sight check.</summary>
    public bool Interact(IGameObject o)
    {
        var ts = TargetSystem.Instance();
        if (ts == null)
            return false;
        ts->Target = (GameObjectStruct*)o.Address;
        ulong result = ts->InteractWithObject((GameObjectStruct*)o.Address, false);
        return result is > 0 and not 7;
    }

    public bool CanUse(uint action, ulong target = 0xE0000000) => this.ActionStatus(action, target) == 0;

    /// <summary>0 when the game would accept <paramref name="action"/> on <paramref name="target"/> now, else its refusal (a LogMessage row).</summary>
    public uint ActionStatus(uint action, ulong target = 0xE0000000)
    {
        var am = ActionManager.Instance();
        return am == null ? uint.MaxValue : am->GetActionStatus(ActionType.Action, action, target);
    }

    public bool OffCooldown(uint action)
    {
        var am = ActionManager.Instance();
        return am == null || am->IsActionOffCooldown(ActionType.Action, action);
    }

    public bool UseAction(uint action, ulong target = 0xE0000000)
    {
        var am = ActionManager.Instance();
        return am != null && am->UseAction(ActionType.Action, action, target);
    }

    /// <summary>Action 7, "attack": toggles auto-attack. The game refuses it in this patch; <see cref="EnsureAutoAttack"/> is the way in.</summary>
    public const uint AutoAttack = 7;

    /// <summary>General action 1, the hotbar's "Auto-attack": the same toggle, on the current target.</summary>
    public const uint AutoAttackGeneral = 1;

    /// <summary>Starts auto-attacking <paramref name="target"/> without using a weapon skill; send it only while not in combat.</summary>
    public bool StartAutoAttack(IGameObject target)
    {
        this.Target(target);
        return this.UseAction(AutoAttack, target.GameObjectId);
    }

    /// <summary>Auto-attack is on (read as BossMod does, UIState's WeaponState).</summary>
    public bool IsAutoAttacking
    {
        get
        {
            var ui = UIState.Instance();
            return ui != null && ui->WeaponState.AutoAttackState.IsAutoAttacking;
        }
    }

    /// <summary>
    /// Targets <paramref name="target"/> and turns auto-attack on with the hotbar's Auto-attack, a toggle, so only while it is
    /// off. Without it a character the enemy pulled stands still: the rotation plugin fights but never starts the swings.
    /// </summary>
    public void EnsureAutoAttack(IGameObject target)
    {
        this.Target(target);
        // A toggle: sent again before the swing flag catches up, it would turn the swings back off.
        var now = DateTime.UtcNow;
        if (this.IsAutoAttacking || now < this.nextAutoAttack)
            return;
        this.nextAutoAttack = now + TimeSpan.FromSeconds(1.5);
        var am = ActionManager.Instance();
        uint status = am == null ? uint.MaxValue : am->GetActionStatus(ActionType.GeneralAction, AutoAttackGeneral);
        if (status == 0)
            this.UseGeneral(AutoAttackGeneral);
        if (status != this.autoAttackRefusal)
        {
            this.autoAttackRefusal = status;
            if (status != 0)
                Plugin.Trace($"Journey: the game refuses Auto-attack on {target.Name.TextValue} (status {status})");
        }
    }

    private DateTime nextAutoAttack;
    private uint autoAttackRefusal;

    public bool UseGeneral(uint id)
    {
        var am = ActionManager.Instance();
        return am != null && am->GetActionStatus(ActionType.GeneralAction, id) == 0 && am->UseAction(ActionType.GeneralAction, id);
    }

    /// <summary>Uses an inventory item (a gourd, a coffer) the way the inventory's "Use" does.</summary>
    public bool UseItem(uint item)
    {
        var agent = AgentInventoryContext.Instance();
        return agent != null && agent->UseItem(item) == 0;
    }

    /// <summary>Uses a quest key item on a target.</summary>
    public bool UseEventItem(uint item, IGameObject target)
    {
        var am = ActionManager.Instance();
        return am != null && am->UseAction(ActionType.EventItem, item, target.GameObjectId);
    }

    /// <summary>Plays an emote at the current target (AgentEmote; emote 105 is "Pet", /stroke).</summary>
    public bool Emote(ushort emote, IGameObject target)
    {
        this.Target(target);
        var agent = AgentEmote.Instance();
        if (agent == null || !agent->CanUseEmote(emote))
            return false;
        agent->ExecuteEmote(emote);
        return true;
    }

    public int ItemCount(uint item)
    {
        var inv = InventoryManager.Instance();
        return inv == null ? 0 : inv->GetInventoryItemCount(item);
    }

    public static bool HasStatus(IGameObject? o, uint status) =>
        o is IBattleChara b && b.StatusList.Any(s => s.StatusId == status);

    public bool HasStatus(uint status) => HasStatus(this.Me, status);

    // --- Beastmaster horns ----------------------------------------------------------------------------------------

    /// <summary>The horn slot (0-2) holding <paramref name="beast"/>, or -1. <c>ActionManager.BeastmasterPets</c> holds XBMPet ids.</summary>
    public int HornOf(uint beast)
    {
        var am = ActionManager.Instance();
        if (am == null)
            return -1;
        var pets = am->BeastmasterPets;
        for (int i = 0; i < pets.Length && i < Battlehorns.Length; i++)
        {
            if (pets[i] == beast)
                return i;
        }
        return -1;
    }

    /// <summary>The beast on the first Battlehorn that holds one, or null.</summary>
    public uint? FirstHornBeast()
    {
        var am = ActionManager.Instance();
        if (am == null)
            return null;
        var pets = am->BeastmasterPets;
        for (int i = 0; i < pets.Length && i < Battlehorns.Length; i++)
        {
            if (pets[i] != 0)
                return pets[i];
        }
        return null;
    }

    /// <summary>The player's summoned familiar, if any, recognised by its BNpcName.</summary>
    public uint SummonedBeast()
    {
        if (this.Me is not { } me)
            return 0;
        foreach (var o in objects)
        {
            if (o.OwnerId == me.EntityId && o is ICharacter c && c.NameId is >= 14407 and <= 14456)
                return c.NameId - 14406;
        }
        return 0;
    }

    // --- Addons ---------------------------------------------------------------------------------------------------

    public AtkUnitBase* Addon(string name)
    {
        var ptr = gameGui.GetAddonByName(name);
        var addon = (AtkUnitBase*)ptr.Address;
        return addon != null && addon->IsVisible && addon->IsReady ? addon : null;
    }

    public static bool Callback(AtkUnitBase* addon, params int[] values)
    {
        var atk = stackalloc AtkValue[values.Length];
        for (int i = 0; i < values.Length; i++)
            atk[i].SetInt(values[i]);
        return addon->FireCallback((uint)values.Length, atk);
    }

    /// <summary>Clicks a button component the way a mouse click does.</summary>
    public static bool ClickButton(AtkUnitBase* addon, uint nodeId)
    {
        var button = addon->GetComponentButtonById(nodeId);
        if (button == null || !button->IsEnabled)
            return false;
        var owner = button->AtkComponentBase.OwnerNode;
        var registered = owner == null ? null : owner->AtkResNode.AtkEventManager.Event;
        var evt = new AtkEvent
        {
            Listener = (AtkEventListener*)addon,
            Node = owner == null ? null : &owner->AtkResNode,
            Target = &AtkStage.Instance()->AtkEventTarget,
        };
        var data = new AtkEventData();
        addon->ReceiveEvent(AtkEventType.ButtonClick, registered == null ? 0 : (int)registered->Param, &evt, &data);
        return true;
    }

    /// <summary>Advances a Talk box with a click.</summary>
    public static void ClickAnywhere(AtkUnitBase* addon)
    {
        var evt = new AtkEvent { Listener = (AtkEventListener*)addon, Target = &AtkStage.Instance()->AtkEventTarget };
        var data = new AtkEventData();
        addon->ReceiveEvent(AtkEventType.MouseClick, 0, &evt, &data);
    }

    /// <summary>The entries of a SelectString or SelectIconString menu.</summary>
    public static string[] MenuEntries(AtkUnitBase* addon, bool icon)
    {
        var menu = icon ? &((FFXIVClientStructs.FFXIV.Client.UI.AddonSelectIconString*)addon)->PopupMenu.PopupMenu
                        : &((FFXIVClientStructs.FFXIV.Client.UI.AddonSelectString*)addon)->PopupMenu.PopupMenu;
        var entries = new string[menu->EntryCount];
        for (int i = 0; i < entries.Length; i++)
            entries[i] = menu->EntryNames[i].ToString() ?? string.Empty;
        return entries;
    }

    /// <summary>Every text in an addon's nodes, for checking what a confirmation dialog is about to do.</summary>
    public static string AllText(AtkUnitBase* addon)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node != null && node->Type == NodeType.Text)
                sb.Append(((AtkTextNode*)node)->NodeText.ToString()).Append('\n');
        }
        for (int i = 0; i < addon->AtkValuesCount; i++)
        {
            var v = addon->AtkValues[i];
            if (v.Type is AtkValueType.String or AtkValueType.ManagedString)
                sb.Append(v.GetValueAsString()).Append('\n');
        }
        return sb.ToString();
    }

    // --- Job and gear ---------------------------------------------------------------------------------------------

    public bool OnBeastmaster => player.IsLoaded && player.ClassJob.RowId == CrucibleIds.BeastmasterJob;

    /// <summary>Equips the first gear set saved for Beastmaster; false when there is none.</summary>
    public bool EquipBeastmasterGearset()
    {
        var gs = RaptureGearsetModule.Instance();
        if (gs == null)
            return false;
        for (int i = 0; i < 100; i++)
        {
            if (gs->IsValidGearset(i) && gs->GetGearset(i)->ClassJob == CrucibleIds.BeastmasterJob)
                return gs->EquipGearset(i) == 0;
        }
        return false;
    }

    /// <summary>Moves <paramref name="item"/> from the bags or the armoury into the main hand (which changes the job).</summary>
    public bool EquipMainHand(uint item)
    {
        var inv = InventoryManager.Instance();
        if (inv == null)
            return false;
        InventoryType[] from = [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4, InventoryType.ArmoryMainHand];
        foreach (var type in from)
        {
            var container = inv->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded)
                continue;
            for (int slot = 0; slot < container->Size; slot++)
            {
                var it = container->GetInventorySlot(slot);
                if (it != null && it->ItemId == item)
                    return inv->MoveItemSlot(type, (ushort)slot, InventoryType.EquippedItems, 0, true) == 0;
            }
        }
        return false;
    }

    /// <summary>Saves the current gear as a new set (named for the job by the game).</summary>
    public bool CreateGearset()
    {
        var gs = RaptureGearsetModule.Instance();
        return gs != null && gs->CreateGearset() >= 0;
    }

    /// <summary>Starts equipping the game's recommended gear for Beastmaster; call again until it returns true.</summary>
    public bool EquipRecommended()
    {
        var rec = RecommendEquipModule.Instance();
        if (rec == null)
            return false;
        if (rec->IsUpdating)
            return false;
        if (rec->IsSetupForDifferentClassJob || rec->ClassJob != CrucibleIds.BeastmasterJob)
        {
            rec->SetupForClassJob((byte)CrucibleIds.BeastmasterJob);
            return false;
        }
        rec->EquipRecommendedGear();
        return true;
    }

    /// <summary>
    /// Saves the equipped gear into Beastmaster's gear set: the current set when it is Beastmaster's, else the first one;
    /// false when there is none. A set of another job is never overwritten.
    /// </summary>
    public bool UpdateBeastmasterGearset()
    {
        var gs = RaptureGearsetModule.Instance();
        if (gs == null)
            return false;
        int index = gs->CurrentGearsetIndex;
        if (index < 0 || !gs->IsValidGearset(index) || gs->GetGearset(index)->ClassJob != CrucibleIds.BeastmasterJob)
        {
            index = -1;
            for (int i = 0; i < 100 && index < 0; i++)
            {
                if (gs->IsValidGearset(i) && gs->GetGearset(i)->ClassJob == CrucibleIds.BeastmasterJob)
                    index = i;
            }
        }
        if (index < 0)
            return false;
        gs->UpdateGearset(index);
        return true;
    }

    public static bool IsHostile(IGameObject o) => o.ObjectKind == ObjectKind.BattleNpc && o.SubKind == (byte)BattleNpcSubKind.Combatant;

    /// <summary>BNpcName rows of the training dummies ("striking dummy", "timeworn striking dummy"): never killed, no EXP.</summary>
    private static readonly HashSet<uint> TrainingDummies = [541, 13078];

    /// <summary>A striking dummy. It keeps the character in combat and targets it, but it is not an enemy to fight.</summary>
    public static bool IsTrainingDummy(IGameObject o) => o is IBattleNpc n && TrainingDummies.Contains(n.NameId);

    /// <summary>The object targets nobody. An idle mob holds the game's "no object" id 0xE0000000 there, not 0.</summary>
    public static bool HasNoTarget(IGameObject o) => (uint)o.TargetObjectId is 0 or 0xE0000000;
}
