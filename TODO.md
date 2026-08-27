# TODO

## Rebuild plan (approved 2026-08-23) — completed

Decisions D1-D7 live in `MEMORY.md`. Rebuild commits: `a00bbfa` (Phase 1), `9de2161` (Phase 2), `0c4871a` (Phase 3); cleanup and archive pointer in `OBLIVIONIS.md`.

### Phase 0 - memory / privacy / baseline

- [x] Empty baseline commit `dc8c598` recorded as the OBLIVIONIS archive pointer candidate.
- [x] Replace the personal absolute SR path in `MEMORY.md` with repo-relative `../squeaky_ratkin`.
- [x] Record D1-D7 rebuild decisions in `MEMORY.md`.
- [x] Rewrite this TODO to the rebuild action surface.

### Phase 1 - de-SR-ize kernel/pure + US test gate

- [x] Rebuild `Source/UniversalSqueaker/Kernel/*.cs` (9 files): namespace `UniversalSqueaker.Kernel`; Ratkin seed, `SR_*` keys, and `DomainFilter` whitelist semantics removed.
- [x] Rebuild `Source/UniversalSqueaker/Pure/*.cs` (2 files): namespace `UniversalSqueaker`; math byte-equivalent, zero Verse.
- [x] `Source/UniversalSqueaker/UniversalSqueaker.csproj`: net472, `<Version>0.1.0-dev`, TreatWarningsAsErrors, Dev/Release flavors.
- [x] `tools/UniversalSqueakerKernelTests/` links new Kernel+Pure; RaceA/RaceB equal routing, neutral test keys, three-way sync, US 0.1.0 corpus.
- [x] Gate green: kernel tests replay zero delta; new code free of `SqueakyRatkin`/`Ratkin`/`SR_`.

### Phase 2 - runtime/assembly generalization

- [x] Runtime sources rebuilt under `Source/UniversalSqueaker/` (catalog/resolver/settings/logging/comp/production patches); product-domain filter and HAR Ratkin special-casing deleted.
- [x] Prefix migration `SR_` -> `US_`, `[SqueakyRatkin]` -> `[UniversalSqueaker]`, `usdiag` protocol v1/v2 re-frozen.
- [x] US data surface: `UniversalSqueakerFallbackProfileDef` (no shipped seed Defs), `1.6/Languages/*/Keyed/UniversalSqueaker.xml`, no race-specific patch.
- [x] Gates green: Dev/Release builds, kernel tests, config-copy tests, log tests.

### Phase 3 - minimal UI + componentization

- [x] Evaluation doc `docs/ui-componentization-evaluation-zh.md`: IMGUI reality, options A/B/C, reactive view-model + declarative immediate-mode components recommended.
- [x] Componentized minimal UI implemented (`UI/Model`, `UI/Components`, `VoicePacksPage`); write bridges unchanged; Scribe schema unchanged.
- [x] Crash-safety matrix and race-generic acceptance checklist documented in `docs/ui-phase3-implementation-notes-zh.md` (in-game execution remains a maintainer step).

### Phase 4 - cleanup and memory closeout

- [x] Final gates green (kernel tests + both build flavors + config-copy/log tools).
- [x] Atomic cleanup commit: `Kernel/`, `Pure/`, `fixtures/`, `sr_reference/`, `tools/KernelCharacterization/` deleted; `.gitattributes`, `README.md`, docs reference tree, `HANDOFF.md`, `MEMORY.md` updated.
- [x] `OBLIVIONIS.md`: cold-archive entry pointing to baseline `dc8c598` (snapshots remain in git history).
- [x] Privacy review of the complete reachable tree.

## Done (pre-rebuild migration)

- [x] Migrate Kernel/Pure/harness/fixtures and the memory agreement from SR `0.3.x` `b19d68a`; local git only.
- [x] Adapt harness links to local Kernel+Pure; SR content snapshot under `sr_reference/` (deleted in Phase 4).
- [x] Add `docs/mod-structure-reference-zh.md`; scaffold About/LoadFolders/1.6/Source/scripts/.github.
- [x] Migrate the SR release flow into `docs/release-runbook-zh.md`; write `HANDOFF.md`.

