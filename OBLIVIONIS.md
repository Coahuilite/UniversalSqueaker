# OBLIVIONIS

> Cold archive. Read only for a historical conflict or an explicit request; it cannot override current sources.

## Entries

- 2026-08-23 US rebuild cleanup: the SR-derived reference trees were consumed and deleted from the working tree — root `Kernel/` (9 cs files), `Pure/` (2 cs files), `fixtures/` (SR corpora + 0.2.4-shaped settings fixtures), `sr_reference/` (SR SoundDefs/localization snapshot), and `tools/KernelCharacterization/` (legacy SR harness). They remain retrievable from git history. Pre-rebuild baseline commit: `dc8c598`; last pre-cleanup commit: `0c4871a`. Rebuild commits: `a00bbfa` (de-SR-ized kernel/pure + US test gate), `9de2161` (runtime assembly + data surface + tool gates), `0c4871a` (componentized minimal UI).
