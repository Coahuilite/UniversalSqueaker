using System;
using System.Collections.Generic;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// The diagnostics panel's US-owned widget family (round-9 rulings §1.1/§1.5, reworked by 09 §3.2/§3.3):
/// toolbar (s/t, collapse, lock-detach), the paged/searched list, the pager, the narrow-Back control,
/// the current-summary + grouped gate-chain detail column, and the collapsed BAR that answers the three
/// questions (what is this / is it on / what is it doing) instead of replaying a summary row.
/// All widgets draw through <see cref="UsKernelDraw"/>/<see cref="UiThemeDraw"/> and hit-test through
/// <see cref="UiControls"/> only: the family is written so the panel files can LEAVE the gate 14
/// whitelist (raw backend hits: zero). Each widget wraps Measure/Draw in the session guard, so a
/// throw becomes the engine's RecoveryBand row, never a corrupted frame.
/// </summary>
public abstract class UsDiagWidgetBase : IUiWidget
{
    protected internal UiElementSpec spec = UiElementSpec.Empty;

    public abstract string Kind { get; }

    string IUiWidget.Kind => Kind;

    public void Configure(UiElementSpec value)
    {
        spec = value ?? throw new ArgumentNullException(nameof(value));
    }

    public abstract void Validate(IUiBindingAccess bindings, string elementPath);

    public float Measure(UiWidgetContext ctx)
    {
        return UiSessionGuard.MeasureOrFallback(
            ctx, spec.Id, Kind, ctx.ElementPath, FallbackHeight(ctx), () => MeasureBody(ctx));
    }

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;
        UiSessionGuard.DrawOrFallback(
            ctx,
            spec.Id,
            Kind,
            ctx.ElementPath,
            rect,
            () => DrawBody(rect, ctx),
            fallback => DrawFallback(fallback, ctx));
    }

    protected abstract float MeasureBody(UiWidgetContext ctx);
    protected abstract void DrawBody(UiLayoutRect rect, UiWidgetContext ctx);
    protected virtual float FallbackHeight(UiWidgetContext ctx) => 24f;

    protected virtual void DrawFallback(UiLayoutRect rect, UiWidgetContext ctx)
    {
        UsKernelDraw.Label(rect, "[" + Kind + "]", ctx, UiFont.Tiny, UiTextAnchor.MiddleCenter);
    }

    protected static bool IsCollapsed(UiWidgetContext ctx)
        => ctx.Bindings.TryGet(UsDiagnosticsHost.KeyCollapsed, out bool collapsed) && collapsed;

    protected string Scope => spec.TryGetAttribute("Scope", out string scope) ? scope : "main";

    protected static bool GetBool(IUiBindingAccess b, string key) => b.TryGet(key, out bool v) && v;
    protected static int GetInt(IUiBindingAccess b, string key) => b.TryGet(key, out int v) ? v : 0;
    protected static string GetString(IUiBindingAccess b, string key) => b.TryGet(key, out string v) ? v ?? string.Empty : string.Empty;


    /// <summary>DX1.5: the honest wrapped height of a text block at this width - measured through the
    /// SAME metrics seam the draw path uses, never capped. A capped measure feeding an uncapped draw
    /// is what let one paragraph paint over its neighbour (the logged Tiny need48/have36 pair).</summary>
    protected static float MeasuredTextHeight(UiWidgetContext ctx, string text, float width)
        => string.IsNullOrEmpty(text)
            ? 0f
            : Math.Max(LineHeight, ctx.Metrics.MeasureText(text, UiFont.Tiny, Math.Max(1f, width)));

    /// <summary>Calibrated single-line advance for Tiny text (the in-game audit's own need value).</summary>
    protected internal const float LineHeight = 18f;
}

/// <summary>
/// The four-state paint table, in ONE place (09 §3.4 / Task 1's ruling). Attention is the only hue a
/// condition row may carry: a Block status is US's attention cyan through <see cref="UsAttention"/>,
/// everything else is neutral ink. <c>UiTheme.Warning</c> is deliberately absent - on the carrier it is
/// a redirect onto Danger, and a failed gate is not a destructive action.
/// </summary>
internal static class UsDiagPaint
{
    /// <summary>Status cell: Block attention-cyan, Pass neutral (NOT Success green), Pending neutral (NOT Selected).</summary>
    internal static UiColour Status(UiTheme theme, UsDiagGateState state) => state switch
    {
        UsDiagGateState.Block => UsAttention.Brush,
        UsDiagGateState.Pass => theme[UiPaletteRole.Foreground],
        UsDiagGateState.Pending => theme[UiPaletteRole.SecondaryForeground],
        _ => theme[UiPaletteRole.SecondaryForeground],
    };

    /// <summary>The value cell is always neutral: a blocked condition must not colour its evidence.</summary>
    internal static UiColour Value(UiTheme theme, UsDiagGateState state)
        => state == UsDiagGateState.NA ? theme[UiPaletteRole.SecondaryForeground] : theme[UiPaletteRole.Foreground];

    /// <summary>Pawn-row / bar activity dot. Blocked is the attention role; the rest is neutral ink.</summary>
    internal static UiColour Dot(UiTheme theme, UsDiagDotTone tone) => tone switch
    {
        UsDiagDotTone.Ready => theme[UiPaletteRole.SuccessSurface],
        UsDiagDotTone.Blocked => UsAttention.Brush,
        _ => theme[UiPaletteRole.SecondaryForeground],
    };
}

/// <summary>
/// Glyph policy for the panel (defect D7): a glyph the font cannot draw is replaced by the word it
/// stood for, never by an empty box. The question "can this font draw it" is asked through the same
/// measurement seam that draws the text - never the backend directly, so this file adds no raw contact
/// to the boundary audit - and a harness drives the fallback branch through <see cref="SupportProbe"/>
/// instead of pretending to own a font.
/// </summary>
public static class UsDiagGlyphs
{
    public const string Dot = "●";
    public const string Prev = "◀";
    public const string Next = "▶";

