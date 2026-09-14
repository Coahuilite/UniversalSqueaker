using System;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker;

/// <summary>
/// One detachable per-pawn detail window (round-9 lock ruling): created by the overlay when a
/// row click or lock button detaches a pawn's details, tracked off-screen until it is closed or
/// unlocked - closing IS the unlock (the overlay's NotifyDetailWindowClosed), and the pawn dying,
/// despawning or the session ending closes this window in return (IsValid self-check).
/// Same shell, same widget family, page without list/pager/search; its collapsed bar identifies the
/// pinned pawn (09 §3.3 rule 5). Zero raw backend calls, so it never joins the gate 14 whitelist.
/// </summary>
internal sealed class SqueakDiagnosticsDetailWindow : UiWindowHost
{
    private const float KeepGrabPx = 24f;
    private const float EscArmSeconds = 3f;
    private const float BarContentHeight = UsDiagBarWidget.BarHeight;

    private static readonly UiTheme WindowTheme = UsTheme.Surface();

    private readonly Pawn pinnedPawn;
    private readonly UsDiagnosticsDetailSource source;
    private float escArmedUntil = -1f;
    private bool lastCollapsed;
    private Rect expandedRect = Rect.zero;

    public SqueakDiagnosticsDetailWindow(Pawn pawn)
    {
        if (pawn == null) throw new ArgumentNullException(nameof(pawn));
        pinnedPawn = pawn;
        source = new UsDiagnosticsDetailSource(pawn);

        forcePause = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
        draggable = true;
        closeOnCancel = false;
        closeOnAccept = false;
        closeOnClickedOutside = false;
        onlyOneOfTypeAllowed = false; // multiple locks are allowed by ruling.
        focusWhenOpened = false;
        onlyDrawInDevMode = true;
    }

    // 320 -> 340: the same 40/60 value column the main panel now gives its detail (defect D4).
    protected override Func<Vector2>? InitialSizePolicy => () => new Vector2(340f, 480f);

    protected override UiTheme Theme => WindowTheme;

    protected override string Title => pinnedPawn.LabelShort + " - " + Translator.Translate("US.Diagnostics.Title");

    protected override string CloseText => Translator.Translate("US.Diagnostics.UnlockClose");

    protected override bool PrerequisiteVerified => UniversalSqueakerMod.PrerequisiteVerified;

    protected override UiHost CreateHost() => UsDiagnosticsHost.CreateDetail(source);

    protected override void BeforeDraw(Rect contentRect)
    {
        // Self-close the moment the pinned pawn stopped being tracked (dead/despawned/map change/
        // session end). BeforeDraw is inside THIS window's own pass, so Close here is the same
        // mid-draw close a button click performs.
        if (!source.IsValid || source.CloseRequested)
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
        windowRect.x = Mathf.Clamp(windowRect.x, Mathf.Min(0f, KeepGrabPx - windowRect.width), Verse.UI.screenWidth - KeepGrabPx);
        windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Verse.UI.screenHeight - KeepGrabPx));
    }

    public override void OnCancelKeyPressed()
    {
        // Same two-press arm and the same no-consumption contract as the main window.
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

    protected override void OnDrawFailure(Exception error)
    {
        Log.Warning("[UniversalSqueaker] Diagnostics detail window draw failed: " + SqueakLogText.SanitizeExceptionMessage(error.Message));
    }

    public override void PreClose()
    {
        base.PreClose();
        SqueakDiagnosticsOverlay.NotifyDetailWindowClosed(pinnedPawn);
    }
}
