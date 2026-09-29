using System;
using System.Collections.Generic;
using System.Xml;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker;

/// <summary>
/// US's surface tokens, resolved from ONE style document instead of a C# assignment list (redesign S2).
///
/// <para>
/// <b>US owns the COMPLETE baseline (R2).</b> The table used to start from the carrier's
/// <c>UiTheme.DarkGold</c> and declare only the tokens S6-3 re-tinted, which made US's look a PARTIAL
/// overlay on a peer palette the carrier was free to retire. It is not: the carrier ships <c>Vanilla</c>
/// only, so every colour the DarkGold template used to contribute — including <c>AccentGold</c> and the
/// four per-surface edges its fallbacks resolved to — is declared HERE. The starting point is
/// <see cref="UiTheme.Vanilla"/>, and <see cref="ColorTokens"/> is the list that rule is checked over: after
/// <see cref="Configure"/>, no token on the bag still carries a vanilla value. Two instruments enforce it —
/// the document/list agreement and the resolved-value comparison in the palette lane — rather than a flag on
/// this type, because the rule is about the DOCUMENT the lane reads, not a claim the type makes about itself.
/// </para>
///
/// <para>
/// <b>Missing colour vs explicitly transparent (R2 item 4).</b> The two are DIFFERENT and both are
/// reachable: a token the document does not mention keeps the baseline's value (that is the "missing colour
/// ⇒ the carrier's <c>Vanilla</c>" rule, and it is the fail-soft path), while a token declared as
/// <c>#RRGGBB00</c> is a real, authored answer that resolves to a fully transparent colour. A palette that
/// wants "this surface paints nothing" therefore has a spelling; a palette that omits a token has a
/// different one. The lane asserts the distinction on a real document rather than describing it.
/// </para>
///
/// <para>
/// <b>A palette is pure colour.</b> This document declares <see cref="ColorTokens"/> and nothing else — no
/// <c>&lt;Font&gt;</c>, no <c>&lt;Density&gt;</c>/<c>&lt;Metric&gt;</c>. Font and density are the two
/// LAYOUT-BEARING theme axes (moving either one advances <see cref="UiTheme.LayoutRevision"/>), so mixing
/// them into the palette would make "switch the palette" silently re-measure the page. Geometry belongs to
/// the layout manifest's own <c>&lt;Styles&gt;</c> section (the two density tiers live there), and the
/// carrier owns the font vocabulary AND its legacy redirect (<c>UiStyleDocument</c>). Selecting the palette
/// alone therefore applies no other theme default, and the lane asserts both halves of that.
/// </para>
///
/// <para>
/// Two recorded trade-offs survive the move, because they are product rulings and not formatting:
/// <list type="bullet">
/// <item><c>Hover</c> takes the spec's control/hover plane. One token serves hovered ROWS (the many) and the
/// shell's close button (the one); the spec gives those two different steps (s2 vs s3), and with one token
/// serving both, rows win because they are the surface users scan. The button-hover step stays unrepresented
/// until the library grows a second token or a later batch lands one.</item>
/// <item><b><c>AccentGold</c> is now declared HERE and its value is unchanged</b> (RULED 2026-09-24: the
/// series gold IS the identity). It used to arrive with the carrier's DarkGold template, which is exactly
/// the inheritance this change removes — the hex below is the SAME value that template carried, so the
/// accent does not move. There is still no attention token here on purpose:
/// <c>UiTheme.Warning</c> is the carrier's redirect onto <c>Danger</c>, so US carries the attention role in
/// <see cref="UsAttention"/> instead.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>The four per-surface edges are CLAIMED at the shared token's own value.</b> The two candidate readings
/// are "declare them" and "leave them unclaimed and let <c>?? Border</c> / <c>?? BorderStrong</c> answer"; both
/// paint the same pixels today, so the choice is about what the next edit does. Claimed wins for two reasons.
/// First, <b>the committed effective appearance</b>: at HEAD every per-surface edge was unclaimed and resolved
/// to US's own <c>Border = #575247</c> / <c>BorderStrong = #6b6459</c>, so those are the colours US shipped,
/// and a lane can pin them. Second, <b>the baseline is now owned</b>: leaving them unclaimed would make four
/// surfaces' edges an incidental function of <see cref="UiTheme.Vanilla"/>'s per-token fallback, which is the
/// inheritance this file exists to remove, and a later edit to <c>Border</c> would move them without saying so.
/// The cost of claiming is stated with it: a flat scope that re-tints only <c>Border</c> no longer reaches
/// these four, which is why the lane asserts the claimed values EQUAL the shared tokens rather than freezing
/// the hexes. <c>SelectedBorder</c> is deliberately NOT declared: it is the one surface whose fallback is the
/// ACCENT (<c>SelectedBorder ?? AccentGold</c>), and a flat style scope aliases that fallback away by setting
/// it equal to <c>Selected</c> — the equality that IS "a flat surface paints no box".
/// </para>
///
/// <para>
/// Public because the kernel-host harness references the built mod assembly rather than linking this source;
/// internal would make the table unobservable from the only place that can assert it.
/// </para>
/// </summary>
public static class UsTheme
{
    /// <summary>
    /// The US baseline palette, as a style document. Single-quoted XML attributes on purpose: this is a C#
    /// verbatim string and XML allows them, so the table reads exactly as it will read in the loose file S5
    /// adds.
    ///
    /// <para>
    /// Values: the S6-3 warm re-tint for every token it moved, and — for the tokens it left alone — the value
    /// that token had in the effective US look before this file owned the baseline. That second group is
    /// <c>Success</c>, <c>AccentGold</c>, and the four per-surface edges, and for the edges the effective
    /// value was NOT the carrier template's own border (<c>#333330</c>): at HEAD US started from DarkGold with
    /// every per-surface edge null and then applied <c>Border = #575247</c> / <c>BorderStrong = #6b6459</c>,
    /// so each edge resolved through <c>?? Border</c> / <c>?? BorderStrong</c> to US's OWN shared token.
    /// Writing out the pre-override template value here would have silently moved four surfaces' edges, which
    /// is exactly what this correction round fixes; nothing in the document is invented.
    /// </para>
    /// </summary>
    public const string SchemeXml =
        @"<Styles Schema='1' Scheme='us-root'>"
        + @"<Scheme Name='us-root'>"
        // --- surfaces (S6-3 warm dark) ---
        + @"<Color Token='Base' Value='#0e0d0c' />"
        + @"<Color Token='WorkspacePlane' Value='#0e0d0c' />"
        + @"<Color Token='Panel' Value='#171512' />"
        + @"<Color Token='SectionBand' Value='#171512' />"
        + @"<Color Token='Raised' Value='#1d1b17' />"
        + @"<Color Token='Hover' Value='#242019' />"
        + @"<Color Token='Selected' Value='#3a311f' />"
        // --- the un-re-tinted slots: the retired DarkGold template's own values ---
        + @"<Color Token='Success' Value='#1c3d26' />"
        + @"<Color Token='Danger' Value='#3f1c1a' />"
        // --- structure ---
        + @"<Color Token='Border' Value='#575247' />"
        + @"<Color Token='BorderStrong' Value='#6b6459' />"
        + @"<Color Token='Divider' Value='#2a2620' />"
        // The four per-surface edges are WRITTEN OUT at the value US's own shared tokens give them, which is
        // the value US painted before this file owned the baseline: at HEAD `Surface()` started from the
        // carrier's DarkGold template with every per-surface edge NULL, and US's applied scheme answered
        // `Border = #575247` / `BorderStrong = #6b6459`, so each surface resolved through `?? Border` /
        // `?? BorderStrong` to exactly these colours. Claiming them (rather than leaving them unclaimed and
        // re-deriving the same answer at runtime) is what makes the "complete owned baseline" true rather
        // than incidental: a future edit to `Border` cannot silently move four other surfaces' edges without
        // reddening the lane that pins the agreement. SelectedBorder is deliberately absent (see the type
        // remarks): its fallback is the ACCENT, not a shared structural token.
        + @"<Color Token='BaseBorder' Value='#575247' />"
        + @"<Color Token='PanelBorder' Value='#575247' />"
        + @"<Color Token='RaisedBorder' Value='#575247' />"
        + @"<Color Token='HoverBorder' Value='#6b6459' />"
        // --- ink ---
        + @"<Color Token='TextPrimary' Value='#eae6de' />"
        + @"<Color Token='TextSecondary' Value='#b0ada3' />"
        + @"<Color Token='TextDisabled' Value='#8a8780' />"
        + @"<Color Token='TextOnGold' Value='#ffd68c' />"
        + @"<Color Token='TextOnDanger' Value='#ffdcd6' />"
        // --- danger edge (per-surface: the template DID claim this one) ---
        + @"<Color Token='DangerBorder' Value='#c96057' />"
        // --- the series accent. US owns it now; the value is the retired template's, unchanged. ---
        + @"<Color Token='AccentGold' Value='#d19a38' />"
        + @"</Scheme></Styles>";

