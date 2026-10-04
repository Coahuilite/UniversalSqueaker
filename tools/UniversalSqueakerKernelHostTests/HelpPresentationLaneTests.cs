using System;
using System.Collections.Generic;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// (乙1) lane: ONE player intent, TWO mutually exclusive help presentations, decided by the page box the
/// shell ACTUALLY hands the page this pass (fed by the settings window), never by a prediction of the
/// post-resize one. At the game's minimum logical screen 1024x768 the open page box leaves the centre column
/// 416px, so the help column shares the page with the settings; a box that cannot afford the column gets the
/// full-width band instead.
///
/// Mutation proofs, named at each clause: reverting <c>DrawerSharesTheBody</c> to the pre-fix full-delta
/// question reddens the pinned arithmetic and the acceptance shape; making the host ignore the fed width
/// (predicting the widened box) reddens the pre-resize edge clause's fit gate - that pairing is the overflow
/// this round fixes. Every clause arranges a REAL page box, so a width the policy cannot produce is never
/// asserted against.
/// </summary>
internal static class HelpPresentationLaneTests
{
    // The shell's page box (UiWindowHost.ContentRect: window minus 2 x 20 chrome, minus the 56px title bar
    // and the 20px bottom inset). page-root then takes its own declared Padding 12 out of it.
    private static readonly Vector2 AcceptancePageBox = new(984f, 524f);  // 1024x768 -> window 1024x600
    private static readonly Vector2 PreResizePageBox = new(760f, 524f);   // the closed 800x600 window's box
    private static readonly Vector2 WidePageBox = new(1252f, 644f);       // 1920x1080 -> window 1292x720
    private const int MinimumScreenWidth = 1024;
    private const int MinimumScreenHeight = 768;
    private const string WideId = "help-scroll";
    private const string NarrowId = "help-band";

    public static int RunAll()
    {
        Step("the help presentation is decided by the actual page box", ExactlyOnePresentationPerScreen);
        Console.WriteLine("HelpPresentationLaneTests ALL PASS");
        return 0;
    }

    private static void ExactlyOnePresentationPerScreen()
    {
        int savedWidth = Verse.UI.screenWidth;
        int savedHeight = Verse.UI.screenHeight;
        var metrics = new Program.StubMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            // The policy answer per ACTUAL box, before any page exists. Mutation target: the pre-fix
            // full-delta question over the SCREEN answered false at 984 (the playtest failure).
            Assert(WindowChromeLayout.DrawerSharesTheBody(WidePageBox.x),
                "the 1920x1080 open page box must share");
            Assert(WindowChromeLayout.DrawerSharesTheBody(AcceptancePageBox.x),
                "the acceptance page box 984 must share: centre = 984 - 24 - 200 - 320 - 24 = 416");
            Assert(!WindowChromeLayout.DrawerSharesTheBody(PreResizePageBox.x),
                "the pre-resize page box 760 cannot afford the column (centre 192)");
            Assert(Math.Abs(WindowChromeLayout.CentreColumnWidthInPage(AcceptancePageBox.x) - 416f) < 0.01f,
                "CentreColumnWidthInPage(984) must be 416, got "
                + WindowChromeLayout.CentreColumnWidthInPage(AcceptancePageBox.x));

            // The real Keyed tables: this lane measures WIDTHS, and a lane without them measures the keys.
            foreach (string language in new[] { "English", "ChineseSimplified" })
            {
                Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
                AssertAcceptanceBoxSharesThePage(language, metrics, reports);
                AssertPreResizeEdgeStaysInBounds(language, metrics, reports);
                AssertWideScreenShares(language, metrics, reports);
                AssertBandFallbackStress(language, metrics, reports);
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
            UiFitAudit.Detach();
            UiFitAudit.Enabled = false;
            Verse.UI.screenWidth = savedWidth;
            Verse.UI.screenHeight = savedHeight;
        }
    }

