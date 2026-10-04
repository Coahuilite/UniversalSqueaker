using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// RPT1 lane: the Report button now answers. Before this slice a complete report could be written to the
/// layout log while the button said nothing at all - <c>UniversalSqueakerSettingsWindow.BeforeDraw</c> discarded
/// the generated pass that <c>UsKernelSettingsSource.ConsumeLayoutReportRequest</c> already returned, and the
/// only sentence in the card was capture state, which a click never moved.
///
/// <para>
/// WHAT THIS MEASURES, AND WITH WHICH INSTRUMENTS. (1) The sentence sits immediately below the button in the
/// real manifest, and every one of its sentences is an additive Keyed entry present in BOTH shipped tables.
/// (2) The production source - not a copy - names the real reason a request was refused, waits while an
/// accepted request is deferred, and prints the pass a report was ACTUALLY generated from: the success state
/// is asserted to be absent before <c>ConsumeLayoutReportRequest</c> returns a pass, and to carry a strictly
/// newer pass after a repeated click in the same window. (3) The line is per window and defaults off. (4) At
/// the ONE real 1024x768 page box (760x524, since BH1 retired the drawer-expanded width so help open and
/// help closed share it) in EN and ZH, the report band is scrolled into the viewport, the button's own drawn
/// band is clicked through the carrier's button-override seam, and the sentence the page prints is asserted
/// to have been DRAWN (metrics channel) with no fit finding on that row. The help switch sits in the footer
/// band beside the build/status composite in this configuration, so the report row is reached in a page whose
/// footer is that ROW.
/// </para>
///
/// <para>
/// MUTATION LEDGER (US AGENTS.md:62). PM executed two faithful reverts: removing the
/// production success assignment reddens "the success sentence must name the generated pass";
/// removing the shipped status widget reddens the adjacent-declaration step. Evidence is in the
/// PM RPT1 packet. These prove the outcome and declaration channels. The translation, refusal,
/// repeated-request, isolation and real-box clauses are GUARDS; their own mutations were not
/// executed. Real-box clicks use the Dev configuration because it has the report instrument.
/// </para>
///
/// <para>
/// NOT COVERED HERE, stated rather than implied: the "capture produced no dump" outcome
/// (<c>US.Diagnostics.Geometry.Report.NotProduced</c>) is implemented in the source but has no lane. It needs
/// a pending request whose completed pass carries no snapshot, a carrier state this harness cannot produce;
/// a faithful revert of that branch therefore has no assertion to redden. The instrument-unavailable reason
/// is config-dependent, and the config-aware helpers below read it from the carrier rather than hardcoding
/// it: a Dev carrier answers "capture off" (the Unavailable branch is unreachable there), and a Release
/// harness run answers "no instrument" for the same clauses. One run therefore cannot show both reasons, and
/// the Dev run - the one this lane is written against - exercises the capture-off reason only.
/// </para>
/// </summary>
internal static class ReportFeedbackLaneTests
{
    // The page box the shell hands the page on the game's minimum logical screen. BH1 (2026-10-05) retired
    // the drawer-expanded second width: the help panel reserves HEIGHT above the footer, so the open and the
    // closed help state are arranged in the SAME box, read out of the policy instead of being spelled.
    private const float MinimumScreenWidth = 1024f;
    private const float MinimumScreenHeight = 768f;

    /// <summary>The policy's page box for a screen: the window minus the side chrome, minus the title bar and
    /// the shell's own bottom inset. Since BH1 this one function answers BOTH help states.</summary>
    private static Vector2 PageBoxAt(float screenWidth, float screenHeight) => new Vector2(
        WindowChromeLayout.SettingsWindowWidth(screenWidth, screenHeight) - WindowChromeLayout.WindowChromeInset,
        WindowChromeLayout.SettingsWindowHeight(screenWidth, screenHeight)
        - WindowChromeLayout.TitleBarHeight - WindowChromeLayout.WindowChromeInset * 0.5f);

    private static readonly Vector2 PageBox = PageBoxAt(MinimumScreenWidth, MinimumScreenHeight);

    private const string Source = "coahuilite.universalsqueaker";

    private const string StatusWidgetId = "diagnostics-geometry-report-status";
    private const string ReportWidgetId = "diagnostics-geometry-report";
    private const string GeometryRowId = "diagnostics-geometry-row";

    /// <summary>The report outcome sentences: every one must resolve in BOTH shipped tables (RPT1.1's
    /// "existing translation mechanism", asserted rather than assumed).</summary>
    private static readonly string[] SentenceKeys =
    {
        "US.Diagnostics.Geometry.Report.Ready",
        "US.Diagnostics.Geometry.Report.CaptureOff",
        "US.Diagnostics.Geometry.Report.RefusedCaptureOff",
        "US.Diagnostics.Geometry.Report.Unavailable",
        "US.Diagnostics.Geometry.Report.NoScope",
        "US.Diagnostics.Geometry.Report.Waiting",
        "US.Diagnostics.Geometry.Report.Written",
        "US.Diagnostics.Geometry.Report.NotProduced",
    };

    /// <summary>The minimal page the production report lifecycle is driven on. It is deliberately NOT the real
    /// manifest: producing a report needs real draw passes, and the real page's bindings call
    /// <c>BuildView</c>, which needs RimWorld's def database - absent from the Verse stub. The real manifest is
    /// asserted separately (declaration + the real host's binding of the same getter).</summary>
    private const string ReportPageManifest =
        "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">"
        + "<Column Id=\"report-root\" Gap=\"4\" Padding=\"4\">"
        + "<Widget Id=\"report\" Kind=\"input/button\" Text=\"Report\" ActionBind=\"request-layout-report\""
        + " Width=\"96\" Height=\"24\" />"
        + "<Widget Id=\"" + StatusWidgetId + "\" Kind=\"text/wrapped\" Bind=\"layout-report-status\" />"
        + "</Column></UiPage>";

