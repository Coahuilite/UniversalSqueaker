using System;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Right-hand help panel in the style of modern RimWorld settings mods (Camera+ pattern).
/// Shows the help text for the currently selected section, or a neutral hint when nothing is
/// selected. Pure visual chrome; it does not own any state beyond the shared help scroll
/// position on <see cref="UiPageState"/>.
/// </summary>
public static class UsHelpPanel
{
    private const string DefaultTitle = "Help";
    private const string EmptyText = "Select a section to see its help here.";

    private const float Padding = 8f;
    private const float TitleHeight = 20f;
    private const float TitleGap = 6f;

    public static void Draw(Rect rect, string helpKey, WidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UsSurface.DrawSurface(rect, UsSurface.SurfaceKind.Panel);

        float x = rect.x + Padding;
        float y = rect.y + Padding;

        UiText.DrawLabel(new Rect(x, y, Math.Max(1f, rect.width - Padding * 2f), TitleHeight), DefaultTitle);
        UiPanel.DrawDivider(new Rect(x, y + TitleHeight, Math.Max(1f, rect.width - Padding * 2f), 1f));
        y += TitleHeight + TitleGap;

        string? text = UsHelpCatalog.Get(helpKey);
        string resolvedText = string.IsNullOrEmpty(text) ? EmptyText : text!;

        float textWidth = Math.Max(1f, rect.width - Padding * 2f);
        float textHeight = Math.Max(1f, ctx.Metrics.MeasureText(resolvedText, UiFont.Tiny, textWidth));
        Rect bodyRect = new(
            x,
            y,
            textWidth,
            Math.Max(1f, rect.yMax - Padding - y));
        Rect contentRect = new(0f, 0f, textWidth, textHeight);

        Widgets.BeginScrollView(bodyRect, ref ctx.State.HelpScrollPosition, contentRect);
        try
        {
            Color oldColor = GUI.color;
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = UsVisualTokens.TextSecondary;
                Widgets.Label(contentRect, resolvedText);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }
        finally
        {
            Widgets.EndScrollView();
        }
    }
}
