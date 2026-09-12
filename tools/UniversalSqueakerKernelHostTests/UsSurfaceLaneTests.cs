using System;
using System.Collections;
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
    }

    /// <summary>
    /// Section 1.1's table, slot by slot. The library template is the starting point, so every row here is
    /// a deliberate override; anything not overridden stays the library's value, which is why
    /// <c>AccentGold</c> is asserted against the library rather than against a literal.
    /// </summary>
    private static void TableCarriesSpecValues()
    {
        UiTheme theme = UsTheme.Surface();
        UiTheme library = UiTheme.DarkGold;

        Assert(Same(theme.AccentGold, library.AccentGold),
            "the identity accent must stay the library's series gold, not a new colour");

        Assert(Same(theme.Base, Rgb(0x0f, 0x11, 0x16)) && Same(theme.WorkspacePlane, Rgb(0x0f, 0x11, 0x16)),
            "s0 is the window and work plane");
        Assert(Same(theme.Panel, Rgb(0x17, 0x1a, 0x21)) && Same(theme.SectionBand, Rgb(0x17, 0x1a, 0x21)),
            "s1 is the panel and band plane");
        Assert(Same(theme.Raised, Rgb(0x1f, 0x23, 0x2c)),
            "s2 is the control base");
        Assert(Same(theme.Hover, Rgb(0x1f, 0x23, 0x2c)),
            "the hover plane is the spec's hover row plane");
        Assert(Same(theme.Selected, Rgb(0x1c, 0x21, 0x2b)),
            "the selected row keeps its own plane");
        Assert(Same(theme.Border, Rgb(0x33, 0x3a, 0x46)) && Same(theme.Divider, Rgb(0x23, 0x28, 0x33)),
            "structure and the row divider are the two line strengths");
        Assert(Same(theme.BorderStrong, Rgb(0x3d, 0x44, 0x52)),
            "the window edge is one step brighter than the content rule");
        Assert(Same(theme.TextPrimary, Rgb(0xe6, 0xe9, 0xee))
            && Same(theme.TextSecondary, Rgb(0x98, 0xa1, 0xaf))
            && Same(theme.TextDisabled, Rgb(0x98, 0xa1, 0xaf)),
            "ink, dim, and dim again for unavailable text");
        Assert(Same(theme.Danger, Rgb(0x3a, 0x1f, 0x1f))
            && theme.DangerBorder.HasValue
            && Same(theme.DangerBorder.Value, Rgb(0xc8, 0x5a, 0x5a))
            && Same(theme.TextOnDanger, Rgb(0xff, 0xd9, 0xd9)),
            "destructive action: dark plane, saturated edge, light text");

        Assert(!Same(theme.Base, theme.Panel) && !Same(theme.Panel, theme.Raised),
            "the three surface layers must stay distinguishable from each other");

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
        UsKernelDraw.Checkbox(new Rect(0f, 0f, 18f, 18f), theme, value: true);

        IList solids = RecordedSolids();
        Assert(solids.Count >= 10, "the box paints its frame and then the checked square; recorded " + solids.Count);
        Assert(Same(Colour(solids[5]), theme.TextPrimary) && Same(Colour(solids[6]), theme.TextPrimary),
            "the checked square is filled with the ink token");
        Assert(!Painted(theme.AccentGold), "a checked box must not paint the accent");
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
