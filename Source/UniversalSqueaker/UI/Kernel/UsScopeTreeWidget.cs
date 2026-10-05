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
///  - XG1.1: while the XENOTYPE layer has no tunable target (its own identity is empty, which is the state
///    an empty domain catalog produces), one reason sentence is drawn under the target row and the action,
///    value and reset controls become INERT: a static disabled-ink band replaces the scope trigger (no
///    dropdown call, so no popup path exists), static number/minus/plus/slider representations replace the
///    editing atoms (no focus/draft/drag path exists) and the reset controls are not even hit-tested - all in
///    the SAME bands, so the row shapes are unchanged. The layer segment stays live so the player can leave.
///    Nothing here redefines a target: the catalog definition and the persisted domain are the model's.
/// All values come from typed read bindings; every write is a typed action.
/// </summary>
public sealed class UsScopeTreeWidget : UsSectionWidgetBase
{
    public const string KindName = "us/scope-tree";

    private const float LayerRowHeight = 30f;
    private const float DomainRowHeight = 28f;
    private const float LayerRowLabelBandHeight = 22f;
    private const float RowHeight = 28f;

    // V3 A1: the inherited-scope hint is part of the row's LAYOUT, not a width-gated decoration. The row
    // keeps the hint inline beside the dropdown while the action name still gets ScopeLabelMinBand; when the
    // body is too tight for both, the hint moves to its own line under the name instead of disappearing (the
    // pre-V3 shape suppressed it entirely below a 480px element width, which is every real help-open body).
    private const float InheritedHintHeight = 18f;
    /// <summary>Minimum band the hint keeps (measured text wins when it is wider).</summary>
    private const float InheritedHintMinWidth = 86f;
    /// <summary>Gap between the hint and the dropdown trigger / the action name.</summary>
    private const float InheritedHintGap = 6f;
    /// <summary>Right inset of the dropdown trigger inside the row.</summary>
    private const float ScopeDropdownGap = 8f;
    /// <summary>Smallest action-name band the row accepts before the hint drops to its own line.</summary>
    private const float ScopeLabelMinBand = 120f;

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
    /// <summary>V3 A2: band floor for the mood source/inheritance readout (a Tiny line).</summary>
    private const float MoodSourceMinBand = 14f;
    /// <summary>V3 A2: gap between the mood name and its source readout.</summary>
    private const float MoodSourceGap = 2f;
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
    // int index, scope options carry the SqueakActionScope value itself (R4-A / A2 - no string token),
    // the domain dropdown carries the typed (race, xeno) selection and the stepper element ids are built
    // from enums. Only the text drawn to the player goes through these keys, resolved at the site that
    // owns the Label/SelectionButton call.
    private const string LayerLabelKey = "US.Tuning.Layer";
    private const string DomainLabelKey = "US.Tuning.Domain";
    private const string ActionScopeHeaderKey = "US.Tuning.ActionScope";
    private const string GroupPlayerKey = "US.Tuning.Group.PlayerTriggered";
    private const string GroupSystemKey = "US.Tuning.Group.SystemOrEvent";
    /// <summary>SA1.2: SR's "征召 / 取消征召" sub-heading above the Draft/Undraft pair; the two settings
    /// stay independent rows, the heading only names the pair.</summary>
    private const string DraftUndraftKey = "US.Action.DraftUndraft";
    private const float DraftHeadingHeight = 18f;
    private const string MoodTuningHeaderKey = "US.Tuning.MoodTuning";
    /// <summary>Action-scope dropdown option: "inherit from below". Per the term split this word belongs to
    /// the action side only; the mood row's controls use the two "reset" phrases below instead
    /// (one word, two meanings was the direct cause of mis-clicks).</summary>
    private const string AutoLabelKey = "US.Tuning.Auto";
    /// <summary>V3 task-18 additive key: no layer supplies this factor, so the value is the DEFAULT.</summary>
    private const string DefaultSourceKey = "US.Tuning.Source.Default";
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
    /// <summary>XG1.1 additive key: the xenotype tuning layer has no tunable target, so nothing in this card
    /// can be submitted until a target exists. Drawn as one sentence under the target row.</summary>
    private const string EmptyXenotypeTargetKey = "US.Tuning.EmptyXenotypeTarget";
    /// <summary>XG1.1: the mood area's own help entry, reused for a control that is inert because the LAYER has
    /// no target. Reusing it keeps the message honest: the row-specific "no local value" / "not from a preset"
    /// reasons would be false here (the row can be perfectly Ready while the layer has nothing to write to),
    /// and no new help item is invented outside the help catalog this step does not own.</summary>
    private const string MoodTuningAreaHelpKey = "us/scope-tree/mood-tuning";
    /// <summary>XG1.1: band floor for the reason sentence (one Tiny line), measured and drawn through the same
    /// metrics seam so the card grows by exactly the text it paints.</summary>
    private const float EmptyTargetMinBand = 14f;
    private const string PitchLabelKey = "US.Tuning.Factor.Pitch";
    private const string VolumeLabelKey = "US.Tuning.Factor.Volume";
    private const string JitterLabelKey = "US.Tuning.Factor.Jitter";

