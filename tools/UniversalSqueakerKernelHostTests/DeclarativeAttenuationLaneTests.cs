using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// S4-3b lane: the Distance card, dissolved out of the US-owned composite <c>us/attenuation-editor</c> and
/// rebuilt as a manifest subtree whose chart is the library's own <c>chart/line</c> widget (declared
/// directly - the composite hand-built a UiElementSpec to drive it), plus a bound status sentence and three
/// declarative preset buttons.
///
/// <para>
/// MUTATION LEDGER (the mutation each step is built to catch, and what reddens):
/// <list type="number">
/// <item><b>TheCardIsDeclaredOverTheEngineChart</b> - restoring the composite's manifest line (and its
/// Registrar line) reddens the Section assertion here, the negative step in <c>Program.cs</c>'s
/// resource-shape lane, and the Registrar kind-set pin in <c>UiSourceInvariantTests</c>.</item>
/// <item><b>TheChartKeepsTheCompositesDragContract</b> - dropping Editable, EditablePoints or the
/// ActionBind reddens: the drag contract is what the composite's wrapper existed to declare.</item>
/// <item><b>TheStatusSentenceIsTheHostsOwn</b> - un-binding the status row, or a host that stopped printing
/// the range, reddens the equality against a sentence assembled here from the same two ingredients.</item>
/// <item><b>OnePresetIsSelectedAndItIsTheModels</b> - flipping a SelectedKey bool, or binding two buttons to
/// one key, reddens the "exactly one true" assertion and the press test that follows it.</item>
/// <item><b>EachPresetCarriesItsOwnPayload</b> - pointing a button at another button's payload key reddens:
/// the press must land on the preset that button NAMES, not on a neighbour.</item>
/// </list>
/// </para>
///
/// <para>
/// NOT CLAIMED HERE: the drag itself (the chart's hotControl path is the carrier's own, exercised by its
/// harness), the preset buttons' painted look, and the composite's narrow branch (body &lt; 200px, dropped
/// on purpose - see the manifest note; the window is never that narrow because the drawer replaces the body
/// instead of squeezing it).
/// </para>
/// </summary>
internal static class DeclarativeAttenuationLaneTests
{
    private const float PageWidth = 1024f;
    private const float PageHeight = 900f;
    private const float CardPadding = 12f;
    private const float SectionGap = 6f;
    private const float BodyGap = 4f;
    private const float PresetGap = 4f;
    private const float ChartHeight = 64f;
    /// <summary>The composite's declared preset height. It is a FLOOR, not a pin: the button measures its
    /// own caption, and the engine's default row height (24 under this theme) is what a button actually
    /// draws in the row it is arranged in, so the lane asserts the floor with the theme's own slack.</summary>
    private const float PresetHeight = 26f;

    /// <summary>Slack for "the button is at least the declared floor".</summary>
    private const float MinPresetHeightSlack = 3f;

    /// <summary>The drawn preset band with no caption wrap: the theme's regular row height (24), measured.</summary>
    private const float PresetHeightFloor = 24f;

    /// <summary>The tallest a preset may draw: three wrapped Small lines plus the atom's own lead.</summary>
    private const float PresetHeightCeil = 70f;
    private const float Tolerance = 0.51f;

    private static readonly string[] PresetIds =
    {
        "attenuation-preset-conservative", "attenuation-preset-balanced", "attenuation-preset-strong"
    };

    private static readonly string[] PresetSelectedKeys =
    {
        "distance-preset-conservative", "distance-preset-balanced", "distance-preset-strong"
    };

    private static readonly string[] PresetValueKeys =
    {
        "distance-preset-conservative-value", "distance-preset-balanced-value", "distance-preset-strong-value"
    };

