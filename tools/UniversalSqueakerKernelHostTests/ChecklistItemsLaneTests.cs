using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// The "the filtered key list does not lie" lane (docs/ui-redesign-0.7-zh.md 5.6, B-1). It exists because
/// the projection and the search box are two clocks: the search write bumps the content revision and the
/// <c>checklist-pack-keys</c> binding is recomputed by the host, so a list built from the PREVIOUS frame's
/// query - or one that skipped the predicate in either direction - draws a perfectly tidy list of the wrong
/// rows with nothing on screen to say so. Both directions are asserted, because one alone is blind: a list
/// with extra keys lies, a list with missing keys silently drops data.
/// <para>
/// What it asserts, in the order the spec fixes: the baseline (no search) IS the selected domain's pack keys
/// in source order with the source's count; after a search that accepts a strict, non-contiguous subset the
/// list is exactly the accepted keys, in source order, with no duplicate; every listed key passes the
/// production predicate AND every key the predicate accepts is listed; and clearing the search returns the
/// list to the baseline element by element, so the lane cannot pass on a list that never changes.
/// </para>
/// <para>
/// The predicate the lane compares against is the PRODUCT's (<see cref="UsChecklistFilter.Matches"/>), not a
/// copy: a lane that restated the matching rule would agree with itself while the page disagreed with both.
/// The source-invariant clause at the end pins the other half of that claim - the composite widget's row loop
/// must keep reading the same predicate.
/// </para>
/// </summary>
internal static class ChecklistItemsLaneTests
{
    private const string ItemsKey = "checklist-pack-keys";

    /// <summary>Five keys, two of them matched by the probe query, and the matches are NOT contiguous.</summary>
    private static readonly VoicePackRowView[] Packs =
    {
        Row("us.alpha", "Alpha Voice Pack", "def.alpha", "alpha"),
        Row("us.beta", "Beta Voice Pack", "def.beta", "beta", selected: true),
        Row("us.gamma", "Gamma Voice Pack", "def.gamma", "gamma"),
        Row("us.alpha2", "Alpha Extra Pack", "def.alpha2", "alpha extra"),
        Row("us.delta", "Delta Voice Pack", "def.delta", "delta")
    };

    private const string NarrowingQuery = "alpha";

    /// <summary>The Repeat element's declared Id and the template root's: the row identity is &lt;root&gt;#&lt;key&gt;.</summary>
    private const string RepeatId = "checklist-rows";
    private const string TemplateRootId = "checklist-row";

    public static int RunAll()
    {
        Step("the filtered item keys pass the predicate in both directions, in order", TheFilteredItemKeysDoNotLie);
        Step("the materialized rows are the list, and their item-local keys resolve", TheMaterializedRowsAreTheList);
        Step("the projection never hands a repeat element a duplicate or blank identity", TheProjectionRefusesDuplicateAndBlankIdentities);
        Step("no selected domain projects an empty list rather than throwing", NoSelectedDomainProjectsAnEmptyList);
        Step("one predicate, one projection, and no second row loop", TheWidgetRowLoopReadsTheProjectedPredicate);
        Console.WriteLine("ChecklistItemsLaneTests ALL PASS");
        return 0;
    }

