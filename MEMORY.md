# MEMORY

## Current durable state

- This repository is the **Universal Squeaker (US)** local fork of Squeaky Ratkin (SR), created 2026-08-23. It has **no remote** and is **not published**.
- Fork source: SR branch `0.3.x` tip `b19d68a` (post-0.3.2-pre1, including the `.slim` cleanup commits C34–C42).
- Confirmed identity: repo `coahuilite/UniversalSqueaker`, packageId `coahuilite.universalsqueaker`, namespace/prefix/log = `UniversalSqueaker` / `US_` / `usdiag`. Workshop display name and license are pending maintainer confirmation.
- 0.4 co-existence policy (decided): SR owns Ratkin exclusively; US serves other races only; both mods may be enabled together; no `incompatibleWith`. Maintainer-authorized legacy bridge exception (2026-08-24): US ships one thin empty `SqueakyRatkin.SqueakVoicePackDef` shim so old SR VoicePack XML loads; legacy packs are explicitly marked in logs (`usdiag voicepack.pack.legacy_admitted`) and UI (`Legacy SR` tag + banner). Legacy packs omitting `raceDefName` default to `Ratkin` (SR-dependency semantics; explicit declarations are kept). US still ships no Ratkin audio/content/profiles/attachments.
- SR 1.0.0 will shrink SR into a pure audio pack with US as prerequisite. The legacy bridge offline prototype lives in the SR repository (`tools/LegacyBridgePrototype` / `tools/LegacyBridgeHarness`) and will be activated only in the US takeover version.

## Authoritative entries

- Fork handoff and UI adaptation evaluation: `HANDOFF.md` (single entry point).
- Mod structure reference: `docs/mod-structure-reference-zh.md` (RimWorld Wiki + SR-minus-Extras/audio baseline).
- Release flow: `docs/release-runbook-zh.md` (inherited from SR and adapted for US: no Extras/audio/OGG mirror/codemap checks; `version.txt` first line is `UniversalSqueaker <version>`). Local scripts: `scripts/verify-local.ps1` (six gates), `scripts/build-dev.ps1`, `scripts/pack-dev.ps1`, `scripts/stage-package.ps1`. Dev packaging flow verified 2026-08-23 on commit `8c2f056`: six gates green, package content/exclusions/version.txt/DLL identity pass the runbook Phase 0 checks.
- Kernel compile set (rebuilt, zero-Verse, de-SR-ized): `Source/UniversalSqueaker/Kernel/`. Legacy root `Kernel/` was deleted in the Phase 4 cleanup (git history retains it).
- Pure funnel logic (rebuilt, zero-Verse): `Source/UniversalSqueaker/Pure/SqueakActionPlan.cs`, `Source/UniversalSqueaker/Pure/SqueakTimingModel.cs`. Legacy root `Pure/` was deleted in the Phase 4 cleanup.
- Kernel harness (rebuilt): `tools/UniversalSqueakerKernelTests/` links `Source/UniversalSqueaker/Kernel/`+`Pure/`; US 0.1.0 golden corpus committed and replay-green. Legacy `tools/KernelCharacterization/`, `fixtures/`, and `sr_reference/` were deleted in the Phase 4 cleanup (see `OBLIVIONIS.md`).
- Runtime assembly (rebuilt): `Source/UniversalSqueaker/` builds Dev/Release clean (0 warnings). Catalog/resolver/settings/logging/comp/production patches all US-namespaced; `usdiag` protocol re-frozen; no `SqueakProductDomainFilter`, no HAR Ratkin special-casing (HAR discovery deferred TODO).
- Data surface: `UniversalSqueakerFallbackProfileDef` injects fallback profiles from DefDatabase (no shipped seed Defs; empty = valid); `1.6/Languages/*/Keyed/UniversalSqueaker.xml` committed.
- Tool gates: `tools/UniversalSqueakerConfigCopyTests/` and `tools/UniversalSqueakerLogTests/` both pass; they link Source kernel/fallback/logging files.
- VoicePack authoring skill: `.github/skills/us-voicepack-authoring/SKILL.md` (migrated from SR; canonical `US_` authoring only — legacy `SR_` packs need no author action because the bridge auto-loads, defaults to Ratkin, auto-attaches the comp, and marks them `Legacy SR`).
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
- D5b Comp attach: VoicePacks may attach `CompProperties_Squeaker` via their own XML patch; the legacy bridge additionally auto-attaches a default comp to every race declared by an admitted legacy pack when none exists (Ratkin in practice), so old SR packs work with no pack-side patch.
- D6 UI: reactive view-model + declarative immediate-mode components over Verse widgets (evaluation: `docs/ui-componentization-evaluation-zh.md`; implementation notes: `docs/ui-phase3-implementation-notes-zh.md`).
- D7 Cleanup executed in an atomic commit: `Kernel/`, `Pure/`, `fixtures/`, `sr_reference/`, old `tools/KernelCharacterization/` deleted; `OBLIVIONIS.md` records the pre-rebuild baseline commit `dc8c598`.

