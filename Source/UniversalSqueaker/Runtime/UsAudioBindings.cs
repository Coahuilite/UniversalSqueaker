using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using Verse;
using Verse.Sound;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker;

/// <summary>Startup-only observations and final-object bindings; settings never rebuild native resources.</summary>
internal static class UsAudioBindings
{
    private static AudioSourceIndex sources = new();
    private static readonly Dictionary<string, List<SoundDef>> constructed = new(StringComparer.Ordinal);
    private static readonly Dictionary<AudioId, SoundDef> objects = new();
    private static readonly Dictionary<SoundDef, AudioId> identities = new();
    internal static void ObserveOriginal(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> assets)
    {
        sources = new AudioSourceIndex();
        constructed.Clear();
        objects.Clear();
        identities.Clear();
        foreach (XmlNode node in document.DocumentElement!.ChildNodes)
        {
            if (!IsSound(node) || node.Attributes?["Abstract"]?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) == true) continue;
            string? name = node.SelectSingleNode("defName")?.InnerText;
            if (!string.IsNullOrWhiteSpace(name) && assets.TryGetValue(node, out var asset) && asset.mod != null)
                sources.ObserveOriginal(name!, asset.mod.PackageId);
        }
    }
    private static bool IsSound(XmlNode node)
    {
        if (node.Name == "SoundDef" || node.Name == "Verse.SoundDef") return true;
        Type? type = GenTypes.GetTypeInAnyAssembly(node.Name);
        return type != null && typeof(SoundDef).IsAssignableFrom(type);
    }
    internal static void ObserveFinal(Def? result, LoadableXmlAsset? asset)
    {
        if (result is SoundDef sound)
        {
            sources.ObserveFinal(sound.defName, asset?.mod?.PackageId);
            if (!constructed.TryGetValue(sound.defName, out var list)) constructed.Add(sound.defName, list = new List<SoundDef>());
            list.Add(sound);
        }
    }
    internal static void Publish()
    {
        objects.Clear();
        identities.Clear();
        foreach (var sound in DefDatabase<SoundDef>.AllDefsListForReading)
        {
            bool ready = constructed.TryGetValue(sound.defName, out var observed) && observed.Count == 1
                && ReferenceEquals(observed[0], sound)
                && SqueakSoundAvailabilityCache.Resolve(sound).State == SqueakSoundAvailabilityState.Available;
            if (sources.TryBind(sound.defName, ready, out var id, out string reason))
            {
                objects.Add(id, sound);
                identities.Add(sound, id);
            }
            else Log.Warning("usdiag resource " + sound.defName + " " + reason);
        }
    }
    internal static string? KeyFor(SoundDef? sound) => sound != null && identities.TryGetValue(sound, out var id) ? id.ToString() : null;
    internal static string? KeyForName(string name) => KeyFor(DefDatabase<SoundDef>.GetNamedSilentFail(name));
    internal static SoundDef? Resolve(string key) => AudioId.TryParse(key, out var id) && objects.TryGetValue(id, out var sound) ? sound : null;
}

[HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.CombineIntoUnifiedXML))]
internal static class UsObserveOriginalAudio
{
    private static void Postfix(XmlDocument __result, Dictionary<XmlNode, LoadableXmlAsset> assetlookup)
    {
        try { UsAudioBindings.ObserveOriginal(__result, assetlookup); }
        catch (Exception error) { Log.Error("usdiag source observation failed: " + error.GetType().Name); }
    }
}

[HarmonyPatch]
internal static class UsObserveConstructedAudio
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(DirectXmlLoader), "DefFromNode");
        yield return AccessTools.Method(AccessTools.TypeByName("Verse.DirectXmlToObjectNew"), "DefFromNodeNew");
    }
    private static void Postfix(Def? __result, object[] __args)
    {
        try { UsAudioBindings.ObserveFinal(__result, __args.OfType<LoadableXmlAsset>().FirstOrDefault()); }
        catch (Exception error) { Log.Error("usdiag final observation failed: " + error.GetType().Name); }
    }
}
