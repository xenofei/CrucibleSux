# Crucible sheet columns

What each column of the Crucible's sheets means, as read from game 2026.09.15.0000.0000 with
`tools/CrucibleSux.DataDump`. Lumina's stable `XBM*` types name these columns `UnknownN`. The named types in
`Lumina.Excel.Sheets.Experimental` raise the `PendingExcelSchema` error, so the code reads the stable ones and this
file is the map. On patch day, rerun the dump and compare:

```
dotnet run --project tools/CrucibleSux.DataDump -- --game "<FFXIV install>\game\sqpack" > docs/data/dumps/dump.txt
```

The `dumps/` folder is gitignored because a dump holds game text.

"Verified" means checked against a known example (the wiki, a beast's page). "Inferred" means it is consistent with the
data but has not been checked in game.

## XBMContent (one row per board, 1-5)

| Column | Meaning | Status |
|---|---|---|
| 0-32 | Score bonus points | Inferred |
| 33 | ContentFinderCondition row | Verified (names match) |
| 34 | Team size (beasts you may bring) | Verified (10/12/14/12/15, wiki) |
| 35 | Recommended rank | Inferred: 3/8/13/18/23, while the wiki says 1/6/11/16/21 |
| 36 | Sync rank | Verified (5/10/15/20/25) |

## XBMPet (one row per beast, 1-50; row 0 is empty)

Read raw by column index. The stable `XBMPet` type's column hash is stale, so Lumina refuses it.

| Column | Type | Meaning | Status |
|---|---|---|---|
| 0 | Int32 | Pet sheet row (the name) | Verified |
| 1 | UInt8 | Kin: 1 Beastkin, 2 Vilekin, 3 Cloudkin, 4 Seedkin, 5 Wavekin, 6 Scalekin, 7 Soulkin, 8 Ashkin | Verified by examples |
| 2 | UInt8 | XBMPetParamGrow row | Inferred |
| 3 | UInt8 | Satiety maximum | Inferred |
| 4 | UInt32 | Icon | Verified |
| 5 | UInt16 | Action (the beast's skill) | Inferred |
| 6 | UInt8 | Location key: 0 none (starter beasts), 1 = column 7 is a PlaceName, 2 = special source | Partly verified (see below) |
| 7 | UInt16 | Location value | Key 1 verified (zone names) |
| 8 | String | Description | Verified |
| 9 | String | Trick text | Verified |
| 10 | String | Tempered text | Verified |
| 11-21 | Bool | Can inflict status *i* (same order as BNpcResist columns 0-10) | Inferred |
| 22-26 | UInt8 | Per-attribute percentages: STR, INT, PR, MR, CON | Inferred |

With location key 2, column 7 names neither a PlaceName nor a TerritoryType that fits: 3, 7, 12, 17, 22, 27, 28, 75,
92 and 98 resolve to Map names such as Old Gridania, Limsa Lominsa Lower Decks and The Hourglass. These 13 beasts
(17, 18, 37, 38 and 43-50) seem to come from a special source. Until that source is decoded, the plugin shows their
description.

## XBMPetParamGrow (subrow = rank − 1)

Columns 0-4 are STR, INT, PR, MR and CON at that rank. Rows 1-4 have 30 subrows; the beasts' rows have 25.
`BeastInfo.StatsAt` takes `grow × percent / 100`, rounded down. That formula is **unverified**. The Bestiary
reader stores a rank only when exactly one rank's computed attributes match the page.

## XBMContentBattle (row = board, subrow = battle)

Column 0 is an XBMBattleDetail row. Subrow 0 is the boss battle. For example, board 1 is `[6,1,2,3,4,5]`.

## XBMBattleDetail (row = battle, subrow = enemy)

| Column | Meaning | Status |
|---|---|---|
| 0 | BNpcName row | Verified |
| 1 | Icon | Inferred |
| 2, 3 | Actions | Inferred |
| 4 | BNpcResist row (11 bools; true = resists that status) | Inferred |
| 5-9 | 1-5 ratings, read as STR, INT, PR, MR, CON | **Unverified** |
| 10 | XBMElement row (the enemy's attack type) | Inferred |

## Small sheets

- **XBMElement:** 1 Fire, 2 Wind, 3 Earth, 4 Lightning, 5 Ice, 6 Water, 7 Blunt, 8 Piercing, 9 Slashing.
- **XBMItemType:** 1 Beast Gear, 2 Crucible Item, 3 Feed.
- **XBMItem columns:** 2 name, 4 short description, 12 price, 13 type.
- **XBMScoreRank (0-8):** Legendary, Apex, Elite, Renowned, Exemplary, Adept, Journeyman, Novice, Apprentice.
- **XBMScoreBonus:** 33 bonuses, each with a name, a description and an always-active flag.

## Pinned IDs (CrucibleSux.Core/CrucibleIds.cs, Boards.cs)

| What | IDs |
|---|---|
| Beastmaster | ClassJob 43, unlocked by quest 71026 |
| Board unlock quests | 71030 Into the Crucible, 71032 Gobsmacked, 71034 A Beastmaster's Path, 71036 Free for All, 71037 Mastery Rematch |
| Degrees and rankings | Quest 71045 Blazing Trails |
| Board clears | InstanceContent 56001-56005; achievements 4033-4037 |
| Third degree | Achievements 4038-4042 |
| Apex and Legendary | Achievements 4043/4044, 4045/4046, 4047/4048, 4049/4050, 4051/4052 |
| Tiered achievements | Job level 4023-4027; befriended 4028-4032; rank 25 4056-4060 |
| Kin mates | 4061-4068 |
| Rankings | 4075-4077 |
| Remnants | Faded Remnant of Resilience 51734; Bright Remnant of Resilience 51735 |
