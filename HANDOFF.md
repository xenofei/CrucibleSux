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
  - 57 tests in total.
- **Plugin:**
  - `Integrations/AutoDutyIpc.cs`: overrides, plus `Run(territory, 1, bareMode)`.
  - `Automation/SessionRunner.cs`
  - `Ui/SessionTabs.cs`: the Run and History tabs.
  - Session settings in the Settings tab.
  - `/sux run` and `/sux stop`.
- PLAN.md has a Phase 2 status block with AutoDuty's facts and what is unverified.
- CHANGELOG has an `[Unreleased]` section.
- **AutoDuty source used:** commit 4a0dfdc (2026-10-09). The notes are in the PLAN status. The raw sources were in the session scratchpad and are gone after `/clear`; refetch them with `gh api repos/erdelf/AutoDuty/contents/<path>?ref=<sha> -H "Accept: application/vnd.github.raw"`.

## Next
1. Plan the opt-in quest, capture and leveling automation the owner asked for (research: docs/research/06-quests-leveling-capture.md).
2. **Release:** once the owner approves a push, bump `<Version>` to 0.2.0.0, rename `[Unreleased]` to `[0.2.0] - date`, and tag v0.2.0. Ask before pushing or tagging.
3. **Play-test** on a character with Beastmaster (the owner will arrange a tester). Run the First Board once, attended. Check:
   - Run tab preflight
   - AutoDuty takes the overrides (AutoDuty log: "Applied 7 override(s)")
   - the board is reached, the watchdog does not trip, the history row and the report appear
   - AutoDuty's settings are restored afterwards

   Then do the Phase 2 proof: First and Second Board, 3× each, unattended.
4. **Phase 3:** the native executor, so Crucible SUX makes the route, treasure and shop decisions itself. It also needs:
   - a way to set "Participate in rankings" (an addon callback, traced live)
   - Resume Progress recovery

## Check command
`dotnet build CrucibleSux.sln -c Release -warnaserror; dotnet test --project CrucibleSux.Tests -c Release --no-build; dotnet format CrucibleSux.sln --verify-no-changes`

## Open questions
- XBMPet location key 2 (13 special beasts): the source is not decoded.
- XBMContent recommended rank (3/8/13/18/23) disagrees with the wiki (1/6/11/16/21). The code uses the sheet.
- XBMBattleDetail columns 5–9 are read as STR/INT/PR/MR/CON ratings. Unverified.
- **Phase 2, unverified:**
  - whether XBMResult shows after a lost board
  - whether AutoDuty's IsStopped stays false while zoning
  - whether 6 minutes is enough to reach the board from far away
  - whether 4 minutes is the right "stuck" threshold
- Unmapped: degree and clear history, the board-map agent, the capture API.
