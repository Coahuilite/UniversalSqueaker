using System;
using System.Globalization;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// S4-Vol global volume slider. Reads the view state key "GlobalVolumeFactor" (0..1) and emits
/// <see cref="UiCommandKind.SetGlobalVolume"/> with the new normalized value on change.
/// </summary>
public sealed class GlobalVolumeWidget : IWidget
{
    public const string Kind = "us/global-volume";

    private const float RowHeight = 44f;
    private const float LeftPadding = 10f;
    private const float RightPadding = 10f;
    private const float LabelHeight = 20f;
    private const float SliderHeight = 20f;

    private const string ViewKey = "GlobalVolumeFactor";
    private const string Label = "Global volume";

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        float height = RowHeight;
        string helpKey = UsHelp.ResolveKey(_spec);
        if (UsHelp.IsOpen(ctx, helpKey))
        {
            height += UsHelp.BannerHeight(ctx, helpKey, VoicePacksLayout.InnerWidth(ctx.ViewWidth)) + VoicePacksLayout.Gap;
        }
        return UiGuard.MeasureOrFallback(() => height, height, Kind);
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
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        float value = ReadValue(ctx);
        float y = rect.y;

        Rect helpRect = new(rect.xMax - 22f, rect.y, 22f, 22f);
        UsHelp.DrawHelpButton(helpRect, helpKey, ctx, emit);

        UsSurface.DrawSurface(rect, UsSurface.SurfaceKind.Panel);
        if (UsHelp.IsOpen(ctx, helpKey))
        {
            float helpHeight = UsHelp.BannerHeight(ctx, helpKey, rect.width - LeftPadding - RightPadding);
            if (helpHeight > 0f)
            {
                UsHelp.DrawBanner(new Rect(rect.x + LeftPadding, y, rect.width - LeftPadding - RightPadding, helpHeight), helpKey, ctx);
                y += helpHeight + VoicePacksLayout.Gap;
            }
        }

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + LeftPadding, y + 4f, Math.Max(1f, rect.width - 80f), LabelHeight), Label);

        var id = new UiControlId(Kind, "global-volume");
        Rect fieldRect = new(rect.xMax - RightPadding - 64f, y + 2f, 64f, LabelHeight);
        Rect sliderRect = new(rect.x + LeftPadding, y + LabelHeight + 2f, rect.width - LeftPadding - RightPadding, SliderHeight);

        float sliderValue = UiInteract.Slider(sliderRect, id, value, 0f, 1f, out bool sliderChanged);
        UiInteract.NumberField(fieldRect, id, sliderValue, 0f, 1f, "0%", out bool committed);

        if (sliderChanged || committed)
        {
            float current = UiValueStore.GetOrCreate(id).FloatValue;
            businessEmit(new UiCommand(UiCommandKind.SetGlobalVolume, arg: current.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    private static void DrawVanilla(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        float value = ReadValue(ctx);
        Widgets.Label(new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, 18f), Label + "  " + Mathf.RoundToInt(value * 100f) + "%");
        float next = Widgets.HorizontalSlider(new Rect(rect.x + 6f, rect.y + 22f, rect.width - 12f, 18f), value, 0f, 1f, middleAlignment: true);
        if (Math.Abs(next - value) > 0.0001f)
        {
            businessEmit(new UiCommand(UiCommandKind.SetGlobalVolume, arg: next.ToString("0.###", CultureInfo.InvariantCulture)));
        }
    }

    private static float ReadValue(WidgetContext ctx)
    {
        return ctx.TryGetViewValue(ViewKey, out object? value) && value is float f ? f : 1f;
    }
}
