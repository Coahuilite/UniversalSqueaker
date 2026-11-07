using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker.UI;

/// <summary>Instance-local author workspace over the production Core configuration fold.
/// No GameSettings, singleton, native world, filesystem or runtime publication. Development API.
/// Definitions and roster are explicit author inputs, not observations of the running game.</summary>
public sealed class UsOfflineBehaviorSource : IUsBehaviorPageSource, IUsBehaviorFieldSource
{
    public const string Identity = "us/offline-core-v1; memory-only; no runtime publication";
    private readonly DefinitionCatalog definitions;
    private readonly ActionPlan[] plans;
    private readonly Dictionary<string, ActionPlan> actions;
    private readonly UsResponsePolicy policy = UsResponsePolicy.CreateOrdinary();
    private readonly IUiTranslation translation;
    private readonly IReadOnlyList<UsRosterTarget> roster;
    private readonly Action<string, string, string, Action>? confirm;
    private readonly Action? openOutput;
    private readonly BehaviorValues emptyRow = new();
    private readonly Dictionary<(string Action, string Race, string Child), BehaviorValues> rows = new();
    private readonly Dictionary<(string Action, string Race, string Child), string> results = new();
    private readonly Dictionary<(string Action, string Race, string Child), (EffectiveBehavior State, BehaviorValues Values)> effective = new();
    private BehaviorSourceState sources;
    private BehaviorConfiguration configuration;
    private UsBehaviorPageView? view;
    private UiHost? host;
    private long revision;

