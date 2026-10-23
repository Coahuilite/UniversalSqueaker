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
    {
        var facts = Capture(pawn, eventKey, out var diagnostics);
        return catalog.Capture(pawn.ThingID, facts, diagnostics);
    }

    internal static Dictionary<string, Truth> Capture(Pawn pawn, string? eventKey)
        => Capture(pawn, eventKey, out _);

    internal static Dictionary<string, Truth> Capture(Pawn pawn, string? eventKey, out IReadOnlyList<FactDiagnostic> diagnostics)
    {
        var result = new Dictionary<string, Truth>(StringComparer.Ordinal);
        foreach (string key in BuiltInPawnContracts.CandidateOrder)
        {
            result[BuiltInPawnContracts.FactKey(key)] = BuiltInPawnContracts.IsEvent(key) ? Yes("us.core/" + key == eventKey) : Truth.Unknown;
            if (!UsPawnEventObservation.Available(BuiltInPawnContracts.FactKey(key))) result[BuiltInPawnContracts.FactKey(key)] = Truth.Unknown;
        }
        // No complete observer means missing implementation, not evidence of an accepted selection.
        if (!UsSelectObservation.Ready) result[UsSelectObservation.FactKey] = Truth.Unknown;
        if (!UsAttackObservation.Ready) result["us.core/AttackEvent"] = Truth.Unknown;
        var job = pawn.CurJob;
        var driver = pawn.jobs?.curDriver;
        result["us.core/SleepState"] = driver == null ? Truth.Unknown : Yes(driver.asleep);
        result["us.core/MoveState"] = pawn.pather == null ? Truth.Unknown : Yes(pawn.pather.Moving);
        result["us.core/WorkState"] = UsWorkObservation.Capture(pawn, out var workDiagnostic);
        var capturedDiagnostics = UsSelectObservation.Diagnostics.Concat(UsIngestionObservation.Diagnostics)
            .Concat(UsAttackObservation.Diagnostics).Concat(UsPawnEventObservation.Diagnostics).ToList();
        if (workDiagnostic.HasValue) capturedDiagnostics.Add(workDiagnostic.Value);
        diagnostics = capturedDiagnostics.AsReadOnly();
        result["us.core/JoyState"] = Yes(job?.def?.joyKind != null);
        string name = job?.def?.defName ?? "";
        result["us.core/SocialState"] = Yes(job?.def?.joyKind == JoyKindDefOf.Social
            || name == "StandAndBeSociallyActive" || name == "GotoAndBeSociallyActive" || name == "Lovin");
        // IsIdle is a positive JobTag observation. Wait_Combat/isIdle alone is not sufficient.
        result["us.core/CallState"] = pawn.mindState == null || pawn.pather == null ? Truth.Unknown
            : Yes(pawn.mindState.IsIdle && !pawn.pather.Moving && (name == "Wait" || name == "Wait_Wander"));
        UsIngestionObservation.Capture(pawn, result);
        result[EatPolicy.Result] = UsIngestionObservation.Ingested == null ? Truth.Unknown : Yes(eventKey == EatPolicy.Result);
        result["us.core/ActiveCommand"] = Yes(job?.playerForced == true && pawn.IsPlayerControlled);
        result["us.core/PlayerControlled"] = Yes(pawn.IsPlayerControlled);
        result["us.core/Awake"] = pawn.health == null ? Truth.Unknown : Yes(pawn.Awake());
        result["us.core/NotDowned"] = pawn.health == null ? Truth.Unknown : Yes(!pawn.Downed);
        result["us.core/PlayerEquipCommand"] = Yes(pawn.IsPlayerControlled && job?.playerForced == true && job.def == JobDefOf.Equip);
        result["us.core/Responsive"] = pawn.health == null ? Truth.Unknown : Yes(!pawn.Downed && pawn.Awake());
        result["us.core/HasCurrentInjury"] = pawn.health?.hediffSet == null ? Truth.Unknown
            : Yes(pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(x => x.Severity > 0));
        result["us.core/BiotechAvailable"] = Yes(ModsConfig.BiotechActive);
        return result;
    }
    private static Truth Yes(bool value) => value ? Truth.True : Truth.False;
}
