using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;
namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// US-PACK1: the pack-card result page. This lane REPLACES three retired lanes and inherits their
/// still-true claims - named here so the replacement scenarios are on the record, per the dispatch
/// contract's "先说清替代场景":
/// <list type="bullet">
/// <item><description>DeclarativePacksLaneTests (S4-2): "one hit element per row, the hit IS the row,
/// each row reports its own key, selection lands on exactly one row" - the declarative row set now
/// lives on <c>pack-card-row</c>; UsSelectionSurfaceLaneTests and SettingsGeometryLaneTests keep the
/// surface/geometry halves re-pointed there, and THIS lane owns the identity/key halves (steps 2-3).</description></item>
/// <item><description>PacksHierarchyLaneTests (V2 P1-P5): "filter -> browse -> enable" became "filter ->
/// cards" by the accepted §4.2 redesign; the P2 claim (browse selection and enable are DIFFERENT
/// channels) survives INSIDE a card - the domain-row hit selects, the row's switch enables - asserted
/// in steps 4-5.</description></item>
/// <item><description>ChecklistItemsLaneTests (B-1): "the projected item keys pass the predicate in
/// both directions, in order, deduplicated" - re-founded on the pure <see cref="PackCardProjection"/>
/// in step 1 and on the host's flat key list in step 2.</description></item>
/// </list>
/// The behavior checks the PACK1 contract demands: pack-name hit lists the card collapsed; xenotype-name
/// hit auto-expands and shows that row; clearing the query restores exactly the manual set; filtering
/// and expanding never write enable; two rows of one card write two independent domain identities;
/// empty result vs empty catalog are different sentences; the whole path runs at the real minimum page
/// box in both languages.
/// Honest limits: the fake projects the fixture through the SAME pure Build the model calls, so the
/// rule tested is production's rule; the real catalog/settings collection is exercised by the in-game
/// pass, not here.
/// </summary>
internal static class PackCardsLaneTests
{
    private const float PageWidth = 1280f;
    private const float PageHeight = 900f;
    private const float MinimumPageBoxWidth = 760f;
    private const float MinimumPageBoxHeight = 524f;

