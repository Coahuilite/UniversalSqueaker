using System;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Workspace-only navigation for the five player tasks. It intentionally does not expose every
/// child control: each destination owns one coherent flow in the centre workspace.
///
/// Geometry contract: one fixed column width comes from the manifest (160 since the compact-nav ruling),
/// every card fills it exactly, and every card has the SAME outer bounds whichever one is selected. Card
/// height is a single shared number, not a per-card measurement: <see cref="SubtitleLines"/> Tiny lines
/// are reserved for the subtitle on every card in every language, so a wrapping translation can no longer
/// make one card taller than its siblings. The label and subtitle are ellipsized inside the card (through
/// the injected metric seam) rather than allowed to resize it. Selected state changes ink, fill and rail
/// only.
/// <para>
/// COMPACT PASS (user feedback, 2026-09-13): the stack was too tall in game. Paddings are tightened and
/// the reserved subtitle band is one line instead of two, which is a user-directed deviation from the
/// brief's "two-line subtitle band" wording. The band is still ONE shared constant for all five cards, so
/// the stability contract is unchanged; setting <see cref="SubtitleLines"/> back to 2 restores the previous
/// shape in one line.
/// </para>
/// </summary>
public sealed class UsNavWidget : IUiWidget
{
    public const string Kind = "us/nav";

    // Compact-nav paddings (user feedback): the stack was too tall in game.
    private const float Gap = 4f;
    private const float SidePadding = 8f;
    private const float TopPadding = 10f;
    private const float LabelTop = 4f;
    private const float LabelHeight = 20f;
    private const float DescriptionGap = 1f;
    private const float BottomPadding = 6f;

    // Text bands lose 8px of left inset plus 6px of breathing room to the row edge.
    private const float TextInset = 8f;
    private const float TextRightReserve = 6f;

    /// <summary>
    /// Lines reserved for the subtitle on EVERY card, in every language. This is what makes the stack
    /// geometrically stable: the old per-card description band sized each card from its own translation,
    /// so one wrapping subtitle moved every card below it.
    /// <para>
    /// One line since the compact-nav ruling (user-directed deviation from the brief's "two-line subtitle
    /// band"): the band is still one shared value for all five cards, and the subtitle is ellipsized into
    /// it. Set this back to 2 to restore the previous, taller stack in one line.
    /// </para>
    /// </summary>
    private const int SubtitleLines = 1;

    /// <summary>
    /// Tab is a machine token: it is compared against the persisted <c>UiBindings.ActiveTabKey</c>
    /// binding and against the manifest's Tab attributes, so it never gets translated. Label and Description are Keyed.
    /// </summary>
    private static readonly (string Tab, string LabelKey, string DescriptionKey)[] Workspaces =
    {
        ("Overview", "US.Nav.Overview.Label", "US.Nav.Overview.Description"),
        ("Distance", "US.Nav.Distance.Label", "US.Nav.Distance.Description"),
        ("Packs", "US.Nav.Packs.Label", "US.Nav.Packs.Description"),
        ("Tuning", "US.Nav.Tuning.Label", "US.Nav.Tuning.Description"),
        ("Presets", "US.Nav.Presets.Label", "US.Nav.Presets.Description")
    };

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            Kind,
            () => new UsNavWidget(),
            new[] { "Id", "Kind", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec value)
    {
        spec = value ?? throw new ArgumentNullException(nameof(value));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<string>(UiBindings.ActiveTabKey, elementPath);
        bindings.ValidateAction<string>("set-tab", elementPath);
    }

    /// <summary>
    /// The stack height: the header inset, five identical cards and four identical gaps. Draw walks the
    /// exact same sequence (see <see cref="Draw"/>), so Measure returns what is drawn - never a value
    /// derived from a per-card text measurement.
    /// </summary>
    public float Measure(UiWidgetContext ctx)
    {
        return TopPadding + Workspaces.Length * CardHeight(ctx) + (Workspaces.Length - 1) * Gap;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiThemeDraw.BackgroundPlane(rect, ctx.Theme);
        UiThemeDraw.Surface(rect, ctx.Theme, Color.clear, ctx.Theme.Border);
        ctx.Bindings.TryGet(UiBindings.ActiveTabKey, out string activeTab);

        float innerWidth = Math.Max(1f, rect.width - SidePadding * 2f);
        float textWidth = TextWidth(rect.width);
        float cardHeight = CardHeight(ctx);
        float subtitleBand = SubtitleBand(ctx);
        float y = rect.y + TopPadding;
        for (int i = 0; i < Workspaces.Length; i++)
        {
            (string tab, string labelKey, string descriptionKey) = Workspaces[i];
            Rect card = new(rect.x + SidePadding, y, innerWidth, cardHeight);
            bool active = string.Equals(tab, activeTab, StringComparison.Ordinal);
            bool hovered = UsKernelDraw.HelpHover(card, ctx, "us/page-title/nav");

            // Selected/unselected differ in ink, fill and rail ONLY: the rect handed to every state is the
            // same one, so the two states cannot diverge in x, width or height.
            UiThemeDraw.StatusTreatment(card, ctx.Theme, active ? UiStatusTone.Active : UiStatusTone.Neutral);
            UiThemeDraw.AccentRail(card, ctx.Theme, active, 3f);

            // Both bands are ellipsized into the card's fixed text column through the same metric seam the
            // fit audit reads, so a long translation is cut deliberately instead of resizing the card.
            string label = UsKernelDraw.Ellipsized(ctx.Translation.Translate(labelKey), ctx, UiFont.Small, textWidth);
            UsKernelDraw.Label(
                new Rect(card.x + TextInset, card.y + LabelTop, textWidth, LabelHeight),
                label,
                ctx.Theme,
                active ? ctx.Theme.TextOnGold : ctx.Theme.TextPrimary,
                UiFont.Small,
                TextAnchor.MiddleLeft,
                singleLine: true);

            string description = UsKernelDraw.EllipsizedToLines(
                ctx.Translation.Translate(descriptionKey), ctx, UiFont.Tiny, textWidth, SubtitleLines);
            UsKernelDraw.Label(
                new Rect(card.x + TextInset, card.y + LabelTop + LabelHeight + DescriptionGap, textWidth, subtitleBand),
                description,
                ctx.Theme,
                active ? ctx.Theme.TextOnGold : hovered ? ctx.Theme.TextPrimary : ctx.Theme.TextSecondary,
                UiFont.Tiny,
                TextAnchor.UpperLeft);

            if (UiNative.Button(card, ctx))
            {
                ctx.Bindings.Invoke("set-tab", tab);
            }

            y += cardHeight + Gap;
        }
    }

    /// <summary>One shared card height for all five cards: the label band plus the reserved subtitle band.
    /// Nothing in this formula reads a translation, so the five cards cannot disagree.</summary>
    private static float CardHeight(UiWidgetContext ctx)
    {
        return LabelTop + LabelHeight + DescriptionGap + SubtitleBand(ctx) + BottomPadding;
    }

    /// <summary>The reserved subtitle band: <see cref="SubtitleLines"/> Tiny line advances, measured
    /// through the injected seam so the harness and the game reserve the same band.</summary>
    private static float SubtitleBand(UiWidgetContext ctx)
    {
        return SubtitleLines * UsKernelDraw.LineHeightOf(ctx, UiFont.Tiny);
    }

    private static float TextWidth(float rowWidth)
    {
        return Math.Max(1f, rowWidth - SidePadding * 2f - TextInset - TextRightReserve);
    }
}
