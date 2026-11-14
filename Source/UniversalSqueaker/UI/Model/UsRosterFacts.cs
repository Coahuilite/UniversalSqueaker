using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>Read-only configuration facts, not eligibility, resource readiness or playback results.</summary>
public sealed class UsConfiguredSupplyPack
{
    public readonly string Key;
    public readonly bool Selected;
    public readonly IReadOnlyList<string> Mappings;
    public UsConfiguredSupplyPack(string key, bool selected, IEnumerable<string> mappings)
    { Key = key; Selected = selected; Mappings = Array.AsReadOnly(mappings.OrderBy(x => x, StringComparer.Ordinal).ToArray()); }
}

public sealed class UsConfiguredSupply
{
    public readonly IReadOnlyList<UsConfiguredSupplyPack> ExactRegular, RaceRegular, ExactFallback;
    public readonly SqueakVoicePackMode Mode;
    public readonly bool HasTable;
    public readonly int TableMappings;
    public UsConfiguredSupply(IEnumerable<UsConfiguredSupplyPack> exactRegular, IEnumerable<UsConfiguredSupplyPack> raceRegular,
        IEnumerable<UsConfiguredSupplyPack> exactFallback, SqueakVoicePackMode mode, bool hasTable, int tableMappings)
    {
        ExactRegular = Array.AsReadOnly(exactRegular.ToArray()); RaceRegular = Array.AsReadOnly(raceRegular.ToArray());
        ExactFallback = Array.AsReadOnly(exactFallback.ToArray()); Mode = mode; HasTable = hasTable; TableMappings = tableMappings;
    }
}

/// <summary>Immutable directory/domain join. No admission, selection or audio reconstruction.</summary>
public sealed class UsRosterTarget
{
    public readonly string Key, Race, Xeno, Label, SourceId, SourceName;
    public readonly UsRaceIdentity? RaceIdentity;
    public readonly VoicePackDomainView Domain;
    public readonly bool Loose, LoadedRace, Legal;
    public readonly int Tier;
    public bool Editable => !Loose && LoadedRace && Legal && !Domain.IsDormant && !Domain.IsTargetUnavailable && !Domain.HasCanonicalConflict;
    public int SelectedCount => Domain.Packs.Count(x => x.IsSelected);
    public bool Issue => !LoadedRace && !Loose || !Legal && !Loose || Domain.IsDormant || Domain.IsTargetUnavailable || Domain.HasCanonicalConflict || Domain.OrphanCount > 0;
    public UsRosterTarget(string race, string xeno, string label, string sourceId, string sourceName,
        UsRaceIdentity? raceIdentity, VoicePackDomainView domain, bool loose = false, bool legal = true)
    {
        Race = race; Xeno = xeno; Key = loose ? "L|" + sourceId + "|" + xeno : race + (xeno == "" ? "" : "|" + xeno);
        Label = label; SourceId = sourceId; SourceName = sourceName; RaceIdentity = raceIdentity;
        Domain = new VoicePackDomainView(domain.Scope, domain.RaceDefName, domain.TargetDefName, domain.DisplayName,
            domain.SourceText, domain.State, domain.IsDormant, domain.IsTargetUnavailable, domain.HasCanonicalConflict,
            domain.EnabledCount, domain.CandidateCount, domain.OrphanCount, Array.AsReadOnly(domain.EnabledKeys.ToArray()),
            Array.AsReadOnly(domain.Packs.ToArray()), domain.RaceDisplay);
        Loose = loose; LoadedRace = raceIdentity.HasValue; Legal = legal;
        Tier = loose ? 8 : !LoadedRace ? 9 : xeno != "" ? 7 : raceIdentity!.Value.IsHuman ? 1 : UsBoardBrowsePolicy.Tier(raceIdentity.Value);
    }
    public bool InGroup(UsBoardBrowsePolicy.BoardGroup group)
    {
        if (Loose) return group == UsBoardBrowsePolicy.BoardGroup.PlayerRaces || group == UsBoardBrowsePolicy.BoardGroup.Xenotypes || group == UsBoardBrowsePolicy.BoardGroup.All;
        if (Xeno != "" && group == UsBoardBrowsePolicy.BoardGroup.Xenotypes) return true;
        if (Xeno != "" && group == UsBoardBrowsePolicy.BoardGroup.Races) return false;
        return RaceIdentity.HasValue ? UsBoardBrowsePolicy.InGroup(RaceIdentity.Value, group) : group == UsBoardBrowsePolicy.BoardGroup.All;
    }
    public bool Matches(string query) => UsBrowseListView.Contains(Label + "\n" + Race + "\n" + Xeno + "\n" + SourceId + "\n" + SourceName
        + "\n" + string.Join("\n", Domain.Packs.Select(x => x.SearchText)), query);
}

