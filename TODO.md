# TODO

> Action surface only. Landed history lives in git log, `OBLIVIONIS.md` (cold archive - including the
> pre-2026-09-21c byte-copy and the 2026-10-02 substantive summary) and the pointer lines at the end;
> durable engineering rules are in `MEMORY.md` and `AGENTS.md`. Standing ruling (maintainer 2026-09-07): at
> every memory edit **compress stale/verbose parts instead of appending session-shaped prose**.

## NOW — checkpoint acceptance (2026-10-07)

The approved checkpoint implements D4, DT1.US, Tuning/PRE1, VF1, DX1 and SA1-F02. Technical
status and paired package identities are authoritative in the PM checkpoint evidence and manifest:
`../modding_documents/relay_mod/evidence/bh1-bottom-help-20261005/ckpt-20261007/`.
The new candidate is separate from the retained BH1 and SA1 packages; no install or publication is implied.

- [ ] **Whole-checkpoint human tasks**, per `../modding_documents/relay_mod/PLAYTEST-CHECKPOINT-20261007.md`:
  select/search/enable a pack; tune and import/restore PRE1 A then B; edit the per-race final native
  fallback and confirm new-race admission after a full restart; use the compact US diagnostics and
  settings developer tools at 1024x768. Real fonts, native input, actual playback and game Scribe
  persistence remain human evidence, distinct from local checks and type-loading stubs.
- [ ] **PRE1 fixture acceptance:** use the Ratkin work copy outside US-testpack-archive, keep both
  originals untouched, and preserve current Config before import. Removing test Defs does not undo
  already imported settings. Restoring one field, one row and a preset must have distinguishable results.
- **Already accepted:** BH1 help usability, same-window position, real-font footer/long-help appearance,
  independent scrolling and accurate clicks; SA1 mixed-mode confirmation and independent outline.
  Do not repeat these as separate matrices or reinstate mandatory whitespace. Observe regressions only.
- **Deferred:** composite splitting, scroll-distance polish and unrelated backlog below. Tuning redesign,
  D4 page layout, final native fallback and US diagnostics ARE part of this checkpoint, not deferred work.
- Untriggered runtime cases stay untested. A Release carrier refusal control is not the current Dev
  rehearsal. Each lane states which named assertions have executed faithful proof and which are guards.

## Open decisions / blocked on the maintainer or on FL

- [ ] **Retire the short-lived branch `feat/help-drawer-visiblekey`** (its work is already in the `0.5.x` line):
  deletion needs **push authorization** - do not delete it locally either until then.
- [ ] **`container/tree` inline children / Route A** (give the tree an optional per-row template) - re-price
  before acting. The remaining blocker is hierarchy x inline composition; the maintainer leant this way but
  wants the existing components used FIRST. Do not take it on one data point.
- [ ] **Carried: the per-frame `GetComp` over every spawned pawn** (`Patch_MapInterface_DiagnosticsMarks`).
  Reducing it means a cache with invalidation or reusing the overlay's viewport sweep - a P2/P3 shape decision.
- [ ] **Carried: FL's chrome-key runtime self-check.** US attaches no `UiWindowKey`, so every multi-instance
  window's chrome scope identity is type-only; implementing it means adopting `UiWindowCatalog` + key attachment
  on the very windows P2 is retiring.
- [ ] **Carried: the legacy audit channel boundary.** `UsTextFitAudit` is per-host, but the process-wide
  `UiFitAudit.Enabled` is still shared and `Detach`/the legacy sink still exist. The misattribution was **never
  reproduced** - keep it an open condition, never "pollution already happened".
- [ ] **Hot reload: demand-driven first.** Decide which need is real - XML layout-document hot reload, or non-UI
  writers of settings/model state - before adopting `UiDocumentService` + `HostAttached`.
- [ ] **(甲) the global density (12/8/4/24/1)**: moves every control's INNER inset, so it needs its own slice
  and its own in-game look. Spec §3 records why it was separated from S3.
- [ ] **Row fill / row hover in the layer cards.** Two routes measured blocked (the hit painting itself moves the
  band's geometry; an `input/button` sibling throws or becomes a second hit surface). The third - **a CONTAINER
  state sibling with its own flat scope** - is a cited candidate that has NEVER been tried.
- [ ] **The 3px selected rail.** Needs drawing code today. **WAITS ON FL's per-edge stroke**, then re-price;
  when those land, `us/selection-surface` is the thing to retire.
- [ ] **B8's vocabulary half**: the `Description1..8` LABEL-SET half was fixed FL-side; the schema half (removal
  vs drawing) is the maintainer's call.
