using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

internal static class LocalizedModMetadata
{
    public const string NameKey = "US.About.Name";
    public const string DescriptionKey = "US.About.Description";

    public static bool IsThisMod(ModMetaData meta)
    {
        return meta?.SamePackageId(UniversalSqueakerMod.PackageId, ignorePostfix: true) == true;
    }

    public static bool TryTranslateMetadata(string key, out TaggedString text)
    {
        text = default;
        return LanguageDatabase.activeLanguage != null && key.TryTranslate(out text);
    }
}

[HarmonyPatch(typeof(ModMetaData), nameof(ModMetaData.Name), MethodType.Getter)]
public static class Patch_ModMetaData_Name
{
    private static void Postfix(ModMetaData __instance, ref string __result)
    {
        if (LocalizedModMetadata.IsThisMod(__instance) && LocalizedModMetadata.TryTranslateMetadata(LocalizedModMetadata.NameKey, out TaggedString name))
        {
            __result = name;
        }
    }
}

[HarmonyPatch(typeof(ModMetaData), nameof(ModMetaData.Description), MethodType.Getter)]
public static class Patch_ModMetaData_Description
{
    private static void Postfix(ModMetaData __instance, ref string __result)
    {
        if (LocalizedModMetadata.IsThisMod(__instance) && LocalizedModMetadata.TryTranslateMetadata(LocalizedModMetadata.DescriptionKey, out TaggedString description))
        {
            __result = description;
        }
    }
}
