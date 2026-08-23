# MEMORY

## Current durable state

- This repository is the **Universal Squeaker (US)** local fork of Squeaky Ratkin (SR), created 2026-08-23. It has **no remote** and is **not published**.
- Fork source: SR branch `0.3.x` tip `b19d68a` (post-0.3.2-pre1, including the `.slim` cleanup commits C34–C42).
- Confirmed identity: repo `coahuilite/UniversalSqueaker`, packageId `coahuilite.universalsqueaker`, namespace/prefix/log = `UniversalSqueaker` / `US_` / `usdiag`. Workshop display name and license are pending maintainer confirmation.
- 0.4 co-existence policy (decided): SR owns Ratkin exclusively; US serves other races only; both mods may be enabled together; no `incompatibleWith`; US 0.4 ships no Ratkin assemblies/profiles/attachments.
- SR 1.0.0 will shrink SR into a pure audio pack with US as prerequisite. The legacy bridge offline prototype lives in the SR repository (`tools/LegacyBridgePrototype` / `tools/LegacyBridgeHarness`) and will be activated only in the US takeover version.

## Authoritative entries

- Fork handoff and UI adaptation evaluation: `HANDOFF.md` (single entry point).
- Mod structure reference: `docs/mod-structure-reference-zh.md` (RimWorld Wiki + SR-minus-Extras/audio baseline).
- Release flow: `docs/release-runbook-zh.md` (same process as SR).
- Kernel compile set (rebuilt, zero-Verse, de-SR-ized): `Source/UniversalSqueaker/Kernel/`. Legacy root `Kernel/` was deleted in the Phase 4 cleanup (git history retains it).
- Pure funnel logic (rebuilt, zero-Verse): `Source/UniversalSqueaker/Pure/SqueakActionPlan.cs`, `Source/UniversalSqueaker/Pure/SqueakTimingModel.cs`. Legacy root `Pure/` was deleted in the Phase 4 cleanup.
- Kernel harness (rebuilt): `tools/UniversalSqueakerKernelTests/` links `Source/UniversalSqueaker/Kernel/`+`Pure/`; US 0.1.0 golden corpus committed and replay-green. Legacy `tools/KernelCharacterization/`, `fixtures/`, and `sr_reference/` were deleted in the Phase 4 cleanup (see `OBLIVIONIS.md`).
- Runtime assembly (rebuilt): `Source/UniversalSqueaker/` builds Dev/Release clean (0 warnings). Catalog/resolver/settings/logging/comp/production patches all US-namespaced; `usdiag` protocol re-frozen; no `SqueakProductDomainFilter`, no HAR Ratkin special-casing (HAR discovery deferred TODO).
- Data surface: `UniversalSqueakerFallbackProfileDef` injects fallback profiles from DefDatabase (no shipped seed Defs; empty = valid); `1.6/Languages/*/Keyed/UniversalSqueaker.xml` committed.
- Tool gates: `tools/UniversalSqueakerConfigCopyTests/` and `tools/UniversalSqueakerLogTests/` both pass; they link Source kernel/fallback/logging files.
- SR upstream (read-only evidence source): sibling repository at `../squeaky_ratkin` relative to this repo root. Do not write there and do not infer its external state from this repo.

## Engineering decisions and handoff

- **Local fork only**: no remote is configured; local commits are the only permitted git operations until the maintainer authorizes remote/push. Reachable history still contains the pre-fix personal absolute SR path in `MEMORY.md` (commits `eb2ac90..dc8c598`); it is recorded in TODO and must be handled by the maintainer before any first push.
- **Migration inventory (2026-08-23, historical)**: `Kernel/*.cs` (9 files), `Pure/` (2 files), `tools/KernelCharacterization/*` plus `fixtures/` and `sr_reference/` were migrated, consumed as references during the rebuild, and deleted in Phase 4; they remain in git history (`dc8c598` baseline).
- **Inherited technical debt (from the SR snapshot) — resolved by the rebuild**: namespaces migrated to `UniversalSqueaker*`, Ratkin seed and `SR_*` keys removed, `DomainFilter`/`SqueakProductDomainFilter` deleted, five-way sync replaced by three-way sync plus tool gates.
- **Target baseline**: kernel with zero product literals (race/sound-key/prefix all injected as data); UI only needs VoicePack assignment to work, all other pages may be removed but must not crash; every race routes equally with no Ratkin special-casing.
- **UI adaptation decision (per HANDOFF.md)**: Plan A (vertical cut) is recommended — keep settings shell, Off/Fallback/Remix mode cards, Race layer, and VoicePack domain checkboxes; remove SoundMood workbench, Diagnostics, audio browser, statistics/overlay/mote diagnostics, and the Xenotype behavior editor without changing any Scribe schema.

## Rebuild plan decisions (approved 2026-08-23)

- D1 Version: csproj `<Version>0.1.0-dev` is primary; `About.xml <modVersion>` follows it.
- D2 Rebuilt pure code lives under `Source/UniversalSqueaker/Kernel/` (zero-Verse compile set) and `Source/UniversalSqueaker/Pure/` (funnel pure logic); the old root `Kernel/`+`Pure/` were reference-only and are now deleted.
- D3 SR 0.2.4 settings-migration fixtures are not replicated now: US is a new mod with no legacy config; the legacy bridge is deferred to the takeover version.
- D4 Verification project: `tools/UniversalSqueakerKernelTests/` with the US 0.1.0 golden corpus; old `tools/KernelCharacterization/` was reference-only and is now deleted.
- D5 Kernel product literals: `BuiltInFallbackCatalog` (Ratkin seed + `SR_*` keys) and `DomainFilter` whitelist semantics removed; `BuiltInFallbackTable.Empty` plus data injection remain.
- D5b Comp attach: VoicePacks attach `CompProperties_Squeaker` via their own XML patch for their declared race; US ships no race-specific patch.
- D6 UI: reactive view-model + declarative immediate-mode components over Verse widgets (evaluation: `docs/ui-componentization-evaluation-zh.md`; implementation notes: `docs/ui-phase3-implementation-notes-zh.md`).
- D7 Cleanup executed in an atomic commit: `Kernel/`, `Pure/`, `fixtures/`, `sr_reference/`, old `tools/KernelCharacterization/` deleted; `OBLIVIONIS.md` records the pre-rebuild baseline commit `dc8c598`.
