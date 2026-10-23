using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker.KernelTests;

/// <summary>验证门单测。每断言独立输出；失败计数由 Program 汇总。</summary>
public static partial class UnitTests
{
    public static void RunAll(ref int failures)
    {
        DomainKeys(ref failures);
        ActionKeyMapping(ref failures);
        VerseEventBindingContract(ref failures);
        ActionSync(ref failures);
        BuiltInTable(ref failures);
        FailurePathFallback(ref failures);
        PoolOrdering(ref failures);
        DirectLifeStageContract(ref failures);
        SelectOff(ref failures);
        SelectFallbackChain(ref failures);
        SelectRemix(ref failures);
        RemixEmptyTierRegression.RunAll(ref failures);
        DistributionBounds(ref failures);
        Determinism(ref failures);
        FallbackCopyLifecycle(ref failures);
        SoundLevelFilter(ref failures);
        EntryLevelFilter(ref failures);
        DomainStatus(ref failures);
        ModulationRules(ref failures);
        AgeDefault(ref failures);
        AgePriority(ref failures);
        VariantWeightMixing(ref failures);
        PackFallbackTier(ref failures);
        PackFallbackExactDomainOnly(ref failures);
        EggFiltering(ref failures);
        PackWeight(ref failures);
        SelectInvalidGuards(ref failures);
        TimingModelRules(ref failures);
        EatOccurrenceRules(ref failures);
        TriggerInvocationRules(ref failures);
        PerRacePoolIsolation(ref failures);
        EqualRouting(ref failures);
        LayeredTuning(ref failures);
        XenoDoubleKeyDomains(ref failures);
    }

    private static void Check(bool condition, string name, ref int failures)
    {
        if (condition) Console.WriteLine("  ok: " + name);
        else { Console.Error.WriteLine("  FAIL: " + name); failures++; }
    }



    private static void DomainKeys(ref int failures)
    {
        RaceKey a = new("US_Race_A");
        RaceKey b = new("US_Race_A");
        RaceKey c = new("us_race_a"); // Ordinal 区分大小写
        Check(a == b && a.GetHashCode() == b.GetHashCode(), "RaceKey value equality", ref failures);
        Check(a != c, "RaceKey case-sensitive", ref failures);
        Check(new AudioDomain(a, null) == new AudioDomain(b, null), "AudioDomain race-only equality", ref failures);
        Check(new AudioDomain(a, new XenotypeKey("US_Xeno_A")) == new AudioDomain(a, new XenotypeKey("US_Xeno_A")), "AudioDomain xenotype equality", ref failures);
        Check(new AudioDomain(a, null) != new AudioDomain(a, new XenotypeKey("US_Xeno_A")), "AudioDomain null-vs-xeno distinct", ref failures);
        Check(new AudioDomain(a, null).ToString() == "US_Race_A", "AudioDomain ToString race", ref failures);
        Check(new AudioDomain(a, new XenotypeKey("US_Xeno_A")).ToString() == "US_Race_A+US_Xeno_A", "AudioDomain ToString xeno", ref failures);

        Check(!AudioDomains.TryCreate("   ", "X", out _) && !AudioDomains.TryCreate("", "X", out _),
            "AudioDomains.TryCreate rejects whitespace/empty race", ref failures);
        Check(!AudioDomains.TryCreate("Ratkin", "   ", out _),
            "AudioDomains.TryCreate rejects whitespace-only xeno", ref failures);
        Check(AudioDomains.TryCreate("Ratkin", "", out AudioDomain emptyXeno) && emptyXeno.IsRaceOnly,
            "AudioDomains.TryCreate keeps empty xeno as race-only", ref failures);
        bool raceOnlyRejected = false;
        try
        {
            _ = AudioDomains.RaceOnly("   ");
        }
        catch (ArgumentException)
        {
            raceOnlyRejected = true;
        }
        Check(raceOnlyRejected, "AudioDomains.RaceOnly rejects whitespace-only race", ref failures);
    }





    /// <summary>三方同步离线断言：① 枚举名序 == BuiltInActionKeys 17 键序 == 动作名镜像序；
    /// ② 内置表 15 项且不列 Crying/Giggling（SoundDef 面默认静默）；
    /// ③ 镜像 17 == 枚举 17（语料 17 动作矩阵同变更重建）。
    /// 数据面（SoundDef/本地化）同步在 US 数据面建成后另行覆盖。</summary>
    private static void ActionSync(ref int failures)
    {
        string[] enumNames = CoreActions.All.ToArray();
        bool namesMatch = enumNames.Length == BuiltInActionKeys.All.Count && enumNames.Length == ActionKeyMirror.Count;
        for (int i = 0; i < enumNames.Length && namesMatch; i++)
            namesMatch = enumNames[i] == BuiltInActionKeys.All[i] && enumNames[i] == ActionKeyMirror.All[i];
        Check(namesMatch, "three-way sync: complete IDs == closed official membership == fixture mirror (15)", ref failures);

        FallbackProfile? profile = Scenarios.BuildBuiltIn().For(Scenarios.RaceA);
        bool builtInExcludes = profile != null && profile.SoundKeys.Count == ActionKeyMirror.BuiltInCount
            && !profile.SoundKeys.ContainsKey("Crying") && !profile.SoundKeys.ContainsKey("Giggling");
        Check(builtInExcludes, "three-way sync: built-in table 15 mappings without Crying/Giggling (default silence)", ref failures);

        Check(ActionKeyMirror.Count == 15 && ActionKeyMirror.BuiltInCount == 15,
            "three-way sync: fifteen official actions and fifteen fallback mappings", ref failures);
    }

