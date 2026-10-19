using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse.Sound;

namespace UniversalSqueaker;

/// <summary>Reads the pool the native selector actually uses; never resolves or repairs grains.</summary>
internal static class UsNativeAudioPool
{
    private static readonly FieldInfo? Pool = typeof(SubSoundDef).GetField("resolvedGrains", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? Completed = typeof(SubSoundDef).GetField("distinctResolvedGrainsCount", BindingFlags.NonPublic | BindingFlags.Instance);
    internal static IReadOnlyList<ResolvedGrain>? Read(SubSoundDef? subSound)
    {
        if (subSound == null || Pool?.GetValue(subSound) is not List<ResolvedGrain> grains
            || Completed?.GetValue(subSound) is not int count || count <= 0
            || grains.Count == 0 || grains.Distinct().Count() != count)
        {
            return null;
        }
        foreach (var grain in grains)
        {
            if (grain is ResolvedGrain_Clip clip && clip.clip != null && clip.clip.loadState == UnityEngine.AudioDataLoadState.Loaded)
            {
                continue;
            }
            if (grain != null && grain.GetType().FullName == "Verse.Sound.ResolvedGrain_Silence")
            {
                continue;
            }
            return null;
        }
        return grains;
    }
}
