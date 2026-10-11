# Crucible of the Unbroken: player gripes and plugin feature ideas

Research date 2026-10-11. Game patch 7.56 (Crucible launched), Season 1 rankings postponed.

## Method and coverage (read first)

- Reached: five Square Enix forum threads (sampled pages only, see below), ConsoleGamesWiki, Power Up Gaming, search snippets for MassivelyOP.
- NOT reached: MassivelyOP "Wisdom of Nym" article and its sibling posts (HTTP 403, so only search-engine summaries); Icy Veins guide (403); Reddit (fetch blocked, and search surfaced no Reddit threads); ResetEra/NeoGAF, Kotaku, PC Gamer, Push Square, YouTube (searches returned nothing relevant). Gripe counts below are therefore forum-weighted and low-confidence on absolute frequency.
- Forum text was summarised by a fetch model; quotes are as returned by it and not re-verified against the raw page. Each thread was sampled on only 1-2 pages out of 6-26.
- "Frequency" = distinct threads/sources (of 5 forum threads + MassivelyOP + wiki) in which the gripe appeared in the sampled pages. Severity 1-3 (3 = blocks progress or makes players quit).

Threads (T1-T5):
- T1 https://forum.square-enix.com/ffxiv/threads/531731 "Crucible is extremely poorly designed..." (156 posts; first 10 read)
- T2 https://forum.square-enix.com/ffxiv/threads/531241-Crucible-of-the-Unbroken-is-not-fun-and-shows-DT-s-problem-with-visual-clarity.?p=6842821 (posts 201-210 read)
- T3 https://forum.square-enix.com/ffxiv/threads/531377?p=6845108 "Beastmaster feedback and Improvements" (posts 51-59) and https://forum.square-enix.com/ffxiv/threads/531377 (posts 1-10)
- T4 https://forum.square-enix.com/ffxiv/threads/531250-Beastnaster-feedback-post-release?p=6841827 (posts 91-100) and https://forum.square-enix.com/ffxiv/threads/531250-Beastnaster-feedback-post-release (posts 1-10)
- T5 https://forum.square-enix.com/ffxiv/threads/531804-Beastmaster-Crucible-of-the-Unbroken-Great-Content-but-Several-Adjustments-needed (posts 1-3)
- W = https://ffxiv.consolegameswiki.com/wiki/Crucible_of_the_Unbroken
- M1 = https://massivelyop.com/2026/09/18/final-fantasy-xiv-delays-its-crucible-of-the-unbroken-rankings-for-a-beastmaster-balance-pass/ (search snippet only)

## Ranked gripes (frequency x severity)

