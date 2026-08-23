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
- Kernel compile set: `Kernel/` (zero-Verse snapshot; not yet de-SR-ized).
- Pure funnel logic: `Pure/SqueakActionPlan.cs`, `Pure/SqueakTimingModel.cs`.
- Kernel harness: `tools/KernelCharacterization/` (linked to local `Kernel/`+`Pure/`; fixtures and SR reference snapshot migrated; passes locally).
- SR upstream (read-only evidence source): sibling repository at `../squeaky_ratkin` relative to this repo root. Do not write there and do not infer its external state from this repo.

## Engineering decisions and handoff

- **Local fork only**: no remote is configured; local commits are the only permitted git operations until the maintainer authorizes remote/push.
- **Migration inventory (2026-08-23)**: `Kernel/*.cs` (9 files), `Pure/` (2 files), `tools/KernelCharacterization/*` (7 files) plus `fixtures/` and `sr_reference/`, memory agreement (AGENTS/MEMORY/TODO/HANDOFF/README), mod skeleton (`About/`, `LoadFolders.xml`, `1.6/`, `Source/UniversalSqueaker/`, `scripts/`, `.github/workflows/`), and `docs/` references/runbook.
- **Inherited technical debt (from the SR snapshot)**: Kernel/Pure namespaces are still `SqueakyRatkin*`; `BuiltInFallbackTable` contains the Ratkin seed and `SR_*` sound keys; the harness `ActionAudioKeyMirror` and five-way sync still reference SR content under `sr_reference/`. These must be removed in the first US generalization phase.
- **Target baseline**: kernel with zero product literals (race/sound-key/prefix all injected as data); UI only needs VoicePack assignment to work, all other pages may be removed but must not crash; every race routes equally with no Ratkin special-casing.
- **UI adaptation decision (per HANDOFF.md)**: Plan A (vertical cut) is recommended — keep settings shell, Off/Fallback/Remix mode cards, Race layer, and VoicePack domain checkboxes; remove SoundMood workbench, Diagnostics, audio browser, statistics/overlay/mote diagnostics, and the Xenotype behavior editor without changing any Scribe schema.

## Rebuild plan decisions (approved 2026-08-23)

- D1 Version: csproj `<Version>0.1.0-dev` is primary; `About.xml <modVersion>` follows it.
- D2 Rebuilt pure code lives under `Source/UniversalSqueaker/Kernel/` (zero-Verse compile set) and `Source/UniversalSqueaker/Pure/` (funnel pure logic); the old root `Kernel/`+`Pure/` are reference-only and will be deleted.
- D3 SR 0.2.4 settings-migration fixtures are not replicated now: US is a new mod with no legacy config; the legacy bridge is deferred to the takeover version.
- D4 New verification project: `tools/UniversalSqueakerKernelTests/` with a new US 0.1.0 golden corpus; old `tools/KernelCharacterization/` is reference-only.
- D5 Kernel product literals: remove `BuiltInFallbackCatalog` (Ratkin seed + `SR_*` keys) and `DomainFilter` whitelist semantics; keep `BuiltInFallbackTable.Empty` plus data injection.
- D5b Comp attach: VoicePacks attach `CompProperties_Squeaker` via their own XML patch for their declared race; US ships no race-specific patch.
- D6 UI: reactive view-model + declarative immediate-mode components over Verse widgets (evaluation: `docs/ui-componentization-evaluation-zh.md`).
- D7 Cleanup: after new gates are green, one atomic commit deletes `Kernel/`, `Pure/`, `fixtures/`, `sr_reference/`, old `tools/KernelCharacterization/`; `OBLIVIONIS.md` records the pre-rebuild baseline commit SHA. Pre-rebuild baseline commit: `dc8c598`.
