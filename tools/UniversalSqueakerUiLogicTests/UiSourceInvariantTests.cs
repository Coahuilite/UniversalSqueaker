using System;
using System.IO;

namespace UniversalSqueaker.UiLogicTests;

/// <summary>
/// Zero-Verse source-level invariants for the G2 “unusable controls” and G4 “visual language” batches.
/// These widgets depend on Verse/Unity at runtime, so the UI logic gate cannot instantiate them;
/// instead it asserts that each previously-invisible control now has an explicit visible draw
/// surface + label, still registers the deferred UiInteract button, and routes switching/selection
/// visuals through the neutral UiKit SelectionButton helper. This is intentionally a
/// narrow structural guard: it fails if the helper or its required draw/registration lines disappear.
/// </summary>
internal static class UiSourceInvariantTests
{
    public static void RunAll()
    {
        string root = RepoRoot();

        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Components", "VoicePackChecklist.cs"),
            new[]
            {
                "private static void DrawForgetButton",
                "SelectionButton.Draw(button, \"Forget Unavailable\"",
                "UiInteract.Button(button"
            },
            "Forget Unavailable must draw through the neutral SelectionButton helper and keep its UiInteract button");

        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "PresetListWidget.cs"),
            new[]
            {
                "private static void DrawImportButton",
                "SelectionButton.Draw(button, \"Import\"",
                "UiInteract.Button(button"
            },
            "Preset Import must draw through the neutral SelectionButton helper and keep its UiInteract button");

        string scopeTree = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "ScopeTreeWidget.cs");
        CheckSourceContains(
            scopeTree,
            new[]
            {
                "private static void DrawAutoClearButton",
                "SelectionButton.Draw(clearRect",
                "UiInteract.Button(clearRect"
            },
            "Mood Auto must draw through the neutral SelectionButton helper and keep its UiInteract button");
        CheckSourceCountAtLeast(
            scopeTree,
            "DrawAutoClearButton(clearRect, row, race, xeno, kitEmit);",
            2,
            "Mood Auto must be drawn both in the narrow-screen branch and in the normal-width branch");

        // G4 visual-language regression guards: US switching/selecting controls must route through
        // the neutral UiKit SelectionButton helper rather than the old UsSurface.DrawSegment style.
        string[] g4Widgets =
        {
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "ScopeTreeWidget.cs"),
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "FilterBarWidget.cs"),
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "AttenuationEditorWidget.cs"),
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "PresetListWidget.cs")
        };
        foreach (string file in g4Widgets)
        {
            CheckSourceDoesNotContain(file, "UsSurface.DrawSegment(",
                "G4 widgets must not use the old UsSurface.DrawSegment custom segment style");
        }

        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "ScopeTreeWidget.cs"),
            new[]
            {
                "SelectionButton.Draw(buttonRect, LayerNames[i], selected, font: UiFont.Tiny);"
            },
            "Tuning layer segments must draw through SelectionButton");

        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "FilterBarWidget.cs"),
            new[]
            {
                "SelectionButton.Draw(rect, label, selected, font: UiFont.Tiny);"
            },
            "Filter bar chips/segments must draw through SelectionButton");

        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "AttenuationEditorWidget.cs"),
            new[]
            {
                "SelectionButton.Draw(rect, label, selected, font: UiFont.Tiny);",
                "IsCurrentPreset(currentPreset, SqueakDistancePreset."
            },
            "Attenuation preset buttons must draw through SelectionButton and show the current preset");

        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "PresetListWidget.cs"),
            new[]
            {
                "SelectionButton.Draw(button, \"Import\""
            },
            "Preset Import must use the neutral SelectionButton primary style");

        CheckSourceContains(
            Path.Combine(root, "Source", "FerriteLib.UiKit", "Widgets", "DropdownWidget.cs"),
            new[]
            {
                "SelectionButton.DrawField(rect, display, selected, font: UiFont.Small);"
            },
            "Dropdown trigger fields must use the SelectionButton field helper");

        CheckSourceContains(
            Path.Combine(root, "Source", "FerriteLib.UiKit", "Widgets", "ModeCardRenderer.cs"),
            new[]
            {
                "SelectionButton.DrawSurface("
            },
            "ModeCardRenderer must use the neutral SelectionButton surface/accent helper");

        // G1 text-height regression guards: the two-line/triple-line widgets must use the pure
        // measured-height functions instead of bare hard-coded row heights.
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "BasicTuningWidget.cs"),
            new[]
            {
                "VoicePacksLayout.TwoLineRowHeight"
            },
            "BasicTuningWidget must measure two-line rows through VoicePacksLayout.TwoLineRowHeight");

        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Components", "VoicePackChecklist.cs"),
            new[]
            {
                "VoicePacksLayout.VoicePackRowHeightFor"
            },
            "VoicePackChecklist must size rows through the dynamic VoicePackLayout height");

        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Components", "VoicePackRow.cs"),
            new[]
            {
                "metrics.CalcHeight"
            },
            "VoicePackRow must lay out its three text lines from measured line heights");

        // T5 M1: Tuning layer sticky row must exist and be driven by the active scroll view.
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "ScopeTreeWidget.cs"),
            new[]
            {
                "private static void DrawStickyLayerRowIfNeeded",
                "UiInteract.TryGetCurrentScrollView",
                "DrawLayerRow(pinnedRect, layer, emit)"
            },
            "ScopeTree must draw a pinned Tuning layer row when its normal row scrolls above the viewport");

        // T5 M2: action scopes are grouped and Biotech defensive actions are hidden by default.
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "ScopeTreeWidget.cs"),
            new[]
            {
                "AutonomousHeaderText",
                "OperableHeaderText",
                "row.Group != targetGroup"
            },
            "ScopeTree must render autonomous and operable action groups");
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Model", "VoicePacksPageModel.cs"),
            new[]
            {
                "ActionScopeRules.IsHiddenByDefault(action)",
                "ActionScopeRules.GroupFor(definition)"
            },
            "BuildActionScopes must filter hidden defensive actions and attach the action group");

        // T5 low-risk: pack page race/xenotype parallel filter and author dropdown.
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "FilterBarWidget.cs"),
            new[]
            {
                "private static void DrawFilterDropdownRow",
                "private static void DrawAuthorDropdown",
                "DrawFilterDropdown("
            },
            "FilterBar must draw Race/Xenotype/Author dropdowns");
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Model", "VoicePacksPageModel.cs"),
            new[]
            {
                "VoicePacksFilters.RaceFilterMatches",
                "VoicePacksFilters.XenotypeFilterMatches"
            },
            "BuildView must apply race/xenotype filters through the pure filter predicates");

        // T5 low-risk: zero-candidate xenotype rows are dimmed and labeled "No available packs".
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Components", "XenotypeLayerRow.cs"),
            new[]
            {
                "bool dimmed = domain.CandidateCount <= 0",
                "VoicePacksLayout.LayerDetailText(domain.EnabledCount, domain.CandidateCount",
                "Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.35f))"
            },
            "XenotypeLayerRow must dim and relabel rows with no available packs");

        // T5 low-risk: chart control points expose a neutral hover highlight.
        CheckSourceContains(
            Path.Combine(root, "Source", "FerriteLib.UiKit", "Widgets", "LineChartWidget.cs"),
            new[]
            {
                "internal static int? FindHoveredPoint",
                "HoverPointSize"
            },
            "LineChartWidget must provide a hover highlight for draggable control points");

        // T5 low-risk: Tuning editor height coordination uses consistent 24px+ interactive heights.
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Widgets", "ScopeTreeWidget.cs"),
            new[]
            {
                "private const float ButtonHeight = 24f;",
                "private const float DomainRowHeight = 28f;",
                "private const float MoodRowHeight = 32f;"
            },
            "Tuning editor interactive heights must stay coordinated (no 20px buttons / cramped mood rows)");
    }

    private static void CheckSourceContains(string path, string[] requiredFragments, string message)
    {
        string text = File.ReadAllText(path);
        foreach (string fragment in requiredFragments)
        {
            if (text.IndexOf(fragment, StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException(
                    message + "; missing fragment: " + fragment + " in " + path);
            }
        }
    }

    private static void CheckSourceCountAtLeast(string path, string fragment, int minimum, string message)
    {
        string text = File.ReadAllText(path);
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += fragment.Length;
        }

        if (count < minimum)
        {
            throw new InvalidOperationException(
                message + "; expected at least " + minimum + " occurrence(s) of '" + fragment
                + "', found " + count + " in " + path);
        }
    }

    private static void CheckSourceDoesNotContain(string path, string fragment, string message)
    {
        string text = File.ReadAllText(path);
        if (text.IndexOf(fragment, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                message + "; forbidden fragment: " + fragment + " in " + path);
        }
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "verify-local.ps1")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate repository root from " + AppDomain.CurrentDomain.BaseDirectory);
    }
}
