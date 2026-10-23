using System;
using System.Xml;
using System.Collections.Generic;
using System.Linq;

namespace UniversalSqueaker.Authoring;

/// <summary>Normal Def custom-field parsing; ownership is supplied by the loaded Def, not XML.</summary>
public sealed class BehaviorSourceDeclaration
{
    public BehaviorSource Source = new();
    public readonly List<BehaviorSource> Sources = new();
    public void LoadDataFromXmlCustom(XmlNode node)
    {
        if (node is not XmlElement element) throw new ArgumentException("Expected behavior declaration");
        Sources.Clear();
        if (element.HasAttribute("family"))
        {
            Sources.Add(BehaviorSourceState.ReadSource(element, BehaviorSourceState.ReadDomain(element), ""));
        }
        else
        {
            if (element.Attributes.Count != 0)
            {
                throw new ArgumentException("Multi-target declaration uses target-local attributes");
            }
            foreach (XmlNode child in AuthorXml.Elements(element))
            {
                if (child is not XmlElement target || target.Name != "target")
                {
                    throw new ArgumentException("Expected exact target block");
                }
                var source = BehaviorSourceState.ReadSource(target, BehaviorSourceState.ReadDomain(target), "");
                if (Sources.Any(x => x.Domain.Equals(source.Domain)))
                {
                    throw new ArgumentException("Duplicate exact source target");
                }
                Sources.Add(source);
            }
        }
        if (Sources.Count == 0)
        {
            throw new ArgumentException("Behavior source needs at least one target");
        }
        Source = Sources[0];
    }
}
