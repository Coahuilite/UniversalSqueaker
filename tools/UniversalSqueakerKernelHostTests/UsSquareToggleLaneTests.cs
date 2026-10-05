using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// The ON/OFF switch on the Overview page. <b>R2 (2026-09-28): the controls are the CARRIER's boolean kind
/// now</b> — <c>input/checkbox</c> with <c>Appearance="switch"</c> — and the US-only kind
/// <c>us/square-toggle</c> is retired with its file and its registration. This lane was re-cut in the same
/// change rather than relaxed: the SUBJECT moved from "a US kind's own material" to "the shared kind's
/// switch appearance", and every assertion below names which of the two it is.
///
/// <para>
/// <b>WHY THE MIGRATION WAS POSSIBLE AT ALL</b> (the retiral's own justification, and the mutation it
/// invites back): <c>us/square-toggle</c> existed for ONE reason — the knob's end depends on a bound bool,
/// and no declarative element could express that. The carrier's checkbox gained the additive
/// <c>Appearance</c> attribute (<c>switch</c> default, <c>checkbox</c> the explicit alternative) whose switch
/// branch draws exactly this control's proportions (34x18 track, 14px knob, 2px inset, 6px label gap) over
/// the SAME one boolean input path. One boolean BEHAVIOUR, two looks — so a second kind would freeze
/// vocabulary the attribute already carries. Re-adding the US kind reddens
/// <c>DeclarativeOverviewLaneTests.RetiredKindsAreGone</c>.
/// </para>
///
/// <para>
/// <b>WHAT DID NOT MOVE:</b> every band stays 36x30 (a 36-wide band is what lets the 34x18 track render at
/// its own size instead of being clamped), each control still names the same bool in <c>Bind</c> AND
/// <c>SelectedKey</c>, and the whole arranged band is still the hit target. The row layout is not what this
/// change touches.
/// </para>
///
/// <para>
/// <b>WHAT THE SHARED KIND PAINTS (CURRENT INSPECTED, <c>CheckboxWidget.PaintSwitch</c>):</b> the ON track is
/// the carrier's <c>Active</c> role — <c>SelectedSurface</c> fill + its resolved edge — with <b>the ACCENT
/// ITSELF</b> (<c>AccentGold</c>) on the knob, and the OFF track is the raised plane's fill with the theme's
/// control-edge ladder on its outline. In the flat scope (<c>SelectedBorder == Selected</c>) the ON track's
/// edge is its own fill, so the ON signal is the FILL plus the accent knob, where the retired US kind used a
/// saturated accent edge as well. It is asserted here as the shipped treatment, derived from the theme; see
/// the note on <c>BothStatesAreDrawnAndVisible</c>.
/// </para>
///
/// <para>
/// MUTATION LEDGER (which assertion is the proof and which is only a guard):
/// <list type="bullet">
/// <item><b>TheSwitchesAreDeclaredOverTheirOwnBool</b> — a row left on another kind, an element that dropped
/// <c>Appearance</c>, or a control whose <c>SelectedKey</c> names a different bool, reddens. It is the
/// mutation that would silently re-open the migration.</item>
/// <item><b>BothStatesAreDrawnAndVisible</b> — MUTATION-PROVEN. Every expectation is built from the THEME
/// plus the state this lane READ, never from the widget's own helper: the retired lane measured that
/// comparing against a widget's own <c>Material()</c> stayed green under a state-blind mutation, because
/// both sides read the same bool through the same function.</item>
/// <item><b>TheWholeBandIsTheHitRule</b> — a CONTRACT guard over the carrier's own source (the whole
/// arranged rect is passed to <c>UiNative.Button</c> and the click writes the inverse of the bool it read),
/// plus the readable half (the arranged band is the declared 36x30 and is taller than the track). The WRITE
/// itself is proven next door, by
/// <c>DeclarativeOverviewLaneTests.EachDeclaredControlIsTheOnlyWriteChannel</c>, which drives the real
/// <c>UiNative.Button</c> seam against the same page and asserts one typed write and one revision bump per
/// press — so it is not re-implemented here, and the two lanes must be read together.</item>
/// </list>
/// </para>
///
/// <para>
/// STILL NOT CLAIMED: the look on a real screen, and whether the ON state reads at a glance — the harness
/// has no glyphs and no pixels. That remains a real-screen item, named rather than papered over.
/// </para>
/// </summary>
internal static class UsSquareToggleLaneTests
{
    /// <summary>The carrier kind the eight controls are declared as now.</summary>
    private const string SwitchKind = "input/checkbox";

