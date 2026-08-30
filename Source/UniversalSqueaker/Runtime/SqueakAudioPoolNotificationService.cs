using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>One-process notification API for selectable VoicePack domains. UI can render returned state
/// without legacy pool schema; the startup hint is a plain player message until the componentized UI lands.</summary>
public static class SqueakAudioPoolNotificationService
{
    private static bool shownThisProcess;

    public static IReadOnlyList<SqueakVoicePackDomainStatus> GetDomainStatuses(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog)
    {
        List<SqueakVoicePackDomainStatus> result = new();
        foreach (string race in catalog.RaceDefNames)
            result.Add(settings.GetVoicePackSelectionStatus(SqueakVoicePackScope.Race, race));
        if (ModsConfig.BiotechActive)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IReadOnlyList<SqueakVoicePackDef>> pair in catalog.XenotypePacksByDefName)
            {
                foreach (SqueakVoicePackDef pack in pair.Value)
                {
                    if (pack == null || string.IsNullOrEmpty(pack.raceDefName) || string.IsNullOrEmpty(pair.Key)) continue;
                    string identity = pack.raceDefName + "\n" + pair.Key;
                    if (!seen.Add(identity)) continue;
                    result.Add(settings.GetVoicePackSelectionStatus(SqueakVoicePackScope.Xenotype, pack.raceDefName, pair.Key));
                }
            }
        }
        return result;
    }

    public static void EvaluateAndMaybeShow(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog)
    {
        if (shownThisProcess) return;
        List<string> missing = new();
        foreach (SqueakVoicePackDef pack in catalog.PackByKey.Values)
        {
            if (pack == null || !pack.TryGetPackKey(out string key)) continue;
            if (pack.scope == SqueakVoicePackScope.Race)
            {
                if (!HasEnabledKnownPack(settings, pack.scope, pack.raceDefName, "", key))
                    missing.Add("US.Notice.RaceDomain".Translate() + " " + pack.raceDefName);
            }
            else if (pack.scope == SqueakVoicePackScope.Xenotype && ModsConfig.BiotechActive)
            {
                if (!HasEnabledKnownPack(settings, pack.scope, pack.raceDefName, pack.targetDefName, key))
                {
                    string label = catalog.XenotypeByDefName.TryGetValue(pack.targetDefName, out XenotypeDef? xenotype) && xenotype != null
                        ? xenotype.LabelCap + " (" + pack.targetDefName + ")"
                        : pack.targetDefName + " (" + "US.Notice.TargetUnavailable".Translate() + ")";
                    missing.Add(label);
                }
            }
        }
        if (missing.Count == 0) return;
        Messages.Message("US.Notice.AudioPool.Body".Translate() + "\n• " + string.Join("\n• ", missing), MessageTypeDefOf.SilentInput, false);
        shownThisProcess = true;
    }

    private static bool HasEnabledKnownPack(UniversalSqueakerSettings settings, SqueakVoicePackScope scope, string raceDefName, string targetDefName, string packKey)
    {
        VoicePackSelectionRecord? record = (settings.voicePackSelections ?? new List<VoicePackSelectionRecord>())
            .LastOrDefault(x => VoicePackSelectionRecord.SameDomain(x, scope, raceDefName, targetDefName));
        if (record == null) return false;
        HashSet<string> enabledKeys = new(record.enabledPackKeys ?? new List<string>(), StringComparer.Ordinal);
        return enabledKeys.Contains(packKey);
    }
}
