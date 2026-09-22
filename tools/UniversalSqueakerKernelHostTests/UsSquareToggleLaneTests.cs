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
/// S6-3 step 4: the square ON/OFF toggle. GEOMETRY AND HIT FIRST, appearance last - and both halves are
/// asserted from the DRAW RECORD's own internal relations, never by predicting what a widget will draw from
/// the arranged snapshot.
///
/// <para>
/// WHY A KIND (measured before it was written, 2026-09-22; the citation for the candidate these two
/// negatives produced):
/// <list type="bullet">
/// <item><b>There is no purely filled declarative element.</b> <c>chrome/banner</c> is a text band (it
/// colours text), <c>chrome/rule</c> paints a HORIZONTAL hairline only (<c>RuleWidget.cs:61-80</c>), and a
/// container's chrome is a whole filled band with four edges.</item>
/// <item><b>Nothing lets geometry or position depend on a bound value.</b> The engine-wide value-dependent
/// attributes are Visible / VisibleKey / Hidden / SelectedKey
/// (<c>AtomVocabulary.EngineWideAttributes</c>) - visibility and STATE, never shape.</item>
/// </list>
/// </para>
///
/// <para>
/// MATERIAL, NOT ROLE (a measured ruling, not a style preference). The toggle paints its own body from theme
/// VALUES rather than asking the role table what an on/off surface looks like, because the cards it lives in
/// are the flat scope: that scope sets <c>SelectedBorder</c> equal to <c>Selected</c> and <c>RaisedBorder</c>
/// equal to <c>Raised</c> - the equality that IS "a flat surface paints no box" - so a track painted from the
/// role table's OFF treatment came out <c>#191612</c> on a card whose face is <c>#191612</c>: the control
/// vanished. <b>A control that must be VISIBLE on a flat plane cannot take its material from the role of the
/// plane it sits on.</b> The values are still the theme's own (no literal, no new token).
/// </para>
///
/// <para>
/// MUTATION LEDGER:
/// <list type="bullet">
/// <item><b>TheTogglesAreDeclaredOverTheirOwnBool</b> - a row left on another kind, or a toggle whose
/// SelectedKey names a different bool, reddens.</item>
/// <item><b>BothStatesAreDrawnAndVisible</b> - MUTATION-PROVEN, twice over, and the second half is the one
/// that took work: (a) the knob's END follows the state; (b) the track's fill, the track's edge and the knob's
/// ink are each compared against an expectation built from the THEME + the state this lane read. Expecting
/// them from the widget's own <c>Material()</c> instead would have been tautological - measured: the first
/// version of that assertion stayed green when the material was made state-blind, because both sides read the
/// same bool through the same function. With the independent expectation, "the OFF row painted the ON
/// material" reddens (<c>scale-cooldown=False: expected #191612, got #D19938</c>).</item>
/// <item><b>TheWholeBandIsTheHitRule</b> - a CONTRACT guard, NOT mutation-proven, and the step says so in
/// place: the press path is a native IMGUI round trip whose pointer the lane cannot aim reliably (the frame a
/// widget draws in and the frame the lane arranges are different arrangements - measured repeatedly in this
/// lane), and the lead ruled against adding a public seam for it. So the step reads the widget's own source
/// for the two facts a press depends on (the whole arranged rect is passed to <c>UiNative.Button</c>, and the
/// click writes the inverse value) and asserts the band is taller than the track it draws. The press itself
/// stays a REAL-SCREEN item, named rather than papered over.</item>
/// </list>
/// </para>
///
/// <para>
/// NOT CLAIMED: the look, and the reference geometry. The band is the row layout's 24x30, so the track is
/// 24x18 rather than the reference's 34x18 (widening the band re-wraps one of the egg row's state lines at
/// 320px - measured On=67.67 / Off=89 - so it is a separate layout step, not part of this kind). Beyond that,
/// whether the track reads as "on" at a glance needs a real screen.
/// </para>
/// </summary>
internal static class UsSquareToggleLaneTests
{
    private const string ToggleKind = "us/square-toggle";
    private const float PageWidth = 800f;
    private const float TallHeight = 1100f;

