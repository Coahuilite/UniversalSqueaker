namespace UniversalSqueaker;

// Pure Eat occurrence-granularity decision (ported from the Squeaky Ratkin reference; US deliberately
// differs only in namespace and in the adapter, which never casts to JobDriver_Ingest). The default keeps
// job-level dispatch: the whole JobDefOf.Ingest job (walk to the food, pick it up, carry it, sit down,
// chew, finish) counts as Eat. That is the signature feel and must stay byte-identical to the
// pre-granularity behaviour. Two player switches -> three effective modes:
//   1) parent off             = WholeJob: every Ingest job counts as Eat. Parent off beats the child:
//                               ResolveMode returns WholeJob regardless of the child value.
//   2) parent on, child off   = GainingNutrition: only while vanilla reports nutrition being gained
//                               (zero-nutrition drugs do not count).
//   3) parent on, child on    = ChewingToil: match the vanilla chewing/lighting toil by its debugName
//                               (so zero-nutrition drugs count too). While that toil name has not been
//                               confirmed in this process, fall back to full job level (fail-open: never
//                               go silent).
// Semantic boundary: vanilla's nutrition authority requires CachedNutrition > 0, so nutrition-bearing
// drugs (beer 0.08, ambrosia 0.2) already count in mode 2; mode 3 only adds what the toil name covers,
// predominantly zero-nutrition drugs (smokeleaf joints, flake).
// This file has zero Verse references (the same funnel purity rule as SqueakActionPlan / SqueakTimingModel;
// the kernel harness links it explicitly). Verse sampling (CurJob / curDriver / nutrition / toil name)
// lives in the Runtime adapter (PeriodicStateBinding).

/// <summary>Occurrence granularity for the Eat action. The player switches only decide which stage counts
/// as Eat; no routing, cooldown or audio-selection behaviour changes with them.</summary>
public enum SqueakEatOccurrenceMode { WholeJob, GainingNutrition, ChewingToil }

/// <summary>Pure Eat occurrence-granularity rules.</summary>
public static class SqueakEatOccurrence
{
    /// <summary>Parent switch factory default: false = do not require "really eating", so the whole Ingest
    /// job counts as Eat (including walking to the food). Single source for the runtime static
    /// CompSqueaker.EatPrecisionEnabled and for the settings Scribe default.</summary>
    public const bool EatPrecisionDefault = false;

    /// <summary>Child switch factory default: false = exclude drugs. While the parent is off this value must
    /// stay false (parent off beats child on): the UI clears it on a parent-off write, PostLoadInit
    /// normalises a hand-edited file, and <see cref="ResolveMode"/> already resolves parent-off to
    /// WholeJob.</summary>
    public const bool EatPrecisionIncludeDrugsDefault = false;

    /// <summary>vanilla Core chewing/lighting toil debugName: Toils_Ingest.ChewIngestible is created by
    /// ToilMaker.MakeToil("ChewIngestible") and Toil.ToString() returns that debugName. This depends on the
    /// name, not on a public interface; when the name disappears (Ludeon rename / non-vanilla driver) the
    /// adapter treats it as "unconfirmed" and falls back to full job level instead of going silent.</summary>
    public const string ChewingToilDebugName = "ChewIngestible";

    /// <summary>Two-level switches -> effective mode. Parent off beats the child: whichever value the child
    /// carries, parent off resolves to <see cref="SqueakEatOccurrenceMode.WholeJob"/>.</summary>
    public static SqueakEatOccurrenceMode ResolveMode(bool eatPrecision, bool includeDrugs)
        => !eatPrecision ? SqueakEatOccurrenceMode.WholeJob
            : includeDrugs ? SqueakEatOccurrenceMode.ChewingToil
            : SqueakEatOccurrenceMode.GainingNutrition;

    /// <summary><paramref name="gainingNutritionNow"/> = sampled vanilla IEatingDriver.GainingNutritionNow;
    /// <paramref name="chewToilActive"/> = the current toil name matched <see cref="ChewingToilDebugName"/>;
    /// <paramref name="chewToilNameConfirmed"/> = the adapter has confirmed that toil name exists in this
    /// process. Only <see cref="SqueakEatOccurrenceMode.ChewingToil"/> reads the samples;
    /// <see cref="SqueakEatOccurrenceMode.WholeJob"/> is unconditional and therefore never samples.</summary>
    public static bool AllowsOccurrence(SqueakEatOccurrenceMode mode, bool gainingNutritionNow,
        bool chewToilActive, bool chewToilNameConfirmed) => mode switch
    {
        SqueakEatOccurrenceMode.WholeJob => true,
        SqueakEatOccurrenceMode.GainingNutrition => gainingNutritionNow,
        // Unconfirmed toil name (renamed / non-vanilla driver / not sampled yet this process) -> fall back
        // to full job level; once confirmed, decide on the chewing/lighting toil, with nutrition as an
        // additional safety union.
        _ => !chewToilNameConfirmed || chewToilActive || gainingNutritionNow,
    };
}
