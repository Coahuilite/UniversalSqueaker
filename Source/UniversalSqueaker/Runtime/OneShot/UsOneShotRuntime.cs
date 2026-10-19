using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;
using RimWorld;
using UnityEngine;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.OneShot;

namespace UniversalSqueaker;

public sealed class UsVoicePackDef : Def
{
    public VoicePackDeclaration declaration = new();
}

public sealed class UsActionDef : Def
{
    public ActionDeclaration declaration = new();
}

public sealed class UsProviderDef : Def
{
    public Type providerClass = null!;
}

/// <summary>Explicit developer session, not the unapproved replacement of the player's daily schedule.</summary>
public sealed class UsControlledOneShotSession : IOneShotBackend, IOneShotRandom
{
    private readonly Pawn pawn;
    private readonly AuthorCatalog catalog;
    private readonly DecisionState state;
    private readonly StatePolicy policy;
    private readonly ActionPlan[] actions;
    private readonly OneShotDecision decision = new();
    private readonly List<KeyValuePair<RandomPurpose, double>> draws = new();
    public IReadOnlyList<KeyValuePair<RandomPurpose, double>> LastRandomDraws => draws.AsReadOnly();
    public DecisionOutcome LastOutcome { get; private set; }
    public DecisionState State => state;

    internal UsControlledOneShotSession(Pawn pawn, AuthorCatalog catalog, StatePolicy policy, long workTicks, long woundedTicks)
    {
        this.pawn = pawn;
        this.catalog = catalog;
        this.policy = policy;
        state = new DecisionState(Find.TickManager.TicksGame);
        actions = catalog.Actions.Select(action => new ActionPlan(
            action.Key,
            action.Priority,
            action.Condition,
            action.BaseAction == "us.core/Wounded" ? woundedTicks : workTicks,
            action.CheckShared,
            action.WriteShared,
            action.Chance,
            action.BaseAction)).ToArray();
    }

    public void Tick(bool wounded, bool activeCommand = false)
    {
        long now = Find.TickManager.TicksGame;
        if (!wounded && now < state.NextStateTick)
        {
            return;
        }

        var comp = pawn.GetComp<CompSqueaker>();
        bool work = !wounded && comp.ControlledWorkActive();
        bool woundedOccurred = wounded;
        var vocal = comp.ControlledOneShotVocal();
        var values = new Dictionary<string, Truth>
        {
            ["us.core/WorkState"] = work ? Truth.True : Truth.False,
            ["us.core/WoundedEvent"] = woundedOccurred ? Truth.True : Truth.False,
            // No unconditional Call fallback; a complete idle fact belongs to its later adapter.
            ["us.core/CallState"] = Truth.Unknown
        };
        var input = new DecisionInput
        {
            Subject = pawn.ThingID,
            Domain = pawn.def.defName,
            Tick = now,
            StateOpportunity = !wounded,
            Eligible = pawn.Spawned
                && pawn.MapHeld == Find.CurrentMap
                && Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(pawn.Position)
                && SqueakRuntimeResolver.Current.VoicePackMode != SqueakVoicePackMode.Disabled,
            HasVocalOrgans = vocal.VocalOrganEfficiency > SqueakVocalCapability.VocalSilenceThreshold,
            TalkingChance = CompSqueaker.ScaleFrequencyWithTalking && vocal.TalkingChance < SqueakVocalCapability.TalkingCertainThreshold ? vocal.TalkingChance : 1,
            Facts = catalog.Capture(pawn.ThingID, values),
            Actions = actions,
            Audio = catalog.Audio,
            SourceMode = SqueakKernelAdapter.ToSelectionMode(SqueakRuntimeResolver.Current.VoicePackMode),
            Fallback = SqueakKernelAdapter.BuildBuiltIn(),
            BlockedActions = CaptureBlockedActions(comp, activeCommand)
        };
        draws.Clear();
        LastOutcome = decision.Run(state, policy, input, this, this);
    }

    private ISet<string> CaptureBlockedActions(CompSqueaker comp, bool activeCommand)
    {
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in actions)
        {
            string legacyKey = action.BaseAction.Substring(action.BaseAction.LastIndexOf('/') + 1);
            if (!comp.ControlledOneShotAllowed(legacyKey, activeCommand))
            {
                blocked.Add(action.Key);
            }
        }
        return blocked;
    }
    public double Next(RandomPurpose purpose)
    {
        double value = Rand.Value;
        draws.Add(new KeyValuePair<RandomPurpose, double>(purpose, value));
        return value;
    }

    public PlaybackReceipt Dispatch(PlaybackRequest request)
    {
        SoundDef? sound = null;
        SoundInfo info = default;
        string rejection = "DynamicPreflightRejected";
        bool Preflight()
        {
            if (!UnityData.IsInMainThread)
            {
                rejection = "MainThreadRequired";
                return false;
            }
            if (request.Subject != pawn.ThingID || request.Domain != pawn.def.defName)
            {
                rejection = "SubjectIdentityChanged";
                return false;
            }
            if (!pawn.Spawned || pawn.Destroyed || pawn.MapHeld != Find.CurrentMap
                || !Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(pawn.Position))
            {
                rejection = "SubjectOrLocationUnavailable";
                return false;
            }
            sound = UsAudioBindings.Resolve(request.Audio.AudioId);
            if (sound == null)
            {
                rejection = "MissingFinalBinding";
                return false;
            }
            if (sound.sustain)
            {
                rejection = "OneShotShapeRequired";
                return false;
            }
            if (!SqueakSoundAvailabilityCache.TryCreateProductionInfo(sound, pawn, out info, out var playability))
            {
                rejection = "DynamicPlayability:" + playability;
                return false;
            }
            return true;
        }

        void Play()
        {
            info.volumeFactor = (float)request.Audio.Volume * CompSqueaker.GlobalVolumeFactor;
            sound!.PlayOneShot(info);
        }

        var receipt = OneShotDispatch.Execute(
            Preflight,
            Play,
            () => Log.Message("usdiag OneShot " + request.Action + " " + request.Audio.AudioId + " NativeCallReturned"));
        return receipt.Result == PlaybackResult.Rejected && receipt.Reason == "PreflightRejected"
            ? new PlaybackReceipt(PlaybackResult.Rejected, rejection)
            : receipt;
    }
}

