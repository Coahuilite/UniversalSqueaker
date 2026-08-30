using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// US composite widget: draws the "Xenotype Layer" section header plus every Xenotype domain row.
/// Hides itself (returns 0 height / no draw) when the view model has no Xenotype domains.
/// </summary>
public sealed class XenotypeLayerWidget : IWidget
{
    public const string Kind = "us/xenotype-layer";

    private const string HeaderText = "Xenotype Layer";

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
            if (!ctx.TryGetViewValue("XenotypeDomains", out object? value)
                || value is not IReadOnlyList<VoicePackDomainView> xenotypes
                || xenotypes.Count == 0)
            {
                return 0f;
            }

            float bodyHeight = xenotypes.Count * (VoicePacksLayout.RaceLayerRowHeight + VoicePacksLayout.Gap);
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
        if (!ctx.TryGetViewValue("XenotypeDomains", out object? value)
            || value is not IReadOnlyList<VoicePackDomainView> xenotypes
            || xenotypes.Count == 0)
        {
            return;
        }

        UsCard.Draw(rect, HeaderText, ctx, body => DrawBody(body, ctx, emit));
    }

    private static void DrawBody(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (!ctx.TryGetViewValue("XenotypeDomains", out object? value)
            || value is not IReadOnlyList<VoicePackDomainView> xenotypes
            || xenotypes.Count == 0)
        {
            return;
        }

        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y;

        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        foreach (VoicePackDomainView domain in xenotypes)
        {
            bool selected = ctx.TryGetViewValue("SelectedDomain", out object? selectedValue)
                && selectedValue is VoicePackDomainView selectedDomain
                && selectedDomain.Scope == SqueakVoicePackScope.Xenotype
                && string.Equals(selectedDomain.RaceDefName, domain.RaceDefName, StringComparison.Ordinal)
                && string.Equals(selectedDomain.TargetDefName, domain.TargetDefName, StringComparison.Ordinal);

            XenotypeLayerRow.Draw(
                new Rect(x, y, innerWidth, VoicePacksLayout.RaceLayerRowHeight),
                domain,
                selected,
                businessEmit);

            y += VoicePacksLayout.RaceLayerRowHeight + VoicePacksLayout.Gap;
        }
    }
}
