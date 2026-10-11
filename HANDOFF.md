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
- **Tests:** 131.
- **Review:** the fresh-context review found 3 high, 7 medium and 6 low issues; the owner said "fix all", and all are fixed (PLAN.md Phase 2b block). Gourds are bought only with "Collect every beast" on (the recommendation for finding 16, applied under "fix all"; the owner can reverse it).

**v0.2.0 released (2026-10-11):** main was fast-forwarded to `phase-2-orchestrator` (b53192e) and tagged v0.2.0. CI and the Release workflow passed. The release has latest.zip and SHA256SUMS.txt, and `release.yml` committed pluginmaster.json for v0.2.0 to main (1ab4751). The branch is now merged; work continues on main or a new branch.

**First tester report and combat research (2026-10-11):**
- The tester (Maple) used the original BossMod, RSR and the First Board.
- The report, the causes and the proposed fixes are in docs/research/15-combat-findings.md. Research 11–14 hold the
  agents' scoring, fight and automation-coverage reports.
- The DataDump tool now prints all 33 bonus columns and decodes the enemy intel (XBMBattleDetailAction).
- No plugin code has changed yet. The owner is to pick from §5: run settings, target priority, cast audit, beast healing.

## Next
0. **Owner decision on research 15 §5.** Then a 0.2.1 with the run settings, at minimum: team cap 3 and shop off. The
   push and release need fresh approval.
1. **Play-test** on a character with Beastmaster (the owner will arrange a tester). Run the First Board once, attended. Check:
   - Run tab preflight
   - AutoDuty takes the overrides (AutoDuty log: "Applied 7 override(s)")
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
