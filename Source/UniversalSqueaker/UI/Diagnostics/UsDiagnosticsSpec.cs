using System.Globalization;

namespace UniversalSqueaker.UI;

/// <summary>
/// Programmatic page construction for the diagnostics windows (round-9 ruling: the embedded-XML
/// manifest ritual is NOT copied to the panel). The strings here feed the SAME production parser
/// (<c>UiLayoutManifest.Parse</c>) as the shipped settings manifests - creation-time validation,
/// kind resolution and attribute rejection all stay on the proven path; what is skipped is a new
/// embedded resource and a gate 11 registration, neither of which the audit asserted for pages
/// (it names exactly two shipped manifests). No Height attributes: every widget self-measures
/// (the collapsed state drives real heights, and a fixed attribute would override the ruling).
/// <para>
/// Responsive shape (09 §3.5): the page carries TWO mutually exclusive presentations, each behind its
/// own <c>VisibleKey</c> (the host contract keys <see cref="UsDiagnosticsHost.KeyWide"/> /
/// <see cref="UsDiagnosticsHost.KeyNarrow"/>, fed from the source's own narrow decision). Wide is the
/// master list plus the independent detail column; narrow is ONE in-window navigation column. The
/// root Row keeps its <c>Breakpoint</c>/<c>Narrow="Column"</c> for the narrow stack, but it is no
/// longer what makes the two shapes exclusive: FL 0.7's A1 retired the "an unmeasurable
/// <c>Width="Auto"</c> Row child collapses to a 1px stub" idiom this page used to rely on, and the
/// supported replacement is the one FL documented (<c>consume-from-0.7.0.md</c> §3): a hidden
/// presentation owns NO geometry rather than a pixel-wide remnant, so it can never share the wide
/// Row's leftover space (which is exactly what A1 exposed).
/// <see cref="UsDiagnosticsHost.ApplyContentWidth"/> is what makes the swap re-arrange: a read-only
/// VisibleKey binding announces no revision of its own.
/// The navigation column holds either the list or the detail - never both - and the switch belongs to
/// the US-owned body widget, not to an attribute: the carrier allows <c>Tab</c> on widgets only, and
/// the view is interaction state the widget owns. All views are the same US-owned widget kinds; only
/// their arrangement differs.
/// </para>
/// </summary>
public static class UsDiagnosticsSpec
{
    private const string PageHead = "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">";
    private const string PageTail = "</UiPage>";

    /// <summary>Invariant spelling of the breakpoint, so the spec is byte-stable across locales.</summary>
    internal static readonly string Breakpoint =
        UsDiagnosticsProjection.NarrowBreakpoint.ToString("0.###", CultureInfo.InvariantCulture);

    internal static readonly string ListWidth =
        UsDiagnosticsProjection.ListColumnWidth.ToString("0.###", CultureInfo.InvariantCulture);

    internal static readonly string DetailWidth =
        UsDiagnosticsProjection.DetailColumnWidth.ToString("0.###", CultureInfo.InvariantCulture);

    internal static readonly string Gap =
        UsDiagnosticsProjection.PageGap.ToString("0.###", CultureInfo.InvariantCulture);

    internal static readonly string Padding =
        UsDiagnosticsProjection.PagePadding.ToString("0.###", CultureInfo.InvariantCulture);

    public static string MainXml => string.Join(
        "\n",
        PageHead,
        // The collapsed bar is its own page-wide root (09 §3.3 rule 2): as the Row's third child it
        // inherited a 250px list row's width and painter, which is exactly the reported defect.
        "  <Widget Id=\"diag-bar\" Kind=\"us/diag/bar\" Scope=\"main\" />",
        "  <Row Id=\"diag-root\" Gap=\"" + Gap + "\" Padding=\"" + Padding + "\" Breakpoint=\"" + Breakpoint + "\" Narrow=\"Column\">",
        // Wide presentation: master list + independent detail column, BOTH behind one VisibleKey so the
        // pair is absent - not zero-sized - whenever the narrow presentation is the arranged one.
        "    <Column Id=\"diag-list-col\" Width=\"" + ListWidth + "\" Fill=\"true\" Gap=\"2\" VisibleKey=\"" + UsDiagnosticsHost.KeyWide + "\">",
        "      <Widget Id=\"diag-toolbar\" Kind=\"us/diag/toolbar\" Scope=\"main\" />",
        "      <Scroll Id=\"diag-list-scroll\" Fill=\"true\" Gap=\"2\">",
        "        <Widget Id=\"diag-list\" Kind=\"us/diag/list\" />",
        "      </Scroll>",
        "      <Widget Id=\"diag-pager\" Kind=\"us/diag/pager\" />",
        "    </Column>",
        "    <Scroll Id=\"diag-detail-scroll\" Width=\"" + DetailWidth + "\" Fill=\"true\" Gap=\"4\" VisibleKey=\"" + UsDiagnosticsHost.KeyWide + "\">",
        "      <Widget Id=\"diag-detail\" Kind=\"us/diag/detail\" Scope=\"main\" />",
        "    </Scroll>",
        // Narrow presentation: ONE in-window navigation column, behind the complementary VisibleKey.
        // It keeps Width="Auto" and is the only visible child in this shape, so the unsized
        // distribution hands it the full width; in the wide shape it is not arranged at all, so it
        // cannot take a share of the split. No fixed Width: that would pin the narrow state too.
        "    <Column Id=\"diag-nav-col\" Width=\"Auto\" Gap=\"4\" VisibleKey=\"" + UsDiagnosticsHost.KeyNarrow + "\">",
        // Back sits ABOVE the body: it measures zero except in the narrow detail view, which is the only
        // state with an owning list to return to. The pinned detail window declares no nav widget at all.
        "      <Widget Id=\"diag-nav-back\" Kind=\"us/diag/nav\" Scope=\"main\" />",
        "      <Widget Id=\"diag-nav-toolbar\" Kind=\"us/diag/toolbar\" Scope=\"main\" />",
        "      <Scroll Id=\"diag-nav-scroll\" Fill=\"true\" Gap=\"2\">",
        "        <Widget Id=\"diag-nav-body\" Kind=\"us/diag/navbody\" Scope=\"main\" />",
        "      </Scroll>",
        "    </Column>",
        "  </Row>",
        PageTail);

    /// <summary>
    /// The pinned detail window's page: bar + toolbar + independent detail scroll. It has no list and
    /// no navigation column, so it declares no <c>us/diag/nav</c> element and can never grow a Back
    /// control that would have nowhere to go; its identity/lock lifecycle stays the window's own.
    /// </summary>
    public static string DetailXml => string.Join(
        "\n",
        PageHead,
        "  <Widget Id=\"lock-bar\" Kind=\"us/diag/bar\" Scope=\"lock\" />",
        "  <Column Id=\"lock-root\" Gap=\"4\" Padding=\"" + Padding + "\">",
        "    <Widget Id=\"lock-toolbar\" Kind=\"us/diag/toolbar\" Scope=\"lock\" />",
        "    <Scroll Id=\"lock-detail-scroll\" Fill=\"true\" Gap=\"4\">",
        "      <Widget Id=\"lock-detail\" Kind=\"us/diag/detail\" Scope=\"lock\" />",
        "    </Scroll>",
        "  </Column>",
        PageTail);
}
