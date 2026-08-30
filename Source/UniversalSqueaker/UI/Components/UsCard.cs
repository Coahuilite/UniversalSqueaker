using System;
using FerriteLib.UiKit;
using UnityEngine;

namespace UniversalSqueaker.UI;

/// <summary>
/// Shared modern card chrome for US settings sections.
///
/// A card is a framed surface with a header row (title) and a content body. Widgets use
/// <see cref="Measure"/> in their Measure pass and <see cref="Draw"/> in their Draw pass so every
/// section shares the same padding, border and header behavior.
/// </summary>
public static class UsCard
{
    public const float Padding = UsCardLayout.Padding;
    public const float HeaderHeight = UsCardLayout.HeaderHeight;
    public const float HeaderGap = UsCardLayout.HeaderGap;

    public static float Measure(float bodyHeight, WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        return Measure(bodyHeight, ctx, titleHidden: false);
    }

    /// <summary>
    /// Measures a card. When <paramref name="titleHidden"/> is true the header row and its gap
    /// are removed so Measure matches <see cref="Draw(Rect,string,WidgetContext,Action{Rect},bool)"/>.
    /// </summary>
    public static float Measure(float bodyHeight, WidgetContext ctx, bool titleHidden)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        return UsCardLayout.MeasureBody(bodyHeight, titleHidden);
    }

    public static void Draw(
        Rect rect,
        string title,
        WidgetContext ctx,
        Action<Rect> drawBody)
    {
        Draw(rect, title, ctx, drawBody, titleHidden: false);
    }

    /// <summary>
    /// Draws a card. When <paramref name="titleHidden"/> is true the header row and its gap are
    /// skipped; the body starts immediately below the top padding.
    /// </summary>
    public static void Draw(
        Rect rect,
        string title,
        WidgetContext ctx,
        Action<Rect> drawBody,
        bool titleHidden)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (drawBody == null) throw new ArgumentNullException(nameof(drawBody));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UsSurface.DrawSurface(rect, UsSurface.SurfaceKind.Panel);

        float x = rect.x + Padding;
        float y = rect.y + Padding;

        if (!titleHidden)
        {
            Rect headerRect = new(x, y, Math.Max(1f, rect.width - Padding * 2f), HeaderHeight);
            DrawHeader(headerRect, title);
            y += HeaderHeight + HeaderGap;
        }

        Rect bodyRect = new(
            x,
            y,
            Math.Max(1f, rect.width - Padding * 2f),
            Math.Max(1f, rect.yMax - Padding - y));
        drawBody(bodyRect);
    }

    /// <summary>Parses a boolean XML-style attribute used by US widgets.</summary>
    public static bool IsTrueAttribute(UiElementSpec? spec, string name)
    {
        if (spec == null || string.IsNullOrEmpty(name)) return false;
        return spec.TryGetAttribute(name, out string value)
            && (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.Ordinal));
    }

    private static void DrawHeader(Rect rect, string title)
    {
        UiText.DrawLabel(rect, title);
        UiPanel.DrawDivider(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f));
    }
}