    public static int RunAll()
    {
        Step("the report status is declared immediately below the Report button", TheStatusIsDeclaredBesideTheButton);
        Step("every report sentence resolves in both shipped tables", TheSentencesResolveInBothTables);
        Step("the production line names the real reason and the generated pass", TheLineNamesTheRealReasonAndTheGeneratedPass);
        Step("the report line is per window and defaults off", TheLineIsPerWindowAndDefaultsOff);
        Step("the real 1024x768 page box keeps the status readable and the button reachable in both help"
            + " states (EN/ZH)", TheRealBoxesKeepTheStatusReadableAndTheButtonReachable);
        Console.WriteLine("ReportFeedbackLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // 1. The declaration: immediately below the button
    // ---------------------------------------------------------------------------------------------

    private static void TheStatusIsDeclaredBesideTheButton()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });

        UiElementSpec row = FindById(host.Manifest.Roots, GeometryRowId)
            ?? throw new InvalidOperationException("the shipped manifest must declare " + GeometryRowId);
        int reportIndex = -1;
        for (int i = 0; i < row.Children.Count; i++)
        {
            if (string.Equals(row.Children[i].Id, ReportWidgetId, StringComparison.Ordinal))
            {
                reportIndex = i;
                break;
            }
        }

        Assert(reportIndex >= 0, "the manifest must declare the Report button " + ReportWidgetId);
        UiElementSpec button = row.Children[reportIndex];
        Assert(button.Kind == "input/button"
            && button.TryGetAttribute("ActionBind", out string action) && action == "request-layout-report",
            "the Report button must stay the declared request command, got "
            + (button.TryGetAttribute("ActionBind", out string rawAction) ? rawAction : "(none)"));

        // RPT1.1 is a placement requirement, so it is asserted as ADJACENCY in the same column, not as
        // "somewhere in the card": a status line parked back above the switches is what the defect already was.
        Assert(reportIndex + 1 < row.Children.Count,
            "RPT1.1: the report status must be a sibling immediately BELOW the Report button, in the same column");
        UiElementSpec status = row.Children[reportIndex + 1];
        Assert(status.Id == StatusWidgetId,
            "the element immediately below the button must be the report status, got '" + status.Id + "'");
        Assert(status.Kind == "text/wrapped",
            "the report status must be the wrapping text atom, got " + status.Kind);
        Assert(status.TryGetAttribute("Bind", out string bind) && bind == "layout-report-status",
            "the report status must read the report line binding, got '"
            + (status.TryGetAttribute("Bind", out string rawBind) ? rawBind : "(none)") + "'");
        Assert(status.TryGetAttribute("HelpKey", out string help) && help == "us/diagnostics/layout-report",
            "the report status must explain the same action as the button, got '"
            + (status.TryGetAttribute("HelpKey", out string rawHelp) ? rawHelp : "(none)") + "'");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. The translation mechanism
    // ---------------------------------------------------------------------------------------------

