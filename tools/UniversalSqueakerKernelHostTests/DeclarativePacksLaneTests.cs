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
/// <item><b>input/button Chrome="none" + Height="Auto"</b> (G3) - a hit area that paints nothing. Asserted,
/// together with the boundary spec section 0.4 already recorded: Auto heights from the element's OWN
/// caption (empty =&gt; the density token), so the band cannot be stretched to the content-measured row it
/// covers.</item>
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
/// <item><b>TheHitBandIsTheDensityTokenNotTheRow</b> - dropping a band, or a caption that made Auto follow
/// the content, reddens.</item>
/// <item><b>TheRowsFollowTheManifestBandRule</b> - changing the template's Gap, the header Height or a
/// declared band reddens.</item>
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

    /// <summary>The density token the empty caption falls back to (manifest Styles/Density=regular).</summary>
    private const float DensityRowHeight = 24f;

    private const float CardPadding = 12f;
    private const float CardGap = 6f;
    private const float HeaderHeight = 26f;

    /// <summary>The manifest's declared <c>Repeat</c> rhythm for both layer row sets. The Padding is
    /// declared explicitly (S3-5): an undeclared container Padding falls back to the density default of 6 on
    /// each side, which would inset the rows 6px inside the card they already pad.</summary>
    private const float RepeatGap = 2f;

    private const float RepeatPadding = 0f;

    private static readonly string[] RetiredKinds = { "us/race-layer", "us/xenotype-layer" };

    /// <summary>The two cards, their Repeat element, their Items binding, their template and the row keys the
    /// rich fixture supplies (in projected order).</summary>
    private static readonly (string Card, string RepeatId, string ItemsKey, string Template, string[] Keys)[]
        Cards =
        {
            ("race-layer", "race-layer-rows", "race-rows", "race-layer-row",
                new[] { "human", "testrace", "sanguophage" }),
            ("xenotype-layer", "xenotype-layer-rows", "xenotype-rows", "xenotype-layer-row",
                new[] { "human|sanguophage" }),
        };

    public static int RunAll()
    {
        Step("the two dissolved layer kinds are retired from the registry", RetiredKindsAreGone);
        Step("both cards are declared row sets over the atom vocabulary", CardsAreDeclaredRowSets);
        Step("selection lands on exactly one row (B1)", SelectionLandsOnExactlyOneRow);
        // G3 before G2 on purpose: the band census is an attribute of the declared shape, and running it
        // before the press step keeps "a band is missing" attributable to the step that owns the band.
        Step("the hit band is the density token, not the row (G3 and its boundary)", TheHitBandIsTheDensityTokenNotTheRow);
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

        foreach ((string card, string repeatId, string itemsKey, string template, string[] _) in Cards)
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

            // The declared G2/G3 shape, read off the live template.
            List<UiElementSpec> buttons = Descendants(root).Where(e => e.Kind == "input/button").ToList();
            Assert(buttons.Count > 0, "the row template must declare its bare hit band(s)");
            foreach (UiElementSpec button in buttons)
            {
                Assert(button.TryGetAttribute("Chrome", out string chrome) && chrome == "none",
                    "G3: the row's hit band must paint nothing (Chrome=\"none\"), got '" + chrome + "'");
                Assert(button.TryGetAttribute("Height", out string height) && height == "Auto",
                    "G3: the hit band must be content-measured (Height=\"Auto\"), got '" + height + "'");
                Assert(button.TryGetAttribute("ActionBind", out string action) && action == "select-domain",
                    "the hit band must fire select-domain, got '" + action + "'");
                Assert(button.TryGetAttribute("PayloadKey", out string payload) && payload == "payload",
                    "G2: the hit band must carry the row's own key (PayloadKey=\"payload\"), got '" + payload + "'");
            }

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

            // The no-selection state is deliberately NOT exercised here, and the reason is recorded rather
            // than worked around: this fixture's SetRaceFilter only RECORDS the write (it does not move
            // ViewState.RaceFilter), so the rich view's selected domain cannot be cleared through the model's
            // own channel, and the empty view has no rows at all. Driving it would mean changing a SHARED
            // fixture's input, which this round requires asking about first. The property is still fully
            // asserted in the state that exists: exactly one of the four rows is true and the other three are
            // false, which is what "no second row may claim to be selected" means.
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
        foreach ((string _, string _, string itemsKey, string _, string[] keys) in Cards)
        {
            foreach (string key in keys)
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
                // The bands are recorded in the row Overlay's OWN group origin (the engine opens a native
                // group per Overlay), so every row's first band is (0, 0, W, H) and its second is (0, H, W, H).
                // Position therefore cannot identify a row inside the harness; what identifies it is the
                // PAYLOAD, which is exactly the property under test. The lane presses the n-th band in draw
                // order and asserts which domain the model received, so the mapping is proved rather than
                // assumed.
                float bandWidth = RectOf(snapshot, "race-layer-row-hit-a#" + Cards[0].Keys[0]).width;

                // Race rows first (manifest order), two bands each, then the xenotype row.
                for (int i = 0; i < Cards[0].Keys.Length; i++)
                {
                    int ordinal = i * 2 + 1;
                    source.LastSelectedScope = null;
                    source.LastSelectedRace = null;
                    source.LastSelectedTarget = null;
                    int revision = host.Session.ContentRevision;
                    PressBand(host, bandWidth, ordinal);
                    Assert(source.LastSelectedScope == SqueakVoicePackScope.Race
                           && source.LastSelectedRace == Cards[0].Keys[i],
                        "pressing band #" + ordinal + " must select row " + i + " ('" + Cards[0].Keys[i]
                        + "'); the model received scope=" + (source.LastSelectedScope?.ToString() ?? "null")
                        + " race='" + (source.LastSelectedRace ?? "null") + "'. A payload that is not per item is"
                        + " exactly the pre-G2 shape this slice exists to disprove");
                    Assert(host.Session.ContentRevision == revision + 1,
                        "and exactly one write (revision " + revision + " -> " + host.Session.ContentRevision + ")");
                }

                // The xenotype row: its key carries both identity halves, and the host decodes them.
                source.LastSelectedScope = null;
                source.LastSelectedRace = null;
                source.LastSelectedTarget = null;
                PressBand(host, bandWidth, Cards[0].Keys.Length * 2 + 1);
                Assert(source.LastSelectedScope == SqueakVoicePackScope.Xenotype
                       && source.LastSelectedRace == "human" && source.LastSelectedTarget == "sanguophage",
                    "a composite row key must decode into a xenotype selection, got scope="
                    + (source.LastSelectedScope?.ToString() ?? "null") + " race='" + (source.LastSelectedRace ?? "null")
                    + "' target='" + (source.LastSelectedTarget ?? "null") + "'");
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

    private static void TheHitBandIsTheDensityTokenNotTheRow()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            float plainHit = HitHeight(wrapping: false);
            float plainRow = RowHeight(wrapping: false);
            float wrappedHit = HitHeight(wrapping: true);
            float wrappedRow = RowHeight(wrapping: true);

            // The census lives here so that the band shape and the band COUNT are proven by the same
            // mutation: dropping a band has to redden this step rather than a later one.
            Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
            using (UiHost censusHost = UsKernelSettingsHost.Create(
                       new RecordingSettingsSource { RichData = true }, new Program.StubMetrics()))
            {
                censusHost.Bindings.Invoke("set-tab", PacksTab);
                UiLayoutSnapshot census = Arrange(censusHost);
                float bandWidth = RectOf(census, "race-layer-row-hit-a#" + Cards[0].Keys[0]).width;
                int bandCount = CountBands(censusHost, bandWidth);
                Assert(bandCount == (Cards[0].Keys.Length + Cards[1].Keys.Length) * 2,
                    "the two layer cards must draw two bare bands per row ("
                    + (Cards[0].Keys.Length + Cards[1].Keys.Length) + " rows), got " + bandCount);
            }

            // MUTATION PROOF for the band shape: the band is the density token the atom's empty caption falls
            // back to, so it is INDEPENDENT of the row it covers. Dropping a band changes the height; a caption
            // that made Auto follow content would change it too.
            Assert(Math.Abs(plainHit - DensityRowHeight * 2f) <= 0.5f,
                "the row's hit area must be the two declared bare bands, i.e. 2 x the density token ("
                + (DensityRowHeight * 2f) + "px), got " + Num(plainHit));
            Assert(Math.Abs(wrappedHit - plainHit) <= 0.5f,
                "the hit area must not follow the row's content height: plain " + Num(plainHit) + "px vs"
                + " wrapping " + Num(wrappedHit) + "px");

            // THE BOUNDARY, measured rather than assumed (spec 0.4 G3): a wrapped row outgrows its band, and
            // the uncovered remainder is named in px so the friction report can cite it.
            Assert(wrappedRow > plainRow + 1f,
                "the wrapping fixture must actually grow the row, or this step measures nothing: plain "
                + Num(plainRow) + "px, wrapping " + Num(wrappedRow) + "px");
            Assert(wrappedRow > wrappedHit,
                "the wrapped row (" + Num(wrappedRow) + "px) must exceed its hit area (" + Num(wrappedHit)
                + "px) - that gap IS the G3 boundary this slice records");

            Console.WriteLine("[packs-hit] flat row=" + Num(plainRow) + "px hit=" + Num(plainHit) + "px covered="
                + Num(plainHit / plainRow * 100f, "0.#") + "% | wrapped row=" + Num(wrappedRow) + "px hit="
                + Num(wrappedHit) + "px covered=" + Num(wrappedHit / wrappedRow * 100f, "0.#")
                + "% uncovered=" + Num(wrappedRow - wrappedHit) + "px");
        }
        finally
        {
            Program.SetTranslatorResolver(null);
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

                foreach ((string card, _, string itemsKey, string template, string[] keys) in Cards)
                {
                    Rect cardRect = RectOf(snapshot, card);
                    float headerHeight = RectOf(snapshot, card + "-header").height;
                    Assert(Math.Abs(headerHeight - HeaderHeight) <= 0.5f,
                        "the card header band must be the declared " + HeaderHeight + "px, got " + headerHeight);

                    float rowsHeight = 0f;
                    string evidence = "";
                    foreach (string key in keys)
                    {
                        Rect row = RectOf(snapshot, template + "#" + key);
                        Rect hitA = RectOf(snapshot, template + "-hit-a#" + key);
                        Rect hitB = RectOf(snapshot, template + "-hit-b#" + key);
                        Rect title = RectOf(snapshot, template + "-title#" + key);
                        Rect detail = RectOf(snapshot, template + "-detail#" + key);

                        // The two bands are one stack with no gap, starting at the row's own top edge: the
                        // whole band is the hit surface, which is what makes a click anywhere on the upper
                        // part of the row select it.
                        Assert(Math.Abs(hitB.y - hitA.yMax) <= 0.5f,
                            key + ": the two bare bands must be stacked without a gap (" + Describe(hitA)
                            + " then " + Describe(hitB) + ")");
                        Assert(Math.Abs(hitA.y - row.y) <= 0.5f,
                            key + ": the hit stack must start at the row's top edge (" + Describe(hitA)
                            + " vs row " + Describe(row) + ")");
                        Assert(Math.Abs(hitA.x - row.x) <= 0.5f && Math.Abs(hitA.xMax - row.xMax) <= 0.5f,
                            key + ": the hit band must span the row's full width (" + Describe(hitA) + " vs "
                            + Describe(row) + ")");

                        // The text lines share one width, and their bands are the atom's own rule.
                        string titleText = host.Bindings.Get<string>(itemsKey + "." + key + ".title");
                        string detailText = host.Bindings.Get<string>(itemsKey + "." + key + ".detail");
                        Assert(Math.Abs(title.x - row.x) <= 0.5f && Math.Abs(title.xMax - row.xMax) <= 0.5f,
                            key + ": the title line must span the row (" + Describe(title) + ")");
                        Assert(Math.Abs(title.xMax - detail.xMax) <= 0.5f,
                            key + ": both text lines must share one right edge");
                        float expectedTitle = WrappedBand(metrics, titleText, title.width);
                        float expectedDetail = WrappedBand(metrics, detailText, detail.width);
                        Assert(Math.Abs(title.height - expectedTitle) <= 0.5f,
                            key + ": title band " + Num(title.height) + "px vs the atom's rule " + Num(expectedTitle)
                            + "px for '" + titleText + "'");
                        Assert(Math.Abs(detail.height - expectedDetail) <= 0.5f,
                            key + ": detail band " + Num(detail.height) + "px vs the atom's rule " + Num(expectedDetail)
                            + "px for '" + detailText + "'");

                        float expectedRow = Math.Max(hitA.height + hitB.height, title.height + 2f + detail.height);
                        Assert(Math.Abs(row.height - expectedRow) <= 0.5f,
                            key + ": the row must be the taller of its two Overlay children at " + language
                            + " (bands " + Num(hitA.height + hitB.height) + " vs text "
                            + Num(title.height + 2f + detail.height) + "), got " + Num(row.height));

                        rowsHeight += row.height;
                        evidence += key + "=" + Num(row.height) + "/hit:" + Num(hitA.height + hitB.height) + " ";
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
                    float expectedRepeat = RepeatPadding * 2f + rowsHeight + repeatGap * (keys.Length - 1);
                    Assert(Math.Abs(repeatHeight - expectedRepeat) <= 0.5f,
                        "the " + card + " row set must be the rows plus the declared Repeat Gap at " + language
                        + ": set " + Num(repeatHeight) + " vs " + Num(expectedRepeat) + " (rows " + Num(rowsHeight)
                        + ", gap " + Num(repeatGap) + ")");

                    Console.WriteLine("[packs-row] " + language + " " + card + " card=" + Num(cardRect.height)
                        + " header=" + Num(headerHeight) + " set=" + Num(repeatHeight) + " rows=[" + evidence + "]");
                }
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

    private static void FontsAndRows(
        out RecordingSettingsSource source, out UiHost host, out UiLayoutSnapshot snapshot)
    {
        source = new RecordingSettingsSource { RichData = true };
        host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", PacksTab);
        snapshot = Arrange(host);
    }

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        return host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
    }

    /// <summary>
    /// True for a row's bare band as the draw hands it to the hit seam: every row Overlay opens its own native
    /// group, so a band is the group-local rect <c>(0, k * RowHeight, W, RowHeight)</c>.
    /// </summary>
    private static bool IsBand(Rect rect, float width)
    {
        return Math.Abs(rect.x) <= 0.5f
            && (Math.Abs(rect.y) <= 0.5f || Math.Abs(rect.y - DensityRowHeight) <= 0.5f)
            && Math.Abs(rect.height - DensityRowHeight) <= 0.5f
            && Math.Abs(rect.width - width) <= 0.5f;
    }

    /// <summary>How many row bands one draw pass registers - the recording pass the presses are derived from.</summary>
    private static int CountBands(UiHost host, float width)
    {
        int count = 0;
        try
        {
            SetField(ButtonOverrideField, new Func<Rect, bool>(rect =>
            {
                if (IsBand(rect, width)) count++;
                return false;
            }));
            host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
        }
        finally
        {
            ClearOverrides();
        }

        return count;
    }

    /// <summary>
    /// Presses exactly the <paramref name="ordinal"/>-th band of the draw pass (1-based, draw order), and
    /// nothing else: the override answers true only for that band, so one press can only be decided by one
    /// control - the property IMGUI's overlapping-hit case would break.
    /// </summary>
    private static void PressBand(UiHost host, float width, int ordinal)
    {
        int seen = 0;
        try
        {
            SetField(ButtonOverrideField, new Func<Rect, bool>(rect =>
            {
                if (!IsBand(rect, width)) return false;
                seen++;
                return seen == ordinal;
            }));
            host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
        }
        finally
        {
            ClearOverrides();
        }

        Assert(seen >= ordinal,
            "the draw pass registered only " + seen + " row bands, so band #" + ordinal + " was never reached");
    }

    private static float HitHeight(bool wrapping)
    {
        using UiHost host = UsKernelSettingsHost.Create(
            new RecordingSettingsSource { RichData = true, WrappingDomainText = wrapping }, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", PacksTab);
        UiLayoutSnapshot snapshot = Arrange(host);
        string key = wrapping ? "human" : "human";
        return RectOf(snapshot, "race-layer-row-hit-a#" + key).height
            + RectOf(snapshot, "race-layer-row-hit-b#" + key).height;
    }

    private static float RowHeight(bool wrapping)
    {
        using UiHost host = UsKernelSettingsHost.Create(
            new RecordingSettingsSource { RichData = true, WrappingDomainText = wrapping }, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", PacksTab);
        UiLayoutSnapshot snapshot = Arrange(host);
        return RectOf(snapshot, "race-layer-row#human").height;
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
