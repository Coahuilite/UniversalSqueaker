using System;
using System.Collections.Generic;
using System.IO;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;
using Verse;

namespace UniversalSqueaker.SettingsMigrationTests;

/// <summary>
/// Phase 3 settings-migration characterization harness. It links the production Settings/Models/Kernel/Pure
/// sources and drives the same internal transaction methods the mod uses at startup:
///   1. v3/v1 → v5/v2 migration creates actionTuning + moodTuning, advances schema markers, queues persistence.
///   2. Malformed legacy records fail closed, preserve schema markers, and remain retryable.
///   3. A settings-schema-current/voice-schema-stale load never overwrites user moodTuning edits.
///   4. SetActionTuningScope(null) clears every duplicate for the same identity.
///   5. SetMoodTuning with an unknown factor does not create an empty row.
///   6. BaselinePresetImporter last-wins upsert and (race,xeno) composite xenotype selection keys.
///   7. AudioDomains.TryCreate rejects whitespace-only race/xeno.
/// </summary>
internal static class Program
{
    private static int failures;

    private static int Main()
    {
        try
        {
            MigrateV3RecordsTransactionallySucceeds();
            MigrationFailureBlocksAndRetries();
            VoiceSchemaStaleDoesNotOverwriteMoodTuning();
            SetActionTuningScopeNullClearsAllDuplicates();
            SetMoodTuningUnknownFactorDoesNotInsertEmptyRecord();
            BaselineImporterClearsDuplicatesAndUsesCompositeXenotypeKeys();
            TwoPresetsImportIndependentlyAndIdempotently();
            AudioDomainsRejectWhitespace();
            GlobalVolumeDefaultsAndClamps();
            GlobalVolumeScribeRoundTrip();
            SetDistanceRangeClampsAndMarksCustom();

            if (failures == 0)
            {
                Console.WriteLine("UniversalSqueaker settings migration characterization passed.");
                return 0;
            }

            Console.Error.WriteLine("UniversalSqueaker settings migration characterization FAILED (" + failures + ").");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("UNHANDLED: " + ex.GetType().FullName + " :: " + ex.Message);
            return 1;
        }
    }

    private static void MigrateV3RecordsTransactionallySucceeds()
    {
        Scenario("1-v3-to-v5-success");
        UniversalSqueakerSettings settings = NewSettings(settingsSchemaVersion: 3, voicePackSchemaVersion: 1);

        settings.moodOverrides.Add(SqueakMood.Good, new SqueakMoodMod
        {
            mood = SqueakMood.Good,
            pitchFactor = 1.1f,
            volumeFactor = 1f,
            pitchJitter = FloatRange.One,
        });

        settings.xenotypePresets.Add(new XenotypePresetRecord
        {
            raceDefName = "RaceA",
            xenotypeDefName = "XenoA",
            actionOverrides = new List<XenotypeActionBehaviorOverride>
            {
                new XenotypeActionBehaviorOverride
                {
                    action = SqueakAction.Call,
                    hasEnabled = true,
                    enabled = true,
                    hasIntervalMultiplier = true,
                    intervalMultiplier = 1.5f,
                },
            },
            moodOverrides = new List<XenotypeMoodOverride>
            {
                new XenotypeMoodOverride
                {
                    mood = SqueakMood.Bad,
                    hasPitchFactor = true,
                    pitchFactor = 0.7f,
                },
            },
        });

        bool migrated = settings.MigrateV3RecordsTransactionally();
        Check(migrated, "migrate: v3/v1 transaction succeeds", ref failures);
        Check(settings.settingsSchemaVersion == 5 && settings.voicePackSchemaVersion == 2,
            "migrate: schema markers advanced to v5/v2", ref failures);
        Check(settings.MigrationPersistencePendingForFixture && !settings.MigrationPersistenceBlockedForFixture,
            "migrate: persistence pending, not blocked", ref failures);

        Check(settings.actionTuning.Count == 1
            && settings.actionTuning[0].actionKey == "Call"
            && settings.actionTuning[0].raceDefName == "RaceA"
            && settings.actionTuning[0].xenotypeDefName == "XenoA"
            && settings.actionTuning[0].hasScope
            && settings.actionTuning[0].scope == SqueakActionScope.AnyOccurrence
            && settings.actionTuning[0].hasIntervalMultiplier
            && Math.Abs(settings.actionTuning[0].intervalMultiplier - 1.5f) < 0.0001f,
            "migrate: actionTuning built from legacy xenotypePresets.actionOverrides", ref failures);

        Check(settings.moodTuning.Count == 2, "migrate: moodTuning contains global + xenotype rows", ref failures);
        MoodTuningRecord? globalMood = settings.moodTuning.Find(r => r.mood == SqueakMood.Good
            && string.IsNullOrEmpty(r.raceDefName) && string.IsNullOrEmpty(r.xenotypeDefName));
        MoodTuningRecord? xenoMood = settings.moodTuning.Find(r => r.mood == SqueakMood.Bad
            && r.raceDefName == "RaceA" && r.xenotypeDefName == "XenoA");
        Check(globalMood != null && Math.Abs(globalMood.pitchFactor - 1.1f) < 0.0001f,
            "migrate: global moodOverrides became layer-0 moodTuning", ref failures);
        Check(xenoMood != null && xenoMood.hasPitchFactor && Math.Abs(xenoMood.pitchFactor - 0.7f) < 0.0001f,
            "migrate: xenotypePresets.moodOverrides became layer-2 moodTuning", ref failures);
    }

