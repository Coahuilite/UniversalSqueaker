using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker.Kernel;

public sealed class AudioPackSelection
{
    public AudioDomain Domain { get; }
    public IReadOnlyList<string> Keys { get; }
    public AudioPackSelection(AudioDomain domain, IEnumerable<string> keys)
    { Domain = domain; Keys = Array.AsReadOnly(keys.ToArray()); }
}

/// <summary>One normalized audio input revision. Selected packs are a decision-time configuration filter.</summary>
public sealed class AudioCatalog
{
    public static readonly AudioCatalog Empty = new(Array.Empty<AudioCandidate>(), BuiltInFallbackTable.Empty);
    private readonly SqueakPoolRegistry selector;
    public IReadOnlyList<VoicePackEntry> Packs { get; }
    private readonly HashSet<AudioDomain> domains;
    public bool HasDomain(AudioDomain domain) => domains.Contains(domain);

    // Review the original supply, never run selection or sample replacement audio.
    internal bool Authorizes(AudioCandidate active, SelectionContext context, SelectionMode mode,
        ISoundGate? gate, Func<AudioDomain, string, bool>? selectedPacks)
        => selector.Authorizes(active, context, mode, gate, selectedPacks);

    public AudioCatalog(IEnumerable<AudioCandidate> entries, BuiltInFallbackTable? fallback = null,
        IEnumerable<VoicePackEntry>? nativePacks = null)
    {
        var packs = new List<VoicePackEntry>(nativePacks ?? Array.Empty<VoicePackEntry>());
        foreach (var pack in entries.GroupBy(x => (Domain: x.SourceDomain, Pack: x.PackKey.Length == 0 ? x.EntryKey : x.PackKey)))
        {
            var actions = new Dictionary<string, IReadOnlyList<ActionSoundSet>>(StringComparer.Ordinal);
            foreach (var action in pack.Where(x => !x.IsFallback).GroupBy(x => x.Action, StringComparer.Ordinal))
                actions.Add(action.Key, Array.AsReadOnly(action.OrderBy(x => x.EntryKey, StringComparer.Ordinal)
                    .Select(x => new ActionSoundSet(x.SoundIds, x.Age, (float)x.Weight, x.Egg, x)).ToArray()));
            var first = pack.First();
            packs.Add(new VoicePackEntry(pack.Key.Pack, pack.Key.Domain,
                (float)(first.PackKey.Length == 0 ? first.Weight : first.PackWeight), actions,
                pack.Where(x => x.IsFallback && x.AudioId != "").ToDictionary(x => x.Action, x => x.AudioId, StringComparer.Ordinal)));
        }
        Packs = Array.AsReadOnly(packs.ToArray());
        domains = new HashSet<AudioDomain>(Packs.Select(x => x.Domain));
        selector = new SqueakPoolRegistry(Packs, fallback ?? BuiltInFallbackTable.Empty);
    }

    public AudioCandidate? Select(SelectionContext context, SelectionMode mode, ISoundGate gate, IRollSource rolls, Func<AudioDomain, string, bool>? selectedPacks = null)
    {
        var chosen = selector.Select(context, mode, gate, rolls, selectedPacks);
        if (chosen.IsNone) return null;
        var entry = chosen.SelectedVariant?.Entry;
        return new AudioCandidate(entry?.EntryKey ?? chosen.SelectedEntryKey!, chosen.SoundKey!, context.ActionKey,
            context.Domain.IsNonPawn ? context.Domain.ObjectTarget! : context.Domain.Race.DefName,
            entry?.Weight ?? chosen.SelectedVariant?.Weight ?? 1, entry?.Volume ?? 1,
            context.Domain.IsNonPawn ? SubjectFamily.NonPawn : SubjectFamily.PawnRace,
            context.Domain.IsNonPawn ? context.Domain.ObjectNamespace! : "", entry?.Pitch ?? 1,
            entry?.Participation, entry?.PackKey ?? chosen.PoolStableKey ?? "", entry?.PackWeight ?? 1,
            context.Domain.Xenotype?.DefName ?? "", chosen.SelectedContext!.Value.Domain, chosen.Tier);
    }
}
