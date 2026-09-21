using System;
using System.Globalization;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US trigger-timing section: the global interval floor (slider in ticks plus a
/// seconds number field, both writing the typed int "min-interval" binding) and the global
/// cooldown multiplier (minus/plus buttons plus a number field on the typed float
/// "cooldown-multiplier" binding). Both writes are cheap runtime statics; the Host binding
/// owns the display-revision bump.
/// <para>
/// The multiplier row's label band follows the measured wrap height (see
/// <see cref="MultiplierRowHeight"/>): a language whose label needs two lines grows the card rather
/// than having the second line clipped away inside a fixed band.
/// </para>
/// </summary>
public sealed class UsTimingWidget : UsSectionWidgetBase
{
    public const string KindName = "us/timing";

    // Row geometry follows the global-volume card: a Tiny label band over a slider row with a
    // right-aligned number field. The multiplier row is one Small line with stepper buttons; its label
    // band grows to the measured wrap height when the shipped string needs two lines.
    private const float IntervalRowHeight = 44f;
    private const float IntervalCaptionFloor = 18f;
    private const float IntervalTopPadding = 3f;
    private const float IntervalSliderGap = 2f;
    private const float IntervalBottomPadding = 1f;
    private const float LabelHeight = 20f;
    private const float SliderHeight = 20f;
    private const float FieldWidth = 64f;
    private const float ButtonWidth = 26f;
    private const float RowGap = 4f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;

    /// <summary>Gap the caption leaves before the seconds field, and the stepper cluster's own gaps.</summary>
    private const float LabelGap = 8f;
    private const float StepperGap = 4f;

    /// <summary>
    /// Sample caption used to size the interval caption band: the longest text the DRAW can produce at the
    /// widest value, so the band's height never depends on the live value AND never under-reserves it. It
    /// carries the unit suffix the draw appends - a bare "10.0" was one line while "10.0 s" was two at some
    /// content widths, and the fit audit reported it as <c>timing Height needs 36 has 18</c> once the
    /// narrow-screen help band took height off the body row and the centre column lost its scrollbar (so it
    /// was 16px wider). Measure and Draw must format the SAME string, which is why the sample is written the
    /// way the draw builds it rather than as the field's own format.
    /// </summary>
    private const string IntervalCaptionSample = "10.0 s";

    /// <summary>The slider covers 1..600 game ticks (10 s at 60 ticks/s); the field edits seconds.</summary>
    private const int MinIntervalTicksFloor = 1;
    private const int MinIntervalTicksCeil = 600;
    private const float MinIntervalSecondsFloor = 0.1f;
    private const float MinIntervalSecondsCeil = 10f;

    private const float MultiplierFloor = 0f;
    private const float MultiplierCeil = 3f;
    private const float MultiplierStep = 0.1f;

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsTimingWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<int>("min-interval", elementPath);
        bindings.ValidateValue<float>("cooldown-multiplier", elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return ContentHeight(ctx);
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        return ContentHeight(ctx);
    }

    private float ContentHeight(UiWidgetContext ctx)
    {
        return TopPadding + IntervalRowHeightFor(ctx, BodyWidth(ctx)) + RowGap
            + MultiplierRowHeight(ctx, BodyWidth(ctx)) + BottomPadding;
    }

    /// <summary>The interval row's height: the fixed 44px floor (caption band over the slider) grown when
    /// the translated caption needs more than one line at its band. Measure and Draw take the band from
    /// here so they cannot size two different bands.</summary>
    private float IntervalRowHeightFor(UiWidgetContext ctx, float bodyWidth)
    {
        float caption = IntervalCaptionBand(ctx, bodyWidth);
        return Math.Max(IntervalRowHeight, IntervalTopPadding + caption + IntervalSliderGap + SliderHeight + IntervalBottomPadding);
    }

    /// <summary>The interval caption's band, measured against the longest rendered caption at the width
    /// left of the seconds field.</summary>
    private float IntervalCaptionBand(UiWidgetContext ctx, float bodyWidth)
    {
        float width = IntervalCaptionWidth(bodyWidth);
        string sample = string.Format(ctx.Translation.Translate("US.Tuning.MinInterval"), IntervalCaptionSample);
        return UsKernelDraw.TextBandHeight(width, ctx, sample, UiFont.Tiny, IntervalCaptionFloor);
    }

    /// <summary>Width of the interval caption: everything left of the seconds field, which itself is
    /// right-aligned on the shared control-column right edge.</summary>
    private float IntervalCaptionWidth(float bodyWidth)
    {
        float fieldX = bodyWidth - UsKernelDraw.ControlColumnRightInset - FieldWidth;
        return Math.Max(1f, fieldX - UsKernelDraw.RowLeftPadding - LabelGap);
    }

    /// <summary>
    /// The multiplier row's height, through the shared support-row rule: the label takes the width left
    /// of the one fixed control column and the row keeps the density token as its floor, so a language
    /// whose label needs a second line grows the row instead of having its tail clipped (the
    /// <c>Height needs 42.66666px, has 20px at width 142px</c> finding this card used to carry). Measure
    /// and Draw both come through here, so the two halves cannot size different bands.
    /// </summary>
    private static float MultiplierRowHeight(UiWidgetContext ctx, float bodyWidth)
    {
        // The shared support-row rule: the label gets the fixed column's left side, and the row keeps the
        // density token as its floor. The cluster's own height never depends on the label.
        return Math.Max(SliderHeight, UsKernelDraw.RowLabelHeight(bodyWidth, ctx, ctx.Translation.Translate("US.Tuning.CooldownMultiplier")));
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        int ticks = ctx.Bindings.TryGet("min-interval", out int bound) ? bound : MinIntervalTicksFloor;
        float multiplier = ctx.Bindings.TryGet("cooldown-multiplier", out float mult) ? mult : 1f;

        float x = rect.x;
        float y = rect.y + TopPadding;
        float width = rect.width;

        // Both rows take their height from the same measure path Measure used, so a wrapped caption or
        // label cannot make the drawn card shorter than the measured one.
        float intervalHeight = IntervalRowHeightFor(ctx, width);
        DrawIntervalRow(new Rect(x, y, width, intervalHeight), ticks, ctx);
        y += intervalHeight + RowGap;
        DrawMultiplierRow(new Rect(x, y, width, MultiplierRowHeight(ctx, width)), multiplier, ctx);
    }

    private void DrawIntervalRow(Rect rect, int ticks, UiWidgetContext ctx)
    {
        float captionBand = IntervalCaptionBand(ctx, rect.width);
        // Both controls on this row terminate on the shared control-column right edge; the slider below
        // the caption sits on it too, so the card's right edge reads as one line.
        Rect fieldRect = new(rect.xMax - UsKernelDraw.ControlColumnRightInset - FieldWidth, rect.y + 2f, FieldWidth, LabelHeight);
        Rect sliderRect = new(
            rect.x,
            rect.y + IntervalTopPadding + captionBand + IntervalSliderGap,
            Math.Max(1f, rect.width - UsKernelDraw.ControlColumnRightInset),
            SliderHeight);

        UsKernelDraw.HelpHover(sliderRect, ctx, "us/timing/interval");
        if (fieldRect.x >= rect.x)
        {
            UsKernelDraw.HelpHover(fieldRect, ctx, "us/timing/interval");
        }
        else
        {
            // Degenerate row width (a layout too narrow to host the field): the field would be painted
            // outside its own row, so it is not drawn at all for this pass.
            fieldRect = new Rect(rect.x, fieldRect.y, 1f, 1f);
        }

        float sliderValue = UiNative.Slider(
            sliderRect, "timing-interval-slider", ctx.Session,
            Mathf.Clamp(ticks, MinIntervalTicksFloor, MinIntervalTicksCeil),
            MinIntervalTicksFloor, MinIntervalTicksCeil, out bool sliderChanged);
        if (sliderChanged)
        {
            ctx.Bindings.Set("min-interval", Mathf.RoundToInt(sliderValue));
        }

        // The field edits seconds (player unit); the binding stays machine ticks.
        float seconds = ticks / 60f;
        if (fieldRect.width >= FieldWidth)
        {
            UiNative.NumberField(fieldRect, "timing-interval-field", ctx.Session, seconds,
                MinIntervalSecondsFloor, MinIntervalSecondsCeil, "0.0", out bool committed);
            if (committed)
            {
                float edited = ctx.Session.GetOrCreateValueState("timing-interval-field").FloatValue;
                ctx.Bindings.Set("min-interval", Mathf.Max(MinIntervalTicksFloor, Mathf.RoundToInt(edited * 60f)));
            }
        }

        string secondsText = (ticks / 60f).ToString("0.0", CultureInfo.InvariantCulture) + " s";
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + IntervalTopPadding, IntervalCaptionWidth(rect.width), captionBand),
            string.Format(ctx.Translation.Translate("US.Tuning.MinInterval"), secondsText),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
    }

    private void DrawMultiplierRow(Rect rect, float multiplier, UiWidgetContext ctx)
    {
        UsKernelDraw.HelpHover(rect, ctx, "us/timing/multiplier");

        // The controls keep their own row height and sit on the band's midline: a two-line label makes
        // the row taller, not the steppers taller.
        float controlY = rect.y + (rect.height - SliderHeight) * 0.5f;
        Rect column = UsKernelDraw.ControlColumnRect(rect);
        float clusterFloor = ButtonWidth * 2f + StepperGap * 2f + 1f;
        if (column.width >= clusterFloor)
        {
            // The cluster fills the shared control column exactly, so the minus button's left edge sits on
            // the same line as the segmented control's first cell and the plus button's right edge sits on
            // the shared right edge.
            float fieldWidth = Math.Max(1f, column.width - ButtonWidth * 2f - StepperGap * 2f);
            Rect minusRect = new(column.x, controlY, ButtonWidth, SliderHeight);
            Rect fieldRect = new(minusRect.xMax + StepperGap, controlY, fieldWidth, SliderHeight);
            Rect plusRect = new(fieldRect.xMax + StepperGap, controlY, ButtonWidth, SliderHeight);

            if (UsKernelDraw.SelectionButton(minusRect, ctx, "−", ctx.Theme, selected: false))
            {
                ctx.Bindings.Set("cooldown-multiplier", UiNative.ClampValue(multiplier - MultiplierStep, MultiplierFloor, MultiplierCeil));
            }

            UiNative.NumberField(fieldRect, "timing-multiplier-field", ctx.Session, multiplier,
                MultiplierFloor, MultiplierCeil, "0.0", out bool committed);
            if (committed)
            {
                float edited = ctx.Session.GetOrCreateValueState("timing-multiplier-field").FloatValue;
                ctx.Bindings.Set("cooldown-multiplier", edited);
            }

            if (UsKernelDraw.SelectionButton(plusRect, ctx, "+", ctx.Theme, selected: false))
            {
                ctx.Bindings.Set("cooldown-multiplier", UiNative.ClampValue(multiplier + MultiplierStep, MultiplierFloor, MultiplierCeil));
            }
        }

        UsKernelDraw.Label(
            UsKernelDraw.RowLabelRect(rect, rect.height),
            ctx.Translation.Translate("US.Tuning.CooldownMultiplier"),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
    }
}
