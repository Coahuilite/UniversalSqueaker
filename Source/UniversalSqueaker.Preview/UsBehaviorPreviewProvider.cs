using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.Preview;

/// <summary>Real B behavior composition with isolated Core data. No Tools or game dependency.</summary>
public static class UsBehaviorPreviewProvider
{
    public const string PreviewId = "coahuilite.universalsqueaker/behavior";
    public const string LayoutDocumentId = "us.behavior.author";
    public const string StyleDocumentId = "us.behavior.styles";
    public const string StructuralMeasurementSource = "author character-cell structural metrics; no native glyph/IME/render evidence";
    public const string DefaultLanguage = "zh-Hans";
    public const string LayoutFileName = "Layout.Schema2.xml";
    public const string StyleFileName = "Styles.xml";

    public static UiPreviewRegistration Register(string language = DefaultLanguage)
        => Register(new UiPreviewContext(new CharacterCellMetrics(), new KeyedTranslation(language), StructuralMeasurementSource, language: language));

    // The original registration shape keeps its embedded, non-watching default.
    public static UiPreviewRegistration Register(ITextMetrics metrics, IUiTranslation translation, string measurementSource)
        => Register(new UiPreviewContext(metrics, translation, measurementSource));

    public static UiPreviewRegistration Register(UiPreviewContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (FerriteLibVersion.Api < new Version(0, 8, 0) || FerriteLibVersion.Api >= new Version(0, 9, 0)
            || UiOfflineWorkbench.Profile != "Legacy08" || UiOfflineWorkbench.Capability != "uikit.offline-workbench.v1")
            throw new InvalidOperationException("US offline behavior requires the fixed Legacy08 workbench extension.");
        if (context.Watch && context.AuthorDirectory == null)
            throw new ArgumentException("Watch requires an explicit --author-directory.", nameof(context));
        var translation = new KeyedTranslation(context.Language, context.Translation);
        string? authorDirectory = context.AuthorDirectory == null ? null : Path.GetFullPath(context.AuthorDirectory);
        return new UiPreviewRegistration(PreviewId, translation.Translate("US.UI.Behavior"), UiPreviewKind.Settings, () =>
        {
            UiDocumentService? documents = null;
            UiHost? host = null;
            try
            {
                string full = UsKernelSettingsHost.ReadBehaviorAuthorDocument();
                UiLayoutManifest layout;
                string? styleId = null;
                if (authorDirectory == null) layout = UsKernelSettingsHost.BehaviorRegionDefinition(full);
                else
                {
                    documents = new UiDocumentService(autoWatch: context.Watch);
                    documents.AddProjectedLayout(new UiDocumentSource(LayoutDocumentId, UiDocumentKind.Layout,
                        Path.Combine(authorDirectory, LayoutFileName)), full, UsKernelSettingsHost.BehaviorRegionDefinition);
                    var accepted = documents.GetSnapshot(LayoutDocumentId);
                    layout = accepted?.HasLastGood == true && accepted.Layout != null ? accepted.Layout
                        : throw new InvalidOperationException("No valid US author layout: " + string.Join("; ", documents.Reports.Select(report => report.ToString())));
                    // Optional standalone styles are an explicit overriding authority, not a merge.
                    string stylePath = Path.Combine(authorDirectory, StyleFileName);
                    if (File.Exists(stylePath))
                    {
                        documents.Add(new UiDocumentSource(StyleDocumentId, UiDocumentKind.Style, stylePath));
                        if (documents.GetSnapshot(StyleDocumentId)?.HasLastGood != true)
                            throw new InvalidOperationException("No valid standalone US styles: " + string.Join("; ", documents.Reports.Select(report => report.ToString())));
                        styleId = StyleDocumentId;
                    }
                }
                var data = CreateSource(translation);
                host = UsKernelSettingsHost.CreateBehaviorRegion(data, context.Metrics, translation, UsTheme.Surface(),
                    UsOfflineBehaviorSource.Identity, layout);
                if (documents != null && !documents.Attach(host, LayoutDocumentId, styleId))
                    throw new InvalidOperationException("US author documents could not attach: " + string.Join("; ", documents.Reports.Select(report => report.ToString())));
                var currentHost = host;
                string source = UsOfflineBehaviorSource.Identity + "; language=" + context.Language
                    + (documents == null ? "; layout=embedded; watch=off" : "; layout-origin=Documents.GetSnapshot; styles="
                        + (styleId == null ? "layout-inline" : StyleFileName + " overrides layout-inline"));
                return new UiPreviewInstance(host, source, context.MeasurementSource, documents,
                    available => UsKernelSettingsHost.PrepareBehaviorRegion(currentHost, available), documents);
            }
            catch
            {
                try { host?.Dispose(); } finally { documents?.Dispose(); }
                throw;
            }
        });
    }

