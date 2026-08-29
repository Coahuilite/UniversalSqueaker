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

            float width = VoicePacksLayout.InnerWidth(ctx.ViewWidth);
            var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
            float headerHeight = VoicePacksLayout.SectionHeaderHeightFor(HeaderText, width, metrics);
            float height = headerHeight + VoicePacksLayout.Gap
                + xenotypes.Count * (VoicePacksLayout.RaceLayerRowHeight + VoicePacksLayout.Gap);
            string helpKey = UsHelp.ResolveKey(_spec);
            if (UsHelp.IsOpen(ctx, helpKey))
            {
                height += UsHelp.BannerHeight(ctx, helpKey, width) + VoicePacksLayout.Gap;
            }
            return height;
        }, 0f, Kind, "UniversalSqueaker");
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        string helpKey = UsHelp.ResolveKey(_spec);
        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit, helpKey),
            fallback => Widgets.Label(fallback, HeaderText + " (unavailable)"),
            Kind, "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit, string helpKey)
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
        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
        float headerHeight = VoicePacksLayout.SectionHeaderHeightFor(HeaderText, innerWidth, metrics);
        Rect headerRect = new(x, y, innerWidth, headerHeight);
        UsWidgetDrawing.DrawSectionHeader(headerRect, HeaderText);
        Rect helpRect = new(headerRect.xMax - 22f, headerRect.y, 22f, Math.Min(22f, headerHeight));
        UsHelp.DrawHelpButton(helpRect, helpKey, ctx, emit);
        y += headerHeight + VoicePacksLayout.Gap;

        if (UsHelp.IsOpen(ctx, helpKey))
        {
            float helpHeight = UsHelp.BannerHeight(ctx, helpKey, innerWidth);
            if (helpHeight > 0f)
            {
                UsHelp.DrawBanner(new Rect(x, y, innerWidth, helpHeight), helpKey, ctx);
                y += helpHeight + VoicePacksLayout.Gap;
            }
        }

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
