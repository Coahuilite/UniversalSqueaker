# Universal Squeaker

**English** | [中文](./README.zh-CN.md)

A voice-pack routing kernel for RimWorld 1.6, derived from Squeaky Ratkin under MPL-2.0.

US attaches third-party voices to accurate race/xenotype domains, with layered tuning, mood modulation, easter-egg eligibility, camera-height attenuation and diagnostics. It ships no audio files of its own; the current tree includes a Ratkin final-fallback mapping that references vanilla Core audio.

The settings window has five workspaces: **Overview / Packs / Tuning / Presets / Advanced**, plus an expandable bottom help panel.

## Requirements and identity

- RimWorld 1.6.
- **FerriteLib** (`coahuilite.ferritelib`) is a required prerequisite. US references its DLL and never ships a second copy; the supported API range is checked in code.
- packageId: `coahuilite.universalsqueaker`.
- Namespace / Def prefix / diagnostic prefix: `UniversalSqueaker` / `US_` / `usdiag`.
- License: [MPL-2.0](LICENSE).

## Documentation

This branch is **0.6.0 Dev**: the single production owner covers [all fifteen contracts and the settled configurable Select qualifications](docs/us-kernel-0.6-spec-zh.md#23-d-c-十五包select资格与明确事实边界), with real author examples and per-package SR comparisons. Ordinary coverage/policy, native behavior and human acceptance remain separate gates. The [documentation index](docs/README.md) distinguishes retained 0.5 guides from current 0.6 interfaces.

- [Current behavior and settings](docs/current-behavior-0.5-zh.md)
- [Retained 0.5 VoicePack authoring](docs/voicepack-authoring-0.5-zh.md)
- [Kernel 0.6.x first-edition specification](docs/us-kernel-0.6-spec-zh.md)
- [Build and debug](docs/build-and-debug.md)
- [Release flow](docs/release-runbook-zh.md) and [privacy gate](docs/push-privacy-gate.md)

The current product declaration is 0.6.0. Documentation describes this source tree, which may include changes not present in an older download. Developer working instructions remain in `AGENTS.md`; durable state and open actions remain in `MEMORY.md` and `TODO.md`.
