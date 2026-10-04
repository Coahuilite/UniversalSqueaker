# MEMORY

> Durable facts, rules and pointers only. Session narrative, finished-implementation detail, raw logs and
> commit chains live in `OBLIVIONIS.md` (cold archive); per-incident history lives in the git log. The action
> surface is `TODO.md`. Code and the carrier outrank this file.
> **Compaction history:** the pre-2026-09-21c text of this file and of `TODO.md` is archived byte-verbatim in
> `OBLIVIONIS.md` "Memory compaction 2026-09-21c"; the 2026-10-02 section summarizes substantive decisions
> removed in that consolidation. It is not a verbatim copy; the original prose remains in Git history.
> Read the cold archive only for a historical conflict.

## Current state (2026-10-02)

### V1 implementation checkpoint - real-game acceptance pending

The V1 visual refresh is **implemented and locally accepted** - technical acceptance only, with real-game
acceptance pending. It contains the **paint/spacing increment**: navigation cards paint their own
ordinary/hover/selected surfaces on the shared measured+hit rect (the outer navigation outline is gone; the
flat scope makes only `PanelBorder` transparent and keeps `Panel` INHERITED so a page re-tint still reaches
scoped cards), flat `Section` edges are explicitly transparent, Overview Section gaps are 8 and the volume
card's padding is 16. Original controls, bindings, translations, help and the committed report fix are
retained; the 83 declared control semantics/help/translations/state attributes are preserved.

PM completed the remaining two layout requirements using existing composition. `nav-column` is a 200px-wide
Fill Scroll: its natural five-card stack can exceed the clipped viewport and the last destination remains
clickable. Fourteen Overview parameter rows have a local 400px breakpoint and stack label/description above
the input when narrow. Small control slots retain the 36x30 switch band and 64px volume-number field; the timing
stepper stays a horizontal minus/field/plus group beneath its label. Widget identifiers, business/state/help/
translation declarations and window/help sizing policy remain unchanged. No new kind or library product edit.

All 15 normal US checks pass with the explicit Dev carrier. The real Host selects Presets through native
MouseDown/MouseUp after scrolling a 120px nav viewport over 271px content; central scroll stays independent.
Both language tables satisfy all fourteen wide/narrow row endpoint checks. Restoring the committed old nav
Column fails `NavOwnsViewport`; restoring the pre-responsive Overview subtree fails `NarrowLabelAboveInput`.
Those two clauses are faithful-revert proven; wheel feel, glyph rendering and final visual/report acceptance
remain game-only. Details and current shape: `docs/ui-redesign-0.7-zh.md` current checkpoint note.

V2 (Packs), V3 (Tuning) and V4 (remaining pages/help/footer) are FUTURE and unstarted. V1 is ready for the
combined revised-package game rehearsal; this is technical acceptance, not a claim of real-game acceptance.

**Frozen playable packages (2026-10-04):** FL `b31e2c3`, US `889f34b`, Demo `d972dbc`; the PM's
`v1-playable-package-manifest.json` records the rehearsal artifacts. Subsequent commits change HEAD,
not this package. FL/US payloads are Dev; Demo is a Release-configured consumer paired with the Dev carrier.

**Working direction:** specifications lead with purpose, ownership, main path and a few invariants. Validate
at responsible boundaries, then use established internal contracts directly. Hashes identify payloads, not
business correctness. Unexpected failures retain context and the original exception; isolation or recovery
is not success. Add tolerance for known business states or observed faults, guided by actual runtime feedback.

**Next session:** development uses a fresh DSH leader with PM-designated roles; the old session is memory-only.
PM prepares a separate observability patch after checking the real diagnostic-publish call path. This is pending,
not an implemented fix or global audit. V1 game acceptance follows the PM's `PLAYTEST-V1.md`; V2–V4 remain future.
### DIAG-FIX: repeated reports keep waiting for their own pass

Technically accepted. The consumer no longer treats a previous successful report as a reason to retire a new
request: `ConsumeLayoutReportRequest` clears on success or when capture is no longer `Active`/`Overlay`, and an
off/unavailable request is refused at request time so a later enable cannot satisfy it. PM replaced the lane
that mirrored the consume logic with a PRODUCTION `UsKernelSettingsSource` instance driven by bound requests on
a real `UiHost`; restoring only the old production method fails `TheSecondReportDescribesANewerPass`
(first 2, second −1). Default-off capture and the report output are retained. Demo's explicit Report writes the
complete retained dump **once per successful report** to a distinct `[FerriteLibUiKitDemo][ui-geometry]` entry
carrying host/session/pass - never repurposed onto `ui-audit`, never per frame, never auto-enabled.

**The old in-game failure is preserved as a FAIL**: the second same-window report did not appear in the game
build. The fix has technical acceptance only; **revised-package game verification is still pending.**

### Historical product checkpoint (2026-10-02, before memory consolidation)

FL `b31e2c3` (minimal `ModSettings` base for the production regression - no library product change), US
`33a0aad` (V1 paint/spacing partial), Demo `d972dbc` (one-shot geometry log outlet). All three trees were clean
when packaged; that US Dev package matches the product checkpoint and selected FL carrier. A later docs-only
commit moves HEAD without rebuilding that package. Read current package identity from its stamp. No publication,
no install.

### R4-B (integrated 2026-09-30)

The preset subtree composes through FL's shared `UiRowBand` with typed callbacks, and the native xenotype icon
arrives as consumer-supplied data from the Verse-side adapter (`VoicePacksPageModel.ResolveXenotypeIcon` is the
single resource lookup; `UI/Kernel` performs none). Native Def texture loading remains game-only. Detail:
`docs/r4b-preset-subtree-row-composition.md`.

### R4-A: a typed choice carries its value, and the row index is the bridge

