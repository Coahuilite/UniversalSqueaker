using LudeonTK;
using Verse;

namespace UniversalSqueaker;

public static class ResponseDebugActions
{
#if US_DEV
    private static readonly UsDevelopmentPolicy fixture = new(20, 40, 120, 120, 120, 1, 1,
        equipQualification: UsEquipQualification.PlayerCoreEquip);

    [DebugAction("Universal Squeaker", "S4AB-S5 GLOBAL Dev candidate: W20 H40 I120 spread120 p1 N/speed1; Equip=PlayerCoreEquip (pending)", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void SelectCandidate()
    {
        SqueakerRuntime.SelectDevelopmentPolicy(fixture);
    }
#endif
}
