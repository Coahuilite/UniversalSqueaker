using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// S4-1 lane: the three Overview cards that were dissolved out of US-owned composite widgets
/// (<c>us/global-volume</c>, <c>us/basic-tuning</c>, <c>us/camera-indicator</c>) and rebuilt as manifest
/// subtrees over the library's own atoms. It carries one failure-sensitive step per dissolved widget, and
/// each step's own comment says which assertion is the mutation proof and which is only a guard.
///
/// <para>
/// MUTATION LEDGER (the mutation each step is built to catch, and what reddens):
/// <list type="bullet">
/// <item><b>RetiredKindsAreGone</b> - re-adding a Registrar line for any of the three kinds reddens the
/// kind-set pin. It is the "restore the old kind's participation" mutation for the whole batch.</item>
/// <item><b>CardsAreDeclaredSections</b> - putting a composite <c>&lt;Widget Kind="us/..."&gt;</c> element
/// back into a card reddens the declared-shape assertion on the LIVE manifest tree.</item>
/// <item><b>EachDeclaredControlIsTheOnlyWriteChannel</b> - rebinding a control to another value key, or
/// adding a second hit surface behind a checkbox, reddens the write count / the surface count.</item>
/// <item><b>DeclaredRowsFollowTheManifestBandRule</b> - changing a declared band in the manifest (the
/// checkbox Height, a Row gap, the card Padding) reddens the arithmetic the manifest declares.</item>
/// <item><b>TheEggStateBandDoesNotDependOnTheToggle</b> - stretching ONE of the two shipped state strings
/// reddens; that is what proves the lane really measures both atoms rather than one.</item>
/// </list>
/// </para>
///
/// <para>
/// NOT CLAIMED HERE: the player-visible look. The harness has no real glyphs, so the row hit band, the
/// hover/selected rail, the ink colours the manifest does not carry and the atom's own box geometry are
/// recorded as deltas in the S4-1 friction report and remain a real-screen item.
/// </para>
/// </summary>
internal static class DeclarativeOverviewLaneTests
{
    private const string Scope = "coahuilite.universalsqueaker";
    private const float PageWidth = 1024f;
    private const float PageHeight = 900f;
    private const float RowGap = 8f;
    private const float DeclaredCheckboxWidth = 24f;
    private const float DeclaredCheckboxHeight = 30f;
    private const float CardPadding = 12f;
    private const float CardGap = 6f;
    private const float HeaderHeight = 26f;

    private static readonly string[] RetiredKinds =
    {
        "us/global-volume", "us/basic-tuning", "us/camera-indicator"
    };

    /// <summary>The declared control rows of the basic-tuning card, in manifest order: the row element, its
    /// label element, its checkbox element and the label's Keyed name. The child row is last because it is
    /// the one row whose presence depends on another control.</summary>
    private static readonly (string Row, string Label, string Check, string LabelKey)[] BasicRows =
    {
        ("basic-egg-row", "basic-egg-label", "basic-egg-check", "US.Tuning.EasterEggs"),
        ("basic-cooldown-row", "basic-cooldown-label", "basic-cooldown-check", "US.Tuning.ScaleCooldown"),
        ("basic-talking-row", "basic-talking-label", "basic-talking-check", "US.Tuning.ScaleTalking"),
        ("basic-population-row", "basic-population-label", "basic-population-check", "US.Tuning.ScalePopulation"),
        ("basic-eat-row", "basic-eat-label", "basic-eat-check", "US.Tuning.EatPrecision"),
        ("basic-eat-child-row", "basic-eat-child-label", "basic-eat-child-check", "US.Tuning.EatPrecision.IncludeDrugs"),
    };

    private static readonly string[] BasicRules =
    {
        "basic-egg-rule", "basic-cooldown-rule", "basic-talking-rule", "basic-population-rule", "basic-eat-rule"
    };