    /// <summary>The band every toggle declares. 24 wide because the enclosing rows are laid out around it;
    /// the track shrinks to it rather than overflowing.</summary>
    public const float DeclaredBandWidth = 24f;
    public const float DeclaredBandHeight = 30f;

    /// <summary>The scope the toggle rows draw in, so the lane asks the same theme the widget was handed.</summary>
    private const string FlatScheme = "us-flat-panel";

    /// <summary>
    /// The rows this lane drives, in the order the page DRAWS them (top to bottom), with the state the
    /// fixture's rich view gives each one. They are the two states of the knob's travel, and both come from
    /// the page's own data - no lane-only seed, and no assumption about what a write through the binding
    /// layer does to this fixture's read-only projection (measured: it does not move it, which is how an
    /// earlier version of this step drew the "on" case with the off treatment).
    /// </summary>
    private static readonly (string RowId, string CheckId, string Bind, bool State)[] Rows =
    {
        // Measured: the egg row is the first toggle the page draws and it is ON; the cooldown row is the
        // second and it is OFF. The written contract is asserted, so a fixture or manifest reorder reddens
        // rather than silently measuring the other control.
        ("basic-egg-row", "basic-egg-check", "allow-eggs", true),
        ("basic-cooldown-row", "basic-cooldown-check", "scale-cooldown", false),
    };

