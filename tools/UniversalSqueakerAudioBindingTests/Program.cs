using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using UniversalSqueaker;
using Verse;
using Verse.Sound;

internal static partial class Program
{
    private static int checks;
    private static void Check(bool value, string label)
    {
        checks++;
        if (!value)
        {
            Console.Error.WriteLine("FAIL " + label);
            throw new Exception(label);
        }
        Console.WriteLine("  PASS " + label);
    }

    private static XmlNode Add(XmlDocument document, string name, string attributes = "")
    {
        var temporary = new XmlDocument();
        temporary.LoadXml("<SoundDef " + attributes + "><defName>" + name + "</defName><value>before</value></SoundDef>");
        var node = document.ImportNode(temporary.DocumentElement!, true);
        document.DocumentElement!.AppendChild(node);
        return node;
    }

    private static void Main(string[] args)
    {
        new Harmony("us.s2.controlled.adapter.fixture").PatchAll(Assembly.GetExecutingAssembly());
        if (args.Contains("--partial-observation"))
        {
            PartialObservation();
        }
        else
        {
            InitializationOrder();
            NativePools();
            CompleteBinding();
            CallerConsumption();
            S3PawnFactAdapter();
            S3NewAuthorDoesNotNeedLegacyTable();
            S5InstanceOutputWithRealPatches();
            S4ActualRuntimeCallers();
        }
        Console.WriteLine("S2 controlled adapter fixture PASS: " + checks + " (not native game evidence)");
    }

