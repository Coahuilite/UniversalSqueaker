using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Verse;

namespace UniversalSqueaker;

/// <summary>Read-only player templates. Import copies sparse values, never binds an author source.</summary>
public sealed class UsBehaviorPresetDef : Def
{
    public string presetLabel = "", presetDescription = "";
    public List<PresetRaceEntry> races = new();
}

public sealed class PresetRaceEntry
{
    public string raceDefName = "";
    public List<PresetActionValues> actions = new();
    public List<PresetXenotypeEntry> xenotypes = new();
}

public sealed class PresetXenotypeEntry
{
    public string xenotypeDefName = "";
    public bool inheritFromRace = true;
    public List<PresetActionValues> actions = new();
}

/// <summary>The same action/value grammar as behavior sources; no alternate multiplier/mood schema.</summary>
public sealed class PresetActionValues
{
    public string ActionId { get; private set; } = "";
    public BehaviorValues Values { get; private set; } = new();
    public PresetActionValues() { }
    public PresetActionValues(string actionId, BehaviorValues values)
    {
        if (!Authoring.AuthorXml.ActionId(actionId) || !values.Valid || !values.ApplicableTo("Pawn"))
            throw new ArgumentException("InvalidPlayerTemplate");
        ActionId = actionId; Values = values.Copy();
    }
    public void LoadDataFromXmlCustom(XmlNode node)
    {
        var document = new XmlDocument();
        var source = document.CreateElement("snapshot");
        var action = document.CreateElement("action");
        foreach (XmlAttribute attribute in node.Attributes!) action.SetAttribute(attribute.Name, attribute.Value);
        foreach (XmlNode child in node.ChildNodes) action.AppendChild(document.ImportNode(child, true));
        source.AppendChild(action);
        var parsed = BehaviorSourceState.ReadSource(source, new BehaviorDomain("Pawn", "", "template"), "template").Contributions.Single();
        if (parsed.RequiredFacts.Length != 0) throw new ArgumentException("PlayerTemplateCannotChangeFixedCondition");
        var checkedValue = new PresetActionValues(parsed.Action, parsed.Values);
        ActionId = checkedValue.ActionId; Values = checkedValue.Values;
    }
}
