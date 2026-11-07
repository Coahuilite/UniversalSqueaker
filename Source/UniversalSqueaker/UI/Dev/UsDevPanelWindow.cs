using System;
using System.Collections.Generic;

using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI.Dev;

/// <summary>
/// DT1 (2026-10-07 user ruling): the small draggable developer panel for the US SETTINGS workspace.
/// Its targets are the settings page and its current confirmation dialog (including Remix) - the in-game
/// diagnostics panel is served solely by its own Debug Action, never from here. The panel exists so
/// capture, outline and the geometry report are reachable from every settings page without
/// scrolling back to the Overview diagnostics band, and each command names its REAL target.
///
/// <para>
/// <b>Composition, not a new framework.</b> The window is the same library shell the diagnostics
/// panel uses (a <see cref="UiWindowHost"/> subclass opened through <see cref="UiWindowCatalog"/>
/// with <see cref="UiWindowOptions"/> - the FL DT1 contract's blessed composition), the page is
/// programmatic XML fed through the SAME production parser the diagnostics spec uses, and the
/// options list registers through the dropdown's own <c>BindOptions</c> seam exactly like the
/// settings page's filter dropdowns (a read-only value binding is NOT an options binding - the
/// production widget validation refuses it).
/// </para>
///
/// <para>
/// <b>Entry and lifetime.</b> Reachable from the Overview diagnostics band (the settings window is
/// by definition open there) and from the Debug Actions menu, which refuses without a settings
/// target; closing the settings window closes the panel, and Escape belongs to the window STACK, never
/// to the tool by default - see the constructor and the PRE-CONTENT <c>WindowOnGUI</c> recompute. The
/// panel's OWN host never registers as a target - the tool's layout must not enter the dump it produces
/// (DT1 pass condition 2).
/// </para>
/// </summary>
internal sealed class UsDevPanelWindow : UiWindowHost
{
    private const string Consumer = "coahuilite.universalsqueaker";

    // UIR-006: the shared chrome bag (repainted on a live palette switch), like the settings window's
    // own chrome. The panel is a companion of the settings workspace, so it wears the page's palette.
    private static readonly UiTheme PanelTheme = UsTheme.ChromeTheme;

    /// <summary>The one open panel (band button / Debug Action open it; settings PostClose closes it).</summary>
    internal static UsDevPanelWindow? Active { get; private set; }

    private readonly UiBindings bindings = new();
    private readonly DevPanelSource source = new();
    private string statusKey = "US.DevPanel.Status.Idle";
    private string statusArg = "";
    private UiHost? reportingTarget;
    private int reportingAfterPass = -1;
    private string reportingLabel = "";

