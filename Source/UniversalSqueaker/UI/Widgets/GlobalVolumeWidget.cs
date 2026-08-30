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

    private const string Title = "Global volume";
    private const float RowHeight = 44f;
    private const float LeftPadding = 10f;
    private const float RightPadding = 10f;
    private const float LabelHeight = 20f;
    private const float SliderHeight = 20f;

    private const string ViewKey = "GlobalVolumeFactor";

    private const string SliderHelpKey = "us/global-volume/slider";
    private const string FieldHelpKey = "us/global-volume/number";

    private UiElementSpec? _spec;

    private bool HideBodyLabel => UsCard.IsTrueAttribute(_spec, "HideBodyLabel");

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        return UiGuard.MeasureOrFallback(
            () => UsCard.Measure(RowHeight, ctx),
            UsCard.Measure(RowHeight, ctx),
            Kind, "UniversalSqueaker");
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit, HideBodyLabel),
            fallback => DrawVanilla(fallback, ctx, emit),
            Kind, "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit, bool hideBodyLabel)
    {
        UsCard.Draw(rect, Title, ctx, body => DrawBody(body, ctx, emit, hideBodyLabel));
    }

    private static void DrawBody(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit, bool hideBodyLabel)
    {
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        float value = ReadValue(ctx);

        var id = new UiControlId(Kind, "global-volume");
        Rect fieldRect = new(rect.xMax - RightPadding - 64f, rect.y + 2f, 64f, LabelHeight);
        Rect sliderRect = new(rect.x + LeftPadding, rect.y + LabelHeight + 2f, rect.width - LeftPadding - RightPadding, SliderHeight);

        if (hideBodyLabel)
        {
            // Hide the duplicate in-body label; vertically center the slider and number field.
            float controlHeight = Math.Max(SliderHeight, LabelHeight);
            float y = rect.y + Math.Max(0f, (rect.height - controlHeight) * 0.5f);
            fieldRect.y = y;
            sliderRect.y = y + (controlHeight - SliderHeight) * 0.5f;
            sliderRect.height = SliderHeight;
        }

        float sliderValue = UiInteract.Slider(sliderRect, id, value, 0f, 1f, out bool sliderChanged);
        UiInteract.NumberField(fieldRect, id, sliderValue, 0f, 1f, "0%", out bool committed);

        UsHelpHighlight.DrawFor(sliderRect, SliderHelpKey, ctx.State);
        UsHelpHighlight.DrawFor(fieldRect, FieldHelpKey, ctx.State);

        if (sliderChanged || committed)
        {
            float current = UiValueStore.GetOrCreate(id).FloatValue;
            businessEmit(new UiCommand(UiCommandKind.SetGlobalVolume, arg: current.ToString("0.###", CultureInfo.InvariantCulture)));
        }
    }

    private static void DrawVanilla(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        float value = ReadValue(ctx);
        Widgets.Label(new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, 18f), Title + "  " + Mathf.RoundToInt(value * 100f) + "%");
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
