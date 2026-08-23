using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker.KernelTests;

/// <summary>
/// US 内核验证门：
/// 1) 单测：语义规范逐条断言（UnitTests）。
/// 2) 正常运行：设置全项矩阵（场景 × mode × 域 × 17 action × 彩蛋开关 × 多种子 × gate 面）生成确定性
///    输入→期望 ChainResult 字节，并在写入前与提交的 US 0.1.0 冻结基线比较。
///    只有显式 <c>--update-corpus</c> 维护模式可以重建该基线。
/// 3) 确定性重放：生成两遍必须字节相同。
/// 基线是 US 自己的语料（S1-S7 构造场景），不依赖任何历史内容快照。
/// </summary>
internal static class Program
{
    private const string CorpusFileName = "us-corpus-0.1.0.txt";
    private const string UpdateCorpusArgument = "--update-corpus";
    private static readonly long[] Seeds = { 1, 2, 3 };
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private static int Main(string[] args)
    {
        if (args.Length > 1 || (args.Length == 1 && !string.Equals(args[0], UpdateCorpusArgument, StringComparison.Ordinal)))
        {
            Console.Error.WriteLine("Usage: UniversalSqueakerKernelTests [--update-corpus]");
            return 2;
        }
        try
        {
            return Run(args.Length == 1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("UNHANDLED: " + ex.GetType().FullName + " :: " + ex.Message);
            return 1;
        }
    }

    private static int Run(bool updateCorpus)
    {
        int failures = 0;
        Console.WriteLine("Unit tests...");
        UnitTests.RunAll(ref failures);

        string corpusDir = Path.Combine(FindRepositoryRoot(), "tools", "UniversalSqueakerKernelTests", "fixtures", "corpus");
        string corpusPath = Path.Combine(corpusDir, CorpusFileName);

        Console.WriteLine("Golden corpus generation (17 actions + egg dimension + two-race)...");
        string corpus;
        try
        {
            corpus = GenerateCorpus();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  corpus generation exception: " + ex.GetType().FullName + " :: " + ex.Message);
            return 1;
        }
        byte[] generated = Utf8WithoutBom.GetBytes(corpus);

        Console.WriteLine(updateCorpus ? "Golden corpus update..." : "Golden corpus replay...");
        if (updateCorpus)
        {
            Directory.CreateDirectory(corpusDir);
            File.WriteAllBytes(corpusPath, generated);
            Console.WriteLine("  corpus updated: " + corpusPath + " (" + CountLines(corpus) + " cases)");
        }
        else if (!File.Exists(corpusPath))
        {
            Console.Error.WriteLine("  FAIL: committed corpus missing: " + corpusPath);
            return 1;
        }

        byte[] baseline = File.ReadAllBytes(corpusPath);
        if (BytesEqual(generated, baseline))
        {
            Console.WriteLine(updateCorpus
                ? "  ok: updated corpus byte-stable"
                : "  ok: committed corpus replay zero delta (byte-identical)");
        }
        else
        {
            Console.Error.WriteLine("  FAIL: corpus delta detected (generated " + generated.Length + " bytes, baseline " + baseline.Length + " bytes)");
            failures++;
        }

        Console.WriteLine("Replay determinism check...");
        byte[] replay = Utf8WithoutBom.GetBytes(GenerateCorpus());
        if (BytesEqual(generated, replay))
        {
            Console.WriteLine("  ok: deterministic replay zero delta");
        }
        else
        {
            Console.Error.WriteLine("  FAIL: deterministic replay delta detected");
            failures++;
        }

        Console.WriteLine(failures == 0 ? "UniversalSqueaker kernel characterization passed." : "UniversalSqueaker kernel characterization FAILED (" + failures + ").");
        return failures == 0 ? 0 : 1;
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
        return true;
    }

    /// <summary>全矩阵语料生成。确定性：每 case 独立 roll 流（seed 派生），同输入同输出。</summary>
    private static string GenerateCorpus()
    {
        StringBuilder sb = new();
        sb.Append("# us 0.1.0 golden corpus - kernel Select (UniversalSqueaker). Lines: scenario|mode|domain|action|seed|gate|eggs|soundKey|tier|poolStableKey; '-' = none.\n");
        sb.Append("# Rebuilt by tools/UniversalSqueakerKernelTests; scenarios frozen in Scenarios.cs (S1-S7 constructed, two-race equal routing). Any delta on replay = regression.\n");
        foreach (string scenario in Scenarios.ScenarioNames)
        {
            SqueakPoolRegistry registry = Scenarios.BuildRegistry(scenario);
            AppendScenarioCases(sb, scenario, registry, Scenarios.DomainsFor(scenario));
        }
        return sb.ToString();
    }

    private static void AppendScenarioCases(StringBuilder sb, string scenario, SqueakPoolRegistry registry, AudioDomain[] domains)
    {
        int actionCount = ActionKeyMirror.Count;
        bool[] eggFlags = new[] { false, true };
        foreach (SelectionMode mode in new[] { SelectionMode.Off, SelectionMode.Fallback, SelectionMode.Remix })
        {
            foreach (AudioDomain domain in domains)
            {
                for (int actionIndex = 0; actionIndex < actionCount; actionIndex++)
                {
                    SqueakAction action = (SqueakAction)actionIndex;
                    string actionKey = ActionKey.For(action)!;
                    foreach (bool allowEggs in eggFlags)
                    {
                        foreach (long seed in Seeds)
                        {
                            foreach (SimGate gate in new[] { SimGate.All, SimGate.Partial })
                            {
                                ChainResult result = registry.Select(
                                    new SelectionContext(domain, actionKey, AgeBucket.Adult, false, allowEggs),
                                    mode, gate, new LcgRandom(seed));
                                sb.Append(scenario).Append('|').Append(ModeName(mode)).Append('|').Append(domain).Append('|')
                                  .Append(actionKey).Append('|').Append(seed).Append('|').Append(gate == SimGate.All ? "all" : "partial").Append('|');
                                sb.Append(allowEggs ? "on" : "off").Append('|');
                                sb.Append(result.SoundKey ?? "-").Append('|').Append(TierName(result.Tier)).Append('|')
                                  .Append(result.PoolStableKey ?? "-").Append('\n');
                            }
                        }
                    }
                }
            }
        }
    }

    private static string ModeName(SelectionMode mode) => mode switch
    {
        SelectionMode.Off => "Off",
        SelectionMode.Fallback => "Fallback",
        _ => "Remix",
    };

    private static string TierName(ChainTier? tier) => tier switch
    {
        ChainTier.XenotypePack => "XenotypePack",
        ChainTier.RacePack => "RacePack",
        ChainTier.PackFallback => "PackFallback",
        ChainTier.BuiltInFallback => "BuiltInFallback",
        _ => "-",
    };

    private static int CountLines(string text)
    {
        int count = 0;
        foreach (char c in text) if (c == '\n') count++;
        return count;
    }

    internal static string FindRepositoryRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repository root (AGENTS.md) not found from " + AppContext.BaseDirectory);
    }
}
