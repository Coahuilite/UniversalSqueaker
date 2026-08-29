using System;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// US page title composite: title text, the help "?" toggle, and the help banner when open.
/// Emits <c>ToggleHelp</c>; the Ferrite page owns and flips <see cref="UiPageState.HelpOpen"/>.
/// </summary>
public sealed class PageTitleWidget : IWidget
{
    public const string Kind = "us/page-title";

    private const string DefaultTitle = "VoicePack Routing";

    private const float MinimumTitleAndHelpWidth = 48f;

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        float width = VoicePacksLayout.InnerWidth(ctx.ViewWidth);
        float height = VoicePacksLayout.TitleHeight + VoicePacksLayout.Gap;
        if (NeedsHelpOnNextLine(width))
            height += VoicePacksLayout.TitleHeight + VoicePacksLayout.Gap;

        if (ctx.State.HelpOpen)
        {
            string helpText = ResolveHelpText(ctx);
            if (helpText.Length > 0)
            {
                var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
                height += VoicePacksLayout.BannerHeight(helpText, width, metrics) + VoicePacksLayout.Gap;
            }
        }
        return height;
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UsGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit),
            fallback => DrawVanilla(fallback, ctx, emit),
            Kind);
    }

    private void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y;

        if (NeedsHelpOnNextLine(innerWidth))
        {
            UsWidgetDrawing.DrawTitle(
                new Rect(x, y, innerWidth, VoicePacksLayout.TitleHeight),
                ResolveTitle());
            y += VoicePacksLayout.TitleHeight + VoicePacksLayout.Gap;

            Rect helpRect = new(x, y, VoicePacksLayout.TitleHeight, VoicePacksLayout.TitleHeight);
            if (HelpToggle.Draw(helpRect, ctx.State.HelpOpen))
            {
                emit(new KitUiCommand("ToggleHelp"));
            }

            y += VoicePacksLayout.TitleHeight + VoicePacksLayout.Gap;
        }
        else
        {
            Rect titleRect = new(
                x,
                y,
                Math.Max(1f, innerWidth - VoicePacksLayout.TitleHeight - 6f),
                VoicePacksLayout.TitleHeight);
            UsWidgetDrawing.DrawTitle(titleRect, ResolveTitle());

            Rect helpRect = new(
                x + innerWidth - VoicePacksLayout.TitleHeight,
                y,
                VoicePacksLayout.TitleHeight,
                VoicePacksLayout.TitleHeight);
            if (HelpToggle.Draw(helpRect, ctx.State.HelpOpen))
            {
                emit(new KitUiCommand("ToggleHelp"));
            }

            y += VoicePacksLayout.TitleHeight + VoicePacksLayout.Gap;
        }

        if (ctx.State.HelpOpen)
        {
            string helpText = ResolveHelpText(ctx);
            if (helpText.Length > 0)
            {
                var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
                float helpHeight = VoicePacksLayout.BannerHeight(helpText, innerWidth, metrics);
                StatusBanner.Draw(
                    new Rect(x, y, innerWidth, helpHeight),
                    helpText,
                    UsSurface.SurfaceKind.Panel);
            }
        }
    }

    private static void DrawVanilla(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        Widgets.Label(rect, ResolveTitleStatic());
        if (Widgets.ButtonText(new Rect(rect.xMax - 24f, rect.y, 22f, 22f), "?"))
        {
            emit(new KitUiCommand("ToggleHelp"));
        }
    }

    private static string ResolveTitleStatic()
    {
        return "VoicePack Routing";
    }

    private static bool NeedsHelpOnNextLine(float innerWidth) => innerWidth < MinimumTitleAndHelpWidth;

    private string ResolveTitle()
    {
        return _spec?.TryGetAttribute("Title", out string title) == true && title.Length > 0
            ? title
            : DefaultTitle;
    }

    private string ResolveHelpText(WidgetContext ctx)
    {
        if (_spec?.TryGetAttribute("Bind", out string key) == true
            && key.Length > 0
            && ctx.TryGetViewValue(key, out object? value)
            && value != null)
        {
            return value as string ?? "";
        }

        return _spec?.TryGetAttribute("Text", out string literal) == true ? literal : "";
    }
}
