using UnityEngine;

namespace UniversalSqueaker;

/// <summary>
/// Explicit DIAG-07 platform output, adopted by PM/CO for this round (WO r2/A002).
/// No clipboard reads or rendering. Retire when an adopted public clipboard seam replaces it,
/// or when feedback copying is retired. The boundary audit counts this exact sole setter.
/// </summary>
internal static class SqueakDiagnosticsClipboard
{
    internal static void Write(string text) => GUIUtility.systemCopyBuffer = text;
}
