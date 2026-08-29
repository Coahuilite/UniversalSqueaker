using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Pure filter state for voice-pack domain rows. Zero Verse/Unity dependencies.
/// An empty filter (all flags false and Author empty) matches everything.
/// </summary>
public readonly struct UiDomainFilter
{
    public readonly bool EnabledOnly;
    public readonly bool ConflictOnly;
    public readonly bool OrphanOnly;
    public readonly string Author;

    public UiDomainFilter(bool enabledOnly = false, bool conflictOnly = false, bool orphanOnly = false, string? author = null)
    {
        EnabledOnly = enabledOnly;
        ConflictOnly = conflictOnly;
        OrphanOnly = orphanOnly;
        Author = author ?? "";
    }
}

/// <summary>
/// Pure filter state for voice-pack rows inside a domain. Zero Verse/Unity dependencies.
/// </summary>
public readonly struct UiPackFilter
{
    public readonly bool EnabledOnly;
    public readonly string Author;

    public UiPackFilter(bool enabledOnly = false, string? author = null)
    {
        EnabledOnly = enabledOnly;
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
        string? author,
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

        if (!string.IsNullOrEmpty(filter.Author) && !ContainsIgnoreCase(author, filter.Author))
        {
            return false;
        }

        return true;
    }

    public static bool PackMatches(
        string? author,
        string? modName,
        bool isSelected,
        in UiPackFilter filter)
    {
        if (filter.EnabledOnly && !isSelected)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filter.Author)
            && !ContainsIgnoreCase(author, filter.Author)
            && !ContainsIgnoreCase(modName, filter.Author))
        {
            return false;
        }

        return true;
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
