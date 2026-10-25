using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace UniversalSqueaker;

/// <summary>Pure embedded colour data, shared by settings repair and the UI. Loaded/validated once;
/// no native types, filesystem polling, mutable registry, or separate picker-id table.</summary>
public static class UsSwatchIds
{
    public const string Default = "core";
    public const string ResourceName = "UniversalSqueaker.Themes.Palettes.xml";
    public static readonly IReadOnlyList<UsSwatchData> Palettes;
    public static readonly string[] All;
    public static readonly IReadOnlyList<string> RoleNames;
    public static readonly string DataError;

    static UsSwatchIds()
    {
        try
        {
            using var stream = typeof(UsSwatchIds).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException("Missing embedded " + ResourceName);
            var root = XDocument.Load(stream).Root ?? throw new InvalidOperationException("Missing palette root");
            var palettes = new List<UsSwatchData>();
            foreach (var element in root.Elements("Palette"))
            {
                string id = Required(element, "Id");
                if (palettes.Any(p => p.Id == id)) throw new InvalidOperationException("Duplicate palette " + id);
                var roles = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var color in element.Elements("Color"))
                {
                    string role = Required(color, "Role");
                    string? from = (string?)color.Attribute("From");
                    string hex = from == null ? Required(color, "Value") : roles.TryGetValue(from, out string found)
                        ? found : throw new InvalidOperationException(id + ": unresolved alias " + from);
                    if (hex.Length != 7 || hex[0] != '#' || hex.Skip(1).Any(c => !Uri.IsHexDigit(c)))
                        throw new InvalidOperationException(id + ": invalid colour " + role);
                    roles.Add(role, hex);
                }
                if (roles.Count != 30) throw new InvalidOperationException(id + ": expected thirty named roles");
                palettes.Add(new UsSwatchData(id, Required(element, "LabelKey"), Required(element, "NoteKey"), roles));
            }
            if (palettes.Count != 3 || palettes[0].Id != Default)
                throw new InvalidOperationException("Expected three palettes, core first");
            Palettes = palettes.AsReadOnly();
            All = palettes.Select(p => p.Id).ToArray();
            RoleNames = root.Elements("Palette").First().Elements("Color").Select(c => Required(c, "Role")).ToArray();
            if (palettes.Any(p => RoleNames.Any(role => !p.Roles.ContainsKey(role))))
                throw new InvalidOperationException("Palettes disagree on named roles");
            DataError = "";
        }
        catch (Exception error)
        {
            // Settings must still load. The native UI reports this one actionable failure and uses
            // the carrier's safe colour defaults instead of throwing from a window constructor.
            Palettes = Array.Empty<UsSwatchData>(); All = new[] { Default }; RoleNames = Array.Empty<string>(); DataError = error.Message;
        }
    }
    private static string Required(XElement e, string name) => (string?)e.Attribute(name)
        ?? throw new InvalidOperationException("Missing " + name + " on " + e.Name);
    public static bool IsKnown(string id) => Array.IndexOf(All, id) >= 0;
}

public sealed class UsSwatchData
{
    internal UsSwatchData(string id, string label, string note, IDictionary<string, string> roles)
    { Id = id; LabelKey = label; NoteKey = note; Roles = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(roles); }
    public string Id { get; }
    public string LabelKey { get; }
    public string NoteKey { get; }
    public IReadOnlyDictionary<string, string> Roles { get; }
}
