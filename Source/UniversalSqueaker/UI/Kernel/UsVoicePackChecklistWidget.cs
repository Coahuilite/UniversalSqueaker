using System;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// The checklist card's STATUS BAND composite: the three role-mapped domain conditions (dormant / target
/// unavailable / canonical conflict) and the orphan band with its destructive "Forget dangling" button.
/// Everything else the card used to draw - the search field, the pack rows and the empty states - is a
/// declarative manifest element since step B (<c>input/text-field</c>, <c>Repeat</c> + <c>&lt;Templates&gt;</c>,
/// <c>state/empty</c>), so this widget now owns one thing instead of six.
/// <para>
/// <b>Why this kind is the G5 citation, written down where the ruling requires it.</b> <c>chrome/banner</c>'s
/// schema carries no <c>Tone</c>, so a band whose appearance is decided by DOMAIN STATE rather than authored
/// per element (attention cyan for a missing required resource or an unresolved source, the unavailable
/// hatch for a dormant domain) cannot be expressed declaratively. The honest answer to a recorded gap is to
/// keep the consumer kind until the library provides the capability - not to publish a player-visible colour
/// regression so the migration looks complete.
/// </para>
/// <para>
/// The orphan band draws above the row list (step B moved it): a widget element is one arranged rect and
/// cannot straddle the manifest siblings that now hold the search field and the rows.
/// </para>
/// </summary>
public sealed class UsVoicePackChecklistWidget : UsSectionWidgetBase
{
    public const string KindName = "us/voice-pack-checklist";

    // Every player-facing string this widget draws. Element ids and binding keys stay machine tokens.
    private const string KeyDormantBanner = "US.Packs.Checklist.DormantBanner";
    private const string KeyTargetUnavailableBanner = "US.Packs.Checklist.TargetUnavailableBanner";
    private const string KeyConflictBanner = "US.Packs.Checklist.ConflictBanner";
    private const string KeyOrphanBanner = "US.Packs.Checklist.OrphanBanner";
    private const string KeyForgetUnavailable = "US.Packs.Checklist.ForgetUnavailable";

    private const float BannerGap = 4f;

    // Floors, not fixed sizes: every band below is grown by the text metrics so a translated sentence
    // that needs a second or third line is drawn instead of cut off.
    private const float BannerMinHeight = 30f;
    private const float BandVerticalPadding = 8f;
    private const float BannerTextInset = 8f;

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
        bindings.ValidateAction<UsDomainIdentity>("forget-unavailable", elementPath);
        // "search-text" and "toggle-pack" are deliberately not validated here any more: the search field is
        // an input/text-field element (which validates the binding itself) and the row toggle is the
        // item-local "enabled" binding of the Repeat's template.
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return BannerMinHeight + BannerGap;
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        VoicePackDomainView? selected = SelectedDomain(ctx);
        if (!selected.HasValue) return 0f;

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

        if (domain.OrphanCount > 0)
        {
            height += BannerBand(ctx, ctx.Translation.Translate(KeyOrphanBanner)) + BannerGap;
        }

        return height;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (TitleHidden)
        {
            // The manifest's Section container owns the card, so this widget draws its body only.
            DrawBands(rect, ctx);
            return;
        }

