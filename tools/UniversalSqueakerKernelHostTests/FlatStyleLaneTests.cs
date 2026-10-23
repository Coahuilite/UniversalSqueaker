using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// UIR-006 (WO-US-UI-REFACTOR-20261014) rewrote this lane. It used to assert the S6-2/S6-3 REGION
/// COLOUR SCOPES: manifest-declared schemes (us-flat-panel / us-command-button) that re-spelled
/// surface borders INSIDE one authored warm-dark baseline. With the six official palettes each owning
/// the complete 24-role token table, those hard-coded region hexes are retired ON PURPOSE: they were
/// palette values wearing a scope's clothes, and leaving them would make one palette switch repaint
/// the page but not the regions. The role values themselves now spell the surface relationships.
///
/// <para>
/// MUTATION LEDGER (the three failures this lane must catch):
/// <list type="number">
/// <item><b>RegionColourScopesAreRetired</b> - a colour <c>&lt;Scheme&gt;</c> block or a colour-scope
/// <c>Scheme=</c> attribute reappearing in the manifest reddens: it would silently re-hard-code one
/// palette into every other palette's page.</item>
/// <item><b>ThePaletteOwnsTheSurfaceRoles</b> - a token the core role table does not declare (or
/// declares with a drifted hex) reddens the bag comparison: this is where the "US owns the complete
/// baseline" check now lives, against the PM-screened role table instead of the old scheme text.</item>
/// <item><b>StatesStaySeparableWithoutRegionScopes</b> - if a palette ever makes the interactive
/// states indistinguishable (identical hover/raised EDGES and fills, or the selected edge no longer
/// answering through the accent fallback), the page loses state feedback; this reddens here rather
/// than in someone's screenshot.</item>
/// </list>
/// </para>
/// </summary>
internal static class FlatStyleLaneTests
{
    private static readonly string[] RetiredColourScopes = { "us-flat-panel", "us-command-button" };

    public static int RunAll()
    {
        Step("the manifest declares no region colour scopes any more", RegionColourScopesAreRetired);
        Step("the core palette carries every surface role on the page bag", ThePaletteOwnsTheSurfaceRoles);
        Step("the six tables equal the INDEPENDENT baseline (OVT-004/005)", TheSixTablesEqualTheIndependentBaseline);
        Step("hover/selected stay distinguishable through palette roles alone", StatesStaySeparableWithoutRegionScopes);
        Console.WriteLine("FlatStyleLaneTests ALL PASS");
        return 0;
    }