- **The anti-pattern is gone.** `UsScopeTreeWidget` no longer binds the scope as text and reverses it with
  `Enum.TryParse`; the domain dropdown no longer joins and splits a `\u0001` token. Both commit the option's own
  typed value through `set-action-scope` / `set-tuning-domain`.
- **`UsKernelDraw.Dropdown<T>` is the typed form** over FL's `UiChoice<T>` (label + value); the trigger matches
  the current value by VALUE, never by label, and both it and the string overload share one
  `DrawDropdownCore` so the popup push, clamp/flip and owner-id hit rule exist once.
- **The payload bridge is the ROW INDEX** because FL's row list is still string-payloaded; matching by label
  would collapse two options that share a label.
- **`IUiTypedChoices` is deliberately unused in US**: it exists for a non-generic widget that cannot name the
  consumer's `T`, and `UsScopeTreeWidget` can name `SqueakActionScope?`, so it consumes the carrier directly.
- **Null-inherit stayed a value**: the Auto entry is a real `null` and a row with no own scope commits `null`,
  never a default.

### R3-B: layout diagnosis is an explicit per-host developer control

- **The geometry instrument is decoupled from logging.** `UsTextFitAudit.Open` no longer enables it; it is
  switched per host (`SetGeometryCapture` / `SetGeometryOverlay` / `RequestGeometryReport`), default OFF, wired
  to three real page commands. `!GeometryEnabled` right after `Open` is the decoupling's mutation proof.
- **`Open(host, auditFit)` separates the SCOPE from the POLICY.** Every US window opens its scope in every
  logging mode because the per-host commands resolve a scope by host identity - a command with no open scope is
  dead. `auditFit` is `SqueakLog.ShouldEmitDev` at all three call sites and gates ONLY the text-fit audit, so
  the FIT/RECOVERY policy is unchanged. **A window must open a scope before its geometry command can do
  anything**, and harness lanes must do the same or they test nothing.
- **Status watching is not status changing.** `GetDevGeometryStatus` is read-only; the capability probe asks the
  carrier once, caches the answer and restores `GeometryEnabled` to false, so only `SetGeometryCapture` leaves
  sampling on - and the cached answer is why a later explicit enable still succeeds.
- **Status vocabulary, so a release carrier is never a clean empty report:** `ScopeMissing` / `Off` / `Active` /
  `Overlay` / `Unavailable`, compiled in BOTH configurations. `geometryEnabled`, `reportPending`, the report
  payload and the scope registry are unconditional; only the carrier CALLS and `reportRequestPass` are
  `US_DEV`. That split made both configurations compile with zero warnings - no pragma, no disabled warning, no
  weakened refusal.
- **The report is one-shot, explicit and holds BOTH renderings of ONE capture.** `Publish` emits no geometry
  (normal fit/recovery reporting is untouched): the Report command is the emission path. `PublishGeometryReport`
  returns the pass it described or -1, waits for a pass NEWER than the one that existed at the click, then
  clears. `EmitGeometryReport` reads the snapshot and the dump together and retains both.
- **Nothing dangles**: a request made while capture is off/unavailable is refused at request time, and `Dispose`
  removes the scope entry in both builds.
- **The instrument's human outlet is the existing `ltrace` channel**, and a developer greps the player log for
  it: `SqueakLog.LayoutTrace` emits `[UniversalSqueaker] ltrace: <message>` and only under `ShouldEmitDev`, so
  no lane asserts it and none had to be re-cut. `UsTextFitAudit` writes one line per dump record as
  `ltrace: geometry <record>`, plus ONE refusal line per process,
  `ltrace: geometry unavailable: …` - on a Release payload the reason is "this payload has no layout
  instrument (a release library package)". That distinction is the whole of R3-B's B3: the developer must be
  able to see that the tool is missing rather than that the layout was clean. **The record CONTENT on those
  lines belongs to the carrier's own dump** - read the shapes from the carrier, do not restate them here.

## Diagnostics panel, attention role

- **Diagnostics panel rules (the round-9 contract; the nine ruling rounds are archived in `OBLIVIONIS.md`).**
  Entry is the debug action ONLY, and its label key is `DebugAction_<MethodName>` - **a rename must re-key both
  language tables in the same commit**. Settings stays the canary and gains no panel affordance. A dev-only page
  may build its Schema=2 XML IN CODE and feed the real parser (gate 11 asserts exactly the two shipped
  manifests, so no new embedded resource is needed). **A widget `Height` attribute overrides `Measure`**, so
  collapse-governed pages must omit it. `lastDispatched` is the single audio-attribution outlet (G16 is
  tri-state, and failures never clear the row). The four-tier labels show the supplying tier as-is, and
  cross-tier pass-through is deliberately NOT a label - showing it would extend kernel `ChainResult` and force a
  golden-corpus regeneration. **The two-press Esc never consumes the event by design**: an observed leak is FL
  event-seam material, never a whitelist entry. All 16 conditions state their evidence basis, and
  `UsDiagGateLine.IsCurrent` marks rows 0-12 as current observations while rows 13-15 read the REMEMBERED
  evaluation - which matches the ACTION key and is therefore NOT freshness.
- **Narrow mode there is an owned widget, not a carrier capability**: the page declares `Breakpoint=592` and
  composes a US-owned narrow nav/body shape; the two shapes are mutually exclusive behind `VisibleKey`, and the
  host must move the content revision when the fed width crosses the threshold, because a read-only binding
  announces none.
