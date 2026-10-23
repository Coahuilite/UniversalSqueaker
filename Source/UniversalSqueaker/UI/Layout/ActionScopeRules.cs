using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// Pure classification rules for the built-in action scope editor (SA1.2, SR-parity round).
///
/// Two UI groups whose MEMBERSHIP is SR's fixed semantic split - who can START the action - and is
/// deliberately NOT derived from <c>SupportedScopes</c> any more. SR's own list puts Select in the
/// player group (it carries no command scope; the selection itself is the player act) and Work in the
/// system group (the job is the default path, the forced-work command is an option). Deriving the
/// group from scope support would move exactly those two rows, so the table is spelled out.
///
/// Classification is presentation: the typed scope values, trigger eligibility, routing and saving
/// are untouched by anything in this file (SA1.2 ruling). The order lists are the draw order; the
/// per-action scope keys are captions only, and an action with no SR caption falls back to the
/// generic one instead of shipping an untranslated key.
/// </summary>
public enum ActionScopeGroup
{
    /// <summary>SR "玩家可主动触发" / "Player-triggered actions".</summary>
    PlayerTriggered,

    /// <summary>SR "系统或事件触发" / "System or event-triggered actions".</summary>
    SystemOrEvent
}

public static class ActionScopeRules
{
    /// <summary>The player group in SR's display order. Draft/Undraft lead because the widget's
    /// "Draft / Undraft" sub-heading (SR's 征召 / 取消征召) sits above that pair.</summary>
    public static readonly IReadOnlyList<string> PlayerGroupOrder = new[]
    {
        CoreActions.Draft, CoreActions.Undraft, CoreActions.Attack, CoreActions.Equip, CoreActions.Select,
    };

    /// <summary>The ten official system/event rows in SR's display order.</summary>
    public static readonly IReadOnlyList<string> SystemGroupOrder = new[]
    {
        CoreActions.Call, CoreActions.Eat, CoreActions.Sleep, CoreActions.Wounded, CoreActions.Move,
        CoreActions.Work, CoreActions.Social, CoreActions.Joy, CoreActions.Death, CoreActions.MentalBreak,
    };

    private static readonly Dictionary<string, ActionScopeGroup> membership = BuildMembership();

    /// <summary>
    /// SR's per-action "any occurrence" captions: the action has one. The caption itself lives in the
    /// Keyed tables (US.Tuning.Scope.&lt;Action&gt;.Any); this set is the existence test, so a missing
    /// translation row cannot be hidden behind a present lookup.
    /// </summary>
    private static readonly HashSet<string> PerActionAny = new()
    {
        CoreActions.Call, CoreActions.Eat, CoreActions.Sleep, CoreActions.Wounded, CoreActions.Select,
        CoreActions.Move, CoreActions.Social, CoreActions.Joy, CoreActions.Death, CoreActions.Attack,
        CoreActions.Work, CoreActions.MentalBreak,
    };

    /// <summary>SR's per-action "player command" captions (US.Tuning.Scope.&lt;Action&gt;.Command).</summary>
    private static readonly HashSet<string> PerActionCommand = new()
    {
        CoreActions.Draft, CoreActions.Undraft, CoreActions.Attack, CoreActions.Work, CoreActions.Equip,
    };

    public static ActionScopeGroup GroupFor(string action)
    {
        return membership.TryGetValue(action, out ActionScopeGroup group) ? group : ActionScopeGroup.SystemOrEvent;
    }


    /// <summary>
    /// Display-text key of a scope for one action. Auto and Off are US's own semantics and keep the
    /// generic keys; Any/Command read the SR per-action caption when the tables carry one and fall back
    /// to the generic key otherwise.
    /// </summary>
    public static string ScopeLabelKey(string action, BehaviorScope scope)
    {
        return scope switch
        {
            BehaviorScope.AnyOccurrence when PerActionAny.Contains(action)
                => ActionPresentations.Get(action).DisplayKey.Replace("US.Action.", "US.Tuning.Scope.") + ".Any",
            BehaviorScope.ActiveCommand when PerActionCommand.Contains(action)
                => ActionPresentations.Get(action).DisplayKey.Replace("US.Action.", "US.Tuning.Scope.") + ".Command",
            BehaviorScope.AnyOccurrence => "US.Tuning.Scope.Any",
            BehaviorScope.ActiveCommand => "US.Tuning.Scope.Command",
            _ => "US.Tuning.Scope.Off",
        };
    }

    private static Dictionary<string, ActionScopeGroup> BuildMembership()
    {
        var map = new Dictionary<string, ActionScopeGroup>();
        for (int i = 0; i < PlayerGroupOrder.Count; i++) map[PlayerGroupOrder[i]] = ActionScopeGroup.PlayerTriggered;
        for (int i = 0; i < SystemGroupOrder.Count; i++) map[SystemGroupOrder[i]] = ActionScopeGroup.SystemOrEvent;
        return map;
    }
}
