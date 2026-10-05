using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// V2 Packs lane (contract V2.1-V2.5): the Packs workspace reads filter → race/xenotype browse → current
/// domain enable with ONE card rhythm; the enable band names the domain its list belongs to; browse selection
/// and pack enable stay different channels; the one real page box stays reachable in both help states and
/// both languages.
///
/// This lane owns ONLY the V2 properties no other lane pins (order + rhythm, the scope line, the
/// browse-vs-enable difference, the title/secondary ink roles as a GUARD, real-box accessibility - both the
/// state's own two-screen number (recorded honestly, PM r2) and the P4b scroll+click+typed-write GUARD - the
/// three empty states, and the Packs completeness + increment-based separation). The per-element attribute
/// contracts belong to: DeclarativePacksLaneTests (row sets, templates, G2/G3), UsSquareToggleLaneTests (the
/// enable control's kind/appearance/band), SettingsGeometryLaneTests (the checklist card's children/gaps) and
/// FrameGeometryLaneTests (container Padding/Gap). NOTE what UiSourceInvariantTests does NOT cover: it proves
/// every manifest key VALUE resolves in both language tables, not that an element still carries the SAME
/// value, and Bind/Items are not in its collected attribute list - so the by-value pins for the Packs headers,
/// the empty-state TextKeys, the search Bind and the checklist Kind live HERE (see PacksByValuePins).
///
/// MUTATION LEDGER - the reverts RUN for this lane, named at their clauses (clauses that hold under both the
/// pre-V2 and V2 shapes are labelled GUARD and are not evidence). The filter band's three declaration clauses
/// were each attributed by its OWN revert, measured:
///  - band back to the pre-V2 bare widget (id filter-layer -> filter-bar) → the ORDER clause reddens, because
///    the declared id list stops matching [filter-layer, race-layer, xenotype-layer, checklist-card];
///  - the id kept but the band declared as a Widget (not a Section) → the "must be an engine Section" clause;
///  - the Section kept but without the header first child → the "must be title-first with the 26px
///    us/section-header" clause. The measured header-at-Padding+12 clause is the layout half behind those
///    declarations (it fires when a Section arranges its header anywhere but one Padding below the band).
///  - the checklist-scope element removed → "the enable band must declare its scope line" (P2);
///  - VisibleKey="checklist-has-domain" removed from the band composite → the no-domain empty-state clauses
///    (the composite and the search field would arrange) (P4);
///  - the two empty-state VisibleKeys swapped → the "domain with no packs" clause (P4).
/// Those V2 proofs are historical; BH1 changed their page-box inputs and re-ran the guards, without
/// repeating each undo. New P4b is a current-input GUARD with no fresh faithful-revert proof: row 29 of
/// 30 is scrolled through the session seam, then clicked through Host pointer events. It covers that
/// near-tail target's typed write, not the last row, native mouse-wheel routing or real-font comfort.
/// </summary>
internal static class PacksHierarchyLaneTests
{
    // The Packs page's REAL layout box. The shell hands the page window-minus-chrome, and 1024x768 is the
    // game's MINIMUM SUPPORTED logical screen (not the only reachable one - every larger supported resolution
    // is reachable too, and this is the tight end of the product's own window policy): the policy opens the
    // window at 800x600 there, so the page box is 760x524. BH1 (2026-10-05) retired the SECOND width: the
    // help panel reserves height above the footer instead of taking a column beside the settings, so help
    // open and help closed are arranged in the SAME box - the box is therefore read out of the policy
    // function instead of being spelled. Window-policy output, NOT a synthetic stress input.
    private const float MinimumScreenWidth = 1024f;
    private const float MinimumScreenHeight = 768f;

    /// <summary>The page box the shell hands the page on this screen: the policy's window minus the side
    /// chrome, minus the title bar and the shell's own bottom inset. Since BH1 this answers BOTH help
    /// states, because the policy has no help argument any more.</summary>
    private static Vector2 PageBoxAt(float screenWidth, float screenHeight) => new Vector2(
        WindowChromeLayout.SettingsWindowWidth(screenWidth, screenHeight) - WindowChromeLayout.WindowChromeInset,
        WindowChromeLayout.SettingsWindowHeight(screenWidth, screenHeight)
        - WindowChromeLayout.TitleBarHeight - WindowChromeLayout.WindowChromeInset * 0.5f);

    private static readonly Vector2 PageBox = PageBoxAt(MinimumScreenWidth, MinimumScreenHeight);

    /// <summary>The Packs bands in declared reading order (P1).</summary>
    private static readonly string[] PacksBandOrder = { "filter-layer", "race-layer", "xenotype-layer", "checklist-card" };

    /// <summary>
    /// P5 completeness: the Packs elements the slice must not lose. Ids only, because the per-element
    /// ATTRIBUTE contracts are the other lanes' (class doc) - this is the one place "no element/help/
    /// translation key lost" (V2.5) is pinned for the Packs tab.
    /// </summary>
    private static readonly string[] PacksElementIds =
    {
        "filter-layer", "filter-layer-header", "filter-bar",
        "race-layer", "race-layer-header", "race-layer-rows",
        "xenotype-layer", "xenotype-layer-header", "xenotype-layer-rows",
        "checklist-card", "checklist-header", "checklist-scope", "checklist", "checklist-list",
        "checklist-search", "checklist-rows", "checklist-empty-domain", "checklist-empty-search",
        "checklist-empty-nodomain",
        "race-layer-row", "xenotype-layer-row", "checklist-row", "race-layer-row-surface", "race-layer-row-hit",
        "xenotype-layer-row-surface", "xenotype-layer-row-hit", "checklist-row-check",
        "checklist-row-surface", "checklist-row-content", "checklist-row-text",
        "race-layer-row-content", "race-layer-row-gutter", "race-layer-row-text",
        "xenotype-layer-row-content", "xenotype-layer-row-gutter", "xenotype-layer-row-text",
        "checklist-row-gutter",
    };

