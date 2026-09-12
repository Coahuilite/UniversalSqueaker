using System;
using System.Collections.Generic;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Visual helpers for kernel-owned US composite widgets. These are drawing helpers only; input
/// authority stays with native IMGUI controls through <see cref="UiNative"/>. All global GUI state
/// (color/font/anchor) is restored before returning.
///
/// Text contract: every <c>text</c>/<c>label</c>/<c>title</c> parameter here is already-resolved
/// display text. Only <see cref="Dropdown"/> sees a <see cref="UiWidgetContext"/>, and even it
/// resolves nothing — option pairs arrive as (display, value) with the display decided by the
/// caller. That is deliberate: the helpers carry no key knowledge, so a string is resolved exactly
/// once at the site that owns it (see <see cref="Keyed"/>), never once for Measure and again for
/// Draw.
/// </summary>
public static class UsKernelDraw
{
    public const float RowLeftPadding = 10f;

    /// <summary>RowRail: selected = dim 3px rail, current = accent 3px, both = dual, disabled = hatch.
    /// StateShape: the four effect shapes of spec 1.6, drawn as shape so grey-scale keeps them apart.
    /// The conflict rail and shape are deliberately absent: attn has no chosen hue yet.</summary>
    public enum RowRail
    {
        None,
        Selected,
        Current,
        CurrentAndSelected,
        Disabled
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
    public static void RowSurface(Rect rect, UiTheme theme, bool hovered, bool selected, bool danger = false)
    {
        RowSurface(rect, theme, hovered, selected ? RowRail.Selected : RowRail.None, danger);
    }

    /// <summary>
    /// The row plane plus its semantic rail. The fill rules are unchanged: the accent never fills a row,
    /// because "this row is selected" is not one of the accent's three meanings (where I am, keyboard
    /// focus, what is in effect) and two selected rows would otherwise read as two current objects. The
    /// current-object rail is the accent at 3px; the selected rail is dim ink at the same width; both at
    /// once puts the accent inside a 5px dim rail so neither fact hides the other; an unavailable row gets
    /// the hatch, and its labels stay the caller's job (theme.TextDisabled is the same dim ink).
    /// </summary>
    public static void RowSurface(Rect rect, UiTheme theme, bool hovered, RowRail rail, bool danger = false)
    {
        bool selected = rail == RowRail.Selected || rail == RowRail.CurrentAndSelected;
        Color fill = danger ? theme.Warning
            : selected ? theme.Selected
            : hovered ? theme.Hover
            : theme.Raised;

        Color border = danger ? theme.Danger : theme.Border;
        UiThemeDraw.Surface(rect, theme, fill, border);

        // The hatch ink is a plane token one step darker than the row (the spec's stripe is #1b1f27 over
        // #1f232c, which lands on s1 in this table) - never a new colour.
        if (rail == RowRail.Disabled)
        {
            Hatch(rect, theme.Panel, RowHatchPitch);
        }

        // Rails last, so the plane's edge cannot paint over them, and the dim rail before the accent one so
        // the accent stays the innermost mark in the dual state.
        if (rail == RowRail.CurrentAndSelected)
        {
            UiThemeDraw.Solid(LeftRail(rect, CurrentAndSelectedOuterWidth), theme.TextSecondary);
            UiThemeDraw.Solid(LeftRail(rect, RailWidth), theme.AccentGold);
        }
        else if (rail == RowRail.Selected)
        {
            UiThemeDraw.Solid(LeftRail(rect, RailWidth), theme.TextSecondary);
        }
        else if (rail == RowRail.Current)
        {
            UiThemeDraw.Solid(LeftRail(rect, RailWidth), theme.AccentGold);
        }
    }

    private static Rect LeftRail(Rect rect, float width)
    {
        return new Rect(rect.x, rect.y, Mathf.Max(1f, Mathf.Min(width, rect.width)), rect.height);
    }