    private static void BuiltInTable(ref int failures)
    {
        BuiltInFallbackTable table = Scenarios.BuildBuiltIn();
        FallbackProfile? profile = table.For(Scenarios.RaceA);
        Check(profile != null && profile.SoundKeys.Count == ActionKeyMirror.BuiltInCount && !profile.SoundKeys.ContainsKey("Crying"),
            "built-in RaceA profile has current 15 mappings", ref failures);
        Check(table.For(new RaceKey("US_Race_Unknown")) == null, "unknown race returns null", ref failures);
        Check(table.TryGetSoundKey(Scenarios.RaceA, CoreActions.Call, out string? k) && k == "US_Fallback_Call", "TryGetSoundKey full Call=US_Fallback_Call", ref failures);
        Check(!table.TryGetSoundKey(Scenarios.RaceA, "other.mod:US_X", out _) && !table.TryGetSoundKey(Scenarios.RaceA, "Crying", out _),
            "built-in lookup rejects external and reserved-unmapped keys", ref failures);
        bool retiredRejected = false;
        try { _ = new FallbackProfile(Scenarios.RaceA, 1, new Dictionary<string, string> { ["Crying"] = "US_Retired" }); }
        catch (ArgumentException) { retiredRejected = true; }
        Check(retiredRejected, "closed official fallback rejects retired slots; third-party contracts have independent mappings", ref failures);
        bool rejected = false;
        try
        {
            _ = new FallbackProfile(Scenarios.RaceA, 1, new Dictionary<string, string> { ["other.mod:US_X"] = "US_X" });
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Check(rejected, "FallbackProfile rejects non-built-in action keys", ref failures);
        Check(BuiltInFallbackTable.Empty.For(Scenarios.RaceA) == null, "empty built-in table has no profile", ref failures);
    }

    private static void FallbackCopyLifecycle(ref int failures)
    {
        FallbackProfile source = new(Scenarios.RaceA, 3, new Dictionary<string, string>
        {
            [CoreActions.Call] = "US_Call_Source",
            [CoreActions.Eat] = "US_Eat_Source",
        });
        FallbackDelta delta = new(new Dictionary<string, string> { [CoreActions.Call] = "Core_Call_Override" });

        Check(FallbackProfileOperations.DecideCopy(source, delta, 3, true) == CopyDisposition.RebuildFromSource,
            "fallback corrupt copy rebuilds from source", ref failures);
        // VF1 finalized ruling (2026-10-07): a version bump NEVER drops the player's delta - only
        // corruption rebuilds. The old "older copy rebuilds from source" verdict was the E-scenario
        // defect this replaces.
        Check(FallbackProfileOperations.DecideCopy(source, delta, 2, false) == CopyDisposition.MergeDelta,
            "fallback older copy KEEPS its delta (version bump is not a deletion trigger)", ref failures);
        Check(FallbackProfileOperations.DecideCopy(source, delta, 3, false) == CopyDisposition.MergeDelta,
            "fallback current copy with delta merges", ref failures);
        Check(FallbackProfileOperations.DecideCopy(source, null, 3, false) == CopyDisposition.KeepCopy,
            "fallback current copy without delta keeps copy", ref failures);
        Check(FallbackProfileOperations.DecideCopy(source, null, 1, false) == CopyDisposition.KeepCopy
                && FallbackProfileOperations.NeedsRestamp(source, 1),
            "fallback stale clean copy keeps content but needs a version re-stamp", ref failures);
        Check(FallbackProfileOperations.DecideCopy(source, new FallbackDelta(new Dictionary<string, string>()), 3, false) == CopyDisposition.MergeDelta,
            "fallback current copy with explicit empty delta merges", ref failures);

        FallbackProfile merged = FallbackProfileOperations.Merge(source, delta);
        Check(merged.Race == source.Race && merged.Version == source.Version
            && merged.SoundKeys[CoreActions.Call] == "Core_Call_Override" && merged.SoundKeys[CoreActions.Eat] == "US_Eat_Source",
            "fallback merge preserves source identity and untouched keys", ref failures);

        // VF1: the empty sound value is the LEGAL explicit delete marker - the key is present (the
        // player touched it) but resolves to no sound; merge removes the source entry.
        FallbackDelta withDelete = new(new Dictionary<string, string>
        {
            [CoreActions.Call] = "Core_Call_Override",
            [CoreActions.Eat] = "",
        });
        FallbackProfile deleted = FallbackProfileOperations.Merge(source, withDelete);
        Check(deleted.SoundKeys[CoreActions.Call] == "Core_Call_Override" && !deleted.SoundKeys.ContainsKey(CoreActions.Eat)
                && !deleted.TryGetSoundKey(CoreActions.Eat, out _),
            "fallback delete marker removes the source entry while other keys merge normally", ref failures);
        FallbackProfile playerOnly = FallbackProfileOperations.Merge(
            new FallbackProfile(Scenarios.RaceA, 0, new Dictionary<string, string>()), withDelete);
        Check(playerOnly.SoundKeys.Count == 1 && playerOnly.SoundKeys[CoreActions.Call] == "Core_Call_Override",
            "a player-only table merges over the empty profile (delete markers of absent keys are inert)",
            ref failures);
        bool nullRejected = false;
        try
        {
            _ = new FallbackDelta(new Dictionary<string, string> { [CoreActions.Call] = null! });
        }
        catch (ArgumentException)
        {
            nullRejected = true;
        }
        Check(nullRejected, "a NULL sound is still rejected - only the empty string is the marker", ref failures);

        bool rejected = false;
        try
        {
            _ = new FallbackDelta(new Dictionary<string, string> { ["external.mod:Call"] = "Core_Call" });
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Check(rejected, "fallback delta rejects external action key", ref failures);

        Dictionary<string, string> mutableProfileKeys = new() { [CoreActions.Call] = "US_Call_Original" };
        FallbackProfile copiedProfile = new(Scenarios.RaceA, 3, mutableProfileKeys);
        mutableProfileKeys[CoreActions.Call] = "US_Call_Mutated";
        Check(copiedProfile.SoundKeys[CoreActions.Call] == "US_Call_Original",
            "FallbackProfile defensively copies input dictionary", ref failures);

        Dictionary<string, string> mutableDeltaKeys = new() { [CoreActions.Call] = "US_Delta_Original" };
        FallbackDelta copiedDelta = new(mutableDeltaKeys);
        mutableDeltaKeys[CoreActions.Call] = "US_Delta_Mutated";
        Check(copiedDelta.Overrides[CoreActions.Call] == "US_Delta_Original",
            "FallbackDelta defensively copies input dictionary", ref failures);
    }

    /// <summary>错误路径回归：无池条目 + 种子内置表 + Off 仍放内置兜底；
    /// 空内置表才静音。</summary>
    private static void FailurePathFallback(ref int failures)
    {
        SqueakPoolRegistry failureRegistry = new(Array.Empty<VoicePackEntry>(), Scenarios.BuildBuiltIn());
        ChainResult hit = failureRegistry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Off, SimGate.All, new LcgRandom(1));
        Check(hit.Tier == ChainTier.BuiltInFallback && hit.SoundKey == "US_Fallback_Call" && hit.PoolStableKey == null, "failure-path registry resolves built-in US_Fallback_Call", ref failures);
        SqueakPoolRegistry emptyTable = new(Array.Empty<VoicePackEntry>(), BuiltInFallbackTable.Empty);
        ChainResult none = emptyTable.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Off, SimGate.All, new LcgRandom(1));
        Check(none.IsNone, "empty built-in table resolves to silence", ref failures);
    }

