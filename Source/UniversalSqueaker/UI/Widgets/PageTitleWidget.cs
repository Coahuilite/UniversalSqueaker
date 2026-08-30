using System;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// US page title composite: title text only. Inline help was removed; help now lives in the
/// right-hand help panel.
/// </summary>
public sealed class PageTitleWidget : IWidget
{
    public const string Kind = "us/page-title";

    private const string DefaultTitle = "VoicePack Routing";

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        return UiGuard.MeasureOrFallback(
            () => VoicePacksLayout.TitleHeight + VoicePacksLayout.Gap,
            0f, Kind, "UniversalSqueaker");
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect),
            fallback => Widgets.Label(fallback, ResolveTitle()),
            Kind, "UniversalSqueaker");
    }

    private void DrawCore(Rect rect)
    {
        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y;
        UsWidgetDrawing.DrawTitle(new Rect(x, y, innerWidth, VoicePacksLayout.TitleHeight), ResolveTitle());
    }

    private string ResolveTitle()
    {
        return _spec?.TryGetAttribute("Title", out string title) == true && title.Length > 0
            ? title
            : DefaultTitle;
    }
}