- **`UsAttention` is US's attention role, NOT a theme token** (`Source/UniversalSqueaker/UsAttention.cs`). It
  owns the one hue `#86E7D8`, the dark ink `#0f1116` for text ON that fill, and the substrate draws. It is
  US-side because `UiTheme.Warning` is the carrier's redirect onto `Danger`: assigning cyan there would repaint
  every destructive control. `UsTheme` stays the surface table and deliberately gains no attention token.
  `RowRail.Attention` is a 2px cyan edge over a neutral fill (location rails stay 3px). The four checklist
  banners split by role - conflict and an unloaded target are attention, a dormant domain takes the
  unavailable/hatch treatment, and only the destructive "Forget dangling" BUTTON keeps the danger family; the
  footer save-status "Failed" keeps danger on purpose, because a save failure is not a gate failure.

### R12-US: US owns its baseline; palette is colour-only; the switch is the carrier's

- **US owns the COMPLETE DarkGold baseline.** `UsTheme.SchemeXml` declares all 23 colour tokens, including
  **`AccentGold` `#d19a38`** (unchanged - the identity did not move with ownership), and the four per-surface
  edges are claimed at US's own shared-token values (`Border` `#575247` / `BorderStrong` `#6b6459`) - the colours
  US actually painted before the template was retired. The lane pins *claimed value == shared token*, not a hex.
  `SelectedBorder` stays **unclaimed on purpose** (its fallback IS the accent, and a flat scope aliases it away
  to spell "no box").
- **A palette may only move the colour clock.** `ColourRevision` moves on colour assignment;
  `LayoutRevision`'s writers stay `DefaultFont` and `Geometry`. `UiStyleResolver.ThemeFor` drops cached region
  clones when EITHER moved, so a re-tint repaints a scoped region while arrangement, node identity, focus, hover
  claim, scroll and open popups are untouched. Evidence splits in two: a recorded DRAW for "the palette reached
  the paint", and the session's own bookkeeping for "a colour is not a structural event" - the lane explicitly
  does NOT claim all-state redraw proof. Contract page: `docs/r12-us-theme-contract-zh.md`.
- **The nine ON/OFF controls are the carrier's `input/checkbox` with `Appearance="switch"`**; the US-only
  `us/square-toggle` is retired (its file, registration and lane subject - the lane was re-cut onto the shared
  kind, not deleted). Every band stays 36x30 and every control names the same bool in `Bind` and `SelectedKey`.
- **`UsFontMigration.cs` is DELETED**: the carrier's own legacy `<Font>` redirect now maps to a font metric and
  reports one issue naming the successor, so a consumer-side ledger was a second convention free to contradict
  it.

## Environment facts that are easy to get wrong

- **RimWorld's minimum supported resolution is 1024x768** (maintainer ruling). 800x600 is NOT testable in game:
  every "800x600 in-game re-check" item is closed as impossible-by-platform, not deferred. The harness keeps its
  800x600 sweep as deliberately-conservative narrow-tier evidence and the window keeps its 800x600 design floor
  as defensive geometry.
- `UIScaleSafeWithResolution` requires `w/scale >= 1024` **and** `h/scale >= 768`, so `UI.screenWidth` is always
  >= 1024. The narrow-help trigger is therefore the band where the drawer cannot widen the window: **logical
  screen width in [1024, 1131]** (a 1920 monitor reaches it only above UI scale ~2.5).
- **The fit audit's join is two halves**: `Enabled` only opens the switch; a host must ALSO hold its own
  `UiHost.Diagnostics` subscription. A host with no subscription is measured with a **null ruler** and measures
  nothing - which is what removes cross-window misattribution by construction.
- **A nested `dotnet build` whose graph has ProjectReferences fails silently here** (exit 1, zero warnings, zero
  errors, nothing at `-v n`) while the identical command succeeds with `-m:1`. `KernelHostTests.csproj` therefore
  passes `-m:1` to its four nested stub builds. Harness-only; no shipped byte moves.

## Evidence discipline (rules in `AGENTS.md`; these are the measured specimens)

- **Line numbers are not a stable coordinate.** A second edit to the same file must not read its region at the
  offsets the FIRST edit left behind. Re-read the file and guard the slice with its own first and last line.
  Measured 2026-09-22: a TODO re-cut clobbered a neighbouring section and was recovered by `git checkout` plus a
  guarded re-run.
- **The mutation flow's own BUILD can be the broken instrument.** Manifests and language tables are embedded
  resources; a `Copy-Item` restore preserves the old mtime, so an incremental build reused the previous
  mutation's assembly and the next mutation observed nothing (M5 reported M3's symptom). Touch the resource
  before every build.
- **Do not move a SHARED fixture's input to give one lane the case it needs.** S4-1's first cut changed the
  empty view's egg value for every file that constructs the fixture; the lane now drives its two states from the
  fixture's own shipped views, and the fixture stayed byte-identical.
- **Any lane that measures text must install the Keyed table per language**
  (`Program.SetTranslatorResolver(Program.ReadKeyedTable(language))`), or `Translate` passes keys through and the
  lane measures the literal key text - a measured red-for-the-wrong-reason.
- **A lane that counts DRAWN controls must draw the same frame its snapshot came from.** `DrawFrame` re-arranges
  at the viewport it is handed, so a census at another width counts a different layout (measured: one row read
  748px vs 524px, and a page with four bands reported "0 bands").
- **At the hit seam, a row's identity is DRAW ORDER, not geometry.** Every row control is handed its rect at its
  own group origin and drawn rects carry no window position, so a "first rect matching this shape" predicate let
  a press aimed at one row select another. Rely on manifest/draw order and prove the row by its PAYLOAD.
- **The engine does not hand the hit seam a control outside the drawn viewport** - a press aimed at a clipped
  row silently lands on a row above it, because the rect it does get is that other row's. Lanes that press every
  row must arrange a canvas tall enough.
- **Cross-space rect comparison is a trap**: an element inside a `Scroll` has its ARRANGED rect in the containing
  space while the rect handed to `UiNative` is in the scroll's local space. Translate both into one space first.
