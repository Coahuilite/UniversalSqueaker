using System;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned Overview playback behaviour: Easter-egg sounds, the three runtime scaling toggles and
/// the two-level eat-occurrence switch (parent "only while actually eating" plus its "include drugs"
/// child). Distance preset/range controls live exclusively in the Distance workspace.
/// </summary>
public sealed class UsBasicTuningWidget : UsSectionWidgetBase
{
    public const string KindName = "us/basic-tuning";

    // The single-line row height comes from the theme's density axis (24 regular / 20 dense, authored in
    // the manifest's <Styles>); this widget keeps no row constant of its own.
    private const float EggRowHeight = 52f;
    private const float RowGap = 2f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;

    // The egg row stacks two bands (title above, On/Off state underneath); both are measured against the
    // shared label band, and the row keeps 52px as its floor so one-line copy costs what it always did.
    private const float EggTitleTop = 4f;
    private const float EggTitleGap = 1f;
    private const float EggBottomPadding = 7f;
    private const float EggTitleFloor = 22f;
    private const float EggStateFloor = 18f;

    private const string EggLabelKey = "US.Tuning.EasterEggs";
    private const string EggOnKey = "US.Tuning.EasterEggs.On";
    private const string EggOffKey = "US.Tuning.EasterEggs.Off";

    // ---- Eat-occurrence pair: the parent switch, and the child switch it governs -----------------
    // Maintainer ruling 2026-09-15: the child row EXISTS ONLY WHILE THE PARENT IS ON (a greyed control that
    // cannot be used was judged worse than no control), and the inline grey notes are gone - the help drawer
    // carries that copy. Measure and Draw must never disagree, so the parent state the MEASURE pass read is
    // cached and reused by the DRAW pass of the same frame; the click that flips the parent bumps the content
    // revision, so the next frame re-measures.
    private const string EatPrecisionLabelKey = "US.Tuning.EatPrecision";
    private const string EatPrecisionChildLabelKey = "US.Tuning.EatPrecision.IncludeDrugs";
    private const string EatPrecisionValueKey = "eat-precision";
    private const string EatPrecisionToggleKey = "toggle-eat-precision";
    private const string EatPrecisionChildValueKey = "eat-precision-include-drugs";
    private const string EatPrecisionChildToggleKey = "toggle-eat-precision-include-drugs";
    private const string EatPrecisionHelpKey = "us/basic-tuning/eat-precision";
    private const string EatPrecisionChildHelpKey = "us/basic-tuning/eat-precision-include-drugs";

    /// <summary>The parent switch as the MEASURE pass read it; Draw reuses this so the drawn rows and the
    /// measured card height can never disagree within one frame.</summary>
    private bool measuredChildVisible;

    /// <summary>Shared measure/draw formula for the overview toggle rows. The child row is part of the sum
    /// only while the parent is on, and its state is cached for the draw pass.</summary>
    private float ContentHeight(UiWidgetContext ctx)
    {
        measuredChildVisible = ParentOn(ctx);
        return TopPadding + EggBands(ctx).Total + RowGap
            + BasicRowHeight(ctx, "US.Tuning.ScaleCooldown") + RowGap
            + BasicRowHeight(ctx, "US.Tuning.ScaleTalking") + RowGap
            + BasicRowHeight(ctx, "US.Tuning.ScalePopulation") + RowGap
            + BasicRowHeight(ctx, EatPrecisionLabelKey)
            + (measuredChildVisible ? RowGap + BasicRowHeight(ctx, EatPrecisionChildLabelKey) : 0f)
            + BottomPadding;
    }

    private static bool ParentOn(UiWidgetContext ctx)
    {
        return ctx.Bindings.TryGet(EatPrecisionValueKey, out bool value) && value;
    }

    /// <summary>
    /// One resolution path for the row label, shared by Measure and Draw. The band is the shared
    /// support-row rule: the label gets the width left of the one fixed control column, and the row keeps
    /// the theme's density token as its floor - so the three scaling sentences (which already exceed one
    /// line at the narrow layout) grow the row instead of being clipped, and no translation can move the
    /// checkbox column.
    /// </summary>
    private float BasicRowHeight(UiWidgetContext ctx, string labelKey)
    {
        return UsKernelDraw.RowLabelHeight(BodyWidth(ctx), ctx, ctx.Translation.Translate(labelKey));
    }

