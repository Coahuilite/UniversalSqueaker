using Verse;

namespace UniversalSqueaker;

/// <summary>本地化 helper:动作/心情显示名走 Keyed(US.Action.*/US.Mood.*),供 UI/调试复用。</summary>
public static class SqueakLabels
{
    public static string Action(string a) => ActionPresentations.Get(a).DisplayKey.Translate();
    public static string SettingsCategory => "US.SettingsCategory".Translate();
}
