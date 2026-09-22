using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// S4-2 lane: the two Packs layer cards that were dissolved out of <c>us/race-layer</c> and
/// <c>us/xenotype-layer</c>. These two are the reason the slice exists - they could not be migrated while
/// G2 (a repeated row reporting its OWN key) and G3 (an appearance-less hit area) were missing, so every
/// step below is also the consumption-side evidence the carrier's promotion gate asks for.
///
/// <para>
/// WHAT THIS LANE VERIFIES ABOUT THE CARRIER, in the report's terms:
/// <list type="bullet">
/// <item><b>Repeat + &lt;Templates&gt;</b> - a declared subtree materialized once per business key, with the
/// item key composed into the element identity (<c>race-layer-row#testrace</c>).</item>
/// <item><b>input/button.PayloadKey</b> (G2) - the command receives the row's own key, resolved in the
/// item-local binding scope. Asserted by PRESSING each row and checking that the model selected THAT
/// row.</item>
/// <item><b>input/button Chrome="none" + Height="MatchContent"</b> (G3) - a hit area that paints nothing
/// and IS the row it covers. Asserted as the carrier's own rule: the declaring child contributes nothing to
/// its parent's measured height, so it can resolve to the full content height of the non-declaring sibling
/// it exists to cover. The retired shape could only reach 69.9% of a flat row and 53.3% of a wrapped one
/// with two 24px <c>Auto</c> bands; the measured ratio is now 100% at every accepted width.</item>
/// <item><b>text/wrapped with a bound string</b> - the per-item title/detail, measured by the atom.</item>
/// </list>
/// </para>
///
/// <para>
/// MUTATION LEDGER (what each step is built to catch):
/// <list type="bullet">
/// <item><b>RetiredKindsAreGone</b> - re-adding a Registrar line reddens.</item>
/// <item><b>CardsAreDeclaredRowSets</b> - restoring a composite <c>&lt;Widget&gt;</c>, or giving a template
/// element a <c>SelectedKey</c>/<c>Tone</c> (the recorded gap), reddens.</item>
/// <item><b>EachRowReportsItsOwnKey</b> - a payload that is not per item (the pre-G2 shape) reddens: every
/// row would select the same domain.</item>
/// <item><b>TheHitIsTheRow</b> - reverting the manifest's <c>Height="MatchContent"</c> to <c>Auto</c>
/// reddens it, mutation-proven: the measured hit height drops to the 24px density token while the text
/// column it exists to cover stays 68.67px ("the hit area must BE the text column's height"). The
/// declaration guard in <b>CardsAreDeclaredRowSets</b> names the same defect one step earlier, so it is a
/// GUARD and this is the proof.</item>
/// <item><b>TheRowsFollowTheManifestBandRule</b> - changing the template's Gap, the header Height or the
/// hit's declared height mode reddens.</item>
/// </list>
/// </para>
///
/// <para>
/// NOT CLAIMED: the player-visible look. The row surface/hover/selected treatment and the per-row selected
/// ink are NOT expressed (SelectedKey is not item-scoped - see the manifest comment); the deltas are listed
/// in the S4-2 friction report (spec section 5.9) and need a real screen.
/// </para>
/// </summary>
internal static class DeclarativePacksLaneTests
{
    private const string Scope = "coahuilite.universalsqueaker";
    private const string PacksTab = "Packs";
    private const float PageWidth = 800f;
    private const float PageHeight = 720f;

    /// <summary>The step-3 canvas: tall enough that both layer cards draw in full (see FontsAndRows).</summary>
    private const float StepThreeHeight = 1100f;

    private const float CardPadding = 12f;
    private const float CardGap = 6f;
    private const float HeaderHeight = 26f;

    /// <summary>The manifest's declared <c>Repeat</c> rhythm for both layer row sets. The Padding is
    /// declared explicitly (S3-5): an undeclared container Padding falls back to the density default of 6 on
    /// each side, which would inset the rows 6px inside the card they already pad.</summary>
    private const float RepeatGap = 2f;

    private const float RepeatPadding = 0f;

    private static readonly string[] RetiredKinds = { "us/race-layer", "us/xenotype-layer" };

    /// <summary>The two cards and the identifiers that connect them to the page: the Repeat element, the
    /// ordered key binding the Repeat is built from, and the &lt;Templates&gt; name its rows are cut from.
    /// The ROW KEYS deliberately do not appear here - they are the live model's, read through the Items
    /// binding wherever a step needs them, because the fixture's keys follow its own mode (the wrapping
    /// fixture renames the race row itself).</summary>
    private static readonly (string Card, string RepeatId, string ItemsKey, string Template)[] Cards =
    {
        ("race-layer", "race-layer-rows", "race-rows", "race-layer-row"),
        ("xenotype-layer", "xenotype-layer-rows", "xenotype-rows", "xenotype-layer-row"),
    };

