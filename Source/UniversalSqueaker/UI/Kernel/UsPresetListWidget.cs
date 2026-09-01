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
            bodyHeight += PresetHeaderHeight + RowGap;
            if (!preset.Expanded) continue;
            if (!string.IsNullOrEmpty(preset.Description))
            {
                bodyHeight += ctx.Metrics.MeasureText(preset.Description, UiFont.Tiny, Math.Max(1f, width - 16f)) + 6f + RowGap;
            }

            foreach (BaselineRaceView race in preset.Races)
            {
                bodyHeight += MeasuredRowHeight(RaceLabel(race), Math.Max(1f, width - 64f), ctx, RaceRowHeight) + RowGap;
                float xenoWidth = Math.Max(1f, width - xenoIndent);
                foreach (BaselineXenotypeView xenotype in race.Xenotypes)
                {
                    bodyHeight += MeasuredRowHeight(XenotypeLabel(xenotype), Math.Max(1f, xenoWidth - 64f), ctx, XenotypeRowHeight) + RowGap;
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
                "No baseline presets are installed. This feature reads presets provided by Defs.",
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
            DrawPresetHeader(new Rect(x, y, innerWidth, PresetHeaderHeight), preset, ctx);
            y += PresetHeaderHeight + RowGap;

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
                float raceRowHeight = MeasuredRowHeight(RaceLabel(race), Math.Max(1f, innerWidth - 64f), ctx, RaceRowHeight);
                DrawRaceRow(new Rect(x, y, innerWidth, raceRowHeight), preset.DefName, race, ctx);
                y += raceRowHeight + RowGap;

                float xenoWidth = Math.Max(1f, innerWidth - xenoIndent);
                foreach (BaselineXenotypeView xenotype in race.Xenotypes)
                {
                    float xenoRowHeight = MeasuredRowHeight(XenotypeLabel(xenotype), Math.Max(1f, xenoWidth - 64f), ctx, XenotypeRowHeight);
                    DrawXenotypeRow(new Rect(x + xenoIndent, y, xenoWidth, xenoRowHeight), preset.DefName, race.RaceDefName, xenotype, ctx);
                    y += xenoRowHeight + RowGap;
                }
            }
        }
    }

    private void DrawPresetHeader(Rect rect, BaselinePresetView preset, UiWidgetContext ctx)
    {
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, preset.Expanded);

        Rect importRect = new(rect.xMax - ImportButtonWidth - 8f, rect.y + (rect.height - ImportButtonHeight) / 2f, ImportButtonWidth, ImportButtonHeight);

        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y + 3f, Math.Max(1f, importRect.x - rect.x - LeftPadding - 8f), 16f),
            (preset.Expanded ? "− " : "+ ") + preset.Label,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding + 4f, rect.y + 20f, Math.Max(1f, importRect.x - rect.x - LeftPadding - 16f), 11f),
            preset.SelectedRaceCount + " races · " + preset.SelectedXenotypeCount + " xenotypes",
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        if (UsKernelDraw.SelectionButton(importRect, "Import", ctx.Theme, selected: true, font: UiFont.Tiny))
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
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y + 3f, Math.Max(1f, rect.width - 64f), Math.Max(18f, rect.height - 6f)),
            RaceLabel(race),
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
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y + 4f, Math.Max(1f, rect.width - 64f), Math.Max(14f, rect.height - 6f)),
            XenotypeLabel(xenotype),
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

    private static float MeasuredRowHeight(string text, float width, UiWidgetContext ctx, float fallback)
    {
        float measured = ctx.Metrics.MeasureText(text, UiFont.Tiny, width);
        return Math.Max(fallback, measured + 8f);
    }

    private static string RaceLabel(BaselineRaceView race)
    {
        return race.DisplayName + "  (" + race.ActionCount + " actions, " + race.MoodCount + " moods)";
    }

    private static string XenotypeLabel(BaselineXenotypeView xenotype)
    {
        string inheritTag = xenotype.InheritFromRace ? " (inherits race)" : " (own only)";
        return xenotype.DisplayName + inheritTag + "  (" + xenotype.ActionCount + " actions, " + xenotype.MoodCount + " moods)";
    }
}
