# 07: Beastmaster quest walkthroughs and Questionable

Researched 2026-10-11 (game 7.56). Confidence is medium.
- The step lists come from the consolegameswiki quest pages [1], checked against the game's own quest data in `docs/data/bst-progression.md`. That data is authoritative for NPC ids, positions and sequences.
- The Questionable facts come from reading its source, not from running it.

## What the wiki adds to the game data

**The chain:**
- There are 13 quests: 71026–71037 and 71045.
- The neighbouring rows 71038–71042 and 71046–71050 are unrelated, and 71043 and 71044 are empty [3].

**Dialogue choices:**
- Only two matter:
  - In **71030**, the lead to report is "A woman clad in lavender was escorted to Bentbranch Meadows."
  - In **71031**, at the first escort stop, choose "Speak with Loosefrox" (not "Part ways").
- Every other choice is flavour; the wiki records no difference. We pick the first option.

**71029:**
- Tempered Release and Borrow consume One with Nature, so resummon between uses.
- Example kin: a pugil for wavekin (Quelling Wave) and a coblyn for soulkin (Soul Crush).
- A soulkin Borrow can interrupt the Captive Serpent's cast. This is optional.

**71028:**
- The geshunpest is level 14, near the Tam-Tara crypt at about (19, 28).
- To reset the dummy, use "Reset Striking Dummy Enmity" (a subcommand).

**71034:** the Universality infinitive combo is a Sunstrider action followed by a Moonstalker action.

**The Crucible itself:**
- It is solo. A KO fails the board.
- You can suspend only on a cleared space [4].

**Rewards:** 71035 gives 0 EXP. For 71028 the gil reward is 241 in Garland and 108 in xivapi; the game sheet says 241.

## Questionable (PunishXIV/Questionable, branch `new-main`)

**License:** AGPL-3.0 [5]. We call it over IPC only and copy nothing.

**External paths** [7]:
- `QuestRegistry.Reload()` loads, in order: the downloaded bundle, the project directory, then `{ConfigDirectory}/Quests`.
- The user directory loads only when Questionable's debug mode is on (`/qst @debugmode`), it is a dev plugin, or it is a DEBUG build.
- There is no file watcher; reload with `/qst reload`.
- We therefore **do not ship paths into Questionable**. That would need every user to enable its debug mode.

**Paths for our quests** [6][11]:
- They exist only for 5490, 5491 and 5492 (= 71026, 71027, 71028).
- 5490 does not use the Cu Sith Gourd or equip the job.
- 5491 and 5492 stop at `WaitForManualProgress` for the battlehorn, the dummy combos and the capture. 5492's last wait is WIP.

**Schema** [9]: steps of type Interact, Combat, UseItem, Action, Emote, EquipItem, WaitForManualProgress, SinglePlayerDuty, AcceptQuest, CompleteQuest and others, with TerritoryId, DataId, Position and DialogueChoices.

**IPC** [10] (`Questionable.<name>`):
- `StartQuest(string)`, `StartSingleQuest(string)`, `Stop(string)`
- `IsRunning` (true whenever it is not in Manual mode)
- `GetCurrentQuestId`
- `GetCurrentStepData` (QuestId, Sequence, Step, InteractionType, Position, TerritoryId)
- `IsQuestComplete`, `IsQuestAccepted`, `IsQuestLocked`

## Unknowns

1. Arena and mechanics of the two field battles: the Captive Serpent (71029, seq 6) and Spicekick Mustard (71031, seq 6). The game data gives their event ranges.
2. The striking dummy's object id at the Bannock. The quests name ENEMY0 = 19456 (Questionable uses it as the dummy's DataId).
3. Whether the escort in 71031 needs us to stay within some range of Loosefrox.

## Sources

1. https://ffxiv.consolegameswiki.com/wiki/ quest pages: Strangers_in_the_Wood, The_Wilds_Call, Hearts_Aligned, Master_of_the_Lhu, Into_the_Crucible, Wit_as_Weaponry, Gobsmacked, Sin_of_Blood, A_Beastmaster%27s_Path, Bonds_Unbroken, Free_for_All, Mastery_Rematch, Blazing_Trails
2. https://www.garlandtools.org/db/doc/quest/en/2/71028.json
3. https://v2.xivapi.com/api/sheet/Quest (rows 71020–71050)
4. https://ffxiv.consolegameswiki.com/wiki/Crucible_of_the_Unbroken and /wiki/Lauda
5. https://raw.githubusercontent.com/PunishXIV/Questionable/new-main/LICENSE
6. https://api.github.com/repos/PunishXIV/Questionable/contents/QuestPaths/7.x%20-%20Dawntrail/Class%20%26%20Job%20Quests/Disciple%20of%20War%20Job%20Quests/Beastmaster%20Quests?ref=new-main
7. https://raw.githubusercontent.com/PunishXIV/Questionable/new-main/Questionable/Controller/QuestRegistry.cs
8. https://raw.githubusercontent.com/PunishXIV/Questionable/new-main/Questionable/Controller/CommandHandler.cs
9. https://qstxiv.github.io/schema/quest-v1.json
10. https://raw.githubusercontent.com/PunishXIV/Questionable/new-main/Questionable/External/QuestionableIpc.cs
11. The files `5490_Strangers in the Wood.json`, `5491_The Wilds Call.json` and `5492_Hearts Aligned.json` under [6]