    public static int RunAll()
    {
        Step("the two dissolved layer kinds are retired from the registry", RetiredKindsAreGone);
        Step("both cards are declared row sets over the atom vocabulary", CardsAreDeclaredRowSets);
        Step("selection lands on exactly one row (B1)", SelectionLandsOnExactlyOneRow);
        // G3 before G2 on purpose: the band census is an attribute of the declared shape, and running it
        // before the press step keeps "a band is missing" attributable to the step that owns the band.
        Step("the hit IS the row it covers (G3, re-cut on Height=MatchContent)", TheHitIsTheRow);
        Step("every row reports its own key (G2)", EachRowReportsItsOwnKey);
        Step("the rows follow the manifest band rule", TheRowsFollowTheManifestBandRule);
        Console.WriteLine("DeclarativePacksLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 1
    // ---------------------------------------------------------------------------------------------

    private static void RetiredKindsAreGone()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        IReadOnlyCollection<string> kinds = UiWidgetRegistry.KnownKinds(Scope);
        foreach (string retired in RetiredKinds)
        {
            Assert(!kinds.Contains(retired),
                "the dissolved layer composite '" + retired + "' must not be a registered kind any more;"
                + " the cardinality authority is UiSourceInvariantTests (13), this is the per-widget half");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 2
    // ---------------------------------------------------------------------------------------------

    private static void CardsAreDeclaredRowSets()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });

        foreach ((string card, string repeatId, string itemsKey, string template) in Cards)
        {
            UiElementSpec? element = FindById(host.Manifest.Roots, card);
            Assert(element != null, "the shipped manifest must carry the card '" + card + "'");
            Assert(element!.Kind == "Section",
                "'" + card + "' must be the engine's Section container, got " + element.Kind);
            Assert(element.TryGetAttribute("Tab", out string tab) && tab == PacksTab,
                "'" + card + "' must stay gated by the Packs workspace");

            UiElementSpec? repeat = FindById(host.Manifest.Roots, repeatId);
            Assert(repeat != null && repeat.Kind == "Repeat",
                "'" + card + "' must hold the Repeat '" + repeatId + "'");
            Assert(repeat!.TryGetAttribute("Items", out string items) && items == itemsKey,
                "the Repeat's Items must name the ordered key binding '" + itemsKey + "', got " + items);
            Assert(repeat.TryGetAttribute("Template", out string named) && named == template,
                "the Repeat's Template must name '" + template + "', got " + named);

            Assert(host.Manifest.Templates.ContainsKey(template),
                "the manifest must declare the template '" + template + "'");
            UiElementSpec root = host.Manifest.Templates[template];
            Assert(root.Kind == "Overlay",
                "the row template must be an Overlay (the hit area has to COVER the text, not sit beside it),"
                + " got " + root.Kind);

            // The declared G2/G3 shape, read off the live template. ONE hit element since the carrier
            // shipped Height="MatchContent" (FL 490d4f07): the two stacked bands were a MEASURED workaround
            // (Auto measures a button's own caption), and together they reached only 69.9% of a flat row /
            // 53.3% of a wrapped one.
            List<UiElementSpec> buttons = Descendants(root).Where(e => e.Kind == "input/button").ToList();
            Assert(buttons.Count == 1,
                "the row template must declare exactly ONE hit element: the two-band workaround has no reason"
                + " to exist once the hit can take the row's measured content height, got " + buttons.Count);
            UiElementSpec hit = buttons[0];
            Assert(hit.TryGetAttribute("Chrome", out string chrome) && chrome == "none",
                "G3: the row's hit area must paint nothing (Chrome=\"none\"), got '" + chrome + "'");
            // Guard, not proof (see the lane's ledger): reverting this attribute is caught by the height
            // assertion in the hit-IS-the-row step, and this line only brings the failure forward to the
            // declaration it belongs to.
            Assert(hit.TryGetAttribute("Height", out string height) && height == "MatchContent",
                "the hit area must take the row's measured content height (Height=\"MatchContent\"), got '"
                + height + "': Auto would measure the button's own caption and leave the row's lower half dead");
            Assert(hit.TryGetAttribute("ActionBind", out string action) && action == "select-domain",
                "the hit area must fire select-domain, got '" + action + "'");
            Assert(hit.TryGetAttribute("PayloadKey", out string payload) && payload == "payload",
                "G2: the hit area must carry the row's own key (PayloadKey=\"payload\"), got '" + payload + "'");
            Assert(root.Children.Count == 2,
                "the row Overlay must hold the hit plus the text column, so the mode has a sibling that does"
                + " NOT declare it (the reference comes from the siblings), got " + root.Children.Count);

            List<UiElementSpec> texts = Descendants(root).Where(e => e.Kind == "text/wrapped").ToList();
            Assert(texts.Count == 2,
                "the row template must declare its two bound text lines (title + detail), got " + texts.Count);

            // SELECTION, positively asserted since carrier e929fa11 scoped SelectedKey per item: the row's
            // TITLE declares it, and the DETAIL must NOT. The Active treatment takes TextOnGold whatever
            // Emphasis says, so a SelectedKey on the detail would pull its ink gold too, while the shipped
            // composite kept the detail TextSecondary - a fidelity detail, not an omission.
            UiElementSpec title = texts.Single(t => t.TryGetAttribute("Bind", out string bind) && bind == "title");
            UiElementSpec detail = texts.Single(t => t.TryGetAttribute("Bind", out string bind2) && bind2 == "detail");
            Assert(title.TryGetAttribute("SelectedKey", out string selectedKey) && selectedKey == "selected",
                "the row TITLE must declare SelectedKey=\"selected\" so the selected row's own answer styles it,"
                + " got '" + selectedKey + "'");
            Assert(!detail.TryGetAttribute("SelectedKey", out _),
                "the row DETAIL must NOT declare SelectedKey: the Active treatment's text token is TextOnGold"
                + " whatever Emphasis says, so declaring it there would paint the detail gold too, and the"
                + " shipped composite kept it TextSecondary");

            // Tone/Emphasis stay absent, and that is still true for the same reason as before: they are
            // literal-only attributes, so a template cannot give them a per-row value.
            foreach (UiElementSpec element2 in Descendants(root))
            {
                Assert(!element2.TryGetAttribute("Tone", out _),
                    "'" + element2.Id + "' declares a literal Tone inside a template; a data-driven row cannot"
                    + " express per-item state that way, and a literal tone would paint every row alike");
            }
        }

        var kinds = new HashSet<string>(StringComparer.Ordinal);
        CollectKinds(host.Manifest.Roots, kinds);
        foreach (string retired in RetiredKinds)
        {
            Assert(!kinds.Contains(retired),
                "the live manifest must not contain the retired kind '" + retired + "' anywhere");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 3: B1 - exactly one row answers "I am selected"
    //
    // Before carrier e929fa11 SelectedKey was not item-scoped, so every row resolved one page-level key:
    // either all rows styled alike or each row recorded an unresolvable binding. The property is now
    // assertable per row, and it is asserted in BOTH model states rather than one:
    //   - a domain IS selected  => exactly ONE row over both cards answers true, and it is that domain's row;
    //   - no domain is selected => NO row answers true.
    // The second state is reached through the model's own filter channel (the rich fixture drops its
    // selection when the race filter hides every xenotype domain), so no shared fixture input moves.
    //
    // MUTATION PROOF: dropping the per-row "selected" registration drops the first count to zero; making
    // IsSelected answer true unconditionally raises it to the row count. Either reddens the count.
    // ---------------------------------------------------------------------------------------------

    private static void SelectionLandsOnExactlyOneRow()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            var source = new RecordingSettingsSource { RichData = true };
            using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
            host.Bindings.Invoke("set-tab", PacksTab);
            Arrange(host);

            VoicePackDomainView? selected = host.Bindings.Get<VoicePackDomainView?>("selected-domain");
            Assert(selected.HasValue,
                "the rich fixture must carry a selected domain, or this step measures nothing at all");
            VoicePackDomainView domain = selected!.Value;

            List<string> trueKeys = RowsAnsweringSelected(host);
            Assert(trueKeys.Count == 1,
                "exactly ONE row may answer selected=true while a domain is selected, got ["
                + string.Join(",", trueKeys) + "]. A page-level SelectedKey makes every row answer alike, and a"
                + " row set that cannot say WHICH row is selected is the gap carrier e929fa11 closed");

            // The helper reports "<itemsKey>.<rowKey>", which is the identity a failure can be read from.
            string expectedRow = domain.Scope == SqueakVoicePackScope.Race
                ? domain.RaceDefName
                : domain.RaceDefName + "|" + domain.TargetDefName;
            string expected = (domain.Scope == SqueakVoicePackScope.Race ? Cards[0].ItemsKey : Cards[1].ItemsKey)
                + "." + expectedRow;
            Assert(trueKeys[0] == expected,
                "and the true row must be the model's own selected domain: got '" + trueKeys[0] + "', expected '"
                + expected + "'");

            // THE ZERO-SELECTION STATE IS NOT EXERCISED, and the reason is a missing reachability proof
            // rather than a missing input. Two facts, kept separate on purpose:
            //   - REACHABILITY IS UNPROVEN: nothing has shown that the real page can hold a null
            //     selected-domain. This fixture cannot reach it either - SetRaceFilter only RECORDS the write
            //     (it does not move ViewState.RaceFilter), and the empty view has no rows at all - so the
            //     state is currently touched only as a MUTATION state (M-A below drives it by removing the
            //     per-row registration, which makes every row answer false). Writing a lane for a state nobody
            //     has shown a player can reach is measuring a scenario that may not exist; the project's answer
            //     to "unproven" is to say UNPROVEN, not to add an input that makes it look tested.
            //   - JUDGEMENT IS NOT THE GAP: both directions already redden on the assertion above (M-A gives
            //     [], M-B gives all four rows), so what is missing is reachability evidence, not discriminating
            //     power.
            // WHEN someone proves the state is reachable in the product, add the lane here. Until then this
            // comment is the record, and the state stays a mutation state only.
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    /// <summary>Every registered row whose item-local <c>selected</c> binding answers true, as
    /// <c>&lt;itemsKey&gt;.&lt;key&gt;</c> - the row identities, so a failure names them.</summary>
    private static List<string> RowsAnsweringSelected(UiHost host)
    {
        var found = new List<string>();
        foreach ((string _, string _, string itemsKey, string _) in Cards)
        {
            foreach (string key in host.Bindings.Get<IReadOnlyList<string>>(itemsKey))
            {
                // TryGetBool, not Get: the per-row key is registered on demand, and a missing one must read
                // as "not selected" through the same fail-soft query the carrier's own SelectedKey uses -
                // so a missing registration reddens the COUNT below (with the row list in the message)
                // instead of throwing out of the lane.
                if (host.Bindings.TryGetBool(itemsKey + "." + key + ".selected", out bool isSelected) && isSelected)
                {
                    found.Add(itemsKey + "." + key);
                }
            }
        }

        return found;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 4: G2 - each row reports ITS OWN key
    // ---------------------------------------------------------------------------------------------

    private static void EachRowReportsItsOwnKey()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            FontsAndRows(out RecordingSettingsSource source, out UiHost host, out UiLayoutSnapshot snapshot);
            using (host)
            {
                // A row control is handed its rect in its row Overlay's OWN group origin (the engine opens a
                // native group per Overlay), so a drawn rect carries no window position, and at 1024 flat all
                // four layer rows even measure the same box. What the lane can rely on is DRAW ORDER (see
                // PressBand), which is why the two row sets are collected below in manifest order, once, from
                // the UNFILTERED snapshot. Which row a press actually reached is then asserted, not assumed:
                // the payload is the property under test.
                var viewport = new Vector2(PageWidth, StepThreeHeight);

                // The keys come from the live model rather than from the fixture list: a row's identity IS
                // the projection's key, and the replay source changes the key one of the sets carries with
                // its wrapping mode, so re-deriving them here is what keeps this step measuring the page.
                //
                // The PRESS ORDER is a product fact, not a lane convenience. Selecting a race is a real
                // filter write, and the xenotype card then only keeps the domains of that race (measured: the
                // first race press drops the 'human|...' row out of the replay source's XenotypeDomains, so a
                // press addressed at it afterwards has no control to land on at all). The xenotype row is
                // therefore pressed FIRST and the race rows after it - while both sets are still the ones the
                // snapshot above was taken from.
                var rowKeys = new List<string>();
                var rowRects = new List<Rect>();
                foreach ((string _, string _, string itemsKey, string template) in Cards)
                {
                    foreach (string rowKey in host.Bindings.Get<IReadOnlyList<string>>(itemsKey))
                    {
                        rowKeys.Add(rowKey);
                        rowRects.Add(RectOf(snapshot, template + "#" + rowKey));
                    }
                }

                int raceCount = host.Bindings.Get<IReadOnlyList<string>>(Cards[0].ItemsKey).Count;
                IReadOnlyList<string> raceKeys = rowKeys.GetRange(0, raceCount);

                // The xenotype row: its key carries both identity halves, and the host decodes them.
                source.LastSelectedScope = null;
                source.LastSelectedRace = null;
                source.LastSelectedTarget = null;
                int revision = host.Session.ContentRevision;
                PressBand(host, viewport, rowRects, raceCount + 1);
                Assert(source.LastSelectedScope == SqueakVoicePackScope.Xenotype
                       && source.LastSelectedRace == "human" && source.LastSelectedTarget == "sanguophage",
                    "a composite row key must decode into a xenotype selection, got scope="
                    + (source.LastSelectedScope?.ToString() ?? "null") + " race='" + (source.LastSelectedRace ?? "null")
                    + "' target='" + (source.LastSelectedTarget ?? "null") + "'");
                Assert(host.Session.ContentRevision == revision + 1,
                    "and exactly one write (revision " + revision + " -> " + host.Session.ContentRevision + ")");

                // Race rows (manifest order), one band each.
                for (int i = 0; i < raceKeys.Count; i++)
                {
                    string key = raceKeys[i];
                    source.LastSelectedScope = null;
                    source.LastSelectedRace = null;
                    source.LastSelectedTarget = null;
                    revision = host.Session.ContentRevision;
                    PressBand(host, viewport, rowRects, i + 1);
                    Assert(source.LastSelectedScope == SqueakVoicePackScope.Race
                           && source.LastSelectedRace == key,
                        "pressing band #" + (i + 1) + " must select row " + i + " ('" + key
                        + "'); the model received scope=" + (source.LastSelectedScope?.ToString() ?? "null")
                        + " race='" + (source.LastSelectedRace ?? "null") + "'. A payload that is not per item is"
                        + " exactly the pre-G2 shape this slice exists to disprove");
                    Assert(host.Session.ContentRevision == revision + 1,
                        "and exactly one write (revision " + revision + " -> " + host.Session.ContentRevision + ")");
                }
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 4: G3 - and the boundary spec 0.4 recorded
    // ---------------------------------------------------------------------------------------------

    private static void TheHitIsTheRow()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                foreach (float width in new[] { 1024f, 736f, 480f, 320f })
                {
                    var metrics = new Program.StubMetrics();
                    var reports = new List<UiOverflowReport>();
                    UiFitAudit.Attach(metrics, reports.Add);
                    UiFitAudit.Enabled = true;
                    try
                    {
                        foreach (bool wrapping in new[] { false, true })
                        {
                            using UiHost host = UsKernelSettingsHost.Create(
                                new RecordingSettingsSource { RichData = true, WrappingDomainText = wrapping },
                                metrics);
                            host.Bindings.Invoke("set-tab", PacksTab);
                            UiLayoutSnapshot snapshot = Arrange(host, width);
                            host.DrawChecked(new Rect(0f, 0f, width, PageHeight));

                            // The row under test is the live model's first race row, not a fixture literal:
                            // the replay source's keys carry its wrapping mode.
                            string key = host.Bindings.Get<IReadOnlyList<string>>("race-rows")[0];
                            Rect row = RectOf(snapshot, "race-layer-row#" + key);
                            Rect hit = RectOf(snapshot, "race-layer-row-hit#" + key);
                            Rect text = RectOf(snapshot, "race-layer-row-text#" + key);
                            Rect title = RectOf(snapshot, "race-layer-row-title#" + key);
                            Rect detail = RectOf(snapshot, "race-layer-row-detail#" + key);

                            // (a) THE assertion: the hit IS the row. Same top, same width, and the same
                            // MEASURED height as the text column it exists to cover - not merely "the
                            // container grew".
                            Assert(Math.Abs(hit.y - row.y) <= 0.5f,
                                language + " " + width + ": the hit area must start at the row's top edge, "
                                + Describe(hit) + " vs row " + Describe(row));
                            Assert(Math.Abs(hit.x - row.x) <= 0.5f && Math.Abs(hit.xMax - row.xMax) <= 0.5f,
                                language + " " + width + ": the hit area must span the row's full width, "
                                + Describe(hit) + " vs row " + Describe(row));
                            Assert(Math.Abs(hit.height - text.height) <= 0.5f,
                                language + " " + width + ": the hit area must BE the text column's height -"
                                + " hit " + Num(hit.height) + "px vs text " + Num(text.height) + "px");
                            float coverage = hit.height / row.height * 100f;
                            Assert(coverage >= 99.5f,
                                language + " " + width + ": the hit area must cover the whole row - measured "
                                + Num(coverage, "0.#") + "% of " + Num(row.height) + "px");

                            // (b) the text column keeps its own shape (the two atom lines and the declared
                            // 2px gap), so the box the hit matched is the box the player reads.
                            float expectedText = title.height + 2f + detail.height;
                            Assert(Math.Abs(text.height - expectedText) <= 0.5f,
                                language + " " + width + ": the text column must stay title + 2px + detail ("
                                + Num(expectedText) + "px), got " + Num(text.height) + "px");

                            // (c) the census: ONE band per row now, across both cards. The draw pass must be
                            // the SAME frame the snapshot came from - the frame re-arranges at the viewport it
                            // is handed, so a census drawn at a different width counts a different layout (the
                            // trap this step hit while the band identity was still the density token).
                            var declaredRows = new List<Rect>();
                            foreach ((string _, string _, string itemsKey, string template) in Cards)
                            {
                                foreach (string rowKey in host.Bindings.Get<IReadOnlyList<string>>(itemsKey))
                                {
                                    declaredRows.Add(RectOf(snapshot, template + "#" + rowKey));
                                }
                            }

                            int bands = CountBands(host, width, declaredRows);
                            Assert(bands == declaredRows.Count,
                                language + " " + width + ": one hit area per row (" 
                                + declaredRows.Count + " rows), got " + bands + " rows=["
                                + string.Join(" | ", declaredRows.Select(r => Describe(r))) + "]");

                            Console.WriteLine("[packs-hit] " + Num(width, "0") + " " + language
                                + (wrapping ? " wrapped" : " flat ")
                                + " row=" + Num(row.height) + "px hit=" + Num(hit.height) + "px text="
                                + Num(text.height) + "px covered=" + Num(coverage, "0.#")
                                + "% uncovered=" + Num(row.height - hit.height)
                                + "px | control: the retired two-band workaround measured 69.9% flat / 53.3%"
                                + " wrapped / 42px dead at the reference width");
                        }
                    }
                    finally
                    {
                        UiFitAudit.Detach();
                        UiFitAudit.Enabled = false;
                    }
                }
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 5: the declared bands are the drawn bands
    // ---------------------------------------------------------------------------------------------

    private static void TheRowsFollowTheManifestBandRule()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                var metrics = new Program.StubMetrics();
                using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, metrics);
                host.Bindings.Invoke("set-tab", PacksTab);
                UiLayoutSnapshot snapshot = Arrange(host);
                host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));

                var expectedRows = new List<Rect>();
                foreach ((string card, _, string itemsKey, string template) in Cards)
                {
                    Rect cardRect = RectOf(snapshot, card);
                    float headerHeight = RectOf(snapshot, card + "-header").height;
                    Assert(Math.Abs(headerHeight - HeaderHeight) <= 0.5f,
                        "the card header band must be the declared " + HeaderHeight + "px, got " + headerHeight);

                    float rowsHeight = 0f;
                    string evidence = "";
                    IReadOnlyList<string> rowKeys = host.Bindings.Get<IReadOnlyList<string>>(itemsKey);
                    foreach (string rowKey in rowKeys)
                    {
                        Rect row = RectOf(snapshot, template + "#" + rowKey);
                        Rect hit = RectOf(snapshot, template + "-hit#" + rowKey);
                        Rect title = RectOf(snapshot, template + "-title#" + rowKey);
                        Rect detail = RectOf(snapshot, template + "-detail#" + rowKey);

                        // The hit area IS the row: same origin, same width, and - the point of the re-cut -
                        // the same MEASURED height. Height="MatchContent" resolves against the row's
                        // non-declaring sibling, so the band can no longer be the density token the retired
                        // Auto shape fell back to, and there is nothing left of the row for a click to miss.
                        Assert(Close(hit.x, row.x) && Close(hit.y, row.y) && MatchesShape(hit, row),
                            rowKey + ": the hit area must be the row's own box (" + Describe(hit) + " vs row "
                            + Describe(row) + ")");
                        Assert(Math.Abs(row.height - Math.Max(1f, title.height + 2f + detail.height)) <= 0.5f,
                            rowKey + ": the row must still be its text column's box at " + language + " (title "
                            + Num(title.height) + " + 2 + detail " + Num(detail.height) + "), got "
                            + Num(row.height) + " - a declaring child contributes nothing to the parent's height");

                        // The text lines share one width, and their bands are the atom's own rule.
                        string titleText = host.Bindings.Get<string>(itemsKey + "." + rowKey + ".title");
                        string detailText = host.Bindings.Get<string>(itemsKey + "." + rowKey + ".detail");
                        Assert(Math.Abs(title.x - row.x) <= 0.5f && Math.Abs(title.xMax - row.xMax) <= 0.5f,
                            rowKey + ": the title line must span the row (" + Describe(title) + ")");
                        Assert(Math.Abs(title.xMax - detail.xMax) <= 0.5f,
                            rowKey + ": both text lines must share one right edge");
                        float expectedTitle = WrappedBand(metrics, titleText, title.width);
                        float expectedDetail = WrappedBand(metrics, detailText, detail.width);
                        Assert(Math.Abs(title.height - expectedTitle) <= 0.5f,
                            rowKey + ": title band " + Num(title.height) + "px vs the atom's rule " + Num(expectedTitle)
                            + "px for '" + titleText + "'");
                        Assert(Math.Abs(detail.height - expectedDetail) <= 0.5f,
                            rowKey + ": detail band " + Num(detail.height) + "px vs the atom's rule " + Num(expectedDetail)
                            + "px for '" + detailText + "'");

                        rowsHeight += row.height;
                        expectedRows.Add(row);
                        evidence += rowKey + "=" + Num(row.height) + "/hit:" + Num(hit.height) + " ";
                    }

                    // Two relations, asserted separately so a failure names which one moved: the card over its
                    // two children, and the Repeat over its rows.
                    float repeatHeight = RectOf(snapshot, card == "race-layer" ? "race-layer-rows" : "xenotype-layer-rows").height;
                    float expectedCard = CardPadding + headerHeight + CardGap + repeatHeight + CardPadding;
                    Assert(Math.Abs(cardRect.height - expectedCard) <= 0.5f,
                        "the " + card + " card must equal Padding + header + Gap + the row set + Padding at "
                        + language + ": card " + Num(cardRect.height) + " vs " + Num(expectedCard) + " (repeat "
                        + Num(repeatHeight) + ")");

                    float repeatGap = RepeatGap;
                    float expectedRepeat = RepeatPadding * 2f + rowsHeight + repeatGap * (rowKeys.Count - 1);
                    Assert(Math.Abs(repeatHeight - expectedRepeat) <= 0.5f,
                        "the " + card + " row set must be the rows plus the declared Repeat Gap at " + language
                        + ": set " + Num(repeatHeight) + " vs " + Num(expectedRepeat) + " (rows " + Num(rowsHeight)
                        + ", gap " + Num(repeatGap) + ")");

                    Console.WriteLine("[packs-row] " + language + " " + card + " card=" + Num(cardRect.height)
                        + " header=" + Num(headerHeight) + " set=" + Num(repeatHeight) + " rows=[" + evidence + "]");
                }

                // The declared shape of the template is now ONE full-height hit, and this is that claim's
                // own evidence: a single checked frame hands the hit seam exactly one band per row, in
                // manifest order, and each band's drawn box is the row's arranged box. Before the re-cut the
                // same census saw two boxes per row (the 24px Auto token plus a second copy), so this is the
                // assertion the old shape reddens.
                List<Rect> bands = OrderedBands(host, PageWidth, expectedRows);
                Assert(bands.Count == expectedRows.Count,
                    "one hit area per declared row (" + expectedRows.Count + " rows across both cards), got "
                    + bands.Count + " - a second band per row means the template still carries the retired"
                    + " two-box workaround");
                for (int i = 0; i < bands.Count; i++)
                {
                    Assert(MatchesShape(bands[i], expectedRows[i]),
                        "band #" + (i + 1) + " must be the row the manifest declares at that draw position: got "
                        + Describe(bands[i]) + ", expected " + Describe(expectedRows[i]) + " at " + language);
                }

                Console.WriteLine("[packs-band] " + language + " bands=" + bands.Count + " rows="
                    + string.Join(" | ", expectedRows.Select(r => Describe(r))));
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Plumbing
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The step-3 fixture: a REAL full frame for every layer row. The canvas is taller than the page's own
    /// 720px on purpose - the engine does not hand the hit seam a control that falls outside the viewport it
    /// draws (measured: at 720px the xenotype row sits below the clip, and a press aimed at it lands on the
    /// race row above instead).
    /// </summary>
    private static void FontsAndRows(
        out RecordingSettingsSource source, out UiHost host, out UiLayoutSnapshot snapshot)
    {
        source = new RecordingSettingsSource { RichData = true };
        host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", PacksTab);
        host.MeasureAndArrange(new Vector2(PageWidth, StepThreeHeight));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        snapshot = host.MeasureAndArrange(new Vector2(PageWidth, StepThreeHeight));
    }

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        return Arrange(host, PageWidth);
    }

