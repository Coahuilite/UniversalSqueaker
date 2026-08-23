using System;
using System.Collections.Generic;
using System.IO;
using UniversalSqueaker.Kernel;
using Verse;

namespace UniversalSqueaker.ConfigCopyTests;

/// <summary>
/// Config 副本生命周期 harness：生产 <c>SqueakFallbackProfileStore</c>（适配层单写者 temp+atomic
/// replace，不经 WriteSettings）在 Scribe stub 上的端到端场景：
///   A 缺失 → RebuildFromSource（写出干净副本，重启幂等）
///   B 损坏 → RebuildFromSource（替换损坏文件 + 记 store-failed 日志）
///   C 版本低 → RebuildFromSource（源版本覆盖副本）
///   D delta 合并 → MergeDelta（field-presence override 合入，副本回写并保留 delta）
///   E 重置覆盖 → 源版本提升时旧 delta 被源覆盖（重建 = 源胜）
///   F 包身份不符（损坏类）→ RebuildFromSource
/// 内核无装配域过滤：A 场景同时断言两个源 race 都解析并写副本，且“第二遍加载不再变”幂等。
/// 每个场景在临时 Config 目录运行，结束时清理。
/// </summary>
internal static class Program
{
    private const string ProfileFileName = "UniversalSqueaker_Profile_RaceA.xml";
    private static string configDir = null!;
    private static string profilePath = null!;
    private static string raceBPath = null!;
    private static int failures;
    private static readonly RaceKey RaceA = new("RaceA");
    private static readonly RaceKey RaceB = new("RaceB");

