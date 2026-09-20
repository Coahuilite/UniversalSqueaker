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

    /// <summary>Five keys, four of them matched by the probe query, and the matches are NOT contiguous.</summary>
    private static readonly VoicePackRowView[] Packs =
    {
        Row("us.alpha", "Alpha Voice Pack", "def.alpha", "alpha"),
        Row("us.beta", "Beta Voice Pack", "def.beta", "beta"),
        Row("us.gamma", "Gamma Voice Pack", "def.gamma", "gamma"),
        Row("us.alpha2", "Alpha Extra Pack", "def.alpha2", "alpha extra"),
        Row("us.delta", "Delta Voice Pack", "def.delta", "delta")
    };

    private const string NarrowingQuery = "alpha";

    public static int RunAll()
    {
        Step("the filtered item keys pass the predicate in both directions, in order", TheFilteredItemKeysDoNotLie);
        Step("the projection never hands a repeat element a duplicate or blank identity", TheProjectionRefusesDuplicateAndBlankIdentities);
        Step("no selected domain projects an empty list rather than throwing", NoSelectedDomainProjectsAnEmptyList);
        Step("the widget row loop reads the projected predicate instead of a private copy", TheWidgetRowLoopReadsTheProjectedPredicate);
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
    /// The other half of "the same predicate": the drawn row loop must read the projected rule rather than
    /// keeping a private copy of it. Re-inlining the matching rule in the widget reddens this, which is the
    /// mutation it exists for - otherwise the list and the screen could filter differently and this lane
    /// would still be green.
    /// </summary>
    private static void TheWidgetRowLoopReadsTheProjectedPredicate()
    {
        string path = Path.Combine(
            RepoRoot(), "Source", "UniversalSqueaker", "UI", "Kernel", "UsVoicePackChecklistWidget.cs");
        string source = File.ReadAllText(path);
        Assert(source.Contains("UsChecklistFilter.Matches("),
            "the checklist widget's row loop must read UsChecklistFilter.Matches: " + path);
        Assert(!source.Contains("private static bool MatchesSearch"),
            "the checklist widget must not keep a second copy of the search predicate: " + path);
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

    private static VoicePackRowView Row(string key, string label, string defName, string searchText)
    {
        return new VoicePackRowView(key, label, "TestMod", "AuthorA", defName, "full", searchText, isSelected: false);
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
