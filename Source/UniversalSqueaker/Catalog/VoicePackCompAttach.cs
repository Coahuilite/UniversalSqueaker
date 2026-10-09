using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Route-table auto-attach (canonical). Every race declared by an admitted VoicePack
/// (<c>raceDefName</c>, Race or Xenotype scope) gets <see cref="CompProperties_Squeaker.CreateDefault"/>
/// if it does not already have a squeak comp. The declaration itself is the routing table and the
/// auto-attach is its reader: a pack's raceDefName claim is enough to make the race audible, with no
/// race or sound-key literal and no mandatory author patch.
/// <para>
/// Escape hatch: an existing <see cref="CompProperties_Squeaker"/> (author patch with custom trigger
/// tuning) always wins — the mount is skipped so custom config is never overwritten. The patch thus
/// degrades from "required" to "optional advanced use".
/// </para>
/// <para>
/// Uninstall safety is unchanged: mounting mutates only the in-memory <c>ThingDef.comps</c>, never the
/// save; a restart naturally reverts.
/// </para>
/// </summary>
public static class VoicePackCompAttach
{
    /// <summary>
    /// Mounts the default squeak comp on the VF1 support set: the union of races declared by
    /// admitted packs AND races carried by a final fallback table (maintainer data or a
    /// player-created one). No pack and no table = no mount, no routing, no notice. Runs on the
    /// main thread during startup (LongEventHandler.ExecuteWhenFinished), i.e. after Defs are
    /// fully loaded and XML patches applied, and before any pawn is generated.
    /// </summary>
    public static void Apply(SqueakXenotypeCatalogSnapshot catalog, IEnumerable<string>? tableRaceDefNames = null)
    {
        if (catalog == null) return;
        try
        {
            // Mount set = pack declarations ∪ table races (both scopes declare raceDefName). A
            // Xenotype targetDefName never mounts: a XenotypeDef is not a component carrier;
            // xenotypes enter at runtime as the identity's second dimension. Player pack selection
            // is irrelevant - a mounted comp with an empty pool is silent, same as a patch.
            HashSet<string> mount = new(StringComparer.Ordinal);
            foreach (string raceDefName in catalog.RaceDefNames)
            {
                if (!string.IsNullOrWhiteSpace(raceDefName)) mount.Add(raceDefName);
            }
            if (tableRaceDefNames != null)
            {
                foreach (string raceDefName in tableRaceDefNames)
                {
                    if (!string.IsNullOrWhiteSpace(raceDefName)) mount.Add(raceDefName);
                }
            }

            foreach (string raceDefName in mount)
            {
                try
                {
                    ThingDef? def = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName);
                    if (def == null)
                    {
                        SqueakLog.CompAttachSkipped(raceDefName, "race_not_found");
                        continue;
                    }
                    if (def.race == null)
                    {
                        SqueakLog.CompAttachSkipped(raceDefName, "no_race_props");
                        continue;
                    }
                    if (def.comps.Any(comp => comp is CompProperties_Squeaker))
                    {
                        SqueakLog.CompAttachSkipped(raceDefName, "author_patch");
                        continue;
                    }
                    def.comps.Add(CompProperties_Squeaker.CreateDefault());
                    SqueakLog.CompAutoAttached(raceDefName);
                }
                catch (Exception ex)
                {
                    SqueakLog.CompAttachSkipped(raceDefName, "exception");
                    SqueakLog.CompAttachFailed(ex);
                }
            }
        }
        catch (Exception ex)
        {
            SqueakLog.CompAttachFailed(ex);
        }
    }
}