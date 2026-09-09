using System;
using System.Collections.Generic;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// The diagnostics panel's US-owned widget family (round-9 rulings §1.1/§1.5): everything the
/// mode badge used to be, plus what replaced it - toolbar (s/t, collapse, lock-detach), the
/// paged/searched list, the pager, the gate-chain detail column, and the one-row monitor bar.
/// All five draw through <see cref="UsKernelDraw"/>/<see cref="UiThemeDraw"/> and hit-test through
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
        if (UsKernelDraw.SelectionButton(secondsRect, seconds ? "s" : "t", ctx.Theme, seconds))
        {
            b.Set(UsDiagnosticsHost.KeySeconds, !seconds);
        }

        x += 40f + Gap;
        if (Scope == "main" && GetBool(b, UsDiagnosticsHost.KeyCanLock))
        {
            Rect lockRect = new(x, y, 56f, ButtonHeight);
            if (UsKernelDraw.SelectionButton(lockRect, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Lock"), ctx.Theme, false))
            {
                b.Invoke(UsDiagnosticsHost.KeyLock, 0);
            }

            x += 56f + Gap;
        }

        float collapseWidth = Math.Max(1f, rect.xMax - x);
        Rect collapseRect = new(x, y, collapseWidth, ButtonHeight);
        if (UsKernelDraw.SelectionButton(collapseRect, UsKernelDraw.Keyed(ctx, "US.Diagnostics.Bar"), ctx.Theme, false))
        {
            b.Set(UsDiagnosticsHost.KeyCollapsed, true);
        }
    }
}

/// <summary>The paged, searchable summary list: one search field + 8 rows (the shared summary-row
/// layout the monitor bar reuses). Row click routes through the source: a viewport pawn drills in
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

        for (int i = 0; i < rows.Count; i++)
        {
            UsDiagRow row = rows[i];
            Rect rowRect = new(rect.x, y + i * RowHeight, rect.width, RowHeight);
            bool hovered = UiNative.IsMouseOver(rowRect);
            UsKernelDraw.RowSurface(rowRect, ctx.Theme, hovered, row.Locked);
            UsDiagRowPainter.Paint(rowRect, row, ctx);
            if (UiNative.Button(rowRect))
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
/// silently dropped, the tracking set pages 8 at a time.</summary>
public sealed class UsDiagPagerWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/pager";
    private const float ArrowWidth = 28f;
    private const float RowHeight = 22f;

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

        if (page > 0 && UsKernelDraw.SelectionButton(new Rect(rect.x, rect.y, ArrowWidth, RowHeight), "◀", ctx.Theme, false))
        {
            b.Set(UsDiagnosticsHost.KeyPage, page - 1);
        }

        if (page < pages - 1 && UsKernelDraw.SelectionButton(new Rect(rect.xMax - ArrowWidth, rect.y, ArrowWidth, RowHeight), "▶", ctx.Theme, false))
        {
            b.Set(UsDiagnosticsHost.KeyPage, page + 1);
        }

        float textX = rect.x + ArrowWidth + 4f;
        float textW = Math.Max(1f, rect.width - (ArrowWidth + 4f) * 2f);
        UsKernelDraw.Label(
            new Rect(textX, rect.y, textW, RowHeight),
            string.Format(UsKernelDraw.Keyed(ctx, "US.Diagnostics.Page.Format"), page + 1, pages, total),
            ctx, UiFont.Tiny, TextAnchor.MiddleCenter);
    }
}

