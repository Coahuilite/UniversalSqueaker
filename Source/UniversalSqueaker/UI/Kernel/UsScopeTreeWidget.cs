using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US S5 tuning editor. One composite manages the built-in action scope tree and the
/// four mood rows for the current layer:
///  - layer segment (Global/Race/Xenotype) writes the typed "set-tuning-layer" int action;
///  - domain picker (Race/Xenotype layers only) is a session-popup dropdown writing
///    "set-tuning-domain";
///  - scope rows cycle [Auto, Off, Any, Command] filtered by each action's supported scopes,
///    writing "set-action-scope";
///  - each mood is one card: a header with the mood's US.Mood.* name and two reset controls -
///    "reset to default" ("set-mood-tuning" with Clear) and "reset to preset" ("reset-mood-to-preset") -
///    followed by the same two-line parameter block for Pitch, Volume and Jitter (localized label, minus,
///    numeric field and plus on the first line, the slider on the second). An unavailable control stays
///    drawn and greyed, and hovering it explains why in the help panel.
/// All values come from typed read bindings; every write is a typed action.
/// </summary>
public sealed class UsScopeTreeWidget : UsSectionWidgetBase
{
    public const string KindName = "us/scope-tree";

    private const float LayerRowHeight = 30f;
    private const float DomainRowHeight = 28f;
    private const float LayerRowLabelBandHeight = 22f;
    private const float RowHeight = 28f;

    // The inherited-scope hint is a Tiny line: its band has to hold a full Tiny line (a 14px band cut
    // the tail of every scope name, in any language).
    private const float InheritedHintHeight = 18f;

    // A mood card is a repeated two-line parameter template: the mood name and its two reset controls on
    // the header line, then three parameter blocks - label, minus, numeric field and plus on the first
    // line, the slider on the second, aligned to the numeric group's left edge. Every block draws every
    // control in every width regime: the only width-dependent choices move a control onto its own line,
    // they never drop one.
    private const float MoodControlHeight = 22f;
    private const float MoodControlButtonWidth = 20f;
    private const float MoodControlButtonMinWidth = 6f;
    private const float MoodControlFieldWidth = 40f;
    private const float MoodControlFieldMinWidth = 24f;
    private const float MoodControlGap = 4f;
    private const float MoodSliderHeight = 16f;
    private const float MoodSliderGap = 3f;
    private const float MoodBlockGap = 8f;
    // The parameter label column is measured from the resolved localized labels through the metrics seam
    // (never the old fixed 14px band, which clipped Pitch / 音高), plus this padding so a rounding
    // difference between the measuring pass and the drawing pass cannot wrap what the column was sized
    // for. Measure and Draw both come through MoodRowsLayoutFor below.
    private const float MoodLabelColumnGap = 6f;
    private const float MoodLabelColumnPad = 8f;
    private const float MoodParameterLabelMinBand = 16f;
    private const float MoodNameMinBand = 18f;
    private const float MoodHeaderGap = 6f;
    private const float RowGap = 2f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;
    private const float LeftPadding = 10f;
    private const float ButtonWidth = 96f;
    private const float ButtonHeight = 24f;
    // Floor for each of the mood row's two reset controls. Every drawn width is measured from its own
    // label (MoodResetWidthFor): the ruled phrases are verb phrases ("重置为默认" / "Reset to default"),
    // and a fixed box wrapped the earlier single control in English - the bilingual fit sweep caught it
    // as "needs 54px, has 24px at width 40px". Two controls, two measurements, same floor.
    private const float MoodResetWidthMin = 52f;
    private const float MoodGap = 6f;

    // Keyed display text. Every bound value stays untouched: the tuning layer is the "tuning-layer"
    // int index, scope options bind SqueakActionScope.ToString(), the domain dropdown binds the
    // (race, xeno) pair and the stepper element ids are built from enums. Only the text drawn to the
    // player goes through these keys, resolved at the site that owns the Label/SelectionButton call.
    private const string LayerLabelKey = "US.Tuning.Layer";
    private const string DomainLabelKey = "US.Tuning.Domain";
    private const string ActionScopeHeaderKey = "US.Tuning.ActionScope";
    private const string GroupAutonomousKey = "US.Tuning.Group.Autonomous";
    private const string GroupOperableKey = "US.Tuning.Group.Operable";
    private const string MoodTuningHeaderKey = "US.Tuning.MoodTuning";
    /// <summary>Action-scope dropdown option: "inherit from below". Per the term split this word belongs to
    /// the action side only; the mood row's controls use the two "reset" phrases below instead
    /// (one word, two meanings was the direct cause of mis-clicks).</summary>
    private const string AutoLabelKey = "US.Tuning.Auto";
    /// <summary>Mood row, action one - "reset to default": clear this layer's three factor fields and keep
    /// the preset source, so the value falls through to inheritance again. A CLEAR.</summary>
    private const string ResetDefaultKey = "US.Tuning.ResetToDefault";
    /// <summary>Mood row, action two - "reset to preset": re-apply the baseline of the row's source preset.
    /// A WRITE, not a clear; the two actions are deliberately different classes.</summary>
    private const string ResetPresetKey = "US.Tuning.ResetToPreset";
    // Help entries claimed while hovering the two controls: enabled explains what the control does,
    // disabled explains why it is unavailable. The help panel is this UI's reason channel; the control
    // itself stays drawn and only greys out (unavailable is not invisible).
    private const string ResetDefaultHelpKey = "us/scope-tree/mood-reset-default";
    private const string ResetPresetHelpKey = "us/scope-tree/mood-reset-preset";
    // Item keys stay at three path segments (us/<section>/<item>): the UI-logic lane's dead-content scan
    // reads exactly three-segment literals as claims, and a four-segment key would be invisible to it.
    private const string ResetDefaultNoLocalHelpKey = "us/scope-tree/mood-reset-no-local";
    private const string ResetPresetNotFromPresetHelpKey = "us/scope-tree/mood-reset-not-from-preset";
    private const string ResetPresetMissingHelpKey = "us/scope-tree/mood-reset-preset-missing";
    private const string ResetPresetNoEntryHelpKey = "us/scope-tree/mood-reset-preset-no-entry";
    private const string PitchLabelKey = "US.Tuning.Factor.Pitch";
    private const string VolumeLabelKey = "US.Tuning.Factor.Volume";
    private const string JitterLabelKey = "US.Tuning.Factor.Jitter";

