using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using Verse;

internal static partial class Program
{
    private static List<VoicePackEntry> ProjectSelection(SqueakXenotypeCatalogSnapshot directory, Dictionary<AudioDomain, HashSet<string>> selection)
    {
        SqueakerMod.Settings.voicePackSelections = selection.Select(x => new VoicePackSelectionRecord
        {
            scope = x.Key.Xenotype.HasValue ? SqueakVoicePackScope.Xenotype : SqueakVoicePackScope.Race,
            raceDefName = x.Key.Race.DefName, xenotypeDefName = x.Key.Xenotype?.DefName ?? "", enabledPackKeys = x.Value.ToList()
        }).ToList();
        SqueakerMod.Settings.PublishBehaviorConfiguration();
        return SqueakerRuntime.Audio.Packs.Where(x => selection.TryGetValue(x.Domain, out var keys) && keys.Contains(x.PackKey)).ToList();
    }

    private static void CallerConsumption()
    {
        var domain = AudioDomains.RaceOnly("Ratkin");
        var definitions = new DefinitionCatalog(0, 0, true);
        var builder = new AudioInputBuilder(definitions);
        var pack = new VoicePackDeclaration { Race = "Ratkin" };
        pack.Entries.Add(new VoiceEntry { Key = "all", Action = CoreActions.Work, Sound = "Ratkin1" });
        pack.Entries.Add(new VoiceEntry { Key = "child", Action = CoreActions.Work, Sound = "Ratkin0", Age = AgeBucket.Child });
        Check(builder.AddPack("entry.author", "Pack", pack, UsAudioBindings.KeyForName), "canonical caller validates author audio directly");
        var audio = new AudioCatalog(builder.Entries);
        var context = new SelectionContext(domain, CoreActions.Work, AgeBucket.Adult, true);
        var chosen = audio.Select(context, SelectionMode.Fallback, SqueakKernelAdapter.GateFor(null, null, null), new NoRolls());
        Check(chosen?.AudioId == "actual.provider/Ratkin1" && chosen.PackKey == "entry.author:Pack", "normal caller keeps pack attribution separate from complete AudioId");
        Check(ReferenceEquals(UsAudioBindings.Resolve(chosen!.AudioId), DefDatabase<SoundDef>.GetNamedSilentFail("Ratkin1")), "normal caller consumes identical validated final SoundDef");
        Check(chosen.EntryKey == "entry.author/Pack/all" && chosen.SourceDomain == domain, "canonical entry/context survive without metadata reverse mapping or reroll");
        Check(audio.Select(new SelectionContext(domain, CoreActions.Work, AgeBucket.Child, true), SelectionMode.Fallback,
            SqueakKernelAdapter.GateFor(null, null, null), new NoRolls()) == null, "rejected exact-age declaration never unlocks all-age");
        var order = new VoicePackDeclaration { Race = "Ratkin" };
        order.Entries.Add(new VoiceEntry { Key = "order", Action = CoreActions.Call, Sounds = new List<string> { "Z_Audio", "A_Audio" } });
        Check(builder.AddPack("order", "Pack", order, UsAudioBindings.KeyForName), "multi-sound declaration uses same canonical input");
        var expanded = new AudioCatalog(builder.Entries);
        Check(expanded.Packs.Single(x => x.PackKey == "order:Pack").Actions[CoreActions.Call][0].SoundKeys.SequenceEqual(new[] { "zzz.audio/A_Audio", "aaa.audio/Z_Audio" }), "full AudioId does not reorder declared sound-name normalization");
        int reads = SqueakSoundAvailabilityCache.ResolveCalls;
        var selected = new HashSet<string>(StringComparer.Ordinal);
        bool Filter(AudioDomain exact, string key) => exact == domain && selected.Contains(key);
        Check(audio.Select(context, SelectionMode.Fallback, SqueakKernelAdapter.GateFor(null, null, null), new NoRolls(), Filter) == null, "disabled pack filters the same catalog");
        selected.Add("entry.author:Pack");
        Check(audio.Select(context, SelectionMode.Fallback, SqueakKernelAdapter.GateFor(null, null, null), new NoRolls(), Filter) != null
            && SqueakSoundAvailabilityCache.ResolveCalls == reads, "reenable uses same catalog without native pool rebuild");
        Check(UsAudioBindings.Resolve("Ratkin1") == null, "normal caller has no bare-name recovery");
        var source = new BuiltInFallbackTable(new[] { new FallbackProfile(domain.Race, 1, new Dictionary<string, string> { [CoreActions.Work] = "Ratkin1" }) });
        SqueakFallbackProfileStore.Current = source;
        Check(SqueakKernelAdapter.BuildBuiltIn().For(domain.Race)!.SoundKeys[CoreActions.Work] == "actual.provider/Ratkin1", "final fallback data is normalized once at audio boundary");
        Check(source.For(domain.Race)!.SoundKeys[CoreActions.Work] == "Ratkin1", "normalization does not mutate persisted resource references");
    }
}
