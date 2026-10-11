# Beastmaster progression data (quests, beasts, gourds, EXP)

Read from game 2026.09.15.0000.0000 on 2026-10-11 with a scratch Lumina probe (sheets Quest, Level, ENpcResident,
ENpcBase, quest/<nnn>/JobXbm* text, XBMPet, Item, SpecialShop, Status, ParamGrow). Positions are world coordinates
(X, Y, Z); "map" is the in-game map coordinate. Objectives are paraphrased from the quest journal.

## Quests

All quests run in the Central Shroud (territory 148) unless noted. "Seq" is the quest sequence the game reports.

| Quest | Lv | Giver → turn-in | Steps (seq: objective @ place) |
|---|---|---|---|
| 71026 Strangers in the Wood | 50 (any job), after 70058 | excited adventurer `1059317`, New Gridania (132) map (11.8, 13.7) → J'yhuh Tia `1059767` | 1: reach the "destination" EObj `2015394` in Sorrel Haven (-319.5, 60.9, -128.1). 2: talk to the mysterious Miqo'te `1059318` (-302.8, 63.5, -257.2). 3: give the Cu Sith `1059320` raw meat (quest item `2003830`, event action 68) at (-305.1, 63.5, -258.1). 4: `/pet` (emote 105) the Cu Sith. 255: talk to J'yhuh Tia; any dialogue answer. Rewards: Soul of the Beastmaster `47474`, Hand Axe `47470`, Hoplon `47472`, coffer `52649`, **Cu Sith Gourd `49805`** (must be used) |
| 71027 The Wilds Call | 1, job 43 | J'yhuh Tia `1059767` (-302.8, 63.5, -257.2) → J'yhuh Tia `1059323` at the Bannock (119.9, -7.0, -87.5) | Before 1: use the Cu Sith Gourd. 1: talk to the wary Wailer `1059322` (121.8, -7.0, -86.4). 2: talk to J'yhuh Tia. 3: assign the Cu Sith to a battlehorn and summon it, then talk to J'yhuh Tia. 4: own 3 beasts (capture 2 more), then talk to J'yhuh Tia |
| 71028 Hearts Aligned | 8 | J'yhuh Tia `1059323` → same | 1: complete instinctual combos on a striking dummy (Bannock area, (107.6, -9.4, -75.6), radius 204). 2: talk to J'yhuh Tia. 3: reach level 14, then talk to J'yhuh Tia `1059325` (72.3, -6.5, -117.3). 4: J'yhuh Tia `1059327` near Tam-Tara Deepcroft (-38.9, -32.3, 326.1): capture a **geshunpest**. 5: summon the geshunpest and talk to him. 6: reach level 16 and talk to J'yhuh Tia at the Bannock. 7: complete 2 intentional combos (Sunstrider or Moonstalker) on the dummy. 255: talk to J'yhuh Tia |
| 71029 Master of the Lhu | 18 | J'yhuh Tia `1059323` → J'yhuh Tia `1059325` | 1: on the dummy, use the opo-opo's and the flying trap's Tempered Release (needs One with Nature, status 4601, which summoning grants). 2: talk. 3: reach level 22 and talk. 4: use Borrow with a wavekin then Quelling Wave, and with a soulkin then Soul Crush, on the dummy. 5: talk. 6: event range (-163.5, 1.0, -19.7): defeat the captive serpent. 7: talk to Lauda `1059329` (-161.1, 0.9, -29.1). 8: render aid to Lauda `1059333` (event action 72). 9: summon the Cu Sith and talk to J'yhuh Tia `1059325`. Next quest at level 30 |
| 71030 Into the Crucible | 30 | J'yhuh Tia `1059325` → Lauda `1059339` (25.4, -6.0, 67.4) | 1: own 15 beasts, then talk. 2: ask Elmar `1001280` (89.3, -6.4, -122.0), Mariustel `1000411` (135.2, -7.0, -67.0) and Alixie `1000409` (96.7, -1.5, -73.8). 3: talk to J'yhuh Tia `1059336` (84.3, -2.1, -50.6); answer "A woman clad in lavender was escorted to Bentbranch Meadows." (inferred from the story: Lauda waits in Bentbranch). 255: talk to Lauda. Unlocks the First Board and the Kornago merchant |
| 71031 Wit as Weaponry | 30; needs the First Board cleared (InstanceContent 56001) | J'yhuh Tia `1059340` (23.8, -6.0, 64.8) → J'yhuh Tia `1059325` | 1: talk to J'yhuh Tia. 2: talk to Loosefrox `1059343` (178.8, -8.9, -25.8) and choose "Speak with Loosefrox" to have her follow. 3–5: escort her to (277.0, -8.8, 12.7), (324.3, -7.1, -45.4) and (418.3, 4.2, -79.2), talking at each. 6: event range (428.1, 7.9, -87.0): defeat Spicekick Mustard. 7: talk to Loosefrox `1059344`. 255: talk to J'yhuh Tia |
| 71032 Gobsmacked | 40 | J'yhuh Tia `1059325` → Lauda `1059316` (25.5, -6.0, 67.5) | 1: summon the Cu Sith and talk. 2: own 20 beasts and talk. 255: talk to Lauda. Unlocks the Second Board and new merchant wares |
| 71033 Sin of Blood | 40; needs the Second Board cleared (56002) | Lauda `1059316` → J'yhuh Tia `1059325` | 1: wait at the "destination" EObj `2015400` (-26.1, 1.7, -163.3). 255: talk to J'yhuh Tia |
| 71034 A Beastmaster's Path | 50 | J'yhuh Tia `1059325` → Lauda `1059759` (25.5, -6.0, 67.5) | 1: complete the infinitive combo Universality (chain Sunstrider and Moonstalker) on the dummy. 2: talk to J'yhuh Tia `1059323`. 3: talk to J'yhuh Tia `1059325`. 255: talk to Lauda. Unlocks the Third Board and the Bright shop |
| 71035 Bonds Unbroken | 50; needs the Third Board cleared (56003) | Lauda `1059759` → same | 1: talk to J'yhuh Tia `1059411` (-24.7, -35.5, 426.8). 255: talk to Lauda |
| 71036 Free for All | 50; 25 beasts | Sylmond `1059407` (19.5, -6.0, 59.9) → same | Accept and complete at once. Unlocks the First Master's Board |
| 71037 Mastery Rematch | 50; First Master's Board cleared (56004); 30 beasts | Sylmond → same | Accept and complete at once. Unlocks the Second Master's Board |
| 71045 Blazing Trails | 50; Second Master's Board cleared (56005) | Sylmond → same | Accept and complete at once. Unlocks degrees and rankings |