## Next development plan — UI migration + orphan features (decided 2026-08-24)

Full plan with decision table, orphan inventory, and implementation order: `docs/us-ui-migration-plan-zh.md`.

Key decisions locked:
- Mode set is `Vanilla / Fallback / Remix / Disabled` (Off renamed to Vanilla; `Disabled` = true bypass, sampling-layer short-circuit, no `ThingDef.comps` mutation; only one `usdiag` notice while disabled, no repeated trigger logging).
- Tuning editor (behavior + mood merged): author baseline = standalone read-only XML Def (NOT `CompProperties_Squeaker`, NOT `SqueakVoicePackDef`), player overrides = layered Global→Race→Xenotype table (Plan A, single record type replacing `globalActionEnabled` + `xenotypePresets.actionOverrides`), import/export = player delta feedback.
- Action scope: one layered table, `Xenotype > Race > Global > DefaultScope`, supports "global off + xenotype individually on"; merged with the action-gate design.
- Action entry wrapper (new): `ActionEntry` + `TriggerBinding` replace hardcoded `Notify_*`+Harmony patches; faithful routing of the original RimWorld path underneath; `Disabled` bypass = wrapper short-circuits and lets vanilla sound. `Sustained` is currently a dead mode; enable via the `Sustainer` binding kind in S3. Three-segment architecture (从哪来 TriggerBinding → 我是谁 PawnIdentity/ResolveContext → 到哪去 Registry.Select) with neutral-kernel vs external-adapter split locked in plan doc §13.4–13.5.
- Implementation order: S1 global-layer removal (distance/interval/talking-scaling to US settings) → S2 action-entry wrapper + action-gate → S3 Sustainer path → S4 tuning editor + layered override + feedback + UI face → S5 cleanup.
- Easter-egg toggle in main settings alongside mode cards, default off.
- Delete `experimentalRaceAllowlist`, `voicePackDefaultSeeded` (evaluate), `<description>`+`Name` localization patch; keep+restore `localizeDebugActions` (re-add `Patch_DebugTabMenu_Actions` + `US_` keys).
- Filtering (author/race/conflict + dropdown quick-filter), componentized help (widget-attached help content), narrow responsive (Race page + dedicated settings page), visual modernization (no vanilla black/gray boxes, RimWorld-style modern UI), build identity in footer — see doc §10.
- Developer page replaced by DebugAction panel.


### Progress checkpoint (2026-08-25) — implemented & gated green

Executed in this session, all passing `scripts/verify-local.ps1` (12 gates) + Dev/Release builds (0 warnings):

- **S0** — 5 design docs landed: `docs/us-s0-scribe-migration-zh.md`, `docs/us-s0-data-model-zh.md`, `docs/us-s0-key-and-race-context-zh.md`, `docs/us-s0-log-protocol-zh.md`, `docs/us-action-wrapper-interface-zh.md`.
- **S1** — global-layer removal: `globalMinIntervalTicks`/`scaleFrequencyWithTalking`/`distancePresets` removed from the author comp face; new `UniversalSqueakerSettings.globalMinIntervalTicks=216` + Scribe + runtime publish; skill template cleaned.
- **S2a** — enum `Off→Vanilla` + new `Disabled(3)`; UI mode cards now 4 (Vanilla/Fallback/Remix/Disabled); `Disabled` true-bypass short-circuit in `CompTick`/`NotifyExternal`; new v2 log event `audio.disabled` + LogTests (registry 8→9).
- **S2b (semantics)** — H1 fix: action scope now field-level last-wins `X > R > G > Default` (覆盖) instead of logical AND; removed the global early-return in `TryTrigger`/`IsScopeEligible`. New `ActionTuningRecord` layered table + `actionTuning` settings field + migration `TryCreateActionTuningRecords` + consumption in `BuildGlobalActions`/`BuildBehavior`. H3: Race-layer contexts + `ResolvedSqueakContext.Overlay` + three-tier `ResolveContext`.
- **S5** — deleted `SqueakGlobalActionPolicy.cs`, `experimentalRaceAllowlist`, `Patch_ModMetaData_LocalizedMetadata.cs`, dead `EnsureBuiltInRaceDefault`, `developerToolsEnabled`; restored `Patch_DebugTabMenu_Actions` + `localizeDebugActions` wiring.
- **Goal A (wrapper 化 H4, COMPLETE)** — `ActionEntry`/`TriggerBinding`/`ActionEntryRegistry` types + `Binding` property; `allowExternalActions` gate (settings + Scribe + `IsActionAllowedByKey` + registry publish); `CompSqueaker` 5 fixed arrays → `Dictionary<string,...>`; enum-keyed consumption → `ActionKey` string throughout (resolver + trigger chain + `SoundCacheMixed`); external-action end-to-end firing via `NotifyExternalByKey`; behavior-equivalence via `VerseEventBindingContract` test + golden-corpus replay zero-delta. Patch shape: 8 patches stay thin wrappers; Origin hardcoding removed via `VerseEventBinding` static aggregated binding.

