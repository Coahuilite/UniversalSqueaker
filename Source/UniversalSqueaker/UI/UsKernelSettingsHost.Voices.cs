using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public static partial class UsKernelSettingsHost
{
    private static readonly string[] ToolPages = { "Behavior", "Presets", "Output", "Fallback", "Appearance", "Diagnostics", "Restore" };
    private static bool IsToolPage(string page) => Array.IndexOf(ToolPages, page) >= 0;
    private static string PageHelpRef(VoicePacksPageState state) => state.Page switch
    {
        "Voices" => state.BrowseDefinitions ? "us/race-board" : "us/pack-cards",
        "Behavior" => "us/scope-tree", "Presets" => "us/preset-list", "Output" => "us/basic-tuning",
        "Fallback" => "us/fallback-editor", "Appearance" => "us/theme-picker",
        "Diagnostics" => "us/diagnostics", "Restore" => "us/reset-center", _ => "us/page-title"
    };
    private static string LegacyPage(string tab) => tab switch { "Tuning" => "Behavior", "Presets" => "Presets", "Advanced" => "Diagnostics", _ => "Voices" };
    private static void SetPage(VoicePacksPageState state, string page)
    {
        if (state.Page != page)
        {
            state.HelpPanelOpen = false;
            state.PinnedHelpRef = ""; state.PinnedHelpContext = "";
        }
        state.Page = page;
        if (IsToolPage(page)) state.LastToolPage = page;
        state.ActiveTab = page == "Behavior" ? "Tuning" : page == "Presets" ? "Presets" :
            page == "Appearance" || page == "Diagnostics" || page == "Restore" ? "Advanced" : "Packs";
        state.ActiveSectionKey = page switch { "Behavior" => "scope-tree", "Output" => "basic-tuning",
            "Appearance" => "theme-picker", "Diagnostics" => "diagnostics", "Fallback" => "fallback-editor",
            "Presets" => "preset-list", _ => "packs-results" };
    }
    private static void NavigatePage(VoicePacksPageState state, string page)
    {
        if (page == "Tools") page = state.LastToolPage;
        if (state.Page == page) return;
        if (page == "Voices") state.PageHistory.Clear();
        // Sibling tools replace the current destination, not the return trail to Voices.
        else if (!(IsToolPage(page) && (IsToolPage(state.Page) || state.Page == "Tools")))
            state.PageHistory.Add(state.Page);
        SetPage(state, page);
    }
    private static void ReturnToPreviousPage(VoicePacksPageState state)
    {
        int last = state.PageHistory.Count - 1;
        string page = last < 0 ? "Voices" : state.PageHistory[last];
        if (last >= 0) state.PageHistory.RemoveAt(last);
        SetPage(state, page);
    }
    private static void ApplyPackArrival(IUsKernelSettingsSource source, string destination)
    {
        var cards = source.BuildView().PackCards;
        int marker = destination.IndexOf('#');
        string key = marker < 0 ? "" : destination.Substring(marker + 1).Split(new[] { '|' })[0];
        int index = -1;
        for (int i = 0; i < cards.Count; i++) if (cards[i].Key == key) { index = i; break; }
        source.ViewState.SelectedPackKey = index >= 0 ? key : "";
        if (index >= 0) source.ViewState.PackPage = index / 20;
    }
    // Definition updates preserve the host and every compatible node identity, including scroll/drafts.
    internal static UiLayoutManifest PageDefinition(bool wide, float helpHeight = 128f) => UiLayoutManifest.Parse(ReadManifest()
        .Replace("Id=\"voice-split\" Breakpoint=\"1\"", "Id=\"voice-split\" Breakpoint=\"" + (wide ? "1" : "99999") + "\"")
        .Replace("Id=\"voice-detail-scroll\" Width=\"440\"", "Id=\"voice-detail-scroll\"" + (wide ? " Width=\"440\"" : ""))
        .Replace("Id=\"tool-nav\" Cols=\"7\"", "Id=\"tool-nav\" Cols=\"" + (wide ? "7" : "3") + "\"")
        .Replace("Id=\"help-drawer\" Height=\"128\"", "Id=\"help-drawer\" Height=\"" + helpHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\""));

    /// <summary>Use the public arranged page, not copied window-chrome constants, for the optional
    /// help budget. Runs only after a content/size/text change while open; compatible definitions
    /// retain node/editor/scroll identity. The second bounded arrange accounts for scrollbar width.</summary>
    public static void PreparePage(UiHost host, UiLayoutPoint available)
    {
        if (!host.Bindings.TryGet("page-state", out VoicePacksPageState state) || !state.HelpPanelOpen) return;
        var ctx = host.CreateContext(available.X);
        string topic = state.PinnedHelpRef.Length > 0 ? state.PinnedHelpRef : host.Session.CurrentHelpRef ?? "";
        UsHelpCatalog.TryGetSection(host.Bindings.TryGet("help-section-ref", out string section) ? section : "", out var fallback);
        var display = UsHelpPanelLogic.Resolve(fallback, topic, key => UsKernelDraw.Keyed(ctx, key));
        string text = display.TitleText + "\n" + display.LabelText + "\n" + display.BodyText + "\n" + state.PinnedHelpContext;
        var environment = ctx.Metrics as IUiTextMetricsEnvironment;
        var small = environment?.CaptureEnvironment(UiFont.Small);
        var tiny = environment?.CaptureEnvironment(UiFont.Tiny);
        if (state.HelpLayoutRevision == host.Session.ContentRevision && state.HelpViewWidth == available.X
            && state.HelpViewHeight == available.Y && state.HelpLayoutText == text && small != null && tiny != null
            && small.Equals(state.HelpSmallEnvironment) && tiny.Equals(state.HelpTinyEnvironment)) return;
        // Current text/environment may change without a business write (language or hover grace).
        if (state.HelpLayoutText != text || !Equals(small, state.HelpSmallEnvironment)
            || !Equals(tiny, state.HelpTinyEnvironment)) host.Invalidate();
        for (int pass = 0; pass < 2; pass++)
        {
            var layout = host.MeasureAndArrange(available);
            if (!layout.RectById.TryGetValue("business-body", out var body)
                || !layout.RectById.TryGetValue("footer-band", out var footer)
                || !layout.RectById.TryGetValue("help-header", out var header)
                || !layout.ScrollContents.TryGetValue("help-scroll", out var content)) break;
            // Use the real available bottom and measured footer height: an initial oversized
            // placeholder may itself push footer.Y down. Padding/gaps belong to our manifest,
            // not to copied window chrome. The resulting cap is independent of that placeholder.
            float budget = Math.Max(0f, (available.Y - 12f - footer.Height - 6f - body.Y) / 3f);
            float height = Math.Min(budget, header.Height + 4f + content.Height);
            if (Math.Abs(state.HelpHeight - height) < .01f) break;
            state.HelpHeight = height;
            host.UpdateDefinition(PageDefinition(state.WideLayout, height));
        }
        state.HelpViewWidth = available.X; state.HelpViewHeight = available.Y;
        state.HelpLayoutText = text; state.HelpLayoutRevision = host.Session.ContentRevision;
        state.HelpSmallEnvironment = small; state.HelpTinyEnvironment = tiny;
    }

    private static void BindVoiceManagement(IUsKernelSettingsSource source, UsWriteBindings writes,
        SessionRevisionBumper bumper, IUiTranslation t)
    {
        var b = writes.Bindings;
        var s = source.ViewState;
        b.BindReadOnly<VoicePacksPageState>("page-state", () => s);
        b.BindReadOnly<string>("voice-empty", () => "");
        string T(string key) => t.Translate("US.UI." + key);
        void Go(string page)
        {
            NavigatePage(s, page);
            bumper.Bump();
        }
        string EditContext() => s.TuningLayer == 0 ? T("PawnProfile") : s.TuningRaceDefName +
            (s.TuningLayer == 2 ? "/" + s.TuningXenotypeDefName : "");
        void OpenHelp(string topic, string context)
        {
            bool changed = s.PinnedHelpRef != topic || s.PinnedHelpContext != context;
            s.PinnedHelpRef = topic;
            s.PinnedHelpContext = context;
            s.HelpPanelOpen = true;
            if (changed) bumper.ResetHelpScroll();
            bumper.Bump();
        }
        foreach (string page in new[] { "Voices", "Tools", "Behavior", "Presets", "Output", "Fallback", "Appearance", "Diagnostics", "Restore", "Help" })
        {
            string p = page;
            b.BindReadOnly<bool>("page-" + p, () => s.Page == p);
            b.BindReadOnly<string>("go-" + p + "-payload", () => p);
            b.BindReadOnly<bool>("tool-is-" + p, () => s.Page == p);
        }
        writes.Action<string>("go-page", Go);
        writes.Command("go-home", () => Go("Voices"));
        b.BindReadOnly<bool>("page-tools-area", () => IsToolPage(s.Page));
        b.BindReadOnly<bool>("page-not-help", () => s.Page != "Help");
        b.BindReadOnly<bool>("tool-nav-wide", () => IsToolPage(s.Page) && s.WideLayout);
        b.BindReadOnly<bool>("tool-nav-compact", () => IsToolPage(s.Page) && !s.WideLayout);
        b.BindChoices<string>("tool-page-options", () => ToolPages.Select(p => new UiChoice<string>(T(p), p)).ToArray());
        writes.Value<string>("tool-page", () => s.Page, page => { if (IsToolPage(page)) Go(page); });
        b.BindReadOnly<string>("voice-page-title", () => T(s.Page));
        b.BindReadOnly<bool>("page-not-home", () => s.Page != "Voices");
        writes.Command("page-back", () => {
            if (s.Page == "Voices" && s.DetailOpen) s.DetailOpen = false;
            else ReturnToPreviousPage(s);
            bumper.Bump();
        });
        writes.Command("voice-back", () => { s.DetailOpen = false; bumper.Bump(); });
        writes.Command("voice-detail-cancel", () => { s.DetailOpen = false; s.SelectedPackKey = ""; bumper.Bump(); },
            () => !s.HelpPanelOpen && s.DetailOpen && s.SelectedPackKey != "");
        writes.Command("page-cancel", () => {
            if (s.HelpPanelOpen) s.HelpPanelOpen = false;
            else if (s.Page == "Voices") s.DetailOpen = false;
            else ReturnToPreviousPage(s);
            bumper.Bump();
        }, () => s.HelpPanelOpen || s.Page != "Voices" || (!s.WideLayout && s.DetailOpen));
        b.BindReadOnly<bool>("voice-packs", () => !s.BrowseDefinitions);
        b.BindReadOnly<bool>("voice-definitions", () => s.BrowseDefinitions);
        writes.Command("browse-packs", () => { s.BrowseDefinitions = false; bumper.Bump(); });
        writes.Command("browse-definitions", () => { s.BrowseDefinitions = true; s.BoardGroup = UsBoardBrowsePolicy.BoardGroup.All; bumper.Bump(); });
        b.BindReadOnly<bool>("voice-list-visible", () => !s.BrowseDefinitions && (s.WideLayout || !s.DetailOpen));
        b.BindReadOnly<bool>("voice-detail-visible", () => !s.BrowseDefinitions && (s.WideLayout || s.DetailOpen));
        b.BindReadOnly<bool>("voice-compact-back", () => !s.WideLayout && s.DetailOpen);
        b.BindReadOnly<string>("voice-detail-title", () => {
            var card = source.BuildView().PackCards.FirstOrDefault(x => x.Key == s.SelectedPackKey);
            return card.Key == null ? T("ChoosePack") : T("ViewingPack") + ": " + card.Label;
        });
        b.BindReadOnly<string>("voice-editing-target", () => {
            var domain = source.BuildView().SelectedDomain;
            return T("Editing") + ": " + (domain.HasValue ? domain.Value.DisplayName : T("ChooseDomain"));
        });
        b.BindReadOnly<string>("voice-detail-meta", () => {
            var card = source.BuildView().PackCards.FirstOrDefault(x => x.Key == s.SelectedPackKey);
            return card.Key == null ? T("ChoosePackNote") : card.ModName + " · " + card.Author + "\n" + card.DefName + "\n" + card.Coverage;
        });
        b.BindReadOnly<bool>("voice-filters-open", () => s.FiltersOpen);
        writes.Command("voice-filters", () => { s.FiltersOpen = !s.FiltersOpen; bumper.Bump(); });
        writes.Command("voice-refresh", () => { s.EnabledFilterIdentity = ""; bumper.Bump(); });
        b.BindReadOnly<bool>("voice-stable-filter", () => s.DomainFilter.EnabledOnly);
        b.BindReadOnly<string>("voice-summary", () => { source.BuildView(); return string.Format(T("Summary"), s.PackSelected, s.PackTotal, s.OrphanSelectionCount); });
        b.BindReadOnly<string>("voice-page-count", () => { int count = source.BuildView().PackCards.Count; return string.Format(T("PageCount"), Math.Min(s.PackPage + 1, Math.Max(1, (count + 19) / 20)), Math.Max(1, (count + 19) / 20)); });
        b.BindReadOnly<bool>("voice-pager", () => source.BuildView().PackCards.Count > 20);
        writes.Command("voice-prev", () => { s.PackPage = Math.Max(0, s.PackPage - 1); s.DetailOpen = false; bumper.Bump(); });
        writes.Command("voice-next", () => { s.PackPage = Math.Min(Math.Max(0, (source.BuildView().PackCards.Count - 1) / 20), s.PackPage + 1); s.DetailOpen = false; bumper.Bump(); });
        writes.Command("voice-fallback", () => {
            s.FallbackSelectedRace = s.SelectedRaceDefName;
            Go("Fallback");
        });
        writes.Command("voice-behavior", () => {
            if (s.SelectedRaceDefName != "") { s.TuningLayer = s.SelectedTargetName == "" ? 1 : 2; s.TuningRaceDefName = s.SelectedRaceDefName; s.TuningXenotypeDefName = s.SelectedTargetName; }
            Go("Behavior");
        });
        writes.Command("open-topic", () => {
            OpenHelp(PageHelpRef(s), T(s.Page));
        });
        writes.Action<string>("explain-topic", topic => OpenHelp(topic,
            topic.StartsWith("us/basic-tuning/", StringComparison.Ordinal) || topic.StartsWith("us/behavior-source/", StringComparison.Ordinal)
                ? EditContext() : topic == "us/fallback-editor" && s.FallbackSelectedRace != ""
                    ? s.FallbackSelectedRace : T(s.Page)));
        foreach (string topic in new[] { "us/behavior-source/selection", "us/scope-tree", "us/preset-list", "us/basic-tuning/select",
            "us/basic-tuning/eat-phase", "us/basic-tuning/eat-nutrition", "us/basic-tuning/eat-drug", "us/basic-tuning/activity", "us/attenuation-editor", "us/fallback-editor" })
            b.BindReadOnly<string>("explain:" + topic, () => topic);
        b.BindReadOnly<string>("pinned-help-ref", () => s.PinnedHelpRef);
        b.BindReadOnly<string>("pinned-help-context", () => s.PinnedHelpContext);
        b.BindReadOnly<string>("help-title", () => {
            UsHelpCatalog.TryGetSection(source.SectionHelpRef(s.ActiveSectionKey), out var section);
            var display = UsHelpPanelLogic.Resolve(section, s.PinnedHelpRef, t.Translate);
            return UsHelpCatalog.TryFindItem(s.PinnedHelpRef, out _, out _) ? display.LabelText : display.TitleText;
        });
        b.BindReadOnly<string>("voice-missing-selections", () => { source.BuildView(); return s.MissingSelections; });
        b.BindReadOnly<bool>("voice-has-missing", () => { source.BuildView(); return s.OrphanSelectionCount > 0; });
        b.BindReadOnly<bool>("restore-all-open", () => s.RestoreAllOpen);
        writes.Command("toggle-restore-all", () => { s.RestoreAllOpen = !s.RestoreAllOpen; bumper.Bump(); });
        b.BindReadOnly<bool>("save-failed", () => source.SaveStatus == "Failed");
        writes.Command("retry-save", () => { SqueakerMod.Instance?.RetrySettingsSave(); bumper.Bump(); });
        var production = source as UsKernelSettingsSource;
        b.BindReadOnly<bool>("source-edit-unavailable", () => production == null || s.TuningLayer == 0 || s.TuningRaceDefName == "" || (s.TuningLayer == 2 && s.TuningXenotypeDefName == ""));
        b.BindChoices<string>("behavior-source-options", () => production?.SourceChoices(t) ?? Array.Empty<UiChoice<string>>());
        writes.Value<string>("behavior-source", () => production?.SelectedSource ?? "", value => { production?.CommitSource(value); bumper.Bump(); });
        b.BindReadOnly<string>("behavior-source-status", () => production?.SourceStatus(t) ?? T("SourceUnavailable"));
        b.BindReadOnly<bool>("action-fields-visible", () => production != null && s.TuningSelectedAction != "" && source.CanResetTuningArea());
        b.BindReadOnly<string>("action-fields-target", () => T("Editing") + ": " + s.TuningSelectedAction + " · " + EditContext());
        b.BindReadOnly<string>("edit-domain-context", () => { source.BuildView(); return T("EditContext") + ": " + EditContext(); });
        // Retained editors belong to the exact action/domain, not mutable navigation state.
        // Previously visited rows remain hidden compatible nodes, so switching context cannot
        // publish an old draft into a different row or silently discard it.
        var editKeys = new List<string>();
        string CurrentEditKey() => s.TuningSelectedAction + "|" + (s.TuningLayer == 0 ? "" : s.TuningRaceDefName) + "|" + (s.TuningLayer == 2 ? s.TuningXenotypeDefName : "");
        b.BindReadOnly<IReadOnlyList<string>>("action-edit-keys", () => {
            source.BuildView();
            if (production == null || s.TuningSelectedAction == "" || !source.CanResetTuningArea()) return editKeys;
            string key = CurrentEditKey();
            if (editKeys.Contains(key)) return editKeys;
            string action = s.TuningSelectedAction;
            string race = s.TuningLayer == 0 ? "" : s.TuningRaceDefName;
            string child = s.TuningLayer == 2 ? s.TuningXenotypeDefName : "";
            editKeys.Add(key);
            string Address(string leaf) => UsWriteBindings.ItemKey("action-edit-keys", key, leaf);
            b.BindReadOnly<bool>(Address("current-row"), () => CurrentEditKey() == key);
            b.BindChoices<string>(Address("sparse-bool-options"), () => new[] { new UiChoice<string>(T("SourceInherit"), ""), new UiChoice<string>(T("Yes"), "True"), new UiChoice<string>(T("No"), "False") });
            foreach (string field in new[] { "enabled", "interval", "volume", "pitch", "talking", "age", "mood" })
            {
                string f = field;
                writes.ItemValue<string>("action-edit-keys", key, "action-field-" + f,
                    () => production.ReadActionField(f, action, race, child),
                    v => { production.CommitActionField(f, v, action, race, child); bumper.Bump(); });
                b.BindReadOnly<string>(Address("explain-" + f + "-topic"), () => "us/action-fields/" + f);
                writes.ItemAction<string>("action-edit-keys", key, "explain-" + f,
                    topic => OpenHelp(topic, action + " · " + (race == "" ? T("PawnProfile") : race + (child == "" ? "" : "/" + child))));
            }
            return editKeys;
        });
        b.BindReadOnly<string>("action-row-diagnostic", () => {
            string[] parts = (production?.RowDiagnostic ?? "").Split(new[] { '\n' }, 2);
            return parts[0] == "" ? "" : t.Translate(parts[0]);
        });
        b.BindReadOnly<string>("transaction-diagnostics", () => s.SourceChangeDiagnostic + "\n" + (production?.RowDiagnostic ?? ""));
        b.BindReadOnly<string>("kernel-status-raw", () => SqueakerRuntime.PolicyStatus);
        string carrier = typeof(UiHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        b.BindReadOnly<string>("dependency-status", () => source.BuildIdentity + "\nFerriteLib " + carrier + "\n" + T("ObservationBoundary"));
    }
}
