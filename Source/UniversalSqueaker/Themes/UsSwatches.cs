using System;
using System.Collections.Generic;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker;

/// <summary>
/// One official US surface swatch: an identity, a Keyed label, a short author note, and the COMPLETE
/// colour-role table the US baseline must declare (the same 24-token list <see cref="UsTheme"/> pins —
/// <c>SelectedBorder</c> is deliberately absent from every swatch: its contract is the accent fallback
/// <c>SelectedBorder ?? AccentGold</c>, and a flat declaration would alias that fallback away).
///
/// <para>
/// The role tables are the PM-screened implementation input
/// <c>modding_documents/us-fl-development/ProjectManager/palettes/US-OFFICIAL-PALETTES-20261014.json</c>
/// r1 (collection <c>us-official-six-palettes</c>; source collection r3
/// <c>87cf6a4b…3cfc60f</c>). Core's eight reference values are the published FHI TPG digital references
/// quoted verbatim by that input — retailer digital HEX, not a Pantone Connect export and not a physical
/// match; no image sampling was used for Core. The five DLC palettes are trial-swatch schemes kept for
/// colour validation. This file carries ONLY the colour values and the author's own notes; no personal
/// machine paths and no provenance records beyond the relative repository pointer above.
/// </para>
///
/// <para>
/// A swatch is PURE COLOUR (the UsTheme ruling): no font, no density, no metric. The layout-bearing
/// vocabulary stays in the layout manifest's own <c>&lt;Styles&gt;</c> section and in the carrier.
/// Selecting a swatch therefore repaints and never re-measures, which is what makes a swatch switch a
/// colour-only invalidation instead of a layout event.
/// </para>
/// </summary>
public sealed class UsSwatch
{
    internal UsSwatch(string id, string labelKey, string noteKey, string authorNote, IReadOnlyList<string> roleHexes)
    {
        Id = id;
        LabelKey = labelKey;
        AuthorNote = authorNote;
        // CORRECTIONS-R3: the note key is DECLARED, not composed - the old
        // `labelKey + ".Note"` spelled US.Theme.Core.Label.Note while both shipped tables carry
        // "US.Theme.Core.Note", so the author-note binding painted a raw key literal in game. The
        // real-source action chain (ThemeSwitch lane) caught it; a composed key must never drift again.
        NoteKey = noteKey;
        RoleHexes = roleHexes;
    }

    /// <summary>Machine token persisted in settings and compared ordinally. Never translated.</summary>
    public string Id { get; }

    /// <summary>Keyed display-name entry (the library ships zero strings; this is US's own vocabulary).</summary>
    public string LabelKey { get; }

    /// <summary>Short author note (English source text, kept as data and documentation). The UI shows
    /// the Keyed <see cref="NoteKey"/> entry instead, so each language carries its own wording.</summary>
    public string AuthorNote { get; }

    /// <summary>Keyed short author note (zh authoritative, en translated) - the picker's explanation line.</summary>
    public string NoteKey { get; }

    /// <summary>The 24 declared values, in the exact order of <see cref="UsTheme.ColorTokens"/>.</summary>
    public IReadOnlyList<string> RoleHexes { get; }

    public override string ToString() => Id;
}

/// <summary>One published FHI TPG digital-reference colour of the Core set: its Pantone code, name,
/// retailer digital HEX, and the role the screened map assigns it (IdentityMinor = reserved identity
/// accent, deliberately not consumed by any of the 24 FL role slots - the eighth value is kept as a
/// DEFINITION, not squeezed into another role).</summary>
public readonly struct UsSwatchReferenceColor
{
    public UsSwatchReferenceColor(string code, string name, string hex, string usage)
    {
        Code = code;
        Name = name;
        Hex = hex;
        Usage = usage;
    }

    public readonly string Code;
    public readonly string Name;
    public readonly string Hex;
    public readonly string Usage;
}

