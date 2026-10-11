# 08: Beast collection (all 50)

Researched 2026-10-11 (patch 7.56), online only. Confidence is medium:
- Facts from the sheets (gourd ids, prices, gates, shops, status 4626) are high confidence. They agree with `docs/data/bst-progression.md`.
- The per-beast wild names and levels come from the wiki's Master's Bestiary as summarised by a fetch tool, so spot-check them.

## Answer

- **Gourds:** 16 gourd items (49805–49820). One is the Cu Sith (a quest reward) and 15 are sold.
- **Captures:** the other 37 beasts are caught in the open world. Crab, worm and drake can be caught or bought.
- **"Special source" beasts:** these are instanced-duty beasts. XBMPet column 7 holds a ContentFinderCondition / InstanceContent row. There are 12 of them (17, 18, 37, 38, 43–50), and every one has a gourd. Their open-world alternatives are FATEs, hunt marks and treasure maps, which are not guaranteed. So **the gourd is the unattended route** for them.
- **Kornago merchant:** `ENpcResident 1059408` at (21.9, 22.6), Bentbranch Meadows, selling SpecialShops 1771019 (Faded) and 1771020 (Bright). The twin NPC 1059342 appears only during *Into the Crucible*.
- **Cost of every bought gourd:** 125 Faded plus 290 Bright. Counted all in Faded that is 705, since one Bright costs 2 Faded.

## Wild beasts (wiki "Min Lvl"; coordinates are the wiki's)

The zone comes from the game sheet (XBMPet column 7). The wild name is the wiki's, with its BNpcName row in brackets.

| # | Beast | Zone | Wild name (BNpcName) | Lv | Map |
|---|---|---|---|---|---|
| 2 | Squirrel | Central Shroud | Ground Squirrel (37) | 1 | 24,16 |
| 3 | Lamb | Middle La Noscea | Lost Lamb (392) | 3 | 22,26 |
| 4 | Pugil | Middle La Noscea | Pugil (640) | 4 | 22,22 |
| 5 | Opo-opo | North Shroud | Opo-opo (5) | 5 | 27,24 |
| 6 | Dodo | Lower La Noscea | Wild Dodo (393) | 4 | 29,21 |
| 7 | Coblyn | Western Thanalan | Rusty Coblyn (276) | 6 | 21,26 |
| 8 | Diremite | Central Shroud | Diremite (10) | 10 | 20,19 |
| 9 | Megalocrab | Middle La Noscea | Megalocrab (561) | 10 | 16,14 |
| 10 | Wespe | Central Thanalan | Huge Hornet (632) | 1 | 20,27 |
| 11 | Vulture | Western Thanalan | Nesting Buzzard (299) | 6 | 21,25 |
| 12 | Mandragora | Middle La Noscea | Tiny Mandragora (405) | 5 | 23,18 |
| 13 | Geshunpest | Central Shroud | Geshunpest (7466) | 14 | 19,28 |
| 14 | Puk | Middle La Noscea | Puk Hatchling (401) | 4 | 23,19 |
| 15 | Crab | Western Thanalan | Thickshell (636) | 13 | 15,15 |
| 16 | Mantis | Western La Noscea | Killer Mantis (644) | 16 | 22,23 |
| 19 | Bat | Lower La Noscea | Cave Bat (364) | 7 | 27,16 |
| 20 | Flying trap | Central Shroud | Roselet (22) | 10 | 23,26 |
| 21 | Ziz | Western La Noscea | Rothlyt Pelican (1181) | 16 | 23,24 |
| 22 | Sabotender | Western Thanalan | Cactuar (287) | 3 | 28,24 |
| 23 | Golem | Southern Thanalan | Sandstone Golem (280) | 29 | 24,12 |
| 24 | Apkallu | Eastern La Noscea | Apkallu (341) | 30 | 27,35 |
| 25 | Adamantoise | Central Thanalan | Giant Tortoise (244) | 12 | 24,30 |
| 26 | Buffalo | Middle La Noscea | Wounded Aurochs (358) | 8 | 18,17 |
| 27 | Uragnite | Western Thanalan | Scaphite (635) | 14 | 17,14 |
| 28 | Worm | Southern Thanalan | Sandworm (290) | 31 | 16,34 |
| 29 | Spriggan | Central Thanalan | Spriggan Graverobber (317) | 7 | 17,23 |
| 30 | Goobbue | Lower La Noscea | Mossless Goobbue (354) | 12 | 25,26 |
| 31 | Gigantoad | Lower La Noscea | Rivertoad (349) | 4 | 25,23 |
| 32 | Colibri | Eastern La Noscea | Colibri (639) | 33 | 30,25 |
| 33 | Coeurl | Outer La Noscea per the sheet; the wiki says Upper La Noscea | Master Coeurl (9911) | 24 | 9,21 |
| 34 | Raptor | Central Shroud | Anole (120) | 9 | 31,21 |
| 35 | Drake | Southern Thanalan | Sundrake (264) | 32 | 25,38 |
| 36 | Treant | Central Shroud | Treant Sapling (128) | 12 | 27,15 |
| 39 | Morbol | Central Shroud | Stroper (238) | 31 | 13,22 |
| 40 | Ghost | Middle La Noscea | Bogy (404) | 7 | 20,19 |
| 41 | Salamander | Central Shroud | Black Eft (196) | 6 | 26,21 |
| 42 | Cobra | Mor Dhona | Lake Cobra (1851) | 45 | 25,13 |

## Capture mechanics [1][2][4][6]

