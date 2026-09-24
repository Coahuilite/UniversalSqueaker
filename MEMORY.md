# MEMORY

> Durable facts and evidence pointers only. Session narrative, finished-implementation detail, raw logs and
> commit chains live in `OBLIVIONIS.md` (cold archive); per-incident history lives in the git log. The action
> surface, including the ONE collected in-game list, is `TODO.md`. Code and the carrier outrank this file.
> **The pre-compaction text of this file and of `TODO.md` is archived byte-verbatim in `OBLIVIONIS.md`
> "Memory compaction 2026-09-21c"** - read it only for a historical conflict.


## Handover — start here (2026-09-22)

> The first ten minutes of a new session. Everything here is a POINTER: the code, the manifests and the
> carrier outrank this file. Nothing here pins a live artifact identity — see item 2's last line.

**1. Read these, in this order**

1. `AGENTS.md` — the rules that were each paid for once (evidence, commit, carrier, push).
2. `MEMORY.md` (this file), then `TODO.md` — the live action surface.
3. `Source/UniversalSqueaker/UI/Layout.Schema2.xml` — the MAIN page manifest. `Layout.Overlay.Schema2.xml`
   beside it is the in-world camera overlay, not a second settings page.
4. `Source/UniversalSqueaker/UI/Kernel/UsKernelWidgetRegistrar.cs` — the US-owned kind set: **13
   `Register()` calls**. The cardinality is pinned by `UiSourceInvariantTests` (13) and the per-lane checks
   read `KnownKinds(scope)`, so a kind no manifest element uses, or a dropped registration, reddens.
5. The maintainer's in-game checklist: `../modding_documents/team-mode/us-ingame-checklist-2026-09-22-zh.md`
   — a WORKSPACE-level path one directory above this repository; it is not in-tree.
6. Tooling: `scripts/verify-local.ps1 -NoRestore` (the 15-gate chain; gate 6 is the carrier boundary,
   gates 14/15 the boundary and stub-coverage audits) and `tools/UniversalSqueakerKernelHostTests` — the
   real-Host harness, one lane per slice.

**2. How US uses the carrier's development geometry instrument**

- **One place, zero widget instrumentation:** `Source/UniversalSqueaker/UI/Kernel/UsTextFitAudit.cs`. That
  class is already the per-window host diagnostic scope and already owns the window's
  `UiDiagnosticSubscription`, so it opts the subscription in (`Open`), prints the dump (`Publish`) and
  turns it back off (`Dispose`). All of it is inside `#if US_DEV`.
- **The switch is the existing one:** the scope only opens while detailed logging is effective
  (`SqueakLog.ShouldEmitDev`), which the Diagnostics workspace toggles in game — no second switch to drift.
- **The channel is the existing out-of-protocol `ltrace`** (`SqueakLog.LayoutTrace`), so no lane asserts it
  and none had to be re-cut. Output is bounded: one block per sampled pass; identical clicks in later passes are preserved.
- **Grep in `Player.log`** — the two line shapes that answer "who ate this click":
  `ltrace: geometry input path=… kind=… point=(…) rect=(…) verdict=hit|miss|covered|disabled`
  `ltrace: geometry rect path=… arranged=(…) draw=(…) window=(…) height=… h=…`
- **Current build contract (2026-09-24):** outputs are isolated under `dist/build/Dev` and `dist/build/Release`; `FerriteLibArtifactPath` selects an explicit carrier (relative paths are US-root-relative). `build-dev` never builds FL. The compiler records the selected DLL hash and staging refuses a different one. Use `docs/build-and-debug.md` for the paired Dev workflow; the old shared-output round trip below is history.
- **On a Release carrier** the enable setter throws by design; US catches it, writes ONE line per process
  (`ltrace geometry unavailable: …`) and keeps drawing.
- **A live carrier identity is never written here.** Only history. The current identity is whatever the
  LATEST FREEZE NOTICE states, quoted as a hash AND an mtime pair — the two answer different questions.

**3. Open defect, claims, and what is closed**

- **OPEN, highest value: the Packs workspace's race row does not respond to a click** (the filter does).
  `Player.log` shows no exception, no TRIPPED/recovery band and no `KeyNotFound`, so a missing command
  binding is ruled OUT. Four candidates, **none of them measured**: `Chrome="none"` short-circuiting the hit,
  the `Overlay`'s paint order, a `Scroll` consuming the press, the `MatchContent` band's real rect.
  **Item 2 is the instrument that answers it, and it needs a dev carrier.**
- **Claims, not facts — do not repeat them as done:** the square toggle's press path and its hover lane (only
  the contract is asserted); the 24 -> 36 band widening's lane re-cut at the 320 shape (a page width the
  window floor cannot produce); row fill / row hover in the layer cards (two routes measured blocked, the
  third — a CONTAINER state sibling — never tried); the 3px selected rail (needs the carrier's per-edge
  stroke).
- **CLOSED with its reason, so nobody re-opens it:** the OFF knob's ink stays `TextSecondary` — the disabled
  branch already returns `TextDisabled`, so reusing it for enabled-OFF would make a read-only ON toggle look
  like a clickable OFF one. Semantic collapse, not a colour preference.
- **Style debt at the S4 finish line:** `us/diagnostics` paints its own chrome in C#, so neither the manifest
  nor the flat scope reaches it and the Overview page keeps one boxed card among flat ones.

- **S6-2/S6-3 started: the nav column is scoped flat, and the scheme had to live in the MANIFEST.**
  A style scope that sets a surface's BORDER token equal to its FILL is how "no box" is spelled here (the
  vocabulary has no `Border=none`; `UiThemeDraw.Surface` paints a 1px frame in the border colour).
  **Durable pitfall measured on the lane's first run:** the engine resolves an element's `Scheme` against
  **the document the Host was built with - the layout manifest's own `<Styles>` section** - while
  `UsTheme.SchemeXml` is the in-code palette applied to the theme INSTANCE. A scheme declared only in
  `UsTheme` is never resolved, and the scope silently keeps the page's bordered values (the resolver just
  records "unknown scheme" and falls back); the lane caught it as `border #333A46FF vs fill #1F232CFF`.
  So a new scheme goes in `Layout.Schema2.xml`'s `<Styles>`, and it should declare **only the tokens it
  overrides** - the rest inherits the cloned baseline. Evidence: `FlatStyleLaneTests` (document tokens +
  resolved scoped theme + the page-level control + the states-still-differ mutation), three mutations red.

- **S6-2/S6-3 step 2 LANDED: the cards lost their borders and the bodies lost their separators.** The five
  `chrome/rule` elements in the basic-tuning body are deleted (the rows separate by the body Column's Gap,
  raised 2 -> 6), and the **eight declarative Section cards** carry the flat scope. Composite cards
  (`us/diagnostics`, `scope-tree`, `filter-bar`, `preset-list`, `footer`, `help`) are deliberately NOT
  scoped: they paint their own chrome in C#, so flat is S4's destination rather than a shortcut around it.
  `FlatStyleLaneTests` asserts the scope on every declarative card and that no `chrome/rule` survives.
- **Two recorded workarounds, with their citations, NOT vocabulary requests:**
  (a) **`Border=none` does not exist** - "no box" is expressed by setting a surface's BORDER token equal to
  its FILL (the vocabulary paints a 1px frame in the border colour, so equal values make it invisible).
  Citation: the nav column and the eight cards, S6-2/S6-3. No forced consumer for a real switch, so it stays
  a workaround.
  (b) **an unknown `Scheme` name falls back silently** while an unknown TOKEN is recorded, in the same
  document (`UiStyleResolver.ApplyScheme` records and returns; `UiStyleDocument` refuses the token). Live
  citation: the flat scheme first written into `UsTheme.SchemeXml` resolved to nothing and the scope kept the
  page's values. Reported to the lead; FL is folding **diagnostics only, not a hard refusal** into task-9.

