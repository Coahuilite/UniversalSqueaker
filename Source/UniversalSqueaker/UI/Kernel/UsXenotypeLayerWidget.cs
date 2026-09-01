using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;
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

    private const float RowGap = 2f;

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

        float height = 0f;
        foreach (VoicePackDomainView domain in domains)
        {
            height += RowHeightFor(domain, ctx) + RowGap;
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

        float y = rect.y;
        foreach (VoicePackDomainView domain in domains)
        {
            bool isSelected = selected.HasValue
                && selected.Value.Scope == SqueakVoicePackScope.Xenotype
                && string.Equals(selected.Value.RaceDefName, domain.RaceDefName, StringComparison.Ordinal)
                && string.Equals(selected.Value.TargetDefName, domain.TargetDefName, StringComparison.Ordinal);
            float rowHeight = RowHeightFor(domain, ctx);
            DrawDomainRow(new Rect(rect.x, y, rect.width, rowHeight), domain, isSelected, ctx);
            y += rowHeight + RowGap;
        }
    }

    private void DrawDomainRow(Rect rect, VoicePackDomainView domain, bool selected, UiWidgetContext ctx)
    {
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, selected);

        string detail = domain.EnabledCount + " / " + domain.CandidateCount + " enabled" + StateSuffix(domain.State);
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + 4f, Math.Max(1f, rect.width - 20f), 18f),
            domain.DisplayName + " (" + domain.RaceDefName + ")",
            ctx.Theme,
            selected ? ctx.Theme.TextOnGold : ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + 22f, Math.Max(1f, rect.width - 20f), 16f),
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

    private float RowHeightFor(VoicePackDomainView domain, UiWidgetContext ctx)
    {
        float measured = ctx.Metrics.MeasureText(domain.DisplayName, UiFont.Small, Math.Max(1f, BodyWidth(ctx) - 60f));
        return Math.Max(48f, measured + 32f);
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
