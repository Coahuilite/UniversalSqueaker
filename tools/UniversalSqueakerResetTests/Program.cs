using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;
using Verse;

namespace UniversalSqueaker.ResetTests;

/// <summary>
/// US-RESET1 backend verification harness (dedicated restore harness; the shared
/// Program/RecordingSettingsSource entries of the other harnesses belong to PACK1 and are NOT touched).
///
/// It links the PRODUCTION Settings sources (incl. UniversalSqueakerSettings.Reset.cs), the
/// production Scribe boundary (Verse stub Scribe, same characterization as the migration harness)
/// and the production fallback store behind its ConfigDirectoryForTests seam, and executes the
/// PRODUCTION restore operations against polluted temporary config copies. It never touches the
/// player's real Config directory, retained dist/dev or any held/testpack artifact.
///
/// Lanes:
///   1. Global restore over a fully polluted instance: every owned field back to the unified
///      default source; schema markers and migration diagnostics preserved; live object identity
///      kept; exactly one runtime publish round and one persistence request; repeat = NoChange.
///   2. Distance restore: preset+range back to Balanced/15-50, global volume and all other domains
///      preserved.
///   3. Action ROW restore: exact (actionKey, race, xeno) identity incl. stale duplicate rows and
///      the source anchor; other keys/layers/domains preserved; invalid targets Rejected with zero
///      side effects; unknown identity NoChange (never falls through to Global).
///   4. Action AREA restore: exact (race, xeno) domain across all keys; the legacy domain-level
///      overall interval multiplier (xenotypePresets) is preserved by every local op and owned
///      ONLY by the global op (preparation §6.6); layer-0 area ("","") is a distinct operation
///      from the global restore; empty domain must not fall back to Global.
///   5. Mood AREA restore: three factors + anchor across all moods of the exact domain; the legacy
///      global moodOverrides dict (not runtime-participating) is NOT synced by local ops.
///   6. Normal visible-area restores: the four UI-bound field sets (Distance/Basics/Timing/
///      Diagnostics) restore exactly their own fields; other areas preserved; publish shape per area.
///   7. Copied-config production round trip: Scribe-written polluted copy -> production load ->
///      production ResetAllSettings -> Scribe-written clean copy; parse mapping checked on both
///      ends; the player's fallback profile files byte-identical and parse-map identical across
///      the restore; live settings reference identity preserved through the load/save path.
///   8. Migration-blocked legacy state: a failed v3 transaction keeps schema markers and the
///      blocked flag through a global restore (no disguised migration).
///   9. Changed-input checks for every API: already-default state -> NoChange, zero notifications,
///      zero persistence requests (repeated restore value-idempotent).
/// </summary>
internal static class Program
{
    private static int failures;
    private static int checks;
    private static string tmpRoot = "";

