using System;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace UniversalSqueaker;

/// <summary>
/// The single source of US's surface tokens: a theme factory whose values are the interaction spec's
/// colour table (the style-and-appearance spec, section 1.1), mapped onto FerriteLib's token slots.
/// <para>
/// Why this type exists: FerriteLib ships a look, not our look. Its <see cref="UiTheme.DarkGold"/> is a
/// template a consumer re-tints, and US paints three windows (settings, diagnostics panel, diagnostics
/// detail) plus two kernel hosts — five surfaces that must agree on one table or they drift apart one
/// token at a time. Every site that used to read the library template reads this instead.
/// </para>
/// <para>
/// Mapping, with the spec's token name at each assignment. Slots the spec does not define are left at
/// the library's default on purpose: inventing a value here would be US writing a colour nobody ruled
/// on. Two entries carry a recorded trade-off:
/// <list type="bullet">
/// <item><c>Hover</c> takes the spec's control/hover plane. In this tree the token is used for hovered
/// ROWS (the many) and for the shell's close button (the one), and the spec gives those two different
/// steps (s2 vs s3); with one token serving both, rows win because they are the surface users scan. The
/// button-hover step stays unrepresented until the library grows a second token or a later batch lands
/// one.</item>
/// <item><c>AccentGold</c> is deliberately untouched: the spec keeps the series gold as the identity
/// accent, so it arrives with the library default this factory starts from, and the lane pins that it
/// still equals it.</item>
/// </list>
/// </para>
/// <para>
/// Public because the kernel-host harness references the built mod assembly rather than linking this
/// source; internal would make the table unobservable from the only place that can assert it.
/// </para>
/// </summary>
public static class UsTheme
{
    /// <summary>
    /// A fresh instance, one per window that needs one — the same contract as the library's template
    /// (which hands out a new bag per call). A theme is mutated at runtime (a host applies a style
    /// document's page level into it), so two windows sharing one bag would paint each other.
    /// </summary>
    public static UiTheme Surface()
    {
        return Configure(UiTheme.DarkGold);
    }

    /// <summary>Applies the US table to <paramref name="theme"/> in place and returns it.</summary>
    public static UiTheme Configure(UiTheme theme)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));

        // Three surface layers and two line strengths (spec 1.2): s0 is the window/work plane, s1 the
        // panel and band plane, s2 the control base, the rule is structure, and the row divider sits one
        // step weaker than it.
        theme.Base = Rgb(0x0f, 0x11, 0x16);           // s0  #0f1116
        theme.WorkspacePlane = Rgb(0x0f, 0x11, 0x16); // s0
        theme.Panel = Rgb(0x17, 0x1a, 0x21);          // s1  #171a21
        theme.SectionBand = Rgb(0x17, 0x1a, 0x21);    // s1
        theme.Raised = Rgb(0x1f, 0x23, 0x2c);         // s2  #1f232c (control base)
        theme.Hover = Rgb(0x1f, 0x23, 0x2c);          // s2  (hovered row / control under the pointer)
        theme.Selected = Rgb(0x1c, 0x21, 0x2b);       // selected row plane (spec 1.5)
        theme.Border = Rgb(0x33, 0x3a, 0x46);         // rule #333a46
        theme.BorderStrong = Rgb(0x3d, 0x44, 0x52);   // window edge, one step brighter (spec 1.2)
        theme.Divider = Rgb(0x23, 0x28, 0x33);        // row divider #232833

        // Text: ink, dim, and dim again for unavailable text — the spec carries that state in shape, not
        // in a second colour (spec 1.5/1.6).
        theme.TextPrimary = Rgb(0xe6, 0xe9, 0xee);    // ink #e6e9ee
        theme.TextSecondary = Rgb(0x98, 0xa1, 0xaf);  // dim #98a1af
        theme.TextDisabled = Rgb(0x98, 0xa1, 0xaf);   // dim

        // Destructive action only (spec 1.1, danger): dark plane, light text, and the saturated colour on
        // the EDGE — which is what keeps tone=Danger rules and badges bright instead of muddy.
        theme.Danger = Rgb(0x3a, 0x1f, 0x1f);
        theme.DangerBorder = Rgb(0xc8, 0x5a, 0x5a);
        theme.TextOnDanger = Rgb(0xff, 0xd9, 0xd9);
        return theme;
    }

    /// <summary>0..255 channels, written the way the spec writes them; no 0..1 conversion at the call site.</summary>
    private static Color Rgb(int r, int g, int b)
    {
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
