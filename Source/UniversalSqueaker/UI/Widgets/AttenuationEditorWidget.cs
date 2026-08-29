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

    private const float MinDistance = AttenuationMath.MinDistance;
    private const float MaxDistance = AttenuationMath.MaxDistance;
    private const float MinRange = AttenuationMath.MinRange;

    private const int DragNone = 0;
    private const int DragStart = 1;
    private const int DragEnd = 2;

    private static readonly Dictionary<UiControlId, AttenuationDragState> DragStates = new();

    internal static void ResetSession()
    {
        DragStates.Clear();
    }

    private static AttenuationDragState GetOrCreateDragState()
    {
        var id = new UiControlId(Kind, "drag");
        if (!DragStates.TryGetValue(id, out AttenuationDragState? state))
        {
            state = new AttenuationDragState();
            DragStates[id] = state;
        }

        return state;
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
        float height = ctx.ViewWidth < MinWidth ? NarrowHeight
            : TopPadding + ChartHeight + Gap + StatusHeight + Gap + ButtonsHeight + BottomPadding;
        string helpKey = UsHelp.ResolveKey(_spec);
        if (UsHelp.IsOpen(ctx, helpKey))
        {
            height += UsHelp.BannerHeight(ctx, helpKey, VoicePacksLayout.InnerWidth(ctx.ViewWidth)) + VoicePacksLayout.Gap;
        }
        return height;
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        string helpKey = UsHelp.ResolveKey(_spec);
        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit, helpKey),
            fallback => DrawVanilla(fallback, ctx, emit),
            Kind);
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit, string helpKey)
    {
        Rect helpRect = new(rect.xMax - 22f, rect.y, 22f, 22f);
        UsHelp.DrawHelpButton(helpRect, helpKey, ctx, emit);

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

        AttenuationDragState drag = GetOrCreateDragState();
        if (drag.Mode != DragNone)
        {
            min = drag.DragMin;
            max = drag.DragMax;
        }

        float innerWidth = Math.Max(1f, rect.width - LeftPadding - RightPadding);
        float x = rect.x + LeftPadding;
        float y = rect.y + TopPadding;

        if (UsHelp.IsOpen(ctx, helpKey))
        {
            float helpHeight = UsHelp.BannerHeight(ctx, helpKey, innerWidth);
            if (helpHeight > 0f)
            {
                UsHelp.DrawBanner(new Rect(x, y, innerWidth, helpHeight), helpKey, ctx);
                y += helpHeight + VoicePacksLayout.Gap;
            }
        }

        Rect chartRect = new(x, y, innerWidth, ChartHeight);
        UiInteract.Protect(chartRect);
        HandleDrag(chartRect, ref min, ref max, drag, businessEmit);
        DrawChart(chartRect, min, max);
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

    private static void DrawChart(Rect chartRect, float min, float max)
    {
        UsSurface.DrawSurface(chartRect, UsSurface.SurfaceKind.Base);

        Color curveColor = UsVisualTokens.AccentGold;
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
        Widgets.DrawLine(new Vector2(startX, chartRect.yMax), new Vector2(endX, chartRect.y), UsVisualTokens.TextPrimary, 1f);
        DrawHandle(new Vector2(startX, chartRect.y));
        DrawHandle(new Vector2(endX, chartRect.yMax));
    }

    private static void DrawHandle(Vector2 position)
    {
        Rect handleRect = new(position.x - 4f, position.y - 4f, 8f, 8f);
        Widgets.DrawBoxSolid(handleRect, UsVisualTokens.TextPrimary);
        UsSurface.DrawBorder(handleRect);
    }

    private static void HandleDrag(Rect chartRect, ref float min, ref float max, AttenuationDragState drag, Action<UiCommand> emit)
    {
        if (Event.current.type == EventType.MouseDown)
        {
            Vector2 startPosition = new(ChartX(chartRect, min), chartRect.y);
            Vector2 endPosition = new(ChartX(chartRect, max), chartRect.yMax);
            Rect startHandle = new(startPosition.x - 5f, startPosition.y - 5f, 10f, 10f);
            Rect endHandle = new(endPosition.x - 5f, endPosition.y - 5f, 10f, 10f);

            if (Mouse.IsOver(startHandle))
            {
                drag.Mode = DragStart;
                drag.DragMin = min;
                drag.DragMax = max;
                drag.OriginalMin = min;
                drag.OriginalMax = max;
                Event.current.Use();
            }
            else if (Mouse.IsOver(endHandle))
            {
                drag.Mode = DragEnd;
                drag.DragMin = min;
                drag.DragMax = max;
                drag.OriginalMin = min;
                drag.OriginalMax = max;
                Event.current.Use();
            }
        }

        if (drag.Mode == DragNone) return;

        if (Event.current.type == EventType.MouseUp)
        {
            bool changed = Math.Abs(drag.DragMin - drag.OriginalMin) > 0.001f || Math.Abs(drag.DragMax - drag.OriginalMax) > 0.001f;
            if (changed)
            {
                emit(new UiCommand(UiCommandKind.SetDistanceRange, arg: FormatRange(drag.DragMin, drag.DragMax)));
            }

            drag.Mode = DragNone;
            Event.current.Use();
            return;
        }

        if (Event.current.type == EventType.MouseDrag)
        {
            float distance = DistanceFromX(chartRect, Event.current.mousePosition.x);
            if (drag.Mode == DragStart)
            {
                drag.DragMin = Mathf.Clamp(distance, MinDistance, max - MinRange);
            }
            else
            {
                drag.DragMax = Mathf.Clamp(distance, min + MinRange, MaxDistance);
            }

            min = drag.DragMin;
            max = drag.DragMax;
            Event.current.Use();
        }
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
        UiInteract.Button(new Rect(rect.x, rect.y, buttonWidth, rect.height), UiLayer.Content,
            () => emit(new UiCommand(UiCommandKind.SetDistancePreset, arg: SqueakDistancePreset.Conservative.ToString())));

        UiInteract.Button(new Rect(rect.x + buttonWidth + Gap, rect.y, buttonWidth, rect.height), UiLayer.Content,
            () => emit(new UiCommand(UiCommandKind.SetDistancePreset, arg: SqueakDistancePreset.Balanced.ToString())));

        UiInteract.Button(new Rect(rect.x + (buttonWidth + Gap) * 2f, rect.y, buttonWidth, rect.height), UiLayer.Content,
            () => emit(new UiCommand(UiCommandKind.SetDistancePreset, arg: SqueakDistancePreset.Strong.ToString())));
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

    private static float ChartX(Rect chartRect, float distance)
    {
        return AttenuationMath.ChartX(chartRect.x, chartRect.width, distance);
    }

    private static float DistanceFromX(Rect chartRect, float mouseX)
    {
        return AttenuationMath.DistanceFromX(chartRect.x, chartRect.width, mouseX);
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

    private sealed class AttenuationDragState
    {
        internal int Mode;
        internal float DragMin = MinDistance;
        internal float DragMax = 50f;
        internal float OriginalMin = MinDistance;
        internal float OriginalMax = 50f;
    }
}
