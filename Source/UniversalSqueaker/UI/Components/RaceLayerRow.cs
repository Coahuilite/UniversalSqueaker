using System;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>One race row in the Race layer. Stateless; emits SelectDomain when clicked.</summary>
public static class RaceLayerRow
{
    public static void Draw(Rect rect, RaceLayerRowView row, bool selected, Action<UiCommand> emit, ITextMetrics? metrics = null)
    {
        ITextMetrics effectiveMetrics = metrics ?? VerseTextMetrics.Instance;
        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, row, selected, emit, effectiveMetrics),
            fallback => DrawVanilla(fallback, row, emit),
            "us/race-layer-row",
            "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, RaceLayerRowView row, bool selected, Action<UiCommand> emit, ITextMetrics metrics)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, selected, false);

        float textWidth = Math.Max(1f, rect.width - 24f);
        bool showDetail = VoicePacksLayout.ForWidth(rect.width) == LayoutTier.Comfortable;
        string detail = row.EnabledCount + " / " + row.CandidateCount + " enabled" + StateSuffix(row.State);
        float labelHeight = Math.Max(20f, metrics.CalcHeight(row.DisplayName, textWidth));
        float detailHeight = showDetail ? Math.Max(16f, metrics.CalcHeight(detail, textWidth)) : 0f;

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = selected ? UsVisualTokens.TextOnGold : UsVisualTokens.TextPrimary;
        Rect labelRect = new(rect.x + 12f, rect.y + 4f, textWidth, labelHeight);
        Widgets.Label(labelRect, row.DisplayName);

        if (showDetail)
        {
            Rect detailRect = new(
                rect.x + 12f,
                rect.y + 4f + labelHeight + 2f,
                textWidth,
                detailHeight);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UsVisualTokens.TextSecondary;
            Widgets.Label(detailRect, detail);
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