    /// <summary>Diagonal hatching as 1px dashes stepped along the diagonal. The lab uses
    /// repeating-linear-gradient(45deg, transparent 0 5px, #1b1f27 5px 10px); this library exposes exactly
    /// one fill primitive - an axis-aligned rect (UiThemeDraw.Solid, deliberately, so the backend contact
    /// stays gateable) - and a gradient needs a texture asset. The staircase keeps the pitch, the 45-degree
    /// direction and the darker-than-the-plane relationship, which is what the grey-scale reading needs.</summary>
    public static void Hatch(Rect rect, Color ink, float pitch)
    {
        if (rect.width <= 0f || rect.height <= 0f || pitch <= 0f) return;
        float step = Mathf.Max(2f, pitch);
        for (float offset = -rect.height; offset < rect.width; offset += step * 2f)
        {
            for (float y = 0f; y < rect.height; y += step)
            {
                float start = rect.x + offset + y;
                float left = Mathf.Max(rect.x, start);
                float right = Mathf.Min(rect.xMax, start + step);
                if (right - left <= 0f) continue;
                UiThemeDraw.Solid(new Rect(left, rect.y + y, right - left, 1f), ink);
            }
        }
    }

    /// <summary>One effect shape in a square cell (spec 1.6). Colour carries only "should I act"; the shape
    /// carries which state, which is what makes the four survive a grey-scale screenshot.</summary>
    public static void DrawStateShape(Rect rect, UiTheme theme, StateShape shape)
    {
        switch (shape)
        {
            case StateShape.InEffect:
                Disc(rect, theme.TextPrimary);
                break;
            case StateShape.Unavailable:
                UiThemeDraw.Surface(rect, theme, Color.clear, theme.TextSecondary);
                Hatch(
                    new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(1f, rect.width - 2f), Mathf.Max(1f, rect.height - 2f)),
                    theme.Panel,
                    ShapeHatchPitch);
                break;
            case StateShape.Inherited:
                DottedCircle(rect, theme.TextPrimary);
                break;
            case StateShape.Overridden:
                Disc(rect, theme.TextPrimary);
                Ring(rect, theme.AccentGold, 2f);
                break;
        }
    }

    /// <summary>Scanline disc: one rect per row, so the shape is exact at any cell size.</summary>
    private static void Disc(Rect rect, Color ink)
    {
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        float centreX = rect.x + rect.width * 0.5f;
        float centreY = rect.y + rect.height * 0.5f;
        int rows = Mathf.Max(1, Mathf.CeilToInt(rect.height));
        for (int i = 0; i < rows; i++)
        {
            float y = rect.y + i;
            float dy = y + 0.5f - centreY;
            // System.Math rather than Mathf.Sqrt: the harness's Unity stub does not declare Mathf.Sqrt, and
            // the trip guard caught exactly that (2026-09-12) - a consumer-side call to a member the double
            // lacks. The value is identical for this geometry, and the missing stub member is reported to the
            // carrier instead of being papered over.
            float half = (float)Math.Sqrt(Mathf.Max(0f, radius * radius - dy * dy));
            if (half <= 0.05f) continue;
            UiThemeDraw.Solid(new Rect(centreX - half, y, half * 2f, 1f), ink);
        }
    }

    /// <summary>Outline ring of the requested thickness around the same disc.</summary>
    private static void Ring(Rect rect, Color ink, float thickness)
    {
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        float inner = Mathf.Max(0f, radius - thickness);
        float centreX = rect.x + rect.width * 0.5f;
        float centreY = rect.y + rect.height * 0.5f;
        int rows = Mathf.Max(1, Mathf.CeilToInt(rect.height));
        for (int i = 0; i < rows; i++)
        {
            float y = rect.y + i;
            float dy = y + 0.5f - centreY;
            float outerHalf = (float)Math.Sqrt(Mathf.Max(0f, radius * radius - dy * dy));
            if (outerHalf <= 0.05f) continue;
            float innerHalf = (float)Math.Sqrt(Mathf.Max(0f, inner * inner - dy * dy));
            float band = Mathf.Max(1f, outerHalf - innerHalf);
            UiThemeDraw.Solid(new Rect(centreX - outerHalf, y, band, 1f), ink);
            if (innerHalf > 0.05f)
            {
                UiThemeDraw.Solid(new Rect(centreX + innerHalf, y, band, 1f), ink);
            }
        }
    }

