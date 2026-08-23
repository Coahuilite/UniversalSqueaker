namespace SqueakyRatkin;

// Legacy bridge root type (explicitly activated for the US takeover/dev compatibility window).
// Thin subclass of the canonical UniversalSqueaker.SqueakVoicePackDef: legacy `SqueakyRatkin.SqueakVoicePackDef`
// XML nodes fill the inherited canonical fields, so old SR packs load without any schema duplication.
// The catalog, validator, and UI treat every legacy instance explicitly as an old SR pack (see
// LegacyVoicePackBridge.IsLegacy and SqueakLog.LegacyVoicePackAdmitted).
public class SqueakVoicePackDef : UniversalSqueaker.SqueakVoicePackDef
{
}
