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

    internal BehaviorValues Defaults(ActionPlan action)
    {
        var values = BehaviorContract.DeclaredDefaults(action);
        if (development != null && BuiltInActionKeys.Contains(action.Key))
            values.HardIntervalTicks = action.Key == CoreActions.Wounded ? development.WoundedInterval : development.Interval;
        else if (intervals.TryGetValue(action.Key, out long interval)) values.HardIntervalTicks = interval;
        if (scopes.TryGetValue(action.Key, out var scope)) values.Scope = scope;
        return values;
    }

    internal bool ChecksShared(ActionPlan action) => action.CheckShared && !sharedExemptions.Contains(action.Key);
}
