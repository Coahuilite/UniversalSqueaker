using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.Authoring;

namespace UniversalSqueaker.UI;

public static partial class UsKernelSettingsHost
{
#if !US_PREVIEW_NEUTRAL
    private static string TuningContextLabel(IUsKernelSettingsSource source, IUiTranslation t, int layer, string race, string child)
        => layer == 0 ? t.Translate("US.UI.PawnProfile") : TuningContextLabel(source.BuildView().Roster, t, layer, race, child);

#endif
    private static string TuningContextLabel(IReadOnlyList<UsRosterTarget> roster, IUiTranslation t, int layer, string race, string child)
    {
        if (layer == 0) return t.Translate("US.UI.PawnProfile");
        string identity = race + (child == "" ? "" : " / " + child);
        var target = roster.FirstOrDefault(r => r.Race == race && r.Xeno == child);
        string label = target?.Label ?? identity;
        return t.Translate(layer == 1 ? "US.Tuning.Layer.Race" : "US.Tuning.Layer.Xenotype") + ": "
            + label + (label == identity ? "" : " [" + identity + "]");
    }

    // Window-local presentation state. Business values remain owned by the source/settings funnel.
    private sealed class TuningActionRow
    {
        internal readonly string Key, Action, Race, Child;
        internal readonly int Layer;
        internal readonly List<string> Fields = new();
        internal string TimingKind;
        internal bool Details;
        internal TuningActionRow(string key, string action, int layer, string race, string child)
        {
            Key = key; Action = action; Layer = layer; Race = race; Child = child;
            TimingKind = UsResponsePolicy.IsOfficialInteraction(action) ? "Interaction" : "Simulation";
        }
        internal string FieldKey(string field) => Key + "-F-" + RosterKey(field);
    }

#if !US_PREVIEW_NEUTRAL
    private static void BindBehaviorSheet(IUsKernelSettingsSource source, UsWriteBindings writes, SessionRevisionBumper bumper, IUiTranslation t)
        => BindBehaviorSheet(new UsSettingsBehaviorPageSource(source), writes, bumper, t);

#endif
    private static void BindBehaviorSheet(IUsBehaviorPageSource source, UsWriteBindings writes, SessionRevisionBumper bumper, IUiTranslation t)
    {
        var b = writes.Bindings;
        var s = source.ViewState;
        var native = source.Fields;
        string T(string key) => t.Translate("US.B." + key);
        bool contextControlsOpen = false;
        long sheetRevision = 0;
        UsBehaviorDisplayCapture? capture = null;
#if !US_PREVIEW_NEUTRAL
        if (writes.RetainBehaviorDisplay && source is UsSettingsBehaviorPageSource productionSheet
            && productionSheet.Production is UsKernelSettingsSource production)
        {
            capture = new UsBehaviorDisplayCapture(() => new UsBehaviorDisplayCapture.Stamp(
                production.ObserveBehaviorDisplayInputs(), production.BehaviorRowResultRevision, sheetRevision,
                t.TranslationRevision, s.TuningSelectedAction, s.SourceChangeDiagnostic,
                System.Globalization.CultureInfo.CurrentCulture.Name),
                () => b.Get<IReadOnlyList<string>>("b-action-keys"), () => s.Page == "Behavior");
            writes.BehaviorDisplay = capture;
        }
#endif
        b.BindReadOnly<bool>("b-context-open", () => contextControlsOpen);
        writes.Command("b-context-toggle", () => { contextControlsOpen = !contextControlsOpen; sheetRevision++; bumper.Bump(); });
        string Race() => s.TuningLayer == 0 ? "" : s.TuningRaceDefName;
        string Child() => s.TuningLayer == 2 ? s.TuningXenotypeDefName : "";
        bool Valid() => s.TuningLayer == 0 || source.ReadView().Roster.Any(r => r.Race == s.TuningRaceDefName
            && (s.TuningLayer == 1 ? r.Xeno == "" && r.LoadedRace : r.Xeno == s.TuningXenotypeDefName && r.Editable));
        for (int i = 0; i < 3; i++)
        {
            int layer = i;
            b.BindReadOnly<bool>("b-layer-is-" + i, () => s.TuningLayer == layer);
            b.BindReadOnly<string>("b-layer-title-" + i, () => t.Translate(layer == 0 ? "US.UI.PawnProfile" : layer == 1 ? "US.Tuning.Layer.Race" : "US.Tuning.Layer.Xenotype"));
            writes.Command("b-layer-" + i, () => {
                if (layer > 0)
                {
                    var row = source.ReadView().Roster.FirstOrDefault(r => r.Race == s.SelectedRaceDefName && r.Xeno == (layer == 2 ? s.SelectedTargetName : ""));
                    if (row == null || (layer == 2 ? !row.Editable || row.Xeno == "" : !row.LoadedRace)) return;
                    source.SetTuningDomain(row.Race, layer == 2 ? row.Xeno : "");
                }
                source.SetTuningLayer(layer); bumper.Bump();
            }, () => layer == 0 || source.ReadView().Roster.Any(r => r.Race == s.SelectedRaceDefName && (layer == 1 ? r.Xeno == "" && r.LoadedRace : r.Xeno == s.SelectedTargetName && r.Xeno != "" && r.Editable)));
        }
        b.BindReadOnly<bool>("b-action-invalid", () => !Valid());
        writes.Command("b-area-reset", () => {
            string token = s.TuningLayer + "\n" + Race() + "\n" + Child();
            source.ConfirmAreaReset(t.Translate("US.Reset.Area"), t.Translate("US.Reset.Area.Action.Confirm") + "\n" + (s.TuningLayer == 0 ? t.Translate("US.UI.PawnProfile") : Race() + (Child() == "" ? "" : " / " + Child())),
                t.Translate("US.Reset.Confirm"), () => {
                    if (Valid() && token == s.TuningLayer + "\n" + Race() + "\n" + Child()) b.TryInvokeCommand("reset-action-area");
                });
        }, () => Valid() && source.CanConfirmAreaReset);

        var retainedRows = new List<TuningActionRow>();
        var knownRows = new Dictionary<string, TuningActionRow>(StringComparer.Ordinal);
        string ContractField(string field) => field switch {
            "interval" => "simulationIntervalTicks", "interaction-seconds" => "interactionIntervalSeconds",
            "talking" => "talkingEnabled", "age" => "ageEnabled", "mood" => "moodEnabled", _ => field };
        void EndActionEdit(int layer, string race, string child, string? action, IReadOnlyCollection<string>? fields = null)
            => source.EndAffectedEdit(id => retainedRows.Any(row => row.Layer == layer && row.Race == race && row.Child == child
                && (action == null || row.Action == action) && row.Fields.Any(field => (fields == null || fields.Contains(ContractField(field)))
                    && id.EndsWith("#" + row.FieldKey(field), StringComparison.Ordinal))));

        // Keep the existing command entries and eligibility. Capture the effective domain before
        // invoking the restore; navigation memory and unrelated retained editors remain untouched.
        writes.Command("reset-action-row", () => {
            int layer = s.TuningLayer;
            string action = s.TuningSelectedAction;
            VoicePacksPageModel.GetTuningWriteDomain(s, out string race, out string child);
            if (source.ResetActionTuningRow(action) != SqueakResetOutcome.Applied) return;
            EndActionEdit(layer, race, child, action); bumper.Bump();
        }, () => s.TuningSelectedAction.Length > 0 && source.CanResetTuningArea());
        writes.Command("reset-action-area", () => {
            int layer = s.TuningLayer;
            VoicePacksPageModel.GetTuningWriteDomain(s, out string race, out string child);
            if (source.ResetActionTuningArea() != SqueakResetOutcome.Applied) return;
            EndActionEdit(layer, race, child, null); bumper.Bump();
        }, source.CanResetTuningArea);

        var order = ActionScopeRules.PlayerGroupOrder.Concat(ActionScopeRules.SystemGroupOrder).ToArray();
        int ActionOrder(string action) { int index = Array.IndexOf(order, action); return index < 0 ? int.MaxValue : index; }
        int Group(string action) => !Kernel.BuiltInActionKeys.Contains(action) ? 2
            : ActionScopeRules.GroupFor(action) == ActionScopeGroup.PlayerTriggered ? 0 : 1;
        UsBehaviorPageView? actionProjectionView = null;
        var actionProjection = new Dictionary<string, ActionScopeRowView>(StringComparer.Ordinal);
        var groupFirst = new Dictionary<int, string>();
        ActionScopeRowView[] orderedActions = Array.Empty<ActionScopeRowView>();
        void RefreshActionProjection()
        {
            var view = source.ReadView();
            if (ReferenceEquals(actionProjectionView, view)) return;
            orderedActions = view.ActionScopes.OrderBy(r => ActionOrder(r.ActionKey)).ThenBy(r => r.ActionKey, StringComparer.Ordinal).ToArray();
            actionProjection.Clear(); groupFirst.Clear();
            foreach (var action in orderedActions)
            {
                actionProjection[action.ActionKey] = action;
                if (!groupFirst.ContainsKey(Group(action.ActionKey))) groupFirst.Add(Group(action.ActionKey), action.ActionKey);
            }
            actionProjectionView = view;
        }
        bool SameDomain(TuningActionRow row) => s.TuningLayer == row.Layer && Race() == row.Race && Child() == row.Child;
        ActionScopeRowView? ActionRow(TuningActionRow row)
        {
            if (!SameDomain(row)) return null;
            RefreshActionProjection();
            return actionProjection.TryGetValue(row.Action, out var current) ? current : (ActionScopeRowView?)null;
        }
        bool Current(TuningActionRow row) => SameDomain(row) && ActionRow(row).HasValue;
        bool Expanded(TuningActionRow row) => Current(row) && s.TuningSelectedAction == row.Action && Valid() && SameDomain(row);
        string Context(TuningActionRow row) => row.Layer == 0 ? t.Translate("US.UI.PawnProfile")
            : TuningContextLabel(source.ReadView().Roster, t, row.Layer, row.Race, row.Child);
        bool Contribution(string field) => field.Contains(":");
        string FieldTitle(string field) => Contribution(field) ? UsTuningFieldPresentation.For(field).Title(field, t) : t.Translate("US.UI.Field." + field);
        string FieldGroup(string field) => Contribution(field) ? UsTuningFieldPresentation.For(field).GroupKey
            : field == "volume" || field == "pitch" ? "US.B.FieldGroup.Output"
            : field == "talking" || field == "age" || field == "mood" ? "US.B.FieldGroup.Responses" : "US.B.FieldGroup.Opportunity";

        // One flat Repeat: header, advanced fields, contributions, then restores for each action.
        // Hidden entries stay in this ordered set. No nested Repeat or global-binding escape is used;
        // collapsing/reopening and exact-domain switches retain the same field node and draft.
        UsBehaviorPageView? keysView = null;
        string keysContext = "", keysDomain = "";
        IReadOnlyList<string> retainedKeys = Array.Empty<string>();
        var sortedRows = new List<TuningActionRow>();
        b.BindReadOnly<IReadOnlyList<string>>("b-action-keys", () => {
            var view = source.ReadView();
            int layer = s.TuningLayer;
            string race = Race(), child = Child();
            string domain = layer + "\n" + race + "\n" + child;
            string context = domain + "\n" + s.TuningSelectedAction;
            if (ReferenceEquals(keysView, view) && keysContext == context) return retainedKeys;
            RefreshActionProjection();
            bool added = false;
            if (!ReferenceEquals(keysView, view) || keysDomain != domain)
            foreach (var action in orderedActions)
            {
                string key = RosterKey(action.ActionKey + "\n" + layer + "\n" + race + "\n" + child);
                if (knownRows.ContainsKey(key)) continue;
                var row = new TuningActionRow(key, action.ActionKey, layer, race, child);
                knownRows.Add(key, row);
                retainedRows.Add(row);
                added = true;
                BindItem(row, key + "-H", "", false);
                BindItem(row, key + "-Z", "", true);
            }
            if (added) { sortedRows = retainedRows.OrderBy(r => ActionOrder(r.Action)).ThenBy(r => r.Action, StringComparer.Ordinal).ToList(); }
            string expandedKey = RosterKey(s.TuningSelectedAction + "\n" + layer + "\n" + race + "\n" + child);
            if (native != null && knownRows.TryGetValue(expandedKey, out var expanded) && Expanded(expanded))
            {
                var fields = new[] { "enabled", "interaction-seconds", "interval", "volume", "pitch", "talking", "age", "mood" }
                    .Concat(native.ContributionFields(expanded.Action, expanded.Race, expanded.Child)
                        .OrderBy(UsTuningFieldPresentation.Order).ThenBy(f => f, StringComparer.Ordinal));
                foreach (string field in fields)
                {
                    if (expanded.Fields.Contains(field)) continue;
                    expanded.Fields.Add(field); added = true;
                    BindItem(expanded, expanded.FieldKey(field), field, false);
                }
            }
            // Visibility changes still invalidate layout, but a collapse/reopen does not change
            // the retained identity set. Only new rows/fields allocate a replacement key vector.
            if (added)
            {
                var keys = new List<string>();
                foreach (var row in sortedRows)
                {
                    keys.Add(row.Key + "-H"); keys.AddRange(row.Fields.Select(row.FieldKey)); keys.Add(row.Key + "-Z");
                }
                retainedKeys = keys;
            }
            keysView = view; keysContext = context; keysDomain = domain;
            return retainedKeys;
        });

        void BindItem(TuningActionRow row, string key, string field, bool footer)
        {
            bool header = field == "" && !footer;
            string action = row.Action, race = row.Race, child = row.Child;
            string Address(string leaf) => UsWriteBindings.ItemKey("b-action-keys", key, leaf);
            var display = capture?.Add(key, footer, () => header ? Current(row) : Expanded(row));
            // Actual manifest demand domain: do not evaluate an old child just to compare it
            // after its parent/field/timing/details branch became unreachable.
            bool DisplayDemand(string leaf)
            {
                if (leaf == "visible" || leaf == "header" || leaf == "footer" || leaf == "field-visible"
                    || leaf == "source-visible" || leaf == "details") return true;
                if (leaf.StartsWith("field-", StringComparison.Ordinal) || leaf == "has-field-note" || leaf == "technical-key")
                    return FieldVisible() && (leaf != "field-provenance" && leaf != "technical-key" || row.Details);
                if (leaf == "row-result" || leaf == "has-row-result" || leaf == "preset-anchor" || leaf == "explanation")
                    return footer && Expanded(row) && (leaf != "explanation" || row.Details);
                if (leaf == "context" || leaf.StartsWith("source-", StringComparison.Ordinal)
                    || leaf.StartsWith("timing-kind", StringComparison.Ordinal))
                    return header && Expanded(row) && (leaf != "source-technical" || row.Details);
                return header;
            }
            void Read<T>(string leaf, Func<T> read)
            {
                b.BindReadOnly<T>(Address(leaf), read);
                display?.Value(leaf, read, () => DisplayDemand(leaf));
            }
            void Options<T>(string leaf, Func<IReadOnlyList<UiChoice<T>>> read)
            {
                b.BindChoices<T>(Address(leaf), read);
                display?.Choices(leaf, read, () => DisplayDemand(leaf));
            }
            bool Editable() => header && Current(row) && Valid() && SameDomain(row);
            bool FieldVisible() => field != "" && Expanded(row) &&
                (field != "interval" || row.TimingKind == "Simulation") && (field != "interaction-seconds" || row.TimingKind == "Interaction");
            IReadOnlyList<UiChoice<BehaviorScope?>> Choices()
            {
                if (!Editable()) return Array.Empty<UiChoice<BehaviorScope?>>();
                var options = new List<UiChoice<BehaviorScope?>> {
                    new(t.Translate("US.Tuning.Auto"), null), new(t.Translate("US.Tuning.Scope.Off"), BehaviorScope.Disabled) };
                var supported = Kernel.BuiltInActionKeys.Contains(action) ? BuiltInPawnContracts.SupportedScopes(action)
                    : ActionScopeSupport.AnyOccurrence | ActionScopeSupport.ActiveCommand;
                foreach (var scope in new[] { BehaviorScope.AnyOccurrence, BehaviorScope.ActiveCommand })
                    if ((supported & (scope == BehaviorScope.AnyOccurrence ? ActionScopeSupport.AnyOccurrence : ActionScopeSupport.ActiveCommand)) != 0)
                        options.Add(new UiChoice<BehaviorScope?>(t.Translate(ActionScopeRules.ScopeLabelKey(action, scope)), scope));
                return options;
            }
            Read<bool>("visible", () => header ? Current(row) : Expanded(row));
            Read<bool>("header", () => header);
            Read<bool>("footer", () => footer);
            Read<bool>("group-first", () => header && Current(row)
                && groupFirst.TryGetValue(Group(action), out string? first) && first == action);
            Read<string>("group", () => Group(action) == 2 ? T("Custom") : t.Translate(Group(action) == 0
                ? "US.Tuning.Group.PlayerTriggered" : "US.Tuning.Group.SystemOrEvent"));
            Read<string>("title", () => ActionRow(row)?.DisplayName ?? action);
            Read<bool>("expanded", () => Expanded(row));
            Read<bool>("collapsed", () => !Expanded(row));
            Read<bool>("editable", Editable);
            Read<bool>("not-editable", () => !Editable());
            Read<string>("scope-display", () => ActionRow(row) is ActionScopeRowView r && r.HasOwnScope
                ? t.Translate(ActionScopeRules.ScopeLabelKey(action, r.Scope)) : t.Translate("US.Tuning.Auto"));
            Read<string>("context", () => T("CurrentAction") + ": " + (ActionRow(row)?.DisplayName ?? action) + " · " + Context(row));
            Read<string>("payload", () => action);
            Options<BehaviorScope?>("scope-options", Choices);
            display?.Value<BehaviorScope?>("scope", () => ActionRow(row) is ActionScopeRowView r && r.HasOwnScope ? r.Scope : null, () => header);
            writes.ItemValue<BehaviorScope?>("b-action-keys", key, "scope", () => ActionRow(row) is ActionScopeRowView r && r.HasOwnScope ? r.Scope : null,
                scope => {
                    // SetActionScope reads page state: refuse a stale popup before reaching that owner.
                    if (!Editable() || !Choices().Any(choice => choice.Value == scope)) return;
                    source.SetActionScope(action, scope); bumper.Bump();
                });
            Read<string>("scope-effective", () => ActionRow(row) is ActionScopeRowView r && !(r.HasOwnScope && r.Scope == r.EffectiveScope)
                ? "→ " + t.Translate(ActionScopeRules.ScopeLabelKey(action, r.EffectiveScope)) : "");
            Read<bool>("has-scope-hint", () => ActionRow(row) is ActionScopeRowView r && !(r.HasOwnScope && r.Scope == r.EffectiveScope));
            writes.ItemCommand("b-action-keys", key, "advanced", () => {
                if (!Editable()) return;
                string selected = s.TuningSelectedAction == action ? "" : action;
#if !US_PREVIEW_NEUTRAL
                if (source is UsSettingsBehaviorPageSource settingsSource)
                    settingsSource.SetActionExpansion(selected, bumper.Bump);
                else
#endif
                { source.SetTuningSelectedAction(selected); bumper.Bump(); }
            }, Editable);
            void Help(string topic)
            {
                string context = (ActionRow(row)?.DisplayName ?? action) + " · " + Context(row)
                    + (field == "" ? "" : "\n" + FieldTitle(field));
                bool changed = DisplayHelpRef(s) != topic || DisplayHelpContext(s) != context;
                s.PinnedHelpRef = s.PinnedHelpContext = ""; s.FollowHelpRef = topic; s.FollowHelpContext = context;
                s.FollowHelpLabel = field == "" ? "" : FieldTitle(field);
                s.HelpScope = HelpScope(s); s.HelpPanelOpen = true;
                if (changed) bumper.ResetHelpScroll();
                bumper.Bump();
            }
            Read<bool>("source-visible", () => header && Expanded(row) && native != null);
            IReadOnlyList<UiChoice<string>> SourceChoices() => header && Expanded(row)
                ? native?.SourceChoices(t) ?? Array.Empty<UiChoice<string>>() : Array.Empty<UiChoice<string>>();
            // Keep the source explanation, but do not reserve an empty dropdown row for Pawn profile.
            Read<bool>("source-controls-visible", () => SourceChoices().Count != 0);
            Options<string>("source-options", SourceChoices);
            display?.Value<string>("source", () => SameDomain(row) ? native?.SelectedSource ?? "" : "", () => header && Expanded(row));
            writes.ItemValue<string>("b-action-keys", key, "source", () => SameDomain(row) ? native?.SelectedSource ?? "" : "", value => {
                if (!header || !Expanded(row) || native == null) return;
                native.CommitSource(value); bumper.Bump();
            });
            Read<string>("source-status", () => header && Expanded(row) ? native?.SourceStatusSummary(t) ?? "" : "");
            Read<string>("source-technical", () => header && Expanded(row) && row.Details ? native?.SourceStatus(t) ?? "" : "");
            Read<string>("source-topic", () => "us/behavior-source/selection");
            writes.ItemAction<string>("b-action-keys", key, "explain-source", Help);
            Read<bool>("timing-kind-choice", () => !Kernel.BuiltInActionKeys.Contains(action));
            Options<string>("timing-kind-options", () => new[] {
                new UiChoice<string>(t.Translate("US.UI.IntervalInteraction"), "Interaction"), new UiChoice<string>(t.Translate("US.UI.IntervalSimulation"), "Simulation") });
            display?.Value<string>("timing-kind", () => row.TimingKind, () => header && Expanded(row));
            writes.ItemValue<string>("b-action-keys", key, "timing-kind", () => row.TimingKind, value => {
                if (header && (value == "Interaction" || value == "Simulation")) { row.TimingKind = value; sheetRevision++; bumper.Bump(); }
            });

            // All field callbacks close over this exact action/domain, never the selected-action string.
            // Scope above remains an ordinary setter; these remain separate persist-first transactions.
            Read<bool>("field-visible", FieldVisible);
            Read<string>("field-title", () => field == "" ? "" : FieldTitle(field));
            Read<string>("field-note", () => Contribution(field) ? t.Translate(UsTuningFieldPresentation.For(field).NoteKey) : "");
            Read<bool>("has-field-note", () => Contribution(field));
            Read<string>("field-group", () => t.Translate(FieldGroup(field)));
            Read<bool>("field-group-first", () => field != "" && row.Fields.FirstOrDefault(f => FieldGroup(f) == FieldGroup(field)) == field);
            foreach (string kind in new[] { "interval", "interaction-seconds", "volume", "pitch", "parameter", "boolean" })
            {
                string k = kind;
                Read<bool>("field-is-" + k, () => field != "" && (k == "parameter" ? field.StartsWith("parameter:", StringComparison.Ordinal)
                    : k == "boolean" ? field is "enabled" or "talking" or "age" or "mood" || field.StartsWith("response:", StringComparison.Ordinal) || field.StartsWith("participation:", StringComparison.Ordinal) : field == k));
            }
            Options<string>("field-options", () => new[] {
                new UiChoice<string>(T("FieldInherit"), ""), new UiChoice<string>(t.Translate("US.UI.Yes"), "True"), new UiChoice<string>(t.Translate("US.UI.No"), "False") });
            display?.Value<string>("field-value", () => field == "" ? "" : native?.ReadActionField(field, action, race, child) ?? "", FieldVisible);
            writes.ItemValue<string>("b-action-keys", key, "field-value", () => field == "" ? "" : native?.ReadActionField(field, action, race, child) ?? "", value => {
                if (field == "" || native == null) return;
                bool committed = native.TryCommitActionField(field, value, action, race, child); bumper.Bump();
                if (!committed) throw new InvalidOperationException(native.RowResult(action, race, child));
            });
            Read<string>("field-effective", () => !FieldVisible() ? "" : native?.EffectiveFieldSummary(field, action, race, child, t) ?? "");
            Read<string>("field-provenance", () => !FieldVisible() || !row.Details ? "" : native?.EffectiveField(field, action, race, child, t) ?? "");
            Read<string>("field-topic", () => Contribution(field) ? "us/action-fields/contributions" : "us/action-fields/" + field);
            Read<string>("field-help-context", () => (ActionRow(row)?.DisplayName ?? action) + " · " + Context(row));
            writes.ItemAction<string>("b-action-keys", key, "explain-field", Help);
            Read<string>("technical-key", () => field);
            Read<bool>("details", () => Expanded(row) && row.Details);
            writes.ItemCommand("b-action-keys", key, "toggle-details", () => { row.Details = !row.Details; sheetRevision++; bumper.Bump(); }, () => footer && Expanded(row));
            Read<string>("explanation", () => footer && Expanded(row) && row.Details ? ActionRow(row)?.Explanation ?? "" : "");
            Read<string>("row-result", () => {
                string raw = native?.RowResult(action, race, child) ?? "";
                var parts = raw.Split(new[] { '\n' }, 2);
                return parts[0] == "" ? "" : t.Translate(parts[0]) + (parts.Length == 2 ? "\n" + parts[1] : "");
            });
            Read<bool>("has-row-result", () => (native?.RowResult(action, race, child) ?? "") != "");
            Read<string>("preset-anchor", () => ActionRow(row) is ActionScopeRowView r && r.HasPresetAnchor
                ? T("PresetAnchor") + ": " + r.ResetPresetTarget : T("NoAnchor"));
            writes.ItemCommand("b-action-keys", key, "row-reset", () => {
                if (footer && Expanded(row)) b.TryInvokeCommand("reset-action-row");
            }, () => footer && Expanded(row));
            writes.ItemCommand("b-action-keys", key, "preset-reset", () => {
                if (!footer || !Expanded(row) || ActionRow(row)?.PresetResetReady != true) return;
                if (native != null)
                {
                    if (!native.TryResetActionToPreset(action, out var fields)) return;
                    EndActionEdit(row.Layer, race, child, action, fields); bumper.Bump();
                }
                else { source.ResetActionToPreset(action); bumper.Bump(); }
            }, () => footer && Expanded(row) && ActionRow(row)?.PresetResetReady == true);
            writes.ItemCommand("b-action-keys", key, "rules-output", () => { source.OpenOutput(); bumper.Bump(); }, () => footer && Expanded(row) && source.CanOpenOutput);
        }
        foreach (var tuple in new[] { ("eat-phase", CoreActions.Eat, "phase"), ("eat-nutrition", CoreActions.Eat, "nutritionScope"), ("eat-drug", CoreActions.Eat, "drugScope"),
            ("select-player", CoreActions.Select, "selectRequirePlayerControlled"), ("select-awake", CoreActions.Select, "selectRequireAwake"), ("select-not-downed", CoreActions.Select, "selectRequireNotDowned") })
        {
            var pair = tuple;
            b.BindReadOnly<string>("b-effective-" + pair.Item1, () => native?.EffectiveField(pair.Item3, pair.Item2, Race(), Child(), t) ?? T("Unknown"));
        }
    }
}
