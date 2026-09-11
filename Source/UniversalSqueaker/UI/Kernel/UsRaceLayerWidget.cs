using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US Race Layer: one row per race domain from the typed "races" binding. Clicking a
/// row selects the domain through the typed "select-domain" action.
/// </summary>
public sealed class UsRaceLayerWidget : UsSectionWidgetBase
{
    public const string KindName = "us/race-layer";

    // Row detail line: composed from keyed templates only (see UsPacksText at the bottom of this
    // file, which the Xenotype layer and the VoicePack checklist share, so one text has one resolver).

    // Row text bands. Both lines go through RowBands below: GUI.Label does not clip to its rect, so a
    // detail line long enough to wrap must grow the row rather than spill over the next section.
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

        float textWidth = Math.Max(1f, BodyWidth(ctx) - RowTextInset);
        float height = 0f;
        foreach (RaceLayerRowView race in races)
        {
            height += RowHeightFor(race, ctx, textWidth) + RowGap;
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
        float textWidth = Math.Max(1f, rect.width - RowTextInset);

        float y = rect.y;
        foreach (RaceLayerRowView race in races)
        {
            bool isSelected = selected.HasValue
                && selected.Value.Scope == SqueakVoicePackScope.Race
                && string.Equals(selected.Value.RaceDefName, race.RaceDefName, StringComparison.Ordinal);
            float rowHeight = RowHeightFor(race, ctx, textWidth);
            DrawRaceRow(new Rect(rect.x, y, rect.width, rowHeight), race, isSelected, textWidth, ctx);
            y += rowHeight + RowGap;
        }
    }

    private void DrawRaceRow(Rect rect, RaceLayerRowView race, bool selected, float textWidth, UiWidgetContext ctx)
    {
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/race-layer/row");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, selected);

        string title = UsPacksText.TitleWithState(ctx, race.DisplayName, race.State);
        string detail = UsPacksText.DetailText(ctx, race.EnabledCount, race.CandidateCount);
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
            ctx.Bindings.Invoke("select-domain", new UsDomainSelection(SqueakVoicePackScope.Race, race.RaceDefName, ""));
        }
    }

    private float RowHeightFor(RaceLayerRowView race, UiWidgetContext ctx, float textWidth)
    {
        return RowBands(
            ctx,
            textWidth,
            UsPacksText.TitleWithState(ctx, race.DisplayName, race.State),
            UsPacksText.DetailText(ctx, race.EnabledCount, race.CandidateCount)).Total;
    }

    /// <summary>
    /// The two text bands of a domain row, resolved by one shared path so the height Measure allocates
    /// and the offsets Draw uses cannot disagree. Previously only the title was measured and the detail
    /// line sat in an unmeasured 16px band, so a wrapped detail over drew the section below it.
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

/// <summary>
/// Keyed-text outlet for the Packs workspace. The Race and Xenotype layers render the same
/// "n / m enabled" detail line and the same "name (race)" title, and the checklist joins pack
/// metadata with the same separator, so the template composition lives here once: the row title and
/// its state, and the detail line, are measured and drawn from this one resolver, and every lookup
/// goes through <see cref="UiWidgetContext.Translation"/> — no Verse bypass, no second copy.
///
/// Word order is part of the key text, never of layout code: a translation is free to put the state
/// before or after the name, and the en/zh pair can then share one layout (measured band, one label).
/// </summary>
internal static class UsPacksText
{
    internal const string KeyEnabledSummary = "US.Packs.Domain.EnabledSummary";
    /// <summary>Whole-sentence template for "object plus its state"; the placeholder order IS the word order.</summary>
    internal const string KeyNameWithState = "US.Packs.Domain.NameWithState";
    internal const string KeyStateOrphan = "US.Packs.Domain.State.Orphan";
    internal const string KeyStateTargetUnavailable = "US.Packs.Domain.State.TargetUnavailable";
    internal const string KeyStateDormant = "US.Packs.Domain.State.Dormant";
    internal const string KeyXenotypeRaceContext = "US.Packs.Domain.XenotypeRaceContext";

    /// <summary>Formats a keyed template with invariant culture so digits stay as plain as before.</summary>
    internal static string Format(UiWidgetContext ctx, string key, params object[] args)
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, ctx.Translation.Translate(key), args);
    }

    /// <summary>The Tiny status line under a domain row: enabled/candidate counts, no state.</summary>
    internal static string DetailText(UiWidgetContext ctx, int enabled, int candidate)
    {
        return Format(ctx, KeyEnabledSummary, enabled, candidate);
    }

    /// <summary>
    /// The row title: the object's name followed by its state, composed through one keyed template
    /// (the state is postposed in Chinese and the pair shares a single label rect in both languages).
    /// A domain without a state keeps its bare name, so the template is never asked to render a hole.
    /// </summary>
    internal static string TitleWithState(UiWidgetContext ctx, string name, SqueakVoicePackDomainState state)
    {
        string stateKey = StateKey(state);
        return stateKey.Length == 0 ? name : Format(ctx, KeyNameWithState, name, ctx.Translation.Translate(stateKey));
    }

    /// <summary>Translation key of the row state, or empty when the domain carries no state.</summary>
    internal static string StateKey(SqueakVoicePackDomainState state)
    {
        return state switch
        {
            SqueakVoicePackDomainState.Orphan => KeyStateOrphan,
            SqueakVoicePackDomainState.TargetUnavailable => KeyStateTargetUnavailable,
            SqueakVoicePackDomainState.Dormant => KeyStateDormant,
            _ => "",
        };
    }
}
