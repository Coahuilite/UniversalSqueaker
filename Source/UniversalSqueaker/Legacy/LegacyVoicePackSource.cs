using System.Collections.Generic;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Single legacy-bridge entry point for catalog admission. DefDatabase enumerates the legacy root
/// type and upcasts each instance to the canonical type once; downstream validator/kernel/UI code
/// still sees only canonical SqueakVoicePackDef values (with IsLegacy available for explicit marking).
/// Removal surface: delete SqueakyRatkinLegacyVoicePackDef.cs + this file and the one catalog call site.
/// </summary>
public static class LegacyVoicePackSource
{
    public static IEnumerable<SqueakVoicePackDef> CollectLegacy()
    {
        foreach (SqueakyRatkin.SqueakVoicePackDef legacy in DefDatabase<SqueakyRatkin.SqueakVoicePackDef>.AllDefs)
            yield return legacy;
    }
}
