using System;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US VoicePack checklist for the selected domain: status banners, search field,
/// pack checkbox rows, empty state and the Forget Unavailable banner. Reads the typed
/// "selected-domain" binding; writes "toggle-pack" / "forget-unavailable" typed actions and the
/// "search-text" value binding. Search focus/edit state lives in the session.
/// </summary>
public sealed class UsVoicePackChecklistWidget : UsSectionWidgetBase
{
    public const string KindName = "us/voice-pack-checklist";

    private const float RowGap = 2f;
    private const float SearchFieldHeight = 24f;
    private const float EmptyStateHeight = 48f;
    private const float BannerGap = 4f;

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsVoicePackChecklistWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<VoicePackDomainView?>("selected-domain", elementPath);
        bindings.ValidateValue<string>("search-text", elementPath);
        bindings.ValidateAction<UsPackToggle>("toggle-pack", elementPath);
        bindings.ValidateAction<UsDomainIdentity>("forget-unavailable", elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return SearchFieldHeight + EmptyStateHeight + RowGap;
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        VoicePackDomainView? selected = ctx.Bindings.TryGet("selected-domain", out VoicePackDomainView? s) ? s : null;
        if (!selected.HasValue) return EmptyStateHeight;

        string search = ctx.Bindings.TryGet("search-text", out string text) ? text : "";
        VoicePackDomainView domain = selected.Value;
        float height = 0f;
        if (domain.IsDormant || domain.IsTargetUnavailable || domain.HasCanonicalConflict)
        {
            height += BannerHeight + BannerGap;
        }

        height += SearchFieldHeight + RowGap;

        int shown = 0;
        foreach (VoicePackRowView row in domain.Packs)
        {
            if (!MatchesSearch(row, search)) continue;
            height += RowHeightFor(row, ctx) + RowGap;
            shown++;
        }

        if (shown == 0)
        {
            height += EmptyStateHeight + RowGap;
        }

        if (domain.OrphanCount > 0)
        {
            height += BannerHeight + BannerGap;
        }

        return height;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        VoicePackDomainView? selected = ctx.Bindings.TryGet("selected-domain", out VoicePackDomainView? s) ? s : null;
        if (!selected.HasValue)
        {
            UsKernelDraw.Label(
                new Rect(rect.x, rect.y, rect.width, EmptyStateHeight),
                "No VoicePack domains are available yet. Install a VoicePack that declares a raceDefName.",
                ctx.Theme,
                ctx.Theme.TextSecondary,
                UiFont.Small,
                TextAnchor.MiddleCenter);
            return;
        }

        VoicePackDomainView domain = selected.Value;
        float y = rect.y;

        if (domain.IsDormant)
        {
            y = DrawBanner(rect, y, "Biotech is not active; this Xenotype domain is dormant.", ctx);
        }

        if (domain.IsTargetUnavailable)
        {
            y = DrawBanner(rect, y, "The selected Xenotype target is not loaded. Selections are retained for recovery.", ctx);
        }

        if (domain.HasCanonicalConflict)
        {
            y = DrawBanner(rect, y, "Multiple Xenotype Defs share this target; routing fails closed until resolved.", ctx);
        }

        string search = DrawSearchField(new Rect(rect.x, y, rect.width, SearchFieldHeight), ctx);
        y += SearchFieldHeight + RowGap;

        int shown = 0;
        foreach (VoicePackRowView row in domain.Packs)
        {
            if (!MatchesSearch(row, search)) continue;
            float rowHeight = RowHeightFor(row, ctx);
            DrawPackRow(new Rect(rect.x, y, rect.width, rowHeight), domain, row, ctx);
            y += rowHeight + RowGap;
            shown++;
        }

        if (shown == 0)
        {
            UsKernelDraw.Label(
                new Rect(rect.x, y, rect.width, EmptyStateHeight),
                domain.Packs.Count == 0
                    ? "No VoicePacks are installed for this domain."
                    : "No VoicePacks match the current search.",
                ctx.Theme,
                ctx.Theme.TextSecondary,
                UiFont.Small,
                TextAnchor.MiddleCenter);
            y += EmptyStateHeight;
        }

        if (domain.OrphanCount > 0)
        {
            DrawOrphanBanner(new Rect(rect.x, y, rect.width, BannerHeight), domain, ctx);
        }
    }