    /// <summary>
    /// Whether the font can draw this glyph. <paramref name="probe"/> is the test seam - a lane answers
    /// "no" for a font it does not own - and the production answer comes from the injected metrics seam,
    /// never from the backend directly (this file stays out of the raw-contact audit).
    /// </summary>
    public static bool Supports(string glyph, ITextMetrics metrics, UiFont font, Func<string, bool>? probe = null)
        => !string.IsNullOrEmpty(glyph) && (probe ?? (text => metrics.MeasureWidth(text, font) > 0f))(glyph);

    public static string DotText(UiWidgetContext ctx, UsDiagDotTone tone, Func<string, bool>? probe = null)
        => Supports(Dot, ctx.Metrics, UiFont.Tiny, probe) ? Dot : UsKernelDraw.Keyed(ctx, UsDiagnosticsProjection.DotFallbackKey(tone));

    public static string PrevText(UiWidgetContext ctx, Func<string, bool>? probe = null)
        => Supports(Prev, ctx.Metrics, UiFont.Tiny, probe) ? Prev : UsKernelDraw.Keyed(ctx, "US.Diagnostics.Pager.Prev");

    public static string NextText(UiWidgetContext ctx, Func<string, bool>? probe = null)
        => Supports(Next, ctx.Metrics, UiFont.Tiny, probe) ? Next : UsKernelDraw.Keyed(ctx, "US.Diagnostics.Pager.Next");
}

/// <summary>Toolbar: seconds unit toggle (s/t), lock-detach (main scope only), collapse-to-bar.
/// Hidden (measure 0) while collapsed - the bar itself expands on click.</summary>
public sealed class UsDiagToolbarWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/toolbar";
    private const float ButtonHeight = 30f;
    private const float Gap = 4f;

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagToolbarWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Height", UiAttributeKind.Length), new UiAttributeDefinition("Scope"))));

    public override void Validate(IUiBindingAccess bindings, string elementPath)
    {
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeySeconds, elementPath);
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyCollapsed, elementPath);
        bindings.ValidateAction<int>(UsDiagnosticsHost.KeyLock, elementPath);
    }

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? 0f
        : Scope == "capture" ? CaptureLayout(new UiLayoutRect(0, 0, ctx.ViewWidth, 0), ctx, false) : ButtonHeight + Gap;

    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;
        if (Scope == "capture") { CaptureLayout(rect, ctx, true); return; }

        var b = ctx.Bindings;
        float y = rect.Y;
        float x = rect.X;

        // Units stay with their time fields/technical reader, never a misleading tracking switch.
        if (Scope == "main" && GetBool(b, UsDiagnosticsHost.KeyCanLock))
        {
            string label = UsKernelDraw.Keyed(ctx, "US.Diagnostics.Lock");
            float lockWidth = UsKernelDraw.Text(ctx).MeasureWidth(label) + UsKernelDraw.SelectionButtonLabelInset * 2;
            UiLayoutRect lockRect = new(x, y, lockWidth, ButtonHeight);
            if (UsKernelDraw.StyledButton(lockRect, ctx, label, false, controlId: UsDiagnosticsHost.KeyLock))
            {
                b.Invoke(UsDiagnosticsHost.KeyLock, 0);
            }

            x += lockWidth + Gap;
        }

        float collapseWidth = Math.Max(1f, rect.XMax - x);
        UiLayoutRect collapseRect = new(x, y, collapseWidth, ButtonHeight);
        if (UsKernelDraw.StyledButton(collapseRect, ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar"), false, controlId: UsDiagnosticsHost.KeyCollapsed))
        {
            b.Set(UsDiagnosticsHost.KeyCollapsed, true);
        }
    }

    private sealed class CaptureDisplay
    {
        internal float Width, Height;
        internal string Status = "";
        internal UiLayoutRect StatusRect;
        internal readonly List<(string Command, string Label, bool Secondary, UiLayoutRect Rect)> Actions = new();
        internal UiTextFont Font;
        internal UiMeasureInputs? Environment;
    }
    private static CaptureDisplay CapturePresentation(UiWidgetContext ctx, float width)
    {
        var display = new CaptureDisplay { Width = width };
        var text = UsKernelDraw.Text(ctx);
        display.Font = text.Font; display.Environment = text.CaptureEnvironment();
        if (!GetBool(ctx.Bindings, "diag-capture-visible")) return display;
        float x = 0, y = 0, row = UsKernelDraw.ControlHeight(ctx, 30);
        void Add(bool visible, string caption, string command, bool secondary = false)
        {
            if (!visible) return;
            string label = UsKernelDraw.Keyed(ctx, caption);
            float w = Math.Min(width, text.MeasureWidth(label) + UsKernelDraw.SelectionButtonLabelInset * 2);
            if (x > 0 && x + w > width) { x = 0; y += row + Gap; }
            display.Actions.Add((command, label, secondary, new UiLayoutRect(x, y, w, row)));
            x += w + Gap;
        }
        Add(GetBool(ctx.Bindings, "diag-capture-start-visible"), "US.Diagnostics.Capture.Start", "diag-capture-start");
        Add(GetBool(ctx.Bindings, "diag-capture-stop-visible"), "US.Diagnostics.Capture.Stop", "diag-capture-stop");
        Add(true, "US.Diagnostics.Capture.Copy", "diag-capture-copy");
        Add(GetBool(ctx.Bindings, "diag-capture-technical-visible"), "US.Diagnostics.Technical.Open", "diag-open-technical", true);
        Add(GetBool(ctx.Bindings, "diag-capture-clear-visible"), "US.Diagnostics.Capture.Clear", "diag-capture-clear", true);
        y += row + Gap;
        display.Status = GetString(ctx.Bindings, "diag-capture-status");
        float h = text.MeasureText(display.Status, width);
        display.StatusRect = new UiLayoutRect(0, y, width, h);
        display.Height = y + h;
        return display;
    }
    // Natural packing and paint share one captured presentation. Clicks take effect after paint.
    internal static float CaptureLayout(UiLayoutRect rect, UiWidgetContext ctx, bool paint)
    {
        var captured = ctx.State("diag-capture-measured", () => new List<CaptureDisplay>());
        if (!paint)
        {
            var current = CapturePresentation(ctx, Math.Max(1, rect.Width));
            captured.RemoveAll(x => x.Width == current.Width);
            captured.Add(current);
            if (captured.Count > 4) captured.RemoveAt(0);
            return current.Height;
        }
        var display = captured.FindLast(x => x.Width == rect.Width);
        var text = UsKernelDraw.TextForDrawing(ctx);
        if (display == null || display.Font != text.Font || !Equals(display.Environment, text.CaptureEnvironment()))
        { ctx.Invalidate(); return rect.Height; }
        string? command = null;
        foreach (var action in display.Actions)
        {
            var hit = new UiLayoutRect(rect.X + action.Rect.X, rect.Y + action.Rect.Y, action.Rect.Width, action.Rect.Height);
            bool pressed;
            if (action.Secondary)
            {
                var outlet = new UiLayoutRect(hit.X + UsKernelDraw.SelectionButtonLabelInset, hit.Y,
                    Math.Max(1, hit.Width - UsKernelDraw.SelectionButtonLabelInset * 2), hit.Height);
                text.Draw(outlet, UsKernelDraw.Ellipsized(action.Label, text, outlet.Width),
                    ctx.Theme.Styles.Resolve(UiStatusTone.Neutral, UiEmphasis.Muted).Text, singleLine: true);
                pressed = ctx.Controls.Activation(hit, ctx, action.Command, action.Command).Request;
            }
            else pressed = UsKernelDraw.StyledButton(hit, ctx, action.Label, false, controlId: action.Command);
            if (pressed) command = action.Command;
        }
        var status = display.StatusRect;
        text.Draw(new UiLayoutRect(rect.X + status.X, rect.Y + status.Y, status.Width, status.Height), display.Status,
            ctx.Theme[UiPaletteRole.Foreground], UiTextAnchor.UpperLeft);
        if (command != null) ctx.Bindings.Invoke(command);
        return display.Height;
    }
}

