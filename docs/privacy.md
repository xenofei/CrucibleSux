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

## What it stores

Dalamud's plugin configuration file (`CrucibleSux.json` in your pluginConfigs folder) stores:

- the boards you opted into the rankings
- per character, keyed by the game's numeric content id: each beast's rank, EXP and the time its page was last read

It does not store character names. Settings → Forget all saved ranks erases the per-character data.
