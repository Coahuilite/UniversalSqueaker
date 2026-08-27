using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Incremental baseline-preset importer (方案 C: 分层预设 + 增量导入). Reads a read-only
/// <see cref="UniversalSqueakerTuningBaselineDef"/> tree and merges the player-selected race/xenotype
/// rows into the settings' layered tuning tables:
///   - actions → <see cref="UniversalSqueakerSettings.actionTuning"/> (Race layer 1 / Xenotype layer 2)
///   - moods   → <see cref="UniversalSqueakerSettings.moodOverrides"/> (global, for race rows) and
///               <see cref="XenotypePresetRecord.moodOverrides"/> (per-xenotype, for xenotype rows)
/// Merge semantics are "same key overwrites, new key appends", both within the preset (race baseline →
/// xenotype delta when <see cref="BaselineXenotypeEntry.inheritFromRace"/>) and against the existing
/// settings rows. Every imported <see cref="ActionTuningRecord"/> carries
/// <see cref="ActionTuningRecord.sourcePresetDefName"/>.
/// </summary>
public static class BaselinePresetImporter
{
    /// <summary>Player-checked rows to import, keyed by defName.</summary>
    public sealed class Selection
    {
        public readonly HashSet<string> RaceDefNames = new(StringComparer.Ordinal);
        public readonly HashSet<string> XenotypeDefNames = new(StringComparer.Ordinal);
    }

    public static BaselineImportResult Import(
        UniversalSqueakerTuningBaselineDef preset,
        Selection selection,
        UniversalSqueakerSettings settings)
    {
        if (preset == null || selection == null || settings == null)
            return BaselineImportResult.Empty;

        settings.actionTuning ??= new List<ActionTuningRecord>();
        settings.moodOverrides ??= new Dictionary<SqueakMood, SqueakMoodMod>();
        settings.xenotypePresets ??= new List<XenotypePresetRecord>();

        int actionsImported = 0;
        int moodsImported = 0;

        foreach (BaselineRaceEntry race in preset.races ?? new List<BaselineRaceEntry>())
        {
            if (race == null || string.IsNullOrWhiteSpace(race.raceDefName)) continue;

            List<BaselineActionTuning> raceActions = race.actions ?? new List<BaselineActionTuning>();
            List<BaselineMoodTuning> raceMoods = race.moods ?? new List<BaselineMoodTuning>();

            if (selection.RaceDefNames.Contains(race.raceDefName))
            {
                // Race layer actions (layer 1).
                foreach (ActionTuningRecord record in BuildActionRecords(preset, race.raceDefName, "", raceActions, Array.Empty<BaselineActionTuning>()))
                {
                    UpsertAction(settings, record);
                    actionsImported++;
                }
                // Race moods → global moodOverrides (the only non-xenotype mood path).
                moodsImported += MergeGlobalMoods(settings, raceMoods);
            }

            foreach (BaselineXenotypeEntry xenotype in race.xenotypes ?? new List<BaselineXenotypeEntry>())
            {
                if (xenotype == null || string.IsNullOrWhiteSpace(xenotype.xenotypeDefName)) continue;
                if (!selection.XenotypeDefNames.Contains(xenotype.xenotypeDefName)) continue;

                List<BaselineActionTuning> xenoActions = xenotype.actions ?? new List<BaselineActionTuning>();
                List<BaselineMoodTuning> xenoMoods = xenotype.moods ?? new List<BaselineMoodTuning>();

                // inheritFromRace: the parent race's baseline becomes this xenotype's base first.
                IReadOnlyList<BaselineActionTuning> baseActions = xenotype.inheritFromRace ? raceActions : Array.Empty<BaselineActionTuning>();
                IReadOnlyList<BaselineMoodTuning> baseMoods = xenotype.inheritFromRace ? raceMoods : Array.Empty<BaselineMoodTuning>();

                foreach (ActionTuningRecord record in BuildActionRecords(preset, race.raceDefName, xenotype.xenotypeDefName, baseActions, xenoActions))
                {
                    UpsertAction(settings, record);
                    actionsImported++;
                }
                moodsImported += MergeXenotypeMoods(settings, race.raceDefName, xenotype.xenotypeDefName, baseMoods, xenoMoods);
            }
        }

        return new BaselineImportResult(actionsImported, moodsImported);
    }

    private static List<ActionTuningRecord> BuildActionRecords(
        UniversalSqueakerTuningBaselineDef preset,
        string raceDefName,
        string xenotypeDefName,
        IEnumerable<BaselineActionTuning> baseTunings,
        IEnumerable<BaselineActionTuning> deltaTunings)
    {
        Dictionary<string, ActionTuningRecord> byKey = new(StringComparer.Ordinal);
        void Add(IEnumerable<BaselineActionTuning> list)
        {
            foreach (BaselineActionTuning tuning in list)
            {
                if (tuning == null || string.IsNullOrWhiteSpace(tuning.actionKey)) continue;
                byKey[tuning.actionKey] = ToRecord(preset, tuning, raceDefName, xenotypeDefName);
            }
        }
        Add(baseTunings);
        Add(deltaTunings);
        return byKey.Values.ToList();
    }

