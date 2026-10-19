using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

namespace UniversalSqueaker;

public enum BehaviorSourceIntent
{
    Unconfigured,
    Inherit,
    Bound
}

/// <summary>Configuration identity, not a running Pawn or an audio pool.</summary>
public sealed class BehaviorDomain : IEquatable<BehaviorDomain>
{
    public readonly string Family;
    public readonly string Namespace;
    public readonly string Target;
    public readonly string Child;
    public BehaviorDomain(string family, string targetNamespace, string target, string child = "")
    {
        if ((family != "Pawn" && family != "NonPawn") || string.IsNullOrWhiteSpace(target)
            || target != target.Trim() || child != child.Trim()
            || (family == "Pawn" && targetNamespace != "")
            || (family == "NonPawn" && (string.IsNullOrWhiteSpace(targetNamespace) || targetNamespace != targetNamespace.Trim() || child != "")))
        {
            throw new ArgumentException("Invalid behavior domain");
        }
        Family = family;
        Namespace = targetNamespace;
        Target = target;
        Child = child;
    }
    public BehaviorDomain Parent => new(Family, Namespace, Target);
    public bool Equals(BehaviorDomain? other) => other != null && Family == other.Family
        && Namespace == other.Namespace && Target == other.Target && Child == other.Child;
    public override bool Equals(object? obj) => Equals(obj as BehaviorDomain);
    public override int GetHashCode() => (Family, Namespace, Target, Child).GetHashCode();
}

/// <summary>Sparse parameter contribution. Null is absence; false and zero are present values.</summary>
public sealed class BehaviorValues
{
    public bool? Enabled;
    public bool? TalkingEnabled;
    public bool? AgeEnabled;
    public bool? MoodEnabled;
    public Dictionary<string, double> ResponseParameters = new(StringComparer.Ordinal);
    public Dictionary<string, bool> ResponseEnabled = new(StringComparer.Ordinal);
    public Dictionary<string, bool> Participation = new(StringComparer.Ordinal);
    public SqueakActionScope? Scope;
    public long? HardIntervalTicks;
    public double? Volume;
    public double? Pitch;
    public BehaviorValues Copy()
    {
        var copy = (BehaviorValues)MemberwiseClone();
        copy.ResponseParameters = new Dictionary<string, double>(ResponseParameters, StringComparer.Ordinal);
        copy.ResponseEnabled = new Dictionary<string, bool>(ResponseEnabled, StringComparer.Ordinal);
        copy.Participation = new Dictionary<string, bool>(Participation, StringComparer.Ordinal);
        return copy;
    }
    public void Overlay(BehaviorValues value)
    {
        Enabled = value.Enabled ?? Enabled;
        Scope = value.Scope ?? Scope;
        HardIntervalTicks = value.HardIntervalTicks ?? HardIntervalTicks;
        TalkingEnabled = value.TalkingEnabled ?? TalkingEnabled;
        AgeEnabled = value.AgeEnabled ?? AgeEnabled;
        MoodEnabled = value.MoodEnabled ?? MoodEnabled;
        foreach (var parameter in value.ResponseParameters)
        {
            ResponseParameters[parameter.Key] = parameter.Value;
        }
        foreach (var response in value.ResponseEnabled)
        {
            ResponseEnabled[response.Key] = response.Value;
        }
        foreach (var response in value.Participation)
        {
            Participation[response.Key] = response.Value;
        }
        Volume = value.Volume ?? Volume;
        Pitch = value.Pitch ?? Pitch;
    }
    public bool Valid => (!Scope.HasValue || Enum.IsDefined(typeof(SqueakActionScope), Scope.Value))
        && (!HardIntervalTicks.HasValue || HardIntervalTicks.Value >= 0)
        && (!Volume.HasValue || Finite(Volume.Value) && Volume.Value >= 0 && Volume.Value <= float.MaxValue)
        && (!Pitch.HasValue || Finite(Pitch.Value) && Pitch.Value > 0 && Pitch.Value <= float.MaxValue)
        && ResponseParameters.All(x => Exact(x.Key) && Finite(x.Value))
        && ResponseEnabled.Keys.All(Exact) && Participation.Keys.All(Exact);
    private static bool Exact(string key) => !string.IsNullOrWhiteSpace(key) && key == key.Trim();
    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
}

public sealed class BehaviorContribution
{
    public string Action = "";
    public BehaviorValues Values = new();
    public string[] RequiredFacts = Array.Empty<string>();
    public BehaviorContribution Copy() => new() { Action = Action, Values = Values.Copy(), RequiredFacts = (string[])RequiredFacts.Clone() };
}