/// <summary>
/// The six official US palettes (Core default + five DLC trial schemes) and the one place that turns a
/// swatch into the <c>us-root</c> style document <see cref="UsTheme"/> applies. Selection is a persisted
/// US setting; author-custom themes remain available through the same carrier document mechanism — this
/// table is US consumer content and stays out of the library (FL neutrality holds).
/// </summary>
public static class UsSwatches
{
    /// <summary>The shipped default. The WO overrides the historical collection's <c>default=null</c>.</summary>
    public const string DefaultId = UsSwatchIds.Default;

    private static readonly string[] CoreHexes =
    {
        // 19-0303 Jet Black / 19-3906 Dark Shadow / 18-4006 Quiet Shade / 12-0106 Meadow Mist /
        // 17-0843 Bronze Mist / 16-1324 Lark / 19-0815 Desert Palm / 12-0806 Rutabaga — the eight exact
        // FHI TPG digital references, plus the semantic slots the screened role map assigns.
        "#434243", "#434243", "#505557", "#505557", "#434243", "#5E4E3E", "#5E4E3E",
        "#1C3D26", "#3F1C1A",
        "#6D6E72", "#D4DDC4", "#6D6E72",
        "#6D6E72", "#6D6E72", "#6D6E72", "#D4DDC4",
        "#ECE1BD", "#D4DDC4", "#D4DDC4", "#ECE1BD", "#FFDCD6",
        "#C96057",
        "#B49C73",
        "#D4DDC4",
    };

    private static readonly string[] RoyaltyHexes =
    {
        "#0B1420", "#0B1420", "#151F2E", "#151F2E", "#202B3B", "#2B3748", "#463339",
        "#1C3D26", "#3F1C1A",
        "#586576", "#8C95A2", "#2B3544",
        "#586576", "#586576", "#586576", "#8C95A2",
        "#ECEBE7", "#BBC4D0", "#A0A9B7", "#F4DEAA", "#FFDCD6",
        "#C96057",
        "#E7BB61",
        "#A0A9B7",
    };

    private static readonly string[] IdeologyHexes =
    {
        "#191713", "#191713", "#25211A", "#25211A", "#302B23", "#3B3429", "#493C2A",
        "#1C3D26", "#3F1C1A",
        "#6A6050", "#B7AA8B", "#3B342A",
        "#6A6050", "#6A6050", "#6A6050", "#B7AA8B",
        "#E8E0CE", "#C9C0AC", "#AAA28E", "#F0E1BC", "#FFDCD6",
        "#C96057",
        "#C4A56F",
        "#AAA28E",
    };

    private static readonly string[] BiotechHexes =
    {
        "#111523", "#111523", "#1B2132", "#1B2132", "#262E43", "#323B53", "#4A354F",
        "#1C3D26", "#3F1C1A",
        "#626C86", "#A0ABC1", "#30374E",
        "#626C86", "#626C86", "#626C86", "#A0ABC1",
        "#ECEAF3", "#C7CBDF", "#ABB2C7", "#F0CEDC", "#FFDCD6",
        "#C96057",
        "#DC9CBA",
        "#ABB2C7",
    };

    private static readonly string[] AnomalyHexes =
    {
        "#101316", "#101316", "#1A1F23", "#1A1F23", "#252B2F", "#32383C", "#384039",
        "#1C3D26", "#3F1C1A",
        "#59665F", "#A7B4AB", "#303A37",
        "#59665F", "#59665F", "#59665F", "#A7B4AB",
        "#E5E8E2", "#BDC7C1", "#9FAAA4", "#E0E6CF", "#FFDCD6",
        "#C96057",
        "#B8C29D",
        "#9FAAA4",
    };

