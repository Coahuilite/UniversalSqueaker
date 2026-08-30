using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Pure filter state for voice-pack domain rows. Zero Verse/Unity dependencies.
/// An empty filter (all flags false) matches everything.
/// </summary>
public readonly struct UiDomainFilter
{
    public readonly bool EnabledOnly;
    public readonly bool ConflictOnly;
    public readonly bool OrphanOnly;

    public UiDomainFilter(bool enabledOnly = false, bool conflictOnly = false, bool orphanOnly = false)
    {
        EnabledOnly = enabledOnly;
        ConflictOnly = conflictOnly;
        OrphanOnly = orphanOnly;
    }
}

/// <summary>
/// Pure filter state for voice-pack rows inside a domain. Zero Verse/Unity dependencies.
/// </summary>
public readonly struct UiPackFilter
{
    public readonly string Author;

    public UiPackFilter(string? author = null)
    {
        Author = author ?? "";
    }
}

/// <summary>
/// Predicates for the VoicePacks filtering UI. All methods are deterministic pure functions.
/// </summary>
public static class VoicePacksFilters
{
    public static bool DomainMatches(
        bool hasConflict,
        bool isDormant,
        bool isTargetUnavailable,
        bool isOrphan,
        bool isEnabled,
        in UiDomainFilter filter)
    {
        if (filter.ConflictOnly && !(hasConflict || isDormant || isTargetUnavailable || isOrphan))
        {
            return false;
        }

        if (filter.OrphanOnly && !isOrphan)
        {
            return false;
        }

        if (filter.EnabledOnly && !isEnabled)
        {
            return false;
        }

        return true;
    }

    public static bool PackMatches(
        string? author,
        string? modName,
        in UiPackFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.Author)
            && !ContainsIgnoreCase(author, filter.Author)
            && !ContainsIgnoreCase(modName, filter.Author))
        {
            return false;
        }

        return true;
    }

    /// <summary>True when a race filter (empty = All) accepts the given race.</summary>
    public static bool RaceFilterMatches(string? filter, string raceDefName)
    {
        return string.IsNullOrEmpty(filter)
            || string.Equals(filter, raceDefName, StringComparison.Ordinal);
    }

    /// <summary>True when a (race,xenotype) domain passes both parallel race and xenotype filters.</summary>
    public static bool XenotypeFilterMatches(
        string? raceFilter,
        string? xenotypeFilter,
        string domainRaceDefName,
        string domainXenotypeDefName)
    {
        return RaceFilterMatches(raceFilter, domainRaceDefName)
            && (string.IsNullOrEmpty(xenotypeFilter)
                || string.Equals(xenotypeFilter, domainXenotypeDefName, StringComparison.Ordinal));
    }

    private static bool ContainsIgnoreCase(string? value, string search)
    {
        if (value == null || value.Length == 0)
        {
            return false;
        }

        return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