public sealed class BehaviorSource
{
    public string Id = "";
    public BehaviorDomain Domain = new("Pawn", "", "invalid");
    public int Semantics = 1;
    public List<BehaviorContribution> Contributions = new();
    public BehaviorSource Copy() => new() { Id = Id, Domain = Domain, Semantics = Semantics, Contributions = Contributions.Select(x => x.Copy()).ToList() };
}

public sealed class BehaviorBinding
{
    public BehaviorDomain Domain = new("Pawn", "", "invalid");
    public BehaviorSourceIntent Intent;
    public string SourceId = "";
    public BehaviorSource? Snapshot;
    public BehaviorBinding Copy() => new() { Domain = Domain, Intent = Intent, SourceId = SourceId, Snapshot = Snapshot?.Copy() };
}

/// <summary>Complete startup candidates, independently of audio enablement. Duplicates never win by order.</summary>
public sealed class BehaviorSourceDirectory
{
    private readonly BehaviorSource[] sources;
    public BehaviorSourceDirectory(IEnumerable<BehaviorSource> sources)
    {
        this.sources = sources.Distinct().Select(x => x.Copy()).ToArray();
    }
    public BehaviorSource[] Candidates(BehaviorDomain domain) => sources.Where(x => x.Domain.Equals(domain))
        .GroupBy(x => x.Id, StringComparer.Ordinal).Where(x => x.Count() == 1)
        .Select(x => x.Single()).Where(x => !x.Contributions.GroupBy(c => c.Action).Any(g => g.Count() > 1))
        .Select(x => x.Copy()).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
    public BehaviorSource? Find(BehaviorDomain domain, string id) => Candidates(domain).SingleOrDefault(x => x.Id == id);
}

/// <summary>Only author contributions are retained here. Player settings stay in their existing records.</summary>
public sealed class BehaviorSourceState
{
    public readonly List<BehaviorBinding> Bindings = new();
    public BehaviorSourceState Copy()
    {
        var result = new BehaviorSourceState();
        result.Bindings.AddRange(Bindings.Select(x => x.Copy()));
        return result;
    }
    public BehaviorBinding Read(BehaviorDomain domain) => Bindings.SingleOrDefault(x => x.Domain.Equals(domain))?.Copy()
        ?? new BehaviorBinding { Domain = domain };

    public void Forget(BehaviorDomain domain)
    {
        Bindings.RemoveAll(x => x.Domain.Equals(domain));
        Bindings.Add(new BehaviorBinding { Domain = domain, Intent = BehaviorSourceIntent.Inherit });
    }

    public bool Bind(BehaviorDomain domain, string sourceId, BehaviorSourceDirectory directory)
    {
        var source = directory.Find(domain, sourceId);
        if (string.IsNullOrWhiteSpace(sourceId) || source == null || source.Semantics != 1 || source.Contributions.Any(x => !Valid(x)))
        {
            return false;
        }
        Bindings.RemoveAll(x => x.Domain.Equals(domain));
        Bindings.Add(new BehaviorBinding { Domain = domain, Intent = BehaviorSourceIntent.Bound, SourceId = sourceId, Snapshot = source });
        return true;
    }

    // Explicit Dev injection only; callers must choose it. Never invoked during ordinary initialization.
    public bool AdoptUniqueDevCandidate(BehaviorDomain domain, BehaviorSourceDirectory directory)
    {
        if (Read(domain).Intent != BehaviorSourceIntent.Unconfigured)
        {
            return false;
        }
        var choices = directory.Candidates(domain);
        return choices.Length == 1 && Bind(domain, choices[0].Id, directory);
    }

    public void Refresh(BehaviorSourceDirectory directory)
    {
        foreach (var binding in Bindings.Where(x => x.Intent == BehaviorSourceIntent.Bound))
        {
            var current = directory.Find(binding.Domain, binding.SourceId);
            if (current == null || current.Semantics != 1)
            {
                continue;
            }
            // A bad action update preserves that action's last good contribution; unrelated actions refresh.
            var next = current.Copy();
            next.Contributions = current.Contributions.Where(Valid).Select(x => x.Copy()).ToList();
            foreach (var bad in current.Contributions.Where(x => !Valid(x)))
            {
                var previous = binding.Snapshot?.Contributions.SingleOrDefault(x => x.Action == bad.Action);
                if (previous != null)
                {
                    next.Contributions.Add(previous.Copy());
                }
            }
            binding.Snapshot = next;
        }
    }