    /// <summary>
    /// Display-text keys of the three tuning layers, index-aligned with the "tuning-layer" int binding.
    /// The length doubles as the layer count for the stacked-row height, so text and geometry share one
    /// source and Measure can never size a different number of buttons than Draw makes.
    /// </summary>
    private static readonly string[] LayerKeys = { "US.Tuning.Layer.Global", "US.Tuning.Layer.Race", "US.Tuning.Layer.Xenotype" };

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsScopeTreeWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<int>("tuning-layer", elementPath);
        bindings.ValidateValue<string>("tuning-race", elementPath);
        bindings.ValidateValue<string>("tuning-xeno", elementPath);
        bindings.ValidateValue<IReadOnlyList<TuningDomainOptionView>>("tuning-domains", elementPath);
        bindings.ValidateValue<IReadOnlyList<ActionScopeRowView>>("action-scopes", elementPath);
        bindings.ValidateValue<IReadOnlyList<MoodTuningRowView>>("mood-rows", elementPath);
        bindings.ValidateAction<int>("set-tuning-layer", elementPath);
        bindings.ValidateAction<UsTuningDomainSelection>("set-tuning-domain", elementPath);
        bindings.ValidateAction<UsScopeWrite>("set-action-scope", elementPath);
        bindings.ValidateAction<UsMoodWrite>("set-mood-tuning", elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return LayerRowHeight + RowHeight * 6f + TopPadding + BottomPadding;
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        int layer = ctx.Bindings.TryGet("tuning-layer", out int l) ? l : 0;
        IReadOnlyList<ActionScopeRowView> scopeRows = ctx.Bindings.TryGet("action-scopes", out IReadOnlyList<ActionScopeRowView> scopes)
            ? scopes
            : Array.Empty<ActionScopeRowView>();
        IReadOnlyList<MoodTuningRowView> moodRows = ctx.Bindings.TryGet("mood-rows", out IReadOnlyList<MoodTuningRowView> moods)
            ? moods
            : Array.Empty<MoodTuningRowView>();

        float width = BodyWidth(ctx);
        float bodyHeight = TopPadding + LayerRowHeightFor(width) + RowGap;
        if (layer > 0) bodyHeight += DomainRowHeight + RowGap;
        bodyHeight += RowHeight + RowGap; // "Action Scope" header
        if (scopeRows.Count > 0)
        {
            for (int group = 0; group < 2; group++)
            {
                ActionScopeGroup targetGroup = group == 0 ? ActionScopeGroup.Autonomous : ActionScopeGroup.Operable;
                bool hasGroupRows = false;
                foreach (ActionScopeRowView row in scopeRows)
                {
                    if (row.Group == targetGroup)
                    {
                        hasGroupRows = true;
                        break;
                    }
                }

                if (!hasGroupRows) continue;

                bodyHeight += RowHeight + RowGap; // group header
                foreach (ActionScopeRowView row in scopeRows)
                {
                    if (row.Group == targetGroup)
                    {
                        bodyHeight += ScopeRowHeightFor(width, row.DisplayName, ctx) + RowGap;
                    }
                }
            }

            bodyHeight += RowHeight + RowGap; // "Mood Tuning" header
            MoodRowsLayout moodLayout = MoodRowsLayoutFor(width, ctx, moodRows);
            bodyHeight += moodRows.Count * (moodLayout.TotalHeight + RowGap);
        }

        return bodyHeight + BottomPadding;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        int layer = ctx.Bindings.TryGet("tuning-layer", out int l) ? l : 0;
        IReadOnlyList<ActionScopeRowView> scopeRows = ctx.Bindings.TryGet("action-scopes", out IReadOnlyList<ActionScopeRowView> scopes)
            ? scopes
            : Array.Empty<ActionScopeRowView>();
        IReadOnlyList<MoodTuningRowView> moodRows = ctx.Bindings.TryGet("mood-rows", out IReadOnlyList<MoodTuningRowView> moods)
            ? moods
            : Array.Empty<MoodTuningRowView>();

        float innerWidth = rect.width;
        float x = rect.x;
        float y = rect.y + TopPadding;

        float layerRowHeight = LayerRowHeightFor(innerWidth);
        DrawLayerRow(new Rect(x, y, innerWidth, layerRowHeight), layer, ctx);
        y += layerRowHeight + RowGap;

        if (layer > 0)
        {
            DrawDomainRow(new Rect(x, y, innerWidth, DomainRowHeight), ctx);
            y += DomainRowHeight + RowGap;
        }

        UsKernelDraw.Label(
            new Rect(x, y, innerWidth, RowHeight),
            UsKernelDraw.Keyed(ctx, ActionScopeHeaderKey),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        y += RowHeight + RowGap;

        bool anyScopeDrawn = false;
        for (int group = 0; group < 2; group++)
        {
            ActionScopeGroup targetGroup = group == 0 ? ActionScopeGroup.Autonomous : ActionScopeGroup.Operable;
            bool hasGroupRows = false;
            foreach (ActionScopeRowView row in scopeRows)
            {
                if (row.Group == targetGroup)
                {
                    hasGroupRows = true;
                    break;
                }
            }

            if (!hasGroupRows) continue;

            UsKernelDraw.Label(
                new Rect(x, y, innerWidth, RowHeight),
                UsKernelDraw.Keyed(ctx, targetGroup == ActionScopeGroup.Operable ? GroupOperableKey : GroupAutonomousKey),
                ctx.Theme,
                ctx.Theme.TextSecondary,
                UiFont.Tiny,
                TextAnchor.MiddleLeft);
            y += RowHeight + RowGap;

            foreach (ActionScopeRowView row in scopeRows)
            {
                if (row.Group != targetGroup) continue;
                float rowHeight = ScopeRowHeightFor(innerWidth, row.DisplayName, ctx);
                DrawScopeRow(new Rect(x, y, innerWidth, rowHeight), row, ctx);
                y += rowHeight + RowGap;
            }

            anyScopeDrawn = true;
        }

        if (anyScopeDrawn)
        {
            UsKernelDraw.Label(
                new Rect(x, y, innerWidth, RowHeight),
                UsKernelDraw.Keyed(ctx, MoodTuningHeaderKey),
                ctx.Theme,
                ctx.Theme.TextPrimary,
                UiFont.Small,
                TextAnchor.MiddleLeft);
            y += RowHeight + RowGap;

            MoodRowsLayout moodLayout = MoodRowsLayoutFor(innerWidth, ctx, moodRows);
            foreach (MoodTuningRowView mood in moodRows)
            {
                DrawMoodRow(new Rect(x, y, innerWidth, moodLayout.TotalHeight), mood, moodLayout, ctx);
                y += moodLayout.TotalHeight + RowGap;
            }
        }
    }

    private void DrawLayerRow(Rect rect, int layer, UiWidgetContext ctx)
    {
        UsKernelDraw.HelpHover(rect, ctx, "us/scope-tree/layer");
        if (UsesStackedLayerButtons(rect.width))
        {
            UsKernelDraw.Label(
                new Rect(rect.x + LeftPadding, rect.y, 120f, LayerRowLabelBandHeight),
                UsKernelDraw.Keyed(ctx, LayerLabelKey),
                ctx.Theme,
                ctx.Theme.TextPrimary,
                UiFont.Small,
                TextAnchor.MiddleLeft);

            float stackedButtonWidth = Math.Max(1f, (rect.width - LeftPadding * 2f - RowGap * 2f) / 3f);
            float y = rect.y + LayerRowLabelBandHeight;
            for (int i = 0; i < LayerKeys.Length; i++)
            {
                Rect buttonRect = new(rect.x + LeftPadding, y, stackedButtonWidth, ButtonHeight);
                if (UsKernelDraw.SelectionButton(buttonRect, ctx, UsKernelDraw.Keyed(ctx, LayerKeys[i]), ctx.Theme, layer == i, font: UiFont.Tiny))
                {
                    ctx.Bindings.Invoke("set-tuning-layer", i);
                }

                y += ButtonHeight + RowGap;
            }

            return;
        }

        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y, 120f, rect.height),
            UsKernelDraw.Keyed(ctx, LayerLabelKey),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);

