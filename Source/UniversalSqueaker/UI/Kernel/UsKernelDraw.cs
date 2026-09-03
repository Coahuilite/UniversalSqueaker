using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;
using VerseWidgets = Verse.Widgets;

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

    public static void RowSurface(Rect rect, UiTheme theme, bool hovered, bool selected, bool danger = false)
    {
        Color fill = danger ? theme.Warning
            : selected ? theme.Selected
            : hovered ? theme.Hover
            : theme.Raised;
        Color border = danger ? theme.Danger
            : selected ? theme.AccentGold
            : theme.Border;
        UiThemeDraw.Surface(rect, theme, fill, border);
    }

    public static void Checkbox(Rect rect, UiTheme theme, bool value)
    {
        UiThemeDraw.Surface(rect, theme, theme.Raised, theme.Border);
        if (value)
        {
            VerseWidgets.DrawBoxSolid(
                new Rect(rect.x + 3f, rect.y + 3f, Mathf.Max(1f, rect.width - 6f), Mathf.Max(1f, rect.height - 6f)),
                theme.AccentGold);
        }
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

    /// <summary>Draws a selection-style button surface and returns whether it was clicked (native invisible button).</summary>
    public static bool SelectionButton(Rect rect, string label, UiTheme theme, bool selected, bool danger = false, UiFont? font = null)
    {
        RowSurface(rect, theme, Mouse.IsOver(rect), selected, danger);
        Label(
            new Rect(rect.x + 6f, rect.y, Mathf.Max(1f, rect.width - 12f), rect.height),
            label,
            theme,
            danger ? theme.TextOnDanger : selected ? theme.TextOnGold : theme.TextPrimary,
            font ?? UiFont.Tiny,
            TextAnchor.MiddleLeft);
        return UiNative.Button(rect);
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

        bool open = ctx.Session.IsPopupOpen(elementId);
        UiThemeDraw.Surface(
            rect,
            ctx.Theme,
            open ? ctx.Theme.Selected : ctx.Theme.Raised,
            open || current.Length > 0 ? ctx.Theme.AccentGold : ctx.Theme.Border);
        Label(
            new Rect(rect.x + 6f, rect.y, Mathf.Max(1f, rect.width - 12f), rect.height),
            display,
            ctx.Theme,
            current.Length > 0 ? ctx.Theme.TextOnGold : ctx.Theme.TextPrimary,
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
                string capturedCurrent = current;
                ctx.Session.RegisterPopupDraw(() => UiPopup.DrawOptionList(
                    UiPopup.RectFor(anchor.Value, options.Count, ctx.Session.HostViewport),
                    elementId,
                    ctx,
                    options,
                    capturedCurrent,
                    onSelected));
            }
        }
    }

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

    /// <summary>Accent border drawn around the whole card when the help selection points at this section.</summary>
    public static void DrawHelpSelectionBorder(Rect rect, UiTheme theme, bool selected)
    {
        if (!selected) return;
        UiThemeDraw.Surface(rect, theme, Color.clear, theme.AccentGold);
    }
}
