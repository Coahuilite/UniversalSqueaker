using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US footer: left build identity, right save state. Reads typed string/bool bindings
/// and is non-interactive.
/// </summary>
public sealed class UsKernelFooterWidget : IUiWidget
{
    public const string Kind = "us/footer";

    public const float FooterHeight = 28f;
    private const float Padding = 10f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            Kind,
            () => new UsKernelFooterWidget(),
            new[] { "Id", "Kind", "Height", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<string>("build-identity", elementPath);
        bindings.ValidateValue<string>("save-status", elementPath);
        bindings.ValidateValue<bool>("is-dirty", elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        return FooterHeight;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        ctx.Bindings.TryGet("build-identity", out string buildIdentity);
        ctx.Bindings.TryGet("save-status", out string saveStatus);
        bool isDirty = ctx.Bindings.TryGet("is-dirty", out bool dirty) && dirty;

        UiThemeDraw.Surface(new Rect(rect.x, rect.y, rect.width, 1f), ctx.Theme, ctx.Theme.Divider, ctx.Theme.Divider);

        UsKernelDraw.Label(
            new Rect(rect.x + Padding, rect.y, Math.Max(1f, rect.width * 0.5f - Padding), rect.height),
            buildIdentity,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        Color statusColor = saveStatus switch
        {
            "Failed" => ctx.Theme.Danger,
            "Saving" => ctx.Theme.AccentGold,
            _ => ctx.Theme.TextSecondary,
        };
        if (isDirty && saveStatus != "Failed")
        {
            statusColor = ctx.Theme.AccentGold;
        }

        string prefix = saveStatus == "Saving" || isDirty ? "● " : "";
        Rect statusRect = new(rect.x + rect.width * 0.5f, rect.y, Math.Max(1f, rect.width * 0.5f - Padding), rect.height);
        UsKernelDraw.HelpHover(statusRect, ctx, "us/page-title/apply");
        UsKernelDraw.Label(
            statusRect,
            prefix + SaveStatusText(ctx, saveStatus),
            ctx.Theme,
            statusColor,
            UiFont.Tiny,
            TextAnchor.MiddleRight);
    }

    /// <summary>
    /// Display-only translation of the save-status token. All status logic in Draw (color, dot
    /// prefix) keeps matching on the raw token from the binding - never on translated text. The
    /// token set is a closed enum surface: an unrecognized token is drift (a new state without a
    /// Keyed entry), so it is reported once per value and still rendered raw as the last resort.
    /// </summary>
    private static readonly HashSet<string> ReportedStatusTokens = new HashSet<string>();

    private static string SaveStatusText(UiWidgetContext ctx, string token)
    {
        string? key = token switch
        {
            "Idle" => "US.Footer.SaveStatus.Idle",
            "Saving" => "US.Footer.SaveStatus.Saving",
            "Saved" => "US.Footer.SaveStatus.Saved",
            "Failed" => "US.Footer.SaveStatus.Failed",
            "Unknown" => "US.Footer.SaveStatus.Unknown",
            _ => null,
        };
        if (key == null)
        {
            if (ReportedStatusTokens.Add(token ?? ""))
            {
                Log.Error("[US] footer save-status token '" + token + "' has no Keyed entry; "
                    + "extend UsFooterWidget.SaveStatusText and both language tables.");
            }
            return token ?? "";
        }
        return ctx.Translation.Translate(key);
    }
}
