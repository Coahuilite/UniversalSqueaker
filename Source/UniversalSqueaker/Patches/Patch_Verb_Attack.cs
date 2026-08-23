using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Covers successful Core melee, ranged, and special non-Ability verb implementations that declare TryCastShot.
/// It intentionally excludes Ability and DLC assemblies; this is not a claim to cover every possible attack API.
/// </summary>
[HarmonyPatch]
public static class Patch_Verb_Attack
{
    private const int MaxSkippedTargetWarnings = 8;
    private static int skippedTargetWarnings;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        HashSet<MethodBase> targets = new();
        foreach (Assembly assembly in new[] { typeof(Verb).Assembly, typeof(JobDefOf).Assembly }.Distinct())
        {
            foreach (Type type in GetTypesSafely(assembly))
            {
                if (!typeof(Verb).IsAssignableFrom(type) || type.FullName?.IndexOf("Ability", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                MethodInfo? method = AccessTools.DeclaredMethod(type, "TryCastShot", Type.EmptyTypes);
                if (method == null || method.IsStatic || method.IsAbstract || method.ContainsGenericParameters || method.ReturnType != typeof(bool)) continue;
                try
                {
                    if (method.GetMethodBody() == null)
                    {
                        WarnSkippedTarget(method, "has no method body");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    WarnSkippedTarget(method, "could not inspect its method body (" + ex.GetType().Name + ")");
                    continue;
                }

                targets.Add(method);
            }
        }

        if (targets.Count == 0)
        {
            SqueakLog.HookAttackUnavailable();
        }
        foreach (MethodBase target in targets) yield return target;
    }

    private static void WarnSkippedTarget(MethodInfo method, string reason)
    {
        if (skippedTargetWarnings++ >= MaxSkippedTargetWarnings) return;
        if (!SqueakLog.ShouldEmitDev) return;
        SqueakLog.HookAttackTargetSkipped(method.DeclaringType?.FullName + "." + method.Name, reason);
    }

    private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(type => type != null)!; }
    }

    private static void Postfix(object __instance, bool __result)
    {
        if (!__result || __instance is not Verb verb || verb.caster is not Pawn pawn || !pawn.Spawned || pawn.MapHeld != Find.CurrentMap) return;
        pawn.GetComp<CompSqueaker>()?.Notify_Attack();
    }
}
