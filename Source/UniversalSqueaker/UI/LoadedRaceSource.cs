using System;
using System.Collections.Generic;

using UniversalSqueaker;

namespace UniversalSqueaker.UI;

/// <summary>
/// The facts seam behind the board (UIR-007 / CORRECTIONS-R1 topic 1, OVT-001 re-cut): Collect runs
/// once per VIEW BUILD (the view itself is revision-cached), never per GUI pass, and answers a
/// plain facts BUNDLE - race rows AND the xenotype directory rows. The DEFINITION authority moved
/// out of this seam: identities come from the published <see cref="RaceDefinitionCatalog"/>
/// snapshot (collected once at the definition-ready entry), so this file no longer enumerates the
/// def database at all - no second discovery path, no per-adapter reflection, no corpse boundary
/// to get wrong here. What stays is the SELECTION/supply fact merge: catalog packs, per-domain
/// statuses from the settings, the Biotech gate answer and the fallback projection.
/// Harnesses replace the seam wholesale; production defaults to <see cref="VerseRaceBoardSource"/>.
/// Reading plain snapshots changes no admission rule and no routing authority.
/// </summary>
public interface IUsRaceBoardFactsSource
{
    UsRaceBoardFactsBundle Collect(
        UniversalSqueakerSettings settings,
        IReadOnlyList<FallbackRaceView> fallbackRaces,
        bool biotechActive);
}

/// <summary>
/// The thin facts adapter behind the overview board (UIR-002/003, OVT-001/003): it merges the
/// PUBLISHED directory snapshot (identity/source/HAR/life-kind facts, Verse-free by construction)
/// with the sound supply (the voice catalog's pack declarations), the per-domain selection
/// statuses (settings) and the final-fallback answer (the caller passes the SAME supported-race
/// list the fallback editor and the routing read). The projection over the plain bundle is
/// ordinary data logic. Xenotype supply is attributed by each pack's OWN declared raceDefName -
/// never a guessed XenotypeDef.race, which does not exist.
/// </summary>
public sealed class VerseRaceBoardSource : IUsRaceBoardFactsSource
{
    /// <summary>The single production instance the model defaults to.</summary>
    public static readonly VerseRaceBoardSource Instance = new VerseRaceBoardSource();

    public UsRaceBoardFactsBundle Collect(
        UniversalSqueakerSettings settings,
        IReadOnlyList<FallbackRaceView> fallbackRaces,
        bool biotechActive)
    {
        UsDefinitionSnapshot directory = RaceDefinitionCatalog.Current;
        SqueakXenotypeCatalogSnapshot catalog = SqueakXenotypeCatalog.Current;

        // Xenotype supply grouped by the pack's OWN declared raceDefName (the exact identity
        // CollectXenotypeDomains uses; the CO-confirmed correction, not redone here).
        var xenoPacksByRace = new Dictionary<string, List<SqueakVoicePackDef>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, IReadOnlyList<SqueakVoicePackDef>> group in catalog.XenotypePacksByDefName)
        {
            foreach (SqueakVoicePackDef pack in group.Value)
            {
                if (pack == null || string.IsNullOrEmpty(pack.raceDefName)) continue;
                if (!xenoPacksByRace.TryGetValue(pack.raceDefName, out List<SqueakVoicePackDef>? list))
                {
                    list = new List<SqueakVoicePackDef>();
                    xenoPacksByRace.Add(pack.raceDefName, list);
                }

                list.Add(pack);
            }
        }

        var fallbackByRace = new Dictionary<string, FallbackRaceView>(StringComparer.Ordinal);
        if (fallbackRaces != null)
        {
            for (int i = 0; i < fallbackRaces.Count; i++)
            {
                if (!string.IsNullOrEmpty(fallbackRaces[i].DefName) && !fallbackByRace.ContainsKey(fallbackRaces[i].DefName))
                {
                    fallbackByRace.Add(fallbackRaces[i].DefName, fallbackRaces[i]);
                }
            }
        }

        var races = new List<UsRaceBoardFacts>(directory.Races.Count);
        for (int i = 0; i < directory.Races.Count; i++)
        {
            UsRaceIdentity identity = directory.Races[i];
            if (string.IsNullOrEmpty(identity.DefName)) continue;

            var packs = new List<UsBoardPackFact>();
            var domains = new List<UsRaceDomainFact>();

            IReadOnlyList<SqueakVoicePackDef> racePacks =
                catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, identity.DefName) ?? Array.Empty<SqueakVoicePackDef>();
            for (int p = 0; p < racePacks.Count; p++)
            {
                if (racePacks[p].TryGetPackKey(out string key))
                {
                    packs.Add(new UsBoardPackFact(SqueakVoicePackScope.Race, "", key, PackDisplay(racePacks[p]), "", biotechActive));
                }
            }

            SqueakVoicePackDomainStatus raceStatus =
                settings.GetVoicePackSelectionStatus(SqueakVoicePackScope.Race, identity.DefName);
            domains.Add(new UsRaceDomainFact(
                SqueakVoicePackScope.Race, "", raceStatus.State, raceStatus.EnabledKeys ?? Array.Empty<string>()));

