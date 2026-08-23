using System;
using System.Collections.Generic;
using System.IO;
using SqueakyRatkin.Kernel;

namespace SqueakyRatkin.KernelCharacterization;

/// <summary>
/// 语料场景（0.3.0 固化，0.3.1 切核回放时不得修改场景构造——只更新 Kernel 链接）。
/// 场景 = 池内容 + 内置表，对应设置全项矩阵的「选择状态 × Biotech」维度（§6.2 场景语义）。
/// S1 无选择（空池）；S2 仅内置种子（Race 池 1 条目）；S3 内置+Xeno（Race 池 + (Ratkin,XenoA) 池）；
/// S4 orphan（xeno 域选择存在但池空——域未装配）；S5 dormant（xeno 域存在但 Biotech 关——域不注入）。
/// 每个场景的域注入面：S3 有 (Ratkin,XenoA)；S4/S5 无（适配层不注入）。
/// </summary>
public static class Scenarios
{
    public static readonly RaceKey Ratkin = new("Ratkin");
    public static readonly RaceKey Kiiro = new("Kiiro");
    public static readonly XenotypeKey XenoA = new("XenoA");

    /// <summary>内置表种子：本 harness 的 15 项 AudioKey 镜像投影（Crying/Giggling 无内置 SoundDef，不播种）。
    /// 内核键空间保留 17 个 built-in key，但内置表只列 15 个映射（§4.7 默认静默）。</summary>
    public static BuiltInFallbackTable BuildBuiltIn()
    {
        Dictionary<string, string> keys = new(StringComparer.Ordinal);
        for (int i = 0; i < ActionAudioKeyMirror.BuiltInCount; i++)
        {
            SqueakyRatkin.SqueakAction action = (SqueakyRatkin.SqueakAction)i;
            keys[ActionKey.For(action)!] = ActionAudioKeyMirror.For(action);
        }
        return new BuiltInFallbackTable(new[] { new FallbackProfile(Ratkin, 1, keys) });
    }

    public static string[] ScenarioNames { get; } = { "S1-empty", "S2-builtin-seed", "S3-builtin-plus-xeno", "S4-orphan-xeno", "S5-dormant-xeno" };

    /// <summary>0.3.1 波 4a 扩展场景（语料 17 动作矩阵重建 + 彩蛋维度 + per-race 池隔离）：
    /// S6-egg（彩蛋开关维度：加性池成员）；S7-two-races（Ratkin/Kiiro 两 race 池互不串扰）。
    /// 0.3.0 冻结语料（corpus-0.3.0.txt）只回放 <see cref="ScenarioNames"/> + Fixture 场景，构造不得改动。</summary>
    public static string[] ExtendedScenarioNames { get; } = { "S1-empty", "S2-builtin-seed", "S3-builtin-plus-xeno", "S4-orphan-xeno", "S5-dormant-xeno", "S6-egg", "S7-two-races" };

    /// <summary>fixture 驱动场景（§5 步骤 6：0.2.4 真实设置 fixture 驱动语料）：输入 = fixtures/input/*.xml 的 selection 状态。
    /// 映射：F03→03-explicit-off（无选择）；F04→04-fallback-seeded；F05→05-multi-selections-lastwins（同域 last-wins）；
    /// F06→06-orphan-packkey（key 无 pack 定义 → 池空）；F07→07-biotech-inactive-target（xeno 选择域无池）。
    /// pack 声音内容为构造（fixture 无音频数据）；selection 状态与 last-wins 语义 = 真实解析。</summary>
    public static string[] FixtureScenarioNames { get; } = { "F03-explicit-off", "F04-fallback-seeded", "F05-multi-lastwins", "F06-orphan", "F07-inactive" };

    public static string FixtureInputFile(string scenario) => scenario switch
    {
        "F03-explicit-off" => "03-explicit-off.xml",
        "F04-fallback-seeded" => "04-fallback-seeded.xml",
        "F05-multi-lastwins" => "05-multi-selections-lastwins.xml",
        "F06-orphan" => "06-orphan-packkey.xml",
        "F07-inactive" => "07-biotech-inactive-target.xml",
        _ => throw new ArgumentException("Unknown fixture scenario: " + scenario),
    };

    /// <summary>fixture 06 的消失 PackKey（与 fixtures 生成器 MissingPackKey 同值）；orphan 语义 =
    /// selection 存在但 pack 缺失 → 域池空（不构造条目），Select 回退下级 tier。</summary>
    public const string OrphanPackKey = "coahuilite.squeakyratkin:SR_GonePack_9999";

