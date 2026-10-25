using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Scroll-owned help content. Explicit pinned topics take precedence; unpinned consumers retain
/// the session's effective hover reference and fifteen-pass grace. Only the displayed text is
/// translated/measured, with the same width and font in Measure and Draw, never catalog maxima.
/// </summary>
public sealed class UsHelpPanelWidget : IUiWidget
{
    public const string Kind = "us/help-panel";

    private const float Padding = 8f;
    private const float TitleMinHeight = 20f;
    private const float TitleGap = 6f;
    private const float ContentGap = 6f;

    /// <summary>Keyed format string of the panel header; <c>{0}</c> is the active section title.</summary>
    private const string HeaderFormatKey = "US.Help.Header";

    private UiElementSpec spec = UiElementSpec.Empty;

    // Node-owned, released with that node. FL may measure a scroll's full and scrollbar-reserved
    // widths in one arrange and reuse either result later; retain the corresponding displays,
    // not just the last Measure call. This is not a catalog or process-wide cache.
    private sealed class DisplayState
    {
        internal readonly List<MeasuredDisplay> Measured = new();
        internal string ObservedHoverRef = "";
        internal bool HasObservedHover;
    }

    private sealed class MeasuredDisplay
    {
        internal string Header = "", Label = "", Body = "";
        internal float Width, HeaderHeight, LabelHeight, BodyHeight;
        internal bool HideTitle;
        internal UiMeasureInputs? SmallEnvironment, TinyEnvironment;
        internal float Height => Padding * 2f + (HideTitle ? 0 : HeaderHeight + TitleGap + ContentGap)
            + LabelHeight + 2f + BodyHeight + 4f;
        internal bool Matches(UsHelpPanelLogic.HelpPanelDisplay display, string header)
            => Header == header && Label == display.LabelText && Body == display.BodyText;
    }

