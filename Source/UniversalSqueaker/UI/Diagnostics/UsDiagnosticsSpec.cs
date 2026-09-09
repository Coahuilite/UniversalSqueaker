namespace UniversalSqueaker.UI;

/// <summary>
/// Programmatic page construction for the diagnostics windows (round-9 ruling: the embedded-XML
/// manifest ritual is NOT copied to the panel). The strings here feed the SAME production parser
/// (<c>UiLayoutManifest.Parse</c>) as the shipped settings manifests - creation-time validation,
/// kind resolution and attribute rejection all stay on the proven path; what is skipped is a new
/// embedded resource and a gate 11 registration, neither of which the audit asserted for pages
/// (it names exactly two shipped manifests).
/// </summary>
public static class UsDiagnosticsSpec
{
    private const string PageHead = "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">";
    private const string PageTail = "</UiPage>";

    public static string MainXml => string.Join(
        "\n",
        PageHead,
        "  <Row Id=\"diag-root\" Gap=\"8\" Padding=\"8\">",
        "    <Column Id=\"diag-list-col\" Width=\"250\" Fill=\"true\">",
        "      <Widget Id=\"diag-toolbar\" Kind=\"us/diag/toolbar\" Scope=\"main\" Height=\"26\" />",
        "      <Scroll Id=\"diag-list-scroll\" Fill=\"true\" Gap=\"2\">",
        "        <Widget Id=\"diag-list\" Kind=\"us/diag/list\" />",
        "      </Scroll>",
        "      <Widget Id=\"diag-pager\" Kind=\"us/diag/pager\" Height=\"24\" />",
        "    </Column>",
        "    <Scroll Id=\"diag-detail-scroll\" Width=\"262\" Fill=\"true\" Gap=\"4\">",
        "      <Widget Id=\"diag-detail\" Kind=\"us/diag/detail\" Scope=\"main\" />",
        "    </Scroll>",
        "    <Widget Id=\"diag-monitor\" Kind=\"us/diag/monitor\" Height=\"24\" />",
        "  </Row>",
        PageTail);

    public static string DetailXml => string.Join(
        "\n",
        PageHead,
        "  <Column Id=\"lock-root\" Gap=\"4\" Padding=\"8\">",
        "    <Widget Id=\"lock-toolbar\" Kind=\"us/diag/toolbar\" Scope=\"lock\" Height=\"26\" />",
        "    <Scroll Id=\"lock-detail-scroll\" Fill=\"true\" Gap=\"4\">",
        "      <Widget Id=\"lock-detail\" Kind=\"us/diag/detail\" Scope=\"lock\" />",
        "    </Scroll>",
        "    <Widget Id=\"lock-monitor\" Kind=\"us/diag/monitor\" Height=\"24\" />",
        "  </Column>",
        PageTail);
}
