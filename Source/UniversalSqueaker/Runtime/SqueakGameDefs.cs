using System;
using System.Collections.Generic;

using Verse;

namespace UniversalSqueaker.Runtime;

/// <summary>
/// Baseline-definition lookup used by the production tuning projection and preset reset.
/// The defaults query the game's current database without caching. The production-fold harness
/// supplies real baseline Def instances through these two delegates and restores the defaults
/// after its pass; it does not implement the game's generic DefDatabase or claim game loading.
/// </summary>
public static class SqueakGameDefs
{
    public static Func<string, UsBehaviorPresetDef?> BaselineByName = DefaultBaselineByName;
    public static Func<IEnumerable<UsBehaviorPresetDef>> AllBaselines = DefaultAllBaselines;

    public static void ResetToGameDefaults()
    {
        BaselineByName = DefaultBaselineByName;
        AllBaselines = DefaultAllBaselines;
    }

    private static UsBehaviorPresetDef? DefaultBaselineByName(string defName) => DefDatabase<UsBehaviorPresetDef>.GetNamedSilentFail(defName);
    private static IEnumerable<UsBehaviorPresetDef> DefaultAllBaselines() => DefDatabase<UsBehaviorPresetDef>.AllDefs;
}
