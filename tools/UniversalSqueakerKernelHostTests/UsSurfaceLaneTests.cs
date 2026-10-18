using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// The US surface table and the two accent points the convergence pass moved off it.
/// <para>
/// Why the harness owns this: the table lives in the mod assembly, and the only way to observe what it
/// says is to reference the built assembly and ask. Text-level source scans would pin the numbers but
/// not that they reach a draw call, and the draw call is the claim — so the second half of this lane
/// paints through <see cref="UsKernelDraw"/> and reads back the fills the runtime stub recorded.
/// </para>
/// <para>
/// Colour comparison is component-wise on purpose: the harness's UnityEngine stub has no
/// <c>Color.op_Equality</c>, so <c>==</c> would either not compile or compare by reference and pass
/// vacuously.
/// </para>
/// </summary>
internal static class UsSurfaceLaneTests
{
    public static void RunAll()
    {
        Step("the US surface table carries the spec values and keeps the series gold", TableCarriesSpecValues);
        Step("a selected row takes no accent", SelectedRowTakesNoAccent);
        Step("a checked box is ink solid and takes no accent", CheckedBoxIsInkSolid);
        Step("the four row rails are four different marks, and only the current one takes the accent", RailStatesAreDistinguishable);
        Step("the four effect shapes are four different drawings", EffectShapesAreDistinguishable);
        Step("the two density tiers land on their own bags and do not bleed", DensityTiersDoNotBleed);
        Step("attention is the ruling cyan, not the danger alias and not the accent", AttentionIsTheRulingCyan);
        Step("the attention badge pairs its cyan fill with the dark ink, never the light one", AttentionBadgePairsCyanWithDarkInk);
        Step("an attention row takes the thin cyan edge and keeps the neutral fill", AttentionRailStaysThinCyanOnNeutralFill);
        Step("no unsanctioned attention-cyan literal exists outside UsAttention.cs", StrayCyanScanFindsOnlyTheOwner);
    }

    /// <summary>
    /// The library's density axis is a document that mutates the theme in place, so two windows sharing one
    /// bag would paint each other's rows. Two documents, two bags, two row heights - and the first bag keeps
    /// its value after the second resolves. The positive control is the fresh-instance assertion in
    /// <see cref="TableCarriesSpecValues"/>: caching <c>UsTheme.Surface()</c> into a static makes it fail,
    /// which is what turns "don't share the bag" from a comment into something that can go red.
    /// </summary>
    private static void DensityTiersDoNotBleed()
    {
        UiTheme regular = UsTheme.Surface();
        UiTheme dense = UsTheme.Surface();
        Assert(!ReferenceEquals(regular, dense), "two windows get two bags, so neither can move the other's density");

        UiStyleDocument tiers = UiStyleDocument.Parse(
            "<Styles Schema=\"1\" Density=\"regular\">"
            + "<Density Name=\"regular\"><Metric Token=\"RowHeight\" Value=\"24\" /></Density>"
            + "<Density Name=\"dense\"><Metric Token=\"RowHeight\" Value=\"20\" /></Density>"
            + "</Styles>");
        Assert(tiers.DensityNames.Count == 2, "both tiers must parse, found " + tiers.DensityNames.Count);

        new UiStyleResolver(regular, tiers).ApplyTo(regular);
        Assert(Math.Abs(regular.Geometry.RowHeight - 24f) < 0.01f,
            "the regular tier's RowHeight token lands on its own bag: " + regular.Geometry.RowHeight);

        UiStyleDocument denseOnly = UiStyleDocument.Parse(
            "<Styles Schema=\"1\" Density=\"dense\">"
            + "<Density Name=\"dense\"><Metric Token=\"RowHeight\" Value=\"20\" /></Density>"
            + "</Styles>");
        new UiStyleResolver(dense, denseOnly).ApplyTo(dense);
        Assert(Math.Abs(dense.Geometry.RowHeight - 20f) < 0.01f, "the dense tier is 20px: " + dense.Geometry.RowHeight);
        Assert(Math.Abs(regular.Geometry.RowHeight - 24f) < 0.01f,
            "resolving the dense document left the other bag at 24: density must not be a process-wide value");
    }