    /// <summary>
    /// The identity accent as the document spells it. Named separately because the surface lane pins the
    /// accent against THIS constant rather than against a carrier palette: the token used to be asserted
    /// against <c>UiTheme.DarkGold</c>, which made a carrier retirement a US test failure instead of a US
    /// compile error with an obvious fix. The value did not move with the ownership.
    /// </summary>
    public const string AccentGoldHex = "#d19a38";

    /// <summary>
    /// The colour tokens <see cref="SchemeXml"/> is required to declare. This is the "US owns the complete
    /// baseline" rule made checkable: the lane enumerates the document's own colour map and asserts this set
    /// is a subset of it, so a token silently dropped from the string reddens instead of inheriting a
    /// carrier value nobody chose.
    /// <para>
    /// <c>SelectedBorder</c> is absent ON PURPOSE and that absence is the one documented exception: it must
    /// stay unclaimed so the accent fallback (<c>SelectedBorder ?? AccentGold</c>) survives, and a flat
    /// scope is allowed to alias it away.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<string> ColorTokens = new[]
    {
        "Base", "WorkspacePlane", "Panel", "SectionBand", "Raised", "Hover", "Selected",
        "Success", "Danger",
        "Border", "BorderStrong", "Divider",
        "BaseBorder", "PanelBorder", "RaisedBorder", "HoverBorder",
        "TextPrimary", "TextSecondary", "TextDisabled", "TextOnGold", "TextOnDanger",
        "DangerBorder",
        "AccentGold"
    };

