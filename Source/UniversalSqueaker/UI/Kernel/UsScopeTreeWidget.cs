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
///  - mood rows expose pitch/volume/jitter steppers plus two reset controls: "reset to default"
///    ("set-mood-tuning" with Clear) and "reset to preset" ("reset-mood-to-preset"); an unavailable
///    control stays drawn and greyed, and hovering it explains why in the help panel.
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

    private const float MoodRowHeight = 32f;
    // The mood name is a Small line in a fixed column: a 16px band is shorter than one such line, so
    // the band is measured and the row grows with it. Measure and Draw share MoodLabelBand below.
    private const float MoodLabelMinHeight = 16f;
    private const float MoodLabelTop = 7f;
    private const float RowGap = 2f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;
    private const float LeftPadding = 10f;
    private const float ButtonWidth = 96f;
    private const float ButtonHeight = 24f;
    private const float MoodLabelWidth = 64f;
    // Floor for each of the mood row's two reset controls. Every drawn width is measured from its own
    // label (MoodResetWidthFor): the ruled phrases are verb phrases ("重置为默认" / "Reset to default"),
    // and a fixed box wrapped the earlier single control in English - the bilingual fit sweep caught it
    // as "needs 54px, has 24px at width 40px". Two controls, two measurements, same floor.
    private const float MoodResetWidthMin = 52f;
    private const float MoodGap = 6f;
    // The narrowest stepper group the inline layout accepts; below it the buttons move to a second line,
    // and below that the whole cluster stacks (same 110px floor the single-button decision used).
    private const float MoodStepperGroupMinWidth = 110f;

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
            bodyHeight += moodRows.Count * (MoodRowHeightFor(width, ctx, moodRows) + RowGap);
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

            float moodRowHeight = MoodRowHeightFor(innerWidth, ctx, moodRows);
            foreach (MoodTuningRowView mood in moodRows)
            {
                DrawMoodRow(new Rect(x, y, innerWidth, moodRowHeight), mood, ctx);
                y += moodRowHeight + RowGap;
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

    private void DrawMoodRow(Rect rect, MoodTuningRowView row, UiWidgetContext ctx)
    {
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, "us/scope-tree/mood-tuning");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, UsKernelDraw.RowRail.None);

        float pitch = row.Own?.hasPitchFactor == true ? row.Own.pitchFactor : row.EffectivePitch;
        float volume = row.Own?.hasVolumeFactor == true ? row.Own.volumeFactor : row.EffectiveVolume;
        float jitter = row.Own?.hasPitchJitter == true ? Math.Max(0f, row.Own.pitchJitter.max - 1f) : row.EffectiveJitterHalf;

        float buttonsWidth = MoodResetClusterWidthFor(ctx);
        MoodRowMode mode = MoodRowModeFor(rect.width, ctx, buttonsWidth);
        if (mode == MoodRowMode.Stacked)
        {
            DrawStackedMoodRow(rect, row, ctx, pitch, volume, jitter, buttonsWidth);
            return;
        }

        float lineHeight = InlineMoodLineHeight(ctx, row.DisplayName);
        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y + MoodLabelTop, MoodLabelWidth, MoodLabelBand(ctx, row.DisplayName)),
            row.DisplayName,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);

        // Inline shares the line with both buttons; SecondLine gives the buttons their own line, so the
        // three steppers may use the full body width. The key is the BODY WIDTH, not hover: a
        // hover-dependent height would make Measure and Draw disagree and clip the section card.
        float controlsRight = mode == MoodRowMode.Inline ? rect.xMax - buttonsWidth - 8f : rect.xMax;
        float controlsWidth = controlsRight - (rect.x + LeftPadding + MoodLabelWidth) - MoodGap;
        float groupWidth = (controlsWidth - MoodGap * 2f) / 3f;
        float factorX = rect.x + LeftPadding + MoodLabelWidth + MoodGap;

        factorX = DrawMoodStepper(new Rect(factorX, rect.y, groupWidth, lineHeight), PitchLabelKey, pitch, 0.5f, 2f, row, SqueakMoodFactor.Pitch, ctx);
        factorX = DrawMoodStepper(new Rect(factorX + MoodGap, rect.y, groupWidth, lineHeight), VolumeLabelKey, volume, 0.1f, 2f, row, SqueakMoodFactor.Volume, ctx);
        DrawMoodStepper(new Rect(factorX + MoodGap, rect.y, groupWidth, lineHeight), JitterLabelKey, jitter, 0f, 0.5f, row, SqueakMoodFactor.Jitter, ctx);

        if (mode == MoodRowMode.Inline)
        {
            DrawMoodResetButtons(new Rect(rect.xMax - buttonsWidth - 8f, rect.y, buttonsWidth, rect.height), row, ctx);
        }
        else
        {
            DrawMoodResetButtons(new Rect(rect.xMax - buttonsWidth - 8f, rect.y + lineHeight + RowGap, buttonsWidth, ButtonHeight), row, ctx);
        }
    }

    /// <summary>Which of the three mood-row layouts a given body width gets.</summary>
    private enum MoodRowMode
    {
        /// <summary>Label + three steppers + both reset controls on one line.</summary>
        Inline,

        /// <summary>Steppers on the first line, the two reset controls on a second (the "second line"
        /// shape: the controls that do not fit wrap instead of being squeezed or shrunk).</summary>
        SecondLine,

        /// <summary>Label line, reset controls, then three full-width stepper lines.</summary>
        Stacked,
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

    /// <summary>Height of the mood row's single control line (label band or the 32px minimum).</summary>
    private static float InlineMoodLineHeight(UiWidgetContext ctx, string displayName)
    {
        return Math.Max(MoodRowHeight, MoodLabelTop + MoodLabelBand(ctx, displayName) + MoodLabelTop);
    }

    /// <summary>Lines the two reset controls need at this row width: one when they fit side by side, else one each.</summary>
    private static int MoodResetLinesFor(float rowWidth, UiWidgetContext ctx)
    {
        return rowWidth >= MoodResetClusterWidthFor(ctx) ? 1 : 2;
    }

    private static float MoodResetBlockHeight(float rowWidth, UiWidgetContext ctx)
    {
        return MoodResetLinesFor(rowWidth, ctx) == 1 ? ButtonHeight : 2f * ButtonHeight + RowGap;
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

    private void DrawStackedMoodRow(Rect rect, MoodTuningRowView row, UiWidgetContext ctx, float pitch, float volume, float jitter, float buttonsWidth)
    {
        bool buttonsInline = StackedHeaderFitsButtons(rect.width, buttonsWidth);
        float headerHeight = ButtonHeight;
        float labelWidth = Math.Max(1f, rect.width - LeftPadding * 2f - (buttonsInline ? buttonsWidth + MoodGap : 0f));
        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y, labelWidth, headerHeight),
            row.DisplayName,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);

        float y = rect.y;
        if (buttonsInline)
        {
            DrawMoodResetButtons(new Rect(rect.xMax - buttonsWidth - LeftPadding, y, buttonsWidth, headerHeight), row, ctx);
        }

        y += headerHeight + RowGap;
        if (!buttonsInline)
        {
            float block = MoodResetBlockHeight(rect.width, ctx);
            DrawMoodResetButtons(new Rect(rect.x + LeftPadding, y, rect.width - LeftPadding * 2f, block), row, ctx);
            y += block + RowGap;
        }

        float lineWidth = Math.Max(1f, rect.width - LeftPadding * 2f);
        DrawMoodStepper(
            new Rect(rect.x + LeftPadding, y, lineWidth, ButtonHeight),
            PitchLabelKey, pitch, 0.5f, 2f, row, SqueakMoodFactor.Pitch, ctx);
        y += ButtonHeight + RowGap;
        DrawMoodStepper(
            new Rect(rect.x + LeftPadding, y, lineWidth, ButtonHeight),
            VolumeLabelKey, volume, 0.1f, 2f, row, SqueakMoodFactor.Volume, ctx);
        y += ButtonHeight + RowGap;
        DrawMoodStepper(
            new Rect(rect.x + LeftPadding, y, lineWidth, ButtonHeight),
            JitterLabelKey, jitter, 0f, 0.5f, row, SqueakMoodFactor.Jitter, ctx);
    }

    private float DrawMoodStepper(
        Rect rect,
        string labelKey,
        float value,
        float min,
        float max,
        MoodTuningRowView row,
        SqueakMoodFactor factor,
        UiWidgetContext ctx)
    {
        UsKernelDraw.Label(
            new Rect(rect.x, rect.y + 4f, 14f, 18f),
            UsKernelDraw.Keyed(ctx, labelKey),
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        string elementId = "mood-" + row.Mood + "-" + factor;
        float x = rect.x + 14f;
        float height = rect.height;
        float buttonWidth = 20f;
        float fieldWidth = 40f;
        float gap = 4f;

        Rect minusRect = new(x, rect.y, buttonWidth, height);
        x += buttonWidth + gap;
        float sliderWidth = Math.Max(1f, rect.xMax - x - fieldWidth - gap * 2f - buttonWidth);
        Rect sliderRect = new(x, rect.y, sliderWidth, height);
        x += sliderRect.width + gap;
        Rect fieldRect = new(x, rect.y, fieldWidth, height);
        x += fieldWidth + gap;
        Rect plusRect = new(Math.Min(x, rect.xMax - buttonWidth), rect.y, buttonWidth, height);

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

        return rect.x + rect.width;
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

    /// <summary>
    /// Layout mode for a given body width. Inline needs a stepper group of at least
    /// <see cref="MoodStepperGroupMinWidth"/> beside the label AND both reset controls; when only the
    /// controls fail to fit they wrap to a second line (SecondLine) and the steppers keep the full
    /// width; otherwise the whole cluster stacks. Measure and Draw both come through here.
    /// </summary>
    private static MoodRowMode MoodRowModeFor(float bodyWidth, UiWidgetContext ctx, float buttonsWidth)
    {
        if (StepperGroupWidth(bodyWidth, buttonsWidth, out float inlineGroup) && inlineGroup >= MoodStepperGroupMinWidth)
        {
            return MoodRowMode.Inline;
        }

        if (StepperGroupWidth(bodyWidth, 0f, out float wrappedGroup) && wrappedGroup >= MoodStepperGroupMinWidth)
        {
            return MoodRowMode.SecondLine;
        }

        return MoodRowMode.Stacked;
    }

    /// <summary>Group width the three steppers get when the first line carries <paramref name="buttonsWidthOnLine"/>
    /// pixels of reset controls (0 = none). Mirrors the draw math in <see cref="DrawMoodRow"/>.</summary>
    private static bool StepperGroupWidth(float bodyWidth, float buttonsWidthOnLine, out float groupWidth)
    {
        float controlsRight = bodyWidth - (buttonsWidthOnLine > 0f ? buttonsWidthOnLine + 8f : 0f);
        float controlsWidth = controlsRight - (LeftPadding + MoodLabelWidth) - MoodGap;
        groupWidth = (controlsWidth - MoodGap * 2f) / 3f;
        return controlsWidth > 0f;
    }

    /// <summary>Whether the stacked header line can hold the mood label and both reset controls.</summary>
    private static bool StackedHeaderFitsButtons(float bodyWidth, float buttonsWidth)
    {
        return bodyWidth - LeftPadding * 2f >= buttonsWidth + MoodLabelWidth;
    }

    /// <summary>
    /// Height a mood row actually consumes in the mode its body width selects: one control line
    /// (Inline); that line plus a controls line (SecondLine); or the stacked header - plus a controls
    /// line when the header cannot hold them - and three full-width stepper lines. Measure and Draw
    /// must agree so the section card never clips or overlaps mood controls.
    /// </summary>
    private static float MoodRowHeightFor(float bodyWidth, UiWidgetContext ctx, IReadOnlyList<MoodTuningRowView> rows)
    {
        float buttonsWidth = MoodResetClusterWidthFor(ctx);
        MoodRowMode mode = MoodRowModeFor(bodyWidth, ctx, buttonsWidth);
        float lineHeight = Math.Max(MoodRowHeight, MoodLabelTop + MaxMoodLabelBand(ctx, rows) + MoodLabelTop);

        if (mode == MoodRowMode.Inline) return lineHeight;
        if (mode == MoodRowMode.SecondLine) return lineHeight + RowGap + MoodResetBlockHeight(bodyWidth, ctx);

        // Stacked: label line, then either the controls in the header (when they fit beside the label) or
        // their own block (one or two lines), then the three full-width stepper lines.
        float controlsBlock = StackedHeaderFitsButtons(bodyWidth, buttonsWidth)
            ? ButtonHeight
            : ButtonHeight + RowGap + MoodResetBlockHeight(bodyWidth, ctx);
        return controlsBlock + RowGap + 3f * ButtonHeight + 2f * RowGap;
    }

    /// <summary>Tallest mood-name band in the set, so one row height serves every mood in the language.</summary>
    private static float MaxMoodLabelBand(UiWidgetContext ctx, IReadOnlyList<MoodTuningRowView> rows)
    {
        float band = MoodLabelMinHeight;
        foreach (MoodTuningRowView row in rows)
        {
            band = Math.Max(band, MoodLabelBand(ctx, row.DisplayName));
        }

        return band;
    }

    private static float MoodLabelBand(UiWidgetContext ctx, string label)
    {
        return Math.Max(MoodLabelMinHeight, ctx.Metrics.MeasureText(label, UiFont.Small, MoodLabelWidth));
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
