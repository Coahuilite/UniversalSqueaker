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
/// </summary>
public sealed class UsTimingWidget : UsSectionWidgetBase
{
    public const string KindName = "us/timing";

    // Row geometry follows the global-volume card: a Tiny label band over a slider row with a
    // right-aligned number field. The multiplier row is one Small line with stepper buttons.
    private const float IntervalRowHeight = 44f;
    private const float LabelHeight = 20f;
    private const float SliderHeight = 20f;
    private const float FieldWidth = 64f;
    private const float ButtonWidth = 26f;
    private const float RightPadding = 10f;
    private const float RowGap = 4f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;

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
        return ContentHeight();
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        return ContentHeight();
    }

    private static float ContentHeight()
    {
        return TopPadding + IntervalRowHeight + RowGap + SliderHeight + BottomPadding;
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
        DrawMultiplierRow(new Rect(x, y, width, SliderHeight), multiplier, ctx);
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

        float fieldWidth = FieldWidth;
        float gap = 4f;
        Rect minusRect = new(rect.xMax - RightPadding - (ButtonWidth * 2f + fieldWidth + gap * 3f), rect.y, ButtonWidth, rect.height);
        Rect fieldRect = new(minusRect.xMax + gap, rect.y, fieldWidth, rect.height);
        Rect plusRect = new(fieldRect.xMax + gap, rect.y, ButtonWidth, rect.height);

        if (UsKernelDraw.SelectionButton(minusRect, "−", ctx.Theme, selected: false))
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

        if (UsKernelDraw.SelectionButton(plusRect, "+", ctx.Theme, selected: false))
        {
            ctx.Bindings.Set("cooldown-multiplier", UiNative.ClampValue(multiplier + MultiplierStep, MultiplierFloor, MultiplierCeil));
        }

        UsKernelDraw.Label(
            new Rect(rect.x, rect.y, Math.Max(1f, minusRect.x - rect.x - 8f), rect.height),
            ctx.Translation.Translate("US.Tuning.CooldownMultiplier"),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
    }
}
