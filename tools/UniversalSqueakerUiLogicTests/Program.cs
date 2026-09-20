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
        TestSettingsWindowSizePolicy();
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

    // TestWindowChromeLayout is DELETED with the rule it pinned (TODO:15, closed 2026-09-20). It asserted
    // US's own close-affordance formula - a 16px padding over a hardcoded VerseFerriteTextMetrics.Instance -
    // which was the second definition of a rule the shell already derives as
    // max(110, MeasureWidth(CloseText, CloseFont) + 2 x CloseButtonPadding) through its own Metrics seam.
    // The consumer override is gone, so a US lane for it would now assert FL's arithmetic against a copy of
    // itself; the shell's own harness owns that rule, and this repository asserting a second copy of it is
    // exactly the seam defect the deletion closes.

    /// <summary>
    /// Pure size policy of the settings window (task-10): it opens NARROW - 24% of the screen width
    /// clamped into [600, 860] - and the retractable help drawer adds exactly the column it occupies
    /// plus the body-row gap it introduces (176 + 12 = 188). Height keeps the shipped 0.66-of-screen
    /// shape with a 600 floor. This file is compiled by the zero-Verse gate, so these are float
    /// helpers; the window composes them into its UnityEngine.Vector2 at the Verse boundary.
    /// The referenced declarations live in Layout.Schema2.xml (help-scroll Width 176, body-row Gap 12).
    /// </summary>
    private static void TestSettingsWindowSizePolicy()
    {
        const float tolerance = 0.001f;

        Assert(Math.Abs(WindowChromeLayout.HelpDrawerWidth - 176f) < tolerance,
            "the help column declaration the policy mirrors is 176");
        Assert(Math.Abs(WindowChromeLayout.BodyRowGap - 12f) < tolerance,
            "the body-row gap declaration the policy mirrors is 12");
        Assert(Math.Abs(WindowChromeLayout.DrawerWidthDelta
                - (WindowChromeLayout.HelpDrawerWidth + WindowChromeLayout.BodyRowGap)) < tolerance,
            "the drawer delta is derived from the two manifest declarations, not repeated");
        Assert(Math.Abs(WindowChromeLayout.DrawerWidthDelta - 188f) < tolerance,
            "expanding the drawer costs the window exactly 176 + 12 = 188px");

        foreach ((float screen, float screenHeight) in new[]
            { (1024f, 768f), (1280f, 800f), (1920f, 1080f), (2560f, 1440f), (3840f, 2160f) })
        {
            float closed = WindowChromeLayout.SettingsClosedWidth(screen, screenHeight);
            float open = WindowChromeLayout.SettingsOpenWidth(screen, screenHeight);
            float height = WindowChromeLayout.SettingsWindowHeight(screen, screenHeight);
            Assert(closed >= WindowChromeLayout.SettingsWidthFloor - tolerance,
                "the closed window is never below the " + WindowChromeLayout.SettingsWidthFloor + " floor at screen "
                + screen + ": " + closed);
            Assert(closed <= WindowChromeLayout.SettingsWidthCeiling + tolerance,
                "the closed window is never above the " + WindowChromeLayout.SettingsWidthCeiling + " ceiling at screen "
                + screen + ": " + closed);
            Assert(open >= closed,
                "the open window is never narrower than the closed one at screen " + screen);
            Assert(Math.Abs(closed - Math.Round(closed)) < tolerance,
                "the closed width is a whole pixel count at screen " + screen + ": " + closed);
            Assert(Math.Abs(height - Math.Round(height)) < tolerance,
                "the height is a whole pixel count at " + screen + "x" + screenHeight + ": " + height);
            Assert(Math.Abs(height * 4f - closed * 3f) < tolerance,
                "the window keeps 4:3 exactly at " + screen + "x" + screenHeight + ": " + closed + "x" + height);
            // The drawer delta is what the window gains where the screen can hold it. Below that the open
            // width is capped at the screen, so the gain is the screen's remaining room instead - the whole
            // reason SettingsOpenWidth clamps rather than adding blindly.
            float expectedDelta = Math.Min(WindowChromeLayout.DrawerWidthDelta, Math.Max(0f, screen - closed));
            Assert(Math.Abs((open - closed) - expectedDelta) < tolerance,
                "open - closed is the drawer delta where the screen holds it, the remaining room otherwise, at screen "
                + screen + ": " + (open - closed) + " vs " + expectedDelta);
            Assert(Math.Abs(WindowChromeLayout.SettingsWindowWidth(screen, screenHeight, drawerExpanded: false) - closed) < tolerance
                && Math.Abs(WindowChromeLayout.SettingsWindowWidth(screen, screenHeight, drawerExpanded: true) - open) < tolerance,
                "SettingsWindowWidth selects the closed/open branch at screen " + screen);
        }

        // The policy's floor is vanilla's OWN options window (Dialog_Options.InitialSize = 650x600, a
        // fixed size), so the minimum canvas must land exactly on that footprint.
        // The floor is the smallest whole-pixel 4:3 box that covers vanilla's own 650x600, and the minimum
        // canvas opens exactly there; every larger screen is the same 4:3 box scaled.
        Assert(Math.Abs(WindowChromeLayout.SettingsClosedWidth(1024f, 768f) - 800f) < tolerance,
            "1024x768 opens at the 800 floor (vanilla's 650x600 rounded up to whole-pixel 4:3)");
        Assert(Math.Abs(WindowChromeLayout.SettingsWindowHeight(1024f, 768f) - 600f) < tolerance,
            "1024x768 is 800x600 - 4:3 at vanilla's own height, both whole numbers");
        Assert(Math.Abs(WindowChromeLayout.SettingsClosedWidth(1920f, 1080f) - 960f) < tolerance,
            "1920x1080 opens at 960 = half the screen width");
        Assert(Math.Abs(WindowChromeLayout.SettingsWindowHeight(1920f, 1080f) - 720f) < tolerance,
            "1920x1080 keeps 4:3: 960x720");
        Assert(Math.Abs(WindowChromeLayout.SettingsClosedWidth(2560f, 1440f) - 1280f) < tolerance,
            "2560x1440 opens at 1280 = half the screen width");
        Assert(Math.Abs(WindowChromeLayout.SettingsWindowHeight(2560f, 1440f) - 960f) < tolerance,
            "2560x1440 keeps 4:3: 1280x960 (the old portrait 614x950 is what wrapped every long label)");
        Assert(Math.Abs(WindowChromeLayout.SettingsClosedWidth(3840f, 2160f) - 1600f) < tolerance,
            "3840x2160 clamps to the 1600 ceiling (half = 1920)");
        Assert(Math.Abs(WindowChromeLayout.SettingsWindowHeight(3840f, 2160f) - 1200f) < tolerance,
            "3840x2160 keeps 4:3: 1600x1200");

        // The expanded window may never be wider than the screen it lives in; below the drawer delta it is
        // capped at the screen, and under the width floor at the closed width itself.
        Assert(Math.Abs(WindowChromeLayout.SettingsOpenWidth(1920f, 1080f) - 1148f) < tolerance,
            "1920 open = 960 + 188");
        Assert(Math.Abs(WindowChromeLayout.SettingsOpenWidth(2560f, 1440f) - 1468f) < tolerance,
            "2560 open = 1280 + 188");
        Assert(Math.Abs(WindowChromeLayout.SettingsOpenWidth(800f, 600f) - 800f) < tolerance,
            "an 800-wide screen cannot be widened past the floor, so open == closed");
        Assert(Math.Abs(WindowChromeLayout.SettingsOpenWidth(900f, 700f) - 900f) < tolerance,
            "900 screen caps the expanded window at the screen (it would be 988)");
        Assert(Math.Abs(WindowChromeLayout.SettingsOpenWidth(988f, 800f) - 988f) < tolerance,
            "988 screen still fits the full drawer delta on top of the floor");
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
        // Count pin, updated with the help-coverage pass: the catalog gained one entry per Playback
        // behaviour row (us/basic-tuning/scale-cooldown|scale-talking|scale-population), the attenuation
        // status/range read-out (us/attenuation-editor/status) and the Help drawer toggle
        // (us/page-title/help-drawer), and lost the single shared us/basic-tuning/scaling item. The
        // eat-occurrence pair adds one entry per control: us/basic-tuning/eat-precision (parent) and
        // us/basic-tuning/eat-precision-include-drugs (the child row), 44 -> 46.
        Assert(itemCount == 46, "catalog item count matches the shipped wiring table (46 items incl. the three per-row basic-tuning entries, the two eat-precision entries, us/attenuation-editor/status and us/page-title/help-drawer): " + itemCount);

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
