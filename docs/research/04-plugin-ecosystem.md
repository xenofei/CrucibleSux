# Plugin ecosystem for Crucible SUX

Research date: 2026-10-11. Some IPC listings were not verified because GitHub returned 403 near the end of the research; those are marked.

## Dalamud baseline

| Item | Value | Source |
|---|---|---|
| API level | 15 on stable (Dalamud 15.0.3.6, game 2026.09.15). `api16` branches exist but are unreleased. | [kamori VersionInfo](https://kamori.goats.dev/Dalamud/Release/VersionInfo?track=release) |
| SDK | `Dalamud.NET.Sdk/15.0.0`; manifest fields live in the csproj | [SamplePlugin.csproj](https://raw.githubusercontent.com/goatcorp/SamplePlugin/master/SamplePlugin/SamplePlugin.csproj) |
| TFM | SDK default (AutoDuty pins `net10.0-windows8.0`) | [AutoDuty.csproj](https://raw.githubusercontent.com/erdelf/AutoDuty/master/AutoDuty/AutoDuty.csproj) |
| Custom repo | JSON array (`InternalName`, `AssemblyVersion`, `DalamudApiLevel`, `DownloadLink*`, `LastUpdate`…), served by plain GET; users add it in /xlsettings > Experimental | [dalamud.dev](https://dalamud.dev/plugin-publishing/custom-repositories) |
| Publishing | Same pattern as the owner's Tsukimichi repo: a tag build, `latest.zip` on GitHub Releases, `pluginmaster.json` refreshed on main | `Tsukimichi (Main Repo)/.github/workflows/release.yml` |

## Plugins that already touch Beastmaster or the Crucible

### AutoDuty (erdelf fork)

- **Status:**
  - Version 0.0.0.382 is installed on this machine. Repo: https://github.com/erdelf/AutoDuty, custom repo `https://puni.sh/api/repository/erdelf`.
  - **No license.** The upstream ffxivcode/AutoDuty has none either, so we can call it over IPC but must not copy its code.
- **Crucible support:**
  - The code is about 120 KB across `Managers/CrucibleManager.cs` and `Managers/Crucible/`: Board, BoardReader, BoardWindow, ItemData, Menus, Team and Ui.
  - It is under active development: the first commit was 2026-09-17, and commits were still landing daily through 2026-10-09.
- **What it covers:**
  - Walking to Lauda (territory 148, position (25.5, −6, 67.5)).
  - The Lauda menus.
  - A Bestiary scan with ranks.
  - Team setup modes.
  - Board selection, with Duty Finder IDs 1088–1092 mapped to boards 1–5.
  - Degree selection, once quest 71045 is complete.
  - Commencing a board.
  - Reading the board graph (stops and paths).
  - Moving along the board, with a path search that prefers a campsite before the next fight.
  - Feeding.
  - Blocking items in shops and treasure.
  - A kin filter.
- **What it does not cover:** combat. Its README requires Wrath or RSR for the rotation and BossMod or BossMod Reborn for mechanics.
- **IPC** (`ECommons.EzIPC`, prefix `AutoDuty.`):
  - `ListConfig()`, `GetConfig(string)`, `SetConfig(string, object)`.
  - `PushConfigOverrides(object)` and `PopConfigOverrides()`.
  - `Run(uint territoryType, int loops = 0, bool bareMode = false)`, `Start(bool)`, `Stop()`.
  - `IsNavigating()`, `IsLooping()`, `IsStopped()`, `ContentHasPath(uint)`, `SetLevelingMode(int)`.
  - Crucible behaviour is driven by its `Configuration.Meta.Crucible.*` settings (TeamMode, Degree, camps, item blocks…), which can be set through `SetConfig` or `PushConfigOverrides`.
  - There is no IPC for board state, run events or results.

### RotationSolverReborn

- Installed: 7.5.6.21.
- Beastmaster support arrived in PRs #1366–#1372 (2026-09-11 to 09-13).
- It detects the Crucible by territory 1339–1343.
- Its capture and Crucible helpers are commented out. See 05-client-internals.md.
- IPC: `ChangeOperatingMode(StateCommandType)`, `AutorotationActive()` and 15 other `[EzIPC]` methods ([IPCProvider.cs](https://raw.githubusercontent.com/FFXIV-CombatReborn/RotationSolverReborn/main/RotationSolver/IPC/IPCProvider.cs)).

### BossMod Reborn

- Installed: 7.5.6.34.
- It has a Beastmaster autorotation module, `Autorotation/Standard/xan/Melee/BST.cs`, marked WIP; last commit 2026-09-17.
- There is no Crucible boss module in `Modules/Dawntrail`.
- IPC ([IPCProvider.cs](https://raw.githubusercontent.com/FFXIV-CombatReborn/BossmodReborn/master/BossMod/Framework/IPCProvider.cs)):
  - `BossMod.Presets.Get/Create/SetActive/ClearActive`.
  - `BossMod.AI.SetPreset(string)`, `AI.IsNavigating`, `AI.PauseMovement(bool)`.
  - `BossMod.Hints.IsPositionSafe(Vector3)`, `Hints.ArenaCenter`, `Hints.NextDamageIn`.
  - `BossMod.HasModuleByDataId(uint)`, `ActiveModuleName`.
  - Chat commands `/vbmai on` and `/vbm cfg …` (as Questionable uses them).

### Others that touch Beastmaster

- **anmili2022/Beastmaster:** display and auto-output, no license, no IPC seen.
- **BeastmasterAssist (Kriss-Hietala):** overlay only.

## The rest of the stack

| Plugin | Installed | IPC we would use | Notes |
|---|---|---|---|
| vnavmesh | 1.2.3.14 | `vnavmesh.Nav.IsReady`, `SimpleMove.PathfindAndMoveTo(Vector3,bool)`, `Path.IsRunning`, `Path.Stop` | Hard dependency for walking to Lauda and the Kornago Merchant |
| Wrath Combo | 1.0.4.26 | Lease model: `RegisterForLease`, `SetAutoRotationState`, `ReleaseControl` | No Beastmaster mention in the IPC doc |
| Lifestream | 2.5.4.23 | `Teleport(uint,byte)`, `AethernetTeleport(string)`, `IsBusy()`, `Abort()` | Teleport to Bentbranch Meadows |
| TextAdvance | 3.3.0.1 | `TextAdvance.EnableExternalControl(string, ExternalTerritoryConfig)` | Unverified |
| Pandora's Box | 1.6.4.5 | `PandorasBox.SetFeatureEnabled(string,bool)` | Optional |
| YesAlready, Splatoon, SomethingNeedDoing | — | Not verified | Not needed for the core loop |
| ECommons | NuGet 3.2.1.22 (2026-10-02) | — | `ECommonsMain.Init(pi, this)`; gives TaskManager, EzIPC and addon helpers |

## Conflicts to manage

- **One rotation engine at a time:** RSR, Wrath and BossMod Reborn autorotation must not all be on.
- **AutoDuty's own combat settings** start whichever rotation plugin AutoDuty is configured with. Crucible SUX must agree with it.
- **Two board drivers:** if Crucible SUX ever drives the board itself, AutoDuty must not also be running in the Crucible.

## Sources

- https://raw.githubusercontent.com/erdelf/AutoDuty/master/AutoDuty/Managers/CrucibleManager.cs
- https://raw.githubusercontent.com/erdelf/AutoDuty/master/AutoDuty/IPC/IPCProvider.cs
- https://raw.githubusercontent.com/erdelf/AutoDuty/master/README.md
- https://raw.githubusercontent.com/awgil/ffxiv_navmesh/master/vnavmesh/IPCProvider.cs
- https://raw.githubusercontent.com/PunishXIV/WrathCombo/main/docs/IPC.md
- https://raw.githubusercontent.com/NightmareXIV/Lifestream/main/Lifestream/IPC/IPCProvider.cs
- https://raw.githubusercontent.com/PunishXIV/Questionable/main/Questionable/External/TextAdvanceIpc.cs
- https://raw.githubusercontent.com/PunishXIV/Questionable/main/Questionable/External/PandorasBoxIpc.cs
- https://www.nuget.org/packages/ecommons
