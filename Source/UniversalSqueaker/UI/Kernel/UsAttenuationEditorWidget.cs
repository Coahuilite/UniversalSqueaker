using System;
using System.Collections.Generic;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US camera-height attenuation editor. Draws the 15..65 by 0..100 chart with two
/// horizontally draggable endpoints (start locked at 100%, end locked at 0%) via the kernel
/// <c>chart/line</c> widget and its typed "attenuation-point" action, plus three quick presets
/// through the typed "set-distance-preset" action. The chart drag uses native hotControl semantics
/// provided by the kernel chart.
/// </summary>
public sealed class UsAttenuationEditorWidget : UsSectionWidgetBase
{
    public const string KindName = "us/attenuation-editor";

    private const float ChartHeight = 64f;
    private const float StatusHeight = 20f;
    private const float ButtonsHeight = 26f;
    private const float Gap = 4f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;
    private const float NarrowHeight = 24f;
    private const float MinWidth = 200f;

    /// <summary>
    /// Fallback value of the "distance-preset" binding. It is a <see cref="SqueakDistancePreset"/> name,
    /// never display text: <see cref="IsCurrentPreset"/> compares against it and it must stay
    /// byte-identical. The player-facing word lives in <see cref="PresetCustomKey"/> instead.
    /// </summary>
    private const string PresetCustomValue = "Custom";

    private const string PresetConservativeKey = "US.Distance.Preset.Conservative";
    private const string PresetBalancedKey = "US.Distance.Preset.Balanced";
    private const string PresetStrongKey = "US.Distance.Preset.Strong";
    private const string PresetCustomKey = "US.Distance.Preset.Custom";

    /// <summary>Keyed narrow-screen status line; <c>{0}</c> is the preset, <c>{1}</c> the distance range.</summary>
    private const string NarrowSummaryKey = "US.Distance.Status";

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsAttenuationEditorWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<float>("distance-range-min", elementPath);
        bindings.ValidateValue<float>("distance-range-max", elementPath);
        bindings.ValidateValue<string>("distance-preset", elementPath);
        bindings.ValidateAction<SqueakDistancePreset>("set-distance-preset", elementPath);
        bindings.ValidateValue<IReadOnlyList<Vector2>>("attenuation-points", elementPath);
        bindings.ValidateAction<UiChartPointChange>("attenuation-point", elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return ChartHeight + StatusHeight + ButtonsHeight + Gap * 2f + TopPadding + BottomPadding;
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        return BodyWidth(ctx) < MinWidth ? NarrowHeight
            : TopPadding + ChartHeight + Gap + StatusHeight + Gap + ButtonsHeight + BottomPadding;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width < MinWidth)
        {
            DrawNarrowSummary(rect, ctx);
            return;
        }

        float min = ctx.Bindings.TryGet("distance-range-min", out float minBound) ? minBound : AttenuationMath.MinDistance;
        float max = ctx.Bindings.TryGet("distance-range-max", out float maxBound) ? maxBound : 50f;
        AttenuationMath.SanitizeRange(ref min, ref max);
        string preset = ctx.Bindings.TryGet("distance-preset", out string presetText) ? presetText : PresetCustomValue;

        float x = rect.x;
        float y = rect.y + TopPadding;

        UsKernelDraw.HelpHover(new Rect(x, y, rect.width, ChartHeight), ctx, "us/attenuation-editor/chart");
        DrawChart(new Rect(x, y, rect.width, ChartHeight), ctx);

        UsKernelDraw.Label(
            new Rect(x, y, rect.width, StatusHeight),
            PresetDisplay(ctx, preset) + "  " + AttenuationMath.FormatRangeDisplay(min, max),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
        y += StatusHeight + Gap;

        UsKernelDraw.HelpHover(new Rect(x, y, rect.width, ButtonsHeight), ctx, "us/attenuation-editor/presets");
        DrawPresetButtons(new Rect(x, y, rect.width, ButtonsHeight), preset, ctx);
    }

