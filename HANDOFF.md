# HANDOFF: Crucible SUX

## Goal
A Dalamud plugin that fully automates FFXIV's Crucible of the Unbroken (the Beastmaster solo boards), tracks progress and readiness, and ships from xenofei's own custom repo. **SUX = Set-and-forget Unbroken eXecutor.**

## Decisions (docs/PLAN.md §6)
- AutoDuty: hybrid, over IPC only. AutoDuty has no license, so never copy its code.
- Rankings: off by default.
- Phases 0–1 shipped as v0.1.0. Phase 2 is the current work.

## Done
**Phases 0–1 (v0.1.0, released 2026-10-11):**
- scaffold, CI and release workflows, `pluginmaster.json`
- the read-only dashboard: Overview, One sitting, Beasts, Teams, Plugins, Settings, About
- `/sux check`

On 2026-10-11 the owner confirmed in game that v0.1.0 loads and reads a character without Beastmaster correctly.

**Phase 2 (2026-10-11, committed on branch `phase-2-orchestrator`; fresh-context review done and its 3 medium + 7 low findings fixed; not tested in game):**
- **Core:**
  - `Orchestrator.cs`: the queue state machine and watchdog.
  - `Preflight.cs`: the guard rails.
  - `RunHistory.cs`: run records and the report.
  - `SessionQueue.cs`.
  - 65 tests in total.
- **Plugin:**
  - `Integrations/AutoDutyIpc.cs`: overrides, plus `Run(territory, 1, bareMode)`.
  - `Automation/SessionRunner.cs`
  - `Ui/SessionTabs.cs`: the Run and History tabs.
  - Session settings in the Settings tab.
  - `/sux run` and `/sux stop`.
- PLAN.md has a Phase 2 status block with AutoDuty's facts and what is unverified.
- CHANGELOG has an `[Unreleased]` section.
- **AutoDuty source used:** commit 4a0dfdc (2026-10-09). The notes are in the PLAN status. The raw sources were in the session scratchpad and are gone after `/clear`; refetch them with `gh api repos/erdelf/AutoDuty/contents/<path>?ref=<sha> -H "Accept: application/vnd.github.raw"`.

**Phase 2b, the Beastmaster journey (2026-10-11, committed on `phase-2-orchestrator`; not tested in game):**
- **What it does:** the job quests, beast collection and levelling, each opt-in and off by default. It was built from game data and online sources only.
- **Status and gaps:** see PLAN.md, the Phase 2b status block, for the file list and everything unverified.
- **Research:** docs/research/06–10.
- **Tests:** 131 at 0.2.0.
- **Review:** the fresh-context review found 3 high, 7 medium and 6 low issues; the owner said "fix all", and all are fixed (PLAN.md Phase 2b block). Gourds are bought only with "Collect every beast" on (the recommendation for finding 16, applied under "fix all"; the owner can reverse it).

**v0.2.0 released (2026-10-11):** main was fast-forwarded to `phase-2-orchestrator` (b53192e) and tagged v0.2.0. CI and the Release workflow passed. The release has latest.zip and SHA256SUMS.txt, and `release.yml` committed pluginmaster.json for v0.2.0 to main (1ab4751). The branch is now merged; work continues on main or a new branch.

**First tester report and combat research (2026-10-11):**
- The tester (Maple) used the original BossMod, RSR and the First Board.
- The report, the causes and the proposed fixes are in docs/research/15-combat-findings.md. Research 11–14 hold the
  agents' scoring, fight and automation-coverage reports.
- The DataDump tool now prints all 33 bonus columns and decodes the enemy intel (XBMBattleDetailAction).
- The owner approved §5 items 1–3 and asked for a log the player can copy and paste. Item 4 (beast healing) waits for the addon trace.

**§5 items 1–3 built (2026-10-11, local commit, not pushed):**
- **Run settings:**
  - Core: `RunOptions.cs` holds `RunOptions` and `AutoDutyOverrides`.
  - `Configuration` gains RunTeamOfThree (on), RunShop (off), RunGoal (Clear) and PickTargets (on).
  - `AutoDutyIpc.Start` takes the options.
- **Target priority:**
  - Core: `EnemyIntel.cs` (`TargetPriority`).
  - `CrucibleData` reads XBMBattleDetailAction into `EnemyInfo.Abilities`.
  - `Automation/TargetPicker.cs` acts only in boards and in combat. It steps in only when a priority-1 or 2 enemy outranks the current target. After 4 re-picks of the same enemy in 20 s, it backs off for 30 s.
- **Combat log:**
  - Core: `CombatLog.cs` and `AoeShape.cs`.
  - The recorder is `Diagnostics/CombatRecorder.cs`. It is active in a board, or while a session or the journey runs, and samples at 100 ms. Finished logs are saved to `<plugin config>/logs` (30 kept).
  - It is shown in `Ui/LogTab.cs` and copied with `/sux report [full]`.
- **Plugins tab:** the original BossMod now shows a Warn caveat.
- **Tests:** 150.
- **Unverified in game:**
  - the cast origin and rotation from `CastInfo`
  - whether beasts have OwnerId == the player
  - the chat `LogKind` low-7-bit masking
  - `vnavmesh.Path.ListWaypoints`
  - whether RSR or BossMod fight the picker's retargets
  - AutoDuty accepting `Meta.Crucible.TeamMode` and `RecommendedTeamCap` (names read from 4a0dfdc)

## Next
0. **Release 0.2.1** (version bump, push, tag) only with the owner's approval. Then ask Maple to replay the First Board and send `/sux report` (and the full log if a hit or KO shows).
1. **Play-test** on a character with Beastmaster (the owner will arrange a tester). Run the First Board once, attended. Check:
   - Run tab preflight
   - AutoDuty takes the overrides (AutoDuty log: "Applied 10 override(s)" with the defaults; 13 in Score mode)
   - the board is reached, the watchdog does not trip, the history row and the report appear
   - AutoDuty's settings are restored afterwards

   Then do the Phase 2 proof: First and Second Board, 3× each, unattended.

   **Journey play-test**, attended, with `/xllog` open at Verbose:
   - "Strangers in the Wood" from acceptance through the gear setup
   - one capture (the squirrel in the Central Shroud)
   - one FATE
   - one gourd purchase

   Capture the Battlehorn assignment callback (Bestiary context menu) so it can be automated.
2. **Phase 3:** the native executor, so Crucible SUX makes the route, treasure and shop decisions itself. It also needs:
   - a way to set "Participate in rankings" (an addon callback, traced live)
   - Resume Progress recovery

## Check command
`dotnet build CrucibleSux.sln -c Release -warnaserror; dotnet test --project CrucibleSux.Tests -c Release --no-build; dotnet format CrucibleSux.sln --verify-no-changes`

## Open questions
- XBMPet location key 2 is decoded: the 12 instanced-duty beasts, every one sold as a gourd.
- XBMContent recommended rank (3/8/13/18/23) disagrees with the wiki (1/6/11/16/21). The code uses the sheet.
- XBMBattleDetail columns 5–9 are read as STR/INT/PR/MR/CON ratings. Unverified.
- **Phase 2, unverified:**
  - whether XBMResult shows after a lost board
  - whether AutoDuty's IsStopped stays false while zoning
  - whether 6 minutes is enough to reach the board from far away
  - whether 4 minutes is the right "stuck" threshold
- Unmapped:
  - degree and clear history
  - the board-map agent
  - the capture API
  - the Battlehorn assignment call
- **Phase 2b, unverified:** everything in game. PLAN.md has the list.
