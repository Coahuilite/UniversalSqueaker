using System;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Workspace-only navigation for the five player tasks. It intentionally does not expose every
/// child control: each destination owns one coherent flow in the centre workspace.
/// </summary>
public sealed class UsNavWidget : IUiWidget
{
    public const string Kind = "us/nav";

    private const float ItemHeight = 52f;
    private const float Gap = 5f;
    private const float SidePadding = 10f;
    private const float TopPadding = 12f;

    private static readonly (string Tab, string Label, string Description)[] Workspaces =
    {
        ("Overview", "Overview", "Routing and playback"),
        ("Distance", "Distance", "Range and attenuation"),
        ("Packs", "VoicePacks", "Filter, domain, then pack"),
        ("Tuning", "Tuning", "Layered action and mood rules"),
        ("Presets", "Presets", "Def-provided baselines")
    };

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            Kind,
            () => new UsNavWidget(),
            new[] { "Id", "Kind", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec value)
    {
        spec = value ?? throw new ArgumentNullException(nameof(value));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<string>("active-tab", elementPath);
        bindings.ValidateAction<string>("set-tab", elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        // Draw starts at rect.y + TopPadding and ends after the last item; no trailing TopPadding.
        return TopPadding + Workspaces.Length * ItemHeight + (Workspaces.Length - 1) * Gap;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiThemeDraw.BackgroundPlane(rect, ctx.Theme);
        UiThemeDraw.Surface(rect, ctx.Theme, Color.clear, ctx.Theme.Border);
        ctx.Bindings.TryGet("active-tab", out string activeTab);

        float y = rect.y + TopPadding;
        float innerWidth = Math.Max(1f, rect.width - SidePadding * 2f);
        for (int i = 0; i < Workspaces.Length; i++)
        {
            (string tab, string label, string description) = Workspaces[i];
            Rect itemRect = new(rect.x + SidePadding, y, innerWidth, ItemHeight);
            bool active = string.Equals(tab, activeTab, StringComparison.Ordinal);
            bool hovered = Mouse.IsOver(itemRect);
            UiThemeDraw.StatusTreatment(itemRect, ctx.Theme, active ? UiStatusTone.Active : UiStatusTone.Neutral);
            UiThemeDraw.AccentRail(itemRect, ctx.Theme, active, 3f);
            UsKernelDraw.Label(new Rect(itemRect.x + 10f, itemRect.y + 6f, Math.Max(1f, itemRect.width - 18f), 18f), label,
                ctx.Theme, active ? ctx.Theme.TextOnGold : ctx.Theme.TextPrimary, UiFont.Small, TextAnchor.MiddleLeft);
            UsKernelDraw.Label(new Rect(itemRect.x + 10f, itemRect.y + 25f, Math.Max(1f, itemRect.width - 18f), 16f), description,
                ctx.Theme, active ? ctx.Theme.TextOnGold : hovered ? ctx.Theme.TextPrimary : ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft);
            if (UiNative.Button(itemRect))
            {
                ctx.Bindings.Invoke("set-tab", tab);
            }
            y += ItemHeight + Gap;
        }
    }
}