    private void DrawChart(Rect chartRect, UiWidgetContext ctx)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "attenuation-chart",
            ["Kind"] = LineChartWidget.Kind,
            ["Bind"] = "attenuation-points",
            ["ActionBind"] = "attenuation-point",
            ["Editable"] = "true",
            ["EditablePoints"] = "1,2",
            ["Height"] = ChartHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var chartSpec = new UiElementSpec("attenuation-chart", LineChartWidget.Kind, attributes);
        var chart = new LineChartWidget();
        chart.Configure(chartSpec);
        chart.Draw(chartRect, ctx);
    }

    private void DrawPresetButtons(Rect rect, string currentPreset, UiWidgetContext ctx)
    {
        float buttonWidth = (rect.width - Gap * 2f) / 3f;
        DrawPresetButton(new Rect(rect.x, rect.y, buttonWidth, rect.height), UsKernelDraw.Keyed(ctx, PresetConservativeKey),
            IsCurrentPreset(currentPreset, SqueakDistancePreset.Conservative), SqueakDistancePreset.Conservative, ctx);
        DrawPresetButton(new Rect(rect.x + buttonWidth + Gap, rect.y, buttonWidth, rect.height), UsKernelDraw.Keyed(ctx, PresetBalancedKey),
            IsCurrentPreset(currentPreset, SqueakDistancePreset.Balanced), SqueakDistancePreset.Balanced, ctx);
        DrawPresetButton(new Rect(rect.x + (buttonWidth + Gap) * 2f, rect.y, buttonWidth, rect.height), UsKernelDraw.Keyed(ctx, PresetStrongKey),
            IsCurrentPreset(currentPreset, SqueakDistancePreset.Strong), SqueakDistancePreset.Strong, ctx);
    }

    private void DrawPresetButton(Rect rect, string label, bool selected, SqueakDistancePreset preset, UiWidgetContext ctx)
    {
        if (UsKernelDraw.SelectionButton(rect, label, ctx.Theme, selected))
        {
            ctx.Bindings.Invoke("set-distance-preset", preset);
        }
    }

    private void DrawNarrowSummary(Rect rect, UiWidgetContext ctx)
    {
        string preset = ctx.Bindings.TryGet("distance-preset", out string presetText) ? presetText : PresetCustomValue;
        float min = ctx.Bindings.TryGet("distance-range-min", out float minBound) ? minBound : AttenuationMath.MinDistance;
        float max = ctx.Bindings.TryGet("distance-range-max", out float maxBound) ? maxBound : 50f;
        AttenuationMath.SanitizeRange(ref min, ref max);
        UsKernelDraw.Label(
            rect,
            string.Format(
                UsKernelDraw.Keyed(ctx, NarrowSummaryKey),
                PresetDisplay(ctx, preset),
                AttenuationMath.FormatRangeDisplay(min, max)),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
    }

    private static bool IsCurrentPreset(string currentPreset, SqueakDistancePreset preset)
    {
        return string.Equals(currentPreset, preset.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Display text for the bound preset value. The value is the <see cref="SqueakDistancePreset"/> name
    /// that <c>set-distance-preset</c> writes and <see cref="IsCurrentPreset"/> compares, so it is never
    /// translated; only this label mapping is. It matches presets through the same comparison the
    /// buttons use, so the highlighted button and the status line can never name two different presets;
    /// an unrecognised value is shown verbatim instead of being renamed.
    /// </summary>
    private static string PresetDisplay(UiWidgetContext ctx, string presetValue)
    {
        if (IsCurrentPreset(presetValue, SqueakDistancePreset.Conservative)) return UsKernelDraw.Keyed(ctx, PresetConservativeKey);
        if (IsCurrentPreset(presetValue, SqueakDistancePreset.Balanced)) return UsKernelDraw.Keyed(ctx, PresetBalancedKey);
        if (IsCurrentPreset(presetValue, SqueakDistancePreset.Strong)) return UsKernelDraw.Keyed(ctx, PresetStrongKey);
        if (IsCurrentPreset(presetValue, SqueakDistancePreset.Custom)) return UsKernelDraw.Keyed(ctx, PresetCustomKey);
        return presetValue;
    }
}
