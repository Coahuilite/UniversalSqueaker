using System;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Workspace-only navigation for the five player tasks. It intentionally does not expose every
/// child control: each destination owns one coherent flow in the centre workspace.
///
/// Row heights come from the injected text metrics instead of a constant, because the description line
/// is a translated string: a fixed 16px band fits English but silently cuts the wrapped line of any
/// language that needs more room. One line costs exactly what it cost before, so the wide-window layout
/// is unchanged.
/// </summary>
public sealed class UsNavWidget : IUiWidget
{
    public const string Kind = "us/nav";

    private const float Gap = 5f;
    private const float SidePadding = 10f;
    private const float TopPadding = 12f;
    private const float LabelTop = 6f;
    private const float LabelHeight = 22f;
    private const float DescriptionGap = 1f;
    private const float DescriptionHeight = 16f;
    private const float BottomPadding = 11f;

    // Text bands lose 10px of left inset plus 8px of breathing room to the row edge.
    private const float TextInset = 10f;
    private const float TextRightReserve = 8f;

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

    public float Measure(UiWidgetContext ctx)
    {
        float textWidth = TextWidth(Math.Max(1f, ctx.ViewWidth));
        float total = TopPadding;
        for (int i = 0; i < Workspaces.Length; i++)
        {
            if (i > 0) total += Gap;
            total += ItemHeight(ctx, textWidth, Workspaces[i].DescriptionKey);
        }

        // Draw starts at rect.y + TopPadding and ends after the last item; no trailing TopPadding.
        return total;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiThemeDraw.BackgroundPlane(rect, ctx.Theme);
        UiThemeDraw.Surface(rect, ctx.Theme, Color.clear, ctx.Theme.Border);
        ctx.Bindings.TryGet(UiBindings.ActiveTabKey, out string activeTab);

        float innerWidth = Math.Max(1f, rect.width - SidePadding * 2f);
        float textWidth = TextWidth(rect.width);
        float y = rect.y + TopPadding;
        for (int i = 0; i < Workspaces.Length; i++)
        {
            (string tab, string labelKey, string descriptionKey) = Workspaces[i];
            float itemHeight = ItemHeight(ctx, textWidth, descriptionKey);
            Rect itemRect = new(rect.x + SidePadding, y, innerWidth, itemHeight);
            bool active = string.Equals(tab, activeTab, StringComparison.Ordinal);
            bool hovered = UsKernelDraw.HelpHover(itemRect, ctx, "us/page-title/nav");
            UiThemeDraw.StatusTreatment(itemRect, ctx.Theme, active ? UiStatusTone.Active : UiStatusTone.Neutral);
            UiThemeDraw.AccentRail(itemRect, ctx.Theme, active, 3f);
            UsKernelDraw.Label(new Rect(itemRect.x + TextInset, itemRect.y + LabelTop, textWidth, LabelHeight),
                ctx.Translation.Translate(labelKey),
                ctx.Theme, active ? ctx.Theme.TextOnGold : ctx.Theme.TextPrimary, UiFont.Small, TextAnchor.MiddleLeft);
            UsKernelDraw.Label(new Rect(itemRect.x + TextInset, itemRect.y + LabelTop + LabelHeight + DescriptionGap, textWidth, DescriptionBand(ctx, textWidth, descriptionKey)),
                ctx.Translation.Translate(descriptionKey),
                ctx.Theme, active ? ctx.Theme.TextOnGold : hovered ? ctx.Theme.TextPrimary : ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.UpperLeft);
            if (UiNative.Button(itemRect))
            {
                ctx.Bindings.Invoke("set-tab", tab);
            }
            y += itemHeight + Gap;
        }
    }

    private static float TextWidth(float rowWidth)
    {
        return Math.Max(1f, rowWidth - SidePadding * 2f - TextInset - TextRightReserve);
    }

    private static float DescriptionBand(UiWidgetContext ctx, float textWidth, string descriptionKey)
    {
        string description = ctx.Translation.Translate(descriptionKey);
        return Math.Max(DescriptionHeight, Math.Max(1f, ctx.Metrics.MeasureText(description, UiFont.Tiny, textWidth)));
    }

    private static float ItemHeight(UiWidgetContext ctx, float textWidth, string descriptionKey)
    {
        return LabelTop + LabelHeight + DescriptionGap + DescriptionBand(ctx, textWidth, descriptionKey) + BottomPadding;
    }
}
