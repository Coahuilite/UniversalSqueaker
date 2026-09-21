using System;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US global-volume section: a 0..1 slider plus a percent number field, both writing
/// through the typed float "global-volume" binding (which routes to the business volume setter).
/// Transient focus/edit state lives in the session value state for element "global-volume".
///
/// <para>
/// The caption's band is MEASURED, not written down. It was a hard-coded 18px (the P1-era shape), and the
/// maintainer's 2026-09-21 in-game logs show what that produces: <c>ui.text.overflow tiny need=33 have=18
/// @ .../global-volume</c>, replicated at both walked resolutions - the last real overflow left on the page.
/// The string itself is short, so the defect was never the width: it is the BAND, and a bigger constant
/// would only move the threshold to the next language or font size (R14 forbids the written threshold).
/// The band therefore comes from the injected metrics, exactly like the page title's caption band and the
/// timing interval's caption, and the ROW GROWS with it - a measured band inside a fixed 44px row would
/// simply paint over the slider instead.
/// </para>
/// <para>
/// The sample is the widest value the caption can render (100%), so the band does not move when the player
/// drags the slider. Measuring the CURRENT text would make the card's height depend on the value, which is
/// the measure/draw mismatch class this project keeps paying for. Measure and Draw both call
/// <see cref="LabelBandFor"/> against that one sample, so the two halves cannot size different bands.
/// </para>
/// </summary>
public sealed class UsGlobalVolumeWidget : UsSectionWidgetBase
{
    public const string KindName = "us/global-volume";

    private const float RowHeight = 44f;
    private const float LabelHeight = 20f;
    private const float SliderHeight = 20f;
    private const float FieldWidth = 64f;
    private const float RightPadding = 10f;

    /// <summary>Top of the caption inside the row: the drawn offset this widget has always used.</summary>
    private const float LabelTop = 3f;

    /// <summary>Floor of the caption band. What the band used to BE is now only a floor, so a one-line
    /// caption keeps exactly the shipped row.</summary>
    private const float LabelFloor = 18f;

    /// <summary>The gap the caption keeps from the field it sits beside.</summary>
    private const float LabelFieldGap = 8f;

    /// <summary>Space between the caption band and the slider: 3 + 18 + 1 = the shipped slider top (22).</summary>
    private const float SliderGap = 1f;

    /// <summary>What the row keeps below the slider: 22 + 20 + 2 = the shipped row height (44).</summary>
    private const float RowBottomPadding = 2f;

    /// <summary>The value the band is measured against: the widest the caption can render.</summary>
    private const string WidestSampleValue = "100%";

    private const string CaptionKey = "US.Tuning.GlobalVolume";

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
        return BodyHeightFor(ctx, BodyWidth(ctx));
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        return BodyHeightFor(ctx, BodyWidth(ctx));
    }

    /// <summary>
    /// The row's height: the shipped floor grown by whatever the caption needs. Measure and Draw share it,
    /// so the card can never allocate one band and paint another.
    /// </summary>
    private static float BodyHeightFor(UiWidgetContext ctx, float bodyWidth)
    {
        return Mathf.Max(
            RowHeight,
            SliderTopFor(LabelBandFor(ctx, bodyWidth)) + SliderHeight + RowBottomPadding);
    }

    /// <summary>
    /// Where the slider starts: below the caption band plus its gap, floored at the shipped one-line row.
    /// A band taller than the floor pushes the slider down instead of letting the caption paint over it.
    /// </summary>
    private static float SliderTopFor(float labelBand)
    {
        return Mathf.Max(LabelHeight + 2f, LabelTop + labelBand + SliderGap);
    }

    /// <summary>The caption's width: everything left of the field, minus the gap it keeps from it.</summary>
    private static float LabelWidthFor(float bodyWidth)
    {
        return Mathf.Max(1f, bodyWidth - RightPadding - FieldWidth - LabelFieldGap);
    }

    /// <summary>
    /// The caption band, measured against the WIDEST value so it cannot move with the slider. Both halves of
    /// the frame call this one method.
    /// </summary>
    private static float LabelBandFor(UiWidgetContext ctx, float bodyWidth)
    {
        string sample = string.Format(ctx.Translation.Translate(CaptionKey), WidestSampleValue);
        return UsKernelDraw.TextBandHeight(LabelWidthFor(bodyWidth), ctx, sample, UiFont.Tiny, LabelFloor);
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        float value = ctx.Bindings.TryGet("global-volume", out float bound) ? bound : 1f;
        string elementId = "global-volume";

        // Same band and same row arithmetic as Measure (DrawBody hands this method exactly BodyWidth).
        float labelBand = LabelBandFor(ctx, rect.width);
        float sliderTop = SliderTopFor(labelBand);

        Rect fieldRect = new(rect.xMax - RightPadding - FieldWidth, rect.y + 2f, FieldWidth, LabelHeight);
        Rect sliderRect = new(rect.x, rect.y + sliderTop, Math.Max(1f, rect.width - RightPadding), SliderHeight);

        UsKernelDraw.HelpHover(sliderRect, ctx, "us/global-volume/slider");
        UsKernelDraw.HelpHover(fieldRect, ctx, "us/global-volume/number");

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
            new Rect(rect.x, rect.y + LabelTop, LabelWidthFor(rect.width), labelBand),
            string.Format(ctx.Translation.Translate(CaptionKey), display),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
    }
}
