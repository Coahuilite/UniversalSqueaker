# US Settings UI — independent validation report (round 3, Worker D)

Date: 2026-09-13
Validator: worker-d-validator (task-13, W4)
Baseline: US HEAD c8794ff7ca14955aaf1e4083af7194dcb06353f0 (branch 0.4.x, uncommitted change set); FerriteLib HEAD 75c04ee, clean and read-only.
This file replaces the round-2/closure report; those verdicts are superseded where the tree changed.

## 0. Evidence classes and method

1. **Static review** of the complete round-3 diff (25 code/doc items: 25 tracked modifications + 8 untracked files).
2. **Lead's verify-local**: 16/16 gates, VERIFY_EXIT=0, on the frozen tree. I did **not** re-run it. The Lead recorded that the first run failed at gate 6 because the FerriteLib payload was 'Dev'-configured, and that the documented recovery is 'dotnet build ../ferritelib/Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release --no-incremental'; the carrier tree stayed clean (0 changes) and all gates then passed. I re-checked the carrier independently: 'git -C ../ferritelib status --porcelain' is empty at 75c04ee.
3. **My locked re-run** of the three commands (cross-process lock at %TEMP%/us-dotnet-build.lock).
4. **My independent probe** (P0-P5), a temporary lane with a local ModuleInitializerAttribute polyfill gated on US_ROUND3_PROBE=1, so it ran without editing Program.cs or any W1/W2/W3 lane. Deleted after the run; the final tree contains no probe file.

Transparency: the first probe run reported 13 failures. All 13 were probe-side bugs, not product defects: a double-negated state assertion (P1b) and a wrong Tiny line-height constant (I used 21.333 where StubMetrics says 18, so my expected card height was 52.333 instead of 49). After fixing those two lines the same probe reports failures=0 over 1908 pointer draws. Nothing in the product tree was changed.

## 1. Exact commands and observed results

| # | Command (from UniversalSqueaker/, under the lock) | Observed |
|---|---|---|
| 1 | dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release | exit 0, 0 warnings / 0 errors |
| 2 | dotnet run --no-restore --project tools/UniversalSqueakerUiLogicTests -c Release | exit 0, ALL GREEN |
| 3 | dotnet run --no-restore --project tools/UniversalSqueakerKernelHostTests -c Release | exit 0, ALL PASS |
| 4 | $env:US_ROUND3_PROBE='1'; dotnet run --no-restore --project tools/UniversalSqueakerKernelHostTests -c Release | exit 0, R3 PROBE SUMMARY failures=0 draws=1908, 26.5 s |

Fresh artifact dist/ui-evidence/layout-sweep.txt (written by run 3):

    1024 EN open   nav=160 content=640 help=176 contentArea=624 overflow=False fit=0
    1024 EN closed nav=160 content=828 help=-   contentArea=812 overflow=False fit=0
     736 EN open   nav=160 content=352 help=176 contentArea=336 overflow=False fit=0
     736 EN closed nav=160 content=540 help=-   contentArea=524 overflow=False fit=0
     480 EN open   nav=456 content=456 help=456 contentArea=440 overflow=False fit=0
     480 EN closed nav=456 content=456 help=-   contentArea=440 overflow=False fit=0
     320 EN open   nav=296 content=296 help=296 contentArea=280 overflow=False fit=0
     320 EN closed nav=296 content=296 help=-   contentArea=280 overflow=False fit=0
    (all eight rows identical for ChineseSimplified)

## 2. Item 1 — narrow window policy, default drawer state, first frame, no lag

### 2.1 Policy arithmetic — HARNESS-PROVEN

My P0 (pure functions) and the UiLogicTests WindowChromeLayout test agree:

| screen width | closed = clamp(0.24*s, 600, 860) | open = closed + 188 | measured delta |
|---|---|---|---|
| 800 | 600 | 788 | 188 |
| 1024 | 600 | 788 | 188 |
| 1280 | 600 | 788 | 188 |
| 1920 | 600 | 788 | 188 |
| 2560 | 614.4 | 802.4 | 188 |
| 3840 | 860 | 1048 | 188 |

