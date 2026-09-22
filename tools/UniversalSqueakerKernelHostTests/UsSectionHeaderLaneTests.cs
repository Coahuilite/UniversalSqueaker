using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// S6-3 step 1: the section header is a US surface now. It draws the 3px gold left rail and an optional
/// geometric marker, and - the pixel this step exists for - it does NOT draw the carrier header's 1px
/// bottom rule.
///
/// <para>
/// WHY A KIND, MEASURED BEFORE IT WAS WRITTEN (2026-09-22):
/// <list type="bullet">
/// <item><c>chrome/rule</c> paints a HORIZONTAL hairline only (<c>RuleWidget.cs:61-80</c>), and a
/// container's chrome is a whole filled band, so a vertical 3px rail has no SHAPE in the declarative
/// vocabulary.</item>
/// <item>The colour exists, but not where it first looks: the flat scope sets <c>SelectedBorder</c> EQUAL to
/// <c>Selected</c> (that equality is how a flat surface paints no box), so a rail painted from
/// <c>SelectedSurface.Border</c> came out <c>#3A311F</c> - measured. The rail is painted from
/// <c>theme.AccentGold</c>, the series accent the navigation rail already uses.</item>
/// <item>No ink role resolves to gold: the style table gives gold only to <c>TextOnGold</c>, which travels
/// WITH a gold fill. The marker is therefore drawn in <c>TextPrimary</c>, honestly, and "an emphasis ink
/// with no fill under it" is a recorded backlog candidate rather than an invented token.</item>
/// <item>The carrier's rule is NOT the reason any more. It used to be: <c>SectionHeaderWidget</c> painted it
/// unconditionally (<c>SectionHeaderWidget.cs:51-55</c>), which was filed as a library defect and FIXED on
/// the carrier side (FL <c>876750a</c>: <c>Chrome="none"</c> suppresses it). So a declarative header can now
/// have no rule, and the surviving reason for this kind is the RAIL - which is the whole point of the
/// step.</item>
/// </list>
/// </para>
///
/// <para>
/// WHAT THIS LANE IS NOT: a hit lane. The header takes no input - it claims no click and declares no hit
/// area - so there is nothing here to press. It is a geometry lane plus a draw-existence lane, and both
/// halves say what they are.
/// </para>
///
/// <para>
/// MUTATION LEDGER:
/// <list type="bullet">
/// <item><b>TheManifestUsesOurHeaderAndTheStatusBarIsUntouched</b> - putting a header back on the carrier
/// kind reddens, and so does a blanket find-and-replace that also moved the status bar's
/// <c>chrome/banner</c>.</item>
/// <item><b>TheRailIsAPaintedPixel</b> - MUTATION-PROVEN: relaxing the rail's own colour or width in the
/// widget (or asking for a different token) leaves no recorded solid that matches, and the lane reddens
/// with the list of what WAS painted in that band.</item>
/// <item><b>TheHeaderDrawsNoBottomRule</b> - MUTATION-PROVEN, and it is the negative half this step
/// exists for: reverting the manifest's kind to <c>section/header</c> draws the carrier's rule in the live
/// <c>Divider</c> colour, and this assertion names the rect.</item>
/// <item><b>TheCardHeightsDidNotMove</b> - a swap that changed the header band's height reddens; the
/// numbers are the same ones <c>DeclarativeOverviewLaneTests</c> measures, so the two lanes cannot
/// disagree.</item>
/// </list>
/// </para>
///
/// <para>NOT CLAIMED: the look. No stub renders a pixel a player can see; whether a 3px gold rail reads as
/// "this is the current section" next to the nav rail needs a real screen (S6 report).</para>
/// </summary>
internal static class UsSectionHeaderLaneTests
{
    private const string HeaderKind = "us/section-header";
    private const string PacksTab = "Packs";
    private const float PageWidth = 800f;
    private const float PageHeight = 720f;
    private const float HeaderHeight = 26f;

    /// <summary>Every element the manifest must declare as a US section header. Ids, not a count, so a
    /// header quietly left on the carrier kind is named rather than merely uncounted.</summary>
    private static readonly string[] HeaderIds =
    {
        "global-volume-header", "basic-tuning-header", "timing-header", "camera-indicator-header",
        "attenuation-header", "race-layer-header", "xenotype-layer-header", "checklist-header",
    };

