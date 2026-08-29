using System;
using System.Collections.Generic;
using System.Globalization;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// S4-Vol camera-height attenuation editor. Draws a 15..65 by 0..100 chart with two horizontally
/// draggable endpoints (start locked at 100%, end locked at 0%), three quick presets, and a current
/// preset label. Drag release emits <see cref="UiCommandKind.SetDistanceRange"/>; preset buttons emit
/// <see cref="UiCommandKind.SetDistancePreset"/>.
/// </summary>
public sealed class AttenuationEditorWidget : IWidget
{
    public const string Kind = "us/attenuation-editor";

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

    private const float MinDistance = 15f;
    private const float MaxDistance = 65f;
    private const float MinRange = 5f;

    private const int DragNone = 0;
    private const int DragStart = 1;
    private const int DragEnd = 2;

    private static int _dragging;
    private static float _dragMin = MinDistance;
    private static float _dragMax = 50f;
    private static float _originalMin = MinDistance;
    private static float _originalMax = 50f;

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (ctx.ViewWidth < MinWidth) return NarrowHeight;
        return TopPadding + ChartHeight + Gap + StatusHeight + Gap + ButtonsHeight + BottomPadding;
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

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

        if (_dragging != DragNone)
        {
            min = _dragMin;
            max = _dragMax;
        }

        float innerWidth = Math.Max(1f, rect.width - LeftPadding - RightPadding);
        float x = rect.x + LeftPadding;
        float y = rect.y + TopPadding;

        Rect chartRect = new(x, y, innerWidth, ChartHeight);
        HandleDrag(chartRect, ref min, ref max, businessEmit);
        DrawChart(chartRect, min, max);
        y += ChartHeight + Gap;

        DrawStatus(new Rect(x, y, innerWidth, StatusHeight), preset, min, max);
        y += StatusHeight + Gap;

