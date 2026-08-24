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

## Next session — resume here (2026-08-24 checkpoint)

- [ ] Install `dist/dev/UniversalSqueaker-dev-v0.1.0-dev-bf9cf26.zip` (or the staged `dist/dev/UniversalSqueaker` folder) over the game Mods copy.
- [ ] Install/refresh test packs from `dist/`: `Kiiro-US-EXP`, `SqueakyRatkinExampleVoices`, `SqueakyRatkinLegacyVoices`, `Nivarian-US-EXP` (new packageIds must be re-enabled in the mod list).
- [ ] Re-test and read `Player.log`:
  - legacy pack: expect ONE `voicepack.pack.legacy_admitted` and ONE `voicepack.comp.legacy_auto_attached`, NO `duplicate_key`;
  - Ratkin/Kiiro_Race/NivarianRace_Pawn: expect `audio.route.selected` dispatches;
  - settings UI: opens, legacy rows show `Legacy SR` tag + banner count.
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

- [x] Legacy bridge activated (maintainer authorization 2026-08-24): thin `SqueakyRatkin.SqueakVoicePackDef` shim + catalog upcast + SR_/US_ prefix context; old SR packs are explicitly marked in logs (`usdiag voicepack.pack.legacy_admitted`) and UI (`Legacy SR` row tag + banner); missing `raceDefName` defaults to `Ratkin` for legacy packs; bridge auto-attaches the default comp to legacy pack races when absent (`usdiag voicepack.comp.legacy_auto_attached`).
- [x] VoicePack authoring skill migrated from SR to `.github/skills/us-voicepack-authoring/SKILL.md`; canonical `US_` authoring only, with a legacy auto-compat section (old `SR_` packs need no author changes).
- [ ] Workshop display name and license (maintainer only; do not invent).
- [ ] SR-side full history/tag privacy cleanup (decided and executed on the SR side only).
- [ ] scripts/CI migration: dev scripts migrated (`scripts/verify-local.ps1`/`build-dev.ps1`/`pack-dev.ps1`/`stage-package.ps1`); GitHub/Steam build-pack scripts and CI workflows remain deferred until first release prep.
- [ ] HAR reflection discovery generalization (currently a catalog-side TODO; assembled-only).
- [ ] In-game crash/assignment matrix per `docs/ui-phase3-implementation-notes-zh.md` (maintainer step).
- [ ] First release prep: About icon/preview, final description, runbook US copy adaptation.
- [ ] Before any first push: maintainer decides how to handle the reachable-history personal absolute path (pre-fix `MEMORY.md` line in commits `eb2ac90..dc8c598`); no remote exists so there is no external exposure today.
