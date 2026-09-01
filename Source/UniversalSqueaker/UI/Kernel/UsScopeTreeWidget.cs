using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;
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
///  - mood rows expose pitch/volume/jitter steppers plus an Auto clear, writing "set-mood-tuning".
/// All values come from typed read bindings; every write is a typed action.
/// </summary>
public sealed class UsScopeTreeWidget : UsSectionWidgetBase
{
    public const string KindName = "us/scope-tree";

    private const float LayerRowHeight = 30f;
    private const float DomainRowHeight = 28f;
    private const float LayerRowLabelBandHeight = 22f;
    private const float RowHeight = 28f;
    private const float MoodRowHeight = 32f;
    private const float RowGap = 2f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;
    private const float LeftPadding = 10f;
    private const float ButtonWidth = 96f;
    private const float ButtonHeight = 24f;
    private const float MoodLabelWidth = 64f;
    private const float MoodClearWidth = 52f;
    private const float MoodGap = 6f;

    private static readonly string[] LayerNames = { "Global", "Race", "Xenotype" };

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
            bodyHeight += moodRows.Count * (MoodRowHeightFor(width) + RowGap);
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
            "Action Scope",
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
                targetGroup == ActionScopeGroup.Operable ? "Operable / Command" : "Autonomous",
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
                "Mood Tuning",
                ctx.Theme,
                ctx.Theme.TextPrimary,
                UiFont.Small,
                TextAnchor.MiddleLeft);
            y += RowHeight + RowGap;

            float moodRowHeight = MoodRowHeightFor(innerWidth);
            foreach (MoodTuningRowView mood in moodRows)
            {
                DrawMoodRow(new Rect(x, y, innerWidth, moodRowHeight), mood, ctx);
                y += moodRowHeight + RowGap;
            }
        }
    }

    private void DrawLayerRow(Rect rect, int layer, UiWidgetContext ctx)
    {
        if (UsesStackedLayerButtons(rect.width))
        {
            UsKernelDraw.Label(
                new Rect(rect.x + LeftPadding, rect.y, 120f, LayerRowLabelBandHeight),
                "Tuning layer",
                ctx.Theme,
                ctx.Theme.TextPrimary,
                UiFont.Small,
                TextAnchor.MiddleLeft);

            float stackedButtonWidth = Math.Max(1f, (rect.width - LeftPadding * 2f - RowGap * 2f) / 3f);
            float y = rect.y + LayerRowLabelBandHeight;
            for (int i = 0; i < LayerNames.Length; i++)
            {
                Rect buttonRect = new(rect.x + LeftPadding, y, stackedButtonWidth, ButtonHeight);
                if (UsKernelDraw.SelectionButton(buttonRect, LayerNames[i], ctx.Theme, layer == i, font: UiFont.Tiny))
                {
                    ctx.Bindings.Invoke("set-tuning-layer", i);
                }

                y += ButtonHeight + RowGap;
            }

            return;
        }

        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y, 120f, rect.height),
            "Tuning layer",
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);

        float available = rect.width - LeftPadding * 2f - 120f - RowGap * 2f;
        float buttonWidth = Math.Max(1f, (available - RowGap * 2f) / 3f);
        float buttonX = rect.x + rect.width - LeftPadding - buttonWidth * 3f - RowGap * 2f;
        for (int i = 0; i < LayerNames.Length; i++)
        {
            Rect buttonRect = new(buttonX, rect.y + (rect.height - ButtonHeight) / 2f, buttonWidth, ButtonHeight);
            if (UsKernelDraw.SelectionButton(buttonRect, LayerNames[i], ctx.Theme, layer == i, font: UiFont.Tiny))
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

        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);
        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y, 120f, rect.height),
            "Layer domain",
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
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        string displayName = ctx.Translation.Translate(DefinitionFor(row.Action).DisplayKey);
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
                new Rect(rect.x + rect.width - scopeButtonWidth - 96f, rect.y + 6f, Math.Max(1f, 86f), 14f),
                "→ " + ShortName(row.EffectiveScope),
                ctx.Theme,
                ctx.Theme.TextSecondary,
                UiFont.Tiny,
                TextAnchor.MiddleLeft);
        }

        var options = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Auto", "")
        };
        foreach (SqueakActionScope scope in SupportedStates(row.Action))
        {
            options.Add(new KeyValuePair<string, string>(ShortName(scope), scope.ToString()));
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
        bool hovered = Mouse.IsOver(rect);
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, false);

        float pitch = row.Own?.hasPitchFactor == true ? row.Own.pitchFactor : row.EffectivePitch;
        float volume = row.Own?.hasVolumeFactor == true ? row.Own.volumeFactor : row.EffectiveVolume;
        float jitter = row.Own?.hasPitchJitter == true ? Math.Max(0f, row.Own.pitchJitter.max - 1f) : row.EffectiveJitterHalf;

        if (UsesStackedMoodRows(rect.width))
        {
            float headerHeight = ButtonHeight;
            float labelWidth = Math.Max(1f, rect.width - LeftPadding - MoodClearWidth - 8f - RowGap);
            UsKernelDraw.Label(
                new Rect(rect.x + LeftPadding, rect.y, labelWidth, headerHeight),
                row.DisplayName,
                ctx.Theme,
                ctx.Theme.TextPrimary,
                UiFont.Small,
                TextAnchor.MiddleLeft);

            DrawAutoClearButton(
                new Rect(rect.xMax - MoodClearWidth - 8f, rect.y, MoodClearWidth, headerHeight),
                row,
                ctx);

            float lineWidth = Math.Max(1f, rect.width - LeftPadding * 2f);
            float y = rect.y + headerHeight + RowGap;
            DrawMoodStepper(
                new Rect(rect.x + LeftPadding, y, lineWidth, ButtonHeight),
                "P", pitch, 0.5f, 2f, row, SqueakMoodFactor.Pitch, ctx);
            y += ButtonHeight + RowGap;
            DrawMoodStepper(
                new Rect(rect.x + LeftPadding, y, lineWidth, ButtonHeight),
                "V", volume, 0.1f, 2f, row, SqueakMoodFactor.Volume, ctx);
            y += ButtonHeight + RowGap;
            DrawMoodStepper(
                new Rect(rect.x + LeftPadding, y, lineWidth, ButtonHeight),
                "J", jitter, 0f, 0.5f, row, SqueakMoodFactor.Jitter, ctx);
            return;
        }

        UsKernelDraw.Label(
            new Rect(rect.x + LeftPadding, rect.y + 7f, MoodLabelWidth, 16f),
            row.DisplayName,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);

        float clearX = rect.xMax - MoodClearWidth - 8f;
        Rect clearRect = new(clearX, rect.y, MoodClearWidth, rect.height);
        float controlsWidth = clearX - (rect.x + LeftPadding + MoodLabelWidth) - MoodGap;
        float groupWidth = (controlsWidth - MoodGap * 2f) / 3f;
        float factorX = rect.x + LeftPadding + MoodLabelWidth + MoodGap;

        factorX = DrawMoodStepper(new Rect(factorX, rect.y, groupWidth, rect.height), "P", pitch, 0.5f, 2f, row, SqueakMoodFactor.Pitch, ctx);
        factorX = DrawMoodStepper(new Rect(factorX + MoodGap, rect.y, groupWidth, rect.height), "V", volume, 0.1f, 2f, row, SqueakMoodFactor.Volume, ctx);
        DrawMoodStepper(new Rect(factorX + MoodGap, rect.y, groupWidth, rect.height), "J", jitter, 0f, 0.5f, row, SqueakMoodFactor.Jitter, ctx);

        DrawAutoClearButton(clearRect, row, ctx);
    }

    private void DrawAutoClearButton(Rect clearRect, MoodTuningRowView row, UiWidgetContext ctx)
    {
        if (UsKernelDraw.SelectionButton(clearRect, "Auto", ctx.Theme, selected: false, danger: true, font: UiFont.Tiny))
        {
            ctx.Bindings.Invoke("set-mood-tuning", new UsMoodWrite(row.Mood, SqueakMoodFactor.Clear, null));
        }
    }

    private float DrawMoodStepper(
        Rect rect,
        string label,
        float value,
        float min,
        float max,
        MoodTuningRowView row,
        SqueakMoodFactor factor,
        UiWidgetContext ctx)
    {
        UsKernelDraw.Label(
            new Rect(rect.x, rect.y + 4f, 14f, 16f),
            label,
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

        bool minusClicked = UsKernelDraw.SelectionButton(minusRect, "−", ctx.Theme, selected: false);
        bool plusClicked = UsKernelDraw.SelectionButton(plusRect, "+", ctx.Theme, selected: false);

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
            ? LayerRowLabelBandHeight + LayerNames.Length * ButtonHeight + (LayerNames.Length - 1) * RowGap
            : LayerRowHeight;
    }

    /// <summary>
    /// True when the three horizontal mood stepper groups cannot fit beside the mood label and
    /// Auto clear button. Uses the same group-width math as the former narrow branch; Measure and
    /// Draw both feed the card body width.
    /// </summary>
    private static bool UsesStackedMoodRows(float bodyWidth)
    {
        float clearX = bodyWidth - MoodClearWidth - 8f;
        float controlsWidth = clearX - (LeftPadding + MoodLabelWidth) - MoodGap;
        float groupWidth = (controlsWidth - MoodGap * 2f) / 3f;
        return groupWidth < 110f;
    }

    /// <summary>
    /// Height a mood row actually consumes: the single-line <see cref="MoodRowHeight"/> on wide
    /// layouts, or the stacked header + three full-width stepper lines on narrow ones. Measure and
    /// Draw must agree so the section card never clips or overlaps mood controls.
    /// </summary>
    private static float MoodRowHeightFor(float bodyWidth)
    {
        if (!UsesStackedMoodRows(bodyWidth)) return MoodRowHeight;
        return ButtonHeight + RowGap + 3f * ButtonHeight + 2f * RowGap;
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

    private static string ShortName(SqueakActionScope scope)
    {
        return scope switch
        {
            SqueakActionScope.Disabled => "Off",
            SqueakActionScope.AnyOccurrence => "Any",
            SqueakActionScope.ActiveCommand => "Command",
            _ => "Off",
        };
    }
}
