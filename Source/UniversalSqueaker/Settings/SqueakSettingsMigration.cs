using System;
using System.Collections.Generic;

namespace UniversalSqueaker;

/// <summary>
/// Pure v3/v1 → v4/v2 settings migration. It only creates replacement lists; callers publish them and
/// advance schema markers after this method returns success, so a malformed record cannot half-migrate Config.
/// </summary>
internal static class SqueakSettingsMigration
{

    internal static bool TryCreateV4Records(
        IEnumerable<VoicePackSelectionRecord>? sourceSelections,
        IEnumerable<XenotypePresetRecord>? sourcePresets,
        string legacyDefaultRaceDefName,
        out List<VoicePackSelectionRecord> selections,
        out List<XenotypePresetRecord> presets,
        out string failure)
    {
        selections = new List<VoicePackSelectionRecord>();
        presets = new List<XenotypePresetRecord>();
        failure = "";
        // US has no product legacy race. An empty default is allowed at the transaction level; a record
        // that actually needs the default will fail individually, so explicit-race records can still migrate.

        try
        {
            List<VoicePackSelectionRecord> normalizedSelections = new();
            foreach (VoicePackSelectionRecord? source in sourceSelections ?? Array.Empty<VoicePackSelectionRecord>())
            {
                if (!TryNormalizeSelection(source, legacyDefaultRaceDefName, out VoicePackSelectionRecord? normalized, out failure)) return false;
                normalizedSelections.Add(normalized!);
            }

            // A selection is whole-domain last-wins. Walk backward so a duplicate's final record and its
            // original relative ordering are both retained without dropping any still-effective PackKey.
            HashSet<SelectionIdentity> retainedDomains = new();
            for (int i = normalizedSelections.Count - 1; i >= 0; i--)
            {
                VoicePackSelectionRecord normalized = normalizedSelections[i];
                SelectionIdentity identity = new(normalized.scope, normalized.raceDefName, normalized.xenotypeDefName);
                if (retainedDomains.Add(identity)) selections.Add(normalized);
            }
            selections.Reverse();

            // Presets are field-presence deltas. Multiple records for one domain are intentionally retained:
            // their list order is the existing field-level last-wins merge contract.
            foreach (XenotypePresetRecord? source in sourcePresets ?? Array.Empty<XenotypePresetRecord>())
            {
                if (source == null)
                {
                    failure = "xenotypePresets contains a null record.";
                    return false;
                }

                XenotypePresetRecord normalized = XenotypePresetRecord.Clone(source);
                if (string.IsNullOrWhiteSpace(normalized.xenotypeDefName))
                {
                    failure = "A Xenotype preset has no xenotypeDefName.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(normalized.raceDefName)) normalized.raceDefName = legacyDefaultRaceDefName;
                if (string.IsNullOrWhiteSpace(normalized.raceDefName))
                {
                    failure = "A Xenotype preset has no raceDefName and no legacy default race is configured.";
                    return false;
                }
                presets.Add(normalized);
            }

            return true;
        }
        catch (Exception ex)
        {
            selections = new List<VoicePackSelectionRecord>();
            presets = new List<XenotypePresetRecord>();
            failure = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static bool TryNormalizeSelection(
        VoicePackSelectionRecord? source,
        string legacyDefaultRaceDefName,
        out VoicePackSelectionRecord? normalized,
        out string failure)
    {
        normalized = null;
        failure = "";
        if (source == null)
        {
            failure = "voicePackSelections contains a null record.";
            return false;
        }

        string raceDefName = source.raceDefName ?? "";
        string xenotypeDefName = source.xenotypeDefName ?? "";
        string legacyTargetDefName = source.legacyTargetDefName ?? "";
        switch (source.scope)
        {
            case SqueakVoicePackScope.Race:
                if (!string.IsNullOrEmpty(xenotypeDefName) || !string.IsNullOrEmpty(legacyTargetDefName))
                {
                    failure = "A Race selection contains a Xenotype target.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(raceDefName)) raceDefName = legacyDefaultRaceDefName;
                if (string.IsNullOrWhiteSpace(raceDefName))
                {
                    failure = "A Race selection has no raceDefName and no legacy default race is configured.";
                    return false;
                }
                xenotypeDefName = "";
                break;

            case SqueakVoicePackScope.Xenotype:
                if (!string.IsNullOrEmpty(legacyTargetDefName))
                {
                    if (!string.IsNullOrEmpty(xenotypeDefName) && !string.Equals(xenotypeDefName, legacyTargetDefName, StringComparison.Ordinal))
                    {
                        failure = "A Xenotype selection has conflicting legacy and v2 targets.";
                        return false;
                    }
                    xenotypeDefName = legacyTargetDefName;
                }
                if (string.IsNullOrWhiteSpace(raceDefName)) raceDefName = legacyDefaultRaceDefName;
                if (string.IsNullOrWhiteSpace(raceDefName))
                {
                    failure = "A Xenotype selection has no raceDefName and no legacy default race is configured.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(xenotypeDefName))
                {
                    failure = "A Xenotype selection has no xenotypeDefName.";
                    return false;
                }
                break;

            default:
                failure = "A VoicePack selection has an invalid scope.";
                return false;
        }

        normalized = new VoicePackSelectionRecord
        {
            scope = source.scope,
            raceDefName = raceDefName,
            xenotypeDefName = xenotypeDefName,
            enabledPackKeys = new List<string>(source.enabledPackKeys ?? new List<string>()),
        };
        return true;
    }

    private readonly struct SelectionIdentity : IEquatable<SelectionIdentity>
    {
        private readonly SqueakVoicePackScope scope;
        private readonly string raceDefName;
        private readonly string xenotypeDefName;

        internal SelectionIdentity(SqueakVoicePackScope scope, string raceDefName, string xenotypeDefName)
        {
            this.scope = scope;
            this.raceDefName = raceDefName;
            this.xenotypeDefName = xenotypeDefName;
        }

        public bool Equals(SelectionIdentity other) => scope == other.scope
            && string.Equals(raceDefName, other.raceDefName, StringComparison.Ordinal)
            && string.Equals(xenotypeDefName, other.xenotypeDefName, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SelectionIdentity other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)scope;
                hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(raceDefName ?? "");
                return hash * 397 ^ StringComparer.Ordinal.GetHashCode(xenotypeDefName ?? "");
            }
        }
    }
}
