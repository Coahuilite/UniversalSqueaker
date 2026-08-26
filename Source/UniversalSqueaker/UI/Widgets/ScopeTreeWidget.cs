using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// US composite widget: the A6 tree-style per-action scope switch (first iteration: Global layer only).
/// Draws the "Action Scope" section header plus one row per built-in action. Each row shows the
/// localized action display name (via <see cref="SqueakLabels.Action"/>) and a three-state cycle
/// button (Off / Any / Command -> Disabled / AnyOccurrence / ActiveCommand). Clicking the button
/// emits a business <see cref="UiCommand"/> (<see cref="UiCommandKind.SetActionTuningScope"/>)
/// with arg "&lt;scope&gt;|&lt;actionKey&gt;"; clearing the layer is arg "|&lt;actionKey&gt;".
/// </summary>
public sealed class ScopeTreeWidget : IWidget
{
    public const string Kind = "us/scope-tree";

    private const string HeaderText = "Action Scope";
    private const float RowHeight = 24f;
    private const float RowGap = 2f;
    private const float TopPadding = 2f;
    private const float BottomPadding = 2f;
    private const float LeftPadding = 10f;
    private const float ButtonWidth = 96f;
    private const float ButtonHeight = 20f;

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        if (!ctx.TryGetViewValue("ActionScopes", out object? value)
            || value is not IReadOnlyList<ActionScopeRowView> rows
            || rows.Count == 0)
        {
            return 0f;
        }

        float width = VoicePacksLayout.InnerWidth(ctx.ViewWidth);
        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
        float headerHeight = VoicePacksLayout.SectionHeaderHeightFor(HeaderText, width, metrics);
        return TopPadding + headerHeight + VoicePacksLayout.Gap
            + rows.Count * (RowHeight + RowGap) + BottomPadding;
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        if (!ctx.TryGetViewValue("ActionScopes", out object? value)
            || value is not IReadOnlyList<ActionScopeRowView> rows
            || rows.Count == 0)
        {
            return;
        }

        float innerWidth = VoicePacksLayout.InnerWidth(rect.width);
        float x = rect.x + VoicePacksLayout.Padding;
        float y = rect.y + TopPadding;

        var metrics = new FerriteTextMetricsAdapter(ctx.Metrics);
        float headerHeight = VoicePacksLayout.SectionHeaderHeightFor(HeaderText, innerWidth, metrics);
        UsWidgetDrawing.DrawSectionHeader(new Rect(x, y, innerWidth, headerHeight), HeaderText);
        y += headerHeight + VoicePacksLayout.Gap;

        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        foreach (ActionScopeRowView row in rows)
        {
            DrawRow(new Rect(x, y, innerWidth, RowHeight), row, businessEmit);
            y += RowHeight + RowGap;
        }
    }

    private static void DrawRow(Rect rect, ActionScopeRowView row, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        Widgets.DrawBoxSolid(rect, hovered ? new Color(.16f, .145f, .12f, .94f) : UiPalette.Panel);
        SectionFrame.DrawBorder(rect);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Widgets.Label(new Rect(rect.x + LeftPadding, rect.y + 3f, rect.width - LeftPadding - ButtonWidth - 12f, ButtonHeight), row.DisplayName);

        // 三态循环按钮：Disabled -> AnyOccurrence -> ActiveCommand -> Disabled。
        // 用 NormalizeFor 归一：不支持的态（如 Draft/Undraft/Equip 的 AnyOccurrence）回退到
        // 该动作的 DefaultScope，避免写出运行时永远不匹配的作用域。
        SqueakActionScope next = NormalizeFor(row.Action, NextScope(row.Scope));
        string shortName = ShortName(row.Scope);
        Rect buttonRect = new(rect.xMax - ButtonWidth - 8f, rect.y + (rect.height - ButtonHeight) / 2f, ButtonWidth, ButtonHeight);
        bool selectedHover = Mouse.IsOver(buttonRect);
        Widgets.DrawBoxSolid(buttonRect, row.Scope == SqueakActionScope.Disabled
            ? (selectedHover ? new Color(.45f, .28f, .22f, .96f) : UiPalette.Selected)
            : (selectedHover ? new Color(.20f, .30f, .22f, .96f) : UiPalette.Raised));
        SectionFrame.DrawBorder(buttonRect, UiPalette.Gold);
        GUI.color = row.Scope == SqueakActionScope.Disabled ? UiPalette.Muted : UiPalette.Gold;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(buttonRect.x, buttonRect.y + 3f, buttonRect.width, 16f), shortName);
        Text.Font = oldFont;
        GUI.color = oldColor;

        if (Widgets.ButtonInvisible(buttonRect))
            emit?.Invoke(new UiCommand(UiCommandKind.SetActionTuningScope, arg: next + "|" + row.ActionKey));
    }

    private static SqueakActionScope NextScope(SqueakActionScope current)
    {
        return current switch
        {
            SqueakActionScope.Disabled => SqueakActionScope.AnyOccurrence,
            SqueakActionScope.AnyOccurrence => SqueakActionScope.ActiveCommand,
            SqueakActionScope.ActiveCommand => SqueakActionScope.Disabled,
            _ => SqueakActionScope.Disabled,
        };
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

    /// <summary>Normalize the next scope against the action's SupportedScopes so unsupported states
    /// (e.g. Draft/Undraft/Equip's AnyOccurrence) fall back to the action's default scope instead of
    /// being written into actionTuning.</summary>
    private static SqueakActionScope NormalizeFor(SqueakAction action, SqueakActionScope scope)
        => SqueakActionDefinitions.NormalizeScope(action, scope);
}
