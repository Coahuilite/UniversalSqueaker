# TODO

> Action surface only. Landed history lives in git log, `OBLIVIONIS.md` (cold archive — including the
> byte-copy of this file's pre-2026-09-21c content) and the pointer lines at the end; durable engineering
> rules are in `MEMORY.md` and `AGENTS.md`. Standing ruling (maintainer 2026-09-07): at every memory edit
> **compress stale/verbose parts instead of appending session-shaped prose** (this file was compacted under
> that ruling on 2026-09-21c).

## NOW — the handover queue (2026-09-22; read `MEMORY.md` "Handover — start here" first)

Ordered by what unblocks what. Each item's detail lives in the section it names.

- [ ] **1. The Packs workspace's race row does not respond to a click** (the filter does). `Player.log` shows
  no exception, no TRIPPED/recovery band and no `KeyNotFound` ⇒ a missing command binding is ruled OUT. Four
  candidates, NONE of them measured: `Chrome="none"` short-circuiting the hit, the `Overlay`'s paint order, a
  `Scroll` consuming the press, the `MatchContent` band's real rect. **The carrier's dev geometry dump is the
  instrument that answers it** (`MEMORY.md` "Handover — start here", item 2). **WAITS ON THE MAINTAINER**: the
  dump exists only in a dev carrier.
- [ ] **2. The dev round trip, in one sitting** (same trigger as item 1): the carrier repo builds Dev → US
  `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev` → test → the carrier owner's delivery
  step back to Release (PDB removed, freeze notice re-issued). Nothing on the US side is missing; the wiring is
  committed and compile-verified.
- [ ] **3. Claims carried as claims** (each has its own line in §S6 and in `MEMORY.md`): the square toggle's
  press path and hover lane; the 24 → 36 widening's lane re-cut at the 320 shape; row fill / row hover (the
  CONTAINER-sibling candidate is untried); the 3px selected rail. **The rail WAITS ON FL**; the rest need a real
  screen.
- [ ] **4. The one style debt at the S4 finish line**: `us/diagnostics` is a C#-drawn composite, so the flat
  scope cannot reach it and the Overview page keeps one boxed card. S4-shaped work, not a tweak.



## S4 — per-workspace atomisation (CLOSED 2026-09-22; the 0.5.x main goal met)

**All four slices landed, each migrating a workspace AND retiring the kinds it replaced: S4-1 Overview,
S4-2 Packs, S4-3a trigger timing, S4-3b attenuation. The US kind set went 18 -> 15 -> 13 -> 12 -> 11, five
composites and the seven `toggle-*` action bindings were deleted with them, and the whole grade chain ran
end to end green.** Friction reports, player-visible deltas and mutation ledgers: `docs/ui-redesign-0.7-zh.md`
§5.8-§5.10 plus the S4 bullets in `MEMORY.md`. The two carrier gaps found on the way were FIXED rather than
worked around (per-item `SelectedKey`; `Height="MatchContent"` replacing the two 24px hit bands, measured
100% row coverage). **Nothing below is a re-do.**

  - [ ] **The selected row's FILL: two routes tried 2026-09-22, both blocked; the 3px rail still open
    (spec §5.9.6).** Not "cannot be expressed" any more, but not delivered either: (a) the TEXT cannot carry it
    (`text/wrapped` paints no surface); (b) letting the HIT paint itself works (idle `#191612`, selected
    `#3A311F`, hit height still == the text column's) but moves the hit band's geometry and reddens
    `DeclarativePacksLaneTests` at 320px, so it is refused; (c) an `input/button` SIBLING keeps the geometry
    but cannot exist - the atom invokes its command on every click, so without `ActionBind` it throws at the
    first press and with one it is a second hit surface. **Untried candidate with this round as its citation: a
    CONTAINER state sibling with its own flat scope.** The **left 3px rail** is separate and needs drawing code
    (a vertical rail is not declarable). Debt boundary: visible but not cheaply fixable - record + cite, and the
    row fill/hover sit on the REAL-SCREEN list; do not re-open unasked.
- [ ] **Route A (give `container/tree` an optional per-row template) — re-price before acting.** WAVE-1's G1-G5
  gaps are all closed (spec §0.4), so "layers stay composite because G2" no longer holds; the remaining blocker
  is hierarchy x composition (2 widgets / 933 code lines, spec §5.1). The maintainer leant Route A but wants the
  existing components used FIRST — do not take it on one data point.
- [ ] **Carried: the per-frame `GetComp` over every spawned pawn** (`Patch_MapInterface_DiagnosticsMarks`).
  Reducing it means either a cache with invalidation or reusing the overlay's viewport sweep — a P2/P3 shape
  decision, not a seam fix.
- [ ] **Carried: FL `a306cae`'s chrome-key runtime self-check.** US attaches no `UiWindowKey`, so every
  multi-instance window's chrome scope identity is type-only (`SqueakDiagnosticsDetailWindow/chrome` for every
  open detail window); implementing it means adopting `UiWindowCatalog` + key attachment on the very windows
  P2 is retiring.