    /// <summary>The declared appearance. Asserted on the element rather than assumed: `switch` is the kind's
    /// DEFAULT today, so a page that omitted it would keep looking right until the default moved — the
    /// declaration is what makes the look authored.</summary>
    private const string SwitchAppearance = "switch";

    private const float PageWidth = 800f;
    private const float TallHeight = 1100f;

    /// <summary>
    /// The switch's own geometry, mirroring <c>CheckboxWidget</c>'s documented constants (34x18 track, 14px
    /// knob, 2px inset). They are duplicated here on purpose: a lane that read them off the library would not
    /// be able to redden when the library changed them under the US page, and this lane's whole subject is
    /// "does the US page still get the control it declared". The band below is what US declares, and the
    /// relationship between the two is asserted rather than assumed.
    /// </summary>
    private const float TrackWidth = 34f;
    private const float TrackHeight = 18f;
    private const float KnobSize = 14f;
    private const float KnobInset = 2f;

    /// <summary>The band every switch declares. 36 wide because that is what lets the track render its own
    /// 34x18: at 24 the track shrank to 24x18 and the knob's throw fell to 6px, which is the difference
    /// between a switch and a block. A narrower band still shrinks the track rather than overflowing.
    /// Unchanged by the migration — this is US's own declaration and the reason it survives.</summary>
    public const float DeclaredBandWidth = 36f;
    public const float DeclaredBandHeight = 30f;

    /// <summary>The scope the switch rows draw in, so the lane asks the same theme the widget was handed.</summary>
    private const string FlatScheme = "us-flat-panel";

    /// <summary>
    /// The declared switches this lane drives, in the order the page DRAWS them (top to bottom), with the
    /// state the fixture's rich view gives each one. They are the two states of the knob's travel, and both
    /// come from the page's own data - no lane-only seed, and no assumption about what a write through the
    /// binding layer does to this fixture's read-only projection (measured: it does not move it).
    /// <para>
    /// R2 re-cut: the CHECK ids and binds are unchanged (the migration is a kind swap), so this table is the
    /// same contract it was — which is itself evidence that the row layout did not move.
    /// </para>
    /// </summary>
    private static readonly (string RowId, string CheckId, string Bind, bool State)[] Rows =
    {
        // Measured: the egg row is the first toggle the page draws and it is ON; the cooldown row is the
        // second and it is OFF. The written contract is asserted, so a fixture or manifest reorder reddens
        // rather than silently measuring the other control.
        ("basic-egg-row", "basic-egg-check", "allow-eggs", true),
        ("basic-cooldown-row", "basic-cooldown-check", "scale-cooldown", false),
    };

    /// <summary>Every declared switch on the shipped Overview page, by element id, so the retiral's own
    /// completeness is an assertion rather than a claim: a control left behind on the old kind reddens here.
    /// </summary>
    private static readonly string[] DeclaredSwitches =
    {
        "basic-egg-check", "basic-baby-check", "basic-cooldown-check", "basic-talking-check",
        "basic-population-check", "basic-eat-check", "basic-eat-child-check", "camera-indicator-check",
        "diagnostics-localize-check"
    };

