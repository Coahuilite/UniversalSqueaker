using System;
using System.Collections.Generic;
using System.IO;
using SqueakyRatkin.Kernel;

namespace SqueakyRatkin.KernelCharacterization;

/// <summary>验证门单测（决策 §5 验证门 + §4 语义规范）。每断言独立输出；失败计数由 Program 汇总。</summary>
public static class UnitTests
{
    public static void RunAll(ref int failures)
    {
        DomainKeys(ref failures);
        ActionKeyMapping(ref failures);
        ActionFiveWaySync(ref failures);
        BuiltInTable(ref failures);
        FailurePathFallback(ref failures);
        PoolOrdering(ref failures);
        DirectLifeStageContract(ref failures);
        SelectOff(ref failures);
        SelectFallbackChain(ref failures);
        SelectRemix(ref failures);
        DistributionBounds(ref failures);
        Determinism(ref failures);
        FallbackCopyLifecycle(ref failures);
        SoundLevelFilter(ref failures);
        EntryLevelFilter(ref failures);
        DomainStatus(ref failures);
        ModulationRules(ref failures);
        AgeDefault(ref failures);
        AgePriority(ref failures);
        PackFallbackTier(ref failures);
        EggFiltering(ref failures);
        PackWeight(ref failures);
        TimingModelRules(ref failures);
        TriggerInvocationRules(ref failures);
        PerRacePoolIsolation(ref failures);
    }

    private static void Check(bool condition, string name, ref int failures)
    {
        if (condition) Console.WriteLine("  ok: " + name);
        else { Console.Error.WriteLine("  FAIL: " + name); failures++; }
    }

    private static void DomainKeys(ref int failures)
    {
        RaceKey a = new("Ratkin");
        RaceKey b = new("Ratkin");
        RaceKey c = new("ratkin"); // Ordinal 区分大小写
        Check(a == b && a.GetHashCode() == b.GetHashCode(), "RaceKey value equality", ref failures);
        Check(a != c, "RaceKey case-sensitive", ref failures);
        Check(new AudioDomain(a, null) == new AudioDomain(b, null), "AudioDomain race-only equality", ref failures);
        Check(new AudioDomain(a, new XenotypeKey("X")) == new AudioDomain(a, new XenotypeKey("X")), "AudioDomain xenotype equality", ref failures);
        Check(new AudioDomain(a, null) != new AudioDomain(a, new XenotypeKey("X")), "AudioDomain null-vs-xeno distinct", ref failures);
        Check(new AudioDomain(a, null).ToString() == "Ratkin", "AudioDomain ToString race", ref failures);
        Check(new AudioDomain(a, new XenotypeKey("XenoA")).ToString() == "Ratkin+XenoA", "AudioDomain ToString xeno", ref failures);
    }

    private static void ActionKeyMapping(ref int failures)
    {
        string[] enumNames = Enum.GetNames(typeof(SqueakyRatkin.SqueakAction));
        bool allOk = enumNames.Length == BuiltInActionKeys.All.Count;
        for (int i = 0; i < enumNames.Length; i++)
        {
            SqueakyRatkin.SqueakAction action = (SqueakyRatkin.SqueakAction)i;
            string? key = ActionKey.For(action);
            if (key == null || key != enumNames[i] || key != BuiltInActionKeys.All[i]
                || !ActionKey.TryParseBuiltIn(key, out SqueakyRatkin.SqueakAction parsed) || parsed != action)
                allOk = false;
        }
        Check(BuiltInActionKeys.All.Count == 17 && BuiltInActionKeys.All[15] == "Crying" && BuiltInActionKeys.All[16] == "Giggling",
            "BuiltInActionKeys holds 17 ordered keys", ref failures);
        Check(allOk, "ActionKey 17-action prefix is bidirectional", ref failures);
        Check(enumNames.Length == 17 && enumNames[15] == "Crying" && enumNames[16] == "Giggling",
            "SqueakAction append-only ordinals 15/16 = Crying/Giggling", ref failures);
        Check(ActionKey.For((SqueakyRatkin.SqueakAction)99) == null, "ActionKey.For unknown null", ref failures);
        Check(BuiltInActionKeys.TryGetIndex("Crying", out int cryingIndex) && cryingIndex == 15
            && ActionKey.TryParseBuiltIn("Crying", out SqueakyRatkin.SqueakAction crying) && crying == SqueakyRatkin.SqueakAction.Crying,
            "Crying key is enum-mappable after wave 3c append", ref failures);
        Check(!ActionKey.TryParseBuiltIn("other.mod:SR_X", out _), "TryParseBuiltIn external key false", ref failures);
        Check(!ActionKey.TryParseBuiltIn("call", out _), "TryParseBuiltIn wrong case false", ref failures);
    }

