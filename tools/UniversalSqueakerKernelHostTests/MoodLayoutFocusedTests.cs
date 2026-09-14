using System;
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
    // Below the manifest's 500px three-column breakpoint the body row resolves to a stacked column, so
    // this viewport (480 - 24 page padding = 456 inner < 500) exercises the narrow page shape; the card
    // itself is then the full column width.
    private const float NarrowViewportWidth = 480f;
    // A viewport comfortably inside the three-column row regime (724 inner 700 >= the row's 500
    // breakpoint) - the narrow-card regime this lane exists for.
    private const float NarrowestThreeColumnWidth = 724f;
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
        Step("narrowest three-column card keeps every control", () => TwoLineMoodTemplateGeometry(NarrowestThreeColumnWidth, 720f));
        Step("736px three-column card keeps the template", () => TwoLineMoodTemplateGeometry(736f, 720f));
        Step("mood value round-trip clamps to the factor ranges", MoodValueRoundTripClampsToFactorRanges);
        Step("minus click routes typed set-mood-tuning", MinusClickRoutesTypedMoodTuning);
        Step("plus click routes typed set-mood-tuning", PlusClickRoutesTypedMoodTuning);
        Step("slider change routes typed set-mood-tuning", SliderChangeRoutesTypedMoodTuning);
        Step("number commit routes typed set-mood-tuning", NumberCommitRoutesTypedMoodTuning);
        Step("auto button routes typed set-mood-tuning", AutoButtonRoutesTypedMoodTuning);
        Step("preset reset routes typed reset-mood-to-preset", PresetResetRoutesTypedMoodTuning);
        Step("reset controls route at every card width", ResetRoutingAcrossCardWidths);
        Step("a popup-covered reset control yields the click", CoveredControlYieldsTheClick);
        Step("a checkbox-row press is decided by one control and flips the value once", CheckboxRowPressDecidesOnce);

        Console.WriteLine("MoodLayoutFocusedTests ALL PASS");
        return 0;
    }

    /// <summary>
    /// The checkbox row's click contract (structure from task-100, assertion from task-103). Two facts,
    /// failing for different reasons:
    /// <list type="number">
    /// <item>THE DRAW FACT. The row's hit band stops where the checkbox slot starts, so the two button
    /// rects the row registers are disjoint and one press can only be decided by one of them. This is what
    /// the truncation in the widgets is for, and it is measured on the recorded button rects, not inferred
    /// from the source.</item>
    /// <item>THE BEHAVIOUR FACT. One press on either rect advances the session's revision clock by exactly
    /// one write and flips the value once. Two overlapping buttons both report the same press in IMGUI, so
    /// a missing short-circuit would show up here as two writes and a value back where it started.</item>
    /// </list>
    /// </summary>
    private static void CheckboxRowPressDecidesOnce()
    {
        const float Width = ViewportWidth;
        const float Height = 900f;
        var source = new RecordingSettingsSource { RichData = true };
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

            List<Rect> slots = raw.Buttons
                .Where(r => IsInside(r, card)
                    && Math.Abs(r.width - UsKernelDraw.CheckboxHit) <= 0.5f
                    && Math.Abs(r.height - UsKernelDraw.CheckboxHit) <= 0.5f)
                .OrderBy(r => r.y)
                .ToList();
            Assert(slots.Count > 0, "the basic-tuning card must register at least one 24px checkbox slot");

            // THE DRAW FACT, stated as the property itself: no row band in this card may reach into any
            // checkbox slot. Nothing is assumed about which slot belongs to which row - a widget that
            // restores a full-width row button fails here whichever row it is, and the band list is built
            // from what the card actually registered (bands are 24 tall and wider than a slot).
            foreach (Rect slotRect in slots)
            {
                List<Rect> sameRow = RowBandsFor(raw.Buttons, slotRect);
                Assert(sameRow.Count == 1,
                    "every checkbox slot must own exactly one row hit band, got " + sameRow.Count + " for the slot at y " + slotRect.y);
                Assert(Math.Abs(sameRow[0].xMax - slotRect.x) <= 0.01f,
                    "and that band must end exactly where the slot starts (band ends at " + sameRow[0].xMax + ", slot starts at " + slotRect.x + ")");
            }

            // THE BEHAVIOUR FACT. The egg row is the widget's first checkbox row (it is drawn first), and
            // that is verified by what the press does rather than assumed: if this row is not the egg row,
            // allow-eggs does not move and the lane fails with a name.
            Rect slot = slots[0];
            Rect band = RowBandsFor(raw.Buttons, slot)[0];

            // (a) A press that reaches BOTH the slot and the band - the overlapping-button case IMGUI hands
            //     to both controls - must still be exactly one write and one flip. That is the short-circuit's
            //     whole job; without it the value would end where it started after two writes.
            // The observable is the source write (UsKernelSettingsHost wires toggle-egg to
            // SetEasterEggs and bumps the revision clock), not the cached value binding.
            bool? before = source.LastEasterEggs;
            int revisionBefore = host.Session.ContentRevision;
            try
            {
                SetButtonOverride(rect => Math.Abs(rect.y - slot.y) < 0.5f && Math.Abs(rect.height - slot.height) < 0.5f);
                host.DrawChecked(new Rect(0f, 0f, Width, Height));
            }
            finally
            {
                ClearOverrides();
            }

            Assert(source.LastEasterEggs != before, "a press both controls report must flip the value exactly once");
            Assert(host.Session.ContentRevision == revisionBefore + 1,
                "and it must be exactly one write, not two (revision " + revisionBefore + " -> " + host.Session.ContentRevision + ")");

            // (b) A press on the row band only: the row decides and still writes once.
            int rowRevisionBefore = host.Session.ContentRevision;
            try
            {
                Rect rowOnly = band;
                SetButtonOverride(rect => Math.Abs(rect.x - rowOnly.x) < 0.5f && Math.Abs(rect.width - rowOnly.width) < 0.5f && Math.Abs(rect.y - rowOnly.y) < 0.5f);
                host.DrawChecked(new Rect(0f, 0f, Width, Height));
            }
            finally
            {
                ClearOverrides();
            }

            Assert(source.LastEasterEggs != before, "a press on the row band must flip it again, exactly once");
            Assert(host.Session.ContentRevision == rowRevisionBefore + 1,
                "and that press must be exactly one write too (revision " + rowRevisionBefore + " -> " + host.Session.ContentRevision + ")");
        }
        finally
        {
            host.Dispose();
        }
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
        using CaptureContext ctx = CreateCaptureContext(NarrowestThreeColumnWidth, 720f);
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
    /// Both reset controls must route their typed write at every card width: the narrowest three-column
    /// card, the 800px reference, a mid width and the wide layout. The header may keep the controls beside
    /// the mood name or drop them to their own line(s) - only the placement changes, never the semantics.
    /// "Reset to default" is a CLEAR (set-mood-tuning with Clear and a null value); "reset to preset" is a
    /// typed reset-mood-to-preset WRITE. The two-line template must hold at every width as well.
    /// </summary>
    private static void ResetRoutingAcrossCardWidths()
    {
        float[] widths = { NarrowestThreeColumnWidth, ViewportWidth, 1000f, 1920f };
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
    /// The ctx-contract assertion: with a popup covering a reset control, aiming a click at that control
    /// must not fire it - the protected overload asks the hit stack first and returns false without ever
    /// reaching the native button. The positive control (popup closed, same click, same pointer) proves the
    /// assertion can go the other way, so this step dies the moment a call site falls back to the
    /// context-free overload.
    /// </summary>
    private static void CoveredControlYieldsTheClick()
    {
        using CaptureContext ctx = CreateCaptureContext();

        // The covered control must belong to a DIFFERENT element than the popup's owner: the hit-stack rule
        // deliberately lets an element keep clicks over its own popup (that is what makes toggle-to-close
        // work), so a popup owned by us/scope-tree can never be stolen from a sibling control inside the
        // same widget. The nav rows are a separate element, are always drawn, and their click is observable
        // through the active-tab binding.
        var navRects = new List<Rect>();
        try
        {
            SetButtonOverride(rect =>
            {
                // The compact nav card measures ~144x49 under the stub metrics (160 column minus the
                // widget's 2x8 padding), so the old >=160/>=50 filter no longer matches it: keep enough
                // width to exclude support-row controls and a height floor that still isolates rows.
                if (rect.width >= 100f && rect.height >= 30f) navRects.Add(rect);
                return false;
            });
            ctx.Host.DrawChecked(ctx.Viewport);
        }
        finally
        {
            ClearOverrides();
        }

        Assert(navRects.Count >= 1, "expected at least one nav row for the covered-click case");
        Rect target = navRects.OrderBy(r => r.y).ThenBy(r => r.x).First();
        Rect targetWindow = target;
        string tabBefore = ctx.Host.Bindings.TryGet(UiBindings.ActiveTabKey, out string t0) ? t0 : "";
        Assert(tabBefore == "Tuning", "the lane must start on Tuning, got " + tabBefore);

        // Anchor a scope-dropdown popup just above the target so it opens downward over the control.
        string? owner = null;
        Rect popup = default;
        foreach (string key in UniversalSqueaker.Kernel.BuiltInActionKeys.All)
        {
            ctx.Host.Session.OpenPopup("scope-tree-scope-" + key, new Rect(targetWindow.x, targetWindow.y - 24f, targetWindow.width, 22f));
            ctx.Host.DrawChecked(ctx.Viewport);
            ctx.Host.DrawChecked(ctx.Viewport);
            if (TryGetPopupHitLayerFromHost(ctx.Host, out UiHitLayer layer))
            {
                owner = key;
                popup = layer.Rect;
                break;
            }

            ctx.Host.Session.ClosePopup();
        }

        Assert(owner != null, "no scope dropdown published a popup layer for the covered-click case");
        Vector2 pointerWindow = new(targetWindow.x + targetWindow.width / 2f, targetWindow.y + targetWindow.height / 2f);
        Assert(popup.x <= pointerWindow.x && pointerWindow.x <= popup.xMax
            && popup.y <= pointerWindow.y && pointerWindow.y <= popup.yMax,
            "the popup must really cover the target control; popup=" + popup + " target=" + targetWindow);

        try
        {
            SetMousePosition(pointerWindow);
            SetButtonOverride(rect => RectMatches(rect, target));
            ctx.Host.DrawChecked(ctx.Viewport);
            Assert(!(ctx.Host.Bindings.TryGet(UiBindings.ActiveTabKey, out string coveredTab) && coveredTab != tabBefore),
                "a popup-covered control must not take the click (ctx overload); a raw UiNative.Button(Rect) site fails here");

            // Positive control: same pointer, same click, popup gone - the tab must now switch.
            ctx.Host.Session.ClosePopup();
            ctx.Host.DrawChecked(ctx.Viewport);
            ctx.Host.DrawChecked(ctx.Viewport);
            SetButtonOverride(rect => RectMatches(rect, target));
            ctx.Host.DrawChecked(ctx.Viewport);
            string tabAfter = ctx.Host.Bindings.TryGet(UiBindings.ActiveTabKey, out string t1) ? t1 : "";
            Assert(tabAfter != tabBefore,
                "with no popup the same click must reach the control (positive control for the yield assertion); tab stayed " + tabAfter);
        }
        finally
        {
            ClearMousePosition();
            ClearOverrides();
            ctx.Host.Session.ClosePopup();
        }
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
