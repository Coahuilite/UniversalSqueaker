using System;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// 周期状态探针：从 pawn 当前状态推导动作，替代 CompSqueaker.CurrentAction 硬编码 switch。
/// 行为与旧 CurrentAction 谓词链逐条等价：Sleeping→Sleep, Eating→Eat, Socializing→Social,
/// JoyJob→Joy, Moving→Move, Working→Work, 否则→Call。
/// </summary>
public static class PeriodicStateBinding
{
    private static readonly string[] SocialJobMarkers = { "Chat", "Social", "Visit", "Lovin", "Entertain" };

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

    private static bool IsEating(Pawn pawn) => pawn.CurJob?.def == JobDefOf.Ingest;
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
