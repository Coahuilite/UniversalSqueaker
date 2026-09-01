using System;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// Kernel-owned Overview playback behaviour: Easter-egg sounds and the three runtime scaling
/// toggles. Distance preset/range controls live exclusively in the Distance workspace.
/// </summary>
public sealed class UsBasicTuningWidget : UsSectionWidgetBase
{
    public const string KindName = "us/basic-tuning";

    private const float RowHeight = 26f;
    private const float RowGap = 2f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;

    /// <summary>Shared measure/draw formula for the overview toggle rows.</summary>
    private static float ContentHeight()
    {
        return TopPadding + RowHeight * 5f + RowGap * 3f + BottomPadding;
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
        return ContentHeight();
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        return ContentHeight();
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

        DrawEggRow(new Rect(x, y, innerWidth, RowHeight * 2f), ctx);
        y += RowHeight * 2f + RowGap;

        DrawBasicRow(new Rect(x, y, innerWidth, RowHeight), ctx, "scale-cooldown", "toggle-scale-cooldown", "Scale cooldown with time speed");
        y += RowHeight + RowGap;
        DrawBasicRow(new Rect(x, y, innerWidth, RowHeight), ctx, "scale-talking", "toggle-scale-talking", "Scale frequency with talking");
        y += RowHeight + RowGap;
        DrawBasicRow(new Rect(x, y, innerWidth, RowHeight), ctx, "scale-population", "toggle-scale-population", "Scale periodic with audible population");
    }

    private void DrawEggRow(Rect rect, UiWidgetContext ctx)
    {
        bool enabled = ctx.Bindings.TryGet("allow-eggs", out bool value) && value;
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + 4f, Math.Max(1f, rect.width - 60f), 18f),
            "Easter egg sounds",
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + 22f, Math.Max(1f, rect.width - 60f), 16f),
            enabled ? "On (eggs join the pool)" : "Off (ordinary entries only)",
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


    private void DrawBasicRow(Rect rect, UiWidgetContext ctx, string valueKey, string actionKey, string label)
    {
        bool enabled = ctx.Bindings.TryGet(valueKey, out bool value) && value;
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y, Math.Max(1f, rect.width - 60f), rect.height),
            label,
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
