using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker;

/// <summary>A colour-only projection of one embedded named-role palette.</summary>
public sealed class UsSwatch
{
    internal UsSwatch()
    {
        Id = "core"; LabelKey = "US.Theme.Core.Label"; NoteKey = "US.Theme.Core.Note";
        RoleHexes = Array.Empty<string>(); Colors = new Dictionary<UiPaletteRole, UiColour>(); Document = UiStyleDocument.Empty;
    }
    internal UsSwatch(UsSwatchData data)
    {
        Id = data.Id; LabelKey = data.LabelKey; NoteKey = data.NoteKey;
        RoleHexes = UsSwatchIds.RoleNames.Select(role => data.Roles[role]).ToArray();
        var colors = new Dictionary<UiPaletteRole, UiColour>();
        foreach (var pair in data.Roles)
        {
            if (!UiPalette.TryRole(pair.Key, out var role) || !UiColour.TryParse(pair.Value, out var color))
                throw new InvalidOperationException("Invalid US palette role " + data.Id + "/" + pair.Key);
            colors.Add(role, color);
        }
        Colors = colors;
        Document = UiStyleDocument.Parse(UsSwatches.DocumentFor(this));
        if (Document.Issues.Count != 0) throw new InvalidOperationException(data.Id + ": " + Document.Issues[0].Message);
    }
    public string Id { get; }
    public string LabelKey { get; }
    public string NoteKey { get; }
    public IReadOnlyList<string> RoleHexes { get; }
    internal IReadOnlyDictionary<UiPaletteRole, UiColour> Colors { get; }
    internal UiStyleDocument Document { get; }
    public override string ToString() => Id;
}

/// <summary>One process-load projection of Palettes.xml. Picker, settings IDs and render colours share
/// that resource. No per-frame IO, style parse or string-to-colour conversion.</summary>
public static class UsSwatches
{
    public const string DefaultId = UsSwatchIds.Default;
    private static readonly UsSwatch[] table;
    static UsSwatches()
    {
        try
        {
            if (UsSwatchIds.DataError.Length != 0) throw new InvalidOperationException(UsSwatchIds.DataError);
            table = UsSwatchIds.Palettes.Select(data => new UsSwatch(data)).ToArray();
        }
        catch (Exception error)
        {
            table = new[] { new UsSwatch() };
            Verse.Log.Error("[US] Palettes.xml is invalid; reinstall the mod to restore its embedded colour data. Using carrier defaults: " + error.Message);
        }
    }
    public static IReadOnlyList<UsSwatch> All => table;
    public static bool IsKnown(string id) => UsSwatchIds.IsKnown(id);
    public static UsSwatch Resolve(string id) => table.FirstOrDefault(p => p.Id == id) ?? table.First(p => p.Id == DefaultId);
    public static string DocumentFor(UsSwatch swatch)
    {
        var builder = new StringBuilder("<Styles Schema='1' Scheme='us-root'><Scheme Name='us-root'>");
        if (swatch.RoleHexes.Count == 0) return builder.Append("</Scheme></Styles>").ToString();
        for (int i = 0; i < UsSwatchIds.RoleNames.Count; i++)
            builder.Append("<Palette Role='").Append(UsSwatchIds.RoleNames[i]).Append("' Value='").Append(swatch.RoleHexes[i]).Append("' />");
        return builder.Append("</Scheme></Styles>").ToString();
    }
    public static UiStyleDocument ParsedDocument(string id) => table.Length == 0 ? UiStyleDocument.Empty : Resolve(id).Document;
}