/// <summary>
/// The narrow presentation's Back control (09 §3.5). It exists only in the in-window navigation view,
/// which is the only place with an owning list: the pinned detail window's page declares no nav element
/// at all, so this widget can never appear there. Back only switches the view - search text, page and
/// the list's own scroll position live in the source and the session, so they are restored untouched.
/// </summary>
public sealed class UsDiagNavWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/nav";
    private const float RowHeight = 30f;

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagNavWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Height", UiAttributeKind.Length), new UiAttributeDefinition("Scope"))));

    public override void Validate(IUiBindingAccess bindings, string elementPath)
    {
        bindings.ValidateValue<UsDiagNavView>(UsDiagnosticsHost.KeyNavView, elementPath);
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyShowBack, elementPath);
    }

    protected override float MeasureBody(UiWidgetContext ctx)
        => IsCollapsed(ctx) || !ShowBack(ctx) ? 0f : RowHeight;

    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx) || !ShowBack(ctx)) return;

        string label = UsDiagGlyphs.PrevText(ctx) + " " + UsKernelDraw.Keyed(ctx, "US.Diagnostics.Nav.Back");
        if (UsKernelDraw.StyledButton(new UiLayoutRect(rect.X, rect.Y, rect.Width, RowHeight), ctx, label, false, controlId: UsDiagnosticsHost.KeyNavView))
        {
            ctx.Bindings.Set(UsDiagnosticsHost.KeyNavView, UsDiagNavView.List);
        }
    }

    private static bool ShowBack(UiWidgetContext ctx) => GetBool(ctx.Bindings, UsDiagnosticsHost.KeyShowBack);
}

/// <summary>
/// The narrow presentation's body: one element that OWNS the view switch and shows either the list
/// (search + rows + pager) or the detail, never both. The switch belongs here rather than in an
/// attribute because the carrier's creation-time contract allows <c>Tab</c> on widgets only, and
/// because "which view am I in" is interaction state this widget owns - with its own measure contract
/// over the content it will draw, which is what earns a kind.
/// <para>
/// Switching the view is a binding write onto <see cref="UsDiagnosticsHost.KeyNavView"/>, so the layout
/// cache sees it on the same clock as every other display write. Coming back restores the list's search
/// text and page from the source and its scroll position from the scroll node's own session state -
/// nothing is recreated, so nothing has to be re-derived.
/// </para>
/// </summary>
public sealed class UsDiagNavBodyWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/navbody";

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagNavBodyWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Height", UiAttributeKind.Length), new UiAttributeDefinition("Scope"))));

    public override void Validate(IUiBindingAccess bindings, string elementPath)
    {
        // The body composes two other widget families, so it restates their read/write contract: a
        // missing binding must fail at creation here, not half-draw inside a scroll.
        bindings.ValidateValue<UsDiagNavView>(UsDiagnosticsHost.KeyNavView, elementPath);
        bindings.ValidateValue<string>(UsDiagnosticsHost.KeySearch, elementPath);
        bindings.ValidateValue<int>(UsDiagnosticsHost.KeyPage, elementPath);
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyRawOpen, elementPath);
        bindings.ValidateAction<int>(UsDiagnosticsHost.KeyRowClick, elementPath);
    }

    protected override float MeasureBody(UiWidgetContext ctx)
        => IsCollapsed(ctx) ? 0f : IsListView(ctx)
            ? UsDiagListWidget.BodyHeight(ctx) + UsDiagPagerWidget.BodyHeight
            : UsDiagDetailWidget.DetailHeight(ctx, ctx.ViewWidth);

    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;
        if (!IsListView(ctx))
        {
            UsDiagDetailWidget.DrawDetail(rect, ctx);
            return;
        }

        UsDiagListWidget.DrawBodyCore(new UiLayoutRect(rect.X, rect.Y, rect.Width, UsDiagListWidget.BodyHeight(ctx)), ctx);
        UsDiagPagerWidget.DrawBodyCore(
            new UiLayoutRect(rect.X, rect.Y + UsDiagListWidget.BodyHeight(ctx), rect.Width, UsDiagPagerWidget.BodyHeight),
            ctx);
    }

    private static bool IsListView(UiWidgetContext ctx)
        => !ctx.Bindings.TryGet(UsDiagnosticsHost.KeyNavView, out UsDiagNavView view) || view != UsDiagNavView.Detail;
}