- **The recorder stores the PARAMETER THAT WAS WRITTEN, not the pixel that was seen.** `DrawBoxSolidColors` keeps
  RGBA, so a lane comparing RGB alone reads a raw token as if it were painted. Every colour lane therefore splits
  in two: the **written channel** asserts the token AND the alpha explicitly, and the **perceived channel** uses
  the composited value read from the widget's single source of truth, so no lane carries a second copy of the
  arithmetic. Real pixels are not directly observed here.
- **Two alpha guards are guards without their own red** (`a == 0.57`, the rail's opacity) because the minimal
  mutation also changes the product and needs its own byte-level restore; recorded as such, not dressed up.
- **Process rule earned the hard way: a commit requires an OBSERVED green, not an INVOKED check.** One red lane
  reached HEAD because the check and the commit were written into the same script.

## Carrier and artifact discipline

- `scripts/read-assembly-stamp.ps1` reads `AssemblyConfigurationAttribute` in a CHILD process (a handle on a DLL
  copied or rebuilt moments later is fatal; `MetadataReader` is absent from the Store PowerShell).
- `pack-dev` builds the US payload `-c Dev --no-incremental` and produces a **folder, no archive** (a rehearsal
  is installed by dropping it in `Mods/`). **`build-dev` builds the US payload Dev and NEVER builds the
  carrier** - it resolves the carrier it was handed and forwards it as `FerriteLibArtifactPath`. Gate 6 asserts
  the SELECTED carrier is Release-CONFIGURED, not merely present, and the stager refuses a payload whose recorded
  compiler-reference hash differs from the selected DLL. The engine refuses a `dev` label over Release bytes.
- **The carrier payload's configuration has ONE judge: `AssemblyConfigurationAttribute`**, never the version
  suffix - FL's `<VersionSuffix>dev</VersionSuffix>` is unconditional, so a correct Release carrier still reads
  `0.7.0-dev+<sha>` and a suffix-keyed gate would refuse a good payload.
- **Gate 6 reddens on any carrier commit, docs included**, since it compares the payload's identity against the
  carrier checkout HEAD. Never edit FL source to satisfy it: rebuild the payload and announce that HEAD moved.
  `verify-local`'s gate-6 retry hint is PROSE (task-7) precisely because a red gate is the frame that must not
  build - the 2026-09-22 incident rebuilt a frozen payload by pasting the old hint.
- **A hash and an mtime answer different questions**: the hash says "same bytes", the mtime says "was the file
  written". A rebuild of identical content moves the second only, so a freeze notice quotes BOTH.
- **A live carrier identity is never pinned in a tracked file.** A SHA written here is stale the moment it lands.
  The current identity is what the latest FREEZE NOTICE states, as a hash + mtime pair; this file keeps history.

## Identity and enduring corrections (verified against source; do not re-derive)

- Repo `coahuilite/UniversalSqueaker`; packageId `coahuilite.universalsqueaker`; namespace / Def prefix / log
  prefix = `UniversalSqueaker` / `US_` / `usdiag`; licence **MPL-2.0** series-wide. **Public since 2026-09-07**
  with branch protection live (force-push off, deletion off, no required checks). Workshop display name and the
  licence action stay maintainer-gated.
- US is the local fork of Squeaky Ratkin (SR), created 2026-08-23 from SR branch `0.3.x` tip `b19d68a`. **SR
  owns Ratkin exclusively**: US ships no `SqueakyRatkin.*` types and no Ratkin audio/content/profiles. The 0.4
  co-existence policy is both mods enabled with no `incompatibleWith`.
- **Content-pack packageIds namespace under the AUDIO AUTHOR's lowercase id**
  (`saryaki.meowingkiiro.voices`), never `coahuilite.*`.
- Reflective HAR discovery is **not implemented**: `Catalog/SqueakXenotypeCatalog.cs` carries `TODO(HAR)`,
  discovery is assembled-only and the hint lists are permanently empty. The `AGENTS.md` HAR sentence is intent,
  not the shipped build.
- `CompProperties_Squeaker` carries only `actions` and `moodMods`. Older notes saying 17 kinds are wrong; the
  Registrar's `us/*` set is pinned by `UiSourceInvariantTests` and the script's own count is the authority.
- **`Vanilla` silence is unfinished content, not design**: no built-in fallback/baseline Def ships, so
  `BuildBuiltInSource()` is always empty. Never explain it away as a property of the mode.
- **uGUI / UIElements are NOT absent from the game** (`UnityEngine.UI.dll`, `UIModule`, `Unity.TextMeshPro`,
  `UIElementsModule` all ship in `Data/Managed`). The accurate constraint is that the game creates no Canvas and
  no EventSystem, and **IMGUI composites above every Canvas**, so uGUI is reachable but cannot go over the game's
  own UI except through a RenderTexture blit. The "no public access path" claims in
  `docs/ui-componentization-evaluation-zh.md` and `docs/ui-shared-library-design-zh.md` are wrong as written.
- The game's widget palette is `Verse.Widgets`' 18 Color fields with **no accent slot**, and vanilla chrome is
  `ButtonBGAtlas*` plus nine `AtlasUV_*` 9-slice rects, not flat fills. DarkGold matches the game's *content*
  palette, not its *widget* palette; never use "matches RimWorld's design language" to reject another consumer's
  theme. Related gap: `UiTheme` has one global `Border` and cannot express the game's per-surface pairing.
- `Verse.ModRequirement` = {packageId, alternativePackageIds, displayName} and `ModDependency` adds only download
  URLs, so **`modDependencies` cannot carry a version**: negotiation is mod code, reusing
  `RimWorld.VersionControl` and `ModMetaData.ModVersion`, **never `AssemblyInformationalVersion`** (it embeds the
  commit SHA). `Verse.ModAssemblyHandler` installs a global AssemblyResolve, so duplicate assembly names resolve
  by load order and the loser cannot tell without enumerating loaded assemblies.
- **Cross-mod assembly binding is proven in a running game**: `HintPath` + `<Private>False` is the settled
  design and the "second copy / `Private=true`" alternative is closed on evidence.
- **The library ships zero translation keys**; every player-visible fallback string comes from the consumer's own
  delegate.
- **Camera+ help mechanics** (from `Source/Settings.cs` at tag `v3.4.7.0`): the right panel is hover-driven with
  topic fallback (`hoveredHelpTitleKey ?? topic.LabelKey`), hover fields are cleared each `DoWindowContents`, so
  **no pin state exists**. Every row wires hover through `DrawControlHover`. Nav 160px, help column
  `min(220, max(190, w*0.25))`, panel non-scrolling. No `?` buttons and no tooltips.
- **Hash archaeology is CLOSED**: history was rewritten three times before the first push, so citations drift.
  Stop chasing hashes and never rewrite history for them. The 81 historical blobs carrying the unreleased sibling
  mod's name stay as-is.
- **`HANDOFF.md` is maintainer-local**, gitignored, never one of the three active memory files, and never gates a
  session.

## UI engineering rules (durable)

- **Cutover facts**: the legacy chain is deleted, `UiKitFonts` is the only `UiFont`->`GameFont` mapping, and the
  settings window and diagnostics panel draw through `UiTheme`/`UiThemeDraw` only. Whole-page fallback is the
  terminal `pageUnavailable` notice, and since FL 0.3.0 P2 that machine plus all window chrome are
  `UiWindowHost`'s. Do not "restore for parity" the caller-less deletions. The only surviving secondary surface
  is the ~20-line pure-Verse camera readout in the overlay patch.
- **Substring ban**: no type under `Source/UniversalSqueaker/UI/**` may contain `UiText`, `UiPanel`, `Palette`,
  `SurfaceFrame`, `UiValueStore` or `UiInteract`; the gate scans exactly these six, which is why `UsTheme.cs`
  lives at the namespace root. net472 has no runtime `string.Contains(string, StringComparison)` and no
  `[NotNullWhen]`; `"Key".Translate(args)` binds `Verse.TranslatorFormattedStringExtensions`, so stubs must carry
  that type.
- **Text fit**: "long Chinese clips" is measurement-false (6 of 152 keys were wider in Chinese, by half a unit; a
  CJK glyph costs one em, Latin averages half). The real causes were hardcoded prose and sub-line bands.
  `ITextMetrics` = `MeasureText` (wrapped height) + `MeasureWidth` (single line), and the audit hooks the single
  `UiThemeDraw.Label` outlet, dedupes per element+text and caps at 48 findings. **Its default axis is height;
  width is opt-in `singleLine` only**, because Verse wraps into the rect. Every band must measure or use a
  calibrated line height, and its string must resolve through ONE path shared by Measure and Draw.
- **Metrics harness**: layout and audit share ONE wrap-aware `ITextMetrics` instance. Stub one-line heights are
  calibrated from in-game `ui.text.overflow` need values (tiny 18.0, small 21.33333, medium 30.0); a constant
  stub turns every height assertion into theatre. Real glyph advance exists only in game.
- **Popup**: `UiPopup` is the single owner of popup geometry/input (below/flip/pin/clamp + `SetPopupRect`), and
  both the manifest widget and the US composite delegate to it. Inside a scroll container the event pointer is
  group-space while published rects are window-space - translate through the caller's own `ctx`. Stub input
  harnesses must pump real event passes with hot-control capture and group-origin-relative pointers; faked
  hit-testing cannot express click theft at all.
- **Cache clock**: any cache whose contents feed layout must expire on the session `ContentRevision`, never a
  wall/Unity frame clock - two clocks guarantee a one-bump staleness window.
- **Localization contract**: manifests carry `TitleKey` only; every player-visible string resolves through Keyed;
  machine tokens (binding names, persisted filter/tab tokens, `SqueakActionScope`, element ids) are never
  translated; Def dropdowns display the Def's own `LabelCap` while writes stay machine tokens. The gate asserts
  key-set parity, non-empty values, `{n}` placeholder multiset equality and referenced-key existence, **and its
  regex alphabet must cover multi-segment keys** (the original pattern left 72% of shipped keys unguarded).
  Derived/concatenated keys are prohibited. The fmt=2 registry has 10 events incl. DevOnly `ui.text.overflow`.
- **Chinese term** (ruling 2026-09-04): xenotype = **异种**, never 异型/异形.
- **UI boundary containment (gate 14)**: raw renderer calls (`GUI`, `Event.current`, `Mouse.IsOver`, `Widgets.*`,
  `GUIUtility`) are confined to a **1-file whitelist** (the frozen camera-indicator patch). Hover reads
  `UiNative.IsMouseOver`, close affordances read `UiNative.Button`, chrome is the library's, and a raw
  `Mouse.IsOver` anywhere fails it. An undeclared hit, a vanished exemption file or a nonzero hover count fails
  it, and the gate self-tests its scanner before trusting a green. It is the consumer half of FerriteLib's
  containment gate, so the two whitelists must agree entry by entry. The canary stays the settings window.
- **Packaging**: `AssemblyInformationalVersion` embeds the full commit SHA, so **every commit - docs included -
  changes `1.6/Assemblies/*.dll` bytes**: never claim "no shipped-byte change" for a rebuilt package and never
  swap a maintainer's in-flight test build. One staging engine serves both channels; the channel is **measured,
  not declared**. `US_STEAM` is a code axis with **no build axis** - do not debug the dead branch.
- **US -> FL request classification (binding policy)**: classify BEFORE asking and write it down. **(A) US misuse
  / US's own job** - US relied on incidental behaviour FL never contracted, or the need is satisfiable with US's
  own kinds: fix here, file NO request. **(B) a genuine FL gap** - and it must be a GENERAL capability (neutral,
  symmetric with an existing general property, useful to a non-US consumer), with that generality argument
  stated. The single findings ledger is the FL issues file under the workspace's team-mode directory - reference
  it, never copy it into this repo.
- **Responsive vocabulary is already in the carrier US builds against**: `Width="Auto"` resolves to a measured
  text-natural column, and a container may declare `Breakpoint` with `Narrow`/`Cols`/`NarrowCols`/`NarrowHidden`;
  a narrow-state attribute with no governing `Breakpoint` is refused at creation. **Row `Width="Auto"` semantics
  CHANGED at FL 0.7**: the 0.6-era "collapses to 1px" reading is SUPERSEDED - that child now takes a share of
  the leftover. The supported replacement for shape-changing is **two mutually exclusive presentations +
  `VisibleKey`**, one child per shape; a narrow-only *child* has no single-attribute form in 0.7. Two traps: an
  unresolvable `VisibleKey` stays **VISIBLE** (fail-soft), so key-name drift shows both presentations at once;
  and a read-only `VisibleKey` announces no revision, so whatever flips it must move the content revision itself.
- **The engine registers declared keys only** (`Bind`/`ActionBind`/`OptionsBind`/`Tab`/`VisibleKey`/`Items`); it
  does not track a composite widget's internal C# getters, so a notification must be mapped onto the owning
  element's Id.
- **Write keys are registered centrally since 2026-09-24.** `UI/Kernel/UsWriteBindings.cs` is the funnel every
  SETTINGS-PAGE write registration goes through; the kernel-host lane enumerates that registry instead of a
  hand-list, and `UiSourceInvariantTests.VerifyWriteBindingsGoThroughTheRegistry` reddens on a raw
  `.BindValue`/`.BindAction`/`.BindCommand` under `UI/` outside the funnel and its one named exemption. Recorded
  brittleness: the guard counts TEXT occurrences, so a comment carrying one of those names would count too.
- **Legacy flat scopes set a surface's BORDER token equal to its FILL to hide the box** (the
  vocabulary has no `Border=none`; `UiThemeDraw.Surface` paints a 1px frame in the border colour). Current V1
  Section scopes instead use an explicitly transparent `PanelBorder`, preserving inherited Panel re-tints. **Durable
  pitfall: the engine resolves an element's `Scheme` against the DOCUMENT the Host was built with - the
  manifest's own `<Styles>` section - while `UsTheme.SchemeXml` is the in-code palette applied to the theme
  INSTANCE.** A scheme declared only in `UsTheme` is never resolved and the scope silently keeps the page's
  bordered values; the resolver records "unknown scheme" and falls back, while an unknown TOKEN is refused. So a
  new scheme goes in `Layout.Schema2.xml`'s `<Styles>` and declares **only the tokens it overrides**.
