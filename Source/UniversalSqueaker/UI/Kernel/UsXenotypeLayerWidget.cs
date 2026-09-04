using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US Xenotype Layer: one row per (race, xenotype) domain from the typed
/// "xenotype-domains" binding. Clicking a row selects the domain through the typed
/// "select-domain" action.
/// </summary>
public sealed class UsXenotypeLayerWidget : UsSectionWidgetBase
{
    public const string KindName = "us/xenotype-layer";

    // Row title and detail line resolve through UsPacksText (UsRaceLayerWidget.cs): one keyed
    // template per text, translated once at the draw call site.

    // Row text bands, shared with the Race layer's shape. Both lines are measured through RowBands
    // below, and from the same composed strings Draw renders: this row used to measure the bare
    // xenotype name while drawing "name (race)", so the band was short by the whole suffix.
    private const float RowGap = 2f;
    private const float RowTopPadding = 4f;
    private const float RowBottomPadding = 6f;
    private const float TitleBand = 18f;
    private const float DetailGap = 2f;
    private const float DetailBand = 16f;
    private const float RowMinHeight = 48f;
    private const float RowTextInset = 20f;

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsXenotypeLayerWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<IReadOnlyList<VoicePackDomainView>>("xenotype-domains", elementPath);
        bindings.ValidateValue<VoicePackDomainView?>("selected-domain", elementPath);
        bindings.ValidateAction<UsDomainSelection>("select-domain", elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return 64f;
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        IReadOnlyList<VoicePackDomainView> domains = ctx.Bindings.TryGet("xenotype-domains", out IReadOnlyList<VoicePackDomainView> d)
            ? d
            : Array.Empty<VoicePackDomainView>();
        if (domains.Count == 0) return 0f;

        float textWidth = Math.Max(1f, BodyWidth(ctx) - RowTextInset);
        float height = 0f;
        foreach (VoicePackDomainView domain in domains)
        {
            height += RowHeightFor(domain, ctx, textWidth) + RowGap;
        }

        return height;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        IReadOnlyList<VoicePackDomainView> domains = ctx.Bindings.TryGet("xenotype-domains", out IReadOnlyList<VoicePackDomainView> d)
            ? d
            : Array.Empty<VoicePackDomainView>();
        if (domains.Count == 0) return;

        VoicePackDomainView? selected = ctx.Bindings.TryGet("selected-domain", out VoicePackDomainView? s) ? s : null;
        float textWidth = Math.Max(1f, rect.width - RowTextInset);

        float y = rect.y;
        foreach (VoicePackDomainView domain in domains)
        {
            bool isSelected = selected.HasValue
                && selected.Value.Scope == SqueakVoicePackScope.Xenotype
                && string.Equals(selected.Value.RaceDefName, domain.RaceDefName, StringComparison.Ordinal)
                && string.Equals(selected.Value.TargetDefName, domain.TargetDefName, StringComparison.Ordinal);
            float rowHeight = RowHeightFor(domain, ctx, textWidth);
            DrawDomainRow(new Rect(rect.x, y, rect.width, rowHeight), domain, isSelected, textWidth, ctx);
            y += rowHeight + RowGap;
        }
    }

    private void DrawDomainRow(Rect rect, VoicePackDomainView domain, bool selected, float textWidth, UiWidgetContext ctx)
    {
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, selected);

        string title = TitleText(ctx, domain);
        string detail = UsPacksText.DetailText(ctx, domain.EnabledCount, domain.CandidateCount, domain.State);
        (float titleBand, float detailBand, float _) = RowBands(ctx, textWidth, title, detail);
        float x = rect.x + UsKernelDraw.RowLeftPadding;
        float lineY = rect.y + RowTopPadding;
        UsKernelDraw.Label(
            new Rect(x, lineY, textWidth, titleBand),
            title,
            ctx.Theme,
            selected ? ctx.Theme.TextOnGold : ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        lineY += titleBand + DetailGap;
        UsKernelDraw.Label(
            new Rect(x, lineY, textWidth, detailBand),
            detail,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        if (UiNative.Button(rect))
        {
            ctx.Bindings.Invoke(
                "select-domain",
                new UsDomainSelection(SqueakVoicePackScope.Xenotype, domain.RaceDefName, domain.TargetDefName));
        }
    }

    /// <summary>The one title outlet for a xenotype row: the name qualified by its race's translated
    /// label (the race layer already shows that label as its own row title; showing the raw defName
    /// here would present one entity two ways in the same card column).</summary>
    private static string TitleText(UiWidgetContext ctx, VoicePackDomainView domain)
    {
        return UsPacksText.Format(ctx, UsPacksText.KeyXenotypeRaceContext, domain.DisplayName, domain.RaceDisplay);
    }

    private float RowHeightFor(VoicePackDomainView domain, UiWidgetContext ctx, float textWidth)
    {
        return RowBands(
            ctx,
            textWidth,
            TitleText(ctx, domain),
            UsPacksText.DetailText(ctx, domain.EnabledCount, domain.CandidateCount, domain.State)).Total;
    }

    /// <summary>
    /// The two text bands of a domain row, resolved by one shared path so the height Measure allocates
    /// and the offsets Draw uses cannot disagree.
    /// </summary>
    private (float Title, float Detail, float Total) RowBands(
        UiWidgetContext ctx, float textWidth, string title, string detail)
    {
        float titleBand = Math.Max(TitleBand, ctx.Metrics.MeasureText(title, UiFont.Small, textWidth));
        float detailBand = Math.Max(DetailBand, ctx.Metrics.MeasureText(detail, UiFont.Tiny, textWidth));
        float total = Math.Max(
            RowMinHeight,
            RowTopPadding + titleBand + DetailGap + detailBand + RowBottomPadding);
        return (titleBand, detailBand, total);
    }
}
