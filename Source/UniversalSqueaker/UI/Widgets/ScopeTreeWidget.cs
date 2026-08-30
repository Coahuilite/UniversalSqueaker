using System;
using System.Collections.Generic;
using System.Globalization;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Widgets;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// US composite widget: the S5 three-layer tuning editor (选项 ①). One editor manages both the
/// built-in action scope tree and the four mood rows for the CURRENT layer:
///   - layer segment: Global / Race / Xenotype (emits <see cref="UiCommandKind.SetTuningLayer"/>);
///   - domain picker (Race/Xenotype layers only): cycles the layer domain options
///     (emits <see cref="UiCommandKind.SetTuningDomain"/>);
///   - scope rows: per-action ring of [inherit, Off, Any, Command] filtered by the action's
///     supported scopes; inherit = clear this layer's record (恢复继承);
///   - mood rows: per-mood pitch/volume/jitter sliders (field-level writes, hasX per factor,
///     emits <see cref="UiCommandKind.SetMoodTuning"/>) + Auto (clear row).
/// Every value and state comes from the page view state; the widget stays stateless.
/// </summary>
public sealed class ScopeTreeWidget : IWidget
{
    public const string Kind = "us/scope-tree";

    private const string Title = "Tuning editor";
    private const string ScopeHeaderText = "Action Scope";
    private const string MoodHeaderText = "Mood Tuning";

    private const float LayerRowHeight = 28f;
    private const float DomainRowHeight = 24f;
    private const float RowHeight = 24f;
    private const float MoodRowHeight = 30f;
    private const float RowGap = 2f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;
    private const float LeftPadding = 10f;
    private const float ButtonWidth = 96f;
    private const float ButtonHeight = 20f;
    private const float MoodLabelWidth = 64f;
    private const float MoodClearWidth = 46f;
    private const float MoodGap = 6f;
    private const float MoodValueWidth = 34f;
    private const string ValueSeparator = "\u0001";

    private static readonly string[] LayerNames = { "Global", "Race", "Xenotype" };

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        int layer = ReadLayer(ctx);
        int scopeCount = ctx.TryGetViewValue("ActionScopes", out object? scopesValue)
            && scopesValue is IReadOnlyList<ActionScopeRowView> rows
            ? rows.Count : 0;
        int moodCount = ctx.TryGetViewValue("MoodTuningRows", out object? moodsValue)
            && moodsValue is IReadOnlyList<MoodTuningRowView> moodRows
            ? moodRows.Count : 0;

