# TODO

> Action surface only. Landed history lives in git log, `OBLIVIONIS.md` (cold archive - including the
> pre-2026-09-21c byte-copy and the 2026-10-02 substantive summary) and the pointer lines at the end;
> durable engineering rules are in `MEMORY.md` and `AGENTS.md`. Standing ruling (maintainer 2026-09-07): at
> every memory edit **compress stale/verbose parts instead of appending session-shaped prose**.

## NOW — current queue (2026-10-05, BH1)

**BH1 now enters requested real-game acceptance.** Package identity is owned by its stamp and the PM paired
manifest. FL remains `0181268`, Demo `9d4b7e0`; only US needs the new package. Current human checklist:
`../modding_documents/relay_mod/PLAYTEST-BH1-20261005.md`.

- The user reaffirmed C1/C2 as normal, keeping the original IDs; verbatim feedback and runtime evidence are
  in `../modding_documents/relay_mod/PLAYTEST-FINAL-RESULTS-20261004.md`. Remaining coverage limits are in
  `../modding_documents/relay_mod/PM_HANDOFF.md`; they are distinct from human observations.
- [ ] **Remaining in-game observations of the landed UI rounds** - do not re-run or re-claim their
  technical passes, and do not repeat the V1/B3 observations the user already gave. All are US-only with no
  carrier change and a single FL carrier.
  - **RPT1 report-button feedback**: observe the adjacent refusal/wait/success sentence in a new package.
  - **XG1 empty xenotype target**: observe the unavailable message and that the layer can still be left.
  - **V2 Packs**: observe filter -> race/xenotype browse -> pack-enable order, the switch-shaped per-pack
    enable and the read-only scope line. Disclosed change: the filter band no longer draws the help-focus
    border (a Section + `us/section-header` now; no other Packs band had one). With BH1 open, the enable
    card starts 2.90 current viewports down: assess scrolling comfort and native wheel reachability.
  - **V3 Tuning**: observe layer/domain -> action scope -> mood order, the no-longer width-suppressed
    inherited-scope hint, and per-factor provenance with the preset shown only as a labelled reset target.
  - **V4 Distance/Presets/footer**: observe the camera-height axis wording, the read-only baseline note
    versus current Tuning values, and the footer keeping dirty/saving/failure plus the build identity.
  - **B3 help/settings coexistence**: observed usable at 1024x768 with the OLD side-column shape. BH1
    (2026-10-05) replaced that shape with the bottom band, so what is left to observe in game is the NEW
    one: the footer switch opening and closing the panel without moving or widening the window, the panel
    scrolling on its own while the settings keep their place, and the save-status line staying readable
    beside the switch.
- **BH1 bottom help implemented and locally verified; package/playtest remains open.** The 140px panel
  opens above the footer switch without changing window width; stable machine identifiers are retained.
  Contract and acceptance detail: `../modding_documents/relay_mod/BH1-BOTTOM-HELP-CONTRACT-20261005.md`
  and its `evidence/bh1-bottom-help-20261005/pm-result.json`. Synthetic save-state fit does not verify
  untriggered in-game saves; real fonts, pointer feel and independent wheel routing still need playtest.
- **Deferred (unchanged)**: tuning-layer redesign, composite splitting and further page-layout changes.
  A new team starting here reads `MEMORY.md` first and treats the deferred list below as the standing
  scope boundary.

Untriggered cases stay untested; a Release carrier is a refusal-control case, not the current rehearsal input.

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

- [ ] **D7 mood rows work surface** (the full text with file:line anchors is in `OBLIVIONIS.md`): no
  hand-written thresholds or fixed bands; help lands on the factor name itself; Auto semantics move to field
  level; **the editor's value chain is missing its author-baseline bottom layer** (the real Auto defect - the
  view model seeds from 1/1/One while the runtime seeds from the mounted comp's `Props.moodMods`); jitter is a
  lossy projection of an asymmetric range; the action-row Auto destroys two multipliers the UI never shows; the
  field-level clear is a spec amendment; the region shape is the maintainer's open fork; mood names are not
  localized; `MoodLayoutFocusedTests` must be re-derived from new geometry, never relaxed.
- [ ] **Text fit / localization decisions**: review the proposed Chinese wordings before publication (routing
  modes, distance presets, filter labels, card titles - product vocabulary, not mechanical translation); decide
  the help-key naming tightening (it changes shipped bytes and forces a re-test).
- [ ] **VoicePack routing-table content/fixture gaps**: escape-hatch fixture; author-side coverage fixture;
  **built-in content decision** (no fallback/baseline Def ships, so `Vanilla` is silent for every pawn and
  Presets can only show its empty state - author per-race profiles, or de-scope the tier and rename the mode;
  do not explain the silence away as design); brand-boundary ruling for fixture packs carrying Ratkin/SR names;
  the D8/F6 preset fixture.
- [ ] **Diagnostics product calls**: the collapsed bar is still as wide as the expanded panel - measure and
  shrink the width, or keep it and tighten the content; the expanded detail column shows blank space with
  nothing selected - decide the empty state; over-long author names overlap in the dropdown popup - single-line
  + ellipsis in the library (cross-repo) or rows that grow (moves the pinned popup-height lane).
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
- [ ] **D4 Packs domain-selection redesign** (maintainer six-point spec): race/xenotype lists side by side, the
  xenotype list follows race selection, dropdowns into their own cards, an explicit clear option, an independent
  search box per list, visible list height 4.5 rows. The search-matching discussion precedes implementation.
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
