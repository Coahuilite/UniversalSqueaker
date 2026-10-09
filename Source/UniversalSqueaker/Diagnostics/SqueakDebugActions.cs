using LudeonTK;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// S4 diagnostics foundation: vanilla Debug-menu entry for the Universal Squeaker diagnostics
/// surface (main window; detail columns, locks and search live inside the panel). DT1 (2026-10-07)
/// adds the two developer entries: the settings dev panel, and the in-game outline toggle that
/// affects ONLY the US diagnostics panel's own host.
/// </summary>
public static class SqueakDebugActions
{
    [DebugAction("Universal Squeaker", "Diagnostics: open panel", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DiagnosticsOpen()
    {
        SqueakDebug.OpenDiagnostics();
    }

    [DebugAction("Universal Squeaker", "Developer: open settings dev panel", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DevPanelOpen()
    {
        // DT1 pass condition 1: the dev feature gates the entry, and the tool never stands alone -
        // without an open settings target the open is refused. Normal play never reaches the panel.
        if (UniversalSqueakerMod.Settings?.devLoggingMode == SqueakDevLoggingMode.Disabled)
        {
            Messages.Message("Universal Squeaker: enable a dev logging mode in the settings first.",
                MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (!UniversalSqueaker.UI.Dev.UsDevPanelWindow.OpenOrFocus())
        {
            Messages.Message("Universal Squeaker: open the US settings window first - the developer panel belongs to it.",
                MessageTypeDefOf.RejectInput, historical: false);
        }
    }

    [DebugAction("Universal Squeaker", "Diagnostics: toggle panel UI outline", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DiagnosticsToggleOutline()
    {
        // DT1: the in-game entry flips ONLY the US diagnostics panel's own subscription - the setting
        // page, the dialog and every other host keep their states (per-Host isolation, SA1.5/FL-20).
        SqueakDiagnosticsPanel? panel = SqueakDiagnosticsOverlay.ActivePanelOrNull;
        FerriteLib.UiKit.Kernel.UiHost? host = panel?.LiveHostForDebug;
        if (host == null)
        {
            Messages.Message("Universal Squeaker: the diagnostics panel is not open.",
                MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        UniversalSqueaker.UI.UsTextFitAudit.DevGeometryStatus status =
            UniversalSqueaker.UI.UsTextFitAudit.GetDevGeometryStatus(host);
        bool on = status is UniversalSqueaker.UI.UsTextFitAudit.DevGeometryStatus.Overlay
            or UniversalSqueaker.UI.UsTextFitAudit.DevGeometryStatus.OutlineOnly;
        bool honoured = UniversalSqueaker.UI.UsTextFitAudit.SetGeometryOverlay(host, !on);
        Messages.Message("Universal Squeaker: diagnostics outline " + (!on ? "on" : "off")
            + (honoured ? "" : " (refused: this carrier has no instrument)"),
            MessageTypeDefOf.NeutralEvent, historical: false);
    }
}
