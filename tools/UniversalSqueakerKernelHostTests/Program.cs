using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// Real-schema Settings Host creation regression for Gate U. Executes the REAL production
/// <see cref="UsKernelSettingsHost"/> against the REAL embedded Schema=2 resource, the REAL US
/// widget registrations and the REAL typed binding table, using a recording
/// <see cref="IUsKernelSettingsSource"/> instead of the RimWorld graph.
///
/// This harness is deliberately creation/measure/draw focused (stub evidence): it does NOT claim
/// real RimWorld rendering, real translations, real catalog data or maintainer-verified behavior.
/// It proves the production creation-time contract (resource shape, kind resolution, attribute
/// schema, typed binding/action coverage, session lifecycle, layout structure at three viewports,
/// and typed business-boundary routing) executes without Verse.
/// </summary>
internal static class Program
{
    private const string ExpectedSource = "coahuilite.universalsqueaker";

    private static readonly (string Id, string Kind)[] ExpectedWidgets =
    {
        ("nav", "us/nav"),
        ("page-title", "us/page-title"),
        ("banner", "chrome/banner"),
        ("mode-row", "us/mode-row"),
        ("global-volume", "us/global-volume"),
        ("attenuation-editor", "us/attenuation-editor"),
        ("basic-tuning", "us/basic-tuning"),
        ("camera-indicator", "us/camera-indicator"),
        ("scope-tree", "us/scope-tree"),
        ("preset-list", "us/preset-list"),
        ("filter-bar", "us/filter-bar"),
        ("race-layer", "us/race-layer"),
        ("xenotype-layer", "us/xenotype-layer"),
        ("checklist", "us/voice-pack-checklist"),
        ("footer", "us/footer"),
        ("help-panel", "us/help-panel")
    };

