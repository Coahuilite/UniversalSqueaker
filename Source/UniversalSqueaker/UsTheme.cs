using System;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker;

/// <summary>
/// US's surface tokens, resolved from ONE style document instead of a C# assignment list (redesign S2).
/// <para>
/// Why the table lives in a document: FerriteLib ships a look, not our look - its <see cref="UiTheme.DarkGold"/>
/// is a template a consumer re-tints, and US paints three windows (settings, diagnostics panel, diagnostics
/// detail) plus two kernel hosts, so the five surfaces must agree on one table or they drift apart one token
/// at a time. The table is now data (<see cref="SchemeXml"/>) applied through the library's own resolver, so
/// the same table can move to a loose <c>Style.Schema1.xml</c> in S5 with this string as the embedded fallback
/// - the C# below no longer names a single colour.
/// </para>
/// <para>
/// Two recorded trade-offs survive the move, because they are product rulings and not formatting:
/// <list type="bullet">
/// <item><c>Hover</c> takes the spec's control/hover plane. One token serves hovered ROWS (the many) and the
/// shell's close button (the one); the spec gives those two different steps (s2 vs s3), and with one token
/// serving both, rows win because they are the surface users scan. The button-hover step stays unrepresented
/// until the library grows a second token or a later batch lands one.</item>
/// <item><c>AccentGold</c> is deliberately NOT in the document: the spec keeps the series gold as the identity
/// accent, so it arrives with the library template this factory starts from, and the surface lane pins that it
/// still equals it. There is no attention token here on purpose either - <c>UiTheme.Warning</c> is the carrier's
/// redirect onto <c>Danger</c>, so US carries the attention role in <see cref="UsAttention"/> instead.</item>
/// </list>
/// </para>
/// <para>
/// Public because the kernel-host harness references the built mod assembly rather than linking this source;
/// internal would make the table unobservable from the only place that can assert it.
/// </para>
/// </summary>
public static class UsTheme
{
    /// <summary>
    /// The US scheme as a style document. Single-quoted XML attributes on purpose: this is a C# verbatim
    /// string and XML allows them, so the table reads exactly as it will read in the loose file S5 adds.
    /// </summary>
    public const string SchemeXml =
        @"<Styles Schema='1' Scheme='us-root'>"
        + @"<Scheme Name='us-root'>"
        + @"<Color Token='Base' Value='#0e0d0c' />"
        + @"<Color Token='WorkspacePlane' Value='#0e0d0c' />"
        + @"<Color Token='Panel' Value='#171512' />"
        + @"<Color Token='SectionBand' Value='#171512' />"
        + @"<Color Token='Raised' Value='#1d1b17' />"
        + @"<Color Token='Hover' Value='#242019' />"
        + @"<Color Token='Selected' Value='#3a311f' />"
        + @"<Color Token='Border' Value='#575247' />"
        + @"<Color Token='BorderStrong' Value='#6b6459' />"
        + @"<Color Token='Divider' Value='#2a2620' />"
        + @"<Color Token='TextPrimary' Value='#eae6de' />"
        + @"<Color Token='TextSecondary' Value='#b0ada3' />"
        + @"<Color Token='TextDisabled' Value='#8a8780' />"
        + @"<Color Token='Danger' Value='#3f1c1a' />"
        + @"<Color Token='DangerBorder' Value='#c96057' />"
        + @"<Color Token='TextOnDanger' Value='#ffdcd6' />"
        + @"<Color Token='TextOnGold' Value='#ffd68c' />"
        + @"</Scheme></Styles>";

    /// <summary>
    /// A fresh instance, one per window that needs one - the same contract as the library's template
    /// (which hands out a new bag per call). A theme is mutated at runtime (a host applies a style
    /// document's page level into it), so two windows sharing one bag would paint each other.
    /// </summary>
    public static UiTheme Surface()
    {
        return Configure(UiTheme.DarkGold);
    }

    /// <summary>
    /// Applies the US scheme to <paramref name="theme"/> in place through the library's resolver and returns
    /// it. Fail-soft on purpose: a malformed embedded document must not take the mod down with a
    /// <c>TypeInitializationException</c>, so the surfaces fall back to the library template and the failure
    /// is one loud line. The surface lane is the positive control - it asserts the resolved token values, so
    /// a document that stopped parsing reddens there instead of shipping the wrong look.
    /// </summary>
    public static UiTheme Configure(UiTheme theme)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        new UiStyleResolver(theme, Scheme).ApplyTo(theme);
        return theme;
    }

    /// <summary>The parsed scheme, or the empty document when the embedded fallback is malformed.</summary>
    private static readonly UiStyleDocument Scheme = LoadScheme();

    private static UiStyleDocument LoadScheme()
    {
        try
        {
            return UiStyleDocument.Parse(SchemeXml);
        }
        catch (Exception error)
        {
            Verse.Log.Error("[US] the embedded us-root style document did not parse; surfaces fall back to the library template: " + error.Message);
            return UiStyleDocument.Empty;
        }
    }
}