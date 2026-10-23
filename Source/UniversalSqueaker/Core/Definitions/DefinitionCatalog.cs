using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker.Authoring;

public sealed class VoiceEntry
{
    public string Key = "";
    public string Action = "";
    public string Sound = "";
    public List<string> Sounds = new();
    public Kernel.AgeBucket? Age;
    public bool Egg;
    public double Weight = 1;
    public bool? Enabled;
    public double? Volume;
    public double Pitch = 1;
    public Dictionary<string, bool> Participation = new(StringComparer.Ordinal);
}

/// <summary>RimWorld calls this same custom field loader during normal Def deserialization.</summary>
public sealed class VoicePackDeclaration
{
    public double Weight = 1;
    public int FormatVersion = 1;
    public string Race = "";
    public string Family = "Pawn", Namespace = "", Target = "", Child = "";
    public BehaviorDomain Domain => new(Family, Namespace, Target == "" ? Race : Target, Child);
    public readonly List<VoiceEntry> Entries = new();
    public Dictionary<string, string> Fallbacks = new(StringComparer.Ordinal);
    public Dictionary<string, bool> Participation = new(StringComparer.Ordinal);
    public Dictionary<string, Dictionary<string, bool>> ActionParticipation = new(StringComparer.Ordinal);
    public void LoadDataFromXmlCustom(XmlNode node)
    {
        AuthorXml.Fields(node, "formatVersion", "race", "family", "namespace", "target", "child", "weight", "entries", "fallbacks", "participation", "actionParticipation");
        Weight = double.Parse(node.SelectSingleNode("weight")?.InnerText ?? "1", CultureInfo.InvariantCulture);
        Participation = AuthorXml.Participation(node.SelectSingleNode("participation"));
        ActionParticipation.Clear();
        if (node.SelectSingleNode("actionParticipation")?.Attributes?.Count > 0)
        {
            throw new ArgumentException("Unexpected actionParticipation container attributes");
        }
        foreach (XmlNode action in AuthorXml.Elements(node.SelectSingleNode("actionParticipation")))
        {
            string key = action.Attributes?["action"]?.Value ?? "";
            if (action.Name != "li" || action.Attributes?.Count != 1 || !AuthorXml.ExactKey(key))
            {
                throw new ArgumentException("Expected pack-local action participation li with exact action");
            }
            ActionParticipation.Add(key, AuthorXml.Participation(action, actionContainer: true));
        }
        FormatVersion = AuthorXml.Version(node);
        Race = node.SelectSingleNode("race") == null ? "" : AuthorXml.Required(node, "race");
        Family = node.SelectSingleNode("family")?.InnerText ?? "Pawn";
        Namespace = node.SelectSingleNode("namespace")?.InnerText ?? "";
        Target = node.SelectSingleNode("target")?.InnerText ?? "";
        Child = node.SelectSingleNode("child")?.InnerText ?? "";
        if (Race != "" && (Family != "Pawn" || Target != "")) throw new ArgumentException("Use one exact target declaration");
        _ = Domain;
        Entries.Clear();
        Fallbacks.Clear();
        foreach (XmlNode fallback in AuthorXml.Elements(node.SelectSingleNode("fallbacks")))
        {
            string action = fallback.Attributes?["action"]?.Value ?? "";
            if (fallback.Name != "li" || fallback.Attributes?.Count != 1 || !AuthorXml.ActionId(action)
                || fallback.ChildNodes.OfType<XmlElement>().Any() || !AuthorXml.ExactKey(fallback.InnerText))
                throw new ArgumentException("Expected exact fallback action and sound");
            Fallbacks.Add(action, fallback.InnerText);
        }
        foreach (XmlNode entry in AuthorXml.Elements(node.SelectSingleNode("entries")))
        {
            if (entry.Name != "li")
            {
                throw new ArgumentException("Unknown entries element " + entry.Name);
            }

            AuthorXml.Fields(entry, "key", "action", "sound", "sounds", "age", "egg", "weight", "enabled", "volume", "pitch", "participation");
            var weightText = entry.SelectSingleNode("weight")?.InnerText;
            var enabledText = entry.SelectSingleNode("enabled")?.InnerText;
            var volumeText = entry.SelectSingleNode("volume")?.InnerText;
            Entries.Add(new VoiceEntry
            {
                Key = AuthorXml.Required(entry, "key"),
                Action = AuthorXml.Required(entry, "action"),
                Sound = entry.SelectSingleNode("sound") == null ? "" : AuthorXml.Required(entry, "sound"),
                Sounds = AuthorXml.Elements(entry.SelectSingleNode("sounds")).Select(x => x.Name == "li" ? AuthorXml.Required(x, ".") : throw new ArgumentException("Expected sound li")).ToList(),
                Age = entry.SelectSingleNode("age") == null ? null : (Kernel.AgeBucket)Enum.Parse(typeof(Kernel.AgeBucket), AuthorXml.Required(entry, "age")),
                Egg = bool.Parse(entry.SelectSingleNode("egg")?.InnerText ?? "false"),
                Weight = weightText == null ? 1 : double.Parse(weightText, CultureInfo.InvariantCulture),
                Enabled = enabledText == null ? null : bool.Parse(enabledText),
                Volume = volumeText == null ? null : double.Parse(volumeText, CultureInfo.InvariantCulture),
                Pitch = double.Parse(entry.SelectSingleNode("pitch")?.InnerText ?? "1", CultureInfo.InvariantCulture),
                Participation = AuthorXml.Participation(entry.SelectSingleNode("participation"))
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
    public bool Sustained;
    public int Priority;
    public long Interval;
    public double Chance = 1;
    public bool CheckShared = true, WriteShared = true;
    public bool RequiresVocal, TalkingEnabled;
    public void LoadDataFromXmlCustom(XmlNode node)
    {
        AuthorXml.Fields(node, "formatVersion", "key", "baseAction", "condition", "playback", "priority", "intervalTicks", "chance", "checkShared", "writeShared", "requiresVocal", "talkingEnabled");
        string playback = node.SelectSingleNode("playback")?.InnerText ?? "OneShot";
        if (playback != "OneShot" && playback != "Sustained")
        {
            throw new ArgumentException("Expected OneShot or Sustained playback");
        }
        Sustained = playback == "Sustained";
        FormatVersion = AuthorXml.Version(node);
        Key = AuthorXml.Required(node, "key");
        BaseAction = node.SelectSingleNode("baseAction") == null ? "" : AuthorXml.Required(node, "baseAction");
        Priority = int.Parse(node.SelectSingleNode("priority")?.InnerText ?? "0", CultureInfo.InvariantCulture);
        Interval = long.Parse(node.SelectSingleNode("intervalTicks")?.InnerText ?? "0", CultureInfo.InvariantCulture);
        Chance = double.Parse(node.SelectSingleNode("chance")?.InnerText ?? "1", CultureInfo.InvariantCulture);
        CheckShared = bool.Parse(node.SelectSingleNode("checkShared")?.InnerText ?? "true");
        WriteShared = bool.Parse(node.SelectSingleNode("writeShared")?.InnerText ?? "true");
        RequiresVocal = bool.Parse(node.SelectSingleNode("requiresVocal")?.InnerText ?? "false");
        TalkingEnabled = bool.Parse(node.SelectSingleNode("talkingEnabled")?.InnerText ?? "false");
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
    public static Dictionary<string, bool> Participation(XmlNode? node, bool actionContainer = false)
    {
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (node?.Attributes?.Count > (actionContainer ? 1 : 0))
        {
            throw new ArgumentException("Unexpected participation container attributes");
        }
        foreach (XmlNode entry in Elements(node))
        {
            if (entry.Name != "li" || entry.Attributes?.Count != 1 || entry.Attributes["key"] == null
                || entry.ChildNodes.OfType<XmlElement>().Any())
            {
                throw new ArgumentException("Expected participation li with key and Apply or Skip text");
            }
            string key = entry.Attributes["key"]!.Value;
            if (!ExactKey(key))
            {
                throw new ArgumentException("Invalid participation identity");
            }
            result.Add(key, BehaviorSourceState.ReadParticipation(entry.InnerText));
        }
        return result;
    }
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

    public static bool ActionId(string value) => ExactKey(value) && value.IndexOf('/') > 0 && !value.EndsWith("/", StringComparison.Ordinal);
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
    private readonly List<IUsResponseRule> responses = new();
    public IReadOnlyList<IUsFactSource> Facts => facts;
    public IReadOnlyList<IUsResponseRule> Responses => responses;

    public void AddResponse(IUsResponseRule response)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }
        responses.Add(response);
    }

    public void AddFactSource(IUsFactSource fact)
    {
        if (fact == null)
        {
            throw new ArgumentNullException(nameof(fact));
        }

        facts.Add(fact);
    }
}

public sealed class DefinitionCatalog
{
    private bool published;
    private BehaviorSourceDirectory? sourceDirectory;
    public DefinitionCatalog Publish() { sourceDirectory = new BehaviorSourceDirectory(behaviorSources); published = true; return this; }
    private void RequireBuilding() { if (published) throw new InvalidOperationException("DefinitionRevisionAlreadyPublished"); }
    public DefinitionCatalog NextRevision()
    {
        var next = new DefinitionCatalog(0, 0, true);
        next.facts.Clear(); foreach (var pair in facts) next.facts.Add(pair.Key, pair.Value);
        next.actions.Clear(); foreach (var pair in actions) next.actions.Add(pair.Key, pair.Value);
        next.responses.Clear(); foreach (var pair in responses) next.responses.Add(pair.Key, pair.Value);
        foreach (var source in behaviorSources) next.behaviorSources.Add(source.Copy());
        next.Issues.AddRange(Issues);
        return next;
    }
    private readonly Dictionary<string, RegisteredFact> facts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActionPlan> actions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IUsResponseRule> responses = new(StringComparer.Ordinal)
    {
        ["us.core/Age"] = new NativeAgeResponse(),
        ["us.core/Mood"] = new MoodResponse()
    };
    public IUsResponseRule[] Responses => responses.Values.ToArray();
    public readonly List<string> Issues = new();
    public DefinitionCatalog(long workInterval, long woundedInterval, bool fullDevCandidate = false)
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
            foreach (string key in new[] { "IngestionExecution", "IngestibleNutrition", "IngestibleDrug", "IngestionResultEvent", "ActiveCommand", "PlayerControlled", "Awake", "NotDowned", "PlayerEquipCommand", "Responsive", "HasCurrentInjury", "BiotechAvailable" })
            {
                string identity = "us.core/" + key;
                facts.Add(identity, new RegisteredFact(new BuiltInFact(identity), "us.core"));
            }
        }
    }

    public ActionPlan[] Actions => actions.Values.ToArray();
    public bool HasAction(string key) => actions.ContainsKey(key);
    private readonly List<BehaviorSource> behaviorSources = new();
    public BehaviorSourceDirectory BehaviorSources => sourceDirectory ?? new(behaviorSources);
    public bool HasFact(string key) => facts.ContainsKey(key);
    public IEnumerable<string> FactKeys => facts.Keys;
    public bool DeclareNotification(NotificationContract contract)
    {
        RequireBuilding();
        if (contract == null) throw new ArgumentNullException(nameof(contract));
        if (actions.ContainsKey(contract.ActionId) || facts.ContainsKey(contract.FactId))
        {
            Issues.Add("Notification:" + contract.ActionId + ":DuplicateIdentity");
            return false;
        }
        facts.Add(contract.FactId, new RegisteredFact(new BuiltInFact(contract.FactId), "us.notification"));
        if (AddAction(contract.Declaration())) return true;
        facts.Remove(contract.FactId);
        return false;
    }
    public void AddBehaviorSource(BehaviorSource source)
    {
        RequireBuilding();
        behaviorSources.Add(source.Copy());
        foreach (var contribution in source.Contributions)
            foreach (string required in contribution.RequiredFacts.Where(x => !HasFact(x)))
                Issues.Add(source.Id + ":" + contribution.Action + ":Missing required fact " + required);
    }

    public bool AddProvider(IUsDefinitionProvider provider)
    {
        RequireBuilding();
        try
        {
            var batch = new ProviderBatch();
            provider.Collect(batch);
            var pending = new Dictionary<string, RegisteredFact>(StringComparer.Ordinal);
            var pendingResponses = new Dictionary<string, IUsResponseRule>(StringComparer.Ordinal);
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

            foreach (var response in batch.Responses)
            {
                string key = response.Key;
                var stage = response.Stage;
                if (!AuthorXml.ExactKey(key) || responses.ContainsKey(key) || pendingResponses.ContainsKey(key)
                    || !Enum.IsDefined(typeof(ResponseStage), stage))
                {
                    throw new ArgumentException("Duplicate/invalid response " + key);
                }
                pendingResponses.Add(key, new RegisteredResponse(key, stage, response));
            }
            foreach (var entry in pending)
            {
                facts.Add(entry.Key, entry.Value);
            }
            foreach (var entry in pendingResponses)
            {
                responses.Add(entry.Key, entry.Value);
            }

            return true;
        }
        catch (Exception error)
        {
            Issues.Add("Provider:" + error.Message);
            return false;
        }
    }

    public bool AddAction(ActionDeclaration declaration) => AddActions(new[] { declaration }) == 1;

    /// <summary>Resolve the complete declaration graph, independent of Def enumeration order.</summary>
    public int AddActions(IEnumerable<ActionDeclaration> declarations)
    {
        RequireBuilding();
        var groups = declarations.GroupBy(x => x.Key, StringComparer.Ordinal).ToArray();
        var pending = new Dictionary<string, ActionDeclaration>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            if (!AuthorXml.ActionId(group.Key) || group.Count() != 1 || actions.ContainsKey(group.Key))
            {
                Issues.Add("Action:" + group.Key + ":Duplicate/invalid identity");
                continue;
            }
            pending.Add(group.Key, group.Single());
        }
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var failed = new HashSet<string>(StringComparer.Ordinal);
        bool Resolve(string key)
        {
            if (actions.ContainsKey(key)) return true;
            if (failed.Contains(key) || !pending.TryGetValue(key, out var declaration)) return false;
            if (!visiting.Add(key))
            {
                Issues.Add("Action:" + key + ":Inheritance cycle");
                failed.Add(key);
                return false;
            }
            try
            {
                if (declaration.FormatVersion != 1) throw new ArgumentException("Unsupported formatVersion");
                ActionPlan? parent = null;
                if (declaration.BaseAction != "")
                {
                    if (!Resolve(declaration.BaseAction)) throw new ArgumentException("Unavailable parent " + declaration.BaseAction);
                    parent = actions[declaration.BaseAction];
                }
                foreach (string fact in declaration.Condition.RequiredFacts())
                    if (!facts.ContainsKey(fact)) throw new ArgumentException("Missing required fact " + fact);
                // Inherit fixed conditions only. Routing, priority and mutable configuration are not inherited identities.
                var condition = parent == null ? declaration.Condition
                    : new Condition("all", "", parent.Condition, declaration.Condition);
                actions.Add(key, new ActionPlan(key, declaration.Priority, condition,
                    declaration.Interval, declaration.CheckShared, declaration.WriteShared, declaration.Chance,
                    declaration.BaseAction, !declaration.TalkingEnabled, sustained: declaration.Sustained,
                    requiresVocal: declaration.RequiresVocal || parent?.RequiresVocal == true,
                    subjectRole: parent?.SubjectRole));
                return true;
            }
            catch (Exception error)
            {
                failed.Add(key);
                Issues.Add("Action:" + key + ":" + error.Message);
                return false;
            }
            finally { visiting.Remove(key); }
        }
        int before = actions.Count;
        foreach (string key in pending.Keys.OrderBy(x => x, StringComparer.Ordinal)) Resolve(key);
        return actions.Count - before;
    }

