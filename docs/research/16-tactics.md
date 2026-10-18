# Aggro sharing and the score advisor

Design date 2026-10-18, game 2026.09.15.0000.0000. The owner asked for two things:

- The beast and the character should share aggro. Each takes hits for a while, handing off by HP.
- The choices should keep the highest score the run can still reach. The owner's rule: "as long as they're healed by
  the end of the Crucible, it shouldn't affect score".

## 1. Facts from the game data

**Duty actions** (Action sheet, read with a scratch Lumina reader):

| Id | Name | Recast | Cooldown group | Range | Target |
|---|---|---|---|---|---|
| 46751 | Snarl | 15 s | 81 | 25 y | hostile |
| 46750 | Challenge | 15 s | 81 | 25 y | hostile |

- **One shared cooldown.** Snarl and Challenge share group 81 (15 s, confirmed). Every 15 s there is one decision: the
  beast tanks or the character tanks.
- **What each does.** Snarl makes the familiar use Threaten (46752): the familiar takes top enmity for 15 s and gains
  Cover, which lasts up to 45 s per the tooltip, and takes the hits meant for the character. Challenge puts the
  character at the top of enmity for 30 s and cancels the familiar's Cover (DescriptionString 1733; research 13).
- **Statuses.** Cover is 2412, on the familiar; Covered is 2413, on the character (awgil BossMod,
  `ActionQueue/Melee/BST.cs` lines 120–121).