- **S6-3 step 3 LANDED: the palette is SR's warm dark + gold, and the guard landed FIRST.** The re-tint
  moved Base/WorkspacePlane #0f1116 -> #0e0d0c, Panel/SectionBand -> #171512, Raised -> #1d1b17, Hover ->
  #242019, Selected -> #3a311f, Border/Divider -> #575247 / #2a2620, BorderStrong -> #6b6459, TextPrimary ->
  #eae6de, TextSecondary -> #b0ada3, TextDisabled -> #8a8780 (split from TextSecondary, which it used to
  equal, because on a warm plane the two read alike), Danger pair -> #3f1c1a / #c96057. **`AccentGold` was
  deliberately NOT re-tinted**: `UsSurfaceLaneTests` pins it against `UiTheme.DarkGold` as the series
  identity, so SR's #EBAD4D stays a benchmark. **RULED 2026-09-24 (maintainer): the series gold IS the
  identity and `AccentGold` keeps it; SR's #EBAD4D is a benchmark, never adopted** - that closes the last
  undecided item on the appearance axis, so no US file may restate it as pending. Unmapped SR roles
  (ButtonPrimary/Ghost, FilterChip,
  HelpIndicator/Toggle, SelectableCard's own hover tone, knob greys, empty rail, dropdown ink, zebra band)
  are recorded rather than invented into tokens.
- **The guard is the point of the step: `PaletteLaneTests`.** A re-tint can silently resurrect every card
  outline (the flat look is a surface's border token EQUALLING its fill), so the lane asserts every flat pair
  is still equal, with the page level as the boxed control; and it measures WCAG contrast for the four inks
  against the surfaces they are painted on (primary 14.64/13.81, secondary 8.12/7.66, active ink 9.30, floor
  3.0). Mutations: a one-sided `RaisedBorder` change and a mud-dark `TextSecondary` both redden.
  `UsSurfaceLaneTests`' spec literals were re-cut in the same batch - they pin this palette, so a re-tint
  invalidates them by definition.
- **When you want the accent, NAME the accent (measured 2026-09-22, corrected the same day).**
  `UiTheme.SelectedSurface` is `(Selected, SelectedBorder ?? AccentGold)` (`UiTheme.cs:219-224`) and
  `SelectedBorder` **is assignable from a style document** (`UiStyleDocument.cs:516` ->
  `UiStyleResolver.cs:272`), so the flat scope `us-flat-panel` setting it EQUAL to `Selected` - the
  equality that IS "a flat surface paints no box" - **aliases the `?? AccentGold` fallback away**: inside the
  scoped cards `SelectedSurface.Border` reads `#3A311F`. The first `us/section-header` rail was passed that
  value explicitly and came out that colour. **The trap is the CALL CONVENTION, not the helper**:
  `UiThemeDraw.AccentRail`'s own fallback is `color ?? theme.AccentGold` (`UiThemeDraw.cs:150`), never
  `SelectedSurface`, so calling it with no colour is safe. A consumer that wants the accent passes
  `theme.AccentGold` (or omits the argument). FL recorded the same lesson on its consumer page without
  changing code or API, which is the right split: this is a consumer-side convention, not a component defect.
- **S6-3 step 4 LANDED: the square ON/OFF toggle is a US kind (`us/square-toggle`), and the Registrar is
  12 -> 13.** Seven Overview controls moved onto it. The reason is a SHAPE, measured first: there is **no
  purely filled declarative element**, and **nothing lets geometry or position depend on a bound value** (the
  engine-wide value-dependent attributes are Visible/VisibleKey/Hidden/SelectedKey - visibility and state,
  never shape). "Position follows a bound bool" has exactly ONE consumer, so it is a recorded CANDIDATE with
  this step as its citation, **never a carrier request**.
- **ROLE DESCRIBES A SURFACE'S TONE, NOT A CONTROL'S MATERIAL (measured, and it overturned an instruction).**
  The first version resolved the track through the role table (`Active` when on, `Neutral` when off). The
  cards are the flat scope, which deliberately sets `SelectedBorder` equal to `Selected` and `RaisedBorder`
  equal to `Raised` - the equality that IS "a flat surface paints no box" - so the OFF track's fill AND edge
  were both exactly the plane behind them (**`#191612` on `#191612`**) and the control vanished. **A control
  that must be VISIBLE on a flat plane cannot take its material from the role of the plane it sits on.** The
  toggle paints off = `Raised` fill + `Border` edge, on = `AccentWith(0.25)` fill + `AccentGold` edge,
  knob = `TextSecondary` off / `TextOnGold` on: theme values, no literal and no new token, but the material
  belongs to the control. (One consequence recorded with it: on this page the track's fill coincides with the
  card face by construction, so the **edge** is what separates them - the lane asserts the knob against both
  the track and the plane, and the track against the plane through at least one half.)
- **S6-2 (B) MEASURED, STOPPED, AND ONE SHAPE LEFT UNTRIED (2026-09-22).** Two candidate shapes were
  built and run, and both are blocked (the third was identified only after the fact and is recorded as a
  candidate, not as a measured result):
  (a) **the hit element paints itself** - it WORKS (idle `#191612`, selected `#3A311F`, hit height still ==
  the text column's) but it moves the hit band's own geometry and reddens `DeclarativePacksLaneTests` at
  320px (`one hit area per row (4 rows), got 3`; the row's measured height 90 -> 68.67), and the whole-row hit
  is the point of the slice, so it is refused;
  (b) **an `input/button` painting SIBLING behind the hit** - the geometry holds exactly
  (`covered=100% uncovered=0px` in all sixteen passes) but the sibling cannot exist as a button: it invokes
  its command on every click (`ButtonWidget.Draw`), so without `ActionBind` it throws the moment a press
  lands on it - measured `KeyNotFoundException: No command binding registered for ''` at
  `.../race-layer-row-state#human`, the element then replaced by a recovery band; with a command instead it
  is a SECOND hit surface and the press step's ordinal maps to the wrong row (measured: band #1 selects
  `testrace`).
- **What the vocabulary actually offers, stated precisely (corrected the same day; the wide version of this
  sentence was wrong and would have sent the next reader down a dead end).** `input/button` is the only atom
  that is **interactive AND paints a surface**; **a non-interactive surface is provided by a CONTAINER's
  `Chrome`** - it paints the whole rect's fill plus four edge strips, and a flat scope aliases the edges away
  by making them equal the fill. So a container IS a non-interactive surface painter, and it takes no input and
  invokes nothing. **The untried shape is therefore: a CONTAINER as the state sibling, with its own flat
  scope** (same Overlay, same `MatchContent`, `SelectedKey` for the state). It is recorded as a **candidate
  with this round as its citation**, NOT as a measured result - it was not built. **Row fill and row hover go
  to the REAL-SCREEN list** together with the 3px selected rail (that one needs drawing for its own recorded
  reason), to be decided when there is live feedback; the earlier "the selected fill IS expressible" stays
  narrowed to the shape that changes the hit band's geometry, which this slice refuses.
- **Two lane facts this step paid for, on top of the three already recorded.** (1) **Assert a widget's output
  against an independently built expectation, never against the widget's own helper**: the first version asked
  `UsSquareToggleWidget.Material(theme, state, …)` and compared the drawn fill to its answer - which stayed
  GREEN when the material was made state-blind, because both sides read the same bool through the same
  function. Built from the theme + the state the lane read, the same mutation reddens with the colours named.
  (2) **The widget's press path is not harness-aimable here**: a press computed from the arranged snapshot
  lands on the page but not on the control, because the drawing frame and the arranging frame are different
  arrangements. The step therefore asserts the CONTRACT the press depends on and records the press itself as a
  real-screen item; adding a public seam so a test could aim is vocabulary growth and was ruled out.
- **S6-3 step 1 LANDED: the section header is a US surface again (`us/section-header`), with a 3px gold
  left rail and a geometric marker.** The Registrar's us/* set **grew 11 -> 12** - the pin's first growth, and
  the parity check above it keeps that honest (the kind exists only while a manifest element uses it). The
  reason is a SHAPE, measured before it was written: `chrome/rule` paints a horizontal hairline only and a
  container's chrome is a whole filled band, so a vertical rail is not declarable. **The rail's token was
  measured twice and the first answer was wrong**: `SelectedSurface.Border` looks like the gold path
  (`SelectedBorder ?? AccentGold`) but the flat scope sets `SelectedBorder` EQUAL to `Selected` - that
  equality IS "a flat surface paints no box" - so inside the scoped cards that token is `#3A311F`. The rail
  paints `theme.AccentGold`, the series accent the navigation rail uses. The marker is `U+25B6`, gated by
  the D7 probe seam: a font that cannot draw it gets NO marker rather than an empty box. It is inked
  `TextPrimary` honestly - no style-table cell resolves a gold ink without a gold fill - and "an emphasis ink
  with no fill under it" is recorded as a BACKLOG candidate with this step as its citation. Lane:
  `UsSectionHeaderLaneTests` (geometry + drawn-pixel existence, **not** a hit lane). Its two mutation proofs
  are the declaration (a header back on the carrier kind) and the PIXEL (with that step disabled, the
  assertion still names the offending rect `#2A2620`).
- **The carrier defect behind that step is FIXED (FL `876750a`, task-10): `section/header` now reads
  `Chrome="none"` and suppresses its rule.** Consequence recorded in both the widget and the lane so nobody
  keeps the stale story: a declarative header can now have no rule, so **the surviving reason for
  `us/section-header` is the RAIL** - if the rail is ever given up, the manifest can go back to
  `section/header Chrome="none"` and both new files can be deleted.
- **Carrier identity moved three times on 2026-09-22** (`876750a`/`58B57EAD…` -> `e668344`/`1BBF5F4D…` ->
  `553fc53`/`3FA8CABE…`; all 252416 B, Release, no PDB). **Do not pin the live identity here.** Every commit
  that moves FL's HEAD forces a payload rebuild, so a SHA written into a tracked file is stale the moment it
  lands - that loop is why this bullet replaces a pinned one. The live identity is what the **FREEZE NOTICE**
  states, quoted as **hash + mtime**; this file keeps the history.
- **"US reads the carrier read-only" is MEASURED, not asserted:** across that chain run the carrier was
  byte-identical before and after (same SHA, same bytes, no PDB).
- **The first push of `0.5.x` happened 2026-09-22**: `privacy-audit -FullHistory` CLEAN over 392 revisions,
  upstream `origin/0.5.x` set, no tag created. **A pushed dev line is not a release**, and the workflows agree:
  `ci.yml` triggers on `main` only and `release.yml` on tags/releases only.
- **A hash and an mtime answer different questions, and the freeze needs both.** The hash says "are these the
  same bytes"; the mtime says "was the file written". A rebuild of identical content moves the second and not
  the first (measured above), so a FREEZE NOTICE that quotes only the SHA is under-specified for anyone
  checking whether the payload was touched - quote the hash AND the mtime, and report the pair when verifying.
- **Carrier freeze before the task-10 rebuild: FL `490d4f076431`**, SHA-256
  `E396E089FE0D59065F3E7D672427131EF01B70D4A07A7F71733A16FDB36E5ACB`, 252416 B, Release, no PDB, stamp
  `0.7.0-dev+490d4f076431…` == HEAD. It **shipped `Height="MatchContent"`**, which unlocked the whole-row hit
  area; that step ran green on it.

## Documentation-vs-code defect rule (measured 2026-09-22)

- **When a document and the code disagree, the code is the fact and the document is the defect - unless the
  maintainer says otherwise in the room.** A doc defect must never be promoted into a product decision.
  Measured specimen: the old `US.Help.BasicTuning.ScaleTalking.Text` said the option only lets an action pass
  "while the pawn is talking", which transcribed the CODE'S ENUM NAME (`SqueakVocalGatePolicy.ApplyTalkingGate`)
  as if a name were a behaviour. The mechanism is a probability roll against the pawn's Talking capacity
  (`CompSqueaker.cs:513-524`, `SqueakVocalCapability.cs:21-29`, `CompSqueaker.cs:895-896`). A lead scoped the
  difference as a candidate behaviour change; the maintainer's reply was "I do not remember ever asking for
  that", a workspace-wide search found the phrase ONLY in the language tables (and their `dist` copies) and in
  that round's own reconnaissance note - never in a spec, TODO, MEMORY or design record - so the requirement
  never existed. Debt boundary: the wording was corrected to say what the code does; "squeak only while
  talking" would be a NEW FEATURE with its own entry, not a wording fix.

## Current state (2026-09-21)

- **Product axis `0.5.x`; all three identity axes are `0.5.0`** (`<Version>` / `<VersionPrefix>` /
  `About.xml <modVersion>`, moved together 2026-09-20). The local `0.5.x` line is open at `ad1a744`. The line
  name follows US's OWN product axis, never the carrier's (`0.5.x` against carrier `0.7.x` is the proof case).
  `<Version>` still moves only by maintainer ruling, and `main` is a release-signal surface: it moves only when
  an rc or a stable is cut.
- **Carrier pin `[0.7.0, 0.8.0)`** (`Source/UniversalSqueaker/Mod.cs`, lifted 2026-09-19). **The carrier is
  identified by COMMIT** (`AssemblyInformationalVersion`, which gate 6 already compares against the carrier
  checkout HEAD), **never by `Api`**: during the US UI coordination phase FL may add surface inside `0.7.x`
  with no minor bump, so an older `0.7.0` payload lacks, for example, `input/text-field`. US compiles against
  `../ferritelib/1.6/Assemblies/FerriteLib.UiKit.dll` by relative `HintPath` with `Private=false`, ships no
  copy of it, and declares the dependency in `About/About.xml`.
- **S3 frame reset LANDED 2026-09-21** on `0.5.x`: S3-0..S3-5, one commit per step, each green on its own
  (newest `7958376`), plus the (乙1) re-cut `4c4a19a`. Spec authority `docs/ui-redesign-0.7-zh.md`
  sections 0 / 2 / 3 / 6.1. Shipped frame: a declared `Row` **header band** (title + the page's only Help
  switch) / the three-column body / a declared `Overlay` **footer band**, with the page rhythm written on the
  containers (ruling 丁 - the density tokens stay the library baseline).
- **(乙1) the narrow-screen help presentation is LANDED and verified.** `help-scroll` (wide column, 320) and
  `help-band` (a full-width `Scroll` between `body-row` and `footer-band`) are two MUTUALLY EXCLUSIVE
  presentations of one player intent: `help-open` is still the only value the toggle writes, and the host
  derives `help-open-wide` / `help-open-narrow` (read-only) from
  `WindowChromeLayout.DrawerWidensTheWindow`, a pure and screen-only predicate. The band declares no height,
  uses `Fill="true"`, and REPLACES the body - the body row is hidden by its own host-derived key, so the band
  is `page-root`'s only flexible fill child and takes the whole leftover between header and footer by
  construction. Exclusion was forced by arithmetic, not taste: at 1024x768 (page 960x530) header + footer + gaps
  leave 428 for body + band while the body's content floor is 271.
- **Known frame limitation (durable).** `nav-column` is a plain `Column`, **not** a `Scroll`, so `body-row`
  cannot shrink below its content floor (`us/nav`, 271px at this page). A band that SHARES the page with the
  body is therefore not expressible - that is why the narrow help REPLACES it. **If sharing is ever required,
  the fix is to put `nav-column` inside a `Scroll`**, which makes the body shrink-safe. The reserved-band
  vocabulary is also still absent (no container `MinHeight`/`MaxHeight`, no fill weight, no `HeightKey`).
- **(甲) the global density (12/8/4/24/1) is deliberately NOT inside S3.** It moves every control's INNER inset
  (the atoms read `theme.Geometry.Padding` as their own inset), so it needs its own slice and its own in-game
  look; spec section 3 records why it was separated.
- **S4-1 LANDED 2026-09-21: the Overview workspace's three composite cards are declarative and their kinds
  are retired.** `us/global-volume` / `us/basic-tuning` / `us/camera-indicator` are gone; the Registrar's
  `us/*` kind set is **15** (was 18, pinned by `UiSourceInvariantTests`), 493 widget lines were deleted, and
  the seven `toggle-*` action bindings retired in the same change because a declarative `input/checkbox`
  writes the inverse of the bool it read **through its value binding** - the value binding IS the toggle, so a
  second write channel per row had no reason to exist. Evidence: harness ALL PASS + `verify-local` 15/15 on a
  Release carrier, 5 mutations each red on the expected assertion. Report (measured geometry, the five
  player-visible deltas, the mutation ledger): `docs/ui-redesign-0.7-zh.md` §5.8.
- **The one atom fact S4-1 measured, durable.** `input/checkbox` paints `side = max(8, height -
  theme.Geometry.Padding * 2)` **left-aligned** in its band and `Padding` is 6, so a declared `Height="30"`
  is what reproduces the shipped 18px visual box (`Height="24"` draws a 12px one) - which makes a declarative
  row 24x30, not the composite's 24x24. `text/wrapped` has no alignment axis and no font attribute: it is
  UpperLeft at the theme font plus `Padding*2` of vertical lead, so a declared label is top-anchored, sits at
  the card padding instead of `RowLeftPadding`, and `chrome/rule` paints the resolved role's **Border** ink
  rather than the shipped `Divider` token. All three are player-visible and are listed as such in §5.8.
- **S4-2 LANDED 2026-09-21: the two Packs layer cards are declarative row sets and their kinds are
  retired.** `us/race-layer` / `us/xenotype-layer` are gone; the Registrar's `us/*` kind set is **13** (was 15,
  pinned by `UiSourceInvariantTests`), 388 widget lines and the `UsDomainSelection` payload struct retired with
  them, `UsPacksText` moved to `UI/Layout/`, and `select-domain`'s payload became the row's own string key. This
  is the **first real consumer of G2** (`input/button.PayloadKey`: a repeated row reports its own key - the lane
  presses each row and asserts which domain the model received) and of G3 (`Chrome="none"` bare hit area).
  Report + measured geometry + eight mutations: spec §5.9.
- **S4-3a's commit message claims a `verify-local` run that did not happen; corrected here, not by amending.**
  Commit `a2e9547` says "harness ALL PASS + verify-local"; only the kernel-host harness ran. The lead ran
  `verify-local` afterwards: **gate 6 FAIL / EXIT 1** on the doc-first condition (payload built from
  `e929fa1`, carrier checkout at `d1f2c50` - the expected red), so **everything from gate 6 onward is
  UNRUN**. The carrier re-verified read-only (SHA `C3B369…0CA6FB`, no PDB).
- **The measured cost of an "expected red" gate, and the rule it bought.** Gate 6 stopped `verify-local` in
  S4-3a, so every later gate went unrun - which is how `UiSourceInvariantTests`'s registrar cardinality
  stayed at **13** through the slice that made it 12. That pin lives in the **UI-logic project**, not in the
  kernel-host harness, so the harness could not see it. **Rule: run the expected-red gate AND run what
  follows it separately, marking the untaken range UNRUN.** Re-cut in this batch: the pin is **12**, and the
  two stepper captions moved from manifest literals to Keyed entries
  (`US.Tuning.CooldownMultiplier.Minus`/`.Plus`) because that project's localization guard refuses a
  literal `Text` on a shipped manifest element - the hidden defect's second half.
- **Measured this batch, individually (all green)**: kernel-host harness, `UiLogicTests`, `KernelTests`,
  `ConfigCopyTests`, `SettingsMigrationTests`, `LogTests` Release and Dev. Still UNRUN because they sit
  behind the expected red: the carrier/payload gates (6, 9), the LICENSE gate, the UI-boundary audit and the
  stub-coverage gate.
- **CARRIER IDENTITY MOVED in this batch, by me, against the discipline - recorded, not hidden.** While
  re-running the post-gate-6 lanes I also ran `dotnet build ../ferritelib/Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj
  -c Release --no-incremental` - verbatim the command `verify-local`'s gate 6 runs - which **rebuilt the
  carrier**: SHA `C3B36923…0CA6FB` -> **`6094C8FB…DB10D6`** (Release, no PDB). The FL checkout is clean at
  `d1f2c50`, so the payload now embeds **`0.7.0-dev+d1f2c5014de0…`**: it is a real carrier build from that
  commit, not a stale one, and gate 6's doc-first red is therefore **gone for the wrong reason** (the hash was
  moved rather than left frozen). The old hash is what the previous FREEZE NOTICE named. **Zero push/tag/
  release**; no other FL file changed. Consequence: the next FREEZE NOTICE has to name the new hash, and
  "gate 6 is expected red for doc-first" is no longer true on this tree until FL lands another commit or the
  carrier is rebuilt again at a freeze.
- **The full US gate chain was attempted after the re-freeze and stopped at gate 6 for a NEW reason: the FL
  checkout is DIRTY.** `d1f2c50` plus **two uncommitted FL paths** (`AGENTS.md`, `scripts/verify-local.ps1` -
  the owner's task-8 edit to the retry hints, in progress). Gate 6 refuses that state on purpose (a payload
  cannot be attributed to a commit while its tree carries unreviewed source), so gates 7-15 were again
  UNRUN by the chain. First five gates green; **6 UNRUN (blocked on the FL owner committing); 7, 8, 9, 10,
  11, 12, 13, 14, 15 all green when run individually** (all 15 commands read from the script itself, none
  invented). The new identity was verified read-only first (SHA `6094C8FB…DB10D6`, `0.7.0-dev+d1f2c5014de0…`,
  Release, 249344 B, no PDB, exclusivity FREE - the maintainer's own run).
- **S4-3b LANDED: the Distance card is declarative and `us/attenuation-editor` is retired.** The
  Registrar's `us/*` kind set is **11 (12 before)**, the 195-line widget is deleted, and the card is a
  Section over the engine's own `chart/line` - declared directly with `Editable`/`EditablePoints=1,2`/
  `Height=64`, which is the hand-built `UiElementSpec` wrapper removed - plus a bound status sentence and
  three declarative preset buttons. **S4's kind target is met: 18 -> 11.**
- **Two contract facts this slice measured.** (1) A declarative `input/button` validates the STRING action
  contract (its own `Validate`): a button with an `ActionBind` and no payload needs `BindCommand`, and one
  with a `PayloadKey` needs `BindAction<string>` - so adopting a button made a previously enum-typed action
  string-typed, and the enum parse moved to the host (the `select-domain` shape). (2) `SelectedKey` is how a
  static (non-repeated) button says "the model is on me": one read-only bool per button; the manifest pairing
  is what the lane must assert, because a constant fixture view can only ever make one key true.
- **Recorded narrow-tier finding (measured, not asserted away).** At a 320-wide PAGE the centre column is
  ~296px, so a third of the preset row is ~76px and the English captions "Conservative"/"Balanced" need
  ~96px; the `input/button` atom draws its caption single-line, so it clips there. The lane's overflow sweep
  covers the three widths the page is accepted at (1024/736/480) and this tier is recorded instead.
  **Provenance of every number, so the claim is not read as a fact about the product:** the 320 page width is
  the HARNESS's own bottom tier (`FrameGeometryLaneTests.cs:72`, `Widths = { 1280, 1024, 736, 480, 320 }`) -
  a stress case the lane drives directly, not a screen the game can present. The US window's own floor is
  `WindowChromeLayout.SettingsWidthFloor = 800f` (`UI/Layout/WindowChromeLayout.cs:60`, derived as the
  smallest whole-pixel 4:3 box covering vanilla's `Dialog_Options.InitialSize = (650, 600)`), so the narrowest
  shipped window gives a 736-wide inner page, not 296. The logical SCREEN floor (1024) is the separate,
  platform-enforced one (`UIScaleSafeWithResolution`), and at an 800-wide screen the drawer does not squeeze
  the centre column at all - `DrawerWidensTheWindow` answers false, so `help-open-narrow` is true and the
  band REPLACES the body (`UsKernelSettingsHost.cs:546-560`). **Not claimed:** whether any real screen can
  drive the 320 tier - unmeasured; the lane asserts it directly rather than reaching it through the window.
- **The dev rehearsal was re-staged at S4-3b** (clean tree, `commit=f378715`, carrier `0.7.0-dev+b997f3a9f009…`).
- **The full US gate chain is GREEN end to end (2026-09-22) against the re-issued freeze.** FL HEAD
  `b997f3a9f009…`, carrier SHA-256 `0F95DF35D5E7F827848364B9A9C83081A64E4C2C3AF15924D8089430025A0B66`,
  249344 B, `0.7.0-dev+b997f3a9f009…` == FL HEAD. `verify-local -NoRestore` ran **all 15 gates, exit 0**,
  including the seven that the previous attempts left UNRUN (6 payload identity, 7/8 the two configuration
  builds, 9 single-carrier, 10 LICENSE, 11 manifests, 14 UI boundary, 15 stub coverage). The UNRUN list is
  therefore closed.
- **Measured and written as fact: the US chain does NOT move the shared carrier.** Read-only pre/post around
  the whole run: SHA-256 `0F95DF35…` and mtime `2026-09-22 13:30:00` **both unchanged**, no PDB beside the
  payload, size still 249344. (The lead's warning that a full `verify-local` moves the identity is about
  **FL's** chain, not US's: US references the carrier through a `HintPath` and gate 6 only compares identity.)
- **Dev rehearsal re-staged against the frozen carrier (2026-09-22).** `pack-dev` built US Dev and the
  stager measured it: `flavor=dev measured=Dev label=0.5.0 commit=95fa9a0
  carrier=Release 0.7.0-dev+b997f3a9f009…`; `dist/dev/UniversalSqueaker/` carries 7 files
  (US dll 458752 B + 2 Keyed tables 37118/39067 + About.xml 2543 + LICENSE 15780 + LoadFolders.xml 104 +
  version.txt 176). The staged DLL is **Dev-configured and has no PDB**, which is the shape `dist/dev`
  documents and what a rehearsal dropped into `Mods/` should be (US_DEV also enables the dev logging and
  the footer revision a rehearsal wants). The maintainer-side acceptance of this batch runs from this folder.
- **Gate 6's retry hint is PROSE now, and that is the fix (task-7, 2026-09-24).** It used to print a
  copyable cross-repository build command, and a red gate is exactly the frame that must not build: the
  2026-09-22 incident rebuilt a frozen payload by pasting that hint in the name of "verifying". The hint now
  states what is true - rebuilding the FerriteLib payload is the CARRIER OWNER'S DELIVERY STEP, it REPLACES
  the current freeze, and it is complete only when the owner re-issues a FREEZE NOTICE quoting the new hash
  AND mtime as a pair; a consumer selects the carrier it compiled against and never builds one. The same
  four points now carry in `stage-package.ps1`'s release refusal, and `resolve-carrier.ps1` has NO default
  carrier any more (the old one silently resolved the root Release payload - the wrong artifact for a Dev
  rehearsal). Historical measurement, kept because it is why the hint had to change: the old command targeted
  the carrier and whether it WROTE was conditional - the DLL's SHA-256 **and its mtime both stayed put** when
  MSBuild found the target up-to-date (`6094C8FB…DB10D6`, 13:23:45, no PDB), while the same command at a
  stale payload DID rewrite the file and move the hash.
- **S4-3a LANDED: the trigger-timing card is declarative and its kind is retired.** `us/timing` is gone;
  the Registrar's `us/*` kind set is **12 (13 before, pinned by `UiSourceInvariantTests`)**, the 263-line
  widget was deleted, and the card is a declared `Section` over `input/slider` + `input/number-field` +
  `text/wrapped` + `input/button` with the checklist card's declared geometry. Report: spec §5.10.
- **The S4-3 friction, and it is a FIX rather than a relocation.** The composite sized its interval caption band
  against a hand-written worst-case SAMPLE constant (`"10.0 s"`) because measure and draw had to agree; the
  declarative shape has no reserved-band vocabulary (no `MinHeight`), so the caption became a read-only string
  binding the host builds once and the atom measures exactly the string it paints - measure == draw by
  construction. What is NOT reproduced is the reserved band: the caption is a flex sibling taking the row's
  leftover, so at the narrowest centre column it wraps one extra line and the card grows (~+10px at 320). That
  is a real-screen delta, not a defect.
- **Measured atom fact (durable): a fixed `Width` on a Row child is what makes the engine draw that child
  OUTSIDE its own row.** Once the drawer's degenerate arrangement shrank the centre column below the declared
  width, the multiplier/seconds number fields were placed past the row's right edge and `FrameGeometryLaneTests`
  caught five violations. Making both number fields flex siblings fixed it at every width. A declarative row is
  inside its parent by construction only when its children can flex.
- **Measured instrument fact (durable): `UiDiagnosticKind.Fit` carries TWO channels** - `fit.overflow`
  (which has an `Overflow` record) and `fit.style-fallback` (which does not). `UsAuditRoutingLaneTests`'s
  calibrated-zero half counted bare Fit events, so a real (and harmless) style-fallback finding read as a ruler
  leak. The lane now counts OVERFLOW records and reports the fallback count in its message: the instrument was
  made faithful to what the lane claims to measure.
- **Carrier-side `WidthKey` behaviour, measured:** a widget's `WidthKey` is consulted (and resolves) when its
  key is a registered numeric binding; if the key is NOT registered the engine records one
  `fit.style-fallback` and leaves the element unsized. Declaring `WidthKey` without registering the key is
  therefore a silent unsize, and it is exactly the finding above.
- **B1 FIXED on the carrier side (FL `e929fa11`), and the US half landed with it.** `SelectedKey` is now
  item-scoped (`UiLayoutEngine.QualifyItemBinding` gained it alongside Bind / ActionBind / OptionsBind /
  VisibleKey / PayloadKey), so a data-driven row CAN say "I am the selected one": the layer row's TITLE declares
  `SelectedKey="selected"` and the host registers a per-row `<items>.<key>.selected` bool, which resolves the
  `Active` role and paints the title in `TextOnGold` - the shipped selected ink. The **detail line
  deliberately does NOT declare it**: the Active cell takes TextOnGold whatever `Emphasis` says, so declaring
  it there would pull the detail's ink gold too while the shipped detail stayed TextSecondary. The S4-2 lane pin
  was **flipped from negative to positive** (exactly one row answers true and it is the model's selected row);
  `Tone`/`Emphasis` stay forbidden in templates (still literal-only). Mutation pair M-A/M-B proves both
  directions of the one assertion.
- **The selected row's FILL: re-measured 2026-09-22, two routes tried and both blocked; the rail is still
  outstanding (spec §5.9.6).** The old entry here said the fill "still cannot be expressed" and named a missing
  row-state carrier. That is now precise rather than blanket:
  (a) `text/wrapped` paints no surface, so the TEXT cannot carry the fill - that part of the old entry stands;
  (b) letting **the hit element paint itself** works (measured: idle `#191612`, selected `#3A311F`, hit height
  still equal to the text column's) but it **moves the hit band's own geometry** and reddens
  `DeclarativePacksLaneTests` at 320px, so it is refused - the whole-row hit is the point of that slice;
  (c) an **`input/button` painting SIBLING** keeps the geometry exactly (`covered=100%`) but cannot exist: the
  atom invokes its command on every click, so without `ActionBind` it throws at the first press (measured
  `KeyNotFoundException: No command binding registered for ''`), and with one it becomes a second hit surface
  and the press ordinal maps to the wrong row.
  **Untried candidate, with this round as its citation: a CONTAINER as the state sibling with its own flat
  scope** (a container's `Chrome` is a non-interactive whole-rect fill + four edges, and pairing the edge to the
  fill aliases the edges away). It is recorded, not measured.
  The **left 3px rail** is a separate and still-open gap: `chrome/rule` paints a horizontal hairline only, so a
  vertical rail needs drawing code (a real extra capability, not an attribute). Debt boundary unchanged: B1 was
  visible and cheap and was fixed; these are visible and not cheap, so they stay recorded with their citation,
  and the row fill/hover go to the REAL-SCREEN list rather than being re-opened unasked.
- **The other (B) S4-2 recorded is now CLOSED by a carrier capability, not by a workaround.** The recorded
  facts were: `Height="Auto"` heights from the element's **own caption** (`ButtonWidget.cs:84-99`), an Overlay
  keeps every child at its measured height (`UiLayoutEngine.cs:1423-1450`), and `AlignY="Stretch"` names a
  position, not an extent (`UiPlacement.cs:71-74`) - so the workaround was two bare 24px bands, **69.9%** of a
  flat row and **53.3%** of a wrapped one with a 42px dead zone. FL `490d4f07` ships **`Height="MatchContent"`**
  (the declaring child contributes NOTHING to its parent's measured height, so it resolves to the content height
  of the non-declaring sibling it covers; valid on Row/Overlay), and the two templates now declare ONE
  `Chrome="none"` + `Height="MatchContent"` hit each: **measured 100% coverage** at 1024/736/480/320 x EN/ZH x
  flat/wrapped. The lane keeps the 69.9/53.3/42px numbers as its printed control, and reverting the attribute to
  `Auto` reddens it ("hit 24px vs text 68.67px").
- **Gate 6's "payload commit == carrier HEAD" comparison is red whenever FL lands a doc-only commit after a
  freeze, and that is expected, not a defect.** Measured 2026-09-21: FL `5052440` (docs only) moved HEAD past the
  frozen build `e929fa11`. The round's acceptance identity is the FREEZE NOTICE (commit `e929fa11`,
  Configuration Release, SHA-256 `C3B36923…0CA6FB`), never HEAD. **Also measured**: a stale
  `FerriteLib.UiKit.pdb` (135 960 B, 16 s older than the DLL) sat beside the frozen carrier although the notice
  said "no PDB" - FL's own ledger records that accident (a verify-only gate run after a freeze leaves the stale
  Dev PDB behind). It does not move the DLL's identity. US must not build the carrier to "fix" it: FL's harness
  is itself a writer of that folder, so verification on the consumer side is read-only.
- **A container that omits `Padding` gets the density default (6 per side), and this has now bitten twice.**
  S4-2's `Repeat` did not declare it and the row set silently grew 12px; the lane now computes the row set from
  the DECLARED padding, so an omission reddens instead of shifting the card (the S3-5 rule, second specimen).
- **Next big goal: S4 continues = the remaining workspaces** (Distance / Tuning / Presets), one
  failure-sensitive loop per workspace; the slice ladder is spec section 6.
- **Green evidence at the current revision**: harness `ALL PASS` / EXIT 0 and `verify-local -NoRestore` 15/15
  EXIT 0 on a Release carrier with no PDB. Gate counts, key counts and catalog counts drift - the script's own
  `Invoke-Check` count and the parity/count gates are the authority, never a number quoted here.
- **The (乙1)/U1 stale status rows in spec section 6 were corrected on 2026-09-21** in the S4-1 batch, and
  S4-1's own row was added there. The remaining known drift is §5.3/§5.4, which are deliberately kept
  verbatim as the record of why the pre-§0.4 conclusions were wrong.

## Three lane facts the S4-2 close paid for (recorded 2026-09-22, reusable beyond that slice)

- **A lane that counts DRAWN controls must draw the same frame its snapshot came from.** `DrawChecked` calls
  `UiHost.DrawFrame`, and `DrawFrame` **re-arranges at the viewport it is handed** - so a census drawn at a
  different width is counting a different layout. Measured: one and the same layer row reads **748px** in the
  snapshot the step arranged at and **524px** when the census drew it at another width, and the census reported
  "0 bands" for a page that visibly had four. It is the same defect class as the two-metrics incident: the
  instrument's input was not the thing under test.
- **At the hit seam, a row's identity is DRAW ORDER, not geometry.** The carrier opens one native group per row
  Overlay, so every row control is handed its rect at its own group origin and the drawn rect carries no window
  position; at 1024 flat all four layer rows even measure the **same** 68.67x524 box. A predicate that armed
  "the first drawn rect matching this shape" therefore let a press aimed at the xenotype row select `human`
  instead. What the lane may rely on is manifest/draw order (step 5 asserts arranged boxes in that order), so a
  press arms the n-th control of the family and the PAYLOAD assertion proves which row it was.
- **The engine does not hand the hit seam a control that falls outside the drawn viewport.** Measured: at a
  720px canvas the xenotype row (y=632.67) is clipped, and a press aimed at it lands on the race row **above**
  it - silently, because the rect it does get is that other row's. A lane that means to press every row must
  arrange and draw a canvas tall enough for all of them (this one uses 1100px). Two facts that ride with it:
  selecting a race is a **real filter write** that drops the xenotype card's row (so that row is pressed
  first), and the fixture's row keys follow its own mode (so keys come from the live `Items` binding, never
  from a literal list in the lane).

## Environment facts that are easy to get wrong (written down once)

- **RimWorld's minimum supported resolution is 1024x768** (maintainer ruling 2026-09-06). 800x600 is NOT
  testable in game: every "800x600 in-game re-check" backlog item is closed as impossible-by-platform, not
  deferred, and no acceptance checklist may demand one. The harness keeps its 800x600 sweep as
  deliberately-conservative narrow-tier evidence and the window keeps its 800x600 design floor as defensive
  geometry.
- `UIScaleSafeWithResolution` requires `w/scale >= 1024` **and** `h/scale >= 768`, so **`UI.screenWidth` is
  always >= 1024 and the UI scale cannot push it lower.** The narrow-help trigger is therefore the narrow band
  of logical widths where the drawer cannot widen the window: **logical screen width in [1024, 1131]** (a normal
  1920 monitor reaches it only at UI scale above roughly 2.5).
- **The fit audit's join is two halves**: `Enabled` only opens the switch; a host must ALSO hold its own
  `UiHost.Diagnostics` subscription. A host with no subscription is measured with a **null ruler** and measures
  nothing - which is exactly what removes cross-window misattribution, by construction.
- **A nested `dotnet build` whose graph has ProjectReferences fails silently here** (exit 1, zero warnings,
  zero errors, nothing even at `-v n`) while the identical command succeeds with `-m:1`;
  `-nodeReuse:false`, `-tl:off`, `MSBUILDUSESERVER=0`, rsp files and killing stale nodes do NOT fix it.
  `KernelHostTests.csproj` therefore passes `-m:1` to its four nested stub builds. Harness-only, no shipped
  byte moves.

## Evidence discipline (rules in `AGENTS.md`; these are the measured specimens)

- **Line numbers are not a stable coordinate.** A second edit to the same file must not read its target region
  with the line numbers the FIRST edit left behind: the ranges shift, and the slice silently takes the wrong
  text instead of failing. Re-read the file, and guard the slice with its own first and last line as an
  assertion before editing. Measured 2026-09-22: a TODO re-cut read its second block at pre-edit offsets,
  clobbered the neighbouring section, and was recovered by `git checkout` plus a guarded re-run. Same family
  as "check the instrument's input" - here the instrument was the line number.
- **The sixth specimen, and a new mechanism: the mutation flow's own BUILD was the broken instrument**
  (S4-1, 2026-09-21). `Layout.Schema2.xml` and the language tables are **embedded resources** of the main
  assembly, and the mutation battery's `Copy-Item` restore preserves the source's old mtime - so an
  incremental build reused the assembly built for the *previous* mutation and the next mutation observed
  nothing. The symptom was convincing: M5 (a stretched egg string) first reported M3's geometry symptom,
  then M7's binding symptom. Setting the resource's `LastWriteTime` to now before each build made M5 red on
  its own assertion (`On=89 Off=67.67`). Promoted to `AGENTS.md` Evidence discipline.
- **Do not move a SHARED fixture's input to give one lane the case it needs.** S4-1's first cut added a seed
  field to `RecordingSettingsSource` whose default changed the empty view's egg value; the suite stayed
  green, which proves nothing about the nine files that construct that fixture. The lane now drives its two
  states from the fixture's own two shipped views (`RichData ? true : false`), and the fixture is byte-
  identical to the revision before the change (`git diff` empty).
- This phase caught **five greens-for-the-wrong-reason and one red-for-the-wrong-reason**. The red: a new lane
  measured widths without installing a Keyed table, so `Translate` passed keys through and the header switch
  was measured as the literal text `US.Help.Drawer.Toggle` (168px) against a 116px band, while the shipped
  caption is `Help` / `帮助`. **Any lane that measures text must install
  `Program.SetTranslatorResolver(Program.ReadKeyedTable(language))` per language** (`FrameGeometryLaneTests`).
- The greens include a stub ruler that made content shorter than real (so a real collapsing budget passed), a
  process-level counter satisfied by another lane's finding, and lanes still asserting a channel whose
  measurement side had already changed.

## Carrier and artifact discipline (rules in `AGENTS.md`; the US mechanics)

- `scripts/read-assembly-stamp.ps1` reads `AssemblyConfigurationAttribute` in a CHILD process, because a
  handle on a DLL that is copied or rebuilt moments later is fatal and because `MetadataReader` is absent from
  the Store PowerShell (both measured).
- `pack-dev` builds the US payload `-c Dev --no-incremental` and produces a **folder, no archive** (a
  rehearsal is installed by dropping it in `Mods/`; the old dev zip was shaped wrong too, contents at the
  root). **`build-dev` builds the US payload Dev and NEVER builds the carrier** - it resolves the carrier it
  was handed and forwards it as `FerriteLibArtifactPath`; the old shared-output round trip in which
  `build-dev` rebuilt FL is history (see the 2026-09-24 build contract in the handover section). Gate 6
  asserts the SELECTED carrier is Release-CONFIGURED, not merely present, and the stager refuses a payload
  whose recorded compiler-reference hash differs from the selected DLL. The engine refuses a `dev` label over
  Release bytes and the reverse, which in US is not cosmetic: `US_DEV` gates Auto dev-logging and the footer
  revision.
- **Gate 6 reddens on any carrier commit, docs included**, since it compares the payload's identity against the
  carrier checkout HEAD. Never edit FL source to satisfy it - rebuild the payload and announce that HEAD moved.

## Identity and enduring corrections (verified against source; do not re-derive)

- Repo `coahuilite/UniversalSqueaker`; packageId `coahuilite.universalsqueaker`; namespace / Def prefix /
  log prefix = `UniversalSqueaker` / `US_` / `usdiag`; licence **MPL-2.0** series-wide (no "Incompatible
  With Secondary Licenses" notice). **Public since 2026-09-07** with branch protection live (force-push off,
  deletion off, no required checks; the PUT needs `"restrictions": null` or GitHub 422s, and it was impossible
  while private - free-plan REST 403). Workshop display name and the licence action stay maintainer-gated.
- US is the local fork of Squeaky Ratkin (SR), created 2026-08-23 from SR branch `0.3.x` tip `b19d68a` - an
  object of the SR repository, intentionally unreachable from this clone, not a lost commit. **SR owns Ratkin
  exclusively**: US ships no `SqueakyRatkin.*` types and no Ratkin audio/content/profiles/attachments. The 0.4
  co-existence policy is both mods enabled together with no `incompatibleWith`, and the legacy SR bridge is
  dropped. SR 1.0.0 will shrink SR into a pure audio pack with US as prerequisite.
- **Content-pack packageIds namespace under the AUDIO AUTHOR's lowercase id**
  (`saryaki.meowingkiiro.voices`), never `coahuilite.*` (ruling 2026-09-14).
- Reflective HAR discovery is **not implemented**: `Catalog/SqueakXenotypeCatalog.cs` carries `TODO(HAR)`,
  discovery is assembled-only, and the hint lists are permanently empty. The `AGENTS.md` sentence about HAR
  reflection is intent, not the shipped build.
- `CompProperties_Squeaker` carries only `actions` and `moodMods`; the kernel registers **16** `us/*`
  kinds (older notes saying 17 are wrong). The structural-migration target separately counts **18
  consumer-owned `us/*` widget kinds** - spec 5.1's inventory is the authority for that list.
- **`Vanilla` silence is unfinished content, not design**: no built-in fallback/baseline Def ships, so
  `BuildBuiltInSource()` is always empty. Never explain it away as a property of the mode.
- **uGUI / UIElements are NOT absent from the game**: `UnityEngine.UI.dll`, `UIModule`,
  `Unity.TextMeshPro` and `UIElementsModule` all ship in `Data/Managed`. The accurate constraint is that
  the game creates no Canvas and no EventSystem, and **IMGUI composites above every Canvas** (`GUI.depth`
  orders IMGUI against IMGUI only), so uGUI is reachable but cannot go over the game's own UI except through a
  RenderTexture blit. The "no public access path" claims in `docs/ui-componentization-evaluation-zh.md` and
  `docs/ui-shared-library-design-zh.md` are wrong as written.
- The game's widget palette is `Verse.Widgets`' 18 Color fields - five (fill, border) pairs plus
  normal/mouseover/inactive, separator and highlight - with **no accent slot**, and vanilla chrome is
  `ButtonBGAtlas*` plus nine `AtlasUV_*` 9-slice rects, not flat fills. DarkGold matches the game's *content*
  palette, not its *widget* palette; never use "matches RimWorld's design language" to reject another consumer's
  theme. Related gap: `UiTheme` has one global `Border` and cannot express the game's per-surface
  fill/border pairing.
- `Verse.ModRequirement` = {packageId, alternativePackageIds, displayName} and `ModDependency` adds only
  download URLs, so **`modDependencies` cannot carry a version**: negotiation is mod code, reusing
  `RimWorld.VersionControl` and `ModMetaData.ModVersion` and **never `AssemblyInformationalVersion`**
  (it embeds the commit SHA). `Verse.ModAssemblyHandler` installs a global AssemblyResolve, so duplicate
  assembly names resolve by load order and the loser cannot tell without enumerating loaded assemblies.
- **Cross-mod assembly binding is proven in a running game** (US ships no FerriteLib payload, the carrier mod
  supplies it, settings page + camera overlay ran with no red text): `HintPath` + `<Private>False` is the
  settled design and the "second copy / `Private=true`" alternative is closed on evidence.
- **The library ships zero translation keys** (`Ferrite.UiKit` is nowhere in `Source/**`); its only fallback
  is an English `[FerriteLib.UiKit] fallback triggered ...` log line, and every player-visible fallback string
  comes from the consumer's own `fallback` delegate.
- **Camera+ help mechanics** (read from `Source/Settings.cs` at tag `v3.4.7.0` = the installed Workshop
  version): the right panel is **hover-driven with topic fallback** - `DrawHelp` resolves
  `hoveredHelpTitleKey ?? topic.LabelKey` and the hover fields are cleared at the top of every
  `DoWindowContents`, so **no pin state exists**. Every row type wires hover through `DrawControlHover`
  (which also paints a 3.5%-white row highlight). Tiers: 11 topics -> 12 groups -> control rows, each with its
  own `SettingsHelp_*` key; `helpExtra` appends dynamic amber text under the panel body; `SettingsNote_*`
  rows are runtime-state notes, NOT help. No `?` buttons and no tooltips. Nav 160px, help column
  `min(220, max(190, w*0.25))`, panel non-scrolling.
- **Hash archaeology is CLOSED** (ruling 2026-09-07): history was rewritten three times before the first push,
  so citations drift - stop chasing hashes and never rewrite history for them. A dangling hash in any doc is
  archaeology, not damage. Related NGS ruling (2026-09-06): the 81 historical blobs carrying the unreleased
  sibling mod's name stay as-is; no second rewrite.
- **`HANDOFF.md` is maintainer-local**: removed from published history and gitignored - not one of the three
  active memory files, and it never gates a session.

## FerriteLib spin-off (executed 2026-09-03)

- The library lives in the sibling repository `../ferritelib` as prerequisite mod `coahuilite.ferritelib`
  (canonical repo name `Coahuilite/FerriteLib`, public, default branch `main`); phase 0 (library-side prep)
  landed here first. US compiles against its payload and ships no copy.
- **One assembly, not Core+UiKit.** The visual core (`UiTheme`, `UiThemeDraw`, `UiFitAudit`, `UiKitFonts`,
  `UiFont`, `ITextMetrics`, `VerseFerriteTextMetrics`) has **zero** references to the page model - 7 of ~38
  files, ~10% of 5,114 LOC - so the boundary is enforced by a mutation-checked gate in the library's own harness
  instead of by an assembly edge. Revisit only if a consumer needs the core without the DLL.
- **The second real consumer is `coahuilite.nivariansgrandstructure`, and it is not page-shaped**: a
  full-screen transparent world-map window that flows its panel rects by hand and keeps interaction state in
  private fields. Hence **physical independence now, API freeze not** - freezing before that consumer is wired
  calibrates the contract to n=1.
- Coupling measured for the record: ~90 IMGUI/Verse touchpoints in 7 files, ~4,300 LOC (manifest / registry /
  typed binding / session / layout / creation-time validation / fit audit) backend-independent. "Abandon IMGUI"
  was never the lever for "get retained mode": state ownership is already retained, only submission and node
  identity are not.
- `VerseFerriteTextMetrics` was `internal` inside consumer one while being the interface's only production
  implementation - that is why it lives in the library. Do not move production adapters back into
  `Source/UniversalSqueaker/`.

## UI engineering rules (durable)

- **Cutover facts**: the legacy chain is deleted, `UiKitFonts` is the only `UiFont`->`GameFont` mapping,
  and the settings window and diagnostics panel draw through `UiTheme`/`UiThemeDraw` only (the dual-palette
  root cause is closed). Whole-page fallback is the terminal `pageUnavailable` notice, and since FL 0.3.0 P2
  that machine plus all window chrome are `UiWindowHost`'s. Do not "restore for parity" the caller-less
  deletions. The only surviving secondary surface is the ~20-line pure-Verse camera readout in the overlay patch.
- **Substring ban**: no type under `Source/UniversalSqueaker/UI/**` may contain `UiText`, `UiPanel`,
  `Palette`, `SurfaceFrame`, `UiValueStore` or `UiInteract` - the gate scans exactly these six, which is
  why `UsTheme.cs` lives at the namespace root. net472 has no runtime
  `string.Contains(string, StringComparison)` and no `[NotNullWhen]`; `"Key".Translate(args)` binds
  `Verse.TranslatorFormattedStringExtensions`, so stubs must carry that type.
- **Text fit**: the "long Chinese clips" premise is measurement-false (6 of 152 keys were wider in Chinese, by
  one half-unit; a CJK glyph costs one em, Latin averages half) - the real causes were hardcoded prose and
  sub-line bands. `ITextMetrics` = `MeasureText` (wrapped height) + `MeasureWidth` (single line), and the
  audit hooks the single `UiThemeDraw.Label` outlet, dedupes per element+text and caps at 48 findings. **Its
  default axis is height; width is opt-in `singleLine` only**, because Verse wraps into the rect and a
  width-first check flags every paragraph while hiding the band defect. Every band must measure or use a
  calibrated line height, and its string must resolve through ONE path shared by Measure and Draw.
- **Metrics harness**: layout and audit share ONE wrap-aware `ITextMetrics` instance, never a hard-coded one.
  Stub one-line heights are calibrated from in-game `ui.text.overflow` need values (tiny 18.0, small 21.33333,
  medium 30.0); a constant stub turns every height assertion into theatre and a width-axis positive control will
  not reveal it. Real glyph advance exists only in game.
- **Popup**: `UiPopup` is the single owner of popup geometry/input (below/flip/pin/clamp +
  `SetPopupRect`), and both the manifest widget and the US composite delegate to it. The library proves the
  yield guard GIVEN a published rect; the consumer proves publication through the real production host. Inside a
  scroll container the event pointer is group-space while published rects are window-space - translate through
  the caller's own `ctx` (`UiNative.PointerPositionIn`). Stub input harnesses must pump real event passes
  with hot-control capture, event consumption and group-origin-relative pointers; faked hit-testing cannot
  express click theft at all.
- **Cache clock**: any cache whose contents feed layout must expire on the session `ContentRevision`, never a
  wall/Unity frame clock - two clocks guarantee a one-bump staleness window (the 2026-09-04d filter
  misalignment).
- **Localization contract**: manifests carry `TitleKey` only; every player-visible string resolves through
  Keyed; machine tokens (binding names, persisted filter/tab tokens, `SqueakActionScope`, element ids) are
  never translated; Def dropdowns display the Def's own `LabelCap` while writes stay machine tokens. The gate
  asserts key-set parity, non-empty values, `{n}` placeholder multiset equality and referenced-key existence,
  **and its regex alphabet must cover multi-segment keys**: the original pattern left 72% of shipped keys (152 of
  211) unguarded, so a green "zero missing" over a blind subset was worse than no gate. Derived/concatenated
  keys are prohibited (the scan only sees literals). The fmt=2 registry has 10 events incl. DevOnly
  `ui.text.overflow`.
- **Chinese term** (ruling 2026-09-04): xenotype = **异种**, never 异型/异形.
- **UI boundary containment (verify-local gate 14)**: raw renderer calls (`GUI`, `Event.current`,
  `Mouse.IsOver`, `Widgets.*`, `GUIUtility`) are confined to a **1-file whitelist** - only the frozen
  camera-indicator patch; the dev panel's exemption retired with the 2026-09-10 migration, landing the
  only-shrink ratchet. Hover reads `UiNative.IsMouseOver`, close affordances read `UiNative.Button`, chrome
  is the library's, and a raw `Mouse.IsOver` anywhere fails it. An undeclared hit, a vanished exemption file
  or a nonzero hover count fails it, and the gate self-tests its scanner before trusting a green. It is the
  consumer half of FerriteLib's containment gate, so the two whitelists must agree entry by entry. The canary
  stays the settings window: its UiKit failures surface untouched, never hardened.
- **Packaging**: `AssemblyInformationalVersion` embeds the full commit SHA, so **every commit - docs included
  - changes `1.6/Assemblies/*.dll` bytes**: never claim "no shipped-byte change" for a rebuilt package and
  never swap a maintainer's in-flight test build. One staging engine (`scripts/stage-package.ps1`) serves both
  channels, thin packers own only flavour identity, and the channel is **measured, not declared**. The release
  archive writer lives in the engine (names normalised to `/`, sorted, every entry stamped with the tagged
  commit's author date, one top-level `UniversalSqueaker/`), so the quoted SHA-256 is reproducible; Workshop
  identity is excluded by construction and build debris is asserted absent. `US_STEAM` is a code axis with
  **no build axis** - do not debug the dead branch.
- **Carrier lockstep**: pre-1.0, any library public-surface change bumps `Api.Minor` and
  `About.xml modVersion` together - **except** the standing 2026-09-20 ruling that during the coordination
  phase FL may add surface inside `0.7.x` with no minor bump. US records the verdict in
  `PrerequisiteVerified` and short-circuits to a named `US.Settings.Prerequisite.*` notice instead of
  drawing. The kernel-host lane reads the pin **out of `Mod.cs`** rather than repeating it and asserts
  one-minor-wide plus floor-equals-linked-Api, because a restated constant is how a lockstep gate starts
  agreeing with itself. The 2026-09-04b desync (`TypeLoadException` on `UiPopup` under a stale carrier) is
  the proof case. `pack-dev` labels itself from the csproj `<Version>`, so the dev folder reports US's
  product axis while the sibling carrier's own dev package reports its own - frozen-axis discipline, not drift.
- **US -> FL request classification (binding policy, 2026-09-19)**: classify BEFORE asking and write the
  classification down. **(A) US misuse / US's own job** - US relied on incidental behaviour FL never contracted,
  or the need is satisfiable with US's own kinds: fix here, file NO request. **(B) a genuine FL gap** - and it
  must be a GENERAL capability (neutral, symmetric with an existing general property, useful to a consumer that
  is not US), with that generality argument stated explicitly. Worked example, do not re-litigate: the
  diagnostics `diag-nav-col Width="Auto"` break is **(A)** - the 1px collapse was never contracted and the
  general `VisibleKey` already existed.
- **Responsive vocabulary is already in the carrier US builds against**: `Width="Auto"` resolves to a
  measured text-natural column (`MinWidth`/`MaxWidth` clamped, fixed siblings subtracted first), and a
  container may declare `Breakpoint` with `Narrow`/`Cols`/`NarrowCols`/`NarrowHidden`; a narrow-state
  attribute with no governing `Breakpoint` is refused at creation. Both US workflows check the carrier out
  **without a `ref:`**, so CI compiles against FL's default branch - "wait for the next minor" is not an
  argument against declarative sizing. Caveat from the library's own memory: its harness proves the wiring
  against a linear character-count width model, so a real fit stays an in-game question.
- **Row `Width="Auto"` semantics CHANGED at FL 0.7 (A1)** - the 0.6-era "Auto on a Row collapses to 1px"
  reading is SUPERSEDED. Under 0.7 that child joins the ordinary unsized/flex distribution and takes a SHARE of
  the leftover (a declared `MinWidth` still rescues it into the Auto bucket). What A1 retired is "the same
  element behaves differently in the two container shapes"; the supported replacement is FL's documented
  migration - **two mutually exclusive presentations + `VisibleKey`**, one child per shape. A narrow-only
  *child* has no single-attribute form in 0.7 (`WideHidden` registered, not implemented). Two traps: an
  unresolvable `VisibleKey` stays **VISIBLE** (fail-soft, one deduplicated finding), so key-name drift shows
  both presentations at once; and a read-only `VisibleKey` announces no revision, so whatever flips it must
  move the content revision itself (`CommitNotifications` bumps the clock only for ANNOUNCED keys).
- **The engine registers declared keys only** (`Bind`/`ActionBind`/`OptionsBind`/`Tab`/`VisibleKey`/
  `Items`); it does not track a composite widget's internal C# getters, so a notification must be mapped onto
  the owning element's Id.
- **The write keys are registered centrally since 2026-09-24 (adoption plan §9 item 6 / P3-2a).** `IUiBindings`
  has no "enumerate every write key" surface, so `UI/Kernel/UsWriteBindings.cs` is a funnel every
  SETTINGS-PAGE write registration goes through; it records (key, kind, item-scoped) and the kernel-host lane
  enumerates that registry instead of a hand-list. **The counts, with their units, so nobody pits two "correct"
  numbers against each other (measured 2026-09-24 at HEAD 5e88904, re-checked statically after the change):**
  **45** = write REGISTRATION CALL SITES in `UsKernelSettingsHost.cs` (`BindValue` 22 + `BindAction` 20 +
  `BindCommand` 3; includes `active-tab` and counts the two item-scoped sites once each); **43** = distinct
  LITERAL keys (45 minus those two item-scoped sites); **3** = distinct item-scoped TEMPLATES
  (`race-rows.<item>.select-domain`, `xenotype-rows.<item>.select-domain`,
  `checklist-pack-keys.<item>.enabled` - three, not two, because the `LayerRowBindings` site is executed by
  BOTH families, each with its own `itemsKey`); **46** = distinct REGISTRY KEYS = 43 + 3 = the lane's probe
  count (the probe table is keyed by registry key, hence 46 = 45 + 1). `active-tab` is in all of them; nothing
  is excluded. `UI/Diagnostics/UsDiagnosticsHost.cs` adds **12** more call sites on its own host and its own
  `DiagRevisionBumper` clock and is NOT in the funnel (named next adopter) ⇒ **57** write call sites in US
  `Source` today. The retired hand-list covered **21** of the settings page's 45, so the adoption plan's
  "20 of 46" was stale in BOTH halves - and its 46 is not this 46 (that one was `BindValue`+`BindAction` call
  sites, this one is distinct registry keys). The lane asserts probe-set == registry-set in both directions (a
  new key with no probe reddens, a stale probe reddens) and
  `UiSourceInvariantTests.VerifyWriteBindingsGoThroughTheRegistry` reddens on any raw
  `.BindValue`/`.BindAction`/`.BindCommand` under `UI/` outside the funnel and the one named, reasoned,
  count-pinned exemption.
- **The text-fit audit is per HOST since 2026-09-20 (FL-20)**: `UsTextFitAudit` is a per-window scope over
  that window's own `UiHost.Diagnostics` subscription, drained once per pass and once at close, while the
  process-wide `UiFitAudit.Enabled` stays reference-counted. **The boundary, easy to overclaim**: the gain is
  ROUTING and RULER ISOLATION, not "subscribing removes every interference" - `Check` still returns
  immediately while the shared `Enabled` is false and `Detach` still exists. A subscription is a BOUNDED
  RING, so drain per pass. The old shared route's misattribution was **never reproduced** - keep it as an open
  condition, never as "pollution already happened". Lane: `UsAuditRoutingLaneTests`.
- **Cross-space rect comparison is a lane trap (2026-09-21)**: an element inside a `Scroll` has its ARRANGED
  rect in the containing space while the rect handed to `UiNative` is in the SCROLL's local space
  (`ToDrawRect` subtracts the scroll container's own position). A lane filtering recorded slots with
  `IsInside(r, card)` across the two passed only by alignment; a 60px shift then made it pick a LATER
  section's row, and the write stopped while the product was fine. Translate both rects into one space first.
- **The page frame is guarded by a lane before it changes**: `FrameGeometryLaneTests` sweeps five page
  viewports (1280 / 1024 / 736 / 480 / 320) x EN/ZH x drawer open/closed x five workspaces and asserts no
  degenerate rect, containment, cross-parent overlap **in one coordinate space**, and the three-column/stacked
  shape. Its first run reddened on a real defect no other lane saw (the width-fixed `nav-column` also declared
  `Fill="true"`, so the stacked frame gave it a 212px height share while `us/nav` measures 271px). Overlap is
  checked ACROSS parents on purpose - the engine's flow distribution makes sibling overlap impossible, so a
  sibling-only lane tests nothing. Containment and cross-parent overlap have mutation proofs; finite/non-negative
  and "the scroll box is real" are guards only and are not evidence.
- **Surface table**: the reviewed palette lands as `Source/UniversalSqueaker/UsTheme.cs` (namespace root,
  `public` because the kernel-host harness references the production DLL), mapping the spec's tokens onto
  library slots. **accent is deliberately not set** - it stays the library's series gold, and a lane asserts the
  table's accent still equals the library default, so a silent rebrand fails a gate. Two written trade-offs: one
  `Hover` token serves both row hover and the shell's close affordance (the spec's two-step hover collapses to
  s2, rows winning), and slots the spec does not define keep library defaults.
- **`UsAttention` is US's attention role, not a theme token** (`Source/UniversalSqueaker/UsAttention.cs`
  owns the hue `#86E7D8`, the dark ink `#0f1116` for text ON that fill, and the substrate draws). It is
  US-side because `UiTheme.Warning` is the carrier's redirect onto `Danger`, so cyan there would repaint
  every destructive control. `RowRail.Attention` is a 2px cyan edge over a neutral fill (location rails stay
  3px). The four checklist banners split by role: conflict and an unloaded target are attention, a dormant
  domain takes the unavailable/hatch treatment, and only the destructive "Forget dangling" BUTTON keeps danger
  (the footer save-status "Failed" keeps danger on purpose).
- **Diagnostics panel rules (round-9 contract, 2026-09-10)**: entry is the debug action ONLY (its key is
  `DebugAction_<MethodName>`, so a rename must re-key both language tables in the same commit); settings
  stays the canary and gains no panel affordance. A dev-only page may build its Schema=2 XML IN CODE and feed
  the real parser. Widget `Height` attributes override Measure, so collapse-governed pages must omit them.
  `lastDispatched` is the single audio-attribution outlet (G16 tri-state; failures never clear the row);
  four-tier labels show the supplying tier as-is, and cross-tier pass-through is deliberately NOT a label (it
  would extend kernel `ChainResult` and force a golden-corpus regeneration). The two-press Esc never consumes
  the event by design: an observed leak is FL round-4 material, never a whitelist entry. All 16 conditions state
  their evidence basis - `IsCurrent` marks rows 0-12 as current observations while rows 13-15 read the
  remembered evaluation, which matches the ACTION key and is therefore NOT freshness.
- **Narrow mode is an owned widget, not a carrier capability**: the carrier refuses `Tab` on containers at
  creation time, so the diagnostics spec declares `Breakpoint=592` and the page composes a US-owned
  `us/diag/navbody`; the two shapes are mutually exclusive behind `VisibleKey`, and the host's
  `ApplyContentWidth` must move the content revision when the fed width crosses the threshold because a
  read-only binding announces none.
- **Repeated-rework root causes** (the per-incident narratives are in the git log): the kernel path removed
  dual coordinate spaces with late popup drawing and deferred hit-rect registration, but it did NOT remove
  "nothing enforces that narrow widths keep controls reachable" - which is why the same defect reappeared once
  before the geometry assertion existed. **Process cause**: completion was repeatedly declared faster than
  review could match, and no gate covered real-resolution, Chinese-text or real-catalog behaviour.

## Settings page: window policy, frame and manifest shape

- **Vanilla's options window is a FIXED size that does not scale with resolution**:
  `RimWorld.Dialog_Options.InitialSize = (650, 600)` (1.6 source; `CategoryListWidth` 160,
  `OkayButtonSize` 160x40), confirmed by the maintainer's 1024x768 screenshot. That is US's baseline.
- **Current window policy** (`Source/UniversalSqueaker/UI/Layout/WindowChromeLayout.cs` - those constants are
  the single source of truth): `w = RoundUpToStep(clamp(min(0.5*screenWidth, 0.9*screenHeight*4/3), 800,
  1600))`, `h = w * 3/4`, and `open = min(closed + 332, max(closed, screenWidth))`. The 800 floor is the
  smallest 4:3 box covering vanilla's own dialog, the 1600 ceiling stops a 4K screen getting a
  near-full-width dialog, and every width is a multiple of 4 so the height is an exact integer. 1024x768 ->
  **800x600**; 1920x1080 -> 960x720; 2560x1440 -> **1280x960**; 3840x2160 -> 1600x1200. **The window opens
  narrow** and widens by 332 (the 320 help column + the 12px row gap; it was 188) when Help expands.
  `DrawerWidensTheWindow(screenWidth, screenHeight)` answers that purely from the SCREEN with a half-pixel
  tolerance and is what decides the narrow presentation. **Anything restating 0.44/16:9,
  `clamp(0.24*screenWidth, 600, 860)` or `max(600, 0.66*screenHeight)` is stale**: the old portrait policy
  opened 614x950 at 2560x1440, and that - not the width - is what the 2026-09-14 feedback was really about (it
  recorded `page-root/footer` needing 47.3px in a 28px band, `global-volume` 32.7/18, `checklist` 32.7/20).
- The collapsed diagnostics bar had been clipping its own text (two 14px lines in a 32px strip while Tiny needs
  18 and the identity line draws at Small, ~21.3); fixed by `StripLineHeight` 14 -> 18, a new
  `TitleLineHeight` 22 and `BarHeight` 32 -> 44.
- **Frame and manifest shape** (`Source/UniversalSqueaker/UI/Layout.Schema2.xml`; gate 11 asserts exactly the
  two shipped Schema=2 manifests): nav column 200, help column 320 with `MinWidth=260`, `body-row`
  `Breakpoint=500`, support rows sharing one control column, cards reserving ONE subtitle line (the single
  documented `SubtitleLines` constant is the revert point). **The help catalog is 46 items and the count is
  pinned** in `tools/UniversalSqueakerUiLogicTests/Program.cs`, and
  `VerifyHoverClaimsMatchCatalogItems` is bidirectional (claim <-> catalog), so a catalog edit and its widget
  claim land in the same change.
- **The help drawer is DECLARATIVE, and node identity is why**: `help-scroll` carries
  `VisibleKey="help-open"` and `UI/Layout/UsLayoutVariants.cs` is DELETED. `help-open` stays the page's own
  value binding, so help visibility is independent state and is never `Tab` (a container refuses `Tab` at
  creation time anyway). `SessionRevisionBumper.Bump()` is **still required**, because the layout snapshot
  cache compares `cachedContentRevision`. `UiSession.PruneNodesExcept` releases a node whose identity the
  definition no longer declares together with its `scrollPositions` entry, so a variant that removes the drawer
  destroys its node and scroll position on every close: **keeping the element IN the definition is exactly what
  preserves node identity and `ScrollPosition`**. That is a **0.4 -> 0.5 public-observable semantic
  reversal** (a removed identity releases its node, a hidden one keeps it) - not a 0.6 regression and not an
  original US defect - established by a controlled A/B on ONE source tree where only the two carrier
  `HintPath`s differed.
- **US's layout granularity stops at the CARD**: the manifest declares containers, workspaces (`Tab`),
  breakpoints and the card list, while a card's rows are widget C#. **"Add one option" is never a data-file
  edit** - the eat port's floor was about 8 source files + 2 language tables + 1 ledger + 1 geometry lane + 1
  help count pin. FL ships 17 widget kinds plus 11 container kinds and US declares **zero**
  `Repeat`/`<Templates>`, so the row-level half is **US's own backlog (bucket (A))**, not a carrier limit;
  the one genuine remaining FL gap is **hierarchy x inline composition**, priced at 2 widgets / 933 code lines
  in spec 5.1.
- **Style is half-file-driven; US sits at the coarsest level it chose.** FL's machinery is real (one parser,
  two text origins - a standalone `<Styles>` file via `ParseFile` or the manifest's embedded section US
  uses - 26 colour tokens + 5 density metrics + font, page/element nearest-first `Scheme`/`Density`,
  resolve-before-Measure riding the theme's `LayoutRevision`, fail-soft drops recorded to the fit audit), but
  US's shipped use is the density axis only (regular/dense `RowHeight` 24/20) while the palette stays the
  `UsTheme.cs` C# factory: deliberate and partial, not a carrier gap. The web preset-kit ladder for the
  record: Bootstrap build-time SASS variables ~ FL's embedded `<Styles>`; MUI runtime `createTheme` scopes
  ~ element-level `Scheme`/`Density`; AntD v5 per-component tokens ~ the escape hatch US still owns in C#
  (`UsKernelDraw` per-row paint). A future "raise US's style granularity" ask must name which rung.

## Cross-repo state (US <-> FL)

- **Dispositions**: `../../modding_documents/team-mode/fl-to-us-2026-09-19-zh.md` (authority order: code >
  FL `MEMORY.md` > that file; `HANDOFF.md` section 6's "未处置" table was STALE and is corrected there).
  The single findings ledger is `../../modding_documents/team-mode/fl-uikit-issues-zh.md` - reference it,
  never copy it. Per-item (A)/(B) buckets and the open Batch 2 surface live in `TODO.md`.
- **The carrier payload's configuration has ONE judge: `AssemblyConfigurationAttribute`**
  (`scripts/read-assembly-stamp.ps1`), never the version suffix - FL's `<VersionSuffix>dev</VersionSuffix>`
  is unconditional and only the release channel erases it, so a correct Release carrier still reads
  `0.7.0-dev+<sha>` and a suffix-keyed gate would refuse a good payload. FL's delivery order: commit everything
  -> gates -> forced Release rebuild -> delete the Dev-leftover PDB -> report identity, and `-PackDev` is never
  the last step.
- **Provenance must be COMMITTED before FL may cite it** (FL's promotion rule: the citation is
  `owner/repo@sha:path:line`). A requirement whose only evidence is a working-tree document is not yet citable.
  Corollary for US: commit the doc that is the provenance of an FL ask before filing the ask.
- **WAVE-1's five gaps G1-G5 are ALL closed on the same carrier** (element-level `HelpKey`,
  `input/button` `PayloadKey`, `Chrome="none"` + `Height="Auto"`, `mode-row` `TitleKey1..8` +
  per-option hover help, banner roles). Registers: `Tone`/`Emphasis` are literal-only = CONFIRMED;
  `chrome/banner` has no `Tone` = CONFIRMED (source); `state/empty` takes no `Bind` = NOT PROVEN;
  no `MinHeight`/`reserved band` = NOT PROVEN. Durable consequence: "layers stay composite because G2 is a
  proven gap" no longer holds - the remaining blocker there is hierarchy x inline composition. Spec 5.3/5.4 keep
  their wording behind a superseded banner; the re-adjudication is the maintainer's.
- **The checklist card's body is declarative since 2026-09-20** (`cdcc675` + `340526f`): the carrier's first
  consumer-side `Repeat` + `<Templates>`, the first use of the engine's element-level `HelpKey` (G1's
  answer) and the first `Tab`-gated container. The projection owns its per-item binding namespace by
  registering exactly the rows it just named, because the registry has no prefix resolution and the pack set is
  a runtime projection. **The durable economics, not the narrative**: the widget went 431 -> 236 lines (code
  322 -> 154) while `Source/**` went **net +68** - the scaffolding (projection, per-item namespace, the
  does-not-lie lane, the card-probe re-derivation, the two help-gate widenings) is roughly 60% reusable and the
  per-widget remainder is dominated by the interaction shape. So "migrate 15 widgets" is NOT 15x the first one,
  and bucket (i)'s 1,969 code lines bound what can MOVE, not what can be deleted. Report: spec 5.7.
