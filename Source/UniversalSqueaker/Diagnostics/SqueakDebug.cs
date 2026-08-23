using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Minimal production-facing debug surface. US 0.1.x has no statistics, overlay, mote, camera indicator,
/// or audio-path diagnostic classes; only the successful-dispatch log wiring survives so the usdiag
/// routing record keeps working. Business code must not reference this class for diagnostics UI.
/// </summary>
public static class SqueakDebug
{
    private sealed class AudioSample { public float nextDetail; public int dispatched; public int suppressed; }
    private static readonly Dictionary<SqueakAction, AudioSample> audioSamples = new();
    private static float nextSummary;

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

    /// <summary>Detailed logging is independent from any future visual diagnostics. One route record per
    /// action per 5-second window; suppressed counts roll up into a 60-second summary.</summary>
    private static void NotifyAudioDispatched(Pawn pawn, SqueakAction action, SqueakSoundChoice choice)
    {
        if (!SqueakLog.EffectiveDevLogging) return;
        SoundDef? def = choice.Sound;
        if (def == null) return;
        float now = Time.realtimeSinceStartup;
        if (!audioSamples.TryGetValue(action, out AudioSample? sample)) { sample = new AudioSample(); audioSamples.Add(action, sample); }
        sample.dispatched++;
        if (now >= sample.nextDetail)
        {
            SqueakLog.AudioRouteSelected(
                UniversalSqueaker.Kernel.ActionKey.For(action) ?? action.ToString(),
                pawn.def?.defName ?? "",
                pawn.genes?.Xenotype?.defName,
                pawn.thingIDNumber.ToString(),
                def.defName,
                ProtocolTier(choice.Source),
                choice.PoolStableKey,
                choice.IsEgg,
                sample.suppressed,
                pawn.LabelShort,
                pawn.ThingID,
                pawn.IsPlayerControlled,
                pawn.Faction?.def?.defName ?? "-");
            sample.suppressed = 0;
            sample.nextDetail = now + 5f;
        }
        else sample.suppressed++;
        if (now < nextSummary) return;
        int dispatched = 0, suppressed = 0;
        foreach (AudioSample item in audioSamples.Values) { dispatched += item.dispatched; suppressed += item.suppressed; item.dispatched = 0; item.suppressed = 0; }
        nextSummary = now + 60f;
        SqueakLog.TriggerOutcomeSummary(dispatched, suppressed);
    }
}
