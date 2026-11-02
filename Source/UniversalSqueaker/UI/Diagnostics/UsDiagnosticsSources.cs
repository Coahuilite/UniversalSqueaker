using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Production source for the MAIN diagnostics window: viewport/search rows, live-selection
/// detail, monitor row, and the navigation actions (drill-in select, lock-detach, and the
/// in-window narrow Back). The row cache expires on the overlay revision plus the view-state
/// keys - never a wall clock (cache-clock rule; the 2026-09-04 filter misalignment is the proof case).
/// </summary>
public sealed class UsDiagnosticsSessionSource : IUsDiagnosticsSource, IUsDiagnosticsCaptureSource
{
    private readonly Dictionary<int, Pawn> pawnsById = new();
    private readonly List<UsDiagRow> rowsCache = new();

    private int rowsCacheRevision = -1;
    private bool rowsCacheSeconds;
    private string rowsCacheQuery = string.Empty;

    private string searchQuery = string.Empty;
    private int page;
    private float contentWidth;
    private UsDiagNavView navigationView = UsDiagNavView.List;
    private readonly bool[] groupOpen = { true, true, true };
    private bool searchTruncated;
    public string CaptureStatus => SqueakDiagnosticsOverlay.CaptureStatus
        + (searchTruncated ? "\n" + "US.Diagnostics.Capture.SearchTruncated".Translate() : "");
    public bool CanStartCapture => SqueakDiagnosticsOverlay.CanStartCapture(SqueakDiagnosticsOverlay.SelectedEntry?.Pawn);
    public bool Recording => SqueakDiagnosticsOverlay.Recording;
    public bool CanClearCapture => SqueakDiagnosticsOverlay.HasCapture && !Recording;
    public bool CaptureControlsVisible => !Collapsed;
    public void StartCapture() => SqueakDiagnosticsOverlay.StartCapture(SqueakDiagnosticsOverlay.SelectedEntry?.Pawn);
    public void StopCapture() => SqueakDiagnosticsOverlay.StopCapture();
    public void ClearCapture() => SqueakDiagnosticsOverlay.ClearCapture();
    public void CopyFeedback() => SqueakDiagnosticsOverlay.CopyFeedback(SqueakDiagnosticsOverlay.SelectedEntry?.Pawn);

    public int Revision => SqueakDiagnosticsOverlay.Revision;
    public bool IsValid => SqueakDiagnosticsOverlay.IsSessionActive;

    public bool ShowSeconds { get; set; }
    public bool Collapsed { get; set; }

    public string SearchQuery
    {
        get => searchQuery;
        set
        {
            string query = value ?? string.Empty;
            if (string.Equals(query, searchQuery, StringComparison.Ordinal)) return;
            searchQuery = query;
            page = 0;
        }
    }

    public int Page
    {
        get => page;
        set => page = UsDiagnosticsProjection.ClampPage(value, AllRows.Count);
    }

    /// <summary>
    /// The arranged content width the shell hands the page each pass, and the single input of
    /// <see cref="Narrow"/>. This setter itself does NOT bump; the flip's clock movement belongs to
    /// <see cref="UsDiagnosticsHost.ApplyContentWidth"/>, which is the production door for it - the
    /// page's two presentations are swapped by a read-only <c>VisibleBind</c>, and a read-only binding
    /// announces no revision of its own. The engine's own <c>Breakpoint</c> evaluation reads the same
    /// coordinate space, because the shell feeds and arranges the same rect in one frame.
    /// </summary>
    public void SetContentWidth(float width) => contentWidth = width;

    public bool Narrow => UsDiagnosticsProjection.IsNarrowPresentation(contentWidth);

    /// <summary>
    /// Which narrow view is showing. The wide presentation ignores it; the narrow one shows exactly
    /// one of list/detail. Back (the nav widget) writes List through the binding, so the layout cache
    /// sees the change on the same clock as every other display write.
    /// </summary>
    public UsDiagNavView NavigationView
    {
        get => navigationView;
        set => navigationView = value;
    }

    /// <summary>The Back control exists only where an owning list does: narrow AND showing the detail.</summary>
    public bool ShowBackControl => Narrow && navigationView == UsDiagNavView.Detail;

    public IReadOnlyList<UsDiagRow> PageRows
    {
        get
        {
            List<UsDiagRow> all = AllRows;
            int start = page * UsDiagnosticsProjection.RowsPerPage;
            List<UsDiagRow> result = new(UsDiagnosticsProjection.RowsPerPage);
            for (int i = start; i < all.Count && i < start + UsDiagnosticsProjection.RowsPerPage; i++)
            {
                result.Add(all[i]);
            }

            return result;
        }
    }

