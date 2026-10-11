# HANDOFF: Crucible SUX

## Goal
A Dalamud plugin that fully automates FFXIV's Crucible of the Unbroken (the Beastmaster solo boards), tracks progress and readiness, and ships from xenofei's own custom repo. **SUX = Set-and-forget Unbroken eXecutor.**

## Decisions (docs/PLAN.md §6)
- AutoDuty: hybrid, over IPC.
- Rankings: off by default.
- This session's scope: Phases 0–1.

## Done (2026-10-11)
**Research:** `docs/research/01–05`.

**Plan:** `docs/PLAN.md`.

**Phase 0 (scaffold):**
- `CrucibleSux.sln` contains:
  - the plugin (`CrucibleSux/`, Dalamud.NET.Sdk 15.0.0)
  - `CrucibleSux.Core` (pure rules)
  - `CrucibleSux.Tests` (xUnit v3 on MTP, 31 tests)
  - `tools/CrucibleSux.DataDump`
- `.github/workflows/ci.yml` and `release.yml`, plus `tools/make_pluginmaster.py`.
- README, CHANGELOG (0.1.0 unreleased), `docs/privacy.md`, `assets/icon.png`.
- Lock files committed.

**Phase 1 (read-only):** `/sux` opens a window with these tabs:
- Overview
- One sitting
- Beasts (ranks from the Bestiary page)
- Teams (experimental)
- Plugins
- Settings (rankings opt-in)
- About

`/sux check` prints the sitting to chat.

**Sheet map:** `docs/data/xbm-columns.md`.

## Next
1. **In-game check** (game was not running this session):
   - The plugin loads.
   - Overview numbers match the character.
   - The Bestiary page reader recognises ranks. RankEstimator assumes `grow × pct / 100`; if it is wrong, ranks read "not recognised".
   - The beast list and achievements load.
   - Screenshot the window.
2. Create the GitHub repo `xenofei/CrucibleSux`, push, then tag `v0.1.0`. The owner must approve the push.
3. Phase 2: the orchestrator through AutoDuty IPC, the combat controller, and F11 run history.

## Check command
`dotnet build CrucibleSux.sln -c Release -warnaserror; dotnet test --project CrucibleSux.Tests -c Release --no-build; dotnet format CrucibleSux.sln --verify-no-changes`

## Open questions
- AutoDuty has no license: integrate through IPC only, never copy its code.
- XBMPet location key 2 (13 special beasts): source not decoded.
- XBMContent recommended rank (3/8/13/18/23) disagrees with the wiki (1/6/11/16/21). The code uses the sheet.
- XBMBattleDetail columns 5–9 are read as STR/INT/PR/MR/CON ratings, unverified. Team scores depend on that reading.
- Unmapped: degree and clear history, the board-map agent, the capture API.
