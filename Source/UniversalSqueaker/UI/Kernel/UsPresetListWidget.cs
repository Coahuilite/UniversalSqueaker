using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;

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

    // Row heights come from the theme's density axis (spec 1.4: 24 regular / 20 dense), not from a pair of
    // per-row constants that had drifted 2px apart.
    private const float RowGap = 2f;
    private const float XenotypeIndent = 18f;
    /// <summary>The indent step below the 900px view, where the card narrows it.</summary>
    private const float XenotypeIndentNarrow = 12f;
    private const float LeftPadding = 10f;
    private const float ImportButtonWidth = 76f;
    private const float ImportButtonHeight = 28f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;

    // Keyed UI text. English values in the Keyed table are verbatim copies of the former literals;
    // every string is resolved through Tr() so Measure and Draw share one outlet.
    private const string EmptyTextKey = "US.Preset.List.Empty";
    private const string BaselineNoteKey = "US.Preset.List.BaselineNote";
    private const string SelectionSummaryKey = "US.Preset.List.Selection";
    private const string ImportKey = "US.Preset.List.Import";
    private const string RaceSummaryKey = "US.Preset.Race.Summary";
    private const string XenotypeSummaryKey = "US.Preset.Xeno.Summary";
    private const string XenotypeInheritsKey = "US.Preset.Xeno.Inherits";
    private const string XenotypeOwnOnlyKey = "US.Preset.Xeno.Own";

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName, () => new UsPresetListWidget(), UsKernelWidgetRegistrar.SectionSchema));
    }

    public override void Validate(IUiBindingAccess bindings, string elementPath)
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
        if (presets.Count == 0) return TopPadding + EmptyHeight(ctx, BodyWidth(ctx)) + BottomPadding;

        float width = BodyWidth(ctx);
        float bodyHeight = TopPadding + BaselineNoteHeight(ctx, width) + RowGap;
        foreach (BaselinePresetView preset in presets)
        {
            bodyHeight += HeaderBands(ctx, preset, width).Total + RowGap;
            if (!preset.Expanded) continue;
            if (!string.IsNullOrEmpty(preset.Description))
            {
                bodyHeight += UsKernelDraw.Text(ctx, UiFont.Tiny).MeasureText(preset.Description, DescriptionWidth(width)) + 6f + RowGap;
            }

            foreach (BaselineRaceView race in preset.Races)
            {
                bodyHeight += ComposedRowHeight(ctx, preset.DefName + "|" + race.RaceDefName, 0, RaceLabel(ctx, race), race.Selected) + RowGap;
                foreach (BaselineXenotypeView xenotype in race.Xenotypes)
                {
                    bodyHeight += ComposedRowHeight(
                        ctx, preset.DefName + "|" + race.RaceDefName + "|" + xenotype.XenotypeDefName, 1, XenotypeLabel(ctx, xenotype),
                        xenotype.Selected, xenotype.Image) + RowGap;
                }
            }
        }

        return bodyHeight + BottomPadding;
    }

    /// <summary>
    /// One composed row's reserved height: the card's own text measurement, raised by the shared primitive to
    /// the picture's own height when the row carries an image - the same call Draw makes, so the height the
    /// card reserves and the height the band occupies cannot disagree.
    /// </summary>
    private float ComposedRowHeight(
        UiWidgetContext ctx, string key, int depth, string text, bool isChecked, UiImage image = default)
    {
        float textHeight = MeasuredRowHeight(
            text, Math.Max(1f, BodyWidth(ctx) - LeftPadding - 48f), ctx, UsKernelDraw.RowVisualHeight(ctx));
        return UiRowBand.Measure(BuildRow(key, depth, text, isChecked, image), ctx.Theme, textHeight);
    }

    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(UiLayoutRect rect, UiWidgetContext ctx)
    {
        IReadOnlyList<BaselinePresetView> presets = ctx.Bindings.TryGet("baseline-presets", out IReadOnlyList<BaselinePresetView> p)
            ? p
            : Array.Empty<BaselinePresetView>();
        if (presets.Count == 0)
        {
            UsKernelDraw.StyledLabel(ctx,
                new UiLayoutRect(rect.X, rect.Y + TopPadding, rect.Width, EmptyHeight(ctx, rect.Width)),
                Tr(ctx, EmptyTextKey), UiFont.Small, UiTextAnchor.MiddleCenter, emphasis: UiEmphasis.Muted);
            return;
        }

        float innerWidth = rect.Width;
        float x = rect.X;
        float y = rect.Y + TopPadding;

        float noteHeight = BaselineNoteHeight(ctx, innerWidth);
        UsKernelDraw.StyledLabel(ctx,
            new UiLayoutRect(x + LeftPadding, y, Math.Max(1f, innerWidth - LeftPadding * 2f), noteHeight),
            Tr(ctx, BaselineNoteKey), UiFont.Tiny, UiTextAnchor.UpperLeft, emphasis: UiEmphasis.Muted);
        y += noteHeight + RowGap;

        foreach (BaselinePresetView preset in presets)
        {
            (float headerTitle, float headerSummary, float headerTotal) = HeaderBands(ctx, preset, innerWidth);
            DrawPresetHeader(new UiLayoutRect(x, y, innerWidth, headerTotal), preset, headerTitle, headerSummary, ctx);
            y += headerTotal + RowGap;

            if (!preset.Expanded) continue;

            if (!string.IsNullOrEmpty(preset.Description))
            {
                float descHeight = UsKernelDraw.Text(ctx, UiFont.Tiny).MeasureText(preset.Description, DescriptionWidth(innerWidth)) + 6f;
                UsKernelDraw.StyledLabel(ctx,
                    new UiLayoutRect(x + LeftPadding, y + 2f, DescriptionWidth(innerWidth), descHeight - 4f),
                    preset.Description, UiFont.Tiny, UiTextAnchor.UpperLeft, emphasis: UiEmphasis.Muted);
                y += descHeight + RowGap;
            }

            foreach (BaselineRaceView race in preset.Races)
            {
                float raceRowHeight = ComposedRowHeight(ctx, preset.DefName + "|" + race.RaceDefName, 0, RaceLabel(ctx, race), race.Selected);
                DrawRaceRow(new UiLayoutRect(x, y, innerWidth, raceRowHeight), preset.DefName, race, ctx);
                y += raceRowHeight + RowGap;

                foreach (BaselineXenotypeView xenotype in race.Xenotypes)
                {
                    float xenoRowHeight = ComposedRowHeight(
                        ctx, preset.DefName + "|" + race.RaceDefName + "|" + xenotype.XenotypeDefName, 1, XenotypeLabel(ctx, xenotype),
                        xenotype.Selected, xenotype.Image);
                    DrawXenotypeRow(new UiLayoutRect(x, y, innerWidth, xenoRowHeight), preset.DefName, race.RaceDefName, xenotype, ctx);
                    y += xenoRowHeight + RowGap;
                }
            }
        }
    }

    private void DrawPresetHeader(
        UiLayoutRect rect, BaselinePresetView preset, float titleBand, float summaryBand, UiWidgetContext ctx)
    {
        // The header claims the tree entry first; the import button re-claims below, so hovering
        // the button shows "Import" and anywhere else on the header shows the tree.
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/preset-list/tree");
        UsKernelDraw.RowSurface(ctx.Drawing, rect, ctx.Theme, hovered, preset.Expanded ? UsKernelDraw.RowRail.Selected : UsKernelDraw.RowRail.None);

        float importHeight = UsKernelDraw.ControlHeight(ctx, ImportButtonHeight);
        UiLayoutRect importRect = new(rect.XMax - ImportButtonWidth - 8f, rect.Y + (rect.Height - importHeight) / 2f, ImportButtonWidth, importHeight);
        UsKernelDraw.HelpHover(importRect, ctx, "us/preset-list/import");

        UsKernelDraw.StyledLabel(ctx,
            new UiLayoutRect(rect.X + LeftPadding, rect.Y + HeaderTopPadding, Math.Max(1f, importRect.X - rect.X - LeftPadding - 8f), titleBand),
            HeaderTitle(preset), UiFont.Small, UiTextAnchor.MiddleLeft);
        UsKernelDraw.StyledLabel(ctx,
            new UiLayoutRect(rect.X + LeftPadding + 4f, rect.Y + HeaderTopPadding + titleBand + HeaderInnerGap, Math.Max(1f, importRect.X - rect.X - LeftPadding - 16f), summaryBand),
            SummaryText(ctx, preset), UiFont.Tiny, UiTextAnchor.MiddleLeft, emphasis: UiEmphasis.Muted);

        if (UsKernelDraw.StyledButton(importRect, ctx, Tr(ctx, ImportKey), selected: true, controlId: "import-baseline", itemKey: preset.DefName))
        {
            ctx.Bindings.Invoke("import-baseline", preset.DefName);
        }

        UiLayoutRect expandRect = new(rect.X, rect.Y, Math.Max(1f, importRect.X - rect.X - 8f), rect.Height);
        if (ctx.Controls.Activation(expandRect, ctx, "toggle-baseline-preset", "header", preset.DefName).Request)
        {
            ctx.Bindings.Invoke("toggle-baseline-preset", preset.DefName);
        }
    }

    private void DrawRaceRow(UiLayoutRect rect, string presetDefName, BaselineRaceView race, UiWidgetContext ctx)
    {
        // R4-B: the band is composed by the library's shared primitive - the same call `container/tree` makes.
        // The card keeps what is its own: the hover plane, the list rule, and its right-aligned checkbox
        // convention and indent step, both handed to the band as layout inputs.
        DrawComposedRow(
            rect,
            ctx,
            BuildRow(presetDefName + "|" + race.RaceDefName, depth: 0, RaceLabel(ctx, race), race.Selected),
            bindings => ToggleRace(presetDefName, race, bindings));
    }

    private void DrawXenotypeRow(UiLayoutRect rect, string presetDefName, string raceDefName, BaselineXenotypeView xenotype, UiWidgetContext ctx)
    {
        // The icon rides the row as a window-owned neutral handle; an empty handle uses the band's fallback.
        DrawComposedRow(
            rect,
            ctx,
            BuildRow(
                presetDefName + "|" + raceDefName + "|" + xenotype.XenotypeDefName,
                depth: 1,
                XenotypeLabel(ctx, xenotype),
                xenotype.Selected,
                xenotype.Image),
            bindings => ToggleXenotype(presetDefName, raceDefName, xenotype, bindings),
            UiEmphasis.Muted);
    }

    /// <summary>
    /// The row data the shared band composes. The key is the stable business key (never an index); the
    /// callbacks below close over the row's own values, so nothing is parsed back out of it.
    /// </summary>
    private static UiTreeRow BuildRow(string key, int depth, string text, bool isChecked, UiImage image = default)
    {
        return new UiTreeRow(
            key,
            depth: depth,
            text: text,
            checkable: true,
            isChecked: isChecked,
            image: image);
    }

    /// <summary>
    /// One composed band: the card's own hover plane and list rule, then the library's band. The indent step
    /// and the right-aligned box inset are the card's own conventions, so the composed row lands where the
    /// hand-drawn rows did, and either part (box or body) writes this row's own typed action.
    /// </summary>
    private static void DrawComposedRow(
        UiLayoutRect rect,
        UiWidgetContext ctx,
        UiTreeRow row,
        Action<IUiBindingAccess> toggle,
        UiEmphasis emphasis = UiEmphasis.Normal)
    {
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/preset-list/tree");
        UsKernelDraw.RowSurface(ctx.Drawing, rect, ctx.Theme, hovered, UsKernelDraw.RowRail.None);
        // The list convention the basic-tuning rows set: a single-line list row ends in one hairline in the
        // divider token, so a stack of rows reads as a list.
        UsKernelDraw.RowBottomLine(ctx.Drawing, rect, ctx.Theme);

        bool toggled = false;
        var actions = new UiRowBandActions(
            body: _ => toggled = true,
            checkbox: _ => toggled = true);
        UiRowBand.Draw(
            row,
            rect,
            ctx,
            UsKernelDraw.RowVisualHeight(ctx),
            ctx.Theme.Styles.Resolve(UiStatusTone.Neutral, emphasis),
            actions,
            new UiRowBandLayout(indentStep: RowIndent(ctx), checkboxRightInset: CheckboxRightInset,
                controlSize: UsKernelDraw.CheckboxVisual));
        if (toggled) toggle(ctx.Bindings);
    }

    /// <summary>The card's own indent step, which the band is told to use instead of the theme's spacing.</summary>
    /// <summary>The card's own indent step, which the band is told to use instead of the theme's spacing.</summary>
    private static float RowIndent(UiWidgetContext ctx)
    {
        return ctx.ViewWidth >= 900f ? XenotypeIndent : XenotypeIndentNarrow;
    }

    /// <summary>
    /// The checkbox's right-alignment inset for the composed band: the card's control-column convention, so
    /// the box lands at the right end of the row rather than after the indent.
    /// </summary>
    private static float CheckboxRightInset => UsKernelDraw.ControlColumnRightInset;

    private static void ToggleRace(string presetDefName, BaselineRaceView race, IUiBindingAccess bindings)
    {
        bindings.Invoke("toggle-baseline-race", new UsBaselineRaceToggle(presetDefName, race.RaceDefName, !race.Selected));
    }

    private static void ToggleXenotype(string presetDefName, string raceDefName, BaselineXenotypeView xenotype, IUiBindingAccess bindings)
    {
        bindings.Invoke(
            "toggle-baseline-xenotype",
            new UsBaselineXenoToggle(presetDefName, raceDefName, xenotype.XenotypeDefName, !xenotype.Selected));
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

    private static float BaselineNoteHeight(UiWidgetContext ctx, float width)
    {
        return UsKernelDraw.Text(ctx, UiFont.Tiny).MeasureText(Tr(ctx, BaselineNoteKey), Math.Max(1f, width - LeftPadding * 2f));
    }

    private static float DescriptionWidth(float width) => Math.Max(1, width - LeftPadding - 8);
    private static float EmptyHeight(UiWidgetContext ctx, float width)
        => Math.Max(48, UsKernelDraw.Text(ctx).MeasureText(Tr(ctx, EmptyTextKey), width));

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
            HeaderTitleMinHeight, UsKernelDraw.Text(ctx, UiFont.Small).MeasureText(title, titleWidth));
        float summaryBand = Math.Max(
            SelectionSummaryHeight, UsKernelDraw.Text(ctx, UiFont.Tiny).MeasureText(summary, summaryWidth));
        float total = Math.Max(
            PresetHeaderHeight,
            Math.Max(HeaderTopPadding + titleBand + HeaderInnerGap + summaryBand + HeaderBottomPadding,
                UsKernelDraw.ControlHeight(ctx, ImportButtonHeight) + HeaderTopPadding + HeaderBottomPadding));
        return (titleBand, summaryBand, total);
    }

    private static float MeasuredRowHeight(string text, float width, UiWidgetContext ctx, float fallback)
    {
        float measured = UsKernelDraw.Text(ctx, UiFont.Small).MeasureText(text, width);
        return Math.Max(fallback, measured + 8f);
    }

    private static string Tr(UiWidgetContext ctx, string key)
    {
        return ctx.Translation.Translate(key);
    }

    private static string RaceLabel(UiWidgetContext ctx, BaselineRaceView race)
    {
        return string.Format(Tr(ctx, RaceSummaryKey), race.DisplayName, race.ActionCount, race.FieldCount);
    }

    private static string XenotypeLabel(UiWidgetContext ctx, BaselineXenotypeView xenotype)
    {
        string inheritTag = " " + Tr(ctx, xenotype.InheritFromRace ? XenotypeInheritsKey : XenotypeOwnOnlyKey);
        return string.Format(Tr(ctx, XenotypeSummaryKey), xenotype.DisplayName, inheritTag, xenotype.ActionCount, xenotype.FieldCount);
    }
}
