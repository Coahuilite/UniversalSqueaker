using System;
using System.Collections.Generic;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.UiLogicTests;

/// <summary>
/// Self-contained console gate for the zero-Verse UI pure logic.
/// Prints ALL GREEN and returns 0 on success; any assertion failure throws and returns non-zero.
/// </summary>
internal static class Program
{
    private static int Main()
    {
        try
        {
            RunAll();
            Console.WriteLine("ALL GREEN");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            return 1;
        }
    }

    private static void RunAll()
    {
        TestVoicePackModeDomain();
        TestVoicePacksFilters();
        TestAttenuationMath();
        TestActionScopeRules();
        TestRaceXenotypeFiltering();
        TestUsCardLayoutHeight();
        TestUsFilterBarLayout();
        TestHelpCatalog();
        TestHelpPanelLogic();
        UiSourceInvariantTests.RunAll();
        UsKernelContractInvariantTests.RunAll();
        UsDiagnosticsLogicTests.RunAll();
    }

    /// <summary>
    /// Anti-drift gate for the voice-pack routing-mode domain: SqueakVoicePackModes.IsKnown is the
    /// single authority the runtime resolver uses to accept or downgrade a persisted mode
    /// (unknown values Log.Error once and degrade to Vanilla). If someone adds a fifth enum member
    /// without extending the All table, this assertion goes red — silently absorbing a new mode as
    /// "unknown" would be a behaviour change the resolver's one-time error would bury in logs.
    /// </summary>
    private static void TestVoicePackModeDomain()
    {
        Array declared = Enum.GetValues(typeof(SqueakVoicePackMode));
        Assert(SqueakVoicePackModes.All.Length == declared.Length,
            "SqueakVoicePackModes.All must stay exactly as long as the SqueakVoicePackMode enum "
            + "(All.Length=" + SqueakVoicePackModes.All.Length + ", enum.Length=" + declared.Length
            + "); a new mode without a table entry is a silent behaviour change");

        for (int i = 0; i < declared.Length; i++)
        {
            Assert(SqueakVoicePackModes.All[i].Equals(declared.GetValue(i)),
                "SqueakVoicePackModes.All[" + i + "] must equal enum value " + declared.GetValue(i)
                + " in declaration order");
        }

        foreach (SqueakVoicePackMode mode in SqueakVoicePackModes.All)
        {
            Assert(SqueakVoicePackModes.IsKnown(mode),
                "every declared mode must be IsKnown: " + mode);
        }

        Assert(!SqueakVoicePackModes.IsKnown((SqueakVoicePackMode)(declared.Length + 100)),
            "a value outside the declared set must NOT be IsKnown (resolver degrades it to Vanilla)");
    }

    private static void TestUsCardLayoutHeight()
    {
        const float tolerance = 0.0001f;

        float withTitle = UsCardLayout.MeasureBody(10f, titleHidden: false);
        float withoutTitle = UsCardLayout.MeasureBody(10f, titleHidden: true);
        AssertEqual(
            UsCardLayout.Padding + UsCardLayout.HeaderHeight + UsCardLayout.HeaderGap + 10f + UsCardLayout.Padding,
            withTitle,
            tolerance,
            "UsCard with title includes header height and gap");
        AssertEqual(
            UsCardLayout.Padding + 10f + UsCardLayout.Padding,
            withoutTitle,
            tolerance,
            "UsCard with hidden title removes header height and gap");
        Assert(withoutTitle < withTitle, "TitleHidden cards measure shorter than titled cards");
        AssertEqual(withTitle - withoutTitle, UsCardLayout.HeaderHeight + UsCardLayout.HeaderGap, tolerance,
            "TitleHidden removes exactly HeaderHeight + HeaderGap");
        AssertEqual(
            UsCardLayout.Padding + UsCardLayout.Padding,
            UsCardLayout.MeasureBody(-5f, titleHidden: true),
            tolerance,
            "UsCard clamps negative body heights to zero");
    }

