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
        // VF1定稿: the area switch, the selected-action editor and the fallback table editor all
        // bind through this widget - a missing key must fail at creation, not half-draw a tab.
        bindings.ValidateValue<int>("tuning-area", elementPath);
        bindings.ValidateValue<string>("tuning-selected-action", elementPath);
        bindings.ValidateValue<IReadOnlyList<FallbackRaceView>>("fallback-races", elementPath);
        bindings.ValidateValue<string>("fallback-selected-race", elementPath);
        bindings.ValidateValue<IReadOnlyList<FallbackEntryView>>("fallback-entries", elementPath);
        bindings.ValidateValue<string>("fallback-selected-entry", elementPath);
        bindings.ValidateValue<string>("fallback-new-race-query", elementPath);
        bindings.ValidateValue<string>("fallback-sound-query", elementPath);
        bindings.ValidateOptions<FilterOptionView>("fallback-sound-options", elementPath);
        bindings.ValidateOptions<FilterOptionView>("fallback-candidate-options", elementPath);
        bindings.ValidateAction<UsActionTuningWrite>("set-action-tuning", elementPath);
        bindings.ValidateAction<string>("reset-action-to-preset", elementPath);
        bindings.ValidateAction<UsFallbackSelection>("set-fallback-selection", elementPath);
        bindings.ValidateAction<UsFallbackEntryWrite>("set-fallback-entry", elementPath);
        bindings.ValidateAction<string>("create-fallback-table", elementPath);
        bindings.ValidateCommand("restore-fallback-default", elementPath);
        bindings.ValidateAction<string>("delete-fallback-table", elementPath);
        // US-ESC1: the manifest names BOTH tuning keys - cancel-tuning-target on this element and
        // cancel-tuning-context on the container that wraps it (review1 observation 1). Validating both
        // here keeps a typo of either layer a creation-time failure (FL's walk skips unregistered keys
        // instead of throwing, and the composite is the owner of that branch of the tree).
        bindings.ValidateCommand("cancel-tuning-context", elementPath);
        // US-ESC1: this element carries the manifest's CancelBind="cancel-tuning-target"; the key must
        // exist at CREATION, or the tree layer would silently never answer (FL's walk skips unregistered
        // keys instead of throwing - which is why an unvalidated typo needs this guard, not luck).
        bindings.ValidateCommand("cancel-tuning-target", elementPath);
        // US-RESET1: the three restore keys this composite invokes (row direct, the two area
        // confirmations' staged actions). Same creation-time rule as the cancel keys above: an
        // unregistered typo must fail at once, not half-draw a button whose press answers nothing.
        bindings.ValidateCommand("reset-action-row", elementPath);
        bindings.ValidateCommand("reset-action-area", elementPath);
        bindings.ValidateCommand("reset-mood-area", elementPath);

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
        int area = ctx.Bindings.TryGet("tuning-area", out int a) ? a : 0;

        float width = BodyWidth(ctx);
        // F06 (US-UI1): the area decides what is PRESENTED. The layer/domain context rows and the
        // empty-target reason band belong to the ACTION and MOOD areas alone - the fallback area answers
        // to its own single race target (the editor's own dropdown) and must not be topped by the
        // tuning layer's context or its empty-xenotype notice (the XG1-accepted row order stays exactly
        // where it was in the two areas that own it). DrawContent walks the same sequence over the same
        // bindings, so Measure never reserves rows Draw will not paint.
        float bodyHeight = TopPadding;
        if (area != 2)
        {
            bodyHeight += LayerRowHeightFor(width) + RowGap;
            if (layer > 0) bodyHeight += DomainRowHeight + RowGap;
            // XG1.1: the empty-target reason band. Measure and Draw both derive it from the SAME predicate
            // and the same metrics seam, so the card can never reserve a band it does not paint (or paint
            // one it did not reserve) - the failure that would move every control below it.
            if (XenotypeTargetIsEmpty(layer, ctx)) bodyHeight += EmptyTargetBandHeight(ctx, width) + RowGap;
        }
        bodyHeight += AreaTabsHeight + RowGap;
        // VF1定稿: the page switches THREE internal areas and the body handles exactly ONE task type at a
        // time. This replaces the old long page that stacked actions AND moods together; the
        // "just add two steppers per row" shape was explicitly rejected, so the multipliers live
        // in the selected action's editor band below the list instead.
        if (area == 0)
        {
            bodyHeight += RowHeight + RowGap; // "Action rules" header
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

            bodyHeight += ActionEditorHeight(ctx, width) + RowGap;
        }
        else if (area == 1)
        {
            // V3 P1 deliberate rule survives the re-layout: the MOOD area is gated on the mood rows
            // ALONE, never on the scope list - Measure and Draw share this predicate.
            if (moodRows.Count > 0)
            {
                bodyHeight += RowHeight + RowGap; // "Mood Tuning" header
                MoodRowsLayout moodLayout = MoodRowsLayoutFor(width, ctx, moodRows);
                bodyHeight += moodRows.Count * (moodLayout.TotalHeight + RowGap);
            }
        }
        else
        {
            bodyHeight += FallbackEditorHeight(ctx, width) + RowGap;
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

        int area = ctx.Bindings.TryGet("tuning-area", out int a) ? a : 0;

        // F06 (US-UI1): only the ACTION and MOOD areas present their layer/domain context (MeasureBody
        // walks the identical sequence); the fallback area shows its own single race target through the
        // editor's dropdown. Within those areas the XG1-accepted order is unchanged.
        // XG1.1: the current XENOTYPE layer has no tunable target. The reason is stated ONCE, visibly, right
        // under the target row it is about; the controls below stay drawn and greyed but cannot submit. The
        // layer segment stays live on purpose: switching layer (or picking a target when one exists) is
        // the ONLY way out of this state, so blocking it would trap the player.
        bool submittable = !XenotypeTargetIsEmpty(layer, ctx);
        if (area != 2)
        {
            float layerRowHeight = LayerRowHeightFor(innerWidth);
            DrawLayerRow(new Rect(x, y, innerWidth, layerRowHeight), layer, ctx);
            y += layerRowHeight + RowGap;

            if (layer > 0)
            {
                DrawDomainRow(new Rect(x, y, innerWidth, DomainRowHeight), ctx);
                y += DomainRowHeight + RowGap;
            }

            if (!submittable)
            {
                float reasonBand = EmptyTargetBandHeight(ctx, innerWidth);
                UsKernelDraw.Label(
                    new Rect(x + LeftPadding, y, Mathf.Max(1f, innerWidth - LeftPadding), reasonBand),
                    EmptyTargetText(ctx),
                    ctx.Theme, ctx.Theme.TextSecondary,
                    UiFont.Tiny, TextAnchor.MiddleLeft);
                y += reasonBand + RowGap;
            }
        }

        DrawAreaTabs(new Rect(x, y, innerWidth, AreaTabsHeight), area, ctx);
        y += AreaTabsHeight + RowGap;

        if (area == 0)
        {
            // US-RESET1: the action-area batch restore sits on the area's own header line. The
            // button is drawn only while the CURRENT layer identity is valid (the effect command's
            // CanExecute is the single answer) - an XG1 empty target never offers a restore that
            // could silently fall back to Global. The question names the exact layer/domain; the
            // staged action runs the effect key through the same bindings.
            bool canAreaReset = ctx.Bindings.CanExecute("reset-action-area");
            float areaResetWidth = 132f; // distinct from the measured scope-trigger band widths
            UsKernelDraw.Label(
                new Rect(x, y, Math.Max(1f, innerWidth - (canAreaReset ? areaResetWidth + RowGap : 0f)), RowHeight),
                UsKernelDraw.Keyed(ctx, ActionScopeHeaderKey),
                ctx.Theme, ctx.Theme.TextPrimary, UiFont.Small,
                TextAnchor.MiddleLeft);
            if (canAreaReset
                && UsKernelDraw.SelectionButton(
                    new Rect(x + innerWidth - areaResetWidth, y, areaResetWidth, RowHeight), ctx,
                    UsKernelDraw.Keyed(ctx, "US.Reset.Area"), ctx.Theme, false))
            {
                // review1 item 3: the question names the layer/domain read at OPEN time, so the
                // answer must act on exactly that identity. The effect command reads the page state
                // at run time; the staged action therefore compares the CURRENT identity token with
                // the captured one and refuses on drift (a late read of a different valid layer/
                // domain restoring it is the failure the contract names). The modal absorb is not
                // treated as an identity guarantee.
                IUiBindings editorBindings = ctx.Bindings;
                string captured = TuningIdentityToken(editorBindings);
                UsConfirmWindow.Open(
                    UsKernelDraw.Keyed(ctx, "US.Reset.Area"),
                    UsKernelDraw.Keyed(ctx, "US.Reset.Area.Action.Confirm") + " " + AreaResetScopeText(layer, ctx),
                    UsKernelDraw.Keyed(ctx, "US.Reset.Confirm"),
                    () =>
                    {
                        if (string.Equals(TuningIdentityToken(editorBindings), captured, StringComparison.Ordinal))
                        {
                            editorBindings.TryInvokeCommand("reset-action-area");
                        }
                    });
            }
            y += RowHeight + RowGap;

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
                    ctx.Theme, ctx.Theme.TextSecondary, UiFont.Tiny,
                    TextAnchor.MiddleLeft);
                y += RowHeight + RowGap;

                if (targetGroup == ActionScopeGroup.PlayerTriggered && HasDraftPair(groupRows))
                {
                    UsKernelDraw.Label(
                        new Rect(x + 6f, y, Math.Max(1f, innerWidth - 6f), DraftHeadingHeight),
                        UsKernelDraw.Keyed(ctx, DraftUndraftKey),
                        ctx.Theme, ctx.Theme.AccentGold, UiFont.Tiny,
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

            y = DrawActionEditor(y, x, innerWidth, scopeRows, ctx, submittable);
        }
        else if (area == 1)
        {
            if (moodRows.Count > 0)
            {
                // US-RESET1: same header-line rule as the action area - the button exists only
                // while the current layer identity is valid, and the question names it.
                bool canMoodReset = ctx.Bindings.CanExecute("reset-mood-area");
                float moodResetWidth = 132f; // same distinct width as the action area's entry
                UsKernelDraw.Label(
                    new Rect(x, y, Math.Max(1f, innerWidth - (canMoodReset ? moodResetWidth + RowGap : 0f)), RowHeight),
                    UsKernelDraw.Keyed(ctx, MoodTuningHeaderKey),
                    ctx.Theme, ctx.Theme.TextPrimary, UiFont.Small,
                    TextAnchor.MiddleLeft);
                if (canMoodReset
                    && UsKernelDraw.SelectionButton(
                        new Rect(x + innerWidth - moodResetWidth, y, moodResetWidth, RowHeight), ctx,
                        UsKernelDraw.Keyed(ctx, "US.Reset.Area"), ctx.Theme, false))
                {
                    // review1 item 3: same captured-identity rule as the action area.
                    IUiBindings editorBindings = ctx.Bindings;
                    string captured = TuningIdentityToken(editorBindings);
                    UsConfirmWindow.Open(
                        UsKernelDraw.Keyed(ctx, "US.Reset.Area"),
                        UsKernelDraw.Keyed(ctx, "US.Reset.Area.Mood.Confirm") + " " + AreaResetScopeText(layer, ctx),
                        UsKernelDraw.Keyed(ctx, "US.Reset.Confirm"),
                        () =>
                        {
                            if (string.Equals(TuningIdentityToken(editorBindings), captured, StringComparison.Ordinal))
                            {
                                editorBindings.TryInvokeCommand("reset-mood-area");
                            }
                        });
                }
                y += RowHeight + RowGap;

                MoodRowsLayout moodLayout = MoodRowsLayoutFor(innerWidth, ctx, moodRows);
                foreach (MoodTuningRowView mood in moodRows)
                {
                    DrawMoodRow(new Rect(x, y, innerWidth, moodLayout.TotalHeight), mood, moodLayout, ctx, layer, submittable);
                    y += moodLayout.TotalHeight + RowGap;
                }
            }
        }
        else
        {
            DrawFallbackEditor(y, x, innerWidth, ctx);
        }
    }

    // -----------------------------------------------------------------------------------------
    // VF1定稿: area tabs, the selected-action editor band, and the final-fallback table editor
    // -----------------------------------------------------------------------------------------

    private const float AreaTabsHeight = 26f;
    private const float MultiplierStep = 0.05f;
    // A value no SoundDef can have: the display-only pair that lets an UNSET entry read as
    // "unset" instead of masquerading as the explicit no-sound marker.
    private const string DisplayOnlyCurrentValue = "\u0000vf1-display-only";

    private static readonly string[] AreaTabKeys =
    {
        "US.Tuning.Area.Actions", "US.Tuning.Area.Mood", "US.Tuning.Area.Fallback",
    };

    private void DrawAreaTabs(Rect rect, int area, UiWidgetContext ctx)
    {
        float tabWidth = Math.Max(1f, (rect.width - RowGap * 2f) / 3f);
        for (int i = 0; i < 3; i++)
        {
            var tab = new Rect(rect.x + i * (tabWidth + RowGap), rect.y, tabWidth, AreaTabsHeight);
            if (UsKernelDraw.SelectionButton(tab, ctx, UsKernelDraw.Keyed(ctx, AreaTabKeys[i]), ctx.Theme, area == i))
            {
                ctx.Bindings.Set("tuning-area", i);
            }
        }
    }

    private static ActionScopeRowView? SelectedAction(IReadOnlyList<ActionScopeRowView> rows, UiWidgetContext ctx)
    {
        string key = ctx.Bindings.TryGet("tuning-selected-action", out string s) ? s ?? "" : "";
        if (key.Length == 0) return null;
        foreach (ActionScopeRowView row in rows)
        {
            if (string.Equals(row.ActionKey, key, StringComparison.Ordinal)) return row;
        }

        return null;
    }
    /// <summary>US-RESET1: names the CURRENT layer/domain in the area-restore question (§4.3: the
    /// wording must state the scope). Layer 0 is the legitimate Global; layers 1/2 resolve their
    /// domain through the same option list the domain dropdown draws, falling back to the raw def
    /// names when the list has no entry (a race-only layer, or a target the catalog lost).</summary>
    private string AreaResetScopeText(int layer, UiWidgetContext ctx)
    {
        int index = layer < 0 ? 0 : layer >= LayerKeys.Length ? LayerKeys.Length - 1 : layer;
        string scope = UsKernelDraw.Keyed(ctx, LayerKeys[index]);
        if (layer == 0) return scope;
        string race = ctx.Bindings.TryGet("tuning-race", out string r) ? r : "";
        string xeno = ctx.Bindings.TryGet("tuning-xeno", out string x) ? x : "";
        IReadOnlyList<TuningDomainOptionView> domains = ctx.Bindings.TryGet("tuning-domains",
            out IReadOnlyList<TuningDomainOptionView> d) ? d : Array.Empty<TuningDomainOptionView>();
        foreach (TuningDomainOptionView domain in domains)
        {
            if (string.Equals(domain.RaceDefName, race, StringComparison.Ordinal)
                && string.Equals(domain.TargetDefName, xeno, StringComparison.Ordinal))
            {
                return scope + ": " + domain.DisplayName;
            }
        }
        return scope + ": " + (race.Length > 0 && xeno.Length > 0 ? race + "/" + xeno : race + xeno);
    }

    /// <summary>review1 item 3: the machine identity a staged area answer must still match at run
    /// time - the same three page-state halves the question text was composed from, read through
    /// the page's own value bindings so the comparison sees exactly what the effect command will.</summary>
    private static string TuningIdentityToken(IUiBindings bindings)
    {
        int layer = bindings.TryGet("tuning-layer", out int l) ? l : 0;
        string race = bindings.TryGet("tuning-race", out string r) ? r ?? "" : "";
        string xeno = bindings.TryGet("tuning-xeno", out string x) ? x ?? "" : "";
        return layer + "|" + race + "|" + xeno;
    }

    private float ActionEditorHeight(UiWidgetContext ctx, float width)
    {
        IReadOnlyList<ActionScopeRowView> rows = ctx.Bindings.TryGet("action-scopes", out IReadOnlyList<ActionScopeRowView> r)
            ? r : Array.Empty<ActionScopeRowView>();
        ActionScopeRowView? selected = SelectedAction(rows, ctx);
        float height = RowHeight + RowGap + (selected.HasValue ? 2f * (RowHeight + RowGap) : RowHeight);
        // F07 (US-UI1): the measured reset slot moves to its own line when the remaining band is narrower
        // than its content - and the MEASURE pass adds exactly the same extra line Draw will paint, using
        // the same helper. Both multiplier lines resolve the same row's anchor state, so one decision
        // applies twice.
        if (selected.HasValue)
        {
            int layer = ctx.Bindings.TryGet("tuning-layer", out int l) ? l : 0;
            bool asButton = !XenotypeTargetIsEmpty(layer, ctx) && selected.Value.PresetResetReady;
            if (MultiplierResetWraps(ctx, width, MultiplierResetText(ctx, selected.Value, asButton)))
            {
                height += 2f * (RowHeight + RowGap);
            }
        }

        return height;
    }

    /// <summary>F07: the smallest reset-slot band the row accepts before the control drops to its own
    /// line - the same floor discipline as <see cref="MoodResetWidthMin"/>.</summary>
    private const float MultiplierResetWidthMin = 52f;

    /// <summary>F07: the fixed lead the multiplier line spends before the reset slot - name 110 + value
    /// 150 bands (x+270), the two 30px steppers with their gaps, and the 64px restore-inherit slot that is
    /// reserved whether or not the row owns a value (270 + 34 + 38 + 68).</summary>
    private const float MultiplierFixedLeadWidth = 410f;

    /// <summary>
    /// F07: the ONE text the reset slot answers with in both passes. A ready row gets the actionable label
    /// alone (button or - when the line cannot submit - the same words as a plain explanation); an
    /// anchored-but-unavailable or anchorless row states its reason, with the target sentence only while
    /// there is a target to name.
    /// </summary>
    private static string MultiplierResetText(UiWidgetContext ctx, ActionScopeRowView row, bool asButton)
    {
        string resetKey = row.PresetResetReady
            ? "US.Tuning.ResetToPreset"
            : row.HasPresetAnchor ? "US.Tuning.ResetPresetUnavailable" : "US.Tuning.ResetPresetNoAnchor";
        string text = UsKernelDraw.Keyed(ctx, resetKey);
        if (asButton || row.PresetResetReady || string.IsNullOrEmpty(row.ResetPresetTarget)) return text;
        return text + " " + row.ResetPresetTarget;
    }

    /// <summary>F07: does the resolved reset text need its own line at this body width? Measure and Draw
    /// call THIS and only this, so the reserved height and the painted line cannot disagree.</summary>
    private static bool MultiplierResetWraps(UiWidgetContext ctx, float width, string resetText)
    {
        float needed = Mathf.Max(MultiplierResetWidthMin,
            ctx.Metrics.MeasureWidth(resetText, UiFont.Tiny) + UsKernelDraw.SelectionButtonLabelInset * 2f);
        return width - MultiplierFixedLeadWidth < needed;
    }

    /// <summary>The editor band for the selected action: effective value + per-factor source + this
    /// layer's own multiplier with a stepper, single-field Clear (restore inheritance) and the
    /// reset-to-preset with its Ready/Unavailable answer. The probability line states the runtime
    /// rule instead of hiding it: the multiplier is open-ended, the FINAL probability still clamps.</summary>
    private float DrawActionEditor(float y, float x, float width, IReadOnlyList<ActionScopeRowView> rows, UiWidgetContext ctx, bool submittable)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        foreach (ActionScopeRowView row in rows)
        {
            pairs.Add(new KeyValuePair<string, string>(row.DisplayName, row.ActionKey));
        }

        string current = ctx.Bindings.TryGet("tuning-selected-action", out string s) ? s ?? "" : "";
        // US-RESET1: the row's own restore-to-DEFAULT (all three overrides + the preset anchor of this
        // exact layer+domain row). It is a SIMPLE restore - executed directly, no confirmation - and it
        // stays a THIRD entry beside the existing restore-inheritance and reset-to-preset semantics,
        // never merged with them. The button only exists while the effect command can answer (a row
        // is selected and the layer identity is valid); the dropdown keeps the full width otherwise.
        bool canRowReset = submittable && ctx.Bindings.CanExecute("reset-action-row");
        float rowResetWidth = 104f; // distinct from ButtonWidth (96) and the stepper/inherit bands
        float dropdownWidth = canRowReset ? Math.Max(1f, width - rowResetWidth - RowGap) : width;
        UsKernelDraw.Dropdown(
            new Rect(x, y, dropdownWidth, RowHeight), "scope-tree-action-select", ctx, current, pairs,
            selected => ctx.Bindings.Set("tuning-selected-action", selected));
        if (canRowReset
            && UsKernelDraw.SelectionButton(
                new Rect(x + dropdownWidth + RowGap, y, rowResetWidth, RowHeight), ctx,
                UsKernelDraw.Keyed(ctx, "US.Reset.Row"), ctx.Theme, false))
        {
            ctx.Bindings.Invoke("reset-action-row");
        }
        y += RowHeight + RowGap;

        ActionScopeRowView? rowView = SelectedAction(rows, ctx);
        if (!rowView.HasValue) return y - RowGap;
        ActionScopeRowView selected = rowView.Value;

        y = DrawMultiplierLine(y, x, width, selected.ActionKey, intervalField: true, selected, ctx, submittable);
        y = DrawMultiplierLine(y, x, width, selected.ActionKey, intervalField: false, selected, ctx, submittable);
        return y - RowGap;
    }

    private float DrawMultiplierLine(
        float y, float x, float width, string actionKey, bool intervalField, ActionScopeRowView row,
        UiWidgetContext ctx, bool submittable)
    {
        float own = intervalField ? row.OwnInterval : row.OwnProbability;
        bool hasOwn = intervalField ? row.HasOwnInterval : row.HasOwnProbability;
        float effective = intervalField ? row.EffectiveInterval : row.EffectiveProbability;
        int source = intervalField ? row.IntervalSourceLayer : row.ProbabilitySourceLayer;
        string label = UsKernelDraw.Keyed(ctx, intervalField ? "US.Tuning.Interval" : "US.Tuning.Probability");
        string valueText = "×" + effective.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + "  (" + UsKernelDraw.Keyed(ctx, source switch
            {
                0 => "US.VF1.Source.Global",
                1 => "US.VF1.Source.Race",
                2 => "US.VF1.Source.Xenotype",
                _ => "US.VF1.Source.Default",
            }) + ")";
        UsKernelDraw.Label(new Rect(x, y, 110f, RowHeight), label, ctx.Theme, ctx.Theme.TextPrimary, UiFont.Tiny, TextAnchor.MiddleLeft);
        UsKernelDraw.Label(new Rect(x + 114f, y, 150f, RowHeight), valueText, ctx.Theme, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft);

        float bx = x + 270f;
        float buttonWidth = 30f;
        float display = hasOwn ? own : effective;
        if (submittable && UsKernelDraw.SelectionButton(new Rect(bx, y, buttonWidth, RowHeight), ctx, "-", ctx.Theme, false))
        {
            ctx.Bindings.Invoke("set-action-tuning", new UsActionTuningWrite(actionKey, intervalField, Math.Max(0f, display - MultiplierStep)));
        }
        bx += buttonWidth + 4f;
        if (submittable && UsKernelDraw.SelectionButton(new Rect(bx, y, buttonWidth, RowHeight), ctx, "+", ctx.Theme, false))
        {
            ctx.Bindings.Invoke("set-action-tuning", new UsActionTuningWrite(actionKey, intervalField, display + MultiplierStep));
        }
        bx += buttonWidth + 8f;
        if (submittable && hasOwn
            && UsKernelDraw.SelectionButton(new Rect(bx, y, 64f, RowHeight), ctx, UsKernelDraw.Keyed(ctx, "US.Tuning.RestoreInherit"), ctx.Theme, false))
        {
            ctx.Bindings.Invoke("set-action-tuning", new UsActionTuningWrite(actionKey, intervalField, null));
        }
        bx += 68f;

        // F07 (US-UI1) - the double-draw fix. The pre-fix shape keyed the drawn form off
        // SelectionButton's CLICK return value: a READY row drew the button, and because the button
        // returns true only on the frame it is clicked, every non-click frame fell into the else branch
        // and painted the explanation label OVER the button's own pixels. The drawn form now depends on
        // STATE alone (submittable && PresetResetReady => button; anything else => the explanation
        // label), and the slot's width comes from the measured content: when the band left of the fixed
        // lead cannot hold it, the whole slot drops to its own line below, at full width. ActionEditorHeight
        // calls the same wrap decision, so measure and draw agree by construction, and the old fixed
        // 96px box (the narrow-width clip risk) is gone.
        bool asButton = submittable && row.PresetResetReady;
        string resetText = MultiplierResetText(ctx, row, asButton);
        bool wraps = MultiplierResetWraps(ctx, width, resetText);
        float resetX = wraps ? x : bx;
        float resetY = wraps ? y + RowHeight + RowGap : y;
        float resetW = wraps ? Math.Max(1f, width) : Math.Max(1f, width - (bx - x));
        if (asButton)
        {
            if (UsKernelDraw.SelectionButton(new Rect(resetX, resetY, resetW, RowHeight), ctx, resetText, ctx.Theme, false))
            {
                ctx.Bindings.Invoke("reset-action-to-preset", actionKey);
            }
        }
        else
        {
            UsKernelDraw.Label(new Rect(resetX, resetY, resetW, RowHeight), resetText,
                ctx.Theme, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft);
        }

        return wraps ? y + 2f * (RowHeight + RowGap) : y + RowHeight + RowGap;
    }

    private float FallbackEditorHeight(UiWidgetContext ctx, float width)
    {
        // Mirrors DrawFallbackEditor line for line (r5): race line + status line + create line are
        // drawn for every shape; the entry list, the SOUND QUERY field, the picker row and the
        // button row only when a race is selected. A measure that forgets a drawn row clips it.
        float height = 3 * (RowHeight + RowGap);
        string selectedRace = ctx.Bindings.TryGet("fallback-selected-race", out string sr) ? sr ?? "" : "";
        if (selectedRace.Length > 0)
        {
            height += UniversalSqueaker.Kernel.BuiltInActionKeys.All.Count * RowHeight + RowGap;
            height += 2 * (RowHeight + RowGap) + RowHeight + RowGap; // query + picker + buttons
        }

        return height;
    }

    /// <summary>The VF1 editor: race picker over the supported tables (maintainer ∪ player), the
    /// closed 17-key entry list with per-row state, a query-filtered sound picker labelled with the
    /// current production availability, create-from-loaded-pawn-races, restore-default and deletion
    /// through ONE ordinary UiKit confirmation window.</summary>
    private void DrawFallbackEditor(float y, float x, float width, UiWidgetContext ctx)
    {
        IReadOnlyList<FallbackRaceView> races = ctx.Bindings.TryGet("fallback-races", out IReadOnlyList<FallbackRaceView> rs)
            ? rs : Array.Empty<FallbackRaceView>();
        string selectedRace = ctx.Bindings.TryGet("fallback-selected-race", out string sr) ? sr ?? "" : "";

        var racePairs = new List<KeyValuePair<string, string>>();
        foreach (FallbackRaceView race in races)
        {
            racePairs.Add(new KeyValuePair<string, string>(
                race.Label + "  " + race.DefName + (race.IsPlayerTable ? "  (*)" : ""), race.DefName));
        }

        UsKernelDraw.Label(new Rect(x, y, 60f, RowHeight), UsKernelDraw.Keyed(ctx, "US.VF1.Race"),
            ctx.Theme, ctx.Theme.TextPrimary, UiFont.Tiny, TextAnchor.MiddleLeft);
        UsKernelDraw.Dropdown(new Rect(x + 64f, y, Math.Max(1f, width - 64f), RowHeight), "vf1-race", ctx, selectedRace, racePairs,
            selected => ctx.Bindings.Invoke("set-fallback-selection", new UsFallbackSelection(selected, null)));
        y += RowHeight + RowGap;

        string status = ctx.Bindings.TryGet("fallback-status", out string st) ? st ?? "" : "";
        UsKernelDraw.Label(new Rect(x, y, width, RowHeight),
            status.Length > 0 ? UsKernelDraw.Keyed(ctx, status)
            : races.Count == 0 ? UsKernelDraw.Keyed(ctx, "US.VF1.NoTable") : "",
            ctx.Theme, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft);
        y += RowHeight + RowGap;

        // Create row: query + candidate dropdown (candidates = loaded pawn races, so a race with no
        // VoicePack at all is a legal new table).
        UiValueState queryState = ctx.Session.GetOrCreateValueState("vf1-new-race-query");
        string query = ctx.Bindings.TryGet("fallback-new-race-query", out string q) ? q ?? "" : "";
        if (!queryState.Focused) queryState.EditText = query;
        UsKernelDraw.Label(new Rect(x, y, 60f, RowHeight), UsKernelDraw.Keyed(ctx, "US.VF1.Create"),
            ctx.Theme, ctx.Theme.TextPrimary, UiFont.Tiny, TextAnchor.MiddleLeft);
        string typed = UiNative.TextField(new Rect(x + 64f, y, 120f, RowHeight), queryState.EditText);
        if (typed != queryState.EditText)
        {
            queryState.EditText = typed;
            ctx.Bindings.Set("fallback-new-race-query", typed);
        }

        IReadOnlyList<FilterOptionView> candidates = ctx.Bindings.GetOptions<FilterOptionView>("fallback-candidate-options");
        var candidatePairs = new List<KeyValuePair<string, string>>();
        foreach (FilterOptionView option in candidates) candidatePairs.Add(new KeyValuePair<string, string>(option.DisplayName, option.Value));
        UsKernelDraw.Dropdown(new Rect(x + 190f, y, Math.Max(1f, width - 190f), RowHeight), "vf1-candidate", ctx, "", candidatePairs,
            selected => ctx.Bindings.Invoke("create-fallback-table", selected));
        y += RowHeight + RowGap;

        if (races.Count == 0 || selectedRace.Length == 0) return;

        IReadOnlyList<FallbackEntryView> entries = ctx.Bindings.TryGet("fallback-entries", out IReadOnlyList<FallbackEntryView> en)
            ? en : Array.Empty<FallbackEntryView>();
        string selectedEntry = ctx.Bindings.TryGet("fallback-selected-entry", out string fs) ? fs ?? "" : "";
        foreach (FallbackEntryView entry in entries)
        {
            var rowRect = new Rect(x, y, width, RowHeight - 2f);
            bool isSel = string.Equals(entry.ActionKey, selectedEntry, StringComparison.Ordinal);
            string stateWord = UsKernelDraw.Keyed(ctx, entry.State switch
            {
                FallbackEntryView.PlayerOverride => "US.VF1.State.Override",
                FallbackEntryView.Maintainer => "US.VF1.State.Maintainer",
                FallbackEntryView.NoSound => "US.VF1.State.NoSound",
                _ => "US.VF1.State.Unset",
            });
            string text = entry.ActionLabel + "   " + (entry.SoundLabel.Length > 0 ? entry.SoundLabel : "-") + "   " + stateWord;
            if (UsKernelDraw.SelectionButton(rowRect, ctx, text, ctx.Theme, isSel))
            {
                ctx.Bindings.Invoke("set-fallback-selection", new UsFallbackSelection(null, entry.ActionKey));
            }

            y += RowHeight;
        }

        y += RowGap;

        if (selectedEntry.Length > 0)
        {
            // The sound query is the user's REAL search input (r5 wiring gap): the candidate list is
            // display-capped at 40 AFTER filtering, so typing a name or defName is how any legal Core
            // sound becomes reachable. The value binding already existed on the Host; this is the
            // control, with the same focus seam as the race-query field.
            UiValueState soundQueryState = ctx.Session.GetOrCreateValueState("vf1-sound-query");
            string soundQuery = ctx.Bindings.TryGet("fallback-sound-query", out string sq) ? sq ?? "" : "";
            if (!soundQueryState.Focused) soundQueryState.EditText = soundQuery;
            UsKernelDraw.Label(new Rect(x, y, 60f, RowHeight), UsKernelDraw.Keyed(ctx, "US.VF1.SoundSearch"),
                ctx.Theme, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft);
            string typedSoundQuery = UiNative.TextField(new Rect(x + 64f, y, Math.Max(1f, width - 64f), RowHeight), soundQueryState.EditText);
            if (typedSoundQuery != soundQueryState.EditText)
            {
                soundQueryState.EditText = typedSoundQuery;
                ctx.Bindings.Set("fallback-sound-query", typedSoundQuery);
            }

            y += RowHeight + RowGap;

            // Three distinct answers (r5): pick a sound = override; "Unset (no sound)" = the EXPLICIT
            // silent marker (the key stays present with the delete value); "Restore inheritance"
            // removes the key so the entry follows future shipped updates again. Explicit silence
            // and inheritance are different states and get different controls.
            FallbackEntryView selectedRow = default;
            foreach (FallbackEntryView entry in entries)
            {
                if (string.Equals(entry.ActionKey, selectedEntry, StringComparison.Ordinal)) { selectedRow = entry; break; }
            }

            IReadOnlyList<FilterOptionView> sounds = ctx.Bindings.GetOptions<FilterOptionView>("fallback-sound-options");
            var soundPairs = new List<KeyValuePair<string, string>>();
            foreach (FilterOptionView option in sounds) soundPairs.Add(new KeyValuePair<string, string>(option.DisplayName, option.Value));
            soundPairs.Add(new KeyValuePair<string, string>(UsKernelDraw.Keyed(ctx, "US.VF1.Unset"), ""));
            // The CURRENT reading is unambiguous per state: an override/shipped sound shows its own
            // defName (so the label resolves), the explicit marker shows "Unset (no sound)", and an
            // unset entry gets a display-only pair so it never masquerades as the marker.
            string currentSound = selectedRow.State switch
            {
                FallbackEntryView.Unset => DisplayOnlyCurrentValue,
                FallbackEntryView.NoSound => "",
                _ => selectedRow.SoundDefName ?? "",
            };
            if (selectedRow.State == FallbackEntryView.Unset)
                soundPairs.Add(new KeyValuePair<string, string>(UsKernelDraw.Keyed(ctx, "US.VF1.State.Unset"), DisplayOnlyCurrentValue));
            UsKernelDraw.Dropdown(new Rect(x, y, Math.Max(1f, width - 150f - RowGap), RowHeight), "vf1-sound", ctx, currentSound, soundPairs,
                selected =>
                {
                    if (selected == DisplayOnlyCurrentValue) return; // re-picking the display-only unset row writes nothing
                    ctx.Bindings.Invoke("set-fallback-entry", new UsFallbackEntryWrite(selectedEntry, selected));
                });
            if (UsKernelDraw.SelectionButton(new Rect(x + Math.Max(1f, width - 146f), y, 146f, RowHeight), ctx,
                    UsKernelDraw.Keyed(ctx, "US.VF1.RestoreInherit"), ctx.Theme, false))
            {
                ctx.Bindings.Invoke("set-fallback-entry", new UsFallbackEntryWrite(selectedEntry, null));
            }

            y += RowHeight + RowGap;
        }

        float half = Math.Max(1f, (width - RowGap) / 2f);
        if (UsKernelDraw.SelectionButton(new Rect(x, y, half, RowHeight), ctx, UsKernelDraw.Keyed(ctx, "US.VF1.RestoreDefault"), ctx.Theme, false))
        {
            ctx.Bindings.Invoke("restore-fallback-default");
        }

        // VF1 (r5): deletion asks ONCE through the ordinary UiKit confirmation window - not the
        // inline two-click arm, and not the Remix double-swap (that ceremony belongs to a mode
        // change across pages, not to deleting one's own table).
        if (UsKernelDraw.SelectionButton(new Rect(x + half + RowGap, y, half, RowHeight), ctx,
                UsKernelDraw.Keyed(ctx, "US.VF1.Delete"), ctx.Theme, false))
        {
            IUiBindings editorBindings = ctx.Bindings;
            string requestedRace = selectedRace;
            UsConfirmWindow.Open(
                UsKernelDraw.Keyed(ctx, "US.VF1.Delete"),
                UsKernelDraw.Keyed(ctx, "US.VF1.DeleteQuestion") + " " + selectedRace,
                UsKernelDraw.Keyed(ctx, "US.VF1.DeleteConfirm"),
                () => editorBindings.Invoke("delete-fallback-table", requestedRace));
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
