# Universal Squeaker

**English** | [中文](./README.zh-CN.md)

Universal Squeaker (US) lets RimWorld pawns speak with voices from third-party voice packs, routed by **exact race and xenotype**.
It is a voice-pack routing kernel: it ships no audio of its own and decides who speaks, when, which sound from which pack, and how.
Derived from Squeaky Ratkin under MPL-2.0. The tree includes one Ratkin final-fallback mapping that references vanilla Core audio.

## Status

| Item | Status |
| --- | --- |
| Version | 0.6.0 |
| Release | v0.6.0-rc1 in preparation, not yet published |
| RimWorld 1.6 | Primary target |
| RimWorld 1.7 | Basic-compatibility candidate, to be used with the matching FerriteLib package; sound and UI not yet verified in game |
| Prerequisites | Harmony, FerriteLib 0.8.x |

See the [release notes](docs/releases/v0.6.0-rc1.md) for scope and upgrade notes, and
[open issues](docs/development/specification/open-issues.md) for known deviations and evidence limits.

## For players

### Install and update

1. Install **[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)** (`brrainz.harmony`) and
   **FerriteLib** (`coahuilite.ferritelib`, 0.8.x) and load both before US. Neither is included in the US archive.
2. Install at least one voice-pack mod. US makes no sound on its own.
3. To update, quit the game completely, move the old US folder out of Mods, then put the new complete folder in its place.
   Do not overwrite only the DLL. **Keep the same folder name** to keep your settings; there is no need to clear Config or saves.

### Open the settings

Three entries open the same window: the startup menu, **US Voices** in the category column of the vanilla Options, and the vanilla
Mod Settings. The two added entries can be hidden independently.

In the settings window you can:

- Choose which voice packs are active for each race or xenotype. Humanoid races and legal xenotypes are listed by default; animals
  are opt-in. Filter by category, keyword and Def source.
- Tune each action: fifteen official actions such as select, draft, attack and eat, each with its own scope and parameters.
- Edit final-fallback tables: what a race uses when no suitable pack exists.
- Switch appearance: Original dark, Royalty, or Black & gold.
- Check prerequisites and record diagnostic feedback.

### Reporting a voice problem

In a map, open **Settings & tools → Diagnostics & dependencies → Open response diagnostics**, close settings normally, **Expand**
the diagnostic bar, select or lock the pawn, then **Start new recording → reproduce normally → Stop → Copy feedback**. Paste the
copied text with a description of the problem. No developer mode or detailed logs are needed. The latest 64 results are kept;
leaving or reloading the game clears them.

## How it works

In one sentence: **a game event → one response owner per pawn decides whether to speak → a sound is selected from the packs of
that pawn's exact domain → modulated → played natively.** The decision core (`Core/`) has no game references: it takes facts and
returns decisions. All code that touches the game lives in the adapter layer.

```text
Vanilla events (Harmony patches, observers)   Voice-pack and action Defs (XML)    Player settings
          │                                          │                               │
          ▼                                          ▼                               ▼
 SqueakerRuntime (1.6 adapter)                 DefinitionCatalog         GameSettings → configuration snapshot
          │ facts                                    │ declarations                  │
          └──────────────► ResponseSession (one per pawn, serial) ◄──────────────────┘
                                 │  ResponseDecision: opportunity, conditions, cooldown, settlement
                                 ▼
                           AudioCatalog: select by exact race + xenotype domain and selection mode
                                 ▼
                           local modulation: volume, pitch and related parameters
                                 ▼
                 UsNativeAudioPool / UsInstanceOutput → native game sound
```

| Concept | Meaning |
| --- | --- |
| Domain | an exact race + xenotype combination. A pack applies only to the domains it declares; nothing is guessed by name |
| Action | the fifteen official actions plus author-defined ones, each with its own conditions and parameters |
| Opportunity | one legal moment to speak. Player actions (select, draft) use real seconds; simulated events use game ticks |
| Response owner | one `ResponseSession` per pawn that serially decides, cools down and settles, so one pawn never double-speaks |
| Final fallback | the mapping used when a domain has no usable pack |

**The settings UI** is built on FerriteLib.UiKit: `UI/Layout.Schema2.xml` defines page structure → `UsKernelSettingsHost`
registers widgets and builds bindings → `IUsKernelSettingsSource` is the single boundary between UI and business logic.
The UI only sends requests; the business side decides whether and when anything is saved or takes effect.

### Source map (`Source/UniversalSqueaker/`)

| Folder | Contents |
| --- | --- |
| `Core/` | engine-free decision core: `Definitions`, `Configuration` (behavior settings), `Responses` (opportunities and settlement), `Audio` (catalog, selection, modulation) |
| `Game/` | 1.6 adapter: runtime, bootstrap, vanilla event observers, settings persistence |
| `Runtime/` | the Comp, the kernel seam, native audio pool and output |
| `Patches/` | Harmony patches for event and settings entries |
| `Catalog/` | race, xenotype and Def identity collection |
| `Settings/` | player settings read/write, migration, reset, preset import |
| `Logging/`, `Diagnostics/` | log sink, diagnostic recording and feedback |
| `UI/` | the settings window: page definition, Host, widgets and view models |
| `tools/` (repository root) | tests and fixtures |

## For voice-pack authors

- [Author guide](docs/us-kernel-0.6-authoring-zh.md) and [loadable examples](docs/examples/0.6/)
- Specification: [audio selection](docs/openspec/specs/audio-selection/spec.md) and
  [the fifteen official actions](docs/openspec/specs/official-actions/spec.md)
- Migrating from older versions: [0.6 upgrade notes](docs/upgrade-0.6-zh.md)

## For contributors

Building requires a fixed FerriteLib carrier DLL:

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1 -FerriteLibArtifactPath '<FerriteLib package>/1.6/Assemblies/FerriteLib.UiKit.dll'
pwsh -NoProfile -File scripts/build-dev.ps1 -FerriteLibArtifactPath '<FerriteLib package>/1.6/Assemblies/FerriteLib.UiKit.dll'
```

| Document | Purpose |
| --- | --- |
| [docs/specification.md](docs/specification.md) | current specification entry, the single authority for behavior guarantees |
| [docs/openspec/specs/](docs/openspec/specs/) | specification text, one folder per capability |
| [docs/development/specification/open-issues.md](docs/development/specification/open-issues.md) | implementation deviations and evidence status |
| [docs/build-and-debug.md](docs/build-and-debug.md) | building, gates and diagnostics |
| [docs/fl08-consumer-migration-zh.md](docs/fl08-consumer-migration-zh.md) | consuming the fixed FerriteLib 0.8 package |
| [docs/release-runbook-zh.md](docs/release-runbook-zh.md), [docs/push-privacy-gate.md](docs/push-privacy-gate.md) | release flow and privacy gate |
| [docs/README.md](docs/README.md) | full documentation index and history |
| `AGENTS.md`, `MEMORY.md`, `TODO.md` | working rules, durable facts and open actions for development agents, not user documentation |

## Identity

- packageId: `coahuilite.universalsqueaker`
- Namespace / Def prefix / diagnostic prefix: `UniversalSqueaker` / `US_` / `usdiag`
- The FerriteLib API range is checked at startup: `[0.8.0, 0.9.0)`. US ships neither FerriteLib nor `0Harmony.dll`.
- License: [MPL-2.0](LICENSE)
