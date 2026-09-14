using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace UniversalSqueaker;

/// <summary>
/// 周期状态探针：从 pawn 当前状态推导动作，替代 CompSqueaker.CurrentAction 硬编码 switch。
/// 行为与旧 CurrentAction 谓词链逐条等价：Sleeping→Sleep, Eating→Eat, Socializing→Social,
/// JoyJob→Joy, Moving→Move, Working→Work, 否则→Call。
/// </summary>
public static class PeriodicStateBinding
{
    private static readonly string[] SocialJobMarkers = { "Chat", "Social", "Visit", "Lovin", "Entertain" };

    // Process-level, runtime-only fact (never Scribed): the vanilla chewing/lighting toil debugName has been
    // sampled at least once. Until then the pure rule falls back to full job level for ChewingToil
    // (fail-open: a renamed toil degrades to the factory feel instead of going silent).
    private static bool chewToilNameConfirmed;

    public static SqueakAction? Probe(Pawn pawn)
    {
        if (pawn == null) return null;
        if (IsSleeping(pawn)) return SqueakAction.Sleep;
        if (IsEating(pawn)) return SqueakAction.Eat;
        if (IsSocializing(pawn)) return SqueakAction.Social;
        if (IsJoyJob(pawn)) return SqueakAction.Joy;
        if (IsMoving(pawn)) return SqueakAction.Move;
        if (IsWorking(pawn)) return SqueakAction.Work;
        return SqueakAction.Call;
    }

    /// <summary>Eat occurrence granularity (contract 1.4): the two switches resolve to one of three modes.
    /// Only public API is sampled and there is no JobDriver_Ingest cast; the WholeJob arm returns before any
    /// sampling, so "parent off" never touches jobs / curDriver / the nutrition probe.</summary>
    private static bool IsEating(Pawn pawn)
    {
        if (pawn.CurJob?.def != JobDefOf.Ingest) return false;
        return SqueakEatOccurrence.ResolveMode(CompSqueaker.EatPrecisionEnabled, CompSqueaker.EatPrecisionIncludeDrugs) switch
        {
            SqueakEatOccurrenceMode.WholeJob => true,
            SqueakEatOccurrenceMode.GainingNutrition => IsGainingNutritionNow(pawn),
            _ => SqueakEatOccurrence.AllowsOccurrence(SqueakEatOccurrenceMode.ChewingToil,
                IsGainingNutritionNow(pawn), SampleChewingToil(pawn), chewToilNameConfirmed),
        };
    }

    /// <summary>vanilla nutrition authority: the public IEatingDriver interface exposes GainingNutritionNow.</summary>
    private static bool IsGainingNutritionNow(Pawn pawn)
        => pawn.jobs?.curDriver is IEatingDriver eating && eating.GainingNutritionNow;

    /// <summary>Match the current toil against the vanilla chewing/lighting debugName. JobDriver's public
    /// CurToilString needs no subclass cast; a hit confirms the name for the rest of this process.</summary>
    private static bool SampleChewingToil(Pawn pawn)
    {
        JobDriver? driver = pawn.jobs?.curDriver;
        if (driver == null) return false;
        if (!string.Equals(driver.CurToilString, SqueakEatOccurrence.ChewingToilDebugName, StringComparison.Ordinal))
            return false;
        chewToilNameConfirmed = true;
        return true;
    }
    private static bool IsSleeping(Pawn pawn) => pawn.GetPosture() == PawnPosture.LayingInBed && pawn.needs?.rest != null;
    private static bool IsMoving(Pawn pawn) => pawn.pather != null && pawn.pather.Moving;
    private static bool IsJoyJob(Pawn pawn) => pawn.CurJob?.def?.joyKind != null;
    private static bool IsWorking(Pawn pawn) => pawn.CurJob?.workGiverDef != null;

    private static bool IsSocializing(Pawn pawn)
    {
        string? d = pawn.CurJob?.def?.defName;
        if (d == null) return false;
        foreach (string marker in SocialJobMarkers)
            if (d.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
}