        float available = rect.width - LeftPadding * 2f - 120f - RowGap * 2f;
        float buttonWidth = Math.Max(1f, (available - RowGap * 2f) / 3f);
        float buttonX = rect.x + rect.width - LeftPadding - buttonWidth * 3f - RowGap * 2f;
        for (int i = 0; i < LayerKeys.Length; i++)
        {
            Rect buttonRect = new(buttonX, rect.y + (rect.height - ButtonHeight) / 2f, buttonWidth, ButtonHeight);
            if (UsKernelDraw.SelectionButton(buttonRect, ctx, UsKernelDraw.Keyed(ctx, LayerKeys[i]), ctx.Theme, layer == i, font: UiFont.Tiny))
            {
                ctx.Bindings.Invoke("set-tuning-layer", i);
            }

            buttonX += buttonWidth + RowGap;
        }
    }

    private void DrawDomainRow(Rect rect, UiWidgetContext ctx)
    {
        string race = ctx.Bindings.TryGet("tuning-race", out string r) ? r : "";
        string xeno = ctx.Bindings.TryGet("tuning-xeno", out string x) ? x : "";
        IReadOnlyList<TuningDomainOptionView> domains = ctx.Bindings.TryGet("tuning-domains", out IReadOnlyList<TuningDomainOptionView> d)
            ? d
            : Array.Empty<TuningDomainOptionView>();

        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/scope-tree/domain");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, UsKernelDraw.RowRail.None);
        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y, 120f, rect.height),
            UsKernelDraw.Keyed(ctx, DomainLabelKey),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);

        float dropdownWidth = Math.Min(ButtonWidth, Math.Max(40f, rect.width - 160f));
        Rect dropdownRect = new(rect.xMax - dropdownWidth - 8f, rect.y + (rect.height - ButtonHeight) / 2f, dropdownWidth, ButtonHeight);

        var options = new List<KeyValuePair<string, string>>();
        string current = "";
        foreach (TuningDomainOptionView domain in domains)
        {
            string value = domain.RaceDefName + "\u0001" + domain.TargetDefName;
            options.Add(new KeyValuePair<string, string>(domain.DisplayName, value));
            if (string.Equals(domain.RaceDefName, race, StringComparison.Ordinal)
                && string.Equals(domain.TargetDefName, xeno, StringComparison.Ordinal))
            {
                current = value;
            }
        }

        UsKernelDraw.Dropdown(dropdownRect, "scope-tree-domain", ctx, current, options, selected =>
        {
            string[] parts = selected.Split(new[] { '\u0001' }, StringSplitOptions.None);
            if (parts.Length != 2) return;
            ctx.Bindings.Invoke("set-tuning-domain", new UsTuningDomainSelection(parts[0], parts[1]));
        });
    }

    private void DrawScopeRow(Rect rect, ActionScopeRowView row, UiWidgetContext ctx)
    {
        // A trigger that currently reads "Auto" claims the Auto/Clear entry instead of the generic
        // action-scope one, so the panel explains what the visible value means.
        bool hovered = UsKernelDraw.HelpHover(
            rect, ctx, row.HasOwnScope ? "us/scope-tree/action-scope" : "us/scope-tree/auto");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, UsKernelDraw.RowRail.None);

        string displayName = UsKernelDraw.Keyed(ctx, DefinitionFor(row.Action).DisplayKey);
        float scopeButtonWidth = Math.Min(ButtonWidth, Math.Max(40f, rect.width - 120f));

        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y, Math.Max(1f, rect.width - LeftPadding - scopeButtonWidth - 90f), rect.height),
            displayName,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);

        if (ctx.ViewWidth >= 480f && (!row.HasOwnScope || row.Scope != row.EffectiveScope))
        {
            UsKernelDraw.Label(
                new Rect(rect.x + rect.width - scopeButtonWidth - 96f, rect.y + 6f, Math.Max(1f, 86f), InheritedHintHeight),
                "→ " + UsKernelDraw.Keyed(ctx, ScopeLabelKey(row.EffectiveScope)),
                ctx.Theme,
                ctx.Theme.TextSecondary,
                UiFont.Tiny,
                TextAnchor.MiddleLeft);
        }

        var options = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(UsKernelDraw.Keyed(ctx, AutoLabelKey), "")
        };
        foreach (SqueakActionScope scope in SupportedStates(row.Action))
        {
            options.Add(new KeyValuePair<string, string>(UsKernelDraw.Keyed(ctx, ScopeLabelKey(scope)), scope.ToString()));
        }

        string current = row.HasOwnScope ? row.Scope.ToString() : "";
        Rect dropdownRect = new(rect.xMax - scopeButtonWidth - 8f, rect.y + (rect.height - ButtonHeight) / 2f, scopeButtonWidth, ButtonHeight);
        string elementId = "scope-tree-scope-" + row.ActionKey;
        UsKernelDraw.Dropdown(dropdownRect, elementId, ctx, current, options, selected =>
        {
            SqueakActionScope? scope = null;
            if (selected.Length > 0 && Enum.TryParse(selected, true, out SqueakActionScope parsed))
            {
                scope = parsed;
            }

            ctx.Bindings.Invoke("set-action-scope", new UsScopeWrite(row.ActionKey, scope));
        });
    }

    /// <summary>
    /// One mood card. The header line carries the mood name (resolved through its US.Mood.* key, so no
    /// language literal lives in this file) and the two reset controls; below it every parameter gets the
    /// same two-line block: label, minus, numeric field and plus on the first line, the slider on the
    /// second. The geometry comes from <see cref="MoodRowsLayoutFor"/> - the same function Measure uses -
    /// so a hover never changes a height and the section card can never clip a control.
    /// </summary>
    private void DrawMoodRow(Rect rect, MoodTuningRowView row, MoodRowsLayout layout, UiWidgetContext ctx)
    {
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/scope-tree/mood-tuning");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, UsKernelDraw.RowRail.None);

        float pitch = row.Own?.hasPitchFactor == true ? row.Own.pitchFactor : row.EffectivePitch;
        float volume = row.Own?.hasVolumeFactor == true ? row.Own.volumeFactor : row.EffectiveVolume;
        float jitter = row.Own?.hasPitchJitter == true ? Math.Max(0f, row.Own.pitchJitter.max - 1f) : row.EffectiveJitterHalf;

        // Header. When the name and the measured reset block cannot share the line, the controls drop to
        // their own line - they are never squeezed away, and an unavailable control stays drawn and inert
        // while still claiming the help entry that carries its reason.
        float nameY = layout.HeaderInline
            ? rect.y + (layout.HeaderHeight - layout.NameBandHeight) * 0.5f
            : rect.y;
        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, nameY, layout.NameBandWidth, layout.NameBandHeight),
            MoodName(ctx, row),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);

        if (layout.HeaderInline)
        {
            DrawMoodResetButtons(
                new Rect(
                    rect.x + LeftPadding + layout.ParameterWidth - layout.ResetWidth,
                    rect.y + (layout.HeaderHeight - layout.ResetHeight) * 0.5f,
                    layout.ResetWidth,
                    layout.ResetHeight),
                row,
                ctx);
        }
        else
        {
            DrawMoodResetButtons(
                new Rect(rect.x + LeftPadding, rect.y + layout.NameBandHeight + RowGap, layout.ParameterWidth, layout.ResetHeight),
                row,
                ctx);
        }

        float parameterX = rect.x + LeftPadding;
        for (int i = 0; i < MoodParameterFactors.Length; i++)
        {
            SqueakMoodFactor factor = MoodParameterFactors[i];
            float value = i == 0 ? pitch : i == 1 ? volume : jitter;

            float blockY = rect.y + layout.ParameterTop + i * (layout.BlockHeight + MoodBlockGap);
            Rect labelRect;
            Rect groupRect;
            if (layout.LabelOnOwnLine)
            {
                // Narrow card: the label keeps its own full-width line and the numeric line follows it, so
                // both stay readable instead of one being clipped to make room for the other.
                labelRect = new Rect(parameterX, blockY, layout.ParameterWidth, layout.LabelBandHeight);
                groupRect = new Rect(parameterX, blockY + layout.LabelBandHeight, layout.ControlGroupWidth, layout.ControlLineHeight);
            }
            else
            {
                labelRect = new Rect(
                    parameterX,
                    blockY + (layout.ControlLineHeight - layout.LabelBandHeight) * 0.5f,
                    layout.LabelColumnWidth,
                    layout.LabelBandHeight);
                groupRect = new Rect(
                    parameterX + layout.LabelColumnWidth + MoodLabelColumnGap,
                    blockY,
                    layout.ControlGroupWidth,
                    layout.ControlLineHeight);
            }

            DrawMoodStepper(
                labelRect,
                groupRect,
                layout.ControlButtonWidth,
                layout.ControlFieldWidth,
                new Rect(rect.x + layout.SliderX, blockY + layout.SliderY, layout.SliderWidth, MoodSliderHeight),
                ParameterLabelKey(factor),
                value,
                MoodParameterMin[i],
                MoodParameterMax[i],
                row,
                factor,
                ctx);
        }
    }

    /// <summary>Mood display name through the Host translation seam. Keyed (US.Mood.Good and friends), so
    /// a mood is never rendered from a hard-coded language literal or from a debug name.</summary>
    private static string MoodName(UiWidgetContext ctx, MoodTuningRowView row)
    {
        return UsKernelDraw.Keyed(ctx, "US.Mood." + row.Mood);
    }

    /// <summary>Display-text key of one factor, index-aligned with <see cref="MoodParameterFactors"/>.</summary>
    private static string ParameterLabelKey(SqueakMoodFactor factor)
    {
        return factor switch
        {
            SqueakMoodFactor.Volume => VolumeLabelKey,
            SqueakMoodFactor.Jitter => JitterLabelKey,
            _ => PitchLabelKey,
        };
    }

    /// <summary>
    /// Width of one mood reset control: its own label's measured single-line width plus the control's
    /// 6px side padding, floored at <see cref="MoodResetWidthMin"/>. Measure and Draw both come through
    /// here (the mode decision depends on it), so a longer ruled phrase in either language widens the
    /// control instead of wrapping it into a clipped band.
    /// </summary>
    private static float MoodResetWidthFor(UiWidgetContext ctx, string labelKey)
    {
        float label = ctx.Metrics.MeasureWidth(UsKernelDraw.Keyed(ctx, labelKey), UiFont.Tiny);
        return Math.Max(MoodResetWidthMin, label + 12f);
    }

    private static float MoodResetClusterWidthFor(UiWidgetContext ctx)
    {
        return MoodResetWidthFor(ctx, ResetDefaultKey) + MoodGap + MoodResetWidthFor(ctx, ResetPresetKey);
    }

    private void DrawMoodResetButtons(Rect rect, MoodTuningRowView row, UiWidgetContext ctx)
    {
        float defaultWidth = MoodResetWidthFor(ctx, ResetDefaultKey);
        float presetWidth = MoodResetWidthFor(ctx, ResetPresetKey);
        // Side by side when both labels fit; otherwise each control gets its own line (never squeezed,
        // never shrunk - the ruling). Order stays default-then-preset in both shapes.
        bool sideBySide = rect.width >= defaultWidth + MoodGap + presetWidth - 0.5f;
        float lineWidth = Math.Max(1f, rect.width);
        Rect defaultRect = new(rect.x, rect.y, Math.Min(defaultWidth, lineWidth), ButtonHeight);
        Rect presetRect = sideBySide
            ? new(rect.xMax - presetWidth, rect.y, presetWidth, ButtonHeight)
            : new(rect.x, rect.y + ButtonHeight + RowGap, Math.Min(presetWidth, lineWidth), ButtonHeight);

        DrawMoodResetButton(
            defaultRect,
            ResetDefaultKey,
            ResetDefaultHelpKey,
            ResetDefaultUnavailableHelpKey(row),
            row.DefaultReset == SqueakMoodResetDefaultState.Ready,
            () => ctx.Bindings.Invoke("set-mood-tuning", new UsMoodWrite(row.Mood, SqueakMoodFactor.Clear, null)),
            ctx);

        DrawMoodResetButton(
            presetRect,
            ResetPresetKey,
            ResetPresetHelpKey,
            ResetPresetUnavailableHelpKey(row),
            row.PresetReset == SqueakMoodResetPresetState.Ready,
            () => ctx.Bindings.Invoke("reset-mood-to-preset", new UsMoodPresetReset(row.Mood)),
            ctx);
    }

    /// <summary>
    /// One reset control, neutral by design: "reset to default" restores inheritance, which is not a
    /// destructive action and therefore carries no danger semantics (07 §7 bans red for it). Unavailable
    /// means greyed out and inert, never invisible: the control is still drawn and hovering it claims the
    /// help entry that carries its reason sentence (the reason channel of this UI). Available controls
    /// claim the entry that explains the action they perform.
    /// </summary>
    private static void DrawMoodResetButton(
        Rect rect,
        string labelKey,
        string enabledHelpKey,
        string unavailableHelpKey,
        bool enabled,
        Action invoke,
        UiWidgetContext ctx)
    {
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, enabled ? enabledHelpKey : unavailableHelpKey);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered && enabled, UsKernelDraw.RowRail.None);
        UsKernelDraw.Label(
            new Rect(rect.x + 6f, rect.y, Mathf.Max(1f, rect.width - 12f), rect.height),
            UsKernelDraw.Keyed(ctx, labelKey),
            ctx.Theme,
            enabled ? ctx.Theme.TextPrimary : ctx.Theme.TextDisabled,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        // The disabled control still claims its rect (it is drawn and hit-testable - unavailable is not
        // invisible); only the action is suppressed. Not routing the click keeps it out of every binding.
        bool clicked = UiNative.Button(rect, ctx);
        if (enabled && clicked) invoke();
    }

    private static string ResetDefaultUnavailableHelpKey(MoodTuningRowView row)
    {
        return row.DefaultReset == SqueakMoodResetDefaultState.Ready ? ResetDefaultHelpKey : ResetDefaultNoLocalHelpKey;
    }

    private static string ResetPresetUnavailableHelpKey(MoodTuningRowView row)
    {
        return row.PresetReset switch
        {
            SqueakMoodResetPresetState.Ready => ResetPresetHelpKey,
            SqueakMoodResetPresetState.NotFromPreset => ResetPresetNotFromPresetHelpKey,
            SqueakMoodResetPresetState.PresetMissing => ResetPresetMissingHelpKey,
            _ => ResetPresetNoEntryHelpKey,
        };
    }

    /// <summary>
    /// One parameter block: the label band the layout sized from the resolved localized text, then the
    /// numeric line - minus, numeric field, plus - then the slider on its own line starting at the numeric
    /// group's left edge. Every width regime reaches this method, so no regime can omit a control; a group
    /// narrower than the standard control widths shrinks (field first, buttons second) rather than spilling
    /// out of the card. Writes are unchanged: the typed "set-mood-tuning" action, the shared
    /// "mood-&lt;Mood&gt;-&lt;Factor&gt;" element id, 0.05 steps and the 0.### format.
    /// </summary>
    private void DrawMoodStepper(
        Rect labelRect,
        Rect groupRect,
        float buttonWidth,
        float fieldWidth,
        Rect sliderRect,
        string labelKey,
        float value,
        float min,
        float max,
        MoodTuningRowView row,
        SqueakMoodFactor factor,
        UiWidgetContext ctx)
    {
        UsKernelDraw.Label(
            labelRect,
            UsKernelDraw.Keyed(ctx, labelKey),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        string elementId = "mood-" + row.Mood + "-" + factor;

        Rect minusRect = new(groupRect.x, groupRect.y, buttonWidth, groupRect.height);
        Rect fieldRect = new(minusRect.xMax + MoodControlGap, groupRect.y, fieldWidth, groupRect.height);
        Rect plusRect = new(fieldRect.xMax + MoodControlGap, groupRect.y, buttonWidth, groupRect.height);

        bool minusClicked = UsKernelDraw.SelectionButton(minusRect, ctx, "−", ctx.Theme, selected: false);
        bool plusClicked = UsKernelDraw.SelectionButton(plusRect, ctx, "+", ctx.Theme, selected: false);

        float sliderValue = UiNative.Slider(sliderRect, elementId, ctx.Session, value, min, max, out bool sliderChanged);
        if (sliderChanged)
        {
            ctx.Bindings.Invoke("set-mood-tuning", new UsMoodWrite(row.Mood, factor, sliderValue));
        }

        UiNative.NumberField(fieldRect, elementId, ctx.Session, value, min, max, "0.###", out bool committed);
        if (committed)
        {
            float fieldValue = ctx.Session.GetOrCreateValueState(elementId).FloatValue;
            ctx.Bindings.Invoke("set-mood-tuning", new UsMoodWrite(row.Mood, factor, fieldValue));
        }

        if (minusClicked)
        {
            ctx.Bindings.Invoke("set-mood-tuning", new UsMoodWrite(row.Mood, factor, UiNative.ClampValue(value - 0.05f, min, max)));
        }

        if (plusClicked)
        {
            ctx.Bindings.Invoke("set-mood-tuning", new UsMoodWrite(row.Mood, factor, UiNative.ClampValue(value + 0.05f, min, max)));
        }
    }

    /// <summary>
    /// True when the layer buttons cannot fit beside the "Tuning layer" label and are stacked
    /// below it. Must be identical in Measure and Draw; both feed the card body width
    /// (see <see cref="UsSectionWidgetBase.BodyWidth"/>).
    /// </summary>
    private static bool UsesStackedLayerButtons(float bodyWidth)
    {
        float available = bodyWidth - LeftPadding * 2f - 120f - RowGap * 2f;
        float buttonWidth = Math.Max(1f, (available - RowGap * 2f) / 3f);
        return buttonWidth < 56f;
    }

    /// <summary>
    /// Height the layer row actually consumes: the single-line <see cref="LayerRowHeight"/> on
    /// wide layouts, or the stacked-button stack on narrow ones. Measure and Draw must agree so
    /// the "Action Scope" header never overlaps the stacked buttons.
    /// </summary>
    private static float LayerRowHeightFor(float bodyWidth)
    {
        return UsesStackedLayerButtons(bodyWidth)
            ? LayerRowLabelBandHeight + LayerKeys.Length * ButtonHeight + (LayerKeys.Length - 1) * RowGap
            : LayerRowHeight;
    }

    /// <summary>The three factor parameters in draw order. The arrays below are index-aligned with it, so
    /// the label, the range and the element id of one parameter can never drift apart.</summary>
    private static readonly SqueakMoodFactor[] MoodParameterFactors =
    {
        SqueakMoodFactor.Pitch,
        SqueakMoodFactor.Volume,
        SqueakMoodFactor.Jitter,
    };

    /// <summary>Factor ranges, unchanged: Pitch 0.5-2, Volume 0.1-2, Jitter 0-0.5.</summary>
    private static readonly float[] MoodParameterMin = { 0.5f, 0.1f, 0f };

    private static readonly float[] MoodParameterMax = { 2f, 2f, 0.5f };

    /// <summary>
    /// The one geometry function Measure and Draw share for the whole Mood Modulation section: the card
    /// header (mood name + measured reset block) and the repeated two-line parameter block. Everything is
    /// derived from the card body width, the metrics/resolver seam and the rows themselves - never from
    /// hover - so the row count, the section height and the drawn rects always agree.
    /// <para>
    /// One layout serves every mood and every parameter: the label column is the widest RESOLVED
    /// localized label (plus padding), the numeric group is one fixed width, and the slider starts at the
    /// group's left edge and runs to the card's inner right edge. That is what makes slider track widths
    /// equal across Pitch/Volume/Jitter and across all four moods.
    /// </para>
    /// </summary>
    private static MoodRowsLayout MoodRowsLayoutFor(float bodyWidth, UiWidgetContext ctx, IReadOnlyList<MoodTuningRowView> rows)
    {
        var layout = new MoodRowsLayout();
        float innerWidth = Mathf.Max(1f, bodyWidth - LeftPadding * 2f);
        layout.ParameterWidth = innerWidth;

        // Header: the mood name is measured in its own font through the resolver; the reset block is
        // measured from its own two labels, so a longer ruled phrase in either language widens the block
        // instead of clipping it.
        float nameWidth = 0f;
        foreach (MoodTuningRowView row in rows)
        {
            nameWidth = Mathf.Max(nameWidth, ctx.Metrics.MeasureWidth(MoodName(ctx, row), UiFont.Small));
        }

        float resetCluster = MoodResetClusterWidthFor(ctx);
        layout.ResetWidth = resetCluster;
        layout.ResetHeight = innerWidth >= resetCluster - 0.5f ? ButtonHeight : 2f * ButtonHeight + RowGap;
        layout.HeaderInline = innerWidth >= nameWidth + MoodHeaderGap + resetCluster;
        layout.NameBandWidth = layout.HeaderInline ? innerWidth - MoodHeaderGap - resetCluster : innerWidth;

        float nameBand = MoodNameMinBand;
        foreach (MoodTuningRowView row in rows)
        {
            nameBand = Mathf.Max(nameBand, ctx.Metrics.MeasureText(MoodName(ctx, row), UiFont.Small, Mathf.Max(1f, layout.NameBandWidth)));
        }

        layout.NameBandHeight = nameBand;
        layout.HeaderHeight = layout.HeaderInline
            ? Mathf.Max(ButtonHeight, nameBand)
            : nameBand + RowGap + layout.ResetHeight;

        // Parameter labels. The column is the widest resolved label plus padding - never a fixed band, and
        // never a language literal. A card too narrow to carry that column beside the numeric group moves
        // the label to its own line; the controls themselves are drawn in both shapes.
        float labelWidth = 0f;
        foreach (SqueakMoodFactor factor in MoodParameterFactors)
        {
            labelWidth = Mathf.Max(labelWidth, ctx.Metrics.MeasureWidth(UsKernelDraw.Keyed(ctx, ParameterLabelKey(factor)), UiFont.Tiny));
        }

        layout.ControlButtonWidth = MoodControlButtonWidth;
        layout.ControlFieldWidth = MoodControlFieldWidth;
        float defaultGroupWidth = MoodControlButtonWidth * 2f + MoodControlFieldWidth + MoodControlGap * 2f;
        float groupWidth = Mathf.Min(defaultGroupWidth, innerWidth);
        if (groupWidth < defaultGroupWidth)
        {
            // Pathologically narrow card: shrink the field first, then the two buttons, so every control
            // stays inside the card instead of spilling over its neighbours. Standard widths are unaffected.
            float shrink = defaultGroupWidth - groupWidth;
            float fieldShrink = Mathf.Min(shrink, MoodControlFieldWidth - MoodControlFieldMinWidth);
            layout.ControlFieldWidth = MoodControlFieldWidth - fieldShrink;
            shrink -= fieldShrink;
            layout.ControlButtonWidth = MoodControlButtonWidth
                - Mathf.Min(MoodControlButtonWidth - MoodControlButtonMinWidth, shrink * 0.5f);
        }

        layout.ControlGroupWidth = layout.ControlButtonWidth * 2f + layout.ControlFieldWidth + MoodControlGap * 2f;
        layout.LabelColumnWidth = labelWidth + MoodLabelColumnPad;
        layout.LabelOnOwnLine = innerWidth < layout.LabelColumnWidth + MoodLabelColumnGap + layout.ControlGroupWidth;

        float labelBandWidth = layout.LabelOnOwnLine ? innerWidth : layout.LabelColumnWidth;
        float labelBand = MoodParameterLabelMinBand;
        foreach (SqueakMoodFactor factor in MoodParameterFactors)
        {
            labelBand = Mathf.Max(labelBand, ctx.Metrics.MeasureText(UsKernelDraw.Keyed(ctx, ParameterLabelKey(factor)), UiFont.Tiny, Mathf.Max(1f, labelBandWidth)));
        }

        layout.LabelBandHeight = labelBand;
        layout.ControlLineHeight = layout.LabelOnOwnLine ? MoodControlHeight : Mathf.Max(MoodControlHeight, labelBand);
        layout.SliderX = LeftPadding + (layout.LabelOnOwnLine ? 0f : layout.LabelColumnWidth + MoodLabelColumnGap);
        layout.SliderY = (layout.LabelOnOwnLine ? labelBand : 0f) + layout.ControlLineHeight + MoodSliderGap;
        layout.SliderWidth = Mathf.Max(1f, bodyWidth - LeftPadding - layout.SliderX);
        layout.BlockHeight = layout.SliderY + MoodSliderHeight;
        layout.ParameterTop = layout.HeaderHeight + MoodHeaderGap;
        layout.TotalHeight = layout.ParameterTop
            + MoodParameterFactors.Length * layout.BlockHeight
            + (MoodParameterFactors.Length - 1) * MoodBlockGap;
        return layout;
    }

    /// <summary>
    /// Everything one mood card consumes, relative to its own rect. A mutable struct filled by
    /// <see cref="MoodRowsLayoutFor"/>: both Measure (TotalHeight) and Draw (the rest) read the same
    /// numbers, which is what keeps the section card from clipping a control.
    /// </summary>
    private struct MoodRowsLayout
    {
        public bool HeaderInline;
        public float HeaderHeight;
        public float NameBandWidth;
        public float NameBandHeight;
        public float ResetWidth;
        public float ResetHeight;
        public float ParameterWidth;
        public float ParameterTop;
        public bool LabelOnOwnLine;
        public float LabelColumnWidth;
        public float LabelBandHeight;
        public float ControlLineHeight;
        public float ControlGroupWidth;
        public float ControlButtonWidth;
        public float ControlFieldWidth;
        public float SliderX;
        public float SliderY;
        public float SliderWidth;
        public float BlockHeight;
        public float TotalHeight;
    }

    private float ScopeRowHeightFor(float rowWidth, string displayName, UiWidgetContext ctx)
    {
        float labelWidth = Math.Max(1f, rowWidth - LeftPadding - Math.Min(ButtonWidth, Math.Max(40f, rowWidth - 120f)) - 90f);
        float measured = ctx.Metrics.MeasureText(displayName, UiFont.Small, labelWidth);
        return Math.Max(RowHeight, measured + 8f);
    }

    private static SqueakActionScope[] SupportedStates(SqueakAction action)
    {
        var states = new List<SqueakActionScope>(3);
        foreach (SqueakActionScope scope in new[] { SqueakActionScope.Disabled, SqueakActionScope.AnyOccurrence, SqueakActionScope.ActiveCommand })
        {
            if (SqueakActionDefinitions.NormalizeScope(action, scope) == scope)
            {
                states.Add(scope);
            }
        }

        return states.ToArray();
    }

    private static SqueakActionDefinition DefinitionFor(SqueakAction action)
    {
        return SqueakActionDefinitions.Get(action);
    }

    /// <summary>
    /// Display-text key of a scope. Purely cosmetic: the bound value of a scope option is always
    /// <c>scope.ToString()</c>, nothing parses this text back, and the inherited-scope hint is the only
    /// other consumer. That is what lets the short label be translated while the persisted value stays
    /// byte-identical.
    /// </summary>
    private static string ScopeLabelKey(SqueakActionScope scope)
    {
        return scope switch
        {
            SqueakActionScope.AnyOccurrence => "US.Tuning.Scope.Any",
            SqueakActionScope.ActiveCommand => "US.Tuning.Scope.Command",
            _ => "US.Tuning.Scope.Off",
        };
    }
}