    private UsDevPanelWindow()
    {
        // F01/US-UI1 review1 (PM observation 2): KEYBOARD COOPERATION, not just layering. The real
        // WindowStack hands Cancel to the first (top-down) window that is eligible -
        // (closeOnCancel || forceCatchAcceptAndCancelEventEvenIfUnfocused) - and LetsInput decides,
        // without any mouse-position test; a SubSuper tool with closeOnCancel=true would therefore take
        // EVERY Esc away from the settings window's own menus, edits and business layers, and a tool that
        // declined would still close itself at base. The panel instead owns no unconditional claim:
        // Verse's own fields decide its eligibility at the top of every window pass (WindowOnGUI, before
        // Verse's dispatch): while the panel's page
        // has an open menu, held capture or focused edit the tool is the top layer and answers exactly
        // one layer of ITS OWN; when it is idle it is not eligible at all, and Verse's search continues
        // down the stack to the settings window's ladder and, at the settings root, to Verse's meaning
        // of the key. The panel exits through its close button or with the settings window (PostClose) -
        // it never swallows or hijacks another window's key, and Enter is gated by the same field.
        layer = WindowLayer.SubSuper;
        closeOnCancel = false;
        forcePause = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
        closeOnAccept = false;
        closeOnClickedOutside = false;
        focusWhenOpened = false;
        onlyDrawInDevMode = true;
        HostDetached += _ => reportingTarget = null;
        // draggable / one-instance / normal-size arrive through UiWindowOptions at the catalog door.

        bindings.BindChoices<string>("dev-target-options", () => source.Targets);
        bindings.BindReadOnly<bool>("dev-capture", () => source.CaptureOn);
        bindings.BindReadOnly<bool>("dev-outline", () => source.OutlineOn);
        bindings.BindReadOnly<string>("dev-status", () => statusArg.Length > 0
            ? Translator.Translate(statusKey) + " " + statusArg
            : Translator.Translate(statusKey));
        bindings.BindValue("dev-target", () => source.TargetKey, (string value) =>
        {
            source.TargetKey = value ?? "settings";
            Requery();
        });
        bindings.BindValue("dev-capture-toggle", () => source.CaptureOn, (bool value) =>
        {
            source.SetCapture(value);
            statusKey = "US.DevPanel.Status.Capture";
            statusArg = source.TargetLabel + " " + (source.CaptureOn ? "on" : "off");
        });
        bindings.BindValue("dev-outline-toggle", () => source.OutlineOn, (bool value) =>
        {
            source.SetOverlay(value);
            statusKey = "US.DevPanel.Status.Overlay";
            statusArg = source.TargetLabel + " " + (source.OutlineOn ? "on" : "off");
        });
        bindings.BindCommand("dev-page-overview", () => Page("Overview"));
        bindings.BindCommand("dev-page-packs", () => Page("Packs"));
        bindings.BindCommand("dev-page-tuning", () => Page("Tuning"));
        // UIR-001: the Distance workspace retired with the navigation; the panel pages the five real ones.
        bindings.BindCommand("dev-page-advanced", () => Page("Advanced"));
        bindings.BindCommand("dev-page-presets", () => Page("Presets"));
        bindings.BindCommand("dev-report", Report);
        bindings.BindReadOnly<bool>("dev-sample-ready", () => SampleTarget()?.CanQueueUiCostSample == true);
        bindings.BindReadOnly<string>("dev-sample-status", () => SampleTarget()?.UiCostSampleStatus
            ?? Translator.Translate("US.DevPanel.Sample.Unavailable"));
        bindings.BindCommand("dev-sample", () => SampleTarget()?.QueueUiCostSample(),
            () => SampleTarget()?.CanQueueUiCostSample == true);
    }

    /// <summary>
    /// Open the panel. Returns false when there is no settings target (the tool never stands alone);
    /// the Debug Action turns that answer into a message, the settings band cannot hit it.
    /// </summary>
    internal static bool OpenOrFocus()
    {
        if (UsDevPanelTargets.SettingsHost == null) return false;

        UiWindowCatalog opened = catalog ??= new UiWindowCatalog(Find.WindowStack);
        if (!registered)
        {
            opened.Register(Consumer, WindowKind, new UiWindowOptions
            {
                Draggable = true,
                Resizeable = false,
                ForcePause = false,
                PreventCameraMotion = false,
                AbsorbInputAroundWindow = false,
                CloseOnAccept = false,
                CloseOnCancel = false, // review1 observation 2: the key belongs to the stack, not to the tool
                AllowMultipleInstances = false,
                NormalSize = new Vector2(360f, 240f),
            }, _ =>
            {
                UsDevPanelWindow panel = new();
                Active = panel;
                return panel;
            });
            registered = true;
        }

        opened.Open(new UiWindowKey(Consumer, WindowKind, ""));
        return true;
    }

    internal static void CloseIfOpen()
    {
        Active?.Close();
    }

    private const string WindowKind = "dev-panel";
    private static UiWindowCatalog? catalog;
    private static bool registered;

    protected override UiHost CreateHost()
    {
        UiLayoutManifest manifest = UiLayoutManifest.Parse(PageXml);
        var host = VerseUiDiagnosticLog.CreateHost(
            UsUiDiagnostics.Consumer, Consumer,
            manifest,
            bindings,
            PanelTheme,
            VerseFerriteTextMetrics.Instance,
            new UsKernelTranslation(), options: UsUiDiagnostics.Options());
        UsUiDiagnostics.Adopt(host);
        return host;
    }

    protected override UiTheme Theme => PanelTheme;

    protected override string Title => Translator.Translate("US.DevPanel.Title");

    protected override string CloseText => Translator.Translate("US.Common.Cancel");

    protected override bool PrerequisiteVerified => SqueakerMod.PrerequisiteVerified;

