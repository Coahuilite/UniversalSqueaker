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
    public const float Padding = 12f;
    public const float HeaderHeight = 26f;
    public const float HeaderGap = 6f;

    public static float Measure(float bodyHeight, WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        return Padding + HeaderHeight + HeaderGap + Math.Max(0f, bodyHeight) + Padding;
    }

    public static void Draw(
        Rect rect,
        string title,
        WidgetContext ctx,
        Action<Rect> drawBody)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (drawBody == null) throw new ArgumentNullException(nameof(drawBody));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UsSurface.DrawSurface(rect, UsSurface.SurfaceKind.Panel);

        float x = rect.x + Padding;
        float y = rect.y + Padding;

        Rect headerRect = new(x, y, Math.Max(1f, rect.width - Padding * 2f), HeaderHeight);
        DrawHeader(headerRect, title);
        y += HeaderHeight + HeaderGap;

        Rect bodyRect = new(
            x,
            y,
            Math.Max(1f, rect.width - Padding * 2f),
            Math.Max(1f, rect.yMax - Padding - y));
        drawBody(bodyRect);
    }

    private static void DrawHeader(Rect rect, string title)
    {
        UiText.DrawLabel(rect, title);
        UiPanel.DrawDivider(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f));
    }
}
