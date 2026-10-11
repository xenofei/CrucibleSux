# Crucible SUX: plan

**Crucible SUX = Set-and-forget Unbroken eXecutor.** You queue the boards, step away, and come back to a report.

Status: draft for the owner's approval, 2026-10-11. The research behind this plan is in [docs/research/](research/) (01 mechanics, 02 player gripes, 03 builds, 04 plugin ecosystem, 05 client internals).

## 1. What the research changed

1. **The Crucible is a board game, not a dungeon.** Each board is a fixed graph of spaces (Enemy, Elite, Boss, Campsite, Shop, Treasure, Random). A branch you take locks out the others. Battles happen in small arenas.
   - "Movement through the instance" means choosing spaces on the board, plus positioning inside a battle arena.
   - Navmesh is needed only for the overworld: reaching Lauda and the Kornago Merchant in Central Shroud (territory 148), and capture trips.
2. **AutoDuty (erdelf fork, already installed) has a working Crucible engine.** It is about 120 KB of code and has changed almost daily since 2026-09-17.
   - It covers walking to Lauda, the menus, a Bestiary scan with ranks, team setup, degree selection, reading the board graph, moving along the board, campsite routing, feeding and item blocking.
   - It has **no combat** and **no license**. We can drive it through IPC (`Run`, `Stop`, `SetConfig`, `PushConfigOverrides`, `IsStopped`…) but cannot copy its code.
3. **Combat exists, but it is young.**
   - RotationSolverReborn shipped a Beastmaster rotation on 2026-09-13.
   - BossMod Reborn has a work-in-progress Beastmaster module (`xan/BST.cs`).
   - **No boss-mechanic modules exist for any Crucible boss.**
4. **The client internals are mostly mapped:**
   - The XBM* Excel sheets: beasts, boards, battles, elements, items, score bonuses.
   - `BeastmasterGauge`, `XBMManager` (unlocked beasts), and `AgentXBMPetParty` (team HP and rank).
   - The Crucible director: inventory and `UseItem`.
   - `ActionManager._beastmasterPets` (the Battlehorn picks).
   - Still unmapped: per-beast EXP outside the party agent, degree and clear history, the board-map agent and the capture API. We must scrape addons or reverse these ourselves.
5. **Community "optimal builds" don't exist yet.** The game data is enough to compute them: enemy elements and resistances per battle, beast stats per rank, and the ×1.3 weakness bonus. The recommender works from that data instead of from guides.

## 2. Honest limits

**"Step away and be 100% confident" is the goal, not a promise.** The plugin will be built to recover, never to wedge, and to tell you exactly what happened. These risks stay:

- **No mechanic modules for the bosses:** Guttler, Lauda the Spellcleaver, Borgny, Toxic Mass and Thanatos. Until we write them, dodging relies on BossMod Reborn's generic AI. The Third and Master's boards will fail more often than a human would.
- **Capture is RNG** ("non-guaranteed") for rare beasts. Kornago Gourds are the guaranteed fallback, and the plugin will say so.
- **Upstream breakage:** AutoDuty, RSR and BossMod Reborn change weekly. Every integration goes through a version-checked adapter with a kill switch, like Tsukimichi's HookGate.
- **Terms of Service:** automation breaks Square Enix's ToS. The plugin runs only solo content, but the account risk is yours.
- **Leaderboards:** automated runs that count toward the Season rankings compete with real players. **Default: "Participate in rankings" is OFF for automated runs.** You can override it per board.

## 3. Architecture

```
CrucibleSux (Dalamud plugin: windows, commands, IPC adapters, game readers)
 ├─ Game/            readers: sheets (Lumina), XBMManager, AgentXBMPetParty, director, gauge,
 │                   quests/achievements, inventory currencies, gear; addon scrapers
 ├─ Integrations/    one adapter per plugin, each with a detect + version gate + kill switch:
 │                   AutoDuty, vnavmesh, Lifestream, RSR, Wrath, BossMod/BMR, TextAdvance
 ├─ Automation/      Session orchestrator (state machine on ECommons TaskManager):
 │                   Plan → Prepare → Travel → Run board → Recover → Collect → Report
 └─ Ui/              Dashboard, Readiness, Session queue, Run history, Settings
CrucibleSux.Core     pure logic, no Dalamud: models, readiness rules, team recommender,
                     route/score planner, EXP farm planner, item scoring, session plan
CrucibleSux.Tests    xUnit on Core with fixtures captured from the game
```

**Executors.** The orchestrator decides what to do; an executor carries it out on the board.

- **AutoDutyExecutor (phase 2):** writes Crucible SUX's plan into AutoDuty's Crucible settings, then calls `Run(territory)`. It watches progress through `IsStopped`/`IsNavigating`, the director state and the territory.
- **NativeExecutor (phase 3):** Crucible SUX's own board, treasure and shop decisions, for the choices AutoDuty does not expose. The design keeps both executors so we never depend on AutoDuty's internals.

**Combat controller.**