    internal static void RunAll()
    {
        Step("the pure projection accepts and rejects in both directions, in order", ProjectionBothDirectionsAndOrder);
        Step("the host's flat key list is the card list plus the OPEN cards' rows", HostKeyListIsTheScreen);
        Step("one card, two rows, two identities: the switches cannot address each other", TwoRowsWriteTwoIdentities);
        Step("filter and expansion never write enable, selection or persistence", FilteringNeverWritesEnable);
        Step("clearing the keyword restores exactly the manual expansion set", ClearingQueryRestoresManualExpansion);
        Step("real clicks open a card and select a domain through the drawn page", RealClicksOpenAndSelect);
        Step("empty result and empty catalog are different sentences", EmptyResultVersusEmptyCatalog);
        Step("the card page arranges and reads at the minimum box in EN and ZH", MinimumBoxBothLanguages);
        Console.WriteLine("[us-pack1] projection both directions + order; flat key list = headers + open rows; "
            + "two rows two identities; filter/expansion writes zero enable; cleared query restores the manual "
            + "set exactly; real click opened the card and selected the domain; empty result != empty catalog; "
            + "minimum box arranged EN+ZH with zero overflow reports");
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("PackCards lane step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("PackCards lane: " + message);
    }

    // ---------------------------------------------------------------- the pure rule (step 1)

    private static PackCardSourceRow Pair(string packKey, string label, string def, string mod, string author,
        SqueakVoicePackScope scope, string race, string target, string domainDisplay, bool enabled,
        bool orphan = false, bool conflict = false)
        => new(packKey, label, def, mod, author, "Actions 5/17", scope, race, target, domainDisplay,
            enabled, conflict, false, false, orphan);

    private static void ProjectionBothDirectionsAndOrder()
    {
        var source = new List<PackCardSourceRow>
        {
            Pair("mod.a:rat", "Ratkin Squeaks", "rat_pack", "Rat Mod", "Alice",
                SqueakVoicePackScope.Race, "ratkin", "", "Ratkin", enabled: true),
            Pair("mod.a:rat", "Ratkin Squeaks", "rat_pack", "Rat Mod", "Alice",
                SqueakVoicePackScope.Xenotype, "ratkin", "squire", "Squire", enabled: false),
            Pair("mod.b:sang", "Sanguophage Voice", "sang_pack", "Sang Mod", "Bob",
                SqueakVoicePackScope.Xenotype, "human", "sanguophage", "Sanguophage", enabled: true, conflict: true),
        };
        var manual = new List<string>();
        UiPackFilter noAuthor = default;
        UiDomainFilter noState = default;

        // No conditions: every pair survives; cards follow FIRST appearance (mod.a:rat, then mod.b:sang);
        // rows follow scan order; nothing expands on its own.
        List<PackCardView> all = PackCardProjection.Build(source, "", in noAuthor, "", "",
            in noState, manual);
        Assert(all.Count == 2 && all[0].Key == "mod.a:rat" && all[1].Key == "mod.b:sang",
            "card order is first appearance, got " + string.Join(",", all.ConvertAll(c => c.Key)));
        Assert(all[0].Rows.Count == 2 && all[0].Rows[0].Scope == SqueakVoicePackScope.Race
                && all[0].Rows[1].TargetDefName == "squire",
            "row order is scan order within the card");
        Assert(!all[0].Expanded && !all[1].Expanded,
            "no query and no manual gesture means every card stands collapsed");

        // Pack-name hit: the card lists COLLAPSED (the hit is on the pack half).
        List<PackCardView> byPack = PackCardProjection.Build(source, "Squeaks", in noAuthor, "", "",
            in noState, manual);
        Assert(byPack.Count == 1 && byPack[0].Key == "mod.a:rat" && !byPack[0].Expanded,
            "a pack-name hit lists the card but does not open it");
        Assert(byPack[0].Rows.Count == 2 && !byPack[0].Rows[0].AutoTrigger && !byPack[0].Rows[1].AutoTrigger,
            "a pack-only hit marks NO domain row (the rows survive through the pack half)");

        // Xenotype-name hit: the providing card AUTO-EXPANDS and the hit row is marked.
        List<PackCardView> byXeno = PackCardProjection.Build(source, "squire", in noAuthor, "", "",
            in noState, manual);
        Assert(byXeno.Count == 1 && byXeno[0].Key == "mod.a:rat" && byXeno[0].AutoExpanded && byXeno[0].Expanded,
            "the xenotype hit auto-expands the card that provides it");
        Assert(byXeno[0].Rows.Count == 1 && byXeno[0].Rows[0].TargetDefName == "squire"
                && byXeno[0].Rows[0].AutoTrigger,
            "the expanded card shows exactly the hit content row");

        // Author condition (pack half) composes with the keyword (pack-name half): AND across conditions.
        UiPackFilter bob = new("Bob");
        List<PackCardView> authorAndKeyword = PackCardProjection.Build(source, "Voice", in bob, "", "",
            in noState, manual);
        Assert(authorAndKeyword.Count == 1 && authorAndKeyword[0].Key == "mod.b:sang",
            "author=Bob AND keyword=Voice leaves only Bob's card");
        List<PackCardView> raceRatkin = PackCardProjection.Build(source, "", in noAuthor, "ratkin", "",
            in noState, manual);
        Assert(raceRatkin.Count == 1 && raceRatkin[0].Rows.Count == 2,
            "race=ratkin keeps the race domain plus its xenotype domain");

        // Xenotype dropdown: only that (race, xenotype) domain row; race domains step out. And
        // review-1 correction 1: the dropdown ALONE (empty keyword) auto-expands the card providing
        // the selected domain - §4.2's "expose the corresponding content" - while clearing it restores
        // the manual set (empty here, so the card is collapsed again; nothing sticks open).
        List<PackCardView> xenoSang = PackCardProjection.Build(source, "", in noAuthor, "", "sanguophage",
            in noState, manual);
        Assert(xenoSang.Count == 1 && xenoSang[0].Key == "mod.b:sang" && xenoSang[0].Rows.Count == 1
                && xenoSang[0].Rows[0].Scope == SqueakVoicePackScope.Xenotype
                && xenoSang[0].Rows[0].AutoTrigger && xenoSang[0].AutoExpanded && xenoSang[0].Expanded,
            "xenotype=sanguophage selects that domain row AND auto-expands its card with no keyword up");
        List<PackCardView> dropdownCleared = PackCardProjection.Build(source, "", in noAuthor, "", "",
            in noState, manual);
        Assert(dropdownCleared.Count == 2 && !dropdownCleared[1].Expanded && !dropdownCleared[1].ManualExpanded,
            "clearing the dropdown leaves no stuck-open card - the manual set is what remains");

        // State flags act on the DOMAIN half: Enabled only keeps enabled pairs; Conflicts keeps the
        // conflicting domain's pairs.
        UiDomainFilter enabledOnly = new(enabledOnly: true);
        List<PackCardView> enabled = PackCardProjection.Build(source, "", in noAuthor, "", "",
            in enabledOnly, manual);
        Assert(enabled.Count == 2
                && enabled[0].Rows.Count == 1 && enabled[0].Rows[0].Scope == SqueakVoicePackScope.Race
                && enabled[1].Rows.Count == 1 && enabled[1].Rows[0].TargetDefName == "sanguophage",
            "Enabled only keeps exactly the enabled pairs");
        UiDomainFilter conflicts = new(conflictOnly: true);
        List<PackCardView> conflicted = PackCardProjection.Build(source, "", in noAuthor, "", "",
            in conflicts, manual);
        Assert(conflicted.Count == 1 && conflicted[0].Key == "mod.b:sang",
            "Conflicts keeps the conflicting domain's card");

        // The manual set is the ONLY other way open - and it survives every filter unchanged.
        manual.Add("mod.b:sang");
        List<PackCardView> opened = PackCardProjection.Build(source, "Voice", in bob, "", "",
            in noState, manual);
        Assert(opened.Count == 1 && opened[0].ManualExpanded && opened[0].Expanded && !opened[0].AutoExpanded,
            "the manual gesture opens the card without a query hit pretending to be the player");
        manual.Clear();
    }

    // ---------------------------------------------------------------- host key list (step 2)

    private static void HostKeyListIsTheScreen()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Packs");
        ArrangeAndDraw(host);

        IReadOnlyList<string> collapsed = host.Bindings.Get<IReadOnlyList<string>>("pack-card-keys");
        Assert(collapsed.Count == 2
                && collapsed[0] == "us.sang" && collapsed[1] == "us.sang2",
            "with nothing open the list is exactly the two headers, in projection order, got "
            + string.Join(",", collapsed));

        host.Bindings.Invoke(UsWriteBindings.ItemKey("pack-card-keys", "us.sang", "toggle-pack-card"), "");
        ArrangeAndDraw(host);
        IReadOnlyList<string> opened = host.Bindings.Get<IReadOnlyList<string>>("pack-card-keys");
        Assert(opened.Count == 4 && opened[0] == "us.sang"
                && opened[1] == "us.sang|human|sanguophage" && opened[2] == "us.sang|human"
                && opened[3] == "us.sang2",
            "the open card contributes its rows directly after its header, xenotype first (scan order), got "
            + string.Join(",", opened));

        // Every key resolves its item-local reads (the list never lies about what the screen can draw).
        foreach (string key in opened)
        {
            Assert(host.Bindings.TryGet(UsWriteBindings.ItemKey("pack-card-keys", key, "is-header"), out bool isHeader)
                    && host.Bindings.TryGet(UsWriteBindings.ItemKey("pack-card-keys", key, "is-domain"), out bool isDomain)
                    && isHeader != isDomain,
                "key '" + key + "' resolves both kind flags and exactly one is true");
            Assert(host.Bindings.TryGet(UsWriteBindings.ItemKey("pack-card-keys", key, "title"), out string title)
                    && title.Length > 0,
                "key '" + key + "' resolves a non-empty title");
            Assert(host.Bindings.TryGet(UsWriteBindings.ItemKey("pack-card-keys", key, "payload"), out string payload)
                    && payload.Length > 0,
                "key '" + key + "' resolves its payload");
        }
    }

