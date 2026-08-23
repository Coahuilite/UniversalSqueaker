using System;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>One VoicePack row in a checklist. Stateless; emits TogglePack when clicked.</summary>
public static class VoicePackRow
{
    public static void Draw(Rect rect, VoicePackRowView row, SqueakVoicePackScope scope,
        string raceDefName, string targetDefName, Action<UiCommand> emit)
    {
        Widgets.DrawBoxSolid(rect, Mouse.IsOver(rect) ? UiPalette.Raised : UiPalette.Panel);
        SectionFrame.DrawBorder(rect);

        Rect switchRect = new(rect.x + 6f, rect.y + (rect.height - 18f) * .5f, 34f, 18f);
        Widgets.DrawBoxSolid(switchRect, row.IsSelected ? new Color(.31f, .245f, .135f, .98f) : new Color(.12f, .115f, .105f, .98f));
        SectionFrame.DrawBorder(switchRect);
        float knobX = row.IsSelected ? switchRect.xMax - 16f : switchRect.x + 2f;
        Widgets.DrawBoxSolid(new Rect(knobX, switchRect.y + 2f, 14f, 14f),
            row.IsSelected ? UiPalette.Gold : new Color(.54f, .52f, .48f));

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = row.IsSelected ? new Color(1f, .86f, .58f) : Color.white;
        Rect primaryRect = new(rect.x + 50f, rect.y + 3f, Math.Max(1f, rect.width - 58f), 25f);
        Widgets.Label(primaryRect, row.Label);
        Text.Font = GameFont.Tiny;
        GUI.color = UiPalette.Muted;
        Rect secondaryRect = new(rect.x + 50f, rect.y + 27f, Math.Max(1f, rect.width - 58f), 20f);
        Widgets.Label(secondaryRect, row.ModName + " · " + row.Author);
        Rect coverageRect = new(rect.x + 50f, rect.y + 48f, Math.Max(1f, rect.width - 58f), 20f);
        Widgets.Label(coverageRect, row.Coverage);
        Text.Font = oldFont;
        GUI.color = oldColor;

        if (Widgets.ButtonInvisible(rect))
        {
            emit?.Invoke(new UiCommand(
                UiCommandKind.TogglePack,
                scope: scope,
                raceDefName: raceDefName,
                targetDefName: targetDefName,
                arg: row.Key,
                flag: !row.IsSelected));
        }
    }
}
