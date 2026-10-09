using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// The checklist's ONE row predicate and the ordered key projection built from it. Both halves exist here
/// rather than inside the widget because there are two consumers and they must not drift: the composite
/// widget's row loop (still the drawing path today) and the <c>checklist-pack-keys</c> value binding that
/// the declarative <c>Repeat</c> reads its row identities from.
/// <para>
/// <b>Why the projection is not a second filter.</b> The projected list IS the screen: the engine
/// materializes one row per key, so a key the predicate rejects must not be listed ("the list would lie")
/// and a key the predicate accepts must be listed ("the list would silently drop data"). The two
/// directions are asserted by <c>ChecklistItemsLaneTests</c> against this very predicate, which is why the
/// predicate is public: a lane that restated the matching rule would agree with itself instead of with the
/// page.
/// </para>
/// <para>
/// <b>Identity hygiene belongs to the projection.</b> <c>Repeat</c> refuses a blank key and a key that a
/// sibling row already used by refusing the ROW - no element, no node, no state, and a report rather than
/// an exception (<c>UiLayoutEngine.AcceptItemKey</c>). A projection that handed it either shape would make
/// a row vanish on screen with nothing to see, so the first occurrence of a key wins and a blank key is
/// dropped here, where the defect can still be reported as "the source list is wrong" instead of as a row
/// that is simply missing.
/// </para>
/// </summary>
public static class UsChecklistFilter
{
    /// <summary>
    /// The one search predicate: the trimmed query is matched case-insensitively against the row's search
    /// blob and its three identity strings; an empty query accepts every row. Kept byte-for-byte equivalent
    /// to the rule the widget's row loop used before the projection existed, because a difference between
    /// the two would show up as rows the list has and the screen does not (or the reverse).
    /// </summary>
    public static bool Matches(VoicePackRowView row, string query)
        => QueryMatches(query, row.SearchText, row.Label, row.DefName, row.Key);

    /// <summary>
    /// The ONE substring rule, shared by the pack checklist and (since D4) the two domain lists: the
    /// trimmed query must appear case-insensitively in at least one of the given fields; an empty query
    /// accepts everything. The PM ruling of 2026-10-07 keeps exactly this rule for all three search
    /// boxes - no tokenization, no fuzzy option - so "search finds it, search finds it the same way"
    /// holds across the page, and one function is where the rule lives.
    /// </summary>
    public static bool QueryMatches(string query, params string[] fields)
    {
        if (query == null || query.Trim().Length == 0) return true;
        string needle = query.Trim();
        for (int i = 0; i < fields.Length; i++)
        {
            string field = fields[i];
            if (field != null && field.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    /// <summary>
    /// The ordered item keys of <paramref name="domain"/> that the search accepts: source order, one entry
    /// per distinct key, no blank entry. Order is part of the contract rather than a detail - <c>Repeat</c>
    /// reuses a row's node and state by key, so a reordered list is how a state lands on the wrong row.
    /// </summary>
    public static IReadOnlyList<string> Keys(VoicePackDomainView domain, string search)
    {
        IReadOnlyList<VoicePackRowView> packs = domain.Packs;
        if (packs == null || packs.Count == 0) return Array.Empty<string>();

        var keys = new List<string>(packs.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < packs.Count; i++)
        {
            VoicePackRowView row = packs[i];
            if (string.IsNullOrEmpty(row.Key)) continue;
            if (!Matches(row, search)) continue;
            if (!seen.Add(row.Key)) continue;
            keys.Add(row.Key);
        }

        return keys;
    }
}
