using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Production source for the MAIN diagnostics window: viewport/search rows, live-selection
/// detail, monitor row, and the two navigation actions (drill-in select, lock-detach).
/// The row cache expires on the overlay revision plus the view-state keys - never a wall clock
/// (cache-clock rule; the 2026-09-04 filter misalignment is the proof case).
/// </summary>
public sealed class UsDiagnosticsSessionSource : IUsDiagnosticsSource
{
    private readonly Dictionary<int, Pawn> pawnsById = new();
    private readonly List<UsDiagRow> rowsCache = new();

    private int rowsCacheRevision = -1;
    private bool rowsCacheSeconds;
    private string rowsCacheQuery = string.Empty;

    private string searchQuery = string.Empty;
    private int page;

    public int Revision => SqueakDiagnosticsOverlay.Revision;
    public bool IsValid => SqueakDiagnosticsOverlay.IsSessionActive;

    public bool ShowSeconds { get; set; }
    public bool Collapsed { get; set; }

    public string SearchQuery
    {
        get => searchQuery;
        set
        {
            string trimmed = (value ?? string.Empty).Trim();
            if (string.Equals(trimmed, searchQuery, StringComparison.Ordinal)) return;
            searchQuery = trimmed;
            page = 0;
        }
    }

    public int Page
    {
        get => page;
        set => page = UsDiagnosticsProjection.ClampPage(value, AllRows.Count);
    }

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

    public bool CanLockDisplayed => SqueakDiagnosticsOverlay.SelectedEntry != null;