    protected override Func<Vector2>? InitialSizePolicy => () => new Vector2(
        360f,
        Math.Max(220f, Math.Min(240f, Verse.UI.screenHeight - 40f)));
    /// <summary>Terminal notices in US's own vocabulary (the library ships zero strings) - the same
    /// shape the settings window and the diagnostics panel draw.</summary>
    protected override void DrawNotice(Rect rect, UiWindowNotice notice)
    {
        UiThemeDraw.Surface(Execution, VerseUiExecution.Rect(rect), PanelTheme, PanelTheme[UiPaletteRole.PanelSurface], PanelTheme[UiPaletteRole.Border]);
        Rect body = rect.ContractedBy(16f);
        bool prerequisite = notice == UiWindowNotice.Prerequisite;
        UsKernelDraw.Label(Execution, VerseUiExecution.Rect(new Rect(body.x, body.y, body.width, 24f)), Translator.Translate(prerequisite ? "US.Settings.Prerequisite.Title" : "US.Diagnostics.PageUnavailable.Title"), PanelTheme, PanelTheme[UiPaletteRole.Foreground], UiFont.Small);
        UsKernelDraw.Label(Execution, VerseUiExecution.Rect(new Rect(body.x, body.y + 28f, body.width, Mathf.Max(1f, body.height - 28f))), Translator.Translate(prerequisite ? "US.Settings.Prerequisite.Body" : "US.Diagnostics.PageUnavailable.Body"), PanelTheme, PanelTheme[UiPaletteRole.SecondaryForeground], UiFont.Tiny);
    }


    /// <summary>Refresh the two target states while the panel is visible, so a target opened or
    /// closed elsewhere reads honestly without a poll loop.</summary>
    protected override void BeforeDraw(Rect contentRect)
    {
        if (Host != null) UsUiDiagnostics.Synchronize(Host);
        Requery();
    }

    /// <summary>
    /// F01/US-UI1 review1 (PM observation 2 + the focus-timing observation): Verse reads the key
    /// eligibility fields in its PRE-CONTENT dispatch - before this window's BeforeDraw/DoWindowContents
    /// ever run - so a per-pass refresh placed in BeforeDraw is one pass stale in BOTH directions: a menu
    /// opened by a click could not receive the next event's Escape, and a menu just closed let the next
    /// Enter answer to an idle tool and blocked the settings window underneath. That staleness is a
    /// defect, not a limit. The recompute therefore runs at the top of WindowOnGUI, before <c>base</c>
    /// reaches Verse's dispatch, and reads exactly this window's own session: the panel is eligible ONLY
    /// while its page actually holds an open menu, a held capture or a focused edit - the library ladder
    /// then answers exactly one of ITS layers and consumes (an open menu owning the keyboard is the
    /// popup-layer rule, not a hijack). Idle, the field is cleared and Verse's stack search continues to
    /// the settings window's own ladder and, at its root, to Verse's meaning of the key; Accept is gated
    /// by the same field, so the tool never stands between the player and an edit elsewhere. No second
    /// input authority: this is Verse's own field, computed live at the last point before Verse reads it.
    /// </summary>
    public override void WindowOnGUI()
    {
        UiSession? session = Host?.Session;
        forceCatchAcceptAndCancelEventEvenIfUnfocused = session != null
            && (session.OpenPopupId != null || session.ActiveEditNode != null
                || (session.OwnedHotControl.HasValue && session.OwnedHotControlOwner != null));
        base.WindowOnGUI();
    }

    private void Requery()
    {
        source.RefreshStates();
        RefreshReportStatus();
        Host?.Invalidate();
    }

    private GameSettingsWindow? SampleTarget() => source.TargetKey == "settings"
        ? GameSettingsWindow.CostSampleOwner(source.ResolveTarget()) : null;

    private void Page(string tab)
    {
        UiHost? settings = UsDevPanelTargets.SettingsHost;
        if (settings == null)
        {
            statusKey = "US.DevPanel.Status.NoSettings";
            statusArg = "";
            return;
        }

        settings.Bindings.Invoke("set-tab", tab);
        statusKey = "US.DevPanel.Status.Page";
        statusArg = tab;
    }

    /// <summary>The status names the requested target/page, source and session; completion adds the
    /// actual captured pass. The page label is explicitly request-time, not a later page guess.</summary>
    private void Report()
    {
        reportingTarget = null;
        UiHost? target = source.ResolveTarget();
        if (target == null)
        {
            statusKey = "US.DevPanel.Status.ReportUnavailable";
            statusArg = source.TargetLabel;
            return;
        }

        int afterPass = UsTextFitAudit.FindOpenScope(target)?.LastGeometryReportPass ?? -1;
        bool requested = UsTextFitAudit.RequestGeometryReport(target);
        int pass = requested ? UsTextFitAudit.PublishGeometryReport(target) : -1;
        statusKey = requested
            ? (pass >= 0 ? "US.DevPanel.Status.ReportWritten" : "US.DevPanel.Status.ReportWaiting")
            : "US.DevPanel.Status.ReportUnavailable";
        statusArg = source.TargetLabel + " request_page=" + source.CurrentPage(target)
            + " source=" + Consumer + " session=" + target.Session.Identity + (pass >= 0 ? " pass=" + pass : "");
        if (requested && pass < 0)
        {
            reportingTarget = target;
            reportingAfterPass = afterPass;
            reportingLabel = statusArg;
        }
    }

