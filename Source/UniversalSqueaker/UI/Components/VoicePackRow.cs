using System;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>One VoicePack row in a checklist. Stateless; emits TogglePack when clicked.</summary>
public static class VoicePackRow
{
    public static void Draw(Rect rect, VoicePackRowView row, SqueakVoicePackScope scope,
        string raceDefName, string targetDefName, Action<UiCommand> emit)
    {
        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, row, scope, raceDefName, targetDefName, emit),
            fallback => DrawVanilla(fallback, row, scope, raceDefName, targetDefName, emit),
            "us/voice-pack-row");
    }

    private static void DrawCore(Rect rect, VoicePackRowView row, SqueakVoicePackScope scope,
        string raceDefName, string targetDefName, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);

        Rect checkRect = new(rect.x + 10f, rect.y + (rect.height - 18f) * .5f, 18f, 18f);
        UsSurface.DrawCheckbox(checkRect, row.IsSelected);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = row.IsSelected ? UsVisualTokens.TextOnGold : UsVisualTokens.TextPrimary;
        Rect primaryRect = new(rect.x + 36f, rect.y + 3f, Math.Max(1f, rect.width - 44f), 25f);
        Widgets.Label(primaryRect, row.Label);
        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextSecondary;
        Rect secondaryRect = new(rect.x + 36f, rect.y + 27f, Math.Max(1f, rect.width - 44f), 20f);
        Widgets.Label(secondaryRect, row.ModName + " · " + row.Author);
        if (VoicePacksLayout.ForWidth(rect.width) == LayoutTier.Comfortable)
        {
            Rect coverageRect = new(rect.x + 36f, rect.y + 48f, Math.Max(1f, rect.width - 44f), 20f);
            Widgets.Label(coverageRect, row.Coverage);
        }
        Text.Font = oldFont;
        GUI.color = oldColor;

        UiInteract.Row(rect, () => emit?.Invoke(new UiCommand(
            UiCommandKind.TogglePack,
            scope: scope,
            raceDefName: raceDefName,
            targetDefName: targetDefName,
            arg: row.Key,
            flag: !row.IsSelected)));
    }

    private static void DrawVanilla(Rect rect, VoicePackRowView row, SqueakVoicePackScope scope,
        string raceDefName, string targetDefName, Action<UiCommand> emit)
    {
        bool checkboxValue = row.IsSelected;
        Widgets.Checkbox(new Vector2(rect.x + 6f, rect.y + (rect.height - 18f) * .5f), ref checkboxValue, 18f);
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Widgets.Label(new Rect(rect.x + 30f, rect.y + 3f, Math.Max(1f, rect.width - 36f), 20f), row.Label);
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Widgets.Label(new Rect(rect.x + 30f, rect.y + 24f, Math.Max(1f, rect.width - 36f), 16f), row.ModName + " · " + row.Author);
        Widgets.Label(new Rect(rect.x + 30f, rect.y + 42f, Math.Max(1f, rect.width - 36f), 16f), row.Coverage);
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
