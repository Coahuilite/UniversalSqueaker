using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.Responses;
using Verse;

namespace UniversalSqueaker;

/// <summary>Read-side pack labels/attribution from the same admitted normalized audio input.</summary>
internal static class VoicePackDirectory
{
    internal static IReadOnlyList<VoicePackInfo> Directory { get; private set; } = Array.Empty<VoicePackInfo>();
    internal static void Refresh(IReadOnlyList<AudioCandidate> inputs)
    {
        var result = new List<VoicePackInfo>();
        foreach (var declaration in DefDatabase<UsVoicePackDef>.AllDefsListForReading)
        {
            string key = (declaration.modContentPack?.PackageId ?? "") + ":" + declaration.defName;
            var entries = inputs.Where(x => x.PackKey == key).ToArray();
            if (entries.Length == 0 || entries[0].Family != SubjectFamily.PawnRace) continue;
            var first = entries[0];
            result.Add(new VoicePackInfo
            {
                defName = declaration.defName, label = declaration.label, description = declaration.description,
                modContentPack = declaration.modContentPack, weight = (float)first.PackWeight,
                scope = first.ChildDomain == "" ? SqueakVoicePackScope.Race : SqueakVoicePackScope.Xenotype,
                raceDefName = first.Domain, targetDefName = first.ChildDomain, Entries = Array.AsReadOnly(entries)
            });
        }
        Directory = result.AsReadOnly();
    }
}