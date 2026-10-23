// Native shapes only. The production patches, notifications, facts, owner and output are linked.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Verse
{
    public struct DamageInfo { }
    public class ThingWithComps : Thing { }
    public class Gizmo { }
    public sealed class Command_Toggle : Gizmo { public object? hotKey; public string tutorTag = ""; public Action? toggleAction; }
    public sealed class Pawn_DrawTracker
    {
        public int Executions;
#if !US_FIXTURE_DC_MISSING_MELEE_MARKER
        [MethodImpl(MethodImplOptions.NoInlining)] public void Notify_MeleeAttackOn(Thing target) => Executions++;
#endif
    }
    public sealed partial class Pawn
    {
        public readonly Pawn_DrawTracker Drawer = new();
        public bool Drafted, DamageReturned, KillEntered, ThrowDamage;
        public readonly List<Gizmo> FixtureGizmos = new();
#if !US_FIXTURE_DC_MISSING_WOUNDED
        [MethodImpl(MethodImplOptions.NoInlining)] public void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        { if (ThrowDamage) throw new InvalidOperationException("NativeDamageFailure"); DamageReturned = true; }
#endif
        [MethodImpl(MethodImplOptions.NoInlining)] public void Kill(DamageInfo? dinfo, Hediff? exactCulprit = null) { KillEntered = true; Spawned = false; }
        [MethodImpl(MethodImplOptions.NoInlining)] public IEnumerable<Gizmo> GetGizmos() => FixtureGizmos;
    }
    public sealed class Pawn_EquipmentTracker
    {
        public Pawn pawn = null!;
        public readonly List<ThingWithComps> AllEquipmentListForReading = new();
        public bool ThrowNotification;
        [MethodImpl(MethodImplOptions.NoInlining)] public void Notify_EquipmentAdded(ThingWithComps eq)
        { if (ThrowNotification) throw new InvalidOperationException("NativeEquipmentFailure"); }
        public void AddEquipment(ThingWithComps eq) { AllEquipmentListForReading.Add(eq); Notify_EquipmentAdded(eq); }
    }
    public class Verb
    {
        public object caster = null!;
        public bool Result = true, Busy, Throw;
        public int Executions;
        public Action? OnExecution;
        protected virtual bool TryCastShot()
        {
            if (Throw) throw new InvalidOperationException("NativeVerbFailure");
            if (Result) { Executions++; OnExecution?.Invoke(); }
            return Result;
        }
        public bool Execute() => TryCastShot();
    }
    public class Verb_LaunchProjectile : Verb { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
    public class Verb_Shoot : Verb_LaunchProjectile { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
    public class Verb_AbilityShoot : Verb_Shoot { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
    public class Verb_LaunchProjectileStatic : Verb_LaunchProjectile { }
    public class Verb_LaunchProjectileStaticOneUse : Verb_LaunchProjectileStatic { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
    public class Verb_ShootBeam : Verb { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
    public class Verb_Spray : Verb { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
    public class Verb_ArcSprayIncinerator : Verb_ShootBeam { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
    public class Verb_SpewFire : Verb { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
}
namespace RimWorld
{
    public static class KeyBindingDefOf { public static readonly object Command_ColonistDraft = new(); }
    public sealed class Verb_MeleeAttack : Verse.Verb
    {
        [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot()
        {
            if (caster is not Verse.Pawn pawn || !pawn.Spawned || Busy) return false;
            if (Throw) throw new InvalidOperationException("NativeMeleeFailure");
            Executions++;
#if !US_FIXTURE_DC_MISSING_MELEE_MARKER
            pawn.Drawer.Notify_MeleeAttackOn(new Verse.Thing());
#endif
            return Result; // false includes a genuine executed miss/dodge, like the audited body.
        }
    }
    public class Verb_ShootOneUse : Verse.Verb_Shoot { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
    public class Verb_Ignite : Verse.Verb { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
    public class Verb_PowerBeam : Verse.Verb { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
    public class Verb_Bombardment : Verse.Verb { [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot() => base.TryCastShot(); }
    public class Verb_BeatFire : Verse.Verb
    {
        [MethodImpl(MethodImplOptions.NoInlining)] protected override bool TryCastShot()
        {
#if !US_FIXTURE_DC_MISSING_MELEE_MARKER
            if (caster is Verse.Pawn pawn) pawn.Drawer.Notify_MeleeAttackOn(new Verse.Thing());
#endif
            return base.TryCastShot();
        }
    }
    public class Verb_Jump : Verse.Verb { }
    public class Verb_CastAbility : Verse.Verb { }
    public class Verb_CastAbilityJump : Verb_CastAbility { }
    public class Verb_DeployBroadshield : Verse.Verb { }
    public class Verb_DeployToxPack : Verse.Verb { }
    public class Verb_DeployDeadlifePack : Verse.Verb { }
    public class Verb_FirefoamPop : Verse.Verb { }
    public class Verb_SmokePop : Verse.Verb { }
    public class Verb_Spawn : Verse.Verb { }
    public class Verb_MechCluster : Verse.Verb { }
    public class JobDriver_Research : Verse.AI.JobDriver { }
    public class JobDriver_Mine : Verse.AI.JobDriver { }
    public class JobDriver_Repair : Verse.AI.JobDriver { }
    public class JobDriver_CleanFilth : Verse.AI.JobDriver { }
    public class JobDriver_ConstructFinishFrame : Verse.AI.JobDriver { }
}
namespace Verse.AI
{
    public class Verb_CastTargetEffect : Verse.Verb { }
    public class JobDriver_DoBill : JobDriver { }
    public class JobDriver_HaulToCell : JobDriver { }
    public class MentalBreakWorker
    {
        public bool Result = true, Throw;
        [MethodImpl(MethodImplOptions.NoInlining)] public virtual bool TryStart(Pawn pawn, string reason, bool causedByMood)
        { if (Throw) throw new InvalidOperationException("NativeMentalBreakFailure"); return Result; }
    }
    public class MentalBreakWorker_Catatonic : MentalBreakWorker
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public override bool TryStart(Pawn pawn, string reason, bool causedByMood)
        { if (Throw) throw new InvalidOperationException("NativeCatatonicFailure"); return true; }
    }
    public class MentalBreakWorker_RunWild : MentalBreakWorker
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public override bool TryStart(Pawn pawn, string reason, bool causedByMood)
        { if (Throw) throw new InvalidOperationException("NativeRunWildFailure"); return true; }
    }
    public class MentalBreakWorker_WildDecree : MentalBreakWorker
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public override bool TryStart(Pawn pawn, string reason, bool causedByMood)
        { if (Throw) throw new InvalidOperationException("NativeDecreeFailure"); return true; }
    }
    public class MentalBreakWorker_HumanityBreak : MentalBreakWorker
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public override bool TryStart(Pawn pawn, string reason, bool causedByMood)
            => base.TryStart(pawn, reason, causedByMood);
    }
}