    /// <summary>
    /// THE ACCEPTANCE CONFIGURATION (B3.1/B3.2): screen 1024x768, drawer open, the ACTUAL page box 984 fed by
    /// the window. The settings body, the navigation (rect AND scroll viewport) and the centre scroll all stay
    /// arranged beside the 320px help column, the centre keeps the manifest's declared floor, the footer stays
    /// inside the page, the band is not arranged, and the fit audit reports nothing in EN and ZH.
    ///
    /// Mutation proof: reverting <c>DrawerSharesTheBody</c> to the pre-fix full-delta question makes the
    /// host's keys answer "band" here, so the help-scroll and band-absent clauses redden first.
    /// </summary>
    private static void AssertAcceptanceBoxSharesThePage(
        string language, Program.StubMetrics metrics, List<UiOverflowReport> reports)
    {
        Verse.UI.screenWidth = MinimumScreenWidth;
        Verse.UI.screenHeight = MinimumScreenHeight;
        string where = "at 1024x768 with the actual page box " + AcceptancePageBox.x + "x" + AcceptancePageBox.y
            + " (" + language + ")";

        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics, () => AcceptancePageBox.x);
        host.Bindings.Invoke("set-tab", "Overview");
        host.Bindings.Set("help-open", true);
        host.MeasureAndArrange(AcceptancePageBox);
        UiFitAudit.Reset();
        reports.Clear();
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(AcceptancePageBox);
        host.DrawChecked(new Rect(0f, 0f, AcceptancePageBox.x, AcceptancePageBox.y));

        Assert(snapshot.Viewports.TryGetValue(WideId, out Rect help),
            where + ": the help column must be the presentation, with a real scroll viewport");
        Assert(help.width > 0f && help.height > 0f,
            where + ": the help viewport must be usable, got " + Fmt(help));
        Assert(Math.Abs(help.width - WindowChromeLayout.HelpDrawerWidth) < 0.5f,
            where + ": the help column must keep its declared " + WindowChromeLayout.HelpDrawerWidth
            + "px width, got " + help.width);
        Assert(snapshot.RectById.TryGetValue("help-panel", out Rect helpPanel),
            where + ": the help panel must be arranged inside the help column");
        Assert(help.height >= helpPanel.height - 0.5f,
            where + ": the help viewport must be at least the panel's measured content, got viewport="
            + help.height + " panel=" + helpPanel.height);
        Assert(snapshot.RectById.ContainsKey("body-row") && snapshot.RectById.ContainsKey("nav-column"),
            where + ": the settings body and the navigation must stay arranged");
        Assert(snapshot.Viewports.TryGetValue("nav-column", out Rect navViewport),
            where + ": the navigation must keep a scroll viewport");
        Assert(navViewport.height > 0f && navViewport.width > 0f,
            where + ": the navigation's viewport must be a real reading surface (B3.1's reachable input)");
        Assert(snapshot.Viewports.TryGetValue("content-scroll", out Rect centre),
            where + ": the centre column must keep a live viewport");
        Assert(centre.width >= WindowChromeLayout.ResponsiveParameterRowBreakpoint - 0.5f,
            where + ": the centre column must keep the page's declared Breakpoint "
            + WindowChromeLayout.ResponsiveParameterRowBreakpoint + ", got " + centre.width);
        Assert(centre.x >= snapshot.RectById["nav-column"].xMax - 0.5f && help.x >= centre.xMax - 0.5f,
            where + ": nav, centre and help must be side by side, not stacked");
        Assert(!snapshot.Viewports.ContainsKey(NarrowId) && !snapshot.RectById.ContainsKey("help-panel-narrow"),
            where + ": the band must not be arranged when the box affords the third column");
        Assert(snapshot.RectById.TryGetValue("footer-band", out Rect footer),
            where + ": the footer band must be arranged");
        Assert(footer.yMax <= AcceptancePageBox.y + 0.5f,
            where + ": the footer must stay inside the page, got " + footer.yMax);
        Assert(reports.Count == 0,
            where + ": the fit audit must report nothing, got " + Describe(reports));

