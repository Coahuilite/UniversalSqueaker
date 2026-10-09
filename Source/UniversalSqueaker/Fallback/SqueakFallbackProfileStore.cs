using System;
using System.Collections.Generic;
using System.IO;
using UniversalSqueaker.Kernel;
using Verse;

namespace UniversalSqueaker;

/// <summary>One field-presence override row in an independently persisted fallback-profile copy.</summary>
public class SqueakFallbackProfileOverride : IExposable
{
    public string actionKey = "";
    public string soundKey = "";

    public void ExposeData()
    {
        Scribe_Values.Look(ref actionKey, "actionKey", "");
        Scribe_Values.Look(ref soundKey, "soundKey", "");
    }
}

/// <summary>On-disk Config copy. The source version is separate from field-presence overrides.</summary>
public class SqueakFallbackProfileCopy : IExposable
{
    public string packageId = "";
    public int sourceVersion;
    public bool hasOverrides;
    public List<SqueakFallbackProfileOverride> overrides = new();

    // VF1: the race this copy belongs to, carried IN the file. Player-created tables have no
    // source-side filename convention to recover from (sanitized names are lossy), so discovery
    // reads the identity from the payload. Old copies predate the field; Look defaults them to ""
    // and they keep loading through the source-race path exactly as before.
    public string raceDefName = "";

    public void ExposeData()
    {
        Scribe_Values.Look(ref packageId, "packageId", "");
        Scribe_Values.Look(ref sourceVersion, "sourceVersion", 0);
        Scribe_Values.Look(ref hasOverrides, "hasOverrides", false);
        Scribe_Values.Look(ref raceDefName, "raceDefName", "");
        Scribe_Collections.Look(ref overrides, "overrides", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && overrides == null) overrides = new List<SqueakFallbackProfileOverride>();
    }
}
/// <summary>
/// Per-race Config work-copy lifecycle. It is deliberately independent of ModSettings: no
/// WriteSettings call, no debounce queue, and no save-game data. Missing/corrupt/stale copies are
/// rebuilt from the immutable data-driven source; a current field-presence delta is merged and re-emitted
/// through this single SafeSaver writer so the artifact self-heals without entering ModSettings.
/// </summary>
internal static class SqueakFallbackProfileStore
{
    private const string DocumentElementName = "UniversalSqueakerFallbackProfile";
    private const string FilePrefix = "UniversalSqueaker_Profile_";
    private const string FileSuffix = ".xml";
    private static readonly Dictionary<RaceKey, FallbackProfile> profiles = new();
    private static BuiltInFallbackTable? current;
    private static BuiltInFallbackTable? lastSource;

    /// <summary>VF1 test seam: lanes point the store at a temp folder and NEVER touch the player's
    /// real Config directory.</summary>
    internal static string? ConfigDirectoryForTests { get; set; }

    private static string Directory => ConfigDirectoryForTests ?? GenFilePaths.ConfigFolderPath;

    public static BuiltInFallbackTable? Current => current;

    public static BuiltInFallbackTable LoadOrRebuild(BuiltInFallbackTable source)
    {
        lastSource = source ?? BuiltInFallbackTable.Empty;
        Dictionary<RaceKey, FallbackProfile> resolved = new();
        foreach (FallbackProfile sourceProfile in Profiles(lastSource))
        {
            FallbackProfile profile = LoadOne(sourceProfile);
            resolved[profile.Race] = profile;
        }

        // VF1: a player-created table is a first-class support source even with no VoicePack -
        // discovery reads the Config directory, not just source.Profiles, and an empty-delta
        // LoadOrRebuild could never have claimed otherwise.
        DiscoverPlayerTables(resolved);

        profiles.Clear();
        foreach (KeyValuePair<RaceKey, FallbackProfile> pair in resolved) profiles.Add(pair.Key, pair.Value);
        BuiltInFallbackTable result = new(new List<FallbackProfile>(resolved.Values));
        current = result;
        return result;
    }

    /// <summary>Re-run the whole load against the last data source (after a save/restore/delete).</summary>
    public static BuiltInFallbackTable Rebuild()
        => LoadOrRebuild(lastSource ?? BuiltInFallbackTable.Empty);

    /// <summary>The races the final table supports right now (maintainer sources and player tables).</summary>
    public static IEnumerable<string> SupportedRaceDefNames()
    {
        foreach (KeyValuePair<RaceKey, FallbackProfile> pair in profiles) yield return pair.Key.DefName;
    }

    /// <summary>True when a maintainer data source (a shipped profile Def) owns this race.</summary>
    public static bool HasMaintainerSource(RaceKey race)
    {
        BuiltInFallbackTable? source = lastSource;
        return source != null && source.For(race) != null;
    }

