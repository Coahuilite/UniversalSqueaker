using System;
using System.Collections.Generic;
using UnityEngine;
using FerriteLib.UiKit;
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
        string preset = ctx.Bindings.TryGet("distance-preset", out string presetText) ? presetText : "Custom";

        float x = rect.x;
        float y = rect.y + TopPadding;

        DrawChart(new Rect(x, y, rect.width, ChartHeight), ctx);
        y += ChartHeight + Gap;

        UsKernelDraw.Label(
            new Rect(x, y, rect.width, StatusHeight),
            preset + "  " + AttenuationMath.FormatRangeDisplay(min, max),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
        y += StatusHeight + Gap;

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
        DrawPresetButton(new Rect(rect.x, rect.y, buttonWidth, rect.height), "Conservative",
            IsCurrentPreset(currentPreset, SqueakDistancePreset.Conservative), SqueakDistancePreset.Conservative, ctx);
        DrawPresetButton(new Rect(rect.x + buttonWidth + Gap, rect.y, buttonWidth, rect.height), "Balanced",
            IsCurrentPreset(currentPreset, SqueakDistancePreset.Balanced), SqueakDistancePreset.Balanced, ctx);
        DrawPresetButton(new Rect(rect.x + (buttonWidth + Gap) * 2f, rect.y, buttonWidth, rect.height), "Strong",
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
        string preset = ctx.Bindings.TryGet("distance-preset", out string presetText) ? presetText : "Custom";
        float min = ctx.Bindings.TryGet("distance-range-min", out float minBound) ? minBound : AttenuationMath.MinDistance;
        float max = ctx.Bindings.TryGet("distance-range-max", out float maxBound) ? maxBound : 50f;
        AttenuationMath.SanitizeRange(ref min, ref max);
        UsKernelDraw.Label(
            rect,
            "Attenuation " + preset + "  " + AttenuationMath.FormatRangeDisplay(min, max),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
    }

    private static bool IsCurrentPreset(string currentPreset, SqueakDistancePreset preset)
    {
        return string.Equals(currentPreset, preset.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
