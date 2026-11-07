# AGENTS.md — Universal Squeaker

> This file contains repository-specific working instructions. Human contributors should read `README.md`.

## Work entry

- Task and status: the assigned task defines authority; read the relevant [TODO](TODO.md) and [MEMORY](MEMORY.md) sections as needed. This repository has no separate START entry.
- Stable contracts: [specification](docs/specification.md) and the applicable rules below.
- LESSONS: no repository-local LESSONS is currently set; read applicable lessons supplied with the task or a later local `LESSONS.md` if present.
- Verification: [build and verification guidance](docs/build-and-debug.md) and the checks required by the assigned work; select by actual risk and permission.

Read applicable AGENTS first, then the responsibility entry and task evidence. The project owner controls
path mappings; workers do not create competing STATUS/PROGRESS or relocate authority. Authorized moves update
consumers and entries. Keep this file concise and stable: durable rules and a few stable navigation pointers.
Current owners, phases, versions and provisional conclusions stay in the linked authorities; update this
entry only when responsibilities or structure change, not for each task.

## Project identity

- Project: RimWorld mod **Universal Squeaker**. Version/adapter guarantees and implemented support are described in `docs/specification.md`; publication status belongs in `MEMORY.md`, not this stable entry.
- Confirmed identity: repo `coahuilite/UniversalSqueaker`; permanent `packageId` `coahuilite.universalsqueaker`; target C# namespace `UniversalSqueaker`; Def/log/debug-key prefix `US_`; diagnostic log prefix `usdiag`. License is **MPL-2.0** for the whole Coahuilite mod series (`LICENSE`, byte-identical per repo, no "Incompatible With Secondary Licenses" notice). Display/publication decisions and release history live in `MEMORY.md`; they do not grant permission to publish.
- Squeaky Ratkin (`coahuilite.squeakyratkin`) is a separate product. Never reuse its brand, packageId, namespace, or `SR_` prefix here. No `SqueakyRatkin.*` types or SR-owned assemblies, profiles, attachments or audio assets may ship with US. Neutral US fallback profile data may reference Core audio for any supported race.
- Product version source: `Source/UniversalSqueaker/UniversalSqueaker.csproj <Version>` is primary; the bare numeric `VersionPrefix` and `About/About.xml <modVersion>` follow it.

Current product contracts start at `docs/specification.md`; source/status mappings are under `docs/development/specification/`. Replaced full texts are retained in `docs/archive/specs-before-20261029/`.

## Maintenance-line boundary

This is the US0.6/FL0.8 maintenance line for RimWorld 1.6/net472. Select an explicit compatible FL0.8
carrier and preserve its Dev pairing. The existing LoadFolders mapping of 1.7 to the 1.6 payload is a
basic compatibility candidate, not native 1.7 acceptance or permission to replace this line with FL0.9.
Keep the maintenance line's existing embedded UiKit Dev entry until a compatible replacement is accepted;
the separate 0.9 migration alone does not make it dead code. US Kernel diagnostics are a different product
responsibility. Actual build inputs and accepted outputs are in [the build guide](docs/build-and-debug.md).

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

## UI, diagnostics and preview

- Adopt the actual FL page/window contracts and US author XML, styles and typed bindings. Use layout,
  spacing and typography for hierarchy; keep units and effective/inherited values clear, and use existing
  on-demand help instead of adding labels everywhere. Do not shrink text to hide a layout problem.
- Reuse the production page/binding/edit/lifetime path in offline providers with isolated sample state;
  do not build a second hard-coded page or mutate real Config/Saves from preview. UI changes follow
  [UI consumption](docs/openspec/specs/ui-consumption/spec.md) and
  [same-source preview](docs/openspec/specs/developer-preview/spec.md).
- Browsing Pawn B must neither replace recording target A nor change the vanilla selected Pawn. Preserve
  those independent identities during navigation and filtering; closing/reopening follows each owner's
  existing cleanup contract. The pawn-head screen marker is a US projection/control, not a new business
  responsibility for FL or the UiKit inspector.
- Diagnostics remain bounded observers. Measure actual changed hot paths and separate US Kernel, host
  rendering and tool/RPC cost; do not call RPC round-trip time FPS or extrapolate a formatting cleanup
  into a general performance claim. Capability expansion needs evidence and its own authorized slice.

## Verification boundaries

Use the existing Kernel/Core checks and affected adapter, settings, consumer or packaging entries from
[build and verification guidance](docs/build-and-debug.md). No new unit tests/cases or equivalent renamed
probes; no changed golden/thresholds merely to pass. Keep 1.6/1.7, FL profiles and native/headless evidence
separate. Reuse applicable checks instead of repeating full matrices for a documentation or session change.
The selected compile-time FL artifact must match the runtime pair; US never ships its own FL DLL. Use pwsh 7
only, including child processes. Real game/Config/Saves, foreground input, installation and publication
require their corresponding authorization, not just permission to edit the repository.

## Memory protocol

At the start of relevant work:

- Read only the `MEMORY.md` sections relevant to the task when confirmed facts, decisions, constraints or evidence pointers are needed.
- Read `TODO.md` when selecting or continuing work; it stores current goals, open actions, blockers, and explicit deferrals.
- Read `OBLIVIONIS.md` only for a historical conflict or explicit request; it is cold archive evidence and cannot override current sources.
- Memory files use the fixed basenames `AGENTS.md`, `MEMORY.md`, `TODO.md` and `LESSONS.md`, created only when needed and maintained in accurate English. The existing `OBLIVIONIS.md` is optional historical material and follows the same language rule when appended.
- This is a local fork of SR. SR state must be sourced from the SR repository and never inferred from this repository.

Maintain these boundaries:

- Update `MEMORY.md` only when confirmed durable facts change; keep it compact and leave current actions at their owning surface.
- Keep measured implementation facts at their responsible source. Read lessons relevant to the task when supplied with it or in a local `LESSONS.md`, if present; retain each reusable lesson and its evidence in its responsible scope, without duplicate ledgers. Promotion uses an existing applicable check or instruction when evidence warrants and follows current verification limits.
- Compact by default: settled release/implementation details live in `docs/release_review/` (Claim Packs, process review) and the runbook; MEMORY keeps only pointers. Do not grow MEMORY with finished work.
- Update `TODO.md` only when its current task surface changes.
- Do not store session narratives, transient artifacts, raw logs, completed test matrices, commit chains, or release checklists in either active memory file.
- Documentation edits alone are not memory events; external-state summaries never override their authoritative source.
- An existing current record can serve a handoff if it gives the next owner the result, inputs, evidence, limits and next action. Create a separate handoff only for an actual consumer or recovery need.

## Privacy and security

- Default scope is this repository root. Reading outside it requires path-specific authorization and remains read-only.
- Never place personal local paths, diagnostic-log excerpts, credentials, API keys, tokens, private keys, or `PublishedFileId.txt` values in Git, documentation, generated artifacts, staging, or reachable history.
- An authorized push is preceded by a privacy review of the complete reachable range (`scripts/privacy-audit.ps1 -FullHistory`; `docs/push-privacy-gate.md`). Current-tree success is not history success. Configuring a remote, pushing, tagging or releasing remains an external operation requiring explicit maintainer authorization.
- Destructive history/tag rewrites of the SR repository are out of scope here and must be handled on the SR side.

## External-state boundaries

- Local commits are permitted. `git remote`, push, PR, tag, release, and all publication actions require explicit maintainer authorization.

