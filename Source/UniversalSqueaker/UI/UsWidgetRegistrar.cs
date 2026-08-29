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
            WidgetRegistry.Register(Scope, GlobalVolumeWidget.Kind, () => new GlobalVolumeWidget());
            WidgetRegistry.Register(Scope, AttenuationEditorWidget.Kind, () => new AttenuationEditorWidget());
            WidgetRegistry.Register(Scope, BasicTuningWidget.Kind, () => new BasicTuningWidget());
            WidgetRegistry.Register(Scope, CameraIndicatorWidget.Kind, () => new CameraIndicatorWidget());
            WidgetRegistry.Register(Scope, ScopeTreeWidget.Kind, () => new ScopeTreeWidget());
            WidgetRegistry.Register(Scope, PresetListWidget.Kind, () => new PresetListWidget());
            WidgetRegistry.Register(Scope, UsFooterWidget.Kind, () => new UsFooterWidget());
            WidgetRegistry.Register(Scope, FilterBarWidget.Kind, () => new FilterBarWidget());

            registered = true;
        }
    }
}
