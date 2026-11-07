using System;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>One borrowed drawing lane per diagnostics session; no Host, frame snapshots or Pawn storage.</summary>
internal sealed class UsDiagnosticsMarkScreen : IDisposable
{
    private readonly UsDiagnosticsMarkWidget widget;
    private readonly UiTheme theme;
    private readonly ITextMetrics metrics = new VerseTextMetrics(VerseFerriteTextMetrics.Instance);
    private readonly VerseTextExecution drawing;
    internal UsDiagnosticsMarkScreen()
    {
        var definition = UsDiagnosticsMarkDefinition.Parse(UsDiagnosticsMarkDefinition.ReadAuthorDocument());
        widget = new UsDiagnosticsMarkWidget(); widget.Configure(definition.Roots[0]);
        theme = UsDiagnosticsMarkDefinition.Theme(definition);
        drawing = new VerseTextExecution();
    }
    internal bool TryDraw(Pawn pawn, string glyph, Color colour)
    {
        Map? map = pawn.MapHeld;
        if (map == null || !ReferenceEquals(map, Find.CurrentMap) || !SqueakDiagnosticsOverlay.IsTrackingCurrentMap(map)) return false;
        // Preserve the existing WORLD head/outline offsets. Project every point through Verse,
        // whose logical UI conversion includes camera projection, UIScale and screen-Y inversion.
        Vector3 draw = pawn.DrawPos;
        Vector3 center = new(draw.x, 0, draw.z + 1.15f);
        Vector2 anchor = center.MapToUIPosition();
        const float edge = .05f;
        UiLayoutPoint Delta(float x, float z)
        {
            Vector2 point = new Vector3(center.x + x, 0, center.z + z).MapToUIPosition();
            return new UiLayoutPoint(point.x - anchor.x, point.y - anchor.y);
        }
        return widget.PaintAt(new UsDiagnosticsScreenMark(glyph, VerseUiExecution.Colour(colour),
            new UiLayoutPoint(anchor.x, anchor.y), Delta(-edge, 0), Delta(edge, 0), Delta(0, -edge), Delta(0, edge)), metrics, drawing, theme);
    }
    public void Dispose() => drawing.Dispose();
}
