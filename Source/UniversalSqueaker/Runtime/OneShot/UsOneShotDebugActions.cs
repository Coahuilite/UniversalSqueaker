using LudeonTK;
using Verse;

namespace UniversalSqueaker;

public static class UsOneShotDebugActions
{
#if US_DEV
    [DebugAction("Universal Squeaker", "S1 controlled fixture: enable W20 H40 actions120", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void Enable(Pawn pawn) => UsOneShotRuntime.EnableControlled(pawn, waitTicks: 20, sharedTicks: 40, workTicks: 120, woundedTicks: 120);
    [DebugAction("Universal Squeaker", "S3 CANDIDATE order: 17 actions W20 H40 actions120", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void EnableS3Candidate(Pawn pawn) => UsOneShotRuntime.EnableControlled(pawn, waitTicks: 20, sharedTicks: 40, workTicks: 120, woundedTicks: 120, fullDevCandidate: true);
    [DebugAction("Universal Squeaker", "S1 controlled fixture: disable", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void Disable(Pawn pawn) => UsOneShotRuntime.DisableControlled(pawn);
#endif
}
