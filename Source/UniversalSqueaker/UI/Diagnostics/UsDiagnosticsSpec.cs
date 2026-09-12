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
/// Responsive shape (09 §3.5): the root Row declares ONE numeric <c>Breakpoint</c> against its own
/// inner width with <c>Narrow="Column"</c>, so below it the two master/detail columns are
/// <c>NarrowHidden</c> and the navigation column lays out in their place. The navigation column holds
/// either the list or the detail - never both - and the switch belongs to the US-owned body widget,
/// not to an attribute: the carrier allows <c>Tab</c> on widgets only, and the view is interaction
/// state the widget owns. All views are the same US-owned widget kinds; only their arrangement differs.
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
        // Wide presentation: master list + independent detail column, hidden whole when narrow.
        "    <Column Id=\"diag-list-col\" Width=\"" + ListWidth + "\" Fill=\"true\" Gap=\"2\" NarrowHidden=\"true\">",
        "      <Widget Id=\"diag-toolbar\" Kind=\"us/diag/toolbar\" Scope=\"main\" />",
        "      <Scroll Id=\"diag-list-scroll\" Fill=\"true\" Gap=\"2\">",
        "        <Widget Id=\"diag-list\" Kind=\"us/diag/list\" />",
        "      </Scroll>",
        "      <Widget Id=\"diag-pager\" Kind=\"us/diag/pager\" />",
        "    </Column>",
        "    <Scroll Id=\"diag-detail-scroll\" Width=\"" + DetailWidth + "\" Fill=\"true\" Gap=\"4\" NarrowHidden=\"true\">",
        "      <Widget Id=\"diag-detail\" Kind=\"us/diag/detail\" Scope=\"main\" />",
        "    </Scroll>",
        // Narrow presentation: ONE in-window navigation column. An Auto column so it costs the wide
        // layout one pixel (a 1px rect is skipped by every widget's Draw); below the Breakpoint it takes
        // the full width and the two wide columns are gone.
        "    <Column Id=\"diag-nav-col\" Width=\"Auto\" Gap=\"4\">",
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
