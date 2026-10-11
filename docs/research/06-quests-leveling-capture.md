# 06: Job quests, leveling and capture

Researched 2026-10-11 for the owner's request: optional automation of the Beastmaster job quests, beast collection and leveling. Confidence is medium.
- Quest, capture and plugin facts come from primary pages or plugin source.
- The rankings of leveling methods come from secondary guides.
- The Duty Support rule is the generic Limited Job rule.

## Verdict

| Want | Automatable with existing plugins? | Blocker |
|---|---|---|
| Job quests | **Partly.** Questionable has paths for 3 of about 13 quests, and those still have manual steps | No paths from *Into the Crucible* onward; board clears are needed (our session queue covers that part) |
| Capture | **No** | No plugin captures. Capture is "apply a mark, then kill"; it would need a native module and beast-to-mob spawn data |
| Leveling | **Partly.** A rotation exists (RSR), and the best source is FATEs | Duty Support and Trusts are closed to limited jobs, so AutoDuty's leveling mode does not apply |

## Leveling (Beastmaster starts at 1, caps at 50)

**EXP sources:**
- Beastmaster gets **bonus EXP from FATEs** [1][2].
- Captures and the Crucible boards also give EXP.
- Open-world kills get no bonus.

**Limited Job rules** (generic) [2]:
- no Duty Roulette, Duty Support, Trust or Command Missions
- dungeons give EXP only when synced, which needs a party
- level sync keeps job abilities
- no role actions

**Armoury bonus:** +100% up to level 90, per the generic Limited Job page.

**Guides:** they rank FATEs as the best solo route. Premade-party dungeons are "arguably fastest" and the Crucible is "not fastest" [3]. This comes from a search summary and is unverified.

**Not found:** EXP per board, and any hunting log or leves for the job.

**Quest EXP** [5]:

| Quest | EXP |
|---|---|
| Strangers in the Wood | 11,700 |
| Hearts Aligned | 1,496 |
| Master of the Lhu | 1,863 |
| Into the Crucible | 3,120 |
| Wit as Weaponry | 3,120 |
| Gobsmacked | 5,390 |
| Sin of Blood | 5,390 |

## Job quests

| Id | Quest | Lv | Needs |
|---|---|---|---|
| 71026 | Strangers in the Wood | 50 (any job) | Dawntrail, after "The Ultimate Weapon" (the wiki; our sheet note said 70058). Unlocks Beastmaster |
| 71027 | The Wilds Call | 1 | Capture 2 beasts |
| 71028 | Hearts Aligned | 8 | Reach 14 and 16; capture Geshunpest; combos on a dummy |
| 71029 | Master of the Lhu | 18 | Reach 22; a captive serpent fight; captures |
| 71030 | Into the Crucible | 30 | 15 beasts |
| 71031 | Wit as Weaponry | 30 | First Board clear; boss Spicekick Mustard |
| 71032 | Gobsmacked | 40 | 20 beasts; Second Board clear |
| 71033 | Sin of Blood | 40 | Waiting and a cutscene |
| 71034 | A Beastmaster's Path | 50 | Third Board clear |
| 71035 | Bonds Unbroken | 50 | None |
| 71036, 71037, 71045 | Free for All, Mastery Rematch, Blazing Trails | 50 | Not checked |

Ids 71028, 71031 and 71033 come from Garland links on the wiki.

**Questionable:**
- Repo: PunishXIV/Questionable, branch `new-main`, head 2dfa845 on 2026-10-11 [6].
- Paths exist for `5490_Strangers in the Wood`, `5491_The Wilds Call` and `5492_Hearts Aligned`. A file id is the quest id minus 65536.
- They were last touched in 6b71a73 on 2026-10-05.
- `5492` is marked WIP and has `WaitForManualProgress` steps for the dummy combos, kills and the capture.
- IPC [7], 22 endpoints in all, including:
  - `StartQuest(string)`, `StartSingleQuest`
  - `IsRunning`, `GetCurrentQuestId`
  - `IsQuestComplete`, `IsQuestAccepted`, `IsQuestLocked`
  - `Stop(string)`
  - quest-priority endpoints

## Capture

**The action** [8]:
- Level 1, instant, 2.5 s recast, 10 yalm range.
- It applies **Interest Captured** for 120 s, and **success is rolled when the target dies** while it is active.
- The odds are better at lower target HP and against a lower-level target. It does not work on higher-level targets.
- The gauge action shows the chance. The wiki says weakening the target "may improve the chance". No numbers were found.
- The wiki says it requires an unrestricted party. This probably applies to instanced duties.

**Where beasts are:**
- Per-beast zones and minimum levels are on the Master's Bestiary page [9].
- Beasts from dungeons and trials are marked "Gourd".

**Kornago Gourds** [10]:
- 17 beasts: Cu Sith from the first quest, then one per beast from an instanced duty.
- They unlock with the quests at levels 30, 40 and 50.
- They cost 10–30 Faded or 35–40 Bright Remnants of Resilience, which come from the Crucible.
- This is the only guaranteed route.

**Other facts:**
- The "13 special beasts" (XBMPet location key 2) are not defined in any source found. They probably overlap the gourd beasts.
- No daily limit was found.

**Existing automation:**
- RSR `BeastmasterRotation.cs` still has `//setting.TargetType = TargetType.Capture;` commented out (2026-09-30) [11].
- BossMod Reborn `xan/BST.cs` has no capture code (WIP) [12].
- AutoDuty has nothing.

## Unknowns

1. Board EXP, and numeric capture odds.
2. Requirements for 71036, 71037 and 71045, and whether Questionable will add the later quests.
3. Spawn coordinates per beast. The sheets give a zone, not a spot.
4. The Kornago Merchant's shop addon and callbacks (needs a live trace).
5. Whether Duty Support is really closed to Beastmaster specifically.

## Sources

1. https://ffxiv.consolegameswiki.com/wiki/Beastmaster
2. https://ffxiv.consolegameswiki.com/wiki/Limited_Job
3. https://mein-mmo.de/en/you-can-finally-almost-play-pokemon-in-ff14-we-show-you-how-to-level-up-effectively,1586720/2/ (also lists EXP bonus items)
4. (merged into 3)
5. Quest pages on ffxiv.consolegameswiki.com, e.g. /wiki/Hearts_Aligned
6. https://api.github.com/repos/PunishXIV/Questionable/contents/QuestPaths/7.x%20-%20Dawntrail/Class%20&%20Job%20Quests/Disciple%20of%20War%20Job%20Quests/Beastmaster%20Quests?ref=new-main
7. https://raw.githubusercontent.com/PunishXIV/Questionable/new-main/Questionable/External/QuestionableIpc.cs
8. https://ffxiv.consolegameswiki.com/wiki/Capture
9. https://ffxiv.consolegameswiki.com/wiki/Master%27s_Bestiary
10. https://ffxiv.consolegameswiki.com/wiki/Kornago_Gourd
11. https://raw.githubusercontent.com/FFXIV-CombatReborn/RotationSolverReborn/main/RotationSolver.Basic/Rotations/Basic/BeastmasterRotation.cs
12. https://raw.githubusercontent.com/FFXIV-CombatReborn/BossmodReborn/master/BossMod/Autorotation/Standard/xan/Melee/BST.cs