    private static void DirectLifeStageContract(ref int failures)
    {
        // The adapter maps RimWorld's DevelopmentalStage directly; this pure mirror locks the
        // Contract decision without linking Verse into the kernel characterization project.
        Check(DirectLifeStageBucket("Newborn") == AgeBucket.Baby && DirectLifeStageBucket("Baby") == AgeBucket.Baby,
            "life-stage Newborn/Baby map to Baby", ref failures);
        Check(DirectLifeStageBucket("Child") == AgeBucket.Child,
            "life-stage Child maps to Child", ref failures);
        Check(DirectLifeStageBucket("Adult") == AgeBucket.Adult && DirectLifeStageBucket("None") == AgeBucket.Adult,
            "life-stage Adult and other states map to Adult", ref failures);
    }

    private static AgeBucket DirectLifeStageBucket(string stage) => stage == "Newborn" || stage == "Baby"
        ? AgeBucket.Baby : stage == "Child" ? AgeBucket.Child : AgeBucket.Adult;

    private static void PoolOrdering(ref int failures)
    {
        SqueakPoolRegistry registry = Scenarios.BuildRegistry("S3-builtin-plus-xeno");
        DomainPool? race = registry.PoolFor(Scenarios.RaceDomain);
        Check(race != null && race.Entries.Count == 1, "S3 race pool 1 entry", ref failures);
        DomainPool? xeno = registry.PoolFor(Scenarios.XenoDomain);
        Check(xeno != null && xeno.Entries.Count == 1, "S3 xeno pool 1 entry", ref failures);
        Check(registry.PoolFor(new AudioDomain(new RaceKey("US_Race_Unknown"), null)) == null, "unknown race no pool", ref failures);
        // 排序：构造倒序，池应序数排序
        SqueakPoolRegistry mixed = new(new[]
        {
            ScenariosEntry("zzz.test:US_Z", Scenarios.RaceDomain),
            ScenariosEntry("aaa.test:US_A", Scenarios.RaceDomain),
        }, Scenarios.BuildBuiltIn());
        DomainPool? pool = mixed.PoolFor(Scenarios.RaceDomain);
        Check(pool != null && pool.Entries[0].PackKey == "aaa.test:US_A" && pool.Entries[1].PackKey == "zzz.test:US_Z", "pool sorted by PackKey ordinal", ref failures);
    }

    private static void SelectOff(ref int failures)
    {
        foreach (string scenario in Scenarios.ScenarioNames)
        {
            SqueakPoolRegistry registry = Scenarios.BuildRegistry(scenario);
            foreach (AudioDomain domain in Scenarios.DomainsFor(scenario))
            {
                // Off = 内置表兜底：只对 15 个有内置映射的动作成立；Crying/Giggling 无内置条目 = 静默。
                for (int i = 0; i < ActionKeyMirror.BuiltInCount; i++)
                {
                    string action = CoreActions.All[i];
                    ChainResult result = registry.Select(Ctx(domain, action), SelectionMode.Off, SimGate.All, new LcgRandom(1));
                    string expectedKey = ActionKeyMirror.BuiltInFallbackKey(action);
                    if (!(result.Tier == ChainTier.BuiltInFallback && result.SoundKey == expectedKey && result.PoolStableKey == null))
                    {
                        Check(false, "Off " + scenario + " " + domain + " " + action, ref failures);
                        return;
                    }
                }
            }
        }
        Check(true, "Off mode ignores pools, built-in only, stable PoolStableKey null", ref failures);
    }

    private static void SelectFallbackChain(ref int failures)
    {
        // S2 Race 域：x 跳过（无 xeno）→ r 命中 RacePack。
        SqueakPoolRegistry s2 = Scenarios.BuildRegistry("S2-builtin-seed");
        ChainResult r = s2.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(r.Tier == ChainTier.RacePack && r.PoolStableKey == Scenarios.RacePackA && r.SoundKey != null && r.SoundKey.StartsWith(Scenarios.RacePackA + "_Call_"),
            "Fallback S2 race tier wins", ref failures);

        // S3 Xeno 域：x 命中 XenotypePack（1 entry 池）。
        SqueakPoolRegistry s3 = Scenarios.BuildRegistry("S3-builtin-plus-xeno");
        ChainResult x = s3.Select(Ctx(Scenarios.XenoDomain, CoreActions.Eat), SelectionMode.Fallback, SimGate.All, new LcgRandom(2));
        Check(x.Tier == ChainTier.XenotypePack && x.PoolStableKey == Scenarios.XenoPack, "Fallback S3 xeno tier wins", ref failures);

        // S4 orphan xeno 域：无池 → x None → r 命中 RacePack（orphan 保留 = 域选择存在但无包 → 回退）。
        SqueakPoolRegistry s4 = Scenarios.BuildRegistry("S4-orphan-xeno");
        ChainResult s4r = s4.Select(Ctx(Scenarios.XenoDomain, CoreActions.Sleep), SelectionMode.Fallback, SimGate.All, new LcgRandom(3));
        Check(s4r.Tier == ChainTier.RacePack, "Fallback S4 orphan xeno falls to race", ref failures);

        // S1 空池 → builtin。
        SqueakPoolRegistry s1 = Scenarios.BuildRegistry("S1-empty");
        ChainResult s1r = s1.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(4));
        Check(s1r.Tier == ChainTier.BuiltInFallback && s1r.SoundKey == "US_Fallback_Call", "Fallback S1 empty falls to built-in", ref failures);

