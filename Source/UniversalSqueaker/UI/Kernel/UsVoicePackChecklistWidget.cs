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

    // Every player-facing string this widget draws. Element ids and binding keys
    // ("checklist-search", "search-text", "toggle-pack", "forget-unavailable") stay machine tokens.
    private const string KeyNoDomains = "US.Packs.Checklist.NoDomains";
    private const string KeyDormantBanner = "US.Packs.Checklist.DormantBanner";
    private const string KeyTargetUnavailableBanner = "US.Packs.Checklist.TargetUnavailableBanner";
    private const string KeyConflictBanner = "US.Packs.Checklist.ConflictBanner";
    private const string KeyEmptyDomain = "US.Packs.Checklist.EmptyDomain";
    private const string KeyEmptySearch = "US.Packs.Checklist.EmptySearch";
    private const string KeyOrphanBanner = "US.Packs.Checklist.OrphanBanner";
    private const string KeyForgetUnavailable = "US.Packs.Checklist.ForgetUnavailable";
    private const string KeySearchPlaceholder = "US.Packs.Checklist.SearchPlaceholder";
    private const string KeyPackMeta = "US.Packs.Checklist.PackMeta";

    private const float RowGap = 2f;
    private const float SearchFieldHeight = 24f;
    private const float BannerGap = 4f;

    // Floors, not fixed sizes: every band below is grown by the text metrics so a translated sentence
    // that needs a second or third line is drawn instead of cut off.
    private const float EmptyStateMinHeight = 48f;
    private const float BannerMinHeight = 30f;
    private const float BandVerticalPadding = 8f;
    private const float BannerTextInset = 8f;
    private const float RowTopPadding = 4f;
    private const float RowBottomPadding = 4f;
    private const float LabelBand = 18f;
    private const float MetaBand = 16f;
    private const float CoverageBand = 16f;
    private const float RowTextReserve = 60f;

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
        return SearchFieldHeight + EmptyStateMinHeight + RowGap;
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        VoicePackDomainView? selected = ctx.Bindings.TryGet("selected-domain", out VoicePackDomainView? s) ? s : null;
        if (!selected.HasValue) return EmptyStateBand(ctx, ctx.Translation.Translate(KeyNoDomains));

        string search = ctx.Bindings.TryGet("search-text", out string text) ? text : "";
        VoicePackDomainView domain = selected.Value;
        float height = 0f;

        // Each banner that Draw will render is measured separately. Summing them once for all three
        // conditions under-counted the body whenever a domain was dormant, unavailable AND in conflict,
        // so the last banner fell outside the allocated card.
        if (domain.IsDormant)
        {
            height += BannerBand(ctx, ctx.Translation.Translate(KeyDormantBanner)) + BannerGap;
        }

        if (domain.IsTargetUnavailable)
        {
            height += BannerBand(ctx, ctx.Translation.Translate(KeyTargetUnavailableBanner)) + BannerGap;
        }

        if (domain.HasCanonicalConflict)
        {
            height += BannerBand(ctx, ctx.Translation.Translate(KeyConflictBanner)) + BannerGap;
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
            height += EmptyStateBand(ctx, ctx.Translation.Translate(domain.Packs.Count == 0 ? KeyEmptyDomain : KeyEmptySearch)) + RowGap;
        }

        if (domain.OrphanCount > 0)
        {
            height += BannerBand(ctx, ctx.Translation.Translate(KeyOrphanBanner)) + BannerGap;
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
                new Rect(rect.x, rect.y, rect.width, EmptyStateBand(ctx, ctx.Translation.Translate(KeyNoDomains))),
                ctx.Translation.Translate(KeyNoDomains),
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
            y = DrawBanner(rect, y, ctx.Translation.Translate(KeyDormantBanner), ctx);
        }

        if (domain.IsTargetUnavailable)
        {
            y = DrawBanner(rect, y, ctx.Translation.Translate(KeyTargetUnavailableBanner), ctx);
        }

        if (domain.HasCanonicalConflict)
        {
            y = DrawBanner(rect, y, ctx.Translation.Translate(KeyConflictBanner), ctx);
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
            float emptyBand = EmptyStateBand(ctx, ctx.Translation.Translate(domain.Packs.Count == 0 ? KeyEmptyDomain : KeyEmptySearch));
            UsKernelDraw.Label(
                new Rect(rect.x, y, rect.width, emptyBand),
                ctx.Translation.Translate(domain.Packs.Count == 0 ? KeyEmptyDomain : KeyEmptySearch),
                ctx.Theme,
                ctx.Theme.TextSecondary,
                UiFont.Small,
                TextAnchor.MiddleCenter);
            y += emptyBand;
        }

        if (domain.OrphanCount > 0)
        {
            DrawOrphanBanner(new Rect(rect.x, y, rect.width, BannerBand(ctx, ctx.Translation.Translate(KeyOrphanBanner))), domain, ctx);
        }
    }

    private float DrawBanner(Rect outer, float y, string text, UiWidgetContext ctx)
    {
        float band = BannerBand(ctx, text);
        Rect bannerRect = new(outer.x, y, outer.width, band);
        UsKernelDraw.RowSurface(bannerRect, ctx.Theme, hovered: false, UsKernelDraw.RowRail.None, danger: true);
        UsKernelDraw.Label(
            new Rect(bannerRect.x + BannerTextInset, bannerRect.y, Math.Max(1f, bannerRect.width - BannerTextInset * 2f), band),
            text,
            ctx.Theme,
            ctx.Theme.TextOnDanger,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
        return y + band + BannerGap;
    }

    private void DrawOrphanBanner(Rect rect, VoicePackDomainView domain, UiWidgetContext ctx)
    {
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered: false, UsKernelDraw.RowRail.None, danger: true);

        Rect button = new(rect.xMax - 132f, rect.y + 5f, 124f, Math.Max(20f, rect.height - 10f));
        UsKernelDraw.HelpHover(button, ctx, "us/voice-pack-checklist/forget");
        Rect text = new(rect.x + 8f, rect.y + 5f, Math.Max(1f, button.x - rect.x - 16f), Math.Max(1f, rect.height - 10f));
        UsKernelDraw.Label(
            text,
            ctx.Translation.Translate(KeyOrphanBanner),
            ctx.Theme,
            ctx.Theme.TextOnDanger,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        if (UsKernelDraw.SelectionButton(button, ctx, ctx.Translation.Translate(KeyForgetUnavailable), ctx.Theme, selected: true, danger: true, font: UiFont.Tiny))
        {
            ctx.Bindings.Invoke(
                "forget-unavailable",
                new UsDomainIdentity(domain.Scope, domain.RaceDefName, domain.TargetDefName));
        }
    }

    private string DrawSearchField(Rect rect, UiWidgetContext ctx)
    {
        UsKernelDraw.HelpHover(rect, ctx, "us/voice-pack-checklist/search");
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
            UsKernelDraw.Label(textRect, ctx.Translation.Translate(KeySearchPlaceholder), ctx.Theme, ctx.Theme.TextDisabled, UiFont.Small, TextAnchor.MiddleLeft);
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
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/voice-pack-checklist/row");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, row.IsSelected ? UsKernelDraw.RowRail.Selected : UsKernelDraw.RowRail.None);
        // Same list convention as the single-line rows: the pack row ends in one hairline in the divider
        // token. It sits inside the row's bottom edge, so neither the row height nor the 24px hit band
        // (RowHitRect centres that band on the visual row) changes.
        UsKernelDraw.RowBottomLine(rect, ctx.Theme);

        string meta = UsPacksText.Format(ctx, KeyPackMeta, row.ModName, row.Author);
        float textWidth = Math.Max(1f, rect.width - RowTextReserve);
        (float labelBand, float metaBand, float coverageBand) = RowBands(ctx, textWidth, row, meta);

        float rowY = rect.y + RowTopPadding;
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rowY, textWidth, labelBand),
            row.Label,
            ctx.Theme,
            // The selected row is a plane plus the dim rail (spec 1.4/1.5): its label stays primary ink.
            // TextOnGold here implied a gold fill that this row never has, and it spent one of the screen's
            // two accent readings on a state the rail already carries.
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        rowY += labelBand + RowGap;
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rowY, textWidth, metaBand),
            meta,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.UpperLeft);
        rowY += metaBand + RowGap;
        UsKernelDraw.Label(
            new Rect(rect.x + UsKernelDraw.RowLeftPadding, rowY, textWidth, coverageBand),
            row.Coverage,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.UpperLeft);

        Rect checkbox = UsKernelDraw.CheckboxSlot(rect);
        bool toggled = UsKernelDraw.Checkbox(checkbox, ctx, row.IsSelected);

        Rect rowHit = UsKernelDraw.RowHitRect(rect, ctx);
        rowHit.width = Math.Max(1f, checkbox.x - rowHit.x);
        if (toggled || UiNative.Button(rowHit, ctx))
        {
            ctx.Bindings.Invoke(
                "toggle-pack",
                new UsPackToggle(domain.Scope, domain.RaceDefName, domain.TargetDefName, row.Key, !row.IsSelected));
        }
    }

    private float RowHeightFor(VoicePackRowView row, UiWidgetContext ctx)
    {
        float textWidth = Math.Max(1f, BodyWidth(ctx) - RowTextReserve);
        (float labelBand, float metaBand, float coverageBand) = RowBands(
            ctx, textWidth, row, UsPacksText.Format(ctx, KeyPackMeta, row.ModName, row.Author));

        return RowTopPadding + labelBand + RowGap + metaBand + RowGap + coverageBand + RowBottomPadding;
    }

    /// <summary>
    /// The three text bands of a pack row, resolved by one shared path so the height Measure allocates
    /// and the offsets Draw uses cannot disagree. The meta and coverage lines sit in Tiny bands that were
    /// previously 14px, shorter than a single Tiny line, which is why their tails never rendered.
    /// </summary>
    private (float Label, float Meta, float Coverage) RowBands(
        UiWidgetContext ctx, float textWidth, VoicePackRowView row, string meta)
    {
        return (
            Math.Max(LabelBand, ctx.Metrics.MeasureText(row.Label, UiFont.Small, textWidth)),
            Math.Max(MetaBand, ctx.Metrics.MeasureText(meta, UiFont.Tiny, textWidth)),
            Math.Max(CoverageBand, ctx.Metrics.MeasureText(row.Coverage, UiFont.Tiny, textWidth)));
    }

    /// <summary>Warning band for a whole-width sentence: at least one line tall, plus its own padding.</summary>
    private float BannerBand(UiWidgetContext ctx, string text)
    {
        float textWidth = Math.Max(1f, BodyWidth(ctx) - BannerTextInset * 2f);
        return Math.Max(BannerMinHeight, ctx.Metrics.MeasureText(text, UiFont.Tiny, textWidth) + BandVerticalPadding);
    }

    private float EmptyStateBand(UiWidgetContext ctx, string text)
    {
        return Math.Max(EmptyStateMinHeight, ctx.Metrics.MeasureText(text, UiFont.Small, BodyWidth(ctx)) + BandVerticalPadding);
    }

    private static bool MatchesSearch(VoicePackRowView row, string query)
    {
        if (query == null || query.Trim().Length == 0) return true;
        return row.SearchText.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0
            || row.Label.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0
            || row.DefName.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0
            || row.Key.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