/// <summary>The detail column: header (label + ready badge + locked tag), the two current-state
/// rows, and the gate chain with the four-state colors (N/A neutral white, never a green Pass).</summary>
public sealed class UsDiagDetailWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/detail";
    private const float HeaderHeight = 28f;
    private const float SectionHeight = 20f;
    private const float RowHeight = 19f;
    private const float BadgeWidth = 72f;
    private const float Gap = 4f;

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Register(
        UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagDetailWidget(),
        new[] { "Id", "Kind", "Scope" });

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        // Reads only (TryGet degrades); the lock action is validated by the toolbar that invokes it.
    }

    protected override float FallbackHeight(UiWidgetContext ctx) => HeaderHeight + SectionHeight * 2f + RowHeight * 18f;

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return 0f;
        if (!ctx.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) || detail == null) return 80f;
        return HeaderHeight + SectionHeight + RowHeight * 2f + Gap + SectionHeight + RowHeight * detail.Gates.Count;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (IsCollapsed(ctx)) return;
        if (!ctx.Bindings.TryGet(UsDiagnosticsHost.KeyDetail, out UsDiagDetail? detail) || detail == null)
        {
            UsKernelDraw.Label(rect.ContractedBy(8f), UsKernelDraw.Keyed(ctx, "US.Diagnostics.Detail.Empty"), ctx, UiFont.Small, TextAnchor.MiddleCenter);
            return;
        }

        float y = rect.y;

        Rect badgeRect = new(rect.xMax - BadgeWidth, y + 3f, BadgeWidth, HeaderHeight - 6f);
        UiThemeDraw.StatusBadge(
            badgeRect,
            UsKernelDraw.Keyed(ctx, detail.Ready ? "US.Diagnostics.Ready" : "US.Diagnostics.Blocked"),
            ctx.Theme,
            detail.Ready ? UiStatusTone.Success : UiStatusTone.Neutral,
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
        y = Section(y, rect, ctx, "US.Diagnostics.Section.State");
        y = FieldRow(y, rect, ctx, "US.Diagnostics.Field.CurrentAction", detail.ActionText);
        y = FieldRow(y, rect, ctx, "US.Diagnostics.Field.LastDispatch", detail.AudioText);
        y += Gap;
        y = Section(y, rect, ctx, "US.Diagnostics.Section.Chain");

        for (int i = 0; i < detail.Gates.Count; i++)
        {
            UsDiagGateLine gate = detail.Gates[i];
            Rect rowRect = new(rect.x, y, rect.width, RowHeight);
            float nameWidth = rowRect.width * .45f;
            float valueX = rowRect.x + nameWidth + 4f;
            float valueWidth = Math.Max(1f, rowRect.width - nameWidth - 4f);
            UsKernelDraw.Label(new Rect(rowRect.x, y, nameWidth, RowHeight), gate.Name, ctx, UiFont.Tiny, TextAnchor.MiddleLeft);
            UsKernelDraw.Label(
                new Rect(valueX, y, valueWidth, RowHeight),
                UsKernelDraw.Ellipsized(gate.Value, ctx, UiFont.Tiny, valueWidth),
                ctx, GateColor(ctx.Theme, gate.State), UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);
            y += RowHeight;
        }
    }

    private static float Section(float y, Rect rect, UiWidgetContext ctx, string key)
    {
        UsKernelDraw.Label(new Rect(rect.x, y, rect.width, SectionHeight), UsKernelDraw.Keyed(ctx, key), ctx, UiFont.Tiny, TextAnchor.MiddleLeft);
        return y + SectionHeight;
    }

    private static float FieldRow(float y, Rect rect, UiWidgetContext ctx, string labelKey, string value)
    {
        float nameWidth = rect.width * .45f;
        float valueX = rect.x + nameWidth + 4f;
        float valueWidth = Math.Max(1f, rect.width - nameWidth - 4f);
        UsKernelDraw.Label(new Rect(rect.x, y, nameWidth, RowHeight), UsKernelDraw.Keyed(ctx, labelKey), ctx, UiFont.Tiny, TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(valueX, y, valueWidth, RowHeight),
            UsKernelDraw.Ellipsized(value, ctx, UiFont.Tiny, valueWidth),
            ctx, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);
        return y + RowHeight;
    }

    /// <summary>The auditable four-state mapping (round-9): Pass green, Block gold, Pending blue,
    /// NA neutral white - a N/A must never render in the success color.</summary>
    internal static Color GateColor(UiTheme theme, UsDiagGateState state) => state switch
    {
        UsDiagGateState.Pass => theme.Success,
        UsDiagGateState.Block => theme.AccentGold,
        UsDiagGateState.Pending => theme.Selected,
        _ => theme.TextPrimary,
    };
}

/// <summary>The collapsed monitor bar (visible ONLY while collapsed): one summary row, click to
/// expand. Main window feeds it the last-CHANGED pawn (snapshot monitor ruling); detail windows
/// their own pinned pawn.</summary>
public sealed class UsDiagMonitorWidget : UsDiagWidgetBase
{
    public const string KindName = "us/diag/monitor";

