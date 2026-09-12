using System;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker;

/// <summary>
/// The main diagnostics window (round-9 contract): the library shell (<see cref="UiWindowHost"/>)
/// around one kernel page in master-detail shape - left list column (search + 8-row pages),
/// right detail column following the live selection, plus the detach-to-lock and collapsed-bar
/// states. Closing it ends the whole diagnostics session (the overlay cascades every detail
/// window closed). The file draws ZERO raw backend calls: chrome is the shell's, content is the
/// widget family's, hover/keys are the session's - so it leaves the gate 14 whitelist with this
/// rewrite (only-shrink ratchet landing).
/// </summary>
internal sealed class SqueakDiagnosticsPanel : UiWindowHost
{
    private const float KeepGrabPx = 24f;
    private const float EscArmSeconds = 3f;
    // 09 §3.3's authorised collapsed geometry (26 -> 32); the bar widget owns the number.
    private const float BarContentHeight = UsDiagBarWidget.BarHeight;

    private static readonly UiTheme WindowTheme = UiTheme.DarkGold;

    private readonly UsDiagnosticsSessionSource source = new();
    private float escArmedUntil = -1f;
    private bool lastCollapsed;
    private Rect expandedRect = Rect.zero;

    public SqueakDiagnosticsPanel()
    {
        // Non-modal diagnostic panel: never pauses, never absorbs surroundings, camera stays live.
        // The chrome flags the shell owns (close button, background, shadow) are deliberately absent here.
        forcePause = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
        draggable = true;
        closeOnCancel = false; // Esc handled by the two-press arm below.
        closeOnAccept = false;
        closeOnClickedOutside = false;
        onlyOneOfTypeAllowed = true;
        focusWhenOpened = false;
        onlyDrawInDevMode = true;
    }

    // 620 -> 680 (lead-authorised, dev-only): the detail column needs 320 for its 40/60 value split
    // without ellipsizing Chinese values (defect D4).
    protected override Func<Vector2>? InitialSizePolicy => () => new Vector2(680f, 560f);

    protected override UiTheme Theme => WindowTheme;

    protected override string Title => Translator.Translate("US.Diagnostics.Title");

    protected override string CloseText => Translator.Translate("US.Diagnostics.Close");

    protected override bool PrerequisiteVerified => UniversalSqueakerMod.PrerequisiteVerified;

    protected override UiHost CreateHost() => UsDiagnosticsHost.CreateMain(source);

    /// <summary>The window owns collapsed GEOMETRY (the source owns the flag): shrink to a bar of
    /// chrome + one row, restore the remembered rect on expand.</summary>
    protected override void BeforeDraw(Rect contentRect)
    {
        // The collapsed bar carries a visible close (09 §3.3 rule 3): about-to-draw is the same
        // mid-draw close point the detail window already uses for its IsValid self-check.
        if (source.CloseRequested)
        {
            Close();
            return;
        }

        bool collapsed = source.Collapsed;
        if (collapsed == lastCollapsed)
        {
            return;
        }

        if (collapsed)
        {
            expandedRect = windowRect;
            float chrome = Math.Max(0f, windowRect.height - contentRect.height);
            windowRect = new Rect(windowRect.x, windowRect.y, windowRect.width, chrome + BarContentHeight);
        }
        else if (expandedRect.height > 1f)
        {
            windowRect = expandedRect;
        }

        lastCollapsed = collapsed;
    }

    public override void WindowOnGUI()
    {
        base.WindowOnGUI();
        // Keep the title bar grabbable: the panel can never be dragged fully off-screen.
        windowRect.x = Mathf.Clamp(windowRect.x, Mathf.Min(0f, KeepGrabPx - windowRect.width), Verse.UI.screenWidth - KeepGrabPx);
        windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Verse.UI.screenHeight - KeepGrabPx));
    }

    public override void OnCancelKeyPressed()
    {
        // Two presses within EscArmSeconds close. NOTE: no Event.current consumption - the UiNative
        // seam exposes none and the gate-14 contract requires zero raw backend calls in this file.
        // Whether an unconsumed Esc leaks into game cancel/selection is a live-walkthrough check
        // item; if it leaks, that is the shell-level gap to take to FerriteLib as round-4 material.
        float now = Time.realtimeSinceStartup;
        if (now > escArmedUntil)
        {
            escArmedUntil = now + EscArmSeconds;
        }
        else
        {
            escArmedUntil = -1f;
            Close();
        }
    }

    /// <summary>Terminal notices in US's own vocabulary (the library ships zero strings).</summary>
    protected override void DrawNotice(Rect rect, UiWindowNotice notice)
    {
        UiThemeDraw.Surface(rect, WindowTheme, WindowTheme.Panel, WindowTheme.Border);
        Rect body = rect.ContractedBy(24f);
        bool prerequisite = notice == UiWindowNotice.Prerequisite;

        UsKernelDraw.Label(
            new Rect(body.x, body.y, body.width, 30f),
            Translator.Translate(prerequisite ? "US.Settings.Prerequisite.Title" : "US.Diagnostics.PageUnavailable.Title"),
            WindowTheme, WindowTheme.TextPrimary, UiFont.Medium);
        UsKernelDraw.Label(
            new Rect(body.x, body.y + 38f, body.width, Mathf.Max(1f, body.height - 38f)),
            Translator.Translate(prerequisite ? "US.Settings.Prerequisite.Body" : "US.Diagnostics.PageUnavailable.Body"),
            WindowTheme, WindowTheme.TextSecondary, UiFont.Small);
    }

    /// <summary>Plain log line, deliberately NOT a usdiag protocol event: the frozen vocabulary
    /// gates a registry, and a dev-panel failure has no business editing it.</summary>
    protected override void OnDrawFailure(Exception error)
    {
        Log.Warning("[UniversalSqueaker] Diagnostics panel draw failed: " + SqueakLogText.SanitizeExceptionMessage(error.Message));
    }

    public override void PreClose()
    {
        base.PreClose();
        SqueakDiagnosticsOverlay.NotifyPanelClosed();
    }
}
