using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalSqueaker;

public partial class UniversalSqueakerSettings
{
    // Add-only Config field. Existing imported player records remain player-owned under their original meaning.
    public string behaviorSourcesXml = "";
    // Independent positive activity. Invalid external/Config input is rejected by the opportunity owner.
    public double activity = 1;

    public void SetActivity(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Activity must be positive and finite");
        }
        if (activity == value)
        {
            return;
        }
        activity = value;
        QueuePersistence();
    }

    internal bool TryWriteBehaviorCandidate(string path, string candidate, Action<string> serialize,
        out string failure, Action? beforeCommit = null)
    {
        string previous = behaviorSourcesXml;
        try
        {
            behaviorSourcesXml = candidate;
            return UsAtomicConfigFile.TryWrite(path, serialize, out failure, beforeCommit);
        }
        finally
        {
            behaviorSourcesXml = previous;
        }
    }

    public bool TryChangeBehaviorSource(BehaviorDomain domain, string? sourceId, BehaviorSourceDirectory directory,
        Func<string, bool> persistCandidate, out string failure)
    {
        failure = "";
        if (IsPersistenceBlockedByMigrationFailure)
        {
            failure = "SettingsMigrationBlocked";
            return false;
        }
        try
        {
            var candidate = BehaviorSourceState.FromXml(behaviorSourcesXml);
            if (sourceId == null)
            {
                candidate.Forget(domain);
            }
            else if (!candidate.Bind(domain, sourceId, directory))
            {
                failure = "InvalidBehaviorSource";
                return false;
            }
            return CommitBehaviorState(candidate, persistCandidate, out failure);
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ":" + error.Message;
            return false;
        }
    }

    public bool TryRefreshBehaviorSources(BehaviorSourceDirectory directory, Func<string, bool> persistCandidate, out string failure)
    {
        failure = "";
        if (IsPersistenceBlockedByMigrationFailure)
        {
            failure = "SettingsMigrationBlocked";
            return false;
        }
        try
        {
            var candidate = BehaviorSourceState.FromXml(behaviorSourcesXml);
            candidate.Refresh(directory);
            if (candidate.ToXml() == BehaviorSourceState.FromXml(behaviorSourcesXml).ToXml())
            {
                return true;
            }
            return CommitBehaviorState(candidate, persistCandidate, out failure);
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ":" + error.Message;
            return false;
        }
    }

    private bool CommitBehaviorState(BehaviorSourceState candidate, Func<string, bool> persistCandidate, out string failure)
    {
        failure = "";
        string xml = candidate.ToXml();
        if (BehaviorSourceState.FromXml(xml).ToXml() != xml)
        {
            throw new InvalidOperationException("Unstable source serialization");
        }
        if (!persistCandidate(xml))
        {
            failure = "BehaviorSourceSaveFailed";
            return false;
        }
        behaviorSourcesXml = xml;
        return true;
    }

    public BehaviorValues ResolveBehaviorSource(BehaviorDomain domain, string action, BehaviorSourceDirectory directory,
        Func<string, bool> implementationAvailable, out bool paused, Action<string>? reportPause = null)
    {
        var effective = CaptureBehaviorConfiguration().Resolve(domain, action, new BehaviorValues(), directory, implementationAvailable);
        paused = effective.Paused;
        if (paused) reportPause?.Invoke(effective.Failure);
        return effective.Read();
    }

    public BehaviorConfigurationSnapshot CaptureBehaviorConfiguration()
    {
        return new BehaviorConfigurationSnapshot(behaviorSourcesXml, actionTuning.Select(CapturePlayerRecord),
            voicePackMode != SqueakVoicePackMode.Disabled, activity, globalVolumeFactor);
    }
    private static BehaviorPlayerOverride CapturePlayerRecord(ActionTuningRecord record)
    {
        bool valid = record.IsValidLayer(out _);
        BehaviorValues values;
        try { values = ProjectBehaviorPlayerValues(record); }
        catch (ArgumentException) { values = new BehaviorValues(); valid = false; }
        return new BehaviorPlayerOverride(record.serviceFamily, record.targetNamespace,
            record.serviceFamily == "Pawn" ? record.raceDefName : record.objectTarget,
            record.xenotypeDefName, Kernel.ActionKey.DefinitionId(record.actionKey), values, valid);
    }

    /// <summary>Replace one sparse player row transactionally using the existing complete Settings serializer.</summary>
    public bool TryChangeBehaviorOverride(string family, string targetNamespace, string target, string child, string action,
        BehaviorValues values, Func<bool> persistCandidate, out string failure)
    {
        failure = "";
        if (IsPersistenceBlockedByMigrationFailure) { failure = "SettingsMigrationBlocked"; return false; }
        var row = new ActionTuningRecord { actionKey = Kernel.ActionKey.DefinitionId(action), serviceFamily = family,
            targetNamespace = targetNamespace, raceDefName = family == "Pawn" ? target : "", objectTarget = family == "NonPawn" ? target : "", xenotypeDefName = child };
        if (string.IsNullOrWhiteSpace(action) || action != action.Trim() || target != target.Trim() || child != child.Trim()
            || !row.IsValidLayer(out _) || !values.Valid || !values.ApplicableTo(family))
        { failure = "InvalidPlayerOverride"; return false; }
        row.hasEnabled = values.Enabled.HasValue; row.enabled = values.Enabled ?? true;
        row.hasScope = values.Scope.HasValue; row.scope = values.Scope ?? SqueakActionScope.AnyOccurrence;
        row.hasHardIntervalTicks = values.HardIntervalTicks.HasValue; row.hardIntervalTicks = values.HardIntervalTicks ?? 0;
        row.hasTalkingEnabled = values.TalkingEnabled.HasValue; row.talkingEnabled = values.TalkingEnabled ?? false;
        row.hasVolume = values.Volume.HasValue; row.volume = values.Volume ?? 1;
        row.hasPitch = values.Pitch.HasValue; row.pitch = values.Pitch ?? 1;
        row.hasEatPhase = values.Phase.HasValue; row.eatPhase = values.Phase ?? EatPhase.WholeProcess;
        row.hasNutritionScope = values.NutritionScope.HasValue; row.nutritionScope = values.NutritionScope ?? EatNutritionScope.Any;
        row.hasDrugScope = values.DrugScope.HasValue; row.drugScope = values.DrugScope ?? EatDrugScope.Any;
        row.hasSelectRequirePlayerControlled = values.SelectRequirePlayerControlled.HasValue; row.selectRequirePlayerControlled = values.SelectRequirePlayerControlled ?? true;
        row.hasSelectRequireAwake = values.SelectRequireAwake.HasValue; row.selectRequireAwake = values.SelectRequireAwake ?? true;
        row.hasSelectRequireNotDowned = values.SelectRequireNotDowned.HasValue; row.selectRequireNotDowned = values.SelectRequireNotDowned ?? true;
        foreach (var pair in values.ResponseEnabled) row.responses.Add(new ResponseTuningValue { key = pair.Key, hasEnabled = true, enabled = pair.Value });
        if (values.AgeEnabled.HasValue) row.responses.Add(new ResponseTuningValue { key = "us.core/Age", hasEnabled = true, enabled = values.AgeEnabled.Value });
        if (values.MoodEnabled.HasValue) row.responses.Add(new ResponseTuningValue { key = "us.core/Mood", hasEnabled = true, enabled = values.MoodEnabled.Value });
        foreach (var pair in values.Participation) row.responses.Add(new ResponseTuningValue { key = pair.Key, hasParticipation = true, apply = pair.Value });
        foreach (var pair in values.ResponseParameters) row.responseParameters.Add(new ResponseParameterValue { key = pair.Key, value = pair.Value });
        var previous = actionTuning;
        var candidate = previous.Where(x => !(x.serviceFamily == family && x.targetNamespace == targetNamespace
            && (family == "Pawn" ? x.raceDefName : x.objectTarget) == target && x.xenotypeDefName == child
            && Kernel.ActionKey.DefinitionId(x.actionKey) == row.actionKey)).Select(ActionTuningRecord.Clone).ToList();
        // The new behavior replacement does not erase old numeric/provenance fields still owned by retained callers.
        foreach (var old in previous.Where(x => x.serviceFamily == family && x.targetNamespace == targetNamespace
            && (family == "Pawn" ? x.raceDefName : x.objectTarget) == target && x.xenotypeDefName == child
            && Kernel.ActionKey.DefinitionId(x.actionKey) == row.actionKey))
        {
            if (!old.hasIntervalMultiplier && !old.hasProbabilityMultiplier && old.sourcePresetDefName == "") continue;
            var retained = ActionTuningRecord.Clone(old);
            retained.ClearBehaviorFields(); retained.hasScope = false; retained.responses.Clear(); retained.responseParameters.Clear();
            candidate.Add(retained);
        }
        candidate.Add(row);
        try
        {
            actionTuning = candidate;
            if (persistCandidate()) return true;
            failure = "PlayerOverrideSaveFailed";
        }
        catch (Exception error) { failure = error.GetType().Name + ":" + error.Message; }
        actionTuning = previous;
        return false;
    }

    private static BehaviorValues ProjectBehaviorPlayerValues(ActionTuningRecord record)
    {
        // Old probability/interval multipliers are not converted into new chance or hard ticks.
        var player = new BehaviorValues
        {
            Scope = record.hasScope ? record.scope : null,
            Enabled = record.hasEnabled ? record.enabled : record.hasScope ? record.scope != SqueakActionScope.Disabled : null,
            HardIntervalTicks = record.hasHardIntervalTicks ? record.hardIntervalTicks : null,
            TalkingEnabled = record.hasTalkingEnabled ? record.talkingEnabled : null,
            Volume = record.hasVolume ? record.volume : null, Pitch = record.hasPitch ? record.pitch : null,
            Phase = record.hasEatPhase ? record.eatPhase : null,
            NutritionScope = record.hasNutritionScope ? record.nutritionScope : null,
            DrugScope = record.hasDrugScope ? record.drugScope : null,
            SelectRequirePlayerControlled = record.hasSelectRequirePlayerControlled ? record.selectRequirePlayerControlled : null,
            SelectRequireAwake = record.hasSelectRequireAwake ? record.selectRequireAwake : null,
            SelectRequireNotDowned = record.hasSelectRequireNotDowned ? record.selectRequireNotDowned : null
        };
        foreach (var response in record.responses)
        {
            if (response.hasEnabled)
            {
                switch (response.key)
                {
                    case "us.core/Age":
                        player.AgeEnabled = response.enabled;
                        break;
                    case "us.core/Mood":
                        player.MoodEnabled = response.enabled;
                        break;
                    default:
                        player.ResponseEnabled[response.key] = response.enabled;
                        break;
                }
            }
            if (response.hasParticipation)
            {
                player.Participation[response.key] = response.apply;
            }
        }
        foreach (var parameter in record.responseParameters)
        {
            player.ResponseParameters.Add(parameter.key, parameter.value);
        }
        return player;
    }

    public BehaviorValues ReadEatParameters(string race = "", string child = "")
        => CaptureBehaviorConfiguration().Resolve(new BehaviorDomain("Pawn", "", race == "" ? "__PawnProfile" : race, child),
            "us.core/Eat", new BehaviorValues { Phase = EatPhase.WholeProcess, NutritionScope = EatNutritionScope.Any, DrugScope = EatDrugScope.Any },
            UsBehaviorDirectoryForSettings(), _ => true).Read();

    // Supplied by the platform partial; harnesses may keep the immutable empty author directory.
    private BehaviorSourceDirectory UsBehaviorDirectoryForSettings()
    {
        var directory = new BehaviorSourceDirectory(Array.Empty<BehaviorSource>());
        PopulateBehaviorDirectory(ref directory);
        return directory;
    }
    partial void PopulateBehaviorDirectory(ref BehaviorSourceDirectory directory);

    internal void SetEatParameter(string key, int? value, string race = "", string child = "")
    {
        if (child != "" && race == "") throw new ArgumentException("Child requires parent");
        Type type = key == "phase" ? typeof(EatPhase) : key == "nutritionScope" ? typeof(EatNutritionScope)
            : key == "drugScope" ? typeof(EatDrugScope) : throw new ArgumentException("Unknown Eat parameter", nameof(key));
        if (value.HasValue && !Enum.IsDefined(type, value.Value)) throw new ArgumentOutOfRangeException(nameof(value));
        bool Matches(ActionTuningRecord x) => x.serviceFamily == "Pawn" && Kernel.ActionKey.DefinitionId(x.actionKey) == "us.core/Eat"
            && x.raceDefName == race && x.xenotypeDefName == child;
        var row = actionTuning.LastOrDefault(Matches);
        if (row == null) { row = new ActionTuningRecord { actionKey = "us.core/Eat", raceDefName = race, xenotypeDefName = child }; actionTuning.Add(row); }
        foreach (var previous in actionTuning.Where(x => !ReferenceEquals(x, row) && Matches(x)))
        {
            if (key == "phase") previous.hasEatPhase = false;
            if (key == "nutritionScope") previous.hasNutritionScope = false;
            if (key == "drugScope") previous.hasDrugScope = false;
        }
        switch (key)
        {
            case "phase":
                if (value.HasValue && !Enum.IsDefined(typeof(EatPhase), value.Value)) throw new ArgumentOutOfRangeException(nameof(value));
                row.hasEatPhase = value.HasValue; row.eatPhase = (EatPhase)(value ?? 0); break;
            case "nutritionScope":
                if (value.HasValue && !Enum.IsDefined(typeof(EatNutritionScope), value.Value)) throw new ArgumentOutOfRangeException(nameof(value));
                row.hasNutritionScope = value.HasValue; row.nutritionScope = (EatNutritionScope)(value ?? 0); break;
            case "drugScope":
                if (value.HasValue && !Enum.IsDefined(typeof(EatDrugScope), value.Value)) throw new ArgumentOutOfRangeException(nameof(value));
                row.hasDrugScope = value.HasValue; row.drugScope = (EatDrugScope)(value ?? 0); break;
            default: throw new ArgumentException("Unknown Eat parameter", nameof(key));
        }
        QueuePersistence();
    }
}
