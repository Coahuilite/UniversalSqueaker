using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>Player vocabulary for the exact parameter IDs consumed by the response rules.
/// Unknown author parameters have no schema: retain their identity and defer units to the author.</summary>
internal readonly struct UsTuningFieldPresentation
{
    private readonly string label;
    public readonly string NoteKey, GroupKey;
    private readonly int order;
    private UsTuningFieldPresentation(string label, string note, string group, int order)
    {
        this.label = "US.B.Field." + label;
        NoteKey = "US.B.FieldNote." + note;
        GroupKey = "US.B.FieldGroup." + group;
        this.order = order;
    }
    private static readonly IReadOnlyDictionary<string, UsTuningFieldPresentation> Known = new Dictionary<string, UsTuningFieldPresentation>(StringComparer.Ordinal)
    {
        ["parameter:age.override"] = new("AgePitch", "Pitch", "Age", 0),
        ["parameter:age.overrideVolume"] = new("AgeVolume", "Volume", "Age", 1),
        ["participation:us.core/Age"] = new("AgeParticipation", "Participation", "Age", 2),
        ["parameter:mood.low"] = new("MoodLow", "MoodThreshold", "Mood", 10),
        ["parameter:mood.high"] = new("MoodHigh", "MoodThreshold", "Mood", 11),
        ["parameter:mood.low.pitch"] = new("MoodLowPitch", "Pitch", "Mood", 12),
        ["parameter:mood.low.volume"] = new("MoodLowVolume", "Volume", "Mood", 13),
        ["parameter:mood.neutral.pitch"] = new("MoodNeutralPitch", "Pitch", "Mood", 14),
        ["parameter:mood.neutral.volume"] = new("MoodNeutralVolume", "Volume", "Mood", 15),
        ["parameter:mood.high.pitch"] = new("MoodHighPitch", "Pitch", "Mood", 16),
        ["parameter:mood.high.volume"] = new("MoodHighVolume", "Volume", "Mood", 17),
        ["participation:us.core/Mood"] = new("MoodParticipation", "Participation", "Mood", 18),
        ["parameter:jitter.min"] = new("JitterMin", "Jitter", "Jitter", 20),
        ["parameter:jitter.max"] = new("JitterMax", "Jitter", "Jitter", 21),
    };
    public static UsTuningFieldPresentation For(string field) => Known.TryGetValue(field, out var value) ? value
        : field.StartsWith("response:", StringComparison.Ordinal) ? new("AuthorResponse", "Response", "Author", 30)
        : field.StartsWith("participation:", StringComparison.Ordinal) ? new("AuthorParticipation", "Participation", "Author", 31)
        : new("AuthorParameter", "AuthorParameter", "Author", 32);
    public static int Order(string field) => For(field).order;
    public string Title(string field, IUiTranslation translation)
    {
        string text = translation.Translate(label);
        // Registered author rules expose an ID and stage, but no translated name or parameter unit.
        return Known.ContainsKey(field) ? text : string.Format(text, field.Substring(field.IndexOf(':') + 1));
    }
}
