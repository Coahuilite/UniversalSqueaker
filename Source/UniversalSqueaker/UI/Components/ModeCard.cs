using System;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>One selectable mode card (Off / Fallback / Remix). Stateless; emits SetMode on click.</summary>
public static class ModeCard
{
    public static void Draw(Rect rect, SqueakVoicePackMode current, SqueakVoicePackMode target,
        string title, string description, Action<UiCommand> emit)
    {
        bool selected = current == target;
        bool hovered = Mouse.IsOver(rect);
        Color fill = selected ? UiPalette.Selected : hovered ? new Color(.16f, .145f, .12f, .94f) : UiPalette.Panel;
        Widgets.DrawBoxSolid(rect, fill);
        SectionFrame.DrawBorder(rect);
        if (selected)
            Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.yMax - 4f, rect.width - 2f, 3f), UiPalette.Gold);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = selected ? new Color(1f, .86f, .58f) : Color.white;
        Rect titleRect = new(rect.x + 10f, rect.y + 7f, Math.Max(1f, rect.width - 20f), 24f);
        Widgets.Label(titleRect, title);
        Text.Font = GameFont.Tiny;
        GUI.color = new Color(.82f, .80f, .74f, .92f);
        Rect descRect = new(rect.x + 10f, rect.y + 31f, Math.Max(1f, rect.width - 20f), Math.Max(1f, rect.height - 38f));
        Widgets.Label(descRect, description);
        Text.Font = oldFont;
        GUI.color = oldColor;

        if (Widgets.ButtonInvisible(rect))
        {
            emit?.Invoke(new UiCommand(UiCommandKind.SetMode, mode: target));
        }
    }
}