    /// <summary>The seven declared value bindings the three cards own, and the business field each write
    /// must land in. The count is the point: the composites needed a value binding AND a toggle-* action per
    /// row (14 channels), and the declarative shape needs one.</summary>
    private static readonly string[] DeclaredValueBindings =
    {
        "allow-eggs", "scale-cooldown", "scale-talking", "scale-population",
        "eat-precision", "eat-precision-include-drugs", "camera-indicator"
    };

    public static int RunAll()
    {
        Step("the three dissolved composite kinds are retired from the registry", RetiredKindsAreGone);
        Step("every Overview card is a declared Section over atoms, not a composite widget", CardsAreDeclaredSections);
        // The value-independence invariant runs BEFORE the interaction step on purpose: a stretched state
        // sentence changes the egg row's own band, and the interaction step's "one 24 x 30 band per row"
        // filter is about the checkbox shape, not about that band. Measuring the invariant first is what
        // keeps each mutation attributable to the step that owns it.
        Step("the egg state band does not depend on the toggle (4 widths x EN/ZH)", TheEggStateBandDoesNotDependOnTheToggle);
        Step("the declared rows follow the manifest band rule (4 widths x EN/ZH)", DeclaredRowsFollowTheManifestBandRule);
        Step("each declared control is the row's only hit surface and writes exactly once", EachDeclaredControlIsTheOnlyWriteChannel);
        Console.WriteLine("DeclarativeOverviewLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 1: the retirement is real
    // ---------------------------------------------------------------------------------------------

    private static void RetiredKindsAreGone()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        IReadOnlyCollection<string> kinds = UiWidgetRegistry.KnownKinds(Scope);

        foreach (string retired in RetiredKinds)
        {
            Assert(!kinds.Contains(retired),
                "the dissolved composite '" + retired + "' must not be a registered kind any more: a"
                + " registration that outlives its manifest line is the half-retired state this batch exists"
                + " to prevent");
        }

        // The registry is not where the cardinality is pinned, and this lane deliberately does not
        // re-pin it: KnownKinds(scope) holds every kind registered under the US scope, which includes the
        // seven diagnostics-panel kinds UsDiagnosticsWidgets registers directly as well as the fifteen
        // UsKernelWidgetRegistrar names, so a number here would be a second, weaker copy of a fact that
        // already has an authority. The authority is UiSourceInvariantTests: the Registrar's registered
        // us/* kind SET must equal the us/* kind set the two Schema2 manifests use, and that count is
        // pinned at 15 (S4-1 shrank it from 18). The mutation ledger for this batch is therefore:
        //   - re-adding a Registrar line          -> UiSourceInvariantTests reddens (registrar != manifest)
        //   - restoring a composite manifest line  -> CardsAreDeclaredSections (step 2) reddens
        //   - the two together                     -> UiSourceInvariantTests' 15 pin reddens
        // The six core atoms the three cards are built from are NOT in this list on purpose: KnownKinds
        // reports the kinds registered under the given scope, and core atoms register under the core
        // scope. Resolution is what matters and it is already proven one step over - step 2 creates the
        // real Host from the real embedded manifest, and Host creation refuses an unresolvable kind.
    }

    // ---------------------------------------------------------------------------------------------
    // Step 2: the live manifest is declarative
    // ---------------------------------------------------------------------------------------------

    private static void CardsAreDeclaredSections()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });

        foreach (string id in new[] { "global-volume", "basic-tuning", "camera-indicator" })
        {
            UiElementSpec? card = FindById(host.Manifest.Roots, id);
            Assert(card != null, "the shipped manifest must carry the card '" + id + "'");
            Assert(card!.Kind == "Section",
                "'" + id + "' must be the engine's Section container - the composite kind that used to own"
                + " this card is retired - got " + card.Kind);
            Assert(card.TryGetAttribute("Tab", out string tab) && tab == "Overview",
                "'" + id + "' must stay gated by the Overview workspace");
        }

        var kinds = new HashSet<string>(StringComparer.Ordinal);
        CollectKinds(host.Manifest.Roots, kinds);
        foreach (string retired in RetiredKinds)
        {
            Assert(!kinds.Contains(retired),
                "the live manifest must not contain the retired kind '" + retired + "' anywhere");
        }

        // The controls are atoms now, matched by the value binding each one owns - the property that makes
        // them controls rather than decoration.
        UiElementSpec basic = FindById(host.Manifest.Roots, "basic-tuning")!;
        foreach (string bind in DeclaredValueBindings.Where(b => b != "camera-indicator"))
        {
            UiElementSpec? control = FindByAttribute(basic, "Bind", bind);
            Assert(control != null && control.Kind == "input/checkbox",
                "the basic-tuning card's '" + bind + "' control must be the input/checkbox atom, got "
                + (control == null ? "(missing)" : control.Kind));
        }

        UiElementSpec childRow = FindById(host.Manifest.Roots, "basic-eat-child-row")!;
        Assert(childRow != null
               && childRow.TryGetAttribute("VisibleKey", out string gate)
               && gate == "eat-precision",
            "the eat-precision child row must be gated by VisibleKey reading the parent's own bool binding"
            + " - the declarative form of the rule the composite enforced in C#");

        UiElementSpec volume = FindById(host.Manifest.Roots, "global-volume")!;
        UiElementSpec? slider = FindById(volume.Children, "global-volume-slider");
        Assert(slider?.Kind == "input/slider",
            "the global-volume card's value control is the input/slider atom");
        // The two controls are two UNITS over one value, so which binding each one owns is the contract, not
        // a detail: the slider carries the 0..1 value and the number field carries the 0..100 projection.
        // Swapping them keeps the page compiling and the card looking unchanged while the field silently
        // edits 0..1 points - which is why this is asserted here and not left to the drawing.
        Assert(slider!.TryGetAttribute("Bind", out string sliderBind) && sliderBind == "global-volume",
            "the slider atom must own the normalized 'global-volume' binding, got "
            + (slider.TryGetAttribute("Bind", out string actual) ? actual : "(none)"));
        UiElementSpec? number = FindById(volume.Children, "global-volume-number");
        Assert(number?.Kind == "input/number-field",
            "the global-volume card's points control is the input/number-field atom");
        Assert(number!.TryGetAttribute("Bind", out string numberBind) && numberBind == "global-volume-percent",
            "the number field must own the percent projection 'global-volume-percent', got "
            + (number.TryGetAttribute("Bind", out string actualNumber) ? actualNumber : "(none)"));
        Assert(number.TryGetAttribute("Max", out string numberMax)
               && Math.Abs(float.Parse(numberMax, System.Globalization.CultureInfo.InvariantCulture) - 100f) < 0.001f,
            "and that projection's window must be 0..100 points, got Max='" + numberMax + "'");
        Assert(FindByAttribute(volume, "Bind", "global-volume-caption")?.Kind == "text/wrapped",
            "the global-volume caption is a bound text/wrapped atom, not a hand-formatted label");

        UiElementSpec camera = FindById(host.Manifest.Roots, "camera-indicator")!;
        Assert(FindByAttribute(camera, "Bind", "camera-indicator")?.Kind == "input/checkbox",
            "the camera-indicator card's one control is the input/checkbox atom");
    }

    // ---------------------------------------------------------------------------------------------
    // Step 3: one hit surface, one write
    // ---------------------------------------------------------------------------------------------

    private static void EachDeclaredControlIsTheOnlyWriteChannel()
    {
        // A real language table, not the identity resolver: the caption this step asserts on is a formatted
        // Keyed string, and the identity resolver would hand back the key ("US.Tuning.GlobalVolume"), which
        // contains no percent at all - a lane that measured text with no table would pass for the wrong
        // reason or fail for one. Same rule the frame/geometry lanes already follow.
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            RunDeclaredWriteChannelChecks();
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    private static void RunDeclaredWriteChannelChecks()
    {
        var source = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
        using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Overview");
        UiLayoutSnapshot snapshot = Arrange(host);

        // One recording pass: every declared control takes its hit through UiNative.Button, the same seam
        // the composite rows used, so the recorded set IS the page's hit surface.
        var recorded = new List<Rect>();
        DrawWithButtons(host, rect => { recorded.Add(rect); return false; });

        // (a) the camera card. It has exactly one control, so its band is unambiguous.
        Rect cameraBand = BandIn(recorded, snapshot, "camera-indicator");
        Assert(CountOverlapping(recorded, cameraBand) == 1,
            "the camera card's declared checkbox must be the card's ONLY hit surface, found "
            + CountOverlapping(recorded, cameraBand));

        // A checkbox writes the INVERSE of the bool it read through its value binding (that inverse write
        // is the whole toggle - there is no ActionBind on the element), so the expected write is read from
        // the binding rather than assumed.
        bool cameraBefore = host.Bindings.Get<bool>("camera-indicator");
        source.LastCameraIndicator = null;
        int revision = host.Session.ContentRevision;
        DrawWithButtons(host, rect => RectMatches(rect, cameraBand));
        Assert(source.LastCameraIndicator == !cameraBefore,
            "one press on the declared camera checkbox must write the inverse of the value it read ("
            + !cameraBefore + "), got " + (source.LastCameraIndicator?.ToString() ?? "null"));
        Assert(host.Session.ContentRevision == revision + 1,
            "and it must advance the session clock exactly once (revision " + revision + " -> "
            + host.Session.ContentRevision + ")");

        // (b) every basic-tuning row: one press, one write, and no second surface behind it. The parent is
        // ON, so the child row exists and is pressed too.
        var basic = new List<Rect>();
        foreach (Rect band in recorded.Where(r => Inside(r, CardLocal(snapshot, "basic-tuning"))))
        {
            if (Math.Abs(band.width - DeclaredCheckboxWidth) <= 0.5f
                && Math.Abs(band.height - DeclaredCheckboxHeight) <= 0.5f) basic.Add(band);
        }

        basic = basic.OrderBy(r => r.y).ToList();
        Console.WriteLine("[declared-bands] card bands=" + basic.Count
            + " all=" + string.Join(" ", basic.Select(Describe)));
        Assert(basic.Count == 6,
            "the basic-tuning card must draw six declared checkbox bands with the parent ON, got "
            + basic.Count + " [" + string.Join(" ", basic.Select(Describe)) + "]");

        for (int index = 0; index < basic.Count; index++)
        {
            Assert(CountOverlapping(recorded, basic[index]) == 1,
                "declared row " + index + " must carry exactly ONE hit surface; band " + Describe(basic[index])
                + " intersects " + CountOverlapping(recorded, basic[index]) + " recorded surfaces. In IMGUI a"
                + " second band behind a checkbox turns one press into two writes");
        }

        // The egg row is the first band; that it really is the egg row is verified by what the press does.
        bool eggBefore = host.Bindings.Get<bool>("allow-eggs");
        source.LastEasterEggs = null;
        int eggRevision = host.Session.ContentRevision;
        DrawWithButtons(host, rect => RectMatches(rect, basic[0]));
        Assert(source.LastEasterEggs == !eggBefore,
            "pressing the egg row's band must write the inverse of the value it read through allow-eggs (the"
            + " atom's only channel), got " + (source.LastEasterEggs?.ToString() ?? "null"));
        Assert(host.Session.ContentRevision == eggRevision + 1,
            "and exactly once (revision " + eggRevision + " -> " + host.Session.ContentRevision + ")");

        // The last band is the eat-precision child's.
        source.LastEatPrecisionIncludeDrugs = null;
        source.LastEatPrecision = null;
        int childRevision = host.Session.ContentRevision;
        DrawWithButtons(host, rect => RectMatches(rect, basic[basic.Count - 1]));
        Assert(source.LastEatPrecisionIncludeDrugs == true,
            "pressing the child row's band must write the child value exactly once");
        Assert(source.LastEatPrecision == null, "and it must not touch the parent switch");
        Assert(host.Session.ContentRevision == childRevision + 1,
            "and exactly once (revision " + childRevision + " -> " + host.Session.ContentRevision + ")");

        // (c) the global-volume card: the slider atom's drag and the number-field atom's binding must land
        // in the SAME business field, or the two controls on one card would show different volumes.
        var sliders = new List<Rect>();
        DrawWithSliders(host, (rect, value, min, max) => { sliders.Add(rect); return value; });
        Rect? volumeSlider = sliders
            .Where(r => Inside(r, CardLocal(snapshot, "global-volume")))
            .OrderBy(r => r.y)
            .Select(r => (Rect?)r)
            .FirstOrDefault();
        Assert(volumeSlider.HasValue, "the global-volume card must draw its input/slider atom's track");

        source.LastGlobalVolume = null;
        DrawWithSliders(host, (rect, value, min, max) => RectMatches(rect, volumeSlider!.Value) ? 0.5f : value);
        Assert(source.LastGlobalVolume.HasValue && Math.Abs(source.LastGlobalVolume.Value - 0.5f) < 0.001f,
            "dragging the declared slider must write the normalized volume through the business setter, got "
            + (source.LastGlobalVolume?.ToString() ?? "null"));

        source.LastGlobalVolume = null;
        host.Bindings.Set("global-volume-percent", 42f);
        Assert(source.LastGlobalVolume.HasValue && Math.Abs(source.LastGlobalVolume.Value - 0.42f) < 0.001f,
            "the number field's percent binding must write the SAME normalized field as the slider, got "
            + (source.LastGlobalVolume?.ToString() ?? "null"));

        host.Bindings.Set("global-volume", 0.6f);
        Assert(host.Bindings.Get<float>("global-volume-percent") - 60f < 0.001f,
            "and reading the percent projection must follow the slider's value back");
        Assert(host.Bindings.Get<string>("global-volume-caption").Contains("60%"),
            "the caption is a projection of the same value, got '"
            + host.Bindings.Get<string>("global-volume-caption") + "'");

        // Guard (not a mutation proof): the seven declared value bindings are the whole write surface the
        // three cards own - the composites also needed seven toggle-* actions, and those are gone.
        foreach (string bind in DeclaredValueBindings)
        {
            Assert(host.Bindings.IsWritable(bind), "the declared value binding '" + bind + "' must be writable");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 4: the declared bands are the drawn bands
    // ---------------------------------------------------------------------------------------------

    private static void DeclaredRowsFollowTheManifestBandRule()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
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
                        var source = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
                        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
                        host.Bindings.Invoke("set-tab", "Overview");
                        UiLayoutSnapshot snapshot = Arrange(host, width);
                        host.DrawChecked(new Rect(0f, 0f, width, PageHeight));

                        foreach (string card in new[] { "global-volume", "basic-tuning", "camera-indicator" })
                        {
                            foreach (UiOverflowReport report in reports.Where(
                                         r => r.ElementPath.IndexOf(card, StringComparison.Ordinal) >= 0))
                            {
                                throw new InvalidOperationException(
                                    "the declarative card '" + card + "' overflows at " + width + " ("
                                    + language + "): " + report.ElementPath + " " + report.Axis + " needs "
                                    + report.Needed + " has " + report.Available);
                            }
                        }

                        Rect viewport = snapshot.Viewports["content-scroll"];
                        Assert(RectOf(snapshot, "basic-tuning").width <= viewport.width + 0.5f,
                            "the declarative card must not exceed the content viewport at " + width
                            + " (" + language + ")");

                        string evidence = "";
                        foreach ((string rowId, string labelId, string checkId, string labelKey) in BasicRows)
                        {
                            Rect row = RectOf(snapshot, rowId);
                            Rect label = RectOf(snapshot, labelId);
                            Rect check = RectOf(snapshot, checkId);

                            // The Row's own contract: the label starts at the row's left edge, the checkbox
                            // ends at its right edge, and the manifest's Gap is exactly what separates them.
                            Assert(Math.Abs(label.x - row.x) <= 0.5f,
                                rowId + " at " + width + " (" + language + "): the label must start at the"
                                + " row's left edge, label " + Describe(label) + " row " + Describe(row));
                            Assert(Math.Abs(check.xMax - row.xMax) <= 0.5f,
                                rowId + " at " + width + " (" + language + "): the declared checkbox must"
                                + " terminate on the row's right edge, check " + Describe(check) + " row "
                                + Describe(row));
                            Assert(Math.Abs((check.x - label.xMax) - RowGap) <= 0.5f,
                                rowId + " at " + width + " (" + language + "): the Row's declared Gap must be"
                                + " the space between the label and the checkbox, got "
                                + (check.x - label.xMax));
                            Assert(Math.Abs(check.width - DeclaredCheckboxWidth) <= 0.5f
                                   && Math.Abs(check.height - DeclaredCheckboxHeight) <= 0.5f,
                                rowId + " at " + width + " (" + language + "): the declared checkbox band is"
                                + " " + DeclaredCheckboxWidth + " x " + DeclaredCheckboxHeight + ", got "
                                + Describe(check));

                            // MUTATION PROOF: the row height is the engine's own rule over the declared
                            // bands - max of the children's measured bands - so changing the checkbox
                            // Height, the atom's padding or a declared band reddens here.
                            float expected = Math.Max(DeclaredCheckboxHeight,
                                WrappedBand(metrics, table[labelKey], label.width));

                            // The egg row stacks a second band under the title, so its column is taller.
                            if (rowId == "basic-egg-row")
                            {
                                string stateKey = RectOf(snapshot, "basic-egg-state-on") is var on && on.width > 0f
                                    ? "US.Tuning.EasterEggs.On"
                                    : "US.Tuning.EasterEggs.Off";
                                Rect state = RectOf(snapshot, stateKey == "US.Tuning.EasterEggs.On"
                                    ? "basic-egg-state-on"
                                    : "basic-egg-state-off");
                                expected = Math.Max(DeclaredCheckboxHeight,
                                    WrappedBand(metrics, table[labelKey], label.width) + 1f
                                    + WrappedBand(metrics, table[stateKey], state.width));
                            }

                            Assert(Math.Abs(row.height - expected) <= 0.5f,
                                rowId + " at " + width + " (" + language + ") was drawn " + row.height
                                + "px tall but the declared bands plus the engine's own wrap rule say "
                                + expected + "px [label '" + table[labelKey] + "' in " + label.width + "px]");

                            evidence += rowId + "=" + Num(row.height) + "/band:" + Num(expected)
                                + "/check:" + Num(check.width) + "x" + Num(check.height) + " ";
                        }

                        // The card chrome relation the checklist card already pins: Padding + the Section's
                        // two children + the one Section Gap + Padding accounts for the whole card.
                        float headerHeight = RectOf(snapshot, "basic-tuning-header").height;
                        float bodyHeight = RectOf(snapshot, "basic-tuning-body").height;
                        float cardHeight = RectOf(snapshot, "basic-tuning").height;
                        float expectedCard = CardPadding + headerHeight + CardGap + bodyHeight + CardPadding;
                        Assert(Math.Abs(cardHeight - expectedCard) <= 0.5f,
                            "the basic-tuning card must equal Padding + header + Section Gap + body + Padding at"
                            + " " + width + " (" + language + "): card " + cardHeight + " vs " + expectedCard
                            + " (header " + headerHeight + ", body " + bodyHeight + ")");

                        // MUTATION PROOF: the body is the declared stack - six rows, five declared rules, and
                        // one Column Gap between every neighbour - so changing a declared band, a rule or the
                        // body's own Gap reddens here instead of silently moving the card.
                        float rowSum = BasicRows.Sum(r => RectOf(snapshot, r.Row).height);
                        float ruleSum = BasicRules.Sum(id => RectOf(snapshot, id).height);
                        float expectedBody = rowSum + ruleSum + 2f * (BasicRows.Length + BasicRules.Length - 1);
                        Assert(Math.Abs(bodyHeight - expectedBody) <= 0.5f,
                            "the declared body must be rows + rules + the Column's own Gap at " + width + " ("
                            + language + "): body " + bodyHeight + " vs " + expectedBody + " (rows " + rowSum
                            + ", rules " + ruleSum + ")");

                        Console.WriteLine("[declared] " + width + " " + language + " card=" + Num(cardHeight)
                            + " header=" + Num(HeaderHeight) + " rows=[" + evidence + "]"
                            + " visualRight=" + Num(VisualRight(RectOf(snapshot, "basic-cooldown-check")))
                            + " contentRight=" + Num(RectOf(snapshot, "basic-tuning").xMax - CardPadding));
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
    // Step 5: the egg state band is value-independent
    //
    // The composite used to measure the On/Off band against the LONGER of the two shipped strings so the
    // row height could never depend on the current value. The declarative shape has no reserved-band
    // vocabulary, so the property has to be re-established by measurement: whichever of the two atoms the
    // VisibleKey gate leaves visible, the row must be the same height. This is the invariant that keeps the
    // card from resizing under the player's own toggle.
    // ---------------------------------------------------------------------------------------------

    private static void TheEggStateBandDoesNotDependOnTheToggle()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                foreach (float width in new[] { 1024f, 736f, 480f, 320f })
                {
                    float off = EggRowHeight(width, allowEggs: false);
                    float on = EggRowHeight(width, allowEggs: true);
                    Assert(Math.Abs(on - off) <= 0.01f,
                        "the egg row must not change height when the player flips its own switch at " + width
                        + " (" + language + "): On=" + Num(on) + " Off=" + Num(off) + ". The composite"
                        + " measured the band against the longer of the two strings for exactly this reason;"
                        + " the declarative shape has no reserved band, so the two atoms must measure equal");
                    Console.WriteLine("[egg-band] " + width + " " + language + " on=" + Num(on)
                        + " off=" + Num(off));
                }
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    private static float EggRowHeight(float width, bool allowEggs)
    {
        var source = new RecordingSettingsSource { RichData = true, AllowEasterEggs = allowEggs };
        using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Overview");
        UiLayoutSnapshot snapshot = Arrange(host, width);
        return RectOf(snapshot, "basic-egg-row").height;
    }

    // ---------------------------------------------------------------------------------------------
    // Plumbing
    // ---------------------------------------------------------------------------------------------

    private static UiLayoutSnapshot Arrange(UiHost host, float width = PageWidth)
    {
        host.MeasureAndArrange(new Vector2(width, PageHeight));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        return host.MeasureAndArrange(new Vector2(width, PageHeight));
    }

    private static void DrawWithButtons(UiHost host, Func<Rect, bool> click)
    {
        try
        {
            SetField(ButtonOverrideField, click);
            host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
        }
        finally
        {
            ClearOverrides();
        }
    }

    private static void DrawWithSliders(UiHost host, Func<Rect, float, float, float, float> drag)
    {
        try
        {
            SetField(SliderOverrideField, drag);
            host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
        }
        finally
        {
            ClearOverrides();
        }
    }

    private static Rect RectOf(UiLayoutSnapshot snapshot, string id)
    {
        Assert(snapshot.RectById.TryGetValue(id, out Rect rect),
            "the arranged snapshot must carry '" + id + "'; a missing element means the manifest lost it");
        return rect;
    }

    /// <summary>The card's rect in the coordinate space the recorded control rects live in (the scroll's
    /// content-local space), the same translation the mood lane uses.</summary>
    private static Rect CardLocal(UiLayoutSnapshot snapshot, string cardId)
    {
        Rect card = RectOf(snapshot, cardId);
        Rect viewport = snapshot.Viewports["content-scroll"];
        return new Rect(card.x - viewport.x, card.y - viewport.y, card.width, card.height);
    }

    private static Rect BandIn(List<Rect> recorded, UiLayoutSnapshot snapshot, string cardId)
    {
        Rect card = CardLocal(snapshot, cardId);
        List<Rect> bands = recorded
            .Where(r => Inside(r, card)
                && Math.Abs(r.width - DeclaredCheckboxWidth) <= 0.5f
                && Math.Abs(r.height - DeclaredCheckboxHeight) <= 0.5f)
            .OrderBy(r => r.y)
            .ToList();
        Assert(bands.Count == 1, "the '" + cardId + "' card must draw exactly one declared checkbox band, got "
            + bands.Count);
        return bands[0];
    }

    private static int CountOverlapping(List<Rect> recorded, Rect band)
    {
        return recorded.Count(r => Overlaps(r, band));
    }

    /// <summary>The atom's own band contract, read from the library source rather than re-derived: a
    /// text/wrapped element measures the wrapped height of its string at its own arranged width plus twice
    /// <c>theme.Geometry.Padding</c>.</summary>
    private static float WrappedBand(Program.StubMetrics metrics, string text, float width)
    {
        return Math.Max(1f, metrics.MeasureText(text, UiFont.Small, Math.Max(1f, width)))
            + 6f * 2f;
    }

    /// <summary>The right edge of the 18px visual box the input/checkbox atom paints inside its declared
    /// band: side = max(8, height - Padding * 2) at the band's LEFT edge, vertically centred.</summary>
    private static float VisualRight(Rect band)
    {
        float side = Math.Max(8f, band.height - 6f * 2f);
        return band.x + side;
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

    private static UiElementSpec? FindByAttribute(UiElementSpec root, string attribute, string value)
    {
        if (root.TryGetAttribute(attribute, out string actual) && string.Equals(actual, value, StringComparison.Ordinal))
        {
            return root;
        }

        foreach (UiElementSpec child in root.Children)
        {
            UiElementSpec? nested = FindByAttribute(child, attribute, value);
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

    private static bool Overlaps(Rect a, Rect b)
    {
        return a.x < b.xMax - 0.01f && b.x < a.xMax - 0.01f
            && a.y < b.yMax - 0.01f && b.y < a.yMax - 0.01f;
    }

    private static bool Inside(Rect inner, Rect outer)
    {
        return inner.x >= outer.x - 0.5f && inner.y >= outer.y - 0.5f
            && inner.xMax <= outer.xMax + 0.5f && inner.yMax <= outer.yMax + 0.5f;
    }

    private static string Describe(Rect rect)
    {
        return "(x=" + Num(rect.x) + " y=" + Num(rect.y) + " w=" + Num(rect.width) + " h=" + Num(rect.height)
            + " right=" + Num(rect.xMax) + ")";
    }

    private static string Num(float value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static FieldInfo ButtonOverrideField => RequireField("ButtonOverride", typeof(Func<Rect, bool>));

    private static FieldInfo SliderOverrideField =>
        RequireField("SliderOverride", typeof(Func<Rect, float, float, float, float>));

    private static void ClearOverrides()
    {
        SetField(ButtonOverrideField, null);
        SetField(SliderOverrideField, null);
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
                "REFLECTION BLOCKER: UiNative." + name + " is not the expected "
                + fieldType.Name + " seam on net472; the hit/drag seams this lane drives have moved.");
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
            throw new InvalidOperationException("DeclarativeOverviewLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
