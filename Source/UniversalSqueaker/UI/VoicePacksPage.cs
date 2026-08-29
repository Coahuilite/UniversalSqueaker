using UnityEngine;
using Verse;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>
/// VoicePacks settings page entry. Ferrite is the only rendering path; the legacy
/// componentized path has been removed to avoid dual-path drift.
/// </summary>
public static class VoicePacksPage
{
    public static void BeginSession()
    {
        UsWidgetRegistrar.EnsureRegistered();
        UiGuard.ResetSessionLog();
        AttenuationEditorWidget.ResetSession();
        VanillaVoicePacksPage.ResetSession();
        FerriteVoicePacksPage.BeginSession();
    }

    public static void EndSession()
    {
        UiGuard.ResetSessionLog();
        AttenuationEditorWidget.ResetSession();
        VanillaVoicePacksPage.ResetSession();
        FerriteVoicePacksPage.EndSession();
    }

    public static void Draw(Rect rect)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;
        UsWidgetRegistrar.EnsureRegistered();
        FerriteVoicePacksPage.Draw(rect);
    }
}
