using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker;

/// <summary>Native UI metadata for an admitted AudioCatalog pack. Not a Def or another audio reader.</summary>
public sealed class VoicePackInfo
{
    public string defName = "", label = "", description = "", raceDefName = "", targetDefName = "";
    public ModContentPack? modContentPack;
    public SqueakVoicePackScope scope;
    public float weight = 1;
    public string LabelCap => label;
    public IReadOnlyList<AudioCandidate> Entries = Array.Empty<AudioCandidate>();
    public int CountPlayableActions() => Entries.Select(x => x.Action).Distinct(StringComparer.Ordinal).Count();
    public bool TryGetPackKey(out string key)
    {
        string package = modContentPack?.PackageId ?? "";
        key = package + ":" + defName;
        return package != "" && defName != "";
    }
}
/// <summary>Canonical, last-wins persisted selection for one exact Race or Xenotype domain.
/// raceDefName is required after v4 migration; Xenotype scope additionally requires xenotypeDefName.
/// 2b-2: record domain identity is the (scope, raceDefName, xenotypeDefName) fields themselves —
/// the legacy string domain bridge (ComposeDomainKey/DomainKey) is deleted.
/// targetDefName is a v1 load-only Scribe source and is never serialized or used as record runtime state.</summary>
public class VoicePackSelectionRecord : IExposable
{
    public SqueakVoicePackScope scope = SqueakVoicePackScope.Unspecified;
    public string raceDefName = "";
    public string xenotypeDefName = "";
    public List<string> enabledPackKeys = new();

    public void ExposeData()
    {
        Scribe_Values.Look(ref scope, "scope", SqueakVoicePackScope.Unspecified);
        Scribe_Values.Look(ref raceDefName, "raceDefName", "");
        Scribe_Values.Look(ref xenotypeDefName, "xenotypeDefName", "");
        Scribe_Collections.Look(ref enabledPackKeys, "enabledPackKeys", LookMode.Value);
        enabledPackKeys ??= new List<string>();
    }

    /// <summary>2b-2: record domain identity (scope, raceDefName, xenotypeDefName) field match.
    /// Consumers pass the exact race they are addressing; there is no implicit product race.</summary>
    internal static bool SameDomain(VoicePackSelectionRecord? record, SqueakVoicePackScope scope, string raceDefName, string xenotypeDefName)
        => record != null
           && record.scope == scope
           && string.Equals(record.raceDefName, raceDefName, StringComparison.Ordinal)
           && string.Equals(record.xenotypeDefName, xenotypeDefName, StringComparison.Ordinal);
}

public readonly struct SqueakVoicePackDomainStatus
{
    public readonly SqueakVoicePackDomainState State;
    public readonly IReadOnlyList<string> EnabledKeys;
    public SqueakVoicePackDomainStatus(SqueakVoicePackDomainState state, IReadOnlyList<string> enabledKeys) { State = state; EnabledKeys = enabledKeys; }
}
