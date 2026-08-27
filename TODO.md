# TODO

## Rebuild plan (approved 2026-08-23) — completed

All phases completed. See `OBLIVIONIS.md` for the pre-rebuild baseline.

## Next development plan — UI migration + orphan features (decided 2026-08-24)

Full plan: `docs/us-ui-migration-plan-zh.md`.

### Completed (2026-08-24 through 2026-08-25c)

- S0 — 5 design docs
- S1 — global-layer removal
- S2a — mode enum Off→Vanilla + Disabled
- S2b — action scope field-level last-wins + ActionTuningRecord layered table + H3 Race-layer contexts
- S3 — Sustainer path + external-action end-to-end
- S4-Tuning-Backend — baseline Def + consumer wiring
- S4-Orphan-Sync — Ferrite path for A7/A3/A4
- S4-Scope-Tree — A6 per-action scope switch (Global layer)
- S4-Diag-Foundation — camera indicator + DebugAction entry
- S4-Diag-Panel — diagnostics panel (overlay + draggable window + PostDraw)
- S5 — delete dead code (SqueakGlobalActionPolicy, experimentalRaceAllowlist, localization patch, developerToolsEnabled)
- Goal A — ActionEntry/TriggerBinding wrapper + external-action firing
- **双源统一** — delete globalActionEnabled/GlobalActionEnabledRecord/GetActionGlobalScope/SetActionGlobalScope
- **voicePackDefaultSeeded** — delete Scribe sentinel
- **dist/ 残留** — delete stale comp fields from test pack XMLs (working-tree only, dist/ is gitignored)
- **Legacy 兼容桥删除** — delete entire Legacy/ directory + 11 file references + 3 legacy log events + UI Legacy SR tags
- **Baseline 预设系统** — tree-shaped Def + BaselinePresetImporter + sourcePresetDefName + PresetListWidget
- **路由表开放** — VoicePackCompAttach canonical auto-attach + 3 new v2 events + LogTests registry 6→9 + SKILL sync (456b2a4)
- **审计修复 A1–A3** — actionTuning built-in-only contract (24f958c); BuildFallback preserves mode / true bypass survives crash (23785a3); CompTick head bypass gate before sustainer maintenance (11904d5)

### Remaining (next goal)

- **分层心情调音 + Race/Xeno scope UI（B1+B3 并块，方案 A）** — unified MoodTuningRecord three-layer table; race.moods → Race layer (decision flip, no longer global moodOverrides); xenotype overlays race; migration moodOverrides→layer 0 + XenotypePresetRecord.moodOverrides→layer 2; fold mood resolution into snapshot contexts; importer rewrite; UI three-layer scope tree + mood editor v1. Commit split: 1 data+migration+runtime+importer, 2 UI, 3 docs.
- **专项测试** — layered merge semantics ('global off + xeno on' actions; race→xeno mood inheritance). Extract merge logic to Kernel/Pure alongside the layered-mood block; Runtime harness for Verse-coupled residue (includes BaselinePresetImporter merge extraction).
- **S4-Polish（纯视觉，最后）** — filtering (author/race/conflict), componentized help, narrow responsive, visual modernization (eval doc first), footer build identity, distance preview chart.
- **docs/workdocs/ 移除** — delete temporary task-book directory after all remaining blocks land.
- **Ferrite UI 游戏内稳定化** — maintainer step (requires RimWorld runtime).

## Pending decisions / follow-ups

- [ ] Workshop display name and license (maintainer only; do not invent).
- [ ] HAR reflection discovery generalization (currently a catalog-side TODO; assembled-only).
- [ ] In-game crash/assignment matrix per `docs/ui-phase3-implementation-notes-zh.md` (maintainer step).
- [ ] First release prep: About icon/preview, final description, runbook US copy adaptation.
- [ ] Before any first push: maintainer decides how to handle the reachable-history personal absolute path (pre-fix `MEMORY.md` line in commits `eb2ac90..dc8c598`); no remote exists so there is no external exposure today.
- [ ] scripts/CI migration: GitHub/Steam build-pack scripts and CI workflows remain deferred until first release prep.
- [ ] Baseline preset system: no unit tests (BaselinePresetImporter couples Verse/Scribe), no in-game validation (PresetListWidget), no shipped example preset Def XML.
- [ ] Builtin fallback table maintenance review (2026-08-27): per-race Defs already support race-level independent maintenance; same-race multi-Def last-wins = single-owner contract (no Def-level field merge); BuiltInActionKeys whitelist closes external keys out of the builtin table; decide whether to publish example fallback/baseline Def XML as doc fixture (currently zero shipped Defs, empty = valid).