    /// <summary>
    /// Display-text keys of the three tuning layers, index-aligned with the "tuning-layer" int binding.
    /// The length doubles as the layer count for the stacked-row height, so text and geometry share one
    /// source and Measure can never size a different number of buttons than Draw makes.
    /// </summary>
    private static readonly string[] LayerKeys = { "US.Tuning.Layer.Global", "US.Tuning.Layer.Race", "US.Tuning.Layer.Xenotype" };

    /// <summary>
    /// XG1.1: the ONE predicate that says "this layer has nothing to tune". It is the xenotype layer's own
    /// identity, and nothing else: the production model builds an EMPTY domain option list exactly when no
    /// (race, xenotype) tuning domain exists, and leaves the target empty in that case
    /// (<c>BuildTuningDomains</c>, layer 2). It deliberately does NOT read the colonist count, a Biotech flag
    /// or the target catalog's definition - this control may not change what a target IS, only refuse to
    /// submit one that does not exist. Measure, Draw and every write gate call this same function, so the
    /// reserved band, the painted sentence and the blocked invokes cannot disagree.
    /// <para>
    /// Both callers evaluate it AFTER the view-backed reads (action-scopes / mood-rows), which is what makes
    /// the identity here the one the model normalised on this pass rather than a stale pre-build value.
    /// </para>
    /// </summary>
    private static bool XenotypeTargetIsEmpty(int layer, UiWidgetContext ctx)
    {
        if (layer != 2) return false;
        string xeno = ctx.Bindings.TryGet("tuning-xeno", out string value) ? value : "";
        return string.IsNullOrEmpty(xeno);
    }

    /// <summary>The reason sentence, resolved through the Host translation seam (never a source literal).</summary>
    private static string EmptyTargetText(UiWidgetContext ctx)
    {
        return UsKernelDraw.Keyed(ctx, EmptyXenotypeTargetKey);
    }

    /// <summary>
    /// XG1.1: height of the reason band. The SAME width expression is used by Measure and Draw, and the width
    /// is the label rect's own width, so a wrap in either language grows the band instead of clipping.
    /// </summary>
    private static float EmptyTargetBandHeight(UiWidgetContext ctx, float bodyWidth)
    {
        float wrapWidth = Mathf.Max(1f, bodyWidth - LeftPadding);
        return Mathf.Max(EmptyTargetMinBand, ctx.Metrics.MeasureText(EmptyTargetText(ctx), UiFont.Tiny, wrapWidth));
    }

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
        // XG1.1: the empty-target reason band. Measure and Draw both derive it from the SAME predicate and the
        // same metrics seam, so the card can never reserve a band it does not paint (or paint one it did not
        // reserve) - the failure that would move every control below it.
        if (XenotypeTargetIsEmpty(layer, ctx)) bodyHeight += EmptyTargetBandHeight(ctx, width) + RowGap;
        bodyHeight += RowHeight + RowGap; // "Action Scope" header
        if (scopeRows.Count > 0)
        {
            for (int group = 0; group < 2; group++)
            {
                ActionScopeGroup targetGroup = group == 0
                    ? ActionScopeGroup.PlayerTriggered
                    : ActionScopeGroup.SystemOrEvent;
                List<ActionScopeRowView> groupRows = OrderedRows(targetGroup, scopeRows);
                if (groupRows.Count == 0) continue;

                bodyHeight += RowHeight + RowGap; // group header
                if (targetGroup == ActionScopeGroup.PlayerTriggered && HasDraftPair(groupRows))
                {
                    bodyHeight += DraftHeadingHeight + RowGap; // SA1.2 pair sub-heading
                }

                for (int i = 0; i < groupRows.Count; i++)
                {
                    bodyHeight += ScopeRowHeightFor(width, groupRows[i], ctx) + RowGap;
                }
            }
        }

