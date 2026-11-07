using System;
using System.Collections.Generic;


using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Visual helpers for kernel-owned US composite widgets. These are drawing helpers only; input
/// authority stays with the Host interaction session through <see cref="UiControls"/>. Native GUI state
/// (color/font/anchor) is restored before returning.
///
/// Text contract: every <c>text</c>/<c>label</c>/<c>title</c> parameter here is already-resolved
/// display text. Input helpers receive a <see cref="UiWidgetContext"/> and
/// resolve nothing — option pairs arrive as (display, value) with the display decided by the
/// caller. That is deliberate: the helpers carry no key knowledge, so a string is resolved exactly
/// once at the site that owns it (see <see cref="Keyed"/>), never once for Measure and again for
/// Draw.
/// </summary>
public static partial class UsKernelDraw
{
    public const float RowLeftPadding = 10f;

    /// <summary>RowRail: selected = dim 3px rail, current = accent 3px, both = dual, disabled = hatch.
    /// Attention is the thin cyan edge from <see cref="UsAttention"/> and the row fill stays neutral; the
    /// ruling settled that hue, which closes the former "attn has no chosen hue yet" absence and leaves
    /// <see cref="UsAttention.Brush"/> as the one place the literal may appear. StateShape: the four
    /// effect shapes of spec 1.6, drawn as shape so grey-scale keeps them apart.</summary>
    public enum RowRail
    {
        None,
        Selected,
        Current,
        CurrentAndSelected,
        Disabled,
        Attention
    }

    public enum StateShape
    {
        InEffect,
        Unavailable,
        Inherited,
        Overridden
    }

    /// <summary>Rail widths and shape cells, from the spec's numbers (1.4/1.5/1.6). Public so the lane
    /// pins the geometry the drawing uses instead of re-deriving it from a picture.</summary>
    public const float RailWidth = 3f;

    public const float CurrentAndSelectedOuterWidth = 5f;

    public const float ShapeSize = 10f;

    public const float RowHatchPitch = 5f;

    public const float ShapeHatchPitch = 3f;

    /// <summary>Compatibility overload for the call sites whose state is a plain selected flag. It maps
    /// onto RowRail.Selected and nothing else, so a caller that means the current object says so by name.</summary>
    public static void RowSurface(IUiDrawing drawing, UiLayoutRect rect, UiTheme theme, bool hovered, bool selected, bool danger = false)
    {
        RowSurface(drawing, rect, theme, hovered, selected ? RowRail.Selected : RowRail.None, danger);
    }

    /// <summary>
    /// The row plane plus its semantic rail. The fill rules are unchanged: the accent never fills a row,
    /// because "this row is selected" is not one of the accent's three meanings (where I am, keyboard
    /// focus, what is in effect) and two selected rows would otherwise read as two current objects. The
    /// current-object rail is the accent at 3px; the selected rail is dim ink at the same width; both at
    /// once puts the accent inside a 5px dim rail so neither fact hides the other; an unavailable row gets
    /// the hatch, and its labels stay the caller's job (theme.TextDisabled is the same dim ink).
    /// <para>
    /// An attention row takes the thin cyan edge from <see cref="UsAttention"/> and keeps the neutral
    /// fill: attention is not selection, so the two never share one rail - the ruling puts the attention
    /// mark in the status cell on a row that is also selected.
    /// </para>
    /// </summary>
    public static void RowSurface(IUiDrawing drawing, UiLayoutRect rect, UiTheme theme, bool hovered, RowRail rail, bool danger = false)
    {
        bool selected = rail == RowRail.Selected || rail == RowRail.CurrentAndSelected;
        // theme.Danger, not theme.Warning: the two hold identical bytes today (Warning is the carrier's
        // pre-0.4 redirect onto Danger), and reading the destructive fill through the alias is how a
        // future Warning assignment would silently repaint every danger row. Same pixels, honest name.
        UiColour fill = danger ? theme[UiPaletteRole.DangerSurface]
            : selected ? theme[UiPaletteRole.SelectedSurface]
            : hovered ? theme[UiPaletteRole.HoverSurface]
            : theme[UiPaletteRole.ControlSurface];

        UiColour border = danger ? theme[UiPaletteRole.DangerSurface] : theme[UiPaletteRole.Border];
        UiThemeDraw.Surface(drawing, rect, theme, fill, border);

        // The hatch ink is a plane token one step darker than the row (the spec's stripe is #1b1f27 over
        // #1f232c, which lands on s1 in this table) - never a new colour.
        if (rail == RowRail.Disabled)
        {
            Hatch(drawing, rect, theme[UiPaletteRole.PanelSurface], RowHatchPitch);
        }

        // Rails last, so the plane's edge cannot paint over them, and the dim rail before the accent one so
        // the accent stays the innermost mark in the dual state.
        if (rail == RowRail.CurrentAndSelected)
        {
            UiThemeDraw.Solid(drawing, LeftRail(rect, CurrentAndSelectedOuterWidth), theme[UiPaletteRole.SecondaryForeground]);
            UiThemeDraw.Solid(drawing, LeftRail(rect, RailWidth), theme[UiPaletteRole.Accent]);
        }
        else if (rail == RowRail.Selected)
        {
            UiThemeDraw.Solid(drawing, LeftRail(rect, RailWidth), theme[UiPaletteRole.SecondaryForeground]);
        }
        else if (rail == RowRail.Current)
        {
            UiThemeDraw.Solid(drawing, LeftRail(rect, RailWidth), theme[UiPaletteRole.Accent]);
        }
        else if (rail == RowRail.Attention)
        {
            // Thinner than either location rail (2px against 3px) and cyan, so a grey-scale screenshot
            // still keeps attention apart from "selected" and "current"; the fill above stays neutral.
            UsAttention.Rail(drawing, rect, theme);
        }
    }

