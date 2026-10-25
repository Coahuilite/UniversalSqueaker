using System;
using System.Collections.Generic;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker.Authoring;

/// <summary>Canonical conditions, roles and scope capabilities; runtime policy supplies ordinary timing/defaults.</summary>
public static class BuiltInPawnContracts
{
    public static IReadOnlyList<string> CandidateOrder { get; } = Array.AsReadOnly(new[]
    {
        CoreActions.Death, CoreActions.Wounded, CoreActions.MentalBreak, CoreActions.Attack, CoreActions.Draft, CoreActions.Undraft, CoreActions.Equip,
        CoreActions.Select, CoreActions.Sleep, CoreActions.Eat, CoreActions.Social, CoreActions.Joy, CoreActions.Move, CoreActions.Work, CoreActions.Call
    });

    public static bool IsEvent(string key) => key != CoreActions.Call && key != CoreActions.Eat && key != CoreActions.Sleep
        && key != CoreActions.Social && key != CoreActions.Joy && key != CoreActions.Move && key != CoreActions.Work;

    public static string FactKey(string key) => key == CoreActions.Eat ? EatPolicy.Process : key + (IsEvent(key) ? "Event" : "State");

    // Canonical scope capabilities for the official contracts. Settings and UI query the
    // same definition; presentation labels cannot change a saved value or runtime default.
    public static ActionScopeSupport SupportedScopes(string key) => key switch
    {
        CoreActions.Draft or CoreActions.Undraft or CoreActions.Equip => ActionScopeSupport.ActiveCommand,
        CoreActions.Attack or CoreActions.Work => ActionScopeSupport.AnyOccurrence | ActionScopeSupport.ActiveCommand,
        _ when Kernel.BuiltInActionKeys.Contains(key) => ActionScopeSupport.AnyOccurrence,
        _ => throw new ArgumentException("NoOfficialScopeContract:" + key, nameof(key))
    };

    // Retained declaration/Dev default. Ordinary runtime inputs can supply another supported scope.
    public static BehaviorScope DefaultScope(string key) => key == CoreActions.Work
        || SupportedScopes(key) == ActionScopeSupport.ActiveCommand ? BehaviorScope.ActiveCommand : BehaviorScope.AnyOccurrence;

    public static BehaviorScope NormalizeScope(string key, BehaviorScope scope)
    {
        var supported = SupportedScopes(key);
        if (scope == BehaviorScope.Disabled) return scope;
        var required = scope == BehaviorScope.ActiveCommand ? ActionScopeSupport.ActiveCommand : ActionScopeSupport.AnyOccurrence;
        return (supported & required) != 0 ? scope : DefaultScope(key);
    }

    private static string Role(string key) => key switch
    {
        CoreActions.Select => "SelectedSubject", CoreActions.Wounded => "DamageRecipient", CoreActions.Death => "DyingSubject",
        CoreActions.Attack => "Attacker", CoreActions.Move => "SelfMover", CoreActions.Equip => "EquipmentWearer",
        CoreActions.Call => "IdlePawn", CoreActions.Eat => "ActualIngester", CoreActions.Sleep => "Sleeper",
        CoreActions.Social => "SocialParticipant", CoreActions.Joy => "JoyParticipant", CoreActions.Work => "Worker",
        CoreActions.Draft => "DraftedPawn", CoreActions.Undraft => "UndraftedPawn", CoreActions.MentalBreak => "BreakSubject",
        _ => "Subject"
    };

    public static IEnumerable<ActionPlan> Create(long interval, long woundedInterval, bool fullDevCandidate)
    {
        for (int i = 0; i < CandidateOrder.Count; i++)
        {
            string key = CandidateOrder[i];
            if (!fullDevCandidate && key != CoreActions.Work && key != CoreActions.Wounded && key != CoreActions.Call)
            {
                continue;
            }
            int priority = fullDevCandidate ? CandidateOrder.Count - i : key == CoreActions.Wounded ? 30 : key == CoreActions.Work ? 20 : 0;
            yield return new ActionPlan(key, priority, new Condition("ref", FactKey(key)),
                key == CoreActions.Wounded ? woundedInterval : interval, skipTalking: key == CoreActions.Death || key == CoreActions.Select,
                requiresVocal: key != CoreActions.Select, subjectRole: Role(key),
                scope: DefaultScope(key));
        }
    }
}
