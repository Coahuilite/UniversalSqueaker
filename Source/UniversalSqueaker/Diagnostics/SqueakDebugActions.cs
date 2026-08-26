using LudeonTK;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// S4 diagnostics foundation: vanilla Debug-menu entry for the Universal Squeaker diagnostics
/// surface. The panel body (SqueakDiagnosticsOverlay / SqueakDiagnosticsPanel) lands in the
/// next block; the action currently delegates to the placeholder
/// <see cref="SqueakDebug.OpenSelectedDiagnostics"/>.
/// </summary>
public static class SqueakDebugActions
{
    [DebugAction("Universal Squeaker", "Diagnostics: selected pawn", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DiagnosticsSelected()
    {
        SqueakDebug.OpenSelectedDiagnostics();
    }
}