    public static int RunAll()
    {
        Step("the switch kinds are declared over the bool each one owns", TheSwitchesAreDeclaredOverTheirOwnBool);
        Step("both states are drawn, and the control is visible in both", BothStatesAreDrawnAndVisible);
        Step("the whole band is the hit rule", TheWholeBandIsTheHitRule);
        Console.WriteLine("UsSquareToggleLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 1
    // ---------------------------------------------------------------------------------------------

    private static void TheSwitchesAreDeclaredOverTheirOwnBool()
    {
        Assert(DeclaredBandWidth >= TrackWidth,
            "the declared band (" + DeclaredBandWidth + "px) must host the track's own width (" + TrackWidth
            + "px); below it the rendered track is CLAMPED to the band and every shape assertion in this lane"
            + " would have to key on the band instead of on the track");

        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });

        // (a) the two rows this lane drives, by id, with their bindings.
        foreach ((string _, string checkId, string bind, bool _) in Rows)
        {
            UiElementSpec? check = FindById(host.Manifest.Roots, checkId);
            Assert(check != null, "the shipped manifest must carry '" + checkId + "'");
            Assert(check!.Kind == SwitchKind,
                "'" + checkId + "' must be the carrier's boolean surface, got '" + check.Kind + "'");
            Assert(check.TryGetAttribute("Appearance", out string appearance) && appearance == SwitchAppearance,
                "'" + checkId + "' must declare Appearance=\"" + SwitchAppearance + "\", got '"
                + (check.TryGetAttribute("Appearance", out string raw) ? raw : "(none)") + "'");
            Assert(check.TryGetAttribute("Bind", out string actualBind) && actualBind == bind,
                "'" + checkId + "' must own the bool binding '" + bind + "', got '" + actualBind + "'");
            // The switch's drawn state comes from the value it READ (the bound bool). SelectedKey is the
            // engine's role-resolution declaration over the SAME bool — the shared kind no longer consults it
            // for the knob's end, but keeping both on one binding is what stops the painted state and the
            // resolved role from drifting apart, so the pairing is asserted rather than tolerated.
            Assert(check.TryGetAttribute("SelectedKey", out string selected) && selected == bind,
                "'" + checkId + "' must name the same bool in SelectedKey as in Bind, so the engine's own role"
                + " resolution agrees with the state this control paints, got '" + selected + "'");
        }

        // (b) COMPLETENESS: every control the migration named is on the shared kind, and the count is tied to
        // the manifest rather than to this list. A control left behind on the retired kind reddens here.
        var onSharedKind = new List<string>();
        foreach (string id in DeclaredSwitches)
        {
            UiElementSpec? spec = FindById(host.Manifest.Roots, id);
            Assert(spec != null, "the shipped manifest must carry '" + id + "'");
            if (spec!.Kind == SwitchKind) onSharedKind.Add(id);
            Assert(spec.Kind == SwitchKind,
                "'" + id + "' must be on the shared kind after the migration, got '" + spec.Kind + "'");
        }

        Assert(onSharedKind.Count == DeclaredSwitches.Length,
            "all " + DeclaredSwitches.Length + " declared switches must be on the shared kind, got "
            + onSharedKind.Count);

        // CONTROL, re-cut by V2 P2 (2026-10-04): the checklist's per-row ENABLE control is the same shipped
        // switch as the ON/OFF controls above, because it is one too - a pack row's enable state. It was
        // declared Width="24" until V2, which CLAMPS the 34x18 track to 24 and cuts the knob's throw to 6px
        // (the clamp this lane's own band comment records); the shipped band is 36. The control is still
        // manifest-level, so the retired kind cannot come back unnoticed.
        string manifest = ReadShippedManifest();
        int checklistDeclarations = System.Text.RegularExpressions.Regex.Matches(
            manifest, "Id=\"checklist-row-check\" Kind=\"input/checkbox\"").Count;
        Assert(checklistDeclarations == 1,
            "control: the checklist's per-row control must still be exactly one input/checkbox declaration, found "
            + checklistDeclarations + " - a blanket kind rename would have taken it too");
        Assert(System.Text.RegularExpressions.Regex.IsMatch(
                manifest, "Id=\"checklist-row-check\"[^>]*Appearance=\"switch\""),
            "V2 P2: the checklist's per-row enable control must declare the shipped switch appearance - it is"
            + " an ON/OFF control, and its look must not be left to the kind's default");
        Assert(System.Text.RegularExpressions.Regex.IsMatch(
                manifest, "Id=\"checklist-row-check\"[^>]*Width=\"36\""),
            "V2 P2: the checklist's per-row enable control must declare the 36-wide band, or the switch's"
            + " 34x18 track is clamped and the knob's throw falls to 6px");
        // The criterion is a DECLARATION, not the bare word: the manifest's own migration note names the
        // retired kind to explain why it is gone, and documentation is not a usage. A blunt Contains() would
        // report that note and would equally miss nothing real, so the matcher is the live declaration.
        Assert(!System.Text.RegularExpressions.Regex.IsMatch(manifest, "Kind=\"us/square-toggle\""),
            "the retired US kind must not survive as a live declaration in the shipped manifest");
    }