    /// <summary>A dotted circle outline: short marks around the perimeter with every other step skipped,
    /// which is the closest an axis-aligned fill gets to the spec's dashed ring at a 10px cell.</summary>
    private static void DottedCircle(Rect rect, Color ink)
    {
        float radius = Mathf.Max(0.5f, Mathf.Min(rect.width, rect.height) * 0.5f - 1f);
        float centreX = rect.x + rect.width * 0.5f;
        float centreY = rect.y + rect.height * 0.5f;
        const int Steps = 20;
        for (int i = 0; i < Steps; i += 2)
        {
            double angle = Math.PI * 2.0 * i / Steps;
            float x = centreX + (float)(Math.Cos(angle) * radius);
            float y = centreY + (float)(Math.Sin(angle) * radius);
            UiThemeDraw.Solid(new Rect(x, y, 2f, 1f), ink);
        }
    }

    /// <summary>Spec 1.4/3-4 checkbox geometry: an 18px visual box inside a 24px hit band, so the pointer
    /// keeps 3px of slack on every side. Public because the lane pins these numbers rather than a picture.</summary>
    public const float CheckboxVisual = 18f;

    public const float CheckboxHit = 24f;

    public const float CheckboxInset = (CheckboxHit - CheckboxVisual) * 0.5f;

    /// <summary>Right inset of the visual box from its row's right edge - part of the one placement rule.</summary>
    public const float CheckboxRightInset = 34f;

    /// <summary>Radius 2 as this backend can express it: the four corner cells the outline stops short of.</summary>
    public const float CheckboxCornerCut = 2f;

    /// <summary>
    /// The one placement rule for a checkbox in a row: the 24px hit box is vertically centred in the row,
    /// and the 18px visual keeps a <see cref="CheckboxRightInset"/> right inset. Every call site goes
    /// through this, which is what replaced four different hand-picked vertical offsets.
    /// </summary>
    public static Rect CheckboxSlot(Rect row)
    {
        return new Rect(
            row.xMax - CheckboxRightInset - CheckboxInset,
            row.y + (row.height - CheckboxHit) * 0.5f,
            CheckboxHit,
            CheckboxHit);
    }

    /// <summary>
    /// Draws the checkbox and reports the click: <paramref name="hitRect"/> is the 24px slot from
    /// <see cref="CheckboxSlot"/>, the visual is drawn inset 3 inside it. The hit goes through
    /// <see cref="UiNative.Button(Rect, UiWidgetContext)"/>, so an open popup keeps the click.
    /// <para>
    /// The caller must take this hit band OUT of any row button behind it: two overlapping IMGUI buttons
    /// both report the same click, and one press would toggle twice.
    /// </para>
    /// </summary>
    public static bool Checkbox(Rect hitRect, UiWidgetContext ctx, bool value)
    {
        CheckboxSurface(
            new Rect(hitRect.x + CheckboxInset, hitRect.y + CheckboxInset, CheckboxVisual, CheckboxVisual),
            ctx.Theme,
            value);
        return UiNative.Button(hitRect, ctx);
    }

    /// <summary>Draw-only checkbox, for read-only indicators (the camera indicator): no hit band, no click.</summary>
    public static void Checkbox(Rect rect, UiTheme theme, bool value)
    {
        CheckboxSurface(rect, theme, value);
    }

    private static void CheckboxSurface(Rect rect, UiTheme theme, bool value)
    {
        UiThemeDraw.Surface(rect, theme, theme.Raised, theme.Border);

        // Radius 2, expressed the only way this library can: the outline's four 2px corner cells are
        // repainted with the box's own plane, so the border stops short of each corner. A real rounded
        // corner needs a primitive the backend contact does not expose (UiThemeDraw has exactly one fill -
        // an axis-aligned rect - on purpose, so the contact stays gateable).
        float cut = Mathf.Min(CheckboxCornerCut, Mathf.Min(rect.width, rect.height) * 0.25f);
        UiThemeDraw.Solid(new Rect(rect.x, rect.y, cut, cut), theme.Raised);
        UiThemeDraw.Solid(new Rect(rect.xMax - cut, rect.y, cut, cut), theme.Raised);
        UiThemeDraw.Solid(new Rect(rect.x, rect.yMax - cut, cut, cut), theme.Raised);
        UiThemeDraw.Solid(new Rect(rect.xMax - cut, rect.yMax - cut, cut, cut), theme.Raised);

        if (!value) return;

        // "Checked" is a state, and states are carried by shape and ink, not by the accent: the spec's
        // checked box is an ink-solid square with a check mark in the border colour, and the accent would
        // put a fourth accent on every screen that has a checkbox. The lane pins that this fill is the ink
        // token and that no accent colour is painted with it.
        Rect ink = new Rect(rect.x + 3f, rect.y + 3f, Mathf.Max(1f, rect.width - 6f), Mathf.Max(1f, rect.height - 6f));
        UiThemeDraw.Surface(ink, theme, theme.TextPrimary, theme.TextPrimary);
        CheckMark(new Rect(ink.x + 1f, ink.y + 1f, Mathf.Max(1f, ink.width - 2f), Mathf.Max(1f, ink.height - 2f)), theme.Border);
    }

