using System;
using System.Collections.Generic;
using System.Globalization;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Widgets;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// S4-Vol camera-height attenuation editor. Draws a 15..65 by 0..100 chart with two horizontally
/// draggable endpoints (start locked at 100%, end locked at 0%), three quick presets, and a current
/// preset label. Dragging emits live <see cref="UiCommandKind.SetDistanceRange"/> updates; preset
/// buttons emit <see cref="UiCommandKind.SetDistancePreset"/>.
/// </summary>
public sealed class AttenuationEditorWidget : IWidget
{
    public const string Kind = "us/attenuation-editor";

    private const string Title = "Camera height attenuation";
    private const float ChartHeight = 64f;
    private const float StatusHeight = 20f;
    private const float ButtonsHeight = 26f;
    private const float Gap = 4f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;
    private const float LeftPadding = 10f;
    private const float RightPadding = 10f;
    private const float NarrowHeight = 24f;
    private const float MinWidth = 200f;

    private const float MinDistance = AttenuationMath.MinDistance;
    private const float MaxDistance = AttenuationMath.MaxDistance;
    private const float MinRange = AttenuationMath.MinRange;

    internal static void ResetSession()
    {
    }

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        float bodyHeight = ctx.ViewWidth < MinWidth ? NarrowHeight
            : TopPadding + ChartHeight + Gap + StatusHeight + Gap + ButtonsHeight + BottomPadding;
        return UiGuard.MeasureOrFallback(
            () => UsCard.Measure(bodyHeight, ctx),
            UsCard.Measure(bodyHeight, ctx),
            Kind, "UniversalSqueaker");
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit),
            fallback => DrawVanilla(fallback, ctx, emit),
            Kind, "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        UsCard.Draw(rect, Title, ctx, body => DrawBody(body, ctx, emit));
    }

    private static void DrawBody(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (rect.width < MinWidth)
        {
            DrawNarrowSummary(rect, ctx);
            return;
        }

        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        float min = ReadFloat(ctx, "DistanceRangeMin", MinDistance);
        float max = ReadFloat(ctx, "DistanceRangeMax", 50f);
        string preset = ReadString(ctx, "DistancePreset", "Custom");
        SanitizeRange(ref min, ref max);

        float innerWidth = Math.Max(1f, rect.width - LeftPadding - RightPadding);
        float x = rect.x + LeftPadding;
        float y = rect.y + TopPadding;

        Rect chartRect = new(x, y, innerWidth, ChartHeight);
        DrawAttenuationChart(chartRect, min, max, ctx, businessEmit);
        y += ChartHeight + Gap;

        DrawStatus(new Rect(x, y, innerWidth, StatusHeight), preset, min, max);
        y += StatusHeight + Gap;

        DrawPresetButtons(new Rect(x, y, innerWidth, ButtonsHeight), businessEmit);
    }

    private static void DrawVanilla(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        string preset = ReadString(ctx, "DistancePreset", "Custom");
        float min = ReadFloat(ctx, "DistanceRangeMin", MinDistance);
        float max = ReadFloat(ctx, "DistanceRangeMax", 50f);
        SanitizeRange(ref min, ref max);
        Widgets.Label(rect, "Attenuation " + preset + "  " + FormatRangeDisplay(min, max));
    }

    private static void DrawAttenuationChart(Rect chartRect, float min, float max, WidgetContext ctx, Action<UiCommand> emit)
    {
        float minNorm = Mathf.InverseLerp(MinDistance, MaxDistance, min);
        float maxNorm = Mathf.InverseLerp(MinDistance, MaxDistance, max);
        var points = new List<Vector2>
        {
            new Vector2(0f, 1f),
            new Vector2(Mathf.Clamp01(minNorm), 1f),
            new Vector2(Mathf.Clamp01(maxNorm), 0f),
            new Vector2(1f, 0f)
        };

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bind"] = "Points",
            ["EmitName"] = "AttenuationPoint",
            ["Height"] = ChartHeight.ToString(CultureInfo.InvariantCulture),
            ["Editable"] = "true",
            ["EditablePoints"] = "1,2"
        };
        var spec = new UiElementSpec(Kind + "-chart", LineChartWidget.Kind, attributes);
        var view = new Dictionary<string, object?> { ["Points"] = points };
        var chartCtx = new WidgetContext(ctx.Source, view, ctx.Metrics, ctx.State);
        var chart = new LineChartWidget();
        chart.Configure(spec);
        chart.Draw(chartRect, chartCtx, cmd =>
        {
            if (cmd.Name != "AttenuationPoint" || cmd.Payload is not LineChartPointChange change) return;
            float distance = Mathf.Lerp(MinDistance, MaxDistance, Mathf.Clamp01(change.Point.x));
            if (change.Index == 1)
            {
                min = Mathf.Clamp(distance, MinDistance, max - MinRange);
            }
            else if (change.Index == 2)
            {
                max = Mathf.Clamp(distance, min + MinRange, MaxDistance);
            }
            else
            {
                return;
            }
            emit(new UiCommand(UiCommandKind.SetDistanceRange, arg: FormatRange(min, max)));
        });
    }

    private static void DrawStatus(Rect rect, string preset, float min, float max)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextSecondary;
        Widgets.Label(rect, preset + "  " + FormatRangeDisplay(min, max));
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    private static void DrawPresetButtons(Rect rect, Action<UiCommand> emit)
    {
        float buttonWidth = (rect.width - Gap * 2f) / 3f;
        DrawPresetButton(new Rect(rect.x, rect.y, buttonWidth, rect.height), "Conservative",
            () => emit(new UiCommand(UiCommandKind.SetDistancePreset, arg: SqueakDistancePreset.Conservative.ToString())));
        DrawPresetButton(new Rect(rect.x + buttonWidth + Gap, rect.y, buttonWidth, rect.height), "Balanced",
            () => emit(new UiCommand(UiCommandKind.SetDistancePreset, arg: SqueakDistancePreset.Balanced.ToString())));
        DrawPresetButton(new Rect(rect.x + (buttonWidth + Gap) * 2f, rect.y, buttonWidth, rect.height), "Strong",
            () => emit(new UiCommand(UiCommandKind.SetDistancePreset, arg: SqueakDistancePreset.Strong.ToString())));
    }

    private static void DrawPresetButton(Rect rect, string label, Action onClick)
    {
        UsSurface.DrawSegment(rect, label, false);
        UiInteract.Button(rect, UiLayer.Content, onClick);
    }

    private static void DrawNarrowSummary(Rect rect, WidgetContext ctx)
    {
        string preset = ReadString(ctx, "DistancePreset", "Custom");
        float min = ReadFloat(ctx, "DistanceRangeMin", MinDistance);
        float max = ReadFloat(ctx, "DistanceRangeMax", 50f);
        SanitizeRange(ref min, ref max);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextSecondary;
        Widgets.Label(rect, "Attenuation " + preset + "  " + FormatRangeDisplay(min, max));
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    private static string FormatRange(float start, float end)
    {
        return AttenuationMath.FormatRange(start, end);
    }

    private static string FormatRangeDisplay(float start, float end)
    {
        return AttenuationMath.FormatRangeDisplay(start, end);
    }

    private static void SanitizeRange(ref float min, ref float max)
    {
        AttenuationMath.SanitizeRange(ref min, ref max);
    }

    private static float ReadFloat(WidgetContext ctx, string key, float fallback)
    {
        return ctx.TryGetViewValue(key, out object? value) && value is float f ? f : fallback;
    }

    private static string ReadString(WidgetContext ctx, string key, string fallback)
    {
        return ctx.TryGetViewValue(key, out object? value) && value is string s ? s : fallback;
    }
}