    // ---------------------------------------------------------------------------------------------
    // Step 2: drawn relations, not predicted geometry
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// THE MATERIAL OF BOTH STATES, read from the DRAW RECORD, plus the visibility criterion the retired kind's
    /// ruling left behind ("a control that must be visible on a flat plane cannot take its material from the
    /// role of the plane it sits on").
    /// <para>
    /// <b>What the shared kind paints (CURRENT INSPECTED, <c>CheckboxWidget.PaintSwitch</c>).</b> ON is the
    /// role table's <c>Active</c> treatment (<c>SelectedSurface</c>) with <b>the ACCENT ITSELF</b> on the
    /// knob; OFF is the raised plane's FILL plus the theme's control-edge LADDER (<c>BorderStrong</c> when the
    /// palette claims one, else <c>Border</c>), with <c>TextPrimary</c> on the knob. The ladder is what makes
    /// the OFF half survive US's flat scope, which sets <c>Raised == RaisedBorder</c> — the equality that IS
    /// "a flat surface paints no box". The two thumb inks are deliberately different KINDS of token: OFF is
    /// neutral ink on a neutral track, ON is the accent, and neither is <c>TextOnGold</c> (which is ink FOR a
    /// gold plane, not the control's own state).
    /// </para>
    /// <para>
    /// <b>What time did to the ON signal, stated rather than buried.</b> The retired US kind painted an
    /// accent-alpha track fill, a state-blind <c>Border</c> edge and the accent on the knob. The shared kind's
    /// ON track is the <c>Selected</c> token (which the flat scope sets equal to <c>SelectedBorder</c>, so the
    /// ON track's edge is its own fill), so the ON signal is the FILL plus the accent knob, with less gold on
    /// the perimeter than before. The accent knob itself is restored, so the state still reads as the series
    /// accent; whether that is enough at a glance is a REAL-SCREEN item, named rather than asserted.
    /// </para>
    /// </summary>
    private static void BothStatesAreDrawnAndVisible()
    {
        var seen = new List<bool>();
        var fills = new List<Color>();
        var knobs = new List<Color>();
        for (int i = 0; i < Rows.Length; i++)
        {
            seen.Add(OneRowBothStates(Rows[i].RowId, Rows[i].CheckId, Rows[i].Bind, Rows[i].State, i,
                out Color fill, out Color knob));
            fills.Add(fill);
            knobs.Add(knob);
        }

        Assert(seen.Contains(true) && seen.Contains(false),
            "the page's own two rows must be in OPPOSITE states, or this step would have measured one knob end"
            + " twice and could not tell the two apart: " + string.Join(",", seen));

        // The state signal, stated as the shared kind's own contract: each state's knob ink differs from the
        // track it sits in AND the two states' fills differ. Both are read from the draw record, so a
        // state-blind mutation reddens here.
        for (int i = 0; i < Rows.Length; i++)
        {
            Assert(!SameColor(knobs[i], fills[i]),
                Rows[i].Bind + ": the knob must not be the same colour as its own track ("
                + Hex(knobs[i]) + " on " + Hex(fills[i]) + ") - the knob is the state's own signal");
        }

        Assert(!SameColor(fills[0], fills[1]),
            "the ON and OFF rows' track fills must differ, or the ONLY state signal left is the knob's end: "
            + Hex(fills[0]) + " vs " + Hex(fills[1]));
    }

    /// <summary>
    /// Draws one row and reads the relations out of the draw record's own boxes, with the state the page's
    /// OWN data gives it. Nothing here predicts a position from the arranged snapshot: measured, two
    /// consecutive frames can arrange the same page 128px apart, so a lane that computed "where the band will
    /// be" would be measuring a different frame than the one the widget drew in.
    /// </summary>
    private static bool OneRowBothStates(string rowId, string checkId, string bind, bool expectedState,
        int rowIndex, out Color fill, out Color knob)
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            // EatPrecisionEnabled is pinned ON here, and that is a RE-CUT rather than a preference: the
            // manifest declares one more switch than it arranges when the parent is off, because
            // `basic-eat-child-row` carries `VisibleKey="eat-precision"`. With the parent off the page draws
            // 8 tracks and this lane's "one cluster per DECLARED switch" assertion would redden on a page that
            // is behaving correctly - a red for the wrong reason. The old version of this lane ran with the
            // fixture's default (OFF) and counted the manifest's declarations, which is the same defect
            // waiting to fire; the gate is now part of the fixture state the lane declares.
            var source = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
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
            // material. The band's y range is what isolates this row's control from the other switches.
            List<(Rect Rect, Color Colour)> drawn = DrawAndCollect(host, new Vector2(-1000f, -1000f), band,
                rowIndex, out Rect trackFill, out Color trackFillColour);