    /// <summary>The spec's border-coloured check mark. The library has no line primitive, so each stroke is
    /// a staircase of 2px cells - the same explicit-approximation口径 as <see cref="Hatch"/>: two straight
    /// strokes of different lengths at 45 degrees, not a curve.</summary>
    private static void CheckMark(Rect box, Color ink)
    {
        const float cell = 2f;
        float midX = box.x + box.width * 0.4f;
        Stroke(box.x, box.y + box.height * 0.5f, midX, box.yMax, cell, ink);
        Stroke(midX, box.yMax, box.xMax, box.y, cell, ink);
    }

    private static void Stroke(float x0, float y0, float x1, float y1, float cell, Color ink)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) / cell));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            UiThemeDraw.Solid(new Rect(x0 + dx * t, y0 + dy * t, cell, cell), ink);
        }
    }

    /// <summary>Spec 1.4 row system. The visual row follows the theme's density (24px regular / 20px dense,
    /// authored as RowHeight tokens in the manifest's &lt;Styles&gt;), the hit band is always 24px so a dense
    /// row stays a comfortable target, and a row ends in a 1px hairline. The numbers are read from
    /// <see cref="UiTheme.Geometry"/> - the library's density axis - never from a US constant, so one
    /// document moves the whole page.</summary>
    public const float RowMinHit = 24f;

    public static float RowVisualHeight(UiWidgetContext ctx) => ctx.Theme.Geometry.RowHeight;

    public static float RowHitHeight(UiWidgetContext ctx) => Mathf.Max(RowMinHit, RowVisualHeight(ctx));

    /// <summary>The hit band of a row: the row is hit at 24px even when its visual is 20px (dense), so the
    /// band is centred on the visual row and may extend past it.</summary>
    public static Rect RowHitRect(Rect row, UiWidgetContext ctx)
    {
        float hit = RowHitHeight(ctx);
        return new Rect(row.x, row.y + (row.height - hit) * 0.5f, row.width, hit);
    }

    /// <summary>The row's bottom rule: one hairline in the divider token, so a stack of rows reads as a
    /// list instead of a stack of boxes.</summary>
    public static void RowBottomLine(Rect row, UiTheme theme)
    {
        float hairline = Mathf.Max(1f, theme.Geometry.Hairline);
        UiThemeDraw.Solid(new Rect(row.x, row.yMax - hairline, row.width, hairline), theme.Divider);
    }

    /// <param name="singleLine">
    /// Forwarded to the fitting audit. True for surfaces that must render on one line (dropdown trigger
    /// and option rows), where the real failure mode is a too-narrow rect rather than a too-short band.
    /// </param>
    public static void Label(
        Rect rect,
        string text,
        UiTheme theme,
        Color? color = null,
        UiFont? font = null,
        TextAnchor anchor = TextAnchor.MiddleLeft,
        bool singleLine = false)
    {
        UiThemeDraw.Label(rect, text, theme, color ?? theme.TextPrimary, font ?? UiFont.Small, anchor, singleLine);
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
    public static void Label(Rect rect, string text, UiWidgetContext ctx, UiFont? font = null,
        TextAnchor anchor = TextAnchor.MiddleLeft, bool singleLine = false)
        => Label(rect, text, ctx.Theme, null, font, anchor, singleLine);

    public static void Label(Rect rect, string text, UiWidgetContext ctx, Color color, UiFont? font = null,
        TextAnchor anchor = TextAnchor.MiddleLeft, bool singleLine = false)
        => Label(rect, text, ctx.Theme, color, font, anchor, singleLine);

    /// <summary>
    /// The single outlet for claiming a control's help entry on hover (C+A model): while the pointer is
    /// over <paramref name="rect"/> the panel explains that entry; with no claim it falls back to the
    /// active section's overview. The claim is a per-pass transient owned by the session — FL P3's
    /// <c>UiSession.ClaimHover</c>/<c>HoverClaim</c> machine runs its frame boundary inside
    /// <c>DrawFrame</c> and holds a finished claim for <c>HoverGraceFrames</c> passes, so a control that
    /// stops being hovered stops being shown without any cleanup of its own, and a pointer crossing the
    /// gap between two neighbours never flashes the overview. Widgets must call this from <c>Draw</c>
    /// with the rect they actually drew (scroll-local space is fine; the group translation stays honest
    /// inside the pass because hover is read through <c>UiNative.IsMouseOver</c>).
    /// </summary>
    /// <returns>Whether the pointer is over the rect, so callers can reuse it for row highlighting.</returns>
    public static bool HelpHover(Rect rect, UiWidgetContext ctx, string itemKey)
    {
        bool hovered = UiNative.IsMouseOver(rect);
        if (hovered)
        {
            ctx.Session.ClaimHover(itemKey);
        }
        return hovered;
    }

    /// <summary>
    /// Side inset of a selection button's label from its own rect. A caller that sizes a band for this
    /// label must measure at <c>rect.width - SelectionButtonLabelInset * 2</c>, not at the rect width:
    /// measuring the outer rect lets a label wrap at draw time inside a band sized for one line.
    /// </summary>
    public const float SelectionButtonLabelInset = 6f;

    /// <summary>
    /// Draws a selection-style button surface and returns whether it was clicked. The click goes through
    /// <see cref="UiNative.Button(Rect, UiWidgetContext)"/>, so a control covered by an open popup yields
    /// the click to the popup instead of stealing it (FL api-tiers.md: the context-free overload is
    /// outside the hit stack). Callers must hold a context; there is no reason to hand-roll the hit.
    /// </summary>
    public static bool SelectionButton(Rect rect, UiWidgetContext ctx, string label, UiTheme theme, bool selected, bool danger = false, UiFont? font = null)
    {
        RowSurface(rect, theme, UiNative.IsMouseOver(rect), selected ? RowRail.Selected : RowRail.None, danger);
        Label(
            new Rect(rect.x + SelectionButtonLabelInset, rect.y, Mathf.Max(1f, rect.width - SelectionButtonLabelInset * 2f), rect.height),
            label,
            theme,
            danger ? theme.TextOnDanger : selected ? theme.TextOnGold : theme.TextPrimary,
            font ?? UiFont.Tiny,
            TextAnchor.MiddleLeft);
        return UiNative.Button(rect, ctx);
    }

    /// <summary>
    /// Self-drawn dropdown trigger + session popup used by composites for dynamic option lists.
    /// Options are (display, value) pairs; selecting invokes <paramref name="onSelected"/>.
    /// </summary>
    public static void Dropdown(
        Rect rect,
        string elementId,
        UiWidgetContext ctx,
        string current,
        IReadOnlyList<KeyValuePair<string, string>> options,
        Action<string> onSelected)
    {
        if (options.Count == 0) return;

        string display = current;
        foreach (KeyValuePair<string, string> option in options)
        {
            if (string.Equals(option.Value, current, StringComparison.Ordinal)
                || string.Equals(option.Key, current, StringComparison.Ordinal))
            {
                display = option.Key;
                break;
            }
        }

        // Accent discipline (05 §3.1): a filled trigger means "this field carries a value", which is
        // not one of the accent's three uses. Openness stays a plane change (Selected fill with the
        // stronger neutral border), never a hue change; the text is always TextPrimary.
        bool open = ctx.Session.IsPopupOpen(elementId);
        UiThemeDraw.Surface(
            rect,
            ctx.Theme,
            open ? ctx.Theme.Selected : ctx.Theme.Raised,
            open ? ctx.Theme.BorderStrong : ctx.Theme.Border);
        // D9 (2026-09-06 in-game): the trigger column is a fixed width, so a selected label that
        // outgrows it (long author credits, pack names) clipped. The trigger ellipsizes through the
        // same metrics seam the fit audit measures with, so the cut is deliberate, not silent.
        float triggerTextWidth = Mathf.Max(1f, rect.width - 12f);
        string triggerDisplay = EllipsizeToFit(display, triggerTextWidth, ctx.Metrics, UiFont.Tiny);
        Label(
            new Rect(rect.x + 6f, rect.y, triggerTextWidth, rect.height),
            triggerDisplay,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft,
            singleLine: true);

        // The trigger click stores the popup anchor in Host window space (the engine translates
        // draw rects inside scrolls/groups); the popup pass draws and hit-tests in that same
        // stored space, so both share one anchor source.
        UiNative.DropdownButton(rect, elementId, ctx);

        if (ctx.Session.IsPopupOpen(elementId))
        {
            Rect? anchor = ctx.Session.OpenPopupAnchor;
            if (anchor.HasValue)
            {
                // D9: the popup grows to the widest option label instead of inheriting the
                // trigger's narrow column (UiPopup draws each row single-line Small with 6px side
                // padding). Capped at the viewport width - RectFor still flips vertically and
                // clamps x, and a label that survives the cap keeps overflowing loudly into the
                // audit, which is honest: the window itself is too narrow.
                float popupWidth = anchor.Value.width;
                foreach (KeyValuePair<string, string> option in options)
                {
                    float needed = ctx.Metrics.MeasureWidth(option.Key, UiFont.Small) + 12f;
                    if (needed > popupWidth) popupWidth = needed;
                }
                float viewportWidth = ctx.Session.HostViewport.width;
                if (viewportWidth > 0f && popupWidth > viewportWidth) popupWidth = viewportWidth;
                Rect popupAnchor = new Rect(anchor.Value.x, anchor.Value.y, popupWidth, anchor.Value.height);

                // The popup pass draws after content and outside the layout engine's element scope, so a
                // finding inside a popup row would be reported as "(unscoped)" - the exact reason the
                // first in-game overflow was not actionable. Claim the owning element's path for the
                // duration of the rows so every report from this draw carries a stable identity.
                string capturedCurrent = current;
                string popupPath = (ctx.ElementPath ?? "") + "/popup";
                ctx.Session.RegisterPopupDraw(() =>
                {
                    UiFitAudit.BeginElement(popupPath);
                    try
                    {
                        UiPopup.DrawOptionList(
                            UiPopup.RectFor(popupAnchor, options.Count, ctx.Session.HostViewport),
                            elementId,
                            ctx,
                            options,
                            capturedCurrent,
                            onSelected);
                    }
                    finally
                    {
                        UiFitAudit.EndElement();
                    }
                });
            }
        }
    }

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

    /// <summary>Card frame with a title header; returns the inner body rect.</summary>
    public static Rect DrawCard(Rect rect, string title, UiTheme theme)
    {
        UiThemeDraw.Panel(rect, theme);
        Label(
            new Rect(rect.x + UsCardLayout.Padding, rect.y + 2f, Mathf.Max(1f, rect.width - UsCardLayout.Padding * 2f), 22f),
            title,
            theme,
            theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UiThemeDraw.Surface(
            new Rect(rect.x + UsCardLayout.Padding, rect.y + UsCardLayout.HeaderHeight - 2f, Mathf.Max(1f, rect.width - UsCardLayout.Padding * 2f), 1f),
            theme,
            theme.Divider,
            theme.Divider);
        return new Rect(
            rect.x + UsCardLayout.Padding,
            rect.y + UsCardLayout.HeaderHeight + UsCardLayout.HeaderGap,
            Mathf.Max(1f, rect.width - UsCardLayout.Padding * 2f),
            Mathf.Max(1f, rect.height - UsCardLayout.HeaderHeight - UsCardLayout.HeaderGap - UsCardLayout.Padding * 2f));
    }

    /// <summary>Border drawn around the card the help panel is currently explaining (follows the live
    /// hover claim). Neutral BorderStrong rather than the accent: the help claim is not one of the
    /// accent's three uses (05 §3.1).</summary>
    public static void DrawHelpFocusBorder(Rect rect, UiTheme theme, bool focused)
    {
        if (!focused) return;
        UiThemeDraw.Surface(rect, theme, Color.clear, theme.BorderStrong);
    }
}
