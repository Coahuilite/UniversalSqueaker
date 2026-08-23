# OBLIVIONIS

> Cold archive. Read only for a historical conflict or an explicit request; it cannot override current sources.

## Entries

- 2026-08-24 legacy bridge activation (maintainer authorization): the 0.4 co-existence rule "US must not define SqueakyRatkin.* types" was explicitly overridden for one thin empty `SqueakyRatkin.SqueakVoicePackDef` compatibility shim. Old SR VoicePacks now load through the bridge and are explicitly marked as old SR content in logs (`voicepack.pack.legacy_admitted`) and UI (`Legacy SR` tag/banner). No Ratkin audio/content ships with US.
- 2026-08-23 US rebuild cleanup: the SR-derived reference trees were consumed and deleted from the working tree — root `Kernel/` (9 cs files), `Pure/` (2 cs files), `fixtures/` (SR corpora + 0.2.4-shaped settings fixtures), `sr_reference/` (SR SoundDefs/localization snapshot), and `tools/KernelCharacterization/` (legacy SR harness). They remain retrievable from git history. Pre-rebuild baseline commit: `dc8c598`; last pre-cleanup commit: `0c4871a`. Rebuild commits: `a00bbfa` (de-SR-ized kernel/pure + US test gate), `9de2161` (runtime assembly + data surface + tool gates), `0c4871a` (componentized minimal UI).
