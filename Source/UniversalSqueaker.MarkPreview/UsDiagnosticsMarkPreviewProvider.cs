using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.Preview;

/// <summary>Author samples of the production read-only mark; never a fake Pawn or another painter.</summary>
public static class UsDiagnosticsMarkPreviewProvider
{
    public const string PreviewPrefix = "coahuilite.universalsqueaker/diagnostics-mark/";
    public const string LayoutDocumentId = "us.diagnostics-mark.author";
    public const string StyleDocumentId = "us.diagnostics-mark.styles";
    public const string StyleFileName = "Styles.DiagnosticsMark.xml";
    public static IEnumerable<UiPreviewRegistration> Register(ITextMetrics metrics, IUiTranslation translation, string measurementSource)
        => Register(new UiPreviewContext(metrics, translation, measurementSource));
    public static IEnumerable<UiPreviewRegistration> Register(UiPreviewContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (FerriteLibVersion.Api < new Version(0, 8, 0) || FerriteLibVersion.Api >= new Version(0, 9, 0))
            throw new InvalidOperationException("US mark preview requires the fixed Legacy08 workbench extension.");
        if (context.Watch && context.AuthorDirectory == null) throw new ArgumentException("Watch requires --author-directory.", nameof(context));
        var registrations = new List<UiPreviewRegistration>();
        foreach (var sample in new[] {
            (Id: "ready", Label: "就绪", Colour: UsDiagnosticsMarkPresentation.Ready),
            (Id: "blocked", Label: "受阻", Colour: UsDiagnosticsMarkPresentation.Blocked),
            (Id: "pending", Label: "等待", Colour: UsDiagnosticsMarkPresentation.Pending) })
        {
            string name = context.Language == "en" ? "US screen mark · " + sample.Id + " (isolated sample)" : "US屏幕标记 · " + sample.Label + "（隔离样例）";
            registrations.Add(new UiPreviewRegistration(PreviewPrefix + sample.Id, name, UiPreviewKind.Component,
                () => Create(context, sample.Id, sample.Colour)));
        }
        return registrations;
    }
    private static UiPreviewInstance Create(UiPreviewContext context, string sample, UiColour colour)
    {
        UsDiagnosticsMarkWidget.EnsureRegistered();
        UiDocumentService? documents = null;
        UiHost? host = null;
        try
        {
            string embedded = UsDiagnosticsMarkDefinition.ReadAuthorDocument();
            UiLayoutManifest layout;
            string? styleId = null;
            if (context.AuthorDirectory == null) layout = UsDiagnosticsMarkDefinition.Parse(embedded);
            else
            {
                string root = Path.GetFullPath(context.AuthorDirectory);
                documents = new UiDocumentService(autoWatch: context.Watch);
                documents.AddProjectedLayout(new UiDocumentSource(LayoutDocumentId, UiDocumentKind.Layout,
                    Path.Combine(root, UsDiagnosticsMarkDefinition.FileName)), embedded, UsDiagnosticsMarkDefinition.Parse);
                var accepted = documents.GetSnapshot(LayoutDocumentId);
                layout = accepted?.HasLastGood == true && accepted.Layout != null ? accepted.Layout
                    : throw new InvalidOperationException("No valid mark author definition: " + string.Join("; ", documents.Reports.Select(report => report.ToString())));
                string stylePath = Path.Combine(root, StyleFileName);
                if (File.Exists(stylePath))
                {
                    documents.Add(new UiDocumentSource(StyleDocumentId, UiDocumentKind.Style, stylePath));
                    if (documents.GetSnapshot(StyleDocumentId)?.HasLastGood != true) throw new InvalidOperationException("No valid standalone mark style.");
                    styleId = StyleDocumentId;
                }
            }
            var mark = new UsDiagnosticsScreenMark(UsDiagnosticsMarkPresentation.Glyph, colour,
                new UiLayoutPoint(64, 32), new UiLayoutPoint(-1, 0), new UiLayoutPoint(1, 0),
                new UiLayoutPoint(0, -1), new UiLayoutPoint(0, 1));
            var bindings = new UiBindings();
            bindings.BindReadOnly<UsDiagnosticsScreenMark>("diagnostics-mark", () => mark);
            host = new UiHost(UsDiagnosticsMarkWidget.Scope, layout, bindings, UsTheme.Surface(), context.Metrics, context.Translation);
            if (documents != null && !documents.Attach(host, LayoutDocumentId, styleId))
                throw new InvalidOperationException("Mark author documents could not attach.");
            return new UiPreviewInstance(host, "us/diagnostics-mark; state=" + sample
                + "; isolated screen anchor/deltas; no world projection or runtime observation; origin="
                + (documents == null ? "embedded" : "Documents.GetSnapshot"), context.MeasurementSource,
                documents, null, documents);
        }
        catch
        {
            try { host?.Dispose(); } finally { documents?.Dispose(); }
            throw;
        }
    }
}
