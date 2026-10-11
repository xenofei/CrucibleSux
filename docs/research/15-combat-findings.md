# Combat, scoring and the tester's first report

Research date: 2026-10-11, game 2026.09.15.0000.0000. This file answers the first play-test report (Maple, First Board,
original BossMod, Rotation Solver Reborn) and ties together 11–14:

- [11-scoring.md](11-scoring.md): the score rules from the web.
- [12-fights-boards-1-3.md](12-fights-boards-1-3.md) and [13-fights-masters-boards.md](13-fights-masters-boards.md): a
  playbook for every fight, from the wiki.
- [14-automation-coverage.md](14-automation-coverage.md): what AutoDuty and the two BossMod forks do inside a board.

Where this file and 11–13 disagree, the game data below wins.

## 1. The tester's report

| # | Report | Cause | Fix |
|---|---|---|---|
| 1 | "Took 10 beasts instead of 3; more than 3 costs points" | AutoDuty's default team mode is Recommended with a cap of 15, so it registers the board's full roster (10 on the First Board). Beast Squad III, a team of 1–3, is worth 4,250. | Push `Meta.Crucible.RecommendedTeamCap=3`. |
| 2 | "Needs a check for healing the character or up to 2 beasts" | AutoDuty heals only the player. Its campsite check compares a beast's HP with itself (always 100%), so it never rests for beasts. It never heals beasts in battle. | Crucible SUX has to watch beast HP itself. See §3 for how healing trades against the score. |
| 3 | "Choices and tactics need optimising" (the message was cut off) | AutoDuty follows one fixed path per board. Shop, items, treasure and rest default to on, with no score model. | The score model in §2; the route and shop decisions are PLAN F4, Phase 3. |
| 4 | "Hit by an AoE in the Ogre piece fight" | Very likely the wisps. Burning Ward sends wisps after you, and a wisp that reaches you sets off Scorched Earth, an untelegraphed hit across the whole arena (inferred from the 0 s, range-60 cast). Allfire is also unavoidable. The original BossMod ranks the wisps equal to the ogre (priority 0), so the AI does not kill them first. BossMod Reborn ranks them above the ogre. | Use BossMod Reborn, or have Crucible SUX set the target (§4). |
| 5 | "Buying items affects the points" | Partly right. A purchase is not −500 each: spending any tokens loses Coin Clutcher (500) once. Using a crucible item loses Economic Ethos (750), and acquiring beast gear loses Nameday Attire (750). | Push `Meta.Crucible.Shop=false`, and decide about `Items` and `Treasure` per goal. |
| 6 | "Did not kill the mage first in Pas de Seul" | The original BossMod gives the succubus mage and knight the boss's priority (0), so the AI picks the nearest. BossMod Reborn ranks mage 2 above knight 1. The mage's Fanaticism (6 s cast) gives the boss Damage Up. | Same as 4. |

The movement report from before ("runs all over the place into damage") has one more cause, from 14: AutoDuty walks
the board and closes on far enemies with vnavmesh, which ignores AoEs.

## 2. Score bonuses (game data)

`XBMScoreBonus` names the 33 bonuses. `XBMContent` columns 0–32 give each one's points per board. A bonus worth 0 on a
board does not apply there.

| Bonus | Condition | 1st | 2nd | 3rd | 1st M | 2nd M |
|---|---|---|---|---|---|---|
| Willing Sacrifice I / II / III | Clear with 3 / 2 / 1 beasts knocked out | 250 / 500 / 750 | same | same | same | same |
| No Beast Left Behind | No beast knocked out | 1,000 | 1,000 | 1,000 | 1,000 | 1,000 |
| Beast Squad I / II / III | Team of 6–7 / 4–5 / 1–3 | 1,250 / 2,500 / 4,250 | same | same | same | same |
| Self-preservation | Beastmaster at 100% HP at the clear | 250 | 250 | 250 | 250 | 250 |
| Selfless | Every beast at 100% HP at the clear | 1,000 | 1,000 | 1,000 | 1,000 | 1,000 |
| Infinity and Beyond I / II / III | Universality 5 / 10 / 15 times | – | – | – | – | 150 / 300 / 450 |
| Unbeastable | Never Damage Down or Vulnerability Up | 1,250 | 1,250 | 1,250 | 1,250 | 1,250 |
| Nameday Attire | No beast gear acquired | 750 | 750 | 750 | 750 | 750 |
| Walking Armory | Hold 10 beast gear | – | 500 | 500 | 500 | 500 |
| Economic Ethos | No crucible items used | 750 | 750 | 750 | 750 | 750 |
| Habitual Hoarder | Hold 10 crucible items | – | 1,000 | 1,000 | 1,000 | 1,000 |
| Starve the Fever | No beast fed | 750 | 750 | 750 | 750 | 750 |
| Feed the Bold | Fed 20+ times | – | – | 1,500 | – | 1,500 |
| Coin Clutcher | No tokens spent | 500 | 500 | 500 | 500 | 500 |
| Rolling in Riches | Hold 2,000+ tokens | – | 500 | 500 | 500 | 500 |
| Sleepless Survival | No campsite rest | 375 | 375 | 375 | 375 | 375 |
| (Kin) Gambit ×8 | Team of one kin only | 750 | 750 | 750 | 750 | 750 |
| First / Second / Third Degree | Clear under that degree | 4,000 / 6,250 / 9,000 | same | same | same | same |

What follows for the plugin:

