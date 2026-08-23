using UniversalSqueaker.Kernel;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Adapts RimWorld's already-resolved current life stage to the Kernel age bucket.
/// Newborn/Baby share Baby, Child maps to Child; missing or other stages degrade to Adult.
/// </summary>
internal static class SqueakLifeStageResolver
{
    public static AgeBucket Resolve(Pawn? pawn)
    {
        DevelopmentalStage stage = pawn?.ageTracker?.CurLifeStage?.developmentalStage ?? DevelopmentalStage.Adult;
        return stage == DevelopmentalStage.Newborn || stage == DevelopmentalStage.Baby
            ? AgeBucket.Baby
            : stage == DevelopmentalStage.Child
                ? AgeBucket.Child
                : AgeBucket.Adult;
    }
}
