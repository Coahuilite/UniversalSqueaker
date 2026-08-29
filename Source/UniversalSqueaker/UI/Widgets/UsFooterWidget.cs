using System;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// US footer dual-slot widget: left build identity, right save state. Non-interactive.
/// Reads "BuildIdentity", "SaveStatus" and "IsDirty" from the page view state.
/// </summary>
public sealed class UsFooterWidget : IWidget
{
    public const string Kind = "us/footer";

    public const float FooterHeight = 28f;
    private const float Padding = 10f;

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        return UiGuard.MeasureOrFallback(() => FooterHeight, FooterHeight, Kind);
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx),
            fallback => DrawVanilla(fallback, ctx),
            Kind);
    }

    private static void DrawCore(Rect rect, WidgetContext ctx)
    {
        string buildIdentity = ReadString(ctx, "BuildIdentity");
        string saveStatus = ReadString(ctx, "SaveStatus");
        bool isDirty = ReadBool(ctx, "IsDirty");

        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), UsVisualTokens.Border);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = UsVisualTokens.TextSecondary;
        Widgets.Label(new Rect(rect.x + Padding, rect.y, Math.Max(1f, rect.width * 0.5f - Padding), rect.height), buildIdentity);

        Color statusColor = saveStatus switch
        {
            "Failed" => UsVisualTokens.Danger,
            "Saving" => UsVisualTokens.AccentGold,
            _ => UsVisualTokens.TextSecondary,
        };
        if (isDirty && saveStatus != "Failed")
        {
            statusColor = UsVisualTokens.AccentGold;
        }
        string prefix = saveStatus == "Saving" || isDirty ? "● " : "";
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = statusColor;
        Widgets.Label(new Rect(rect.xMax - rect.width * 0.5f, rect.y, Math.Max(1f, rect.width * 0.5f - Padding), rect.height), prefix + saveStatus);

        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;
    }

    private static void DrawVanilla(Rect rect, WidgetContext ctx)
    {
        string buildIdentity = ReadString(ctx, "BuildIdentity");
        string saveStatus = ReadString(ctx, "SaveStatus");
        Widgets.Label(new Rect(rect.x + 4f, rect.y, Math.Max(1f, rect.width * 0.5f - 8f), rect.height), buildIdentity);
        Widgets.Label(new Rect(rect.x + rect.width * 0.5f, rect.y, Math.Max(1f, rect.width * 0.5f - 8f), rect.height), saveStatus);
    }

    private static string ReadString(WidgetContext ctx, string key)
    {
        return ctx.TryGetViewValue(key, out object? value) && value is string s ? s : "";
    }

    private static bool ReadBool(WidgetContext ctx, string key)
    {
        return ctx.TryGetViewValue(key, out object? value) && value is true;
    }
}
