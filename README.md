# Crucible SUX

**Set-and-forget Unbroken eXecutor**: a Dalamud plugin for the Beastmaster's Crucible of the Unbroken.

The goal is to queue the boards, step away, and come back to a report. This first release is the read-only part:

- **Overview:** each board's clear, third degree, Apex and Legendary, plus the Crucible achievements and how far you are from each tier.
- **One sitting:** whether you can clear every remaining board in one go and, if not, which board stops you and why. It checks job level, unlock quests, beasts befriended, item level, roster size and beast ranks.
- **Beasts:** all 50 beasts, which you have befriended, their ranks and where to find the ones you are missing. Ranks are read when you open a beast's page in the Master's Bestiary.
- **Teams (experimental):** a suggested roster per board and three beasts per battle, scored from the game's sheets.
- **Plugins:** checks for the companion plugins the automation will drive: vnavmesh, Rotation Solver Reborn or BossMod Reborn, BossMod Reborn's AI, AutoDuty (erdelf's build), Lifestream and TextAdvance.

`/sux` opens the window. `/sux check` prints what this sitting can cover.

The automation itself comes in later releases. They will run board movement through AutoDuty, combat through your
rotation plugin and BossMod Reborn, and recovery and tracking through this plugin. See [docs/PLAN.md](docs/PLAN.md).

## Install

Crucible SUX is not on the official Dalamud repository. To add its custom repository:

1. In game, open `/xlsettings`, then **Experimental**.
2. Under **Custom Plugin Repositories**, add `https://raw.githubusercontent.com/xenofei/CrucibleSux/main/pluginmaster.json`, tick it, and save.
3. Open `/xlplugins`, search for **Crucible SUX**, and install it.

Each release lists the SHA-256 of `latest.zip` and `CrucibleSux.dll` so you can check what you installed.

## A word on risk

Unattended automation breaks the game's terms of service. This release only reads game state and sends no input.
The later automation releases will not change that rule, so use them at your own risk.

## Build

You need the .NET 10 SDK and Dalamud's dev files (XIVLauncher installs them, or set `DALAMUD_HOME`).

```
dotnet build CrucibleSux.sln -c Release -warnaserror
dotnet test --project CrucibleSux.Tests
dotnet format CrucibleSux.sln --verify-no-changes
```

| Folder | What it holds |
|---|---|
| `CrucibleSux.Core` | The rules (readiness, progress, ranks, teams, dependencies). Plain .NET, unit tested. |
| `CrucibleSux` | The plugin: game reads, the Bestiary reader and the window. |
| `tools/CrucibleSux.DataDump` | Dumps the Crucible's sheets on patch day ([docs/data/xbm-columns.md](docs/data/xbm-columns.md)). |

## Licence

MIT. See [LICENSE](LICENSE).