    private static void TestUsFilterBarLayout()
    {
        const float tolerance = 0.0001f;

        // 800px source window: content column leaves the filter bar a ~320px card body, so the
        // three dropdowns must stack full-width instead of three-up with a ~24px field.
        Assert(UsFilterBarLayout.DropdownsStack(320f), "800px-window body width stacks the dropdowns");
        AssertEqual(
            UsFilterBarLayout.RowHeight * 2f + UsFilterBarLayout.RowHeight * 2f + UsFilterBarLayout.Gap * 2f,
            UsFilterBarLayout.BodyHeight(320f),
            tolerance,
            "stacked body height covers the domain row plus three full-width dropdown rows");
        AssertEqual(
            UsFilterBarLayout.BodyHeight(320f),
            UsFilterBarLayout.ExtraDropdownRows(320f) + UsFilterBarLayout.RowHeight * 2f,
            tolerance,
            "BodyHeight is the two base rows plus the stacking delta");
        Assert(
            UsFilterBarLayout.BodyHeight(320f) > UsFilterBarLayout.RowHeight * 2f,
            "stacked layout is taller than the fixed two-row layout (old bug: always 2 rows)");
        Assert(
            320f - UsFilterBarLayout.DropdownLabelWidth >= UsFilterBarLayout.MinDropdownFieldWidth,
            "a stacked dropdown keeps a usable full-width field (label + >=96 field)");

        // Wide body: three-up layout is preserved (one dropdown row).
        Assert(!UsFilterBarLayout.DropdownsStack(800f), "wide body keeps the three-up dropdown row");
        AssertEqual(
            UsFilterBarLayout.RowHeight * 2f,
            UsFilterBarLayout.BodyHeight(800f),
            tolerance,
            "wide body height stays the two base rows");

        // Boundary: three-up is only kept while each dropdown gets label + field.
        Assert(!UsFilterBarLayout.DropdownsStack(536f),
            "536 body still fits label(80) + field(96) per dropdown");
        Assert(UsFilterBarLayout.DropdownsStack(535f),
            "535 body stacks: a dropdown would fall below label(80) + field(96)");
    }

    private static void TestHelpCatalog()
    {
        Assert(UsHelpCatalog.TryGetSection("us/scope-tree", out HelpSection scopeTree),
            "TryGetSection finds us/scope-tree");
        Assert(scopeTree.Title.Length > 0, "scope-tree section has a title");
        Assert(scopeTree.Overview.Length > 0, "scope-tree section has an overview");
        Assert(scopeTree.Items.Count >= 1, "every section has at least one item");

        string[] sectionKeys =
        {
            "us/page-title", "us/mode-row", "us/global-volume", "us/attenuation-editor",
            "us/basic-tuning", "us/timing", "us/camera-indicator", "us/diagnostics",
            "us/scope-tree", "us/preset-list", "us/filter-bar", "us/race-layer", "us/xenotype-layer", "us/voice-pack-checklist"
        };
        foreach (string key in sectionKeys)
        {
            Assert(UsHelpCatalog.TryGetSection(key, out HelpSection section), "section exists: " + key);
            Assert(section.Items.Count >= 1, "section has at least one item: " + key);
        }

        Assert(UsHelpCatalog.TryGetItem("us/scope-tree", "us/scope-tree/action-scope", out HelpItem item),
            "TryGetItem finds an item by section and item key");
        Assert(item.Label.Length > 0 && item.Text.Length > 0, "help item has label and text");

        Assert(!UsHelpCatalog.TryGetSection("missing/section", out _), "missing section returns false");
        Assert(!UsHelpCatalog.TryGetItem("us/scope-tree", "us/scope-tree/not-real", out _),
            "missing item returns false");

        // Global hover resolution requires globally unique item keys; a duplicate would make the
        // panel's meaning depend on dictionary order.
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        int itemCount = 0;
        foreach (string sectionKey in sectionKeys)
        {
            Assert(UsHelpCatalog.TryGetSection(sectionKey, out HelpSection s), "section for uniqueness scan: " + sectionKey);
            foreach (HelpItem helpItem in s.Items)
            {
                itemCount++;
                Assert(seenKeys.Add(helpItem.Key), "item key is globally unique: " + helpItem.Key);
                Assert(helpItem.Key.StartsWith(sectionKey + "/", StringComparison.Ordinal),
                    "item key embeds its section prefix: " + helpItem.Key);
            }
        }

        // 28 → 34 with the global-tuning wiring (timing +2, diagnostics +4), then 40 with the two mood
        // reset controls (2026-09-12 ruling): one entry per action plus one per unavailable reason
        // (no local setting / not from a preset / preset missing / preset has no entry).
        Assert(itemCount == 40, "catalog item count matches the shipped wiring table (34 + 6 mood reset entries: 2 actions + 4 reasons): " + itemCount);

        // The dead-entry guard: every section still owns at least one claimable item, and the
        // removed distance entry must stay removed (its control lives in the Distance workspace now).
        Assert(!UsHelpCatalog.TryGetItem("us/basic-tuning", "us/basic-tuning/distance", out _),
            "basic-tuning has no distance item (control moved to the Distance workspace)");
    }

