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
        float y = rect.y;

        Rect helpRect = new(rect.xMax - 22f, rect.y, 22f, 22f);
        UsHelp.DrawHelpButton(helpRect, helpKey, ctx, emit);

        bool enabled = ctx.TryGetViewValue(ViewKey, out object? value) && value is true;
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);
        if (UsHelp.IsOpen(ctx, helpKey))
        {
            float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
            float helpHeight = UsHelp.BannerHeight(ctx, helpKey, innerWidth);
            if (helpHeight > 0f)
            {
                UsHelp.DrawBanner(new Rect(rect.x + LeftPadding, y, innerWidth, helpHeight), helpKey, ctx);
                y += helpHeight + VoicePacksLayout.Gap;
            }
        }

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + LeftPadding, y + 4f, Math.Max(1f, rect.width - 60f), 20f), Label);

        Rect checkRect = new(rect.xMax - 34f, y + 4f, 18f, 18f);
        UsSurface.DrawCheckbox(checkRect, enabled);
        GUI.color = oldColor;
        Text.Font = oldFont;

        UiInteract.Row(rect, () => businessEmit(new UiCommand(UiCommandKind.ToggleBasic, arg: ToggleArg, flag: !enabled)));
    }

    private static void DrawVanilla(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        bool enabled = ctx.TryGetViewValue(ViewKey, out object? value) && value is true;
        bool checkboxValue = enabled;
        Widgets.Checkbox(new Vector2(rect.xMax - 34f, rect.y + 4f), ref checkboxValue, 18f);
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 4f, rect.width - 60f, 20f), Label);
        if (Widgets.ButtonInvisible(rect))
            businessEmit(new UiCommand(UiCommandKind.ToggleBasic, arg: ToggleArg, flag: !enabled));
    }
}
