# Universal Squeaker

Accepted Select now uses 0.3 real seconds; Draft/Undraft use 0.6 each, while simulation keeps game ticks. This single explicit-opportunity contract rejects missing facts and obsolete timing fields rather than falling back. See [timing and upgrade guidance](docs/response-opportunities-0.6-zh.md).

Preparing **v0.6.0-rc1**, a prerelease/non-Latest for RimWorld1.6. The ea610fa/FL833c14f4 baseline received overall internal-trial acceptance; granular native results remain scoped known limits. See [release and upgrade notes](docs/releases/v0.6.0-rc1.md). The new local fixed input for CI preparation is not a publishedRC2 dependency.

0.6 r2: [current author/configuration contract](docs/us-kernel-0.6-refactor-zh.md) — complete neutral Core, one synchronous response owner and explicit notification declarations. Ordinary responses now initialize automatically without developer mode; native/human acceptance remains pending.

**English** | [中文](./README.zh-CN.md)

A voice-pack routing kernel for RimWorld 1.6, derived from Squeaky Ratkin under MPL-2.0.

US attaches third-party voices to accurate race/xenotype domains, with layered tuning, mood modulation, easter-egg eligibility, camera-height attenuation and diagnostics. It ships no audio files of its own; the current tree includes a Ratkin final-fallback mapping that references vanilla Core audio.

Open **US Voices** from the startup menu, the vanilla Options category column, or Mod Settings. The default voice-management page keeps mode, volume, search and exact pack targets together; **Settings & tools** opens your last editor in this window, with equal-width direct tabs. Context help expands below the business page only when requested. Appearance offers Original dark, Royalty and Black & gold from one embedded colour resource. The two native entries can be hidden independently. See the [current UI contract](docs/ui-contract-0.6-zh.md) for compact/wide layouts and native acceptance boundaries.

The definition directory starts with humanoid races and legal xenotypes; animals are opt-in. Category, keyword and real Def-source filters combine, with bounded searchable source lists and reachable result pages. Existing fallback tables, new Race candidates and Core sounds keep separate browsing state; selecting a candidate does not create a table. At logical1024×768 the centred window is864×640, with no extra UI scaling. The native Options entry carries the US microphone icon.

## Requirements and identity

- RimWorld 1.6.
- **[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)** (`brrainz.harmony`) is a required external mod; US does not bundle `0Harmony.dll`.
- **FerriteLib** (`coahuilite.ferritelib`) is a required prerequisite. US references its DLL and never ships a second copy; the supported API range is checked in code.
- packageId: `coahuilite.universalsqueaker`.
- Namespace / Def prefix / diagnostic prefix: `UniversalSqueaker` / `US_` / `usdiag`.
- License: [MPL-2.0](LICENSE).

Install and enable Harmony and the paired FerriteLib package before Universal Squeaker in the mod list. Both prerequisites are installed separately from the US archive.

Current Dev builds explicitly consume a fixed FerriteLib **0.8** package (`[0.8.0,0.9.0)`). See the [consumer migration guide](docs/fl08-consumer-migration-zh.md) for build input, retained UI behavior and native acceptance boundaries.

## Documentation

This branch prepares **0.6.0-rc1**: the single production owner covers [all fifteen contracts and the settled configurable Select qualifications](docs/us-kernel-0.6-spec-zh.md#23-d-c-十五包select资格与明确事实边界), with real author examples and per-package SR comparisons. The adopted ordinary defaults are documented in the specification; native behavior and all-fifteen human acceptance remain open. The [documentation index](docs/README.md) distinguishes retained 0.5 guides from current 0.6 interfaces.

- [Current r2 behavior and settings](docs/us-kernel-0.6-refactor-zh.md)
- [Current 0.6 r2 authoring](docs/us-kernel-0.6-authoring-zh.md)
- [Kernel 0.6.x first-edition specification](docs/us-kernel-0.6-spec-zh.md)
- [Build and debug](docs/build-and-debug.md)
- [Release flow](docs/release-runbook-zh.md) and [privacy gate](docs/push-privacy-gate.md)

The current product declaration is 0.6.0. Documentation describes this source tree, which may include changes not present in an older download. Developer working instructions remain in `AGENTS.md`; durable state and open actions remain in `MEMORY.md` and `TODO.md`.