- **When you want the accent, NAME the accent.** `UiTheme.SelectedSurface` is `(Selected, SelectedBorder ??
  AccentGold)` and `SelectedBorder` IS assignable from a style document, so the flat scope setting it EQUAL to
  `Selected` **aliases the `?? AccentGold` fallback away** - inside scoped cards `SelectedSurface.Border` reads
  the selection fill colour. `UiThemeDraw.AccentRail`'s own fallback is `color ?? theme.AccentGold`, never
  `SelectedSurface`, so calling it with no colour is safe; a consumer that wants the accent passes
  `theme.AccentGold` or omits the argument. **The trap is the CALL CONVENTION, not the helper.**
- **A control that must be VISIBLE on a flat plane cannot take its material from the role of the plane it sits
  on.** The first square-toggle cut resolved the track through the role table and the OFF track's fill AND edge
  both equalled the plane behind it (`#191612` on `#191612`), so the control vanished. The material belongs to
  the control.
- **A re-tint can resurrect outlines in legacy equal-fill scopes** if their border tokens do not follow the
  fill. Palette guards retain those pairs and the boxed page control; V1 Section edges have their own transparent
  border assertion. The colour lane also measures contrast for the four inks against their surfaces.
- **Invalidation/notification**: a structural change is not a notification - rebuild the filtered list, the item
  bindings and the adapter mapping BEFORE announcing, or the UI hears about the new list while the old set is
  still held.
