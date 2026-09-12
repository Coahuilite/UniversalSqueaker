using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// Round-9 harness lane for the diagnostics panel (the 建满 set at the seams the engine owns):
/// fake snapshot source injection, both generated pages validated by the REAL production host,
/// view-state writes routing + bumping the session clock, row-click/lock actions routing, the
/// collapse reflow (columns vanish, bar appears on the SAME clock as everything else), and a
/// planted projection failure landing in the session guard's recovery instead of escaping the
/// frame. Off-screen lock tracking itself is pinned by the pure model lane (SqueakDiagnostics-
/// SessionModel); the window-level teardown interlock (main close cascades, close = unlock) is
/// the maintainer live-walkthrough item - WindowStack is not stubbable at this seam.
/// </summary>
internal static class DiagnosticsPanelLaneTests
{
    public static void RunAll()
    {
        Step("main host validates the generated master-detail spec", MainHostCreation);
        Step("detail host validates the lock page", DetailHostCreation);
        Step("view writes route to the source and bump the session clock", WritesRouteAndBump);
        Step("row click and lock actions route through the source", ActionsRoute);
        Step("collapse swaps columns and the bar on the session clock", CollapseReflow);
        Step("collapsed bar answers the three questions (09 §3.3)", CollapsedBarAnswers);
        Step("expanded detail leads with the verdict and groups the chain", VerdictAndGroupedChain);
        Step("raw values fold by default", RawFoldsByDefault);
        Step("the bar's close action routes as a page request", CloseRoutes);
        Step("a glyph the font cannot draw becomes its word (D7)", MissingGlyphFallsBack);
        Step("the state word is printed once per blocked row", GateBlockMarkAppearsOnceOnScreen);
        Step("throwing projection lands in guard recovery, frame survives", ThrowingSourceRecovers);
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

    private sealed class FakeDiagnosticsSource : IUsDiagnosticsSource
    {
        public int RevisionValue = 5;
        public bool ThrowOnDetail;
        public int LastClickedPawnId = -1;
        public int LockCalls;

        public List<UsDiagRow> Rows = new();

        public int Revision => RevisionValue;
        public bool IsValid { get; set; } = true;
        public bool ShowSeconds { get; set; }
        public bool Collapsed { get; set; }
        public string SearchQuery { get; set; } = string.Empty;
        public int Page { get; set; }

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

                List<UsDiagGateLine> gates = UsDiagnosticsProjection.BuildGateChain(new UsDiagGateFacts(), Tr);
                return new UsDiagDetail(
                    "Violet (Human)",
                    false,
                    false,
                    "Work",
                    "[XenotypePack·craftsmen-xeno] : Squeak_Happy_03",
                    gates,
                    UsDiagnosticsProjection.BuildVerdict(false, gates, Tr));
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

        public bool RawOpen { get; set; }
        public bool CloseRequested { get; private set; }
        public void RequestClose() => CloseRequested = true;

        public bool CanLockDisplayed { get; set; } = true;

        public void ClickRow(int pawnId) => LastClickedPawnId = pawnId;
        public void LockDisplayed() => LockCalls++;

        /// <summary>
        /// The format-shaped half of the translation stub: the new sentences carry their placeholders in
        /// the translated value (the language files hold them), so a lane that returned the bare key
        /// everywhere would assert nothing about substitution.
        /// </summary>
        private static string Tr(string key) => key switch
        {
            "US.Diagnostics.Bar.Scale" => "{0} targets",
            "US.Diagnostics.Bar.IdentityLocked" => "Sound log (locked: {0})",
            "US.Diagnostics.Bar.Activity.Recent" => "Last {0} {1} {2}",
            "US.Diagnostics.Bar.Activity.Stale" => "{0} min without a sound, last {1}",
            "US.Diagnostics.Verdict.Blocked" => "Blocked at: {0}",
            "US.Diagnostics.Gates.FirstBlock" => "first block: {0}",
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
        host.DrawFrame(new Rect(0f, 0f, 320f, 480f));
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

    private static void VerdictAndGroupedChain()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 8);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        Assert(host.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) && detail != null,
            "the detail binding still projects a model");
        Assert(detail!.Verdict.Length > 0, "the expanded state leads with a verdict sentence");

        int first = UsDiagnosticsProjection.FirstBlockedIndex(detail.Gates);
        Assert(first >= 0 && detail.Gates[first].State == UsDiagGateState.Block,
            "the fake's chain is blocked, so a first block exists to mark");
        Assert(detail.Verdict.Contains(detail.Gates[first].Name),
            "the verdict names that same gate: '" + detail.Verdict + "'");

        int game = 0, rules = 0, audio = 0;
        foreach (UsDiagGateLine gate in detail.Gates)
        {
            if (gate.Group == UsDiagGateGroup.Game) game++;
            else if (gate.Group == UsDiagGateGroup.Rules) rules++;
            else audio++;
        }

        Assert(game == 4 && rules == 7 && audio == 5, "all three sides survive the projection, got " + game + "/" + rules + "/" + audio);

        host.DrawFrame(new Rect(0f, 0f, 680f, 560f));
        Assert(host.Session.IsActive, "the grouped chain draws inside a full real frame");
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
    /// The in-game defect, pinned where a player saw it: on the drawn frame. The state word is read back
    /// from the stub's label recorder, so a widget that grew a second prefix - whatever the projection
    /// says - reddens this step; the first block's emphasis is the attention-coloured rail instead.
    /// </summary>
    private static void GateBlockMarkAppearsOnceOnScreen()
    {
        var fake = new FakeDiagnosticsSource();
        FillRows(fake, 4);
        using UiHost host = UsDiagnosticsHost.CreateMain(fake);
        host.MeasureAndArrange(new Vector2(680f, 560f));

        IList texts = RecordedStub("LabelTexts");
        IList rects = RecordedStub("DrawBoxSolidRects");
        IList colors = RecordedStub("DrawBoxSolidColors");
        int textStart = texts.Count;
        int boxStart = colors.Count;

        host.DrawFrame(new Rect(0f, 0f, 680f, 560f));

        int blockRows = 0;
        if (host.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) && detail != null)
        {
            foreach (UsDiagGateLine gate in detail.Gates)
            {
                if (gate.State == UsDiagGateState.Block) blockRows++;
            }
        }

        Assert(blockRows > 1, "the fake's chain blocks in more than one place, so the sweep below is meaningful");
        // The detail must actually have drawn. A tripping widget is swapped for the recovery band, which
        // would leave every assertion below vacuously green - the shape that hid this defect.
        Assert(host.Session.TrippedNodes.Count == 0,
            "the frame drew the gate chain through the guard without tripping (" + host.Session.TrippedNodes.Count + " tripped)");

        // The mark token as THIS harness's translation seam resolves it (its stub returns the key).
        const string Mark = "US.Diagnostics.Gate.BlockMark";
        int marked = 0;
        for (int i = textStart; i < texts.Count; i++)
        {
            string text = (string)texts[i]!;
            int marks = CountOccurrences(text, Mark);
            Assert(marks <= 1, "no drawn row repeats the state word (the in-game defect): '" + text + "'");
            if (marks == 1) marked++;
        }

        Assert(marked == blockRows, "every blocked row is marked exactly once, got " + marked + " of " + blockRows);

        // "First" is carried by the rail: a 2px attention-coloured track - a different means from the word.
        // The comparison uses the theme the HOST drew with, not a stock instance: the panel's palette is
        // the host's own, and a lane that assumed DarkGold's literals would be asserting the wrong ruler.
        UiTheme hostTheme = host.CreateContext(1f).Theme;
        int attentionRails = 0;
        int dividerRails = 0;
        for (int i = boxStart; i < rects.Count && i < colors.Count; i++)
        {
            Rect rail = (Rect)rects[i]!;
            if (Math.Abs(rail.width - 2f) > 0.01f) continue;

            Color colour = (Color)colors[i]!;
            if (SameColor(colour, hostTheme.Warning)) attentionRails++;
            if (SameColor(colour, hostTheme.Divider)) dividerRails++;
        }

        Assert(attentionRails == 1, "exactly one rail carries the attention colour (the first block), got " + attentionRails);
        Assert(dividerRails >= 1, "the other chain rows keep the plain group rail, got " + dividerRails);
    }

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
        host.DrawFrame(new Rect(0f, 0f, 620f, 560f));
        Assert(host.Session.IsActive,
            "a throwing detail projection is absorbed by the session guard (engine RecoveryBand), the frame survives");
        fake.ThrowOnDetail = false;
        host.MeasureAndArrange(new Vector2(620f, 560f));
        host.DrawFrame(new Rect(0f, 0f, 620f, 560f));
        Assert(host.Session.IsActive, "and the same host keeps working after the failure clears");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
