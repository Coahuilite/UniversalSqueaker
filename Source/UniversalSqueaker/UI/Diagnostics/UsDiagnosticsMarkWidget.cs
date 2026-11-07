using System;
using System.IO;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>Detached paint input in logical GUI units. No Pawn, map, business clock or owner.</summary>
public readonly struct UsDiagnosticsScreenMark
{
    public UsDiagnosticsScreenMark(string glyph, UiColour colour, UiLayoutPoint anchor,
        UiLayoutPoint left, UiLayoutPoint right, UiLayoutPoint up, UiLayoutPoint down)
    { Glyph = glyph; Colour = colour; Anchor = anchor; Left = left; Right = right; Up = up; Down = down; }
    public string Glyph { get; }
    public UiColour Colour { get; }
    public UiLayoutPoint Anchor { get; }
    // Projected outline deltas, not world distances interpreted as pixels.
    public UiLayoutPoint Left { get; }
    public UiLayoutPoint Right { get; }
    public UiLayoutPoint Up { get; }
    public UiLayoutPoint Down { get; }
    internal UsDiagnosticsScreenMark Translate(UiLayoutPoint offset)
        => new(Glyph, Colour, Anchor + offset, Left, Right, Up, Down);
}

/// <summary>The retained screen-mark paint meanings; selection of a meaning stays with diagnostics.</summary>
public static class UsDiagnosticsMarkPresentation
{
    public const string Glyph = "●";
    public static readonly UiColour Ready = new(.25f, .82f, .38f);
    public static UiColour Blocked => UsAttention.Brush;
    public static readonly UiColour Pending = new(.35f, .66f, .95f);
}