    /// <summary>
    /// The by-value attribute pins for the Packs tab, asserted on the live manifest.
    /// <list type="bullet">
    /// <item>V2-owned (introduced or MOVED by this change, pinned nowhere else): the filter band's
    /// HelpKey/TitleKey on its new header and the widget's TitleHidden (P1); the scope line's kind, binding,
    /// visibility and ink role (P2); the band composite's declaration-level visibility (P4 gap fix); the
    /// enable control's two binding ROLES (P2 - its kind/appearance/band are the R2 lane's).</item>
    /// <item>RESTORED pre-existing values (task-12 B): the race/xenotype/checklist header TitleKeys, the
    /// three empty-state TextKeys, the search field's Bind, the checklist composite's Kind. The zero-Verse
    /// gate proves these key VALUES resolve in both language tables, NOT that the element still carries the
    /// same value, and Bind is outside its collected attribute list - so the same-value pin belongs here.</item>
    /// </list>
    /// </summary>
    private static readonly (string Id, string Attribute, string Value)[] PacksByValuePins =
    {
        // V2-owned.
        ("filter-layer-header", "Kind", "us/section-header"),
        ("filter-layer-header", "TitleKey", "US.Section.FilterDomains"),
        ("filter-layer-header", "HelpKey", "us/filter-bar"),
        ("filter-bar", "TitleHidden", "true"),
        ("checklist-scope", "Kind", "text/wrapped"),
        ("checklist-scope", "Bind", "checklist-scope"),
        ("checklist-scope", "VisibleKey", "checklist-has-domain"),
        ("checklist-scope", "Emphasis", "Muted"),
        ("checklist", "VisibleKey", "checklist-has-domain"),
        ("checklist-row-check", "Bind", "enabled"),
        ("checklist-row-check", "SelectedKey", "enabled"),
        // Restored pre-existing values (same-value pins the trim had dropped).
        ("race-layer-header", "TitleKey", "US.Section.RaceDomain"),
        ("xenotype-layer-header", "TitleKey", "US.Section.XenotypeDomain"),
        ("checklist-header", "TitleKey", "US.Section.ChooseVoicePacks"),
        ("checklist-empty-domain", "TextKey", "US.Packs.Checklist.EmptyDomain"),
        ("checklist-empty-search", "TextKey", "US.Packs.Checklist.EmptySearch"),
        ("checklist-empty-nodomain", "TextKey", "US.Packs.Checklist.NoDomains"),
        ("checklist-search", "Bind", "search-text"),
        ("checklist", "Kind", "us/voice-pack-checklist"),
    };

    public static int RunAll()
    {
        Step("the Packs bands read filter -> browse -> enable with one card rhythm (P1)", ReadingOrderAndRhythm);
        Step("browse selection and pack enable are expressed differently (P2)", BrowseAndEnableAreExpressedDifferently);
        Step("titles carry the primary role and metadata stays muted (P3, GUARD)", SecondaryLinesStayMuted);
        Step("the Packs tab is reachable at the real page box in both help states and languages (P4)",
            RealPageBoxAccessibility);
        Step("the Packs surface is preserved and the two channels stay separate (P5)", PreservationAndSeparation);
        Console.WriteLine("PacksHierarchyLaneTests ALL PASS");
        return 0;
    }

    // P1 - reading order, title-first, one card rhythm.

    private static void ReadingOrderAndRhythm()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        UiElementSpec contentScroll = Find(host.Manifest.Roots[0], "content-scroll")
            ?? throw new InvalidOperationException("the manifest must declare content-scroll");
        List<UiElementSpec> packs = contentScroll.Children
            .Where(c => c.TryGetAttribute("Tab", out string tab) && tab.Trim() == "Packs")
            .ToList();

        Assert(packs.Select(p => p.Id).SequenceEqual(PacksBandOrder),
            "the Packs tab must declare exactly filter -> race browse -> xenotype browse -> enable, in that"
            + " order; got [" + string.Join(", ", packs.Select(p => p.Id)) + "]");

        foreach (UiElementSpec band in packs)
        {
            // The filter band became a Section in V2; before it, this was false for filter-bar (a bare widget
            // drawing its own plain title), which is the revert this proof names.
            Assert(band.Kind == "Section",
                "'" + band.Id + "' must be an engine Section so every Packs band gets the same card chrome, got "
                + band.Kind);
            Assert(band.TryGetAttribute("Padding", out string padding) && padding == "12",
                "'" + band.Id + "' must declare the card Padding 12, got '" + padding + "'");
            Assert(band.TryGetAttribute("Gap", out string gap) && gap == "6",
                "'" + band.Id + "' must declare the card Gap 6, got '" + gap + "'");
            Assert(band.Children.Count > 0 && band.Children[0].Kind == "us/section-header"
                && band.Children[0].TryGetAttribute("Height", out string headerHeight) && headerHeight == "26",
                "'" + band.Id + "' must be title-first with the 26px us/section-header");
            Assert(!HasDescendantKind(band, "Section"),
                "'" + band.Id + "' must not nest another card Section (V2 keeps the frame count down)");
        }

        // Measured: the declared order IS the drawn order at the acceptance box, one card Padding between a
        // band's top and its header.
        var metrics = new Program.StubMetrics();
        using UiHost measured = UsKernelSettingsHost.Create(
            new RecordingSettingsSource { RichData = true, WrappingDomainText = true }, metrics);
        measured.Bindings.Invoke("set-tab", "Packs");
        UiLayoutSnapshot snapshot = measured.MeasureAndArrange(PageBox);

        float previousY = float.MinValue;
        foreach (UiElementSpec band in packs)
        {
            Assert(snapshot.RectById.TryGetValue(band.Id, out Rect bandRect),
                "'" + band.Id + "' must be arranged on the Packs tab at the acceptance box");
            Assert(bandRect.y > previousY,
                "'" + band.Id + "' must be drawn below the band before it (declared order is the drawn order)");
            previousY = bandRect.y;

            string headerId = band.Children[0].Id;
            Assert(snapshot.RectById.TryGetValue(headerId, out Rect header),
                "the band header '" + headerId + "' must be arranged");
            Assert(Math.Abs(header.y - (bandRect.y + 12f)) <= 0.5f,
                "'" + headerId + "' must sit one card Padding (12) below its band: header y=" + header.y
                + " band y=" + bandRect.y);
        }

