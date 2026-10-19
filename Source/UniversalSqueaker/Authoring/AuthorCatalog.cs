using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using UniversalSqueaker.OneShot;

namespace UniversalSqueaker.Authoring;

public sealed class VoiceEntry
{
    public string Key = "";
    public string Action = "";
    public string Sound = "";
    public double Weight = 1;
    public bool? Enabled;
    public double? Volume;
}

/// <summary>RimWorld calls this same custom field loader during normal Def deserialization.</summary>
public sealed class VoicePackDeclaration
{
    public int FormatVersion = 1;
    public string Race = "";
    public readonly List<VoiceEntry> Entries = new();
    public void LoadDataFromXmlCustom(XmlNode node)
    {
        AuthorXml.Fields(node, "formatVersion", "race", "entries");
        FormatVersion = AuthorXml.Version(node);
        Race = AuthorXml.Required(node, "race");
        Entries.Clear();
        foreach (XmlNode entry in AuthorXml.Elements(node.SelectSingleNode("entries")))
        {
            if (entry.Name != "li")
            {
                throw new ArgumentException("Unknown entries element " + entry.Name);
            }

            AuthorXml.Fields(entry, "key", "action", "sound", "weight", "enabled", "volume");
            var weightText = entry.SelectSingleNode("weight")?.InnerText;
            var enabledText = entry.SelectSingleNode("enabled")?.InnerText;
            var volumeText = entry.SelectSingleNode("volume")?.InnerText;
            Entries.Add(new VoiceEntry
            {
                Key = AuthorXml.Required(entry, "key"),
                Action = AuthorXml.Required(entry, "action"),
                Sound = AuthorXml.Required(entry, "sound"),
                Weight = weightText == null ? 1 : double.Parse(weightText, CultureInfo.InvariantCulture),
                Enabled = enabledText == null ? null : bool.Parse(enabledText),
                Volume = volumeText == null ? null : double.Parse(volumeText, CultureInfo.InvariantCulture)
            });
        }
    }
}

public sealed class ActionDeclaration
{
    public int FormatVersion = 1;
    public string Key = "";
    public string BaseAction = "";
    public Condition Condition = new("all");
    public void LoadDataFromXmlCustom(XmlNode node)
    {
        AuthorXml.Fields(node, "formatVersion", "key", "baseAction", "condition");
        FormatVersion = AuthorXml.Version(node);
        Key = AuthorXml.Required(node, "key");
        BaseAction = AuthorXml.Required(node, "baseAction");
        var roots = AuthorXml.Elements(node.SelectSingleNode("condition")).ToArray();
        if (roots.Length != 1)
        {
            throw new ArgumentException("condition requires exactly one root expression");
        }

        var root = roots[0];
        Condition = AuthorXml.Condition(root);
    }
}

public static class AuthorXml
{
    public static void Fields(XmlNode node, params string[] allowed)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (!allowed.Contains(child.Name) || !seen.Add(child.Name))
            {
                throw new ArgumentException("Unknown/duplicate field " + child.Name);
            }
        }
    }

    public static IEnumerable<XmlNode> Elements(XmlNode? node)
    {
        if (node == null)
        {
            yield break;
        }

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType == XmlNodeType.Element)
            {
                yield return child;
            }
            else if (child.NodeType != XmlNodeType.Comment && !string.IsNullOrWhiteSpace(child.InnerText))
            {
                throw new ArgumentException("Unexpected text in " + node.Name);
            }
        }
    }

    public static bool ExactKey(string value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    public static int Version(XmlNode node)
    {
        var text = node.SelectSingleNode("formatVersion")?.InnerText;
        int version = text == null ? 1 : int.Parse(text, CultureInfo.InvariantCulture);
        if (version != 1)
        {
            throw new ArgumentException("Unsupported formatVersion: " + version);
        }

        return version;
    }

    public static string Required(XmlNode node, string name)
    {
        string value = node.SelectSingleNode(name)?.InnerText ?? "";
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException("Invalid " + name);
        }

        return value;
    }

    public static Condition Condition(XmlNode node)
    {
        if (node.Attributes?.Count > 0)
        {
            throw new ArgumentException("Condition attributes are not supported");
        }

        if (node.Name == "ref")
        {
            if (node.ChildNodes.Cast<XmlNode>().Any(x => x.NodeType == XmlNodeType.Element))
            {
                throw new ArgumentException("ref must be a text leaf");
            }

            return new Condition("ref", Required(node, "."));
        }

        if (node.Name != "all" && node.Name != "any" && node.Name != "not")
        {
            throw new ArgumentException("Unknown condition " + node.Name);
        }

        var children = Elements(node).Select(Condition).ToArray();
        if (children.Length == 0 || (node.Name == "not" && children.Length != 1))
        {
            throw new ArgumentException("Invalid condition arity");
        }

        return new Condition(node.Name, "", children);
    }
}