- **Runtime mode, RULED 2026-09-24 (maintainer)**: FL stays in its `0.7.x` **development** state (no line or
  minor move); the remote is **off-site backup only** - no tag and no release, and push is executed by the
  Lead alone; local testing uses **Dev packages only** (`build-dev` / `pack-dev`), never
  `pack-release` / `pack-steam`. Consequence: this round has no release vehicle, `main` and tags stay
  untouched, and an acceptance claim rests on the paired Dev folder, not on a published artifact.

## Release state and version scope

- **Current release: `v0.4.0-rc1` on both repositories, prerelease; `/releases/latest` 404ing is the
  CORRECT state in an rc-only window** (local tags `v0.2.0-rc1`, `v0.4.0-rc1`). A bad rc is fixed by
  deleting the release AND the tag and re-cutting the SAME rc number from a fixed head. Series conventions: the
  asset name is exactly `<Mod>-<tag>.zip`, the zip roots at a top-level `<Mod>/` so unzipping into `Mods/`
  yields a valid mod directory, and the identity axes freeze at the base during an rc window (the rc suffix
  lives only in tag + artifact name) with `main` returning to a dev suffix only at a deliberate stable cut.
- **Release identity is set in the repo, never injected**: US's csproj pins `<Version>` explicitly, so
  `-p:VersionSuffix=` does NOT clear `-dev` (the carrier's trick does not transfer). Three assertions lock
  the axes: check-pack-readiness `-RequireReleaseMetadata`, release.yml (tag shape + tag==csproj==modVersion
  + ancestry), and the harness gate `PrerequisiteRangeTracksCompiledApi` reading the carrier Api **from the
  loaded DLL** - never by parsing `../ferritelib/Source/`, whose commit is whatever the local session left
  there while the DLL is the artifact actually compiled against.
- **CI dependency chain = checkout + full-tree sibling staging.** `actions/checkout` resolves `path` INSIDE
  `GITHUB_WORKSPACE` and throws outside it, so the workflows check the carrier out to `ci-ferritelib/` and
  then stage the WHOLE tree (minus `.git`) to the sibling path: the DLL-only variant passed gates 1-12 but died
  at gate 13, because `KernelHostTests` builds FerriteLib's stub assemblies in place and the licence gate
  compares `ferritelib/LICENSE`. The payload must be built INSIDE the checkout that has `.git` - otherwise
  the SDK reads no `SourceRevisionId`, stamps `0.4.0-dev` with no `+sha`, and gate 6 refuses the bytes as
  unattributable (the rc1 pipeline defect, fixed). The Runner SDK is pinned `10.0.x` so CI equals the evidence
  baseline (mixing majors across a shared `obj/` triggers NETSDK1047). `verify-local`'s harness gates run
  `dotnet run --no-restore`, so any workflow must restore EVERY csproj before calling it with `-NoRestore`.
- **Pre-push ceremony is deliberately minimal** (ruling 2026-09-06): `privacy-audit.ps1 -FullHistory` plus the
  mechanical final check. Do not re-add manual ritual.
- **0.5.x scope**: item A = file-driven **invocation** + hot reload (editing a layout or style file and
  REOPENING the window shows the change, no restart and no recompile; **adding/removing/changing widget KINDS is
  explicitly out of scope** because kinds are compiled C# - XML is the surface for USING components, C# for
  EXTENDING the vocabulary). Item B = appearance file-driven + tier-2 granularity actually used. Item C =
  structural migration, now S4. Scope discipline: a proposal that touches source is a 0.5.x item unless the
  maintainer unlocks 0.5.x in that same instruction; `0.4.x` took exactly one source item (eat granularity)
  and is otherwise closed, and no release claim may be made without the deferred in-game batch.
- **Eat-granularity port (LANDED 2026-09-14, `e414b71`, must-fix `712de50`)**: two-level controls - parent
  "Eat only during real nutrition" (`GainingNutritionNow`) and child "include drugs" (toil name
  `ChewIngestible`, an unconfirmed name fail-safing back to the whole job) - landing in Overview
  `us/basic-tuning` below the four existing rows (a whole-behaviour gate, so never scope-tree and not timing).
  Defaults `false/false` are a **compatibility policy**: rc1 shipped the job-level feel, so parent-off =
  WholeJob is never re-litigated. Add-only Scribe at `settingsSchemaVersion` 5; pure rule in
  `Pure/SqueakEatOccurrence.cs`; key family `US.Tuning.EatPrecision` + `.IncludeDrugs` with help ids
  `us/basic-tuning/eat-precision[-include-drugs]`; help catalog 44 -> 46. The manual in-game matrix remains
  the maintainer's and is NOT claimed.
- **MeowingKiiro** is the first skill-conformant production pack (`dist/voicepacks/MeowingKiiro/`,
  `saryaki.meowingkiiro.voices` / `US_MeowingKiiro_Kiiro`, scope Race -> `Kiiro_Race`, 15 actions /
  53 clips, author Saryaki; audio licence CONFIRMED, superseding the old "licensing unconfirmed" blocker). Raw
  clips were formatted in place with a pristine backup alongside; static checks green, in-game verification
  open. Records: `docs/audio-formatting-meowing-kiiro-zh.md`,
  `docs/voicepack-meowingkiiro-production-plan-zh.md`; authoring skill
  `.github/skills/us-voicepack-authoring/SKILL.md`.

## Pointers (authoritative sources; read these, not this file)

- Mod structure: `docs/mod-structure-reference-zh.md`. Release flow: `docs/release-runbook-zh.md`
  (inherited from SR and adapted for US: no Extras/audio/OGG-mirror/codemap checks; `version.txt`'s first line
  is `UniversalSqueaker <version>`).
- Scripts: `verify-local.ps1` (**15 gates**; numbering is append-only and the script's own `Invoke-Check`
  count is the authority - gate 14 is the UI boundary audit, gate 15 is harness stub coverage over the US
  payload with the exemption ledger `scripts/stub-coverage-exemptions.txt`; the four library gates and the
  neutrality guard live in `../ferritelib`), `build-dev.ps1`, `pack-dev.ps1`, `stage-package.ps1`,
  `check-pack-readiness.ps1`, `privacy-audit.ps1`, `ui-boundary-audit.ps1`,
  `read-assembly-stamp.ps1`. CI: `.github/workflows/ci.yml` (push/PR) + `release.yml` (tag `v*`).
- Harnesses: `tools/UniversalSqueakerKernelTests/` (links `Kernel/` + `Pure/`, replays the committed US
  0.1.0 golden corpus), `tools/UniversalSqueakerKernelHostTests/`, `tools/UniversalSqueakerUiLogicTests/`,
  `tools/UniversalSqueakerConfigCopyTests/`, `tools/UniversalSqueakerLogTests/`.
- Kernel compile set (rebuilt, zero-Verse, de-SR-ized): `Source/UniversalSqueaker/Kernel/`; pure funnel logic
  (also zero-Verse): `Source/UniversalSqueaker/Pure/`. The legacy root `Kernel/` and `Pure/` trees were
  deleted in the phase-4 cleanup and remain retrievable from git history.
- UI design and slice ladder: `docs/ui-redesign-0.7-zh.md` (section 0 carrier re-verification, section 2 frame
  XML, section 5 migration pricing, section 6 slices, section 6.1 the five viewports). Migration plan:
  `docs/us-ui-migration-plan-zh.md` (decision table, orphan inventory, tuning editor, layered action table,
  implementation order S1-S5, action-entry wrapper). Historical conflict only: `OBLIVIONIS.md`.

## Archived history (pointer)

- Everything moved out of this file lives in `OBLIVIONIS.md`: the pre-distill text of MEMORY and TODO, the
  session checkpoints 2026-08-24 -> 2026-09-10, the FL 0.3.0 migration round, the diagnostics round-9 audit,
  the US -> FL 0.4 migration and its append-only evidence, the pre-cutover architecture audit, and the
  gate-count history (12 -> 13 -> 14 -> 15 and back to 13 after the 2026-09-03 library split), together with the
  fact that `Palette` / `UiText` / `UiPanel` / `SurfaceFrame` / `UiInteract` / `UiValueStore` /
  `UiGuard` / `VoicePacksLayout` / the Schema=1 `Layout.xml` and the whole legacy page chain no longer
  exist.
- The docs sweep at HEAD still carries the OLD Gate-U constraint ("fallback must be retained / no clean
  cutover") in `docs/uikit-rebuild/**`; the old-UI removal is executed and supersedes it.
- `dist/ui-evidence/layout-sweep.txt` and the per-revision matrices are cold artifacts; the reviews live in
  `docs/review/**`.
