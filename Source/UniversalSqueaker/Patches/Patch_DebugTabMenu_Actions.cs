using System.Reflection;
using HarmonyLib;
using LudeonTK;
using Verse;

namespace UniversalSqueaker;

/// <summary>Localizes vanilla DebugTabMenu action names/categories when the setting is on (S5 restore).</summary>
[HarmonyPatch(typeof(DebugTabMenu_Actions), "GenerateCacheForMethod")]
public static class Patch_DebugTabMenu_Actions
{
    private const string ActionKeyPrefix = "DebugAction_";
    private const string CategoryKeyPrefix = "DebugActionCategory_";
    private static bool enabled;

    public static void SetEnabled(bool value)
    {
        if (enabled == value) return;
        enabled = value;
        ResetDebugActionCache();
    }

    private static void ResetDebugActionCache()
    {
        Dialog_Debug.rootNode = null;
        object? roots = AccessTools.Field(typeof(Dialog_Debug), "roots")?.GetValue(null);
        if (roots is System.Collections.IDictionary dictionary) dictionary.Clear();
    }

    private static void Prefix(MethodInfo method, DebugActionAttribute attribute, out (string? Name, string Category) __state)
    {
        __state = (attribute.name, attribute.category);
        if (!enabled) return;

        string actionKey = ActionKeyPrefix + method.Name;
        if (actionKey.TryTranslate(out TaggedString actionLabel)) attribute.name = actionLabel;

        if (!attribute.category.NullOrEmpty())
        {
            string categoryKey = CategoryKeyPrefix + attribute.category.Replace(" ", "");
            if (categoryKey.TryTranslate(out TaggedString categoryLabel)) attribute.category = categoryLabel;
        }
    }

    private static void Postfix(DebugActionAttribute attribute, (string? Name, string Category) __state)
    {
        attribute.name = __state.Name;
        attribute.category = __state.Category;
    }
}
