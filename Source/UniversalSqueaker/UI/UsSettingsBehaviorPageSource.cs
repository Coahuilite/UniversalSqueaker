using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

// Adapts the existing entry without imposing preview or Tools prerequisites on normal settings.
internal sealed class UsSettingsBehaviorPageSource : IUsBehaviorPageSource
{
    private readonly IUsKernelSettingsSource source;
    private VoicePacksViewState? observed;
    private UsBehaviorPageView? view;
    internal UsSettingsBehaviorPageSource(IUsKernelSettingsSource source)
    {
        this.source = source;
        Fields = source is UsKernelSettingsSource production ? new FieldsAdapter(production) : null;
    }
    internal UsKernelSettingsSource? Production => source as UsKernelSettingsSource;
    public VoicePacksPageState ViewState => source.ViewState;
    public IUsBehaviorFieldSource? Fields { get; }
    public UsBehaviorPageView ReadView()
    {
        var current = source.BuildView();
        if (!ReferenceEquals(current, observed))
        {
            view = new UsBehaviorPageView(current.ActionScopes, current.Roster);
            observed = current;
        }
        return view!;
    }
    public void SetTuningLayer(int layer) => source.SetTuningLayer(layer);
    public void SetTuningDomain(string race, string child) => source.SetTuningDomain(race, child);
    public void SetTuningSelectedAction(string action) => source.SetTuningSelectedAction(action);
    internal void SetActionExpansion(string action, Action invalidate)
    {
        if (source is UsKernelSettingsSource production) production.SetActionExpansion(action, invalidate);
        else { source.SetTuningSelectedAction(action); invalidate(); }
    }
    public void SetActionScope(string action, BehaviorScope? scope) => source.SetActionScope(action, scope);
    public bool CanResetTuningArea() => source.CanResetTuningArea();
    public SqueakResetOutcome ResetActionTuningRow(string action) => source.ResetActionTuningRow(action);
    public SqueakResetOutcome ResetActionTuningArea() => source.ResetActionTuningArea();
    public void ResetActionToPreset(string action) => source.ResetActionToPreset(action);
    public void EndAffectedEdit(Func<string, bool> affected) => source.EndAffectedEdit(affected);
    public bool CanConfirmAreaReset => true;
    public void ConfirmAreaReset(string title, string message, string confirm, Action apply)
        => UsConfirmWindow.Open(title, message, confirm, apply);
    public void OpenOutput() => UsKernelSettingsHost.NavigateBehaviorOutput(ViewState);
    public bool CanOpenOutput => true;

    private sealed class FieldsAdapter : IUsBehaviorFieldSource
    {
        private readonly UsKernelSettingsSource source;
        internal FieldsAdapter(UsKernelSettingsSource source) => this.source = source;
        public IReadOnlyList<string> ContributionFields(string action, string race, string child) => source.ContributionFields(action, race, child);
        public string ReadActionField(string field, string action, string race, string child) => source.ReadActionFieldForView(field, action, race, child);
        public bool TryCommitActionField(string field, string text, string action, string race, string child) => source.TryCommitActionField(field, text, action, race, child);
        public string EffectiveField(string field, string action, string race, string child, IUiTranslation translation) => source.EffectiveField(field, action, race, child, translation);
        public string EffectiveFieldSummary(string field, string action, string race, string child, IUiTranslation translation) => source.EffectiveFieldSummary(field, action, race, child, translation);
        public string RowResult(string action, string race, string child) => source.RowResult(action, race, child);
        public string SelectedSource => source.SelectedSource;
        public IReadOnlyList<UiChoice<string>> SourceChoices(IUiTranslation translation) => source.SourceChoices(translation);
        public void CommitSource(string sourceId) => source.CommitSource(sourceId);
        public string SourceStatus(IUiTranslation translation) => source.SourceStatus(translation);
        public string SourceStatusSummary(IUiTranslation translation) => source.SourceStatusSummary(translation);
        public bool TryResetActionToPreset(string action, out IReadOnlyCollection<string> affectedFields) => source.TryResetActionToPreset(action, out affectedFields);
    }
}