### Remaining (next goal — hard debt first, per HANDOFF.md §4)

- **清理：双源统一** — S4 UI 已让 `actionTuning` 可写（`Settings.SetActionTuningScope`），现可删旧链路 `globalActionEnabled`/`GlobalActionEnabledRecord`/`GetActionGlobalScope`/`SetActionGlobalScope`（含 Scribe 迁移；`BuildGlobalActions` 当前初值仍读 `GetActionGlobalScope`，删除时需改回 baseline Def + actionTuning 双层）。
- **清理：`voicePackDefaultSeeded`** — Scribe 兼容哨兵，评估后删。
- **清理：`dist/` 残留** — 7 个测试包残留旧 comp 字段 XML（`globalMinIntervalTicks`/`scaleFrequencyWithTalking`/`distancePresets`），误导作者。
- **专项测试缺口** — "global off + xenotype on" 覆盖语义需抽 Kernel/Pure 或 Runtime harness 才能进 `UniversalSqueakerKernelTests`。
- **S4-Polish（纯视觉，最后）** — 过滤（作者/种族/冲突）、组件化帮助、窄屏响应式、美化（先出 `docs/ui-visual-modernization-zh.md` 评估稿）、footer build identity、距离预览折线图、A1/A2 调音编辑器 UI（依赖 baseline Def 已就绪）、Race/Xenotype 层 scope UI（当前 A6 只做 Global 层）。

### 已完成（2026-08-25b 会话，8 commit，verify-local 全绿）

- **S3 Sustainer + 外部动作端到端**（`f492e6c`）、**S4-Tuning-Backend**（`ace23a3`）、**S4-Orphan-Sync**（`f6a7ca9`）、**S4-Scope-Tree**（`f580ec0`）、**PoolStableKey 地基**（`84c7a64`）、**S4-Diag-Foundation**（`70329d5`）、**S4-Diag-Panel**（`801792e`）。详见 HANDOFF.md §3 与 MEMORY checkpoint。


## Next session — resume here (2026-08-24 checkpoint)

- [ ] Install `dist/dev/UniversalSqueaker-dev-v0.1.0-dev-bf9cf26.zip` (or the staged `dist/dev/UniversalSqueaker` folder) over the game Mods copy.
- [ ] Install/refresh test packs from `dist/`: `Kiiro-US-EXP`, `Nivarian-US-EXP` (new packageIds must be re-enabled in the mod list).
- [ ] Re-test and read `Player.log`:
  - Ratkin/Kiiro_Race/NivarianRace_Pawn: expect `audio.route.selected` dispatches;
  - settings UI: opens and renders pack rows.
- [ ] Send the new `Player.log` back for review before any further code changes.
- [ ] After log confirmation: decide whether to also auto-attach comp for canonical packs (currently canonical packs must carry their own comp patch), then final release-prep items below.

