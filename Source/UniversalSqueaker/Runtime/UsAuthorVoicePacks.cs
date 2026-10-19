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
            var pack = new SqueakVoicePackDef
            {
                defName = declaration.defName,
                label = declaration.label,
                description = declaration.description,
                modContentPack = declaration.modContentPack,
                IsKernelDeclaration = true,
                scope = SqueakVoicePackScope.Race,
                raceDefName = declaration.declaration.Race
            };
            if (!pack.TryGetPackKey(out string key))
            {
                continue;
            }
            var audio = catalog.Audio.Where(x => x.PackKey == key).ToArray();
            if (audio.Length == 0)
            {
                continue;
            }
            foreach (var entry in audio)
            {
                var action = catalog.Actions.Single(x => x.Key == entry.Action);
                string baseKey = action.BaseAction.Substring("us.core/".Length);
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
        var selected = new HashSet<string>(StringComparer.Ordinal);
        var pool = registry.PoolFor(domain.Parent);
        if (pool != null)
        {
            foreach (var entry in pool.Entries)
            {
                selected.Add(entry.PackKey);
            }
        }
        // Current new declaration format is Race-only; a child selection never substitutes for its parent.
        return audio.Where(x => selected.Contains(x.PackKey)).ToArray();
    }
}
