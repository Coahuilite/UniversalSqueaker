using System;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US camera-indicator section: one toggle row writing through the typed
/// "toggle-camera-indicator" bool action.
/// </summary>
public sealed class UsCameraIndicatorWidget : UsSectionWidgetBase
{
    public const string KindName = "us/camera-indicator";

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsCameraIndicatorWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<bool>("camera-indicator", elementPath);
        bindings.ValidateAction<bool>("toggle-camera-indicator", elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return UsKernelDraw.RowVisualHeight(ctx);
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        // One data row: its height is the theme's density axis (24 regular / 20 dense, spec 1.4).
        return UsKernelDraw.RowVisualHeight(ctx);
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        bool enabled = ctx.Bindings.TryGet("camera-indicator", out bool value) && value;
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/camera-indicator/toggle");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, UsKernelDraw.RowRail.None);

        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y, Math.Max(1f, rect.width - 60f), rect.height),
            ctx.Translation.Translate("US.Tuning.CameraIndicator"),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        Rect checkbox = UsKernelDraw.CheckboxSlot(rect);
        bool toggled = UsKernelDraw.Checkbox(checkbox, ctx, enabled);

        Rect rowHit = UsKernelDraw.RowHitRect(rect, ctx);
        rowHit.width = Math.Max(1f, checkbox.x - rowHit.x);
        if (toggled || UiNative.Button(rowHit, ctx))
        {
            ctx.Bindings.Invoke("toggle-camera-indicator", !enabled);
        }
    }
}
