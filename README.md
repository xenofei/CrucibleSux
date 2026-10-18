# Crucible SUX

**Set-and-forget Unbroken eXecutor**: a Dalamud plugin for the Beastmaster's Crucible of the Unbroken.

Queue the boards, step away, and come back to a report.

- **Run:** a queue of boards, each run once or more. Guard rails are checked before it starts:
  - the job
  - the companion plugins
  - each board's readiness, judged in queue order
  - the rankings

  AutoDuty plays each board, with your rotation plugin and BossMod's AI. Crucible SUX handles the rest:
  - it watches every run and stops one that is stuck
  - it retries a run that failed outside the board, and stops the session if anything goes wrong inside one
  - it reports in chat and with a notification

  By default a run registers only your 3 best beasts (the Beast Squad III bonus) and keeps out of the board shop (Coin Clutcher). Score mode in Settings also skips items, treasure and campsite rest. In board battles it targets the dangerous adds first, from the game's own enemy intel: the Pas de Seul's mage and the Ogre's wisps.
- **History:** every run per character, with its outcome, duration and first clears.
- **Journey (opt-in, each part off by default):** takes a character from the unlock quest to level 50 with all 50 beasts.
  - **Job quests:** all 13, including the captures, levels and boards each quest asks for.
  - **Beasts:** captures the wild ones, uses gourds you hold, and buys the rest from the Kornago merchant (farming remnants on the highest cleared board when short). Remnants are spent only with this part on.
  - **Levelling:** FATEs in the zones that suit each level band, picked by expected EXP per second, with level sync. It moves zone when another one has measured faster.

  It needs vnavmesh and Rotation Solver Reborn. A character that can take the unlock quest but has not is offered the unlock on its own: Crucible SUX picks the quest up, plays it and sets up Beastmaster's gear, and can go on with the job quests until the First Board opens. You put beasts on Battlehorns yourself when asked, because no API to do it is known yet.
- **Overview:** each board's clear, third degree, Apex and Legendary, plus the Crucible achievements and how far you are from each tier.
- **One sitting:** whether you can clear every remaining board in one go and, if not, which board stops you and why. It checks job level, unlock quests, beasts befriended, item level, roster size and beast ranks.
- **Beasts:** all 50 beasts, which you have befriended, their ranks and where to find the ones you are missing. Ranks are read when you open a beast's page in the Master's Bestiary.
- **Teams (experimental):** a suggested roster per board and three beasts per battle, scored from the game's sheets.
- **Plugins:** checks the companion plugins:
  - vnavmesh
  - Rotation Solver Reborn or BossMod Reborn
  - BossMod's AI: BossMod Reborn or the original BossMod. The first session or journey asks which one you use (you can change it in Settings). Both answer AutoDuty under the same names, so only that one may be loaded during a run: Crucible SUX offers to turn the other off for the run and back on afterwards. Your plugin settings stay as they are.
  - AutoDuty (erdelf's build)
  - Lifestream
  - TextAdvance

  A plugin older than the release Crucible SUX was last tested with shows a warning.
- **Log:** a combat log of each board, for bug reports. It records:
  - every enemy cast, with its area from the game data and where you stood
  - hits, HP, statuses and KOs, for you and your beasts
  - targets, movement and vnavmesh paths
  - AutoDuty and the Crucible windows

  "Copy summary" copies one Discord-sized message; "Copy full log" copies everything. Your character's name is replaced with `<you>`.

Commands:
- `/sux` opens the window.
- `/sux check` prints what this sitting can cover.
- `/sux run` starts the queue.
- `/sux journey` starts the Beastmaster journey.
- `/sux stop` stops either.
- `/sux report` copies the latest combat log summary; `/sux report full` copies the whole log.

Neither the session queue nor the journey has been tested in game yet. The journey was built from the game's data and public guides only. Treat both as a beta and stay nearby for the first runs.

Still to come:
- route, treasure and shop decisions on the boards made by Crucible SUX itself
- mechanics for the Crucible bosses

See [docs/PLAN.md](docs/PLAN.md).

## Install

Crucible SUX is not on the official Dalamud repository. To add its custom repository:

1. In game, open `/xlsettings`, then **Experimental**.
2. Under **Custom Plugin Repositories**, add `https://raw.githubusercontent.com/xenofei/CrucibleSux/main/pluginmaster.json`, tick it, and save.
3. Open `/xlplugins`, search for **Crucible SUX**, and install it.

Each release lists the SHA-256 of `latest.zip` and `CrucibleSux.dll` so you can check what you installed.

## A word on risk

Unattended automation breaks the game's terms of service, so use the session queue at your own risk. The dashboard tabs only read game state.
Crucible SUX sends no game input itself: it asks AutoDuty to run a board and to stop.

## Build

You need the .NET 10 SDK and Dalamud's dev files (XIVLauncher installs them, or set `DALAMUD_HOME`).

```
dotnet build CrucibleSux.sln -c Release -warnaserror
dotnet test --project CrucibleSux.Tests
dotnet format CrucibleSux.sln --verify-no-changes
```

| Folder | What it holds |
|---|---|
| `CrucibleSux.Core` | The rules (readiness, progress, ranks, teams, dependencies, the session orchestrator and its guard rails). Plain .NET, unit tested. |
| `CrucibleSux` | The plugin: game reads, the Bestiary reader, the AutoDuty adapter, the session runner and the window. |
| `tools/CrucibleSux.DataDump` | Dumps the Crucible's sheets on patch day ([docs/data/xbm-columns.md](docs/data/xbm-columns.md)). |

## Licence

MIT. See [LICENSE](LICENSE).

The wild-beast spawn points in `CrucibleSux.Core/BeastSources.cs` are derived from [FFXIV Teamcraft](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft)'s `monsters.json` (MIT, Copyright (c) 2017 Flavien Normand).