/// <summary>
/// The collapsed bar (09 §3.3): a page-wide strip that answers the three questions in words - identity,
/// on/off, what it is doing now - and carries the two actions (expand, close) a collapsed window needs.
/// It is its own root element, NOT a child of the two-column Row, and it never reuses the list row pen:
/// the defect it replaces was a bar whose width and content both came from a 250px list row.
/// The switch segment is READ-ONLY status text derived by the projection from monitor-row presence and
/// game-paused state; the source exposes no recording setter, so no ON/OFF or pause control is drawn.
/// </summary>
public sealed class UsDiagBarWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/bar";

    /// <summary>
    /// 09 §3.3's geometry, re-measured against real glyph heights: the 32px strip carried two 14px lines,
    /// but a tiny line needs 18 and the identity line draws at Small (calibrated 21.33). The 2026-09-14
    /// in-game log reported it six times as "diag-bar (height axis, needs 18, has 14)" - the collapsed bar
    /// was clipping every line it drew. The strip now reserves the real bands: TopPad 2 + 22 + 18 + 2 = 44.
    /// </summary>
    internal const float BarHeight = 44f;

    private const float Padding = 8f;

    /// <summary>Band of the identity line, drawn at <see cref="UiFont.Small"/> (calibrated one-line 21.33).</summary>
    private const float TitleLineHeight = 22f;

    /// <summary>Band of every Tiny line (calibrated one-line height 18).</summary>
    private const float StripLineHeight = 18f;
    private const float TopPad = 2f;
    private const float Gap = 6f;
    private const float DotWidth = 14f;
    private const float ActionGap = 4f;
    private const float ActionPad = 8f;

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagBarWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Height", UiAttributeKind.Length), new UiAttributeDefinition("Scope"))));

    public override void Validate(IUiBindingAccess bindings, string elementPath)
    {
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyCollapsed, elementPath);
        bindings.ValidateValue<UsDiagBarModel>(UsDiagnosticsHost.KeyBar, elementPath);
        bindings.ValidateCommand(UsDiagnosticsHost.KeyClose, elementPath);
    }

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? BarHeight : 0f;

    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (!IsCollapsed(ctx)) return;

        var b = ctx.Bindings;
        if (!b.TryGet(UsDiagnosticsHost.KeyBar, out UsDiagBarModel? bar) || bar == null) return;

        bool hovered = ctx.Controls.IsMouseOver(rect);
        UiThemeDraw.Surface(ctx.Drawing, rect, ctx.Theme, hovered ? ctx.Theme[UiPaletteRole.HoverSurface] : ctx.Theme[UiPaletteRole.PanelSurface], ctx.Theme[UiPaletteRole.Border]);

        float expandWidth = MeasureAction(ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar.Expand"));
        float closeWidth = MeasureAction(ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar.Close"));
        float actionsWidth = expandWidth + closeWidth + ActionGap;

        float innerLeft = rect.X + Padding;
        float innerRight = rect.XMax - Padding;
        float lineTop = rect.Y + TopPad;

        // Line 2 actions are laid out first: they are unconditional, the activity sentence is not.
        UiLayoutRect closeRect = new(innerRight - closeWidth, lineTop + TitleLineHeight, closeWidth, StripLineHeight);
        UiLayoutRect expandRect = new(closeRect.X - ActionGap - expandWidth, lineTop + TitleLineHeight, expandWidth, StripLineHeight);

        UsDiagBarLayout layout = UsDiagnosticsProjection.LayoutBar(
            bar,
            Math.Max(1f, innerRight - innerLeft),
            actionsWidth + Gap,
            Gap,
            text => ctx.Metrics.MeasureWidth(text, UiFont.Tiny));

        // Line 1: identity (never omitted) then the switch, then the scale figure when it fits.
        float identityWidth = Math.Max(40f, innerRight - innerLeft - ctx.Metrics.MeasureWidth(bar.SwitchText, UiFont.Tiny) - Gap
            - (layout.ShowScale ? ctx.Metrics.MeasureWidth(bar.Scale, UiFont.Tiny) + Gap * 3f : 0f));
        string identity = UsKernelDraw.Ellipsized(bar.Identity, ctx, UiFont.Small, identityWidth);
        UsKernelDraw.Label(new UiLayoutRect(innerLeft, lineTop, identityWidth, TitleLineHeight), identity, ctx, ctx.Theme[UiPaletteRole.Foreground], UiFont.Small, UiTextAnchor.MiddleLeft, singleLine: true);
        float switchX = innerLeft + ctx.Metrics.MeasureWidth(identity, UiFont.Small) + Gap;
        UsKernelDraw.Label(new UiLayoutRect(switchX, lineTop, Math.Max(1f, innerRight - switchX), TitleLineHeight), bar.SwitchText, ctx, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: true);
        if (layout.ShowScale)
        {
            float scaleWidth = ctx.Metrics.MeasureWidth(bar.Scale, UiFont.Tiny);
            UsKernelDraw.Label(new UiLayoutRect(innerRight - scaleWidth, lineTop, scaleWidth, TitleLineHeight), bar.Scale, ctx, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.MiddleRight, singleLine: true);
        }

        // Line 2: the activity sentence (first to go when narrow) and the two actions.
        if (layout.ShowActivity)
        {
            string dot = UsDiagGlyphs.DotText(ctx, bar.ActivityTone);
            UiLayoutRect dotRect = new(innerLeft, lineTop + TitleLineHeight, DotWidth, StripLineHeight);
            UsKernelDraw.Label(dotRect, dot, ctx, UsDiagPaint.Dot(ctx.Theme, bar.ActivityTone), UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: true);
            float textX = dotRect.XMax;
            float textWidth = Math.Max(1f, expandRect.X - Gap - textX);
            UsKernelDraw.Label(
                new UiLayoutRect(textX, lineTop + TitleLineHeight, textWidth, StripLineHeight),
                UsKernelDraw.Ellipsized(bar.Activity, ctx, UiFont.Tiny, textWidth),
                ctx, ctx.Theme[UiPaletteRole.Foreground], UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: true);
        }

        if (UsKernelDraw.SelectionButton(expandRect, ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar.Expand"), ctx.Theme, false, controlId: "bar-expand"))
        {
            b.Set(UsDiagnosticsHost.KeyCollapsed, false);
        }

        if (UsKernelDraw.SelectionButton(closeRect, ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar.Close"), ctx.Theme, false, controlId: UsDiagnosticsHost.KeyClose))
        {
            // A command binding, invoked through its own overload: the typed overload would look for a
            // payload action and throw inside the draw pass.
            b.Invoke(UsDiagnosticsHost.KeyClose);
        }

        // The whole strip still expands; the two buttons above capture their own clicks first.
        if (ctx.Controls.Activation(rect, ctx, "bar-expand", "strip").Request)
        {
            b.Set(UsDiagnosticsHost.KeyCollapsed, false);
        }
    }

    private static float MeasureAction(UiWidgetContext ctx, string label)
    {
        return ctx.Metrics.MeasureWidth(label, UiFont.Tiny) + ActionPad * 2f;
    }
}

/// <summary>The paged, searchable summary list: one search field + 8 rows (the shared summary-row
/// layout). A row click browses that pawn without a vanilla selection or capture write. The same widget serves the wide
/// master column and the narrow list view - only the arrangement differs.</summary>
public sealed class UsDiagListWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/list";
    internal const float FieldHeight = 24f;

    // 22, and deliberately NOT UiTheme.Geometry.RowHeight: this is the diagnostics report surface, not a
    // settings page. Its rows are one Tiny line each, its page holds a fixed RowsPerPage of them, and its
    // fallback height is arithmetic over both - so it owns a fixed rhythm. Reading the settings density
    // here would let a style document resize the report's pagination, and the report is the surface that
    // has to stay comparable between two runs of the game.
    internal const float RowHeight = 22f;
    // DX1.3: below the single-line column budget (UsDiagRowPainter.SingleLineMinimum) a row splits
    // into two lines and grows to this height; the page rhythm stays arithmetic over the row height
    // the painter actually draws.
    internal const float TwoLineRowHeight = 38f;
    private const string SearchStateId = "diag-search-field";

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagListWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"))));

    public override void Validate(IUiBindingAccess bindings, string elementPath)
    {
        bindings.ValidateValue<string>(UsDiagnosticsHost.KeySearch, elementPath);
        bindings.ValidateAction<int>(UsDiagnosticsHost.KeyRowClick, elementPath);
    }

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? 0f : BodyHeight(ctx);

    /// <summary>The search field plus one page of rows; the navigation body composes the same number.
    /// DX1.4: the field reserves the MEASURED Small line plus its 2+2 insets - the fixed 24 left the
    /// text box 20px where the engine asked for 22 (the logged need22/have20 pair).</summary>
    internal static float FieldHeightFor(UiWidgetContext ctx)
        => UsKernelDraw.ControlHeight(ctx, 30);

    /// <summary>DX1.3: the 232px master column cannot carry six single-line columns without crushing
    /// the name; the wide navigation list can. The painter and this height arithmetic read ONE
    /// decision, so a row is never measured as two lines and drawn as one (or the reverse).</summary>
    internal static bool TwoLine(UiWidgetContext ctx) => ctx.ViewWidth < UsDiagRowPainter.SingleLineMinimum;

    internal static float RowHeightFor(UiWidgetContext ctx) => TwoLine(ctx) ? TwoLineRowHeight : RowHeight;

    internal static float BodyHeight(UiWidgetContext ctx)
    {
        float height = FieldHeightFor(ctx) + QueryStatusHeight(ctx);
        if (ctx.Bindings.TryGet(UsDiagnosticsHost.KeyRows, out IReadOnlyList<UsDiagRow> rows) && rows.Count > 0)
            foreach (var row in rows) height += UsDiagRowPainter.Height(ctx, row, ctx.ViewWidth);
        else height += UsKernelDraw.Text(ctx).MeasureText(UsKernelDraw.Keyed(ctx,
            GetString(ctx.Bindings, UsDiagnosticsHost.KeySearch).Length > 0 ? "US.Diagnostics.Search.None" : "US.Diagnostics.Panel.Empty"), Math.Max(1, ctx.ViewWidth - 8));
        return height;
    }
    private static string QueryStatus(UiWidgetContext ctx) => GetString(ctx.Bindings, "diag-query-status");
    internal static float QueryStatusHeight(UiWidgetContext ctx)
        => string.IsNullOrEmpty(QueryStatus(ctx)) ? 0 : UsKernelDraw.Text(ctx).MeasureText(QueryStatus(ctx), Math.Max(1, ctx.ViewWidth - 8));

    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;
        DrawBodyCore(new UiLayoutRect(rect.X, rect.Y, rect.Width, BodyHeight(ctx)), ctx);
    }

    /// <summary>
    /// The list's own content, reusable by the narrow navigation body (which stacks the pager under it).
    /// It draws the search field and one page of rows into exactly <see cref="BodyHeight(UiWidgetContext)"/> pixels.
    /// </summary>
    internal static void DrawBodyCore(UiLayoutRect rect, UiWidgetContext ctx)
    {
        var b = ctx.Bindings;
        string query = DrawSearchField(new UiLayoutRect(rect.X, rect.Y, Math.Max(1, rect.Width - 64), FieldHeightFor(ctx)), ctx);
        if (UsKernelDraw.StyledButton(new UiLayoutRect(rect.XMax - 60, rect.Y, 60, FieldHeightFor(ctx)), ctx,
            UsKernelDraw.Keyed(ctx, "US.Browse.Clear"), false, controlId: "diag-search-clear")) b.Set(UsDiagnosticsHost.KeySearch, "");
        float y = rect.Y + FieldHeightFor(ctx);
        float scopeHeight = QueryStatusHeight(ctx);
        if (scopeHeight > 0) UsKernelDraw.StyledLabel(ctx, new UiLayoutRect(rect.X + 4, y, Math.Max(1, rect.Width - 8), scopeHeight), QueryStatus(ctx), anchor: UiTextAnchor.UpperLeft);
        y += scopeHeight;

        if (!b.TryGet(UsDiagnosticsHost.KeyRows, out IReadOnlyList<UsDiagRow>? rows) || rows == null || rows.Count == 0)
        {
            string note = query.Length > 0
                ? UsKernelDraw.Keyed(ctx, "US.Diagnostics.Search.None")
                : UsKernelDraw.Keyed(ctx, "US.Diagnostics.Panel.Empty");
            float h = UsKernelDraw.Text(ctx).MeasureText(note, Math.Max(1, rect.Width - 8));
            UsKernelDraw.StyledLabel(ctx, new UiLayoutRect(rect.X + 4, y, Math.Max(1, rect.Width - 8), h), note, anchor: UiTextAnchor.UpperLeft);
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            UsDiagRow row = rows[i];
            float rowHeight = UsDiagRowPainter.Height(ctx, row, rect.Width);
            UiLayoutRect rowRect = new(rect.X, y, rect.Width, rowHeight);
            bool hovered = ctx.Controls.IsMouseOver(rowRect);
            // A locked row is unavailable, not selected: it takes the disabled rail (hatch) so the hatch
            // and the selected plane can never be confused for one another.
            UsKernelDraw.RowSurface(ctx.Drawing, rowRect, ctx.Theme, hovered, row.Locked ? UsKernelDraw.RowRail.Disabled : UsKernelDraw.RowRail.None);
            UsDiagRowPainter.Paint(rowRect, row, ctx);
            y += rowHeight;
            if (ctx.Controls.Activation(rowRect, ctx, UsDiagnosticsHost.KeyRowClick, "row", row.PawnId).Request)
            {
                b.Invoke(UsDiagnosticsHost.KeyRowClick, row.PawnId);
            }
        }
    }

    private static string DrawSearchField(UiLayoutRect rect, UiWidgetContext ctx)
    {
        var b = ctx.Bindings;
        string current = GetString(b, UsDiagnosticsHost.KeySearch);
        UiThemeDraw.Surface(ctx.Drawing, rect, ctx.Theme, ctx.Theme[UiPaletteRole.ControlSurface], ctx.Theme[UiPaletteRole.Border]);

        UiLayoutRect textRect = new(rect.X + 6f, rect.Y + 2f, Math.Max(1f, rect.Width - 12f), Math.Max(1f, rect.Height - 4f));
        UiEditSession<string> editor = UsKernelDraw.LiveTextField(textRect, ctx, UsDiagnosticsHost.KeySearch);
        if (!editor.Focused && editor.Draft.Length == 0)
            UsKernelDraw.StyledLabel(ctx, textRect, UsKernelDraw.Ellipsized(UsKernelDraw.Keyed(ctx, "US.Diagnostics.Search.Placeholder"), UsKernelDraw.Text(ctx), textRect.Width), singleLine: true);
        return editor.Draft;
    }
}

/// <summary>Pager: prev/next plus "page n/N · total M" - the honest-count ruling: nothing is
/// silently dropped, the tracking set pages 8 at a time. The two arrows size themselves from the
/// text they actually draw (D7: a glyph the font cannot draw becomes its word), so the control can
/// never clip its own label.</summary>
public sealed class UsDiagPagerWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/pager";
    private const float RowHeight = 30f;
    private const float ButtonPad = 8f;

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagPagerWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Height", UiAttributeKind.Length))));

    public override void Validate(IUiBindingAccess bindings, string elementPath)
    {
        bindings.ValidateValue<int>(UsDiagnosticsHost.KeyPage, elementPath);
    }

    /// <summary>The pager's own height, for a composer that stacks it.</summary>
    internal const float BodyHeight = RowHeight;

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? 0f : BodyHeight;

    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;
        DrawBodyCore(new UiLayoutRect(rect.X, rect.Y, rect.Width, BodyHeight), ctx);
    }

    /// <summary>The pager's content, reusable by the narrow navigation body.</summary>
    internal static void DrawBodyCore(UiLayoutRect rect, UiWidgetContext ctx)
    {
        var b = ctx.Bindings;
        int page = GetInt(b, UsDiagnosticsHost.KeyPage);
        int pages = Math.Max(1, GetInt(b, UsDiagnosticsHost.KeyPageCount));
        int total = GetInt(b, UsDiagnosticsHost.KeyTotal);

        string prev = UsDiagGlyphs.PrevText(ctx);
        string next = UsDiagGlyphs.NextText(ctx);
        float prevWidth = UsKernelDraw.Text(ctx).MeasureWidth(prev) + ButtonPad * 2f;
        float nextWidth = UsKernelDraw.Text(ctx).MeasureWidth(next) + ButtonPad * 2f;

        if (page > 0 && UsKernelDraw.StyledButton(new UiLayoutRect(rect.X, rect.Y, prevWidth, RowHeight), ctx, prev, false, controlId: "page-previous", itemKey: page))
        {
            b.Set(UsDiagnosticsHost.KeyPage, page - 1);
        }

        if (page < pages - 1 && UsKernelDraw.StyledButton(new UiLayoutRect(rect.XMax - nextWidth, rect.Y, nextWidth, RowHeight), ctx, next, false, controlId: "page-next", itemKey: page))
        {
            b.Set(UsDiagnosticsHost.KeyPage, page + 1);
        }

        float textX = rect.X + prevWidth + 4f;
        float textW = Math.Max(1f, rect.Width - prevWidth - nextWidth - 8f);
        UsKernelDraw.StyledLabel(ctx,
            new UiLayoutRect(textX, rect.Y, textW, RowHeight),
            string.Format(UsKernelDraw.Keyed(ctx, "US.Diagnostics.Page.Format"), page + 1, pages, total),
            anchor: UiTextAnchor.MiddleCenter, singleLine: true);
    }
}

