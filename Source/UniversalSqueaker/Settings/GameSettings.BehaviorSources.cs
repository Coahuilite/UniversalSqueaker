using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalSqueaker;

public partial class GameSettings
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
        PublishBehaviorConfiguration();
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

    /// <summary>Stage source intent and adopt only if the caller's acceptCandidate returns true.
    /// True means adoption, not disk commit. The Mod caller supplies persistence; pure callers may not.</summary>
    public bool TryChangeBehaviorSource(BehaviorDomain domain, string? sourceId, BehaviorSourceDirectory directory,
        Func<string, bool> acceptCandidate, out string diagnostic)
    {
        diagnostic = "";
        if (IsConfigurationUnreadable)
        {
            diagnostic = "ConfigurationUnreadable";
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
                diagnostic = "InvalidBehaviorSource";
                return false;
            }
            return TryAdoptBehaviorState(candidate, acceptCandidate, out diagnostic);
        }
        catch (Exception error)
        {
            diagnostic = error.GetType().Name + ":" + error.Message;
            return false;
        }
    }

    public bool TryRefreshBehaviorSources(BehaviorSourceDirectory directory, Func<string, bool> acceptCandidate, out string failure)
    {
        failure = "";
        if (IsConfigurationUnreadable)
        {
            failure = "ConfigurationUnreadable";
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
            return TryAdoptBehaviorState(candidate, acceptCandidate, out failure);
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ":" + error.Message;
            return false;
        }
    }

    private bool TryAdoptBehaviorState(BehaviorSourceState candidate, Func<string, bool> acceptCandidate, out string failure)
    {
        failure = "";
        string xml = candidate.ToXml();
        if (BehaviorSourceState.FromXml(xml).ToXml() != xml)
        {
            throw new InvalidOperationException("Unstable source serialization");
        }
        if (!acceptCandidate(xml))
        {
            failure = "BehaviorSourceCandidateRejected";
            return false;
        }
        behaviorSourcesXml = xml;
        publishedBehavior = null;
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

    private BehaviorConfiguration? publishedBehavior;
    private long behaviorRevision;
    public BehaviorConfiguration CaptureBehaviorConfiguration() => publishedBehavior ??= BuildBehaviorConfiguration();
    internal void PublishBehaviorConfiguration() => publishedBehavior = BuildBehaviorConfiguration();
    private BehaviorConfiguration BuildBehaviorConfiguration()
    {
        return new BehaviorConfiguration(behaviorSourcesXml, actionTuning.Select(CapturePlayerRecord),
            !IsConfigurationUnreadable && voicePackMode != SqueakVoicePackMode.Disabled, activity, globalVolumeFactor,
            SqueakKernelAdapter.ToSelectionMode(voicePackMode), CaptureAudioSelection(), AllowEasterEggSounds, scaleFrequencyWithTalking, ++behaviorRevision);
    }
    private IEnumerable<Kernel.AudioPackSelection> CaptureAudioSelection()
    {
        foreach (var row in voicePackSelections)
        {
            if (row.raceDefName == "" || row.scope == SqueakVoicePackScope.Unspecified
                || row.scope == SqueakVoicePackScope.Xenotype && row.xenotypeDefName == "") continue;
            yield return new Kernel.AudioPackSelection(new Kernel.AudioDomain(new Kernel.RaceKey(row.raceDefName),
                row.scope == SqueakVoicePackScope.Xenotype ? new Kernel.XenotypeKey(row.xenotypeDefName) : null), row.enabledPackKeys);
        }
    }
    private static BehaviorPlayerOverride CapturePlayerRecord(ActionTuningRecord record)
    {
        bool valid = record.IsValidLayer(out _);
        BehaviorValues values;
        try { values = ProjectBehaviorPlayerValues(record); }
        catch (ArgumentException) { values = new BehaviorValues(); valid = false; }
        return new BehaviorPlayerOverride(record.serviceFamily, record.targetNamespace,
            record.serviceFamily == "Pawn" ? record.raceDefName : record.objectTarget,
            record.xenotypeDefName, record.actionKey, values, valid);
    }

    /// <summary>Replace one complete sparse row only when acceptCandidate accepts it.
    /// True means replacement, not physical commit: the caller may persist first or queue an ordinary
    /// edit. This method neither writes a file, queues saving nor publishes the native runtime.</summary>
    public bool TryReplaceBehaviorOverride(string family, string targetNamespace, string target, string child, string action,
        BehaviorValues values, Func<bool> acceptCandidate, out string diagnostic)
    {
        diagnostic = "";
        if (IsConfigurationUnreadable) { diagnostic = "ConfigurationUnreadable"; return false; }
        if (values.UnsupportedTiming != "") { diagnostic = values.UnsupportedTiming; return false; }
        string timing = Responses.BehaviorContract.InvalidTimingUnit(action, values);
        if (timing != "") { diagnostic = timing; return false; }
        if (!Authoring.AuthorXml.ActionId(action) || target != target.Trim() || child != child.Trim()
            || !new ActionTuningRecord { serviceFamily = family, targetNamespace = targetNamespace, raceDefName = family == "Pawn" ? target : "", objectTarget = family == "NonPawn" ? target : "", xenotypeDefName = child }.IsValidLayer(out _) || !values.Valid || !values.ApplicableTo(family))
        { diagnostic = "InvalidPlayerOverride"; return false; }
        var row = CreatePlayerRecord(family, targetNamespace, target, child, action, values);
        var previous = actionTuning;
        var candidate = previous.Where(x => !(x.serviceFamily == family && x.targetNamespace == targetNamespace
            && (family == "Pawn" ? x.raceDefName : x.objectTarget) == target && x.xenotypeDefName == child
            && x.actionKey == row.actionKey)).Select(ActionTuningRecord.Clone).ToList();
        row.sourcePresetDefName = previous.LastOrDefault(x => x.serviceFamily == family && x.targetNamespace == targetNamespace
            && (family == "Pawn" ? x.raceDefName : x.objectTarget) == target && x.xenotypeDefName == child && x.actionKey == action)?.sourcePresetDefName ?? "";
        candidate.Add(row);
        try
        {
            actionTuning = candidate;
            if (acceptCandidate()) { publishedBehavior = null; return true; }
            diagnostic = "PlayerOverrideCandidateRejected";
        }
        catch (Exception error) { diagnostic = error.GetType().Name + ":" + error.Message; }
        actionTuning = previous;
        return false;
    }

    private static ActionTuningRecord CreatePlayerRecord(string family, string targetNamespace, string target, string child,
        string action, BehaviorValues values)
    {
        var row = new ActionTuningRecord { actionKey = action, serviceFamily = family,
            targetNamespace = targetNamespace, raceDefName = family == "Pawn" ? target : "", objectTarget = family == "NonPawn" ? target : "", xenotypeDefName = child };
        row.hasEnabled = values.Enabled.HasValue; row.enabled = values.Enabled ?? true;
        row.hasScope = values.Scope.HasValue; row.scope = values.Scope ?? BehaviorScope.AnyOccurrence;
        row.hasSimulationIntervalTicks = values.SimulationIntervalTicks.HasValue; row.simulationIntervalTicks = values.SimulationIntervalTicks ?? 0;
        row.hasInteractionIntervalSeconds = values.InteractionIntervalSeconds.HasValue; row.interactionIntervalSeconds = values.InteractionIntervalSeconds ?? 0;
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
        return row;
    }

    internal void ReplacePlayerValues(string action, string race, string child, BehaviorValues values, string preset)
    {
        if (!Authoring.AuthorXml.ActionId(action) || !IsValidTuningDomain(race, child) || !values.Valid || !values.ApplicableTo("Pawn"))
            throw new ArgumentException("InvalidPlayerTemplateTarget");
        var row = CreatePlayerRecord("Pawn", "", race, child, action, values);
        row.sourcePresetDefName = preset;
        actionTuning.RemoveAll(x => SameActionTuningIdentity(x, action, race, child));
        actionTuning.Add(row);
    }
    private static BehaviorValues ProjectBehaviorPlayerValues(ActionTuningRecord record)
    {
        // Old probability/interval multipliers are not converted into new chance or hard ticks.
        var player = new BehaviorValues
        {
            UnsupportedTiming = record.HasUnsupportedTiming ? "UnsupportedBehaviorTiming:" + record.actionKey
                + ":hardIntervalTicks; explicitly replace/reset this row using simulationIntervalTicks or interactionIntervalSeconds" : "",
            Scope = record.hasScope ? record.scope : null,
            Enabled = record.hasEnabled ? record.enabled : null,
            SimulationIntervalTicks = record.hasSimulationIntervalTicks ? record.simulationIntervalTicks : null,
            InteractionIntervalSeconds = record.hasInteractionIntervalSeconds ? record.interactionIntervalSeconds : null,
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

    public BehaviorValues ReadPlayerActionOverride(string action, string race, string child)
    {
        var values = new BehaviorValues();
        foreach (var row in actionTuning.Where(x => x.serviceFamily == "Pawn" && x.actionKey == action && x.raceDefName == race && x.xenotypeDefName == child))
            values.Overlay(ProjectBehaviorPlayerValues(row));
        return values;
    }

    public BehaviorValues ReadEatParameters(string race = "", string child = "")
        => ReadParameters(CoreActions.Eat, new BehaviorValues { Phase = EatPhase.WholeProcess, NutritionScope = EatNutritionScope.Any, DrugScope = EatDrugScope.Any }, race, child);

    private BehaviorValues ReadParameters(string action, BehaviorValues defaults, string race, string child)
    {
        var configuration = CaptureBehaviorConfiguration();
        var directory = UsBehaviorDirectoryForSettings();
        return (race == "" ? configuration.ResolveProfile("Pawn", "", action, defaults, directory, SqueakerRuntime.ImplementationAvailable)
            : configuration.Resolve(new BehaviorDomain("Pawn", "", race, child), action, defaults, directory, SqueakerRuntime.ImplementationAvailable)).Read();
    }

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
        bool Matches(ActionTuningRecord x) => x.serviceFamily == "Pawn" && x.actionKey == "us.core/Eat"
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
        PublishBehaviorConfiguration();
        QueuePersistence();
    }
}