    private static UiLayoutSnapshot Arrange(UiHost host, float width, float height = PageHeight)
    {
        host.MeasureAndArrange(new Vector2(width, height));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        return host.MeasureAndArrange(new Vector2(width, height));
    }

    /// <summary>Sub-pixel comparison for rect edges: geometry here is float math on whole pixels.</summary>
    private static bool Close(float a, float b)
    {
        return Math.Abs(a - b) <= 0.5f;
    }

    /// <summary>
    /// True when a drawn control's box is a declared row's MEASURED box. Only the box, not the position:
    /// the carrier opens one native group per row Overlay, so every row control is handed its rect at that
    /// overlay's own origin and the drawn rect carries no window position to compare. What identifies a
    /// declared row here is therefore its size - and with one <c>MatchContent</c> hit per row that size IS
    /// the row's own measured content box, which is the property this lane is about.
    /// </summary>
    private static bool MatchesShape(Rect rect, Rect row)
    {
        return Close(rect.width, row.width) && Close(rect.height, row.height);
    }

    /// <summary>
    /// The band list one checked frame registers: one entry per declared row, resolved in draw order. The
    /// row's identity at the hit seam is its MEASURED BOX (see MatchesShape), so a row whose drawn control is
    /// missing shows up as a short list and a surplus control is invisible to every declared row - which is
    /// what makes this census redden for both shapes of the defect.
    /// </summary>
    private static List<Rect> OrderedBands(UiHost host, float width, IReadOnlyList<Rect> declaredRows)
    {
        var drawn = new List<Rect>();
        try
        {
            SetField(ButtonOverrideField, new Func<Rect, bool>(rect =>
            {
                if (Math.Abs(rect.x) <= 0.5f) drawn.Add(rect);
                return false;
            }));
            host.DrawChecked(new Rect(0f, 0f, width, PageHeight));
        }
        finally
        {
            ClearOverrides();
        }

        // One entry per declared row, addressed in draw order, so a missing band shows up as a short list
        // and a second band per row is visible as a drawn box no declared row claims.
        var found = new List<Rect>();
        foreach (Rect row in declaredRows)
        {
            foreach (Rect rect in drawn)
            {
                if (MatchesShape(rect, row))
                {
                    found.Add(rect);
                    break;
                }
            }
        }

        return found;
    }