    private static void TheFilteredItemKeysDoNotLie()
    {
        var fake = new RecordingSettingsSource { RichData = true, ChecklistPacks = Packs };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Packs");
        Rect viewport = new(0f, 0f, 800f, 600f);
        host.DrawChecked(viewport);

        // Baseline: no search, so the list must be the source's own pack keys - count, order and membership.
        List<string> baseline = Items(host);
        Assert(baseline.Count == Packs.Length,
            "an empty search must list every pack of the selected domain: " + Describe(baseline) + " vs " + Packs.Length);
        for (int i = 0; i < Packs.Length; i++)
        {
            Assert(baseline[i] == Packs[i].Key,
                "the baseline list must keep the source's order at " + i + ": " + Describe(baseline));
        }

        // The write and the list it produces must be the SAME frame's answer: one arrange after the search
        // write, with no extra frame in between. A projection that read a captured or previous-frame query
        // reddens here (failure mode 1 in 5.6).
        host.Bindings.Set("search-text", NarrowingQuery);
        host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
        List<string> filtered = Items(host);

        Assert(fake.LastSearchText == NarrowingQuery && fake.ViewState.SearchText == NarrowingQuery,
            "the search write must reach the page state the projection reads (fake.LastSearchText="
            + (fake.LastSearchText ?? "(null)") + ", state=" + fake.ViewState.SearchText + ")");
        Assert(filtered.Count < baseline.Count,
            "the probe query must narrow the list or this lane asserts nothing: " + Describe(filtered));

        // Direction 1 (no extra keys): every listed key passes the CURRENT predicate.
        foreach (string key in filtered)
        {
            Assert(Passes(key, NarrowingQuery),
                "the list contains a key the search rejects (the list would lie): '" + key + "' in " + Describe(filtered));
        }

        // Direction 2 (no missing keys): every key the predicate accepts is listed, in source order.
        List<string> accepted = Accepted(NarrowingQuery);
        Assert(accepted.Count > 0 && accepted.Count < Packs.Length,
            "the probe query must accept a strict subset, or the both-directions clauses below are vacuous: "
            + Describe(accepted));
        AssertSequence(filtered, accepted, "the list must be exactly the accepted keys in source order");

        // Duplicate identity inside the list: Repeat reuses nodes by key, so a repeated key is a state
        // misplacement as well as a refused row.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in filtered)
        {
            Assert(seen.Add(key), "a projected key must appear once: '" + key + "' is listed twice");
        }

        // Reversibility: clearing the search must return the list to the baseline element by element, which
        // is what stops this lane from passing on a list that simply never changes.
        host.Bindings.Set("search-text", "");
        host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
        List<string> cleared = Items(host);
        AssertSequence(cleared, baseline, "clearing the search must restore the baseline list exactly");

        // And the narrowing is reversible rather than one-way: the same query narrows the same way again.
        host.Bindings.Set("search-text", NarrowingQuery);
        host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
        AssertSequence(Items(host), filtered, "re-applying the search must produce the same list again");
    }

    /// <summary>
    /// The row-identity contract the engine applies to whatever the projection supplies: it refuses a blank
    /// key and a key a sibling row already used by refusing the ROW - no element, no node, no state, and a
    /// report instead of an exception. So the projection must never hand it either shape; a list that did
    /// would make a row vanish on screen with nothing to see.
    /// </summary>
    private static void TheProjectionRefusesDuplicateAndBlankIdentities()
    {
        var dirty = new[]
        {
            Row("us.alpha", "Alpha Voice Pack", "def.alpha", "alpha"),
            new VoicePackRowView("", "Blank Key Pack", "TestMod", "AuthorA", "def.blank", "full", "blank", isSelected: false),
            Row("us.alpha", "Alpha Duplicate Pack", "def.alpha.dup", "alpha dup"),
            Row("us.beta", "Beta Voice Pack", "def.beta", "beta")
        };

        var fake = new RecordingSettingsSource { RichData = true, ChecklistPacks = dirty };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Packs");
        List<string> items = Items(host);

        AssertSequence(items, new List<string> { "us.alpha", "us.beta" },
            "a blank key and a repeated key must collapse to one entry each, in source order");
        Assert(items.Count == 2, "the projected list must carry one entry per distinct non-blank key: " + Describe(items));
    }

    /// <summary>The empty case is a state, not a failure: no selected domain projects an empty list.</summary>
    private static void NoSelectedDomainProjectsAnEmptyList()
    {
        var fake = new RecordingSettingsSource { RichData = false };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        List<string> items = Items(host);
        Assert(items.Count == 0, "no selected domain must project an empty list, got " + Describe(items));
    }

    /// <summary>
    /// The structural half of "one predicate, one list". Step B deleted the widget's row loop, so the
    /// invariant is no longer "the row loop reads the shared rule" but the stronger "there is exactly one
    /// predicate, exactly one projection, and nothing else filters rows": re-inlining a predicate or a
    /// second row loop reddens this, which is the mutation it exists for.
    /// </summary>
    private static void TheWidgetRowLoopReadsTheProjectedPredicate()
    {
        string root = RepoRoot();
        string widgetPath = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsVoicePackChecklistWidget.cs");
        string widget = File.ReadAllText(widgetPath);
        Assert(!widget.Contains("private static bool MatchesSearch"),
            "the checklist widget must not keep a second copy of the search predicate: " + widgetPath);
        Assert(!widget.Contains("domain.Packs"),
            "the checklist widget must not iterate the domain's packs any more - the row set is the projection's: " + widgetPath);

        string hostPath = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "UsKernelSettingsHost.cs");
        Assert(File.ReadAllText(hostPath).Contains("UsChecklistFilter.Keys("),
            "the checklist-pack-keys binding must read the one projection function: " + hostPath);