    public int TotalRowCount => AllRows.Count;
    public int PageCount => UsDiagnosticsProjection.PageCount(AllRows.Count);
    public string QueryStatus
    {
        get
        {
            int returned = TotalRowCount;
            string scope = (string.IsNullOrWhiteSpace(searchQuery) ? "US.Diagnostics.Search.Viewport" : "US.Diagnostics.Search.Map").Translate();
            return scope + " · " + string.Format("US.Diagnostics.Search.Returned".Translate(), returned)
                + (searchTruncated ? "\n" + "US.Diagnostics.Capture.SearchTruncated".Translate() : "");
        }
    }

    public UsDiagDetail? Detail
    {
        get
        {
            SqueakDiagnosticsOverlay.CachedPawn? entry = SqueakDiagnosticsOverlay.SelectedEntry;
            return entry == null ? null : UsDiagnosticsRowFactory.BuildDetail(entry, ShowSeconds);
        }
    }

    public UsDiagRow? MonitorRow
    {
        get
        {
            SqueakDiagnosticsOverlay.CachedPawn? entry = SqueakDiagnosticsOverlay.MonitorEntry;
            return entry == null ? null : UsDiagnosticsRowFactory.BuildRow(entry, ShowSeconds);
        }
    }

    /// <summary>The bar's three answers, built from the same monitor row the bar used to paint raw.</summary>
    public UsDiagBarModel? Bar => SqueakDiagnosticsOverlay.CaptureBar ?? UsDiagnosticsRowFactory.BuildBar(
        MonitorRow,
        locked: false,
        lockedPawnText: string.Empty,
        totalTargets: TotalRowCount,
        paused: Find.TickManager.Paused,
        snapshot: SqueakDiagnosticsOverlay.MonitorEntry?.Snapshot);

    public bool RawOpen { get; set; }

    /// <summary>All three groups start open; only a user click folds one away.</summary>
    public bool IsGroupOpen(UsDiagGateGroup group) => groupOpen[(int)group];

    public void SetGroupOpen(UsDiagGateGroup group, bool open) => groupOpen[(int)group] = open;

    public bool CloseRequested { get; private set; }

    public void RequestClose() => CloseRequested = true;

    public bool CanLockDisplayed => SqueakDiagnosticsOverlay.SelectedEntry != null;

    /// <summary>
    /// Row click, unchanged in meaning: an on-screen pawn drills in through game selection, an
    /// off-screen (search-only) hit locks and detaches. The ONLY addition is navigation - in narrow
    /// mode a drill-in also switches the in-window view to the detail, because the pinned window is
    /// the off-screen pawn's detail surface while selection cannot reach it.
    /// </summary>
    public void ClickRow(int pawnId)
    {
        if (!pawnsById.TryGetValue(pawnId, out Pawn? pawn) || pawn == null) return;
        if (SqueakDiagnosticsOverlay.IsInViewport(pawn))
        {
            Find.Selector.Select(pawn);
            navigationView = UsDiagNavView.Detail;
        }
        else
        {
            SqueakDiagnosticsOverlay.LockAndOpenDetail(pawn);
        }
    }

    public void LockDisplayed()
    {
        SqueakDiagnosticsOverlay.CachedPawn? entry = SqueakDiagnosticsOverlay.SelectedEntry;
        if (entry != null)
        {
            SqueakDiagnosticsOverlay.LockAndOpenDetail(entry.Pawn);
        }
    }

    private List<UsDiagRow> AllRows
    {
        get
        {
            int revision = Revision;
            if (rowsCacheRevision == revision && rowsCacheSeconds == ShowSeconds
                && string.Equals(rowsCacheQuery, searchQuery, StringComparison.Ordinal))
            {
                return rowsCache;
            }

            rowsCache.Clear();
            pawnsById.Clear();
            searchTruncated = false;

            string query = searchQuery.Trim();
            if (query.Length > 0)
            {
                List<Pawn> hits = SqueakDiagnosticsOverlay.SearchCurrentMap(query, out searchTruncated);
                for (int i = 0; i < hits.Count; i++)
                {
                    rowsCache.Add(UsDiagnosticsRowFactory.BuildSearchRow(hits[i], ShowSeconds));
                    pawnsById[hits[i].thingIDNumber] = hits[i];
                }
            }
            else
            {
                IReadOnlyList<SqueakDiagnosticsOverlay.CachedPawn> entries = SqueakDiagnosticsOverlay.ViewportEntries;
                for (int i = 0; i < entries.Count; i++)
                {
                    rowsCache.Add(UsDiagnosticsRowFactory.BuildRow(entries[i], ShowSeconds));
                    pawnsById[entries[i].Pawn.thingIDNumber] = entries[i].Pawn;
                }
            }

            rowsCacheRevision = revision;
            rowsCacheSeconds = ShowSeconds;
            rowsCacheQuery = searchQuery;
            return rowsCache;
        }
    }
}

