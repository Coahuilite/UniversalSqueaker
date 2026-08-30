using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// US composite widget: draws the "Race Layer" section header plus every race row.
/// Dynamic list content lives inside this widget because the Phase A engine only supports flat,
/// static roots. Row heights are measured from the display/detail text so wrapped names never clip.
/// </summary>
public sealed class RaceLayerWidget : IWidget
{
    public const string Kind = "us/race-layer";

    private const string HeaderText = "Race Layer";

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        return UiGuard.MeasureOrFallback(() =>
        {
            if (!ctx.TryGetViewValue("Races", out object? value)
                || value is not IReadOnlyList<RaceLayerRowView> races
                || races.Count == 0)
            {
                return 0f;
            }

            float innerWidth = VoicePacksLayout.InnerWidth(ctx.ViewWidth);
            var metrics = new FerriteTextMetricsAdapter(ctx.Metrics, UiFont.Small);
            float bodyHeight = 0f;
            foreach (RaceLayerRowView race in races)
            {
                string detail = race.EnabledCount + " / " + race.CandidateCount + " enabled" + StateSuffix(race.State);
                bodyHeight += VoicePacksLayout.LayerRowHeightFor(race.DisplayName, detail, innerWidth, metrics) + VoicePacksLayout.Gap;
            }

            return UiGuard.MeasureOrFallback(
                () => UsCard.Measure(bodyHeight, ctx),
                UsCard.Measure(bodyHeight, ctx),
                Kind, "UniversalSqueaker");
        }, 0f, Kind, "UniversalSqueaker");
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit),
            fallback => Widgets.Label(fallback, HeaderText + " (unavailable)"),
            Kind, "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (!ctx.TryGetViewValue("Races", out object? value)
            || value is not IReadOnlyList<RaceLayerRowView> races
            || races.Count == 0)
        {
            return;
        }

        UsCard.Draw(rect, HeaderText, ctx, body => DrawBody(body, ctx, emit));
    }

    private static void DrawBody(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (!ctx.TryGetViewValue("Races", out object? value)
            || value is not IReadOnlyList<RaceLayerRowView> races
            || races.Count == 0)
        {
            return;
        }

        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y;
        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics, UiFont.Small);

        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        foreach (RaceLayerRowView race in races)
        {
            bool selected = ctx.TryGetViewValue("SelectedDomain", out object? domainValue)
                && domainValue is VoicePackDomainView domain
                && domain.Scope == SqueakVoicePackScope.Race
                && string.Equals(domain.RaceDefName, race.RaceDefName, StringComparison.Ordinal);

            string detail = race.EnabledCount + " / " + race.CandidateCount + " enabled" + StateSuffix(race.State);
            float rowHeight = VoicePacksLayout.LayerRowHeightFor(race.DisplayName, detail, innerWidth, metrics);
            RaceLayerRow.Draw(
                new Rect(x, y, innerWidth, rowHeight),
                race,
                selected,
                businessEmit,
                metrics);

            y += rowHeight + VoicePacksLayout.Gap;
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