    public FactSnapshot Capture(string subject, IDictionary<string, Truth> builtIns)
        => Capture(subject, builtIns, null);

    public FactSnapshot Capture(string subject, IDictionary<string, Truth> builtIns, IEnumerable<FactDiagnostic>? builtInDiagnostics)
        => Capture(subject, builtIns, builtInDiagnostics, null);
    public FactSnapshot Capture(string subject, IDictionary<string, Truth> builtIns, IEnumerable<FactDiagnostic>? builtInDiagnostics, IngestionResultFact? ingestion)
    {
        var snapshot = new Dictionary<string, Truth>(builtIns, StringComparer.Ordinal);
        var diagnostics = new List<FactDiagnostic>(builtInDiagnostics ?? Array.Empty<FactDiagnostic>());
        foreach (var entry in facts)
        {
            string key = entry.Key;
            var registered = entry.Value;
            if (registered.Provider == "us.notification")
            {
                if (!snapshot.ContainsKey(key)) snapshot[key] = Truth.False;
                continue;
            }
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

        return new FactSnapshot(snapshot, diagnostics, ingestion);
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

    private sealed class RegisteredResponse : IUsResponseRule
    {
        public string Key { get; }
        public ResponseStage Stage { get; }
        private readonly IUsResponseRule source;
        internal RegisteredResponse(string key, ResponseStage stage, IUsResponseRule source)
        {
            Key = key;
            Stage = stage;
            this.source = source;
        }
        public ResponseContribution Evaluate(ResponseContext context) => source.Evaluate(context);
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
