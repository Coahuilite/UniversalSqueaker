using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>
/// Registers US-owned composite widget kinds in the neutral widget registry under the US scope.
/// Core kinds stay in the library; only US-specific dynamic content lives here.
/// </summary>
public static class UsWidgetRegistrar
{
    public const string Scope = "coahuilite.universalsqueaker";

    private static readonly object Gate = new();
    private static bool registered;

    public static void EnsureRegistered()
    {
        if (registered) return;

        lock (Gate)
        {
            if (registered) return;

            WidgetRegistry.Register(Scope, RaceLayerWidget.Kind, () => new RaceLayerWidget());
            WidgetRegistry.Register(Scope, XenotypeLayerWidget.Kind, () => new XenotypeLayerWidget());
            WidgetRegistry.Register(Scope, VoicePackChecklistWidget.Kind, () => new VoicePackChecklistWidget());
            WidgetRegistry.Register(Scope, PageTitleWidget.Kind, () => new PageTitleWidget());

            registered = true;
        }
    }
}
