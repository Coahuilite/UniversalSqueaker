using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>US output policy; the Host/Session owns the actual FL binding and its close summary.</summary>
internal static class UsUiDiagnostics
{
    internal const string Consumer = "universalsqueaker";
    internal static UiDiagnosticLogOptions Options() => new() { DetailEnabled = SqueakLog.ShouldEmitDev };

    // A Session-root marker, not another output owner/index. Existing independent attach/audit
    // callers need not adopt US's consumer or replace their own Warning/output policy.
    private sealed class Policy { internal bool Adopted; }
    private static Policy State(UiHost host) => host.CreateContext(1).State("us/ui-diagnostic-policy", () => new Policy());
    internal static bool IsAdopted(UiHost host) => State(host).Adopted;
    internal static UiHost Adopt(UiHost host)
    {
        VerseUiDiagnosticLog.Attach(host, Consumer).Configure(Options());
        State(host).Adopted = true;
        Synchronize(host);
        return host;
    }

    internal static void Synchronize(UiHost host)
    {
        if (!host.Session.IsActive || !IsAdopted(host)) return;
        // CreateHost already supplied this owner before construction. Attach retrieves it, including
        // without adding an adoption prerequisite to independent callers of existing attach seams.
        VerseUiDiagnosticLog.Attach(host, Consumer).Configure(Options());
        // Keep US's existing bounded forensic channel, not a second native printer for FL facts.
        host.Session.Controls.Trace = SqueakLog.ShouldEmitDev ? SqueakLog.PopupTrace : null;
    }
}