    /// <summary>
    /// Section 1.1's table, slot by slot. <b>R2 (2026-09-28): the table no longer starts from a carrier
    /// palette</b> - the carrier ships <c>Vanilla</c> and US declares every token, so this step asserts the
    /// whole table against literals and the accent against US's own pinned hex. The accent assertion used to
    /// read "must equal <c>UiTheme.DarkGold.AccentGold</c>", which made the identity a CARRIER fact: retiring
    /// the template would have turned a US test red with no US defect. The value is unchanged, so the
    /// assertion is re-cut at the same value with the same meaning - "the accent did not move" - rather than
    /// loosened.
    /// </summary>
    private static void TableCarriesSpecValues()
    {
        UiTheme theme = UsTheme.Surface();

        // The identity accent, pinned twice on purpose: the constant the document is authored with, and the
        // colour the resolver actually produced from it. A document edit that disagrees with the constant
        // reddens on the second, and a constant edited alone reddens on the first.
        // UIR-006 (WO-US-UI-REFACTOR-20261014): the shipped default is the OFFICIAL Core palette -
        // the table, not a remembered literal, is the authority. The accent is pinned twice against
        // it (the constant and the resolved colour) exactly as before; only the value moved.
        string coreAccent = UsSwatches.Resolve(UsSwatches.DefaultId).RoleHexes[
            System.Array.IndexOf(UsTheme.ColorTokens as string[], "AccentGold")];
        Assert(string.Equals(UsTheme.AccentGoldHex, coreAccent, StringComparison.OrdinalIgnoreCase),
            "the identity accent constant must equal the core role table's AccentGold " + coreAccent
            + ", got '" + UsTheme.AccentGoldHex + "'");
        Assert(Same(theme.AccentGold, Rgb(0xb4, 0x9c, 0x73)),
            "the identity accent must resolve to the Core Bronze #B49C73 from the PM role table");

        // The FULL 24-role table: every resolved token equals the Core role table's hex (the single
        // source the product ships), compared token by token so a drifted entry names itself.
        UsSwatch core = UsSwatches.Resolve(UsSwatches.DefaultId);
        for (int slot = 0; slot < UsTheme.ColorTokens.Count; slot++)
        {
            string token = UsTheme.ColorTokens[slot];
            Assert(Same(ResolvedToken(theme, token), ParseHex(core.RoleHexes[slot])),
                "core token '" + token + "' must resolve to " + core.RoleHexes[slot]);
        }

        // The structural promises that SURVIVE the palette change, restated as relationships, not
        // hexes: the interactive states must separate from the resting plane, and with Core's flat
        // fills (Base == WorkspacePlane == Raised by authorial design) the separation lives on the
        // EDGES - exactly what the region-scope retirement left to the palette.
        Assert(!Same(theme.Hover, theme.Raised), "hover must separate from the resting plane");
        Assert(!Same(theme.HoverSurface.Border, theme.RaisedSurface.Border),
            "the hover EDGE is what carries the state on Core - it must differ from the resting edge");
        Assert(!Same(theme.SelectedSurface.Border, theme.RaisedSurface.Border),
            "the selected EDGE (the accent fallback) must separate from the resting edge");
        Assert(!Same(theme.Panel, theme.Raised), "the panel plane must differ from the raised plane");
        Assert(theme.TextOnGold.a > 0f && !Same(theme.TextOnGold, theme.TextSecondary),
            "the ink on the selected plate is its own role value");

        UiTheme second = UsTheme.Surface();
        Assert(!ReferenceEquals(theme, second),
            "each caller gets its own bag: a host applies a document into it, so two windows must not share one");
    }