    private static void TestHelpPanelLogic()
    {
        Assert(UsHelpCatalog.TryGetSection("us/scope-tree", out HelpSection scopeTree),
            "help panel logic test uses an existing section");
        Assert(UsHelpCatalog.TryGetSection("us/global-volume", out HelpSection globalVolume),
            "help panel logic test uses the global-volume section");

        // No hover: the panel explains the active section's overview (the big-level fallback).
        UsHelpPanelLogic.HelpPanelDisplay overview = UsHelpPanelLogic.Resolve(scopeTree, "");
        Assert(overview.Title == scopeTree.Title, "overview display uses section title");
        Assert(overview.Label == UsHelpPanelLogic.OverviewLabel, "overview display uses the overview label");
        Assert(overview.Text == scopeTree.Overview, "overview display uses section overview");

        // The zero-Verse seam is identity, so display strings are the catalog's Keyed entry names;
        // the action-scope label reuses the row's own caption key (US.Tuning.ActionScope).
        UsHelpPanelLogic.HelpPanelDisplay hover = UsHelpPanelLogic.Resolve(scopeTree, "us/scope-tree/action-scope");
        Assert(hover.Label == "US.Tuning.ActionScope", "hover display uses the hovered item's label key");
        Assert(hover.Text == "US.Help.ScopeTree.ActionScope.Text", "hover display uses the hovered item's body key");
        Assert(hover.Title == scopeTree.Title, "in-section hover keeps the active section header");

        // Global hover: the always-visible surfaces claim entries of other sections (nav claims
        // page-title/nav while any section is active); the header follows the claimed entry's section.
        Assert(UsHelpCatalog.TryFindItem("us/global-volume/slider", out HelpItem sliderItem, out _),
            "global-volume slider exists in the catalog");
        UsHelpPanelLogic.HelpPanelDisplay foreignHover = UsHelpPanelLogic.Resolve(scopeTree, "us/global-volume/slider");
        Assert(foreignHover.Title == globalVolume.Title, "foreign hover header names the claimed entry's section");
        Assert(foreignHover.Label == sliderItem.Label && foreignHover.Text == sliderItem.Text,
            "foreign hover resolves across the whole catalog, not just the current section");

        // An unknown key is ignored, not fatal: the panel falls back to the section overview.
        UsHelpPanelLogic.HelpPanelDisplay unknown = UsHelpPanelLogic.Resolve(scopeTree, "us/nope/not-real");
        Assert(unknown.Label == UsHelpPanelLogic.OverviewLabel && unknown.Text == scopeTree.Overview,
            "unknown hover key falls back to the section overview");

        UsHelpPanelLogic.HelpPanelDisplay noSection = UsHelpPanelLogic.Resolve(null, "");
        Assert(noSection.Title == UsHelpPanelLogic.EmptyTitle, "missing section uses Help title");
        Assert(noSection.Text == UsHelpPanelLogic.EmptyText, "missing section uses empty text");
        UsHelpPanelLogic.HelpPanelDisplay noSectionHover = UsHelpPanelLogic.Resolve(null, "us/scope-tree/layer");
        Assert(noSectionHover.Title == scopeTree.Title, "hover works even with no active section");

        // Every returned string - the panel's own three AND the catalog's - resolves through the
        // one seam handed to Resolve, so measured and drawn text can never come from different passes.
        UsHelpPanelLogic.HelpPanelDisplay translated = UsHelpPanelLogic.Resolve(
            scopeTree, "us/scope-tree/action-scope", key => "[" + key + "]");
        Assert(translated.Title == "[" + scopeTree.Title + "]"
                && translated.Label == "[US.Tuning.ActionScope]"
                && translated.Text == "[US.Help.ScopeTree.ActionScope.Text]",
            "the translation seam wraps title, label and body in one pass");
    }



