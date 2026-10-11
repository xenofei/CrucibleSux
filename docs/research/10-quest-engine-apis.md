# 10: Quest engine APIs

Checked 2026-10-11 from source, with nothing run in game. Versions:
- FFXIVClientStructs main 964b88c
- Questionable `new-main` 2dfa845 (AGPL: described only, never copied)
- TextAdvance master 9dee627
- Dalamud `api15-rollup`

Confidence: medium-high on signatures and constants, medium on how other plugins behave, and low on anything marked (inferred).

**Quest ids:** ClientStructs takes `ushort questId` = Quest RowId − 65536, so 71026 becomes 5490.

## 1. Interacting with an NPC or object

- **The call:** `TargetSystem.Instance()->InteractWithObject(GameObject*, bool checkLineOfSight = true)` returns `ulong` [1].
- **Questionable's pattern:** set the target, then interact with `checkLineOfSight: false`. It counts the interaction as started when the result is above 0 and not 7 [3].
- **Before interacting:**
  - Dismount first.
  - The target must be targetable.
  - Range comes from the approach: TextAdvance uses 6 + hitbox radius for an EventNpc, 3 for an EventObj and 8 for an Aetheryte [4].
- **Success:** `OccupiedInQuestEvent` or `OccupiedInEvent` appears. Retry the interaction every tick until then.
- **Also available:**
  - `EventFramework.CheckInteractRange(source, target, interactionType, log)` [2]
  - `InteractWithHandlerFromSelector(int)`, for when several handlers overlap on one object [2]

## 2. Dialogue, rewards and cutscenes

Every one of these is a callback: `AtkUnitBase.FireCallback(count, AtkValue*, close)` or `FireCallbackInt(int)` [5].

| Addon | What to do |
|---|---|
| Talk | Click: send ReceiveEvent mouse down, click and up (ECommons `AddonMaster.Talk.Click`) [7] |
| SelectString, SelectIconString | Callback with the entry index; read the entry text to choose [3][7] |
| CutSceneSelectString | `FireCallbackInt(index)` |
| SelectYesno | `FireCallbackInt(0)` is Yes, 1 is No [3] |
| JournalAccept | Button id 44 accepts [4][7] |
| JournalResult | Button id 37 completes [4] |
| JournalResult reward | TextAdvance hooks `AtkComponentJournalCanvas`. Our quests' rewards have no choice (inferred) |

**Cutscene skip:** TextAdvance hooks the cutscene input and patches a jump. That is too invasive for us. We rely on TextAdvance when the player has it, and otherwise press through Talk and the skip confirm.

**TextAdvance IPC** (`TextAdvance.` prefix) [9][10]:
- **Control:** `EnableExternalControl(string requester, ExternalTerritoryConfig)` returns bool. Release with `DisableExternalControl(requester)`.
- **State:** `IsEnabled`, `IsPaused`, `IsBusy`, `IsInExternalControl`.
- **Config:** each nullable field overrides the user's setting:
  - `EnableQuestAccept`, `EnableQuestComplete`, `EnableRewardPick`
  - `EnableCutsceneEsc`, `EnableCutsceneSkipConfirm`
  - `EnableTalkSkip`, `EnableRequestHandin`, `EnableRequestFill`, `EnableAutoInteract`
- **What it doesn't do:** **TextAdvance does not answer quest choices.** We pick SelectString answers ourselves (71030 and 71031).

## 3. Emotes

- **The call:** `AgentEmote.Instance()->ExecuteEmote(ushort emoteId, PlayEmoteOption* opt, ...)`. `PlayEmoteOption.TargetId` aims the emote [15].
- **Before emoting:** set the target and dismount.
- **Emote 105:** it is "Pet", and its text command is `/stroke` [16].

## 4. Key items

- **Questionable's own path** for Strangers in the Wood (5490) makes the raw-meat step a plain **Interact** with 1059320 [17]. The NPC's event takes the item.
- **Fallback:** use the item on the target with `AgentInventoryContext.UseItem(itemId)`, or `ActionManager.UseAction(ActionType.EventItem, itemId, targetId)` [3][19].

## 5. Changing job and gear

