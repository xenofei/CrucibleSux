# Changelog

## [Unreleased]

### Fixed

- **A capture that never starts the fight (the cause):** the journey read the game's auto-attack flag, which FFXIVClientStructs notes no longer holds the state in this patch. It read "already attacking", so the journey never sent the attack.
  - The 0.3.7 log showed the character in reach of the squirrel for 26 s with nothing sent.
  - The journey now treats combat starting as the sign the fight is on. Until then it sends the attack action twice, then the hotbar's Auto-attack twice, then Capture, about 1.5 s apart, logging each try and what the game returned.

## [0.3.7] - 2026-10-18

### Fixed

- **A capture that never starts the fight:** in the play-test the character stood next to a ground squirrel for minutes and the game answered "Cannot execute at this time", because the auto-attack it sent was refused. If the game refuses the "attack" action, the journey now tries the hotbar's Auto-attack, and if that is refused too, it opens with Capture, which is an attack itself.
  - Each capture step goes into the plugin log: the move, the HP, whether the beast is in reach, and any refusal with the game's code.

## [0.3.6] - 2026-10-18

### Changed

- **Travel flies and teleports more:**
  - Where this character can fly (A Realm Reborn's zones after its main scenario), it summons a flying mount and flies.
  - A trip of more than 400 y in the same zone teleports to an attuned aetheryte when that saves 300 y or more. A character already mounted rides on instead.

### Fixed

- **Capture spam:** Capture and auto-attack are now sent only when the game would accept them, so a refusal no longer fills the chat with "cannot execute". The game's refusal code goes into the status line and the plugin log, to show why.
- **Turning back and forth while alt-tabbed:** the game's "limit frame rate when client is inactive" makes vnavmesh overshoot and turn back while the game is not focused. The Journey tab and the journey's start now say so, and say how to turn it off. Your settings are not changed.

## [0.3.5] - 2026-10-18

### Fixed

From the first play-test of the journey:

- **Captures never started.** A wild beast that targets nobody holds the game's "no object" id, not 0, so every free squirrel looked as if it were fighting someone else, and the search walked past them. They are now recognised.
- **Attacks were not fought back.** The character used to stand still: Rotation Solver Reborn fights but never moves the character, and a mounted character cannot fight. When something attacks the character or its beast, the character now:
  - gets off its mount
  - targets the attacker and walks to it while the rotation fights
  - does this during travel, quests, captures, FATEs, gourds and the Kornago merchant
  - deals with an attacker before the prey during a capture, and before the FATE's own enemies in a FATE

### Added

- **Fight or flee outside the Crucible** (docs/research/18-threats.md):
  - **Flee:** the character flees instead of fighting when an attacker is 3 or more levels above it, when 3 or more enemies are on it (not counted in FATEs), or when its HP would fall below the floor within 5 s at the rate it is dropping.
  - **Where it runs:** with Sprint, back the way it came (to where it was last out of combat, at least 40 y back), until the enemies give up, then it resumes.
  - **Mounted, it stays mounted:** riding, it rides on; standing on the mount, it rides away.
  - **Giving up:** after a minute of fleeing without getting away, it fights.
  - **Dangerous spots:** a spot that made it flee twice is avoided for capture searches and FATEs.
- **KO recovery in every activity:** a KO'd character accepts the return to its home point, the run counts one problem and goes on. This used to work only while levelling.

## [0.3.4] - 2026-10-18

### Added

- **Go on to the Crucible:** after the unlock, Crucible SUX can carry on with the Beastmaster job quests until "Into the Crucible" opens the First Board, then stop.
  - Along the way it levels to each quest's level and captures the beasts the quests ask for (15 by "Into the Crucible", level 30). It does this whatever the Journey tab's parts say.
  - The offer window now has "Unlock and go on to the Crucible" next to "Unlock Beastmaster". A character that already has Beastmaster but not the Crucible is offered "Go on to the Crucible" instead.
  - The Journey tab has the same buttons.
  - No board runs before it stops, so the BossMod question is skipped.

## [0.3.3] - 2026-10-18

### Changed

- The journey captures the dullahan in North Shroud from level 47; 7.57 made it capturable. Below 47 it still buys the gourd, as before.

## [0.3.2] - 2026-10-18

### Fixed

From a two-agent audit of 0.3.x:

- **Potions:**
  - They now also run when no familiar is out or no enemy is near; a character tanking alone at low HP was never healed.
  - A potion that fails no longer counts as spent. Economic Ethos is only counted as lost when an item really leaves the bag.
  - After AutoDuty drinks, Crucible SUX waits the same 5 s, so the two never drink for the same hit.
  - Holding 10 items now counts Habitual Hoarder (1,000) as part of a potion's cost from the Second Board on.
- **Selfless:**
  - It now counts as lost only when a benched beast is hurt (only the familiar out can drink) or a beast is KO'd.
  - The familiar out being scratched is no longer a reason to stop protecting it.
- **Aggro:**
  - A pause in sampling (the rotation plugin holding aggro, the familiar away) no longer counts as one huge hit.
  - A tankbuster is never handed to a character it would take below the floor.
  - Damage is measured for each of you separately when several enemies split their targets.
  - A Snarl or Challenge from another plugin counts as a handoff.
  - The target a potion borrowed is put back only if it still exists.
- **Unlock offer:**
  - The gear setup after "Strangers in the Wood" now waits for the quest's last window and event, and retries a refused equip.
  - The gear setup also no longer waits for the Master's Bestiary, and has a 3-minute limit.
  - A stopped run no longer leaves gear-setup steps for the next one.
  - The offer window closes on a character switch or when a run starts, and unlock-only refuses a character that cannot take the quest.
  - Unlock-only skips the BossMod question: the quest has no board.
  - The Journey tab has an "Unlock Beastmaster only" button for later.
- **Levelling (a second audit, and a recheck of the fastest route after 7.57):**
  - In a FATE, the character now walks to the FATE's enemies. It used to stop at the edge and wait for them.
  - A FATE whose progress has not moved for 3 minutes is left and not picked again (a boss it cannot solo). It used to stop the journey after 3 rounds of 15 minutes.
  - The picker no longer prefers a nearly finished FATE that its own estimate says ends before the character gets there.
  - It fights back while waiting to teleport to another zone.
  - An unreachable zone is skipped. The journey stops only when no zone of the level band can be reached.
  - A dry band no longer teleports from zone to zone forever: a zone change is no longer progress, but a FATE's progress is.
  - A level gained mid-FATE finishes the FATE before moving to the new band, and every band change is now noticed.
  - When KO'd while levelling, it accepts the return to the home point.
  - The zones per level band follow the wiki's FATE tables: Eastern Thanalan for 22-29; a new 41-44 band without Coerthas, which has no FATEs there; Southern Thanalan first for 45-50. FATEs are still the fastest solo route; the faster ones need other players (docs/research/09-leveling.md).
- **BossMod switch:** it waits a moment after the turned-off BossMod reports unloaded before checking whether the kept one is still reachable.

## [0.3.1] - 2026-10-18

### Fixed

- The Beastmaster unlock offer and the journey now check that Dawntrail is installed and registered on the account. "Strangers in the Wood" belongs to Dawntrail, so the game refuses it otherwise; Crucible SUX offered it anyway, and the journey would have walked to a quest it could not accept. The Journey tab now says what is missing.

## [0.3.0] - 2026-10-18

### Added

- **Share aggro with the beast (Settings, on by default):** in board battles, Snarl and Challenge hand the hits between you and your familiar.
  - **Safety first:** whoever would fall below its HP floor in the next 15 s hands off. The floors are Settings (you 35%, the familiar 15%). The expected loss starts at 25% per window and then follows the damage each of you actually takes; a tankbuster cast from the enemy intel triggers an early handoff.
  - **Then score:** when both are safe, the one whose lost HP costs fewer points tanks: Selfless (1,000) for the familiar, Self-preservation (250) for you, or the potions that undo it when cheaper. A campsite before the boss heals only you: AutoDuty rests no familiar. Ties go to the beast.
  - When neither of you is safe, the familiar takes the hits: your death ends the board, a KO'd beast costs about 250 points.
  - Each handoff and its reason goes into the combat log. While an enemy has the mechanic Rotation Solver Reborn's Beastmaster rotation Snarls for, aggro is left to it.
- **Heal with Beast Potions (Settings, on by default):** AutoDuty drinks potions only from your HP, which barely drops while a familiar covers you, and never heals a familiar. Crucible SUX now drinks them itself in board battles:
  - before a KO: for you below your floor, and for the familiar when the next 15 s would KO it and no handoff can save it
  - at the end of the final battle (boss below 15%): to bring the team and you to 100% when Selfless or Self-preservation is worth more than the Economic Ethos (750) a first potion costs; after any item has been used, further potions cost nothing
  - each potion and its reason goes into the combat log
- **The original BossMod works again:** the first time a session or the journey starts, Crucible SUX asks which BossMod you use, BossMod Reborn or the original. Settings › Sessions and the Plugins tab show the answer, and you can change it there.
  - The one you use covers mechanics. The other one is turned off for the run and back on afterwards, the same way the original BossMod was in 0.2.3; that question is part of the same window.
  - Right after AutoDuty starts a board, Crucible SUX sets BossMod's "prevent moving while casting" back to off. AutoDuty turns it on when it manages BossMod's AI, and the original BossMod obeys it even while your rotation plugin fights, so you could not dodge while casting. AutoDuty restores your own value when it stops.
  - The combat log header says which BossMod is in use.
- **Unlock Beastmaster for you:** when a character can take "Strangers in the Wood" (the main scenario through "The Ultimate Weapon" and another combat job at level 50) and has not finished it, Crucible SUX offers to pick the quest up and play it through, then switch to Beastmaster and set up its gear. It stops there; the rest of the journey stays as the Journey tab sets it.
  - It asks once per character per game session, and only while nothing runs and you are out of combat and duties. "Don't ask again for this character" turns the offer off for good.
- **Tested versions:** the Plugins tab warns when a companion plugin is older than the release Crucible SUX was last checked against (vnavmesh 1.2.3.14, Rotation Solver Reborn 7.5.7.3, BossMod Reborn 7.5.7.1, BossMod 7.5.7.0, AutoDuty 0.0.0.389, Lifestream 2.6.0.7, TextAdvance 3.3.0.1).

### Changed

- Checked against game 2026.10.06 (7.57). Its only Crucible data change: Sleepless Survival is worth 1,000 on every board (375 before), so a campsite rest now costs more than a first potion.
- Until you answer the BossMod question, Crucible SUX assumes BossMod Reborn, as before.
- `/sux report` copies the latest board's log, not the short "outside the boards" log a session records on the way out.

## [0.2.3] - 2026-10-11

### Added

- **The original BossMod is turned off for a run:** when a session or the journey starts while it is enabled, Crucible SUX asks to turn it off and turns it back on when the run ends.
  - It uses Dalamud's temporary switch (`/xldisableplugintemp`), so your plugin settings never change, and restarting the game turns it back on as well.
  - "Do this without asking next time" in the question, or the setting under Settings › Sessions, skips the question.
  - Both BossMods answer AutoDuty under the same names. When the original unloads, those names go silent, so BossMod Reborn is reloaded once to answer again.
  - Dalamud can switch a plugin only when it is in exactly one collection. If it is in more than one, the run does not start, and the chat explains why.
- The journey now checks for the original BossMod too: it blocks movement while you cast in FATEs as well.

## [0.2.2] - 2026-10-11

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
