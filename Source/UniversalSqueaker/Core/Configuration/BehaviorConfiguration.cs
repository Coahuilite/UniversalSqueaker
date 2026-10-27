using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace UniversalSqueaker;

/// <summary>Neutral captured player row; empty Target denotes a service profile, never a sound pool.</summary>
public sealed class BehaviorPlayerOverride
{
    public readonly string Family, Namespace, Target, Child, Action;
    private readonly BehaviorValues values;
    public readonly bool ValidScope;
    public BehaviorPlayerOverride(string family, string targetNamespace, string target, string child, string action, BehaviorValues values, bool validScope = true)
    {
        Family = family; Namespace = targetNamespace; Target = target; Child = child; Action = action;
        this.values = values.Copy(); ValidScope = validScope;
    }
    public int Layer => Target == "" ? 0 : Child == "" ? 1 : 2;
    public bool Applies(BehaviorDomain domain) => Family == domain.Family && Namespace == domain.Namespace
        && (Target == "" || Target == domain.Target && (Child == "" || Child == domain.Child));
    public string Origin => "player:" + Family + ":" + Namespace + (Target == "" ? ":profile" : ":" + Target + ":" + Child);
    public BehaviorValues Read() => values.Copy();
}

/// <summary>Values/attribution for one action. Accessors never expose the captured mutable contribution.</summary>
public sealed class EffectiveBehavior
{
    private readonly BehaviorValues values;
    public readonly bool Paused;
    public readonly string Failure;
    public readonly IReadOnlyDictionary<string, string> Origins;
    public readonly IReadOnlyList<string> Dependencies;
    public EffectiveBehavior(BehaviorValues values, bool paused, string failure, IDictionary<string, string> origins, IEnumerable<string> dependencies)
    {
        this.values = values.Copy(); Paused = paused; Failure = failure;
        Origins = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(origins, StringComparer.Ordinal));
        Dependencies = Array.AsReadOnly(dependencies.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
    public BehaviorValues Read() => values.Copy();
}

/// <summary>A bounded decision snapshot: authors first, all player layers after them; system controls stay separate.</summary>
public sealed class BehaviorConfiguration
{
    public readonly long Id;
    public readonly bool SystemEnabled;
    public readonly double Activity, SystemVolume;
    public readonly Kernel.SelectionMode SourceMode;
    public readonly bool AllowEggs, TalkingScaling;
    private readonly Dictionary<Kernel.AudioDomain, HashSet<string>>? selectedPacks;
    public bool IsPackSelected(Kernel.AudioDomain domain, string key) => selectedPacks == null
        || selectedPacks.TryGetValue(domain, out var keys) && keys.Contains(key);
    private readonly BehaviorSourceState? sources;
    private readonly BehaviorPlayerOverride[] players;
    private readonly HashSet<Kernel.AudioDomain> configuredDomains = new();
    public bool HasDomain(Kernel.AudioDomain domain) => configuredDomains.Contains(domain)
        || selectedPacks != null && selectedPacks.ContainsKey(domain);
    private readonly string failure = "";
    public BehaviorConfiguration(string sourcesXml, IEnumerable<BehaviorPlayerOverride> players, bool enabled, double activity, double systemVolume,
        Kernel.SelectionMode sourceMode = Kernel.SelectionMode.Fallback, IEnumerable<Kernel.AudioPackSelection>? selection = null,
        bool allowEggs = false, bool talkingScaling = true, long revision = 0)
    {
        Id = revision;
        this.players = players.Select(x => new BehaviorPlayerOverride(x.Family, x.Namespace, x.Target, x.Child, x.Action, x.Read(), x.ValidScope)).ToArray();
        SystemEnabled = enabled; Activity = activity; SystemVolume = systemVolume;
        SourceMode = sourceMode; AllowEggs = allowEggs; TalkingScaling = talkingScaling;
        if (selection != null)
        {
            selectedPacks = new Dictionary<Kernel.AudioDomain, HashSet<string>>();
            foreach (var row in selection) selectedPacks[row.Domain] = new HashSet<string>(row.Keys, StringComparer.Ordinal);
        }
        try
        {
            if (double.IsNaN(activity) || double.IsInfinity(activity) || activity <= 0 || double.IsNaN(systemVolume)
                || double.IsInfinity(systemVolume) || systemVolume < 0 || systemVolume > 1) throw new ArgumentException("InvalidSystemProfile");
            sources = BehaviorSourceState.FromXml(sourcesXml);
            foreach (var binding in sources.Bindings) configuredDomains.Add(binding.Domain.Identity);
            foreach (var row in this.players.Where(x => x.ValidScope && x.Target != ""))
                configuredDomains.Add(new BehaviorDomain(row.Family, row.Namespace, row.Target, row.Child).Identity);
        }
        catch (Exception error) { failure = "UnreadableBehaviorConfig:" + error.GetType().Name + ":" + error.Message; }
    }
    public EffectiveBehavior Resolve(BehaviorDomain domain, string action, BehaviorValues defaults, BehaviorSourceDirectory directory,
        Func<string, bool> implementationAvailable, IEnumerable<string>? fixedFacts = null)
        => ResolveCore(domain, domain.Family, domain.Namespace, action, defaults, directory, implementationAvailable, fixedFacts);
    public EffectiveBehavior ResolveProfile(string family, string targetNamespace, string action, BehaviorValues defaults, BehaviorSourceDirectory directory,
        Func<string, bool> implementationAvailable, IEnumerable<string>? fixedFacts = null)
        => ResolveCore(null, family, targetNamespace, action, defaults, directory, implementationAvailable, fixedFacts);
    private EffectiveBehavior ResolveCore(BehaviorDomain? domain, string family, string targetNamespace, string action,
        BehaviorValues defaults, BehaviorSourceDirectory directory, Func<string, bool> implementationAvailable, IEnumerable<string>? fixedFacts)
    {
        var origins = new Dictionary<string, string>(StringComparer.Ordinal);
        var dependencies = new HashSet<string>(StringComparer.Ordinal);
        if (fixedFacts != null) foreach (string fact in fixedFacts) dependencies.Add(fact);
        string problem = failure;
        bool paused = failure != "";
        var result = new BehaviorValues();
        try
        {
            if (!paused)
            {
                var layers = players.Where(x => x.Action == action && (domain != null ? x.Applies(domain)
                    : x.Target == "" && x.Family == family && x.Namespace == targetNamespace)).OrderBy(x => x.Layer).ToArray();
                if (layers.Any(x => !x.ValidScope)) throw new ArgumentException("InvalidPlayerScope");
                if (domain != null)
                    result = sources!.Resolve(domain, action, directory, layers.Select(x => x.Read()), implementationAvailable,
                        out paused, reason => problem = reason, origins, dependencies);
                else
                    for (int i = 0; i < layers.Length; i++)
                    {
                        var values = layers[i].Read(); result.Overlay(values);
                        foreach (string key in values.PresentKeys()) origins[key] = "player:" + i;
                    }
                foreach (string key in origins.Keys.ToArray())
                    if (origins[key].StartsWith("player:", StringComparison.Ordinal))
                        origins[key] = layers[int.Parse(origins[key].Substring(7), System.Globalization.CultureInfo.InvariantCulture)].Origin;
            }
            // Fill absent fields only after every sparse contribution has been folded.
            var completed = defaults.Copy(); completed.Overlay(result); result = completed;
            foreach (string key in defaults.PresentKeys()) if (!origins.ContainsKey(key)) origins[key] = "package:" + action;
            if (result.UnsupportedTiming != "") throw new ArgumentException(result.UnsupportedTiming);
            if (!result.Valid || !result.ApplicableTo(family)) throw new ArgumentException("InvalidEffectiveBehavior");
            if (result.Phase == EatPhase.Execution) dependencies.Add(EatPolicy.Execution);
            if (result.NutritionScope.HasValue && result.NutritionScope != EatNutritionScope.Any) dependencies.Add(EatPolicy.Nutrition);
            if (result.DrugScope.HasValue && result.DrugScope != EatDrugScope.Any) dependencies.Add(EatPolicy.Drug);
            if (result.SelectRequirePlayerControlled == true) dependencies.Add("us.core/PlayerControlled");
            if (result.SelectRequireAwake == true) dependencies.Add("us.core/Awake");
            if (result.SelectRequireNotDowned == true) dependencies.Add("us.core/NotDowned");
            foreach (string required in dependencies)
                if (!implementationAvailable(required)) { paused = true; problem = "MissingRequiredImplementation:" + required; }
        }
        catch (Exception error) { paused = true; problem = error.GetType().Name + ":" + error.Message; }
        return new EffectiveBehavior(result, paused, problem, origins, dependencies);
    }
}