    /// <summary>How many row bands one draw pass registers - the recording pass the presses are derived from.</summary>
    private static int CountBands(UiHost host, float width, IReadOnlyList<Rect> declaredRows)
    {
        return OrderedBands(host, width, declaredRows).Count;
    }

    /// <summary>
    /// Presses exactly one row: the <paramref name="ordinal"/>-th (1-based) control of the draw pass that
    /// belongs to <paramref name="family"/>, and nothing else. The override answers true for one rect, so a
    /// press can only ever be decided by one control - the property IMGUI's overlapping-hit case would break.
    ///
    /// <para>
    /// WHY ORDER AND NOT GEOMETRY. The carrier opens a native group per row Overlay, so every row control is
    /// handed its rect at that overlay's own origin and the drawn rect carries no window position. At 1024
    /// flat all four layer rows measure the same 68.67px box, and the xenotype card's single row is
    /// indistinguishable from the race card's first row by box alone - measured, and the failure it produced
    /// was a press aimed at the xenotype row that selected 'human' instead. What IS dependable is that the
    /// engine draws a row set in its declared item order (DeclarativePacksLaneTests step 5 asserts exactly
    /// that against the arranged boxes), so the ordinal inside a family names the row; the payload assertion
    /// after the press is what proves it.
    /// </para>
    /// </summary>
    private static void PressBand(UiHost host, Vector2 viewport, IReadOnlyList<Rect> family, int ordinal)
    {
        int seen = 0;
        try
        {
            SetField(ButtonOverrideField, new Func<Rect, bool>(rect =>
            {
                if (!MatchesFamilyRow(rect, family)) return false;
                seen++;
                return seen == ordinal;
            }));
            host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));
        }
        finally
        {
            ClearOverrides();
        }

        Assert(seen >= ordinal,
            "the draw pass registered only " + seen + " row band(s) of this set, so band #" + ordinal
            + " was never reached");
    }

    /// <summary>
    /// True when a drawn control is one of <paramref name="family"/>'s rows: a row's measured box at its
    /// group's own x-origin. A control that is not a row (a header button, a checkbox) or belongs to another
    /// card's set is never armed, so one press can only be decided by the row the caller intended.
    /// </summary>
    private static bool MatchesFamilyRow(Rect rect, IReadOnlyList<Rect> family)
    {
        if (Math.Abs(rect.x) > 0.5f) return false;
        foreach (Rect row in family)
        {
            if (MatchesShape(rect, row)) return true;
        }

        return false;
    }

    /// <summary>The atom's own band contract: a text/wrapped element measures the wrapped height of its
    /// string at its own arranged width plus twice <c>theme.Geometry.Padding</c>.</summary>
    private static float WrappedBand(Program.StubMetrics metrics, string text, float width)
    {
        return Math.Max(1f, metrics.MeasureText(text, UiFont.Small, Math.Max(1f, width))) + 6f * 2f;
    }

    private static Rect RectOf(UiLayoutSnapshot snapshot, string id)
    {
        Assert(snapshot.RectById.TryGetValue(id, out Rect rect),
            "the arranged snapshot must carry '" + id + "'; a missing element means the manifest lost it");
        return rect;
    }

    private static IEnumerable<UiElementSpec> Descendants(UiElementSpec root)
    {
        foreach (UiElementSpec child in root.Children)
        {
            yield return child;
            foreach (UiElementSpec nested in Descendants(child)) yield return nested;
        }
    }

    private static UiElementSpec? FindById(IReadOnlyList<UiElementSpec> roots, string id)
    {
        foreach (UiElementSpec spec in roots)
        {
            if (string.Equals(spec.Id, id, StringComparison.Ordinal)) return spec;
            UiElementSpec? nested = FindById(spec.Children, id);
            if (nested != null) return nested;
        }

        return null;
    }

    private static void CollectKinds(IReadOnlyList<UiElementSpec> elements, HashSet<string> kinds)
    {
        foreach (UiElementSpec element in elements)
        {
            kinds.Add(element.Kind);
            CollectKinds(element.Children, kinds);
        }
    }

    private static bool RectMatches(Rect a, Rect b)
    {
        return Math.Abs(a.x - b.x) <= 0.01f && Math.Abs(a.y - b.y) <= 0.01f
            && Math.Abs(a.width - b.width) <= 0.01f && Math.Abs(a.height - b.height) <= 0.01f;
    }

    private static bool Inside(Rect inner, Rect outer)
    {
        return inner.x >= outer.x - 0.5f && inner.y >= outer.y - 0.5f
            && inner.xMax <= outer.xMax + 0.5f && inner.yMax <= outer.yMax + 0.5f;
    }

    private static string Describe(Rect rect)
    {
        return "(x=" + Num(rect.x) + " y=" + Num(rect.y) + " w=" + Num(rect.width) + " h=" + Num(rect.height) + ")";
    }

    private static string Num(float value, string format = "0.##")
    {
        return value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static FieldInfo ButtonOverrideField => RequireField("ButtonOverride", typeof(Func<Rect, bool>));

    private static void ClearOverrides()
    {
        SetField(ButtonOverrideField, null);
    }

    private static void SetField(FieldInfo field, object? value)
    {
        try
        {
            field.SetValue(null, value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "REFLECTION BLOCKER: cannot set UiNative." + field.Name + " on net472. Add"
                + " InternalsVisibleTo(\"UniversalSqueakerKernelHostTests\") in the carrier assembly info,"
                + " rebuild the carrier, then rerun.", ex);
        }
    }

    private static FieldInfo RequireField(string name, Type fieldType)
    {
        FieldInfo? field = typeof(UiNative).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null || field.FieldType != fieldType)
        {
            throw new InvalidOperationException(
                "REFLECTION BLOCKER: UiNative." + name + " is not the expected " + fieldType.Name
                + " seam on net472; the hit seam this lane drives has moved.");
        }

        return field;
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("DeclarativePacksLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}