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
        return RowHeight;
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        float value = ReadValue(ctx);

        Widgets.DrawBoxSolid(rect, UiPalette.Panel);
        SectionFrame.DrawBorder(rect);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 4f, rect.width - 70f, LabelHeight), Label);

        Text.Font = GameFont.Tiny;
        GUI.color = new Color(.82f, .80f, .74f, .92f);
        Widgets.Label(new Rect(rect.xMax - 60f, rect.y + 4f, 50f, LabelHeight), Mathf.RoundToInt(value * 100f) + "%");
        Text.Font = oldFont;
        GUI.color = oldColor;

        Rect sliderRect = new(rect.x + LeftPadding, rect.y + LabelHeight + 2f, rect.width - LeftPadding - RightPadding, SliderHeight);
        float next = Widgets.HorizontalSlider(sliderRect, value, 0f, 1f, middleAlignment: true);
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
