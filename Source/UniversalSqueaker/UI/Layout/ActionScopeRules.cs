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
    public static readonly IReadOnlyList<SqueakAction> PlayerGroupOrder = new[]
    {
        SqueakAction.Draft, SqueakAction.Undraft, SqueakAction.Attack, SqueakAction.Equip, SqueakAction.Select,
    };

    /// <summary>The system/event group in SR's display order. US's optional Crying/Giggling join the
    /// tail here; they only ever appear under the eligibility gate that already decides the row
    /// exists, so this table adds no reachability of its own.</summary>
    public static readonly IReadOnlyList<SqueakAction> SystemGroupOrder = new[]
    {
        SqueakAction.Call, SqueakAction.Eat, SqueakAction.Sleep, SqueakAction.Wounded, SqueakAction.Move,
        SqueakAction.Work, SqueakAction.Social, SqueakAction.Joy, SqueakAction.Death, SqueakAction.MentalBreak,
        SqueakAction.Crying, SqueakAction.Giggling,
    };

    private static readonly Dictionary<SqueakAction, ActionScopeGroup> membership = BuildMembership();

    /// <summary>
    /// SR's per-action "any occurrence" captions: the action has one. The caption itself lives in the
    /// Keyed tables (US.Tuning.Scope.&lt;Action&gt;.Any); this set is the existence test, so a missing
    /// translation row cannot be hidden behind a present lookup.
    /// </summary>
    private static readonly HashSet<SqueakAction> PerActionAny = new()
    {
        SqueakAction.Call, SqueakAction.Eat, SqueakAction.Sleep, SqueakAction.Wounded, SqueakAction.Select,
        SqueakAction.Move, SqueakAction.Social, SqueakAction.Joy, SqueakAction.Death, SqueakAction.Attack,
        SqueakAction.Work, SqueakAction.MentalBreak,
    };

    /// <summary>SR's per-action "player command" captions (US.Tuning.Scope.&lt;Action&gt;.Command).</summary>
    private static readonly HashSet<SqueakAction> PerActionCommand = new()
    {
        SqueakAction.Draft, SqueakAction.Undraft, SqueakAction.Attack, SqueakAction.Work, SqueakAction.Equip,
    };

    public static ActionScopeGroup GroupFor(SqueakAction action)
    {
        return membership.TryGetValue(action, out ActionScopeGroup group) ? group : ActionScopeGroup.SystemOrEvent;
    }


    /// <summary>
    /// Display-text key of a scope for one action. Auto and Off are US's own semantics and keep the
    /// generic keys; Any/Command read the SR per-action caption when the tables carry one and fall back
    /// to the generic key otherwise (Crying/Giggling have no SR caption).
    /// </summary>
    public static string ScopeLabelKey(SqueakAction action, SqueakActionScope scope)
    {
        return scope switch
        {
            SqueakActionScope.AnyOccurrence when PerActionAny.Contains(action)
                => "US.Tuning.Scope." + action + ".Any",
            SqueakActionScope.ActiveCommand when PerActionCommand.Contains(action)
                => "US.Tuning.Scope." + action + ".Command",
            SqueakActionScope.AnyOccurrence => "US.Tuning.Scope.Any",
            SqueakActionScope.ActiveCommand => "US.Tuning.Scope.Command",
            _ => "US.Tuning.Scope.Off",
        };
    }

    /// <summary>The editor's exclusion rule, unchanged by SA1.2: the baby actions share the runtime
    /// eligibility rule and the default editor excludes them.</summary>
    public static bool IsHiddenByDefault(SqueakAction action) => !SqueakActionEligibility.IsEligible(action, false);


    private static Dictionary<SqueakAction, ActionScopeGroup> BuildMembership()
    {
        var map = new Dictionary<SqueakAction, ActionScopeGroup>();
        for (int i = 0; i < PlayerGroupOrder.Count; i++) map[PlayerGroupOrder[i]] = ActionScopeGroup.PlayerTriggered;
        for (int i = 0; i < SystemGroupOrder.Count; i++) map[SystemGroupOrder[i]] = ActionScopeGroup.SystemOrEvent;
        return map;
    }
}