    private static UsOfflineBehaviorSource CreateSource(IUiTranslation translation)
    {
        const string race = "WorkbenchPawn", child = "WorkbenchXeno", package = "us.offline.sample";
        var definitions = new DefinitionCatalog(720, 216, fullDevCandidate: true);
        definitions.AddBehaviorSource(new BehaviorSource { Id = "us.offline/quiet", Domain = new BehaviorDomain("Pawn", "", race),
            Contributions = new List<BehaviorContribution> {
                new() { Action = CoreActions.Work, Values = new BehaviorValues { Volume = .65, Scope = BehaviorScope.AnyOccurrence } },
                new() { Action = CoreActions.Select, Values = new BehaviorValues { Pitch = 1.1 } }
            } });
        definitions.Publish();
        var raceIdentity = new UsRaceIdentity(race, "Workbench Pawn", UsRaceSourceKind.ThirdParty, package,
            "US isolated Core author input", false, UsRaceLifeKind.Humanlike);
        UsRosterTarget Row(string xeno)
        {
            var domain = new VoicePackDomainView(xeno == "" ? SqueakVoicePackScope.Race : SqueakVoicePackScope.Xenotype,
                race, xeno, xeno == "" ? "Workbench Pawn" : "Workbench Xeno", "isolated author input",
                SqueakVoicePackDomainState.Available, false, false, false, 0, 0, 0,
                Array.Empty<string>(), Array.Empty<VoicePackRowView>(), "Workbench Pawn");
            return new UsRosterTarget(race, xeno, domain.DisplayName, package, "US isolated Core author input", raceIdentity, domain);
        }
        var source = new UsOfflineBehaviorSource(definitions, translation, new[] { Row(""), Row(child) });
        source.ViewState.SelectedRaceDefName = race;
        source.ViewState.SelectedTargetName = child;
        source.SetTuningDomain(race, child);
        // Begin in the Pawn profile. Domain buttons retain the explicit isolated sample target.
        source.SetTuningLayer(0);
        return source;
    }

    private sealed class KeyedTranslation : IUiTranslation
    {
        private readonly IReadOnlyDictionary<string, string> text;
        private readonly IUiTranslation? fallback;
        private readonly int languageRevision;
        internal KeyedTranslation(string language, IUiTranslation? fallback = null)
        {
            language = language == "en" ? "English" : language == "zh-Hans" ? "ChineseSimplified" : language;
            if (language != "English" && language != "ChineseSimplified") throw new ArgumentException("Choose English or ChineseSimplified.", nameof(language));
            languageRevision = language == "English" ? 1 : 2;
            this.fallback = fallback;
            using Stream input = typeof(UsBehaviorPreviewProvider).Assembly.GetManifestResourceStream("UniversalSqueaker.Preview." + language + ".xml")
                ?? throw new InvalidOperationException("Missing actual US Keyed resource.");
            // The shipped table retains repeated identical legacy keys. Preserve their actual
            // text; a conflicting duplicate is an invalid author input, not a guessed translation.
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var element in XDocument.Load(input).Root!.Elements())
            {
                string key = element.Name.LocalName;
                if (table.TryGetValue(key, out string? prior) && prior != element.Value)
                    throw new InvalidOperationException("Conflicting actual US Keyed entry: " + key);
                table[key] = element.Value;
            }
            text = table;
        }
        public string Translate(string key) => text.TryGetValue(key, out string? value) ? value
            : key.StartsWith("US.", StringComparison.Ordinal) ? key : fallback?.Translate(key) ?? key;
        public int TranslationRevision => unchecked((fallback?.TranslationRevision ?? 0) * 2 + languageRevision);
    }

    // This author's explicit headless model is intentionally structural. The native Tools host
    // supplies its actual font metrics through the other overload when rendering.
    private sealed class CharacterCellMetrics : ITextMetrics
    {
        private static float Size(UiFont font) => font == UiFont.Medium ? 22 : font == UiFont.Tiny ? 14 : 16;
        public float MeasureWidth(string text, UiFont font) => (text ?? "").Split('\n').Max(line => line.Sum(character => character >= 0x2e80 ? Size(font) : Size(font) * .5f));
        public float MeasureText(string text, UiFont font, float width)
        {
            float line = Size(font) * 1.4f;
            return (text ?? "").Split('\n').Sum(value => Math.Max(1, (int)Math.Ceiling(MeasureWidth(value, font) / Math.Max(1, width))) * line);
        }
    }
}
