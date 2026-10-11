# Crucible of the Unbroken scoring (patch 7.56), researched 2026-10-11

Sources: [CW-C] https://ffxiv.consolegameswiki.com/wiki/Crucible_of_the_Unbroken ; [CW-D] .../wiki/Crucible_Degrees_and_Leaderboards (same content, last edit 26 Sep 2026) ; [ET] https://eorzeantavern.com/crucible-of-the-unbroken/ ; [PU] https://powerupgaming.co.uk/2026/09/13/ff14-crucible-of-the-unbroken-guide-beastmaster/ ; [EI] .../wiki/Ephemeral_items ; [B1] .../First_Board_of_the_Unbroken ; [B2] Second_Board... ; [B3] Third_Board... ; [BM1] First_Master%27s_Board ; [BM2] Second_Master%27s_Board ; [AS] https://accountshark.net/blog/ffxiv-crucible-unbroken-season-one-rankings-guide ; [AT] https://allthings.how/final-fantasy-xiv-beastmaster-how-the-patch-7-56-limited-job-works/ ; [XI] https://v2.xivapi.com/api/sheet/XBMScoreBonus (33 rows, all fields EMPTY: schema lag).
Confidence: CONF = 2+ sources, SINGLE, INFER.

## 1. Base score
| Item | Pts | Src | Conf |
|---|---|---|---|
| Enemy space | 250 | CW-C, EI | CONF |
| Elite space | 625 | CW-C, EI | CONF |
| Boss | 1000 | CW-C, EI | CONF |
| Space total cap | 5000 (100% unless duty failed; failure reduces it) | CW-C, EI | CONF |
| Degree clear bonus 1/2/3 | 4000 / 6250 / 9000 | CW-C, ET, PU, EI | CONF |
| Forfeit outside battle | still gives progress-based score | AS | SINGLE |
Score formula per AS/AT: "spaces traversed, remaining player HP, other factors". Whether remaining HP is a separate term beyond Self-preservation: NOT FOUND. How bonuses combine with the base: not documented (CW-D says so). Likely simple addition (INFER).