    /// <summary>
    /// The convergence pass: a selected row keeps the structural line. Two selected rows on one screen
    /// used to read as two current objects because both carried the accent.
    /// </summary>
    private static void SelectedRowTakesNoAccent()
    {
        UiTheme theme = UsTheme.Surface();
        ClearSolids();
        UsKernelDraw.RowSurface(new Rect(0f, 0f, 200f, 24f), theme, hovered: false, selected: true);

        IList solids = RecordedSolids();
        Assert(solids.Count >= 5, "one surface paints a fill and four edges; recorded " + solids.Count);
        Assert(Same(Colour(solids[0]), theme.Selected), "the selected row's plane is the selected token");
        for (int i = 1; i < 5; i++)
        {
            Assert(Same(Colour(solids[i]), theme.Border), "and its edge is the structural line");
        }

        Assert(!Painted(theme.AccentGold), "a selected row must not paint the accent at all");
    }

    /// <summary>
    /// The second convergence point: checked is a state, drawn as ink, not as a fourth accent.
    /// </summary>
    private static void CheckedBoxIsInkSolid()
    {
        UiTheme theme = UsTheme.Surface();
        ClearSolids();

        // Positive control for the property search below: two decoys in the ink token - one too small to be
        // the square, one square-sized but outside the box. A finder that keyed on position (the old
        // solids[5]/solids[6]) would land on a decoy and fail here; this one must step over both.
        UiThemeDraw.Solid(new Rect(0f, 0f, 4f, 4f), theme.TextPrimary);
        UiThemeDraw.Solid(new Rect(100f, 100f, UsKernelDraw.CheckboxVisual, UsKernelDraw.CheckboxVisual), theme.TextPrimary);

        Rect box = new Rect(0f, 0f, UsKernelDraw.CheckboxVisual, UsKernelDraw.CheckboxVisual);
        UsKernelDraw.Checkbox(box, theme, value: true);

        IList solids = RecordedSolids();
        Assert(FindCheckedSquare(solids, RecordedSolidRects(), theme.TextPrimary, box) >= 0,
            "the checked square is filled with the ink token (an ink solid, box-sized, inside the box) - "
            + "found by property and geometry, so one more rect anywhere cannot move the assertion");
        Assert(!Painted(theme.AccentGold), "a checked box must not paint the accent");
    }

    /// <summary>
    /// The index of the checked square: the ink token, at least 8px on a side (the check mark's cells are
    /// 2px), and inside the box the lane drew. Position is deliberately not part of the search key - a
    /// fixed index is what made this lane fail while the checkbox it judges was being changed, and the
    /// failure looked like a product regression rather than a lane that keys on the wrong thing.
    /// </summary>
    private static int FindCheckedSquare(IList solids, IList rects, Color ink, Rect box)
    {
        for (int i = 0; i < solids.Count && i < rects.Count; i++)
        {
            if (!Same(Colour(solids[i]), ink)) continue;
            if (rects[i] is not Rect rect) continue;
            if (rect.width < 8f || rect.height < 8f) continue;
            if (rect.x < box.x || rect.y < box.y || rect.xMax > box.xMax || rect.yMax > box.yMax) continue;
            return i;
        }

        return -1;
    }

    /// <summary>
    /// Spec 1.5, as four observable draws: none paints nothing, selected is dim ink at 3px, current is the
    /// accent at 3px, both at once paints both (accent inside the wider dim rail), and unavailable is a
    /// hatched row that still takes no accent. Geometry is asserted from the recorded rects, not from a
    /// picture, so "3px" and "5px" are numbers this lane can fail on.
    /// </summary>
    private static void RailStatesAreDistinguishable()
    {
        UiTheme theme = UsTheme.Surface();
        Rect row = new(0f, 0f, 200f, 24f);

        ClearSolids();
        UsKernelDraw.RowSurface(row, theme, hovered: false, UsKernelDraw.RowRail.None);
        Assert(!Painted(theme.AccentGold) && !Painted(theme.TextSecondary),
            "a plain row paints no rail at all");

        ClearSolids();
        UsKernelDraw.RowSurface(row, theme, hovered: false, UsKernelDraw.RowRail.Selected);
        Assert(RailIs(theme.TextSecondary, UsKernelDraw.RailWidth),
            "a selected row's rail is dim ink at the spec's 3px");
        Assert(!Painted(theme.AccentGold), "and a selected row still takes no accent");

        ClearSolids();
        UsKernelDraw.RowSurface(row, theme, hovered: false, UsKernelDraw.RowRail.Current);
        Assert(RailIs(theme.AccentGold, UsKernelDraw.RailWidth),
            "the current object's rail is the accent at the spec's 3px");

        ClearSolids();
        UsKernelDraw.RowSurface(row, theme, hovered: false, UsKernelDraw.RowRail.CurrentAndSelected);
        Assert(RailIs(theme.TextSecondary, UsKernelDraw.CurrentAndSelectedOuterWidth)
            && RailIs(theme.AccentGold, UsKernelDraw.RailWidth),
            "both states at once draw the dim rail and the accent rail inside it");

        ClearSolids();
        UsKernelDraw.RowSurface(row, theme, hovered: false, UsKernelDraw.RowRail.Disabled);
        Assert(Painted(theme.Panel), "an unavailable row is hatched in a plane token, not a new colour");
        Assert(!Painted(theme.AccentGold), "and it never takes the accent");
    }