public interface IUsDefinitionProvider
{
    void Collect(ProviderBatch batch);
}

public interface IUsFactSource
{
    string Key { get; }

    Truth Capture(string subject);
}

public sealed class ProviderBatch
{
    private readonly List<IUsFactSource> facts = new();
    public IReadOnlyList<IUsFactSource> Facts => facts;

    public void AddFactSource(IUsFactSource fact)
    {
        if (fact == null)
        {
            throw new ArgumentNullException(nameof(fact));
        }

        facts.Add(fact);
    }
}

public sealed class AuthorCatalog
{
    private readonly Dictionary<string, RegisteredFact> facts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActionPlan> actions = new(StringComparer.Ordinal);
    private readonly List<AudioCandidate> audio = new();
    private readonly HashSet<string> packKeys = new(StringComparer.Ordinal);
    public readonly List<string> Issues = new();
    public AuthorCatalog(long workInterval, long woundedInterval, bool fullDevCandidate = false)
    {
        foreach (var builtIn in BuiltInPawnContracts.Create(workInterval, woundedInterval, fullDevCandidate))
        {
            foreach (string fact in builtIn.Condition.RequiredFacts())
            {
                if (!facts.ContainsKey(fact))
                {
                    facts.Add(fact, new RegisteredFact(new BuiltInFact(fact), "us.core"));
                }
            }
            actions.Add(builtIn.Key, builtIn);
        }
        if (fullDevCandidate)
        {
            foreach (string key in new[] { "EatWholeJob", "EatNutrition", "EatChewing", "EatDrug", "ActiveCommand", "PlayerControlled", "Responsive", "BiotechAvailable" })
            {
                string identity = "us.core/" + key;
                facts.Add(identity, new RegisteredFact(new BuiltInFact(identity), "us.core"));
            }
        }
    }

    public ActionPlan[] Actions => actions.Values.ToArray();
    public AudioCandidate[] Audio => audio.ToArray();
    private readonly List<BehaviorSource> behaviorSources = new();
    public BehaviorSourceDirectory BehaviorSources => new(behaviorSources);
    public bool HasFact(string key) => facts.ContainsKey(key);
    public void AddBehaviorSource(BehaviorSource source)
    {
        behaviorSources.Add(source.Copy());
        foreach (var contribution in source.Contributions)
            foreach (string required in contribution.RequiredFacts.Where(x => !HasFact(x)))
                Issues.Add(source.Id + ":" + contribution.Action + ":Missing required fact " + required);
    }

    public bool AddProvider(IUsDefinitionProvider provider)
    {
        try
        {
            var batch = new ProviderBatch();
            provider.Collect(batch);
            var pending = new Dictionary<string, RegisteredFact>(StringComparer.Ordinal);
            string providerId = provider.GetType().FullName ?? provider.GetType().Name;
            foreach (var fact in batch.Facts)
            {
                // Key is author code: read exactly once before publishing any fact.
                string key = fact.Key;
                if (!AuthorXml.ExactKey(key) || facts.ContainsKey(key) || pending.ContainsKey(key))
                {
                    throw new ArgumentException("Duplicate/invalid fact " + key);
                }

                pending.Add(key, new RegisteredFact(fact, providerId));
            }

            foreach (var entry in pending)
            {
                facts.Add(entry.Key, entry.Value);
            }

            return true;
        }
        catch (Exception error)
        {
            Issues.Add("Provider:" + error.Message);
            return false;
        }
    }

