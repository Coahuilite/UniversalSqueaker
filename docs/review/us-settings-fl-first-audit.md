# US Settings UI — FL-first architecture audit (closure pass)

Scope: the Mod Settings page path (`Source/UniversalSqueaker/UI/**`) after the four-outcome change set.
Baseline: US HEAD `c8794ff7ca14955aaf1e4083af7194dcb06353f0` (branch `0.4.x`, dirty = the uncommitted change set); FerriteLib HEAD `75c04ee0813bb5ea67621015866310ad3af0cea2`, clean and read-only.

## 1. Method

Two independent checks:

1. Static token scan of the settings path — pattern `\bGUI\.[A-Za-z]|\bWidgets\.[A-Za-z]|Event\.current|BeginScrollView|EndScrollView|InvisibleButton|ButtonInvisible` over `Source/UniversalSqueaker/UI/**/*.cs`, plus the same pattern over `Source/UniversalSqueaker/Patches/**/*.cs`.
2. Gate 14 of `scripts/verify-local.ps1` (`scripts/ui-boundary-audit.ps1`), which self-tests its scanner, strips comments, and reddens any backend call outside its dated, shrink-only whitelist. It ran **OK** in both full verify-local runs of this change set.

## 2. Findings

`UI/**` contains **zero** raw backend calls. Every hit is either the FerriteLib seam or prose:

- `UI/Diagnostics/UsDiagnosticsWidgets.cs:350,501` and `UI/Kernel/UsKernelDraw.cs:568,591` — `UiNative.IsMouseOver(...)`, the library's own funnel (the audit's cross-repo rule that US holds zero raw hover calls).
- `UI/Kernel/UsHelpPanelWidget.cs:11` and `UI/Kernel/UsRaceLayerWidget.cs:21` — documentation comments only (stripped by the gate's comment stripper).

New code in this change set introduces no backend coupling:

- `UI/Layout/UsLayoutVariants.cs` — pure spec-tree data work (no Verse/Unity drawing).
- `UI/Kernel/UsPageTitleWidget.cs` — the Help toggle goes through `UsKernelDraw.SelectionButton` (→ `UiNative.Button`) and `UsKernelDraw.Keyed`.
- `UI/Kernel/UsKernelDraw.cs` (control-column contract) — `UiThemeDraw` fills/borders, `UiNative.Button`, injected `ITextMetrics`; no manual scroll view and no `Event.current`.
- `UI/Kernel/UsNavWidget.cs`, `UsDiagnosticsWidget.cs`, `UsBasicTuningWidget.cs`, `UsTimingWidget.cs`, `UsCameraIndicatorWidget.cs`, `UsScopeTreeWidget.cs` — same seams as before; no new input routing.

## 3. Accepted exception (per the brief, section 'Explicit FL exception')

`Patches/Patch_GlobalControlsUtility_CameraIndicator.cs:45` — `Widgets.Label(rect, "US.Debug.CameraIndicator".Translate(...))`.
This is the world-HUD camera indicator in the original RimWorld lower-right HUD region; the brief marks it as an intentional, documented exception to the settings FL-first rule. It was **not** migrated, refactored, or expanded in this pass. It is not counted as a violation.

## 4. One architectural deviation to record (not an FL-first breach)

`UsLayoutVariants.TryReplaceRoots` index-assigns the manifest's live `List<UiElementSpec>` behind `UiLayoutManifest.Roots`. Rationale, established by reading FerriteLib rather than guessing:

- `UiLayoutEngine.IsHidden` reads a static `Hidden` attribute plus `Tab` compared against `UiBindings.ActiveTabKey` — the only binding-driven visibility switch in the library, and the brief forbids it for help.
- `ResolveColumnWidths` reads only static `Width`/`Auto`/`MinWidth`/`MaxWidth`; there is no `VisibleBind`/`WidthBind`, no layout-source delegate, `UiHost`'s constructor is fixed, `Manifest` is read-only and `UiWindowHost` caches its single host.
- `UiLayoutManifest` is sealed with a private constructor, so a consumer cannot hand the host a variant manifest.

Therefore the only FL-first-compatible way to retract the column is FL's own prescribed consumer path ('compose it on the tree'): the drawer stays a real tree element with a real `Scroll` node, and the variant is applied from the revision bumper (`ApplyVariant()` before `BumpContentRevision()`), fail-soft with one warning if a future carrier changes the root-list runtime type.

**Recommended follow-up (unchanged):** propose a library primitive — `VisibleBind`/`WidthBind` on a child, or a supported layout swap — so `UsLayoutVariants` can retire. Until then this stays a documented consumer-side composition, not a new US-side drawing/input helper.

## 5. Verdict

FL-first: **PASS**. Settings page path uses only `UsKernelDraw`/`UiNative`/`UiThemeDraw`/session bindings/cache-invalidation seams; no new direct `GUI.*`, `Widgets.*`, `Event.current`, manual scroll view or custom input routing; the single `Widgets.Label` is the brief's accepted camera-indicator exception.