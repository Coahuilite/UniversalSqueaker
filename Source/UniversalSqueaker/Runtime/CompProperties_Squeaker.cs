using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace UniversalSqueaker;

public class CompProperties_Squeaker : CompProperties
{
    public List<SqueakActionConfig> actions = new();
    public List<SqueakMoodMod> moodMods = new();

    public CompProperties_Squeaker()
    {
        compClass = typeof(CompSqueaker);
    }

    /// <summary>
    /// Programmatic default used by the canonical auto-attach path. It mirrors the shipped 216-tick
    /// baseline and the 15 production-action plan of the example packs; no race or sound-key literals.
    /// </summary>
    public static CompProperties_Squeaker CreateDefault()
    {
        return new CompProperties_Squeaker
        {
            actions = new List<SqueakActionConfig>
            {
                new() { action = SqueakAction.Eat, mode = SqueakTriggerMode.EachTime, minIntervalTicks = 144 },
                new() { action = SqueakAction.Call, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 864, probabilityPerCheck = 0.012f },
                new() { action = SqueakAction.Move, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 504, probabilityPerCheck = 0.012f },
                new() { action = SqueakAction.Sleep, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 1080, probabilityPerCheck = 0.008f },
                new() { action = SqueakAction.Social, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 504, probabilityPerCheck = 0.016f },
                new() { action = SqueakAction.Joy, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 504, probabilityPerCheck = 0.016f },
                new() { action = SqueakAction.Wounded, mode = SqueakTriggerMode.External, minIntervalTicks = 216 },
                new() { action = SqueakAction.Select, mode = SqueakTriggerMode.External, minIntervalTicks = 18, ignoreGlobalCooldown = true, cooldownClock = SqueakCooldownClock.Realtime },
                new() { action = SqueakAction.Death, mode = SqueakTriggerMode.External, minIntervalTicks = 0, ignoreGlobalCooldown = true },
                new() { action = SqueakAction.Draft, mode = SqueakTriggerMode.External, minIntervalTicks = 36, ignoreGlobalCooldown = true, cooldownClock = SqueakCooldownClock.Realtime },
                new() { action = SqueakAction.Undraft, mode = SqueakTriggerMode.External, minIntervalTicks = 36, ignoreGlobalCooldown = true, cooldownClock = SqueakCooldownClock.Realtime },
                new() { action = SqueakAction.Attack, mode = SqueakTriggerMode.External, minIntervalTicks = 216 },
                new() { action = SqueakAction.Work, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 720, probabilityPerCheck = 0.012f },
                new() { action = SqueakAction.Equip, mode = SqueakTriggerMode.External, minIntervalTicks = 216 },
                new() { action = SqueakAction.MentalBreak, mode = SqueakTriggerMode.External, minIntervalTicks = 0, ignoreGlobalCooldown = true },
            },
            moodMods = new List<SqueakMoodMod>
            {
                new() { mood = SqueakMood.Good, pitchFactor = 1.2f, pitchJitter = new FloatRange(0.97f, 1.03f), volumeFactor = 1.3f },
                new() { mood = SqueakMood.Neutral, pitchFactor = 1.0f, pitchJitter = new FloatRange(0.97f, 1.03f), volumeFactor = 1.0f },
                new() { mood = SqueakMood.Bad, pitchFactor = 0.8f, pitchJitter = new FloatRange(0.97f, 1.03f), volumeFactor = 0.7f },
                new() { mood = SqueakMood.Break, pitchFactor = 1.1f, pitchJitter = new FloatRange(0.6f, 1.5f), volumeFactor = 1.5f },
            },
        };
    }
}

