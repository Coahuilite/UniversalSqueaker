using System;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>One race row in the Race layer. Stateless; emits SelectDomain when clicked.</summary>
public static class RaceLayerRow
{
    public static void Draw(Rect rect, RaceLayerRowView row, bool selected, Action<UiCommand> emit)
    {
        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, row, selected, emit),
            fallback => DrawVanilla(fallback, row, emit),
            "us/race-layer-row",
            "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, RaceLayerRowView row, bool selected, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, selected, false);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = selected ? UsVisualTokens.TextOnGold : UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + 12f, rect.y, Math.Max(1f, rect.width - 24f), 25f), row.DisplayName);
        if (VoicePacksLayout.ForWidth(rect.width) == LayoutTier.Comfortable)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = UsVisualTokens.TextSecondary;
            string detail = row.EnabledCount + " / " + row.CandidateCount + " enabled" + StateSuffix(row.State);
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 25f, Math.Max(1f, rect.width - 24f), 18f), detail);
        }
        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;

        UiInteract.Row(rect, () => emit?.Invoke(new UiCommand(
            UiCommandKind.SelectDomain,
            scope: SqueakVoicePackScope.Race,
            raceDefName: row.RaceDefName,
            targetDefName: "",
            arg: row.RaceDefName)));
    }

    private static void DrawVanilla(Rect rect, RaceLayerRowView row, Action<UiCommand> emit)
    {
        if (Widgets.ButtonText(rect, row.DisplayName))
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
            _ => "",
        };
    }
}
