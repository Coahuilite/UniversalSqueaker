using System;
using System.Collections.Generic;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker;

/// <summary>US owns thirty semantic colour roles through FL 0.8's public Palette.
/// The six swatches preserve the former effective colours, including explicit selected/success
/// borders and independent warning roles. Font, density and widget state belong to their
/// existing owners; changing a swatch writes colours only and keeps layout/scroll/focus/draft.</summary>
public static class UsTheme
{
    /// <summary>
    /// The shipped default swatch's identity accent, spelled the way the table spells it
    /// (<c>#B49C73</c>, the Core Bronze/Lark family member). Named separately because the surface lane
    /// pins the accent against THIS constant rather than against a carrier swatch. Every swatch owns
    /// its own accent through the token; this constant documents the default identity.
    /// </summary>
    public const string AccentGoldHex = "#B49C73";

    /// <summary>All thirty concrete FL 0.8 palette roles; order matches UsSwatch.RoleHexes.</summary>
    public static readonly IReadOnlyList<string> ColorTokens = new[]
    {
        "WindowSurface", "WorkspaceSurface", "PanelSurface", "SectionSurface", "ControlSurface", "HoverSurface", "SelectedSurface",
        "SuccessSurface", "DangerSurface",
        "Border", "StrongBorder", "Divider",
        "WindowBorder", "PanelBorder", "ControlBorder", "HoverBorder",
        "Foreground", "SecondaryForeground", "DisabledForeground", "SelectedForeground", "DangerForeground",
        "DangerBorder",
        "Accent", "SwitchOff",
        "SelectedBorder", "SuccessBorder", "SuccessForeground",
        "WarningSurface", "WarningForeground", "WarningBorder"
    };

    /// <summary>The persisted swatch in force (fail-soft: null settings or an unknown id resolve to
    /// Core). This is the ONE read every surface takes; the writing side goes through the settings
    /// facade so persistence and the live repaint cannot disagree.</summary>
    public static string ActiveSwatchId
    {
        get
        {
            string id = UniversalSqueakerMod.Settings?.uiSwatchId ?? "";
            return UsSwatches.IsKnown(id) ? id : UsSwatches.DefaultId;
        }
    }

    // --- the one shared chrome bag (settings window title/close, dev panel, confirm dialog) ---
    /// <summary>
    /// The process's ONE shared chrome theme bag. The window SHELL chrome (the settings window's own
    /// title bar and close affordance, the dev panel, the confirm dialog) draws from a bag that is
    /// separate from any page host's bag: the page bag is repainted on a swatch switch by
    /// <see cref="ApplyToLive"/>, and this bag is repainted by <see cref="ReapplyChromeSwatch"/>. It
    /// is shared rather than per-window on purpose - the swatch is a page-wide decision, these bags
    /// carry no per-widget mutation (only the swatch writer touches them), so one bag cannot let one
    /// window "paint another" the way two sharing HOST bags would. A fresh Surface() still hands each
    /// HOST its own bag (the resolver mutates those); a window chrome that wants to FOLLOW a live
    /// switch reads this one.
    /// </summary>
    private static UiTheme? chromeTheme;

    public static UiTheme ChromeTheme
    {
        get
        {
            UiTheme bag = chromeTheme ??= Configure(UiTheme.Vanilla, ActiveSwatchId);
            return bag;
        }
    }

    /// <summary>Repaint the shared chrome bag to the CURRENTLY ACTIVE swatch. The swatch write path
    /// (the source facade) calls this right after it repaints the attached page host's own bag, so a
    /// single switch lands on the page and the chrome together - colours only, no host or window
    /// rebuild. Reading the live ActiveSwatchId (not a passed id) keeps the two repaints from ever
    /// naming different palettes.</summary>
    public static void ReapplyChromeSwatch()
    {
        UiTheme bag = chromeTheme ??= Configure(UiTheme.Vanilla, ActiveSwatchId);
        ApplyToLive(bag, ActiveSwatchId);
    }

