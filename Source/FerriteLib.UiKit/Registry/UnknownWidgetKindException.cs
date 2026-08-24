using System;

namespace FerriteLib.UiKit;

/// <summary>Thrown when a manifest references a widget Kind that no registered scope can resolve.</summary>
public sealed class UnknownWidgetKindException : Exception
{
    public string Scope { get; }

    public string Kind { get; }

    public UnknownWidgetKindException(string scope, string kind)
        : this(scope, kind, null)
    {
    }

    internal UnknownWidgetKindException(string scope, string kind, string? detail)
        : base(detail == null
            ? $"Unknown widget kind '{kind}' for scope '{scope}'."
            : $"Unknown widget kind '{kind}' for scope '{scope}'. {detail}")
    {
        Scope = scope ?? "";
        Kind = kind ?? "";
    }
}