    public override string Kind => KindName;

    public static void Register() => UiWidgetRegistry.Register(
        UsKernelWidgetRegistrar.Scope, KindName, () => new UsDiagMonitorWidget(),
        new[] { "Id", "Kind", "Height" });

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<bool>(UsDiagnosticsHost.KeyCollapsed, elementPath);
    }

    protected override float MeasureBody(UiWidgetContext ctx) => IsCollapsed(ctx) ? 22f : 0f;

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (!IsCollapsed(ctx)) return;

        UsKernelDraw.RowSurface(rect, ctx.Theme, UiNative.IsMouseOver(rect), false);
        if (ctx.Bindings.TryGet(UsDiagnosticsHost.KeyMonitor, out UsDiagRow? row) && row != null)
        {
            UsDiagRowPainter.Paint(rect, row, ctx);
        }
        else
        {
            UsKernelDraw.Label(rect.ContractedBy(6f, 0f), UsKernelDraw.Keyed(ctx, "US.Diagnostics.Monitor.Empty"), ctx, UiFont.Tiny, TextAnchor.MiddleLeft);
        }

        if (UiNative.Button(rect))
        {
            ctx.Bindings.Set(UsDiagnosticsHost.KeyCollapsed, false);
        }
    }
}

/// <summary>The summary-row layout used THREE ways (list row, main bar, detail-window bar):
/// dot | pawn | action | cooldown | audio | status. Column widths live here so the three uses
/// never drift apart.</summary>
internal static class UsDiagRowPainter
{
    private const float DotWidth = 16f;
    private const float ActionWidth = 48f;
    private const float CooldownWidth = 84f;
    private const float AudioWidth = 44f;
    private const float StatusWidth = 40f;
    private const float CellGap = 4f;

    internal static void Paint(Rect rect, UsDiagRow row, UiWidgetContext ctx)
    {
        Color dotColor = DotColor(ctx.Theme, row.Tone);
        float x = rect.x + 2f;
        UsKernelDraw.Label(new Rect(x, rect.y, DotWidth, rect.height), "●", ctx, dotColor, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        x += DotWidth;

        float statusX = rect.xMax - StatusWidth;
        float audioX = statusX - AudioWidth - CellGap;
        float cooldownX = audioX - CooldownWidth - CellGap;
        float actionX = cooldownX - ActionWidth - CellGap;
        float pawnWidth = Math.Max(1f, actionX - x - CellGap);

        UsKernelDraw.Label(new Rect(x, rect.y, pawnWidth, rect.height),
            UsKernelDraw.Ellipsized(row.PawnText, ctx, UiFont.Tiny, pawnWidth),
            ctx, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new Rect(actionX, rect.y, ActionWidth, rect.height),
            UsKernelDraw.Ellipsized(row.ActionText, ctx, UiFont.Tiny, ActionWidth),
            ctx, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft, singleLine: true);
        UsKernelDraw.Label(new Rect(cooldownX, rect.y, CooldownWidth, rect.height),
            UsKernelDraw.Ellipsized(row.CooldownText, ctx, UiFont.Tiny, CooldownWidth),
            ctx, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);
        UsKernelDraw.Label(new Rect(audioX, rect.y, AudioWidth, rect.height),
            UsKernelDraw.Ellipsized(row.AudioText, ctx, UiFont.Tiny, AudioWidth),
            ctx, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);
        UsKernelDraw.Label(new Rect(statusX, rect.y, StatusWidth, rect.height),
            UsKernelDraw.Keyed(ctx, row.Ready ? "US.Diagnostics.Ready" : "US.Diagnostics.Blocked"),
            ctx, row.Ready ? ctx.Theme.Success : ctx.Theme.AccentGold, UiFont.Tiny, TextAnchor.MiddleRight, singleLine: true);
    }

    private static Color DotColor(UiTheme theme, UsDiagDotTone tone) => tone switch
    {
        UsDiagDotTone.Ready => theme.Success,
        UsDiagDotTone.Blocked => theme.AccentGold,
        UsDiagDotTone.Pending => theme.Selected,
        _ => theme.TextSecondary,
    };
}
