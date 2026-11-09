using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public static partial class UsKernelSettingsHost
{
    // Repeat identities cannot contain ActionId's slash. Business identities stay in typed closures.

    private static bool TuningPage(string page) => page == "Behavior" || page == "Output" || page == "Fallback" || page == "Presets";

    private static void BindRoster(IUsKernelSettingsSource source, UsWriteBindings writes, SessionRevisionBumper bumper, IUiTranslation t)
    {
        var b = writes.Bindings; var s = source.ViewState;
        var native = source as UsKernelSettingsSource;
        string T(string key) => t.Translate("US.B." + key);
        void Bump() => bumper.Bump();
        void Go(string page) { NavigatePage(s, page); Bump(); }
        UsRosterTarget? Target(string key) => source.BuildView().Roster.FirstOrDefault(x => x.Key == key);
        UsRosterTarget? Current() => Target(s.HighlightedTarget);
        PackCardView? Pack(string key)
        {
            foreach (var p in source.BuildView().AllPackCards) if (p.Key == key) return p;
            return null;
        }
        void Choose(UsRosterTarget row)
        {
            s.HighlightedTarget = row.Key; s.DetailOpen = true;
            if (row.Editable)
            {
                source.SelectDomain(row.Domain.Scope, row.Race, row.Xeno);
                source.SetTuningDomain(row.Race, row.Xeno);
            }
            Bump();
        }
        string State(UsRosterTarget row) => string.Join(" · ", new[] {
            row.Loose ? T("Loose") : "", !row.LoadedRace && !row.Loose ? T("UnloadedRace") : "",
            row.Domain.IsDormant ? T("Dormant") : "", row.Domain.IsTargetUnavailable && !row.Loose ? T("Unavailable") : "",
            row.Domain.HasCanonicalConflict ? T("Conflict") : "", !row.Legal && !row.Loose ? T("NoLegalDomain") : "",
            row.Domain.OrphanCount > 0 ? T("Orphan") + " " + row.Domain.OrphanCount : "" }.Where(x => x != ""));
        string Supply(UsRosterTarget row) => row.Domain.Packs.Count == 1 ? row.Domain.Packs[0].Label
            : row.Domain.Packs.Count == 0 ? T("NoPack") : row.SelectedCount + "/" + row.Domain.Packs.Count + " " + T("PacksOn");
        bool Ambiguous(UsRosterTarget row) => row.Domain.HasCanonicalConflict
            || source.BuildView().Roster.Any(other => other.Key != row.Key && other.Label == row.Label);
        string Identity(UsRosterTarget row) => row.Race + (row.Xeno == "" ? "" : " / " + row.Xeno) + " · " + row.SourceName;
        bool Outside(UsRosterTarget row) => !Targets(false).Any(x => x.Key == row.Key);
        bool Kept(UsRosterTarget row) => s.RosterCoverage == "enabled" && row.SelectedCount == 0
            && Targets(false).Any(x => x.Key == row.Key);
        string TargetNotice() => Current() is UsRosterTarget row
            ? string.Join(" · ", new[] { Outside(row) ? T("Outside") : "", Kept(row) ? T("Kept") : "" }.Where(x => x != "")) : "";
        string TargetStatus() => Current() is UsRosterTarget row ? string.Join("\n", new[] {
            TargetNotice(), State(row), Ambiguous(row) ? Identity(row) : "" }.Where(x => x != "")) : "";
        bool technicalOpen = false;
        VoicePacksViewState? supplyView = null; string supplyTarget = ""; UsConfiguredSupply? supply = null;
        UsConfiguredSupply? ConfiguredSupply()
        {
            var view = source.BuildView(); var row = Current();
            if (!ReferenceEquals(view, supplyView) || supplyTarget != (row?.Key ?? ""))
            { supplyView = view; supplyTarget = row?.Key ?? ""; supply = native?.ConfiguredSupply(row); }
            return supply;
        }
        string Band(IReadOnlyList<UsConfiguredSupplyPack>? packs) => packs == null ? T("Unknown")
            : string.Format(T("SupplyCounts"), packs.Count, packs.Count(p => p.Selected), packs.Where(p => p.Selected).Sum(p => p.Mappings.Count));
        string BandDetails(string title, IReadOnlyList<UsConfiguredSupplyPack> packs) => title + ":\n"
            + (packs.Count == 0 ? T("NoDeclarations") : string.Join("\n", packs.Select(p => p.Key + " · "
                + T(p.Selected ? "Selected" : "NotSelected") + " [" + string.Join(", ", p.Mappings) + "]")));
        UsBrowseListState Browse() => s.BrowseDefinitions ? s.RosterBrowse : s.RosterPackBrowse;
        string Coverage() => s.BrowseDefinitions ? s.RosterCoverage : s.RosterPackCoverage;
        int targetMatched = 0, targetEligible = 0, packEligible = 0;
        VoicePacksViewState? targetView = null, packsView = null;
        string targetConditions = "", packConditions = "";
        IReadOnlyList<UsRosterTarget> targetRows = Array.Empty<UsRosterTarget>();
        IReadOnlyList<PackCardView> packRows = Array.Empty<PackCardView>();
        IReadOnlyList<UsRosterTarget> Targets(bool paged)
        {
            var q = s.RosterBrowse; var cov = s.RosterCoverage;
            var view = source.BuildView();
            string identity = q.Query + "\n" + q.Group + "\n" + q.SourcePackageId + "\n" + cov;
            string conditions = identity + "\n" + s.RosterRetentionIdentity + "\n" + string.Join("\n", s.RosterExpanded);
            if (ReferenceEquals(targetView, view) && targetConditions == conditions)
                return paged ? UsBrowseListView.Slice(targetRows, q.Page, 12, out _, out _) : targetRows;
            var all = view.Roster.Where(x => x.InGroup(q.Group)).ToArray(); targetEligible = all.Length;
            if (s.RosterRetentionIdentity != identity)
            {
                s.RosterRetentionIdentity = identity; s.RosterRetained.Clear();
                foreach (var row in all.Where(x => x.SelectedCount > 0)) s.RosterRetained.Add(row.Key);
            }
            var filtered = all.Where(x => x.Matches(q.Query)
                && (q.SourcePackageId == "" || x.SourceId == q.SourcePackageId || x.SourceName.Contains("[" + q.SourcePackageId + "]"))
                && (cov == "any" || cov == "has" && x.Domain.CandidateCount > 0 || cov == "none" && x.Domain.CandidateCount == 0
                    || cov == "enabled" && s.RosterRetained.Contains(x.Key) || cov == "issues" && x.Issue)).ToArray();
            targetMatched = filtered.Length;
            // Race/domain adjacency without an invented parent. Search/conditions derive expansion only.
            bool auto = q.Query != "" || cov != "any" || q.SourcePackageId != "" || q.Group == UsBoardBrowsePolicy.BoardGroup.Xenotypes;
            var visible = filtered.Where(x => x.Xeno == "" || x.Loose || auto || s.RosterExpanded.Contains(x.Race)).ToList();
            var parents = view.Roster.Where(x => x.Race != "" && x.Xeno == "").ToDictionary(x => x.Race, StringComparer.Ordinal);
            visible.Sort((x, y) => {
                parents.TryGetValue(x.Race, out var xr); parents.TryGetValue(y.Race, out var yr);
                int order = (xr?.Tier ?? x.Tier).CompareTo(yr?.Tier ?? y.Tier);
                if (order == 0) order = StringComparer.Ordinal.Compare(xr?.Label ?? x.Label, yr?.Label ?? y.Label);
                if (order == 0) order = StringComparer.Ordinal.Compare(x.Race, y.Race);
                if (order == 0) order = (x.Xeno != "").CompareTo(y.Xeno != "");
                return order == 0 ? StringComparer.Ordinal.Compare(x.Label + x.Key, y.Label + y.Key) : order;
            });
            targetRows = visible; targetView = view;
            targetConditions = identity + "\n" + s.RosterRetentionIdentity + "\n" + string.Join("\n", s.RosterExpanded);
            return paged ? UsBrowseListView.Slice(targetRows, q.Page, 12, out _, out _) : targetRows;
        }
        IReadOnlyList<PackCardView> Packs(bool paged)
        {
            var q = s.RosterPackBrowse; string cov = s.RosterPackCoverage;
            var view = source.BuildView();
            string identity = q.Query + "\n" + q.Group + "\n" + q.SourcePackageId + "\n" + cov;
            if (ReferenceEquals(packsView, view) && packConditions == identity + "\n" + s.RosterPackRetentionIdentity)
                return paged ? UsBrowseListView.Slice(packRows, q.Page, 12, out _, out _) : packRows;
            var all = view.AllPackCards.Where(p => q.Group == UsBoardBrowsePolicy.BoardGroup.All
                || p.Rows.Any(d => view.Roster.Any(r => r.Race == d.RaceDefName && r.Xeno == d.TargetDefName && r.InGroup(q.Group)))).ToArray(); packEligible = all.Length;
            if (s.RosterPackRetentionIdentity != identity)
            {
                s.RosterPackRetentionIdentity = identity; s.RosterPackRetained.Clear();
                foreach (var p in all.Where(p => p.AnyDomainEnabled)) s.RosterPackRetained.Add(p.Key);
            }
            var rows = all.Where(p => UsBrowseListView.Contains(p.Label + "\n" + p.Key + "\n" + p.Author + "\n" + p.ModName + "\n"
                + string.Join("\n", p.Rows.Select(d => d.DisplayName + " " + d.RaceDefName + " " + d.TargetDefName)), q.Query)
                && (q.SourcePackageId == "" || p.Key.StartsWith(q.SourcePackageId + ":", StringComparison.Ordinal))
                && (cov == "any" || cov == "enabled" && s.RosterPackRetained.Contains(p.Key) || cov == "none" && !p.AnyDomainEnabled
                    || cov == "has" || cov == "issues" && p.Rows.Any(d => source.BuildView().Roster.Any(r => r.Race == d.RaceDefName && r.Xeno == d.TargetDefName && r.Issue))))
                .OrderBy(p => p.Label, StringComparer.Ordinal).ThenBy(p => p.Key, StringComparer.Ordinal).ToArray();
            packRows = rows; packsView = view; packConditions = identity + "\n" + s.RosterPackRetentionIdentity;
            return paged ? UsBrowseListView.Slice(rows, q.Page, 12, out _, out _) : rows;
        }
        object? sourcesInput = null;
        string sourcesQuery = "";
        bool sourcesDefinitions = false;
        IReadOnlyList<UsBrowseItem> sourceRows = Array.Empty<UsBrowseItem>();
        IReadOnlyList<UsBrowseItem> Sources(bool paged)
        {
            var q = Browse();
            object input = s.BrowseDefinitions ? RaceDefinitionCatalog.Current : (object)source.BuildView();
            if (ReferenceEquals(sourcesInput, input) && sourcesQuery == q.SourceQuery && sourcesDefinitions == s.BrowseDefinitions)
                return paged ? UsBrowseListView.Slice(sourceRows, q.SourcePage, 5, out _, out _) : sourceRows;
            var all = s.BrowseDefinitions
                ? RaceDefinitionCatalog.Current.Races.Select(x => new UsBrowseItem(x.SourcePackageId, x.SourcePackageName))
                    .Concat(RaceDefinitionCatalog.Current.Xenotypes.Select(x => new UsBrowseItem(x.SourcePackageId, x.SourcePackageName)))
                : source.BuildView().AllPackCards.Select(p => new UsBrowseItem(p.Key.Split(new[] { ':' })[0], p.ModName));
            var rows = all.Where(x => x.Value != "").GroupBy(x => x.Value, StringComparer.Ordinal).Select(g => g.First())
                .Where(x => UsBrowseListView.Contains(x.Value + " " + x.Label, q.SourceQuery)).OrderBy(x => x.Label, StringComparer.Ordinal).ToArray();
            sourcesInput = input; sourcesQuery = q.SourceQuery; sourcesDefinitions = s.BrowseDefinitions; sourceRows = rows;
            return paged ? UsBrowseListView.Slice(rows, q.SourcePage, 5, out _, out _) : rows;
        }
        void Page(int delta) { int count = s.BrowseDefinitions ? Targets(false).Count : Packs(false).Count; Browse().Page = Math.Max(0, Math.Min(Math.Max(0, (count - 1) / 12), Browse().Page + delta)); Bump(); }
        b.BindReadOnly<bool>("b-tuning", () => TuningPage(s.Page));
        b.BindReadOnly<bool>("b-system", () => IsToolPage(s.Page) && !TuningPage(s.Page));
        b.BindReadOnly<bool>("b-voices", () => s.Page == "Voices");
        writes.Command("b-tuning", () => Go(s.LastTuningPage));
        writes.Command("b-system", () => Go(s.LastSystemPage));
        b.BindReadOnly<bool>("b-list", () => s.RosterIsWide || !s.DetailOpen);
        b.BindReadOnly<bool>("b-sheet", () => s.RosterIsWide || s.DetailOpen);
        b.BindReadOnly<bool>("b-control", () => s.RosterIsWide || !s.DetailOpen);
        b.BindReadOnly<float>("b-list-width", () => s.WideLayout ? s.RosterListWidth : 0);
        b.BindReadOnly<bool>("b-target-sheet", () => s.BrowseDefinitions && Current() != null);
        b.BindReadOnly<bool>("b-pack-sheet", () => !s.BrowseDefinitions && Pack(s.SelectedPackKey).HasValue);
        b.BindReadOnly<bool>("b-no-sheet", () => s.BrowseDefinitions ? Current() == null : !Pack(s.SelectedPackKey).HasValue);
        b.BindReadOnly<bool>("b-no-results", () => s.BrowseDefinitions ? Targets(false).Count == 0 : Packs(false).Count == 0);
        b.BindReadOnly<bool>("b-packs-empty", () => !s.BrowseDefinitions && source.BuildView().PackNoPacks);
        b.BindReadOnly<string>("b-summary", () => { source.BuildView(); return s.PackSelected + "/" + s.PackTotal + " " + T("PacksOn") + " · " + s.OrphanSelectionCount + " " + T("Missing") + " · " + t.Translate("US.Kernel." + SqueakerRuntime.PolicyStatus); });
        writes.Value<string>("b-query", () => Browse().Query, value => { Browse().Query = value; Browse().Page = 0; Bump(); });
        writes.Value<string>("b-range", () => Browse().Group.ToString(), value => { if (Enum.TryParse(value, out UsBoardBrowsePolicy.BoardGroup g)) { Browse().Group = g; Browse().Page = 0; Bump(); } });
        b.BindChoices<string>("b-ranges", () => new[] { UsBoardBrowsePolicy.BoardGroup.PlayerRaces, UsBoardBrowsePolicy.BoardGroup.Races,
            UsBoardBrowsePolicy.BoardGroup.Xenotypes, UsBoardBrowsePolicy.BoardGroup.HarHumanlike, UsBoardBrowsePolicy.BoardGroup.Human,
            UsBoardBrowsePolicy.BoardGroup.Creatures, UsBoardBrowsePolicy.BoardGroup.Other, UsBoardBrowsePolicy.BoardGroup.All }
            .Select(x => new UiChoice<string>(t.Translate(UsBoardBrowsePolicy.GroupLabelKey(x)), x.ToString())).ToArray());
        writes.Value<string>("b-coverage", Coverage, value => { if (s.BrowseDefinitions) s.RosterCoverage = value; else s.RosterPackCoverage = value; Browse().Page = 0; Bump(); });
        string CoverageLabel(string key) => T("Coverage." + (key == "none" && !s.BrowseDefinitions ? "idle" : key));
        b.BindChoices<string>("b-coverages", () => (s.BrowseDefinitions ? new[] { "any", "has", "enabled", "none", "issues" } : new[] { "any", "enabled", "none", "issues" }).Select(x => new UiChoice<string>(CoverageLabel(x), x)).ToArray());
        writes.Value<string>("b-source-query", () => Browse().SourceQuery, value => { Browse().SourceQuery = value; Browse().SourcePage = 0; Bump(); });
        writes.Command("b-source-clear", () => { Browse().SourcePackageId = ""; Browse().Page = 0; Bump(); });
        writes.Command("b-source-prev", () => { Browse().SourcePage = Math.Max(0, Browse().SourcePage - 1); Bump(); });
        writes.Command("b-source-next", () => { Browse().SourcePage = Math.Min(Math.Max(0, (Sources(false).Count - 1) / 5), Browse().SourcePage + 1); Bump(); });
        writes.Command("b-clear", () => { Browse().Clear(); if (s.BrowseDefinitions) s.RosterCoverage = "any"; else s.RosterPackCoverage = "any"; Bump(); });
        writes.Command("b-refresh", () => { if (s.BrowseDefinitions) s.RosterRetentionIdentity = ""; else s.RosterPackRetentionIdentity = ""; Bump(); });
        writes.Command("b-prev", () => Page(-1)); writes.Command("b-next", () => Page(1));
        b.BindReadOnly<bool>("b-retained", () => Coverage() == "enabled");
        b.BindReadOnly<bool>("b-has-kept", () => s.BrowseDefinitions ? Targets(false).Any(r => r.SelectedCount == 0)
            && Coverage() == "enabled" : Coverage() == "enabled" && Packs(false).Any(p => !p.AnyDomainEnabled));
        b.BindReadOnly<bool>("b-has-pages", () => (s.BrowseDefinitions ? Targets(false).Count : Packs(false).Count) > 12);
        b.BindReadOnly<bool>("b-source-has-pages", () => Sources(false).Count > 5);
        bool Filtered() => Browse().Query != "" || Browse().Group != UsBoardBrowsePolicy.BoardGroup.PlayerRaces
            || Browse().SourcePackageId != "" || Browse().SourceQuery != "" || Coverage() != "any";
        b.BindReadOnly<bool>("b-filtered", Filtered);
        string FilterSummary() => string.Join(" · ", new[] {
            t.Translate(UsBoardBrowsePolicy.GroupLabelKey(Browse().Group)), CoverageLabel(Coverage()),
            Sources(false).FirstOrDefault(x => x.Value == Browse().SourcePackageId).Label ?? Browse().SourcePackageId }.Where(x => x != ""));
        b.BindReadOnly<string>("b-count", () => {
            int count = s.BrowseDefinitions ? Targets(false).Count : Packs(false).Count;
            int total = s.BrowseDefinitions ? targetEligible : packEligible;
            return (s.BrowseDefinitions ? targetMatched : count) + "/" + total
                + (count > 12 ? " · " + T("Page") + " " + Math.Min(Browse().Page + 1, (count + 11) / 12) + "/" + (count + 11) / 12 : "")
                + (Filtered() ? "\n" + FilterSummary() : "");
        });
        b.BindReadOnly<string>("b-source-count", () => Sources(false).Count + " · " + T("Page") + " " + (Browse().SourcePage + 1) + "/" + Math.Max(1, (Sources(false).Count + 4) / 5));
        var registered = new HashSet<string>(StringComparer.Ordinal);
        void Read<TValue>(string family, string key, string leaf, Func<TValue> get) => b.BindReadOnly(UsWriteBindings.ItemKey(family, key, leaf), get);
        b.BindReadOnly<IReadOnlyList<string>>("b-source-keys", () => Sources(true).Select(item => {
            string key = RosterKey(item.Value);
            if (registered.Add("src:" + key))
            {
                Read("b-source-keys", key, "title", () => item.Label + " · " + item.Value);
                Read("b-source-keys", key, "selected", () => Browse().SourcePackageId == item.Value);
                writes.ItemAction<string>("b-source-keys", key, "choose", _ => { Browse().SourcePackageId = item.Value; Browse().Page = 0; Bump(); });
                Read("b-source-keys", key, "payload", () => item.Value);
            }
            return key;
        }).ToArray());
        b.BindReadOnly<IReadOnlyList<string>>("b-roster-keys", () => {
            var visible = Targets(true); return visible.Select(row => {
                string id = row.Key, key = RosterKey(id); UsRosterTarget? Row() => Target(id);
                if (registered.Add("row:" + key))
                {
                    Read("b-roster-keys", key, "title", () => Row()?.Label ?? id);
                    Read("b-roster-keys", key, "meta", () => Row() is UsRosterTarget r ? string.Join(" · ", new[] {
                        Supply(r), State(r), Ambiguous(r) ? Identity(r) : "", Kept(r) ? T("Kept") : "" }.Where(x => x != "")) : "");
                    Read("b-roster-keys", key, "selected", () => s.HighlightedTarget == id);
                    Read("b-roster-keys", key, "payload", () => id);
                    Read("b-roster-keys", key, "has-switch", () => Row() is UsRosterTarget r && r.Editable && r.Domain.Packs.Count == 1);
                    Read("b-roster-keys", key, "no-switch", () => Row() is not UsRosterTarget r || !r.Editable || r.Domain.Packs.Count != 1);
                    Read("b-roster-keys", key, "badge", () => Row() is UsRosterTarget r && r.Domain.Packs.Count > 1 ? r.SelectedCount + "/" + r.Domain.Packs.Count : "—");
                    Read("b-roster-keys", key, "has-children", () => Row() is UsRosterTarget r && r.Xeno == "" && source.BuildView().Roster.Any(x => x.Race == r.Race && x.Xeno != ""));
                    Read("b-roster-keys", key, "tier", () => Row() is UsRosterTarget r ? T("Tier." + r.Tier) : "");
                    Read("b-roster-keys", key, "tier-first", () => Row() is UsRosterTarget r && Targets(true).FirstOrDefault(x => x.Tier == r.Tier)?.Key == r.Key);
                    writes.ItemAction<string>("b-roster-keys", key, "choose", _ => { var r = Row(); if (r != null) Choose(r); });
                    writes.ItemAction<string>("b-roster-keys", key, "fold", _ => { var r = Row(); if (r != null && !s.RosterExpanded.Remove(r.Race)) s.RosterExpanded.Add(r.Race); Bump(); });
                    writes.ItemValue<bool>("b-roster-keys", key, "enabled", () => Row()?.Domain.Packs.FirstOrDefault().IsSelected ?? false,
                        value => { var r = Row(); if (r != null && r.Editable && r.Domain.Packs.Count == 1) { source.ToggleVoicePack(r.Domain.Scope, r.Race, r.Xeno, r.Domain.Packs[0].Key, value); Bump(); } });
                }
                return key;
            }).ToArray();
        });
        b.BindReadOnly<IReadOnlyList<string>>("b-pack-keys", () => Packs(true).Select(pack => {
            string id = pack.Key, key = RosterKey(id);
            if (registered.Add("pack:" + key))
            {
                Read("b-pack-keys", key, "title", () => Pack(id)?.Label ?? id);
                Read("b-pack-keys", key, "meta", () => Pack(id) is PackCardView p ? p.ModName + " · " + p.Author + " · " + p.Rows.Count + " " + T("Targets")
                    + (s.RosterPackCoverage == "enabled" && !p.AnyDomainEnabled ? " · " + T("Kept") : "") : "");
                Read("b-pack-keys", key, "selected", () => s.SelectedPackKey == id);
                Read("b-pack-keys", key, "payload", () => id);
                UsRosterTarget? SingleTarget() => Pack(id) is PackCardView p && p.Rows.Count == 1
                    ? source.BuildView().Roster.FirstOrDefault(r => r.Race == p.Rows[0].RaceDefName && r.Xeno == p.Rows[0].TargetDefName) : null;
                Read("b-pack-keys", key, "has-single", () => SingleTarget()?.Editable == true);
                Read("b-pack-keys", key, "single-title", () => SingleTarget()?.Label ?? "");
                writes.ItemValue<bool>("b-pack-keys", key, "enabled", () => SingleTarget()?.Domain.Packs.Any(p => p.Key == id && p.IsSelected) == true,
                    value => { var r = SingleTarget(); if (r?.Editable == true) { source.ToggleVoicePack(r.Domain.Scope, r.Race, r.Xeno, id, value); Bump(); } });
                writes.ItemAction<string>("b-pack-keys", key, "view", _ => { if (s.SelectedPackKey != id) { s.PackSheetPage = 0; s.PackSheetQuery = ""; } s.SelectedPackKey = id; s.DetailOpen = true; Bump(); });
            }
            return key;
        }).ToArray());
        b.BindReadOnly<string>("b-target-title", () => Current()?.Label ?? "");
        b.BindReadOnly<string>("b-target-layer", () => Current() is UsRosterTarget r ? r.Loose ? T("ReadOnlyTarget")
            : r.Xeno == "" ? T("RaceDomain") : T("XenoDomain") + " · " + (r.RaceIdentity?.Label ?? r.Race) + " / " + r.Label : "");
        b.BindReadOnly<string>("b-target-status", TargetStatus);
        b.BindReadOnly<bool>("b-target-has-status", () => TargetStatus() != "");
        b.BindReadOnly<bool>("b-target-no-packs", () => Current()?.Domain.Packs.Count == 0);
        b.BindReadOnly<bool>("b-target-orphans", () => Current()?.Domain.OrphanCount > 0);
        writes.Command("b-forget", () => { var r = Current(); if (r != null && !r.Loose) { source.ForgetUnavailable(r.Domain.Scope, r.Race, r.Xeno); Bump(); } });
        b.BindReadOnly<bool>("b-can-behavior", () => Current()?.Editable == true);
        b.BindReadOnly<bool>("b-can-table", () => Current() is UsRosterTarget r && r.LoadedRace);
        writes.Command("b-behavior", () => { var r = Current(); if (r == null || !r.Editable) return; Choose(r); source.SetTuningLayer(r.Xeno == "" ? 1 : 2); Go("Behavior"); });
        writes.Command("b-table", () => { var r = Current(); if (r == null || !r.LoadedRace) return;
            bool exists = source.BuildView().FallbackRaces.Any(x => x.DefName == r.Race);
            source.SetFallbackSelection(exists ? r.Race : "", ""); s.PendingFallbackRace = exists ? "" : r.Race;
            s.FallbackCreating = !exists; s.FallbackDetailOpen = true; Go("Fallback"); });
        b.BindReadOnly<string>("b-pack-title", () => Pack(s.SelectedPackKey)?.Label ?? s.SelectedPackKey);
        b.BindReadOnly<string>("b-pack-meta", () => Pack(s.SelectedPackKey) is PackCardView p ? p.ModName + " · " + p.Author
            + (PackNotice() == "" ? "" : "\n" + PackNotice()) + (technicalOpen ? "\n" + p.Key + "\n" + p.Coverage : "") : T("Unavailable"));
        string PackNotice() => Pack(s.SelectedPackKey) is PackCardView p ? string.Join(" · ", new[] {
            !Packs(false).Any(x => x.Key == p.Key) ? T("Outside") : "",
            s.RosterPackCoverage == "enabled" && !p.AnyDomainEnabled && Packs(false).Any(x => x.Key == p.Key) ? T("Kept") : "" }.Where(x => x != "")) : "";
        b.BindReadOnly<bool>("b-details-open", () => technicalOpen);
        b.BindReadOnly<bool>("b-detail-actions", () => s.BrowseDefinitions ? Current() != null : Pack(s.SelectedPackKey).HasValue);
        b.BindReadOnly<bool>("b-target-details", () => technicalOpen && s.BrowseDefinitions && Current() != null);
        writes.Command("b-details-toggle", () => { technicalOpen = !technicalOpen; Bump(); });
        b.BindReadOnly<bool>("b-supply-exact", () => Current() is UsRosterTarget r && !r.Loose && r.Xeno != "");
        b.BindReadOnly<bool>("b-has-supply", () => Current() is UsRosterTarget r && !r.Loose);
        b.BindReadOnly<string>("b-supply-exact-counts", () => T("Exact") + "\n" + Band(ConfiguredSupply()?.ExactRegular));
        b.BindReadOnly<string>("b-supply-race-counts", () => T("RaceRegular") + "\n" + Band(ConfiguredSupply()?.RaceRegular));
        b.BindReadOnly<string>("b-supply-fallback-counts", () => T("PackFallback") + "\n" + Band(ConfiguredSupply()?.ExactFallback));
        b.BindReadOnly<string>("b-supply-table-counts", () => T("FinalTable") + " · " + (ConfiguredSupply() is UsConfiguredSupply c
            ? c.HasTable ? c.TableMappings + " " + T("Mapped") : T("NoTable") : T("Unknown")));
        b.BindReadOnly<string>("b-supply-route", () => ConfiguredSupply() is UsConfiguredSupply c
            ? T("ConfiguredSupply") + " · " + t.Translate("US.Tuning.Mode." + c.Mode) + "\n" + T("SupplyRoute." + c.Mode) : T("Unknown"));
        b.BindReadOnly<string>("b-path", () => (Current() is UsRosterTarget r ? Identity(r) + " · " + r.SourceId
            + (r.RaceIdentity?.IsHar == true ? " · HAR" : "")
            + (r.RaceIdentity?.IsHuman == true && r.Xeno == "" ? " · " + T("NativeHuman") : "") + "\n" : "") + (ConfiguredSupply() is UsConfiguredSupply c
            ? (Current()?.Xeno != "" ? BandDetails(T("Exact"), c.ExactRegular) + "\n" : "")
                + BandDetails(T("RaceRegular"), c.RaceRegular) + "\n" + BandDetails(T("PackFallback"), c.ExactFallback)
                + (Current()?.Xeno != "" ? "\n" + T("RaceRegularOnly") : "")
            : native?.ConfiguredPath(Current(), t) ?? T("Unknown")) + "\n" + T("AdmissionNote"));
        b.BindReadOnly<IReadOnlyList<string>>("b-target-pack-keys", () => {
            var r = Current(); if (r == null) return Array.Empty<string>();
            return r.Domain.Packs.Select(pack => {
                string target = r.Key, pk = pack.Key, key = RosterKey(target + "\n" + pk);
                UsRosterTarget? Row() => Target(target);
                VoicePackRowView Info() => Row()?.Domain.Packs.FirstOrDefault(p => p.Key == pk) ?? default;
                if (registered.Add("tp:" + key))
                {
                    Read("b-target-pack-keys", key, "title", () => Info().Label);
                    Read("b-target-pack-keys", key, "meta", () => Info().ModName + " · " + Info().Author + "\n" + Info().Coverage
                        + (Row() is UsRosterTarget row && !row.Editable ? "\n" + State(row) : ""));
                    Read("b-target-pack-keys", key, "editable", () => Row()?.Editable == true);
                    Read("b-target-pack-keys", key, "readonly", () => Row()?.Editable != true);
                    Read("b-target-pack-keys", key, "payload", () => pk);
                    writes.ItemValue<bool>("b-target-pack-keys", key, "enabled", () => Info().IsSelected, value => { var row = Row(); if (row?.Editable == true) { source.ToggleVoicePack(row.Domain.Scope, row.Race, row.Xeno, pk, value); Bump(); } });
                    writes.ItemAction<string>("b-target-pack-keys", key, "view", _ => { s.SelectedPackKey = pk; s.BrowseDefinitions = false; s.DetailOpen = true; s.PackSheetPage = 0; Go("Voices"); });
                }
                return key;
            }).ToArray();
        });
        writes.Value<string>("b-pack-sheet-query", () => s.PackSheetQuery, value => { s.PackSheetQuery = value; s.PackSheetPage = 0; Bump(); });
        IReadOnlyList<PackCardDomainRowView> PackTargets(bool paged)
        {
            var rows = (Pack(s.SelectedPackKey)?.Rows ?? Array.Empty<PackCardDomainRowView>()).Where(r => UsBrowseListView.Contains(r.DisplayName + " " + r.RaceDefName + " " + r.TargetDefName, s.PackSheetQuery)).ToArray();
            return paged ? UsBrowseListView.Slice(rows, s.PackSheetPage, 10, out _, out _) : rows;
        }
        writes.Command("b-sheet-prev", () => { s.PackSheetPage = Math.Max(0, s.PackSheetPage - 1); Bump(); });
        writes.Command("b-sheet-next", () => { s.PackSheetPage = Math.Min(Math.Max(0, (PackTargets(false).Count - 1) / 10), s.PackSheetPage + 1); Bump(); });
        b.BindReadOnly<string>("b-sheet-count", () => PackTargets(false).Count + " · " + T("Page") + " " + (s.PackSheetPage + 1) + "/" + Math.Max(1, (PackTargets(false).Count + 9) / 10));
        b.BindReadOnly<bool>("b-sheet-has-pages", () => PackTargets(false).Count > 10);
        b.BindReadOnly<IReadOnlyList<string>>("b-pack-target-keys", () => PackTargets(true).Select(d => {
            string pk = s.SelectedPackKey, target = d.RaceDefName + (d.TargetDefName == "" ? "" : "|" + d.TargetDefName), key = RosterKey(pk + "\n" + target);
            UsRosterTarget? Row() => Target(target);
            if (registered.Add("pt:" + key))
            {
                Read("b-pack-target-keys", key, "title", () => Row()?.Label ?? target);
                Read("b-pack-target-keys", key, "status", () => Row() is UsRosterTarget r ? State(r) : T("Unavailable"));
                Read("b-pack-target-keys", key, "has-status", () => Row() is not UsRosterTarget r || State(r) != "");
                Read("b-pack-target-keys", key, "editable", () => Row()?.Editable == true);
                Read("b-pack-target-keys", key, "readonly", () => Row()?.Editable != true);
                Read("b-pack-target-keys", key, "payload", () => target);
                writes.ItemValue<bool>("b-pack-target-keys", key, "enabled", () => Row()?.Domain.Packs.Any(p => p.Key == pk && p.IsSelected) == true,
                    value => { var row = Row(); if (row?.Editable == true) { source.ToggleVoicePack(row.Domain.Scope, row.Race, row.Xeno, pk, value); Bump(); } });
                writes.ItemAction<string>("b-pack-target-keys", key, "choose", _ => { var row = Row(); if (row?.Editable == true) { source.SelectDomain(row.Domain.Scope, row.Race, row.Xeno); source.SetTuningDomain(row.Race, row.Xeno); Bump(); } });
            }
            return key;
        }).ToArray());
        b.BindReadOnly<bool>("b-publication-pending", () => native?.PublicationPending == true);
        bool diagnosticDetailsOpen = false;
        b.BindReadOnly<bool>("b-diagnostic-details-open", () => diagnosticDetailsOpen);
        writes.Command("b-diagnostic-details-toggle", () => { diagnosticDetailsOpen = !diagnosticDetailsOpen; Bump(); });
        b.BindReadOnly<bool>("b-transaction-visible", () => diagnosticDetailsOpen && b.TryGet("transaction-diagnostics", out string detail)
            && !string.IsNullOrWhiteSpace(detail));
        bool developerToolsOpen = false;
        b.BindReadOnly<bool>("b-dev-open", () => developerToolsOpen);
        writes.Command("b-dev-toggle", () => { developerToolsOpen = !developerToolsOpen; Bump(); });
        b.BindReadOnly<string>("b-preset-result", () => s.PresetResult);
        // Missing selections stay addressable even when their domain isn't in the current result page.
        b.BindReadOnly<IReadOnlyList<string>>("b-missing-keys", () => source.BuildView().Roster.Where(r => r.Domain.OrphanCount > 0).Select(r => {
            string target = r.Key, key = RosterKey(target);
            if (registered.Add("missing:" + key))
            {
                Read("b-missing-keys", key, "title", () => Target(target) is UsRosterTarget row ? row.Label + " · " + string.Join(", ", row.Domain.EnabledKeys.Where(k => !row.Domain.Packs.Any(p => p.Key == k))) : target);
                Read("b-missing-keys", key, "payload", () => target);
                writes.ItemAction<string>("b-missing-keys", key, "forget", _ => { var row = Target(target); if (row != null) { source.ForgetUnavailable(row.Domain.Scope, row.Race, row.Xeno); Bump(); } });
            }
            return key;
        }).ToArray());
    }
}
