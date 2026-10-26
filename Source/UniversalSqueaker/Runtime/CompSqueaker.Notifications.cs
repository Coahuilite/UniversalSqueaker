using System.Globalization;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>Production notification callers shared by the native hooks and the controlled fixture.</summary>
public partial class CompSqueaker
{
    internal void Notify_Wounded(float? totalDamageDealt = null) => NotifyExternal(CoreActions.Wounded,
        SqueakTriggerOrigin.Wounded, SqueakInvocationSource.StateEvent,
        totalDamageDealt.HasValue ? "Pawn.PostApplyDamage:Returned,totalDamageDealt=" + totalDamageDealt.Value.ToString(CultureInfo.InvariantCulture) : "CallerSupplied:Wounded");
    internal void Notify_Select(Responses.InteractionOpportunity opportunity)
        => NotifyObservedByKey(CoreActions.Select, SqueakTriggerOrigin.Select, SqueakInvocationSource.PlayerSelection,
            "Selector:AcceptedFeedback", opportunity);
    internal void Notify_Death() => NotifyExternal(CoreActions.Death, SqueakTriggerOrigin.Death,
        SqueakInvocationSource.StateEvent, "Pawn.Kill:Entering");
    internal void Notify_Draft(bool drafted, Responses.InteractionOpportunity opportunity)
        => NotifyObservedByKey(drafted ? CoreActions.Draft : CoreActions.Undraft,
            drafted ? SqueakTriggerOrigin.Draft : SqueakTriggerOrigin.Undraft, SqueakInvocationSource.ActiveCommand,
            "DraftGizmo:Changed:" + !drafted + "->" + drafted, opportunity);
    internal void Notify_Attack(string mechanism = "CallerSupplied") => NotifyExternal(CoreActions.Attack,
        SqueakTriggerOrigin.Attack, Pawn.CurJob?.playerForced == true ? SqueakInvocationSource.ActiveCommand : SqueakInvocationSource.StateEvent,
        "AttackExecution:" + mechanism);
    internal void Notify_Equip(ThingWithComps equipment) => NotifyExternal(CoreActions.Equip,
        SqueakTriggerOrigin.Equip, Pawn.CurJob?.playerForced == true && Pawn.CurJob.def == JobDefOf.Equip
            ? SqueakInvocationSource.ActiveCommand : SqueakInvocationSource.StateEvent,
        "Pawn_EquipmentTracker.Notify_EquipmentAdded:Returned,wearer=" + Pawn.ThingID + ",item=" + equipment.ThingID);
    internal void Notify_MentalBreak(string mechanism = "CallerSupplied") => NotifyExternal(CoreActions.MentalBreak, SqueakTriggerOrigin.MentalBreak,
        SqueakInvocationSource.StateEvent, "MentalBreak:" + mechanism + ":True");

    private void NotifyExternal(string action, SqueakTriggerOrigin origin, SqueakInvocationSource source, string evidence)
        => NotifyObservedByKey(action, origin, source, evidence);
}