- **Who tanks.** The familiar tanks when the character has Covered (2413) or any hostile's `IBattleNpc.TargetObjectId`
  is the familiar's `GameObjectId`; this is how Rotation Solver Reborn reads it. The familiar is a BattleNpc with
  `OwnerId` == the character and subkind Pet. Only one beast is out at a time (the gauge's ActiveBattlehorn).
- **Rotation Solver Reborn also Snarls.** Its Beastmaster rotation by default uses one Snarl, only when the target has
  status 2552, a First Board battle-1 mechanic (likely the Bone Knight's Forward Guard). Its "FamiliarTankSwap" option
  is off by default. While any nearby enemy has 2552, Crucible SUX leaves Snarl and Challenge to it.

**HP rules** (the in-game guide, DescriptionString 1732–1742):

- Neither the character nor the familiars regenerate HP. HP carries over from battle to battle on a board.
- HP comes back only from:
  - crucible items: Beast Potions restore 10/23/36/50% "to self or familiar" (XBMItem 76–79)
  - campsite rest: the character always rests, plus a set number of familiars. Resting fewer familiars than the
    maximum restores more HP to each one that rests.
- A familiar at 0 HP is incapacitated and cannot be summoned again in combat.
- **Losing a battle expels you from the board.** The character's death therefore costs the whole run.

**Bonuses this design touches** (research 15 §2):

| Bonus | Condition | Points |
|---|---|---|
| No Beast Left Behind | no beast KO'd | 1,000 |
| Willing Sacrifice III / II / I | 1 / 2 / 3 beasts KO'd | 750 / 500 / 250 |
| Selfless | every beast at 100% HP at the clear | 1,000 |
| Self-preservation | character at 100% HP at the clear | 250 |
| Economic Ethos | no crucible item used (any number after the first costs nothing more) | 750 |
| Sleepless Survival | no campsite rest | 375 |

"At the clear" means when the final battle ends, and nothing can heal after that. Damage taken in the **final**
battle can only be undone with items used during that battle.

## 2. Why the handoff point is not a fixed 25% step

The owner asked for 25% increments and whether a better step exists. A fixed step ignores how hard the fight hits:

- **On the First Board,** 25% of a beast's HP may take a minute to lose, so handing off at 50% wastes beast HP the
  character could have spared.
- **Under the Third Degree and on the Master's boards,** enemies hit harder. A tankbuster can take more than 25% in one
  hit, so a beast handed off at 50% can be KO'd before the next 15 s decision.

**The better step is the damage the tank will take before the next decision.** For each actor (character, beast), the
advisor keeps a rolling measure of the damage taken while that actor held aggro:

- `Dtps`: HP% lost per second, averaged over the last 20 s of tanking
- `BigHit`: the largest single drop seen in this battle

Projected loss over the next window:

`loss = max(Dtps × 15 s, BigHit) + margin`

`margin` is 10 points, so one unexpected hit does not KO the tank. A tank is safe for the next window when
`HP% − loss ≥ floor`.

- **Floors:** character 35% (death ends the run), beast 15%. Both are Settings.
- **Before any data:** the advisor assumes 25% per window, which is the owner's increment. It then adapts once hits come
  in.
- **Known tankbusters:** when an enemy starts casting an ability that the intel lists as `HighestEnmity` or
  `SingleTarget`, the advisor checks the cast's projected hit against the current tank (`BigHit`, or 25% unknown)
  before the cast ends. If the other actor is safer, it hands off now, while the cast is still going.

## 3. Choosing the tank (every time the shared cooldown is ready)

1. **Safety first.** If only one actor is safe, it tanks.
2. **Neither safe:** the one with more HP after the projected loss tanks, and the heal rule (§4) runs.
3. **Both safe:** the cheaper one in score tanks. The cost of letting actor X take this window's damage:
   - **Beast:**
     - 0 if Selfless is already lost (a beast is KO'd, or below 100% with no recovery left before the clear)
     - 0 if recovery before the clear will bring it back to 100%: a campsite before the final battle with room for
       it, or healing items already counted for this run
     - otherwise 1,000 (Selfless)
     - plus the KO risk: (1,000 − the Willing Sacrifice tier it falls into) × the chance the next window KOs it. That
       chance is 0 when safe, so this term matters only for unsafe tanks.
   - **Character:**
     - 0 if recovery before the clear will bring it back to 100%
     - otherwise 250 (Self-preservation)
     - the run-loss risk outweighs everything else, which is why the floor is higher
4. **Ties** go to the beast. It is what Snarl is for, and the character's HP is the run.
5. **Hysteresis.** The advisor does not hand off again for one window unless step 1 forces it. Snarl's Cover lasts the
   whole 15 s window anyway.

Recovery before the clear needs to know:

- which battle this is (the final one is the boss space)
- whether a campsite lies ahead of it on AutoDuty's path, and how many familiars it takes
- the crucible items held

**Phase 1 (this change)** knows the battle and whether it is the boss. It treats "campsite ahead" as true for every
battle before the boss when AutoDuty's Rest is on (Clear goal), and false in Score mode or in the final battle. The
board graph read (PLAN F4) makes it exact later.

## 4. Healing with items (score-aware)

Items are used only when:

- **Safety:** the character's HP is below its floor, or a beast's projected loss would KO it, and no handoff can save
  it this window. Any healing potion is allowed; the weakest one that is enough comes first.
- **Final battle, securing Selfless or Self-preservation:**
  - Fires once the boss is low (below 15% HP) and an actor is below 100%.
  - Heals only when the bonuses still reachable are worth more than what using an item costs. The cost is 750 if
    Economic Ethos is still intact, else 0. For example, healing a beast to secure Selfless (1,000) is worth spending
    Economic Ethos (750).
  - Picks the smallest set of potions that reaches 100%.

The item path depends on the director's `UseItem(slot, beastId)`, from FFXIVClientStructs
`InstanceContentCrucibleOfTheUnbroken`. Whether this Dalamud build has it is checked in §5.

## 5. Where it lives

**Core** (`CrucibleSux.Core/Tactics.cs`, unit-tested, no Dalamud):

- `DamageMeter`: the rolling Dtps and BigHit per actor
- `ScoreOutlook`: which bonuses are still reachable
- `AggroAdvisor.Decide(view) → Snarl | Challenge | None`, with a reason string for the log
- `HealAdvisor`

**Plugin** (`CrucibleSux/Automation/AggroController.cs`):

- Acts only in a board battle, in combat, with a familiar out.
- Reads HP and targets, feeds the meter, and uses Snarl or Challenge on the enemy aimed at the actor giving up aggro.
- Every decision and its reason goes into the combat log.
- Settings: "Share aggro with the beast" (on), the two floors, and "Use items" (on).