        DrawCard(rect, ctx, body => DrawBands(body, ctx));
    }

    private void DrawBands(Rect rect, UiWidgetContext ctx)
    {
        VoicePackDomainView? selected = SelectedDomain(ctx);
        if (!selected.HasValue) return;

        VoicePackDomainView domain = selected.Value;
        float y = rect.y;

        if (domain.IsDormant)
        {
            y = DrawBanner(rect, y, ctx.Translation.Translate(KeyDormantBanner), ctx, BannerRole.Unavailable);
        }

        if (domain.IsTargetUnavailable)
        {
            y = DrawBanner(rect, y, ctx.Translation.Translate(KeyTargetUnavailableBanner), ctx, BannerRole.Attention);
        }

        if (domain.HasCanonicalConflict)
        {
            y = DrawBanner(rect, y, ctx.Translation.Translate(KeyConflictBanner), ctx, BannerRole.Attention);
        }

        if (domain.OrphanCount > 0)
        {
            DrawOrphanBanner(
                new Rect(rect.x, y, rect.width, BannerBand(ctx, ctx.Translation.Translate(KeyOrphanBanner))),
                domain,
                ctx);
        }
    }

    /// <summary>
    /// What a whole-width banner means, so the four domain conditions stop sharing one treatment.
    /// <see cref="Attention"/> is the ruling's "a condition worth investigating" - a missing required
    /// resource (target not loaded) or an unresolved source (multiple Defs share the target, pack keys no
    /// longer installed). <see cref="Unavailable"/> is the ruling's "unavailable control" row: a readable
    /// disabled treatment plus the sentence that explains it. A destructive ACTION is neither: it keeps the
    /// danger family at its own call site (see <see cref="DrawOrphanBanner"/>).
    /// </summary>
    private enum BannerRole
    {
        Attention,
        Unavailable
    }

    private float DrawBanner(Rect outer, float y, string text, UiWidgetContext ctx, BannerRole role)
    {
        float band = BannerBand(ctx, text);
        Rect bannerRect = new(outer.x, y, outer.width, band);
        Color ink;
        if (role == BannerRole.Attention)
        {
            // Neutral panel fill plus the cyan border and thin edge: the ruling's default treatment for an
            // attention band, and deliberately not a new tinted surface token. The sentence stays primary
            // ink - a cyan-on-cyan label would fail the same substrate rule the filled badge is held to.
            UsAttention.Band(bannerRect, ctx.Theme);
            UsAttention.Rail(bannerRect, ctx.Theme);
            ink = ctx.Theme.TextPrimary;
        }
        else
        {
            // "Biotech is not active" is neither attention nor destruction: the domain is unavailable, so
            // the band takes the kernel's disabled row treatment (neutral plane + hatch, the shape spec 1.4
            // gives an unavailable row) and its own sentence is the reason.
            UsKernelDraw.RowSurface(bannerRect, ctx.Theme, hovered: false, UsKernelDraw.RowRail.Disabled);
            ink = ctx.Theme.TextDisabled;
        }

        UsKernelDraw.Label(
            new Rect(bannerRect.x + BannerTextInset, bannerRect.y, Math.Max(1f, bannerRect.width - BannerTextInset * 2f), band),
            text,
            ctx.Theme,
            ink,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
        return y + band + BannerGap;
    }

    private void DrawOrphanBanner(Rect rect, VoicePackDomainView domain, UiWidgetContext ctx)
    {
        // "Selected pack keys are no longer installed" is an unresolved source: an attention condition,
        // not a destructive one. The band therefore takes the attention treatment and primary ink, while
        // the button keeps the danger family below - forgetting dangling keys has no undo.
        UsAttention.Band(rect, ctx.Theme);
        UsAttention.Rail(rect, ctx.Theme);

        Rect button = new(rect.xMax - 132f, rect.y + 5f, 124f, Math.Max(20f, rect.height - 10f));
        UsKernelDraw.HelpHover(button, ctx, "us/voice-pack-checklist/forget");
        Rect text = new(rect.x + 8f, rect.y + 5f, Math.Max(1f, button.x - rect.x - 16f), Math.Max(1f, rect.height - 10f));
        UsKernelDraw.Label(
            text,
            ctx.Translation.Translate(KeyOrphanBanner),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        if (UsKernelDraw.SelectionButton(button, ctx, ctx.Translation.Translate(KeyForgetUnavailable), ctx.Theme, selected: true, danger: true, font: UiFont.Tiny))
        {
            ctx.Bindings.Invoke(
                "forget-unavailable",
                new UsDomainIdentity(domain.Scope, domain.RaceDefName, domain.TargetDefName));
        }
    }

    /// <summary>The selected domain, or null: no domain means this composite draws nothing at all.</summary>
    private static VoicePackDomainView? SelectedDomain(UiWidgetContext ctx)
    {
        return ctx.Bindings.TryGet("selected-domain", out VoicePackDomainView? selected) ? selected : null;
    }

    /// <summary>Warning band for a whole-width sentence: at least one line tall, plus its own padding.</summary>
    private float BannerBand(UiWidgetContext ctx, string text)
    {
        float textWidth = Math.Max(1f, BodyWidth(ctx) - BannerTextInset * 2f);
        return Math.Max(BannerMinHeight, ctx.Metrics.MeasureText(text, UiFont.Tiny, textWidth) + BandVerticalPadding);
    }
}