            // The draw seam in this harness records three of the five: the track's left and right edge strips
            // are two boxes at the same x, so only one survives in the record. The shape is asserted rather
            // than assumed - a fill box, at least one edge strip, and exactly one knob.
            Assert(drawn.Count >= 3,
                bind + ": the switch must record its track fill, its edges and its knob, got " + drawn.Count
                + " boxes: " + Describe(drawn));
            List<(Rect Rect, Color Colour)> edges = drawn.Where(d => Close(d.Rect.height, 1f)).ToList();
            Assert(edges.Count >= 1,
                bind + ": the track must be edged (a 1px strip in the record), got " + Describe(drawn));

            // The knob = the 14x14 box whose top is the track's top offset by the switch's own inset.
            List<(Rect Rect, Color Colour)> knobs = drawn
                .Where(d => Close(d.Rect.width, KnobSize) && Close(d.Rect.height, KnobSize))
                .ToList();
            Assert(knobs.Count == 1,
                bind + ": exactly one " + KnobSize + "px knob must be drawn, got " + knobs.Count + ": "
                + Describe(drawn));
            Rect knobRect = knobs[0].Rect;
            Assert(Close(knobRect.y, trackFill.y + KnobInset),
                bind + ": the knob must sit " + KnobInset + "px below the track's top edge, got "
                + Describe(knobRect) + " vs track " + Describe(trackFill));

            // THE STATE: the knob's end. This is the property the retired US kind existed for, and it is read
            // from the drawn boxes rather than from the state the lane believes it set.
            if (state)
            {
                Assert(Close(knobRect.xMax, trackFill.xMax - KnobInset),
                    bind + "=on: the knob must sit at the track's RIGHT end, got " + Describe(knobRect)
                    + " vs track " + Describe(trackFill));
            }
            else
            {
                Assert(Close(knobRect.x, trackFill.x + KnobInset),
                    bind + "=off: the knob must sit at the track's LEFT end, got " + Describe(knobRect)
                    + " vs track " + Describe(trackFill));
            }

            // THE MATERIAL, and the expectation is built from the THEME + the STATE THIS LANE READ, never from
            // the widget's own helper. The carrier's switch resolves its ON state through the Active role and
            // its OFF state through the raised plane plus the theme's CONTROL-EDGE ladder; its knob is that
            // state's role ink. R12-I re-cut the OFF EDGE expectation onto that ladder, and the re-cut is a
            // fix rather than a loosening: under US's flat scope `Raised == RaisedBorder` (that equality IS
            // "a flat surface paints no box"), so reading the OFF edge from `RaisedSurface.Border` asked for
            // the fill's own colour - the two halves of the old expectation contradicted each other and the
            // control collapsed onto the card face. The ladder (BorderStrong when the palette claims one, else
            // Border) reads an edge from a token the scope does NOT flatten, so a fill and an edge from two
            // independent tokens cannot both disappear.
            Color expectedFill = state ? theme.Selected : theme.RaisedSurface.Fill;
            Color expectedEdge = state
                ? theme.SelectedSurface.Border
                : (theme.BorderStrong.a > 0f ? theme.BorderStrong : theme.Border);
            // The thumb's ink. The two halves are DIFFERENT KINDS of token on purpose (CURRENT INSPECTED,
            // carrier `CheckboxWidget.PaintSwitch`): since SA1.1 OFF is the switch's OWN declared grey
            // (`SwitchThumbOff`, US declares #8a857a from the SR reference in UsTheme) on a neutral track -
            // the grey thumb no longer borrows label ink, so greying it darkens no text - while ON is
            // **the ACCENT ITSELF** (`AccentGold`): "this is on" is what the accent means here and the ON
            // half answers AccentGold whatever SwitchThumbOff says. Unset, the token answers TextPrimary
            // (the historical neutral thumb) - the expectation is derived from the theme, so it follows
            // US's palette wherever the declaration puts it.
            Color expectedKnob = state ? theme.AccentGold : theme.SwitchThumbOff;
            Assert(SameColor(trackFillColour, expectedFill),
                bind + "=" + state + ": the track must be filled with this state's own role material ("
                + Hex(expectedFill) + "), got " + Hex(trackFillColour) + " - a state-blind material reddens"
                + " here");
            Assert(SameColor(edges[0].Colour, expectedEdge),
                bind + "=" + state + ": the track's edge must be this state's own role edge ("
                + Hex(expectedEdge) + "), got " + Hex(edges[0].Colour));
            Assert(SameColor(knobs[0].Colour, expectedKnob),
                bind + "=" + state + ": the knob must be inked with this state's own role ink ("
                + Hex(expectedKnob) + "), got " + Hex(knobs[0].Colour));
            if (!state)
            {
                // SA1.1, named faithful-revert target: the OFF thumb answers the DECLARED grey (SR
                // reference .54,.52,.48 = #8a857a) - not the label ink it used to borrow. Deleting the
                // SwitchThumbOff declaration from UsTheme makes theme.SwitchThumbOff fall back to
                // TextPrimary and reddens BOTH clauses below; greying the thumb must never darken a
                // label, so the second clause pins the exact reference colour.
                Assert(!SameColor(knobs[0].Colour, theme.TextPrimary),
                    bind + ": the OFF thumb must not answer the label ink (SA1.1 declaration)");
                Assert(SameColor(knobs[0].Colour, new Color(138f / 255f, 133f / 255f, 122f / 255f, 1f)),
                    bind + ": the OFF thumb must be the SR-reference grey #8a857a, got "
                    + Hex(knobs[0].Colour));
            }

