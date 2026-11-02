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
    private static BuiltInFallbackTable? current;
    private static BuiltInFallbackTable? lastSource;
    // Bound once by native startup after the initial catalog is ready. Detached store users retain
    // the persistence-only contract; every production mutation after startup reaches this publisher.
    internal static Action<BuiltInFallbackTable>? RuntimePublisher { private get; set; }

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

        BuiltInFallbackTable result = new(new List<FallbackProfile>(resolved.Values));
        current = result;
        return result;
    }

    /// <summary>Re-run the load/recovery against the last data source; successful mutations do not reload or rewrite.</summary>
    public static BuiltInFallbackTable Rebuild()
        => LoadOrRebuild(lastSource ?? BuiltInFallbackTable.Empty);

    /// <summary>The races the final table supports right now (maintainer sources and player tables).</summary>
    public static IEnumerable<string> SupportedRaceDefNames()
    {
        foreach (FallbackProfile profile in Profiles(current ?? BuiltInFallbackTable.Empty)) yield return profile.Race.DefName;
    }

    /// <summary>True when a maintainer data source (a shipped profile Def) owns this race.</summary>
    public static bool HasMaintainerSource(RaceKey race)
    {
        BuiltInFallbackTable? source = lastSource;
        return source != null && source.For(race) != null;
    }

    /// <summary>Written reports physical commitment (and adoption when a native publisher is bound).
    /// WrittenRuntimeNotAdopted also reports commitment, but preserves the last-good runtime catalog.
    /// Failed and Refused* occur before commitment. Detached store usage remains persistence-only.</summary>
    public enum StoreOutcome { Written, Failed, RefusedNoSource, RefusedMaintainer, WrittenRuntimeNotAdopted }

    public static FallbackDelta? LoadPlayerDelta(RaceKey race)
        => TryLoadPlayerDelta(race, out var delta) ? delta : null;

    /// <summary>An absent delta is valid inheritance; an unreadable delta must not become an edit
    /// starting from an empty row, which would lose untouched overrides and explicit-empty markers.</summary>
    public static bool TryLoadPlayerDelta(RaceKey race, out FallbackDelta? delta)
    {
        delta = null;
        try
        {
            string path = PathFor(race);
            if (!File.Exists(path)) return true;
            SqueakFallbackProfileCopy copy = Read(path);
            if (!string.Equals(copy.packageId, SqueakerMod.PackageId, StringComparison.Ordinal))
                throw new InvalidDataException("Fallback profile package identity is invalid.");
            delta = ToDelta(copy, out bool invalid);
            if (invalid) throw new InvalidDataException("Fallback profile delta is invalid.");
            return true;
        }
        catch (Exception ex)
        {
            SqueakLog.FallbackProfileStoreFailed(race.DefName, ex, "read_player_delta");
            return false;
        }
    }

    /// <summary>VF1 save: write the player's field-presence delta for one race and publish the staged table. A
    /// maintainer race keeps its source version (the delta merges over the shipped data); a
    /// player-only race records version 0 and merges over the empty profile. The write goes
    /// through the same SafeSaver single writer as every other copy, and its result is returned.</summary>
    public static StoreOutcome SaveProfile(RaceKey race, FallbackDelta delta)
    {
        FallbackProfile source = SourceFor(race);
        var candidate = ReplaceProfile(race, FallbackProfileOperations.Merge(source, delta));
        if (!TryWrite(PathFor(race), source, delta)) return StoreOutcome.Failed;
        return PublishCommitted(race, candidate);
    }

    /// <summary>VF1 restore-default: clear the player overrides back to the maintainer data. A
    /// player-only race has no maintainer data to restore TO - callers must delete it instead,
    /// and this answer is explicit rather than a fake success.</summary>
    public static StoreOutcome RestoreDefault(RaceKey race)
    {
        FallbackProfile? source = lastSource?.For(race);
        if (source == null) return StoreOutcome.RefusedNoSource;
        var candidate = ReplaceProfile(race, source);
        if (!TryWrite(PathFor(race), source, null)) return StoreOutcome.Failed;
        return PublishCommitted(race, candidate);
    }

    /// <summary>VF1 delete: removes ONLY a player-created table (file + support). A race with a
    /// maintainer source cannot be "permanently deleted" here - that would masquerade a shipped
    /// data removal - so the call is refused and the caller offers restore-default instead.</summary>
    public static StoreOutcome DeletePlayerTable(RaceKey race)
    {
        if (HasMaintainerSource(race)) return StoreOutcome.RefusedMaintainer;
        var candidate = ReplaceProfile(race, null);
        try
        {
            string path = PathFor(race);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            SqueakLog.FallbackProfileStoreFailed(race.DefName, ex, "delete_player_table");
            return StoreOutcome.Failed;
        }

        return PublishCommitted(race, candidate);
    }

    private static BuiltInFallbackTable ReplaceProfile(RaceKey race, FallbackProfile? replacement)
    {
        var next = new List<FallbackProfile>();
        bool replaced = false;
        foreach (var profile in Profiles(current ?? BuiltInFallbackTable.Empty))
        {
            if (!profile.Race.Equals(race)) next.Add(profile);
            else
            {
                replaced = true;
                if (replacement != null) next.Add(replacement);
            }
        }
        if (!replaced && replacement != null) next.Add(replacement);
        return new BuiltInFallbackTable(next);
    }

    private static StoreOutcome PublishCommitted(RaceKey race, BuiltInFallbackTable candidate)
    {
        // No reload/restamp or second disk transaction after success. Publish the already staged
        // complete table, then let the existing audio owner atomically replace its own reference.
        current = candidate;
        try { RuntimePublisher?.Invoke(candidate); }
        catch (Exception error) when (error is not OutOfMemoryException && error is not StackOverflowException
            && error is not AccessViolationException && error is not System.Threading.ThreadAbortException)
        {
            SqueakLog.FallbackAudioPublishFailed(race.DefName, error);
            return StoreOutcome.WrittenRuntimeNotAdopted;
        }
        return StoreOutcome.Written;
    }

    private static FallbackProfile SourceFor(RaceKey race)
        => lastSource?.For(race) ?? new FallbackProfile(race, 0, new Dictionary<string, string>());

    public static FallbackProfile? For(RaceKey race) => current?.For(race);

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
            SqueakLog.FallbackProfileStoreFailed(source.Race.DefName, ex, "load_profile_copy");
        }

        bool invalid = false;
        if (!string.Equals(copy?.packageId, SqueakerMod.PackageId, StringComparison.Ordinal)) corrupt = true;
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
                packageId = SqueakerMod.PackageId,
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
            SqueakLog.FallbackProfileStoreFailed(source.Race.DefName, ex, "write_profile_copy");
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
            SqueakLog.FallbackProfileStoreFailed("*", ex, "discover_player_tables");
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
                SqueakLog.FallbackProfileStoreFailed(Path.GetFileName(path), ex, "read_player_table");
                continue;
            }

            if (copy == null || !string.Equals(copy.packageId, SqueakerMod.PackageId, StringComparison.Ordinal)) continue;
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
