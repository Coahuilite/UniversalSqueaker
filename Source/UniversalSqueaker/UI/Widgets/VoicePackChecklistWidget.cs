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
            if (!ctx.TryGetViewValue("SelectedDomain", out object? value)) return 0f;

            float width = VoicePacksLayout.InnerWidth(ctx.ViewWidth);
            var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
            float bodyHeight;
            if (value is not VoicePackDomainView domain)
            {
                bodyHeight = VoicePacksLayout.EmptyStateHeight;
            }
            else
            {
                bodyHeight = VoicePacksLayout.ChecklistHeight(domain, ctx.State.SearchText, width, metrics);
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
        if (!ctx.TryGetViewValue("SelectedDomain", out object? value)) return;
        UsCard.Draw(rect, HeaderText, ctx, body => DrawBody(body, ctx, emit));
    }

    private static void DrawBody(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (!ctx.TryGetViewValue("SelectedDomain", out object? value)) return;

        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y;
        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);

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
            UsWidgetCommandAdapter.For(emit),
            metrics);
    }
}
