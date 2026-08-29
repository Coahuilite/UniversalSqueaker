using System;
using System.Collections.Generic;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker.KernelTests;

/// <summary>验证门单测。每断言独立输出；失败计数由 Program 汇总。</summary>
public static class UnitTests
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

    /// <summary>S5 分层调音纯折叠（Pure/SqueakLayeredTuning）：H1 回归 + race→xeno 心情继承 + 字段级 last-wins。</summary>
    private static void LayeredTuning(ref int failures)
    {
        // H1 回归：global off + xeno on —— 层折叠后 xeno 显式启用胜出（真旁路语义只在外置 gate，与折叠无关）。
        ResolvedActionDelta h1 = ResolvedActionDelta.Resolve(
            SqueakActionScope.AnyOccurrence,
            new LayerActionDelta(SqueakActionScope.Disabled, true, false, 1f, false, 1f),
            null,
            new LayerActionDelta(SqueakActionScope.AnyOccurrence, true, false, 1f, false, 1f));
        Check(h1.Enabled && h1.Scope == SqueakActionScope.AnyOccurrence, "layered: global off + xeno on wins (H1)", ref failures);

        // 优先级：DefaultScope < Global < Race < Xenotype。
        ResolvedActionDelta prio = ResolvedActionDelta.Resolve(
            SqueakActionScope.ActiveCommand,
            new LayerActionDelta(SqueakActionScope.AnyOccurrence, true, false, 1f, false, 1f),
            new LayerActionDelta(SqueakActionScope.Disabled, true, false, 1f, false, 1f),
            new LayerActionDelta(SqueakActionScope.AnyOccurrence, true, false, 1f, false, 1f));
        Check(prio.Scope == SqueakActionScope.AnyOccurrence, "layered: xeno overrides race over global over default", ref failures);

        // 字段级 last-wins：race 只写 scope，interval 继承 global。
        ResolvedActionDelta field = ResolvedActionDelta.Resolve(
            SqueakActionScope.AnyOccurrence,
            new LayerActionDelta(SqueakActionScope.AnyOccurrence, false, true, 2f, false, 1f),
            new LayerActionDelta(SqueakActionScope.Disabled, true, false, 1f, false, 1f),
            null);
        Check(field.Scope == SqueakActionScope.Disabled && Math.Abs(field.IntervalMultiplier - 2f) < 0.0001f,
            "layered: field-level last-wins (scope overridden, interval inherited)", ref failures);

        // 空层记录 = 恒等（不吞继承）：基座用非默认值，空层不得破坏任何字段。
        ResolvedActionDelta empty = ResolvedActionDelta.Resolve(
            SqueakActionScope.ActiveCommand,
            new LayerActionDelta(SqueakActionScope.AnyOccurrence, false, true, 2f, true, 1.5f),
            new LayerActionDelta(SqueakActionScope.AnyOccurrence, false, false, 1f, false, 1f),
            null);
        Check(empty.Scope == SqueakActionScope.ActiveCommand
            && Math.Abs(empty.IntervalMultiplier - 2f) < 0.0001f
            && Math.Abs(empty.ProbabilityMultiplier - 1.5f) < 0.0001f,
            "layered: empty layer is identity (non-default base preserved)", ref failures);

        // 概率乘数跨层继承。
        ResolvedActionDelta prob = ResolvedActionDelta.Resolve(
            SqueakActionScope.AnyOccurrence,
            new LayerActionDelta(SqueakActionScope.AnyOccurrence, false, false, 1f, true, 1.5f),
            new LayerActionDelta(SqueakActionScope.Disabled, true, false, 1f, false, 1f),
            null);
        Check(Math.Abs(prob.ProbabilityMultiplier - 1.5f) < 0.0001f && prob.Scope == SqueakActionScope.Disabled,
            "layered: probability inherited across layers", ref failures);

        // 显式 ActiveCommand 在 race 层保真（旧 builder 曾拍平为 Any；这是有意的语义修复）。
        ResolvedActionDelta command = ResolvedActionDelta.Resolve(
            SqueakActionScope.AnyOccurrence,
            null,
            new LayerActionDelta(SqueakActionScope.ActiveCommand, true, false, 1f, false, 1f),
            null);
        Check(command.Scope == SqueakActionScope.ActiveCommand,
            "layered: explicit ActiveCommand survives race layer (old builder flattened to Any; intentional)", ref failures);

        // 同层多记录合并：后写覆盖先写字段，HasX 取并集。
        LayerActionDelta merged = new LayerActionDelta(SqueakActionScope.Disabled, true, true, 1.5f, false, 1f)
            .Merge(new LayerActionDelta(SqueakActionScope.AnyOccurrence, true, false, 1f, false, 1f));
        Check(merged.HasScope && merged.Scope == SqueakActionScope.AnyOccurrence
            && merged.HasIntervalMultiplier && Math.Abs(merged.IntervalMultiplier - 1.5f) < 0.0001f,
            "layered: same-layer merge last-wins per field", ref failures);

        // 心情：xeno pitch 覆盖 global，race volume 继承（三层各写一因子）。
        ResolvedMoodDelta mood = ResolvedMoodDelta.Resolve(
            new LayerMoodDelta(true, 1.2f, false, 1f, false, 1f, 1f),
            new LayerMoodDelta(false, 1f, true, 0.8f, false, 1f, 1f),
            new LayerMoodDelta(true, 0.9f, false, 1f, false, 1f, 1f));
        Check(Math.Abs(mood.PitchFactor - 0.9f) < 0.0001f && Math.Abs(mood.VolumeFactor - 0.8f) < 0.0001f,
            "layered: mood xeno pitch overrides, race volume inherited", ref failures);

        // jitter 跨层继承：global 设抖动，xeno 只写 pitch → 抖动保留（HasPitchJitter 沿链合并）。
        ResolvedMoodDelta jittered = ResolvedMoodDelta.Resolve(
            new LayerMoodDelta(false, 1f, false, 1f, true, 0.9f, 1.1f),
            null,
            new LayerMoodDelta(true, 0.95f, false, 1f, false, 1f, 1f));
        Check(Math.Abs(jittered.PitchFactor - 0.95f) < 0.0001f
            && jittered.HasPitchJitter
            && Math.Abs(jittered.JitterMin - 0.9f) < 0.0001f
            && Math.Abs(jittered.JitterMax - 1.1f) < 0.0001f,
            "layered: jitter inherited from global while xeno overrides pitch", ref failures);

        // 心情同层合并：后写因子覆盖，未写因子保留。
        LayerMoodDelta moodMerged = new LayerMoodDelta(true, 1.1f, false, 1f, false, 1f, 1f)
            .Merge(new LayerMoodDelta(true, 1.3f, true, 0.7f, false, 1f, 1f));
        Check(Math.Abs(moodMerged.PitchFactor - 1.3f) < 0.0001f
            && moodMerged.HasVolumeFactor && Math.Abs(moodMerged.VolumeFactor - 0.7f) < 0.0001f,
            "layered: mood same-layer merge last-wins per factor", ref failures);

        // race→xeno 心情继承：xeno 无记录 → race 因子生效（H3 完成面）。
        ResolvedMoodDelta inherited = ResolvedMoodDelta.Resolve(null,
            new LayerMoodDelta(true, 1.1f, false, 1f, false, 1f, 1f), null);
        Check(Math.Abs(inherited.PitchFactor - 1.1f) < 0.0001f && inherited.HasPitchFactor,
            "layered: xeno inherits race mood when no xeno record", ref failures);

        // 心情默认：无任何层记录 = 全默认且 IsDefault（GetMoodDelta 空语义 = 无 delta）。
        ResolvedMoodDelta defaults = ResolvedMoodDelta.Resolve(null, null, null);
        Check(defaults.IsDefault && Math.Abs(defaults.PitchFactor - 1f) < 0.0001f,
            "layered: mood default identity", ref failures);
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

    private static void ActionKeyMapping(ref int failures)
    {
        string[] enumNames = Enum.GetNames(typeof(SqueakAction));
        bool allOk = enumNames.Length == BuiltInActionKeys.All.Count;
        for (int i = 0; i < enumNames.Length; i++)
        {
            SqueakAction action = (SqueakAction)i;
            string? key = ActionKey.For(action);
            if (key == null || key != enumNames[i] || key != BuiltInActionKeys.All[i]
                || !ActionKey.TryParseBuiltIn(key, out SqueakAction parsed) || parsed != action)
                allOk = false;
        }
        Check(BuiltInActionKeys.All.Count == 17 && BuiltInActionKeys.All[15] == "Crying" && BuiltInActionKeys.All[16] == "Giggling",
            "BuiltInActionKeys holds 17 ordered keys", ref failures);
        Check(allOk, "ActionKey 17-action prefix is bidirectional", ref failures);
        Check(enumNames.Length == 17 && enumNames[15] == "Crying" && enumNames[16] == "Giggling",
            "SqueakAction append-only ordinals 15/16 = Crying/Giggling", ref failures);
        Check(ActionKey.For((SqueakAction)99) == null, "ActionKey.For unknown null", ref failures);
        Check(BuiltInActionKeys.TryGetIndex("Crying", out int cryingIndex) && cryingIndex == 15
            && ActionKey.TryParseBuiltIn("Crying", out SqueakAction crying) && crying == SqueakAction.Crying,
            "Crying key is enum-mappable after append", ref failures);
        Check(!ActionKey.TryParseBuiltIn("other.mod:US_X", out _), "TryParseBuiltIn external key false", ref failures);
        Check(!ActionKey.TryParseBuiltIn("call", out _), "TryParseBuiltIn wrong case false", ref failures);
    }

    /// <summary>VerseEvent 绑定契约（行为等价回归）：8 个外部动作的 Origin 固定映射 + 默认 Source，
    /// 与 us-s0-worknotes-zh.md §8 的 patch 行为基线逐条对齐。外部动作键回落 SqueakTriggerOrigin.External。</summary>
    private static void VerseEventBindingContract(ref int failures)
    {
        Check(VerseEventBinding.OriginFor(SqueakAction.Wounded) == SqueakTriggerOrigin.Wounded, "OriginFor Wounded", ref failures);
        Check(VerseEventBinding.OriginFor(SqueakAction.Select) == SqueakTriggerOrigin.Select, "OriginFor Select", ref failures);
        Check(VerseEventBinding.OriginFor(SqueakAction.Death) == SqueakTriggerOrigin.Death, "OriginFor Death", ref failures);
        Check(VerseEventBinding.OriginFor(SqueakAction.Draft) == SqueakTriggerOrigin.Draft, "OriginFor Draft", ref failures);
        Check(VerseEventBinding.OriginFor(SqueakAction.Undraft) == SqueakTriggerOrigin.Undraft, "OriginFor Undraft", ref failures);
        Check(VerseEventBinding.OriginFor(SqueakAction.Attack) == SqueakTriggerOrigin.Attack, "OriginFor Attack", ref failures);
        Check(VerseEventBinding.OriginFor(SqueakAction.Equip) == SqueakTriggerOrigin.Equip, "OriginFor Equip", ref failures);
        Check(VerseEventBinding.OriginFor(SqueakAction.MentalBreak) == SqueakTriggerOrigin.MentalBreak, "OriginFor MentalBreak", ref failures);
        Check(VerseEventBinding.OriginFor(SqueakAction.Crying) == SqueakTriggerOrigin.Crying, "OriginFor Crying", ref failures);
        Check(VerseEventBinding.OriginFor(SqueakAction.Giggling) == SqueakTriggerOrigin.Giggling, "OriginFor Giggling", ref failures);
        Check(VerseEventBinding.OriginFor(SqueakAction.Call) == SqueakTriggerOrigin.External, "OriginFor periodic Call falls back External", ref failures);
        Check(VerseEventBinding.DefaultSource(SqueakAction.Select) == SqueakInvocationSource.PlayerSelection, "DefaultSource Select", ref failures);
        Check(VerseEventBinding.DefaultSource(SqueakAction.Draft) == SqueakInvocationSource.ActiveCommand, "DefaultSource Draft", ref failures);
        Check(VerseEventBinding.DefaultSource(SqueakAction.Equip) == SqueakInvocationSource.ActiveCommand, "DefaultSource Equip", ref failures);
        Check(VerseEventBinding.DefaultSource(SqueakAction.Wounded) == SqueakInvocationSource.StateEvent, "DefaultSource Wounded", ref failures);
    }

    /// <summary>三方同步离线断言：① 枚举名序 == BuiltInActionKeys 17 键序 == 动作名镜像序；
    /// ② 内置表 15 项且不列 Crying/Giggling（SoundDef 面默认静默）；
    /// ③ 镜像 17 == 枚举 17（语料 17 动作矩阵同变更重建）。
    /// 数据面（SoundDef/本地化）同步在 US 数据面建成后另行覆盖。</summary>
    private static void ActionSync(ref int failures)
    {
        string[] enumNames = Enum.GetNames(typeof(SqueakAction));
        bool namesMatch = enumNames.Length == BuiltInActionKeys.All.Count && enumNames.Length == ActionKeyMirror.Count;
        for (int i = 0; i < enumNames.Length && namesMatch; i++)
            namesMatch = enumNames[i] == BuiltInActionKeys.All[i] && enumNames[i] == ActionKeyMirror.All[i];
        Check(namesMatch, "three-way sync: enum names/ordinals == BuiltInActionKeys == action mirror (17)", ref failures);

        FallbackProfile? profile = Scenarios.BuildBuiltIn().For(Scenarios.RaceA);
        bool builtInExcludes = profile != null && profile.SoundKeys.Count == ActionKeyMirror.BuiltInCount
            && !profile.SoundKeys.ContainsKey("Crying") && !profile.SoundKeys.ContainsKey("Giggling");
        Check(builtInExcludes, "three-way sync: built-in table 15 mappings without Crying/Giggling (default silence)", ref failures);

        Check(ActionKeyMirror.Count == 17 && ActionKeyMirror.BuiltInCount == 15,
            "three-way sync: mirror 17-action with 15 built-in mappings", ref failures);
    }

    private static void BuiltInTable(ref int failures)
    {
        BuiltInFallbackTable table = Scenarios.BuildBuiltIn();
        FallbackProfile? profile = table.For(Scenarios.RaceA);
        Check(profile != null && profile.SoundKeys.Count == ActionKeyMirror.BuiltInCount && !profile.SoundKeys.ContainsKey("Crying"),
            "built-in RaceA profile has current 15 mappings", ref failures);
        Check(table.For(new RaceKey("US_Race_Unknown")) == null, "unknown race returns null", ref failures);
        Check(table.TryGetSoundKey(Scenarios.RaceA, "Call", out string? k) && k == "US_Fallback_Call", "TryGetSoundKey Call=US_Fallback_Call", ref failures);
        Check(!table.TryGetSoundKey(Scenarios.RaceA, "other.mod:US_X", out _) && !table.TryGetSoundKey(Scenarios.RaceA, "Crying", out _),
            "built-in lookup rejects external and reserved-unmapped keys", ref failures);
        BuiltInFallbackTable reservedTable = new(new[]
        {
            new FallbackProfile(Scenarios.RaceA, 1, new Dictionary<string, string> { ["Crying"] = "US_Reserved_Crying" }),
        });
        Check(reservedTable.TryGetSoundKey(Scenarios.RaceA, "Crying", out string? reserved) && reserved == "US_Reserved_Crying",
            "string fallback lookup accepts reserved built-in keys", ref failures);
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
            ["Call"] = "US_Call_Source",
            ["Eat"] = "US_Eat_Source",
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
            && merged.SoundKeys["Call"] == "Core_Call_Override" && merged.SoundKeys["Eat"] == "US_Eat_Source",
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

        Dictionary<string, string> mutableProfileKeys = new() { ["Call"] = "US_Call_Original" };
        FallbackProfile copiedProfile = new(Scenarios.RaceA, 3, mutableProfileKeys);
        mutableProfileKeys["Call"] = "US_Call_Mutated";
        Check(copiedProfile.SoundKeys["Call"] == "US_Call_Original",
            "FallbackProfile defensively copies input dictionary", ref failures);

        Dictionary<string, string> mutableDeltaKeys = new() { ["Call"] = "US_Delta_Original" };
        FallbackDelta copiedDelta = new(mutableDeltaKeys);
        mutableDeltaKeys["Call"] = "US_Delta_Mutated";
        Check(copiedDelta.Overrides["Call"] == "US_Delta_Original",
            "FallbackDelta defensively copies input dictionary", ref failures);
    }

    /// <summary>错误路径回归：无池条目 + 种子内置表 + Off 仍放内置兜底；
    /// 空内置表才静音。</summary>
    private static void FailurePathFallback(ref int failures)
    {
        SqueakPoolRegistry failureRegistry = new(Array.Empty<VoicePackEntry>(), Scenarios.BuildBuiltIn());
        ChainResult hit = failureRegistry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Off, SimGate.All, new LcgRandom(1));
        Check(hit.Tier == ChainTier.BuiltInFallback && hit.SoundKey == "US_Fallback_Call" && hit.PoolStableKey == null, "failure-path registry resolves built-in US_Fallback_Call", ref failures);
        SqueakPoolRegistry emptyTable = new(Array.Empty<VoicePackEntry>(), BuiltInFallbackTable.Empty);
        ChainResult none = emptyTable.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Off, SimGate.All, new LcgRandom(1));
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
                    SqueakAction action = (SqueakAction)i;
                    ChainResult result = registry.Select(Ctx(domain, action), SelectionMode.Off, SimGate.All, new LcgRandom(1));
                    string expectedKey = ActionKeyMirror.BuiltInFallbackKey(action);
                    if (!(result.Tier == ChainTier.BuiltInFallback && result.SoundKey == expectedKey && result.PoolStableKey == null))
                    {
                        Check(false, "Off " + scenario + " " + domain + " " + action, ref failures);
                        return;
                    }
                }
                ChainResult crying = registry.Select(Ctx(domain, SqueakAction.Crying), SelectionMode.Off, SimGate.All, new LcgRandom(1));
                ChainResult giggling = registry.Select(Ctx(domain, SqueakAction.Giggling), SelectionMode.Off, SimGate.All, new LcgRandom(1));
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
        ChainResult r = s2.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(r.Tier == ChainTier.RacePack && r.PoolStableKey == Scenarios.RacePackA && r.SoundKey != null && r.SoundKey.StartsWith(Scenarios.RacePackA + "_Call_"),
            "Fallback S2 race tier wins", ref failures);

        // S3 Xeno 域：x 命中 XenotypePack（1 entry 池）。
        SqueakPoolRegistry s3 = Scenarios.BuildRegistry("S3-builtin-plus-xeno");
        ChainResult x = s3.Select(Ctx(Scenarios.XenoDomain, SqueakAction.Eat), SelectionMode.Fallback, SimGate.All, new LcgRandom(2));
        Check(x.Tier == ChainTier.XenotypePack && x.PoolStableKey == Scenarios.XenoPack, "Fallback S3 xeno tier wins", ref failures);

        // S4 orphan xeno 域：无池 → x None → r 命中 RacePack（orphan 保留 = 域选择存在但无包 → 回退）。
        SqueakPoolRegistry s4 = Scenarios.BuildRegistry("S4-orphan-xeno");
        ChainResult s4r = s4.Select(Ctx(Scenarios.XenoDomain, SqueakAction.Sleep), SelectionMode.Fallback, SimGate.All, new LcgRandom(3));
        Check(s4r.Tier == ChainTier.RacePack, "Fallback S4 orphan xeno falls to race", ref failures);

        // S1 空池 → builtin。
        SqueakPoolRegistry s1 = Scenarios.BuildRegistry("S1-empty");
        ChainResult s1r = s1.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(4));
        Check(s1r.Tier == ChainTier.BuiltInFallback && s1r.SoundKey == "US_Fallback_Call", "Fallback S1 empty falls to built-in", ref failures);

        // 全无声：x 池 entry 全 Muted + race 池空 → builtin 仍可播（gate 对 builtin 也执行——SimGate.All 可播）。
        SqueakPoolRegistry muted = new(new[]
        {
            MutedEntry(Scenarios.XenoPack, Scenarios.XenoDomain),
        }, Scenarios.BuildBuiltIn());
        ChainResult m = muted.Select(Ctx(Scenarios.XenoDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(5));
        Check(m.Tier == ChainTier.BuiltInFallback && m.SoundKey == "US_Fallback_Call", "Fallback muted xeno falls to built-in", ref failures);
    }

    private static void SelectRemix(ref int failures)
    {
        // S3 Xeno 域：tiers=[x,r,builtin] 全非 None → 等权折叠（固定序 [xeno, race, builtin]）。
        SqueakPoolRegistry s3 = Scenarios.BuildRegistry("S3-builtin-plus-xeno");
        HashSet<ChainTier?> seen = new();
        for (int seed = 1; seed <= 60; seed++)
        {
            ChainResult result = s3.Select(Ctx(Scenarios.XenoDomain, SqueakAction.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(seed));
            if (result.IsNone) { Check(false, "Remix S3 never none", ref failures); return; }
            seen.Add(result.Tier);
        }
        Check(seen.Contains(ChainTier.XenotypePack) && seen.Contains(ChainTier.RacePack) && seen.Contains(ChainTier.BuiltInFallback), "Remix S3 folds all three tiers", ref failures);

        // S1 空池 → tiers=[builtin] → builtin。
        SqueakPoolRegistry s1 = Scenarios.BuildRegistry("S1-empty");
        ChainResult s1r = s1.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(1));
        Check(s1r.Tier == ChainTier.BuiltInFallback && s1r.SoundKey == "US_Fallback_Call", "Remix S1 single tier", ref failures);
    }

    private static void DistributionBounds(ref int failures)
    {
        // entry 级：S2 Race 池 1 entry × 2 sounds → sound 级等权（400 次，界 0.3-0.7）。
        SqueakPoolRegistry s2 = Scenarios.BuildRegistry("S2-builtin-seed");
        int sound0 = 0, sound1 = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            ChainResult result = s2.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
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
            ChainResult result = mixed.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
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
            ChainResult result = s3.Select(Ctx(Scenarios.XenoDomain, SqueakAction.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(seed));
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
                    ChainResult first = registry.Select(Ctx(domain, SqueakAction.Call), mode, SimGate.All, new LcgRandom(42));
                    ChainResult second = registry.Select(Ctx(domain, SqueakAction.Call), mode, SimGate.All, new LcgRandom(42));
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
            ChainResult result = s3.Select(Ctx(Scenarios.XenoDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(seed));
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
            ChainResult result = registry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(seed));
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
        ChainResult adult = s2.Select(new SelectionContext(Scenarios.RaceDomain, "Call", AgeBucket.Adult, true, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(7));
        ChainResult baby = s2.Select(new SelectionContext(Scenarios.RaceDomain, "Call", AgeBucket.Baby, true, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(7));
        Check(adult.SoundKey == baby.SoundKey && adult.Tier == baby.Tier, "age-neutral (all-age default)", ref failures);
    }

    private static void AgePriority(ref int failures)
    {
        VoicePackEntry exactAndAll = Entry("age.test:US_Age", Scenarios.RaceDomain,
            Variant("Call", "US_All", null),
            Variant("Call", "US_Baby", AgeBucket.Baby));
        SqueakPoolRegistry registry = new(new[] { exactAndAll }, BuiltInFallbackTable.Empty);
        ChainResult baby = registry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, AgeBucket.Baby), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        ChainResult child = registry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, AgeBucket.Child), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(baby.SoundKey == "US_Baby" && child.SoundKey == "US_All", "age exact variant wins; missing exact falls to all-age", ref failures);

        VoicePackEntry exactOnly = Entry("age.test:US_ExactOnly", Scenarios.RaceDomain, Variant("Call", "US_BabyOnly", AgeBucket.Baby));
        SqueakPoolRegistry noMatch = new(new[] { exactOnly }, BuiltInFallbackTable.Empty);
        ChainResult none = noMatch.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, AgeBucket.Child), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(none.IsNone, "age with neither exact nor all-age is not a candidate", ref failures);

        VoicePackEntry mutedExact = Entry("age.test:US_MutedExact", Scenarios.RaceDomain,
            Variant("Call", "US_AllPlayable", null),
            Variant("Call", "US_Baby_Muted", AgeBucket.Baby));
        SqueakPoolRegistry noCrossVariantFallback = new(new[] { mutedExact }, BuiltInFallbackTable.Empty);
        ChainResult muted = noCrossVariantFallback.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, AgeBucket.Baby), SelectionMode.Fallback, SimGate.Partial, new LcgRandom(1));
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
            ChainResult result = registry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, allowEggs: true), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.SoundKey == null) { Check(false, "same-pack variants enabled never none", ref failures); return; }
            enabled.Add(result.SoundKey);
        }
        Check(enabled.Contains("US_SamePack_Normal") && enabled.Contains("US_SamePack_Egg"),
            "same-pack normal+egg all-age variants both drawn when eggs enabled", ref failures);

        for (int seed = 1; seed <= 100; seed++)
        {
            ChainResult disabled = registry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
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
            ChainResult result = weightedRegistry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.SoundKey == "US_Weighted_Heavy") heavy++;
        }
        double ratio = heavy / 800.0;
        Check(ratio > 0.6 && ratio < 0.85, "variant ActionSoundSet.Weight drives weighted draw: " + ratio.ToString("0.000"), ref failures);
    }

    private static void PackFallbackTier(ref int failures)
    {
        VoicePackEntry raceFallback = Entry("fallback.test:US_Race", Scenarios.RaceDomain,
            fallback: new Dictionary<string, string> { ["Call"] = "US_PackFallback_Race" });
        SqueakPoolRegistry raceRegistry = new(new[] { raceFallback }, Scenarios.BuildBuiltIn());
        ChainResult race = raceRegistry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(race.Tier == ChainTier.PackFallback && race.SoundKey == "US_PackFallback_Race" && race.PoolStableKey == "fallback.test:US_Race", "pack fallback runs after empty race tier before built-in", ref failures);

        VoicePackEntry xenoFallback = Entry("fallback.test:US_Xeno", Scenarios.XenoDomain,
            fallback: new Dictionary<string, string> { ["Call"] = "US_PackFallback_Xeno" });
        VoicePackEntry otherRaceFallback = Entry("fallback.test:US_RaceOther", Scenarios.RaceDomain,
            fallback: new Dictionary<string, string> { ["Call"] = "US_PackFallback_RaceOther" });
        SqueakPoolRegistry xenoRegistry = new(new[] { xenoFallback, otherRaceFallback }, Scenarios.BuildBuiltIn());
        ChainResult xeno = xenoRegistry.Select(Ctx(Scenarios.XenoDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(xeno.Tier == ChainTier.PackFallback && xeno.SoundKey == "US_PackFallback_Xeno", "xeno context reads only its exact pool fallback", ref failures);

        SqueakPoolRegistry absent = new(Array.Empty<VoicePackEntry>(), Scenarios.BuildBuiltIn());
        ChainResult builtin = absent.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(builtin.Tier == ChainTier.BuiltInFallback, "absent pack fallback leaves tier empty", ref failures);

        HashSet<ChainTier?> tiers = new();
        for (int seed = 1; seed <= 80; seed++)
        {
            ChainResult result = xenoRegistry.Select(Ctx(Scenarios.XenoDomain, SqueakAction.Call), SelectionMode.Remix, SimGate.All, new LcgRandom(seed));
            tiers.Add(result.Tier);
        }
        Check(tiers.Contains(ChainTier.PackFallback) && tiers.Contains(ChainTier.BuiltInFallback), "remix includes pack fallback as a fourth tier", ref failures);
    }

    /// <summary>M3：PackFallback 只读精确 ctx.Domain 池；xeno context 不会读取同 race 的 race-pool fallback。</summary>
    private static void PackFallbackExactDomainOnly(ref int failures)
    {
        VoicePackEntry raceFallback = Entry("fallback.test:US_RaceOnly", Scenarios.RaceDomain,
            fallback: new Dictionary<string, string> { ["Call"] = "US_PackFallback_RaceOnly" });
        SqueakPoolRegistry registry = new(new[] { raceFallback }, Scenarios.BuildBuiltIn());

        ChainResult xeno = registry.Select(Ctx(Scenarios.XenoDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(xeno.Tier == ChainTier.BuiltInFallback && xeno.SoundKey == "US_Fallback_Call",
            "xeno context with no xeno pack fallback does NOT read race pack fallback", ref failures);

        ChainResult race = registry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(race.Tier == ChainTier.PackFallback && race.SoundKey == "US_PackFallback_RaceOnly" && race.PoolStableKey == "fallback.test:US_RaceOnly",
            "race context still reads its own exact pool pack fallback", ref failures);
    }

    private static void EggFiltering(ref int failures)
    {
        VoicePackEntry egg = Entry("egg.test:US_Egg", Scenarios.RaceDomain, Variant("Call", "US_EggOnly", null, true));
        SqueakPoolRegistry onlyEgg = new(new[] { egg }, BuiltInFallbackTable.Empty);
        ChainResult disabled = onlyEgg.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, AgeBucket.Adult, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        ChainResult enabled = onlyEgg.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, AgeBucket.Adult, true), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(disabled.IsNone && enabled.SoundKey == "US_EggOnly", "egg variant is excluded while disabled and admitted while enabled", ref failures);
        VoicePackEntry exactEggWithAllAge = Entry("egg.test:US_ExactEgg", Scenarios.RaceDomain,
            Variant("Call", "US_AllAgeNormal", null),
            Variant("Call", "US_BabyEgg", AgeBucket.Baby, true));
        SqueakPoolRegistry noEggAgeFallback = new(new[] { exactEggWithAllAge }, BuiltInFallbackTable.Empty);
        ChainResult exactDisabled = noEggAgeFallback.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, AgeBucket.Baby, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(exactDisabled.IsNone, "disabled exact egg does not fall through to all-age", ref failures);

        VoicePackEntry normal = Entry("egg.test:US_Normal", Scenarios.RaceDomain, Variant("Call", "US_Normal", null));
        SqueakPoolRegistry mixed = new(new[] { egg, normal }, BuiltInFallbackTable.Empty);
        int eggs = 0, normalCount = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            ChainResult result = mixed.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, AgeBucket.Adult, true), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
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
            ChainResult result = registry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
            if (result.SoundKey == "US_Heavy") heavyCount++;
        }
        double ratio = heavyCount / 800.0;
        Check(ratio > 0.65 && ratio < 0.85, "pack weight uses cumulative weighted draw: " + ratio.ToString("0.000"), ref failures);

        VoicePackEntry invalid = Entry("weight.test:US_Invalid", Scenarios.RaceDomain, 0f, Variant("Call", "US_Invalid", null));
        SqueakPoolRegistry rejectsInvalid = new(new[] { invalid }, BuiltInFallbackTable.Empty);
        ChainResult none = rejectsInvalid.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(none.IsNone, "nonpositive direct pack weight is rejected", ref failures);
    }

    private static void SelectInvalidGuards(ref int failures)
    {
        SqueakPoolRegistry registry = Scenarios.BuildRegistry("S2-builtin-seed");
        ChainResult nullKey = registry.Select(new SelectionContext(Scenarios.RaceDomain, null!, AgeBucket.Adult, false, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(nullKey.IsNone, "null ActionKey returns None", ref failures);
        ChainResult whitespaceKey = registry.Select(new SelectionContext(Scenarios.RaceDomain, "   ", AgeBucket.Adult, false, false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(whitespaceKey.IsNone, "whitespace ActionKey returns None", ref failures);
        ChainResult unknownMode = registry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call), (SelectionMode)999, SimGate.All, new LcgRandom(1));
        Check(unknownMode.IsNone, "unknown SelectionMode returns None instead of falling into Remix", ref failures);
    }

    /// <summary>漏斗纯逻辑：SqueakTimingModel 纯求值语义逐条断言。数学与旧内嵌实现逐字节等价。</summary>
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

    /// <summary>漏斗纯逻辑：SqueakTriggerInvocation 语义（非周期跳过 RandomOneShot 概率）。
    /// 身份门控：PlayerSelection/ActiveCommand 派生 IsPlayerInitiated；PlayerSelection 派生 RequiresResponsivePawn。</summary>
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
            "invocation Crying/Giggling origins are external", ref failures);
        Check(!crying.IsPlayerInitiated && !giggling.IsPlayerInitiated,
            "invocation Crying/Giggling origins are not player initiated", ref failures);

        SqueakTriggerInvocation equip = new(SqueakTriggerOrigin.Equip, SqueakInvocationSource.ActiveCommand);
        Check(equip.IsPlayerInitiated && !equip.RequiresResponsivePawn,
            "invocation equip active command is player initiated without responsiveness requirement", ref failures);
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
                ChainResult result = registry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, allowEggs: false), mode, SimGate.All, new LcgRandom(seed));
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
            ChainResult off = registry.Select(Ctx(Scenarios.RaceBDomain, SqueakAction.Call, allowEggs: false), SelectionMode.Off, SimGate.All, new LcgRandom(seed));
            if (off.Tier != ChainTier.BuiltInFallback || off.SoundKey != "US_Fallback_Call")
            {
                Check(false, "per-race RaceB Off must resolve its own built-in profile", ref failures);
                return;
            }
            ChainResult fallback = registry.Select(Ctx(Scenarios.RaceBDomain, SqueakAction.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(seed));
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
        ChainResult unknownOff = raceAOnly.Select(Ctx(Scenarios.RaceBDomain, SqueakAction.Call, allowEggs: false), SelectionMode.Off, SimGate.All, new LcgRandom(1));
        ChainResult noLeak = raceAOnly.Select(Ctx(Scenarios.RaceBDomain, SqueakAction.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(1));
        Check(unknownOff.IsNone && noLeak.IsNone, "per-race RaceB query never falls through to RaceA pool or RaceA built-in", ref failures);

        // xeno 子域隔离：RaceA+Xeno 池只在 RaceA+Xeno 域可见；RaceB+Xeno 查询不得命中。
        AudioDomain raceAXeno = new(Scenarios.RaceA, Scenarios.XenoA);
        AudioDomain raceBXeno = new(Scenarios.RaceB, Scenarios.XenoA);
        SqueakPoolRegistry xenoIsolated = new(new[]
        {
            ScenariosEntry("us.test:US_RaceA_Xeno", raceAXeno, sounds: 1),
            ScenariosEntry("us.test:US_RaceB_Xeno", raceBXeno, sounds: 1),
        }, Scenarios.BuildBuiltIn());
        ChainResult bx = xenoIsolated.Select(Ctx(raceBXeno, SqueakAction.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(2));
        Check(bx.PoolStableKey == "us.test:US_RaceB_Xeno", "per-race RaceB+xeno resolves its exact pool", ref failures);
        ChainResult ax = xenoIsolated.Select(Ctx(raceAXeno, SqueakAction.Call, allowEggs: false), SelectionMode.Fallback, SimGate.All, new LcgRandom(2));
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
                ChainResult a = registry.Select(Ctx(Scenarios.RaceDomain, SqueakAction.Call, allowEggs: false), mode, SimGate.All, new LcgRandom(seed));
                ChainResult b = registry.Select(Ctx(Scenarios.RaceBDomain, SqueakAction.Call, allowEggs: false), mode, SimGate.All, new LcgRandom(seed));
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

    /// <summary>双键域工具 + Pure 聚合器 + context 选择器：同 xeno 多 race 不串音、缺失/ambiguous/空 race 回退。</summary>
    private static void XenoDoubleKeyDomains(ref int failures)
    {
        AudioDomain ratkinX = new(new RaceKey("Ratkin"), new XenotypeKey("X"));
        AudioDomain felineX = new(new RaceKey("Feline"), new XenotypeKey("X"));
        AudioDomain ratkinOnly = new(new RaceKey("Ratkin"), null);

        // AudioDomains：构造/去重/排序。
        Check(AudioDomains.TryCreate("Ratkin", "X", out AudioDomain created) && created == ratkinX,
            "double-key: AudioDomains.TryCreate builds exact AudioDomain", ref failures);
        Check(!AudioDomains.TryCreate("", "X", out _) && !AudioDomains.TryCreate(null, "X", out _),
            "double-key: empty race rejected", ref failures);
        var collected = AudioDomains.Collect(new[] { ("Ratkin", "X"), ("Feline", "X"), ("Ratkin", "X"), ("Ratkin", (string?)null) });
        Check(collected.Count == 3 && collected[0] == felineX && collected[1] == ratkinOnly && collected[2] == ratkinX,
            "double-key: AudioDomains.Collect dedupes and sorts deterministically", ref failures);

        // 聚合器：同 xeno 不同 race 各自独立；同域后写覆盖。
        IReadOnlyDictionary<AudioDomain, LayerBehaviorAggregate> table = SqueakTuningAggregator.Aggregate(
            new[]
            {
                (ratkinX, "Call", new LayerActionDelta(SqueakActionScope.Disabled, true, false, 1f, false, 1f)),
                (felineX, "Call", new LayerActionDelta(SqueakActionScope.ActiveCommand, true, false, 1f, false, 1f)),
                (ratkinX, "Call", new LayerActionDelta(SqueakActionScope.AnyOccurrence, true, false, 1f, false, 1f)),
            },
            Array.Empty<(AudioDomain, SqueakMood, LayerMoodDelta)>(),
            Array.Empty<(AudioDomain, float)>());
        Check(table[ratkinX].Actions["Call"].Scope == SqueakActionScope.AnyOccurrence
            && table[felineX].Actions["Call"].Scope == SqueakActionScope.ActiveCommand,
            "double-key: aggregator keeps same xeno per-race independent and same-domain last-wins", ref failures);

        IReadOnlyDictionary<AudioDomain, LayerBehaviorAggregate> multipliers = SqueakTuningAggregator.Aggregate(
            Array.Empty<(AudioDomain, string, LayerActionDelta)>(),
            Array.Empty<(AudioDomain, SqueakMood, LayerMoodDelta)>(),
            new[] { (ratkinX, 2f), (felineX, 3f) });
        Check(Math.Abs(multipliers[ratkinX].OverallIntervalMultiplier - 2f) < 0.0001f
            && Math.Abs(multipliers[felineX].OverallIntervalMultiplier - 3f) < 0.0001f,
            "double-key: overall interval multiplier is per (race,xeno) domain", ref failures);

        IReadOnlyDictionary<AudioDomain, LayerBehaviorAggregate> sanitized = SqueakTuningAggregator.Aggregate(
            Array.Empty<(AudioDomain, string, LayerActionDelta)>(),
            Array.Empty<(AudioDomain, SqueakMood, LayerMoodDelta)>(),
            new[] { (ratkinX, float.NaN), (felineX, float.PositiveInfinity), (ratkinOnly, -2f) });
        Check(Math.Abs(sanitized[ratkinX].OverallIntervalMultiplier - 1f) < 0.0001f
            && Math.Abs(sanitized[felineX].OverallIntervalMultiplier - 1f) < 0.0001f
            && Math.Abs(sanitized[ratkinOnly].OverallIntervalMultiplier - 0f) < 0.0001f,
            "double-key: aggregator sanitizes NaN/Infinity to 1 and negative to 0", ref failures);

        // 选择器：命中精确 (race,xeno)。
        ContextSelection hit = SqueakContextSelector.Select("Ratkin", "X", new[] { ratkinX, felineX }, Array.Empty<string>(), true);
        Check(hit.Kind == ContextSelectionKind.Xeno && hit.XenoDomain == ratkinX,
            "double-key: selector hits exact (race,xeno) domain", ref failures);

        // 选择器：同 xeno 但 race 不匹配 → 回退 Race，绝不跨 race。
        ContextSelection wrongRace = SqueakContextSelector.Select("Human", "X", new[] { ratkinX, felineX }, Array.Empty<string>(), true);
        Check(wrongRace.Kind == ContextSelectionKind.Race,
            "double-key: selector falls back to Race when (race,xeno) absent; never crosses race", ref failures);

        // 选择器：无 raceContext → Global。
        ContextSelection noRace = SqueakContextSelector.Select("Human", "X", new[] { ratkinX }, Array.Empty<string>(), false);
        Check(noRace.Kind == ContextSelectionKind.Global,
            "double-key: selector falls back to Global when no race context", ref failures);

        // 选择器：ambiguous xeno → Global（fail-closed）。
        ContextSelection ambiguous = SqueakContextSelector.Select("Ratkin", "X", new[] { ratkinX }, new[] { "X" }, true);
        Check(ambiguous.Kind == ContextSelectionKind.Global,
            "double-key: selector fails closed on ambiguous xenotype", ref failures);

        // 选择器：无 xeno / 空 race 分支。
        ContextSelection noXeno = SqueakContextSelector.Select("Ratkin", null, new[] { ratkinX }, Array.Empty<string>(), true);
        Check(noXeno.Kind == ContextSelectionKind.Race, "double-key: no xeno uses Race", ref failures);
        ContextSelection emptyRace = SqueakContextSelector.Select("", "X", new[] { ratkinX }, Array.Empty<string>(), true);
        Check(emptyRace.Kind == ContextSelectionKind.Global, "double-key: empty race uses Global", ref failures);
    }

    // ---- helpers ----

    private static SelectionContext Ctx(AudioDomain domain, SqueakAction action, AgeBucket age = AgeBucket.Adult, bool allowEggs = false)
        => new(domain, ActionKey.For(action)!, age, false, allowEggs);

    private static SqueakActionDefinition TestDefinition()
        => new(SqueakAction.Call, "US.Action.Call", "US_Call", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence);

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
        for (int i = 0; i < ActionKeyMirror.Count; i++)
        {
            SqueakAction action = (SqueakAction)i;
            string actionName = ActionKeyMirror.For(action);
            List<string> keys = new(sounds);
            for (int s = 0; s < sounds; s++) keys.Add(packKey + "_" + actionName + "_" + s);
            actions[ActionKey.For(action)!] = new[] { new ActionSoundSet(keys, null, 1f) };
        }
        return new VoicePackEntry(packKey, domain, 1f, actions);
    }

    private static VoicePackEntry MutedEntry(string packKey, AudioDomain domain)
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        for (int i = 0; i < ActionKeyMirror.Count; i++)
        {
            SqueakAction action = (SqueakAction)i;
            string actionName = ActionKeyMirror.For(action);
            actions[ActionKey.For(action)!] = new[] { new ActionSoundSet(new[] { packKey + "_" + actionName + "_Muted" }, null, 1f) };
        }
        return new VoicePackEntry(packKey, domain, 1f, actions);
    }
}