    /// <summary>fixture 驱动注册表：解析 selection（last-wins，与 BuildSelections 同语义）→ 域池投影。
    /// 2b-2：域键 = AudioDomain（场景种族 + 目标），不再镜像旧字符串域键桥。
    /// 未知 pack key（无 pack 定义）不构造条目 = orphan（域注入面由 catalog 决定，语料以选择域为准）。</summary>
    public static SqueakPoolRegistry BuildFixtureRegistry(string scenario, string fixturesRoot)
    {
        string path = Path.Combine(fixturesRoot, "input", FixtureInputFile(scenario));
        if (!File.Exists(path)) throw new FileNotFoundException("fixture input missing: " + path);

        System.Xml.XmlDocument doc = new();
        doc.Load(path);
        Dictionary<AudioDomain, HashSet<string>> selections = new();
        System.Xml.XmlNodeList? nodes = doc.SelectNodes("//voicePackSelections/li");
        if (nodes != null)
        {
            foreach (System.Xml.XmlNode li in nodes)
            {
                System.Xml.XmlNode? scopeNode = li["scope"];
                string scope = scopeNode?.InnerText ?? "";
                if (scope != "Race" && scope != "Xenotype") continue;
                // v1/v3 fixture 记录：targetDefName 是 load-only 迁移源；域身份 = (Ratkin, 目标)。
                string target = scope == "Race" ? "" : (li["targetDefName"]?.InnerText ?? "");
                if (scope == "Xenotype" && target.Length == 0) continue;
                HashSet<string> keys = new(StringComparer.Ordinal);
                System.Xml.XmlNode? keysNode = li["enabledPackKeys"];
                if (keysNode != null)
                {
                    foreach (System.Xml.XmlNode key in keysNode.ChildNodes)
                    {
                        if (key.NodeType == System.Xml.XmlNodeType.Element && !string.IsNullOrEmpty(key.InnerText)) keys.Add(key.InnerText);
                    }
                }
                AudioDomain domain = scope == "Race"
                    ? new AudioDomain(Ratkin, null)
                    : new AudioDomain(Ratkin, new XenotypeKey(target));
                selections[domain] = keys;
            }
        }

        List<VoicePackEntry> entries = new();
        AddFixtureDomain(entries, selections, RaceDomain);
        // Biotech 注入面：F07 = dormant（Biotech 关，xeno 域不注入 → 查询该域回退）；其余场景注入全部 xeno 选择域。
        if (scenario != "F07-inactive")
        {
            foreach (KeyValuePair<AudioDomain, HashSet<string>> pair in selections)
            {
                if (pair.Key.Xenotype == null) continue;
                AddFixtureDomain(entries, selections, pair.Key);
            }
        }
        return new SqueakPoolRegistry(entries, BuildBuiltIn(), DomainFilter.Everything);
    }

    public static AudioDomain[] DomainsForFixture(string scenario)
    {
        List<AudioDomain> domains = new() { RaceDomain };
        if (scenario == "F05-multi-lastwins")
        {
            domains.Add(new AudioDomain(Ratkin, new XenotypeKey("Baseliner_Highmate")));
            domains.Add(new AudioDomain(Ratkin, new XenotypeKey("Sanguophage")));
        }
        else if (scenario == "F07-inactive")
        {
            domains.Add(new AudioDomain(Ratkin, new XenotypeKey("InactiveXeno_NotLoaded")));
        }
        return domains.ToArray();
    }

    private static void AddFixtureDomain(List<VoicePackEntry> entries, IReadOnlyDictionary<AudioDomain, HashSet<string>> selections, AudioDomain domain)
    {
        if (!selections.TryGetValue(domain, out HashSet<string>? keys)) return;
        foreach (string packKey in keys)
        {
            // orphan：pack 缺失（无 pack 定义）→ 不构造条目，域池保持空。
            if (packKey == OrphanPackKey) continue;
            entries.Add(EntryFor(packKey, domain));
        }
    }