    private static bool Valid(BehaviorContribution value) => !string.IsNullOrWhiteSpace(value.Action)
        && value.Values.Valid && value.RequiredFacts.All(x => !string.IsNullOrWhiteSpace(x));

    public BehaviorValues Resolve(BehaviorDomain domain, string action, BehaviorSourceDirectory directory,
        IEnumerable<BehaviorValues> playerLayers, Func<string, bool> implementationAvailable, out bool paused,
        Action<string>? reportPause = null)
    {
        var result = new BehaviorValues();
        paused = false;
        foreach (var layer in domain.Child == "" ? new[] { domain } : new[] { domain.Parent, domain })
        {
            var binding = Read(layer);
            if (binding.Intent != BehaviorSourceIntent.Bound)
            {
                continue;
            }
            var current = directory.Find(layer, binding.SourceId);
            bool hasCompatibleSnapshot = binding.Snapshot?.Semantics == 1;
            if (current?.Semantics != 1 && !hasCompatibleSnapshot)
            {
                // Unknown semantics is not an empty contribution. Preserve the record and suspend
                // the actions known to depend on that source until a compatible identity returns.
                bool depends = binding.Snapshot?.Contributions.Any(x => x.Action == action) == true
                    || current?.Contributions.Any(x => x.Action == action) == true;
                paused |= depends;
                if (depends)
                {
                    reportPause?.Invoke("IncompatibleAuthorSnapshot:" + binding.SourceId + ":" + action);
                }
                continue;
            }
            BehaviorContribution? contribution = null;
            if (current?.Semantics == 1)
            {
                contribution = current.Contributions.SingleOrDefault(x => x.Action == action);
                // A valid live source omitting this action deliberately contributes nothing.
                if (contribution == null)
                {
                    continue;
                }
            }
            if (contribution == null || !Valid(contribution))
            {
                contribution = binding.Snapshot?.Semantics == 1
                    ? binding.Snapshot.Contributions.SingleOrDefault(x => x.Action == action)
                    : null;
            }
            if (contribution == null)
            {
                // A newly invalid action without a good snapshot cannot become an enabled default.
                if (current?.Contributions.Any(x => x.Action == action) == true)
                {
                    paused = true;
                    reportPause?.Invoke("InvalidAuthorContribution:" + binding.SourceId + ":" + action);
                }
                continue;
            }
            if (contribution.RequiredFacts.Any(x => !implementationAvailable(x)))
            {
                paused = true;
                reportPause?.Invoke("MissingRequiredImplementation:" + binding.SourceId + ":" + action);
            }
            result.Overlay(contribution.Values);
        }
        foreach (var player in playerLayers)
        {
            if (!player.Valid)
            {
                throw new ArgumentException("InvalidPlayerBehaviorContribution");
            }
            result.Overlay(player);
        }
        return result;
    }

    public string ToXml()
    {
        var document = new XmlDocument();
        var root = document.CreateElement("behaviorSources");
        root.SetAttribute("format", "1");
        document.AppendChild(root);
        foreach (var binding in Bindings.OrderBy(x => x.Domain.Family, StringComparer.Ordinal).ThenBy(x => x.Domain.Namespace, StringComparer.Ordinal)
            .ThenBy(x => x.Domain.Target, StringComparer.Ordinal).ThenBy(x => x.Domain.Child, StringComparer.Ordinal))
        {
            var node = document.CreateElement("binding");
            WriteDomain(node, binding.Domain);
            node.SetAttribute("intent", binding.Intent.ToString());
            node.SetAttribute("source", binding.SourceId);
            root.AppendChild(node);
            if (binding.Snapshot != null)
            {
                WriteSource(document, node, binding.Snapshot);
            }
        }
        return root.OuterXml;
    }