## UI shared library (accepted 2026-08-24) — FerriteLib UiKit

Spec + development outline: `docs/ui-shared-library-design-zh.md` (packageId `coahuilite.ferritelib.uikit`; C# `FerriteLib.UiKit`; neutral core).

- [x] Maintainer chose final name: `coahuilite.ferritelib.uikit` / `FerriteLib.UiKit`.
- [x] Create `Source/FerriteLib.UiKit/` project + core interfaces (`IWidget`, `WidgetRegistry`, `WidgetContext`, `UiCommand`, `ITextMetrics`, `UiPageState`).
- [x] Implement `LayoutManifest` + `LayoutEngine` two-pass Measure/Draw.
- [x] Migrate baseline components (`StatusBanner`, `Footer`, `ModeCard`) into `core` widgets.
- [x] Add `tools/FerriteLib.UiKit.Tests/` + verify-local gates (both DLLs present, neutrality grep, registry/manifest consistency).
- [x] Wire US settings page to the new engine (old UI may coexist briefly) via `VoicePacksPage.UseFerriteUi` (default false) + embedded `UI/Layout.xml`.
- [ ] Stabilize the Ferrite UI path in-game (player/maintainer step; `UseFerriteUi` is now `true` by default).
- [ ] After stabilization: split into private dependency mod `coahuilite.ferritelib.uikit` (phase B).

## Old UI retirement (evaluated 2026-08-24)

Minimum visible feature set documented in `docs/ui-audio-pack-management-minimum-zh.md`.

- [x] Evaluate minimum visible features for audio pack management.
- [x] Ferrite path: add Xenotype domain list entry (reuse `VoicePacksViewState.XenotypeDomains`; emit `SelectDomain` Xenotype).
- [ ] In-game matrix incl. Race + Xenotype domain switch (extend `docs/ui-phase3-implementation-notes-zh.md` §4).
- [x] Flip `VoicePacksPage.UseFerriteUi` default to `true`; old branch retained as fallback (in-game matrix confirmation still required before deleting old UI).
- [ ] After stable release: delete old UI branch and obsolete legacy components; update docs/TODO.

## Logging MVP (2026-08-24)

- [x] Per-dispatch dev log: one `audio.route.selected` per successful dispatch (no 5s rate limit).
- [x] Vanilla fallback dispatch: new warning event `audio.dispatch.vanilla_fallback` (yellow).
- [x] Keep pack info key-only in log MVP (`pack=<packageId:defName>`; no label/author metadata yet).
- [ ] Future: simplify SR-style assembled route line for human reading (current `usdiag` suffix is machine-heavy).
- [ ] Future: optional pack label/scope/mod metadata in route logs (not in MVP).
- [ ] Reserve mood/faction/controlled fields for later diagnostics.

## Pending decisions / follow-ups

- [x] Legacy SR compatibility bridge dropped (maintainer decision): old SR VoicePack XML is no longer auto-loaded; authors must migrate to the canonical `US_` form.
- [x] VoicePack authoring skill migrated from SR to `.github/skills/us-voicepack-authoring/SKILL.md`; canonical `US_` authoring only.
- [ ] Workshop display name and license (maintainer only; do not invent).
- [ ] SR-side full history/tag privacy cleanup (decided and executed on the SR side only).
- [ ] scripts/CI migration: dev scripts migrated (`scripts/verify-local.ps1`/`build-dev.ps1`/`pack-dev.ps1`/`stage-package.ps1`); GitHub/Steam build-pack scripts and CI workflows remain deferred until first release prep.
- [ ] HAR reflection discovery generalization (currently a catalog-side TODO; assembled-only).
- [ ] In-game crash/assignment matrix per `docs/ui-phase3-implementation-notes-zh.md` (maintainer step).
- [ ] First release prep: About icon/preview, final description, runbook US copy adaptation.
- [ ] Before any first push: maintainer decides how to handle the reachable-history personal absolute path (pre-fix `MEMORY.md` line in commits `eb2ac90..dc8c598`); no remote exists so there is no external exposure today.
