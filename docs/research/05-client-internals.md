# Client internals: Beastmaster and the Crucible of the Unbroken

Research date: 2026-10-11.

Confidence is medium-high on the IDs and PR facts, which were read directly from xivapi and raw GitHub files. It is low on the "not found" claims: there was no code search, and some pages were cut off at 100k characters.

## Verdict per area

| Area | Source | Status |
|---|---|---|
| Beasts, boards, stages, items, quests, achievements (static) | Excel sheets (XBM*) | Readable |
| Job gauge (TP, affinity, combo, Battlehorn) | ClientStructs `BeastmasterGauge` | Readable |
| Unlocked beasts (bitmask) | ClientStructs `XBMManager`, Dalamud `IUnlockState.IsXBMPetUnlocked` | Readable once the pet list has loaded |
| Selected Battlehorn beasts | `ActionManager._beastmasterPets` (0x184, 3 bytes) | Readable |
| Active beast | `UIState->Buddy.PetInfo.Pet`, as RotationSolverReborn reads it | Readable |
| Per-beast HP, rank and stats in the party | `AgentXBMPetParty` | Readable while the agent is populated |
| In-run Crucible inventory, and using an item | `InstanceContentCrucibleOfTheUnbroken` director | Readable and callable |
| Stage and battle detail, degree selector | `AgentXBMStageDetailList` | Partly mapped |
| Beast rank/EXP/satiety outside the party agent | — | Not located: scrape addons (`XBMMonsterBookDetail`) or reverse ourselves |
| Board clears, degrees, best scores | — | Not located: achievements and quests give a proxy; scrape the addons |
| Leaderboard | — | Not located (server-fed; agent XBMRanking 506) |
| Board/node map state and node selection | Agents XBMStageList 503 / XBMStageMap 504 have no struct | Must be reversed |
| Capture target and result | — | Must be reversed (RSR's capture code is commented out) |

## 1. Excel sheets

Read from xivapi v2 and EXDSchema `latest` on 2026-10-11. The internal prefix is **XBM**.

### ClassJob and actions

- **ClassJob 43 (BST):**
  - Limited job, ClassJobCategory 203.
  - Unlock quest 71026 *Strangers in the Wood* (level 50, previous quest 70058).
  - Soul crystal: item 47474.
- **Actions from the sheet (ClassJob 43):**

| ID | Action | ID | Action |
|---|---|---|---|
| 44879 | Smash Axe | 44892 | Second Battlehorn |
| 44880 | Capture | 44893 | Shield Charge |
| 44881 | First Battlehorn | 44894 | Third Battlehorn |
| 44882 | Gauge | 44895 | Borrow |
| 44883 | Axeblade Bite | 44904 | Rallying Cheer |
| 44884 | Avalanche Axe | 44905 | Rally |
| 44885 | Shieldsplitter | 47093 | Trick |
| 44886 | Beast Mode | | |
| 44887 | Mistral Axe | | |
| 44888 | Spinning Axe | | |
| 44889 | Gale Axe | | |
| 44890 | Tempered Release | | |
| 44891 | Parting Blow | | |

- **Actions from RotationSolverReborn's `ActionId.resx`.** These are not yet checked against the Action sheet:

| IDs | What |
|---|---|
| 44896–44903 | Kinship skills |
| 44930–44933 | Finishers |
| 46750 / 46751 | Challenge / Snarl (duty actions) |
| 47092 | Tempered Release variant |
| 47238–47245 | Borrow, one per kin type |

### Beast and Crucible sheets

- **XBMPet** has 51 rows (0–50). Columns: Pet link, Action link, Location, LocationKey, Classification, InflictsStatus, SatietyMax, ParamGrow. Examples: 1 = Cu Sith (Action 45187), 2 = squirrel, 50 = behemoth.
- **XBMPetParamGrow** holds five stats: Strength, Intelligence, PhysicalResistance, MagicalResistance, Constitution. Subrow = Rank − 1.
- **XBMContent:**
  - Rows 1–5 link to ContentFinderCondition 1088–1092.
  - Columns: TeamSize, RecommendedRank, SynkRank, 33 BonusPoints.
- **XBMItem rows 1–99:**
  - 1–75 are Beast Gear; 76–99 are Crucible Items (76–79 Beast Potions, 96 Blessed Horn, 97 Beastly Smokebomb).
  - Ephemeral items live in XBMItem, not Item.
- **Other XBM sheets:** XBMItemType, XBMScoreRank, XBMScoreBonus, XBMContentStageEvent, XBMContentStageEventMap, XBMContentBattle, XBMBattleDetail, XBMBattleDetailAction, XBMElement, XBMEntrance, XBMContentCamp, XBMRandomStageEvent, XBMContentRandomStageEvent, XBMStageEventType, XBMActionEffectType, XBMActionTarget.
  - Several columns are still `UnknownN`.
  - XBMScoreBonus came back empty from the API.

### Duties

| CFC | Board | Territory | InstanceContent |
|---|---|---|---|
| 1088 | First Board | 1339 | 56001 |
| 1089 | Second Board | 1340 | 56002 |
| 1090 | Third Board | 1341 | 56003 |
| 1091 | First Master's Board | 1342 | 56004 |
| 1092 | Second Master's Board | 1343 (from RSR; not seen in the row read) | — |

All five use ContentType 40 ("XBM") and accepted category 203.

### Quests and achievements

- **Quests:** 71026 *Strangers in the Wood*, 71027 *The Wilds Call*, 71030 *Into the Crucible*. More Beastmaster quests exist; the search result was cut off.
- **Achievements:**

| Group | IDs |
|---|---|
| Beasts befriended | 4028–4032 |
| Beastmaster level | 4023–4027 |
| Beasts at rank 25 | 4056–4060 |
| Freeing the Beast (Apex rank) | 4053 |
| Crucible Master (Legendary rank) | 4054 |
| Unbroken Endgame (Third Degree) | 4055 |
| Beastkin Mate | 4061 |
| Rankings | 4075–4077 |

## 2. FFXIVClientStructs

- **PR #1976** (merged 2026-09-30) adds `InstanceContentCrucibleOfTheUnbroken`:
  - Size 0x25A0; it inherits `InstanceContentDirector`.
  - `_inventory` holds 10 slots at 0x2380, with `ItemId` as a ushort at +4.
  - `UseItem(slot, beastId)` acts on the current target. `beastId` is an XBMPet row, used only for Blessed Horn.
  - `data.yml` lists the director's vtable and a function `IsInCrucibleOfTheUnbroken` at 0x1400E1A70.
- **PR #1947** (merged 2026-09-30) adds `BeastmasterGauge` at JobGauge +0x08, size 0x18:
  - Fields: TPGauge, PetTPGauge, PetTPAtLastUse, ActiveBattlehorn, InstinctualComboState, CurrentAffinity, ChainCount, KinshipState, InstinctState.
  - Also adds the `BeastmasterAffinity` enum.
- **PR #1952** (merged 2026-10-02) adds three agents:
  - `AgentXBMMonsterNotebook`:
    - Unlocked-beast bitmask at +0x70 and a mirage bitmask at +0x77.
    - Selected pet and content IDs.
    - `IsPetUnlocked` and `HandlePetListPacket`.
  - `AgentXBMPetParty`:
    - Per-pet current and max HP arrays.
    - `ParamGrowValues`: Rank plus the five stats.
    - `GetSyncedRank` and `GetTeamSize`.
  - `AgentXBMStageDetailList`:
    - 1000 stage and battle entries.
    - Stage-event, battle-detail, element and resist IDs.
    - A running-challenge mode, `StartBattle`, and `SelectedRank` (0–3, the degree selector).
- **Commit 1134d0e** (2026-10-03) adds vtables for `AddonJobHudXBM0` (TP gauge) and `AddonJobHudXBM1` (affinity gauge). They have no field layouts.
- **XBMManager** (2026-09-09): `Instance()`, the 50-bit unlocked-beast bitmask, `NumUnlockedPets`, `State`, `IsPetUnlocked`, `SetPetUnlocked`.
- **PR #1896** (2026-07-26) adds the XBM UI components.
- **ActionManager** has `_beastmasterPets` at 0x184: 3 bytes of XBMPet row IDs, the selected Battlehorn beasts.
- **AgentId enum:**

| ID | Agent | Struct mapped |
|---|---|---|
| 497 | XBMContentsMainHUD | no |
| 498 | XBMItemDetail | no |
| 499 | XBMBattleMonsterDetail | no |
| 500 | XBMMonsterNotebook | yes |
| 501 | XBMPetParty | yes |
| 502 | XBMStageDetailList | yes |
| 503 | XBMStageList | no |
| 504 | XBMStageMap | no |
| 505 | XBMResult | no |
| 506 | XBMRanking | no |

- **PlayerState and UIState** have no XBM fields. For comparison, `PlayerState._completedMaskedCarnivale` is at 0x68C and `UIState.Buddy` at 0x1EF0.

## 3. Dalamud

- **PR #2952** (merged 2026-09-08) adds `JobFlags.Beastmaster` (bit 32, job 43) for Party Finder.
- **PR #2958** (merged 2026-09-09) adds `IUnlockState.IsXBMPetListLoaded` and `IsXBMPetUnlocked(XBMPet)`. It also hooks `SetPetUnlocked` and raises unlock events.
- No new condition flags or `ClientState` fields were found.
- Beasts appear to be Buddy pets, not a new object kind. The Dalamud `ObjectKind` and BattleNpc-owner behaviour has not been verified.

## 4. Addon names

| Addon | Status |
|---|---|
| `XBMMonsterNotebook` | Has an addon struct |
| `XBMMonsterBookDetail` | Has an addon struct, with text nodes for HP, EXP, Satiety, the five stats and the three beast actions |
| `XBMActivePet` | Named only in agent comments |
| `XBMMonsterNotebookFilterSetting` | Named only in agent comments |
| Stage-list, map, result and ranking addons | UNKNOWN; probably named after their agents |

YesAlready, TextAdvance and Pandora's Box were not checked.

## 5. Existing plugin source

- **RotationSolverReborn** added Beastmaster in PRs #1366, #1368, #1370 and #1372 (2026-09-11 to 09-13):
  - `RotationSolver.Basic/Rotations/Basic/BeastmasterRotation.cs` reads the gauge by raw pointer at +0x08.
  - `RotationSolver.Basic/DataCenter.cs` detects the Crucible by territory 1339–1343 and reads `ActivePet` from `UIState`.
  - `RotationSolver.Basic/Helpers/BestiaryHelper.cs` builds a beast dictionary from XBMPet. Its Crucible code is commented out.
  - Capture targeting is commented out.
- **Wrath Combo and BossModReborn:** no "Beastmaster" issue or PR titles. Their code was not searched.
- **ACT/IINACT parsers:** not checked.

## 6. What we must reverse ourselves

1. Where Crucible progress and degrees are stored.
2. Where beast rank and EXP are stored outside `AgentXBMPetParty`.
3. Leaderboard data and its packets.
4. Board and node map state, and how a node is selected (agents 503 and 504, and their addons).
5. Struct layouts for the stage-list, map, result and ranking agents and the HUD addons.
6. The Capture target and result API.
7. The packets for the roster and for feeding beasts.

## Sources

1. https://v2.xivapi.com/api/sheet/ClassJob/43
2. https://v2.xivapi.com/api/search?sheets=Action&query=ClassJob=43
3. https://github.com/FFXIV-CombatReborn/RotationSolverReborn/pull/1366
4. https://raw.githubusercontent.com/xivdev/EXDSchema/latest/XBMPet.yml (the same path pattern for the other XBM sheets)
5. https://v2.xivapi.com/api/sheet/ContentFinderCondition?rows=1088,1089,1090,1091,1092
6. https://v2.xivapi.com/api/sheet/XBMItem
7. https://v2.xivapi.com/api/search?sheets=Quest&query=Name~"Crucible"
8. https://v2.xivapi.com/api/search?sheets=Achievement&query=Description~"Crucible"
9. https://github.com/aers/FFXIVClientStructs/pull/1976
10. https://github.com/aers/FFXIVClientStructs/pull/1947
11. https://github.com/aers/FFXIVClientStructs/pull/1952
12. https://github.com/aers/FFXIVClientStructs/commit/1134d0e058ac5b7eb99bda3146c06cf71346aae5
13. https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/XBMManager.cs
14. https://github.com/aers/FFXIVClientStructs/pull/1896
15. https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/ActionManager.cs
16. https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentModule.cs
17. https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/PlayerState.cs and UIState.cs
18. https://github.com/goatcorp/Dalamud/pull/2952
19. https://github.com/goatcorp/Dalamud/pull/2958
20. https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonXBMMonsterBookDetail.cs
21. https://github.com/FFXIV-CombatReborn/RotationSolverReborn/pull/1372
22. https://api.github.com/search/issues?q=Beastmaster+repo:PunishXIV/WrathCombo
