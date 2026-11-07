using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker.UI;

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
