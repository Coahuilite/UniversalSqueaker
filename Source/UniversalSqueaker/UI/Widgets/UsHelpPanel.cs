using System;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Right-hand help panel in the style of modern RimWorld settings mods (Camera+ pattern).
/// Shows the current section's title, an Overview row, every item in the section, and the resolved
/// overview/hover/selection content. Hovering an item updates <see cref="UiPageState.HelpHoverKey"/>;
/// clicking an item pins <see cref="UiPageState.HelpSelectionKey"/>; clicking Overview clears it.
/// The body uses <see cref="UiPageState.HelpScrollPosition"/> so long text and long item lists scroll.
/// </summary>
public static class UsHelpPanel
{
    private const float Padding = 8f;
    private const float TitleHeight = 20f;
    private const float TitleGap = 6f;
    private const float ItemHeight = 20f;
    private const float ItemGap = 2f;
    private const float ContentGap = 6f;
    private const float ContentLabelHeight = 18f;

    public static void Draw(Rect rect, string helpKey, WidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UsSurface.DrawSurface(rect, UsSurface.SurfaceKind.Panel);

        float x = rect.x + Padding;
        float y = rect.y + Padding;

        UsHelpCatalog.TryGetSection(helpKey, out HelpSection section);
        UsHelpPanelLogic.HelpPanelDisplay display = UsHelpPanelLogic.Resolve(section, ctx.State.HelpHoverKey, ctx.State.HelpSelectionKey);

        UiText.DrawLabel(new Rect(x, y, Math.Max(1f, rect.width - Padding * 2f), TitleHeight), display.Title);
        UiPanel.DrawDivider(new Rect(x, y + TitleHeight, Math.Max(1f, rect.width - Padding * 2f), 1f));
        y += TitleHeight + TitleGap;

        float textWidth = Math.Max(1f, rect.width - Padding * 2f);
        float listHeight = ItemHeight + (section != null && section.Items.Count > 0 ? ItemHeight + ItemGap * (section.Items.Count - 1) + ItemGap : 0f);
        if (section == null)
        {
            listHeight = ItemHeight;
        }

        float textHeight = Math.Max(1f, ctx.Metrics.MeasureText(display.Text, UiFont.Tiny, textWidth));
        float contentHeight = listHeight + ContentGap + ContentLabelHeight + 2f + textHeight + 4f;

        Rect bodyRect = new(
            x,
            y,
            textWidth,
            Math.Max(1f, rect.yMax - Padding - y));
        Rect contentRect = new(0f, 0f, textWidth, contentHeight);

        Widgets.BeginScrollView(bodyRect, ref ctx.State.HelpScrollPosition, contentRect);
        UiInteract.PushScrollView(bodyRect, ctx.State.HelpScrollPosition);
        try
        {
            float contentY = 0f;

            DrawItemRow(
                new Rect(0f, contentY, textWidth, ItemHeight),
                UsHelpPanelLogic.OverviewLabel,
                selected: display.IsOverview,
                itemKey: null,
                ctx);
            contentY += ItemHeight;

            if (section != null)
            {
                foreach (HelpItem item in section.Items)
                {
                    contentY += ItemGap;
                    DrawItemRow(
                        new Rect(0f, contentY, textWidth, ItemHeight),
                        item.Label,
                        selected: !display.IsOverview && string.Equals(display.ItemKey, item.Key, StringComparison.Ordinal),
                        itemKey: item.Key,
                        ctx);
                    contentY += ItemHeight;
                }
            }

            contentY += ContentGap;

            Color oldColor = GUI.color;
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = UsVisualTokens.TextPrimary;
                Widgets.Label(new Rect(0f, contentY, textWidth, ContentLabelHeight), display.Label);

                contentY += ContentLabelHeight + 2f;
                Text.Font = GameFont.Tiny;
                GUI.color = UsVisualTokens.TextSecondary;
                Widgets.Label(new Rect(0f, contentY, textWidth, textHeight), display.Text);
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
            UiInteract.PopScrollView();
            Widgets.EndScrollView();
        }
    }

    private static void DrawItemRow(Rect rect, string label, bool selected, string? itemKey, WidgetContext ctx)
    {
        bool hovered = Mouse.IsOver(rect);
        if (itemKey == null)
        {
            if (hovered)
            {
                ctx.State.HelpHoverKey = "";
            }
        }
        else if (hovered)
        {
            ctx.State.HelpHoverKey = itemKey;
        }

        SelectionButton.Draw(rect, label, selected, font: UiFont.Tiny, hovered: hovered);
        UsHelpHighlight.Draw(rect, itemKey ?? "", ctx.State);

        UiInteract.Button(rect, UiLayer.Content, () =>
        {
            ctx.State.HelpSelectionKey = itemKey ?? "";
        });
    }
}