    private static UiLayoutRect LeftRail(UiLayoutRect rect, float width)
    {
        return new UiLayoutRect(rect.X, rect.Y, Math.Max(1f, Math.Min(width, rect.Width)), rect.Height);
    }

    /// <summary>Diagonal hatching as 1px dashes stepped along the diagonal. The lab uses
    /// repeating-linear-gradient(45deg, transparent 0 5px, #1b1f27 5px 10px); this library exposes exactly
    /// one fill primitive - an axis-aligned rect (UiThemeDraw.Solid, deliberately, so the backend contact
    /// stays gateable) - and a gradient needs a texture asset. The staircase keeps the pitch, the 45-degree
    /// direction and the darker-than-the-plane relationship, which is what the grey-scale reading needs.</summary>
    public static void Hatch(IUiDrawing drawing, UiLayoutRect rect, UiColour ink, float pitch)
    {
        if (rect.Width <= 0f || rect.Height <= 0f || pitch <= 0f) return;
        float step = Math.Max(2f, pitch);
        for (float offset = -rect.Height; offset < rect.Width; offset += step * 2f)
        {
            for (float y = 0f; y < rect.Height; y += step)
            {
                float start = rect.X + offset + y;
                float left = Math.Max(rect.X, start);
                float right = Math.Min(rect.XMax, start + step);
                if (right - left <= 0f) continue;
                UiThemeDraw.Solid(drawing, new UiLayoutRect(left, rect.Y + y, right - left, 1f), ink);
            }
        }
    }

    /// <summary>One effect shape in a square cell (spec 1.6). Colour carries only "should I act"; the shape
    /// carries which state, which is what makes the four survive a grey-scale screenshot.</summary>
    public static void DrawStateShape(IUiDrawing drawing, UiLayoutRect rect, UiTheme theme, StateShape shape)
    {
        switch (shape)
        {
            case StateShape.InEffect:
                Disc(drawing, rect, theme[UiPaletteRole.Foreground]);
                break;
            case StateShape.Unavailable:
                UiThemeDraw.Surface(drawing, rect, theme, default(UiColour), theme[UiPaletteRole.SecondaryForeground]);
                Hatch(drawing,
                    new UiLayoutRect(rect.X + 1f, rect.Y + 1f, Math.Max(1f, rect.Width - 2f), Math.Max(1f, rect.Height - 2f)),
                    theme[UiPaletteRole.PanelSurface],
                    ShapeHatchPitch);
                break;
            case StateShape.Inherited:
                DottedCircle(drawing, rect, theme[UiPaletteRole.Foreground]);
                break;
            case StateShape.Overridden:
                Disc(drawing, rect, theme[UiPaletteRole.Foreground]);
                Ring(drawing, rect, theme[UiPaletteRole.Accent], 2f);
                break;
        }
    }

    /// <summary>Scanline disc: one rect per row, so the shape is exact at any cell size.</summary>
    private static void Disc(IUiDrawing drawing, UiLayoutRect rect, UiColour ink)
    {
        float radius = Math.Min(rect.Width, rect.Height) * 0.5f;
        float centreX = rect.X + rect.Width * 0.5f;
        float centreY = rect.Y + rect.Height * 0.5f;
        int rows = Math.Max(1, (int)Math.Ceiling(rect.Height));
        for (int i = 0; i < rows; i++)
        {
            float y = rect.Y + i;
            float dy = y + 0.5f - centreY;
            // System.Math rather than Mathf.Sqrt: the harness's Unity stub does not declare Mathf.Sqrt, and
            // the trip guard caught exactly that (2026-09-12) - a consumer-side call to a member the double
            // lacks. The value is identical for this geometry, and the missing stub member is reported to the
            // carrier instead of being papered over.
            float half = (float)Math.Sqrt(Math.Max(0f, radius * radius - dy * dy));
            if (half <= 0.05f) continue;
            UiThemeDraw.Solid(drawing, new UiLayoutRect(centreX - half, y, half * 2f, 1f), ink);
        }
    }

    /// <summary>Outline ring of the requested thickness around the same disc.</summary>
    private static void Ring(IUiDrawing drawing, UiLayoutRect rect, UiColour ink, float thickness)
    {
        float radius = Math.Min(rect.Width, rect.Height) * 0.5f;
        float inner = Math.Max(0f, radius - thickness);
        float centreX = rect.X + rect.Width * 0.5f;
        float centreY = rect.Y + rect.Height * 0.5f;
        int rows = Math.Max(1, (int)Math.Ceiling(rect.Height));
        for (int i = 0; i < rows; i++)
        {
            float y = rect.Y + i;
            float dy = y + 0.5f - centreY;
            float outerHalf = (float)Math.Sqrt(Math.Max(0f, radius * radius - dy * dy));
            if (outerHalf <= 0.05f) continue;
            float innerHalf = (float)Math.Sqrt(Math.Max(0f, inner * inner - dy * dy));
            float band = Math.Max(1f, outerHalf - innerHalf);
            UiThemeDraw.Solid(drawing, new UiLayoutRect(centreX - outerHalf, y, band, 1f), ink);
            if (innerHalf > 0.05f)
            {
                UiThemeDraw.Solid(drawing, new UiLayoutRect(centreX + innerHalf, y, band, 1f), ink);
            }
        }
    }