    /// <summary>0.3.1 波 3c/4a 五处同步离线断言（决策 §2.3：枚举/XML/SoundDef/本地化/统计 机械检查）：
    /// ① 枚举名序 == BuiltInActionKeys 17 键序；② 内置表 15 项且不列 Crying/Giggling（SoundDef 面默认静默）；
    /// ③ SoundDef XML 无 SR_Crying/SR_Giggling defName（缺席是有意同步）；④ 双语本地化含 SR.Action.Crying/Giggling；
    /// ⑤ 语料范围守卫：镜像 17 == 枚举 17（0.3.1 波 4a 语料 17 动作矩阵同变更重建）。</summary>
    private static void ActionFiveWaySync(ref int failures)
    {
        string root = Program.FindRepositoryRoot();
        string[] enumNames = Enum.GetNames(typeof(SqueakyRatkin.SqueakAction));
        bool namesMatch = enumNames.Length == BuiltInActionKeys.All.Count;
        for (int i = 0; i < enumNames.Length && namesMatch; i++) namesMatch = enumNames[i] == BuiltInActionKeys.All[i];
        Check(namesMatch, "five-way sync: enum names/ordinals == BuiltInActionKeys (17)", ref failures);

        FallbackProfile? profile = Scenarios.BuildBuiltIn().For(Scenarios.Ratkin);
        bool builtInExcludes = profile != null && profile.SoundKeys.Count == ActionAudioKeyMirror.BuiltInCount
            && !profile.SoundKeys.ContainsKey("Crying") && !profile.SoundKeys.ContainsKey("Giggling");
        Check(builtInExcludes, "five-way sync: built-in table 15 mappings without Crying/Giggling (default silence)", ref failures);

        bool soundDefsAbsent = true;
        string soundDefsDir = Path.Combine(root, "sr_reference", "1.6", "Defs", "SoundDefs");
        foreach (string file in Directory.GetFiles(soundDefsDir, "*.xml"))
        {
            string text = File.ReadAllText(file);
            if (text.IndexOf("<defName>SR_Crying</defName>", StringComparison.Ordinal) >= 0
                || text.IndexOf("<defName>SR_Giggling</defName>", StringComparison.Ordinal) >= 0)
                soundDefsAbsent = false;
        }
        Check(soundDefsAbsent, "five-way sync: no SR_Crying/SR_Giggling SoundDef XML (absence deliberate)", ref failures);

        bool localizationPresent = true;
        foreach (string lang in new[] { "English", "ChineseSimplified" })
        {
            string file = Path.Combine(root, "sr_reference", "1.6", "Languages", lang, "Keyed", "SqueakyRatkin.xml");
            if (!File.Exists(file)) { localizationPresent = false; continue; }
            string text = File.ReadAllText(file);
            if (text.IndexOf("<SR.Action.Crying>", StringComparison.Ordinal) < 0
                || text.IndexOf("<SR.Action.Giggling>", StringComparison.Ordinal) < 0)
                localizationPresent = false;
        }
        Check(localizationPresent, "five-way sync: SR.Action.Crying/Giggling present in both languages", ref failures);

        Check(ActionAudioKeyMirror.Count == 17 && ActionAudioKeyMirror.BuiltInCount == 15,
            "five-way sync: corpus mirror 17-action with 15 built-in mappings (wave 4a corpus rebuild)", ref failures);
    }