    private static int Main()
    {
        try
        {
            RunAll();
            Console.WriteLine("ALL PASS");
            return 0;
        }
        catch (Exception ex)
        {
            // Robust failure report: Exception.ToString can itself fail (e.g. when the message
            // getter or a stack frame cannot be resolved), so print the parts separately.
            Console.Error.WriteLine("FAIL: " + ex.GetType().FullName);
            try
            {
                Console.Error.WriteLine("MESSAGE: " + ex.Message);
            }
            catch (Exception messageError)
            {
                Console.Error.WriteLine("MESSAGE-UNPRINTABLE: " + messageError.GetType().FullName);
            }

            // The whole chain, not one level: lane wrappers (MoodLayoutFocusedTests.Step, the Step()
            // helper) each rethrow with their own message, so the assertion that actually failed sits
            // below the first InnerException and used to be invisible to whoever read the console.
            Exception? inner = ex.InnerException;
            int depth = 0;
            while (inner != null && depth < 8)
            {
                string label = depth == 0 ? "INNER" : "INNER-" + (depth + 1);
                Console.Error.WriteLine(label + ": " + inner.GetType().FullName);
                try
                {
                    Console.Error.WriteLine(label + "-MESSAGE: " + inner.Message);
                }
                catch (Exception innerMessageError)
                {
                    Console.Error.WriteLine(label + "-MESSAGE-UNPRINTABLE: " + innerMessageError.GetType().FullName);
                }

                inner = inner.InnerException;
                depth++;
            }

            return 1;
        }
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Step failed: " + name, ex);
        }
    }

    private static void RunAll()
    {
        Step("real embedded resource + schema shape", RealEmbeddedResourcePresentAndShaped);
        Step("real host creation validates full schema", RealHostCreationValidatesFullSchema);
        Step("reopen session isolation", RealHostReopenIsolation);
        Step("unknown kind fails at creation", UnknownKindFailsAtCreation);
        Step("unknown attribute fails at creation", UnknownAttributeFailsAtCreation);
        Step("typed bindings route to business boundary", TypedBindingsRouteToBusinessBoundary);
        Step("full typed write coverage", FullTypedWriteCoverage);
        Step("three viewport measure + draw", ThreeViewportMeasureAndDraw);
        Step("five workspaces across viewports", FiveWorkspacesAcrossViewports);
        Step("workspace switch resets session scroll", WorkspaceSwitchResetsSessionScroll);
        Step("rich dynamic data measure + draw", RichDynamicDataMeasureAndDraw);
        Step("800px three-column mood layout focused geometry/interaction", () => MoodLayoutFocusedTests.RunAll());
        Step("session popup isolation + cleanup", SessionPopupIsolationAndCleanup);
        Step("disposed host cannot draw", DisposedHostCannotDraw);
        Step("settings window uses the pageUnavailable failure model", SettingsWindowPageUnavailableModel);
        Step("the window shell carries the next-frame trip and the two notices", SettingsWindowShellCarriesTheFailureContract);
        Step("overlay host without settings window", OverlayHostCreatedWithoutSettingsWindow);
        Step("overlay widget contract fails at creation", OverlayWidgetContractFailsAtCreation);
        Step("overlay dual-host session isolation", OverlayDualHostSessionIsolation);
        Step("text-fit audit against both shipped language tables", TextFitAuditAcrossLanguages);
        Step("wrapping timing label grows the timing card", WrappingTimingLabelGrowsTheCard);
        Step("wrapping Packs layer text grows both layer cards", WrappingDomainTextGrowsLayerRows);
        Step("composite dropdown popup publishes its covering rect", CompositeDropdownPublishesCoveringRect);
        Step("long author filter grows the popup and ellipsizes the trigger", LongAuthorFilterGrowsPopupAndEllipsizesTrigger);
        Step("a popup overflow report carries the owner's element identity", PopupOverflowReportCarriesElementIdentity);
        Step("popup width follows the widest option then the viewport", PopupWidthFollowsWidestOptionThenViewport);
        Step("prerequisite range tracks the compiled FerriteLib Api", PrerequisiteRangeTracksCompiledApi);
        Step("live filter write lays out identical to a fresh filtered host", FilterWriteLaysOutIdenticalToFreshFilteredHost);
        Step("control hover claims help through real pointer passes", HoverClaimsHelpThroughRealPointerPasses);
        Step("distance card draws its bands filled and disjoint", DistanceCardBandsFillTheMeasuredCard);
        Step("every display write advances the shared revision clock", DisplayWriteAdvancesSharedRevision);
        Step("hover claims release to the overview only after the D10 grace window", HelpHoverClaimReleasesOnlyAfterGraceWindow);
        Step("overlay show/hide/dispose/reopen", OverlayShowHideDisposeReopen);
        Step("overlay no-map safe exit", OverlayNoMapSafeExit);
        Step("overlay draw failure does not double-reserve the row cursor", OverlayDrawFailureDoesNotDoubleReserveRow);
        Step("closing settings window does not affect overlay", ClosingSettingsWindowDoesNotAffectOverlay);
        Step("diagnostics panel lane (round-9 contract)", () => DiagnosticsPanelLaneTests.RunAll());
        Step("US surface table + the two accent convergence points", () => UsSurfaceLaneTests.RunAll());
    }

    /// <summary>
    /// The in-game failure reported on 2026-09-04: picking Auto in a Tuning scope dropdown opened the
    /// next row's dropdown instead of selecting it. The composite popup path (UsKernelDraw.Dropdown:
    /// scope rows, domain picker, filter dropdowns) never fed the yield rule, so every covered trigger
    /// kept stealing the click. Since 0.4.0 that rule is the owned hit stack: a popup pushes a
    /// UiHitLayer and UiSession.IsPointerOverHigherLayer refuses the click for anything covered, in
    /// place of the retired single published rect. The library's own lanes prove dispatch given a
    /// stack; this lane proves the composite path publishes one, through the real production host.
    /// </summary>
    private static void CompositeDropdownPublishesCoveringRect()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);
        host.Bindings.Invoke("set-tab", "Tuning");
        Rect viewport = new(0f, 0f, 800f, 600f);
        host.DrawChecked(viewport);

        // Anchor low enough that two or more option rows cannot fit below it (the composite popup must
        // flip above the trigger, the same branch the reported click loss exercised) and far enough
        // right that the popup lands over a second widget: the yield rule below needs a node the popup
        // really covers. The real page hands the trigger's own rect in; injecting the anchor is what
        // lets this lane choose what sits underneath.
        Rect anchor = new(620f, 560f, 120f, 22f);
        string? publishedBy = null;
        Rect popup = default;
        UiNode? popupOwner = null;
        foreach (string key in UniversalSqueaker.Kernel.BuiltInActionKeys.All)
        {
            host.Session.OpenPopup("scope-tree-scope-" + key, anchor);
            host.DrawChecked(viewport);
            if (TryGetPopupHitLayer(host.Session, out UiHitLayer layer))
            {
                publishedBy = key;
                popup = layer.Rect;
                popupOwner = layer.Element;
                break;
            }

            host.Session.ClosePopup();
        }

        Assert(publishedBy != null,
            "no Tuning scope dropdown published a popup layer; composite popups can never win a covered click");
        Assert(popup.height >= 24f && Math.Abs(popup.height % 24f) < 0.01f,
            "composite popup height must be a whole number of option rows: " + popup.height);
        Assert(popup.y >= -0.01f && popup.yMax <= 600f + 0.01f,
            "composite popup must stay inside the host viewport: " + popup);
        Assert(Math.Abs(popup.yMax - anchor.y) < 0.01f,
            "a composite popup that cannot fit below its anchor must flip above it: " + popup);

        // 0.4.0 replaced the single published popup rect with the owned hit stack: UiPopup pushes a
        // popup layer and dispatch consults it topmost-first (UiSession.IsPointerOverHigherLayer).
        // Input arrives in the frame AFTER the draw, and a pass dispatches against the stack the
        // previous pass finished, so the predicate is asserted after a second frame - the same
        // next-frame reason the retired rect had to be republished before it was read.
        host.DrawChecked(viewport);

        // The layer names its owner by node identity, so the owner comes from the layer itself: the
        // scope rows are drawn inside one widget, so they are not tree elements a string id resolves.
        UiNode? owner = popupOwner;
        UiNode? covered = host.Session.GetNodeByElementId("help-panel");
        Assert(owner != null, "the published popup layer must name the element that pushed it");
        Assert(covered != null, "the help panel must be an arranged element for the covered case");

        Vector2 inside = new(popup.x + popup.width / 2f, popup.y + 12f);
        Vector2 outside = new(inside.x, popup.yMax + 40f);
        Assert(host.Session.IsPointerOverHigherLayer(covered!, inside),
            "a popup layer must make a covered element yield the click (owned hit stack)");
        Assert(!host.Session.IsPointerOverHigherLayer(covered!, outside),
            "the yield predicate must not fire outside the popup");
        Assert(!host.Session.IsPointerOverHigherLayer(owner!, inside),
            "the popup's own trigger keeps the click, which is what preserves toggle-to-close");

        // The layer is per-frame state: a second frame must republish it, and closing must drop it so a
        // stale layer cannot shadow later clicks (the dispatch stack is read one pass after the close).
        Assert(TryGetPopupHitLayer(host.Session, out _), "the popup layer must be republished every frame it draws");
        host.Session.ClosePopup();
        host.DrawChecked(viewport);
        host.DrawChecked(viewport);
        Assert(!TryGetPopupHitLayer(host.Session, out _), "a closed composite popup must leave no popup layer");
        Assert(!host.Session.IsPointerOverHigherLayer(covered!, inside),
            "a closed popup must stop shadowing the elements it covered");
    }

    /// <summary>
    /// The owned hit stack's popup layer for the pass that just drew, if any. Replaces the retired
    /// single-rect seam: a popup is the layer with <see cref="UiHitLayer.IsPopup"/>.
    /// </summary>
    internal static bool TryGetPopupHitLayer(UiSession session, out UiHitLayer layer)
    {
        foreach (UiHitLayer candidate in session.HitLayers)
        {
            if (candidate.IsPopup)
            {
                layer = candidate;
                return true;
            }
        }

        layer = default;
        return false;
    }

    /// <summary>
    /// 0.4.0 keys scroll positions by node identity, so a lane must arrange the page first: arranging is
    /// what creates the element nodes the string id resolves to. A missing node is a lane bug, never a
    /// silent zero, because a lane that skips the write would assert nothing.
    /// </summary>
    internal static void SetScrollPositionById(UiSession session, string elementId, Vector2 position)
    {
        UiNode? node = session.GetNodeByElementId(elementId);
        Assert(node != null, "no arranged element carries the scroll id " + elementId + "; arrange before writing a scroll position");
        session.SetScrollPosition(node!, position);
    }

    /// <summary>Reads one scroll container's position through its node; an unarranged container holds zero.</summary>
    internal static Vector2 ScrollPositionById(UiSession session, string elementId)
    {
        UiNode? node = session.GetNodeByElementId(elementId);
        return node == null ? Vector2.zero : session.GetScrollPosition(node);
    }

    /// <summary>
    /// D9 (2026-09-06 in-game): a long author credit clipped in the pack filter - the popup
    /// inherited the 143px trigger column verbatim (a 530px row was audited into 131px) and the
    /// trigger drew the selected label with no ellipsis. The popup now grows to the widest option
    /// label capped at the viewport, and the trigger ellipsizes through the same seam the audit
    /// measures. This lane asserts both against the real host and proves the fit audit goes silent
    /// on the two filter surfaces.
    /// </summary>
    /// <summary>
    /// The in-game overflow that started this task was reported as "(unscoped)" and could not be traced
    /// back to a code site. The popup pass draws after content and outside the layout engine's element
    /// scope, so it is the one unscoped draw path US owns; UsKernelDraw.Dropdown now claims the owner's
    /// path for the duration of the rows. This control forces a row wider than the viewport-capped popup,
    /// so the finding is guaranteed to exist, and asserts it carries that identity. Reverting the scope
    /// in UsKernelDraw.Dropdown turns this step red with the fallback identity - the mutation this exists
    /// for.
    /// </summary>
    private static void PopupOverflowReportCarriesElementIdentity()
    {
        string impossible = new string('A', 200); // StubMetrics Small: ~1600px, far wider than the viewport
        var stub = new StubMetrics();
        var fake = new RecordingSettingsSource { RichData = true, Authors = new[] { impossible } };
        using UiHost host = UsKernelSettingsHost.Create(fake, stub);
        host.Bindings.Invoke("set-tab", "Packs");
        Rect viewport = new(0f, 0f, 800f, 600f);

        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(stub, reports.Add);
        UiFitAudit.Enabled = true;
        SetTranslatorResolver(ReadKeyedTable("English"));
        try
        {
            host.MeasureAndArrange(new UnityEngine.Vector2(800f, 600f));
            host.DrawChecked(viewport);
            host.Session.OpenPopup("pack-filter", new Rect(300f, 200f, 143f, 24f));
            UiFitAudit.Reset();
            reports.Clear();
            host.MeasureAndArrange(new UnityEngine.Vector2(800f, 600f));
            host.DrawChecked(viewport);

            Assert(reports.Count > 0, "a popup row wider than the capped popup must be reported at all");
            foreach (UiOverflowReport report in reports)
            {
                Assert(!string.IsNullOrEmpty(report.ElementPath) && report.ElementPath != "(unscoped)",
                    "every popup overflow report must carry the owner's element identity, got '" + report.ElementPath + "'");
            }

            Assert(reports.Exists(r => r.ElementPath.EndsWith("/popup", StringComparison.Ordinal)),
                "the popup finding must be scoped to the owner's popup path: " + DescribeOverflow(reports));
        }
        finally
        {
            UiFitAudit.Detach();
            SetTranslatorResolver(null);
        }
    }

    private static void LongAuthorFilterGrowsPopupAndEllipsizesTrigger()
    {
        string longAuthor = new string('A', 60); // StubMetrics Small: 60 x 8px = 480px, far over 143
        var stub = new StubMetrics();
        var fake = new RecordingSettingsSource { RichData = true, Authors = new[] { longAuthor } };
        using UiHost host = UsKernelSettingsHost.Create(fake, stub);
        host.Bindings.Invoke("set-tab", "Packs");
        host.Bindings.Invoke("set-pack-filter", longAuthor);
        Rect viewport = new(0f, 0f, 800f, 600f);

        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(stub, reports.Add);
        UiFitAudit.Enabled = true;
        SetTranslatorResolver(ReadKeyedTable("English"));
        try
        {
            host.MeasureAndArrange(new UnityEngine.Vector2(800f, 600f));
            host.DrawChecked(viewport); // prime the view cache and popup state
            UiFitAudit.Reset();
            reports.Clear();
            host.MeasureAndArrange(new UnityEngine.Vector2(800f, 600f));
            host.DrawChecked(viewport);
            Assert(
                reports.FindAll(r => r.ElementPath.Contains("filter-bar")).Count == 0,
                "a selected long author must ellipsize inside the fixed trigger column, not overflow: "
                + DescribeOverflow(reports));

            Rect anchor = new(300f, 200f, 143f, 24f);
            host.Session.OpenPopup("pack-filter", anchor);
            host.MeasureAndArrange(new UnityEngine.Vector2(800f, 600f));
            host.DrawChecked(viewport);
            Assert(TryGetPopupHitLayer(host.Session, out UiHitLayer popupLayer),
                "the opened pack-filter must publish its popup layer");
            Rect popup = popupLayer.Rect;
            float needed = stub.MeasureWidth(longAuthor, UiFont.Small) + 12f;
            Assert(popup.width >= needed - 0.01f,
                "the popup must grow to the widest option label: " + popup.width + " < " + needed);
            Assert(popup.width <= viewport.width + 0.01f,
                "the grown popup must stay capped at the viewport: " + popup.width);

            UiFitAudit.Reset();
            reports.Clear();
            host.MeasureAndArrange(new UnityEngine.Vector2(800f, 600f));
            host.DrawChecked(viewport); // repaint with the popup open: rows measured against the grown rect
            Assert(
                reports.FindAll(r => r.ElementPath.Length == 0 || r.ElementPath.Contains("filter-bar")).Count == 0,
                "the widened popup must render its long row without overflow: " + DescribeOverflow(reports));
        }
        finally
        {
            UiFitAudit.Detach();
            SetTranslatorResolver(null);
        }
    }

    /// <summary>
    /// The D9 popup-width rule asserted as a rule, not as one incident's accident:
    /// <c>UsKernelDraw.Dropdown</c> takes the trigger column as the floor, grows to the widest option
    /// label plus the 6px per-side row padding, and caps the result at the host viewport. The long-author
    /// lane above pins the pack-filter report that produced the rule; this lane pins the three clauses with
    /// injected option text, so a hard-coded width, a dropped content term and a dropped cap each turn this
    /// lane red on their own:
    /// (1) floor - every option fits the 143px trigger column, so the popup keeps exactly that width
    ///     (a 268/313 literal, or a collapse to zero, fails here);
    /// (2) growth - 40 vs 70 characters of option text differ by exactly the measured 240px, and the
    ///     40-character popup is exactly its label plus 12px (any constant width fails here);
    /// (3) cap - a label wider than the 800px viewport yields the viewport width exactly (the content
    ///     clause without the cap fails here).
    /// The option text is not the interesting part: the pack-filter authors are display == value, so the
    /// lane can state each width as an exact number instead of a lower bound.
    /// </summary>
    private static void PopupWidthFollowsWidestOptionThenViewport()
    {
        Rect anchor = new(300f, 200f, 143f, 24f);
        Rect viewport = new(0f, 0f, 800f, 600f);
        var stub = new StubMetrics();
        float perChar = stub.MeasureWidth("A", UiFont.Small);
        Assert(perChar > 0f, "the stub must measure a character, or every width clause below is vacuous");

        float floorWidth = DrawnPopupWidth(new[] { "a" }, stub, anchor, viewport);
        Assert(Math.Abs(floorWidth - anchor.width) < 0.01f,
            "an option list that fits its trigger must keep the trigger width as the popup floor: "
            + floorWidth + " vs anchor " + anchor.width);

        string forty = new('A', 40);
        string seventy = new('A', 70);
        float fortyWidth = DrawnPopupWidth(new[] { forty }, stub, anchor, viewport);
        float seventyWidth = DrawnPopupWidth(new[] { seventy }, stub, anchor, viewport);
        Assert(Math.Abs(fortyWidth - (40f * perChar + 12f)) < 0.01f,
            "the popup must be exactly the widest label plus the 6px-per-side row padding: "
            + fortyWidth + " vs " + (40f * perChar + 12f));
        Assert(Math.Abs(seventyWidth - fortyWidth - 30f * perChar) < 0.01f,
            "30 more characters of option text must widen the popup by exactly 30 measured characters: "
            + (seventyWidth - fortyWidth) + " vs " + (30f * perChar));

        string impossible = new('A', 200); // StubMetrics Small: 1600px + 12px, far over the 800px viewport
        Assert(stub.MeasureWidth(impossible, UiFont.Small) + 12f > viewport.width,
            "the cap clause needs a label wider than the viewport, or it asserts nothing");
        float cappedWidth = DrawnPopupWidth(new[] { impossible }, stub, anchor, viewport);
        Assert(Math.Abs(cappedWidth - viewport.width) < 0.01f,
            "a popup wider than its viewport must be capped at the viewport width: "
            + cappedWidth + " vs " + viewport.width);
    }

    /// <summary>
    /// Opens the real pack-filter dropdown on the real production host with the given author options and
    /// returns the width of the popup layer that pass published (the popup is the layer carrying
    /// <see cref="UiHitLayer.IsPopup" /> - the rule reads back the rect the popup was actually drawn with,
    /// not a width this lane recomputed).
    /// </summary>
    private static float DrawnPopupWidth(string[] authors, StubMetrics stub, Rect anchor, Rect viewport)
    {
        var fake = new RecordingSettingsSource { RichData = true, Authors = authors };
        using UiHost host = UsKernelSettingsHost.Create(fake, stub);
        host.Bindings.Invoke("set-tab", "Packs");
        host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
        host.DrawChecked(viewport); // prime the view cache before the popup opens
        host.Session.OpenPopup("pack-filter", anchor);
        host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
        host.DrawChecked(viewport);
        Assert(TryGetPopupHitLayer(host.Session, out UiHitLayer layer),
            "an open pack-filter dropdown must publish its popup layer (authors: " + string.Join(",", authors) + ")");
        return layer.Rect.width;
    }

    private static string DescribeOverflow(List<UiOverflowReport> reports)
    {
        return string.Join(" | ", reports.ConvertAll(
            r => "(" + (r.ElementPath.Length == 0 ? "unscoped" : r.ElementPath) + " " + r.Axis
            + " " + r.Needed + "/" + r.Available + ")"));
    }

    /// <summary>
    /// Build-time lockstep pin: US compiles against the sibling carrier payload, so the range in Mod.cs
    /// must track the Api of the library this harness just linked. If the library bumps its public
    /// surface, this step goes red until PrerequisiteApiMin/Max move and both mods ship together - the
    /// desync that produced the 2026-09-04 TypeLoadException cannot recur silently.
    ///
    /// The expected range is PARSED from Mod.cs rather than repeated here: a literal copy of the number
    /// in this file is how a lockstep gate starts agreeing with itself instead of with the pin. Source
    /// is the only place to read it from - the Verse stub deliberately ships no Verse.Mod, so
    /// UniversalSqueakerMod cannot be loaded (let alone its private fields reflected) here.
    /// </summary>
    private static void PrerequisiteRangeTracksCompiledApi()
    {
        string modSource = File.ReadAllText(
            Path.Combine(RepoRoot(), "Source", "UniversalSqueaker", "Mod.cs"));
        Version min = ReadPinnedVersion(modSource, "PrerequisiteApiMin");
        Version max = ReadPinnedVersion(modSource, "PrerequisiteApiMax");
        Version api = FerriteLib.UiKit.Kernel.FerriteLibVersion.Api;

        Assert(api >= min && api < max,
            "the linked carrier reports FerriteLib Api " + api + " but US pins [" + min + ", " + max + ");"
            + " move PrerequisiteApiMin/Max in Mod.cs and ship both mods in lockstep");
        Assert(max.Major == min.Major && max.Minor == min.Minor + 1,
            "the prerequisite window must stay exactly one minor wide (pre-1.0 carrier lockstep); got ["
            + min + ", " + max + ")");
        Assert(api.Major == min.Major && api.Minor == min.Minor && api.Build == min.Build,
            "US must pin its floor to the Api it was actually compiled against: floor " + min
            + " vs linked carrier " + api + " (widening the window is a release decision, not a gate fix)");
    }

    private static Version ReadPinnedVersion(string source, string fieldName)
    {
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            source, fieldName + @"\s*=\s*new Version\((\d+),\s*(\d+),\s*(\d+)\)");
        Assert(match.Success,
            "cannot read " + fieldName + " from Mod.cs: the pin must stay a three-part"
            + " new Version(major, minor, build), or this gate stops being a check");
        return new Version(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value));
    }

    /// <summary>
    /// The post-filter misalignment reported from the game must be reproduced or excluded here:
    /// two real hosts at one viewport (800x600, where the Packs content overflows and the engine
    /// reserves the scrollbar - the regime the game log showed: 693 vs 677), one filtered live
    /// through the typed binding and one filtered from the first frame. If the layout after a
    /// filter write ever diverges from the from-the-start layout, the two snapshots disagree and
    /// this lane names the element.
    private static void FilterWriteLaysOutIdenticalToFreshFilteredHost()
    {
        // 800x600 (minimum), 1129x600 (the maintainer's 1568-wide screen at the 72%x66% default),
        // 1280x720: three regimes of scrollbar reservation and column width.
        var viewports = new[]
        {
            new Rect(0f, 0f, 800f, 600f),
            new Rect(0f, 0f, 1129f, 600f),
            new Rect(0f, 0f, 1280f, 720f),
        };
        string[] ids = { "filter-bar", "race-layer", "xenotype-layer", "checklist", "footer" };

        foreach (Rect viewport in viewports)
        {
            var liveFake = new RecordingSettingsSource { RichData = true };
            using UiHost live = UsKernelSettingsHost.Create(liveFake);
            live.Bindings.Invoke("set-tab", "Packs");
            live.DrawChecked(viewport);
            live.Bindings.Set("race-filter", "sanguophage");
            live.DrawChecked(viewport);
            UiLayoutSnapshot liveSnapshot = live.MeasureAndArrange(new Vector2(viewport.width, viewport.height));

            var freshFake = new RecordingSettingsSource { RichData = true };
            freshFake.SetRaceFilter("sanguophage");
            using UiHost fresh = UsKernelSettingsHost.Create(freshFake);
            fresh.Bindings.Invoke("set-tab", "Packs");
            fresh.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
            UiLayoutSnapshot freshSnapshot = fresh.MeasureAndArrange(new Vector2(viewport.width, viewport.height));

            foreach (string id in ids)
            {
                if (!liveSnapshot.RectById.ContainsKey(id) || !freshSnapshot.RectById.ContainsKey(id))
                {
                    throw new Exception("parity lane missing element id: " + id + " at " + viewport.width + "x" + viewport.height);
                }

                Rect liveRect = liveSnapshot.RectById[id];
                Rect freshRect = freshSnapshot.RectById[id];
                Assert(
                    Math.Abs(liveRect.x - freshRect.x) < 0.01f
                    && Math.Abs(liveRect.y - freshRect.y) < 0.01f
                    && Math.Abs(liveRect.width - freshRect.width) < 0.01f
                    && Math.Abs(liveRect.height - freshRect.height) < 0.01f,
                    id + " diverges after a live filter write at " + viewport.width + "x" + viewport.height
                    + ": live=" + liveRect + " fresh=" + freshRect);
            }

            Assert(
                Math.Abs(liveSnapshot.ContentSize.y - freshSnapshot.ContentSize.y) < 0.01f,
                "scroll content height diverges after a live filter write at " + viewport.width + "x" + viewport.height
                + ": live=" + liveSnapshot.ContentSize.y + " fresh=" + freshSnapshot.ContentSize.y);
        }
    }

    /// <summary>
    /// End-to-end C+A hover claim through the REAL production host and the REAL event pump (the
    /// 09-04b stub models hover from Event.mousePosition, so this is genuine pointer routing, not an
    /// override). The footer is the probe surface: it draws in window space (no scroll group), claims
    /// "us/page-title/apply", and since FL P3 that claim must land on the SESSION, which is where the
    /// help panel and the accent border read it. The lane no longer replays the frame boundary by hand
    /// - UiHost.DrawFrame runs it, exactly as the window relies on:
    ///   1. pointer over the footer  -> the pass's draw claims the entry;
    ///   2. pointer elsewhere        -> the next pass starts cleared (no stickiness);
    ///   3. panel height identical between hovered and unhovered passes (hover-invariant bands);
    ///   4. no ContentRevision bump across hover changes (claims never invalidate layout).
    /// The grace window itself is pinned by HelpHoverClaimReleasesOnlyAfterGraceWindow.
    /// </summary>
    private static void HoverClaimsHelpThroughRealPointerPasses()
    {
        // StubMetrics (not the default out-of-game Verse metrics, which return a constant) so the
        // height-invariance assert below is a real assertion: a displayed-string Measure would
        // produce DIFFERENT heights for the overview vs the hovered item and fail it.
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new StubMetrics());
        Rect viewport = new(0f, 0f, 1280f, 720f);
        host.Bindings.Invoke("set-tab", "Overview");

        // Pass 0: pointer parked away from every claimed surface (top-left corner of the nav
        // column above the first item is unclaimed chrome). Establish the baseline geometry.
        Vector2 away = new(4f, 4f);
        DrawWithPointer(host, viewport, away);
        UiLayoutSnapshot baseline = host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
        Assert(host.Session.HoverClaim == null,
            "an unhovered pass must carry no help claim, got '" + host.Session.HoverClaim + "'");
        int revisionBefore = host.Session.ContentRevision;

        // Pass 1: pointer over the footer's right half (the save-status claim rect).
        Rect footer = baseline.RectById["footer"];
        Vector2 onFooter = new(footer.x + footer.width * 0.75f, footer.y + footer.height / 2f);
        DrawWithPointer(host, viewport, onFooter);
        Assert(string.Equals(host.Session.HoverClaim, "us/page-title/apply", StringComparison.Ordinal),
            "hovering the footer save-status must claim us/page-title/apply on the session, got '"
            + host.Session.HoverClaim + "'");

        // Pass 2: pointer leaves. The boundary holds a finished claim for its grace window but starts
        // THIS pass cleared, so the claim must be gone even though pass 1 set it - the old sticky-hover
        // model failed exactly here.
        DrawWithPointer(host, viewport, away);
        Assert(host.Session.HoverClaim == null,
            "the claim must not survive the pass after the pointer leaves, got '"
            + host.Session.HoverClaim + "'");

        // Height parity across hover state, measured FRESH on two hosts: the engine caches the
        // snapshot by content revision and a hover claim must never bump it, so one host cannot
        // re-measure a changed hover state. Two hosts - one starting with the claim already in
        // the state, one without - compare the panel height the layout would give each. A Measure
        // that sized from the displayed string (the pre-C+A model) makes these two differ; the
        // hover-invariant catalog-maximum bands make them identical.
        var claimedFake = new RecordingSettingsSource { RichData = true };
        using UiHost claimedHost = UsKernelSettingsHost.Create(claimedFake, new StubMetrics());
        claimedHost.Session.ClaimHover("us/page-title/apply");
        claimedHost.Bindings.Invoke("set-tab", "Overview");
        float claimedHeight = claimedHost.MeasureAndArrange(new Vector2(1280f, 720f)).RectById["help-panel"].height;

        var plainFake = new RecordingSettingsSource { RichData = true };
        using UiHost plainHost = UsKernelSettingsHost.Create(plainFake, new StubMetrics());
        plainHost.Bindings.Invoke("set-tab", "Overview");
        float plainHeight = plainHost.MeasureAndArrange(new Vector2(1280f, 720f)).RectById["help-panel"].height;

        Assert(claimedHeight > 0f && plainHeight > 0f, "both hosts must lay out a help panel");
        Assert(Math.Abs(claimedHeight - plainHeight) < 0.01f,
            "the help panel height must not depend on the hover claim: claimed=" + claimedHeight
            + " plain=" + plainHeight);

        Assert(host.Session.ContentRevision == revisionBefore,
            "hover claims must never bump the content revision (layout is hover-invariant): "
            + revisionBefore + " -> " + host.Session.ContentRevision);
    }

    /// <summary>
    /// Regression lane for D3: the 72cff33 hover-wiring edit deleted `y += ChartHeight + Gap` in
    /// UsAttenuationEditorWidget.DrawContent, folding the status line and the preset buttons into
    /// the 64px chart band (the card then draws its content to ~43% of the height it measured).
    /// Intra-card rects never enter the layout snapshot, so the probe is behavioral: walk the
    /// vertical center line of the card and read which help claim each pixel wins. The chart band
    /// must be a real band and the LAST claimed band must reach near the card bottom - a layout
    /// that collapses bands upward cannot fill the height it measured. Mutation-checked: deleting
    /// the cursor advance turns the reach assertion red.
    /// </summary>
    private static void DistanceCardBandsFillTheMeasuredCard()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new StubMetrics());
        Rect viewport = new(0f, 0f, 1280f, 720f);
        host.Bindings.Invoke("set-tab", "Distance");
        // 0.4.0 keys scroll positions by node, so the containers must be arranged before they can hold
        // one; this write pins the probe to the top of the card in the arranged frame below.
        host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
        SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
        Assert(snapshot.RectById.TryGetValue("attenuation-editor", out Rect card), "Distance snapshot must contain attenuation-editor");

        float probeX = card.x + card.width / 2f;
        List<float> chartRows = new();
        List<float> presetRows = new();
        for (float y = card.y + 2f; y < card.yMax - 2f; y += 4f)
        {
            // Same per-pass protocol the settings window runs.
            DrawWithPointer(host, viewport, new Vector2(probeX, y));
            string claim = host.Session.HoverClaim ?? "";
            if (claim == "us/attenuation-editor/chart") chartRows.Add(y);
            else if (claim == "us/attenuation-editor/presets") presetRows.Add(y);
        }

        Assert(chartRows.Count >= 14, "the chart claim band must span at least ~56px, got " + (chartRows.Count * 4) + "px - bands are collapsing into the chart");
        Assert(presetRows.Count >= 5, "the preset-button claim band must be at least 20px tall, got " + (presetRows.Count * 4) + "px");
        Assert(chartRows.Count > 0 && presetRows.Count > 0 && presetRows.Min() > chartRows.Max(),
            "the preset band must start below the observed chart band (claims disjoint vertically): chartMax="
            + (chartRows.Count > 0 ? chartRows.Max() : -1f) + " presetMin=" + (presetRows.Count > 0 ? presetRows.Min() : -1f));
        // Span, not card edge: the element rect includes card chrome (title band + padding) the
        // content bands are not supposed to reach. The layout signature is the DISTANCE from the
        // top of the first claimed row to the bottom of the last: a filled card spans ~118px of
        // bands (chart 64 + gaps + status + buttons 26), the collapsed-bug shape ends at ~48px.
        Assert(presetRows.Max() - chartRows.Min() >= 100f,
            "claimed bands must span the full measured band sequence (chart top to presets bottom): span="
            + (presetRows.Max() - chartRows.Min()) + "px (collapsed shape measures ~48)");
    }

    /// <summary>
    /// D1/D6 contract lane. The production view cache and the layout cache share ONE clock (the
    /// session content revision, wired by AttachRevisionSource after 2026-09-04d); any write that
    /// changes what the page displays must advance it, or the cache serves the pre-write
    /// projection until some unrelated bumping write lands - exactly the "click does nothing until
    /// a workspace switch" report from the 2026-09-05 acceptance round. The fake carries a
    /// revision-gated cache (RecordingSettingsSource.RevisionSource) mirroring production; this
    /// lane drives each display-write binding and asserts (1) the clock moved and (2) the very
    /// next BuildView rebuilds instead of hitting the cache. Every key whose value flows back to
    /// the screen belongs in this list. The visible read-back flip stays a production-only
    /// property (the fake returns a constant view); FullTypedWriteCoverage pins write routing.
    /// </summary>
    private static void DisplayWriteAdvancesSharedRevision()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;

        void AssertBumped(string key, Action write)
        {
            int rev0 = host.Session.ContentRevision;
            fake.BuildView();               // sync the cache onto the current revision
            int primed = fake.BuildViewCount;
            fake.BuildView();               // a repeat read at the same revision must NOT rebuild
            Assert(fake.BuildViewCount == primed, key + ": repeat reads at one revision must hit the view cache");
            write();
            Assert(host.Session.ContentRevision > rev0,
                key + ": a display write must advance the session clock (D1/D6: stale until the next bump)");
            fake.BuildView();
            Assert(fake.BuildViewCount == primed + 1,
                key + ": the new revision must rebuild the view, not serve the cached projection");
        }

        AssertBumped("mode", () => host.Bindings.Set("mode", SqueakVoicePackMode.Disabled));
        AssertBumped("global-volume", () => host.Bindings.Set("global-volume", 0.42f));
        AssertBumped("allow-eggs", () => host.Bindings.Set("allow-eggs", false));
        AssertBumped("scale-cooldown", () => host.Bindings.Set("scale-cooldown", false));
        AssertBumped("scale-talking", () => host.Bindings.Set("scale-talking", false));
        AssertBumped("scale-population", () => host.Bindings.Set("scale-population", true));
        AssertBumped("camera-indicator", () => host.Bindings.Set("camera-indicator", false));
        AssertBumped("toggle-egg", () => host.Bindings.Invoke("toggle-egg", false));
        AssertBumped("toggle-scale-cooldown", () => host.Bindings.Invoke("toggle-scale-cooldown", false));
        AssertBumped("toggle-scale-talking", () => host.Bindings.Invoke("toggle-scale-talking", false));
        AssertBumped("toggle-scale-population", () => host.Bindings.Invoke("toggle-scale-population", true));
        AssertBumped("toggle-camera-indicator", () => host.Bindings.Invoke("toggle-camera-indicator", false));
        AssertBumped("set-distance-preset", () => host.Bindings.Invoke("set-distance-preset", SqueakDistancePreset.Conservative));
        AssertBumped("attenuation-point", () => host.Bindings.Invoke("attenuation-point", new FerriteLib.UiKit.Kernel.UiChartPointChange(2, 0.7f, 0f)));
        AssertBumped("min-interval", () => host.Bindings.Set("min-interval", 300));
        AssertBumped("cooldown-multiplier", () => host.Bindings.Set("cooldown-multiplier", 1.5f));
        AssertBumped("dev-logging", () => host.Bindings.Set("dev-logging", SqueakDevLoggingMode.Enabled));
        AssertBumped("localize-debug-menu", () => host.Bindings.Set("localize-debug-menu", true));
        AssertBumped("set-action-scope", () => host.Bindings.Invoke("set-action-scope", new UsScopeWrite("Eat", SqueakActionScope.Disabled)));
        AssertBumped("set-mood-tuning", () => host.Bindings.Invoke("set-mood-tuning", new UsMoodWrite(SqueakMood.Good, SqueakMoodFactor.Pitch, 1.2f)));
    }
    /// <summary>
    /// One pass of the settings-window protocol: pump a Repaint pass with the pointer at
    /// <paramref name="pointer"/> and let the host draw the page. Since FL P3 this is the whole
    /// protocol - the D10 hover-frame boundary runs inside UiHost.DrawFrame on the session, so the
    /// harness cannot drift from the window by replaying it wrong, and it no longer needs the source
    /// to clear anything.
    /// </summary>
    private static void DrawWithPointer(UiHost host, Rect viewport, Vector2 pointer)
    {
        Event e = Event.KeyboardEvent("dummy");
        e.type = EventType.Repaint;
        e.mousePosition = pointer;
        Event.current = e;
        try
        {
            host.DrawChecked(viewport);
        }
        finally
        {
            Event.current = null;
        }
    }

    /// <summary>
    /// D10 grace contract (maintainer ruling 2026-09-06), owned by <c>UiSession</c> since FL P3:
    ///  - a claim that landed during a pass is held, and the NEXT pass starts cleared (widgets
    ///    re-claim while they draw, so a stationary pointer never reads as stale);
    ///  - with no fresh claim the held one is restored for exactly HoverGraceFrames passes, then
    ///    releases to the section overview;
    ///  - a re-claim re-arms the window instead of consuming it, and a new claim replaces the old hold.
    /// The length is the consumer's (the library ships no default), so the lane also pins that the
    /// production host still asks for 15 and that one DrawFrame advances the session clock by exactly
    /// one - which is what lets the window own no frame protocol at all.
    /// </summary>
    private static void HelpHoverClaimReleasesOnlyAfterGraceWindow()
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake);
        Rect viewport = new(0f, 0f, 1280f, 720f);
        int grace = host.Session.HoverGraceFrames;
        Assert(grace == 15,
            "the production host must set the D10 grace length on the session (FL P3: the consumer picks"
            + " it, the library defaults to none); got " + grace);

        int frameBefore = host.Session.Frame;
        host.DrawChecked(viewport);
        Assert(host.Session.Frame == frameBefore + 1,
            "one UiHost.DrawFrame must advance the session clock by exactly one pass, or the window's"
            + " frame boundary is not the session's (" + frameBefore + " -> " + host.Session.Frame + ")");

        void Pass() => host.Session.BeginFrame();

        host.Session.ClaimHover("us/global-volume/slider");
        Pass();
        Assert(host.Session.HoverClaim == null,
            "a landed claim is held and the next pass starts cleared for the re-claim");

        host.Session.ClaimHover("us/global-volume/slider");
        Pass();
        host.Session.ClaimHover("us/global-volume/slider");
        Pass();
        Assert(host.Session.HoverClaim == null,
            "a stationary re-claim keeps the pass cleared for a fresh claim instead of consuming grace");

        Pass();
        Assert(host.Session.HoverClaim == "us/global-volume/slider",
            "the first gap pass restores the held claim");
        for (int gap = 2; gap <= grace; gap++)
        {
            Pass();
            Assert(host.Session.HoverClaim == "us/global-volume/slider",
                "the claim holds for the whole grace window (gap pass " + gap + ")");
        }
        Pass();
        Assert(host.Session.HoverClaim == null,
            "grace exhaustion releases to the section overview, got '" + host.Session.HoverClaim + "'");

        host.Session.ClaimHover("us/scope-tree/layer");
        Pass();
        Pass();
        Assert(host.Session.HoverClaim == "us/scope-tree/layer",
            "the gap pass after a new claim restores the NEW claim, not the old one");
    }

    private static void SettingsWindowPageUnavailableModel()
    {
        // New failure semantics (replaces the deleted legacy-fallback-reachability tests):
        //   * the page model has no Execute/ExecuteAll command dispatcher any more — the typed
        //     facade is the ONLY write authority, so no generic command can smuggle a legacy
        //     page switch back in;
        //   * the settings source interface exposes no Execute* surface either;
        //   * after a whole-frame failure the window's own catch disposes the host, and the
        //     disposed-host contract below proves the kernel page can never be drawn again —
        //     the window therefore shows only the pageUnavailable notice (its source-level
        //     shape is pinned by the UiLogicTests failure-model invariant; the window derives
        //     from Verse.Window, which the stub runtime deliberately does not provide).
        var (types, names) = LoadProductionTypeNames();

        Type? model = FindLoadedType(types, "VoicePacksPageModel");
        Assert(model != null, "the page model must be loadable for this gate to be meaningful");

        Assert(model!.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static) == null,
            "VoicePacksPageModel.Execute (generic legacy command dispatcher) must be gone");
        Assert(model.GetMethod("ExecuteAll", BindingFlags.Public | BindingFlags.Static) == null,
            "VoicePacksPageModel.ExecuteAll must be gone");
        foreach (MethodInfo method in model.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            Assert(!method.Name.StartsWith("Execute", StringComparison.Ordinal),
                "no Execute* dispatcher may return on the page model: found " + method.Name);
        }

        string[] typedFacade =
        {
            "SetActiveTab", "ScrollToSection", "SetDomainFilter", "SetMoodTuning",
            "ToggleVoicePack", "ForgetUnavailable"
        };
        foreach (string facade in typedFacade)
        {
            Assert(model.GetMethod(facade, BindingFlags.Public | BindingFlags.Static) != null,
                "the typed facade entry '" + facade + "' must exist as the sole write authority");
        }

        Type? source = FindLoadedType(types, "IUsKernelSettingsSource");
        Assert(source != null, "the kernel settings source interface must be loadable");
        foreach (MethodInfo method in source!.GetMethods())
        {
            Assert(!method.Name.StartsWith("Execute", StringComparison.Ordinal),
                "IUsKernelSettingsSource must not grow an Execute* command surface: found " + method.Name);
        }

        // Terminal state proof against the REAL host: once a whole-frame failure disposes it,
        // every further draw attempt is refused — there is nothing left to draw except the
        // pageUnavailable notice, and no second page implementation exists to fall back to.
        var fake = new RecordingSettingsSource();
        UiHost host = UsKernelSettingsHost.Create(fake);
        host.Dispose();
        AssertThrows<InvalidOperationException>(
            () => host.DrawChecked(new Rect(0f, 0f, 900f, 700f)),
            "after the window's whole-frame failure catch disposed the host, no frame can ever render the page again");

        // Types deleted with the legacy UI chain, plus the caller-less preview surface that only
        // existed to serve the removed Dev audio browser. They must not reappear in the shipped
        // assembly by name in any namespace: a resurrection under a different folder would otherwise
        // pass a source-level check.
        string[] deletedTypes =
        {
            "FerriteVoicePacksPage", "VanillaVoicePacksPage", "VoicePacksPage",
            "UsWidgetRegistrar", "UsCommandPayload", "UsWidgetCommandAdapter",
            "UiCommand",
            "SqueakFinalPreviewStatus", "SqueakFinalPreviewResult", "SqueakSettingsGameContext"
        };
        foreach (string dead in deletedTypes)
        {
            Assert(!names.Contains(dead),
                "deleted fallback/preview type '" + dead + "' must not exist in the shipped assembly");
        }
    }

    private static (List<Type> Loaded, HashSet<string> AllNames) LoadProductionTypeNames()
    {
        Assembly production = typeof(UsKernelSettingsHost).Assembly;
        var loaded = new List<Type>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        Type[] candidates;
        try
        {
            candidates = production.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            candidates = ex.Types.Where(t => t != null).ToArray()!;
        }

        foreach (Type type in candidates)
        {
            loaded.Add(type);
            names.Add(type.Name);
        }

        return (loaded, names);
    }

    private static Type? FindLoadedType(List<Type> types, string name)
    {
        foreach (Type type in types)
        {
            if (type.Name == name) return type;
        }

        return null;
    }

    private static void RealEmbeddedResourcePresentAndShaped()
    {
        Assembly production = typeof(UsKernelSettingsHost).Assembly;
        string[] names = production.GetManifestResourceNames();
        Assert(Array.Exists(names, name => name == "UniversalSqueaker.UI.Layout.Schema2.xml"),
            "production assembly must embed UniversalSqueaker.UI.Layout.Schema2.xml");

        string xml;
        using (Stream? stream = production.GetManifestResourceStream("UniversalSqueaker.UI.Layout.Schema2.xml"))
        {
            Assert(stream != null, "Schema=2 resource stream must be readable");
            using var reader = new StreamReader(stream!);
            xml = reader.ReadToEnd();
        }

        Assert(xml.Contains("Schema=\"2\""), "resource root declares Schema=2");
        Assert(xml.Contains("Source=\"" + ExpectedSource + "\""), "resource root declares the US source scope");
        Assert(xml.Contains("<Scroll Id=\"content-scroll\""), "content scroll container present");
        Assert(xml.Contains("<Scroll Id=\"help-scroll\""), "help scroll container present");
        Assert(xml.Contains("<Widget Id=\"footer\" Kind=\"us/footer\" Height=\"28\""), "fixed footer declared as body sibling");

        foreach ((string id, string kind) in ExpectedWidgets)
        {
            Assert(xml.Contains("<Widget Id=\"" + id + "\" Kind=\"" + kind + "\""),
                "resource declares widget " + id + " (" + kind + ")");
        }
    }

    private static void RealHostCreationValidatesFullSchema()
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake);

        Assert(host.Source == ExpectedSource, "host source matches the resource scope");
        Assert(host.Manifest.SchemaVersion == "2", "host manifest is schema 2");
        Assert(host.Session.IsActive, "host session is active after creation");

        // Creation succeeded => every kind in the real resource resolved through the real US/core
        // registries and every widget's Validate passed against the real typed binding table.
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        CollectKinds(host.Manifest.Roots, kinds);
        foreach ((string id, string kind) in ExpectedWidgets)
        {
            Assert(kinds.Contains(kind), "manifest contains kind " + kind + " (widget " + id + ")");
        }

        Assert(kinds.Contains("chrome/banner"), "core scope fallback resolved chrome/banner for the US scope");
        Assert(UiWidgetRegistry.KnownKinds(ExpectedSource).Count >= 15, "US scope registry holds the kernel composite kinds");
    }

    private static void CollectKinds(IReadOnlyList<FerriteLib.UiKit.Kernel.UiElementSpec> elements, HashSet<string> kinds)
    {
        foreach (FerriteLib.UiKit.Kernel.UiElementSpec element in elements)
        {
            kinds.Add(element.Kind);
            CollectKinds(element.Children, kinds);
        }
    }

    private static void RealHostReopenIsolation()
    {
        var fake = new RecordingSettingsSource();
        using UiHost first = UsKernelSettingsHost.Create(fake);
        using UiHost second = UsKernelSettingsHost.Create(fake);

        Assert(!ReferenceEquals(first.Session, second.Session),
            "each Host owns its own session; reopening must not share session state");
        Assert(first.Session.IsActive && second.Session.IsActive, "both sessions are active while both hosts are open");

        first.Dispose();
        Assert(!first.Session.IsActive, "closing the host disposes exactly its session");
        Assert(second.Session.IsActive, "closing one host must not touch the other host's session");
    }

    private static void UnknownKindFailsAtCreation()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        UiWidgetRegistry.InitializeCore();
        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + ExpectedSource + "\">"
            + "<Widget Id=\"x\" Kind=\"us/no-such-kind\" />"
            + "</UiPage>");

        AssertThrows<UiContractException>(
            () => new UiHost(ExpectedSource, manifest, new UiBindings(), UiTheme.DarkGold, new StubMetrics(), new StubTranslation()),
            "unknown US kind must fail at Host creation");
    }

    private static void UnknownAttributeFailsAtCreation()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        UiWidgetRegistry.InitializeCore();
        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + ExpectedSource + "\">"
            + "<Widget Id=\"x\" Kind=\"us/page-title\" Bogus=\"1\" />"
            + "</UiPage>");

        AssertThrows<UiContractException>(
            () => new UiHost(ExpectedSource, manifest, new UiBindings(), UiTheme.DarkGold, new StubMetrics(), new StubTranslation()),
            "unknown widget attribute must fail at Host creation");
    }

    private static void TypedBindingsRouteToBusinessBoundary()
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake);
        IUiBindings bindings = host.Bindings;

        // Creation-time contract: every typed binding/action key the real widgets consume must
        // exist with the exact registered type. Any mismatch fails here.
        bindings.ValidateValue<SqueakVoicePackMode>("mode", "test");
        bindings.ValidateValue<float>("global-volume", "test");
        bindings.ValidateValue<string>("distance-preset", "test");
        bindings.ValidateValue<bool>("allow-eggs", "test");
        bindings.ValidateValue<bool>("scale-cooldown", "test");
        bindings.ValidateValue<bool>("scale-talking", "test");
        bindings.ValidateValue<bool>("scale-population", "test");
        bindings.ValidateValue<bool>("camera-indicator", "test");
        bindings.ValidateValue<int>("tuning-layer", "test");
        bindings.ValidateValue<IReadOnlyList<ActionScopeRowView>>("action-scopes", "test");
        bindings.ValidateValue<IReadOnlyList<MoodTuningRowView>>("mood-rows", "test");
        bindings.ValidateValue<IReadOnlyList<BaselinePresetView>>("baseline-presets", "test");
        bindings.ValidateValue<IReadOnlyList<RaceLayerRowView>>("races", "test");
        bindings.ValidateValue<IReadOnlyList<VoicePackDomainView>>("xenotype-domains", "test");
        bindings.ValidateValue<VoicePackDomainView?>("selected-domain", "test");
        bindings.ValidateValue<UiDomainFilter>("domain-filter", "test");
        bindings.ValidateOptions<FilterOptionView>("race-filter-options", "test");
        bindings.ValidateOptions<FilterOptionView>("xenotype-filter-options", "test");
        bindings.ValidateOptions<FilterOptionView>("author-options", "test");
        bindings.ValidateAction<UsDomainFilterWrite>("set-domain-filter", "test");
        bindings.ValidateAction<string>("clear-pack-filters", "test");
        bindings.ValidateAction<UsScopeWrite>("set-action-scope", "test");
        bindings.ValidateAction<UsMoodWrite>("set-mood-tuning", "test");
        bindings.ValidateAction<UsBaselineRaceToggle>("toggle-baseline-race", "test");
        bindings.ValidateAction<UsBaselineXenoToggle>("toggle-baseline-xenotype", "test");
        bindings.ValidateAction<UsDomainSelection>("select-domain", "test");
        bindings.ValidateAction<UsPackToggle>("toggle-pack", "test");
        bindings.ValidateAction<UsDomainIdentity>("forget-unavailable", "test");
        bindings.ValidateAction<UiChartPointChange>("attenuation-point", "test");

        // Typed writes must reach the business boundary (the recording source) without any string
        // command bridge.
        bindings.Set("global-volume", 0.5f);
        Assert(fake.LastGlobalVolume == 0.5f, "global-volume value write routes to the business setter");

        bindings.Set("mode", SqueakVoicePackMode.Remix);
        Assert(fake.LastMode == SqueakVoicePackMode.Remix, "mode value write routes to the business setter");

        bindings.Invoke("set-tab", "Packs");
        Assert(fake.LastActiveTab == "Packs", "set-tab action routes to the business surface");

        bindings.Invoke("set-action-scope", new UsScopeWrite("Eat", SqueakActionScope.ActiveCommand));
        Assert(fake.LastActionKey == "Eat" && fake.LastActionScope == SqueakActionScope.ActiveCommand,
            "set-action-scope typed action routes to the business surface");

        bindings.Invoke("set-mood-tuning", new UsMoodWrite(SqueakMood.Neutral, SqueakMoodFactor.Pitch, 1.25f));
        Assert(fake.LastMood == SqueakMood.Neutral
            && fake.LastMoodFactor == SqueakMoodFactor.Pitch
            && fake.LastMoodValue == 1.25f,
            "set-mood-tuning typed action routes to the business surface");

        bindings.Invoke("toggle-pack", new UsPackToggle(SqueakVoicePackScope.Race, "human", "", "us.human", true));
        Assert(fake.LastPackScope == SqueakVoicePackScope.Race
            && fake.LastPackRace == "human"
            && fake.LastPackKey == "us.human"
            && fake.LastPackEnabled == true,
            "toggle-pack typed action routes to the business surface");

        bindings.Invoke("forget-unavailable", new UsDomainIdentity(SqueakVoicePackScope.Xenotype, "human", "sanguophage"));
        Assert(fake.LastForgetScope == SqueakVoicePackScope.Xenotype && fake.LastForgetTarget == "sanguophage",
            "forget-unavailable typed action routes to the business surface");

        bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.ConflictOnly, true));
        Assert(fake.LastDomainFilterKind == SqueakDomainFilterKind.ConflictOnly && fake.LastDomainFilterFlag == true,
            "set-domain-filter typed action routes to the business surface");

        bindings.Set("search-text", "sang");
        Assert(fake.LastSearchText == "sang", "search-text value write routes to the business setter");

        int clearRevision = host.Session.ContentRevision;
        bindings.Invoke("clear-pack-filters", "");
        Assert(fake.LastDomainFilterFlag == false
            && fake.LastRaceFilter == ""
            && fake.LastXenotypeFilter == ""
            && fake.LastPackFilter == ""
            && fake.LastSearchText == "",
            "clear-pack-filters clears every filter represented by the UI");
        Assert(host.Session.ContentRevision == clearRevision + 1,
            "clear-pack-filters invalidates the layout once after the batch");

        // Options bindings are read-only projections; they must exist and be typed.
        Assert(bindings.GetOptions<FilterOptionView>("author-options").Count == 0, "author-options reads as typed option list");

        // Layout-affecting actions bump the session content revision through the Host boundary.
        int revision = host.Session.ContentRevision;
        bindings.Invoke("scroll-to", "global-volume");
        Assert(host.Session.ContentRevision == revision + 1, "scroll-to bumps the session content revision");
        Assert(host.Session.ScrollTargetElementId == "global-volume",
            "scroll-to forwards the target into the session scroll-target state");
    }

    private static void ThreeViewportMeasureAndDraw()
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake);

        // The maintainer-specified safe-area sizes (window ≈ 60-75% of the three reference
        // resolutions, floored at 800x600).
        var viewports = new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) };
        foreach (Vector2 viewport in viewports)
        {
            UiLayoutSnapshot snapshot = host.MeasureAndArrange(new UnityEngine.Vector2(800f, 600f));
            Assert(snapshot.Viewports.ContainsKey("content-scroll"), "content scroll viewport present at " + viewport);
            Assert(snapshot.Viewports.ContainsKey("help-scroll"), "help scroll viewport present at " + viewport);
            Assert(snapshot.RectById.ContainsKey("footer"), "footer rect present at " + viewport);

            Rect contentScroll = snapshot.Viewports["content-scroll"];
            Assert(contentScroll.width > 100f && contentScroll.height > 100f,
                "content scroll viewport is usable at " + viewport + " (got " + contentScroll + ")");

            Rect navColumn = snapshot.RectById["nav"];
            Assert(Math.Abs(navColumn.width - 192f) < 0.5f, "nav column keeps its declared 192 width at " + viewport);
            Assert(snapshot.ScrollContents.ContainsKey("content-scroll"), "content scroll content rect present at " + viewport);

            Rect footer = snapshot.RectById["footer"];
            Assert(footer.height == 28f, "footer keeps its declared 28 height at " + viewport);
            Assert(footer.yMax <= viewport.y + 0.5f, "footer stays inside the viewport at " + viewport);

            // Overview is the default workspace: behavior controls are visible while every other
            // player task is gated out.
            foreach (string overviewId in new[] { "mode-row", "global-volume", "basic-tuning", "camera-indicator" })
            {
                Assert(snapshot.RectById.ContainsKey(overviewId), "Overview section " + overviewId + " visible at " + viewport);
            }

            Assert(!snapshot.RectById.ContainsKey("attenuation-editor"), "Distance workspace hidden by default at " + viewport);
            Assert(!snapshot.RectById.ContainsKey("scope-tree"), "Tuning workspace hidden by default at " + viewport);
            Assert(!snapshot.RectById.ContainsKey("checklist"), "Packs workspace hidden by default at " + viewport);
            Assert(!snapshot.RectById.ContainsKey("preset-list"), "Presets workspace hidden by default at " + viewport);

            Rect contentArea = snapshot.ScrollContents["content-scroll"];
            Assert(contentArea.width <= contentScroll.width + 0.5f,
                "Overview content never creates horizontal overflow at " + viewport);

            // One complete synchronous frame on the real tree at each viewport; native scopes must
            // all close (no leaked BeginScrollView/BeginGroup).
            host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));
            Assert(StubScrollDepth() == 0, "no leaked Verse scroll scope after DrawFrame at " + viewport);
            Assert(StubGroupDepth() == 0, "no leaked GUI group scope after DrawFrame at " + viewport);
            Assert(host.Session.IsActive, "session stays active after a complete frame at " + viewport);
        }
    }

    private static void FullTypedWriteCoverage()
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake);
        IUiBindings bindings = host.Bindings;

        // Every typed write the real widgets emit must reach the business boundary.
        bindings.Set("mode", SqueakVoicePackMode.Fallback);
        Assert(fake.LastMode == SqueakVoicePackMode.Fallback, "mode value write routes");
        bindings.Set("global-volume", 0.25f);
        Assert(Math.Abs(fake.LastGlobalVolume.GetValueOrDefault() - 0.25f) < 0.001f, "global-volume value write routes");
        bindings.Set("allow-eggs", true);
        Assert(fake.LastEasterEggs == true, "allow-eggs value write routes");
        bindings.Set("scale-cooldown", false);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScaleCooldown && fake.LastBasicToggleValue == false, "scale-cooldown value write routes");
        bindings.Set("scale-talking", true);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScaleTalking && fake.LastBasicToggleValue == true, "scale-talking value write routes");
        bindings.Set("scale-population", false);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScalePopulation && fake.LastBasicToggleValue == false, "scale-population value write routes");
        bindings.Set("camera-indicator", true);
        Assert(fake.LastCameraIndicator == true, "camera-indicator value write routes");
        bindings.Invoke("toggle-egg", false);
        Assert(fake.LastEasterEggs == false, "toggle-egg action routes");
        bindings.Invoke("toggle-scale-cooldown", true);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScaleCooldown, "toggle-scale-cooldown action routes");
        bindings.Invoke("toggle-scale-talking", false);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScaleTalking, "toggle-scale-talking action routes");
        bindings.Invoke("toggle-scale-population", true);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScalePopulation, "toggle-scale-population action routes");
        bindings.Invoke("toggle-camera-indicator", false);
        Assert(fake.LastCameraIndicator == false, "toggle-camera-indicator action routes");
        bindings.Invoke("set-distance-preset", SqueakDistancePreset.Conservative);
        Assert(fake.LastDistancePreset == SqueakDistancePreset.Conservative, "set-distance-preset action routes");
        bindings.Set("min-interval", 300);
        Assert(fake.LastMinIntervalTicks == 300, "min-interval value write routes");
        bindings.Set("cooldown-multiplier", 1.5f);
        Assert(Math.Abs(fake.LastCooldownMultiplier.GetValueOrDefault() - 1.5f) < 0.001f, "cooldown-multiplier value write routes");
        bindings.Set("dev-logging", SqueakDevLoggingMode.Disabled);
        Assert(fake.LastDevLoggingMode == SqueakDevLoggingMode.Disabled, "dev-logging value write routes");
        bindings.Set("localize-debug-menu", true);
        Assert(fake.LastLocalizeDebugActions == true, "localize-debug-menu value write routes");
        bindings.Invoke("set-action-scope", new UsScopeWrite("Work", null));
        Assert(fake.LastActionKey == "Work" && fake.LastActionScope == null, "set-action-scope null-clear routes");
        bindings.Invoke("set-mood-tuning", new UsMoodWrite(SqueakMood.Bad, SqueakMoodFactor.Volume, 0.8f));
        Assert(fake.LastMood == SqueakMood.Bad
            && fake.LastMoodFactor == SqueakMoodFactor.Volume
            && Math.Abs(fake.LastMoodValue.GetValueOrDefault() - 0.8f) < 0.001f,
            "set-mood-tuning action routes");
        bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.OrphanOnly, true));
        Assert(fake.LastDomainFilterKind == SqueakDomainFilterKind.OrphanOnly && fake.LastDomainFilterFlag == true, "set-domain-filter action routes");
        bindings.Invoke("scroll-to", "preset-list");
        Assert(fake.LastScrollToSection == "preset-list", "scroll-to action routes");
        // Attenuation chart drag: a normalized X on the second point writes the min distance.
        bindings.Invoke("attenuation-point", new UiChartPointChange(1, 0.25f, 0f));
        Assert(fake.LastDistanceRangeStart.HasValue, "attenuation-point action routes to SetDistanceRange");
    }

    private static void FiveWorkspacesAcrossViewports()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);

        var viewports = new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) };
        (string Tab, string[] Visible, string[] Hidden)[] tabs =
        {
            ("Overview", new[] { "mode-row", "global-volume", "basic-tuning", "camera-indicator" },
                new[] { "attenuation-editor", "scope-tree", "preset-list", "filter-bar", "checklist" }),
            ("Distance", new[] { "attenuation-editor" },
                new[] { "mode-row", "scope-tree", "preset-list", "filter-bar", "checklist" }),
            ("Packs", new[] { "filter-bar", "race-layer", "xenotype-layer", "checklist" },
                new[] { "mode-row", "attenuation-editor", "scope-tree", "preset-list" }),
            ("Tuning", new[] { "scope-tree" },
                new[] { "mode-row", "attenuation-editor", "preset-list", "checklist" }),
            ("Presets", new[] { "preset-list" },
                new[] { "mode-row", "attenuation-editor", "scope-tree", "checklist" })
        };

        foreach ((string tab, string[] visible, string[] hidden) in tabs)
        {
            int before = host.Session.ContentRevision;
            host.Bindings.Invoke("set-tab", tab);
            Assert(fake.LastActiveTab == tab, "set-tab routes to the business surface");
            Assert(host.Session.ContentRevision == before + 1,
                "workspace switch bumps the session content revision (layout cache invalidation)");

            foreach (Vector2 viewport in viewports)
            {
                UiLayoutSnapshot snapshot = host.MeasureAndArrange(new UnityEngine.Vector2(800f, 600f));
                foreach (string id in visible)
                {
                    Assert(snapshot.RectById.ContainsKey(id), tab + " section " + id + " visible at " + viewport);
                }
                foreach (string id in hidden)
                {
                    Assert(!snapshot.RectById.ContainsKey(id), "non-" + tab + " section " + id + " hidden at " + viewport);
                }

                host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));
                Assert(StubScrollDepth() == 0, "no leaked Verse scroll scope at workspace " + tab + " viewport " + viewport);
                Assert(StubGroupDepth() == 0, "no leaked GUI group scope at workspace " + tab + " viewport " + viewport);
                Assert(host.Session.IsActive, "session active at workspace " + tab + " viewport " + viewport);
            }
        }
    }

    /// <summary>
    /// End-to-end text-fitting gate. Production widgets, the production Schema=2 host, and the two
    /// shipped Keyed tables are driven through the kernel text-fit audit at three window widths. Two
    /// things must hold: no single-line label overflows the rect it is given (in either language), and
    /// the audit demonstrably fires when a label does overflow — an assertion that can only pass because
    /// nothing is measured would be worthless, so the second half injects a deliberately impossible
    /// option name. Widths come from the Verse stub's half-width advance model (CJK = one em, Latin =
    /// half an em); absolute pixel truth remains an in-game property.
    /// </summary>
    private static void TextFitAuditAcrossLanguages()
    {
        Dictionary<string, string> english = ReadKeyedTable("English");
        Dictionary<string, string> chinese = ReadKeyedTable("ChineseSimplified");
        Assert(english.Count >= 100, "the shipped English Keyed table should be populated, got " + english.Count);
        string[] tabs = { "Overview", "Distance", "Packs", "Tuning", "Presets" };
        var viewports = new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) };
        var reports = new List<UiOverflowReport>();

        var metrics = new StubMetrics();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            // Positive control, and the reason the zero-finding sweeps below mean anything: prove in this
            // process, through the same production drawing outlet, that a label which cannot fit is seen.
            // Without it, a silently disabled audit would pass every "no overflow" assertion forever.
            UiFitAudit.Reset();
            reports.Clear();
            UiFitAudit.BeginElement("probe/single-line");
            FerriteLib.UiKit.Kernel.UiThemeDraw.Label(
                new Rect(0f, 0f, 20f, 16f), "probe text", UiTheme.DarkGold, null, FerriteLib.UiKit.Kernel.UiFont.Tiny,
                TextAnchor.MiddleLeft, singleLine: true);
            UiFitAudit.EndElement();
            Assert(reports.Count == 1, "positive control failed: the audit saw nothing for a label that cannot fit (" + Describe(reports) + ")");
            Assert(reports[0].ElementPath == "probe/single-line" && reports[0].Axis == UiOverflowAxis.Width,
                "the overflow report must name the element path it was scoped to, got '" + reports[0].ElementPath + "' on the " + reports[0].Axis + " axis");

            // Negative control for the identity rule: the same impossible label drawn with no scope is
            // still reported, under the shell's fallback identity. This is the shape the first in-game
            // overflow had, and it is why CheckLanguageTable below treats an unidentified report as a
            // failure of its own rather than a footnote to the overflow.
            UiFitAudit.Reset();
            reports.Clear();
            FerriteLib.UiKit.Kernel.UiThemeDraw.Label(
                new Rect(0f, 0f, 20f, 16f), "probe text", UiTheme.DarkGold, null, FerriteLib.UiKit.Kernel.UiFont.Tiny,
                TextAnchor.MiddleLeft, singleLine: true);
            Assert(reports.Count == 1 && reports[0].ElementPath == "(unscoped)",
                "an unscoped draw must be reported under the fallback identity, got " + Describe(reports));

            // Why this lane never saw the in-game close-button overflow: it drives the page host, not the
            // window shell, so the chrome labels are never drawn here, and the width axis still runs on the
            // half-width model (only the height axis was calibrated against the real engine). For the
            // record, the close text and its unresolved Keyed literal measure in this model as:
            foreach (KeyValuePair<string, Dictionary<string, string>> closeTable in new[]
            {
                new KeyValuePair<string, Dictionary<string, string>>("english", english),
                new KeyValuePair<string, Dictionary<string, string>>("chinese", chinese),
            })
            {
                SetTranslatorResolver(closeTable.Value);
                string closeText = Translator.Translate("US.Settings.Window.Close");
                Console.WriteLine("[fit] " + closeTable.Key + " close text='" + closeText + "' modeled="
                    + metrics.MeasureWidth(closeText, FerriteLib.UiKit.Kernel.UiFont.Tiny)
                    + "px, unresolved key literal=" + metrics.MeasureWidth("US.Settings.Window.Close", FerriteLib.UiKit.Kernel.UiFont.Tiny) + "px");
            }
            SetTranslatorResolver(null);

            UiFitAudit.Reset();
            reports.Clear();
            CheckLanguageTable(reports, tabs, viewports, english, "english", metrics);
            CheckLanguageTable(reports, tabs, viewports, chinese, "chinese", metrics);

            // Reported widths of the two mood reset controls (measured Tiny + 12px side padding, floored
            // at 52): the numbers the wrap decision is built on, for the record.
            foreach (KeyValuePair<string, Dictionary<string, string>> table in new[]
            {
                new KeyValuePair<string, Dictionary<string, string>>("english", english),
                new KeyValuePair<string, Dictionary<string, string>>("chinese", chinese),
            })
            {
                SetTranslatorResolver(table.Value);
                float resetDefault = Math.Max(52f, metrics.MeasureWidth(Translator.Translate("US.Tuning.ResetToDefault"), FerriteLib.UiKit.Kernel.UiFont.Tiny) + 12f);
                float resetPreset = Math.Max(52f, metrics.MeasureWidth(Translator.Translate("US.Tuning.ResetToPreset"), FerriteLib.UiKit.Kernel.UiFont.Tiny) + 12f);
                Console.WriteLine("[fit] " + table.Key + " reset-to-default=" + resetDefault + "px reset-to-preset=" + resetPreset + "px cluster=" + (resetDefault + 6f + resetPreset) + "px");
            }
            SetTranslatorResolver(null);

            // Failure sensitivity for the sweeping checks: one Keyed string is replaced with a value no
            // fixed column can hold, then the real page is drawn again. If the audit stays silent here,
            // every "no overflow" result above is meaningless — that is the exact failure this guards.
            string impossible = new string('\u6d4b', 30);
            var stretched = new Dictionary<string, string>(chinese, StringComparer.Ordinal)
            {
                ["US.Packs.Filter.Race"] = impossible
            };

            SetTranslatorResolver(stretched);
            UiFitAudit.Reset();
            reports.Clear();
            using (UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, metrics))
            {
                host.Bindings.Invoke("set-tab", "Packs");
                host.MeasureAndArrange(viewports[0]);
                host.DrawChecked(new Rect(0f, 0f, viewports[0].x, viewports[0].y));
            }

            bool caught = false;
            foreach (UiOverflowReport report in reports)
            {
                if (report.Axis == UiOverflowAxis.Width
                    && report.ElementPath.IndexOf("filter-bar", StringComparison.Ordinal) >= 0)
                {
                    caught = true;
                }
            }

            Assert(caught, "the audit must report a Keyed string too wide for its column, findings: " + Describe(reports));
        }
        finally
        {
            UiFitAudit.Detach();
            SetTranslatorResolver(null);
        }
    }

    /// <summary>
    /// Failure sensitivity for the Packs layer row heights. The bilingual fit sweep compares one label
    /// against its own band, so it cannot see a row that is too short for the content stacked inside it —
    /// exactly the bug that made the race and xenotype layers overdraw the sections below them. This step
    /// drives the real Host twice, once with text that fits one line and once with text that cannot, and
    /// requires both cards to grow. Under the old height formulas (title only, measured from the bare
    /// xenotype name) the two arrangements come out identical and this fails.
    /// </summary>
    private static void WrappingDomainTextGrowsLayerRows()
    {
        var metrics = new StubMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            // The detail and title templates are Keyed: without a loaded table Translate returns the key
            // itself, the numeric arguments are dropped and nothing is long enough to wrap, which would
            // make this step pass without measuring anything.
            SetTranslatorResolver(ReadKeyedTable("English"));
            var narrow = new Vector2(800f, 600f);
            (float raceShort, float xenotypeShort) = LayerCardHeights(narrow, wrapping: false, metrics);
            (float raceLong, float xenotypeLong) = LayerCardHeights(narrow, wrapping: true, metrics);

            Assert(raceShort > 0f && xenotypeShort > 0f, "the rich fixture must place both layer cards");
            Assert(raceLong > raceShort + 10f,
                "a race row title that cannot fit one line must grow the race-layer card: one-line "
                + raceShort + "px, wrapping " + raceLong + "px");
            Assert(xenotypeLong > xenotypeShort + 10f,
                "a composed xenotype title that cannot fit one line must grow the xenotype-layer card: one-line "
                + xenotypeShort + "px, wrapping " + xenotypeLong + "px");
            Assert(reports.Count == 0,
                "wrapping layer text must be measured into its band, not clipped: " + Describe(reports));
        }
        finally
        {
            UiFitAudit.Detach();
            SetTranslatorResolver(null);
        }
    }

    /// <summary>
    /// Failure sensitivity for the timing card's multiplier row (task-92). The bilingual sweep above is an
    /// absence - "no label overflowed its band" - and an absence cannot tell a band that grew to fit its
    /// text from a card that quietly kept drawing past its own bottom. This step drives the real Host
    /// twice: the shipped English value, which does not fit one line in the narrow band, and a value short
    /// enough to fit. The card must be one wrapped line TALLER in the first case, and both frames must
    /// draw with the audit silent. Under the old fixed 20px band the two heights come out identical and
    /// this fails; so does a card that measures short while its row draws the grown band.
    /// </summary>
    private static void WrappingTimingLabelGrowsTheCard()
    {
        var metrics = new StubMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            Dictionary<string, string> english = ReadKeyedTable("English");
            var fits = new Dictionary<string, string>(english, StringComparer.Ordinal)
            {
                ["US.Tuning.CooldownMultiplier"] = "Cooldown"
            };

            float fitsOneLine = TimingCardHeight(fits, metrics);
            float wraps = TimingCardHeight(english, metrics);

            Assert(fitsOneLine > 0f && wraps > 0f,
                "the rich fixture must place the timing card, got " + fitsOneLine + "px / " + wraps + "px");
            Assert(wraps > fitsOneLine + 10f,
                "the shipped English multiplier label needs two lines in the narrow band, so the card must "
                + "grow by that line: one-line " + fitsOneLine + "px, wrapping " + wraps + "px");
            Assert(reports.Count == 0,
                "and growing the band is the answer, not clipping the text: " + Describe(reports));
        }
        finally
        {
            UiFitAudit.Detach();
            SetTranslatorResolver(null);
        }
    }

    /// <summary>Arranges and draws the Overview page at 800x600 with one language table, returning the timing card's height.</summary>
    private static float TimingCardHeight(Dictionary<string, string> table, StubMetrics metrics)
    {
        SetTranslatorResolver(table);
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(800f, 600f));
        float height = snapshot.RectById.TryGetValue("timing", out Rect rect) ? rect.height : 0f;
        // Draw it too: a band that only measures tall enough is not the fix, and a tripped element would
        // fail here (UsTripGuard) instead of leaving this step's silence unexplained.
        host.DrawChecked(new Rect(0f, 0f, 800f, 600f));
        return height;
    }

    private static (float Race, float Xenotype) LayerCardHeights(Vector2 viewport, bool wrapping, StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = true, WrappingDomainText = wrapping };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Packs");
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new UnityEngine.Vector2(800f, 600f));
        float Race = snapshot.RectById.TryGetValue("race-layer", out Rect raceRect) ? raceRect.height : 0f;
        float Xenotype = snapshot.RectById.TryGetValue("xenotype-layer", out Rect xenoRect) ? xenoRect.height : 0f;
        return (Race, Xenotype);
    }

    private static void CheckLanguageTable(
        List<UiOverflowReport> reports,
        string[] tabs,
        Vector2[] viewports,
        Dictionary<string, string> table,
        string label,
        StubMetrics metrics)
    {
        SetTranslatorResolver(table);
        UiFitAudit.Reset();
        reports.Clear();

        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        int frames = 0;
        foreach (string tab in tabs)
        {
            host.Bindings.Invoke("set-tab", tab);
            foreach (Vector2 viewport in viewports)
            {
                host.MeasureAndArrange(new UnityEngine.Vector2(800f, 600f));
                // Guarded: a frame that leaves an element in recovery fails HERE, naming the element,
                // instead of silently shrinking what this sweep was able to measure.
                host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));
                frames++;
            }
        }

        // The frame count is asserted rather than implied by the two loops above: this half of the
        // lane's claim is "the whole shipped page drew", and every one of those frames went through the
        // trip guard, so a page that fell into a recovery band can neither pass nor quietly reduce it.
        Assert(frames == tabs.Length * viewports.Length,
            label + ": the sweep must draw every workspace at every viewport, drew " + frames);

        var findings = new List<string>();
        var unidentified = new List<string>();
        foreach (UiOverflowReport report in reports)
        {
            if (string.IsNullOrEmpty(report.ElementPath) || report.ElementPath == "(unscoped)")
            {
                unidentified.Add(report.Axis + " needs " + report.Needed + "px, has " + report.Available + "px");
            }

            findings.Add(report.ElementPath + " " + report.Axis + " needs " + report.Needed + "px, has "
                + report.Available + "px at width " + report.RectWidth + "px, text=\"" + Short(report.Text) + "\"");
        }

        // Actionability is asserted before emptiness: a finding with no element identity cannot be traced
        // back to a code site, which is exactly what made the first in-game overflow unusable. An
        // unidentified report is therefore a failure even if the overflow list were otherwise empty.
        Assert(unidentified.Count == 0,
            label + ": every overflow report must carry a non-empty element identity (element id or node path); unidentified: "
            + string.Join(" | ", unidentified));

        Assert(findings.Count == 0,
            label + ": every label on the shipped page must fit the rect it is given; offenders: "
            + string.Join(" | ", findings));
        Console.WriteLine("  ok: " + label + " table draws " + frames + " frames (5 workspaces x 3 viewports)"
            + " with no text overflow and no tripped element");
    }

    private static string Describe(List<UiOverflowReport> reports)
    {
        var parts = new List<string>();
        foreach (UiOverflowReport report in reports)
        {
            parts.Add(report.ElementPath + "/" + report.Axis + " " + report.Needed + ">" + report.Available);
        }

        return parts.Count == 0 ? "(none)" : string.Join(" | ", parts);
    }

    /// <summary>Trims an offender string so the harness message stays one readable line per finding.</summary>
    private static string Short(string? text)
    {
        string value = text ?? "";
        return value.Length <= 28 ? value : value.Substring(0, 28) + "…";
    }

    private static Dictionary<string, string> ReadKeyedTable(string languageFolder)
    {
        string path = System.IO.Path.Combine(
            RepoRoot(), "1.6", "Languages", languageFolder, "Keyed", "UniversalSqueaker.xml");
        var document = new System.Xml.XmlDocument();
        document.Load(path);

        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Xml.XmlNode node in document.SelectNodes("/LanguageData/*")!)
        {
            table[node.Name] = node.InnerText.Trim();
        }

        return table;
    }

    private static string RepoRoot()
    {
        System.IO.DirectoryInfo? dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "scripts", "verify-local.ps1"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Installs the harness language database into the Verse stub's resolver. Reached by reflection
    /// because the field exists only on the stub: call sites compile against the Krafs reference
    /// assembly, which has no such member.
    /// </summary>
    private static void SetTranslatorResolver(Dictionary<string, string>? table)
    {
        System.Reflection.FieldInfo? field = typeof(Translator).GetField(
            "Resolve", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert(field != null, "the Verse stub must expose Translator.Resolve for language-driven checks");
        if (field == null) return;

        if (table == null)
        {
            field.SetValue(null, null);
            return;
        }

        field.SetValue(null, new Func<string, string>(
            key => table.TryGetValue(key, out string? text) ? text : key));
    }

    private static void WorkspaceSwitchResetsSessionScroll()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);

        // 0.4.0 keys scroll positions by element node, so arranging first is what makes the ids
        // addressable; without it the writes below would be silent no-ops and this lane would assert
        // nothing. The mid-lane assertion is the guard against that failure mode returning.
        host.MeasureAndArrange(new Vector2(1280f, 720f));
        SetScrollPositionById(host.Session, "content-scroll", new Vector2(50f, 120f));
        SetScrollPositionById(host.Session, "help-scroll", new Vector2(0f, 40f));
        Assert(ScrollPositionById(host.Session, "content-scroll").y == 120f,
            "the lane must really move the centre content scroll before it asserts the reset");
        host.Bindings.Invoke("set-tab", "Packs");

        Vector2 content = ScrollPositionById(host.Session, "content-scroll");
        Vector2 help = ScrollPositionById(host.Session, "help-scroll");
        Assert(content.x == 0f && content.y == 0f,
            "workspace switch resets the centre content scroll to top");
        Assert(help.x == 0f && help.y == 0f,
            "workspace switch resets the right help scroll to top");
    }

    private static void RichDynamicDataMeasureAndDraw()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);
        IUiBindings bindings = host.Bindings;

        // Filter/options/dynamic-list bindings read the rich projections.
        Assert(bindings.GetOptions<FilterOptionView>("author-options").Count == 2, "author-options reads the rich author list");
        Assert(bindings.GetOptions<FilterOptionView>("race-filter-options").Count == 4, "race-filter-options reads the rich list");
        Assert(bindings.GetOptions<FilterOptionView>("xenotype-filter-options").Count == 2, "xenotype-filter-options reads the rich list");
        Assert(bindings.Get<IReadOnlyList<RaceLayerRowView>>("races").Count == 3, "races reads the rich race list");
        Assert(bindings.Get<IReadOnlyList<BaselinePresetView>>("baseline-presets").Count == 1, "baseline-presets reads the rich preset list");
        Assert(bindings.Get<IReadOnlyList<VoicePackDomainView>>("xenotype-domains").Count == 1, "xenotype-domains reads the rich list");
        Assert(bindings.Get<VoicePackDomainView?>("selected-domain").HasValue, "selected-domain reads the rich selected domain");
        Assert(bindings.Get<IReadOnlyList<ActionScopeRowView>>("action-scopes").Count == 2, "action-scopes reads the rich list");
        Assert(bindings.Get<IReadOnlyList<MoodTuningRowView>>("mood-rows").Count == 2, "mood-rows reads the rich list");
        Assert(bindings.Get<IReadOnlyList<TuningDomainOptionView>>("tuning-domains").Count == 2, "tuning-domains reads the rich list");

        // Full dynamic draw: every workspace with non-empty lists at the reference viewports.
        foreach (string tab in new[] { "Overview", "Distance", "Packs", "Tuning", "Presets" })
        {
            host.Bindings.Invoke("set-tab", tab);
            foreach (Vector2 viewport in new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) })
            {
                host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));
                Assert(StubScrollDepth() == 0, "no leaked Verse scroll scope with rich data at " + tab + " " + viewport);
                Assert(StubGroupDepth() == 0, "no leaked GUI group scope with rich data at " + tab + " " + viewport);
            }
        }

        // Filter/search value writes route to the boundary and bump the layout revision.
        int before = host.Session.ContentRevision;
        bindings.Set("race-filter", "human");
        Assert(fake.LastRaceFilter == "human", "race-filter value write routes to the business setter");
        Assert(host.Session.ContentRevision == before + 1, "race-filter write bumps the layout revision");
        bindings.Set("xenotype-filter", "sanguophage");
        Assert(fake.LastXenotypeFilter == "sanguophage", "xenotype-filter value write routes");
        bindings.Set("pack-filter", "AuthorA");
        Assert(fake.LastPackFilter == "AuthorA", "pack-filter value write routes");
        bindings.Set("search-text", "sang");
        Assert(fake.LastSearchText == "sang", "search-text value write routes");
        bindings.Invoke("set-pack-filter", "AuthorB");
        Assert(fake.LastPackFilter == "AuthorB", "set-pack-filter action routes");

        // Dynamic list interactions: domain selection + checklist toggle + forget + baseline.
        bindings.Invoke("select-domain", new UsDomainSelection(SqueakVoicePackScope.Xenotype, "human", "sanguophage"));
        Assert(fake.LastSelectedScope == SqueakVoicePackScope.Xenotype && fake.LastSelectedTarget == "sanguophage", "select-domain routes");
        bindings.Invoke("toggle-pack", new UsPackToggle(SqueakVoicePackScope.Xenotype, "human", "sanguophage", "us.sang2", true));
        Assert(fake.LastPackKey == "us.sang2" && fake.LastPackEnabled == true, "toggle-pack routes");
        bindings.Invoke("forget-unavailable", new UsDomainIdentity(SqueakVoicePackScope.Xenotype, "human", "sanguophage"));
        Assert(fake.LastForgetTarget == "sanguophage", "forget-unavailable routes");
        bindings.Invoke("toggle-baseline-preset", "us.preset1");
        Assert(fake.LastBaselinePresetToggle == "us.preset1", "toggle-baseline-preset routes");
        bindings.Invoke("toggle-baseline-race", new UsBaselineRaceToggle("us.preset1", "human", true));
        Assert(fake.LastBaselineRace == "human" && fake.LastBaselineRaceSelected == true, "toggle-baseline-race routes");
        bindings.Invoke("toggle-baseline-xenotype", new UsBaselineXenoToggle("us.preset1", "human", "sanguophage", true));
        Assert(fake.LastBaselineXeno == "sanguophage", "toggle-baseline-xenotype routes");
        bindings.Invoke("import-baseline", "us.preset1");
        Assert(fake.LastBaselineImport == "us.preset1", "import-baseline routes");
        bindings.Invoke("set-tuning-layer", 2);
        Assert(fake.LastTuningLayer == 2, "set-tuning-layer routes");
        bindings.Invoke("set-tuning-domain", new UsTuningDomainSelection("human", "sanguophage"));
        Assert(fake.LastTuningDomainRace == "human" && fake.LastTuningDomainTarget == "sanguophage", "set-tuning-domain routes");
    }

    private static void SessionPopupIsolationAndCleanup()
    {
        var fake = new RecordingSettingsSource();
        using UiHost a = UsKernelSettingsHost.Create(fake);
        using UiHost b = UsKernelSettingsHost.Create(fake);

        a.Session.OpenPopup("a-popup", new Rect(10f, 10f, 120f, 100f));
        Assert(a.Session.IsPopupOpen("a-popup"), "session A owns its popup");
        Assert(!b.Session.IsPopupOpen("a-popup"), "session B never sees session A's popup");
        a.Session.RegisterPopupDraw(() => { });
        Assert(a.Session.PopupDrawActions.Count == 1, "popup draw action registered on A");
        Assert(b.Session.PopupDrawActions.Count == 0, "session B has no popup draw actions");

        a.DrawChecked(new Rect(0f, 0f, 800f, 600f));
        Assert(a.Session.PopupDrawActions.Count == 0, "popup draw actions are consumed at EndFrame");
        Assert(a.Session.IsPopupOpen("a-popup"), "popup ownership survives the frame");

        a.Session.ClosePopup();
        Assert(!a.Session.IsPopupOpen("a-popup"), "ClosePopup releases the popup");

        // Dispose clears popup + hot-control state for exactly this session.
        a.Session.OpenPopup("a-popup", new Rect(0f, 0f, 10f, 10f));
        a.Session.CaptureHotControl(4242);
        Assert(a.Session.IsHotControlOwned(4242), "session A owns the captured hot control");
        a.Dispose();
        Assert(!a.Session.IsActive, "dispose deactivates session A");
        Assert(!a.Session.IsPopupOpen("a-popup"), "dispose clears popup state");
        Assert(!a.Session.IsHotControlOwned(4242), "dispose releases exactly its hot control");
        Assert(b.Session.IsActive, "session B untouched by A's dispose");
    }

    private static void OverlayHostCreatedWithoutSettingsWindow()
    {
        // Real embedded overlay Schema=2 resource present in the production assembly.
        Assembly production = typeof(UsKernelOverlayHost).Assembly;
        string[] names = production.GetManifestResourceNames();
        Assert(Array.Exists(names, name => name == "UniversalSqueaker.UI.Layout.Overlay.Schema2.xml"),
            "production assembly must embed UniversalSqueaker.UI.Layout.Overlay.Schema2.xml");

        var source = new RecordingOverlaySource();
        using UiHost host = UsKernelOverlayHost.Create(source);

        Assert(host.Source == ExpectedSource, "overlay host source matches the resource scope");
        Assert(host.Manifest.SchemaVersion == "2", "overlay manifest is schema 2");
        Assert(host.Session.IsActive, "overlay session active after creation");
        Assert(host.Bindings.Get<string>("camera-readout") == source.Text,
            "camera-readout typed binding reads the overlay source");
        Assert(UiWidgetRegistry.KnownKinds(ExpectedSource).Contains("us/camera-readout"),
            "the shared US-scope registry holds the overlay readout kind");

        // The overlay host is fully independent: no settings host/source/window involved. Draw at
        // the three reference resolutions; the layout must not throw and native scopes close.
        foreach (Vector2 viewport in new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) })
        {
            host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));
            Assert(StubScrollDepth() == 0, "no leaked Verse scroll scope after overlay DrawFrame at " + viewport);
            Assert(StubGroupDepth() == 0, "no leaked GUI group scope after overlay DrawFrame at " + viewport);
        }

        Assert(host.Session.IsActive, "overlay session stays active after frames");
    }

    private static void OverlayWidgetContractFailsAtCreation()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        UiWidgetRegistry.InitializeCore();
        string xml;
        using (Stream? stream = typeof(UsKernelOverlayHost).Assembly.GetManifestResourceStream("UniversalSqueaker.UI.Layout.Overlay.Schema2.xml"))
        {
            Assert(stream != null, "overlay Schema2 resource stream must be readable");
            using var reader = new StreamReader(stream!);
            xml = reader.ReadToEnd();
        }

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        // Missing the required typed binding => the real widget's Validate must fail at creation.
        AssertThrows<UiContractException>(
            () => new UiHost(ExpectedSource, manifest, new UiBindings(), UiTheme.DarkGold, new StubMetrics(), new StubTranslation()),
            "missing camera-readout binding must fail at overlay Host creation");
    }

    private static void OverlayDualHostSessionIsolation()
    {
        var settingsFake = new RecordingSettingsSource();
        var overlayFake = new RecordingOverlaySource();
        using UiHost settingsHost = UsKernelSettingsHost.Create(settingsFake);
        using UiHost overlayA = UsKernelOverlayHost.Create(overlayFake);
        using UiHost overlayB = UsKernelOverlayHost.Create(overlayFake);

        overlayA.Dispose();
        Assert(!overlayA.Session.IsActive, "disposing overlay A deactivates exactly A");
        Assert(overlayB.Session.IsActive, "overlay B session untouched by A's dispose");
        Assert(settingsHost.Session.IsActive, "settings session untouched by overlay dispose");

        settingsHost.Dispose();
        Assert(!settingsHost.Session.IsActive, "settings dispose deactivates the settings session");
        Assert(overlayB.Session.IsActive, "overlay B survives the settings window close");
    }

    private static void OverlaySafeAreaThreeViewports()
    {
        // Simulated date-bar rects at the three reference resolutions (bottom-left, above the date
        // bar — the position already proven by the legacy patch; no invented offsets).
        var source = new RecordingOverlaySource();
        using var controller = new UsKernelOverlayController(source);

        foreach (Vector2 screen in new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) })
        {
            float leftX = 16f;
            float width = screen.x * 0.35f;
            float curBaseY = screen.y - 34f;
            Assert(controller.TryDraw(leftX, width, ref curBaseY), "overlay draws at " + screen);
            Assert(controller.IsActive, "overlay host active at " + screen);
            Assert(curBaseY >= 0f && curBaseY + 26f <= screen.y + 0.5f,
                "overlay row stays inside the safe area at " + screen + " (y=" + curBaseY + ")");
            Assert(StubScrollDepth() == 0 && StubGroupDepth() == 0, "no leaked scopes at " + screen);
        }

        // Resolution change: a wider rect re-measures (engine cache keyed by size); the session stays.
        float y = 1080f - 34f;
        Assert(controller.TryDraw(20f, 1920f * 0.5f, ref y), "overlay re-draws after a resolution-width change");
        Assert(controller.IsActive, "overlay stays active across the size change");
        Assert(y >= 0f && y + 26f <= 1080f + 0.5f, "overlay stays inside the safe area after the size change");
    }

    private static void OverlayShowHideDisposeReopen()
    {
        var source = new RecordingOverlaySource();
        var controller = new UsKernelOverlayController(source);
        Assert(!controller.IsActive, "overlay starts inactive");

        // Show: the first enabled frame creates the host.
        float y = 600f - 34f;
        Assert(controller.TryDraw(16f, 200f, ref y), "enabled overlay draws");
        Assert(controller.IsActive, "overlay active after the first enabled frame");

        // Hide: disable -> Maintain disposes the host; no draw while disabled.
        source.Enabled = false;
        controller.Maintain();
        Assert(!controller.IsActive, "overlay disposed when the toggle is off");
        Assert(!controller.TryDraw(16f, 200f, ref y), "overlay refuses to draw while disabled");

        // Reopen: a fresh host and a fresh session.
        source.Enabled = true;
        Assert(controller.TryDraw(16f, 200f, ref y), "overlay draws again after re-enable");
        Assert(controller.IsActive, "overlay active after re-enable");

        // Dispose: full cleanup; a disposed controller never draws.
        controller.Dispose();
        Assert(!controller.IsActive, "overlay disposed by controller.Dispose");
        Assert(!controller.TryDraw(16f, 200f, ref y), "disposed controller never draws");
    }

    private static void OverlayNoMapSafeExit()
    {
        var source = new RecordingOverlaySource { MapPresent = false };
        var controller = new UsKernelOverlayController(source);
        float y = 600f - 34f;
        Assert(!controller.TryDraw(16f, 200f, ref y), "no-map overlay does not draw");
        Assert(!controller.IsActive, "no-map overlay never activates");
        Assert(StubScrollDepth() == 0 && StubGroupDepth() == 0, "no scopes leaked by the no-map path");

        // Map present then gone mid-session: teardown without exception.
        source.MapPresent = true;
        Assert(controller.TryDraw(16f, 200f, ref y), "overlay draws while the map is present");
        source.MapPresent = false;
        controller.Maintain();
        Assert(!controller.IsActive, "map-gone tears the overlay session down");
        Assert(!controller.TryDraw(16f, 200f, ref y), "map-gone overlay does not draw");
    }

    private static void OverlayDrawFailureDoesNotDoubleReserveRow()
    {
        // Regression: when the kernel overlay's pass fails, TryDraw must not commit the row cursor.
        // The caller then draws the legacy pure-Verse readout at the same curBaseY; if the kernel path
        // had already decremented it, the fallback would double-reserve the row height.
        //
        // The injection moved one level up with FL 0.3.0 item C: a widget that throws mid-draw is now
        // recovered BY THE ENGINE (UiSessionGuard.DrawWidget paints a recovery band and keeps the page
        // alive), so it never reaches this catch - the old throwing widget simply no longer tests the
        // path it was written for, and FL proves that split from its side. What still escapes
        // UiHost.DrawFrame is frame-level work the guard does not wrap, and the session popup pass is
        // exactly that: UiHost.Draw runs the registered popup callbacks after the content tree, with
        // nothing between them and the consumer's catch. That is the failure planted here.
        var source = new RecordingOverlaySource();
        var controller = new UsKernelOverlayController(source);
        FieldInfo? hostField = typeof(UsKernelOverlayController).GetField("host", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(hostField != null, "overlay controller exposes a host field for failure injection");

        float y = 600f - 34f;
        Assert(controller.TryDraw(16f, 200f, ref y), "the live overlay draws before anything fails");
        Assert(Math.Abs(y - (600f - 34f - 26f)) < 0.001f, "a successful kernel pass reserves exactly one row");

        var host = hostField!.GetValue(controller) as UiHost;
        Assert(host != null && host.Session.IsActive, "the draw pass must have created a live overlay host to fail");
        host!.Session.RegisterPopupDraw(() => throw new InvalidOperationException("injected overlay frame failure"));

        float before = y;
        Assert(!controller.TryDraw(16f, 200f, ref y), "a frame-level overlay failure returns false");
        Assert(Math.Abs(y - before) < 0.001f,
            "failed overlay draw leaves curBaseY untouched so the legacy fallback does not double-reserve the row");
        Assert(controller.CreationFailed, "draw failure trips the permanent fallback flag");
        Assert(!controller.IsActive, "draw failure disposes the overlay host");
        Assert(!host.Session.IsActive, "and the failing session is the one that was torn down");
        Assert(StubScrollDepth() == 0 && StubGroupDepth() == 0, "no scopes leaked by the failure path");

        // The permanent flag is the whole point of the handover: no retry, even though the injected
        // failure is gone.
        float stillY = before;
        Assert(!controller.TryDraw(16f, 200f, ref stillY) && Math.Abs(stillY - before) < 0.001f,
            "the controller never re-enters the kernel overlay once it tripped");
    }

    private static void ClosingSettingsWindowDoesNotAffectOverlay()
    {
        var settingsFake = new RecordingSettingsSource();
        UiHost settingsHost = UsKernelSettingsHost.Create(settingsFake);
        settingsHost.DrawChecked(new Rect(0f, 0f, 800f, 600f)); // a live settings frame
        settingsHost.Dispose(); // window close

        var source = new RecordingOverlaySource();
        var controller = new UsKernelOverlayController(source);
        float y = 720f - 34f;
        Assert(controller.TryDraw(16f, 260f, ref y), "overlay draws after the settings window closed");
        Assert(controller.IsActive, "overlay session independent of the settings window");
    }

    private static void DisposedHostCannotDraw()
    {
        var fake = new RecordingSettingsSource();
        UiHost host = UsKernelSettingsHost.Create(fake);
        host.Dispose();
        Assert(!host.Session.IsActive, "disposed host session is inactive");
        AssertThrows<InvalidOperationException>(
            () => host.DrawChecked(new Rect(0f, 0f, 800f, 600f)),
            "a disposed session must refuse DrawFrame (close-path contract)");
    }

    private static int StubScrollDepth()
    {
        FieldInfo? field = typeof(Widgets).GetField("ScrollViewDepth", BindingFlags.Public | BindingFlags.Static);
        return field != null ? (int)(field.GetValue(null) ?? 0) : -1;
    }

    private static int StubGroupDepth()
    {
        FieldInfo? field = typeof(GUI).GetField("GroupDepth", BindingFlags.Public | BindingFlags.Static);
        return field != null ? (int)(field.GetValue(null) ?? 0) : -1;
    }

    private static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException("Expected " + typeof(TException).Name + ": " + message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class StubMetrics : FerriteLib.UiKit.Kernel.ITextMetrics
    {
        /// <summary>
        /// Wrap-aware on purpose. The fit audit's height axis compares this against the band it was
        /// drawn into, so a font-constant answer makes every band at or above that constant pass no
        /// matter how long the string is: that is exactly how the help-panel header and the Packs row
        /// detail lines stayed green in this gate while visibly overdrawn in game. Height is therefore
        /// derived from the same half-width advance model as MeasureWidth.
        /// </summary>
        public float MeasureText(string text, FerriteLib.UiKit.Kernel.UiFont font, float width)
        {
            float need = MeasureWidth(text, font);
            if (need <= 0f) return LineHeight(font);
            int lines = (int)Math.Ceiling(need / Math.Max(1f, width));
            return lines * LineHeight(font);
        }

        /// <summary>
        /// One text line including leading, per font. Calibrated 2026-09-04 against the real font
        /// engine: the in-game fit audit (ui.text.overflow, English client) reported one-line needs of
        /// 18.0px tiny, 21.33333px small and 30.0px medium while these constants said 15/19/21, which
        /// is how constant bands of 16/18/24/26/28 passed this sweep and still clipped in game. Line
        /// advance is a per-font constant (it does not depend on the glyph set), so one calibrated
        /// table serves both language tables; the width axis keeps its own half-width model.
        /// </summary>
        private static float LineHeight(FerriteLib.UiKit.Kernel.UiFont font) => font switch
        {
            FerriteLib.UiKit.Kernel.UiFont.Tiny => 18f,
            FerriteLib.UiKit.Kernel.UiFont.Medium => 30f,
            _ => 21.33333f,
        };

        // Half-width advance model (CJK/full-width = one em, Latin = half an em), matching the Verse
        // stub's own CalcSize so harness-level and production-level widths agree.
        public float MeasureWidth(string text, FerriteLib.UiKit.Kernel.UiFont font)
        {
            float em = font switch
            {
                FerriteLib.UiKit.Kernel.UiFont.Tiny => 12f,
                FerriteLib.UiKit.Kernel.UiFont.Medium => 18f,
                _ => 16f
            };

            float units = 0f;
            foreach (char c in text ?? "")
            {
                units += IsWide(c) ? 2f : 1f;
            }

            return units * em * 0.5f;
        }

        private static bool IsWide(char c)
        {
            return c >= '\u2E80' && (
                c <= '\u303F'
                || (c >= '\u3400' && c <= '\u4DBF')
                || (c >= '\u4E00' && c <= '\u9FFF')
                || (c >= '\uAC00' && c <= '\uD7AF')
                || (c >= '\uF900' && c <= '\uFAFF')
                || (c >= '\uFF00' && c <= '\uFF60')
                || (c >= '\uFFE0' && c <= '\uFFE6'));
        }
    }

    private sealed class StubTranslation : FerriteLib.UiKit.Kernel.IUiTranslation
    {
        public string Translate(string key)
        {
            return key ?? "";
        }

        public int TranslationRevision => 0;
    }

    /// <summary>
    /// The chrome and the whole-frame failure state machine belong to <c>UiWindowHost</c> since FL P2
    /// and US deleted its own copy of both, so this lane proves from the consumer's side what the
    /// settings window now trusts:
    ///  - a pass whose CreateHost throws reports the failure and draws NO notice in that same pass;
    ///  - the NEXT pass is the first PageUnavailable pass, and the page is never rebuilt after that;
    ///  - an unmet prerequisite draws the Prerequisite notice without ever building a page, so the two
    ///    notices stay distinguishable (US branches one DrawNotice on them);
    ///  - closing disposes the page session (the shell's PreClose), which is the half US must NOT redo.
    ///
    /// It drives a probe subclass rather than UniversalSqueakerSettingsWindow itself because the Verse
    /// stub deliberately ships no Verse.Mod, so the real window type cannot be constructed here; the
    /// notice TEXTS are US's and are pinned by the Keyed localization gate instead.
    /// </summary>
    private static void SettingsWindowShellCarriesTheFailureContract()
    {
        var failing = new ShellProbeWindow(throwOnCreate: true) { Prerequisite = true };
        failing.windowRect = new Rect(0f, 0f, 900f, 700f);

        failing.WindowOnGUI();
        Assert(failing.HostCreations == 1, "the first pass tries to build the page");
        Assert(failing.Failures == 1, "and reports the throw exactly once");
        Assert(failing.Notices.Count == 0,
            "the throwing pass must not switch to the notice inside itself - that pass already claimed"
            + " the page's layout state (the condition c next-frame rule)");

        failing.WindowOnGUI();
        Assert(failing.Notices.Count == 1 && failing.Notices[0] == UiWindowNotice.PageUnavailable,
            "the next pass is the first unavailable notice, drawn from a clean pass");

        failing.WindowOnGUI();
        Assert(failing.HostCreations == 1 && failing.Failures == 1 && failing.Notices.Count == 2,
            "a tripped window neither retries the page nor re-logs it; it just keeps saying the same thing");

        var blocked = new ShellProbeWindow(throwOnCreate: false) { Prerequisite = false };
        blocked.windowRect = new Rect(0f, 0f, 900f, 700f);
        blocked.WindowOnGUI();
        Assert(blocked.HostCreations == 0, "an unmet prerequisite never reaches page creation");
        Assert(blocked.BuiltSession == null,
            "and there is no page session to check: a pass that builds nothing has no page that could have drawn");
        Assert(blocked.Notices.Count == 1 && blocked.Notices[0] == UiWindowNotice.Prerequisite,
            "and it names the desync instead of the generic unavailable page");

        var healthy = new ShellProbeWindow(throwOnCreate: false) { Prerequisite = true };
        healthy.windowRect = new Rect(0f, 0f, 900f, 700f);
        healthy.WindowOnGUI();
        Assert(healthy.HostCreations == 1 && healthy.Notices.Count == 0 && healthy.Failures == 0,
            "a real US page under the shell draws with no notice and no failure");
        UiSession? built = healthy.BuiltSession;
        Assert(built != null && built.IsActive,
            "the probe must have a live session to test the close path");
        // The two notice passes above build no session at all (nothing drew), but this pass draws a real
        // page under the shell: assert the page DREW, not merely that the window lived (see UsTripGuard).
        UsTripGuard.ExpectNoTrips(built!, "SettingsWindowShellCarriesTheFailureContract");
        healthy.PreClose();
        Assert(built != null && !built.IsActive,
            "the shell's PreClose disposes the page session - US keeps only its own audit teardown");
        Assert(healthy.BasePreCloseCalls == 1, "and the consumer override still chains to the shell");
    }

    /// <summary>A <see cref="UiWindowHost"/> that records what the shell tells it to do.</summary>
    private sealed class ShellProbeWindow : UiWindowHost
    {
        private readonly bool throwOnCreate;

        public ShellProbeWindow(bool throwOnCreate)
        {
            this.throwOnCreate = throwOnCreate;
        }

        public readonly List<UiWindowNotice> Notices = new();
        public int HostCreations;
        public int Failures;
        public int BasePreCloseCalls;
        public bool Prerequisite = true;
        public UiSession? BuiltSession;

        protected override UiTheme Theme => UiTheme.DarkGold;
        protected override string Title => "probe title";
        protected override string Subtitle => "probe subtitle";
        protected override string CloseText => "probe close";
        protected override bool PrerequisiteVerified => Prerequisite;

        protected override UiHost CreateHost()
        {
            HostCreations++;
            if (throwOnCreate)
            {
                throw new InvalidOperationException("planted page creation failure");
            }

            // A real production US page, so the healthy pass exercises the shell against the manifest,
            // widgets and bindings the settings window actually uses.
            UiHost host = UsKernelOverlayHost.Create(new RecordingOverlaySource());
            BuiltSession = host.Session;
            return host;
        }

        protected override void DrawNotice(Rect rect, UiWindowNotice notice)
        {
            Notices.Add(notice);
        }

        protected override void OnDrawFailure(Exception error)
        {
            Failures++;
        }

        public override void PreClose()
        {
            BasePreCloseCalls++;
            base.PreClose();
        }
    }
}