    private static void MigrationFailureBlocksAndRetries()
    {
        Scenario("2-failure-and-retry");
        UniversalSqueakerSettings settings = NewSettings(settingsSchemaVersion: 3, voicePackSchemaVersion: 1);
        settings.voicePackSelections.Add(new VoicePackSelectionRecord
        {
            scope = SqueakVoicePackScope.Race,
            raceDefName = "",
            xenotypeDefName = "",
            enabledPackKeys = new List<string> { "pack.mod:US_Pack" },
        });

        bool failed = settings.MigrateV3RecordsTransactionally();
        Check(!failed, "failure: malformed selection rejects migration", ref failures);
        Check(settings.MigrationPersistenceBlockedForFixture,
            "failure: migrationPersistenceBlocked set", ref failures);
        Check(settings.settingsSchemaVersion == 3 && settings.voicePackSchemaVersion == 1,
            "failure: schema markers stay old", ref failures);
        Check(!settings.MigrationPersistencePendingForFixture,
            "failure: no persistence pending flag on failed transaction", ref failures);
        Check(settings.voicePackSelections.Count == 1 && settings.voicePackSelections[0].raceDefName == "",
            "failure: legacy source list left unchanged", ref failures);

        // A later startup with the same object can fix the malformed source and retry the transaction.
        settings.voicePackSelections[0].raceDefName = "RaceA";
        bool retried = settings.MigrateV3RecordsTransactionally();
        Check(retried, "retry: repaired selection migrates successfully", ref failures);
        Check(settings.settingsSchemaVersion == 5 && settings.voicePackSchemaVersion == 2 && !settings.MigrationPersistenceBlockedForFixture,
            "retry: schema advances and blocked flag clears", ref failures);
    }