    private static int Main()
    {
        configDir = Path.Combine(Path.GetTempPath(), "us-config-copy-" + Guid.NewGuid().ToString("N"));
        Verse.GenFilePaths.ConfigFolderPath = configDir;
        profilePath = Path.Combine(configDir, ProfileFileName);
        raceBPath = Path.Combine(configDir, "UniversalSqueaker_Profile_RaceB.xml");
        try
        {
            MissingCopyRebuildsFromSource();
            CorruptCopyRebuildsAndLogs();
            StaleVersionCopyRebuilds();
            DeltaMergesIntoSource();
            ResetOverwriteDropsStaleDelta();
            ForeignPackageIdRebuilds();
            if (failures == 0)
            {
                Console.WriteLine("Config copy characterization passed.");
                return 0;
            }
            Console.Error.WriteLine("Config copy characterization FAILED (" + failures + ").");
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(configDir)) Directory.Delete(configDir, true); } catch { }
        }
    }

    private static BuiltInFallbackTable SourceV(int version, params RaceKey[] races)
    {
        if (races.Length == 0) races = new[] { RaceA };
        List<FallbackProfile> profiles = new();
        foreach (RaceKey race in races)
        {
            string suffix = race.DefName;
            profiles.Add(new FallbackProfile(race, version, new Dictionary<string, string>
            {
                ["Call"] = "US_Call_" + suffix,
                ["Eat"] = "US_Eat_" + suffix,
                ["Sleep"] = "US_Sleep_" + suffix,
            }));
        }
        return new BuiltInFallbackTable(profiles);
    }

    private static void MissingCopyRebuildsFromSource()
    {
        ResetState();
        Scenario("A-missing");
        Check(!File.Exists(profilePath), "missing: no copy on disk before first load", ref failures);
        Check(!File.Exists(raceBPath), "missing: no second-race copy on disk before first load", ref failures);

        BuiltInFallbackTable source = SourceV(1, RaceA, RaceB);
        BuiltInFallbackTable loaded = SqueakFallbackProfileStore.LoadOrRebuild(source);
        FallbackProfile? profile = loaded.For(RaceA);
        Check(profile != null && profile.SoundKeys.Count == 3 && profile.SoundKeys["Call"] == "US_Call_RaceA"
            && !profile.SoundKeys.ContainsKey("Crying") && !profile.SoundKeys.ContainsKey("Giggling"),
            "missing: resolved profile equals source (3 mappings, no Crying/Giggling)", ref failures);
        Check(loaded.For(RaceB) != null && loaded.For(RaceB)!.SoundKeys["Call"] == "US_Call_RaceB",
            "missing: every source race resolves (no domain filter)", ref failures);
        Check(File.Exists(profilePath) && File.Exists(raceBPath),
            "missing: every source race gets a copy file", ref failures);

        string first = ReadFile(profilePath);
        Check(first.Contains("<sourceVersion>1</sourceVersion>")
            && !first.Contains("<hasOverrides>True</hasOverrides>"),
            "missing: rebuild wrote a clean version-1 copy without overrides", ref failures);
        string firstB = ReadFile(raceBPath);
        Check(firstB.Contains("<sourceVersion>1</sourceVersion>")
            && !firstB.Contains("<hasOverrides>True</hasOverrides>"),
            "missing: second-race rebuild wrote a clean version-1 copy", ref failures);

        FallbackProfile? reload = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(reload != null && reload.SoundKeys.Count == 3, "missing: second load keeps the healed copy", ref failures);
        Check(ReadFile(profilePath) == first, "missing: second load did not rewrite the file (idempotent)", ref failures);
        Check(ReadFile(raceBPath) == firstB, "missing: second load did not rewrite the second-race file (idempotent)", ref failures);
    }

    private static void CorruptCopyRebuildsAndLogs()
    {
        ResetState();
        Scenario("B-corrupt");
        File.WriteAllText(profilePath, "this is not a Scribe xml copy");
        SqueakLog.StoreFailures.Clear();

        BuiltInFallbackTable source = SourceV(3);
        FallbackProfile? profile = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(profile != null && profile.SoundKeys["Call"] == "US_Call_RaceA" && profile.SoundKeys["Eat"] == "US_Eat_RaceA" && profile.SoundKeys["Sleep"] == "US_Sleep_RaceA",
            "corrupt: resolved profile equals source", ref failures);
        Check(SqueakLog.StoreFailures.Contains("RaceA"), "corrupt: store-failed diagnostic captured", ref failures);

        string first = ReadFile(profilePath);
        Check(first.Contains("<sourceVersion>3</sourceVersion>") && !first.Contains("<hasOverrides>True</hasOverrides>"),
            "corrupt: file replaced with a clean version-3 copy", ref failures);
        Check(ReadFile(profilePath) != "this is not a Scribe xml copy", "corrupt: garbage file was replaced", ref failures);

        FallbackProfile? reload = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(reload != null && reload.SoundKeys.Count == 3, "corrupt: second load stable", ref failures);
        Check(ReadFile(profilePath) == first, "corrupt: second load did not rewrite the file (idempotent)", ref failures);
    }

    private static void StaleVersionCopyRebuilds()
    {
        ResetState();
        Scenario("C-stale-version");
        WritePlayerCopy(sourceVersion: 1, hasOverrides: false, overrides: null);

        BuiltInFallbackTable source = SourceV(3);
        FallbackProfile? profile = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(profile != null && profile.Version == 3 && profile.SoundKeys["Call"] == "US_Call_RaceA",
            "stale: older copy rebuilds from current source", ref failures);

        string first = ReadFile(profilePath);
        Check(first.Contains("<sourceVersion>3</sourceVersion>") && !first.Contains("<hasOverrides>True</hasOverrides>"),
            "stale: file rewritten at current source version without overrides", ref failures);
        Check(SqueakLog.StoreFailures.Count == 0, "stale: version bump is not a corruption event", ref failures);

        FallbackProfile? reload = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(reload != null && reload.Version == 3, "stale: second load stable at version 3", ref failures);
        Check(ReadFile(profilePath) == first, "stale: second load did not rewrite the file (idempotent)", ref failures);
    }

    private static void DeltaMergesIntoSource()
    {
        ResetState();
        Scenario("D-delta-merge");
        WritePlayerCopy(sourceVersion: 3, hasOverrides: true, overrides: new Dictionary<string, string> { ["Call"] = "US_Call_Override" });

        BuiltInFallbackTable source = SourceV(3);
        FallbackProfile? profile = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(profile != null && profile.Version == 3 && profile.SoundKeys["Call"] == "US_Call_Override"
            && profile.SoundKeys["Eat"] == "US_Eat_RaceA" && profile.SoundKeys["Sleep"] == "US_Sleep_RaceA",
            "delta: field-presence override merges over source, untouched keys inherit", ref failures);

        string first = ReadFile(profilePath);
        Check(first.Contains("<hasOverrides>True</hasOverrides>") && first.Contains("US_Call_Override"),
            "delta: copy rewritten preserving the override delta", ref failures);

        FallbackProfile? reload = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(reload != null && reload.SoundKeys["Call"] == "US_Call_Override", "delta: second load keeps the merged override", ref failures);
        Check(ReadFile(profilePath) == first, "delta: second load did not rewrite the file (idempotent)", ref failures);
    }

    private static void ResetOverwriteDropsStaleDelta()
    {
        ResetState();
        Scenario("E-reset-overwrite");
        // 玩家 override 副本 + 源版本提升：重建 = 源覆盖（override 不跨版本存活）。
        WritePlayerCopy(sourceVersion: 3, hasOverrides: true, overrides: new Dictionary<string, string> { ["Call"] = "US_Call_Override" });

        BuiltInFallbackTable source = SourceV(4);
        FallbackProfile? profile = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(profile != null && profile.Version == 4 && profile.SoundKeys["Call"] == "US_Call_RaceA",
            "reset: source version bump overwrites stale delta (source wins)", ref failures);

        string first = ReadFile(profilePath);
        Check(first.Contains("<sourceVersion>4</sourceVersion>") && !first.Contains("<hasOverrides>True</hasOverrides>"),
            "reset: file overwritten with a clean version-4 copy", ref failures);
        Check(SqueakLog.StoreFailures.Count == 0, "reset: legitimate rebuild is not a corruption event", ref failures);

        FallbackProfile? reload = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(reload != null && reload.SoundKeys["Call"] == "US_Call_RaceA", "reset: second load stable at source", ref failures);
        Check(ReadFile(profilePath) == first, "reset: second load did not rewrite the file (idempotent)", ref failures);
    }

    private static void ForeignPackageIdRebuilds()
    {
        ResetState();
        Scenario("F-foreign-package-id");
        WritePlayerCopy(sourceVersion: 3, hasOverrides: true, overrides: new Dictionary<string, string> { ["Call"] = "US_Call_Override" }, packageId: "other.mod.owner");

        BuiltInFallbackTable source = SourceV(3);
        FallbackProfile? profile = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(profile != null && profile.SoundKeys["Call"] == "US_Call_RaceA",
            "foreign: packageId mismatch is corrupt → rebuild from source", ref failures);
        Check(SqueakLog.StoreFailures.Count == 0,
            "foreign: packageId mismatch is silent normalization (only read failures log)", ref failures);

        string first = ReadFile(profilePath);
        Check(first.Contains("coahuilite.universalsqueaker") && first.Contains("<sourceVersion>3</sourceVersion>"),
            "foreign: file rewritten under the permanent packageId", ref failures);
        FallbackProfile? reload = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(reload != null && reload.SoundKeys["Call"] == "US_Call_RaceA", "foreign: second load stable", ref failures);
        Check(ReadFile(profilePath) == first, "foreign: second load did not rewrite the file (idempotent)", ref failures);
    }

    // ---- helpers ----

    private static void ResetState()
    {
        if (File.Exists(profilePath)) File.Delete(profilePath);
        if (File.Exists(raceBPath)) File.Delete(raceBPath);
        SqueakLog.StoreFailures.Clear();
    }

    private static void Scenario(string name) => Console.WriteLine("Scenario " + name + "...");

    /// <summary>写一个「玩家/上次会话留下的」副本（与生产 store 同一 Scribe 形状）。</summary>
    private static void WritePlayerCopy(int sourceVersion, bool hasOverrides, Dictionary<string, string>? overrides, string packageId = UniversalSqueakerMod.PackageId)
    {
        SqueakFallbackProfileCopy copy = new()
        {
            packageId = packageId,
            sourceVersion = sourceVersion,
            hasOverrides = hasOverrides,
        };
        if (overrides != null)
        {
            foreach (KeyValuePair<string, string> entry in overrides)
                copy.overrides.Add(new SqueakFallbackProfileOverride { actionKey = entry.Key, soundKey = entry.Value });
        }
        Verse.SafeSaver.Save(profilePath, "UniversalSqueakerFallbackProfile", () =>
        {
            SqueakFallbackProfileCopy? saveable = copy;
            Scribe_Deep.Look(ref saveable, "FallbackProfile");
        });
    }

    private static string ReadFile(string path) => File.ReadAllText(path);

    private static string ReadFile() => File.ReadAllText(profilePath);

    private static void Check(bool condition, string name, ref int failures)
    {
        if (condition) Console.WriteLine("  ok: " + name);
        else { Console.Error.WriteLine("  FAIL: " + name); failures++; }
    }
}