Height is max(600, 0.66*screen): 712.8 at 1080, 600 at 600. DrawerWidthDelta (188) equals the *manifest's* help-scroll Width 176 + body-row Gap 12 read at runtime, so the window width and the layout cannot drift apart silently. The closed floor (600 window - 2*20 carrier SidePadding = 560 page - 2*12 page padding = 536 body inner) stays above the body-row Breakpoint 500, and my P4 arranged the page at exactly 560/525/519 to confirm the regime either side of the breakpoint (see 5.3).

**Boundary finding (policy, not a failing gate):** the width floor is 600 for the *closed* state, so the *open* width floor is 788. On a logical screen narrower than 788 the expanded window cannot fit: my measured on-screen margin (screen - open) is -148 at 640, -88 at 700, 0 at 788 and +12 at 800. ApplyDrawerWidth clamps x into [0, screen - width] but never the width, so below 788 the expanded window is off-screen because of the floor. Mainline RimWorld logical widths (>=1024, plus 800 with a 12 px margin) are unaffected; the Lead should decide whether the open width should also clamp to the screen.

### 2.2 Default CLOSED + first frame + no one-state lag — HARNESS-PROVEN

* VoicePacksPageState.HelpDrawerOpen defaults to false (and Reset() re-establishes false).
* P1a: a host created with the shipped default and arranged with **no binding write at all** has no help-scroll viewport on the FIRST arrange, and the centre is the closed width 828 at 1024. This is exactly the path the new 'bumper.ApplyVariant() after Attach' line fixes: the parsed manifest is the open tree while the state is retracted, and without the explicit reconcile the first frame would be open and every later toggle one state behind.
* P1d: a source whose ViewState.HelpDrawerOpen is pre-set true before Create lays out **open** on frame one (640 centre). Both starting states are therefore reconciled at construction, not on the first write.
* P1b: five consecutive toggle-action flips; after EVERY write the very next MeasureAndArrange already shows the matching variant (no lag) with the matching centre width (640 open / 828 closed), and ContentRevision advances by exactly one each time.
* P1c: the value-binding route, including a redundant write of the current value (no drift).
* P2: two hosts created from two sources are independent — opening A's drawer leaves B retracted; a newly created third host starts retracted (per-window, not persisted, not global).

### 2.3 Window runtime mutation — IN-GAME-ONLY

The window's own size change has **no harness lane** (the lane cannot construct Verse.Mod, so the real window type cannot be built out of game). Static reading only: ApplyDrawerWidth runs from BeforeDraw (before the page draws), fires only on the drawer-state edge, commits the new width, re-centres on the old centre and clamps x; appliedDrawerExpanded starts retracted, matching InitialSizeFromScreen(drawerExpanded: false). So on the frame AFTER the click the window resizes and the page re-arranges into the same variant; there is no window/page divergence inside one frame. Confirming the real windowRect mutation, the real centring, the real screen bounds and the real WM resize path requires RimWorld. This is the gap the Lead flagged, and I confirm it as an evidence gap, not a defect.

## 3. Item 2 — compact navigation — HARNESS-PROVEN

Measured from the real draw path (UiNative.ButtonOverride) at 1024/736/480/320 in EN and ZH:

| width | nav column | card | gap | five identical | selected == unselected |
|---|---|---|---|---|---|
| 1024 | 160 | 144 x 49 | 4 | yes | yes |
| 736 | 160 | 144 x 49 | 4 | yes | yes |
| 480 | 456 (stacked Fill) | 440 x 49 | 4 | yes | yes |
| 320 | 296 (stacked Fill) | 280 x 49 | 4 | yes | yes |

