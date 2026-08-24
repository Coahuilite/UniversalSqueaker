using System;
using System.Collections.Generic;
using System.Linq;

namespace FerriteLib.UiKit;

/// <summary>
/// Scope-based widget registry. Kinds are resolved exact-scope first, then the core scope.
/// The core scope hosts the library's neutral built-in widgets.
/// </summary>
public static class WidgetRegistry
{
    public const string CoreScope = "core";

    private static readonly object Gate = new();
    private static readonly Dictionary<string, Dictionary<string, Func<IWidget>>> Scopes =
        new(StringComparer.Ordinal);

    static WidgetRegistry()
    {
        // Core widgets are always available for the core fallback path.
        Widgets.CoreWidgetRegistrar.RegisterAll();
    }

    public static void Register(string scope, string kind, Func<IWidget> factory)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (kind == null) throw new ArgumentNullException(nameof(kind));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        lock (Gate)
        {
            if (!Scopes.TryGetValue(scope, out Dictionary<string, Func<IWidget>>? registry))
            {
                registry = new Dictionary<string, Func<IWidget>>(StringComparer.Ordinal);
                Scopes.Add(scope, registry);
            }
            registry[kind] = factory;
        }
    }

    /// <summary>Resolves a fresh widget instance. Exact scope wins; core scope is the fallback.</summary>
    public static IWidget Resolve(string scope, string kind)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (kind == null) throw new ArgumentNullException(nameof(kind));

        lock (Gate)
        {
            if (Scopes.TryGetValue(scope, out Dictionary<string, Func<IWidget>>? registry)
                && registry.TryGetValue(kind, out Func<IWidget>? factory))
            {
                return factory();
            }

            if (!string.Equals(scope, CoreScope, StringComparison.Ordinal)
                && Scopes.TryGetValue(CoreScope, out Dictionary<string, Func<IWidget>>? core)
                && core.TryGetValue(kind, out Func<IWidget>? coreFactory))
            {
                return coreFactory();
            }
        }

        throw new UnknownWidgetKindException(scope, kind);
    }

    /// <summary>
    /// Returns registered kind strings. When <paramref name="scope"/> is null, returns the union of
    /// all kinds across every scope (deduplicated). When a scope is provided, returns that scope's
    /// kinds (empty when the scope is unknown).
    /// </summary>
    public static IReadOnlyCollection<string> KnownKinds(string? scope = null)
    {
        lock (Gate)
        {
            if (scope != null)
            {
                if (!Scopes.TryGetValue(scope, out Dictionary<string, Func<IWidget>>? registry))
                    return Array.Empty<string>();
                return registry.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            }

            var all = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Dictionary<string, Func<IWidget>> registry in Scopes.Values)
            {
                foreach (string kind in registry.Keys)
                    all.Add(kind);
            }
            return all.ToArray();
        }
    }

    /// <summary>Clears all registered scopes. Intended for tests.</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            Scopes.Clear();
        }
    }
}
