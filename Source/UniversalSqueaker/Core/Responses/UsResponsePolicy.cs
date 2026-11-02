using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker;

/// <summary>Immutable runtime input. Ordinary defaults and explicit development inputs share one owner.</summary>
public sealed class UsResponsePolicy
{
    internal readonly StatePolicy State;
    internal readonly UsEquipQualification EquipQualification;
    internal readonly long InitialSpread;
    internal readonly double EventProbability, StateWaitFactor;
    internal readonly bool UsesNativeRhythm;
    private readonly UsDevelopmentPolicy? development;
    private readonly IReadOnlyDictionary<string, long> intervals;
    private readonly IReadOnlyDictionary<string, BehaviorScope> scopes;
    private readonly HashSet<string> sharedExemptions;

    /// <summary>Ordinary Release defaults, folded before explicit author and player values.</summary>
    internal static UsResponsePolicy CreateOrdinary() => new(new StatePolicy(216, 600, StatePhasePolicy.Ordinary), 600,
        UsEquipQualification.PlayerCoreEquip,
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [CoreActions.Eat] = 144, [CoreActions.Call] = 900, [CoreActions.Move] = 504,
            [CoreActions.Sleep] = 1200, [CoreActions.Social] = 504, [CoreActions.Joy] = 504,
            [CoreActions.Work] = 720, [CoreActions.Wounded] = 216, [CoreActions.Attack] = 216,
            [CoreActions.Equip] = 216, [CoreActions.Select] = 0, [CoreActions.Draft] = 0,
            [CoreActions.Undraft] = 0, [CoreActions.Death] = 0, [CoreActions.MentalBreak] = 0
        },
        new Dictionary<string, BehaviorScope>(StringComparer.Ordinal)
        {
            [CoreActions.Work] = BehaviorScope.AnyOccurrence,
            [CoreActions.Attack] = BehaviorScope.AnyOccurrence,
            [CoreActions.Equip] = BehaviorScope.ActiveCommand
        },
        new[] { CoreActions.Select, CoreActions.Draft, CoreActions.Undraft, CoreActions.Death, CoreActions.MentalBreak });

    internal UsResponsePolicy(UsDevelopmentPolicy development)
    {
        this.development = development;
        State = development.State;
        EquipQualification = development.EquipQualification;
        InitialSpread = development.InitialSpread;
        EventProbability = development.EventProbability;
        StateWaitFactor = development.StateWaitFactor;
        intervals = new Dictionary<string, long>();
        scopes = new Dictionary<string, BehaviorScope>();
        sharedExemptions = new HashSet<string>(StringComparer.Ordinal);
    }

    internal UsResponsePolicy(StatePolicy state, long initialSpread, UsEquipQualification equip,
        IReadOnlyDictionary<string, long> intervals, IReadOnlyDictionary<string, BehaviorScope> scopes,
        IEnumerable<string> sharedExemptions)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        if (initialSpread < 0 || !Enum.IsDefined(typeof(UsEquipQualification), equip))
            throw new ArgumentException("Invalid ordinary runtime inputs");
        foreach (string key in BuiltInPawnContracts.CandidateOrder)
            if (!intervals.TryGetValue(key, out long interval) || interval < 0)
                throw new ArgumentException("OrdinaryActionIntervalRequired:" + key, nameof(intervals));
        InitialSpread = initialSpread;
        EquipQualification = equip;
        this.intervals = intervals.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        this.scopes = scopes.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        this.sharedExemptions = new HashSet<string>(sharedExemptions, StringComparer.Ordinal);
        EventProbability = StateWaitFactor = 1;
        UsesNativeRhythm = true;
    }

    internal BehaviorValues Defaults(ActionPlan action) => Defaults(action, "Pawn");

    internal BehaviorValues Defaults(ActionPlan action, string family)
    {
        var values = BehaviorContract.DeclaredDefaults(action, family);
        if (development != null && BuiltInActionKeys.Contains(action.Key))
            values.SimulationIntervalTicks = action.Key == CoreActions.Wounded ? development.WoundedInterval : development.Interval;
        else if (intervals.TryGetValue(action.Key, out long interval)) values.SimulationIntervalTicks = interval;
        if (IsOfficialInteraction(action.Key))
        {
            values.SimulationIntervalTicks = null;
            values.InteractionIntervalSeconds = development == null ? action.Key == CoreActions.Select ? 0.3 : 0.6 : development.InteractionIntervalSeconds;
        }
        if (family == "Pawn" && scopes.TryGetValue(action.Key, out var scope)) values.Scope = scope;
        return values;
    }

    internal bool ChecksShared(ActionPlan action) => action.CheckShared && !sharedExemptions.Contains(action.Key);
    internal static bool IsOfficialInteraction(string key)
        => key == CoreActions.Select || key == CoreActions.Draft || key == CoreActions.Undraft;
}
