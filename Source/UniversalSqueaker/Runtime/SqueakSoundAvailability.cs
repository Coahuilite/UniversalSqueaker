using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace UniversalSqueaker;

/// <summary>Intrinsic, loaded-resource availability. Resolution is deliberately demand-driven.</summary>
public enum SqueakSoundAvailabilityState { Unknown, Available, Empty, Failed }
public enum SqueakSoundContextKind { NoAudio, CameraOnly, InMapOnly, Mixed }
public enum SqueakSoundPlayability { Playable, NoAudio, Failed, SustainerUnsupported, MixedContextUnsupported, UnsafeSoundContext, MapRequired }

public sealed class SqueakResolvedClip
{
    public readonly SubSoundDef SubSound;
    public readonly ResolvedGrain_Clip Grain;
    public readonly AudioClip Clip;
    public readonly int SubSoundIndex;
    public readonly int GrainIndex;
    public readonly string GrainType;
    public readonly string ClipPath;
    public readonly string FolderPath;
    internal SqueakResolvedClip(SubSoundDef subSound, ResolvedGrain_Clip grain, int subSoundIndex, int grainIndex,
        string grainType, string clipPath, string folderPath)
    {
        SubSound = subSound; Grain = grain; Clip = grain.clip; SubSoundIndex = subSoundIndex; GrainIndex = grainIndex;
        GrainType = grainType; ClipPath = clipPath; FolderPath = folderPath;
    }
}

public sealed class SqueakSoundAvailability
{
    public readonly SqueakSoundAvailabilityState State;
    public readonly SqueakSoundContextKind Context;
    public readonly IReadOnlyList<SqueakResolvedClip> Clips;
    public readonly string Diagnostic;
    internal SqueakSoundAvailability(SqueakSoundAvailabilityState state, SqueakSoundContextKind context, IReadOnlyList<SqueakResolvedClip> clips, string diagnostic = "")
    {
        State = state; Context = context; Clips = clips; Diagnostic = diagnostic;
    }
}

/// <summary>One-shot cache of already-loaded clips; it never enumerates files or copies audio data.</summary>
public static class SqueakSoundAvailabilityCache
{
    private static readonly Dictionary<SoundDef, SqueakSoundAvailability> cache = new();
    private static readonly SqueakSoundAvailability Empty = new(SqueakSoundAvailabilityState.Empty, SqueakSoundContextKind.NoAudio, Array.Empty<SqueakResolvedClip>());

    public static SqueakSoundAvailabilityState PeekState(SoundDef? sound) => sound == null
        ? SqueakSoundAvailabilityState.Empty
        : cache.TryGetValue(sound, out SqueakSoundAvailability? availability) ? availability.State : SqueakSoundAvailabilityState.Unknown;

    public static bool TryGetCached(SoundDef? sound, out SqueakSoundAvailability availability)
    {
        if (sound != null && cache.TryGetValue(sound, out SqueakSoundAvailability? found)) { availability = found; return true; }
        availability = Empty;
        return false;
    }

    public static SqueakSoundAvailability Resolve(SoundDef? sound)
    {
        if (sound == null) return Empty;
        if (cache.TryGetValue(sound, out SqueakSoundAvailability? cached)) return cached;

        List<SqueakResolvedClip> clips = new();
        bool failed = false, unsupported = false;
        string failure = "";
        int subSoundIndex = -1;
        foreach (SubSoundDef? subSound in sound.subSounds ?? new List<SubSoundDef>())
        {
            subSoundIndex++;
            if (subSound == null) continue;
            int grainIndex = -1;
            foreach (AudioGrain? grain in subSound.grains ?? new List<AudioGrain>())
            {
                grainIndex++;
                if (grain == null) continue;
                try
                {
                    string grainType = grain.GetType().Name;
                    string clipPath = ReadStringMember(grain, "clipPath");
                    string folderPath = ReadStringMember(grain, "clipFolderPath");
                    bool yielded = false;
                    foreach (object resolved in grain.GetResolvedGrains())
                    {
                        if (resolved is ResolvedGrain_Clip clip && clip.clip != null)
                        {
                            yielded = true;
                            clips.Add(new SqueakResolvedClip(subSound, clip, subSoundIndex, grainIndex, grainType, clipPath, folderPath));
                        }
                    }
                    if (!yielded && grainType.IndexOf("Clip", StringComparison.OrdinalIgnoreCase) < 0) unsupported = true;
                }
                catch (Exception ex) { failed = true; if (failure.Length == 0) failure = ex.GetType().Name; }
            }
        }

        SqueakSoundAvailabilityState state = clips.Count > 0 ? SqueakSoundAvailabilityState.Available
            : failed ? SqueakSoundAvailabilityState.Failed : SqueakSoundAvailabilityState.Empty;
        string diagnostic = clips.Count > 0 ? "available"
            : failed ? "exception:" + failure : unsupported ? "unsupported-or-no-result" : "silence-or-no-result";
        SqueakSoundAvailability result = new(state, GetContext(sound), clips.AsReadOnly(), diagnostic);
        cache[sound] = result;
        return result;
    }