    private static DisplayState State(UiWidgetContext ctx) => ctx.State("us/help-measured-display", () => new DisplayState());

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, Kind, () => new UsHelpPanelWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Tab"), new UiAttributeDefinition("Hidden", UiAttributeKind.Boolean), new UiAttributeDefinition("TitleHidden", UiAttributeKind.Boolean))));
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindingAccess bindings, string elementPath)
    {
        bindings.ValidateValue<string>("help-section-ref", elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        DisplayState state = State(ctx);
        float textWidth = Math.Max(1f, ctx.ViewWidth - Padding * 2f);
        // BeginHelpPass clears a fresh claim before the controls republish during Draw. Consume
        // that previous Draw's observed ref at this boundary; there is no second grace clock.
        // Explicit pins and a currently effective session ref always take precedence.
        var display = ResolveFrameDisplay(ctx, CurrentSection(ctx), state.HasObservedHover ? state.ObservedHoverRef : null);
        string header = TitleHidden ? "" : HeaderText(ctx, display);
        var environment = ctx.Metrics as IUiTextMetricsEnvironment;
        var measured = new MeasuredDisplay {
            Width = textWidth, HideTitle = TitleHidden, Header = header,
            Label = display.LabelText, Body = display.BodyText,
            HeaderHeight = TitleHidden ? 0 : Math.Max(TitleMinHeight, Band(ctx, header, textWidth, UiFont.Small)),
            LabelHeight = Band(ctx, display.LabelText, textWidth, UiFont.Small),
            BodyHeight = Band(ctx, display.BodyText, textWidth, UiFont.Tiny),
            SmallEnvironment = environment?.CaptureEnvironment(UiFont.Small),
            TinyEnvironment = environment?.CaptureEnvironment(UiFont.Tiny)
        };
        state.Measured.RemoveAll(value => value.Width == textWidth && value.HideTitle == measured.HideTitle);
        state.Measured.Add(measured);
        // Same bounded set of alternate measure contexts the carrier retains per leaf.
        if (state.Measured.Count > 4) state.Measured.RemoveAt(0);
        return measured.Height;
    }

    private bool TitleHidden => spec.TryGetAttribute("TitleHidden", out string raw)
        && string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The one header-text outlet. The header is a Keyed format string wrapping the active title,
    /// so its length is data: Measure and Draw must both take it from here or the band gets
    /// sized for one string while another is drawn.
    /// </summary>
    private static string HeaderText(UiWidgetContext ctx, UsHelpPanelLogic.HelpPanelDisplay display)
    {
        return string.Format(UsKernelDraw.Keyed(ctx, HeaderFormatKey), display.TitleText);
    }

    private static float Band(UiWidgetContext ctx, string text, float textWidth, UiFont font)
    {
        return Math.Max(1f, ctx.Metrics.MeasureText(text, font, textWidth));
    }

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;

        UiThemeDraw.Panel(ctx.Drawing, rect, ctx.Theme);

        float x = rect.X + Padding;
        float y = rect.Y + Padding;
        float textWidth = Math.Max(1f, rect.Width - Padding * 2f);
        DisplayState state = State(ctx);
        var display = ResolveFrameDisplay(ctx, CurrentSection(ctx));
        bool pinned = ctx.Bindings.TryGet("pinned-help-ref", out string pin) && pin.Length > 0;
        state.HasObservedHover = !pinned;
        state.ObservedHoverRef = pinned ? "" : ctx.Session.CurrentHelpRef ?? "";
        MeasuredDisplay? measured = state.Measured.FindLast(value => value.Width == textWidth && value.HideTitle == TitleHidden);
        var environment = ctx.Metrics as IUiTextMetricsEnvironment;
        if (measured == null || !Equals(measured.SmallEnvironment, environment?.CaptureEnvironment(UiFont.Small))
            || !Equals(measured.TinyEnvironment, environment?.CaptureEnvironment(UiFont.Tiny)))
        {
            ctx.Invalidate();
            return; // Never paint text using an unmeasured width/font environment.
        }
        if (!measured.Matches(display, TitleHidden ? "" : HeaderText(ctx, display)))
        {
            ctx.Invalidate();
            // A pin changed by an earlier action belongs to the next arranged text, never the old
            // topic. Ordinary explicit navigation already bumps/arranges before its next repaint.
            if (pinned) return;
        }

        // A hover claim can change after this pass arranged. Draw only that arrangement's captured
        // strings AND bands; the dirty node adopts the new effective topic at the next boundary.
        if (!measured.HideTitle)
        {
            UiLayoutRect headerRect = new(x, y, textWidth, measured.HeaderHeight);
            UiThemeDraw.SectionHeader(ctx.Drawing, headerRect, measured.Header, ctx.Theme, ctx.Theme[UiPaletteRole.Foreground], UiFont.Small);
            y += headerRect.Height + TitleGap + ContentGap;
        }
        UsKernelDraw.Label(ctx.Drawing,
            new UiLayoutRect(x, y, textWidth, measured.LabelHeight),
            measured.Label,
            ctx.Theme,
            ctx.Theme[UiPaletteRole.Foreground],
            UiFont.Small,
            UiTextAnchor.UpperLeft);
        y += measured.LabelHeight + 2f;
        UsKernelDraw.Label(ctx.Drawing,
            new UiLayoutRect(x, y, textWidth, measured.BodyHeight),
            measured.Body,
            ctx.Theme,
            ctx.Theme[UiPaletteRole.SecondaryForeground],
            UiFont.Tiny,
            UiTextAnchor.UpperLeft);
    }

    /// <summary>The section this panel is currently falling back to (null when none is active).</summary>
    private static HelpSection? CurrentSection(UiWidgetContext ctx)
    {
        string sectionKey = ctx.Bindings.TryGet("help-section-ref", out string key) ? key : "";
        UsHelpCatalog.TryGetSection(sectionKey, out HelpSection section);
        return section;
    }

    /// <summary>Reads the session's effective help reference, including its 15-pass grace.
    /// CurrentHelpRef is a catalog reference, not a binding or a raw current-frame hit.</summary>
    private static UsHelpPanelLogic.HelpPanelDisplay ResolveFrameDisplay(UiWidgetContext ctx, HelpSection? section, string? observedHoverRef = null)
    {
        string current = ctx.Bindings.TryGet("pinned-help-ref", out string pinned) && pinned.Length > 0
            ? pinned : ctx.Session.CurrentHelpRef ?? observedHoverRef ?? "";
        return UsHelpPanelLogic.Resolve(section, current, TranslationSeam(ctx));
    }

    /// <summary>
    /// The seam handed to the Verse-free panel logic: catalog and panel strings are Keyed entry
    /// names, and Measure and Draw both resolve through it, so a measured height
    /// can never belong to a different string than the one drawn.
    /// </summary>
    private static Func<string, string> TranslationSeam(UiWidgetContext ctx)
    {
        return key => UsKernelDraw.Keyed(ctx, key);
    }
}