        Console.WriteLine("[v2-order] " + string.Join(" ", packs.Select(p => p.Id + "@"
            + Num(snapshot.RectById[p.Id].y))) + " headerOffset=12");
    }

    // P2 - browse vs enable expression.

    private static void BrowseAndEnableAreExpressedDifferently()
    {
        var metrics = new Program.StubMetrics();
        using UiHost host = UsKernelSettingsHost.Create(
            new RecordingSettingsSource { RichData = true }, metrics, out UsWriteBindings writes);
        host.Bindings.Invoke("set-tab", "Packs");

        // The two channels must not CROSS. The template shapes themselves are DeclarativePacksLaneTests'
        // contract, so this lane pins only the difference.
        foreach (string template in new[] { "race-layer-row", "xenotype-layer-row" })
        {
            Assert(!Descendants(host.Manifest.Templates[template]).Any(e => e.Kind == "input/checkbox"),
                "'" + template + "' must not carry an enable control: browse and enable are different channels");
        }

        // SA1.7 re-cut of the kind-ban: the enable row now DOES carry a us/selection-surface sibling -
        // the hover plate the user asked for. The plate may only READ the pointer: it must bind no
        // selection bool, and nothing in the row may bind or invoke the browse channel. Asserting the
        // SEMANTICS is stronger than banning the kind name ever was.
        UiElementSpec? enablePlate = Find(host.Manifest.Templates["checklist-row"], "checklist-row-surface");
        Assert(enablePlate != null
                && enablePlate!.Kind == "us/selection-surface"
                && enablePlate.TryGetAttribute("Hover", out string plate) && plate == "true"
                && !enablePlate.TryGetAttribute("Bind", out _),
            "the enable row's surface is the SA1.7 hover plate and nothing else: Hover=\"true\", no Bind");
        Assert(!Descendants(host.Manifest.Templates["checklist-row"]).Any(e =>
                (e.TryGetAttribute("ActionBind", out string action) && action == "select-domain")
                || (e.TryGetAttribute("Bind", out string bind) && bind == "selected")),
            "no element of the enable row may bind or invoke the browse selection: the plate is paint,"
            + " the switch stays the only writer");
        UiElementSpec check = Find(host.Manifest.Templates["checklist-row"], "checklist-row-check")
            ?? throw new InvalidOperationException("the manifest must declare the checklist row control");
        Assert(check.Kind == "input/checkbox"
            && check.TryGetAttribute("Bind", out string checkBind) && checkBind == "enabled"
            && check.TryGetAttribute("SelectedKey", out string selectedKey) && selectedKey == "enabled",
            "the enable row must be one input/checkbox over the item-local 'enabled' bool, Bind and SelectedKey"
            + " naming the same key (its kind/appearance/band are UsSquareToggleLaneTests')");

        UiElementSpec scope = Find(host.Manifest.Roots[0], "checklist-scope")
            ?? throw new InvalidOperationException("the enable band must declare its scope line 'checklist-scope'");
        Assert(scope.TryGetAttribute("Bind", out string scopeBind) && scopeBind == "checklist-scope",
            "the scope line must read the read-only 'checklist-scope' binding");
        Assert(scope.TryGetAttribute("VisibleKey", out string scopeVisible) && scopeVisible == "checklist-has-domain",
            "the scope line must hide with the list when no domain is selected");
        // The bidirectional write-registry gate is DisplayWriteAdvancesSharedRevision; this states the V2 fact
        // locally so a scope line that ever became writable reddens in the round that changed it.
        Assert(!writes.Bound.Any(b => b.Key.IndexOf("checklist-scope", StringComparison.Ordinal) >= 0),
            "the scope line must be READ-ONLY: no write key may be registered for it");

        // The bound text must BE the production projection of the selected domain - composed from the
        // production language keys over the production view fields (DisplayName / RaceDisplay / Scope), not a
        // fixture literal: this fixture's built-in xenotype display would otherwise print the doubled
        // "Sanguophage (Human) (human)". UsPacksText is internal with no InternalsVisibleTo, so the lane
        // restates the composition against the same keys.
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            try
            {
                var scoped = new RecordingSettingsSource
                {
                    RichData = true,
                    ReflectSelectionInView = true,
                    XenotypeDisplayName = "Sanguophage",
                    XenotypeRaceDisplay = "Human",
                };
                using UiHost localized = UsKernelSettingsHost.Create(scoped, metrics);
                localized.Bindings.Invoke("set-tab", "Packs");
                localized.MeasureAndArrange(PageBox);

                VoicePackDomainView xenotype = scoped.BuildView().SelectedDomain!.Value;
                string xenotypeExpected = ProductionScopeText(table, xenotype);
                string xenotypeActual = localized.Bindings.Get<string>("checklist-scope");
                Assert(string.Equals(xenotypeActual, xenotypeExpected, StringComparison.Ordinal),
                    "the enable scope line must be the production projection of the selected xenotype domain in "
                    + language + ": expected '" + xenotypeExpected + "', got '" + xenotypeActual + "'");

                // The selection is the only input: selecting the RACE domain re-derives the line.
                localized.Bindings.Invoke("select-domain", "human");
                VoicePackDomainView race = scoped.BuildView().SelectedDomain!.Value;
                string raceExpected = ProductionScopeText(table, race);
                string raceActual = localized.Bindings.Get<string>("checklist-scope");
                Assert(string.Equals(raceActual, raceExpected, StringComparison.Ordinal)
                    && !string.Equals(raceActual, xenotypeActual, StringComparison.Ordinal),
                    "selecting a race domain must re-derive the enable scope line in " + language
                    + ": expected '" + raceExpected + "', got '" + raceActual + "'");

                Console.WriteLine("[v2-scope] " + language + " xenotype='" + xenotypeActual
                    + "' race='" + raceActual + "'");
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(PageBox);
        Assert(snapshot.RectById.TryGetValue("checklist-scope", out Rect scopeRect)
            && snapshot.RectById.TryGetValue("checklist-card", out Rect card)
            && scopeRect.y >= card.y && scopeRect.yMax <= card.yMax,
            "the scope line must be arranged inside the enable card");
    }

    /// <summary>The production scope composition over Scope / DisplayName / RaceDisplay and the existing keys:
    /// axis + domain name, a xenotype keeping its race context.</summary>
    private static string ProductionScopeText(Dictionary<string, string> table, VoicePackDomainView domain)
    {
        bool xenotype = domain.Scope == SqueakVoicePackScope.Xenotype;
        string axis = table[xenotype ? "US.Packs.Filter.Xenotype" : "US.Packs.Filter.Race"];
        string name = xenotype
            ? string.Format(CultureInfo.InvariantCulture, table["US.Packs.Domain.XenotypeRaceContext"],
                domain.DisplayName, domain.RaceDisplay)
            : domain.RaceDisplay;
        return string.Format(CultureInfo.InvariantCulture, table["US.Packs.Domain.NameWithState"], axis, name);
    }

    // P3 - secondary information stays secondary (GUARD: this slice does not touch these declarations).

    private static void SecondaryLinesStayMuted()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });

        // GUARD: passes under both the pre-V2 and V2 shapes - it is here so a later edit cannot take the
        // metadata lines into the title's ink role unnoticed.
        foreach ((string template, string[] titles, string[] muted) in new[]
                 {
                     ("race-layer-row", new[] { "race-layer-row-title" }, new[] { "race-layer-row-detail" }),
                     ("xenotype-layer-row", new[] { "xenotype-layer-row-title" }, new[] { "xenotype-layer-row-detail" }),
                     ("checklist-row", new[] { "checklist-row-label" }, new[] { "checklist-row-meta", "checklist-row-coverage" }),
                 })
        {
            UiElementSpec root = host.Manifest.Templates[template];
            foreach (string id in titles)
            {
                UiElementSpec? element = Find(root, id);
                Assert(element != null && !element.TryGetAttribute("Emphasis", out _),
                    "'" + id + "' is the row's title and must keep the primary ink role (no Emphasis)");
            }

            foreach (string id in muted)
            {
                UiElementSpec? element = Find(root, id);
                Assert(element != null
                    && element.TryGetAttribute("Emphasis", out string emphasis) && emphasis == "Muted",
                    "'" + id + "' is secondary information and must stay Emphasis=\"Muted\"");
            }
        }
    }

    // P4 - the real page box, both help states, both languages, long content and every empty state.

    private static void RealPageBoxAccessibility()
    {
        var metrics = new Program.StubMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        int savedWidth = Verse.UI.screenWidth;
        int savedHeight = Verse.UI.screenHeight;
        try
        {
            foreach (string language in new[] { "English", "ChineseSimplified" })
            {
                Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
                // The help state is PART of the configuration, but since BH1 it is NOT a width: the panel is
                // reserved out of the body's height above the footer, so help open and help closed are both
                // arranged in the one 760x524 box the policy gives a 1024x768 screen (centre column 524 in
                // either state). The pairing the pre-correction lane measured - 984 with no help column - is
                // not a frame production can produce, and neither is a 984 box with the help open.
                //
                // BH1 correction r2 (PM ruling): the reachability numbers below are measured against THE
                // STATE'S OWN viewport and reported honestly. The r1 detour - borrowing the taller
                // help-closed box as the budget reference while help was open - was a pseudo pass: a ruler
                // from a box the acceptance configuration does not have proves nothing about the box it
                // does. It is gone. The original two-screen guard's real status with the 140px panel is
                // recorded per state; business reachability is proven separately by the P4b real scroll +
                // click + typed-write step, and the cross-state clause after the loop still pins that the
                // panel moves no content position at all - it costs visible height, nothing else.
                var enableYByState = new Dictionary<string, float>();

                foreach ((string state, Vector2 box, bool helpOpen) in new[]
                         {
                             ("help open", PageBox, true),
                             ("help closed", PageBox, false),
                         })
                {
                    Verse.UI.screenWidth = 1024;
                    Verse.UI.screenHeight = 768;
                    // LONG CONTENT: 30 pack rows whose names/authors wrap at these widths.
                    var fake = new RecordingSettingsSource
                    {
                        RichData = true,
                        WrappingDomainText = true,
                        ChecklistPacks = LongPacks(30).ToArray(),
                    };
                    using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
                    host.Bindings.Invoke("set-tab", "Packs");
                    host.Bindings.Set("help-open", helpOpen);
                    host.MeasureAndArrange(box);
                    UiFitAudit.Reset();
                    reports.Clear();
                    UiLayoutSnapshot snapshot = host.MeasureAndArrange(box);
                    host.DrawChecked(new Rect(0f, 0f, box.x, box.y));

                    string where = "Packs " + language + " with the " + state + " at page box " + box.x + "x" + box.y;
                    foreach (string id in new[] { "filter-layer", "race-layer", "xenotype-layer", "checklist-card",
                                 "checklist-scope", "checklist-search" })
                    {
                        Assert(snapshot.RectById.ContainsKey(id), where + ": '" + id + "' must be arranged");
                    }

                    Assert(snapshot.Viewports.TryGetValue("content-scroll", out Rect viewport),
                        where + ": the content scroll must publish its viewport");
                    Assert(snapshot.ScrollContents.TryGetValue("content-scroll", out Rect content),
                        where + ": the content scroll must publish its content box");

                    // The arranged geometry must BE this help state's geometry, not the other one's. BH1: the
                    // open state arranges the 140px panel above the footer and keeps the settings' whole
                    // centre column; the closed state has no panel and hands that reservation back to the
                    // body. The centre column is therefore 524 in BOTH states - what the panel costs is body
                    // height, never width. This is the clause the pre-correction lane could not redden.
                    if (helpOpen)
                    {
                        Assert(snapshot.Viewports.ContainsKey("help-scroll"),
                            where + ": the bottom help panel must be arranged with help open");
                        Assert(snapshot.RectById.ContainsKey("body-row")
                                && snapshot.RectById.ContainsKey("nav-column")
                                && snapshot.Viewports.ContainsKey("content-scroll"),
                            where + ": the open help panel must be arranged with the body row, the nav column"
                            + " and the centre scroll still arranged - it does not replace the settings");
                        Assert(viewport.width >= 524f - 0.5f,
                            where + ": the help-open centre column must keep the whole 524px column (the panel"
                            + " takes height, not width), got " + viewport.width);
                    }
                    else
                    {
                        Assert(!snapshot.Viewports.ContainsKey("help-scroll"),
                            where + ": the help panel must not be arranged with help closed");
                        Assert(viewport.width >= 524f - 0.5f,
                            where + ": the help-closed centre column must be the same 524px column the open"
                            + " state keeps, got " + viewport.width);
                    }

                    Assert(content.height > viewport.height,
                        where + ": the long fixture must actually overflow the viewport, or the reachability"
                        + " clause below measures nothing (content " + content.height + " viewport " + viewport.height + ")");

                    // Reachability - BH1 correction r2 (PM ruling): the ORIGINAL two-screen guard is measured
                    // against THIS state's own viewport, never a borrowed closed-height reference. With the
                    // 140px panel reservation the guard really FAILS in the help-open state (enable y ~742.7
                    // against a ~256 viewport, ~2.9 screens); that failure is RECORDED here as an experience
                    // residual - its comfort is awaiting_human - instead of being papered over, and no run
                    // may claim the old guard passed with help open. The help-closed pass keeps its honest
                    // own-viewport result. Business reachability is the P4b proof below: session-scroll the
                    // deep enable control into view, click it with real pointer events, and read the typed
                    // write back at the source boundary. The long-BROWSE-list counterfactual stays the
                    // REPORT-ONLY residual printed after the loop.
                    Assert(snapshot.RectById.TryGetValue("checklist-card", out Rect enableCard),
                        where + ": the enable card must be arranged");
                    enableYByState[state] = enableCard.y;
                    string screensDown = (enableCard.y / viewport.height).ToString("0.00", CultureInfo.InvariantCulture);
                    if (enableCard.y > viewport.height * 2f)
                    {
                        Console.WriteLine("[v2-reachability-residual] " + where
                            + " ORIGINAL two-screen guard FAILS against this state's own viewport: enableY="
                            + Num(enableCard.y) + " viewport=" + Num(viewport.height) + " screensDown="
                            + screensDown + " - recorded per the PM r2 ruling as an EXPERIENCE RESIDUAL"
                            + " (awaiting_human), NOT claimed as a pass; reachability is proven by P4b.");
                    }
                    else
                    {
                        Console.WriteLine("[v2-reachability] " + where
                            + " two-screen guard holds for this state: enableY=" + Num(enableCard.y)
                            + " viewport=" + Num(viewport.height) + " screensDown=" + screensDown);
                    }
                    Assert(reports.Count == 0,
                        where + ": the fit audit must report nothing with long names, got " + Describe(reports));

                    // BH1's cross-state clauses on THIS host: one width function, so the two help states are
                    // arranged in the same page box, and the panel's whole cost is the height it reserves.
                    // Runs after the fit clause so its extra passes cannot add findings to this report set.
                    AssertHelpPanelReservesHeightNotWidth(host, box, where);

                    // Dropdown popup: opened by its owner inside the page. The owner/clip rule is the existing
                    // popup lanes' contract (CompositeDropdownPublishesCoveringRect); this checks the REAL page
                    // box containment.
                    Rect anchor = new(viewport.x + 40f, viewport.y + 10f, 120f, 24f);
                    host.Session.OpenPopup("pack-filter", anchor);
                    host.MeasureAndArrange(box);
                    host.DrawChecked(new Rect(0f, 0f, box.x, box.y));
                    Assert(Program.TryGetPopupHitLayer(host.Session, out UiHitLayer layer) && layer.IsPopup,
                        where + ": an open pack-filter dropdown must publish a POPUP layer");
                    Assert(layer.Rect.x >= -0.5f && layer.Rect.y >= -0.5f
                        && layer.Rect.xMax <= box.x + 0.5f && layer.Rect.yMax <= box.y + 0.5f,
                        where + ": the popup must stay inside the page, got " + Describe(layer.Rect));
                    host.Session.ClosePopup();

                    Console.WriteLine("[v2-access] " + where
                        + " viewport=" + Num(viewport.height) + " content=" + Num(content.height)
                        + " enableY=" + Num(enableCard.y) + " enableH=" + Num(enableCard.height)
                        + " fit=" + reports.Count + " popup=" + Describe(layer.Rect)
                        + " filterBand=" + Describe(snapshot.RectById["filter-layer"])
                        + " filterBody=" + Describe(snapshot.RectById["filter-bar"])
                        + " check=" + Describe(snapshot.RectById["checklist-row-check#us.pack1"]));
                }

                Assert(enableYByState.Count == 2
                        && Math.Abs(enableYByState["help open"] - enableYByState["help closed"]) < 0.01f,
                    language + ": opening the help panel must not move the enable band's content position (it"
                    + " costs visible height only): open y=" + Num(enableYByState["help open"])
                    + " closed y=" + Num(enableYByState["help closed"]));

                // P4b (BH1 correction r2, PM ruling): a GUARD for the near-tail row 29 enable control,
                // checked on the real Host in both help states - scroll input is the session seam, the click
                // is a real MouseDown/MouseUp pair at an asserted visible point, and the effect is read
                // back as a typed write at the source boundary.
                EnableDeepControlScrollClickAndTypedWrite(language, metrics, reports, helpOpen: true);
                EnableDeepControlScrollClickAndTypedWrite(language, metrics, reports, helpOpen: false);

                AssertEmptyStates(language, metrics, reports);
                ReportLongBrowseResidual(language, metrics);
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
            UiFitAudit.Detach();
            UiFitAudit.Enabled = false;
            Verse.UI.screenWidth = savedWidth;
            Verse.UI.screenHeight = savedHeight;
        }
    }

    /// <summary>
    /// P4b (BH1 correction r2, PM ruling): a business-reachability GUARD for a deep enable control. At
    /// the real page box (760x524), long content (30 wrapping pack rows) and each help state, the lane
    /// session-scrolls the centre column until row 29's check band is fully inside the visible
    /// viewport (coordinates and the visible intersection are asserted, then printed), posts a real
    /// MouseDown/MouseUp pair at its page point, and reads the typed write back at the source boundary:
    /// the target pack's enabled write is recorded, and no tab, help or scroll-to write happens - the help state itself
    /// must survive the click unchanged. What stays honestly UNVERIFIED and is labelled so in the printed
    /// record: the scroll is the session seam, not a real mouse wheel over the carrier.
    /// </summary>
    private static void EnableDeepControlScrollClickAndTypedWrite(
        string language, Program.StubMetrics metrics, List<UiOverflowReport> reports, bool helpOpen)
    {
        Verse.UI.screenWidth = 1024;
        Verse.UI.screenHeight = 768;
        var fake = new RecordingSettingsSource
        {
            RichData = true,
            WrappingDomainText = true,
            ChecklistPacks = LongPacks(30).ToArray(),
        };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Packs");
        host.Bindings.Set("help-open", helpOpen);
        host.MeasureAndArrange(PageBox);
        UiFitAudit.Reset();
        reports.Clear();
        string state = helpOpen ? "help open" : "help closed";
        string where = "Packs " + language + " deep-enable reachability (" + state + ") at page box "
            + PageBox.x + "x" + PageBox.y;

        // A DEEP enable control of the long fixture: row 29's check band. The fixture marks every third
        // row selected and 29 % 3 == 2, so the band starts OFF and the real click must land as
        // enabled=true at the source. Row 29 (not the very last row) because the centre-to-viewport
        // scroll wanted below must survive the clamp at maxScroll, or the band would sit at the fold edge
        // and the visible-intersection clause would measure a clipped target.
        const string targetId = "checklist-row-check#us.pack29";
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(PageBox);
        Assert(snapshot.RectById.TryGetValue(targetId, out Rect rowCheck),
            where + ": the near-tail row 29 enable control must be arranged in the content");
        bool hasViewport = snapshot.Viewports.TryGetValue("content-scroll", out Rect viewport);
        bool hasContent = snapshot.ScrollContents.TryGetValue("content-scroll", out Rect content);
        Assert(hasViewport && hasContent,
            where + ": the centre column must publish its viewport and content box");
        host.DrawChecked(new Rect(0f, 0f, PageBox.x, PageBox.y));

        // The proof must measure something: with the scroll at the top the target sits BELOW the fold.
        Assert(rowCheck.y > viewport.height,
            where + ": the deep control must start below the fold (row y=" + Num(rowCheck.y)
            + " viewport height=" + Num(viewport.height) + ") or the scroll step proves nothing");
        float maxScroll = Mathf.Max(0f, content.height - viewport.height);
        float wanted = Mathf.Clamp(rowCheck.y - viewport.height * 0.5f, 0f, maxScroll);
        Assert(wanted > 0f, where + ": reaching the control must really require scrolling, got " + Num(wanted));
        Program.SetScrollPositionById(host.Session, "content-scroll", new Vector2(0f, wanted));

        snapshot = host.MeasureAndArrange(PageBox);
        viewport = snapshot.Viewports["content-scroll"];
        Vector2 scroll = Program.ScrollPositionById(host.Session, "content-scroll");
        Assert(Math.Abs(scroll.y - wanted) <= 0.51f,
            where + ": the session must really hold the scroll position, got " + Num(scroll.y));

        // Page-space band after the scroll, with the VISIBLE-INTERSECTION clause the r2 ruling demands.
        // The snapshot's arranged rects of scroll children are PAGE-space at scroll zero - the viewport
        // origin is already folded in on both axes - and the draw pass shifts them by MINUS the scroll
        // position (Program.cs:2734 uses the same rect - scroll relation for the hit band; XG1's +viewport
        // transform belongs to a METRICS-captured rect, which sits in the scroll window's own origin).
        // The whole check band must lie inside the centre viewport, so the point below is provably a
        // point on the enable control and not outside the clip.
        var pageBand = new Rect(
            rowCheck.x - scroll.x,
            rowCheck.y - scroll.y,
            rowCheck.width,
            rowCheck.height);
        Assert(pageBand.y >= viewport.y + 1f && pageBand.yMax <= viewport.yMax - 1f
                && pageBand.x >= viewport.x + 1f && pageBand.xMax <= viewport.xMax - 1f,
            where + ": the enable band must be FULLY visible inside the viewport before the click (band "
            + Describe(pageBand) + ", viewport " + Describe(viewport) + ")");
        var point = new Vector2(pageBand.x + pageBand.width * 0.5f, pageBand.y + pageBand.height * 0.5f);

        // Reset the write recorders AFTER setup: the help-open binding legitimately wrote the source once
        // above. From here the click must be the ONLY write reaching the boundary.
        fake.LastPackScope = null;
        fake.LastPackRace = null;
        fake.LastPackTarget = null;
        fake.LastPackKey = null;
        fake.LastPackEnabled = null;
        fake.LastActiveTab = null;
        fake.LastHelpPanelOpen = null;
        fake.LastScrollToSection = null;

        Program.DrawWithEvent(host, new Rect(0f, 0f, PageBox.x, PageBox.y), EventType.MouseDown, point);
        Program.DrawWithEvent(host, new Rect(0f, 0f, PageBox.x, PageBox.y), EventType.MouseUp, point);
        GUIUtility.hotControl = 0;
        Event.current = null;

        // (1) the typed write: exactly the clicked pack, flipped from the fixture's off state.
        Assert(string.Equals(fake.LastPackKey, "us.pack29", StringComparison.Ordinal),
            where + ": the click must write the deep pack's key at the source boundary, got '"
            + (fake.LastPackKey ?? "no write") + "'");
        Assert(fake.LastPackEnabled == true,
            where + ": the unselected row must flip to enabled, got "
            + (fake.LastPackEnabled?.ToString() ?? "no write"));
        Assert(fake.LastPackScope != null && !string.IsNullOrEmpty(fake.LastPackRace),
            where + ": the write must carry its domain identity, got scope=" + fake.LastPackScope
            + " race='" + (fake.LastPackRace ?? "") + "'");

        // (2) nothing else was touched: no workspace switch, no help/footer write, help state survives.
        Assert(fake.LastActiveTab == null, where + ": the click must not write the active tab");
        Assert(fake.LastHelpPanelOpen == null, where + ": the click must not write the help state");
        Assert(fake.LastScrollToSection == null, where + ": the click must not write a scroll-to target");
        Assert(host.Bindings.Get<bool>("help-open") == helpOpen,
            where + ": the help state must SURVIVE the click unchanged");

        Console.WriteLine("[v2-reach-p4b] " + where
            + " enableY=" + Num(rowCheck.y) + " viewport=" + Num(viewport.height)
            + " screensDown=" + (rowCheck.y / viewport.height).ToString("0.00", CultureInfo.InvariantCulture)
            + " scrollTo=" + Num(wanted) + " point=" + Num(point.x) + "," + Num(point.y)
            + " band=" + Describe(pageBand) + " write=us.pack29->true tab=null help=null"
            + " (scroll is the SESSION seam; real mouse wheel over the carrier: UNVERIFIED)");
        Assert(reports.Count == 0, where + ": the reach pass must fit at the real box, got " + Describe(reports));
    }

    /// <summary>
    /// The three empty states, each with the fixture that produces it, at BOTH help states of the single BH1
    /// page box (760x524 at the game's minimum screen, help open and help closed). Mutation proof: swapping
    /// checklist-empty-domain and checklist-empty-search VisibleKeys reddens the pair clauses; removing
    /// VisibleKey="checklist-has-domain" from the band composite (or the list column) reddens the no-domain
    /// clauses, because the list's search field would arrange with no domain.
    /// </summary>
    private static void AssertEmptyStates(string language, Program.StubMetrics metrics, List<UiOverflowReport> reports)
    {
        foreach ((string state, Vector2 box, bool helpOpen) in new[]
                 {
                     ("help open", PageBox, true),
                     ("help closed", PageBox, false),
                 })
        {
            // (a) no domain at all: the list (and its search field and scope line) yields to the no-domain state.
            RunEmptyCase(language, metrics, reports, new RecordingSettingsSource { RichData = false }, box, helpOpen,
                present: new[] { "checklist-empty-nodomain" },
                absent: new[] { "checklist-empty-domain", "checklist-empty-search", "checklist", "checklist-scope",
                                "checklist-list", "checklist-search" },
                label: state + ", no domain");

            // (b) a selected domain with no pack installed.
            RunEmptyCase(language, metrics, reports,
                new RecordingSettingsSource { RichData = true, ChecklistPacks = Array.Empty<VoicePackRowView>() },
                box, helpOpen,
                present: new[] { "checklist-empty-domain", "checklist-search", "checklist-scope" },
                absent: new[] { "checklist-empty-search" },
                label: state + ", domain with no packs");

            // (c) packs exist but the search matches nothing.
            RunEmptyCase(language, metrics, reports, new RecordingSettingsSource { RichData = true }, box, helpOpen,
                present: new[] { "checklist-empty-search", "checklist-search", "checklist-scope" },
                absent: new[] { "checklist-empty-domain" },
                label: state + ", search with no match",
                searchText: "zzz-no-match");
        }
    }

    private static void RunEmptyCase(
        string language, Program.StubMetrics metrics, List<UiOverflowReport> reports,
        RecordingSettingsSource fake, Vector2 box, bool helpOpen,
        string[] present, string[] absent, string label, string? searchText = null)
    {
        Verse.UI.screenWidth = 1024;
        Verse.UI.screenHeight = 768;
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Packs");
        host.Bindings.Set("help-open", helpOpen);
        if (searchText != null) host.Bindings.Set("search-text", searchText);
        UiFitAudit.Reset();
        reports.Clear();
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(box);
        host.DrawChecked(new Rect(0f, 0f, box.x, box.y));

        string where = "Packs " + language + " empty state (" + label + ") at " + box.x + "x" + box.y;
        string cardHeight = snapshot.RectById.TryGetValue("checklist-card", out Rect card)
            ? Num(card.height) : "(absent)";
        Console.WriteLine("[v2-empty] " + where + " arranged=" + string.Join(",", present)
            + " cardHeight=" + cardHeight + " fit=" + reports.Count);

        Assert(snapshot.Viewports.ContainsKey("help-scroll") == helpOpen,
            where + ": the bottom help panel must be arranged exactly while help is open");
        if (helpOpen)
        {
            Assert(snapshot.RectById.ContainsKey("body-row") && snapshot.RectById.ContainsKey("nav-column")
                    && snapshot.Viewports.ContainsKey("content-scroll"),
                where + ": the open help panel must be arranged BESIDE a still-arranged body row, nav column"
                + " and centre scroll (BH1 reserves height above the footer; it does not replace the page)");
        }
        else
        {
            Assert(snapshot.RectById.TryGetValue("body-row", out Rect closedBody),
                where + ": the body row must stay arranged with help closed");
            Assert(snapshot.RectById.TryGetValue("footer-band", out Rect closedFooter),
                where + ": the footer band must stay arranged with help closed");
            Assert(Math.Abs(closedBody.yMax + DeclaredFloat(host.Manifest.Roots[0], "Gap")
                    - closedFooter.y) <= 0.5f,
                where + ": with help closed the body row must keep the WHOLE leftover down to the footer,"
                + " paying only the page gap (body yMax " + Num(closedBody.yMax) + ", footer y "
                + Num(closedFooter.y) + ")");
        }

        foreach (string id in present)
        {
            Assert(snapshot.RectById.ContainsKey(id), where + ": '" + id + "' must be arranged");
        }

        foreach (string id in absent)
        {
            Assert(!snapshot.RectById.ContainsKey(id), where + ": '" + id + "' must NOT be arranged");
        }

        Assert(reports.Count == 0, where + ": the fit audit must report nothing, got " + Describe(reports));
    }

    /// <summary>
    /// REPORT-ONLY residual (no assertion gates it, the B3 precedent): how far a LONG browse list pushes the
    /// enable band down. The vocabulary has no MinHeight/MaxHeight, so a declared Height binds in BOTH
    /// directions and would raise the band for short lists - reported to the Lead, not asserted.
    /// </summary>
    private static void ReportLongBrowseResidual(string language, Program.StubMetrics metrics)
    {
        var longRaces = new List<RaceLayerRowView>();
        for (int i = 0; i < 30; i++)
        {
            longRaces.Add(new RaceLayerRowView(
                "race" + i, "Race " + i + " with a long display name that wraps", 2, 3,
                SqueakVoicePackDomainState.Available));
        }

        Verse.UI.screenWidth = 1024;
        Verse.UI.screenHeight = 768;
        var fake = new RecordingSettingsSource { RichData = true, WrappingDomainText = true, Races = longRaces.ToArray() };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Packs");
        host.Bindings.Set("help-open", true); // the acceptance configuration (help open)
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(PageBox);
        if (snapshot.Viewports.TryGetValue("content-scroll", out Rect viewport)
            && snapshot.ScrollContents.TryGetValue("content-scroll", out Rect content)
            && snapshot.RectById.TryGetValue("checklist-card", out Rect enableCard))
        {
            Console.WriteLine("[v2-residual] " + language + " help open, 30 browse races: enableY=" + Num(enableCard.y)
                + " viewportsDown=" + (enableCard.y / viewport.height).ToString("0.00", CultureInfo.InvariantCulture)
                + " content=" + Num(content.height)
                + " (no assertion: fixed-Height bound is not MinHeight/MaxHeight, reported to the Lead)");
        }
    }

    // P5 - preservation and channel separation.

    private static void PreservationAndSeparation()
    {
        var metrics = new Program.StubMetrics();
        var fake = new RecordingSettingsSource { RichData = true };
        // The write registry itself is DisplayWriteAdvancesSharedRevision's contract (both directions); the
        // read-only fact for the new scope binding is asserted next to the scope element in the P2 step.
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics, out _);
        host.Bindings.Invoke("set-tab", "Packs");
        host.MeasureAndArrange(PageBox);

        foreach (string id in PacksElementIds)
        {
            Assert(FindAnywhere(host.Manifest, id) != null, "the Packs inventory lost the element '" + id + "'");
        }

        foreach ((string id, string attribute, string value) in PacksByValuePins)
        {
            UiElementSpec element = FindAnywhere(host.Manifest, id)
                ?? throw new InvalidOperationException("the Packs inventory lost the element '" + id + "'");
            // Kind is a spec property, not an attribute in the map.
            string actual = string.Equals(attribute, "Kind", StringComparison.Ordinal)
                ? element.Kind
                : (element.TryGetAttribute(attribute, out string raw) ? raw : "");
            Assert(string.Equals(actual, value, StringComparison.Ordinal),
                "the Packs declaration changed '" + id + "'." + attribute + ": expected '" + value + "', got '"
                + actual + "'");
        }

        // SEPARATION (V2.2) on the INCREMENT: reset the recorders, make one browse write, and the enable
        // channel must stay untouched - then the reverse.
        fake.LastSelectedScope = null;
        fake.LastSelectedRace = null;
        fake.LastSelectedTarget = null;
        fake.LastPackKey = null;
        fake.LastPackEnabled = null;
        host.Bindings.Invoke("select-domain", "testrace");
        Assert(fake.LastSelectedRace == "testrace",
            "the browse write must reach the business boundary, got '" + (fake.LastSelectedRace ?? "null") + "'");
        Assert(fake.LastPackKey == null && fake.LastPackEnabled == null,
            "selecting a browse domain must not touch the enable channel (LastPackKey="
            + (fake.LastPackKey ?? "null") + ")");

        fake.LastSelectedScope = null;
        fake.LastSelectedRace = null;
        fake.LastSelectedTarget = null;
        fake.LastPackKey = null;
        fake.LastPackEnabled = null;
        host.Bindings.Set("checklist-pack-keys.us.sang2.enabled", true);
        Assert(fake.LastPackKey == "us.sang2" && fake.LastPackEnabled == true,
            "the enable write must reach the business boundary, got '" + (fake.LastPackKey ?? "null")
            + "' enabled=" + fake.LastPackEnabled);
        Assert(fake.LastSelectedScope == null && fake.LastSelectedRace == null && fake.LastSelectedTarget == null,
            "enabling a pack must not change the browse selection (LastSelectedRace="
            + (fake.LastSelectedRace ?? "null") + ")");
    }

    private static List<VoicePackRowView> LongPacks(int count)
    {
        var packs = new List<VoicePackRowView>();
        for (int i = 1; i <= count; i++)
        {
            packs.Add(new VoicePackRowView(
                "us.pack" + i,
                "A very long VoicePack name number " + i + " that wraps at this width",
                "TestMod" + i,
                "AuthorWithALongName" + i,
                "def.pack" + i,
                "full",
                "search" + i,
                isSelected: (i % 3) == 0));
        }

        return packs;
    }

    /// <summary>
    /// BH1's cross-state clause, asserted on the host the caller is measuring. The window policy has ONE width
    /// function and the help state is not an input to it, so both help states must be arranged in the SAME
    /// page box; the bottom panel must appear exactly in the open pass; the open pass must still arrange the
    /// body row, the nav column and the centre scroll; and closing the panel must hand its WHOLE reservation
    /// (its declared height plus the page gap it no longer pays) back to the body. Every number is read out of
    /// the manifest and the policy, never re-declared here. The caller's help state is restored.
    /// </summary>
    private static void AssertHelpPanelReservesHeightNotWidth(UiHost host, Vector2 box, string where)
    {
        UiElementSpec pageRoot = host.Manifest.Roots[0];
        Assert(string.Equals(pageRoot.Id, "page-root", StringComparison.Ordinal),
            where + ": the page's band column must be 'page-root', got '" + pageRoot.Id + "'");
        UiElementSpec panel = Find(pageRoot, "help-scroll")
            ?? throw new InvalidOperationException("the manifest must declare the bottom help panel 'help-scroll'");
        float panelHeight = DeclaredFloat(panel, "Height");
        float pageGap = DeclaredFloat(pageRoot, "Gap");
        float pagePadding = DeclaredFloat(pageRoot, "Padding");
        float policyPageWidth = WindowChromeLayout.SettingsWindowWidth(MinimumScreenWidth, MinimumScreenHeight)
            - WindowChromeLayout.WindowChromeInset;
        bool state = host.Bindings.Get<bool>("help-open");

        host.Bindings.Set("help-open", true);
        UiLayoutSnapshot open = host.MeasureAndArrange(box);
        host.Bindings.Set("help-open", false);
        UiLayoutSnapshot closed = host.MeasureAndArrange(box);
        host.Bindings.Set("help-open", state);

        Assert(open.RectById.TryGetValue("page-root", out Rect openPage),
            where + ": the open pass must arrange the page root");
        Assert(closed.RectById.TryGetValue("page-root", out Rect closedPage),
            where + ": the closed pass must arrange the page root");
        Assert(Math.Abs(openPage.width - policyPageWidth) <= 0.5f
                && Math.Abs(closedPage.width - policyPageWidth) <= 0.5f
                && Math.Abs(openPage.x - closedPage.x) <= 0.5f
                && Math.Abs(openPage.y - closedPage.y) <= 0.5f
                && Math.Abs(openPage.width - closedPage.width) <= 0.5f
                && Math.Abs(openPage.height - closedPage.height) <= 0.5f,
            where + ": BH1 leaves BOTH help states the same policy page box " + Num(policyPageWidth) + "x"
            + Num(box.y) + " - open " + Describe(openPage) + ", closed " + Describe(closedPage));

        Assert(open.Viewports.TryGetValue("help-scroll", out Rect helpRect),
            where + ": the open pass must arrange the bottom help panel");
        Assert(!closed.Viewports.ContainsKey("help-scroll"),
            where + ": the closed pass must not arrange the bottom help panel");
        Assert(Math.Abs(helpRect.height - panelHeight) <= 0.5f
                && Math.Abs(helpRect.width - (openPage.width - pagePadding * 2f)) <= 0.5f,
            where + ": the panel must hold its declared reservation across the page's inner width, got "
            + Describe(helpRect) + " (declared Height " + Num(panelHeight) + ", inner width "
            + Num(openPage.width - pagePadding * 2f) + ")");
        Assert(open.RectById.TryGetValue("help-panel", out Rect helpBody),
            where + ": the help topic composite must be arranged while help is open");
        Assert(helpBody.width > 0f && helpBody.height > 0f
                && helpBody.width <= helpRect.width + 0.5f
                && Math.Abs(helpBody.x - helpRect.x) <= 0.5f
                && helpBody.y >= helpRect.y - 0.5f,
            where + ": the help topic composite must be arranged inside the reserved band (a longer topic"
            + " scrolls within it, so only its left edge and its width are bounded)");

        Assert(open.RectById.TryGetValue("body-row", out Rect openBody),
            where + ": the open pass must arrange the body row");
        Assert(closed.RectById.TryGetValue("body-row", out Rect closedBody),
            where + ": the closed pass must arrange the body row");
        Assert(open.RectById.ContainsKey("nav-column") && open.Viewports.ContainsKey("content-scroll")
                && closed.RectById.ContainsKey("nav-column") && closed.Viewports.ContainsKey("content-scroll"),
            where + ": the body row, the nav column and the centre scroll must stay arranged in BOTH help states");
        Assert(Math.Abs(closedBody.height - (openBody.height + panelHeight + pageGap)) <= 0.5f,
            where + ": closing the panel must hand its whole reservation back to the body row (open "
            + Num(openBody.height) + " + panel " + Num(panelHeight) + " + gap " + Num(pageGap) + " vs closed "
            + Num(closedBody.height) + ")");
        Assert(Math.Abs(openBody.width - closedBody.width) <= 0.01f
                && Math.Abs(open.Viewports["content-scroll"].width
                    - closed.Viewports["content-scroll"].width) <= 0.01f,
            where + ": the help panel may cost the settings no width at all (body " + Num(openBody.width)
            + " vs " + Num(closedBody.width) + ", centre " + Num(open.Viewports["content-scroll"].width)
            + " vs " + Num(closed.Viewports["content-scroll"].width) + ")");
    }

    /// <summary>One declared numeric attribute, so a lane never re-spells a manifest number.</summary>
    private static float DeclaredFloat(UiElementSpec spec, string attribute)
    {
        if (!spec.TryGetAttribute(attribute, out string raw))
        {
            throw new InvalidOperationException("'" + spec.Id + "' must declare " + attribute);
        }

        return float.Parse(raw.Trim(), CultureInfo.InvariantCulture);
    }

    private static UiElementSpec? Find(UiElementSpec root, string id)
    {
        if (string.Equals(root.Id, id, StringComparison.Ordinal)) return root;
        foreach (UiElementSpec child in root.Children)
        {
            UiElementSpec? found = Find(child, id);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>Finds an element anywhere the manifest declares it - page roots or a template subtree.</summary>
    private static UiElementSpec? FindAnywhere(UiLayoutManifest manifest, string id)
    {
        foreach (UiElementSpec root in manifest.Roots)
        {
            UiElementSpec? found = Find(root, id);
            if (found != null) return found;
        }

        foreach (UiElementSpec template in manifest.Templates.Values)
        {
            UiElementSpec? found = Find(template, id);
            if (found != null) return found;
        }

        return null;
    }

    private static IEnumerable<UiElementSpec> Descendants(UiElementSpec root)
    {
        foreach (UiElementSpec child in root.Children)
        {
            yield return child;
            foreach (UiElementSpec nested in Descendants(child)) yield return nested;
        }
    }

    private static bool HasDescendantKind(UiElementSpec root, string kind)
    {
        return Descendants(root).Any(e => string.Equals(e.Kind, kind, StringComparison.Ordinal));
    }

    private static string Describe(List<UiOverflowReport> reports)
    {
        var text = new System.Text.StringBuilder();
        text.Append(reports.Count).Append(" finding(s)");
        foreach (UiOverflowReport report in reports)
        {
            text.Append("; ").Append(report.ElementPath).Append(" ").Append(report.Axis)
                .Append(" needs ").Append(report.Needed).Append(" has ").Append(report.Available);
        }

        return text.ToString();
    }

    private static string Describe(Rect rect)
    {
        return "(" + Num(rect.x) + "," + Num(rect.y) + "," + Num(rect.width) + "," + Num(rect.height) + ")";
    }

    private static string Num(float value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("PacksHierarchyLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
