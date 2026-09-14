# Universal Squeaker Settings UI — DeepSeek Leader Report

Date: 2026-09-13 (session) · Repo: `UniversalSqueaker/` · Branch: `0.4.x` · HEAD: `c8794ff7ca14955aaf1e4083af7194dcb06353f0` (unchanged; no commit, no push, no tag, no PR)
Carrier: `ferritelib` at `75c04ee0813bb5ea67621015866310ad3af0cea2`, working tree clean (0 changes) — read-only, never edited.

## 1. Worktree status

19 modified + 5 untracked paths, all inside the change set (no stray artifacts; raw harness evidence lives in the gitignored `dist/ui-evidence/`):

- Modified: `1.6/Languages/English/Keyed/UniversalSqueaker.xml`, `1.6/Languages/ChineseSimplified/Keyed/UniversalSqueaker.xml`, `Source/UniversalSqueaker/UI/IUsKernelSettingsSource.cs`, `UsKernelSettingsSource.cs`, `UsKernelSettingsHost.cs`, `Layout.Schema2.xml`, `Model/VoicePacksPageState.cs`, `Kernel/UsKernelDraw.cs`, `Kernel/UsPageTitleWidget.cs`, `Kernel/UsNavWidget.cs`, `Kernel/UsDiagnosticsWidget.cs`, `Kernel/UsBasicTuningWidget.cs`, `Kernel/UsTimingWidget.cs`, `Kernel/UsCameraIndicatorWidget.cs`, `Kernel/UsScopeTreeWidget.cs`, `tools/UniversalSqueakerKernelHostTests/Program.cs`, `MoodLayoutFocusedTests.cs`, `RecordingSettingsSource.cs`, `tools/UniversalSqueakerUiLogicTests/UiSourceInvariantTests.cs`.
- New: `Source/UniversalSqueaker/UI/Layout/UsLayoutVariants.cs`, `tools/UniversalSqueakerKernelHostTests/HelpDrawerLaneTests.cs`, `tools/UniversalSqueakerKernelHostTests/SettingsGeometryLaneTests.cs`, `docs/review/us-settings-deepseek-validation-report.md`, `docs/review/us-settings-deepseek-leader-report.md`.

## 2. Worker ownership (4 workers + Lead, all tasks completed)

| Task | Owner | Owned files |
|---|---|---|
| task-1 drawer | worker-a-drawer | `UI/Layout/UsLayoutVariants.cs` (new), `UsKernelSettingsHost.cs`, `IUsKernelSettingsSource.cs`, `UsKernelSettingsSource.cs`, `Model/VoicePacksPageState.cs`, `Kernel/UsPageTitleWidget.cs`, `UI/Layout.Schema2.xml`, `tools/.../RecordingSettingsSource.cs`, `tools/.../HelpDrawerLaneTests.cs` |
| task-2 mood | worker-b-mood | `Kernel/UsScopeTreeWidget.cs`, `tools/.../MoodLayoutFocusedTests.cs` |
| task-3 geometry+nav | worker-c-geometry | `Kernel/UsKernelDraw.cs`, `UsDiagnosticsWidget.cs`, `UsBasicTuningWidget.cs`, `UsTimingWidget.cs`, `UsCameraIndicatorWidget.cs`, `UsNavWidget.cs`, `tools/.../SettingsGeometryLaneTests.cs` |
| task-4 validation | worker-d-validator | `docs/review/us-settings-deepseek-validation-report.md` (no production file) |
| task-5 checkbox-edge fix | worker-c-geometry | `Kernel/UsKernelDraw.cs`, `tools/.../SettingsGeometryLaneTests.cs` |
| Lead | lead | both Keyed tables, `tools/.../Program.cs`, `tools/UniversalSqueakerUiLogicTests/UiSourceInvariantTests.cs`, `UI/Layout/UsLayoutVariants.cs` handover + this report |

## 3. Changed files grouped by outcome