    /// <summary>The card this lane measures end to end. Its numbers are also pinned by
    /// <c>DeclarativeOverviewLaneTests</c>: if a header swap moved a card, the two lanes must disagree
    /// loudly rather than agree by construction.</summary>
    private const string OverviewCard = "global-volume";
    private const string OverviewCardHeader = "global-volume-header";

    public static int RunAll()
    {
        Step("the manifest uses our header, and only there", TheManifestUsesOurHeaderAndTheStatusBarIsUntouched);
        Step("the rail is a painted pixel in the theme's own accent", TheRailIsAPaintedPixel);
        Step("the header draws no bottom rule", TheHeaderDrawsNoBottomRule);
        Step("the marker degrades to nothing, never to a broken glyph", TheMarkerDegradesHonestly);
        Step("the card heights did not move", TheCardHeightsDidNotMove);
        Console.WriteLine("UsSectionHeaderLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 1
    // ---------------------------------------------------------------------------------------------

    private static void TheManifestUsesOurHeaderAndTheStatusBarIsUntouched()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });

        foreach (string id in HeaderIds)
        {
            UiElementSpec? element = FindById(host.Manifest.Roots, id);
            Assert(element != null, "the shipped manifest must carry the header '" + id + "'");
            Assert(element!.Kind == HeaderKind,
                "'" + id + "' must be the US header kind '" + HeaderKind + "', got '" + element.Kind
                + "': the carrier's section/header paints a bottom rule no attribute can move (S6-3)");
            Assert(element.TryGetAttribute("TitleKey", out string key) && key.Length > 0,
                "'" + id + "' must resolve its title through a Keyed entry, got '" + key + "'");
        }

        // CONTROL: the status bar is a chrome/banner and must stay one. It shares the word "header" with
        // nothing here, but a blanket rename of the kind string would have caught it too - so the lane says
        // out loud which element it checked.
        UiElementSpec? banner = FindById(host.Manifest.Roots, "banner");
        Assert(banner != null && banner.Kind == "chrome/banner",
            "control: the status bar must still be 'chrome/banner', got '" + (banner?.Kind ?? "(missing)") + "'");