public static class UsOneShotRuntime
{
    private static readonly Dictionary<Pawn, UsControlledOneShotSession> sessions = new();
    private static AuthorCatalog catalog = new(720, 216);
    public static IReadOnlyList<string> Issues => catalog.Issues;
    public static IEnumerable<string> DeclaredRaces => catalog.Audio.Select(x => x.Domain).Distinct(StringComparer.Ordinal);

    public static void Initialize()
    {
        sessions.Clear();
        catalog = new AuthorCatalog(720, 216);
        foreach (var provider in DefDatabase<UsProviderDef>.AllDefsListForReading)
        {
            try
            {
                catalog.AddProvider((IUsDefinitionProvider)Activator.CreateInstance(provider.providerClass)!);
            }
            catch (Exception error)
            {
                catalog.Issues.Add(provider.defName + ":" + error.Message);
            }
        }

        foreach (var action in DefDatabase<UsActionDef>.AllDefsListForReading)
        {
            catalog.AddAction(action.declaration);
        }

        foreach (var pack in DefDatabase<UsVoicePackDef>.AllDefsListForReading)
        {
            if (DefDatabase<ThingDef>.GetNamedSilentFail(pack.declaration.Race)?.race != null)
            {
                catalog.AddPack(pack.modContentPack?.PackageId ?? "", pack.defName, pack.declaration, Bind);
            }
            else
            {
                catalog.Issues.Add(pack.defName + ":TargetRaceUnavailable");
            }
        }

        foreach (string issue in catalog.Issues)
        {
            Log.Warning("usdiag authoring " + issue);
        }
    }

    private static string? Bind(string name)
    {
        string? key = UsAudioBindings.KeyForName(name);
        if (key == null)
        {
            return null;
        }
        var sound = UsAudioBindings.Resolve(key);
        if (sound == null)
        {
            return null;
        }
        return key;
    }

    public static void EnableControlled(Pawn pawn, long waitTicks, long sharedTicks, long workTicks, long woundedTicks)
    {
        if (!Prefs.DevMode)
        {
            throw new InvalidOperationException("Controlled OneShot requires developer mode and explicit policy");
        }

        if (pawn.GetComp<CompSqueaker>() == null)
        {
            throw new InvalidOperationException("Pawn needs an existing CompSqueaker");
        }

        if (pawn.GetComp<CompSqueaker>().HasActiveSustainer)
        {
            throw new InvalidOperationException("End existing sustained playback before opting into OneShot control");
        }

        sessions[pawn] = new UsControlledOneShotSession(pawn, catalog, new StatePolicy(sharedTicks, waitTicks), workTicks, woundedTicks);
    }

    public static void DisableControlled(Pawn pawn) => sessions.Remove(pawn);
    public static bool IsControlled(Pawn pawn) => sessions.ContainsKey(pawn);
    public static bool TryTick(Pawn pawn)
    {
        if (!sessions.TryGetValue(pawn, out var session))
        {
            return false;
        }

        if (!pawn.Spawned || pawn.Destroyed)
        {
            sessions.Remove(pawn);
            return true;
        }

        if (SqueakRuntimeResolver.Current.VoicePackMode != SqueakVoicePackMode.Disabled && pawn.MapHeld == Find.CurrentMap)
        {
            session.Tick(false);
        }

        return true;
    }

    public static bool TryEvent(Pawn pawn, string key, SqueakTriggerOrigin origin, SqueakInvocationSource source)
    {
        if (!sessions.TryGetValue(pawn, out var session))
        {
            return false;
        }

        var invocation = new SqueakTriggerInvocation(origin, source);
        if (key == "Wounded" && SqueakRuntimeResolver.Current.VoicePackMode != SqueakVoicePackMode.Disabled && pawn.GetComp<CompSqueaker>().ControlledInvocationAllowed(invocation))
        {
            session.Tick(true, invocation.IsActiveCommand);
        }

        // Controlled session is exclusively the S1 action set; no parallel legacy cooldown owner.
        return true;
    }

    public static void ResetSession() => sessions.Clear();
}

public sealed class UsOneShotSessionLifecycle : GameComponent
{
    public UsOneShotSessionLifecycle(Game game)
    {
        UsOneShotRuntime.ResetSession();
    }

    public override void StartedNewGame() => UsOneShotRuntime.ResetSession();
    public override void LoadedGame() => UsOneShotRuntime.ResetSession();
}
