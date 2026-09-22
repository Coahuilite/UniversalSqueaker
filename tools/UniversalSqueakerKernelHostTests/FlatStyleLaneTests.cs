using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// S6-2/S6-3 appearance lane: the flat (borderless) style scope. A style scheme that declares a fill's
/// BORDER TOKEN equal to the fill is how "no box" is expressed with this vocabulary - there is no
/// "Border=none" attribute - so this lane asserts BOTH halves: the scheme document's tokens, and the
/// theme a scoped sub-tree actually resolves to.
///
/// <para>
/// MUTATION LEDGER:
/// <list type="number">
/// <item><b>TheSchemeIsDeclared</b> - deleting the <c>us-flat-panel</c> block from <c>UsTheme.SchemeXml</c>
/// reddens the document assertions AND the scope assertion (an unknown scheme falls back to the page
/// level, so the resolved border returns to the blue-grey one).</item>
/// <item><b>TheNavColumnIsScoped</b> - removing <c>Scheme="us-flat-panel"</c> from the manifest reddens
/// the scope assertion while the document one stays green: the two halves are independent, which is why
/// both are asserted.</item>
/// <item><b>AStateStillDiffersFromTheSurface</b> - setting <c>Hover</c>/<c>Selected</c> equal to the fill
/// reddens: a flat scope that also erased the hover/selected difference would be a page with no state
/// feedback, which is NOT what "borderless" means.</item>
/// </list>
/// </para>
/// </summary>
internal static class FlatStyleLaneTests
{
    private const string SchemeName = "us-flat-panel";
    private const string NavColumnId = "nav-column";

    public static int RunAll()
    {
        Step("the flat scheme is declared and its tokens are the flat ones", TheSchemeIsDeclared);
        Step("the nav column resolves the flat theme", TheNavColumnIsScoped);
        Step("every declarative card is scoped flat, and the separators are gone", EveryDeclarativeCardIsFlat);
        Console.WriteLine("FlatStyleLaneTests ALL PASS");
        return 0;
    }

    private static void TheSchemeIsDeclared()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        UiStyleDocument document = host.Manifest.Styles;
        Assert(document.Issues.Count == 0,
            "the shipped style section must parse without dropping a declaration: "
            + string.Join(" | ", document.Issues.Select(i => i.ToString())));
        Assert(document.SchemeNames.Contains(SchemeName),
            "the manifest's <Styles> section must declare the scheme '" + SchemeName + "'; declared: "
            + string.Join(",", document.SchemeNames));
        Assert(document.DefaultScheme == null,
            "the manifest section must stay a page-level DENSITY declaration: it names no default scheme"
            + " (the palette's page level is UsTheme's), got '" + document.DefaultScheme + "'");

        // Apply the scope the way the engine does, over the palette the host was given, and ask the
        // RESOLVED theme rather than the document text.
        UiTheme page = UsTheme.Surface();
        UiTheme flat = new UiStyleResolver(page, document)
            .ThemeFor(new[] { new UiStyleDeclaration(SchemeName) });

        // MUTATION PROOF: a fill whose border token is a different colour draws a 1px frame (that is what
        // UiThemeDraw.Surface does), so equality here is exactly "no box".
        Assert(SameColor(flat.RaisedSurface.Border, flat.Raised),
            "a flat surface must paint no box: RaisedBorder must equal Raised, got border "
            + Hex(flat.RaisedSurface.Border) + " vs fill " + Hex(flat.Raised));
        Assert(SameColor(flat.WorkspacePlane, flat.Raised),
            "the plane behind the flat surface must be the same tone, or the 'card' still reads as a box:"
            + " plane " + Hex(flat.WorkspacePlane) + " vs raised " + Hex(flat.Raised));
        Assert(SameColor(flat.HoverSurface.Border, flat.Hover),
            "the hover surface must be flat too, got border " + Hex(flat.HoverSurface.Border));
        Assert(SameColor(flat.SelectedSurface.Border, flat.Selected),
            "the selected surface must be flat too, got border " + Hex(flat.SelectedSurface.Border));

        // MUTATION PROOF: the flat scope may not erase the state difference.
        Assert(!SameColor(flat.Hover, flat.Raised),
            "hover must still differ from the resting surface: " + Hex(flat.Hover) + " vs " + Hex(flat.Raised));
        Assert(!SameColor(flat.Selected, flat.Raised),
            "selected must still differ from the resting surface: " + Hex(flat.Selected));
        Assert(!SameColor(flat.Selected, flat.Hover),
            "selected and hover must be distinguishable: " + Hex(flat.Selected) + " vs " + Hex(flat.Hover));