    public UsOfflineBehaviorSource(DefinitionCatalog definitions, IUiTranslation translation,
        IReadOnlyList<UsRosterTarget>? roster = null, string sourcesXml = "",
        IEnumerable<BehaviorPlayerOverride>? players = null,
        Action<string, string, string, Action>? confirm = null, Action? openOutput = null)
    {
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        plans = definitions.Actions;
        actions = plans.ToDictionary(plan => plan.Key, StringComparer.Ordinal);
        this.translation = translation ?? throw new ArgumentNullException(nameof(translation));
        this.roster = Array.AsReadOnly((roster ?? Array.Empty<UsRosterTarget>()).ToArray());
        this.confirm = confirm; this.openOutput = openOutput;
        sources = BehaviorSourceState.FromXml(sourcesXml);
        foreach (var row in players ?? Array.Empty<BehaviorPlayerOverride>())
        {
            if (row.Family != "Pawn" || row.Namespace != "" || !row.ValidScope || !row.Read().Valid)
                throw new ArgumentException("Offline behavior inputs require valid Pawn configuration rows.", nameof(players));
            rows[(row.Action, row.Target, row.Child)] = row.Read();
        }
        configuration = Capture();
    }
    public VoicePacksPageState ViewState { get; } = new() { Page = "Behavior", ActiveTab = "Tuning", ActiveSectionKey = "scope-tree" };
    public IUsBehaviorFieldSource Fields => this;
    private string Race => ViewState.TuningLayer == 0 ? "" : ViewState.TuningRaceDefName;
    private string Child => ViewState.TuningLayer == 2 ? ViewState.TuningXenotypeDefName : "";
    private BehaviorDomain? Domain => Race == "" ? null : new BehaviorDomain("Pawn", "", Race, Child);
    private bool Known(string action) => actions.ContainsKey(action);
    private bool Valid(string race, string child) => race == "" ? child == "" : roster.Any(r => r.Race == race && r.Xeno == child && r.Editable);
    private BehaviorValues Own(string action, string race, string child)
        => rows.TryGetValue((action, race, child), out var row) ? row : emptyRow;
    private BehaviorConfiguration Capture() => new(sources.ToXml(), rows.Select(row =>
        new BehaviorPlayerOverride("Pawn", "", row.Key.Race, row.Key.Child, row.Key.Action, row.Value)),
        true, 1, 1, revision: revision);
    private void Changed()
    {
        revision++; configuration = Capture(); effective.Clear(); view = null;
    }
    private (EffectiveBehavior State, BehaviorValues Values) Resolve(string action, string race, string child)
    {
        var key = (action, race, child);
        if (effective.TryGetValue(key, out var cached)) return cached;
        var plan = actions[action];
        var defaults = BehaviorContract.Defaults(plan, policy);
        var result = race == "" ? configuration.ResolveProfile("Pawn", "", action, defaults, definitions.BehaviorSources,
            definitions.HasFact, plan.Condition.RequiredFacts()) : configuration.Resolve(new BehaviorDomain("Pawn", "", race, child),
            action, defaults, definitions.BehaviorSources, definitions.HasFact, plan.Condition.RequiredFacts());
        var values = result.Read();
        string invalid = BehaviorContract.InvalidParameters(plan, values);
        if (!result.Paused && invalid != "") result = new EffectiveBehavior(values, true, invalid,
            result.Origins.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal), result.Dependencies);
        return effective[key] = (result, values);
    }
    public UsBehaviorPageView ReadView() => view ??= new UsBehaviorPageView(plans.Select(plan => {
        var resolved = Resolve(plan.Key, Race, Child);
        var value = resolved.Values;
        var own = Own(plan.Key, Race, Child);
        return new ActionScopeRowView(plan.Key, Kernel.BuiltInActionKeys.Contains(plan.Key)
            ? translation.Translate(ActionPresentations.Get(plan.Key).DisplayKey) : plan.Key,
            ActionScopeRules.GroupFor(plan.Key), own.Scope ?? value.Scope ?? plan.Scope, plan.Key, own.Scope.HasValue,
            value.Enabled == false ? BehaviorScope.Disabled : value.Scope ?? plan.Scope,
            explanation: Identity + "\n" + string.Join("; ", resolved.State.Origins.Select(x => x.Key + "=" + x.Value))
                + (resolved.State.Paused ? "\n" + resolved.State.Failure : ""));
    }).ToArray(), roster);

    public void SetTuningLayer(int layer)
    {
        if (layer < 0 || layer > 2 || layer > 0 && !Valid(ViewState.TuningRaceDefName, layer == 2 ? ViewState.TuningXenotypeDefName : "")) return;
        VoicePacksPageModel.SetTuningLayer(ViewState, layer); view = null;
    }
    public void SetTuningDomain(string race, string child)
    {
        if (!Valid(race, child)) return;
        VoicePacksPageModel.SetTuningDomain(ViewState, race, child); view = null;
    }
    public void SetTuningSelectedAction(string action)
    { if (action == "" || Known(action)) VoicePacksPageModel.SetTuningSelectedAction(ViewState, action); }
    public void SetActionScope(string action, BehaviorScope? scope)
    {
        if (!Known(action) || !CanResetTuningArea()) return;
        if (scope.HasValue && Kernel.BuiltInActionKeys.Contains(action)
            && BuiltInPawnContracts.NormalizeScope(action, scope.Value) != scope.Value) return;
        var value = Own(action, Race, Child).Copy(); value.Scope = scope;
        if (!value.Valid) return;
        rows[(action, Race, Child)] = value; Changed();
    }
    public bool CanResetTuningArea() => ViewState.TuningLayer == 0 || Race != "" && (ViewState.TuningLayer != 2 || Child != "") && Valid(Race, Child);
    public SqueakResetOutcome ResetActionTuningRow(string action)
    {
        if (!Known(action) || !CanResetTuningArea()) return SqueakResetOutcome.Rejected;
        if (!rows.Remove((action, Race, Child))) return SqueakResetOutcome.NoChange;
        results.Remove((action, Race, Child)); Changed(); return SqueakResetOutcome.Applied;
    }
    public SqueakResetOutcome ResetActionTuningArea()
    {
        if (!CanResetTuningArea()) return SqueakResetOutcome.Rejected;
        var affected = rows.Keys.Where(key => key.Race == Race && key.Child == Child).ToArray();
        if (affected.Length == 0) return SqueakResetOutcome.NoChange;
        foreach (var key in affected) { rows.Remove(key); results.Remove(key); }
        Changed(); return SqueakResetOutcome.Applied;
    }
    // This author input declares no preset anchors; the real binding therefore keeps reset disabled.
    public void ResetActionToPreset(string action) { }
    public bool TryResetActionToPreset(string action, out IReadOnlyCollection<string> affectedFields)
    { affectedFields = Array.Empty<string>(); return false; }
    internal void Attach(UiHost value)
    {
        if (host != null) throw new InvalidOperationException("Use one isolated source per Host.");
        host = value;
    }
    public void EndAffectedEdit(Func<string, bool> affected) => UsKernelSettingsHost.EndAffectedEdit(host, affected);
    public bool CanConfirmAreaReset => confirm != null;
    public bool CanOpenOutput => openOutput != null;
    public void ConfirmAreaReset(string title, string message, string confirmLabel, Action apply)
    {
        if (confirm == null) throw new NotSupportedException("This Host has no confirmation wrapper.");
        confirm(title, message, confirmLabel, apply);
    }
    public void OpenOutput()
    {
        if (openOutput == null) throw new NotSupportedException("This Host contains only the behavior region.");
        openOutput();
    }
    public string ReadActionField(string field, string action, string race, string child)
        => UsBehaviorFieldValues.Format(UsBehaviorFieldValues.Read(Own(action, race, child), field));
    public bool TryCommitActionField(string field, string text, string action, string race, string child)
    {
        if (!Known(action) || !Valid(race, child)) return false;
        var candidate = Own(action, race, child).Copy();
        if (!UsBehaviorFieldValues.TryWrite(candidate, field, text))
        { results[(action, race, child)] = "US.UI.RowInvalid"; return false; }
        rows[(action, race, child)] = candidate;
        results[(action, race, child)] = Identity; Changed(); return true;
    }
    public string EffectiveField(string field, string action, string race, string child, IUiTranslation t)
        => FormatEffective(field, action, race, child, t, false);
    public string EffectiveFieldSummary(string field, string action, string race, string child, IUiTranslation t)
        => FormatEffective(field, action, race, child, t, true);
    private string FormatEffective(string field, string action, string race, string child, IUiTranslation t, bool compact)
    {
        if (!Known(action)) return t.Translate("US.B.Unknown");
        var read = Resolve(action, race, child);
        return UsBehaviorFieldValues.Effective(field, read.State, read.Values, Own(action, race, child), t, compact);
    }
    public string RowResult(string action, string race, string child)
        => results.TryGetValue((action, race, child), out var result) ? result : "";
    public IReadOnlyList<string> ContributionFields(string action, string race, string child)
    {
        if (!Known(action)) return Array.Empty<string>();
        return Array.AsReadOnly(Resolve(action, race, child).Values.PresentKeys().Concat(Own(action, race, child).PresentKeys())
            .Where(key => key.Contains(":"))
            .Concat(definitions.Responses.Where(r => r.Key != "us.core/Age" && r.Key != "us.core/Mood")
                .SelectMany(r => new[] { "response:" + r.Key, "participation:" + r.Key }))
            .Concat(new[] { "age.override", "age.overrideVolume", "mood.low", "mood.high", "mood.low.pitch", "mood.low.volume", "mood.neutral.pitch", "mood.neutral.volume", "mood.high.pitch", "mood.high.volume", "jitter.min", "jitter.max" }.Select(x => "parameter:" + x))
            .Concat(new[] { "participation:us.core/Age", "participation:us.core/Mood" })
            .Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }
    private const string RejectedSourceChoice = "\0rejected-behavior-source";
    public string SelectedSource => Domain is BehaviorDomain domain
        ? sources.Read(domain).RejectedTimingXml != "" ? RejectedSourceChoice : sources.Read(domain).SourceId : "";
    public IReadOnlyList<UiChoice<string>> SourceChoices(IUiTranslation t)
    {
        if (Domain is not BehaviorDomain domain) return Array.Empty<UiChoice<string>>();
        var choices = new List<UiChoice<string>> { new(t.Translate("US.UI.SourceInherit"), "") };
        var binding = sources.Read(domain);
        if (binding.RejectedTimingXml != "") choices.Add(new UiChoice<string>(t.Translate("US.UI.SourceRejectedChoice")
            + (binding.SourceId == "" ? "" : " · " + binding.SourceId), RejectedSourceChoice));
        choices.AddRange(definitions.BehaviorSources.Candidates(domain).Select(source => new UiChoice<string>(source.Id, source.Id)));
        return choices;
    }
    public void CommitSource(string sourceId)
    {
        if (sourceId == RejectedSourceChoice || Domain is not BehaviorDomain domain || !CanResetTuningArea()) return;
        var candidate = sources.Copy();
        if (sourceId == "") candidate.Forget(domain);
        else if (!candidate.Bind(domain, sourceId, definitions.BehaviorSources)) return;
        sources = candidate; ViewState.SourceChangeDiagnostic = Identity; Changed();
    }
    public string SourceStatusSummary(IUiTranslation t)
    {
        if (Domain is not BehaviorDomain domain) return t.Translate("US.UI.SourceChooseDomain");
        var binding = sources.Read(domain);
        if (binding.RejectedTimingXml != "") return t.Translate("US.UI.SourceTimingRejected") + "\n" + binding.RejectedTimingReason;
        return t.Translate("US.UI.SourceStatus") + ": " + t.Translate(binding.Intent == BehaviorSourceIntent.Bound
            ? "US.UI.SourceBound" : binding.Intent == BehaviorSourceIntent.Inherit ? "US.UI.SourceInherit" : "US.UI.SourceUnconfigured")
            + " · " + t.Translate("US.B.LastGood") + ": "
            + t.Translate(binding.Snapshot == null ? "US.B.NotSupplied" : "US.B.SourceSnapshotAvailable");
    }
    public string SourceStatus(IUiTranslation t) => SourceStatusSummary(t) + "\n" + Identity
        + (Domain is BehaviorDomain domain ? "\n" + domain.Target + "/" + domain.Child + " · " + sources.Read(domain).SourceId : "");
}