    private float DrawBanner(Rect outer, float y, string text, UiWidgetContext ctx)
    {
        Rect bannerRect = new(outer.x, y, outer.width, BannerHeight);
        UsKernelDraw.RowSurface(bannerRect, ctx.Theme, hovered: false, selected: false, danger: true);
        UsKernelDraw.Label(
            new Rect(bannerRect.x + 8f, bannerRect.y, Math.Max(1f, bannerRect.width - 16f), bannerRect.height),
            text,
            ctx.Theme,
            ctx.Theme.TextOnDanger,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
        return y + BannerHeight + BannerGap;
    }

    private void DrawOrphanBanner(Rect rect, VoicePackDomainView domain, UiWidgetContext ctx)
    {
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered: false, selected: false, danger: true);

        Rect button = new(rect.xMax - 132f, rect.y + 5f, 124f, Math.Max(20f, rect.height - 10f));
        Rect text = new(rect.x + 8f, rect.y + 5f, Math.Max(1f, button.x - rect.x - 16f), Math.Max(1f, rect.height - 10f));
        UsKernelDraw.Label(
            text,
            "Selected pack keys are no longer installed. Use Forget Unavailable to clean them.",
            ctx.Theme,
            ctx.Theme.TextOnDanger,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        if (UsKernelDraw.SelectionButton(button, "Forget Unavailable", ctx.Theme, selected: true, danger: true, font: UiFont.Tiny))
        {
            ctx.Bindings.Invoke(
                "forget-unavailable",
                new UsDomainIdentity(domain.Scope, domain.RaceDefName, domain.TargetDefName));
        }
    }

    private string DrawSearchField(Rect rect, UiWidgetContext ctx)
    {
        string current = ctx.Bindings.TryGet("search-text", out string text) ? text : "";
        UiThemeDraw.Surface(rect, ctx.Theme, ctx.Theme.Raised, ctx.Theme.Border);

        string elementId = "checklist-search";
        UiValueState state = ctx.Session.GetOrCreateValueState(elementId);
        if (!state.Focused)
        {
            state.EditText = current;
        }

        Rect textRect = new(rect.x + 6f, rect.y + 2f, Math.Max(1f, rect.width - 12f), Math.Max(1f, rect.height - 4f));
        string typed = UiNative.TextField(textRect, state.EditText);

        if (!string.Equals(typed, state.EditText, StringComparison.Ordinal))
        {
            state.EditText = typed;
            // search-text bumps the session revision through the Host binding boundary.
            ctx.Bindings.Set("search-text", typed);
        }
        else if (!state.Focused && current.Length == 0)
        {
            UsKernelDraw.Label(textRect, "Search VoicePacks…", ctx.Theme, ctx.Theme.TextDisabled, UiFont.Small, TextAnchor.MiddleLeft);
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

    private void DrawPackRow(Rect rect, VoicePackDomainView domain, VoicePackRowView row, UiWidgetContext ctx)
    {
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, row.IsSelected);

        string meta = row.ModName + " · " + row.Author;
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + 4f, Math.Max(1f, rect.width - 60f), 18f),
            row.Label,
            ctx.Theme,
            row.IsSelected ? ctx.Theme.TextOnGold : ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + 22f, Math.Max(1f, rect.width - 60f), 14f),
            meta,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rect.y + 36f, Math.Max(1f, rect.width - 60f), 14f),
            row.Coverage,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        UsKernelDraw.Checkbox(new Rect(rect.xMax - 34f, rect.y + 4f, 18f, 18f), ctx.Theme, row.IsSelected);

        if (UiNative.Button(rect))
        {
            ctx.Bindings.Invoke(
                "toggle-pack",
                new UsPackToggle(domain.Scope, domain.RaceDefName, domain.TargetDefName, row.Key, !row.IsSelected));
        }
    }

    private float RowHeightFor(VoicePackRowView row, UiWidgetContext ctx)
    {
        float measured = ctx.Metrics.MeasureText(row.Label, UiFont.Small, Math.Max(1f, BodyWidth(ctx) - 60f));
        return Math.Max(54f, measured + 38f);
    }

    private float BannerHeight => Math.Max(30f, 22f);

    private static bool MatchesSearch(VoicePackRowView row, string query)
    {
        if (query == null || query.Trim().Length == 0) return true;
        return row.SearchText.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0
            || row.Label.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0
            || row.DefName.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0
            || row.Key.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
