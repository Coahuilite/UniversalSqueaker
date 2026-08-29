using System;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// Modern inline help "?" button. Emits a neutral <c>ToggleHelp</c> command with the help key as
/// payload; the page owns toggling <see cref="UiPageState.OpenHelpKeys"/>.
/// </summary>
public static class UsHelpButton
{
    public static void Draw(Rect rect, string helpKey, UiPageState state, Action<KitUiCommand> emit)
    {
        if (string.IsNullOrEmpty(helpKey) || UsHelpCatalog.Get(helpKey) == null)
        {
            return;
        }

        bool active = state.OpenHelpKeys.Contains(helpKey);
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawSurface(rect,
            active ? UsSurface.SurfaceKind.Selected
            : hovered ? UsSurface.SurfaceKind.Hover
            : UsSurface.SurfaceKind.Panel);
        UsSurface.DrawBorder(rect, active ? UsVisualTokens.AccentGold : hovered ? UsVisualTokens.BorderStrong : UsVisualTokens.Border);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = active ? UsVisualTokens.AccentGold : hovered ? UsVisualTokens.TextPrimary : UsVisualTokens.TextSecondary;
        Widgets.Label(rect, "?");
        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;

        UiInteract.Button(rect, UiLayer.TopAction, () => emit?.Invoke(new KitUiCommand("ToggleHelp", helpKey)));
    }
}
