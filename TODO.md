# TODO

> Action surface only. Landed history is in git log, `OBLIVIONIS.md` (cold archive) and the dated
> pointer lines below; durable engineering rules are in `MEMORY.md`.

## Blocked on the lib session / maintainer (cloud publication chain)

- **Cross-repo round CLOSED on both sides (2026-09-07) — US→FL round 1**: P1-P6 + A-E landed in `../ferritelib` (carrier Api 0.3.0), and the US consumer half (the FL 0.3.0 migration package, `HANDOFF.md` §1 M1-M6) landed the same day: probe deleted, chrome into `UiWindowHost`, the D10 claim machine into `UiSession`, `UiBindings.ActiveTabKey`, both frame gates on `UiNative.IsLayoutEvent`, pin written back to `[0.3.0, 0.4.0)` as the last step. Gate 14's US whitelist is now 2 entries with a zero raw-hover assertion. **No library defect surfaced, so no round 2 was opened.** What is left is not code: the carrier 0.3.0 release tag + US's next rc pairing is a maintainer decision (FL holds its tag until this commit; §2 forbids touching the US version axes).
- [x] ~~US `HANDOFF.md` §1 items 1-6 (seam round)~~ **EXECUTED 2026-09-07b** (dev session): chrome close button `UiNative.Button`; hover behind a then-new `UI/UsHoverProbe.cs`; checkbox off `DrawBoxSolid` onto `UiThemeDraw.Surface`; `VoicePacksPageState` scroll fossils deleted; boundary gate landed as verify-local **gate 14** (self-testing scanner, append-only gate numbers). Its 5-file whitelist and the `[0.2.0, 0.3.0)` pin were both superseded the same day by the migration above - the only-shrink ratchet is what made that shrink an expected outcome rather than a renegotiation.
- [ ] **In-game smoke of BOTH 2026-09-07 rounds (maintainer, not runnable here)**: five workspaces, one dropdown, the camera readout, help-panel hover linkage (D10: no overview flash between adjacent controls, release after ~15 passes), close-button hover/click, real-screen window size, and one notice path if convenient. Both rounds are pass-through or shell-owned (gate 14 + 14 gates + both UI harnesses cover the static half), so the expectation is "no behaviour change" - with two deliberate deltas to look for: (1) the chrome's title/subtitle now carry `singleLine: true`, so a localized title that is too wide reports `ui.text.overflow` on the **width** axis (shell band, not a US regression); (2) switching workspace no longer clears the help-hover claim instantly (that write died with the state field) - the session's grace window releases it, so a `scroll-to` triggered away from the nav column can keep the previous explanation for up to ~0.25 s.

- [x] ~~CI first real run~~ **GREEN 2026-09-07** (run `34075213342`): carrier checkout + full-tree sibling staging + 13/13 gates + privacy audit CLEAN. Two runner-only defects found and fixed on the way (DLL-only staging broke gate 13's in-place stub build; missing `* text=auto eol=lf` would have broken gate 10's byte-exact licence compare on a CRLF checkout) - both proven by a clean worktree simulation before re-push.
- [x] ~~Branch protection / public flip~~ **DONE 2026-09-07**: repo flipped public on maintainer authorization (collaborator download need; full-history privacy audit CLEAN immediately before). Protection applied same session in the carrier-minimal shape (force-push off, deletion off; PUT requires explicit `"restrictions": null`). Anonymous download of the rc1 asset verified byte-exact against the server digest.
- [x] ~~First US rc tag~~ **CUT 2026-09-07**: version axis 0.2.0 (aligned with carrier), `v0.2.0-rc1` prerelease live, six-point reconciliation green after two re-cuts (asset name, then zip shape - both fixed in release.yml, 0 downloads each time so the collaborator never saw a bad artifact). Release.yml's first real execution proved the whole chain end to end.
- [ ] **rc1 trial loop (collaborator testing, maintainer relays)**: bad rc = delete release + tag, re-cut same rc number from fixed head (carrier precedent); stable cut stays a deliberate decision. When the trial ends: tag `v0.2.0` stable, then main returns to `0.2.1-dev` for the next window.
- [x] ~~Cross-repo hash duty~~ **CLOSED by maintainer ruling 2026-09-07**: stop chasing hashes; existing history is never rewritten for them. Dangling citations are archaeology (MEMORY "Hash archaeology - CLOSED").
- [ ] Workshop display name and license (maintainer only; do not invent). First release prep: About icon/preview, final description, runbook US copy adaptation.

