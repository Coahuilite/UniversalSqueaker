using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>Existing layered scope editor and current response explanation. Window navigation remains local.</summary>
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

    private const float RowGap = 2f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;
    private const float LeftPadding = 10f;
    private const float ButtonWidth = 96f;
    private const float ButtonHeight = 24f;
    // Keyed display text. Every bound value stays untouched: the tuning layer is the "tuning-layer"
    // int index, scope options carry the BehaviorScope value itself (R4-A / A2 - no string token),
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
    /// <summary>Action-scope dropdown option: "inherit from below". Per the term split this word belongs to
    /// the action side only; the mood row's controls use the two "reset" phrases below instead
    /// (one word, two meanings was the direct cause of mis-clicks).</summary>
    private const string AutoLabelKey = "US.Tuning.Auto";
    // Help entries claimed while hovering the two controls: enabled explains what the control does,
    // disabled explains why it is unavailable. The help panel is this UI's reason channel; the control
    // itself stays drawn and only greys out (unavailable is not invisible).
    // Item keys stay at three path segments (us/<section>/<item>): the UI-logic lane's dead-content scan
    // reads exactly three-segment literals as claims, and a four-segment key would be invisible to it.
    /// <summary>XG1.1 additive key: the xenotype tuning layer has no tunable target, so nothing in this card
    /// can be submitted until a target exists. Drawn as one sentence under the target row.</summary>
    private const string EmptyXenotypeTargetKey = "US.Tuning.EmptyXenotypeTarget";
    /// <summary>XG1.1: band floor for the reason sentence (one Tiny line), measured and drawn through the same
    /// metrics seam so the card grows by exactly the text it paints.</summary>
    private const float EmptyTargetMinBand = 14f;

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
    /// Both callers evaluate it AFTER the view-backed action-scopes read, which is what makes
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
        bindings.ValidateValue<string>("response-details", elementPath);
        bindings.ValidateValue<int>("tuning-layer", elementPath);
        bindings.ValidateValue<string>("tuning-race", elementPath);
        bindings.ValidateValue<string>("tuning-xeno", elementPath);
        bindings.ValidateValue<IReadOnlyList<TuningDomainOptionView>>("tuning-domains", elementPath);
        bindings.ValidateValue<IReadOnlyList<ActionScopeRowView>>("action-scopes", elementPath);
        bindings.ValidateAction<int>("set-tuning-layer", elementPath);
        bindings.ValidateAction<UsTuningDomainSelection>("set-tuning-domain", elementPath);
        bindings.ValidateAction<UsScopeWrite>("set-action-scope", elementPath);
        // VF1定稿: the area switch and the selected-action editor bind through this widget - a
        // missing key must fail at creation, not half-draw a tab. UIR-004 (WO-US-UI-REFACTOR-20261014):
        // the fallback table editor's keys moved to us/fallback-editor with the editor itself (the
        // Packs page); they stay validated - just by their new owner.
        bindings.ValidateValue<int>("tuning-area", elementPath);
        bindings.ValidateValue<string>("tuning-selected-action", elementPath);
        bindings.ValidateAction<string>("reset-action-to-preset", elementPath);
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
        int area = ctx.Bindings.TryGet("tuning-area", out int a) ? a : 0;

        float width = BodyWidth(ctx);
        // Two existing areas: permissions and current response details.
        // The fallback table editor moved to the Packs page as its own composite (us/fallback-editor),
        // so the third area and its F06 "area decides presentation" gate retire: the layer/domain
        // context rows and the empty-target reason band are always presented, exactly as they were in
        // the two areas that own it. The XG1-accepted row order is unchanged. DrawContent walks this
        // same sequence over the same bindings, so Measure never reserves rows Draw will not paint.
        float bodyHeight = TopPadding;
        bodyHeight += LayerRowHeightFor(width) + RowGap;
        if (layer > 0) bodyHeight += DomainRowHeight + RowGap;
        // XG1.1: the empty-target reason band. Measure and Draw both derive it from the SAME predicate
        // and the same metrics seam, so the card can never reserve a band it does not paint (or paint
        // one it did not reserve) - the failure that would move every control below it.
        if (XenotypeTargetIsEmpty(layer, ctx)) bodyHeight += EmptyTargetBandHeight(ctx, width) + RowGap;
        bodyHeight += AreaTabsHeight + RowGap;
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

            bodyHeight += RowHeight + RowGap + ResponseDetailsHeight(ctx, width) + RowGap;
        }
        else bodyHeight += ResponseDetailsHeight(ctx, width) + RowGap;
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

        float innerWidth = rect.width;
        float x = rect.x;
        float y = rect.y + TopPadding;

        int area = ctx.Bindings.TryGet("tuning-area", out int a) ? a : 0;


        // Both permission and response-detail areas retain the measured layer/domain context.
        // The separate fallback editor owns its own target; the XG1 order remains unchanged.
        // XG1.1: the current XENOTYPE layer has no tunable target. The reason is stated ONCE, visibly, right
        // under the target row it is about; the controls below stay drawn and greyed but cannot submit. The
        // layer segment stays live on purpose: switching layer (or picking a target when one exists) is
        // the ONLY way out of this state, so blocking it would trap the player.
        bool submittable = !XenotypeTargetIsEmpty(layer, ctx);
        // UIR-004: the area gate retired - with the fallback editor moved to the Packs page the
        // layer/domain rows and the empty-target band are presented for BOTH remaining areas.
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
                ctx.Theme, ctx.Theme[UiPaletteRole.SecondaryForeground],
                UiFont.Tiny, TextAnchor.MiddleLeft);
            y += reasonBand + RowGap;
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
                ctx.Theme, ctx.Theme[UiPaletteRole.Foreground], UiFont.Small,
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
                    ctx.Theme, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny,
                    TextAnchor.MiddleLeft);
                y += RowHeight + RowGap;

                if (targetGroup == ActionScopeGroup.PlayerTriggered && HasDraftPair(groupRows))
                {
                    UsKernelDraw.Label(
                        new Rect(x + 6f, y, Math.Max(1f, innerWidth - 6f), DraftHeadingHeight),
                        UsKernelDraw.Keyed(ctx, DraftUndraftKey),
                        ctx.Theme, ctx.Theme[UiPaletteRole.Accent], UiFont.Tiny,
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

            DrawActionRestore(y, x, innerWidth, scopeRows, ctx, submittable);
        }
        else DrawResponseDetails(new Rect(x, y, innerWidth, ResponseDetailsHeight(ctx, innerWidth)), ctx);
    }

    private static string ResponseDetailsText(UiWidgetContext ctx)
        => ctx.Bindings.TryGet("response-details", out string text) ? text : "";
    private static float ResponseDetailsHeight(UiWidgetContext ctx, float width)
    {
        return ctx.Metrics.MeasureText(ResponseDetailsText(ctx), UiFont.Small, Math.Max(1, width));
    }

    private static void DrawResponseDetails(Rect rect, UiWidgetContext ctx)
    {
        UsKernelDraw.HelpHover(rect, ctx, "us/scope-tree/response-details");
        UsKernelDraw.Label(rect, ResponseDetailsText(ctx), ctx, UiFont.Small);
    }

    // -----------------------------------------------------------------------------------------
    // VF1定稿: area tabs, the selected-action editor band, and the final-fallback table editor
    // -----------------------------------------------------------------------------------------

    private const float AreaTabsHeight = 26f;

    private static readonly string[] AreaTabKeys =
    {
        // UIR-004 (WO-US-UI-REFACTOR-20261014): the Fallback tab retired with the editor's move
        // to the Packs page; the tuning tree owns exactly the two rule areas it was named for.
        "US.Tuning.Area.Actions", "US.Tuning.Area.Mood",
    };

    private void DrawAreaTabs(Rect rect, int area, UiWidgetContext ctx)
    {
        float tabWidth = Math.Max(1f, (rect.width - RowGap * 1f) / 2f);
        for (int i = 0; i < 2; i++)
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



    /// <summary>Current action selection, exact row/template restore and measured response explanation.</summary>
    private float DrawActionRestore(float y, float x, float width, IReadOnlyList<ActionScopeRowView> rows, UiWidgetContext ctx, bool submittable)
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
        var selectedRow = SelectedAction(rows, ctx);
        bool canPresetReset = submittable && selectedRow?.PresetResetReady == true;
        float rowResetWidth = 104f; // distinct from ButtonWidth (96) and the stepper/inherit bands
        int resetCount = (canRowReset ? 1 : 0) + (canPresetReset ? 1 : 0);
        float dropdownWidth = Math.Max(1f, width - resetCount * (rowResetWidth + RowGap));
        UsKernelDraw.Dropdown(
            new Rect(x, y, dropdownWidth, RowHeight), "scope-tree-action-select", ctx, current, pairs,
            selected => ctx.Bindings.Set("tuning-selected-action", selected));
        if (canRowReset) UsKernelDraw.HelpHover(new Rect(x + dropdownWidth + RowGap, y, rowResetWidth, RowHeight), ctx, "us/scope-tree/reset-default");
        if (canPresetReset) UsKernelDraw.HelpHover(new Rect(x + width - rowResetWidth, y, rowResetWidth, RowHeight), ctx, "us/scope-tree/reset-preset");
        if (canRowReset
            && UsKernelDraw.SelectionButton(
                new Rect(x + dropdownWidth + RowGap, y, rowResetWidth, RowHeight), ctx,
                UsKernelDraw.Keyed(ctx, "US.Reset.Row"), ctx.Theme, false))
        {
            ctx.Bindings.Invoke("reset-action-row");
        }
        if (canPresetReset && UsKernelDraw.SelectionButton(
            new Rect(x + width - rowResetWidth, y, rowResetWidth, RowHeight), ctx,
            UsKernelDraw.Keyed(ctx, "US.Tuning.ResetToPreset"), ctx.Theme, false))
            ctx.Bindings.Invoke("reset-action-to-preset", selectedRow!.Value.ActionKey);
        y += RowHeight + RowGap;

        float noteHeight = ResponseDetailsHeight(ctx, width);
        DrawResponseDetails(new Rect(x, y, width, noteHeight), ctx);
        return y + noteHeight;
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
                ctx.Theme[UiPaletteRole.Foreground],
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
            ctx.Theme[UiPaletteRole.Foreground],
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
            ctx.Theme[UiPaletteRole.Foreground],
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

        ScopeRowLayout layout = ScopeRowLayoutFor(rect.width, row.DisplayName, HintTextFor(ctx, row), ctx.Metrics);
        float scopeButtonWidth = layout.DropdownWidth;

        // XG1.1: the row is drawn in the disabled ink while the layer has no target. The carrier dropdown has
        // no enabled flag this scope owns, so the ink plus the reason sentence above are how the state reads.
        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y, layout.LabelWidth, layout.LabelLineHeight),
            row.DisplayName,
            ctx.Theme,
            submittable ? ctx.Theme[UiPaletteRole.Foreground] : ctx.Theme[UiPaletteRole.DisabledForeground],
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
                submittable ? ctx.Theme[UiPaletteRole.SecondaryForeground] : ctx.Theme[UiPaletteRole.DisabledForeground],
                UiFont.Tiny,
                TextAnchor.MiddleLeft);
        }

        // R4-A / A2: the option list carries the TYPED value, and the Auto entry carries a real null - the
        // null-inherit commit is a value, not an empty token. Nothing here converts a scope to a string.
        var options = new List<UiChoice<BehaviorScope?>>
        {
            new UiChoice<BehaviorScope?>(UsKernelDraw.Keyed(ctx, AutoLabelKey), null)
        };
        foreach (BehaviorScope scope in SupportedStates(row.Action))
        {
            options.Add(new UiChoice<BehaviorScope?>(UsKernelDraw.Keyed(ctx, ActionScopeRules.ScopeLabelKey(row.Action, scope)), scope));
        }

        BehaviorScope? current = row.HasOwnScope ? row.Scope : null;
        Rect dropdownRect = new(rect.xMax - scopeButtonWidth - ScopeDropdownGap, rect.y + (rect.height - ButtonHeight) * 0.5f, scopeButtonWidth, ButtonHeight);
        string elementId = "scope-tree-scope-" + Uri.EscapeDataString(row.ActionKey);

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
    private static void DrawDisabledScopeBand(Rect rect, UiWidgetContext ctx, BehaviorScope? current, IReadOnlyList<UiChoice<BehaviorScope?>> options)
    {
        string display = "";
        for (int i = 0; i < options.Count; i++)
        {
            if (EqualityComparer<BehaviorScope?>.Default.Equals(options[i].Value, current))
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
            ctx.Theme[UiPaletteRole.DisabledForeground],
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
    private float ScopeRowHeightFor(float rowWidth, ActionScopeRowView row, UiWidgetContext ctx)
    {
        return ScopeRowLayoutFor(rowWidth, row.DisplayName, HintTextFor(ctx, row), ctx.Metrics).RowHeight;
    }

    private static BehaviorScope[] SupportedStates(string action)
    {
        var states = new List<BehaviorScope>(3);
        foreach (BehaviorScope scope in new[] { BehaviorScope.Disabled, BehaviorScope.AnyOccurrence, BehaviorScope.ActiveCommand })
        {
            if ((!Kernel.BuiltInActionKeys.Contains(action) || Authoring.BuiltInPawnContracts.NormalizeScope(action, scope) == scope))
            {
                states.Add(scope);
            }
        }

        return states.ToArray();
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
        IReadOnlyList<string> order = group == ActionScopeGroup.PlayerTriggered
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
    /// bound value stays the BehaviorScope instance (R4-A / A2), only the text key is chosen there.
    /// </summary>
    private static bool HasDraftPair(List<ActionScopeRowView> playerRows)
    {
        bool draft = false;
        bool undraft = false;
        for (int i = 0; i < playerRows.Count; i++)
        {
            if (playerRows[i].Action == CoreActions.Draft) draft = true;
            else if (playerRows[i].Action == CoreActions.Undraft) undraft = true;
        }

        return draft && undraft;
    }
}