    /// <summary>A dotted circle outline: short marks around the perimeter with every other step skipped,
    /// which is the closest an axis-aligned fill gets to the spec's dashed ring at a 10px cell.</summary>
    private static void DottedCircle(IUiDrawing drawing, UiLayoutRect rect, UiColour ink)
    {
        float radius = Math.Max(0.5f, Math.Min(rect.Width, rect.Height) * 0.5f - 1f);
        float centreX = rect.X + rect.Width * 0.5f;
        float centreY = rect.Y + rect.Height * 0.5f;
        const int Steps = 20;
        for (int i = 0; i < Steps; i += 2)
        {
            double angle = Math.PI * 2.0 * i / Steps;
            float x = centreX + (float)(Math.Cos(angle) * radius);
            float y = centreY + (float)(Math.Sin(angle) * radius);
            UiThemeDraw.Solid(drawing, new UiLayoutRect(x, y, 2f, 1f), ink);
        }
    }

    /// <summary>Spec 1.4/3-4 checkbox geometry: an 18px visual box inside a 24px hit band, so the pointer
    /// keeps 3px of slack on every side. Public because the lane pins these numbers rather than a picture.</summary>
    public const float CheckboxVisual = 18f;

    public const float CheckboxHit = 24f;

    public const float CheckboxInset = (CheckboxHit - CheckboxVisual) * 0.5f;

    /// <summary>Left offset of the 24px checkbox hit box from its row's right edge, chosen so the 18px
    /// VISUAL box's right edge - not the hit box's - lands exactly on the shared control-column right
    /// edge (<see cref="ControlColumnRightInset"/> + <see cref="CheckboxVisual"/> +
    /// <see cref="CheckboxInset"/>). The hit box keeps its 24px and stays centred on the visual, so its
    /// own right edge deliberately overhangs the column edge by <see cref="CheckboxInset"/> while the
    /// drawn box agrees with the segmented control to the pixel.</summary>
    public const float CheckboxRightInset = ControlColumnRightInset + CheckboxVisual + CheckboxInset;

    /// <summary>Radius 2 as this backend can express it: the four corner cells the outline stops short of.</summary>
    public const float CheckboxCornerCut = 2f;

    /// <summary>
    /// The one placement rule for a checkbox in a row: the 18px visual box is the box the player sees, so
    /// IT is what terminates on the shared control-column right edge (<see cref="ControlColumnRect(UiLayoutRect)"/>)
    /// - a checkbox and the segmented control agree to the pixel. The 24px hit box is centred on the visual
    /// (3px of pointer slack per side) and is vertically centred in the row. Every call site goes through
    /// this, which is what replaced four different hand-picked vertical offsets.
    /// </summary>
    public static UiLayoutRect CheckboxSlot(UiLayoutRect row)
    {
        return new UiLayoutRect(
            row.XMax - CheckboxRightInset,
            row.Y + (row.Height - CheckboxHit) * 0.5f,
            CheckboxHit,
            CheckboxHit);
    }

    /// <summary>
    /// Draws the checkbox and reports the click: <paramref name="hitRect"/> is the 24px slot from
    /// <see cref="CheckboxSlot"/>, the visual is drawn inset 3 inside it. The hit goes through
    /// <see cref="UiControls"/>, so an open popup keeps the click.
    /// <para>
    /// The caller must take this hit band OUT of any row button behind it: two overlapping IMGUI buttons
    /// both report the same click, and one press would toggle twice.
    /// </para>
    /// </summary>
    public static bool Checkbox(UiLayoutRect hitRect, UiWidgetContext ctx, bool value)
    {
        CheckboxSurface(ctx.Drawing,
            new UiLayoutRect(hitRect.X + CheckboxInset, hitRect.Y + CheckboxInset, CheckboxVisual, CheckboxVisual),
            ctx.Theme,
            value);
        return ctx.Controls.Button(hitRect, ctx);
    }

    /// <summary>Draw-only checkbox, for read-only indicators (the camera indicator): no hit band, no click.</summary>
    public static void Checkbox(IUiDrawing drawing, UiLayoutRect rect, UiTheme theme, bool value)
    {
        CheckboxSurface(drawing, rect, theme, value);
    }

