using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Diagnostics session driver (round-9 contract). One live session feeds three observation roles
/// into <see cref="SqueakDiagnosticsSessionModel{TKey}"/>: a viewport sweep (every squeaker-carrying
/// pawn on screen, stable thingID order, no headcount gate), the live selection (the main window's
/// detail column), and the lock set (detachable per-pawn detail windows, tracked even off-screen).
/// Per-pawn snapshots are low-frequency, side-effect-free reads; the model's revision is the only
/// clock the UiKit pages rebuild on. Closing the main window ends the session and cascades every
/// detail window closed; a map change tears the session down through the existing lifecycle guard.
/// </summary>
public static class SqueakDiagnosticsOverlay
{
    private const float DetailRefreshSeconds = 0.25f;
    private const float SweepRefreshSeconds = 0.5f;

    /// <summary>Cap on one search result page source; a colony never legitimately shows this many name matches.</summary>
    internal const int SearchResultsCap = 64;

    internal const string Mark = "●";

    /// <summary>Diagnostics mark colors: green = ready; amber = deterministic block; blue = random/parameter gate.</summary>
    internal static readonly Color ReadyColor = new(.25f, .82f, .38f);
    internal static readonly Color BlockedColor = new(.95f, .68f, .22f);
    internal static readonly Color PendingColor = new(.35f, .66f, .95f);

    /// <summary>One cached structured snapshot per tracked pawn. Read-only for the pages; the driver mutates only during refresh.</summary>
    internal sealed class CachedPawn
    {
        public Pawn Pawn = null!;
        public CompSqueaker Comp = null!;
        public SqueakDiagnosticSnapshot Snapshot;
        public string MarkText = string.Empty;
        public Color MarkColor = Color.white;
        public int Fingerprint;
    }

    private static readonly SqueakDiagnosticsSessionModel<Pawn> model = new();
    private static readonly Dictionary<Pawn, CachedPawn> entriesByPawn = new();
    private static readonly List<CachedPawn> viewportEntries = new();
    private static readonly List<Pawn> sweepScratch = new();
    private static readonly List<Pawn> lockedScratch = new();
    private static readonly List<Pawn> dropScratch = new();
    private static readonly Dictionary<Pawn, SqueakDiagnosticsDetailWindow> detailWindows = new();

    private static SqueakDiagnosticsPanel? mainPanel;
    private static Map? cachedMap;
    private static bool sessionActive;
    private static float nextSweepRealtime;
    private static float nextDetailRealtime;
    private static CachedPawn? lastChangedEntry;

    /// <summary>The single rebuild clock: membership, selection, and snapshot updates all flow through the model.</summary>
    internal static int Revision => model.Revision;

    public static bool IsSessionActive => sessionActive;

    internal static IReadOnlyList<CachedPawn> ViewportEntries => viewportEntries;

    internal static IReadOnlyList<Pawn> LockedPawns => model.Locked;

    internal static bool IsLocked(Pawn pawn) => model.IsLocked(pawn);

    internal static bool TryGetEntry(Pawn pawn, out CachedPawn entry) => entriesByPawn.TryGetValue(pawn, out entry!);

    /// <summary>Viewport membership decides a row click: visible pawn drills in via selection,
    /// search-only (off-screen) pawn locks directly - selection cannot reach what the camera cannot.</summary>
    internal static bool IsInViewport(Pawn pawn)
    {
        for (int i = 0; i < viewportEntries.Count; i++)
        {
            if (ReferenceEquals(viewportEntries[i].Pawn, pawn)) return true;
        }

        return false;
    }

    internal static bool IsTracked(Pawn pawn) => sessionActive && model.IsTracked(pawn);

    /// <summary>Collapsed-bar content: the pawn whose row content last CHANGED while still tracked;
    /// falls back to the first viewport entry (a bar showing nothing would be a lie of omission).</summary>
    internal static CachedPawn? MonitorEntry
    {
        get
        {
            if (lastChangedEntry != null && model.IsTracked(lastChangedEntry.Pawn)) return lastChangedEntry;
            return viewportEntries.Count > 0 ? viewportEntries[0] : null;
        }
    }

    /// <summary>The entry the main window's detail column shows (live selection), or null.</summary>
    internal static CachedPawn? SelectedEntry
    {
        get
        {
            if (!model.HasSelected) return null;
            return entriesByPawn.TryGetValue(model.Selected!, out CachedPawn? entry) ? entry : null;
        }
    }

