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
///   C 版本低（无 delta）→ 内容即源，仅回写版本戳（stale 不再是删除触发器）
///   D delta 合并 → MergeDelta（field-presence override 合入，副本回写并保留 delta）
///   E 版本提升 + delta → VF1 定稿裁定：delta 存活、副本回写新版本戳（旧「源胜丢覆盖」已废）
///   F 包身份不符（损坏类）→ RebuildFromSource
///   G 玩家表：SaveProfile 创建 → 目录发现 → DeletePlayerTable 撤除（随包 race 拒绝删除）
///   H 单项编辑以真实玩家 delta 为起点：其它动作的删除标记/覆盖不被复活也不丢失
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
            VersionBumpKeepsDelta();
            ForeignPackageIdRebuilds();
            PlayerTableCreateDiscoverDelete();
            SingleEntryEditKeepsOtherMarkers();
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
        Scenario("C-stale-version-clean");
        WritePlayerCopy(sourceVersion: 1, hasOverrides: false, overrides: null);

        BuiltInFallbackTable source = SourceV(3);
        FallbackProfile? profile = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(profile != null && profile.Version == 3 && profile.SoundKeys["Call"] == "US_Call_RaceA",
            "stale: a clean older copy resolves to the current source (content was never the player's)", ref failures);

        string first = ReadFile(profilePath);
        Check(first.Contains("<sourceVersion>3</sourceVersion>") && !first.Contains("<hasOverrides>True</hasOverrides>"),
            "stale: the version stamp is re-stamped to the current source without inventing overrides", ref failures);
        Check(SqueakLog.StoreFailures.Count == 0, "stale: version skew is not a corruption event", ref failures);

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

    private static void VersionBumpKeepsDelta()
    {
        ResetState();
        Scenario("E-version-bump-keeps-delta");
        // VF1 定稿裁定（2026-10-07）：默认数据版本提升 NOT 丢弃玩家 delta。旧 E 情景
        // （"source version bump overwrites stale delta"）是被本裁定替换的缺陷。
        WritePlayerCopy(sourceVersion: 3, hasOverrides: true, overrides: new Dictionary<string, string> { ["Call"] = "US_Call_Override" });

        BuiltInFallbackTable source = new(new[]
        {
            new FallbackProfile(RaceA, 4, new Dictionary<string, string>
            {
                ["Call"] = "US_Call_V4", ["Eat"] = "US_Eat_V4", ["Sleep"] = "US_Sleep_RaceA",
            }),
        });
        FallbackProfile? profile = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(profile != null && profile.Version == 4 && profile.SoundKeys["Call"] == "US_Call_Override"
                && profile.SoundKeys["Eat"] == "US_Eat_V4",
            "bump: the player's Call delta survives while untouched Eat follows the NEW source sound", ref failures);

        string first = ReadFile(profilePath);
        Check(first.Contains("<sourceVersion>4</sourceVersion>") && first.Contains("<hasOverrides>True</hasOverrides>")
                && first.Contains("US_Call_Override"),
            "bump: the copy is re-stamped at version 4 WITH the delta kept", ref failures);
        Check(SqueakLog.StoreFailures.Count == 0, "bump: a legitimate merge is not a corruption event", ref failures);

        FallbackProfile? reload = SqueakFallbackProfileStore.LoadOrRebuild(source).For(RaceA);
        Check(reload != null && reload.SoundKeys["Call"] == "US_Call_Override", "bump: second load keeps the merged override", ref failures);
        Check(ReadFile(profilePath) == first, "bump: second load did not rewrite the file (idempotent)", ref failures);
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

    /// <summary>VF1 (r5): the player-table lifecycle through the PRODUCTION store APIs - create via
    /// SaveProfile, discover via the directory scan, withdraw via DeletePlayerTable, and the shipped
    /// race's table refuses deletion. Temp dir only; the player's real Config is never touched.</summary>
    private static void PlayerTableCreateDiscoverDelete()
    {
        ResetState();
        Scenario("G-player-table-lifecycle");
        RaceKey raceZ = new("RaceZ");
        string raceZPath = Path.Combine(configDir, "UniversalSqueaker_Profile_RaceZ.xml");

        BuiltInFallbackTable source = SourceV(1);
        SqueakFallbackProfileStore.LoadOrRebuild(source);
        Check(SqueakFallbackProfileStore.Current!.For(raceZ) == null, "create: RaceZ unsupported before the table", ref failures);

        Check(SqueakFallbackProfileStore.SaveProfile(raceZ, new FallbackDelta(new Dictionary<string, string> { ["Call"] = "US_Call_RaceZ" }))
                == SqueakFallbackProfileStore.StoreOutcome.Written,
            "create: SaveProfile reports Written", ref failures);
        Check(SqueakFallbackProfileStore.Current!.For(raceZ) != null
                && SqueakFallbackProfileStore.Current!.For(raceZ)!.SoundKeys["Call"] == "US_Call_RaceZ",
            "discover: the directory scan resolves the player table on reload", ref failures);
        Check(File.Exists(raceZPath) && ReadFile(raceZPath).Contains("RaceZ"),
            "discover: the copy carries its raceDefName identity in the file", ref failures);

        Check(SqueakFallbackProfileStore.DeletePlayerTable(raceZ) == SqueakFallbackProfileStore.StoreOutcome.Written
                && SqueakFallbackProfileStore.Current!.For(raceZ) == null && !File.Exists(raceZPath),
            "delete: the player table and its support are withdrawn symmetrically", ref failures);
        Check(SqueakFallbackProfileStore.DeletePlayerTable(RaceA) == SqueakFallbackProfileStore.StoreOutcome.RefusedMaintainer,
            "delete: a shipped race's table refuses player deletion (restore-default is that path)", ref failures);
    }

    /// <summary>VF1 (r5): the single-entry edit starts from the player's REAL delta (LoadPlayerDelta),
    /// so another action's delete marker is never resurrected and overrides that merely equal the
    /// shipped default are not silently dropped - the exact loss the resolved-diff reconstruction
    /// had.</summary>
    private static void SingleEntryEditKeepsOtherMarkers()
    {
        ResetState();
        Scenario("H-single-entry-edit");
        BuiltInFallbackTable source = SourceV(1);
        SqueakFallbackProfileStore.LoadOrRebuild(source);

        // A player delta with: an explicit delete marker (Eat -> ""), an override that HAPPENS to
        // equal the shipped Call value, and a real Sleep override.
        WritePlayerCopy(sourceVersion: 1, hasOverrides: true, overrides: new Dictionary<string, string>
        {
            ["Eat"] = "",
            ["Call"] = "US_Call_RaceA",
            ["Sleep"] = "US_Sleep_Custom",
        });

        FallbackDelta? before = SqueakFallbackProfileStore.LoadPlayerDelta(RaceA);
        Check(before != null && before.Overrides["Eat"] == "" && before.Overrides.Count == 3,
            "readback: the REAL delta (markers included) is the edit's starting point", ref failures);

        var edited = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in before!.Overrides) edited[entry.Key] = entry.Value;
        edited.Remove("Call");               // restore inheritance for Call
        edited["Sleep"] = "US_Sleep_Custom2"; // touch only Sleep
        Check(SqueakFallbackProfileStore.SaveProfile(RaceA, new FallbackDelta(edited))
                == SqueakFallbackProfileStore.StoreOutcome.Written,
            "edit: the single-entry save reports Written", ref failures);

        FallbackProfile? after = SqueakFallbackProfileStore.Current!.For(RaceA);
        Check(after != null && after.SoundKeys["Call"] == "US_Call_RaceA"
                && !after.SoundKeys.ContainsKey("Eat")
                && after.SoundKeys["Sleep"] == "US_Sleep_Custom2",
            "edit: removing an override restores inheritance, the untouched delete marker STAYS a "
            + "marker (Eat is not resurrected), and only Sleep moved", ref failures);
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
    private static void WritePlayerCopy(int sourceVersion, bool hasOverrides, Dictionary<string, string>? overrides,
        string packageId = UniversalSqueakerMod.PackageId, string? race = null, string? path = null)
    {
        SqueakFallbackProfileCopy copy = new()
        {
            packageId = packageId,
            sourceVersion = sourceVersion,
            hasOverrides = hasOverrides,
            raceDefName = race ?? "RaceA",
        };
        if (overrides != null)
        {
            foreach (KeyValuePair<string, string> entry in overrides)
                copy.overrides.Add(new SqueakFallbackProfileOverride { actionKey = entry.Key, soundKey = entry.Value });
        }
        string target = path ?? profilePath;
        Verse.SafeSaver.Save(target, "UniversalSqueakerFallbackProfile", () =>
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
