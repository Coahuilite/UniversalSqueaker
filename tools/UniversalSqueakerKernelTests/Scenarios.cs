using System;
using System.Collections.Generic;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker.KernelTests;

/// <summary>
/// US 0.1.0 语料场景。S1 无选择（空池）；S2 仅内置种子（RaceA 池 1 条目）；S3 内置+Xeno（RaceA 池 + (RaceA,XenoA) 池）；
/// S4 orphan（xeno 域选择存在但池空——域未装配）；S5 dormant（xeno 域存在但 Biotech 关——域不注入）；
/// S6 egg（彩蛋开关维度）；S7 two-races（RaceA/RaceB 两族构造相同 pool 结构，平等路由 + 池隔离）。
/// 无任何种族特判：两族数据对称注入，断言同输入仅换 race 结果结构一致、池零交叉。
/// </summary>
public static class Scenarios
{
    public static readonly RaceKey RaceA = new("US_Race_A");
    public static readonly RaceKey RaceB = new("US_Race_B");
    public static readonly XenotypeKey XenoA = new("US_Xeno_A");

    public const string RacePackA = "us.test:US_Pack_RaceA";
    public const string RacePackB = "us.test:US_Pack_RaceB";
    public const string XenoPack = "us.test:US_Pack_XenoA";
    public const string EggPack = "us.test:US_EggPack";

    /// <summary>内置表测试种子：RaceA/RaceB 各注入完全相同的 15 项中性映射（数据注入的对称性）。
    /// 内核本身不携带任何产品种子。</summary>
    public static BuiltInFallbackTable BuildBuiltIn()
    {
        Dictionary<string, string> keys = new(StringComparer.Ordinal);
        for (int i = 0; i < ActionKeyMirror.BuiltInCount; i++)
        {
            SqueakAction action = (SqueakAction)i;
            keys[ActionKey.For(action)!] = ActionKeyMirror.BuiltInFallbackKey(action);
        }
        return new BuiltInFallbackTable(new[]
        {
            new FallbackProfile(RaceA, 1, keys),
            new FallbackProfile(RaceB, 1, keys),
        });
    }

    public static string[] ScenarioNames { get; } =
    {
        "S1-empty",
        "S2-builtin-seed",
        "S3-builtin-plus-xeno",
        "S4-orphan-xeno",
        "S5-dormant-xeno",
        "S6-egg",
        "S7-two-races",
    };

    public static SqueakPoolRegistry BuildRegistry(string scenario)
    {
        BuiltInFallbackTable builtIn = BuildBuiltIn();
        switch (scenario)
        {
            case "S1-empty":
                return new SqueakPoolRegistry(Array.Empty<VoicePackEntry>(), builtIn);
            case "S2-builtin-seed":
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry(RacePackA, 2),
                }, builtIn);
            case "S3-builtin-plus-xeno":
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry(RacePackA, 2),
                    XenoEntry(XenoPack, 2),
                }, builtIn);
            case "S4-orphan-xeno":
                // xeno 域选择存在但未装配：域池不注入 = 无 (RaceA,XenoA) 池。
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry(RacePackA, 2),
                }, builtIn);
            case "S5-dormant-xeno":
                // Biotech 关闭：xeno 域由适配层不注入，内核侧与 S4 同形。
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry(RacePackA, 2),
                }, builtIn);
            case "S6-egg":
                // 彩蛋维度：EggPack 的 Call 有普通+彩蛋变体（开关开 = 加性池成员），Move 只有彩蛋变体
                // （开关关 = 该 pack 无 Move 候选 → 其他 pack/内置兜底）。开关作为 SelectionContext.AllowEggs 路由输入。
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry(RacePackA, 2),
                    EggRaceEntry(),
                }, builtIn);
            case "S7-two-races":
                // per-race 池隔离：RaceA + RaceB 两 race 池，构造相同 pool 结构；内置表对两族同形。
                return new SqueakPoolRegistry(new[]
                {
                    RaceEntry(RacePackA, 2),
                    RaceEntry(RacePackB, 2, RaceBDomain),
                }, builtIn);
            default:
                throw new ArgumentException("Unknown scenario: " + scenario);
        }
    }

    public static AudioDomain RaceDomain => new(RaceA, null);
    public static AudioDomain RaceBDomain => new(RaceB, null);
    public static AudioDomain XenoDomain => new(RaceA, XenoA);

    public static AudioDomain[] DomainsFor(string scenario)
    {
        // S3 的域注入面含 (RaceA,XenoA)；S4 的 orphan 语义需要同时查询 RaceDomain 与 XenoDomain，
        // 以覆盖“xeno 域选择存在但池空 → RacePack/BuiltIn 回退”；S7 含 RaceB race 域。
        // S5 dormant 表示适配层不注入也不查询 xeno 域，因此只保留 RaceDomain；把 XenoDomain 加入会
        // 把它误标成可查询状态并与 S4 重复（S4/S5 的 BuildRegistry 虽同形，但语义角色不同）。
        switch (scenario)
        {
            case "S3-builtin-plus-xeno":
                return new[] { RaceDomain, XenoDomain };
            case "S4-orphan-xeno":
                return new[] { RaceDomain, XenoDomain };
            case "S7-two-races":
                return new[] { RaceDomain, RaceBDomain };
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

    /// <summary>彩蛋 pack：Call = 普通 + 彩蛋变体（同权混抽）；Move = 仅彩蛋变体；
    /// 其余动作 = 普通变体。条目级 IsEgg 由 SelectionContext.AllowEggs 过滤。
    /// 条目 Weight 设为 4，使 S6 语料在固定 3 个 seed 下确定采样到 EggPack（M2 要求）。</summary>
    private static VoicePackEntry EggRaceEntry()
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        for (int i = 0; i < ActionKeyMirror.Count; i++)
        {
            SqueakAction action = (SqueakAction)i;
            string actionName = ActionKeyMirror.For(action);
            List<ActionSoundSet> sets;
            if (action == SqueakAction.Call)
            {
                sets = new List<ActionSoundSet>
                {
                    new(new[] { EggPack + "_" + actionName + "_0" }, null, 1f),
                    new(new[] { EggPack + "_" + actionName + "_Egg" }, null, 1f, isEgg: true),
                };
            }
            else if (action == SqueakAction.Move)
            {
                sets = new List<ActionSoundSet>
                {
                    new(new[] { EggPack + "_" + actionName + "_Egg" }, null, 1f, isEgg: true),
                };
            }
            else
            {
                sets = new List<ActionSoundSet>
                {
                    new(new[] { EggPack + "_" + actionName + "_0" }, null, 1f),
                };
            }
            actions[ActionKeyFor(action)] = sets;
        }
        return new VoicePackEntry(EggPack, RaceDomain, 4f, actions);
    }

    private static VoicePackEntry Entry(string packKey, AudioDomain domain, int soundCount, bool muteLast)
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        for (int i = 0; i < ActionKeyMirror.Count; i++)
        {
            SqueakAction action = (SqueakAction)i;
            string actionName = ActionKeyMirror.For(action);
            List<string> sounds = new(soundCount);
            for (int s = 0; s < soundCount; s++)
            {
                string key = packKey + "_" + actionName + "_" + s;
                if (muteLast && s == soundCount - 1) key += "_Muted";
                sounds.Add(key);
            }
            actions[ActionKeyFor(action)] = new[] { new ActionSoundSet(sounds, null, 1f) };
        }
        return new VoicePackEntry(packKey, domain, 1f, actions);
    }

    private static string ActionKeyFor(SqueakAction action) => ActionKey.For(action)!;
}
