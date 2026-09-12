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
            UiThemeDraw.Surface(
                new Rect(rect.x + 3f, rect.y + 3f, Mathf.Max(1f, rect.width - 6f), Mathf.Max(1f, rect.height - 6f)),
                theme,
                theme.AccentGold,
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
    /// Draws a selection-style button surface and returns whether it was clicked. The click goes through
    /// <see cref="UiNative.Button(Rect, UiWidgetContext)"/>, so a control covered by an open popup yields
    /// the click to the popup instead of stealing it (FL api-tiers.md: the context-free overload is
    /// outside the hit stack). Callers must hold a context; there is no reason to hand-roll the hit.
    /// </summary>
    public static bool SelectionButton(Rect rect, UiWidgetContext ctx, string label, UiTheme theme, bool selected, bool danger = false, UiFont? font = null)
    {
        RowSurface(rect, theme, UiNative.IsMouseOver(rect), selected, danger);
        Label(
            new Rect(rect.x + 6f, rect.y, Mathf.Max(1f, rect.width - 12f), rect.height),
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
