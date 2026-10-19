using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalSqueaker.OneShot;

public enum ResponseStage { Age, Mood }

public readonly struct ResponseContribution
{
    public readonly double Pitch;
    public readonly double Volume;
    public ResponseContribution(double pitch, double volume) { Pitch = pitch; Volume = volume; }
}

/// <summary>One immutable numerical context shared by all rules; no cumulative output is exposed.</summary>
public sealed class ResponseContext
{
    public readonly string Subject;
    private readonly Dictionary<string, double> values;
    public ResponseContext(string subject, IEnumerable<KeyValuePair<string, double>> values)
    {
        Subject = subject;
        this.values = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
    }
    public bool TryRead(string key, out double value) => values.TryGetValue(key, out value);
    public double Require(string key)
    {
        if (!values.TryGetValue(key, out double value)) throw new InvalidOperationException("MissingResponseInput:" + key);
        return value;
    }
    public double Optional(string key, double identity) => values.TryGetValue(key, out double value) ? value : identity;
}

public interface IUsResponseRule
{
    string Key { get; }
    ResponseStage Stage { get; }
    ResponseContribution Evaluate(ResponseContext context);
}

public sealed class NativeAgeResponse : IUsResponseRule
{
    public string Key => "us.core/Age";
    public ResponseStage Stage => ResponseStage.Age;
    public ResponseContribution Evaluate(ResponseContext context)
    {
        double pitch = context.TryRead("age.override", out double explicitPitch) ? explicitPitch : context.Require("age.native");
        return new ResponseContribution(pitch, 1);
    }
}

public sealed class MoodResponse : IUsResponseRule
{
    public string Key => "us.core/Mood";
    public ResponseStage Stage => ResponseStage.Mood;
    public ResponseContribution Evaluate(ResponseContext context)
    {
        double level = context.Require("mood.level");
        double low = context.Require("mood.low");
        double high = context.Require("mood.high");
        if (double.IsNaN(level) || double.IsNaN(low) || double.IsNaN(high)
            || low < 0 || high > 1 || low >= high || level < 0 || level > 1)
        {
            throw new InvalidOperationException("InvalidMoodThresholds");
        }
        string band = level < low ? "low" : level > high ? "high" : "neutral";
        return new ResponseContribution(context.Optional("mood." + band + ".pitch", 1), context.Optional("mood." + band + ".volume", 1));
    }
}

public sealed class ModulationPlan
{
    public ResponseContext Context = new("", Array.Empty<KeyValuePair<string, double>>());
    public IUsResponseRule[] Rules = { new NativeAgeResponse(), new MoodResponse() };
    public string[] EnabledRules = Array.Empty<string>();
    public IReadOnlyDictionary<string, bool> PlayerParticipation = new Dictionary<string, bool>();
    public double JitterMin = 1;
    public double JitterMax = 1;
}

public readonly struct LocalParameters
{
    public readonly bool Valid;
    public readonly double Pitch;
    public readonly double Volume;
    public readonly string Reason;
    public LocalParameters(bool valid, double pitch, double volume, string reason = "")
    {
        Valid = valid;
        Pitch = pitch;
        Volume = volume;
        Reason = reason;
    }
}

public static class ResponseModulation
{
    public static LocalParameters Compose(double calibrationPitch, double calibrationVolume, ModulationPlan plan,
        IReadOnlyDictionary<string, bool> entryParticipation, IOneShotRandom random)
    {
        double pitch = calibrationPitch;
        double volume = calibrationVolume;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rules = plan.Rules.GroupBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        var selected = new List<IUsResponseRule>();
        foreach (string key in plan.EnabledRules)
        {
            if (!seen.Add(key)) continue;
            bool participates = plan.PlayerParticipation.TryGetValue(key, out bool player) ? player
                : !entryParticipation.TryGetValue(key, out bool entry) || entry;
            if (!participates) continue;
            if (!rules.TryGetValue(key, out var implementations) || implementations.Length != 1)
                return new LocalParameters(false, 0, 0, "MissingOrDuplicateResponse:" + key);
            selected.Add(implementations[0]);
        }
        foreach (var rule in selected.OrderBy(x => x.Stage).ThenBy(x => x.Key, StringComparer.Ordinal))
        {
            try
            {
                var contribution = rule.Evaluate(plan.Context);
                if (!OneShotOutput.TryPrepare(contribution.Volume, contribution.Pitch, 1, out _, out _))
                    return new LocalParameters(false, 0, 0, "InvalidResponse:" + rule.Key);
                pitch *= contribution.Pitch;
                volume *= contribution.Volume;
            }
            catch (Exception error)
            {
                return new LocalParameters(false, 0, 0, "ResponseFault:" + rule.Key + ":" + error.Message);
            }
        }
        if (double.IsNaN(plan.JitterMin) || double.IsNaN(plan.JitterMax) || plan.JitterMin <= 0 || plan.JitterMax < plan.JitterMin)
            return new LocalParameters(false, 0, 0, "InvalidJitter");
        double jitter = plan.JitterMin;
        if (plan.JitterMin != plan.JitterMax)
        {
            double roll = random.Next(RandomPurpose.ModulationJitter);
            if (double.IsNaN(roll) || roll < 0 || roll >= 1) return new LocalParameters(false, 0, 0, "InvalidJitterRoll");
            jitter += (plan.JitterMax - plan.JitterMin) * roll;
        }
        pitch *= jitter;
        return OneShotOutput.TryPrepare(volume, pitch, 1, out _, out _)
            ? new LocalParameters(true, pitch, volume)
            : new LocalParameters(false, 0, 0, "InvalidLocalParameters");
    }
}
