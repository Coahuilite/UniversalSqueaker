using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>Diagnostics display mode: off / single-pawn detail / multi-pawn list.</summary>
public enum SqueakDiagnosticsMode { Off, Selected, Visible }

/// <summary>
/// S4 diagnostic session manager (US rebuild, no MapInterface hook). Maintains a low-frequency
/// cached snapshot set per pawn, drives the draggable <see cref="SqueakDiagnosticsPanel"/>, and
/// exposes the cached marks for CompSqueaker.PostDraw — the only on-pawn drawing path (a public
/// ThingComp hook, so marks follow the pawn natively). Layout owns snapshot work; drawing owns
/// marks only.
/// </summary>
public static class SqueakDiagnosticsOverlay
{
    private const int MaxVisiblePawns = 16;
    private const float SelectedRefreshSeconds = 0.25f;
    private const float VisibleRefreshSeconds = 0.5f;

    internal const string Mark = "●";

    /// <summary>Diagnostics mark colors: green = ready; amber = deterministic block; blue = random/parameter gate.</summary>
    internal static readonly Color ReadyColor = new(.25f, .82f, .38f);
    internal static readonly Color BlockedColor = new(.95f, .68f, .22f);
    internal static readonly Color PendingColor = new(.35f, .66f, .95f);

    /// <summary>One cached structured snapshot per tracked pawn. Read-only for the panel; the overlay mutates only during refresh.</summary>
    internal sealed class CachedPawn
    {
        public Pawn Pawn = null!;
        public CompSqueaker Comp = null!;
        public SqueakDiagnosticSnapshot Snapshot;
        public string MarkText = string.Empty;
        public Color MarkColor = Color.white;
    }

    private static readonly List<CachedPawn> cachedPawns = new();
    private static readonly Dictionary<Pawn, CachedPawn> entriesByPawn = new();
    private static readonly HashSet<Pawn> refreshedPawns = new();
    private static SqueakDiagnosticsMode mode;
    private static Map? cachedMap;
    private static Pawn? selectedPawn;
    private static float nextRefreshRealtime;
    private static int revision;
    private static SqueakDiagnosticsPanel? panel;

    /// <summary>Bumped whenever a snapshot entry is updated or removed; the panel rebuilds its formatted cache on change.</summary>
    internal static int Revision => revision;

    internal static SqueakDiagnosticsMode Mode => mode;

    internal static Pawn? SelectedPawn => selectedPawn;

    /// <summary>Read-only entry access for the panel. Only read during window draw; the overlay mutates only during refresh.</summary>
    internal static IReadOnlyList<CachedPawn> CachedPawns => cachedPawns;

    /// <summary>The single readiness rule shared by the mark color and the panel badges.</summary>
    internal static bool ReadyFor(SqueakDiagnosticSnapshot s) => s.EffectiveTimingReady && s.CurrentActionEnabled
        && s.VocalCapability.VocalOrganEfficiency > SqueakVocalCapability.VocalSilenceThreshold;

    public static void SetMode(SqueakDiagnosticsMode newMode)
    {
        ClearSession();
        if (newMode == SqueakDiagnosticsMode.Off)
        {
            return;
        }

        if (Find.CurrentMap == null)
        {
            return;
        }

        mode = newMode;
        cachedMap = Find.CurrentMap;
        CompSqueaker.DiagnosticsEnabled = true;
        OpenPanel();
    }

    /// <summary>Per-frame teardown guard. It must remain free of snapshot, formatting, translation, and draw work.</summary>
    public static void MaintainLifecycle()
    {
        if (mode == SqueakDiagnosticsMode.Off)
        {
            return;
        }

        Map? map = Find.CurrentMap;
        if (map == null || !ReferenceEquals(cachedMap, map))
        {
            ClearSession();
        }
    }

    /// <summary>Layout-only lifecycle and snapshot work. Never call from a draw path.</summary>
    public static void RefreshIfDue()
    {
        try
        {
            RefreshIfDueCore();
        }
        catch (Exception ex)
        {
            // Diagnostics must fail closed: a modded pawn/snapshot exception never breaks the game frame.
            Log.Warning("[UniversalSqueaker] Diagnostics refresh failed: " + SqueakLogText.SanitizeExceptionMessage(ex.Message));
        }
    }

    private static void RefreshIfDueCore()
    {
        MaintainLifecycle();

        if (mode == SqueakDiagnosticsMode.Off)
        {
            return;
        }

        // Keep diagnostics fresh even when production population scaling is disabled. Individual
        // Comp diagnostic snapshots only read the resulting immutable shared state.
        CompSqueaker.MaintainPeriodicPopulationDiagnostics();

        Map map = cachedMap!;

        float now = Time.realtimeSinceStartup;
        if (mode == SqueakDiagnosticsMode.Selected)
        {
            Pawn? pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (!ReferenceEquals(pawn, selectedPawn) || now >= nextRefreshRealtime)
            {
                RefreshSelected(pawn, now);
            }
        }
        else if (now >= nextRefreshRealtime)
        {
            RefreshVisible(map, now);
        }
    }

    /// <summary>No map-level cached drawing in US: on-pawn marks are drawn by CompSqueaker.PostDraw.</summary>
    public static void DrawCached() { }

