# Fights outside the Crucible: fight, flee or ride on

Design date: 2026-10-18. This answers the first journey play-test. When something attacked the character while it
travelled, did a quest or searched for a beast, it stood still or kept walking. It had no rule for enemies too strong to
fight.

## The rule (`ThreatPolicy`, Core)

When an enemy attacks the character or its beast:

1. **Already fleeing:** it keeps fleeing until combat ends. After a minute without getting away, it fights instead.
2. **Mounted** (the owner's rule: a mounted character stays mounted):
   - Moving: it **rides on**, and the trip leaves the attackers behind.
   - Standing (it has arrived, or a teleport is waiting for combat to end): it **rides away**, still mounted.
3. **On foot,** it **flees** when any of these holds; otherwise it **fights**:
   - an attacker is 3 or more levels above the character
   - 3 or more enemies are on it at once. This one does not count in a FATE, where groups at the synced level are
     normal.
   - its HP, at the rate it is losing HP in this fight, would be below the floor within 5 s. The floor is the Settings
     floor, 35% by default.
4. **Fighting:**
   - It gets off the mount, which happens only after a flee that failed.
   - It targets the attacker (the current target first, then the nearest) and walks to it.
   - Rotation Solver Reborn fights.
   - Since RSR never moves the character, the walking is Crucible SUX's job.

## Fleeing

- **Where it runs:**
  - While out of combat, the runner drops a breadcrumb every 2 s (the last 30 are kept).
  - A flight runs to the most recent breadcrumb at least 40 y from where the fight started, back the way the
    character came.
  - With no such breadcrumb, it runs 40 y straight away from the attackers.
- **How it runs:**
  - On foot it uses Sprint, and the rotation plugin is switched off.
  - It arrives still in combat: it runs on, away from the attackers again.
- **Why it works:** mobs give up a chase once the character is far enough from where they started.

## Dangerous spots (`DangerMap`)

- Every flight records the spot it started from.
- A spot (30 y radius) that made the character flee twice is dangerous. A capture search point or a FATE there is then
  skipped.
- A quest NPC there is not skipped, because a quest cannot be done anywhere else. There the one-minute give-up turns a
  third flight into a fight.

## KO

- In every activity, a KO'd character accepts the return to its home point.
- Once it is back on its feet, the run counts one problem; three in a row stop the journey, as before.

**Unverified in game:**

- that the return prompt is a SelectYesno whose text contains "home point"
- that mobs give up within 40 y of their spawn
- that Sprint (general action 4) is accepted by the call used