    private static readonly string[] OdysseyHexes =
    {
        "#11171C", "#11171C", "#1B252D", "#1B252D", "#28343D", "#34444F", "#3D3B55",
        "#1C3D26", "#3F1C1A",
        "#627786", "#A1B5C4", "#344650",
        "#627786", "#627786", "#627786", "#A1B5C4",
        "#E8EDF0", "#BECBD5", "#9FADB8", "#DDE6F3", "#FFDCD6",
        "#C96057",
        "#A4C8E0",
        "#9FADB8",
    };

    /// <summary>
    /// Every table document parses with ZERO carrier-parser issues - an unspellable hex or an
    /// unknown-token drift can be silently DROPPED by the style document, which would leave the
    /// affected role answering Vanilla while the value comparison on the resolved bag never ran.
    /// FlatStyleLaneTests pins all six tables' resolved values AND their document.Issues; this
    /// process-start guard re-checks the parse silently (it cannot throw - Surface() would die),
    /// and UsTheme.SchemeIssues stays the visible channel for the DEFAULT document.
    /// </summary>
    static UsSwatches()
    {
        for (int i = 0; i < table.Length; i++)
        {
            UiStyleDocument document = UiStyleDocument.Parse(DocumentFor(table[i]));
            if (document.Issues.Count == 0) continue;
            Verse.Log.Error("[US] official swatch document '" + table[i].Id + "' parsed with "
                + document.Issues.Count + " drops - the role table will not fully apply; first: "
                + document.Issues[0].Message);
        }
    }

    private static readonly UsSwatch[] table =
    {
        new UsSwatch("core", "US.Theme.Core.Label", "US.Theme.Core.Note",
            "Still-working terminal on a lost colony ship: muted grey space, old bronze interaction, "
            + "pale ink and parchment accents. Small text is never laid on Quiet Shade / Bronze Mist / "
            + "Lark; required states ride Rutabaga markers, switch position and text together, and the "
            + "accent alone never carries a required mark.",
            CoreHexes),
        new UsSwatch("royalty", "US.Theme.Royalty.Label", "US.Theme.Royalty.Note",
            "Night-blue space with gold actions and a dark wine-red selected plate; the series keeps its "
            + "warm-gold identity, now carried by cool blue.",
            RoyaltyHexes),
        new UsSwatch("ideology", "US.Theme.Ideology.Label", "US.Theme.Ideology.Note",
            "Charcoal and earthen surfaces, bone-white text and restrained ochre.",
            IdeologyHexes),
        new UsSwatch("biotech", "US.Theme.Biotech.Label", "US.Theme.Biotech.Note",
            "Indigo surfaces with soft rose action marks; bright cyan is reserved for the attention role.",
            BiotechHexes),
        new UsSwatch("anomaly", "US.Theme.Anomaly.Label", "US.Theme.Anomaly.Note",
            "Cold grey and charcoal carry pale text with a restrained lichen accent.",
            AnomalyHexes),
        new UsSwatch("odyssey", "US.Theme.Odyssey.Label", "US.Theme.Odyssey.Note",
            "Dark metal, ice-blue engine light and muted blue-violet glowforest surfaces.",
            OdysseyHexes),
    };

