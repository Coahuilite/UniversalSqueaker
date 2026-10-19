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

/// <summary>Startup observations and final-object bindings; settings never rebuild native resources.</summary>
internal static class UsAudioBindings
{
    private static AudioSourceIndex sources = new();
    private static bool published;
    private static bool originalObservationComplete;
    private static bool declarationObservationComplete = true;
    private static Dictionary<XmlNode, OriginalNode> originalNodes = new();
    private static readonly Dictionary<SoundDef, string> rejected = new();
    private static readonly HashSet<SoundDef> reported = new();
    private static readonly Dictionary<string, List<SoundDef>> constructed = new(StringComparer.Ordinal);
    private static readonly Dictionary<AudioId, SoundDef> objects = new();
    private static readonly Dictionary<SoundDef, AudioId> identities = new();

    private sealed class OriginalNode
    {
        internal readonly string Name;
        internal readonly ModContentPack Owner;
        internal OriginalNode(string name, ModContentPack owner)
        {
            Name = name;
            Owner = owner;
        }
    }

    internal static void InitializeAfterNativeAudioResolution(Action initialize)
    {
        LongEventHandler.ExecuteWhenFinished(() => LongEventHandler.ExecuteWhenFinished(initialize));
    }

    internal static void ObserveOriginal(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> assets)
    {
        if (published)
        {
            return;
        }
        originalObservationComplete = false;
        var pendingSources = new AudioSourceIndex();
        var pendingNodes = new Dictionary<XmlNode, OriginalNode>();
        constructed.Clear();
        objects.Clear();
        identities.Clear();
        foreach (XmlNode node in document.DocumentElement!.ChildNodes)
        {
            if (!IsConcreteSound(node))
            {
                continue;
            }
            string? name = node.SelectSingleNode("defName")?.InnerText;
            if (!string.IsNullOrWhiteSpace(name) && assets.TryGetValue(node, out var asset) && asset.mod != null)
            {
                pendingSources.ObserveOriginal(name!, asset.mod.PackageId);
                pendingNodes.Add(node, new OriginalNode(name!, asset.mod));
            }
        }
        sources = pendingSources;
        originalNodes = pendingNodes;
        originalObservationComplete = true;
    }

    internal static void ObservationFailed(string stage, Exception error)
    {
        if (stage == "original")
        {
            originalObservationComplete = false;
        }
        else
        {
            declarationObservationComplete = false;
        }
        Log.Error("usdiag " + stage + " observation incomplete: " + error.GetType().Name + ":" + error.Message);
    }

    private static bool IsConcreteSound(XmlNode node)
    {
        if (node.NodeType != XmlNodeType.Element
            || node.Attributes?["Abstract"]?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
        {
            return false;
        }
        string typeName = node.Attributes?["Class"]?.Value ?? node.Name;
        if (typeName == "SoundDef" || typeName == "Verse.SoundDef")
        {
            return true;
        }
        Type? type = GenTypes.GetTypeInAnyAssembly(typeName);
        return type != null && typeof(SoundDef).IsAssignableFrom(type);
    }

    // Called by a loader prefix after native MayRequire filtering and inheritance resolution.
    // It records declarations even when the subsequent constructor returns null or throws.
    internal static void ObserveDeclaration(XmlNode node, LoadableXmlAsset? asset)
    {
        if (published || !IsConcreteSound(node))
        {
            return;
        }
        XmlNode resolved = XmlInheritance.GetResolvedNodeFor(node);
        string? name = resolved.SelectSingleNode("defName")?.InnerText;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        string? originalName = null;
        if (originalNodes.TryGetValue(node, out var original) && ReferenceEquals(asset?.mod, original.Owner))
        {
            originalName = original.Name;
        }
        sources.ObserveFinal(name!, asset?.mod?.PackageId, originalName);
    }

    internal static void ObserveConstructed(Def? result)
    {
        if (published || result is not SoundDef sound)
        {
            return;
        }
        if (!constructed.TryGetValue(sound.defName, out var declarations))
        {
            declarations = new List<SoundDef>();
            constructed.Add(sound.defName, declarations);
        }
        declarations.Add(sound);
    }

    internal static void Publish()
    {
        objects.Clear();
        identities.Clear();
        rejected.Clear();
        foreach (var sound in DefDatabase<SoundDef>.AllDefsListForReading)
        {
            if (!originalObservationComplete || !declarationObservationComplete)
            {
                rejected[sound] = "IncompleteSourceObservation";
                continue;
            }
            bool ready = constructed.TryGetValue(sound.defName, out var observed) && observed.Count == 1
                && ReferenceEquals(observed[0], sound)
                && SqueakSoundAvailabilityCache.Resolve(sound).State == SqueakSoundAvailabilityState.Available;
            if (sources.TryBind(sound.defName, ready, out var id, out string reason))
            {
                objects.Add(id, sound);
                identities.Add(sound, id);
            }
            else
            {
                rejected[sound] = reason;
            }
        }
        originalNodes.Clear();
        constructed.Clear();
        published = true;
    }

    internal static string? KeyFor(SoundDef? sound)
    {
        if (sound == null)
        {
            return null;
        }
        if (identities.TryGetValue(sound, out var id))
        {
            return id.ToString();
        }
        if (reported.Add(sound))
        {
            string reason = rejected.TryGetValue(sound, out var failure) ? failure : "MissingFinalBinding";
            Log.Warning("usdiag resource " + sound.defName + " " + reason);
        }
        return null;
    }

    internal static string? KeyForName(string name) => KeyFor(DefDatabase<SoundDef>.GetNamedSilentFail(name));
    internal static SoundDef? Resolve(string key)
        => AudioId.TryParse(key, out var id) && objects.TryGetValue(id, out var sound) ? sound : null;
}

[HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.CombineIntoUnifiedXML))]
internal static class UsObserveOriginalAudio
{
    private static void Postfix(XmlDocument __result, Dictionary<XmlNode, LoadableXmlAsset> assetlookup)
    {
        try
        {
            UsAudioBindings.ObserveOriginal(__result, assetlookup);
        }
        catch (Exception error)
        {
            UsAudioBindings.ObservationFailed("original", error);
        }
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

    private static void Prefix(object[] __args)
    {
        try
        {
            var node = __args.OfType<XmlNode>().FirstOrDefault();
            if (node != null)
            {
                UsAudioBindings.ObserveDeclaration(node, __args.OfType<LoadableXmlAsset>().FirstOrDefault());
            }
        }
        catch (Exception error)
        {
            UsAudioBindings.ObservationFailed("declaration", error);
        }
    }

    private static void Postfix(Def? __result)
    {
        try
        {
            UsAudioBindings.ObserveConstructed(__result);
        }
        catch (Exception error)
        {
            UsAudioBindings.ObservationFailed("constructed", error);
        }
    }
}