- [ ] **Chrome semantics** (deferred to a dedicated session): `UiWindowHost.DoWindowContents` is a sealed
  override, so a consumer cannot place anything in the chrome band; the consequence in this tree is that the
  Help toggle sits in the in-page title band. Any FL addition bumps `Api.Minor` and moves US's pin in the same
  cross-repo round.
- [ ] **Knife 3 - optional capability re-port** (maintainer decision): sticky Tuning layer row; xenotype-row
  dimming at zero candidate packs; minor `HideBodyLabel`; an explicit `All` row in the author dropdown - each
  with a failure-sensitive geometry + interaction assertion driven by the real Host. Alternative: drop them as
  legacy-anchored, deleting the matching help entries and backlog lines in the same commit.
- [ ] **Cross-repo release alignment**: US pins `[0.7.0, 0.8.0)` and both CI workflows rebuild the carrier from
  FL's default branch, so CI tracks FL `main` rather than a release; the lib-side publish is the maintainer's
  call.
- [ ] **rc trial loop**: a bad rc is fixed by deleting release + tag and re-cutting the SAME rc number;
  `/releases/latest` 404ing is the correct rc-window state. The stable cut and the next product axis are
  maintainer decisions.
- [ ] **The generated release body does not name the carrier release it was built against** - close it in the
  release-body step of `.github/workflows/release.yml`.
- [ ] **Workshop display name and license** (maintainer only; do not invent) + first-release prep: About
  icon/preview, final description, runbook US copy adaptation.
- [ ] **`US_STEAM` is a code axis with no build axis** (recorded, not a bug): `#if US_STEAM` gates code but no
  csproj/workflow/script defines the symbol. Do not debug the dead branch.
- [ ] **Cross-repo requests - Batch 2, non-blocking, do NOT file yet.** Classify (A) US's own job / (B) a
  genuinely general FL gap **before** asking; the single ledger is the FL issues file under the workspace's
  team-mode directory - reference it, never copy it into this file.
- [ ] **Split ownership confirmations**: `us/preset-list` STAYS a US kind (FL declined inline tree children);
  the orphan-name check is not a request - US is the consumer-side positive control.

## Deferred / backlog (durable detail in `MEMORY.md` or `OBLIVIONIS.md`; not this round)

- [ ] **Mood follow-ups beyond the checkpoint:** re-evaluate asymmetric jitter representation and
  author-baseline presentation against current production code before proposing another slice. Archive-era
  claims do not establish a current defect or gate. The current multiplier editor, per-field inheritance,
  resolved-label measurement and redesigned geometry belong to this checkpoint.
- [ ] **Text fit / localization decisions**: review the proposed Chinese wordings before publication (routing
  modes, distance presets, filter labels, card titles - product vocabulary, not mechanical translation); decide
  the help-key naming tightening (it changes shipped bytes and forces a re-test).
- [ ] **Further VoicePack author fixtures:** escape-hatch and author-side coverage remain deferred.
  The checkpoint ships neutral Ratkin final-fallback data with Core audio references; PRE1 uses a separate
  work-copy fixture and its human acceptance is owned by NOW, not an absent-content design decision.
- [ ] **Diagnostics follow-ups beyond DX1:** re-price author-name dropdown overflow only on a current
  reproduction. Collapsed/expanded/empty geometry and compact navigation are implemented in DX1 and
  await the whole-checkpoint human task.
- [ ] **0.5.x items A/B/C** (`MEMORY.md` "Release state and version scope"): (A) file-driven invocation + hot
  reload; (B) appearance file-driven + tier-2 granularity actually used; (C) structural migration.
- [ ] **Route A re-price** and **optional tail**: the runtime harness does not cover the adapter
  fold/converters/`BuildFallback` pass-through; HAR reflective discovery is `TODO(HAR)` and assembled-only -
  never claim reflection; the two 2px band floors (`checklist`, `diag-list`: 20 vs the calibrated 21.33 Small
  line); a FL lane for the single-line RENDERING (the carrier stub records rect/text/colour but not wrap state);
  drop the nav subtitle line entirely to reclaim ~100px of stack height if the compact cards still read tall.
- [ ] **`ui.text.overflow` walk**: with detailed logging in both languages, walk all five workspaces and confirm
  `usdiag evt=ui.text.overflow` stays silent (any new line is a fix target with an exact need/have pair).
- [ ] **Remaining custom-page row shapes** belong to V2-V4. Overview already has local responsive parameter
  rows; do not revive the old proposal to raise the shared body breakpoint just to fix Overview.
- [ ] **D4/DX1 human acceptance:** part of NOW's whole-checkpoint tasks. D4's VisibleRows4.5 viewport
  follows actual measured rows in the same pass, without a fixed 189px floor. DX1's real-shell state-change
  guard checks the whole window position; live fonts, world data and pointer behavior remain human checks.