- [ ] **Carried: the legacy audit channel boundary.** `UsTextFitAudit` is per-host now (FL-20), but the
  process-wide `UiFitAudit.Enabled` is still shared and `Detach`/the legacy sink still exist. The actual
  misattribution was **never reproduced** — keep it recorded as an open condition, never as "pollution already
  happened".
- [ ] **Invalidation discipline, made structural (adoption plan §9 item 6 / P3-2a)**: `IUiBindings` exposes no
  full write-key enumeration, so register the write bindings centrally and emit the test metadata from that
  registry (the UI-logic harness hand-lists **20 of 46** write keys today). Land it BEFORE the migration deepens.
- [ ] **Hot reload: demand-driven first.** Decide which need is real — XML layout-document hot reload, or non-UI
  writers of settings/model state — before adopting `UiDocumentService` + `HostAttached`.
  - [ ] **The selected row's FILL: two routes tried 2026-09-22, both blocked; the 3px rail still open
    (spec §5.9.6).** Not "cannot be expressed" any more, but not delivered either: (a) the TEXT cannot carry it
    (`text/wrapped` paints no surface); (b) letting the HIT paint itself works (idle `#191612`, selected
    `#3A311F`, hit height still == the text column's) but moves the hit band's geometry and reddens
    `DeclarativePacksLaneTests` at 320px, so it is refused; (c) an `input/button` SIBLING keeps the geometry
    but cannot exist - the atom invokes its command on every click, so without `ActionBind` it throws at the
    first press and with one it is a second hit surface. **Untried candidate with this round as its citation: a
    CONTAINER state sibling with its own flat scope.** The **left 3px rail** is separate and needs drawing code
    (a vertical rail is not declarable). Debt boundary: visible but not cheaply fixable - record + cite, and the
    row fill/hover sit on the REAL-SCREEN list; do not re-open unasked.
- [ ] **Route A (give `container/tree` an optional per-row template) — re-price before acting.** WAVE-1's G1-G5
  gaps are all closed (spec §0.4), so "layers stay composite because G2" no longer holds; the remaining blocker
  is hierarchy x composition (2 widgets / 933 code lines, spec §5.1). The maintainer leant Route A but wants the
  existing components used FIRST — do not take it on one data point.
- [ ] **Carried: the per-frame `GetComp` over every spawned pawn** (`Patch_MapInterface_DiagnosticsMarks`).
  Reducing it means either a cache with invalidation or reusing the overlay's viewport sweep — a P2/P3 shape
  decision, not a seam fix.
- [ ] **Carried: FL `a306cae`'s chrome-key runtime self-check.** US attaches no `UiWindowKey`, so every
  multi-instance window's chrome scope identity is type-only (`SqueakDiagnosticsDetailWindow/chrome` for every
  open detail window); implementing it means adopting `UiWindowCatalog` + key attachment on the very windows
  P2 is retiring.
- [ ] **Carried: the legacy audit channel boundary.** `UsTextFitAudit` is per-host now (FL-20), but the
  process-wide `UiFitAudit.Enabled` is still shared and `Detach`/the legacy sink still exist. The actual
  misattribution was **never reproduced** — keep it recorded as an open condition, never as "pollution already
  happened".
- [ ] **Invalidation discipline, made structural (adoption plan §9 item 6 / P3-2a)**: `IUiBindings` exposes no
  full write-key enumeration, so register the write bindings centrally and emit the test metadata from that
  registry (the UI-logic harness hand-lists **20 of 46** write keys today). Land it BEFORE the migration deepens.