    public bool AddAction(ActionDeclaration declaration)
    {
        try
        {
            if (declaration.FormatVersion != 1 || !AuthorXml.ExactKey(declaration.Key) || actions.ContainsKey(declaration.Key))
            {
                throw new ArgumentException("Duplicate/invalid action");
            }

            if (!actions.ContainsKey(declaration.BaseAction) || !declaration.BaseAction.StartsWith("us.core/", StringComparison.Ordinal))
            {
                throw new ArgumentException("baseAction must be a built-in action");
            }

            var parent = actions[declaration.BaseAction];
            foreach (string fact in declaration.Condition.RequiredFacts())
            {
                if (!facts.ContainsKey(fact))
                {
                    throw new ArgumentException("Missing required fact " + fact);
                }
            }

            // Composition can narrow a base condition, never bypass its qualification.
            actions.Add(declaration.Key, new ActionPlan(
                declaration.Key,
                parent.Priority,
                new Condition("all", "", parent.Condition, declaration.Condition),
                parent.Interval,
                parent.CheckShared,
                parent.WriteShared,
                parent.Chance,
                parent.BaseAction));
            return true;
        }
        catch (Exception error)
        {
            Issues.Add("Action:" + error.Message);
            return false;
        }
    }

    public bool AddPack(string packageId, string defName, VoicePackDeclaration declaration, Func<string, string?> bindSound)
    {
        string key = packageId + "/" + defName;
        try
        {
            if (!AuthorXml.ExactKey(packageId) || !AuthorXml.ExactKey(defName) || declaration.FormatVersion != 1 || !AuthorXml.ExactKey(declaration.Race) || packKeys.Contains(key))
            {
                throw new ArgumentException("Invalid/duplicate pack identity");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var batch = new List<AudioCandidate>();
            foreach (var entry in declaration.Entries)
            {
                if (!AuthorXml.ExactKey(entry.Key) || !seen.Add(entry.Key))
                {
                    throw new ArgumentException("Duplicate entry key");
                }

                if (entry.Enabled == false)
                {
                    continue;
                }

                if (!actions.ContainsKey(entry.Action))
                {
                    Issues.Add(key + ":Missing action " + entry.Action);
                    continue;
                }

                string? audioId = bindSound(entry.Sound);
                if (audioId == null)
                {
                    Issues.Add(key + ":Unbound sound " + entry.Sound);
                    continue;
                }

                batch.Add(new AudioCandidate(key + "/" + entry.Key, audioId, entry.Action, declaration.Race, entry.Weight, entry.Volume ?? 1));
            }

            audio.AddRange(batch);
            packKeys.Add(key);
            return true;
        }
        catch (Exception error)
        {
            Issues.Add(key + ":" + error.Message);
            return false;
        }
    }

    public FactSnapshot Capture(string subject, IDictionary<string, Truth> builtIns)
    {
        var snapshot = new Dictionary<string, Truth>(builtIns, StringComparer.Ordinal);
        var diagnostics = new List<FactDiagnostic>();
        foreach (var entry in facts)
        {
            string key = entry.Key;
            var registered = entry.Value;
            if (registered.Provider == "us.core")
            {
                continue;
            }

            try
            {
                Truth value = registered.Source.Capture(subject);
                snapshot[key] = value;
                if (value == Truth.Unknown)
                {
                    diagnostics.Add(new FactDiagnostic(key, registered.Provider, "ReportedUnknown"));
                }
            }
            catch (Exception error)
            {
                snapshot[key] = Truth.Unknown;
                diagnostics.Add(new FactDiagnostic(key, registered.Provider, error.GetType().Name + ":" + error.Message));
            }
        }

        return new FactSnapshot(snapshot, diagnostics);
    }

    private sealed class RegisteredFact
    {
        public readonly IUsFactSource Source;
        public readonly string Provider;
        public RegisteredFact(IUsFactSource source, string provider)
        {
            Source = source;
            Provider = provider;
        }
    }

    private sealed class BuiltInFact : IUsFactSource
    {
        public string Key { get; }

        public BuiltInFact(string key)
        {
            Key = key;
        }

        public Truth Capture(string subject) => Truth.Unknown;
    }
}