## Docs compression and cleanup (next block, after session compaction)

- [x] ~~Hash re-pointing of live docs~~ **CLOSED by the 2026-09-07 ruling**: no further hash maintenance anywhere; `OBLIVIONIS.md`/`docs/review/**`/live docs all keep whatever hashes they carry.
- [x] ~~Pre-existing hash rot in cold docs~~ **CLOSED by the same ruling**: archaeology, not damage; the mirror chain stays available but unused by default.
- [x] `docs/uikit-rebuild/**` Gate U constraint banners - landed 2026-09-07 (`02f5f5e`): README + 07 contract now carry dated corrections covering the "no clean cutover before Gate U" lines.
- [x] Nivarian pack README rewritten to the actual canonical pack (2026-09-07, dist/ is gitignored).
- [ ] Compaction style ruling for this repo's memory files: compress existing stale/verbose parts rather than appending more session-shaped prose (maintainer, 2026-09-07) - standing rule, applies to every future memory edit.

## Knife 3 - optional capability re-port (maintainer decision, OPEN)

- [ ] Re-implement natively in `UI/Kernel/`, each with a failure-sensitive geometry + interaction assertion driven by the real Host: sticky Tuning layer row; xenotype-row dimming at zero candidate packs; minor `HideBodyLabel` in the global volume widget; explicit `All` row inside the author dropdown. (Reverse help linkage was closed 2026-09-05 by the C+A landing.)
- [ ] Also decide: delete the remaining ~20-line pure-Verse camera-readout fallback (`Patch_GlobalControlsUtility_CameraIndicator.cs`) and make the overlay kernel-only.
- [ ] Alternative: drop those behaviours as legacy-anchored - then delete the matching help entries and backlog lines in the same commit.

## VoicePack routing table - content and fixture gaps (open)

- [ ] **Escape-hatch fixture**: nothing proves an author's custom comp patch wins over the default mount (all three final-test packs were de-patched 2026-09-02). Add one fixture pack whose patch deliberately differs (e.g. longer `Work` interval or a `Sustained` action); assert `attach_skipped reason=author_patch` plus the custom timing.
- [ ] **Author-side coverage fixture**: no pack declares `ageTag`, `isEgg` or `fallbacks`, so age-variant priority, egg gating, pack-fallback tier and the Remix four-tier branch (`HasPackFallback` in `SqueakPoolRegistry.Select`) are kernel-verified but never author-exercised. Add one fixture pack exercising all three and assert end to end.
- [ ] **Built-in content decision** (cannot be inferred from code): no `UniversalSqueakerFallbackProfileDef` / `UniversalSqueakerTuningBaselineDef` ships anywhere, so `BuildBuiltInSource()` is always empty - `Vanilla` is silent for every pawn, `Fallback`'s last tier is empty, `Remix`'s built-in ticket is always None, Presets can only show its empty state. Author per-race built-in profiles, or de-scope the built-in tier and rename the mode. Do not explain the silence away as mode design.
- [ ] **Brand-boundary ruling**: fixture `Ratkin-US-EXP` routes to race `Ratkin`; `Kiiro-US-EXP` keeps packageId/clip roots `coahuilite.squeakyratkin.*`. dist/ is gitignored test content, but AGENTS.md reserves those names - re-target to non-Ratkin third-party races or record the exception explicitly.
- [ ] **D8/F6 preset fixture**: retool `dist/voicepacks/final-test/Nivarian-US-EXP` to ship a `UniversalSqueakerTuningBaselineDef` so Presets is exercisable in game; parameter choice pending maintainer (recommend Work `intervalMultiplier` 0.15 + second preset Joy scope Disabled). Importer pre-check already landed (`TwoPresetsImportIndependentlyAndIdempotently`).

## In-game acceptance - still open (maintainer steps)