### 1. Third Board / Third Degree difficulty spike, and the job quest is gated behind it
- Freq: 5 (T1, T2, T4, T5, M1/W delay). Severity 3. Score 15.
- Quotes: "Job quests should not be gated behind this level of difficulty in the first place." (T1 OP). "The jump of difficulty between 2 and 3 is massive." (T2 #209). "should have been an optional mode and not required to continue the BST quests" (T4 LanaC). "Third Degree feels completely disproportionate by comparison." (T5).
- Data point (unverified claim by a poster): "Nearly 46% of players who have reached max level on Beastmaster have not completed the job quest." (T1)
- Mechanics: first three boards are required for job quests, Master's boards optional (W). Each degree adds 42% enemy HP and damage (https://powerupgaming.co.uk/2026/09/13/ff14-crucible-of-the-unbroken-guide-beastmaster/).
- Idea A (information): Quest-gate tracker: which of boards 1-3 are cleared, best rank per board, next quest step blocked. 
- Idea B (decision support): Readiness check before Third Board: team beast ranks vs the recommended 11-15 (Power Up Gaming table), roster size 14, food/item stock.
- Cannot do: changing difficulty or bypassing the quest gate (server-side).

### 2. Healing/sustain item RNG: runs lost because no heal item dropped or was stocked
- Freq: 3 (T1, T2, T3). Severity 3. Score 9.
- Quotes: "you might STILL die simply because you did not get enough sustain items." (T1). "I didnt get a single Healing Potion as a drop or in the shop" (T2 #201). "It still feels bad if the game repeatedly refuses to drop healing potions for consecutive stages" (T3 #57). Suggested dev fix: "always at least one healing item at treasure nodes".
- Context: automatic HP regeneration is off; regular potions disabled; start each board with one G3 Beast Potion and Ring of Sacrifice; shops sell 8 feed / 4 crucible items / 4 beast gear (W, Power Up Gaming).
- Idea A (decision support): shop advisor that warns "you have N heals, M spaces left, campsites ahead: X" using the fixed board layout (layouts are fixed per board per W).
- Idea B (information): per-run inventory and heal-count HUD; log of item drops per node for the community to build drop-rate stats.
- Idea C (automation, unwise): save-scumming via Suspend to re-roll drops. Suspend only works on cleared spaces and one save exists (W); it is exploit-adjacent and may break after a balance patch. Flag as unwise.
- Cannot do: change drop tables (server-side).

### 3. Beast rank levelling grind (repeat the same board over and over)
- Freq: 3 (T2, T3, T4). Severity 2-3. Score ~7.
- Quotes: "Upping your beast is a freaking nightmare, since you have to do the SAME FREAKING BOARD OVER AND OVER again." (T2 #209). "Leveling pets is too grindy and it's only going to get worse." (T3 Moqi #9). "people who rush to 50 will still get stuck in crucible with subpar beasts" (T4 Pricia).
- Idea A (information): rank-XP tracker per beast, runs-to-next-rank estimate, which board gives most beast XP for the time.
- Idea B (decision support): "farm planner" suggesting which beasts to bring to which low board to level the ones below the Third Board target.
- Idea C (automation): auto-repeat runs. High ToS risk (see section Policy). Not recommended; keep to tracking.

### 4. Capture RNG (rare beasts, e.g. Zu, Cutter's Cry) and no way to target-farm
- Freq: 2 forum threads (T3, T4), several posters. Severity 2-3. Score ~5.
- Quotes: after "20+ runs without Zu ... Is that really fun?" (T4 #91, as paraphrased). "there shouldn't be any rng to the instance captures though." (T4 Arkdra #99). "Beast customization is incredibly lucklaster." (T3 Moqi).
- Counterpoint: all pets can be bought from the Kornago Merchant (T4 #91 quote), and developers said odds would be "generous" (T4 #98, relayed Famitsu claim, unverified).
- Capture rule from search summary: success is inversely proportional to target HP and rises if target is lower level (https://ffxiv.consolegameswiki.com/wiki/Beastmaster, via search summary, not opened).
- Idea A (information): per-beast capture/attempt counter and drop-luck stats (expected vs actual), missing-beast checklist with where to get each, and remnant cost at the Kornago Merchant as the guaranteed route.
- Idea B (decision support): warn when target HP is too high to capture.
- Cannot do: alter capture odds or auto-capture reliably.

### 5. Information overload in roster and team building (50 beasts, weaknesses, resistances, filters do not combine)
- Freq: 3 (T2, T3, T5). Severity 2. Score 6.
- Quotes: "the amount of information to learn and manage can quickly become overwhelming." (T5). "cross filter pets by resistance to boss autos AND their specific damage done to the boss itself" (T2 Valence #205). "Crucible turns into a spreadsheet simulator before you even enter." (T1).
- Idea (decision support): team recommender per board: match owned beasts and ranks against each enemy's weakness; combined filters; save roster presets ("We need to be able to save presets for the overworld and Crucible", T3 Moqi). In-game "Recommended Team" button exists for newcomers (https://allthings.how/final-fantasy-xiv-beastmaster-how-the-patch-7-56-limited-job-works/, via search summary).
- Data risk: wiki enemy stats and beast ranks still contain "?" placeholders (https://ffxiv.consolegameswiki.com/wiki/First_Board_of_the_Unbroken, via search summary). Plugin would need to read weakness data from game sheets (client-side, plausible) rather than the wiki.

### 6. Encounters are scripted/solved and not really roguelike (no variety, same enemies every run)
- Freq: 4 (T1, T2, T4, M1-adjacent). Severity 2. Score 8 but not plugin-addressable.
- Quotes: "Here you there is zero variability." (T2 #209). "The Crucible is NOT a roguelite" because enemies are the same every time (T4 Salzkartoffel). "This is not a good rougelike/rougelite at all." (T1).
- Idea (information): route notebook: record chosen path, node outcomes and score per attempt so players can compare routes; since layouts are fixed, optimal-path sharing becomes feasible.
- Unfixable by a plugin: design complaint.

### 7. Scoring penalties and Legendary incentives that discourage using the mode's mechanics
- Freq: 2 (T2, T3). Severity 2. Score 4.
- Quotes: shop purchases are "an immediate -500 points each and you cannot make up for those lost points." (T3 KageTokage #54). Legendary encourages "only use three beasts and don't use anything unique to the crucible" (T2 Merton9999 #202).
- Scoring facts: 250/625/1000 per enemy/elite/boss, degree bonuses 4,000/6,250/9,000, Legendary guarantee 17,875 (Unbroken) and 18,766 (Master's) (W).
- Idea (information): live score calculator and "points left on the table" readout; threshold sheet is linked below.

### 8. Ranking season delay, FOMO, and the top-10 gap
- Freq: 3 (M1, T1 #5, T4 Salzkartoffel). Severity 1-2. Score ~4.
- Quotes: ranking "adds incredible FOMO." (T4). "the gap between the top 10 players or so is enormous." (T1 #5). M1: the team "received considerable feedback on the job's combat balance and certain gameplay mechanics" and Season 1 was postponed (search snippet; W confirms "delayed indefinitely").
- Rules: seasonal board uses Third Degree, score from last three attempts per board, top 300 per region get Tourmaline Golem mount (W).
- Idea (information): local history of last three attempts per board and the projected ranking score. Leaderboard standings of others are server-side: impossible except by scraping Lodestone; not recommended.
- Conflicting source: AccountShark gave Sep 24, 2026 start (https://accountshark.net/blog/ffxiv-crucible-unbroken-season-one-rankings-guide); treat as outdated.

### 9. QoL gaps in the Crucible UI (no Restart, no forfeit from selection screen, can start with no beasts, hotbar bloat)
- Freq: 2 (T5, T3). Severity 1-2. Score ~3.
- Quotes: "It should simply be impossible to start an encounter without selecting any beasts." / "adding a 'Restart' option alongside Suspend and Forfeit." (T5). Hotbar: Borrow and Beast Mode "occupy two separate buttons" (T5).
- Idea A (information): pre-battle guard that warns on empty or under-filled roster.
- Idea B (automation, fragile): Restart helper that forfeits and re-enters. Uses addon clicks; breaks on UI changes; destructive if misfired (Forfeit erases the run, W). Ask-first confirmation required.
- Hotbar consolidation changes are not plugin-feasible without touching combo/action data; unwise.

### 10. Pet AI jank, action-queue delay, visual clarity, last-second AoEs
- Freq: 3 (T1, T2 title, T4). Severity 2. Score ~6 but mostly unaddressable.
- Quotes: "You are forced to deal with pet jank." and "Pet action queue delays also come into play here" (T1). Thread T2 title: "...shows DT's problem with visual clarity." "too many adds for the average player to follow" (T2 #209). "full of cheap shots" with "last second aoe" (T1 #10, Calysto, defending difficulty as overstated).
- Idea: none safe. Combat overlays/auto-dodge/auto-pilot are combat automation (see Policy). A purely informational pre-fight board note (enemy tells, from guides) is acceptable.

### Further gripes found (lower rank)
- Rewards feel stingy or grind-gated: "only 5 coins as reward? Not even a chest?" (T4 Pricia); "500 token for the the hairstyle might be a little much" (T2). Idea (information): remnant/token wallet and goal tracker, Weekly Challenge Log (large remnant rewards, W) checklist. Hairstyle Loosened Locks is a rare Apex/Legendary drop (W).
- Degrees locked behind clearing all Master's boards plus Blazing Trails: "It's a little annoying you have to clear all the master boards to get access to degrees." (T2 #205). Idea (information): unlock-chain tracker.
- Beasts as disposable, no naming/bonding/customisation: "You can't even name them." (T3 #53), "no nicknaming" (T3 Xander_Diabolos). Plugin could show local-only nicknames; real names are server-side, not possible.
- Pet-mod lag in Limsa from roaming beasts (T4 Pricia). Not Crucible-specific.
- Job is "one and done" after the Crucible, too weak elsewhere (T3 #53, T4). Out of scope.
- Strategy workaround offered by a player: "Just have the beasts tank and let them die while you focus on mechanics." (T1 #6).

## Existing community tools found
- Score-threshold spreadsheet (work in progress, linked from the wiki): https://docs.google.com/spreadsheets/d/1P7fXvsWMlCavs_4QbdjiU4Y7m2Ejeb0PhDaD0pun3GU/edit?gid=0#gid=0
- ConsoleGamesWiki Crucible pages (board, ephemeral items, degrees and leaderboards; many "?" values): https://ffxiv.consolegameswiki.com/wiki/Crucible_of_the_Unbroken , https://ffxiv.consolegameswiki.com/wiki/Ephemeral_items , https://ffxiv.consolegameswiki.com/wiki/Crucible_Degrees_and_Leaderboards
- ConsoleGamesWiki Discord: https://discord.gg/MDMDqkX
- Board table / tips: https://powerupgaming.co.uk/2026/09/13/ff14-crucible-of-the-unbroken-guide-beastmaster/
- Icy Veins Crucible guide (stage sections were "forthcoming" per search summary; page returned 403 to me): https://www.icy-veins.com/ffxiv/beastmaster-crucible-of-the-unbroken
- Season-rank explainer: https://accountshark.net/blog/ffxiv-crucible-unbroken-season-one-rankings-guide
- No Dalamud plugin, GitHub tracker, calculator or Reddit tool turned up in two targeted searches. Not found does not mean none exist (Reddit/Discord/GitHub were not searchable by me).

## Policy and feasibility flags for the plugin
- Automating gameplay (auto-repeat runs, auto-combat, auto-dodge) is against the FFXIV user agreement and is not accepted into the official Dalamud repo; the user ships from a third-party repo, but account-ban risk remains. I did not verify the current Square Enix policy text in this session; treat as a flag, not a finding.
- Server-side, impossible for a client plugin: drop or capture rates, leaderboard data for other players, difficulty tuning, real beast names, quest gating.
- Client-readable, plausible: own roster/ranks, board layout and enemy data from game sheets, run state, inventory, score components (needs verification in-game).

## Top 10 summary (rank, gripe, one feature idea)
See the chat reply; same order as sections 1-10 above except section order reflects the score column (6 and 10 scored 8 and 6 on frequency x severity but are not plugin-addressable, so they are listed lower).