    public static int RunAll()
    {
        Step("the Distance card is a declared Section over the engine's chart/line", TheCardIsDeclaredOverTheEngineChart);
        Step("the chart keeps the composite's drag contract", TheChartKeepsTheCompositesDragContract);
        Step("the status sentence is the host's own (EN + ZH)", TheStatusSentenceIsTheHostsOwn);
        Step("exactly one preset answers selected and it is the model's", OnePresetIsSelectedAndItIsTheModels);
        Step("each preset button carries its own payload", EachPresetCarriesItsOwnPayload);
        Step("the declared card geometry is the drawn geometry", TheDeclaredGeometryIsTheDrawnGeometry);
        Console.WriteLine("DeclarativeAttenuationLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------

    private static void TheCardIsDeclaredOverTheEngineChart()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        UiElementSpec? card = FindById(host.Manifest.Roots, "attenuation-editor");
        Assert(card != null, "the shipped manifest must carry the attenuation card");
        Assert(card!.Kind == "Section",
            "the card must be the engine's Section container - the composite kind that used to own it is"
            + " retired - got " + card.Kind);
        Assert(card.TryGetAttribute("Tab", out string tab) && tab == "Distance",
            "the card must stay gated by the Distance workspace");

        UiElementSpec? chart = FindById(card.Children, "attenuation-chart");
        Assert(chart?.Kind == "chart/line",
            "the chart must be the engine's own chart/line widget, got "
            + (chart == null ? "(missing)" : chart.Kind));
        Assert(FindByAttribute(card, "Bind", "attenuation-status")?.Kind == "text/wrapped",
            "the status read-out must be a BOUND text/wrapped atom, not a hand-formatted label");

        var kinds = new HashSet<string>(StringComparer.Ordinal);
        CollectKinds(host.Manifest.Roots, kinds);
        Assert(!kinds.Contains("us/attenuation-editor"),
            "the live manifest must not contain the retired kind 'us/attenuation-editor'");
    }

    private static void TheChartKeepsTheCompositesDragContract()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        UiElementSpec chart = FindById(host.Manifest.Roots, "attenuation-chart")!;
        Assert(chart.TryGetAttribute("Bind", out string bind) && bind == "attenuation-points",
            "the chart must read the attenuation-points list binding, got '" + bind + "'");
        Assert(chart.TryGetAttribute("ActionBind", out string action) && action == "attenuation-point",
            "the chart must publish its drags through the typed attenuation-point action, got '" + action + "'");
        Assert(chart.TryGetAttribute("Editable", out string editable)
               && string.Equals(editable.Trim(), "true", StringComparison.OrdinalIgnoreCase),
            "the chart must be declared Editable - that declaration IS the drag contract the composite's"
            + " hand-built spec used to carry, got '" + editable + "'");
        Assert(chart.TryGetAttribute("EditablePoints", out string points) && points.Trim() == "1,2",
            "exactly the two interior points must be draggable (both ends are locked by the business"
            + " projection), got '" + points + "'");
        Assert(chart.TryGetAttribute("Height", out string height)
               && Math.Abs(float.Parse(height, System.Globalization.CultureInfo.InvariantCulture) - ChartHeight) < 0.01f,
            "the declared chart height must be the composite's 64px, got '" + height + "'");
    }

