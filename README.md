# Universal Squeaker

**English** | [中文](./README.zh-CN.md)

A voice-pack routing kernel for RimWorld 1.6. This repository is the **Universal Squeaker local fork** of Squeaky Ratkin (MPL-2.0; the provenance notice is kept as the license requires for derivatives), preparing for its first cloud release.

Universal Squeaker ships **no audio of its own**: it is a routing kernel that attaches third-party voice packs to pawns by race/xenotype domain, with layered tuning (global/race/xenotype overrides), distance attenuation curves, mood modulation, easter-egg gating, and a camera indicator. Settings live in a dedicated window — five workspaces (Overview / Distance / Packs / Tuning / Presets) plus a hover-driven help panel — and the built-in `usdiag` protocol writes leveled diagnostics to the player log for troubleshooting.

## Requirements

- RimWorld 1.6
- **FerriteLib** (`coahuilite.ferritelib`) as a hard prerequisite: it carries the UI kernel `FerriteLib.UiKit.dll`. US compiles against it by relative path and never ships a copy; the API-range check lives in code (`FerriteLibVersion.Require`), not in `About.xml` (RimWorld's modDependencies cannot express versions).

## Identity

- packageId: `coahuilite.universalsqueaker`
- namespace / Def prefix / log prefix: `UniversalSqueaker` / `US_` / `usdiag`
- license: MPL-2.0 (Workshop display name pending maintainer confirmation)

## Local verification and build

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1   # 15 gates (6 harnesses + main assembly Dev/Release + carrier boundary and single-carrier red line + MPL-2.0 licence parity + Schema=2 manifests)
pwsh -NoProfile -File scripts/build-dev.ps1      # builds the ../ferritelib payload first, then a Dev build + dev package (dist/dev/)
pwsh -NoProfile -File scripts/privacy-audit.ps1  # privacy gate (three vectors + credential patterns + PublishedFileId values + identity uniqueness; -FullHistory for the full-history mode)
```

The UI library was split into its own prerequisite-mod repository `../ferritelib` (`coahuilite.ferritelib`), which runs its own gate suite.

## Documentation index

- Handoff and acceptance checklist: `HANDOFF.md` (maintainer-local, not published)
- Memory protocol / durable facts / action surface / cold archive: `AGENTS.md`, `MEMORY.md`, `TODO.md`, `OBLIVIONIS.md`
- Release flow (every release): `docs/release-runbook-zh.md`
- First cloud upload: executed 2026-09-06; durable decisions and push order live in `MEMORY.md` ("First cloud upload"), the PASS ledger in `TODO.md`.
- Mod structure reference: `docs/mod-structure-reference-zh.md`
- Voice-pack authoring guide: `.github/skills/us-voicepack-authoring/SKILL.md`

Historical material (describes the old UI implementation deleted on 2026-09-02): `docs/ui-componentization-evaluation-zh.md`, `docs/ui-phase3-implementation-notes-zh.md`.
