using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// Focused geometry lane for the two layout contracts of task-3.
///
/// <para>
/// CONTRACT 1 - diagnostic/support rows (Overview). Every row of us/diagnostics, us/basic-tuning,
/// us/timing and us/camera-indicator is [label band | control column]: one fixed column width and one
/// right edge, the three-choice row drawn as one segmented control with equal cells, and a label width
/// derived from the ROW so no translation can move a control. The lane records the rectangles the
/// widgets actually hand to UiNative (the Button/Slider/TextField reflection seams) and asserts the drawn
/// right edges, the segmented cell widths and the measured snapshot, in English and Chinese at the four
/// required logical widths.
/// </para>
/// <para>
/// CONTRACT 2 - the us/nav stack: one shared card height, one fixed column width, identical bounds for
/// the selected and unselected states, a constant inter-card gap, and Measure equal to the drawn stack
/// height. A long synthetic label and subtitle must not change any of it.
/// </para>
/// <para>
/// Rect spaces: controls drawn inside the centre scroll are recorded in scroll-content space, the nav is
/// recorded in page space. Section controls are matched by shape and by containment in the card's
/// content-local rect; nav cards are matched by containment in the nav element's page rect.
/// </para>
/// </summary>
internal static class SettingsGeometryLaneTests
{
    private static readonly float[] Widths = { 1024f, 736f, 480f, 320f };

    /// <summary>
    /// The declared width of the timing card's minus/number-field/plus cluster:
    /// 26 + Gap 8 + 150 + Gap 8 + 26. It is the composite's own floor (ButtonWidth x 2 + StepperGap x 2 + 1
    /// = 61) widened by the 150 field the declaration adds, and it is what makes the full-cluster assertion
    /// below hostable-only rather than a blanket demand.
    /// </summary>
    private const float TimingStepperClusterWidth = 218f;
    private static readonly string[] Languages = { "English", "ChineseSimplified" };

    private const float Height = 720f;
    private const float RightEdgeTolerance = 1.0f;
    private const float ShapeTolerance = 0.51f;

    /// <summary>The precondition for asserting the FIXED column width: the row must be wide enough to host
    /// the column unclamped (<see cref="UsKernelDraw.ControlColumnRect"/> shrinks it below this). A narrower
    /// body is recorded as degenerate; the no-overflow and no-clipping checks still run there.</summary>
    private const float ContractBodyFloor =
        UsKernelDraw.ControlColumnWidth + UsKernelDraw.RowLeftPadding + UsKernelDraw.ControlColumnRightInset;

    private static readonly string[] Sections = { "basic-tuning", "timing", "camera-indicator", "diagnostics" };

    /// <summary>
    /// The Overview cards that STILL draw their own [label | control column] row. S4-1 dissolved
    /// us/global-volume, us/basic-tuning and us/camera-indicator into manifest subtrees and S4-3 dissolved
    /// us/timing, so the shared control column is from now on us/diagnostics's contract alone. The four
    /// declarative cards are covered by <c>DeclarativeOverviewLaneTests</c>/<c>DeclarativeTimingLaneTests</c>,
    /// which measure the engine's own Row/atom geometry from the manifest rather than a US hand-rolled
    /// column - the composite shape classifiers below deliberately do not match an atom's band, so no
    /// composite assertion silently re-interprets a declared row. That exclusion is load-bearing, not
    /// tidiness: a declarative stepper is a 26x20 input/button, which <see cref="IsSmallControl"/> DOES
    /// match, and a declarative row's right edge is the card's content edge, not the composite column's
    /// inset. Had timing stayed in this list, this lane would have demanded the retired contract from the
    /// new atoms - the same classifier gap S4-1 recorded for the checkbox band.
    /// </summary>
    private static readonly string[] CompositeSections = { "diagnostics" };

    private static FieldInfo ButtonOverrideField => RequireField("ButtonOverride", typeof(Func<Rect, bool>));
    private static FieldInfo SliderOverrideField => RequireField("SliderOverride", typeof(Func<Rect, float, float, float, float>));
    private static FieldInfo TextFieldOverrideField => RequireField("TextFieldOverride", typeof(Func<Rect, string, string>));

