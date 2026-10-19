using System;
using System.Collections.Generic;

namespace UniversalSqueaker.Kernel;

/// <summary>Resource identity comes from the loaded definition, never its referring voice pack.</summary>
public readonly struct AudioId : IEquatable<AudioId>
{
    public readonly string PackageId;
    public readonly string DefName;

    public AudioId(string packageId, string defName)
    {
        if (!ValidPart(packageId) || !ValidPart(defName))
        {
            throw new ArgumentException("Invalid audio identity");
        }
        PackageId = packageId.ToLowerInvariant();
        DefName = defName;
    }

    private static bool ValidPart(string value)
        => !string.IsNullOrWhiteSpace(value) && value == value.Trim() && !value.Contains("/");

    public static bool TryParse(string? value, out AudioId id)
    {
        id = default;
        if (value == null)
        {
            return false;
        }
        int slash = value.IndexOf('/');
        if (slash <= 0 || slash == value.Length - 1 || value.IndexOf('/', slash + 1) >= 0)
        {
            return false;
        }
        string owner = value.Substring(0, slash);
        string name = value.Substring(slash + 1);
        if (!ValidPart(owner) || !ValidPart(name))
        {
            return false;
        }
        id = new AudioId(owner, name);
        return true;
    }

    public bool Equals(AudioId other) => PackageId == other.PackageId && DefName == other.DefName;
    public override bool Equals(object? other) => other is AudioId id && Equals(id);
    public override int GetHashCode() => (PackageId?.GetHashCode() ?? 0) * 397 ^ (DefName?.GetHashCode() ?? 0);
    public override string ToString() => PackageId + "/" + DefName;
}

/// <summary>Finite startup observations. No XML nodes, game objects or runtime state live here.</summary>
public sealed class AudioSourceIndex
{
    private readonly Dictionary<string, List<string>> originals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<FinalDeclaration>> finals = new(StringComparer.Ordinal);

    private sealed class FinalDeclaration
    {
        internal readonly string? Owner;
        internal readonly string OriginalName;
        internal FinalDeclaration(string? owner, string originalName)
        {
            Owner = string.IsNullOrWhiteSpace(owner) ? null : owner!.ToLowerInvariant();
            OriginalName = originalName;
        }
    }

    public void ObserveOriginal(string name, string owner)
    {
        if (!originals.TryGetValue(name, out var sources))
        {
            sources = new List<string>();
            originals.Add(name, sources);
        }
        sources.Add(owner.ToLowerInvariant());
    }

    /// <summary>The adapter supplies originalName only for the same observed node and matching asset.</summary>
    public void ObserveFinal(string name, string? owner, string? originalName = null)
    {
        if (!finals.TryGetValue(name, out var declarations))
        {
            declarations = new List<FinalDeclaration>();
            finals.Add(name, declarations);
        }
        declarations.Add(new FinalDeclaration(owner, originalName ?? name));
    }

    public bool TryBind(string name, bool ready, out AudioId id, out string reason)
    {
        id = default;
        if (!finals.TryGetValue(name, out var declarations))
        {
            reason = "DeletedOrUnconstructed";
            return false;
        }
        if (declarations.Count != 1)
        {
            reason = "DuplicateFinalDeclaration";
            return false;
        }
        var declaration = declarations[0];
        if (!originals.TryGetValue(declaration.OriginalName, out var sources) || sources.Count != 1)
        {
            reason = sources == null ? "UnobservedSource" : "DuplicateOriginalDeclaration";
            return false;
        }
        string owner = sources[0];
        if (declaration.Owner != null && declaration.Owner != owner)
        {
            reason = "ConflictingFinalSource";
            return false;
        }
        if (!ready)
        {
            reason = "UnsafeNativePool";
            return false;
        }
        if (!AudioId.TryParse(owner + "/" + name, out id))
        {
            reason = "InvalidSourceIdentity";
            return false;
        }
        reason = "Bound";
        return true;
    }
}
