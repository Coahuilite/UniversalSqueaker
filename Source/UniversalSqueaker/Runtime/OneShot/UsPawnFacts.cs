using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.OneShot;

namespace UniversalSqueaker;

/// <summary>One capture at the real opportunity/notification. No decisions, clocks or random draws.</summary>
internal static class UsPawnFacts
{
    internal static FactSnapshot CaptureSnapshot(AuthorCatalog catalog, Pawn pawn, string? eventKey)
        => catalog.Capture(pawn.ThingID, Capture(pawn, eventKey), UsSelectObservation.Diagnostics);

    internal static Dictionary<string, Truth> Capture(Pawn pawn, string? eventKey)
    {
        var result = new Dictionary<string, Truth>(StringComparer.Ordinal);
        foreach (string key in BuiltInPawnContracts.CandidateOrder)
        {
            result[BuiltInPawnContracts.FactKey(key)] = BuiltInPawnContracts.IsEvent(key) ? Yes("us.core/" + key == eventKey) : Truth.Unknown;
        }
        // No complete observer means missing implementation, not evidence of an accepted selection.
        if (!UsSelectObservation.Ready) result[UsSelectObservation.FactKey] = Truth.Unknown;
        var job = pawn.CurJob;
        var driver = pawn.jobs?.curDriver;
        result["us.core/SleepState"] = driver == null ? Truth.Unknown : Yes(driver.asleep);
        result["us.core/MoveState"] = pawn.pather == null ? Truth.Unknown : Yes(pawn.pather.Moving);
        result["us.core/WorkState"] = Yes(job?.workGiverDef != null);
        result["us.core/JoyState"] = Yes(job?.def?.joyKind != null);
        string name = job?.def?.defName ?? "";
        result["us.core/SocialState"] = Yes(job?.def?.joyKind == JoyKindDefOf.Social
            || name == "StandAndBeSociallyActive" || name == "GotoAndBeSociallyActive" || name == "Lovin");
        // IsIdle is a positive JobTag observation. Wait_Combat/isIdle alone is not sufficient.
        result["us.core/CallState"] = pawn.mindState == null || pawn.pather == null ? Truth.Unknown
            : Yes(pawn.mindState.IsIdle && !pawn.pather.Moving && (name == "Wait" || name == "Wait_Wander"));
        bool ingest = job?.def == JobDefOf.Ingest;
        bool nutrition = driver is IEatingDriver eating && eating.GainingNutritionNow;
        Truth chewing = Truth.False;
        Truth drug = Truth.False;
        if (ingest)
        {
            chewing = Truth.Unknown;
            if (driver != null && string.Equals(driver.CurToilString, SqueakEatOccurrence.ChewingToilDebugName, StringComparison.Ordinal))
            {
                chewing = Truth.True;
            }
            drug = job?.targetA.Thing == null ? Truth.Unknown : Yes(job.targetA.Thing.def.IsDrug);
        }
        result["us.core/EatWholeJob"] = Yes(ingest);
        result["us.core/EatNutrition"] = Yes(ingest && nutrition);
        result["us.core/EatChewing"] = chewing;
        result["us.core/EatDrug"] = drug;
        // Retained player mode (including its explicit legacy fallback) is not the precise chewing fact.
        result["us.core/EatState"] = Yes(PeriodicStateBinding.CaptureEatingOccurrence(pawn));
        result["us.core/ActiveCommand"] = Yes(job?.playerForced == true && pawn.IsPlayerControlled);
        result["us.core/PlayerControlled"] = Yes(pawn.IsPlayerControlled);
        result["us.core/Responsive"] = pawn.health == null ? Truth.Unknown : Yes(!pawn.Downed && pawn.Awake());
        result["us.core/HasCurrentInjury"] = pawn.health?.hediffSet == null ? Truth.Unknown
            : Yes(pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(x => x.Severity > 0));
        result["us.core/BiotechAvailable"] = Yes(ModsConfig.BiotechActive);
        return result;
    }
    private static Truth Yes(bool value) => value ? Truth.True : Truth.False;
}
