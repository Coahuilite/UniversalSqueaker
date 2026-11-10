using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public sealed partial class UsKernelSettingsSource
{
    public string RowDiagnostic { get; private set; } = "";
    public string EffectiveInterval(string action, string race, string child, bool interaction, IUiTranslation t)
    {
        var effective = SqueakerRuntime.Explain(settings.CaptureBehaviorConfiguration(), action, race, child);
        var values = effective.Read();
        string field = interaction ? "interactionIntervalSeconds" : "simulationIntervalTicks";
        string number = interaction ? (values.InteractionIntervalSeconds ?? 0).ToString("R", CultureInfo.InvariantCulture)
            : (values.SimulationIntervalTicks ?? 0).ToString(CultureInfo.InvariantCulture);
        return t.Translate(interaction ? "US.UI.Field.interaction-seconds" : "US.UI.Field.interval") + ": " + number
            + " · " + (effective.Origins.TryGetValue(field, out string origin) ? origin : "package:" + action)
            + (effective.Paused ? "\n" + effective.Failure : "");
    }
    public string ReadActionField(string field, string action, string race, string child)
    {
        var value = settings.ReadPlayerActionOverride(action, race, child);
        object? result = FieldValue(value, field);
        return result is IFormattable number ? number.ToString(null, CultureInfo.InvariantCulture) : result?.ToString() ?? "";
    }

    internal string ReadActionFieldForView(string field, string action, string race, string child)
    {
        // Only the production sheet shares its current presentation snapshot. Direct readers and
        // writes keep their original fresh-row contract, including without a Host or WindowStack.
        object? result = FieldValue(ReadEffectiveFields(action, race, child).Own, field);
        return result is IFormattable number ? number.ToString(null, CultureInfo.InvariantCulture) : result?.ToString() ?? "";
    }

    // A field commits one complete sparse row through the existing persist-first transaction.
    // It does not rebuild effective values or erase other fields/response contributions.
    public void CommitActionField(string field, string text, string action, string race, string child)
        => TryCommitActionField(field, text, action, race, child);
    public bool TryCommitActionField(string field, string text, string action, string race, string child)
    {
        if (action == "" || SqueakerMod.Instance == null) return false;
        var value = settings.ReadPlayerActionOverride(action, race, child);
        if (!UsBehaviorFieldValues.TryWrite(value, field, text, out bool invalid))
        {
            if (invalid) { RowDiagnostic = "US.UI.RowInvalid"; rowResults[RowIdentity(action, race, child)] = RowDiagnostic; }
            return false;
        }
        bool committed = SqueakerMod.Instance.TryCommitBehaviorOverride("Pawn", "", race, child,
            action, value, out string diagnostic);
        RowDiagnostic = committed ? diagnostic.StartsWith("Committed:", StringComparison.Ordinal)
            ? "US.UI.SourcePublishFailed" : "US.UI.SourceCommitted" : "US.UI.SourceRejected";
        RowDiagnostic += "\n" + action + " · " + (race == "" ? "Pawn profile" : race + (child == "" ? "" : "/" + child));
        if (diagnostic != "") RowDiagnostic += "; " + diagnostic;
        rowResults[RowIdentity(action, race, child)] = RowDiagnostic;
        cachedView = null;
        return committed;
    }
    private BehaviorDomain? SourceDomain => state.TuningLayer == 0 || state.TuningRaceDefName == "" ||
        (state.TuningLayer == 2 && state.TuningXenotypeDefName == "") ? null :
        new BehaviorDomain("Pawn", "", state.TuningRaceDefName, state.TuningLayer == 2 ? state.TuningXenotypeDefName : "");

    private const string RejectedSourceChoice = "\0rejected-behavior-source";
    private string? sourceXmlRead;
    private BehaviorDomain? sourceDomainRead;
    private BehaviorBinding sourceBindingRead = new();
    private string sourceReadFailure = "";
    private bool TryReadSource(out BehaviorBinding binding, out string failure)
    {
        var domain = SourceDomain;
        string xml = settings.behaviorSourcesXml;
        if (sourceXmlRead != xml || !Equals(sourceDomainRead, domain))
        {
            sourceXmlRead = xml; sourceDomainRead = domain;
            sourceBindingRead = new BehaviorBinding(); sourceReadFailure = "";
            if (domain != null)
                try { sourceBindingRead = BehaviorSourceState.FromXml(xml).Read(domain); }
                catch (Exception error) { sourceReadFailure = error.GetType().Name + ":" + error.Message; }
        }
        binding = sourceBindingRead; failure = sourceReadFailure;
        return failure == "";
    }
    public string SelectedSource
    {
        get
        {
            return !TryReadSource(out var binding, out _) || binding.RejectedTimingXml != ""
                ? RejectedSourceChoice : binding.SourceId;
        }
    }

    public IReadOnlyList<UiChoice<string>> SourceChoices(IUiTranslation t)
    {
        if (SourceDomain == null) return Array.Empty<UiChoice<string>>();
        var choices = new List<UiChoice<string>> { new(t.Translate("US.UI.SourceInherit"), "") };
        if (!TryReadSource(out var binding, out _) || binding.RejectedTimingXml != "")
            choices.Add(new UiChoice<string>(t.Translate("US.UI.SourceRejectedChoice")
                + (binding.SourceId == "" ? "" : " · " + binding.SourceId), RejectedSourceChoice));
        if (SourceDomain is BehaviorDomain domain)
            choices.AddRange(SqueakerRuntime.BehaviorSources.Candidates(domain).Select(x => new UiChoice<string>(x.Id, x.Id)));
        return choices;
    }

    public void CommitSource(string sourceId)
    {
        if (sourceId == RejectedSourceChoice) return; // Display-only, never a stored source identity.
        if (SourceDomain is not BehaviorDomain domain || SqueakerMod.Instance == null) return;
        bool committed = SqueakerMod.Instance.TryCommitBehaviorSource(domain, sourceId == "" ? null : sourceId, out string diagnostic);
        state.SourceChangeDiagnostic = committed ? diagnostic.StartsWith("Committed:", StringComparison.Ordinal)
            ? "US.UI.SourcePublishFailed" : "US.UI.SourceCommitted" : "US.UI.SourceRejected";
        state.SourceChangeDiagnostic += "\n" + domain.Target + (domain.Child == "" ? "" : "/" + domain.Child);
        if (diagnostic != "") state.SourceChangeDiagnostic += "; " + diagnostic;
        cachedView = null;
    }

    public string SourceStatus(IUiTranslation t)
    {
        string note;
        if (SourceDomain is BehaviorDomain domain)
        {
            bool readable = TryReadSource(out var binding, out string failure);
            string intent = binding.Intent switch { BehaviorSourceIntent.Bound => "US.UI.SourceBound",
                BehaviorSourceIntent.Inherit => "US.UI.SourceInherit", _ => "US.UI.SourceUnconfigured" };
            note = !readable || binding.RejectedTimingXml != "" ? t.Translate("US.UI.SourceTimingRejected") + "\n"
                + (failure != "" ? failure : binding.RejectedTimingReason)
                : t.Translate("US.UI.SourceStatus") + ": " + t.Translate(intent)
                    + (binding.SourceId == "" ? "" : " · " + binding.SourceId)
                    + "\n" + t.Translate("US.B.LastGood") + ": " + (binding.Snapshot == null ? t.Translate("US.B.NotSupplied") : binding.Snapshot.Id)
                    + " · " + SqueakerRuntime.BehaviorSources.Candidates(domain).Length + " " + t.Translate("US.B.SourceCandidates");
        }
        else note = t.Translate("US.UI.SourceChooseDomain");
        if (state.SourceChangeDiagnostic != "")
        {
            string[] parts = state.SourceChangeDiagnostic.Split(new[] { '\n' }, 2);
            note += "\n" + t.Translate(parts[0]);
            if (parts.Length > 1 && parts[1] != "") note += "\n" + parts[1];
        }
        return note;
    }

    internal string SourceStatusSummary(IUiTranslation t)
    {
        if (SourceDomain == null) return t.Translate("US.UI.SourceChooseDomain");
        if (!TryReadSource(out var binding, out _) || binding.RejectedTimingXml != ""
            || state.SourceChangeDiagnostic.StartsWith("US.UI.SourceRejected", StringComparison.Ordinal)
            || state.SourceChangeDiagnostic.StartsWith("US.UI.SourcePublishFailed", StringComparison.Ordinal)) return SourceStatus(t);
        string intent = binding.Intent switch { BehaviorSourceIntent.Bound => "US.UI.SourceBound",
            BehaviorSourceIntent.Inherit => "US.UI.SourceInherit", _ => "US.UI.SourceUnconfigured" };
        return t.Translate("US.UI.SourceStatus") + ": " + t.Translate(intent) + " · " + t.Translate("US.B.LastGood")
            + ": " + t.Translate(binding.Snapshot == null ? "US.B.NotSupplied" : "US.B.SourceSnapshotAvailable");
    }

    internal void SetContentLayout(bool wide)
    {
        if (state.WideLayout == wide) return;
        state.WideLayout = wide;
        cachedView = null;
    }
}
