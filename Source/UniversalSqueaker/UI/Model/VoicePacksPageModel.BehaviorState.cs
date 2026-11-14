namespace UniversalSqueaker.UI;

public static partial class VoicePacksPageModel
{
    /// <summary>Project the active write domain without changing remembered navigation identities.
    /// Callers retain their own eligibility checks; this projection adds no new prerequisite.</summary>
    internal static void GetTuningWriteDomain(VoicePacksPageState state, out string race, out string child)
    {
        race = state.TuningLayer == 0 ? "" : state.TuningRaceDefName ?? "";
        child = state.TuningLayer is 0 or 1 ? "" : state.TuningXenotypeDefName ?? "";
    }
    /// <summary>The CONTEXT return layer above the row steps, declared on the container that wraps the
    /// composite (PM observation 1, per §4.1 "当前层域/区域 → 设置主层"): the branch's activity is its own
    /// business state, so even the untouched default Global/Actions page answers one press before the
    /// settings main layer may close. The exit reposes the branch to its initial defaults - it changes NO
    /// settings, selects no domain (the Global layer has none to auto-pick, the BuildTuningDomains
    /// layer-0 branch), and any later tuning write re-arms the context.</summary>
    public static bool CanCancelTuningContext(VoicePacksPageState state)
        => state != null && state.TuningContextActive;


    public static void CancelTuningContext(VoicePacksPageState state)
    {
        if (state == null) return;
        state.TuningContextActive = false;
        state.TuningLayer = 0;
        state.TuningRaceDefName = "";
        state.TuningXenotypeDefName = "";
        state.TuningArea = 0;
        state.TuningSelectedAction = "";
        // UIR-004: the fallback selection state is NOT reposed here any more - the editor lives on the
        // Packs page and owns its state through its own return layer. Clearing it from a tuning-context
        // return would be one page silently rewriting another page's browse state.
    }
    public static void SetTuningLayer(VoicePacksPageState state, int layer)
    {
        if (state == null) return;
        if (layer >= 0 && layer <= 2) { state.TuningLayer = layer; state.TuningContextActive = true; }
    }
    public static void SetTuningDomain(VoicePacksPageState state, string raceDefName, string targetDefName)
    {
        if (state == null) return;
        if (!string.IsNullOrEmpty(raceDefName))
        {
            state.TuningRaceDefName = raceDefName;
            state.TuningXenotypeDefName = targetDefName ?? "";
            state.TuningContextActive = true;
        }
    }
    public static void SetTuningSelectedAction(VoicePacksPageState state, string actionKey)
    {
        if (state == null) return;
        state.TuningSelectedAction = actionKey ?? "";
        state.TuningContextActive = true;
    }
}