- [ ] **Hot reload: demand-driven first.** Decide which need is real — XML layout-document hot reload, or non-UI
  writers of settings/model state — before adopting `UiDocumentService` + `HostAttached`.

## S6 — align US with SR's visual language and wording (landed 2026-09-22; one style debt left)

**Landed:** S6-1 wording (two rounds, EN+ZH, against the SR benchmark); S6-2 the nav column flat and the
eight declarative cards de-boxed with the five body separators deleted; S6-3 the warm palette (guard first),
the section header's 3px gold rail, and the square toggle as `us/square-toggle` (Registrar 11 -> 12 -> 13).
**The toggle follow-up landed last:** all seven bands went 24 -> 36 wide so the widget renders the
reference's own 34x18 (knob throw 6 -> 16px), the ON edge stays the neutral `Border` both states paint, and
the ON knob is the accent itself.

**Rulings that are decisions, not backlog:** help stays the primary channel and there is NO per-row `?`
button (that would be a second help mechanism); the square toggle is an APPEARANCE variant, not a new control
(four-ruler ruling + F-02); the OFF knob's ink stays `TextSecondary`, because the disabled branch already
owns `TextDisabled` and reusing it would collapse "read-only ON" into "clickable OFF".

- [ ] **The one style debt left: `us/diagnostics` paints its own chrome in C#**, so neither the manifest nor
  the flat scope reaches it and the Overview page keeps one boxed card among flat ones. Closing it is
  S4-shaped work (dissolve the composite), not a style tweak.
- [ ] **Row fill / row hover in the layer cards.** Two routes measured blocked (the hit painting itself moves
  the band's geometry; an `input/button` sibling throws or becomes a second hit surface). The third —
  **a CONTAINER state sibling with its own flat scope** — is a cited candidate that has NEVER been tried.
- [ ] **The 3px selected rail.** Needs drawing code today. **WAITS ON FL's per-edge stroke (task-11)**, then
  re-price.
- [ ] **The unified style rule** (borderless, whitespace-separated, state by fill) now holds on the
  declarative surfaces; the composite survivors above are the remaining work.

## Narrow help (乙1) — the shipped shape and its one known limitation

At a logical width that cannot host the widened window the open drawer **replaces the body** (host-derived
`body-visible`; `help-band` is `Fill="true"` and takes the whole slot). Durable facts: `MEMORY.md` "Current
state". The engine vocabulary has **no reserved band** (no container `MinHeight`/`MaxHeight`, no fill weight,
no `HeightKey`).

