using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Focused tests for the new creation-time and interaction contracts of the greenfield kernel:
/// unknown-attribute rejection, unified Bind/OptionsBind/ActionBind semantics (Bind falls back to
/// Id), missing-binding and type errors at Host creation, Fill layout (viewport/content
/// separation), session scroll targets and the chart's native hotControl drag loop.
/// </summary>
internal static class KernelContractTests
{
    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Host rejects unknown widget attributes", VerifyUnknownWidgetAttributeRejected);
        failures += Run("Host rejects unknown container attributes", VerifyUnknownContainerAttributeRejected);
        failures += Run("Bind falls back to Id and both resolve", VerifyBindFallsBackToId);
        failures += Run("Missing binding fails at creation", VerifyMissingBindingFailsAtCreation);
        failures += Run("ActionBind missing/wrong type fails at creation", VerifyActionBindValidated);
        failures += Run("Fill scroll splits viewport and content heights", VerifyFillScrollViewport);
        failures += Run("Host applies session scroll target", VerifyScrollTargetApplied);
        failures += Run("Chart drag uses native hotControl lifecycle", VerifyChartHotControlDrag);
        failures += Run("Host close mid-drag releases only its session capture", VerifyCloseDuringDragReleasesOwnCapture);
        failures += Run("Host close does not release another session's capture", VerifyCrossHostCloseIsolated);
        failures += Run("Session content revision invalidates the layout cache", VerifyContentRevisionInvalidatesLayout);
        failures += Run("Neutral full-page manifest creates host", VerifyNeutralFullPageManifestCreatesHost);
        failures += Run("Duplicate widget kind registration is rejected", VerifyDuplicateRegistrationRejected);
        return failures;
    }

    private static int Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.Message);
            return 1;
        }
    }

    private static void VerifyUnknownWidgetAttributeRejected()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"volume\" Kind=\"input/stepper-slider\" Bogus=\"1\" />"
            + "</UiPage>");

        var bindings = new UiBindings();
        bindings.BindValue("volume", () => 0.5f, _ => { });

        try
        {
            using UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
            throw new Exception("Unknown widget attribute was not rejected");
        }
        catch (UiContractException ex)
        {
            if (!ex.Message.Contains("Bogus")) throw new Exception("Diagnostic lacks attribute name: " + ex.Message);
            if (!ex.ElementPath.Contains("volume")) throw new Exception("Diagnostic lacks element path: " + ex.Message);
        }
    }

    private static void VerifyUnknownContainerAttributeRejected()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Stack Id=\"root\" Nope=\"1\">"
            + "<Widget Id=\"a\" Kind=\"chrome/banner\" Text=\"x\" />"
            + "</Stack>"
            + "</UiPage>");

        var bindings = new UiBindings();
        try
        {
            using UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
            throw new Exception("Unknown container attribute was not rejected");
        }
        catch (UiContractException ex)
        {
            if (!ex.Message.Contains("Nope")) throw new Exception("Diagnostic lacks attribute name: " + ex.Message);
            if (!ex.ElementPath.Contains("root")) throw new Exception("Diagnostic lacks container path: " + ex.Message);
        }
    }

    private static void VerifyBindFallsBackToId()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        // Same kind twice: one keyed by Id, one by an explicit Bind pointing at a different key.
        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"by-id\" Kind=\"input/stepper-slider\" />"
            + "<Widget Id=\"by-attr\" Kind=\"input/stepper-slider\" Bind=\"explicit-key\" />"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        bindings.BindValue("by-id", () => 0.1f, _ => { });
        bindings.BindValue("explicit-key", () => 0.9f, _ => { });

        using UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        host.Close();

        // The reverse must fail: Bind pointing at an unregistered key.
        var missing = new UiBindings();
        missing.BindValue("by-attr", () => 0.1f, _ => { });
        try
        {
            using UiHost bad = new("test", manifest, missing, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
            throw new Exception("Explicit Bind key mismatch was not rejected");
        }
        catch (UiContractException)
        {
            // expected
        }
    }

    private static void VerifyMissingBindingFailsAtCreation()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"volume\" Kind=\"input/stepper-slider\" />"
            + "</UiPage>");

        var bindings = new UiBindings();
        try
        {
            using UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
            throw new Exception("Missing binding was not rejected");
        }
        catch (UiContractException ex)
        {
            if (!ex.Message.Contains("volume")) throw new Exception("Diagnostic lacks binding key: " + ex.Message);
        }
    }

    private static void VerifyActionBindValidated()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"chart\" Kind=\"chart/line\" Bind=\"points\" ActionBind=\"point-changed\" Editable=\"true\" EditablePoints=\"1,2\" />"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);

        // Missing action binding.
        var missing = new UiBindings();
        missing.BindReadOnly<IReadOnlyList<Vector2>>("points", () => new List<Vector2> { new(0f, 1f), new(1f, 0f) });
        try
        {
            using UiHost missingHost = new("test", manifest, missing, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
            throw new Exception("Missing ActionBind was not rejected");
        }
        catch (UiContractException ex)
        {
            if (!ex.Message.Contains("point-changed")) throw new Exception("Diagnostic lacks action key: " + ex.Message);
        }

        // Wrong payload type.
        var wrongType = new UiBindings();
        wrongType.BindReadOnly<IReadOnlyList<Vector2>>("points", () => new List<Vector2> { new(0f, 1f), new(1f, 0f) });
        wrongType.BindAction<string>("point-changed", _ => { });
        try
        {
            using UiHost wrongHost = new("test", manifest, wrongType, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
            throw new Exception("Wrong ActionBind payload type was not rejected");
        }
        catch (UiContractException)
        {
            // expected
        }

        // Correct contract creates.
        var good = new UiBindings();
        good.BindReadOnly<IReadOnlyList<Vector2>>("points", () => new List<Vector2> { new(0f, 1f), new(1f, 0f) });
        good.BindAction<UiChartPointChange>("point-changed", _ => { });
        using UiHost host = new("test", manifest, good, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        host.Close();
    }

    private static void VerifyFillScrollViewport()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        var xmlBuilder = new System.Text.StringBuilder();
        xmlBuilder.Append("<UiPage Schema=\"2\" Source=\"test\">");
        xmlBuilder.Append("<Row Id=\"body\" Gap=\"10\">");
        xmlBuilder.Append("<Column Id=\"nav\" Width=\"100\" Fill=\"true\" />");
        xmlBuilder.Append("<Scroll Id=\"content\" Fill=\"true\">");
        for (int i = 0; i < 20; i++)
        {
            xmlBuilder.Append("<Widget Id=\"a").Append(i).Append("\" Kind=\"chrome/banner\" Text=\"x\" />");
        }

        xmlBuilder.Append("</Scroll>");
        xmlBuilder.Append("</Row>");
        xmlBuilder.Append("</UiPage>");
        string xml = xmlBuilder.ToString();

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();

        using UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(800f, 300f));

        // Fill scroll viewport height = available height; content keeps its natural height.
        if (!snapshot.Viewports.TryGetValue("content", out Rect viewport))
        {
            throw new Exception("Fill scroll produced no viewport");
        }

        if (Math.Abs(viewport.height - 300f) > 0.01f)
        {
            throw new Exception("Fill scroll viewport height != available height (" + viewport.height + ")");
        }

        if (!snapshot.ScrollContents.TryGetValue("content", out Rect content))
        {
            throw new Exception("Fill scroll produced no content rect");
        }

        if (content.height <= viewport.height)
        {
            throw new Exception("Fill scroll content did not keep its natural height");
        }

        if (!snapshot.RectById.TryGetValue("nav", out Rect nav) || Math.Abs(nav.height - 300f) > 0.01f)
        {
            throw new Exception("Fill column did not stretch to available height");
        }
    }

    private static void VerifyScrollTargetApplied()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Scroll Id=\"scroll\" Height=\"50\">"
            + "<Widget Id=\"a\" Kind=\"chrome/banner\" Text=\"x\" Height=\"30\" />"
            + "<Widget Id=\"b\" Kind=\"chrome/banner\" Text=\"y\" Height=\"30\" />"
            + "<Widget Id=\"target\" Kind=\"chrome/banner\" Text=\"z\" Height=\"30\" />"
            + "</Scroll>"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();

        using UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());

        host.Session.SetScrollTarget("target");
        host.DrawFrame(new Rect(0f, 0f, 200f, 200f));

        Vector2 pos = host.Session.GetScrollPosition("scroll");
        if (Math.Abs(pos.y - 40f) > 0.01f)
        {
            throw new Exception("Scroll target did not position the scroll (" + pos.y + ", expected 40)");
        }

        if (host.Session.ScrollTargetElementId != null)
        {
            throw new Exception("Resolved scroll target was not cleared");
        }

        // Pending target for a hidden (unresolvable) element stays pending.
        host.Session.SetScrollTarget("missing-id");
        host.DrawFrame(new Rect(0f, 0f, 200f, 200f));
        if (host.Session.ScrollTargetElementId == null)
        {
            throw new Exception("Unresolvable scroll target was dropped instead of staying pending");
        }
    }

    private static void VerifyChartHotControlDrag()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"chart\" Kind=\"chart/line\" Bind=\"points\" ActionBind=\"point-changed\" Editable=\"true\" EditablePoints=\"1,2\" Height=\"60\" />"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        var points = new List<Vector2> { new(0f, 1f), new(0.3f, 1f), new(0.7f, 0f), new(1f, 0f) };
        var changes = new List<UiChartPointChange>();
        bindings.BindReadOnly<IReadOnlyList<Vector2>>("points", () => points);
        bindings.BindAction<UiChartPointChange>("point-changed", change => changes.Add(change));

        using UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 100f));
        Rect chartRect = snapshot.RectById["chart"];

        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMouseDown = false;
            UiNative.DebugMouseDrag = false;
            UiNative.DebugMouseUp = false;
            UiNative.DebugHotControl = 0;
            UiNative.DebugControlIdCounter = 0;

            // MouseDown on editable point 2 (x=0.3, y=1): plot padding 8, width 300-16=284,
            // x = 8 + 0.3*284 = 93.2; y = 8 (top).
            UiNative.DebugMousePosition = new Vector2(chartRect.x + 93.2f, chartRect.y + 8f);
            UiNative.DebugMouseDown = true;
            host.BeginFrame();
            host.Draw(new Rect(0f, 0f, 300f, 100f), snapshot);
            host.EndFrame();

            if (UiNative.DebugHotControl == 0)
            {
                throw new Exception("MouseDown did not capture the native hot control");
            }

            // Drag to x=0.6 while captured (pointer leaves the chart rect to prove capture).
            UiNative.DebugMouseDown = false;
            UiNative.DebugMouseDrag = true;
            UiNative.DebugMousePosition = new Vector2(chartRect.x + 500f, chartRect.y + 500f);
            host.BeginFrame();
            host.Draw(new Rect(0f, 0f, 300f, 100f), snapshot);
            host.EndFrame();

            if (changes.Count == 0)
            {
                throw new Exception("Drag while captured emitted no typed change");
            }

            if (changes[0].Index != 1 || Math.Abs(changes[0].X - 1f) > 0.0001f)
            {
                throw new Exception("Drag emitted wrong index/coordinate: " + changes[0].Index + "," + changes[0].X);
            }

            // MouseUp releases the hot control.
            UiNative.DebugMouseDrag = false;
            UiNative.DebugMouseUp = true;
            host.BeginFrame();
            host.Draw(new Rect(0f, 0f, 300f, 100f), snapshot);
            host.EndFrame();

            if (UiNative.DebugHotControl != 0)
            {
                throw new Exception("MouseUp did not release the native hot control");
            }

            // A fresh MouseDown outside any editable point must not capture.
            UiNative.DebugMouseUp = false;
            UiNative.DebugMouseDown = true;
            UiNative.DebugMousePosition = new Vector2(chartRect.x + 8f, chartRect.y + 8f); // point 0 is not editable
            host.BeginFrame();
            host.Draw(new Rect(0f, 0f, 300f, 100f), snapshot);
            host.EndFrame();

            if (UiNative.DebugHotControl != 0)
            {
                throw new Exception("MouseDown on a non-editable point captured the hot control");
            }
        }
        finally
        {
            UiNative.DebugMousePositionEnabled = false;
            UiNative.DebugMouseDown = false;
            UiNative.DebugMouseDrag = false;
            UiNative.DebugMouseUp = false;
            UiNative.DebugHotControl = 0;
            UiNative.DebugControlIdCounter = 0;
        }
    }

    private static void VerifyContentRevisionInvalidatesLayout()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register("rev-test", "test/height", () => new HeightBoundWidget());

        string xml =
            "<UiPage Schema=\"2\" Source=\"rev-test\">"
            + "<Widget Id=\"h\" Kind=\"test/height\" />"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        float current = 10f;
        bindings.BindReadOnly("height", () => current);

        using UiHost host = new("rev-test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        UiLayoutSnapshot first = host.MeasureAndArrange(new Vector2(200f, 200f));
        if (Math.Abs(first.RectById["h"].height - 10f) > 0.01f)
        {
            throw new Exception("Initial measure did not reflect the binding height");
        }

        // Same revision and size: the arranged snapshot is reused.
        UiLayoutSnapshot cached = host.MeasureAndArrange(new Vector2(200f, 200f));
        if (!ReferenceEquals(first, cached))
        {
            throw new Exception("Same revision re-arranged instead of using the cached snapshot");
        }

        // The binding changes but the revision does not: the cache stays (stale) until bumped.
        current = 30f;
        UiLayoutSnapshot stale = host.MeasureAndArrange(new Vector2(200f, 200f));
        if (!ReferenceEquals(stale, cached) || Math.Abs(stale.RectById["h"].height - 10f) > 0.01f)
        {
            throw new Exception("Unbumped content change re-arranged or changed heights");
        }

        // Bumping the session content revision forces re-measure with the new value.
        host.Session.BumpContentRevision();
        UiLayoutSnapshot refreshed = host.MeasureAndArrange(new Vector2(200f, 200f));
        if (Math.Abs(refreshed.RectById["h"].height - 30f) > 0.01f)
        {
            throw new Exception("Content revision bump did not re-arrange with the new height");
        }

        // A different available size must bypass the single-slot cache (width-change invalidation).
        UiLayoutSnapshot resized = host.MeasureAndArrange(new Vector2(120f, 200f));
        if (ReferenceEquals(resized, refreshed))
        {
            throw new Exception("Available-size change reused the cached snapshot");
        }

        UiLayoutSnapshot backToOriginal = host.MeasureAndArrange(new Vector2(200f, 200f));
        if (ReferenceEquals(backToOriginal, resized))
        {
            throw new Exception("Returning to the previous size reused the resized snapshot");
        }
    }

    private sealed class HeightBoundWidget : IUiWidget
    {
        public string Kind => "test/height";

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            return ctx.Bindings.TryGet("height", out float height) ? height : 0f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }

    private static void VerifyCloseDuringDragReleasesOwnCapture()
    {
        (UiHost host, UiLayoutSnapshot snapshot, Rect chartRect) = CreateChartHost("chart", 300f, 100f);

        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMouseDown = true;
            UiNative.DebugMouseDrag = false;
            UiNative.DebugMouseUp = false;
            UiNative.DebugHotControl = 0;

            // MouseDown on editable point 2 (x=0.3, y=1) captures through the session path.
            UiNative.DebugMousePosition = new Vector2(chartRect.x + 93.2f, chartRect.y + 8f);
            host.BeginFrame();
            host.Draw(new Rect(0f, 0f, 300f, 100f), snapshot);
            host.EndFrame();

            if (UiNative.DebugHotControl == 0)
            {
                throw new Exception("MouseDown did not capture through the session path");
            }

            int capturedId = UiNative.DebugHotControl;
            if (!host.Session.IsHotControlOwned(capturedId))
            {
                throw new Exception("Session does not record ownership of the captured hot control");
            }

            // Close mid-drag (no MouseUp): Host.Close disposes the session, which releases exactly
            // the capture this session owns and clears popup/focus/drag state.
            host.Close();
            if (UiNative.DebugHotControl != 0)
            {
                throw new Exception("Host close did not release its session's owned hot control");
            }

            if (host.Session.OwnedHotControl != null || host.Session.IsActive)
            {
                throw new Exception("Host close left the session ownership record or active flag behind");
            }
        }
        finally
        {
            UiNative.DebugMousePositionEnabled = false;
            UiNative.DebugMouseDown = false;
            UiNative.DebugMouseDrag = false;
            UiNative.DebugMouseUp = false;
            UiNative.DebugHotControl = 0;
            host.Close();
        }
    }

    private static void VerifyCrossHostCloseIsolated()
    {
        (UiHost hostA, UiLayoutSnapshot snapshotA, Rect chartRectA) = CreateChartHost("chart-a", 300f, 100f);
        (UiHost hostB, UiLayoutSnapshot snapshotB, Rect chartRectB) = CreateChartHost("chart-b", 300f, 100f);

        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMouseDown = true;
            UiNative.DebugMouseDrag = false;
            UiNative.DebugMouseUp = false;
            UiNative.DebugHotControl = 0;

            // Both hosts capture their own chart point mid-drag.
            UiNative.DebugMousePosition = new Vector2(chartRectA.x + 93.2f, chartRectA.y + 8f);
            hostA.BeginFrame();
            hostA.Draw(new Rect(0f, 0f, 300f, 100f), snapshotA);
            hostA.EndFrame();

            UiNative.DebugMousePosition = new Vector2(chartRectB.x + 93.2f, chartRectB.y + 8f);
            hostB.BeginFrame();
            hostB.Draw(new Rect(0f, 0f, 300f, 100f), snapshotB);
            hostB.EndFrame();

            int captureB = UiNative.DebugHotControl;
            if (captureB == 0 || !hostB.Session.IsHotControlOwned(captureB))
            {
                throw new Exception("Host B did not capture through its session");
            }

            // Closing host A must not release host B's native capture nor touch B's ownership.
            hostA.Close();
            if (UiNative.DebugHotControl != captureB)
            {
                throw new Exception("Closing host A released host B's native capture");
            }

            if (!hostB.Session.IsHotControlOwned(captureB))
            {
                throw new Exception("Closing host A cleared host B's ownership record");
            }

            // Host B can still release its own capture.
            UiNative.DebugMouseDown = false;
            UiNative.DebugMouseUp = true;
            hostB.BeginFrame();
            hostB.Draw(new Rect(0f, 0f, 300f, 100f), snapshotB);
            hostB.EndFrame();
            if (UiNative.DebugHotControl != 0)
            {
                throw new Exception("Host B could not release its own capture after host A closed");
            }
        }
        finally
        {
            UiNative.DebugMousePositionEnabled = false;
            UiNative.DebugMouseDown = false;
            UiNative.DebugMouseDrag = false;
            UiNative.DebugMouseUp = false;
            UiNative.DebugHotControl = 0;
            hostA.Close();
            hostB.Close();
        }
    }

    private static (UiHost Host, UiLayoutSnapshot Snapshot, Rect ChartRect) CreateChartHost(string elementId, float width, float height)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"" + elementId + "\" Kind=\"chart/line\" Bind=\"" + elementId + "-points\""
            + " ActionBind=\"" + elementId + "-changed\" Editable=\"true\" EditablePoints=\"1,2\" Height=\"60\" />"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        var points = new List<Vector2> { new(0f, 1f), new(0.3f, 1f), new(0.7f, 0f), new(1f, 0f) };
        bindings.BindReadOnly<IReadOnlyList<Vector2>>(elementId + "-points", () => points);
        bindings.BindAction<UiChartPointChange>(elementId + "-changed", _ => { });

        UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(width, height));
        return (host, snapshot, snapshot.RectById[elementId]);
    }

    /// <summary>
    /// Neutral full-page fixture that mirrors the real product page root: a Column root with a
    /// body Row of Fill children and a fixed-height footer. The fixture uses only neutral kinds so
    /// the library test tree stays free of product literals; the real manifest's shape is pinned
    /// by the US-owned structural guard.
    /// </summary>
    private static void VerifyNeutralFullPageManifestCreatesHost()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        const string scope = "neutral-test";
        RegisterNeutralSectionKinds(scope);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + scope + "\">"
            + "<Column Id=\"page-root\" Gap=\"6\" Padding=\"12\">"
            + "<Row Id=\"body-row\" Gap=\"12\">"
            + "<Column Id=\"nav-column\" Width=\"176\" Fill=\"true\">"
            + "<Widget Id=\"nav\" Kind=\"neutral/section\" />"
            + "</Column>"
            + "<Scroll Id=\"content-scroll\" Fill=\"true\" Gap=\"10\">"
            + "<Widget Id=\"page-title\" Kind=\"neutral/section\" />"
            + "<Widget Id=\"section-a\" Kind=\"neutral/section\" Tab=\"GroupA\" />"
            + "<Widget Id=\"section-b\" Kind=\"neutral/section\" Tab=\"GroupB\" />"
            + "</Scroll>"
            + "<Scroll Id=\"help-scroll\" Width=\"200\" Fill=\"true\">"
            + "<Widget Id=\"help-panel\" Kind=\"neutral/section\" />"
            + "</Scroll>"
            + "</Row>"
            + "<Widget Id=\"footer\" Kind=\"neutral/footer\" Height=\"28\" />"
            + "</Column>"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        bindings.BindValue("active-tab", () => "GroupA", _ => { });

        using UiHost host = new(scope, manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(800f, 600f));

        if (!snapshot.Viewports.TryGetValue("content-scroll", out Rect contentViewport)
            || !snapshot.Viewports.TryGetValue("help-scroll", out Rect helpViewport))
        {
            throw new Exception("Fill scrolls produced no viewports");
        }

        if (!snapshot.RectById.TryGetValue("footer", out Rect footer))
        {
            throw new Exception("Footer produced no rect");
        }

        // Real-equivalent root contract: the fixed footer sits below every scroll viewport with
        // the declared gap, and its bottom stays inside the window (no overlap, no clipping).
        if (footer.y < contentViewport.yMax + 6f - 0.01f || footer.y < helpViewport.yMax + 6f - 0.01f)
        {
            throw new Exception("Footer overlaps a Fill scroll viewport (footer.y=" + footer.y
                + ", content.yMax=" + contentViewport.yMax + ", help.yMax=" + helpViewport.yMax + ")");
        }

        if (footer.yMax > 600f - 12f + 0.01f)
        {
            throw new Exception("Footer bottom leaves the window (footer.yMax=" + footer.yMax + ")");
        }

        if (Math.Abs(snapshot.ContentSize.y - 600f) > 0.01f)
        {
            throw new Exception("Root content height does not fit the window (" + snapshot.ContentSize.y + ")");
        }

        host.Close();
    }

    private static void RegisterNeutralSectionKinds(string scope)
    {
        string[] sectionSchema = { "Id", "Kind", "Title", "TitleKey", "HelpKey", "Tab", "Hidden", "Height" };
        string[] footerSchema = { "Id", "Kind", "Height", "Tab", "Hidden" };

        UiWidgetRegistry.Register(scope, "neutral/section", () => new StubWidget("neutral/section"), sectionSchema);
        UiWidgetRegistry.Register(scope, "neutral/footer", () => new StubWidget("neutral/footer"), footerSchema);
    }

    private static void VerifyDuplicateRegistrationRejected()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        UiWidgetRegistry.Register("dup-test", "test/kind", () => new StubWidget("test/kind"));
        try
        {
            UiWidgetRegistry.Register("dup-test", "test/kind", () => new StubWidget("test/kind"));
            throw new Exception("Duplicate kind registration was not rejected");
        }
        catch (InvalidOperationException ex)
        {
            if (!ex.Message.Contains("test/kind") || !ex.Message.Contains("dup-test"))
            {
                throw new Exception("Duplicate diagnostic lacks kind/scope: " + ex.Message);
            }
        }

        // The explicit reset path keeps initialization idempotent: clearing then re-registering
        // the same kind succeeds, and InitializeCore stays a no-op after the first call.
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register("dup-test", "test/kind", () => new StubWidget("test/kind"));
        UiWidgetRegistry.Clear();
    }

    private sealed class StubWidget : IUiWidget
    {
        private readonly string kind;

        public StubWidget(string kind)
        {
            this.kind = kind;
        }

        public string Kind => kind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            return 24f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return font switch
            {
                UiFont.Tiny => 16f,
                UiFont.Small => 24f,
                _ => 32f
            };
        }
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key)
        {
            return "[" + key + "]";
        }
    }
}
