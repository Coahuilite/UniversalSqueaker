using System;

namespace FerriteLib.UiKit;

/// <summary>
/// A reusable UI element. Widgets are stateless renderers: <see cref="Configure"/> stores the
/// XML element spec once, <see cref="Measure"/> computes natural height, and <see cref="Draw"/>
/// renders into a pre-measured rect. Widgets never mutate page state directly; they emit
/// neutral <see cref="UiCommand"/> values through the provided action.
/// </summary>
public interface IWidget
{
    /// <summary>Kind tag used in XML manifests and the widget registry.</summary>
    string Kind { get; }

    /// <summary>Called once after factory instantiation. The widget stores its XML attributes.</summary>
    void Configure(UiElementSpec spec);

    /// <summary>Returns the natural content height for the current context, or 0 for nothing to draw.</summary>
    float Measure(WidgetContext ctx);

    /// <summary>Draws into <paramref name="rect"/> using the pre-measured layout rect.</summary>
    void Draw(UnityEngine.Rect rect, WidgetContext ctx, Action<UiCommand> emit);
}