    private static void RegionColourScopesAreRetired()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });

        // The host's effective document is the manifest's <Styles> section (no document was attached at
        // construction). It may keep the layout-bearing vocabulary; it must not keep the retired COLOUR
        // schemes - and us-preset-rows stays legal because it declares a font metric, not a colour.
        UiStyleDocument styles = host.StyleResolver.Document;
        foreach (string retired in RetiredColourScopes)
        {
            Assert(!styles.SchemeNames.Contains(retired),
                "the manifest's <Styles> must not declare the retired colour scheme '" + retired
                + "'; declared: " + string.Join(",", styles.SchemeNames));
        }

        Assert(styles.SchemeNames.Contains("us-preset-rows"),
            "us-preset-rows is the layout-bearing (Tiny-font) scope and must survive; got "
            + string.Join(",", styles.SchemeNames));

        // No element may name a retired scope in a Scheme attribute either - an orphan attribute would
        // record an unknown-scheme drop on every pass (the resolver's own issue channel) and prove the
        // migration was half-done.
        var named = new List<string>();
        foreach (UiElementSpec root in host.Manifest.Roots) Walk(root, node =>
        {
            if (node.TryGetAttribute("Scheme", out string scheme) && RetiredColourScopes.Contains(scheme))
            {
                named.Add(node.Id + "=" + scheme);
            }
        });
        Assert(named.Count == 0,
            "no manifest element may reference a retired colour scope; found " + string.Join(" ", named));

        // The resolver records NO unknown-scheme drops for the live page.
        Assert(host.StyleResolver.Issues.Count == 0,
            "the live page must resolve every scope it names; issues: "
            + string.Join("; ", host.StyleResolver.Issues.Select(i => i.Message)));
    }

    private static void ThePaletteOwnsTheSurfaceRoles()
    {
        // CORRECTIONS-R1 topic 4: EVERY official table, not just Core - a silently-dropped hex
        // (parser drop -> the role answers Vanilla while nothing compares) or a drifted value is
        // caught per palette now. document.Issues==0 pins the parse; the bag comparison pins the
        // landing; together they close the "silent drop" hole the r1 core-only pin left open.
        IReadOnlyList<string> tokens = UsTheme.ColorTokens;
        foreach (UsSwatch swatch in UsSwatches.All)
        {
            UiStyleDocument document = UsSwatches.ParsedDocument(swatch.Id);
            Assert(document.Issues.Count == 0,
                swatch.Id + ": the table document must parse with zero drops, got: "
                    + string.Join("; ", document.Issues.Select(i => i.Message)));
            Assert(swatch.RoleHexes.Count == tokens.Count,
                swatch.Id + " must spell exactly the thirty declared roles, got " + swatch.RoleHexes.Count);
            UiTheme bag = UsTheme.Configure(UiTheme.Vanilla, swatch.Id);
            for (int i = 0; i < tokens.Count; i++)
            {
                Assert(SameColor(Resolved(bag, tokens[i]), Parse(swatch.RoleHexes[i])),
                    swatch.Id + ": token '" + tokens[i] + "' must resolve to " + swatch.RoleHexes[i]);
            }
        }

        UsSwatch core = UsSwatches.Resolve(UsSwatches.DefaultId);
        // The eight FHI TPG digital references are SHIPPED DATA (UsSwatches.CoreReferenceColors),
        // value-pinned here - including Bronze Mist #A0824D as the reserved IdentityMinor entry the
        // 24-role map deliberately does NOT consume (the eighth value is kept as a definition).
        Assert(UsSwatches.CoreReferenceColors.Count == 8,
            "the Core reference set must carry all eight published values, got " + UsSwatches.CoreReferenceColors.Count);
        string[] expectedRefs = new[]
        {
            "#434243", "#505557", "#6D6E72", "#D4DDC4", "#A0824D", "#B49C73", "#5E4E3E", "#ECE1BD",
        };
        for (int i = 0; i < expectedRefs.Length; i++)
        {
            Assert(string.Equals(UsSwatches.CoreReferenceColors[i].Hex, expectedRefs[i], StringComparison.OrdinalIgnoreCase),
                "reference colour " + i + " must stay " + expectedRefs[i] + ", got " + UsSwatches.CoreReferenceColors[i].Hex);
        }

        // Bronze Mist is a reserved DEFINITION (IdentityMinor): it appears in NO role slot of any
        // official table - consuming it would silently move a shipped value, which the correction
        // forbids ("not to fill the eighth, change no other role").
        for (int s = 0; s < UsSwatches.All.Count; s++)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                Assert(!string.Equals(UsSwatches.All[s].RoleHexes[i], "#A0824D", StringComparison.OrdinalIgnoreCase),
                    UsSwatches.All[s].Id + ": Bronze Mist must stay reserved, found as role " + tokens[i]);
            }
        }

        // Every 0.8 palette explicitly declares SelectedBorder with its former effective accent.
        // Check the actual resolved surface for all six tables, not only the declaration strings.
        for (int s = 0; s < UsSwatches.All.Count; s++)
        {
            UiTheme bag = UsTheme.Configure(UiTheme.Vanilla, UsSwatches.All[s].Id);
            Assert(bag.Palette.IsDeclared(UiPaletteRole.SelectedBorder),
                UsSwatches.All[s].Id + ": SelectedBorder must be declared with the former effective accent");
            Assert(SameColor(bag.SelectedSurface.Border, bag[UiPaletteRole.Accent]),
                UsSwatches.All[s].Id + ": the selected surface's edge must answer the accent");
        }
    }

    private static void StatesStaySeparableWithoutRegionScopes()
    {
        // Walk EVERY palette: the interactive ladder must stay readable without any region scope. The
        // PM tables keep the HOVER edge visibly stronger than the resting edge, and the SELECTED state
        // separates from both through its explicitly declared accent edge or its own
        // edge; a palette that erased all three differences at once would be a UX defect, so we require
        // (hover edge != raised edge) AND (selected edge != raised edge) for every shipped scheme.
        foreach (UsSwatch swatch in UsSwatches.All)
        {
            UiTheme bag = UsTheme.Configure(UiTheme.Vanilla, swatch.Id);

            Color raisedEdge = bag.RaisedSurface.Border;
            Color hoverEdge = bag.HoverSurface.Border;
            Color selectedEdge = bag.SelectedSurface.Border;
            Assert(!SameColor(hoverEdge, raisedEdge),
                swatch.Id + ": the hover edge must separate from the resting edge without a region scope");
            Assert(!SameColor(selectedEdge, raisedEdge),
                swatch.Id + ": the selected edge must separate from the resting edge");
            // The attention role lives OUTSIDE the palette (UsAttention) and must not be a theme hue:
            // it differs from every identity accent shipped so far.
            foreach (UsSwatch other in UsSwatches.All)
            {
                UiTheme otherBag = UsTheme.Configure(UiTheme.Vanilla, other.Id);
                Assert(!SameColor(UsAttention.Brush, otherBag[UiPaletteRole.Accent]),
                    "the attention brush must not collide with palette '" + other.Id + "'s accent");
            }
        }
    }

    // ---- helpers ----

    private static Color Resolved(UiTheme bag, string token)
    {
        return bag[(UiPaletteRole)Enum.Parse(typeof(UiPaletteRole), token)];
    }

    private static bool SameColor(Color a, Color b)
    {
        const float eps = 1f / 512f;
        return Mathf.Abs(a.r - b.r) < eps && Mathf.Abs(a.g - b.g) < eps
            && Mathf.Abs(a.b - b.b) < eps && Mathf.Abs(a.a - b.a) < eps;
    }

    private static void Walk(UiElementSpec node, Action<UiElementSpec> visit)
    {
        visit(node);
        foreach (UiElementSpec child in node.Children) Walk(child, visit);
    }

    private static Color Parse(string hex)
    {
        // Hand-parsed on purpose: the harness's UnityEngine stub has no ColorUtility, and '#RRGGBB' /
        // '#RRGGBBAA' is the exact spelling vocabulary the style document declares.
        string s = (hex ?? "").Trim();
        if (s.StartsWith("#", StringComparison.Ordinal)) s = s.Substring(1);
        if (s.Length != 6 && s.Length != 8) throw new InvalidOperationException("unreadable hex: " + hex);
        byte r = Convert.ToByte(s.Substring(0, 2), 16);
        byte g = Convert.ToByte(s.Substring(2, 2), 16);
        byte b = Convert.ToByte(s.Substring(4, 2), 16);
        byte a = s.Length == 8 ? Convert.ToByte(s.Substring(6, 2), 16) : (byte)255;
        return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            throw new InvalidOperationException("FlatStyleLaneTests step failed: " + name
                + "\n  INNER: " + error.GetType().Name + "\n  MESSAGE: " + error.Message, error);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void TheSixTablesEqualTheIndependentBaseline()
    {
        foreach (UsSwatch swatch in UsSwatches.All)
        {
            string[] want = ThemeBaseline[swatch.Id];
            Assert(swatch.RoleHexes.Count == want.Length + 6, swatch.Id + ": table width drift");
            for (int i = 0; i < want.Length; i++)
            {
                Assert(string.Equals(swatch.RoleHexes[i], want[i], StringComparison.OrdinalIgnoreCase),
                    swatch.Id + " role " + UsTheme.ColorTokens[i] + " must equal the baseline " + want[i]
                        + ", got " + swatch.RoleHexes[i]);
            }
            string[] derived = { want[22], want[10], want[19], want[8], want[20], want[21] };
            for (int i = 0; i < derived.Length; i++)
                Assert(swatch.RoleHexes[24 + i] == derived[i], swatch.Id + ": explicit 0.8 role must preserve its former effective colour");
        }
    }

    /// <summary>
    /// OVT-004/005 INDEPENDENT baseline (PM: "Core/Royalty/Biotech equal the old baseline,
    /// the new three equal the PM input"): the SIX tables spelled out here from the pre-swap
    /// working-tree capture (THEME-BASELINE-PRE.json) and US-THEME-CANDIDATES-20261015.json r1 -
    /// a silent drift in any single hex reddens this step even though the bag comparison above is
    /// self-referential. Token order = UsTheme.ColorTokens.</summary>
    private static readonly Dictionary<string, string[]> ThemeBaseline = new Dictionary<string, string[]>
    {
        { "core", new[]
        {
            "#434243", "#434243", "#505557", "#505557", "#434243", "#5E4E3E",
            "#5E4E3E", "#1C3D26", "#3F1C1A", "#6D6E72", "#D4DDC4", "#6D6E72",
            "#6D6E72", "#6D6E72", "#6D6E72", "#D4DDC4", "#ECE1BD", "#D4DDC4",
            "#D4DDC4", "#ECE1BD", "#FFDCD6", "#C96057", "#B49C73", "#D4DDC4"
        } },
        { "royalty", new[]
        {
            "#0B1420", "#0B1420", "#151F2E", "#151F2E", "#202B3B", "#2B3748",
            "#463339", "#1C3D26", "#3F1C1A", "#586576", "#8C95A2", "#2B3544",
            "#586576", "#586576", "#586576", "#8C95A2", "#ECEBE7", "#BBC4D0",
            "#A0A9B7", "#F4DEAA", "#FFDCD6", "#C96057", "#E7BB61", "#A0A9B7"
        } },
        { "ideology", new[]
        {
            "#191713", "#191713", "#25211A", "#25211A", "#302B23", "#3B3429",
            "#493C2A", "#1C3D26", "#3F1C1A", "#6A6050", "#B7AA8B", "#3B342A",
            "#6A6050", "#6A6050", "#6A6050", "#B7AA8B", "#E8E0CE", "#C9C0AC",
            "#AAA28E", "#F0E1BC", "#FFDCD6", "#C96057", "#C4A56F", "#AAA28E"
        } },
        { "biotech", new[]
        {
            "#111523", "#111523", "#1B2132", "#1B2132", "#262E43", "#323B53",
            "#4A354F", "#1C3D26", "#3F1C1A", "#626C86", "#A0ABC1", "#30374E",
            "#626C86", "#626C86", "#626C86", "#A0ABC1", "#ECEAF3", "#C7CBDF",
            "#ABB2C7", "#F0CEDC", "#FFDCD6", "#C96057", "#DC9CBA", "#ABB2C7"
        } },
        { "anomaly", new[]
        {
            "#101316", "#101316", "#1A1F23", "#1A1F23", "#252B2F", "#32383C",
            "#384039", "#1C3D26", "#3F1C1A", "#59665F", "#A7B4AB", "#303A37",
            "#59665F", "#59665F", "#59665F", "#A7B4AB", "#E5E8E2", "#BDC7C1",
            "#9FAAA4", "#E0E6CF", "#FFDCD6", "#C96057", "#B8C29D", "#9FAAA4"
        } },
        { "odyssey", new[]
        {
            "#11171C", "#11171C", "#1B252D", "#1B252D", "#28343D", "#34444F",
            "#3D3B55", "#1C3D26", "#3F1C1A", "#627786", "#A1B5C4", "#344650",
            "#627786", "#627786", "#627786", "#A1B5C4", "#E8EDF0", "#BECBD5",
            "#9FADB8", "#DDE6F3", "#FFDCD6", "#C96057", "#A4C8E0", "#9FADB8"
        } },
    };

    }