Beast-count objectives count **unique beasts owned** (Cu Sith included), so gourds count as well.

## Beasts (XBMPet 1–50)

- **1 Cu Sith:** quest 71026 gives its gourd.
- **Open-world capture (`key=1`), 37 beasts.** Zone per beast:
  - Central Shroud: 2 squirrel, 8 diremite, 13 geshunpest, 20 flying trap, 34 raptor, 36 treant, 39 morbol, 41 salamander
  - North Shroud: 5 opo-opo
  - Middle La Noscea: 3 lamb, 4 pugil, 9 megalocrab, 12 mandragora, 14 puk, 26 buffalo, 40 ghost
  - Lower La Noscea: 6 dodo, 19 bat, 30 goobbue, 31 gigantoad
  - Eastern La Noscea: 24 apkallu, 32 colibri
  - Western La Noscea: 16 mantis, 21 ziz
  - Outer La Noscea: 33 coeurl
  - Western Thanalan: 7 coblyn, 11 vulture, 15 crab, 22 sabotender, 27 uragnite
  - Central Thanalan: 10 wespe, 25 adamantoise, 29 spriggan
  - Southern Thanalan: 23 golem, 28 worm, 35 drake
  - Mor Dhona: 42 cobra
- **Gourd only (`key=2`, column 7 = InstanceContent of the duty the beast lives in), 12 beasts:** 17 slime (Copperbell Mines), 18 dullahan (Halatali), 37 antling and 38 chimera (Cutter's Cry), 43 hydra (the Heroes' Gauntlet / A Relic Reborn: the Hydra), 44 damselfly and 45 rotting goobbue (Amdapor), 46 zu (Pharos Sirius), 47 ice golem (Snowcloak), 48 Karlabos (Sastasha Hard), 49 rafflesia (T6 / Tender Valley), 50 behemoth (Labyrinth of the Ancients). This decodes the old "special source" question.
- **Capture statuses:**
  - 4624 Capturing Interest
  - 4626 **Interest Captured**: "alliance will be determined upon incapacitation"
  - 4601 One with Nature