    private static void VoiceSchemaStaleDoesNotOverwriteMoodTuning()
    {
        Scenario("3-v5-stale-voice-does-not-overwrite-mood");
        UniversalSqueakerSettings settings = NewSettings(settingsSchemaVersion: 5, voicePackSchemaVersion: 1);
        settings.moodTuning.Add(new MoodTuningRecord
        {
            mood = SqueakMood.Bad,
            raceDefName = "RaceB",
            xenotypeDefName = "",
            hasPitchFactor = true,
            pitchFactor = 0.55f,
        });
        settings.moodOverrides.Add(SqueakMood.Good, new SqueakMoodMod { mood = SqueakMood.Good, pitchFactor = 1.2f });
        settings.xenotypePresets.Add(new XenotypePresetRecord
        {
            raceDefName = "RaceA",
            xenotypeDefName = "XenoA",
            moodOverrides = new List<XenotypeMoodOverride>
            {
                new XenotypeMoodOverride { mood = SqueakMood.Bad, hasPitchFactor = true, pitchFactor = 0.4f },
            },
        });

        bool migrated = settings.MigrateV3RecordsTransactionally();
        Check(migrated, "stale-voice: voice-only migration succeeds", ref failures);
        Check(settings.settingsSchemaVersion == 5 && settings.voicePackSchemaVersion == 2,
            "stale-voice: settings schema unchanged at 5, voice advanced to 2", ref failures);
        Check(settings.MigrationPersistencePendingForFixture && !settings.MigrationPersistenceBlockedForFixture,
            "stale-voice: persistence pending, not blocked", ref failures);
        Check(settings.moodTuning.Count == 1
            && settings.moodTuning[0].mood == SqueakMood.Bad
            && settings.moodTuning[0].raceDefName == "RaceB"
            && settings.moodTuning[0].hasPitchFactor
            && Math.Abs(settings.moodTuning[0].pitchFactor - 0.55f) < 0.0001f,
            "stale-voice: user moodTuning edit preserved (old moodOverrides not replayed)", ref failures);
    }

    private static void SetActionTuningScopeNullClearsAllDuplicates()
    {
        Scenario("4-action-scope-null-clears-all-duplicates");
        UniversalSqueakerSettings settings = NewSettings();
        settings.actionTuning = new List<ActionTuningRecord>
        {
            new ActionTuningRecord { actionKey = "Call", raceDefName = "RaceA", xenotypeDefName = "", hasScope = true, scope = SqueakActionScope.AnyOccurrence },
            new ActionTuningRecord { actionKey = "Call", raceDefName = "RaceA", xenotypeDefName = "", hasScope = true, scope = SqueakActionScope.Disabled },
            new ActionTuningRecord { actionKey = "Call", raceDefName = "RaceA", xenotypeDefName = "", hasIntervalMultiplier = true, intervalMultiplier = 2f },
            new ActionTuningRecord { actionKey = "Eat", raceDefName = "RaceA", xenotypeDefName = "", hasScope = true, scope = SqueakActionScope.AnyOccurrence },
        };

        settings.SetActionTuningScope("Call", "RaceA", "", null);

        Check(settings.actionTuning.Count == 1
            && settings.actionTuning[0].actionKey == "Eat",
            "action-scope-null: all same-identity rows removed, unrelated row retained", ref failures);
    }

    private static void SetMoodTuningUnknownFactorDoesNotInsertEmptyRecord()
    {
        Scenario("5-mood-unknown-factor-no-empty-record");
        UniversalSqueakerSettings settings = NewSettings();
        settings.moodTuning = new List<MoodTuningRecord>
        {
            new MoodTuningRecord { mood = SqueakMood.Good, raceDefName = "RaceA", hasPitchFactor = true, pitchFactor = 1f },
        };

        settings.SetMoodTuning(SqueakMood.Bad, "RaceA", "", "not-a-factor", 1.5f);

        Check(settings.moodTuning.Count == 1
            && settings.moodTuning[0].mood == SqueakMood.Good,
            "mood-unknown-factor: no empty record inserted", ref failures);
    }

