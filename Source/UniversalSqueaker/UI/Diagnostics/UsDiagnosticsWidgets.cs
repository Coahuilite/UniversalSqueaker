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
/// <see cref="UiNative"/> only: the family is written so the panel files can LEAVE the gate 14
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

    public abstract void Validate(IUiBindings bindings, string elementPath);

    public float Measure(UiWidgetContext ctx)
    {
        return UiSessionGuard.MeasureOrFallback(
            ctx.Session, spec.Id, Kind, ctx.ElementPath, FallbackHeight(ctx), () => MeasureBody(ctx));
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;
        UiSessionGuard.DrawOrFallback(
            ctx.Session,
            spec.Id,
            Kind,
            ctx.ElementPath,
            rect,
            () => DrawBody(rect, ctx),
            fallback => DrawFallback(fallback, ctx));
    }

    protected abstract float MeasureBody(UiWidgetContext ctx);
    protected abstract void DrawBody(Rect rect, UiWidgetContext ctx);
    protected virtual float FallbackHeight(UiWidgetContext ctx) => 24f;

    protected virtual void DrawFallback(Rect rect, UiWidgetContext ctx)
    {
        UsKernelDraw.Label(rect, "[" + Kind + "]", ctx, UiFont.Tiny, TextAnchor.MiddleCenter);
    }

    protected static bool IsCollapsed(UiWidgetContext ctx)
        => ctx.Bindings.TryGet(UsDiagnosticsHost.KeyCollapsed, out bool collapsed) && collapsed;

    protected string Scope => spec.TryGetAttribute("Scope", out string scope) ? scope : "main";

    protected static bool GetBool(IUiBindings b, string key) => b.TryGet(key, out bool v) && v;
    protected static int GetInt(IUiBindings b, string key) => b.TryGet(key, out int v) ? v : 0;
    protected static string GetString(IUiBindings b, string key) => b.TryGet(key, out string v) ? v ?? string.Empty : string.Empty;

    /// <summary>Line count a wrapped string needs in this font, clamped so one paragraph cannot eat the page.</summary>
    protected static int LineCount(UiWidgetContext ctx, string text, float width, int maxLines)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        float height = ctx.Metrics.MeasureText(text, UiFont.Tiny, Math.Max(1f, width));
        int lines = (int)Math.Ceiling(height / LineHeight);
        return lines < 1 ? 1 : lines > maxLines ? maxLines : lines;
    }

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
    internal static Color Status(UiTheme theme, UsDiagGateState state) => state switch
    {
        UsDiagGateState.Block => UsAttention.Brush,
        UsDiagGateState.Pass => theme.TextPrimary,
        UsDiagGateState.Pending => theme.TextSecondary,
        _ => theme.TextSecondary,
    };

    /// <summary>The value cell is always neutral: a blocked condition must not colour its evidence.</summary>
    internal static Color Value(UiTheme theme, UsDiagGateState state)
        => state == UsDiagGateState.NA ? theme.TextSecondary : theme.TextPrimary;

    /// <summary>Pawn-row / bar activity dot. Blocked is the attention role; the rest is neutral ink.</summary>
    internal static Color Dot(UiTheme theme, UsDiagDotTone tone) => tone switch
    {
        UsDiagDotTone.Ready => theme.Success,
        UsDiagDotTone.Blocked => UsAttention.Brush,
        _ => theme.TextSecondary,
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

    public static void Register() => UiWidgetRegistry.Register(
        UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagToolbarWidget(),
        new[] { "Id", "Kind", "Height", "Scope" });

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeySeconds, elementPath);
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyCollapsed, elementPath);
        bindings.ValidateAction<int>(UsDiagnosticsHost.KeyLock, elementPath);
    }

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? 0f : ButtonHeight + Gap;

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;

        var b = ctx.Bindings;
        float y = rect.y;
        float x = rect.x;

        bool seconds = GetBool(b, UsDiagnosticsHost.KeySeconds);
        Rect secondsRect = new(x, y, 40f, ButtonHeight);
        if (UsKernelDraw.SelectionButton(secondsRect, ctx, seconds ? "s" : "t", ctx.Theme, seconds))
        {
            b.Set(UsDiagnosticsHost.KeySeconds, !seconds);
        }

        x += 40f + Gap;
        if (Scope == "main" && GetBool(b, UsDiagnosticsHost.KeyCanLock))
        {
            Rect lockRect = new(x, y, 56f, ButtonHeight);
            if (UsKernelDraw.SelectionButton(lockRect, ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Lock"), ctx.Theme, false))
            {
                b.Invoke(UsDiagnosticsHost.KeyLock, 0);
            }

            x += 56f + Gap;
        }

        float collapseWidth = Math.Max(1f, rect.xMax - x);
        Rect collapseRect = new(x, y, collapseWidth, ButtonHeight);
        if (UsKernelDraw.SelectionButton(collapseRect, ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar"), ctx.Theme, false))
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

    public static void Register() => UiWidgetRegistry.Register(
        UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagNavWidget(),
        new[] { "Id", "Kind", "Height", "Scope" });

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<UsDiagNavView>(UsDiagnosticsHost.KeyNavView, elementPath);
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyShowBack, elementPath);
    }

    protected override float MeasureBody(UiWidgetContext ctx)
        => IsCollapsed(ctx) || !ShowBack(ctx) ? 0f : RowHeight;

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx) || !ShowBack(ctx)) return;

        string label = UsDiagGlyphs.PrevText(ctx) + " " + UsKernelDraw.Keyed(ctx, "US.Diagnostics.Nav.Back");
        if (UsKernelDraw.SelectionButton(new Rect(rect.x, rect.y, rect.width, RowHeight), ctx, label, ctx.Theme, false))
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

    public static void Register() => UiWidgetRegistry.Register(
        UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagNavBodyWidget(),
        new[] { "Id", "Kind", "Height", "Scope" });

    public override void Validate(IUiBindings bindings, string elementPath)
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
            ? UsDiagListWidget.BodyHeight + UsDiagPagerWidget.BodyHeight
            : UsDiagDetailWidget.DetailHeight(ctx, ctx.ViewWidth);

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;
        if (!IsListView(ctx))
        {
            UsDiagDetailWidget.DrawDetail(rect, ctx);
            return;
        }

        UsDiagListWidget.DrawBodyCore(new Rect(rect.x, rect.y, rect.width, UsDiagListWidget.BodyHeight), ctx);
        UsDiagPagerWidget.DrawBodyCore(
            new Rect(rect.x, rect.y + UsDiagListWidget.BodyHeight, rect.width, UsDiagPagerWidget.BodyHeight),
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

    /// <summary>09 §3.3's authorised geometry: 26 -> 32, dev-only panel.</summary>
    internal const float BarHeight = 32f;

    private const float Padding = 8f;

    /// <summary>The bar's own two-line rhythm: TopPad 2 + 14 + 14 = 30 inside the 32px strip.</summary>
    private const float StripLineHeight = 14f;
    private const float TopPad = 2f;
    private const float Gap = 6f;
    private const float DotWidth = 14f;
    private const float ActionGap = 4f;
    private const float ActionPad = 8f;

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Register(
        UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagBarWidget(),
        new[] { "Id", "Kind", "Height", "Scope" });

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyCollapsed, elementPath);
        bindings.ValidateValue<UsDiagBarModel>(UsDiagnosticsHost.KeyBar, elementPath);
        bindings.ValidateCommand(UsDiagnosticsHost.KeyClose, elementPath);
    }

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? BarHeight : 0f;

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (!IsCollapsed(ctx)) return;

        var b = ctx.Bindings;
        if (!b.TryGet(UsDiagnosticsHost.KeyBar, out UsDiagBarModel? bar) || bar == null) return;

        bool hovered = UiNative.IsMouseOver(rect);
        UiThemeDraw.Surface(rect, ctx.Theme, hovered ? ctx.Theme.Hover : ctx.Theme.Panel, ctx.Theme.Border);

        float expandWidth = MeasureAction(ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar.Expand"));
        float closeWidth = MeasureAction(ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar.Close"));
        float actionsWidth = expandWidth + closeWidth + ActionGap;

        float innerLeft = rect.x + Padding;
        float innerRight = rect.xMax - Padding;
        float lineTop = rect.y + TopPad;

        // Line 2 actions are laid out first: they are unconditional, the activity sentence is not.
        Rect closeRect = new(innerRight - closeWidth, lineTop + StripLineHeight, closeWidth, StripLineHeight);
        Rect expandRect = new(closeRect.x - ActionGap - expandWidth, lineTop + StripLineHeight, expandWidth, StripLineHeight);

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
        UsKernelDraw.Label(new Rect(innerLeft, lineTop, identityWidth, StripLineHeight), identity, ctx, ctx.Theme.TextPrimary, UiFont.Small, TextAnchor.MiddleLeft, singleLine: true);
        float switchX = innerLeft + ctx.Metrics.MeasureWidth(identity, UiFont.Small) + Gap;
        UsKernelDraw.Label(new Rect(switchX, lineTop, Math.Max(1f, innerRight - switchX), StripLineHeight), bar.SwitchText, ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        if (layout.ShowScale)
        {
            float scaleWidth = ctx.Metrics.MeasureWidth(bar.Scale, UiFont.Tiny);
            UsKernelDraw.Label(new Rect(innerRight - scaleWidth, lineTop, scaleWidth, StripLineHeight), bar.Scale, ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);
        }

        // Line 2: the activity sentence (first to go when narrow) and the two actions.
        if (layout.ShowActivity)
        {
            string dot = UsDiagGlyphs.DotText(ctx, bar.ActivityTone);
            Rect dotRect = new(innerLeft, lineTop + StripLineHeight, DotWidth, StripLineHeight);
            UsKernelDraw.Label(dotRect, dot, ctx, UsDiagPaint.Dot(ctx.Theme, bar.ActivityTone), UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
            float textX = dotRect.xMax;
            float textWidth = Math.Max(1f, expandRect.x - Gap - textX);
            UsKernelDraw.Label(
                new Rect(textX, lineTop + StripLineHeight, textWidth, StripLineHeight),
                UsKernelDraw.Ellipsized(bar.Activity, ctx, UiFont.Tiny, textWidth),
                ctx, ctx.Theme.TextPrimary, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        }

        if (UsKernelDraw.SelectionButton(expandRect, ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar.Expand"), ctx.Theme, false))
        {
            b.Set(UsDiagnosticsHost.KeyCollapsed, false);
        }

        if (UsKernelDraw.SelectionButton(closeRect, ctx, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar.Close"), ctx.Theme, false))
        {
            // A command binding, invoked through its own overload: the typed overload would look for a
            // payload action and throw inside the draw pass.
            b.Invoke(UsDiagnosticsHost.KeyClose);
        }

        // The whole strip still expands; the two buttons above capture their own clicks first.
        if (UiNative.Button(rect, ctx))
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
    private const string SearchStateId = "diag-search-field";

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Register(
        UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagListWidget(),
        new[] { "Id", "Kind" });

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<string>(UsDiagnosticsHost.KeySearch, elementPath);
        bindings.ValidateAction<int>(UsDiagnosticsHost.KeyRowClick, elementPath);
    }

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? 0f : BodyHeight;

    /// <summary>The search field plus one page of rows; the navigation body composes the same number.</summary>
    internal static float BodyHeight => FieldHeight + UsDiagnosticsProjection.RowsPerPage * RowHeight;

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;
        DrawBodyCore(new Rect(rect.x, rect.y, rect.width, BodyHeight), ctx);
    }

    /// <summary>
    /// The list's own content, reusable by the narrow navigation body (which stacks the pager under it).
    /// It draws the search field and one page of rows into exactly <see cref="BodyHeight"/> pixels.
    /// </summary>
    internal static void DrawBodyCore(Rect rect, UiWidgetContext ctx)
    {
        var b = ctx.Bindings;
        string query = DrawSearchField(new Rect(rect.x, rect.y, rect.width, FieldHeight), ctx);
        float y = rect.y + FieldHeight;

        if (!b.TryGet(UsDiagnosticsHost.KeyRows, out IReadOnlyList<UsDiagRow>? rows) || rows == null || rows.Count == 0)
        {
            string note = query.Length > 0
                ? UsKernelDraw.Keyed(ctx, "US.Diagnostics.Search.None")
                : UsKernelDraw.Keyed(ctx, "US.Diagnostics.Panel.Empty");
            UsKernelDraw.Label(new Rect(rect.x + 4f, y, Math.Max(1f, rect.width - 8f), RowHeight), note, ctx, UiFont.Tiny, TextAnchor.MiddleLeft);
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
            Rect rowRect = new(rect.x, y + i * RowHeight, rect.width, RowHeight);
            bool hovered = UiNative.IsMouseOver(rowRect);
            // A locked row is unavailable, not selected: it takes the disabled rail (hatch) so the hatch
            // and the selected plane can never be confused for one another.
            UsKernelDraw.RowSurface(rowRect, ctx.Theme, hovered, row.Locked ? UsKernelDraw.RowRail.Disabled : UsKernelDraw.RowRail.None);
            UsDiagRowPainter.Paint(rowRect, row, ctx, cooldownWidth, audioWidth);
            if (UiNative.Button(rowRect, ctx))
            {
                b.Invoke(UsDiagnosticsHost.KeyRowClick, row.PawnId);
            }
        }
    }

    private static string DrawSearchField(Rect rect, UiWidgetContext ctx)
    {
        var b = ctx.Bindings;
        string current = GetString(b, UsDiagnosticsHost.KeySearch);
        UiThemeDraw.Surface(rect, ctx.Theme, ctx.Theme.Raised, ctx.Theme.Border);

        UiValueState state = ctx.Session.GetOrCreateValueState(SearchStateId);
        if (!state.Focused)
        {
            state.EditText = current;
        }

        Rect textRect = new(rect.x + 6f, rect.y + 2f, Math.Max(1f, rect.width - 12f), Math.Max(1f, rect.height - 4f));
        string typed = UiNative.TextField(textRect, state.EditText);
        if (!string.Equals(typed, state.EditText, StringComparison.Ordinal))
        {
            state.EditText = typed;
            b.Set(UsDiagnosticsHost.KeySearch, typed);
        }
        else if (!state.Focused && current.Length == 0)
        {
            UsKernelDraw.Label(textRect, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Search.Placeholder"), ctx, UiFont.Small, TextAnchor.MiddleLeft);
        }

        if (UiNative.IsMouseDownOver(rect))
        {
            state.Focused = true;
        }

        if (state.Focused && (UiNative.IsEnterPressed() || UiNative.IsFocusLost(rect)))
        {
            state.Focused = false;
        }

        return state.Focused ? state.EditText : current;
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

    public static void Register() => UiWidgetRegistry.Register(
        UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagPagerWidget(),
        new[] { "Id", "Kind", "Height" });

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<int>(UsDiagnosticsHost.KeyPage, elementPath);
    }

    /// <summary>The pager's own height, for a composer that stacks it.</summary>
    internal const float BodyHeight = RowHeight;

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? 0f : BodyHeight;

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;
        DrawBodyCore(new Rect(rect.x, rect.y, rect.width, BodyHeight), ctx);
    }

    /// <summary>The pager's content, reusable by the narrow navigation body.</summary>
    internal static void DrawBodyCore(Rect rect, UiWidgetContext ctx)
    {
        var b = ctx.Bindings;
        int page = GetInt(b, UsDiagnosticsHost.KeyPage);
        int pages = Math.Max(1, GetInt(b, UsDiagnosticsHost.KeyPageCount));
        int total = GetInt(b, UsDiagnosticsHost.KeyTotal);

        string prev = UsDiagGlyphs.PrevText(ctx);
        string next = UsDiagGlyphs.NextText(ctx);
        float prevWidth = ctx.Metrics.MeasureWidth(prev, UiFont.Tiny) + ButtonPad * 2f;
        float nextWidth = ctx.Metrics.MeasureWidth(next, UiFont.Tiny) + ButtonPad * 2f;

        if (page > 0 && UsKernelDraw.SelectionButton(new Rect(rect.x, rect.y, prevWidth, RowHeight), ctx, prev, ctx.Theme, false))
        {
            b.Set(UsDiagnosticsHost.KeyPage, page - 1);
        }

        if (page < pages - 1 && UsKernelDraw.SelectionButton(new Rect(rect.xMax - nextWidth, rect.y, nextWidth, RowHeight), ctx, next, ctx.Theme, false))
        {
            b.Set(UsDiagnosticsHost.KeyPage, page + 1);
        }

        float textX = rect.x + prevWidth + 4f;
        float textW = Math.Max(1f, rect.width - prevWidth - nextWidth - 8f);
        UsKernelDraw.Label(
            new Rect(textX, rect.y, textW, RowHeight),
            string.Format(UsKernelDraw.Keyed(ctx, "US.Diagnostics.Page.Format"), page + 1, pages, total),
            ctx, UiFont.Tiny, TextAnchor.MiddleCenter);
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

    public static void Register() => UiWidgetRegistry.Register(
        UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagDetailWidget(),
        new[] { "Id", "Kind", "Scope" });

    public override void Validate(IUiBindings bindings, string elementPath)
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
            + SectionHeight + TextHeight(ctx, detail.Summary.Headline, Math.Max(1f, width - 12f), 2)
            + (detail.Summary.Detail.Length > 0 ? TextHeight(ctx, detail.Summary.Detail, Math.Max(1f, width - 12f), 2) : 0f)
            + SectionHeight
            + TextHeight(ctx, detail.Previous.EvaluationLine, content, 2)
            + (detail.Previous.DispatchLine.Length > 0 ? TextHeight(ctx, detail.Previous.DispatchLine, content, 2) : 0f)
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

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;
        DrawDetail(rect, ctx);
    }

    /// <summary>The detail content, reusable by the narrow navigation body.</summary>
    internal static void DrawDetail(Rect rect, UiWidgetContext ctx)
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
        if (Math.Abs(wanted - rect.height) > 0.5f)
        {
            ctx.Node?.MarkDirty();
        }

        float width = Math.Max(1f, rect.width);
        float content = Math.Max(1f, width - Indent);
        float y = rect.y;

        // 1. Identity + readiness. The badge is NEUTRAL for a ready pawn and attention-cyan for a
        // blocked one: a diagnostic block is not a destructive action and must not borrow the danger
        // family the carrier's Warning/Danger alias would give it.
        Rect badgeRect = new(rect.xMax - BadgeWidth, y + 3f, BadgeWidth, HeaderHeight - 6f);
        if (detail.Ready)
        {
            UiThemeDraw.Surface(badgeRect, ctx.Theme, ctx.Theme.Raised, ctx.Theme.Border);
            UsKernelDraw.Label(badgeRect, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Ready"), ctx, ctx.Theme.TextPrimary, UiFont.Tiny, TextAnchor.MiddleCenter, singleLine: true);
        }
        else
        {
            UsAttention.Badge(badgeRect, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Blocked"), ctx.Theme);
        }

        float labelWidth = Math.Max(1f, badgeRect.x - rect.x - 8f);
        UsKernelDraw.Label(
            new Rect(rect.x, y, labelWidth, HeaderHeight),
            UsKernelDraw.Ellipsized(detail.PawnText, ctx, UiFont.Small, labelWidth),
            ctx, UiFont.Small, TextAnchor.MiddleLeft);
        if (detail.Locked)
        {
            UsKernelDraw.Label(
                new Rect(badgeRect.x - 44f, y + 5f, 40f, 18f),
                UsKernelDraw.Keyed(ctx, "US.Diagnostics.LockedTag"),
                ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleRight);
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
            y = Wrapped(y, rect, ctx, detail.Previous.EvaluationLine, ctx.Theme.TextPrimary, 2);
        }

        if (detail.Previous.DispatchLine.Length > 0)
        {
            y = Wrapped(y, rect, ctx, detail.Previous.DispatchLine, ctx.Theme.TextSecondary, 2);
        }

        // 4. The conditions, grouped by side and split by clock basis inside a group.
        UsKernelDraw.Label(
            new Rect(rect.x, y, rect.width, SectionHeight),
            string.Format(UsKernelDraw.Keyed(ctx, "US.Diagnostics.Section.ChainCount"), detail.Gates.Count),
            ctx,
            UiFont.Tiny,
            TextAnchor.MiddleLeft,
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
            Rect headingRect = new(rect.x, y, rect.width, GroupTitleHeight);
            if (UsKernelDraw.SelectionButton(headingRect, ctx, heading, ctx.Theme, false))
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
        Rect rawToggle = new(rect.x, y, rect.width, SectionHeight);
        if (UsKernelDraw.SelectionButton(rawToggle, ctx, UsKernelDraw.Keyed(ctx, rawOpen ? "US.Diagnostics.Raw.Hide" : "US.Diagnostics.Raw.Show"), ctx.Theme, false))
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

    private static void DrawEmpty(Rect rect, UiWidgetContext ctx)
    {
        // "Current state incomplete" is the honest headline when there is no subject at all; the second
        // line says what to do about it. Neither is a pass state and neither implies "not reached".
        float y = rect.y + 8f;
        UsKernelDraw.Label(new Rect(rect.x + 8f, y, Math.Max(1f, rect.width - 16f), LineHeight),
            UsKernelDraw.Keyed(ctx, "US.Diagnostics.Summary.Incomplete"), ctx, UiFont.Small, TextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new Rect(rect.x + 8f, y + LineHeight, Math.Max(1f, rect.width - 16f), LineHeight),
            UsKernelDraw.Keyed(ctx, "US.Diagnostics.Detail.Empty"), ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft);
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

    private static float DrawSummary(float y, Rect rect, UiWidgetContext ctx, UsDiagCurrentSummary summary)
    {
        float width = Math.Max(1f, rect.width - 12f);
        float headlineHeight = TextHeight(ctx, summary.Headline, width, 2);
        if (summary.HasCurrentBlock)
        {
            // The primary finding gets the attention treatment: a neutral fill with a cyan border and a
            // cyan label. It is the ONE place the attention substrate is used as a band.
            UsAttention.Band(new Rect(rect.x, y, rect.width, headlineHeight), ctx.Theme);
            UsKernelDraw.Label(
                new Rect(rect.x + 6f, y, Math.Max(1f, rect.width - 12f), headlineHeight),
                summary.Headline,
                ctx,
                UsAttention.Brush,
                UiFont.Tiny,
                TextAnchor.MiddleLeft);
        }
        else
        {
            UsKernelDraw.Label(new Rect(rect.x, y, width, headlineHeight), summary.Headline, ctx, ctx.Theme.TextPrimary, UiFont.Tiny, TextAnchor.MiddleLeft);
        }

        y += headlineHeight;
        if (summary.Detail.Length > 0)
        {
            float detailHeight = TextHeight(ctx, summary.Detail, width, 2);
            UsKernelDraw.Label(new Rect(rect.x, y, width, detailHeight), summary.Detail, ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft);
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
    private static float DrawGate(float y, Rect rect, UiWidgetContext ctx, UsDiagGateLine gate, float contentWidth, bool firstCurrentBlock)
    {
        Func<string, string> tr = key => UsKernelDraw.Keyed(ctx, key);
        int rowLines = RowLines(ctx, gate, contentWidth);
        int reasonLines = ReasonLines(ctx, gate, contentWidth, tr);
        float rowHeight = rowLines * LineHeight + reasonLines * LineHeight + 2f;
        float lineSpan = rowLines * LineHeight;
        float nameWidth = Math.Max(1f, (contentWidth - StatusWidth - BasisWidth - CellGap * 3f) * NameShare);
        float valueWidth = Math.Max(1f, contentWidth - StatusWidth - BasisWidth - nameWidth - CellGap * 3f);
        float x = rect.x + Indent;
        float right = rect.x + Indent + contentWidth;

        // The rail: attention cyan on the first CURRENT block (the row the summary quotes), the plain
        // divider elsewhere. "First" is carried by this rail, never by repeating the status word.
        if (firstCurrentBlock)
        {
            UsAttention.Rail(new Rect(rect.x, y, 2f, rowHeight), ctx.Theme);
        }
        else
        {
            UiThemeDraw.Solid(new Rect(rect.x, y, 2f, rowHeight), ctx.Theme.Divider);
        }

        UsKernelDraw.Label(new Rect(x, y, StatusWidth, lineSpan), gate.Status, ctx, UsDiagPaint.Status(ctx.Theme, gate.State), UiFont.Tiny, TextAnchor.MiddleLeft);
        UsKernelDraw.Label(new Rect(x + StatusWidth + CellGap, y, nameWidth, lineSpan), gate.Name, ctx, UiFont.Tiny, TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(x + StatusWidth + CellGap + nameWidth + CellGap, y, valueWidth, lineSpan),
            gate.Value,
            ctx,
            UsDiagPaint.Value(ctx.Theme, gate.State),
            UiFont.Tiny,
            TextAnchor.MiddleRight,
            singleLine: false);
        UsKernelDraw.Label(
            new Rect(right - BasisWidth, y, BasisWidth, lineSpan),
            UsDiagnosticsProjection.BasisText(gate, tr),
            ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);

        if (reasonLines > 0)
        {
            float reasonWidth = Math.Max(1f, contentWidth - 12f);
            UsKernelDraw.Label(
                new Rect(x + 12f, y + lineSpan, reasonWidth, reasonLines * LineHeight),
                UsDiagnosticsProjection.ReasonText(gate, tr),
                ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: false);
        }

        return y + rowHeight;
    }

    private static bool HasReason(UsDiagGateLine gate)
        => gate.State == UsDiagGateState.NA || gate.State == UsDiagGateState.Pending;

    /// <summary>
    /// A condition row's measured height: the taller of the wrapped name and the wrapped value, plus the
    /// reason line when the row has one. Long Chinese values therefore receive real height instead of
    /// being clipped or shrunk.
    /// </summary>
    private static float RowHeight(UiWidgetContext ctx, UsDiagGateLine gate, float contentWidth)
    {
        return (RowLines(ctx, gate, contentWidth) * LineHeight)
            + (ReasonLines(ctx, gate, contentWidth, key => UsKernelDraw.Keyed(ctx, key)) * LineHeight) + 2f;
    }

    private static int RowLines(UiWidgetContext ctx, UsDiagGateLine gate, float contentWidth)
    {
        float nameWidth = Math.Max(1f, (contentWidth - StatusWidth - BasisWidth - CellGap * 3f) * NameShare);
        float valueWidth = Math.Max(1f, contentWidth - StatusWidth - BasisWidth - nameWidth - CellGap * 3f);
        return Math.Max(LineCount(ctx, gate.Name, nameWidth, 2), LineCount(ctx, gate.Value, valueWidth, 2));
    }

    private static int ReasonLines(UiWidgetContext ctx, UsDiagGateLine gate, float contentWidth, Func<string, string> tr)
        => HasReason(gate) ? LineCount(ctx, UsDiagnosticsProjection.ReasonText(gate, tr), Math.Max(1f, contentWidth - 12f), 2) : 0;

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

    private static float TextHeight(UiWidgetContext ctx, string text, float width, int maxLines)
        => LineCount(ctx, text, width, maxLines) * LineHeight;

    private static float Section(float y, Rect rect, UiWidgetContext ctx, string key)
    {
        UsKernelDraw.Label(new Rect(rect.x, y, rect.width, SectionHeight), UsKernelDraw.Keyed(ctx, key), ctx, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        return y + SectionHeight;
    }

    private static float Wrapped(float y, Rect rect, UiWidgetContext ctx, string text, Color color, int maxLines)
    {
        float width = Math.Max(1f, rect.width - Indent);
        float height = TextHeight(ctx, text, width, maxLines);
        UsKernelDraw.Label(new Rect(rect.x + Indent, y, width, height), text, ctx, color, UiFont.Tiny, TextAnchor.MiddleLeft);
        return y + height;
    }

    private static float FieldRow(float y, Rect rect, UiWidgetContext ctx, string labelKey, string value)
    {
        float nameWidth = rect.width * 0.38f - 4f;
        float valueX = rect.x + nameWidth + 4f;
        float valueWidth = Math.Max(1f, rect.width - nameWidth - 4f);
        UsKernelDraw.Label(new Rect(rect.x, y, nameWidth, LineHeight), UsKernelDraw.Keyed(ctx, labelKey), ctx, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new Rect(valueX, y, valueWidth, LineHeight), value, ctx, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: false);
        return y + LineHeight;
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

    internal static void Paint(Rect rect, UsDiagRow row, UiWidgetContext ctx, float cooldownWidth = 0f, float audioWidth = 0f)
    {
        Color dotColor = UsDiagPaint.Dot(ctx.Theme, row.Tone);
        float x = rect.x + 2f;
        // D7: the dot is a glyph until the font says it cannot draw one, then it is the state word.
        string dot = UsDiagGlyphs.DotText(ctx, row.Tone);
        UsKernelDraw.Label(new Rect(x, rect.y, DotWidth, rect.height), dot, ctx, dotColor, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        x += DotWidth;

        float statusX = rect.xMax - StatusWidth;
        float audioX = statusX - AudioWidth - CellGap;
        float cooldownX = audioX - CooldownWidth - CellGap;
        float actionX = cooldownX - ActionWidth - CellGap;
        float pawnWidth = Math.Max(1f, actionX - x - CellGap);

        Func<string, float> measure = text => ctx.Metrics.MeasureWidth(text, UiFont.Tiny);
        UsKernelDraw.Label(new Rect(x, rect.y, pawnWidth, rect.height),
            UsKernelDraw.Ellipsized(row.PawnText, ctx, UiFont.Tiny, pawnWidth),
            ctx, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new Rect(actionX, rect.y, ActionWidth, rect.height),
            UsKernelDraw.Ellipsized(row.ActionText, ctx, UiFont.Tiny, ActionWidth),
            ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new Rect(cooldownX, rect.y, CooldownWidth, rect.height),
            UsKernelDraw.Ellipsized(UsDiagnosticsProjection.PadNumeric(row.CooldownText, cooldownWidth, measure), ctx, UiFont.Tiny, CooldownWidth),
            ctx, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);
        UsKernelDraw.Label(new Rect(audioX, rect.y, AudioWidth, rect.height),
            UsKernelDraw.Ellipsized(UsDiagnosticsProjection.PadNumeric(row.AudioText, audioWidth, measure), ctx, UiFont.Tiny, AudioWidth),
            ctx, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);
        UsKernelDraw.Label(new Rect(statusX, rect.y, StatusWidth, rect.height),
            UsKernelDraw.Keyed(ctx, row.Ready ? "US.Diagnostics.Ready" : "US.Diagnostics.Blocked"),
            ctx, row.Ready ? ctx.Theme.TextSecondary : UsAttention.Brush, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);
    }
}
