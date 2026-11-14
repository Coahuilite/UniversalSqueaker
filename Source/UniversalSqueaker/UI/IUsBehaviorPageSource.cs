using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>The actual behavior sheet's data boundary. Development/preview API: public-unstable.
/// A preview supplies detached author data here, never a substitute Pawn, Map or settings singleton.
/// Keep ReadView's identity while its inputs are unchanged, as the ordinary source does.</summary>
public interface IUsBehaviorPageSource
{
    VoicePacksPageState ViewState { get; }
    UsBehaviorPageView ReadView();
    IUsBehaviorFieldSource? Fields { get; }
    void SetTuningLayer(int layer);
    void SetTuningDomain(string race, string child);
    void SetTuningSelectedAction(string action);
    void SetActionScope(string action, BehaviorScope? scope);
    bool CanResetTuningArea();
    SqueakResetOutcome ResetActionTuningRow(string action);
    SqueakResetOutcome ResetActionTuningArea();
    void ResetActionToPreset(string action);
    void EndAffectedEdit(Func<string, bool> affected);
    bool CanConfirmAreaReset { get; }
    void ConfirmAreaReset(string title, string message, string confirm, Action apply);
    bool CanOpenOutput { get; }
    void OpenOutput();
}

/// <summary>Only the two existing projections read by the behavior composition.</summary>
public sealed class UsBehaviorPageView
{
    public UsBehaviorPageView(IReadOnlyList<ActionScopeRowView> actions, IReadOnlyList<UsRosterTarget> roster)
    {
        ActionScopes = actions ?? throw new ArgumentNullException(nameof(actions));
        Roster = roster ?? throw new ArgumentNullException(nameof(roster));
    }
    public IReadOnlyList<ActionScopeRowView> ActionScopes { get; }
    public IReadOnlyList<UsRosterTarget> Roster { get; }
}

/// <summary>Optional sparse-field capability; commits and their results remain with the supplied
/// source. A handled editor request does not imply physical persistence or runtime publication.</summary>
public interface IUsBehaviorFieldSource
{
    IReadOnlyList<string> ContributionFields(string action, string race, string child);
    string ReadActionField(string field, string action, string race, string child);
    bool TryCommitActionField(string field, string text, string action, string race, string child);
    string EffectiveField(string field, string action, string race, string child, IUiTranslation translation);
    string EffectiveFieldSummary(string field, string action, string race, string child, IUiTranslation translation);
    string RowResult(string action, string race, string child);
    string SelectedSource { get; }
    IReadOnlyList<UiChoice<string>> SourceChoices(IUiTranslation translation);
    void CommitSource(string sourceId);
    string SourceStatus(IUiTranslation translation);
    string SourceStatusSummary(IUiTranslation translation);
    bool TryResetActionToPreset(string action, out IReadOnlyCollection<string> affectedFields);
}