    public void ClickRow(int pawnId)
    {
        if (!pawnsById.TryGetValue(pawnId, out Pawn? pawn) || pawn == null) return;
        if (SqueakDiagnosticsOverlay.IsInViewport(pawn))
        {
            Find.Selector.Select(pawn);
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

            if (searchQuery.Length > 0)
            {
                List<Pawn> hits = SqueakDiagnosticsOverlay.SearchCurrentMap(searchQuery);
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
/// session ends.
/// </summary>
public sealed class UsDiagnosticsDetailSource : IUsDiagnosticsSource
{
    private readonly Pawn pinnedPawn;

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
            SqueakDiagnosticsOverlay.IsLocked(pawn));
    }

    internal static UsDiagRow BuildSearchRow(Pawn pawn, bool showSeconds)
    {
        if (SqueakDiagnosticsOverlay.TryGetEntry(pawn, out SqueakDiagnosticsOverlay.CachedPawn? entry))
        {
            return BuildRow(entry, showSeconds);
        }

        // Not tracked yet (off-screen hit): honest blanks everywhere except the identity.
        return new UsDiagRow(pawn.thingIDNumber, UsDiagDotTone.Unknown, LabelOf(pawn),
            "—", "—", "—", false, SqueakDiagnosticsOverlay.IsLocked(pawn));
    }

    internal static UsDiagDetail BuildDetail(SqueakDiagnosticsOverlay.CachedPawn entry, bool showSeconds)
    {
        Pawn pawn = entry.Pawn;
        SqueakDiagnosticSnapshot s = entry.Snapshot;
        UsDiagGateFacts f = new()
        {
            ModeDisabled = SqueakRuntimeResolver.Current.VoicePackMode == SqueakVoicePackMode.Disabled,
            OnMap = pawn.Spawned && pawn.MapHeld == Find.CurrentMap,
            OnScreen = Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(pawn.Position),
            HasTimingAction = s.CurrentTimingAction.HasValue,
            ExternalTriggerPlan = s.CurrentTriggerMode == SqueakTriggerMode.External,
            PlayerControlled = pawn.IsPlayerControlled,
            NotDowned = !pawn.Downed,
            Awake = pawn.Awake(),
            ActionEnabled = s.CurrentActionEnabled,
            StartupPending = s.StartupPending,
            ProbabilityValue = Percent(s.EffectiveProbability, s.BaseProbability),
            ActionCooldownPass = s.Timing.ActionReady,
            ActionCooldownValue = UsDiagnosticsProjection.FormatCooldownPair(
                s.Timing.ActionRemainingTicks, s.Timing.ActionRemainingSeconds,
                s.Timing.ActionIntervalTicks, s.Timing.ActionIntervalSeconds, showSeconds),
            GlobalApplicable = s.Timing.GlobalApplicable,
            GlobalPass = s.Timing.GlobalReady,
            GlobalCooldownValue = UsDiagnosticsProjection.FormatCooldownPair(
                s.Timing.GlobalRemainingTicks, null, s.Timing.GlobalCooldownTicks == 0 ? (int?)null : s.Timing.GlobalCooldownTicks, null, showSeconds),
            VocalPass = s.VocalCapability.VocalOrganEfficiency > SqueakVocalCapability.VocalSilenceThreshold,
            TalkingValue = Percent(s.VocalCapability.TalkingChance, null),
            CurrentActionText = CurrentActionText(s),
            HasLastDispatch = s.LastDispatched.HasValue,
            LastDispatchText = DispatchText(s.LastDispatched),
        };

        if (s.CurrentTimingAction.HasValue)
        {
            SqueakAction action = s.CurrentTimingAction.Value;
            RuntimeActionDelta delta = SqueakRuntimeResolver.Current.ResolveContext(pawn).GetAction(action);
            if (delta.Scope == SqueakActionScope.ActiveCommand)
            {
                f.ScopeMatchApplicable = true;
                f.ScopeMatchPass = DiagnosticInvocationFor(action, pawn).IsActiveCommand;
            }
        }

        if (OutcomeForCurrentAction(s, s.LastEvaluation) is SqueakRecentOutcome ev)
        {
            f.EvaluationBelongsToCurrentAction = true;
            f.AudioPoolBlocked = ev.Outcome == SqueakTriggerOutcome.NoSoundFallback;
            f.EligibilityRejected = ev.Outcome == SqueakTriggerOutcome.EligibilityRejected;
            f.Dispatched = ev.Outcome == SqueakTriggerOutcome.Dispatched;
            f.PlaybackFailed = ev.Outcome == SqueakTriggerOutcome.PlaybackFailed;
        }

        return new UsDiagDetail(
            LabelOf(pawn),
            SqueakDiagnosticsOverlay.ReadyFor(s),
            SqueakDiagnosticsOverlay.IsLocked(pawn),
            f.CurrentActionText,
            f.LastDispatchText,
            UsDiagnosticsProjection.BuildGateChain(f, Tr));
    }

    private static string LabelOf(Pawn pawn) => $"{pawn.LabelShort} ({pawn.def.defName})";

    private static string CurrentActionText(SqueakDiagnosticSnapshot s)
        => s.CurrentTimingAction.HasValue ? SqueakLabels.Action(s.CurrentTimingAction.Value) : "—";

    private static string CooldownText(SqueakDiagnosticSnapshot s, bool showSeconds)
    {
        string action = UsDiagnosticsProjection.FormatCooldownPair(
            s.Timing.ActionRemainingTicks, s.Timing.ActionRemainingSeconds,
            s.Timing.ActionIntervalTicks, s.Timing.ActionIntervalSeconds, showSeconds);
        if (!s.Timing.GlobalApplicable)
        {
            return action;
        }

        string global = UsDiagnosticsProjection.FormatCooldownPair(
            s.Timing.GlobalRemainingTicks, null,
            s.Timing.GlobalCooldownTicks == 0 ? (int?)null : s.Timing.GlobalCooldownTicks, null, showSeconds);
        return action + " " + global;
    }

    private static string DispatchText(SqueakRecentOutcome? dispatched)
    {
        if (dispatched == null) return "—";
        SqueakRecentOutcome o = dispatched.Value;
        return UsDiagnosticsProjection.FormatDispatch(o.Tier?.ToString() ?? "Vanilla", o.PoolStableKey, o.Sound?.defName, Tr);
    }

    private static string Percent(float? value, float? baseValue)
    {
        if (!value.HasValue) return "—";
        return baseValue.HasValue
            ? $"{value.Value:0.###} / {baseValue.Value:0.###}"
            : $"{value.Value:0.###}";
    }

    /// <summary>Returns the supplied outcome only when it belongs to the currently displayed action,
    /// so an older result from a different action never impersonates this chain's gate states.</summary>
    private static SqueakRecentOutcome? OutcomeForCurrentAction(SqueakDiagnosticSnapshot s, SqueakRecentOutcome? outcome)
    {
        if (!s.CurrentTimingAction.HasValue || !outcome.HasValue) return null;
        string? key = UniversalSqueaker.Kernel.ActionKey.For(s.CurrentTimingAction.Value) ?? s.CurrentTimingAction.Value.ToString();
        return string.Equals(outcome.Value.Action, key, StringComparison.Ordinal) ? outcome : null;
    }

    /// <summary>Mirrors CompSqueaker.PeriodicInvocationFor plus the external sources used by
    /// Notify_Draft/Notify_Attack/Notify_Equip, so the diagnostic scope gate reads the same
    /// IsActiveCommand semantics as production.</summary>
    private static SqueakTriggerInvocation DiagnosticInvocationFor(SqueakAction action, Pawn pawn)
    {
        switch (action)
        {
            case SqueakAction.Work:
                return new SqueakTriggerInvocation(SqueakTriggerOrigin.Periodic,
                    pawn.CurJob?.playerForced == true ? SqueakInvocationSource.ActiveCommand : SqueakInvocationSource.Periodic);
            case SqueakAction.Attack:
                return new SqueakTriggerInvocation(SqueakTriggerOrigin.Attack,
                    pawn.CurJob?.playerForced == true ? SqueakInvocationSource.ActiveCommand : SqueakInvocationSource.StateEvent);
            case SqueakAction.Draft:
                return new SqueakTriggerInvocation(SqueakTriggerOrigin.Draft, SqueakInvocationSource.ActiveCommand);
            case SqueakAction.Undraft:
                return new SqueakTriggerInvocation(SqueakTriggerOrigin.Undraft, SqueakInvocationSource.ActiveCommand);
            case SqueakAction.Equip:
                return new SqueakTriggerInvocation(SqueakTriggerOrigin.Equip, SqueakInvocationSource.ActiveCommand);
            default:
                return new SqueakTriggerInvocation(SqueakTriggerOrigin.Periodic, SqueakInvocationSource.Periodic);
        }
    }

    private static string Tr(string key) => key.Translate().ToString();
}
