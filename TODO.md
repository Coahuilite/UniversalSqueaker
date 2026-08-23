# TODO

## Rebuild plan (approved 2026-08-23, active)

Decisions D1-D7 live in `MEMORY.md`. Subagents edit only their assigned file sets and never run git commit; the parent session integrates, verifies, and commits.

### Phase 0 - memory / privacy / baseline

- [x] Empty baseline commit `dc8c598` recorded as the OBLIVIONIS archive pointer candidate.
- [x] Replace the personal absolute SR path in `MEMORY.md` with repo-relative `../squeaky_ratkin`.
- [x] Record D1-D7 rebuild decisions in `MEMORY.md`.
- [x] Rewrite this TODO to the rebuild action surface.

### Phase 1 - de-SR-ize kernel/pure + US test gate (batch 1-A)

- [x] Rebuild `Source/UniversalSqueaker/Kernel/*.cs` (9 files): namespace `UniversalSqueaker.Kernel`; remove Ratkin seed, `SR_*` keys, and `DomainFilter` whitelist semantics.
- [x] Rebuild `Source/UniversalSqueaker/Pure/*.cs` (2 files): namespace `UniversalSqueaker`; math byte-equivalent, zero Verse.
- [x] Create `Source/UniversalSqueaker/UniversalSqueaker.csproj`: net472, `<Version>0.1.0-dev` (VersionPrefix 0.1.0 + VersionSuffix dev), TreatWarningsAsErrors, Dev/Release flavors.
- [x] Create `tools/UniversalSqueakerKernelTests/` linking the new Kernel+Pure; scenarios use RaceA/RaceB equal routing, neutral test sound keys, three-way sync, new US 0.1.0 corpus.
- [x] Gate: `dotnet run --project tools/UniversalSqueakerKernelTests -c Release` green; new code free of `SqueakyRatkin`/`Ratkin`/`SR_`.

### Phase 2 - runtime/assembly generalization

- [x] Rebuild runtime sources under `Source/UniversalSqueaker/` from the SR reference: catalog/resolver/settings/logging/comp/production patches; delete product-domain-filter equivalents and HAR Ratkin special-casing.
- [x] Prefix migration `SR_` -> `US_`, `[SqueakyRatkin]` -> `[UniversalSqueaker]`, re-freeze `usdiag` protocol v1/v2 for US.
- [x] US data surface: data-driven `UniversalSqueakerFallbackProfileDef` (no shipped seed Defs), `1.6/Languages/*/Keyed/UniversalSqueaker.xml`, production patches only (no race-specific patch).
- [x] Gate: Dev and Release builds green; Source + 1.6 free of SR literals; three tool gates green.

### Phase 3 - minimal UI + componentization

- [x] Evaluation doc `docs/ui-componentization-evaluation-zh.md`: IMGUI reality, options A/B/C, recommend reactive view-model + declarative immediate-mode components (batch 1-B).
- [ ] Implement Plan A minimal UI with `UI/Model`, `UI/Components`, `VoicePacksPage`; write bridges unchanged; Scribe schema unchanged.
- [ ] Crash-safety matrix and race-generic acceptance checklist.

### Phase 4 - cleanup and memory closeout

- [ ] Final gates green (kernel tests + both build flavors).
- [ ] One atomic cleanup commit: delete `Kernel/`, `Pure/`, `fixtures/`, `sr_reference/`, `tools/KernelCharacterization/`; update `.gitattributes`, `README.md`, docs reference tree, `HANDOFF.md`, `MEMORY.md`.
- [ ] `OBLIVIONIS.md`: cold-archive entry pointing to baseline commit (snapshots remain in git history).
- [ ] Privacy review of the complete reachable tree.

## Done (pre-rebuild migration)

- [x] Migrate Kernel/Pure/harness/fixtures and the memory agreement from SR `0.3.x` `b19d68a`; local git only.
- [x] Adapt harness links to local Kernel+Pure; SR content snapshot under `sr_reference/`.
- [x] Add `docs/mod-structure-reference-zh.md`; scaffold About/LoadFolders/1.6/Source/scripts/.github.
- [x] Migrate the SR release flow into `docs/release-runbook-zh.md`; write `HANDOFF.md`.

## Pending decisions

- [ ] Workshop display name and license (maintainer only; do not invent).
- [ ] SR-side full history/tag privacy cleanup (decided and executed on the SR side only).
- [ ] scripts/CI migration and release copy adaptation (deferred until after the rebuild).
- [ ] HAR reflection discovery generalization (currently assembled-only TODO in catalog).