/// <summary>
/// The detail column (09 §3.2 reading order): header, the CURRENT-ONLY summary, the previous-event band,
/// the 16 conditions grouped by side with each band naming its clock and each row carrying its own basis,
/// and the raw values folded away by default. Values get the wider half of the row, wrap to a measured
/// number of lines and are never ellipsized as their only representation; fonts are never shrunk.
/// </summary>
public sealed class UsDiagDetailWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/detail";
    public override string Kind => KindName;
    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName,
        () => new UsDiagDetailWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Scope"))));
    public override void Validate(IUiBindingAccess bindings, string elementPath)
    {
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyRawOpen, elementPath);
        foreach (UsDiagGateGroup group in Enum.GetValues(typeof(UsDiagGateGroup)))
            bindings.ValidateValue<bool>(UsDiagnosticsHost.GroupKey(group), elementPath);
    }
    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? 0 : DetailHeight(ctx, ctx.ViewWidth);
    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx) { if (!IsCollapsed(ctx)) DrawDetail(rect, ctx); }
    internal static float DetailHeight(UiWidgetContext ctx, float width) => Layout(new UiLayoutRect(0, 0, width, 0), ctx, false);
    internal static void DrawDetail(UiLayoutRect rect, UiWidgetContext ctx)
    {
        float wanted = Layout(rect, ctx, true);
        if (Math.Abs(wanted - rect.Height) > .5f) ctx.Node?.MarkDirty();
    }
    // One measured single-column flow. Long status, values and reasons receive the same width at paint.
    private static float Layout(UiLayoutRect rect, UiWidgetContext ctx, bool paint)
    {
        float y = rect.Y, width = Math.Max(1, rect.Width);
        var text = UsKernelDraw.Text(ctx);
        float Line(string value, UiColour? ink = null, float indent = 0)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            float w = Math.Max(1, width - indent);
            float h = text.MeasureText(value, w);
            if (paint) UsKernelDraw.TextForDrawing(ctx).Draw(new UiLayoutRect(rect.X + indent, y, w, h), value,
                ink ?? ctx.Theme[UiPaletteRole.Foreground], UiTextAnchor.UpperLeft);
            y += h;
            return h;
        }
        void GateHeading(UsDiagGateLine gate)
        {
            // Keep the status its own semantic/colour outlet, without adding a vertical row.
            // Both bands wrap at their actual painted widths with the same body run.
            float available = Math.Max(1, width - 8);
            float statusWidth = Math.Max(1, Math.Min(text.MeasureWidth(gate.Status), available * .5f));
            float nameWidth = Math.Max(1, available - statusWidth - 6);
            float height = Math.Max(text.MeasureText(gate.Name, nameWidth), text.MeasureText(gate.Status, statusWidth));
            if (paint)
            {
                var drawing = UsKernelDraw.TextForDrawing(ctx);
                drawing.Draw(new UiLayoutRect(rect.X + 8, y, nameWidth, height), gate.Name,
                    ctx.Theme[UiPaletteRole.Foreground], UiTextAnchor.UpperLeft);
                drawing.Draw(new UiLayoutRect(rect.X + 8 + nameWidth + 6, y, statusWidth, height), gate.Status,
                    UsDiagPaint.Status(ctx.Theme, gate.State), UiTextAnchor.UpperLeft);
            }
            y += height;
        }
        string Tr(string key) => UsKernelDraw.Keyed(ctx, key);
        float Button(string label, string id, object? key = null)
        {
            float h = Math.Max(UsKernelDraw.ControlHeight(ctx, 30), text.MeasureText(label, Math.Max(1, width - 12)));
            bool pressed = paint && UsKernelDraw.SelectionButton(new UiLayoutRect(rect.X, y, width, h), ctx, label, ctx.Theme, false,
                font: UiFont.Small, controlId: id, itemKey: key);
            y += h + 4;
            return pressed ? 1 : 0;
        }
        if (!ctx.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) || detail == null)
        {
            Line(Tr("US.Diagnostics.Summary.Incomplete")); y += 4;
            Line(Tr("US.Diagnostics.Detail.Empty"));
            return y - rect.Y + 8;
        }
        string titleText = Tr("US.Diagnostics.Viewing") + " · " + detail.PawnText;
        var titleRun = UiTextFonts.Resolve(ctx.Metrics, paint ? ctx.Drawing : null, new UiTextFont(UiFont.Medium, 22), elementPath: ctx.ElementPath);
        float titleHeight = titleRun.MeasureText(titleText, width);
        if (paint) titleRun.Draw(new UiLayoutRect(rect.X, y, width, titleHeight), titleText, ctx.Theme[UiPaletteRole.Foreground], UiTextAnchor.UpperLeft);
        y += titleHeight + 4;
        Line(detail.ObservationStatus ?? Tr(detail.Ready ? "US.Diagnostics.Ready" : "US.Diagnostics.Blocked"));
        y += 8;
        Line(detail.Summary.Headline);
        Line(detail.Summary.Detail); y += 8;
        Line(detail.Previous.Heading.Length == 0 ? Tr("US.Diagnostics.Section.Previous") : detail.Previous.Heading);
        Line(detail.Previous.EvaluationLine);
        Line(detail.Previous.DispatchLine); y += 8;
        if (ctx.Bindings.TryGet("diag-history", out IReadOnlyList<UsDiagEvent> history) && history.Count > 0)
        {
            Line(GetString(ctx.Bindings, "diag-history-identity"));
            long selected = ctx.Bindings.Get<long>("diag-event-selected");
            foreach (var entry in history)
            {
                if (Button(entry.Summary, "diag-event", entry.Sequence) != 0)
                    ctx.Bindings.Set("diag-event-selected", entry.Sequence);
                if (entry.Sequence == selected || selected < 0 && ReferenceEquals(entry, history[0])) Line(entry.Reason);
            }
            y += 8;
        }
        int firstBlock = UsDiagnosticsProjection.FirstCurrentBlockIndex(detail.Gates);
        foreach (UsDiagGateGroup group in Enum.GetValues(typeof(UsDiagGateGroup)))
        {
            bool any = false;
            foreach (var gate in detail.Gates) if (gate.Group == group) { any = true; break; }
            if (!any) continue;
            bool open = !ctx.Bindings.TryGet(UsDiagnosticsHost.GroupKey(group), out bool value) || value;
            string groupKey = group == UsDiagGateGroup.Game ? "US.Diagnostics.Gate.Group.Game" : group == UsDiagGateGroup.Rules
                ? "US.Diagnostics.Gate.Group.Rules" : "US.Diagnostics.Gate.Group.Audio";
            if (Button((open ? "− " : "+ ") + Tr(groupKey), "group-toggle", group) != 0)
                ctx.Bindings.Set(UsDiagnosticsHost.GroupKey(group), !open);
            if (!open) continue;
            // Preserve every supplied basis, including Rules/PreviousEvaluation and Unknown.
            foreach (UsDiagBasis basis in Enum.GetValues(typeof(UsDiagBasis)))
            {
                bool heading = false;
                for (int i = 0; i < detail.Gates.Count; i++)
                {
                    var gate = detail.Gates[i];
                    if (gate.Group != group || gate.Basis != basis) continue;
                    if (!heading) { Line(UsDiagnosticsProjection.BasisText(gate, Tr)); heading = true; }
                    float top = y;
                    GateHeading(gate);
                    Line(gate.Value, ctx.Theme[UiPaletteRole.Foreground], 8);
                    Line(UsDiagnosticsProjection.ReasonText(gate, Tr), ctx.Theme[UiPaletteRole.Foreground], 8);
                    if (paint)
                    {
                        var rail = new UiLayoutRect(rect.X, top, 2, y - top);
                        if (i == firstBlock) UsAttention.Rail(ctx.Drawing, rail, ctx.Theme);
                        else UiThemeDraw.Solid(ctx.Drawing, rail, ctx.Theme[UiPaletteRole.Divider]);
                    }
                    y += 6;
                }
            }
            y += 4;
        }
        if (Button(Tr("US.Diagnostics.Raw.Show"), UsDiagnosticsHost.KeyRawOpen) != 0)
            ctx.Bindings.Set(UsDiagnosticsHost.KeyRawOpen, true);
        return y - rect.Y + 4;
    }
}
/// <summary>The summary-row layout used by the list: dot | pawn | action | cooldown | audio | status.
/// Column widths live here so the uses never drift apart. The two numeric columns arrive with their
/// column advance (D6) so every row in one page right-aligns on the same edge. The status word is
/// NEUTRAL for a ready row and attention-cyan for a blocked one.</summary>
internal static class UsDiagRowPainter
{
    internal const float SingleLineMinimum = 352;
    private static string State(UsDiagRow row, UiWidgetContext ctx) => row.ObservationStatus
        ?? UsKernelDraw.Keyed(ctx, row.Ready ? "US.Diagnostics.Ready" : "US.Diagnostics.Blocked");
    private static string Summary(UsDiagRow row) => row.ActionText + " · " + row.CooldownText;
    internal static float Height(UiWidgetContext ctx, UsDiagRow row, float width)
    {
        var text = UsKernelDraw.Text(ctx);
        float available = Math.Max(1, width - 12);
        return text.MeasureText(row.PawnText, available) + text.MeasureText(State(row, ctx), available)
            + text.MeasureText(Summary(row), available) + 12;
    }
    internal static void Paint(UiLayoutRect rect, UsDiagRow row, UiWidgetContext ctx, float cooldownWidth = 0, float audioWidth = 0)
    {
        var text = UsKernelDraw.Text(ctx);
        float y = rect.Y + 4, width = Math.Max(1, rect.Width - 12);
        void Line(string value, UiColour ink)
        {
            float h = text.MeasureText(value, width);
            UsKernelDraw.TextForDrawing(ctx).Draw(new UiLayoutRect(rect.X + 6, y, width, h), value, ink, UiTextAnchor.UpperLeft);
            y += h;
        }
        Line(row.PawnText, ctx.Theme[UiPaletteRole.Foreground]);
        Line(State(row, ctx), row.Tone == UsDiagDotTone.Blocked ? UsAttention.Brush : ctx.Theme[UiPaletteRole.Foreground]);
        Line(Summary(row), ctx.Theme[UiPaletteRole.SecondaryForeground]);
    }
}
