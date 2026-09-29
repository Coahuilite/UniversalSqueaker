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
/// R12-P / R12-T: US's palette contract against the carrier's CURRENT theme seam, asserted through a real
/// production host and the stub's own DRAW recorders rather than through cached properties.
///
/// <list type="number">
/// <item><b>US owns its complete baseline (R12-P.1).</b> The carrier ships <c>Vanilla</c> only, so the US
/// palette declares every colour the retired warm-gold template used to contribute, and no token a US
/// surface renders still answers a Vanilla value. Every colour US renders is owned; the ONE documented
/// exception is <c>SelectedBorder</c>, whose fallback is the ACCENT rather than a structural token.</item>
/// <item><b>Two clocks, and a palette only touches one of them (R12-P.3 / R12-P.4).</b> A colour assignment
/// moves <see cref="UiTheme.ColourRevision"/> and must NOT move <see cref="UiTheme.LayoutRevision"/>; a font
/// or a density move is the other way round. That disjointness is what lets a re-tint repaint a page without
/// re-measuring it, and it is asserted at the seam rather than assumed.</item>
/// <item><b>A colour-only re-tint reaches a SCOPED region (R12-P.3).</b> The carrier caches one clone per
/// effective (scheme, density) pair and that clone copies token VALUES, so a re-tint used to leave a styled
/// region painting its old palette. The cache now expires on either clock, so the clone is rebuilt with the
/// new palette while the scope's own overrides still win - and the layout clock does not move.</item>
/// <item><b>The repaint is observable in a DRAW, the arrangement is REUSED, and the session's own bookkeeping
/// is untouched (R12-P.3). The claim is about painted output and a re-arranged tree, never about a property
/// read back after an assignment.</b> The lane splits the two instruments deliberately: a recorded draw for
/// "the palette reached the paint", an identical rect map plus an unmoved arrangement clock for "no layout
/// moved", and the session's own state read back for "a colour is not a structural event". It does NOT claim
/// all-state redraw proof; the carrier's resolver/popup lanes own that.</item>
/// <item><b>Missing colour vs explicit transparent vs unclaimed edge (R12-P.2).</b> A token the document omits
/// answers the carrier's Vanilla value; an explicitly assigned transparent stays transparent; an unclaimed
/// per-surface edge keeps its documented "use the shared token" meaning.</item>
/// </list>
///
/// <para>
/// NOT A HUMAN GATE. This foundation slice has none: the harness cannot render pixels, and no build or run
/// happened in this session, so every claim below is a static reading of the carrier/US sources plus a
/// written lane. The lane's own runtime behaviour is (d) UNVERIFIED.
/// </para>
/// </summary>
internal static class UsPaletteLaneTests
{
    private const string FlatScheme = "us-flat-panel";

    /// <summary>
    /// The colour-only palette this lane switches to. Every slot it declares is a DIFFERENT colour from the
    /// US palette, so a half-applied document reddens instead of passing on a coincidence. Deliberately
    /// colour-only: no <c>&lt;Font&gt;</c> and no <c>&lt;Metric&gt;</c>, because those are the layout-bearing
    /// axes and the whole point is that a palette does not carry them.
    /// </summary>
    private const string AltPaletteXml =
        @"<Styles Schema='1' Scheme='us-alt'>"
        + @"<Scheme Name='us-alt'>"
        + @"<Color Token='Raised' Value='#123456' />"
        + @"<Color Token='Panel' Value='#123456' />"
        + @"<Color Token='WorkspacePlane' Value='#123456' />"
        + @"</Scheme></Styles>";

    /// <summary>
    /// A minimal but REAL host: the production parser, engine, drawing outlets and theme. It declares no
    /// widget of US's own, so it exists only to answer "does the palette reach a draw call"; the settings page
    /// answers the geometry and session questions.
    /// </summary>
    private const string ProbeManifest =
        "<UiPage Schema=\"2\" Source=\"coahuilite.us.palette.lane\">"
        + "<Column Id=\"probe-root\" Padding=\"0\" Gap=\"0\">"
        + "<Widget Id=\"probe-keyed\" Kind=\"text/wrapped\" TextKey=\"US.Tuning.EasterEggs\" />"
        + "<Widget Id=\"probe-literal\" Kind=\"text/wrapped\" Text=\"probe panel literal\" />"
        + "</Column></UiPage>";