    /// <summary>VF1 (r5): every write answer is OBSERVABLE - a swallowed exception must never come
    /// back as "Saved". Written = the single-writer save succeeded and the table was reloaded;
    /// Failed = the write threw (the store logged it, the old file state stands); Refused* = the
    /// call was legitimate to reject (no shipped data to restore to / a shipped table cannot be
    /// deleted by the player path).</summary>
    public enum StoreOutcome { Written, Failed, RefusedNoSource, RefusedMaintainer }

    /// <summary>VF1 (r5): the player's REAL field-presence delta for one race, read straight from
    /// the copy - the starting point for a single-entry edit. It keeps delete markers and values
    /// that merely equal an old default, which a diff against the resolved table would lose.</summary>
    public static FallbackDelta? LoadPlayerDelta(RaceKey race)
    {
        try
        {
            string path = PathFor(race);
            if (!File.Exists(path)) return null;
            SqueakFallbackProfileCopy copy = Read(path);
            if (!string.Equals(copy.packageId, UniversalSqueakerMod.PackageId, StringComparison.Ordinal)) return null;
            bool invalid = false;
            return ToDelta(copy, out invalid);
        }
        catch (Exception ex)
        {
            SqueakLog.FallbackProfileStoreFailed(race.DefName, ex);
            return null;
        }
    }

    /// <summary>VF1 save: write the player's field-presence delta for one race and reload. A
    /// maintainer race keeps its source version (the delta merges over the shipped data); a
    /// player-only race records version 0 and merges over the empty profile. The write goes
    /// through the same SafeSaver single writer as every other copy, and its result is returned.</summary>
    public static StoreOutcome SaveProfile(RaceKey race, FallbackDelta delta)
    {
        FallbackProfile source = SourceFor(race);
        if (!TryWrite(PathFor(race), source, delta)) return StoreOutcome.Failed;
        Rebuild();
        return StoreOutcome.Written;
    }

    /// <summary>VF1 restore-default: clear the player overrides back to the maintainer data. A
    /// player-only race has no maintainer data to restore TO - callers must delete it instead,
    /// and this answer is explicit rather than a fake success.</summary>
    public static StoreOutcome RestoreDefault(RaceKey race)
    {
        FallbackProfile? source = lastSource?.For(race);
        if (source == null) return StoreOutcome.RefusedNoSource;
        if (!TryWrite(PathFor(race), source, null)) return StoreOutcome.Failed;
        Rebuild();
        return StoreOutcome.Written;
    }

    /// <summary>VF1 delete: removes ONLY a player-created table (file + support). A race with a
    /// maintainer source cannot be "permanently deleted" here - that would masquerade a shipped
    /// data removal - so the call is refused and the caller offers restore-default instead.</summary>
    public static StoreOutcome DeletePlayerTable(RaceKey race)
    {
        if (HasMaintainerSource(race)) return StoreOutcome.RefusedMaintainer;
        try
        {
            string path = PathFor(race);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            SqueakLog.FallbackProfileStoreFailed(race.DefName, ex);
            return StoreOutcome.Failed;
        }

        Rebuild();
        return StoreOutcome.Written;
    }

    private static FallbackProfile SourceFor(RaceKey race)
        => lastSource?.For(race) ?? new FallbackProfile(race, 0, new Dictionary<string, string>());

    public static FallbackProfile? For(RaceKey race) => profiles.TryGetValue(race, out FallbackProfile? profile) ? profile : null;

    /// <summary>VF1: the shipped data-source profile for a race (before player deltas), when one
    /// exists - the editor uses it to tell "maintainer data" from "player override".</summary>
    public static FallbackProfile? MaintainerSourceFor(RaceKey race) => lastSource?.For(race);


    private static FallbackProfile LoadOne(FallbackProfile source)
    {
        string path = PathFor(source.Race);
        SqueakFallbackProfileCopy? copy = null;
        bool corrupt = false;
        try
        {
            if (File.Exists(path)) copy = Read(path);
            else corrupt = true;
        }
        catch (Exception ex)
        {
            corrupt = true;
            SqueakLog.FallbackProfileStoreFailed(source.Race.DefName, ex);
        }

        bool invalid = false;
        if (!string.Equals(copy?.packageId, UniversalSqueakerMod.PackageId, StringComparison.Ordinal)) corrupt = true;
        FallbackDelta? delta = corrupt ? null : ToDelta(copy, out invalid);
        corrupt |= invalid;
        int copyVersion = copy?.sourceVersion ?? 0;
        CopyDisposition disposition = FallbackProfileOperations.DecideCopy(source, delta, copyVersion, corrupt);
        FallbackProfile resolved = disposition == CopyDisposition.MergeDelta
            ? FallbackProfileOperations.Merge(source, delta!)
            : source;
        if (disposition == CopyDisposition.RebuildFromSource)
            TryWrite(path, source, null);
        else if (disposition == CopyDisposition.MergeDelta)
            TryWrite(path, source, delta);
        else if (FallbackProfileOperations.NeedsRestamp(source, copyVersion))
            // VF1: the version bump never deletes a delta (DecideCopy), but a clean copy whose
            // stamp lags is re-stamped so the field cannot silently rot into "perpetually stale".
            TryWrite(path, source, null);
        return resolved;
    }

