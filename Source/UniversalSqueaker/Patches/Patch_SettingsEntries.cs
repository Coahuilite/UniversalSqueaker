using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace UniversalSqueaker;

/// <summary>Only the native entry rows; the panel itself is a single FL-owned settings window.</summary>
[HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.DoMainMenuControls))]
internal static class Patch_USMainMenuEntry
{
    private static readonly MethodInfo Listing = AccessTools.Method(typeof(OptionListingUtility),
        nameof(OptionListingUtility.DrawOptionListing), new[] { typeof(Rect), typeof(List<ListableOption>) });
    private static readonly MethodInfo WithEntry = AccessTools.Method(typeof(Patch_USMainMenuEntry), nameof(DrawPlayOptions));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var sites = code.Where(i => i.Calls(Listing)).ToArray();
        // Target 1.6: the play list then the separate web-links list. Preserve all original IL on drift.
        if (sites.Length != 2)
        {
            Log.Warning("usdiag UI=MainMenuEntrySeamUnavailable; original menu retained; use Mod Settings");
            return code;
        }
        sites[0].opcode = OpCodes.Call;
        sites[0].operand = WithEntry;
        return code;
    }

    private static float DrawPlayOptions(Rect rect, List<ListableOption> options)
    {
        if (Current.ProgramState == ProgramState.Entry && SqueakerMod.Instance != null && SqueakerMod.Settings.showMainMenuEntry)
            options.Insert(Math.Max(0, options.Count - 1), new ListableOption("US.UI.Entry".Translate(), () => SqueakerMod.Instance?.OpenSettings()));
        return OptionListingUtility.DrawOptionListing(rect, options);
    }
}

// No OptionCategoryDef: vanilla rejects unofficial category definitions. Add one local row without
// replacing the category loop, its selected category, the options body, OK or another mod's entries.
[HarmonyPatch(typeof(Dialog_Options), nameof(Dialog_Options.DoWindowContents))]
internal static class Patch_USOptionsEntry
{
    private static readonly FieldInfo? HasModSettings = AccessTools.Field(typeof(Dialog_Options), "hasModSettings");
    private static void Postfix(Dialog_Options __instance, Rect inRect)
    {
        if (SqueakerMod.Instance == null || !SqueakerMod.Settings.showOptionsEntry || HasModSettings == null) return;
        bool hasMods = (bool)HasModSettings.GetValue(__instance);
        int rows = DefDatabase<OptionCategoryDef>.AllDefs.Count(category =>
            (Prefs.DevMode || !category.isDev) && category.modContentPack.IsOfficialMod
            && (category != OptionCategoryDefOf.Mods || hasMods));
        var rect = new Rect(4, rows * 50f + 4, 152, 40);
        if (rect.yMax > inRect.height - 50) return;
        // Use the same category background/hit primitive as Dialog_Options.DoCategoryRow,
        // not its brown command-button chrome. This row opens a child, never selects a fake Def.
        Color color = GUI.color;
        bool enabled = GUI.enabled;
        GameFont font = Text.Font;
        TextAnchor anchor = Text.Anchor;
        try
        {
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawOptionBackground(rect, Find.WindowStack.Windows.Any(window => window is UI.GameSettingsWindow));
            if (Widgets.ButtonInvisible(rect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                SqueakerMod.Instance.OpenSettings();
            }
            Widgets.Label(new Rect(rect.x + 10f, rect.y, rect.width - 20f, rect.height), "US.UI.Entry".Translate());
        }
        finally { GUI.color = color; GUI.enabled = enabled; Text.Font = font; Text.Anchor = anchor; }
    }
}
