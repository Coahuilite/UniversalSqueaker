using System;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US diagnostics section: the dev logging level (three mutually exclusive buttons
/// writing the typed "dev-logging" binding) and the vanilla debug-menu localization toggle
/// (checkbox row writing the typed bool "localize-debug-menu" binding). Both are player-facing
/// support tools; the logging level applies live through <c>SqueakLog.Configure</c>.
/// </summary>
public sealed class UsDiagnosticsWidget : UsSectionWidgetBase
{
    public const string KindName = "us/diagnostics";

    private const float LabelHeight = 20f;
    private const float ModeRowHeight = 26f;
    private const float ToggleRowHeight = 28f;
    private const float RowGap = 4f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;
    private const float SidePadding = 10f;
    private const float ModeGap = 6f;

    /// <summary>Mode values stay the typed enum the "dev-logging" binding carries; only the display name is keyed.</summary>
    private static readonly (SqueakDevLoggingMode Mode, string LabelKey, string HelpKey)[] LoggingOptions =
    {
        (SqueakDevLoggingMode.Auto, "US.Diagnostics.Logging.Auto", "us/diagnostics/logging-auto"),
        (SqueakDevLoggingMode.Enabled, "US.Diagnostics.Logging.Enabled", "us/diagnostics/logging-enabled"),
        (SqueakDevLoggingMode.Disabled, "US.Diagnostics.Logging.Disabled", "us/diagnostics/logging-disabled"),
    };

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsDiagnosticsWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<SqueakDevLoggingMode>("dev-logging", elementPath);
        bindings.ValidateValue<bool>("localize-debug-menu", elementPath);
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
        return TopPadding + LabelHeight + ModeRowHeight + RowGap + ToggleRowHeight + BottomPadding;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        SqueakDevLoggingMode mode = ctx.Bindings.TryGet("dev-logging", out SqueakDevLoggingMode bound)
            ? bound
            : SqueakDevLoggingMode.Auto;

        float x = rect.x + SidePadding;
        float y = rect.y + TopPadding;
        float innerWidth = Math.Max(1f, rect.width - SidePadding * 2f);

        UsKernelDraw.Label(
            new Rect(x, y, innerWidth, LabelHeight),
            ctx.Translation.Translate("US.Diagnostics.Logging.Label"),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
        y += LabelHeight;

        float buttonWidth = Math.Max(1f, (innerWidth - ModeGap * (LoggingOptions.Length - 1)) / LoggingOptions.Length);
        float buttonX = x;
        for (int i = 0; i < LoggingOptions.Length; i++)
        {
            Rect optionRect = new(buttonX, y, buttonWidth, ModeRowHeight);
            UsKernelDraw.HelpHover(optionRect, ctx, LoggingOptions[i].HelpKey);
            if (UsKernelDraw.SelectionButton(optionRect, ctx, ctx.Translation.Translate(LoggingOptions[i].LabelKey), ctx.Theme, LoggingOptions[i].Mode == mode))
            {
                ctx.Bindings.Set("dev-logging", LoggingOptions[i].Mode);
            }

            buttonX += buttonWidth + ModeGap;
        }

        y += ModeRowHeight + RowGap;
        DrawLocalizeRow(new Rect(rect.x, y, rect.width, ToggleRowHeight), ctx);
    }

    private void DrawLocalizeRow(Rect rect, UiWidgetContext ctx)
    {
        bool enabled = ctx.Bindings.TryGet("localize-debug-menu", out bool value) && value;
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/diagnostics/localize-debug");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y, Math.Max(1f, rect.width - 60f), rect.height),
            ctx.Translation.Translate("US.Diagnostics.LocalizeDebugMenu"),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Checkbox(new Rect(rect.xMax - 34f, rect.y + (rect.height - 18f) * 0.5f, 18f, 18f), ctx.Theme, enabled);

        if (UiNative.Button(rect, ctx))
        {
            ctx.Bindings.Set("localize-debug-menu", !enabled);
        }
    }
}