/// <summary>One author definition for direct game painting and the retained workbench widget.</summary>
public static class UsDiagnosticsMarkDefinition
{
    public const string FileName = "Layout.DiagnosticsMark.Schema2.xml";
    private const string Resource = "UniversalSqueaker.UI.Layout.DiagnosticsMark.Schema2.xml";
    public static string ReadAuthorDocument()
    {
        using var stream = typeof(UsDiagnosticsMarkDefinition).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException("Missing US diagnostics mark author definition.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public static UiLayoutManifest Parse(string document)
    {
        var manifest = UiLayoutManifest.Parse(document);
        if (manifest.Roots.Count != 1 || manifest.Roots[0].Kind != UsDiagnosticsMarkWidget.KindName)
            throw new InvalidOperationException("The mark author document requires one us/diagnostics-mark root.");
        return manifest;
    }
    public static UiTheme Theme(UiLayoutManifest manifest)
    {
        var resolver = new UiStyleResolver(UsTheme.Surface(), manifest.Styles);
        resolver.ApplyTo(resolver.Baseline);
        var spec = manifest.Roots[0];
        return resolver.ThemeFor(new[] { new UiStyleDeclaration(
            spec.TryGetAttribute("Scheme", out string scheme) ? scheme : null,
            spec.TryGetAttribute("Density", out string density) ? density : null) });
    }
}

/// <summary>Read-only US mark. Game and preview call this exact PaintAt implementation.</summary>
public sealed partial class UsDiagnosticsMarkWidget : IUiWidget
{
    public const string KindName = "us/diagnostics-mark";
    public const string Scope = "coahuilite.universalsqueaker";
    private string binding = "diagnostics-mark";
    private float outlineScale = 1, offsetX, offsetY = -2;
    private UiColour outlineInk = new(0, 0, 0);
#if US_OFFLINE_WORKBENCH
    private bool painted;
    private UsDiagnosticsMarkGeometry lastGeometry;
    private int paintedFrame = -1;
#endif
    public string Kind => KindName;

    // This independent overlay kind is adopted only by its author preview; the settings registrar
    // does not gain another settings-page capability or a second diagnostics state owner.
    public static void EnsureRegistered()
    {
        if (UiWidgetRegistry.KnownKinds(Scope).Contains(KindName)) return;
        UiWidgetRegistry.Define(new UiWidgetDefinition(Scope, KindName,
            () => new UsDiagnosticsMarkWidget(), new UiAttributeSchema(
                new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"),
                new UiAttributeDefinition("Bind", binding: UiBindingRole.Value),
                new UiAttributeDefinition("OutlineScale", UiAttributeKind.Number),
                new UiAttributeDefinition("OutlineInk"), new UiAttributeDefinition("GlyphOffsetX", UiAttributeKind.Number),
                new UiAttributeDefinition("GlyphOffsetY", UiAttributeKind.Number))));
    }
    public void Configure(UiElementSpec spec)
    {
        binding = spec.TryGetAttribute("Bind", out string key) ? key : "diagnostics-mark";
        float Number(string name, float fallback)
        {
            if (!spec.TryGetAttribute(name, out _)) return fallback;
            if (!spec.TryGetNumber(name, out float value) || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Invalid mark " + name);
            return value;
        }
        outlineScale = Number("OutlineScale", 1);
        if (outlineScale < 0) throw new ArgumentException("Mark OutlineScale cannot be negative.");
        offsetX = Number("GlyphOffsetX", 0); offsetY = Number("GlyphOffsetY", -2);
        outlineInk = new UiColour(0, 0, 0);
        if (spec.TryGetAttribute("OutlineInk", out string ink) && !UiColour.TryParse(ink, out outlineInk))
            throw new ArgumentException("Invalid mark OutlineInk.");
#if US_OFFLINE_WORKBENCH
        painted = false;
#endif
    }
    public void Validate(IUiBindingAccess bindings, string elementPath)
        => bindings.ValidateValue<UsDiagnosticsScreenMark>(binding, elementPath);
    public float Measure(UiWidgetContext ctx)
    {
        var mark = ctx.Bindings.Get<UsDiagnosticsScreenMark>(binding);
        var run = UiTextFonts.Resolve(ctx);
        return Math.Max(1, Geometry(mark, run).Bounds.YMax);
    }
    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        PaintAt(ctx.Bindings.Get<UsDiagnosticsScreenMark>(binding).Translate(rect.Position), ctx.Metrics, ctx.Drawing, ctx.Theme);
#if US_OFFLINE_WORKBENCH
        paintedFrame = ctx.Session.Frame;
#endif
    }
    public bool PaintAt(UsDiagnosticsScreenMark mark, ITextMetrics metrics, IUiDrawing drawing, UiTheme theme)
    {
#if US_OFFLINE_WORKBENCH
        painted = false;
#endif
        if (string.IsNullOrEmpty(mark.Glyph)) return false;
        var run = UiTextFonts.Resolve(metrics, drawing, UiTextFonts.Requested(theme), elementPath: KindName);
        var geometry = Geometry(mark, run);
        void Paint(UiLayoutPoint delta, UiColour colour)
            => run.Draw(new UiLayoutRect(geometry.Glyph.X + delta.X, geometry.Glyph.Y + delta.Y,
                geometry.Glyph.Width, geometry.Glyph.Height), mark.Glyph, colour, UiTextAnchor.UpperLeft, singleLine: true);
        Paint(geometry.Left, outlineInk); Paint(geometry.Right, outlineInk);
        Paint(geometry.Up, outlineInk); Paint(geometry.Down, outlineInk);
        Paint(UiLayoutPoint.Zero, mark.Colour);
#if US_OFFLINE_WORKBENCH
        lastGeometry = geometry; painted = true;
#endif
        return true;
    }
    private UsDiagnosticsMarkGeometry Geometry(UsDiagnosticsScreenMark mark, UiTextRun run)
    {
        float width = Math.Max(1, run.MeasureWidth(mark.Glyph ?? ""));
        float height = Math.Max(1, run.MeasureText(mark.Glyph ?? "", 100000));
        var glyph = new UiLayoutRect(mark.Anchor.X + offsetX - width / 2, mark.Anchor.Y + offsetY, width, height);
        UiLayoutPoint Scale(UiLayoutPoint point) => new(point.X * outlineScale, point.Y * outlineScale);
        return new UsDiagnosticsMarkGeometry(glyph, Scale(mark.Left), Scale(mark.Right), Scale(mark.Up), Scale(mark.Down), run.Font);
    }
    private readonly struct UsDiagnosticsMarkGeometry
    {
        internal readonly UiLayoutRect Glyph, Bounds;
        internal readonly UiLayoutPoint Left, Right, Up, Down;
        internal readonly UiTextFont Font;
        internal UsDiagnosticsMarkGeometry(UiLayoutRect glyph, UiLayoutPoint left, UiLayoutPoint right,
            UiLayoutPoint up, UiLayoutPoint down, UiTextFont font)
        {
            Glyph = glyph; Left = left; Right = right; Up = up; Down = down; Font = font;
            float x = Math.Min(0, Math.Min(Math.Min(left.X, right.X), Math.Min(up.X, down.X)));
            float y = Math.Min(0, Math.Min(Math.Min(left.Y, right.Y), Math.Min(up.Y, down.Y)));
            float maxX = Math.Max(0, Math.Max(Math.Max(left.X, right.X), Math.Max(up.X, down.X)));
            float maxY = Math.Max(0, Math.Max(Math.Max(left.Y, right.Y), Math.Max(up.Y, down.Y)));
            Bounds = new UiLayoutRect(glyph.X + x, glyph.Y + y, glyph.Width + maxX - x, glyph.Height + maxY - y);
        }
    }
}
