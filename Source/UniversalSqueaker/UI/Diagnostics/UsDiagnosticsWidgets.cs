using System;
using System.Collections.Generic;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// The diagnostics panel's US-owned widget family (round-9 rulings §1.1/§1.5, reworked by 09 §3.2/§3.3):
/// toolbar (s/t, collapse, lock-detach), the paged/searched list, the pager, the verdict + grouped
/// gate-chain detail column, and the collapsed BAR that answers the three questions (what is this /
/// is it on / what is it doing) instead of replaying a summary row.
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
/// The collapsed bar (09 §3.3): a page-wide strip that answers the three questions in words - identity,
/// on/off, what it is doing now - and carries the two actions (expand, close) a collapsed window needs.
/// It is its own root element, NOT a child of the two-column Row, and it never reuses the list row pen:
/// the defect it replaces was a bar whose width and content both came from a 250px list row.
/// </summary>
public sealed class UsDiagBarWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/bar";

    /// <summary>09 §3.3's authorised geometry: 26 -> 32, dev-only panel.</summary>
    internal const float BarHeight = 32f;

    private const float Padding = 8f;
    private const float LineHeight = 14f;
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
        Rect closeRect = new(innerRight - closeWidth, lineTop + LineHeight, closeWidth, LineHeight);
        Rect expandRect = new(closeRect.x - ActionGap - expandWidth, lineTop + LineHeight, expandWidth, LineHeight);

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
        UsKernelDraw.Label(new Rect(innerLeft, lineTop, identityWidth, LineHeight), identity, ctx, ctx.Theme.TextPrimary, UiFont.Small, TextAnchor.MiddleLeft, singleLine: true);
        float switchX = innerLeft + ctx.Metrics.MeasureWidth(identity, UiFont.Small) + Gap;
        UsKernelDraw.Label(new Rect(switchX, lineTop, Math.Max(1f, innerRight - switchX), LineHeight), bar.SwitchText, ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        if (layout.ShowScale)
        {
            float scaleWidth = ctx.Metrics.MeasureWidth(bar.Scale, UiFont.Tiny);
            UsKernelDraw.Label(new Rect(innerRight - scaleWidth, lineTop, scaleWidth, LineHeight), bar.Scale, ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);
        }

        // Line 2: the activity sentence (first to go when narrow) and the two actions.
        if (layout.ShowActivity)
        {
            string dot = UsDiagGlyphs.DotText(ctx, bar.ActivityTone);
            Rect dotRect = new(innerLeft, lineTop + LineHeight, DotWidth, LineHeight);
            UsKernelDraw.Label(dotRect, dot, ctx, DotColor(ctx.Theme, bar.ActivityTone), UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
            float textX = dotRect.xMax;
            float textWidth = Math.Max(1f, expandRect.x - Gap - textX);
            UsKernelDraw.Label(
                new Rect(textX, lineTop + LineHeight, textWidth, LineHeight),
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

    private static Color DotColor(UiTheme theme, UsDiagDotTone tone) => tone switch
    {
        UsDiagDotTone.Ready => theme.Success,
        UsDiagDotTone.Blocked => theme.Warning,
        UsDiagDotTone.Pending => theme.Selected,
        _ => theme.TextSecondary,
    };
}

/// <summary>The paged, searchable summary list: one search field + 8 rows (the shared summary-row
/// layout the bar reuses). Row click routes through the source: a viewport pawn drills in
/// via game selection, a search-only (off-screen) hit locks and detaches a detail window.</summary>
public sealed class UsDiagListWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/list";
    internal const float FieldHeight = 24f;
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

    protected override float MeasureBody(UiWidgetContext ctx)
        => IsCollapsed(ctx) ? 0f : FieldHeight + UsDiagnosticsProjection.RowsPerPage * RowHeight;

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;

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
            UsKernelDraw.RowSurface(rowRect, ctx.Theme, hovered, row.Locked);
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

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? 0f : RowHeight;

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;

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
/// The detail column (09 §3.2): header, the VERDICT sentence, the gate chain grouped by the three sides
/// with the first block marked, and the raw values folded away by default. Values get the wider half of
/// the row and may wrap to two lines, so a Chinese value is no longer ellipsized mid-word (defect D4).
/// </summary>
public sealed class UsDiagDetailWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/detail";
    private const float HeaderHeight = 28f;
    private const float SectionHeight = 22f;
    private const float RowHeight = 22f;
    private const float GroupTitleHeight = 16f;
    private const float BadgeWidth = 72f;
    private const float Gap = 4f;
    private const float ValueShare = 0.60f;

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Register(
        UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagDetailWidget(),
        new[] { "Id", "Kind", "Scope" });

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        // Reads only (TryGet degrades); the lock action is validated by the toolbar that invokes it.
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyRawOpen, elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx) => HeaderHeight + SectionHeight * 3f + RowHeight * 22f;

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return 0f;
        if (!ctx.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) || detail == null) return 80f;

        return HeaderHeight
            + SectionHeight + RowHeight                       // verdict section + sentence
            + SectionHeight + GroupTitles(detail.Gates) + RowHeight * detail.Gates.Count
            + SectionHeight                                    // raw section toggle
            + (GetBool(ctx.Bindings, UsDiagnosticsHost.KeyRawOpen) ? RowHeight * 2f : 0f)
            + Gap;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;
        if (!ctx.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) || detail == null)
        {
            // Inline inset, not Verse.GenUI.ContractedBy: that extension lives in Assembly-CSharp, and
            // the harness's Verse stub does not carry GenUI, so calling it made this whole widget
            // undrawable (and therefore untestable) outside the game. Same 8px inset, no backend tie.
            UsKernelDraw.Label(
                new Rect(rect.x + 8f, rect.y + 8f, Math.Max(1f, rect.width - 16f), Math.Max(1f, rect.height - 16f)),
                UsKernelDraw.Keyed(ctx, "US.Diagnostics.Detail.Empty"),
                ctx, UiFont.Small, TextAnchor.MiddleCenter);
            return;
        }

        float y = rect.y;

        Rect badgeRect = new(rect.xMax - BadgeWidth, y + 3f, BadgeWidth, HeaderHeight - 6f);
        UiThemeDraw.StatusBadge(
            badgeRect,
            UsKernelDraw.Keyed(ctx, detail.Ready ? "US.Diagnostics.Ready" : "US.Diagnostics.Blocked"),
            ctx.Theme,
            detail.Ready ? UiStatusTone.Success : UiStatusTone.Warning,
            UiFont.Tiny);
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

        // Verdict: the one-line answer, first thing under the header.
        y = Section(y, rect, ctx, "US.Diagnostics.Section.Verdict");
        UsKernelDraw.Label(new Rect(rect.x, y, rect.width, RowHeight), detail.Verdict, ctx, UiFont.Small, TextAnchor.MiddleLeft, singleLine: true);
        y += RowHeight;

        // Gate chain: grouped by side, with the first block quoted in the heading and marked in place.
        int firstBlock = UsDiagnosticsProjection.FirstBlockedIndex(detail.Gates);
        string heading = string.Format(
            UsKernelDraw.Keyed(ctx, "US.Diagnostics.Section.ChainCount"),
            detail.Gates.Count)
            + " · " + UsDiagnosticsProjection.FirstBlockedText(detail.Gates, key => UsKernelDraw.Keyed(ctx, key));
        UsKernelDraw.Label(new Rect(rect.x, y, rect.width, SectionHeight), heading, ctx, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        y += SectionHeight;

        y = DrawGroup(y, rect, ctx, detail.Gates, UsDiagGateGroup.Game, firstBlock);
        y = DrawGroup(y, rect, ctx, detail.Gates, UsDiagGateGroup.Rules, firstBlock);
        y = DrawGroup(y, rect, ctx, detail.Gates, UsDiagGateGroup.Audio, firstBlock);
        y += Gap;

        // Raw values: folded away by default - they are evidence, not the answer.
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

    private static float GroupTitles(IReadOnlyList<UsDiagGateLine> gates)
    {
        float total = 0f;
        for (int group = 0; group < 3; group++)
        {
            total += CountInGroup(gates, (UsDiagGateGroup)group) > 0 ? GroupTitleHeight : 0f;
        }

        return total;
    }

    private static int CountInGroup(IReadOnlyList<UsDiagGateLine> gates, UsDiagGateGroup group)
    {
        int count = 0;
        for (int i = 0; i < gates.Count; i++)
        {
            if (gates[i].Group == group) count++;
        }

        return count;
    }

    private static float DrawGroup(float y, Rect rect, UiWidgetContext ctx, IReadOnlyList<UsDiagGateLine> gates, UsDiagGateGroup group, int firstBlock)
    {
        if (CountInGroup(gates, group) == 0) return y;

        UsKernelDraw.Label(
            new Rect(rect.x, y, rect.width, GroupTitleHeight),
            UsKernelDraw.Keyed(ctx, GroupKey(group)),
            ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        y += GroupTitleHeight;

        for (int i = 0; i < gates.Count; i++)
        {
            UsDiagGateLine gate = gates[i];
            if (gate.Group != group) continue;

            // The group rail (05 §3.2: grouping is a 2px track plus indent, not a card). On the chain's
            // FIRST block the same rail turns attention-coloured: that is the "first" emphasis, because
            // repeating the state word here is what printed the mark twice in game.
            // TODO(maintainer): decide whether later blocked rows keep their own state word or read
            // "not reached" - deferred until that call is made.
            bool firstBlockRow = i == firstBlock;
            UiThemeDraw.Solid(
                new Rect(rect.x, y, 2f, RowHeight),
                firstBlockRow ? ctx.Theme.Warning : ctx.Theme.Divider);
            float indent = 6f;
            float rowWidth = Math.Max(1f, rect.width - indent);
            float nameWidth = rowWidth * (1f - ValueShare) - 4f;
            float valueX = rect.x + indent + nameWidth + 4f;
            float valueWidth = Math.Max(1f, rect.width - indent - nameWidth - 4f);
            UsKernelDraw.Label(new Rect(rect.x + indent, y, nameWidth, RowHeight), gate.Name, ctx, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);

            // D4: the value gets the wider half and TWO lines; only a real overflow is clipped, and the
            // fit audit reports it through the same seam as every other label. The state word is applied
            // ONCE by the projection's rule, so a blocked row cannot be marked twice whatever its place
            // in the chain (the defect it replaced had one branch per condition).
            string value = UsDiagnosticsProjection.GateValueText(
                gate.Value, gate.State, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Gate.BlockMark"));


            UsKernelDraw.Label(
                new Rect(valueX, y, valueWidth, RowHeight),
                value,
                ctx,
                GateColor(ctx.Theme, gate.State),
                UiFont.Tiny,
                TextAnchor.MiddleRight,
                singleLine: false);
            y += RowHeight;
        }

        return y;
    }

    private static string GroupKey(UsDiagGateGroup group) => group switch
    {
        UsDiagGateGroup.Game => "US.Diagnostics.Gate.Group.Game",
        UsDiagGateGroup.Rules => "US.Diagnostics.Gate.Group.Rules",
        _ => "US.Diagnostics.Gate.Group.Audio",
    };

    private static float Section(float y, Rect rect, UiWidgetContext ctx, string key)
    {
        UsKernelDraw.Label(new Rect(rect.x, y, rect.width, SectionHeight), UsKernelDraw.Keyed(ctx, key), ctx, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        return y + SectionHeight;
    }

    private static float FieldRow(float y, Rect rect, UiWidgetContext ctx, string labelKey, string value)
    {
        float nameWidth = rect.width * (1f - ValueShare) - 4f;
        float valueX = rect.x + nameWidth + 4f;
        float valueWidth = Math.Max(1f, rect.width - nameWidth - 4f);
        UsKernelDraw.Label(new Rect(rect.x, y, nameWidth, RowHeight), UsKernelDraw.Keyed(ctx, labelKey), ctx, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(
            new Rect(valueX, y, valueWidth, RowHeight),
            value,
            ctx, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: false);
        return y + RowHeight;
    }

    /// <summary>The auditable four-state mapping: Pass green, Block the ATTENTION token plus the "挡"
    /// prefix, Pending blue, NA neutral - a N/A must never render in the success colour, and a block
    /// must never share the accent that means "current object" (09 §3.2 item 3 / §3.4).</summary>
    internal static Color GateColor(UiTheme theme, UsDiagGateState state) => state switch
    {
        UsDiagGateState.Pass => theme.Success,
        UsDiagGateState.Block => theme.Warning,
        UsDiagGateState.Pending => theme.Selected,
        _ => theme.TextPrimary,
    };
}

/// <summary>The summary-row layout used by the list and both bars: dot | pawn | action | cooldown |
/// audio | status. Column widths live here so the uses never drift apart. The two numeric columns
/// arrive with their column advance (D6) so every row in one page right-aligns on the same edge.</summary>
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
        Color dotColor = DotColor(ctx.Theme, row.Tone);
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
            ctx, row.Ready ? ctx.Theme.Success : ctx.Theme.Warning, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);
    }

    private static Color DotColor(UiTheme theme, UsDiagDotTone tone) => tone switch
    {
        UsDiagDotTone.Ready => theme.Success,
        UsDiagDotTone.Blocked => theme.Warning,
        UsDiagDotTone.Pending => theme.Selected,
        _ => theme.TextSecondary,
    };
}