    private static void TestVoicePacksFilters()
    {
        UiDomainFilter emptyDomain = new UiDomainFilter();
        Assert(VoicePacksFilters.DomainMatches(false, false, false, false, false, in emptyDomain),
            "empty domain filter should match an all-false row");
        Assert(VoicePacksFilters.DomainMatches(true, true, true, true, true, in emptyDomain),
            "empty domain filter should match a mixed row");

        UiPackFilter emptyPack = new UiPackFilter();
        Assert(VoicePacksFilters.PackMatches(null, null, in emptyPack),
            "empty pack filter should match any row");

        UiDomainFilter enabledOnlyDomain = new UiDomainFilter(enabledOnly: true);
        Assert(VoicePacksFilters.DomainMatches(false, false, false, false, true, in enabledOnlyDomain),
            "EnabledOnly domain filter should keep enabled rows");
        Assert(!VoicePacksFilters.DomainMatches(false, false, false, false, false, in enabledOnlyDomain),
            "EnabledOnly domain filter should drop disabled rows");

        UiDomainFilter conflictOnly = new UiDomainFilter(conflictOnly: true);
        Assert(VoicePacksFilters.DomainMatches(true, false, false, false, false, in conflictOnly),
            "ConflictOnly should match hasConflict");
        Assert(VoicePacksFilters.DomainMatches(false, true, false, false, false, in conflictOnly),
            "ConflictOnly should match isDormant");
        Assert(VoicePacksFilters.DomainMatches(false, false, true, false, false, in conflictOnly),
            "ConflictOnly should match isTargetUnavailable");
        Assert(VoicePacksFilters.DomainMatches(false, false, false, true, false, in conflictOnly),
            "ConflictOnly should match isOrphan");
        Assert(!VoicePacksFilters.DomainMatches(false, false, false, false, false, in conflictOnly),
            "ConflictOnly should drop rows with no conflict-like flag");

        UiDomainFilter orphanOnly = new UiDomainFilter(orphanOnly: true);
        Assert(VoicePacksFilters.DomainMatches(false, false, false, true, false, in orphanOnly),
            "OrphanOnly should match isOrphan");
        Assert(!VoicePacksFilters.DomainMatches(true, true, true, false, true, in orphanOnly),
            "OrphanOnly should ignore other flags when isOrphan is false");

        UiPackFilter authorPack = new UiPackFilter(author: "AUTHOR");
        Assert(VoicePacksFilters.PackMatches("My Author", null, in authorPack),
            "Pack author filter should match author");
        Assert(VoicePacksFilters.PackMatches(null, "AUTHOR MOD", in authorPack),
            "Pack author filter should match modName");
        Assert(!VoicePacksFilters.PackMatches("Other", "Other Mod", in authorPack),
            "Pack author filter should drop rows where neither author nor modName matches");

        UiDomainFilter combinedDomain = new UiDomainFilter(enabledOnly: true, conflictOnly: true);
        Assert(VoicePacksFilters.DomainMatches(true, false, false, false, true, in combinedDomain),
            "Combined domain filter should keep rows satisfying both conditions");
        Assert(!VoicePacksFilters.DomainMatches(false, false, false, false, true, in combinedDomain),
            "Combined domain filter should drop rows failing ConflictOnly");
        Assert(!VoicePacksFilters.DomainMatches(true, false, false, false, false, in combinedDomain),
            "Combined domain filter should drop rows failing EnabledOnly");
    }

    private static void TestActionScopeRules()
    {
        var draft = new SqueakActionDefinition(
            SqueakAction.Draft,
            "Draft",
            "US_Draft",
            SqueakVocalGatePolicy.ApplyTalkingGate,
            SqueakActionScopeSupport.ActiveCommand,
            SqueakActionScope.ActiveCommand);
        var attack = new SqueakActionDefinition(
            SqueakAction.Attack,
            "Attack",
            "US_Attack",
            SqueakVocalGatePolicy.ApplyTalkingGate,
            SqueakActionScopeSupport.AnyOccurrence | SqueakActionScopeSupport.ActiveCommand,
            SqueakActionScope.AnyOccurrence);
        var call = new SqueakActionDefinition(
            SqueakAction.Call,
            "Call",
            "US_Call",
            SqueakVocalGatePolicy.ApplyTalkingGate,
            SqueakActionScopeSupport.AnyOccurrence,
            SqueakActionScope.AnyOccurrence);

        Assert(ActionScopeRules.GroupFor(draft) == ActionScopeGroup.Operable,
            "ActiveCommand-only actions group as Operable");
        Assert(ActionScopeRules.GroupFor(attack) == ActionScopeGroup.Operable,
            "actions supporting ActiveCommand group as Operable even when default is AnyOccurrence");
        Assert(ActionScopeRules.GroupFor(call) == ActionScopeGroup.Autonomous,
            "AnyOccurrence-only actions group as Autonomous");

        Assert(ActionScopeRules.IsHiddenByDefault(SqueakAction.Crying),
            "Crying is hidden from the Action Scope editor by default");
        Assert(ActionScopeRules.IsHiddenByDefault(SqueakAction.Giggling),
            "Giggling is hidden from the Action Scope editor by default");
        Assert(!ActionScopeRules.IsHiddenByDefault(SqueakAction.Call),
            "regular actions remain visible in the Action Scope editor");
        Assert(!ActionScopeRules.ShowBiotechDefensiveActions,
            "the Biotech defensive action UI switch defaults to hidden");
    }