    private static void BuiltInTable(ref int failures)
    {
        BuiltInFallbackTable table = Scenarios.BuildBuiltIn();
        FallbackProfile? profile = table.For(Scenarios.Ratkin);
        Check(profile != null && profile.SoundKeys.Count == ActionAudioKeyMirror.BuiltInCount && !profile.SoundKeys.ContainsKey("Crying"),
            "built-in Ratkin profile has current 15 mappings", ref failures);
        Check(table.For(new RaceKey("Kiiro")) == null, "unknown race returns null", ref failures);
        Check(table.TryGetSoundKey(Scenarios.Ratkin, "Call", out string? k) && k == "SR_Call", "TryGetSoundKey Call=SR_Call", ref failures);
        Check(!table.TryGetSoundKey(Scenarios.Ratkin, "other.mod:SR_X", out _) && !table.TryGetSoundKey(Scenarios.Ratkin, "Crying", out _),
            "built-in lookup rejects external and reserved-unmapped keys", ref failures);
        BuiltInFallbackTable reservedTable = new(new[]
        {
            new FallbackProfile(Scenarios.Ratkin, 1, new Dictionary<string, string> { ["Crying"] = "SR_Reserved_Crying" }),
        });
        Check(reservedTable.TryGetSoundKey(Scenarios.Ratkin, "Crying", out string? reserved) && reserved == "SR_Reserved_Crying",
            "string fallback lookup accepts reserved built-in keys", ref failures);
        bool rejected = false;
        try
        {
            _ = new FallbackProfile(Scenarios.Ratkin, 1, new Dictionary<string, string> { ["other.mod:SR_X"] = "SR_X" });
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        BuiltInFallbackTable formal = BuiltInFallbackCatalog.Create("Ratkin");
        FallbackProfile? formalProfile = formal.For(Scenarios.Ratkin);
        Check(formalProfile != null && formalProfile.Version == BuiltInFallbackCatalog.RatkinProfileVersion
            && formalProfile.SoundKeys.Count == 15 && formalProfile.SoundKeys["MentalBreak"] == "SR_MentalBreak"
            && !formalProfile.SoundKeys.ContainsKey("Crying") && !formalProfile.SoundKeys.ContainsKey("Giggling"),
            "formal built-in catalog has exactly the shipped 15 mappings", ref failures);
        Check(rejected, "FallbackProfile rejects non-built-in action keys", ref failures);
    }
    private static void FallbackCopyLifecycle(ref int failures)
    {
        FallbackProfile source = new(Scenarios.Ratkin, 3, new Dictionary<string, string>
        {
            ["Call"] = "SR_Call_Source",
            ["Eat"] = "SR_Eat_Source",
        });
        FallbackDelta delta = new(new Dictionary<string, string> { ["Call"] = "Core_Call_Override" });

        Check(FallbackProfileOperations.DecideCopy(source, delta, 3, true) == CopyDisposition.RebuildFromSource,
            "fallback corrupt copy rebuilds from source", ref failures);
        Check(FallbackProfileOperations.DecideCopy(source, delta, 2, false) == CopyDisposition.RebuildFromSource,
            "fallback older copy rebuilds from source", ref failures);
        Check(FallbackProfileOperations.DecideCopy(source, delta, 3, false) == CopyDisposition.MergeDelta,
            "fallback current copy with delta merges", ref failures);
        Check(FallbackProfileOperations.DecideCopy(source, null, 3, false) == CopyDisposition.KeepCopy,
            "fallback current copy without delta keeps copy", ref failures);
        Check(FallbackProfileOperations.DecideCopy(source, new FallbackDelta(new Dictionary<string, string>()), 3, false) == CopyDisposition.MergeDelta,
            "fallback current copy with explicit empty delta merges", ref failures);

        FallbackProfile merged = FallbackProfileOperations.Merge(source, delta);
        Check(merged.Race == source.Race && merged.Version == source.Version
            && merged.SoundKeys["Call"] == "Core_Call_Override" && merged.SoundKeys["Eat"] == "SR_Eat_Source",
            "fallback merge preserves source identity and untouched keys", ref failures);

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
    }


    /// <summary>BuildFallback 形状（0.3.0 错误路径修复回归）：无池条目 + 种子内置表 + Off 仍放 SR_* 兜底；
    /// 空内置表（GlobalOnly 形状）才静音。锁 v0.2.4 重建失败快照语义。</summary>
    private static void FailurePathFallback(ref int failures)
    {
        SqueakPoolRegistry failureRegistry = new(Array.Empty<VoicePackEntry>(), Scenarios.BuildBuiltIn(), DomainFilter.Everything);
        ChainResult hit = failureRegistry.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Off, SimGate.All, new LcgRandom(1));
        Check(hit.Tier == ChainTier.BuiltInFallback && hit.SoundKey == "SR_Call" && hit.PoolStableKey == null, "failure-path registry resolves built-in SR_Call", ref failures);
        SqueakPoolRegistry emptyTable = new(Array.Empty<VoicePackEntry>(), BuiltInFallbackTable.Empty, DomainFilter.Everything);
        ChainResult none = emptyTable.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Off, SimGate.All, new LcgRandom(1));
        Check(none.IsNone, "empty built-in table resolves to silence", ref failures);
    }

    private static void DirectLifeStageContract(ref int failures)
    {
        // The adapter maps RimWorld's DevelopmentalStage directly; this pure mirror locks the
        // Contract-2 decision without linking Verse into the Kernel characterization project.
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
        Check(registry.PoolFor(new AudioDomain(new RaceKey("Kiiro"), null)) == null, "unknown race no pool", ref failures);
        // 排序：构造倒序，池应序数排序
        SqueakPoolRegistry mixed = new(new[]
        {
            ScenariosEntry("zzz.mod:SR_Z", Scenarios.RaceDomain),
            ScenariosEntry("aaa.mod:SR_A", Scenarios.RaceDomain),
        }, Scenarios.BuildBuiltIn(), DomainFilter.Everything);
        DomainPool? pool = mixed.PoolFor(Scenarios.RaceDomain);
        Check(pool != null && pool.Entries[0].PackKey == "aaa.mod:SR_A" && pool.Entries[1].PackKey == "zzz.mod:SR_Z", "pool sorted by PackKey ordinal", ref failures);
    }

    private static void SelectOff(ref int failures)
    {
        foreach (string scenario in Scenarios.ScenarioNames)
        {
            SqueakPoolRegistry registry = Scenarios.BuildRegistry(scenario);
            foreach (AudioDomain domain in Scenarios.DomainsFor(scenario))
            {
                // Off = 内置表兜底：只对 15 个有内置 SoundDef 映射的动作成立；Crying/Giggling 无内置条目 = 静默。
                foreach (int i in Range(0, ActionAudioKeyMirror.BuiltInCount))
                {
                    SqueakyRatkin.SqueakAction action = (SqueakyRatkin.SqueakAction)i;
                    ChainResult result = registry.Select(Ctx(domain, action), SelectionMode.Off, SimGate.All, new LcgRandom(1));
                    string expectedKey = ActionAudioKeyMirror.For(action);
                    if (!(result.Tier == ChainTier.BuiltInFallback && result.SoundKey == expectedKey && result.PoolStableKey == null))
                    {
                        Check(false, "Off " + scenario + " " + domain + " " + action, ref failures);
                        return;
                    }
                }
                ChainResult crying = registry.Select(Ctx(domain, SqueakyRatkin.SqueakAction.Crying), SelectionMode.Off, SimGate.All, new LcgRandom(1));
                ChainResult giggling = registry.Select(Ctx(domain, SqueakyRatkin.SqueakAction.Giggling), SelectionMode.Off, SimGate.All, new LcgRandom(1));
                if (!crying.IsNone || !giggling.IsNone)
                {
                    Check(false, "Off unmapped Crying/Giggling must be silent", ref failures);
                    return;
                }
            }
        }
        Check(true, "Off mode ignores pools, built-in only, stable PoolStableKey null", ref failures);
    }

    private static void SelectFallbackChain(ref int failures)
    {
        // S2 Race 域：x 跳过（无 xeno）→ r 命中 RacePack。
        SqueakPoolRegistry s2 = Scenarios.BuildRegistry("S2-builtin-seed");
        ChainResult r = s2.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(r.Tier == ChainTier.RacePack && r.PoolStableKey == "coahuilite.squeakyratkin:SR_OfficialExample_Race" && r.SoundKey != null && r.SoundKey.StartsWith("coahuilite.squeakyratkin:SR_OfficialExample_Race_SR_Call_"), "Fallback S2 race tier wins", ref failures);

        // S3 Xeno 域：x 命中 XenotypePack（1 entry 池）。
        SqueakPoolRegistry s3 = Scenarios.BuildRegistry("S3-builtin-plus-xeno");
        ChainResult x = s3.Select(Ctx(Scenarios.XenoDomain, SqueakyRatkin.SqueakAction.Eat), SelectionMode.Fallback, SimGate.All, new LcgRandom(2));
        Check(x.Tier == ChainTier.XenotypePack && x.PoolStableKey == "other.mod:SR_XenoAVoice", "Fallback S3 xeno tier wins", ref failures);

        // S4 orphan xeno 域：无池 → x None → r 命中 RacePack（orphan 保留 = 域选择存在但无包 → 回退）。
        SqueakPoolRegistry s4 = Scenarios.BuildRegistry("S4-orphan-xeno");
        ChainResult s4r = s4.Select(Ctx(Scenarios.XenoDomain, SqueakyRatkin.SqueakAction.Sleep), SelectionMode.Fallback, SimGate.All, new LcgRandom(3));
        Check(s4r.Tier == ChainTier.RacePack, "Fallback S4 orphan xeno falls to race", ref failures);

        // S1 空池 → builtin。
        SqueakPoolRegistry s1 = Scenarios.BuildRegistry("S1-empty");
        ChainResult s1r = s1.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(4));
        Check(s1r.Tier == ChainTier.BuiltInFallback && s1r.SoundKey == "SR_Call", "Fallback S1 empty falls to built-in", ref failures);

        // 全无声：x 池 entry 全 Muted + race 池空 → builtin 仍可播（内置不受 gate 影响？gate 对 builtin 也执行——SimGate.All 可播；Partial 下 SR_* 无 Muted 后缀 → 可播）。
        SqueakPoolRegistry muted = new(new[]
        {
            MutedEntry("other.mod:SR_XenoAVoice", Scenarios.XenoDomain),
        }, Scenarios.BuildBuiltIn(), DomainFilter.Everything);
        ChainResult m = muted.Select(Ctx(Scenarios.XenoDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(5));
        Check(m.Tier == ChainTier.BuiltInFallback && m.SoundKey == "SR_Call", "Fallback muted xeno falls to built-in", ref failures);
    }

    private static void SelectRemix(ref int failures)
    {
        // S3 Xeno 域：tiers=[x,r,vanilla] 全非 None → 等权折叠（固定序 [xeno, race, builtin]）。
        SqueakPoolRegistry s3 = Scenarios.BuildRegistry("S3-builtin-plus-xeno");
        HashSet<ChainTier?> seen = new();
        for (int seed = 1; seed <= 60; seed++)
        {
            ChainResult result = s3.Select(Ctx(Scenarios.XenoDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(seed));
            if (result.IsNone) { Check(false, "Remix S3 never none", ref failures); return; }
            seen.Add(result.Tier);
        }
        Check(seen.Contains(ChainTier.XenotypePack) && seen.Contains(ChainTier.RacePack) && seen.Contains(ChainTier.BuiltInFallback), "Remix S3 folds all three tiers", ref failures);

        // S1 空池 → tiers=[vanilla] → vanilla。
        SqueakPoolRegistry s1 = Scenarios.BuildRegistry("S1-empty");
        ChainResult s1r = s1.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(1));
        Check(s1r.Tier == ChainTier.BuiltInFallback && s1r.SoundKey == "SR_Call", "Remix S1 single tier", ref failures);
    }

    private static void DistributionBounds(ref int failures)
    {
        // entry 级：S2 Race 池 1 entry × 2 sounds → sound 级等权（400 次，界 0.3-0.7）。
        SqueakPoolRegistry s2 = Scenarios.BuildRegistry("S2-builtin-seed");
        int sound0 = 0, sound1 = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            ChainResult result = s2.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.SoundKey == null) { Check(false, "S2 always sound", ref failures); return; }
            if (result.SoundKey.EndsWith("_0")) sound0++;
            else sound1++;
        }
        double ratio = sound0 / 400.0;
        Check(ratio > 0.3 && ratio < 0.7, "S2 sound-level uniform (0.3-0.7): " + ratio.ToString("0.000"), ref failures);

        // entry 级：两 entry 池（各有 1 sound）→ 每 entry ~50%。
        SqueakPoolRegistry mixed = new(new[]
        {
            ScenariosEntry("aaa.mod:SR_A", Scenarios.RaceDomain, sounds: 1),
            ScenariosEntry("zzz.mod:SR_Z", Scenarios.RaceDomain, sounds: 1),
        }, Scenarios.BuildBuiltIn(), DomainFilter.Everything);
        int a = 0, z = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            ChainResult result = mixed.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.PoolStableKey == "aaa.mod:SR_A") a++;
            else z++;
        }
        double ar = a / 400.0;
        Check(ar > 0.3 && ar < 0.7, "two-entry pool uniform (0.3-0.7): " + ar.ToString("0.000"), ref failures);

        // Remix 三级分布有界（S3 Xeno 域，600 次，各级界 0.1-0.5）。
        SqueakPoolRegistry s3 = Scenarios.BuildRegistry("S3-builtin-plus-xeno");
        int tx = 0, tr = 0, tb = 0;
        for (int seed = 1; seed <= 600; seed++)
        {
            ChainResult result = s3.Select(Ctx(Scenarios.XenoDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(seed));
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
                    ChainResult first = registry.Select(Ctx(domain, SqueakyRatkin.SqueakAction.Call), mode, SimGate.All, new LcgRandom(42));
                    ChainResult second = registry.Select(Ctx(domain, SqueakyRatkin.SqueakAction.Call), mode, SimGate.All, new LcgRandom(42));
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
            ChainResult result = s3.Select(Ctx(Scenarios.XenoDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(seed));
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
            MutedEntry("aaa.mod:SR_A", Scenarios.RaceDomain),
            ScenariosEntry("zzz.mod:SR_Z", Scenarios.RaceDomain, sounds: 1),
        }, Scenarios.BuildBuiltIn(), DomainFilter.Everything);
        for (int seed = 1; seed <= 50; seed++)
        {
            ChainResult result = registry.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(seed));
            if (result.PoolStableKey != "zzz.mod:SR_Z") { Check(false, "muted entry skipped: " + result.PoolStableKey, ref failures); return; }
        }
        Check(true, "entry-level filter skips fully-muted entries", ref failures);
    }

    private static void DomainStatus(ref int failures)
    {
        Check(AudioDomainStatuses.Classify(new AudioDomain(Scenarios.Ratkin, null), true, true, true) == AudioDomainStatus.Available, "status available", ref failures);
        Check(AudioDomainStatuses.Classify(new AudioDomain(Scenarios.Ratkin, null), true, false, true) == AudioDomainStatus.Orphan, "status orphan (assembled, no selection)", ref failures);
        Check(AudioDomainStatuses.Classify(new AudioDomain(Scenarios.Ratkin, new XenotypeKey("X")), false, true, true) == AudioDomainStatus.TargetUnavailable, "status target unavailable", ref failures);
        Check(AudioDomainStatuses.Classify(new AudioDomain(Scenarios.Ratkin, new XenotypeKey("X")), false, true, false) == AudioDomainStatus.Dormant, "status dormant (biotech off)", ref failures);
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
        // 0.3.0：AgeTag 全 null = 全年龄；Select 结果不受 ctx.Age 影响。
        SqueakPoolRegistry s2 = Scenarios.BuildRegistry("S2-builtin-seed");
        ChainResult adult = s2.Select(new SelectionContext(Scenarios.RaceDomain, "Call", AgeBucket.Adult, true, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(7));
        ChainResult baby = s2.Select(new SelectionContext(Scenarios.RaceDomain, "Call", AgeBucket.Baby, true, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(7));
        Check(adult.SoundKey == baby.SoundKey && adult.Tier == baby.Tier, "age-neutral in 0.3.0 (all-age default)", ref failures);
    }

    private static void AgePriority(ref int failures)
    {
        VoicePackEntry exactAndAll = Entry("age.mod:SR_Age", Scenarios.RaceDomain,
            Variant("Call", "SR_All", null),
            Variant("Call", "SR_Baby", AgeBucket.Baby));
        SqueakPoolRegistry registry = new(new[] { exactAndAll }, BuiltInFallbackTable.Empty, DomainFilter.Everything);
        ChainResult baby = registry.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call, AgeBucket.Baby), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        ChainResult child = registry.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call, AgeBucket.Child), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(baby.SoundKey == "SR_Baby" && child.SoundKey == "SR_All", "age exact variant wins; missing exact falls to all-age", ref failures);

        VoicePackEntry exactOnly = Entry("age.mod:SR_ExactOnly", Scenarios.RaceDomain, Variant("Call", "SR_BabyOnly", AgeBucket.Baby));
        SqueakPoolRegistry noMatch = new(new[] { exactOnly }, BuiltInFallbackTable.Empty, DomainFilter.Everything);
        ChainResult none = noMatch.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call, AgeBucket.Child), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(none.IsNone, "age with neither exact nor all-age is not a candidate", ref failures);

        VoicePackEntry mutedExact = Entry("age.mod:SR_MutedExact", Scenarios.RaceDomain,
            Variant("Call", "SR_AllPlayable", null),
            Variant("Call", "SR_Baby_Muted", AgeBucket.Baby));
        SqueakPoolRegistry noCrossVariantFallback = new(new[] { mutedExact }, BuiltInFallbackTable.Empty, DomainFilter.Everything);
        ChainResult muted = noCrossVariantFallback.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call, AgeBucket.Baby), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(1));
        Check(muted.IsNone, "muted exact age does not fall through to all-age", ref failures);
    }

    private static void PackFallbackTier(ref int failures)
    {
        VoicePackEntry raceFallback = Entry("fallback.mod:SR_Race", Scenarios.RaceDomain,
            fallback: new Dictionary<string, string> { ["Call"] = "SR_PackFallback_Race" });
        SqueakPoolRegistry raceRegistry = new(new[] { raceFallback }, Scenarios.BuildBuiltIn(), DomainFilter.Everything);
        ChainResult race = raceRegistry.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(race.Tier == ChainTier.PackFallback && race.SoundKey == "SR_PackFallback_Race" && race.PoolStableKey == "fallback.mod:SR_Race", "pack fallback runs after empty race tier before built-in", ref failures);

        VoicePackEntry xenoFallback = Entry("fallback.mod:SR_Xeno", Scenarios.XenoDomain,
            fallback: new Dictionary<string, string> { ["Call"] = "SR_PackFallback_Xeno" });
        VoicePackEntry otherRaceFallback = Entry("fallback.mod:SR_RaceOther", Scenarios.RaceDomain,
            fallback: new Dictionary<string, string> { ["Call"] = "SR_PackFallback_RaceOther" });
        SqueakPoolRegistry xenoRegistry = new(new[] { xenoFallback, otherRaceFallback }, Scenarios.BuildBuiltIn(), DomainFilter.Everything);
        ChainResult xeno = xenoRegistry.Select(Ctx(Scenarios.XenoDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(xeno.Tier == ChainTier.PackFallback && xeno.SoundKey == "SR_PackFallback_Xeno", "xeno context reads only its exact pool fallback", ref failures);

        SqueakPoolRegistry absent = new(Array.Empty<VoicePackEntry>(), Scenarios.BuildBuiltIn(), DomainFilter.Everything);
        ChainResult builtin = absent.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(builtin.Tier == ChainTier.BuiltInFallback, "absent pack fallback leaves tier empty", ref failures);

        HashSet<ChainTier?> tiers = new();
        for (int seed = 1; seed <= 80; seed++)
        {
            ChainResult result = xenoRegistry.Select(Ctx(Scenarios.XenoDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(seed));
            tiers.Add(result.Tier);
        }
        Check(tiers.Contains(ChainTier.PackFallback) && tiers.Contains(ChainTier.BuiltInFallback), "remix includes pack fallback as a fourth tier", ref failures);
    }

    private static void EggFiltering(ref int failures)
    {
        VoicePackEntry egg = Entry("egg.mod:SR_Egg", Scenarios.RaceDomain, Variant("Call", "SR_EggOnly", null, true));
        SqueakPoolRegistry onlyEgg = new(new[] { egg }, BuiltInFallbackTable.Empty, DomainFilter.Everything);
        ChainResult disabled = onlyEgg.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call, AgeBucket.Adult, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        ChainResult enabled = onlyEgg.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call, AgeBucket.Adult, true), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(disabled.IsNone && enabled.SoundKey == "SR_EggOnly", "egg variant is excluded while disabled and admitted while enabled", ref failures);
        VoicePackEntry exactEggWithAllAge = Entry("egg.mod:SR_ExactEgg", Scenarios.RaceDomain,
            Variant("Call", "SR_AllAgeNormal", null),
            Variant("Call", "SR_BabyEgg", AgeBucket.Baby, true));
        SqueakPoolRegistry noEggAgeFallback = new(new[] { exactEggWithAllAge }, BuiltInFallbackTable.Empty, DomainFilter.Everything);
        ChainResult exactDisabled = noEggAgeFallback.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call, AgeBucket.Baby, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(exactDisabled.IsNone, "disabled exact egg does not fall through to all-age", ref failures);

        VoicePackEntry normal = Entry("egg.mod:SR_Normal", Scenarios.RaceDomain, Variant("Call", "SR_Normal", null));
        SqueakPoolRegistry mixed = new(new[] { egg, normal }, BuiltInFallbackTable.Empty, DomainFilter.Everything);
        int eggs = 0, normalCount = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            ChainResult result = mixed.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call, AgeBucket.Adult, true), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.SoundKey == "SR_EggOnly") eggs++;
            else if (result.SoundKey == "SR_Normal") normalCount++;
        }
        double ratio = eggs / 400.0;
        Check(normalCount > 0 && ratio > 0.3 && ratio < 0.7, "enabled egg is an equal additive pool member", ref failures);
    }

    private static void PackWeight(ref int failures)
    {
        VoicePackEntry light = Entry("weight.mod:SR_Light", Scenarios.RaceDomain, 1f, Variant("Call", "SR_Light", null));
        VoicePackEntry heavy = Entry("weight.mod:SR_Heavy", Scenarios.RaceDomain, 3f, Variant("Call", "SR_Heavy", null));
        SqueakPoolRegistry registry = new(new[] { light, heavy }, BuiltInFallbackTable.Empty, DomainFilter.Everything);
        int heavyCount = 0;
        for (int seed = 1; seed <= 800; seed++)
        {
            ChainResult result = registry.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.SoundKey == "SR_Heavy") heavyCount++;
        }
        double ratio = heavyCount / 800.0;
        Check(ratio > 0.65 && ratio < 0.85, "pack weight uses cumulative weighted draw: " + ratio.ToString("0.000"), ref failures);

        VoicePackEntry invalid = Entry("weight.mod:SR_Invalid", Scenarios.RaceDomain, 0f, Variant("Call", "SR_Invalid", null));
        SqueakPoolRegistry rejectsInvalid = new(new[] { invalid }, BuiltInFallbackTable.Empty, DomainFilter.Everything);
        ChainResult none = rejectsInvalid.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(none.IsNone, "nonpositive direct pack weight is rejected", ref failures);
    }

    /// <summary>0.3.1 波 4a 漏斗纯逻辑提取：SqueakTimingModel 纯求值语义逐条断言（决策 §5 漏斗纯逻辑提取）。
    /// 数学与 0.2.4 CompSqueaker 内嵌实现逐字节等价（提取 = 同变更，无新时序语义）。</summary>
    private static void TimingModelRules(ref int failures)
    {
        SqueakActionPlan plan = TestPlan(300);
        SqueakTimingEvaluation fresh = SqueakTimingModel.Evaluate(Timing(0, plan));
        Check(fresh.ActionIntervalTicks == 300 && fresh.ActionIntervalSeconds == null && fresh.ActionRemainingTicks == 300
            && fresh.GlobalCooldownTicks == 216 && fresh.GlobalRemainingTicks == 216,
            "timing game-tick fresh state (300/216 intervals, 300/216 remaining)", ref failures);
        Check(!fresh.ActionReady && !fresh.GlobalReady && !fresh.TimingReady,
            "timing fresh state not ready", ref failures);

        SqueakTimingEvaluation elapsed = SqueakTimingModel.Evaluate(Timing(300, plan));
        Check(elapsed.ActionReady && elapsed.ActionRemainingTicks == 0 && elapsed.GlobalReady && elapsed.GlobalRemainingTicks == 0 && elapsed.TimingReady,
            "timing ready when elapsed reaches interval", ref failures);

        SqueakTimingEvaluation timeSpeed = SqueakTimingModel.Evaluate(Timing(0, plan, timeSpeed: 3f));
        Check(timeSpeed.ActionIntervalTicks == 900 && timeSpeed.GlobalCooldownTicks == 648,
            "timing time-speed scaling (300*3, 216*3)", ref failures);
        SqueakTimingEvaluation noScale = SqueakTimingModel.Evaluate(Timing(0, plan, timeSpeed: 3f, scaleTimeSpeed: false));
        Check(noScale.ActionIntervalTicks == 300 && noScale.GlobalCooldownTicks == 216,
            "timing time-speed disabled keeps base ticks", ref failures);

        SqueakTimingEvaluation chain = SqueakTimingModel.Evaluate(Timing(0, plan, overall: 2f, action: 1.5f, master: 0.5f));
        Check(chain.ActionIntervalTicks == 450,
            "timing multiplier chain rounds staged (300*2*1.5=900, then *0.5=450)", ref failures);

        SqueakTimingEvaluation zero = SqueakTimingModel.Evaluate(Timing(0, TestPlan(0), globalBase: 0));
        Check(zero.ActionIntervalTicks == 0 && zero.ActionReady && zero.GlobalCooldownTicks == 0 && zero.GlobalReady && zero.TimingReady,
            "timing zero interval means no cooldown", ref failures);

        SqueakTimingEvaluation sanitized = SqueakTimingModel.Evaluate(Timing(0, plan, overall: float.NaN, action: float.PositiveInfinity, timeSpeed: float.NaN));
        Check(sanitized.ActionIntervalTicks == 300 && sanitized.GlobalCooldownTicks == 216,
            "timing sanitizes NaN/Infinity multipliers to identity", ref failures);

        SqueakTimingEvaluation population = SqueakTimingModel.Evaluate(Timing(0, plan, populationScale: 2f));
        Check(population.ActionIntervalTicks == 600 && population.GlobalCooldownTicks == 432,
            "timing population scale multiplies ticks", ref failures);

        SqueakActionPlan ignoring = TestPlan(300, ignoreGlobalCooldown: true);
        SqueakTimingEvaluation ignoreGlobal = SqueakTimingModel.Evaluate(Timing(0, ignoring));
        Check(!ignoreGlobal.GlobalApplicable && ignoreGlobal.TimingReady == ignoreGlobal.ActionReady,
            "timing ignore-global makes TimingReady equal ActionReady", ref failures);

        SqueakActionPlan realtime = TestPlan(300, clock: SqueakCooldownClock.Realtime);
        SqueakTimingEvaluation rtPending = SqueakTimingModel.Evaluate(Timing(0, realtime, nowRealtime: 104f, lastActionRealtime: 100f));
        Check(rtPending.ActionIntervalTicks == null && rtPending.ActionIntervalSeconds == 5f
            && rtPending.ActionRemainingSeconds == 1f && !rtPending.ActionReady,
            "timing realtime clock uses seconds and elapsed realtime", ref failures);
        SqueakTimingEvaluation rtReady = SqueakTimingModel.Evaluate(Timing(0, realtime, nowRealtime: 105f, lastActionRealtime: 100f));
        Check(rtReady.ActionReady && rtReady.ActionRemainingSeconds == 0f,
            "timing realtime ready at 5s", ref failures);
        SqueakTimingEvaluation rtPopulation = SqueakTimingModel.Evaluate(Timing(0, realtime, nowRealtime: 0f, lastActionRealtime: 0f, populationScale: 2f));
        Check(rtPopulation.ActionIntervalSeconds == 10f,
            "timing realtime population scale multiplies seconds", ref failures);

        SqueakTimingEvaluation clamped = SqueakTimingModel.Evaluate(Timing(0, TestPlan(int.MaxValue), lastActionTick: 0));
        Check(clamped.ActionRemainingTicks == int.MaxValue,
            "timing remaining ticks clamps at int.MaxValue", ref failures);

        Check(SqueakTimingModel.GetActionIntervalTicks(300, 1f, 1f, 1f, true, 2f) == 600,
            "timing public GetActionIntervalTicks helper", ref failures);
        Check(SqueakTimingModel.GetActionIntervalSeconds(300, 1f, 1f, 1f) == 5f,
            "timing public GetActionIntervalSeconds helper", ref failures);
        Check(SqueakTimingModel.GetGlobalCooldownTicks(216, 2f, true, 1f) == 432,
            "timing public GetGlobalCooldownTicks helper", ref failures);
    }

    /// <summary>0.3.1 波 4a 漏斗纯逻辑提取：SqueakTriggerInvocation 语义（非周期跳过 RandomOneShot 概率）。
    /// 0.3.2 身份门控扩展：PlayerSelection/ActiveCommand 派生 IsPlayerInitiated；PlayerSelection 派生 RequiresResponsivePawn。</summary>
    private static void TriggerInvocationRules(ref int failures)
    {
        SqueakTriggerInvocation periodic = new(SqueakTriggerOrigin.Periodic, SqueakInvocationSource.Periodic);
        Check(!periodic.SkipsRandomOneShotProbability && !periodic.IsExternal && !periodic.IsActiveCommand,
            "invocation periodic keeps probability and is not external", ref failures);
        Check(!periodic.IsPlayerInitiated && !periodic.RequiresResponsivePawn,
            "invocation periodic is not player initiated and requires no responsiveness", ref failures);

        SqueakTriggerInvocation wounded = new(SqueakTriggerOrigin.Wounded, SqueakInvocationSource.StateEvent);
        Check(wounded.SkipsRandomOneShotProbability && wounded.IsExternal && !wounded.IsActiveCommand,
            "invocation state event skips probability and is external", ref failures);
        Check(!wounded.IsPlayerInitiated && !wounded.RequiresResponsivePawn,
            "invocation state event is not player initiated", ref failures);

        SqueakTriggerInvocation draft = new(SqueakTriggerOrigin.Draft, SqueakInvocationSource.ActiveCommand);
        Check(draft.IsActiveCommand && draft.IsExternal && draft.SkipsRandomOneShotProbability,
            "invocation active command carries IsActiveCommand", ref failures);
        Check(draft.IsPlayerInitiated && !draft.RequiresResponsivePawn,
            "invocation active command is player initiated without responsiveness requirement", ref failures);

        SqueakTriggerInvocation select = new(SqueakTriggerOrigin.Select, SqueakInvocationSource.PlayerSelection);
        Check(select.IsExternal && !select.IsActiveCommand,
            "invocation player selection is external but not active command", ref failures);
        Check(select.IsPlayerInitiated && select.RequiresResponsivePawn,
            "invocation player selection is player initiated and requires a responsive pawn", ref failures);

        SqueakTriggerInvocation crying = new(SqueakTriggerOrigin.Crying, SqueakInvocationSource.StateEvent);
        SqueakTriggerInvocation giggling = new(SqueakTriggerOrigin.Giggling, SqueakInvocationSource.StateEvent);
        Check(crying.IsExternal && giggling.IsExternal,
            "invocation BabyFits origins are external", ref failures);
        Check(!crying.IsPlayerInitiated && !giggling.IsPlayerInitiated,
            "invocation BabyFits origins are not player initiated", ref failures);

        SqueakTriggerInvocation equip = new(SqueakTriggerOrigin.Equip, SqueakInvocationSource.ActiveCommand);
        Check(equip.IsPlayerInitiated && !equip.RequiresResponsivePawn,
            "invocation equip active command is player initiated without responsiveness requirement", ref failures);
    }

    /// <summary>0.3.1 波 4a per-race 池隔离 harness（决策 §5：两 race 池互不串扰）：
    /// Ratkin/Kiiro 两 race 池 + 仅 Ratkin 内置表；跨模式/种子断言零交叉。</summary>
    private static void PerRacePoolIsolation(ref int failures)
    {
        SqueakPoolRegistry registry = Scenarios.BuildRegistry("S7-two-races");
        DomainPool? ratkinPool = registry.PoolFor(Scenarios.RaceDomain);
        DomainPool? kiiroPool = registry.PoolFor(Scenarios.KiiroDomain);
        Check(ratkinPool != null && ratkinPool.Entries.Count == 1 && ratkinPool.Entries[0].PackKey == "coahuilite.squeakyratkin:SR_OfficialExample_Race",
            "per-race Ratkin pool holds only Ratkin pack", ref failures);
        Check(kiiroPool != null && kiiroPool.Entries.Count == 1 && kiiroPool.Entries[0].PackKey == "kiiro.mod:SR_KiiroVoice",
            "per-race Kiiro pool holds only Kiiro pack", ref failures);

        // Ratkin 查询绝不返回 Kiiro 包键（三模式 × 多种子）。
        foreach (SelectionMode mode in new[] { SelectionMode.Off, SelectionMode.Fallback, SelectionMode.Remix })
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                ChainResult result = registry.Select(Ctx(Scenarios.RaceDomain, SqueakyRatkin.SqueakAction.Call, allowEggs: false), mode, SimGate.All, new LcgRandom(seed));
                if (result.PoolStableKey != null && result.PoolStableKey.StartsWith("kiiro.mod:", StringComparison.Ordinal))
                {
                    Check(false, "per-race Ratkin query leaked Kiiro pack: " + result.PoolStableKey, ref failures);
                    return;
                }
            }
        }

        // Kiiro 查询只返回 Kiiro 包键；Off = 静默（内置表 per-race，Kiiro 无 profile）。
        for (int seed = 1; seed <= 40; seed++)
        {
            ChainResult off = registry.Select(Ctx(Scenarios.KiiroDomain, SqueakyRatkin.SqueakAction.Call, allowEggs: false), SelectionMode.Off, SimGate.All, new LcgRandom(seed));
            if (!off.IsNone)
            {
                Check(false, "per-race Kiiro Off must be silent (no built-in profile)", ref failures);
                return;
            }
            ChainResult fallback = registry.Select(Ctx(Scenarios.KiiroDomain, SqueakyRatkin.SqueakAction.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (fallback.PoolStableKey != "kiiro.mod:SR_KiiroVoice" || (fallback.SoundKey != null && !fallback.SoundKey.StartsWith("kiiro.mod:", StringComparison.Ordinal)))
            {
                Check(false, "per-race Kiiro Fallback must resolve only Kiiro pack", ref failures);
                return;
            }
        }

        // Kiiro 域无池（只有 Ratkin 池）→ Fallback 不越域落入 Ratkin 池/内置（链不跨 race）。
        SqueakPoolRegistry ratkinOnly = new(new[]
        {
            ScenariosEntry("coahuilite.squeakyratkin:SR_OfficialExample_Race", Scenarios.RaceDomain, sounds: 1),
        }, Scenarios.BuildBuiltIn(), DomainFilter.Everything);
        ChainResult noLeak = ratkinOnly.Select(Ctx(Scenarios.KiiroDomain, SqueakyRatkin.SqueakAction.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(noLeak.IsNone, "per-race Kiiro query never falls through to Ratkin pool or Ratkin built-in", ref failures);

        // xeno 子域隔离：Kiiro+Xeno 池只在 Kiiro+Xeno 域可见；Ratkin+Xeno 查询不得命中。
        AudioDomain ratkinXeno = new(Scenarios.Ratkin, Scenarios.XenoA);
        AudioDomain kiiroXeno = new(Scenarios.Kiiro, Scenarios.XenoA);
        SqueakPoolRegistry xenoIsolated = new(new[]
        {
            ScenariosEntry("ratkin.mod:SR_RatkinXeno", ratkinXeno, sounds: 1),
            ScenariosEntry("kiiro.mod:SR_KiiroXeno", kiiroXeno, sounds: 1),
        }, Scenarios.BuildBuiltIn(), DomainFilter.Everything);
        ChainResult kx = xenoIsolated.Select(Ctx(kiiroXeno, SqueakyRatkin.SqueakAction.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(2));
        Check(kx.PoolStableKey == "kiiro.mod:SR_KiiroXeno", "per-race Kiiro+xeno resolves its exact pool", ref failures);
        ChainResult rx = xenoIsolated.Select(Ctx(ratkinXeno, SqueakyRatkin.SqueakAction.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(2));
        Check(rx.PoolStableKey == "ratkin.mod:SR_RatkinXeno", "per-race Ratkin+xeno resolves its exact pool", ref failures);
    }

    // ---- helpers ----

    private static SelectionContext Ctx(AudioDomain domain, SqueakyRatkin.SqueakAction action, AgeBucket age = AgeBucket.Adult, bool allowEggs = false)
        => new(domain, ActionKey.For(action)!, age, false, allowEggs);

    private static SqueakActionDefinition TestDefinition()
        => new(SqueakyRatkin.SqueakAction.Call, "SR.Action.Call", "SR_Call", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence);

    private static SqueakActionPlan TestPlan(int minIntervalTicks, bool ignoreGlobalCooldown = false, SqueakCooldownClock clock = SqueakCooldownClock.GameTicks)
        => new(TestDefinition(), true, SqueakTriggerMode.RandomOneShot, minIntervalTicks, .02f, ignoreGlobalCooldown, clock);

    private static SqueakTimingInput Timing(int nowTick, SqueakActionPlan plan, float nowRealtime = 0f, float timeSpeed = 1f,
        float overall = 1f, float action = 1f, int lastActionTick = 0, float lastActionRealtime = 0f,
        int lastGlobalTick = 0, int globalBase = 216, float master = 1f, bool scaleTimeSpeed = true, float populationScale = 1f)
        => new(nowTick, nowRealtime, timeSpeed, plan, overall, action, lastActionTick, lastActionRealtime,
            lastGlobalTick, globalBase, master, scaleTimeSpeed, populationScale);

    private static IEnumerable<int> Range(int from, int count) { for (int i = from; i < from + count; i++) yield return i; }

    private readonly struct TestVariant
    {
        public readonly string ActionKey;
        public readonly ActionSoundSet Set;

        public TestVariant(string actionKey, ActionSoundSet set)
        {
            ActionKey = actionKey;
            Set = set;
        }
    }

    private static TestVariant Variant(string actionKey, string soundKey, AgeBucket? ageTag, bool isEgg = false)
        => new(actionKey, new ActionSoundSet(new[] { soundKey }, ageTag, 1f, isEgg));

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
        return new VoicePackEntry(packKey, domain, weight, actions, fallback);
    }
    public static VoicePackEntry ScenariosEntry(string packKey, AudioDomain domain, int sounds = 2)
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        for (int i = 0; i < ActionAudioKeyMirror.Count; i++)
        {
            SqueakyRatkin.SqueakAction action = (SqueakyRatkin.SqueakAction)i;
            string audioKey = ActionAudioKeyMirror.For(action);
            List<string> keys = new(sounds);
            for (int s = 0; s < sounds; s++) keys.Add(packKey + "_" + audioKey + "_" + s);
            actions[ActionKey.For(action)!] = new[] { new ActionSoundSet(keys, null, 1f) };
        }
        return new VoicePackEntry(packKey, domain, 1f, actions);
    }

    private static VoicePackEntry MutedEntry(string packKey, AudioDomain domain)
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        for (int i = 0; i < ActionAudioKeyMirror.Count; i++)
        {
            SqueakyRatkin.SqueakAction action = (SqueakyRatkin.SqueakAction)i;
            string audioKey = ActionAudioKeyMirror.For(action);
            actions[ActionKey.For(action)!] = new[] { new ActionSoundSet(new[] { packKey + "_" + audioKey + "_Muted" }, null, 1f) };
        }
        return new VoicePackEntry(packKey, domain, 1f, actions);
    }
}