    private static void BaselineImporterClearsDuplicatesAndUsesCompositeXenotypeKeys()
    {
        Scenario("6-baseline-import-last-wins-composite-key");
        UniversalSqueakerTuningBaselineDef preset = new()
        {
            defName = "TestBaseline",
            races = new List<BaselineRaceEntry>
            {
                new BaselineRaceEntry
                {
                    raceDefName = "RaceA",
                    xenotypes = new List<BaselineXenotypeEntry>
                    {
                        new BaselineXenotypeEntry
                        {
                            xenotypeDefName = "XenoA",
                            inheritFromRace = false,
                            actions = new List<BaselineActionTuning>
                            {
                                new BaselineActionTuning { actionKey = "Call", scope = SqueakActionScope.AnyOccurrence, intervalMultiplier = 1.25f, probabilityMultiplier = 1f },
                            },
                        },
                    },
                },
                new BaselineRaceEntry
                {
                    raceDefName = "RaceB",
                    xenotypes = new List<BaselineXenotypeEntry>
                    {
                        new BaselineXenotypeEntry
                        {
                            xenotypeDefName = "XenoA",
                            inheritFromRace = false,
                            actions = new List<BaselineActionTuning>
                            {
                                new BaselineActionTuning { actionKey = "Eat", scope = SqueakActionScope.AnyOccurrence, intervalMultiplier = 1f, probabilityMultiplier = 1f },
                            },
                        },
                    },
                },
            },
        };

        UniversalSqueakerSettings settings = NewSettings();
        settings.actionTuning = new List<ActionTuningRecord>
        {
            new ActionTuningRecord { actionKey = "Call", raceDefName = "RaceA", xenotypeDefName = "XenoA", sourcePresetDefName = "old-1", hasScope = true, scope = SqueakActionScope.Disabled },
            new ActionTuningRecord { actionKey = "Call", raceDefName = "RaceA", xenotypeDefName = "XenoA", sourcePresetDefName = "old-2", hasScope = true, scope = SqueakActionScope.AnyOccurrence },
            new ActionTuningRecord { actionKey = "Call", raceDefName = "RaceA", xenotypeDefName = "XenoA", sourcePresetDefName = "old-3", hasIntervalMultiplier = true, intervalMultiplier = 0.5f },
        };

        BaselinePresetImporter.Selection selection = new();
        selection.XenotypeDomainKeys.Add(BaselinePresetImporter.XenotypeDomainKey("RaceA", "XenoA"));

        BaselineImportResult result = BaselinePresetImporter.Import(preset, selection, settings);

        Check(result.ActionsImported == 1 && result.MoodsImported == 0,
            "baseline: exactly the selected RaceA/XenoA action imported", ref failures);
        int sameIdentityCount = settings.actionTuning.FindAll(r => r.actionKey == "Call"
            && r.raceDefName == "RaceA" && r.xenotypeDefName == "XenoA").Count;
        Check(sameIdentityCount == 1, "baseline: duplicate same-identity rows cleared before append", ref failures);
        Check(settings.actionTuning.Count == 1
            && settings.actionTuning[0].sourcePresetDefName == "TestBaseline"
            && Math.Abs(settings.actionTuning[0].intervalMultiplier - 1.25f) < 0.0001f,
            "baseline: imported row is last-wins and carries sourcePresetDefName", ref failures);
        Check(settings.actionTuning.Find(r => r.actionKey == "Eat" && r.raceDefName == "RaceB" && r.xenotypeDefName == "XenoA") == null,
            "baseline: same xenotype under unselected race is not imported (composite (race,xeno) key)", ref failures);
    }