- One rotation engine at a time. Default order: RSR, then BossMod Reborn's BST module, then Wrath. It is leased per battle and released after.
- BossMod Reborn AI handles movement and dodging, set through its preset IPC.
- Battlehorn reassignment before every battle; the game doesn't save it.
- Snarl/Challenge use, a beast swap at low HP, and a Blessed Horn revive through the director's `UseItem`.

## 4. Features

Each feature names the player gripe it answers (from 02-player-gripes.md).

| # | Feature | Kind | Gripe |
|---|---|---|---|
| F1 | **Progress dashboard:** boards cleared and best rank, degrees, the Crucible achievements (4023–4077) with progress, beasts befriended (n/50), beasts at rank 25, roster ranks and EXP, Bright/Faded Remnants, Kornago Gourds, gear tier, job quests (what's left vs done) | information | info overload, quest gating |
| F2 | **Readiness check per board and for a single sitting:** job level, item level, gear tier, roster size vs cap, beast ranks vs recommended, unlock quests, owned key beasts. Green/amber/red per board, with the exact missing items and how to get each one | information | the Third Board difficulty spike |
| F3 | **Team recommender:** picks the best roster and the 3-beast team for each battle from enemy elements and resistances (XBMBattleDetail/XBMElement) and beast stats at their current ranks; saves presets | decision | roster and weakness overload |
| F4 | **Route and score planner:** the best path through the fixed graph for the goal (clear safely / Legendary / Degree), counting heal stock, campsites, shop cost (−500) and bonus conditions; a live score panel | decision | healing drops, scoring |
| F5 | **Session queue:** "clear everything I can": orders the boards, runs them back to back, suspends and resumes on trouble, and stops at a goal (all boards cleared / every beast rank ≥ N / Legendary on X) | automation | the whole loop |
| F6 | **EXP farm planner and loop:** the cheapest board for the beasts that are behind (255 EXP cap per board, smaller rosters earn more), looping until the target ranks are reached | automation | rank grind |
| F7 | **Capture planner and runs:** missing beasts with zone, coordinates and level (XBMPet.Location); an attempt counter; teleport, walk and Capture; suggests a Kornago Gourd after N failures | automation | capture RNG |
| F8 | **Gear and remnants:** equips the best Beastmaster gear, then spends Remnants on the upgrade chain at the Kornago Merchant | automation | gear grind |
| F9 | **Watchdog and recovery:** detects being stuck (no state change for N s), a crash out of the duty, a disconnect or a failed run. It recovers through Resume Progress where possible and otherwise stops safely, never looping forever | automation | unattended trust |
| F10 | **Return report:** what ran, results, ranks, EXP gained, captures, what failed and why, what's next. Shown in a window, chat and a toast. Local only, no network | information | "how did it go?" |
| F11 | **Run history:** every attempt stored locally (path, items, score, rank, duration); the last 3 per board feed a projected ranking score | information | rankings opacity |
| F12 | **Guard rails:** empty-roster warning, potions-disabled notice, food reminder, an "ask before forfeit" option | information | UI gaps |
| F13 | **Boss mechanic hints for the Crucible bosses:** phase 5. Written as BossMod Reborn modules and offered upstream, so the whole community gets them | automation | difficulty spike |

## 5. Phases

| Phase | Delivers | In-game risk | Proof |
|---|---|---|---|
| 0 Scaffold | Repo, solution (plugin + Core + Tests), CI and release workflows modelled on Tsukimichi, `pluginmaster.json`, icon, README, privacy note | none | `dotnet build -c Release -warnaserror` and `dotnet test` green in CI |
| 1 Read-only | F1, F2, F3, F11 data layer; dependency checker window (which plugins are installed, enabled and new enough) | none (reads only) | unit tests on Core; in-game screenshot of the dashboard on your character |
| 2 Orchestrator | F5, F9, F10 using AutoDutyExecutor and the combat controller; F12 | medium | First and Second Board cleared unattended 3× each on your character |
| 3 Native decisions | F4 and NativeExecutor (treasure and shop picks, route) | medium | a live UI trace per addon, then Third Board clears |
| 4 Grind tools | F6, F7, F8 | medium | captures and upgrades seen working in-game |
| 5 Mechanics | F13 modules for the Third and Master's bosses | high | Master's Board clears |

**Status (2026-10-11):**
- Phase 0 is built.
- Phase 1 ships F1, F2, F3 (labelled experimental) and the dependency checker.
- F11 (run history) moves to Phase 2: nothing records runs until the orchestrator does.
- `pluginmaster.json` is written by the first tagged release.
- v0.1.0 was released on 2026-10-11, and the dashboard was seen in game on a character without Beastmaster.

**Status of Phase 2 (2026-10-11): built, not yet tested in game.**

- **Session queue and orchestrator** (F5, F9): `CrucibleSux.Core/Orchestrator.cs`.
- **Guard rails** (F12): `Preflight.cs`.
- **Run history and report** (F10, F11): `RunHistory.cs`.
- **AutoDuty adapter:** `CrucibleSux/Integrations/AutoDutyIpc.cs`, which uses IPC only. Each run does the following:
  - It pushes temporary overrides: Crucible duty mode, Standard degree, auto-exit, and the rotation plugin (RSR, else BossMod Reborn).
  - It calls `Run(territory, 1, bareMode: true)`. Bare mode skips AutoDuty's loop actions, such as logout or killing the client.
  - The adapter's version gate: `ContentHasPath` must answer for every board, and AutoDuty validates every override key.
- **Combat controller:** AutoDuty already switches the chosen rotation plugin and BossMod's AI on when a board starts and off when it stops. The controller is therefore the choice of engine plus that override. Battlehorn and item handling stay with AutoDuty until Phase 3.
- **Not done yet:**
  - "Participate in rankings" cannot be set. AutoDuty does not touch it, so preflight warns instead.
  - Recovery through Resume Progress is not built. Anything that goes wrong inside a board stops the session.
- **Unverified until a play-test:**
  - whether `XBMResult` appears after a lost board
  - whether AutoDuty's `IsStopped` stays false through zoning
  - the 4-minute stuck default

**Status of Phase 2b, the Beastmaster journey (2026-10-11): built, not yet tested in game.** The owner asked on 2026-10-11 for the job quests, levelling and beast collection, each opt-in. It was built from game data and public sources only (docs/research/06–10), because the owner has no Beastmaster to trace.

- **Core (tested):**
  - `QuestScripts.cs`: the 13 quests as step scripts.
  - `BeastSources.cs`: all 50 beasts.
  - `KornagoShop.cs`: the two exchange shops and their visible line order.
  - `Leveling.cs`: the EXP table, the FATE picker, zones per band, the EXP-per-hour meter, and capture tactics.
  - `Progression.cs`: the planner. Gourds are bought, and remnants farmed, only with the beasts option on.
  - `JourneyRules.cs`: what counts as progress for the watchdog, how a board run ended, and the levelling zone choice.
- **Fresh-context review (2026-10-11):** 3 high, 7 medium and 6 low findings, all fixed. A board the journey starts stays its own until it ends; a clear is not a failure; a player stop from the Run tab stops the journey; `/sux run` waits for the journey; the watchdog counts only game progress and also catches a window that never closes; it fights back when attacked on the way; RSR is switched off outright at start, at stop and after a board.
- **Plugin:**
  - `Automation/ProgressionRunner.cs`: the tick loop, the watchdog (15 minutes) and the 3-strike stop.
  - `GameActions.cs`: interact, actions, emotes, items, horns, addons and gear.
  - `Travel.cs`: teleport to the nearest attuned aetheryte, mount, then vnavmesh.
  - `Dialogue.cs`: answers only the windows it expects.
  - `Game/ProgressionReader.cs`.
  - `Integrations/NavmeshIpc.cs` and `RotationSolverIpc.cs`.
  - `Ui/JourneyTab.cs` and `/sux journey`.
- **Boards** the quests or remnant farming need run through the Phase 2 session runner, one board at a time.
- **Known gaps:**
  - No API is known for putting a beast on a Battlehorn, so the runner asks the player and resumes by itself.
  - Quest menu answers are matched against English text.
  - Collection FATEs (hand-ins) are skipped.
- **Unverified until a play-test:**
  - every step script (positions, the sequences, which steps need an item, an emote or a fight)
  - the Talk click and JournalAccept (44) and JournalResult (37) buttons
  - the shop flow: the SelectIconString entry names, the `ShopExchangeItem` callback `[0, index, quantity]`, the dialog confirm `[0]`, and whether quest-gated lines are hidden
  - capture: whether auto-attacks alone weaken the beast, and the odds
  - wild-beast spawn points (Teamcraft and the wiki; the Central Shroud ones are wiki only)
  - FATE EXP against the wiki table
  - the gear setup after the unlock quest
  - RSR's Manual mode attacking the current target

**Unverifiable without the game:** in-game behaviour, the addon callback indexes, the capture flow, and boss timing. Phases 2–5 need sessions with the game running. I can launch and drive it per your global rules, or you play-test.

## 6. Decisions (owner, 2026-10-11)

1. **AutoDuty strategy: hybrid.** Drive AutoDuty over IPC for board movement now, and build the native executor behind it.
2. **Rankings: OFF by default** for automated runs. The owner can opt in per board.
3. **Scope of the first session: Phases 0–1.** Phase 2 follows, with in-game testing.
4. **Name: Crucible SUX = Set-and-forget Unbroken eXecutor.**

## 7. Dependencies

- **Hard:** vnavmesh; one rotation engine (RSR recommended, since it has a BST rotation; BossMod Reborn's BST module is WIP); BossMod Reborn for AI dodging; AutoDuty (erdelf) while the hybrid executor depends on it.
- **Optional:** Lifestream (travel and captures), TextAdvance (dialogs).
- **Library:** ECommons from NuGet (TaskManager, EzIPC, addon helpers).
- **Build:** Dalamud.NET.Sdk 15.0.0, API 15, .NET 10. An `api16` CI job will be added the same way Tsukimichi does it.