    public static BehaviorSourceState FromXml(string xml)
    {
        var result = new BehaviorSourceState();
        if (string.IsNullOrEmpty(xml))
        {
            return result;
        }
        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml(xml);
        var root = document.DocumentElement!;
        if (root.Name != "behaviorSources" || root.GetAttribute("format") != "1")
        {
            throw new ArgumentException("Unsupported behavior snapshot format");
        }
        CheckAttributes(root, "format");
        foreach (XmlElement node in root.ChildNodes.OfType<XmlElement>())
        {
            if (node.Name != "binding")
            {
                throw new ArgumentException("Unknown behavior state element " + node.Name);
            }
            CheckAttributes(node, "family", "namespace", "target", "child", "intent", "source");
            if (node.ChildNodes.OfType<XmlElement>().Any(x => x.Name != "snapshot") || node.ChildNodes.OfType<XmlElement>().Count() > 1)
            {
                throw new ArgumentException("Binding requires at most one snapshot");
            }
            var domain = ReadDomain(node);
            if (result.Bindings.Any(x => x.Domain.Equals(domain)))
            {
                throw new ArgumentException("Duplicate behavior domain");
            }
            var intent = (BehaviorSourceIntent)Enum.Parse(typeof(BehaviorSourceIntent), node.GetAttribute("intent"));
            if (!Enum.IsDefined(typeof(BehaviorSourceIntent), intent))
            {
                throw new ArgumentException("Invalid source intent");
            }
            var binding = new BehaviorBinding { Domain = domain, Intent = intent, SourceId = node.GetAttribute("source") };
            var snapshot = node["snapshot"];
            if (snapshot != null)
            {
                binding.Snapshot = ReadSource(snapshot, domain, binding.SourceId);
            }
            if (binding.Snapshot?.Semantics == 1 && binding.Snapshot.Contributions.Any(x => !Valid(x)))
            {
                throw new ArgumentException("Invalid author snapshot");
            }
            if (intent == BehaviorSourceIntent.Bound && (binding.SourceId == "" || binding.Snapshot == null))
            {
                throw new ArgumentException("Bound source requires author snapshot");
            }
            if (intent != BehaviorSourceIntent.Bound && (binding.SourceId != "" || binding.Snapshot != null))
            {
                throw new ArgumentException("Unbound source cannot own snapshot");
            }
            result.Bindings.Add(binding);
        }
        return result;
    }