    /// <summary>
    /// A fresh instance, one per window that needs one - the same contract as the carrier's template
    /// (which hands out a new bag per call). A theme is mutated at runtime (a host applies a style
    /// document's page level into it), so two windows sharing one bag would paint each other.
    /// </summary>
    public static UiTheme Surface()
    {
        return Configure(UiTheme.Vanilla);
    }

    /// <summary>
    /// Applies the US palette to <paramref name="theme"/> in place through the carrier's resolver and returns
    /// it. Fail-soft on purpose: a malformed embedded document must not take the mod down with a
    /// <c>TypeInitializationException</c>, so the surfaces fall back to <see cref="UiTheme.Vanilla"/> and the
    /// failure is one loud line plus an entry on <see cref="SchemeIssues"/>. The surface lane is the positive
    /// control - it asserts the resolved token values, so a document that stopped parsing reddens there
    /// instead of shipping the wrong look.
    /// </summary>
    public static UiTheme Configure(UiTheme theme)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        new UiStyleResolver(theme, Scheme).ApplyTo(theme);
        return theme;
    }

    /// <summary>
    /// The parsed palette, or the empty document when the embedded fallback is malformed.
    /// <para>
    /// PUBLIC because a palette-only INVALIDATION is a page-level re-apply of exactly this document, and the
    /// carrier's seam for that is <see cref="UiStyleResolver.ApplyTo"/> on the live resolver the host already
    /// holds (<see cref="UiHost.StyleResolver"/>). A consumer that reaches for a second resolver instead gets
    /// a second region-theme cache, which is the drift this property exists to close. It is not a second
    /// source of truth: the text above is the source, and this is its parse.
    /// </para>
    /// </summary>
    // Declared BEFORE Scheme on purpose: static field initializers run in textual order, and LoadScheme()
    // writes into this list, so a later declaration would hand it a null list and the type initializer
    // would fail before any surface was built.
    private static readonly List<string> schemeIssues = new();

    public static UiStyleDocument Scheme { get; } = LoadScheme();

    /// <summary>
    /// Everything wrong with the embedded palette, discovered when it is read: the carrier parser's own drops
    /// - an unknown colour token, an unreadable colour, a duplicate, and a legacy font spelling together with
    /// the redirect it resolved to. The carrier owns every one of those reports
    /// (<c>UiStyleDocument.Issues</c>, which the parse below copies). US adds nothing to them, because a
    /// second mapping here would duplicate the carrier's and be free to contradict it: R12-T deleted the
    /// consumer-side ledger that did exactly that once the carrier's own legacy redirect became real.
    /// <para>
    /// Read-once process state, like the document itself: the palette is an embedded constant, so there is
    /// nothing to re-read and no clock to invent. A lane asserts the list is empty, which is what keeps
    /// "a dropped declaration is never silent" a checkable fact for US's own document.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> SchemeIssues => schemeIssues;

    private static UiStyleDocument LoadScheme()
    {
        try
        {
            // US deliberately does not re-map the parser's drops; see SchemeIssues. R12-T deleted the
            // consumer-side ledger that used to duplicate the carrier's own legacy font redirect.
            UiStyleDocument document = UiStyleDocument.Parse(SchemeXml);

            // The parser records its own drops AND the legacy font redirect; surface them on one list so a
            // host has a single place to read.
            for (int i = 0; i < document.Issues.Count; i++)
            {
                schemeIssues.Add("parser: " + document.Issues[i].Message);
            }

            return document;
        }
        catch (Exception error)
        {
            Verse.Log.Error("[US] the embedded us-root style document did not parse; surfaces fall back to the"
                + " carrier's Vanilla template: " + error.Message);
            schemeIssues.Add("parse failure: " + error.Message);
            return UiStyleDocument.Empty;
        }
    }
}