- **Repeated-rework root cause**: "nothing enforces that narrow widths keep controls reachable" - which is why
  the same defect reappeared once before a geometry assertion existed. **Process cause**: completion repeatedly
  declared faster than review could match, and no gate covered real-resolution, Chinese-text or real-catalog
  behaviour.

## Settings page: window policy, frame and manifest shape

- **Vanilla's options window is a FIXED size that does not scale with resolution**:
  `RimWorld.Dialog_Options.InitialSize = (650, 600)` (1.6 source), confirmed by screenshot. That is US's
  baseline.
- **Current window policy** (`UI/Layout/WindowChromeLayout.cs` - those constants are the single source of
  truth): `w = RoundUpToStep(clamp(min(0.5*screenWidth, 0.9*screenHeight*4/3), 800, 1600))`, `h = w * 3/4`, and
  `open = min(closed + 332, max(closed, screenWidth))` - the 332 is the 320 help column + the 12px row gap.
  **The window opens narrow** and widens when Help expands; `DrawerWidensTheWindow(screenWidth, screenHeight)`
  answers purely from the SCREEN with a half-pixel tolerance and decides the narrow presentation.
  1024x768 -> **800x600**; 1920x1080 -> 960x720; 2560x1440 -> **1280x960**. **Anything restating 0.44/16:9,
  `clamp(0.24*screenWidth, 600, 860)` or `max(600, 0.66*screenHeight)` is stale**: the old portrait policy
  opened 614x950 at 2560x1440, and that is what the 2026-09-14 feedback was really about.