- [ ] **MeowingKiiro skill-flow validation** (pack built, static green): the in-game pass - enable order,
  Kiiro-Race-domain tick, Fallback, Call/Select + spot-check, four modes, dispatch log - and record any
  skill-vs-implementation mismatch back into the production plan doc and the skill. Publication stays
  maintainer-gated.
- [ ] **PackFallback first in-game observation**: one VoicePack missing the tested action's sound set but
  declaring fallbacks - the panel must show the pack-fallback attribution on dispatch.
- [ ] **Eat-granularity manual acceptance matrix**: meal / smokeleaf / go-juice / beer / ambrosia / nutrient
  paste / inventory / animal / corpse × (parent-off, parent-on+child-off, parent-on+child-on); parent-off must
  feel byte-identical to today.
- [ ] **Eat follow-ups filed by the independent passes** (all non-blocking): the disabled child checkbox still
  paints in the enabled ink; with the parent on the reserved reason band is an empty strip (inherent to the
  constant-sum rule); the migration test has no assertion for the two new fields; the child help states the
  real fallback but the flag stays process-level and one-way; the eat child row's rule is mislabelled in
  non-shared-row failure messages.
- [ ] **Diagnostics live-walkthrough** for the rebuilt panel: master-detail + locked detach windows, the
  collapsed bars, the 16-line chain, four-tier dispatch labels, and the **unconsumed two-press Esc** - a leak
  there is FL event-seam material, never a whitelist entry.
- [ ] **Attention palette + diagnostics state/layout** (harness-verified only): attention cyan legibility at
  real UIScale in both languages, grayscale distinctness of the states, the 592px split and the narrow Back
  restoring the SAME search/page/scroll, group folding that does not move when values update, the
  previous-evaluation band's recency wording, the pinned window's lifecycle plus the two-press Esc leak, and the
  checklist's role-split banners against the settings canary.

## In-game acceptance — still open (maintainer steps)

- [ ] **Settings-window acceptance pass** (blocks a release claim, not the commit): open/close Help at the
  actual supported resolution; 736/480 page widths are harness probes, not forced game-resolution
  instructions. **BH1 changed what this observes**: the window has ONE width per screen (2560x1440 opens
  1280x960, 4:3) and stays centred and on screen while help opens and closes - the panel is a 140px band
  above the footer, so nothing widens, shifts or replaces the settings. Observe: the footer switch executing
  on a real click, the panel scrolling on its own while the settings keep their place, the status text and
  the switch readable on one row in both languages, nav card compactness, the Playback help entries, the mood
  cards' readability, and a session save/reopen. The old side-column/drawer wording is retired: the only
  remaining shape question is what the player sees in game.
- [ ] **`ui.text.overflow` and tab-switch smoke** (F-05/F-07) as part of the ONE collected pass above.
- [ ] **Early-round remnants still unverified**: Packs/Presets linkage, Distance chart hover+drag, Tuning
  cross-session file round-trip, the Chinese help tone skim, and the composite-dropdown check (picking Auto
  selects and closes; a near-bottom dropdown flips upward).

## Landed — pointer lines (detail in git log / `MEMORY.md` / `OBLIVIONIS.md`)

- **2026-10-04 — HM1 memory/handoff round**: `MEMORY.md` and this file compacted; the final user feedback
  recorded at its own altitude; no product, build or package change (the package stays `1a54e1f`).
- **2026-10-04 — RPT1 / XG1**: report-button result sentence and the empty-xenotype tuning block (inert
  controls, live layer selector, identity guard before Def lookup) - technical passes only; in-game clarity and
  disabled feel stay open.
- **2026-10-02 — DIAG-FIX**: repeated report requests wait for their own pass; the lane now drives the
  production settings source; Demo logs the full retained report once per success under its own prefix.
- **2026-10-02 — V1 implementation checkpoint**: nav surfaces, flatter Section edges, Overview spacing,
  independent nav scrolling and responsive parameter rows.
- **2026-09-30 — R4-A / R4-B**: typed action-scope choice (the `ToString`/`Enum.TryParse` round-trip is gone)
  and the preset subtree composed on FL's shared row band with native xenotype icons.
- **R3-B / R12-US**: the per-host geometry instrument and the complete owned DarkGold baseline; durable facts in
  `MEMORY.md`.
- **S3 / S4 / S6 and everything earlier (2026-08-23 → 2026-09-28)**: landed; the durable half is in `MEMORY.md`
  and the finished narratives are archived in `OBLIVIONIS.md`.