    public static BehaviorDomain ReadDomain(XmlElement node) => new(node.GetAttribute("family"), node.GetAttribute("namespace"), node.GetAttribute("target"), node.GetAttribute("child"));
    private static void WriteDomain(XmlElement node, BehaviorDomain domain)
    {
        node.SetAttribute("family", domain.Family);
        node.SetAttribute("namespace", domain.Namespace);
        node.SetAttribute("target", domain.Target);
        node.SetAttribute("child", domain.Child);
    }
    public static BehaviorSource ReadSource(XmlElement node, BehaviorDomain domain, string id)
    {
        if (node.Name == "snapshot")
        {
            CheckAttributes(node, "semantics");
        }
        else
        {
            CheckAttributes(node, "family", "namespace", "target", "child", "semantics");
        }
        var source = new BehaviorSource
        {
            Id = id,
            Domain = domain,
            Semantics = node.HasAttribute("semantics") ? int.Parse(node.GetAttribute("semantics"), CultureInfo.InvariantCulture) : 1
        };
        foreach (XmlElement action in node.ChildNodes.OfType<XmlElement>())
        {
            CheckAttributes(action, "key", "enabled", "scope", "hardIntervalTicks", "talkingEnabled", "ageEnabled", "moodEnabled", "volume", "pitch", "requires");
            if (action.Name != "action")
            {
                throw new ArgumentException("Invalid behavior action shape");
            }
            string key = action.GetAttribute("key");
            if (source.Contributions.Any(x => x.Action == key))
            {
                throw new ArgumentException("Duplicate behavior action");
            }
            var values = new BehaviorValues();
            foreach (XmlAttribute field in action.Attributes)
            {
                string text = field.Value;
                switch (field.Name)
                {
                    case "key":
                    case "requires":
                        break;
                    case "enabled":
                        values.Enabled = bool.Parse(text);
                        break;
                    case "scope":
                        values.Scope = (SqueakActionScope)Enum.Parse(typeof(SqueakActionScope), text);
                        break;
                    case "hardIntervalTicks":
                        values.HardIntervalTicks = long.Parse(text, CultureInfo.InvariantCulture);
                        break;
                    case "talkingEnabled":
                        values.TalkingEnabled = bool.Parse(text);
                        break;
                    case "ageEnabled":
                        values.AgeEnabled = bool.Parse(text);
                        break;
                    case "moodEnabled":
                        values.MoodEnabled = bool.Parse(text);
                        break;
                    case "volume":
                        values.Volume = double.Parse(text, CultureInfo.InvariantCulture);
                        break;
                    case "pitch":
                        values.Pitch = double.Parse(text, CultureInfo.InvariantCulture);
                        break;
                    default:
                        throw new ArgumentException("Unknown behavior parameter " + field.Name);
                }
            }
            foreach (XmlElement setting in action.ChildNodes.OfType<XmlElement>())
            {
                if (setting.ChildNodes.OfType<XmlElement>().Any())
                {
                    throw new ArgumentException("Response setting must be a leaf");
                }
                string identity = setting.GetAttribute("key");
                if (string.IsNullOrWhiteSpace(identity) || identity != identity.Trim())
                {
                    throw new ArgumentException("Invalid response setting identity");
                }
                if (setting.Name == "parameter")
                {
                    CheckAttributes(setting, "key", "value");
                    values.ResponseParameters.Add(identity, double.Parse(setting.GetAttribute("value"), CultureInfo.InvariantCulture));
                }
                else if (setting.Name == "response")
                {
                    CheckAttributes(setting, "key", "enabled", "participation");
                    if (!setting.HasAttribute("enabled") && !setting.HasAttribute("participation"))
                    {
                        throw new ArgumentException("Empty response setting");
                    }
                    if (setting.HasAttribute("enabled"))
                    {
                        values.ResponseEnabled.Add(identity, bool.Parse(setting.GetAttribute("enabled")));
                    }
                    if (setting.HasAttribute("participation"))
                    {
                        values.Participation.Add(identity, ReadParticipation(setting.GetAttribute("participation")));
                    }
                }
                else
                {
                    throw new ArgumentException("Unknown response setting " + setting.Name);
                }
            }
            source.Contributions.Add(new BehaviorContribution { Action = key, Values = values,
                RequiredFacts = action.GetAttribute("requires").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) });
        }
        return source;
    }
    private static void WriteSource(XmlDocument document, XmlElement parent, BehaviorSource source)
    {
        var node = document.CreateElement("snapshot");
        node.SetAttribute("semantics", source.Semantics.ToString(CultureInfo.InvariantCulture));
        parent.AppendChild(node);
        foreach (var contribution in source.Contributions.OrderBy(x => x.Action, StringComparer.Ordinal))
        {
            var action = document.CreateElement("action");
            action.SetAttribute("key", contribution.Action);
            var v = contribution.Values;
            Field(action, "enabled", v.Enabled);
            Field(action, "scope", v.Scope);
            Field(action, "hardIntervalTicks", v.HardIntervalTicks);
            Field(action, "talkingEnabled", v.TalkingEnabled);
            Field(action, "ageEnabled", v.AgeEnabled);
            Field(action, "moodEnabled", v.MoodEnabled);
            Field(action, "volume", v.Volume);
            Field(action, "pitch", v.Pitch);
            foreach (var parameter in v.ResponseParameters.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var element = document.CreateElement("parameter");
                element.SetAttribute("key", parameter.Key);
                element.SetAttribute("value", parameter.Value.ToString("R", CultureInfo.InvariantCulture));
                action.AppendChild(element);
            }
            foreach (string identity in v.ResponseEnabled.Keys.Concat(v.Participation.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal))
            {
                var element = document.CreateElement("response");
                element.SetAttribute("key", identity);
                if (v.ResponseEnabled.TryGetValue(identity, out bool enabled))
                {
                    element.SetAttribute("enabled", enabled.ToString());
                }
                if (v.Participation.TryGetValue(identity, out bool apply))
                {
                    element.SetAttribute("participation", apply ? "Apply" : "Skip");
                }
                action.AppendChild(element);
            }
            if (contribution.RequiredFacts.Length > 0) action.SetAttribute("requires", string.Join(" ", contribution.RequiredFacts));
            node.AppendChild(action);
        }
    }
    private static void Field<T>(XmlElement node, string key, T? value) where T : struct
    {
        if (value.HasValue) node.SetAttribute(key, Convert.ToString(value.Value, CultureInfo.InvariantCulture)!);
    }

    public static bool ReadParticipation(string text)
    {
        if (text == "Apply")
        {
            return true;
        }
        if (text == "Skip")
        {
            return false;
        }
        throw new ArgumentException("Expected Apply or Skip participation");
    }

    private static void CheckAttributes(XmlElement node, params string[] allowed)
    {
        if (node.ChildNodes.Cast<XmlNode>().Any(x => x.NodeType == XmlNodeType.Text && !string.IsNullOrWhiteSpace(x.InnerText)))
        {
            throw new ArgumentException("Unexpected behavior text in " + node.Name);
        }
        foreach (XmlAttribute attribute in node.Attributes)
        {
            if (!allowed.Contains(attribute.Name))
            {
                throw new ArgumentException("Unknown behavior field " + attribute.Name);
            }
        }
    }
}