    /// <summary>
    /// The Core scheme's eight published FHI TPG digital-reference values, VERBATIM from the PM input
    /// (retailer digital HEX; not a Pantone Connect export, not a physical match; no image sampling).
    /// Seven are consumed by the 24-role map; Bronze Mist is reserved as the IdentityMinor accent -
    /// a DEFINITION kept so the shipped eight are inspectable, deliberately NOT squeezed into another
    /// role slot to make a count. The palette lane pins all eight code/name/HEX/usage tuples.
    /// </summary>
    public static readonly IReadOnlyList<UsSwatchReferenceColor> CoreReferenceColors =
        new UsSwatchReferenceColor[]
        {
            new UsSwatchReferenceColor("19-0303 TPG", "Jet Black", "#434243",
                "Base / WorkspacePlane / Raised - the muted grey space"),
            new UsSwatchReferenceColor("19-3906 TPG", "Dark Shadow", "#505557",
                "Panel / SectionBand - the lifted plane"),
            new UsSwatchReferenceColor("18-4006 TPG", "Quiet Shade", "#6D6E72",
                "Border family + Divider - structure lines; never a small-text ground"),
            new UsSwatchReferenceColor("12-0106 TPG", "Meadow Mist", "#D4DDC4",
                "BorderStrong / HoverBorder / TextSecondary / TextDisabled / SwitchThumbOff"),
            new UsSwatchReferenceColor("17-0843 TPG", "Bronze Mist", "#A0824D",
                "IdentityMinor - reserved identity accent, not consumed by a role slot"),
            new UsSwatchReferenceColor("16-1324 TPG", "Lark", "#B49C73",
                "AccentGold - the interaction identity; never a small-text ground"),
            new UsSwatchReferenceColor("19-0815 TPG", "Desert Palm", "#5E4E3E",
                "Hover / Selected - the warm interaction plate"),
            new UsSwatchReferenceColor("12-0806 TPG", "Rutabaga", "#ECE1BD",
                "TextPrimary / TextOnGold - pale ink; required-state marker ground"),
        };

    /// <summary>Every official swatch, default first. The ids are the machine tokens the picker writes.</summary>
    public static IReadOnlyList<UsSwatch> All => table;

    /// <summary>True for a known swatch id - the decision lives in the pure <see cref="UsSwatchIds"/>
    /// set (settings-side, no theme engine), and the table is built over the same ids; a drift between
    /// the two is a lane failure, never a silent disagreement.</summary>
    public static bool IsKnown(string id) => UsSwatchIds.IsKnown(id);

    /// <summary>The swatch a persisted id names, falling back to <see cref="DefaultId"/>. Fail-soft:
    /// a hand-edited or stale setting value must not take the settings window down; the shipped look is
    /// still the answer.</summary>
    public static UsSwatch Resolve(string id)
    {
        for (int i = 0; i < table.Length; i++)
        {
            if (string.Equals(table[i].Id, id, StringComparison.Ordinal)) return table[i];
        }

        return table[0];
    }

    /// <summary>
    /// The swatch as the <c>us-root</c> style document: the same shape the historical embedded
    /// baseline was, generated from the role table instead of hand-written, so one fact (a colour) lives
    /// in exactly one place. The scheme NAME stays <c>us-root</c> for every swatch: the name is the
    /// page-level contract, the values are the theme.
    /// </summary>
    public static string DocumentFor(UsSwatch swatch)
    {
        if (swatch == null) throw new ArgumentNullException(nameof(swatch));
        var builder = new System.Text.StringBuilder(2560);
        builder.Append("<Styles Schema='1' Scheme='us-root'><Scheme Name='us-root'>");
        IReadOnlyList<string> tokens = UsTheme.ColorTokens;
        for (int i = 0; i < tokens.Count; i++)
        {
            builder.Append("<Color Token='").Append(tokens[i]).Append("' Value='")
                .Append(swatch.RoleHexes[i]).Append("' />");
        }

        builder.Append("</Scheme></Styles>");
        return builder.ToString();
    }

    /// <summary>The document for a persisted id (resolved fail-soft). Parsed once per swatch and
    /// cached here: the tables are constants, so there is nothing to re-read and no clock to invent.</summary>
    public static UiStyleDocument ParsedDocument(string id)
    {
        return CacheFor(Resolve(id));
    }

    private static readonly Dictionary<string, UiStyleDocument> parsed =
        new(StringComparer.Ordinal);

    private static UiStyleDocument CacheFor(UsSwatch swatch)
    {
        if (parsed.TryGetValue(swatch.Id, out UiStyleDocument? document)) return document;
        UiStyleDocument parsedDocument = UiStyleDocument.Parse(DocumentFor(swatch));
        parsed.Add(swatch.Id, parsedDocument);
        return parsedDocument;
    }
}
