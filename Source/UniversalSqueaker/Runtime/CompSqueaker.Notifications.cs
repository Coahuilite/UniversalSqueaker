using System.Globalization;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>Production notification callers shared by the native hooks and the controlled fixture.</summary>
public partial class CompSqueaker
{
    public void Notify_Wounded(float? totalDamageDealt = null) => NotifyExternal(SqueakAction.Wounded,
        SqueakTriggerOrigin.Wounded, SqueakInvocationSource.StateEvent,
        totalDamageDealt.HasValue ? "Pawn.PostApplyDamage:Returned,totalDamageDealt=" + totalDamageDealt.Value.ToString(CultureInfo.InvariantCulture) : "CallerSupplied:Wounded");
    public void Notify_Select() => NotifyExternal(SqueakAction.Select, SqueakTriggerOrigin.Select,
        SqueakInvocationSource.PlayerSelection, "Selector:AcceptedFeedback");
    public void Notify_Death() => NotifyExternal(SqueakAction.Death, SqueakTriggerOrigin.Death,
        SqueakInvocationSource.StateEvent, "Pawn.Kill:Entering");
    public void Notify_Draft(bool drafted) => NotifyExternal(drafted ? SqueakAction.Draft : SqueakAction.Undraft,
        drafted ? SqueakTriggerOrigin.Draft : SqueakTriggerOrigin.Undraft, SqueakInvocationSource.ActiveCommand,
        "DraftGizmo:Changed:" + !drafted + "->" + drafted);
    public void Notify_Attack(string mechanism = "CallerSupplied") => NotifyExternal(SqueakAction.Attack,
        SqueakTriggerOrigin.Attack, Pawn.CurJob?.playerForced == true ? SqueakInvocationSource.ActiveCommand : SqueakInvocationSource.StateEvent,
        "AttackExecution:" + mechanism);
    public void Notify_Equip(ThingWithComps equipment) => NotifyExternal(SqueakAction.Equip,
        SqueakTriggerOrigin.Equip, Pawn.CurJob?.playerForced == true && Pawn.CurJob.def == JobDefOf.Equip
            ? SqueakInvocationSource.ActiveCommand : SqueakInvocationSource.StateEvent,
        "Pawn_EquipmentTracker.Notify_EquipmentAdded:Returned,wearer=" + Pawn.ThingID + ",item=" + equipment.ThingID);
    public void Notify_MentalBreak(string mechanism = "CallerSupplied") => NotifyExternal(SqueakAction.MentalBreak, SqueakTriggerOrigin.MentalBreak,
        SqueakInvocationSource.StateEvent, "MentalBreak:" + mechanism + ":True");

    private void NotifyExternal(SqueakAction action, SqueakTriggerOrigin origin, SqueakInvocationSource source, string evidence)
        => NotifyObservedByKey(Kernel.ActionKey.For(action) ?? action.ToString(), origin, source, evidence);
}
