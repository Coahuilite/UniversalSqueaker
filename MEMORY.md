# MEMORY

> Durable facts and evidence pointers only. Session narrative, finished-implementation detail, raw logs and
> commit chains live in `OBLIVIONIS.md` (cold archive); per-incident history lives in the git log. The action
> surface, including the ONE collected in-game list, is `TODO.md`. Code and the carrier outrank this file.
> **The pre-compaction text of this file and of `TODO.md` is archived byte-verbatim in `OBLIVIONIS.md`
> "Memory compaction 2026-09-21c"** - read it only for a historical conflict.


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
- **Next big goal: S4 continues = the remaining workspaces** (Distance / Packs / Tuning / Presets), one
  failure-sensitive lane per workspace; the slice ladder is spec section 6.
- **Green evidence at the current revision**: harness `ALL PASS` / EXIT 0 and `verify-local -NoRestore` 15/15
  EXIT 0 on a Release carrier with no PDB. Gate counts, key counts and catalog counts drift - the script's own
  `Invoke-Check` count and the parity/count gates are the authority, never a number quoted here.
- **The (乙1)/U1 stale status rows in spec section 6 were corrected on 2026-09-21** in the S4-1 batch, and
  S4-1's own row was added there. The remaining known drift is §5.3/§5.4, which are deliberately kept
  verbatim as the record of why the pre-§0.4 conclusions were wrong.

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
- `pack-dev` builds `-c Dev --no-incremental` and produces a **folder, no archive** (a rehearsal is installed
  by dropping it in `Mods/`; the old dev zip was shaped wrong too, contents at the root); `build-dev` rebuilds
  the carrier `-c Release --no-incremental` and gate 6 asserts the carrier is Release-CONFIGURED, not merely
  present. The engine refuses a `dev` label over Release bytes and the reverse. The need is that Dev and
  Release share one `OutputPath` and up-to-dateness is judged per configuration: `pack-dev` after
  `verify-local` reproducibly produced `build=dev` over a Release assembly, which in US is not cosmetic -
  `US_DEV` gates Auto dev-logging and the footer revision.
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
