using System;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// US composite widget: draws the "VoicePack Checklist" section header plus the full checklist
/// (search field, pack rows, empty state, dormant/target-unavailable/canonical-conflict banners,
/// orphan banner, and the Forget Unavailable button). The dynamic list stays in the US layer.
/// </summary>
public sealed class VoicePackChecklistWidget : IWidget
{
    public const string Kind = "us/voice-pack-checklist";

    private const string HeaderText = "VoicePack Checklist";
    private const string NoDomainsText = "No VoicePack domains are available yet. Install a VoicePack that declares a raceDefName.";

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _ = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        if (!ctx.TryGetViewValue("SelectedDomain", out object? value)) return 0f;

        float width = VoicePacksLayout.InnerWidth(ctx.ViewWidth);
        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
        float headerHeight = VoicePacksLayout.SectionHeaderHeightFor(HeaderText, width, metrics);

        if (value is not VoicePackDomainView domain)
        {
            return headerHeight + VoicePacksLayout.Gap
                + VoicePacksLayout.EmptyStateHeight + VoicePacksLayout.Gap;
        }

        return headerHeight + VoicePacksLayout.Gap
            + VoicePacksLayout.ChecklistHeight(domain, ctx.State.SearchText, width, metrics)
            + VoicePacksLayout.Gap;
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UsGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit),
            fallback => Widgets.Label(fallback, HeaderText + " (unavailable)"),
            Kind);
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (!ctx.TryGetViewValue("SelectedDomain", out object? value)) return;

        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y;
        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
        float headerHeight = VoicePacksLayout.SectionHeaderHeightFor(HeaderText, innerWidth, metrics);
        UsWidgetDrawing.DrawSectionHeader(new Rect(x, y, innerWidth, headerHeight), HeaderText);
        y += headerHeight + VoicePacksLayout.Gap;

        if (value is not VoicePackDomainView domain)
        {
            EmptyState.Draw(
                new Rect(x, y, innerWidth, VoicePacksLayout.EmptyStateHeight),
                NoDomainsText);
            return;
        }

        float checklistHeight = VoicePacksLayout.ChecklistHeight(
            domain, ctx.State.SearchText, innerWidth, metrics);

        UiPageState pageState = ctx.State;
        VoicePackChecklist.Draw(
            new Rect(x, y, innerWidth, checklistHeight),
            domain,
            ref pageState.SearchText,
            UsWidgetCommandAdapter.For(emit));
    }
}
