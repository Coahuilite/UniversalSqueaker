using System;
using System.Collections.Generic;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Settings' Scribe boundary. Migration never writes Config directly: it builds replacement records first,
/// publishes them atomically only on success, and lets the startup main-thread bridge queue base.WriteSettings().
/// </summary>
public partial class UniversalSqueakerSettings
{
    private const int CurrentSettingsSchemaVersion = 5;
    private const int CurrentVoicePackSchemaVersion = 2;
    private const int LegacyVoicePackSchemaVersion = 1;
    // US has no product legacy race. Explicit-race records can migrate; records that need a default fail closed.
    private const string LegacyDefaultRaceDefName = "";

    // 0.3.1 波 3c 彩蛋开关（决策 §2.4）：默认关；开时 IsEgg 条目以加性池成员身份参与抽取，关时候选池只含普通条目。
    // 0.3.1 为隐藏 Scribe 开关（UI 不渲染）；0.3.2 UI 专项正式化。false = 序列化省略（fixture 字节稳定）。
    public bool allowEasterEggSounds;
#if US_EXPERIMENTAL
    /// <summary>
    /// 实验开关占位（仅 Dev/EXP 构建编译，默认关，重启生效）。
    /// 当前 US 0.1.x 没有内置 race 映射实验；该字段仅为未来通用实验保留。
    /// </summary>
    public bool experimentalKiiroCompat;
#endif

    private bool distanceRangeWasLoaded;
    private bool scaleFrequencyWithTalkingWasLoaded;
    private bool settingsSchemaWasLoaded;
    private bool voicePackModeWasLoaded;
    private bool migrationPersistencePending;
    private bool migrationPersistenceBlocked;
    // Reached only when a settings file was successfully deserialized through Scribe (ReadModSettings ran
    // ExposeData in LoadingVars). A fresh or unreadable file never reaches it -> SettingsOrigin FreshCreated.
    private bool settingsLoadedFromFile;
    internal bool SettingsLoadedFromFile => settingsLoadedFromFile;
    internal bool MigrationPersistencePendingForFixture => migrationPersistencePending;
    internal bool MigrationPersistenceBlockedForFixture => migrationPersistenceBlocked;
    internal bool IsPersistenceBlockedByMigrationFailure => migrationPersistenceBlocked;

    public override void ExposeData()
    {
        base.ExposeData();
        // Presence flags are reset once per LoadingVars pass and survive until PostLoadInit.
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            settingsSchemaWasLoaded = false;
            distanceRangeWasLoaded = false;
            scaleFrequencyWithTalkingWasLoaded = false;
            voicePackModeWasLoaded = false;
            settingsLoadedFromFile = true;
        }