**1 — retractable right-side help drawer.** `UI/Layout/UsLayoutVariants.cs` (new: immutable tree clone + `TryReplaceRoots`), `UsKernelSettingsHost.cs` (open/closed root snapshots, `help-open` value binding, `toggle-help-drawer` action, `ApplyVariant()` before `BumpContentRevision()`), `Model/VoicePacksPageState.cs` (`HelpDrawerOpen = true`, reset), `IUsKernelSettingsSource.cs` + `UsKernelSettingsSource.cs` (`SetHelpDrawerOpen`), `Kernel/UsPageTitleWidget.cs` (page-header Help toggle), `UI/Layout.Schema2.xml` (`body-row Breakpoint=700 Narrow=Column`; `help-scroll` unchanged, no `Tab`), `RecordingSettingsSource.cs`, `HelpDrawerLaneTests.cs`.

**2 — uniform diagnostic/support row geometry.** `Kernel/UsKernelDraw.cs` (one control-column contract: `ControlColumnRightInset=10`, `ControlColumnWidth=210`, `SegmentedRow`, `RowLabelRect/Width/Height`, `CheckboxSlot` anchored on the visible box's right edge), `UsDiagnosticsWidget.cs`, `UsBasicTuningWidget.cs`, `UsTimingWidget.cs`, `UsCameraIndicatorWidget.cs`, `SettingsGeometryLaneTests.cs`.

**3 — localized two-line mood cards.** `Kernel/UsScopeTreeWidget.cs` (one `MoodRowsLayoutFor` geometry for Measure+Draw; header + Pitch/Volume/Jitter blocks, label + `[-] [field] [+]` then slider; `MoodRowMode`/stacked branch deleted), `MoodLayoutFocusedTests.cs`; Keyed values `US.Tuning.Factor.Pitch/Volume/Jitter` = `Pitch/Volume/Jitter` and `音高/音量/抖动`; new `US.Help.Drawer.Toggle` + `.Hint`.

**4 — stable equal navigation cards.** `Kernel/UsNavWidget.cs` (one shared card height, reserved two-line subtitle band, identical bounds for selected/unselected, ellipsized label/subtitle), tests in `SettingsGeometryLaneTests.cs`.

**Lead plumbing.** `tools/.../Program.cs` (two lane wirings + `WidthAndLanguageEvidenceSweep` + helper visibility), `tools/UniversalSqueakerUiLogicTests/UiSourceInvariantTests.cs` (`VerifyHelpDrawerIsIndependentState`).

## 4. Commands and results

All run from `UniversalSqueaker/`. `scripts/verify-local.ps1` was run twice (before and after the checkbox fix); both runs printed `[verify] all checks passed.` with `VERIFY_EXIT=0` for all 16 gates: KernelTests, ConfigCopy, SettingsMigration, LogTests Release + Dev, FerriteLib carrier payload (clean checkout, Release bytes attributed to `75c04ee`), Dev build, Release build (warnings as errors), single-carrier payload, LICENSE, Schema=2 manifests, UiLogicTests, KernelHostTests, UI boundary audit, harness stub coverage.

Focused lanes (inside gate 13), all green: `MoodLayoutFocusedTests ALL PASS`, `HelpDrawerLaneTests ALL PASS`, `SettingsGeometryLaneTests ALL PASS`, EN/ZH text-fit sweep 15+15 frames "no text overflow and no tripped element", and the width/language evidence sweep with 16 rows, `overflow=False fit=0` everywhere.

Extra independent verification by worker D: build exit 0 (0 warnings/0 errors), UiLogicTests `ALL GREEN`, KernelHostTests `ALL PASS`, plus a temporary probe lane (P0–P7, `failures=0`) covering drawer counterexamples (a)–(f); probe deleted, `Program.cs` untouched by D.

## 5. Evidence artifacts

- `UniversalSqueaker/dist/ui-evidence/layout-sweep.txt` — measured/drawn geometry at 1024/736/480/320 × English/ChineseSimplified × drawer open/closed (nav / content / help / content-area widths, overflow, fit findings).
- `UniversalSqueaker/docs/review/us-settings-deepseek-validation-report.md` — worker D's independent report (per-outcome verdicts, adversarial probes, boundary confirmations, gaps).
- No screenshots exist in this repository's harness: all visual evidence is harness-measured geometry text, explicitly not real RimWorld pixels.

## 6. Acceptance checks (brief)

| Check | Result |
|---|---|
| open → close → open preserves the selected help topic | PASS (same `help-section-key`) |
| closing removes the reserved help width (no blank strip) | PASS (1024: content 608→796; 736: 320→508 = +176+12; closed centre `xMax` == body-row/page inner right, 0.000 gap) |
| switching tabs while open updates help without losing drawer state | PASS (`us/mode-row` → `us/filter-bar`, drawer stays open) |
| narrow widths do not clip or overflow | PASS (480/320 stack via `Breakpoint=700 Narrow=Column`; 0 horizontal overflow, fit=0) |
| no `active-tab` for help visibility | PASS (manifest `help-scroll` has no `Tab`; new source invariant `VerifyHelpDrawerIsIndependentState` pins it) |
| diagnostic control right edges ≤1px | PASS after task-5 (hit-box and visible-box right edges both 0px vs the segmented cell edge, EN+ZH × 4 widths) |
| nav card left/right edges equal; selected == unselected | PASS (5 cards identical x/width/height, constant gap, selected(Tuning) == unselected(Overview)) |
| mood parameter labels present and not clipped | PASS (label band measured from the resolved label; EN+ZH) |
| slider widths match within 1px across parameters and moods | PASS (six tracks equal <1px) |
| closed help drawer reserves zero help width | PASS |
| open help drawer has independent center/help scroll regions | PASS (both `Scroll` nodes, positions preserved across toggles) |

## 7. Key design decision: why the drawer is a layout variant

FerriteLib's declarative engine has **no binding-driven visibility or width**: `UiLayoutEngine.IsHidden` reads a static `Hidden` attribute plus `Tab` compared against `UiBindings.ActiveTabKey` (forbidden for help by the brief), and `ResolveColumnWidths` reads only static `Width`/`Auto`/`Min`/`Max`. `UiHost`'s constructor is fixed, `Manifest` is read-only, and `UiWindowHost` caches the single host, so there is no supported rebuild path either. US must not modify FerriteLib.

The drawer is therefore composed **on the tree**, FL's prescribed consumer path: `UsLayoutVariants` builds a CLOSED snapshot (the shipped tree with `help-scroll` omitted) and installs it into the manifest root list from the revision bumper, immediately before `BumpContentRevision()`. The help `Scroll` stays a real tree element in the open variant, so node identity, its scroll position, the fit audit and the hover-claim machine all survive a toggle, and nothing is recreated. `TryReplaceRoots` fails soft (one `Verse.Log.Warning`, shipped tree kept) if a future carrier changes the root-list runtime type; its refusal path is unreachable against the shipped payload (verified by D).

## 8. Documented deviations / known limitations

1. **Support-row height grows with a wrapped label** (worker C, confirmed by D). The density token is the floor; at 1024 (both languages) and 736-closed/ZH nothing grows. At 736-open and 320 the fixed 210px control column leaves an 18–42px label band while the shipped English label `Scale periodic with audible population` measures 304px, so constant token height is geometrically unsatisfiable there without clipping or silently ellipsizing settings labels. Alignment is never solved by height: the column's width and right edge are computed from the row before any text is measured. Requested follow-up if a more compact narrow mode is wanted: raise `body-row Breakpoint` 700 → ~760 (so 736 stacks) or add a stacked narrow-row variant.
2. **Fail-soft warning outlet.** The one layout-variant refusal warning uses `Verse.Log.Warning` rather than a dedicated `SqueakLog` event, because `SqueakLog` has no generic warning outlet and `Logging/` was outside this pass's scope. Follow-up: add a named event if the diagnostic channel should own it.
3. **Test fixture breadth.** `RecordingSettingsSource` carries 2 of the 4 product moods; per-mood assertions cover those two cards and the remaining identity rests on the single shared layout struct plus cross-card equal-geometry assertions. Follow-up: extend the fixture to 4 moods.
4. **Drawer toggle click is harness-proven through `UiNative.ButtonOverride`**, not real IMGUI hit testing.

## 9. In-game-only evidence gaps (not validated here)

Real Verse text metrics and font glyphs (the stub uses a half-width advance model; e.g. `Disabled` measures 48px in the stub against a 54px cell text band — a real font could consume that margin and trigger cell ellipsis); real IMGUI hit testing and pointer/focus behaviour; real screen sizes, DPI and scaling; real language injection or a non-shipped language; real save/load round-trips; and actual screenshots. **No release readiness is claimed from harness/browser evidence alone.** No push, publish, install, tag or PR was performed.

## 10. Requested follow-ups (none blocking)

1. Decide on a compact narrow-mode support-row variant (item 1 above).
2. Optionally promote a `VisibleBind`/`WidthBind` (or a supported layout swap) into FerriteLib so `UsLayoutVariants` can retire; until then it is a documented consumer-side composition.
3. Extend the mood fixture to all four moods.
4. In-game pass on a real RimWorld 1.6 install: drawer toggle, both languages, 480/320 window sizes, and the mood card readability check.
---

# Closure pass (second round) — FL-first audit and explicit gap closure

Baseline for this round: US HEAD `c8794ff7ca14955aaf1e4083af7194dcb06353f0` on `0.4.x`, worktree = the uncommitted change set (19 modified + 8 untracked paths, all in scope); FerriteLib `75c04ee0813bb5ea67621015866310ad3af0cea2`, clean, read-only.
Verification entry points: `scripts/verify-local.ps1` (16 gates), `scripts/ui-boundary-audit.ps1` (gate 14), `tools/UniversalSqueakerUiLogicTests`, `tools/UniversalSqueakerKernelHostTests` (lanes: Mood, HelpDrawer, SettingsGeometry, DiagnosticsPanel, UsSurface, text-fit, width/language evidence), `tools/UniversalSqueakerSettingsMigrationTests`, `tools/UniversalSqueakerConfigCopyTests`.

Round-2 workers and owned files:
- **A2 (task-6)** — `tools/UniversalSqueakerKernelHostTests/HelpDrawerLaneTests.cs`: drawer-per-window/never-persisted, combined scroll+tab+close+reopen, drawer-path FL-first scan.
- **B2 (task-7)** — `RecordingSettingsSource.cs`, `MoodLayoutFocusedTests.cs`: fixture now exposes all four product moods; per-card geometry/routing/keyed-header assertions in EN+ZH.
- **C2 (task-8)** — `SettingsGeometryLaneTests.cs`: 16-case visible-edge + row-height evidence table with per-row growth assertions.
- **D2 (task-9)** — `docs/review/us-settings-deepseek-validation-report.md`: independent re-run validation on the frozen revision.
- **Lead** — `Program.cs` (mood-count assertion 2→4), `docs/review/us-settings-fl-first-audit.md` (new), this report.

## FL-first verdict (brief section 'Explicit FL exception')

**PASS.** Whole-source scan yields exactly **one** code-level backend call: `Patches/Patch_GlobalControlsUtility_CameraIndicator.cs` `Widgets.Label` — the brief's accepted world-HUD exception, untouched. `UI/**` uses only `UsKernelDraw` / `UiNative` / `UiThemeDraw` / session bindings / cache-invalidation seams; no new `GUI.*`, `Widgets.*`, `Event.current`, manual scroll view, or custom input routing. Gate 14 re-run after freeze: CLEAN (117 files, 3 dated exemptions, raw `Mouse.IsOver` = 0). Details: `docs/review/us-settings-fl-first-audit.md`.

## Corrected diagnostics row-height growth evidence (replaces section 8 item 1)

Floor = the density token `RowVisualHeight` **24.00px** (asserted equal to the manifest `Density=regular` RowHeight). Across 16 cases (1024/736/480/320 × EN/ZH × drawer open/closed): **46 shared-rule rows grow in 9 of 16 cases**; every drawn height equals `max(24, measured wrapped label)` within 0.01px and the fit audit is silent everywhere. Growth is now machine-checked per row as `grown ⟺ resolved label width > label band`.

| Case | Label band | Growth (EN) | Growth (ZH) |
|---|---|---|---|
| 1024 open & closed | 330 / 518 | none | none |
| 736 closed | 230 | 2 rows, +18.67px | none |
| 736 open | 42 | 6 rows, max +146.67px | 6 rows, +40..+61.33px |
| 480 open & closed | 178 | 4 rows, +18.67px | none |
| 320 open & closed | 18 | 6 rows, max +338.67px | 6 rows, +104..+146.67px |

No case is provably avoidable without clipping: fitting the 304px English label `Scale periodic with audible population` on one line at 736-closed would need a control column ≤ ~136px, whose ~41px cells cannot hold the measured 48px `Disabled` label — that is ellipsis/clipping, not avoidance. Options remain layout redesigns (raise `body-row Breakpoint` 700 → ~760 so 736 stacks, or add a stacked narrow-row variant). **Product decision, not a validator change.**

## Checkbox visible-edge alignment (closed)

96 checkbox rows over the same 16 cases: **max |visible 18px box right edge − segmented last-cell right edge| = 0.000px** (hard-asserted ≤1px). The prior 3px visible deviation is closed (`CheckboxRightInset = ControlColumnRightInset 10 + CheckboxVisual 18 + CheckboxInset 3 = 31`); the 24px hit band deliberately overhangs the shared edge by 3px and stays inside the row. Every drawn height equals the shared rule; segmented last-cell right edges 570 / 282 / 470 / 418 / 258.

## Four mood cards (closed)

The rich fixture now carries all four product moods (Good/Neutral/Bad/Break, distinct values, production enum order) and the lane asserts, per card: existence; **shared row width 324px**; Pitch/Volume/Jitter blocks with `[-] [field] [+]` on line 1 and a slider on line 2; **slider width 172px for all six blocks (12 tracks equal <1px)**; label bands ≥ the resolved label width (EN 36px max / ZH 24px); and headers resolved through `US.Mood.<Mood>` in EN and ZH (良好/中性/低落/崩溃; `音高/音量/抖动`). Per-card routing proves identity (Good→0.95, Neutral→0.85, Bad→0.70, Break→0.55).

## Save / reopen (closed as a harness property)

Drawer visibility is per-window VIEW state: scanning the persistence layer (`Settings/*.cs` + `Mod.cs`, 5 files) finds **0 drawer tokens** with planted-token positive controls, and the drawer state lives only on 5 UI files. Toggling closed then reopening through `VoicePacksPageState.Reset()` or a fresh source+host yields a 176px open drawer, and the production reopen path builds it open with no prior drawer write. Settings save/migration gates (config-copy lifecycle A–F, settings-migration) stayed green.

## Frozen-tree verification

`scripts/verify-local.ps1` re-run after every writer stopped: **`[verify] all checks passed.` / `VERIFY_EXIT=0`, all 16 gates OK**, including gate 13 KernelHostTests with the four-mood, drawer, geometry and bilingual text-fit lanes, gate 12 UiLogicTests, gate 14 UI boundary audit and gate 15 stub coverage. Independent D2 re-runs matched (build 0/0, ALL PASS, ALL GREEN, gate 14 CLEAN).

## Remaining in-game-only gaps

Real RimWorld/Verse text metrics and glyph advances (the harness models half-width/CJK advances); real IMGUI pointer, hover, focus and click dispatch; runtime translation injection beyond the shipped EN/ZH tables; real screen sizes/DPI; the real Config round-trip through RimWorld's Scribe path; actual screenshots; and the product judgment on 736-open/320 row growth. **No release readiness is claimed from harness evidence alone.**
---

# Round 3 (user feedback pass) — narrow window, compact nav, complete help coverage

User feedback from in-game screenshots: the window is far too wide (72% of screen), the left nav is too wide and too tall, and the four Playback behaviour rows need their own help entries. Work was split across the same 3 writers + 1 validator (no new hires).

## What changed

**Window policy (W1).** `WindowChromeLayout` gains a pure size policy: closed = clamp(24% of screen, 600, 860); open = closed + 188 (= the manifest's help 176 + body-row gap 12, derived so window and manifest cannot drift); height unchanged (0.66 screen, floor 600). `UniversalSqueakerSettingsWindow` keeps its source in a field and writes `windowRect` once per drawer-state edge (centred, clamped on screen — the pattern already used by the diagnostics windows). `HelpDrawerOpen` now defaults to **closed**, so the window opens narrow and only widens when Help expands. Nav column 192 → **160**; body-row `Breakpoint` 700 → **500** (keeps the 600-wide window in the two-column regime; 736/1024 stay wide, 480/320 still stack). `UsKernelSettingsHost` gained one required line — `bumper.ApplyVariant()` after `Attach` — because the parsed tree is the open variant while the state now defaults retracted.

Measured: 2560×1440 → closed 614.4 / open 802.4; 1920×1080 → 600 / 788; 800 → 600 / 788; 3840 → 860 / 1048. Content at 1024 open 608 → **640**, at 736 open 320 → **352** (+32 = the nav saving).

**Compact navigation (W3).** `SubtitleLines` 2 → 1 (still one shared band for all five cards, so stability holds; named constant = the one-line revert point) plus tighter paddings. Card 176×76 → **144×49** under stub metrics (144×52.3 under carrier metrics vs 82.7): **card 36% shorter, stack 34% shorter** (412 → 271 / 445 → 288). Five identical cards, constant gap, selected == unselected, EN == ZH bounds.

**Help coverage (W2).** The three scaling rows no longer share one item: `us/basic-tuning/scale-cooldown|scale-talking|scale-population` are new, each with its own EN/ZH copy; the shared `us/basic-tuning/scaling` item was removed. Two further gaps closed: `us/attenuation-editor/status` (the status/range read-out had **no** HelpHover at all) and `us/page-title/help-drawer` (the Help toggle, the last interactive control without an item). Catalog 40 → **44**, with a full control→help-item audit of every section recorded in the validation report; the bidirectional invariant now reads 44 catalog items == 44 claim literals, both directions empty.

## Commands and results (frozen tree)

- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release` → 0 warnings, 0 errors.
- `dotnet run --no-restore --project tools/UniversalSqueakerUiLogicTests -c Release` → **ALL GREEN** (after the Lead set the catalog count pin to 44).
- `dotnet run --no-restore --project tools/UniversalSqueakerKernelHostTests -c Release` → **ALL PASS** (mood, drawer, geometry lanes + 16/16 sweep rows, `overflow=False`, `fit=0`).
- `pwsh -NoProfile -File scripts/verify-local.ps1` → **all 16 gates OK, exit 0.** One earlier run failed at **gate 6** because the FerriteLib payload was `Dev`-configured; the documented recovery `dotnet build ../ferritelib/Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release --no-incremental` was applied (carrier tree stayed clean, 0 changes) and the re-run passed.
- Independent validator D2/D re-runs matched: build 0/0, UiLogic GREEN, KernelHost ALL PASS, plus a temporary 1908-draw probe (failures 0) walking 13 sections and 3 chrome surfaces with zero fit findings.

## Lead integration fix after validation

Validator D found one real boundary defect: the expanded window's 788 floor cannot fit screens narrower than 788 logical px (`ApplyDrawerWidth` clamped `x` but not the width; margin −148 at 640). Fixed in `WindowChromeLayout.SettingsOpenWidth`: the open width is now capped at the screen, so the page falls back on its own narrow-layout capability instead of hanging off the display. Pure-policy assertions added for 640/700/800 and the full gate suite re-run green.

## Known deviations in this round

1. **`SubtitleLines = 1`** (nav) is a user-directed deviation from the brief's "two-line subtitle band" wording; the stability contract is kept and the constant is the single revert point.
2. **Window runtime resize has no harness lane** — `UniversalSqueakerSettingsWindow` needs `Verse.Mod` to construct. It is compile-verified, gate-15 safe (the carrier stub declares `Window.windowRect`) and pure-policy-covered; open/close at real resolutions remains in-game-only.
3. **Timing-wrap lane retargeted** to a 580-wide page (centre column 384 = the exact pre-round-3 band) instead of pinning the drawer open, preserving its mutation sensitivity.

## In-game-only gaps after this round

The real window rect behaviour (resize, centring, WM), real Verse metrics/glyphs (ellipsis decisions), real IMGUI input, real screen sizes/DPI — including the <788 logical-width case now capped — runtime `Translate`, save/load, and screenshots. No release readiness is claimed from harness evidence alone.