using System;
using System.Collections.Generic;
using UniversalSqueaker.OneShot;

namespace UniversalSqueaker.Authoring;

/// <summary>The expanded order is an explicitly selected Dev candidate, never the daily default.</summary>
public static class BuiltInPawnContracts
{
    public static IReadOnlyList<string> CandidateOrder { get; } = Array.AsReadOnly(new[]
    {
        "Death", "Wounded", "MentalBreak", "Attack", "Draft", "Undraft", "Equip", "Crying",
        "Giggling", "Select", "Sleep", "Eat", "Social", "Joy", "Move", "Work", "Call"
    });

    public static bool IsEvent(string key) => key != "Call" && key != "Eat" && key != "Sleep"
        && key != "Social" && key != "Joy" && key != "Move" && key != "Work";

    public static string FactKey(string key) => key == "Eat" ? EatPolicy.Process : "us.core/" + key + (IsEvent(key) ? "Event" : "State");

    public static IEnumerable<ActionPlan> Create(long interval, long woundedInterval, bool fullDevCandidate)
    {
        for (int i = 0; i < CandidateOrder.Count; i++)
        {
            string key = CandidateOrder[i];
            if (!fullDevCandidate && key != "Work" && key != "Wounded" && key != "Call")
            {
                continue;
            }
            int priority = fullDevCandidate ? CandidateOrder.Count - i : key == "Wounded" ? 30 : key == "Work" ? 20 : 0;
            yield return new ActionPlan("us.core/" + key, priority, new Condition("ref", FactKey(key)),
                key == "Wounded" ? woundedInterval : interval, skipTalking: key == "Death" || key == "Select",
                requiresVocal: key != "Select");
        }
    }
}