## 2. The 33 bonuses (XBMScoreBonus has 33 rows; this list has exactly 33 = 4+3+2+3+1+3+... see count below)
Conditions are all "Clear the board ..." (must complete). Nothing is a negative penalty.
| Bonus | Condition | Pts | Src | Notes |
|---|---|---|---|---|
| Willing Sacrifice I | clear with 3 incapacitated familiars | 250 | CW-C, ET | CONF |
| Willing Sacrifice II | 2 incapacitated | 500 | same | CONF |
| Willing Sacrifice III | 1 incapacitated | 750 | same | CONF |
| No Beast Left Behind | 0 incapacitated | 1000 | CW-C, ET, PU | CONF |
| Beast Squad I | team of 6-7 familiars | 1250 | CW-C, ET | CONF |
| Beast Squad II | team of 4-5 familiars | 2500 | CW-C, ET | CONF |
| Beast Squad III | team of 1-3 familiars | 4250 | CW-C, ET, PU | CONF |
| Self-preservation | clear with Beastmaster HP 100% | 250 | CW-C, ET | CONF |
| Selfless | clear with all familiar HP 100% | 1000 | CW-C, ET, PU | CONF |
| Infinity and Beyond I/II/III | Universality 5/10/15+ times (Second Master's only per CW) | CW: 150/300/450 ; ET: 250/500/750 | CONFLICT |
| Unbeastable | no Damage Down or Vulnerability Up | 1250 | CW-C, ET | CONF |
| Nameday Attire | no beast gear acquired | 750 | CW-C, ET, EI | CONF; starting Ring of Sacrifice counting is unclear (EI) |
| Walking Armory | hold 10 beast gear (not First Board) | 500 | CW-C, ET | CONF; mutually exclusive with Nameday Attire |
| Economic Ethos | no crucible items used | 750 | CW-C, ET, EI | CONF; the free G3 Beast Potion given at start counts as a crucible item (EI); feed and gear are not crucible items |
| Habitual Horder (ET: "Hoarder") | hold 10 crucible items (not First Board) | 1000 | CW-C, ET, EI | CONF; only holding matters; 10 consumable slots |
| Starve the Fever | no feed given | 750 | CW-C, ET | CONF |
| Feed the Bold | feed 20+ times (Third Board, Second Master's only per CW) | CW 1500 ; ET 750 | CONFLICT |
| Coin Clutcher | clear without spending territory tokens | 500 (CW-C, ET) ; "375" in one search summary of Gamer Escape (unverified, GE 403) | CONF 500, minor conflict |
| Rolling in Riches | hold 2000+ territory tokens (not First Board) | 500 | CW-C, ET | CONF |
| Sleepless Survival | no campsite rest | 375 | CW-C, ET | CONF |
| 8 Gambits (Beast, Vile, Cloud, Seed, Wave, Scale, Soul, Ash kin) | team made only of that kin | 750 each | CW-C, ET, PU | CONF |
| First/Second/Third Degree | degree cleared | 4000/6250/9000 | CONF |
Count: 4 (WS x3 + NBLB) +3 +2 +3 +1 +2 (nameday, armory) +2 (ethos, horder) +2 (starve, feed) +2 (coin, riches) +1 (sleepless) +8 +3 = 33 with Beast Squad x3, Self-pres, Selfless, Infinity x3, Unbeastable -> matches 33 (INFER from count).
Board availability: per-board "always active" flag exists in XBMScoreBonus and XBMContent has 33 BonusPoints columns per board; actual per-board values/flags NOT retrievable (XIVAPI fields empty; sheet text is not in public schema yet). Wiki notes which are unavailable on First Board and which are Third/Second-Master's only. Board page "View Bonuses" in-game shows the live list.

## 3. Penalties
- No explicit point penalty was found anywhere. Only documented reduction: failing the duty lowers the space-cap term (CW-C, EI). "Penalties" sections of AS/AT list only gameplay penalties (HP 0 = fail, KO'd familiars sit out).
- Time: no time bonus found (duty limit 120 min on the board page). HP-loss/damage taken: only Self-preservation, Selfless, Unbeastable. Treasure choice, route choice: no direct score term found; route changes space count (e.g. BM2 middle path 13 vs 14 spaces, BM2 page) which changes the base total.

## 4. Tester claims
(a) "More than 3 beasts loses points": CONFIRMED in direction, exact mechanic is a missing bonus not a deduction. Beast Squad III (+4250) only applies for a team of 1-3 familiars; 4-5 gives 2500 (-1750), 6-7 gives 1250 (-3000). Above 7 gives nothing. ET says explicitly "A smaller roster scores higher". Ambiguity: "team"/"on the team" is almost certainly the roster prepared before entry (roster caps 10-15 exceed 7, and battle party is always 3), so BS III likely requires roster of 1-3 (recruiting only the 3 you fight with). INFER, not stated in any source; verify in game via Board Layout "View Bonuses". PU says "use 3-familiar teams, BS III very effective at Legendary ranks" (SINGLE, consistent). Related trade-off: campsite heal per party member drops with more familiars (B3: player alone 100%; 1 familiar 60% each; 2 40%; 3 30%) so a 3-only roster is also better for healing. Roster of fewer than 3: only 3 per battle anyway, so 3 is the max useful size under BS III.
(b) "Shop purchases cost points": NOT a per-purchase penalty (the known "-500 each" is unsupported by any source). Correct mechanic: Coin Clutcher (500) is lost if you spend ANY territory tokens (one purchase loses it, further purchases cost nothing in score). Economic Ethos (750) is lost if any crucible item is USED (buying one is fine; holding is fine), and buying 10 crucible items and holding gives Habitual Horder +1000. Buying beast gear loses Nameday Attire (750) (acquiring), buying 10 gives Walking Armory +500. Feed purchases and feeding lose Starve the Fever (750) (feeding), 20 feedings give Feed the Bold. Rolling in Riches (500) requires holding 2000+ tokens at clear (not First Board), so spending also threatens it. Hence shopping can swing ~500-2000+ points, but as lost bonuses. Applies to every rank/degree; degree itself only adds 4000/6250/9000. Resale is allowed (crucible items 45%, beast gear 20%, feed not resellable) [CW-C].

## 5. Ranks and thresholds
- Nine ranks low to high: Apprentice, Novice, Journeyman, Adept, Exemplary, Renowned, Elite, Apex, Legendary (XBMScoreRank, CW-C, ET).
- Min. legendary points (board infobox, consolegameswiki): First 17,500 [B1]; Second 17,500 [B2]; Third 17,875 [B3]; First Master's 18,750 [BM1]; Second Master's 18,500 [BM2]. CW-C/CW-D instead say "17,875+ guarantees Legendary on Unbroken boards, 18,766+ on Master's boards" (these are guaranteed-ceiling values; board minima are lower). Resolution: 18,766 vs 18,750 conflict stays unresolved; 18,766 looks like the max over Masters boards or a different bound; use per-board minima as the floor with a margin. Icy Veins snippet: Legendary lands "around ~17,500-18,500 depending on board" (SINGLE, snippet; page 403).
- Apex and lower thresholds: NOT FOUND. CW-D says an external spreadsheet is being compiled; link not retrievable. Apex+ achievements: Eighth Rank (Apex on Third Board), Built Different II (Apex+ on Second Master's).
- Rewards by rank: Remnants (faded for Unbroken boards, bright for Masters): B1 Legendary x10, Apex 8, Elite 6, Renowned 5, Exemplary 4, Adept 3, Journeyman 2, Novice 1; BM2 L14 A12 E10 R8 Ex6 Ad4 J2 N1. Legendary/Apex rare Loosened Locks drop.
- Is rank computed from the total score with a fixed per-board threshold? Yes implied by "Min. legendary pts."; degree matters because the 9000 degree bonus is likely required for Legendary: B1 max base 5000 + 9000 = 14,000 so 17,500 needs 3,500+ of other bonuses. Max stack check (INFER): e.g. 5000+9000+4250 (BS III)+1000+1000+1250+750*... reaches ~17-20k. First/Second degrees (4000/6250) make 17,500 hard: 5000+6250=11,250 needs 6,250 extras. Always play Third Degree for Legendary. Rankings (Season 1, postponed, ends 7.58): Third Degree only, last three attempts per board, Home World, per DC [AS, CW-D].

## 6. Degrees
Enemy HP up: First Board first encounter +42/+84/+126%; BM2 final boss +70/+140/+210% (CW-D). Damage scaling "unknown" (CW-D) vs "+42% HP and damage each" (PU, SINGLE). Third degree unlocked after Second Master's Board + Blazing Trails (CW-D).

## 7. Tactics found (all SINGLE-source unless noted)
- Keep the team at 3 for Beast Squad III (PU, ET).
- Bonuses conflict: Gambit (single-kin team) vs elemental coverage; Starve the Fever and Sleepless Survival vs feed/rest; Nameday vs Walking Armory; Economic Ethos+Horder compatible (hold but don't use) (ET, EI).
- Don't camp familiars if banking feed buffs (resting wipes eaten feed unless Lily Simular) (AS, CW-C).
- Campsite heals split among selected members; resting alone gives player 100% (B3, BM1, CW-C).
- Pre-borrow ability then swap; plenty of feed because potions disabled (PU); Snarl to tank (PU).
- No community per-board optimum routes/score runs found. Reddit not surfaced. YouTube videos (G0_G1NCwhJ0 "First Board Easy Guide Legendary & Third Degree", Iatnkt48UI0) exist but descriptions unreadable.

## 8. Biggest unknowns
1. Exact definition of "team" for Beast Squad (roster at entry vs party per battle).
2. Per-board active bonus flags and per-board point values (conflicts for Infinity and Beyond, Feed the Bold, Coin Clutcher 375/500).
3. Apex and lower thresholds; exact Legendary numbers (18,750 vs 18,766).
4. Whether remaining HP / time contribute beyond listed bonuses; whether failure/forfeit scoring depends on spaces.
5. XBMScoreBonus text: re-query xivapi once schema is updated (EXDSchema XBMScoreBonus.yml lists Name, Description, AlwaysActive but the xivapi build returned empty). Could read from game data via Lumina/SaintCoinach on the installed game instead (exact Name/Description/AlwaysActive).