        // 全无声：x 池 entry 全 Muted + race 池空 → builtin 仍可播（gate 对 builtin 也执行——SimGate.All 可播）。
        SqueakPoolRegistry muted = new(new[]
        {
            MutedEntry(Scenarios.XenoPack, Scenarios.XenoDomain),
        }, Scenarios.BuildBuiltIn());
        ChainResult m = muted.Select(Ctx(Scenarios.XenoDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(5));
        Check(m.Tier == ChainTier.BuiltInFallback && m.SoundKey == "US_Fallback_Call", "Fallback muted xeno falls to built-in", ref failures);
    }

    private static void SelectRemix(ref int failures)
    {
        // S3 Xeno 域：tiers=[x,r,builtin] 全非 None → 等权折叠（固定序 [xeno, race, builtin]）。
        SqueakPoolRegistry s3 = Scenarios.BuildRegistry("S3-builtin-plus-xeno");
        HashSet<ChainTier?> seen = new();
        for (int seed = 1; seed <= 60; seed++)
        {
            ChainResult result = s3.Select(Ctx(Scenarios.XenoDomain, CoreActions.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(seed));
            if (result.IsNone) { Check(false, "Remix S3 never none", ref failures); return; }
            seen.Add(result.Tier);
        }
        Check(seen.Contains(ChainTier.XenotypePack) && seen.Contains(ChainTier.RacePack) && seen.Contains(ChainTier.BuiltInFallback), "Remix S3 folds all three tiers", ref failures);

        // S1 空池 → tiers=[builtin] → builtin。
        SqueakPoolRegistry s1 = Scenarios.BuildRegistry("S1-empty");
        ChainResult s1r = s1.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(1));
        Check(s1r.Tier == ChainTier.BuiltInFallback && s1r.SoundKey == "US_Fallback_Call", "Remix S1 single tier", ref failures);
    }

