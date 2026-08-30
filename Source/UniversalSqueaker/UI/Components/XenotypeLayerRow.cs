using System;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>One Xenotype domain row in the Xenotype layer. Stateless; emits SelectDomain when clicked.</summary>
public static class XenotypeLayerRow
{
    public static void Draw(Rect rect, VoicePackDomainView domain, bool selected, Action<UiCommand> emit, ITextMetrics? metrics = null)
    {
        ITextMetrics effectiveMetrics = metrics ?? VerseTextMetrics.Instance;
        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, domain, selected, emit, effectiveMetrics),
            fallback => DrawVanilla(fallback, domain, emit),
            "us/xenotype-layer-row",
            "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, VoicePackDomainView domain, bool selected, Action<UiCommand> emit, ITextMetrics metrics)
    {
        bool hovered = Mouse.IsOver(rect);
        bool dimmed = domain.CandidateCount <= 0;
        UsSurface.DrawRowSurface(rect, hovered, selected, false);
        if (dimmed)
        {
            // Dim the whole row so "no packs installed" reads as unavailable-content, not unsupported.
            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.35f));
        }

        float textWidth = Math.Max(1f, rect.width - 24f);
        bool showDetail = VoicePacksLayout.ForWidth(rect.width) == LayoutTier.Comfortable;
        string detail = VoicePacksLayout.LayerDetailText(domain.EnabledCount, domain.CandidateCount, StateSuffix(domain.State));
        float labelHeight = Math.Max(20f, metrics.CalcHeight(domain.DisplayName, textWidth));
        float detailHeight = showDetail ? Math.Max(16f, metrics.CalcHeight(detail, textWidth)) : 0f;

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = selected ? UsVisualTokens.TextOnGold : UsVisualTokens.TextPrimary;
        if (dimmed) GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 0.55f);
        Rect labelRect = new(rect.x + 12f, rect.y + 4f, textWidth, labelHeight);
        Widgets.Label(labelRect, domain.DisplayName);

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
            if (dimmed) GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 0.55f);
            Widgets.Label(detailRect, detail);
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