    /// <summary>
    /// The egg row's two stacked bands, taken once per measure/draw pass. The On/Off band is measured
    /// against the LONGER of the two shipped state strings, so the row height can never depend on the
    /// current binding value (Measure and Draw would otherwise disagree while a toggle is in flight).
    /// </summary>
    private (float Title, float State, float Total) EggBands(UiWidgetContext ctx)
    {
        float width = UsKernelDraw.RowLabelWidth(BodyWidth(ctx));
        float title = UsKernelDraw.TextBandHeight(width, ctx, ctx.Translation.Translate(EggLabelKey), UiFont.Small, EggTitleFloor);
        float on = ctx.Metrics.MeasureText(ctx.Translation.Translate(EggOnKey), UiFont.Tiny, width);
        float off = ctx.Metrics.MeasureText(ctx.Translation.Translate(EggOffKey), UiFont.Tiny, width);
        float state = Math.Max(EggStateFloor, Math.Max(on, off));
        float total = Math.Max(EggRowHeight, EggTitleTop + title + EggTitleGap + state + EggBottomPadding);
        return (title, state, total);
    }

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsBasicTuningWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<bool>("allow-eggs", elementPath);
        bindings.ValidateValue<bool>("scale-cooldown", elementPath);
        bindings.ValidateValue<bool>("scale-talking", elementPath);
        bindings.ValidateValue<bool>("scale-population", elementPath);
        bindings.ValidateValue<bool>(EatPrecisionValueKey, elementPath);
        bindings.ValidateValue<bool>(EatPrecisionChildValueKey, elementPath);
        bindings.ValidateAction<bool>("toggle-egg", elementPath);
        bindings.ValidateAction<bool>("toggle-scale-cooldown", elementPath);
        bindings.ValidateAction<bool>("toggle-scale-talking", elementPath);
        bindings.ValidateAction<bool>("toggle-scale-population", elementPath);
        bindings.ValidateAction<bool>(EatPrecisionToggleKey, elementPath);
        bindings.ValidateAction<bool>(EatPrecisionChildToggleKey, elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return ContentHeight(ctx);
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        return ContentHeight(ctx);
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        float x = rect.x;
        float y = rect.y + TopPadding;
        float innerWidth = rect.width;

        (float eggTitle, float eggState, float eggHeight) = EggBands(ctx);
        DrawEggRow(new Rect(x, y, innerWidth, eggHeight), ctx, eggTitle, eggState);
        y += eggHeight + RowGap;

        // Each row's band is measured once per frame and reused for both the rect and the cursor
        // advance; calling BasicRowHeight inline in both spots would double the text-measuring cost.
        float cooldownHeight = BasicRowHeight(ctx, "US.Tuning.ScaleCooldown");
        DrawBasicRow(new Rect(x, y, innerWidth, cooldownHeight), ctx, "scale-cooldown", "toggle-scale-cooldown", "US.Tuning.ScaleCooldown", "us/basic-tuning/scale-cooldown");
        y += cooldownHeight + RowGap;

        float talkingHeight = BasicRowHeight(ctx, "US.Tuning.ScaleTalking");
        DrawBasicRow(new Rect(x, y, innerWidth, talkingHeight), ctx, "scale-talking", "toggle-scale-talking", "US.Tuning.ScaleTalking", "us/basic-tuning/scale-talking");
        y += talkingHeight + RowGap;

        float populationHeight = BasicRowHeight(ctx, "US.Tuning.ScalePopulation");
        DrawBasicRow(new Rect(x, y, innerWidth, populationHeight), ctx, "scale-population", "toggle-scale-population", "US.Tuning.ScalePopulation", "us/basic-tuning/scale-population");
        y += populationHeight + RowGap;

        // Parent switch: the ordinary support-row shape, so the checkbox column and the help claim are
        // the same ones every row above uses.
        float eatPrecisionHeight = BasicRowHeight(ctx, EatPrecisionLabelKey);
        DrawBasicRow(new Rect(x, y, innerWidth, eatPrecisionHeight), ctx, EatPrecisionValueKey, EatPrecisionToggleKey, EatPrecisionLabelKey, EatPrecisionHelpKey);
        y += eatPrecisionHeight + RowGap;

        // The child row is drawn only when it is on screen - and only when the MEASURE pass planned for it, so
        // the drawn rows always match the arranged card height.
        if (measuredChildVisible)
        {
            float childHeight = BasicRowHeight(ctx, EatPrecisionChildLabelKey);
            DrawBasicRow(new Rect(x, y, innerWidth, childHeight), ctx, EatPrecisionChildValueKey, EatPrecisionChildToggleKey, EatPrecisionChildLabelKey, EatPrecisionChildHelpKey);
        }
    }


    private void DrawEggRow(Rect rect, UiWidgetContext ctx, float titleBand, float stateBand)
    {
        bool enabled = ctx.Bindings.TryGet("allow-eggs", out bool value) && value;
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/basic-tuning/egg");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, UsKernelDraw.RowRail.None);

        float labelWidth = UsKernelDraw.RowLabelWidth(rect);
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + EggTitleTop, labelWidth, titleBand),
            ctx.Translation.Translate(EggLabelKey),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + EggTitleTop + titleBand + EggTitleGap, labelWidth, stateBand),
            ctx.Translation.Translate(enabled ? EggOnKey : EggOffKey),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
        Rect checkbox = UsKernelDraw.CheckboxSlot(rect);
        bool toggled = UsKernelDraw.Checkbox(checkbox, ctx, enabled);

        // The row's hit band stops where the checkbox's starts: overlapping buttons would both report one
        // press, so inside its own 24px slot the checkbox decides, and the row decides everywhere else.
        Rect rowHit = UsKernelDraw.RowHitRect(rect, ctx);
        rowHit.width = Math.Max(1f, checkbox.x - rowHit.x);
        if (toggled || UiNative.Button(rowHit, ctx))
        {
            ctx.Bindings.Invoke("toggle-egg", !enabled);
        }
    }

    private void DrawBasicRow(Rect rect, UiWidgetContext ctx, string valueKey, string actionKey, string labelKey, string helpKey)
    {
        bool enabled = ctx.Bindings.TryGet(valueKey, out bool value) && value;
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, helpKey);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, UsKernelDraw.RowRail.None);
        UsKernelDraw.RowBottomLine(rect, ctx.Theme);

        UsKernelDraw.Label(
            UsKernelDraw.RowLabelRect(rect, rect.height),
            ctx.Translation.Translate(labelKey),
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
            ctx.Bindings.Invoke(actionKey, !enabled);
        }
    }

}