    /// <summary>pack 定义构造（fixture 无音频数据）：声音 key = packKey + "_" + AudioKey + "_" + s。</summary>
    private static VoicePackEntry EntryFor(string packKey, AudioDomain domain)
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        for (int i = 0; i < ActionAudioKeyMirror.Count; i++)
        {
            SqueakyRatkin.SqueakAction action = (SqueakyRatkin.SqueakAction)i;
            string audioKey = ActionAudioKeyMirror.For(action);
            actions[ActionKeyFor(action)] = new[] { new ActionSoundSet(new[] { packKey + "_" + audioKey + "_0" }, null, 1f) };
        }
        return new VoicePackEntry(packKey, domain, 1f, actions);
    }

    /// <summary>场景 → 注册表（语料与回放共用同一构造）。</summary>
    public static SqueakPoolRegistry BuildRegistry(string scenario)
    {
        BuiltInFallbackTable builtIn = BuildBuiltIn();
        switch (scenario)
        {
            case "S1-empty":
                return new SqueakPoolRegistry(Array.Empty<VoicePackEntry>(), builtIn, DomainFilter.Everything);
            case "S2-builtin-seed":
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry("coahuilite.squeakyratkin:SR_OfficialExample_Race", 2),
                }, builtIn, DomainFilter.Everything);
            case "S3-builtin-plus-xeno":
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry("coahuilite.squeakyratkin:SR_OfficialExample_Race", 2),
                    XenoEntry("other.mod:SR_XenoAVoice", 2),
                }, builtIn, DomainFilter.Everything);
            case "S4-orphan-xeno":
                // xeno 域选择存在但未装配：域池不注入 = 无 (Ratkin,XenoA) 池。
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry("coahuilite.squeakyratkin:SR_OfficialExample_Race", 2),
                }, builtIn, DomainFilter.Everything);
            case "S5-dormant-xeno":
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry("coahuilite.squeakyratkin:SR_OfficialExample_Race", 2),
                }, builtIn, DomainFilter.Everything);
            case "S6-egg":
                // 彩蛋维度：EggPack 的 Call 有普通+彩蛋变体（开关开 = 加性池成员），Move 只有彩蛋变体
                // （开关关 = 该 pack 无 Move 候选 → 其他 pack/内置兜底）。开关作为 SelectionContext.AllowEggs 路由输入。
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry("coahuilite.squeakyratkin:SR_OfficialExample_Race", 2),
                    EggRaceEntry(),
                }, builtIn, DomainFilter.Everything);
            case "S7-two-races":
                // per-race 池隔离：Ratkin + Kiiro 两 race 池；内置表只含 Ratkin（Kiiro Off = 静默，
                // Kiiro 查询绝不落入 Ratkin 池或 Ratkin 内置）。
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry("coahuilite.squeakyratkin:SR_OfficialExample_Race", 2),
                    RaceEntry("kiiro.mod:SR_KiiroVoice", 2, KiiroDomain),
                }, builtIn, DomainFilter.Everything);
            default:
                throw new ArgumentException("Unknown scenario: " + scenario);
        }
    }

    public static AudioDomain RaceDomain => new(Ratkin, null);
    public static AudioDomain KiiroDomain => new(Kiiro, null);
    public static AudioDomain XenoDomain => new(Ratkin, XenoA);

    public static AudioDomain[] DomainsFor(string scenario)
    {
        // S3 的域注入面含 (Ratkin,XenoA)；S7 含 Kiiro race 域；其余场景只有 (Ratkin,null)。
        switch (scenario)
        {
            case "S3-builtin-plus-xeno":
                return new[] { RaceDomain, XenoDomain };
            case "S7-two-races":
                return new[] { RaceDomain, KiiroDomain };
            default:
                return new[] { RaceDomain };
        }
    }

    private static VoicePackEntry RaceEntry(string packKey, int soundCount)
    {
        return Entry(packKey, RaceDomain, soundCount, muteLast: false);
    }

    private static VoicePackEntry RaceEntry(string packKey, int soundCount, AudioDomain domain)
    {
        return Entry(packKey, domain, soundCount, muteLast: false);
    }

    private static VoicePackEntry XenoEntry(string packKey, int soundCount)
    {
        return Entry(packKey, XenoDomain, soundCount, muteLast: true);
    }

    /// <summary>彩蛋 pack（§2.4 语义）：Call = 普通 + 彩蛋变体（同权混抽）；Move = 仅彩蛋变体；
    /// 其余动作 = 普通变体。条目级 IsEgg 由 SelectionContext.AllowEggs 过滤。</summary>
    private static VoicePackEntry EggRaceEntry()
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        for (int i = 0; i < ActionAudioKeyMirror.Count; i++)
        {
            SqueakyRatkin.SqueakAction action = (SqueakyRatkin.SqueakAction)i;
            string audioKey = ActionAudioKeyMirror.For(action);
            List<ActionSoundSet> sets;
            if (action == SqueakyRatkin.SqueakAction.Call)
            {
                sets = new List<ActionSoundSet>
                {
                    new(new[] { "egg.mod:SR_EggPack_" + audioKey + "_0" }, null, 1f),
                    new(new[] { "egg.mod:SR_EggPack_" + audioKey + "_Egg" }, null, 1f, isEgg: true),
                };
            }
            else if (action == SqueakyRatkin.SqueakAction.Move)
            {
                sets = new List<ActionSoundSet>
                {
                    new(new[] { "egg.mod:SR_EggPack_" + audioKey + "_Egg" }, null, 1f, isEgg: true),
                };
            }
            else
            {
                sets = new List<ActionSoundSet>
                {
                    new(new[] { "egg.mod:SR_EggPack_" + audioKey + "_0" }, null, 1f),
                };
            }
            actions[ActionKeyFor(action)] = sets;
        }
        return new VoicePackEntry("egg.mod:SR_EggPack", RaceDomain, 1f, actions);
    }

    private static VoicePackEntry Entry(string packKey, AudioDomain domain, int soundCount, bool muteLast)
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        for (int i = 0; i < ActionAudioKeyMirror.Count; i++)
        {
            SqueakyRatkin.SqueakAction action = (SqueakyRatkin.SqueakAction)i;
            string audioKey = ActionAudioKeyMirror.For(action);
            List<string> sounds = new(soundCount);
            for (int s = 0; s < soundCount; s++)
            {
                string key = packKey + "_" + audioKey + "_" + s;
                if (muteLast && s == soundCount - 1) key += "_Muted";
                sounds.Add(key);
            }
            actions[ActionKeyFor(action)] = new[] { new ActionSoundSet(sounds, null, 1f) };
        }
        return new VoicePackEntry(packKey, domain, 1f, actions);
    }

    private static string ActionKeyFor(SqueakyRatkin.SqueakAction action) => ActionKey.For(action)!;
}
