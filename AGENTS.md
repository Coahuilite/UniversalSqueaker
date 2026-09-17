# AGENTS.md — Universal Squeaker

> This file is the memory agreement for AI agents working in this repository. Human contributors should read `README.md`.

## Project identity

- Project: RimWorld 1.6 mod **Universal Squeaker** (local fork; remote `Coahuilite/UniversalSqueaker` created private and pushed 2026-09-06; publication actions still maintainer-gated per session).
- Confirmed identity: repo `coahuilite/UniversalSqueaker`; permanent `packageId` `coahuilite.universalsqueaker`; target C# namespace `UniversalSqueaker`; Def/log/debug-key prefix `US_`; diagnostic log prefix `usdiag`. License is **MPL-2.0** for the whole Coahuilite mod series (`LICENSE`, byte-identical per repo, no "Incompatible With Secondary Licenses" notice). Workshop display name remains pending maintainer confirmation; the GitHub repo is **public since 2026-09-07** and the first release `v0.2.0-rc1` (prerelease) is cut — current publication state lives in `MEMORY.md`.
- Squeaky Ratkin (`coahuilite.squeakyratkin`) is a separate product. Never reuse its brand, packageId, namespace, or `SR_` prefix here. No `SqueakyRatkin.*` types and no Ratkin assemblies, profiles, attachments, or content may ship with US.
- Product version source: once the product csproj exists, its `<Version>` is primary and `About/About.xml <modVersion>` must follow it.

## Project philosophy

- Neutral routing: a VoicePack's `raceDefName` declaration is the only routing entry. There is no built-in race special-casing; HAR races, vanilla Human, and any third-party race route identically.
- Optional dependencies degrade silently and never crash. HAR reflective discovery is **intent, not the shipped build**: `Catalog/SqueakXenotypeCatalog.cs` carries `TODO(HAR)` and discovers assembled defs only, so the reflective hint lists stay empty until that TODO lands.
- Uninstall safety is a hard rule: removing the mod must never affect a saved game. No permanent data is written into saves; settings and profiles live in the Config folder.
- Content and kernel are separate: the Kernel compile set must not reference Verse/Unity/RimWorld and must not contain product sound keys or race seeds. `SR_*`/Ratkin seed data belongs to SR content packs; US provides data-driven fallback profiles.

## Memory protocol

At every non-trivial session:

- Read `MEMORY.md` before claiming project context; it stores confirmed durable facts, decisions, constraints, and evidence pointers.
- Read `TODO.md` before continuing work; it stores only current goals, open actions, blockers, and explicit deferrals.
- Read `OBLIVIONIS.md` only for a historical conflict or explicit request; it is cold archive evidence and cannot override current sources.
- The three active memory files (`AGENTS.md`, `MEMORY.md`, `TODO.md`) are maintained in accurate English; `OBLIVIONIS.md` is the cold archive and follows the same language rule when appended.
- This is a local fork of SR. SR state must be sourced from the SR repository and never inferred from this repository.

Maintain these boundaries:

- Update `MEMORY.md` only when durable facts or the open action surface changes; keep it compact.
- Compact by default: settled release/implementation details live in `docs/release_review/` (Claim Packs, process review) and the runbook; MEMORY keeps only pointers. Do not grow MEMORY with finished work.
- Update `TODO.md` only when its current task surface changes.
- Do not store session narratives, transient artifacts, raw logs, completed test matrices, commit chains, or release checklists in either active memory file.
- Documentation edits alone are not memory events; external-state summaries never override their authoritative source.

## Privacy and security

- Default scope is this repository root. Reading outside it requires path-specific authorization and remains read-only.
- Never place personal local paths, diagnostic-log excerpts, credentials, API keys, tokens, private keys, or `PublishedFileId.txt` values in Git, documentation, generated artifacts, staging, or reachable history.
- Every push is preceded by a privacy review of the complete reachable range (`scripts/privacy-audit.ps1 -FullHistory`). Configuring a remote, pushing, tagging or releasing remains an external operation requiring explicit maintainer authorization.
- Destructive history/tag rewrites of the SR repository are out of scope here and must be handled on the SR side.

## External-state boundaries

- Local commits are permitted. `git remote`, push, PR, tag, release, and all publication actions require explicit maintainer authorization.
- The pre-push ceremony is deliberately minimal (maintainer ruling 2026-09-06): run `scripts/privacy-audit.ps1 -FullHistory` plus the mechanical final check (clean tree, single main, no tags, noreply identity, mirror backup present). Everything else is automated by verify-local / check-pack-readiness / the workflows - do not re-add manual ritual. Push order and durable upload decisions: `MEMORY.md` "First cloud upload: durable decisions".

## Push discipline (added 2026-09-17; every rule below was paid for once)

These extend the minimal ceremony above - they do not replace it.

- **Use the repo's noreply identity from the FIRST local commit.** A placeholder identity (`US Dev <noreply@local>` was used once) is not cosmetic: the audit's identity-uniqueness vector fails on it, and the only fix is rewriting the commits before the push. Check `git config user.name` / `user.email` before writing history, not before pushing it.
- **The audit scans `git rev-list --all`, so every local ref participates.** Consequences: (1) a leak fixed in the working tree still fails the gate while the offending blob is reachable from *any* ref; (2) a **backup tag made after the fix re-exposes the old history** and fails the audit on its own; (3) the check is therefore "no reachable ref carries it", not "the file is clean today". Move backups out of the repository (a `git bundle` on the local disk) before running the audit.
- **Cleaning history is a rewrite of the unpublished range only, and it has a cross-repo cascade.** Rewrite only the commits after the last *published* tip, preserve author/committer/message metadata, and verify the final tree is byte-identical to the pre-rewrite tip. Then expect downstream work: the rewritten SHA is cited by the consumer repo and is embedded in the carrier payload's `AssemblyInformationalVersion`, so the payload must be **rebuilt** and the consumer's gates re-run. Do not rewrite a line that is already public.
- **Short-lived feature branches must be deleted, together with their worktrees, when their work lands.** Eleven stale `feat/0.5-*` / `feat/0.6-*` branches and their `.fl-worktrees/` entries survived earlier rounds; they are hygiene debt *and* an audit liability under the previous rule. Verify `git worktree list` is down to the main checkout.
- **Run `-FullHistory` before the FIRST push of a line, not only before a release.** A line that has never been pushed is exactly where a rewrite is still cheap.

