using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned camera readout row for the overlay Host (second host). Draws the translated
/// camera height / view size right-aligned, exactly like the legacy pure-Verse patch, from a typed
/// read-only binding. Display-only: no input, no scroll, no popup, no native scope — the overlay
/// surface stays the narrowest possible real consumer of the shared registry/binding/theme/schema
/// mechanism.
/// </summary>
public sealed class UsCameraReadoutWidget : IUiWidget
{
    public const string KindName = "us/camera-readout";
    private const float RowHeight = 26f;

    public string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName, () => new UsCameraReadoutWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Hidden", UiAttributeKind.Boolean), new UiAttributeDefinition("Tab"))));
    }

    public void Configure(UiElementSpec spec)
    {
    }

    public void Validate(IUiBindingAccess bindings, string elementPath)
    {
        bindings.ValidateValue<string>("camera-readout", elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        return RowHeight;
    }

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;
        if (!ctx.Bindings.TryGet("camera-readout", out string text) || text.Length == 0) return;

        UsKernelDraw.Label(ctx.Drawing, rect, text, ctx.Theme, ctx.Theme[UiPaletteRole.Foreground], UiFont.Small, UiTextAnchor.UpperRight);
    }
}