    private static void RefreshSelected(Pawn? pawn, float now)
    {
        selectedPawn = pawn;
        nextRefreshRealtime = now + SelectedRefreshSeconds;
        refreshedPawns.Clear();
        CellRect view = Find.CameraDriver.CurrentViewRect.ExpandedBy(1);
        if (pawn?.Spawned == true && !pawn.Dead && pawn.MapHeld == cachedMap && view.Contains(pawn.Position))
        {
            CompSqueaker? comp = pawn.GetComp<CompSqueaker>();
            if (comp != null)
            {
                RefreshSnapshot(pawn, comp);
            }
        }

        RemoveUnrefreshedPawns();
    }

    private static void RefreshVisible(Map map, float now)
    {
        nextRefreshRealtime = now + VisibleRefreshSeconds;
        CellRect view = Find.CameraDriver.CurrentViewRect.ExpandedBy(1);
        IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
        refreshedPawns.Clear();
        for (int i = 0; i < pawns.Count && refreshedPawns.Count < MaxVisiblePawns; i++)
        {
            Pawn pawn = pawns[i];
            if (pawn.Dead || !view.Contains(pawn.Position))
            {
                continue;
            }

            CompSqueaker? comp = pawn.GetComp<CompSqueaker>();
            if (comp != null)
            {
                RefreshSnapshot(pawn, comp);
            }
        }

        RemoveUnrefreshedPawns();
    }

    private static void RefreshSnapshot(Pawn pawn, CompSqueaker comp)
    {
        try
        {
            if (!entriesByPawn.TryGetValue(pawn, out CachedPawn? entry))
            {
                comp.ResetDiagnosticState();
                entry = new CachedPawn { Pawn = pawn, Comp = comp };
                entriesByPawn.Add(pawn, entry);
                cachedPawns.Add(entry);
            }

            refreshedPawns.Add(pawn);
            entry.Snapshot = comp.GetDiagnosticSnapshot();
            entry.MarkText = Mark;
            entry.MarkColor = MarkColorFor(entry.Snapshot);
            revision++;
        }
        catch (Exception ex)
        {
            // Fail closed: do not let one pawn's snapshot exception abort the overlay refresh loop.
            Log.Warning("[UniversalSqueaker] Diagnostics snapshot failed for " + pawn.LabelShort + ": " + SqueakLogText.SanitizeExceptionMessage(ex.Message));
        }
    }

    /// <summary>Three-state mark color: green = ready; amber = deterministic block; blue = random/parameter gate pending.</summary>
    private static Color MarkColorFor(SqueakDiagnosticSnapshot s)
    {
        if (ReadyFor(s))
        {
            return ReadyColor;
        }

        if (s.StartupPending || !s.Timing.ActionReady || (s.Timing.GlobalApplicable && !s.Timing.GlobalReady)
            || s.VocalCapability.VocalOrganEfficiency <= SqueakVocalCapability.VocalSilenceThreshold)
        {
            return BlockedColor;
        }

        return PendingColor;
    }

    /// <summary>Read-side mark lookup used by CompSqueaker.PostDraw (public draw path).</summary>
    internal static bool TryGetMark(Pawn pawn, out string mark, out Color color)
    {
        if (mode != SqueakDiagnosticsMode.Off && entriesByPawn.TryGetValue(pawn, out CachedPawn? entry))
        {
            mark = entry.MarkText;
            color = entry.MarkColor;
            return true;
        }

        mark = string.Empty;
        color = Color.white;
        return false;
    }

    private static void RemoveUnrefreshedPawns()
    {
        for (int i = cachedPawns.Count - 1; i >= 0; i--)
        {
            CachedPawn entry = cachedPawns[i];
            if (!refreshedPawns.Contains(entry.Pawn))
            {
                entry.Comp.ResetDiagnosticState();
                entriesByPawn.Remove(entry.Pawn);
                cachedPawns.RemoveAt(i);
                revision++;
            }
        }
    }

    private static void ClearSession()
    {
        ClearTrackedPawns();
        cachedMap = null;
        selectedPawn = null;
        nextRefreshRealtime = 0f;
        mode = SqueakDiagnosticsMode.Off;
        CompSqueaker.DiagnosticsEnabled = false;
        ClosePanel();
    }

    private static void ClosePanel()
    {
        SqueakDiagnosticsPanel? current = panel;
        panel = null;
        // Null the reference before Close() so the panel's PreClose -> NotifyPanelClosed
        // cannot re-enter ClosePanel (the window is still in the stack during PreClose).
        if (current != null && current.IsOpen)
        {
            current.Close();
        }
    }

    /// <summary>Called from the panel's PreClose: the user closed the panel (X/Esc) — tear down the whole diagnostics session. Never re-enters window close.</summary>
    internal static void NotifyPanelClosed()
    {
        panel = null;
        ClearTrackedPawns();
        cachedMap = null;
        selectedPawn = null;
        nextRefreshRealtime = 0f;
        mode = SqueakDiagnosticsMode.Off;
        CompSqueaker.DiagnosticsEnabled = false;
    }

    internal static void OpenPanel()
    {
        SqueakDiagnosticsPanel diagnosticsPanel = new();
        panel = diagnosticsPanel;
        Find.WindowStack.Add(diagnosticsPanel);
    }

    private static void ClearTrackedPawns()
    {
        for (int i = 0; i < cachedPawns.Count; i++)
        {
            cachedPawns[i].Comp.ResetDiagnosticState();
        }
        cachedPawns.Clear();
        entriesByPawn.Clear();
        refreshedPawns.Clear();
    }
}
