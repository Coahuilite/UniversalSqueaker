using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using VerseWidgets = Verse.Widgets;

namespace UniversalSqueaker.UI;

/// <summary>
/// Visual helpers for kernel-owned US composite widgets. These are drawing helpers only; input
/// authority stays with native IMGUI controls through <see cref="UiNative"/>. All global GUI state
/// (color/font/anchor) is restored before returning.
/// </summary>
public static class UsKernelDraw
{
    public const float RowLeftPadding = 10f;
    private const float DropdownOptionHeight = 24f;

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

    public static void Label(Rect rect, string text, UiTheme theme, Color? color = null, UiFont? font = null, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        UiThemeDraw.Label(rect, text, theme, color ?? theme.TextPrimary, font ?? UiFont.Small, anchor);
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
            TextAnchor.MiddleLeft);

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
                ctx.Session.RegisterPopupDraw(() => DrawDropdownPopup(anchor.Value, elementId, options, capturedCurrent, ctx, onSelected));
            }
        }
    }

    private static void DrawDropdownPopup(
        Rect anchor,
        string elementId,
        IReadOnlyList<KeyValuePair<string, string>> options,
        string current,
        UiWidgetContext ctx,
        Action<string> onSelected)
    {
        Rect popupRect = new(anchor.x, anchor.yMax, anchor.width, options.Count * DropdownOptionHeight);
        UiThemeDraw.Panel(popupRect, ctx.Theme);

        for (int i = 0; i < options.Count; i++)
        {
            Rect rowRect = new(popupRect.x, popupRect.y + i * DropdownOptionHeight, popupRect.width, DropdownOptionHeight);
            bool selected = string.Equals(options[i].Value, current, StringComparison.Ordinal);
            UiThemeDraw.Surface(
                rowRect,
                ctx.Theme,
                selected ? ctx.Theme.Selected : ctx.Theme.Raised,
                selected ? ctx.Theme.AccentGold : ctx.Theme.Border);
            Label(
                new Rect(rowRect.x + 6f, rowRect.y, rowRect.width - 12f, rowRect.height),
                options[i].Key,
                ctx.Theme,
                selected ? ctx.Theme.TextOnGold : ctx.Theme.TextPrimary,
                UiFont.Small,
                TextAnchor.MiddleLeft);

            if (UiNative.DropdownOptionRow(rowRect, elementId, ctx.Session))
            {
                ctx.Session.ClosePopup();
                onSelected(options[i].Value);
            }
        }
    }

    /// <summary>Card frame with a title header; returns the inner body rect.</summary>
    public static Rect DrawCard(Rect rect, string title, UiTheme theme)
    {
        UiThemeDraw.Panel(rect, theme);
        Label(
            new Rect(rect.x + UsCardLayout.Padding, rect.y + 4f, Mathf.Max(1f, rect.width - UsCardLayout.Padding * 2f), 18f),
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
