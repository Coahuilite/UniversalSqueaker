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
/// static roots.
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

        if (!ctx.TryGetViewValue("Races", out object? value)
            || value is not IReadOnlyList<RaceLayerRowView> races
            || races.Count == 0)
        {
            return 0f;
        }

        float width = VoicePacksLayout.InnerWidth(ctx.ViewWidth);
        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
        float headerHeight = VoicePacksLayout.SectionHeaderHeightFor(HeaderText, width, metrics);
        float height = headerHeight + VoicePacksLayout.Gap
            + races.Count * (VoicePacksLayout.RaceLayerRowHeight + VoicePacksLayout.Gap);
        string helpKey = UsHelp.ResolveKey(_spec);
        if (UsHelp.IsOpen(ctx, helpKey))
        {
            height += UsHelp.BannerHeight(ctx, helpKey, width) + VoicePacksLayout.Gap;
        }
        return height;
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        string helpKey = UsHelp.ResolveKey(_spec);
        UsGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit, helpKey),
            fallback => Widgets.Label(fallback, HeaderText + " (unavailable)"),
            Kind);
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit, string helpKey)
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
        foreach (RaceLayerRowView race in races)
        {
            bool selected = ctx.TryGetViewValue("SelectedDomain", out object? domainValue)
                && domainValue is VoicePackDomainView domain
                && domain.Scope == SqueakVoicePackScope.Race
                && string.Equals(domain.RaceDefName, race.RaceDefName, StringComparison.Ordinal);

            RaceLayerRow.Draw(
                new Rect(x, y, innerWidth, VoicePacksLayout.RaceLayerRowHeight),
                race,
                selected,
                businessEmit);

            y += VoicePacksLayout.RaceLayerRowHeight + VoicePacksLayout.Gap;
        }
    }
}
