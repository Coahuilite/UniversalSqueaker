using System;

namespace FerriteLib.UiKit;

/// <summary>
/// Stable identifier for a value control. Two controls sharing the same scope and name share the
/// same <see cref="UiValueState"/>.
/// </summary>
public readonly struct UiControlId : IEquatable<UiControlId>
{
    public string Scope { get; }
    public string Name { get; }

    public UiControlId(string scope, string name)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public bool IsValid => Scope.Length > 0 && Name.Length > 0;

    public bool Equals(UiControlId other)
    {
        return string.Equals(Scope, other.Scope, StringComparison.Ordinal)
            && string.Equals(Name, other.Name, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is UiControlId other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (Scope != null ? StringComparer.Ordinal.GetHashCode(Scope) : 0);
            hash = hash * 31 + (Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0);
            return hash;
        }
    }

    public static bool operator ==(UiControlId left, UiControlId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(UiControlId left, UiControlId right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return Scope + ":" + Name;
    }
}