    private static ActionTuningRecord ToRecord(
        UniversalSqueakerTuningBaselineDef preset,
        BaselineActionTuning tuning,
        string raceDefName,
        string xenotypeDefName)
    {
        SqueakActionScope scope = tuning.scope;
        // Normalize built-in scopes exactly like SetActionTuningScope so unsupported states
        // (e.g. Draft/Undraft/Equip are ActiveCommand-only) never enter the table.
        if (UniversalSqueaker.Kernel.ActionKey.TryParseBuiltIn(tuning.actionKey, out SqueakAction action))
            scope = SqueakActionDefinitions.NormalizeScope(action, scope);

        return new ActionTuningRecord
        {
            actionKey = tuning.actionKey,
            raceDefName = raceDefName ?? "",
            xenotypeDefName = xenotypeDefName ?? "",
            hasScope = true,
            scope = scope,
            hasIntervalMultiplier = true,
            intervalMultiplier = tuning.intervalMultiplier,
            hasProbabilityMultiplier = true,
            probabilityMultiplier = tuning.probabilityMultiplier,
            sourcePresetDefName = preset.defName
        };
    }

    private static void UpsertAction(UniversalSqueakerSettings settings, ActionTuningRecord incoming)
    {
        for (int i = 0; i < settings.actionTuning.Count; i++)
        {
            ActionTuningRecord existing = settings.actionTuning[i];
            if (existing != null
                && string.Equals(existing.actionKey, incoming.actionKey, StringComparison.Ordinal)
                && string.Equals(existing.raceDefName ?? "", incoming.raceDefName ?? "", StringComparison.Ordinal)
                && string.Equals(existing.xenotypeDefName ?? "", incoming.xenotypeDefName ?? "", StringComparison.Ordinal))
            {
                settings.actionTuning[i] = incoming;
                return;
            }
        }
        settings.actionTuning.Add(incoming);
    }

    private static int MergeGlobalMoods(UniversalSqueakerSettings settings, IEnumerable<BaselineMoodTuning> moods)
    {
        int count = 0;
        foreach (BaselineMoodTuning tuning in moods)
        {
            if (tuning == null) continue;
            settings.moodOverrides[tuning.mood] = new SqueakMoodMod
            {
                mood = tuning.mood,
                pitchFactor = tuning.pitchFactor,
                volumeFactor = tuning.volumeFactor,
                pitchJitter = tuning.pitchJitter
            };
            count++;
        }
        return count;
    }

    private static int MergeXenotypeMoods(
        UniversalSqueakerSettings settings,
        string raceDefName,
        string xenotypeDefName,
        IEnumerable<BaselineMoodTuning> baseMoods,
        IEnumerable<BaselineMoodTuning> deltaMoods)
    {
        Dictionary<SqueakMood, XenotypeMoodOverride> byMood = new();
        void Add(IEnumerable<BaselineMoodTuning> list)
        {
            foreach (BaselineMoodTuning tuning in list)
            {
                if (tuning == null) continue;
                byMood[tuning.mood] = new XenotypeMoodOverride
                {
                    mood = tuning.mood,
                    hasPitchFactor = true,
                    pitchFactor = tuning.pitchFactor,
                    hasVolumeFactor = true,
                    volumeFactor = tuning.volumeFactor,
                    hasPitchJitter = true,
                    pitchJitter = tuning.pitchJitter
                };
            }
        }
        Add(baseMoods);
        Add(deltaMoods);
        if (byMood.Count == 0) return 0;

        XenotypePresetRecord record = FindOrCreatePreset(settings, raceDefName, xenotypeDefName);
        int count = 0;
        foreach (KeyValuePair<SqueakMood, XenotypeMoodOverride> pair in byMood)
        {
            int index = record.moodOverrides.FindIndex(existing => existing != null && existing.mood == pair.Key);
            if (index >= 0) record.moodOverrides[index] = pair.Value;
            else record.moodOverrides.Add(pair.Value);
            count++;
        }
        return count;
    }

    private static XenotypePresetRecord FindOrCreatePreset(UniversalSqueakerSettings settings, string raceDefName, string xenotypeDefName)
    {
        foreach (XenotypePresetRecord record in settings.xenotypePresets)
            if (record != null
                && string.Equals(record.raceDefName, raceDefName, StringComparison.Ordinal)
                && string.Equals(record.xenotypeDefName, xenotypeDefName, StringComparison.Ordinal))
                return record;

        XenotypePresetRecord created = new()
        {
            raceDefName = raceDefName,
            xenotypeDefName = xenotypeDefName
        };
        settings.xenotypePresets.Add(created);
        return created;
    }
}

/// <summary>Import result for UI feedback.</summary>
public readonly struct BaselineImportResult
{
    public static readonly BaselineImportResult Empty = new(0, 0);
    public readonly int ActionsImported;
    public readonly int MoodsImported;
    public BaselineImportResult(int actionsImported, int moodsImported)
    {
        ActionsImported = actionsImported;
        MoodsImported = moodsImported;
    }
}
