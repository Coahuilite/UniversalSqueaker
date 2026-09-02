using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Owns the kernel text-fit audit lifetime for the settings window.
///
/// Layout can promise a rect but cannot promise that a translated string fits it, and no build-time
/// harness can know: real glyph advances only exist inside the game's font engine. This turns that
/// unknown into evidence. While the window is open and detailed logging is effective, every label the
/// kernel draws is measured against the rect it was given, and each distinct failure is written as one
/// <c>usdiag evt=ui.text.overflow</c> record carrying the element path, the failing axis, the font and
/// the need/have pixel pair.
///
/// The overflowing text itself is deliberately not logged: labels embed third-party pack and Def names,
/// and the element path already identifies the exact code site. When dev logging is off the audit is
/// never attached, so the measuring cost is not paid.
/// </summary>
internal static class UsTextFitAudit
{
    internal static void Begin()
    {
        if (!SqueakLog.ShouldEmitDev) return;

        UiFitAudit.Attach(VerseFerriteTextMetrics.Instance, Report);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;
    }

    internal static void End()
    {
        UiFitAudit.Detach();
    }

    private static void Report(UiOverflowReport report)
    {
        SqueakLog.LabelOverflow(
            report.ElementPath,
            report.Axis == UiOverflowAxis.Width ? "width" : "height",
            FontName(report.Font),
            report.Needed,
            report.Available);
    }

    private static string FontName(UiFont font)
    {
        return font switch
        {
            UiFont.Tiny => "tiny",
            UiFont.Medium => "medium",
            _ => "small"
        };
    }
}
