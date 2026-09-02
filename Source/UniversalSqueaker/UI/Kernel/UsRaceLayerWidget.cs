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

        string detail = UsPacksText.DetailText(ctx, race.EnabledCount, race.CandidateCount, race.State);
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
}

/// <summary>
/// Keyed-text outlet for the Packs workspace. The Race and Xenotype layers render the same
/// "n / m enabled · state" line and the same "name (race)" title, and the checklist joins pack
/// metadata with the same separator, so the template composition lives here once: Measure takes row
/// heights from Def/data text only (never from these strings), Draw is their single consumer, and
/// every lookup goes through <see cref="UiWidgetContext.Translation"/> — no Verse bypass, no second copy.
/// </summary>
internal static class UsPacksText
{
    internal const string KeyEnabledSummary = "US.Packs.Domain.EnabledSummary";
    internal const string KeyEnabledState = "US.Packs.Domain.EnabledState";
    internal const string KeyStateOrphan = "US.Packs.Domain.State.Orphan";
    internal const string KeyStateTargetUnavailable = "US.Packs.Domain.State.TargetUnavailable";
    internal const string KeyStateDormant = "US.Packs.Domain.State.Dormant";
    internal const string KeyXenotypeRaceContext = "US.Packs.Domain.XenotypeRaceContext";

    /// <summary>Formats a keyed template with invariant culture so digits stay as plain as before.</summary>
    internal static string Format(UiWidgetContext ctx, string key, params object[] args)
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, ctx.Translation.Translate(key), args);
    }

    /// <summary>The Tiny status line under a domain row: enabled/candidate counts plus optional state.</summary>
    internal static string DetailText(UiWidgetContext ctx, int enabled, int candidate, SqueakVoicePackDomainState state)
    {
        string summary = Format(ctx, KeyEnabledSummary, enabled, candidate);
        string stateKey = StateKey(state);
        return stateKey.Length == 0 ? summary : Format(ctx, KeyEnabledState, summary, ctx.Translation.Translate(stateKey));
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
