using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker.UI;

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

public static partial class VoicePacksPageModel
{
    private static IReadOnlyList<UsRosterTarget> BuildRoster(GameSettings settings, SqueakXenotypeCatalogSnapshot catalog,
        VoicePacksPageState state, IReadOnlyList<VoicePackDomainView> xenotypes)
    {
        var definitions = RaceDefinitionCatalog.Current;
        var races = definitions.Races.ToDictionary(x => x.DefName, StringComparer.Ordinal);
        var rows = new List<UsRosterTarget>();
        var configuration = settings.CaptureBehaviorConfiguration();
        VoicePackDomainView Canonical(VoicePackDomainView d) => new(d.Scope, d.RaceDefName, d.TargetDefName, d.DisplayName,
            d.SourceText, d.State, d.IsDormant, d.IsTargetUnavailable, catalog.AmbiguousCanonicalDefNames.Contains(d.TargetDefName),
            d.EnabledCount, d.CandidateCount, d.OrphanCount, d.EnabledKeys, d.Packs, d.RaceDisplay);
        bool Legal(string race, string child)
        {
            var identity = new AudioDomain(new RaceKey(race), new XenotypeKey(child));
            return configuration.HasDomain(identity) || SqueakerRuntime.Audio.HasDomain(identity) || SqueakerRuntime.BehaviorSources.HasDomain(identity);
        }
        var domains = xenotypes.ToList();
        // Existing owner lookups establish exact author/config-only pairs too. There is no xeno.race.
        var seen = new HashSet<string>(domains.Select(d => d.RaceDefName + "\n" + d.TargetDefName), StringComparer.Ordinal);
        foreach (var race in definitions.Races)
        foreach (var child in definitions.Xenotypes)
            if (Legal(race.DefName, child.DefName) && seen.Add(race.DefName + "\n" + child.DefName))
                domains.Add(Canonical(BuildEmptyXenotypeDomain(settings, race.DefName, child.DefName)));
        var raceNames = definitions.Races.Select(x => x.DefName).Concat(catalog.RaceDefNames)
            .Concat(settings.voicePackSelections.Where(x => x != null).Select(x => x.raceDefName))
            .Concat(domains.Select(x => x.RaceDefName)).Where(x => x != "").Distinct(StringComparer.Ordinal);
        foreach (string race in raceNames)
        {
            bool loaded = races.TryGetValue(race, out var identity);
            var domain = BuildRaceDomain(settings, catalog, race);
            rows.Add(new UsRosterTarget(race, "", domain.DisplayName, loaded ? identity.SourcePackageId : "",
                loaded ? identity.SourcePackageName : "", loaded ? identity : null, domain));
        }
        foreach (var domain in domains)
        {
            bool loaded = races.TryGetValue(domain.RaceDefName, out var identity);
            var sources = definitions.Xenotypes.Where(x => x.DefName == domain.TargetDefName).ToArray();
            // A conflict remains one exact domain with every conflicting source shown; no arbitrary winner.
            rows.Add(new UsRosterTarget(domain.RaceDefName, domain.TargetDefName,
                domain.DisplayName + " (" + domain.RaceDisplay + ")", sources.Length == 1 ? sources[0].SourcePackageId : "",
                string.Join(" · ", sources.Select(x => x.SourcePackageName + " [" + x.SourcePackageId + "]")),
                loaded ? identity : null, domain, legal: Legal(domain.RaceDefName, domain.TargetDefName)));
        }
        foreach (var xeno in definitions.Xenotypes.Where(x => !domains.Any(d => d.TargetDefName == x.DefName)))
            rows.Add(new UsRosterTarget("", xeno.DefName, xeno.Label, xeno.SourcePackageId, xeno.SourcePackageName, null,
                Canonical(BuildEmptyXenotypeDomain(settings, "", xeno.DefName)), true));
        rows.Sort((a, b) => { int tier = a.Tier.CompareTo(b.Tier); return tier != 0 ? tier : StringComparer.Ordinal.Compare(a.Label + a.Key, b.Label + b.Key); });
        return Array.AsReadOnly(rows.ToArray());
    }
}