        string filterPath = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Layout", "UsChecklistFilter.cs");
        Assert(File.ReadAllText(filterPath).Contains("public static bool Matches("),
            "the one predicate must live in UsChecklistFilter: " + filterPath);
    }

    /// <summary>
    /// The engine-side half of the same claim, and the only place failure modes 4 and 5 of 5.6 are visible
    /// at all. The projected list is not the screen until the engine materializes a row per key, so this
    /// step reads the ARRANGED tree (the Repeat's child nodes, in layout order) and requires it to be the
    /// list exactly - same count, same order, same keys. It then reads each row's item-local bindings and
    /// requires them to resolve to THAT row's own data rather than the library's fail-soft default (which is
    /// what a checkbox or a bound string draws for an absent item-local key, with nothing on screen to say
    /// so), and finally reads the engine's own report channel: a refused row identity or an unresolved
    /// item-local key is recorded there, so an absence of findings is the assertion.
    /// </summary>
    private static void TheMaterializedRowsAreTheList()
    {
        var fallbacks = new List<UiStyleFallbackReport>();
        UiFitAudit.AttachStyleFallback(fallbacks.Add);
        UiFitAudit.Reset();
        try
        {
            var fake = new RecordingSettingsSource { RichData = true, ChecklistPacks = Packs };
            using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
            host.Bindings.Invoke("set-tab", "Packs");
            Rect viewport = new(0f, 0f, 800f, 600f);
            host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
            host.DrawChecked(viewport);

            List<string> items = Items(host);
            AssertSequence(MaterializedRows(host), items,
                "the arranged rows must be the projected list, in order (Repeat reuses a node by key, so a reordered list is a misplaced state)");

            foreach (string key in items)
            {
                VoicePackRowView row = FixtureRow(key);
                Assert(ReadString(host, key, "label") == row.Label,
                    "the item-local label of '" + key + "' must read that row's own label, got '" + ReadString(host, key, "label") + "'");
                Assert(ReadString(host, key, "coverage") == row.Coverage,
                    "the item-local coverage of '" + key + "' must read that row's own coverage, got '" + ReadString(host, key, "coverage") + "'");
                Assert(ReadBool(host, key, "enabled") == row.IsSelected,
                    "the item-local enabled of '" + key + "' must read that row's own state, got " + ReadBool(host, key, "enabled"));
            }

            // The positive control for the fail-soft clause: the fixture carries one selected and one
            // unselected row, so an item-local read that silently answered the default would be visible.
            Assert(ReadBool(host, "us.beta", "enabled"), "the selected fixture row must read true");
            Assert(!ReadBool(host, "us.alpha", "enabled"), "the unselected fixture row must read false");

            // Narrowing must remove rows from the ARRANGED tree, not merely hide them: the removed key owns
            // no row node at all, which is what makes the list and the screen one answer.
            host.Bindings.Set("search-text", NarrowingQuery);
            host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
            host.DrawChecked(viewport);

            items = Items(host);
            List<string> rows = MaterializedRows(host);
            AssertSequence(rows, items, "the arranged rows must be the narrowed list, in order");
            Assert(rows.Count < Packs.Length, "the search must actually remove rows from the tree: " + Describe(rows));
            foreach (string key in new[] { "us.beta", "us.gamma", "us.delta" })
            {
                Assert(!rows.Contains(key),
                    "a key the search rejects must own no row node: '" + key + "' is still arranged");
            }

            // The engine's report channel: a refused row identity and an unresolved item-local key are both
            // recorded here (UiFitAudit.ReportStyleFallback). Any finding on a checklist row is a failure of
            // this lane's claim, so the assertion is the absence.
            foreach (UiStyleFallbackReport report in fallbacks)
            {
                Assert(report.ElementPath.IndexOf(TemplateRootId, StringComparison.Ordinal) < 0,
                    "no checklist row may be refused or draw an unresolved item-local key: " + report.Diagnostic);
            }
        }
        finally
        {
            UiFitAudit.Detach();
        }
    }

    /// <summary>The Repeat's arranged rows, in layout order, read from the session's node tree.</summary>
    private static List<string> MaterializedRows(UiHost host)
    {
        UiNode? repeat = host.Session.GetNodeByElementId(RepeatId);
        if (repeat == null)
        {
            throw new InvalidOperationException(
                "the manifest must declare the Repeat element '" + RepeatId + "' inside the checklist card");
        }

        string prefix = TemplateRootId + "#";
        var rows = new List<string>();
        foreach (UiNode child in repeat.Children)
        {
            if (child.ElementId.StartsWith(prefix, StringComparison.Ordinal))
            {
                rows.Add(child.ElementId.Substring(prefix.Length));
            }
        }

        return rows;
    }

    /// <summary>The item-local binding key the engine composes for one row's declared key.</summary>
    private static string ItemKey(string key, string declared) => ItemsKey + "." + key + "." + declared;

    private static string ReadString(UiHost host, string key, string declared)
    {
        string itemKey = ItemKey(key, declared);
        Assert(host.Bindings.TryGet<string>(itemKey, out string value),
            "the projection must have registered '" + itemKey + "'");
        return value ?? "";
    }

    private static bool ReadBool(UiHost host, string key, string declared)
    {
        string itemKey = ItemKey(key, declared);
        Assert(host.Bindings.TryGet<bool>(itemKey, out bool value),
            "the projection must have registered '" + itemKey + "'");
        return value;
    }

    private static VoicePackRowView FixtureRow(string key)
    {
        foreach (VoicePackRowView row in Packs)
        {
            if (row.Key == key) return row;
        }

        throw new InvalidOperationException("the fixture has no row '" + key + "'");
    }

    // --- helpers ---------------------------------------------------------------------------------

    /// <summary>The projected list as the host publishes it right now.</summary>
    private static List<string> Items(UiHost host)
    {
        if (!host.Bindings.TryGet<IReadOnlyList<string>>(ItemsKey, out IReadOnlyList<string> keys) || keys == null)
        {
            throw new InvalidOperationException(
                "the host must register the '" + ItemsKey + "' IReadOnlyList<string> value binding the declarative row set reads");
        }

        return new List<string>(keys);
    }

    /// <summary>The keys the PRODUCT's predicate accepts, in source order, one per distinct key.</summary>
    private static List<string> Accepted(string query)
    {
        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (VoicePackRowView row in Packs)
        {
            if (string.IsNullOrEmpty(row.Key) || !seen.Add(row.Key)) continue;
            if (UsChecklistFilter.Matches(row, query)) keys.Add(row.Key);
        }

        return keys;
    }

    /// <summary>
    /// Whether the row with this key passes the production predicate for the given query. Resolved through
    /// the product rule, never through a copy of it.
    /// </summary>
    private static bool Passes(string key, string query)
    {
        foreach (VoicePackRowView row in Packs)
        {
            if (row.Key == key) return UsChecklistFilter.Matches(row, query);
        }

        return false;
    }

    private static void AssertSequence(List<string> actual, List<string> expected, string because)
    {
        Assert(actual.Count == expected.Count,
            because + ": count " + actual.Count + " vs " + expected.Count
            + " (actual " + Describe(actual) + ", expected " + Describe(expected) + ")");
        for (int i = 0; i < expected.Count; i++)
        {
            Assert(actual[i] == expected[i],
                because + ": entry " + i + " is '" + actual[i] + "', expected '" + expected[i] + "'");
        }
    }

    private static string Describe(List<string> keys)
    {
        return "[" + string.Join(", ", keys) + "]";
    }

    private static VoicePackRowView Row(string key, string label, string defName, string searchText, bool selected = false)
    {
        return new VoicePackRowView(key, label, "TestMod", "AuthorA", defName, "full", searchText, selected);
    }

    /// <summary>The same root marker the harness's own RepoRoot walks to: the repository, not its bin folder.</summary>
    private static string RepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "verify-local.ps1"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("cannot locate the repository root from " + AppContext.BaseDirectory);
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ChecklistItemsLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
