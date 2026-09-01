using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US Race Layer: one row per race domain from the typed "races" binding. Clicking a
/// row selects the domain through the typed "select-domain" action.
/// </summary>
public sealed class UsRaceLayerWidget : UsSectionWidgetBase
{
    public const string KindName = "us/race-layer";

    private const float RowGap = 2f;

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsRaceLayerWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<IReadOnlyList<RaceLayerRowView>>("races", elementPath);
        bindings.ValidateValue<VoicePackDomainView?>("selected-domain", elementPath);
        bindings.ValidateAction<UsDomainSelection>("select-domain", elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return 64f;
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        IReadOnlyList<RaceLayerRowView> races = ctx.Bindings.TryGet("races", out IReadOnlyList<RaceLayerRowView> r)
            ? r
            : Array.Empty<RaceLayerRowView>();
        if (races.Count == 0) return 0f;

        float height = 0f;
        foreach (RaceLayerRowView race in races)
        {
            height += RowHeightFor(race, ctx) + RowGap;
        }

        return height;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        IReadOnlyList<RaceLayerRowView> races = ctx.Bindings.TryGet("races", out IReadOnlyList<RaceLayerRowView> r)
            ? r
            : Array.Empty<RaceLayerRowView>();
        if (races.Count == 0) return;

        VoicePackDomainView? selected = ctx.Bindings.TryGet("selected-domain", out VoicePackDomainView? s) ? s : null;

        float y = rect.y;
        foreach (RaceLayerRowView race in races)
        {
            bool isSelected = selected.HasValue
                && selected.Value.Scope == SqueakVoicePackScope.Race
                && string.Equals(selected.Value.RaceDefName, race.RaceDefName, StringComparison.Ordinal);
            float rowHeight = RowHeightFor(race, ctx);
            DrawRaceRow(new Rect(rect.x, y, rect.width, rowHeight), race, isSelected, ctx);
            y += rowHeight + RowGap;
        }
    }

    private void DrawRaceRow(Rect rect, RaceLayerRowView race, bool selected, UiWidgetContext ctx)
    {
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, selected);

        string detail = race.EnabledCount + " / " + race.CandidateCount + " enabled" + StateSuffix(race.State);
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + 4f, Math.Max(1f, rect.width - 20f), 18f),
            race.DisplayName,
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
            ctx.Bindings.Invoke("select-domain", new UsDomainSelection(SqueakVoicePackScope.Race, race.RaceDefName, ""));
        }
    }

    private float RowHeightFor(RaceLayerRowView race, UiWidgetContext ctx)
    {
        float measured = ctx.Metrics.MeasureText(race.DisplayName, UiFont.Small, Math.Max(1f, BodyWidth(ctx) - 40f));
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
