using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// Harness lane for the diagnostics panel (the 建满 set at the seams the engine owns):
/// fake snapshot source injection, both generated pages validated by the REAL production host,
/// view-state writes routing + bumping the session clock, row-click/lock actions routing, the
/// collapse reflow, the four-state presentation on a DRAWN frame, the provenance split (current
/// observation vs previous evaluation), the responsive list/detail navigation, and a planted
/// projection failure landing in the session guard's recovery instead of escaping the frame.
/// Off-screen lock tracking itself is pinned by the pure model lane (SqueakDiagnostics-
/// SessionModel); the window-level teardown interlock (main close cascades, close = unlock) is
/// the maintainer live-walkthrough item - WindowStack is not stubbable at this seam.
/// </summary>
internal static class DiagnosticsPanelLaneTests
{
    /// <summary>UsAttention.Marker, spelled through the public seam the production source uses.</summary>
    private const string Marker = UniversalSqueaker.UsAttention.Marker;

    public static void RunAll()
    {
        Step("main host validates the generated master-detail spec", MainHostCreation);
        Step("detail host validates the lock page", DetailHostCreation);
        Step("view writes route to the source and bump the session clock", WritesRouteAndBump);
        Step("row click and lock actions route through the source", ActionsRoute);
        Step("collapse swaps columns and the bar on the session clock", CollapseReflow);
        Step("collapsed bar answers the three questions (09 §3.3)", CollapsedBarAnswers);
        Step("the current summary and the grouped chain draw in one frame", SummaryAndGroupedChain);
        Step("raw values fold by default", RawFoldsByDefault);
        Step("the bar's close action routes as a page request", CloseRoutes);
        Step("a glyph the font cannot draw becomes its word (D7)", MissingGlyphFallsBack);
        Step("one attention marker per blocked row, none inside a value", GateBlockMarkAppearsOnceOnScreen);
        Step("the block rail is drawn with the attention brush, not Warning", BlockRailUsesAttentionBrush);
        Step("the blocked status text itself is painted attention-cyan", BlockStatusTextIsAttentionCyan);
        Step("N/A is neutral and Pending never reads as not reached", NaAndPendingAreNeutral);
        Step("a previous failure is never promoted into the current summary", PreviousFailureStaysPrevious);
        Step("narrow width switches to in-window navigation with a working Back", NarrowNavigation);
        Step("the pinned detail page can never grow a Back control", PinnedDetailHasNoBack);
        Step("the detail value column fits the measured Chinese value", ValueColumnFitsMeasuredChinese);
        Step("all 16 conditions show, and a group folds only when clicked", GroupFold);
        Step("throwing projection lands in guard recovery, frame survives", ThrowingSourceRecovers);
        Step("the trip guard fails a planted trip (positive control)", TripGuardFailsAPlantedTrip);
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("DiagnosticsPanelLane step failed: " + name, ex);
        }
    }

    /// <summary>
    /// A fake snapshot source. It mirrors the PRODUCTION derivations (narrow = the projection's own
    /// content-width rule, active tab = the projection's token, Back only in the narrow detail view) so
    /// a spec that stopped agreeing with the source's constants reddens the lane instead of the game.
    /// </summary>
    private sealed class FakeDiagnosticsSource : IUsDiagnosticsSource
    {
        public int RevisionValue = 5;
        public bool ThrowOnDetail;
        public int LastClickedPawnId = -1;
        public int LockCalls;
        public bool Ready { get; set; }

        /// <summary>The arranged content width the shell reports; the fake's narrow state derives from it.</summary>
        public float ContentWidth = 680f;

        /// <summary>Planted chain facts, so a step can drive any of the 16 rows without a game.</summary>
        public UsDiagGateFacts Facts = new();

        public List<UsDiagRow> Rows = new();

        public int Revision => RevisionValue;
        public bool IsValid { get; set; } = true;
        public bool ShowSeconds { get; set; }
        public bool Collapsed { get; set; }
        public string SearchQuery { get; set; } = string.Empty;
        public int Page { get; set; }

        public void SetContentWidth(float width) => ContentWidth = width;
        public bool Narrow => UsDiagnosticsProjection.IsNarrowPresentation(ContentWidth);
        public UsDiagNavView NavigationView { get; set; } = UsDiagNavView.List;
        public bool ShowBackControl => Narrow && NavigationView == UsDiagNavView.Detail;

        public IReadOnlyList<UsDiagRow> PageRows => Rows;
        public int TotalRowCount => Rows.Count;
        public int PageCount => UsDiagnosticsProjection.PageCount(Rows.Count);

        public UsDiagDetail? Detail
        {
            get
            {
                if (ThrowOnDetail)
                {
                    throw new InvalidOperationException("planted projection failure");
                }

                List<UsDiagGateLine> gates = UsDiagnosticsProjection.BuildGateChain(Facts, Tr, Marker);
                return new UsDiagDetail(
                    "Violet (Human)",
                    Ready,
                    false,
                    "Work",
                    "[XenotypePack·craftsmen-xeno] : Squeak_Happy_03",
                    gates,
                    UsDiagnosticsProjection.BuildCurrentSummary(gates, Tr),
                    UsDiagnosticsProjection.BuildPreviousBand(Facts.Previous, Tr));
            }
        }

        public UsDiagRow? MonitorRow => Rows.Count > 0 ? Rows[0] : null;

        /// <summary>The bar's three answers, projected from the same fake rows the list uses.</summary>
        public UsDiagBarModel? Bar => UsDiagnosticsProjection.BuildBar(
            new UsDiagBarFacts
            {
                TotalTargets = Rows.Count,
                HasMonitorRow = Rows.Count > 0,
                PawnText = Rows.Count > 0 ? Rows[0].PawnText : string.Empty,
                ActionText = Rows.Count > 0 ? Rows[0].ActionText : string.Empty,
                AudioText = Rows.Count > 0 ? Rows[0].AudioText : string.Empty,
                MonitorTone = Rows.Count > 0 ? Rows[0].Tone : UsDiagDotTone.Unknown,
                HasLastEvent = true,
                LastEventTimeText = "12:04:09",
                MinutesSinceLastEvent = 0,
            },
            Tr);

        /// <summary>Per-group fold state, all open by default (the ruling's default).</summary>
        public readonly bool[] GroupOpen = { true, true, true };

        public bool IsGroupOpen(UsDiagGateGroup group) => GroupOpen[(int)group];

        public void SetGroupOpen(UsDiagGateGroup group, bool open) => GroupOpen[(int)group] = open;

        public bool RawOpen { get; set; }
        public bool CloseRequested { get; private set; }
        public void RequestClose() => CloseRequested = true;

        public bool CanLockDisplayed { get; set; } = true;

        public void ClickRow(int pawnId) => LastClickedPawnId = pawnId;
        public void LockDisplayed() => LockCalls++;

        /// <summary>
        /// The format-shaped half of the translation stub: the sentences carry their placeholders in the
        /// translated value (the language files hold them), so a lane that returned the bare key
        /// everywhere would assert nothing about substitution.
        /// </summary>
        internal static string Tr(string key) => key switch
        {
            "US.Diagnostics.Bar.Scale" => "{0} targets",
            "US.Diagnostics.Bar.IdentityLocked" => "Sound log (locked: {0})",
            "US.Diagnostics.Bar.Activity.Recent" => "Last {0} {1} {2}",
            "US.Diagnostics.Bar.Activity.Stale" => "{0} min without a sound, last {1}",
            "US.Diagnostics.Summary.CurrentBlock" => "Current known block: {0}",
            "US.Diagnostics.Summary.Undetermined" => "Undetermined here: {0}",
            "US.Diagnostics.Summary.NoCurrentBlock" => "No block found in current observations",
            "US.Diagnostics.Previous.Evaluation" => "Previous evaluation: {0} · {1} · {2}",
            "US.Diagnostics.Previous.Dispatch" => "Last dispatch: {0} · {1}",
            "US.Diagnostics.Recency.JustNow" => "at {0}, less than a minute ago",
            "US.Diagnostics.Recency.Ago" => "at {0}, {1} min ago",
            _ => key,
        };
    }

    private static void FillRows(FakeDiagnosticsSource fake, int count)
    {
        fake.Rows = new List<UsDiagRow>();
        for (int i = 0; i < count; i++)
        {
            fake.Rows.Add(new UsDiagRow(
                1000 + i,
                i % 3 == 0 ? UsDiagDotTone.Blocked : (i % 3 == 1 ? UsDiagDotTone.Pending : UsDiagDotTone.Ready),
                "Pawn" + i + " (Human)",
                "Work",
                "120/600t 0/216t",
                "[RacePack·k" + i + "] : s" + i,
                i % 3 == 2,
                i == 0));
        }
    }

    private static void MainHostCreation()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 8);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);

        Assert(host.Manifest.SchemaVersion == "2", "the generated main spec is Schema=2");
        Assert(host.Session.IsActive, "session active after creation");
        Assert(host.Bindings.TryGet(UsDiagnosticsHost.KeyRows, out IReadOnlyList<UsDiagRow>? rows) && rows != null && rows.Count == 8,
            "typed diag-rows binding reads the injected fake source (假快照源注入)");
        Assert(host.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) && detail != null
                && detail.Gates.Count == 16,
            "detail binding projects the 16-line chain through the real host");
    }

    private static void DetailHostCreation()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 1);
        using UiHost host = UsDiagnosticsHost.CreateDetail(fake);
        Assert(host.Session.IsActive, "lock page host builds");
        host.DrawChecked(new Rect(0f, 0f, 320f, 480f));
        Assert(host.Session.IsActive, "lock page survives a full frame");
    }

    private static void WritesRouteAndBump()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 20);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        IUiBindings bindings = host.Bindings;

        int rev = host.Session.ContentRevision;
        bindings.Set(UsDiagnosticsHost.KeySeconds, true);
        Assert(fake.ShowSeconds, "s/t write routes to the source");
        Assert(host.Session.ContentRevision == rev + 1, "s/t write bumps the session clock (display-write contract)");

        rev = host.Session.ContentRevision;
        bindings.Set(UsDiagnosticsHost.KeySearch, "vi");
        Assert(fake.SearchQuery == "vi", "search write routes");
        Assert(host.Session.ContentRevision == rev + 1, "search bumps (row set changed)");

        rev = host.Session.ContentRevision;
        bindings.Set(UsDiagnosticsHost.KeyPage, 2);
        Assert(fake.Page == 2, "page write routes");
        Assert(host.Session.ContentRevision == rev + 1, "page bumps");

        rev = host.Session.ContentRevision;
        bindings.Set(UsDiagnosticsHost.KeyCollapsed, true);
        Assert(fake.Collapsed, "collapse write routes");
        Assert(host.Session.ContentRevision == rev + 1, "collapse bumps (measure switches)");

        rev = host.Session.ContentRevision;
        bindings.Set(UsDiagnosticsHost.KeyNavView, UsDiagNavView.Detail);
        Assert(fake.NavigationView == UsDiagNavView.Detail, "the narrow view write routes to the source");
        Assert(host.Session.ContentRevision == rev + 1, "and bumps: the view switch changes the arranged page");
    }

    private static void ActionsRoute()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 8);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);

        host.Bindings.Invoke(UsDiagnosticsHost.KeyRowClick, 1004);
        Assert(fake.LastClickedPawnId == 1004, "row click routes the pawn id (drill-in/lock decision lives in the source)");

        host.Bindings.Invoke(UsDiagnosticsHost.KeyLock, 0);
        Assert(fake.LockCalls == 1, "toolbar lock routes to LockDisplayed");
    }

    private static void CollapseReflow()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 8);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        Vector2 size = new(680f, 560f); // the panel's round-10 geometry (620 -> 680, lead-authorised)

        UiLayoutSnapshot open = host.MeasureAndArrange(size);
        Assert(open.RectById.TryGetValue("diag-list", out Rect listRect) && listRect.height > 0f,
            "expanded: list column has height");
        Assert(!open.RectById.TryGetValue("diag-bar", out Rect barOpen) || barOpen.height <= 0f,
            "expanded: the bar measures zero");

        host.Bindings.Set(UsDiagnosticsHost.KeyCollapsed, true);
        UiLayoutSnapshot bar = host.MeasureAndArrange(size);
        Assert(bar.RectById.TryGetValue("diag-bar", out Rect barRect) && barRect.height > 0f,
            "collapsed: the bar has height (same session, one binding flip - the bar ruling)");
        Assert(Math.Abs(barRect.width - size.x) <= 0.5f,
            "collapsed: the bar spans the full page width, got " + barRect.width + " (the defect was a bar squeezed into the 250px list column)");
        Assert((bar.RectById.TryGetValue("diag-list", out Rect listBar) ? listBar.height : 0f) <= 0f,
            "collapsed: list column measures zero");
    }

    private static void CollapsedBarAnswers()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 8);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        host.Bindings.Set(UsDiagnosticsHost.KeyCollapsed, true);
        host.MeasureAndArrange(new Vector2(680f, 560f));

        Assert(host.Bindings.TryGet(UsDiagnosticsHost.KeyBar, out UsDiagBarModel? bar) && bar != null,
            "the collapsed bar reads its model through the real host (typed binding, not a paint-side lookup)");
        Assert(bar!.Identity.Length > 0, "what is this: the bar carries an identity segment");
        Assert(bar.SwitchText.Length > 0, "is it on: the bar carries a switch segment");
        Assert(bar.Activity.Length > 0, "what is it doing: the bar carries an activity sentence");
        Assert(bar.Scale.Contains("8"), "and the tracking scale, got " + bar.Scale);
        Assert(bar.Activity.Contains("12:04:09"), "the activity sentence carries the event time, got " + bar.Activity);
    }

    private static void SummaryAndGroupedChain()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 8);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        Assert(host.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) && detail != null,
            "the detail binding still projects a model");
        Assert(detail!.Summary.Headline.Length > 0, "the expanded state leads with the current-only summary");
        Assert(detail.Previous.Heading.Length > 0, "and keeps the previous-event band");

        int game = 0, rules = 0, audio = 0;
        foreach (UsDiagGateLine gate in detail.Gates)
        {
            if (gate.Group == UsDiagGateGroup.Game) game++;
            else if (gate.Group == UsDiagGateGroup.Rules) rules++;
            else audio++;
        }

        Assert(game == 4 && rules == 7 && audio == 5, "all three sides survive the projection, got " + game + "/" + rules + "/" + audio);

        host.DrawChecked(new Rect(0f, 0f, 680f, 560f));
        Assert(host.Session.IsActive, "the summary and the grouped chain draw inside a full real frame");
    }

    private static void RawFoldsByDefault()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 8);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        Assert(!fake.RawOpen, "raw values start folded: evidence never leads");

        UiLayoutSnapshot folded = host.MeasureAndArrange(new Vector2(680f, 560f));
        // Through the binding: the fold is page state on the same session clock as every other write.
        host.Bindings.Set(UsDiagnosticsHost.KeyRawOpen, true);
        Assert(fake.RawOpen, "the fold write routes to the source");
        UiLayoutSnapshot open = host.MeasureAndArrange(new Vector2(680f, 560f));
        Assert(open.RectById["diag-detail"].height > folded.RectById["diag-detail"].height,
            "unfolding the raw block adds its two rows to the detail column");
    }

    private static void CloseRoutes()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 8);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        host.Bindings.Invoke(UsDiagnosticsHost.KeyClose);
        Assert(fake.CloseRequested,
            "the bar's close action sets a page-level request; the window, not the widget, closes");
    }

    /// <summary>
    /// The in-game defect, pinned where a player saw it: on the drawn frame. Each blocked condition row
    /// carries the attention marker EXACTLY once, no other row carries one, and the raw value never
    /// contains it (the marker lives in the separate status cell).
    /// </summary>
    private static void GateBlockMarkAppearsOnceOnScreen()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 4);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        host.MeasureAndArrange(new Vector2(680f, 560f));

        int blockRows = 0;
        List<string> blockValues = new();
        if (host.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) && detail != null)
        {
            foreach (UsDiagGateLine gate in detail.Gates)
            {
                if (gate.State != UsDiagGateState.Block) continue;
                blockRows++;
                blockValues.Add(gate.Value);
            }
        }

        Assert(blockRows > 1, "the fake's chain blocks in more than one place, so the sweep below is meaningful");

        IList texts = RecordedStub("LabelTexts");
        int textStart = texts.Count;
        host.DrawChecked(new Rect(0f, 0f, 680f, 560f));

        // The detail must actually have drawn. A tripping widget is swapped for the recovery band, which
        // would leave every assertion below vacuously green - the shape that hid this defect.
        Assert(host.Session.TrippedNodes.Count == 0,
            "the frame drew the gate chain through the guard without tripping (" + host.Session.TrippedNodes.Count + " tripped)");

        int marked = 0;
        List<string> drawn = new();
        for (int i = textStart; i < texts.Count; i++)
        {
            string text = (string)texts[i]!;
            drawn.Add(text);
            int marks = CountOccurrences(text, Marker);
            Assert(marks <= 1, "no drawn cell repeats the marker (the in-game defect): '" + text + "'");
            if (marks == 1) marked++;
        }

        Assert(marked == blockRows,
            "every blocked row is marked exactly once and nothing else is, got " + marked + " of " + blockRows);

        // And no VALUE cell carries the marker: the marker must live in the status cell alone. The test is
        // "contains", not "equals", so a painter that concatenated the status onto the value - the exact
        // regression this checks for - cannot slip through as a non-matching string.
        foreach (string value in blockValues)
        {
            if (value.Length == 0) continue;
            foreach (string text in drawn)
            {
                if (text.IndexOf(value, StringComparison.Ordinal) < 0) continue;
                Assert(CountOccurrences(text, Marker) == 0,
                    "a cell carrying a blocked row's value must not also carry the marker, got '" + text + "'");
            }
        }
    }

    /// <summary>
    /// The block rail must carry US's attention brush. Asserting against <c>UsAttention.Brush</c> - not
    /// <c>UiTheme.Warning</c> - is the difference between proving the cyan reached the frame and proving
    /// that the Danger-aliased token was named: a rail painted gold or danger-red fails here.
    /// </summary>
    private static void BlockRailUsesAttentionBrush()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 4);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        host.MeasureAndArrange(new Vector2(680f, 560f));

        IList rects = RecordedStub("DrawBoxSolidRects");
        IList colors = RecordedStub("DrawBoxSolidColors");
        int boxStart = colors.Count;
        host.DrawChecked(new Rect(0f, 0f, 680f, 560f));

        UiTheme hostTheme = host.CreateContext(1f).Theme;
        int attentionRails = 0;
        int warningRails = 0;
        int dividerRails = 0;
        for (int i = boxStart; i < rects.Count && i < colors.Count; i++)
        {
            Rect rail = (Rect)rects[i]!;
            Color colour = (Color)colors[i]!;
            if (SameColor(colour, hostTheme.Divider) && Math.Abs(rail.width - 2f) <= 0.01f) dividerRails++;
            if (!SameColor(colour, UniversalSqueaker.UsAttention.Brush)) continue;
            if (Math.Abs(rail.width - 2f) <= 0.01f) attentionRails++;
            if (SameColor(colour, hostTheme.Warning)) warningRails++;
        }

        // The two names must be different colours, or this whole step is a tautology on this theme.
        Assert(!SameColor(UniversalSqueaker.UsAttention.Brush, hostTheme.Warning),
            "the attention brush and the Warning/Danger alias are different colours on this theme");
        Assert(attentionRails == 1,
            "exactly one rail carries UsAttention.Brush (the first CURRENT block), got " + attentionRails);
        Assert(warningRails == 0, "and no rail uses the Danger-aliased Warning token, got " + warningRails);
        Assert(dividerRails >= 1,
            "the other chain rows keep the plain group rail that carries the same brush where the band is, got " + dividerRails);
    }

    /// <summary>
    /// G1: the blocked STATUS TEXT - the marker plus the state word - must be painted with the attention
    /// brush on the real frame. The rail check alone let a mutation paint the status word gold pass
    /// green, so this reads the drawn label colour back and guards it against the accent gold and the
    /// whole danger family (Warning is the carrier's alias of Danger).
    /// </summary>
    private static void BlockStatusTextIsAttentionCyan()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 4);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        host.MeasureAndArrange(new Vector2(680f, 560f));

        int blockRows = 0;
        if (host.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) && detail != null)
        {
            foreach (UsDiagGateLine gate in detail.Gates)
            {
                if (gate.State == UsDiagGateState.Block) blockRows++;
            }
        }

        Assert(blockRows > 1, "the fixture blocks in more than one row, so the sweep is not a one-row special case");

        IList texts = RecordedStub("LabelTexts");
        IList colors = RecordedStub("LabelColors");
        int start = texts.Count;
        host.DrawChecked(new Rect(0f, 0f, 680f, 560f));

        UiTheme theme = host.CreateContext(1f).Theme;
        Assert(!SameColor(UniversalSqueaker.UsAttention.Brush, theme.AccentGold)
                && !SameColor(UniversalSqueaker.UsAttention.Brush, theme.Danger)
                && !SameColor(UniversalSqueaker.UsAttention.Brush, theme.Warning),
            "the theme really distinguishes attention from gold and danger, or this step is a tautology");

        string statusText = Marker + " US.Diagnostics.Gate.BlockMark";
        int found = 0;
        for (int i = start; i < texts.Count && i < colors.Count; i++)
        {
            if (!string.Equals((string)texts[i]!, statusText, StringComparison.Ordinal)) continue;
            found++;
            Color colour = (Color)colors[i]!;
            Assert(SameColor(colour, UniversalSqueaker.UsAttention.Brush),
                "a blocked status cell is painted with the attention brush, got " + colour);
            Assert(!SameColor(colour, theme.AccentGold),
                "never the accent gold - the ruling's named acceptance clause, got " + colour);
            Assert(!SameColor(colour, theme.Danger) && !SameColor(colour, theme.Warning),
                "and never the danger family, got " + colour);
        }

        Assert(found == blockRows,
            "every blocked row's status cell was drawn and checked, got " + found + " of " + blockRows);
    }

    /// <summary>
    /// The four-state contract on the drawn frame: an N/A status cell is never pass-styled, a Pending
    /// status cell is never the selection accent, and the copy never claims "not reached".
    /// </summary>
    private static void NaAndPendingAreNeutral()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 4);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        host.MeasureAndArrange(new Vector2(680f, 560f));

        IList texts = RecordedStub("LabelTexts");
        IList colors = RecordedStub("LabelColors");
        int textStart = texts.Count;
        host.DrawChecked(new Rect(0f, 0f, 680f, 560f));

        UiTheme theme = host.CreateContext(1f).Theme;
        Assert(!SameColor(theme.Success, theme.TextSecondary) && !SameColor(theme.Selected, theme.TextSecondary)
                && !SameColor(theme.Success, theme.Selected),
            "the theme really distinguishes pass/pending/neutral ink, or the checks below are vacuous");

        int naDrawn = 0;
        int pendingDrawn = 0;
        for (int i = textStart; i < texts.Count && i < colors.Count; i++)
        {
            string text = (string)texts[i]!;
            Color colour = (Color)colors[i]!;
            if (string.Equals(text, "US.Diagnostics.Value.NotApplicable", StringComparison.Ordinal))
            {
                naDrawn++;
                Assert(!SameColor(colour, theme.Success), "N/A must never use the pass colour");
                Assert(SameColor(colour, theme.TextSecondary), "N/A reads as neutral ink");
            }
            else if (string.Equals(text, "US.Diagnostics.Value.Undetermined", StringComparison.Ordinal))
            {
                pendingDrawn++;
                Assert(!SameColor(colour, theme.Selected), "Pending must never use the selection accent");
                Assert(SameColor(colour, theme.TextSecondary), "Pending reads as neutral ink");
            }

            Assert(text.IndexOf("not reached", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("NotReached", StringComparison.Ordinal) < 0,
                "no drawn copy claims a condition was not reached: '" + text + "'");
        }

        Assert(naDrawn > 0 && pendingDrawn > 0,
            "the frame really drew both an N/A and a Pending status cell (na=" + naDrawn + ", pending=" + pendingDrawn + ")");
    }

    /// <summary>
    /// The adversarial case from the ruling: no current block at all, a remembered evaluation failure.
    /// The summary must stay honest and the failure must appear only in the previous-event band.
    /// </summary>
    private static void PreviousFailureStaysPrevious()
    {
        var fake = new FakeDiagnosticsSource
        {
            Facts = new UsDiagGateFacts
            {
                OnMap = true,
                OnScreen = true,
                HasTimingAction = true,
                ActionEnabled = true,
                ActionCooldownPass = true,
                GlobalApplicable = true,
                GlobalPass = true,
                VocalPass = true,
                EvaluationBelongsToCurrentAction = true,
                EligibilityRejected = true,
                Previous = new UsDiagPreviousFacts
                {
                    HasEvaluation = true,
                    EvaluationOutcome = "EligibilityRejected",
                    EvaluationAction = "work",
                    EvaluationTick = 90000,
                    NowTick = 90120,
                },
            },
        };
        FillRows(fake, 4);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        host.MeasureAndArrange(new Vector2(680f, 560f));

        Assert(host.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) && detail != null, "detail projects");
        Assert(detail!.Gates[14].State == UsDiagGateState.Block, "the fixture really does have a previous-event failure");
        Assert(!detail.Summary.HasCurrentBlock && detail.Summary.Headline == "No block found in current observations",
            "the summary headline never promotes it, got '" + detail.Summary.Headline + "'");
        Assert(detail.Previous.EvaluationLine.Contains("EligibilityRejected"),
            "and the band is where it is reported, got '" + detail.Previous.EvaluationLine + "'");

        IList texts = RecordedStub("LabelTexts");
        int textStart = texts.Count;
        host.DrawChecked(new Rect(0f, 0f, 680f, 560f));

        bool headlineDrawn = false;
        bool previousDrawn = false;
        for (int i = textStart; i < texts.Count; i++)
        {
            string text = (string)texts[i]!;
            if (text.Contains("No block found in current observations")) headlineDrawn = true;
            if (text.Contains("Previous evaluation:")) previousDrawn = true;
            Assert(text.IndexOf("Current known block:", StringComparison.Ordinal) < 0,
                "no drawn copy claims a current block here: '" + text + "'");
        }

        Assert(headlineDrawn && previousDrawn, "both the honest headline and the previous band reached the frame");
    }

    /// <summary>
    /// 09 §3.5: below the measured split threshold the page must switch to the in-window list/detail
    /// navigation, expose a working Back, and leave the search text and page exactly where they were.
    /// The switch is the TWO mutually exclusive presentations of <see cref="UsDiagnosticsSpec.MainXml"/>
    /// behind their VisibleKeys, not a column that collapses to a pixel (FL 0.7 A1 retired that idiom),
    /// so each half of the boundary is asserted by the OTHER presentation owning no arranged geometry.
    /// </summary>
    private static void NarrowNavigation()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 12);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);

        // The spec must spell the host's own keys: an unresolvable VisibleKey stays VISIBLE, so a drift
        // between the two would show both presentations at once. This is the positive control for it.
        Assert(UsDiagnosticsSpec.MainXml.IndexOf("VisibleKey=\"" + UsDiagnosticsHost.KeyWide + "\"", StringComparison.Ordinal) >= 0
                && UsDiagnosticsSpec.MainXml.IndexOf("VisibleKey=\"" + UsDiagnosticsHost.KeyNarrow + "\"", StringComparison.Ordinal) >= 0,
            "the main spec binds both presentations to the host's contract keys");
        Assert(UsDiagnosticsSpec.MainXml.IndexOf("NarrowHidden", StringComparison.Ordinal) < 0,
            "the retired idiom is gone: a presentation is hidden whole, never left as a pixel-wide remnant");

        // Warm the arrangement once at the shipped wide width. The FIRST arrange also folds the
        // injected theme's layout revision into the clock (UiHost's own one-time cache invalidation),
        // and that movement is not part of what this step measures; the repeated-regime control below
        // then proves the clock is otherwise quiet, so the flip's delta is the flip's alone.
        HostArrangeAt(680f);

        // This width is half a pixel under the threshold. Crossing it must move the layout clock exactly
        // once: the VisibleKey binding is read-only, so ApplyContentWidth is the ONLY thing that
        // re-arranges the page when the width flips - the engine has no Breakpoint swap doing it for us.
        const float narrowWidth = UsDiagnosticsProjection.NarrowBreakpoint + UsDiagnosticsProjection.PagePadding * 2f - 0.5f;
        int revBeforeFlip = host.Session.ContentRevision;
        UiLayoutSnapshot narrow = HostArrangeAt(narrowWidth);
        Assert(fake.Narrow, "the source's presentation decision is taken from the fed content width");
        Assert(host.Session.ContentRevision == revBeforeFlip + 1,
            "crossing the presentation threshold moves the layout clock exactly once, got "
            + (host.Session.ContentRevision - revBeforeFlip));
        int revAfterFlip = host.Session.ContentRevision;
        HostArrangeAt(narrowWidth);
        Assert(host.Session.ContentRevision == revAfterFlip,
            "re-feeding a width in the same regime moves nothing, got +"
            + (host.Session.ContentRevision - revAfterFlip));
        Assert(host.Bindings.TryGetBool(UsDiagnosticsHost.KeyWide, out bool wideKey) && !wideKey
                && host.Bindings.TryGetBool(UsDiagnosticsHost.KeyNarrow, out bool narrowKey) && narrowKey,
            "and the two keys answer the complement of each other in this shape");
        Assert(!narrow.RectById.ContainsKey("diag-list") && !narrow.RectById.ContainsKey("diag-detail-scroll"),
            "narrow: the wide master and detail subtrees are not arranged at all (diag-list height "
            + Height(narrow, "diag-list") + ")");
        Assert(Height(narrow, "diag-nav-body") > 0f && Width(narrow, "diag-nav-col") > 200f,
            "narrow: the navigation column shows the list and owns the width");

        // Search + page are established BEFORE the navigation switch.
        host.Bindings.Set(UsDiagnosticsHost.KeySearch, "vi");
        host.Bindings.Set(UsDiagnosticsHost.KeyPage, 2);
        int revBefore = host.Session.ContentRevision;

        // Selecting a row opens the detail view through the SAME write the source uses.
        host.Bindings.Set(UsDiagnosticsHost.KeyNavView, UsDiagNavView.Detail);
        Assert(fake.NavigationView == UsDiagNavView.Detail, "the view switch routes");
        Assert(host.Session.ContentRevision == revBefore + 1, "and bumps the layout clock once");
        UiLayoutSnapshot detailView = Arrange(narrowWidth, 560f);
        Assert(Height(detailView, "diag-nav-body") > Height(narrow, "diag-nav-body"),
            "narrow detail: the same body element now measures the taller detail content, got "
            + Height(detailView, "diag-nav-body") + " vs " + Height(narrow, "diag-nav-body") + " for the list");

        IList texts = RecordedStub("LabelTexts");
        int textStart = texts.Count;
        host.DrawChecked(new Rect(0f, 0f, narrowWidth, 560f));
        bool backDrawn = false;
        for (int i = textStart; i < texts.Count; i++)
        {
            if (((string)texts[i]!).IndexOf("US.Diagnostics.Nav.Back", StringComparison.Ordinal) >= 0) backDrawn = true;
        }

        Assert(backDrawn, "the narrow detail view draws a Back control");
        Assert(Height(detailView, "diag-nav-back") > 0f, "and it has real geometry, not a zero-height ghost");

        // Back returns to the list and restores the SAME search text, page and list position. The list
        // position is session state on the list Scroll's own node, so it must survive the round trip -
        // and the switch is driven by CLICKING the drawn control rather than by writing the binding, so a
        // Back handler that also cleared the search or reset the page reddens this step.
        const float shortHeight = 160f;
        host.Bindings.Set(UsDiagnosticsHost.KeyNavView, UsDiagNavView.List);
        Arrange(narrowWidth, shortHeight);
        UiNode? listScroll = host.Session.GetNodeByElementId("diag-nav-scroll");
        Assert(listScroll != null, "the narrow list scroll has a node to own its position");
        host.Session.SetScrollPosition(listScroll!, new Vector2(0f, 60f));
        Arrange(narrowWidth, shortHeight);
        float before = host.Session.GetScrollPosition(listScroll!).y;
        Assert(before > 1f, "the fixture really scrolled (a clamped-to-zero offset would make the check vacuous), got " + before);

        // Show the detail view - the state whose Back control is under test - and click ITS drawn control.
        host.Bindings.Set(UsDiagnosticsHost.KeyNavView, UsDiagNavView.Detail);
        Arrange(narrowWidth, shortHeight);
        Rect shortViewport = new(0f, 0f, narrowWidth, shortHeight);
        int backStart = texts.Count;
        host.DrawChecked(shortViewport);
        Assert(TryFindLabelRect(texts, RecordedStub("LabelRects"), backStart, "US.Diagnostics.Nav.Back", out Rect backLabel),
            "the drawn Back control is on the frame before it is clicked");
        int revAtClick = host.Session.ContentRevision;

        ClickRect(host, shortViewport, backLabel);

        Assert(fake.NavigationView == UsDiagNavView.List, "clicking the drawn Back control switches the view back");
        Assert(host.Session.ContentRevision == revAtClick + 1,
            "and that click is exactly one display write - a handler that also cleared state would bump more, got "
            + (host.Session.ContentRevision - revAtClick));
        Assert(fake.SearchQuery == "vi", "and the search text is exactly what it was, got '" + fake.SearchQuery + "'");
        Assert(fake.Page == 2, "and the page is exactly where it was, got " + fake.Page);
        UiLayoutSnapshot back = Arrange(narrowWidth, shortHeight);
        Assert(Height(back, "diag-nav-body") > 0f, "the list view is arranged again");
        Assert(Math.Abs(host.Session.GetScrollPosition(listScroll!).y - before) < 0.5f,
            "and the list position survived the round trip, got " + host.Session.GetScrollPosition(listScroll!).y + " vs " + before);

        // The wide side of the same boundary: the master/detail split is the arranged shape, and the
        // narrow presentation owns NO geometry there - not a 1px stub, and not a share of the wide
        // Row's leftover space (the A1 defect: the unmeasurable Auto child joined the unsized
        // distribution and took ~96px of the exactly-filled 680-wide Row).
        int revBeforeWide = host.Session.ContentRevision;
        UiLayoutSnapshot wide = HostArrangeAt(680f);
        Assert(host.Session.ContentRevision == revBeforeWide + 1,
            "crossing back to the wide presentation moves the clock exactly once, got "
            + (host.Session.ContentRevision - revBeforeWide));
        Assert(Height(wide, "diag-list") > 0f && Height(wide, "diag-detail-scroll") > 0f,
            "at the shipped 680 width the master/detail split is the arranged shape");
        Assert(!wide.RectById.ContainsKey("diag-nav-col") && !wide.RectById.ContainsKey("diag-nav-body")
                && !wide.RectById.ContainsKey("diag-nav-scroll") && !wide.RectById.ContainsKey("diag-nav-back"),
            "and the narrow presentation is not arranged at all, so it costs the split no column (diag-nav-col width "
            + Width(wide, "diag-nav-col") + ")");
        Assert(host.Bindings.TryGetBool(UsDiagnosticsHost.KeyWide, out bool wideKeyAt680) && wideKeyAt680
                && host.Bindings.TryGetBool(UsDiagnosticsHost.KeyNarrow, out bool narrowKeyAt680) && !narrowKeyAt680,
            "the keys answer the complement of each other in this shape too");

        // Production's own width feed, not a hand-written SetContentWidth: the lane must fail if the
        // path the panel actually uses stops moving the layout clock.
        UiLayoutSnapshot Arrange(float width, float height)
        {
            UsDiagnosticsHost.ApplyContentWidth(fake, host, width);
            return host.MeasureAndArrange(new Vector2(width, height));
        }

        UiLayoutSnapshot HostArrangeAt(float width) => Arrange(width, 560f);
    }

    /// <summary>
    /// The pinned detail window has no owning list, so its page must not declare - and must never draw -
    /// a Back control that would have nowhere to go. Asserted on the spec AND on a drawn frame.
    /// </summary>
    private static void PinnedDetailHasNoBack()
    {
        Assert(UsDiagnosticsSpec.DetailXml.IndexOf("us/diag/nav", StringComparison.Ordinal) < 0,
            "the pinned page declares no nav widget");
        Assert(UsDiagnosticsSpec.DetailXml.IndexOf("Tab=", StringComparison.Ordinal) < 0,
            "and no tab-driven view switch (it has no second view)");
        Assert(UsDiagnosticsSpec.MainXml.IndexOf("us/diag/nav", StringComparison.Ordinal) >= 0,
            "positive control: the responsive main page does declare it");

        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 4);
        // Force the state that shows Back on the MAIN page, then render the PINNED page.
        fake.SetContentWidth(500f);
        fake.NavigationView = UsDiagNavView.Detail;
        Assert(fake.ShowBackControl, "positive control: in this state the main page would show Back");
        using UiHost host = UsDiagnosticsHost.CreateDetail(fake);
        host.MeasureAndArrange(new Vector2(320f, 480f));

        IList texts = RecordedStub("LabelTexts");
        int textStart = texts.Count;
        host.DrawChecked(new Rect(0f, 0f, 320f, 480f));
        for (int i = textStart; i < texts.Count; i++)
        {
            Assert(((string)texts[i]!).IndexOf("US.Diagnostics.Nav.Back", StringComparison.Ordinal) < 0,
                "the pinned detail frame never draws a Back control");
        }
    }

    /// <summary>
    /// The ruling's mutation target: narrow the detail value rect and this must fail. The check compares
    /// the DRAWN value cell against the measured need of the Chinese value drawn in it.
    /// </summary>
    private static void ValueColumnFitsMeasuredChinese()
    {
        const string chineseValue = "随机判定";
        var fake = new FakeDiagnosticsSource { Facts = new UsDiagGateFacts { ProbabilityValue = chineseValue } };
        FillRows(fake, 4);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);

        const float narrowWidth = 620f;
        fake.SetContentWidth(narrowWidth);
        fake.NavigationView = UsDiagNavView.Detail; // the narrow detail view is what draws the value cell
        host.MeasureAndArrange(new Vector2(narrowWidth, 560f));

        IList texts = RecordedStub("LabelTexts");
        IList rects = RecordedStub("LabelRects");
        int textStart = texts.Count;
        host.DrawChecked(new Rect(0f, 0f, narrowWidth, 560f));

        float needed = VerseFerriteTextMetrics.Instance.MeasureWidth(chineseValue, UiFont.Tiny);
        Assert(needed > 0f, "the ruler measures the value at all");

        float drawnWidth = -1f;
        for (int i = textStart; i < texts.Count && i < rects.Count; i++)
        {
            if (string.Equals((string)texts[i]!, chineseValue, StringComparison.Ordinal))
            {
                drawnWidth = Math.Max(drawnWidth, ((Rect)rects[i]!).width);
            }
        }

        Assert(drawnWidth > 0f, "the Chinese value was actually drawn (a missing cell would make this vacuous)");
        Assert(drawnWidth >= needed,
            "the value column (" + drawnWidth + "px) holds the measured Chinese value (" + needed + "px) at the narrow width");
    }

    /// <summary>
    /// 09 §1 decision 1 / §3.2: all 16 conditions are discoverable with the default (all open) groups,
    /// each group folds ONLY on the developer's click, and an update to the data never changes which
    /// groups are open - the stable-view rule.
    /// </summary>
    private static void GroupFold()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 4);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        host.MeasureAndArrange(new Vector2(680f, 560f));

        Assert(host.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) && detail != null, "detail projects");
        Assert(detail!.Gates.Count == 16, "the chain is still 16 conditions");
        foreach (UsDiagGateGroup group in new[] { UsDiagGateGroup.Game, UsDiagGateGroup.Rules, UsDiagGateGroup.Audio })
        {
            Assert(fake.IsGroupOpen(group), "group " + group + " starts open");
            Assert(host.Bindings.TryGet(UsDiagnosticsHost.GroupKey(group), out bool open) && open,
                "and its binding agrees, so the default is the page's own state");
        }

        IList texts = RecordedStub("LabelTexts");
        int start = texts.Count;
        host.DrawChecked(new Rect(0f, 0f, 680f, 560f));
        int drawnNames = 0;
        foreach (UsDiagGateLine gate in detail.Gates)
        {
            if (DrawnContains(texts, start, gate.Name)) drawnNames++;
        }

        Assert(drawnNames == 16, "all 16 conditions are discoverable with the groups open, got " + drawnNames);

        // Positive control for the name sweep: the game names really are in the drawn text.
        Assert(DrawnContains(texts, start, "US.Diagnostics.Gate.OnMap"), "the sweep finds a game-side name");
        Assert(DrawnContains(texts, start, "US.Diagnostics.Gate.Dispatch"), "and an audio-side name");

        float openHeight = Height(host.MeasureAndArrange(new Vector2(680f, 560f)), "diag-detail");
        Rect viewport = new(0f, 0f, 680f, 560f);

        // Fold the Audio group by CLICKING its heading through the engine's own hit outlet - the same
        // stub override the mood lane uses. A heading that stopped being a control reddens here.
        int clickStart = texts.Count;
        host.DrawChecked(viewport);
        Assert(TryFindLabelRect(texts, RecordedStub("LabelRects"), clickStart,
                "[-] US.Diagnostics.Gate.Group.Audio · US.Diagnostics.Basis.Current", out Rect openHeading),
            "the open Audio heading is drawn as a control and names its group");
        int revBefore = host.Session.ContentRevision;
        ClickRect(host, viewport, openHeading);
        Assert(!fake.IsGroupOpen(UsDiagGateGroup.Audio), "clicking the heading folds the group");
        Assert(host.Session.ContentRevision == revBefore + 1, "and the fold is one display write, not a repaint");

        UiLayoutSnapshot folded = host.MeasureAndArrange(viewport.size);
        Assert(Height(folded, "diag-detail") < openHeight,
            "a folded group takes its rows out of the page, got " + Height(folded, "diag-detail") + " vs " + openHeight);

        int start2 = texts.Count;
        host.DrawChecked(viewport);
        Assert(DrawnContains(texts, start2, "[+] US.Diagnostics.Gate.Group.Audio"),
            "the folded group keeps one heading that says so");
        foreach (string audioName in new[] { "US.Diagnostics.Gate.VocalOrgan", "US.Diagnostics.Gate.AudioPool", "US.Diagnostics.Gate.Playability", "US.Diagnostics.Gate.Dispatch" })
        {
            Assert(!DrawnContains(texts, start2, audioName),
                "a folded group draws none of its condition rows (" + audioName + " was still drawn)");
        }

        Assert(DrawnContains(texts, start2, "US.Diagnostics.Gate.OnMap") && DrawnContains(texts, start2, "US.Diagnostics.Gate.Startup"),
            "while the other groups keep drawing");

        // A data update must not move the developer's view: the state is page state, never derived.
        fake.Facts.OnMap = true;
        host.MeasureAndArrange(viewport.size);
        host.DrawChecked(viewport);
        Assert(!fake.IsGroupOpen(UsDiagGateGroup.Audio),
            "an update to the values never re-opens a folded group (no automatic collapse or expand)");
        Assert(fake.IsGroupOpen(UsDiagGateGroup.Game) && fake.IsGroupOpen(UsDiagGateGroup.Rules),
            "and never touches a group the developer did not fold");

        // And the same control opens it again.
        int reopenStart = texts.Count;
        host.DrawChecked(viewport);
        Assert(TryFindLabelRect(texts, RecordedStub("LabelRects"), reopenStart, "[+] US.Diagnostics.Gate.Group.Audio", out Rect closedHeading),
            "the folded heading is drawn as a control");
        ClickRect(host, viewport, closedHeading);
        Assert(fake.IsGroupOpen(UsDiagGateGroup.Audio), "clicking it again expands the group");
    }

    /// <summary>True when the label at or after <paramref name="from"/> matches, with its drawn rect.</summary>
    private static bool TryFindLabelRect(IList texts, IList rects, int from, string needle, out Rect rect)
    {
        for (int i = from; i < texts.Count && i < rects.Count; i++)
        {
            if (((string)texts[i]!).IndexOf(needle, StringComparison.Ordinal) < 0) continue;
            rect = (Rect)rects[i]!;
            return true;
        }

        rect = Rect.zero;
        return false;
    }

    /// <summary>
    /// One frame in which the control whose surface ENCLOSES <paramref name="labelRect"/> reports a
    /// click (the carrier's own test seam, <c>UiNative.ButtonOverride</c>).
    /// </summary>
    private static void ClickRect(UiHost host, Rect viewport, Rect labelRect)
    {
        SetButtonOverride(rect => Math.Abs(rect.y - labelRect.y) <= 0.5f
            && Math.Abs(rect.height - labelRect.height) <= 0.5f
            && rect.x <= labelRect.x + 0.5f
            && rect.xMax >= labelRect.xMax - 0.5f);
        try
        {
            host.DrawChecked(viewport);
        }
        finally
        {
            SetButtonOverride(null);
        }
    }

    private static void SetButtonOverride(Func<Rect, bool>? value)
    {
        FieldInfo? info = typeof(UiNative).GetField("ButtonOverride", BindingFlags.NonPublic | BindingFlags.Static);
        if (info == null)
        {
            throw new InvalidOperationException(
                "the carrier no longer exposes UiNative.ButtonOverride, so this lane cannot simulate a click; "
                + "the fold check must not be deleted to make that compile error go away");
        }

        info.SetValue(null, value);
    }

    private static bool DrawnContains(IList texts, int from, string needle)
    {
        for (int i = from; i < texts.Count; i++)
        {
            if (((string)texts[i]!).IndexOf(needle, StringComparison.Ordinal) >= 0) return true;
        }

        return false;
    }

    private static float Height(UiLayoutSnapshot snapshot, string id)
        => snapshot.RectById.TryGetValue(id, out Rect rect) ? rect.height : 0f;

    private static float Width(UiLayoutSnapshot snapshot, string id)
        => snapshot.RectById.TryGetValue(id, out Rect rect) ? rect.width : 0f;

    private static IList RecordedStub(string field)
    {
        FieldInfo? info = typeof(Verse.Widgets).GetField(field, BindingFlags.Public | BindingFlags.Static);
        if (info == null) throw new InvalidOperationException("the harness stub is missing " + field);
        return (IList)info.GetValue(null)!;
    }

    private static bool SameColor(Color left, Color right)
        => Math.Abs(left.r - right.r) <= 0.0001f && Math.Abs(left.g - right.g) <= 0.0001f
        && Math.Abs(left.b - right.b) <= 0.0001f && Math.Abs(left.a - right.a) <= 0.0001f;

    private static int CountOccurrences(string text, string token)
    {
        int count = 0;
        int index = 0;
        while (token.Length > 0 && index < text.Length)
        {
            int found = text.IndexOf(token, index, StringComparison.Ordinal);
            if (found < 0) break;
            count++;
            index = found + token.Length;
        }

        return count;
    }

    private static void MissingGlyphFallsBack()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 4);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        UiWidgetContext ctx = host.CreateContext(200f);

        string drawn = UsDiagGlyphs.DotText(ctx, UsDiagDotTone.Blocked, _ => true);
        string arrow = UsDiagGlyphs.PrevText(ctx, _ => true);
        string word = UsDiagGlyphs.DotText(ctx, UsDiagDotTone.Blocked, _ => false);
        string wordArrow = UsDiagGlyphs.PrevText(ctx, _ => false);

        Assert(drawn == UsDiagGlyphs.Dot && arrow == UsDiagGlyphs.Prev,
            "a drawable glyph is used as-is");
        Assert(word == UsDiagnosticsProjection.DotFallbackKey(UsDiagDotTone.Blocked),
            "a missing dot becomes the tone's state word, got '" + word + "'");
        Assert(word != "US.Diagnostics.Dot.Ready", "and the word is the tone's own, not a fixed placeholder");
        Assert(wordArrow == "US.Diagnostics.Pager.Prev",
            "a missing pager arrow becomes its word, got '" + wordArrow + "'");
    }

    private static void ThrowingSourceRecovers()
    {
        var fake = new FakeDiagnosticsSource { ThrowOnDetail = true };
        FillRows(fake, 8);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);

        host.MeasureAndArrange(new Vector2(620f, 560f));
        // This lane TRIPS ON PURPOSE, so the switch is explicit in the call rather than an omission - and it
        // is passed on every later frame too, because UiSession remembers a trip until it is disposed.
        host.DrawChecked(new Rect(0f, 0f, 620f, 560f), deliberateTrips: true);
        Assert(host.Session.IsActive,
            "a throwing detail projection is absorbed by the session guard (engine RecoveryBand), the frame survives");
        Assert(UsTripGuard.AnyTripped(host.Session),
            "and the recovery really happened: the element ended the frame in a recovery band");
        Assert(UsTripGuard.Describe(host.Session, "diag").Contains("planted projection failure"),
            "the trip log names what the element threw, so a silent recovery cannot hide the cause");
        fake.ThrowOnDetail = false;
        host.MeasureAndArrange(new Vector2(620f, 560f));
        // Still the declared lane: the detail element draws cleanly from here on, but the session keeps the
        // earlier trip in TrippedNodes, so this is the same deliberate declaration, not a new one.
        host.DrawChecked(new Rect(0f, 0f, 620f, 560f), deliberateTrips: true);
        Assert(host.Session.IsActive, "and the same host keeps working after the failure clears");
        Assert(UsTripGuard.AnyTripped(host.Session),
            "the session keeps the trip recorded after the failing source is cleared, so a later frame is "
            + "still the same declared lane rather than a fresh claim");
    }

    /// <summary>
    /// Positive control for <see cref="UsTripGuard"/>: the guard must FAIL a lane whose page tripped, and
    /// must stay quiet when the lane declared the trip. Without this, a guard that silently passed
    /// everything would look identical to a suite where nothing trips.
    /// </summary>
    private static void TripGuardFailsAPlantedTrip()
    {
        var fake = new FakeDiagnosticsSource { ThrowOnDetail = true };
        FillRows(fake, 8);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        host.MeasureAndArrange(new Vector2(620f, 560f));
        host.DrawFrame(new Rect(0f, 0f, 620f, 560f)); // deliberately unguarded: this step is about the guard failing

        Assert(UsTripGuard.AnyTripped(host.Session), "the planted throw is recorded as a tripped element");

        bool fired = false;
        try
        {
            UsTripGuard.ExpectNoTrips(host.Session, "planted-trip positive control");
        }
        catch (Exception ex)
        {
            fired = true;
            Assert(ex.Message.Contains("planted-trip positive control")
                    && ex.Message.Contains("planted projection failure"),
                "the guard's failure names the lane and what the element threw, got: " + ex.Message);
        }

        Assert(fired, "the guard fails a lane whose page was replaced by a recovery band");

        // And the explicit switch is the only way past it - it is never an omission.
        UsTripGuard.ExpectNoTrips(host.Session, "planted-trip positive control", deliberateTrips: true);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