    private static void TestRaceXenotypeFiltering()
    {
        Assert(VoicePacksFilters.RaceFilterMatches("", "Human"),
            "empty race filter matches every race");
        Assert(VoicePacksFilters.RaceFilterMatches("Human", "Human"),
            "race filter matches the selected race");
        Assert(!VoicePacksFilters.RaceFilterMatches("Human", "Ratkin"),
            "race filter drops other races");

        Assert(VoicePacksFilters.XenotypeFilterMatches("", "", "Human", "Sanguophage"),
            "empty race+xeno filters match every xenotype domain");
        Assert(VoicePacksFilters.XenotypeFilterMatches("Human", "", "Human", "Sanguophage"),
            "race filter narrows xenotype domains to that race");
        Assert(VoicePacksFilters.XenotypeFilterMatches("Human", "Sanguophage", "Human", "Sanguophage"),
            "race+xeno filters keep the matching domain");
        Assert(!VoicePacksFilters.XenotypeFilterMatches("Human", "Sanguophage", "Human", "Genie"),
            "xenotype filter drops other xenotypes on the same race");
        Assert(!VoicePacksFilters.XenotypeFilterMatches("Human", "Sanguophage", "Ratkin", "Sanguophage"),
            "race filter drops the same xenotype on another race");
    }


    private static void TestAttenuationMath()
    {
        const float tolerance = 0.0001f;

        AssertEqual(0f, AttenuationMath.ChartX(0f, 100f, AttenuationMath.MinDistance), tolerance, "ChartX min maps to chart left");
        AssertEqual(100f, AttenuationMath.ChartX(0f, 100f, AttenuationMath.MaxDistance), tolerance, "ChartX max maps to chart right");
        AssertEqual(50f, AttenuationMath.ChartX(0f, 100f, 40f), tolerance, "ChartX 40/65 maps to middle");
        AssertEqual(AttenuationMath.MinDistance, AttenuationMath.DistanceFromX(0f, 100f, 0f), tolerance, "DistanceFromX left maps to MinDistance");
        AssertEqual(AttenuationMath.MaxDistance, AttenuationMath.DistanceFromX(0f, 100f, 100f), tolerance, "DistanceFromX right maps to MaxDistance");
        AssertEqual(40f, AttenuationMath.DistanceFromX(0f, 100f, 50f), tolerance, "DistanceFromX middle maps to 40");

        float min = float.NaN;
        float max = float.PositiveInfinity;
        AttenuationMath.SanitizeRange(ref min, ref max);
        AssertEqual(AttenuationMath.MinDistance, min, tolerance, "SanitizeRange NaN min becomes MinDistance");
        AssertEqual(50f, max, tolerance, "SanitizeRange Infinity max becomes 50");

        min = 10f;
        max = 70f;
        AttenuationMath.SanitizeRange(ref min, ref max);
        AssertEqual(AttenuationMath.MinDistance, min, tolerance, "SanitizeRange clamps min to 15");
        AssertEqual(AttenuationMath.MaxDistance, max, tolerance, "SanitizeRange clamps max to 65");

        min = 60f;
        max = 61f;
        AttenuationMath.SanitizeRange(ref min, ref max);
        Assert(max >= min + AttenuationMath.MinRange, "SanitizeRange enforces MinRange");

        Assert(AttenuationMath.FormatRange(15f, 50f) == "15|50", "FormatRange uses | separator");
        Assert(AttenuationMath.FormatRangeDisplay(15f, 50f).Contains("–"), "FormatRangeDisplay uses en dash");
    }


    private static void AssertEqual(float expected, float actual, float tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(message + " (expected " + expected + ", got " + actual + ")");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

}
