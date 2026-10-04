using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// Focused geometry/interaction tests for the Mood Modulation cards (outcome 3).
///
/// The contract these steps pin:
///  - ALL FOUR product moods (Good / Neutral / Bad / Break) get one card each, asserted per card, not just
///    as a total: the rich fixture carries all four with distinct values. Header (localized US.Mood.* name
///    + both reset controls), then three parameter blocks in the order Pitch, Volume, Jitter;
///  - every card's header resolves through its own US.Mood.&lt;Mood&gt; Keyed entry in EN and ZH: replacing
///    that one entry with a probe changes what the layout measures and grows that card;
///  - each block is ONE two-line template - label, minus, numeric field and plus on the first line, the
///    slider on the second, starting at the numeric group's left edge;
///  - every parameter of every mood registers a slider, a number field, a minus and a plus: no layout
///    branch may omit a control (the old MoodRowMode Stacked branch did);
///  - the parameter label is drawn in a band at least as wide as the RESOLVED localized label (EN and
///    ZH, through Program.SetTranslatorResolver + Program.ReadKeyedTable), never in the old fixed 14px
///    band that clipped Pitch/Volume/Jitter;
///  - slider track widths are equal within 1px across parameters and moods;
///  - values round-trip through slider/number/plus-minus/reset with the unchanged ranges, 0.05 step,
///    0.### format, element id and typed actions.
///
/// Rect capture strategy:
///  - the test host has no InternalsVisibleTo, so the internal static UiNative test seams are set by
///    reflection (ButtonOverride/SliderOverride/TextFieldOverride);
///  - during one recording DrawFrame the overrides record every control rect and return neutral values
///    (no clicks, no slider change, no text commit);
///  - snapshot.RectById["scope-tree"] is the page-space card outer rect; snapshot.Viewports and the
///    session scroll position convert scroll-content-local control rects back to page/window space.
/// </summary>
internal static class MoodLayoutFocusedTests
{
    private const float ViewportWidth = 800f;
    private const float ViewportHeight = 600f;
    // Below the manifest's 500px body-row breakpoint the row resolves to a stacked column, so this
    // viewport (480 - 24 page padding = 456 inner < 500) exercises the narrow page shape; the card
    // itself is then the full column width.
    private const float NarrowViewportWidth = 480f;
    // A viewport comfortably inside the two-column row regime (724 inner 700 >= the row's 500
    // breakpoint) - the narrow-card regime this lane exists for. BH1: the row's two columns are nav +
    // centre at every real width, because help no longer takes a third column out of the width.
    private const float NarrowestRowRegimeWidth = 724f;
    // The rich RecordingSettingsSource fixture mirrors production: one mood row per SqueakMood, i.e.
    // ALL FOUR product moods (Good / Neutral / Bad / Break), each with distinct effective values.
    private const int MoodCount = 4;
    /// <summary>Parameter blocks per mood card: Pitch, Volume, Jitter, in draw order.</summary>
    private const int ParameterCount = 3;
    /// <summary>Controls one mood card registers: 3 sliders + 3 fields + 6 small buttons + 2 resets.</summary>
    private const int ControlsPerMoodCard = 14;
    // Mirrored widget geometry, like the reset widths below: the standard numeric group (minus + field +
    // plus) and the gap between the measured label column and that group.
    private const float StandardControlGroupWidth = 88f;
    private const float LabelColumnGap = 6f;
    // Left padding the widget gives a mood row inside the card body.
    private const float ParameterContentInset = 10f;
    private const float Epsilon = 0.5f;
    private const float RectMatchEpsilon = 0.01f;

    private static readonly string[] ParameterLabelKeys =
    {
        "US.Tuning.Factor.Pitch",
        "US.Tuning.Factor.Volume",
        "US.Tuning.Factor.Jitter",
    };

    /// <summary>The four product moods in production enumeration order (Enum.GetValues(typeof(SqueakMood))).</summary>
    private static readonly SqueakMood[] ProductMoods =
    {
        SqueakMood.Good,
        SqueakMood.Neutral,
        SqueakMood.Bad,
        SqueakMood.Break,
    };

    /// <summary>
    /// Effective Pitch/Volume/Jitter values the rich fixture carries, index-aligned with
    /// <see cref="ProductMoods"/>. Mirrored from RecordingSettingsSource.BuildRichView; kept distinct so
    /// each card can be identified by the value its own controls write. The per-card routing step below
    /// pins the -0.05 step on each of these numbers, so a card silently reusing another card's row fails.
    /// </summary>
    private static readonly float[][] FixtureMoodValues =
    {
        new[] { 1f, 1f, 0f },        // Good
        new[] { 0.9f, 0.8f, 0.1f },  // Neutral
        new[] { 0.75f, 0.6f, 0.2f }, // Bad
        new[] { 0.6f, 0.4f, 0.3f },  // Break
    };

    private static FieldInfo ButtonOverrideField => RequireField("ButtonOverride", typeof(Func<Rect, bool>));
    private static FieldInfo SliderOverrideField => RequireField("SliderOverride", typeof(Func<Rect, float, float, float, float>));
    private static FieldInfo TextFieldOverrideField => RequireField("TextFieldOverride", typeof(Func<Rect, string, string>));

    public static int RunAll()
    {
        Step("two-line mood template geometry at 800px", () => TwoLineMoodTemplateGeometry(ViewportWidth, ViewportHeight));
        Step("every parameter of every mood registers slider/field/minus/plus", EveryParameterRegistersEveryControl);
        Step("parameter labels fit their bands (EN and ZH, resolved labels)", ParameterLabelBandsCoverResolvedLabels);
        Step("parameter labels do not overflow the drawn bands (EN and ZH)", ParameterLabelsFitTheirBands);
        Step("all four product mood cards are present and route their own mood", AllFourProductMoodCardsArePresent);
        Step("all four mood cards share one row width and control geometry", AllFourCardsShareOneRowGeometry);
        Step("every mood header resolves through its own US.Mood key (EN and ZH)", MoodHeadersResolveThroughKeyedEntries);
        Step("the label column follows a longer resolved label", LabelColumnFollowsTheResolvedLabelWidth);
        Step("slider track widths are equal across parameters and moods", SliderTrackWidthsAreEqual);
        Step("narrow viewport keeps the template and every control", () => TwoLineMoodTemplateGeometry(NarrowViewportWidth, 720f));
        Step("narrowest two-column card keeps every control", () => TwoLineMoodTemplateGeometry(NarrowestRowRegimeWidth, 720f));
        Step("736px two-column card keeps the template", () => TwoLineMoodTemplateGeometry(736f, 720f));
        Step("mood value round-trip clamps to the factor ranges", MoodValueRoundTripClampsToFactorRanges);
        Step("minus click routes typed set-mood-tuning", MinusClickRoutesTypedMoodTuning);
        Step("plus click routes typed set-mood-tuning", PlusClickRoutesTypedMoodTuning);
        Step("slider change routes typed set-mood-tuning", SliderChangeRoutesTypedMoodTuning);
        Step("number commit routes typed set-mood-tuning", NumberCommitRoutesTypedMoodTuning);
        Step("auto button routes typed set-mood-tuning", AutoButtonRoutesTypedMoodTuning);
        Step("preset reset routes typed reset-mood-to-preset", PresetResetRoutesTypedMoodTuning);
        Step("reset controls route at every card width", ResetRoutingAcrossCardWidths);
        Step("a popup-covered nav control yields the click", CoveredControlYieldsTheClick);
        Step("every nav card paints its own enclosure (V1)", EveryNavCardPaintsItsOwnEnclosure);
        Step("a checkbox-row press is decided by one control and flips the value once", CheckboxRowPressDecidesOnce);
        Step("V3: the Tuning areas are ordered layer/domain -> scope -> mood at the real boxes", TuningAreasAtTheRealBoxes);
        Step("V3: the inherited hint and the mood source readout are laid out at every real body", TuningInheritanceReadoutsAtRealBodies);
        Step("V3: the mood area survives an empty scope list (defensive degenerate input)", MoodAreaSurvivesMissingScopeRows);
        Step("V3: layer/domain and mood writes stay on their own channels (increments)", TuningChannelsDoNotCrossOnIncrements);
        Step("V3/task-18: the mood readout is truthful about provenance vs the reset TARGET", MoodSourceReadoutMatrix);
        Step("V3/PM: production record fold projects the real per-factor sources and reset target", ProductionMoodSourceProjection);
        Step("V3/PM: action rows measure the resolved long label", ResolvedActionLabelGrowsTheRow);
        Step("V3/PM: reset target remains inside the inline mood header", ResetTargetStaysInTheHeader);

        Console.WriteLine("MoodLayoutFocusedTests ALL PASS");
        return 0;
    }

