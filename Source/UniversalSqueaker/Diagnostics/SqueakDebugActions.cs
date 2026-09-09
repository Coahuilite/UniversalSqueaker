using LudeonTK;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// S4 diagnostics foundation: vanilla Debug-menu entry for the Universal Squeaker diagnostics
/// surface (main window; detail columns, locks and search live inside the panel).
/// </summary>
public static class SqueakDebugActions
{
    [DebugAction("Universal Squeaker", "Diagnostics: open panel", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DiagnosticsOpen()
    {
        SqueakDebug.OpenDiagnostics();
    }
}