    // D8 pre-check: does the import pipeline actually support shipping TWO preset Defs and
    // importing both? Importer-level end to end (the business boundary the UI's Import button
    // calls through). Covers the exact shape the Nivarian fixture will take - two presets on one
    // race, distinct action keys - and the semantics a single-preset test cannot see: cross-preset
    // non-clobbering and re-import idempotency. The UI-click path itself is NOT exercisable here
    // (Verse stub has no DefDatabase; that blind spot is tracked under D5), so this pins the
    // importer contract the fixture depends on and leaves the real in-game pass to the pack.
    private static void TwoPresetsImportIndependentlyAndIdempotently()
    {
        Scenario("6b-two-presets-independent-idempotent");

        UniversalSqueakerTuningBaselineDef cheapTalk = new()
        {
            defName = "US_NivarianExp_CheapTalk",
            presetLabel = "Cheap Talk",
            races = new List<BaselineRaceEntry>
            {
                new BaselineRaceEntry
                {
                    raceDefName = "NivarianRace_Pawn",
                    actions = new List<BaselineActionTuning>
                    {
                        new BaselineActionTuning { actionKey = "Work", scope = SqueakActionScope.AnyOccurrence, intervalMultiplier = 0.15f, probabilityMultiplier = 1f },
                    },
                },
            },
        };
        UniversalSqueakerTuningBaselineDef muteJoy = new()
        {
            defName = "US_NivarianExp_Mute",
            presetLabel = "Mute Joy",
            races = new List<BaselineRaceEntry>
            {
                new BaselineRaceEntry
                {
                    raceDefName = "NivarianRace_Pawn",
                    actions = new List<BaselineActionTuning>
                    {
                        new BaselineActionTuning { actionKey = "Joy", scope = SqueakActionScope.Disabled, intervalMultiplier = 1f, probabilityMultiplier = 1f },
                    },
                },
            },
        };

        UniversalSqueakerSettings settings = NewSettings();
        BaselinePresetImporter.Selection raceSel = new();
        raceSel.RaceDefNames.Add("NivarianRace_Pawn");

        BaselineImportResult first = BaselinePresetImporter.Import(cheapTalk, raceSel, settings);
        Check(first.ActionsImported == 1, "two-preset: first preset imports exactly one action row", ref failures);
        ActionTuningRecord? work = settings.actionTuning.Find(r => r.actionKey == "Work");
        Check(work != null
                && work.sourcePresetDefName == "US_NivarianExp_CheapTalk"
                && work.raceDefName == "NivarianRace_Pawn" && string.IsNullOrEmpty(work.xenotypeDefName)
                && work.hasIntervalMultiplier && Math.Abs(work.intervalMultiplier - 0.15f) < 0.0001f,
            "two-preset: Work row lands at the race layer, stamped with its own preset, interval 0.15", ref failures);

        BaselineImportResult second = BaselinePresetImporter.Import(muteJoy, raceSel, settings);
        Check(second.ActionsImported == 1, "two-preset: second preset imports exactly one action row", ref failures);
        Check(settings.actionTuning.Count == 2, "two-preset: distinct action keys coexist (Work + Joy), count = 2", ref failures);
        ActionTuningRecord? joy = settings.actionTuning.Find(r => r.actionKey == "Joy");
        Check(joy != null
                && joy.sourcePresetDefName == "US_NivarianExp_Mute"
                && joy.hasScope && joy.scope == SqueakActionScope.Disabled,
            "two-preset: Joy row stamped with the SECOND preset and forced Disabled", ref failures);
        Check(settings.actionTuning.Find(r => r.actionKey == "Work")?.sourcePresetDefName == "US_NivarianExp_CheapTalk",
            "two-preset: importing the second preset does NOT clobber the first preset's Work row (identity-scoped upsert)", ref failures);

        // Re-import the first preset: same-identity clear-then-append is idempotent, and it must
        // not touch the second preset's Joy row (different identity).
        BaselinePresetImporter.Import(cheapTalk, raceSel, settings);
        Check(settings.actionTuning.Count == 2
                && settings.actionTuning.FindAll(r => r.actionKey == "Work").Count == 1
                && settings.actionTuning.Find(r => r.actionKey == "Work")?.sourcePresetDefName == "US_NivarianExp_CheapTalk"
                && settings.actionTuning.Find(r => r.actionKey == "Joy")?.sourcePresetDefName == "US_NivarianExp_Mute",
            "two-preset: re-import is idempotent (no Work duplicate) and preserves the other preset's row", ref failures);

        // Same key, two presets, later import wins the identity (the "same key overwrites" half).
        UniversalSqueakerTuningBaselineDef cheapTalkLoud = new()
        {
            defName = "US_NivarianExp_Loud",
            races = new List<BaselineRaceEntry>
            {
                new BaselineRaceEntry
                {
                    raceDefName = "NivarianRace_Pawn",
                    actions = new List<BaselineActionTuning>
                    {
                        new BaselineActionTuning { actionKey = "Work", scope = SqueakActionScope.AnyOccurrence, intervalMultiplier = 3f, probabilityMultiplier = 1f },
                    },
                },
            },
        };
        BaselinePresetImporter.Import(cheapTalkLoud, raceSel, settings);
        ActionTuningRecord? workAfter = settings.actionTuning.Find(r => r.actionKey == "Work");
        Check(settings.actionTuning.Count == 2
                && workAfter != null
                && workAfter.sourcePresetDefName == "US_NivarianExp_Loud"
                && Math.Abs(workAfter.intervalMultiplier - 3f) < 0.0001f,
            "two-preset: a different preset writing the SAME identity overwrites it (last import wins, source re-stamped)", ref failures);
    }