    private static void CheckboxSurface(IUiDrawing drawing, UiLayoutRect rect, UiTheme theme, bool value)
    {
        UiThemeDraw.Surface(drawing, rect, theme, theme[UiPaletteRole.ControlSurface], theme[UiPaletteRole.Border]);

        // Radius 2, expressed the only way this library can: the outline's four 2px corner cells are
        // repainted with the box's own plane, so the border stops short of each corner. A real rounded
        // corner needs a primitive the backend contact does not expose (UiThemeDraw has exactly one fill -
        // an axis-aligned rect - on purpose, so the contact stays gateable).
        float cut = Math.Min(CheckboxCornerCut, Math.Min(rect.Width, rect.Height) * 0.25f);
        UiThemeDraw.Solid(drawing, new UiLayoutRect(rect.X, rect.Y, cut, cut), theme[UiPaletteRole.ControlSurface]);
        UiThemeDraw.Solid(drawing, new UiLayoutRect(rect.XMax - cut, rect.Y, cut, cut), theme[UiPaletteRole.ControlSurface]);
        UiThemeDraw.Solid(drawing, new UiLayoutRect(rect.X, rect.YMax - cut, cut, cut), theme[UiPaletteRole.ControlSurface]);
        UiThemeDraw.Solid(drawing, new UiLayoutRect(rect.XMax - cut, rect.YMax - cut, cut, cut), theme[UiPaletteRole.ControlSurface]);

        if (!value) return;

        // "Checked" is a state, and states are carried by shape and ink, not by the accent: the spec's
        // checked box is an ink-solid square with a check mark in the border colour, and the accent would
        // put a fourth accent on every screen that has a checkbox. The lane pins that this fill is the ink
        // token and that no accent colour is painted with it.
        UiLayoutRect ink = new UiLayoutRect(rect.X + 3f, rect.Y + 3f, Math.Max(1f, rect.Width - 6f), Math.Max(1f, rect.Height - 6f));
        UiThemeDraw.Surface(drawing, ink, theme, theme[UiPaletteRole.Foreground], theme[UiPaletteRole.Foreground]);
        CheckMark(drawing, new UiLayoutRect(ink.X + 1f, ink.Y + 1f, Math.Max(1f, ink.Width - 2f), Math.Max(1f, ink.Height - 2f)), theme[UiPaletteRole.Border]);
    }

    /// <summary>The spec's border-coloured check mark. The library has no line primitive, so each stroke is
    /// a staircase of 2px cells - the same explicit-approximation口径 as <see cref="Hatch"/>: two straight
    /// strokes of different lengths at 45 degrees, not a curve.</summary>
    private static void CheckMark(IUiDrawing drawing, UiLayoutRect box, UiColour ink)
    {
        const float cell = 2f;
        float midX = box.X + box.Width * 0.4f;
        Stroke(drawing, box.X, box.Y + box.Height * 0.5f, midX, box.YMax, cell, ink);
        Stroke(drawing, midX, box.YMax, box.XMax, box.Y, cell, ink);
    }

