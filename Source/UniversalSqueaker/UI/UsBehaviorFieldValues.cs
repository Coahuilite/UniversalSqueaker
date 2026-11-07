using System;
using System.Globalization;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

// Shared sparse field interpretation for the game and isolated author source. No persistence owner.
internal static class UsBehaviorFieldValues
{
    internal static object? Read(BehaviorValues v, string field) => field switch
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
    internal static string Format(object? value) => value is IFormattable number
        ? number.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "";

    // Mutates only the caller's candidate copy. The caller decides whether/how to commit it.
    internal static bool TryWrite(BehaviorValues value, string field, string text)
        => TryWrite(value, field, text, out _);

    internal static bool TryWrite(BehaviorValues value, string field, string text, out bool invalid)
    {
        invalid = true;
        text = text.Trim();
        bool? boolean = null;
        long? ticks = null;
        double? scalar = null;
        if (text != "")
        {
            if (field == "interval") { if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)) return false; ticks = parsed; }
            else if (field == "volume" || field == "pitch" || field == "interaction-seconds" || field.StartsWith("parameter:", StringComparison.Ordinal))
            { if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return false; scalar = parsed; }
            else { if (!bool.TryParse(text, out bool parsed)) return false; boolean = parsed; }
        }
        switch (field)
        {
            case "enabled": value.Enabled = boolean; break;
            case "interval": value.SimulationIntervalTicks = ticks; break;
            case "interaction-seconds": value.InteractionIntervalSeconds = scalar; break;
            case "volume": value.Volume = scalar; break;
            case "pitch": value.Pitch = scalar; break;
            case "talking": value.TalkingEnabled = boolean; break;
            case "age": value.AgeEnabled = boolean; break;
            case "mood": value.MoodEnabled = boolean; break;
            default:
                if (field.StartsWith("parameter:", StringComparison.Ordinal)) { string key = field.Substring(10); if (scalar.HasValue) value.ResponseParameters[key] = scalar.Value; else value.ResponseParameters.Remove(key); }
                else if (field.StartsWith("response:", StringComparison.Ordinal)) { string key = field.Substring(9); if (boolean.HasValue) value.ResponseEnabled[key] = boolean.Value; else value.ResponseEnabled.Remove(key); }
                else if (field.StartsWith("participation:", StringComparison.Ordinal)) { string key = field.Substring(14); if (boolean.HasValue) value.Participation[key] = boolean.Value; else value.Participation.Remove(key); }
                else { invalid = false; return false; }
                break;
        }
        return value.Valid;
    }

    internal static string Effective(string field, EffectiveBehavior effective, BehaviorValues values, BehaviorValues own, IUiTranslation t, bool compact)
    {
        var value = Read(values, field);
        string originKey = field switch { "interval" => "simulationIntervalTicks", "interaction-seconds" => "interactionIntervalSeconds",
            "talking" => "talkingEnabled", "age" => "ageEnabled", "mood" => "moodEnabled", _ => field };
        string origin = effective.Origins.TryGetValue(originKey, out var actual) ? actual : t.Translate("US.B.NotSupplied");
        string display = value is bool boolean ? t.Translate(boolean ? "US.UI.Yes" : "US.UI.No") : Format(value);
        return "→ " + (display == "" ? t.Translate("US.B.NotSupplied") : display)
            + (field == "interval" ? " tick" : field == "interaction-seconds" ? " s" : field == "volume" || field == "pitch" ? " ×" : "")
            + (compact ? " · " + t.Translate(Read(own, field) == null ? "US.B.FieldInherited" : "US.B.FieldOwn") : "  ← " + origin)
            + (effective.Paused ? "\n" + effective.Failure : "");
    }
}
