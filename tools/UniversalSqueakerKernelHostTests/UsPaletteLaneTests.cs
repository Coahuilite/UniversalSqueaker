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
/// surface renders still answers a Vanilla value. Every colour US renders is owned, including an
/// explicit <c>SelectedBorder</c> that preserves the former effective accent.</item>
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
/// <item><b>Missing colour vs explicit transparent (R12-P.2, migrated to 0.8).</b> A role the document omits
/// answers the carrier's default value; explicitly assigned transparent stays transparent. US declares
/// its concrete edges instead of relying on the retired nullable fallback.</item>
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
    // UIR-006: the region colour scopes this lane used to reach through (us-flat-panel) are retired;
    // the scoped-clone mechanics below now run on the unscoped region theme (the baseline clone the
    // resolver builds for every element of the flat page).

    /// <summary>
    /// The colour-only palette this lane switches to. Every slot it declares is a DIFFERENT colour from the
    /// US palette, so a half-applied document reddens instead of passing on a coincidence. Deliberately
    /// colour-only: no <c>&lt;Font&gt;</c> and no <c>&lt;Metric&gt;</c>, because those are the layout-bearing
    /// axes and the whole point is that a palette does not carry them.
    /// </summary>
    private const string AltPaletteXml =
        @"<Styles Schema='1' Scheme='us-alt'>"
        + @"<Scheme Name='us-alt'>"
        + @"<Palette Role='ControlSurface' Value='#123456' />"
        + @"<Palette Role='PanelSurface' Value='#123456' />"
        + @"<Palette Role='WorkspaceSurface' Value='#123456' />"
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
            Assert(UsTheme.SchemeXml.Contains("Role='" + token + "'"),
                "the palette's own token list names '" + token + "' but the document does not declare it -"
                + " one of the two is stale");
        }

        // 0.8 requires the former effective accent to be an explicit SelectedBorder declaration.
        // A flat scope can still override that concrete role to express "no box".
        Assert(UsTheme.ColorTokens.Contains("SelectedBorder"),
            "SelectedBorder must be explicitly declared: its former effective colour is"
            + " the accent, and a flat scope is allowed to alias it away");
        Assert(UsTheme.SchemeXml.Contains("Role='SelectedBorder'"),
            "the document must declare the selected edge explicitly in 0.8");

        // The identity accent, pinned twice: the constant and the colour the resolver produced. UIR-006
        // (WO-US-UI-REFACTOR-20261014) moved the value: the shipped default is now the official Core
        // palette, whose accent is the Bronze/Lark-family #B49C73 taken from the PM role table - the
        // pin therefore checks BOTH readings against the table itself, not against a remembered literal.
        string coreAccentHex = UsSwatches.Resolve(UsSwatches.DefaultId).RoleHexes[
            System.Array.IndexOf(UsTheme.ColorTokens as string[], "Accent")];
        Assert(UsTheme.AccentGoldHex == coreAccentHex,
            "the identity accent constant must equal the core role table's AccentGold '" + coreAccentHex
            + "', got '" + UsTheme.AccentGoldHex + "'");
        UiTheme us = UsTheme.Surface();
        Assert(SameColor(us[UiPaletteRole.Accent], Rgb(0xb4, 0x9c, 0x73)),
            "and the accent must resolve to the Core Bronze #B49C73 (got " + Hex(us[UiPaletteRole.Accent]) + ")");

        // NO VANILLA VALUE SURVIVES on a US surface, checked against UiTheme.Vanilla rather than against a list
        // of literals: the property is "nothing is inherited", not "these hexes are right".
        UiTheme vanilla = UiTheme.Vanilla;
        (string Name, Color Mine, Color Fallback)[] colours =
        {
            ("WindowSurface", us[UiPaletteRole.WindowSurface], vanilla[UiPaletteRole.WindowSurface]),
            ("WorkspaceSurface", us[UiPaletteRole.WorkspaceSurface], vanilla[UiPaletteRole.WorkspaceSurface]),
            ("PanelSurface", us[UiPaletteRole.PanelSurface], vanilla[UiPaletteRole.PanelSurface]),
            ("SectionSurface", us[UiPaletteRole.SectionSurface], vanilla[UiPaletteRole.SectionSurface]),
            ("ControlSurface", us[UiPaletteRole.ControlSurface], vanilla[UiPaletteRole.ControlSurface]),
            ("HoverSurface", us[UiPaletteRole.HoverSurface], vanilla[UiPaletteRole.HoverSurface]),
            ("SelectedSurface", us[UiPaletteRole.SelectedSurface], vanilla[UiPaletteRole.SelectedSurface]),
            ("SuccessSurface", us[UiPaletteRole.SuccessSurface], vanilla[UiPaletteRole.SuccessSurface]),
            ("DangerSurface", us[UiPaletteRole.DangerSurface], vanilla[UiPaletteRole.DangerSurface]),
            ("Border", us[UiPaletteRole.Border], vanilla[UiPaletteRole.Border]),
            ("StrongBorder", us[UiPaletteRole.StrongBorder], vanilla[UiPaletteRole.StrongBorder]),
            ("Divider", us[UiPaletteRole.Divider], vanilla[UiPaletteRole.Divider]),
            ("Foreground", us[UiPaletteRole.Foreground], vanilla[UiPaletteRole.Foreground]),
            ("SecondaryForeground", us[UiPaletteRole.SecondaryForeground], vanilla[UiPaletteRole.SecondaryForeground]),
            ("DisabledForeground", us[UiPaletteRole.DisabledForeground], vanilla[UiPaletteRole.DisabledForeground]),
            ("SelectedForeground", us[UiPaletteRole.SelectedForeground], vanilla[UiPaletteRole.SelectedForeground]),
            ("DangerForeground", us[UiPaletteRole.DangerForeground], vanilla[UiPaletteRole.DangerForeground]),
            ("Accent", us[UiPaletteRole.Accent], vanilla[UiPaletteRole.Accent]),
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
        Assert(us.Palette.IsDeclared(UiPaletteRole.DangerBorder),
            "DangerBorder is in UsTheme.ColorTokens, so the palette must CLAIM it: an unclaimed edge answers"
            + " the carrier template's own value and the ownership claim would be false");
        Assert(SameColor(us[UiPaletteRole.DangerBorder], Rgb(0xc9, 0x60, 0x57)),
            "DangerBorder must be the saturated edge the S6-3 warm palette adopted (#c96057), got "
            + Hex(us[UiPaletteRole.DangerBorder]));
        Assert(!SameColor(us[UiPaletteRole.DangerBorder], vanilla[UiPaletteRole.DangerBorder]),
            "and it must not be the carrier's own danger edge (" + Hex(vanilla[UiPaletteRole.DangerBorder])
            + "); this inequality is a fact about these two palettes and is asserted HERE rather than as an"
            + " ownership rule (see the comment above)");

        // The per-surface edges the retired template left to the `?? Border` / `?? BorderStrong` fallback are
        // CLAIMED here, at the colour US's own shared tokens give them. The assertions below pin the AGREEMENT
        // rather than the hexes, deliberately: the authoritative statement is "US's page-level surfaces are
        // edged by US's own structural tokens", so an edit that moves `Border` without moving the four claimed
        // edges (or the reverse) reddens and has to be reconciled on purpose. The values themselves are the
        // ones US painted at HEAD, where the edges were unclaimed and resolved to exactly these colours.
        Assert(us.Palette.IsDeclared(UiPaletteRole.WindowBorder) && us.Palette.IsDeclared(UiPaletteRole.PanelBorder) && us.Palette.IsDeclared(UiPaletteRole.ControlBorder)
            && us.Palette.IsDeclared(UiPaletteRole.HoverBorder),
            "the four per-surface edges must be CLAIMED by the US palette, or each answers a carrier template"
            + " value nobody chose (base=" + (us.Palette.IsDeclared(UiPaletteRole.WindowBorder) ? Hex(us[UiPaletteRole.WindowBorder]) : "(unclaimed)")
            + " panel=" + (us.Palette.IsDeclared(UiPaletteRole.PanelBorder) ? Hex(us[UiPaletteRole.PanelBorder]) : "(unclaimed)")
            + " raised=" + (us.Palette.IsDeclared(UiPaletteRole.ControlBorder) ? Hex(us[UiPaletteRole.ControlBorder]) : "(unclaimed)")
            + " hover=" + (us.Palette.IsDeclared(UiPaletteRole.HoverBorder) ? Hex(us[UiPaletteRole.HoverBorder]) : "(unclaimed)") + ")");
        Assert(SameColor(us[UiPaletteRole.WindowBorder], us[UiPaletteRole.Border]) && SameColor(us[UiPaletteRole.PanelBorder], us[UiPaletteRole.Border])
            && SameColor(us[UiPaletteRole.ControlBorder], us[UiPaletteRole.Border]),
            "and Base/Panel/Raised must carry US's own shared Border value (" + Hex(us[UiPaletteRole.Border]) + ") - the"
            + " colour they resolved to through `?? Border` in the committed look (base="
            + Hex(us[UiPaletteRole.WindowBorder]) + " panel=" + Hex(us[UiPaletteRole.PanelBorder]) + " raised="
            + Hex(us[UiPaletteRole.ControlBorder]) + ")");
        Assert(SameColor(us[UiPaletteRole.HoverBorder], us[UiPaletteRole.StrongBorder]),
            "and the hover edge must carry US's own BorderStrong value (" + Hex(us[UiPaletteRole.StrongBorder]) + "), its"
            + " `?? BorderStrong` answer (got " + Hex(us[UiPaletteRole.HoverBorder]) + ")");
        Assert(us.Palette.IsDeclared(UiPaletteRole.SelectedBorder),
            "SelectedBorder must explicitly preserve the former effective accent");

        // Explicit edges preserve the former effective treatment: success uses StrongBorder,
        // while danger uses its dedicated DangerBorder declaration.
        Assert(us.Palette.IsDeclared(UiPaletteRole.SuccessBorder)
            && SameColor(us.SuccessSurface.Border, us[UiPaletteRole.StrongBorder]),
            "the declared SuccessBorder must preserve the former StrongBorder treatment ("
            + Hex(us.SuccessSurface.Border) + " vs " + Hex(us[UiPaletteRole.StrongBorder]) + ")");
        Assert(SameColor(us.DangerSurface.Border, us[UiPaletteRole.DangerBorder]),
            "and a claimed DangerBorder must be the edge the Danger surface paints ("
            + Hex(us.DangerSurface.Border) + " vs " + Hex(us[UiPaletteRole.DangerBorder]) + ")");

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
        theme[UiPaletteRole.ControlSurface] = Rgb(0x12, 0x34, 0x56);
        Assert(theme.ColourRevision > colour,
            "assigning a colour must move the colour clock, or a repaint could not be signalled at all");
        Assert(theme.LayoutRevision == layout,
            "and it must NOT move the layout clock: that is what keeps a re-tint from re-measuring the page ("
            + layout + " -> " + theme.LayoutRevision + ")");

        // (b) re-assigning the SAME colour: neither clock. The carrier's setters compare before they mark, so
        // a palette re-applied every frame is idempotent.
        int colourAfter = theme.ColourRevision;
        int layoutAfter = theme.LayoutRevision;
        theme[UiPaletteRole.ControlSurface] = Rgb(0x12, 0x34, 0x56);
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
            && SameColor(documented[UiPaletteRole.ControlSurface], Rgb(0x12, 0x34, 0x56)),
            "control: it DID change the colour, so the assertion above is not passing on a no-op (raised "
            + Hex(documented[UiPaletteRole.ControlSurface]) + ")");
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
        UiTheme flat = resolver.ThemeFor(null);
        Assert(!ReferenceEquals(flat, live),
            "a scoped region must get a CLONE, never the injected bag itself - two regions painting each other"
            + " is the defect the clone exists to prevent");
        // UIR-006: with no region scheme left, the page theme's OWN role pair answers the clone - the
        // control that matters is "the clone carries the palette, edge and fill both".
        Assert(SameColor(flat.RaisedSurface.Border, flat[UiPaletteRole.ControlBorder]),
            "the clone's raised edge must answer the palette's RaisedBorder role, got "
            + Hex(flat.RaisedSurface.Border));
        Color scopeRaised = flat[UiPaletteRole.ControlSurface];
        Assert(SameColor(flat[UiPaletteRole.PanelSurface], live[UiPaletteRole.PanelSurface]),
            "control: the clone inherits the page's panel fill, so the panel is the token this step re-tints ("
            + Hex(flat[UiPaletteRole.PanelSurface]) + " vs page " + Hex(live[UiPaletteRole.PanelSurface]) + ")");
        Assert(!SameColor(scopeRaised, live[UiPaletteRole.Accent]),
            "the tinting token must differ from the accent so the override check below cannot pass by coincidence");

        // The re-tint, on the bag the host was injected with.
        Color tint = Rgb(0x12, 0x34, 0x56);
        live[UiPaletteRole.PanelSurface] = tint;
        Assert(live.ColourRevision > colourBefore, "the re-tint must move the injected theme's colour clock");
        Assert(live.LayoutRevision == layoutBefore,
            "and must not move the layout clock (" + layoutBefore + " -> " + live.LayoutRevision + ")");

        UiTheme rebuilt = resolver.ThemeFor(null);
        Assert(!ReferenceEquals(rebuilt, flat),
            "the cached region clone must be DROPPED once the colour clock has moved - that is the stale-colour"
            + " fix, and the old instance is exactly what used to keep painting the old palette");
        Assert(SameColor(rebuilt[UiPaletteRole.PanelSurface], tint),
            "and the rebuilt clone must carry the new page palette (panel " + Hex(rebuilt[UiPaletteRole.PanelSurface]) + " vs tint "
            + Hex(tint) + ")");
        Assert(SameColor(rebuilt[UiPaletteRole.ControlSurface], live[UiPaletteRole.ControlSurface]),
            "and with no region scheme left, every token of the rebuilt clone follows the page palette ("
            + Hex(rebuilt[UiPaletteRole.ControlSurface]) + " vs " + Hex(live[UiPaletteRole.ControlSurface]) + ")");

        UiTheme flatAgain = resolver.ThemeFor(null);
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
                paper[UiPaletteRole.Foreground] = Rgb(0x12, 0x34, 0x56);
                paper[UiPaletteRole.PanelSurface] = Rgb(0x12, 0x34, 0x56);
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
        UiTheme flatBefore = resolver.ThemeFor(null);
        Color scopeRaised = flatBefore[UiPaletteRole.ControlSurface];
        Color oldPanel = live[UiPaletteRole.PanelSurface];

        Dictionary<string, Rect> rectsBefore = CopyRects(before);
        Assert(rectsBefore.Count > 20,
            "the Overview page must arrange a real tree for this comparison, got " + rectsBefore.Count
            + " rects; a tiny map would make the equality below vacuous");

        Color tint = Rgb(0x12, 0x34, 0x56);
        live[UiPaletteRole.PanelSurface] = tint;
        Assert(live.LayoutRevision == layoutBefore,
            "a palette-only re-tint must not move the layout clock (" + layoutBefore + " -> "
            + live.LayoutRevision + ")");
        Assert(!SameColor(tint, oldPanel),
            "control: the re-tint must actually change the panel (" + Hex(oldPanel) + " -> " + Hex(tint) + ")");

        UiTheme flatAfter = resolver.ThemeFor(null);
        Assert(SameColor(flatAfter[UiPaletteRole.PanelSurface], tint),
            "the scoped region must be rebuilt on the new palette, not painted from the old one (panel "
            + Hex(flatAfter[UiPaletteRole.PanelSurface]) + " vs tint " + Hex(tint) + ")");
        Assert(SameColor(flatAfter[UiPaletteRole.ControlSurface], live[UiPaletteRole.ControlSurface]),
            "and every unoverridden token keeps following the page (raised "
            + Hex(flatAfter[UiPaletteRole.ControlSurface]) + " vs " + Hex(live[UiPaletteRole.ControlSurface]) + ")");

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
            + " scopePanel=" + Hex(oldPanel) + " -> " + Hex(flatAfter[UiPaletteRole.PanelSurface]));
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
        // UIR-001: this step reads a basic-tuning row, which now lives on the Tuning workspace.
        host.Bindings.Invoke("set-tab", "Tuning");
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
        int nodesBefore = host.Session.Identity;

        // NOT asserted here, and the gap is named rather than papered over: `UiSession.ActiveElement` is the
        // node whose Measure/Draw is running and it is `None` between elements - which is what a lane reads
        // before AND after this step. `None == None` passes for every implementation and would be a fifth
        // assertion that cannot fail, so it is deliberately absent. Focus OWNERSHIP and keyboard traversal are
        // not built in the carrier yet (its own `api-tiers.md` records that under `UiSession`), so a US lane
        // cannot observe them; the honest state is "not observable here", not "asserted".

        // The palette application, and only that.
        live[UiPaletteRole.PanelSurface] = Rgb(0x12, 0x34, 0x56);
        live[UiPaletteRole.ControlSurface] = Rgb(0x0a, 0x0b, 0x0c);

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
        Assert(host.Session.Identity == nodesBefore,
            "and the same session must retain ownership (" + nodesBefore + " -> "
            + host.Session.Identity + ")");
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
            + @"<Palette Role='WindowSurface' Value='#00000000' />"
            + @"</Scheme></Styles>";

        Color vanillaSuccess = UiTheme.Vanilla[UiPaletteRole.SuccessSurface];

        UiTheme theme = UiTheme.Vanilla;
        new UiStyleResolver(theme, UiStyleDocument.Parse(partialXml)).ApplyTo(theme);

        // (a) the declared transparent token IS a transparent colour, not a request for the default.
        Assert(theme[UiPaletteRole.WindowSurface].a == 0f,
            "an explicitly transparent token must resolve to alpha 0, got a="
            + theme[UiPaletteRole.WindowSurface].a.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        // (b) the omitted token answers the carrier's Vanilla value.
        Assert(SameColor(theme[UiPaletteRole.SuccessSurface], vanillaSuccess),
            "a token the document OMITS must answer the carrier's Vanilla value - that is the missing-colour"
            + " default, and it is a different answer from an authored transparent (" + Hex(theme[UiPaletteRole.SuccessSurface])
            + " vs " + Hex(vanillaSuccess) + ")");
        // (c) the two are distinguishable: "missing" is not spelled as a transparent colour.
        Assert(theme[UiPaletteRole.SuccessSurface].a != 0f,
            "the missing-colour default must not be transparent, or 'omitted' and 'explicitly transparent'"
            + " would collapse into one answer");

        // (d) the edge half of the same contract: an edge set to NULL is "no override" and the surface falls
        // through to the shared token, while an explicitly transparent edge is a value that stays transparent.
        UiTheme edges = UiTheme.Vanilla;
        Color sharedBorder = edges[UiPaletteRole.Border];
        edges.Palette.Remove(UiPaletteRole.ControlBorder);
        Assert(SameColor(edges.RaisedSurface.Border, sharedBorder),
            "removing a concrete edge declaration reveals that role's own current default ("
            + Hex(edges.RaisedSurface.Border) + " vs " + Hex(sharedBorder) + ")");
        edges[UiPaletteRole.ControlBorder] = new Color(0f, 0f, 0f, 0f);
        Assert(edges.RaisedSurface.Border.a == 0f,
            "while an EXPLICITLY transparent edge is a value and stays transparent - the two spellings mean"
            + " different things");
        Assert(SameColor(edges[UiPaletteRole.ControlSurface], UiTheme.Vanilla[UiPaletteRole.ControlSurface]),
            "and assigning an edge must not move the fill it belongs to");

        // (e) the real US palette declares every role, including the selected edge's former
        // effective accent; no nullable edge fallback is needed in 0.8.
        UiTheme us = UsTheme.Surface();
        Assert(us.Palette.IsDeclared(UiPaletteRole.SelectedBorder) && SameColor(us.SelectedSurface.Border, us[UiPaletteRole.Accent]),
            "US's explicit SelectedBorder must match the former effective accent ("
            + Hex(us.SelectedSurface.Border) + " vs accent " + Hex(us[UiPaletteRole.Accent]) + ")");
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
