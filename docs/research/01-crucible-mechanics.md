# Crucible of the Unbroken: mechanics reference

Research date: 2026-10-11. Content shipped in patch 7.56 (2026-09-08), and every board page describes 7.56. Confidence is medium: the wiki is detailed for the First and Second Boards and for the item lists, but thin for the Third Board and both Master's Boards.

## Unlock and entry

- **Unlock:** Beastmaster level 30, then the quest *Into the Crucible* (J'yhuh Tia; requires *Master of the Lhu*).
- **Entry NPC:** Lauda, in Central Shroud at (21.9, 22.8). The official guide also lists (22.9, 19.1). Lauda handles starting a board, Crucible Mode and Resume Progress.
- **Nearby NPCs:** Sylmond and the Kornago Merchant stand at about (21.8–21.9, 22.6).
- **Entry limits:** none found, daily or weekly. The Weekly Challenge Log is the only weekly element.

## Boards

| Board | Unlock quest | Other requirements | Moves | Boss | Roster cap |
|---|---|---|---|---|---|
| First | Into the Crucible | — | 9 | Pas de Seul + Succubus Mage and Knights | 10 |
| Second | Gobsmacked | — | 10 | Loosefrox Inkyjots + Chewchum Popoto | 12 |
| Third | A Beastmaster's Path | — | 12 | Guttler the Gutter + Thanatos | 14 |
| First Master's | Free for All | *Bonds Unbroken* done, 25 beasts owned | 9 | Borgny the Venomous + Toxic Mass | 12 |
| Second Master's | Mastery Rematch | First Master's cleared, 30 beasts owned | 14 | Lauda the Spellcleaver + Thanatos | 15 |

- *Blazing Trails* (Sylmond) unlocks degrees and rankings after the Second Master's Board.
- Every board has a 120-minute limit.

## Board layout

- Each board has a fixed layout with Left/Right or Left/Middle/Right branches. Taking one branch locks out the others, and there is no backtracking.
- Space types:
  - **Enemy:** can flee.
  - **Elite:** cannot flee.
  - **Boss**, **Campsite**, **Shop** and **Treasure**.
  - **Random:** any space type except Boss.

## Failing, suspending and resuming

- A run fails when the player reaches 0 HP. A Reraiser, the Ring of Sacrifice or Temporal Sand prevents that.
- A familiar at 0 HP is out until it is revived with a Blessed Horn.
- **Suspend Progress** works only on a cleared space and keeps one save slot. Starting a board deletes that save.
- **Forfeit** is allowed outside battle.

## Beasts

- 50 beasts in 8 classes.
- You pick a roster up to the board's cap, take 3 into each battle, and have 1 summoned at a time with a Battlehorn.
  - Battlehorn assignments are **not saved** between battles, so they must be set before every battle.
  - Battlehorns unlock at job levels 1, 10 and 20.
- Beasts have their own HP and rank. They do not regenerate HP.
- Rank caps at 25, at 100 EXP per rank. Each familiar earns at most 255 EXP per board.
- Duty actions are Challenge and Snarl.
- Weaknesses deal ×1.3 damage.
- STR/INT/RES base values and the HP formula are on the Attributes page, which is marked "early research".

## Ephemeral items

| Type | Catalogue | Carry limit |
|---|---|---|
| Crucible Items | 68 | 10, duplicates allowed |
| Beast Gear | 75 | 10, no duplicates |
| Feed | 60 | per-familiar satiety |

- **Treasure:** pick 1 of 10. The First Board shows "four of ten". Thief's Knife gives two picks.
- **Shops:** stock 8 feed, 4 items and 4 gear. A shop purchase costs −500 score points (from the forum gripes).
- **Free at board start:** a G3 Beast Potion and a Ring of Sacrifice.

## Score and ranks

- Spaces give 250 / 625 / 1000 points, up to 5000.
- Degree bonuses are 4000 / 6250 / 9000.
- Run ranks go from Apprentice to Legendary. The rank sets the remnant payout.
- Rankings use the last three attempts per board, home world only, aggregated per data center. Season 1 is delayed with no new date; it was planned for 2026-09-24 through 7.58.
- Degree HP scaling: +42% per degree on the First Board's first encounter, but +70% on the Second Master's final boss. Damage scaling is UNKNOWN on the wiki; Power Up Gaming says +42%.

## UI names

From the official job guide:
- Master's Bestiary, under Main Menu > Character.
- Recommended Team, Board Layout and View Bonuses.
- Crucible Mode, the "Participate in rankings" option, and Resume Progress.

## Unknowns

1. Boss mechanics for Guttler, Lauda the Spellcleaver, Borgny and Toxic Mass. Only Loosefrox is documented.
2. Loot, shop and treasure pools for the Third Board and both Master's Boards ("???" on the wiki).
3. The exact dialog and menu sequences for starting a board, setting Crucible Mode and resuming. This needs a live UI trace.
4. Whether any per-day or weekly entry limit exists.
5. Enemy damage scaling by degree, and the exact rank point thresholds per board.

## Sources

1. https://ffxiv.consolegameswiki.com/wiki/Crucible_of_the_Unbroken
2. https://ffxiv.consolegameswiki.com/wiki/Crucible_Degrees_and_Leaderboards
3. https://ffxiv.consolegameswiki.com/wiki/Into_the_Crucible , /wiki/Free_for_All , /wiki/Mastery_Rematch , /wiki/Blazing_Trails
4. https://na.finalfantasyxiv.com/jobguide/beastmaster/
5. https://ffxiv.consolegameswiki.com/wiki/Crucible_Items , /wiki/Beast_Gear , /wiki/Beast_Feed
6. First and Second Board pages, which are detailed: /wiki/First_Board_of_the_Unbroken , /wiki/Second_Board_of_the_Unbroken
7. Third and Master's Board pages, which are sparse: /wiki/Third_Board_of_the_Unbroken , /wiki/First_Master%27s_Board , /wiki/Second_Master%27s_Board
8. https://ffxiv.consolegameswiki.com/wiki/Master%27s_Bestiary , /wiki/Beastmaster
9. https://ffxiv.consolegameswiki.com/wiki/Crucible_of_the_Unbroken_Attributes (early research)
10. https://ffxiv.consolegameswiki.com/wiki/Kornago_Merchant (the scrape was misaligned)