        Console.WriteLine("[acceptance] " + language + " box " + AcceptancePageBox.x + "x" + AcceptancePageBox.y
            + ": centre=" + Fmt1(centre.width) + " nav=" + Fmt1(navViewport.width)
            + " help=" + Fmt1(help.width) + " helpViewportH=" + Fmt1(help.height)
            + " footerBottom=" + Fmt1(footer.yMax) + " band=false fit=0");

        // B3.2's state edge: the close/reopen must preserve the centre column's scroll offset and every
        // column's node identity while the body stays arranged. The load-bearing clause is NODE IDENTITY,
        // not an offset: at this acceptance geometry the nav column's content (271) and the help panel's
        // (242) both FIT the 404px viewport under the stub ruler, so a non-zero offset there is not
        // representable and the engine clamps it; HelpDrawerLaneTests covers non-zero offsets at geometries
        // where they exist.
        UiNode? contentNodeOrNull = host.Session.GetNodeByElementId("content-scroll");
        UiNode? navNodeOrNull = host.Session.GetNodeByElementId("nav-column");
        UiNode? helpNodeOrNull = host.Session.GetNodeByElementId(WideId);
        Assert(contentNodeOrNull != null && navNodeOrNull != null && helpNodeOrNull != null,
            where + ": the three scroll columns must be arranged nodes for the identity clause to mean anything");
        UiNode contentNode = contentNodeOrNull!;
        UiNode navNode = navNodeOrNull!;
        UiNode helpNode = helpNodeOrNull!;
        Program.SetScrollPositionById(host.Session, "content-scroll", new Vector2(0f, 90f));
        host.Bindings.Set("help-open", false);
        host.MeasureAndArrange(AcceptancePageBox);
        host.Bindings.Set("help-open", true);
        UiLayoutSnapshot reopened = host.MeasureAndArrange(AcceptancePageBox);
        Assert(reopened.RectById.ContainsKey("body-row") && reopened.Viewports.ContainsKey(WideId),
            where + ": reopening must restore the help column beside the still-arranged body");
        Assert(Math.Abs(Program.ScrollPositionById(host.Session, "content-scroll").y - 90f) < 0.01f,
            where + ": the centre column's scroll offset must survive a close/reopen");
        Assert(ReferenceEquals(contentNode, host.Session.GetNodeByElementId("content-scroll"))
                && ReferenceEquals(navNode, host.Session.GetNodeByElementId("nav-column"))
                && ReferenceEquals(helpNode, host.Session.GetNodeByElementId(WideId)),
            where + ": all three columns must keep their node identity across a close/reopen");
    }

    /// <summary>
    /// THE PRE-RESIZE EDGE (this round's regression proof, now GATED). The pass that first presents the open
    /// drawer may still be handed the CLOSED page box; the criterion must answer "cannot share" there, the
    /// pass must be fit-silent in EN and ZH, and the help must stay in bounds. Feeding the resized box on the
    /// same host must then present the sharing shape again, so the settings viewport is reachable as soon as
    /// the window has resized - without recreating the host.
    ///
    /// Mutation proof: ignoring the fed width (using the policy's open box, 984, while the shell hands the
    /// page 760) arranges the wide column into 760 and the fit gate below reddens with the measured overflow.
    /// </summary>
    private static void AssertPreResizeEdgeStaysInBounds(
        string language, Program.StubMetrics metrics, List<UiOverflowReport> reports)
    {
        Verse.UI.screenWidth = MinimumScreenWidth;
        Verse.UI.screenHeight = MinimumScreenHeight;
        string where = "at the pre-resize page box " + PreResizePageBox.x + " (" + language + ")";

        float fedWidth = PreResizePageBox.x;
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics, () => fedWidth);
        host.Bindings.Invoke("set-tab", "Overview");
        host.Bindings.Set("help-open", true);
        host.MeasureAndArrange(PreResizePageBox);
        UiFitAudit.Reset();
        reports.Clear();
        UiLayoutSnapshot edge = host.MeasureAndArrange(PreResizePageBox);
        host.DrawChecked(new Rect(0f, 0f, PreResizePageBox.x, PreResizePageBox.y));

        Assert(host.Bindings.Get<bool>("help-open-narrow") && !host.Bindings.Get<bool>("help-open-wide"),
            where + ": the criterion must answer cannot-share for the box it was actually given");
        bool wide = edge.Viewports.TryGetValue(WideId, out Rect help);
        bool band = edge.Viewports.TryGetValue(NarrowId, out Rect bandRect);
        Assert(wide != band,
            where + ": exactly one presentation may be arranged (wide=" + wide + " band=" + band + ")");
        Assert(!wide,
            where + ": the wide column must not be arranged into a box that cannot afford it");
        Assert(bandRect.xMax <= PreResizePageBox.x + 0.5f && bandRect.yMax <= PreResizePageBox.y + 0.5f,
            where + ": the help must stay inside the page, got " + Fmt(bandRect));
        Assert(reports.Count == 0,
            where + ": the pre-resize pass must be fit-silent, got " + Describe(reports));

        Console.WriteLine("[edge] " + language + " box " + PreResizePageBox.x + ": band=" + Fmt1(bandRect.height)
            + " inBounds=" + (bandRect.xMax <= PreResizePageBox.x + 0.5f)
            + " bandArranged=" + band + " fit=0");

        // The resized box on the SAME host: the presentation flips back to the sharing shape and the settings
        // viewport is arranged again.
        fedWidth = AcceptancePageBox.x;
        UiLayoutSnapshot resized = host.MeasureAndArrange(AcceptancePageBox);
        Assert(resized.RectById.ContainsKey("body-row")
            && resized.RectById.ContainsKey("nav-column")
            && resized.Viewports.ContainsKey(WideId)
            && !resized.Viewports.ContainsKey(NarrowId),
            where + ": the resized box must present the settings and the help together again");
        Assert(resized.Viewports["content-scroll"].width >= WindowChromeLayout.ResponsiveParameterRowBreakpoint - 0.5f,
            where + ": the settings viewport must be reachable once the window has resized, got "
            + resized.Viewports["content-scroll"].width);
    }

    /// <summary>
    /// The wide reference: 1920x1080, whose open page box is 1252. The retracted drawer arranges NEITHER
    /// presentation, the open one arranges exactly the wide column, the fit audit is silent, and closing
    /// retracts it again - no feed, so the no-window default path is exercised too.
    /// </summary>
    private static void AssertWideScreenShares(
        string language, Program.StubMetrics metrics, List<UiOverflowReport> reports)
    {
        Verse.UI.screenWidth = 1920;
        Verse.UI.screenHeight = 1080;
        string where = "at 1920x1080 (" + language + ")";

        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Overview");

        UiLayoutSnapshot retracted = host.MeasureAndArrange(WidePageBox);
        Assert(!retracted.Viewports.ContainsKey(WideId) && !retracted.Viewports.ContainsKey(NarrowId),
            where + ": the retracted drawer must arrange neither presentation");

        host.Bindings.Set("help-open", true);
        UiFitAudit.Reset();
        reports.Clear();
        host.MeasureAndArrange(WidePageBox);
        UiLayoutSnapshot expanded = host.MeasureAndArrange(WidePageBox);
        host.DrawChecked(new Rect(0f, 0f, WidePageBox.x, WidePageBox.y));
        Assert(expanded.Viewports.ContainsKey(WideId) && !expanded.Viewports.ContainsKey(NarrowId),
            where + ": the open drawer must take its third column");
        Assert(reports.Count == 0, where + ": the fit audit must report nothing, got " + Describe(reports));

        host.Bindings.Set("help-open", false);
        UiLayoutSnapshot closed = host.MeasureAndArrange(WidePageBox);
        Assert(!closed.Viewports.ContainsKey(WideId) && !closed.Viewports.ContainsKey(NarrowId),
            where + ": closing must retract the column");
    }

    /// <summary>
    /// STRESS-ONLY probe for the defensive band: a SYNTHETIC screen 800x600 (below the game's 1024x768
    /// minimum, UNREACHABLE) whose open policy box is 760. It proves only that the band, when it is the
    /// presentation, replaces the body and is handed at least the body's own measured content floor - the
    /// pre-fix reserved band failed exactly that. It decides no product behaviour.
    /// </summary>
    private static void AssertBandFallbackStress(
        string language, Program.StubMetrics metrics, List<UiOverflowReport> reports)
    {
        const float DocumentedContentFloor = 271f;
        float contentFloor;
        {
            // The ruler: the body's own content floor, measured in the configuration where the body is the
            // presentation (retracted at the acceptance box), on the same metrics and language.
            Verse.UI.screenWidth = MinimumScreenWidth;
            Verse.UI.screenHeight = MinimumScreenHeight;
            var rulerFake = new RecordingSettingsSource { RichData = true };
            using UiHost rulerHost = UsKernelSettingsHost.Create(rulerFake, metrics);
            rulerHost.Bindings.Invoke("set-tab", "Overview");
            UiLayoutSnapshot retracted = rulerHost.MeasureAndArrange(AcceptancePageBox);
            Assert(retracted.RectById.TryGetValue("nav-column", out Rect navColumn),
                "the ruler needs the nav column the content floor is measured on");
            contentFloor = navColumn.height;
        }

        Assert(contentFloor >= DocumentedContentFloor - 0.5f,
            "the measured content floor must still be the documented us/nav " + DocumentedContentFloor
            + "px, got " + contentFloor);

        // No feed: the default (policy open box at the synthetic screen) is 760, so this also covers the
        // no-window path.
        Verse.UI.screenWidth = 800;
        Verse.UI.screenHeight = 600;
        var page = PreResizePageBox;
        string where = "on the synthetic UNREACHABLE 800x600 probe (" + language + ")";

        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Overview");
        host.Bindings.Set("help-open", true);
        host.MeasureAndArrange(page);
        UiFitAudit.Reset();
        reports.Clear();
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(page);
        host.DrawChecked(new Rect(0f, 0f, page.x, page.y));

        Assert(snapshot.Viewports.TryGetValue(NarrowId, out Rect band),
            where + ": the band must be the presentation");
        Assert(snapshot.RectById.TryGetValue("help-panel-narrow", out Rect panel),
            where + ": the narrow panel must be arranged");
        Assert(!snapshot.RectById.ContainsKey("body-row")
            && !snapshot.Viewports.ContainsKey("content-scroll")
            && !snapshot.Viewports.ContainsKey(WideId),
            where + ": the band must REPLACE the body and its sub-tree, and the wide column must be absent");
        Assert(band.height >= panel.height - 0.5f && band.height >= contentFloor - 0.5f,
            where + ": the band must be handed at least the panel's measured content and the body's own"
            + " content floor: band=" + band.height + " panel=" + panel.height + " floor=" + contentFloor);
        Assert(snapshot.RectById.TryGetValue("footer-band", out Rect footer),
            where + ": the footer band must be arranged");
        Assert(footer.yMax <= page.y + 0.5f,
            where + ": the footer must stay inside the page, got " + footer.yMax);
        Assert(reports.Count == 0, where + ": the fit audit must report nothing, got " + Describe(reports));

        Console.WriteLine("[band-probe] " + language + " synthetic UNREACHABLE screen 800x600 box "
            + page.x + "x" + page.y + ": band=" + Fmt1(band.height) + " panel=" + Fmt1(panel.height)
            + " floor=" + Fmt1(contentFloor) + " fit=0");
    }

    private static string Describe(List<UiOverflowReport> reports)
    {
        var text = new System.Text.StringBuilder();
        text.Append(reports.Count).Append(" finding(s)");
        foreach (UiOverflowReport report in reports)
        {
            text.Append("; ").Append(report.ElementPath).Append(" ").Append(report.Axis)
                .Append(" needs ").Append(report.Needed).Append(" has ").Append(report.Available);
        }

        return text.ToString();
    }

    private static string Fmt(Rect rect)
    {
        return "(" + Fmt1(rect.x) + ", " + Fmt1(rect.y) + ", " + Fmt1(rect.width) + ", " + Fmt1(rect.height) + ")";
    }

    private static string Fmt1(float value)
    {
        return value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("HelpPresentationLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
