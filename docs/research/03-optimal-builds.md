# Crucible of the Unbroken: optimal builds and readiness

Research date: 2026-10-11. All sources describe patch 7.56. Ranking Season 1 was postponed with no new start date, so there are no leaderboard thresholds.

**Short answer:** the community has not published per-boss "optimal builds" yet. None of the reachable sources name a beast trio per boss, a swap order, or take/skip priorities for ephemeral items. What is solid is the official requirement table on the wiki. The readiness checklist below is built from it.

## Board requirements

| Board | Job lvl | Min gear IL (synced) | Rec. beast rank (synced) | Roster cap | Legendary min |
|---|---|---|---|---|---|
| First | 30 | 25 | 1 (5) | 10 | 17,500 |
| Second | 40 | 35 | 6 (10) | 12 | 17,500 |
| Third | 50 | 82 (100) | 11 (15) | 14 | 17,875 |
| First Master's | 50 | 82 (120) | 16 (20) | 12 | 18,750 |
| Second Master's | 50 | 82 (135) | 21 (25) | 15 | 18,500 |

- Beast rank is a recommendation, not a lock. Rank caps at 25, and every 100 EXP gives one rank.
- Level 50 gear runs from item level 90 to 130. It is upgraded at the Kornago Merchant with Bright Remnants. The +4 set (item level 130) costs 116 Bright Remnants in total. The wiki's best-in-slot set gives +67 Strength/Vitality in the Crucible.

## Builds and rotation

- **Beast trios.** The only named advice is two starter trios from Boostroom, with no player or run cited: Mantis/Diremite/Wespe (damage) and Vulture/Bat/Colibri (utility).
- **Utility beasts.** Sources name Chimera (freeze), Ziz (petrify), Coeurl, Buffalo, Treant, Ice Golem and Geshunpest.
- **Player rotation, outline only.** Smash Axe, Axeblade Bite and Shieldsplitter build TP. Instinctual skills from you and the familiar within 7 seconds make an Instinctual Combo, and you follow the affinity order on the job gauge. A priority list specific to the Crucible: UNKNOWN.
- **Unknown.** The 3 beasts per boss, the swap order, the roster per board, and numeric capture rates.

## Evidence