    private static void Stroke(IUiDrawing drawing, float x0, float y0, float x1, float y1, float cell, UiColour ink)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / cell));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            UiThemeDraw.Solid(drawing, new UiLayoutRect(x0 + dx * t, y0 + dy * t, cell, cell), ink);
        }
    }

    /// <summary>Spec 1.4 row system. The visual row follows the theme's density (24px regular / 20px dense,
    /// authored as RowHeight tokens in the manifest's &lt;Styles&gt;), the hit band is always 24px so a dense
    /// row stays a comfortable target, and a row ends in a 1px hairline. The numbers are read from
    /// <see cref="UiTheme.Geometry"/> - the library's density axis - never from a US constant, so one
    /// document moves the whole page.</summary>
    public const float RowMinHit = 24f;

    public static float RowVisualHeight(UiWidgetContext ctx) => ctx.Theme.Geometry.RowHeight;

    public static float RowHitHeight(UiWidgetContext ctx) => Math.Max(RowMinHit, RowVisualHeight(ctx));

    /// <summary>The hit band of a row: the row is hit at 24px even when its visual is 20px (dense), so the
    /// band is centred on the visual row and may extend past it.</summary>
    public static UiLayoutRect RowHitRect(UiLayoutRect row, UiWidgetContext ctx)
    {
        float hit = RowHitHeight(ctx);
        return new UiLayoutRect(row.X, row.Y + (row.Height - hit) * 0.5f, row.Width, hit);
    }

    /// <summary>The row's bottom rule: one hairline in the divider token, so a stack of rows reads as a
    /// list instead of a stack of boxes.</summary>
    public static void RowBottomLine(IUiDrawing drawing, UiLayoutRect row, UiTheme theme)
    {
        float hairline = Math.Max(1f, theme.Geometry.Hairline);
        UiThemeDraw.Solid(drawing, new UiLayoutRect(row.X, row.YMax - hairline, row.Width, hairline), theme[UiPaletteRole.Divider]);
    }

    // ---------------------------------------------------------------------------------------------
    // The one support-row width contract for the Overview diagnostic/support sections.
    //
    // Every row is [label band | control column]. The label takes what is left of the row; the control
    // column keeps one fixed width and one right edge across every row of a section and across the
    // sibling sections (us/diagnostics, us/basic-tuning, us/timing, us/camera-indicator). The label
    // width is derived from the ROW, never from the label, so a translation can never push a control
    // sideways. Measure and Draw both take the geometry from here.
    //
    // Row height keeps the theme's density token as its floor (see RowVisualHeight/RowMinHit); a label
    // that cannot fit the remaining width at one line grows the row instead of being clipped, which is
    // the shape basic-tuning and timing already used. Alignment never depends on that height: the
    // column's width and right edge are computed before any text is measured.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Right inset of the control column from the row's right edge - the single right edge
    /// every support control terminates on (the segmented control, the checkbox slot, the timing number
    /// fields and the timing stepper cluster).</summary>
    public const float ControlColumnRightInset = 10f;

    /// <summary>
    /// The control column's one fixed width. Three equal segmented cells plus two
    /// <see cref="SegmentedCellGap"/>s fit it exactly (66px cells, 54px of text band), so the widest
    /// shipped cell label ("Disabled") keeps a margin against the real font and the column never has to
    /// grow for a translation.
    /// </summary>
    public const float ControlColumnWidth = 210f;

    /// <summary>Breathing room between the label band and the control column.</summary>
    public const float ControlColumnLabelGap = 8f;

    /// <summary>Gap between two segmented cells inside the column.</summary>
    public const float SegmentedCellGap = 6f;

    /// <summary>
    /// The column rect for one row: the same width and the same right edge in every support row. At a
    /// degenerate row width (a layout narrow enough that the column could not fit at all) the width is
    /// clamped to what the row can hold so no control is ever drawn outside its own row; the right edge
    /// never moves.
    /// </summary>
    public static UiLayoutRect ControlColumnRect(UiLayoutRect row)
    {
        float available = Math.Max(1f, row.Width - RowLeftPadding - ControlColumnRightInset);
        float width = Math.Min(ControlColumnWidth, available);
        return new UiLayoutRect(row.XMax - ControlColumnRightInset - width, row.Y, width, row.Height);
    }

    /// <summary>
    /// Width left for a row's label once the control column is reserved. Depends on the row width only -
    /// never on the label - which is what keeps a translation from resizing the column.
    /// </summary>
    public static float RowLabelWidth(float rowWidth)
    {
        float available = Math.Max(1f, rowWidth - RowLeftPadding - ControlColumnRightInset);
        float column = Math.Min(ControlColumnWidth, available);
        return Math.Max(1f, rowWidth - RowLeftPadding - ControlColumnRightInset - column - ControlColumnLabelGap);
    }

    public static float RowLabelWidth(UiLayoutRect row) => RowLabelWidth(row.Width);

    /// <summary>The row's label band: the remaining width, starting at the shared row text inset.</summary>
    public static UiLayoutRect RowLabelRect(UiLayoutRect row, float height)
    {
        return new UiLayoutRect(row.X + RowLeftPadding, row.Y, RowLabelWidth(row), height);
    }

    /// <summary>
    /// The support-row height: the density token as the floor, grown by the wrapped height of the label
    /// measured against the row's own label band. Measure and Draw come through here so a row can never
    /// size one band and draw another.
    /// </summary>
    public static float RowLabelHeight(float rowWidth, UiWidgetContext ctx, string text, UiFont font = UiFont.Small)
    {
        return Math.Max(RowVisualHeight(ctx), ctx.Metrics.MeasureText(text ?? "", font, RowLabelWidth(rowWidth)));
    }

    /// <summary>A measured band for text that is not a support row's label (the row's own stacked heading,
    /// the timing interval caption): the wrapped height at <paramref name="width"/>, floored at
    /// <paramref name="floor"/>.</summary>
    public static float TextBandHeight(float width, UiWidgetContext ctx, string text, UiFont font, float floor)
    {
        return Math.Max(floor, ctx.Metrics.MeasureText(text ?? "", font, Math.Max(1f, width)));
    }

    /// <summary>One equal-width segmented cell inside the control column.</summary>
    public static UiLayoutRect SegmentedCellRect(UiLayoutRect column, int index, int count)
    {
        if (count <= 0) return column;
        float cell = SegmentedCellWidth(column.Width, count);
        return new UiLayoutRect(column.X + index * (cell + SegmentedCellGap), column.Y, cell, column.Height);
    }

    /// <summary>Width of one equal segmented cell for a given column width.</summary>
    public static float SegmentedCellWidth(float columnWidth, int count)
    {
        if (count <= 0) return Math.Max(1f, columnWidth);
        return Math.Max(1f, (columnWidth - SegmentedCellGap * (count - 1)) / count);
    }

    /// <summary>Text band inside one segmented cell (the same inset SelectionButton uses).</summary>
    public static float SegmentedCellLabelWidth(float columnWidth, int count)
    {
        return Math.Max(1f, SegmentedCellWidth(columnWidth, count) - SelectionButtonLabelInset * 2f);
    }

    /// <summary>
    /// The three-choice support control: one segmented control whose total width IS the control column,
    /// with equal cells that never size from a translated label. The display text is ellipsized into the
    /// cell's own text band through the injected metric seam, so a language whose word for "Disabled" is
    /// longer cuts deliberately instead of resizing the column. A cell whose text band is too small to
    /// hold even an ellipsis draws no glyph at all (the degenerate-width case). Returns the clicked
    /// option index, or -1.
    /// </summary>
    public static int SegmentedRow(
        UiLayoutRect column,
        UiWidgetContext ctx,
        IReadOnlyList<KeyValuePair<string, string>> options,
        int selectedIndex)
    {
        if (options == null || options.Count == 0) return -1;

        int clicked = -1;
        float labelWidth = SegmentedCellLabelWidth(column.Width, options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            UiLayoutRect cell = SegmentedCellRect(column, i, options.Count);
            HelpHover(cell, ctx, options[i].Value);
            string display = labelWidth >= MinSegmentedLabelWidth
                ? Ellipsized(options[i].Key ?? "", ctx, UiFont.Tiny, labelWidth)
                : "";
            if (SelectionButton(cell, ctx, display, ctx.Theme, i == selectedIndex, controlId: "segment", itemKey: options[i].Value))
            {
                clicked = i;
            }
        }

        return clicked;
    }

    /// <summary>A cell narrower than this cannot hold even an ellipsized glyph, so the segmented row
    /// draws its plane without text instead of reporting a width overflow in a 1px band.</summary>
    public const float MinSegmentedLabelWidth = 12f;

    /// <param name="singleLine">
    /// Forwarded to the fitting audit. True for surfaces that must render on one line (dropdown trigger
    /// and option rows), where the real failure mode is a too-narrow rect rather than a too-short band.
    /// </param>
    public static void Label(IUiDrawing drawing,
        UiLayoutRect rect,
        string text,
        UiTheme theme,
        UiColour? color = null,
        UiFont? font = null,
        UiTextAnchor anchor = UiTextAnchor.MiddleLeft,
        bool singleLine = false)
    {
        UiThemeDraw.Label(drawing, rect, text, theme, color ?? theme[UiPaletteRole.Foreground], font ?? UiFont.Small, anchor, singleLine);
    }

    /// <summary>
    /// The single keyed-text outlet for US kernel widgets: resolves a Keyed entry name through the
    /// Host-provided translation seam. Call sites resolve once here and hand the resulting string to
    /// the drawing helpers; when one string feeds both Measure and Draw, both passes must come back
    /// through this method, otherwise the measured height belongs to a different string than the one
    /// actually drawn.
    /// </summary>
    public static string Keyed(UiWidgetContext ctx, string key)
    {
        return ctx.Translation.Translate(key);
    }

    /// <summary>Context-bound overloads: widgets pass ctx once and the helper binds theme+seam.
    /// Same single outlet contract - the string arrives resolved, nothing is translated here.</summary>
    public static void Label(UiLayoutRect rect, string text, UiWidgetContext ctx, UiFont? font = null,
        UiTextAnchor anchor = UiTextAnchor.MiddleLeft, bool singleLine = false)
        => Label(ctx.Drawing, rect, text, ctx.Theme, null, font, anchor, singleLine);

    public static void Label(UiLayoutRect rect, string text, UiWidgetContext ctx, UiColour color, UiFont? font = null,
        UiTextAnchor anchor = UiTextAnchor.MiddleLeft, bool singleLine = false)
        => Label(ctx.Drawing, rect, text, ctx.Theme, color, font, anchor, singleLine);

    /// <summary>A string search editor uses the same retained Live funnel as declarative text fields.</summary>
    public static UiEditSession<string> LiveTextField(UiLayoutRect rect, UiWidgetContext ctx, string binding)
    {
        ctx = ctx.Child(binding); // One retained editor owner per declared binding.
        string current = ctx.Bindings.Get<string>(binding) ?? "";
        ctx.Controls.Edit(rect, ctx, binding, current, value => value, ParseText, live: true, multiline: false,
            value => ctx.Bindings.Set(binding, value), () => ctx.Bindings.Get<string>(binding) ?? "");
        return ctx.Controls.Editor<string>(ctx, binding, value => value, ParseText);
    }

    private static bool ParseText(string text, out string value) { value = text; return true; }

    /// <summary>
    /// The single outlet for claiming a control's help entry on hover (C+A model): while the pointer is
    /// over <paramref name="rect"/> the panel explains that entry; with no claim it falls back to the
    /// active section's overview. The claim is a per-pass transient owned by the session — FL P3's
    /// <c>UiSession.PublishHelp</c>/<c>CurrentHelpRef</c> machine runs its frame boundary inside
    /// <c>DrawFrame</c> and holds a finished claim for <c>HelpGracePasses</c> passes, so a control that
    /// stops being hovered stops being shown without any cleanup of its own, and a pointer crossing the
    /// gap between two neighbours never flashes the overview. Widgets must call this from <c>Draw</c>
    /// with the rect they actually drew (scroll-local space is fine; the group translation stays honest
    /// inside the pass because hover is read through <c>ctx.Controls.IsMouseOver</c>).
    /// </summary>
    /// <returns>Whether the pointer is over the rect, so callers can reuse it for row highlighting.</returns>
    public static bool HelpHover(UiLayoutRect rect, UiWidgetContext ctx, string helpRef)
    {
        // Remember the composite's declared general topic for its OWN popup's empty options.
        // This is authored metadata, not a hover claim; covered/clipped content cannot publish.
        if (!string.IsNullOrEmpty(helpRef))
        {
            var general = ctx.State("us/composite-help", () => new CompositeHelp());
            general.Topic = helpRef; general.Pass = ctx.Session.Frame;
        }
        bool hovered = ctx.Controls.IsMouseOver(rect, ctx);
        if (hovered && !string.IsNullOrEmpty(helpRef))
        {
            ctx.Session.PublishHelp(helpRef); // A catalog reference, not translated text.
        }
        return hovered;
    }

    private sealed class CompositeHelp
    {
        internal string Topic = "";
        internal int Pass = -1;
    }

    private static void PopupHelp<T>(UiWidgetContext owner, IReadOnlyList<UiChoice<T>> choices, int hovered)
    {
        if (hovered < 0 || hovered >= choices.Count) return;
        string topic = choices[hovered].HelpRef;
        if (string.IsNullOrEmpty(topic))
        {
            var general = owner.State("us/composite-help", () => new CompositeHelp());
            if (general.Pass == owner.Session.Frame) topic = general.Topic;
        }
        if (!string.IsNullOrEmpty(topic)) owner.Session.PublishHelp(topic);
    }

    /// <summary>
    /// Side inset of a selection button's label from its own rect. A caller that sizes a band for this
    /// label must measure at <c>rect.Width - SelectionButtonLabelInset * 2</c>, not at the rect width:
    /// measuring the outer rect lets a label wrap at draw time inside a band sized for one line.
    /// </summary>
    public const float SelectionButtonLabelInset = 6f;

    /// <summary>
    /// Draws a selection-style button surface and returns whether it was clicked. The click goes through
    /// <see cref="UiControls"/>, so a control covered by an open popup yields
    /// the click to the popup instead of stealing it (FL api-tiers.md: the context-free overload is
    /// outside the hit stack). A composite with several controls must supply a stable action identity
    /// and, where applicable, its business item key; labels and positions are not identities.
    /// </summary>
    public static bool SelectionButton(UiLayoutRect rect, UiWidgetContext ctx, string label, UiTheme theme, bool selected, bool danger = false, UiFont? font = null, string controlId = "activation", object? itemKey = null)
    {
        RowSurface(ctx.Drawing, rect, theme, ctx.Controls.IsMouseOver(rect), selected ? RowRail.Selected : RowRail.None, danger);
        Label(ctx.Drawing,
            new UiLayoutRect(rect.X + SelectionButtonLabelInset, rect.Y, Math.Max(1f, rect.Width - SelectionButtonLabelInset * 2f), rect.Height),
            label,
            theme,
            danger ? theme[UiPaletteRole.DangerForeground] : selected ? theme[UiPaletteRole.SelectedForeground] : theme[UiPaletteRole.Foreground],
            font ?? UiFont.Tiny,
            UiTextAnchor.MiddleLeft);
        return ctx.Controls.Activation(rect, ctx, controlId, controlId, itemKey).Request;
    }

    /// <summary>
    /// Self-drawn dropdown trigger + session popup used by composites for dynamic option lists.
    /// Options are (display, value) pairs; selecting invokes <paramref name="onSelected"/>.
    /// <para>
    /// This is the string-payload form. A composite that owns a TYPED value uses the
    /// <see cref="UiChoice{T}"/> overload below instead, so the committed value never becomes a string.
    /// </para>
    /// </summary>
    public static void Dropdown(
        UiLayoutRect rect, string popupId, UiWidgetContext ctx, string current,
        IReadOnlyList<KeyValuePair<string, string>> options, Action<string> onSelected)
    {
        var choices = new UiChoice<string>[options.Count];
        for (int i = 0; i < options.Count; i++)
            choices[i] = new UiChoice<string>(options[i].Key, options[i].Value);
        Dropdown(rect, popupId, ctx, current, choices, onSelected);
    }

    /// <summary>Actual values, labels and explicit topic references stay together through the popup.
    /// Empty HelpRef means no option topic; it never guesses from value, label or row index.</summary>
    public static void Dropdown<T>(
        UiLayoutRect rect, string popupId, UiWidgetContext ctx, T current,
        IReadOnlyList<UiChoice<T>> choices, Action<T> onSelected)
    {
        if (choices.Count == 0) return;
        UiWidgetContext helpOwner = ctx;
        ctx = ctx.Child(popupId); // Distinct triggers must not share one interaction node.
        string display = "";
        for (int i = 0; i < choices.Count; i++)
            if (EqualityComparer<T>.Default.Equals(choices[i].Value, current))
            {
                display = ChoiceText(choices[i], ctx);
                break;
            }
        bool open = ctx.Session.IsPopupOpen(popupId);
        UiResolvedStyle style = ctx.Theme.Styles.Resolve(
            open ? UiStatusTone.Active : UiStatusTone.Neutral, UiEmphasis.Normal, writable: true);
        UiLayoutRect outlet = UiThemeDraw.SelectorTextOutlet(rect, ctx.Theme);
        string trigger = EllipsizeToFit(display, Math.Max(1f, outlet.Width), ctx.Metrics, UiFont.Tiny);
        UiThemeDraw.SelectorField(ctx.Drawing, rect, trigger, ctx.Theme, style, UiFont.Tiny);
        ctx.Controls.DropdownButton(rect, popupId, ctx);
        if (!ctx.Session.IsPopupOpen(popupId) || !ctx.Session.OpenPopupAnchor.HasValue) return;
        UiLayoutRect anchor = ctx.Session.OpenPopupAnchor.Value;
        float width = anchor.Width;
        for (int i = 0; i < choices.Count; i++)
            width = Math.Max(width, ctx.Metrics.MeasureWidth(ChoiceText(choices[i], ctx), UiFont.Small) + 12f);
        if (ctx.Session.HostViewport.Width > 0f) width = Math.Min(width, ctx.Session.HostViewport.Width);
        anchor.Width = width;
        UiLayoutRect popup = UiPopup.RectFor(anchor, choices.Count, ctx.Session.HostViewport);
        ctx.Session.RegisterPopupDraw(() =>
        {
            UiFitAudit.BeginElement(ctx.ElementPath + "/popup");
            try
            {
                int hovered = UiPopup.DrawChoices(popup, popupId, ctx, choices, current, onSelected);
                if (ctx.Session.IsActive && ctx.Session.IsPopupOpen(popupId)) PopupHelp(helpOwner, choices, hovered);
            }
            finally { UiFitAudit.EndElement(); }
        });
    }

    private static string ChoiceText<T>(UiChoice<T> choice, UiWidgetContext ctx)
        => choice.TextKey.Length == 0 ? choice.Text : ctx.Translation.Translate(choice.TextKey);

    /// <summary>
    /// Shortens <paramref name="text"/> to the longest prefix that fits <paramref name="maxWidth"/>
    /// plus an ellipsis, measured through the injected seam (harness and game truncate identically).
    /// Returns the text untouched when it already fits.
    /// </summary>
    private static string EllipsizeToFit(string text, float maxWidth, FerriteLib.UiKit.Kernel.ITextMetrics metrics, UiFont font)
    {
        if (string.IsNullOrEmpty(text) || metrics.MeasureWidth(text, font) <= maxWidth) return text;
        int lo = 0;
        int hi = text.Length;
        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            if (metrics.MeasureWidth(text.Substring(0, mid) + "…", font) <= maxWidth) lo = mid;
            else hi = mid - 1;
        }
        return text.Substring(0, lo) + "…";
    }

    /// <summary>Public seam over the same ellipsize rule the dropdown trigger uses: single-line
    /// cells (diagnostics summary rows) cut through the injected metrics so harness and game
    /// truncate identically.</summary>
    public static string Ellipsized(string text, UiWidgetContext ctx, UiFont font, float maxWidth)
        => EllipsizeToFit(text, maxWidth, ctx.Metrics, font);

    /// <summary>
    /// Shortens <paramref name="text"/> to the longest prefix that fits <paramref name="maxLines"/>
    /// wrapped lines of <paramref name="font"/> at <paramref name="width"/>, appending an ellipsis.
    /// The navigation cards reserve a fixed subtitle band, so a translation that needs more lines than
    /// the band holds is cut here rather than resizing the card or drawing outside it. Returns the text
    /// untouched when it already fits.
    /// </summary>
    public static string EllipsizedToLines(string text, UiWidgetContext ctx, UiFont font, float width, int maxLines)
    {
        if (string.IsNullOrEmpty(text) || maxLines <= 0) return text ?? "";
        float lineHeight = LineHeightOf(ctx, font);
        float maxHeight = lineHeight * maxLines;
        if (ctx.Metrics.MeasureText(text, font, width) <= maxHeight) return text;

        // A band too narrow to hold even the ellipsis glyph draws nothing rather than a mark that
        // overflows - the degenerate-width case, same rule SegmentedRow uses.
        if (ctx.Metrics.MeasureText("…", font, width) > maxHeight) return "";

        int lo = 0;
        int hi = text.Length;
        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            if (ctx.Metrics.MeasureText(text.Substring(0, mid) + "…", font, width) <= maxHeight) lo = mid;
            else hi = mid - 1;
        }

        return text.Substring(0, lo) + "…";
    }

    /// <summary>One line's advance for a font, measured through the injected seam at a width no line can
    /// wrap into two (a fixed band wants the per-line number, not the wrapped one).</summary>
    public static float LineHeightOf(UiWidgetContext ctx, UiFont font)
    {
        return Math.Max(1f, ctx.Metrics.MeasureText("Ag", font, 100000f));
    }

    /// <summary>Card frame with a title header; returns the inner body rect.</summary>
    public static UiLayoutRect DrawCard(IUiDrawing drawing, UiLayoutRect rect, string title, UiTheme theme)
    {
        UiThemeDraw.Panel(drawing, rect, theme);
        Label(drawing,
            new UiLayoutRect(rect.X + UsCardLayout.Padding, rect.Y + 2f, Math.Max(1f, rect.Width - UsCardLayout.Padding * 2f), 22f),
            title,
            theme,
            theme[UiPaletteRole.Foreground],
            UiFont.Small,
            UiTextAnchor.MiddleLeft);
        UiThemeDraw.Surface(drawing,
            new UiLayoutRect(rect.X + UsCardLayout.Padding, rect.Y + UsCardLayout.HeaderHeight - 2f, Math.Max(1f, rect.Width - UsCardLayout.Padding * 2f), 1f),
            theme,
            theme[UiPaletteRole.Divider],
            theme[UiPaletteRole.Divider]);
        return new UiLayoutRect(
            rect.X + UsCardLayout.Padding,
            rect.Y + UsCardLayout.HeaderHeight + UsCardLayout.HeaderGap,
            Math.Max(1f, rect.Width - UsCardLayout.Padding * 2f),
            Math.Max(1f, rect.Height - UsCardLayout.HeaderHeight - UsCardLayout.HeaderGap - UsCardLayout.Padding * 2f));
    }

    /// <summary>Border drawn around the card the help panel is currently explaining (follows the live
    /// hover claim). Neutral BorderStrong rather than the accent: the help claim is not one of the
    /// accent's three uses (05 §3.1).</summary>
    public static void DrawHelpFocusBorder(IUiDrawing drawing, UiLayoutRect rect, UiTheme theme, bool focused)
    {
        if (!focused) return;
        UiThemeDraw.Surface(drawing, rect, theme, default(UiColour), theme[UiPaletteRole.StrongBorder]);
    }
}
