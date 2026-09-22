using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// S4-3 lane: the trigger-timing card, dissolved out of the US-owned composite <c>us/timing</c> and rebuilt
/// as a manifest subtree over the library's own atoms (<c>input/slider</c>, <c>input/number-field</c>,
/// <c>input/button</c>, <c>text/wrapped</c>, <c>section/header</c>).
///
/// <para>
/// MUTATION LEDGER (the mutation each step is built to catch, and what reddens):
/// <list type="number">
/// <item><b>TheCardIsDeclaredOverAtoms</b> - restoring the composite's manifest line (and its Registrar
/// line) reddens the Section assertion here and the negative step in <c>SettingsGeometryLaneTests</c>'s
/// evidence table; the Registrar kind-set pin in <c>UiSourceInvariantTests</c> reddens with them.</item>
/// <item><b>TheCaptionIsTheOneStringTheHostBuilds</b> - un-binding the caption and replacing the read-only
/// projection with a literal in the manifest reddens the resolved-text equality, because the host's own
/// binding is read through the same public seam the harness uses. This is the friction the slice FIXED:
/// the retired composite sized its band against a hand-written sample constant, while here measure and draw
/// consume one string.</item>
/// <item><b>TheCaptionBandDoesNotFollowTheValue</b> - a caption whose band tracked the value would redden
/// here. It is the property the composite bought with its worst-case sample; the declarative shape buys it
/// from the host's projection instead (the sentence is one line at every value the declared window can
/// produce), and this step measures all three values rather than asserting the reasoning.</item>
/// <item><b>EveryControlTerminatesOnTheDeclaredEdges</b> - moving a declared width, gap or the seconds
/// field's own Width in the manifest reddens the drawn-edge arithmetic.</item>
/// <item><b>OneValueInTwoUnitsWritesOneField</b> - rebinding the seconds field to another key, or a step
/// command that writes nothing, reddens the type-agnostic business-recording assertion.</item>
/// </list>
/// </para>
///
/// <para>
/// NOT CLAIMED HERE: the drawn look (the atom's own box geometry, the ink of the minus/plus captions) and
/// the in-game feel of the number field and the slider. Both are recorded as real-screen items in the S4-3
/// friction report.
/// </para>
/// </summary>
internal static class DeclarativeTimingLaneTests
{
    private const float PageWidth = 1024f;
    private const float PageHeight = 900f;
    private const float CardPadding = 12f;
    private const float SectionGap = 6f;
    private const float HeaderHeight = 26f;
    private const float IntervalRowGap = 8f;
    private const float MultiplierRowGap = 4f;
    private const float ButtonWidth = 26f;
    private const float MinusFieldGap = 4f;
    private const float Tolerance = 0.51f;

    /// <summary>Every width this lane drives. The 320 case is included because it is the narrow tier the
    /// page must still draw without an overflow, not because the card is expected to look the same there.</summary>
    private static readonly float[] Widths = { 1024f, 736f, 480f, 320f };