- **Board stats and unlock quests:** [Powerup Gaming](https://powerupgaming.co.uk/2026/09/13/ff14-crucible-of-the-unbroken-guide-beastmaster/), plus the wiki pages for the [First](https://ffxiv.consolegameswiki.com/wiki/First_Board_of_the_Unbroken), [Second](https://ffxiv.consolegameswiki.com/wiki/Second_Board_of_the_Unbroken), [Third](https://ffxiv.consolegameswiki.com/wiki/Third_Board_of_the_Unbroken), [First Master's](https://ffxiv.consolegameswiki.com/wiki/First_Master%27s_Board) and [Second Master's](https://ffxiv.consolegameswiki.com/wiki/Second_Master%27s_Board) boards.
- **Board layouts, enemy weaknesses and rank rewards:** on the same board pages. This is the main input for the beast recommender.
- **Degrees, leaderboards and scoring:** [Degrees page](https://ffxiv.consolegameswiki.com/wiki/Crucible_Degrees_and_Leaderboards).
  - Degrees unlock after clearing the Second Master's Board and the quest Blazing Trails.
  - Seasonal leaderboards need Third Degree on the player's home world.
  - Third Degree gives +9,000 bonus points.
- **Items and feed:** [Crucible Items](https://ffxiv.consolegameswiki.com/wiki/Crucible_Items), [Beast Gear](https://ffxiv.consolegameswiki.com/wiki/Beast_Gear), [Beast Feed](https://ffxiv.consolegameswiki.com/wiki/Beast_Feed), [Ephemeral items](https://ffxiv.consolegameswiki.com/wiki/Ephemeral_items).
- **Beastmaster gear and upgrades:** [Beastmaster Gear](https://ffxiv.consolegameswiki.com/wiki/Beastmaster_Gear).
- **Captures:** [Master's Bestiary](https://ffxiv.consolegameswiki.com/wiki/Master%27s_Bestiary) has the zone, coordinates and minimum level for 50 beasts.
  - Many level 50 and dungeon beasts are boss captures (Chimera, Antling, Hydra, Zu, Ice Golem, Behemoth). A Kornago Gourd bypasses the capture.
  - The capture rate is only described as "non-guaranteed" ([Chimera](https://ffxiv.consolegameswiki.com/wiki/Chimera_(Beast))).
  - Weather and time-of-day conditions: not found.
- **Beast stats and rank growth:** the [Chimera page](https://ffxiv.consolegameswiki.com/wiki/Chimera_(Beast)) is the worked example.
- **Starter trios and beast-class Borrow skills:** [Boostroom](https://boostroom.com/blog/ffxiv-beastmaster-guide-756-unlock-leveling-beasts-best-setup) (unattributed).
- **Strategy tips** from [Powerup Gaming](https://powerupgaming.co.uk/2026/09/13/ff14-crucible-of-the-unbroken-guide-beastmaster/) and [Eorzean Tavern](https://eorzeantavern.com/crucible-of-the-unbroken/):
  - Use Snarl to make sturdy beasts tank.
  - Set Battlehorns before every battle; they are not saved.
  - Put EXP on a small core team, since smaller rosters give each beast more EXP.
  - Feed effects are lost at a campsite unless Lily Simular was fed.
- **Second Master's approach.** A tank-heavy clear is known only from a search-result summary of a [YouTube third-degree clear](https://www.youtube.com/watch?v=H4tgVMI5kn0); the video could not be opened.
  - Stack HP, let the beast kill Lauda, and bring Paralysis resistance for Lauda.
  - A forum post found via search (no URL) reports a legendary run that used no beasts against Lauda.

## Readiness checklist

Each check is tagged with where its data would come from. This is inferred and not yet verified against client internals; see 05-client-internals.md.

| Check | Where the data comes from |
|---|---|
| Beastmaster job level ≥ 30 / 40 / 50 for boards 1–5 | game memory |
| Unlock quest done for the target board | game memory |
| All three required boards cleared; Blazing Trails and Second Master's Board cleared for Degrees | game memory, via achievements Crucible Checkmate I–III and Crucible Reforged I–II |
| Average equipped item level ≥ the board's minimum; synced values as soft targets | game memory |
| Count and tier of level 50 Beastmaster gear pieces, by item ID | game memory |
| Achievement-weapon unlocks: Friend to the Fierce V, Crucible Master, Unbroken Endgame | game memory |
| Roster size ≥ the board's cap (needs the owned-beast list) | UNKNOWN |
| Owns key utility beasts (Chimera, Ziz, …) | UNKNOWN |
| Each beast's rank vs the board's recommended rank (rank is not derivable from EXP alone) | UNKNOWN |
| Kornago Gourds and Bright/Faded Remnants of Resilience in inventory | game memory |
| Food buff: potions are disabled in the Crucible, so food is the only usable boost | player enters it |
| Goal: board, degree, Legendary vs clear, which of the 33 bonus conditions to chase | player enters it |
| Carry limit of 10 items and 10 beast gear pieces; Territory Tokens earned during a run | UNKNOWN mid-run |

## Confidence

- **Medium:** the board requirements. The wiki and Powerup Gaming agree.
- **Low:** everything else, including the unattributed Boostroom trios and the one-line YouTube summary.

## Caveats

- **Unreachable sources.** Icy Veins returned 403; a search snippet suggests it is a stub. YouTube descriptions, Reddit and Discord guides were not accessible.
- **Disagreements between sources:**
  - Third Board gear level: 100 on Icy Veins and Powerup Gaming, 82 on Eorzean Tavern.
  - Second Master's legendary minimum: 18,500 on its board page, 18,766 on Powerup Gaming and the Degrees page.
  - Third Board territory-token drop: 750, against 1,200 on the Second Board. This was not checked against the other boards.
- **Partly read pages.** The last ~7 KB of the Second Master's Board page and of the Bestiary were not read.
- **Item hints (researcher's own reading, not community consensus):**
  - A Ring of Sacrifice and a G3 Beast Potion are given free at the start of each board.
  - The Third Board's first two fights drop Temporal Sand, a Tome and Soul Serums.
