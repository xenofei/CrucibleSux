# Crucible of the Unbroken: First and Second Master's Board playbook (patch 7.56)

Retrieved 2026-10-11. Confidence tags: [2+] two or more sources, [1] single source, [inf] inferred.
Main source "CGW" = ffxiv.consolegameswiki.com (wiki pages for each piece; all attack data is the game's in-game Board Layout text, which the wiki itself says "may not necessarily reflect all mechanics"). Other sources: ET = eorzeantavern.com/crucible-of-the-unbroken/ ; PU = powerupgaming.co.uk/2026/09/13/ff14-crucible-of-the-unbroken-guide-beastmaster/ ; IV = icy-veins.com/ffxiv/beastmaster-crucible-of-the-unbroken (403 to fetch; only search snippets).

IMPORTANT LIMITS
- NO source found gives timings, cast bars, sizes (radius), kill priority, beast trios, or healing advice per fight. Everything below is: wiki action tables (name, target, shape, element, status) + board layout + inference. "Size/timing" = not found for every attack.
- Shape vocab on the wiki: Front (cone/frontal), Circle (PBAoE centred on self, or ground circle if Target=Ground), Circle/Ring (donut or ring), Universal (raidwide / unavoidable), Rear, Cross, Single Target (tankbuster/auto on Target). "Ground" target = placed circle on a location.
- Weaknesses are given as one damage type or element per piece. Beast attack types are not mapped here; see /wiki/Master%27s_Bestiary.
- Pieces with "(can be nullified)" status: status may be negated by a Crucible item/gear effect (meaning not explained on the pages).

## 0. Cross-cutting facts

| Fact | Source | Conf |
|---|---|---|
| 3 beasts per battle, 1 summoned at a time; beasts have own HP, a downed beast is out for the run unless revived at campsite/item; player HP 0 = fail; no natural regen; potions disabled except ephemeral Crucible items and food | ET, PU | 2+ |
| Challenge (BST L30): gives player enmity, removes Cover. Snarl (BST L30): orders familiar to use Threaten = applies Cover 15 s; beast takes top enmity and absorbs damage meant for the player; removes player's Enmity Up. Parting Blow and Final Sting end Cover slightly early (do not rely on Snarl to block these) | CGW Crucible_of_the_Unbroken (Snarl/Challenge), ET | 2+ |
| PU strategy line: "Use Snarl to let sturdy familiars tank and avoid damage" | PU | 1 |
| Resting at a campsite strips food effects of the beasts who rest; Sleepless Survival bonus (375 pts) for clearing without resting | ET | 1 |
| Campsite healing (First Master's page, which camp it applies to is unclear): player alone 100% max HP; +1 familiar 75% each; +2 familiars 50% each; +3 37% each; +4 30% each | CGW First_Master's_Board | 1 |
| Territory Tokens buy food/items in shops; shop stock not listed | CGW, ET | 2+ |
| Element tags are "a strong hint, not a rule", not verified in game; encounters often pair several enemies sharing one element | ET | 1 |
| Boards are gauntlets; choosing a branch locks the others; spaces cannot be revisited | PU | 1 |
| Tester terms: "Pas de Seul" = Pas de Seul, Breaker of Beasts, boss of the FIRST BOARD OF THE UNBROKEN (L30 voidsent succubus). Not on either Master's Board. "Ogre piece" = Ogre Piece, also First Board of the Unbroken (L30). Neither appears on a Master's Board. Closest Master's analogue: Hapalit Piece (Voidsent Ogre type), Second Master's elite #4. | CGW Pas_de_Seul, Ogre_Piece; board pages | 2+ |
| Guttler: not listed on either Master's Board (appears only in a Move 14 cutscene with Lauda on Second Master's). Borgny and Toxic Mass = First Master's boss. Lauda + Thanatos Piece = Second Master's boss. | CGW | 2+ |

Pas de Seul data (for completeness, First Board of the Unbroken): L30 Voidsent (Succubus); weak Piercing; no vulnerabilities; Str 2 / PhysRes 3 / Con 5 / Int 5 / MagRes 5 stars. Blood Rain (self, Circle/Ring, unaspected), Cold Caress (highest enmity, single target, Poison). [1] https://ffxiv.consolegameswiki.com/wiki/Pas_de_Seul
Ogre Piece (First Board of the Unbroken): L30, weak Wind, vuln Blind and Poison. Scorching Smite (Front, Fire), Straight Punch (single target Player, Fire). [1] https://ffxiv.consolegameswiki.com/wiki/Ogre_Piece

### Degrees (apply to every board)
- Unlock after clearing Second Master's Board and finishing Blazing Trails (ET adds: clear all boards and Sylmond's L50 quests Free for All, Mastery Rematch, Blazing Trails). Set from Board Layout menu. [2+] CGW Crucible_Degrees_and_Leaderboards, ET
- Each degree raises enemy HP and damage; damage scaling "unknown". HP is additive percent per degree and varies by enemy. Documented examples: First Board of the Unbroken first encounter +42/+84/+126%; Second Master's final boss +70/+140/+210%. PU snippet says "+42% enemy HP and damage" per degree (generic). [CGW 1 (examples); ET/PU 1 (generic)]; so degree HP multipliers for First Master's enemies not found, and conflicts exist (+42% generic vs +70% for the 2nd Master's boss) so treat as per-enemy.
- Score bonus: 4,000 / 6,250 / 9,000 for First/Second/Third Degree clear. [2+]
- Third Degree is the only tier used for rankings (ranking season postponed). [2+]
- Achievements: Calcination I (First Master's) and II (Second Master's) = clear under Third Degree.
- NOT found: any degree adding new mechanics, new enemies or changed attack sets. Only HP/damage and score. A YouTube title exists for a Second Master's Third Degree clear (lK6plVt4IGk) and walkthroughs (55ytyredHu8, vpi1vTW1RMs) but YouTube descriptions/chapters did not load (page returns footer only).

---

# BOARD 1: FIRST MASTER'S BOARD

Overview [CGW https://ffxiv.consolegameswiki.com/wiki/First_Master%27s_Board, ET]: L50 (sync 50), beasts rank 16-20 (sync 20); roster max 12; 9 moves + boss (10 encounters), 2 camps (ET), 120 min limit; base STR/INT 155, RES 119; min legendary points 18,750; reward 4,500 gil, 40 Poetics; requires quest Free for All. Rank rewards: Legendary 12 Bright Remnant of Resilience (+rare Modern Aesthetics: Loosened Locks), Apex 10, Elite 8, Renowned 6, Exemplary 4, Adept 3, Journeyman 2, Novice 1. Move-1 loot: 1,000 Territory Tokens, Temporal Sand, random Elemental Fang, Tome of Reflection or Impervious. Other loot, shops, treasure contents: "???" (not found).

Note on "(N beast types)": the board page's counts are shown beside each encounter; meaning not explained (likely recommended beast types or number of enemy types). Unresolved.

## Route
1. Enemy #1 (1 type): Strix Piece
2. Branch: L Enemy #2 (4 types: Morbol, Seedling, Ochu, Carrion Broth) | R Random enemy or Treasure #1 (Corpse Flower, Queen Hawk)
3. Shop #1
4. Branch: L Campsite #1 | R Random enemy or Treasure #2 (Ice Dragon, Ice Sprite)
5. Elite #1: Gargoyle Piece
6. Branch: L Random enemy/Treasure/Campsite #3 (Administrator, Bug, Footman) | R Treasure #1 (choose from bountiful coffer)
7. Branch: L Elite #2 Golem Piece | R Enemy #3 (5 types: Treant, Slug, Sapling, Diremite, Biloko)
8. Branch: L Random enemy or Shop #4 (Progenitrix, Grenade, Bomb; Snoll, Pyrobolus) | R Shop #2
9. Boss: Borgny the Venomous + Toxic Mass
Route recommendation: none found for this board. Fewest-fights path by inference: R at 2 (random/treasure, risk of fight), L at 4 to Camp #1 before the elite, etc. [inf]. Which route avoids the elites: Elite #1 Gargoyle is unavoidable (move 5); Elite #2 Golem avoidable via move 7 right (but that is a 5-type enemy fight).

## Fights (all data CGW per-piece wiki pages, [1] unless stated; "Dmg" = damage type shown by the wiki)

### F1-1 Strix Piece (Enemy #1)
Cloudkin (Strix). Weak Piercing; vulnerable Heavy, Poison. Stars: Str 1, PhysRes 3, Con 3, Int 3, MagRes 3.
- Ultimate Focus: self buff, Magic Damage Up (nullifiable). Expect stronger magic next hit; shorten fight or strip buff if possible.
- On the Properties of Quakes: Universal, Earth. Raidwide, cannot dodge; heal after.
Priority: single enemy. Beasts: piercing damage (bird/pierce); Heavy or Poison for tempo. Danger: low. [inf for advice] https://ffxiv.consolegameswiki.com/wiki/Strix_Piece

### F1-2 Enemy #2 (left branch): Morbol, Seedling, Ochu, Carrion Broth
- Morbol Piece (Seedkin): weak Slashing; no vulnerabilities; Str 2/PR 3/Con 3/Int 3/MR 3. Extremely Bad Breath: Front, unaspected, Paralysis (nullifiable). Tremblor: Universal. Sidestep the cone; avoid paralysis on the player (paralysis on the player is a danger, as Lauda is flagged for it too).
- Seedling Piece (Seedkin): weak Slashing; vuln Slow, Petrification, Stun, Sleep, Bind, Heavy. Attack: single target Player, Blunt, Poison (nullifiable). Trivial add.
- Ochu Piece (Seedkin): weak Slashing; vuln Slow, Stun, Flat damage; Str 2/PR 1/Con 1/Int 2/MR 1. Acid Mist: self-centred Circle (PBAoE), unaspected. Move out or tank with beast.
- Carrion Broth (Ashkin slime): weak Ice; vuln Heavy; PhysRes 5 stars (very high), MR 1. Attack: single target Blunt + Paralysis (nullifiable); Sticky Spit: single target, applies Heavy. Use ice/magic, not physical.
Priority (inf): kill Morbol first (paralysis cone), then Ochu, Seedling; Carrion Broth last, damage with magic/ice. Weaknesses recommended: Slashing x3, Ice for the Broth. Danger: low-medium (paralysis chain).
Sources: https://ffxiv.consolegameswiki.com/wiki/Morbol_Piece , Seedling_Piece , Ochu_Piece , Carrion_Broth

### F1-3 Random enemy/Treasure #1 (right branch): Corpse Flower, Queen Hawk
- Corpse Flower Piece (Seedkin, Rafflesia): weak Wind; vuln Blind; Str 2/PR 3/Con 4/Int 3/MR 3. Rotten Stench: Front, unaspected. Devour: Front, applies "Devoured" (grab/engulf; effect not documented). Avoid its front.
- Queen Hawk Piece (Vilekin, Tarantula Hawk): weak Blunt; vuln Slow, Paralysis, Blind, Stun, Sleep, Flat damage; fragile (PR1/Con2/Int1/MR1). Final Sting: single target on highest enmity, Piercing. Has a tankbuster-like hit; use Snarl/Challenge to place it on a sturdy beast. Note: Final Sting is one of the abilities that end Cover early (ET), so Cover may not block it [inf].
Priority: Queen Hawk (fragile, burst) quickly; Corpse Flower slow. Danger: low.

### F1-4 Random enemy/Treasure #2 (right branch): Ice Dragon, Ice Sprite
- Ice Dragon Piece (Dragon): weak Fire; vuln Blind, Poison; Str 3/PR 3/Con 4/Int 3/MR 3. Cauterize: Front, Ice. Withering Eternity: Ground-targeted Circle, Ice, applies stacking Freezing. Move out of ground circle; freeze stacks are the danger. Healing: heal after each stack; no cleanse documented.
- Ice Sprite (Elemental): weak Fire; vuln Slow, Stun; PR 5 stars (immune-ish to physical), Con 1. Blizzard: single target highest enmity, Ice. Use magic/fire. https://ffxiv.consolegameswiki.com/wiki/Ice_Sprite_(First_Master%27s_Board)
Priority: Sprite is fragile to magic; Dragon is the damage dealer: kill Dragon first only if magic burst available [inf]. Fire beasts.

### F1-5 Elite #1: Gargoyle Piece (unavoidable)
Voidsent. No weakness, no vulnerabilities. Str 4/PR 4/Con 4/Int 3/MR 5 (very magic-resistant, so physical damage preferred). Rippling Evisceration: self-centred Circle/Ring (donut or ring) Slashing; Desolation: Front, unaspected, stacking Growing Dread (stacking debuff, effect not documented). Stay out of the cone, avoid stacking Growing Dread; likely swap beast if stacks high [inf]. Danger: medium. Use sturdy tank beast with Snarl.

### F1-6 Left: Administrator, Bug, Footman (all Forgekin, weak Lightning)
- Administrator Piece (Defense Node): vuln Interruption; Confound (interruptible: YES, Universal, applies Confused) and Flame Thrower (Front, Fire). Interrupt Confound if you have an interrupt (beast Interruption action).
- Bug Piece (Clockwork Bug): vuln Stun, Bind, Heavy. Actions not listed.
- Footman Piece (Clockwork Soldier): vuln Slow, Stun, Bind, Heavy; Headspin: self-centred Circle, Blunt.
Priority: Administrator first (confuse), then Footman. Lightning beasts. Danger low-medium.

### F1-6b Right: Treasure #1 (bountiful coffer; contents unknown).

### F1-7 Elite #2 (left of move 7): Golem Piece (First Master's variant)
Soulkin Golem. Weak Water; no vulnerabilities; Str 3/PR 4/Con 3/Int 4/MR 3. Stoneshower: self Circle/Ring, Blunt. Outcrop: Front, Earth. Water beasts (use caution: the "Golem Piece" page without the suffix is the Third Board golem; this is the "(First Master's Board)" page). https://ffxiv.consolegameswiki.com/wiki/Golem_Piece_(First_Master%27s_Board)

### F1-7b Enemy #3 (right): Treant, Slug, Sapling, Diremite, Biloko
- Treant Piece (Seedkin): weak Blunt; vuln Blind, Heavy, Poison. Acorn Bomb: Circle on Player, applies Nightmare. Move out of circle.
- Slug Piece (Wavekin): weak Blunt; vulnerable to nearly all statuses (Slow, Petrification, Paralysis, Blind, Stun, Sleep, Bind, Heavy, Flat, Poison). Aqueous Discharge: self Circle.
- Sapling Piece (Seedkin Bulb): weak Blunt; vuln Slow, Blind, Flat, Poison. Grab and Grow: targets Allies, applies "Growing" (buff to allies; probably stacks). Kill early to stop growth [inf].
- Diremite Piece (Vilekin): weak Ice; vuln many statuses (Slow, Petrification, Paralysis, Blind, Stun, Sleep, Bind, Heavy, Flat). Silkscreen: Front, applies Heavy.
- Biloko Piece (Seedkin): weak Ice; vuln Slow, Interruption, Flat, Poison. Natural Nurture: Allies single target, Water (heal/buff on allies). Interruptible via Interruption vulnerability: interrupt it.
Priority [inf]: Biloko (support heal) and Sapling (buff) first, Treant (Nightmare) next, then Diremite, Slug. Many status-vulnerable enemies: Stun/Slow/Petrify from beasts are effective. Danger: low-medium (5 enemies).

### F1-8 Left: Progenitrix, Grenade, Bomb; Snoll, Pyrobolus (random enemy or Shop #4)
- Progenitrix Piece (Elemental): weak Wind; vuln Interruption; Massive Explosion (Universal, Fire), Meltdown (Front, Fire, targets Player).
- Grenade Piece (Voidsent Bomb): weak Wind; vuln Slow, Stun, Flat. Massive Explosion (Universal, Fire). Kill or stun before it goes off [inf].
- Bomb Piece (Voidsent Bomb): weak Wind; vuln Slow, Stun, Flat. Fire II (Ground Circle, Fire), Self-destruct (self Circle, Fire). Stay away when it detonates.
- Snoll Piece: weak Fire; vuln Slow, Stun, Flat. Ice Spikes (self buff, reflect, nullifiable): avoid melee/auto hits while active. Hypothermal Combustion (self Circle, Ice).
- Pyrobolus Piece: weak Water; vuln Flat. Toxic Fumes: self, shape unknown, stacking Poison.
Priority [inf]: bombs die/interrupt first (Universal Fire); keep Progenitrix interrupted (interrupt Massive Explosion). Wind beasts for three, Fire for Snoll, Water for Pyrobolus. Danger: medium (Universals stack).

### F1-9 BOSS: Borgny the Venomous + Toxic Mass
- Borgny the Venomous (Dragon, L50): weak Earth; no vulnerabilities. Str 4/PR 4/Con 4/Int 5/MR 5. Toxic Vomit: Circle (target Player; place on player), unaspected, applies Toxicosis (stacking). Cauterize: Front, unaspected, applies Toxicosis. https://ffxiv.consolegameswiki.com/wiki/Borgny_the_Venomous
- Toxic Mass (Voidsent, Hecteyes): weak Water; vuln Slow, Stun, Bind, Heavy, Flat; Str 1/PR 5/Con 2/Int 3/MR 1 (physical-proof, magic-fragile). Noxious Explosion: Universal, applies Toxicosis. https://ffxiv.consolegameswiki.com/wiki/Toxic_Mass
- Toxicosis stacks: effect not documented (a stacking DoT/vuln likely) [inf]. No cleanse documented.
- Priority [inf, no source]: Toxic Mass is magic-weak, low HP profile and applies Toxicosis raidwide, so kill first with Water (magic) beasts, then Borgny with Earth. Keep a sturdy beast on Snarl for Borgny; step out of Toxic Vomit circle and Cauterize cone. Danger: medium-high. Healing: save campsite / items for pre-boss; Campsite is at move 4 or 6 only.
- Not found: cast timings, radius, any phase/enrage, any Degree-specific change.

---

# BOARD 2: SECOND MASTER'S BOARD

Overview [CGW https://ffxiv.consolegameswiki.com/wiki/Second_Master%27s_Board, ET]: L50, beast rank 21-25 (sync 25); roster max 15; 14 moves (13 if middle at the end), 3 camps; IL sync 135; base STR/INT 180, RES 132; min legendary pts 18,500; reward 5,500 gil, 50 Poetics; requires Mastery Rematch; unlocked after First Master's. Rank rewards: Legendary 14 Bright Remnant of Resilience, Apex 12, Elite 10, Renowned 8, Exemplary 6, Adept 4, Journeyman 2, Novice 1. Gambit achievements (Beastkin/Vilekin/Cloudkin/Seedkin/Wavekin/Scalekin/Soulkin/Ashkin Gambit bonuses).

## Route
1. Enemy #1 (2 types): Flauros Piece, Lightning Sprite (Earth weakness)
2. L Elite #1 (2 types): Mindflayer, Myconid | R Shop #1
3. L Campsite #1 (player alone 60%; +1 familiar 30% each) | M Treasure #1 | R Random enemy/treasure (Boogyman, Deepeye, Bomb, Light Sprite)
4. L Random or Shop #2 (Atomos, Red Gremlin, Gremlin, Pudding, Bavarois, Flan, Vodoriga, Menacing Dahak) | M Enemy #2 (4 types: Drake, Barbmole, Abaddon, Morpho) | R Treasure #2
5. Elite #2 (2 types): Durga, Spinner-rook (Fire, Blunt)
6. Campsite #2 (player + up to 2 familiars)
7. L Random elite or Treasure #3 (Medusa, Lamia, Cyclops) | R Shop (listed "Shop #2" again, probably Shop #3, page inconsistency)
8. L Treasure #3 | R Treasure #4
9. L Elite #3 Chimera Piece | R Enemy #3 Sphinx Piece (riddle)
10. Enemy #4 (4 types): Gigantis, Cyclops
11. Shop #3
12. L Random enemy/Treasure #4 (Swordsmog, Mogbreaker, Kinged Kupocurer, Kinged Kupocaster, Kupocurer, Kupocaster, Kinged Swordsmog, Melomog, Mogmugger) | M Campsite #3 (player + up to 3 familiars) | R Treasure #5
13. (skipped if middle of 12 taken) L Random elite or Shop #5 (Behemoth, Giant, Lightning Sprite) | R Elite #4 (3 types: King Ahriman, Hapalit, Dirty Eye)
14. BOSS: Lauda the Spellcleaver + Thanatos Piece
Route recommendation: IV snippet says "right, left, right, then straight at the final fork" for most consistent path, fewest elites and a camp before boss [1; only a search snippet]. Mapping onto the board: R at 2 (shop, skips Elite #1), L at 3 (camp), R at 4 (treasure), ... then straight (middle) at move 12 = Campsite #3 right before the boss, skipping move 13. This matches CGW's note that middle path shortens route to 13 and camp #3 precedes the boss. [IV 1 + CGW 1 + inf]. Mandatory fights on that route: Enemy #1, Elite #2 (Durga/Spinner-rook), then moves 7-10 (the Elite/Random at 7, 9 Chimera or Sphinx, 10 Gigantis/Cyclops).

## Fights (CGW per-piece pages)

### F2-1 Enemy #1: Flauros + Lightning Sprite (weak Earth)
- Flauros Piece (Verge Dragon): weak Earth; vuln Heavy, Poison; Str 2/PR 2/Con 4/Int 3/MR 3. Charged Lightning: Front, Lightning. Erratic Blaster: single target Player, Lightning + Paralysis (nullifiable).
- Lightning Sprite (Enemy 1 variant): weak Earth; vuln Slow; PR 5 stars. Line Voltage: Front, Lightning. https://ffxiv.consolegameswiki.com/wiki/Lightning_Sprite_(Second_Master%27s_Board_-_Enemy_1)
Priority [inf]: Flauros (paralysis on the player) first. Earth beasts. Danger low.

### F2-2 Elite #1: Mindflayer + Myconid
- Mindflayer Piece (Voidsent Soulflayer, aggro lvl 6): weak Slashing; vuln Heavy, Poison; Str 2/PR 3/Con 4/Int 4/MR 4. Void Water III: Circle on Player, Water, applies Water Resistance Down. Void Paralyze III: Universal, Lightning, Paralysis (nullifiable). Raidwide paralysis is unavoidable: bring Paralysis protection if it exists (nullify gear/items), else expect paralysis. Water Res Down stacks into Void Water.
- Myconid Piece (Seedkin Funguar): weak Water; vuln Slow, Blind, Bind, Heavy, Flat, Poison; frail. Spore Spill: self Circle, Poison. Big Burst: Universal, Poison.
Priority [inf]: kill Myconid fast (low HP, Poison Universal), then Mindflayer. Board dialogue: "Moisture dampens the proliferation of spores!" (hint: Water vs the spore enemy). Danger medium.

### F2-3 Random: Boogyman, Deepeye, Bomb, Light Sprite
- Boogyman Piece (Voidsent Baboulas): weak Ice; vuln Heavy, Poison; Str 3/PR 3/Con 4/Int 3/MR 2. Ripples of Gloom: Universal, Blind. Veil of Darkness: Universal, Invisible (possibly makes target untargetable; Lauda says "Can you detect the invisible?").
- Deepeye Piece: weak Wind; vuln nearly everything (Slow, Paralysis, Blind, Stun, Sleep, Bind, Heavy, Flat, Poison). Oogle: Universal, Petrification (player petrify; cannot be dodged).
- Bomb Piece (2nd Master's variant): weak Wind; vuln Stun, Flat. Self-destruct: Universal, Fire; Explosion: self Circle, Fire.
- Light Sprite: no weakness; vuln Slow; PR 5. Banish: single target highest enmity; Diffuse Light: Front.
Priority [inf]: Deepeye first (petrify raidwide) with stun/interrupt; Bomb (Universal self-destruct) second. Danger medium.

### F2-4 Left: Random/Shop (pool): Atomos, Red Gremlin, Gremlin, Pudding, Bavarois, Flan, Vodoriga, Menacing Dahak
- Atomos Piece: weak Blunt; Con 5; Shockwave Universal, Dark II Front.
- Red Gremlin Piece: weak Piercing; many vulns; Bad-mouth: single target Player, stacking Physical Vulnerability Up (swap tank often or kill quickly).
- Gremlin Piece: weak Piercing; many vulns; actions not listed.
- Pudding Piece (Flan): weak Wind; PR 5; Fire III Circle on Player. Bavarois Piece: weak Lightning; PR 5; Water III Universal. Flan Piece: weak Earth; PR 5; Thunder III Circle on Player. (Mapping is by fetched order; verify against Board Layout.)
- Vodoriga Piece: weak Piercing; Terror Eye Ground Circle.
- Menacing Dahak Piece: weak Ice; vuln Poison; Rotten Breath Front, Tail Drive Rear Blunt (avoid sides? it covers the rear, so stand to its flank).
Flans have PR 5 so use magic/elemental beasts. Priority [inf]: Red Gremlin first (vuln stacking).

### F2-5 Middle: Enemy #2: Drake, Barbmole, Abaddon, Morpho
- Drake Piece (Scalekin): weak Wind; vuln Blind, Poison. Blaze Spikes (reflect buff, nullifiable); Mangling Fang single target Piercing.
- Barbmole Piece (Beastkin Mole): weak Fire; vuln Slow, Blind, Bind, Heavy, Flat, Poison. Needles Out (self buff), Seeding Needles: Universal Piercing.
- Abaddon Piece (Wavekin Gigantoad): weak Water; vuln Blind, Flat, Poison. Actions not listed.
- Morpho Piece (Vilekin): weak Blunt; vuln Slow. Unwitting Wings single target Player, applies Wit's End; Delusion Dust Universal, Confused.
Priority [inf]: Morpho (Confuse Universal) first. Danger low-medium.

### F2-6 Elite #2: Durga + Spinner-rook (Fire, Blunt weaknesses)
- Durga Piece (Forgekin Avatar): weak Fire; vuln Blind; PR 4/Con 4/Int 4/MR 5 (very tanky). Atomic Ray: Universal, Fire. Grounding Jolt: Ground Circle, Lightning.
- Spinner-rook Piece (Forgekin): weak Blunt; vuln Slow, Stun; frail. Aether Charge: targets Allies (buffs/charges Durga, effect not documented). Kill Spinner-rook first [inf].
Danger: medium-high (Durga has strong resistances, Universal Fire). Blunt beasts for rook, Fire for Durga. This elite is mandatory.

### F2-7 Left: random elite or Treasure #3: Medusa, Lamia, Cyclops
- Medusa Piece (Chimera Lamia): weak Piercing; no vulns; Str 4/PR 3/Con 5/Int 3/MR 4. Petrifying Regard: Front, applies Stone Curse (petrify gaze; do not stand in front). Impassion: self Damage Up.
- Lamia Piece: weak Piercing; vuln Paralysis, Interruption, Flat, Poison. Terrorizing Miasma: self Circle, Hysteria. Raise: Allies, INTERRUPTIBLE (stop it from rezzing Medusa).
- Cyclops Piece (Random 3): no weakness; vuln Petrification, Blind, Flat, Poison; Str 4. 1000-tonze Swing: self Circle, Blunt. Glower: Front, Lightning + Paralysis.
Priority [inf]: interrupt Lamia's Raise; kill Lamia first; Medusa has Damage Up and gaze. Danger high-ish.

### F2-9L Elite #3: Chimera Piece
Weak Water; no vulns; Str 4/PR 4/Con 5/Int 4/MR 4. The Ram's Roar: self Circle, Ice, Frostbite (nullifiable). The Dragon's Roar: self Ring (donut), Lightning, Electrocution (nullifiable). Stand in the safe zone: for Ring, move to centre/under boss; for Circle, move out. Danger: medium. Water beasts. Wiki Chimera_(Beast) page exists for the capturable beast.

### F2-9R Enemy #3: Sphinx Piece (riddle encounter)
Forgekin; weak Slashing; no vulns; Str 1/PR 3/Con 4/Int 3/MR 3. Mnemonic Riddle and Numeric Riddle: both Universal Lightning (non-interruptible). Dialogue: riddles about numbers, memory, and "which child is kin of beast"; closing line "Prove your strength with answers true." Riddle answers/mechanic: NOT FOUND (a web search returned only Dragon's Dogma 2 content). Treat as raidwide Lightning twice; the riddle UI mechanic (answer prompt?) unknown, which is a blocker for automation.

### F2-10 Enemy #4: Gigantis + Cyclops
- Gigantis Piece (Spoken Cyclops): no weakness; vuln Blind, Heavy, Poison; Str 3/PR 3/Con 4. Smashing Stamp: Front, Blunt, applies Down for the Count. Gigantic Rage: self, Blunt, shape unknown (YouTube comment on a board-2 walkthrough mentions timing movement around the cyclops slam by element, single-user remark).
- Cyclops Piece as above. Danger: medium-high (two cyclops).

### F2-12 Left pool (moogle set): Swordsmog, Mogbreaker, Kinged Kupocurer, Kinged Kupocaster, Kupocurer, Kupocaster, Kinged Swordsmog, Melomog, Mogmugger
Moogle casters, mostly weak Blunt (healers/casters) or Lightning (melee), vulnerable Slow, Stun, Poison.
- Swordsmog Piece: weak Lightning; many vulns; no actions listed.
- Mogbreaker Piece: weak Lightning; vuln Blind, Poison. 100-kuponze Swipe (Front, Slashing), Moogle Tempest (self Circle, Slashing).
- Kinged Swordsmog: weak Lightning; Mogshield Swipe (Front, Blunt), Spirit of Pompetition (self Damage Up).
- Kupocurer Piece: weak Blunt; Pom Holy (shape unknown), Water (single target). Kinged Kupocurer: Pom Flood (Universal, Water, Water Resistance Down).
- Kupocaster Piece: weak Blunt; Pom Meteorain (Ground Circle, Fire), Blizzard (single target). Kinged Kupocaster: Pom Flare (Universal, Fire, Fire Resistance Down).
- Melomog Piece: weak Water; Moghemian Rhapsody: allies, Circle, stacking Damage Up (buffer; kill first).
- Mogmugger Piece: weak Water; Daylight Pommery (single target Slashing), Kupo and Good Night (no data).
Priority [inf]: Melomog first, then the Kinged casters (Universal resistance-down), then melee. Danger medium.

### F2-13 Left: random elite or Shop #5: Behemoth, Giant, Lightning Sprite
- Behemoth Piece (Beastkin): weak Earth; vuln Poison; Str 4/PR 5/Con 4/Int 4/MR 4. Ecliptic Meteor: Universal Fire. Charybdis: self Circle/Ring.
- Giant Piece (Forgekin Magitek Colossus): weak Lightning; vuln Slow, Stun, Bind, Heavy. Grand Sword: Front, Slashing.
- Lightning Sprite (Random 5): weak Earth; Cross Charge: self, Cross shape, Paralysis.
Danger: medium-high (Behemoth). Skippable with the middle path at 12.

### F2-13R Elite #4: King Ahriman, Hapalit, Dirty Eye
- King Ahriman Piece: weak Ice; no vulns; Str 2/PR 4/Con 4/Int 4/MR 5. Roulette (shape unknown), Curtains for Rank 5 (Universal, Unaspected).
- Hapalit Piece (Voidsent Ogre; likely closest to what the tester called "Ogre piece"): weak Wind; vuln Blind, Bind, Heavy, Poison; Str 3. Straight Punch: highest enmity single target, Blunt + Burns (nullifiable). Tankbuster: use Snarl to put it on a beast.
- Dirty Eye Piece (Ahriman): weak Ice; vuln Slow, Stun, Sleep, Flat; frail. Mortal Gaze: Universal, applies Doom (lethal timer unless cleansed; cure unknown). Stone: highest enmity single target, Earth.
Priority [inf]: Dirty Eye first (Doom). Doom handling in this mode is NOT documented. Danger high.

### F2-14 BOSS: Lauda the Spellcleaver + Thanatos Piece
Cutscene with Guttler and Lauda; Lauda taunts "Can you detect the invisible?" (Move 14 dialogue). [CGW Second_Master's_Board]
- Lauda the Spellcleaver (Hyur Midlander, L50): Weakness None; vulnerable to Poison only per the Lauda page (Lauda's own page lists only Poison). Beastly Aura: Universal, Wind, no status. Thunderbolt: Front, Lightning, target Player, applies Paralysis. https://ffxiv.consolegameswiki.com/wiki/Lauda_the_Spellcleaver
- Thanatos Piece (Second Master's Board; Soulkin Dullahan): weak Lightning; vuln Slow, Petrification, Stun; Str 2/PR 4/Con 2/Int 2/MR 3. Infernal Pain: Universal, unaspected. https://ffxiv.consolegameswiki.com/wiki/Thanatos_Piece_(Second_Master%27s_Board)
- The Second Master's board page's boss row lists "Vulnerable to Poison, Slow, Petrification, Stun" combined (union of both enemies). A search-summarising source claimed Lauda is not vulnerable to Paralysis; the Lauda page does not list Paralysis as a vulnerability, so Lauda is probably NOT paralysable (inf from absence; the user's "Lauda and Paralysis resistance" claim is consistent but not explicit anywhere).
- Boss HP: +70% / +140% / +210% for First/Second/Third Degree (CGW Degrees page, 1).
- Priority [inf, no source]: Thanatos has low Con (2 stars) and is lightning-weak and stunnable/petrifiable; likely kill first (reduces Universal Infernal Pain), then Lauda with Poison. Keep Snarl on a sturdy beast so Thunderbolt hits the beast; Thunderbolt's Paralysis on player is the main risk. Dodge the Front cone; Beastly Aura and Infernal Pain cannot be dodged: heal. Danger: highest on the board. Timings/radii/phases/enrage: not found. Use the campsite right before (Camp #3 via middle path).

---

## NOT FOUND (needs in-game / video verification)
1. Any cast timing, radius, telegraph width, or cast-bar names for any enemy (the wiki lists only name, shape, element, status).
2. Meaning of statuses: Toxicosis, Growing Dread, Growing, Nightmare, Devoured, Stone Curse, Hysteria, Wit's End, Doom, Down for the Count, Frostbite, Electrocution, Freezing, Burns (no pages checked).
3. Sphinx riddle prompt and answers.
4. Kill priority, beast trios, swap order, healing plans for any fight (only inference).
5. Contents of shops/treasure; which enemies appear together in "Random" nodes; meaning of "(N beast types)".
6. Per-degree HP% for First Master's enemies; whether degrees add mechanics.
7. Powerup Gaming and Eorzean Tavern pages had no per-fight content; Icy Veins page 403 (only snippets); YouTube chapters/descriptions did not load; no Reddit/Akhmorning/Gamerescape/Cryebunnie sandbox review done (sandbox https://ffxiv.consolegameswiki.com/wiki/User:Cryebunnie/Sandbox:Crucible_board surfaced in search, not opened).
8. Guttler's mechanics (not a Master's board fight).

## Source list
- https://ffxiv.consolegameswiki.com/wiki/First_Master%27s_Board
- https://ffxiv.consolegameswiki.com/wiki/Second_Master%27s_Board
- https://ffxiv.consolegameswiki.com/wiki/Crucible_Degrees_and_Leaderboards
- https://ffxiv.consolegameswiki.com/wiki/Crucible_of_the_Unbroken
- https://ffxiv.consolegameswiki.com/wiki/<Piece_name> (each piece; disambiguated forms noted inline)
- https://eorzeantavern.com/crucible-of-the-unbroken/
- https://powerupgaming.co.uk/2026/09/13/ff14-crucible-of-the-unbroken-guide-beastmaster/
- https://www.icy-veins.com/ffxiv/beastmaster-crucible-of-the-unbroken (snippet only)
