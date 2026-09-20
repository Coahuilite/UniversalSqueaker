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

    private static readonly UiTheme WindowTheme = UsTheme.Surface();

    private readonly UsDiagnosticsSessionSource source = new();
    private float escArmedUntil = -1f;
    private bool lastCollapsed;
    private bool lastEmpty;
    private bool opened;
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
    // without ellipsizing Chinese values (defect D4). The size is also clamped to the REAL coordinate
    // space: at high UIScale the scaled screen is narrower than 680 and a fixed initial width would push
    // the detail column off-screen before the responsive switch could help (09 §3.5).
    // Redesign 2026-09-14 (feedback: "at the minimum resolution it nearly fills the screen, and it is mostly
    // empty"). The panel now OPENS COLLAPSED as its bar, and only widens to the master/detail shape once a row
    // is actually selected - so an empty panel is a strip, never a full screen. The two content sizes below are
    // the content rect only; the shell adds the chrome.
    internal const float CollapsedWidth = 460f;

    /// <summary>Width while nothing is selected. Deliberately BELOW the page's narrow breakpoint (inner width
    /// 600 - 16 pads &lt; 592), so the page presents its list-only shape instead of a list plus an empty detail
    /// column - the blank right half the feedback reported.</summary>
    internal const float EmptyStateWidth = 600f;

    /// <summary>Content height while nothing is selected: the search row, the list header and the empty note.</summary>
    internal const float EmptyStateContentHeight = 240f;

    /// <summary>The wide master/detail content size, used once a row is selected.</summary>
    internal const float ExpandedContentWidth = 680f;

    /// <summary>The wide master/detail content height.</summary>
    internal const float ExpandedContentHeight = 560f;

    protected override Func<Vector2>? InitialSizePolicy => () => new Vector2(
        Mathf.Clamp(CollapsedWidth, 320f, Mathf.Max(320f, Verse.UI.screenWidth - 40f)),
        UsDiagBarWidget.BarHeight);

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

        // The responsive decision is fed BEFORE this pass's layout: the source derives the narrow
        // presentation from the width the shell is about to arrange in, so the page's two VisibleKey
        // presentations and the engine's own Breakpoint evaluation read one coordinate space. The same
        // call moves the layout clock when the width crossed the presentation threshold, because a
        // read-only VisibleKey binding announces no revision of its own. Host is null on the first
        // pass (chrome draws before the shell creates one); the width is still recorded.
        UsDiagnosticsHost.ApplyContentWidth(source, Host, contentRect.width);

        if (!opened)
        {
            // Default state is the collapsed bar: a freshly opened diagnostics session must not own the screen.
            opened = true;
            source.Collapsed = true;
        }

        bool collapsed = source.Collapsed;
        bool empty = !collapsed && source.Detail == null;
        if (collapsed == lastCollapsed && empty == lastEmpty)
        {
            return;
        }

        float chrome = Math.Max(0f, windowRect.height - contentRect.height);
        float screenCap = Math.Max(120f, Verse.UI.screenWidth - 40f);
        float width = collapsed
            ? Math.Min(CollapsedWidth, screenCap)
            : empty
                ? Math.Min(EmptyStateWidth, screenCap)
                : Math.Min(ExpandedContentWidth, screenCap);
        float height = collapsed
            ? chrome + BarContentHeight
            : chrome + (empty ? EmptyStateContentHeight : ExpandedContentHeight);
        windowRect = new Rect(windowRect.x, windowRect.y, width, height);

        lastCollapsed = collapsed;
        lastEmpty = empty;
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