- [ ] **Known limitation + the follow-up fix: the body cannot SHRINK, so height sharing is not expressible.**
  `nav-column` is a plain `Column` outside any `Scroll`, so its content (`us/nav`, 271px natural) can be
  neither shortened nor scrolled; a body slot smaller than that overflows into whatever sits below it — exactly
  what the retired "band shares the page" shape did at 1024x768 (nav 271px inside a 122px slot, measured).
  **Fix = put the nav column inside a `Scroll`**, which makes the body shrink-safe; only then can a
  shared-height band be revisited (and the space-budget assertion relaxed from "the band takes at least the
  whole body content floor").
- [ ] **(甲) the global density (12/8/4/24/1) — deliberately NOT inside S3.** It moves every control's INNER
  inset (the atoms read `theme.Geometry.Padding` as their own inset), so it needs its own slice and its own
  in-game look; spec §3 records why it was separated.

## The one collected in-game pass (maintainer; do NOT run these scattered)

- [ ] **Single session, in this order**: (1) the new skeleton — the header does not scroll, the title band, the
  Help switch staying put, the three columns, the footer band, and the narrow (stacked) presentation;
  (2) the five player-visible deltas of the checklist migration: row hit target narrowed to the checkbox band,
  row surface/hover/selected rail gone, the orphan band above the list, meta/coverage at the atom's font,
  placeholder ink `TextSecondary`; (2b) **the S4-1 Overview deltas** (five, all "needs a real screen", spec
  §5.8): every declared row is ~9px taller (the egg row ~16px), the row label moved 10px left and is
  TOP-anchored instead of vertically centred, the whole-row hit target plus the row hover/surface rail are gone
  (the 24x30 checkbox band is the only target), the row separator is now a `chrome/rule` in **Border** ink
  (brighter than the shipped Divider), the checkbox's 18px box sits 4px further right and no longer lines up
  with the segmented control's shared column, and the egg-state / global-volume captions moved from Tiny to the
  atom's Small font (the volume caption also from MiddleLeft to UpperLeft). Also the first in-game use of
  `input/slider` and `input/number-field` on this page — check the drag feel and the field's focus/commit;
  (2c) **the S4-2 Packs deltas** (six, all "needs a real screen", spec §5.9): the **whole-row hit target** is
  now one `MatchContent` band covering the row in full (100% measured at every accepted width; the retired
  2x24px stack's 69.9% / 53.3% and its 42px dead zone are gone) — what still needs a real screen is whether the
  band's hover/armed paint reads right with the text column drawn over it; **selection in the layer cards is now HALF restored** — the selected row's
  title ink is back (gold, as shipped), but the row's fill and its left 3px rail are still missing (§5.9.6; the
  fill's two tried routes and the untried container-sibling candidate are recorded there and in MEMORY);
  row surface/hover gone; rows +20.67px taller (cards 266
  and 124.67px); the detail line moved Tiny -> Small and both lines sit 10px further left, top-anchored;
  (3) **the narrow help band (乙1) as shipped** — it REPLACES the body (the nav
  and the centre column are not drawn at all), fills header-to-footer, scrolls internally, never pushes the
  footer out, and closing the drawer brings the body back **with its scroll position intact**; the 7 EN / 2 ZH
  overflow findings must stay gone; (4) **F-05**: is `ui.text.overflow` zero on the normal path? (5) **F-07**:
  switching tabs throws nothing; (6) **the width axis** — the harness measures translation KEYS, so confirm real
  translations land inside the single-line rects; (7) the Help switch is a core `input/button` now and its look
  has never been seen; (8) the title band no longer reserves 72px for a control and the header band's height is
  a real vertical cost (content-measured, so it follows the worst workspace caption); (9) the nav cards are 40px
  wider (144 → 184).
- [ ] **The seven square toggles at their new band (S6-3 follow-up)**: 36 wide, so the track renders 34x18
  with a 16px knob throw, one neutral `Border` edge in both states, the accent on the ON knob. Needs a real
  screen: does the ON state read at a glance, and does the wider control still fit the egg row at the narrow
  tier?
- [ ] **Packaging pre-flight**: the dev package is a **folder** — drop it in `Mods/`, no zip; read the package
  `version.txt` `carrier=` line before entering the game and confirm it is a `Release` carrier (on mismatch
  rebuild per the stager's refusal message).

## In-game acceptance — still open (maintainer steps)

- [ ] **2026-09-06 round remnants**: Packs/Presets linkage, Distance chart hover+drag, Tuning cross-session file
  round-trip (explicit reload comparison), Chinese help tone skim, composite-dropdown check (Tuning scope:
  picking Auto selects and closes, never opens the neighbour; a near-bottom dropdown flips upward).
- [ ] **2026-09-07 rounds' in-game smoke** (both rounds are pass-through or shell-owned, so the expectation is
  "no behaviour change"): five workspaces, one dropdown, the camera readout, help-panel hover linkage (D10: no
  overview flash between adjacent controls, release after ~15 passes), close-button hover/click, real-screen
  window size. Two deliberate deltas to look for: the chrome's title/subtitle now carry `singleLine: true`, so
  an over-wide localized title reports `ui.text.overflow` on the **width** axis (shell band, not a US
  regression); and switching workspace no longer clears the help-hover claim instantly - the session's grace
  window releases it, so a `scroll-to` triggered away from the nav column can keep the previous explanation for
  up to ~0.25 s.
- [ ] **`ui.text.overflow` walk**: with detailed logging in both languages, walk all five workspaces and confirm
  `usdiag evt=ui.text.overflow` stays silent (any new line is a fix target with an exact need/have pair).
- [ ] **Attention palette + diagnostics state/layout** (`89499b0`/`0254d85`/`f8316d3`, harness-verified only):
  attention cyan legibility at real UIScale in both languages, grayscale distinctness of
  current-object/attention/pass/pending/N-A, the 592px split and the narrow Back restoring the SAME search, page
  and scroll, group folding that does not move when values update, the previous-evaluation band's recency
  wording, the pinned window's lifecycle plus the two-press Esc leak, and the checklist's role-split banners
  (conflict/target-unavailable cyan, dormant hatch) against the settings canary.
- [ ] **Settings-window acceptance pass** (blocks a release claim, not the commit): open/close Help at
  1024/736/480/320 in EN+ZH; the window opens narrow and widens by 332 (the 320 help column + the 12px row gap)
  when Help expands — 2560x1440 opens 1280x960 (4:3) — stays centred and on screen; nav card compactness, the
  four Playback help entries, the mood cards' readability, and a session save/reopen. Also the declarative
  drawer: open/close, scroll position preserved across close/reopen, and the real in-game window width.
- [ ] **Decide the narrow support-row shape**: rows grow for a wrapped translated label at 736-open /
  736-closed-EN / 480-EN / 320 (up to +338.67px at 320; growth is machine-checked as
  `grown <=> label width > label band`). Options: raise `body-row` `Breakpoint` 500 → ~760 so 736 stacks, or
  add a stacked narrow-row variant. Product call.
- [ ] **D4 Packs domain-selection redesign** (maintainer six-point spec): race/xenotype domain lists side by
  side, the xenotype list follows race selection, dropdowns into their own cards, an explicit clear option
  replaces the 全部 reset, an independent search box per list, visible list height = 4.5 rows. **F7**: the
  search-matching discussion precedes implementation.
- [ ] **MeowingKiiro skill-flow validation** (pack built, static green): the in-game pass per skill §9 (enable
  order, Kiiro-Race-domain tick, Fallback, Call/Select + spot-check, four modes, dispatch log) — record results
  and any skill-vs-implementation mismatch back into `docs/voicepack-meowingkiiro-production-plan-zh.md` and
  the skill. Publication stays maintainer-gated; the About `<description>` waits on Saryaki's own text.
- [ ] **PackFallback first in-game observation** (fold into the diagnostics acceptance build): one VoicePack
  missing the tested action's sound set but declaring `<fallbacks>` — the panel must show
  `[Pack fallback·<pack key>] : <sound>` on dispatch.
- [ ] **Eat-granularity manual acceptance matrix** (handoff §6): meal / smokeleaf / go-juice / beer / ambrosia /
  nutrient paste / inventory / animal / corpse × (parent-off, parent-on+child-off, parent-on+child-on);
  parent-off must feel byte-identical to today; beer/ambrosia still fire at parent-on+child-off (accepted).
- [ ] **Eat follow-ups filed by the independent passes** (all non-blocking): (S3) the disabled child checkbox
  still paints in the enabled ink, so it reads as clickable; (S4) with the parent on, the reserved reason band is
  an empty strip (inherent to the constant-sum rule, documented for testers); (N2)
  `UniversalSqueakerSettingsMigrationTests` has no assertion for the two new fields ("a default config writes no
  `eatPrecision*` node", "parent-off + child-true normalises to false"); (S1) the child help states the real
  fallback, but the flag stays process-level and one-way; (S2) the eat child row's rule is labelled
  `[egg-stack floor 52]` in every non-shared-row failure message — pass the rule name per row.
- [ ] **Diagnostics live-walkthrough** for the rebuilt panel (checklist in `OBLIVIONIS.md` §1): master-detail +
  locked detach windows, the collapsed bars, the 16-line chain (white N/A, G4 breakdown, remaining/effective
  total), four-tier dispatch labels, and the **unconsumed two-press Esc** — a leak there is FL round-4 event-seam
  material, never a whitelist entry.

## Open decisions / blocked on the maintainer or on FL

- [ ] **Retire the short-lived branch `feat/help-drawer-visiblekey`** (its work is already in the `0.5.x`
  line): deletion needs **push authorization** — do not delete it locally either until then.
- [ ] **B8's vocabulary half**: the `Description1..8` LABEL-SET half is a real defect fixed FL-side; the schema
  half (removal vs drawing) is the maintainer's call. It blocks the segmented-control presentation
  (`us/diagnostics` 分段行).
- [ ] **Chrome semantics — deferred to a dedicated session**: `UiWindowHost.DoWindowContents` is a sealed
  override, so a consumer can set only identity/size strings and cannot place anything in the chrome band; the
  consequence in this tree is that the Help toggle sits in the in-page title band. The aligned shape is the
  chrome band as a **declared page region**; any FL addition bumps `Api.Minor` and moves US's pin in the same
  cross-repo round.
- [ ] **Knife 3 — optional capability re-port** (maintainer decision): sticky Tuning layer row; xenotype-row
  dimming at zero candidate packs; minor `HideBodyLabel` in the global-volume widget; an explicit `All` row in
  the author dropdown — each natively in `UI/Kernel/` with a failure-sensitive geometry + interaction assertion
  driven by the real Host. Also decide: delete the remaining ~20-line pure-Verse camera-readout fallback and make
  the overlay kernel-only. Alternative: drop them as legacy-anchored, deleting the matching help entries and
  backlog lines in the same commit.
- [ ] **Cross-repo release alignment**: US pins `[0.7.0, 0.8.0)` and both CI workflows rebuild the carrier from
  FL's **default branch**, so CI tracks FL `main` rather than a release; the lib-side publish of the carrier's
  line is the maintainer's call, and US's rc pairing plus the release body's carrier line follow it.
- [ ] **rc trial loop** (collaborator testing, maintainer relays): a bad rc is fixed by deleting release + tag
  and re-cutting the SAME rc number; `/releases/latest` 404ing is the correct rc-window state. The stable cut
  and the next product axis are maintainer decisions.
- [ ] **The generated release body does not name the carrier release it was built against** — close it in the
  release-body step of `.github/workflows/release.yml` (the published body only catches up when a later tag is
  cut).
- [ ] **Workshop display name and license** (maintainer only; do not invent) + first-release prep: About
  icon/preview, final description, runbook US copy adaptation.
- [ ] **`US_STEAM` is a code axis with no build axis** (recorded, not a bug): `#if US_STEAM` gates
  `SqueakLog.cs` and `Mod.cs`, and no csproj configuration, workflow or script defines the symbol, so the
  channel is unreachable except by hand-passing a define. Do not debug the dead branch; a future Workshop channel
  goes through the same staging engine (`-BuildFlavor` gains a value, `read-assembly-stamp.ps1` measures it).
- [ ] **Cross-repo requests — Batch 2, non-blocking, do NOT file yet**: `WideHidden` (B2(2), mirror of
  `NarrowHidden`), `WidthKey` (B5), the chrome action slot (B6, dropped for now by the header ruling), text
  alignment (B10). `input/text-field` (B7) is approved and being built by FL now — **US adoption is a LATER
  round; do not adopt it this round**. File only what a real adoption actually hits, and classify
  (A) US's own job / (B) a genuinely general FL gap **before** asking; the single ledger is
  `../modding_documents/team-mode/fl-uikit-issues-zh.md` — reference it, never copy it into this file.
- [ ] **Split ownership confirmations**: `us/preset-list` STAYS a US kind (FL declined inline tree children);
  B11 (the orphan-name check) is not a request — US is the consumer-side positive control.

## Deferred / backlog (durable detail in `MEMORY.md` or `OBLIVIONIS.md`; not this round)

- [ ] **D7 mood rows work surface** (eleven open items; the full text with file:line anchors is in
  `OBLIVIONIS.md` "Memory compaction 2026-09-21c"): no hand-written thresholds or fixed bands; help lands on
  the factor name itself; Auto semantics move to field level; **the editor's value chain is missing its
  author-baseline bottom layer** (the real Auto defect — the view model seeds from 1/1/One while the runtime
  seeds from the mounted comp's `Props.moodMods`); jitter is a lossy projection of an asymmetric range; the
  action-row Auto destroys two multipliers the UI never shows; the field-level clear is a **spec amendment**; the
  region shape is the maintainer's open fork; mood names are not localized; `MoodLayoutFocusedTests` must be
  re-derived from the new geometry, never relaxed.
- [ ] **Text fit / localization decisions**: review the proposed Chinese wordings before publication (routing
  modes 原版/回退/混音/禁用, distance presets, filter labels, card titles — product vocabulary, not mechanical
  translation); decide the help-key naming tightening (role suffixes are schema noise; rename now or after the
  acceptance round — it changes shipped bytes and forces a re-test).
- [ ] **VoicePack routing-table content/fixture gaps**: escape-hatch fixture (an author patch must beat the
  default mount, asserting `attach_skipped reason=author_patch`); author-side coverage fixture
  (`ageTag`/`isEgg`/`fallbacks`); **built-in content decision** (no `UniversalSqueakerFallbackProfileDef` /
  `UniversalSqueakerTuningBaselineDef` ships anywhere, so `Vanilla` is silent for every pawn, `Fallback`'s last
  tier is empty and Presets can only show its empty state — author per-race profiles, or de-scope the tier and
  rename the mode; do not explain the silence away as design); brand-boundary ruling for the fixture packs that
  carry Ratkin/SR names; the D8/F6 preset fixture for `Nivarian-US-EXP`.
- [ ] **Diagnostics product calls**: the collapsed bar is still as wide as the expanded panel (`BeforeDraw`
  shrinks height only) — measure the bar and shrink the width, or keep 680 and tighten the content; the expanded
  detail column shows blank space with nothing selected — decide the empty state; over-long author names overlap
  in the dropdown popup (a fixed 24px option band under wrapping text) — single-line + ellipsis in the library
  (cross-repo) or rows that grow (moves the pinned "popup height = options x 24" lane).
- [ ] **0.5.x items A/B/C** (`MEMORY.md` "Version scope convergence"): (A) file-driven **invocation** + hot
  reload — editing a layout/style file and reopening the window shows the change, kinds stay compiled; (B)
  appearance file-driven + tier-2 granularity actually used (`UsTheme.cs` palette into the style document,
  geometry constants into tokens, row-by-row colour reads onto `Tone`/`Emphasis` where the vocabulary reaches
  them); (C) structural migration — now S4 above.
- [ ] **Optional tail**: the runtime harness does not cover the adapter fold/converters/`BuildFallback` mode
  pass-through (ReviewResolverFold P3 residual); HAR reflective discovery is `TODO(HAR)` and assembled-only —
  never claim reflection; the two 2px band floors (`checklist`, `diag-list`: 20 vs the calibrated 21.33 Small
  line); a FL lane for the single-line RENDERING (the carrier stub records rect/text/colour but not wrap state,
  so "the row no longer wraps" rests on the 1.6 source path plus the green suite); drop the nav subtitle line
  entirely (`SubtitleLines = 0`) to reclaim ~100px of stack height if the compact cards still read as tall.

## Landed — pointer lines (detail in git log / `MEMORY.md` / `OBLIVIONIS.md`)

- **2026-09-22 evening — S6 closed and S4 closed in the same round**: S6-1 wording, S6-2 flat nav + de-boxed
  cards + the separators deleted, S6-3 the warm palette (guard first), the section-header gold rail,
  `us/square-toggle` (Registrar 11 -> 12 -> 13), then the toggle follow-up that made it render the
  reference's 34x18 with the accent on the knob; S4 closed at 18 -> 11 kinds. Detail: the S4/S6 sections
  above and `MEMORY.md`.

- S3 frame reset (S3-0..S3-5, 2026-09-21), the narrow help band (乙1) and its 2026-09-21b re-cut, U1 (the
  global-volume caption band) and the checklist-card migration (B-1/B-2): all landed and verified; the durable
  half is in `MEMORY.md` "Current state".
- Rebuild plan (2026-08-23) + UI migration S0-S5 + orphan features: completed. 6-way review 2026-08-28:
  findings closed, reports in `docs/review/**`. Old-UI Knife 1 + Knife 2 done 2026-09-02 (`38b1247`); Knife 3
  open above.
- Text fit + localization (2026-09-02b), the vacuous height axis (2026-09-03), the composite popup path
  (2026-09-04), the desync + band calibration (2026-09-04b), the filter clock fix (2026-09-04d) and the filter
  labels (2026-09-04e): landed; durable rules in `MEMORY.md` "UI engineering rules".
- FerriteLib extraction (2026-09-03, sibling repository) and the first cloud upload + publication chain
  (2026-09-06/07: privacy rewrites, mirror chain, public flip + branch protection, `v0.2.0-rc1`).
- Cross-repo rounds 1-3, the seam round, the diagnostics round-9 migration, the eat-granularity port
  (`e414b71` + must-fix `712de50`), `v0.4.0-rc1` on both repositories, the declarative help drawer
  (`cb1b5e1`), the P1 seam batch (per-host text-fit audit, FL-20) and the 2026-09-19/20 rounds: landed.
- **Everything before 2026-09-21c — including this file's full pre-compaction text — is byte-archived in
  `OBLIVIONIS.md` "Memory compaction 2026-09-21c".**