    public static int RunAll()
    {
        Step("the timing card is a declared Section over atoms, not the retired composite", TheCardIsDeclaredOverAtoms);
        Step("the caption is the one string the host builds (EN + ZH)", TheCaptionIsTheOneStringTheHostBuilds);
        Step("the caption band does not follow the value", TheCaptionBandDoesNotFollowTheValue);
        Step("every timing control terminates on its declared edges", EveryControlTerminatesOnTheDeclaredEdges);
        Step("one interval value in two units writes one business field", OneValueInTwoUnitsWritesOneField);
        Console.WriteLine("DeclarativeTimingLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 1: the retirement is real and the live tree is declarative
    // ---------------------------------------------------------------------------------------------

    private static void TheCardIsDeclaredOverAtoms()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        UiElementSpec? card = FindById(host.Manifest.Roots, "timing");
        Assert(card != null, "the shipped manifest must carry the timing card");
        Assert(card!.Kind == "Section",
            "the timing card must be the engine's Section container - the composite kind that used to own it"
            + " is retired - got " + card.Kind);
        Assert(card.TryGetAttribute("Tab", out string tab) && tab == "Overview",
            "the timing card must stay gated by the Overview workspace");
        Assert(card.TryGetAttribute("Padding", out string padding) && padding == "12",
            "the card must declare the checklist card's Padding 12, got '" + padding + "'");

        // The four controls, matched by the property that makes each one a control: its binding.
        Assert(FindByAttribute(card, "Bind", "interval-ticks")?.Kind == "input/slider",
            "the interval slider must be the input/slider atom over interval-ticks");
        Assert(FindByAttribute(card, "Bind", "interval-seconds")?.Kind == "input/number-field",
            "the seconds editor must be the input/number-field atom over interval-seconds");
        Assert(FindByAttribute(card, "Bind", "timing-interval-caption")?.Kind == "text/wrapped",
            "the interval caption must be a BOUND text/wrapped atom, not a hand-formatted label");
        Assert(FindByAttribute(card, "Bind", "cooldown-multiplier")?.Kind == "input/number-field",
            "the multiplier editor must be the input/number-field atom over cooldown-multiplier");

        // The stepper is two command buttons, and each one names its own command.
        UiElementSpec? minus = FindById(card.Children, "timing-multiplier-minus");
        UiElementSpec? plus = FindById(card.Children, "timing-multiplier-plus");
        Assert(minus?.Kind == "input/button" && minus.TryGetAttribute("ActionBind", out string minusAction)
               && minusAction == "timing-multiplier-minus",
            "the minus control must be an input/button firing its own command");
        Assert(plus?.Kind == "input/button" && plus.TryGetAttribute("ActionBind", out string plusAction)
               && plusAction == "timing-multiplier-plus",
            "the plus control must be an input/button firing its own command");

        // The retired kind must not be anywhere in the live tree, and the two commands must be bound.
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        CollectKinds(host.Manifest.Roots, kinds);
        Assert(!kinds.Contains("us/timing"), "the live manifest must not contain the retired kind 'us/timing'");
        // A command is not a value binding: the registration shape is the asserted contract, so asking the
        // value side "do you have this key" must answer no for both commands.
        Assert(!host.Bindings.IsWritable("timing-multiplier-minus") && !host.Bindings.IsWritable("timing-multiplier-plus"),
            "the two steppers must be COMMANDS, not value bindings (a value binding would make the button a"
            + " different control with a different contract)");
        host.Bindings.Invoke("timing-multiplier-minus");
        host.Bindings.Invoke("timing-multiplier-plus");
    }

    // ---------------------------------------------------------------------------------------------
    // Step 2: the caption is the host's own sentence, resolved through the translation seam
    // ---------------------------------------------------------------------------------------------

    private static void TheCaptionIsTheOneStringTheHostBuilds()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            try
            {
                using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
                string template = table["US.Tuning.MinInterval"];
                // The atom's binding is a float (input/slider validates float); the business value behind it
                // is whole ticks, so the expected sentence is built from the rounded float.
                int ticks = (int)host.Bindings.Get<float>("interval-ticks");
                Assert(ticks > 0, "the fixture must carry a real interval, got " + ticks);

                // The host builds the sentence from ITS OWN two ingredients: the live value and the
                // translated template. Expected is derived here rather than copied from the binding, so a
                // host that stopped formatting the value (or stopped translating) reddens.
                string expected = string.Format(
                    template,
                    (ticks / 60f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s");
                string actual = host.Bindings.Get<string>("timing-interval-caption");
                Assert(string.Equals(expected, actual, StringComparison.Ordinal),
                    "the caption binding must be the host's own sentence at " + language + ": expected '"
                    + expected + "', got '" + actual + "'");
                // "Not the raw key": the caption must contain the template's STATIC text. The template now
                // carries a trailing sentence after {0} ("... · 1.0x uses the default rhythm"), so the
                // static half is split around the placeholder rather than assumed to be a prefix - the
                // assertion is about the key being resolved, and it must not break when the wording grows.
                int placeholder = template.IndexOf("{0}", StringComparison.Ordinal);
                string before = template.Substring(0, placeholder).Trim();
                string after = template.Substring(placeholder + 3).Trim();
                Assert(actual.Contains(before) && actual.Contains(after),
                    "the caption must carry the translated template, not the raw key: '" + actual + "'");
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 3: the caption band is value-independent
    // ---------------------------------------------------------------------------------------------

    private static void TheCaptionBandDoesNotFollowTheValue()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            var metrics = new Program.StubMetrics();
            var source = new RecordingSettingsSource { RichData = true };
            using UiHost host = UsKernelSettingsHost.Create(source, metrics);
            UiLayoutSnapshot snapshot = Arrange(host);
            Rect caption = RectOf(snapshot, "timing-interval-caption");
            Assert(caption.width > 1f, "the caption must be arranged with a real band, got " + Describe(caption));

            // The property the retired composite bought with a worst-case sample, measured rather than
            // argued: the caption's band is only value-independent if the caption's OWN RECT does not move
            // with the value (a flex row would let a longer sentence widen the band) and if no value the
            // declared window can produce needs a second line at that band. The host's projection is read
            // for real values here - the fixture's own view is constant, so the binding is asked directly
            // rather than through a second fixture seed, which would move the SHARED fixture's input for
            // every lane that constructs it (the S4-1 rule).
            var sentences = new List<string>();
            foreach (int ticks in new[] { 1, 300, 600 })
            {
                host.Bindings.Set("interval-ticks", (float)ticks);
                UiLayoutSnapshot after = Arrange(host);
                Rect moved = RectOf(after, "timing-interval-caption");
                Assert(Math.Abs(moved.width - caption.width) <= 0.01f && Math.Abs(moved.height - caption.height) <= 0.01f,
                    "the caption band must not move with the value at " + ticks + " ticks: baseline "
                    + Describe(caption) + " now " + Describe(moved));

                string sentence = BuildCaption(Program.ReadKeyedTable("English"), ticks);
                sentences.Add(sentence);
                float needed = Math.Max(1f, metrics.MeasureText(sentence, UiFont.Tiny, caption.width));
                Assert(needed <= caption.height + 0.01f,
                    "every value in the declared window must fit the caption's one band: '" + sentence
                    + "' needs " + Num(needed) + "px in a " + Num(caption.height) + "px band at width "
                    + Num(caption.width));
                Console.WriteLine("[timing-caption] ticks=" + ticks + " band=" + Describe(moved)
                    + " needs=" + Num(needed) + " text='" + sentence + "'");
            }

            Assert(!string.Equals(sentences[0], sentences[2], StringComparison.Ordinal),
                "the projection must actually vary with the value, or this step would assert nothing: '"
                + sentences[0] + "' vs '" + sentences[2] + "'");
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    /// <summary>The sentence the host's own projection builds for one tick value - the same template and the
    /// same "0.0 s" formatting the host uses, written here independently so the two cannot drift silently.</summary>
    private static string BuildCaption(Dictionary<string, string> table, int ticks)
    {
        return string.Format(
            table["US.Tuning.MinInterval"],
            (ticks / 60f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s");
    }

    // ---------------------------------------------------------------------------------------------
    // Step 4: the declared geometry is the drawn geometry
    // ---------------------------------------------------------------------------------------------

    private static void EveryControlTerminatesOnTheDeclaredEdges()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            try
            {
                foreach (float width in Widths)
                {
                    var metrics = new Program.StubMetrics();
                    var reports = new List<UiOverflowReport>();
                    UiFitAudit.Attach(metrics, reports.Add);
                    UiFitAudit.Enabled = true;
                    try
                    {
                        var source = new RecordingSettingsSource { RichData = true };
                        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
                        UiLayoutSnapshot snapshot = Arrange(host, width);
                        host.DrawChecked(new Rect(0f, 0f, width, PageHeight));

                        foreach (UiOverflowReport report in reports.Where(
                                     r => r.ElementPath.IndexOf("timing", StringComparison.Ordinal) >= 0))
                        {
                            throw new InvalidOperationException(
                                "the declarative timing card overflows at " + width + " (" + language + "): "
                                + report.ElementPath + " " + report.Axis + " needs " + report.Needed
                                + " has " + report.Available);
                        }

                        Rect card = RectOf(snapshot, "timing");
                        Rect header = RectOf(snapshot, "timing-header");
                        Rect intervalRow = RectOf(snapshot, "timing-interval-row");
                        Rect seconds = RectOf(snapshot, "timing-interval-seconds");
                        Rect slider = RectOf(snapshot, "timing-interval-slider");
                        Rect multiplierRow = RectOf(snapshot, "timing-multiplier-row");
                        Rect minus = RectOf(snapshot, "timing-multiplier-minus");
                        Rect field = RectOf(snapshot, "timing-multiplier-field");
                        Rect plus = RectOf(snapshot, "timing-multiplier-plus");

                        // The card chrome relation the checklist card already pins: Padding + header +
                        // Section Gap + the declared children + their gaps + Padding accounts for the card.
                        float expectedCard = CardPadding + header.height + SectionGap
                            + intervalRow.height + SectionGap + slider.height + SectionGap
                            + multiplierRow.height + CardPadding;
                        Assert(Math.Abs(card.height - expectedCard) <= 0.5f,
                            "the timing card must equal Padding + header + Section Gaps + the declared rows at "
                            + width + " (" + language + "): card " + Num(card.height) + " vs "
                            + Num(expectedCard) + " (header " + Num(header.height) + ", intervalRow "
                            + Num(intervalRow.height) + ", slider " + Num(slider.height) + ", multiplierRow "
                            + Num(multiplierRow.height) + ")");

                        // The interval row: [caption | field], both flex siblings, and the declared Gap is
                        // what separates them. The field is the row's terminal control.
                        Assert(Math.Abs(seconds.xMax - intervalRow.xMax) <= Tolerance,
                            "the seconds field must terminate on the interval row's right edge at " + width
                            + " (" + language + "): " + Describe(seconds) + " row " + Describe(intervalRow));
                        Rect caption = RectOf(snapshot, "timing-interval-caption");
                        Assert(Math.Abs(caption.x - intervalRow.x) <= Tolerance,
                            "the caption must start at the interval row's left edge at " + width + " ("
                            + language + "): " + Describe(caption) + " row " + Describe(intervalRow));
                        Assert(Math.Abs((seconds.x - caption.xMax) - IntervalRowGap) <= Tolerance,
                            "the interval row's declared Gap must be the space between caption and field at "
                            + width + " (" + language + "), got " + Num(seconds.x - caption.xMax));

                        // The multiplier row: [label | minus | field | plus], the three declared fixed
                        // weights and the declared Gap, terminating on the row's right edge.
                        Assert(Math.Abs(plus.xMax - multiplierRow.xMax) <= Tolerance,
                            "the plus button must terminate on the multiplier row's right edge at " + width
                            + " (" + language + "): " + Describe(plus) + " row " + Describe(multiplierRow));
                        Assert(Math.Abs(minus.width - ButtonWidth) <= Tolerance
                               && Math.Abs(plus.width - ButtonWidth) <= Tolerance,
                            "both stepper buttons must keep the composite's declared 26px weight at " + width
                            + " (" + language + "): minus " + Num(minus.width) + ", plus " + Num(plus.width));
                        Assert(Math.Abs((field.x - minus.xMax) - MinusFieldGap) <= Tolerance
                               && Math.Abs((plus.x - field.xMax) - MinusFieldGap) <= Tolerance,
                            "the multiplier row's declared gaps must separate minus/field/plus at " + width
                            + " (" + language + "): " + Num(field.x - minus.xMax) + " and "
                            + Num(plus.x - field.xMax));

                        // The slider spans the whole body: one control on its own line.
                        Assert(Math.Abs(slider.width - intervalRow.width) <= Tolerance
                               && Math.Abs(slider.x - intervalRow.x) <= Tolerance,
                            "the interval slider must span the card body at " + width + " (" + language
                            + "): " + Describe(slider) + " vs row " + Describe(intervalRow));

                        Console.WriteLine("[timing-geom] " + width + " " + language
                            + " card=" + Num(card.height) + "/" + Num(expectedCard)
                            + " intervalRow=" + Num(intervalRow.height)
                            + " multiplierRow=" + Num(multiplierRow.height)
                            + " labelW=" + Num(RectOf(snapshot, "timing-multiplier-label").width)
                            + " labelText='" + table["US.Tuning.CooldownMultiplier"] + "'");
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
    // Step 5: one value, two units, one business field
    // ---------------------------------------------------------------------------------------------

    private static void OneValueInTwoUnitsWritesOneField()
    {
        var source = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());

        // The slider's own channel.
        source.LastMinIntervalTicks = null;
        host.Bindings.Set("interval-ticks", 420f);
        Assert(source.LastMinIntervalTicks == 420,
            "the slider's interval-ticks write must reach the business setter, got "
            + (source.LastMinIntervalTicks?.ToString() ?? "null"));
        // The projection's ARITHMETIC, measured against the instrument: this fixture's views carry a
        // constant 216 ticks, so the read must be 216 / 60 = 3.6 s. (The write-back path is asserted below
        // through the business recorder instead - the fixture's view is a constant, so "the projection
        // follows the write" is not observable here and asserting it would measure the fake.)
        float projected = host.Bindings.Get<float>("interval-seconds");
        Assert(Math.Abs(projected - 3.6f) < 0.001f,
            "the seconds projection must be ticks / 60 (216 ticks = 3.6 s), got " + projected);

        // The field's own channel: seconds in, ticks out.
        source.LastMinIntervalTicks = null;
        host.Bindings.Set("interval-seconds", 5f);
        Assert(source.LastMinIntervalTicks == 300,
            "the field's interval-seconds write must convert back into machine ticks (5 s = 300), got "
            + (source.LastMinIntervalTicks?.ToString() ?? "null"));

        // Both stepper commands move the SAME field, in opposite directions. The expected values are read
        // from the instrument: this fixture's views carry a constant multiplier of 1.0.
        source.LastCooldownMultiplier = null;
        host.Bindings.Invoke("timing-multiplier-minus");
        Assert(Math.Abs(source.LastCooldownMultiplier.GetValueOrDefault() - 0.9f) < 0.001f,
            "the minus command must step the viewed 1.0 down by 0.1, got "
            + (source.LastCooldownMultiplier?.ToString() ?? "null"));
        source.LastCooldownMultiplier = null;
        host.Bindings.Invoke("timing-multiplier-plus");
        Assert(Math.Abs(source.LastCooldownMultiplier.GetValueOrDefault() - 1.1f) < 0.001f,
            "the plus command must step it up by 0.1, got "
            + (source.LastCooldownMultiplier?.ToString() ?? "null"));

        // The retired action binding must be gone: one value, one write channel.
        // Existence on this surface is "a value binding answers a read": the retired int key must not.
        Assert(!host.Bindings.TryGet<int>("min-interval", out _),
            "the retired 'min-interval' value binding must not exist: the declarative card owns"
            + " interval-ticks and interval-seconds instead");
    }

    // ---------------------------------------------------------------------------------------------
    // Plumbing
    // ---------------------------------------------------------------------------------------------

    private static UiLayoutSnapshot Arrange(UiHost host, float width = PageWidth)
    {
        host.Bindings.Invoke("set-tab", "Overview");
        host.MeasureAndArrange(new Vector2(width, PageHeight));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        return host.MeasureAndArrange(new Vector2(width, PageHeight));
    }

    private static Rect RectOf(UiLayoutSnapshot snapshot, string id)
    {
        Assert(snapshot.RectById.TryGetValue(id, out Rect rect),
            "the arranged snapshot must carry '" + id + "'; a missing element means the manifest lost it");
        return rect;
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

    private static string Describe(Rect rect)
    {
        return "(x=" + Num(rect.x) + " y=" + Num(rect.y) + " w=" + Num(rect.width) + " h=" + Num(rect.height)
            + " right=" + Num(rect.xMax) + ")";
    }

    private static string Num(float value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("DeclarativeTimingLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}