- **After Strangers in the Wood:** the player holds the Beastmaster weapon.
- **What Questionable's path does next (5491):**
  1. Equip the axe 47470.
  2. `CreateGearset`.
  3. Open the coffer 52649.
  4. Equip 47472 and 50727–50731.
  5. Use the Cu Sith Gourd.
  6. Equip recommended gear.
  7. `UpdateGearset`.
- **The calls:**
  - `InventoryManager.MoveItemSlot(src, srcSlot, InventoryType.EquippedItems, dstSlot, true)` [21]
  - `RaptureGearsetModule.CreateGearset()`, `EquipGearset(id)`, `UpdateGearset(id)`, `GetGearset(id)->ClassJob` [22]
  - `RecommendEquipModule.SetupForClassJob(43)`; wait while `IsUpdating`, then `EquipRecommendedGear()` [24]
- **Switching to Beastmaster later:** find the gearset whose ClassJob is 43 and equip it [23].

## 6. Beastmaster specifics

**Gauge** (`BeastmasterGauge`, ClientStructs PR #1947) [25]:

| Offset | Field |
|---|---|
| 0x08 | TP |
| 0x09 | Pet TP |
| 0x0B | `ActiveBattlehorn` (0 none, 1–3) |
| 0x0C | `InstinctualComboState` |
| 0x0D | `CurrentAffinity` (None, Volant, Rampant, Durant, Eldritch, Sunstrider, Moonstalker) |
| 0x0F | `KinshipState` |
| 0x10 | `InstinctState` |

- **Horn slots:** `ActionManager.BeastmasterPets` holds 3 XBMPet ids [19][26].
- **Actions:**
  - Horns: 44881, 44892, 44894.
  - Tempered Release: 44890.
  - Borrow: 44895.
  - Kinship skills: 44896–44903 (Quelling Wave is 44900, Soul Crush 44902).
  - Trick: 47093.
  - Finishers: 44930–44933 [28].
- **RotationSolver's Beastmaster rotation** covers horns, Borrow, Tempered Release, Trick and the finishers, but never mentions Universality [26]. BossMod's xan Beastmaster rotation is a skeleton [29].
- **Assigning a beast to a horn: not mapped.**
  - Addon rows 17706–17708 and 17733 show it is a context-menu action in the Bestiary [28].
  - `AgentXBMMonsterNotebook` exposes `ContextPetId` (0xA4), `OpenAddon` and `ToggleActivePetAddon` [30], but not the assign call.
  - **Plan:**
    - Check `BeastmasterPets`.
    - If the beast is missing, ask the player to assign it, and resume automatically once it shows up.
    - Ask the tester to capture the callback.

## 7. Quest state

- `QuestManager.IsQuestComplete(ushort)`
- `QuestManager.GetQuestSequence(ushort)`
- `IsQuestAccepted(ushort)`
- `GetQuestById(ushort)`

255 means ready to turn in [31][32].

**Striking dummy:** BNpcBase 19456, BNpcName 541. Questionable leaves the combo steps to the player [17]. No API resets dummy enmity [33].

## 8. Condition flags (Dalamud api15) [34]

| Group | Flags |
|---|---|
| Cutscene | `OccupiedInCutSceneEvent` 35, `WatchingCutscene` 58, `WatchingCutscene78` 78 |
| Event | `OccupiedInQuestEvent` 32, `OccupiedInEvent` 31, `Occupied` 25, `Occupied30` 30, `Occupied33` 33, `Occupied38` 38, `Occupied39` 39 |
| Zoning | `BetweenAreas` 45, `BetweenAreas51` 51 |
| Movement | `Mounted` 4, `InFlight` 77, `Jumping` 48, `Casting` 27, `InCombat` 26 |

## 9. Mounting

- **Mount Roulette:** `UseAction(GeneralAction, 9)`.
- **Flying roulette:** 24.
- **Dismount:** 23. Check that `GetActionStatus(GeneralAction, 23) == 0` first [16].
- **Questionable's pattern:** skip mounting within 50 y of the target or in combat, and retry every 5 s [35].
- **vnavmesh does not mount:** mount first, then pass `fly: true` only where flying is unlocked [36].

## 10. The Kornago shop (local sheets, game 2026.09.15)

- **The merchant:** ENpc 1059408. Its ENpcData lists 1704323, 1771019 and 1771020, plus 1771036.
- **SpecialShop 1771019, "Faded Remnants of Resilience":**
  - the gourds 49806–49812
  - cosmetics
  - Bright Remnant 51735, at 2 Faded each (gated by 71034)
- **SpecialShop 1771020, "Bright Remnants of Resilience":**
  - the gourds 49813–49820
  - Faded ×2 for 1 Bright
  - gear upgrades and orchestrion rolls
- **Gates:** each line carries its quest gate. `KornagoShop.Position` assumes the window hides gated lines (inferred).
- **Buying (inferred from other plugins):**
  1. `ShopExchangeItem` callback `[0, index, quantity]`.
  2. `ShopExchangeItemDialog` callback `[0]` confirms. The runner checks that the dialog names the item first.
- **Auto-attack:** Action 7 is "attack". It toggles, so it is sent only while `UIState.WeaponState.AutoAttackState.IsAutoAttacking` is false.

## Unknowns

1. The function and callback that assign a beast to a horn.
2. Any API for the capture result, beyond the status and the Bestiary flag.
3. The `QuestWork` layout beyond quest id and sequence.
4. Resetting a dummy's enmity.
5. How TextAdvance behaves under external control while the player has it disabled.
6. Whether `InstinctualComboState` or `CurrentAffinity` holds the Sunstrider/Moonstalker state.
7. The real interact range. Everything above in game.

## Sources

1. FFXIVClientStructs `Game/Control/TargetSystem.cs`, `Game/Object/GameObject.cs`
2. FFXIVClientStructs `Game/Event/EventFramework.cs`
3. Questionable `Questionable/Functions/GameFunctions.cs`
4. TextAdvance `TextAdvance/Executors/Exec*.cs`
5. FFXIVClientStructs `Component/GUI/AtkUnitBase.cs`
6. ECommons `Automation/Callback.cs`
7. ECommons `UIHelpers/AddonMasterImplementations`
8. ECommons `Automation/AutoCutsceneSkipper.cs`
9. TextAdvance `Services/IPCProvider.cs`
10. TextAdvance `Services/IPCTester.cs`, `Config.cs`
11. TextAdvance README
12. Questionable `Functions/ChatFunctions.cs`
13. ECommons `Automation/Chat.cs`
14. FFXIVClientStructs `Client/UI/UIModule.cs`
15. FFXIVClientStructs `Client/UI/Agent/AgentEmote.cs`
16. v2.xivapi.com:
    - Emote 105
    - EventItem 2003830
    - GeneralAction 9, 23 and 24
    - BNpcName 541
    - BNpcBase 19456
    - Addon rows 650 and 17705–17733
17. Questionable `QuestPaths/7.x - Dawntrail/Class & Job Quests/Disciple of War Job Quests/Beastmaster Quests`
18. Questionable `Controller/Steps/Interactions/UseItem.cs`
19. FFXIVClientStructs `Client/Game/ActionManager.cs`
20. Questionable `Controller/Steps/Interactions/EquipItem.cs`
21. FFXIVClientStructs `Client/Game/InventoryManager.cs`
22. FFXIVClientStructs `Client/UI/Misc/RaptureGearsetModule.cs`
23. Questionable `Data/ClassJobUtils.cs`
24. FFXIVClientStructs `Client/UI/Misc/RecommendEquipModule.cs`
25. FFXIVClientStructs PR #1947 (`JobGauges.cs`)
26. RotationSolverReborn `RotationSolver.Basic/Rotations/Basic/BeastmasterRotation.cs`
27. ffxiv.consolegameswiki.com: Beastmaster; A Beastmaster's Path
28. v2.xivapi.com Action sheet (ClassJob 43); Status 4601, 4624 and 4626
29. BossmodReborn `BossMod/Autorotation/Standard/xan/Melee/BST.cs`
30. FFXIVClientStructs PR #1952
31. FFXIVClientStructs `Client/Game/QuestManager.cs`
32. Questionable `Functions/QuestFunctions.cs`
33. ffxiv.consolegameswiki.com Striking Dummy
34. Dalamud `api15-rollup` `Game/ClientState/Conditions/ConditionFlag.cs`
35. Questionable `Controller/Steps/Common/MountStep.cs`
36. vnavmesh `IPCProvider.cs`
