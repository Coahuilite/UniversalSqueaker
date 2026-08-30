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
/// The two-line rows (egg/distance) are measured through <see cref="VoicePacksLayout.TwoLineRowHeight"/>
/// so Measure and Draw stay in sync and never clip the secondary caption.
/// </summary>
public sealed class BasicTuningWidget : IWidget
{
    public const string Kind = "us/basic-tuning";

    private const float BasicRowHeight = 26f;
    private const float BasicRowGap = 2f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;
    private const float LeftPadding = 10f;

    private const string Title = "Basic toggles";
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

        float innerWidth = VoicePacksLayout.InnerWidth(ctx.ViewWidth);
        var smallMetrics = new FerriteTextMetricsAdapter(ctx.Metrics, UiFont.Small);
        float eggHeight = VoicePacksLayout.TwoLineRowHeight(EggLabel, EggSubLabel(ctx), innerWidth, smallMetrics);
        float distanceHeight = VoicePacksLayout.TwoLineRowHeight(DistanceLabel, DistanceDescription(ctx), innerWidth, smallMetrics);

        float bodyHeight = TopPadding + eggHeight + distanceHeight
            + BasicRowHeight * 3f + BasicRowGap * 2f + BottomPadding;
        return UiGuard.MeasureOrFallback(
            () => UsCard.Measure(bodyHeight, ctx),
            UsCard.Measure(bodyHeight, ctx),
            Kind, "UniversalSqueaker");
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit),
            fallback => DrawVanilla(fallback, ctx, emit),
            Kind, "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        UsCard.Draw(rect, Title, ctx, body => DrawBody(body, ctx, emit));
    }

    private static void DrawBody(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y + TopPadding;
        var smallMetrics = new FerriteTextMetricsAdapter(ctx.Metrics, UiFont.Small);
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);

        float eggHeight = VoicePacksLayout.TwoLineRowHeight(EggLabel, EggSubLabel(ctx), innerWidth, smallMetrics);
        DrawEggRow(new Rect(x, y, innerWidth, eggHeight), ctx, businessEmit, smallMetrics);
        y += eggHeight;

        float distanceHeight = VoicePacksLayout.TwoLineRowHeight(DistanceLabel, DistanceDescription(ctx), innerWidth, smallMetrics);
        DrawDistanceRow(new Rect(x, y, innerWidth, distanceHeight), ctx, businessEmit, smallMetrics);
        y += distanceHeight;

        DrawBasicRow(new Rect(x, y, innerWidth, BasicRowHeight), ctx, businessEmit, "ScaleCooldown", "Scale cooldown with time speed", "ScaleCooldownWithTimeSpeed");
        y += BasicRowHeight + BasicRowGap;
        DrawBasicRow(new Rect(x, y, innerWidth, BasicRowHeight), ctx, businessEmit, "ScaleTalking", "Scale frequency with talking", "ScaleFrequencyWithTalking");
        y += BasicRowHeight + BasicRowGap;
        DrawBasicRow(new Rect(x, y, innerWidth, BasicRowHeight), ctx, businessEmit, "ScalePopulation", "Scale periodic with audible population", "ScalePeriodicWithAudiblePopulation");
    }

    private static void DrawVanilla(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        var smallMetrics = new FerriteTextMetricsAdapter(ctx.Metrics, UiFont.Small);
        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float eggHeight = VoicePacksLayout.TwoLineRowHeight(EggLabel, EggSubLabel(ctx), innerWidth, smallMetrics);
        float distanceHeight = VoicePacksLayout.TwoLineRowHeight(DistanceLabel, DistanceDescription(ctx), innerWidth, smallMetrics);

        bool egg = ctx.TryGetViewValue("AllowEasterEggs", out object? eggValue) && eggValue is true;
        bool eggChecked = egg;
        Widgets.Checkbox(new Vector2(rect.x + 6f, rect.y + 5f), ref eggChecked, 18f);
        Widgets.Label(new Rect(rect.x + 30f, rect.y + 5f, rect.width - 36f, 20f), EggLabel);
        if (Widgets.ButtonInvisible(new Rect(rect.x, rect.y, rect.width, eggHeight)))
            businessEmit(new UiCommand(UiCommandKind.ToggleEgg, flag: !egg));

        float y = rect.y + eggHeight;
        Rect distanceRect = new(rect.x, y, rect.width, distanceHeight);
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

        y += distanceHeight;
        DrawVanillaBasicRow(new Rect(rect.x, y, rect.width, BasicRowHeight), ctx, businessEmit, "ScaleCooldown", "Scale cooldown with time speed", "ScaleCooldownWithTimeSpeed");
        y += BasicRowHeight + BasicRowGap;
        DrawVanillaBasicRow(new Rect(rect.x, y, rect.width, BasicRowHeight), ctx, businessEmit, "ScaleTalking", "Scale frequency with talking", "ScaleFrequencyWithTalking");
        y += BasicRowHeight + BasicRowGap;
        DrawVanillaBasicRow(new Rect(rect.x, y, rect.width, BasicRowHeight), ctx, businessEmit, "ScalePopulation", "Scale periodic with audible population", "ScalePeriodicWithAudiblePopulation");
    }

    private static void DrawEggRow(Rect rect, WidgetContext ctx, Action<UiCommand> emit, ITextMetrics metrics)
    {
        bool enabled = ctx.TryGetViewValue("AllowEasterEggs", out object? value) && value is true;
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);
        DrawLabel(rect, EggLabel, enabled ? "On (eggs join the pool)" : "Off (ordinary entries only)", metrics);

        UiInteract.Row(rect, () => emit?.Invoke(new UiCommand(UiCommandKind.ToggleEgg, flag: !enabled)));
    }

    private static void DrawDistanceRow(Rect rect, WidgetContext ctx, Action<UiCommand> emit, ITextMetrics metrics)
    {
        string raw = ctx.TryGetViewValue("DistancePreset", out object? value) && value is string text
            ? text
            : "";
        if (!Enum.TryParse(raw, true, out SqueakDistancePreset current))
            current = SqueakDistancePreset.Custom;

        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);
        DrawLabel(rect, DistanceLabel, DistanceDescription(ctx), metrics);

        UiInteract.Row(rect, () =>
        {
            SqueakDistancePreset next = current switch
            {
                SqueakDistancePreset.Conservative => SqueakDistancePreset.Balanced,
                SqueakDistancePreset.Balanced => SqueakDistancePreset.Strong,
                SqueakDistancePreset.Strong => SqueakDistancePreset.Conservative,
                _ => SqueakDistancePreset.Balanced,
            };
            emit?.Invoke(new UiCommand(UiCommandKind.SetDistancePreset, arg: next.ToString()));
        });
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

        UiInteract.Row(rect, () => emit?.Invoke(new UiCommand(UiCommandKind.ToggleBasic, arg: arg, flag: !enabled)));
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

    private static void DrawLabel(Rect rect, string label, string subLabel, ITextMetrics metrics)
    {
        float contentWidth = Math.Max(1f, rect.width - 20f);
        float labelHeight = Math.Max(20f, metrics.CalcHeight(label, contentWidth));

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 4f, contentWidth, labelHeight), label);

        float subLabelY = rect.y + 4f + labelHeight + 2f;
        float subLabelHeight = Math.Max(16f, rect.yMax - subLabelY - 4f);
        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextSecondary;
        Widgets.Label(new Rect(rect.x + LeftPadding, subLabelY, contentWidth, subLabelHeight), subLabel);
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    private static string EggSubLabel(WidgetContext ctx)
    {
        bool enabled = ctx.TryGetViewValue("AllowEasterEggs", out object? value) && value is true;
        return enabled ? "On (eggs join the pool)" : "Off (ordinary entries only)";
    }

    private static string DistanceDescription(WidgetContext ctx)
    {
        string raw = ctx.TryGetViewValue("DistancePreset", out object? value) && value is string text
            ? text
            : "";
        if (!Enum.TryParse(raw, true, out SqueakDistancePreset current))
            current = SqueakDistancePreset.Custom;
        return current switch
        {
            SqueakDistancePreset.Conservative => "Conservative (15~65)",
            SqueakDistancePreset.Strong => "Strong (15~40)",
            SqueakDistancePreset.Balanced => "Balanced (15~50)",
            _ => "Custom",
        };
    }
}