    private static void TheSentencesResolveInBothTables()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            foreach (string key in SentenceKeys)
            {
                string text = table.TryGetValue(key, out string? resolved) ? resolved : "";
                Assert(!string.IsNullOrWhiteSpace(text),
                    language + ": the report sentence '" + key + "' must resolve (an additive Keyed entry)");
                Assert(!string.Equals(text, key, StringComparison.Ordinal),
                    language + ": '" + key + "' must carry a real sentence, not echo its key");
            }
        }

        // The pass number is the whole "a later click is distinguishable" mechanism, so the placeholder is
        // asserted in both languages: a translation that dropped it would print a success with no identity.
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            // net472: string.Contains(string, StringComparison) does not exist, so the ordinal probe is IndexOf.
            Assert(Program.ReadKeyedTable(language)["US.Diagnostics.Geometry.Report.Written"]
                    .IndexOf("{0}", StringComparison.Ordinal) >= 0,
                language + ": the success sentence must carry the generated pass as its format argument");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 3. The production line's real reason + generated pass
    // ---------------------------------------------------------------------------------------------

    private static void TheLineNamesTheRealReasonAndTheGeneratedPass()
    {
        Dictionary<string, string> table = Program.ReadKeyedTable("English");
        Program.SetTranslatorResolver(table);
        try
        {
            // (a) Through the REAL manifest host and its REAL binding, with no page draw: the binding the page
            // reads is the production source's own sentence, and a fresh window names the reason it cannot
            // report yet instead of a success. The full manifest cannot be drawn here (its bindings build the
            // view from RimWorld's def database, absent from the Verse stub), which is why the lifecycle below
            // runs on the minimal page - this clause is what keeps that page honest about the binding name.
            var boundSource = new UsKernelSettingsSource(new UniversalSqueakerSettings());
            using (UiHost boundHost = UsKernelSettingsHost.Create(boundSource, new Program.StubMetrics()))
            using (UsTextFitAudit boundScope = UsTextFitAudit.Open(boundHost, auditFit: false))
            {
                Assert(!boundScope.IsGeometryReportPending,
                    "opening a window's diagnosis scope must not request a report by itself");
                string bound = boundHost.Bindings.Get<string>("layout-report-status");
                Assert(bound == boundSource.LayoutReportStatus,
                    "the manifest binding 'layout-report-status' must be the production source's own sentence");
                Assert(bound == table[ScopeIdleReasonKey(boundHost)],
                    "a fresh window must name the REAL reason it cannot report yet ('"
                    + ScopeIdleReasonKey(boundHost) + "'), got '" + bound + "'");
            }

            // (b) The lifecycle, driven the way the window drives it: a real bound command, real passes, and
            // the same consume call BeforeDraw makes.
            var metrics = new DrawnTextMetrics();
            var source = new UsKernelSettingsSource(new UniversalSqueakerSettings());
            using UiHost host = CreateReportHost(source, metrics);
            var page = new Rect(0f, 0f, 320f, 240f);

            // No diagnosis scope: that IS the reason, and a click cannot change what is not there.
            Assert(source.LayoutReportStatus == table["US.Diagnostics.Geometry.Report.NoScope"],
                "a page with no diagnosis scope must name that reason, got '" + source.LayoutReportStatus + "'");
            host.Bindings.Invoke("request-layout-report");
            Assert(source.ConsumeLayoutReportRequest() < 0,
                "a request with no diagnosis scope can never produce a report");

            using UsTextFitAudit scope = UsTextFitAudit.Open(host, auditFit: false);
            Assert(source.LayoutReportStatus == table[ScopeIdleReasonKey(host)],
                "with a scope and capture off the line must name that reason, got '" + source.LayoutReportStatus + "'");

            // Default off, and a refused click NAMES the real reason rather than staying silent (RPT1.2).
            Assert(!source.LayoutCaptureOn, "this window's developer instrument must default off");
            host.Bindings.Invoke("request-layout-report");
            Assert(!scope.IsGeometryReportPending,
                "a request while capture is off must be refused, not left pending for a later enable");
            Assert(source.LayoutReportStatus == table[RefusalReasonKey(host)],
                "a refused click must name the real reason ('" + RefusalReasonKey(host) + "'), got '"
                + source.LayoutReportStatus + "'");
            Assert(source.ConsumeLayoutReportRequest() < 0,
                "a refused request must never be satisfied by a later enable");

#if US_DEV
            // Capture on: the line follows the instrument back to "ready" instead of keeping a stale refusal.
            host.Bindings.Set("layout-capture", true);
            Assert(source.LayoutCaptureOn, "the bound switch must enable this host's instrument");
            Assert(source.LayoutReportStatus == table["US.Diagnostics.Geometry.Report.Ready"],
                "with capture on and nothing requested the line must be ready, got '" + source.LayoutReportStatus + "'");

            // A real pass, so a capture exists before the click (a request is answered by a NEWER pass).
            host.MeasureAndArrange(page.size);
            host.DrawChecked(page);
            host.Bindings.Invoke("request-layout-report");
            Assert(scope.IsGeometryReportPending, "an accepted request must be pending after the click");
            Assert(source.LayoutReportStatus == table["US.Diagnostics.Geometry.Report.Waiting"],
                "the click must say the report is requested, not written");
            Assert(source.ConsumeLayoutReportRequest() < 0,
                "the click's own pass must not satisfy the request: a report from the pre-click capture"
                + " would describe the wrong frame");
            Assert(source.LayoutReportStatus == table["US.Diagnostics.Geometry.Report.Waiting"],
                "MUTATION PROOF - success must not appear before a REAL report: with no produced pass the line"
                + " must still read as waiting, got '" + source.LayoutReportStatus + "'");

            // The pass AFTER the request completes it, and the success sentence names THAT pass.
            host.MeasureAndArrange(page.size);
            host.DrawChecked(page);
            int first = source.ConsumeLayoutReportRequest();
            Assert(first >= 0, "the request must be produced from a pass completed after the click");
            host.Session.BumpContentRevision(); // the window's own post-report clock move
            string firstSentence = WrittenSentence(table, first);
            Assert(source.LayoutReportStatus == firstSentence,
                "the success sentence must name the generated pass, got '" + source.LayoutReportStatus + "'");
            metrics.Clear();
            host.MeasureAndArrange(page.size);
            host.DrawChecked(page);
            Assert(metrics.Seen(firstSentence),
                "the success sentence must be DRAWN, not only bound: '" + firstSentence + "'");

            // One request, one report: a further pass writes nothing and the sentence keeps its pass.
            host.MeasureAndArrange(page.size);
            host.DrawChecked(page);
            Assert(source.ConsumeLayoutReportRequest() < 0,
                "a further pass without a request must not produce a second report");
            Assert(source.LayoutReportStatus == firstSentence,
                "and the line must keep the pass it actually reported");

            // A later re-click is distinguishable, and its NEWEST result is the one on screen.
            host.Bindings.Invoke("request-layout-report");
            Assert(scope.IsGeometryReportPending, "a repeated request in the same window must be pending");
            Assert(source.LayoutReportStatus == table["US.Diagnostics.Geometry.Report.Waiting"],
                "the new click must visibly replace the previous success sentence");
            Assert(source.ConsumeLayoutReportRequest() < 0,
                "the repeated click must itself wait for a pass newer than the one that existed at the click");
            host.MeasureAndArrange(page.size);
            host.DrawChecked(page);
            int second = source.ConsumeLayoutReportRequest();
            Assert(second > first,
                "TheSecondReportDescribesANewerPass: the same window's repeated request must reach a newer pass"
                + " (first " + first + ", second " + second + ")");
            host.Session.BumpContentRevision();
            string secondSentence = WrittenSentence(table, second);
            Assert(secondSentence != firstSentence && source.LayoutReportStatus == secondSentence,
                "the newest result must be distinguishable from the previous one ('" + firstSentence
                + "' -> '" + secondSentence + "')");
#endif
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Per window, default off
    // ---------------------------------------------------------------------------------------------

    private static void TheLineIsPerWindowAndDefaultsOff()
    {
        Dictionary<string, string> table = Program.ReadKeyedTable("English");
        Program.SetTranslatorResolver(table);
        try
        {
            var firstSource = new UsKernelSettingsSource(new UniversalSqueakerSettings());
            var secondSource = new UsKernelSettingsSource(new UniversalSqueakerSettings());
            using UiHost first = CreateReportHost(firstSource, new Program.StubMetrics());
            using UiHost second = CreateReportHost(secondSource, new Program.StubMetrics());
            // The second window deliberately has NO diagnosis scope, so its own reason is the scope, not the
            // first window's capture switch.
            using UsTextFitAudit firstScope = UsTextFitAudit.Open(first, auditFit: false);
            var page = new Rect(0f, 0f, 320f, 240f);

            Assert(!firstSource.LayoutCaptureOn && !secondSource.LayoutCaptureOn,
                "each window's developer instrument must default off (RPT1.2)");
            Assert(firstSource.LayoutReportStatus == table[ScopeIdleReasonKey(first)],
                "the first window must name its own reason, got '" + firstSource.LayoutReportStatus + "'");
            Assert(secondSource.LayoutReportStatus == table["US.Diagnostics.Geometry.Report.NoScope"],
                "a sibling window with no diagnosis scope must say so, not inherit the first window's answer");
            Assert(firstSource.LayoutReportStatus != secondSource.LayoutReportStatus,
                "the report line must be per window, not one process-wide answer");

            first.Bindings.Invoke("request-layout-report");
            Assert(!firstScope.IsGeometryReportPending,
                "the first window's request while capture is off must be refused");
            Assert(firstSource.LayoutReportStatus == table[RefusalReasonKey(first)],
                "the first window's refusal must name its own reason, got '" + firstSource.LayoutReportStatus + "'");
            second.Bindings.Invoke("request-layout-report");
            Assert(secondSource.LayoutReportStatus == table["US.Diagnostics.Geometry.Report.NoScope"],
                "the second window's refusal must name ITS OWN reason (no scope)");

#if US_DEV
            first.Bindings.Set("layout-capture", true);
            Assert(firstSource.LayoutCaptureOn && !secondSource.LayoutCaptureOn,
                "enabling one window's capture must not move the other's");
            Assert(firstSource.LayoutReportStatus == table["US.Diagnostics.Geometry.Report.Ready"],
                "the first window's line must follow its own instrument to ready");
            Assert(secondSource.LayoutReportStatus == table["US.Diagnostics.Geometry.Report.NoScope"],
                "and the second window's line must not follow it");

            // A real pass before the click, so the request is answered by a strictly NEWER one.
            first.MeasureAndArrange(page.size);
            first.DrawChecked(page);
            first.Bindings.Invoke("request-layout-report");
            Assert(firstSource.ConsumeLayoutReportRequest() < 0,
                "the first window's accepted request must wait for a pass newer than the click");
            first.MeasureAndArrange(page.size);
            first.DrawChecked(page);
            int pass = firstSource.ConsumeLayoutReportRequest();
            Assert(pass >= 0, "the first window's own request must produce its own report");
            first.Session.BumpContentRevision();
            string written = WrittenSentence(table, pass);
            Assert(firstSource.LayoutReportStatus == written,
                "the first window must print its own generated pass, got '" + firstSource.LayoutReportStatus + "'");
            Assert(secondSource.LayoutReportStatus == table["US.Diagnostics.Geometry.Report.NoScope"],
                "the first window's written report must never appear in the second window's line");
            Assert(secondSource.ConsumeLayoutReportRequest() < 0,
                "the second window has no request of its own to consume");
#endif
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 5. The real page boxes
    // ---------------------------------------------------------------------------------------------

    private static void TheRealBoxesKeepTheStatusReadableAndTheButtonReachable()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            try
            {
                foreach ((string state, Vector2 box, bool helpOpen) in new[]
                         {
                             ("help open", PageBox, true),
                             ("help closed", PageBox, false),
                         })
                {
                    var metrics = new DrawnTextMetrics();
                    var reports = new List<UiOverflowReport>();
                    UiFitAudit.Attach(metrics, reports.Add);
                    UiFitAudit.Enabled = true;
                    try
                    {
                        string where = "RPT1 " + language + ", " + state + " at " + box.x + "x" + box.y;
                        var source = new RecordingSettingsSource { RichData = true };
                        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
                        source.AttachHost(host); // exactly what the production factory does for the real source
                        using UsTextFitAudit scope = UsTextFitAudit.Open(host, auditFit: false);
                        var page = new Rect(0f, 0f, box.x, box.y);

                        // The help state is INPUT here, not decoration: the open case really sets help-open, so
                        // the 760x524 box is the page WITH the bottom panel arranged and not a re-labelled
                        // closed one - and BH1 makes it the same box either way, because the toggle no longer
                        // changes the window's width.
                        host.Bindings.Set("help-open", helpOpen);
                        host.Bindings.Invoke("set-tab", "Overview");
                        host.MeasureAndArrange(box);
                        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
                        host.DrawChecked(page);

                        // BH1's cross-state clauses on this host before anything else is measured: the policy
                        // has ONE width function, so both help states are arranged in the same page box, and
                        // the panel's whole cost is the height it reserves. The caller's help state is restored.
                        AssertHelpPanelReservesHeightNotWidth(host, box, where);

                        UiLayoutSnapshot snapshot = host.MeasureAndArrange(box);
                        Assert(snapshot.Viewports.TryGetValue("content-scroll", out Rect viewport),
                            where + ": the centre column must publish its scroll viewport");
                        Assert(snapshot.ScrollContents.TryGetValue("content-scroll", out Rect content),
                            where + ": the centre column must publish its content box");
                        Assert(snapshot.Viewports.ContainsKey("help-scroll") == helpOpen,
                            where + ": the bottom help panel must be arranged exactly while help is open"
                            + " (the help-open input must really be set)");
                        if (helpOpen)
                        {
                            Assert(snapshot.RectById.ContainsKey("body-row")
                                    && snapshot.RectById.ContainsKey("nav-column"),
                                where + ": the open help panel must be arranged with the body row and the nav"
                                + " column still arranged - it takes height above the footer, not the settings");
                        }

                        Assert(!scope.IsGeometryReportPending,
                            where + ": merely drawing the page must not request a report");
                        Assert(snapshot.RectById.ContainsKey(ReportWidgetId),
                            where + ": the Report button must be arranged on the Overview page");
                        Assert(snapshot.RectById.ContainsKey(StatusWidgetId),
                            where + ": the report status must be arranged immediately below the button");

                        // BH1 moved the single help switch into the footer band, so the report row is reached
                        // beside that switch: the band is a ROW and the us/footer composite keeps exactly the
                        // leftover after the declared switch width and the band gap (602 at the shipped box).
                        UiElementSpec footerBandSpec = FindById(host.Manifest.Roots, "footer-band")
                            ?? throw new InvalidOperationException("the manifest must declare the footer band");
                        UiElementSpec toggleSpec = FindById(host.Manifest.Roots, "help-toggle")
                            ?? throw new InvalidOperationException("the manifest must declare the help switch");
                        Assert(string.Equals(footerBandSpec.Kind, "Row", StringComparison.Ordinal),
                            where + ": BH1 declares the footer band as a ROW");
                        Assert(snapshot.RectById.TryGetValue("footer-band", out Rect footerBand),
                            where + ": the footer band must be arranged");
                        Assert(snapshot.RectById.TryGetValue("footer", out Rect footerComposite),
                            where + ": the us/footer composite must be arranged");
                        Assert(snapshot.RectById.TryGetValue("help-toggle", out Rect toggleRect),
                            where + ": the help switch must be arranged inside the footer band");
                        Assert(Math.Abs(toggleRect.width - DeclaredFloat(toggleSpec, "Width")) <= 0.5f
                                && Math.Abs(footerComposite.width + DeclaredFloat(footerBandSpec, "Gap")
                                    + toggleRect.width - footerBand.width) <= 0.5f,
                            where + ": the band's width must be exactly the us/footer composite plus the gap and"
                            + " the declared-width switch - footer " + Describe(footerComposite) + ", switch "
                            + Describe(toggleRect) + ", band " + Describe(footerBand));
                        if (helpOpen)
                        {
                            Assert(snapshot.RectById.TryGetValue("help-scroll", out Rect helpReserved),
                                where + ": the open pass must arrange the bottom help panel");
                            Assert(helpReserved.yMax <= footerBand.y + 0.5f,
                                where + ": the open help panel must be arranged directly ABOVE the footer band"
                                + " that carries its own switch");
                        }

                        // RPT1.3's scrolling clause: the Overview content really overflows, so the button is
                        // reached by scrolling rather than by a box that happens to fit it. The in-game record
                        // for this page already showed a non-zero body scroll on the help-closed box.
                        Assert(content.height > viewport.height,
                            where + ": the Overview content must overflow its viewport, or the scrolling clause"
                            + " measures nothing (content " + content.height + ", viewport " + viewport.height + ")");

                        // Bring the button to the bottom edge of the viewport. The request is clamped to the
                        // content end by construction, so the scroll really happens and the band stays inside.
                        Rect bandBefore = ToContentLocal(
                            snapshot.RectById[ReportWidgetId], viewport);
                        Rect statusBefore = ToContentLocal(snapshot.RectById[StatusWidgetId], viewport);
                        float wanted = Math.Min(
                            Math.Max(0f, statusBefore.yMax - viewport.height + 8f),
                            Math.Max(0f, content.height - viewport.height));
                        Program.SetScrollPositionById(host.Session, "content-scroll", new Vector2(0f, wanted));
                        snapshot = host.MeasureAndArrange(box);
                        viewport = snapshot.Viewports["content-scroll"];
                        Vector2 scroll = Program.ScrollPositionById(host.Session, "content-scroll");
                        Rect buttonBand = ToContentLocal(snapshot.RectById[ReportWidgetId], viewport);
                        Rect statusBand = ToContentLocal(snapshot.RectById[StatusWidgetId], viewport);
                        Assert(buttonBand.y - scroll.y >= -0.5f && buttonBand.yMax - scroll.y <= viewport.height + 0.5f,
                            where + ": after scrolling to " + Num(scroll.y) + " the Report button must be inside the"
                            + " viewport (band " + Describe(buttonBand) + ", viewport " + Describe(viewport) + ")");
                        Assert(statusBand.y - scroll.y >= -0.5f && statusBand.yMax - scroll.y <= viewport.height + 0.5f,
                            where + ": the report answer must be visible beside its button after scrolling");

                        // Readability: the sentence the page prints is the sentence that was DRAWN, and the
                        // button's own band was really drawn at the arranged place (so it is clickable). The
                        // fit findings are read from THIS host's own ring: opening the window's diagnosis scope
                        // makes its subscription live, and a live subscription routes findings to that ring and
                        // measures with that host's ruler rather than the legacy process-wide sink (FL-20).
                        string statusText = host.Bindings.Get<string>("layout-report-status");
                        var seen = new List<Rect>();
                        UiFitAudit.Reset();
                        host.Diagnostics.Clear();
                        metrics.Clear();
                        DrawWithButtons(host, rect => { seen.Add(rect); return false; }, page);
                        Assert(seen.Exists(rect => RectMatches(rect, buttonBand)),
                            where + ": the Report button must really be DRAWN at its arranged band, or nothing"
                            + " there can be clicked (drawn button bands: " + DescribeAll(seen) + ")");
                        Assert(metrics.Seen(statusText),
                            where + ": the report status sentence must be DRAWN beside the button, not only bound: '"
                            + statusText + "'");
                        List<string> rowFindings = ReportRowFindings(host);
                        Assert(rowFindings.Count == 0,
                            where + ": the report button and its status must fit at the real box, got "
                            + (rowFindings.Count == 0 ? "(none)" : string.Join(" | ", rowFindings)));
                        Console.WriteLine("[rpt1-box] " + where + " help=" + (helpOpen ? "open" : "closed")
                            + " content=" + Num(content.height) + "/" + Num(viewport.height)
                            + " scroll=" + Num(scroll.y) + " status='" + statusText + "'"
                            + " fit=" + rowFindings.Count);

#if US_DEV
                        // A click that cannot produce a report still ANSWERS (RPT1.2): with capture off the
                        // sentence changes to the refusal wording instead of looking like a dead button.
                        Assert(statusText == table["US.Diagnostics.Geometry.Report.CaptureOff"],
                            where + ": the fixture must start with capture off, got '" + statusText + "'");
                        DrawWithButtons(host, rect => RectMatches(rect, buttonBand), page);
                        Assert(host.Bindings.Get<string>("layout-report-status")
                                == table["US.Diagnostics.Geometry.Report.RefusedCaptureOff"],
                            where + ": a refused Report click must name the real reason (capture off), got '"
                            + host.Bindings.Get<string>("layout-report-status") + "'");

                        // Capture on through the page's own switch, so the click below can really succeed.
                        host.Bindings.Set("layout-capture", true);
                        Assert(source.LayoutCaptureOn, where + ": the page's switch must enable this host's instrument");
                        Assert(host.Bindings.Get<string>("layout-report-status")
                                == table["US.Diagnostics.Geometry.Report.Ready"],
                            where + ": with capture on and nothing requested the line must be ready, got '"
                            + host.Bindings.Get<string>("layout-report-status") + "'");
                        host.MeasureAndArrange(box);
                        host.DrawChecked(page); // a real pass, so a capture exists before the click
                        snapshot = host.MeasureAndArrange(box);
                        buttonBand = ToContentLocal(
                            snapshot.RectById[ReportWidgetId], snapshot.Viewports["content-scroll"]);

                        int requests = source.LayoutReportRequests;
                        DrawWithButtons(host, rect => RectMatches(rect, buttonBand), page);
                        Assert(source.LayoutReportRequests == requests + 1,
                            where + ": clicking the DRAWN Report band must reach the real request binding");
                        host.MeasureAndArrange(box);
                        host.DrawChecked(page);
                        Assert(host.Bindings.Get<string>("layout-report-status")
                                == table["US.Diagnostics.Geometry.Report.Waiting"],
                            where + ": the click must say a report is requested and not yet written, got '"
                            + host.Bindings.Get<string>("layout-report-status") + "'");

                        Assert(source.ConsumeLayoutReportRequest() < 0,
                            where + ": the click must wait for a pass newer than the one that existed when it was"
                            + " clicked, so a report from the pre-click capture cannot satisfy it");
                        host.MeasureAndArrange(box);
                        host.DrawChecked(page); // the pass the request waits on
                        int pass = source.ConsumeLayoutReportRequest();
                        Assert(pass >= 0, where + ": the requested report must be produced from a pass after the click");
                        host.Session.BumpContentRevision(); // the window's own post-report clock move
                        string written = WrittenSentence(table, pass);
                        metrics.Clear();
                        UiFitAudit.Reset();
                        host.Diagnostics.Clear();
                        host.MeasureAndArrange(box);
                        host.DrawChecked(page);
                        Assert(host.Bindings.Get<string>("layout-report-status") == written,
                            where + ": the success sentence must name the generated pass, got '"
                            + host.Bindings.Get<string>("layout-report-status") + "'");
                        Assert(metrics.Seen(written),
                            where + ": the success sentence must be DRAWN at the real box, not only bound: '"
                            + written + "'");
#endif
                    }
                    finally
                    {
                        UiFitAudit.Detach();
                        UiFitAudit.Enabled = false;
                    }
                }
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The minimal drawable page plus the three REAL binding kinds the manifest uses. The report-status
    /// binding is declared with the SAME key and the SAME getter the production host registers
    /// (<c>UsKernelSettingsHost</c>); the real-manifest clause in step 3 asserts the production host's own
    /// binding of that getter, so this page cannot quietly measure a different surface.
    /// </summary>
    private static UiHost CreateReportHost(UsKernelSettingsSource source, ITextMetrics metrics)
    {
        var bindings = new UiBindings();
        bindings.BindValue<bool>("layout-capture", () => source.LayoutCaptureOn, source.SetLayoutCapture);
        bindings.BindCommand("request-layout-report", source.RequestLayoutReport);
        bindings.BindReadOnly<string>("layout-report-status", () => source.LayoutReportStatus);
        UsKernelWidgetRegistrar.EnsureRegistered();
        UiLayoutManifest manifest = UiLayoutManifest.Parse(ReportPageManifest);
        UiHost host = new(Source, manifest, bindings, UsTheme.Surface(), metrics, new KeyEchoTranslation());
        source.AttachHost(host);
        return host;
    }

    /// <summary>The idle reason when this host HAS a diagnosis scope: capture off, or a carrier without the
    /// instrument. A host without a scope answers <c>NoScope</c> instead, which callers assert directly.</summary>
    private static string ScopeIdleReasonKey(UiHost host)
    {
        return InstrumentUnavailable(host)
            ? "US.Diagnostics.Geometry.Report.Unavailable"
            : "US.Diagnostics.Geometry.Report.CaptureOff";
    }

    /// <summary>The sentence a REFUSED click leaves: the capture-off reason has a distinct "no report written"
    /// wording, so the click is visible even though it produced nothing. An unavailable instrument cannot be
    /// changed by the developer, so its refusal keeps the plain reason.</summary>
    private static string RefusalReasonKey(UiHost host)
    {
        return InstrumentUnavailable(host)
            ? "US.Diagnostics.Geometry.Report.Unavailable"
            : "US.Diagnostics.Geometry.Report.RefusedCaptureOff";
    }

    private static bool InstrumentUnavailable(UiHost host)
    {
        return UsTextFitAudit.GetDevGeometryStatus(host) == UsTextFitAudit.DevGeometryStatus.Unavailable;
    }

    private static string WrittenSentence(Dictionary<string, string> table, int pass)
    {
        return string.Format(
            CultureInfo.InvariantCulture, table["US.Diagnostics.Geometry.Report.Written"], pass);
    }

    /// <summary>
    /// The overflow findings this host's OWN subscription ring carries for the report row. With the window's
    /// diagnosis scope open the subscription is live, so the legacy process-wide sink is deliberately not
    /// consulted (FL-20's routing rule): reading it would report zero for every real box and turn the fit
    /// clause into an assertion that cannot fail.
    /// </summary>
    private static List<string> ReportRowFindings(UiHost host)
    {
        var found = new List<string>();
        foreach (UiDiagnosticEvent candidate in host.Diagnostics.Snapshot())
        {
            if (candidate.Kind != UiDiagnosticKind.Fit || !candidate.Overflow.HasValue) continue;
            UiOverflowReport report = candidate.Overflow.Value;
            if (report.ElementPath.IndexOf("diagnostics-geometry-report", StringComparison.Ordinal) < 0) continue;
            found.Add(report.ElementPath + " " + report.Axis + " needs " + Num(report.Needed)
                + " has " + Num(report.Available));
        }

        return found;
    }

    /// <summary>Native button rectangles use content-local coordinates. The GUI scroll transform applies
    /// the scroll position; visibility therefore compares this rectangle minus the scroll with the viewport.</summary>
    private static Rect ToContentLocal(Rect pageRect, Rect contentViewport)
    {
        return new Rect(
            pageRect.x - contentViewport.x,
            pageRect.y - contentViewport.y,
            pageRect.width,
            pageRect.height);
    }

    /// <summary>Draws one checked pass with the carrier's button seam installed, so a caller can either record
    /// the drawn button bands or activate the one it names (the same seam DeclarativeDiagnosticsLaneTests uses).</summary>
    private static void DrawWithButtons(UiHost host, Func<Rect, bool> click, Rect page)
    {
        FieldInfo? field = typeof(UiNative).GetField("ButtonOverride",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert(field != null && field.FieldType == typeof(Func<Rect, bool>),
            "REFLECTION BLOCKER: UiNative.ButtonOverride is not the expected seam on net472");
        try
        {
            field!.SetValue(null, click);
            host.DrawChecked(page);
        }
        finally
        {
            field!.SetValue(null, null);
        }
    }

    private static bool RectMatches(Rect a, Rect b)
    {
        return Math.Abs(a.x - b.x) <= 0.01f && Math.Abs(a.y - b.y) <= 0.01f
            && Math.Abs(a.width - b.width) <= 0.01f && Math.Abs(a.height - b.height) <= 0.01f;
    }

    private static string Describe(Rect r)
    {
        return "(" + Num(r.x) + "," + Num(r.y) + " " + Num(r.width) + "x" + Num(r.height) + ")";
    }

    private static string DescribeAll(List<Rect> rects)
    {
        return rects.Count == 0 ? "(none)" : string.Join(" ", rects.ConvertAll(r => Describe(r)));
    }

    private static string Num(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// BH1's cross-state clause, asserted on the host the caller is measuring: the window policy has ONE width
    /// function and the help state is not an input to it, so both help states must be arranged in the SAME
    /// page box; the bottom panel must appear exactly in the open pass; the open pass must still arrange the
    /// body row, the nav column and the centre scroll; and closing the panel must hand its WHOLE reservation
    /// (its declared height plus the page gap it no longer pays) back to the body. Numbers come from the
    /// manifest and the policy, never from a re-declaration here. The caller's help state is restored.
    /// </summary>
    private static void AssertHelpPanelReservesHeightNotWidth(UiHost host, Vector2 box, string where)
    {
        UiElementSpec pageRoot = host.Manifest.Roots[0];
        Assert(string.Equals(pageRoot.Id, "page-root", StringComparison.Ordinal),
            where + ": the page's band column must be 'page-root', got '" + pageRoot.Id + "'");
        UiElementSpec panel = FindById(host.Manifest.Roots, "help-scroll")
            ?? throw new InvalidOperationException("the manifest must declare the bottom help panel 'help-scroll'");
        float panelHeight = DeclaredFloat(panel, "Height");
        float pageGap = DeclaredFloat(pageRoot, "Gap");
        float pagePadding = DeclaredFloat(pageRoot, "Padding");
        float policyPageWidth = WindowChromeLayout.SettingsWindowWidth(MinimumScreenWidth, MinimumScreenHeight)
            - WindowChromeLayout.WindowChromeInset;
        bool state = host.Bindings.Get<bool>("help-open");

        host.Bindings.Set("help-open", true);
        UiLayoutSnapshot open = host.MeasureAndArrange(box);
        host.Bindings.Set("help-open", false);
        UiLayoutSnapshot closed = host.MeasureAndArrange(box);
        host.Bindings.Set("help-open", state);

        Assert(open.RectById.TryGetValue("page-root", out Rect openPage),
            where + ": the open pass must arrange the page root");
        Assert(closed.RectById.TryGetValue("page-root", out Rect closedPage),
            where + ": the closed pass must arrange the page root");
        Assert(Math.Abs(openPage.width - policyPageWidth) <= 0.5f
                && Math.Abs(closedPage.width - policyPageWidth) <= 0.5f
                && Math.Abs(openPage.x - closedPage.x) <= 0.5f
                && Math.Abs(openPage.y - closedPage.y) <= 0.5f
                && Math.Abs(openPage.width - closedPage.width) <= 0.5f
                && Math.Abs(openPage.height - closedPage.height) <= 0.5f,
            where + ": BH1 leaves BOTH help states the same policy page box " + Num(policyPageWidth) + "x"
            + Num(box.y) + " - open " + Describe(openPage) + ", closed " + Describe(closedPage));

        Assert(open.Viewports.TryGetValue("help-scroll", out Rect helpRect),
            where + ": the open pass must arrange the bottom help panel");
        Assert(!closed.Viewports.ContainsKey("help-scroll"),
            where + ": the closed pass must not arrange the bottom help panel");
        Assert(Math.Abs(helpRect.height - panelHeight) <= 0.5f
                && Math.Abs(helpRect.width - (openPage.width - pagePadding * 2f)) <= 0.5f,
            where + ": the panel must hold its declared reservation across the page's inner width, got "
            + Describe(helpRect) + " (declared Height " + Num(panelHeight) + ", inner width "
            + Num(openPage.width - pagePadding * 2f) + ")");

        Assert(open.RectById.TryGetValue("body-row", out Rect openBody),
            where + ": the open pass must arrange the body row");
        Assert(closed.RectById.TryGetValue("body-row", out Rect closedBody),
            where + ": the closed pass must arrange the body row");
        Assert(open.RectById.ContainsKey("nav-column") && open.Viewports.ContainsKey("content-scroll")
                && closed.RectById.ContainsKey("nav-column") && closed.Viewports.ContainsKey("content-scroll"),
            where + ": the body row, the nav column and the centre scroll must stay arranged in BOTH help states");
        Assert(Math.Abs(closedBody.height - (openBody.height + panelHeight + pageGap)) <= 0.5f,
            where + ": closing the panel must hand its whole reservation back to the body row (open "
            + Num(openBody.height) + " + panel " + Num(panelHeight) + " + gap " + Num(pageGap) + " vs closed "
            + Num(closedBody.height) + ")");
        Assert(Math.Abs(openBody.width - closedBody.width) <= 0.01f
                && Math.Abs(open.Viewports["content-scroll"].width
                    - closed.Viewports["content-scroll"].width) <= 0.01f,
            where + ": the help panel may cost the settings no width at all (body " + Num(openBody.width)
            + " vs " + Num(closedBody.width) + ", centre " + Num(open.Viewports["content-scroll"].width)
            + " vs " + Num(closed.Viewports["content-scroll"].width) + ")");
    }

    /// <summary>One declared numeric attribute, so a lane never re-spells a manifest number.</summary>
    private static float DeclaredFloat(UiElementSpec spec, string attribute)
    {
        if (!spec.TryGetAttribute(attribute, out string raw))
        {
            throw new InvalidOperationException("'" + spec.Id + "' must declare " + attribute);
        }

        return float.Parse(raw.Trim(), CultureInfo.InvariantCulture);
    }

    private static UiElementSpec? FindById(IReadOnlyList<UiElementSpec> elements, string id)
    {
        foreach (UiElementSpec element in elements)
        {
            if (string.Equals(element.Id, id, StringComparison.Ordinal)) return element;
            UiElementSpec? nested = FindById(element.Children, id);
            if (nested != null) return nested;
        }

        return null;
    }

    /// <summary>Every text measurement of one metric channel, so "the sentence was drawn" is asserted against
    /// the outlet the page really painted through rather than against a re-computed string.</summary>
    private sealed class DrawnTextMetrics : ITextMetrics
    {
        private readonly Program.StubMetrics inner = new();
        private readonly HashSet<string> seen = new(StringComparer.Ordinal);

        public bool Seen(string text) => seen.Contains(text);

        public void Clear() => seen.Clear();

        public float MeasureText(string text, UiFont font, float width)
        {
            seen.Add(text);
            return inner.MeasureText(text, font, width);
        }

        public float MeasureWidth(string text, UiFont font)
        {
            seen.Add(text);
            return inner.MeasureWidth(text, font);
        }
    }

    /// <summary>The minimal page's own translation seam: this lane asserts the source's sentences through the
    /// Verse translator resolver, so the host's manifest-text seam must not hide them behind a second language.</summary>
    private sealed class KeyEchoTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ReportFeedbackLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
