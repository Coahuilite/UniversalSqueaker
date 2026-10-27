using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalSqueaker.UI;

/// <summary>Window-local browsing only; each list owns a separate instance, never settings writes.</summary>
public sealed class UsBrowseListState
{
    public string Query = "", SourcePackageId = "", SourceQuery = "";
    public UsBoardBrowsePolicy.BoardGroup Group = UsBoardBrowsePolicy.BoardGroup.PlayerRaces;
    public int Page, SourcePage;
    public bool SourcesOpen;
    public void Clear()
    {
        Query = SourcePackageId = SourceQuery = ""; Page = SourcePage = 0;
        SourcesOpen = false;
        Group = UsBoardBrowsePolicy.BoardGroup.PlayerRaces;
    }
}

public readonly struct UsBrowseItem
{
    public readonly string Value, Label, SourceId, SourceName;
    public readonly UsRaceIdentity? Race;
    public UsBrowseItem(string value, string label, string sourceId = "", string sourceName = "", UsRaceIdentity? race = null)
    { Value = value; Label = label; SourceId = sourceId; SourceName = sourceName; Race = race; }
}

/// <summary>Complete eligibility, then conjunction, stable order, then bounded presentation.</summary>
public sealed class UsBrowseListView
{
    public static readonly UsBrowseListView Empty = Project(Array.Empty<UsBrowseItem>(), new UsBrowseListState(), false);
    public IReadOnlyList<UsBrowseItem> Rows { get; private set; } = Array.Empty<UsBrowseItem>();
    public IReadOnlyList<UsBrowseItem> Sources { get; private set; } = Array.Empty<UsBrowseItem>();
    public int Eligible, Filtered, Page, Pages, SourceCount, SourcePage, SourcePages;
    public string SelectedSourceLabel = "";
    public static bool Contains(string text, string query) => string.IsNullOrWhiteSpace(query)
        || text.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    public static IReadOnlyList<T> Slice<T>(IReadOnlyList<T> rows, int requestedPage, int size, out int page, out int pages)
    {
        pages = Math.Max(1, (rows.Count + size - 1) / size);
        page = Math.Max(0, Math.Min(requestedPage, pages - 1));
        return rows.Skip(page * size).Take(size).ToArray();
    }
    public static UsBrowseListView Project(IEnumerable<UsBrowseItem> eligible, UsBrowseListState state, bool raceList, int size = 8)
    {
        var all = eligible.ToArray();
        var rows = all.Where(x => (!raceList || x.Race.HasValue && UsBoardBrowsePolicy.InGroup(x.Race.Value, state.Group))
            && (state.SourcePackageId == "" || x.SourceId == state.SourcePackageId)
            && (Contains(x.Label, state.Query) || Contains(x.Value, state.Query)
                || Contains(x.SourceId, state.Query) || Contains(x.SourceName, state.Query))).ToList();
        rows.Sort((a, b) =>
        {
            if (a.Race.HasValue && b.Race.HasValue) return UsBoardBrowsePolicy.Compare(a.Race.Value, b.Race.Value);
            int label = string.CompareOrdinal(a.Label, b.Label);
            return label != 0 ? label : string.CompareOrdinal(a.Value, b.Value);
        });
        var sourceGroups = all.Where(x => x.SourceId != "").GroupBy(x => x.SourceId, StringComparer.Ordinal)
            .Select(g => new UsBrowseItem(g.Key, g.Select(x => x.SourceName).FirstOrDefault(x => x != "") ?? g.Key)).ToArray();
        var duplicateNames = new HashSet<string>(sourceGroups.GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
        var sources = sourceGroups.Select(x => new UsBrowseItem(x.Value, duplicateNames.Contains(x.Label) ? x.Value + " · " + x.Label : x.Label))
            .Where(x => Contains(x.Label, state.SourceQuery) || Contains(x.Value, state.SourceQuery))
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Value, StringComparer.Ordinal).ToArray();
        var view = new UsBrowseListView { Eligible = all.Length, Filtered = rows.Count, SourceCount = sources.Length };
        view.SelectedSourceLabel = sourceGroups.FirstOrDefault(x => x.Value == state.SourcePackageId).Label ?? state.SourcePackageId;
        view.Rows = Slice(rows, state.Page, size, out view.Page, out view.Pages);
        view.Sources = Slice(sources, state.SourcePage, 5, out view.SourcePage, out view.SourcePages);
        return view;
    }
}
