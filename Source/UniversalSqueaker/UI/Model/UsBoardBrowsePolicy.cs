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
/// Framework and life kind are independent: HAR controls sorting/chips, never admission to the
/// default humanlike scope. Animals and other kinds remain reachable through explicit scopes.
/// </para>
/// </summary>
public static class UsBoardBrowsePolicy
{
    /// <summary>The board's top-level browse groups, in tab order.</summary>
    public enum BoardGroup
    {
        /// <summary>Default: humanlike races plus legal xenotypes.</summary>
        PlayerRaces = 0,
        /// <summary>Animals only; other life kinds have their own explicit scope.</summary>
        Creatures = 1,
        /// <summary>Everything the directory holds.</summary>
        All = 2,
        Races = 3,
        Xenotypes = 4,
        Other = 5,
        HarHumanlike = 6,
        Human = 7,
    }
    public static string GroupLabelKey(BoardGroup group) => group switch
    {
        BoardGroup.PlayerRaces => "US.Browse.Group.PlayerRaces", BoardGroup.Races => "US.Browse.Group.Races",
        BoardGroup.Xenotypes => "US.Browse.Group.Xenotypes", BoardGroup.Creatures => "US.Browse.Group.Creatures",
        BoardGroup.Other => "US.Browse.Group.Other", BoardGroup.HarHumanlike => "US.Browse.Group.HarHumanlike",
        BoardGroup.Human => "US.Browse.Group.Human", _ => "US.Browse.Group.All"
    };

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
            case BoardGroup.Races:
                return race.IsHumanlike;
            case BoardGroup.Xenotypes:
                return false;
            case BoardGroup.Creatures:
                return race.IsAnimal;
            case BoardGroup.Other:
                return !race.IsHumanlike && !race.IsAnimal;
            case BoardGroup.HarHumanlike:
                return race.IsHumanlike && race.IsHar && !race.IsHuman;
            case BoardGroup.Human:
                return race.IsHuman;
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
