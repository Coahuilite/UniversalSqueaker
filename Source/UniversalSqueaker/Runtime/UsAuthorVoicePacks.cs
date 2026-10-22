using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.OneShot;
using Verse;

namespace UniversalSqueaker;

/// <summary>Projects validated new declarations into the existing exact-domain pack directory and selection authority.</summary>
internal static class UsAuthorVoicePacks
{
    internal static IReadOnlyList<SqueakVoicePackDef> Directory { get; private set; } = Array.Empty<SqueakVoicePackDef>();

    internal static void Refresh(AuthorCatalog catalog)
    {
        var result = new List<SqueakVoicePackDef>();
        foreach (var declaration in DefDatabase<UsVoicePackDef>.AllDefsListForReading)
        {
            var identity = new SqueakVoicePackDef { defName = declaration.defName, modContentPack = declaration.modContentPack };
            if (!identity.TryGetPackKey(out string key)) continue;
            var audio = catalog.Audio.Where(x => x.PackKey == key).ToArray();
            if (audio.Length == 0 || audio[0].Family != SubjectFamily.PawnRace) continue;
            var first = audio[0]; // Only validated, captured declarations may enter the live directory.
            var pack = new SqueakVoicePackDef
            {
                defName = declaration.defName,
                label = declaration.label,
                description = declaration.description,
                modContentPack = declaration.modContentPack,
                IsKernelDeclaration = true,
                weight = (float)first.PackWeight,
                scope = first.ChildDomain == "" ? SqueakVoicePackScope.Race : SqueakVoicePackScope.Xenotype,
                raceDefName = first.Domain,
                targetDefName = first.ChildDomain
            };
            pack.KernelActionKeys = Array.AsReadOnly(audio.Select(x => x.Action).Distinct(StringComparer.Ordinal).ToArray());
            foreach (var entry in audio)
            {
                string baseKey = ActionKey.SchemaKey(entry.Action);
                if (Enum.TryParse(baseKey, out SqueakAction builtIn))
                {
                    var sound = UsAudioBindings.Resolve(entry.AudioId);
                    if (sound != null)
                    {
                        pack.actions.Add(new SqueakVoicePackAction { action = builtIn, sounds = new List<SoundDef> { sound } });
                    }
                }
            }
            result.Add(pack);
        }
        Directory = result.AsReadOnly();
    }

    internal static AudioCandidate[] Selected(AudioCandidate[] audio, SqueakPoolRegistry registry, AudioDomain domain)
    {
        return audio.Where(x => x.Family == SubjectFamily.PawnRace && x.Domain == domain.Race.DefName
            && (x.ChildDomain == "" || x.ChildDomain == domain.Xenotype?.DefName)
            && registry.PoolFor(x.SourceDomain)?.Entries.Any(e => e.PackKey == x.PackKey) == true).ToArray();
    }
}
