using System;
using System.Collections.Generic;

using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI.Dev;

/// <summary>
/// DT1 (2026-10-07 user ruling): the small draggable developer panel for the US SETTINGS workspace.
/// Its scope is exactly the settings page and its Remix confirmation dialog - the in-game
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
/// target; closing the settings window closes the panel. The panel's OWN host never registers as a
/// target - the tool's layout must not enter the dump it produces (DT1 pass condition 2).
/// </para>
/// </summary>
internal sealed class UsDevPanelWindow : UiWindowHost
{
    private const string Consumer = "coahuilite.universalsqueaker";

    private static readonly UiTheme PanelTheme = UsTheme.Surface();

    /// <summary>The one open panel (band button / Debug Action open it; settings PostClose closes it).</summary>
    internal static UsDevPanelWindow? Active { get; private set; }

    private readonly UiBindings bindings = new();
    private readonly DevPanelSource source = new();
    private string statusKey = "US.DevPanel.Status.Idle";
    private string statusArg = "";

    private UsDevPanelWindow()
    {
        // F01 (US-UI1): the settings main window is Dialog with absorbInputAroundWindow=true, and a
        // click on it natively raises it in-layer - a SAME-layer panel then loses GetsInput and the
        // recorded "panel stops responding" witness. The native-stack answer is layering, not a second
        // input authority: the tool sits on SubSuper (Verse.WindowLayer order measured from the
        // 1.6.4871 reference: GameUI < Dialog < SubSuper < Super), so the main window can never stand
        // in front of it and the two coexist for the whole settings session. Confirmation dialogs sit
        // one step higher again (Super) - the confirmation window keeps priority over tool AND main
        // window. Which window the REAL stack ends up handing each key to is the named short-pass item.
        layer = WindowLayer.SubSuper;
        forcePause = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
        closeOnCancel = true;
        closeOnAccept = false;
        closeOnClickedOutside = false;
        focusWhenOpened = false;
        onlyDrawInDevMode = true;
        // draggable / one-instance / normal-size arrive through UiWindowOptions at the catalog door.

        bindings.BindOptions<UiOption>("dev-target-options", () => source.Targets);
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
        bindings.BindCommand("dev-page-distance", () => Page("Distance"));
        bindings.BindCommand("dev-page-presets", () => Page("Presets"));
        bindings.BindCommand("dev-report", Report);
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
                CloseOnCancel = true,
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
        UiNative.Trace = SqueakLog.PopupTrace;
        UiLayoutManifest manifest = UiLayoutManifest.Parse(PageXml);
        return new UiHost(
            Consumer,
            manifest,
            bindings,
            PanelTheme,
            VerseFerriteTextMetrics.Instance,
            new UsKernelTranslation());
    }

    protected override UiTheme Theme => PanelTheme;

    protected override string Title => Translator.Translate("US.DevPanel.Title");

    protected override string CloseText => Translator.Translate("US.Common.Cancel");

    protected override bool PrerequisiteVerified => UniversalSqueakerMod.PrerequisiteVerified;

    protected override Func<Vector2>? InitialSizePolicy => () => new Vector2(
        360f,
        Math.Max(220f, Math.Min(240f, Verse.UI.screenHeight - 40f)));
    /// <summary>Terminal notices in US's own vocabulary (the library ships zero strings) - the same
    /// shape the settings window and the diagnostics panel draw.</summary>
    protected override void DrawNotice(Rect rect, UiWindowNotice notice)
    {
        UiThemeDraw.Surface(rect, PanelTheme, PanelTheme.Panel, PanelTheme.Border);
        Rect body = rect.ContractedBy(16f);
        bool prerequisite = notice == UiWindowNotice.Prerequisite;
        UsKernelDraw.Label(
            new Rect(body.x, body.y, body.width, 24f),
            Translator.Translate(prerequisite ? "US.Settings.Prerequisite.Title" : "US.Diagnostics.PageUnavailable.Title"),
            PanelTheme, PanelTheme.TextPrimary, UiFont.Small);
        UsKernelDraw.Label(
            new Rect(body.x, body.y + 28f, body.width, Mathf.Max(1f, body.height - 28f)),
            Translator.Translate(prerequisite ? "US.Settings.Prerequisite.Body" : "US.Diagnostics.PageUnavailable.Body"),
            PanelTheme, PanelTheme.TextSecondary, UiFont.Tiny);
    }


    /// <summary>Refresh the two target states while the panel is visible, so a target opened or
    /// closed elsewhere reads honestly without a poll loop.</summary>
    protected override void BeforeDraw(Rect contentRect)
    {
        Requery();
    }

    private void Requery()
    {
        source.RefreshStates();
        Host?.Session.BumpContentRevision();
    }

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

    /// <summary>The success line names the whole identity chain the report was written for:
    /// target, current settings page, host, session and pass (DT1 pass condition 2).</summary>
    private void Report()
    {
        UiHost? target = source.ResolveTarget();
        if (target == null)
        {
            statusKey = "US.DevPanel.Status.ReportUnavailable";
            statusArg = source.TargetLabel;
            return;
        }

        bool requested = UsTextFitAudit.RequestGeometryReport(target);
        int pass = requested ? UsTextFitAudit.PublishGeometryReport(target) : -1;
        statusKey = requested
            ? (pass >= 0 ? "US.DevPanel.Status.ReportWritten" : "US.DevPanel.Status.ReportWaiting")
            : "US.DevPanel.Status.ReportUnavailable";
        statusArg = pass >= 0
            ? source.TargetLabel + " page=" + source.CurrentPage()
              + " host=" + Consumer + " session=" + target.Session.Identity + " pass=" + pass
            : source.TargetLabel;
    }