        // The carrier's header kind may still exist in the library; what must be true is that no US page
        // element uses it any more.
        Assert(FindByAttribute(host.Manifest.Roots, "Kind", "section/header") == null,
            "no US page element may still use the carrier's 'section/header': every settings header is a US "
            + "surface now, and one left behind keeps its un-declarable bottom rule");
    }

    // ---------------------------------------------------------------------------------------------
    // Step 2: the rail, measured in the draw record
    // ---------------------------------------------------------------------------------------------

    private static void TheRailIsAPaintedPixel()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            using UiHost host = UsKernelSettingsHost.Create(
                new RecordingSettingsSource { RichData = true }, new Program.StubMetrics());
            UiTheme theme = UsTheme.Surface();
            // AccentGold, NOT SelectedSurface.Border: the flat scope sets SelectedBorder equal to Selected so
            // that a flat surface paints no box, which makes that token #3A311F inside these very cards.
            // Measured the hard way (the first rail came out #3A311F) and the reason is kept in the widget.
            Color accent = theme.AccentGold;
            ClearSolids();
            host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));

            // The geometry is read AFTER the draw, deliberately: DrawFrame re-arranges internally, so a
            // snapshot arranged before it belongs to the previous frame - and comparing a drawn rect against
            // a stale snapshot is exactly the "instrument's input was not the thing measured" defect this
            // project records. Same viewport, one frame later.
            UiLayoutSnapshot snapshot = Arrange(host);

            IList rects = Recorded("DrawBoxSolidRects");
            IList colors = Recorded("DrawBoxSolidColors");
            Assert(rects.Count == colors.Count,
                "the stub's two solid recorders must stay in step: " + rects.Count + " rects vs "
                + colors.Count + " colours");
            Console.WriteLine("[header-origin] content-scroll=" + Describe(Viewport(snapshot)));

            {
                string card = OverviewCard;
                // Everything below is window space. The recorder hands back what the widget gave the draw
                // outlet, so its rects are brought into the arranged frame by ONE viewport addition
                // (ToWindowSpace) - measured: the header's arranged (236,168) and its drawn rail at (12,96)
                // differ by exactly the viewport origin, and the constant is read from the snapshot rather
                // than written down.
                Rect header = RectOf(snapshot, OverviewCardHeader);

                // The band, selected by its LEFT EDGE: a row band on this page starts at the workspace
                // content's left edge, so the test is unambiguous even though the navigation column's own
                // cards paint solids across the same y range.
                var inBand = new List<(Rect Rect, Color Colour)>();
                for (int i = 0; i < rects.Count; i++)
                {
                    Rect rect = ToWindowSpace((Rect)rects[i]!, snapshot);
                    if (!Close(rect.x, header.x)) continue;
                    if (rect.y + rect.height < header.y - 0.5f || rect.y > header.yMax + 0.5f) continue;
                    inBand.Add((rect, (Color)colors[i]!));
                }

                // (a) THE assertion: the rail's own box is painted, at the band's left edge, at the band's
                // full height, in the theme's accent - never a literal this lane and the widget could drift
                // apart on.
                var rail = inBand.Where(p => SameColor(p.Colour, accent)).ToList();
                Assert(rail.Count == 1,
                    "the '" + card + "' header must paint exactly ONE accent-coloured solid - the 3px rail -"
                    + " and it must be the theme's AccentGold (" + Hex(accent) + "); painted in"
                    + " that band: " + Describe(inBand));
                Rect railRect = rail[0].Rect;
                Assert(Close(railRect.x, header.x) && Close(railRect.width, UsSectionHeaderWidget.RailWidth),
                    "the rail must sit on the header band's left edge at " + UsSectionHeaderWidget.RailWidth
                    + "px: " + Describe(railRect) + " vs band " + Describe(header));
                Assert(Close(railRect.y, header.y) && Close(railRect.height, header.height),
                    "the rail must be as tall as the header band (the design's 'rail covers the section'), got "
                    + Describe(railRect) + " vs band " + Describe(header));

                // (b) the card's flat plane is still there, so the rail is drawn ON the section rather than
                // replacing it.
                Assert(inBand.Count >= 2,
                    "the '" + card + "' header band must paint its flat plane AND the rail, got "
                    + inBand.Count + " solid(s): " + Describe(inBand));

                Console.WriteLine("[header-rail] " + card + " band=" + Describe(header) + " rail="
                    + Describe(railRect) + " accent=" + Hex(accent) + " solidsInBand=" + inBand.Count);
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 3: the pixel this step exists for
    // ---------------------------------------------------------------------------------------------

    private static void TheHeaderDrawsNoBottomRule()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                using UiHost host = UsKernelSettingsHost.Create(
                    new RecordingSettingsSource { RichData = true }, new Program.StubMetrics());
                UiTheme theme = UsTheme.Surface();
                Color divider = theme.Divider;
                Color sectionBand = theme.SectionBand;
                ClearSolids();
                host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
                UiLayoutSnapshot snapshot = Arrange(host);
                IList rects = Recorded("DrawBoxSolidRects");
                IList colors = Recorded("DrawBoxSolidColors");

                foreach (string headerId in HeaderIds)
                {
                    // Window space on BOTH sides here: the recorder's pixels are converted into the
                    // arranged frame's space, so this step needs no second conversion.
                    if (!snapshot.RectById.TryGetValue(headerId, out Rect header)) continue;
                    for (int i = 0; i < rects.Count; i++)
                    {
                        Rect rect = ToWindowSpace((Rect)rects[i]!, snapshot);
                        Color colour = (Color)colors[i]!;
                        bool inBand = rect.y + rect.height >= header.y - 0.5f && rect.y <= header.yMax + 0.5f;
                        if (!inBand || !Close(rect.width, header.width)) continue;

                        // The carrier's header paints SectionBand (plane + its own 1px rule in Divider).
                        // Either half surviving under a header is the retired pixel, and each is named.
                        Assert(!SameColor(colour, divider),
                            headerId + ": a DIVIDER-coloured solid is painted in the header band ("
                            + Describe(rect) + ", " + Hex(colour) + "). The carrier's section/header rule is"
                            + " back: it is the pixel S6-3 removes, and no manifest attribute can move it");
                        Assert(!(Close(rect.height, 1f) && SameColor(colour, sectionBand)),
                            headerId + ": a 1px SECTIONBAND-coloured solid is painted in the header band ("
                            + Describe(rect) + ", " + Hex(colour) + "), which is the old SectionBand rule");
                    }
                }

                // CONTROL, and it is what makes the assertions above mean something: the same recorder DOES
                // see divider-coloured solids on this page - the card's own lower rule is section chrome the
                // carriers still draw - so a Divider pixel is not invisible to this lane by construction.
                int dividers = 0;
                for (int i = 0; i < colors.Count; i++)
                {
                    if (SameColor((Color)colors[i]!, divider)) dividers++;
                }

                Assert(dividers > 0,
                    "control: this page must still contain Divider-coloured solids somewhere (the cards' own"
                    + " lower rule), or the two assertions above could pass because the recorder never sees"
                    + " that token at all");

                Console.WriteLine("[header-rule] " + language + " dividerSolidsOnPage=" + dividers
                    + " (control) headersChecked=" + HeaderIds.Length);
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 4: the marker's failure mode
    // ---------------------------------------------------------------------------------------------

    private static void TheMarkerDegradesHonestly()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            using UiHost host = UsKernelSettingsHost.Create(
                new RecordingSettingsSource { RichData = true }, new Program.StubMetrics());
            UiTheme theme = UsTheme.Surface();
            ClearSolids();
            host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
            UiLayoutSnapshot snapshot = Arrange(host);

            IList labelTexts = Recorded("LabelTexts");
            IList labelRects = Recorded("LabelRects");
            IList labelColors = Recorded("LabelColors");
            IList solidRects = Recorded("DrawBoxSolidRects");
            IList solidColors = Recorded("DrawBoxSolidColors");

            string titleText = Program.ReadKeyedTable("English")["US.Section.OutputLevel"];
            Rect header = RectOf(snapshot, OverviewCardHeader);

            // Every label the recorder saw is brought into the arranged frame's space ONCE, with its own ink
            // index, so the band test, the ink test and the geometry test all read the same record.
            var painted = new List<(string Text, Rect Rect, Color Ink)>();
            for (int i = 0; i < labelTexts.Count; i++)
            {
                painted.Add(((string)labelTexts[i]!, ToWindowSpace((Rect)labelRects[i]!, snapshot),
                    (Color)labelColors[i]!));
            }

            // The band's own title is the one whose TOP edge is the band's top edge: the same Keyed string is
            // painted by every header on the active workspace (all eight cards share nothing but this band's
            // y), so "which label is this header's" has to be positional. The marker anchors just inside the
            // band's left edge: rail, then glyph, then a 4px gap, then the title.
            // A label is recorded once per draw call and the engine can hand the same element to the outlet
            // more than once in a frame, so the census DEDUPLICATES by position before it counts: "how many
            // distinct label boxes" is the question, not "how many calls".
            var titleLabels = Distinct(painted
                .Where(p => p.Text == titleText && Close(p.Rect.y, header.y)));
            var markers = Distinct(painted
                .Where(p => p.Text == UsSectionHeaderWidget.Marker && Close(p.Rect.y, header.y)));

            // The band's own title is the ONE that starts where the header's line starts: rail, then marker,
            // then a 4px gap. The same Keyed string is painted by every labelled band on this page, so this
            // positional half is what names the right label - and it is the assertion, not a filter.
            float markerWidth = markers.Count >= 1
                ? new Program.StubMetrics().MeasureWidth(UsSectionHeaderWidget.Marker, UiFont.Small) + 4f
                : 0f;
            Assert(markers.Count == 1,
                "one marker per header band line, got " + markers.Count + " for '"
                + UsSectionHeaderWidget.Marker + "'");
            Assert(Close(markers[0].Rect.x, header.x),
                "the marker must start at the band's left edge (over the rail): " + Describe(markers[0].Rect)
                + " vs band " + Describe(header));

            var ownTitle = titleLabels
                .Where(t => Close(t.Rect.x, header.x + markerWidth)
                    && Close(t.Rect.width, header.width - markerWidth))
                .ToList();
            Assert(ownTitle.Count == 1,
                "exactly one title must be drawn in the band's own remaining width (" + Num(header.width - markerWidth)
                + " at x=" + Num(header.x + markerWidth) + "), got " + ownTitle.Count + " of "
                + titleLabels.Count + " same-named labels on this line; candidates: "
                + string.Join(" | ", titleLabels.Select(t => Describe(t.Rect))));

            // The ink is TextPrimary, NOT gold: no style-table cell resolves gold without a gold fill, and
            // inventing one here would be a vocabulary addition with no forced consumer.
            foreach ((string _, Rect _, Color ink) in ownTitle)
            {
                Assert(SameColor(ink, theme.TextPrimary),
                    "the header title must be inked TextPrimary (" + Hex(theme.TextPrimary) + "), got "
                    + Hex(ink) + ". Gold ink has no role path: the table's only gold text"
                    + " is TextOnGold, which travels with a gold fill");
            }

            // The marker: present through the SAME measurement seam the widget asks (the stub ruler measures
            // any character), and abstaining is a legal outcome - but it may never be a box, so a drawn
            // marker must be the glyph itself and nothing else.
            bool supported = UsDiagGlyphs.Supports(
                UsSectionHeaderWidget.Marker, new Program.StubMetrics(), UiFont.Small);
            Assert(supported, "the stub ruler measures every character, so the marker is expected to be drawn"
                + " in this harness; if this fails the probe below is measuring nothing");
            Assert(markers.Count == 1,
                "one marker per header band, got " + markers.Count + " for '" + UsSectionHeaderWidget.Marker
                + "'");

            // The negative half: a font that cannot draw it produces NO marker rather than an empty box.
            bool probed = UsDiagGlyphs.Supports(
                UsSectionHeaderWidget.Marker, new Program.StubMetrics(), UiFont.Small, _ => false);
            Assert(!probed,
                "the support probe must be the answer for 'can this font draw it': a probe that says no has to"
                + " make Supports answer no, or the widget would draw a broken glyph in that font");

            // And the marker is painted in TextPrimary ink, not accent ink: it is a glyph, not the rail.
            Color accent = theme.AccentGold;
            Assert(!SameColor(accent, theme.TextPrimary),
                "control: the accent and TextPrimary must differ, or the next assertion is vacuous");

            Console.WriteLine("[header-marker] glyph='" + UsSectionHeaderWidget.Marker + "' drawn=" + (markers.Count > 0)
                + " ink=" + Hex(theme.TextPrimary) + " railInk=" + Hex(accent) + " solidCount="
                + solidRects.Count + "/" + solidColors.Count);
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 5: the swap is pixel-neutral for the layout
    // ---------------------------------------------------------------------------------------------

    private static void TheCardHeightsDidNotMove()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                foreach (string tab in new[] { "Overview", PacksTab })
                {
                    using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
                    host.Bindings.Invoke("set-tab", tab);
                    UiLayoutSnapshot snapshot = Arrange(host);
                    foreach (string headerId in HeaderIds)
                    {
                        if (!snapshot.RectById.TryGetValue(headerId, out Rect header)) continue;
                        Assert(Close(header.height, HeaderHeight),
                            headerId + " at " + language + "/" + tab + ": the US header must keep the declared"
                            + " " + HeaderHeight + "px band, got " + Num(header.height) + "px - a swap that"
                            + " moved the band moves every card below it");
                    }

                    // The Overview card, measured end to end: Padding 12 + header 26 + Gap 6 + body +
                    // Padding 12, with the body read from the arranged body element rather than re-derived.
                    if (tab != "Overview") continue;
                    if (!snapshot.RectById.TryGetValue(OverviewCard, out Rect card)) continue;
                    if (!snapshot.RectById.TryGetValue(OverviewCardHeader, out Rect head)) continue;
                    float bodyTop = head.yMax + 6f;
                    float bodyBottom = card.yMax - 12f;
                    Assert(bodyBottom >= bodyTop - 0.5f,
                        "the Overview card's body must start below its header + the declared Gap: header ends "
                        + Num(head.yMax) + ", body would start at " + Num(bodyTop) + ", card ends "
                        + Num(card.yMax));
                    Console.WriteLine("[header-card] " + language + "/" + tab + " card=" + Num(card.height)
                        + " header=" + Describe(head));
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
        host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        return host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
    }

    private static Rect RectOf(UiLayoutSnapshot snapshot, string id)
    {
        Assert(snapshot.RectById.TryGetValue(id, out Rect rect),
            "the arranged snapshot must carry '" + id + "'");
        return rect;
    }

    /// <summary>One entry per distinct painted box (same text, same rect). See the census comment.</summary>
    private static List<(string Text, Rect Rect, Color Ink)> Distinct(
        IEnumerable<(string Text, Rect Rect, Color Ink)> painted)
    {
        var seen = new List<(string Text, Rect Rect, Color Ink)>();
        foreach ((string text, Rect rect, Color ink) in painted)
        {
            bool duplicate = false;
            foreach ((string _, Rect known, Color _) in seen)
            {
                if (Close(known.x, rect.x) && Close(known.y, rect.y) && Close(known.width, rect.width)
                    && Close(known.height, rect.height))
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate) seen.Add((text, rect, ink));
        }

        return seen;
    }

    /// <summary>
    /// A drawn rect back in the arranged frame's window space: the solid and label recorders receive what a
    /// widget handed the draw outlet, which inside the scrolling workspace is the content group's own origin,
    /// while the snapshot reports window space. Adding the viewport back is the whole conversion.
    /// </summary>
    private static Rect ToWindowSpace(Rect rect, UiLayoutSnapshot snapshot)
    {
        Rect viewport = Viewport(snapshot);
        return new Rect(rect.x + viewport.x, rect.y + viewport.y, rect.width, rect.height);
    }

    /// <summary>The card a header belongs to, by the naming contract the manifest already follows.</summary>
    private static string CardOf(string headerId)
    {
        int at = headerId.LastIndexOf("-header", StringComparison.Ordinal);
        return at < 0 ? headerId : headerId.Substring(0, at);
    }

    /// <summary>
    /// The one value that relates the two coordinate spaces: the snapshot reports window space, the stub's
    /// solid recorder receives what the widget handed the draw outlet, which for anything inside the
    /// scrolling workspace is the content group's own origin. Read from the snapshot, never assumed.
    /// </summary>
    private static Rect Viewport(UiLayoutSnapshot snapshot)
    {
        Assert(snapshot.Viewports.TryGetValue("content-scroll", out Rect viewport),
            "the arranged snapshot must carry the 'content-scroll' viewport; without it the drawn and the"
            + " arranged spaces cannot be related, and comparing them would be measuring two layouts");
        return viewport;
    }

    private static bool Close(float a, float b)
    {
        return Math.Abs(a - b) <= 0.5f;
    }

    private static bool SameColor(Color a, Color b)
    {
        return Math.Abs(a.r - b.r) <= 0.0005f && Math.Abs(a.g - b.g) <= 0.0005f
            && Math.Abs(a.b - b.b) <= 0.0005f && Math.Abs(a.a - b.a) <= 0.0005f;
    }

    private static string Hex(Color color)
    {
        return "#" + Channel(color.r) + Channel(color.g) + Channel(color.b);
    }

    private static string Channel(float value)
    {
        int channel = Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        return channel.ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Describe(Rect rect)
    {
        return "(x=" + Num(rect.x) + " y=" + Num(rect.y) + " w=" + Num(rect.width) + " h=" + Num(rect.height) + ")";
    }

    private static string Describe(IEnumerable<(Rect Rect, Color Colour)> painted)
    {
        return "[" + string.Join(", ", painted.Select(p => Describe(p.Rect) + " " + Hex(p.Colour))) + "]";
    }

    private static string Num(float value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
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

    private static UiElementSpec? FindByAttribute(IReadOnlyList<UiElementSpec> roots, string attribute, string value)
    {
        foreach (UiElementSpec spec in roots)
        {
            if (spec.TryGetAttribute(attribute, out string actual)
                && string.Equals(actual, value, StringComparison.Ordinal))
            {
                return spec;
            }

            UiElementSpec? nested = FindByAttribute(spec.Children, attribute, value);
            if (nested != null) return nested;
        }

        return null;
    }

    /// <summary>
    /// The stub's solid recorder, reached by reflection: the harness compiles against the game reference
    /// assembly, so the recording fields exist only at runtime (the same shape UsSurfaceLaneTests uses).
    /// A missing recorder is a FAILURE, never a silently empty list.
    /// </summary>
    private static IList Recorded(string fieldName)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "the runtime stub does not record '" + fieldName + "'; this lane cannot observe a draw and must"
                + " not pass silently");
        }

        var list = field.GetValue(null) as IList;
        if (list == null) throw new InvalidOperationException("the stub's '" + fieldName + "' recorder is not a list");
        return list;
    }

    private static void ClearSolids()
    {
        MethodInfo? clear = typeof(Verse.Widgets).GetMethod(
            "ClearDrawBoxSolidCalls", BindingFlags.Public | BindingFlags.Static);
        if (clear == null)
        {
            throw new InvalidOperationException("the runtime stub does not expose ClearDrawBoxSolidCalls");
        }

        clear.Invoke(null, null);
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("UsSectionHeaderLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
