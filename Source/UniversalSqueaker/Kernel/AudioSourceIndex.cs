using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalSqueaker.Kernel;

/// <summary>Resource identity comes from the loaded definition, never its referring voice pack.</summary>
public readonly struct AudioId : IEquatable<AudioId>
{
    public readonly string PackageId;
    public readonly string DefName;
    public AudioId(string packageId, string defName)
    {
        if (string.IsNullOrWhiteSpace(packageId) || packageId != packageId.Trim() || packageId.Contains("/")
            || string.IsNullOrWhiteSpace(defName) || defName != defName.Trim() || defName.Contains("/"))
            throw new ArgumentException("Invalid audio identity");
        PackageId = packageId.ToLowerInvariant();
        DefName = defName;
    }
    public static bool TryParse(string? value, out AudioId id)
    {
        id = default;
        if (value == null) return false;
        int slash = value.IndexOf('/');
        if (slash <= 0 || slash == value.Length - 1 || value.IndexOf('/', slash + 1) >= 0) return false;
        try { id = new AudioId(value.Substring(0, slash), value.Substring(slash + 1)); return true; }
        catch (ArgumentException) { return false; }
    }
    public bool Equals(AudioId other) => PackageId == other.PackageId && DefName == other.DefName;
    public override bool Equals(object? other) => other is AudioId id && Equals(id);
    public override int GetHashCode() => (PackageId?.GetHashCode() ?? 0) * 397 ^ (DefName?.GetHashCode() ?? 0);
    public override string ToString() => PackageId + "/" + DefName;
}

/// <summary>Finite startup observations. No game objects, XML loader or runtime state live here.</summary>
public sealed class AudioSourceIndex
{
    private readonly Dictionary<string, List<string>> originals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string?>> finals = new(StringComparer.Ordinal);
    public void ObserveOriginal(string name, string owner)
    {
        if (!originals.TryGetValue(name, out var sources)) originals.Add(name, sources = new List<string>());
        sources.Add(owner.ToLowerInvariant());
    }
    public void ObserveFinal(string name, string? owner)
    {
        if (!finals.TryGetValue(name, out var sources)) finals.Add(name, sources = new List<string?>());
        sources.Add(string.IsNullOrWhiteSpace(owner) ? null : owner!.ToLowerInvariant());
    }
    public bool TryBind(string name, bool ready, out AudioId id, out string reason)
    {
        id = default;
        if (!finals.TryGetValue(name, out var declarations)) { reason = "DeletedOrUnconstructed"; return false; }
        if (declarations.Count != 1) { reason = "DuplicateFinalDeclaration"; return false; }
        if (!originals.TryGetValue(name, out var sources) || sources.Count != 1)
        { reason = sources == null ? "UnobservedSource" : "DuplicateOriginalDeclaration"; return false; }
        string owner = sources[0];
        if (declarations[0] != null && declarations[0] != owner)
        { reason = "ConflictingFinalSource"; return false; }
        if (!ready) { reason = "UnsafeNativePool"; return false; }
        try { id = new AudioId(owner, name); }
        catch (ArgumentException) { reason = "InvalidSourceIdentity"; return false; }
        reason = "Bound";
        return true;
    }
}
