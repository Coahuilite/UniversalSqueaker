using System.Globalization;

namespace UniversalSqueaker.UI;

/// <summary>Schema 2 through the normal UiKit parser: list/detail on wide screens,
/// separate retained list/detail/technical scrolls when narrow, and one fixed capture footer.</summary>
public static class UsDiagnosticsSpec
{
    private const string PageHead = "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">"
        + "<Styles Schema=\"1\"><Scheme Name=\"diag-body\"><Metric Token=\"Font\" Value=\"Small\" /><Metric Token=\"FontSize\" Value=\"16\" /><Metric Token=\"RowHeight\" Value=\"30\" /></Scheme></Styles>"
        + "<Column Id=\"diag-page\" Fill=\"true\" Padding=\"0\" Gap=\"4\" Scheme=\"diag-body\">";
    private const string PageTail = "</Column></UiPage>";
    // Primary: saved conclusion/events. Secondary: full technical reader. One fixed action footer.
    private const string CaptureXml = "<Widget Id=\"diag-capture\" Kind=\"us/diag/toolbar\" Scope=\"capture\" />";
    private const string TechnicalXml =
        "<Column Id=\"diag-technical\" Padding=\"0\" Fill=\"true\" Gap=\"4\" VisibleBind=\"diag-technical-visible\">"
        + "<Widget Id=\"diag-technical-back\" Kind=\"input/button\" TextKey=\"US.Diagnostics.Technical.Back\" ActionBind=\"diag-technical-back\" Width=\"Auto\" Height=\"30\" />"
        + "<Row Id=\"diag-time-display\" Gap=\"6\" Padding=\"0\"><Widget Id=\"diag-time-caption\" Kind=\"text/wrapped\" TextKey=\"US.Diagnostics.Technical.Seconds\" /><Widget Id=\"diag-time-units\" Kind=\"input/checkbox\" Bind=\"diag-seconds\" SelectedBind=\"diag-seconds\" Width=\"36\" Height=\"30\" /></Row>"
        + "<Scroll Padding=\"0\" Id=\"diag-technical-scroll\" Fill=\"true\"><Widget Id=\"diag-technical-text\" Kind=\"input/text-field\" Bind=\"diag-technical-text\" Multiline=\"true\" /></Scroll></Column>";
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

    public static string MainXml => string.Join("\n", PageHead,
        "<Widget Id=\"diag-bar\" Kind=\"us/diag/bar\" Scope=\"main\" />",
        "<Row Id=\"diag-root\" Fill=\"true\" Gap=\"" + Gap + "\" Padding=\"" + Padding + "\" Breakpoint=\"" + Breakpoint + "\" Narrow=\"Column\" VisibleBind=\"diag-reading-visible\">",
        "<Column Id=\"diag-list-col\" Padding=\"0\" Width=\"" + ListWidth + "\" Fill=\"true\" Gap=\"4\" VisibleBind=\"" + UsDiagnosticsHost.KeyWide + "\">",
        "<Scroll Padding=\"0\" Id=\"diag-list-scroll\" Fill=\"true\"><Widget Id=\"diag-list\" Kind=\"us/diag/list\" /></Scroll>",
        "<Widget Id=\"diag-pager\" Kind=\"us/diag/pager\" /></Column>",
        "<Column Id=\"diag-detail-col\" Padding=\"0\" Fill=\"true\" Gap=\"4\" VisibleBind=\"" + UsDiagnosticsHost.KeyWide + "\">",
        "<Widget Id=\"diag-toolbar\" Kind=\"us/diag/toolbar\" Scope=\"main\" />",
        "<Scroll Padding=\"0\" Id=\"diag-detail-scroll\" Fill=\"true\"><Widget Id=\"diag-detail\" Kind=\"us/diag/detail\" Scope=\"main\" /></Scroll></Column>",
        "<Column Id=\"diag-nav-col\" Padding=\"0\" Fill=\"true\" Gap=\"4\" VisibleBind=\"" + UsDiagnosticsHost.KeyNarrow + "\">",
        "<Row Id=\"diag-nav-header\" Padding=\"0\" Gap=\"6\"><Widget Id=\"diag-nav-back\" Kind=\"us/diag/nav\" VisibleBind=\"diag-showback\" Width=\"Auto\" Scope=\"main\" /><Widget Id=\"diag-nav-toolbar\" Kind=\"us/diag/toolbar\" Scope=\"main\" /></Row>",
        "<Scroll Padding=\"0\" Id=\"diag-nav-scroll\" Fill=\"true\" VisibleBind=\"diag-list-view\"><Widget Id=\"diag-nav-body\" Kind=\"us/diag/navbody\" Scope=\"main\" /></Scroll>",
        "<Scroll Padding=\"0\" Id=\"diag-nav-detail-scroll\" Fill=\"true\" VisibleBind=\"diag-detail-view\"><Widget Id=\"diag-nav-detail\" Kind=\"us/diag/detail\" Scope=\"main\" /></Scroll></Column></Row>",
        TechnicalXml, CaptureXml, PageTail);

    public static string DetailXml => string.Join("\n", PageHead,
        "<Widget Id=\"lock-bar\" Kind=\"us/diag/bar\" Scope=\"lock\" />",
        "<Column Id=\"lock-root\" Fill=\"true\" Gap=\"4\" Padding=\"" + Padding + "\" VisibleBind=\"diag-reading-visible\">",
        "<Widget Id=\"lock-toolbar\" Kind=\"us/diag/toolbar\" Scope=\"lock\" />",
        "<Scroll Padding=\"0\" Id=\"lock-detail-scroll\" Fill=\"true\"><Widget Id=\"lock-detail\" Kind=\"us/diag/detail\" Scope=\"lock\" /></Scroll></Column>",
        TechnicalXml, CaptureXml, PageTail);
}