- **Team of 3, all one kin, never knocked out** is worth 4,250 + 750 + 1,000, the largest controllable block.
- **Score-safe choices:** no shop, no items, no feeding, no gear and no rest add up to 3,125 on the First Board.
  Healing pulls the other way: Selfless (1,000) and Self-preservation (250) need full HP at the clear. A potion costs
  Economic Ethos (750) and a rest costs Sleepless Survival (375). So the cheapest top-up is a campsite rest just before
  the boss, if the board has one, and only when beasts are hurt.
- **Unbeastable (1,250)** is lost to Damage Down or Vulnerability Up. In FFXIV these usually come from standing in an
  AoE, so dodging is worth points as well as HP.
- **Open question:** the sources cap space points at 5,000. With that cap, the bonuses a Standard-degree First Board can
  stack (about 11,600) plus 5,000 fall short of Legendary's 17,500. Either Legendary needs a degree, or base scoring is
  larger than the sources say. Check the in-game "View Bonuses" screen and the result screen after a clear.

## 3. What the client knows about each fight

**In the client:**

- `XBMBattleDetail` lists each battle's enemies. `XBMBattleDetailAction` is the board's enemy intel: two abilities per
  enemy, each with its Action row, a target from `XBMActionTarget` (Self, Ground, Highest Enmity, Random, Player,
  Allies), a shape from `XBMActionEffectType` (Single Target, Front, Rear, Front/Rear, Lateral, Circle, Ring,
  Circle/Ring, Universal, Cross) and the Status it inflicts.
- The `Action` row of every ability gives its cast time, `CastType` (2 circle, 10 donut, 11 cross, 12 rect, 13 cone),
  `EffectRange`, `XAxisModifier` (rect width) and its Omen. The Omen path holds the cone angle (`gl_fan120…` is 120°)
  and the donut's inner radius (`gl_sircle_4008…` is 8–40).
- `tools/CrucibleSux.DataDump` prints all of it (section "XBMBattleDetail abilities").

**Not in the client:** the order and timing of an enemy's casts. The server runs enemy AI. A timeline can only be
recorded from real runs: BossMod's replay recorder, or a cast log written by Crucible SUX.

**Dodging does not need a timeline.** Every AoE here is a cast with a 3–12 s telegraph. A module that reacts to "enemy
X started casting action Y at position P" covers it; Dalamud exposes the cast on every battle character. That is how
both BossMod forks work. Their Crucible modules are reactive ("trivial phase"), with no timeline.

## 4. BossMod coverage and the shape check

Both forks have a module for every Crucible fight: 40 files in awgil's BossMod (`BossMod.Modules/Global/Crucible/`)
and 42 in BossMod Reborn (`BossMod/Modules/Global/CrucibleOfTheUnbroken/`). Both are BSD-3-Clause.

**Method.** Each AoE shape in each module was compared with the client's Action data for the same action ID (script in
the session scratchpad, not kept). Result: 256 shape definitions; 224 match. Every Ogre piece and Pas de Seul shape
matches.

Most of the 32 differences are deliberate. They are attacks whose damage falls off with distance, where the client's
`EffectRange` is the full reach and the module draws the deadly core: Void Flare Star (client 100, module 28–30),
Beastly Flare (80 vs 20–30), Typhoon (40 vs 10, a knockback), Tremblor, Swing Round, Obliterate, Shockwave, Water III,
Thermobaric Charge and Aero III.

**Ones to raise upstream or check in game:**

- **BossMod Reborn, Borgny's Cauterize:** a rect 40 long, while the client says 48. The far 8 y may not be drawn as
  unsafe.
- **BossMod Reborn, Gigantis Smashing Stamp:** a rect 7 × 4, while the client says 40 × 8. It looks like the wrong
  action is mapped.
- **Original BossMod, Medusa Petrifying Regard:** a 5.8 circle, while the client says a 60 y, 45° cone. It may be a
  gaze handled elsewhere in the module.
- **Original BossMod, Lakhamu Earth Shaker:** a 3.3 circle, while the client says a 60 y cone. It may be baited.
- **Larger than the client, so safe:** Gigantis Glower (module 6 wide, client 3), Siren Dead Man's Dirge inner radius
  (2 vs 3) and Progenitrix Fire II (6 vs 5).

**Add priorities.** BossMod Reborn ranks the First Board's adds that matter: Pas de Seul mage 2 over knight 1, and the
Ogre wisps 1. The original BossMod ranks those adds equal to the boss (it does rank Borgny's toxic mass 1), but it ships
a Beastmaster Crucible AI (Snarl, Challenge, dispel, interrupt) that BossMod Reborn lacks.

**Cast times differ between the forks.** For example, BossMod Reborn says Fanaticism is 9 s while the client and the
original BossMod say 6 s. Higher degrees may change cast times; this is unverified.

## 5. What to build (proposal, not started)

1. **Run settings (small).** Push `Meta.Crucible.RecommendedTeamCap=3` and `Meta.Crucible.Shop=false`. Add Settings
   for a score goal: "Clear" keeps AutoDuty's defaults; "Score" also turns off items, treasure and rest except before
   the boss. Prefer BossMod Reborn for the Crucible in the Plugins tab.
2. **Target priority (medium).** From the game data: an enemy whose intel ability targets Allies (it buffs another
   enemy) or Self with a Damage Up status dies first. So do adds that home in (wisps). Crucible SUX sets the target
   during board battles, which needs a Dalamud `ITargetManager` write while AutoDuty fights.
3. **Cast audit (medium).** Log every enemy cast in the board: action, shape and size from the client data, and whether
   the player stood inside it when it resolved. This is the "is it dodging?" check. The play-test report can show
   "hit by X at Y", and the logs give the timelines that §3 says are missing.
4. **Beast healing (larger).** Read beast HP from the board's party addon and heal or rest by the trade-offs in §2.
   Needs the addon traced live.
