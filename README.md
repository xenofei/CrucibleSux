# Crucible SUX

**Set-and-forget Unbroken eXecutor**: a Dalamud plugin for the Beastmaster's Crucible of the Unbroken.

Queue the boards, step away, and come back to a report.

- **Run:** a queue of boards, each run once or more. Guard rails are checked before it starts:
  - the job
  - the companion plugins
  - each board's readiness, judged in queue order
  - the rankings

  AutoDuty plays each board, with your rotation plugin and BossMod Reborn's AI. Crucible SUX handles the rest:
  - it watches every run and stops one that is stuck
  - it retries a run that failed outside the board, and stops the session if anything goes wrong inside one
  - it reports in chat and with a notification
- **History:** every run per character, with its outcome, duration and first clears.
- **Overview:** each board's clear, third degree, Apex and Legendary, plus the Crucible achievements and how far you are from each tier.
- **One sitting:** whether you can clear every remaining board in one go and, if not, which board stops you and why. It checks job level, unlock quests, beasts befriended, item level, roster size and beast ranks.
- **Beasts:** all 50 beasts, which you have befriended, their ranks and where to find the ones you are missing. Ranks are read when you open a beast's page in the Master's Bestiary.
- **Teams (experimental):** a suggested roster per board and three beasts per battle, scored from the game's sheets.
- **Plugins:** checks the companion plugins:
  - vnavmesh
  - Rotation Solver Reborn or BossMod Reborn
  - BossMod Reborn's AI
  - AutoDuty (erdelf's build)
  - Lifestream
  - TextAdvance

Commands:
- `/sux` opens the window.
- `/sux check` prints what this sitting can cover.
- `/sux run` starts the queue, and `/sux stop` stops it.

The session queue has not yet been tested in game. Treat it as a beta and stay nearby for the first runs.

Still to come:
- route, treasure and shop decisions made by Crucible SUX itself
- EXP and capture loops
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