    /// <summary>
    /// The declared Overview checkbox row's click contract, re-cut for S4-1. The composite used to draw a
    /// row-wide hit band truncated at the checkbox; the declarative row has exactly one hit surface - the
    /// input/checkbox atom's own band - so the property to pin changed from "the band stops where the slot
    /// starts" to "there IS no second band", which is the same failure caught one step earlier. Two facts,
    /// failing for different reasons:
    /// <list type="number">
    /// <item>THE DRAW FACT. A declared control row registers exactly ONE hit surface inside the card and the
    /// bands are pairwise disjoint. A page that put a <c>Chrome="none"</c> hit area behind a checkbox (the
    /// row-wide target the composite had) registers a second, overlapping band here - and in IMGUI both
    /// report the same press, which is a double toggle.</item>
    /// <item>THE BEHAVIOUR FACT. One press advances the session's revision clock by exactly one write and
    /// flips the value once.</item>
    /// </list>
    /// </summary>
    private static void CheckboxRowPressDecidesOnce()
    {
        const float Width = ViewportWidth;
        const float Height = 900f;
        // Parent ON: the eat-precision child row exists only while its parent switch is on, so the card
        // draws its full seven declared control rows.
        var source = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
        UiHost host = UsKernelSettingsHost.Create(source);
        try
        {
            host.Bindings.Invoke("set-tab", "Overview");
            host.MeasureAndArrange(new Vector2(Width, Height));
            Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
            UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(Width, Height));
            Assert(snapshot.RectById.TryGetValue("basic-tuning", out Rect card),
                "the Overview workspace must hold the basic-tuning card");

            var raw = new CapturedRects();
            try
            {
                SetButtonOverride(rect => { raw.Buttons.Add(rect); return false; });
                host.DrawChecked(new Rect(0f, 0f, Width, Height));
            }
            finally
            {
                ClearOverrides();
            }

            // RECT SPACES, and this is not cosmetic: the card's rect from the snapshot is PAGE space while
            // the button rects recorded above are in the scroll's LOCAL space (the engine's ToDrawRect
            // subtracts the scroll container's own rect position for its children). Filtering across the two
            // is a coordinate-space bug that happens to work until a layout shift moves the boundaries -
            // which the S3 header band did, and this lane then selected a LATER section's checkbox row as
            // "the first slot inside the card" and failed on the egg write. Translate the card into the
            // controls' space first; ordering by y then really is the widget's own draw order.
            Rect scrollViewport = snapshot.Viewports["content-scroll"];
            var cardLocal = new Rect(
                card.x - scrollViewport.x,
                card.y - scrollViewport.y,
                card.width,
                card.height);

            List<Rect> slots = raw.Buttons
                .Where(r => IsInside(r, cardLocal) && IsDeclaredCheckbox(r))
                .OrderBy(r => r.y)
                .ToList();
            Assert(slots.Count == 7,
                "the declared card draws seven control rows with the parent ON (egg + baby + three scalings + the"
                + " eat-precision parent and child), got " + slots.Count);

            // THE DRAW FACT, stated as the property itself: for every declared checkbox band, that band is
            // the ONLY recorded hit surface that intersects it. Nothing is assumed about which band belongs
            // to which row - a page that adds a row-wide hit area behind a checkbox fails here whichever row
            // it is, because IMGUI hands one press to every hit rect it overlaps.
            //
            // The assertion is per-band rather than "everything inside the card rect" on purpose: the
            // recorded rects live in scroll-CONTENT space while the card rect comes from the page-space
            // snapshot, and trying to filter foreign controls by that translation is the coordinate-space
            // bug this suite already paid for once.
            for (int i = 0; i < slots.Count; i++)
            {
                int overlapping = raw.Buttons.Count(r => Overlaps(r, slots[i]));
                Assert(overlapping == 1,
                    "a declared checkbox band must be its row's only hit surface; band " + slots[i]
                    + " intersects " + overlapping + " recorded surfaces. A second band behind a checkbox is"
                    + " the double-toggle IMGUI hands to both controls");
                for (int j = i + 1; j < slots.Count; j++)
                {
                    Assert(!Overlaps(slots[i], slots[j]),
                        "two declared checkbox bands must not overlap: " + slots[i] + " vs " + slots[j]);
                }
            }

            // THE BEHAVIOUR FACT. The egg row is the first control row in the manifest, and that it really
            // is the egg row is verified by what the press does rather than assumed: if it were another row,
            // allow-eggs would not move and the lane fails with a name.
            Rect slot = slots[0];
            bool? before = source.LastEasterEggs;
            int revisionBefore = host.Session.ContentRevision;
            try
            {
                SetButtonOverride(rect => RectMatches(rect, slot));
                host.DrawChecked(new Rect(0f, 0f, Width, Height));
            }
            finally
            {
                ClearOverrides();
            }

            Assert(source.LastEasterEggs != before, "a press on the declared checkbox must flip the value exactly once");
            Assert(host.Session.ContentRevision == revisionBefore + 1,
                "and it must be exactly one write (revision " + revisionBefore + " -> " + host.Session.ContentRevision + ")");

            // The LAST band is the eat-precision child's, the one row whose presence depends on another
            // control - so a press there must route to the child value and to nothing else.
            Rect child = slots[slots.Count - 1];
            int childRevisionBefore = host.Session.ContentRevision;
            bool? parentBefore = source.LastEatPrecision;
            try
            {
                SetButtonOverride(rect => RectMatches(rect, child));
                host.DrawChecked(new Rect(0f, 0f, Width, Height));
            }
            finally
            {
                ClearOverrides();
            }

            Assert(source.LastEatPrecisionIncludeDrugs == true,
                "a press on the child row's declared checkbox must write the child value once");
            Assert(source.LastEatPrecision == parentBefore, "and it must not touch the parent switch");
            Assert(host.Session.ContentRevision == childRevisionBefore + 1,
                "and that press must be exactly one write too (revision " + childRevisionBefore
                + " -> " + host.Session.ContentRevision + ")");
        }
        finally
        {
            host.Dispose();
        }
    }

    /// <summary>
    /// The declared Overview control band: the manifest's boolean-switch cell, 36 x 30. It is the ARRANGED
    /// rect (the whole band is the hit rule) and deliberately NOT the composite's 24x24
    /// <c>UsKernelDraw.CheckboxHit</c> slot - the atom owns its own geometry now, and 30 is what makes the
    /// switch's 18px track fit inside the band's own padding. The 36 follows the manifest: S6-3's follow-up
    /// widened all seven bands 24 -> 36 so the 34x18 track finally renders instead of being clamped, and this
    /// predicate - which selects the bands out of the draw record - was re-cut in the same batch rather than
    /// silently matching nothing.
    /// <para>
    /// R2 (2026-09-28): the KIND behind the cell changed (`us/square-toggle` -> the carrier's
    /// <c>input/checkbox</c> with <c>Appearance="switch"</c>, the same 34x18/14/2/6 proportions). This
    /// predicate keys on the BAND, which the migration deliberately kept at 36x30, so it survives the kind
    /// swap unchanged - and that is the point: the row layout is not what moved.
    /// </para>
    /// </summary>
    private static bool IsDeclaredCheckbox(Rect rect)
    {
        return Math.Abs(rect.width - 36f) <= 0.5f && Math.Abs(rect.height - 30f) <= 0.5f;
    }

    /// <summary>One Repaint pass with the pointer parked at <paramref name="pointer"/> - the same frame
    /// protocol Program uses, kept local because this lane needs the pointer position, not the pump.</summary>
    private static void DrawWithPointer(UiHost host, Rect viewport, Vector2 pointer)
    {
        Event e = Event.KeyboardEvent("dummy");
        e.type = EventType.Repaint;
        e.mousePosition = pointer;
        Event.current = e;
        try
        {
            host.DrawChecked(viewport);
        }
        finally
        {
            Event.current = null;
        }
    }

    private static Vector2 Centre(Rect rect) => new(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);

    /// <summary>The hit bands of the row a slot sits on: wider than the slot, ending at its left edge. The
    /// card's snapshot rect cannot be used to filter these - it does not span the rows' left edge - so the
    /// slot is the anchor and the band is found from what the draw actually registered.</summary>
    private static List<Rect> RowBandsFor(List<Rect> recorded, Rect slot)
    {
        // Same row means: the same height band and the same vertical centre. A looser overlap test also
        // catches the navigation rail's 56px items, which sit to the left of the page and share some
        // vertical range with every row - they are not this row's band and must not be counted as one.
        return recorded
            .Where(r => r.width > UsKernelDraw.CheckboxHit + 0.5f
                && r.xMax <= slot.x + 0.01f
                && Math.Abs(r.height - slot.height) <= 8f
                && Math.Abs((r.y + r.height * 0.5f) - (slot.y + slot.height * 0.5f)) <= 4f)
            .ToList();
    }

    private static bool OverlapsVertically(Rect left, Rect right)
    {
        float overlap = Math.Min(left.yMax, right.yMax) - Math.Max(left.y, right.y);
        return overlap >= Math.Min(left.height, right.height) * 0.5f;
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("MoodLayoutFocusedTests step failed: " + name, ex);
        }
    }

    /// <summary>
    /// The whole card template at one viewport width: presence, containment, and the repeated two-line
    /// parameter block. Mode-agnostic on purpose - the template is the contract whether the card keeps the
    /// label beside the numeric group or has to give the label its own line.
    /// </summary>
    private static void TwoLineMoodTemplateGeometry(float viewportWidth, float viewportHeight)
    {
        using CaptureContext ctx = CreateCaptureContext(viewportWidth, viewportHeight);
        AssertMoodGeometry(ctx, viewportWidth);
        AssertTemplateAlignment(ctx, viewportWidth);

        // Recorded evidence: the measured card, the row the parameters get, the observed label column and
        // one slider track. The sweeps assert these relationships; these numbers are what the Lead can cite.
        List<MoodRowControls> measured = ctx.Mood.Grouped(MoodCount);
        Console.WriteLine("[mood] viewport " + viewportWidth.ToString("0", CultureInfo.InvariantCulture)
            + "px: card=" + ctx.CardPageRect.width.ToString("0.#", CultureInfo.InvariantCulture)
            + "px row=" + MoodRowWidth(ctx).ToString("0.#", CultureInfo.InvariantCulture)
            + "px labelColumn=" + ParameterLabelColumn(ctx).ToString("0.#", CultureInfo.InvariantCulture)
            + "px sliderX=" + measured[0].Sliders[0].x.ToString("0.#", CultureInfo.InvariantCulture)
            + "px sliderW=" + measured[0].Sliders[0].width.ToString("0.#", CultureInfo.InvariantCulture)
            + "px minusX=" + measured[0].Minus[0].x.ToString("0.#", CultureInfo.InvariantCulture)
            + "px sliderY-minusY=" + (measured[0].Sliders[0].y - measured[0].Minus[0].y).ToString("0.#", CultureInfo.InvariantCulture) + "px");
    }

    /// <summary>
    /// The core presence contract, stated per parameter of every mood rather than as a total: for each
    /// fixture mood card and each of the three parameters, the real draw pass must register a minus, a
    /// plus, a numeric field and a slider, and both reset controls. A layout branch that drops any control
    /// (the retired MoodRowMode.Stacked shape dropped sliders/fields/minus/plus) fails here even if totals
    /// happened to add up.
    /// </summary>
    private static void EveryParameterRegistersEveryControl()
    {
        using CaptureContext ctx = CreateCaptureContext(NarrowestRowRegimeWidth, 720f);
        List<MoodRowControls> rows = ctx.Mood.Grouped(MoodCount);
        Assert(rows.Count == MoodCount, "expected one control group per mood card, got " + rows.Count);
        for (int m = 0; m < rows.Count; m++)
        {
            for (int p = 0; p < 3; p++)
            {
                string where = "mood card " + m + ", parameter " + p;
                Assert(rows[m].Minus[p].width > 1f, where + " must register a minus button");
                Assert(rows[m].Plus[p].width > 1f, where + " must register a plus button");
                Assert(rows[m].Fields[p].width > 1f, where + " must register a numeric field");
                Assert(rows[m].Sliders[p].width > 1f, where + " must register a slider");
                Assert(rows[m].Minus[p].x < rows[m].Plus[p].x, where + ": the minus must precede the plus");
            }

            Assert(rows[m].ResetDefault.Count == 1 && rows[m].ResetDefault[0].width > 1f,
                "mood card " + m + " must register its \"reset to default\" control");
            Assert(rows[m].ResetPreset.Count == 1 && rows[m].ResetPreset[0].width > 1f,
                "mood card " + m + " must register its \"reset to preset\" control");
        }
    }

    /// <summary>Slider track widths and left edges must be identical (within 1px) across all TWELVE
    /// parameter blocks - the three parameters of all four fixture moods.</summary>
    private static void SliderTrackWidthsAreEqual()
    {
        using CaptureContext ctx = CreateCaptureContext(ViewportWidth, ViewportHeight);
        List<MoodRowControls> rows = ctx.Mood.Grouped(MoodCount);
        var widths = new List<float>();
        var lefts = new List<float>();
        foreach (MoodRowControls row in rows)
        {
            for (int p = 0; p < 3; p++)
            {
                widths.Add(row.Sliders[p].width);
                lefts.Add(row.Sliders[p].x);
            }
        }

        Assert(widths.Count == MoodCount * ParameterCount, "expected twelve mood sliders across the four fixture moods, got " + widths.Count);
        Assert(widths.Max() - widths.Min() < 1f,
            "slider track widths must be equal within 1px across every parameter and mood (min " + widths.Min() + ", max " + widths.Max() + ")");
        Assert(lefts.Max() - lefts.Min() < 1f,
            "slider tracks must start at the same x across every parameter and mood (min " + lefts.Min() + ", max " + lefts.Max() + ")");
    }

    /// <summary>
    /// The label fix, measured in both shipped languages through the harness language tables. The label band
    /// is the space between the parameter content's left edge and the numeric group's left edge; when the
    /// card is too narrow to carry that column beside the controls the label gets its own full-width line.
    /// Either way the band must be at least as wide as the widest RESOLVED label of the three parameters.
    /// </summary>
    private static void ParameterLabelBandsCoverResolvedLabels()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            var metrics = new Program.StubMetrics();
            Program.SetTranslatorResolver(table);
            try
            {
                float widest = 0f;
                foreach (string key in ParameterLabelKeys)
                {
                    Assert(table.ContainsKey(key), language + ": the shipped Keyed table must carry " + key);
                    widest = Math.Max(widest, metrics.MeasureWidth(table[key], UiFont.Tiny));
                }

                using CaptureContext ctx = CreateCaptureContext(ViewportWidth, ViewportHeight, metrics);
                float column = ParameterLabelColumn(ctx);
                float rowWidth = MoodRowWidth(ctx);
                Console.WriteLine("[mood-i18n] " + language + ": widestResolvedLabel="
                    + widest.ToString("0.#", CultureInfo.InvariantCulture) + "px labelColumn="
                    + column.ToString("0.#", CultureInfo.InvariantCulture) + "px row="
                    + rowWidth.ToString("0.#", CultureInfo.InvariantCulture) + "px");
                float band = column > 0.5f ? column : rowWidth;
                Assert(band + 0.01f >= widest,
                    language + ": the parameter label band must be at least the resolved label width (band " + band
                    + "px for the widest resolved label " + widest + "px; label column " + column + "px, card row " + rowWidth + "px)");
                Assert(column < rowWidth + 0.01f,
                    language + ": the measured label column must stay inside the card row (column " + column + "px, row " + rowWidth + "px)");

                // Per card: each of the four product mood cards must offer the same band. The shared
                // measurement above is the source of the column, so this loop is what proves every card
                // actually consumed it (a card that re-introduced its own narrower band fails here).
                List<MoodRowControls> rows = ctx.Mood.Grouped(MoodCount);
                Assert(rows.Count == MoodCount, language + ": expected " + MoodCount + " mood cards, got " + rows.Count);
                for (int m = 0; m < rows.Count; m++)
                {
                    float cardColumn = rows[m].Minus[0].x - ParameterContentX(ctx);
                    float cardBand = cardColumn > 0.5f ? cardColumn : rowWidth;
                    Assert(cardBand + 0.01f >= widest,
                        language + ": mood card " + m + " (" + ProductMoods[m] + ") must give its parameter labels a band at least the resolved label width (band "
                        + cardBand + "px for the widest resolved label " + widest + "px; card column " + cardColumn + "px)");
                }
                Console.WriteLine("[mood-i18n] " + language + ": per-card label bands all >= " + widest.ToString("0.#", CultureInfo.InvariantCulture)
                    + "px across " + rows.Count + " cards");
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    /// <summary>
    /// Failure sensitivity for the measured column: with a deliberately long translated Pitch label the
    /// observed column must widen beyond what the shipped English labels need. A widget that kept a fixed
    /// band (the old 14px) cannot move the column at all, so this fails for the exact regression the task
    /// describes - and it fails on the RESOLVED label, not on the key text.
    /// </summary>
    private static void LabelColumnFollowsTheResolvedLabelWidth()
    {
        Dictionary<string, string> english = Program.ReadKeyedTable("English");
        var probe = new Dictionary<string, string>(english, StringComparer.Ordinal)
        {
            ["US.Tuning.Factor.Pitch"] = "Pitch (semitones) (probe)"
        };

        var metrics = new Program.StubMetrics();
        float shipped;
        Program.SetTranslatorResolver(english);
        try
        {
            using CaptureContext ctx = CreateCaptureContext(ViewportWidth, ViewportHeight, metrics);
            shipped = ParameterLabelColumn(ctx);
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }

        Program.SetTranslatorResolver(probe);
        try
        {
            using CaptureContext ctx = CreateCaptureContext(ViewportWidth, ViewportHeight, metrics);
            float column = ParameterLabelColumn(ctx);
            float need = metrics.MeasureWidth(probe["US.Tuning.Factor.Pitch"], UiFont.Tiny);
            Console.WriteLine("[mood-probe] shippedLabelColumn=" + shipped.ToString("0.#", CultureInfo.InvariantCulture)
                + "px probeLabelColumn=" + column.ToString("0.#", CultureInfo.InvariantCulture)
                + "px probeLabel=" + need.ToString("0.#", CultureInfo.InvariantCulture) + "px");
            Assert(column > 0.5f, "the 800px card must keep the label column beside the numeric group for this probe");
            Assert(column + 0.01f >= need,
                "the label column must follow the resolved label width (column " + column + "px, probe label " + need + "px)");
            Assert(column > shipped + 20f,
                "a longer resolved Pitch label must widen the column (shipped " + shipped + "px, probe " + column + "px)");
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    /// <summary>
    /// The regression this task exists for, driven through the real fit audit: with the shipped language
    /// table loaded and the harness metrics attached to both the host and the audit, drawing the Tuning page
    /// must produce NO finding for any of the three resolved parameter labels. The pre-fix widget drew them
    /// into a fixed 14x18px band and the audit reported exactly
    /// "Height needs 54px, has 18px at width 14px, text=Pitch|Volume|Jitter".
    /// </summary>
    private static void ParameterLabelsFitTheirBands()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            var metrics = new Program.StubMetrics();
            var reports = new List<UiOverflowReport>();
            UiFitAudit.Attach(metrics, reports.Add);
            UiFitAudit.Enabled = true;
            Program.SetTranslatorResolver(table);
            try
            {
                UiFitAudit.Reset();
                reports.Clear();
                using (UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, metrics))
                {
                    host.Bindings.Invoke("set-tab", "Tuning");
                    host.MeasureAndArrange(new Vector2(ViewportWidth, ViewportHeight));
                    host.DrawChecked(new Rect(0f, 0f, ViewportWidth, ViewportHeight));
                }

                var offenders = new List<string>();
                foreach (UiOverflowReport report in reports)
                {
                    foreach (string key in ParameterLabelKeys)
                    {
                        if (table.TryGetValue(key, out string? text) && string.Equals(report.Text, text, StringComparison.Ordinal))
                        {
                            offenders.Add(report.ElementPath + "/" + report.Axis + " needs " + report.Needed
                                + "px, has " + report.Available + "px at width " + report.RectWidth + "px, text=\"" + report.Text + "\"");
                        }
                    }
                }

                Assert(offenders.Count == 0,
                    language + ": the parameter labels must fit the bands they are measured into; findings: " + string.Join(" | ", offenders));
            }
            finally
            {
                UiFitAudit.Detach();
                Program.SetTranslatorResolver(null);
            }
        }
    }

    /// <summary>
    /// EVERY mood header comes from its own resolved US.Mood.* entry, in both shipped languages - not from
    /// a language literal and not from the row's debug display name. Three facts per mood per language:
    /// <list type="number">
    /// <item>the shipped table's value for US.Mood.Mood is the text the layout measures (and the raw key
    /// literal never reaches the text ruler, so the header really went through the translation seam);</item>
    /// <item>replacing ONLY that mood's entry with a unique probe makes the probe the measured header text -
    /// a hard-coded "Good"/良好 or another mood's entry can never produce the probe;</item>
    /// <item>the probe is long enough to wrap the header band, and the card grows by more than 10px, so the
    /// resolved value is measured into the card's height, not merely drawn.</item>
    /// </list>
    /// The fixture's DisplayNames are the enum names ("Good" ... "Break"), which are identical to the
    /// English values: a widget that drew DisplayName cannot satisfy fact 2 in either language.
    /// </summary>
    private static void MoodHeadersResolveThroughKeyedEntries()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            foreach (SqueakMood mood in ProductMoods)
            {
                string key = "US.Mood." + mood;
                Assert(table.ContainsKey(key), language + ": the shipped Keyed table must carry " + key);
                string resolved = table[key];

                // (1) The shipped table: the resolved value must be measured into the layout, and the
                //     unresolved key literal must not be.
                var shippedMetrics = new RecordingMetrics();
                float shippedHeight = TuningCardHeight(table, shippedMetrics);
                Assert(shippedHeight > 0f, "the Tuning workspace must place the scope-tree card");
                Assert(shippedMetrics.Measured(resolved),
                    language + ": the " + mood + " card header must be measured from the resolved " + key
                    + " value \"" + resolved + "\"");
                Assert(!shippedMetrics.Measured(key),
                    language + ": the raw key literal " + key + " must never reach the text ruler (the header must resolve through the translation seam)");

                // (2)+(3) Per-mood key sensitivity: only this mood's entry changes. The unique probe must
                //         be the measured header text and must grow the card.
                string probe = "US-MOOD-PROBE-" + mood.ToString().ToUpperInvariant() + new string('p', 140);
                var probeTable = new Dictionary<string, string>(table, StringComparer.Ordinal) { [key] = probe };
                var probeMetrics = new RecordingMetrics();
                float probeHeight = TuningCardHeight(probeTable, probeMetrics);
                Assert(probeMetrics.Measured(probe),
                    language + ": the " + mood + " card header must follow " + key + " (the unique probe was never measured, so the header is not keyed)");
                Assert(!probeMetrics.Measured(resolved),
                    language + ": after " + key + " was replaced, the shipped value \"" + resolved + "\" must no longer be the measured header");
                Assert(probeHeight > shippedHeight + 10f,
                    language + ": a longer resolved " + key + " value must be measured into the card header (shipped "
                    + shippedHeight + "px, probe " + probeHeight + "px) - a hard-coded name cannot grow");

                Console.WriteLine("[mood-header] " + language + " " + mood + ": key=" + key + " resolved=\"" + resolved
                    + "\" resolvedMeasured=true rawKeyMeasured=false probeMeasured=true cardHeight="
                    + shippedHeight.ToString("0.#", CultureInfo.InvariantCulture) + "px->"
                    + probeHeight.ToString("0.#", CultureInfo.InvariantCulture) + "px");
            }
        }
    }

    /// <summary>Arranges and draws the Tuning card with one language table, recording every measured text,
    /// and returns its measured height.</summary>
    private static float TuningCardHeight(Dictionary<string, string> table, RecordingMetrics metrics)
    {
        Program.SetTranslatorResolver(table);
        try
        {
            using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, metrics);
            host.Bindings.Invoke("set-tab", "Tuning");
            UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(ViewportWidth, ViewportHeight));
            host.DrawChecked(new Rect(0f, 0f, ViewportWidth, ViewportHeight));
            return snapshot.RectById.TryGetValue("scope-tree", out Rect card) ? card.height : 0f;
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    /// <summary>
    /// The user-facing requirement this lane was extended for: ALL FOUR product mood cards exist, in
    /// production order, and each one owns its own mood identity and distinct fixture values. Identity is
    /// measured, not assumed: pressing the Pitch minus of card m must write exactly the mood of card m with
    /// the value FixtureMoodValues[m][0] - 0.05. The pre-extension two-row fixture cannot run this step at
    /// all; a widget that reused one row for two cards, or emitted them in another order, fails here.
    /// </summary>
    private static void AllFourProductMoodCardsArePresent()
    {
        using CaptureContext ctx = CreateCaptureContext(ViewportWidth, ViewportHeight);
        List<MoodRowControls> rows = ctx.Mood.Grouped(MoodCount);
        Assert(rows.Count == MoodCount, "expected " + MoodCount + " product mood cards, got " + rows.Count);
        Assert(ProductMoods.Length == MoodCount, "ProductMoods must list one mood per fixture card");

        for (int m = 0; m < MoodCount; m++)
        {
            SqueakMood mood = ProductMoods[m];
            MoodRowControls row = rows[m];
            Assert(row.Minus.Count == ParameterCount && row.Plus.Count == ParameterCount
                && row.Fields.Count == ParameterCount && row.Sliders.Count == ParameterCount,
                "mood card " + m + " (" + mood + ") must carry all three parameter blocks");
            Assert(row.ResetDefault.Count == 1 && row.ResetPreset.Count == 1,
                "mood card " + m + " (" + mood + ") must carry both reset controls");

            ClearLastMood(ctx.Source);
            try
            {
                Rect minus = row.Minus[0];
                SetButtonOverride(rect => RectMatches(rect, minus));
                SetSliderOverride((rect, value, min, max) => value);
                SetTextFieldOverride((rect, text) => text);
                ctx.Host.DrawChecked(ctx.Viewport);
                Assert(ctx.Source.LastMood == mood,
                    "the Pitch minus of card " + m + " must write " + mood + " (got " + ctx.Source.LastMood + ")");
                float expected = FixtureMoodValues[m][0] - 0.05f;
                Assert(FloatEquals(ctx.Source.LastMoodValue, expected),
                    "card " + m + " (" + mood + ") must start from its own distinct Pitch " + FixtureMoodValues[m][0]
                    + " and step to " + expected.ToString("0.####", CultureInfo.InvariantCulture)
                    + " (got " + ctx.Source.LastMoodValue + ")");
            }
            finally
            {
                ClearOverrides();
                ClearLastMood(ctx.Source);
            }

            Console.WriteLine("[mood-card] card " + m + " = " + mood
                + ": pitch=" + FixtureMoodValues[m][0].ToString("0.####", CultureInfo.InvariantCulture)
                + " volume=" + FixtureMoodValues[m][1].ToString("0.####", CultureInfo.InvariantCulture)
                + " jitter=" + FixtureMoodValues[m][2].ToString("0.####", CultureInfo.InvariantCulture)
                + "; parameterBlocks=" + ParameterCount
                + " resetControls=" + (row.ResetDefault.Count + row.ResetPreset.Count));
        }
    }

    /// <summary>
    /// The four cards must share ONE row width and ONE control geometry. Measured on the drawn rects:
    /// every card's row starts at the same left edge (the label column) and its slider ends at the card
    /// content's right edge, so each card's row width is the same number; the minus/field/plus widths and
    /// their left/right edges are identical; and the slider track (x and width) is identical across all
    /// twelve parameter instances. This is the per-card counterpart of the aggregate slider-width step.
    /// </summary>
    private static void AllFourCardsShareOneRowGeometry()
    {
        using CaptureContext ctx = CreateCaptureContext(ViewportWidth, ViewportHeight);
        List<MoodRowControls> rows = ctx.Mood.Grouped(MoodCount);
        Assert(rows.Count == MoodCount, "expected " + MoodCount + " product mood cards, got " + rows.Count);

        // The widget gives the parameter row the card content's full width: the row starts at the card body
        // left plus the widget left padding, and the slider track ends at the card body right minus that
        // same padding (the widget's own right inset), which is the same number on every card.
        float rowLeft = ParameterContentX(ctx);
        float rowRight = ctx.CardLocalRect.xMax - UsCardLayout.Padding - ParameterContentInset;
        float rowWidth = rowRight - rowLeft;
        MoodRowControls reference = rows[0];
        for (int m = 0; m < rows.Count; m++)
        {
            float cardRowWidth = rows[m].Sliders[0].xMax - rowLeft;
            Assert(Math.Abs(cardRowWidth - rowWidth) <= 1f,
                "mood card " + m + " (" + ProductMoods[m] + ") must have the same row width as every other card (card "
                + cardRowWidth + "px vs " + rowWidth + "px)");

            for (int p = 0; p < ParameterCount; p++)
            {
                string where = "mood card " + m + " (" + ProductMoods[m] + "), parameter " + p;
                Assert(Math.Abs(rows[m].Minus[p].x - reference.Minus[p].x) <= 0.01f,
                    where + ": the row must start at the shared label column (minus x " + rows[m].Minus[p].x + " vs " + reference.Minus[p].x + ")");
                Assert(Math.Abs(rows[m].Fields[p].x - reference.Fields[p].x) <= 0.01f
                    && Math.Abs(rows[m].Plus[p].x - reference.Plus[p].x) <= 0.01f
                    && Math.Abs(rows[m].Plus[p].xMax - reference.Plus[p].xMax) <= 0.01f,
                    where + ": the minus/field/plus group must share one left and right edge across all four cards");
                Assert(Math.Abs(rows[m].Minus[p].width - reference.Minus[p].width) <= 0.01f
                    && Math.Abs(rows[m].Fields[p].width - reference.Fields[p].width) <= 0.01f
                    && Math.Abs(rows[m].Plus[p].width - reference.Plus[p].width) <= 0.01f,
                    where + ": minus/field/plus widths must be identical across all four cards");
                Assert(Math.Abs(rows[m].Sliders[p].x - reference.Sliders[p].x) <= 0.01f
                    && Math.Abs(rows[m].Sliders[p].width - reference.Sliders[p].width) <= 0.01f,
                    where + ": the slider track must share one x and one width across all four cards");
                Assert(Math.Abs(rows[m].Sliders[p].xMax - rowRight) <= 1f,
                    where + ": the slider track must reach the row's right edge (the row fills the card content; track ends at "
                    + rows[m].Sliders[p].xMax + ", row right " + rowRight + ")");
                Assert(rows[m].Sliders[p].y > rows[m].Minus[p].y + 1f,
                    where + ": the slider must be on the line below the minus/field/plus line");
            }
        }

        Console.WriteLine("[mood-row] all " + rows.Count + " cards share rowWidth=" + rowWidth.ToString("0.#", CultureInfo.InvariantCulture)
            + "px (content x " + rowLeft.ToString("0.#", CultureInfo.InvariantCulture)
            + " -> " + rowRight.ToString("0.#", CultureInfo.InvariantCulture)
            + "); minusW=" + reference.Minus[0].width.ToString("0.#", CultureInfo.InvariantCulture)
            + " fieldW=" + reference.Fields[0].width.ToString("0.#", CultureInfo.InvariantCulture)
            + " plusW=" + reference.Plus[0].width.ToString("0.#", CultureInfo.InvariantCulture)
            + " sliderW=" + reference.Sliders[0].width.ToString("0.#", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Value round-trip: the slider, the number field and the +/- controls still route typed
    /// "set-mood-tuning" writes with the unchanged ranges - Pitch 0.5-2, Volume 0.1-2, Jitter 0-0.5 - the
    /// unchanged 0.05 step and the clamped endpoints.
    /// </summary>
    private static void MoodValueRoundTripClampsToFactorRanges()
    {
        // Plus routes to the parameter it belongs to (Volume) and applies the 0.05 step.
        using (CaptureContext ctx = CreateCaptureContext())
        {
            MoodRowControls row = ctx.Mood.Grouped(MoodCount)[0];
            ClearLastMood(ctx.Source);
            try
            {
                SetButtonOverride(rect => RectMatches(rect, row.Plus[1]));
                SetSliderOverride((rect, value, min, max) => value);
                SetTextFieldOverride((rect, text) => text);
                ctx.Host.DrawChecked(ctx.Viewport);
                Assert(ctx.Source.LastMood == SqueakMood.Good && ctx.Source.LastMoodFactor == SqueakMoodFactor.Volume,
                    "the plus of the second parameter must write the Volume factor of the first card");
                Assert(FloatEquals(ctx.Source.LastMoodValue, 1.05f), "Volume plus must step 1.0 up to 1.05");
            }
            finally
            {
                ClearOverrides();
                ClearLastMood(ctx.Source);
            }
        }

        // Minus at the Jitter floor (the fixture's jitter is 0) clamps to 0 instead of going negative.
        using (CaptureContext ctx = CreateCaptureContext())
        {
            MoodRowControls row = ctx.Mood.Grouped(MoodCount)[0];
            ClearLastMood(ctx.Source);
            try
            {
                SetButtonOverride(rect => RectMatches(rect, row.Minus[2]));
                SetSliderOverride((rect, value, min, max) => value);
                SetTextFieldOverride((rect, text) => text);
                ctx.Host.DrawChecked(ctx.Viewport);
                Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Jitter, "the minus of the third parameter must write the Jitter factor");
                Assert(FloatEquals(ctx.Source.LastMoodValue, 0f), "Jitter minus at the 0 floor must clamp to 0");
            }
            finally
            {
                ClearOverrides();
                ClearLastMood(ctx.Source);
            }
        }

        // A number commit above the Pitch maximum clamps to 2; below the minimum clamps to 0.5.
        (string Text, float Expected, string Why)[] commits =
        {
            ("9", 2f, "above the maximum"),
            ("-5", 0.5f, "below the minimum"),
        };
        foreach (var commit in commits)
        {
            using CaptureContext ctx = CreateCaptureContext();
            MoodRowControls row = ctx.Mood.Grouped(MoodCount)[0];
            ClearLastMood(ctx.Source);
            try
            {
                SetButtonOverride(rect => false);
                SetSliderOverride((rect, value, min, max) => value);
                SetTextFieldOverride((rect, text) => RectMatches(rect, row.Fields[0]) ? commit.Text : text);
                ctx.Host.DrawChecked(ctx.Viewport);
                Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Pitch, "the first number field must target Pitch");
                Assert(FloatEquals(ctx.Source.LastMoodValue, commit.Expected),
                    "a Pitch commit " + commit.Why + " must clamp to " + commit.Expected + " (got " + ctx.Source.LastMoodValue + ")");
            }
            finally
            {
                ClearOverrides();
                ClearLastMood(ctx.Source);
            }
        }
    }

    private static void MinusClickRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        Rect minus = FirstMinusRect(ctx);
        ClearLastMood(ctx.Source);

        try
        {
            SetButtonOverride(rect => RectMatches(rect, minus));
            SetSliderOverride((rect, value, min, max) => value);
            SetTextFieldOverride((rect, text) => text);
            ctx.Host.DrawChecked(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMood == SqueakMood.Good, "minus click must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Pitch, "minus click on the first stepper line must target Pitch");
            Assert(FloatEquals(ctx.Source.LastMoodValue, 0.95f), "Pitch minus must step 1.0 down to 0.95");
        }
        finally
        {
            ClearOverrides();
            ClearLastMood(ctx.Source);
        }
    }

    private static void PlusClickRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        Rect plus = FirstPlusRect(ctx);
        ClearLastMood(ctx.Source);

        try
        {
            SetButtonOverride(rect => RectMatches(rect, plus));
            SetSliderOverride((rect, value, min, max) => value);
            SetTextFieldOverride((rect, text) => text);
            ctx.Host.DrawChecked(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMood == SqueakMood.Good, "plus click must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Pitch, "plus click on the first stepper line must target Pitch");
            Assert(FloatEquals(ctx.Source.LastMoodValue, 1.05f), "Pitch plus must step 1.0 up to 1.05");
        }
        finally
        {
            ClearOverrides();
            ClearLastMood(ctx.Source);
        }
    }

    private static void SliderChangeRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        Rect slider = FirstSliderRect(ctx);
        ClearLastMood(ctx.Source);

        try
        {
            SetButtonOverride(rect => false);
            SetSliderOverride((rect, value, min, max) => RectMatches(rect, slider) ? 1.25f : value);
            SetTextFieldOverride((rect, text) => text);
            ctx.Host.DrawChecked(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMood == SqueakMood.Good, "slider change must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Pitch, "first slider must target Pitch");
            Assert(FloatEquals(ctx.Source.LastMoodValue, 1.25f), "Pitch slider must write 1.25");
        }
        finally
        {
            ClearOverrides();
            ClearLastMood(ctx.Source);
        }
    }

    private static void NumberCommitRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        Rect field = FirstFieldRect(ctx);
        ClearLastMood(ctx.Source);

        try
        {
            SetButtonOverride(rect => false);
            SetSliderOverride((rect, value, min, max) => value);
            SetTextFieldOverride((rect, text) => RectMatches(rect, field) ? "1.25" : text);
            ctx.Host.DrawChecked(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMood == SqueakMood.Good, "number commit must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Pitch, "first number field must target Pitch");
            Assert(FloatEquals(ctx.Source.LastMoodValue, 1.25f), "Pitch number commit must write 1.25");
        }
        finally
        {
            ClearOverrides();
            ClearLastMood(ctx.Source);
        }
    }

    private static void AutoButtonRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        Rect auto = FirstAutoRect(ctx);
        ClearLastMood(ctx.Source);

        try
        {
            SetButtonOverride(rect => RectMatches(rect, auto));
            SetSliderOverride((rect, value, min, max) => value);
            SetTextFieldOverride((rect, text) => text);
            ctx.Host.DrawChecked(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMood == SqueakMood.Good, "Auto click must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Clear, "Auto click must write SqueakMoodFactor.Clear");
            Assert(!ctx.Source.LastMoodValue.HasValue, "Auto clear must write a null mood value");
        }
        finally
        {
            ClearOverrides();
            ClearLastMood(ctx.Source);
        }
    }

    /// <summary>
    /// Card-level presence and containment at one viewport: the Tuning page carries exactly the mood
    /// controls of ALL FOUR product mood cards (twelve sliders and twelve number fields, twenty-four
    /// minus/plus, and both reset controls per rich fixture mood), all inside the scope-tree card and none
    /// overlapping. A layout branch that omitted a control - the old narrow/stacked shape dropped sliders,
    /// fields and minus/plus - fails the counts here, and a fixture that quietly lost the third or fourth
    /// mood fails the per-card loop below instead of balancing out in the totals.
    /// </summary>
    private static void AssertMoodGeometry(CaptureContext ctx, float viewportWidth)
    {
        string at = " at " + viewportWidth + "px";
        int sliders = MoodCount * ParameterCount;    // four moods x three parameters = 12
        int small = MoodCount * ParameterCount * 2;  // minus + plus per parameter = 24
        Assert(ctx.CardPageRect.width > 100f && ctx.CardPageRect.width <= viewportWidth,
            "scope-tree card must be a real, non-collapsed card" + at + " (got width " + ctx.CardPageRect.width + ")");

        Assert(ctx.CapturedSliderCount == sliders, "captured slider count must be " + sliders + " in Tuning" + at + " (got " + ctx.CapturedSliderCount + ")");
        Assert(ctx.CapturedTextFieldCount == sliders, "captured number-field count must be " + sliders + " in Tuning" + at + " (got " + ctx.CapturedTextFieldCount + ")");
        Assert(ctx.Mood.Sliders.Count == sliders, "each of the " + MoodCount + " rich mood rows must have " + ParameterCount + " sliders (got " + ctx.Mood.Sliders.Count + " total)");
        Assert(ctx.Mood.Fields.Count == sliders, "each of the " + MoodCount + " rich mood rows must have " + ParameterCount + " number fields (got " + ctx.Mood.Fields.Count + " total)");
        Assert(ctx.Mood.SmallButtons.Count == small, "each rich mood row must have 6 minus/plus buttons (got " + ctx.Mood.SmallButtons.Count + " total)");
        Assert(ctx.Mood.AutoButtons.Count == MoodCount, "each rich mood row must have 1 \"reset to default\" control (got " + ctx.Mood.AutoButtons.Count + " total)");
        Assert(ctx.Mood.PresetButtons.Count == MoodCount, "each rich mood row must have 1 \"reset to preset\" control (got " + ctx.Mood.PresetButtons.Count + " total)");

        Assert(ctx.CapturedSliderCount == ctx.Mood.Sliders.Count,
            "all captured sliders must be inside the scope-tree card (no slider outside the card)" + at);
        Assert(ctx.CapturedTextFieldCount == ctx.Mood.Fields.Count,
            "all captured number fields must be inside the scope-tree card (no field outside the card)" + at);

        // Per card, not just as a total: three complete parameter blocks and both reset controls on each
        // of the four product moods.
        List<MoodRowControls> rows = ctx.Mood.Grouped(MoodCount);
        Assert(rows.Count == MoodCount, "expected one control group per mood card" + at + ", got " + rows.Count);
        for (int m = 0; m < rows.Count; m++)
        {
            MoodRowControls row = rows[m];
            Assert(row.Sliders.Count == ParameterCount && row.Fields.Count == ParameterCount
                && row.Minus.Count == ParameterCount && row.Plus.Count == ParameterCount,
                "mood card " + m + " (" + ProductMoods[m] + ")" + at + " must carry " + ParameterCount
                + " complete parameter blocks (minus " + row.Minus.Count + ", plus " + row.Plus.Count
                + ", fields " + row.Fields.Count + ", sliders " + row.Sliders.Count + ")");
            Assert(row.ResetDefault.Count == 1 && row.ResetPreset.Count == 1,
                "mood card " + m + " (" + ProductMoods[m] + ")" + at + " must carry both reset controls (got "
                + row.ResetDefault.Count + "/" + row.ResetPreset.Count + ")");
        }

        List<Rect> allMood = ctx.Mood.All().ToList();
        Assert(allMood.Count == MoodCount * ControlsPerMoodCard,
            "four mood cards must yield " + MoodCount * ControlsPerMoodCard + " mood controls (" + MoodCount
            + " * (3 sliders + 3 fields + 6 small buttons + 2 reset controls)); got "
            + allMood.Count + " = " + ctx.Mood.Sliders.Count + " sliders + " + ctx.Mood.Fields.Count + " fields + "
            + ctx.Mood.SmallButtons.Count + " small + " + ctx.Mood.AutoButtons.Count + " default + " + ctx.Mood.PresetButtons.Count + " preset");

        foreach (Rect local in allMood)
        {
            Rect page = ToPageRect(local, ctx.ContentViewport, ctx.ContentScrollPosition);
            Assert(IsInside(page, ctx.CardPageRect), "mood control must be inside the scope-tree card" + at + ": " + page);
        }

        AssertNoOverlaps(allMood, "mood control rects must not overlap" + at);
        AssertAutoDoesNotOverlapSteppers(ctx.Mood);
    }

    /// <summary>
    /// The two-line parameter template itself, on every mood and every parameter: minus, numeric field and
    /// plus share the first line in that order; the slider is on the line below and starts at the numeric
    /// group's left edge; every slider track has the same x and the same width (within 1px) across
    /// Pitch/Volume/Jitter and across the mood cards.
    /// </summary>
    private static void AssertTemplateAlignment(CaptureContext ctx, float viewportWidth)
    {
        List<MoodRowControls> rows = ctx.Mood.Grouped(MoodCount);
        Assert(rows.Count == MoodCount, "expected " + MoodCount + " mood cards at " + viewportWidth + "px, got " + rows.Count);

        bool first = true;
        float firstSliderX = 0f;
        float firstSliderWidth = 0f;
        for (int m = 0; m < rows.Count; m++)
        {
            MoodRowControls row = rows[m];
            Assert(row.Minus.Count == 3 && row.Plus.Count == 3 && row.Fields.Count == 3 && row.Sliders.Count == 3,
                "mood card " + m + " must carry exactly three parameter blocks at " + viewportWidth + "px (minus "
                + row.Minus.Count + ", plus " + row.Plus.Count + ", fields " + row.Fields.Count + ", sliders " + row.Sliders.Count + ")");

            for (int p = 0; p < 3; p++)
            {
                string where = "mood card " + m + ", parameter " + p + " at " + viewportWidth + "px";
                Rect minus = row.Minus[p];
                Rect field = row.Fields[p];
                Rect plus = row.Plus[p];
                Rect slider = row.Sliders[p];

                Assert(Math.Abs(minus.y - field.y) < 0.01f && Math.Abs(field.y - plus.y) < 0.01f,
                    where + ": minus, numeric field and plus must share the first line");
                Assert(minus.xMax <= field.x + 0.01f && field.xMax <= plus.x + 0.01f,
                    where + ": the first line order must be minus, numeric field, plus");

                Assert(slider.y + Epsilon >= minus.yMax,
                    where + ": the slider must sit on the line below the numeric group (slider y " + slider.y + ", numeric line bottom " + minus.yMax + ")");
                Assert(slider.y > minus.y + 1f, where + ": the slider must not share the numeric line");
                Assert(Math.Abs(slider.x - minus.x) <= 1f,
                    where + ": the slider must start at the numeric group's left edge (slider x " + slider.x + ", minus x " + minus.x + ")");
                Assert(slider.xMax + 0.01f >= plus.xMax,
                    where + ": the slider track must span at least the numeric group");

                if (first)
                {
                    firstSliderX = slider.x;
                    firstSliderWidth = slider.width;
                    first = false;
                }
                else
                {
                    Assert(Math.Abs(slider.x - firstSliderX) < 1f,
                        where + ": every slider must share the same x (got " + slider.x + " vs " + firstSliderX + ")");
                    Assert(Math.Abs(slider.width - firstSliderWidth) < 1f,
                        where + ": every slider track must share the same width within 1px (got " + slider.width + " vs " + firstSliderWidth + ")");
                }
            }
        }

        Assert(!first, "the template alignment check must have measured at least one slider");
    }

    private static void AssertNoOverlaps(IReadOnlyList<Rect> rects, string message)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            for (int j = i + 1; j < rects.Count; j++)
            {
                Assert(!Overlaps(rects[i], rects[j]), message + " (" + rects[i] + " vs " + rects[j] + ")");
            }
        }
    }

    private static void AssertAutoDoesNotOverlapSteppers(MoodControls mood)
    {
        foreach (Rect auto in mood.AutoButtons.Concat(mood.PresetButtons))
        {
            foreach (Rect stepper in mood.Sliders.Concat(mood.Fields).Concat(mood.SmallButtons))
            {
                Assert(!Overlaps(auto, stepper), "reset control must not overlap any stepper control: " + auto + " vs " + stepper);
            }
        }
    }

    private static CaptureContext CreateCaptureContext(
        float viewportWidth = ViewportWidth,
        float viewportHeight = ViewportHeight,
        Program.StubMetrics? metrics = null)
    {
        var source = new RecordingSettingsSource { RichData = true };
        // A lane that installs a translator table must also inject the harness metrics model: this is the
        // same instance the expectations are measured with, so layout and assertion cannot disagree.
        UiHost host = metrics == null ? UsKernelSettingsHost.Create(source) : UsKernelSettingsHost.Create(source, metrics);
        try
        {
            host.Bindings.Invoke("set-tab", "Tuning");
            // 0.4.0 keys scroll positions by element node, so the container must be arranged before its
            // id is addressable; this keeps the capture pinned to the top of the scroll content.
            host.MeasureAndArrange(new Vector2(viewportWidth, viewportHeight));
            Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);

            UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(viewportWidth, viewportHeight));
            Assert(snapshot.RectById.TryGetValue("scope-tree", out Rect cardPage), "snapshot must contain scope-tree in Tuning workspace");

            Rect contentViewport = snapshot.Viewports["content-scroll"];
            Vector2 scroll = Program.ScrollPositionById(host.Session, "content-scroll");
            Rect cardLocal = ToContentLocal(cardPage, contentViewport, scroll);

            var raw = new CapturedRects();
            try
            {
                SetButtonOverride(rect => { raw.Buttons.Add(rect); return false; });
                SetSliderOverride((rect, value, min, max) => { raw.Sliders.Add(rect); return value; });
                SetTextFieldOverride((rect, text) => { raw.TextFields.Add(rect); return text; });
                host.DrawChecked(new Rect(0f, 0f, viewportWidth, viewportHeight));
            }
            finally
            {
                ClearOverrides();
            }

            MoodControls mood = FilterMoodControls(raw, cardLocal);
            return new CaptureContext(
                source,
                host,
                snapshot,
                cardPage,
                cardLocal,
                contentViewport,
                scroll,
                raw.Sliders.Count,
                raw.TextFields.Count,
                mood,
                new Rect(0f, 0f, viewportWidth, viewportHeight),
                metrics);
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    private static MoodControls FilterMoodControls(CapturedRects raw, Rect cardLocal)
    {
        MoodControls mood = new()
        {
            Sliders = raw.Sliders.Where(r => IsInside(r, cardLocal)).ToList(),
            Fields = raw.TextFields.Where(r => IsInside(r, cardLocal)).ToList(),
            SmallButtons = raw.Buttons.Where(r => IsInside(r, cardLocal) && IsSmallButton(r)).ToList(),
        };
        SplitResetButtons(
            raw.Buttons.Where(r => IsInside(r, cardLocal) && IsResetButton(r)).ToList(),
            mood.AutoButtons,
            mood.PresetButtons);
        return mood;
    }

    /// <summary>
    /// Left edge of the parameter content inside the captured card, in the same scroll-content-local space
    /// the UiNative overrides record in: the card body starts one card padding in, and the mood rows one
    /// widget left padding further (both mirrored below, like the reset widths).
    /// </summary>
    private static float ParameterContentX(CaptureContext ctx)
    {
        return ctx.CardLocalRect.x + UsCardLayout.Padding + ParameterContentInset;
    }

    /// <summary>Width one mood row has inside the captured card.</summary>
    private static float MoodRowWidth(CaptureContext ctx)
    {
        return Math.Max(1f, ctx.CardLocalRect.width - UsCardLayout.Padding * 2f - ParameterContentInset * 2f);
    }

    /// <summary>
    /// The observed label band of a captured card: the space between the parameter content's left edge and
    /// the numeric group's left edge (the minus button), which the widget sizes from the resolved localized
    /// labels. Zero when the card moved the label onto its own full-width line. Every parameter must share
    /// the same column, so one measurement serves all three.
    /// </summary>
    private static float ParameterLabelColumn(CaptureContext ctx)
    {
        List<MoodRowControls> rows = ctx.Mood.Grouped(MoodCount);
        Assert(rows.Count == MoodCount, "expected " + MoodCount + " mood cards, got " + rows.Count);
        float first = rows[0].Minus[0].x;
        foreach (MoodRowControls row in rows)
        {
            for (int p = 0; p < 3; p++)
            {
                Assert(Math.Abs(row.Minus[p].x - first) < 0.01f,
                    "every parameter must share the measured label column (minus x " + row.Minus[p].x + " vs " + first + ")");
            }
        }

        return first - ParameterContentX(ctx);
    }

    private static bool IsSmallButton(Rect rect)
    {
        return Math.Abs(rect.width - 20f) <= 2f && rect.height >= 14f && rect.height <= 34f;
    }

    /// <summary>
    /// Widths the two mood reset controls draw at (UsScopeTreeWidget.MoodResetWidthFor): each label's
    /// measured Tiny width plus its 12px side padding, floored at 52. Used only as a sanity bound - the
    /// two controls are told apart by POSITION (the left one on a line is "reset to default"), because
    /// both translated labels are the same length in Chinese and width alone cannot separate them. This
    /// lane installs no translator table, so a label resolves to its key literal.
    /// </summary>
    private static readonly float DefaultResetWidth = Math.Max(
        52f,
        FerriteLib.UiKit.Kernel.VerseFerriteTextMetrics.Instance.MeasureWidth("US.Tuning.ResetToDefault", UiFont.Tiny) + 12f);

    private static readonly float PresetResetWidth = Math.Max(
        52f,
        FerriteLib.UiKit.Kernel.VerseFerriteTextMetrics.Instance.MeasureWidth("US.Tuning.ResetToPreset", UiFont.Tiny) + 12f);

    /// <summary>A drawn reset control: wide enough for either measured label (never the 20px minus/plus).</summary>
    private static bool IsResetButton(Rect rect)
    {
        // Both bounds matter: without the upper one the wide layer-segment buttons (212/425px) would be
        // counted as reset controls and the draw-order pairing would then invent ghost pairs.
        float minWidth = Math.Min(DefaultResetWidth, PresetResetWidth) - 1f;
        float maxWidth = Math.Max(DefaultResetWidth, PresetResetWidth) + 1f;
        return rect.width >= minWidth && rect.width <= maxWidth && rect.height >= 14f && rect.height <= 34f;
    }

    /// <summary>
    /// Splits the reset controls into per-line pairs: on each line the left control is "reset to
    /// default" and the right one is "reset to preset". Identity comes from the layout order the widget
    /// draws, not from a width comparison.
    /// </summary>
    private static void SplitResetButtons(IReadOnlyList<Rect> reset, List<Rect> defaults, List<Rect> presets)
    {
        // Draw order per mood row is always default then preset, on one line when they fit and on two
        // when they do not, so pairing by sorted order survives both shapes (width cannot separate them:
        // the two Chinese labels are the same length).
        List<Rect> sorted = reset.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
        for (int i = 0; i + 1 < sorted.Count; i += 2)
        {
            defaults.Add(sorted[i]);
            presets.Add(sorted[i + 1]);
        }
    }

    private static Rect FirstMinusRect(CaptureContext ctx)
    {
        List<Rect> row1 = ctx.Mood.SmallButtons.OrderBy(r => r.y).ThenBy(r => r.x).Take(6).ToList();
        Assert(row1.Count == 6, "first mood row must have 6 small buttons for minus interaction");
        return row1[0];
    }

    private static Rect FirstPlusRect(CaptureContext ctx)
    {
        List<Rect> row1 = ctx.Mood.SmallButtons.OrderBy(r => r.y).ThenBy(r => r.x).Take(6).ToList();
        Assert(row1.Count == 6, "first mood row must have 6 small buttons for plus interaction");
        return row1[1];
    }

    private static Rect FirstSliderRect(CaptureContext ctx)
    {
        List<Rect> row1 = ctx.Mood.Sliders.OrderBy(r => r.y).ThenBy(r => r.x).Take(3).ToList();
        Assert(row1.Count == 3, "first mood row must have 3 sliders for slider interaction");
        return row1[0];
    }

    private static Rect FirstFieldRect(CaptureContext ctx)
    {
        List<Rect> row1 = ctx.Mood.Fields.OrderBy(r => r.y).ThenBy(r => r.x).Take(3).ToList();
        Assert(row1.Count == 3, "first mood row must have 3 number fields for commit interaction");
        return row1[0];
    }

    /// <summary>
    /// The second reset control ("reset to preset") is a different class of write from the first: it
    /// must reach the host as "reset-mood-to-preset" with the row's mood, never as a field-level
    /// "set-mood-tuning" write. The settings semantics live in the migration lane; this step proves the
    /// binding.
    /// </summary>
    private static void PresetResetRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        List<Rect> presets = ctx.Mood.PresetButtons.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
        Assert(presets.Count == MoodCount, "expected " + MoodCount + " \"reset to preset\" controls before the interaction");
        ctx.Source.LastMoodPresetReset = null;
        ctx.Source.LastMoodPresetResetCount = 0;

        try
        {
            SetButtonOverride(rect => RectMatches(rect, presets[0]));
            SetSliderOverride((rect, value, min, max) => value);
            SetTextFieldOverride((rect, text) => text);
            ctx.Host.DrawChecked(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMoodPresetReset == SqueakMood.Good, "the preset reset must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodPresetResetCount == 1, "the preset reset must route exactly one reset-mood-to-preset action");
            Assert(ctx.Source.LastMoodFactor == null, "the preset reset must not masquerade as a set-mood-tuning write");
        }
        finally
        {
            ClearOverrides();
            ctx.Source.LastMoodPresetReset = null;
            ctx.Source.LastMoodPresetResetCount = 0;
        }
    }

    /// <summary>
    /// Both reset controls must route their typed write at every card width: the narrowest two-column
    /// card, the 800px reference, a mid width and the widest sweep (BH1: every one of these is a nav + centre
    /// row with help retracted, which is the page's only shape at these probes; the retired third column is
    /// gone). The header may keep the controls beside
    /// the mood name or drop them to their own line(s) - only the placement changes, never the semantics.
    /// "Reset to default" is a CLEAR (set-mood-tuning with Clear and a null value); "reset to preset" is a
    /// typed reset-mood-to-preset WRITE. The two-line template must hold at every width as well.
    /// </summary>
    private static void ResetRoutingAcrossCardWidths()
    {
        float[] widths = { NarrowestRowRegimeWidth, ViewportWidth, 1000f, 1920f };
        foreach (float width in widths)
        {
            using CaptureContext ctx = CreateCaptureContext(width, 720f);
            List<Rect> defaults = ctx.Mood.AutoButtons.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
            List<Rect> presets = ctx.Mood.PresetButtons.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
            Assert(defaults.Count == MoodCount && presets.Count == MoodCount,
                width + "px must expose both reset controls per mood card, got " + defaults.Count + "/" + presets.Count);

            foreach (MoodRowControls row in ctx.Mood.Grouped(MoodCount))
            {
                for (int p = 0; p < 3; p++)
                {
                    Assert(row.Sliders[p].y > row.Minus[p].y + 1f && Math.Abs(row.Sliders[p].x - row.Minus[p].x) <= 1f,
                        width + "px: the two-line template must hold for every parameter (slider below its numeric group)");
                }
            }

            try
            {
                // Reset to default: the clear write.
                ClearLastMood(ctx.Source);
                SetButtonOverride(rect => RectMatches(rect, defaults[0]));
                SetSliderOverride((rect, value, min, max) => value);
                SetTextFieldOverride((rect, text) => text);
                ctx.Host.DrawChecked(ctx.Viewport);
                Assert(ctx.Source.LastMood == SqueakMood.Good && ctx.Source.LastMoodFactor == SqueakMoodFactor.Clear
                    && !ctx.Source.LastMoodValue.HasValue,
                    width + "px: the \"reset to default\" control must write a null-valued Clear for the first mood card");

                // Reset to preset: the typed re-write.
                ctx.Source.LastMoodPresetReset = null;
                ctx.Source.LastMoodPresetResetCount = 0;
                SetButtonOverride(rect => RectMatches(rect, presets[0]));
                ctx.Host.DrawChecked(ctx.Viewport);
                Assert(ctx.Source.LastMoodPresetReset == SqueakMood.Good && ctx.Source.LastMoodPresetResetCount == 1,
                    width + "px: the \"reset to preset\" control must route exactly one reset-mood-to-preset action");
            }
            finally
            {
                ClearOverrides();
                ClearLastMood(ctx.Source);
                ctx.Source.LastMoodPresetReset = null;
                ctx.Source.LastMoodPresetResetCount = 0;
            }
        }
    }

    /// <summary>
    /// The production nav button must yield to a covering popup. The old fixture teleported a scope
    /// popup over nav; live anchor reconciliation correctly invalidates that setup. This fixture places
    /// a real core dropdown immediately above the real nav kind, so its actual popup covers the first
    /// nav row. It tests the consumer's context-bearing button call, not the full page's geometry.
    /// The closed-popup positive control proves the same pointer can reach that row. A faithful switch
    /// of UsNavWidget's Button(card, ctx) to Button(card) was run and failed the covered-click assertion.
    /// Fixture placement and the closed-popup positive control are guards, not independent mutation proofs.
    /// </summary>
    private static void CoveredControlYieldsTheClick()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        const string xml = "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">"
            + "<Column Id=\"probe-column\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"probe-dropdown\" Kind=\"input/dropdown\" OptionsBind=\"probe-options\" Height=\"28\" />"
            + "<Widget Id=\"probe-nav\" Kind=\"us/nav\" />"
            + "</Column></UiPage>";
        string activeTab = "Tuning", selected = "one";
        var bindings = new UiBindings();
        bindings.BindValue(UiBindings.ActiveTabKey, () => activeTab, value => activeTab = value);
        bindings.BindAction<string>("set-tab", value => activeTab = value);
        bindings.BindValue("probe-dropdown", () => selected, value => selected = value);
        bindings.BindOptions("probe-options", () => new[] { "one", "two", "three", "four" });
        using UiHost host = new(UsKernelWidgetRegistrar.Scope, UiLayoutManifest.Parse(xml), bindings,
            UsTheme.Surface(), new Program.StubMetrics(), new PopupProbeTranslation());
        Rect viewport = new(0f, 0f, 200f, 600f);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
        var navRects = new List<Rect>();
        try
        {
            SetButtonOverride(rect =>
            {
                if (rect.width >= 100f && rect.height >= 30f) navRects.Add(rect);
                return false;
            });
            host.DrawChecked(viewport);
        }
        finally { ClearOverrides(); }
        Assert(navRects.Count == 5, "the fixture must capture exactly the five production nav rows");
        Rect target = navRects.OrderBy(r => r.y).First();
        host.Session.OpenPopup("probe-dropdown", snapshot.RectById["probe-dropdown"]);
        host.DrawChecked(viewport);
        host.DrawChecked(viewport);
        Assert(TryGetPopupHitLayerFromHost(host, out UiHitLayer layer), "the real dropdown must publish a popup");
        Vector2 pointerWindow = new(target.x + target.width / 2f, target.y + target.height / 2f);
        Assert(layer.Rect.x <= pointerWindow.x && pointerWindow.x <= layer.Rect.xMax
            && layer.Rect.y <= pointerWindow.y && pointerWindow.y <= layer.Rect.yMax,
            "the actual popup must cover the first nav row");
        try
        {
            SetMousePosition(pointerWindow);
            SetButtonOverride(rect => RectMatches(rect, target));
            host.DrawChecked(viewport);
            Assert(activeTab == "Tuning", "a popup-covered nav control must not take the click (ctx overload)");
            host.Session.ClosePopup();
            host.DrawChecked(viewport);
            Assert(activeTab == "Overview", "without the popup the same click must reach the nav row");
        }
        finally
        {
            ClearMousePosition();
            ClearOverrides();
            host.Session.ClosePopup();
        }
    }

    private sealed class PopupProbeTranslation : IUiTranslation
    {
        public string Translate(string key) => key;
        public int TranslationRevision => 0;
    }

    // ------------------------------------------------------------------------------------------------
    // V3 (task-14) - the real page box with its REAL help state, production-shaped layer/domain/mood
    // state, and the inheritance readouts. Every case below sets `help-open` itself and arranges the box
    // that state produces - since BH1 the SAME 760x524 box at both states, because the bottom help panel
    // reserves its declared Height 140 out of the body's leftover instead of taking a 320px column out of
    // the width; nothing is inferred from a viewport number.
    //
    // MUTATION LEDGER (each revert RUN during V3/task-18, all restored, suite green after):
    //  - task-18 A1 reporting the SELECTED layer for all three factors reddens the provenance clause
    //    ("the per-factor provenance readout 'Pitch Global · Volume Global · Jitter Default' must be drawn").
    //  - task-18 A4/A5 the first cut (the action side's Auto word + an unattributed arrow onto the anchor)
    //    reddens that same provenance clause by name; A5 must never come back (one word, two meanings).
    //  - task-18 parity: dropping the new key from the ChineseSimplified table reddens the zero-Verse key
    //    parity gate ("only-in-english={US.Tuning.Source.Default}") - the additive key is required in BOTH.
    //  - R1 restoring the pre-V3 coupling (`scopeRows.Count > 0` in Measure / `anyScopeDrawn` in Draw)
    //    reddens "DEVICE-INPUT NOTE: with NO action-scope rows the mood parameters must still be drawn".
    //  - R2 restoring the pre-V3 fixed 90/96 column pair reddens the column clause by name with the
    //    measured overlap: "label end 180, hint x 184 - the pre-V3 fixed 90/96 pair overlapped by 6px".
    //    That pair is the RETIRED 392px body (what the 320px side column left the centre); the pre-V3
    //    numbers were hardcoded, so the overlap itself reddens the clause at any body - only the printed
    //    pair follows the box, and the single BH1 body (the 524 centre column less the card padding = 500)
    //    measures label end 288 against hint x 304 there.
    //  - R2b restoring the pre-V3 hardcoded hint band (the hint text is never measured) reddens
    //    "the inherited-scope hint '→ Command' must be measured/drawn at this real body".
    //  - R3b dropping the source readout's band (so its text is never laid out) reddens
    //    the per-factor provenance readout clause (the first-cut Auto/anchor text is superseded).
    //  - The popup-containment/click-through half is a GUARD over the shared popup owner rule (mutation-
    //    proven for nav by CoveredControlYieldsTheClick); what is NEW here is measured: the Tuning popup
    //    stays inside the page, covers the next scope trigger, swallows that click, and is not replaced.
    // ------------------------------------------------------------------------------------------------

    // BH1 truth: the help toggle changes NO width. The window has ONE page box - 800 on the game's minimum
    // screen minus 2 x 20 shell chrome = 760 wide, 600 minus the title bar and bottom inset = 524 high - and
    // BOTH help states arrange it. The retired drawer pair (open 984x524 / retracted 760x524) went with the
    // 320px side column, so the case below names its help state by a flag and never by a box.
    private const float V3PageBoxWidth = 760f;   // the one page box, in both help states
    private const float V3PageBoxHeight = 524f;
    /// <summary>What the centre column is: 760 less page-root's Padding 12 x 2 = 736 inner, less the
    /// 200 nav column and the body-row Gap 12 = 524, at BOTH help states.</summary>
    private const float V3CentreColumnWidth = 524f;
    /// <summary>The engine's own scrollbar reservation (UiLayoutEngine.ScrollbarWidth): a Scroll's content is
    /// laid out in its viewport less this, so the mood CARD is 16 narrower than the centre column. The B3
    /// playtest measured the same relation at the old open box (card 400 inside centre 416).</summary>
    private const float ScrollbarReserve = 16f;
    /// <summary>The manifest's declared bottom help panel reservation (help-scroll Height="140").</summary>
    private const float V3HelpPanelHeight = 140f;

    /// <summary>
    /// P1 (V3.1): with the REAL page box and each real help state, the Tuning page arranges its three areas
    /// in the order layer/domain -> Action Scope -> Mood, and the mood area sits below both. The order is
    /// read off the DRAWN rects (the layer segment, the dropdown triggers, the mood sliders/fields), not off
    /// the manifest; the heading roles are a source fact recorded in the class doc of this step (area
    /// headings Small/TextPrimary, sub-group headings Tiny/TextSecondary - UsScopeTreeWidget.cs:251-279).
    /// Also the P5 half: green fit audit at both states in both languages.
    /// </summary>
    private static void TuningAreasAtTheRealBoxes()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            try
            {
                // BH1: both help states arrange the SAME box, so the loop switches one flag and records what
                // the arrangement measured instead of naming a box per state.
                var centreByState = new Dictionary<string, float>();
                foreach ((string state, bool helpOpen) in new[]
                         {
                             ("help open", true),
                             ("help closed", false),
                         })
                {
                    float box = V3PageBoxWidth;
                    string where = "Tuning " + language + ", " + state + " at page box " + box;
                    var metrics = new Program.StubMetrics();
                    var reports = new List<UiOverflowReport>();
                    UiFitAudit.Attach(metrics, reports.Add);
                    UiFitAudit.Enabled = true;
                    try
                    {
                        var source = new RecordingSettingsSource { RichData = true, MirrorTuningWrites = true };
                        source.SetTuningLayer(1);
                        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
                        host.Bindings.Invoke("set-tab", "Tuning");
                        host.Bindings.Set("help-open", helpOpen);
                        host.MeasureAndArrange(new Vector2(box, V3PageBoxHeight));
                        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
                        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(box, V3PageBoxHeight));
                        Assert(snapshot.RectById.TryGetValue("scope-tree", out Rect cardPage),
                            "the Tuning workspace must arrange scope-tree");
                        Assert(snapshot.Viewports.ContainsKey("help-scroll") == helpOpen,
                            "the bottom help panel must be arranged exactly while help is open"
                            + " (help-open really set; BH1 has ONE presentation and no side column)");
                        Assert(snapshot.RectById.ContainsKey("body-row")
                                && snapshot.Viewports.ContainsKey("content-scroll")
                                && snapshot.Viewports.ContainsKey("nav-column"),
                            where + ": BH1 never replaces the body - the body row, the centre content scroll"
                            + " and the nav column stay arranged in BOTH help states");
                        Assert(Math.Abs(snapshot.Viewports["content-scroll"].width - V3CentreColumnWidth) <= 0.5f
                                && Math.Abs(cardPage.width
                                    - (V3CentreColumnWidth - ScrollbarReserve)) <= 0.5f,
                            where + ": the centre viewport is the 524 column and the mood card inside it is"
                            + " that column less the engine's 16px scrollbar reservation, at BOTH help states"
                            + " (help costs the body's height, never its width); viewport="
                            + Num(snapshot.Viewports["content-scroll"].width) + " card=" + Num(cardPage.width));
                        centreByState[state] = cardPage.width;
                        if (helpOpen)
                        {
                            Assert(Math.Abs(snapshot.Viewports["help-scroll"].height - V3HelpPanelHeight) <= 0.5f,
                                where + ": the bottom help panel is arranged at its declared Height 140, got "
                                + Num(snapshot.Viewports["help-scroll"].height));
                        }

                        Rect viewport = snapshot.Viewports["content-scroll"];
                        Vector2 scroll = Program.ScrollPositionById(host.Session, "content-scroll");
                        Rect cardLocal = ToContentLocal(cardPage, viewport, scroll);

                        var raw = new CapturedRects();
                        try
                        {
                            SetButtonOverride(rect => { raw.Buttons.Add(rect); return false; });
                            SetSliderOverride((rect, value, min, max) => { raw.Sliders.Add(rect); return value; });
                            SetTextFieldOverride((rect, text) => { raw.TextFields.Add(rect); return text; });
                            UiFitAudit.Reset();
                            reports.Clear();
                            host.DrawChecked(new Rect(0f, 0f, box, V3PageBoxHeight));
                        }
                        finally { ClearOverrides(); }

                        var buttons = raw.Buttons.Where(r => IsInside(r, cardLocal)).OrderBy(r => r.y).ThenBy(r => r.x).ToList();
                        var sliders = raw.Sliders.Where(r => IsInside(r, cardLocal)).OrderBy(r => r.y).ToList();
                        var fields = raw.TextFields.Where(r => IsInside(r, cardLocal)).OrderBy(r => r.y).ToList();

                        // The dropdown TRIGGERS are the fixed 96px bands (the layer segment is the three
                        // equal-width buttons, 117px each at the single BH1 body; mood minus/plus are 20px):
                        // domain trigger + one per scope row.
                        var triggers = buttons.Where(r => Math.Abs(r.width - 96f) <= 1.5f).OrderBy(r => r.y).ThenBy(r => r.x).ToList();
                        Console.WriteLine("[v3-buttons] " + where + " cardLocal=" + Num(cardLocal.width) + "x" + Num(cardLocal.height)
                            + " layer=" + host.Bindings.Get<int>("tuning-layer")
                            + " domains=" + host.Bindings.Get<IReadOnlyList<TuningDomainOptionView>>("tuning-domains").Count
                            + " scopes=" + host.Bindings.Get<IReadOnlyList<ActionScopeRowView>>("action-scopes").Count
                            + " buttons=" + string.Join(" ", buttons.Select(r => Num(r.width) + "@" + Num(r.y)))
                            + " inCard=" + raw.Buttons.Count(r => IsInside(r, cardLocal)));
                        Assert(triggers.Count == 3,
                            where + ": the Race layer must draw the domain trigger plus the two scope triggers, got " + triggers.Count);

                        // The card header owns the widest band at the top; the layer segment is the three
                        // equal-width buttons under it (117px at the 524-wide card, which since BH1 is the only
                        // card either help state arranges - the retired side column left the open case a 416).
                        var layerBand = buttons
                            .Where(r => r.y < triggers[0].y - 0.5f && r.width > 40f && r.width <= 144f)
                            .ToList();
                        Assert(layerBand.Count == 3,
                            where + ": the layer segment must be drawn above the domain row, got " + layerBand.Count + " layer buttons");
                        Assert(layerBand.Select(r => r.y).Distinct().Count() == 1,
                            where + ": the three layer buttons must share one line");

                        Assert(sliders.Count == MoodCount * ParameterCount,
                            where + ": every parameter of every mood must register a slider, got " + sliders.Count);
                        Assert(fields.Count == MoodCount * ParameterCount,
                            where + ": every parameter of every mood must register a numeric field, got " + fields.Count);
                        Assert(layerBand[0].y < triggers[0].y,
                            where + ": the LAYER area must be drawn above the domain row");
                        Assert(triggers[0].y < triggers[1].y && triggers[1].y <= triggers[2].y,
                            where + ": the DOMAIN trigger must be drawn above the scope triggers");
                        Assert(triggers[2].y < sliders[0].y,
                            where + ": the ACTION SCOPE area must be drawn above the mood parameters");
                        Assert(reports.Count == 0,
                            where + ": the fit audit must report nothing on the Tuning page, got " + DescribeFindings(reports));

                        Console.WriteLine("[v3-areas] " + where + " layerY=" + Num(layerBand[0].y)
                            + " domainY=" + Num(triggers[0].y) + " scopeY=" + Num(triggers[1].y) + "/" + Num(triggers[2].y)
                            + " moodY=" + Num(sliders[0].y) + " card=" + Num(cardPage.width) + "x" + Num(cardPage.height)
                            + " help=" + (helpOpen ? "open" : "retracted") + " fit=" + reports.Count);

                        // P5: a Tuning dropdown opens INSIDE the page from its OWN trigger rect, and the click
                        // lands on whatever the popup covers. MEASURED on this page: the popup is 96x72
                        // anchored under its trigger, so what it covers is the NEXT scope trigger - the mood
                        // cards sit ~120px further down and are NOT underneath (their rects are printed). The
                        // underlying control must not take the click, and the popup must not be replaced.
                        var resets = buttons
                            .Where(r => r.width >= 50f && r.width <= 130f && r.y > triggers[2].y && r.y < sliders[0].y)
                            .OrderBy(r => r.y).ThenBy(r => r.x).ToList();
                        Assert(resets.Count >= 2,
                            where + ": the first mood card must draw its two reset controls, got " + resets.Count);
                        Vector2 noScroll = Vector2.zero;
                        Rect triggerPage = ToPageLocal(triggers[1], viewport, noScroll);
                        Rect coveredPage = ToPageLocal(triggers[2], viewport, noScroll);
                        Rect resetPage = ToPageLocal(resets[0], viewport, noScroll);
                        host.Session.OpenPopup("scope-tree-scope-Eat", triggerPage);
                        host.MeasureAndArrange(new Vector2(box, V3PageBoxHeight));
                        host.DrawChecked(new Rect(0f, 0f, box, V3PageBoxHeight));
                        Assert(Program.TryGetPopupHitLayer(host.Session, out UiHitLayer layer) && layer.IsPopup,
                            where + ": the Eat scope dropdown must publish a POPUP layer");
                        Assert(layer.Rect.x >= -0.5f && layer.Rect.y >= -0.5f
                            && layer.Rect.xMax <= box + 0.5f && layer.Rect.yMax <= V3PageBoxHeight + 0.5f,
                            where + ": the Tuning popup must stay inside the page, got " + DescribeRect(layer.Rect));
                        Vector2 clickPoint = new(coveredPage.x + coveredPage.width * 0.5f, coveredPage.y + coveredPage.height * 0.5f);
                        bool coveredScope = layer.Rect.x <= clickPoint.x && clickPoint.x <= layer.Rect.xMax
                            && layer.Rect.y <= clickPoint.y && clickPoint.y <= layer.Rect.yMax;
                        bool moodUnderPopup = Overlaps(layer.Rect, resetPage);
                        Assert(coveredScope,
                            where + ": the popup must cover the next scope trigger this step clicks through"
                            + " (popup " + DescribeRect(layer.Rect) + ", trigger " + DescribeRect(coveredPage) + ")");
                        try
                        {
                            SetMousePosition(clickPoint);
                            SetButtonOverride(rect => RectMatches(rect, triggers[2]));
                            host.DrawChecked(new Rect(0f, 0f, box, V3PageBoxHeight));
                            Assert(source.LastActionScope == null && source.LastActionKey == null,
                                where + ": a popup-covered scope trigger must NOT take the click (no scope write)");
                            Assert(Program.TryGetPopupHitLayer(host.Session, out UiHitLayer still) && still.IsPopup
                                && Math.Abs(still.Rect.y - layer.Rect.y) < 0.5f,
                                where + ": the covered trigger must not replace the open popup with its own");
                        }
                        finally
                        {
                            ClearMousePosition();
                            ClearOverrides();
                            host.Session.ClosePopup();
                        }

                        Console.WriteLine("[v3-popup] " + where + " popup=" + DescribeRect(layer.Rect)
                            + " coveredScopeTrigger=" + DescribeRect(coveredPage) + " coveredScope=" + coveredScope
                            + " moodResetUnder=" + moodUnderPopup + " moodReset=" + DescribeRect(resetPage)
                            + " clickThroughBlocked=true");
                    }
                    finally
                    {
                        UiFitAudit.Detach();
                        UiFitAudit.Enabled = false;
                    }
                }
                // BH1: the retired open/retracted pair is ONE box, and the claim is measured rather than
                // restated - the card keeps its centre column while the panel is arranged, so help costs the
                // body its HEIGHT (the declared 140) and never a width.
                Assert(Math.Abs(centreByState["help open"] - centreByState["help closed"]) <= 0.5f,
                    "BH1: help-open changes no width - the mood card measures " + Num(centreByState["help open"])
                    + " with the panel against " + Num(centreByState["help closed"]) + " without it");
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    /// <summary>
    /// P2 (V3): the two inheritance readouts are really LAID OUT at the single real body both help states
    /// arrange, and the scope hint's columns stay inside the card without overlapping the action name.
    /// <list type="bullet">
    /// <item>The inheritance HINT: pre-V3 it was suppressed below a 480px element width, which used to be
    /// every real help-open body - the retired 320px side column left the open window a 392. BH1 arranges ONE
    /// body at both help states (the 524 centre column less the card padding = 500), so the lane passes the
    /// production layout function at that measured body and asserts the hint is present inline, inside the
    /// card, and disjoint from the label band. Faithful revert: dropping the hint, or restoring the pre-V3
    /// hardcoded (never measured) band, still reddens the "hint must be laid out" clause by name. The
    /// `ctx.ViewWidth &gt;= 480f` suppression gate is NOT re-cut here and is named rather than deleted: at the
    /// single 500px BH1 body that retired gate no longer bites, so what this clause pins at the real box is
    /// the INLINE choice, not the suppression.</item>
    /// <item>The mood SOURCE readout: the text the widget resolves is captured through the real draw pass's
    /// metrics seam, so the row really drew one - the pre-V3 shape (one number, no source) measures none.</item>
    /// </list>
    /// </summary>
    private static void TuningInheritanceReadoutsAtRealBodies()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            try
            {
                // BH1: one page box, two help states - the flag is the input and the body is what the
                // arrangement measured, recorded per state and compared after the loop.
                var bodyByState = new Dictionary<string, float>();
                foreach ((string state, bool helpOpen) in new[]
                         {
                             ("help open", true),
                             ("help closed", false),
                         })
                {
                    float box = V3PageBoxWidth;
                    // The real body the widget arranges at this box = the drawn card minus the card padding.
                    var probeMetrics = new Program.StubMetrics();
                    float body = TuningBodyWidthAt(helpOpen: helpOpen, metrics: probeMetrics);
                    bodyByState[state] = body;
                    string where = "Tuning " + language + ", " + state + " at page box " + box + " (body " + Num(body) + ")";

                    // (a) the production layout function, at the REAL body, for both scope rows. The fixture's
                    // Eat row OWNS its scope (Scope == EffectiveScope) so it has nothing to explain - the
                    // production predicate (HintTextFor) yields ""; the Draft row inherits and needs the hint.
                    var rowsToProbe = new[]
                    {
                        ("Eat", "US.Action.Eat", "US.Tuning.Scope.Any", true, "US.Tuning.Scope.Any"),
                        ("Draft", "US.Action.Draft", "US.Tuning.Scope.Command", false, "US.Tuning.Scope.Command"),
                    };
                    foreach ((string actionKey, string displayKey, string effectiveKey, bool ownsScope, string ownScopeKey) in rowsToProbe)
                    {
                        bool needsHint = !ownsScope || !string.Equals(effectiveKey, ownScopeKey, StringComparison.Ordinal);
                        string display = table[displayKey];
                        string hint = needsHint ? "→ " + table[effectiveKey] : "";
                        UsScopeTreeWidget.ScopeRowLayout layout = UsScopeTreeWidget.ScopeRowLayoutFor(
                            body, display, hint, probeMetrics);
                        if (!needsHint)
                        {
                            Assert(layout.HintText.Length == 0 && layout.HintBandHeight == 0f,
                                where + ": a row whose own scope IS the effective one has nothing to explain, got '"
                                + layout.HintText + "'");
                            Console.WriteLine("[v3-hint] " + where + " " + actionKey + " owns its scope: no hint (correct)");
                            continue;
                        }

                        Assert(layout.HintText.Length > 0, where + ": the inherited hint must be laid out for " + actionKey);
                        Assert(layout.HintInline,
                            where + ": the hint must stay INLINE beside the dropdown at this real body for " + actionKey);
                        Assert(layout.HintBandHeight >= 14f,
                            where + ": the hint band must hold a Tiny line for " + actionKey + ", got " + layout.HintBandHeight);
                        Assert(layout.LabelWidth >= 120f - 0.01f,
                            where + ": the action name must keep its minimum band next to the hint for " + actionKey
                            + ", got " + Num(layout.LabelWidth));
                        Assert(layout.HintX - layout.LabelWidth >= 10f - 0.01f,
                            where + ": the hint must start after the action-name band PLUS the widget's left"
                            + " padding for " + actionKey + " (label end " + Num(layout.LabelWidth)
                            + ", hint x " + Num(layout.HintX) + ") - the pre-V3 fixed 90/96 pair overlapped by 6px");
                        Assert(layout.HintX + layout.HintWidth <= body + 0.01f,
                            where + ": the hint must stay inside the row for " + actionKey
                            + " (hint " + Num(layout.HintX) + "+" + Num(layout.HintWidth) + " > body " + Num(body) + ")");
                        Console.WriteLine("[v3-hint] " + where + " " + actionKey + " hint='" + layout.HintText
                            + "' inline=" + layout.HintInline + " labelW=" + Num(layout.LabelWidth)
                            + " hintX=" + Num(layout.HintX) + " hintW=" + Num(layout.HintWidth) + " rowH=" + Num(layout.RowHeight));
                    }

                    // (b) the source readout of every mood row, captured from the REAL draw pass.
                    var recorder = new RecordingMetrics();
                    var reports = new List<UiOverflowReport>();
                    UiFitAudit.Attach(recorder, reports.Add);
                    UiFitAudit.Enabled = true;
                    try
                    {
                        var source = new RecordingSettingsSource { RichData = true, MirrorTuningWrites = true };
                        source.SetTuningLayer(1);
                        using UiHost host = UsKernelSettingsHost.Create(source, recorder);
                        host.Bindings.Invoke("set-tab", "Tuning");
                        host.Bindings.Set("help-open", helpOpen);
                        host.MeasureAndArrange(new Vector2(box, V3PageBoxHeight));
                        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
                        UiFitAudit.Reset();
                        reports.Clear();
                        host.MeasureAndArrange(new Vector2(box, V3PageBoxHeight));
                        host.DrawChecked(new Rect(0f, 0f, box, V3PageBoxHeight));

                        // The projection half (task-18): the view rows must CARRY the per-factor supplying
                        // layers the fold resolved, and the reset TARGET separately. A model that stops
                        // projecting them (or a fixture that flattens every row to one source) reddens here.
                        IReadOnlyList<MoodTuningRowView> rowViews = host.Bindings.Get<IReadOnlyList<MoodTuningRowView>>("mood-rows");
                        Assert(rowViews.Any(r => (r.PitchSourceLayer >= 0 || r.VolumeSourceLayer >= 0 || r.JitterSourceLayer >= 0)
                                && (r.PitchSourceLayer < 0 || r.VolumeSourceLayer < 0 || r.JitterSourceLayer < 0)),
                            where + ": at least one row must MIX a supplied factor with a defaulted one");
                        Assert(rowViews.Any(r => r.PitchSourceLayer == 0),
                            where + ": at least one row must report the Global layer as a supplier");
                        Assert(rowViews.Any(r => r.SourceLayerFor(SqueakMoodFactor.Pitch) == 1),
                            where + ": at least one row must report the Race layer as a supplier");
                        Assert(rowViews.Any(r => r.SourceLayerFor(SqueakMoodFactor.Pitch) == 2),
                            where + ": at least one row must report the Xenotype layer as a supplier");

                        // The DRAWN provenance of the mixed fixture row: Pitch Global · Volume Global · Jitter Default.
                        string mixedProvenance = table["US.Tuning.Factor.Pitch"] + " " + table["US.Tuning.Layer.Global"]
                            + " · " + table["US.Tuning.Factor.Volume"] + " " + table["US.Tuning.Layer.Global"]
                            + " · " + table["US.Tuning.Factor.Jitter"] + " " + table["US.Tuning.Source.Default"];
                        Assert(recorder.Measured(mixedProvenance),
                            where + ": the per-factor provenance readout '" + mixedProvenance + "' must be drawn");
                        // The readout must never borrow the ACTION side's "Auto" word (the widget's own
                        // ruling): the composed line above is the only mood-side source wording, and the one
                        // that mentions the defaulted factor says DEFAULT.
                        Assert(reports.Count == 0,
                            where + ": the source readout must not trip the fit audit, got " + DescribeFindings(reports));
                        Console.WriteLine("[v3-source] " + where + " provenance='" + mixedProvenance + "' fit=" + reports.Count);
                    }
                    finally
                    {
                        UiFitAudit.Detach();
                        UiFitAudit.Enabled = false;
                    }

                    // (c) the hint really reaches the metrics seam during a production draw at this box.
                    var hintRecorder = new RecordingMetrics();
                    var hintSource = new RecordingSettingsSource { RichData = true, MirrorTuningWrites = true };
                    hintSource.SetTuningLayer(1);
                    using (UiHost host = UsKernelSettingsHost.Create(hintSource, hintRecorder))
                    {
                        host.Bindings.Invoke("set-tab", "Tuning");
                        host.Bindings.Set("help-open", helpOpen);
                        host.MeasureAndArrange(new Vector2(box, V3PageBoxHeight));
                        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
                        host.MeasureAndArrange(new Vector2(box, V3PageBoxHeight));
                        host.DrawChecked(new Rect(0f, 0f, box, V3PageBoxHeight));
                        // The fixture's Draft row inherits (HasOwnScope=false), so its hint is the one the real
                        // draw pass must lay out at this body. The Eat row owns its scope, so it must NOT
                        // produce a hint at all - both halves are asserted through the real pass + geometry.
                        string hint = "→ " + table["US.Tuning.Scope.Command"];
                        Assert(hintRecorder.Measured(hint),
                            where + ": the inherited-scope hint '" + hint + "' must be measured/drawn at this real body;"
                            + " the pre-V3 shape suppressed it below a 480px element width");
                        Assert(!hintRecorder.Measured("→ " + table["US.Tuning.Scope.Any"]),
                            where + ": the row that OWNS its scope must not draw an inheritance hint");
                    }
                }
                // BH1: the two help states hand the widget the SAME body, because the panel reserves its
                // declared 140 out of the body's HEIGHT and takes nothing off any width on this page.
                Assert(Math.Abs(bodyByState["help open"] - bodyByState["help closed"]) <= 0.01f,
                    "BH1: help-open changes no body width - measured " + Num(bodyByState["help open"])
                    + " with the panel against " + Num(bodyByState["help closed"]) + " without it");
                Assert(Math.Abs(bodyByState["help open"]
                        - (V3CentreColumnWidth - ScrollbarReserve - UsCardLayout.Padding * 2f)) <= 0.5f,
                    "BH1: that one body is the 524 centre column less the card's own padding (the card being"
                    + " the column less the scrollbar reservation), got " + Num(bodyByState["help open"]));
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    /// <summary>
    /// P1 (V3.1) defensive probe: ONE predicate decides the mood area, and that predicate is the mood rows -
    /// not the scope rows. The input here (an EMPTY action-scope list) is DEGENERATE and labelled as such:
    /// production builds the rows from the action definition table, so the list is never empty in game. This
    /// probe exists because two predicates for one decision is the defect class this repo bans - the pre-V3
    /// shape hid the whole mood area behind `scopeRows.Count &gt; 0` / `anyScopeDrawn`, and this reddens by
    /// name if either gate comes back.
    /// </summary>
    private static void MoodAreaSurvivesMissingScopeRows()
    {
        var metrics = new Program.StubMetrics();
        float box = V3PageBoxWidth;
        var source = new RecordingSettingsSource
        {
            RichData = true,
            MirrorTuningWrites = true,
            TuningActionScopes = Array.Empty<ActionScopeRowView>(),
        };
        source.SetTuningLayer(1);
        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
        host.Bindings.Invoke("set-tab", "Tuning");
        // BH1: with help open the panel really is arranged - the mood area has to survive the degenerate
        // scope list AND the panel's declared 140 out of the body's height, at the one 760x524 box.
        host.Bindings.Set("help-open", true);
        host.MeasureAndArrange(new Vector2(box, V3PageBoxHeight));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(box, V3PageBoxHeight));

        var raw = new CapturedRects();
        try
        {
            SetButtonOverride(rect => { raw.Buttons.Add(rect); return false; });
            SetSliderOverride((rect, value, min, max) => { raw.Sliders.Add(rect); return value; });
            SetTextFieldOverride((rect, text) => { raw.TextFields.Add(rect); return text; });
            host.DrawChecked(new Rect(0f, 0f, box, V3PageBoxHeight));
        }
        finally { ClearOverrides(); }

        Rect cardPage = snapshot.RectById["scope-tree"];
        Rect cardLocal = ToContentLocal(cardPage, snapshot.Viewports["content-scroll"], Program.ScrollPositionById(host.Session, "content-scroll"));
        int sliders = raw.Sliders.Count(r => IsInside(r, cardLocal));
        int fields = raw.TextFields.Count(r => IsInside(r, cardLocal));
        int triggers = raw.Buttons.Count(r => IsInside(r, cardLocal) && Math.Abs(r.width - 96f) <= 1.5f);
        Assert(sliders == MoodCount * ParameterCount,
            "DEVICE-INPUT NOTE: with NO action-scope rows the mood parameters must still be drawn"
            + " (" + MoodCount + " moods x " + ParameterCount + " factors); got " + sliders
            + " sliders. The mood area is gated on the mood rows, never on the scope list.");
        Assert(fields == MoodCount * ParameterCount,
            "with NO action-scope rows the mood numeric fields must still be drawn, got " + fields);
        Assert(triggers == 1,
            "with NO action-scope rows the layer-1 DOMAIN trigger is the only 96px trigger left (the scope"
            + " rows are gone): got " + triggers);
        Console.WriteLine("[v3-degenerate] no action-scope rows: moodSliders=" + sliders + " fields=" + fields
            + " triggers=" + triggers);
    }

    /// <summary>
    /// P4 (V3.2): cross-channel writes, asserted on the INCREMENT - reset every recorder, make exactly one
    /// write, and require the OTHER channels' recorders to still be untouched. A selected layer/domain must
    /// not touch a parameter, and a mood write must not move the layer/domain identity.
    /// </summary>
    private static void TuningChannelsDoNotCrossOnIncrements()
    {
        var metrics = new Program.StubMetrics();
        var source = new RecordingSettingsSource { RichData = true, MirrorTuningWrites = true };
        source.SetTuningLayer(1);
        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
        host.Bindings.Invoke("set-tab", "Tuning");
        host.MeasureAndArrange(new Vector2(V3PageBoxWidth, V3PageBoxHeight));

        // (1) a LAYER write touches the layer channel and nothing else.
        ResetTuningRecorders(source);
        host.Bindings.Invoke("set-tuning-layer", 2);
        Assert(source.LastTuningLayer == 2, "the layer write must reach the business boundary");
        Assert(source.LastActionScope == null && source.LastActionKey == null,
            "selecting a layer must not write an action scope");
        Assert(source.LastMood == null && source.LastMoodValue == null && source.LastMoodPresetReset == null,
            "selecting a layer must not write a mood parameter");

        // (2) a DOMAIN write touches the domain channel and nothing else.
        ResetTuningRecorders(source);
        host.Bindings.Invoke("set-tuning-domain", new UniversalSqueaker.UI.UsTuningDomainSelection("testrace", ""));
        Assert(source.LastTuningDomainRace == "testrace", "the domain write must reach the business boundary");
        Assert(source.LastTuningLayer == null, "selecting a domain must not write the layer");
        Assert(source.LastActionScope == null && source.LastMood == null && source.LastMoodValue == null,
            "selecting a domain must not write a parameter");

        // (3) a MOOD write touches the mood channel and nothing else.
        ResetTuningRecorders(source);
        host.Bindings.Invoke("set-mood-tuning", new UsMoodWrite(SqueakMood.Bad, SqueakMoodFactor.Volume, 0.7f));
        Assert(source.LastMood == SqueakMood.Bad && source.LastMoodFactor == SqueakMoodFactor.Volume
            && Math.Abs((source.LastMoodValue ?? -1f) - 0.7f) < 0.001f,
            "the mood write must reach the business boundary with its typed payload");
        Assert(source.LastTuningLayer == null && source.LastTuningDomainRace == null,
            "a mood write must not move the layer/domain identity");
        Assert(source.LastActionScope == null && source.LastActionKey == null,
            "a mood write must not write an action scope");

        // (4) the two reset classes stay different: default = a CLEAR mood write, preset = its own typed action.
        ResetTuningRecorders(source);
        host.Bindings.Invoke("set-mood-tuning", new UsMoodWrite(SqueakMood.Good, SqueakMoodFactor.Clear, null));
        Assert(source.LastMood == SqueakMood.Good && source.LastMoodFactor == SqueakMoodFactor.Clear
            && source.LastMoodValue == null && source.LastMoodPresetReset == null,
            "reset-to-default must remain a CLEAR mood write (null value), not the preset action");
        ResetTuningRecorders(source);
        host.Bindings.Invoke("reset-mood-to-preset", new UsMoodPresetReset(SqueakMood.Good));
        Assert(source.LastMoodPresetReset == SqueakMood.Good && source.LastMoodPresetResetCount == 1,
            "reset-to-preset must remain its own typed write");
        Assert(source.LastMood == null && source.LastMoodValue == null,
            "reset-to-preset must not masquerade as a plain mood write");
        Console.WriteLine("[v3-increments] layer/domain/mood/reset channels are pairwise disjoint on increments");
    }

    /// <summary>
    /// V3 task-18: the PM's counterexample matrix, as REAL cases. The anchor
    /// (<c>sourcePresetDefName</c>) is not provenance - it survives a clear - so the readout must name the
    /// per-factor SUPPLYING LAYER (or the DEFAULT word) and may mention the preset ONLY inside the
    /// explicitly labelled reset-to-preset TARGET clause, and only while that target is usable.
    /// </summary>
    private static void MoodSourceReadoutMatrix()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            try
            {
                string LayerWord(int i) => table[new[] { "US.Tuning.Layer.Global", "US.Tuning.Layer.Race", "US.Tuning.Layer.Xenotype" }[Mathf.Clamp(i, 0, 2)]];
                string def = table["US.Tuning.Source.Default"];
                string prov(int p, int v, int j)
                {
                    return table["US.Tuning.Factor.Pitch"] + " " + (p < 0 ? def : LayerWord(p)) + " · "
                        + table["US.Tuning.Factor.Volume"] + " " + (v < 0 ? def : LayerWord(v)) + " · "
                        + table["US.Tuning.Factor.Jitter"] + " " + (j < 0 ? def : LayerWord(j));
                }

                string labelledTarget = table["US.Tuning.ResetToPreset"] + ": Harness Baseline";
                var anchor = new MoodTuningRecord { sourcePresetDefName = "us.harness.anchor" };

                // (0) fresh import: all three factors supplied by the layer, anchor present -> the target
                // clause names it, explicitly labelled.
                RecordingMetrics fresh = DrawMoodReadout(new[] { 1, 1, 1 }, null, "Harness Baseline", null, 1);
                Assert(fresh.Measured(prov(1, 1, 1)), language + " (0) fresh import: provenance '" + prov(1, 1, 1) + "' must be drawn");
                Assert(fresh.Measured(labelledTarget), language + " (0): the LABELLED reset target must be drawn");

                // (i) THE COUNTEREXAMPLE: local flags cleared, anchor retained, and the real parent value
                // differs from the preset baseline. The readout must NOT claim the preset as the source.
                RecordingMetrics cleared = DrawMoodReadout(new[] { 0, -1, -1 }, anchor, "Harness Baseline", null, 1);
                Assert(cleared.Measured(prov(0, -1, -1)),
                    language + " (i) cleared + retained anchor: provenance '" + prov(0, -1, -1) + "' must be drawn");
                Assert(!cleared.Measured("us.harness.anchor"),
                    language + " (i): the raw anchor token must NEVER be drawn as provenance");
                Assert(cleared.Measured(labelledTarget),
                    language + " (i): the anchor may appear ONLY as the labelled reset target");

                // (ii) mixed factors, no usable target: three different suppliers in one row.
                RecordingMetrics mixed = DrawMoodReadout(new[] { 2, 0, -1 }, null, "", null, 1);
                Assert(mixed.Measured(prov(2, 0, -1)), language + " (ii) mixed: '" + prov(2, 0, -1) + "' must be drawn");
                Assert(!mixed.Measured(table["US.Tuning.ResetToPreset"] + ": "),
                    language + " (ii): no reset-target clause may be drawn without a usable target");

                // (iii) each of Global/Race/Xenotype SELECTED: the reported supplier must not follow the
                // selection (revert A1 relabels all three factors as the selected layer).
                foreach (int selected in new[] { 0, 1, 2 })
                {
                    RecordingMetrics sel = DrawMoodReadout(new[] { 1, 0, -1 }, null, "", null, selected);
                    Assert(sel.Measured(prov(1, 0, -1)),
                        language + " (iii) selected layer " + selected + ": the suppliers must stay the real ones");
                }

                // (iv) no local factor and no anchor: the suppliers are named and no preset appears at all.
                RecordingMetrics noAnchor = DrawMoodReadout(new[] { 2, 2, 2 }, null, "", null, 1);
                Assert(noAnchor.Measured(prov(2, 2, 2)), language + " (iv): provenance must be drawn without any anchor");
                Assert(!noAnchor.Measured(table["US.Tuning.ResetToPreset"] + ": "),
                    language + " (iv): a row with no anchor must not imply a preset target");

                // (v) an anchor whose Def no longer resolves: the target clause must disappear rather than
                // look like a usable preset. The first row gets a UNIQUE label so the assertion is scoped to
                // the row under test (the fixture's third row keeps its own working target).
                string uniqueMissing = table["US.Tuning.ResetToPreset"] + ": Missing Target";
                RecordingMetrics missing = DrawMoodReadout(new[] { 1, 1, 1 }, anchor, "Missing Target",
                    SqueakMoodResetPresetState.PresetMissing, 1);
                Assert(missing.Measured(prov(1, 1, 1)), language + " (v): provenance must still be drawn");
                Assert(!missing.Measured(uniqueMissing),
                    language + " (v): a preset that no longer resolves must NOT be shown as a usable target");

                Console.WriteLine("[v3-matrix] " + language
                    + " (0)='" + prov(1, 1, 1) + "' + '" + labelledTarget + "'"
                    + " (i)='" + prov(0, -1, -1) + "' + '" + labelledTarget + "' anchorDrawn=False"
                    + " (ii)='" + prov(2, 0, -1) + "' target=None"
                    + " (iii)='" + prov(1, 0, -1) + "'"
                    + " (iv)='" + prov(2, 2, 2) + "' target=None"
                    + " (v)='" + prov(1, 1, 1) + "' target=None(presetMissing)");
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    /// <summary>Draws the Tuning page at the one real page box with the help panel open, for one matrix
    /// input, and returns the real draw pass's text recorder (a string the widget never lays out is never
    /// measured).</summary>
    private static RecordingMetrics DrawMoodReadout(
        int[] sourceLayers, MoodTuningRecord? own, string targetLabel, SqueakMoodResetPresetState? row1PresetReset, int selectedLayer)
    {
        var recorder = new RecordingMetrics();
        var source = new RecordingSettingsSource
        {
            RichData = true,
            MirrorTuningWrites = true,
            MoodSourceLayers = sourceLayers,
            MoodOwnRecord = own,
            ResetTargetLabel = targetLabel,
            Row1PresetReset = row1PresetReset,
        };
        source.SetTuningLayer(selectedLayer);
        using UiHost host = UsKernelSettingsHost.Create(source, recorder);
        host.Bindings.Invoke("set-tab", "Tuning");
        host.Bindings.Set("help-open", true);
        host.MeasureAndArrange(new Vector2(V3PageBoxWidth, V3PageBoxHeight));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        host.MeasureAndArrange(new Vector2(V3PageBoxWidth, V3PageBoxHeight));
        host.DrawChecked(new Rect(0f, 0f, V3PageBoxWidth, V3PageBoxHeight));
        return recorder;
    }

    // PM faithful reverts (executed; restored; evidence in the PM V3 delivery): selected-layer suppliers
    // redden the retained-anchor clause; an ungated target reddens the non-Ready clause; key-only label
    // measurement reddens ResolvedActionLabelGrowsTheRow; missing target centering reddens the header
    // containment clause. Def lookup availability is injected input, not a claimed DefDatabase test.
    // PM residual F1: the production method consumes real records; the fixture supplies Def lookup
    // facts only. This is a projection boundary check, not a second implementation of the fold.
    private static void ProductionMoodSourceProjection()
    {
        var global = new MoodTuningRecord { mood = SqueakMood.Good, hasPitchFactor = true, pitchFactor = 0.8f };
        var race = new MoodTuningRecord { mood = SqueakMood.Good, raceDefName = "HarnessRace", sourcePresetDefName = "HarnessPreset" };
        var xeno = new MoodTuningRecord { mood = SqueakMood.Good, raceDefName = "HarnessRace", xenotypeDefName = "HarnessXeno", hasVolumeFactor = true, volumeFactor = 0.6f };
        var ignored = new MoodTuningRecord { mood = SqueakMood.Good, raceDefName = "OtherRace", hasPitchFactor = true, pitchFactor = 2f };
        var records = new[] { global, race, xeno, ignored };
        MoodTuningRowView cleared = ProjectRealMood(records, 1, "HarnessRace", "", true, true);
        Assert(cleared.PitchSourceLayer == 0 && cleared.VolumeSourceLayer == -1 && cleared.JitterSourceLayer == -1,
            "production projection: retained preset anchor must not supply any cleared factor");
        Assert(Math.Abs(cleared.EffectivePitch - 0.8f) < 0.001f && cleared.EffectiveVolume == 1f,
            "production projection: values and supplying layers must agree after clear");
        Assert(cleared.DefaultReset == SqueakMoodResetDefaultState.NoLocalSetting
            && cleared.PresetReset == SqueakMoodResetPresetState.Ready && cleared.ResetPresetTarget == "Resolved preset label",
            "production projection: cleared anchor has an explicitly Ready reset target");
        race.hasPitchFactor = true;
        race.pitchFactor = 1.2f;
        MoodTuningRowView mixed = ProjectRealMood(records, 2, "HarnessRace", "HarnessXeno", true, true);
        Assert(mixed.PitchSourceLayer == 1 && mixed.VolumeSourceLayer == 2 && mixed.JitterSourceLayer == -1,
            "production projection: mixed Race/Xenotype/default suppliers must remain distinct");
        Assert(Math.Abs(mixed.EffectivePitch - 1.2f) < 0.001f && Math.Abs(mixed.EffectiveVolume - 0.6f) < 0.001f,
            "production projection: mixed suppliers must carry their actual values");
        foreach (bool defExists in new[] { false, true })
        {
            MoodTuningRowView unavailable = ProjectRealMood(records, 1, "HarnessRace", "", defExists, false);
            Assert(unavailable.ResetPresetTarget.Length == 0 && unavailable.PresetReset != SqueakMoodResetPresetState.Ready,
                "production projection: non-Ready preset must not expose a reset target");
        }
        race.sourcePresetDefName = "";
        Assert(ProjectRealMood(records, 1, "HarnessRace", "", true, true).ResetPresetTarget.Length == 0,
            "production projection: a local row without an anchor has no reset target");
        Console.WriteLine("[v3-production] real records; clear=0/-1/-1 mixed=1/2/-1; Ready/missing/no-entry/no-anchor PASS");
    }

    private static MoodTuningRowView ProjectRealMood(
        IEnumerable<MoodTuningRecord> records, int layer, string race, string xeno, bool defExists, bool hasEntry)
    {
        MethodInfo project = typeof(VoicePacksPageModel).GetMethod("ProjectMoodTuningRows", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("production projection seam missing");
        Func<string, SqueakMood, string, string, Tuple<bool, bool, string>> resolver =
            (source, mood, r, x) => Tuple.Create(defExists, hasEntry, "Resolved preset label");
        var rows = (IReadOnlyList<MoodTuningRowView>)project.Invoke(null, new object[] { records, layer, race, xeno, resolver })!;
        Assert(rows.Count == ProductMoods.Length, "production projection must retain all four moods");
        return rows.Single(row => row.Mood == SqueakMood.Good);
    }

    // PM residual F3: synthetic long translations distinguish the resolved label from its short key.
    // Input is the real Host at the ONE BH1 page box, 760x524 with the help panel arranged, using the
    // wrap-aware calibrated StubMetrics.
    private static void ResolvedActionLabelGrowsTheRow()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            var table = Program.ReadKeyedTable(language);
            // One draw per resolved label, returning the ARRANGED card so the growth is asserted against the
            // body this box really gives and never against a restated width.
            (float CardWidth, float CardHeight) Draw(string draft)
            {
                var translated = new Dictionary<string, string>(table, StringComparer.Ordinal) { ["US.Action.Draft"] = draft };
                Program.SetTranslatorResolver(translated);
                var source = new RecordingSettingsSource { RichData = true };
                using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
                host.Bindings.Invoke("set-tab", "Tuning");
                host.Bindings.Set("help-open", true);
                Rect card = host.MeasureAndArrange(new Vector2(V3PageBoxWidth, V3PageBoxHeight)).RectById["scope-tree"];
                return (card.width, card.height);
            }
            try
            {
                (float cardWidth, float ordinary) = Draw(table["US.Action.Draft"]);
                string longer = string.Concat(Enumerable.Repeat(language == "English" ? "Long drafted action " : "很长的征召动作名称", 20));
                (float longCardWidth, float expanded) = Draw(longer);
                var ruler = new Program.StubMetrics();
                float oneLine = ruler.MeasureText("x", UiFont.Small, 100000f);
                float body = cardWidth - UsCardLayout.Padding * 2f;
                string hint = "→ " + table["US.Tuning.Scope.Command"];
                float predicted = UsScopeTreeWidget.ScopeRowLayoutFor(body, longer, hint, ruler).RowHeight
                    - UsScopeTreeWidget.ScopeRowLayoutFor(body, table["US.Action.Draft"], hint, ruler).RowHeight;
                // BH1 re-cut of the old flat 100px floor: that number was reachable ONLY because the retired
                // 320px side column left the row a 392 body - the same Chinese label that wrapped into eight
                // lines there wraps into five at the 524 centre column. The business claim is unchanged (the
                // RESOLVED label wraps and a key never does), so it is stated in calibrated text lines read
                // off the arrangement: the card must gain at least one wrapped line, and it must gain what the
                // production row-layout helper predicts at the body this box measures, less one line of seam
                // slack. A narrower drawn row than the probed body only ever ADDS lines, never removes them.
                Assert(Math.Abs(longCardWidth - cardWidth) <= 0.01f,
                    language + ": BH1 - the resolved label changes no width, got " + Num(cardWidth)
                    + " -> " + Num(longCardWidth));
                Assert(expanded > ordinary + oneLine - 0.01f,
                    language + ": resolved long action label must grow the production row by at least one"
                    + " wrapped text line (" + Num(oneLine) + "), key-only measurement cannot; got "
                    + Num(ordinary) + " -> " + Num(expanded));
                Assert(expanded >= ordinary + predicted - oneLine,
                    language + ": the drawn growth " + Num(expanded - ordinary) + " must follow the row layout"
                    + " the production helper predicts at the measured body " + Num(body) + " (" + Num(predicted) + ")");
                Console.WriteLine("[v3-resolved-row] " + language + " card=" + Num(cardWidth) + " body=" + Num(body)
                    + " normal=" + Num(ordinary) + " long=" + Num(expanded) + " predictedWrapGrowth=" + Num(predicted));
            }
            finally { Program.SetTranslatorResolver(null); }
        }
    }

    private static void ResetTargetStaysInTheHeader()
    {
        // Numeric input models a wrapped inline header: 21px name, 72px provenance, 54px target,
        // two 2px gaps. Execute the same positioning helper DrawMoodRow consumes, not a copied formula.
        Type layoutType = typeof(UsScopeTreeWidget).GetNestedType("MoodRowsLayout", BindingFlags.NonPublic)!;
        object layout = Activator.CreateInstance(layoutType)!;
        layoutType.GetField("HeaderInline")!.SetValue(layout, true);
        foreach (var field in new Dictionary<string, float> { ["HeaderHeight"] = 151f, ["NameBandHeight"] = 21f,
            ["SourceGap"] = 2f, ["SourceBandHeight"] = 72f, ["TargetGap"] = 2f, ["TargetBandHeight"] = 54f })
            layoutType.GetField(field.Key)!.SetValue(layout, field.Value);
        MethodInfo top = typeof(UsScopeTreeWidget).GetMethod("MoodHeaderLeftTopFor", BindingFlags.NonPublic | BindingFlags.Static)!;
        float offset = (float)top.Invoke(null, new[] { layout })!;
        Assert(offset >= 0f && offset + 151f <= 151.01f,
            "inline mood header: centering must include target band and keep it above parameters");
        Console.WriteLine("[v3-header] wrapped inline stack=151 header=151 offset=" + Num(offset));
    }

    private static void ResetTuningRecorders(RecordingSettingsSource source)
    {
        source.LastTuningLayer = null;
        source.LastTuningDomainRace = null;
        source.LastTuningDomainTarget = null;
        source.LastActionKey = null;
        source.LastActionScope = null;
        source.LastMood = null;
        source.LastMoodFactor = null;
        source.LastMoodValue = null;
        source.LastMoodPresetReset = null;
        source.LastMoodPresetResetCount = 0;
    }

    /// <summary>The action display key the widget resolves for one fixture action row.</summary>
    private static string ActionDisplayKey(string actionKey)
    {
        return actionKey == "Draft" ? "US.Action.Draft" : "US.Action.Eat";
    }

    /// <summary>
    /// The widget BODY width for one help state: measured, not derived - the card is arranged at the one BH1
    /// page box (help never changes it) with the state the product uses, and the body is the card minus the
    /// card padding.
    /// </summary>
    private static float TuningBodyWidthAt(bool helpOpen, Program.StubMetrics metrics)
    {
        var source = new RecordingSettingsSource { RichData = true, MirrorTuningWrites = true };
        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
        host.Bindings.Invoke("set-tab", "Tuning");
        host.Bindings.Set("help-open", helpOpen);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(V3PageBoxWidth, V3PageBoxHeight));
        Rect card = snapshot.RectById["scope-tree"];
        return Math.Max(1f, card.width - UsCardLayout.Padding * 2f);
    }

    /// <summary>
    /// The inherited-scope hint and the mood source readout are observed through the EXISTING
    /// <see cref="RecordingMetrics"/> seam (the same instrument the header-resolution step uses): a string the
    /// widget never lays out is never measured, so the pre-V3 suppression of the hint is observable here - it
    /// simply never appears.
    /// </summary>
    private static string DescribeFindings(List<UiOverflowReport> reports)
    {
        if (reports.Count == 0) return "none";
        var text = new System.Text.StringBuilder();
        foreach (UiOverflowReport report in reports)
        {
            text.Append("[").Append(report.ElementPath).Append(" ").Append(report.Axis)
                .Append(" needs ").Append(report.Needed).Append(" has ").Append(report.Available).Append("]");
        }

        return text.ToString();
    }

    private static string DescribeRect(Rect rect)
    {
        return "(" + Num(rect.x) + "," + Num(rect.y) + "," + Num(rect.width) + "," + Num(rect.height) + ")";
    }

    /// <summary>Content-local (the space the UiNative overrides record in) to page space: the inverse of
    /// <see cref="ToContentLocal"/> for a scroll position of zero, which is where these steps pin the scroll.</summary>
    private static Rect ToPageLocal(Rect contentLocal, Rect viewport, Vector2 scroll)
    {
        return new Rect(contentLocal.x + viewport.x - scroll.x, contentLocal.y + viewport.y - scroll.y,
            contentLocal.width, contentLocal.height);
    }

    private static string Num(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>
    /// V1: the navigation ENCLOSURE. The probe host carries the nav alone UNDER the flat scope - which is
    /// what the real column declares - so the two halves are independent: the scope keeps flattening the
    /// column, and every card must still paint its own enclosing surface and edge at its OWN rect.
    /// <para>
    /// Original demand this replaces nothing of: the nav geometry lane still asserts the five cards' equal
    /// bounds and the fixed column, and this step adds the SURFACE half those bounds could never observe.
    /// PM faithfully restored the committed pre-V1 nav source and observed <b>EveryCardIsEnclosed</b>
    /// fail. <b>SelectedAndOrdinaryDiffer</b> is a state distinction guard, not a separately executed
    /// mutation proof. Each run clears the paint records before observing its own draw.
    /// </para>
    /// </summary>
    private static void EveryNavCardPaintsItsOwnEnclosure()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        const string xml = "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">"
            + "<Styles Schema=\"1\" Density=\"regular\">"
            + "<Scheme Name=\"us-flat-panel\">"
            + "<Color Token=\"WorkspacePlane\" Value=\"#191612\" />"
            + "<Color Token=\"Raised\" Value=\"#191612\" />"
            + "<Color Token=\"RaisedBorder\" Value=\"#191612\" />"
            + "<Color Token=\"Hover\" Value=\"#242019\" />"
            + "<Color Token=\"HoverBorder\" Value=\"#242019\" />"
            + "<Color Token=\"Selected\" Value=\"#3a311f\" />"
            + "<Color Token=\"SelectedBorder\" Value=\"#3a311f\" />"
            + "</Scheme></Styles>"
            + "<Column Id=\"probe-column\" Padding=\"0\" Gap=\"0\" Scheme=\"us-flat-panel\">"
            + "<Widget Id=\"probe-nav\" Kind=\"us/nav\" />"
            + "</Column></UiPage>";
        string activeTab = "Tuning";
        var bindings = new UiBindings();
        bindings.BindValue(UiBindings.ActiveTabKey, () => activeTab, value => activeTab = value);
        bindings.BindAction<string>("set-tab", value => activeTab = value);
        using UiHost host = new(UsKernelWidgetRegistrar.Scope, UiLayoutManifest.Parse(xml), bindings,
            UsTheme.Surface(), new Program.StubMetrics(), new PopupProbeTranslation());
        Rect viewport = new(0f, 0f, 200f, 600f);
        host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));

        IList rects = RecordedStub("DrawBoxSolidRects");
        IList colours = RecordedStub("DrawBoxSolidColors");
        rects.Clear();
        colours.Clear();
        var cards = new List<Rect>();
        try
        {
            SetButtonOverride(rect =>
            {
                if (rect.width >= 100f && rect.height >= 30f) cards.Add(rect);
                return false;
            });
            host.DrawChecked(viewport);
        }
        finally
        {
            ClearOverrides();
        }

        Assert(cards.Count == 5, "the probe must capture exactly the five nav cards, got " + cards.Count);
        cards = cards.OrderBy(r => r.y).ToList();

        Assert(rects.Count == colours.Count,
            "the stub's two solid recorders must stay in step: " + rects.Count + " rects vs " + colours.Count);

        // The SELECTED card is the one whose fill is the palette's Selected tone; the ordinary ones take the
        // raised plane. Asking which enclosures were painted is the whole assertion: the pre-V1 neutral path
        // inherited an invisible frame, so an ordinary card painted no edge at all.
        var enclosures = new List<Color>();
        for (int i = 0; i < cards.Count; i++)
        {
            Color? edge = EdgeOf(cards[i], rects, colours);
            Assert(edge.HasValue,
                "EveryCardIsEnclosed: nav card " + i + " painted no enclosing surface at "
                + cards[i].x + "," + cards[i].y + " " + cards[i].width + "x" + cards[i].height);
            enclosures.Add(edge!.Value);
        }

        List<Color> distinct = enclosures.Distinct().ToList();
        Assert(distinct.Count == 2,
            "SelectedAndOrdinaryDiffer: the five cards must paint exactly two enclosures - the selected tone"
            + " and the ordinary one - got " + distinct.Count + " (" + string.Join(", ",
                distinct.Select(c => "#" + Channel(c.r) + Channel(c.g) + Channel(c.b))) + ")");
    }

    /// <summary>
    /// The enclosure edge a card actually painted: the first recorded solid whose rect IS the card's own
    /// outline. <see cref="UiThemeDraw.Surface"/> paints the fill over the whole rect and then four 1px
    /// strips in the edge colour, so a matching rect with a DIFFERENT colour on a sibling strip is exactly
    /// "this card drew a box". A card that painted only its fill (no edge) answers null.
    /// </summary>
    private static Color? EdgeOf(Rect card, IList rects, IList colours)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i] is not Rect rect || !RectMatches(rect, card)) continue;
            if (colours[i] is not Color fill) continue;

            for (int j = 0; j < rects.Count; j++)
            {
                if (rects[j] is not Rect strip || colours[j] is not Color edge) continue;
                bool isCardEdge = Math.Abs(strip.x - card.x) <= 0.01f
                    && Math.Abs(strip.y - card.y) <= 0.01f
                    && Math.Abs(strip.height - 1f) <= 0.01f
                    && Math.Abs(strip.width - card.width) <= 0.01f;
                if (!isCardEdge) continue;
                if (!SameColour(edge, fill)) return edge;
            }
        }

        return null;
    }

    private static bool SameColour(Color a, Color b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    private static string Channel(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 255f).ToString("X2");
    }

    private static IList RecordedStub(string fieldName)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "the runtime stub does not record '" + fieldName + "'; this lane cannot observe a draw and"
                + " must not pass silently");
        }

        return field.GetValue(null) as IList
            ?? throw new InvalidOperationException("the stub's '" + fieldName + "' recorder is not a list");
    }

    private static bool TryGetPopupHitLayerFromHost(UiHost host, out UiHitLayer layer)
    {
        layer = default;
        foreach (UiHitLayer candidate in host.Session.HitLayers)
        {
            if (candidate.IsPopup)
            {
                layer = candidate;
                return true;
            }
        }
        return false;
    }

    private static void SetMousePosition(Vector2 position)
    {
        SetField(DebugMousePositionField, position);
        SetField(DebugMousePositionEnabledField, true);
    }

    private static void ClearMousePosition()
    {
        SetField(DebugMousePositionEnabledField, false);
    }

    private static FieldInfo DebugMousePositionField => RequireField("DebugMousePosition", typeof(Vector2));
    private static FieldInfo DebugMousePositionEnabledField => RequireField("DebugMousePositionEnabled", typeof(bool));

    private static Rect FirstAutoRect(CaptureContext ctx)
    {
        List<Rect> autos = ctx.Mood.AutoButtons.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
        Assert(autos.Count == MoodCount, "expected " + MoodCount + " Auto buttons before Auto interaction");
        return autos[0];
    }

    private static bool RectMatches(Rect a, Rect b)
    {
        return Math.Abs(a.x - b.x) <= RectMatchEpsilon
            && Math.Abs(a.y - b.y) <= RectMatchEpsilon
            && Math.Abs(a.width - b.width) <= RectMatchEpsilon
            && Math.Abs(a.height - b.height) <= RectMatchEpsilon;
    }

    private static bool Overlaps(Rect a, Rect b)
    {
        return a.x < b.xMax - Epsilon
            && b.x < a.xMax - Epsilon
            && a.y < b.yMax - Epsilon
            && b.y < a.yMax - Epsilon;
    }

    private static bool IsInside(Rect inner, Rect outer)
    {
        return inner.x >= outer.x - Epsilon
            && inner.y >= outer.y - Epsilon
            && inner.xMax <= outer.xMax + Epsilon
            && inner.yMax <= outer.yMax + Epsilon;
    }

    private static Rect ToContentLocal(Rect pageRect, Rect contentViewport, Vector2 scroll)
    {
        return new Rect(
            pageRect.x - contentViewport.x + scroll.x,
            pageRect.y - contentViewport.y + scroll.y,
            pageRect.width,
            pageRect.height);
    }

    private static Rect ToPageRect(Rect localRect, Rect contentViewport, Vector2 scroll)
    {
        return new Rect(
            localRect.x + contentViewport.x - scroll.x,
            localRect.y + contentViewport.y - scroll.y,
            localRect.width,
            localRect.height);
    }

    private static void SetButtonOverride(Func<Rect, bool> value)
    {
        SetField(ButtonOverrideField, value);
    }

    private static void SetSliderOverride(Func<Rect, float, float, float, float> value)
    {
        SetField(SliderOverrideField, value);
    }

    private static void SetTextFieldOverride(Func<Rect, string, string> value)
    {
        SetField(TextFieldOverrideField, value);
    }

    private static void ClearOverrides()
    {
        SetField(ButtonOverrideField, null);
        SetField(SliderOverrideField, null);
        SetField(TextFieldOverrideField, null);
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
                "REFLECTION BLOCKER: cannot set UiNative." + field.Name + " on net472. "
                + "Add InternalsVisibleTo(\"UniversalSqueakerKernelHostTests\") in "
                + "Source/FerriteLib.UiKit/Properties/AssemblyInfo.cs, rebuild, then rerun. "
                + "Do not modify production files in this step.",
                ex);
        }
    }

    private static FieldInfo RequireField(string name, Type fieldType)
    {
        FieldInfo? field = typeof(UiNative).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "REFLECTION BLOCKER: UiNative." + name + " is not accessible via reflection on net472. "
                + "Add InternalsVisibleTo(\"UniversalSqueakerKernelHostTests\") in "
                + "Source/FerriteLib.UiKit/Properties/AssemblyInfo.cs, rebuild, then rerun. "
                + "Do not modify production files in this step.");
        }

        if (field.FieldType != fieldType)
        {
            throw new InvalidOperationException(
                "UiNative." + name + " has unexpected type " + field.FieldType + "; expected " + fieldType);
        }

        return field;
    }

    private static void ClearLastMood(RecordingSettingsSource source)
    {
        source.LastMood = null;
        source.LastMoodFactor = null;
        source.LastMoodValue = null;
    }

    private static bool FloatEquals(float? actual, float expected)
    {
        return actual.HasValue && Math.Abs(actual.Value - expected) < 0.001f;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// An <see cref="ITextMetrics"/> that delegates measurement to the harness stub and records every
    /// string handed to it. The header-resolution step uses it as the ruler the layout must consult: a mood
    /// name appears here only if the widget measured it, which is how "the header resolves through
    /// US.Mood.&lt;Mood&gt;" is observed instead of inferred from the widget source.
    /// </summary>
    private sealed class RecordingMetrics : ITextMetrics
    {
        private readonly Program.StubMetrics inner = new();
        private readonly HashSet<string> measured = new(StringComparer.Ordinal);

        public bool Measured(string text) => measured.Contains(text ?? "");

        public float MeasureText(string text, UiFont font, float width)
        {
            string value = text ?? "";
            measured.Add(value);
            return inner.MeasureText(value, font, width);
        }

        public float MeasureWidth(string text, UiFont font)
        {
            string value = text ?? "";
            measured.Add(value);
            return inner.MeasureWidth(value, font);
        }
    }

    private sealed class CapturedRects
    {
        public List<Rect> Buttons { get; } = new();
        public List<Rect> Sliders { get; } = new();
        public List<Rect> TextFields { get; } = new();
    }

    private sealed class MoodControls
    {
        public List<Rect> Sliders { get; set; } = new();
        public List<Rect> Fields { get; set; } = new();
        public List<Rect> SmallButtons { get; set; } = new();
        public List<Rect> AutoButtons { get; set; } = new();
        public List<Rect> PresetButtons { get; set; } = new();

        public IEnumerable<Rect> All()
        {
            foreach (Rect rect in Sliders) yield return rect;
            foreach (Rect rect in Fields) yield return rect;
            foreach (Rect rect in SmallButtons) yield return rect;
            foreach (Rect rect in AutoButtons) yield return rect;
            foreach (Rect rect in PresetButtons) yield return rect;
        }

        /// <summary>
        /// The captured controls grouped into mood cards and, inside each card, into the three parameter
        /// blocks in draw order (Pitch, Volume, Jitter). Draw order is the widget's contract: within a
        /// parameter the minus is drawn before the plus, and the three parameter blocks run top to bottom.
        /// </summary>
        public List<MoodRowControls> Grouped(int moodCount)
        {
            List<Rect> buttons = SmallButtons.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
            List<Rect> fields = Fields.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
            List<Rect> sliders = Sliders.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
            List<Rect> defaults = AutoButtons.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
            List<Rect> presets = PresetButtons.OrderBy(r => r.y).ThenBy(r => r.x).ToList();

            var rows = new List<MoodRowControls>();
            for (int m = 0; m < moodCount; m++)
            {
                var row = new MoodRowControls();
                for (int p = 0; p < 3; p++)
                {
                    int button = (m * 3 + p) * 2;
                    row.Minus.Add(buttons[button]);
                    row.Plus.Add(buttons[button + 1]);
                    row.Fields.Add(fields[m * 3 + p]);
                    row.Sliders.Add(sliders[m * 3 + p]);
                }

                // A capture whose injected metrics model differs from the library's static reset-width
                // probe cannot tell the reset controls apart by width. Grouping must not require them;
                // the reset presence assertion lives in EveryParameterRegistersEveryControl.
                if (m < defaults.Count) row.ResetDefault.Add(defaults[m]);
                if (m < presets.Count) row.ResetPreset.Add(presets[m]);
                rows.Add(row);
            }

            return rows;
        }
    }

    /// <summary>One mood card's controls, indexed by parameter (0 = Pitch, 1 = Volume, 2 = Jitter).</summary>
    private sealed class MoodRowControls
    {
        public List<Rect> Minus { get; } = new();
        public List<Rect> Plus { get; } = new();
        public List<Rect> Fields { get; } = new();
        public List<Rect> Sliders { get; } = new();
        public List<Rect> ResetDefault { get; } = new();
        public List<Rect> ResetPreset { get; } = new();
    }

    private sealed class CaptureContext : IDisposable
    {
        public RecordingSettingsSource Source { get; }
        public UiHost Host { get; }
        public UiLayoutSnapshot Snapshot { get; }
        public Rect CardPageRect { get; }
        public Rect CardLocalRect { get; }
        public Rect ContentViewport { get; }
        public Vector2 ContentScrollPosition { get; }
        public int CapturedSliderCount { get; }
        public int CapturedTextFieldCount { get; }
        public MoodControls Mood { get; }
        /// <summary>Viewport this pass drew at.</summary>
        public Rect Viewport { get; }
        /// <summary>Metrics model the host measured with when the lane injected one (localized steps).</summary>
        public Program.StubMetrics? Metrics { get; }

        public CaptureContext(
            RecordingSettingsSource source,
            UiHost host,
            UiLayoutSnapshot snapshot,
            Rect cardPageRect,
            Rect cardLocalRect,
            Rect contentViewport,
            Vector2 contentScrollPosition,
            int capturedSliderCount,
            int capturedTextFieldCount,
            MoodControls mood,
            Rect viewport,
            Program.StubMetrics? metrics)
        {
            Source = source;
            Host = host;
            Snapshot = snapshot;
            CardPageRect = cardPageRect;
            CardLocalRect = cardLocalRect;
            ContentViewport = contentViewport;
            ContentScrollPosition = contentScrollPosition;
            CapturedSliderCount = capturedSliderCount;
            CapturedTextFieldCount = capturedTextFieldCount;
            Mood = mood;
            Viewport = viewport;
            Metrics = metrics;
        }

        public void Dispose()
        {
            Host.Dispose();
        }
    }
}