    public static int RunAll()
    {
        Step("the toggle kinds are declared over the bool each one owns", TheTogglesAreDeclared);
        Step("both states are drawn, and the control is visible in both", BothStatesAreDrawnAndVisible);
        Step("the whole band is the hit rule", TheWholeBandIsTheHitRule);
        Console.WriteLine("UsSquareToggleLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 1
    // ---------------------------------------------------------------------------------------------

    private static void TheTogglesAreDeclared()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        foreach ((string _, string checkId, string bind, bool _) in Rows)
        {
            UiElementSpec? check = FindById(host.Manifest.Roots, checkId);
            Assert(check != null, "the shipped manifest must carry '" + checkId + "'");
            Assert(check!.Kind == ToggleKind,
                "'" + checkId + "' must be the US toggle surface, got '" + check.Kind + "'");
            Assert(check.TryGetAttribute("Bind", out string actualBind) && actualBind == bind,
                "'" + checkId + "' must own the bool binding '" + bind + "', got '" + actualBind + "'");
            Assert(check.TryGetAttribute("SelectedKey", out string selected) && selected == bind,
                "'" + checkId + "' must name the same bool in SelectedKey as in Bind, so the engine's own role"
                + " resolution agrees with the state this kind paints, got '" + selected + "'");
        }

        // CONTROL, and it is a manifest-level one on purpose: the library's own checkbox atom must still be
        // USED where a per-row toggle is not the intent (the checklist's pack rows). A blanket rename of the
        // kind would have taken that one too - and the fixture's checklist happens to be empty in the rich
        // view, so this reads the DECLARATION rather than an arranged element (the first version of this
        // control looked for the row's element and found nothing, which is a control that cannot fail).
        string manifestPath = System.IO.Path.Combine(
            Program.RepoRoot(), "Source", "UniversalSqueaker", "UI", "Layout.Schema2.xml");
        string manifest = System.IO.File.ReadAllText(manifestPath);
        int checklistDeclarations = System.Text.RegularExpressions.Regex.Matches(
            manifest, "Id=\"checklist-row-check\" Kind=\"input/checkbox\"").Count;
        Assert(checklistDeclarations == 1,
            "control: the checklist's per-row checkbox must still be declared as input/checkbox, found "
            + checklistDeclarations + " such declaration(s) - a blanket kind rename would have taken it too");
    }

    // ---------------------------------------------------------------------------------------------
    // Step 2: drawn relations, not predicted geometry
    // ---------------------------------------------------------------------------------------------

    private static void BothStatesAreDrawnAndVisible()
    {
        var seen = new List<bool>();
        for (int i = 0; i < Rows.Length; i++)
        {
            seen.Add(OneRowBothStates(Rows[i].RowId, Rows[i].CheckId, Rows[i].Bind, Rows[i].State, i));
        }

        Assert(seen.Contains(true) && seen.Contains(false),
            "the page's own two rows must be in OPPOSITE states, or this step would have measured one knob end"
            + " twice and could not tell the two apart: " + string.Join(",", seen));
    }

    /// <summary>
    /// Draws one row TWICE - pointer parked away, then parked on the row - and reads the relations out of the
    /// draw record's own boxes. Nothing here predicts a position from the arranged snapshot: measured, two
    /// consecutive frames can arrange the same page 128px apart, so a lane that computed "where the band will
    /// be" would be measuring a different frame than the one the widget drew in.
    /// </summary>
    private static bool OneRowBothStates(string rowId, string checkId, string bind, bool expectedState,
        int rowIndex)
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            var source = new RecordingSettingsSource { RichData = true };
            using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
            host.Bindings.Invoke("set-tab", "Overview");
            host.MeasureAndArrange(new Vector2(PageWidth, TallHeight));
            UiLayoutSnapshot snapshot = Arrange(host);

            bool state = host.Bindings.Get<bool>(bind);
            Assert(state == expectedState,
                bind + ": this row's rich-view state is written down as " + expectedState + ", got " + state
                + " - the written contract is what keeps the two knob ends in this lane honest");
            Rect band = RectOf(snapshot, checkId);
            UiTheme theme = new UiStyleResolver(UsTheme.Surface(), host.Manifest.Styles)
                .ThemeFor(new[] { new UiStyleDeclaration(FlatScheme) });
            Color cardFace = theme.Raised;

            // Parked: the pointer sits outside the page, so nothing is hovered and the control draws its own
            // material. The band's y range is what isolates this row's control from the other toggles.
            List<(Rect Rect, Color Colour)> drawn = DrawAndCollect(host, new Vector2(-1000f, -1000f), band, rowIndex);

            var byColour = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach ((Rect _, Color colour) in drawn)
            {
                string key = Hex(colour);
                byColour[key] = byColour.TryGetValue(key, out int count) ? count + 1 : 1;
            }

            // The draw seam in this harness records three of the five: the track's left and right edge strips
            // are two boxes at the same x, so only one survives in the record. The shape is asserted rather
            // than assumed - a fill box, at least one edge strip, and exactly one knob.
            Assert(drawn.Count >= 3,
                bind + ": the toggle must record its track fill, its edges and its knob, got " + drawn.Count
                + " boxes: " + Describe(drawn));
            var fills = drawn.Where(d => Close(d.Rect.height, UsSquareToggleWidget.TrackHeight)).ToList();
            Assert(fills.Count == 1,
                bind + ": exactly one " + UsSquareToggleWidget.TrackHeight + "px track fill must be drawn, got "
                + fills.Count + ": " + Describe(drawn));
            (Rect Rect, Color Colour) trackFill = fills[0];
            var edges = drawn.Where(d => Close(d.Rect.height, 1f)).ToList();
            Assert(edges.Count >= 1,
                bind + ": the track must be edged (a 1px strip in the record), got " + Describe(drawn));

            // The knob = the 14x14 box whose top-right corner is the track's, offset by the widget's own inset.
            List<(Rect Rect, Color Colour)> knobs = drawn
                .Where(d => Close(d.Rect.width, UsSquareToggleWidget.KnobSize)
                    && Close(d.Rect.height, UsSquareToggleWidget.KnobSize))
                .ToList();
            Assert(knobs.Count == 1,
                bind + ": exactly one " + UsSquareToggleWidget.KnobSize + "px knob must be drawn, got "
                + knobs.Count + ": " + Describe(drawn));
            Rect knob = knobs[0].Rect;
            Assert(Close(knob.y, trackFill.Rect.y + 2f),
                bind + ": the knob must sit 2px below the track's top edge, got " + Describe(knob) + " vs track "
                + Describe(trackFill.Rect));

            // THE STATE: the knob's end. This is the property the whole kind exists for, and it is read from
            // the drawn boxes rather than from the state the lane believes it set.
            if (state)
            {
                Assert(Close(knob.xMax, trackFill.Rect.xMax - 2f),
                    bind + "=on: the knob must sit at the track's RIGHT end, got " + Describe(knob)
                    + " vs track " + Describe(trackFill.Rect));
            }
            else
            {
                Assert(Close(knob.x, trackFill.Rect.x + 2f),
                    bind + "=off: the knob must sit at the track's LEFT end, got " + Describe(knob)
                    + " vs track " + Describe(trackFill.Rect));
            }

            // THE MATERIAL, and the expectation is built from the THEME + the STATE THIS LANE READ, not from
            // the widget's Material() function. That distinction is the whole mutation sensitivity: asking
            // Material() would agree with the widget even when the widget is state-blind, because both sides
            // would read the same bool and the same function (measured - the first version of this assertion
            // stayed green under a state-blind mutation, which is a lane that cannot tell the two states
            // apart). These two expectations are the independently written contract:
            Color expectedFill = state ? theme.AccentWith(UsSquareToggleWidget.AccentAlpha) : theme.Raised;
            Color expectedEdge = state ? theme.AccentGold : theme.Border;
            Color expectedKnob = state ? theme.TextOnGold : theme.TextSecondary;
            Assert(SameColor(trackFill.Colour, expectedFill),
                bind + "=" + state + ": the track must be filled with this state's own material ("
                + Hex(expectedFill) + "), got " + Hex(trackFill.Colour) + " - a state-blind material reddens"
                + " here");
            Assert(SameColor(edges[0].Colour, expectedEdge),
                bind + "=" + state + ": the track's edge must be this state's material edge ("
                + Hex(expectedEdge) + "), got " + Hex(edges[0].Colour));
            Assert(SameColor(knobs[0].Colour, expectedKnob),
                bind + "=" + state + ": the knob must be inked with this state's own ink (" + Hex(expectedKnob)
                + "), got " + Hex(knobs[0].Colour));

            // VISIBILITY, the ruling's own criterion: the knob's ink must be distinguishable from the track it
            // sits in AND from the plane the whole control sits on. The second half is the one the role path
            // failed: the OFF track used to equal the card's own face exactly.
            Assert(!SameColor(knobs[0].Colour, trackFill.Colour),
                bind + "=" + state + ": the knob must not be the same colour as its track ("
                + Hex(knobs[0].Colour) + " on " + Hex(trackFill.Colour) + ")");
            Assert(!SameColor(knobs[0].Colour, cardFace),
                bind + "=" + state + ": the knob must not vanish into the card's own plane (" + Hex(cardFace) + ")");
            // The track's EDGE is what draws the control on this page, and that is a measured fact rather
            // than a shortcut: the flat scope's Raised IS the card's own face (#191612), so the track's fill
            // necessarily coincides with the plane it sits on and only the Border-coloured edge separates
            // them. The assertion therefore demands that AT LEAST ONE half of the track differ from the
            // plane - which is exactly the difference between this material and the role table's OFF
            // treatment, where BOTH halves were the plane's own colour and the control vanished.
            Assert(!SameColor(trackFill.Colour, cardFace) || !SameColor(edges[0].Colour, cardFace),
                bind + "=" + state + ": the track must be visible against the card's own plane (" + Hex(cardFace)
                + ") through at least one of its halves: fill=" + Hex(trackFill.Colour) + " edge="
                + Hex(edges[0].Colour) + ". The role table's OFF treatment made BOTH equal to the plane"
                + " (measured #191612 on #191612), which is why this control paints its own material.");

            // The band stays the size the row layout was built around, and the boxes stay inside it.
            Assert(Close(band.width, DeclaredBandWidth) && Close(band.height, DeclaredBandHeight),
                bind + ": the declared band must stay " + DeclaredBandWidth + "x" + DeclaredBandHeight
                + ", got " + Describe(band));

            Console.WriteLine("[toggle] " + rowId + " state=" + state + " band=" + Describe(band) + " track="
                + Describe(trackFill.Rect) + " fill=" + Hex(trackFill.Colour) + " edge=" + Hex(edges[0].Colour)
                + " knob=" + Describe(knob) + " ink=" + Hex(knobs[0].Colour) + " cardFace=" + Hex(cardFace)
                + " boxes=" + drawn.Count);
            return state;
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 3
    // ---------------------------------------------------------------------------------------------

    private static void TheWholeBandIsTheHitRule()
    {
        // HOW FAR THIS GOES, and why it stops here. The press path is a native IMGUI round trip (MouseDown
        // captures the control, MouseUp at the same point activates it), and this harness CAN drive it - the
        // page's other interaction lanes do. What this lane cannot do reliably is AIM the pointer: measured
        // six times while writing this step, the frame a widget DRAWS in and the frame the lane ARRANGES are
        // separate arrangements (the same control read (736,270) in one and (752,398) in another), so a press
        // computed from the arranged snapshot lands on the page but not on the control, and the write comes
        // back null. The lead ruled against adding a public seam for it, and rightly: a seam that exists so a
        // test can hit a control IS vocabulary growth.
        //
        // So this step asserts the CONTRACT that the press depends on, read out of the widget's own source -
        // the whole arranged rect is the hit, and the click writes the inverse of what was read. The press
        // itself is therefore a real-screen item (the same "needs a real screen" class as the look), and it is
        // named here rather than being papered over with a lane that would pass without ever hitting anything.
        string sourcePath = System.IO.Path.Combine(
            Program.RepoRoot(), "Source", "UniversalSqueaker", "UI", "Kernel", "UsSquareToggleWidget.cs");
        string widget = System.IO.File.ReadAllText(sourcePath);

        Assert(widget.Contains("if (UiNative.Button(rect, ctx))"),
            "the toggle must take its hit through the same native button seam every other control uses, on the"
            + " WHOLE arranged rect - the band is the target, not the drawn track");
        Assert(widget.Contains("ctx.Bindings.Set<bool>(key, !state);"),
            "a press must write the INVERSE of the bool the toggle read");
        Assert(!widget.Contains("Geometry(rect, state, out Rect hit"),
            "the toggle must not derive a separate hit rect: the band is the target and the track is what it"
            + " draws inside that band");

        // The readable half of the same claim: the band each row declares is the band the engine hands the
        // toggle (the control's arranged rect is the full 24x30 the manifest declares, not the 24x18 track).
        foreach ((string _, string checkId, string bind, bool _) in Rows)
        {
            using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
            host.Bindings.Invoke("set-tab", "Overview");
            UiLayoutSnapshot snapshot = Arrange(host);
            Rect band = RectOf(snapshot, checkId);
            Assert(Close(band.width, DeclaredBandWidth) && Close(band.height, DeclaredBandHeight),
                bind + ": the toggle's arranged rect must be the whole declared band (" + DeclaredBandWidth
                + "x" + DeclaredBandHeight + "), got " + Describe(band));
            Assert(band.height > UsSquareToggleWidget.TrackHeight,
                bind + ": and the band must be TALLER than the track it draws, so 'the band is the target' is"
                + " a wider promise than 'the track is the target' (band " + Num(band.height) + "px vs track "
                + UsSquareToggleWidget.TrackHeight + "px)");
            Console.WriteLine("[toggle-hit] " + bind + " band=" + Describe(band) + " trackHeight="
                + UsSquareToggleWidget.TrackHeight + " hit=wholeBand (press path: real screen, see the step comment)");
        }
    }

    private static SqueakBasicToggle? ToggleOf(string bind)
    {
        switch (bind)
        {
            case "scale-cooldown": return SqueakBasicToggle.ScaleCooldown;
            case "scale-talking": return SqueakBasicToggle.ScaleTalking;
            case "scale-population": return SqueakBasicToggle.ScalePopulation;
            default: return null;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Plumbing
    // ---------------------------------------------------------------------------------------------

    /// <summary>One draw with the stub pointer parked at <paramref name="pointer"/>, returning every solid the
    /// record holds for boxes inside the given band's y range.</summary>
    private static List<(Rect Rect, Color Colour)> DrawAndCollect(UiHost host, Vector2 pointer, Rect band,
        int rowIndex)
    {
        ClearSolids();
        SetField(DebugMousePositionField, pointer);
        SetField(DebugMousePositionEnabledField, true);
        try
        {
            host.DrawChecked(new Rect(0f, 0f, PageWidth, TallHeight));
        }
        finally
        {
            SetField(DebugMousePositionEnabledField, false);
        }

        IList rects = Recorded("DrawBoxSolidRects");
        IList colors = Recorded("DrawBoxSolidColors");
        Assert(rects.Count == colors.Count,
            "the stub's two solid recorders must stay in step: " + rects.Count + " vs " + colors.Count);

        // A toggle's own boxes are exactly five: the track's fill, its edges, and the knob. Every one of them
        // is either a full-width band box (24x18 fill, 24x1 edges) or the 14px knob - shapes that ARE the
        // control's signature and that no other element on this page produces.
        var signature = new List<(Rect Rect, Color Colour)>();
        for (int i = 0; i < rects.Count; i++)
        {
            Rect rect = (Rect)rects[i]!;
            bool fill = Close(rect.width, DeclaredBandWidth) && Close(rect.height, UsSquareToggleWidget.TrackHeight);
            bool edge = Close(rect.width, DeclaredBandWidth) && Close(rect.height, 1f);
            bool knob = Close(rect.width, UsSquareToggleWidget.KnobSize)
                && Close(rect.height, UsSquareToggleWidget.KnobSize);
            if (fill || edge || knob) signature.Add((rect, (Color)colors[i]!));
        }

        // Which CLUSTER of that signature belongs to the row under test: the TOP-most one whose measured fill
        // starts at or below the band the lane arranged. Measured reason this is a cluster and not a y window:
        // the frame the widget DRAWS in and the frame the lane ARRANGES are not the same arrangement (the same
        // control read (736,270) in one and (752,398) in another), so the drawn y and the arranged y do not
        // coincide - but their ORDER does, and every toggle row contributes one cluster.
        var fills = signature.Where(s => Close(s.Rect.height, UsSquareToggleWidget.TrackHeight)).ToList();
        Assert(fills.Count >= 1,
            "no toggle track was drawn on this page at all: " + Describe(signature));
        // WHICH cluster is this row's. The rows are listed in manifest order and so are the clusters (the
        // page scrolls to the top before every draw), so the two orders correspond; the lane ASSERTS that
        // correspondence rather than assuming it silently, and a page that stopped drawing the two rows in
        // order reddens instead of measuring the wrong control.
        var ordered = fills.OrderBy(f => f.Rect.y).ToList();

        // ONE CLUSTER PER TOGGLE ROW on the page, and the row under test is the index the caller names. The
        // count is the number of rows the Overview's two cards DECLARE (six in basic-tuning plus the camera
        // card's one): a page that stopped drawing one of them reddens here rather than letting the lane read
        // the wrong row's boxes.
        int declaredToggleRows = CountDeclaredToggles();
        Assert(fills.Count == declaredToggleRows,
            "every declared toggle row must draw exactly one track (" + declaredToggleRows + " rows), got "
            + fills.Count + " tracks: " + Describe(signature));
        (Rect Rect, Color Colour) selected = ordered[rowIndex];

        var drawn = signature.Where(s => s.Rect.y >= selected.Rect.y - 2f
            && s.Rect.y <= selected.Rect.y + UsSquareToggleWidget.TrackHeight + 2f).ToList();
        Assert(drawn.Any(d => Close(d.Rect.y, selected.Rect.y) && Close(d.Rect.x, selected.Rect.x)),
            "the selected track must be part of its own cluster");

        return drawn;
    }

    /// <summary>One real press: MouseDown then MouseUp at the same point, through the stub's own hot-control
    /// protocol (the seam the page's other interaction lanes drive).</summary>
    private static void Press(UiHost host, Vector2 pointer)
    {
        SetField(DebugMousePositionField, pointer);
        SetField(DebugMousePositionEnabledField, true);
        try
        {
            DrawWithEvent(host, EventType.MouseDown, pointer);
            DrawWithEvent(host, EventType.MouseUp, pointer);
        }
        finally
        {
            SetField(DebugMousePositionEnabledField, false);
        }
    }

    private static void DrawWithEvent(UiHost host, EventType type, Vector2 pointer)
    {
        Event e = Event.KeyboardEvent("dummy");
        e.type = type;
        e.button = 0;
        e.mousePosition = pointer;
        Event.current = e;
        try
        {
            host.DrawChecked(new Rect(0f, 0f, PageWidth, TallHeight));
        }
        finally
        {
            Event.current = null;
        }
    }

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        host.MeasureAndArrange(new Vector2(PageWidth, TallHeight));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        return host.MeasureAndArrange(new Vector2(PageWidth, TallHeight));
    }

    private static Rect RectOf(UiLayoutSnapshot snapshot, string id)
    {
        Assert(snapshot.RectById.TryGetValue(id, out Rect rect),
            "the arranged snapshot must carry '" + id + "'; a missing one means the page lost the control");
        return rect;
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

    private static string Describe(Vector2 point)
    {
        return "(" + Num(point.x) + "," + Num(point.y) + ")";
    }

    private static string Describe(List<(Rect Rect, Color Colour)> painted)
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

    /// <summary>
    /// How many toggle rows the Overview's cards declare, counted from the manifest so the lane's cluster
    /// count is tied to the page rather than to a number written here.
    /// </summary>
    private static int CountDeclaredToggles()
    {
        string manifestPath = System.IO.Path.Combine(
            Program.RepoRoot(), "Source", "UniversalSqueaker", "UI", "Layout.Schema2.xml");
        string manifest = System.IO.File.ReadAllText(manifestPath);
        // The basic-tuning card's rows and the camera card's single control are the ones this lane's page
        // draws; both are gated to the Overview workspace.
        return System.Text.RegularExpressions.Regex.Matches(manifest, "Kind=\"us/square-toggle\"").Count
            - System.Text.RegularExpressions.Regex.Matches(
                manifest, "Kind=\"us/square-toggle\"[^>]*HelpKey=\"us/basic-tuning/eat-precision-include-drugs\"").Count;
    }

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

    private static FieldInfo DebugMousePositionField =>
        RequireField("DebugMousePosition", typeof(Vector2));

    private static FieldInfo DebugMousePositionEnabledField =>
        RequireField("DebugMousePositionEnabled", typeof(bool));

    private static void SetField(FieldInfo field, object? value)
    {
        try
        {
            field.SetValue(null, value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "REFLECTION BLOCKER: cannot set UiNative." + field.Name + " on net472.", ex);
        }
    }

    private static FieldInfo RequireField(string name, Type fieldType)
    {
        FieldInfo? field = typeof(UiNative).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null || field.FieldType != fieldType)
        {
            throw new InvalidOperationException(
                "REFLECTION BLOCKER: UiNative." + name + " is not the expected " + fieldType.Name
                + " seam on net472; the pointer seam this lane drives has moved.");
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
            throw new InvalidOperationException("UsSquareToggleLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