- **Rules from the quest text:** you cannot befriend a beast above your level. Wild names can differ from the Bestiary entry: "sabotender" includes cactuars, and subspecies count as their species. The Gauge action (44882) shows the chance. BNpcName rows 14408–14456 are the familiars' own names. The sheets hold **no wild-mob-to-beast link**; it lives on the server.

## Kornago merchant and gourds

- **Kornago merchant:** `ENpc 1059408` in Bentbranch Meadows, next to Lauda `1059759` and Sylmond `1059407`. Its ENpcData lists the SpecialShops `1771019` (Faded), `1771020` (Bright) and `1771036`, plus `1704323`.
- **SpecialShop 1771019 (Faded Remnant `51734`), needs 71030:**
  - Gourds:
    - Crab `49806` (10)
    - Slime `49807` (20)
    - Dullahan `49808` (20)
    - Worm `49809` (15)
    - Drake `49810` (10)
    - Antling `49811` (20, needs 71032)
    - Chimera `49812` (30, needs 71032)
  - Also sells Bright Remnant ×1 for 2 Faded (needs 71034).
- **SpecialShop 1771020 (Bright Remnant `51735`), needs 71034:**
  - Gourds:
    - Hydra `49813` (35)
    - Damselfly `49814` (35)
    - Rotting Goobbue `49815` (35)
    - Zu `49816` (35)
    - Ice Golem `49817` (35)
    - Karlabos `49818` (35)
    - Rafflesia `49819` (40)
    - Behemoth `49820` (40)
  - Also: 2 Faded for 1 Bright, the +1…+4 gear upgrades, and Guttler.
- **Cost of every gourd beast:**
  - Without the crab, worm and drake (which can be captured instead): 90 Faded plus 290 Bright.
  - Every Bright can also be bought at 2 Faded each.
- **Using a gourd:** gourds are unique items with ItemAction 2915–2930 and are used from the inventory.

## Actions the quests need

**Player actions (job 43):**

| Id | Action | Level | Notes |
|---|---|---|---|
| 44879 | Smash Axe | 1 | |
| 44883 | Axeblade Bite | 2 | Combo from 44879 |
| 44885 | Shieldsplitter | 12 | Combo from 44883 |
| 44880 | Capture | 1 | 10 y range, 2.5 s recast |
| 44882 | Gauge | 1 | 30 y range, shows the capture chance |
| 44881 | First Battlehorn | 1 | 1 s cast |
| 44892 | Second Battlehorn | 10 | 1 s cast |
| 44894 | Third Battlehorn | 20 | 1 s cast |
| 47093 | Trick | 8 | |
| 44890 | Tempered Release | 18 | Self; needs One with Nature |
| 44895 | Borrow | 22 | Variants 47238–47245, one per kin |
| 44886 | Beast Mode | 22 | |

**Kin actions after Borrow:**
- 44900 Quelling Wave (wavekin)
- 44902 Soul Crush (soulkin)

**Combos:** these are **not player actions** (`IsPlayerAction=false`). They happen when skills are chained while the beast has TP. The dummy steps are therefore "fight the dummy until the quest sequence advances".

| Id | Combo |
|---|---|
| 44910–44913 | Instinctual Combo: Volant, Rampant, Durant, Eldritch |
| 44914 | Intentional Combo: Sunstrider |
| 44915 | Intentional Combo: Moonstalker |
| 44916 | Infinitive Combo: Universality |

**Quest item and emote:** EventItem 2003830 Raw Meat (quest 71026); Emote 105 Pet.

## EXP to reach level 50 (ParamGrow)

| Level | EXP to next |
|---|---|
| 1 | 300 |
| 10 | 5,900 |
| 20 | 24,300 |
| 30 | 68,400 |
| 40 | 155,000 |
| 49 | 259,000 |

- 1→50 needs 3,589,670 EXP in total. Of that, 1→30 is 573,870.
- 30→50 is 3,015,800, which is 84% of the total, so the fastest-route question is mostly about levels 30–50.