/// <summary>
/// Production source for one DETAIL window: pinned to a single pawn for its whole life.
/// The list/pager/search members are honest inert values (those widgets are not in this page's
/// spec); IsValid is the window's self-close signal when the pinned pawn dies, despawns, or the
/// session ends. It has no list navigation context, so it reports no narrow state and no Back
/// control - the pinned window must not grow a fake one.
/// </summary>
public sealed class UsDiagnosticsDetailSource : IUsDiagnosticsSource, IUsDiagnosticsCaptureSource
{
    private readonly Pawn pinnedPawn;
    private readonly bool[] pinnedGroupOpen = { true, true, true };
    public string CaptureStatus => SqueakDiagnosticsOverlay.CaptureStatus;
    public bool CanStartCapture => SqueakDiagnosticsOverlay.CanStartCapture(pinnedPawn);
    public bool Recording => SqueakDiagnosticsOverlay.Recording;
    public bool CanClearCapture => SqueakDiagnosticsOverlay.HasCapture && !Recording;
    public bool CaptureControlsVisible => !Collapsed;
    public void StartCapture() => SqueakDiagnosticsOverlay.StartCapture(pinnedPawn);
    public void StopCapture() => SqueakDiagnosticsOverlay.StopCapture();
    public void ClearCapture() => SqueakDiagnosticsOverlay.ClearCapture();
    public void CopyFeedback() => SqueakDiagnosticsOverlay.CopyFeedback(pinnedPawn);

    public UsDiagnosticsDetailSource(Pawn pinnedPawn)
    {
        this.pinnedPawn = pinnedPawn ?? throw new ArgumentNullException(nameof(pinnedPawn));
    }

    public int Revision => SqueakDiagnosticsOverlay.Revision;
    public bool IsValid => SqueakDiagnosticsOverlay.IsTracked(pinnedPawn);

    public bool ShowSeconds { get; set; }
    public bool Collapsed { get; set; }
    public string SearchQuery { get => string.Empty; set { } }
    public int Page { get => 0; set { } }

    /// <summary>No owning list: the pinned window keeps its identity/lock shape at every width.</summary>
    public void SetContentWidth(float width) { }

    public bool Narrow => false;

    public UsDiagNavView NavigationView { get => UsDiagNavView.Detail; set { } }

    public bool ShowBackControl => false;

    public IReadOnlyList<UsDiagRow> PageRows => Array.Empty<UsDiagRow>();
    public int TotalRowCount => 0;
    public int PageCount => 1;

    public UsDiagDetail? Detail
    {
        get
        {
            if (!SqueakDiagnosticsOverlay.TryGetEntry(pinnedPawn, out SqueakDiagnosticsOverlay.CachedPawn? entry)) return null;
            return UsDiagnosticsRowFactory.BuildDetail(entry, ShowSeconds);
        }
    }

    public UsDiagRow? MonitorRow
    {
        get
        {
            if (!SqueakDiagnosticsOverlay.TryGetEntry(pinnedPawn, out SqueakDiagnosticsOverlay.CachedPawn? entry)) return null;
            return UsDiagnosticsRowFactory.BuildRow(entry, ShowSeconds);
        }
    }

    /// <summary>Same three answers, with the identity segment naming the pinned pawn (09 §3.3 rule 5).</summary>
    public UsDiagBarModel? Bar
    {
        get
        {
            SqueakDiagnosticSnapshot? snapshot = SqueakDiagnosticsOverlay.TryGetEntry(pinnedPawn, out SqueakDiagnosticsOverlay.CachedPawn? entry)
                ? entry.Snapshot
                : null;
            return SqueakDiagnosticsOverlay.CaptureBar ?? UsDiagnosticsRowFactory.BuildBar(
                MonitorRow,
                locked: true,
                lockedPawnText: pinnedPawn.LabelShort,
                totalTargets: 0,
                paused: Find.TickManager.Paused,
                snapshot: snapshot);
        }
    }

    public bool RawOpen { get; set; }

    /// <summary>The pinned window owns its own three group switches; all start open.</summary>
    public bool IsGroupOpen(UsDiagGateGroup group) => pinnedGroupOpen[(int)group];

