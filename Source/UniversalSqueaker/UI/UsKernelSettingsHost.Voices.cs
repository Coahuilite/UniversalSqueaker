using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public static partial class UsKernelSettingsHost
{
    internal static void NavigateBehaviorOutput(VoicePacksPageState state) => NavigatePage(state, "Output");
    private static readonly string[] ToolPages = { "Behavior", "Presets", "Output", "Fallback", "Appearance", "Diagnostics", "Restore" };
    private static bool IsToolPage(string page) => Array.IndexOf(ToolPages, page) >= 0;
    private static string LegacyPage(string tab) => tab switch { "Tuning" => "Behavior", "Presets" => "Presets", "Advanced" => "Diagnostics", _ => "Voices" };
    private static void SetPage(VoicePacksPageState state, string page)
    {
        if (state.Page != page)
        {
            state.HelpPanelOpen = false;
            state.PinnedHelpRef = ""; state.PinnedHelpContext = "";
            state.FollowHelpRef = ""; state.FollowHelpContext = ""; state.FollowHelpLabel = ""; state.HelpScope = "";
        }
        state.Page = page;
        if (IsToolPage(page)) state.LastToolPage = page;
        if (TuningPage(page)) state.LastTuningPage = page;
        else if (IsToolPage(page)) state.LastSystemPage = page;
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
    internal static UiLayoutManifest PageDefinition(bool wide, float helpHeight = 128f, bool? rosterWide = null, bool deferRoster = false) => UiLayoutManifest.Parse(ReadManifest()
        .Replace("Id=\"b-roster-plate\" Padding=\"4\"", "Id=\"b-roster-plate\" Padding=\"4\"" + (deferRoster ? " DeferClippedDisplay=\"true\"" : ""))
        .Replace("Id=\"voice-split\" Breakpoint=\"1\"", "Id=\"voice-split\" Breakpoint=\"" + (wide ? "1" : "99999") + "\"")
        .Replace("Id=\"b-roster-split\" Breakpoint=\"1\"", "Id=\"b-roster-split\" Breakpoint=\"" + ((rosterWide ?? wide) ? "1" : "99999") + "\"")
        .Replace("Id=\"b-list-column\" Width=\"300\"", "Id=\"b-list-column\"" + ((rosterWide ?? wide) ? " Width=\"300\"" : ""))
        .Replace("Id=\"b-final-split\" Breakpoint=\"1\"", "Id=\"b-final-split\" Breakpoint=\"" + (wide ? "1" : "99999") + "\"")
        .Replace("Id=\"b-final-list\" Width=\"300\"", "Id=\"b-final-list\"" + (wide ? " WidthBind=\"b-list-width\"" : ""))
        .Replace("Id=\"voice-detail-scroll\" Width=\"440\"", "Id=\"voice-detail-scroll\"" + (wide ? " Width=\"440\"" : ""))
        .Replace("Id=\"tool-nav\" Cols=\"7\"", "Id=\"tool-nav\" Cols=\"" + (wide ? "7" : "3") + "\"")
        .Replace("Id=\"help-drawer\" Height=\"128\"", "Id=\"help-drawer\" Height=\"" + helpHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\""));

    internal static bool UsesDeferredRoster(UiHost host) => host.Manifest.Templates.TryGetValue("b-roster-row", out UiElementSpec? row)
        && row.Children.Any(child => child.Id == "b-roster-plate" && child.TryGetBoolean("DeferClippedDisplay", out bool enabled) && enabled);

    /// <summary>Use the public arranged page, not copied window-chrome constants, for the optional
    /// help budget. Runs only after a content/size/text change while open; compatible definitions
    /// retain node/editor/scroll identity. The second bounded arrange accounts for scrollbar width.</summary>
    public static void PreparePage(UiHost host, UiLayoutPoint available)
    {
        if (!host.Bindings.TryGet("page-state", out VoicePacksPageState state)) return;
        bool deferRoster = UsesDeferredRoster(host);
        bool rosterWide = WindowChromeLayout.UsesWideRoster(available.X);
        bool rosterChanged = state.RosterIsWide != rosterWide;
        state.RosterWideLayout = rosterWide;
        if (rosterChanged) host.UpdateDefinition(PageDefinition(state.WideLayout, state.HelpHeight, rosterWide, deferRoster));
        float listWidth = Math.Max(300, Math.Min(400, (available.X - 24) * .4f));
        if (Math.Abs(state.RosterListWidth - listWidth) > .01f) { state.RosterListWidth = listWidth; host.Invalidate(); }
        PrepareHelpLayout(host, state, available, height => host.UpdateDefinition(PageDefinition(state.WideLayout, height, state.RosterIsWide, deferRoster)));
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
        string EditContext() => TuningContextLabel(source, t, s.TuningLayer,
            s.TuningLayer == 0 ? "" : s.TuningRaceDefName, s.TuningLayer == 2 ? s.TuningXenotypeDefName : "");
        void OpenHelp(string topic, string context)
        {
            bool changed = DisplayHelpRef(s) != topic || DisplayHelpContext(s) != context;
            s.PinnedHelpRef = ""; s.PinnedHelpContext = "";
            s.FollowHelpRef = topic; s.FollowHelpContext = context;
            s.FollowHelpLabel = "";
            s.HelpScope = HelpScope(source);
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
        void ReturnVoiceList()
        {
            s.DetailOpen = false;
            if (s.PackArrivalFromDirectory) { s.BrowseDefinitions = true; s.PackArrivalFromDirectory = false; }
            bumper.Bump();
        }
        writes.Command("page-back", () => {
            if (s.Page == "Voices" && (s.DetailOpen || s.PackArrivalFromDirectory)) ReturnVoiceList();
            else ReturnToPreviousPage(s);
            bumper.Bump();
        });
        writes.Command("voice-back", ReturnVoiceList);
        writes.Command("voice-detail-cancel", ReturnVoiceList,
            () => !s.HelpPanelOpen && (s.DetailOpen || s.PackArrivalFromDirectory));
        writes.Command("page-cancel", () => {
            if (s.HelpPanelOpen) s.HelpPanelOpen = false;
            else if (s.Page == "Voices" && !s.RosterIsWide && s.DetailOpen) ReturnVoiceList();
            else if (s.Page == "Voices") {
                if (s.BrowseDefinitions) { s.HighlightedTarget = ""; source.CancelDomainSelection(); }
                else s.SelectedPackKey = "";
            }
            else ReturnToPreviousPage(s);
            bumper.Bump();
        }, () => s.HelpPanelOpen || s.Page != "Voices" || (!s.RosterIsWide && s.DetailOpen)
            || (s.BrowseDefinitions ? s.HighlightedTarget != "" : s.SelectedPackKey != "") || s.PackArrivalFromDirectory);
        b.BindReadOnly<bool>("voice-packs", () => !s.BrowseDefinitions);
        b.BindReadOnly<bool>("voice-definitions", () => s.BrowseDefinitions);
        writes.Command("browse-packs", () => { s.BrowseDefinitions = false; s.DetailOpen = false; bumper.Bump(); });
        writes.Command("browse-definitions", () => { s.BrowseDefinitions = true; s.DetailOpen = false; bumper.Bump(); });
        b.BindReadOnly<bool>("voice-list-visible", () => !s.BrowseDefinitions && (s.RosterIsWide || !s.DetailOpen));
        b.BindReadOnly<bool>("voice-detail-visible", () => !s.BrowseDefinitions && (s.RosterIsWide || s.DetailOpen));
        b.BindReadOnly<bool>("voice-compact-back", () => (!s.RosterIsWide && s.DetailOpen) || s.PackArrivalFromDirectory);
        b.BindReadOnly<bool>("voice-directory-return", () => s.PackArrivalFromDirectory && !s.BrowseDefinitions && !s.DetailOpen);
        PackCardView? ViewedPack()
        {
            var view = source.BuildView();
            var cards = view.AllPackCards.Count > 0 ? view.AllPackCards : view.PackCards;
            for (int i = 0; i < cards.Count; i++) if (cards[i].Key == s.SelectedPackKey) return cards[i];
            return null;
        }
        VoicePackDomainView? EditingDomain()
        {
            var domain = source.BuildView().SelectedDomain;
            return !s.DomainSelectionCanceled && domain.HasValue && domain.Value.RaceDefName != ""
                && !domain.Value.IsDormant && !domain.Value.IsTargetUnavailable && !domain.Value.HasCanonicalConflict
                && (domain.Value.Scope == SqueakVoicePackScope.Race || domain.Value.TargetDefName != "") ? domain : null;
        }
        VoicePackDomainView? FallbackDomain()
        {
            var domain = source.BuildView().SelectedDomain;
            if (s.DomainSelectionCanceled || !domain.HasValue || domain.Value.RaceDefName == "") return null;
            // A Xenotype's availability does not decide whether its loaded parent has a race table.
            if (domain.Value.Scope == SqueakVoicePackScope.Race) return domain.Value.IsTargetUnavailable ? null : domain;
            var races = RaceDefinitionCatalog.Current.Races;
            for (int i = 0; i < races.Count; i++) if (races[i].DefName == domain.Value.RaceDefName) return domain;
            return null;
        }
        b.BindReadOnly<bool>("voice-has-viewed-pack", () => ViewedPack().HasValue);
        b.BindReadOnly<bool>("voice-has-edit-domain", () => EditingDomain().HasValue);
        b.BindReadOnly<bool>("voice-can-fallback", () => FallbackDomain().HasValue);
        b.BindReadOnly<string>("voice-detail-title", () => {
            var card = ViewedPack();
            return !card.HasValue ? s.SelectedPackKey == "" ? T("ChoosePackNote") : T("PackUnavailable") + ": " + s.SelectedPackKey
                : T("ViewingPack") + ": " + card.Value.Label + (source.BuildView().PackCards.Any(x => x.Key == card.Value.Key) ? "" : " · " + T("HiddenPack"));
        });
        b.BindReadOnly<string>("voice-editing-target", () => {
            var domain = EditingDomain();
            return domain.HasValue ? T("Editing") + ": " + domain.Value.DisplayName : "";
        });
        b.BindReadOnly<string>("voice-detail-meta", () => {
            var card = ViewedPack();
            return card.HasValue ? card.Value.ModName + " · " + card.Value.Author + "\n" + card.Value.DefName + "\n" + card.Value.Coverage : "";
        });
        b.BindReadOnly<string>("fallback-arrival-race", () => s.FallbackSelectedRace);
        b.BindReadOnly<string>("voice-query-status", () =>
        {
            var view = source.BuildView();
            if (view.PackCards.Count > 0) return "";
            if (s.XenotypeFilter != "" && !view.BiotechActive) return T("BiotechOff");
            if (s.RaceFilter == "" && s.XenotypeFilter == "") return "";
            bool supply = view.AllPackCards.Any(card => card.Rows.Any(row =>
                (s.RaceFilter == "" || row.RaceDefName == s.RaceFilter) && (s.XenotypeFilter == "" || row.TargetDefName == s.XenotypeFilter)));
            string target = s.XenotypeFilter != "" ? s.XenotypeFilter : s.RaceFilter;
            return target + " · " + T(supply ? "TargetFiltered" : "TargetNoPacks");
        });
        b.BindReadOnly<bool>("voice-filters-open", () => s.FiltersOpen);
        writes.Command("voice-filters", () => { s.FiltersOpen = !s.FiltersOpen; bumper.Bump(); });
        writes.Command("voice-refresh", () => { s.EnabledFilterIdentity = ""; bumper.Bump(); });
        b.BindReadOnly<bool>("voice-stable-filter", () => s.DomainFilter.EnabledOnly);
        b.BindReadOnly<string>("voice-summary", () => { source.BuildView(); return string.Format(T("Summary"), s.PackSelected, s.PackTotal, s.OrphanSelectionCount)
            + (SqueakerRuntime.PolicyStatus == "OrdinaryActive" ? " · " + T("OrdinaryReady") : ""); });
        b.BindReadOnly<string>("voice-page-count", () => { int count = source.BuildView().PackCards.Count; return string.Format(T("PageCount"), Math.Min(s.PackPage + 1, Math.Max(1, (count + 19) / 20)), Math.Max(1, (count + 19) / 20)); });
        b.BindReadOnly<bool>("voice-pager", () => source.BuildView().PackCards.Count > 20);
        writes.Command("voice-prev", () => { s.PackPage = Math.Max(0, s.PackPage - 1); s.DetailOpen = false; bumper.Bump(); });
        writes.Command("voice-next", () => { s.PackPage = Math.Min(Math.Max(0, (source.BuildView().PackCards.Count - 1) / 20), s.PackPage + 1); s.DetailOpen = false; bumper.Bump(); });
        writes.Command("voice-fallback", () => {
            var domain = FallbackDomain();
            if (!domain.HasValue) return;
            string race = domain.Value.RaceDefName;
            VoicePacksPageModel.SetFallbackSelection(s, race, "");
            if (!source.BuildView().FallbackRaces.Any(x => x.DefName == race))
                s.PendingFallbackRace = race;
            Go("Fallback");
        }, () => FallbackDomain().HasValue);
        writes.Command("voice-behavior", () => {
            var domain = EditingDomain();
            if (!domain.HasValue) return;
            s.TuningLayer = domain.Value.Scope == SqueakVoicePackScope.Race ? 1 : 2;
            s.TuningRaceDefName = domain.Value.RaceDefName;
            s.TuningXenotypeDefName = domain.Value.Scope == SqueakVoicePackScope.Xenotype ? domain.Value.TargetDefName : "";
            Go("Behavior");
        }, () => EditingDomain().HasValue);
        writes.Command("open-topic", () => {
            if (s.HelpPanelOpen) { s.HelpPanelOpen = false; bumper.Bump(); return; }
            if (s.HelpScope != HelpScope(source) || s.FollowHelpRef == "") OpenHelp(PageHelpRef(s), HelpPageContext(source, t));
            else { s.HelpPanelOpen = true; bumper.Bump(); }
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
        b.BindReadOnly<string>("help-display-ref", () => DisplayHelpRef(s));
        b.BindReadOnly<string>("help-display-context", () => DisplayHelpContext(s));
        b.BindReadOnly<string>("help-scope", () => HelpScope(source));
        b.BindReadOnly<string>("help-page-context", () => HelpPageContext(source, t));
        b.BindReadOnly<string>("help-toggle-label", () => s.HelpPanelOpen ? t.Translate("US.UI.CloseHelp") : t.Translate("US.UI.Help"));
        b.BindReadOnly<bool>("help-closed", () => !s.HelpPanelOpen);
        b.BindReadOnly<string>("help-title", () => {
            if (s.PinnedHelpRef.Length == 0 && s.FollowHelpLabel.Length > 0) return s.FollowHelpLabel;
            UsHelpCatalog.TryGetSection(source.SectionHelpRef(s.ActiveSectionKey), out var section);
            string topic = DisplayHelpRef(s);
            var display = UsHelpPanelLogic.Resolve(section, topic, t.Translate);
            return UsHelpCatalog.TryFindItem(topic, out _, out _) ? display.LabelText : display.TitleText;
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
        b.BindReadOnly<string>("action-row-diagnostic", () => {
            string[] parts = (production?.RowDiagnostic ?? "").Split(new[] { '\n' }, 2);
            return parts[0] == "" ? "" : t.Translate(parts[0]);
        });
        b.BindReadOnly<string>("transaction-diagnostics", () => s.SourceChangeDiagnostic + "\n" + (production?.RowDiagnostic ?? ""));
        b.BindReadOnly<string>("kernel-status-raw", () => SqueakerRuntime.PolicyStatus);
        string carrier = typeof(UiHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        b.BindReadOnly<string>("dependency-status", () => source.BuildIdentity + "\nFerriteLib " + carrier + "\n" + T("ObservationBoundary"));
        BindRoster(source, writes, bumper, t);
        BindBehaviorSheet(source, writes, bumper, t);
        BindFinalSheet(source, writes, bumper, t);
    }
}
