# Universal Squeaker

Accepted Select now uses 0.3 real seconds; Draft/Undraft use 0.6 each, while simulation keeps game ticks. This single explicit-opportunity contract rejects missing facts and obsolete timing fields rather than falling back. See [timing and upgrade guidance](docs/openspec/specs/response-opportunities/spec.md).

Preparing **v0.6.0-rc1**, a prerelease/non-Latest for RimWorld1.6. The ea610fa/FL833c14f4 baseline received overall internal-trial acceptance; granular native results remain scoped known limits. See [release and upgrade notes](docs/releases/v0.6.0-rc1.md). The new local fixed input for CI preparation is not a publishedRC2 dependency.

The local RimWorld 1.7 basic-compatibility candidate must be used with its paired FerriteLib package. Native sound and rendering still await human verification. The existing 1.6 entry and content remain available; see the [compatibility evidence and limits](docs/development/specification/open-issues.md#rw17-基础兼容候选).

0.6: [current specification](docs/specification.md) — complete neutral Core, one synchronous response owner and explicit notification declarations. Ordinary responses now initialize automatically without developer mode; native/human acceptance remains pending.

**English** | [中文](./README.zh-CN.md)

A voice-pack routing kernel for RimWorld, derived from Squeaky Ratkin under MPL-2.0.

US attaches third-party voices to accurate race/xenotype domains, with layered tuning, mood modulation, easter-egg eligibility, camera-height attenuation and diagnostics. It ships no audio files of its own; the current tree includes a Ratkin final-fallback mapping that references vanilla Core audio.

Open **US Voices** from the startup menu, the vanilla Options category column, or Mod Settings. The default voice-management page keeps mode, volume, search and exact pack targets together; **Settings & tools** opens your last editor in this window, with equal-width direct tabs. Context help expands below the business page only when requested. Appearance offers Original dark, Royalty and Black & gold from one embedded colour resource. The two native entries can be hidden independently. See the [current UI contract](docs/openspec/specs/ui-consumption/spec.md) for compact/wide layouts and native acceptance boundaries.

The definition directory starts with humanoid races and legal xenotypes; animals are opt-in. Category, keyword and real Def-source filters combine, with bounded searchable source lists and reachable result pages. Existing fallback tables, new Race candidates and Core sounds keep separate browsing state; selecting a candidate does not create a table. At logical1024×768 the centred window is864×640, with no extra UI scaling. The native Options entry carries the US microphone icon.

## Feedback

To report a voice problem in a map, open **Settings & tools → Diagnostics & dependencies → Open response diagnostics**, close settings normally, **Expand** the diagnostic bar, select or lock the pawn, then **Start new recording → reproduce normally → Stop → Copy feedback**. No developer mode or detailed logs are required. Opening only starts viewing; recording keeps the latest 64 results and marks overwritten or missing evidence. Closing stops recording and retains evidence for this game; leaving or reloading the game clears it. Paste the feedback with a description of the problem; no configuration or raw log collection is needed.

## Requirements and identity

- RimWorld 1.6, or 1.7.4990 rev1069 with this basic-compatibility candidate.
- **[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)** (`brrainz.harmony`) is a required external mod; US does not bundle `0Harmony.dll`.
- **FerriteLib** (`coahuilite.ferritelib`) is a required prerequisite. US references its DLL and never ships a second copy; the supported API range is checked in code.
- packageId: `coahuilite.universalsqueaker`.
- Namespace / Def prefix / diagnostic prefix: `UniversalSqueaker` / `US_` / `usdiag`.
- License: [MPL-2.0](LICENSE).

Install and enable Harmony and the paired FerriteLib package before Universal Squeaker in the mod list. Both prerequisites are installed separately from the US archive.

Current builds explicitly consume a fixed FerriteLib **0.8** package (`[0.8.0,0.9.0)`). See the [consumer migration guide](docs/fl08-consumer-migration-zh.md) for build input, retained UI behavior and native acceptance boundaries.

## Documentation

The [current specification](docs/specification.md) is the single authority for capabilities, producer-consumer connections, native facts and ordinary defaults. [Implementation status and open defects](docs/development/specification/open-issues.md) remain separate; all fifteen itemized native/human gates retain their evidence limits.

- [Author guide and examples](docs/us-kernel-0.6-authoring-zh.md)
- [Upgrade and explicit source recovery](docs/upgrade-0.6-zh.md)
- [Build and debug](docs/build-and-debug.md)
- [Release flow](docs/release-runbook-zh.md) and [privacy gate](docs/push-privacy-gate.md)
- [Documentation index](docs/README.md)

The current product declaration is 0.6.0. Documentation describes this source tree, which may include changes not present in an older download. Developer working instructions remain in `AGENTS.md`; durable state and open actions remain in `MEMORY.md` and `TODO.md`.