            // VISIBILITY, the criterion the retired kind's own ruling left behind: the control must be
            // distinguishable from the plane it sits on. On this page the flat scope's Raised IS the card's own
            // face (#191612), so the OFF track's FILL necessarily coincides with the plane and the EDGE is what
            // separates them; the ON track's Selected fill is a different plane, so its fill does the work. The
            // assertion therefore demands that AT LEAST ONE half of the track differ from the plane - which is
            // exactly the difference between this material and the role table's old OFF treatment, where BOTH
            // halves were the plane's own colour and the control vanished.
            Assert(!SameColor(trackFillColour, cardFace) || !SameColor(edges[0].Colour, cardFace),
                bind + "=" + state + ": the track must be visible against the card's own plane (" + Hex(cardFace)
                + ") through at least one of its halves: fill=" + Hex(trackFillColour) + " edge="
                + Hex(edges[0].Colour));

            // The band stays the size the row layout was built around, and the boxes stay inside it.
            Assert(Close(band.width, DeclaredBandWidth) && Close(band.height, DeclaredBandHeight),
                bind + ": the declared band must stay " + DeclaredBandWidth + "x" + DeclaredBandHeight
                + ", got " + Describe(band));
            Assert(trackFill.width <= band.width + 0.5f && trackFill.height <= band.height + 0.5f,
                bind + ": the track must fit inside its declared band, got track " + Describe(trackFill)
                + " in band " + Describe(band));

            fill = trackFillColour;
            knob = knobs[0].Colour;
            Console.WriteLine("[switch] " + rowId + " state=" + state + " band=" + Describe(band) + " track="
                + Describe(trackFill) + " fill=" + Hex(trackFillColour) + " edge=" + Hex(edges[0].Colour)
                + " knob=" + Describe(knobRect) + " ink=" + Hex(knobs[0].Colour) + " cardFace="
                + Hex(cardFace) + " boxes=" + drawn.Count);
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
        // HOW FAR THIS GOES, and why the write itself lives in another lane. The press path is a native IMGUI
        // round trip (MouseDown captures the control, MouseUp at the same point activates it), and this
        // harness CAN drive it - but not by AIMING a pointer: measured repeatedly while the retired lane was
        // written, the frame a widget DRAWS in and the frame the lane ARRANGES are separate arrangements (the
        // same control read (736,270) in one and (752,398) in another), so a press computed from the arranged
        // snapshot lands on the page but not on the control. The lead ruled against adding a public seam for
        // it, and rightly: a seam that exists so a test can hit a control IS vocabulary growth.
        //
        // The WRITE is therefore proven where it can be aimed honestly - DeclarativeOverviewLaneTests records
        // every native button rect the page produces, presses one by RECT, and asserts one typed write and one
        // revision bump per press for the camera control and for each basic-tuning row. Those assertions are
        // the write-once and covered-control evidence; this step owns the two facts they depend on.
        string sourcePath = ResolveCarrierWidgetSource();
        string widget = File.ReadAllText(sourcePath);