    private static void DistributionBounds(ref int failures)
    {
        // entry 级：S2 Race 池 1 entry × 2 sounds → sound 级等权（400 次，界 0.3-0.7）。
        SqueakPoolRegistry s2 = Scenarios.BuildRegistry("S2-builtin-seed");
        int sound0 = 0, sound1 = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            ChainResult result = s2.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.SoundKey == null) { Check(false, "S2 always sound", ref failures); return; }
            if (result.SoundKey.EndsWith("_0")) sound0++;
            else sound1++;
        }
        double ratio = sound0 / 400.0;
        Check(ratio > 0.3 && ratio < 0.7, "S2 sound-level uniform (0.3-0.7): " + ratio.ToString("0.000"), ref failures);

        // entry 级：两 entry 池（各有 1 sound）→ 每 entry ~50%。
        SqueakPoolRegistry mixed = new(new[]
        {
            ScenariosEntry("aaa.test:US_A", Scenarios.RaceDomain, sounds: 1),
            ScenariosEntry("zzz.test:US_Z", Scenarios.RaceDomain, sounds: 1),
        }, Scenarios.BuildBuiltIn());
        int a = 0, z = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            ChainResult result = mixed.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.PoolStableKey == "aaa.test:US_A") a++;
            else z++;
        }
        double ar = a / 400.0;
        Check(ar > 0.3 && ar < 0.7, "two-entry pool uniform (0.3-0.7): " + ar.ToString("0.000"), ref failures);

        // Remix 三级分布有界（S3 Xeno 域，600 次，各级界 0.1-0.5）。
        SqueakPoolRegistry s3 = Scenarios.BuildRegistry("S3-builtin-plus-xeno");
        int tx = 0, tr = 0, tb = 0;
        for (int seed = 1; seed <= 600; seed++)
        {
            ChainResult result = s3.Select(Ctx(Scenarios.XenoDomain, CoreActions.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(seed));
            if (result.Tier == ChainTier.XenotypePack) tx++;
            else if (result.Tier == ChainTier.RacePack) tr++;
            else tb++;
        }
        double txr = tx / 600.0, trr = tr / 600.0, tbr = tb / 600.0;
        Check(txr > 0.1 && txr < 0.5 && trr > 0.1 && trr < 0.5 && tbr > 0.1 && tbr < 0.5,
            "Remix tier distribution bounded (" + txr.ToString("0.00") + "/" + trr.ToString("0.00") + "/" + tbr.ToString("0.00") + ")", ref failures);
    }

    private static void Determinism(ref int failures)
    {
        foreach (string scenario in Scenarios.ScenarioNames)
        {
            SqueakPoolRegistry registry = Scenarios.BuildRegistry(scenario);
            foreach (AudioDomain domain in Scenarios.DomainsFor(scenario))
            {
                foreach (SelectionMode mode in new[] { SelectionMode.Off, SelectionMode.Fallback, SelectionMode.Remix })
                {
                    ChainResult first = registry.Select(Ctx(domain, CoreActions.Call), mode, SimGate.All, new LcgRandom(42));
                    ChainResult second = registry.Select(Ctx(domain, CoreActions.Call), mode, SimGate.All, new LcgRandom(42));
                    if (!(first.SoundKey == second.SoundKey && first.Tier == second.Tier && first.PoolStableKey == second.PoolStableKey))
                    {
                        Check(false, "determinism " + scenario + " " + domain + " " + mode, ref failures);
                        return;
                    }
                }
            }
        }
        Check(true, "same seed same result across scenarios/modes/domains", ref failures);
    }

    private static void SoundLevelFilter(ref int failures)
    {
        // S3 Xeno 池 entry：2 sounds 其中 1 个 _Muted → Partial gate 下 sound 抽取永远命中 playable 子集（1 个）。
        SqueakPoolRegistry s3 = Scenarios.BuildRegistry("S3-builtin-plus-xeno");
        for (int seed = 1; seed <= 50; seed++)
        {
            ChainResult result = s3.Select(Ctx(Scenarios.XenoDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(seed));
            if (result.Tier != ChainTier.XenotypePack) { Check(false, "partial gate xeno tier", ref failures); return; }
            if (result.SoundKey == null || result.SoundKey.EndsWith("_Muted")) { Check(false, "muted sound never drawn: " + result.SoundKey, ref failures); return; }
        }
        Check(true, "sound-level filter excludes muted under Partial gate", ref failures);
    }

    private static void EntryLevelFilter(ref int failures)
    {
        // 全 Muted 的 entry 不参与 valid（entry 级过滤）→ 另一 entry 命中。
        SqueakPoolRegistry registry = new(new[]
        {
            MutedEntry("aaa.test:US_A", Scenarios.RaceDomain),
            ScenariosEntry("zzz.test:US_Z", Scenarios.RaceDomain, sounds: 1),
        }, Scenarios.BuildBuiltIn());
        for (int seed = 1; seed <= 50; seed++)
        {
            ChainResult result = registry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(seed));
            if (result.PoolStableKey != "zzz.test:US_Z") { Check(false, "muted entry skipped: " + result.PoolStableKey, ref failures); return; }
        }
        Check(true, "entry-level filter skips fully-muted entries", ref failures);
    }

    private static void DomainStatus(ref int failures)
    {
        Check(AudioDomainStatuses.Classify(new AudioDomain(Scenarios.RaceA, null), true, true, true) == AudioDomainStatus.Available, "status available", ref failures);
        Check(AudioDomainStatuses.Classify(new AudioDomain(Scenarios.RaceA, null), true, false, true) == AudioDomainStatus.Orphan, "status orphan (assembled, no selection)", ref failures);
        Check(AudioDomainStatuses.Classify(new AudioDomain(Scenarios.RaceA, new XenotypeKey("US_Xeno_A")), false, true, true) == AudioDomainStatus.TargetUnavailable, "status target unavailable", ref failures);
        Check(AudioDomainStatuses.Classify(new AudioDomain(Scenarios.RaceA, new XenotypeKey("US_Xeno_A")), false, true, false) == AudioDomainStatus.Dormant, "status dormant (biotech off)", ref failures);
    }

    private static void ModulationRules(ref int failures)
    {
        ModulationAxis mood = new(true, 0.8f, false, 1f, false, (1f, 1f));
        ModulationAxis age = new(false, 1f, true, 1.2f, true, (0.9f, 1.1f));
        ModulationAxis composed = Modulation.ComposeModulation(mood, age);
        Check(composed.HasPitch && composed.Pitch == 0.8f, "mood pitch explicit wins", ref failures);
        Check(composed.HasVolume && composed.Volume == 1.2f, "age volume inherited", ref failures);
        Check(composed.HasJitter && composed.Jitter == (0.9f, 1.1f), "age jitter inherited", ref failures);
        ModulationAxis identity = Modulation.ComposeModulation(ModulationAxis.Identity, ModulationAxis.Identity);
        Check(!identity.HasPitch && !identity.HasVolume && !identity.HasJitter, "identity when nothing present", ref failures);
    }

    private static void AgeDefault(ref int failures)
    {
        // AgeTag 全 null = 全年龄；Select 结果不受 ctx.Age 影响。
        SqueakPoolRegistry s2 = Scenarios.BuildRegistry("S2-builtin-seed");
        ChainResult adult = s2.Select(new SelectionContext(Scenarios.RaceDomain, CoreActions.Call, AgeBucket.Adult, true, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(7));
        ChainResult baby = s2.Select(new SelectionContext(Scenarios.RaceDomain, CoreActions.Call, AgeBucket.Baby, true, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(7));
        Check(!adult.IsNone && !baby.IsNone && adult.SoundKey == baby.SoundKey && adult.Tier == baby.Tier, "age-neutral (all-age default), both actually selected", ref failures);
    }

    private static void AgePriority(ref int failures)
    {
        VoicePackEntry exactAndAll = Entry("age.test:US_Age", Scenarios.RaceDomain,
            Variant("Call", "US_All", null),
            Variant("Call", "US_Baby", AgeBucket.Baby));
        SqueakPoolRegistry registry = new(new[] { exactAndAll }, BuiltInFallbackTable.Empty);
        ChainResult baby = registry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, AgeBucket.Baby), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        ChainResult child = registry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, AgeBucket.Child), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(baby.SoundKey == "US_Baby" && child.SoundKey == "US_All", "age exact variant wins; missing exact falls to all-age", ref failures);

        VoicePackEntry exactOnly = Entry("age.test:US_ExactOnly", Scenarios.RaceDomain, Variant("Call", "US_BabyOnly", AgeBucket.Baby));
        SqueakPoolRegistry noMatch = new(new[] { exactOnly }, BuiltInFallbackTable.Empty);
        ChainResult none = noMatch.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, AgeBucket.Child), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(none.IsNone, "age with neither exact nor all-age is not a candidate", ref failures);

        VoicePackEntry mutedExact = Entry("age.test:US_MutedExact", Scenarios.RaceDomain,
            Variant("Call", "US_AllPlayable", null),
            Variant("Call", "US_Baby_Muted", AgeBucket.Baby));
        SqueakPoolRegistry noCrossVariantFallback = new(new[] { mutedExact }, BuiltInFallbackTable.Empty);
        ChainResult muted = noCrossVariantFallback.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, AgeBucket.Baby), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(1));
        Check(muted.IsNone, "muted exact age does not fall through to all-age", ref failures);
    }

    /// <summary>M1：同 pack 多 all-age 变体按 ActionSoundSet.Weight 混抽；彩蛋开时普通+彩蛋都出现。</summary>
    private static void VariantWeightMixing(ref int failures)
    {
        VoicePackEntry samePack = Entry("variant.test:US_SamePack", Scenarios.RaceDomain,
            Variant("Call", "US_SamePack_Normal", null),
            Variant("Call", "US_SamePack_Egg", null, true));
        SqueakPoolRegistry registry = new(new[] { samePack }, BuiltInFallbackTable.Empty);

        HashSet<string> enabled = new();
        for (int seed = 1; seed <= 200; seed++)
        {
            ChainResult result = registry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, allowEggs: true), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.SoundKey == null) { Check(false, "same-pack variants enabled never none", ref failures); return; }
            enabled.Add(result.SoundKey);
        }
        Check(enabled.Contains("US_SamePack_Normal") && enabled.Contains("US_SamePack_Egg"),
            "same-pack normal+egg all-age variants both drawn when eggs enabled", ref failures);

        for (int seed = 1; seed <= 100; seed++)
        {
            ChainResult disabled = registry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (disabled.SoundKey != "US_SamePack_Normal")
            {
                Check(false, "same-pack egg disabled always selects non-egg variant", ref failures);
                return;
            }
        }
        Check(true, "same-pack egg disabled filters egg before variant draw", ref failures);

        VoicePackEntry weighted = Entry("variant.test:US_Weighted", Scenarios.RaceDomain,
            new TestVariant("Call", new ActionSoundSet(new[] { "US_Weighted_Light" }, null, 1f)),
            new TestVariant("Call", new ActionSoundSet(new[] { "US_Weighted_Heavy" }, null, 3f)));
        SqueakPoolRegistry weightedRegistry = new(new[] { weighted }, BuiltInFallbackTable.Empty);
        int heavy = 0;
        for (int seed = 1; seed <= 800; seed++)
        {
            ChainResult result = weightedRegistry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.SoundKey == "US_Weighted_Heavy") heavy++;
        }
        double ratio = heavy / 800.0;
        Check(ratio > 0.6 && ratio < 0.85, "variant ActionSoundSet.Weight drives weighted draw: " + ratio.ToString("0.000"), ref failures);
    }

    private static void PackFallbackTier(ref int failures)
    {
        VoicePackEntry raceFallback = Entry("fallback.test:US_Race", Scenarios.RaceDomain,
            fallback: new Dictionary<string, string> { [CoreActions.Call] = "US_PackFallback_Race" });
        SqueakPoolRegistry raceRegistry = new(new[] { raceFallback }, Scenarios.BuildBuiltIn());
        ChainResult race = raceRegistry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(race.Tier == ChainTier.PackFallback && race.SoundKey == "US_PackFallback_Race" && race.PoolStableKey == "fallback.test:US_Race", "pack fallback runs after empty race tier before built-in", ref failures);

        VoicePackEntry xenoFallback = Entry("fallback.test:US_Xeno", Scenarios.XenoDomain,
            fallback: new Dictionary<string, string> { [CoreActions.Call] = "US_PackFallback_Xeno" });
        VoicePackEntry otherRaceFallback = Entry("fallback.test:US_RaceOther", Scenarios.RaceDomain,
            fallback: new Dictionary<string, string> { [CoreActions.Call] = "US_PackFallback_RaceOther" });
        SqueakPoolRegistry xenoRegistry = new(new[] { xenoFallback, otherRaceFallback }, Scenarios.BuildBuiltIn());
        ChainResult xeno = xenoRegistry.Select(Ctx(Scenarios.XenoDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(xeno.Tier == ChainTier.PackFallback && xeno.SoundKey == "US_PackFallback_Xeno", "xeno context reads only its exact pool fallback", ref failures);

        SqueakPoolRegistry absent = new(Array.Empty<VoicePackEntry>(), Scenarios.BuildBuiltIn());
        ChainResult builtin = absent.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(builtin.Tier == ChainTier.BuiltInFallback, "absent pack fallback leaves tier empty", ref failures);

        HashSet<ChainTier?> tiers = new();
        for (int seed = 1; seed <= 80; seed++)
        {
            ChainResult result = xenoRegistry.Select(Ctx(Scenarios.XenoDomain, CoreActions.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(seed));
            tiers.Add(result.Tier);
        }
        Check(tiers.Contains(ChainTier.PackFallback) && tiers.Contains(ChainTier.BuiltInFallback), "remix includes pack fallback as a fourth tier", ref failures);
    }

    /// <summary>M3：PackFallback 只读精确 ctx.Domain 池；xeno context 不会读取同 race 的 race-pool fallback。</summary>
    private static void PackFallbackExactDomainOnly(ref int failures)
    {
        VoicePackEntry raceFallback = Entry("fallback.test:US_RaceOnly", Scenarios.RaceDomain,
            fallback: new Dictionary<string, string> { [CoreActions.Call] = "US_PackFallback_RaceOnly" });
        SqueakPoolRegistry registry = new(new[] { raceFallback }, Scenarios.BuildBuiltIn());

        ChainResult xeno = registry.Select(Ctx(Scenarios.XenoDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(xeno.Tier == ChainTier.BuiltInFallback && xeno.SoundKey == "US_Fallback_Call",
            "xeno context with no xeno pack fallback does NOT read race pack fallback", ref failures);

        ChainResult race = registry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(race.Tier == ChainTier.PackFallback && race.SoundKey == "US_PackFallback_RaceOnly" && race.PoolStableKey == "fallback.test:US_RaceOnly",
            "race context still reads its own exact pool pack fallback", ref failures);
    }

    private static void EggFiltering(ref int failures)
    {
        VoicePackEntry egg = Entry("egg.test:US_Egg", Scenarios.RaceDomain, Variant("Call", "US_EggOnly", null, true));
        SqueakPoolRegistry onlyEgg = new(new[] { egg }, BuiltInFallbackTable.Empty);
        ChainResult disabled = onlyEgg.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, AgeBucket.Adult, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        ChainResult enabled = onlyEgg.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, AgeBucket.Adult, true), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(disabled.IsNone && enabled.SoundKey == "US_EggOnly", "egg variant is excluded while disabled and admitted while enabled", ref failures);
        VoicePackEntry exactEggWithAllAge = Entry("egg.test:US_ExactEgg", Scenarios.RaceDomain,
            Variant("Call", "US_AllAgeNormal", null),
            Variant("Call", "US_BabyEgg", AgeBucket.Baby, true));
        SqueakPoolRegistry noEggAgeFallback = new(new[] { exactEggWithAllAge }, BuiltInFallbackTable.Empty);
        ChainResult exactDisabled = noEggAgeFallback.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, AgeBucket.Baby, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(exactDisabled.IsNone, "disabled exact egg does not fall through to all-age", ref failures);

        VoicePackEntry normal = Entry("egg.test:US_Normal", Scenarios.RaceDomain, Variant("Call", "US_Normal", null));
        SqueakPoolRegistry mixed = new(new[] { egg, normal }, BuiltInFallbackTable.Empty);
        int eggs = 0, normalCount = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            ChainResult result = mixed.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, AgeBucket.Adult, true), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.SoundKey == "US_EggOnly") eggs++;
            else if (result.SoundKey == "US_Normal") normalCount++;
        }
        double ratio = eggs / 400.0;
        Check(normalCount > 0 && ratio > 0.3 && ratio < 0.7, "enabled egg is an equal additive pool member", ref failures);
    }

    private static void PackWeight(ref int failures)
    {
        VoicePackEntry light = Entry("weight.test:US_Light", Scenarios.RaceDomain, 1f, Variant("Call", "US_Light", null));
        VoicePackEntry heavy = Entry("weight.test:US_Heavy", Scenarios.RaceDomain, 3f, Variant("Call", "US_Heavy", null));
        SqueakPoolRegistry registry = new(new[] { light, heavy }, BuiltInFallbackTable.Empty);
        int heavyCount = 0;
        for (int seed = 1; seed <= 800; seed++)
        {
            ChainResult result = registry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.SoundKey == "US_Heavy") heavyCount++;
        }
        double ratio = heavyCount / 800.0;
        Check(ratio > 0.65 && ratio < 0.85, "pack weight uses cumulative weighted draw: " + ratio.ToString("0.000"), ref failures);

        VoicePackEntry invalid = Entry("weight.test:US_Invalid", Scenarios.RaceDomain, 0f, Variant("Call", "US_Invalid", null));
        SqueakPoolRegistry rejectsInvalid = new(new[] { invalid }, BuiltInFallbackTable.Empty);
        ChainResult none = rejectsInvalid.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(none.IsNone, "nonpositive direct pack weight is rejected", ref failures);
    }

    private static void SelectInvalidGuards(ref int failures)
    {
        SqueakPoolRegistry registry = Scenarios.BuildRegistry("S2-builtin-seed");
        ChainResult nullKey = registry.Select(new SelectionContext(Scenarios.RaceDomain, null!, AgeBucket.Adult, false, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(nullKey.IsNone, "null ActionKey returns None", ref failures);
        ChainResult whitespaceKey = registry.Select(new SelectionContext(Scenarios.RaceDomain, "   ", AgeBucket.Adult, false, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(whitespaceKey.IsNone, "whitespace ActionKey returns None", ref failures);
        ChainResult unknownMode = registry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call), (SelectionMode)999, SimGate.All, new LcgRandom(1));
        Check(unknownMode.IsNone, "unknown SelectionMode returns None instead of falling into Remix", ref failures);
    }



    /// <summary>Independent Eat values replace the retired boolean/fail-open rule.</summary>
    private static void EatOccurrenceRules(ref int failures)
    {
        foreach (EatPhase phase in Enum.GetValues(typeof(EatPhase)))
        foreach (EatNutritionScope nutrition in Enum.GetValues(typeof(EatNutritionScope)))
        foreach (EatDrugScope drug in Enum.GetValues(typeof(EatDrugScope)))
            Check(new BehaviorValues { Phase = phase, NutritionScope = nutrition, DrugScope = drug }.Valid,
                "Eat three axes are independently legal: " + phase + "/" + nutrition + "/" + drug, ref failures);
        Check(!new BehaviorValues { Phase = (EatPhase)99 }.Valid, "invalid explicit phase rejected", ref failures);
        Check(!new BehaviorValues { Phase = EatPhase.WholeProcess }.ApplicableTo("NonPawn"), "Pawn Eat fields never apply to NonPawn", ref failures);
    }


    /// <summary>per-race 池隔离：RaceA/RaceB 两 race 池 + 两族同形内置表；跨模式/种子断言零交叉。</summary>
    private static void PerRacePoolIsolation(ref int failures)
    {
        SqueakPoolRegistry registry = Scenarios.BuildRegistry("S7-two-races");
        DomainPool? raceAPool = registry.PoolFor(Scenarios.RaceDomain);
        DomainPool? raceBPool = registry.PoolFor(Scenarios.RaceBDomain);
        Check(raceAPool != null && raceAPool.Entries.Count == 1 && raceAPool.Entries[0].PackKey == Scenarios.RacePackA,
            "per-race RaceA pool holds only RaceA pack", ref failures);
        Check(raceBPool != null && raceBPool.Entries.Count == 1 && raceBPool.Entries[0].PackKey == Scenarios.RacePackB,
            "per-race RaceB pool holds only RaceB pack", ref failures);

        // RaceA 查询绝不返回 RaceB 包键（三模式 × 多种子）。
        foreach (SelectionMode mode in new[] { SelectionMode.Off, SelectionMode.Fallback, SelectionMode.Remix })
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                ChainResult result = registry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, allowEggs: false), mode, SimGate.All, new LcgRandom(seed));
                if (result.PoolStableKey != null && result.PoolStableKey.StartsWith("us.test:US_Pack_RaceB", StringComparison.Ordinal))
                {
                    Check(false, "per-race RaceA query leaked RaceB pack: " + result.PoolStableKey, ref failures);
                    return;
                }
            }
        }

        // RaceB 查询只返回 RaceB 包键；Off = 同形内置表（两族对称，US_Fallback_Call）。
        for (int seed = 1; seed <= 40; seed++)
        {
            ChainResult off = registry.Select(Ctx(Scenarios.RaceBDomain, CoreActions.Call, allowEggs: false), SelectionMode.Off, SimGate.All, new LcgRandom(seed));
            if (off.Tier != ChainTier.BuiltInFallback || off.SoundKey != "US_Fallback_Call")
            {
                Check(false, "per-race RaceB Off must resolve its own built-in profile", ref failures);
                return;
            }
            ChainResult fallback = registry.Select(Ctx(Scenarios.RaceBDomain, CoreActions.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (fallback.PoolStableKey != Scenarios.RacePackB || (fallback.SoundKey != null && !fallback.SoundKey.StartsWith(Scenarios.RacePackB, StringComparison.Ordinal)))
            {
                Check(false, "per-race RaceB Fallback must resolve only RaceB pack", ref failures);
                return;
            }
        }

        // 无内置 profile 的未知 race：Off/Fallback 均静默，链不跨 race。
        SqueakPoolRegistry raceAOnly = new(new[]
        {
            ScenariosEntry(Scenarios.RacePackA, Scenarios.RaceDomain, sounds: 1),
        }, new BuiltInFallbackTable(new[] { Scenarios.BuildBuiltIn().For(Scenarios.RaceA)! }));
        ChainResult unknownOff = raceAOnly.Select(Ctx(Scenarios.RaceBDomain, CoreActions.Call, allowEggs: false), SelectionMode.Off, SimGate.All, new LcgRandom(1));
        ChainResult noLeak = raceAOnly.Select(Ctx(Scenarios.RaceBDomain, CoreActions.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(unknownOff.IsNone && noLeak.IsNone, "per-race RaceB query never falls through to RaceA pool or RaceA built-in", ref failures);

        // xeno 子域隔离：RaceA+Xeno 池只在 RaceA+Xeno 域可见；RaceB+Xeno 查询不得命中。
        AudioDomain raceAXeno = new(Scenarios.RaceA, Scenarios.XenoA);
        AudioDomain raceBXeno = new(Scenarios.RaceB, Scenarios.XenoA);
        SqueakPoolRegistry xenoIsolated = new(new[]
        {
            ScenariosEntry("us.test:US_RaceA_Xeno", raceAXeno, sounds: 1),
            ScenariosEntry("us.test:US_RaceB_Xeno", raceBXeno, sounds: 1),
        }, Scenarios.BuildBuiltIn());
        ChainResult bx = xenoIsolated.Select(Ctx(raceBXeno, CoreActions.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(2));
        Check(bx.PoolStableKey == "us.test:US_RaceB_Xeno", "per-race RaceB+xeno resolves its exact pool", ref failures);
        ChainResult ax = xenoIsolated.Select(Ctx(raceAXeno, CoreActions.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(2));
        Check(ax.PoolStableKey == "us.test:US_RaceA_Xeno", "per-race RaceA+xeno resolves its exact pool", ref failures);
    }

    /// <summary>平等路由显式断言：同输入仅换 race（数据对称注入）→ tier 链一致；结果各落在自己的 pack。</summary>
    private static void EqualRouting(ref int failures)
    {
        SqueakPoolRegistry registry = Scenarios.BuildRegistry("S7-two-races");
        foreach (SelectionMode mode in new[] { SelectionMode.Off, SelectionMode.Fallback, SelectionMode.Remix })
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                ChainResult a = registry.Select(Ctx(Scenarios.RaceDomain, CoreActions.Call, allowEggs: false), mode, SimGate.All, new LcgRandom(seed));
                ChainResult b = registry.Select(Ctx(Scenarios.RaceBDomain, CoreActions.Call, allowEggs: false), mode, SimGate.All, new LcgRandom(seed));
                if (a.Tier != b.Tier)
                {
                    Check(false, "equal routing tier mismatch " + mode + " seed " + seed + ": " + a.Tier + " vs " + b.Tier, ref failures);
                    return;
                }
                if (a.PoolStableKey != null && !a.PoolStableKey.StartsWith(Scenarios.RacePackA, StringComparison.Ordinal))
                {
                    Check(false, "equal routing RaceA resolved foreign pack: " + a.PoolStableKey, ref failures);
                    return;
                }
                if (b.PoolStableKey != null && !b.PoolStableKey.StartsWith(Scenarios.RacePackB, StringComparison.Ordinal))
                {
                    Check(false, "equal routing RaceB resolved foreign pack: " + b.PoolStableKey, ref failures);
                    return;
                }
            }
        }
        Check(true, "equal routing: same input with only race swapped yields same tier chain and own-pack resolution", ref failures);
    }



    // ---- helpers ----

    private static SelectionContext Ctx(AudioDomain domain, string action, AgeBucket age = AgeBucket.Adult, bool allowEggs = false)
        => new(domain, ActionKeyMirror.FrozenId(action), age, false, allowEggs);

    private static IEnumerable<int> Range(int from, int count) { for (int i = from; i < from + count; i++) yield return i; }

    private readonly struct TestVariant
    {
        public readonly string ActionKey;
        public readonly ActionSoundSet Set;

        public TestVariant(string actionKey, ActionSoundSet set)
        {
            ActionKey = ActionKeyMirror.FrozenId(actionKey);
            Set = set;
        }
    }

    private static TestVariant Variant(string actionKey, string soundKey, AgeBucket? ageTag, bool isEgg = false)
        => new(ActionKeyMirror.FrozenId(actionKey), new ActionSoundSet(new[] { soundKey }, ageTag, 1f, isEgg));

    private static VoicePackEntry Entry(string packKey, AudioDomain domain, params TestVariant[] variants)
        => Entry(packKey, domain, 1f, null, variants);

    private static VoicePackEntry Entry(string packKey, AudioDomain domain, float weight, params TestVariant[] variants)
        => Entry(packKey, domain, weight, null, variants);

    private static VoicePackEntry Entry(string packKey, AudioDomain domain, IReadOnlyDictionary<string, string>? fallback = null, params TestVariant[] variants)
        => Entry(packKey, domain, 1f, fallback, variants);

    private static VoicePackEntry Entry(string packKey, AudioDomain domain, float weight, IReadOnlyDictionary<string, string>? fallback, params TestVariant[] variants)
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        foreach (TestVariant variant in variants)
        {
            if (!actions.TryGetValue(variant.ActionKey, out IReadOnlyList<ActionSoundSet>? existing))
            {
                actions.Add(variant.ActionKey, new[] { variant.Set });
                continue;
            }
            List<ActionSoundSet> merged = new(existing) { variant.Set };
            actions[variant.ActionKey] = merged;
        }
        return new VoicePackEntry(packKey, domain, weight, actions, fallback?.ToDictionary(x => ActionKeyMirror.FrozenId(x.Key), x => x.Value, StringComparer.Ordinal));
    }

    public static VoicePackEntry ScenariosEntry(string packKey, AudioDomain domain, int sounds = 2)
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        for (int i = 0; i < ActionKeyMirror.Count; i++)
        {
            string action = CoreActions.All[i];
            string actionName = ActionKeyMirror.For(action);
            List<string> keys = new(sounds);
            for (int s = 0; s < sounds; s++) keys.Add(packKey + "_" + actionName + "_" + s);
            actions[action!] = new[] { new ActionSoundSet(keys, null, 1f) };
        }
        return new VoicePackEntry(packKey, domain, 1f, actions);
    }

    private static VoicePackEntry MutedEntry(string packKey, AudioDomain domain)
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        for (int i = 0; i < ActionKeyMirror.Count; i++)
        {
            string action = CoreActions.All[i];
            string actionName = ActionKeyMirror.For(action);
            actions[action!] = new[] { new ActionSoundSet(new[] { packKey + "_" + actionName + "_Muted" }, null, 1f) };
        }
        return new VoicePackEntry(packKey, domain, 1f, actions);
    }
}