    private void RefreshReportStatus()
    {
        var target = reportingTarget;
        if (target == null) return;
        var scope = UsTextFitAudit.FindOpenScope(target);
        if (scope != null && scope.LastGeometryReportPass > reportingAfterPass)
        {
            // Read the target owner's actual receipt. A later selector change cannot retarget it.
            statusKey = "US.DevPanel.Status.ReportWritten";
            statusArg = reportingLabel + " pass=" + scope.LastGeometryReportPass;
            reportingTarget = null;
        }
        else if (scope == null || !scope.IsGeometryReportPending)
        {
            statusKey = "US.DevPanel.Status.ReportUnavailable";
            statusArg = reportingLabel;
            reportingTarget = null;
        }
    }

    public override void PostClose()
    {
        reportingTarget = null;
        base.PostClose();
        if (ReferenceEquals(Active, this)) Active = null;
    }

    /// <summary>The whole page, through the same production parser the diagnostics spec uses. No new
    /// kinds, no new attributes: dropdown, switch-shaped checkboxes, buttons and one wrapped status.
    /// The checkboxes carry Width="Auto" so the label gets its real measured space beside the 34px
    /// switch (a 36px box would ellipsize the word the control is named by).</summary>
    private const string PageXml =
        "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">"
        + "  <Styles Schema=\"1\"><Scheme Name=\"us-dev-body\"><Metric Token=\"Font\" Value=\"Small\" /><Metric Token=\"FontSize\" Value=\"16\" /></Scheme></Styles>"
        + "  <Scroll Id=\"dev-root\" Fill=\"true\" Scheme=\"us-dev-body\" Gap=\"6\" Padding=\"10\">"
        + "    <Wrap Id=\"dev-pages\" Cols=\"3\" Gap=\"4\" Padding=\"0\">"
        + "      <Widget Id=\"dev-page-overview\" Kind=\"input/button\" TextKey=\"US.Nav.Overview.Label\" ActionBind=\"dev-page-overview\" Emphasis=\"Muted\" Height=\"30\" />"
        + "      <Widget Id=\"dev-page-packs\" Kind=\"input/button\" TextKey=\"US.Nav.Packs.Label\" ActionBind=\"dev-page-packs\" Emphasis=\"Muted\" Height=\"30\" />"
        + "      <Widget Id=\"dev-page-tuning\" Kind=\"input/button\" TextKey=\"US.Nav.Tuning.Label\" ActionBind=\"dev-page-tuning\" Emphasis=\"Muted\" Height=\"30\" />"
        + "      <Widget Id=\"dev-page-advanced\" Kind=\"input/button\" TextKey=\"US.Nav.Advanced.Label\" ActionBind=\"dev-page-advanced\" Emphasis=\"Muted\" Height=\"30\" />"
        + "      <Widget Id=\"dev-page-presets\" Kind=\"input/button\" TextKey=\"US.Nav.Presets.Label\" ActionBind=\"dev-page-presets\" Emphasis=\"Muted\" Height=\"30\" />"
        + "    </Wrap>"
        + "    <Widget Id=\"dev-target\" Kind=\"input/dropdown\" Bind=\"dev-target\" OptionsBind=\"dev-target-options\" LabelKey=\"US.DevPanel.Target\" Height=\"30\" />"
        + "    <Widget Id=\"dev-sample\" Kind=\"input/button\" TextKey=\"US.DevPanel.Sample\" ActionBind=\"dev-sample\" VisibleBind=\"dev-sample-ready\" Width=\"Auto\" AlignX=\"Left\" Height=\"30\" />"
        + "    <Widget Id=\"dev-sample-status\" Kind=\"text/wrapped\" Bind=\"dev-sample-status\" />"
        + "    <Row Id=\"dev-toggles\" Breakpoint=\"300\" Narrow=\"Column\" Gap=\"8\" Padding=\"0\">"
        + "      <Widget Id=\"dev-capture-check\" Kind=\"input/checkbox\" Appearance=\"switch\" Bind=\"dev-capture-toggle\" SelectedBind=\"dev-capture\" LabelKey=\"US.DevPanel.Capture\" Width=\"Auto\" Height=\"30\" />"
        + "      <Widget Id=\"dev-outline-check\" Kind=\"input/checkbox\" Appearance=\"switch\" Bind=\"dev-outline-toggle\" SelectedBind=\"dev-outline\" LabelKey=\"US.DevPanel.Outline\" Width=\"Auto\" Height=\"30\" />"
        + "    </Row>"
        + "    <Column Id=\"dev-report-row\" Gap=\"4\" Padding=\"0\">"
        + "      <Widget Id=\"dev-report\" Kind=\"input/button\" TextKey=\"US.DevPanel.Report\" ActionBind=\"dev-report\" Width=\"120\" Height=\"30\" />"
        + "      <Widget Id=\"dev-status\" Kind=\"text/wrapped\" Bind=\"dev-status\" Emphasis=\"Muted\" />"
        + "  </Column>"
        + "  </Scroll>"
        + "</UiPage>";