    public static void Clear() => cache.Clear();

    private static string ReadStringMember(object owner, string name)
    {
        try
        {
            Type? type = owner.GetType();
            while (type != null)
            {
                System.Reflection.FieldInfo? field = type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field != null) return field.GetValue(owner) as string ?? "";
                type = type.BaseType;
            }
        }
        catch { }
        return "";
    }

    public static SqueakSoundPlayability GetNativePlayability(SoundDef? sound, Map? sourceMap, TargetInfo? sourceTarget)
    {
        SqueakSoundAvailability availability = Resolve(sound);
        if (availability.State == SqueakSoundAvailabilityState.Empty) return SqueakSoundPlayability.NoAudio;
        if (availability.State == SqueakSoundAvailabilityState.Failed) return SqueakSoundPlayability.Failed;
        if (sound == null || sound.sustain) return SqueakSoundPlayability.SustainerUnsupported;
        return GetPlayability(sound.context, availability.Context, sourceMap, sourceTarget);
    }

    public static SqueakSoundPlayability GetProductionPlayability(SoundDef? sound, Pawn? pawn)
    {
        SqueakSoundAvailability availability = Resolve(sound);
        if (availability.State == SqueakSoundAvailabilityState.Empty) return SqueakSoundPlayability.NoAudio;
        if (availability.State == SqueakSoundAvailabilityState.Failed) return SqueakSoundPlayability.Failed;
        if (sound == null || sound.sustain || sound.context != SoundContext.MapOnly
            || availability.Context != SqueakSoundContextKind.InMapOnly)
        {
            return SqueakSoundPlayability.UnsafeSoundContext;
        }

        return pawn != null && !pawn.Dead && pawn.Spawned && pawn.MapHeld != null
            && pawn.MapHeld == Find.CurrentMap && Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null
            ? SqueakSoundPlayability.Playable
            : SqueakSoundPlayability.MapRequired;
    }

    public static bool TryCreateNeutralInfo(SqueakResolvedClip? clip, Pawn? pawn, TargetInfo? fallbackTarget,
        out SoundInfo info, out SqueakSoundPlayability playability)
    {
        info = default;
        if (clip == null || clip.Clip == null) { playability = SqueakSoundPlayability.NoAudio; return false; }
        SqueakSoundContextKind context = clip.SubSound.onCamera ? SqueakSoundContextKind.CameraOnly : SqueakSoundContextKind.InMapOnly;
        playability = context == SqueakSoundContextKind.CameraOnly
            ? SqueakSoundPlayability.Playable
            : GetMapPlayability(pawn?.MapHeld ?? fallbackTarget?.Map);
        if (playability != SqueakSoundPlayability.Playable) return false;

        info = context == SqueakSoundContextKind.CameraOnly
            ? SoundInfo.OnCamera()
            : SoundInfo.InMap(pawn != null ? new TargetInfo(pawn) : fallbackTarget!.Value);
        info.pitchFactor = 1f;
        info.volumeFactor = 1f;
        return true;
    }

    public static bool TryCreateNeutralInfo(SoundDef? sound, SqueakResolvedClip? clip, Map? sourceMap,
        TargetInfo? sourceTarget, out SoundInfo info, out SqueakSoundPlayability playability)
    {
        info = default;
        if (sound == null || clip == null || clip.Clip == null)
        {
            playability = SqueakSoundPlayability.NoAudio;
            return false;
        }

        if (clip.SubSound.onCamera)
        {
            playability = sound.context switch
            {
                SoundContext.Any => SqueakSoundPlayability.Playable,
                SoundContext.WorldOnly => IsWorldRendered() ? SqueakSoundPlayability.Playable : SqueakSoundPlayability.UnsafeSoundContext,
                SoundContext.MapOnly => GetMapPlayability(sourceMap, sourceTarget),
                _ => SqueakSoundPlayability.UnsafeSoundContext,
            };
            if (playability != SqueakSoundPlayability.Playable) return false;
            info = SoundInfo.OnCamera();
        }
        else
        {
            playability = sound.context == SoundContext.MapOnly
                ? GetMapPlayability(sourceMap, sourceTarget)
                : SqueakSoundPlayability.UnsafeSoundContext;
            if (playability != SqueakSoundPlayability.Playable || sourceTarget == null) return false;
            info = SoundInfo.InMap(sourceTarget.Value);
        }

        info.pitchFactor = 1f;
        info.volumeFactor = 1f;
        return true;
    }

    public static bool TryCreateNativeInfo(SoundDef? sound, Map? sourceMap, TargetInfo? sourceTarget,
        out SoundInfo info, out SqueakSoundPlayability playability)
    {
        info = default;
        playability = GetNativePlayability(sound, sourceMap, sourceTarget);
        if (playability != SqueakSoundPlayability.Playable || sound == null) return false;

        SqueakSoundAvailability availability = Resolve(sound);
        if (availability.Context == SqueakSoundContextKind.CameraOnly)
        {
            info = SoundInfo.OnCamera();
        }
        else if (availability.Context == SqueakSoundContextKind.InMapOnly && sourceMap != null
            && sourceTarget.HasValue && sourceTarget.Value.IsValid && sourceTarget.Value.Map == sourceMap)
        {
            info = SoundInfo.InMap(sourceTarget.Value);
        }
        else
        {
            playability = SqueakSoundPlayability.MapRequired;
            return false;
        }

        info.pitchFactor = 1f;
        info.volumeFactor = 1f;
        return true;
    }

    /// <summary>Production playback is deliberately stricter than native preview eligibility.</summary>
    public static bool TryCreateProductionInfo(SoundDef? sound, Pawn? pawn, out SoundInfo info,
        out SqueakSoundPlayability playability)
    {
        info = default;
        playability = GetProductionPlayability(sound, pawn);
        if (playability != SqueakSoundPlayability.Playable || pawn == null)
        {
            return false;
        }

        info = SoundInfo.InMap(new TargetInfo(pawn));
        info.pitchFactor = 1f;
        info.volumeFactor = 1f;
        return true;
    }

    private static SqueakSoundPlayability GetPlayability(SoundContext soundContext, SqueakSoundContextKind resolvedContext, Map? sourceMap, TargetInfo? sourceTarget) => resolvedContext switch
    {
        SqueakSoundContextKind.NoAudio => SqueakSoundPlayability.NoAudio,
        SqueakSoundContextKind.Mixed => SqueakSoundPlayability.MixedContextUnsupported,
        SqueakSoundContextKind.CameraOnly => soundContext switch
        {
            SoundContext.Any => SqueakSoundPlayability.Playable,
            SoundContext.WorldOnly => IsWorldRendered() ? SqueakSoundPlayability.Playable : SqueakSoundPlayability.UnsafeSoundContext,
            SoundContext.MapOnly => GetMapPlayability(sourceMap, sourceTarget),
            _ => SqueakSoundPlayability.UnsafeSoundContext,
        },
        SqueakSoundContextKind.InMapOnly => soundContext == SoundContext.MapOnly
            ? GetMapPlayability(sourceMap, sourceTarget)
            : SqueakSoundPlayability.UnsafeSoundContext,
        _ => SqueakSoundPlayability.UnsafeSoundContext,
    };

    private static SqueakSoundPlayability GetMapPlayability(Map? sourceMap, TargetInfo? sourceTarget = null) => sourceMap != null && sourceTarget.HasValue && sourceTarget.Value.IsValid && sourceTarget.Value.Map == sourceMap && Current.ProgramState == ProgramState.Playing && Find.CurrentMap == sourceMap
        ? SqueakSoundPlayability.Playable
        : SqueakSoundPlayability.MapRequired;

    private static bool IsWorldRendered()
    {
        try { return Current.ProgramState == ProgramState.Playing && WorldRendererUtility.WorldRendered; }
        catch { return false; }
    }

    private static SqueakSoundContextKind GetContext(SoundDef sound)
    {
        bool camera = false, inMap = false;
        foreach (SubSoundDef? subSound in sound.subSounds ?? new List<SubSoundDef>())
        {
            if (subSound == null) continue;
            if (subSound.onCamera) camera = true; else inMap = true;
        }
        return camera && inMap ? SqueakSoundContextKind.Mixed : camera ? SqueakSoundContextKind.CameraOnly : inMap ? SqueakSoundContextKind.InMapOnly : SqueakSoundContextKind.NoAudio;
    }
}