    public static int RunAll()
    {
        Step("the US palette is complete, pure and issue-free", ThePaletteIsCompleteAndPure);
        Step("a colour moves only the colour clock; a font moves only the layout clock", TheClocksAreDisjoint);
        Step("a colour-only re-tint repaints a scoped region and moves no layout", ARetintRepaintsScopedPaint);
        Step("a re-tint reaches a DRAW and reuses the arrangement", TheRetintIsDrawnAndReusesTheArrangement);
        Step("a palette change leaves the session's own state alone", APaletteChangeLeavesSessionStateAlone);
        Step("missing colour, explicit transparent and an unclaimed edge are three answers", MissingTransparentAndUnclaimedEdge);
        Console.WriteLine("UsPaletteLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // 1. the palette itself
    // ---------------------------------------------------------------------------------------------

    private static void ThePaletteIsCompleteAndPure()
    {
        Assert(UsTheme.SchemeIssues.Count == 0,
            "the embedded US palette must load with no issues (an unknown token, an unreadable colour, a"
            + " duplicate or a legacy font redirect all land here): " + string.Join(" | ", UsTheme.SchemeIssues));

        // PURE COLOUR, as text: no <Font>, no density metric. Both are layout-bearing, so a palette carrying
        // them would make "switch the palette" silently re-measure the page.
        Assert(!UsTheme.SchemeXml.Contains("<Font"),
            "the palette must not declare a font: typography is a pre-measure axis, not a palette entry");
        Assert(!UsTheme.SchemeXml.Contains("<Density") && !UsTheme.SchemeXml.Contains("<Metric"),
            "the palette must not declare a density or a metric: geometry belongs to the layout document");

        // COMPLETENESS, as text. The carrier's document API does not publish its colour map, so this half is a
        // check on the constant; the RUNTIME half is the "no Vanilla value survives" assertion below, taken on
        // a real theme. Two instruments over the same claim, deliberately.
        foreach (string token in UsTheme.ColorTokens)
        {
            Assert(UsTheme.SchemeXml.Contains("Token='" + token + "'"),
                "the palette's own token list names '" + token + "' but the document does not declare it -"
                + " one of the two is stale");
        }

        // The one documented exception, asserted so it cannot be "fixed" by accident: SelectedBorder stays
        // unclaimed, because its fallback IS the accent (SelectedBorder ?? AccentGold) and a flat scope aliases
        // that fallback away to express "no box".
        Assert(!UsTheme.ColorTokens.Contains("SelectedBorder"),
            "SelectedBorder must stay OUT of the declared token list: it is the one surface whose fallback is"
            + " the accent, and a flat scope is allowed to alias it away");
        Assert(!UsTheme.SchemeXml.Contains("Token='SelectedBorder'"),
            "and the document must not declare it either, or the accent fallback is gone");

        // The identity accent, pinned twice: the constant the document is authored with, and the colour the
        // resolver actually produced from it.
        Assert(UsTheme.AccentGoldHex == "#d19a38",
            "the identity accent constant must stay the series gold #d19a38, got '" + UsTheme.AccentGoldHex + "'");
        UiTheme us = UsTheme.Surface();
        Assert(SameColor(us.AccentGold, Rgb(0xd1, 0x9a, 0x38)),
            "the identity accent must resolve to #d19a38 - the value the retired carrier template carried, so"
            + " the accent did not move with the ownership (RULING 2026-09-24)");

        // NO VANILLA VALUE SURVIVES on a US surface, checked against UiTheme.Vanilla rather than against a list
        // of literals: the property is "nothing is inherited", not "these hexes are right".
        UiTheme vanilla = UiTheme.Vanilla;
        (string Name, Color Mine, Color Fallback)[] colours =
        {
            ("Base", us.Base, vanilla.Base),
            ("WorkspacePlane", us.WorkspacePlane, vanilla.WorkspacePlane),
            ("Panel", us.Panel, vanilla.Panel),
            ("SectionBand", us.SectionBand, vanilla.SectionBand),
            ("Raised", us.Raised, vanilla.Raised),
            ("Hover", us.Hover, vanilla.Hover),
            ("Selected", us.Selected, vanilla.Selected),
            ("Success", us.Success, vanilla.Success),
            ("Danger", us.Danger, vanilla.Danger),
            ("Border", us.Border, vanilla.Border),
            ("BorderStrong", us.BorderStrong, vanilla.BorderStrong),
            ("Divider", us.Divider, vanilla.Divider),
            ("TextPrimary", us.TextPrimary, vanilla.TextPrimary),
            ("TextSecondary", us.TextSecondary, vanilla.TextSecondary),
            ("TextDisabled", us.TextDisabled, vanilla.TextDisabled),
            ("TextOnGold", us.TextOnGold, vanilla.TextOnGold),
            ("TextOnDanger", us.TextOnDanger, vanilla.TextOnDanger),
            ("AccentGold", us.AccentGold, vanilla.AccentGold),
        };

        foreach ((string name, Color mine, Color fallback) in colours)
        {
            Assert(!SameColor(mine, fallback),
                "'" + name + "' is still answering the carrier's Vanilla value (" + Hex(fallback) + "), which"
                + " is the inheritance R12-P removes: the US palette must own every colour US renders");
        }

        // DangerBorder is the palette's sixth nullable-owned slot, and it is asserted as an EXPLICIT VALUE
        // rather than through the generic sweep, for two reasons the sweep cannot express: it is a nullable
        // edge (so "ownership" means CLAIMED, not "different"), and a palette may legitimately declare an
        // edge whose value equals Vanilla's - asserting inequality as ownership would make such a palette red.
        // Here the declared value and Vanilla's differ, so both halves are asserted for what they are.
        Assert(us.DangerBorder.HasValue,
            "DangerBorder is in UsTheme.ColorTokens, so the palette must CLAIM it: an unclaimed edge answers"
            + " the carrier template's own value and the ownership claim would be false");
        Assert(SameColor(us.DangerBorder!.Value, Rgb(0xc9, 0x60, 0x57)),
            "DangerBorder must be the saturated edge the S6-3 warm palette adopted (#c96057), got "
            + Hex(us.DangerBorder!.Value));
        Assert(!SameColor(us.DangerBorder!.Value, vanilla.DangerBorder ?? vanilla.Danger),
            "and it must not be the carrier's own danger edge (" + Hex(vanilla.DangerBorder ?? vanilla.Danger)
            + "); this inequality is a fact about these two palettes and is asserted HERE rather than as an"
            + " ownership rule (see the comment above)");

        // The per-surface edges the retired template left to the `?? Border` / `?? BorderStrong` fallback are
        // CLAIMED here, at the colour US's own shared tokens give them. The assertions below pin the AGREEMENT
        // rather than the hexes, deliberately: the authoritative statement is "US's page-level surfaces are
        // edged by US's own structural tokens", so an edit that moves `Border` without moving the four claimed
        // edges (or the reverse) reddens and has to be reconciled on purpose. The values themselves are the
        // ones US painted at HEAD, where the edges were unclaimed and resolved to exactly these colours.
        Assert(us.BaseBorder.HasValue && us.PanelBorder.HasValue && us.RaisedBorder.HasValue
            && us.HoverBorder.HasValue,
            "the four per-surface edges must be CLAIMED by the US palette, or each answers a carrier template"
            + " value nobody chose (base=" + (us.BaseBorder.HasValue ? Hex(us.BaseBorder.Value) : "(unclaimed)")
            + " panel=" + (us.PanelBorder.HasValue ? Hex(us.PanelBorder.Value) : "(unclaimed)")
            + " raised=" + (us.RaisedBorder.HasValue ? Hex(us.RaisedBorder.Value) : "(unclaimed)")
            + " hover=" + (us.HoverBorder.HasValue ? Hex(us.HoverBorder.Value) : "(unclaimed)") + ")");
        Assert(SameColor(us.BaseBorder!.Value, us.Border) && SameColor(us.PanelBorder!.Value, us.Border)
            && SameColor(us.RaisedBorder!.Value, us.Border),
            "and Base/Panel/Raised must carry US's own shared Border value (" + Hex(us.Border) + ") - the"
            + " colour they resolved to through `?? Border` in the committed look (base="
            + Hex(us.BaseBorder!.Value) + " panel=" + Hex(us.PanelBorder!.Value) + " raised="
            + Hex(us.RaisedBorder!.Value) + ")");
        Assert(SameColor(us.HoverBorder!.Value, us.BorderStrong),
            "and the hover edge must carry US's own BorderStrong value (" + Hex(us.BorderStrong) + "), its"
            + " `?? BorderStrong` answer (got " + Hex(us.HoverBorder!.Value) + ")");
        Assert(!us.SelectedBorder.HasValue,
            "SelectedBorder must stay UNCLAIMED so the accent fallback (SelectedBorder ?? AccentGold) survives");

        // The two surfaces whose edge fallback is a DIFFERENT token are asserted as such, because "unclaimed"
        // there is the documented contract rather than an omission: SuccessSurface answers `?? BorderStrong`
        // and DangerSurface answers `?? Danger`.
        Assert(!us.SuccessBorder.HasValue
            && SameColor(us.SuccessSurface.Border, us.BorderStrong),
            "an unclaimed SuccessBorder must still resolve to the shared BorderStrong ("
            + Hex(us.SuccessSurface.Border) + " vs " + Hex(us.BorderStrong) + ")");
        Assert(SameColor(us.DangerSurface.Border, us.DangerBorder!.Value),
            "and a claimed DangerBorder must be the edge the Danger surface paints ("
            + Hex(us.DangerSurface.Border) + " vs " + Hex(us.DangerBorder!.Value) + ")");

        // The font is still a CARRIER default, and that is correct: the palette is colour-only, so the font
        // comes from the library baseline (Small). Named here so a future move of that baseline is visible
        // rather than silent.
        Assert(us.DefaultFont == UiFont.Small,
            "the US palette declares no font, so the default must be the carrier baseline's Small, got "
            + us.DefaultFont + " - if that moved, US's typography moved with it");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. the two clocks
    // ---------------------------------------------------------------------------------------------

    private static void TheClocksAreDisjoint()
    {
        UiTheme theme = UsTheme.Surface();
        int layout = theme.LayoutRevision;
        int colour = theme.ColourRevision;

        // (a) a colour assignment: the colour clock only.
        theme.Raised = Rgb(0x12, 0x34, 0x56);
        Assert(theme.ColourRevision > colour,
            "assigning a colour must move the colour clock, or a repaint could not be signalled at all");
        Assert(theme.LayoutRevision == layout,
            "and it must NOT move the layout clock: that is what keeps a re-tint from re-measuring the page ("
            + layout + " -> " + theme.LayoutRevision + ")");

        // (b) re-assigning the SAME colour: neither clock. The carrier's setters compare before they mark, so
        // a palette re-applied every frame is idempotent.
        int colourAfter = theme.ColourRevision;
        int layoutAfter = theme.LayoutRevision;
        theme.Raised = Rgb(0x12, 0x34, 0x56);
        Assert(theme.ColourRevision == colourAfter && theme.LayoutRevision == layoutAfter,
            "re-assigning the value a token already holds must move NEITHER clock (colour " + colourAfter
            + " -> " + theme.ColourRevision + ", layout " + layoutAfter + " -> " + theme.LayoutRevision + ")");

        // (c) font and density: the layout clock only. Both are pre-measure axes, so both must invalidate the
        // arrangement - R12-P.4's "resolved before measure" half.
        int colourBeforeFont = theme.ColourRevision;
        int layoutBeforeFont = theme.LayoutRevision;
        theme.DefaultFont = UiFont.Medium;
        Assert(theme.LayoutRevision > layoutBeforeFont,
            "a font change must move the LAYOUT clock: typography is pre-measure style, and both the label"
            + " outlet and text measurement read the theme, so a stale arrangement would draw the old size");
        Assert(theme.ColourRevision == colourBeforeFont,
            "and a font change must not move the colour clock - the two axes are separate");

        int layoutBeforeGeometry = theme.LayoutRevision;
        theme.Geometry = new UiGeometry(9f, 9f, 9f, 33f, 3f);
        Assert(theme.LayoutRevision > layoutBeforeGeometry,
            "a density change must move the LAYOUT clock too: it moves every control's inner inset");

        // (d) the US palette itself is colour-only, so applying it moves the colour clock and leaves the layout
        // clock where it was - measured on a theme that already carries a NON-default font and density, which is
        // the only case where a palette carrying defaults of its own could have shown up.
        UiTheme probe = UiTheme.Vanilla;
        probe.DefaultFont = UiFont.Medium;
        probe.Geometry = new UiGeometry(9f, 9f, 9f, 33f, 3f);
        UiFont fontBefore = probe.DefaultFont;
        UiGeometry geometryBefore = probe.Geometry;
        int probeLayout = probe.LayoutRevision;
        UsTheme.Configure(probe);
        Assert(probe.DefaultFont == fontBefore && probe.Geometry == geometryBefore,
            "selecting the US palette must apply NO font or density default of its own (font " + fontBefore
            + " -> " + probe.DefaultFont + ", rowHeight " + geometryBefore.RowHeight + " -> "
            + probe.Geometry.RowHeight + ")");
        Assert(probe.LayoutRevision == probeLayout,
            "and therefore it must not advance the layout clock (" + probeLayout + " -> "
            + probe.LayoutRevision + ")");

        // (e) the same through the resolver, which is the path a consumer actually uses.
        UiTheme documented = UsTheme.Surface();
        int documentedLayout = documented.LayoutRevision;
        int documentedColour = documented.ColourRevision;
        new UiStyleResolver(documented, UiStyleDocument.Parse(AltPaletteXml))
            .ApplyTo(documented);
        Assert(documented.LayoutRevision == documentedLayout,
            "a colour-only document must not advance the layout clock through the resolver either ("
            + documentedLayout + " -> " + documented.LayoutRevision + ")");
        Assert(documented.ColourRevision > documentedColour
            && SameColor(documented.Raised, Rgb(0x12, 0x34, 0x56)),
            "control: it DID change the colour, so the assertion above is not passing on a no-op (raised "
            + Hex(documented.Raised) + ")");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. a scoped region repaints, and no layout moves
    // ---------------------------------------------------------------------------------------------

    private static void ARetintRepaintsScopedPaint()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        UiTheme live = host.StyleResolver.Baseline;
        int layoutBefore = live.LayoutRevision;
        int colourBefore = live.ColourRevision;

        UiStyleResolver resolver = host.StyleResolver;
        UiTheme flat = resolver.ThemeFor(new[] { new UiStyleDeclaration(FlatScheme) });
        Assert(!ReferenceEquals(flat, live),
            "a scoped region must get a CLONE, never the injected bag itself - two regions painting each other"
            + " is the defect the clone exists to prevent");
        Assert(SameColor(flat.RaisedSurface.Border, flat.Raised),
            "the flat scope's own declaration (RaisedBorder == Raised) must reach the clone: fill "
            + Hex(flat.Raised) + " vs edge " + Hex(flat.RaisedSurface.Border));
        Color scopeRaised = flat.Raised;
        Assert(SameColor(flat.Panel, live.Panel),
            "control: the clone inherits the page's panel fill, so the panel is the token this step re-tints ("
            + Hex(flat.Panel) + " vs page " + Hex(live.Panel) + ")");

        // The re-tint, on the bag the host was injected with.
        Color tint = Rgb(0x12, 0x34, 0x56);
        live.Panel = tint;
        Assert(live.ColourRevision > colourBefore, "the re-tint must move the injected theme's colour clock");
        Assert(live.LayoutRevision == layoutBefore,
            "and must not move the layout clock (" + layoutBefore + " -> " + live.LayoutRevision + ")");

        UiTheme rebuilt = resolver.ThemeFor(new[] { new UiStyleDeclaration(FlatScheme) });
        Assert(!ReferenceEquals(rebuilt, flat),
            "the cached region clone must be DROPPED once the colour clock has moved - that is the stale-colour"
            + " fix, and the old instance is exactly what used to keep painting the old palette");
        Assert(SameColor(rebuilt.Panel, tint),
            "and the rebuilt clone must carry the new page palette (panel " + Hex(rebuilt.Panel) + " vs tint "
            + Hex(tint) + ")");
        Assert(SameColor(rebuilt.Raised, scopeRaised),
            "while the scope's OWN declaration still wins for the token it overrides (raised "
            + Hex(rebuilt.Raised) + " vs " + Hex(scopeRaised) + ")");

        UiTheme flatAgain = resolver.ThemeFor(new[] { new UiStyleDeclaration(FlatScheme) });
        Assert(ReferenceEquals(flatAgain, rebuilt),
            "and the rebuilt clone is cached again: a region must cost one theme, not one per frame");
        Assert(live.LayoutRevision == layoutBefore,
            "no part of the repaint may advance the layout clock (" + layoutBefore + " -> "
            + live.LayoutRevision + ")");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. the repaint is a DRAW, and the arrangement is reused
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Two claims, each with the instrument that can actually settle it, and NO third one.
    /// <para>
    /// <b>(a) A re-tint reaches a DRAW record.</b> A theme-property assertion proves nothing about paint, so a
    /// minimal but REAL host is drawn twice and the label ink the drawing outlet was handed is compared. The
    /// probe declares no widget of US's own, which is why it can be built here without the settings page.
    /// </para>
    /// <para>
    /// <b>(b) The arrangement is REUSED, and the scoped clone is not painted from a stale palette.</b> The
    /// production page is arranged, the layout clock and every arranged rect are captured, the injected bag is
    /// re-tinted, the scope is asked for its theme again, and the page is arranged again. The rect map must be
    /// unchanged (a colour is not a rect), the arrangement clock must not move (nothing committed a
    /// Measure/Structure batch), and the scoped clone must carry the new palette while keeping the scope's own
    /// overrides.
    /// </para>
    /// <para>
    /// <b>What this step deliberately does NOT claim</b> - the distinction the previous version of this lane
    /// blurred: it is NOT a proof that session/focus/popup state survives a REDRAW. The carrier owns that
    /// property and asserts it in its own resolver/popup lanes; a US lane cannot aim a redraw at a specific
    /// control (recorded: the drawing frame and the arranging frame are separate arrangements, so a computed
    /// press lands on the page but not on the control). The US-side session evidence is the next step, and it
    /// is scoped to exactly what a palette application can and cannot touch.
    /// </para>
    /// </summary>
    private static void TheRetintIsDrawnAndReusesTheArrangement()
    {
        // ---- (a) the recorded draw.
        UsKernelWidgetRegistrar.EnsureRegistered();
        UiTheme paper = UsTheme.Surface();
        using (UiHost probe = new UiHost(
            "coahuilite.us.palette.lane", UiLayoutManifest.Parse(ProbeManifest), new UiBindings(), paper,
            new Program.StubMetrics(), new Program.StubTranslation()))
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
            try
            {
                ClearSolids();
                ClearLabels();
                probe.DrawChecked(new Rect(0f, 0f, 300f, 120f));
                List<Color> inkBefore = RecordedLabelColours();
                int solidsBefore = Recorded("DrawBoxSolidColors").Count;
                Assert(inkBefore.Count >= 2,
                    "the probe page must draw both of its text widgets, or this step cannot observe a repaint;"
                    + " recorded " + inkBefore.Count + " label colours");

                // Two palette tokens are moved: TextPrimary because it is the one that reaches the LABEL outlet
                // (so its movement is what proves the ink came from the re-tinted theme), and Panel because a
                // plane move is the other half of a palette change. The assertion is on the LABEL ink and not on
                // Panel precisely because the recorded channel is what the outlet was handed.
                paper.TextPrimary = Rgb(0x12, 0x34, 0x56);
                paper.Panel = Rgb(0x12, 0x34, 0x56);
                ClearSolids();
                ClearLabels();
                probe.DrawChecked(new Rect(0f, 0f, 300f, 120f));
                List<Color> inkAfter = RecordedLabelColours();
                int solidsAfter = Recorded("DrawBoxSolidColors").Count;

                Assert(inkAfter.Count == inkBefore.Count,
                    "the same page must draw the same number of labels after a re-tint (" + inkBefore.Count
                    + " -> " + inkAfter.Count + ")");
                Assert(solidsAfter == solidsBefore,
                    "and the same number of solid fills (" + solidsBefore + " -> " + solidsAfter + ") - a"
                    + " repaint changes colours, not the drawing plan");

                bool moved = false;
                for (int i = 0; i < inkBefore.Count; i++)
                {
                    if (!SameColor(inkBefore[i], inkAfter[i])) moved = true;
                }

                Assert(moved,
                    "the re-tint must reach a DRAW: the label outlet was handed "
                    + string.Join(", ", Described(inkBefore)) + " before and "
                    + string.Join(", ", Described(inkAfter)) + " after. A palette change that moves no painted"
                    + " colour is not a repaint");
                Console.WriteLine("[palette-draw] solids=" + solidsBefore + " inks "
                    + string.Join(",", Described(inkBefore)) + " -> " + string.Join(",", Described(inkAfter)));
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }

        // ---- (b) the arrangement is reused and the scoped clone is not stale.
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        host.Bindings.Invoke("set-tab", "Overview");
        UiLayoutSnapshot before = Arrange(host);

        UiTheme live = host.StyleResolver.Baseline;
        UiStyleResolver resolver = host.StyleResolver;
        int layoutBefore = live.LayoutRevision;
        int contentBefore = host.Session.ContentRevision;
        UiTheme flatBefore = resolver.ThemeFor(new[] { new UiStyleDeclaration(FlatScheme) });
        Color scopeRaised = flatBefore.Raised;
        Color oldPanel = live.Panel;

        Dictionary<string, Rect> rectsBefore = CopyRects(before);
        Assert(rectsBefore.Count > 20,
            "the Overview page must arrange a real tree for this comparison, got " + rectsBefore.Count
            + " rects; a tiny map would make the equality below vacuous");

        Color tint = Rgb(0x12, 0x34, 0x56);
        live.Panel = tint;
        Assert(live.LayoutRevision == layoutBefore,
            "a palette-only re-tint must not move the layout clock (" + layoutBefore + " -> "
            + live.LayoutRevision + ")");
        Assert(!SameColor(tint, oldPanel),
            "control: the re-tint must actually change the panel (" + Hex(oldPanel) + " -> " + Hex(tint) + ")");

        UiTheme flatAfter = resolver.ThemeFor(new[] { new UiStyleDeclaration(FlatScheme) });
        Assert(SameColor(flatAfter.Panel, tint),
            "the scoped region must be rebuilt on the new palette, not painted from the old one (panel "
            + Hex(flatAfter.Panel) + " vs tint " + Hex(tint) + ")");
        Assert(SameColor(flatAfter.Raised, scopeRaised),
            "while the scope's own declaration still wins for the token it overrides (raised "
            + Hex(flatAfter.Raised) + " vs " + Hex(scopeRaised) + ")");

        UiLayoutSnapshot after = Arrange(host);
        Assert(host.Session.ContentRevision == contentBefore,
            "re-arranging after a palette-only change must not commit an arrangement batch ("
            + contentBefore + " -> " + host.Session.ContentRevision + ")");
        Dictionary<string, Rect> rectsAfter = CopyRects(after);
        Assert(rectsAfter.Count == rectsBefore.Count,
            "the same tree must arrange the same number of elements (" + rectsBefore.Count + " -> "
            + rectsAfter.Count + ")");
        foreach (KeyValuePair<string, Rect> entry in rectsBefore)
        {
            Assert(rectsAfter.TryGetValue(entry.Key, out Rect now),
                "the re-arrangement must still carry '" + entry.Key + "'");
            Assert(Close(now.x, entry.Value.x) && Close(now.y, entry.Value.y)
                && Close(now.width, entry.Value.width) && Close(now.height, entry.Value.height),
                "'" + entry.Key + "' must be arranged identically after a palette-only change: a colour is not"
                + " a rect (" + Describe(entry.Value) + " -> " + Describe(now) + ")");
        }

        Console.WriteLine("[palette-reuse] rects=" + rectsBefore.Count + " revision=" + contentBefore
            + " scopePanel=" + Hex(oldPanel) + " -> " + Hex(flatAfter.Panel));
    }

    // ---------------------------------------------------------------------------------------------
    // 4b. the session's own state, scoped to what a palette application can touch
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// WHAT THIS IS: the session-state half of the re-tint contract, asserted on the SESSION's own bookkeeping
    /// with the real page arranged. The carrier keys this state by node identity, and a colour assignment
    /// reaches none of it.
    /// <para>
    /// WHAT IT IS NOT, and the reason the previous version was wrong: this is <b>not</b> a redraw proof. It
    /// assigns two colours and then reads the state back, so on its own it would pass for a change that also
    /// tore the page down - which is precisely the claim it must not make. The redraw half lives in the step
    /// above (a recorded draw), and the "no state is lost across a REDRAW" property belongs to the carrier's
    /// own resolver/popup lanes, which can drive a popup through the widget that publishes it. Recorded here
    /// rather than papered over: a US lane cannot aim a redraw at one control, so the honest split is
    /// "draw evidence" + "session bookkeeping survives", and never one standing in for the other.
    /// </para>
    /// <para>
    /// The hover claim is read IMMEDIATELY after claiming, before any frame boundary: a claim is frame-stamped
    /// and <c>BeginFrame</c> deliberately retires a claim made in the previous pass, so reading it after
    /// another pass would measure the grace rule instead of this step's subject.
    /// </para>
    /// </summary>
    private static void APaletteChangeLeavesSessionStateAlone()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        host.Bindings.Invoke("set-tab", "Overview");
        Arrange(host);

        UiTheme live = host.StyleResolver.Baseline;

        // The one state the PALETTE HYGIENE requirement names explicitly: an active popup. It is opened
        // through the session's own API here and its identity/anchor are read back after the re-tint.
        host.Session.OpenPopup("us/palette-lane", new Rect(10f, 20f, 30f, 18f));
        UiNode? scrollNode = host.Session.GetNodeByElementId("content-scroll");
        Assert(scrollNode != null,
            "the content scroll's node must exist after an arrange, or this step cannot observe scroll state");
        host.Session.SetScrollPosition(scrollNode!, new Vector2(0f, 7f));
        UiNode? eggNode = host.Session.GetNodeByElementId("basic-egg-row");
        Assert(eggNode != null, "the egg row's node must exist after an arrange");

        host.Session.ClaimHover("us/basic-tuning/egg");
        string hoverBefore = host.Session.HoverClaim ?? "";

        string? popupBefore = host.Session.OpenPopupId;
        Rect anchorBefore = host.Session.OpenPopupAnchor ?? default;
        int contentRevisionBefore = host.Session.ContentRevision;
        int layoutBefore = live.LayoutRevision;
        Vector2 scrollBefore = host.Session.GetScrollPosition(scrollNode!);
        int hitLayersBefore = host.Session.HitLayers.Count;
        int nodesBefore = host.Session.ScrollPositions.Count;

        // NOT asserted here, and the gap is named rather than papered over: `UiSession.ActiveElement` is the
        // node whose Measure/Draw is running and it is `None` between elements - which is what a lane reads
        // before AND after this step. `None == None` passes for every implementation and would be a fifth
        // assertion that cannot fail, so it is deliberately absent. Focus OWNERSHIP and keyboard traversal are
        // not built in the carrier yet (its own `api-tiers.md` records that under `UiSession`), so a US lane
        // cannot observe them; the honest state is "not observable here", not "asserted".

        // The palette application, and only that.
        live.Panel = Rgb(0x12, 0x34, 0x56);
        live.Raised = Rgb(0x0a, 0x0b, 0x0c);

        Assert(live.LayoutRevision == layoutBefore,
            "a palette-only re-tint must not move the layout clock (" + layoutBefore + " -> "
            + live.LayoutRevision + ")");
        UiNode? eggAfter = host.Session.GetNodeByElementId("basic-egg-row");
        Assert(eggAfter != null && ReferenceEquals(eggAfter, eggNode),
            "the same element must keep the SAME node object across a palette change: node identity is what"
            + " per-element state and scroll positions hang off");
        Assert(host.Session.OpenPopupId == popupBefore && host.Session.OpenPopupId != null,
            "the session's open-popup id must survive a palette change, got '"
            + (host.Session.OpenPopupId ?? "(none)") + "' where '" + (popupBefore ?? "(none)") + "' was open");
        Assert(host.Session.OpenPopupAnchor.HasValue
            && Close(host.Session.OpenPopupAnchor!.Value.x, anchorBefore.x)
            && Close(host.Session.OpenPopupAnchor!.Value.y, anchorBefore.y),
            "and the session's published popup anchor must not move - a colour does not re-anchor a popup");
        Assert(host.Session.HitLayers.Count == hitLayersBefore,
            "and the owned hit stack must not gain or lose a layer (" + hitLayersBefore + " -> "
            + host.Session.HitLayers.Count + ")");
        Assert(host.Session.ScrollPositions.Count == nodesBefore,
            "and the session's scroll-state table must not gain or lose a node entry (" + nodesBefore + " -> "
            + host.Session.ScrollPositions.Count + ")");
        Assert((host.Session.HoverClaim ?? "") == hoverBefore && hoverBefore.Length > 0,
            "and the hover claim must not move, got '" + (host.Session.HoverClaim ?? "(none)") + "' where '"
            + hoverBefore + "' was claimed");
        Vector2 scrollAfter = host.Session.GetScrollPosition(scrollNode!);
        Assert(Close(scrollAfter.x, scrollBefore.x) && Close(scrollAfter.y, scrollBefore.y),
            "and the scroll position must not move");
        Assert(host.Session.ContentRevision == contentRevisionBefore,
            "and nothing committed an arrangement batch (" + contentRevisionBefore + " -> "
            + host.Session.ContentRevision + ")");

        Console.WriteLine("[palette-session] popup=" + popupBefore + " anchor=" + Describe(anchorBefore)
            + " hover=" + hoverBefore + " revision=" + contentRevisionBefore + " scroll=" + Num(scrollBefore.y));
    }

    // ---------------------------------------------------------------------------------------------
    // 5. missing vs explicit transparent vs unclaimed edge
    // ---------------------------------------------------------------------------------------------

    private static void MissingTransparentAndUnclaimedEdge()
    {
        // A document that declares ONE token transparent and omits the rest. The omitted ones must answer the
        // carrier's Vanilla values; the declared one must be a real, fully transparent colour.
        const string partialXml =
            @"<Styles Schema='1' Scheme='us-partial'>"
            + @"<Scheme Name='us-partial'>"
            + @"<Color Token='Base' Value='#00000000' />"
            + @"</Scheme></Styles>";

        Color vanillaSuccess = UiTheme.Vanilla.Success;

        UiTheme theme = UiTheme.Vanilla;
        new UiStyleResolver(theme, UiStyleDocument.Parse(partialXml)).ApplyTo(theme);

        // (a) the declared transparent token IS a transparent colour, not a request for the default.
        Assert(theme.Base.a == 0f,
            "an explicitly transparent token must resolve to alpha 0, got a="
            + theme.Base.a.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        // (b) the omitted token answers the carrier's Vanilla value.
        Assert(SameColor(theme.Success, vanillaSuccess),
            "a token the document OMITS must answer the carrier's Vanilla value - that is the missing-colour"
            + " default, and it is a different answer from an authored transparent (" + Hex(theme.Success)
            + " vs " + Hex(vanillaSuccess) + ")");
        // (c) the two are distinguishable: "missing" is not spelled as a transparent colour.
        Assert(theme.Success.a != 0f,
            "the missing-colour default must not be transparent, or 'omitted' and 'explicitly transparent'"
            + " would collapse into one answer");

        // (d) the edge half of the same contract: an edge set to NULL is "no override" and the surface falls
        // through to the shared token, while an explicitly transparent edge is a value that stays transparent.
        UiTheme edges = UiTheme.Vanilla;
        Color sharedBorder = edges.Border;
        edges.RaisedBorder = null;
        Assert(SameColor(edges.RaisedSurface.Border, sharedBorder),
            "an edge set to NULL must fall through to the shared Border token ("
            + Hex(edges.RaisedSurface.Border) + " vs " + Hex(sharedBorder) + ")");
        edges.RaisedBorder = new Color(0f, 0f, 0f, 0f);
        Assert(edges.RaisedSurface.Border.a == 0f,
            "while an EXPLICITLY transparent edge is a value and stays transparent - the two spellings mean"
            + " different things");
        Assert(SameColor(edges.Raised, UiTheme.Vanilla.Raised),
            "and assigning an edge must not move the fill it belongs to");

        // (e) the same contract on the real US palette: US claims its four edges (so they are values), omits
        // nothing (so no token answers Vanilla) and keeps SelectedBorder unclaimed (so the accent fallback is
        // live rather than aliased away at the page level).
        UiTheme us = UsTheme.Surface();
        Assert(us.SelectedBorder == null && SameColor(us.SelectedSurface.Border, us.AccentGold),
            "US's unclaimed SelectedBorder must still resolve to the accent ("
            + Hex(us.SelectedSurface.Border) + " vs accent " + Hex(us.AccentGold) + ")");
    }

    // ---------------------------------------------------------------------------------------------
    // Plumbing
    // ---------------------------------------------------------------------------------------------

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        host.MeasureAndArrange(new Vector2(1024f, 900f));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        return host.MeasureAndArrange(new Vector2(1024f, 900f));
    }

    /// <summary>
    /// A copy of one arrangement's rect map, so two arrangements can be compared key by key. Copied rather
    /// than held: the snapshot is the engine's own view and a later arrange may replace it.
    /// </summary>
    private static Dictionary<string, Rect> CopyRects(UiLayoutSnapshot snapshot)
    {
        var copy = new Dictionary<string, Rect>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, Rect> entry in snapshot.RectById) copy[entry.Key] = entry.Value;
        return copy;
    }

    private static string Describe(Rect rect)
    {
        return "(x=" + Num(rect.x) + " y=" + Num(rect.y) + " w=" + Num(rect.width) + " h=" + Num(rect.height) + ")";
    }

    private static string Num(float value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static IList Recorded(string fieldName)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "the runtime stub does not record '" + fieldName + "'; this lane cannot observe a draw and"
                + " must not pass silently");
        }

        var list = field.GetValue(null) as IList;
        if (list == null) throw new InvalidOperationException("the stub's '" + fieldName + "' recorder is not a list");
        return list;
    }

    private static List<Color> RecordedLabelColours()
    {
        IList recorded = Recorded("LabelColors");
        var colours = new List<Color>(recorded.Count);
        for (int i = 0; i < recorded.Count; i++)
        {
            if (recorded[i] is Color colour) colours.Add(colour);
        }

        return colours;
    }

    private static void ClearLabels()
    {
        // The stub records each label in parallel lists; keep their indices aligned for later lanes.
        Recorded("LabelTexts").Clear();
        Recorded("LabelRects").Clear();
        Recorded("LabelColors").Clear();
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

    private static List<string> Described(List<Color> colours)
    {
        var text = new List<string>(colours.Count);
        for (int i = 0; i < colours.Count; i++) text.Add(Hex(colours[i]));
        return text;
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

    private static Color Rgb(int r, int g, int b)
    {
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }

    private static string Hex(Color color)
    {
        return "#" + Ch(color.r) + Ch(color.g) + Ch(color.b) + Ch(color.a);
    }

    private static string Ch(float value)
    {
        return Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255)
            .ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("UsPaletteLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