    public override void PostClose()
    {
        base.PostClose();
        if (ReferenceEquals(Active, this)) Active = null;
    }

    /// <summary>The whole page, through the same production parser the diagnostics spec uses. No new
    /// kinds, no new attributes: dropdown, switch-shaped checkboxes, buttons and one wrapped status.
    /// The checkboxes carry Width="Auto" so the label gets its real measured space beside the 34px
    /// switch (a 36px box would ellipsize the word the control is named by).</summary>
    private const string PageXml =
        "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">"
        + "  <Column Id=\"dev-root\" Gap=\"6\" Padding=\"10\">"
        + "    <Row Id=\"dev-pages\" Gap=\"4\" Padding=\"0\">"
        + "      <Widget Id=\"dev-page-overview\" Kind=\"input/button\" TextKey=\"US.Nav.Overview.Label\" ActionBind=\"dev-page-overview\" Emphasis=\"Muted\" Height=\"26\" />"
        + "      <Widget Id=\"dev-page-packs\" Kind=\"input/button\" TextKey=\"US.Nav.Packs.Label\" ActionBind=\"dev-page-packs\" Emphasis=\"Muted\" Height=\"26\" />"
        + "      <Widget Id=\"dev-page-tuning\" Kind=\"input/button\" TextKey=\"US.Nav.Tuning.Label\" ActionBind=\"dev-page-tuning\" Emphasis=\"Muted\" Height=\"26\" />"
        + "      <Widget Id=\"dev-page-distance\" Kind=\"input/button\" TextKey=\"US.Nav.Distance.Label\" ActionBind=\"dev-page-distance\" Emphasis=\"Muted\" Height=\"26\" />"
        + "      <Widget Id=\"dev-page-presets\" Kind=\"input/button\" TextKey=\"US.Nav.Presets.Label\" ActionBind=\"dev-page-presets\" Emphasis=\"Muted\" Height=\"26\" />"
        + "    </Row>"
        + "    <Widget Id=\"dev-target\" Kind=\"input/dropdown\" Bind=\"dev-target\" OptionsBind=\"dev-target-options\" LabelKey=\"US.DevPanel.Target\" Height=\"26\" />"
        + "    <Row Id=\"dev-toggles\" Gap=\"8\" Padding=\"0\">"
        + "      <Widget Id=\"dev-capture-check\" Kind=\"input/checkbox\" Appearance=\"switch\" Bind=\"dev-capture-toggle\" SelectedKey=\"dev-capture\" LabelKey=\"US.DevPanel.Capture\" Width=\"Auto\" Height=\"30\" />"
        + "      <Widget Id=\"dev-outline-check\" Kind=\"input/checkbox\" Appearance=\"switch\" Bind=\"dev-outline-toggle\" SelectedKey=\"dev-outline\" LabelKey=\"US.DevPanel.Outline\" Width=\"Auto\" Height=\"30\" />"
        + "    </Row>"
        + "    <Row Id=\"dev-report-row\" Gap=\"8\" Padding=\"0\">"
        + "      <Widget Id=\"dev-report\" Kind=\"input/button\" TextKey=\"US.DevPanel.Report\" ActionBind=\"dev-report\" Width=\"120\" Height=\"26\" />"
        + "      <Widget Id=\"dev-status\" Kind=\"text/wrapped\" Bind=\"dev-status\" Emphasis=\"Muted\" Width=\"220\" />"
        + "    </Row>"
        + "  </Column>"
        + "</UiPage>";

    /// <summary>Panel-local view model: the two settings-scope targets, their live availability and
    /// their two independent states, all read through the existing per-host audit seam at command
    /// time. The diagnostics panel is NOT a target here (user ruling: Debug Action only).</summary>
    private sealed class DevPanelSource
    {
        public string TargetKey = "settings";

        /// <summary>The dropdown kind's options contract is UiOption pairs (display, value).</summary>
        public IReadOnlyList<UiOption> Targets => new[]
        {
            new UiOption(Translator.Translate("US.DevPanel.Target.Settings"), "settings"),
            new UiOption(Translator.Translate("US.DevPanel.Target.Dialog"), "dialog"),
        };

        public string TargetLabel => TargetKey == "dialog"
            ? Translator.Translate("US.DevPanel.Target.Dialog")
            : Translator.Translate("US.DevPanel.Target.Settings");

        public bool CaptureOn { get; private set; }
        public bool OutlineOn { get; private set; }

        public UiHost? ResolveTarget() => TargetKey == "dialog"
            ? UsDevPanelTargets.DialogHost
            : UsDevPanelTargets.SettingsHost;

        public string CurrentPage()
        {
            UiHost? settings = UsDevPanelTargets.SettingsHost;
            if (settings != null
                && settings.Bindings.TryGet(UiBindings.ActiveTabKey, out string tab)
                && tab != null && tab.Length > 0)
            {
                return tab;
            }

            return TargetKey == "dialog" ? "Remix" : "?";
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
            UsTextFitAudit.SetGeometryCapture(ResolveTarget(), on);
            RefreshStates();
        }

        public void SetOverlay(bool on)
        {
            UsTextFitAudit.SetGeometryOverlay(ResolveTarget(), on);
            RefreshStates();
        }
    }
}

/// <summary>
/// The live developer-panel targets: the settings page and its Remix dialog only. Registration is
/// open/close symmetric at each owner's seams (settings window CreateHost/PreClose, the Remix
/// flow's attach/detach); these are dev-tooling references, not business state, and they never
/// outlive the window that set them. The panel itself and the in-game diagnostics panel never
/// register here.
/// </summary>
internal static class UsDevPanelTargets
{
    public static UiHost? SettingsHost { get; set; }
    public static UiHost? DialogHost { get; set; }
}