    // ---------------------------------------------------------------- identities (step 3)

    private static void TwoRowsWriteTwoIdentities()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Packs");
        ArrangeAndDraw(host);   // materialises the header rows, which registers their toggle-pack-card
        host.Bindings.Invoke(UsWriteBindings.ItemKey("pack-card-keys", "us.sang", "toggle-pack-card"), "");
        ArrangeAndDraw(host);

        // The race row of us.sang: disabled in the fixture. Enabling writes the RACE identity only.
        host.Bindings.Set(UsWriteBindings.ItemKey("pack-card-keys", "us.sang|human", "enabled"), true);
        Assert(fake.LastPackKey == "us.sang" && fake.LastPackScope == SqueakVoicePackScope.Race
                && fake.LastPackRace == "human" && fake.LastPackTarget == "" && fake.LastPackEnabled == true,
            "the race row's switch wrote (Race, human, \"\", us.sang), got "
            + fake.LastPackScope + "/" + fake.LastPackRace + "/" + fake.LastPackTarget);

        // The xenotype row of the SAME pack: already enabled; disabling writes the XENOTYPE identity.
        host.Bindings.Set(UsWriteBindings.ItemKey("pack-card-keys", "us.sang|human|sanguophage", "enabled"), false);
        Assert(fake.LastPackKey == "us.sang" && fake.LastPackScope == SqueakVoicePackScope.Xenotype
                && fake.LastPackRace == "human" && fake.LastPackTarget == "sanguophage"
                && fake.LastPackEnabled == false,
            "the xenotype row's switch wrote the xenotype identity of the same pack key");

