using System;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// S4 diagnostics foundation: player-facing camera indicator toggle under the Ferrite path.
/// One row matching the basic-tuning rows: reads the view state key "ShowCameraIndicator"
/// and emits a business <see cref="UiCommand"/> (UiCommandKind.ToggleBasic, arg "CameraIndicator")
/// through <see cref="UsWidgetCommandAdapter.For"/>.
/// </summary>
public sealed class CameraIndicatorWidget : IWidget
{
    public const string Kind = "us/camera-indicator";

    private const float RowHeight = 28f;
    private const float LeftPadding = 10f;

    private const string Label = "Show camera indicator";
    private const string ViewKey = "ShowCameraIndicator";
    private const string ToggleArg = "CameraIndicator";

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

        bool enabled = ctx.TryGetViewValue(ViewKey, out object? value) && value is true;
        bool hovered = Mouse.IsOver(rect);
        Widgets.DrawBoxSolid(rect, hovered ? new Color(.16f, .145f, .12f, .94f) : UiPalette.Panel);
        SectionFrame.DrawBorder(rect);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 4f, rect.width - 60f, 20f), Label);

        Rect checkRect = new(rect.xMax - 40f, rect.y + 4f, 20f, 20f);
        bool checkboxValue = enabled;
        Widgets.Checkbox(checkRect.position, ref checkboxValue, 20f);
        GUI.color = oldColor;
        Text.Font = oldFont;

        if (Widgets.ButtonInvisible(rect))
            businessEmit(new UiCommand(UiCommandKind.ToggleBasic, arg: ToggleArg, flag: !enabled));
    }
}