    /// <summary>The single readiness rule shared by the mark color, the header badge, and the summary row.</summary>
    internal static bool ReadyFor(SqueakDiagnosticSnapshot s) => s.EffectiveTimingReady && s.CurrentActionEnabled
        && s.VocalCapability.VocalOrganEfficiency > SqueakVocalCapability.VocalSilenceThreshold;

    /// <summary>Opens the session (idempotent) and the main window.</summary>
    public static void BeginSession()
    {
        if (Find.CurrentMap == null)
        {
            return;
        }

        if (!sessionActive)
        {
            sessionActive = true;
            cachedMap = Find.CurrentMap;
            CompSqueaker.DiagnosticsEnabled = true;
            model.Reset();
        }

        OpenMainPanel();
    }

    /// <summary>Lock a pawn and open (or surface) its detachable detail window. Row clicks and search hits both land here.</summary>
    public static void LockAndOpenDetail(Pawn pawn)
    {
        if (pawn == null) throw new ArgumentNullException(nameof(pawn));
        if (!sessionActive)
        {
            BeginSession();
        }
        if (!sessionActive)
        {
            return; // no map: BeginSession declined.
        }

        model.Lock(pawn);
        if (!detailWindows.ContainsKey(pawn))
        {
            SqueakDiagnosticsDetailWindow window = new(pawn);
            detailWindows.Add(pawn, window);
            Find.WindowStack.Add(window);
        }
    }

    /// <summary>Called from a detail window's PreClose: closing it IS the unlock. Never re-enters window close.</summary>
    internal static void NotifyDetailWindowClosed(Pawn pawn)
    {
        detailWindows.Remove(pawn);
        model.Unlock(pawn);
        PruneDroppedEntries();
    }

    /// <summary>Called from the main window's PreClose: the session ends, every detail window cascades closed.</summary>
    internal static void NotifyPanelClosed()
    {
        CloseSession();
    }

    /// <summary>Per-frame teardown guard. It must remain free of snapshot, formatting, translation, and draw work.</summary>
    public static void MaintainLifecycle()
    {
        if (!sessionActive)
        {
            return;
        }

        Map? map = Find.CurrentMap;
        if (map == null || !ReferenceEquals(cachedMap, map))
        {
            CloseSession();
        }
    }

    /// <summary>Layout-only lifecycle and snapshot work. Never call from a draw path.</summary>
    public static void RefreshIfDue()
    {
        try
        {
            if (!sessionActive)
            {
                return;
            }

            MaintainLifecycle();
            if (!sessionActive)
            {
                return;
            }

            // Diagnostics stay fresh even when production population scaling is disabled; individual
            // comp snapshots only read the resulting immutable shared state.
            CompSqueaker.MaintainPeriodicPopulationDiagnostics();

            float now = Time.realtimeSinceStartup;
            if (now >= nextDetailRealtime)
            {
                nextDetailRealtime = now + DetailRefreshSeconds;
                RefreshDetailRoles(now);
            }

            if (now >= nextSweepRealtime)
            {
                nextSweepRealtime = now + SweepRefreshSeconds;
                RefreshViewportSweep();
            }
        }
        catch (Exception ex)
        {
            // Diagnostics must fail closed: a modded pawn/snapshot exception never breaks the game frame.
            Log.Warning("[UniversalSqueaker] Diagnostics refresh failed: " + SqueakLogText.SanitizeExceptionMessage(ex.Message));
        }
    }

