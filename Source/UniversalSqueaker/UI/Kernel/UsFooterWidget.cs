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
        bindings.ValidateValue<bool>("save-status-visible", elementPath);
        bindings.ValidateValue<bool>("is-dirty", elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        // The band must fit what it draws. The 2026-09-14 run reported page-root/footer needing 33px in this
        // 28px band once the build identity wrapped - the footer was the only widget in the page still
        // returning a constant. Both halves are measured at their own half width, and 28 stays the floor, so
        // a wide window keeps the shipped height exactly.
        ctx.Bindings.TryGet("build-identity", out string buildIdentity);
        ctx.Bindings.TryGet("save-status", out string saveStatus);
        float half = Math.Max(1f, ctx.ViewWidth * 0.5f - Padding);
        float left = ctx.Metrics.MeasureText(buildIdentity ?? "", UiFont.Tiny, half);
        bool visible = StatusVisible(ctx);
        bool dirty = ctx.Bindings.Get<bool>("is-dirty");
        float right = visible ? ctx.Metrics.MeasureText(StatusLabel(ctx, saveStatus, dirty), UiFont.Tiny, half) : 0f;
        return Math.Max(FooterHeight, Math.Max(left, right) + 6f);
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
            ctx.Theme.TextDisabled,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        if (!StatusVisible(ctx)) return;

        // Accent discipline (05 §3.1): "saving" and "dirty" are activity, not "currently in effect" -
        // the ● prefix and the text already carry that, so the color only steps up from secondary to
        // primary. The destructive/handling failure keeps its status color (Danger).
        Color statusColor = saveStatus switch
        {
            "Failed" => ctx.Theme.Danger,
            "Saving" => ctx.Theme.TextPrimary,
            _ => ctx.Theme.TextSecondary,
        };
        if (isDirty && saveStatus != "Failed")
        {
            statusColor = ctx.Theme.TextPrimary;
        }

        Rect statusRect = new(rect.x + rect.width * 0.5f, rect.y, Math.Max(1f, rect.width * 0.5f - Padding), rect.height);
        UsKernelDraw.HelpHover(statusRect, ctx, "us/page-title/apply");
        UsKernelDraw.Label(
            statusRect,
            StatusLabel(ctx, saveStatus, isDirty),
            ctx.Theme,
            statusColor,
            UiFont.Tiny,
            TextAnchor.MiddleRight);
    }

    // A pending edit is necessary state even after the transient save sentence has expired.
    private static bool StatusVisible(UiWidgetContext ctx)
    {
        return ctx.Bindings.Get<bool>("save-status-visible") || ctx.Bindings.Get<bool>("is-dirty");
    }

    private static string StatusLabel(UiWidgetContext ctx, string token, bool dirty)
    {
        return (token == "Saving" || dirty ? "● " : "") + SaveStatusText(ctx, token);
    }

    private static readonly HashSet<string> ReportedStatusTokens = new HashSet<string>();

    /// <summary>Translate the raw status token only for display; colour and marker logic use the token.</summary>
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
