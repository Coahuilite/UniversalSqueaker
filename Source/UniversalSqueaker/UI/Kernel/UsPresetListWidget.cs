using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US tuning-baseline preset list. Lists every baseline preset from the typed
/// "baseline-presets" read binding; each preset expands into a race → xenotype checkbox tree and
/// an Import button. All writes are typed actions ("toggle-baseline-preset",
/// "toggle-baseline-race", "toggle-baseline-xenotype", "import-baseline").
/// </summary>
public sealed class UsPresetListWidget : UsSectionWidgetBase
{
    public const string KindName = "us/preset-list";

    private const float PresetHeaderHeight = 40f;
    private const float HeaderTitleMinHeight = 16f;
    private const float HeaderTopPadding = 3f;
    private const float HeaderInnerGap = 2f;
    private const float HeaderBottomPadding = 3f;

    // The selection summary is a Tiny line under the preset title; 40f header = title band + gap + this
    // band + padding, so the summary is no longer cut to an 11px sliver.
    private const float SelectionSummaryHeight = 16f;

    private const float RaceRowHeight = 24f;
    private const float XenotypeRowHeight = 22f;
    private const float RowGap = 2f;
    private const float XenotypeIndent = 18f;
    private const float LeftPadding = 10f;
    private const float ImportButtonWidth = 76f;
    private const float ImportButtonHeight = 20f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;

    // Keyed UI text. English values in the Keyed table are verbatim copies of the former literals;
    // every string is resolved through Tr() so Measure and Draw share one outlet.
    private const string EmptyTextKey = "US.Preset.List.Empty";
    private const string SelectionSummaryKey = "US.Preset.List.Selection";
    private const string ImportKey = "US.Preset.List.Import";
    private const string RaceSummaryKey = "US.Preset.Race.Summary";
    private const string XenotypeSummaryKey = "US.Preset.Xeno.Summary";
    private const string XenotypeInheritsKey = "US.Preset.Xeno.Inherits";
    private const string XenotypeOwnOnlyKey = "US.Preset.Xeno.Own";

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsPresetListWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<IReadOnlyList<BaselinePresetView>>("baseline-presets", elementPath);
        bindings.ValidateAction<string>("toggle-baseline-preset", elementPath);
        bindings.ValidateAction<UsBaselineRaceToggle>("toggle-baseline-race", elementPath);
        bindings.ValidateAction<UsBaselineXenoToggle>("toggle-baseline-xenotype", elementPath);
        bindings.ValidateAction<string>("import-baseline", elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return TopPadding + 48f + BottomPadding;
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        IReadOnlyList<BaselinePresetView> presets = ctx.Bindings.TryGet("baseline-presets", out IReadOnlyList<BaselinePresetView> p)
            ? p
            : Array.Empty<BaselinePresetView>();
        if (presets.Count == 0) return TopPadding + 48f + BottomPadding;

        float width = BodyWidth(ctx);
        float xenoIndent = ctx.ViewWidth >= 900f ? XenotypeIndent : 12f;
        float bodyHeight = TopPadding;
        foreach (BaselinePresetView preset in presets)
        {
            bodyHeight += HeaderBands(ctx, preset, width).Total + RowGap;
            if (!preset.Expanded) continue;
            if (!string.IsNullOrEmpty(preset.Description))
            {
                bodyHeight += ctx.Metrics.MeasureText(preset.Description, UiFont.Tiny, Math.Max(1f, width - 16f)) + 6f + RowGap;
            }

            foreach (BaselineRaceView race in preset.Races)
            {
                bodyHeight += MeasuredRowHeight(RaceLabel(ctx, race), Math.Max(1f, width - 64f), ctx, RaceRowHeight) + RowGap;
                float xenoWidth = Math.Max(1f, width - xenoIndent);
                foreach (BaselineXenotypeView xenotype in race.Xenotypes)
                {
                    bodyHeight += MeasuredRowHeight(XenotypeLabel(ctx, xenotype), Math.Max(1f, xenoWidth - 64f), ctx, XenotypeRowHeight) + RowGap;
                }
            }
        }

        return bodyHeight + BottomPadding;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        IReadOnlyList<BaselinePresetView> presets = ctx.Bindings.TryGet("baseline-presets", out IReadOnlyList<BaselinePresetView> p)
            ? p
            : Array.Empty<BaselinePresetView>();
        if (presets.Count == 0)
        {
            UsKernelDraw.Label(
                new Rect(rect.x, rect.y + TopPadding, rect.width, 48f),
                Tr(ctx, EmptyTextKey),
                ctx.Theme,
                ctx.Theme.TextSecondary,
                UiFont.Small,
                TextAnchor.MiddleCenter);
            return;
        }

        float innerWidth = rect.width;
        float x = rect.x;
        float y = rect.y + TopPadding;
        float xenoIndent = ctx.ViewWidth >= 900f ? XenotypeIndent : 12f;

        foreach (BaselinePresetView preset in presets)
        {
            (float headerTitle, float headerSummary, float headerTotal) = HeaderBands(ctx, preset, innerWidth);
            DrawPresetHeader(new Rect(x, y, innerWidth, headerTotal), preset, headerTitle, headerSummary, ctx);
            y += headerTotal + RowGap;

            if (!preset.Expanded) continue;

            if (!string.IsNullOrEmpty(preset.Description))
            {
                float descHeight = ctx.Metrics.MeasureText(preset.Description, UiFont.Tiny, Math.Max(1f, innerWidth - 16f)) + 6f;
                UsKernelDraw.Label(
                    new Rect(x + LeftPadding, y + 2f, Math.Max(1f, innerWidth - LeftPadding - 8f), descHeight - 4f),
                    preset.Description,
                    ctx.Theme,
                    ctx.Theme.TextSecondary,
                    UiFont.Tiny,
                    TextAnchor.UpperLeft);
                y += descHeight + RowGap;
            }

            foreach (BaselineRaceView race in preset.Races)
            {
                float raceRowHeight = MeasuredRowHeight(RaceLabel(ctx, race), Math.Max(1f, innerWidth - 64f), ctx, RaceRowHeight);
                DrawRaceRow(new Rect(x, y, innerWidth, raceRowHeight), preset.DefName, race, ctx);
                y += raceRowHeight + RowGap;

                float xenoWidth = Math.Max(1f, innerWidth - xenoIndent);
                foreach (BaselineXenotypeView xenotype in race.Xenotypes)
                {
                    float xenoRowHeight = MeasuredRowHeight(XenotypeLabel(ctx, xenotype), Math.Max(1f, xenoWidth - 64f), ctx, XenotypeRowHeight);
                    DrawXenotypeRow(new Rect(x + xenoIndent, y, xenoWidth, xenoRowHeight), preset.DefName, race.RaceDefName, xenotype, ctx);
                    y += xenoRowHeight + RowGap;
                }
            }
        }
    }

