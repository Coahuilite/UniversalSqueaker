using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// OVT-002 (PM ownership decision, moved OUT of Pure by the CO in-flight review): the PLAYER-FACING
/// browse policy - the board's groups, chips, tier order, name/source search and the source display
/// word. This is UI experience strategy, not a kernel duty: the pure side keeps only identity,
/// source and basic life-kind facts, and the discovery contract (the directory lists everything
/// loaded) can never be bent by a display preference - a def that sorts last is still in the
/// directory, and a def excluded from the current group is still counted in the totals line.
///
/// <para>
/// The axes OVERLAP by design (OVT-002 "分类维度可重叠"): a mechanoid from a HAR mod keeps its HAR
/// framework fact AND its mechanoid life kind, is reachable in the default Player races group (the
/// HAR half of "HAR mod races first" does not expire at the life-kind border), shows up under the
/// HAR chip, and under Creatures too. Nothing is ever lost because two axes answer differently.
/// </para>
/// </summary>
public static class UsBoardBrowsePolicy
{
    /// <summary>The board's top-level browse groups, in tab order.</summary>
    public enum BoardGroup
    {
        /// <summary>Default: what a player races as - HAR mod races first (any life kind), vanilla
        /// humanlike and the xenotype dimension next, other third-party humanlike after. NEVER
        /// silently falls back to an animal-first screen when no HAR is loaded.</summary>
        PlayerRaces = 0,
        /// <summary>Animals, mechanoids and Anomaly entities, distinguishable by kind.</summary>
        Creatures = 1,
        /// <summary>Everything the directory holds.</summary>
        All = 2,
    }

    /// <summary>The compact chips inside a group (OVT-002: HAR / vanilla races / vanilla creatures
    /// reachable separately; DLC vs third-party stays a visible per-row source fact + search).</summary>
    public enum BoardFilter
    {
        None = 0,
        Har = 1,
        VanillaRaces = 2,
        VanillaCreatures = 3,
    }

    /// <summary>
    /// Stable per-def tier inside the ordering (lower first). PM mech-HAR addendum: the HAR half
    /// of "HAR mod races first" is the FRAMEWORK axis and leads for EVERY life kind - a mechanoid
    /// built on the HAR framework sorts BEFORE vanilla humanlike and every other non-HAR row, not
    /// merely "visible in the group". Within the tier the order is display label then stable
    /// defName; Ratkin is only an acceptance EXAMPLE - nothing here names any mod or test id.
    /// </summary>
    public static int Tier(in UsRaceIdentity race)
    {
        if (race.IsHar) return 0;                          // HAR framework identity leads, any kind
        if (race.IsVanilla && race.IsHumanlike) return 1;  // vanilla humanlike (Human) next
        if (race.IsHumanlike) return 2;                    // other third-party humanlike, still visible
        if (race.IsAnimal) return 3;
        if (race.IsMechanoid) return 4;
        if (race.IsAnomalyEntity) return 5;
        return 6;                                          // loaded-but-unclassified: never dropped
    }

    /// <summary>The full-view order: tier, then display label (ordinal), then the stable defName -
    /// same-label different-ID rows can never merge or shuffle.</summary>
    public static int Compare(in UsRaceIdentity left, in UsRaceIdentity right)
    {
        int byTier = Tier(left).CompareTo(Tier(right));
        if (byTier != 0) return byTier;
        int byLabel = string.CompareOrdinal(left.Label, right.Label);
        if (byLabel != 0) return byLabel;
        return string.CompareOrdinal(left.DefName, right.DefName);
    }

    public static bool InGroup(in UsRaceIdentity race, BoardGroup group)
    {
        switch (group)
        {
            case BoardGroup.PlayerRaces:
                // Tier <= 2 already holds EVERY HAR identity (PM addendum): the mechanoid HAR sits
                // in the leading HAR tier, so membership and ORDER agree - no separate visibility
                // patch, and the life kind never drops the framework identity the player came for.
                return Tier(race) <= 2;
            case BoardGroup.Creatures:
                return race.IsAnimal || race.IsMechanoid || race.IsAnomalyEntity;
            default:
                return true;
        }
    }

    public static bool InFilter(in UsRaceIdentity race, BoardFilter filter)
    {
        switch (filter)
        {
            case BoardFilter.Har: return race.IsHar;
            case BoardFilter.VanillaRaces:
                return race.IsVanilla && race.IsHumanlike;
            case BoardFilter.VanillaCreatures:
                return race.IsVanilla && (race.IsAnimal || race.IsMechanoid || race.IsAnomalyEntity);
            default: return true;
        }
    }

    /// <summary>Name/source search over plain identity text (ordinal contains; the source package
    /// id, the source package NAME and the display/def names are all searchable - OVT-002
    /// "名称/来源搜索" with the real mod identity, not one collapsed enum word).</summary>
    public static bool MatchesSearch(in UsRaceIdentity race, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return true;
        return Contains(race.Label, needle)
            || Contains(race.DefName, needle)
            || Contains(race.SourcePackageId, needle)
            || Contains(race.SourcePackageName, needle);
    }

    public static bool MatchesXenotypeSearch(in UsXenotypeIdentity xeno, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return true;
        return Contains(xeno.Label, needle)
            || Contains(xeno.DefName, needle)
            || Contains(xeno.SourcePackageId, needle)
            || Contains(xeno.SourcePackageName, needle);
    }

    /// <summary>The short display word for the source axis (the chip; the full package id/name
    /// ride the row's search and help). English machine word - the WIDGET renders the Keyed
    /// per-kind entry; this string is only the fallback label for data-level consumers.</summary>
    public static string SourceWord(UsRaceSourceKind source)
    {
        switch (source)
        {
            case UsRaceSourceKind.Core: return "Core";
            case UsRaceSourceKind.OfficialDlc: return "DLC";
            case UsRaceSourceKind.ThirdParty: return "MOD";
            default: return "?";
        }
    }

    private static bool Contains(string haystack, string needle)
    {
        return !string.IsNullOrEmpty(haystack)
            && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

}
