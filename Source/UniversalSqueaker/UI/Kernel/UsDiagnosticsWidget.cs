using System;
using System.Collections.Generic;
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

    private const float ModeRowHeight = 26f;
    private const float RowGap = 4f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;

    /// <summary>Floor of the section heading band ("Developer log level"); the band grows when the
    /// translated heading needs more than this many pixels at the heading width.</summary>
    private const float HeadingFloor = 20f;

    private const string HeadingKey = "US.Diagnostics.Logging.Label";
    private const string LocalizeLabelKey = "US.Diagnostics.LocalizeDebugMenu";

    /// <summary>Per-frame option buffer for the segmented control, so the draw path allocates nothing.</summary>
    private readonly KeyValuePair<string, string>[] optionBuffer = new KeyValuePair<string, string>[LoggingOptions.Length];

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
        return ContentHeight(ctx);
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        return ContentHeight(ctx);
    }

    /// <summary>
    /// One measure path for the whole card, shared by Measure and Draw. The heading band is measured at
    /// the heading width, and the localize row's height comes from the shared support-row rule: the
    /// density token as the floor, grown when the translated label wraps in the remaining label band.
    /// The mode row is a segmented control and keeps its own fixed height.
    /// </summary>
    private float ContentHeight(UiWidgetContext ctx)
    {
        return TopPadding + HeadingHeight(ctx) + ModeRowHeight + RowGap + LocalizeRowHeight(ctx) + BottomPadding;
    }

    /// <summary>The heading sits on its own line above the segmented control, so it spans the full row
    /// width minus the shared right inset (nothing is to its right).</summary>
    private float HeadingWidth(UiWidgetContext ctx)
    {
        return Math.Max(1f, BodyWidth(ctx) - UsKernelDraw.RowLeftPadding - UsKernelDraw.ControlColumnRightInset);
    }

    private float HeadingHeight(UiWidgetContext ctx)
    {
        return UsKernelDraw.TextBandHeight(HeadingWidth(ctx), ctx, ctx.Translation.Translate(HeadingKey), UiFont.Tiny, HeadingFloor);
    }

    private float LocalizeRowHeight(UiWidgetContext ctx)
    {
        return UsKernelDraw.RowLabelHeight(BodyWidth(ctx), ctx, ctx.Translation.Translate(LocalizeLabelKey));
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

        float x = rect.x;
        float y = rect.y + TopPadding;

        UsKernelDraw.Label(
            new Rect(x + UsKernelDraw.RowLeftPadding, y, HeadingWidth(ctx), HeadingHeight(ctx)),
            ctx.Translation.Translate(HeadingKey),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
        y += HeadingHeight(ctx);

        // The three choices are one segmented control whose total width IS the shared control column:
        // equal cells, fixed width, one right edge - the same edge the localize checkbox below uses.
        // A translated cell label can only be ellipsized inside its own cell, never resize the column.
        int selected = 0;
        for (int i = 0; i < LoggingOptions.Length; i++)
        {
            optionBuffer[i] = new KeyValuePair<string, string>(
                ctx.Translation.Translate(LoggingOptions[i].LabelKey),
                LoggingOptions[i].HelpKey);
            if (LoggingOptions[i].Mode == mode) selected = i;
        }

        Rect modeRow = new(x, y, rect.width, ModeRowHeight);
        int clicked = UsKernelDraw.SegmentedRow(UsKernelDraw.ControlColumnRect(modeRow), ctx, optionBuffer, selected);
        if (clicked >= 0)
        {
            ctx.Bindings.Set("dev-logging", LoggingOptions[clicked].Mode);
        }

        y += ModeRowHeight + RowGap;
        DrawLocalizeRow(new Rect(rect.x, y, rect.width, LocalizeRowHeight(ctx)), ctx);
    }

    private void DrawLocalizeRow(Rect rect, UiWidgetContext ctx)
    {
        bool enabled = ctx.Bindings.TryGet("localize-debug-menu", out bool value) && value;
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/diagnostics/localize-debug");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, UsKernelDraw.RowRail.None);

        UsKernelDraw.Label(
            UsKernelDraw.RowLabelRect(rect, rect.height),
            ctx.Translation.Translate(LocalizeLabelKey),
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
            ctx.Bindings.Set("localize-debug-menu", !enabled);
        }
    }
}