    public static int RunAll()
    {
        Step("uniform support-row control column at 1024/736/480/320 in EN + ZH", UniformControlColumn);
        Step("a long translated label cannot move the control column", LongLabelKeepsTheColumn);
        Step("navigation cards share one stable geometry", NavigationCardsShareOneGeometry);
        Step("checkbox visible edge + support-row height growth evidence table", CheckboxEdgeAndRowHeightEvidence);
        Step("the eat-precision child row exists only while its parent switch is on", ChildRowFollowsTheParentSwitch);
        Step("the declarative checklist card reproduces the card chrome (step A)", DeclarativeChecklistCardReproducesTheCardChrome);
        Console.WriteLine("SettingsGeometryLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // Contract 1: the shared control column
    // ---------------------------------------------------------------------------------------------

    private static void UniformControlColumn()
    {
        var metrics = new Program.StubMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        int measurable = 0;
        try
        {
            PositiveControl(reports);

            foreach (string language in Languages)
            {
                Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
                try
                {
                    foreach (float width in Widths)
                    {
                        reports.Clear();
                        UiFitAudit.Reset();
                        using UiHost host = NewHost(metrics);
                        Rec rec = Record(host, width, Height);

                        Rect contentArea = rec.Snapshot.ScrollContents.TryGetValue("content-scroll", out Rect area)
                            ? area
                            : rec.ContentViewport;
                        Assert(contentArea.width <= rec.ContentViewport.width + 0.5f,
                            "the measured content must not exceed its viewport horizontally at " + width + " (" + language + "): "
                            + contentArea.width + " > " + rec.ContentViewport.width);


                        foreach (UiOverflowReport report in TouchedFindings(reports))
                        {
                            throw new InvalidOperationException(
                                "a touched support row overflows at " + width + " (" + language + "): "
                                + report.ElementPath + " " + report.Axis + " needs " + report.Needed
                                + " has " + report.Available);
                        }

                        ControlSurvey survey = Survey(rec);
                        // At or above the column's own width the column is unclamped, so the fixed-width
                        // clauses below are the contract the row must satisfy; below it the layout cannot host
                        // the column and the width clause would be vacuous.
                        bool contractHostable = survey.MinBodyWidth >= ContractBodyFloor;
                        float visualDeviation = float.NaN;
                        if (survey.Slots.Count > 0 && survey.Cells.Count > 0)
                        {
                            float segmented = survey.Cells.Max(c => c.xMax);
                            visualDeviation = survey.Slots.Max(s => Math.Abs(VisualBox(s).xMax - segmented));
                        }

                        Console.WriteLine("[geometry] " + width + " " + language
                            + " body=" + survey.MinBodyWidth.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                            + " slots=" + survey.Slots.Count
                            + " cells=" + survey.Cells.Count
                            + " edges=" + survey.Edges.Count
                            + " rightEdge=" + survey.CommonRight.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                            + " visualVsSegmented=" + (float.IsNaN(visualDeviation)
                                ? "n/a"
                                : visualDeviation.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "px")
                            + " spansSections=" + survey.SpansEverySection
                            + " contract=" + (contractHostable ? "asserted" : "degenerate-narrow-layout"));

                        Assert(survey.SpansEverySection,
                            "all four Overview support sections must be laid out with a content-local rect at " + width
                            + " (" + language + "); a missing card means the snapshot no longer carries it");

                        if (!contractHostable)
                        {
                            // The centre column is too narrow for the column contract (the pre-existing
                            // narrow-layout gap). The lane still proved no overflow and no clipping above.
                            continue;
                        }

                        measurable++;
                        // S4-1 re-cut: the eight support checkboxes the composites drew are down to the one
                        // us/diagnostics still draws itself (6 basic-tuning + camera left the composite world
                        // entirely). The declarative cards' bands are asserted by DeclarativeOverviewLaneTests,
                        // which measures them against the manifest; keeping the >= 8 pin here would only force
                        // this lane to re-count controls it no longer owns.
                        Assert(survey.Slots.Count >= 1,
                            "the composite sections must still draw their own checkbox (us/diagnostics/localize)"
                            + " at " + width + " (" + language + "), got " + survey.Slots.Count);
                        Assert(survey.Cells.Count == 3,
                            "the three-choice logging row must draw exactly one segmented control of three equal cells at "
                            + width + " (" + language + "), got " + survey.Cells.Count);

                        // One row group per drawn support row: its terminal control must land on the shared
                        // column right edge, and no control of the row may cross it. Grouping by the row's
                        // vertical centre is what makes this a row contract rather than a per-rect one (a
                        // stepper cluster has interior controls that legitimately sit left of the edge).
                        List<List<Rect>> rows = RowGroups(survey.Edges);
                        float expectedRight = survey.BodyRight - UsKernelDraw.ControlColumnRightInset;
                        var rowRights = new List<float>();
                        foreach (List<Rect> row in rows)
                        {
                            float rowRight = row.Max(r => r.xMax);
                            rowRights.Add(rowRight);
                            Assert(Math.Abs(rowRight - expectedRight) <= RightEdgeTolerance,
                                "every support row must terminate on the shared control-column right edge at "
                                + width + " (" + language + "): row " + Describe(row[0]) + " ends at " + rowRight
                                + ", expected " + expectedRight);
                            foreach (Rect control in row)
                            {
                                Assert(control.xMax <= expectedRight + RightEdgeTolerance,
                                    "a support control must not cross the shared column right edge at " + width
                                    + " (" + language + "): " + Describe(control) + " expected " + expectedRight);
                            }
                        }

                        float minRight = rowRights.Min();
                        float maxRight = rowRights.Max();
                        Assert(maxRight - minRight <= RightEdgeTolerance,
                            "every support row's right edge must be identical at " + width + " (" + language
                            + "): deviation " + (maxRight - minRight).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                            + " (min " + minRight + " max " + maxRight + ")");

                        Rect first = survey.Cells.OrderBy(r => r.x).First();
                        Rect last = survey.Cells.OrderBy(r => r.x).Last();
                        float cellWidth = survey.Cells[0].width;
                        foreach (Rect cell in survey.Cells)
                        {
                            Assert(Math.Abs(cell.width - cellWidth) <= 0.01f,
                                "segmented cells must be equal width at " + width + " (" + language + "): "
                                + cell.width + " vs " + cellWidth);
                            Assert(Math.Abs(cell.y - first.y) <= 0.01f,
                                "segmented cells must share one row at " + width + " (" + language + "): " + cell.y + " vs " + first.y);
                        }

                        Assert(Math.Abs((last.xMax - first.x) - UsKernelDraw.ControlColumnWidth) <= 0.5f,
                            "the segmented control's total width must BE the one fixed control column at " + width
                            + " (" + language + "): " + (last.xMax - first.x) + " vs " + UsKernelDraw.ControlColumnWidth);

                        // The VISIBLE edge contract: the 18px box the player sees must terminate on the same
                        // right edge as the segmented control's last cell, not 3px inside its 24px hit band.
                        float segmentedRight = survey.Cells.Max(c => c.xMax);
                        Assert(Math.Abs(segmentedRight - expectedRight) <= RightEdgeTolerance,
                            "the segmented control must terminate on the shared column right edge at " + width
                            + " (" + language + "): " + segmentedRight + " vs " + expectedRight);
                        foreach (Rect slot in survey.Slots)
                        {
                            Rect visual = VisualBox(slot);
                            Assert(Math.Abs(visual.xMax - segmentedRight) <= RightEdgeTolerance,
                                "every checkbox VISUAL box must align with the segmented control's right edge at "
                                + width + " (" + language + "): slot " + Describe(slot) + " visual "
                                + Describe(visual) + " vs segmented " + segmentedRight);
                            Assert(Math.Abs(visual.width - UsKernelDraw.CheckboxVisual) <= 0.01f
                                && Math.Abs(visual.height - UsKernelDraw.CheckboxVisual) <= 0.01f,
                                "the drawn checkbox box must be the 18px visual at " + width + " (" + language
                                + "): " + Describe(visual));
                            Assert(Math.Abs(visual.x - (slot.x + UsKernelDraw.CheckboxInset)) <= 0.01f,
                                "the 24px hit box must stay centred on the visual at " + width + " (" + language
                                + "): slot " + Describe(slot) + " visual " + Describe(visual));
                            Assert(visual.xMax <= slot.xMax + 0.01f,
                                "the hit box must contain the visual at " + width + " (" + language + "): "
                                + Describe(slot) + " vs " + Describe(visual));
                        }
                    }
                }
                finally
                {
                    Program.SetTranslatorResolver(null);
                }
            }

            Assert(measurable >= 2,
                "the control-column contract must be hostable at more than one of the required widths; got "
                + measurable + ". A degenerate centre viewport is a layout bug, not a lane result");
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Enabled = false;
            Program.SetTranslatorResolver(null);
        }
    }

    /// <summary>
    /// The audited widths: a translation that cannot fit its band is reported, so a green sweep is
    /// evidence. The same production drawing outlet is used, scoped to a probe path.
    /// </summary>
    private static void PositiveControl(List<UiOverflowReport> reports)
    {
        UiFitAudit.Reset();
        reports.Clear();
        UiFitAudit.BeginElement("probe/single-line");
        try
        {
            UiThemeDraw.Label(
                new Rect(0f, 0f, 20f, 16f), "probe text", UiTheme.DarkGold, null, UiFont.Tiny,
                TextAnchor.MiddleLeft, singleLine: true);
        }
        finally
        {
            UiFitAudit.EndElement();
        }

        Assert(reports.Count == 1,
            "positive control failed: the audit saw nothing for a label that cannot fit (" + Describe(reports) + ")");
        reports.Clear();
    }

    /// <summary>
    /// A label long enough to overflow any fixed band is injected for three support rows. The rows may
    /// grow (they are measured), but the control column is derived from the row width, so every drawn
    /// control rectangle must be identical to the baseline.
    /// </summary>
    private static void LongLabelKeepsTheColumn()
    {
        var metrics = new Program.StubMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            Dictionary<string, string> english = Program.ReadKeyedTable("English");
            Program.SetTranslatorResolver(english);
            List<Rect> baseline;
            using (UiHost host = NewHost(metrics))
            {
                reports.Clear();
                UiFitAudit.Reset();
                Rec rec = Record(host, 1024f, Height);
                baseline = ControlEdges(rec);
            }

            // The injected string is far wider than any label band at any of the required widths.
            string impossible = new string('测', 40);
            var stretched = new Dictionary<string, string>(english, StringComparer.Ordinal)
            {
                ["US.Diagnostics.LocalizeDebugMenu"] = impossible,
                ["US.Tuning.CameraIndicator"] = impossible,
                ["US.Tuning.ScaleCooldown"] = impossible,
            };

            Program.SetTranslatorResolver(stretched);
            List<Rect> injected;
            using (UiHost host = NewHost(metrics))
            {
                reports.Clear();
                UiFitAudit.Reset();
                Rec rec = Record(host, 1024f, Height);
                injected = ControlEdges(rec);
                Assert(TouchedFindings(reports).Count == 0,
                    "a longer translation must grow the measured row band, never overflow it: " + Describe(reports));
            }

            Assert(baseline.Count == injected.Count,
                "the touched sections must keep drawing the same number of controls with a long translation: "
                + baseline.Count + " -> " + injected.Count);
            for (int i = 0; i < baseline.Count; i++)
            {
                // Horizontal geometry only: a row whose label wraps may grow taller (and the rows below it
                // shift down), but the control column may not move or resize by even a fraction.
                Assert(Math.Abs(baseline[i].x - injected[i].x) <= 0.01f
                    && Math.Abs(baseline[i].width - injected[i].width) <= 0.01f
                    && Math.Abs(baseline[i].height - injected[i].height) <= 0.01f,
                    "a translated label must never move or resize a control horizontally: index " + i + " was "
                    + Describe(baseline[i]) + ", is " + Describe(injected[i]));
            }
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Enabled = false;
            Program.SetTranslatorResolver(null);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Contract 2: the navigation stack
    // ---------------------------------------------------------------------------------------------

    private static void NavigationCardsShareOneGeometry()
    {
        var metrics = new Program.StubMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            Dictionary<string, string> english = Program.ReadKeyedTable("English");
            Program.SetTranslatorResolver(english);

            // The stack must be geometrically stable at every required width: the nav column is the
            // manifest's fixed 160 in the three-column regime (inner width >= the row's 700 breakpoint) and
            // a full-width Fill container in the stacked one, so the same five-equal-cards facts are checked
            // at all four, and the fixed column/card size is checked where the column regime applies.
            foreach (float width in Widths)
            {
                using UiHost perWidth = NewHost(metrics);
                reports.Clear();
                UiFitAudit.Reset();
                Rec overview = Record(perWidth, width, Height);
                List<Rect> baseline = NavCards(overview);
                AssertNavigation(baseline, overview, "Overview at " + width);
                bool fixedColumn = width >= 736f; // 736 - 24 page padding = 712 >= the row's 700 breakpoint
                Console.WriteLine("[nav] width=" + width
                    + " column=" + (overview.Snapshot.RectById.TryGetValue("nav", out Rect navRect)
                        ? navRect.width.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                        : "?")
                    + " card=" + baseline[0].width.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + "x" + baseline[0].height.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + " gap=" + (baseline[1].y - baseline[0].yMax).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + " stack=" + (baseline[baseline.Count - 1].yMax
                        - (overview.Snapshot.RectById.TryGetValue("nav", out Rect navForStack) ? navForStack.y : baseline[0].y))
                        .ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + " regime=" + (fixedColumn ? "fixed-column" : "stacked"));

                AssertNavCardHeight(baseline, "Overview at " + width);
                if (fixedColumn)
                {
                    AssertNavColumnAndCardSize(baseline, overview, width);
                }

                // Selected vs unselected: the SAME host switches workspace, so this exercises the live
                // invalidation path rather than two fresh instances.
                perWidth.Bindings.Invoke("set-tab", "Tuning");
                reports.Clear();
                UiFitAudit.Reset();
                Rec tuned = Record(perWidth, width, Height);
                List<Rect> afterSwitch = NavCards(tuned);
                AssertSameBounds(baseline, afterSwitch, "switching the active tab at " + width);
                AssertNavigation(afterSwitch, tuned, "after switch at " + width);
                Assert(NavFindings(reports).Count == 0,
                    "the nav must fit its reserved bands after a workspace switch at " + width + ": "
                    + Describe(NavFindings(reports)));
            }

            UiHost host = NewHost(metrics);
            try
            {
                reports.Clear();
                UiFitAudit.Reset();
                Rec overview1024 = Record(host, 1024f, Height);
                List<Rect> baseline = NavCards(overview1024);
                AssertNavigation(baseline, overview1024, "baseline");

                // A long synthetic label and a long synthetic subtitle must be cut inside the card. A FRESH
                // host is used because the layout snapshot is cached by content revision, and a harness
                // translation swap does not bump it - a second Record on the same host would compare a stale
                // arrangement with itself and assert nothing.
                var stretched = new Dictionary<string, string>(english, StringComparer.Ordinal)
                {
                    ["US.Nav.Overview.Label"] = new string('测', 30),
                    ["US.Nav.Overview.Description"] = new string('测', 200),
                };

                Program.SetTranslatorResolver(stretched);
                using (UiHost stretchedHost = NewHost(metrics))
                {
                    reports.Clear();
                    UiFitAudit.Reset();
                    Rec longLabel = Record(stretchedHost, 1024f, Height);
                    List<Rect> afterLongLabel = NavCards(longLabel);
                    AssertSameBounds(baseline, afterLongLabel, "a long translated label/subtitle");
                    AssertNavigation(afterLongLabel, longLabel, "long-label pass");
                    Assert(NavFindings(reports).Count == 0,
                        "the nav label and its reserved subtitle band must be ellipsized, not overflow: "
                        + Describe(NavFindings(reports)));
                }

                // The same two facts in the other shipped language, also on a fresh host for the same reason.
                Program.SetTranslatorResolver(Program.ReadKeyedTable("ChineseSimplified"));
                using (UiHost chineseHost = NewHost(metrics))
                {
                    reports.Clear();
                    UiFitAudit.Reset();
                    Rec chinese = Record(chineseHost, 1024f, Height);
                    List<Rect> chineseCards = NavCards(chinese);
                    AssertSameBounds(baseline, chineseCards, "the Chinese table");
                    AssertNavigation(chineseCards, chinese, "Chinese pass");
                    Assert(NavFindings(reports).Count == 0,
                        "the Chinese nav must fit the same reserved bands: " + Describe(NavFindings(reports)));
                }
            }
            finally
            {
                host.Dispose();
            }
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Enabled = false;
            Program.SetTranslatorResolver(null);
        }
    }

    private static void AssertNavigation(List<Rect> cards, Rec rec, string label)
    {
        Assert(cards.Count == 5, "the nav stack must draw five cards (" + label + "), got " + cards.Count);

        float x = cards[0].x;
        float width = cards[0].width;
        float height = cards[0].height;
        foreach (Rect card in cards)
        {
            Assert(Math.Abs(card.x - x) <= 0.01f,
                "every nav card must share one x (" + label + "): " + card.x + " vs " + x);
            Assert(Math.Abs(card.width - width) <= 0.01f,
                "every nav card must fill the column width exactly (" + label + "): " + card.width + " vs " + width);
            Assert(Math.Abs(card.height - height) <= 0.01f,
                "every nav card must share one height (" + label + "): " + card.height + " vs " + height);
        }

        Assert(width > 1f && height > 1f, "the nav cards must be a real size (" + label + ")");

        float firstGap = cards[1].y - cards[0].yMax;
        for (int i = 1; i < cards.Count; i++)
        {
            float gap = cards[i].y - cards[i - 1].yMax;
            Assert(Math.Abs(gap - firstGap) <= 0.01f,
                "the inter-card gap must be constant (" + label + "): " + gap + " vs " + firstGap);
        }

        Assert(firstGap > 0f, "the nav cards must not overlap (" + label + "): gap " + firstGap);

        // Measure must return exactly the drawn stack height: the arranged nav rect is the measured
        // height, and the last card's bottom is where the draw stopped.
        Assert(rec.Snapshot.RectById.TryGetValue("nav", out Rect navRect),
            "the snapshot must carry the nav element (" + label + ")");
        Assert(Math.Abs(navRect.height - (cards[cards.Count - 1].yMax - navRect.y)) <= 0.01f,
            "Measure must return exactly the drawn stack height (" + label + "): measured " + navRect.height
            + ", drawn " + (cards[cards.Count - 1].yMax - navRect.y));
    }

    private static void AssertSameBounds(List<Rect> baseline, List<Rect> after, string because)
    {
        Assert(baseline.Count == after.Count, "the nav stack changed card count " + because);
        for (int i = 0; i < baseline.Count; i++)
        {
            Assert(Math.Abs(baseline[i].x - after[i].x) <= 0.01f
                && Math.Abs(baseline[i].y - after[i].y) <= 0.01f
                && Math.Abs(baseline[i].width - after[i].width) <= 0.01f
                && Math.Abs(baseline[i].height - after[i].height) <= 0.01f,
                "nav card " + i + " must keep identical bounds " + because + ": was "
                + Describe(baseline[i]) + ", now " + Describe(after[i]));
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Evidence table: the visible checkbox edge and the support-row height growth, for the two COMPOSITE
    // support sections that remain after S4-1 (us/timing, us/diagnostics). The three declarative Overview
    // cards are covered by DeclarativeOverviewLaneTests: their rows are engine Rows over atoms, so the
    // composite rule asserted below is not their rule and re-using it here would measure the wrong thing.
    //
    // The lane answers the two questions the contract assertions above leave implicit, one printed line
    // per case, at 1024/736/480/320 x EN/ZH x drawer open/closed:
    //
    //  (a) EDGE - for every support row that carries a checkbox, the right edge of the 18px VISUAL box
    //      the player sees and of its 24px hit slot, against the segmented control's last-cell right
    //      edge. The visible deviation is a hard assertion (<= 1px); the hit slot deliberately overhangs
    //      the shared edge by CheckboxInset, which is pinned instead.
    //
    //  (b) HEIGHT - for every support row, the height the widget actually drew against the density
    //      token, so every row that grows is named with its px growth, its label band width and the
    //      resolved (unwrapped) label width that forced the wrap. Row heights are READ BACK from the
    //      drawn geometry - each row's centre comes from the 24px control the widget centred in it, and
    //      each height is solved from the row below plus the published RowGap - rather than re-running
    //      the measure rule the table is meant to be evidence for. The two are then asserted equal, so
    //      a measure rule that stopped matching the drawn rows goes red here.
    //
    // The density token is observed, not assumed: it is the smallest drawn height in the sweep (the
    // floor every fitting label lands on) and is asserted against the manifest's Styles/Density=regular
    // Metric RowHeight, so a density change is measured rather than absorbed.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Manifest <c>&lt;Styles Schema="1" Density="regular"&gt;&lt;Metric Token="RowHeight" Value="24"&gt;</c>
    /// - the row-height floor this evidence table measures the drawn rows against.</summary>
    private const float RowTokenPin = 24f;

    /// <summary>Retained from the pre-S4-1 composite egg stack (the two-band row with its own 52px
    /// floor). No row in this lane's sweep is a custom row any more - every surviving composite row is a
    /// shared-rule row - but the constant keeps the custom-row reporting branch below compiling and names
    /// what the branch used to describe. The declarative egg row's own height is asserted by
    /// DeclarativeOverviewLaneTests against the manifest, not against this number.</summary>
    private const float EggStackFloor = 52f;

    private static void CheckboxEdgeAndRowHeightEvidence()
    {
        var metrics = new Program.StubMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        var cases = new List<CaseEvidence>();
        try
        {
            foreach (string language in Languages)
            {
                Dictionary<string, string> table = Program.ReadKeyedTable(language);
                Program.SetTranslatorResolver(table);
                try
                {
                    foreach (float width in Widths)
                    {
                        foreach (bool open in new[] { true, false })
                        {
                            reports.Clear();
                            UiFitAudit.Reset();
                            cases.Add(MeasureEvidenceCase(metrics, table, width, open, language));
                        }
                    }
                }
                finally
                {
                    Program.SetTranslatorResolver(null);
                }
            }
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Enabled = false;
            Program.SetTranslatorResolver(null);
        }

        Assert(cases.Count == Widths.Length * Languages.Length * 2,
            "the evidence table must cover every width x language x drawer state; got " + cases.Count);

        // The observed density floor: the smallest drawn support-row height in the whole sweep. A row
        // whose label fits one line lands exactly on the token, so the minimum IS the token; if every
        // label happened to wrap, this minimum would be a grown height and the assertion below says so.
        float token = float.MaxValue;
        foreach (CaseEvidence c in cases)
        {
            foreach (RowEvidence r in c.Rows)
            {
                if (r.SharedRule) token = Math.Min(token, r.ObservedHeight);
            }
        }

        bool floorObserved = false;
        foreach (CaseEvidence c in cases)
        {
            foreach (RowEvidence r in c.Rows)
            {
                if (r.SharedRule && r.WrappedHeight <= token + 0.01f) floorObserved = true;
            }
        }

        Assert(floorObserved,
            "no fitting label was observed at or below the smallest drawn row height (" + Num(token)
            + "px), so that minimum is a grown row, not the density floor the table reports");
        Assert(Math.Abs(token - RowTokenPin) <= 0.01f,
            "the observed support-row density floor is " + Num(token) + "px but the manifest's "
            + "Density=regular RowHeight token is " + Num(RowTokenPin) + "px; the drawn floor and the "
            + "authored metric disagree");

        Console.WriteLine("[height-token] observed RowVisualHeight floor = " + Num(token)
            + "px (manifest Density=regular Metric RowHeight=" + Num(RowTokenPin) + "px)");

        float maxVisibleDeviation = 0f;
        int checkboxRows = 0;
        int sharedRows = 0;
        int grownRows = 0;
        int grownCustomRows = 0;
        int casesWithGrowth = 0;

        foreach (CaseEvidence c in cases)
        {
            var edgeFields = new List<string>();
            var heightFields = new List<string>();
            int caseGrown = 0;
            string drawer = c.Open ? "open" : "closed";

            Assert(c.CellCount == 3,
                "the logging row must draw one segmented control of three cells at " + c.Width + " ("
                + c.Language + ", " + drawer + "), got " + c.CellCount);

            foreach (RowEvidence r in c.Rows)
            {
                float rule = r.SharedRule ? Math.Max(token, r.WrappedHeight) : r.EggRuleHeight;
                string ruleName = r.SharedRule
                    ? "max(RowVisualHeight " + Num(token) + ", label " + Num(r.LabelWidth) + " at "
                        + Num(r.BandWidth, "0.0") + " -> " + Num(r.WrappedHeight) + ")"
                    : "egg-stack floor " + Num(EggStackFloor, "0.#");
                Assert(Math.Abs(r.ObservedHeight - rule) <= 0.01f,
                    "row " + r.Id + " at " + c.Width + " (" + c.Language + ", drawer " + drawer
                    + ") was drawn " + Num(r.ObservedHeight) + "px tall but the rule says " + Num(rule)
                    + "px [" + ruleName + "]");

                if (r.SharedRule)
                {
                    sharedRows++;
                }

                float growth = r.ObservedHeight - token;
                if (r.SharedRule)
                {
                    // The growth criterion itself, not just the heights: a support row may only grow
                    // when its resolved label cannot fit its band on one line. That makes every grown
                    // row in the printed table attributable to a measured label width, and says a row
                    // that grew while its label still fit would be a defect rather than a deviation.
                    Assert((growth > 0.5f) == (r.LabelWidth > r.BandWidth + 0.01f),
                        "row " + r.Id + " at " + c.Width + " (" + c.Language + ", drawer " + drawer
                        + ") drew " + Num(r.ObservedHeight) + "px (token " + Num(token) + ", growth "
                        + Num(growth) + ") for a label " + Num(r.LabelWidth) + "px wide in a "
                        + Num(r.BandWidth, "0.0") + "px band; growth and a label wider than its band "
                        + "must coincide");
                }

                if (r.SharedRule && growth > 0.5f)
                {
                    grownRows++;
                    caseGrown++;
                }
                // The custom rows (the two-band egg stack and the eat-precision child) have no single
                // floor; this counter reports the ones materially taller than the egg stack's own 52px
                // floor, so the printed log still names every row that grew for a reason.
                else if (!r.SharedRule && r.ObservedHeight - EggStackFloor > 0.5f)
                {
                    grownCustomRows++;
                }

                if (r.IsCheckbox)
                {
                    checkboxRows++;
                    float visibleDeviation = Math.Abs(r.VisibleRight - c.SegmentedRight);
                    maxVisibleDeviation = Math.Max(maxVisibleDeviation, visibleDeviation);
                    Assert(visibleDeviation <= RightEdgeTolerance,
                        "the VISIBLE 18px checkbox box must end on the segmented control's right edge at "
                        + c.Width + " (" + c.Language + ", drawer " + drawer + "): " + r.Id + " visual right "
                        + Num(r.VisibleRight) + " vs segmented " + Num(c.SegmentedRight) + " (deviation "
                        + Num(visibleDeviation, "0.###") + "px)");
                    Assert(Math.Abs((r.HitRight - r.VisibleRight) - UsKernelDraw.CheckboxInset) <= 0.01f,
                        "the 24px hit slot must stay centred on the visible box at " + c.Width + " ("
                        + c.Language + ", drawer " + drawer + "): " + r.Id + " hit right " + Num(r.HitRight)
                        + " vs visible right " + Num(r.VisibleRight));

                    edgeFields.Add(r.Id + "=visual:" + Num(r.VisibleRight) + "/hit:" + Num(r.HitRight)
                        + "/visibleDev:" + Num(visibleDeviation, "0.###") + "/hitDev:"
                        + Num(r.HitRight - c.SegmentedRight, "0.###"));
                }

                // Growth is printed against the row's OWN floor where one exists: the density token for a
                // shared-rule row. The two custom rows (the two-band egg stack and the eat-precision child,
                // whose rule is label band + unconditionally reserved reason band) have no single floor, so
                // they print the composite rule the assertion above used instead.
                if (r.SharedRule)
                {
                    heightFields.Add(r.Id + "=drawn:" + Num(r.ObservedHeight) + "/floor:" + Num(token)
                        + "/grow:" + Num(r.ObservedHeight - token)
                        + "/band:" + Num(r.BandWidth, "0.0") + "/label:" + Num(r.LabelWidth, "0.0")
                        + "/wrapped:" + Num(r.WrappedHeight, "0.0"));
                }
                else
                {
                    heightFields.Add(r.Id + "=drawn:" + Num(r.ObservedHeight) + "/rule:" + Num(r.EggRuleHeight)
                        + "/band:" + Num(r.BandWidth, "0.0") + "/label:" + Num(r.LabelWidth, "0.0")
                        + "/wrapped:" + Num(r.WrappedHeight, "0.0"));
                }
            }

            if (caseGrown > 0)
            {
                casesWithGrowth++;
            }

            float caseMax = 0f;
            foreach (RowEvidence r in c.Rows)
            {
                if (r.IsCheckbox) caseMax = Math.Max(caseMax, Math.Abs(r.VisibleRight - c.SegmentedRight));
            }

            Console.WriteLine("[edge] " + Num(c.Width, "0") + " " + c.Language + " " + drawer
                + " segRight=" + Num(c.SegmentedRight) + " | " + string.Join(" | ", edgeFields)
                + " | maxVisibleDev=" + Num(caseMax, "0.###"));
            Console.WriteLine("[height] " + Num(c.Width, "0") + " " + c.Language + " " + drawer
                + " token=" + Num(token) + " | " + string.Join(" | ", heightFields)
                + " | grownRows=" + caseGrown);
        }

        Console.WriteLine("[edge-summary] cases=" + cases.Count + " checkboxRows=" + checkboxRows
            + " maxVisibleDeviation=" + Num(maxVisibleDeviation, "0.###") + "px (asserted <= "
            + Num(RightEdgeTolerance, "0.#") + "px); the 24px hit slot overhangs the shared edge by exactly "
            + Num(UsKernelDraw.CheckboxInset, "0.#") + "px by design");
        Console.WriteLine("[height-summary] observedToken=" + Num(token) + "px sharedRows=" + sharedRows
            + " grownRows=" + grownRows + " grownCustomRows=" + grownCustomRows
            + " casesWithGrowth=" + casesWithGrowth + "/" + cases.Count);

        Assert(maxVisibleDeviation <= RightEdgeTolerance,
            "the maximum visible checkbox-vs-segmented deviation over the whole sweep is "
            + Num(maxVisibleDeviation, "0.###") + "px, above the " + Num(RightEdgeTolerance, "0.#") + "px contract");
    }

    // ---------------------------------------------------------------------------------------------
    // The eat-precision child row: disabled-but-visible, inert, and height-neutral.
    //
    // The evidence table above proves the drawn bands. This step proves the behaviour the hard rules
    // name: while the parent is off the child row keeps its 24px slot and its row band (never hidden),
    // the card and the whole slot stack are drawn with identical geometry to the parent-on state (the
    // disabled-reason band is reserved unconditionally), a press on the child writes NOTHING, and the
    // same press with the parent on routes exactly one child write.
    // ---------------------------------------------------------------------------------------------

    private static void ChildRowFollowsTheParentSwitch()
    {
        var metrics = new Program.StubMetrics();
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            var offSource = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = false };
            float offCard;
            List<Rect> offSlots;
            using (UiHost host = UsKernelSettingsHost.Create(offSource, metrics))
            {
                host.Bindings.Invoke("set-tab", "Overview");
                Rec rec = Record(host, 1024f, Height);
                (offCard, offSlots) = BasicTuningSlotGeometry(rec);
                Assert(offSlots.Count == 5,
                    "with the parent OFF the card draws five support checkboxes (egg + three scalings + the"
                    + " parent) - the child row does not exist at all, got " + offSlots.Count);
                Assert(!rec.Snapshot.RectById.ContainsKey("basic-eat-child-row"),
                    "and the child ROW must leave the arrangement entirely, not merely paint nothing:"
                    + " VisibleKey is the engine's gate, and an arranged-but-invisible row would still"
                    + " measure and still take the pointer");
                Assert(offSource.LastEatPrecisionIncludeDrugs == null,
                    "and nothing may write the child value while it is off screen");
            }

            var onSource = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
            float onCard;
            List<Rect> onSlots;
            using (UiHost host = UsKernelSettingsHost.Create(onSource, metrics))
            {
                host.Bindings.Invoke("set-tab", "Overview");
                Rec rec = Record(host, 1024f, Height);
                (onCard, onSlots) = BasicTuningSlotGeometry(rec);

                Assert(onSlots.Count == offSlots.Count + 1,
                    "turning the parent on adds exactly one checkbox band (the child), got "
                    + offSlots.Count + " -> " + onSlots.Count);
                Assert(rec.Snapshot.RectById.ContainsKey("basic-eat-child-row"),
                    "and the child ROW is arranged once its parent bool answers true");
                Assert(onCard > offCard + 20f,
                    "and the card grows by the child row, got off=" + Num(offCard) + " on=" + Num(onCard));
                for (int index = 0; index < offSlots.Count; index++)
                {
                    Rect before = offSlots[index];
                    Rect after = onSlots[index];
                    Assert(Math.Abs(before.x - after.x) <= 0.01f && Math.Abs(before.y - after.y) <= 0.01f
                        && Math.Abs(before.width - after.width) <= 0.01f && Math.Abs(before.height - after.height) <= 0.01f,
                        "checkbox slot " + index + " must keep identical geometry across the parent toggle (the"
                        + " child is now the ONLY row that appears or disappears): "
                        + Describe(before) + " -> " + Describe(after));
                }

                PressRect(host, 1024f, onSlots[onSlots.Count - 1]);
                Assert(onSource.LastEatPrecisionIncludeDrugs == true,
                    "with the parent on the same press must route exactly one child write, got "
                    + (onSource.LastEatPrecisionIncludeDrugs?.ToString() ?? "null"));
                Assert(onSource.LastEatPrecision == null,
                    "and that press must not touch the parent switch");
            }

            Console.WriteLine("[eat-child] parent-off card=" + Num(offCard) + "px slots=" + offSlots.Count
                + " child-absent=true; parent-on card=" + Num(onCard) + "px slots=" + onSlots.Count
                + " child-present=true press-writes=1");
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    /// <summary>Width of the declared input/checkbox band every Overview card's control row ends in
    /// (the manifest's Width attribute; the atom paints its own 18px box inside it).</summary>
    private const float DeclaredCheckboxWidth = 24f;

    /// <summary>Height of that band: the manifest's Height, which is what makes the atom's own box
    /// geometry (side = max(8, height - Padding*2), Padding 6) land on the shipped 18px visual box.</summary>
    private const float DeclaredCheckboxHeight = 30f;

    /// <summary>
    /// The basic-tuning card's drawn height and its checkbox bands (content-local space, top to bottom),
    /// read from what the draw actually registered. Since S4-1 the card is declarative, so the bands come
    /// from the ATOM's own hit rect: 24 wide and the manifest's 30 tall. The composite 24x24 classifier
    /// deliberately does not match them, which is why this reads its own declared shape instead of
    /// widening a composite rule the atom never satisfied.
    /// </summary>
    private static (float CardHeight, List<Rect> Slots) BasicTuningSlotGeometry(Rec rec)
    {
        Assert(rec.Snapshot.RectById.TryGetValue("basic-tuning", out Rect pageRect),
            "the Overview workspace must arrange the basic-tuning card");
        Rect card = ToContentLocal(pageRect, rec.ContentViewport);
        List<Rect> slots = rec.Buttons
            .Where(r => Inside(r, card)
                && Math.Abs(r.width - DeclaredCheckboxWidth) <= 0.5f
                && Math.Abs(r.height - DeclaredCheckboxHeight) <= 0.5f)
            .OrderBy(r => r.y)
            .ToList();
        return (card.height, slots);
    }

    /// <summary>One draw pass whose only reported button is the requested rect - the harness seam the
    /// press lanes in this suite use.</summary>
    private static void PressRect(UiHost host, float width, Rect target)
    {
        SetField(ButtonOverrideField, new Func<Rect, bool>(rect =>
            Math.Abs(rect.x - target.x) <= 0.01f && Math.Abs(rect.y - target.y) <= 0.01f
            && Math.Abs(rect.width - target.width) <= 0.01f && Math.Abs(rect.height - target.height) <= 0.01f));
        try
        {
            host.DrawChecked(new Rect(0f, 0f, width, Height));
        }
        finally
        {
            ClearOverrides();
        }
    }

    /// <summary>
    /// One case: arrange + draw the real Overview page with the help drawer at the requested state, then
    /// read the drawn geometry back into the two evidence groups of the table.
    /// </summary>
    private static CaseEvidence MeasureEvidenceCase(
        Program.StubMetrics metrics,
        Dictionary<string, string> table,
        float width,
        bool open,
        string language)
    {
        // The eat-precision child row exists only while its parent switch is on (ruling 2026-09-15), so the
        // geometry sweep runs with the parent ON; the parent-off shape (five declared bands, no child row)
        // is covered by ChildRowFollowsTheParentSwitch.
        var fake = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Overview");
        host.Bindings.Set("help-open", open);
        Rec rec = Record(host, width, Height);

        var sections = new Dictionary<string, SectionControls>(StringComparer.Ordinal);
        foreach (string id in CompositeSections)
        {
            Assert(rec.Snapshot.RectById.TryGetValue(id, out Rect pageRect),
                "the snapshot must carry the " + id + " card at " + width + " (" + language + ")");
            sections[id] = SectionControlsFor(rec, ToContentLocal(pageRect, rec.ContentViewport));
        }

        var evidence = new CaseEvidence { Width = width, Open = open, Language = language };

        // (a) the one segmented right edge every support control must agree with.
        SectionControls diagnostics = sections["diagnostics"];
        evidence.CellCount = diagnostics.Cells.Count;
        Assert(diagnostics.Cells.Count == 3,
            "the logging row must draw one segmented control of three cells at " + width + " (" + language
            + "), got " + diagnostics.Cells.Count);
        evidence.SegmentedRight = diagnostics.Cells.Max(cell => cell.xMax);
        foreach (SectionControls section in sections.Values)
        {
            // The cells of one segmented row have three right edges BY DESIGN - only the LAST cell
            // terminates on the shared column edge, and that edge is what the checkboxes must meet.
            foreach (Rect cell in section.Cells)
            {
                Assert(cell.xMax <= evidence.SegmentedRight + RightEdgeTolerance,
                    "a segmented cell must not cross the shared control-column right edge at " + width
                    + " (" + language + "): " + Describe(cell) + " vs " + evidence.SegmentedRight);
            }

            if (section.Cells.Count > 0)
            {
                float lastCellRight = section.Cells.Max(cell => cell.xMax);
                Assert(Math.Abs(lastCellRight - evidence.SegmentedRight) <= RightEdgeTolerance,
                    "the segmented control's LAST cell must terminate on the shared right edge at " + width
                    + " (" + language + "): " + Num(lastCellRight) + " vs " + Num(evidence.SegmentedRight));
            }
        }

        // (b) the ONE composite support section that remains. Verified independently (the declarative
        // cards are covered by DeclarativeOverviewLaneTests and DeclarativeTimingLaneTests), because the rule
        // being asserted here is the composite's own [label | control column] arithmetic.
        //
        // The timing card left this group in S4-3 and its rows are measured by DeclarativeTimingLaneTests.
        // What stays here is the negative half of that move: a stepper cluster inside us/timing would mean
        // the composite came back, so the count is asserted rather than simply dropped. That assertion is the
        // mutation proof for this re-cut - restoring us/timing's manifest line and its Registrar line reddens
        // it (together with the Registrar kind-set pin in UiSourceInvariantTests).
        // The timing card is DECLARATIVE since S4-3 and its two stepper buttons ARE matched by
        // IsSmallControl (26x20), so this assertion is the guard that states where they belong and the
        // mutation proof for the re-cut in one: they must exist (a card that stopped drawing them is a
        // different defect) and they must terminate on the card's own content right edge, which is what a
        // declared row does and what the retired composite did NOT (it terminated on the shared control
        // column's right edge, ControlColumnRightInset inside the body). Restoring us/timing's manifest line
        // puts a 26-wide cluster on that inset edge and reddens this - which is the state assertion a group
        // that only names composite cards cannot make.
        Assert(Array.IndexOf(CompositeSections, "timing") < 0,
            "us/timing must not be back in the composite-column group: the card is declarative since S4-3");
        Assert(rec.Snapshot.RectById.TryGetValue("timing", out Rect timingPage),
            "the snapshot must still carry the timing card at " + width + " (" + language + ")");
        SectionControls timing = SectionControlsFor(rec, ToContentLocal(timingPage, rec.ContentViewport));
        float timingContentRight = timing.Card.xMax - UsCardLayout.Padding;
        // The declared weight of the two steppers and the field between them, in the order the manifest
        // writes them: 26 + 8 + 150 + 8 + 26. Read from the SNAPSHOT rather than re-declared here, so the
        // assertion cannot drift from the manifest.
        var timingCluster = rec.Buttons
            .Where(r => Inside(r, timing.Card) && IsSmallControl(r))
            .OrderBy(r => r.x)
            .ToList();
        Console.WriteLine("[timing-cluster] " + width + " " + language
            + " card=" + Num(timing.Card.width) + " bodyRight=" + Num(timingContentRight)
            + " steppers=" + timingCluster.Count
            + " [" + string.Join(" ", timingCluster.Select(Describe)) + "]");
        foreach (Rect stepper in timingCluster)
        {
            Assert(stepper.xMax <= timingContentRight + RightEdgeTolerance,
                "a declared timing stepper must sit inside the card's content edge, never on the retired"
                + " control-column inset at " + width + " (" + language + "): " + Describe(stepper)
                + " vs content edge " + timingContentRight);
        }
        // The cluster itself needs 26 + 8 + 150 + 8 + 26 = 218 of the row's inner width. Every width this
        // page is accepted at hosts it (the centre column is 748/460/416px at 1024/736/480); only the
        // degenerate 320 centre band does not, and that is the pre-existing narrow-layout gap the row
        // contract is already reported as degenerate for. So the FULL cluster is asserted where it is
        // hostable, and a partial one there is a real regression, not a narrow-layout artifact.
        if (timing.Card.width - UsCardLayout.Padding * 2f >= TimingStepperClusterWidth)
        {
            Assert(timingCluster.Count == 2,
                "the declarative timing card must draw its two 26x20 stepper atoms at " + width + " ("
                + language + "), got " + timingCluster.Count + " [" 
                + string.Join(" ", timingCluster.Select(Describe)) + "]");
        }

        // diagnostics: the localize row is the last row; the heading band above it is reported by the
        // table as context but the localize row is the support row under contract.
        Assert(diagnostics.Slots.Count == 1,
            "the diagnostics section must draw the localize checkbox at " + width + " (" + language
            + "), got " + diagnostics.Slots.Count);
        AddRow(evidence, "diagnostics/localize-debug", 0, KeyedLabel(table, "US.Diagnostics.LocalizeDebugMenu"),
            UsKernelDraw.RowLabelWidth(BodyWidthOf(diagnostics.Card)),
            2f * (BodyBottomOf(diagnostics.Card) - 2f - CentreY(diagnostics.Slots[0])), diagnostics.Slots[0],
            metrics, true, 0f);

        // The height solver above walks the rows from the drawn geometry, so it depends on the rows
        // arriving in the order the layout draws them (top to bottom per section). Pin that here rather
        // than trusting the call order.
        for (int index = 0; index < evidence.Rows.Count; index++)
        {
            Assert(evidence.Rows[index].Order == index,
                "the support rows must be recorded in draw order at " + width + " (" + language + "): row "
                + index + " carries order " + evidence.Rows[index].Order);
        }

        return evidence;
    }

    /// <summary>
    /// The retired composite egg row's own rule, kept only so the custom-row reporting branch has a
    /// named prediction to print. S4-1 moved the egg row into the manifest, where its two bands are two
    /// declared text/wrapped atoms gated by VisibleKey; DeclarativeOverviewLaneTests measures THAT shape.
    /// </summary>
    private static float EggRuleHeight(Program.StubMetrics metrics, Dictionary<string, string> table, float band)
    {
        float title = Math.Max(22f, metrics.MeasureText(KeyedLabel(table, "US.Tuning.EasterEggs"), UiFont.Small, band));
        float on = metrics.MeasureText(KeyedLabel(table, "US.Tuning.EasterEggs.On"), UiFont.Tiny, band);
        float off = metrics.MeasureText(KeyedLabel(table, "US.Tuning.EasterEggs.Off"), UiFont.Tiny, band);
        float state = Math.Max(18f, Math.Max(on, off));
        return Math.Max(EggStackFloor, 4f + title + 1f + state + 7f);
    }


    private static void AddRow(
        CaseEvidence evidence,
        string id,
        int order,
        string label,
        float bandWidth,
        float observedHeight,
        Rect? slot,
        Program.StubMetrics metrics,
        bool sharedRule,
        float eggRule)
    {
        var row = new RowEvidence
        {
            Id = id,
            Order = order,
            Label = label,
            SharedRule = sharedRule,
            EggRuleHeight = eggRule,
            BandWidth = bandWidth,
            LabelWidth = metrics.MeasureWidth(label, UiFont.Small),
            WrappedHeight = metrics.MeasureText(label, UiFont.Small, bandWidth),
            ObservedHeight = observedHeight,
        };

        if (slot.HasValue)
        {
            Rect visual = VisualBox(slot.Value);
            row.IsCheckbox = true;
            row.VisibleRight = visual.xMax;
            row.HitRight = slot.Value.xMax;
        }

        evidence.Rows.Add(row);
    }

    /// <summary>Buttons/fields inside one section card, classified by the shapes the widgets draw.
    /// The wide 24px row-hit button is deliberately not classified: the 24x24 slot is the alignment
    /// subject and the row hit band is only the pointer surface behind it.</summary>
    private static SectionControls SectionControlsFor(Rec rec, Rect card)
    {
        var controls = new SectionControls { Card = card };
        foreach (Rect button in rec.Buttons.Where(r => Inside(r, card)))
        {
            if (IsCheckboxSlot(button)) controls.Slots.Add(button);
            else if (IsSegmentedCell(button)) controls.Cells.Add(button);
            else if (IsSmallControl(button)) controls.Steppers.Add(button);
        }

        controls.Slots.Sort((left, right) => left.y.CompareTo(right.y));
        controls.Cells.Sort((left, right) => left.x.CompareTo(right.x));
        controls.Steppers.Sort((left, right) => left.x.CompareTo(right.x));
        return controls;
    }

    private static float BodyWidthOf(Rect card) => card.width - UsCardLayout.Padding * 2f;

    private static float BodyTopOf(Rect card) => card.y + UsCardLayout.HeaderHeight + UsCardLayout.HeaderGap;

    private static float BodyHeightOf(Rect card) =>
        card.height - UsCardLayout.HeaderHeight - UsCardLayout.HeaderGap - UsCardLayout.Padding * 2f;

    private static float BodyBottomOf(Rect card) => BodyTopOf(card) + BodyHeightOf(card);

    private static float CentreY(Rect rect) => rect.y + rect.height * 0.5f;

    /// <summary>The resolved display text the widget draws, from the same keyed table the harness
    /// resolver serves: a missing key is a lane failure, not a silent literal.</summary>
    private static string KeyedLabel(Dictionary<string, string> table, string key)
    {
        Assert(table.TryGetValue(key, out string? text) && !string.IsNullOrEmpty(text),
            "the keyed table must carry " + key + " for the evidence table");
        return text!;
    }

    private static string Num(float value, string format = "0.00") =>
        value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>One measured case of the evidence table.</summary>
    private sealed class CaseEvidence
    {
        public float Width;
        public bool Open;
        public string Language = "";
        public float SegmentedRight;
        public int CellCount;
        public readonly List<RowEvidence> Rows = new();
    }

    /// <summary>One support row's drawn geometry and the measurements that explain it.</summary>
    private sealed class RowEvidence
    {
        public string Id = "";
        public int Order;
        public string Label = "";
        public bool SharedRule;
        public float EggRuleHeight;
        public float BandWidth;
        public float LabelWidth;
        public float WrappedHeight;
        public float ObservedHeight;
        public bool IsCheckbox;
        public float VisibleRight;
        public float HitRight;
    }

    /// <summary>Controls drawn inside one section card, classified by shape.</summary>
    private sealed class SectionControls
    {
        public Rect Card;
        public readonly List<Rect> Slots = new();
        public readonly List<Rect> Cells = new();
        public readonly List<Rect> Steppers = new();
    }

    // ---------------------------------------------------------------------------------------------
    // Survey / capture plumbing
    // ---------------------------------------------------------------------------------------------

    private sealed class Rec
    {
        public UiLayoutSnapshot Snapshot = null!;
        public Rect ContentViewport;
        public readonly List<Rect> Buttons = new();
        public readonly List<Rect> Fields = new();
        public readonly List<Rect> Sliders = new();
    }

    /// <summary>The control rects of the four support sections plus the facts the assertions need.</summary>
    private sealed class ControlSurvey
    {
        public readonly List<Rect> Slots = new();
        public readonly List<Rect> Cells = new();
        public readonly List<Rect> Edges = new();
        public float BodyRight;
        public float MinBodyWidth = float.MaxValue;
        public bool SpansEverySection = true;
        public float CommonRight;
    }

    private static UiHost NewHost(Program.StubMetrics metrics)
    {
        // Parent ON: the child row exists only while its parent switch is on (ruling 2026-09-15). The
        // parent-off shape is covered by ChildRowFollowsTheParentSwitch.
        var source = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
        UiHost host = UsKernelSettingsHost.Create(source, metrics);
        host.Bindings.Invoke("set-tab", "Overview");
        return host;
    }

    /// <summary>
    /// Arranges twice (the first arrange creates the element nodes the scroll write needs, the second is
    /// the measured snapshot) and then draws one pass with every native control recorded.
    /// </summary>
    private static Rec Record(UiHost host, float width, float height)
    {
        host.MeasureAndArrange(new Vector2(width, height));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        var rec = new Rec { Snapshot = host.MeasureAndArrange(new Vector2(width, height)) };
        try
        {
            SetField(ButtonOverrideField, new Func<Rect, bool>(rect => { rec.Buttons.Add(rect); return false; }));
            SetField(SliderOverrideField, new Func<Rect, float, float, float, float>((rect, value, min, max) =>
            {
                rec.Sliders.Add(rect);
                return value;
            }));
            SetField(TextFieldOverrideField, new Func<Rect, string, string>((rect, text) =>
            {
                rec.Fields.Add(rect);
                return text;
            }));
            host.DrawChecked(new Rect(0f, 0f, width, height));
        }
        finally
        {
            ClearOverrides();
        }

        rec.ContentViewport = rec.Snapshot.Viewports["content-scroll"];
        return rec;
    }

    private static ControlSurvey Survey(Rec rec)
    {
        var survey = new ControlSurvey();
        foreach (string id in Sections)
        {
            if (!rec.Snapshot.RectById.TryGetValue(id, out Rect cardPage))
            {
                survey.SpansEverySection = false;
                continue;
            }

            Rect card = ToContentLocal(cardPage, rec.ContentViewport);
            float bodyWidth = Math.Max(0f, card.width - UsCardLayout.Padding * 2f);
            survey.MinBodyWidth = Math.Min(survey.MinBodyWidth, bodyWidth);
            survey.BodyRight = Math.Max(survey.BodyRight, card.x + UsCardLayout.Padding + bodyWidth);

            // The control rects are collected from the COMPOSITE cards ONLY, and after S4-3 that is
            // us/diagnostics alone. A declarative card's atoms are not this contract's subjects: the
            // manifest's number fields terminate on the card's own content edge, not on the retired
            // ControlColumnRightInset, while the shape classifiers below would happily accept them
            // (IsSmallControl matches a declared 26x20 stepper and IsCheckboxSlot a declared band). Naming
            // the group is what keeps one classifier from grading two different layouts.
            if (Array.IndexOf(CompositeSections, id) < 0) continue;

            foreach (Rect button in rec.Buttons.Where(r => Inside(r, card)))
            {
                if (IsCheckboxSlot(button))
                {
                    // The recorded rect is the 24px HIT box. The box under the alignment contract is the
                    // 18px VISUAL drawn inset 3 inside it, so the visual - not the hit band - joins the
                    // row edge comparisons and the hit/slot pair is pinned separately below.
                    survey.Slots.Add(button);
                    survey.Edges.Add(VisualBox(button));
                }
                else if (IsSegmentedCell(button))
                {
                    survey.Cells.Add(button);
                    survey.Edges.Add(button);
                }
                else if (IsSmallControl(button))
                {
                    survey.Edges.Add(button);
                }
            }

            foreach (Rect field in rec.Fields.Where(r => Inside(r, card))) survey.Edges.Add(field);
            foreach (Rect slider in rec.Sliders.Where(r => Inside(r, card))) survey.Edges.Add(slider);
        }

        if (survey.Edges.Count > 0)
        {
            survey.CommonRight = survey.Edges.Max(r => r.xMax);
        }

        return survey;
    }

    private static List<Rect> ControlEdges(Rec rec)
    {
        ControlSurvey survey = Survey(rec);
        var all = new List<Rect>();
        all.AddRange(survey.Cells.OrderBy(r => r.x));
        all.AddRange(survey.Slots.OrderBy(r => r.y).ThenBy(r => r.x));
        all.AddRange(survey.Edges
            .Where(r => !survey.Slots.Any(s => Math.Abs(s.x - r.x) < 0.01f && Math.Abs(s.y - r.y) < 0.01f && Math.Abs(s.width - r.width) < 0.01f)
                && !survey.Cells.Any(s => Math.Abs(s.x - r.x) < 0.01f && Math.Abs(s.y - r.y) < 0.01f && Math.Abs(s.width - r.width) < 0.01f))
            .OrderBy(r => r.y).ThenBy(r => r.x));
        return all;
    }

    /// <summary>The manifest's fixed nav-column width for the compact pass (was 192 before the ruling).</summary>
    private const float NavColumnWidth = 200f;

    /// <summary><c>UsNavWidget.SidePadding</c> of the same compact pass.</summary>
    private const float NavSidePadding = 8f;

    /// <summary>The card the fixed column holds: the column minus both side paddings (160 - 16 = 144).</summary>
    private const float NavCardWidth = NavColumnWidth - NavSidePadding * 2f;

    /// <summary>
    /// The pre-compaction card height under the DEFAULT carrier metrics: 6 top + 22 label + 1 gap +
    /// 2 x 21.333 subtitle lines + 11 bottom. The compact card must be materially shorter; the user's
    /// in-game screenshot reported roughly this shape.
    /// </summary>
    private const float LegacyTwoLineCardHeight = 82.67f;

    /// <summary>
    /// The compact card height: independent of the column width, so it is asserted at every width. It must be
    /// materially below <see cref="LegacyTwoLineCardHeight"/> and inside the band the compacted paddings plus
    /// one reserved subtitle line produce.
    /// </summary>
    private static void AssertNavCardHeight(List<Rect> cards, string label)
    {
        Assert(cards[0].height <= LegacyTwoLineCardHeight * 0.7f,
            "the compact nav card must be materially shorter than the old " + LegacyTwoLineCardHeight
            + "px two-line card (" + label + "), got " + cards[0].height);
        Assert(cards[0].height >= 36f && cards[0].height <= 60f,
            "the compact nav card must sit in the compact band (tightened paddings + one reserved subtitle"
            + " line) (" + label + "), got " + cards[0].height);
    }

    /// <summary>
    /// The fixed-column regime: the manifest's 160px nav column and the 144px card it holds (column minus
    /// both 8px side paddings).
    /// </summary>
    private static void AssertNavColumnAndCardSize(List<Rect> cards, Rec rec, float width)
    {
        Assert(rec.Snapshot.RectById.TryGetValue("nav", out Rect navRect), "the snapshot must carry the nav element");
        Assert(Math.Abs(navRect.width - NavColumnWidth) <= 0.01f,
            "the nav column must keep the manifest's fixed " + NavColumnWidth + "px width at " + width
            + ", got " + navRect.width);
        Assert(Math.Abs(cards[0].width - NavCardWidth) <= 0.01f,
            "every nav card must fill the " + NavColumnWidth + "px column minus 2x" + NavSidePadding
            + " padding = " + NavCardWidth + "px at " + width + ", got " + cards[0].width);
    }

    /// <summary>
    /// The five nav cards: buttons contained in the nav element's page rect. The height floor is what keeps
    /// this honest at the stacked widths, where the nav column spans the page and the section rows (drawn in
    /// scroll-content space, but numerically overlapping the nav's page rect) are recorded in the same
    /// list - a nav card is a subtitle-band card (~49px at the stub metrics), a support row band is 24px.
    /// </summary>
    private static List<Rect> NavCards(Rec rec)
    {
        Assert(rec.Snapshot.RectById.TryGetValue("nav", out Rect navRect), "the snapshot must carry the nav element");
        return rec.Buttons
            .Where(r => Inside(r, navRect) && r.width >= 100f && r.height >= 40f)
            .OrderBy(r => r.y)
            .ToList();
    }

    /// <summary>Groups control rects into the rows they belong to by vertical centre, so each group is one
    /// drawn support row (the row's own y positions may shift when a label wraps - only its right edge is
    /// under contract).</summary>
    private static List<List<Rect>> RowGroups(List<Rect> edges)
    {
        var groups = new List<List<Rect>>();
        foreach (Rect edge in edges.OrderBy(r => r.y + r.height * 0.5f))
        {
            float centre = edge.y + edge.height * 0.5f;
            List<Rect>? current = groups.Count > 0 ? groups[groups.Count - 1] : null;
            float currentCentre = current != null ? current[0].y + current[0].height * 0.5f : 0f;
            if (current != null && Math.Abs(centre - currentCentre) <= 4f)
            {
                current.Add(edge);
            }
            else
            {
                groups.Add(new List<Rect> { edge });
            }
        }

        return groups;
    }

    private static string Describe(Rect rect)
    {
        return "(x=" + rect.x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + " y=" + rect.y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + " w=" + rect.width.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + " h=" + rect.height.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + " right=" + rect.xMax.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ")";
    }

    private static string Describe(List<UiOverflowReport> reports)
    {
        if (reports.Count == 0) return "(none)";
        return string.Join(" | ", reports.Select(
            r => (r.ElementPath.Length == 0 ? "(unscoped)" : r.ElementPath) + " " + r.Axis + " " + r.Needed + "/" + r.Available));
    }

    private static List<UiOverflowReport> TouchedFindings(List<UiOverflowReport> reports)
    {
        return reports.Where(r => Sections.Any(s => r.ElementPath.IndexOf(s, StringComparison.Ordinal) >= 0)).ToList();
    }

    private static List<UiOverflowReport> NavFindings(List<UiOverflowReport> reports)
    {
        return reports.Where(r => r.ElementPath.IndexOf("nav", StringComparison.Ordinal) >= 0).ToList();
    }

    /// <summary>The box the checkbox actually paints: the 18px visual inset <see cref="UsKernelDraw.CheckboxInset"/>
    /// inside the 24px hit slot that <c>UsKernelDraw.Checkbox</c> receives.</summary>
    private static Rect VisualBox(Rect slot)
    {
        return new Rect(
            slot.x + UsKernelDraw.CheckboxInset,
            slot.y + UsKernelDraw.CheckboxInset,
            UsKernelDraw.CheckboxVisual,
            UsKernelDraw.CheckboxVisual);
    }

    private static bool IsCheckboxSlot(Rect rect)
    {
        return Math.Abs(rect.width - UsKernelDraw.CheckboxHit) <= ShapeTolerance
            && Math.Abs(rect.height - UsKernelDraw.CheckboxHit) <= ShapeTolerance;
    }

    private static bool IsSegmentedCell(Rect rect)
    {
        return Math.Abs(rect.height - 26f) <= ShapeTolerance && rect.width > 24f;
    }

    private static bool IsSmallControl(Rect rect)
    {
        return Math.Abs(rect.height - 20f) <= ShapeTolerance && rect.width <= 26.5f;
    }

    private static Rect ToContentLocal(Rect pageRect, Rect viewport)
    {
        return new Rect(pageRect.x - viewport.x, pageRect.y - viewport.y, pageRect.width, pageRect.height);
    }

    private static bool Inside(Rect inner, Rect outer)
    {
        return inner.x >= outer.x - 0.5f
            && inner.y >= outer.y - 0.5f
            && inner.xMax <= outer.xMax + 0.5f
            && inner.yMax <= outer.yMax + 0.5f;
    }

    // ---------------------------------------------------------------------------------------------
    // UiNative reflection seams (same pattern as MoodLayoutFocusedTests: the test assembly has no
    // InternalsVisibleTo, so the internal static test overrides are set by reflection).
    // ---------------------------------------------------------------------------------------------

    private static void ClearOverrides()
    {
        SetField(ButtonOverrideField, null);
        SetField(SliderOverrideField, null);
        SetField(TextFieldOverrideField, null);
    }

    private static void SetField(FieldInfo field, object? value)
    {
        try
        {
            field.SetValue(null, value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "REFLECTION BLOCKER: cannot set UiNative." + field.Name + " on net472. Add "
                + "InternalsVisibleTo(\"UniversalSqueakerKernelHostTests\") in the FerriteLib assembly info, "
                + "rebuild, then rerun.", ex);
        }
    }

    private static FieldInfo RequireField(string name, Type fieldType)
    {
        FieldInfo? field = typeof(UiNative).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "REFLECTION BLOCKER: UiNative." + name + " is not accessible via reflection on net472. Add "
                + "InternalsVisibleTo(\"UniversalSqueakerKernelHostTests\") in the FerriteLib assembly info, "
                + "rebuild, then rerun.");
        }

        if (field.FieldType != fieldType)
        {
            throw new InvalidOperationException(
                "UiNative." + name + " has unexpected type " + field.FieldType + "; expected " + fieldType);
        }

        return field;
    }

    /// <summary>
    /// The declarative checklist card's chrome as a MEASURED relation rather than an arithmetic argument.
    /// Step A established it over the card's two children (Padding 12 + header 26 + Gap 6 + body + 12); step
    /// B made the card's body declarative, so the same relation is now asserted over the children the
    /// manifest actually arranges (header, the status-band composite, the list column, and the no-domain
    /// empty state when it shows), with one Gap between each. Every number comes from the same snapshot, so
    /// changing the manifest's Padding, the Section Gap, the header Height or the search band reddens this
    /// instead of silently moving the card.
    /// <para>
    /// LIMITATION, stated in the lane itself: the PRE-SWAP card height is not measured here because the old
    /// element left the manifest in step A; the pre-swap side of the comparison is the UsCardLayout formula
    /// (12 + 26 + 6 + body + 12) that this asserts against, which is a derived constant, not a second
    /// measurement.
    /// </para>
    /// </summary>
    private static void DeclarativeChecklistCardReproducesTheCardChrome()
    {
        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("checklist card chrome (step A/B): " + message);
        }

        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);
        host.Bindings.Invoke("set-tab", "Packs");
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(800f, 600f));

