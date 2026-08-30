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
        // FerriteVoicePacksPage.BeginSession resets page state only on the first frame of a session.
        // VanillaVoicePacksPage.ResetSession must NOT be called here every frame: it would wipe
        // ActiveTab/scroll/help state on every DrawSettings call and make navigation appear dead.
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