    public void SetGroupOpen(UsDiagGateGroup group, bool open) => pinnedGroupOpen[(int)group] = open;

    public bool CloseRequested { get; private set; }

    public void RequestClose() => CloseRequested = true;

    public bool CanLockDisplayed => false;

    public void ClickRow(int pawnId) { }
    public void LockDisplayed() { }
}

/// <summary>
/// Shared row/detail construction: extracts the primitive <see cref="UsDiagGateFacts"/> from a
/// cached snapshot + pawn and hands them to the pure projection. This is the ONLY place allowed
/// to read overlay snapshots for display; the old per-action filter of the C row died with the
/// lastDispatched ruling - audio attribution is action-independent now.
/// </summary>
internal static class UsDiagnosticsRowFactory
{
    internal static UsDiagRow BuildRow(SqueakDiagnosticsOverlay.CachedPawn entry, bool showSeconds)
    {
        Pawn pawn = entry.Pawn;
        SqueakDiagnosticSnapshot s = entry.Snapshot;
        return new UsDiagRow(
            pawn.thingIDNumber,
            SqueakDiagnosticsOverlay.ToneFor(s),
            LabelOf(pawn),
            CurrentActionText(s),
            CooldownText(s, showSeconds),
            DispatchText(s.LastDispatched),
            SqueakDiagnosticsOverlay.ReadyFor(s),
            SqueakDiagnosticsOverlay.IsLocked(pawn), ReadStatus(entry));
    }

    internal static UsDiagRow BuildSearchRow(Pawn pawn, bool showSeconds)
    {
        if (SqueakDiagnosticsOverlay.TryGetEntry(pawn, out SqueakDiagnosticsOverlay.CachedPawn? entry))
        {
            return BuildRow(entry, showSeconds);
        }

        // Not tracked yet (off-screen hit): honest blanks everywhere except the identity.
        return new UsDiagRow(pawn.thingIDNumber, UsDiagDotTone.Unknown, LabelOf(pawn),
            UsDiagnosticsProjection.Dash, UsDiagnosticsProjection.Dash, UsDiagnosticsProjection.Dash,
            false, SqueakDiagnosticsOverlay.IsLocked(pawn));
    }

    /// <summary>
    /// The collapsed bar's three answers. The activity sentence needs the event clock, and the clock is
    /// taken from the pawn's last EVALUATION (a blocked attempt is still an event), falling back to its
    /// last dispatch; null means nothing has ever happened and the bar says so instead of implying age.
    /// </summary>
    internal static UsDiagBarModel BuildBar(
        UsDiagRow? row,
        bool locked,
        string lockedPawnText,
        int totalTargets,
        bool paused,
        SqueakDiagnosticSnapshot? snapshot)
    {
        int? lastEventTick = LastEventTick(snapshot);
        int now = Find.TickManager.TicksGame;
        UsDiagBarFacts f = new()
        {
            Locked = locked,
            LockedPawnText = lockedPawnText ?? string.Empty,
            TotalTargets = totalTargets,
            HasMonitorRow = row != null,
            GamePaused = paused,
            PawnText = row?.PawnText ?? string.Empty,
            ActionText = row?.ActionText ?? string.Empty,
            AudioText = row?.AudioText ?? string.Empty,
            MonitorTone = row?.Tone ?? UsDiagDotTone.Unknown,
            HasLastEvent = lastEventTick.HasValue,
            LastEventTimeText = lastEventTick.HasValue ? UsDiagnosticsProjection.ClockText(lastEventTick.Value) : string.Empty,
            MinutesSinceLastEvent = lastEventTick.HasValue ? Math.Max(0, now - lastEventTick.Value) / 60 : 0,
        };

        return UsDiagnosticsProjection.BuildBar(f, Tr);
    }

    private static int? LastEventTick(SqueakDiagnosticSnapshot? snapshot)
    {
        if (!snapshot.HasValue) return null;
        SqueakDiagnosticSnapshot s = snapshot.Value;
        if (s.LastEvaluation.HasValue) return s.LastEvaluation.Value.Tick;
        return s.LastDispatched.HasValue ? s.LastDispatched.Value.Tick : (int?)null;
    }

