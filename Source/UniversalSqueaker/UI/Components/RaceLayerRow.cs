using System;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>One race row in the Race layer. Stateless; emits SelectDomain when clicked.</summary>
public static class RaceLayerRow
{
    public static void Draw(Rect rect, RaceLayerRowView row, bool selected, Action<UiCommand> emit)
    {
        Widgets.DrawBoxSolid(rect, selected ? UiPalette.Selected : Mouse.IsOver(rect) ? UiPalette.Raised : UiPalette.Panel);
        SectionFrame.DrawBorder(rect);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f, 4f, rect.height - 2f),
            selected ? UiPalette.Gold : UiPalette.Border);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = selected ? new Color(1f, .86f, .58f) : Color.white;
        Widgets.Label(new Rect(rect.x + 12f, rect.y, Math.Max(1f, rect.width - 220f), 25f), row.DisplayName);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = UiPalette.Muted;
        string detail = row.EnabledCount + " / " + row.CandidateCount + " enabled" + StateSuffix(row.State);
        Widgets.Label(new Rect(rect.x + 12f, rect.y + 25f, Math.Max(1f, rect.width - 24f), 18f), detail);
        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;

        if (Widgets.ButtonInvisible(rect))
        {
            emit?.Invoke(new UiCommand(
                UiCommandKind.SelectDomain,
                scope: SqueakVoicePackScope.Race,
                raceDefName: row.RaceDefName,
                targetDefName: "",
                arg: row.RaceDefName));
        }
    }

    private static string StateSuffix(SqueakVoicePackDomainState state)
    {
        return state switch
        {
            SqueakVoicePackDomainState.Orphan => " · orphan",
            SqueakVoicePackDomainState.TargetUnavailable => " · target unavailable",
            SqueakVoicePackDomainState.Dormant => " · dormant",
            _ => ""
        };
    }
}