    /// <summary>
    /// Search over the CURRENT MAP only: the data source is the game's own per-map collection, so
    /// the map boundary is native, not hand-rolled. Matches are substring/label or defName,
    /// case-insensitive; a result row click locks directly (selection cannot reach off-screen pawns).
    /// </summary>
    public static List<Pawn> SearchCurrentMap(string? rawQuery)
    {
        List<Pawn> results = new();
        if (!sessionActive || cachedMap == null)
        {
            return results;
        }
        string query = (rawQuery ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            return results;
        }

        foreach (Pawn pawn in cachedMap.mapPawns.AllPawnsSpawned)
        {
            if (pawn.Dead || pawn.GetComp<CompSqueaker>() == null)
            {
                continue;
            }

            if (MatchesQuery(pawn.LabelShort, pawn.def.defName, query))
            {
                results.Add(pawn);
                if (results.Count >= SearchResultsCap)
                {
                    break;
                }
            }
        }

        results.Sort(static (a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
        return results;
    }

    /// <summary>Pure matcher (harness-testable): case-insensitive substring over the label OR the defName.</summary>
    internal static bool MatchesQuery(string label, string defName, string trimmedQuery)
        => trimmedQuery.Length > 0
            && (label.IndexOf(trimmedQuery, StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf(trimmedQuery, StringComparison.OrdinalIgnoreCase) >= 0);

    /// <summary>Read-side mark lookup used by CompSqueaker.PostDraw (public draw path).</summary>
    internal static bool TryGetMark(Pawn pawn, out string mark, out Color color)
    {
        if (sessionActive && entriesByPawn.TryGetValue(pawn, out CachedPawn? entry))
        {
            mark = entry.MarkText;
            color = entry.MarkColor;
            return true;
        }

        mark = string.Empty;
        color = Color.white;
        return false;
    }

    private static void RefreshDetailRoles(float now)
    {
        Pawn? selection = Find.Selector.SingleSelectedThing as Pawn;
        if (selection != null && IsTrackableNow(selection))
        {
            model.SetSelected(selection);
            UpdateSnapshot(EnsureEntry(selection));
        }
        else
        {
            model.SetSelected(null);
        }

        lockedScratch.Clear();
        lockedScratch.AddRange(model.Locked);
        for (int i = 0; i < lockedScratch.Count; i++)
        {
            Pawn pawn = lockedScratch[i];
            if (!IsTrackableNow(pawn))
            {
                // Dead/despawned/map-lost: the lock dies with the pawn, and its window closes with it.
                model.Unlock(pawn);
                CloseDetailWindow(pawn);
                continue;
            }

            UpdateSnapshot(EnsureEntry(pawn));
        }

        PruneDroppedEntries();
    }

    private static void RefreshViewportSweep()
    {
        Map map = cachedMap!;
        sweepScratch.Clear();
        CellRect view = Find.CameraDriver.CurrentViewRect.ExpandedBy(1);
        IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn pawn = pawns[i];
            if (pawn.Dead || !pawn.Spawned || !view.Contains(pawn.Position))
            {
                continue;
            }

            if (pawn.GetComp<CompSqueaker>() != null)
            {
                sweepScratch.Add(pawn);
            }
        }

        sweepScratch.Sort(static (a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
        model.ReplaceViewport(sweepScratch);

        viewportEntries.Clear();
        for (int i = 0; i < sweepScratch.Count; i++)
        {
            CachedPawn entry = EnsureEntry(sweepScratch[i]);
            UpdateSnapshot(entry);
            viewportEntries.Add(entry);
        }

        PruneDroppedEntries();
    }

    private static bool IsTrackableNow(Pawn pawn)
        => !pawn.Dead && pawn.Spawned && !pawn.Destroyed && pawn.MapHeld == cachedMap
            && pawn.GetComp<CompSqueaker>() != null;

    private static CachedPawn EnsureEntry(Pawn pawn)
    {
        if (entriesByPawn.TryGetValue(pawn, out CachedPawn? existing))
        {
            return existing;
        }

        CompSqueaker comp = pawn.GetComp<CompSqueaker>()!;
        comp.ResetDiagnosticState();
        CachedPawn entry = new() { Pawn = pawn, Comp = comp };
        entriesByPawn.Add(pawn, entry);
        return entry;
    }

    private static void UpdateSnapshot(CachedPawn entry)
    {
        try
        {
            entry.Snapshot = entry.Comp.GetDiagnosticSnapshot();
            entry.MarkText = Mark;
            UI.UsDiagDotTone tone = ToneFor(entry.Snapshot);
            entry.MarkColor = ColorForTone(tone);
            int fingerprint = RowFingerprint(entry.Snapshot);
            if (fingerprint != entry.Fingerprint)
            {
                entry.Fingerprint = fingerprint;
                // Monitor-bar semantics (round-9 ruling): the COLLAPSED bar shows the pawn whose
                // content last actually CHANGED, not merely the last refreshed one.
                lastChangedEntry = entry;
            }

            model.BumpRevision();
        }
        catch (Exception ex)
        {
            // Fail closed: one pawn's snapshot exception never aborts the refresh loop.
            Log.Warning("[UniversalSqueaker] Diagnostics snapshot failed for " + entry.Pawn.LabelShort + ": " + SqueakLogText.SanitizeExceptionMessage(ex.Message));
        }
    }

    /// <summary>Three-way row tone: green ready / amber deterministic block / blue pending. The single
    /// source for both the on-pawn mark color and the panel row dot - they can never disagree.</summary>
    internal static UI.UsDiagDotTone ToneFor(SqueakDiagnosticSnapshot s)
    {
        if (ReadyFor(s)) return UI.UsDiagDotTone.Ready;
        if (s.StartupPending || !s.Timing.ActionReady || (s.Timing.GlobalApplicable && !s.Timing.GlobalReady)
            || s.VocalCapability.VocalOrganEfficiency <= SqueakVocalCapability.VocalSilenceThreshold)
        {
            return UI.UsDiagDotTone.Blocked;
        }

        return UI.UsDiagDotTone.Pending;
    }

    private static Color ColorForTone(UI.UsDiagDotTone tone) => tone switch
    {
        UI.UsDiagDotTone.Ready => ReadyColor,
        UI.UsDiagDotTone.Blocked => BlockedColor,
        _ => PendingColor,
    };

    /// <summary>
    /// Numeric-only content fingerprint (never text): fields that changing means "something happened"
    /// for the monitor bar. Formatting stays entirely in the projection layer.
    /// </summary>
    private static int RowFingerprint(SqueakDiagnosticSnapshot s)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (s.CurrentTimingAction.HasValue ? (int)s.CurrentTimingAction.Value : -1);
            hash = hash * 31 + (s.CurrentActionEnabled ? 1 : 0);
            hash = hash * 31 + (s.StartupPending ? 1 : 0);
            hash = hash * 31 + (s.Timing.ActionReady ? 1 : 0);
            hash = hash * 31 + (s.Timing.GlobalReady ? 1 : 0);
            hash = hash * 31 + (s.EffectiveTimingReady ? 1 : 0);
            hash = hash * 31 + (s.Timing.ActionRemainingTicks ?? -1);
            hash = hash * 31 + s.Timing.GlobalRemainingTicks;
            hash = hash * 31 + (int)(s.VocalCapability.VocalOrganEfficiency * 64f);
            hash = hash * 31 + (s.LastDispatched.HasValue ? s.LastDispatched.Value.Tick : -1);
            hash = hash * 31 + (s.LastEvaluation.HasValue ? (int)s.LastEvaluation.Value.Outcome : -1);
            hash = hash * 31 + (s.LastEvaluation.HasValue ? s.LastEvaluation.Value.Tick : -1);
            return hash;
        }
    }

    private static void PruneDroppedEntries()
    {
        dropScratch.Clear();
        foreach (KeyValuePair<Pawn, CachedPawn> pair in entriesByPawn)
        {
            if (!model.IsTracked(pair.Key))
            {
                dropScratch.Add(pair.Key);
            }
        }

        for (int i = 0; i < dropScratch.Count; i++)
        {
            if (entriesByPawn.TryGetValue(dropScratch[i], out CachedPawn? entry))
            {
                entry.Comp.ResetDiagnosticState();
                entriesByPawn.Remove(dropScratch[i]);
                model.BumpRevision();
            }
        }

        // The viewport mirror must not keep pointers to dropped entries within the same frame.
        for (int i = viewportEntries.Count - 1; i >= 0; i--)
        {
            if (!entriesByPawn.ContainsKey(viewportEntries[i].Pawn))
            {
                viewportEntries.RemoveAt(i);
            }
        }
    }

    private static void CloseSession()
    {
        // Null the registries BEFORE closing: window PreClose re-enters the notify hooks, and the
        // windows are still on the stack while their PreClose runs.
        List<SqueakDiagnosticsDetailWindow> windows = new(detailWindows.Values);
        detailWindows.Clear();
        SqueakDiagnosticsPanel? panel = mainPanel;
        mainPanel = null;

        sessionActive = false;
        cachedMap = null;
        nextSweepRealtime = 0f;
        nextDetailRealtime = 0f;
        CompSqueaker.DiagnosticsEnabled = false;
        model.Reset();

        foreach (KeyValuePair<Pawn, CachedPawn> pair in entriesByPawn)
        {
            pair.Value.Comp.ResetDiagnosticState();
        }

        entriesByPawn.Clear();
        viewportEntries.Clear();

        if (panel != null && panel.IsOpen)
        {
            panel.Close();
        }

        for (int i = 0; i < windows.Count; i++)
        {
            if (windows[i].IsOpen)
            {
                windows[i].Close();
            }
        }
    }

    private static void CloseDetailWindow(Pawn pawn)
    {
        if (!detailWindows.TryGetValue(pawn, out SqueakDiagnosticsDetailWindow? window))
        {
            return;
        }

        detailWindows.Remove(pawn);
        if (window.IsOpen)
        {
            window.Close();
        }
    }

    private static void OpenMainPanel()
    {
        if (mainPanel != null && mainPanel.IsOpen)
        {
            return;
        }

        SqueakDiagnosticsPanel panel = new();
        mainPanel = panel;
        Find.WindowStack.Add(panel);
    }
}
