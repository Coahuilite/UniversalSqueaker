using System;
using System.Collections.Generic;
using System.Globalization;
using FerriteLib.UiKit;
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

        string helpKey = UsHelp.ResolveKey(_spec);
        return UiGuard.MeasureOrFallback(
            () => UsCard.Measure(bodyHeight, helpKey, ctx),
            UsCard.Measure(bodyHeight, helpKey, ctx),
            Kind, "UniversalSqueaker");
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        string helpKey = UsHelp.ResolveKey(_spec);
        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit, helpKey),
            fallback => Widgets.Label(fallback, "Tuning editor unavailable in fallback mode. Basic settings remain available."),
            Kind, "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit, string helpKey)
    {
        UsCard.Draw(rect, Title, helpKey, ctx, emit, body => DrawBody(body, ctx, emit));
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
            DrawDomainRow(new Rect(x, y, innerWidth, DomainRowHeight), domains, race, xeno, businessEmit);
            y += DomainRowHeight + VoicePacksLayout.Gap;
        }

        float scopeHeader = VoicePacksLayout.SectionHeaderHeightFor(ScopeHeaderText, innerWidth, metrics);
        UsWidgetDrawing.DrawSectionHeader(new Rect(x, y, innerWidth, scopeHeader), ScopeHeaderText);
        y += scopeHeader + VoicePacksLayout.Gap;

        foreach (ActionScopeRowView row in rows)
        {
            DrawScopeRow(new Rect(x, y, innerWidth, RowHeight), row, race, xeno, businessEmit);
            y += RowHeight + RowGap;
        }

        if (rows.Count == 0) return;

        float moodHeader = VoicePacksLayout.SectionHeaderHeightFor(MoodHeaderText, innerWidth, metrics);
        UsWidgetDrawing.DrawSectionHeader(new Rect(x, y, innerWidth, moodHeader), MoodHeaderText);
        y += moodHeader + VoicePacksLayout.Gap;

        foreach (MoodTuningRowView mood in moodRows)
        {
            DrawMoodRow(new Rect(x, y, innerWidth, MoodRowHeight), mood, race, xeno, businessEmit);
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

    private static void DrawDomainRow(Rect rect, IReadOnlyList<TuningDomainOptionView> domains, string race, string xeno, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);

        TuningDomainOptionView? current = null;
        int currentIndex = -1;
        for (int i = 0; i < domains.Count; i++)
        {
            TuningDomainOptionView option = domains[i];
            if (string.Equals(option.RaceDefName, race, StringComparison.Ordinal)
                && string.Equals(option.TargetDefName, xeno, StringComparison.Ordinal))
            {
                current = option;
                currentIndex = i;
                break;
            }
        }

        float domainButtonWidth = Math.Min(ButtonWidth, Math.Max(40f, rect.width - 160f));
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 4f, 120f, 20f), "Layer domain");
        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextSecondary;
        Widgets.Label(new Rect(rect.x + 120f + 12f, rect.y + 5f, Math.Max(1f, rect.width - 132f - domainButtonWidth - 20f), 16f),
            current != null ? current.Value.DisplayName : "No domain available");
        Text.Font = oldFont;
        GUI.color = oldColor;

        if (domains.Count > 1)
        {
            Rect buttonRect = new(rect.xMax - domainButtonWidth - 8f, rect.y + (rect.height - ButtonHeight) / 2f, domainButtonWidth, ButtonHeight);
            DrawSegment(buttonRect, "Next domain >", false);
            int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % domains.Count;
            TuningDomainOptionView next = domains[nextIndex];
            UiInteract.Button(buttonRect, UiLayer.Content,
                () => emit?.Invoke(new UiCommand(UiCommandKind.SetTuningDomain, raceDefName: next.RaceDefName, targetDefName: next.TargetDefName)));
        }
    }

    private static void DrawScopeRow(Rect rect, ActionScopeRowView row, string race, string xeno, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);

        float scopeButtonWidth = Math.Min(ButtonWidth, Math.Max(40f, rect.width - 120f));
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 3f, Math.Max(1f, rect.width - LeftPadding - scopeButtonWidth - 90f), ButtonHeight), row.DisplayName);

        // 继承提示：本层无记录或与有效值不同时显示有效（生效）作用域；窄屏隐藏。
        LayoutTier tier = VoicePacksLayout.ForWidth(rect.width);
        if (tier == LayoutTier.Comfortable && (!row.HasOwnScope || row.Scope != row.EffectiveScope))
        {
            Text.Font = GameFont.Tiny;
            GUI.color = UsVisualTokens.TextSecondary;
            Widgets.Label(new Rect(rect.x + rect.width - scopeButtonWidth - 96f, rect.y + 6f, Math.Max(1f, 86f), 14f), "→ " + ShortName(row.EffectiveScope));
        }
        Text.Font = oldFont;
        GUI.color = oldColor;

        string label = row.HasOwnScope ? ShortName(row.Scope) : "Auto";
        bool off = row.HasOwnScope && row.Scope == SqueakActionScope.Disabled;
        Rect buttonRect = new(rect.xMax - scopeButtonWidth - 8f, rect.y + (rect.height - ButtonHeight) / 2f, scopeButtonWidth, ButtonHeight);
        DrawSegment(buttonRect, label, off);

        UiInteract.Button(buttonRect, UiLayer.Content, () => CycleScope(rect, row, race, xeno, emit));
    }

    /// <summary>态环：[inherit] + 动作支持的 [Off/Any/Command]（NormalizeFor 过滤）。环到尾部回 inherit =
    /// 清本层记录（恢复继承）。</summary>
    private static void CycleScope(Rect rect, ActionScopeRowView row, string race, string xeno, Action<UiCommand> emit)
    {
        SqueakActionScope[] states = SupportedStates(row.Action);
        bool currentIsInherit = !row.HasOwnScope;
        string scopeText;
        if (states.Length == 0 || (!currentIsInherit && Array.IndexOf(states, row.Scope) == states.Length - 1))
        {
            scopeText = "";
        }
        else if (currentIsInherit)
        {
            scopeText = states[0].ToString();
        }
        else
        {
            int index = Array.IndexOf(states, row.Scope);
            scopeText = states[(index + 1) % states.Length].ToString();
        }
        emit?.Invoke(new UiCommand(UiCommandKind.SetActionTuningScope, raceDefName: race, targetDefName: xeno, arg: scopeText + "|" + row.ActionKey));
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

    private static void DrawMoodRow(Rect rect, MoodTuningRowView row, string race, string xeno, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        UsSurface.DrawRowSurface(rect, hovered, false, false);
        DrawMoodRowBody(rect, row, race, xeno, emit);
    }

    private static void DrawMoodRowBody(Rect rect, MoodTuningRowView row, string race, string xeno, Action<UiCommand> emit)
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
        // 每个因子簇固定占用 86px（label 16 + minus 18 + value 34 + plus 18）；放不下时降级为提示行，
        // 避免三簇与 Auto 按钮互相重叠（窄窗口安全）。
        if (groupWidth < 86f)
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

        factorX = DrawMoodFactor(new Rect(factorX, rect.y, groupWidth, rect.height), "P", pitch, 0.5f, 2f, 0.05f, row, "pitch", race, xeno, emit);
        factorX = DrawMoodFactor(new Rect(factorX + MoodGap, rect.y, groupWidth, rect.height), "V", volume, 0.1f, 2f, 0.05f, row, "volume", race, xeno, emit);
        DrawMoodFactor(new Rect(factorX + MoodGap, rect.y, groupWidth, rect.height), "J", jitter, 0f, 0.5f, 0.05f, row, "jitter", race, xeno, emit);

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
            () => emit?.Invoke(new UiCommand(UiCommandKind.SetMoodTuning, raceDefName: race, targetDefName: xeno, arg: row.Mood + "|clear")));
    }

    /// <summary>心情因子 −/＋ 步进控制（与本页按钮式交互一致，无滑块 API 依赖）。
    /// 值取本层记录（hasX）否则有效（继承）值；每次点击写该因子（字段级 hasX）。</summary>
    private static float DrawMoodFactor(
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
        Action<UiCommand> emit)
    {
        // 标签独占左侧列（16px），控件右移避免被 − 按钮的不透明背景盖住。
        Rect minusRect = new(rect.x + 16f, rect.y + 5f, 18f, 18f);
        Rect valueRect = new(rect.x + 36f, rect.y + 8f, MoodValueWidth, 14f);
        Rect plusRect = new(rect.x + 36f + MoodValueWidth, rect.y + 5f, 18f, 18f);

        Text.Font = GameFont.Tiny;
        GUI.color = UsVisualTokens.TextSecondary;
        Widgets.Label(new Rect(rect.x, rect.y + 4f, 14f, 16f), label);
        DrawSegment(minusRect, "-", false);
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(valueRect, value.ToString("0.###", CultureInfo.InvariantCulture));
        DrawSegment(plusRect, "+", false);
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;

        UiInteract.Button(minusRect, UiLayer.Content,
            () => EmitMoodFactor(row, factor, Mathf.Max(min, value - step), race, xeno, emit));
        UiInteract.Button(plusRect, UiLayer.Content,
            () => EmitMoodFactor(row, factor, Mathf.Min(max, value + step), race, xeno, emit));

        return rect.x + 16f + 20f + MoodValueWidth + 18f;
    }

    private static void EmitMoodFactor(MoodTuningRowView row, string factor, float value, string race, string xeno, Action<UiCommand> emit)
    {
        string arg = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}", row.Mood, factor, value.ToString("0.###", CultureInfo.InvariantCulture));
        emit?.Invoke(new UiCommand(UiCommandKind.SetMoodTuning, raceDefName: race, targetDefName: xeno, arg: arg));
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