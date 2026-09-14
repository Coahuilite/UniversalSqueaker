using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Minimal production-facing debug surface. Every successful audio dispatch emits one dev-level route
/// record; vanilla fallback dispatches are warning-level. No statistical/UI diagnostics are retained.
/// </summary>
public static class SqueakDebug
{
    /// <summary>
    /// S4 diagnostics foundation: player-facing camera indicator. Owned by the settings
    /// toggle (<see cref="UniversalSqueakerSettings.SetCameraIndicator"/>) and consumed by
    /// <see cref="Patch_GlobalControlsUtility_CameraIndicator"/> — no DevMode gating.
    /// </summary>
    public static bool ShowCameraIndicator = false;

    /// <summary>
    /// S4 diagnostics: DebugAction entry point. Opens the session's main window; the detail
    /// column follows the current selection on its own (round-9 model - no more Selected/Visible
    /// mode fork at the entry).
    /// </summary>
    public static void OpenDiagnostics()
    {
        SqueakDiagnosticsOverlay.BeginSession();
    }

    /// <summary>usdiag v2 tier vocabulary: xenotype_pack / race_pack / vanilla / "-" for none.
    /// PackFallback folds into RacePack and BuiltInFallback into Vanilla.</summary>
    private static string ProtocolTier(SqueakSoundSource source) => source switch
    {
        SqueakSoundSource.XenotypePack => "xenotype_pack",
        SqueakSoundSource.RacePack => "race_pack",
        SqueakSoundSource.Vanilla => "vanilla",
        _ => "-",
    };

    public static void NotifySqueak(Pawn pawn, SqueakAction action, SqueakMood mood, SqueakSoundChoice choice)
        => NotifyAudioDispatched(pawn, UniversalSqueaker.Kernel.ActionKey.For(action) ?? action.ToString(), choice);

    public static void NotifySqueakByKey(Pawn pawn, string actionKey, SqueakMood mood, SqueakSoundChoice choice)
        => NotifyAudioDispatched(pawn, actionKey, choice);

    /// <summary>Log MVP: one dev-level record per actual dispatch. Vanilla fallback is warning-level;
    /// normal pack routing stays info-level. No rate limiting or suppression in this MVP.</summary>
    private static void NotifyAudioDispatched(Pawn pawn, string actionKey, SqueakSoundChoice choice)
    {
        if (!SqueakLog.EffectiveDevLogging) return;
        SoundDef? def = choice.Sound;
        if (def == null) return;
        string race = pawn.def?.defName ?? "";
        string? xenotype = pawn.genes?.Xenotype?.defName;
        string target = pawn.thingIDNumber.ToString();
        string pawnId = pawn.ThingID;
        string? pawnFaction = pawn.Faction?.def?.defName ?? "-";
        bool? pawnControlled = pawn.IsPlayerControlled;

        if (choice.Source == SqueakSoundSource.Vanilla)
        {
            SqueakLog.AudioVanillaFallback(
                actionKey,
                race,
                xenotype,
                target,
                def.defName,
                "vanilla",
                choice.PoolStableKey,
                choice.IsEgg,
                pawn.LabelShort,
                pawnId,
                pawnControlled,
                pawnFaction);
            return;
        }

        SqueakLog.AudioRouteSelected(
            actionKey,
            race,
            xenotype,
            target,
            def.defName,
            ProtocolTier(choice.Source),
            choice.PoolStableKey,
            choice.IsEgg,
            0,
            pawn.LabelShort,
            pawnId,
            pawnControlled,
            pawnFaction);
    }
}