            if (xenoPacksByRace.TryGetValue(identity.DefName, out List<SqueakVoicePackDef>? xenoPacks))
            {
                // One domain fact per distinct target this race's xeno packs name; the settings layer
                // answers each target's status (dormant without Biotech, target-unavailable when the
                // xenotype def is gone, orphan when a saved key's pack is gone).
                var seenTargets = new HashSet<string>(StringComparer.Ordinal);
                for (int x = 0; x < xenoPacks.Count; x++)
                {
                    SqueakVoicePackDef pack = xenoPacks[x];
                    string target = pack.targetDefName ?? "";
                    if (pack.TryGetPackKey(out string key))
                    {
                        packs.Add(new UsBoardPackFact(
                            SqueakVoicePackScope.Xenotype, target, key, PackDisplay(pack),
                            XenoDisplay(directory, target), biotechActive));
                    }

                    if (!seenTargets.Add(target) || target.Length == 0) continue;
                    SqueakVoicePackDomainStatus xenoStatus =
                        settings.GetVoicePackSelectionStatus(SqueakVoicePackScope.Xenotype, identity.DefName, target);
                    domains.Add(new UsRaceDomainFact(
                        SqueakVoicePackScope.Xenotype, target, xenoStatus.State, xenoStatus.EnabledKeys ?? Array.Empty<string>()));
                }
            }

            fallbackByRace.TryGetValue(identity.DefName, out FallbackRaceView fallback);
            races.Add(new UsRaceBoardFacts(
                identity.DefName,
                identity.Label,
                packs,
                domains,
                hasFallbackTable: !string.IsNullOrEmpty(fallback.DefName),
                fallbackIsPlayerTable: fallback.IsPlayerTable,
                fallbackSoundCount: fallback.SoundCount,
                identity: identity));
        }

        // OVT-003: the xenotype DIRECTORY - every loaded XenotypeDef from the snapshot, whether or
        // not any pack names it. Supply/enabled facts come only from real pack declarations.
        var xenotypes = new List<UsXenotypeBoardFacts>(directory.Xenotypes.Count);
        for (int i = 0; i < directory.Xenotypes.Count; i++)
        {
            UsXenotypeIdentity xeno = directory.Xenotypes[i];
            if (string.IsNullOrEmpty(xeno.DefName)) continue;

            int packCount = 0;
            int enabledCount = 0;
            string quickKey = "";
            string quickLabel = "";
            string quickRace = "";
            bool quickAmbiguous = false;

            foreach (KeyValuePair<string, List<SqueakVoicePackDef>> pair in xenoPacksByRace)
            {
                string raceDefName = pair.Key;
                SqueakVoicePackDomainStatus? status = null;
                for (int p = 0; p < pair.Value.Count; p++)
                {
                    SqueakVoicePackDef pack = pair.Value[p];
                    if (!string.Equals(pack.targetDefName ?? "", xeno.DefName, StringComparison.Ordinal)) continue;
                    if (!pack.TryGetPackKey(out string key)) continue;
                    packCount++;
                    status ??= settings.GetVoicePackSelectionStatus(
                        SqueakVoicePackScope.Xenotype, raceDefName, xeno.DefName);
                    bool on = status.Value.EnabledKeys != null
                        && ContainsOrdinal(status.Value.EnabledKeys, key);
                    if (on) enabledCount++;
                    else if (!quickAmbiguous && biotechActive)
                    {
                        // The same exactness rule as the race cards: exactly ONE not-yet-enabled pack
                        // (any race) keeps a quick-enable candidate; a second one retires it for good -
                        // navigation stays the honest action. PM dormant-contract gap: with Biotech
                        // off the whole xeno axis is dormant-by-environment, so NO candidate is ever
                        // collected here (supply counts and saved facts stay complete regardless).
                        if (quickKey.Length == 0) { quickKey = key; quickLabel = PackDisplay(pack); quickRace = raceDefName; }
                        else { quickAmbiguous = true; quickKey = ""; quickLabel = ""; quickRace = ""; }
                    }
                }

             }

            // Unambiguous race context: exactly ONE race's packs name this target.
            int namingRaces = 0;
            string onlyRace = "";
            foreach (KeyValuePair<string, List<SqueakVoicePackDef>> pair in xenoPacksByRace)
            {
                for (int p = 0; p < pair.Value.Count; p++)
                {
                    SqueakVoicePackDef pack = pair.Value[p];
                    if (string.Equals(pack.targetDefName ?? "", xeno.DefName, StringComparison.Ordinal))
                    {
                        if (namingRaces == 0) onlyRace = pair.Key;
                        namingRaces++;
                        break;
                    }
                }

                if (namingRaces > 1) break;
            }

            string raceContext = namingRaces == 1 ? onlyRace : "";
            if (namingRaces != 1) { quickKey = ""; quickLabel = ""; quickRace = ""; }
            if (quickKey.Length == 0) quickRace = "";

            xenotypes.Add(new UsXenotypeBoardFacts(
                xeno, packCount, enabledCount, raceContext,
                quickKey, quickLabel ?? "", quickRace));
        }

        return new UsRaceBoardFactsBundle(races, xenotypes);
    }

    private static bool ContainsOrdinal(IReadOnlyList<string> keys, string key)
    {
        for (int i = 0; i < keys.Count; i++)
        {
            if (string.Equals(keys[i], key, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static string PackDisplay(SqueakVoicePackDef pack)
    {
        string label = pack.label ?? "";
        return label.Length > 0 ? label : (pack.defName ?? "");
    }

    /// <summary>Xenotype display names come from the PUBLISHED snapshot (no def-database read on
    /// this path); an unknown target answers its defName (a pack may name a target that is gone).</summary>
    private static string XenoDisplay(UsDefinitionSnapshot directory, string targetDefName)
    {
        for (int i = 0; i < directory.Xenotypes.Count; i++)
        {
            if (string.Equals(directory.Xenotypes[i].DefName, targetDefName, StringComparison.Ordinal))
            {
                return directory.Xenotypes[i].Label;
            }
        }

        return targetDefName;
    }
}
