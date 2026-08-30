using System;
using FerriteLib.UiKit;
using UnityEngine;

namespace UniversalSqueaker.UI;

/// <summary>
/// Shared modern card chrome for US settings sections.
///
/// A card is a framed surface with a header row (title + inline help), optional help banner,
/// and a content body. Widgets use <see cref="Measure"/> in their Measure pass and
/// <see cref="Draw"/> in their Draw pass so every section shares the same padding, border,
/// header and help behavior.
/// </summary>
public static class UsCard
{
    public const float Padding = 12f;
    public const float HeaderHeight = 26f;
    public const float HeaderGap = 6f;
    public const float HelpButtonSize = 22f;

    public static float Measure(float bodyHeight, string helpKey, WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        float height = Padding + HeaderHeight + HeaderGap + Math.Max(0f, bodyHeight) + Padding;
        if (UsHelp.IsOpen(ctx, helpKey))
        {
            float innerWidth = Math.Max(1f, ctx.ViewWidth - Padding * 2f);
            height += UsHelp.BannerHeight(ctx, helpKey, innerWidth) + HeaderGap;
        }

        return height;
    }

    public static void Draw(
        Rect rect,
        string title,
        string helpKey,
        WidgetContext ctx,
        Action<FerriteLib.UiKit.UiCommand> emit,
        Action<Rect> drawBody)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (drawBody == null) throw new ArgumentNullException(nameof(drawBody));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UsSurface.DrawSurface(rect, UsSurface.SurfaceKind.Panel);

        float x = rect.x + Padding;
        float y = rect.y + Padding;

        Rect headerRect = new(x, y, Math.Max(1f, rect.width - Padding * 2f), HeaderHeight);
        DrawHeader(headerRect, title, helpKey, ctx, emit);
        y += HeaderHeight + HeaderGap;

        if (UsHelp.IsOpen(ctx, helpKey))
        {
            float helpHeight = UsHelp.BannerHeight(ctx, helpKey, headerRect.width);
            if (helpHeight > 0f)
            {
                UsHelp.DrawBanner(new Rect(x, y, headerRect.width, helpHeight), helpKey, ctx);
                y += helpHeight + HeaderGap;
            }
        }

        Rect bodyRect = new(
            x,
            y,
            Math.Max(1f, rect.width - Padding * 2f),
            Math.Max(1f, rect.yMax - Padding - y));
        drawBody(bodyRect);
    }

    private static void DrawHeader(
        Rect rect,
        string title,
        string helpKey,
        WidgetContext ctx,
        Action<FerriteLib.UiKit.UiCommand> emit)
    {
        UiText.DrawLabel(rect, title);
        if (!string.IsNullOrEmpty(helpKey))
        {
            Rect helpRect = new(rect.xMax - HelpButtonSize, rect.y + (rect.height - HelpButtonSize) * 0.5f, HelpButtonSize, HelpButtonSize);
            UsHelp.DrawHelpButton(helpRect, helpKey, ctx, emit);
        }
        UiPanel.DrawDivider(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f));
    }
}
