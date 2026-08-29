using System;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// US composite widget: the A7/A3/A4 orphan controls under the Ferrite path.
/// Draws five stable rows — the Easter-egg toggle, the distance preset cycle, and the three
/// runtime scaling toggles — reading every value from the page view state and emitting
/// business <see cref="UiCommand"/>s through <see cref="UsWidgetCommandAdapter.For"/>.
/// Row heights mirror the componentized VoicePacks page (egg 28f, distance 28f, basic 26f).
/// </summary>
public sealed class BasicTuningWidget : IWidget
{
    public const string Kind = "us/basic-tuning";

    private const float EggRowHeight = 28f;
    private const float DistanceRowHeight = 28f;
    private const float BasicRowHeight = 26f;
    private const float BasicRowGap = 2f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;
    private const float LeftPadding = 10f;

    private const string EggLabel = "Easter egg sounds";
    private const string DistanceLabel = "Distance preset";

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        float height = TopPadding + EggRowHeight + DistanceRowHeight
            + BasicRowHeight * 3f + BasicRowGap * 2f + BottomPadding;
        string helpKey = UsHelp.ResolveKey(_spec);
        if (UsHelp.IsOpen(ctx, helpKey))
        {
            height += UsHelp.BannerHeight(ctx, helpKey, VoicePacksLayout.InnerWidth(ctx.ViewWidth)) + VoicePacksLayout.Gap;
        }
        return UsGuard.MeasureOrFallback(() => height, height, Kind);
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        string helpKey = UsHelp.ResolveKey(_spec);
        UsGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit, helpKey),
            fallback => DrawVanilla(fallback, ctx, emit),
            Kind);
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit, string helpKey)
    {
        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y + TopPadding;
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);

        Rect helpRect = new(rect.xMax - 22f, rect.y, 22f, 22f);
        UsHelp.DrawHelpButton(helpRect, helpKey, ctx, emit);
        if (UsHelp.IsOpen(ctx, helpKey))
        {
            float helpHeight = UsHelp.BannerHeight(ctx, helpKey, innerWidth);
            if (helpHeight > 0f)
            {
                UsHelp.DrawBanner(new Rect(x, y, innerWidth, helpHeight), helpKey, ctx);
                y += helpHeight + VoicePacksLayout.Gap;
            }
        }

        DrawEggRow(new Rect(x, y, innerWidth, EggRowHeight), ctx, businessEmit);
        y += EggRowHeight;

        DrawDistanceRow(new Rect(x, y, innerWidth, DistanceRowHeight), ctx, businessEmit);
        y += DistanceRowHeight;

        DrawBasicRow(new Rect(x, y, innerWidth, BasicRowHeight), ctx, businessEmit, "ScaleCooldown", "Scale cooldown with time speed", "ScaleCooldownWithTimeSpeed");
        y += BasicRowHeight + BasicRowGap;
        DrawBasicRow(new Rect(x, y, innerWidth, BasicRowHeight), ctx, businessEmit, "ScaleTalking", "Scale frequency with talking", "ScaleFrequencyWithTalking");
        y += BasicRowHeight + BasicRowGap;
        DrawBasicRow(new Rect(x, y, innerWidth, BasicRowHeight), ctx, businessEmit, "ScalePopulation", "Scale periodic with audible population", "ScalePeriodicWithAudiblePopulation");
    }

    private static void DrawVanilla(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        bool egg = ctx.TryGetViewValue("AllowEasterEggs", out object? eggValue) && eggValue is true;
        bool eggChecked = egg;
        Widgets.Checkbox(new Vector2(rect.x + 6f, rect.y + 5f), ref eggChecked, 18f);
        Widgets.Label(new Rect(rect.x + 30f, rect.y + 5f, rect.width - 36f, 20f), EggLabel);
        if (Widgets.ButtonInvisible(rect))
            businessEmit(new UiCommand(UiCommandKind.ToggleEgg, flag: !egg));

        float y = rect.y + EggRowHeight;
        Rect distanceRect = new(rect.x, y, rect.width, DistanceRowHeight);
        string raw = ctx.TryGetViewValue("DistancePreset", out object? value) && value is string text ? text : "";
        if (!Enum.TryParse(raw, true, out SqueakDistancePreset current)) current = SqueakDistancePreset.Custom;
        string desc = current switch
        {
            SqueakDistancePreset.Conservative => "Conservative (15~65)",
            SqueakDistancePreset.Strong => "Strong (15~40)",
            SqueakDistancePreset.Balanced => "Balanced (15~50)",
            _ => "Custom",
        };
        Widgets.Label(distanceRect, DistanceLabel + "  " + desc);
        if (Widgets.ButtonInvisible(distanceRect))
        {
            SqueakDistancePreset next = current switch
            {
                SqueakDistancePreset.Conservative => SqueakDistancePreset.Balanced,
                SqueakDistancePreset.Balanced => SqueakDistancePreset.Strong,
                SqueakDistancePreset.Strong => SqueakDistancePreset.Conservative,
                _ => SqueakDistancePreset.Balanced,
            };
            businessEmit(new UiCommand(UiCommandKind.SetDistancePreset, arg: next.ToString()));
        }

        y += DistanceRowHeight;
        DrawVanillaBasicRow(new Rect(rect.x, y, rect.width, BasicRowHeight), ctx, businessEmit, "ScaleCooldown", "Scale cooldown with time speed", "ScaleCooldownWithTimeSpeed");
        y += BasicRowHeight + BasicRowGap;
        DrawVanillaBasicRow(new Rect(rect.x, y, rect.width, BasicRowHeight), ctx, businessEmit, "ScaleTalking", "Scale frequency with talking", "ScaleFrequencyWithTalking");
        y += BasicRowHeight + BasicRowGap;
        DrawVanillaBasicRow(new Rect(rect.x, y, rect.width, BasicRowHeight), ctx, businessEmit, "ScalePopulation", "Scale periodic with audible population", "ScalePeriodicWithAudiblePopulation");
    }

    private static void DrawEggRow(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        bool enabled = ctx.TryGetViewValue("AllowEasterEggs", out object? value) && value is true;
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);
        DrawLabel(rect, EggLabel, enabled ? "On (eggs join the pool)" : "Off (ordinary entries only)");

        if (Widgets.ButtonInvisible(rect))
            emit?.Invoke(new UiCommand(UiCommandKind.ToggleEgg, flag: !enabled));
    }

    private static void DrawDistanceRow(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        string raw = ctx.TryGetViewValue("DistancePreset", out object? value) && value is string text
            ? text
            : "";
        if (!Enum.TryParse(raw, true, out SqueakDistancePreset current))
            current = SqueakDistancePreset.Custom;

        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);
        string desc = current switch
        {
            SqueakDistancePreset.Conservative => "Conservative (15~65)",
            SqueakDistancePreset.Strong => "Strong (15~40)",
            SqueakDistancePreset.Balanced => "Balanced (15~50)",
            _ => "Custom",
        };
        DrawLabel(rect, DistanceLabel, desc);

        if (Widgets.ButtonInvisible(rect))
        {
            SqueakDistancePreset next = current switch
            {
                SqueakDistancePreset.Conservative => SqueakDistancePreset.Balanced,
                SqueakDistancePreset.Balanced => SqueakDistancePreset.Strong,
                SqueakDistancePreset.Strong => SqueakDistancePreset.Conservative,
                _ => SqueakDistancePreset.Balanced,
            };
            emit?.Invoke(new UiCommand(UiCommandKind.SetDistancePreset, arg: next.ToString()));
        }
    }

    private static void DrawBasicRow(
        Rect rect,
        WidgetContext ctx,
        Action<UiCommand> emit,
        string arg,
        string label,
        string viewKey)
    {
        bool enabled = ctx.TryGetViewValue(viewKey, out object? value) && value is true;
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 3f, Math.Max(1f, rect.width - 60f), 20f), label);

        Rect checkRect = new(rect.xMax - 34f, rect.y + 4f, 18f, 18f);
        UsSurface.DrawCheckbox(checkRect, enabled);
        GUI.color = oldColor;
        Text.Font = oldFont;

        if (Widgets.ButtonInvisible(rect))
            emit?.Invoke(new UiCommand(UiCommandKind.ToggleBasic, arg: arg, flag: !enabled));
    }

    private static void DrawVanillaBasicRow(
        Rect rect,
        WidgetContext ctx,
        Action<UiCommand> emit,
        string arg,
        string label,
        string viewKey)
    {
        bool enabled = ctx.TryGetViewValue(viewKey, out object? value) && value is true;
        bool checkboxValue = enabled;
        Widgets.Checkbox(new Vector2(rect.xMax - 34f, rect.y + 4f), ref checkboxValue, 18f);
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 3f, Math.Max(1f, rect.width - 60f), 20f), label);
        if (Widgets.ButtonInvisible(rect))
            emit?.Invoke(new UiCommand(UiCommandKind.ToggleBasic, arg: arg, flag: !enabled));
    }

    private static void DrawLabel(Rect rect, string label, string subLabel)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 4f, Math.Max(1f, rect.width - 20f), 20f), label);
        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextSecondary;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 20f, Math.Max(1f, rect.width - 20f), 20f), subLabel);
        Text.Font = oldFont;
        GUI.color = oldColor;
    }
}
