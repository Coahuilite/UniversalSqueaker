using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker;

/// <summary>These two hooks implement one capability. Never install a half observer.</summary>
internal static class UsSelectObservation
{
    internal const string FactKey = "us.core/SelectEvent";
    internal static readonly MethodInfo? Selection = AccessTools.Method(typeof(Selector), "SelectInternal",
        new[] { typeof(object), typeof(bool), typeof(bool) });
    internal static readonly MethodInfo? Feedback = AccessTools.Method(typeof(Selector), "PlaySelectionSoundFor", new[] { typeof(object) });
    internal static bool Ready => Selection != null && Feedback != null;
    internal static string FailureReason => "SelectObservationUnavailable:MissingTarget:"
        + (Selection == null ? "RimWorld.Selector.SelectInternal(object,bool,bool)" : "")
        + (Selection == null && Feedback == null ? ";" : "")
        + (Feedback == null ? "RimWorld.Selector.PlaySelectionSoundFor(object)" : "");
    private static bool reported;

    internal static bool Prepare()
    {
        if (Ready) return true;
        if (!reported)
        {
            reported = true;
            Log.Warning("usdiag capability=SelectObservation fact=" + FactKey + " reason=" + FailureReason);
        }
        return false;
    }

    internal static IEnumerable<FactDiagnostic> Diagnostics
    {
        get
        {
            if (!Ready) yield return new FactDiagnostic(FactKey, "SelectObservation", FailureReason);
        }
    }
}

/// <summary>Observe the resolved feedback recipient, then verify the selector's committed membership.
/// Source basis: installed 1.6.4871 SelectInternal/PlaySelectionSoundFor, cross-checked with RimSage 1.6.4850.
/// No independent duplicate-selection policy: only actual upstream feedback is consumed.</summary>
[HarmonyPatch]
public static class Patch_Selector_Select
{
    internal sealed class Attempt
    {
        internal readonly Selector Selector;
        internal readonly bool FeedbackRequested;
        internal readonly long Root;
        internal object? FeedbackSubject;
        internal Attempt(Selector selector, bool feedback, long root) { Selector = selector; FeedbackRequested = feedback; Root = root; }
    }
    [ThreadStatic] private static Stack<Attempt>? attempts;

    private static bool Prepare() => UsSelectObservation.Prepare();
    private static MethodBase TargetMethod() => UsSelectObservation.Selection!;

    private static void Prefix(Selector __instance, bool __1, out Attempt __state)
    {
        __state = new Attempt(__instance, __1, attempts?.Count > 0 ? attempts.Peek().Root : UsInteractionObservation.NewRoot());
        (attempts ??= new Stack<Attempt>()).Push(__state);
    }

    internal static void ObserveFeedback(Selector selector, object subject)
    {
        if (attempts?.Count > 0 && ReferenceEquals(attempts.Peek().Selector, selector))
            attempts.Peek().FeedbackSubject = subject;
    }

    private static void Postfix(Selector __instance, Attempt? __state)
    {
        // The actual feedback call resolves proxies. Membership alone is not feedback evidence.
        if (__state?.FeedbackRequested == true && __state.FeedbackSubject is Pawn pawn && __instance.IsSelected(pawn))
            pawn.GetComp<CompSqueaker>()?.Notify_Select(UsInteractionObservation.Accepted(UsSelectObservation.FactKey, __state.Root));
    }

    private static Exception? Finalizer(Exception? __exception, Attempt? __state)
    {
        if (attempts?.Count > 0 && ReferenceEquals(attempts.Peek(), __state)) attempts.Pop();
        return __exception;
    }
}

[HarmonyPatch]
public static class Patch_Selector_Feedback
{
    private static bool Prepare() => UsSelectObservation.Prepare();
    private static MethodBase TargetMethod() => UsSelectObservation.Feedback!;
    private static void Postfix(Selector __instance, object __0) => Patch_Selector_Select.ObserveFeedback(__instance, __0);
}