    /// <summary>Panel-local view model: the two settings-scope targets, their live availability and
    /// their two independent states, all read through the existing per-host audit seam at command
    /// time. The diagnostics panel is NOT a target here (user ruling: Debug Action only).</summary>
    private sealed class DevPanelSource
    {
        public string TargetKey = "settings";

        /// <summary>The dropdown kind's options contract is <c>UiChoice&lt;string&gt;</c> pairs (display, value).</summary>
        public IReadOnlyList<UiChoice<string>> Targets => new[]
        {
            new UiChoice<string>(Translator.Translate("US.DevPanel.Target.Settings"), "settings"),
            new UiChoice<string>(Translator.Translate("US.DevPanel.Target.Dialog"), "dialog"),
        };

        public string TargetLabel => TargetKey switch
        {
            "dialog" => Translator.Translate("US.DevPanel.Target.Dialog"),
            "settings" => Translator.Translate("US.DevPanel.Target.Settings"),
            _ => "?",
        };

        public bool CaptureOn { get; private set; }
        public bool OutlineOn { get; private set; }

        public UiHost? ResolveTarget()
        {
            UiHost? target = TargetKey switch { "settings" => UsDevPanelTargets.SettingsHost,
                "dialog" => UsDevPanelTargets.DialogHost, _ => null };
            return target?.Session.IsActive == true ? target : null;
        }

        public string CurrentPage(UiHost? target = null)
        {
            target ??= ResolveTarget();
            if (target == null) return "?";
            if (target.Bindings.TryGet("page-state", out VoicePacksPageState state)) return state.Page;
            if (target.Bindings.TryGet("confirm-step1", out bool _)) return "Remix";
            if (target.Bindings.TryGet(UiBindings.ActiveTabBind, out string tab)
                && tab != null && tab.Length > 0)
            {
                return tab;
            }

            return "Dialog";
        }

        public void RefreshStates()
        {
            UsTextFitAudit.DevGeometryStatus state = UsTextFitAudit.GetDevGeometryStatus(ResolveTarget());
            CaptureOn = state is UsTextFitAudit.DevGeometryStatus.Active
                or UsTextFitAudit.DevGeometryStatus.Overlay;
            OutlineOn = state is UsTextFitAudit.DevGeometryStatus.Overlay
                or UsTextFitAudit.DevGeometryStatus.OutlineOnly;
        }

        public void SetCapture(bool on)
        {
            var target = ResolveTarget();
            if (UsTextFitAudit.SetGeometryCapture(target, on)) target?.Invalidate();
            RefreshStates();
        }

        public void SetOverlay(bool on)
        {
            var target = ResolveTarget();
            if (UsTextFitAudit.SetGeometryOverlay(target, on)) target?.Invalidate();
            RefreshStates();
        }
    }
}

/// <summary>
/// The live developer-panel targets: settings and its current confirmation (including Remix).
/// Registration is open/close symmetric at each owner's HostAttached/HostDetached seams;
/// these are dev-tooling references, not business state, and they never
/// outlive the window that set them. The panel itself and the in-game diagnostics panel never
/// register here.
/// </summary>
internal static class UsDevPanelTargets
{
    public static UiHost? SettingsHost { get; set; }
    public static UiHost? DialogHost { get; set; }
}
