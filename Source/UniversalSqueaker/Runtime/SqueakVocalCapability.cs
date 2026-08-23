using System;

namespace UniversalSqueaker;

internal enum SqueakVocalGateDecision { Allowed, VocalOrgansSilent, TalkingRollRejected }

/// <summary>Pure sampled vocal inputs with no runtime-domain dependencies.</summary>
internal readonly struct SqueakVocalCapability
{
    internal const float VocalSilenceThreshold = 0.001f;
    internal const float TalkingCertainThreshold = 0.999f;
    public readonly float VocalOrganEfficiency;
    public readonly float TalkingChance;

    public SqueakVocalCapability(float vocalOrganEfficiency, float talkingChance)
    {
        VocalOrganEfficiency = float.IsNaN(vocalOrganEfficiency) || float.IsInfinity(vocalOrganEfficiency) ? 0f : Math.Max(0f, vocalOrganEfficiency);
        TalkingChance = float.IsNaN(talkingChance) ? 0f : Math.Max(0f, Math.Min(1f, talkingChance));
    }

    public bool RequiresTalkingRoll(bool applyTalkingGate) => applyTalkingGate
        && VocalOrganEfficiency > VocalSilenceThreshold && TalkingChance < TalkingCertainThreshold;

    public SqueakVocalGateDecision Decide(bool applyTalkingGate, float roll)
    {
        if (VocalOrganEfficiency <= VocalSilenceThreshold) return SqueakVocalGateDecision.VocalOrgansSilent;
        return RequiresTalkingRoll(applyTalkingGate) && !(roll < TalkingChance)
            ? SqueakVocalGateDecision.TalkingRollRejected : SqueakVocalGateDecision.Allowed;
    }
}
