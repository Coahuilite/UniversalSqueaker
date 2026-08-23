using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>One safe settings-frame sample of game-only services.  Never obtain Find services from menu UI code.</summary>
public readonly struct SqueakSettingsGameContext
{
    public readonly bool IsPlaying;
    public readonly bool HasPlayableMapUI;
    public readonly Map? Map;
    public readonly int Tick;
    public readonly float TickRateMultiplier;
    public readonly float Realtime;
    public readonly Pawn? SelectedPawn;

    private SqueakSettingsGameContext(bool playing, bool playableMapUi, Map? map, int tick, float tickRate, float realtime, Pawn? selectedPawn)
    {
        IsPlaying = playing; HasPlayableMapUI = playableMapUi; Map = map; Tick = tick;
        TickRateMultiplier = tickRate; Realtime = realtime; SelectedPawn = selectedPawn;
    }

    public static SqueakSettingsGameContext Capture() => Capture(true);

    /// <summary>Runtime-only sampling needs clocks/maps but never selection; avoid selector work on trigger paths.</summary>
    internal static SqueakSettingsGameContext CaptureRuntime() => Capture(false);

    private static SqueakSettingsGameContext Capture(bool includeSelection)
    {
        float realtime = Time.realtimeSinceStartup;
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null || Current.Root is not Root_Play)
            return new SqueakSettingsGameContext(false, false, null, 0, 0f, realtime, null);

        TickManager? ticks = Find.TickManager;
        Map? map = Find.CurrentMap;
        if (ticks == null || map == null || Find.MapUI == null)
            return new SqueakSettingsGameContext(true, false, map, ticks?.TicksGame ?? 0, ticks?.TickRateMultiplier ?? 0f, realtime, null);

        // Selector is a MapUI service. Do not touch it until the Playing + CurrentMap + MapUI gate passed.
        Pawn? selected = includeSelection ? Find.Selector?.SingleSelectedThing as Pawn : null;
        return new SqueakSettingsGameContext(true, true, map, ticks.TicksGame, ticks.TickRateMultiplier, realtime, selected);
    }

    public bool IsPawnOnCurrentMap(Pawn? pawn) => HasPlayableMapUI && pawn != null && pawn.Spawned && pawn.MapHeld == Map;
    public bool TryGetSelectedSqueaker(out Pawn? pawn, out CompSqueaker? squeaker)
    {
        pawn = SelectedPawn;
        squeaker = IsPawnOnCurrentMap(pawn) ? pawn!.TryGetComp<CompSqueaker>() : null;
        return squeaker != null;
    }
}
