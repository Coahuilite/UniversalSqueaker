using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker.UI;

public sealed partial class UsKernelSettingsSource
{
    public bool PublicationPending => SqueakerMod.Instance?.SettingsPublicationPending == true;
    private string observedSaveState = "";
    internal void SynchronizeSaveState()
    {
        string current = SaveStatus + ":" + SaveStatusVisible + ":" + IsDirty + ":" + PublicationPending;
        if (current == observedSaveState) return;
        observedSaveState = current; host?.Invalidate();
    }
    private readonly Dictionary<string, string> rowResults = new(StringComparer.Ordinal);
    private static string RowIdentity(string action, string race, string child) => action + "\n" + race + "\n" + child;
    public string RowResult(string action, string race, string child) => rowResults.TryGetValue(RowIdentity(action, race, child), out var result) ? result : "";
    private static object? FieldValue(BehaviorValues v, string field) => field switch
    {
        "enabled" => v.Enabled, "scope" => v.Scope, "interval" => v.SimulationIntervalTicks,
        "interaction-seconds" => v.InteractionIntervalSeconds, "volume" => v.Volume, "pitch" => v.Pitch,
        "talking" => v.TalkingEnabled, "age" => v.AgeEnabled, "mood" => v.MoodEnabled,
        "phase" => v.Phase, "nutritionScope" => v.NutritionScope, "drugScope" => v.DrugScope,
        "selectRequirePlayerControlled" => v.SelectRequirePlayerControlled, "selectRequireAwake" => v.SelectRequireAwake,
        "selectRequireNotDowned" => v.SelectRequireNotDowned,
        _ => field.StartsWith("parameter:", StringComparison.Ordinal) && v.ResponseParameters.TryGetValue(field.Substring(10), out var number) ? number
            : field.StartsWith("response:", StringComparison.Ordinal) && v.ResponseEnabled.TryGetValue(field.Substring(9), out var enabled) ? enabled
            : field.StartsWith("participation:", StringComparison.Ordinal) && v.Participation.TryGetValue(field.Substring(14), out var participates) ? participates : null
    };
    private static string FormatField(object? value) => value is IFormattable number ? number.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "";
    public string EffectiveField(string field, string action, string race, string child, IUiTranslation t)
    {
        if (!SqueakerRuntime.Definitions.Actions.Any(x => x.Key == action)) return t.Translate("US.B.Unknown");
        var effective = SqueakerRuntime.Explain(settings.CaptureBehaviorConfiguration(), action, race, child);
        var value = FieldValue(effective.Read(), field);
        string originKey = field switch { "interval" => "simulationIntervalTicks", "interaction-seconds" => "interactionIntervalSeconds",
            "talking" => "talkingEnabled", "age" => "ageEnabled", "mood" => "moodEnabled", _ => field };
        string origin = effective.Origins.TryGetValue(originKey, out var actual) ? actual : t.Translate("US.B.NotSupplied");
        string display = value is bool boolean ? t.Translate(boolean ? "US.UI.Yes" : "US.UI.No") : FormatField(value);
        return "→ " + (display == "" ? t.Translate("US.B.NotSupplied") : display)
            + (field == "interval" ? " tick" : field == "interaction-seconds" ? " s" : field == "volume" || field == "pitch" ? " ×" : "")
            + "  ← " + origin + (effective.Paused ? "\n" + effective.Failure : "");
    }
    public IReadOnlyList<string> ContributionFields(string action, string race, string child)
    {
        if (!SqueakerRuntime.Definitions.Actions.Any(x => x.Key == action)) return Array.Empty<string>();
        var effective = SqueakerRuntime.Explain(settings.CaptureBehaviorConfiguration(), action, race, child).Read();
        var own = settings.ReadPlayerActionOverride(action, race, child);
        // No response parameter schema is exposed by IUsResponseRule: show actual folded/own keys.
        return Array.AsReadOnly(effective.PresentKeys().Concat(own.PresentKeys())
            .Where(x => x.StartsWith("parameter:", StringComparison.Ordinal) || x.StartsWith("response:", StringComparison.Ordinal) || x.StartsWith("participation:", StringComparison.Ordinal))
            .Concat(SqueakerRuntime.Definitions.Responses.Where(r => r.Key != "us.core/Age" && r.Key != "us.core/Mood").SelectMany(r => new[] { "response:" + r.Key, "participation:" + r.Key }))
            .Concat(new[] { "age.override", "age.overrideVolume", "mood.low", "mood.high", "mood.low.pitch", "mood.low.volume", "mood.neutral.pitch", "mood.neutral.volume", "mood.high.pitch", "mood.high.volume", "jitter.min", "jitter.max" }.Select(x => "parameter:" + x))
            .Concat(new[] { "participation:us.core/Age", "participation:us.core/Mood" })
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
    public string ConfiguredPath(UsRosterTarget? target, IUiTranslation t)
    {
        string T(string key) => t.Translate("US.B." + key);
        if (target == null) return "";
        if (target.Loose) return T("Loose");
        var configuration = settings.CaptureBehaviorConfiguration();
        var exact = SqueakerRuntime.Audio.Packs.Where(p => !p.Domain.IsNonPawn && p.Domain.Race.DefName == target.Race
            && (p.Domain.Xenotype?.DefName ?? "") == target.Xeno).ToArray();
        var race = SqueakerRuntime.Audio.Packs.Where(p => !p.Domain.IsNonPawn && p.Domain.Race.DefName == target.Race && !p.Domain.Xenotype.HasValue).ToArray();
        string Regular(IEnumerable<VoicePackEntry> packs) => string.Join(", ", packs.Where(p => configuration.IsPackSelected(p.Domain, p.PackKey))
            .Select(p => p.PackKey + " [" + string.Join(", ", p.Actions.Keys) + "]"));
        string selectedFallback = string.Join(", ", exact.Where(p => configuration.IsPackSelected(p.Domain, p.PackKey) && p.PackFallback?.Count > 0)
            .Select(p => p.PackKey + " [" + string.Join(", ", p.PackFallback!.Keys) + "]"));
        var table = BuildView().FallbackRaces.Where(r => r.DefName == target.Race).ToArray();
        string Empty(string value) => value == "" ? T("NoneDeclared") : value;
        string mode = t.Translate("US.Tuning.Mode." + BuildView().Mode);
        string connector = BuildView().Mode == SqueakVoicePackMode.Remix ? " + " : " → ";
        var slots = new List<string>();
        if (target.Xeno != "") slots.Add(T("Exact") + ": " + Empty(Regular(exact)));
        slots.Add(T("RaceRegular") + ": " + Empty(Regular(race)));
        slots.Add(T("PackFallback") + ": " + Empty(selectedFallback));
        slots.Add(T("FinalTable") + ": " + (table.Length == 0 ? T("NoTable") : table[0].SoundCount + " " + T("Mapped")));
        string route = BuildView().Mode == SqueakVoicePackMode.Disabled ? T("DisabledPath")
            : BuildView().Mode == SqueakVoicePackMode.Vanilla ? T("VanillaPath") : T("ConfiguredOnly");
        return mode + " · " + route + "\n" + string.Join(connector + "\n", slots)
            + (target.Xeno == "" ? "" : "\n" + T("RaceRegularOnly"));
    }
}