- **Frame and manifest shape** (`UI/Layout.Schema2.xml`; gate 11 asserts exactly the two shipped Schema=2
  manifests): nav column 200, help column 320 with `MinWidth=260`, `body-row` `Breakpoint=500`, support rows
  sharing one control column, cards reserving ONE subtitle line (the single documented `SubtitleLines` constant
  is the revert point). **The help catalog count is pinned** in `tools/UniversalSqueakerUiLogicTests/Program.cs`
  and `VerifyHoverClaimsMatchCatalogItems` is bidirectional, so a catalog edit and its widget claim land
  together.
- **The help drawer is DECLARATIVE, and node identity is why**: `help-scroll` carries `VisibleKey="help-open"`
  and `UI/Layout/UsLayoutVariants.cs` is DELETED. `help-open` stays the page's own value binding, so help
  visibility is independent state and is never `Tab`. `SessionRevisionBumper.Bump()` is still required because
  the layout snapshot cache compares `cachedContentRevision`. `UiSession.PruneNodesExcept` releases a node whose
  identity the definition no longer declares together with its `scrollPositions` entry, so a variant that
  REMOVES the drawer destroys its node and scroll position on every close: **keeping the element IN the
  definition is what preserves node identity and `ScrollPosition`.** That is a 0.4 -> 0.5 public-observable
  semantic reversal (a removed identity releases its node, a hidden one keeps it).
- **(乙1) narrow help**: at a logical width that cannot host the widened window the open drawer **REPLACES the
  body** - `help-band` is a full-width `Scroll` with `Fill="true"` between `body-row` and `footer-band`, and
  `body-row` is hidden by its own host-derived key. Exclusion was forced by arithmetic at 1024x768 (header +
  footer + gaps leave 428 for body + band while the body's content floor is 271).
- **Short-body navigation is now a Scroll**: its Fill viewport can shrink while the natural five-card
  content remains clipped and reachable. The prior plain Column's 271px floor is historical; the narrow help
  still replaces the body by product policy. Reserved-band vocabulary remains absent (no container
  `MinHeight`/`MaxHeight`, no fill weight, no `HeightKey`).
- **US uses mixed declarative and composite rows**: the manifest owns containers, workspaces (`Tab`),
  breakpoints and many Overview/Timing controls; richer custom widgets still own some row compositions.
  Remaining row-level dissolution is US's backlog, not a general carrier limit; the genuine remaining FL gap
  is **hierarchy x inline composition**.
- **Style is half-file-driven; US sits at the coarsest level it chose.** FL's machinery is real (one parser, the
  manifest's embedded `<Styles>` section, 26 colour tokens + 5 density metrics + font, nearest-first
  `Scheme`/`Density`, resolve-before-Measure riding `LayoutRevision`, fail-soft drops recorded to the fit
  audit). US uses declared density and local surface-token overrides while the complete baseline palette stays
  in the `UsTheme.cs` C# factory: deliberate and partial, not a carrier gap. A future "raise style granularity"
  ask must name which rung.

## Cross-repo state (US <-> FL)

- **Dispositions**: authority order is code > FL `MEMORY.md` > the team-mode disposition file. The single
  findings ledger is the FL issues file under the workspace's team-mode directory - reference it, never copy it.
  Per-item (A)/(B) buckets and the open Batch 2 surface live in `TODO.md`.
- **FL's delivery order**: commit everything -> gates -> forced Release rebuild -> delete the Dev-leftover PDB ->
  report identity; `-PackDev` is never the last step.
- **Provenance must be COMMITTED before FL may cite it** (the citation form is `owner/repo@sha:path:line`).
  Corollary: commit the doc that is the provenance of an FL ask before filing the ask.
- **WAVE-1's five gaps G1-G5 are ALL closed on the same carrier** (element-level `HelpKey`, `input/button`
  `PayloadKey`, `Chrome="none"` + `Height="Auto"`, `mode-row` `TitleKey1..8` + per-option hover help, banner
  roles). Durable consequence: "layers stay composite because G2 is a proven gap" no longer holds - the
  remaining blocker there is hierarchy x inline composition.
- **Runtime mode, RULED 2026-09-24 (maintainer)**: FL stays in its `0.7.x` **development** state (no line or
  minor move); the remote is **off-site backup only** - no tag and no release; local testing uses **Dev packages
  only**, never `pack-release` / `pack-steam`. So an acceptance claim rests on the paired Dev folder, not on a
  published artifact.
- **Carrier pin `[0.7.0, 0.8.0)`** (`Source/UniversalSqueaker/Mod.cs`). **The carrier is identified by COMMIT**
  (`AssemblyInformationalVersion`, which gate 6 compares against the carrier checkout HEAD), **never by `Api`**:
  during the coordination phase FL may add surface inside `0.7.x` with no minor bump. US compiles against the
  sibling payload by relative `HintPath` with `Private=false`, ships no copy, and declares the dependency in
  `About/About.xml`. The kernel-host lane reads the pin **out of `Mod.cs`** rather than repeating it.

## Release state and version scope

- **Current release: `v0.4.0-rc1` on both repositories, prerelease; `/releases/latest` 404ing is the CORRECT
  state in an rc-only window.** A bad rc is fixed by deleting the release AND the tag and re-cutting the SAME rc
  number from a fixed head. The asset name is exactly `<Mod>-<tag>.zip`, the zip roots at a top-level `<Mod>/`,
  and the identity axes freeze at the base during an rc window with `main` returning to a dev suffix only at a
  deliberate stable cut.