    /// <summary>
    /// Spec 1.6, as four observable drawings: in effect is a solid ink disc, unavailable a dim stroke over a
    /// hatched fill with no ink disc, inherited a hollow ink outline with much less ink than the disc, and
    /// overridden the ink disc inside an accent ring. Shape differences are asserted as ink area and
    /// centre coverage, which is what a grey-scale screenshot would have to show.
    /// </summary>
    private static void EffectShapesAreDistinguishable()
    {
        UiTheme theme = UsTheme.Surface();
        Rect cell = new(0f, 0f, UsKernelDraw.ShapeSize, UsKernelDraw.ShapeSize);

        ClearSolids();
        UsKernelDraw.DrawStateShape(cell, theme, UsKernelDraw.StateShape.InEffect);
        Assert(Painted(theme.TextPrimary) && !Painted(theme.AccentGold),
            "in effect is a solid ink shape and takes no accent");
        Assert(CoversCentre(cell), "and it is solid at the centre");
        float discArea = InkArea();

        ClearSolids();
        UsKernelDraw.DrawStateShape(cell, theme, UsKernelDraw.StateShape.Unavailable);
        Assert(Painted(theme.TextSecondary) && Painted(theme.Panel),
            "unavailable is a dim stroke over a hatched fill");
        Assert(!Painted(theme.TextPrimary), "and it is not the in-effect disc");

        ClearSolids();
        UsKernelDraw.DrawStateShape(cell, theme, UsKernelDraw.StateShape.Inherited);
        Assert(Painted(theme.TextPrimary) && !Painted(theme.AccentGold),
            "inherited is an ink outline and takes no accent");
        Assert(!CoversCentre(cell), "hollow at the centre, which is what separates it from in effect");
        Assert(InkArea() < discArea * 0.6f, "and much less ink than the solid disc");

        ClearSolids();
        UsKernelDraw.DrawStateShape(cell, theme, UsKernelDraw.StateShape.Overridden);
        Assert(Painted(theme.TextPrimary) && Painted(theme.AccentGold),
            "overridden puts an accent ring around the ink disc");
        Assert(CoversCentre(cell), "and stays solid at the centre");
    }

