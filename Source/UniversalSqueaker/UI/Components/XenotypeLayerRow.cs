using System;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>One Xenotype domain row in the Xenotype layer. Stateless; emits SelectDomain when clicked.</summary>
public static class XenotypeLayerRow
{
    public static void Draw(Rect rect, VoicePackDomainView domain, bool selected, Action<UiCommand> emit)
    {
        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, domain, selected, emit),
            fallback => DrawVanilla(fallback, domain, emit),
            "us/xenotype-layer-row",
            "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, VoicePackDomainView domain, bool selected, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, selected, false);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = selected ? UsVisualTokens.TextOnGold : UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + 12f, rect.y, Math.Max(1f, rect.width - 24f), 25f), domain.DisplayName);
        if (VoicePacksLayout.ForWidth(rect.width) == LayoutTier.Comfortable)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = UsVisualTokens.TextSecondary;
            string detail = domain.EnabledCount + " / " + domain.CandidateCount + " enabled" + StateSuffix(domain.State);
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 25f, Math.Max(1f, rect.width - 24f), 18f), detail);
        }
        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;

        UiInteract.Row(rect, () => emit?.Invoke(new UiCommand(
            UiCommandKind.SelectDomain,
            scope: SqueakVoicePackScope.Xenotype,
            raceDefName: domain.RaceDefName,
            targetDefName: domain.TargetDefName,
            arg: domain.TargetDefName)));
    }

    private static void DrawVanilla(Rect rect, VoicePackDomainView domain, Action<UiCommand> emit)
    {
        if (Widgets.ButtonText(rect, domain.DisplayName))
        {
            emit?.Invoke(new UiCommand(
                UiCommandKind.SelectDomain,
                scope: SqueakVoicePackScope.Xenotype,
                raceDefName: domain.RaceDefName,
                targetDefName: domain.TargetDefName,
                arg: domain.TargetDefName));
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
