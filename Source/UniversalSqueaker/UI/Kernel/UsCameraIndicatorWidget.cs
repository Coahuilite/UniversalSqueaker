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

    private const float RowHeight = 28f;

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
        return RowHeight;
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        return RowHeight;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        bool enabled = ctx.Bindings.TryGet("camera-indicator", out bool value) && value;
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y, Math.Max(1f, rect.width - 60f), rect.height),
            "Show camera indicator",
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Checkbox(new Rect(rect.xMax - 34f, rect.y + (rect.height - 18f) * 0.5f, 18f, 18f), ctx.Theme, enabled);

        if (UiNative.Button(rect))
        {
            ctx.Bindings.Invoke("toggle-camera-indicator", !enabled);
        }
    }
}