        Scribe_Values.Look(ref voicePackMode, "voicePackMode", SqueakVoicePackMode.Fallback);
        // v2 is intentionally non-default at the Scribe boundary so the completed 1→2 migration is durable.
        Scribe_Values.Look(ref voicePackSchemaVersion, "voicePackSchemaVersion", LegacyVoicePackSchemaVersion);
        Scribe_Values.Look(ref settingsSchemaVersion, "settingsSchemaVersion", CurrentSettingsSchemaVersion, forceSave: true);
        Scribe_Values.Look(ref scaleCooldownWithTimeSpeed, "scaleCooldownWithTimeSpeed", true);
        Scribe_Values.Look(ref scaleFrequencyWithTalking, "scaleFrequencyWithTalking", GetDefaultScaleFrequencyWithTalking());
        Scribe_Values.Look(ref scalePeriodicWithAudiblePopulation, "scalePeriodicWithAudiblePopulation", true);
        Scribe_Values.Look(ref globalMinIntervalTicks, "globalMinIntervalTicks", 216);
        Scribe_Values.Look(ref localizeDebugActions, "localizeDebugActions", false);
        Scribe_Values.Look(ref showCameraIndicator, "showCameraIndicator", false);
        Scribe_Values.Look(ref devLoggingMode, "devLoggingMode", SqueakDevLoggingMode.Auto);
        Scribe_Values.Look(ref globalCooldownMultiplier, "globalCooldownMultiplier", 1f);
        Scribe_Values.Look(ref distancePreset, "distancePreset", SqueakDistancePreset.Balanced);
        Scribe_Values.Look(ref distanceRange, "distanceRange", GetDistancePresetRange(SqueakDistancePreset.Balanced));
        Scribe_Values.Look(ref allowEasterEggSounds, "allowEasterEggSounds", false);
        Scribe_Values.Look(ref allowExternalActions, "allowExternalActions", false);
#if US_EXPERIMENTAL
        Scribe_Values.Look(ref experimentalKiiroCompat, "experimentalKiiroCompat", false);
#endif

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            scaleFrequencyWithTalkingWasLoaded = Scribe.loader?.curXmlParent?["scaleFrequencyWithTalking"] != null;
            distanceRangeWasLoaded = Scribe.loader?.curXmlParent?["distanceRange"] != null;
            settingsSchemaWasLoaded = Scribe.loader?.curXmlParent?["settingsSchemaVersion"] != null;
            // An absent mode means the player never explicitly selected the voice source policy.
            voicePackModeWasLoaded = Scribe.loader?.curXmlParent?["voicePackMode"] != null;
        }

        Scribe_Collections.Look(ref moodOverrides, "moodOverrides", LookMode.Value, LookMode.Deep);
        Scribe_Collections.Look(ref voicePackSelections, "voicePackSelections", LookMode.Deep);
        Scribe_Collections.Look(ref xenotypePresets, "xenotypePresets", LookMode.Deep);
        Scribe_Collections.Look(ref actionTuning, "actionTuning", LookMode.Deep);
        Scribe_Collections.Look(ref moodTuning, "moodTuning", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.LoadingVars && moodOverrides == null)
            moodOverrides = new Dictionary<SqueakMood, SqueakMoodMod>();
        if (Scribe.mode == LoadSaveMode.LoadingVars && moodTuning == null)
            moodTuning = new List<MoodTuningRecord>();

        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;

        if (!Enum.IsDefined(typeof(SqueakVoicePackMode), voicePackMode)) voicePackMode = SqueakVoicePackMode.Vanilla;
        if (!Enum.IsDefined(typeof(SqueakDevLoggingMode), devLoggingMode)) devLoggingMode = SqueakDevLoggingMode.Auto;
        SqueakLog.Configure(devLoggingMode);

        // Retain the older v2→v3 numeric repair without treating it as a record-schema commit.
        if (!settingsSchemaWasLoaded || settingsSchemaVersion < 3)
        {
            if (Math.Abs(globalCooldownMultiplier - 1.2f) <= .0001f) globalCooldownMultiplier = 1f;
        }
        globalCooldownMultiplier = Math.Max(0f, Math.Min(globalCooldownMultiplier, 3f));
        if (!scaleFrequencyWithTalkingWasLoaded) scaleFrequencyWithTalking = GetDefaultScaleFrequencyWithTalking();
        if (!distanceRangeWasLoaded) distanceRange = GetDistancePresetRange(SqueakDistancePreset.Balanced);
        distanceRange = ClampDistanceRange(distanceRange);

        if (actionTuning == null) actionTuning = new List<ActionTuningRecord>();
        if (moodTuning == null) moodTuning = new List<MoodTuningRecord>();

        // 迁移事务：v3/v1 → v5/v2 统一在 MigrateV3RecordsTransactionally 内完成，含 actionTuning 与 moodTuning。
        // US 0.1.x 未发布过 pre-v4 配置；globalActionEnabled 旧字段不支持（无 legacy 用户），
        // 若未来需要支持，必须恢复 load-only 读取并纳入同一事务迁移。
        bool migrationNeeded = settingsSchemaVersion < CurrentSettingsSchemaVersion || voicePackSchemaVersion < CurrentVoicePackSchemaVersion;
        if (migrationNeeded) MigrateV3RecordsTransactionally();
        else
        {
            if (voicePackSelections == null) voicePackSelections = new List<VoicePackSelectionRecord>();
            if (xenotypePresets == null) xenotypePresets = new List<XenotypePresetRecord>();
        }
    }

    /// <summary>
    /// v3/v1 → v4/v2. No source list or schema marker changes until all cloned records normalize and validate.
    /// A failure deliberately leaves the old in-memory lists/schema available for an idempotent retry next startup.
    /// Internal fixture harnesses call this after Scribe PostLoadInit to prove the same production transaction.
    /// </summary>
    internal bool MigrateV3RecordsTransactionally()
    {
        if (settingsSchemaVersion >= CurrentSettingsSchemaVersion && voicePackSchemaVersion >= CurrentVoicePackSchemaVersion) return true;

        if (!SqueakSettingsMigration.TryCreateV4Records(
                voicePackSelections,
                xenotypePresets,
                LegacyDefaultRaceDefName,
                out List<VoicePackSelectionRecord> migratedSelections,
                out List<XenotypePresetRecord> migratedPresets,
                out string failure))
        {
            // Preserve the on-disk legacy source unchanged: no later UI/framework Config write may erase the
            // load-only targetDefName before the next startup gets to retry this same transaction.
            migrationPersistenceBlocked = true;
            SqueakLog.TargetRejected("settings_schema_migration", "migration_failed:" + failure);
            return false;
        }

        // S5: 旧心情存储 → 统一分层 MoodTuningRecord（moodOverrides → 层 0；xenotypePresets[].moodOverrides → 层 2）。
        // 仅当 settings schema 真正落后时重建 moodTuning；voicePackSchema 单独落后不得用旧字段覆盖用户新编辑。
        // 与记录迁移同事务：任一失败整体不提交，schema 标记保持旧值，下次启动可重试。
        bool settingsMigrationNeeded = settingsSchemaVersion < CurrentSettingsSchemaVersion;
        List<ActionTuningRecord>? migratedActionTuning = null;
        if (settingsMigrationNeeded)
        {
            if (settingsSchemaVersion < 4 && (actionTuning == null || actionTuning.Count == 0))
            {
                if (!SqueakSettingsMigration.TryCreateActionTuningRecords(
                        migratedPresets,
                        out List<ActionTuningRecord> actionTuningRecords,
                        out string actionFailure))
                {
                    migrationPersistenceBlocked = true;
                    SqueakLog.TargetRejected("settings_schema_migration", "action_tuning_migration_failed:" + actionFailure);
                    return false;
                }
                migratedActionTuning = actionTuningRecords;
            }

            if (!SqueakSettingsMigration.TryCreateMoodTuningRecords(
                    moodOverrides,
                    migratedPresets,
                    out List<MoodTuningRecord> migratedMoods,
                    out string moodFailure))
            {
                migrationPersistenceBlocked = true;
                SqueakLog.TargetRejected("settings_schema_migration", "mood_tuning_migration_failed:" + moodFailure);
                return false;
            }

            moodTuning = migratedMoods;
        }

        voicePackSelections = migratedSelections;
        xenotypePresets = migratedPresets;
        if (migratedActionTuning != null) actionTuning = migratedActionTuning;
        migrationPersistenceBlocked = false;
        if (settingsMigrationNeeded) settingsSchemaVersion = CurrentSettingsSchemaVersion;
        if (voicePackSchemaVersion < CurrentVoicePackSchemaVersion) voicePackSchemaVersion = CurrentVoicePackSchemaVersion;
        // This is consumed only by the main-thread startup callback, which queues the existing base.WriteSettings path.
        migrationPersistencePending = true;
        return true;
    }
}