    private void DrawPresetHeader(
        Rect rect, BaselinePresetView preset, float titleBand, float summaryBand, UiWidgetContext ctx)
    {
        // The header claims the tree entry first; the import button re-claims below, so hovering
        // the button shows "Import" and anywhere else on the header shows the tree.
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/preset-list/tree");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, preset.Expanded);

        Rect importRect = new(rect.xMax - ImportButtonWidth - 8f, rect.y + (rect.height - ImportButtonHeight) / 2f, ImportButtonWidth, ImportButtonHeight);
        UsKernelDraw.HelpHover(importRect, ctx, "us/preset-list/import");

        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y + HeaderTopPadding, Math.Max(1f, importRect.x - rect.x - LeftPadding - 8f), titleBand),
            HeaderTitle(preset),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding + 4f, rect.y + HeaderTopPadding + titleBand + HeaderInnerGap, Math.Max(1f, importRect.x - rect.x - LeftPadding - 16f), summaryBand),
            SummaryText(ctx, preset),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        if (UsKernelDraw.SelectionButton(importRect, Tr(ctx, ImportKey), ctx.Theme, selected: true, font: UiFont.Tiny))
        {
            ctx.Bindings.Invoke("import-baseline", preset.DefName);
        }

        Rect expandRect = new(rect.x, rect.y, Math.Max(1f, importRect.x - rect.x - 8f), rect.height);
        if (UiNative.Button(expandRect))
        {
            ctx.Bindings.Invoke("toggle-baseline-preset", preset.DefName);
        }
    }

    private void DrawRaceRow(Rect rect, string presetDefName, BaselineRaceView race, UiWidgetContext ctx)
    {
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/preset-list/tree");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y + 3f, Math.Max(1f, rect.width - 64f), Math.Max(18f, rect.height - 6f)),
            RaceLabel(ctx, race),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Checkbox(new Rect(rect.xMax - 40f, rect.y + 2f, 20f, 20f), ctx.Theme, race.Selected);

        if (UiNative.Button(rect))
        {
            ctx.Bindings.Invoke("toggle-baseline-race", new UsBaselineRaceToggle(presetDefName, race.RaceDefName, !race.Selected));
        }
    }

    private void DrawXenotypeRow(Rect rect, string presetDefName, string raceDefName, BaselineXenotypeView xenotype, UiWidgetContext ctx)
    {
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/preset-list/tree");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y + 4f, Math.Max(1f, rect.width - 64f), Math.Max(14f, rect.height - 6f)),
            XenotypeLabel(ctx, xenotype),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Checkbox(new Rect(rect.xMax - 40f, rect.y + 1f, 20f, 20f), ctx.Theme, xenotype.Selected);

        if (UiNative.Button(rect))
        {
            ctx.Bindings.Invoke(
                "toggle-baseline-xenotype",
                new UsBaselineXenoToggle(presetDefName, raceDefName, xenotype.XenotypeDefName, !xenotype.Selected));
        }
    }

    /// <summary>The preset header title as one outlet: the expander glyph plus the preset label.</summary>
    private static string HeaderTitle(BaselinePresetView preset)
    {
        return (preset.Expanded ? "− " : "+ ") + preset.Label;
    }

    private static string SummaryText(UiWidgetContext ctx, BaselinePresetView preset)
    {
        return string.Format(Tr(ctx, SelectionSummaryKey), preset.SelectedRaceCount, preset.SelectedXenotypeCount);
    }

    /// <summary>
    /// The two header bands and the row height, from the same widths the header draws into. The title is
    /// a Small line and the old 16px band was shorter than one such line, so the summary underneath was
    /// overdrawn whenever the title grew; the row now grows with both.
    /// </summary>
    private (float Title, float Summary, float Total) HeaderBands(
        UiWidgetContext ctx, BaselinePresetView preset, float innerWidth)
    {
        float importX = innerWidth - ImportButtonWidth - 8f;
        float titleWidth = Math.Max(1f, importX - LeftPadding - 8f);
        float summaryWidth = Math.Max(1f, importX - LeftPadding - 16f);
        string title = HeaderTitle(preset);
        string summary = SummaryText(ctx, preset);
        float titleBand = Math.Max(
            HeaderTitleMinHeight, ctx.Metrics.MeasureText(title, UiFont.Small, titleWidth));
        float summaryBand = Math.Max(
            SelectionSummaryHeight, ctx.Metrics.MeasureText(summary, UiFont.Tiny, summaryWidth));
        float total = Math.Max(
            PresetHeaderHeight,
            HeaderTopPadding + titleBand + HeaderInnerGap + summaryBand + HeaderBottomPadding);
        return (titleBand, summaryBand, total);
    }

    private static float MeasuredRowHeight(string text, float width, UiWidgetContext ctx, float fallback)
    {
        float measured = ctx.Metrics.MeasureText(text, UiFont.Tiny, width);
        return Math.Max(fallback, measured + 8f);
    }

    private static string Tr(UiWidgetContext ctx, string key)
    {
        return ctx.Translation.Translate(key);
    }

    private static string RaceLabel(UiWidgetContext ctx, BaselineRaceView race)
    {
        return string.Format(Tr(ctx, RaceSummaryKey), race.DisplayName, race.ActionCount, race.MoodCount);
    }

    private static string XenotypeLabel(UiWidgetContext ctx, BaselineXenotypeView xenotype)
    {
        string inheritTag = " " + Tr(ctx, xenotype.InheritFromRace ? XenotypeInheritsKey : XenotypeOwnOnlyKey);
        return string.Format(Tr(ctx, XenotypeSummaryKey), xenotype.DisplayName, inheritTag, xenotype.ActionCount, xenotype.MoodCount);
    }
}
