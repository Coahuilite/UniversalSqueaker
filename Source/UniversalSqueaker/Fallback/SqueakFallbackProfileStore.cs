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

    public void ExposeData()
    {
        Scribe_Values.Look(ref packageId, "packageId", "");
        Scribe_Values.Look(ref sourceVersion, "sourceVersion", 0);
        Scribe_Values.Look(ref hasOverrides, "hasOverrides", false);
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

    public static BuiltInFallbackTable? Current => current;

    public static BuiltInFallbackTable LoadOrRebuild(BuiltInFallbackTable source)
    {
        Dictionary<RaceKey, FallbackProfile> resolved = new();
        foreach (FallbackProfile sourceProfile in Profiles(source))
        {
            FallbackProfile profile = LoadOne(sourceProfile);
            resolved[profile.Race] = profile;
        }

        profiles.Clear();
        foreach (KeyValuePair<RaceKey, FallbackProfile> pair in resolved) profiles.Add(pair.Key, pair.Value);
        BuiltInFallbackTable result = new(new List<FallbackProfile>(resolved.Values));
        current = result;
        return result;
    }

    public static FallbackProfile? For(RaceKey race) => profiles.TryGetValue(race, out FallbackProfile? profile) ? profile : null;

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
                if (entry == null || string.IsNullOrWhiteSpace(entry.actionKey) || string.IsNullOrWhiteSpace(entry.soundKey)
                    || overrides.ContainsKey(entry.actionKey))
                {
                    invalid = true;
                    return null;
                }
                overrides.Add(entry.actionKey, entry.soundKey);
            }
            return new FallbackDelta(overrides);
        }
        catch (Exception)
        {
            invalid = true;
            return null;
        }
    }

    private static void TryWrite(string path, FallbackProfile source, FallbackDelta? delta)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            SqueakFallbackProfileCopy copy = new()
            {
                packageId = UniversalSqueakerMod.PackageId,
                sourceVersion = source.Version,
                hasOverrides = delta != null,
            };
            if (delta != null)
            {
                foreach (KeyValuePair<string, string> entry in delta.Overrides)
                    copy.overrides.Add(new SqueakFallbackProfileOverride { actionKey = entry.Key, soundKey = entry.Value });
            }
            SafeSaver.Save(path, DocumentElementName, () =>
            {
                SqueakFallbackProfileCopy? saveable = copy;
                Scribe_Deep.Look(ref saveable, "FallbackProfile");
            });
        }
        catch (Exception ex)
        {
            SqueakLog.FallbackProfileStoreFailed(source.Race.DefName, ex);
        }
    }

    private static string PathFor(RaceKey race)
    {
        return Path.Combine(GenFilePaths.ConfigFolderPath,
            FilePrefix + GenText.SanitizeFilename(race.DefName) + FileSuffix);
    }
}
