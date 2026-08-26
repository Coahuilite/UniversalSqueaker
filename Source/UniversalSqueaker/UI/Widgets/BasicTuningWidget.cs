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
/// Row heights mirror the legacy VoicePacks page (egg 28f, distance 28f, basic 26f).
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

        return TopPadding + EggRowHeight + DistanceRowHeight
            + BasicRowHeight * 3f + BasicRowGap * 2f + BottomPadding;
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y + TopPadding;
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);

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

    private static void DrawEggRow(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        bool enabled = ctx.TryGetViewValue("AllowEasterEggs", out object? value) && value is true;
        bool hovered = Mouse.IsOver(rect);
        Widgets.DrawBoxSolid(rect, hovered ? new Color(.16f, .145f, .12f, .94f) : UiPalette.Panel);
        SectionFrame.DrawBorder(rect);
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
        Widgets.DrawBoxSolid(rect, hovered ? new Color(.16f, .145f, .12f, .94f) : UiPalette.Panel);
        SectionFrame.DrawBorder(rect);
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
        Widgets.DrawBoxSolid(rect, hovered ? new Color(.16f, .145f, .12f, .94f) : UiPalette.Panel);
        SectionFrame.DrawBorder(rect);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 3f, rect.width - 60f, 20f), label);

        Rect checkRect = new(rect.xMax - 40f, rect.y + 4f, 20f, 20f);
        bool checkboxValue = enabled;
        Widgets.Checkbox(checkRect.position, ref checkboxValue, 20f);
        GUI.color = oldColor;
        Text.Font = oldFont;

        if (Widgets.ButtonInvisible(rect))
            emit?.Invoke(new UiCommand(UiCommandKind.ToggleBasic, arg: arg, flag: !enabled));
    }

    private static void DrawLabel(Rect rect, string label, string subLabel)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 4f, rect.width - 20f, 20f), label);
        Text.Font = GameFont.Tiny;
        GUI.color = new Color(.82f, .80f, .74f, .92f);
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 20f, rect.width - 20f, 20f), subLabel);
        Text.Font = oldFont;
        GUI.color = oldColor;
    }
}
