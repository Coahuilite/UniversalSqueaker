// ISOLATED DEFINITION-MODEL EXTENSION - UniversalSqueakerDefinitionDirectoryTests ONLY.
// This is NOT the frozen carrier stub snapshot and never ships: it copies the frozen VerseStub
// sources into this task's own output and ADDS the game-definition types the OVT-001 collector
// reads (the frozen surface deliberately has none - gate 15's `defs` exemptions record that
// boundary). Test code compiles against the REAL Krafs.Rimworld.Ref 1.6.4871 signatures; these
// doubles match them at runtime (ThingDef/Def fields per OVT-REF16-MEMBERS-20261015.json:
// category/thingClass/race/modContentPack are FIELDS; RaceProperties.Humanlike/Animal/
// IsMechanoid/IsAnomalyEntity and ModsConfig.BiotechActive are PROPERTIES). Fixtures seed the
// databases; nothing here is a game implementation.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Verse
{
    /// <summary>Real 1.6 ThingCategory (field order = underlying values; the collector compares
    /// ThingCategory.Pawn, whose numeric value is baked into its IL from the reference).</summary>
    public enum ThingCategory
    {
        None,
        Pawn,
        Item,
        Building,
    }

    /// <summary>Real ThingDef's definition fields the collector reads (public fields in 1.6).</summary>
    public class ThingDef : Def
    {
        public ThingCategory category;
        public Type? thingClass;
        public RaceProperties? race;
    }

    /// <summary>The four public bool flags the collector reads (real 1.6 properties).</summary>
    public class RaceProperties
    {
        public bool Humanlike { get; set; }
        public bool Animal { get; set; }
        public bool IsMechanoid { get; set; }
        public bool IsAnomalyEntity { get; set; }
    }

    /// <summary>Corpse exists as a type token (the fixture corpse defs set thingClass to it; the
    /// collector only ever asks typeof(Pawn).IsAssignableFrom(thingClass)).</summary>
    public class Corpse
    {
    }



    /// <summary>The pack-key source (TryGetPackKey reads ModContentPack.ModMetaData.PackageIdNonUnique).</summary>
    public class ModMetaData
    {
        public ModMetaData(string packageId) { PackageIdNonUnique = packageId ?? ""; }
        public string PackageIdNonUnique { get; }
        public string AuthorsString { get; set; } = "";
        public string Name { get; set; } = "";
        public string VersionStr { get; set; } = "";
    }

    /// <summary>Real 1.6 shape: PackageId/Name/IsCoreMod/IsOfficialMod are properties;
    /// official package ids are public const strings (inlined at compile time on the consumer
    /// side, so only the property reads reach this file at runtime).</summary>
    public class ModContentPack
    {
        public const string CoreModPackageId = "Ludeon.RimWorld";
        public const string RoyaltyModPackageId = "Ludeon.RimWorld.Royalty";
        public const string IdeologyModPackageId = "Ludeon.RimWorld.Ideology";
        public const string BiotechModPackageId = "Ludeon.RimWorld.Biotech";
        public const string AnomalyModPackageId = "Ludeon.RimWorld.Anomaly";
        public const string OdysseyModPackageId = "Ludeon.RimWorld.Odyssey";

        public ModContentPack(string packageId, string name, bool isCore = false, bool isOfficial = false)
        {
            PackageId = packageId ?? "";
            Name = name ?? "";
            IsCoreMod = isCore;
            IsOfficialMod = isOfficial;
        }

        public string PackageId { get; }
        public string Name { get; }
        public bool IsCoreMod { get; }
        public bool IsOfficialMod { get; }

        /// <summary>Real 1.6 property the pack-key funnel reads.</summary>
        public Verse.ModMetaData? ModMetaData { get; set; }
    }

    /// <summary>Static database surface (AllDefs / GetNamedSilentFail / Add match the real
    /// statics the collector and the existing product code call). FIXTURE SEAM: Load replaces
    /// the whole set for one scenario; production never calls it.</summary>
    public class DefDatabase<T> where T : Def
    {
        private static readonly List<T> defs = new List<T>();

        public static IEnumerable<T> AllDefs => defs;

        public static void Add(T def)
        {
            if (def != null) defs.Add(def);
        }

        public static T GetNamedSilentFail(string name)
        {
            return defs.FirstOrDefault(d => string.Equals(d.defName, name, StringComparison.OrdinalIgnoreCase));
        }

        public static void Clear() => defs.Clear();
    }

    /// <summary>The real static gate the model reads every view build; settable by fixtures.</summary>
    public static class ModsConfig
    {
        public static bool BiotechActive { get; set; }
    }
}

namespace AlienRace
{
    using Verse;

    /// <summary>FIXTURE SHAPE ONLY (CO TEST-BOUNDARY): the official inheritance is
    /// AlienRace.ThingDef_AlienRace : ThingDef in namespace AlienRace - the collector recognises
    /// loaded defs whose TYPE chain passes through exactly this full name. This is the model of
    /// the framework's shape, NOT the real AlienRace assembly and NOT proof of a user's loaded
    /// HAR list.</summary>
    public class ThingDef_AlienRace : ThingDef
    {
    }
}

namespace Verse.Sound
{
    /// <summary>Real order (Any=0, MapOnly=1, WorldOnly=2) - the validator compares
    /// MapOnly, whose numeric value the product IL bakes from the reference (real namespace
    /// Verse.Sound, verified against the 1.6.4871 reference).</summary>
    public enum SoundContext
    {
        Any,
        MapOnly,
        WorldOnly,
    }

    /// <summary>Validator-reachable data fields only (onCamera + grains); no playback
    /// surface (real namespace: Verse.Sound).</summary>
    public class SubSoundDef
    {
        public bool onCamera;
        public System.Collections.Generic.List<Verse.Sound.AudioGrain>? grains;
    }

    /// <summary>Type token: the validator only requires non-null grain entries.</summary>
    public class AudioGrain
    {
    }
}