    private static void AudioDomainsRejectWhitespace()
    {
        Scenario("7-audio-domains-reject-whitespace");
        Check(!AudioDomains.TryCreate("   ", "Xeno", out _),
            "audio-domains: whitespace-only race rejected", ref failures);
        Check(!AudioDomains.TryCreate("RaceA", "   ", out _),
            "audio-domains: whitespace-only xeno rejected", ref failures);
        Check(AudioDomains.TryCreate("RaceA", "", out AudioDomain raceOnly) && raceOnly.IsRaceOnly,
            "audio-domains: empty xeno remains race-only", ref failures);
        Check(AudioDomains.TryCreate("RaceA", "XenoA", out AudioDomain xeno) && xeno.Xenotype?.DefName == "XenoA",
            "audio-domains: non-empty xeno creates xenotype domain", ref failures);
    }

    private static void GlobalVolumeDefaultsAndClamps()
    {
        Scenario("8-global-volume-default-and-clamp");
        UniversalSqueakerSettings settings = NewSettings();
        Check(Math.Abs(settings.globalVolumeFactor - 1f) < 0.0001f,
            "global-volume: default is 1", ref failures);

        CompSqueaker.GlobalVolumeFactor = 1f;
        settings.SetGlobalVolume(-0.5f);
        Check(Math.Abs(settings.globalVolumeFactor) < 0.0001f
            && Math.Abs(CompSqueaker.GlobalVolumeFactor) < 0.0001f,
            "global-volume: negative clamps to 0 and syncs runtime", ref failures);

        settings.SetGlobalVolume(1.5f);
        Check(Math.Abs(settings.globalVolumeFactor - 1f) < 0.0001f
            && Math.Abs(CompSqueaker.GlobalVolumeFactor - 1f) < 0.0001f,
            "global-volume: >1 clamps to 1 and syncs runtime", ref failures);

        settings.SetGlobalVolume(0.37f);
        Check(Math.Abs(settings.globalVolumeFactor - 0.37f) < 0.0001f
            && Math.Abs(CompSqueaker.GlobalVolumeFactor - 0.37f) < 0.0001f,
            "global-volume: valid value writes field and runtime", ref failures);

        settings.SetGlobalVolume(float.NaN);
        Check(Math.Abs(settings.globalVolumeFactor - 0.37f) < 0.0001f,
            "global-volume: NaN is rejected", ref failures);

        settings.SetGlobalVolume(float.PositiveInfinity);
        Check(Math.Abs(settings.globalVolumeFactor - 0.37f) < 0.0001f,
            "global-volume: Infinity is rejected", ref failures);

        settings.SetGlobalVolume(0.37f);
        Check(Math.Abs(settings.globalVolumeFactor - 0.37f) < 0.0001f,
            "global-volume: same-value no-op keeps current", ref failures);
    }