    /// <summary>
    /// The palette ruling, pinned as literals. The attention hue has exactly one owner
    /// (<see cref="UsAttention.Brush"/>), so a lane that compared it against a theme token would be
    /// unable to tell the ruling's cyan from a repointed token - these compare the channels directly.
    /// <para>
    /// The alias half is the anti-trap property the ruling names: the carrier's <c>Warning</c> is a
    /// compatibility redirect onto <c>Danger</c>, so "Brush != Warning" would still pass if somebody
    /// repointed Warning to cyan and repainted every destructive control. The lane therefore asserts the
    /// redirect itself survives.
    /// </para>
    /// </summary>
    private static void AttentionIsTheRulingCyan()
    {
        UiTheme theme = UsTheme.Surface();
        Color cyan = Rgb(0x86, 0xE7, 0xD8);

        Assert(Same(UsAttention.Brush, cyan), "UsAttention.Brush must be exactly the ruling's #86E7D8");

        // Negative controls: without them an exact-value check could be a checker that accepts anything.
        // Each of these is a colour the ruling explicitly rejects for attention.
        Assert(!Same(Rgb(0xB4, 0x9C, 0x73), cyan), "negative control: the Core accent is not attention");
        Assert(!Same(Rgb(0xC8, 0x5A, 0x5A), cyan), "negative control: the danger edge is not attention");

        // The alarm roles did NOT move with the palette: the official tables carry the same Success /
        // Danger / TextOnDanger / DangerBorder family the S6 ruling fixed (see the token loop above -
        // these two clauses name the promise in words).
        Assert(Same(theme.AccentGold, Rgb(0xb4, 0x9c, 0x73)),
            "the identity accent is the Core Bronze from the role table, not a new colour");
        Assert(Same(theme.Warning, theme.Danger),
            "theme.Warning must stay the compatibility alias of theme.Danger, not a second attention token");
        // The redirect is live, not a coincidentally equal value: assigning Warning writes Danger. That
        // is exactly why nobody can "just set Warning to cyan" to get a Block colour - it would repaint
        // every destructive control in the same pass.
        UiTheme probe = UsTheme.Surface();
        probe.Warning = cyan;
        Assert(Same(probe.Danger, cyan) && Same(probe.Warning, cyan),
            "Warning must stay a redirect onto Danger: a cyan Warning assignment repaints destructive controls");

        Assert(!Same(UsAttention.Brush, theme.Warning),
            "attention must not be the Warning redirect (assigning cyan there repaints destructive controls)");
        Assert(!Same(UsAttention.Brush, theme.Danger)
            && theme.DangerBorder.HasValue
            && !Same(UsAttention.Brush, theme.DangerBorder.Value),
            "attention must not be the danger fill or its saturated edge");
        Assert(!Same(UsAttention.Brush, theme.AccentGold),
            "attention must not be the accent gold: a gold Block marker is the failure this pins");
        Assert(!Same(UsAttention.Brush, theme.TextPrimary)
            && !Same(UsAttention.Brush, theme.TextSecondary)
            && !Same(UsAttention.Brush, theme.TextDisabled),
            "attention must not be one of the three text tokens");
        Assert(!string.IsNullOrEmpty(UsAttention.Marker),
            "attention carries a shape marker: a bare cyan dot cannot be told from the neutral effect dot");
    }

    /// <summary>
    /// The badge substrate: cyan fill with the dark ink, observed through the real draw outlets - the
    /// fill from the recorded solids and the text ink from the label recorder (a colour assertion that
    /// read only the constant would not prove the draw call used it).
    /// </summary>
    private static void AttentionBadgePairsCyanWithDarkInk()
    {
        UiTheme theme = UsTheme.Surface();
        ClearSolids();
        ClearLabels();
        UsAttention.Badge(new Rect(0f, 0f, 40f, 16f), "3", theme);

        Assert(Painted(Rgb(0x86, 0xE7, 0xD8)), "the attention badge fill must be the ruling cyan");
        Assert(!Painted(theme.Danger) && !Painted(theme.AccentGold),
            "the attention badge must paint neither the danger fill nor the accent");

        IList inks = RecordedLabelColors();
        Assert(inks.Count > 0, "the badge must draw its text through the label outlet at all");
        Color recorded = Colour(inks[inks.Count - 1]);
        Assert(Same(recorded, UsAttention.InkOnBrush),
            "the badge text must be drawn in UsAttention.InkOnBrush, got the ink the outlet recorded");
        Assert(Same(UsAttention.InkOnBrush, Rgb(0x0f, 0x11, 0x16)),
            "InkOnBrush must be the ruling's #0f1116");

        // The forbidden half of the pair, as an assertion: the light ink on this cyan is ~1.20:1, so a
        // badge that used TextPrimary or TextOnDanger must fail, and it must fail for a reason this lane
        // states rather than a pixel nobody checked.
        Assert(!Same(UsAttention.InkOnBrush, theme.TextPrimary)
            && !Same(UsAttention.InkOnBrush, theme.TextOnDanger),
            "the attention ink must not be the light ink: light on this cyan is about 1.20:1");
        Assert(RelativeLuminance(UsAttention.Brush) > RelativeLuminance(theme.Danger),
            "the cyan is a light fill; it belongs with dark ink, unlike the dark danger plane");
    }

