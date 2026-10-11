# Crucible of the Unbroken: what existing automation already covers

Research date 2026-10-11. All descriptions are my own summaries. No code was copied (AutoDuty has no licence).

Upstream snapshots used:

| Repo | Commit | Date | Notes |
|---|---|---|---|
| erdelf/AutoDuty | 4a0dfdc | 2026-10-09 | This is HEAD. No newer commits. The last Crucible commit is b8135fe (2026-10-05, "Fix skipping camps when option off"). |
| FFXIV-CombatReborn/BossmodReborn (BMR) | bbfa719 | 2026-10-09 | HEAD |
| awgil/ffxiv_bossmod (VBM) | 40b0abd | 2026-10-01 | HEAD |

Paths below are relative to each repo. A reference like `file:L10-20` means lines at the commit above. For AutoDuty path JSON files, `#n` is the index in the `Actions` array.

ID mapping: territories 1339-1343 correspond to ContentFinderCondition 1088-1092, which are boards 1-5 (`AutoDuty/Managers/CrucibleManager.cs:L28-35`; `AutoDuty/Managers/Crucible/CrucibleTeam.cs:L99-107`).

---

## 1. AutoDuty (erdelf) at 4a0dfdc

### 1.1 Overall flow
- Queueing happens in `AutoDuty/AutoDuty.cs:L1326-1332`. AutoDuty walks to Lauda in Ul'dah (territory 148), talks to her, opens the board list and picks the board. It then runs the team setup, sets the Degree (only if quest 71045 is complete) and confirms (`CrucibleManager.cs:L51-100, L151-245`).
- Inside the duty, AutoDuty runs a normal path file like any other duty. The Crucible-specific menu handling is a separate per-frame hook, `CrucibleManager.Update` → `CrucibleMenus.Update` (`CrucibleManager.cs:L247-267`, called from `AutoDuty.cs:L2050`). It only runs while AutoDuty is Looping or Navigating, the territory is a Crucible territory, and AutoDuty is not paused.

