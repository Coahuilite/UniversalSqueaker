using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.Sound;
using UniversalSqueaker.OneShot;

namespace UniversalSqueaker;

/// <summary>Explicit request contribution. No SoundDef, Pawn or pooled source can confer ownership.</summary>
internal sealed class UsOutputContribution
{
    internal readonly double Volume;
    internal bool FaultReported;
    internal UsOutputContribution(double volume)
    {
        Volume = volume;
    }
}

internal static class UsInstanceOutput
{
    private const string RequestParameter = "us.internal/output-request";
    private static readonly Dictionary<int, UsOutputContribution> requests = new();
    private static readonly ConditionalWeakTable<Sample, UsOutputContribution> samples = new();
    private static readonly ConditionalWeakTable<Sustainer, UsOutputContribution> sustainers = new();
    // Negative identity survives delayed grain callbacks without retaining a live parent or request.
    private static readonly ConditionalWeakTable<Sustainer, object> closedOwnedParents = new();
    private static int nextToken;
    internal static bool Available { get; private set; }
    internal static string Failure { get; private set; } = "OutputAdapterNotInitialized";
    internal static FloatRange Distance = new(15, 50);

    internal static void Initialize(Harmony harmony)
    {
        var targets = new MethodBase?[]
        {
            AccessTools.PropertyGetter(typeof(Sample), "Volume"),
            AccessTools.Method(typeof(Sample), nameof(Sample.Update)),
            AccessTools.Method(typeof(Sample), nameof(Sample.SampleCleanup)),
            AccessTools.Method(typeof(SampleSustainer), "TryMakeAndPlay", new[] { typeof(SubSustainer), typeof(AudioClip), typeof(float), typeof(float) }),
            AccessTools.Constructor(typeof(Sustainer), new[] { typeof(SoundDef), typeof(SoundInfo) }),
            AccessTools.Method(typeof(Sustainer), "PlaySoundOnMapOrCamera", new[] { typeof(SoundDef) }),
            AccessTools.Method(typeof(Sustainer), "Cleanup")
        };
        foreach (var target in targets)
        {
            if (target == null || Harmony.GetPatchInfo(target)?.Owners.Contains(harmony.Id) != true)
            {
                Available = false;
                Failure = "RequiredOutputPatchUnavailable:" + target?.Name;
                return;
            }
        }
        if (!UsSustainerAuxiliaryPatch.Validated)
        {
            Failure = "AuxiliaryCallShapeUnavailable";
            return;
        }
        Available = true;
        Failure = "";
    }

    internal static int Mark(ref SoundInfo info, UsOutputContribution owner)
    {
        // Floats represent every integer in this bounded token space exactly; only live requests occupy it.
        do
        {
            nextToken = nextToken == 16777215 ? 1 : nextToken + 1;
        } while (requests.ContainsKey(nextToken));
        requests.Add(nextToken, owner);
        info.SetParameter(RequestParameter, nextToken);
        return nextToken;
    }
    internal static void ReleaseRequest(int token) => requests.Remove(token);

    private static UsOutputContribution? FromInfo(SoundInfo info)
    {
        foreach (var parameter in info.DefinedParameters)
        {
            if (parameter.Key == RequestParameter && requests.TryGetValue((int)parameter.Value, out var owner))
            {
                return owner;
            }
        }
        return null;
    }

    internal static void CaptureSustainer(Sustainer sustainer, SoundInfo info)
    {
        if (IsClosedOwned(sustainer))
        {
            return;
        }
        var owner = FromInfo(info);
        if (owner != null)
        {
            sustainers.GetValue(sustainer, _ => owner);
        }
    }

    internal static UsOutputContribution? OwnerOf(Sustainer sustainer)
    {
        if (IsClosedOwned(sustainer))
        {
            return null;
        }
        if (sustainers.TryGetValue(sustainer, out var owner))
        {
            return owner;
        }
        owner = FromInfo(sustainer.info);
        if (owner != null)
        {
            sustainers.GetValue(sustainer, _ => owner);
        }
        return owner;
    }

    internal static void PlayAuxiliary(SoundDef sound, SoundInfo info, Sustainer parent)
    {
        if (IsClosedOwned(parent))
        {
            return;
        }
        var owner = OwnerOf(parent);
        if (owner == null)
        {
            sound.PlayOneShot(info);
            return;
        }
        int token = Mark(ref info, owner);
        try
        {
            sound.PlayOneShot(info);
        }
        finally
        {
            ReleaseRequest(token);
        }
    }

