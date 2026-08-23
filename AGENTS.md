# AGENTS.md — Universal Squeaker

> This file is the memory agreement for AI agents working in this repository. Human contributors should read `README.md` and `HANDOFF.md`.

## Project identity

- Project: RimWorld 1.6 mod **Universal Squeaker** (local fork; no remote configured).
- Confirmed identity: repo `coahuilite/UniversalSqueaker`; permanent `packageId` `coahuilite.universalsqueaker`; target C# namespace `UniversalSqueaker`; Def/log/debug-key prefix `US_`; diagnostic log prefix `usdiag`. Workshop display name and license remain pending maintainer confirmation; this repository is not published.
- Squeaky Ratkin (`coahuilite.squeakyratkin`) is a separate product. Never reuse its brand, packageId, namespace, or `SR_` prefix here. During the 0.4 co-existence window US must not define any `SqueakyRatkin.*` types and must not ship Ratkin assemblies, profiles, or attachments.
- Product version source: once the product csproj exists, its `<Version>` is primary and `About/About.xml <modVersion>` must follow it.

## Project philosophy

- Neutral routing: a VoicePack's `raceDefName` declaration is the only routing entry. There is no built-in race special-casing; HAR races, vanilla Human, and any third-party race route identically.
- Optional dependencies are reflective only: HAR is touched via reflection for discovery; missing dependencies degrade silently and never crash.
- Uninstall safety is a hard rule: removing the mod must never affect a saved game. No permanent data is written into saves; settings and profiles live in the Config folder.
- Content and kernel are separate: the Kernel compile set must not reference Verse/Unity/RimWorld and must not contain product sound keys or race seeds. `SR_*`/Ratkin seed data belongs to SR content packs; US provides data-driven fallback profiles.

## Memory protocol

- Read `MEMORY.md` before claiming project context and `TODO.md` before continuing work.
- The three memory files (`AGENTS.md`, `MEMORY.md`, `TODO.md`) are the memory agreement and are maintained in accurate English.
- `MEMORY.md` keeps durable facts and open-action pointers only; `TODO.md` keeps current goals, open actions, blockers, and explicit deferrals. Settled details live under `docs/` (`HANDOFF.md`, references, runbook).
- This is a local fork of SR. SR state must be sourced from the SR repository and never inferred from this repository.

## Privacy and security

- Default scope is this repository root. Reading outside it requires path-specific authorization and remains read-only.
- Never place personal local paths, diagnostic-log excerpts, credentials, API keys, tokens, private keys, or `PublishedFileId.txt` values in Git, documentation, generated artifacts, staging, or reachable history.
- Every push is preceded by a privacy review of the complete reachable range. This repository currently has no remote; configuring a remote or pushing is an external operation that requires explicit maintainer authorization.
- Destructive history/tag rewrites of the SR repository are out of scope here and must be handled on the SR side.

## External-state boundaries

- Local commits are permitted. `git remote`, push, PR, tag, release, and all publication actions require explicit maintainer authorization.
- Never claim that this repository's evidence proves SR, GitHub, or Workshop external state.
