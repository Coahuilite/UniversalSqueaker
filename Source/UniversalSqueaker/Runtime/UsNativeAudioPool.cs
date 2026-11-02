using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using Verse.Sound;

namespace UniversalSqueaker;

/// <summary>Reads the pool the native selector actually uses; never resolves or repairs grains.</summary>
internal static class UsNativeAudioPool
{
    private static readonly FieldInfo? Pool = typeof(SubSoundDef).GetField("resolvedGrains", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? Completed = typeof(SubSoundDef).GetField("distinctResolvedGrainsCount", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly Func<AudioClip, bool> ClipReady = CreateClipReadiness();

    private static Func<AudioClip, bool> CreateClipReadiness()
    {
        var clip = Expression.Parameter(typeof(AudioClip), "clip");
        var stateProperty = typeof(AudioClip).GetProperty("loadState", BindingFlags.Public | BindingFlags.Instance);
        var stateField = typeof(AudioClip).GetField("loadState", BindingFlags.Public | BindingFlags.Instance);
        Type? stateType = stateProperty?.PropertyType ?? stateField?.FieldType;
        if (stateType?.IsEnum == true && Enum.IsDefined(stateType, "Loaded"))
        {
            Expression state = stateProperty != null ? Expression.Property(clip, stateProperty) : Expression.Field(clip, stateField!);
            return Expression.Lambda<Func<AudioClip, bool>>(Expression.Equal(state,
                Expression.Constant(Enum.Parse(stateType, "Loaded"), stateType)), clip).Compile();
        }

        // Waylith opens the backend resource before constructing AudioClip; it has no loadState API.
        // Require that exact resource API and valid native sample metadata, never merely a non-null clip.
        var sound = typeof(AudioClip).GetProperty("Sound", BindingFlags.NonPublic | BindingFlags.Instance);
        if (typeof(AudioClip).Assembly.GetName().Name != "UnityStubs" || sound?.GetGetMethod(true) == null
            || sound.PropertyType.IsValueType)
            return _ => false;

        Expression ready = Expression.NotEqual(Expression.Property(clip, sound), Expression.Constant(null, sound.PropertyType));
        foreach (string name in new[] { "samples", "channels", "frequency" })
        {
            var property = typeof(AudioClip).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property?.PropertyType != typeof(int) || property.GetGetMethod() == null)
                return _ => false;
            ready = Expression.AndAlso(ready, Expression.GreaterThan(Expression.Property(clip, property), Expression.Constant(0)));
        }
        return Expression.Lambda<Func<AudioClip, bool>>(ready, clip).Compile();
    }

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
            if (grain is ResolvedGrain_Clip clip && clip.clip != null && ClipReady(clip.clip))
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
