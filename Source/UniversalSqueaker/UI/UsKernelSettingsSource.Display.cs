using System;
using System.Globalization;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public sealed partial class UsKernelSettingsSource
{
    private UiRegionAttachment? behaviorDisplayAttachment;
    internal void SetBehaviorDisplayAttachment(UiRegionAttachment attachment)
    {
        behaviorDisplayAttachment = attachment;
        host!.CreateContext(1).State("us/behavior-display", () => attachment, value => value.Dispose());
    }

    private ActionTuningRecord[] displayRows = Array.Empty<ActionTuningRecord>();
    private object? displayDefinitions, displayPolicy, displayCatalog, displayRoster, displayDirectory;
    private string displayLanguage = "", displayCulture = "", displayDecimal = "", displayNegative = "", displayPositive = "";
    private long displayConfiguration = -1, displayAudio = -1, displayInputsRevision, rowResultRevision;
    private string displayPolicyStatus = "";
    private string displaySourceXml = "", displayRace = "", displayChild = "";
    private int displayLayer = -1;
    private string[] displayFactKeys = Array.Empty<string>();
    private bool[] displayFacts = Array.Empty<bool>();

    // Called once at the region boundary, not once for every prepared field. Public mutable
    // settings remain legal: compare their complete raw rows (including nested members) without
    // folding, formatting, getter dispatch or allocating when unchanged.
    internal long ObserveBehaviorDisplayInputs()
    {
        var definitions = SqueakerRuntime.Definitions;
        string language = Verse.LanguageDatabase.activeLanguage?.folderName ?? "";
        var culture = CultureInfo.CurrentCulture;
        var numbers = culture.NumberFormat;
        bool definitionsChanged = !ReferenceEquals(displayDefinitions, definitions);
        bool changed = definitionsChanged
            || displayLanguage != language || displayCulture != culture.Name
            || displayDecimal != numbers.NumberDecimalSeparator || displayNegative != numbers.NegativeSign
            || displayPositive != numbers.PositiveSign
            || !ReferenceEquals(displayPolicy, SqueakerRuntime.CurrentPolicy)
            || !ReferenceEquals(displayCatalog, SqueakXenotypeCatalog.Current)
            || !ReferenceEquals(displayRoster, RaceDefinitionCatalog.Current)
            || !ReferenceEquals(displayDirectory, SqueakerRuntime.BehaviorSources)
            || displayAudio != SqueakerRuntime.AudioSnapshot || displayPolicyStatus != SqueakerRuntime.PolicyStatus
            || displayConfiguration != settings.CaptureBehaviorConfiguration().Id
            || displaySourceXml != settings.behaviorSourcesXml
            || displayLayer != state.TuningLayer || displayRace != state.TuningRaceDefName
            || displayChild != state.TuningXenotypeDefName;
        bool rowsChanged = displayRows.Length != settings.actionTuning.Count;
        if (!rowsChanged)
            for (int i = 0; i < displayRows.Length; i++)
                if (!settings.actionTuning[i].SameDisplayInputs(displayRows[i])) { rowsChanged = true; break; }
        string[] factKeys = definitionsChanged ? definitions.FactKeys.ToArray() : displayFactKeys;
        for (int i = 0; i < factKeys.Length; i++)
            if (definitionsChanged || displayFacts[i] != SqueakerRuntime.ImplementationAvailable(factKeys[i])) changed = true;
        if (changed || rowsChanged)
        {
            // Keep the original projection/normalization owner and all legacy invalidations.
            cachedView = null; effectiveFieldsView = null; effectiveFields.Clear();
        }
        if (!changed && !rowsChanged) return displayInputsRevision;
        BuildView();
        // Publish observation evidence only after all reads succeeded. A failed capture may retry.
        var facts = new bool[factKeys.Length];
        for (int i = 0; i < facts.Length; i++) facts[i] = SqueakerRuntime.ImplementationAvailable(factKeys[i]);
        var rows = settings.actionTuning.Select(ActionTuningRecord.Clone).ToArray();
        displayFactKeys = factKeys; displayFacts = facts;
        displayLanguage = language; displayCulture = culture.Name;
        displayDecimal = numbers.NumberDecimalSeparator; displayNegative = numbers.NegativeSign; displayPositive = numbers.PositiveSign;
        displayRows = rows;
        displayDefinitions = definitions; displayPolicy = SqueakerRuntime.CurrentPolicy;
        displayCatalog = SqueakXenotypeCatalog.Current; displayRoster = RaceDefinitionCatalog.Current;
        displayDirectory = SqueakerRuntime.BehaviorSources; displayAudio = SqueakerRuntime.AudioSnapshot;
        displayPolicyStatus = SqueakerRuntime.PolicyStatus;
        displayConfiguration = settings.CaptureBehaviorConfiguration().Id;
        displaySourceXml = settings.behaviorSourcesXml;
        displayLayer = state.TuningLayer; displayRace = state.TuningRaceDefName; displayChild = state.TuningXenotypeDefName;
        return ++displayInputsRevision;
    }

    internal long BehaviorRowResultRevision => rowResultRevision;
    private void SetRowResult(string action, string race, string child, string value)
    {
        string key = RowIdentity(action, race, child);
        if (rowResults.TryGetValue(key, out var prior) && prior == value) return;
        rowResults[key] = value;
        rowResultRevision++;
        // Includes a failed public parse before any Bump or configuration publication.
        // No Host is required by the business caller.
        // The source-owned result clock is sampled at every original legal sync, including
        // Repaint. Do not manufacture a business-view Bump for this independent display fact.
    }
}
