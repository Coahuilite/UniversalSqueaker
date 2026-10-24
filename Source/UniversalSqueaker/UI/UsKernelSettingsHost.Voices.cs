using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public static partial class UsKernelSettingsHost
{
    private static string LegacyPage(string tab) => tab switch { "Tuning" => "Behavior", "Presets" => "Presets", "Advanced" => "Diagnostics", _ => "Voices" };
    private static void SetPage(VoicePacksPageState state, string page)
    {
        state.Page = page;
        // Help keeps the originating section as the fallback for empty/unknown topic refs.
        if (page == "Help") return;
        state.ActiveTab = page == "Behavior" ? "Tuning" : page == "Presets" ? "Presets" :
            page == "Appearance" || page == "Diagnostics" || page == "Restore" ? "Advanced" : "Packs";
        state.ActiveSectionKey = page switch { "Behavior" => "scope-tree", "Output" => "basic-tuning",
            "Appearance" => "theme-picker", "Diagnostics" => "diagnostics", "Fallback" => "fallback-editor",
            "Presets" => "preset-list", _ => "packs-results" };
    }
    private static void NavigatePage(VoicePacksPageState state, string page)
    {
        if (state.Page != page) state.PageHistory.Add(state.Page);
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
    internal static UiLayoutManifest PageDefinition(bool wide) => UiLayoutManifest.Parse(ReadManifest()
        .Replace("Id=\"voice-split\" Breakpoint=\"0\"", "Id=\"voice-split\" Breakpoint=\"" + (wide ? "0" : "99999") + "\"")
        .Replace("Id=\"voice-detail-scroll\" Width=\"440\"", "Id=\"voice-detail-scroll\"" + (wide ? " Width=\"440\"" : "")));

    private static void BindVoiceManagement(IUsKernelSettingsSource source, UsWriteBindings writes,
        SessionRevisionBumper bumper, IUiTranslation t)
    {
        var b = writes.Bindings;
        var s = source.ViewState;
        b.BindReadOnly<string>("voice-empty", () => "");
        string T(string key) => t.Translate("US.UI." + key);
        void Go(string page)
        {
            NavigatePage(s, page);
            bumper.Bump();
        }
        foreach (string page in new[] { "Voices", "Tools", "Behavior", "Presets", "Output", "Fallback", "Appearance", "Diagnostics", "Restore", "Help" })
        {
            string p = page;
            b.BindReadOnly<bool>("page-" + p, () => s.Page == p);
            b.BindReadOnly<string>("go-" + p + "-payload", () => p);
        }
        writes.Action<string>("go-page", Go);
        b.BindReadOnly<string>("voice-page-title", () => T(s.Page));
        b.BindReadOnly<bool>("page-not-home", () => s.Page != "Voices");
        writes.Command("page-back", () => {
            if (s.Page == "Voices" && s.DetailOpen) s.DetailOpen = false;
            else ReturnToPreviousPage(s);
            bumper.Bump();
        });
        writes.Command("voice-back", () => { s.DetailOpen = false; bumper.Bump(); });
        writes.Command("voice-detail-cancel", () => { s.DetailOpen = false; s.SelectedPackKey = ""; bumper.Bump(); },
            () => s.DetailOpen && s.SelectedPackKey != "");
        writes.Command("page-cancel", () => {
            if (s.Page == "Voices") s.DetailOpen = false;
            else ReturnToPreviousPage(s);
            bumper.Bump();
        }, () => s.Page != "Voices" || (!s.WideLayout && s.DetailOpen));
        b.BindReadOnly<bool>("voice-packs", () => !s.BrowseDefinitions);
        b.BindReadOnly<bool>("voice-definitions", () => s.BrowseDefinitions);
        writes.Command("browse-packs", () => { s.BrowseDefinitions = false; bumper.Bump(); });
        writes.Command("browse-definitions", () => { s.BrowseDefinitions = true; s.BoardGroup = UsBoardBrowsePolicy.BoardGroup.All; bumper.Bump(); });
        b.BindReadOnly<bool>("voice-list-visible", () => !s.BrowseDefinitions && (s.WideLayout || !s.DetailOpen));
        b.BindReadOnly<bool>("voice-detail-visible", () => !s.BrowseDefinitions && (s.WideLayout || s.DetailOpen));
        b.BindReadOnly<bool>("voice-compact-back", () => !s.WideLayout && s.DetailOpen);
        b.BindReadOnly<string>("voice-detail-title", () => source.BuildView().PackCards.FirstOrDefault(x => x.Key == s.SelectedPackKey).Label ?? T("ChoosePack"));
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
            if (s.Page == "Help") return;
            s.PinnedHelpRef = bumper.CurrentHelpRef;
            if (s.PinnedHelpRef == "") s.PinnedHelpRef = source.SectionHelpRef(s.ActiveSectionKey);
            Go("Help");
        });
        b.BindReadOnly<string>("pinned-help-ref", () => s.PinnedHelpRef);
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
        b.BindReadOnly<string>("action-fields-target", () => s.TuningSelectedAction + " · " + (s.TuningLayer == 0 ? "Pawn" : s.TuningRaceDefName + (s.TuningLayer == 2 ? "/" + s.TuningXenotypeDefName : "")));
        b.BindReadOnly<string>("edit-domain-context", () => { source.BuildView(); return T("EditContext") + ": " + (s.TuningLayer == 0 ? "Pawn profile" : s.TuningRaceDefName + (s.TuningLayer == 2 ? "/" + s.TuningXenotypeDefName : "")); });
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
            }
            return editKeys;
        });
        b.BindReadOnly<string>("action-row-diagnostic", () => {
            string[] parts = (production?.RowDiagnostic ?? "").Split(new[] { '\n' }, 2);
            return parts[0] == "" ? "" : t.Translate(parts[0]) + (parts.Length == 2 ? "\n" + parts[1] : "");
        });
        b.BindReadOnly<string>("dependency-status", () => source.BuildIdentity + "\nFerriteLib v0.8.0-rc1 · a0b5b98\n" + T("ObservationBoundary"));
    }
}
