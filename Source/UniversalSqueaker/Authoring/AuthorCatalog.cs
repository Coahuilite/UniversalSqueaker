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
        foreach (XmlNode entry in node.SelectNodes("entries/li")!)
        {
            AuthorXml.Fields(entry, "key", "action", "sound", "weight", "enabled", "volume");
            var weightText = entry.SelectSingleNode("weight")?.InnerText;
            Entries.Add(new VoiceEntry { Key = AuthorXml.Required(entry, "key"), Action = AuthorXml.Required(entry, "action"), Sound = AuthorXml.Required(entry, "sound"), Weight = weightText == null ? 1 : double.Parse(weightText, CultureInfo.InvariantCulture), Enabled = entry.SelectSingleNode("enabled") == null ? null : bool.Parse(entry.SelectSingleNode("enabled")!.InnerText), Volume = entry.SelectSingleNode("volume") == null ? null : double.Parse(entry.SelectSingleNode("volume")!.InnerText, CultureInfo.InvariantCulture) });
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
        var root = node.SelectSingleNode("condition/*") ?? throw new ArgumentException("condition is required");
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
            if (child.NodeType != XmlNodeType.Element) continue;
            if (!allowed.Contains(child.Name) || !seen.Add(child.Name)) throw new ArgumentException("Unknown/duplicate field " + child.Name);
        }
    }
    public static bool ExactKey(string value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    public static int Version(XmlNode node)
    {
        var text = node.SelectSingleNode("formatVersion")?.InnerText;
        int version = text == null ? 1 : int.Parse(text, CultureInfo.InvariantCulture);
        if (version != 1) throw new ArgumentException("Unsupported formatVersion: " + version);
        return version;
    }
    public static string Required(XmlNode node, string name)
    {
        string value = node.SelectSingleNode(name)?.InnerText ?? "";
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim()) throw new ArgumentException("Invalid " + name);
        return value;
    }
    public static Condition Condition(XmlNode node)
    {
        if (node.Name == "ref") return new Condition("ref", Required(node, "."));
        if (node.Name != "all" && node.Name != "any" && node.Name != "not") throw new ArgumentException("Unknown condition " + node.Name);
        var children = node.ChildNodes.Cast<XmlNode>().Where(x => x.NodeType == XmlNodeType.Element).Select(Condition).ToArray();
        if (children.Length == 0 || (node.Name == "not" && children.Length != 1)) throw new ArgumentException("Invalid condition arity");
        return new Condition(node.Name, "", children);
    }
}
public interface IUsDefinitionProvider { void Collect(ProviderBatch batch); }
public interface IUsFactSource { string Key { get; } Truth Capture(string subject); }
public sealed class ProviderBatch
{
    private readonly List<IUsFactSource> facts = new();
    public IReadOnlyList<IUsFactSource> Facts => facts;
    public void AddFactSource(IUsFactSource fact) { if (fact == null) throw new ArgumentNullException(nameof(fact)); facts.Add(fact); }
}
public sealed class AuthorCatalog
{
    private readonly Dictionary<string, IUsFactSource> facts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActionPlan> actions = new(StringComparer.Ordinal);
    private readonly List<AudioCandidate> audio = new();
    private readonly HashSet<string> packKeys = new(StringComparer.Ordinal);
    public readonly List<string> Issues = new();
    public AuthorCatalog(long workInterval, long woundedInterval)
    {
        facts.Add("us.core/WorkState", new BuiltInFact("us.core/WorkState"));
        facts.Add("us.core/WoundedEvent", new BuiltInFact("us.core/WoundedEvent"));
        facts.Add("us.core/CallState", new BuiltInFact("us.core/CallState"));
        actions.Add("us.core/Work", new ActionPlan("us.core/Work", 20, new Condition("ref", "us.core/WorkState"), workInterval));
        actions.Add("us.core/Wounded", new ActionPlan("us.core/Wounded", 30, new Condition("ref", "us.core/WoundedEvent"), woundedInterval));
        actions.Add("us.core/Call", new ActionPlan("us.core/Call", 0, new Condition("ref", "us.core/CallState"), workInterval));
    }
    public ActionPlan[] Actions => actions.Values.ToArray();
    public AudioCandidate[] Audio => audio.ToArray();
    public bool AddProvider(IUsDefinitionProvider provider)
    {
        try
        {
            var batch = new ProviderBatch();
            provider.Collect(batch);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fact in batch.Facts) if (!AuthorXml.ExactKey(fact.Key) || facts.ContainsKey(fact.Key) || !keys.Add(fact.Key)) throw new ArgumentException("Duplicate/invalid fact " + fact.Key);
            foreach (var fact in batch.Facts) facts.Add(fact.Key, fact);
            return true;
        }
        catch (Exception error) { Issues.Add("Provider:" + error.Message); return false; }
    }
    public bool AddAction(ActionDeclaration declaration)
    {
        try
        {
            if (declaration.FormatVersion != 1 || !AuthorXml.ExactKey(declaration.Key) || actions.ContainsKey(declaration.Key)) throw new ArgumentException("Duplicate/invalid action");
            if (!actions.TryGetValue(declaration.BaseAction, out var parent)) throw new ArgumentException("Unknown base action");
            foreach (string fact in declaration.Condition.RequiredFacts()) if (!facts.ContainsKey(fact)) throw new ArgumentException("Missing required fact " + fact);
            // Composition can narrow a base condition, never bypass its qualification.
            actions.Add(declaration.Key, new ActionPlan(declaration.Key, parent.Priority, new Condition("all", "", parent.Condition, declaration.Condition), parent.Interval, parent.CheckShared, parent.WriteShared, parent.Chance, parent.BaseAction));
            return true;
        }
        catch (Exception error) { Issues.Add("Action:" + error.Message); return false; }
    }
    public bool AddPack(string packageId, string defName, VoicePackDeclaration declaration, Func<string, string?> bindSound)
    {
        string key = packageId + "/" + defName;
        try
        {
            if (!AuthorXml.ExactKey(packageId) || !AuthorXml.ExactKey(defName) || declaration.FormatVersion != 1 || !AuthorXml.ExactKey(declaration.Race) || packKeys.Contains(key)) throw new ArgumentException("Invalid/duplicate pack identity");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var batch = new List<AudioCandidate>();
            foreach (var entry in declaration.Entries)
            {
                if (!AuthorXml.ExactKey(entry.Key) || !seen.Add(entry.Key)) throw new ArgumentException("Duplicate entry key");
                if (entry.Enabled == false) continue;
                if (!actions.ContainsKey(entry.Action)) { Issues.Add(key + ":Missing action " + entry.Action); continue; }
                string? audioId = bindSound(entry.Sound);
                if (audioId == null) { Issues.Add(key + ":Unbound sound " + entry.Sound); continue; }
                batch.Add(new AudioCandidate(key + "/" + entry.Key, audioId, entry.Action, declaration.Race, entry.Weight, entry.Volume ?? 1));
            }
            audio.AddRange(batch); packKeys.Add(key); return true;
        }
        catch (Exception error) { Issues.Add(key + ":" + error.Message); return false; }
    }
    public FactSnapshot Capture(string subject, IDictionary<string, Truth> builtIns)
    {
        var snapshot = new Dictionary<string, Truth>(builtIns, StringComparer.Ordinal);
        foreach (var fact in facts.Values)
        {
            if (fact.Key.StartsWith("us.core/", StringComparison.Ordinal)) continue;
            try { snapshot[fact.Key] = fact.Capture(subject); } catch { snapshot[fact.Key] = Truth.Unknown; }
        }
        return new FactSnapshot(snapshot);
    }
    private sealed class BuiltInFact : IUsFactSource
    {
        public string Key { get; }
        public BuiltInFact(string key) { Key = key; }
        public Truth Capture(string subject) => Truth.Unknown;
    }
}
