using System;
using System.Collections.Generic;

namespace UniversalSqueaker;

/// <summary>
/// Pure role state machine of the diagnostics session (round-9 contract). Keys are opaque
/// objects (the Verse driver passes Pawns; the harness passes dummies), so every rule is
/// testable without Verse: a pawn is tracked while it holds ANY role - swept into the viewport,
/// live-selected, or locked - and is released only when it holds none (the driver prunes its
/// entry registry against <see cref="IsTracked"/>). The viewport list keeps the stable order the
/// driver supplies, so paging never jitters while pawns enter or leave the screen. Revision is
/// the single clock: it counts membership changes, selection switches, and snapshot updates
/// (bumped by the driver), and every formatted-text cache must expire on it - never a wall clock.
/// </summary>
public sealed class SqueakDiagnosticsSessionModel<TKey>
{
    private readonly List<TKey> viewportOrder = new();
    private readonly HashSet<TKey> viewportSet = new();
    private readonly List<TKey> locked = new();
    private readonly HashSet<TKey> lockedSet = new();
    private readonly IEqualityComparer<TKey> keyEq = EqualityComparer<TKey>.Default;
    private TKey? selected;
    private bool hasSelected;
    private int revision;

    public int Revision => revision;
    public IReadOnlyList<TKey> ViewportOrder => viewportOrder;
    public IReadOnlyList<TKey> Locked => locked;
    public bool HasSelected => hasSelected;
    public TKey? Selected => selected;

    public bool IsLocked(TKey key) => lockedSet.Contains(key);

    /// <summary>True while the key holds any role (viewport, selection, or lock).</summary>
    public bool IsTracked(TKey key) => viewportSet.Contains(key) || lockedSet.Contains(key)
        || (hasSelected && keyEq.Equals(selected!, key));

    /// <summary>Replaces the viewport role set with one sweep result (already in stable order).</summary>
    public void ReplaceViewport(IReadOnlyList<TKey> orderedKeys)
    {
        bool membershipChanged = orderedKeys.Count != viewportSet.Count;
        if (!membershipChanged)
        {
            for (int i = 0; i < orderedKeys.Count; i++)
            {
                if (!viewportSet.Contains(orderedKeys[i])) { membershipChanged = true; break; }
            }
        }

        viewportSet.Clear();
        viewportOrder.Clear();
        for (int i = 0; i < orderedKeys.Count; i++)
        {
            viewportSet.Add(orderedKeys[i]);
            viewportOrder.Add(orderedKeys[i]);
        }

        if (membershipChanged) revision++;
    }

    /// <summary>Switches the live-selection role; a switch bumps the revision so the detail column re-shows.</summary>
    public void SetSelected(TKey? key)
    {
        bool same = hasSelected == (key != null)
            && (!hasSelected || keyEq.Equals(selected!, key!));
        if (same) return;

        selected = key;
        hasSelected = key != null;
        revision++;
    }

    public bool Lock(TKey key)
    {
        if (!lockedSet.Add(key)) return false;
        locked.Add(key);
        revision++;
        return true;
    }

    public bool Unlock(TKey key)
    {
        if (!lockedSet.Remove(key)) return false;
        locked.RemoveAll(k => keyEq.Equals(k, key));
        revision++;
        return true;
    }

    /// <summary>Snapshot update clock entry: the driver bumps once per refreshed entry.</summary>
    public void BumpRevision() => revision++;

    /// <summary>Session teardown: every role and the base revision reset.</summary>
    public void Reset()
    {
        viewportOrder.Clear();
        viewportSet.Clear();
        locked.Clear();
        lockedSet.Clear();
        selected = default;
        hasSelected = false;
        revision++;
    }
}
