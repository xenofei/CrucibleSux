# Privacy

Crucible SUX has no network code. It sends nothing anywhere.

## What it reads, on your machine

- Your Beastmaster level and item level (equipped gear, or your saved Beastmaster gear set).
- Whether the Beastmaster quests and the five boards are complete.
- Your Crucible achievements, once the game has loaded the achievement list.
- Which beasts you have befriended, once the game has loaded the beast list.
- How many Faded and Bright Remnants of Resilience you hold.
- What a beast's page in the Master's Bestiary shows while it is open: the name, attributes and EXP.
- Which plugins are installed, their versions and repository URLs, for the Plugins tab.
- While a session runs:
  - the current zone
  - your position, HP and whether you are in combat
  - which Crucible windows are open
  - whether AutoDuty is running

  The watchdog uses these to tell a stuck run from a working one.
- While the journey runs, it also reads:
  - your quest progress for the Beastmaster quests, and your levels on the other combat jobs (for the unlock quest)
  - your Beastmaster EXP
  - the gourds in your inventory
  - your gear sets and Battlehorns
  - nearby characters, monsters and FATEs
  - the text of dialogue and shop windows that open, so it can answer only the ones it expects

## What it talks to

Only other plugins in the same game process, through Dalamud's IPC. When you start a session, it asks AutoDuty to run a board with temporary settings (Crucible mode, Standard degree, which rotation plugin to use). AutoDuty restores your own settings when it stops. While the journey runs, it asks vnavmesh to walk to places and switches Rotation Solver Reborn's auto-rotation on and off.

## What it stores

Dalamud's plugin configuration file (`CrucibleSux.json` in your pluginConfigs folder) stores:

- the boards you opted into the rankings
- per character, keyed by the game's numeric content id:
  - each beast's rank and EXP, and the time its page was last read
  - the run history: when each run started, the board, the attempt, the outcome, the duration and a one-line detail (up to 500 runs)
- the session queue and the session settings
- which parts of the journey you turned on

It does not store character names. To erase the per-character data:
- **Ranks:** Settings → Forget all saved ranks.
- **Runs:** History → Clear this character's history.
