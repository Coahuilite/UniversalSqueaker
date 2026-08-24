using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Minimal production-facing debug surface. Every successful audio dispatch emits one dev-level route
/// record; vanilla fallback dispatches are warning-level. No statistical/UI diagnostics are retained.
/// </summary>
public static class SqueakDebug
{
    /// <summary>usdiag v2 tier vocabulary: xenotype_pack / race_pack / vanilla / "-" for none.
    /// PackFallback folds into RacePack and BuiltInFallback into Vanilla.</summary>
    private static string ProtocolTier(SqueakSoundSource source) => source switch
    {
        SqueakSoundSource.XenotypePack => "xenotype_pack",
        SqueakSoundSource.RacePack => "race_pack",
        SqueakSoundSource.Vanilla => "vanilla",
        _ => "-",
    };

    public static void NotifySqueak(Pawn pawn, SqueakAction action, SqueakMood mood, SqueakSoundChoice choice)
    {
        NotifyAudioDispatched(pawn, action, choice);
    }

    /// <summary>Log MVP: one dev-level record per actual dispatch. Vanilla fallback is warning-level;
    /// normal pack routing stays info-level. No rate limiting or suppression in this MVP.</summary>
    private static void NotifyAudioDispatched(Pawn pawn, SqueakAction action, SqueakSoundChoice choice)
    {
        if (!SqueakLog.EffectiveDevLogging) return;
        SoundDef? def = choice.Sound;
        if (def == null) return;

        string actionKey = UniversalSqueaker.Kernel.ActionKey.For(action) ?? action.ToString();
        string race = pawn.def?.defName ?? "";
        string? xenotype = pawn.genes?.Xenotype?.defName;
        string target = pawn.thingIDNumber.ToString();
        string pawnId = pawn.ThingID;
        string? pawnFaction = pawn.Faction?.def?.defName ?? "-";
        bool? pawnControlled = pawn.IsPlayerControlled;

        if (choice.Source == SqueakSoundSource.Vanilla)
        {
            SqueakLog.AudioVanillaFallback(
                actionKey,
                race,
                xenotype,
                target,
                def.defName,
                "vanilla",
                choice.PoolStableKey,
                choice.IsEgg,
                pawn.LabelShort,
                pawnId,
                pawnControlled,
                pawnFaction);
            return;
        }

        SqueakLog.AudioRouteSelected(
            actionKey,
            race,
            xenotype,
            target,
            def.defName,
            ProtocolTier(choice.Source),
            choice.PoolStableKey,
            choice.IsEgg,
            0,
            pawn.LabelShort,
            pawnId,
            pawnControlled,
            pawnFaction);
    }
}