    internal static UsDiagDetail BuildDetail(SqueakDiagnosticsOverlay.CachedPawn entry, bool showSeconds)
    {
        var s = entry.Snapshot;
        var gates = new List<UsDiagGateLine>();
        if (s.Response.HasValue)
        {
            var observed = s.Response.Value;
            gates.Add(new UsDiagGateLine("US.Diagnostics.Gate.ActionCooldown".Translate(), CooldownText(s, showSeconds),
                UsDiagGateState.NA, UsDiagGateGroup.Rules, UsDiagBasis.PreviousEvaluation,
                UsDiagnosticsProjection.Status(UsDiagGateState.NA, "!", Tr)));
            gates.Add(new UsDiagGateLine("US.Kernel.LastObservedDecision".Translate(),
                observed.Action + " @" + observed.ObservedTick + " " + observed.Reason + " " + observed.Receipt?.Result,
                UsDiagGateState.NA, UsDiagGateGroup.Audio, UsDiagBasis.PreviousEvaluation, UsDiagnosticsProjection.Status(UsDiagGateState.NA, "!", Tr)));
        }
        gates.Add(new UsDiagGateLine("US.Diagnostics.Gate.Playability".Translate(), UsDiagnosticsProjection.Dash,
            UsDiagGateState.Pending, UsDiagGateGroup.Audio, UsDiagBasis.CurrentObservation,
            UsDiagnosticsProjection.Status(UsDiagGateState.Pending, "!", Tr), UsDiagPendingReason.SourceMissing));
        string status = ReadStatus(entry);
        string saved = s.Evidence == null ? "US.Diagnostics.Capture.NotCaptured".Translate() : s.Evidence.Text();
        var summary = new UsDiagCurrentSummary(status,
            "US.Diagnostics.Capture.ReadBasis".Translate(entry.HasSnapshot ? entry.LastSuccessRealtime.ToString("0.##") : "—", entry.ReadFailures, entry.ReadFailure),
            false, true, Array.Empty<string>());
        return new UsDiagDetail(LabelOf(entry.Pawn), SqueakDiagnosticsOverlay.ReadyFor(s), SqueakDiagnosticsOverlay.IsLocked(entry.Pawn),
            CurrentActionText(s), DispatchText(s.LastDispatched), gates, summary,
            new UsDiagPreviousBand("US.Kernel.LastObservedDecision".Translate(), saved, DispatchText(s.LastDispatched)), status);
    }

    private static string ReadStatus(SqueakDiagnosticsOverlay.CachedPawn entry)
    {
        string state = entry.ReadFailure != "" ? entry.HasSnapshot ? "Stale" : "ReadFailed"
            : entry.Snapshot.Evidence?.Stage != "Evaluated" ? entry.Snapshot.Evidence?.Stage ?? "NotCaptured"
            : entry.Snapshot.Evidence.Result;
        return ("US.Diagnostics.Capture.State." + state).Translate();
    }

    private static string LabelOf(Pawn pawn) => $"{pawn.LabelShort} ({pawn.def.defName})";
    private static string CurrentActionText(SqueakDiagnosticSnapshot s)
        => string.IsNullOrEmpty(s.Response?.Action) ? UsDiagnosticsProjection.Dash : s.Response!.Value.Action;
    private static string CooldownText(SqueakDiagnosticSnapshot s, bool showSeconds)
    {
        if (!s.Response.HasValue) return UsDiagnosticsProjection.Dash;
        var observed = s.Response.Value;
        if (observed.Action.Length == 0) return UsDiagnosticsProjection.Dash;
        if (s.Evidence?.Interaction == true)
            return "US.Diagnostics.Capture.SavedSecondsUntil".Translate(s.Evidence.InteractionUntil?.ToString("0.###") ?? "—");
        long now = Find.TickManager.TicksGame;
        long action = observed.ActionDeadline == long.MinValue ? 0 : Math.Max(0, observed.ActionDeadline - now);
        long shared = observed.ChecksShared ? Math.Max(0, observed.SharedDeadline - now) : 0;
        double divisor = showSeconds ? 60 : 1;
        return (observed.ActionDeadline == long.MinValue ? "—" : (action / divisor).ToString("0.##")) + " / " + (shared / divisor).ToString("0.##") + (showSeconds ? "s" : "t");
    }
    private static string DispatchText(SqueakRecentOutcome? dispatched)
    {
        if (dispatched == null) return UsDiagnosticsProjection.Dash;
        SqueakRecentOutcome o = dispatched.Value;
        return UsDiagnosticsProjection.FormatDispatch(o.Tier?.ToString() ?? "Vanilla", o.PoolStableKey, o.Sound?.defName, Tr);
    }

    private static string Percent(float? value, float? baseValue)
    {
        if (!value.HasValue) return UsDiagnosticsProjection.Dash;
        return baseValue.HasValue
            ? $"{value.Value:0.###} / {baseValue.Value:0.###}"
            : $"{value.Value:0.###}";
    }

    private static string Tr(string key) => key.Translate().ToString();
}
