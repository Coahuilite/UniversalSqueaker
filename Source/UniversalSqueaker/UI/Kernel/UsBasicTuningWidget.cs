using System;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned Overview playback behaviour: Easter-egg sounds and the three runtime scaling
/// toggles. Distance preset/range controls live exclusively in the Distance workspace.
/// </summary>
public sealed class UsBasicTuningWidget : UsSectionWidgetBase
{
    public const string KindName = "us/basic-tuning";

    private const float RowHeight = 26f;
    private const float EggRowHeight = 52f;
    private const float RowGap = 2f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;

    // The label band stops 60px short of the row edge to clear the checkbox; the row then absorbs the
    // wrapped lines of a label that needs more than one line, so translated copy is never cut off.
    private const float LabelRightReserve = 60f;
    private const float RowVerticalPadding = 8f;

    /// <summary>Shared measure/draw formula for the overview toggle rows.</summary>
    private float ContentHeight(UiWidgetContext ctx)
    {
        return TopPadding + EggRowHeight + RowGap
            + BasicRowHeight(ctx, "US.Tuning.ScaleCooldown") + RowGap
            + BasicRowHeight(ctx, "US.Tuning.ScaleTalking") + RowGap
            + BasicRowHeight(ctx, "US.Tuning.ScalePopulation")
            + BottomPadding;
    }

    /// <summary>
    /// One resolution path for the row label, shared by Measure and Draw: the row band is only as tall as
    /// the text the translation seam actually returned. The three scaling labels are full English
    /// sentences that already exceed one line at the minimum window width, and any language can be wider
    /// still, so a constant 26px row silently cut their tails off.
    /// </summary>
    private float BasicRowHeight(UiWidgetContext ctx, string labelKey)
    {
        float textWidth = Math.Max(1f, BodyWidth(ctx) - LabelRightReserve);
        float lines = ctx.Metrics.MeasureText(ctx.Translation.Translate(labelKey), UiFont.Small, textWidth);
        return Math.Max(RowHeight, lines + RowVerticalPadding);
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
        bindings.ValidateAction<bool>("toggle-egg", elementPath);
        bindings.ValidateAction<bool>("toggle-scale-cooldown", elementPath);
        bindings.ValidateAction<bool>("toggle-scale-talking", elementPath);
        bindings.ValidateAction<bool>("toggle-scale-population", elementPath);
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

        DrawEggRow(new Rect(x, y, innerWidth, EggRowHeight), ctx);
        y += EggRowHeight + RowGap;

        // Each row's band is measured once per frame and reused for both the rect and the cursor
        // advance; calling BasicRowHeight inline in both spots would double the text-measuring cost.
        float cooldownHeight = BasicRowHeight(ctx, "US.Tuning.ScaleCooldown");
        DrawBasicRow(new Rect(x, y, innerWidth, cooldownHeight), ctx, "scale-cooldown", "toggle-scale-cooldown", "US.Tuning.ScaleCooldown");
        y += cooldownHeight + RowGap;

        float talkingHeight = BasicRowHeight(ctx, "US.Tuning.ScaleTalking");
        DrawBasicRow(new Rect(x, y, innerWidth, talkingHeight), ctx, "scale-talking", "toggle-scale-talking", "US.Tuning.ScaleTalking");
        y += talkingHeight + RowGap;

        float populationHeight = BasicRowHeight(ctx, "US.Tuning.ScalePopulation");
        DrawBasicRow(new Rect(x, y, innerWidth, populationHeight), ctx, "scale-population", "toggle-scale-population", "US.Tuning.ScalePopulation");
    }

    private void DrawEggRow(Rect rect, UiWidgetContext ctx)
    {
        bool enabled = ctx.Bindings.TryGet("allow-eggs", out bool value) && value;
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + 4f, Math.Max(1f, rect.width - 60f), 22f),
            ctx.Translation.Translate("US.Tuning.EasterEggs"),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + 27f, Math.Max(1f, rect.width - 60f), 18f),
            ctx.Translation.Translate(enabled ? "US.Tuning.EasterEggs.On" : "US.Tuning.EasterEggs.Off"),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Checkbox(new Rect(rect.xMax - 34f, rect.y + 12f, 18f, 18f), ctx.Theme, enabled);

        if (UiNative.Button(rect))
        {
            ctx.Bindings.Invoke("toggle-egg", !enabled);
        }
    }

    private void DrawBasicRow(Rect rect, UiWidgetContext ctx, string valueKey, string actionKey, string labelKey)
    {
        bool enabled = ctx.Bindings.TryGet(valueKey, out bool value) && value;
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y, Math.Max(1f, rect.width - LabelRightReserve), rect.height),
            ctx.Translation.Translate(labelKey),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Checkbox(new Rect(rect.xMax - 34f, rect.y + (rect.height - 18f) * 0.5f, 18f, 18f), ctx.Theme, enabled);

        if (UiNative.Button(rect))
        {
            ctx.Bindings.Invoke(actionKey, !enabled);
        }
    }

}
