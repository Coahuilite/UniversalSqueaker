using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker.Authoring;

/// <summary>Definition-stage audio input validation. It owns no behavior configuration or session.</summary>
public sealed class AudioInputBuilder
{
    private readonly DefinitionCatalog definitions;
    private readonly List<AudioCandidate> audio = new();
    private readonly HashSet<string> packKeys = new(StringComparer.Ordinal);
    public readonly List<string> Issues = new();
    public AudioCandidate[] Entries => audio.ToArray();
    public AudioInputBuilder(DefinitionCatalog definitions) { this.definitions = definitions; }
    public bool AddPack(string packageId, string defName, VoicePackDeclaration declaration, Func<string, string?> bindSound,
        string? selectionKey = null)
    {
        string key = packageId + "/" + defName;
        try
        {
            if (!AuthorXml.ExactKey(packageId) || !AuthorXml.ExactKey(defName) || declaration.FormatVersion != 1 || packKeys.Contains(key))
            {
                throw new ArgumentException("Invalid/duplicate pack identity");
            }
            if (double.IsNaN(declaration.Weight) || double.IsInfinity(declaration.Weight)
                || declaration.Weight <= 0 || declaration.Weight > float.MaxValue)
            {
                throw new ArgumentException("Invalid pack weight");
            }
            if (declaration.ActionParticipation.Keys.Any(x => !AuthorXml.ExactKey(x)))
            {
                throw new ArgumentException("Invalid pack-local action participation identity");
            }

            if (declaration.Race != "" && (declaration.Family != "Pawn" || declaration.Target != ""))
                throw new ArgumentException("Use one exact target declaration");
            var target = declaration.Domain;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var batch = new List<AudioCandidate>();
            foreach (var entry in declaration.Entries)
            {
                if (!AuthorXml.ExactKey(entry.Key) || !seen.Add(entry.Key))
                {
                    throw new ArgumentException("Duplicate entry key");
                }

                if (entry.Enabled == false)
                {
                    continue;
                }

                if (!definitions.HasAction(entry.Action))
                {
                    Issues.Add(key + ":Missing action " + entry.Action);
                    continue;
                }

                if ((entry.Sound == "") == (entry.Sounds.Count == 0) || entry.Age.HasValue && !Enum.IsDefined(typeof(Kernel.AgeBucket), entry.Age.Value))
                    throw new ArgumentException("Use exactly one sound or sounds list and a valid age");
                var sounds = new List<string>();
                var references = entry.Sound != "" ? (IEnumerable<string>)new[] { entry.Sound } : entry.Sounds;
                foreach (string name in references.OrderBy(x => x, StringComparer.Ordinal))
                {
                    if (!AuthorXml.ExactKey(name)) throw new ArgumentException("Exact sound reference required");
                    string? bound = bindSound(name);
                    if (bound == null) Issues.Add(key + ":Unbound sound " + name);
                    else if (!sounds.Contains(bound)) sounds.Add(bound);
                }

                var participation = new Dictionary<string, bool>(declaration.Participation, StringComparer.Ordinal);
                if (declaration.ActionParticipation.TryGetValue(entry.Action, out var actionDefaults))
                {
                    foreach (var setting in actionDefaults)
                    {
                        participation[setting.Key] = setting.Value;
                    }
                }
                foreach (var setting in entry.Participation)
                {
                    participation[setting.Key] = setting.Value;
                }
                if (participation.Keys.Any(x => !AuthorXml.ExactKey(x)))
                {
                    throw new ArgumentException("Invalid entry participation identity");
                }
                batch.Add(new AudioCandidate(key + "/" + entry.Key, sounds.FirstOrDefault() ?? "", entry.Action, target.Target,
                    entry.Weight, entry.Volume ?? 1, target.Family == "Pawn" ? SubjectFamily.PawnRace : SubjectFamily.NonPawn, target.Namespace, pitch: entry.Pitch, participation: participation,
                    packKey: selectionKey ?? packageId + ":" + defName, packWeight: declaration.Weight, childDomain: target.Child,
                    sounds: sounds, age: entry.Age, egg: entry.Egg));
            }
            foreach (var fallback in declaration.Fallbacks)
            {
                if (!definitions.HasAction(fallback.Key)) { Issues.Add(key + ":Missing fallback action " + fallback.Key); continue; }
                string? bound = bindSound(fallback.Value);
                if (bound == null) { Issues.Add(key + ":Unbound fallback " + fallback.Value); continue; }
                batch.Add(new AudioCandidate(key + "/fallback/" + fallback.Key, bound, fallback.Key, target.Target, 1,
                    family: target.Family == "Pawn" ? SubjectFamily.PawnRace : SubjectFamily.NonPawn, targetNamespace: target.Namespace,
                    packKey: selectionKey ?? packageId + ":" + defName, packWeight: declaration.Weight, childDomain: target.Child, fallback: true));
            }

            audio.AddRange(batch);
            packKeys.Add(key);
            return true;
        }
        catch (Exception error)
        {
            Issues.Add(key + ":" + error.Message);
            return false;
        }
    }

}
