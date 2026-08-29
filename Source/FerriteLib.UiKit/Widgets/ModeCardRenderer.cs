using System;
using UnityEngine;
using Verse;
using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit.Widgets;

/// <summary>Shared visual renderer for selectable mode cards.</summary>
internal static class ModeCardRenderer
{
    public static void Draw(Rect rect, bool selected, string title, string description, Action? onClick)
    {
        bool hovered = Mouse.IsOver(rect);
        Color fill = selected ? Palette.Selected : hovered ? Palette.Hover : Palette.Panel;
        VerseWidgets.DrawBoxSolid(rect, fill);
        SurfaceFrame.DrawBorder(rect, selected ? Palette.BorderStrong : Palette.Border);

        if (selected)
        {
            VerseWidgets.DrawBoxSolid(
                new Rect(rect.x + 1f, rect.yMax - 4f, Math.Max(1f, rect.width - 2f), 3f),
                Palette.AccentGold);
        }

        UiKitGui.Label(
            new Rect(rect.x + 10f, rect.y + 7f, Math.Max(1f, rect.width - 20f), 24f),
            title,
            UiFont.Small,
            TextAnchor.UpperLeft,
            selected ? Palette.TextOnGold : Palette.TextPrimary);

        UiKitGui.Label(
            new Rect(rect.x + 10f, rect.y + 31f, Math.Max(1f, rect.width - 20f), Math.Max(1f, rect.height - 38f)),
            description,
            UiFont.Tiny,
            TextAnchor.UpperLeft,
            Palette.TextSecondary);

        if (VerseWidgets.ButtonInvisible(rect))
            onClick?.Invoke();
    }
}
