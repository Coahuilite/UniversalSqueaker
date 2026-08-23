using System;

namespace UniversalSqueaker;

/// <summary>
/// Prefix and identity context shared by legacy (Squeaky Ratkin) and canonical (Universal Squeaker)
/// VoicePack defs. The validator uses the runtime def type to select the required author prefix:
/// legacy packs keep their SR_ author ABI, canonical packs use US_. Catalog/UI call IsLegacy to
/// surface an explicit "old SR pack" marker for every legacy instance.
/// </summary>
public static class LegacyVoicePackBridge
{
    public const string LegacyPrefix = "SR_";
    public const string CanonicalPrefix = "US_";
    public const string LegacyTypeName = "SqueakyRatkin.SqueakVoicePackDef";
    public const string LegacyUiTag = "Legacy SR";

    public static bool IsLegacy(SqueakVoicePackDef def) => def is SqueakyRatkin.SqueakVoicePackDef;

    public static string RequiredPrefixFor(Type defType)
        => defType == typeof(SqueakyRatkin.SqueakVoicePackDef) ? LegacyPrefix : CanonicalPrefix;

    public static string RequiredPrefixFor(SqueakVoicePackDef def) => RequiredPrefixFor(def.GetType());
}
