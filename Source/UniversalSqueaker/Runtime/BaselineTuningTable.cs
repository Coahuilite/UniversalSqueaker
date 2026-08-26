using System;
using System.Collections.Generic;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Merged read-only view over all <see cref="UniversalSqueakerTuningBaselineDef"/>s.
/// Last-wins: later-loaded Defs override earlier ones, and within one Def later list entries win.
/// Empty/missing entries stay absent here so callers fall back to built-in defaults.
/// Every lookup re-scans and fully rebuilds the table (repeatable by design) so DefDatabase
/// lifecycle (reloads, third-party injection) can never leave stale state behind.
/// </summary>
public static class BaselineTuningTable
{
    private static readonly Dictionary<string, BaselineActionTuning> actions = new(StringComparer.Ordinal);
    private static readonly Dictionary<SqueakMood, BaselineMoodTuning> moods = new();

    /// <summary>Full rescan of <c>DefDatabase&lt;UniversalSqueakerTuningBaselineDef&gt;.AllDefs</c>, last-wins.</summary>
    public static void Rebuild()
    {
        actions.Clear();
        moods.Clear();
        try
        {
            foreach (UniversalSqueakerTuningBaselineDef def in DefDatabase<UniversalSqueakerTuningBaselineDef>.AllDefs)
            {
                if (def == null) continue;
                if (def.actions != null)
                {
                    foreach (BaselineActionTuning tuning in def.actions)
                    {
                        if (tuning == null || string.IsNullOrWhiteSpace(tuning.actionKey)) continue;
                        actions[tuning.actionKey] = tuning;
                    }
                }
                if (def.moods != null)
                {
                    foreach (BaselineMoodTuning tuning in def.moods)
                    {
                        if (tuning == null) continue;
                        moods[tuning.mood] = tuning;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // One broken third-party Def must never take the whole tuning baseline down.
            actions.Clear();
            moods.Clear();
            Verse.Log.Error("UniversalSqueaker: BaselineTuningTable rebuild failed: " + ex);
        }
    }

    /// <summary>Baseline scope for the action key, if declared.</summary>
    public static bool TryGetScope(string key, out SqueakActionScope scope)
    {
        scope = SqueakActionScope.AnyOccurrence;
        if (string.IsNullOrEmpty(key)) return false;
        Rebuild();
        if (actions.TryGetValue(key, out BaselineActionTuning? tuning) && tuning != null)
        {
            scope = tuning.scope;
            return true;
        }
        return false;
    }

    /// <summary>Baseline interval multiplier for the action key, if declared.</summary>
    public static bool TryGetInterval(string key, out float intervalMultiplier)
    {
        intervalMultiplier = 1f;
        if (string.IsNullOrEmpty(key)) return false;
        Rebuild();
        if (actions.TryGetValue(key, out BaselineActionTuning? tuning) && tuning != null)
        {
            intervalMultiplier = tuning.intervalMultiplier;
            return true;
        }
        return false;
    }

    /// <summary>Baseline probability multiplier for the action key, if declared.</summary>
    public static bool TryGetProb(string key, out float probabilityMultiplier)
    {
        probabilityMultiplier = 1f;
        if (string.IsNullOrEmpty(key)) return false;
        Rebuild();
        if (actions.TryGetValue(key, out BaselineActionTuning? tuning) && tuning != null)
        {
            probabilityMultiplier = tuning.probabilityMultiplier;
            return true;
        }
        return false;
    }

    /// <summary>Baseline mood modulation for the mood, if declared.</summary>
    public static bool TryGetMood(SqueakMood mood, out BaselineMoodTuning tuning)
    {
        tuning = null!;
        Rebuild();
        return moods.TryGetValue(mood, out tuning);
    }
}