- **Release identity is set in the repo, never injected**: US's csproj pins `<Version>` explicitly, so
  `-p:VersionSuffix=` does NOT clear `-dev`. Three assertions lock the axes: check-pack-readiness
  `-RequireReleaseMetadata`, release.yml (tag shape + tag == csproj == modVersion + ancestry), and the harness
  gate reading the carrier `Api` **from the loaded DLL** - never by parsing the sibling source tree, whose commit
  is whatever the local session left there.
- **CI dependency chain = checkout + full-tree sibling staging.** `actions/checkout` resolves `path` INSIDE
  `GITHUB_WORKSPACE`, so the workflows check the carrier out to `ci-ferritelib/` and stage the WHOLE tree (minus
  `.git`) to the sibling path: the DLL-only variant died at gate 13 because `KernelHostTests` builds FerriteLib's
  stub assemblies in place. The payload must be built INSIDE the checkout that has `.git`, or the SDK reads no
  `SourceRevisionId` and gate 6 refuses the bytes as unattributable. The Runner SDK is pinned so CI equals the
  evidence baseline; `verify-local`'s harness gates run `--no-restore`, so any workflow must restore EVERY csproj
  first.
- **Pre-push ceremony is deliberately minimal**: `privacy-audit.ps1 -FullHistory` plus the mechanical final
  check. Do not re-add manual ritual, and run it before the FIRST push of a line, not only before a release.
- **Product axis `0.5.x`; carrier pin `[0.7.0, 0.8.0)`.** `<Version>` moves only by maintainer ruling, and
  `main` moves only when an rc or stable is cut.
- **0.5.x scope**: item A = file-driven **invocation** + hot reload (editing a layout or style file and
  REOPENING the window shows the change; **adding/removing/changing widget KINDS is explicitly out of scope**
  because kinds are compiled C#). Item B = appearance file-driven + tier-2 granularity. Item C = structural
  migration. A proposal that touches source is a 0.5.x item unless the maintainer unlocks it in that same
  instruction.
- **Eat-granularity port (LANDED)**: two-level controls - parent "Eat only during real nutrition" and child
  "include drugs" - in Overview `us/basic-tuning`. Defaults `false/false` are a **compatibility policy**:
  parent-off = WholeJob is never re-litigated. Add-only Scribe at `settingsSchemaVersion` 5; pure rule in
  `Pure/SqueakEatOccurrence.cs`. The manual in-game matrix remains the maintainer's and is NOT claimed.
- **MeowingKiiro** is the first skill-conformant production pack (`dist/voicepacks/MeowingKiiro/`,
  `saryaki.meowingkiiro.voices`, scope Race -> `Kiiro_Race`, 15 actions / 53 clips; audio licence CONFIRMED).
  Static checks green; in-game verification open.

## Pointers (authoritative sources; read these, not this file)

- Mod structure: `docs/mod-structure-reference-zh.md`. Release flow: `docs/release-runbook-zh.md`.
- Scripts: `verify-local.ps1` (**15 gates**; numbering is append-only and the script's own `Invoke-Check` count
  is the authority - gate 14 is the UI boundary audit, gate 15 is harness stub coverage with the exemption
  ledger; the library gates and the neutrality guard live in the sibling FL repo), `build-dev.ps1`,
  `pack-dev.ps1`, `stage-package.ps1`, `check-pack-readiness.ps1`, `privacy-audit.ps1`, `ui-boundary-audit.ps1`,
  `read-assembly-stamp.ps1`. CI: `.github/workflows/ci.yml` + `release.yml`.
- Harnesses: `tools/UniversalSqueakerKernelTests/` (links `Kernel/` + `Pure/`, replays the committed 0.1.0
  golden corpus), `tools/UniversalSqueakerKernelHostTests/`, `tools/UniversalSqueakerUiLogicTests/`,
  `tools/UniversalSqueakerConfigCopyTests/`, `tools/UniversalSqueakerLogTests/`.
- Kernel compile set (zero-Verse): `Source/UniversalSqueaker/Kernel/`; pure funnel logic (also zero-Verse):
  `Source/UniversalSqueaker/Pure/`.
- UI design and slice ladder: `docs/ui-redesign-0.7-zh.md` (section 0 carrier re-verification, section 2 frame
  XML, section 5 migration pricing, section 6 slices). Migration plan: `docs/us-ui-migration-plan-zh.md`.
  Historical conflict only: `OBLIVIONIS.md`.
- Consumer-side contract pages: `docs/r12-us-theme-contract-zh.md`, `docs/r4a-action-scope-typed-choice-prep.md`,
  `docs/r4b-preset-subtree-row-composition.md`.

## Archived history (pointer)

- Earlier verbatim compactions and the latest substantive summaries live in `OBLIVIONIS.md`; exact removed
  prose remains in Git history. The archive covers the pre-distill text of MEMORY and TODO, the
  session checkpoints 2026-08-24 -> 2026-09-10, the FL 0.3.0 migration round, the diagnostics round-9 audit, the
  US -> FL 0.4 migration and its append-only evidence, the pre-cutover architecture audit, the finished S3/S4/S6
  slice narratives, the superseded "Handover - start here" and "Current state (2026-09-21)" blocks, the task-32
  selection narrative, and the gate-count history - together with the fact that `Palette` / `UiText` / `UiPanel` /
  `SurfaceFrame` / `UiInteract` / `UiValueStore` / `UiGuard` / `VoicePacksLayout` / the Schema=1 `Layout.xml` and
  the whole legacy page chain no longer exist.
- The docs sweep still carries the OLD Gate-U constraint ("fallback must be retained / no clean cutover") in
  `docs/uikit-rebuild/**`; the old-UI removal is executed and supersedes it.
- `dist/ui-evidence/**` and the per-revision matrices are cold artifacts; the reviews live in `docs/review/**`.
