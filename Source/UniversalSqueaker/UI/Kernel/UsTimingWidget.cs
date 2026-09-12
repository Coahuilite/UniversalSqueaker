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
    private const float LabelHeight = 20f;
    private const float SliderHeight = 20f;
    private const float FieldWidth = 64f;
    private const float ButtonWidth = 26f;
    private const float RightPadding = 10f;
    private const float RowGap = 4f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;

    /// <summary>Gap the label leaves before the stepper cluster, and the cluster's own gaps.</summary>
    private const float LabelGap = 8f;
    private const float StepperGap = 4f;

    /// <summary>Width the multiplier row's right-hand stepper cluster (minus, field, plus) reserves.</summary>
    private const float StepperClusterWidth = ButtonWidth * 2f + FieldWidth + StepperGap * 3f;

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
        return TopPadding + IntervalRowHeight + RowGap + MultiplierRowHeight(ctx, BodyWidth(ctx)) + BottomPadding;
    }

    /// <summary>
    /// The multiplier row's label band. The shipped English label ("Global cooldown multiplier",
    /// <c>US.Tuning.CooldownMultiplier</c>) does not fit the band's width at the narrow layout, and the
    /// fit audit's default axis is height, so a fixed 20px band cut its second line away in silence -
    /// the finding this card carried was
    /// <c>page-root/body-row/content-scroll/timing Height needs 42.66666px, has 20px at width 142px</c>.
    /// The band therefore follows the measured wrap height, the shape the other US cards already use
    /// (RaceLayer, VoicePackChecklist): the card grows by the line the label needs, in whichever
    /// language needs it, instead of the text being clipped or declared single-line to keep the audit
    /// quiet. Measure and Draw both come through here, so the two halves cannot size different bands.
    /// </summary>
    private static float MultiplierRowHeight(UiWidgetContext ctx, float bodyWidth)
    {
        string label = ctx.Translation.Translate("US.Tuning.CooldownMultiplier");
        return Math.Max(SliderHeight, ctx.Metrics.MeasureText(label, UiFont.Small, MultiplierLabelWidth(bodyWidth)));
    }

    /// <summary>The label band's width inside one card body: everything left of the stepper cluster.</summary>
    private static float MultiplierLabelWidth(float bodyWidth)
    {
        return Math.Max(1f, bodyWidth - RightPadding - StepperClusterWidth - LabelGap);
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

        DrawIntervalRow(new Rect(x, y, width, IntervalRowHeight), ticks, ctx);
        y += IntervalRowHeight + RowGap;
        DrawMultiplierRow(new Rect(x, y, width, MultiplierRowHeight(ctx, width)), multiplier, ctx);
    }

    private void DrawIntervalRow(Rect rect, int ticks, UiWidgetContext ctx)
    {
        Rect fieldRect = new(rect.xMax - RightPadding - FieldWidth, rect.y + 2f, FieldWidth, LabelHeight);
        Rect sliderRect = new(rect.x, rect.y + LabelHeight + 2f, rect.width - RightPadding, SliderHeight);

        UsKernelDraw.HelpHover(sliderRect, ctx, "us/timing/interval");
        UsKernelDraw.HelpHover(fieldRect, ctx, "us/timing/interval");

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
        UiNative.NumberField(fieldRect, "timing-interval-field", ctx.Session, seconds,
            MinIntervalSecondsFloor, MinIntervalSecondsCeil, "0.0", out bool committed);
        if (committed)
        {
            float edited = ctx.Session.GetOrCreateValueState("timing-interval-field").FloatValue;
            ctx.Bindings.Set("min-interval", Mathf.Max(MinIntervalTicksFloor, Mathf.RoundToInt(edited * 60f)));
        }

        string secondsText = (ticks / 60f).ToString("0.0", CultureInfo.InvariantCulture) + " s";
        UsKernelDraw.Label(
            new Rect(rect.x, rect.y + 3f, Math.Max(1f, fieldRect.x - rect.x - 8f), 18f),
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
        Rect minusRect = new(rect.xMax - RightPadding - StepperClusterWidth, controlY, ButtonWidth, SliderHeight);
        Rect fieldRect = new(minusRect.xMax + StepperGap, controlY, FieldWidth, SliderHeight);
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

        UsKernelDraw.Label(
            new Rect(rect.x, rect.y, MultiplierLabelWidth(rect.width), rect.height),
            ctx.Translation.Translate("US.Tuning.CooldownMultiplier"),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
    }
}