- **Status and roll:** Capture (44880) applies status 4626 *Interest Captured*. When the target dies with the status active, the game rolls; **lower HP at that moment means better odds**. No numbers are published.
- **Level:** the target must be at or below your level. A bigger level gap helps. Gauge reports six tiers, from "No pact can be forged" to "should take no effort at all".
- **Parties:** each Beastmaster rolls separately for their own Capture.
- **Unknown:**
  - whether the killer must be you or your beast
  - retry rules
  - daily limits
  - EXP per capture

## Kornago gourds [7]–[12]

**The two shops:**

| Shop | Currency | Opens with | Notes |
|---|---|---|---|
| 1771019 | Faded | *Into the Crucible* | Antling and Chimera rows need *Gobsmacked* |
| 1771020 | Bright | *A Beastmaster's Path* | |

**Gourd prices:**

| Gourd | Price |
|---|---|
| Crab | 10 Faded |
| Slime | 20 Faded |
| Dullahan | 20 Faded |
| Worm | 15 Faded |
| Drake | 10 Faded |
| Antling | 20 Faded |
| Chimera | 30 Faded |
| Hydra, Damselfly, Rotting Goobbue, Zu, Ice Golem, Karlabos | 35 Bright each |
| Rafflesia | 40 Bright |
| Behemoth | 40 Bright |

**Remnant exchange:** 1 Bright costs 2 Faded, and 1 Bright trades back for 2 Faded.

**Gourd items:** unique, untradable, and usable only by a Beastmaster.

## Shop automation (the addon is unverified)

The Kornago shops are SpecialShops that charge items (`UseCurrencyType` 2). The most likely addon is **ShopExchangeItem** (inferred).

**ShopExchangeItem:**
- Buy: fire the callback `(true, 0, index, amount)` [13].
- Quantity dialog: `ShopExchangeItemDialog`. Exchange is button 18 and Cancel is button 19 [14].
- Final confirm: a `SelectYesno` may follow.
- Close: callback `-1`.

**ShopExchangeCurrency:** the same buy shape. Item ids are at `AtkValues[1066+i]` [15].

**Plan:** check at runtime which addon is open, and find the row by its item id rather than trusting the sheet order.

## Item and action use

- **Use an item:** `AgentInventoryContext.Instance()->UseItem(itemId)` [19][20].
- **Use an action on a target:** `ActionManager.Instance()->UseAction(ActionType.Action, id, targetObjectId)` [21].
- **Statuses:** `IBattleChara.StatusList` [22].

## Spawn positions

Teamcraft's `libs/data/src/lib/json/monsters.json` (branch `staging`, commit 46dde90, MIT) [23] holds positions per monster, keyed by BNpcName:
- **Fields:** `zoneid` is a PlaceName, `map` is a Map row, x and y are map coordinates, and z is usually 0.
- **Our use:** `CrucibleSux.Core/BeastSources.cs` takes the centre of the non-FATE positions for each wild name, and credits Teamcraft.
- **Gap:** Teamcraft has **no Central Shroud positions at all**, so the eight Central Shroud beasts use the wiki's map coordinates.
- **Conversion:** every capture zone's Map row has scale 100 and no offset, so world = (map − 1) × 2048 / 41 − 1024. The height comes from the navmesh at run time.
- **Coeurl:** Teamcraft puts master coeurl (9911) and coeurl pup (28) at Upper La Noscea (8.9, 21.2), matching the wiki, so the planner uses Upper La Noscea.
- **Alternatives:** Teamcraft also has ground squirrels and treant saplings in the North Shroud, and stropers in the South Shroud. These are kept as second spawn points.

## Unknowns

1. Which addon the Kornago shops open, its AtkValue layout, and whether a menu comes first.
2. Numeric capture odds and the HP threshold; retry rules; whether the killer matters; limits; EXP per capture.
3. Whether using a gourd asks for confirmation.
4. The coeurl's zone: the sheet says Outer La Noscea and the wiki says Upper La Noscea. Runtime matching covers both.

## Sources

1. https://ffxiv.consolegameswiki.com/wiki/Capture
2. https://v2.xivapi.com/api/sheet/Action/44880
3. https://ffxiv.consolegameswiki.com/wiki/Master%27s_Bestiary (and the `<Name>_(Beast)` pages)
4. https://ffxiv.consolegameswiki.com/wiki/Gauge
5. https://v2.xivapi.com/api/search?sheets=Status&query=Name="Interest Captured"
6. https://ffxiv.consolegameswiki.com/wiki/Beastmaster
7. https://www.garlandtools.org/db/doc/npc/en/2/1059408.json
8. https://na.finalfantasyxiv.com/jobguide/beastmaster/
9. https://ffxiv.consolegameswiki.com/wiki/Kornago_Merchant
10. https://v2.xivapi.com/api/sheet/SpecialShop?rows=1771019,1771020
11. https://www.garlandtools.org/db/doc/item/en/3/51735.json
12. https://v2.xivapi.com/api/sheet/Item/49806
13. https://github.com/LeontopodiumNivale14/Ices-Cosmic-Exploration `ICE/Utilities/ShopExchangeItem.cs`
14. https://github.com/NightmareXIV/ECommons `ECommons/UIHelpers/AddonMasterImplementations/ShopExchangeItemDialog.cs`
15. Same repo, `ShopExchangeCurrency.cs`
19. https://github.com/InitialDet/AutoHook `AutoHook/Utils/PlayerRes.cs`
20. https://github.com/NightmareXIV/Lifestream
21. https://github.com/PunishXIV/Splatoon
22. https://dalamud.dev/api/Dalamud.Game.ClientState.Objects.Types/Interfaces/IBattleChara
23. https://github.com/ffxiv-teamcraft/ffxiv-teamcraft
