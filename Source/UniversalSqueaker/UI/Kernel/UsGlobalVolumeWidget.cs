using System;
using UnityEngine;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US global-volume section: a 0..1 slider plus a percent number field, both writing
/// through the typed float "global-volume" binding (which routes to the business volume setter).
/// Transient focus/edit state lives in the session value state for element "global-volume".
/// </summary>
public sealed class UsGlobalVolumeWidget : UsSectionWidgetBase
{
    public const string KindName = "us/global-volume";

    private const float RowHeight = 44f;
    private const float LabelHeight = 20f;
    private const float SliderHeight = 20f;
    private const float FieldWidth = 64f;
    private const float RightPadding = 10f;

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsGlobalVolumeWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<float>("global-volume", elementPath);
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
        float value = ctx.Bindings.TryGet("global-volume", out float bound) ? bound : 1f;
        string elementId = "global-volume";

        Rect fieldRect = new(rect.xMax - RightPadding - FieldWidth, rect.y + 2f, FieldWidth, LabelHeight);
        Rect sliderRect = new(rect.x, rect.y + LabelHeight + 2f, rect.width - RightPadding, SliderHeight);

        float sliderValue = UiNative.Slider(sliderRect, elementId, ctx.Session, value, 0f, 1f, out bool sliderChanged);
        if (sliderChanged)
        {
            ctx.Bindings.Set("global-volume", sliderValue);
        }

        // Percent number field: edit in 0..100 points, commit normalized 0..1.
        UiValueState state = ctx.Session.GetOrCreateValueState(elementId);
        UiNative.NumberField(fieldRect, elementId, ctx.Session, value * 100f, 0f, 100f, "0", out bool committed);
        if (committed)
        {
            float normalized = state.FloatValue / 100f;
            ctx.Bindings.Set("global-volume", UiNative.ClampValue(normalized, 0f, 1f));
        }

        string display = Mathf.RoundToInt(value * 100f) + "%";
        UsKernelDraw.Label(
            new Rect(rect.x, rect.y + 4f, Math.Max(1f, fieldRect.x - rect.x - 8f), 16f),
            "Global volume: " + display,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
    }
}
