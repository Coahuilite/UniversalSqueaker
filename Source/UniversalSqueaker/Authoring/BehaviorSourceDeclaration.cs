using System;
using System.Xml;

namespace UniversalSqueaker.Authoring;

/// <summary>Normal Def custom-field parsing; ownership is supplied by the loaded Def, not XML.</summary>
public sealed class BehaviorSourceDeclaration
{
    public BehaviorSource Source = new();
    public void LoadDataFromXmlCustom(XmlNode node)
    {
        if (node is not XmlElement element) throw new ArgumentException("Expected behavior declaration");
        Source = BehaviorSourceState.ReadSource(element, BehaviorSourceState.ReadDomain(element), "");
    }
}