    private static void GlobalVolumeScribeRoundTrip()
    {
        Scenario("9-global-volume-scribe-round-trip");
        UniversalSqueakerSettings source = NewSettings();
        source.globalVolumeFactor = 0.42f;
        string path = Path.Combine(Path.GetTempPath(), "us-settings-global-volume-roundtrip-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            SafeSaver.Save(path, "Settings", () => source.ExposeData());
            UniversalSqueakerSettings loaded = NewSettings();
            Scribe.loader.InitLoading(path);
            loaded.ExposeData();
            Scribe.loader.FinalizeLoading();
            Check(Math.Abs(loaded.globalVolumeFactor - 0.42f) < 0.0001f,
                "global-volume: Scribe round-trip preserves value", ref failures);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void SetDistanceRangeClampsAndMarksCustom()
    {
        Scenario("10-distance-range-clamp-custom");
        UniversalSqueakerSettings settings = NewSettings();
        settings.distancePreset = SqueakDistancePreset.Balanced;
        settings.distanceRange = new FloatRange(15f, 50f);

        settings.SetDistanceRange(10f, 80f);
        Check(settings.distancePreset == SqueakDistancePreset.Custom,
            "distance-range: manual edit marks Custom", ref failures);
        Check(settings.distanceRange.min >= 15f - 0.0001f && settings.distanceRange.max <= 65f + 0.0001f,
            "distance-range: clamps into 15..65", ref failures);
        Check(settings.distanceRange.max >= settings.distanceRange.min + 5f - 0.0001f,
            "distance-range: enforces 5-unit minimum gap", ref failures);

        settings.SetDistanceRange(70f, 70f);
        Check(settings.distanceRange.min >= 15f - 0.0001f && settings.distanceRange.max <= 65f + 0.0001f,
            "distance-range: out-of-range endpoints clamp", ref failures);
        Check(settings.distanceRange.max >= settings.distanceRange.min + 5f - 0.0001f,
            "distance-range: equal endpoints enforce minimum gap", ref failures);

        settings.SetDistanceRange(20f, 22f);
        Check(settings.distanceRange.max >= settings.distanceRange.min + 5f - 0.0001f,
            "distance-range: close endpoints enforce minimum gap", ref failures);

        FloatRange beforeNan = settings.distanceRange;
        settings.SetDistanceRange(float.NaN, float.PositiveInfinity);
        Check(Math.Abs(settings.distanceRange.min - beforeNan.min) < 0.0001f
            && Math.Abs(settings.distanceRange.max - beforeNan.max) < 0.0001f,
            "distance-range: NaN/Infinity is rejected", ref failures);

        settings.distancePreset = SqueakDistancePreset.Custom;
        FloatRange beforeNoop = settings.distanceRange;
        settings.SetDistanceRange(beforeNoop.min, beforeNoop.max);
        Check(Math.Abs(settings.distanceRange.min - beforeNoop.min) < 0.0001f
            && Math.Abs(settings.distanceRange.max - beforeNoop.max) < 0.0001f,
            "distance-range: same Custom range is no-op", ref failures);
    }

    // ---- helpers ----

    private static UniversalSqueakerSettings NewSettings(int settingsSchemaVersion = 5, int voicePackSchemaVersion = 2)
    {
        UniversalSqueakerSettings settings = new()
        {
            settingsSchemaVersion = settingsSchemaVersion,
            voicePackSchemaVersion = voicePackSchemaVersion,
            voicePackSelections = new List<VoicePackSelectionRecord>(),
            xenotypePresets = new List<XenotypePresetRecord>(),
            actionTuning = new List<ActionTuningRecord>(),
            moodTuning = new List<MoodTuningRecord>(),
            moodOverrides = new Dictionary<SqueakMood, SqueakMoodMod>(),
        };
        return settings;
    }

    private static void Scenario(string name) => Console.WriteLine("Scenario " + name + "...");

    private static void Check(bool condition, string name, ref int failures)
    {
        if (condition) Console.WriteLine("  ok: " + name);
        else { Console.Error.WriteLine("  FAIL: " + name); failures++; }
    }
}
