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
    private const float ButtonHeight = 22f;
    private const float Gap = 4f;

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagToolbarWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Height", UiAttributeKind.Length), new UiAttributeDefinition("Scope"))));

    public override void Validate(IUiBindingAccess bindings, string elementPath)
    {
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeySeconds, elementPath);
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyCollapsed, elementPath);
        bindings.ValidateAction<int>(UsDiagnosticsHost.KeyLock, elementPath);
    }

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? 0f : ButtonHeight + Gap;

    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;

        var b = ctx.Bindings;
        float y = rect.Y;
        float x = rect.X;

        bool seconds = GetBool(b, UsDiagnosticsHost.KeySeconds);
        UiLayoutRect secondsRect = new(x, y, 40f, ButtonHeight);
        if (UsKernelDraw.SelectionButton(secondsRect, ctx, seconds ? "s" : "t", ctx.Theme, seconds, controlId: UsDiagnosticsHost.KeySeconds))
        {
            b.Set(UsDiagnosticsHost.KeySeconds, !seconds);
        }

        x += 40f + Gap;
        if (Scope == "main" && GetBool(b, UsDiagnosticsHost.KeyCanLock))
        {
            UiLayoutRect lockRect = new(x, y, 56f, ButtonHeight);
            if (UsKernelDraw.SelectionButton(lockRect, ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Lock"), ctx.Theme, false, controlId: UsDiagnosticsHost.KeyLock))
            {
                b.Invoke(UsDiagnosticsHost.KeyLock, 0);
            }

            x += 56f + Gap;
        }

        float collapseWidth = Math.Max(1f, rect.XMax - x);
        UiLayoutRect collapseRect = new(x, y, collapseWidth, ButtonHeight);
        if (UsKernelDraw.SelectionButton(collapseRect, ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar"), ctx.Theme, false, controlId: UsDiagnosticsHost.KeyCollapsed))
        {
            b.Set(UsDiagnosticsHost.KeyCollapsed, true);
        }
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
    private const float RowHeight = 24f;

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
        if (UsKernelDraw.SelectionButton(new UiLayoutRect(rect.X, rect.Y, rect.Width, RowHeight), ctx, label, ctx.Theme, false, controlId: UsDiagnosticsHost.KeyNavView))
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
/// layout). Row click routes through the source: a viewport pawn drills in via game selection, a
/// search-only (off-screen) hit locks and detaches a detail window. The same widget serves the wide
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
        => Math.Max(FieldHeight, ctx.Metrics.MeasureText("Ag", UiFont.Small, 10000f) + 4f);

    /// <summary>DX1.3: the 232px master column cannot carry six single-line columns without crushing
    /// the name; the wide navigation list can. The painter and this height arithmetic read ONE
    /// decision, so a row is never measured as two lines and drawn as one (or the reverse).</summary>
    internal static bool TwoLine(UiWidgetContext ctx) => ctx.ViewWidth < UsDiagRowPainter.SingleLineMinimum;

    internal static float RowHeightFor(UiWidgetContext ctx) => TwoLine(ctx) ? TwoLineRowHeight : RowHeight;

    internal static float BodyHeight(UiWidgetContext ctx)
        => FieldHeightFor(ctx) + UsDiagnosticsProjection.RowsPerPage * RowHeightFor(ctx);

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
        string query = DrawSearchField(new UiLayoutRect(rect.X, rect.Y, rect.Width, FieldHeightFor(ctx)), ctx);
        float y = rect.Y + FieldHeightFor(ctx);

        if (!b.TryGet(UsDiagnosticsHost.KeyRows, out IReadOnlyList<UsDiagRow>? rows) || rows == null || rows.Count == 0)
        {
            string note = query.Length > 0
                ? UsKernelDraw.Keyed(ctx, "US.Diagnostics.Search.None")
                : UsKernelDraw.Keyed(ctx, "US.Diagnostics.Panel.Empty");
            UsKernelDraw.Label(new UiLayoutRect(rect.X + 4f, y, Math.Max(1f, rect.Width - 8f), RowHeight), note, ctx, UiFont.Tiny, UiTextAnchor.MiddleLeft);
            return;
        }

        // D6: one advance per numeric column for the whole page, measured in the font the cells are
        // drawn with, so the column scans vertically instead of per-row.
        List<string> cooldowns = new(rows.Count);
        List<string> audios = new(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            cooldowns.Add(rows[i].CooldownText);
            audios.Add(rows[i].AudioText);
        }

        Func<string, float> measure = text => ctx.Metrics.MeasureWidth(text, UiFont.Tiny);
        float cooldownWidth = UsDiagnosticsProjection.NumericColumnWidth(cooldowns, measure);
        float audioWidth = UsDiagnosticsProjection.NumericColumnWidth(audios, measure);

        for (int i = 0; i < rows.Count; i++)
        {
            UsDiagRow row = rows[i];
            UiLayoutRect rowRect = new(rect.X, y + i * RowHeightFor(ctx), rect.Width, RowHeightFor(ctx));
            bool hovered = ctx.Controls.IsMouseOver(rowRect);
            // A locked row is unavailable, not selected: it takes the disabled rail (hatch) so the hatch
            // and the selected plane can never be confused for one another.
            UsKernelDraw.RowSurface(ctx.Drawing, rowRect, ctx.Theme, hovered, row.Locked ? UsKernelDraw.RowRail.Disabled : UsKernelDraw.RowRail.None);
            UsDiagRowPainter.Paint(rowRect, row, ctx, cooldownWidth, audioWidth);
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
            UsKernelDraw.Label(textRect, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Search.Placeholder"), ctx, UiFont.Small, UiTextAnchor.MiddleLeft);
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
    private const float RowHeight = 22f;
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
        float prevWidth = ctx.Metrics.MeasureWidth(prev, UiFont.Tiny) + ButtonPad * 2f;
        float nextWidth = ctx.Metrics.MeasureWidth(next, UiFont.Tiny) + ButtonPad * 2f;

        if (page > 0 && UsKernelDraw.SelectionButton(new UiLayoutRect(rect.X, rect.Y, prevWidth, RowHeight), ctx, prev, ctx.Theme, false, controlId: "page-previous", itemKey: page))
        {
            b.Set(UsDiagnosticsHost.KeyPage, page - 1);
        }

        if (page < pages - 1 && UsKernelDraw.SelectionButton(new UiLayoutRect(rect.XMax - nextWidth, rect.Y, nextWidth, RowHeight), ctx, next, ctx.Theme, false, controlId: "page-next", itemKey: page))
        {
            b.Set(UsDiagnosticsHost.KeyPage, page + 1);
        }

        float textX = rect.X + prevWidth + 4f;
        float textW = Math.Max(1f, rect.Width - prevWidth - nextWidth - 8f);
        UsKernelDraw.Label(
            new UiLayoutRect(textX, rect.Y, textW, RowHeight),
            string.Format(UsKernelDraw.Keyed(ctx, "US.Diagnostics.Page.Format"), page + 1, pages, total),
            ctx, UiFont.Tiny, UiTextAnchor.MiddleCenter);
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

    private const float HeaderHeight = 28f;
    private const float SectionHeight = 18f;
    private const float GroupTitleHeight = 18f;
    private const float BadgeWidth = 72f;
    private const float Gap = 4f;

    /// <summary>Leading indent of the condition rows: the rail plus the state shape live here.</summary>
    private const float Indent = 18f;
    private const float StatusWidth = 66f;
    private const float BasisWidth = 52f;
    private const float CellGap = 4f;
    private const float NameShare = 0.42f;

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagDetailWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Scope"))));

    public override void Validate(IUiBindingAccess bindings, string elementPath)
    {
        // Reads only (TryGet degrades); the lock action is validated by the toolbar that invokes it.
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyRawOpen, elementPath);
        // The three group fold switches are written by the headings this widget draws.
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyGroupGame, elementPath);
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyGroupRules, elementPath);
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyGroupAudio, elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx) => HeaderHeight + SectionHeight * 3f + LineHeight * 22f;

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? 0f : DetailHeight(ctx, ctx.ViewWidth);

    /// <summary>
    /// The detail column's measured height for a given arranged width. A one-label paragraph gets the
    /// lines it needs instead of a fixed band, so a long Chinese value or reason receives real height;
    /// no number here shrinks a font.
    /// </summary>
    internal static float DetailHeight(UiWidgetContext ctx, float arrangedWidth)
    {
        if (!ctx.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) || detail == null) return EmptyHeight(ctx);

        float width = Math.Max(1f, arrangedWidth);
        float content = Math.Max(1f, width - Indent);
        float height = HeaderHeight
            + SectionHeight + MeasuredTextHeight(ctx, detail.Summary.Headline, Math.Max(1f, width - 12f))
            + MeasuredTextHeight(ctx, detail.Summary.Detail, Math.Max(1f, width - 12f))
            + SectionHeight
            + MeasuredTextHeight(ctx, detail.Previous.EvaluationLine, content)
            + MeasuredTextHeight(ctx, detail.Previous.DispatchLine, content)
            + SectionHeight;

        for (int band = 0; band < BandCount; band++)
        {
            UsDiagGateGroup group = BandGroup(band);
            UsDiagBasis basis = BandBasis(band);
            if (!HasRows(detail, group, basis)) continue;
            if (!IsGroupOpen(ctx, group))
            {
                // A folded group keeps ONE heading (its first band's), so the developer still sees that
                // the group and its rows exist; its rows contribute no height at all.
                if (IsFirstBandOfGroup(band)) height += GroupTitleHeight;
                continue;
            }

            height += GroupTitleHeight;
            for (int i = 0; i < detail.Gates.Count; i++)
            {
                if (detail.Gates[i].Group != group || detail.Gates[i].Basis != basis) continue;
                height += RowHeight(ctx, detail.Gates[i], content);
            }
        }

        height += SectionHeight; // the raw-values disclosure
        if (GetBool(ctx.Bindings, UsDiagnosticsHost.KeyRawOpen)) height += LineHeight * 2f;
        return height + Gap;
    }

    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;
        DrawDetail(rect, ctx);
    }

    /// <summary>The detail content, reusable by the narrow navigation body.</summary>
    internal static void DrawDetail(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (!ctx.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) || detail == null)
        {
            DrawEmpty(rect, ctx);
            return;
        }

        // The widget's own geometry is measured, so a subject whose value shapes change must ask for a
        // re-measure instead of drawing into a stale band. The node's dirty flag is the engine's own
        // invalidation axis; the frame after the change gets the new geometry.
        float wanted = DetailHeight(ctx, ctx.ViewWidth);
        if (Math.Abs(wanted - rect.Height) > 0.5f)
        {
            ctx.Node?.MarkDirty();
        }

        float width = Math.Max(1f, rect.Width);
        float content = Math.Max(1f, width - Indent);
        float y = rect.Y;

        // 1. Identity + readiness. The badge is NEUTRAL for a ready pawn and attention-cyan for a
        // blocked one: a diagnostic block is not a destructive action and must not borrow the danger
        // family the carrier's Warning/Danger alias would give it.
        UiLayoutRect badgeRect = new(rect.XMax - BadgeWidth, y + 3f, BadgeWidth, HeaderHeight - 6f);
        if (detail.Ready)
        {
            UiThemeDraw.Surface(ctx.Drawing, badgeRect, ctx.Theme, ctx.Theme[UiPaletteRole.ControlSurface], ctx.Theme[UiPaletteRole.Border]);
            UsKernelDraw.Label(badgeRect, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Ready"), ctx, ctx.Theme[UiPaletteRole.Foreground], UiFont.Tiny, UiTextAnchor.MiddleCenter, singleLine: true);
        }
        else
        {
            UsAttention.Badge(ctx.Drawing, badgeRect, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Blocked"), ctx.Theme);
        }

        float labelWidth = Math.Max(1f, badgeRect.X - rect.X - 8f);
        UsKernelDraw.Label(
            new UiLayoutRect(rect.X, y, labelWidth, HeaderHeight),
            UsKernelDraw.Ellipsized(detail.PawnText, ctx, UiFont.Small, labelWidth),
            ctx, UiFont.Small, UiTextAnchor.MiddleLeft);
        if (detail.Locked)
        {
            UsKernelDraw.Label(
                new UiLayoutRect(badgeRect.X - 44f, y + 5f, 40f, 18f),
                UsKernelDraw.Keyed(ctx, "US.Diagnostics.LockedTag"),
                ctx, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.MiddleRight);
        }

        y += HeaderHeight;

        // 2. Current observations: the summary speaks ONLY for rows that are current observations, and
        // its own quote may be "no block found" or "state incomplete" - never a remembered failure.
        y = Section(y, rect, ctx, "US.Diagnostics.Section.Current");
        y = DrawSummary(y, rect, ctx, detail.Summary);

        // 3. Previous evaluation / last dispatch, in its own band with its own recency.
        y = Section(y, rect, ctx, "US.Diagnostics.Section.Previous");
        if (detail.Previous.EvaluationLine.Length > 0)
        {
            y = Wrapped(y, rect, ctx, detail.Previous.EvaluationLine, ctx.Theme[UiPaletteRole.Foreground]);
        }

        if (detail.Previous.DispatchLine.Length > 0)
        {
            y = Wrapped(y, rect, ctx, detail.Previous.DispatchLine, ctx.Theme[UiPaletteRole.SecondaryForeground]);
        }

        // 4. The conditions, grouped by side and split by clock basis inside a group.
        UsKernelDraw.Label(
            new UiLayoutRect(rect.X, y, rect.Width, SectionHeight),
            string.Format(UsKernelDraw.Keyed(ctx, "US.Diagnostics.Section.ChainCount"), detail.Gates.Count),
            ctx,
            UiFont.Tiny,
            UiTextAnchor.MiddleLeft,
            singleLine: true);
        y += SectionHeight;
        int firstCurrentBlock = UsDiagnosticsProjection.FirstCurrentBlockIndex(detail.Gates);
        for (int band = 0; band < BandCount; band++)
        {
            UsDiagGateGroup group = BandGroup(band);
            UsDiagBasis basis = BandBasis(band);
            if (!HasRows(detail, group, basis)) continue;

            bool open = IsGroupOpen(ctx, group);
            if (!open && !IsFirstBandOfGroup(band)) continue;

            // Composed from two real keys rather than one format key: the group name must survive a host
            // that resolves no placeholder (the harness's own translation seam), and the basis word is
            // the same one each row's basis cell shows.
            string groupName = UsKernelDraw.Keyed(ctx, GroupKey(group));
            string heading = open
                ? "[-] " + groupName + " · " + UsKernelDraw.Keyed(ctx,
                    basis == UsDiagBasis.CurrentObservation ? "US.Diagnostics.Basis.Current" : "US.Diagnostics.Basis.Previous")
                : "[+] " + groupName;
            UiLayoutRect headingRect = new(rect.X, y, rect.Width, GroupTitleHeight);
            if (UsKernelDraw.SelectionButton(headingRect, ctx, heading, ctx.Theme, false, controlId: "group-toggle", itemKey: (group, basis)))
            {
                // The ONLY writer of this switch: the developer's click. A value update cannot reach it.
                ctx.Bindings.Set(UsDiagnosticsHost.GroupKey(group), !open);
            }

            y += GroupTitleHeight;
            if (!open) continue;

            for (int i = 0; i < detail.Gates.Count; i++)
            {
                UsDiagGateLine gate = detail.Gates[i];
                if (gate.Group != group || gate.Basis != basis) continue;
                y = DrawGate(y, rect, ctx, gate, content, i == firstCurrentBlock);
            }
        }

        // 5. Raw values: folded away by default - they are evidence, not the answer.
        bool rawOpen = GetBool(ctx.Bindings, UsDiagnosticsHost.KeyRawOpen);
        UiLayoutRect rawToggle = new(rect.X, y, rect.Width, SectionHeight);
        if (UsKernelDraw.SelectionButton(rawToggle, ctx, UsKernelDraw.Keyed(ctx, rawOpen ? "US.Diagnostics.Raw.Hide" : "US.Diagnostics.Raw.Show"), ctx.Theme, false, controlId: UsDiagnosticsHost.KeyRawOpen))
        {
            ctx.Bindings.Set(UsDiagnosticsHost.KeyRawOpen, !rawOpen);
        }

        y += SectionHeight;
        if (rawOpen)
        {
            y = FieldRow(y, rect, ctx, "US.Diagnostics.Field.CurrentAction", detail.ActionText);
            y = FieldRow(y, rect, ctx, "US.Diagnostics.Field.LastDispatch", detail.AudioText);
        }
    }

    private static void DrawEmpty(UiLayoutRect rect, UiWidgetContext ctx)
    {
        // "Current state incomplete" is the honest headline when there is no subject at all; the second
        // line says what to do about it. Neither is a pass state and neither implies "not reached".
        float y = rect.Y + 8f;
        UsKernelDraw.Label(new UiLayoutRect(rect.X + 8f, y, Math.Max(1f, rect.Width - 16f), LineHeight),
            UsKernelDraw.Keyed(ctx, "US.Diagnostics.Summary.Incomplete"), ctx, UiFont.Small, UiTextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new UiLayoutRect(rect.X + 8f, y + LineHeight, Math.Max(1f, rect.Width - 16f), LineHeight),
            UsKernelDraw.Keyed(ctx, "US.Diagnostics.Detail.Empty"), ctx, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.MiddleLeft);
    }

    private static float EmptyHeight(UiWidgetContext ctx) => LineHeight * 2f + 16f;

    /// <summary>True while at least one row of this (group, basis) band exists in the current chain.</summary>
    private static bool HasRows(UsDiagDetail detail, UsDiagGateGroup group, UsDiagBasis basis)
    {
        for (int i = 0; i < detail.Gates.Count; i++)
        {
            if (detail.Gates[i].Group == group && detail.Gates[i].Basis == basis) return true;
        }

        return false;
    }

    /// <summary>
    /// Which band is the group's first. A folded group draws exactly one heading, at its first band, so
    /// the four (group, basis) bands never produce two collapsed headings for one group.
    /// </summary>
    private static bool IsFirstBandOfGroup(int band) => band == 0 || band == 1 || band == 2;

    /// <summary>The group's fold switch: page state, all three open by default, only a click writes it.</summary>
    private static bool IsGroupOpen(UiWidgetContext ctx, UsDiagGateGroup group)
        => !ctx.Bindings.TryGet(UsDiagnosticsHost.GroupKey(group), out bool open) || open;

    private static float DrawSummary(float y, UiLayoutRect rect, UiWidgetContext ctx, UsDiagCurrentSummary summary)
    {
        float width = Math.Max(1f, rect.Width - 12f);
        float headlineHeight = MeasuredTextHeight(ctx, summary.Headline, width);
        if (summary.HasCurrentBlock)
        {
            // The primary finding gets the attention treatment: a neutral fill with a cyan border and a
            // cyan label. It is the ONE place the attention substrate is used as a band.
            UsAttention.Band(ctx.Drawing, new UiLayoutRect(rect.X, y, rect.Width, headlineHeight), ctx.Theme);
            UsKernelDraw.Label(
                new UiLayoutRect(rect.X + 6f, y, Math.Max(1f, rect.Width - 12f), headlineHeight),
                summary.Headline,
                ctx,
                UsAttention.Brush,
                UiFont.Tiny,
                UiTextAnchor.MiddleLeft);
        }
        else
        {
            UsKernelDraw.Label(new UiLayoutRect(rect.X, y, width, headlineHeight), summary.Headline, ctx, ctx.Theme[UiPaletteRole.Foreground], UiFont.Tiny, UiTextAnchor.MiddleLeft);
        }

        y += headlineHeight;
        if (summary.Detail.Length > 0)
        {
            float detailHeight = MeasuredTextHeight(ctx, summary.Detail, width);
            UsKernelDraw.Label(new UiLayoutRect(rect.X, y, width, detailHeight), summary.Detail, ctx, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.MiddleLeft);
            y += detailHeight;
        }

        return y;
    }

    /// <summary>
    /// One condition row. Layout is status | name | value | basis, with an indented reason line when the
    /// row has one (an N/A explanation or a Pending reason). The value column takes what is left, wraps to
    /// a measured number of lines and keeps neutral ink; the status cell is the ONLY place the attention
    /// marker appears.
    /// </summary>
    private static float DrawGate(float y, UiLayoutRect rect, UiWidgetContext ctx, UsDiagGateLine gate, float contentWidth, bool firstCurrentBlock)
    {
        Func<string, string> tr = key => UsKernelDraw.Keyed(ctx, key);
        float rowSpan = RowSpan(ctx, gate, contentWidth);
        float reasonSpan = ReasonSpan(ctx, gate, contentWidth, tr);
        float rowHeight = rowSpan + reasonSpan + 2f;
        float lineSpan = rowSpan;
        float nameWidth = Math.Max(1f, (contentWidth - StatusWidth - BasisWidth - CellGap * 3f) * NameShare);
        float valueWidth = Math.Max(1f, contentWidth - StatusWidth - BasisWidth - nameWidth - CellGap * 3f);
        float x = rect.X + Indent;
        float right = rect.X + Indent + contentWidth;

        // The rail: attention cyan on the first CURRENT block (the row the summary quotes), the plain
        // divider elsewhere. "First" is carried by this rail, never by repeating the status word.
        if (firstCurrentBlock)
        {
            UsAttention.Rail(ctx.Drawing, new UiLayoutRect(rect.X, y, 2f, rowHeight), ctx.Theme);
        }
        else
        {
            UiThemeDraw.Solid(ctx.Drawing, new UiLayoutRect(rect.X, y, 2f, rowHeight), ctx.Theme[UiPaletteRole.Divider]);
        }

        UsKernelDraw.Label(new UiLayoutRect(x, y, StatusWidth, lineSpan), gate.Status, ctx, UsDiagPaint.Status(ctx.Theme, gate.State), UiFont.Tiny, UiTextAnchor.MiddleLeft);
        UsKernelDraw.Label(new UiLayoutRect(x + StatusWidth + CellGap, y, nameWidth, lineSpan), gate.Name, ctx, UiFont.Tiny, UiTextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new UiLayoutRect(x + StatusWidth + CellGap + nameWidth + CellGap, y, valueWidth, lineSpan),
            gate.Value,
            ctx,
            UsDiagPaint.Value(ctx.Theme, gate.State),
            UiFont.Tiny,
            UiTextAnchor.MiddleRight,
            singleLine: false);
        UsKernelDraw.Label(
            new UiLayoutRect(right - BasisWidth, y, BasisWidth, lineSpan),
            UsDiagnosticsProjection.BasisText(gate, tr),
            ctx, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.MiddleRight, singleLine: true);

        if (reasonSpan > 0f)
        {
            float reasonWidth = Math.Max(1f, contentWidth - 12f);
            UsKernelDraw.Label(
                new UiLayoutRect(x + 12f, y + lineSpan, reasonWidth, reasonSpan),
                UsDiagnosticsProjection.ReasonText(gate, tr),
                ctx, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: false);
        }

        return y + rowHeight;
    }

    private static bool HasReason(UsDiagGateLine gate)
        => gate.State == UsDiagGateState.NA || gate.State == UsDiagGateState.Pending;

    /// <summary>
    /// A condition row's measured height: the taller of the wrapped name and the wrapped value, plus
    /// the reason block when the row has one - all through MeasuredTextHeight, so measure and draw
    /// share one ruler and one width (DX1.5). Long Chinese values therefore receive real height
    /// instead of being clipped or shrunk.
    /// </summary>
    private static float RowHeight(UiWidgetContext ctx, UsDiagGateLine gate, float contentWidth)
        => RowSpan(ctx, gate, contentWidth)
            + ReasonSpan(ctx, gate, contentWidth, key => UsKernelDraw.Keyed(ctx, key)) + 2f;

    private static float RowSpan(UiWidgetContext ctx, UsDiagGateLine gate, float contentWidth)
    {
        float nameWidth = Math.Max(1f, (contentWidth - StatusWidth - BasisWidth - CellGap * 3f) * NameShare);
        float valueWidth = Math.Max(1f, contentWidth - StatusWidth - BasisWidth - nameWidth - CellGap * 3f);
        return Math.Max(MeasuredTextHeight(ctx, gate.Name, nameWidth), MeasuredTextHeight(ctx, gate.Value, valueWidth));
    }

    private static float ReasonSpan(UiWidgetContext ctx, UsDiagGateLine gate, float contentWidth, Func<string, string> tr)
        => HasReason(gate) ? MeasuredTextHeight(ctx, UsDiagnosticsProjection.ReasonText(gate, tr), Math.Max(1f, contentWidth - 12f)) : 0f;

    private const int BandCount = 4;

    private static UsDiagGateGroup BandGroup(int band) => band switch
    {
        0 => UsDiagGateGroup.Game,
        1 => UsDiagGateGroup.Rules,
        2 => UsDiagGateGroup.Audio,
        _ => UsDiagGateGroup.Audio,
    };

    private static UsDiagBasis BandBasis(int band)
        => band < 3 ? UsDiagBasis.CurrentObservation : UsDiagBasis.PreviousEvaluation;

    private static string GroupKey(UsDiagGateGroup group) => group switch
    {
        UsDiagGateGroup.Game => "US.Diagnostics.Gate.Group.Game",
        UsDiagGateGroup.Rules => "US.Diagnostics.Gate.Group.Rules",
        _ => "US.Diagnostics.Gate.Group.Audio",
    };

    private static float Section(float y, UiLayoutRect rect, UiWidgetContext ctx, string key)
    {
        UsKernelDraw.Label(new UiLayoutRect(rect.X, y, rect.Width, SectionHeight), UsKernelDraw.Keyed(ctx, key), ctx, UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: true);
        return y + SectionHeight;
    }

    private static float Wrapped(float y, UiLayoutRect rect, UiWidgetContext ctx, string text, UiColour color)
    {
        float width = Math.Max(1f, rect.Width - Indent);
        float height = MeasuredTextHeight(ctx, text, width);
        UsKernelDraw.Label(new UiLayoutRect(rect.X + Indent, y, width, height), text, ctx, color, UiFont.Tiny, UiTextAnchor.MiddleLeft);
        return y + height;
    }

    private static float FieldRow(float y, UiLayoutRect rect, UiWidgetContext ctx, string labelKey, string value)
    {
        float nameWidth = rect.Width * 0.38f - 4f;
        float valueX = rect.X + nameWidth + 4f;
        float valueWidth = Math.Max(1f, rect.Width - nameWidth - 4f);
        float valueHeight = MeasuredTextHeight(ctx, value, valueWidth);
        UsKernelDraw.Label(new UiLayoutRect(rect.X, y, nameWidth, LineHeight), UsKernelDraw.Keyed(ctx, labelKey), ctx, UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new UiLayoutRect(valueX, y, valueWidth, valueHeight), value, ctx, UiFont.Tiny, UiTextAnchor.MiddleRight, singleLine: false);
        return y + Math.Max(LineHeight, valueHeight);
    }
}

/// <summary>The summary-row layout used by the list: dot | pawn | action | cooldown | audio | status.
/// Column widths live here so the uses never drift apart. The two numeric columns arrive with their
/// column advance (D6) so every row in one page right-aligns on the same edge. The status word is
/// NEUTRAL for a ready row and attention-cyan for a blocked one.</summary>
internal static class UsDiagRowPainter
{
    private const float DotWidth = 16f;
    private const float ActionWidth = 48f;
    private const float CooldownWidth = 84f;
    private const float AudioWidth = 44f;
    private const float StatusWidth = 40f;
    private const float CellGap = 4f;
    private const float TwoLineHeight = 18f;

    /// <summary>DX1.3: the narrowest row that can carry the six single-line columns without stealing
    /// the name's real budget: dot + an 80px name floor + action + cooldown + audio + status + five
    /// gaps. Below it the row splits into two lines instead of crushing the identity - the measured
    /// 232px defect had pawnWidth = max(1, negative) with the action column invading the dot.</summary>
    internal const float SingleLineMinimum = DotWidth + 80f + ActionWidth + CooldownWidth + AudioWidth + StatusWidth + 5f * CellGap;

    internal static void Paint(UiLayoutRect rect, UsDiagRow row, UiWidgetContext ctx, float cooldownWidth = 0f, float audioWidth = 0f)
    {
        if (rect.Width < SingleLineMinimum)
        {
            PaintTwoLine(rect, row, ctx, cooldownWidth, audioWidth);
            return;
        }

        UiColour dotColor = UsDiagPaint.Dot(ctx.Theme, row.Tone);
        float x = rect.X + 2f;
        // D7: the dot is a glyph until the font says it cannot draw one, then it is the state word.
        string dot = UsDiagGlyphs.DotText(ctx, row.Tone);
        UsKernelDraw.Label(new UiLayoutRect(x, rect.Y, DotWidth, rect.Height), dot, ctx, dotColor, UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: true);
        x += DotWidth;

        float statusX = rect.XMax - StatusWidth;
        float audioX = statusX - AudioWidth - CellGap;
        float cooldownX = audioX - CooldownWidth - CellGap;
        float actionX = cooldownX - ActionWidth - CellGap;
        float pawnWidth = Math.Max(1f, actionX - x - CellGap);

        Func<string, float> measure = text => ctx.Metrics.MeasureWidth(text, UiFont.Tiny);
        UsKernelDraw.Label(new UiLayoutRect(x, rect.Y, pawnWidth, rect.Height),
            UsKernelDraw.Ellipsized(row.PawnText, ctx, UiFont.Tiny, pawnWidth),
            ctx, UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new UiLayoutRect(actionX, rect.Y, ActionWidth, rect.Height),
            UsKernelDraw.Ellipsized(row.ActionText, ctx, UiFont.Tiny, ActionWidth),
            ctx, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new UiLayoutRect(cooldownX, rect.Y, CooldownWidth, rect.Height),
            UsKernelDraw.Ellipsized(UsDiagnosticsProjection.PadNumeric(row.CooldownText, cooldownWidth, measure), ctx, UiFont.Tiny, CooldownWidth),
            ctx, UiFont.Tiny, UiTextAnchor.MiddleRight, singleLine: true);
        UsKernelDraw.Label(new UiLayoutRect(audioX, rect.Y, AudioWidth, rect.Height),
            UsKernelDraw.Ellipsized(UsDiagnosticsProjection.PadNumeric(row.AudioText, audioWidth, measure), ctx, UiFont.Tiny, AudioWidth),
            ctx, UiFont.Tiny, UiTextAnchor.MiddleRight, singleLine: true);
        UsKernelDraw.Label(new UiLayoutRect(statusX, rect.Y, StatusWidth, rect.Height),
            UsKernelDraw.Keyed(ctx, row.Ready ? "US.Diagnostics.Ready" : "US.Diagnostics.Blocked"),
            ctx, row.Ready ? ctx.Theme[UiPaletteRole.SecondaryForeground] : UsAttention.Brush, UiFont.Tiny, UiTextAnchor.MiddleRight, singleLine: true);
    }

    /// <summary>DX1.3: below the single-line budget the row splits. Line one keeps the IDENTITY -
    /// dot, the pawn name at the wide share (explicitly ellipsized, never dimmed away), status; line
    /// two carries the numbers on the SAME measured column budgets - action left, cooldown and audio
    /// right-aligned. Every cell clips through Ellipsized at exactly the width it measured, so values
    /// cannot bleed into each other and the caller's rowRect stays the one hit band.</summary>
    private static void PaintTwoLine(UiLayoutRect rect, UsDiagRow row, UiWidgetContext ctx, float cooldownWidth, float audioWidth)
    {
        UiColour dotColor = UsDiagPaint.Dot(ctx.Theme, row.Tone);
        Func<string, float> measure = text => ctx.Metrics.MeasureWidth(text, UiFont.Tiny);
        var first = new UiLayoutRect(rect.X, rect.Y, rect.Width, TwoLineHeight);
        var second = new UiLayoutRect(rect.X, rect.Y + TwoLineHeight, rect.Width, Math.Max(1f, rect.Height - TwoLineHeight));

        float x = first.X + 2f;
        string dot = UsDiagGlyphs.DotText(ctx, row.Tone);
        UsKernelDraw.Label(new UiLayoutRect(x, first.Y, DotWidth, first.Height), dot, ctx, dotColor, UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: true);
        x += DotWidth;

        float pawnWidth = Math.Max(1f, first.XMax - StatusWidth - CellGap - x);
        UsKernelDraw.Label(new UiLayoutRect(x, first.Y, pawnWidth, first.Height),
            UsKernelDraw.Ellipsized(row.PawnText, ctx, UiFont.Tiny, pawnWidth),
            ctx, UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new UiLayoutRect(first.XMax - StatusWidth, first.Y, StatusWidth, first.Height),
            UsKernelDraw.Keyed(ctx, row.Ready ? "US.Diagnostics.Ready" : "US.Diagnostics.Blocked"),
            ctx, row.Ready ? ctx.Theme[UiPaletteRole.SecondaryForeground] : UsAttention.Brush, UiFont.Tiny, UiTextAnchor.MiddleRight, singleLine: true);

        float cw = Mathf.Clamp(cooldownWidth > 0f ? cooldownWidth : CooldownWidth, 24f, CooldownWidth);
        float aw = Mathf.Clamp(audioWidth > 0f ? audioWidth : AudioWidth, 20f, AudioWidth);
        float audioX = second.XMax - aw;
        float cooldownX = audioX - CellGap - cw;
        float actionWidth = Math.Max(1f, cooldownX - CellGap - (second.X + 2f));
        UsKernelDraw.Label(new UiLayoutRect(second.X + 2f, second.Y, actionWidth, second.Height),
            UsKernelDraw.Ellipsized(row.ActionText, ctx, UiFont.Tiny, actionWidth),
            ctx, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new UiLayoutRect(cooldownX, second.Y, cw, second.Height),
            UsKernelDraw.Ellipsized(UsDiagnosticsProjection.PadNumeric(row.CooldownText, cw, measure), ctx, UiFont.Tiny, cw),
            ctx, UiFont.Tiny, UiTextAnchor.MiddleRight, singleLine: true);
        UsKernelDraw.Label(new UiLayoutRect(audioX, second.Y, aw, second.Height),
            UsKernelDraw.Ellipsized(UsDiagnosticsProjection.PadNumeric(row.AudioText, aw, measure), ctx, UiFont.Tiny, aw),
            ctx, UiFont.Tiny, UiTextAnchor.MiddleRight, singleLine: true);
    }
}