    /// <summary>The active swatch's resolved entry (never null, never unknown).</summary>
    public static UsSwatch ActiveSwatch => UsSwatches.Resolve(ActiveSwatchId);

    /// <summary>
    /// The DEFAULT swatch's style document text — the surface the swatch lanes read out and compare
    /// the resolved values against. Generated, so it cannot drift from the table.
    /// </summary>
    public static string SchemeXml => UsSwatches.DocumentFor(UsSwatches.Resolve(UsSwatches.DefaultId));

    /// <summary>
    /// A fresh instance, one per window that needs one — the same contract as the carrier's template
    /// (which hands out a new bag per call). A theme is mutated at runtime (a host applies a style
    /// document's page level into it, and a swatch switch re-tints in place), so two windows sharing
    /// one bag would paint each other. The bag is tinted with the ACTIVE swatch.
    /// </summary>
    public static UiTheme Surface()
    {
        return Configure(UiTheme.Vanilla, ActiveSwatchId);
    }

    /// <summary>Applies the ACTIVE swatch to <paramref name="theme"/> in place (fail-soft contract as
    /// before). Retained for the historical call shape; every surface already passes an id.</summary>
    public static UiTheme Configure(UiTheme theme)
    {
        return Configure(theme, ActiveSwatchId);
    }

    /// <summary>Apply only the selected swatch's thirty public palette declarations; preserve metrics and font.</summary>
    public static UiTheme Configure(UiTheme theme, string swatchId)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        // A swatch owns colours only. Applying a whole style document would also withdraw
        // the page's current font/density applications in 0.8; keep those declarations intact.
        UsSwatch swatch = UsSwatches.Resolve(swatchId);
        for (int i = 0; i < ColorTokens.Count; i++)
        {
            if (!UiPalette.TryRole(ColorTokens[i], out var role) || !UiColour.TryParse(swatch.RoleHexes[i], out var colour))
                throw new InvalidOperationException("Invalid US swatch role: " + ColorTokens[i]);
            theme.Palette[role] = colour;
        }
        return theme;
    }

    /// <summary>Repaint this retained theme bag without withdrawing page styles or rebuilding a Host.</summary>
    public static void ApplyToLive(UiTheme theme, string swatchId)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        Configure(theme, swatchId);
    }

    /// <summary>
    /// Everything wrong with any parsed swatch document, discovered when it is read: the carrier
    /// parser's own drops (unknown token, unreadable colour, duplicate). US adds nothing to them.
    /// A lane asserts the list is empty, which is what keeps "a dropped declaration is never silent"
    /// a checkable fact for every swatch in the table.
    /// <para>
    /// DECLARED BEFORE <see cref="Scheme"/> ON PURPOSE: static field initializers run in textual
    /// order and <see cref="LoadScheme"/> writes into this list, so a later declaration would hand
    /// it a null list and the type initializer would fail before any surface was built - the
    /// fail-soft error path would become a hard crash. The original R2 file carried this ordering
    /// note; the palette rewrite restored the declarations but not the order, and the
    /// CORRECTIONS-R1 pass puts the guarantee back where the guard comment can point at it.
    /// </para>
    /// </summary>
    private static readonly List<string> schemeIssues = new();

    /// <summary>The parsed public default-swatch document, validated separately from live colour writes.</summary>
    public static UiStyleDocument Scheme { get; } = LoadScheme();

    public static IReadOnlyList<string> SchemeIssues => schemeIssues;

    private static UiStyleDocument LoadScheme()
    {
        try
        {
            UiStyleDocument document = UiStyleDocument.Parse(SchemeXml);
            for (int i = 0; i < document.Issues.Count; i++)
            {
                schemeIssues.Add("parser: " + document.Issues[i].Message);
            }

            return document;
        }
        catch (Exception error)
        {
            Verse.Log.Error("[US] the generated us-root style document did not parse; surfaces fall back to the"
                + " carrier's Vanilla template: " + error.Message);
            schemeIssues.Add("parse failure: " + error.Message);
            return UiStyleDocument.Empty;
        }
    }
}
