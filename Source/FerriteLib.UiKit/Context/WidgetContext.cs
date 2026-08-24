using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit;

/// <summary>
/// Per-frame context passed to widgets. The view state is an opaque object; the only built-in
/// interpretation is a neutral duck-typed dictionary lookup (no reflection over business DTOs).
/// </summary>
public sealed class WidgetContext
{
    /// <summary>XML manifest source scope (e.g. the owning mod packageId).</summary>
    public string Source { get; }

    /// <summary>Opaque view model projected by the owning mod.</summary>
    public object? ViewState { get; }

    /// <summary>Text measurement seam.</summary>
    public ITextMetrics Metrics { get; }

    /// <summary>Shared page state (scroll, search, help, selection).</summary>
    public UiPageState State { get; }

    /// <summary>
    /// Current layout width. Set by <see cref="LayoutEngine"/> before the measure pass so widgets
    /// can measure text against the real content width even though <see cref="IWidget.Measure"/>
    /// only receives the context. Defaults to 1.
    /// </summary>
    public float ViewWidth { get; internal set; } = 1f;

    public WidgetContext(string source, object? viewState, ITextMetrics metrics, UiPageState state)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        ViewState = viewState;
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary>
    /// Duck-typed view lookup. If <see cref="ViewState"/> implements
    /// <see cref="IDictionary{TKey,TValue}"/> or <see cref="IReadOnlyDictionary{TKey,TValue}"/>
    /// with <c>string</c> keys and <c>object</c> values, returns the value for <paramref name="key"/>;
    /// otherwise returns false. Never uses reflection.
    /// </summary>
    public bool TryGetViewValue(string key, out object? value)
    {
        value = null;
        if (string.IsNullOrEmpty(key)) return false;

        switch (ViewState)
        {
            case IDictionary<string, object?> dictionary:
                return dictionary.TryGetValue(key, out value);
            case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                return readOnlyDictionary.TryGetValue(key, out value);
            default:
                return false;
        }
    }
}