    private static void InitializationOrder()
    {
        SubSoundDef? nativePool = null;
        bool consumed = false;
        UsAudioBindings.InitializeAfterNativeAudioResolution(() =>
        {
            Check(nativePool != null && UsNativeAudioPool.Read(nativePool) != null,
                "actual startup adapter consumes completed native pool after queued resolution");
            consumed = true;
        });
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            nativePool = new SubSoundDef(new List<ResolvedGrain> { new ResolvedGrain_Clip() }, 1);
        });
        Check(!consumed, "startup observation does not publish resources synchronously or on a time delay");
        LongEventHandler.FixtureExecuteGrowingQueue();
        Check(consumed, "native growing callback queue reaches appended initialization");
    }

    private static void NativePools()
    {
        var clip = new ResolvedGrain_Clip();
        var silence = new ResolvedGrain_Silence();
        Check(UsNativeAudioPool.Read(new SubSoundDef(new List<ResolvedGrain> { clip, silence }, 2))?.Count == 2,
            "complete native clip/silence pool accepted");
        Check(UsNativeAudioPool.Read(new SubSoundDef(new List<ResolvedGrain> { clip }, 0)) == null,
            "half-populated pool without completion rejected");
        Check(UsNativeAudioPool.Read(new SubSoundDef(new List<ResolvedGrain> { clip }, 2)) == null,
            "completion count mismatch rejected");
        Check(UsNativeAudioPool.Read(new SubSoundDef(new List<ResolvedGrain> { new ResolvedGrain() }, 1)) == null,
            "unknown native grain rejected");
        Check(UsNativeAudioPool.Read(new SubSoundDef(new List<ResolvedGrain> { new ResolvedGrain_Clip { clip = null } }, 1)) == null,
            "bad clip beside other resources cannot prove safety");
        foreach (UnityEngine.AudioDataLoadState state in Enum.GetValues(typeof(UnityEngine.AudioDataLoadState)))
        {
            var loading = new ResolvedGrain_Clip { clip = new UnityEngine.AudioClip { loadState = state } };
            Check((UsNativeAudioPool.Read(new SubSoundDef(new List<ResolvedGrain> { loading }, 1)) != null)
                == (state == UnityEngine.AudioDataLoadState.Loaded), "clip data readiness " + state);
        }
    }

    private static void CompleteBinding()
    {
        var document = new XmlDocument();
        document.LoadXml("<Defs />");
        var owner = new ModContentPack { PackageId = "Actual.Provider" };
        var foreign = new ModContentPack { PackageId = "other.provider" };
        var assets = new Dictionary<XmlNode, LoadableXmlAsset>();
        for (int i = 0; i < 15; i++)
        {
            assets.Add(Add(document, "Ratkin" + i), new LoadableXmlAsset { mod = owner });
        }
        var renamed = Add(document, "BeforeRename");
        var replaced = Add(document, "RootReplace");
        var deleted = Add(document, "Deleted");
        var unsafeNode = Add(document, "Unsafe", "Unsafe='true'");
        var wrongSource = Add(document, "Conflicting");
        var failedOriginal = Add(document, "FailedDuplicate");
        foreach (var node in new[] { renamed, replaced, deleted, unsafeNode, wrongSource, failedOriginal })
        {
            assets.Add(node, new LoadableXmlAsset { mod = owner });
        }
        assets.Add(Add(document, "Z_Audio"), new LoadableXmlAsset { mod = new ModContentPack { PackageId = "aaa.audio" } });
        assets.Add(Add(document, "A_Audio"), new LoadableXmlAsset { mod = new ModContentPack { PackageId = "zzz.audio" } });
        var duplicate = Add(document, "Ratkin0");
        assets[duplicate] = new LoadableXmlAsset { mod = foreign };
        var failedDuplicate = Add(document, "FailedDuplicate", "Fail='true'");
        assets[failedDuplicate] = new LoadableXmlAsset { mod = foreign };
        // These declarations exist BEFORE the real production pre-patch observer runs.
        var dormant = Add(document, "Ratkin2", "MayRequire='unloaded.biotech.fixture'");
        assets[dormant] = new LoadableXmlAsset { mod = foreign };
        var inherited = Add(document, "temporary", "ParentName='FixtureAbstract'");
        inherited.RemoveChild(inherited.SelectSingleNode("defName")!);
        assets[inherited] = new LoadableXmlAsset { mod = owner };
        var ambiguousFirst = Add(document, "AmbiguousReplace");
        var ambiguousSecond = Add(document, "AmbiguousReplace");
        assets[ambiguousFirst] = new LoadableXmlAsset { mod = owner };
        assets[ambiguousSecond] = new LoadableXmlAsset { mod = foreign };
        LoadedModManager.FixtureDocument = document;
        LoadedModManager.CombineIntoUnifiedXML(new List<LoadableXmlAsset>(), assets);

        document.SelectSingleNode("Defs/SoundDef[defName='Ratkin1']/value")!.InnerText = "after-field-patch";
        renamed.SelectSingleNode("defName")!.InnerText = "AfterRename";
        document.DocumentElement!.RemoveChild(replaced);
        document.DocumentElement.RemoveChild(deleted);
        Add(document, "RootReplace").SelectSingleNode("value")!.InnerText = "after-root-patch";
        Add(document, "PatchedNew");
        assets[wrongSource] = new LoadableXmlAsset { mod = foreign };
        // Simulate the node returned by native inheritance, without implementing an inheritance loader.
        var inheritedResolved = new XmlDocument();
        inheritedResolved.LoadXml("<SoundDef><defName>InheritedName</defName><value>inherited-final</value></SoundDef>");
        XmlInheritance.ResolvedNodes[inherited] = inheritedResolved.DocumentElement!;
        document.DocumentElement.RemoveChild(ambiguousFirst);
        document.DocumentElement.RemoveChild(ambiguousSecond);
        Add(document, "AmbiguousReplace");

        int loader = 0;
        foreach (XmlNode node in document.DocumentElement.ChildNodes)
        {
            if (ReferenceEquals(node, dormant))
            {
                continue;
            }
            assets.TryGetValue(node, out var asset);
            Def? result = loader++ % 2 == 0
                ? DirectXmlLoader.DefFromNode(node, asset)
                : DirectXmlToObjectNew.DefFromNodeNew(node, asset);
            if (result is SoundDef sound)
            {
                DefDatabase<SoundDef>.AllDefsListForReading.RemoveAll(item => item.defName == sound.defName);
                DefDatabase<SoundDef>.AllDefsListForReading.Add(sound);
            }
        }
        UsAudioBindings.Publish();
        Check(UsAudioBindings.KeyForName("Ratkin0") == null, "final duplicate remains rejected after DefDatabase overwrite");
        for (int i = 1; i < 15; i++)
        {
            string key = "actual.provider/Ratkin" + i;
            Check(UsAudioBindings.KeyForName("Ratkin" + i) == key, "unrelated Ratkin resource survives " + i);
            Check(ReferenceEquals(UsAudioBindings.Resolve(key), DefDatabase<SoundDef>.GetNamedSilentFail("Ratkin" + i)),
                "consumer receives same final object " + i);
        }
        Check(UsAudioBindings.Resolve("actual.provider/Ratkin1")!.FixtureValue == "after-field-patch", "field patch uses final content");
        Check(UsAudioBindings.KeyForName("Ratkin2") == "actual.provider/Ratkin2",
            "pre-observed inactive same-name original does not reject the unique verified final node");
        Check(UsAudioBindings.KeyForName("InheritedName") == "actual.provider/InheritedName",
            "same-node matching asset proves owner when final defName is supplied by native inheritance");
        Check(UsAudioBindings.Resolve("actual.provider/InheritedName")!.FixtureValue == "inherited-final",
            "native inherited final content is the consumed object");
        Check(UsAudioBindings.KeyForName("AmbiguousReplace") == null,
            "missing-asset root replacement still rejects multiple original sources");
        Check(UsAudioBindings.KeyForName("AfterRename") == "actual.provider/AfterRename", "same-node final-name patch preserves proven owner");
        Check(UsAudioBindings.Resolve("actual.provider/BeforeRename") == null, "old resource name not resurrected after rename");
        Check(UsAudioBindings.Resolve("actual.provider/RootReplace")!.FixtureValue == "after-root-patch", "unique whole-root replacement uses final object/content");
        Check(UsAudioBindings.KeyForName("Deleted") == null, "deleted root never revived");
        Check(UsAudioBindings.KeyForName("PatchedNew") == null, "patch-created unknown source not guessed");
        Check(UsAudioBindings.KeyForName("Unsafe") == null, "unsafe native pool isolated");
        Check(UsAudioBindings.KeyForName("Conflicting") == null, "contradictory asset source isolated");
        Check(UsAudioBindings.KeyForName("FailedDuplicate") == null, "failed constructor still counts as final duplicate declaration");
        Check(UsAudioBindings.Resolve("Ratkin1") == null, "consumer cannot fall back to bare defName");
        Check(UsAudioBindings.Resolve("author.referrer/Ratkin1") == null, "entry author cannot claim resource ownership");
    }

    private static void PartialObservation()
    {
        var document = new XmlDocument();
        document.LoadXml("<Defs />");
        var owner = new ModContentPack { PackageId = "actual.provider" };
        var known = Add(document, "HiddenConflict");
        var failure = document.CreateElement("FixtureFailure");
        document.DocumentElement!.AppendChild(failure);
        var duplicate = Add(document, "HiddenConflict");
        var assets = new Dictionary<XmlNode, LoadableXmlAsset>
        {
            [known] = new LoadableXmlAsset { mod = owner },
            [duplicate] = new LoadableXmlAsset { mod = new ModContentPack { PackageId = "other.provider" } }
        };
        LoadedModManager.FixtureDocument = document;
        GenTypes.FailOnFixtureType = true;
        LoadedModManager.CombineIntoUnifiedXML(new List<LoadableXmlAsset>(), assets);
        document.DocumentElement.RemoveChild(duplicate);
        var sound = (SoundDef)DirectXmlLoader.DefFromNode(known, assets[known])!;
        DefDatabase<SoundDef>.AllDefsListForReading.Add(sound);
        UsAudioBindings.Publish();
        Check(UsAudioBindings.KeyForName("HiddenConflict") == null, "incomplete original batch cannot invent unique owner after later duplicate deletion");
        Check(Log.Lines.Any(line => line.Contains("controlled original-observation failure")), "original observation exception context preserved");
        Check(Log.Lines.Any(line => line.Contains("IncompleteSourceObservation")), "dependent binding reports incomplete source observation");
    }
    private sealed class NoRolls : UniversalSqueaker.Kernel.IRollSource
    {
        public double Next01() => throw new Exception("Unexpected adapter selection reroll");
    }

    private static void CallerConsumption()
    {
        var domain = UniversalSqueaker.Kernel.AudioDomains.RaceOnly("Ratkin");
        var loaded = DefDatabase<SoundDef>.GetNamedSilentFail("Ratkin1")!;
        var conflict = DefDatabase<SoundDef>.GetNamedSilentFail("Ratkin0")!;
        var pack = new SqueakVoicePackDef
        {
            FixturePackKey = "entry.author:Pack",
            raceDefName = "Ratkin",
            actions = new List<SqueakVoicePackAction>
            {
                new SqueakVoicePackAction { action = SqueakAction.Work, sounds = new List<SoundDef> { loaded } },
                new SqueakVoicePackAction { action = SqueakAction.Work, ageTag = UniversalSqueaker.Kernel.AgeBucket.Child, sounds = new List<SoundDef> { conflict } }
            }
        };
        var catalog = new SqueakXenotypeCatalogSnapshot();
        catalog.RacePacks.Add(pack);
        catalog.PackByKey.Add(pack.FixturePackKey, pack);
        var selected = new Dictionary<UniversalSqueaker.Kernel.AudioDomain, HashSet<string>>
        {
            [domain] = new HashSet<string> { pack.FixturePackKey }
        };
        int resourceReads = SqueakSoundAvailabilityCache.ResolveCalls;
        var entries = SqueakKernelAdapter.BuildEntries(catalog, selected);
        var pool = new UniversalSqueaker.Kernel.SqueakPoolRegistry(entries, UniversalSqueaker.Kernel.BuiltInFallbackTable.Empty);
        var context = new UniversalSqueaker.Kernel.SelectionContext(domain, "Work", UniversalSqueaker.Kernel.AgeBucket.Adult, true);
        var result = pool.Select(context, UniversalSqueaker.Kernel.SelectionMode.Fallback, SqueakKernelAdapter.GateFor(null, null, null), new NoRolls());
        var choice = SqueakKernelAdapter.ToChoice(result, domain);
        Check(result.Resource?.ToString() == "actual.provider/Ratkin1" && result.PoolStableKey == "entry.author:Pack",
            "normal caller projection separates pack attribution and actual AudioId");
        Check(ReferenceEquals(choice.Sound, loaded), "normal ToChoice consumes the identical validated final SoundDef");
        Check(choice.Selection.SelectedVariant != null && choice.Selection.SelectedContext?.Domain == domain,
            "normal caller retains selected variant/context without reroll");
        var child = new UniversalSqueaker.Kernel.SelectionContext(domain, "Work", UniversalSqueaker.Kernel.AgeBucket.Child, true);
        Check(pool.Select(child, UniversalSqueaker.Kernel.SelectionMode.Fallback, SqueakKernelAdapter.GateFor(null, null, null), new NoRolls()).IsNone,
            "actual adapter preserves rejected exact-age declaration instead of unlocking all-age");
        var orderPack = new SqueakVoicePackDef
        {
            FixturePackKey = "order:Pack",
            raceDefName = "Ratkin",
            actions = new List<SqueakVoicePackAction>
            {
                new SqueakVoicePackAction
                {
                    action = SqueakAction.Call,
                    sounds = new List<SoundDef>
                    {
                        DefDatabase<SoundDef>.GetNamedSilentFail("Z_Audio")!,
                        DefDatabase<SoundDef>.GetNamedSilentFail("A_Audio")!
                    }
                }
            }
        };
        catalog.RacePacks.Add(orderPack);
        selected[domain].Add(orderPack.FixturePackKey);
        var orderEntry = SqueakKernelAdapter.BuildEntries(catalog, selected).Find(item => item.PackKey == orderPack.FixturePackKey)!;
        Check(orderEntry.Actions["Call"][0].SoundKeys.SequenceEqual(new[] { "zzz.audio/A_Audio", "aaa.audio/Z_Audio" }),
            "complete AudioId does not reorder retained defName-based sound draw order");
        selected[domain].Remove(orderPack.FixturePackKey);
        selected[domain].Clear();
        Check(SqueakKernelAdapter.BuildEntries(catalog, selected).Count == 0, "pack disable changes only effective selection");
        selected[domain].Add(pack.FixturePackKey);
        Check(SqueakKernelAdapter.BuildEntries(catalog, selected).Count == 1 && SqueakSoundAvailabilityCache.ResolveCalls == resourceReads,
            "pack reenable does not reload or recheck native resources");
        Check(SqueakKernelAdapter.ToChoice(new UniversalSqueaker.Kernel.ChainResult("Ratkin1", UniversalSqueaker.Kernel.ChainTier.RacePack, "x"), domain).Sound == null,
            "normal caller has no bare-name recovery");
        foreach (SqueakVoicePackMode mode in Enum.GetValues(typeof(SqueakVoicePackMode)))
        {
            var mapped = SqueakKernelAdapter.ToSelectionMode(mode);
            Check((mapped == UniversalSqueaker.Kernel.SelectionMode.Disabled) == (mode == SqueakVoicePackMode.Disabled),
                "Disabled is not mapped to Off " + mode);
        }
        var raw = new UniversalSqueaker.Kernel.BuiltInFallbackTable(new[]
        {
            new UniversalSqueaker.Kernel.FallbackProfile(domain.Race, 1, new Dictionary<string, string> { ["Work"] = "Ratkin1" })
        });
        SqueakFallbackProfileStore.Current = raw;
        Check(SqueakKernelAdapter.BuildBuiltIn().For(domain.Race)!.SoundKeys["Work"] == "actual.provider/Ratkin1",
            "player fallback bare-name data is explicitly adapted to complete binding");
        Check(raw.For(domain.Race)!.SoundKeys["Work"] == "Ratkin1", "compatibility persistence meaning stays unchanged");
    }

}



