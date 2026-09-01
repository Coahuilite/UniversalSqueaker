using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Registers every kernel-owned US composite widget kind in the greenfield
/// <see cref="UiWidgetRegistry"/> under the US scope, each with its creation-time
/// allowed-attribute contract. The kernel Host resolves these kinds and rejects unknown attributes
/// at creation time; no old neutral registry is involved.
/// </summary>
public static class UsKernelWidgetRegistrar
{
    public const string Scope = "coahuilite.universalsqueaker";

    private static readonly object Gate = new();
    private static bool registered;

    public static void EnsureRegistered()
    {
        // Self-healing: when a test harness cleared the widget registry, re-register instead of
        // skipping on the stale flag. Keeps repeated production calls idempotent while the
        // registry's own duplicate-kind rejection stays diagnosable.
        if (registered && UiWidgetRegistry.KnownKinds(Scope).Count > 0) return;

        lock (Gate)
        {
            if (registered && UiWidgetRegistry.KnownKinds(Scope).Count > 0) return;

            UsNavWidget.Register();
            UsPageTitleWidget.Register();
            UsModeRowWidget.Register();
            UsGlobalVolumeWidget.Register();
            UsAttenuationEditorWidget.Register();
            UsBasicTuningWidget.Register();
            UsCameraIndicatorWidget.Register();
            UsScopeTreeWidget.Register();
            UsPresetListWidget.Register();
            UsFilterBarWidget.Register();
            UsRaceLayerWidget.Register();
            UsXenotypeLayerWidget.Register();
            UsVoicePackChecklistWidget.Register();
            UsKernelFooterWidget.Register();
            UsHelpPanelWidget.Register();
            UsCameraReadoutWidget.Register();

            registered = true;
        }
    }

    /// <summary>Shared attribute schema for US section composites.</summary>
    internal static readonly IReadOnlyCollection<string> SectionSchema = new[]
    {
        "Id", "Kind", "Title", "TitleKey", "HelpKey", "Tab", "Hidden", "Height"
    };
}
