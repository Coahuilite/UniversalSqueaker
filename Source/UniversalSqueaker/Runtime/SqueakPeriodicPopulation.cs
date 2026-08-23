using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>Current-map membership plus a shared, bounded-cadence audible snapshot.</summary>
internal static class SqueakPeriodicPopulation
{
    private const int MovementRefreshTicks = 30;
    private static readonly Dictionary<Map, HashSet<CompSqueaker>> MembersByMap = new();
    private static Game? ownerGame;
    private static Map? snapshotMap;
    private static int snapshotTick = int.MinValue;
    private static int lastMaintenanceTick = int.MinValue;
    private static bool dirty = true;
    private static CellRect lastView;
    private static Vector3 lastListener;
    private static float lastMaximumDistance;

    internal readonly struct Snapshot
    {
        internal readonly int CandidateCount, AudibleCount, Tick;
        internal readonly CellRect View;
        internal readonly Vector3 Listener;
        internal readonly float MaximumDistance, Scale;
        internal readonly bool Stale;
        internal Snapshot(int candidateCount, int audibleCount, int tick, CellRect view, Vector3 listener, float maximumDistance, bool stale = false)
        {
            CandidateCount = Math.Max(0, candidateCount); AudibleCount = Math.Max(0, audibleCount); Tick = tick;
            View = view; Listener = listener; MaximumDistance = Mathf.Max(0f, maximumDistance);
            Scale = Mathf.Max(1f, AudibleCount / 10f); Stale = stale;
        }
        internal Snapshot AsStale() => new(CandidateCount, AudibleCount, Tick, View, Listener, MaximumDistance, true);
    }

    internal static readonly Snapshot EmptySnapshot = new(0, 0, int.MinValue, default, default, 0f, true);
    private static Snapshot snapshot = EmptySnapshot;

    /// <summary>Explicit maintenance boundary. Never call this from a read-only diagnostic getter.</summary>
    private static void EnsureOwner()
    {
        Game? game = Current.Game;
        if (ReferenceEquals(ownerGame, game)) return;
        ownerGame = game;
        MembersByMap.Clear(); snapshotMap = null; snapshotTick = int.MinValue; lastMaintenanceTick = int.MinValue;
        lastView = default; lastListener = default; lastMaximumDistance = 0f; snapshot = EmptySnapshot; dirty = true;
    }

    internal static void Register(CompSqueaker squeaker, Map? map)
    {
        EnsureOwner();
        if (map == null) return;
        if (!MembersByMap.TryGetValue(map, out HashSet<CompSqueaker>? members)) MembersByMap[map] = members = new();
        if (members.Add(squeaker)) dirty = true;
    }

    internal static void Unregister(CompSqueaker squeaker, Map? map)
    {
        EnsureOwner();
        if (map != null && MembersByMap.TryGetValue(map, out HashSet<CompSqueaker>? members) && members.Remove(squeaker))
        {
            if (members.Count == 0) MembersByMap.Remove(map);
            dirty = true;
        }
    }

    internal static void NotifyDistanceChanged() { EnsureOwner(); dirty = true; }

    internal static void RemoveMap(Map? map)
    {
        EnsureOwner();
        if (map == null) return;
        if (MembersByMap.Remove(map)) dirty = true;
        if (ReferenceEquals(snapshotMap, map)) { snapshotMap = null; snapshotTick = int.MinValue; snapshot = EmptySnapshot; }
    }

    /// <summary>Explicit diagnostic/production maintenance. At most one O(N) scan per game tick.</summary>
    internal static Snapshot Maintain(FloatRange audibleDistance)
    {
        EnsureOwner();
        Map? map = Find.CurrentMap;
        if (map == null) return EmptySnapshot;
        int now = Find.TickManager.TicksGame;
        // The first caller owns this tick. Membership changes afterwards defer to the next tick,
        // rather than allowing batch spawning to repeatedly rescan the registry.
        if (lastMaintenanceTick == now) return snapshot;
        lastMaintenanceTick = now;
        CellRect view = Find.CameraDriver.CurrentViewRect.ExpandedBy(10);
        Vector3 listener = Find.Camera.transform.position;
        float maximumDistance = Mathf.Max(0f, audibleDistance.max);
        bool observerChanged = snapshotMap != map || !SameRect(lastView, view)
            || (lastListener - listener).sqrMagnitude > .0001f || Math.Abs(lastMaximumDistance - maximumDistance) > .001f;
        if (dirty || observerChanged || now - snapshotTick >= MovementRefreshTicks) Rebuild(map, view, listener, maximumDistance, now);
        return snapshot;
    }

    /// <summary>Pure read: never resets ownership, rebuilds, iterates membership, or initializes state.</summary>
    internal static Snapshot GetSnapshot()
    {
        Game? game = Current.Game;
        if (game == null || !ReferenceEquals(ownerGame, game)) return EmptySnapshot;
        return dirty || Find.TickManager.TicksGame - snapshotTick >= MovementRefreshTicks ? snapshot.AsStale() : snapshot;
    }

    private static void Rebuild(Map map, CellRect view, Vector3 listener, float maximumDistance, int now)
    {
        int count = 0, candidates = 0;
        float limitSquared = maximumDistance * maximumDistance;
        if (MembersByMap.TryGetValue(map, out HashSet<CompSqueaker>? members))
        {
            foreach (CompSqueaker squeaker in members)
            {
                Pawn pawn = squeaker.RegisteredPawn;
                if (!pawn.Spawned || pawn.MapHeld != map || !view.Contains(pawn.Position)) continue;
                candidates++;
                Vector3 delta = pawn.DrawPos - listener;
                if (delta.sqrMagnitude <= limitSquared) count++;
            }
        }
        snapshotMap = map; snapshotTick = now; lastView = view;
        lastListener = listener; lastMaximumDistance = maximumDistance; dirty = false;
        snapshot = new Snapshot(candidates, count, now, view, listener, maximumDistance);
    }

    private static bool SameRect(CellRect a, CellRect b) => a.minX == b.minX && a.minZ == b.minZ && a.Width == b.Width && a.Height == b.Height;
}