    /// <summary>
    /// The row substrate: the new <see cref="UsKernelDraw.RowRail.Attention"/> paints the thin cyan edge
    /// on the neutral raised fill, with the structural border - not the danger family, not selection, and
    /// not the accent. Failure-sensitive in both directions: repainting the rail with AccentGold,
    /// theme.Selected or the danger fill each turn this step red.
    /// </summary>
    private static void AttentionRailStaysThinCyanOnNeutralFill()
    {
        UiTheme theme = UsTheme.Surface();
        ClearSolids();
        UsKernelDraw.RowSurface(new Rect(0f, 0f, 200f, 24f), theme, hovered: false, UsKernelDraw.RowRail.Attention);

        IList colours = RecordedSolids();
        Assert(colours.Count >= 5, "an attention row paints a fill and four edges; recorded " + colours.Count);
        Assert(Same(Colour(colours[0]), theme.Raised),
            "the attention row fill must stay the neutral raised plane");
        for (int i = 1; i < 5; i++)
        {
            Assert(Same(Colour(colours[i]), theme.Border), "and its edge must stay the structural line");
        }

        Assert(RailIs(UsAttention.Brush, UsAttention.RailWidth),
            "the attention edge is " + UsAttention.RailWidth + "px of the ruling cyan at the row's left edge");
        Assert(UsAttention.RailWidth < UsKernelDraw.RailWidth,
            "the attention edge must stay thinner than a location rail, or hue alone separates them");
        Assert(!Painted(theme.Danger)
            && theme.DangerBorder.HasValue
            && !Painted(theme.DangerBorder.Value),
            "an attention row is not a destructive treatment");
        Assert(!Painted(theme.AccentGold), "and an attention row must not take the accent");
        Assert(!Painted(theme.Selected), "and attention must never be painted as selection");
        Assert(!Painted(theme.TextOnDanger), "and it must not borrow the danger plane's text token");
    }

    /// <summary>
    /// The no-scatter gate: the ruling's cyan literal is legal in exactly one source file, so a widget
    /// that bypasses <see cref="UsAttention"/> with its own literal fails here even though no draw-level
    /// lane could see it. The positive control runs the same scanner over planted text, so a scanner that
    /// read nothing cannot pass this step silently.
    /// </summary>
    private static void StrayCyanScanFindsOnlyTheOwner()
    {
        string root = Path.Combine(RepoRoot(), "Source", "UniversalSqueaker");
        Assert(Directory.Exists(root), "the scan root must exist: " + root);
        var files = new List<KeyValuePair<string, string>>();
        foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            files.Add(new KeyValuePair<string, string>(path, File.ReadAllText(path)));
        }

        Assert(files.Count >= 20, "the scan must really walk the source tree; found " + files.Count + " files");
        List<string> offenders = AttentionCyanScan.Find(files);
        Assert(offenders.Count == 0,
            "the attention hue must live only in UsAttention.cs; also found in: " + string.Join(", ", offenders));