    private static int Main()
    {
        tmpRoot = Path.Combine(AppContext.BaseDirectory, "reset1-tmp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpRoot);
        GenFilePaths.ConfigFolderPath = tmpRoot; // belt-and-suspenders: even a forgotten seam stays inside out/
        Console.WriteLine("US-RESET1 restore harness | tmp root: " + tmpRoot);

        try
        {
            GlobalRestoreFullPollution();
            GlobalRestorePreservesSchemaAndDiagnostics();
            DistanceRestoreKeepsVolumeAndDomains();
            ActionRowRestoreExactIdentity();
            ActionRowRestoreRejectsInvalidTargets();
            ActionAreaRestorePreservesMultiplierAndOtherDomains();
            ActionAreaLayerZeroIsNotGlobal();
            MoodAreaRestoreKeepsLegacyDictAndOtherDomains();
            NormalAreaRestoresMatchUiBindings();
            NormalAreaUnknownValueRejected();
            CopiedConfigProductionRoundTripWithFallbackBytes();
            MigrationBlockedStateSurvivesGlobalRestore();

            Console.WriteLine();
            Console.WriteLine(failures == 0
                ? "UniversalSqueaker RESET1 restore verification PASSED (" + checks + " checks)."
                : "UniversalSqueaker RESET1 restore verification FAILED (" + failures + "/" + checks + ").");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("UNHANDLED: " + ex.GetType().FullName + " :: " + ex.Message + "\n" + ex.StackTrace);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(tmpRoot)) Directory.Delete(tmpRoot, recursive: true); }
            catch (Exception cleanup) { Console.WriteLine("note: temp cleanup failed: " + cleanup.Message); }
        }
    }

    // ---------------------------------------------------------------- fixtures

    private static UniversalSqueakerSettings NewSettings(int schema = 5, int voiceSchema = 2)
    {
        UniversalSqueakerSettings s = new()
        {
            settingsSchemaVersion = schema,
            voicePackSchemaVersion = voiceSchema,
        };
        return s;
    }

    /// <summary>Pollution across every owned global field and every record store.</summary>
    private static void PolluteAll(UniversalSqueakerSettings s)
    {
        s.voicePackMode = SqueakVoicePackMode.Vanilla;
        s.scaleCooldownWithTimeSpeed = false;
        s.scaleFrequencyWithTalking = false;
        s.scalePeriodicWithAudiblePopulation = false;
        s.globalMinIntervalTicks = 999;
        s.localizeDebugActions = true;
        s.showCameraIndicator = true;
        s.devLoggingMode = SqueakDevLoggingMode.Enabled;
        s.globalCooldownMultiplier = 2.5f;
        s.globalVolumeFactor = 0.4f;
        s.distancePreset = SqueakDistancePreset.Strong;
        s.distanceRange = new FloatRange(20f, 45f);
        s.allowEasterEggSounds = true;
        s.allowExternalActions = true;
        s.allowBabyActions = true;
        s.eatPrecisionEnabled = true;
        s.eatPrecisionIncludeDrugs = true;

        s.moodOverrides[SqueakMood.Good] = new SqueakMoodMod
        {
            mood = SqueakMood.Good, pitchFactor = 1.2f, volumeFactor = 0.9f,
            pitchJitter = new FloatRange(0.9f, 1.1f),
        };

        s.voicePackSelections.Add(new VoicePackSelectionRecord
        {
            scope = SqueakVoicePackScope.Race, raceDefName = "RaceA", xenotypeDefName = "",
            enabledPackKeys = new List<string> { "packA:key1" },
        });
        s.voicePackSelections.Add(new VoicePackSelectionRecord
        {
            scope = SqueakVoicePackScope.Xenotype, raceDefName = "RaceA", xenotypeDefName = "XenoA",
            enabledPackKeys = new List<string> { "packA:key2" },
        });

        // §6.6: legacy collection with the domain-level overall interval multiplier (only the
        // GLOBAL restore is allowed to clear this).
        s.xenotypePresets.Add(new XenotypePresetRecord
        {
            raceDefName = "RaceA",
            xenotypeDefName = "XenoA",
            hasOverallIntervalMultiplier = true, overallIntervalMultiplier = 2.5f,
        });

        s.actionTuning.Add(new ActionTuningRecord
        {
            actionKey = "Call", raceDefName = "RaceA", xenotypeDefName = "XenoA",
            hasScope = true, scope = SqueakActionScope.AnyOccurrence, sourcePresetDefName = "PresetOne",
        });
        // Stale duplicate under the SAME identity carrying a live multiplier (field-level fold).
        s.actionTuning.Add(new ActionTuningRecord
        {
            actionKey = "Call", raceDefName = "RaceA", xenotypeDefName = "XenoA",
            hasIntervalMultiplier = true, intervalMultiplier = 1.5f,
        });
        s.actionTuning.Add(new ActionTuningRecord
        {
            actionKey = "Eat", raceDefName = "RaceA", xenotypeDefName = "XenoA",
            hasProbabilityMultiplier = true, probabilityMultiplier = 0.5f, sourcePresetDefName = "PresetOne",
        });
        s.actionTuning.Add(new ActionTuningRecord
        {
            actionKey = "Move", raceDefName = "RaceA", xenotypeDefName = "",
            hasScope = true, scope = SqueakActionScope.ActiveCommand,
        });
        s.actionTuning.Add(new ActionTuningRecord
        {
            actionKey = "Sleep", raceDefName = "", xenotypeDefName = "",
            hasScope = true, scope = SqueakActionScope.AnyOccurrence,
        });

        s.moodTuning.Add(new MoodTuningRecord
        {
            mood = SqueakMood.Neutral, raceDefName = "RaceA", xenotypeDefName = "XenoA",
            hasPitchFactor = true, pitchFactor = 1.3f, sourcePresetDefName = "PresetOne",
        });
        s.moodTuning.Add(new MoodTuningRecord
        {
            mood = SqueakMood.Bad, raceDefName = "RaceA", xenotypeDefName = "XenoA",
            hasVolumeFactor = true, volumeFactor = 0.6f,
        });
        s.moodTuning.Add(new MoodTuningRecord
        {
            mood = SqueakMood.Good, raceDefName = "RaceB", xenotypeDefName = "",
            hasPitchJitter = true, pitchJitter = new FloatRange(0.8f, 1.2f),
        });
    }

    // ---------------------------------------------------------------- lanes

    private static void GlobalRestoreFullPollution()
    {
        Scenario("1 global-restore full pollution");
        UniversalSqueakerSettings s = NewSettings();
        PolluteAll(s);
        UniversalSqueakerSettings live = s;
        Probe.Reset();

        SqueakResetOutcome outcome = s.ResetAllSettings();
        Check("global: outcome Applied", outcome == SqueakResetOutcome.Applied);
        Check("global: voicePackMode back to Fallback", s.voicePackMode == SqueakVoicePackMode.Fallback);
        Check("global: cheap toggles back to true", s.scaleCooldownWithTimeSpeed && s.scaleFrequencyWithTalking && s.scalePeriodicWithAudiblePopulation);
        Check("global: interval back to 216", s.globalMinIntervalTicks == 216);
        Check("global: localize/camera back to false", !s.localizeDebugActions && !s.showCameraIndicator);
        Check("global: devLogging back to Auto", s.devLoggingMode == SqueakDevLoggingMode.Auto);
        Check("global: multipliers back to 1f", s.globalCooldownMultiplier == 1f && s.globalVolumeFactor == 1f);
        Check("global: distance back to Balanced 15-50",
            s.distancePreset == SqueakDistancePreset.Balanced && s.distanceRange.min == 15f && s.distanceRange.max == 50f);
        Check("global: gates back to false",
            !s.allowEasterEggSounds && !s.allowExternalActions && !s.allowBabyActions
            && !s.eatPrecisionEnabled && !s.eatPrecisionIncludeDrugs);
        Check("global: legacy moodOverrides dict emptied", s.moodOverrides.Count == 0);
        Check("global: voicePackSelections cleared", s.voicePackSelections.Count == 0);
        Check("global: xenotypePresets cleared WITH domain-level multiplier (§6.6 global owns it)", s.xenotypePresets.Count == 0);
        Check("global: actionTuning cleared (incl. anchors/duplicates)", s.actionTuning.Count == 0);
        Check("global: moodTuning cleared (incl. anchors)", s.moodTuning.Count == 0);
        Check("global: live settings object identity preserved", ReferenceEquals(live, s));
        Check("global: exactly one persistence request", Probe.SaveQueuedCount == 1);
        Check("global: exactly one discrete publish round (no setter-chain multi-publish)",
            Probe.DiscreteNotifyCount == 1 && Probe.ContinuousNotifyCount == 0
            && Probe.DistanceApplyCount == 1 && Probe.DebugMenuSetEnabledCount == 1 && Probe.LoggingModeChangedCount == 1
            && Probe.FlushCount == 0);
        Log("  probe after Applied: " + Probe.Snapshot());

        outcome = s.ResetAllSettings();
        Check("global: repeat is NoChange", outcome == SqueakResetOutcome.NoChange);
        Check("global: repeat caused zero further publish/persistence", Probe.SaveQueuedCount == 1
            && Probe.DiscreteNotifyCount == 1 && Probe.DistanceApplyCount == 1);
    }

    private static void GlobalRestorePreservesSchemaAndDiagnostics()
    {
        Scenario("2 global-restore preserves schema markers and migration diagnostics");
        UniversalSqueakerSettings s = NewSettings();
        PolluteAll(s);
        s.settingsSchemaVersion = 5;
        s.voicePackSchemaVersion = 2;

        int beforeSchema = s.settingsSchemaVersion;
        int beforeVoiceSchema = s.voicePackSchemaVersion;
        bool beforeLoaded = s.SettingsLoadedFromFile;
        bool beforePending = s.MigrationPersistencePendingForFixture;
        bool beforeBlocked = s.MigrationPersistenceBlockedForFixture;

        SqueakResetOutcome outcome = s.ResetAllSettings();
        Check("preserve: outcome Applied", outcome == SqueakResetOutcome.Applied);
        Check("preserve: settingsSchemaVersion untouched", s.settingsSchemaVersion == beforeSchema);
        Check("preserve: voicePackSchemaVersion untouched", s.voicePackSchemaVersion == beforeVoiceSchema);
        Check("preserve: SettingsLoadedFromFile untouched", s.SettingsLoadedFromFile == beforeLoaded);
        Check("preserve: migration pending flag untouched", s.MigrationPersistencePendingForFixture == beforePending);
        Check("preserve: migration blocked flag untouched", s.MigrationPersistenceBlockedForFixture == beforeBlocked);
    }

    private static void DistanceRestoreKeepsVolumeAndDomains()
    {
        Scenario("3 distance restore keeps volume and other domains");
        UniversalSqueakerSettings s = NewSettings();
        s.distancePreset = SqueakDistancePreset.Strong;
        s.distanceRange = new FloatRange(15f, 40f);
        s.globalVolumeFactor = 0.4f;
        s.actionTuning.Add(new ActionTuningRecord { actionKey = "Call", raceDefName = "RaceA", hasScope = true, scope = SqueakActionScope.ActiveCommand });
        s.voicePackSelections.Add(new VoicePackSelectionRecord { scope = SqueakVoicePackScope.Race, raceDefName = "RaceA", enabledPackKeys = new List<string> { "k1" } });
        Probe.Reset();

        SqueakResetOutcome outcome = s.ResetDistanceDefaults();
        Check("distance: Applied", outcome == SqueakResetOutcome.Applied);
        Check("distance: Balanced + 15-50", s.distancePreset == SqueakDistancePreset.Balanced
            && s.distanceRange.min == 15f && s.distanceRange.max == 50f);
        Check("distance: globalVolumeFactor RETAINED (§4.3 distance row)", Math.Abs(s.globalVolumeFactor - 0.4f) < 0.0001f);
        Check("distance: actionTuning retained", s.actionTuning.Count == 1);
        Check("distance: voicePackSelections retained", s.voicePackSelections.Count == 1);
        Check("distance: publish = one distance channel + one persistence",
            Probe.DistanceApplyCount == 1 && Probe.SaveQueuedCount == 1
            && Probe.DiscreteNotifyCount == 0 && Probe.ContinuousNotifyCount == 0);

        outcome = s.ResetDistanceDefaults();
        Check("distance: repeat NoChange with zero side effects", outcome == SqueakResetOutcome.NoChange
            && Probe.SaveQueuedCount == 1 && Probe.DistanceApplyCount == 1);
    }

    private static void ActionRowRestoreExactIdentity()
    {
        Scenario("4 action-row restore targets the exact identity incl. stale duplicates and the anchor");
        UniversalSqueakerSettings s = NewSettings();
        PolluteAll(s);
        int untouchedCountBefore = s.actionTuning.Count;
        Probe.Reset();

        SqueakResetOutcome outcome = s.ResetActionTuningRow("Call", "RaceA", "XenoA");
        Check("row: Applied", outcome == SqueakResetOutcome.Applied);
        Check("row: BOTH identity rows (main + stale duplicate) removed",
            !s.actionTuning.Any(r => r.actionKey == "Call" && r.raceDefName == "RaceA" && r.xenotypeDefName == "XenoA"));
        Check("row: other-key row in the SAME domain preserved (Eat/RaceA/XenoA with its anchor)",
            s.actionTuning.Any(r => r.actionKey == "Eat" && r.raceDefName == "RaceA" && r.xenotypeDefName == "XenoA"
                && r.hasProbabilityMultiplier && r.sourcePresetDefName == "PresetOne"));
        Check("row: race-layer row preserved (Move/RaceA)",
            s.actionTuning.Any(r => r.actionKey == "Move" && r.raceDefName == "RaceA" && r.xenotypeDefName == "" && r.hasScope));
        Check("row: layer-0 row preserved (Sleep/Global)",
            s.actionTuning.Any(r => r.actionKey == "Sleep" && r.raceDefName == "" && r.xenotypeDefName == "" && r.hasScope));
        Check("row: moodTuning/selections/legacy presets untouched",
            s.moodTuning.Count == 3 && s.voicePackSelections.Count == 2 && s.xenotypePresets.Count == 1);
        Check("row: exactly one discrete publish + one persistence",
            Probe.DiscreteNotifyCount == 1 && Probe.SaveQueuedCount == 1 && Probe.ContinuousNotifyCount == 0);
        Check("row: list lost exactly the two identity rows", s.actionTuning.Count == untouchedCountBefore - 2);

        outcome = s.ResetActionTuningRow("Call", "RaceA", "XenoA");
        Check("row: repeat NoChange zero side effects", outcome == SqueakResetOutcome.NoChange
            && Probe.DiscreteNotifyCount == 1 && Probe.SaveQueuedCount == 1);
    }

    private static void ActionRowRestoreRejectsInvalidTargets()
    {
        Scenario("5 action-row restore rejects invalid/unsupported targets with zero side effects");
        UniversalSqueakerSettings s = NewSettings();
        PolluteAll(s);
        Probe.Reset();

        Check("row: empty actionKey Rejected", s.ResetActionTuningRow("", "RaceA", "XenoA") == SqueakResetOutcome.Rejected);
        Check("row: race-empty + xeno-present Rejected", s.ResetActionTuningRow("Call", "", "XenoA") == SqueakResetOutcome.Rejected);
        Check("row: unknown identity is NoChange (never falls through to Global)",
            s.ResetActionTuningRow("Joy", "RaceZ", "XenoZ") == SqueakResetOutcome.NoChange);
        Check("row: rejected/unknown ops changed nothing",
            Probe.SaveQueuedCount == 0 && Probe.DiscreteNotifyCount == 0 && Probe.ContinuousNotifyCount == 0
            && s.actionTuning.Count == 5 && s.voicePackSelections.Count == 2 && s.xenotypePresets.Count == 1
            && s.moodTuning.Count == 3 && s.globalMinIntervalTicks == 999);
    }

    private static void ActionAreaRestorePreservesMultiplierAndOtherDomains()
    {
        Scenario("6 action-area restore: exact domain, multiplier preserved, empty domain not Global");
        UniversalSqueakerSettings s = NewSettings();
        PolluteAll(s);
        // A different xenotype domain that must survive a (RaceA, XenoA) area restore.
        s.actionTuning.Add(new ActionTuningRecord
        {
            actionKey = "Call", raceDefName = "RaceA", xenotypeDefName = "XenoB",
            hasIntervalMultiplier = true, intervalMultiplier = 0.75f, sourcePresetDefName = "PresetTwo",
        });
        Probe.Reset();

        SqueakResetOutcome outcome = s.ResetActionTuningArea("RaceA", "XenoA");
        Check("area: Applied", outcome == SqueakResetOutcome.Applied);
        Check("area: both (RaceA,XenoA) action rows for BOTH keys cleared (Call incl. duplicate, Eat incl. anchor)",
            !s.actionTuning.Any(r => r.raceDefName == "RaceA" && r.xenotypeDefName == "XenoA"));
        Check("area: sibling xenotype domain preserved (Call/RaceA/XenoB with anchor)",
            s.actionTuning.Any(r => r.raceDefName == "RaceA" && r.xenotypeDefName == "XenoB" && r.sourcePresetDefName == "PresetTwo"));
        Check("area: race-layer and layer-0 rows preserved",
            s.actionTuning.Count(r => r.raceDefName == "RaceA" && r.xenotypeDefName == "") == 1
            && s.actionTuning.Count(r => r.raceDefName == "" && r.xenotypeDefName == "") == 1);
        Check("area: moodTuning preserved", s.moodTuning.Count == 3);
        Check("area: selections and legacy moodOverrides preserved", s.voicePackSelections.Count == 2 && s.moodOverrides.Count == 1);
        Check("area: xenotypePresets NOT enumerated; domain-level overall multiplier preserved (§6.6)",
            s.xenotypePresets.Count == 1 && s.xenotypePresets[0].hasOverallIntervalMultiplier
            && Math.Abs(s.xenotypePresets[0].overallIntervalMultiplier - 2.5f) < 0.0001f);
        Check("area: plain settings preserved (no disguised global)",
            s.globalMinIntervalTicks == 999 && s.distancePreset == SqueakDistancePreset.Strong);
        Check("area: one discrete publish + one persistence",
            Probe.DiscreteNotifyCount == 1 && Probe.SaveQueuedCount == 1 && Probe.ContinuousNotifyCount == 0);

        outcome = s.ResetActionTuningArea("RaceA", "XenoA");
        Check("area: repeat NoChange zero side effects", outcome == SqueakResetOutcome.NoChange
            && Probe.DiscreteNotifyCount == 1 && Probe.SaveQueuedCount == 1);

        outcome = s.ResetActionTuningArea("RaceQ", "");
        Check("area: empty unsupported domain NoChange, NOT a Global fallback",
            outcome == SqueakResetOutcome.NoChange
            && s.xenotypePresets.Count == 1 && s.moodTuning.Count == 3 && s.globalMinIntervalTicks == 999
            && Probe.SaveQueuedCount == 1);
        Check("area: invalid domain Rejected", s.ResetActionTuningArea("", "XenoA") == SqueakResetOutcome.Rejected);
    }

    private static void ActionAreaLayerZeroIsNotGlobal()
    {
        Scenario("7 layer-0 action area restore is a distinct operation from the global restore");
        UniversalSqueakerSettings s = NewSettings();
        PolluteAll(s);
        Probe.Reset();

        SqueakResetOutcome outcome = s.ResetActionTuningArea("", "");
        Check("layer0-area: Applied", outcome == SqueakResetOutcome.Applied);
        Check("layer0-area: only the layer-0 action row was removed",
            !s.actionTuning.Any(r => r.raceDefName == "" && r.xenotypeDefName == "")
            && s.actionTuning.Count == 4);
        Check("layer0-area: everything else retained (not Global)",
            s.voicePackSelections.Count == 2 && s.xenotypePresets.Count == 1 && s.moodTuning.Count == 3
            && s.moodOverrides.Count == 1 && s.globalMinIntervalTicks == 999);
        Check("layer0-area: one publish round + one persistence",
            Probe.DiscreteNotifyCount == 1 && Probe.SaveQueuedCount == 1);
    }

    private static void MoodAreaRestoreKeepsLegacyDictAndOtherDomains()
    {
        Scenario("8 mood-area restore: factors+anchor across moods, legacy dict unsynced");
        UniversalSqueakerSettings s = NewSettings();
        PolluteAll(s);
        s.moodTuning.Add(new MoodTuningRecord
        {
            mood = SqueakMood.Break, raceDefName = "RaceA", xenotypeDefName = "XenoA",
            hasPitchJitter = true, pitchJitter = new FloatRange(0.5f, 1.5f), sourcePresetDefName = "PresetOne",
        });
        Probe.Reset();

        SqueakResetOutcome outcome = s.ResetMoodTuningArea("RaceA", "XenoA");
        Check("mood-area: Applied", outcome == SqueakResetOutcome.Applied);
        Check("mood-area: all (RaceA,XenoA) mood rows cleared and dropped (Neutral/Bad/Break incl. anchors)",
            !s.moodTuning.Any(r => r.raceDefName == "RaceA" && r.xenotypeDefName == "XenoA"));
        Check("mood-area: other domain preserved (Good/RaceB jitter)",
            s.moodTuning.Count == 1 && s.moodTuning[0].mood == SqueakMood.Good && s.moodTuning[0].hasPitchJitter);
        Check("mood-area: legacy moodOverrides dict NOT synced (§6.4 row 5: not runtime-participating)", s.moodOverrides.Count == 1);
        Check("mood-area: actionTuning/selections/presets/multiplier preserved",
            s.actionTuning.Count == 5 && s.voicePackSelections.Count == 2 && s.xenotypePresets.Count == 1
            && s.xenotypePresets[0].hasOverallIntervalMultiplier);
        Check("mood-area: one continuous publish + one persistence",
            Probe.ContinuousNotifyCount == 1 && Probe.DiscreteNotifyCount == 0 && Probe.SaveQueuedCount == 1);

        outcome = s.ResetMoodTuningArea("RaceA", "XenoA");
        Check("mood-area: repeat NoChange zero side effects", outcome == SqueakResetOutcome.NoChange
            && Probe.ContinuousNotifyCount == 1 && Probe.SaveQueuedCount == 1);
        Check("mood-area: invalid domain Rejected", s.ResetMoodTuningArea("", "XenoA") == SqueakResetOutcome.Rejected);
    }

    private static void NormalAreaRestoresMatchUiBindings()
    {
        Scenario("9 normal visible-area restores match the UI-bound field sets (host 397-441/449-463/470-508/515-539)");
        UniversalSqueakerSettings s = NewSettings();
        PolluteAll(s);
        s.actionTuning.Clear();
        s.moodTuning.Clear();
        s.voicePackSelections.Clear();
        s.xenotypePresets.Clear();
        s.moodOverrides.Clear();

        Probe.Reset();
        SqueakResetOutcome outcome = s.ResetNormalArea(SqueakNormalResetArea.Distance);
        Check("area-Distance: Applied", outcome == SqueakResetOutcome.Applied);
        Check("area-Distance: volume+preset+range restored", Math.Abs(s.globalVolumeFactor - 1f) < 0.0001f
            && s.distancePreset == SqueakDistancePreset.Balanced && s.distanceRange.min == 15f && s.distanceRange.max == 50f);
        Check("area-Distance: other areas polluted still", s.globalMinIntervalTicks == 999 && s.devLoggingMode == SqueakDevLoggingMode.Enabled);
        Check("area-Distance: publish shape (volume static + one distance channel + one persistence)",
            Probe.DistanceApplyCount == 1 && Probe.SaveQueuedCount == 1 && Probe.DiscreteNotifyCount == 0);

        outcome = s.ResetNormalArea(SqueakNormalResetArea.Basics);
        Check("area-Basics: Applied", outcome == SqueakResetOutcome.Applied);
        Check("area-Basics: eggs/scales/camera/baby/eat-pair restored",
            !s.allowEasterEggSounds && s.scaleCooldownWithTimeSpeed && s.scaleFrequencyWithTalking
            && s.scalePeriodicWithAudiblePopulation && !s.showCameraIndicator && !s.allowBabyActions
            && !s.eatPrecisionEnabled && !s.eatPrecisionIncludeDrugs);
        Check("area-Basics: timing/diagnostics/gate-external retained",
            s.globalMinIntervalTicks == 999 && s.globalCooldownMultiplier == 2.5f
            && s.devLoggingMode == SqueakDevLoggingMode.Enabled && s.localizeDebugActions && s.allowExternalActions);
        Check("area-Basics: cheap statics republished + camera static + discrete for eggs + one persistence",
            Probe.DiscreteNotifyCount == 1 && Probe.SaveQueuedCount == 2
            && CompSqueaker.ScaleCooldownWithTimeSpeed && CompSqueaker.ScaleFrequencyWithTalking
            && CompSqueaker.EatPrecisionEnabled == false && !SqueakDebug.ShowCameraIndicator);

        outcome = s.ResetNormalArea(SqueakNormalResetArea.Timing);
        Check("area-Timing: Applied", outcome == SqueakResetOutcome.Applied);
        Check("area-Timing: interval 216 + cooldown 1f", s.globalMinIntervalTicks == 216 && s.globalCooldownMultiplier == 1f);
        Check("area-Timing: diagnostics retained", s.devLoggingMode == SqueakDevLoggingMode.Enabled && s.localizeDebugActions);
        Check("area-Timing: cheap channel + third persistence", Probe.SaveQueuedCount == 3 && Probe.DiscreteNotifyCount == 1);

        outcome = s.ResetNormalArea(SqueakNormalResetArea.Diagnostics);
        Check("area-Diagnostics: Applied", outcome == SqueakResetOutcome.Applied);
        Check("area-Diagnostics: Auto + localize false", s.devLoggingMode == SqueakDevLoggingMode.Auto && !s.localizeDebugActions);
        Check("area-Diagnostics: logging + debug-menu channels + fourth persistence",
            Probe.LoggingModeChangedCount == 1 && Probe.DebugMenuSetEnabledCount == 1 && Probe.SaveQueuedCount == 4);

        foreach (SqueakNormalResetArea area in Enum.GetValues(typeof(SqueakNormalResetArea)))
            Check("area-repeat idempotent: " + area + " NoChange zero new writes",
                s.ResetNormalArea(area) == SqueakResetOutcome.NoChange);
        Check("area-repeat: persistence count stayed at 4", Probe.SaveQueuedCount == 4);
    }

    private static void NormalAreaUnknownValueRejected()
    {
        Scenario("10 normal-area restore rejects an unsupported enum value with zero side effects");
        UniversalSqueakerSettings s = NewSettings();
        s.devLoggingMode = SqueakDevLoggingMode.Enabled;
        Probe.Reset();
        Check("area-unknown: Rejected", s.ResetNormalArea((SqueakNormalResetArea)99) == SqueakResetOutcome.Rejected);
        Check("area-unknown: zero side effects", Probe.SaveQueuedCount == 0 && s.devLoggingMode == SqueakDevLoggingMode.Enabled);
    }

    private static void CopiedConfigProductionRoundTripWithFallbackBytes()
    {
        Scenario("11 copied-config production round trip + fallback file byte/parsing preservation");
        string configDir = Path.Combine(tmpRoot, "config");
        Directory.CreateDirectory(configDir);

        // (a) production Scribe write of a polluted copy (a temporary copy - never the player's Config).
        UniversalSqueakerSettings polluted = NewSettings();
        PolluteAll(polluted);
        string pollutedPath = Path.Combine(configDir, "UniversalSqueaker_Settings_polluted.xml");
        SafeSaver.Save(pollutedPath, "Settings", () => polluted.ExposeData());

        XmlDocument pollutedXml = new();
        pollutedXml.Load(pollutedPath);
        Check("roundtrip-polluted: settingsSchemaVersion node present (forceSave)", Node(pollutedXml, "settingsSchemaVersion") == "5");
        Check("roundtrip-polluted: polluted scalars serialized (voicePackMode/gates)",
            Node(pollutedXml, "voicePackMode") == "Vanilla" && Node(pollutedXml, "allowEasterEggSounds") == "True"
            && Node(pollutedXml, "globalMinIntervalTicks") == "999");
        Count(pollutedXml, "voicePackSelections", 2);
        Count(pollutedXml, "xenotypePresets", 1);
        Count(pollutedXml, "actionTuning", 5);
        Count(pollutedXml, "moodTuning", 3);

        // (b) the production fallback store behind its test seam, with a player-written table.
        SqueakFallbackProfileStore.ConfigDirectoryForTests = configDir;
        try
        {
            RaceKey race = new("RaceA");
            BuiltInFallbackTable source = new(new List<FallbackProfile>
            {
                new FallbackProfile(race, 3, new Dictionary<string, string> { ["Call"] = "snd_call_maintainer" }),
            });
            SqueakFallbackProfileStore.LoadOrRebuild(source);
            SqueakFallbackProfileStore.StoreOutcome saved = SqueakFallbackProfileStore.SaveProfile(
                race, new FallbackDelta(new Dictionary<string, string> { ["Call"] = "snd_custom_player" }));
            Check("fallback: player table written through the production store", saved == SqueakFallbackProfileStore.StoreOutcome.Written);

            string profilePath = Path.Combine(configDir, "UniversalSqueaker_Profile_RaceA.xml");
            Check("fallback: player file exists", File.Exists(profilePath));
            byte[] bytesBefore = File.Exists(profilePath) ? File.ReadAllBytes(profilePath) : Array.Empty<byte>();
            string hashBefore = Sha256(bytesBefore);
            FallbackDelta? deltaBefore = SqueakFallbackProfileStore.LoadPlayerDelta(race);
            Log("  fallback file before restore: " + hashBefore + " bytes=" + bytesBefore.Length
                + " delta=[Call->" + (deltaBefore?.Overrides.TryGetValue("Call", out string? sk) == true ? sk : "?") + "]");

            // (c) production LOAD path -> live instance -> production reset on the loaded instance.
            UniversalSqueakerSettings loaded = NewSettings();
            UniversalSqueakerSettings holder = loaded; // the host-side reference (ModSettings holder)
            Scribe.loader.InitLoading(pollutedPath);
            loaded.ExposeData();
            Scribe.loader.FinalizeLoading();
            Check("roundtrip-load: production load maps polluted values",
                loaded.voicePackMode == SqueakVoicePackMode.Vanilla && loaded.globalMinIntervalTicks == 999
                && loaded.actionTuning.Count == 5 && loaded.xenotypePresets.Count == 1
                && loaded.xenotypePresets[0].hasOverallIntervalMultiplier);
            Check("roundtrip-load: settingsLoadedFromFile diagnostic set by the load path", loaded.SettingsLoadedFromFile);

            Probe.Reset();
            SqueakResetOutcome outcome = loaded.ResetAllSettings();
            Check("roundtrip-reset: Applied on the LOADED instance", outcome == SqueakResetOutcome.Applied);
            Check("roundtrip-reset: live settings reference identity preserved (holder == loaded)", ReferenceEquals(holder, loaded));
            Check("roundtrip-reset: diagnostics preserved (loaded flag true, no migration pending/blocked)",
                loaded.SettingsLoadedFromFile && !loaded.MigrationPersistencePendingForFixture
                && !loaded.MigrationPersistenceBlockedForFixture && loaded.settingsSchemaVersion == 5);
            Check("roundtrip-reset: exactly one persistence request", Probe.SaveQueuedCount == 1);

            // (d) fallback files byte-identical and parse-map-identical across the restore.
            byte[] bytesAfter = File.Exists(profilePath) ? File.ReadAllBytes(profilePath) : Array.Empty<byte>();
            string hashAfter = Sha256(bytesAfter);
            FallbackDelta? deltaAfter = SqueakFallbackProfileStore.LoadPlayerDelta(race);
            Check("fallback: player profile file byte-identical after global restore", hashBefore == hashAfter);
            Check("fallback: player delta parse map unchanged", deltaAfter != null
                && deltaAfter.Overrides.Count == 1 && deltaAfter.Overrides["Call"] == "snd_custom_player");
            Check("fallback: resolved table still carries the player override",
                SqueakFallbackProfileStore.For(race) != null
                && SqueakFallbackProfileStore.For(race)!.TryGetSoundKey("Call", out string? liveSound)
                && liveSound == "snd_custom_player");
            Log("  fallback file after restore:  " + hashAfter + " bytes=" + bytesAfter.Length);

            // (e) production Scribe write of the restored copy: owned nodes absent/default, records empty.
            string restoredPath = Path.Combine(configDir, "UniversalSqueaker_Settings_restored.xml");
            SafeSaver.Save(restoredPath, "Settings", () => loaded.ExposeData());
            XmlDocument restoredXml = new();
            restoredXml.Load(restoredPath);
            Check("roundtrip-restored: schema markers still serialized honestly",
                Node(restoredXml, "settingsSchemaVersion") == "5" && Node(restoredXml, "voicePackSchemaVersion") == "2");
            Check("roundtrip-restored: every owned scalar omitted at the default boundary",
                Node(restoredXml, "voicePackMode") == null && Node(restoredXml, "allowEasterEggSounds") == null
                && Node(restoredXml, "globalMinIntervalTicks") == null && Node(restoredXml, "distanceRange") == null
                && Node(restoredXml, "globalVolumeFactor") == null && Node(restoredXml, "devLoggingMode") == null
                && Node(restoredXml, "localizeDebugActions") == null && Node(restoredXml, "allowExternalActions") == null
                && Node(restoredXml, "allowBabyActions") == null && Node(restoredXml, "eatPrecisionEnabled") == null);
            Count(restoredXml, "voicePackSelections", 0);
            Count(restoredXml, "xenotypePresets", 0);
            Count(restoredXml, "actionTuning", 0);
            Count(restoredXml, "moodTuning", 0);

            // (f) parse map: a fresh production load of the restored copy resolves to defaults.
            UniversalSqueakerSettings reloaded = NewSettings();
            Scribe.loader.InitLoading(restoredPath);
            reloaded.ExposeData();
            Scribe.loader.FinalizeLoading();
            Check("roundtrip-parsemap: reload of the restored copy is all-default",
                reloaded.voicePackMode == SqueakVoicePackMode.Fallback && reloaded.globalMinIntervalTicks == 216
                && reloaded.distancePreset == SqueakDistancePreset.Balanced && reloaded.distanceRange.min == 15f
                && reloaded.actionTuning.Count == 0 && reloaded.moodTuning.Count == 0
                && reloaded.voicePackSelections.Count == 0 && reloaded.xenotypePresets.Count == 0);
            Check("roundtrip-parsemap: restored copy then NoChange on re-reset",
                reloaded.ResetAllSettings() == SqueakResetOutcome.NoChange);
            Log("  settings copy hashes: polluted=" + Sha256(File.ReadAllBytes(pollutedPath))
                + " restored=" + Sha256(File.ReadAllBytes(restoredPath)));
        }
        finally
        {
            SqueakFallbackProfileStore.ConfigDirectoryForTests = null;
        }
    }

    private static void MigrationBlockedStateSurvivesGlobalRestore()
    {
        Scenario("12 migration-blocked legacy state survives the global restore without disguise");
        UniversalSqueakerSettings s = NewSettings(schema: 3, voiceSchema: 1);
        PolluteAll(s);
        // Force the legacy transaction to fail closed: a preset record that needs the (absent)
        // default race, so TryCreateV4Records refuses.
        s.xenotypePresets.Add(new XenotypePresetRecord { raceDefName = "", xenotypeDefName = "XenoOrphan" });

        bool migrated = s.MigrateV3RecordsTransactionally();
        Check("blocked: v3 transaction fails closed", !migrated && s.MigrationPersistenceBlockedForFixture
            && s.settingsSchemaVersion == 3 && s.voicePackSchemaVersion == 1);

        Probe.Reset();
        SqueakResetOutcome outcome = s.ResetAllSettings();
        Check("blocked: global restore still applies to business fields", outcome == SqueakResetOutcome.Applied
            && s.voicePackMode == SqueakVoicePackMode.Fallback && s.actionTuning.Count == 0
            && s.xenotypePresets.Count == 0);
        Check("blocked: schema markers NOT disguised", s.settingsSchemaVersion == 3 && s.voicePackSchemaVersion == 1);
        Check("blocked: migration blocked/pending diagnostics preserved",
            s.MigrationPersistenceBlockedForFixture && !s.MigrationPersistencePendingForFixture);
    }

    // ---------------------------------------------------------------- plumbing

    private static void Scenario(string name)
    {
        Probe.Reset();
        Console.WriteLine();
        Console.WriteLine("== " + name + " ==");
    }

    private static void Check(string name, bool ok)
    {
        checks++;
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name);
        if (!ok) failures++;
    }

    private static void Log(string text) => Console.WriteLine(text);

    private static string? Node(XmlDocument doc, string name) => doc.SelectSingleNode("/Settings/" + name)?.InnerText;
    private static void Count(XmlDocument doc, string container, int expectedItems)
    {
        XmlNodeList? items = doc.SelectNodes("/Settings/" + container + "/li");
        int count = items?.Count ?? 0;
        Check("xml: " + container + " li count == " + expectedItems, count == expectedItems);
    }

    private static string Sha256(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }
}