        // CONTROL: the page level keeps its bordered values, so the assertions above measure the SCOPE and
        // not a document-wide re-tint.
        Assert(!SameColor(page.RaisedSurface.Border, page.Raised),
            "the page-level palette must keep its bordered Raised surface");
        Console.WriteLine("[flat] " + SchemeName + " raised=" + Hex(flat.Raised)
            + " hover=" + Hex(flat.Hover) + " selected=" + Hex(flat.Selected)
            + " | page raised=" + Hex(page.Raised) + " border=" + Hex(page.RaisedSurface.Border));
    }

    private static void TheNavColumnIsScoped()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        UiElementSpec? column = FindById(host.Manifest.Roots, NavColumnId);
        Assert(column != null, "the manifest must carry '" + NavColumnId + "'");
        Assert(column!.TryGetAttribute("Scheme", out string scheme) && scheme == SchemeName,
            "the nav column must adopt the flat scheme, got '" + scheme + "'");

        // The theme the ENGINE hands the nav widget: the host's own document, applied over the host's theme.
        UiTheme page = UsTheme.Surface();
        UiTheme scoped = new UiStyleResolver(page, host.Manifest.Styles)
            .ThemeFor(new[] { new UiStyleDeclaration(scheme) });
        Assert(SameColor(scoped.RaisedSurface.Border, scoped.Raised),
            "the theme the nav sub-tree draws with must be flat: border "
            + Hex(scoped.RaisedSurface.Border) + " vs " + Hex(scoped.Raised));
        Assert(!SameColor(page.RaisedSurface.Border, page.Raised),
            "control: the page theme is NOT flat, so the assertion above measures the scope and not the"
            + " document as a whole");
    }

    /// <summary>
    /// The cards, not just the nav. The eight declarative Section cards all carry the scope, and the
    /// separator rules are gone - the row bands and each Column's own Gap are the whole body now. Both
    /// halves are asserted on the LIVE manifest, so a scope dropped from one card or a rule re-added
    /// reddens here.
    /// </summary>
    private static void EveryDeclarativeCardIsFlat()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        string[] cards =
        {
            "global-volume", "basic-tuning", "timing", "camera-indicator",
            "attenuation-editor", "race-layer", "xenotype-layer", "checklist-card"
        };
        foreach (string id in cards)
        {
            UiElementSpec? card = FindById(host.Manifest.Roots, id);
            Assert(card != null, "the shipped manifest must carry the card '" + id + "'");
            Assert(card!.Kind == "Section", "'" + id + "' must be the engine's Section container");
            Assert(card.TryGetAttribute("Scheme", out string scheme) && scheme == SchemeName,
                "the declarative card '" + id + "' must adopt the flat scope, got '" + scheme + "'");
        }

        Assert(FindByAttribute(host.Manifest.Roots, "Kind", "chrome/rule") == null,
            "no body separator may survive: the rows separate by whitespace, and the declaration that says"
            + " how much is each Column's own Gap");
    }

    private static UiElementSpec? FindByAttribute(IReadOnlyList<UiElementSpec> roots, string attribute, string value)
    {
        foreach (UiElementSpec spec in roots)
        {
            if (spec.TryGetAttribute(attribute, out string actual)
                && string.Equals(actual, value, StringComparison.Ordinal))
            {
                return spec;
            }

            UiElementSpec? nested = FindByAttribute(spec.Children, attribute, value);
            if (nested != null) return nested;
        }

        return null;
    }

    /// <summary>Exact per-channel equality: the stub Unity has no Color.op_Equality, and a tolerance would
    /// weaken the "no box" claim, which is about the two values being the SAME colour.</summary>
    private static bool SameColor(Color a, Color b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    private static string Hex(Color color)
    {
        return "#" + Channel(color.r) + Channel(color.g) + Channel(color.b) + Channel(color.a);
    }

    private static string Channel(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 255f).ToString("X2");
    }

    private static UiElementSpec? FindById(IReadOnlyList<UiElementSpec> roots, string id)
    {
        foreach (UiElementSpec spec in roots)
        {
            if (string.Equals(spec.Id, id, StringComparison.Ordinal)) return spec;
            UiElementSpec? nested = FindById(spec.Children, id);
            if (nested != null) return nested;
        }

        return null;
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("FlatStyleLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}