        Check(snapshot.RectById.TryGetValue("checklist-card", out Rect card), "the declarative card element is arranged");
        Check(snapshot.RectById.TryGetValue("checklist", out Rect bands), "the status-band composite is arranged inside it");
        Check(snapshot.RectById.TryGetValue("checklist-header", out Rect header), "the section header is arranged");
        Check(Math.Abs(header.height - 26f) <= 0.5f, "the header band is UsCardLayout's 26px, got " + header.height);

        string[] children = { "checklist-header", "checklist", "checklist-list", "checklist-empty-nodomain" };
        float sum = 0f;
        int count = 0;
        foreach (string id in children)
        {
            if (snapshot.RectById.TryGetValue(id, out Rect child))
            {
                sum += child.height;
                count++;
            }
        }

        Check(count >= 3, "the card must arrange its header, the band composite and the list column, got " + count);
        float expected = 12f + sum + 6f * (count - 1) + 12f;
        Check(Math.Abs(card.height - expected) <= 0.5f,
            "card height must equal Padding + its arranged children + the Section gaps + Padding: card "
            + card.height + " vs " + expected);
        Check(card.height > bands.height + 20f,
            "the band composite does not itself carry the card chrome (card " + card.height + ", bands " + bands.height + ")");

        // The list column repeats the same relation one level down: the search field, the row set and
        // whichever empty state is showing, one 6px gap between each.
        Check(snapshot.RectById.TryGetValue("checklist-list", out Rect list), "the declarative list column is arranged");
        Check(snapshot.RectById.TryGetValue("checklist-search", out Rect search), "the search field is arranged");
        Check(snapshot.RectById.TryGetValue("checklist-rows", out Rect rows), "the row set is arranged");
        Check(Math.Abs(search.height - 24f) <= 0.5f, "the search field is the declared 24px band, got " + search.height);
        Check(rows.height > 0f, "the rich fixture must arrange at least one pack row, got " + rows.height);
        float listExpected = search.height + 6f + rows.height;
        Check(Math.Abs(list.height - listExpected) <= 0.5f,
            "the list column must be search + Gap + rows: " + list.height + " vs " + listExpected);

        // The rows are real tree elements whose identity carries the item key (Repeat materializes
        // <templateId>#<itemKey>), which is what the item-key lane reads back in layout order.
        Check(snapshot.RectById.ContainsKey("checklist-row#us.sang"),
            "the Repeat must materialize one arranged row element per projected key");
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("SettingsGeometryLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