Identical numbers in Chinese. Card width is column - 2*8 (SidePadding); card height is the single shared formula 4 + 20 + 1 + 1*TinyLine(18) + 6 = 49. Against the round-2 constants (6 + 22 + 1 + 2*18 + 11 = 76) that is **35.5% shorter** (the Lead's '~30%' is a rounding of this). Measure equals the drawn stack height (SettingsGeometryLaneTests), the gap is constant, and a live tab switch leaves every card's outer bounds identical. EN/ZH and the long-label passes change nothing.

**Documented deviation to record:** SubtitleLines is 1, while the handoff says 'reserve the same two-line subtitle band'. This is an explicit user-directed compactness ruling in the widget doc, and the band remains ONE shared constant for all five cards, so the stability contract is intact. It is a deviation from the brief's wording, not a regression of its purpose.

## 4. Item 3 — per-row help items and full control-to-help audit

### 4.1 My own bidirectional static audit — HARNESS-PROVEN (source-level)

Extracted independently with PowerShell (not by trusting the existing guard):
* catalog items parsed from UsHelpCatalog.cs: **44**
* claim keys = every 3-segment 'us/x/y' string literal in Source/UniversalSqueaker/UI/Kernel/*.cs: **44**
* claimed but not in catalog: **none**; in catalog but never claimed: **none**

I then read every dynamic claim site to confirm the literals really are claims: the mode-row Options table (4), the diagnostics LoggingOptions table (3), the basic-tuning DrawBasicRow helpKey argument (3), the page-title HelpDrawerHelpKey const (1), the scope-tree action-scope/auto ternary (2) and the six mood-reset consts, plus the attenuation status surface (2 sites: the wide status line and the narrow whole-body summary). The four Playback behaviour rows each own a distinct item: us/basic-tuning/egg (HelpHover on the egg row) and us/basic-tuning/scale-cooldown / scale-talking / scale-population (HelpHover on each row's own rect via DrawBasicRow). Nothing is missing; the old shared us/basic-tuning/scaling claim is gone from both the catalog and the widget.

The existing UiSourceInvariantTests guard is stricter than a grep: it scans every 'us/...' literal in Kernel/**, keeps 3-segment keys and asserts exact set equality both ways. I re-ran the lane (ALL GREEN) and reproduced its result independently.

### 4.2 Runtime control-to-help walk — HARNESS-PROVEN (spot)

My probe walked 13 section cards (up to 5 x-lanes, 8 px vertical step, scrolling the content to each card's top and bottom when taller than the viewport) plus the three page-chrome surfaces, 1908 pointer draws total:

* the four Playback behaviour rows each produced their own claim (egg, scale-cooldown, scale-talking, scale-population);
* every claim produced anywhere in the walk is a catalog item (no dead hover);
* every walked section produced at least one claim;
* the page chrome produced us/page-title/nav (nav column), us/page-title/help-drawer (header toggle) and us/page-title/apply (footer);
* the fit audit attached to the whole walk reported **0 findings** in 1908 draws.

38 of the 44 catalog items were produced by the walked pointer positions. The six not produced are state- or position-conditional (us/diagnostics/logging-enabled needs the Enabled value, us/scope-tree/domain and the checklist/forget control sit in side rects, and three mood-reset variants are unreachable states for the fixture) — each has a verified literal claim site above, so this is a walk-coverage limit, not a missing help item.

## 5. Item 4 — geometry sweep at 1024/736/480/320 EN+ZH — HARNESS-PROVEN

### 5.1 Sweep results

All 16 sweep rows: overflow=False, fit=0, content viewport alive.

| width | closed centre (EN = ZH) | open centre | closed vs round-2 (192 nav) | open vs round-2 |
|---|---|---|---|---|
| 1024 | 828 | 640 | 796 -> 828 (+32) | 608 -> 640 (+32) |
| 736 | 540 | 352 | 508 -> 540 (+32) | 320 -> 352 (+32) |
| 480 | 456 (stacked) | 456 | unchanged (stacked, full width) | unchanged |
| 320 | 296 (stacked) | 296 | unchanged (stacked, full width) | unchanged |

Content width therefore grew by exactly the 32 px the nav column lost (192 -> 160) at both wide widths, in both drawer states, and the drawer still adds exactly 188 when open (640 + 188 = 828).

### 5.2 Closed drawer reserves nothing

Every closed row has no help viewport; my P4 confirmed at the closed-floor page width (560) that the centre starts at x=184 (=12+160+12) with width 364 and no help column, i.e. the two-column row with no residual strip.

### 5.3 Breakpoint regime

body-row carries Breakpoint 500 / Narrow Column. Page 525 (body inner 501) is still the two-column row (nav 160); page 519 (body inner 495) stacks full width (nav 495 = 519-24, centre below the nav). The closed 600-wide window lands at inner 536, comfortably inside the two-column regime, so opening narrow cannot squeeze three columns.

## 6. Item 5 — FL-first and the non-negotiable boundaries

| Boundary | Evidence |
|---|---|
| FL-first, no new raw backend input/draw on the settings page | My independent token scan over Source/UniversalSqueaker found **zero** GUI.*/Widgets.*/Event.current/Input.*/scrollPosition/GUILayout calls in UI/**; the only Widgets.* in the whole product is Patches/Patch_GlobalControlsUtility_CameraIndicator.cs:45 on the world-HUD camera indicator — the brief's accepted exception — and it is untouched by this diff. The two 'using FerriteLib.UiKit.Kernel.Widgets;' lines are the library's own seam. Gate 14 of the Lead's verify-local (shrink-only whitelist scanner) passed on the same tree. |
| FerriteLib untouched / no Warning-Danger alias change | git -C ../ferritelib status --porcelain empty; FL HEAD 75c04ee; no FL file in the US diff. |
| No fabricated NotReached state | grep NotReached over Source/UniversalSqueaker -> 0 matches. |
| Diagnostics provenance untouched | git diff over Source/UniversalSqueaker/UI/Diagnostics, Diagnostics, Audio, Save and Patches -> empty; the diff list contains only the settings page, its layout/help/window files, US Keyed tables and lanes. |
| No audio/save/migration/voice-pack-discovery change | no file in those areas appears in the 25-item change set. |
| No push/publish/tag/PR, no animation | HEAD still c8794ff; no remote command was run; the new width change is a one-shot assignment on the state edge, with no time/lerp state (grep for Time./deltaTime/Lerp in the window -> none). |
| No unrelated screen redesign | the diff is confined to the settings page, its two Keyed tables and the harness lanes. |

## 7. In-game-only evidence gaps

1. **Window runtime mutation** (2.3): no lane can build Verse.Mod, so InitialSizePolicy, the BeforeDraw width change, centring, on-screen clamping and the real WM resize are compile-verified and policy-covered only. Everything about windowRect in the game stays an in-game claim.
2. **Real Verse text metrics and glyphs**: every measured width/height above comes from Program.StubMetrics. The 160 px nav column, 144 px cards and the compact 49 px card height are metric-independent, but the label/subtitle ellipsis decisions are not: real glyph advances could cut a nav subtitle or a support-row label differently.
3. **Real IMGUI input/hit testing**: the probe replaces UiNative.Button/Slider/TextField and synthesises draws; real pointer routing, real click dispatch on the header Help toggle and popup z-order remain in-game claims.
4. **Real screens/DPI**: the arithmetic covers logical widths, but the expanded window's behaviour on screens narrower than 788 (2.1) and any DPI scaling are unverified in game.
5. **Real localization pipeline**: both Keyed tables are read directly; the new Chinese/English help strings were not rendered by RimWorld's Translate at runtime.
6. **Real save/load**: settings persistence and the Config round trip were not exercised. The drawer state is per-window by design (a new host starts retracted), so this is expected rather than untested behaviour, but the window reopen path itself is unverified.
7. **No screenshots**: no image evidence was produced; the sweep artifact is numeric only.

## 8. Honesty statement

Every verdict above is tagged HARNESS-PROVEN or IN-GAME-ONLY. The harness proves the page layout contract, the policy arithmetic, the help coverage and the boundary rules on the frozen tree; it cannot prove the real window, the real font, the real event pump, the real screen or the real save path. No release readiness is claimed from harness evidence alone. I did not modify any W1/W2/W3 file; my only writes were the temporary probe lane (deleted) and this report.

### Final tree check
After deleting the probe: git status shows exactly the frozen round-3 change set (25 tracked/untracked code+doc items, 8 of them untracked, including this report), Program.cs untouched by me, git log -1 still c8794ff, ferritelib clean at 75c04ee, no NotReached.