### 1.2 Route and node selection
- There is **no dynamic routing.** Each board has a hand-written path: `AutoDuty/Paths/(1339) First Board of the Unbroken.json` … `(1343) Second Master's Board.json`.
  - 1339 walks a fixed sequence: node 1 → Bone Knight (#4) → node 2 → Arch Demon (#12) → camp (#17) → Banemite (#24) → treasure (#29) → "Sixth Fight Golem" node → **Ogre Piece** (#35) → camp (#40) → shop (#43, always visited) → final boss **Pas de Seul** (#51).
  - Each step is a `MoveTo` to a board coordinate (x≈-700), then a `Boss` action at the arena coordinate. `ModifyIndex -1` loops are gated on distance and wait for the teleport into or out of the arena.
  - 1343 is the only path with branches. It always skips the elite side, and a note says "TODO: condition this on some setting" (#8, #25, #29, #40).
- `CrucibleBoard`/`CrucibleBoardReader` read the real board graph (`CrucibleBoard.cs:L29-94`), but only for the item logic (`HasCampBeforeNextFight`, `StopPlayerIsOn`) and the `/autoduty board` map window. Nothing chooses a route from that graph.

### 1.3 Fight team (FightPicks)
- `CrucibleMenus.StartFight` (`CrucibleMenus.cs:L199-258`) runs when the board layout and the team window are both open, before each battle.
  - It sorts the team with `CrucibleTeam.FightOrder`: living beasts only, highest **cached Rank** first, then **lowest** `Score()` (the sum of stats) (`CrucibleTeam.cs:L357-365`).
  - It takes the first `FightSize = 3` (`CrucibleTeam.cs:L128`), unpicks any wrongly selected beast, picks the missing ones and confirms.
- The pick does **not** consider enemy type, element, classification or the encounter.

### 1.4 Board team size ("took 10 beasts instead of 3")
- The roster that gets registered is the **team for the whole board**, not the per-battle three.
  - Size comes from `TeamSize()` (`CrucibleTeam.cs:L97-125`): 1339→10, 1340→12, 1341→14, 1342→12, 1343→15.
  - The mode adjusts that size. **Recommended** (the default) uses `min(size, RecommendedTeamCap)`, and the cap defaults to **15**, so board 1 gets 10. **Leveling** uses `size-3`, `3` (Only_3), `size` (Full) or `size-2` (Carry_Minus_2). Custom uses the user's list.
- `CrucibleTeamSetup` rebuilds the roster from the bestiary on every queue (`CrucibleTeam.cs:L549-1055`).
- This is why the tester saw 10 beasts. AutoDuty fills the board roster up to the board maximum by default.

### 1.5 Healing and survival
- **Player HP, items** (`CrucibleMenus.UpdateItems`, `L538-628`). Runs every 250 ms, with at least 5 s between uses (`ItemGap`).
  - In the arena: at HP ≤ 40% (`FightLow`) **and** in combat, it uses the first available item from `FightHealingItems` (`CrucibleItemData.cs:L179-193`).
  - On the board: at HP ≤ 60% (`BoardLow`), and only if no campsite is reachable before the next fight, it uses `BoardHealingItems` (`L195-204`).
  - Items go through a hooked director function, `UseItem(slot, beastId)` (`AutoDuty/External/InstanceContentCrucible.cs`). AutoDuty always passes `beastId = 0`, so there is **no beast-targeted item use**.
- **Status items** (`L568-571`, list at `CrucibleItemData.cs:L206-213`). Temporal Sands and G1/G2 Reraiser are applied whenever their status is missing. Merchant's Eye and Thief's Eye are used only in combat. The Reraiser covers revives for the **player** only.
- **Beast HP**: nothing during battles. Knocked-out beasts are simply skipped by `FightOrder` (HP>0 filter). No beast revive logic exists.
- **Campsite rest** (`CrucibleMenus.Rest`, `L319-358`).
  - The intent is to pick up to 2 beasts (`RestPicks = 2`) below 60% HP (`RestBelow`), lowest first. Otherwise it picks nobody, which the comment says gives the player a 90% heal.
  - **Bug at L334:** the filter compares `HP / HP` (not `HP / MaxHP`) with 0.6. That value is always 1, so the list is always empty. AutoDuty **never rests beasts**: every campsite is a player-only rest. This matches the tester's "no check for healing up to 2 beasts".
- If `Rest = false`, it presses the leave/return button instead (`L321-326`).

### 1.6 Shop ("bought items in the shop")
- `UpdateShop` (`L363-469`) and `ChooseBuy` (`L471-498`) are **on by default** (`Meta.Crucible.Shop = true`).
  - While the shop window is open, AutoDuty buys the first affordable item in priority order from the active `ShopLists[ShopListIndex]`, skipping `Blocked` entries. It only buys when there is room: items < 10 (`ItemCap`), gear < 10 (`GearCap`), one feed per visit.
  - Feed purchases then auto-feed a beast in FightOrder order (`Feed`, `L500-529`).
- The default list is every shop item, in the hard-coded order from `CrucibleItemData.ShopItems` (`CrucibleItemData.cs:L226-248`). It starts with healing items, then status items, damage fangs, gear and feed.
- With `Shop = false`, AutoDuty closes the shop window and confirms the prompt (`L370-389`). The path still walks onto the shop node.

### 1.7 Treasure and loot
- **Treasure** (`PickTreasure`, `L260-317`, `Meta.Crucible.Treasure = true`) takes the highest-ranked choice according to the same shop priority list. Gear is only taken if it is not owned, not blocked, and gear < 10. Items are only taken if items < 10.
  - Small issue: after `Close` when nothing is eligible, it still falls through to `First()` on an empty list (`L298-304`).
- **Post-fight loot** (`L124-169`, `Meta.Crucible.Loot = true`) takes the coins, then takes each item or gear choice while there is room, then closes.

### 1.8 Combat: movement, targeting, rotation
- **Arena entry**: the `Boss` path action (`AutoDuty/Managers/ActionsManager.cs:L778-813`).
  - It finds the boss object, then moves the player toward it with vnavmesh until the boss is in combat (`BossMoveCheck`, `L740-755`). vnavmesh does **not** know about AoEs.
  - It hard-sets the target to the boss (`L788`), then waits for the fight to end. After that it tries to loot coffers.
- **Between Boss actions**: `StageWaitingForCombat` (`AutoDuty.cs:L1561-1658`).
  - If every in-combat enemy is a non-boss more than 25 y away or untargetable, it **pathfinds to them with vnavmesh** (`L1588-1609`). This is a plausible cause of "ran around into AoEs".
  - If the player has no target, it targets the nearest engaged hostile (`L1617-1624`).
  - It switches BossMod's StayCloseToTarget range between `MaxDistanceToTargetFloat` and `MaxDistanceToTargetAoEFloat` when 3 or more enemies are within 15 y (`L1625-1645`).
- **Rotation plugin choice**: `SetRotationPluginSettings` (`AutoDuty.cs:L1810-1893`). With `RotationPlugin = All` (the default), the order is Wrath, then RSR, then BossMod.
  - If Wrath or RSR takes the job, BossMod gets the **"AutoDuty Passive"** preset (`Resources/AutoDuty_Passive.json`): movement modules only and **no AutoTarget**, so targeting falls to RSR (`TargetingTypeNonTank = LowHP`).
  - Otherwise BossMod gets the **"AutoDuty"** preset (`Resources/AutoDuty.json`).
- **The "AutoDuty" preset sets**:
  - `MiscAI.AutoTarget` with Retarget = Always and Everything = Enabled (`L4-13`).
  - `xan.BST` with Targeting = Auto (`L266-271`).
  - `xan.CrucibleAI` with default tracks (`L351`). That module exists only in VBM (see §2).
  - `StayCloseToTarget`, `StayCloseToPartyRole` (range 20), `NormalMovement` with Destination = Pathfind, and `StayWithinLeylines`.
- **BossMod settings pushed over IPC** (`AutoDuty/IPC/IPCSubscriber.cs:L113-168`):
  - PreventMovingWhileCasting, GTMode, DashSafety, AllowAutomaticActions, AllowAutomaticInteract.
  - StayCloseToTarget range comes from `MaxDistanceToTargetFloat`. It is role-based by default, giving 2.6 for melee/tank and 10 for ranged (`AutoDuty.cs:L1913-1942`).
  - Positional is Rear for melee.
  - IPC works with either "BossMod" or "BossModReborn" (`IPCSubscriber.cs:L67`).
- **No target priority of its own.** Mage-first or adds-first comes only from the active BossMod module, if any.
  - In fact, the damage-item code **overrides the target** to a clustered add when 4 or more enemies are within 40 y and a Fang item is available (`CrucibleMenus.cs:L576-593`).
- **Battlehorn and beast summoning**: AutoDuty does none. It relies on the rotation (BossMod xan BST in VBM, see §3). The BMR BST rotation does **not** summon.

### 1.9 Config keys (exact paths and defaults)
These are from `AutoDuty/Configurations/ConfigurationProfileV2.cs` and `Data/Enums.cs:L212-233`.

You can override them with `AutoDuty.PushConfigOverrides(Dictionary<string,string>)` (`IPC/IPCProvider.cs:L44-59`) or set them with `SetConfig(name, value)`. Names are case-insensitive dotted paths, enums are parsed by name, and AutoDuty restores the values on stop. **List-typed keys are rejected by overrides** (`Helpers/ConfigOverrideHelper.cs:L44-49`).

`Meta.Crucible.*` (`L87-115`):

| Key | Type | Default | Effect |
|---|---|---|---|
| `Meta.Crucible.Degree` | enum CrucibleDegree {Standard, First, Second, Third} | Standard | Board difficulty degree |
| `Meta.Crucible.TeamMode` | enum CrucibleTeamMode {Recommended, Leveling, Custom} | Recommended | How the board roster is built |
| `Meta.Crucible.LevelingMode` | enum CrucibleLevelingMode {Full, Minus_3, Only_3, Carry_Minus_2} | Minus_3 | Roster size in Leveling mode |
| `Meta.Crucible.RecommendedTeamCap` | int, clamped 3..15 | 15 | Roster cap in Recommended mode |
| `Meta.Crucible.ClassificationLock` | int | 0 | 0 = any classification |
| `Meta.Crucible.CustomTeam` | List<uint> | [] | Overrides cannot set it (list); use SetConfig or the UI |
| `Meta.Crucible.ShopLists` | List<CrucibleShopList> | [] (seeded) | Overrides cannot set it (list) |
| `Meta.Crucible.ShopListIndex` | int | 0 | Which shop list is active |
| `Meta.Crucible.ShopGearOrder` | List<uint> | [] | Legacy |
| `Meta.Crucible.FightPicks` | bool | true | Auto-pick 3 beasts per battle |
| `Meta.Crucible.Loot` | bool | true | Take post-fight loot |
| `Meta.Crucible.Treasure` | bool | true | Take a treasure choice |
| `Meta.Crucible.Shop` | bool | true | Buy in the shop |
| `Meta.Crucible.Rest` | bool | true | Rest at campsites |
| `Meta.Crucible.Items` | bool | true | Use HP, status and damage items |
| `Meta.Crucible.RespectGearRequirements` | bool | true | Skip gear whose prerequisite is missing |

`DutyConfig.*` (`L274-338`):

| Key | Type | Default |
|---|---|---|
| `DutyConfig.AutoManageBossModAISettings` | bool | true |
| `DutyConfig.AutoManageRotationPluginState` | bool | true |
| `DutyConfig.RotationPlugin` | enum RotationPlugin | All |
| `DutyConfig.UsingAlternativeRotationPlugin` | bool | false |
| `DutyConfig.UsingAlternativeMovementPlugin` | bool | false |
| `DutyConfig.UsingAlternativeBossPlugin` | bool | false |
| `DutyConfig.BossMod.UpdatePresetsAutomatically` | bool | true |
| `DutyConfig.BossMod.MaxDistanceToTargetRoleBased` | bool | true |
| `DutyConfig.BossMod.MaxDistanceToTargetFloat` | float | 2.6 |
| `DutyConfig.BossMod.MaxDistanceToTargetAoEFloat` | float | 12 |
| `DutyConfig.BossMod.PositionalRoleBased` | bool | true |
| `DutyConfig.BossMod.MaxDistanceToTargetRoleMelee` | float | 2.6 |
| `DutyConfig.BossMod.MaxDistanceToTargetRoleRanged` | float | 10 |
| `DutyConfig.BossMod.PositionalEnum` | enum Positional | Any (forced to Rear for melee when role-based) |
| `DutyConfig.BossMod.PositionalAvarice` | bool, internal | true |
| `DutyConfig.LootTreasure` | bool | true |
| `DutyConfig.AutoExitDuty` | bool | true |
| `DutyConfig.RSR.TargetingTypeNonTank` | enum | LowHP |

Hard-coded behaviour with **no config key**: RestPicks=2, RestBelow=0.6 (buggy), FightLow=0.4, BoardLow=0.6, ItemCap and GearCap=10, ItemGap=5 s (`CrucibleMenus.cs:L24-40`), and FightSize=3.

### 1.10 Tester reports mapped to causes

| Report | Cause in AutoDuty | Override that helps |
|---|---|---|
| Ran around into AoEs | Board and arena-approach movement uses vnavmesh, which is AoE-blind (`ActionsManager.cs:L740-755`, `AutoDuty.cs:L1588-1609`). In-fight dodging is BossMod AI only. | None. Needs an AoE-aware mover or a check that the BossMod build has the modules. |
| Hit in Ogre Piece | Both BossMod forks have an Ogre module (§2). Allfire (40 y raidwide) and the wisps' Scorched Earth (no cast, 60 y) are unavoidable. Other causes: AutoDuty moving the player with vnav, a BossMod build older than the modules, or `RotationPlugin` handing the job to RSR/Wrath while BossMod only moves. | `DutyConfig.RotationPlugin=BossMod` keeps the BossMod AI in control |
| Didn't kill the mage first (Pas de Seul) | AutoDuty has no priority logic. VBM's module gives mage and knight the same priority 0. BMR gives the mage priority 2. AutoDuty's Fang code can also retarget to adds. | `Meta.Crucible.Items=false` (stops the Fang retarget, but also stops HP items); use BMR or add your own priority |
| Took 10 beasts not 3 | Recommended mode: `RecommendedTeamCap` defaults to 15, so the roster is min(10,15) = 10 | `Meta.Crucible.RecommendedTeamCap=3` (Recommended mode), or `Meta.Crucible.TeamMode=Leveling` + `Meta.Crucible.LevelingMode=Only_3` |
| Bought in shop (score penalty) | `Shop` defaults to true | `Meta.Crucible.Shop=false` |
| No healing of player or up to 2 beasts | The player gets 40%/60% item heals only. Beast rest selection is broken (HP/HP, L334). No beast healing in fights. | None. The plugin must implement it. |

---

## 2. BossMod modules for the Crucible

**Both forks have full Crucible module sets today.** Every module is keyed to CFC GroupID 1088-1092, so the AI does see Crucible AoEs when a current build is installed.

- **VBM (awgil)**: `BossMod.Modules/Global/Crucible/`
  - Board1: BoneKnight, ArchDemonPiece, BanemitePiece, **OgrePiece**, **PasDeSeul**, PiscodemonPiece.
  - Board2: 7 modules plus FinalBoss.json.
  - Board3: 8 modules, including **GuttlerTheGutter**, which contains ThanatosPiece adds.
  - Master1: 7 modules, including **BorgnyTheVenomous** with a ToxicMass add at priority 1.
  - Master2: 10 modules, including **LaudaTheSpellcleaver** with ThanatosPiece adds.
  - Helpers: `ProximityAdds.cs` (marks adds within 3 y as should-target) and `ResistHelper.cs` (tracks poison and physical immunity buffs from BST statuses).
  - Item enum: `BossMod/Data/CrucibleID.cs`.
  - History: the modules were split out on 2026-09-16 (d7412bd). Board 1 was finalized on 09-28 (74ad673, 94e0d8b). The last change was 10-01 (4cd4b8f).
- **BMR**: `BossMod/Modules/Global/CrucibleOfTheUnbroken/01FirstBoard…05SecondMasterBoard/`. It has 37 modules, including OgrePiece, PasDeSeul, GuttlerTheGutter, BorgnyTheVenomous and LaudaTheSpellcleaver. They use `GroupType.CrucibleOfTheUnbroken`. Board 1 was last touched 09-21 (f81fe83); the folder was last touched 10-01 (70ce5fb).
- **Ogre Piece**
  - VBM `Board1/OgrePiece.cs:L41-92` models:
    - the Scorching Smite cones (fast and slow),
    - Magma 3 y and 5 y as voidzones,
    - the Burning Ward voidzone,
    - Ball of Fire, predicted as a 10 y AoE at the tethered ball,
    - Allfire as a raidwide only.
    - It does not model Scorched Earth.
  - BMR `01FirstBoard/OgrePiece.cs:L45-97` models the same cones and magma, treats the Ball of Fire as a 6 y moving voidzone, and lists Allfire and Scorched Earth as raidwides.
- **Pas de Seul**
  - VBM `Board1/PasDeSeul.cs:L41-110`:
    - Models the Blood Rain circle and donut, Void Aero rect and cone, Void Fire II, Sweet Steel cone, and Heart Shatter, predicted from the pheromone tether.
    - Adds use `AddsMulti` [Knight, Mage] at **priority 0**, the same as the boss. AutoTarget then picks by distance, so **no mage-first.**
    - Fanaticism (the mage buffing the boss) is only an interrupt hint (L47).
  - BMR `01FirstBoard/PasDeSeul.cs:L45-47`:
    - **Mage priority 2, Knight priority 1**, so mage first.
    - Models the same AoEs, plus a predicted Heart Shatter.
- **VBM `xan.CrucibleAI`** (`BossMod.Autorotation/Standard/xan/AI/Crucible.cs:L5-47`, BST only, max level 50) does four things:
  - **Snarl** on a PreferShirking enemy when a pet is out.
  - **Challenge** on a ShouldBeTanked enemy.
  - **Quelling Wave** on a ShouldBeDispelled enemy, if gauge classification is 5.
  - **Soul Crush** on a ShouldBeInterrupted enemy, if classification is 7, and it moves into melee range for it.
  - BMR has no CrucibleAI, so the AutoDuty preset entry for it does nothing there (unverified how BMR treats the unknown module name).
- **"XBM" prefix**: no BossMod code uses it. It appears only in AutoDuty addon names (`XBMPetParty`, `XBMContentsItemShop`, …) and sheets (`XBMPet`, `XBMItem`).
- **Issues and PRs**:
  - BMR has three closed issues: #1364 (Younger Tablitaur Endless Swing hits AI), #1365 (Loosefrox circles linger) and #1367 (Gargoyle Malady Orb).
  - VBM search returned nothing (issues may be disabled).
  - AutoDuty has closed bugs #441, #469, #470, #471, #517 and #554 (shop, treasure and loot hangs).
  - No open issues mention Crucible or Beastmaster.
- **Without a module**: BossMod AI has no generic omen or cast-based AoE avoidance. Neither tree has such a component. BMR `AI/AIBehaviour.cs:L263-306` only builds goal zones (follow, target range) plus forbidden zones that active modules provide. With no module, the AI does not dodge anything; it only keeps range on the target.

## 3. BMR Beastmaster rotation (`BossMod/Autorotation/Standard/xan/Melee/BST.cs`, 41 lines)
- **What it does**: tracks Targeting and AOE only. It picks a primary target within 3 y, runs the 1-2-3 combo Smash Axe → Axeblade Bite → Shieldsplitter, and adds a 3 y goal zone on the target (`L24-40`).
- **What it does not do**: summon Battlehorns or manage the pet, use Trick, Tempered Release or Parting Blow, use Borrow or the skins, use Rally, Snarl or Quelling Wave, or use any HP-based healing or defensive. Quality is marked WIP. The definition says level 100, but BST is level-capped lower in the content.
- **VBM comparison**: VBM's BST (`BossMod.Autorotation/Standard/xan/Melee/BST.cs`, 474 lines) is much fuller. It summons the first available pet (Summon track), re-summons to refresh One With Nature, pre-borrows, uses Trick, Rally and Rallying Cheer, Tempered Release, Parting Blow, Shield Charge, and Quelling Wave when classification is 5. It still has **no HP-threshold self or beast heals** (no HP checks in the file).
- **Takeaway**: on BMR, nothing calls a beast at all. Summoning, beast HP handling and healing must come from Crucible SUX or from VBM.
