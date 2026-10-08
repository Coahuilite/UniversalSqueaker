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

    public SqueakDiagnosticsPanel()
    {
        // Non-modal diagnostic panel: never pauses, never absorbs surroundings, camera stays live.
        // The chrome flags the shell owns (close button, background, shadow) are deliberately absent here.
        forcePause = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
        draggable = true;
        closeOnCancel = false; // Esc handled by the two-press arm below.
        // FL-IC2 native eligibility (frozen handoff §5): a window with closeOnCancel=false never hears the
        // Cancel key unless it opts into the public Verse field itself. Setting it here is the one-line
        // assignment the frozen contract names for the consumer; it cannot steal a key from a window above,
        // because eligibility is ANDed with the stack's input test.
        forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
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

    /// <summary>The wide master/detail content width, used once a row is selected.</summary>
    internal const float ExpandedContentWidth = 680f;

    /// <summary>The wide master/detail content height, used once a row is selected AND the scaled
    /// screen is non-compact (DX1.2: <see cref="WideMinimumScreenHeight"/> plus the fit checks).</summary>
    internal const float ExpandedContentHeight = 560f;

    /// <summary>DX1.2: content height of the NARROW selected shape - the in-window list/detail with
    /// Back, search and paging kept - used on compact scaled screens and whenever the wide
    /// 680x(76+560)=680x636 outer window would not fit. The DX1 ruling records the starting point
    /// 600x~456 outer; the measured value this slice ships with is 600x(76+380)=600x456.</summary>
    internal const float NarrowSelectedContentHeight = 380f;

    /// <summary>DX1.2 (r2): the scaled screen height from which the wide master/detail may be
    /// chosen at all. 900 puts the whole 768 class (1024x768 at 100% = 768, at 125% = 614) on the
    /// narrow navigation shape per the contract, and keeps the 1080 class at 100%/110%
    /// (1080/982) on the existing master/detail. A pure "does 636 fit" test was the defect this
    /// constant closes: it passed at 1024x768.</summary>
    internal const float WideMinimumScreenHeight = 900f;

    /// <summary>DX1.1: the shell's REAL chrome budget - the title bar plus the bottom inset that
    /// ContentRect subtracts. Every state's outer height is THIS plus the state's content height;
    /// chrome is never reverse-derived from a content rect that may itself have been clamped (the
    /// old 44px outer first frame clamped the content to 1px and the panel then "measured" 43px of
    /// chrome out of the wreckage).</summary>
    private float ChromeHeight => TitleBarHeight + SidePadding;

    protected override Func<Vector2>? InitialSizePolicy => () => new Vector2(
        Mathf.Clamp(CollapsedWidth, 320f, Mathf.Max(320f, Verse.UI.screenWidth - 40f)),
        ChromeHeight + BarContentHeight);

    protected override UiTheme Theme => WindowTheme;

    protected override string Title => Translator.Translate("US.Diagnostics.Title");

    protected override string CloseText => Translator.Translate("US.Diagnostics.Close");

    protected override bool PrerequisiteVerified => UniversalSqueakerMod.PrerequisiteVerified;

    /// <summary>
    /// This window's own audit scope over its own host subscription. R3-B fix 4: the scope exists in every
    /// logging mode (the developer geometry commands need a scope to target); only the text-fit audit inside it
    /// follows the logging policy.
    /// </summary>
    private UniversalSqueaker.UI.UsTextFitAudit? audit;

    protected override UiHost CreateHost()
    {
        UiHost host = UsDiagnosticsHost.CreateMain(source);
        // Per-HOST audit (FL-20): before this, this window shared the settings window's one process-wide
        // Enabled/sink, so its overflow findings were logged as if the settings page had produced them and
        // were measured with the settings window's ruler. Its own subscription ends both.
        //
        // `SqueakLog.ShouldEmitDev` remains the FIT-AUDIT policy (unchanged); it no longer decides whether a
        // scope exists, because the layout-diagnosis commands need one in every mode.
        audit = UniversalSqueaker.UI.UsTextFitAudit.Open(host, SqueakLog.ShouldEmitDev);
        // DT1 (user ruling): the diagnostics panel is NOT a settings-dev-panel target - its outline is
        // switched only by its own Debug Action, which reads LiveHostForDebug below.
        return host;
    }

    /// <summary>DT1: the panel's live page host for the Debug Actions outline toggle (null before the
    /// first pass and after teardown - callers treat null as "nothing to toggle").</summary>
    internal UiHost? LiveHostForDebug => Host;

    /// <summary>The window owns collapsed GEOMETRY (the source owns the flag): shrink to a bar of
    /// chrome + one row, restore the remembered rect on expand.</summary>
    protected override void BeforeDraw(Rect contentRect)
    {
        // Drain this host's bounded diagnostic ring before this pass adds to it (FL-20).
        audit?.Publish();

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

        // DX1.2 (r2 correction): "636 fits inside 728" is NOT the contract's condition - at
        // 1024x768 both old fits were true and the panel still picked the 680x636 master/detail,
        // which the contract explicitly rejects. The wide shape now additionally requires a
        // NON-COMPACT scaled screen: WideMinimumScreenHeight (900) keeps the 768 class - the
        // user's real 1024x768 at 100% AND 125% UI scale (768 and 614) - on the narrow
        // navigation shape at 600x456, while the 1080 class at 100%/110% (1080/982) keeps the
        // existing master/detail. Recorded scaled inputs: 1024x768@1.0 -> 768 < 900 -> narrow;
        // 1920x1080@1.0 -> 1080 >= 900 -> wide; 1920x1080@1.25 -> 864 < 900 -> narrow (the
        // honest consequence of the same rule at high scale, listed for the human check).
        // Heights: collapsed 76+44=120, empty 76+240=316, narrow selected 76+380=456,
        // wide selected 76+560=636; the final clamp keeps the whole window - not a 24px grab
        // strip - inside the screen. The first collapse and a collapse-after-expand run through
        // THIS rule, so both land on the same geometry (DX1.1's symmetry clause).
        float screenCapW = Math.Max(320f, Verse.UI.screenWidth - 40f);
        float maxOuterH = Math.Max(ChromeHeight + BarContentHeight, Verse.UI.screenHeight - 40f);
        bool fitsWide = !collapsed && !empty
            && Verse.UI.screenHeight >= WideMinimumScreenHeight
            && ChromeHeight + ExpandedContentHeight <= maxOuterH
            && ExpandedContentWidth <= screenCapW;
        float width;
        float contentHeight;
        if (collapsed)
        {
            width = Math.Min(CollapsedWidth, screenCapW);
            contentHeight = BarContentHeight;
        }
        else if (empty)
        {
            width = Math.Min(EmptyStateWidth, screenCapW);
            contentHeight = EmptyStateContentHeight;
        }
        else if (fitsWide)
        {
            width = Math.Min(ExpandedContentWidth, screenCapW);
            contentHeight = ExpandedContentHeight;
        }
        else
        {
            width = Math.Min(EmptyStateWidth, screenCapW);
            contentHeight = NarrowSelectedContentHeight;
        }

        float height = Math.Min(ChromeHeight + contentHeight, maxOuterH);
        // DX1.2 (PM review 2026-10-07): a STATE change must keep the WHOLE window inside the scaled
        // screen, so the position is corrected against the FINAL outer size, not only against the
        // 24px grab strip. A centred 1024x768 first open (bar y=324, h=120) that expands to 600x456
        // would otherwise put bottom=780 past the screen; the same rule catches a player-dragged low
        // collapsed bar. Ordinary dragging is untouched: WindowOnGUI keeps its grab-strip clamp for
        // moves that do not change the size.
        float x = windowRect.x;
        float y = windowRect.y;
        if (collapsed != lastCollapsed || empty != lastEmpty)
        {
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Verse.UI.screenWidth - width));
            y = Mathf.Clamp(y, 0f, Mathf.Max(0f, Verse.UI.screenHeight - height));
        }

        windowRect = new Rect(x, y, width, height);
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

    /// <summary>
    /// The two-press Esc policy (round 9), migrated onto the shell's extension point per the frozen
    /// FL-IC2 contract: the page ladder (option menu → held capture → open edit → nearest CancelBind)
    /// always gets first refusal, and this policy is asked only after the ladder declined. Answering true
    /// makes the shell consume the key on the policy's behalf - the old override drew no consumption at all
    /// (the pre-migration note recorded that leak as live-walkthrough material), so the leak question
    /// now has a named shell-level answer; which window a REAL multi-window stack hands the key to remains
    /// the human-pass observation it was.
    /// </summary>
    protected override bool TryHandleUnansweredCancel()
    {
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

        return true;
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
        // Final drain and release, before the shell disposes the host and its subscription.
        audit?.Dispose();
        audit = null;
        base.PreClose();
        SqueakDiagnosticsOverlay.NotifyPanelClosed();
    }
}