        float width = VoicePacksLayout.InnerWidth(ctx.ViewWidth);
        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);

        float bodyHeight = TopPadding + LayerRowHeightFor(width) + VoicePacksLayout.Gap;
        if (layer > 0) bodyHeight += DomainRowHeight + VoicePacksLayout.Gap;
        bodyHeight += VoicePacksLayout.SectionHeaderHeightFor(ScopeHeaderText, width, metrics) + VoicePacksLayout.Gap
            + scopeCount * (RowHeight + RowGap);
        if (scopeCount > 0)
        {
            bodyHeight += VoicePacksLayout.SectionHeaderHeightFor(MoodHeaderText, width, metrics) + VoicePacksLayout.Gap
                + moodCount * (MoodRowHeight + RowGap);
        }
        bodyHeight += BottomPadding;

        return UiGuard.MeasureOrFallback(
            () => UsCard.Measure(bodyHeight, ctx),
            UsCard.Measure(bodyHeight, ctx),
            Kind, "UniversalSqueaker");
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit),
            fallback => Widgets.Label(fallback, "Tuning editor unavailable in fallback mode. Basic settings remain available."),
            Kind, "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        UsCard.Draw(rect, Title, ctx, body => DrawBody(body, ctx, emit));
    }

    private static void DrawBody(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        int layer = ReadLayer(ctx);
        ctx.TryGetViewValue("ActionScopes", out object? scopesValue);
        IReadOnlyList<ActionScopeRowView> rows = scopesValue as IReadOnlyList<ActionScopeRowView>
            ?? Array.Empty<ActionScopeRowView>();
        ctx.TryGetViewValue("MoodTuningRows", out object? moodsValue);
        IReadOnlyList<MoodTuningRowView> moodRows = moodsValue as IReadOnlyList<MoodTuningRowView>
            ?? Array.Empty<MoodTuningRowView>();
        ctx.TryGetViewValue("TuningDomains", out object? domainsValue);
        IReadOnlyList<TuningDomainOptionView> domains = domainsValue as IReadOnlyList<TuningDomainOptionView>
            ?? Array.Empty<TuningDomainOptionView>();
        string race = ReadString(ctx, "TuningRace");
        string xeno = ReadString(ctx, "TuningXeno");

        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y + TopPadding;

        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);

        float layerRowHeight = LayerRowHeightFor(innerWidth);
        DrawLayerRow(new Rect(x, y, innerWidth, layerRowHeight), layer, businessEmit);
        y += layerRowHeight + VoicePacksLayout.Gap;

        if (layer > 0)
        {
            DrawDomainRow(new Rect(x, y, innerWidth, DomainRowHeight), domains, race, xeno, ctx, emit);
            y += DomainRowHeight + VoicePacksLayout.Gap;
        }

        float scopeHeader = VoicePacksLayout.SectionHeaderHeightFor(ScopeHeaderText, innerWidth, metrics);
        UsWidgetDrawing.DrawSectionHeader(new Rect(x, y, innerWidth, scopeHeader), ScopeHeaderText);
        y += scopeHeader + VoicePacksLayout.Gap;

        foreach (ActionScopeRowView row in rows)
        {
            DrawScopeRow(new Rect(x, y, innerWidth, RowHeight), row, race, xeno, ctx, emit);
            y += RowHeight + RowGap;
        }

        if (rows.Count == 0) return;

        float moodHeader = VoicePacksLayout.SectionHeaderHeightFor(MoodHeaderText, innerWidth, metrics);
        UsWidgetDrawing.DrawSectionHeader(new Rect(x, y, innerWidth, moodHeader), MoodHeaderText);
        y += moodHeader + VoicePacksLayout.Gap;

        foreach (MoodTuningRowView mood in moodRows)
        {
            DrawMoodRow(new Rect(x, y, innerWidth, MoodRowHeight), mood, race, xeno, ctx, emit);
            y += MoodRowHeight + RowGap;
        }
    }

    private static int ReadLayer(WidgetContext ctx)
    {
        if (ctx.TryGetViewValue("TuningLayer", out object? value) && value is int layer) return layer;
        return 0;
    }

    private static string ReadString(WidgetContext ctx, string key)
    {
        return ctx.TryGetViewValue(key, out object? value) && value is string text ? text : "";
    }

    private static void DrawLayerRow(Rect rect, int layer, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 4f, 120f, 20f), "Tuning layer");
        Text.Font = oldFont;
        GUI.color = oldColor;

        float available = rect.width - LeftPadding * 2f - 120f - RowGap * 2f;
        float buttonWidth = (available - RowGap * 2f) / 3f;
        bool stacked = buttonWidth < 56f;
        if (stacked)
        {
            float y = rect.y + 22f;
            buttonWidth = Math.Max(1f, (rect.width - LeftPadding * 2f - RowGap * 2f) / 3f);
            for (int i = 0; i < LayerNames.Length; i++)
            {
                Rect buttonRect = new(rect.x + LeftPadding, y, buttonWidth, ButtonHeight);
                bool selected = layer == i;
                DrawSegment(buttonRect, LayerNames[i], selected);
                int captured = i;
                UiInteract.Button(buttonRect, UiLayer.Content,
                    () => emit?.Invoke(new UiCommand(UiCommandKind.SetTuningLayer, arg: captured.ToString(CultureInfo.InvariantCulture))));
                y += ButtonHeight + RowGap;
            }
            return;
        }

        float buttonX = rect.x + rect.width - LeftPadding - buttonWidth * 3f - RowGap * 2f;
        for (int i = 0; i < LayerNames.Length; i++)
        {
            Rect buttonRect = new(buttonX, rect.y + (rect.height - ButtonHeight) / 2f, buttonWidth, ButtonHeight);
            bool selected = layer == i;
            DrawSegment(buttonRect, LayerNames[i], selected);
            int captured = i;
            UiInteract.Button(buttonRect, UiLayer.Content,
                () => emit?.Invoke(new UiCommand(UiCommandKind.SetTuningLayer, arg: captured.ToString(CultureInfo.InvariantCulture))));
            buttonX += buttonWidth + RowGap;
        }
    }

    private static float LayerRowHeightFor(float width)
    {
        float available = width - LeftPadding * 2f - 120f - RowGap * 2f;
        float buttonWidth = (available - RowGap * 2f) / 3f;
        return buttonWidth < 56f ? 22f + ButtonHeight * 3f + RowGap * 2f : LayerRowHeight;
    }

    private static void DrawDomainRow(Rect rect, IReadOnlyList<TuningDomainOptionView> domains, string race, string xeno, WidgetContext ctx, Action<KitUiCommand> kitEmit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);

        TuningDomainOptionView? current = null;
        for (int i = 0; i < domains.Count; i++)
        {
            TuningDomainOptionView option = domains[i];
            if (string.Equals(option.RaceDefName, race, StringComparison.Ordinal)
                && string.Equals(option.TargetDefName, xeno, StringComparison.Ordinal))
            {
                current = option;
                break;
            }
        }

        string currentValue = current != null
            ? current.Value.RaceDefName + ValueSeparator + current.Value.TargetDefName
            : "";

        float dropdownWidth = Math.Min(ButtonWidth, Math.Max(40f, rect.width - 160f));
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 4f, 120f, 20f), "Layer domain");
        Text.Font = oldFont;
        GUI.color = oldColor;

        var options = new List<KeyValuePair<string, string>>();
        foreach (TuningDomainOptionView option in domains)
        {
            options.Add(new KeyValuePair<string, string>(
                option.DisplayName,
                option.RaceDefName + ValueSeparator + option.TargetDefName));
        }

        Rect dropdownRect = new(rect.xMax - dropdownWidth - 8f, rect.y + (rect.height - ButtonHeight) / 2f, dropdownWidth, ButtonHeight);
        DrawDropdown(dropdownRect, "domain", currentValue, options, ctx, kitEmit, selected =>
        {
            string[] parts = selected.Split(new[] { ValueSeparator }, StringSplitOptions.None);
            if (parts.Length != 2) return;
            Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(kitEmit);
            businessEmit(new UiCommand(UiCommandKind.SetTuningDomain, raceDefName: parts[0], targetDefName: parts[1]));
        });
    }

    private static void DrawScopeRow(Rect rect, ActionScopeRowView row, string race, string xeno, WidgetContext ctx, Action<KitUiCommand> kitEmit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);

        float scopeButtonWidth = Math.Min(ButtonWidth, Math.Max(40f, rect.width - 120f));
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 3f, Math.Max(1f, rect.width - LeftPadding - scopeButtonWidth - 90f), ButtonHeight), row.DisplayName);

        LayoutTier tier = VoicePacksLayout.ForWidth(rect.width);
        if (tier == LayoutTier.Comfortable && (!row.HasOwnScope || row.Scope != row.EffectiveScope))
        {
            Text.Font = GameFont.Tiny;
            GUI.color = UsVisualTokens.TextSecondary;
            Widgets.Label(new Rect(rect.x + rect.width - scopeButtonWidth - 96f, rect.y + 6f, Math.Max(1f, 86f), 14f), "→ " + ShortName(row.EffectiveScope));
        }
        Text.Font = oldFont;
        GUI.color = oldColor;

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
        DrawDropdown(dropdownRect, "scope-" + row.ActionKey, current, options, ctx, kitEmit, selected =>
        {
            Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(kitEmit);
            businessEmit(new UiCommand(UiCommandKind.SetActionTuningScope, raceDefName: race, targetDefName: xeno, arg: (selected ?? "") + "|" + row.ActionKey));
        });
    }

    private static void DrawDropdown(
        Rect rect,
        string idSuffix,
        string current,
        IReadOnlyList<KeyValuePair<string, string>> options,
        WidgetContext ctx,
        Action<KitUiCommand> kitEmit,
        Action<string> onSelected)
    {
        if (options.Count == 0) return;

        string id = "scope-tree-" + idSuffix;
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bind"] = "Current",
            ["OptionsBind"] = "Options",
            ["EmitName"] = "Select",
            ["Height"] = rect.height.ToString(CultureInfo.InvariantCulture)
        };
        var spec = new UiElementSpec(id, DropdownWidget.Kind, attributes);
        var view = new Dictionary<string, object?>
        {
            ["Current"] = current,
            ["Options"] = options
        };
        var dropdownCtx = new WidgetContext(ctx.Source, view, ctx.Metrics, ctx.State);
        var dropdown = new DropdownWidget();
        dropdown.Configure(spec);
        dropdown.Draw(rect, dropdownCtx, cmd =>
        {
            if (cmd.Name == "Select" && cmd.Payload is string selected)
                onSelected(selected);
        });
    }

    private static SqueakActionScope[] SupportedStates(SqueakAction action)
    {
        List<SqueakActionScope> states = new(3);
        foreach (SqueakActionScope scope in new[] { SqueakActionScope.Disabled, SqueakActionScope.AnyOccurrence, SqueakActionScope.ActiveCommand })
        {
            if (NormalizeFor(action, scope) == scope) states.Add(scope);
        }
        return states.ToArray();
    }

    private static void DrawMoodRow(Rect rect, MoodTuningRowView row, string race, string xeno, WidgetContext ctx, Action<KitUiCommand> kitEmit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);
        DrawMoodRowBody(rect, row, race, xeno, ctx, kitEmit);
    }

    private static void DrawMoodRowBody(Rect rect, MoodTuningRowView row, string race, string xeno, WidgetContext ctx, Action<KitUiCommand> kitEmit)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 7f, MoodLabelWidth, 16f), row.DisplayName);
        Text.Font = oldFont;
        GUI.color = oldColor;

        float clearX = rect.xMax - MoodClearWidth - 8f;
        float controlsWidth = clearX - (rect.x + LeftPadding + MoodLabelWidth) - MoodGap;
        float groupWidth = (controlsWidth - MoodGap * 2f) / 3f;
        // Each factor uses a compact stepper-slider; if it cannot fit, show a narrow-screen hint.
        if (groupWidth < 96f)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = UsVisualTokens.TextSecondary;
            Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 8f, rect.width - LeftPadding - 8f, 14f), "Window too narrow for mood controls");
            Text.Font = GameFont.Small;
            GUI.color = UsVisualTokens.TextPrimary;
            return;
        }
        float factorX = rect.x + LeftPadding + MoodLabelWidth + MoodGap;

        float pitch = row.Own?.hasPitchFactor == true ? row.Own.pitchFactor : row.EffectivePitch;
        float volume = row.Own?.hasVolumeFactor == true ? row.Own.volumeFactor : row.EffectiveVolume;
        float jitter = row.Own?.hasPitchJitter == true ? Math.Max(0f, row.Own.pitchJitter.max - 1f) : row.EffectiveJitterHalf;

        factorX = DrawMoodStepper(new Rect(factorX, rect.y, groupWidth, rect.height), "P", pitch, 0.5f, 2f, 0.05f, row, "pitch", race, xeno, ctx, kitEmit);
        factorX = DrawMoodStepper(new Rect(factorX + MoodGap, rect.y, groupWidth, rect.height), "V", volume, 0.1f, 2f, 0.05f, row, "volume", race, xeno, ctx, kitEmit);
        DrawMoodStepper(new Rect(factorX + MoodGap, rect.y, groupWidth, rect.height), "J", jitter, 0f, 0.5f, 0.05f, row, "jitter", race, xeno, ctx, kitEmit);

        bool clearHover = Mouse.IsOver(new Rect(clearX, rect.y, MoodClearWidth, rect.height));
        Rect clearRect = new(clearX, rect.y, MoodClearWidth, rect.height);
        UsSurface.DrawSurface(clearRect, clearHover ? UsSurface.SurfaceKind.Danger : UsSurface.SurfaceKind.Selected);
        UsSurface.DrawBorder(clearRect, UsVisualTokens.AccentGold);
        Rect labelRect = new(clearX, rect.y + 7f, MoodClearWidth, 16f);
        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextSecondary;
        Widgets.Label(labelRect, "Auto");
        Text.Font = oldFont;
        GUI.color = oldColor;
        UiInteract.Button(new Rect(clearX, rect.y, MoodClearWidth, rect.height), UiLayer.Content,
            () =>
            {
                Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(kitEmit);
                businessEmit(new UiCommand(UiCommandKind.SetMoodTuning, raceDefName: race, targetDefName: xeno, arg: row.Mood + "|clear"));
            });
    }

    private static float DrawMoodStepper(
        Rect rect,
        string label,
        float value,
        float min,
        float max,
        float step,
        MoodTuningRowView row,
        string factor,
        string race,
        string xeno,
        WidgetContext ctx,
        Action<KitUiCommand> kitEmit)
    {
        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextSecondary;
        Widgets.Label(new Rect(rect.x, rect.y + 4f, 14f, 16f), label);

        Rect stepperRect = new(rect.x + 14f, rect.y, Math.Max(1f, rect.width - 14f), rect.height);
        string id = "mood-" + row.Mood + "-" + factor;
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bind"] = "Value",
            ["Min"] = min.ToString(CultureInfo.InvariantCulture),
            ["Max"] = max.ToString(CultureInfo.InvariantCulture),
            ["Step"] = step.ToString(CultureInfo.InvariantCulture),
            ["Format"] = "0.###",
            ["EmitName"] = "Mood",
            ["Height"] = rect.height.ToString(CultureInfo.InvariantCulture),
            ["ButtonWidth"] = "16",
            ["FieldWidth"] = "36"
        };
        var spec = new UiElementSpec(id, StepperSliderWidget.Kind, attributes);
        var view = new Dictionary<string, object?> { ["Value"] = value };
        var stepperCtx = new WidgetContext(ctx.Source, view, ctx.Metrics, ctx.State);
        var stepper = new StepperSliderWidget();
        stepper.Configure(spec);
        stepper.Draw(stepperRect, stepperCtx, cmd =>
        {
            if (cmd.Name == "Mood" && cmd.Payload is float newValue)
                EmitMoodFactor(row, factor, newValue, race, xeno, kitEmit);
        });

        return rect.x + rect.width;
    }

    private static void EmitMoodFactor(MoodTuningRowView row, string factor, float value, string race, string xeno, Action<KitUiCommand> kitEmit)
    {
        string arg = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}", row.Mood, factor, value.ToString("0.###", CultureInfo.InvariantCulture));
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(kitEmit);
        businessEmit(new UiCommand(UiCommandKind.SetMoodTuning, raceDefName: race, targetDefName: xeno, arg: arg));
    }

    private static void DrawSegment(Rect rect, string label, bool off)
    {
        bool selectedHover = Mouse.IsOver(rect);
        UsSurface.DrawSurface(rect, off
            ? (selectedHover ? UsSurface.SurfaceKind.Danger : UsSurface.SurfaceKind.Selected)
            : (selectedHover ? UsSurface.SurfaceKind.Hover : UsSurface.SurfaceKind.Raised));
        UsSurface.DrawBorder(rect, off ? UsVisualTokens.Danger : UsVisualTokens.AccentGold);
        GUI.color = off ? UsVisualTokens.TextSecondary : UsVisualTokens.AccentGold;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rect.x, rect.y + 3f, rect.width, 16f), label);
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

    /// <summary>Normalize against the action's SupportedScopes so unsupported states
    /// (e.g. Draft/Undraft/Equip's AnyOccurrence) fall back to the action's default scope.</summary>
    private static SqueakActionScope NormalizeFor(SqueakAction action, SqueakActionScope scope)
        => SqueakActionDefinitions.NormalizeScope(action, scope);
}