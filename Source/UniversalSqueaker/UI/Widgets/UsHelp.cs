using System;
using FerriteLib.UiKit;
using UnityEngine;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>Shared inline-help plumbing for US widgets.</summary>
internal static class UsHelp
{
    internal static string ResolveKey(UiElementSpec? spec)
    {
        return spec != null && spec.TryGetAttribute("HelpKey", out string key) ? key : "";
    }

    internal static bool IsOpen(WidgetContext ctx, string helpKey)
    {
        return !string.IsNullOrEmpty(helpKey) && ctx.State.OpenHelpKeys.Contains(helpKey);
    }

    internal static float BannerHeight(WidgetContext ctx, string helpKey, float width)
    {
        string? text = UsHelpCatalog.Get(helpKey);
        if (text == null || text.Length == 0) return 0f;
        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
        return VoicePacksLayout.BannerHeight(text, width, metrics);
    }

    internal static void DrawHelpButton(Rect rect, string helpKey, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        UsHelpButton.Draw(rect, helpKey, ctx.State, emit);
    }

    internal static void DrawBanner(Rect rect, string helpKey, WidgetContext ctx)
    {
        string? text = UsHelpCatalog.Get(helpKey);
        if (text == null || text.Length == 0) return;
        StatusBanner.Draw(rect, text, UsSurface.SurfaceKind.Panel);
    }
}
