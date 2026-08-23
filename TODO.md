# TODO

## Current phase (local fork)

- [x] Migrate Kernel/Pure/harness/fixtures and the memory agreement from SR `0.3.x` `b19d68a` into this local repository; `git init` and local commits only.
- [x] Adapt the harness links to local `Kernel/`+`Pure/`; keep the SR content snapshot under `sr_reference/`.
- [x] Add `docs/mod-structure-reference-zh.md` and scaffold `About/`, `LoadFolders.xml`, `1.6/`, `Source/UniversalSqueaker/`, `scripts/`, `.github/workflows/`.
- [x] Migrate the SR release flow into `docs/release-runbook-zh.md`.
- [x] Write `HANDOFF.md` with the minimal-UI adaptation evaluation (VoicePack assignment works; other UI removable without crashes; race-generic, no Ratkin limits).
- [x] Maintain the three memory files (`AGENTS.md`, `MEMORY.md`, `TODO.md`) in accurate English.

## Phase 1 — De-SR-ize the kernel (US-general state)

- [ ] Migrate namespaces: `SqueakyRatkin.Kernel` / `SqueakyRatkin` → `UniversalSqueaker.Kernel` / `UniversalSqueaker`; update the harness and all references.
- [ ] Remove product literals from the kernel: move the Ratkin seed and `SR_*` sound keys out of `BuiltInFallbackTable` (data-injected or empty-table startup); replace `ActionAudioKeyMirror` and the five-way sync with a US data surface or freeze them as temporary migration guards.
- [ ] Rebuild the harness corpus baseline after removing the SR reference; keep zero-delta replay and add an explicit two-race equal-routing scenario (no Ratkin-specific assertions).
- [ ] Create the US assembly skeleton (csproj/About/packageId) — buildable locally only, not published.

## Phase 2 — Generalize runtime and assembly

- [ ] Delete the `SqueakProductDomainFilter` equivalent: catalog/resolver must not whitelist `{Ratkin}`; domains are data-driven from each pack's `raceDefName`, and every race is assemblable by default.
- [ ] Generalize race discovery: Race layer enumerates from VoicePack/fallback-profile data, with no HAR Ratkin special-casing; Xenotype layer keeps Biotech gating and exact `targetDefName` matching.
- [ ] Generalize the built-in fallback-profile mechanism: no product seed; profiles come from data files/content packs.
- [ ] Migrate logging and key prefixes: `SR_` → `US_`, `[SqueakyRatkin]` → `[UniversalSqueaker]` (`usdiag` protocol prefix); re-freeze the v1/v2 protocol for US.

## Phase 3 — Minimal-UI adaptation (execute HANDOFF Plan A)

- [ ] Keep: settings shell, Off/Fallback/Remix mode cards, Race layer, and Race/Xenotype VoicePack checkbox paths (`SetVoicePackSelection` write bridges).
- [ ] Remove/degrade: SoundMood workbench, Diagnostics page, audio browser, statistics/overlay/mote diagnostics, camera indicator, DebugActions, and the Xenotype behavior editor; delete the corresponding diagnostics patches only.
- [ ] Crash-safety matrix: settings page must open and close cleanly with no candidate packs, no Biotech, no HAR, no selected pawn, and corrupted settings.
- [ ] Race-generic acceptance: at least two races each with a VoicePack; packs route only within their declared race; the Race list contains no Ratkin special-casing.

## Pending decisions

- [ ] Workshop display name and license; US starting version number (suggested local `0.1.0-dev`, unified to `0.4.0` for the 0.4 dual release).
- [ ] Whether to perform full history/tag privacy cleanup in SR (decided on the SR side only; not executed here).