        // Positive control, one entry per literal form the scanner claims to cover: the probe must flag
        // every non-owner form and skip the owner-named one. The first version of this control only
        // exercised the hex-string form and passed while the scanner missed the /255f form - the exact
        // "control that cannot redden" failure, so all four forms are planted and counted.
        List<string> planted = AttentionCyanScan.Find(new[]
        {
            new KeyValuePair<string, string>("Fake/Hex.cs", "var doc = \"#86E7D8\";"),
            new KeyValuePair<string, string>("Fake/Channels.cs", "var ink = new Color(0x86, 0xE7, 0xD8);"),
            new KeyValuePair<string, string>("Fake/Normalized.cs", "var ink = new Color(0x86 / 255f, 0xE7 / 255f, 0xD8 / 255f);"),
            new KeyValuePair<string, string>("Fake/Decimal.cs", "var ink = new Color(134f / 255f, 231f / 255f, 216f / 255f);"),
            new KeyValuePair<string, string>("Fake/UsAttention.cs", "var ink = new Color(0x86, 0xE7, 0xD8);")
        });
        Assert(planted.Count == 4,
            "positive control: the scanner must flag all four non-owner literal forms and skip the owner, flagged "
            + planted.Count + " of 4");
    }

    /// <summary>The channel-count form of the ruling cyan, not a string, so a differently spaced or
    /// reordered call is still caught; the hex form covers prose and string literals.</summary>
    private static class AttentionCyanScan
    {
        private const string OwnerFileName = "UsAttention.cs";

        /// <summary>Paths whose text contains the attention hue anywhere but its owner file.</summary>
        internal static List<string> Find(IEnumerable<KeyValuePair<string, string>> files)
        {
            var offenders = new List<string>();
            foreach (KeyValuePair<string, string> file in files)
            {
                if (string.Equals(Path.GetFileName(file.Key), OwnerFileName, StringComparison.Ordinal)) continue;
                if (HasCyanLiteral(file.Value)) offenders.Add(file.Key);
            }

            return offenders;
        }

        private static bool HasCyanLiteral(string text)
        {
            if (text.IndexOf("86e7d8", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // The channel form, in whatever separator style a widget writes it: plain ints
            // (0x86, 0xE7, 0xD8), normalized floats (0x86 / 255f, ...), or decimal channels
            // (134f / 255f, ...). Bounded gaps keep an unrelated triple from matching.
            return System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"(?:0x86\b|\b134(?!\d))[\s\S]{0,48}?(?:0xE7\b|\b231(?!\d))[\s\S]{0,48}?(?:0xD8\b|\b216(?!\d))",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }

    /// <summary>WCAG relative luminance, used only as a direction check: the cyan fill is light, so dark
    /// ink is the only defensible pair.</summary>
    private static float RelativeLuminance(Color colour)
    {
        return 0.2126f * Linear(colour.r) + 0.7152f * Linear(colour.g) + 0.0722f * Linear(colour.b);
    }

    private static float Linear(float channel)
    {
        return channel <= 0.03928f ? channel / 12.92f : (float)Math.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }

    private static void ClearLabels()
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField("LabelColors", BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "the runtime stub does not record label colours; this lane cannot observe badge ink and must not pass silently");
        }

        var list = field.GetValue(null) as IList;
        if (list == null) throw new InvalidOperationException("the runtime stub's label recorder is not a list");
        list.Clear();
    }

    private static IList RecordedLabelColors()
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField("LabelColors", BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException("the runtime stub does not record label colours; this lane must not pass silently");
        }

        var list = field.GetValue(null) as IList;
        if (list == null) throw new InvalidOperationException("the runtime stub's label recorder is not a list");
        return list;
    }

    /// <summary>The harness's own copy of Program.RepoRoot's rule (the program's helper is private and
    /// this file must not edit it): walk up from the binary until the repository's gate script appears.</summary>
    private static string RepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "verify-local.ps1"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }

    /// <summary>True when a recorded solid of this colour has this rail geometry at the row's left edge.</summary>
    private static bool RailIs(Color colour, float width)
    {
        IList colours = RecordedSolids();
        IList rects = RecordedSolidRects();
        for (int i = 0; i < colours.Count && i < rects.Count; i++)
        {
            if (!Same(Colour(colours[i]), colour)) continue;
            if (rects[i] is Rect rect
                && Math.Abs(rect.width - width) <= 0.01f
                && Math.Abs(rect.x) <= 0.01f
                && Math.Abs(rect.height - 24f) <= 0.01f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CoversCentre(Rect cell)
    {
        float x = cell.x + cell.width * 0.5f;
        float y = cell.y + cell.height * 0.5f;
        IList rects = RecordedSolidRects();
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i] is Rect rect && x >= rect.x && x <= rect.xMax && y >= rect.y && y <= rect.yMax) return true;
        }

        return false;
    }

    private static float InkArea()
    {
        float area = 0f;
        IList rects = RecordedSolidRects();
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i] is Rect rect) area += rect.width * rect.height;
        }

        return area;
    }

    /// <summary>The second recorder field: the lane needs the geometry, not only the colour.</summary>
    private static IList RecordedSolidRects()
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField("DrawBoxSolidRects", BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException("the runtime stub does not record solid rects; this lane must not pass silently");
        }

        var list = field.GetValue(null) as IList;
        if (list == null) throw new InvalidOperationException("the runtime stub's rect recorder is not a list");
        return list;
    }

    private static bool Painted(Color colour)
    {
        IList solids = RecordedSolids();
        for (int i = 0; i < solids.Count; i++)
        {
            if (Same(Colour(solids[i]), colour)) return true;
        }

        return false;
    }

    private static Color Colour(object? recorded)
    {
        if (recorded is Color colour) return colour;
        throw new InvalidOperationException("the recorded solid was not a Color: " + (recorded?.GetType().FullName ?? "(null)"));
    }

    /// <summary>Resolved value of one declared token on a configured bag (edges through their
    /// surface styles, matching how the drawing reads them).</summary>
    private static Color ResolvedToken(UiTheme theme, string token)
    {
        return token switch
        {
            "Base" => theme.Base,
            "WorkspacePlane" => theme.WorkspacePlane,
            "Panel" => theme.Panel,
            "SectionBand" => theme.SectionBand,
            "Raised" => theme.Raised,
            "Hover" => theme.Hover,
            "Selected" => theme.Selected,
            "Success" => theme.Success,
            "Danger" => theme.Danger,
            "Border" => theme.Border,
            "BorderStrong" => theme.BorderStrong,
            "Divider" => theme.Divider,
            "BaseBorder" => theme.BaseSurface.Border,
            "PanelBorder" => theme.PanelSurface.Border,
            "RaisedBorder" => theme.RaisedSurface.Border,
            "HoverBorder" => theme.HoverSurface.Border,
            "TextPrimary" => theme.TextPrimary,
            "TextSecondary" => theme.TextSecondary,
            "TextDisabled" => theme.TextDisabled,
            "TextOnGold" => theme.TextOnGold,
            "TextOnDanger" => theme.TextOnDanger,
            "DangerBorder" => theme.DangerSurface.Border,
            "AccentGold" => theme.AccentGold,
            "SwitchThumbOff" => theme.SwitchThumbOff,
            _ => throw new InvalidOperationException("unknown token " + token),
        };
    }

    private static Color ParseHex(string hex)
    {
        string s = hex.Trim();
        if (s.StartsWith("#", StringComparison.Ordinal)) s = s.Substring(1);
        return new Color(
            Convert.ToByte(s.Substring(0, 2), 16) / 255f,
            Convert.ToByte(s.Substring(2, 2), 16) / 255f,
            Convert.ToByte(s.Substring(4, 2), 16) / 255f, 1f);
    }

    private static bool Same(Color left, Color right)
    {
        return Math.Abs(left.r - right.r) <= 0.0005f
            && Math.Abs(left.g - right.g) <= 0.0005f
            && Math.Abs(left.b - right.b) <= 0.0005f
            && Math.Abs(left.a - right.a) <= 0.0005f;
    }

    private static Color Rgb(int r, int g, int b)
    {
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }

    /// <summary>
    /// The harness's UnityEngine stub is not a compile-time reference (the compile-time Unity comes from
    /// the game reference assembly), so the recorder is reached by reflection — the same shape FerriteLib's
    /// own drawing lanes use.
    /// </summary>
    private static IList RecordedSolids()
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField("DrawBoxSolidColors", BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "the runtime stub does not record solid fills; this lane cannot observe a draw and must not pass silently");
        }

        var list = field.GetValue(null) as IList;
        if (list == null)
        {
            throw new InvalidOperationException("the runtime stub's solid recorder is not a list");
        }

        return list;
    }

    private static void ClearSolids()
    {
        MethodInfo? clear = typeof(Verse.Widgets).GetMethod("ClearDrawBoxSolidCalls", BindingFlags.Public | BindingFlags.Static);
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
            throw new InvalidOperationException("Step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}