    private static IEnumerable<FallbackProfile> Profiles(BuiltInFallbackTable source)
    {
        foreach (FallbackProfile profile in source.Profiles) yield return profile;
    }

    private static SqueakFallbackProfileCopy Read(string path)
    {
        SqueakFallbackProfileCopy? copy = null;
        Scribe.loader.InitLoading(path);
        try
        {
            Scribe_Deep.Look(ref copy, "FallbackProfile");
        }
        finally
        {
            Scribe.loader.FinalizeLoading();
        }
        return copy ?? throw new InvalidDataException("Fallback profile copy contains no profile payload.");
    }

    private static FallbackDelta? ToDelta(SqueakFallbackProfileCopy? copy, out bool invalid)
    {
        invalid = false;
        if (copy == null || !copy.hasOverrides) return null;
        Dictionary<string, string> overrides = new(StringComparer.Ordinal);
        try
        {
            foreach (SqueakFallbackProfileOverride entry in copy.overrides ?? new List<SqueakFallbackProfileOverride>())
            {
                // VF1: an empty soundKey is the LEGAL explicit delete marker (see FallbackDelta);
                // a blank ACTION key or a duplicate is still the corrupt shape it always was.
                if (entry == null || string.IsNullOrWhiteSpace(entry.actionKey)
                    || overrides.ContainsKey(entry.actionKey))
                {
                    invalid = true;
                    return null;
                }
                overrides.Add(entry.actionKey, entry.soundKey ?? "");
            }
            return new FallbackDelta(overrides);
        }
        catch (Exception)
        {
            invalid = true;
            return null;
        }
    }

    private static bool TryWrite(string path, FallbackProfile source, FallbackDelta? delta)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            SqueakFallbackProfileCopy copy = new()
            {
                packageId = UniversalSqueakerMod.PackageId,
                sourceVersion = source.Version,
                hasOverrides = delta != null,
                raceDefName = source.Race.DefName,
            };
            if (delta != null)
            {
                foreach (KeyValuePair<string, string> entry in delta.Overrides)
                    copy.overrides.Add(new SqueakFallbackProfileOverride { actionKey = entry.Key, soundKey = entry.Value ?? "" });
            }
            SafeSaver.Save(path, DocumentElementName, () =>
            {
                SqueakFallbackProfileCopy? saveable = copy;
                Scribe_Deep.Look(ref saveable, "FallbackProfile");
            });
            return true;
        }
        catch (Exception ex)
        {
            SqueakLog.FallbackProfileStoreFailed(source.Race.DefName, ex);
            return false;
        }
    }

    /// <summary>VF1 discovery: every Config copy whose race is NOT a maintainer source race becomes
    /// a player-only profile (empty source + the file's delta). Copies without the carried identity
    /// (pre-VF1 files) can only ever have belonged to a source race, so skipping them here loses
    /// nothing. A corrupt player file is left on disk and logged - the store never rewrites what it
    /// cannot read.</summary>
    private static void DiscoverPlayerTables(Dictionary<RaceKey, FallbackProfile> resolved)
    {
        string[] files;
        try
        {
            files = System.IO.Directory.GetFiles(Directory, FilePrefix + "*" + FileSuffix);
        }
        catch (Exception ex)
        {
            SqueakLog.FallbackProfileStoreFailed("*", ex);
            return;
        }

        foreach (string path in files)
        {
            SqueakFallbackProfileCopy? copy;
            try
            {
                copy = Read(path);
            }
            catch (Exception ex)
            {
                SqueakLog.FallbackProfileStoreFailed(Path.GetFileName(path), ex);
                continue;
            }

            if (copy == null || !string.Equals(copy.packageId, UniversalSqueakerMod.PackageId, StringComparison.Ordinal)) continue;
            string raceName = copy.raceDefName ?? "";
            if (string.IsNullOrWhiteSpace(raceName)) continue;
            RaceKey race = new(raceName);
            if (resolved.ContainsKey(race)) continue;

            bool invalid = false;
            FallbackDelta? delta = ToDelta(copy, out invalid);
            if (invalid) continue;
            FallbackProfile playerSource = new(race, 0, new Dictionary<string, string>());
            resolved[race] = delta == null ? playerSource : FallbackProfileOperations.Merge(playerSource, delta);
        }
    }

    private static string PathFor(RaceKey race)
    {
        return Path.Combine(Directory,
            FilePrefix + GenText.SanitizeFilename(race.DefName) + FileSuffix);
    }
}
