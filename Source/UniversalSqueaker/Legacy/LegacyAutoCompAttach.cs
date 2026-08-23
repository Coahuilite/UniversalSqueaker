using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Legacy compatibility auto-attach. Old SR packs never carried a comp patch themselves; the SR mod
/// attached the comp for them. With the legacy bridge active, US does the same from pack data:
/// every race declared by an admitted legacy pack gets CompProperties_Squeaker if it does not already
/// have one. In practice old SR packs declare Ratkin, so this attaches Ratkin without any product
/// literal — the race comes from the pack's raceDefName, keeping routing neutral.
/// </summary>
public static class LegacyAutoCompAttach
{
    public static void Apply(SqueakXenotypeCatalogSnapshot catalog)
    {
        if (catalog == null) return;
        try
        {
            HashSet<string> legacyRaces = new(StringComparer.Ordinal);
            foreach (SqueakVoicePackDef pack in catalog.PackByKey.Values)
                if (LegacyVoicePackBridge.IsLegacy(pack) && !string.IsNullOrEmpty(pack.raceDefName))
                    legacyRaces.Add(pack.raceDefName);

            foreach (string raceDefName in legacyRaces)
            {
                ThingDef? def = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName);
                if (def?.race == null) continue;
                if (def.comps.Any(comp => comp is CompProperties_Squeaker)) continue;
                def.comps.Add(CompProperties_Squeaker.CreateDefault());
                SqueakLog.LegacyCompAutoAttached(raceDefName);
            }
        }
        catch (Exception ex)
        {
            SqueakLog.LegacyCompAutoAttachFailed(ex);
        }
    }
}