- [ ] Remaining from the 2026-09-06 rounds: Packs/Presets linkage, Distance chart hover+drag, Tuning cross-session file round-trip (explicit reload comparison), 58-entry Chinese help tone skim, composite-dropdown check (Tuning scope: picking Auto selects and closes, never opens the neighbour; near-bottom dropdown flips upward).
- [ ] With detailed logging in both languages, walk all five workspaces and confirm `usdiag evt=ui.text.overflow` stays silent (D9 was found this way; any new line is a fix target with an exact need/have pair).
- [ ] **D4 Packs domain-selection redesign** (maintainer six-point spec, 2026-09-05): race/xenotype domain lists side by side; xenotype list follows race selection; dropdowns move into their own cards; explicit clear option replaces the 全部 reset; independent search box per list (matching semantics need discussion first); visible list height = standing 4.5 rows. **F7**: search-matching discussion precedes implementation.
- [ ] **D7 mood-tuning rows**: display full names (音高/音量/抖动) for the three factor values + attach C+A help; the 自动 button's meaning to players needs a design answer.

## Text fit / localization - open decisions

- [ ] Review proposed Chinese wordings before any publication: routing modes (原版/回退/混音/禁用), distance presets (保守/均衡/强烈/自定义), filter labels (全部/仅启用/冲突/孤立/种族/异种/作者), card titles. Product vocabulary, not mechanical translation.
- [ ] Container-level auto-width deliberately not done (`UiLayoutEngine.ResolveColumnWidths` = static `Width=`/equal split; nav 192 / help 232 fixed). Revisit only if in-game logs show a genuinely too-narrow column after the band fixes.
- [ ] **Help key naming tightening** (batch rename, zero behavior change): role suffixes (.Title/.Overview/.Label/.Text) are schema noise; key = concept, body on the bare name, all help keys ≤4 segments, every reference stays a source literal. Maintainer decision: rename now or after the acceptance round (renaming changes shipped bytes and forces a re-test of the walked build).

## Crash-lineage risks (not layout; belongs with heap-corruption triage)

- [ ] `SqueakDiagnosticsPanel.DrawVisible` pairs `Widgets.BeginScrollView`/`EndScrollView` and restores `Text.Anchor`/`Text.Font`/`GUI.color` outside `try/finally`; `UiSessionGuard` restores GUI state only in its `catch` and then keeps drawing siblings. Both can leave the IMGUI group stack unbalanced after a single throw.

## Optional tail

- [ ] Runtime harness: adapter fold/converters/`BuildFallback` mode pass-through untested by the kernel gate (ReviewResolverFold P3 residual).
- [ ] HAR reflective discovery generalization - `Catalog/SqueakXenotypeCatalog.cs` `TODO(HAR)`, assembled-only (durable fact in MEMORY; do not claim reflection).

## Landed - pointer lines (detail in git log / MEMORY / OBLIVIONIS)

- Rebuild plan (approved 2026-08-23) + UI migration S0-S5 + orphan features: completed; baseline in `OBLIVIONIS.md`.
- 6-way review 2026-08-28: findings closed; reports in `docs/review/**` (era-faithful hashes).
- Old-UI three knives: Knife 1 cutover + Knife 2 verification closure DONE 2026-09-02 (`38b1247`); durable rules in MEMORY "UI engineering rules"; Knife 3 OPEN above.
- Text fit + localization landed 2026-09-02b (`4d80aad`); vacuous height axis closed 2026-09-03 (`8937044`+`bb6f0d3`); composite popup path closed 2026-09-04 (`ae4e77c` + lib `4dd97bf`); desync + band calibration 2026-09-04b (`e6b4f11` + lib `a05fddf`, ptrace half lib `72afa23`); filter clock fix 2026-09-04d (`3727e58`); filter labels localized 2026-09-04e (`d493989`). (Lib hashes re-pointed 2026-09-07: the carrier repo rewrote its own history after the split; the earlier citations `44b00c5`/`b2006a0`/`9a197a2` are unreachable there.)
- FerriteLib extraction ruled + executed 2026-09-03 (sibling repo `8e32620`; NuGet dropped on measurement; single-carrier red line asserted in gate 9 + check-pack-readiness + stage-package).
- Help presentation redesign CLOSED 2026-09-05 (`9ccdbe8`): C+A model + catalog fully keyed; D1-D3/D5/D6/D9/D10 fixed and in-game verified 2026-09-06 (`62c00cf`, `c067de1`; packages `c90d288`, `5206781`); help panel is a pure read surface (D2 ruling).
- First cloud upload §1-§5: all PASS (privacy recheck, three targeted rewrites + mirror chain, HEAD neutralization, `privacy-audit.ps1`, workflows from zero); repo pushed 2026-09-06 @ `fb61298` (219 commits, 0 tags). Ledger evidence: `.git/filter-repo/commit-map` + mirrors; durable decisions in MEMORY "First cloud upload".
