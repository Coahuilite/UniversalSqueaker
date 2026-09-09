using System;
using System.Collections.Generic;
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
        Step("collapse swaps columns and monitor bar on the session clock", CollapseReflow);
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

                return new UsDiagDetail(
                    "Violet (Human)",
                    true,
                    false,
                    "Work",
                    "[XenotypePack·craftsmen-xeno] : Squeak_Happy_03",
                    UsDiagnosticsProjection.BuildGateChain(new UsDiagGateFacts(), key => key));
            }
        }

        public UsDiagRow? MonitorRow => Rows.Count > 0 ? Rows[0] : null;
        public bool CanLockDisplayed { get; set; } = true;

        public void ClickRow(int pawnId) => LastClickedPawnId = pawnId;
        public void LockDisplayed() => LockCalls++;
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
        Vector2 size = new(620f, 560f);

        UiLayoutSnapshot open = host.MeasureAndArrange(size);
        Assert(open.RectById.TryGetValue("diag-list", out Rect listRect) && listRect.height > 0f,
            "expanded: list column has height");
        Assert(!open.RectById.TryGetValue("diag-monitor", out Rect monitorOpen) || monitorOpen.height <= 0f,
            "expanded: monitor bar measures zero");

        host.Bindings.Set(UsDiagnosticsHost.KeyCollapsed, true);
        UiLayoutSnapshot bar = host.MeasureAndArrange(size);
        Assert(bar.RectById.TryGetValue("diag-monitor", out Rect monitorBar) && monitorBar.height > 0f,
            "collapsed: monitor bar has height (same session, one binding flip - the bar ruling)");
        Assert((bar.RectById.TryGetValue("diag-list", out Rect listBar) ? listBar.height : 0f) <= 0f,
            "collapsed: list column measures zero");
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
