# Changelog

## [Unreleased]

### Changed

- BossMod Reborn is now the only mechanics plugin. The original BossMod no longer counts, and a session will not start while it is enabled, because it blocks movement while you cast. Installed but disabled is fine.

## [0.2.1] - 2026-10-11

### Added

- **Log tab and `/sux report`:** a combat log of every board, which you copy and paste into a bug report.
  - **What it records:**
    - each enemy cast: its area from the game data, where you stood, and whether you were inside when it went off
    - HP, statuses and KOs, for you and your beasts
    - enemies appearing, with their target priority
    - target changes
    - movement, and whether you walked into a marked area
    - vnavmesh paths, and whether a path ends inside an area
    - AutoDuty's state, the Crucible windows and system messages
  - **Copying:** "Copy summary" fits one Discord message; "Copy full log" copies everything. The last 30 logs are also saved in the plugin's config folder.
  - **Privacy:** your character's name is replaced with `<you>`.
- **Kill dangerous adds first (Settings, on by default):** in board battles, targets enemies that buff others (the Pas de Seul's mage) and adds with arena-wide attacks (the Ogre's wisps) first. The ranking comes from the game's enemy intel. It backs off for 30 s when something else keeps switching the target away.
- **Board run settings:** register only your 3 best beasts (on by default, for Beast Squad III), let AutoDuty use the board shop (off by default, for Coin Clutcher), and Score mode (no items, treasure or campsite rest).

### Changed

- Board runs now push AutoDuty's team cap of 3 and turn the shop off unless you change the new settings.
- The Plugins tab warns when the original BossMod is the mechanics plugin: it does not put the First Board's dangerous adds first, and BossMod Reborn does.

## [0.2.0] - 2026-10-11

### Added

- Session queue (Run tab, `/sux run`, `/sux stop`): queue boards, each run once or more, or fill the queue with what this sitting can cover. AutoDuty plays each board, and your rotation plugin and BossMod Reborn's AI fight.
- Guard rails before a session starts: the job, the companion plugins, a rotation plugin that can play Beastmaster, AutoDuty being idle and able to run each board, each board's readiness in queue order, and a rankings warning.
- Watchdog:
  - It stops a run that never reaches its board, or that shows no change inside a board for a set time (4 minutes by default).
  - It retries a run that failed outside the board, up to a set number of tries.
  - If anything goes wrong inside a board, it ends the session and leaves the decision to you.
- Return report in chat and as a notification: runs, outcomes, first clears, why the session ended and what is next.
- History tab: every run per character, with its outcome, duration and detail.
- **Journey tab** (`/sux journey`): three opt-in parts, each off by default.
  - **Job quests:** all 13 Beastmaster quests, scripted from the game's quest data. This includes the first-time job and gear setup, and the captures, levels and boards each quest needs.
  - **Beasts:**
    - wild captures: weaken the beast with auto-attacks, use Capture, then kill it
    - gourds you hold
    - Kornago shop purchases, with the exchange checked against the item name before confirming
    - remnant farming on the highest cleared board

    Remnants are spent only with this part on, never for the job quests alone.
  - **Levelling:**
    - FATEs ranked by expected EXP per second, with level sync
    - zones chosen per level band, switching when another has measured faster (a zone left for having no FATEs is not chosen again for 10 minutes)
  - **Safeguards:**
    - It reads its progress back from the game.
    - It stops after 15 minutes without progress or after 3 problems in a row. A beast that cannot be found is skipped after 12 minutes.
    - It fights back when something attacks on the way.
    - A board it starts runs to the end through the session queue. Stopping that session also stops the journey, and `/sux run` waits until the journey ends.
    - It waits for you, and tells you, when only you can act (putting a beast on a Battlehorn, or a window it does not expect).

### Changed

- The release is no longer read-only. Neither the session queue nor the journey has been tested in game yet, so stay nearby for the first runs.

## [0.1.0] - 2026-10-11

### Added

- Overview of each board's clear, third degree, Apex and Legendary, plus the Crucible achievements and their next tier.
- One-sitting check: whether you can clear every remaining board in one go and, if not, which board stops you and why. Also available as `/sux check`.
- Beasts list: what you have befriended, the ranks read from the Master's Bestiary, and where to find missing beasts.
- Experimental team suggestions per board and per battle.
- Companion plugin check for navigation, rotation, mechanics, the board driver, travel and dialogs.
- Rankings opt-in per board, off by default, for the automation releases to come.
