using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// US composite widget: the tuning-baseline preset list (方案 C 分层预设 + 增量导入).
/// Lists every <see cref="UniversalSqueakerTuningBaselineDef"/> from
/// <c>DefDatabase&lt;UniversalSqueakerTuningBaselineDef&gt;.AllDefs</c>; each preset expands into a
/// race → xenotype checkbox tree. The Import button triggers
/// <see cref="BaselinePresetImporter.Import"/> for the player-checked rows through the business
/// <see cref="UiCommand"/> stream (<see cref="UsWidgetCommandAdapter.For"/>).
/// </summary>
public sealed class PresetListWidget : IWidget
{
    public const string Kind = "us/preset-list";

    private const string HeaderText = "Tuning Baseline Presets";
    private const float PresetHeaderHeight = 32f;
    private const float RaceRowHeight = 24f;
    private const float XenotypeRowHeight = 22f;
    private const float RowGap = 2f;
    private const float XenotypeIndent = 18f;
    private const float LeftPadding = 10f;
    private const float ImportButtonWidth = 76f;
    private const float ImportButtonHeight = 20f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        return UiGuard.MeasureOrFallback(() =>
        {
            if (!ctx.TryGetViewValue("BaselinePresets", out object? value)
                || value is not IReadOnlyList<BaselinePresetView> presets
                || presets.Count == 0)
            {
                return 0f;
            }

            float width = VoicePacksLayout.InnerWidth(ctx.ViewWidth);
            var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
            float headerHeight = VoicePacksLayout.SectionHeaderHeightFor(HeaderText, width, metrics);
            float height = TopPadding + headerHeight + VoicePacksLayout.Gap;

            foreach (BaselinePresetView preset in presets)
            {
                height += PresetHeaderHeight + VoicePacksLayout.Gap;
                if (!preset.Expanded) continue;
                if (!string.IsNullOrEmpty(preset.Description))
                    height += metrics.CalcHeight(preset.Description, Math.Max(1f, width - 16f)) + 6f + VoicePacksLayout.Gap;
                foreach (BaselineRaceView race in preset.Races)
                {
                    height += RaceRowHeight + RowGap;
                    height += race.Xenotypes.Count * (XenotypeRowHeight + RowGap);
                }
            }

            string helpKey = UsHelp.ResolveKey(_spec);
            if (UsHelp.IsOpen(ctx, helpKey))
            {
                height += UsHelp.BannerHeight(ctx, helpKey, width) + VoicePacksLayout.Gap;
            }
            return height + BottomPadding;
        }, 0f, Kind);
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        string helpKey = UsHelp.ResolveKey(_spec);
        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit, helpKey),
            fallback => Widgets.Label(fallback, HeaderText + " unavailable in fallback mode. Basic settings remain available."),
            Kind);
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit, string helpKey)
    {
        if (!ctx.TryGetViewValue("BaselinePresets", out object? value)
            || value is not IReadOnlyList<BaselinePresetView> presets
            || presets.Count == 0)
        {
            return;
        }

        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y + TopPadding;

        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
        float headerHeight = VoicePacksLayout.SectionHeaderHeightFor(HeaderText, innerWidth, metrics);
        Rect headerRect = new(x, y, innerWidth, headerHeight);
        UsWidgetDrawing.DrawSectionHeader(headerRect, HeaderText);
        Rect helpRect = new(headerRect.xMax - 22f, headerRect.y, 22f, Math.Min(22f, headerHeight));
        UsHelp.DrawHelpButton(helpRect, helpKey, ctx, emit);
        y += headerHeight + VoicePacksLayout.Gap;

        if (UsHelp.IsOpen(ctx, helpKey))
        {
            float helpHeight = UsHelp.BannerHeight(ctx, helpKey, innerWidth);
            if (helpHeight > 0f)
            {
                UsHelp.DrawBanner(new Rect(x, y, innerWidth, helpHeight), helpKey, ctx);
                y += helpHeight + VoicePacksLayout.Gap;
            }
        }

        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        float xenoIndent = VoicePacksLayout.ForWidth(rect.width) == LayoutTier.Comfortable ? XenotypeIndent : 12f;
        foreach (BaselinePresetView preset in presets)
        {
            DrawPresetHeader(new Rect(x, y, innerWidth, PresetHeaderHeight), preset, businessEmit);
            y += PresetHeaderHeight + VoicePacksLayout.Gap;

            if (!preset.Expanded) continue;

            if (!string.IsNullOrEmpty(preset.Description))
            {
                float descHeight = metrics.CalcHeight(preset.Description, Math.Max(1f, innerWidth - 16f)) + 6f;
                DrawDescription(new Rect(x, y, innerWidth, descHeight), preset.Description);
                y += descHeight + VoicePacksLayout.Gap;
            }

            foreach (BaselineRaceView race in preset.Races)
            {
                DrawRaceRow(new Rect(x, y, innerWidth, RaceRowHeight), preset.DefName, race, businessEmit);
                y += RaceRowHeight + RowGap;
                foreach (BaselineXenotypeView xenotype in race.Xenotypes)
                {
                    DrawXenotypeRow(new Rect(x + xenoIndent, y, Math.Max(1f, innerWidth - xenoIndent), XenotypeRowHeight), preset.DefName, race.RaceDefName, xenotype, businessEmit);
                    y += XenotypeRowHeight + RowGap;
                }
            }
        }
    }

    private static void DrawPresetHeader(Rect rect, BaselinePresetView preset, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);

        Rect importRect = new(rect.xMax - ImportButtonWidth - 8f, rect.y + (rect.height - ImportButtonHeight) / 2f, ImportButtonWidth, ImportButtonHeight);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        string label = (preset.Expanded ? "− " : "+ ") + preset.Label;
        Rect labelRect = new(rect.x + LeftPadding, rect.y + 3f, Math.Max(1f, importRect.x - rect.x - LeftPadding - 8f), 16f);
        Widgets.Label(labelRect, label);

        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextSecondary;
        string summary = preset.SelectedRaceCount + " races · " + preset.SelectedXenotypeCount + " xenotypes";
        Rect summaryRect = new(rect.x + LeftPadding + 4f, rect.y + 20f, Math.Max(1f, importRect.x - rect.x - LeftPadding - 16f), 11f);
        Widgets.Label(summaryRect, summary);
        Text.Font = oldFont;
        GUI.color = oldColor;

        UiInteract.Button(importRect, UiLayer.Content,
            () => emit?.Invoke(new UiCommand(UiCommandKind.ImportBaselinePreset, arg: preset.DefName)));

        Rect expandRect = new(rect.x, rect.y, Math.Max(1f, importRect.x - rect.x - 8f), rect.height);
        UiInteract.Row(expandRect, () => emit?.Invoke(new UiCommand(UiCommandKind.ToggleBaselinePreset, arg: preset.DefName)));
    }

    private static void DrawRaceRow(Rect rect, string presetDefName, BaselineRaceView race, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        string label = race.DisplayName + "  (" + race.ActionCount + " actions, " + race.MoodCount + " moods)";
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 3f, Math.Max(1f, rect.width - 64f), 18f), label);

        Rect checkRect = new(rect.xMax - 40f, rect.y + 2f, 20f, 20f);
        UsSurface.DrawCheckbox(checkRect, race.Selected);
        Text.Font = oldFont;
        GUI.color = oldColor;

        UiInteract.Row(rect, () => emit?.Invoke(new UiCommand(UiCommandKind.ToggleBaselineRace, arg: presetDefName, raceDefName: race.RaceDefName, flag: !race.Selected)));
    }

    private static void DrawXenotypeRow(Rect rect, string presetDefName, string raceDefName, BaselineXenotypeView xenotype, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextPrimary;
        string inheritTag = xenotype.InheritFromRace ? " (inherits race)" : " (own only)";
        string label = xenotype.DisplayName + inheritTag + "  (" + xenotype.ActionCount + " actions, " + xenotype.MoodCount + " moods)";
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 4f, Math.Max(1f, rect.width - 64f), 14f), label);

        Rect checkRect = new(rect.xMax - 40f, rect.y + 1f, 20f, 20f);
        UsSurface.DrawCheckbox(checkRect, xenotype.Selected);
        Text.Font = oldFont;
        GUI.color = oldColor;

        UiInteract.Row(rect, () => emit?.Invoke(new UiCommand(UiCommandKind.ToggleBaselineXenotype, arg: presetDefName, raceDefName: raceDefName, targetDefName: xenotype.XenotypeDefName, flag: !xenotype.Selected)));
    }

    private static void DrawDescription(Rect rect, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextSecondary;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 2f, Math.Max(1f, rect.width - LeftPadding - 8f), Math.Max(1f, rect.height - 4f)), text);
        Text.Font = oldFont;
        GUI.color = oldColor;
    }
}
