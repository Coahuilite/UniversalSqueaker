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
/// 2) 正常运行：冻结的历史路由输入矩阵（不是当前官方包装目录）生成确定性
///    输入→期望 ChainResult 字节，并在写入前与提交的 US 0.1.0 冻结基线比较。
///    重建该基线只有显式 <c>--update-corpus</c> 维护模式；<c>--emit-corpus &lt;path&gt;</c> 写调用方显式给定的那个路径，
///    不隐式选择默认基线，两写法互斥（把默认基线路径显式交给 emit 时，该路径同样会被写入）。
/// 3) 确定性重放：生成两遍必须字节相同。
/// 基线是 US 自己的语料（S1-S7 构造场景），不依赖任何历史内容快照。
/// </summary>
internal static class Program
{
    private const string CorpusFileName = "us-corpus-0.1.0.txt";
    private const string UpdateCorpusArgument = "--update-corpus";
    private const string EmitCorpusArgument = "--emit-corpus";
    private static readonly long[] Seeds = { 1, 2, 3 };
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private static int Main(string[] args)
    {
        bool updateCorpus = false;
        string? emitCorpusPath = null;
        if (args.Length == 0)
        {
            // 默认：只比较，不写任何语料（包括不写仓库内的默认基线）。
        }
        else if (args.Length == 1 && string.Equals(args[0], UpdateCorpusArgument, StringComparison.Ordinal))
        {
            updateCorpus = true;
        }
        else if (args.Length == 2 && string.Equals(args[0], EmitCorpusArgument, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(args[1]))
        {
            // 取证出口：只写调用方显式给定的那个路径（对路径本身无额外限制）；本分支不隐式选择默认基线，且与 --update-corpus 互斥。
            emitCorpusPath = args[1];
        }
        else
        {
            Console.Error.WriteLine("Usage: UniversalSqueakerKernelTests [--update-corpus | --emit-corpus <path>]");
            return 2;
        }
        try
        {
            return Run(updateCorpus, emitCorpusPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("UNHANDLED: " + ex.GetType().FullName + " :: " + ex.Message);
            return 1;
        }
    }

    private static int Run(bool updateCorpus, string? emitCorpusPath = null)
    {
        int failures = 0;
        Console.WriteLine("Unit tests...");
        UnitTests.RunAll(ref failures);

        string corpusDir = Path.Combine(FindRepositoryRoot(), "tools", "UniversalSqueakerKernelTests", "fixtures", "corpus");
        string corpusPath = Path.Combine(corpusDir, CorpusFileName);

        Console.WriteLine("Golden corpus generation (frozen string-key inputs, not the official package inventory)...");
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

        if (emitCorpusPath != null)
        {
            // 只写调用方显式指定的那个路径；本分支不隐式选择默认基线（fixtures/corpus），改写默认基线的入口是 --update-corpus。
            string fullPath = Path.GetFullPath(emitCorpusPath);
            string? parentDir = Path.GetDirectoryName(fullPath);
            if (parentDir != null) Directory.CreateDirectory(parentDir);
            File.WriteAllBytes(fullPath, generated);
            Console.WriteLine("  ok: candidate corpus emitted to explicit path: " + fullPath
                + " (" + CountLines(corpus) + " cases, " + generated.Length + " bytes)");
        }

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
        bool[] eggFlags = new[] { false, true };
        foreach (SelectionMode mode in new[] { SelectionMode.Off, SelectionMode.Fallback, SelectionMode.Remix })
        {
            foreach (AudioDomain domain in domains)
            {
                foreach (string actionKey in Scenarios.FrozenRoutingKeys)
                {
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
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? dir = new DirectoryInfo(start);
            while (dir != null)
            {
                // A relocated test may have an unrelated management AGENTS.md above it.
                if (File.Exists(Path.Combine(dir.FullName, "tools", "UniversalSqueakerKernelTests", "UniversalSqueakerKernelTests.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        throw new InvalidOperationException("Kernel test project root not found from cwd or " + AppContext.BaseDirectory);
    }
}