## Session resume checkpoint (2026-08-24 — keep detailed, do not compress away)

- Human maintainer is mid-testing and may compress the session; this section is the resume anchor. Read it together with `TODO.md`'s "Next session" section before doing anything else.
- Latest commit chain (local only, no remote): `... 8c2f056 → 31fcf50 → 6bde39a → 8a65840 → b00b8bf → 5f3fd62 → bf9cf26 → 5ba8ce4`.
  - `6bde39a`: UI empty-catalog NRE fix.
  - `8a65840`: legacy bridge activation (thin `SqueakyRatkin.SqueakVoicePackDef`, SR_/US_ prefix contexts, `voicepack.pack.legacy_admitted`, UI `Legacy SR` tag + banner).
  - `b00b8bf`: legacy auto-attach default `CompProperties_Squeaker` to races declared by legacy packs (`voicepack.comp.legacy_auto_attached` / `legacy_auto_attach_failed`).
  - `5f3fd62`: legacy packs with missing `raceDefName` default to `Ratkin`.
  - `bf9cf26`: de-duplicate pack enumeration (fixes in-game `duplicate_key count=2` for the legacy test pack) + migrate authoring skill.
  - `5ba8ce4`: skill simplified to canonical-only authoring with legacy auto-compat note.
- In-game evidence (latest `Player.log`, build `b00b8bf`, before `bf9cf26` dedup fix):
  - Canonical routing works: Ratkin (`US_ExampleTemplate_Race`), `Kiiro_Race` (`US_MeowingKiiroExp`), `NivarianRace_Pawn` (`US_NivarianExp`) all dispatched successfully; one summary line showed `dispatched=65`.
  - Legacy test pack (`coahuilite.squeakyratkin.legacyvoices:SR_ExampleTemplate_Race`) was admitted with race Ratkin, then enumerated twice and rejected with `duplicate_key count=2`. Root cause fixed in `bf9cf26`; retest pending.
  - Non-fatal warnings: local EXP packs lack `<downloadUrl>`/`<steamWorkshopUrl>` in About; harmless, clean up before any release.
- Latest dev package: `dist/dev/UniversalSqueaker-dev-v0.1.0-dev-bf9cf26.zip` (six gates green; contains everything up to `bf9cf26`). The skill/doc changes in `5ba8ce4` are under `.github/` and do not enter the mod package.
- Test pack inventory under `dist/` (gitignored, NOT committed):
  - `Kiiro-US-EXP/` — canonical, `raceDefName=Kiiro_Race`, packageId `coahuilite.squeakyratkin.meowingkiiroexp` (kept for ModsConfig continuity), comp patch included.
  - `SqueakyRatkinExampleVoices/` — canonical US-converted, `raceDefName=Ratkin`, packageId kept `coahuilite.squeakyratkin.examplevoices`, comp patch included.
  - `SqueakyRatkinLegacyVoices/` — legacy test pack, packageId `coahuilite.squeakyratkin.legacyvoices`, keeps `SqueakyRatkin.SqueakVoicePackDef` + `SR_`; `<raceDefName>` deliberately removed to exercise the Ratkin default; has its own comp patch (auto-attach will skip it).
  - `Nivarian-US-EXP/` — canonical, `raceDefName=NivarianRace_Pawn`, packageId `coahuilite.nivarian-us-exp`, comp patch included.
- Installation note for resume: copy `dist/dev/UniversalSqueaker` over the game's Mods `UniversalSqueaker` folder, then copy each desired pack folder from `dist/` into the game Mods folder. Do NOT enable the original SR mod together with the US legacy bridge (first-wins type conflict for `SqueakyRatkin.SqueakVoicePackDef`).
- Expected log after retest with `bf9cf26`: exactly one `voicepack.pack.legacy_admitted` and one `voicepack.comp.legacy_auto_attached` per legacy race, zero `voicepack.pack.rejected`.
- Do not treat `dist/` as source of truth for anything; it is ignored build/test output. All product decisions live in `AGENTS.md`/`MEMORY.md`/`TODO.md` and the Source tree.