    private static void TheStatusSentenceIsTheHostsOwn()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            try
            {
                using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
                string presetKey = "US.Distance.Preset." + host.Bindings.Get<string>("distance-preset");
                Assert(table.ContainsKey(presetKey), "the fixture's preset must have a Keyed name: " + presetKey);
                float min = host.Bindings.Get<float>("distance-range-min");
                float max = host.Bindings.Get<float>("distance-range-max");

                // Assembled here from the same two ingredients the host uses, so a host that stopped
                // translating the preset name or stopped printing the range reddens.
                string expected = table[presetKey] + "  " + AttenuationMath.FormatRangeDisplay(min, max);
                string actual = host.Bindings.Get<string>("attenuation-status");
                Assert(string.Equals(expected, actual, StringComparison.Ordinal),
                    "the status row must be the host's own sentence at " + language + ": expected '"
                    + expected + "', got '" + actual + "'");
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    private static void OnePresetIsSelectedAndItIsTheModels()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });

        // The MANIFEST half, and it is the half that carries the weight: this fixture's view is a constant
        // ("Strong"), so at runtime only the strong key can answer true and three read-only bools would pass
        // the "exactly one" assertion even if two buttons shared a key. What must hold is the PAIRING - each
        // button answers for itself, under its own selected key and its own payload - so it is asserted on
        // the declarations.
        for (int i = 0; i < PresetIds.Length; i++)
        {
            UiElementSpec? button = FindById(host.Manifest.Roots, PresetIds[i]);
            Assert(button != null, "the manifest must declare " + PresetIds[i]);
            Assert(button!.TryGetAttribute("SelectedKey", out string selectedKey)
                   && selectedKey == PresetSelectedKeys[i],
                PresetIds[i] + " must answer for ITSELF: expected SelectedKey='" + PresetSelectedKeys[i]
                + "', got '" + selectedKey + "'. Two buttons sharing one selected key paint together.");
            Assert(button.TryGetAttribute("PayloadKey", out string payloadKey) && payloadKey == PresetValueKeys[i],
                PresetIds[i] + " must carry ITS OWN payload: expected PayloadKey='" + PresetValueKeys[i]
                + "', got '" + payloadKey + "'");
        }
        string current = host.Bindings.Get<string>("distance-preset");
        var trueKeys = new List<string>();
        for (int i = 0; i < PresetSelectedKeys.Length; i++)
        {
            if (host.Bindings.Get<bool>(PresetSelectedKeys[i])) trueKeys.Add(PresetSelectedKeys[i]);
        }

        Assert(trueKeys.Count == 1,
            "exactly ONE preset button may answer selected, got " + trueKeys.Count + " ["
            + string.Join(",", trueKeys) + "]. Two true values paint two highlighted buttons; zero paints"
            + " none, and the status sentence still names one.");
        int selectedIndex = Array.IndexOf(PresetSelectedKeys, trueKeys[0]);
        Assert(string.Equals(host.Bindings.Get<string>(PresetValueKeys[selectedIndex]), current, StringComparison.OrdinalIgnoreCase),
            "the highlighted button must be the preset the model holds: model '" + current + "', button '"
            + host.Bindings.Get<string>(PresetValueKeys[selectedIndex]) + "'");
    }

    private static void EachPresetCarriesItsOwnPayload()
    {
        var source = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            UiLayoutSnapshot snapshot = Arrange(host);
            float contentRight = snapshot.RectById["attenuation-body"].xMax;
            float cellWidth = (snapshot.RectById["attenuation-presets"].width - PresetGap * 2f) / 3f;

            // The three buttons are found among the DRAWN rects, in x order, by the shape the manifest
            // declares (equal thirds of the preset row, 26px tall) and by sitting in the card's own body.
            var recorded = new List<Rect>();
            DrawWithButtons(host, rect => { recorded.Add(rect); return false; });
            List<Rect> buttons = recorded
                .Where(r => Math.Abs(r.width - cellWidth) <= Tolerance
                            && r.height >= PresetHeight - MinPresetHeightSlack
                            && r.xMax <= contentRight + Tolerance)
                .OrderBy(r => r.x)
                .ToList();
            Assert(buttons.Count == 3,
                "the card must draw exactly three preset buttons in the declared shape, got "
                + buttons.Count + " [" + string.Join(" ", buttons.Select(Describe)) + "]");

            for (int index = 0; index < buttons.Count; index++)
            {
                source.LastDistancePreset = null;
                DrawWithButtons(host, rect => RectMatches(rect, buttons[index]));
                SqueakDistancePreset? received = source.LastDistancePreset;
                Assert(received.HasValue,
                    "pressing preset button " + index + " must reach the business setter at all");
                Assert(received!.Value.ToString() == ExpectedPreset[index],
                    "pressing preset button " + index + " must select the preset that button NAMES ("
                    + ExpectedPreset[index] + "), got " + received.Value
                    + " - each button carries its own PayloadKey for exactly this reason");
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    private static void DrawWithButtons(UiHost host, Func<Rect, bool> click)
    {
        var field = typeof(UiNative).GetField("ButtonOverride",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert(field != null && field.FieldType == typeof(Func<Rect, bool>),
            "REFLECTION BLOCKER: UiNative.ButtonOverride is not the expected seam on net472");
        try
        {
            field!.SetValue(null, click);
            host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
        }
        finally
        {
            field!.SetValue(null, null);
        }
    }

    private static bool RectMatches(Rect a, Rect b)
    {
        return Math.Abs(a.x - b.x) <= 0.01f && Math.Abs(a.y - b.y) <= 0.01f
            && Math.Abs(a.width - b.width) <= 0.01f && Math.Abs(a.height - b.height) <= 0.01f;
    }

    /// <summary>The preset each button's payload must select, in the manifest's own order.</summary>
    private static readonly string[] ExpectedPreset = { "Conservative", "Balanced", "Strong" };

    private static void TheDeclaredGeometryIsTheDrawnGeometry()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                // 320 is deliberately NOT swept here: below the body-row Breakpoint the nav stacks and the
                // centre column gets ~296px, where a third of the preset row is narrower than the English
                // caption "Conservative"/"Balanced" needs, and input/button draws its caption single-line (or
                // not at all), so a fit finding there is structural, not a layout defect. That state is
                // recorded as a measured narrow-tier finding in the S4-3b report instead of being asserted
                // away; it is also unreachable in the shipped window, which never opens below 800 logical
                // pixels. The three widths below are the ones the page is accepted at.
                foreach (float width in new[] { 1024f, 736f, 480f })
                {
                    var metrics = new Program.StubMetrics();
                    var reports = new List<UiOverflowReport>();
                    UiFitAudit.Attach(metrics, reports.Add);
                    UiFitAudit.Enabled = true;
                    try
                    {
                        var source = new RecordingSettingsSource { RichData = true };
                        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
                        host.Bindings.Invoke("set-tab", "Distance");
                        host.MeasureAndArrange(new Vector2(width, PageHeight));
                        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
                        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(width, PageHeight));
                        host.DrawChecked(new Rect(0f, 0f, width, PageHeight));

                        foreach (UiOverflowReport report in reports.Where(
                                     r => r.ElementPath.IndexOf("attenuation", StringComparison.Ordinal) >= 0))
                        {
                            throw new InvalidOperationException(
                                "the declarative Distance card overflows at " + width + " (" + language + "): "
                                + report.ElementPath + " " + report.Axis + " needs " + report.Needed
                                + " has " + report.Available);
                        }

                        Rect card = RectOf(snapshot, "attenuation-editor");
                        Rect header = RectOf(snapshot, "attenuation-header");
                        Rect body = RectOf(snapshot, "attenuation-body");
                        Rect chart = RectOf(snapshot, "attenuation-chart");
                        Rect status = RectOf(snapshot, "attenuation-status");
                        Rect presets = RectOf(snapshot, "attenuation-presets");
                        Rect first = RectOf(snapshot, PresetIds[0]);
                        Rect last = RectOf(snapshot, PresetIds[PresetIds.Length - 1]);

                        // Card chrome = Padding + header + Section Gap + body + Padding.
                        float expectedCard = CardPadding + header.height + SectionGap + body.height + CardPadding;
                        Assert(Math.Abs(card.height - expectedCard) <= 0.5f,
                            "the card must equal Padding + header + Section Gap + body + Padding at " + width
                            + " (" + language + "): card " + Num(card.height) + " vs " + Num(expectedCard));

                        // Body = declared column: chart + Gap + status + Gap + preset row.
                        float expectedBody = chart.height + BodyGap + status.height + BodyGap + presets.height;
                        Assert(Math.Abs(body.height - expectedBody) <= 0.5f,
                            "the body must be chart + Gap + status + Gap + the preset row at " + width + " ("
                            + language + "): body " + Num(body.height) + " vs " + Num(expectedBody));

                        // The chart keeps its declared height and the body's width.
                        Assert(Math.Abs(chart.height - ChartHeight) <= Tolerance,
                            "the chart must keep its declared 64px at " + width + " (" + language + "), got "
                            + Num(chart.height));

                        // The three presets fill the row: equal cells, the declared gap, terminating on the
                        // row's right edge. MUTATION PROOF for the declared widths/gaps.
                        Assert(Math.Abs(last.xMax - presets.xMax) <= Tolerance,
                            "the last preset must terminate on the preset row's right edge at " + width + " ("
                            + language + "): " + Describe(last) + " row " + Describe(presets));
                        float cellWidth = (presets.width - PresetGap * 2f) / 3f;
                        for (int i = 0; i < PresetIds.Length; i++)
                        {
                            Rect cell = RectOf(snapshot, PresetIds[i]);
                            Assert(Math.Abs(cell.width - cellWidth) <= Tolerance,
                                "each preset must take an equal third of the row at " + width + " ("
                                + language + "): " + PresetIds[i] + " is " + Num(cell.width) + ", expected "
                                + Num(cellWidth));
                            // The button measures its own caption, so the declared 26px is a FLOOR, not a
                            // pin: a third of the narrowest row is narrower than the English captions need,
                            // and the atom wraps them instead of clipping (which is exactly what the fit
                            // audit above would have caught as an overflow). The floor is the assertion.
                            Assert(cell.height >= PresetHeightFloor - Tolerance,
                                "each preset must be at least the density row height at " + width + " ("
                                + language + "), got " + Num(cell.height));
                            Assert(cell.height <= PresetHeightCeil + Tolerance,
                                "and no taller than a wrapped caption can justify at " + width + " ("
                                + language + "), got " + Num(cell.height));
                            if (i > 0)
                            {
                                Rect previous = RectOf(snapshot, PresetIds[i - 1]);
                                Assert(Math.Abs((cell.x - previous.xMax) - PresetGap) <= Tolerance,
                                    "the row's declared Gap must separate presets at " + width + " ("
                                    + language + "), got " + Num(cell.x - previous.xMax));
                            }
                        }

                        Console.WriteLine("[attenuation] " + width + " " + language + " card=" + Num(card.height)
                            + "/" + Num(expectedCard) + " body=" + Num(body.height) + "/" + Num(expectedBody)
                            + " chart=" + Num(chart.height) + " status=" + Num(status.height)
                            + " presetW=" + Num(first.width));
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
    // Plumbing
    // ---------------------------------------------------------------------------------------------

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        host.Bindings.Invoke("set-tab", "Distance");
        host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        return host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
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
            throw new InvalidOperationException("DeclarativeAttenuationLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}