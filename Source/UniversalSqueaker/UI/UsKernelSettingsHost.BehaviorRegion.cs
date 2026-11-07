#if US_OFFLINE_WORKBENCH
using System;
using System.Linq;
using System.Xml.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public static partial class UsKernelSettingsHost
{


    /// <summary>Full shipped author document, not a second preview XML. Development API: public-unstable.</summary>
    public static string ReadBehaviorAuthorDocument() => ReadManifest();

    /// <summary>Selects the real behavior/help region, shared styles and action template from the
    /// full author document. Unrelated workspaces are not materialized. A selected development
    /// document uses exactly the same extraction/parse path; adoption remains the caller's transaction.</summary>
    public static UiLayoutManifest BehaviorRegionDefinition(string? authorDocument = null)
    {
        var document = XDocument.Parse(authorDocument ?? ReadBehaviorAuthorDocument());
        var page = document.Root ?? throw new InvalidOperationException("Missing US UiPage.");
        XElement Element(string id) => page.Descendants().Single(element => (string?)element.Attribute("Id") == id);
        var root = new XElement(Element("page-root"));
        root.RemoveNodes();
        var header = new XElement(Element("header-band"));
        header.Elements().Where(element => (string?)element.Attribute("Id") is not ("page-title" or "topic-link" or "topic-link-close")).Remove();
        var body = new XElement(Element("business-body"));
        body.RemoveNodes();
        body.Add(new XElement(Element("page-Behavior-scroll")));
        var help = new XElement(Element("help-drawer"));
        var footer = new XElement(Element("footer-band"));
        footer.Elements().Where(element => (string?)element.Attribute("Id") != "footer").Remove();
        root.Add(header, body, help, footer);
        var selected = new XElement(page.Name, page.Attributes(),
            new XElement(page.Element("Styles") ?? throw new InvalidOperationException("Missing US styles.")),
            root, new XElement("Templates", new XElement(Element("b-action-row"))));
        return UiLayoutManifest.Parse(selected.ToString(SaveOptions.DisableFormatting));
    }

    /// <summary>Build the actual region with author-supplied, isolated data and host services.
    /// No game singleton, native metrics, logger, persistence or Tools connection is created here.
    /// The caller owns the Host lifetime and must explicitly advance it through the selected backend.
    /// This constructs content; it does not certify a native renderer or an agent transport.</summary>
    public static UiHost CreateBehaviorRegion(IUsBehaviorPageSource source, ITextMetrics metrics,
        IUiTranslation translation, UiTheme theme, string sampleIdentity, UiLayoutManifest? authorLayout = null)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (metrics == null) throw new ArgumentNullException(nameof(metrics));
        if (translation == null) throw new ArgumentNullException(nameof(translation));
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        if (string.IsNullOrWhiteSpace(sampleIdentity)) throw new ArgumentException("Name the isolated author input.", nameof(sampleIdentity));
        var state = source.ViewState;
        if (state.Page != "Behavior") throw new ArgumentException("The region requires an explicit Behavior page state.", nameof(source));
        UsKernelWidgetRegistrar.EnsureBehaviorRegistered();
        var writes = new UsWriteBindings(new UiBindings());
        var b = writes.Bindings;
        var bumper = new SessionRevisionBumper();
        string Context() => TuningContextLabel(source.ReadView().Roster, translation, state.TuningLayer, state.TuningRaceDefName, state.TuningXenotypeDefName);
        b.BindReadOnly<VoicePacksPageState>("page-state", () => state);
        b.BindReadOnly<bool>("page-Behavior", () => state.Page == "Behavior");
        b.BindReadOnly<string>("voice-page-title", () => translation.Translate("US.UI.Behavior"));
        b.BindReadOnly<string>("edit-domain-context", () => translation.Translate("US.UI.EditContext") + ": " + Context());
        b.BindReadOnly<string>("help-page-context", Context);
        b.BindReadOnly<string>("help-scope", () => HelpScope(state));
        b.BindReadOnly<bool>("help-open", () => state.HelpPanelOpen);
        b.BindReadOnly<bool>("help-closed", () => !state.HelpPanelOpen);
        b.BindReadOnly<string>("help-section-ref", () => "us/scope-tree");
        b.BindReadOnly<string>("pinned-help-ref", () => state.PinnedHelpRef);
        b.BindReadOnly<string>("help-display-ref", () => DisplayHelpRef(state));
        b.BindReadOnly<string>("help-display-context", () => DisplayHelpContext(state));
        b.BindReadOnly<string>("help-title", () => {
            if (state.PinnedHelpRef.Length == 0 && state.FollowHelpLabel.Length > 0) return state.FollowHelpLabel;
            UsHelpCatalog.TryGetSection("us/scope-tree", out var section);
            var display = UsHelpPanelLogic.Resolve(section, DisplayHelpRef(state), translation.Translate);
            return UsHelpCatalog.TryFindItem(DisplayHelpRef(state), out _, out _) ? display.LabelText : display.TitleText;
        });
        b.BindReadOnly<string>("build-identity", () => sampleIdentity);
        b.BindReadOnly<string>("save-status", () => "Unknown");
        b.BindReadOnly<bool>("save-status-visible", () => false);
        b.BindReadOnly<bool>("is-dirty", () => false);
        writes.Command("open-topic", () => {
            if (!state.HelpPanelOpen && state.FollowHelpRef.Length == 0) {
                state.FollowHelpRef = "us/scope-tree"; state.FollowHelpContext = Context();
                state.HelpScope = HelpScope(state); bumper.ResetHelpScroll();
            }
            state.HelpPanelOpen = !state.HelpPanelOpen; bumper.Bump();
        });
        writes.Command("cancel-help", () => { state.HelpPanelOpen = false; bumper.Bump(); }, () => state.HelpPanelOpen);
        writes.Command("cancel-tuning-target", () => { source.SetTuningSelectedAction(""); bumper.Bump(); },
            () => !state.HelpPanelOpen && state.TuningSelectedAction.Length > 0);
        writes.Command("cancel-tuning-context", () => { source.SetTuningLayer(0); VoicePacksPageModel.CancelTuningContext(state); bumper.Bump(); },
            () => !state.HelpPanelOpen && VoicePacksPageModel.CanCancelTuningContext(state));
        writes.Command("page-cancel", () => { state.HelpPanelOpen = false; bumper.Bump(); }, () => state.HelpPanelOpen);
        BindBehaviorSheet(source, writes, bumper, translation);
        var host = new UiHost(Source, authorLayout ?? BehaviorRegionDefinition(), b, theme, metrics, translation);
        bumper.Attach(host);
        host.SetLayoutInput("help-drawer", new UiLayoutInput(height: state.HelpHeight));
        host.Session.HelpGracePasses = 0;
        if (source is UsOfflineBehaviorSource sample)
        {
            try { sample.Attach(host); }
            catch { host.Dispose(); throw; }
        }
        return host;
    }

    /// <summary>Explicit layout preparation, never called by observation. Uses the ordinary bounded
    /// help measurement/cache. Author document adoption is a separate explicit UpdateDefinition.</summary>
    public static void PrepareBehaviorRegion(UiHost host, UiLayoutPoint available)
    {
        if (!host.Bindings.TryGet("page-state", out VoicePacksPageState state)) return;
        host.SetLayoutInput("help-drawer", new UiLayoutInput(height: state.HelpHeight));
        PrepareHelpLayout(host, state, available, height => host.SetLayoutInput("help-drawer", new UiLayoutInput(height: height)));
    }
}

#endif