        // V3 P1 deliberate rule: the MOOD area is NOT gated on the scope rows. It is the layer's own
        // parameter area, so it depends on the mood rows alone - Measure and Draw use this same predicate.
        if (moodRows.Count > 0)
        {
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

        // XG1.1: the current XENOTYPE layer has no tunable target. The reason is stated ONCE, visibly, right
        // under the target row it is about; the controls below stay drawn and greyed but cannot submit. The
        // layer segment above stays live on purpose: switching layer (or picking a target when one exists) is
        // the ONLY way out of this state, so blocking it would trap the player.
        bool submittable = !XenotypeTargetIsEmpty(layer, ctx);
        if (!submittable)
        {
            float reasonBand = EmptyTargetBandHeight(ctx, innerWidth);
            UsKernelDraw.Label(
                new Rect(x + LeftPadding, y, Mathf.Max(1f, innerWidth - LeftPadding), reasonBand),
                EmptyTargetText(ctx),
                ctx.Theme,
                ctx.Theme.TextSecondary,
                UiFont.Tiny,
                TextAnchor.MiddleLeft);
            y += reasonBand + RowGap;
        }

        UsKernelDraw.Label(
            new Rect(x, y, innerWidth, RowHeight),
            UsKernelDraw.Keyed(ctx, ActionScopeHeaderKey),
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
        y += RowHeight + RowGap;

        // V3 P1: ONE predicate decides each area, and the rule is deliberate: the scope GROUP rows depend on
        // the scope list, the MOOD area depends only on the mood rows. Measure uses the same two predicates
        // (moodRows.Count for the mood heading/rows, the group scan for the group headers), so the two passes
        // can no longer disagree the way `scopeRows.Count > 0` vs `anyScopeDrawn` could.
        bool hasScopeRows = scopeRows.Count > 0;
        for (int group = 0; group < 2; group++)
        {
            if (!hasScopeRows) break;
            ActionScopeGroup targetGroup = group == 0
                ? ActionScopeGroup.PlayerTriggered
                : ActionScopeGroup.SystemOrEvent;
            List<ActionScopeRowView> groupRows = OrderedRows(targetGroup, scopeRows);
            if (groupRows.Count == 0) continue;

            UsKernelDraw.Label(
                new Rect(x, y, innerWidth, RowHeight),
                UsKernelDraw.Keyed(ctx, targetGroup == ActionScopeGroup.PlayerTriggered ? GroupPlayerKey : GroupSystemKey),
                ctx.Theme,
                ctx.Theme.TextSecondary,
                UiFont.Tiny,
                TextAnchor.MiddleLeft);
            y += RowHeight + RowGap;

            if (targetGroup == ActionScopeGroup.PlayerTriggered && HasDraftPair(groupRows))
            {
                UsKernelDraw.Label(
                    new Rect(x + 6f, y, Math.Max(1f, innerWidth - 6f), DraftHeadingHeight),
                    UsKernelDraw.Keyed(ctx, DraftUndraftKey),
                    ctx.Theme,
                    ctx.Theme.AccentGold,
                    UiFont.Tiny,
                    TextAnchor.MiddleLeft);
                y += DraftHeadingHeight + RowGap;
            }

            for (int i = 0; i < groupRows.Count; i++)
            {
                float rowHeight = ScopeRowHeightFor(innerWidth, groupRows[i], ctx);
                DrawScopeRow(new Rect(x, y, innerWidth, rowHeight), groupRows[i], ctx, submittable);
                y += rowHeight + RowGap;
            }
        }

        if (moodRows.Count > 0)
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
                DrawMoodRow(new Rect(x, y, innerWidth, moodLayout.TotalHeight), mood, moodLayout, ctx, layer, submittable);
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

        // R4-A / A2: the option value is the typed selection itself, so the commit no longer splits a
        // delimiter-joined token. Same labels, same trigger width, same popup.
        var options = new List<UiChoice<UsTuningDomainSelection>>();
        UsTuningDomainSelection current = default;
        foreach (TuningDomainOptionView domain in domains)
        {
            var value = new UsTuningDomainSelection(domain.RaceDefName, domain.TargetDefName);
            options.Add(new UiChoice<UsTuningDomainSelection>(domain.DisplayName, value));
            if (string.Equals(domain.RaceDefName, race, StringComparison.Ordinal)
                && string.Equals(domain.TargetDefName, xeno, StringComparison.Ordinal))
            {
                current = value;
            }
        }

        UsKernelDraw.Dropdown(dropdownRect, "scope-tree-domain", ctx, current, options,
            selection => ctx.Bindings.Invoke("set-tuning-domain", selection));
    }

    private void DrawScopeRow(Rect rect, ActionScopeRowView row, UiWidgetContext ctx, bool submittable)
    {
        // A trigger that currently reads "Auto" claims the Auto/Clear entry instead of the generic
        // action-scope one, so the panel explains what the visible value means.
        bool hovered = UsKernelDraw.HelpHover(
            rect, ctx, row.HasOwnScope ? "us/scope-tree/action-scope" : "us/scope-tree/auto");
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered && submittable, UsKernelDraw.RowRail.None);

        ScopeRowLayout layout = ScopeRowLayoutFor(rect.width, UsKernelDraw.Keyed(ctx, DefinitionFor(row.Action).DisplayKey), HintTextFor(ctx, row), ctx.Metrics);
        float scopeButtonWidth = layout.DropdownWidth;

        // XG1.1: the row is drawn in the disabled ink while the layer has no target. The carrier dropdown has
        // no enabled flag this scope owns, so the ink plus the reason sentence above are how the state reads.
        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y, layout.LabelWidth, layout.LabelLineHeight),
            UsKernelDraw.Keyed(ctx, DefinitionFor(row.Action).DisplayKey),
            ctx.Theme,
            submittable ? ctx.Theme.TextPrimary : ctx.Theme.TextDisabled,
            UiFont.Small,
            TextAnchor.MiddleLeft);

        // V3 A1: the inheritance state is drawn at EVERY body width the real boxes produce - inline beside
        // the dropdown when it fits, on its own line under the name when it does not. Never suppressed.
        if (layout.HintText.Length > 0)
        {
            UsKernelDraw.Label(
                new Rect(rect.x + layout.HintX, rect.y + layout.HintY, layout.HintWidth, layout.HintBandHeight),
                layout.HintText,
                ctx.Theme,
                submittable ? ctx.Theme.TextSecondary : ctx.Theme.TextDisabled,
                UiFont.Tiny,
                TextAnchor.MiddleLeft);
        }

        // R4-A / A2: the option list carries the TYPED value, and the Auto entry carries a real null - the
        // null-inherit commit is a value, not an empty token. Nothing here converts a scope to a string.
        var options = new List<UiChoice<SqueakActionScope?>>
        {
            new UiChoice<SqueakActionScope?>(UsKernelDraw.Keyed(ctx, AutoLabelKey), null)
        };
        foreach (SqueakActionScope scope in SupportedStates(row.Action))
        {
            options.Add(new UiChoice<SqueakActionScope?>(UsKernelDraw.Keyed(ctx, ActionScopeRules.ScopeLabelKey(row.Action, scope)), scope));
        }

        SqueakActionScope? current = row.HasOwnScope ? row.Scope : null;
        Rect dropdownRect = new(rect.xMax - scopeButtonWidth - ScopeDropdownGap, rect.y + (rect.height - ButtonHeight) * 0.5f, scopeButtonWidth, ButtonHeight);
        string elementId = "scope-tree-scope-" + row.ActionKey;

        // XG1.1 (r2): with no target the trigger is not CALLED at all - a static band is drawn in the trigger's
        // own rect instead. That is the difference between "the commit is gated" and "the interaction does not
        // exist": no UiNative.DropdownButton call means no popup anchor, no element id to open and no popup
        // path whatever; the band still shows the scope this row holds, in the disabled ink.
        if (!submittable)
        {
            DrawDisabledScopeBand(dropdownRect, ctx, current, options);
            return;
        }

        UsKernelDraw.Dropdown(dropdownRect, elementId, ctx, current, options, scope =>
        {
            // The chosen option's own value, straight through: no parse, no default, and a row that owned
            // no scope still commits null.
            ctx.Bindings.Invoke("set-action-scope", new UsScopeWrite(row.ActionKey, scope));
        });
    }

    /// <summary>
    /// XG1.1 (r2): the inert stand-in for the scope trigger - the same rect, the same surface vocabulary and
    /// the same displayed value (matched by VALUE, exactly as <c>UsKernelDraw.Dropdown&lt;T&gt;</c> matches it),
    /// but drawn by hand so no interactive atom is reached. The label is ellipsized through the widget's own
    /// measuring seam for the same reason the real trigger is: a fixed-width band must not report an overflow
    /// for a scope name it would have truncated.
    /// </summary>
    private static void DrawDisabledScopeBand(Rect rect, UiWidgetContext ctx, SqueakActionScope? current, IReadOnlyList<UiChoice<SqueakActionScope?>> options)
    {
        string display = "";
        for (int i = 0; i < options.Count; i++)
        {
            if (EqualityComparer<SqueakActionScope?>.Default.Equals(options[i].Value, current))
            {
                display = options[i].Text;
                break;
            }
        }

        UsKernelDraw.RowSurface(rect, ctx.Theme, false, UsKernelDraw.RowRail.None);
        float textWidth = Mathf.Max(1f, rect.width - 12f);
        UsKernelDraw.Label(
            new Rect(rect.x + 6f, rect.y, textWidth, rect.height),
            UsKernelDraw.Ellipsized(display, ctx, UiFont.Tiny, textWidth),
            ctx.Theme,
            ctx.Theme.TextDisabled,
            UiFont.Tiny,
            TextAnchor.MiddleLeft,
            singleLine: true);
    }

    /// <summary>The inherited-scope hint text, or "" when the row's own scope IS the effective one (nothing
    /// to explain). Resolved through the same label keys the dropdown uses, so the hint never invents text.</summary>
    private static string HintTextFor(UiWidgetContext ctx, ActionScopeRowView row)
    {
        if (row.HasOwnScope && row.Scope == row.EffectiveScope) return "";
        return "→ " + UsKernelDraw.Keyed(ctx, ActionScopeRules.ScopeLabelKey(row.Action, row.EffectiveScope));
    }

    /// <summary>
    /// V3 A1: the ONE geometry function the row's measure and draw passes share. It answers the questions the
    /// pre-V3 code answered in two places with a hard-coded 90/96 pair (which overlapped by 6px): how wide the
    /// action-name band is, whether the inherited hint stays inline, and how tall the row must be. The hint is
    /// never dropped - it moves to its own line. Pure numbers + strings, so the harness lane can assert the
    /// columns at the real body widths without re-deriving them.
    /// </summary>
    public static ScopeRowLayout ScopeRowLayoutFor(float rowWidth, string displayName, string hintText, ITextMetrics metrics)
    {
        string hint = hintText ?? "";
        bool hasHint = hint.Length > 0;
        float dropdownWidth = Math.Min(ButtonWidth, Math.Max(40f, rowWidth - 120f));
        float dropdownX = rowWidth - dropdownWidth - ScopeDropdownGap;
        float hintTextWidth = hasHint ? metrics.MeasureWidth(hint, UiFont.Tiny) + 2f : 0f;
        float hintWidth = hasHint ? Math.Max(InheritedHintMinWidth, hintTextWidth) : 0f;
        float hintX = dropdownX - InheritedHintGap - hintWidth;
        float inlineLabelWidth = hintX - InheritedHintGap - LeftPadding;
        bool hintInline = !hasHint || inlineLabelWidth >= ScopeLabelMinBand;
        float labelWidth = Math.Max(1f, hasHint && hintInline
            ? inlineLabelWidth
            : dropdownX - InheritedHintGap - LeftPadding);
        float hintBandHeight = hasHint
            ? Math.Max(InheritedHintHeight, metrics.MeasureText(hint, UiFont.Tiny, Math.Max(1f, hintWidth)))
            : 0f;
        float labelLineHeight = Math.Max(RowHeight, metrics.MeasureText(displayName ?? "", UiFont.Small, labelWidth) + 8f);

        if (!hasHint)
        {
            return new ScopeRowLayout(hint, dropdownWidth, labelWidth, true, 0f, 0f, labelLineHeight, 0f, 0f, labelLineHeight);
        }

        if (hintInline)
        {
            return new ScopeRowLayout(
                hint,
                dropdownWidth,
                labelWidth,
                true,
                hintWidth,
                hintBandHeight,
                labelLineHeight,
                hintX,
                Math.Max(0f, (labelLineHeight - hintBandHeight) * 0.5f),
                labelLineHeight);
        }

        // Own line: the name keeps the full line minus the trigger, the hint sits under it. Even a body far
        // below the real boxes loses no state - it stacks instead.
        return new ScopeRowLayout(
            hint,
            dropdownWidth,
            labelWidth,
            false,
            hintWidth,
            hintBandHeight,
            labelLineHeight,
            LeftPadding,
            labelLineHeight + RowGap,
            labelLineHeight + RowGap + hintBandHeight);
    }

    /// <summary>One action-scope row's column geometry (see <see cref="ScopeRowLayoutFor"/>).</summary>
    public readonly struct ScopeRowLayout
    {
        public readonly string HintText;
        public readonly float DropdownWidth;
        public readonly float LabelWidth;
        public readonly bool HintInline;
        public readonly float HintWidth;
        public readonly float HintBandHeight;
        public readonly float LabelLineHeight;
        public readonly float HintX;
        public readonly float HintY;
        public readonly float RowHeight;

        public ScopeRowLayout(
            string hintText,
            float dropdownWidth,
            float labelWidth,
            bool hintInline,
            float hintWidth,
            float hintBandHeight,
            float labelLineHeight,
            float hintX,
            float hintY,
            float rowHeight)
        {
            HintText = hintText;
            DropdownWidth = dropdownWidth;
            LabelWidth = labelWidth;
            HintInline = hintInline;
            HintWidth = hintWidth;
            HintBandHeight = hintBandHeight;
            LabelLineHeight = labelLineHeight;
            HintX = hintX;
            HintY = hintY;
            RowHeight = rowHeight;
        }
    }

    /// <summary>
    /// One mood card. The header line carries the mood name (resolved through its US.Mood.* key, so no
    /// language literal lives in this file) and the two reset controls; below it every parameter gets the
    /// same two-line block: label, minus, numeric field and plus on the first line, the slider on the
    /// second. The geometry comes from <see cref="MoodRowsLayoutFor"/> - the same function Measure uses -
    /// so a hover never changes a height and the section card can never clip a control.
    /// </summary>
    private void DrawMoodRow(Rect rect, MoodTuningRowView row, MoodRowsLayout layout, UiWidgetContext ctx, int layer, bool submittable)
    {
        bool hovered = UsKernelDraw.HelpHover(rect, ctx, MoodTuningAreaHelpKey);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered && submittable, UsKernelDraw.RowRail.None);

        float pitch = row.Own?.hasPitchFactor == true ? row.Own.pitchFactor : row.EffectivePitch;
        float volume = row.Own?.hasVolumeFactor == true ? row.Own.volumeFactor : row.EffectiveVolume;
        float jitter = row.Own?.hasPitchJitter == true ? Math.Max(0f, row.Own.pitchJitter.max - 1f) : row.EffectiveJitterHalf;

        // Header. When the name and the measured reset block cannot share the line, the controls drop to
        // their own line - they are never squeezed away, and an unavailable control stays drawn and inert
        // while still claiming the help entry that carries its reason. The source readout (V3 A2) sits under
        // the name in BOTH shapes, so it is always visible and always adjacent to the value it explains.
        float nameY = rect.y + MoodHeaderLeftTopFor(layout);
        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, nameY, layout.NameBandWidth, layout.NameBandHeight),
            MoodName(ctx, row),
            ctx.Theme,
            submittable ? ctx.Theme.TextPrimary : ctx.Theme.TextDisabled,
            UiFont.Small,
            TextAnchor.MiddleLeft);

        if (layout.SourceBandHeight > 0f)
        {
            UsKernelDraw.Label(
                new Rect(rect.x + LeftPadding, nameY + layout.NameBandHeight + layout.SourceGap, layout.NameBandWidth, layout.SourceBandHeight),
                MoodSourceText(ctx, row),
                ctx.Theme,
                submittable ? ctx.Theme.TextSecondary : ctx.Theme.TextDisabled,
                UiFont.Tiny,
                TextAnchor.MiddleLeft);
        }

        if (layout.TargetBandHeight > 0f)
        {
            string target = MoodResetTargetText(ctx, row);
            if (target.Length > 0)
            {
                UsKernelDraw.Label(
                    new Rect(rect.x + LeftPadding,
                        nameY + layout.NameBandHeight + layout.SourceGap + layout.SourceBandHeight + layout.TargetGap,
                        layout.NameBandWidth,
                        layout.TargetBandHeight),
                    target,
                    ctx.Theme,
                    submittable ? ctx.Theme.TextSecondary : ctx.Theme.TextDisabled,
                    UiFont.Tiny,
                    TextAnchor.MiddleLeft);
            }
        }

        float headerLeftBottom = nameY + layout.NameBandHeight + layout.SourceGap + layout.SourceBandHeight
            + layout.TargetGap + layout.TargetBandHeight;
        if (layout.HeaderInline)
        {
            DrawMoodResetButtons(
                new Rect(
                    rect.x + LeftPadding + layout.ParameterWidth - layout.ResetWidth,
                    rect.y + (layout.HeaderHeight - layout.ResetHeight) * 0.5f,
                    layout.ResetWidth,
                    layout.ResetHeight),
                row,
                ctx,
                submittable);
        }
        else
        {
            DrawMoodResetButtons(
                new Rect(rect.x + LeftPadding, headerLeftBottom + RowGap, layout.ParameterWidth, layout.ResetHeight),
                row,
                ctx,
                submittable);
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
                ctx,
                submittable);
        }
    }

    /// <summary>Mood display name through the Host translation seam. Keyed (US.Mood.Good and friends), so
    /// a mood is never rendered from a hard-coded language literal or from a debug name.</summary>
    private static string MoodName(UiWidgetContext ctx, MoodTuningRowView row)
    {
        return UsKernelDraw.Keyed(ctx, "US.Mood." + row.Mood);
    }

    // Centre the complete text stack, including its optional reset target, above the parameters.
    private static float MoodHeaderLeftTopFor(MoodRowsLayout layout)
    {
        return layout.HeaderInline
            ? (layout.HeaderHeight - layout.NameBandHeight - layout.SourceGap - layout.SourceBandHeight
                - layout.TargetGap - layout.TargetBandHeight) * 0.5f
            : 0f;
    }

    /// <summary>
    /// V3 P2 (corrected in task-18): the read-only provenance readout of one mood row, PER FACTOR. Each
    /// factor names the layer that actually supplies its effective value ("Global"/"Race"/"Xenotype"), or the
    /// default word when no layer supplies it. Only existing keys plus that one additive key are used.
    /// <para><b>Why the preset name is not here:</b> the persisted <c>sourcePresetDefName</c> is the
    /// reset-to-preset ANCHOR - it survives a clear (F-P) - so it cannot prove where the shown numbers came
    /// from. Printing it behind an arrow made a cleared or mixed row look as if it inherited from that preset.
    /// The preset is expressed ONLY by the reset-to-preset control, which is what it actually targets.</para>
    /// </summary>
    private static string MoodSourceText(UiWidgetContext ctx, MoodTuningRowView row)
    {
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < MoodParameterFactors.Length; i++)
        {
            SqueakMoodFactor factor = MoodParameterFactors[i];
            int supplying = row.SourceLayerFor(factor);
            string source = supplying >= 0 && supplying < LayerKeys.Length
                ? UsKernelDraw.Keyed(ctx, LayerKeys[supplying])
                : UsKernelDraw.Keyed(ctx, DefaultSourceKey);
            if (i > 0) text.Append(" · ");
            text.Append(UsKernelDraw.Keyed(ctx, ParameterLabelKey(factor))).Append(' ').Append(source);
        }

        return text.ToString();
    }

    /// <summary>
    /// V3 task-18 (b): the reset-to-preset TARGET clause, SEPARATE from the provenance line and explicitly
    /// labelled with the existing "Reset to preset" phrase, so the anchor can never read as an origin. Empty
    /// when the row has no usable target (the view carries "" then), which keeps a missing preset from
    /// looking like a live one.
    /// </summary>
    private static string MoodResetTargetText(UiWidgetContext ctx, MoodTuningRowView row)
    {
        return row.ResetPresetTarget.Length == 0
            ? ""
            : UsKernelDraw.Keyed(ctx, ResetPresetKey) + ": " + row.ResetPresetTarget;
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

    private void DrawMoodResetButtons(Rect rect, MoodTuningRowView row, UiWidgetContext ctx, bool submittable)
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

        // XG1.1 (r2): with no target the two controls are inert - drawn in the unavailable ink in their own
        // bands, never hit-tested (no UiNative.Button call), so a click cannot consume the event or take the
        // hot control. Their own unavailable reason keys would be false here - a row can be Ready while the
        // LAYER has nothing to write to - so both hover states claim the mood area's own entry instead of
        // inventing a help item this step does not own.
        DrawMoodResetButton(
            defaultRect,
            ResetDefaultKey,
            submittable ? ResetDefaultHelpKey : MoodTuningAreaHelpKey,
            submittable ? ResetDefaultUnavailableHelpKey(row) : MoodTuningAreaHelpKey,
            submittable && row.DefaultReset == SqueakMoodResetDefaultState.Ready,
            !submittable,
            () => ctx.Bindings.Invoke("set-mood-tuning", new UsMoodWrite(row.Mood, SqueakMoodFactor.Clear, null)),
            ctx);

        DrawMoodResetButton(
            presetRect,
            ResetPresetKey,
            submittable ? ResetPresetHelpKey : MoodTuningAreaHelpKey,
            submittable ? ResetPresetUnavailableHelpKey(row) : MoodTuningAreaHelpKey,
            submittable && row.PresetReset == SqueakMoodResetPresetState.Ready,
            !submittable,
            () => ctx.Bindings.Invoke("reset-mood-to-preset", new UsMoodPresetReset(row.Mood)),
            ctx);
    }

    /// <summary>
    /// One reset control, neutral by design: "reset to default" restores inheritance, which is not a
    /// destructive action and therefore carries no danger semantics (07 §7 bans red for it). Unavailable
    /// means greyed out and inert, never invisible: the control is still drawn and hovering it claims the
    /// help entry that carries its reason sentence (the reason channel of this UI). Available controls
    /// claim the entry that explains the action they perform.
    /// <para>
    /// XG1.1 (r2) adds a second, stronger state: <paramref name="inert"/> is the empty-target block, where the
    /// control must not RESPOND at all - not even the hit test runs, so a click cannot consume the event or
    /// take the hot control. The older unavailable state (a Ready-less row while the layer IS writable) keeps
    /// its documented drawn-and-hit-testable behaviour; only the empty-target state is fully inert.
    /// </para>
    /// </summary>
    private static void DrawMoodResetButton(
        Rect rect,
        string labelKey,
        string enabledHelpKey,
        string unavailableHelpKey,
        bool enabled,
        bool inert,
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

        if (inert) return;
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
        UiWidgetContext ctx,
        bool submittable)
    {
        // XG1.1 (r2): with no target the value control is NOT an editing control. No UiNative.Slider, no
        // UiNative.NumberField and no SelectionButton call happens, so there is no draft, no focus, no drag and
        // no hot control to take - the four bands are drawn as static disabled representations in exactly the
        // rects the interactive path uses. The label ink is the same disabled ink either way.
        if (!submittable)
        {
            DrawDisabledMoodValue(labelRect, groupRect, buttonWidth, fieldWidth, sliderRect, labelKey, value, ctx);
            return;
        }

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
    /// XG1.1 (r2): the inert stand-in for one parameter block. Same bands (label, minus, number, plus, slider),
    /// same value text format as the real field (<c>0.###</c>), drawn with the plain surface/label vocabulary
    /// so nothing here can take a click, a drag or the hot control. The slider band gets a surface only: a Tiny
    /// line does not fit the 16px slider band, and inventing a shorter one would be a second text rule.
    /// </summary>
    private static void DrawDisabledMoodValue(
        Rect labelRect,
        Rect groupRect,
        float buttonWidth,
        float fieldWidth,
        Rect sliderRect,
        string labelKey,
        float value,
        UiWidgetContext ctx)
    {
        UsKernelDraw.Label(
            labelRect,
            UsKernelDraw.Keyed(ctx, labelKey),
            ctx.Theme,
            ctx.Theme.TextDisabled,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        Rect minusRect = new(groupRect.x, groupRect.y, buttonWidth, groupRect.height);
        Rect fieldRect = new(minusRect.xMax + MoodControlGap, groupRect.y, fieldWidth, groupRect.height);
        Rect plusRect = new(fieldRect.xMax + MoodControlGap, groupRect.y, buttonWidth, groupRect.height);

        DrawDisabledGlyphBand(minusRect, "−", ctx);
        DrawDisabledGlyphBand(plusRect, "+", ctx);

        UsKernelDraw.RowSurface(fieldRect, ctx.Theme, false, UsKernelDraw.RowRail.None);
        UsKernelDraw.Label(
            new Rect(fieldRect.x + 4f, fieldRect.y, Mathf.Max(1f, fieldRect.width - 8f), fieldRect.height),
            value.ToString("0.###", CultureInfo.InvariantCulture),
            ctx.Theme,
            ctx.Theme.TextDisabled,
            UiFont.Tiny,
            TextAnchor.MiddleLeft,
            singleLine: true);

        UsKernelDraw.RowSurface(sliderRect, ctx.Theme, false, UsKernelDraw.RowRail.None);
    }

    /// <summary>The inert minus/plus stand-in: the band surface plus the same glyph in the disabled ink.</summary>
    private static void DrawDisabledGlyphBand(Rect rect, string glyph, UiWidgetContext ctx)
    {
        UsKernelDraw.RowSurface(rect, ctx.Theme, false, UsKernelDraw.RowRail.None);
        UsKernelDraw.Label(
            new Rect(rect.x + 4f, rect.y, Mathf.Max(1f, rect.width - 8f), rect.height),
            glyph,
            ctx.Theme,
            ctx.Theme.TextDisabled,
            UiFont.Tiny,
            TextAnchor.MiddleCenter,
            singleLine: true);
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

        // V3 A2: the source/inheritance readout is a Tiny line under the mood name, laid out in the SAME
        // band the name uses (measured, never a fixed band), so it can wrap but never clip. It is always
        // drawn - one of the three states always applies - and it is what tells the player whether the
        // numbers below are this layer's or inherited.
        float sourceBand = 0f;
        foreach (MoodTuningRowView row in rows)
        {
            sourceBand = Mathf.Max(sourceBand, ctx.Metrics.MeasureText(MoodSourceText(ctx, row), UiFont.Tiny, Mathf.Max(1f, layout.NameBandWidth)));
        }

        layout.SourceBandHeight = rows.Count > 0 ? Mathf.Max(MoodSourceMinBand, sourceBand) : 0f;
        layout.SourceGap = rows.Count > 0 ? MoodSourceGap : 0f;

        // V3 task-18 (b): the reset-target clause gets its OWN measured band so it reads as a separate,
        // labelled statement; rows with no usable target contribute 0 and the band disappears entirely.
        float targetBand = 0f;
        foreach (MoodTuningRowView row in rows)
        {
            string target = MoodResetTargetText(ctx, row);
            if (target.Length == 0) continue;
            targetBand = Mathf.Max(targetBand, ctx.Metrics.MeasureText(target, UiFont.Tiny, Mathf.Max(1f, layout.NameBandWidth)));
        }

        layout.TargetBandHeight = targetBand > 0f ? Mathf.Max(MoodSourceMinBand, targetBand) : 0f;
        layout.TargetGap = targetBand > 0f ? MoodSourceGap : 0f;
        float headerLeftHeight = layout.NameBandHeight + layout.SourceGap + layout.SourceBandHeight
            + layout.TargetGap + layout.TargetBandHeight;
        layout.HeaderHeight = layout.HeaderInline
            ? Mathf.Max(ButtonHeight, headerLeftHeight)
            : headerLeftHeight + RowGap + layout.ResetHeight;

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
        /// <summary>V3 A2: height of the mood source/inheritance readout line under the name.</summary>
        public float SourceBandHeight;
        public float SourceGap;
        /// <summary>V3 task-18 (b): the separate, labelled reset-to-preset target line (0 = none).</summary>
        public float TargetBandHeight;
        public float TargetGap;
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

    private float ScopeRowHeightFor(float rowWidth, ActionScopeRowView row, UiWidgetContext ctx)
    {
        return ScopeRowLayoutFor(rowWidth, UsKernelDraw.Keyed(ctx, DefinitionFor(row.Action).DisplayKey), HintTextFor(ctx, row), ctx.Metrics).RowHeight;
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
    /// SA1.2: the rows of one group in the fixed display order. A row whose action is not in the order
    /// table (a future built-in nobody added there) is appended in list order rather than dropped: no
    /// row may vanish from the editor because a presentation table lagged behind the model. Lives on
    /// the widget, not on ActionScopeRules: the rules file is compiled into the zero-Verse gate and
    /// must not name a view-row type.
    /// </summary>
    private static List<ActionScopeRowView> OrderedRows(ActionScopeGroup group, IReadOnlyList<ActionScopeRowView> rows)
    {
        IReadOnlyList<SqueakAction> order = group == ActionScopeGroup.PlayerTriggered
            ? ActionScopeRules.PlayerGroupOrder
            : ActionScopeRules.SystemGroupOrder;
        var result = new List<ActionScopeRowView>();
        for (int i = 0; i < order.Count; i++)
        {
            for (int r = 0; r < rows.Count; r++)
            {
                if (rows[r].Group == group && rows[r].Action == order[i]) result.Add(rows[r]);
            }
        }

        for (int r = 0; r < rows.Count; r++)
        {
            if (rows[r].Group != group) continue;
            bool known = false;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == rows[r].Action) { known = true; break; }
            }

            if (!known) result.Add(rows[r]);
        }

        return result;
    }

    /// <summary>
    /// SA1.2: the sub-heading exists exactly when BOTH halves of the pair are drawn. Measure and Draw
    /// call this on the same ordered list, so the band can never be reserved without being painted (or
    /// the reverse) - the class of defect the XG1 reason band comment already pins for that band. The
    /// scope CAPTION lookup itself moved to <see cref="ActionScopeRules.ScopeLabelKey"/>: the option's
    /// bound value stays the SqueakActionScope instance (R4-A / A2), only the text key is chosen there.
    /// </summary>
    private static bool HasDraftPair(List<ActionScopeRowView> playerRows)
    {
        bool draft = false;
        bool undraft = false;
        for (int i = 0; i < playerRows.Count; i++)
        {
            if (playerRows[i].Action == SqueakAction.Draft) draft = true;
            else if (playerRows[i].Action == SqueakAction.Undraft) undraft = true;
        }

        return draft && undraft;
    }
}
