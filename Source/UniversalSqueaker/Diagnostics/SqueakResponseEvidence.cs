using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker;

/// <summary>Detached, bounded projection of already-settled native facts. Never a response owner.</summary>
internal sealed class SqueakResponseEvidence
{
    private readonly List<KeyValuePair<string, string>> fields = new();
    private readonly List<string> truncatedFields = new();
    private int detailCharacters, detailItems;
    internal string Stage { get; }
    internal long Sequence { get; }
    internal SessionObservation? Observation { get; }
    internal string Action { get; }
    internal string Result { get; }
    internal string Reason { get; }
    internal bool Interaction { get; }
    internal double? InteractionUntil { get; }
    internal int Truncated { get; private set; }
    internal IReadOnlyList<KeyValuePair<string, string>> Fields => fields;

    internal SqueakResponseEvidence(string stage, long sequence = 0, SessionObservation? observation = null,
        string? action = null, string? result = null, string? reason = null, bool interaction = false, double? until = null)
    {
        Stage = stage; Sequence = sequence;
        if (observation.HasValue)
        {
            var saved = observation.Value;
            Observation = new SessionObservation(ObservedToken(saved.Action), ObservedToken(saved.Reason), saved.Receipt.HasValue
                ? new PlaybackReceipt(saved.Receipt.Value.Result, ObservedToken(saved.Receipt.Value.Reason)) : (PlaybackReceipt?)null,
                saved.ObservedTick, saved.SharedDeadline, saved.ActionDeadline, saved.NextStateTick, saved.ConfirmedTick,
                saved.ChecksShared, ObservedToken(saved.ActiveAction));
        }
        Action = Bounded(action); Result = Bounded(result); Reason = Bounded(reason);
        Interaction = interaction; InteractionUntil = until;
        Add("stage", stage); Add("sequence", sequence);
        Add("window_w_details", "NotCaptured");
        Add("sustained_slot_details", "NotCaptured");
    }

    internal void Add(string key, object? value)
    {
        string text = value == null ? "Unavailable" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "Unavailable";
        string safe = Safe(text);
        if (safe.Length > 256) { safe = safe.Substring(0, 256); NoteTruncation(key); }
        if (key.Length > 256) { NoteTruncation(key); key = key.Substring(0, 256); }
        fields.Add(new KeyValuePair<string, string>(key, safe));
    }

    internal void Detail(string key, object? value)
    {
        string text = Safe(Convert.ToString(value, CultureInfo.InvariantCulture));
        if (detailItems >= 16 || detailCharacters + text.Length > 4096) { NoteTruncation(key); return; }
        detailItems++; detailCharacters += text.Length;
        Add(key, text);
    }

    // Only protocol identities / scalar tokens enter feedback. Free prose is deliberately not exported.
    internal static string Safe(string? value)
    {
        if (value == null || value.Length == 0) return "Unavailable";
        if (value.StartsWith("/", StringComparison.Ordinal) || value.IndexOf('\\') >= 0
            || value.Length > 2 && char.IsLetter(value[0]) && value[1] == ':' && value[2] == '/') return "Redacted";
        foreach (char c in value)
            if (!char.IsLetterOrDigit(c) && "._:/+-,=;|@[]()<>?".IndexOf(c) < 0) return "Redacted";
        return value;
    }

    private static string Bounded(string? value)
    {
        string safe = Safe(value);
        return safe.Length > 256 ? safe.Substring(0, 256) : safe;
    }
    private static string ObservedToken(string? value) => value == null || value.Length == 0 ? "" : Bounded(value);

    private void NoteTruncation(string key)
    {
        Truncated++;
        if (truncatedFields.Count < 16) truncatedFields.Add(Bounded(key));
    }

    internal string Text()
    {
        var text = new StringBuilder();
        foreach (var field in fields) text.Append(field.Key).Append('=').Append(field.Value).Append('\n');
        foreach (string field in truncatedFields) text.Append("truncated_field=").Append(field).Append('\n');
        text.Append("fields_truncated=").Append(Truncated).Append(" truncation_names_omitted=").Append(Truncated - truncatedFields.Count);
        return text.ToString();
    }
}