        Assert(widget.Contains("if (UiNative.Button(rect, ctx))"),
            "the shared switch must take its hit through the same native button seam every other control uses,"
            + " on the WHOLE arranged rect - the band is the target, not the drawn track");
        Assert(widget.Contains("ctx.Bindings.Set<bool>(key, !state);"),
            "a press must write the INVERSE of the bool the control read");
        Assert(widget.Contains("if (writable == false) return;"),
            "a read-only control must refuse the pointer through the one writability funnel - that is the"
            + " disabled half of this kind's contract, and it is not re-derived here");

        // The readable half of the same claim: the band each control declares is the band the engine hands the
        // control (the arranged rect is the full 36x30 the manifest declares, not the 34x18 track), so
        // "the whole band is the hit target" is a wider promise than "the track is the hit target".
        foreach ((string _, string checkId, string bind, bool _) in Rows)
        {
            // The same RE-CUT as step 2's own per-row lane, for the same measured reason: the manifest
            // declares one more switch than it arranges while the eat-precision parent is OFF, because
            // `basic-eat-child-row` carries VisibleKey="eat-precision". "One cluster per DECLARED switch row"
            // is only a meaningful assertion when the declared set and the arranged set agree, so the gate is
            // part of the fixture state this step declares rather than a count the lane narrows to fit.
            using UiHost host = UsKernelSettingsHost.Create(
                new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true });
            host.Bindings.Invoke("set-tab", "Overview");
            UiLayoutSnapshot snapshot = Arrange(host);
            Rect band = RectOf(snapshot, checkId);
            Assert(Close(band.width, DeclaredBandWidth) && Close(band.height, DeclaredBandHeight),
                bind + ": the control's arranged rect must be the whole declared band (" + DeclaredBandWidth
                + "x" + DeclaredBandHeight + "), got " + Describe(band));
            Assert(band.height > TrackHeight,
                bind + ": and the band must be TALLER than the track it draws, so 'the band is the target' is"
                + " a wider promise than 'the track is the target' (band " + Num(band.height) + "px vs track "
                + TrackHeight + "px)");
            Console.WriteLine("[switch-hit] " + bind + " band=" + Describe(band) + " trackHeight=" + TrackHeight
                + " hit=wholeBand (press path + write: DeclarativeOverviewLaneTests)");
        }
    }

    /// <summary>
    /// The carrier's checkbox source. The library is a SIBLING checkout and is read-only to this consumer; a
    /// clone that does not have it must FAIL this step rather than pass silently, because a lane that quietly
    /// asserted nothing would be the worst of both worlds.
    /// </summary>
    private static string ResolveCarrierWidgetSource()
    {
        string root = Program.RepoRoot();
        string sibling = Path.GetFullPath(Path.Combine(root, "..", "ferritelib", "Source", "FerriteLib.UiKit",
            "Kernel", "Widgets", "CheckboxWidget.cs"));
        Assert(File.Exists(sibling),
            "the carrier's CheckboxWidget.cs must be readable at the sibling checkout for this step to assert"
            + " the shared kind's hit and write contract; looked for '" + sibling + "'");
        return sibling;
    }

    // ---------------------------------------------------------------------------------------------
    // Plumbing
    // ---------------------------------------------------------------------------------------------

    /// <summary>One draw with the stub pointer parked at <paramref name="pointer"/>, returning every solid the
    /// record holds for boxes inside the given band's y range.</summary>
    private static List<(Rect Rect, Color Colour)> DrawAndCollect(UiHost host, Vector2 pointer, Rect band,
        int rowIndex, out Rect trackFillRect, out Color trackFillColour)
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

        System.Collections.IList rects = Recorded("DrawBoxSolidRects");
        System.Collections.IList colors = Recorded("DrawBoxSolidColors");
        Assert(rects.Count == colors.Count,
            "the stub's two solid recorders must stay in step: " + rects.Count + " vs " + colors.Count);

        // A switch's own boxes are exactly five: the track's fill, its four edge strips, and the knob. Every
        // one of them is a RENDERED-TRACK-width box (34x18 fill, 34x1 edges) or the 14px knob - shapes that
        // ARE the control's signature and that no other element on this page produces.
        var signature = new List<(Rect Rect, Color Colour)>();
        for (int i = 0; i < rects.Count; i++)
        {
            Rect rect = (Rect)rects[i]!;
            bool fill = Close(rect.width, TrackWidth) && Close(rect.height, TrackHeight);
            bool edge = Close(rect.width, TrackWidth) && Close(rect.height, 1f);
            bool knob = Close(rect.width, KnobSize) && Close(rect.height, KnobSize);
            if (fill || edge || knob) signature.Add((rect, (Color)colors[i]!));
        }

        // Which CLUSTER of that signature belongs to the row under test: the TOP-most ones ordered by measured
        // y. Measured reason this is a cluster and not a y window: the frame the widget DRAWS in and the frame
        // the lane ARRANGES are not the same arrangement, so the drawn y and the arranged y do not coincide -
        // but their ORDER does, and every switch row contributes one cluster.
        var fills = signature.Where(s => Close(s.Rect.height, TrackHeight)).ToList();
        Assert(fills.Count >= 1,
            "no switch track was drawn on this page at all: " + Describe(signature));
        // WHICH cluster is this row's. The rows are listed in manifest order and so are the clusters (the page
        // scrolls to the top before every draw), so the two orders correspond; the lane ASSERTS that
        // correspondence rather than assuming it silently, and a page that stopped drawing the rows in order
        // reddens instead of measuring the wrong control.
        var ordered = fills.OrderBy(f => f.Rect.y).ToList();

        // ONE CLUSTER PER DECLARED SWITCH ROW on the page, and the row under test is the index the caller
        // names. The count is read from the manifest so it is tied to the page rather than to a literal here:
        // a page that stopped drawing one of them reddens instead of letting the lane read the wrong row.
        int declaredSwitches = CountDeclaredSwitchRows();
        Assert(fills.Count == declaredSwitches,
            "every declared switch row must draw exactly one track (" + declaredSwitches + " rows), got "
            + fills.Count + " tracks: " + Describe(signature));
        (Rect Rect, Color Colour) selected = ordered[rowIndex];
        trackFillRect = selected.Rect;
        // The COLOR the stub recorded for that same box, paired by index from its two parallel recorders.
        // Observed, never the expected value: the material assertions below compare this against the theme.
        trackFillColour = selected.Colour;

        var drawn = signature.Where(s => s.Rect.y >= selected.Rect.y - 2f
            && s.Rect.y <= selected.Rect.y + TrackHeight + 2f).ToList();
        Assert(drawn.Any(d => Close(d.Rect.y, selected.Rect.y) && Close(d.Rect.x, selected.Rect.x)),
            "the selected track must be part of its own cluster");

        return drawn;
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

    private static string ReadShippedManifest()
    {
        string path = Path.Combine(Program.RepoRoot(), "Source", "UniversalSqueaker", "UI", "Layout.Schema2.xml");
        Assert(File.Exists(path), "the shipped manifest must exist at '" + path + "'");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// How many switch rows the shipped manifest declares for the Overview workspace, counted from the
    /// manifest itself so the lane's cluster count is tied to the page rather than to a number written here.
    /// The basic-tuning card's rows plus the camera card's single control are the ones this lane's page draws;
    /// both are gated to the Overview tab, so the declaration count and the drawn count are the same set.
    /// </summary>
    private static int CountDeclaredSwitchRows()
    {
        string manifest = ReadShippedManifest();
        // Counted by the DECLARED APPEARANCE on an ELEMENT, not by the bare attribute text: the manifest's own
        // migration note quotes the attribute while explaining it, and a textual match counted that note as a
        // tenth control - a count that disagreed with every drawn frame for a reason that had nothing to do
        // with the page.
        //
        // V2 P2 RE-CUT of the measurement channel: the checklist's per-row enable control is ALSO a declared
        // switch now (36x30, Appearance="switch"), but it lives on the Packs tab and this lane draws Overview,
        // so it is excluded by id. Counting it here would make the drawn-track count and the declared count
        // disagree for a reason that is not the page.
        int count = 0;
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                     manifest, "<Widget[^>]*Appearance=\"switch\"[^>]*>"))
        {
            if (match.Value.IndexOf("checklist-row-check", StringComparison.Ordinal) < 0) count++;
        }

        return count;
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

    private static System.Collections.IList Recorded(string fieldName)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "the runtime stub does not record '" + fieldName + "'; this lane cannot observe a draw and must"
                + " not pass silently");
        }

        var list = field.GetValue(null) as System.Collections.IList;
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
