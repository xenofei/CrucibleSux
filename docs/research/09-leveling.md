# 09: Beastmaster leveling 1–50

Researched 2026-10-11 from web sources; nothing here was tested in game.
- The API facts come from reading plugin and ClientStructs source. Confidence is medium.
- The "fastest" ranking has low confidence: no source measures EXP per hour, and the Beastmaster FATE bonus percentage is not published.

## Verdict

- **Open to Beastmaster:** FATEs (with the job's FATE bonus), captures, Crucible boards (from level 30), job quests, and synced dungeons with a premade or unrestricted party only.
- **Closed:** Duty Support, Trust, roulettes, Deep Dungeons, Variant and Criterion, and Field Operations [7]. Occult Crescent is a Field Operation, so closed (inferred).
- **Unattended solo route:** FATEs at or near your level, with every EXP multiplier stacked, capturing beasts in the same zones on the way. From level 30, Crucible clears are a side source.
- **No dungeons:** dungeons need a premade party, so they are not a solo option.
- **Our approach to "fastest":** since no published rate exists, the plugin **measures EXP per hour** for each source it uses and keeps using the best one.

## EXP needed

1→50 is 3,589,670 EXP, of which 30→50 is 3,015,800 (84%). See `docs/data/bst-progression.md` for the table.

## EXP sources

| Source | Numbers and rules |
|---|---|
| FATEs, Middle La Noscea [2] | Base EXP: level 3 = 240, level 7 = 560, level 10 = 855, level 14 = 1,380 |
| FATEs, South Shroud [2b] | Level 21 = 1,800, 24 = 1,980, 30 = 3,120 (archived page) |
| FATEs, Coerthas Central Highlands [2c] | Level 35 = 3,960, 40 = 5,390, 45 = 5,940, 49 = 6,960, 50 = 7,080 |
| FATE rules [4] | More than 5 levels above a FATE, you must sync down. Below its level you can join, but your contribution is penalised. Bonus FATEs give +50% |
| Crucible boards [5] | Give character and beast EXP; no amount published. Open at levels 30/40/50 |
| Captures [6] | Give EXP; no amount published. The target must be at or below your level |
| Job quests | About 31k EXP in total (doc 06) |
| Heritage Found FATEs at level 97–98 [3] | About 370k base EXP each, but only with a max-level helper party. Not solo |

As an inferred share of a level: a level 14 FATE gives about 12% of the level, a level 35 FATE about 3.9%, and a level 40 FATE about 3.5%. The Armoury bonus roughly doubles these.

## Multipliers [7][10][11]

- **Armoury bonus:** +100% up to level 89, on kills and FATE rewards. It is applied first.
- **Rested EXP:** +50%.
- **Food (Well Fed):** +3%.
- **FC buff:** "The Heat of Battle" I/II/III gives +5/10/15%.
- **Squadron Battle Manual:** +15%.
- **Rings, earrings and circlet:** the Brand-new Ring (+30% to level 30), Neophyte's Ring (+30% to 60), Menphina's or Azeyma's earrings (+30% to 80/90) and the Friendship Circlet (+20% to 25) are said to stack [11]. Whether they work on a limited job is unverified.

## Route by level band (guides [8][11][12])

| Levels | Where to FATE |
|---|---|
| 1–15 | Middle or Lower La Noscea, Central Shroud, Western Thanalan (the capture zones too) |
| 15–30 | South Shroud |
| 30–40 | Eastern La Noscea, then Coerthas Central Highlands; add board clears |
| 40–50 | Coerthas Central Highlands, Outer La Noscea, Northern Thanalan, Mor Dhona |

Prefer collect and normal FATEs to boss FATEs [4][12].

## FATE plugins (none usable over IPC)

- **FateFrenzy** (n0way02, AGPL-3.0, pushed 2026-10-02) [13]:
  - Teleports, paths with vnavmesh, syncs level and hops instances.
  - No IPC, and no limited-job handling.
- **autofate** (notmugi, no license, mostly AI-written by its author's account) [14]: no IPC.
- **SND scripts** (baanderson40) [15]: they need a separate "Fate Tool Kit" plugin that could not be found.

We therefore build a native FATE loop and copy nothing.

## APIs for a native FATE loop

**Dalamud** [17][18]:
- `IFateTable` lists FATEs.
- `IFate` gives `FateId`, `State`, `Progress`, `Level`, `MaxLevel`, `Position`, `Radius`, `TimeRemaining`, `HasBonus` and `HandInCount`.

**ClientStructs `FateManager`** [19]:
- `CurrentFate`, `SyncedFateId`, `LevelSync()` and `IsInFateRadius`.
- `FateState` values: Preparing 3, Running 4, Ending 5, Ended 7, Failed 8.
- Level sync: call `LevelSync()` when `SyncedFateId == 0` while not mounted, at most once a second (autofate's pattern [14]).

**Mounting:** `ActionManager.UseAction(ActionType.GeneralAction, 9)` (mount roulette; the same call dismounts).

**vnavmesh IPC** (`vnavmesh.` prefix) [20]:
- `Nav.IsReady`, `Nav.PathfindInProgress`
- `SimpleMove.PathfindAndMoveTo(Vector3, bool fly) -> bool`
- `SimpleMove.PathfindAndMoveCloseTo(Vector3, bool, float range) -> bool`
- `Path.IsRunning`, `Path.Stop`
- `Query.Mesh.PointOnFloor(Vector3, bool, float) -> Vector3?`

`Nav.PathfindCancelAll` reloads the mesh, so do not use it to cancel.

**Teleport:** native `Telepo.Instance()->Teleport(uint aetheryte, byte subIndex)` [22], or Lifestream's `Teleport(uint, byte)`, `IsBusy()` and `Abort()` [21].

**RotationSolver Reborn IPC** (`RotationSolverReborn.` prefix) [23]:
- `ChangeOperatingMode(StateCommandType)` (Off, Auto, Manual)
- `AutorotationActive()`
- `AddPriorityNameID`, `AddBlacklistNameID`
- `EnableTargetFreelyOverride()`, `DisableTargetFreelyOverride()`

Its defaults ignore non-FATE targets while you are in a FATE.

**BossMod Reborn IPC** [24]: `BossMod.Presets.SetActive(string)`, `ClearActive`, `GetActive`, `AddTransientStrategy`, and `BossMod.AI.IsNavigating`.

## Unknowns

1. The Beastmaster FATE bonus percentage, and the EXP per board clear and per capture.
2. Whether a solo low-level Beastmaster earns EXP in a high-level FATE.
3. Whether the EXP rings and earrings apply to a limited job.
4. Access to Hunt bills, Wondrous Tails, treasure maps and Cosmic Exploration.

## Sources

1. https://v2.xivapi.com/api/sheet/ParamGrow
2. https://ffxiv.consolegameswiki.com/wiki/Middle_La_Noscea_FATEs; [2b] /wiki/South_Shroud_FATEs; [2c] /wiki/Coerthas_Central_Highlands_FATEs; [2d] /wiki/Heritage_Found_FATEs
3. https://eorzeantavern.com/beastmaster-leveling/
4. https://ffxiv.consolegameswiki.com/wiki/Fate
5. https://ffxiv.consolegameswiki.com/wiki/Crucible_of_the_Unbroken
6. https://ffxiv.consolegameswiki.com/wiki/Beastmaster
7. https://ffxiv.consolegameswiki.com/wiki/Limited_Job
8. https://boostroom.com/blog/ffxiv-beastmaster-leveling-guide-fastest-way-to-reach-level-50
10. https://ffxiv.consolegameswiki.com/wiki/Experience_Points
11. https://mein-mmo.de/en/you-can-finally-almost-play-pokemon-in-ff14-we-show-you-how-to-level-up-effectively,1586720/2/
12. https://www.elyxir.gg/guide/final-fantasy-xiv/ffxiv-beastmaster-leveling-guide-best-solo-route-to-50-and-early-beast-captures
13. https://github.com/n0way02/FateFrenzy
14. https://github.com/notmugi/autofate
15. https://github.com/baanderson40/SND_Scripts/tree/main/FATEs
17. Dalamud `Dalamud/Plugin/Services/IFateTable.cs`; [18] `Dalamud/Game/ClientState/Fates/Fate.cs`
19. FFXIVClientStructs `FFXIV/Client/Game/Fate/FateManager.cs`
20. https://github.com/awgil/ffxiv_navmesh `vnavmesh/IPCProvider.cs`
21. https://github.com/NightmareXIV/Lifestream `Lifestream/IPC/IPCProvider.cs`
22. FFXIVClientStructs `FFXIV/Client/Game/UI/Telepo.cs`
23. https://github.com/FFXIV-CombatReborn/RotationSolverReborn `RotationSolver/IPC/IPCProvider.cs`, `Configuration/Configs.cs`
24. https://github.com/FFXIV-CombatReborn/BossmodReborn