        // And the live read-back: after the second write the race row still answers enabled (the writes
        // are whole-domain replacements under DIFFERENT identities - no crosstalk).
        ArrangeAndDraw(host);
        Assert(host.Bindings.TryGet(UsWriteBindings.ItemKey("pack-card-keys", "us.sang|human", "enabled"), out bool raceStill)
                && raceStill,
            "the xenotype write did not reach into the race row's selection");
        Assert(host.Bindings.TryGet(UsWriteBindings.ItemKey("pack-card-keys", "us.sang|human|sanguophage", "enabled"), out bool xenoNow)
                && !xenoNow,
            "the xenotype row reads back its own write");
    }

    // ---------------------------------------------------------------- zero writes (step 4)

    private static void FilteringNeverWritesEnable()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Packs");
        ArrangeAndDraw(host);

        host.Bindings.Set("search-text", "sang");
        host.Bindings.Set("pack-filter", "AuthorA");
        host.Bindings.Set("race-filter", "human");
        host.Bindings.Set("xenotype-filter", "sanguophage");
        host.Bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.EnabledOnly, true));
        host.Bindings.Invoke(UsWriteBindings.ItemKey("pack-card-keys", "us.sang", "toggle-pack-card"), "");
        ArrangeAndDraw(host);

        Assert(fake.LastPackKey == null && fake.LastPackEnabled == null,
            "five filter gestures and an expansion wrote NO enable value at the business boundary");
        Assert(fake.LastSelectedScope == null,
            "filtering never selects a domain either");
        // The echo names what is active (the region's visibility claim), and All clears it.
        Assert(host.Bindings.TryGet("pack-filter-summary", out string summary) && summary.Length > 0
                && host.Bindings.TryGet("pack-filter-active", out bool active) && active,
            "the condition echo is visible and non-empty while conditions are active");
        host.Bindings.Invoke("clear-pack-filters", "");
        ArrangeAndDraw(host);
        Assert(host.Bindings.TryGet("pack-filter-active", out bool stillActive) && !stillActive,
            "All clears every condition and the echo steps out");
        Assert(fake.LastPackKey == null && fake.ViewState.PackCardsExpanded.Count == 0
                || fake.LastPackKey == null,
            "the clear touched no enable value");
    }

    // ---------------------------------------------------------------- manual restore (step 5)

    private static void ClearingQueryRestoresManualExpansion()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Packs");
        ArrangeAndDraw(host);

        // The player opens us.sang2 themselves; us.sang stays closed.
        host.Bindings.Invoke(UsWriteBindings.ItemKey("pack-card-keys", "us.sang2", "toggle-pack-card"), "");
        ArrangeAndDraw(host);
        Assert(host.Bindings.TryGet(UsWriteBindings.ItemKey("pack-card-keys", "us.sang2", "expanded"), out bool manualNow)
                && manualNow,
            "setup: the manual gesture opened us.sang2");

        // A xenotype keyword auto-opens us.sang alongside it (the hit row is the xenotype's own name).
        host.Bindings.Set("search-text", "sanguophage");
        ArrangeAndDraw(host);
        PackCardView sang = fake.BuildView().PackCards[0];
        PackCardView sang2 = fake.BuildView().PackCards[1];
        Assert(sang.Key == "us.sang" && sang.AutoExpanded && !sang.ManualExpanded
                && sang2.Key == "us.sang2" && sang2.ManualExpanded,
            "the query's auto answer and the player's manual answer stay SEPARATE flags even where both "
            + "open the same card - the manual set is exactly what the player opened");
        // Clearing the keyword: the auto card closes, the manual card STAYS open - the restore is the
        // manual set, not "everything closed".
        host.Bindings.Set("search-text", "");
        ArrangeAndDraw(host);
        PackCardView afterSang = fake.BuildView().PackCards[0];
        PackCardView afterSang2 = fake.BuildView().PackCards[1];
        Assert(!afterSang.Expanded, "the auto-expansion left with the query");
        Assert(afterSang2.ManualExpanded && afterSang2.Expanded,
            "the player's own expansion survived the clear untouched");

        // And the result layer collapses exactly the manual set (the auto half is the query's).
        host.Bindings.Set("search-text", "sanguophage");
        ArrangeAndDraw(host);
        Assert(host.Bindings.TryInvokeCommand("cancel-pack-results"),
            "with a manual card open the result layer answers");
        ArrangeAndDraw(host);
        Assert(fake.ViewState.PackCardsExpanded.Count == 0,
            "the collapse cleared exactly the manual set");
        PackCardView stillAuto = fake.BuildView().PackCards[0];
        PackCardView manualNowAuto = fake.BuildView().PackCards[1];
        Assert(stillAuto.AutoExpanded && stillAuto.Expanded
                && manualNowAuto.AutoExpanded && !manualNowAuto.ManualExpanded,
            "the query's auto-expansion is NOT something the cancel silently erased - it is the query's, "
            + "and it leaves when the query does; the player's flag left with the player's layer");
        host.Bindings.Set("search-text", "");
        ArrangeAndDraw(host);
    }

    // ---------------------------------------------------------------- real clicks (step 6)
    private static void RealClicksOpenAndSelect()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Packs");
        Rect viewport = new(0f, 0f, PageWidth, PageHeight);
        ArrangeAndDraw(host);

        // (a) The header's Contents button opens the card. The declarative button is pressed the way the
        // retired DeclarativePacks lane pressed its declarative rows: the ButtonOverride seam is armed to
        // answer true for exactly the node whose ElementId names the us.sang header, then one real draw
        // pass runs. The engine's own Button(rect, ctx) then records the interaction subject
        // (NoteInteractionTarget) and the item action fires - no coordinate guess, no synthetic SetCancelTarget.
        int fired = Program.PressDeclarativeButton(host, viewport, "pack-card-row-expand#us.sang");
        Assert(fired == 1,
            "exactly one Contents button for us.sang must answer the press, got " + fired);
        Assert(fake.ViewState.PackCardsExpanded.Contains("us.sang"),
            "the press on the Contents button opened the card through the item action");
        UiNode? subject = host.Session.LastInteractionNode;
        Assert(subject != null && string.Equals(subject.ElementId, "pack-card-row-expand#us.sang", StringComparison.Ordinal),
            "the press recorded its subject on the button node, got '" + (subject?.ElementId ?? "null") + "'");

        // (b) A press on the domain row's hit selects that domain (P2's channel split: the hit selects,
        // the switch enables - and this press wrote no enable).
        fake.LastPackKey = null;
        fired = Program.PressDeclarativeButton(host, viewport, "pack-card-row-hit#us.sang|human|sanguophage");
        Assert(fired == 1, "exactly one xenotype-row hit must answer, got " + fired);
        Assert(fake.LastSelectedScope == SqueakVoicePackScope.Xenotype
                && fake.LastSelectedRace == "human" && fake.LastSelectedTarget == "sanguophage",
            "the press on the domain row selected that (race, xenotype) domain through the payload");
        Assert(fake.LastPackKey == null,
            "selecting a domain wrote no enable value (the channels stay separate)");
        subject = host.Session.LastInteractionNode;
        Assert(subject != null
                && string.Equals(subject.ElementId, "pack-card-row-hit#us.sang|human|sanguophage", StringComparison.Ordinal),
            "the press recorded the row's own hit node as the subject (the domain CancelBind lives on "
            + "the page-level Repeat above it - the walk climbs there, asserted by the cancel lane)");
    }

    // ---------------------------------------------------------------- empties (step 7)

    private static void EmptyResultVersusEmptyCatalog()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Packs");
        ArrangeAndDraw(host);

        host.Bindings.Set("search-text", "nothing-can-match-this");
        ArrangeAndDraw(host);
        Assert(host.Bindings.TryGet("pack-results-empty", out bool narrowed) && narrowed,
            "an over-narrowed filter says EMPTY-RESULT");
        Assert(host.Bindings.TryGet("pack-no-packs", out bool noneNow) && !noneNow,
            "and does NOT claim the catalog is empty");
        UiLayoutSnapshot narrowedSnap = host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Assert(narrowedSnap.RectById.ContainsKey("packs-empty"),
            "the empty-result sentence is the arranged one");

        var barren = new RecordingSettingsSource { RichData = false };
        using UiHost emptyHost = UsKernelSettingsHost.Create(barren, new Program.StubMetrics());
        emptyHost.Bindings.Invoke("set-tab", "Packs");
        emptyHost.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Assert(emptyHost.Bindings.TryGet("pack-no-packs", out bool none) && none,
            "a catalog with no packs says the OTHER sentence");
        Assert(emptyHost.Bindings.TryGet("pack-results-empty", out bool narrowedEmpty) && !narrowedEmpty,
            "and it is not reported as an over-narrowed filter");
    }

    // ---------------------------------------------------------------- minimum box (step 8)

    private static void MinimumBoxBothLanguages()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                var fake = new RecordingSettingsSource { RichData = true };
                using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
                fake.RevisionSource = () => host.Session.ContentRevision;
                host.Bindings.Invoke("set-tab", "Packs");
                host.Bindings.Set("search-text", "sang");
                ArrangeAndDraw(host);   // materialises the headers before the manual toggle is invoked
                host.Bindings.Invoke(UsWriteBindings.ItemKey("pack-card-keys", "us.sang", "toggle-pack-card"), "");
                host.Bindings.Set(UsWriteBindings.ItemKey("pack-card-keys", "us.sang|human", "enabled"), true);

                var reports = new List<UiOverflowReport>();
                var metrics = new Program.StubMetrics();
                UiFitAudit.Attach(metrics, reports.Add);
                UiFitAudit.Enabled = true;
                try
                {
                    UiLayoutSnapshot snap = host.MeasureAndArrange(
                        new Vector2(MinimumPageBoxWidth, MinimumPageBoxHeight));
                    Program.DrawWithPointer(host,
                        new Rect(0f, 0f, MinimumPageBoxWidth, MinimumPageBoxHeight),
                        new Vector2(MinimumPageBoxWidth / 2f, 140f));

                    Assert(snap.RectById.TryGetValue("packs-filter", out Rect filterBox) && filterBox.height > 40f,
                        language + ": the filter region is arranged at the minimum box");
                    Assert(snap.RectById.TryGetValue("packs-results", out Rect resultsBox) && resultsBox.height > 40f,
                        language + ": the card result is arranged at the minimum box");
                    Assert(snap.RectById.ContainsKey("pack-card-row#us.sang")
                            && snap.RectById.ContainsKey("pack-card-row#us.sang|human|sanguophage")
                            && snap.RectById.ContainsKey("pack-card-row#us.sang|human"),
                        language + ": the open card and both of its rows are arranged");
                    Assert(snap.RectById.TryGetValue("pack-card-row-check#us.sang|human", out Rect checkRect)
                            && checkRect.width >= 30f,
                        language + ": the row's switch keeps its 36-wide band (the knob's throw)");
                    foreach (KeyValuePair<string, Rect> entry in snap.RectById)
                    {
                        if (entry.Key.StartsWith("pack-card-row-title#", StringComparison.Ordinal)
                            || entry.Key.StartsWith("pack-card-row-domain-text#", StringComparison.Ordinal))
                        {
                            Assert(entry.Value.width > 20f,
                                language + ": a card text element collapsed to " + entry.Value.width
                                + " at " + entry.Key);
                        }
                    }
                }
                finally
                {
                    UiFitAudit.Detach();
                }

                Assert(reports.Count == 0,
                    language + ": the card page overflowed at the minimum box: "
                    + string.Join("; ", reports.ConvertAll(r => r.ElementPath + " " + r.Axis + " need="
                        + r.Needed + " have=" + r.Available)));
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    // ---------------------------------------------------------------- helpers

    private static void ArrangeAndDraw(UiHost host)
    {
        host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Program.DrawWithPointer(host, new Rect(0f, 0f, PageWidth, PageHeight), new Vector2(PageWidth / 2f, 140f));
    }

}