        DrawPresetButtons(new Rect(x, y, innerWidth, ButtonsHeight), businessEmit);
    }

    private static void DrawChart(Rect chartRect, float min, float max)
    {
        Widgets.DrawBoxSolid(chartRect, new Color(0.10f, 0.10f, 0.10f, 0.55f));
        SectionFrame.DrawBorder(chartRect);

        Color curveColor = new(0.92f, 0.68f, 0.30f, 0.85f);
        IReadOnlyList<DistanceSample> samples = DistancePreview.SampleAudibilityCurve(min, max, 96, MinDistance, MaxDistance);
        float barWidth = Math.Max(1f, chartRect.width / samples.Count);
        for (int i = 0; i < samples.Count; i++)
        {
            float x = ChartX(chartRect, samples[i].Distance);
            float audibility = Mathf.Clamp01(samples[i].Audibility);
            float top = chartRect.yMax - audibility * chartRect.height;
            Widgets.DrawBoxSolid(new Rect(x, top, barWidth, Math.Max(1f, chartRect.yMax - top)), curveColor);
        }

        float startX = ChartX(chartRect, min);
        float endX = ChartX(chartRect, max);
        Widgets.DrawLine(new Vector2(startX, chartRect.yMax), new Vector2(endX, chartRect.y), Color.white, 1f);
        DrawHandle(new Vector2(startX, chartRect.y));
        DrawHandle(new Vector2(endX, chartRect.yMax));
    }

    private static void DrawHandle(Vector2 position)
    {
        Rect handleRect = new(position.x - 4f, position.y - 4f, 8f, 8f);
        Widgets.DrawBoxSolid(handleRect, Color.white);
        SectionFrame.DrawBorder(handleRect);
    }

    private static void HandleDrag(Rect chartRect, ref float min, ref float max, Action<UiCommand> emit)
    {
        if (Event.current.type == EventType.MouseDown)
        {
            Vector2 startPosition = new(ChartX(chartRect, min), chartRect.y);
            Vector2 endPosition = new(ChartX(chartRect, max), chartRect.yMax);
            Rect startHandle = new(startPosition.x - 5f, startPosition.y - 5f, 10f, 10f);
            Rect endHandle = new(endPosition.x - 5f, endPosition.y - 5f, 10f, 10f);

            if (Mouse.IsOver(startHandle))
            {
                _dragging = DragStart;
                _dragMin = min;
                _dragMax = max;
                _originalMin = min;
                _originalMax = max;
                Event.current.Use();
            }
            else if (Mouse.IsOver(endHandle))
            {
                _dragging = DragEnd;
                _dragMin = min;
                _dragMax = max;
                _originalMin = min;
                _originalMax = max;
                Event.current.Use();
            }
        }

        if (_dragging == DragNone) return;

        if (Event.current.type == EventType.MouseUp)
        {
            bool changed = Math.Abs(_dragMin - _originalMin) > 0.001f || Math.Abs(_dragMax - _originalMax) > 0.001f;
            if (changed)
            {
                emit(new UiCommand(UiCommandKind.SetDistanceRange, arg: FormatRange(_dragMin, _dragMax)));
            }

            _dragging = DragNone;
            Event.current.Use();
            return;
        }

        if (Event.current.type == EventType.MouseDrag)
        {
            float distance = DistanceFromX(chartRect, Event.current.mousePosition.x);
            if (_dragging == DragStart)
            {
                _dragMin = Mathf.Clamp(distance, MinDistance, max - MinRange);
            }
            else
            {
                _dragMax = Mathf.Clamp(distance, min + MinRange, MaxDistance);
            }

            min = _dragMin;
            max = _dragMax;
            Event.current.Use();
        }
    }

    private static void DrawStatus(Rect rect, string preset, float min, float max)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GUI.color = new Color(.82f, .80f, .74f, .92f);
        Widgets.Label(rect, preset + "  " + FormatRangeDisplay(min, max));
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    private static void DrawPresetButtons(Rect rect, Action<UiCommand> emit)
    {
        float buttonWidth = (rect.width - Gap * 2f) / 3f;
        if (Widgets.ButtonText(new Rect(rect.x, rect.y, buttonWidth, rect.height), "Conservative"))
        {
            emit(new UiCommand(UiCommandKind.SetDistancePreset, arg: SqueakDistancePreset.Conservative.ToString()));
        }

        if (Widgets.ButtonText(new Rect(rect.x + buttonWidth + Gap, rect.y, buttonWidth, rect.height), "Balanced"))
        {
            emit(new UiCommand(UiCommandKind.SetDistancePreset, arg: SqueakDistancePreset.Balanced.ToString()));
        }

        if (Widgets.ButtonText(new Rect(rect.x + (buttonWidth + Gap) * 2f, rect.y, buttonWidth, rect.height), "Strong"))
        {
            emit(new UiCommand(UiCommandKind.SetDistancePreset, arg: SqueakDistancePreset.Strong.ToString()));
        }
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
        GUI.color = new Color(.82f, .80f, .74f, .92f);
        Widgets.Label(rect, "Attenuation " + preset + "  " + FormatRangeDisplay(min, max));
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    private static float ChartX(Rect chartRect, float distance)
    {
        float t = Mathf.InverseLerp(MinDistance, MaxDistance, distance);
        return chartRect.x + Mathf.Clamp01(t) * chartRect.width;
    }

    private static float DistanceFromX(Rect chartRect, float mouseX)
    {
        float t = Mathf.InverseLerp(chartRect.x, chartRect.xMax, mouseX);
        return Mathf.Lerp(MinDistance, MaxDistance, Mathf.Clamp01(t));
    }

    private static string FormatRange(float start, float end)
    {
        return start.ToString("0.###", CultureInfo.InvariantCulture) + "|" + end.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatRangeDisplay(float start, float end)
    {
        return start.ToString("0.#", CultureInfo.InvariantCulture) + "–" + end.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static void SanitizeRange(ref float min, ref float max)
    {
        if (float.IsNaN(min) || float.IsInfinity(min)) min = MinDistance;
        if (float.IsNaN(max) || float.IsInfinity(max)) max = 50f;
        min = Mathf.Clamp(min, MinDistance, MaxDistance - MinRange);
        max = Mathf.Clamp(max, MinDistance + MinRange, MaxDistance);
        if (max < min + MinRange) max = Mathf.Min(MaxDistance, min + MinRange);
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
