# AGENTS.md — Universal Squeaker

> This file contains repository-specific working instructions. Human contributors should read `README.md`.

## Project identity

- Project: RimWorld 1.6 mod **Universal Squeaker** (local fork; remote `Coahuilite/UniversalSqueaker` created private and pushed 2026-09-06; publication actions still maintainer-gated per session).
- Confirmed identity: repo `coahuilite/UniversalSqueaker`; permanent `packageId` `coahuilite.universalsqueaker`; target C# namespace `UniversalSqueaker`; Def/log/debug-key prefix `US_`; diagnostic log prefix `usdiag`. License is **MPL-2.0** for the whole Coahuilite mod series (`LICENSE`, byte-identical per repo, no "Incompatible With Secondary Licenses" notice). Workshop display name remains pending maintainer confirmation; the GitHub repo is **public since 2026-09-07** and the first release `v0.2.0-rc1` (prerelease) is cut — current publication state lives in `MEMORY.md`.
- Squeaky Ratkin (`coahuilite.squeakyratkin`) is a separate product. Never reuse its brand, packageId, namespace, or `SR_` prefix here. No `SqueakyRatkin.*` types or SR-owned assemblies, profiles, attachments or audio assets may ship with US. Neutral US fallback profile data may reference Core audio for any supported race.
- Product version source: once the product csproj exists, its `<Version>` is primary and `About/About.xml <modVersion>` must follow it.

Current product contracts start at `docs/specification.md`; source/status mappings are under `docs/development/specification/`. Replaced full texts are retained in `docs/archive/specs-before-20261029/`.

## Project philosophy

- Neutral routing: a VoicePack declaration OR a per-race final fallback table admits a race. With neither, the race remains silent and receives no automatic comp; author-supplied comps are preserved. New table-only comp admission requires a full game restart; edits for already admitted races refresh the resolver. No built-in race special-casing: HAR races, vanilla Human and third-party races follow the same rule.
- Optional dependencies degrade without crashing unrelated capabilities. HAR reflective discovery remains deferred (`TODO(HAR)`); its hint lists stay empty. Canonical Xenotype identities come from all loaded native defs independently of audio-pack targets; `GameRuntime.ResolveContext` still requires Biotech, object identity/unambiguous names and an exact domain for the current Race.
- Uninstall safety is a hard rule: removing the mod must never affect a saved game. No permanent data is written into saves; settings and profiles live in the Config folder.
- Content and core are separate: the complete `Core/**/*.cs` production compile set must not reference Verse/Unity/RimWorld, platform doubles, native objects or mutable process-global state. Canonical contract IDs belong there; audio algorithms contain no product sound keys or race seeds. `SR_*`/Ratkin seed data belongs to SR content packs; US provides data-driven fallback profiles. Game observation, resources and lifecycle belong to the native adapters.

## Keep authority and consumers connected

- Production callers must use the existing owner for state, eligibility, commitment/publication and lifetime results. Legal backends/adapters, registry composition, independent author roots and complete synchronous fallback remain supported; they are not competing owners.
- Storage or a view update is not runtime adoption. Close the actual publisher-to-consumer path; distinguish physical commitment from publication failure and identity facts from audio supply/browse projections. Ordinary pack selection remains configuration filtering, not audio reconstruction. See `docs/specification.md`.
- Diagnostics never control business results. Isolate only the adopted observation/formatting/output boundary, not the surrounding business command. Do not convert valid callers to a new no-throw prerequisite or swallow real native/command failures.
- Native `Verse.Log` calls belong only to the named `SqueakLogSink.Emit` member, reached through protected named event/forensic facade methods. No raw logger, debug exception or directory-wide exemption. Detail preparation stays behind the effective mode gate; memory records, receipts, feedback, publication repair and remaining cleanup do not depend on successful logging.
- When the original entry is insufficient, extend it or propose an independent module/new cohesive core/responsibility migration using `docs/development/specification/changes.md`: state need, owner/lifetime, adjacent guarantees, the complete entry/consumer/failure/release slice, and adoption/exit. Original owners supply concrete constraints and alternatives; adopted capabilities follow normal maintenance. No new governance platform or CI gate is implied.

## Memory protocol

At every non-trivial session:

- Read `MEMORY.md` before claiming project context; it stores confirmed durable facts, decisions, constraints, and evidence pointers.
- Read `TODO.md` before continuing work; it stores only current goals, open actions, blockers, and explicit deferrals.
- Read `OBLIVIONIS.md` only for a historical conflict or explicit request; it is cold archive evidence and cannot override current sources.
- The three active memory files (`AGENTS.md`, `MEMORY.md`, `TODO.md`) are maintained in accurate English; `OBLIVIONIS.md` is the cold archive and follows the same language rule when appended.
- This is a local fork of SR. SR state must be sourced from the SR repository and never inferred from this repository.

Maintain these boundaries:

- Update `MEMORY.md` only when durable facts or the open action surface changes; keep it compact.
- Keep incident-derived lessons and measured implementation facts in `MEMORY.md`, rather than promoting them into standing instructions in `AGENTS.md`.
- Compact by default: settled release/implementation details live in `docs/release_review/` (Claim Packs, process review) and the runbook; MEMORY keeps only pointers. Do not grow MEMORY with finished work.
- Update `TODO.md` only when its current task surface changes.
- Do not store session narratives, transient artifacts, raw logs, completed test matrices, commit chains, or release checklists in either active memory file.
- Documentation edits alone are not memory events; external-state summaries never override their authoritative source.

## Privacy and security

- Default scope is this repository root. Reading outside it requires path-specific authorization and remains read-only.
- Never place personal local paths, diagnostic-log excerpts, credentials, API keys, tokens, private keys, or `PublishedFileId.txt` values in Git, documentation, generated artifacts, staging, or reachable history.
- An authorized push is preceded by a privacy review of the complete reachable range (`scripts/privacy-audit.ps1 -FullHistory`; `docs/push-privacy-gate.md`). Current-tree success is not history success. Configuring a remote, pushing, tagging or releasing remains an external operation requiring explicit maintainer authorization.
- Destructive history/tag rewrites of the SR repository are out of scope here and must be handled on the SR side.

## External-state boundaries

- Local commits are permitted. `git remote`, push, PR, tag, release, and all publication actions require explicit maintainer authorization.