    private static UsOutputContribution? OwnerOf(Sample sample)
    {
        if (samples.TryGetValue(sample, out var owner))
        {
            return owner;
        }
        if (sample is SampleSustainer sustained && sustained.subSustainer?.parent != null)
            owner = OwnerOf(sustained.subSustainer.parent);
        owner ??= FromInfo(sample.Info); // Includes constructor-time first samples before parent constructor postfix.
        if (owner != null)
        {
            samples.GetValue(sample, _ => owner);
        }
        return owner;
    }

    internal static void ApplyVolume(Sample sample, ref float originalNativeVolume)
    {
        var owner = OwnerOf(sample);
        if (owner == null)
        {
            return;
        }
        double factor = owner.Volume * CompSqueaker.GlobalVolumeFactor;
        double result = originalNativeVolume * factor;
        if (!OneShotOutput.TryPrepare(result, 1, 1, out float volume, out _))
        {
            originalNativeVolume = 0;
            if (!owner.FaultReported)
            {
                owner.FaultReported = true;
                Log.Warning("usdiag output instance invalid current volume; silenced owned instance");
            }
            return;
        }
        originalNativeVolume = volume;
    }

    internal static void ApplyDistance(Sample sample)
    {
        if (OwnerOf(sample) == null || sample.source == null || sample.subDef.onCamera)
        {
            return;
        }
        sample.source.minDistance = Distance.TrueMin;
        sample.source.maxDistance = Distance.TrueMax;
    }

    internal static void ReleaseSample(Sample sample) => samples.Remove(sample);
    internal static bool IsClosedOwned(Sustainer sustainer) => closedOwnedParents.TryGetValue(sustainer, out _);

    internal static void ReleaseSustainer(Sustainer sustainer)
    {
        if (OwnerOf(sustainer) != null)
        {
            closedOwnedParents.GetValue(sustainer, _ => new object());
        }
        sustainers.Remove(sustainer);
    }
}

[HarmonyPatch(typeof(SampleSustainer), "TryMakeAndPlay", new[] { typeof(SubSustainer), typeof(AudioClip), typeof(float), typeof(float) })]
internal static class UsLateSustainedSamplePatch
{
    private static bool Prefix(SubSustainer subSus, ref SampleSustainer __result)
    {
        if (!UsInstanceOutput.IsClosedOwned(subSus.parent))
        {
            return true;
        }
        __result = null!;
        return false;
    }
}

[HarmonyPatch(typeof(Sample), "get_Volume")]
internal static class UsSampleVolumePatch
{
    private static void Postfix(Sample __instance, ref float __result) => UsInstanceOutput.ApplyVolume(__instance, ref __result);
}
[HarmonyPatch(typeof(Sample), nameof(Sample.Update))]
internal static class UsSampleDistancePatch
{
    private static void Prefix(Sample __instance) => UsInstanceOutput.ApplyDistance(__instance);
}
[HarmonyPatch(typeof(Sample), nameof(Sample.SampleCleanup))]
internal static class UsSampleCleanupPatch
{
    private static void Postfix(Sample __instance) => UsInstanceOutput.ReleaseSample(__instance);
}
[HarmonyPatch(typeof(Sustainer), MethodType.Constructor, new[] { typeof(SoundDef), typeof(SoundInfo) })]
internal static class UsSustainerCapturePatch
{
    private static void Postfix(Sustainer __instance, SoundInfo info) => UsInstanceOutput.CaptureSustainer(__instance, info);
}
[HarmonyPatch(typeof(Sustainer), "PlaySoundOnMapOrCamera")]
internal static class UsSustainerAuxiliaryPatch
{
    internal static bool Validated;
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> original)
    {
        var code = original.ToList();
        var play = AccessTools.Method(typeof(SoundStarter), "PlayOneShot", new[] { typeof(SoundDef), typeof(SoundInfo) });
        var replacement = AccessTools.Method(typeof(UsInstanceOutput), nameof(UsInstanceOutput.PlayAuxiliary));
        Validated = code.Count(x => x.Calls(play)) == 2;
        if (!Validated)
        {
            return code;
        }
        var result = new List<CodeInstruction>();
        foreach (var instruction in code)
        {
            if (instruction.Calls(play))
            {
                var parent = new CodeInstruction(OpCodes.Ldarg_0);
                parent.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                result.Add(parent);
                instruction.operand = replacement;
            }
            result.Add(instruction);
        }
        return result;
    }
}
[HarmonyPatch(typeof(Sustainer), "Cleanup")]
internal static class UsSustainerCleanupPatch
{
    private static void Postfix(Sustainer __instance) => UsInstanceOutput.ReleaseSustainer(__instance);
}
