using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.Authoring;

namespace UniversalSqueaker.UI;

public static partial class UsKernelSettingsHost
{
    private static string TuningContextLabel(IUsKernelSettingsSource source, IUiTranslation t, int layer, string race, string child)
    {
        if (layer == 0) return t.Translate("US.UI.PawnProfile");
        string identity = race + (child == "" ? "" : " / " + child);
        var target = source.BuildView().Roster.FirstOrDefault(r => r.Race == race && r.Xeno == child);
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

    private static void BindBehaviorSheet(IUsKernelSettingsSource source, UsWriteBindings writes, SessionRevisionBumper bumper, IUiTranslation t)
    {
        var b = writes.Bindings;
        var s = source.ViewState;
        var native = source as UsKernelSettingsSource;
        string T(string key) => t.Translate("US.B." + key);
        string Race() => s.TuningLayer == 0 ? "" : s.TuningRaceDefName;
        string Child() => s.TuningLayer == 2 ? s.TuningXenotypeDefName : "";
        bool Valid() => s.TuningLayer == 0 || source.BuildView().Roster.Any(r => r.Race == s.TuningRaceDefName
            && (s.TuningLayer == 1 ? r.Xeno == "" && r.LoadedRace : r.Xeno == s.TuningXenotypeDefName && r.Editable));
        for (int i = 0; i < 3; i++)
        {
            int layer = i;
            b.BindReadOnly<bool>("b-layer-is-" + i, () => s.TuningLayer == layer);
            b.BindReadOnly<string>("b-layer-title-" + i, () => t.Translate(layer == 0 ? "US.UI.PawnProfile" : layer == 1 ? "US.Tuning.Layer.Race" : "US.Tuning.Layer.Xenotype"));
            writes.Command("b-layer-" + i, () => {
                if (layer > 0)
                {
                    var row = source.BuildView().Roster.FirstOrDefault(r => r.Race == s.SelectedRaceDefName && r.Xeno == (layer == 2 ? s.SelectedTargetName : ""));
                    if (row == null || (layer == 2 ? !row.Editable || row.Xeno == "" : !row.LoadedRace)) return;
                    source.SetTuningDomain(row.Race, layer == 2 ? row.Xeno : "");
                }
                source.SetTuningLayer(layer); bumper.Bump();
            }, () => layer == 0 || source.BuildView().Roster.Any(r => r.Race == s.SelectedRaceDefName && (layer == 1 ? r.Xeno == "" && r.LoadedRace : r.Xeno == s.SelectedTargetName && r.Xeno != "" && r.Editable)));
        }
        b.BindReadOnly<bool>("b-action-invalid", () => !Valid());
        writes.Command("b-area-reset", () => {
            string token = s.TuningLayer + "\n" + Race() + "\n" + Child();
            UsConfirmWindow.Open(t.Translate("US.Reset.Area"), t.Translate("US.Reset.Area.Action.Confirm") + "\n" + (s.TuningLayer == 0 ? t.Translate("US.UI.PawnProfile") : Race() + (Child() == "" ? "" : " / " + Child())),
                t.Translate("US.Reset.Confirm"), () => {
                    if (Valid() && token == s.TuningLayer + "\n" + Race() + "\n" + Child()) b.TryInvokeCommand("reset-action-area");
                });
        }, Valid);

        var retainedRows = new List<TuningActionRow>();
        var knownRows = new HashSet<string>(StringComparer.Ordinal);
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
        bool SameDomain(TuningActionRow row) => s.TuningLayer == row.Layer && Race() == row.Race && Child() == row.Child;
        ActionScopeRowView? ActionRow(TuningActionRow row)
        {
            var view = source.BuildView();
            if (!SameDomain(row)) return null;
            foreach (var current in view.ActionScopes) if (current.ActionKey == row.Action) return current;
            return null;
        }
        bool Current(TuningActionRow row) => SameDomain(row) && ActionRow(row).HasValue;
        bool Expanded(TuningActionRow row) => Current(row) && s.TuningSelectedAction == row.Action && Valid() && SameDomain(row);
        string Context(TuningActionRow row) => TuningContextLabel(source, t, row.Layer, row.Race, row.Child);
        bool Contribution(string field) => field.Contains(":");
        string FieldTitle(string field) => Contribution(field) ? UsTuningFieldPresentation.For(field).Title(field, t) : t.Translate("US.UI.Field." + field);
        string FieldGroup(string field) => Contribution(field) ? UsTuningFieldPresentation.For(field).GroupKey : "US.B.ActionFields";

        // One flat Repeat: header, advanced fields, contributions, then restores for each action.
        // Hidden entries stay in this ordered set. No nested Repeat or global-binding escape is used;
        // collapsing/reopening and exact-domain switches retain the same field node and draft.
        b.BindReadOnly<IReadOnlyList<string>>("b-action-keys", () => {
            var view = source.BuildView();
            int layer = s.TuningLayer;
            string race = Race(), child = Child();
            foreach (var action in view.ActionScopes.OrderBy(r => ActionOrder(r.ActionKey)).ThenBy(r => r.ActionKey, StringComparer.Ordinal))
            {
                string key = RosterKey(action.ActionKey + "\n" + layer + "\n" + race + "\n" + child);
                if (!knownRows.Add(key)) continue;
                var row = new TuningActionRow(key, action.ActionKey, layer, race, child);
                retainedRows.Add(row);
                BindItem(row, key + "-H", "", false);
                BindItem(row, key + "-Z", "", true);
            }
            var keys = new List<string>();
            foreach (var row in retainedRows.OrderBy(r => ActionOrder(r.Action)).ThenBy(r => r.Action, StringComparer.Ordinal))
            {
                keys.Add(row.Key + "-H");
                if (native != null && Expanded(row))
                {
                    var fields = new[] { "enabled", "interaction-seconds", "interval", "volume", "pitch", "talking", "age", "mood" }
                        .Concat(native.ContributionFields(row.Action, row.Race, row.Child)
                            .OrderBy(UsTuningFieldPresentation.Order).ThenBy(f => f, StringComparer.Ordinal));
                    foreach (string field in fields)
                    {
                        if (row.Fields.Contains(field)) continue;
                        row.Fields.Add(field);
                        BindItem(row, row.FieldKey(field), field, false);
                    }
                }
                keys.AddRange(row.Fields.Select(row.FieldKey));
                keys.Add(row.Key + "-Z");
            }
            return keys;
        });

        void BindItem(TuningActionRow row, string key, string field, bool footer)
        {
            bool header = field == "" && !footer;
            string action = row.Action, race = row.Race, child = row.Child;
            string Address(string leaf) => UsWriteBindings.ItemKey("b-action-keys", key, leaf);
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
            b.BindReadOnly<bool>(Address("visible"), () => header ? Current(row) : Expanded(row));
            b.BindReadOnly<bool>(Address("header"), () => header);
            b.BindReadOnly<bool>(Address("footer"), () => footer);
            b.BindReadOnly<bool>(Address("group-first"), () => header && Current(row) && source.BuildView().ActionScopes
                .Where(r => Group(r.ActionKey) == Group(action)).OrderBy(r => ActionOrder(r.ActionKey))
                .ThenBy(r => r.ActionKey, StringComparer.Ordinal).First().ActionKey == action);
            b.BindReadOnly<string>(Address("group"), () => Group(action) == 2 ? T("Custom") : t.Translate(Group(action) == 0
                ? "US.Tuning.Group.PlayerTriggered" : "US.Tuning.Group.SystemOrEvent"));
            b.BindReadOnly<string>(Address("title"), () => ActionRow(row)?.DisplayName ?? action);
            b.BindReadOnly<bool>(Address("expanded"), () => Expanded(row));
            b.BindReadOnly<bool>(Address("collapsed"), () => !Expanded(row));
            b.BindReadOnly<bool>(Address("editable"), Editable);
            b.BindReadOnly<bool>(Address("not-editable"), () => !Editable());
            b.BindReadOnly<string>(Address("scope-display"), () => ActionRow(row) is ActionScopeRowView r && r.HasOwnScope
                ? t.Translate(ActionScopeRules.ScopeLabelKey(action, r.Scope)) : t.Translate("US.Tuning.Auto"));
            b.BindReadOnly<string>(Address("context"), () => T("CurrentAction") + ": " + (ActionRow(row)?.DisplayName ?? action) + " · " + Context(row));
            b.BindReadOnly<string>(Address("payload"), () => action);
            b.BindChoices<BehaviorScope?>(Address("scope-options"), Choices);
            writes.ItemValue<BehaviorScope?>("b-action-keys", key, "scope", () => ActionRow(row) is ActionScopeRowView r && r.HasOwnScope ? r.Scope : null,
                scope => {
                    // SetActionScope reads page state: refuse a stale popup before reaching that owner.
                    if (!Editable() || !Choices().Any(choice => choice.Value == scope)) return;
                    source.SetActionScope(action, scope); bumper.Bump();
                });
            b.BindReadOnly<string>(Address("scope-effective"), () => ActionRow(row) is ActionScopeRowView r && !(r.HasOwnScope && r.Scope == r.EffectiveScope)
                ? "→ " + t.Translate(ActionScopeRules.ScopeLabelKey(action, r.EffectiveScope)) : "");
            b.BindReadOnly<bool>(Address("has-scope-hint"), () => ActionRow(row) is ActionScopeRowView r && !(r.HasOwnScope && r.Scope == r.EffectiveScope));
            writes.ItemCommand("b-action-keys", key, "advanced", () => {
                if (!Editable()) return;
                source.SetTuningSelectedAction(s.TuningSelectedAction == action ? "" : action); bumper.Bump();
            }, Editable);
            void Help(string topic)
            {
                s.PinnedHelpRef = topic; s.PinnedHelpContext = (ActionRow(row)?.DisplayName ?? action) + " · " + Context(row)
                    + (field == "" ? "" : "\n" + FieldTitle(field));
                s.HelpPanelOpen = true; bumper.ResetHelpScroll(); bumper.Bump();
            }
            b.BindReadOnly<bool>(Address("source-visible"), () => header && Expanded(row) && native != null);
            b.BindChoices<string>(Address("source-options"), () => header && Expanded(row) ? native?.SourceChoices(t) ?? Array.Empty<UiChoice<string>>() : Array.Empty<UiChoice<string>>());
            writes.ItemValue<string>("b-action-keys", key, "source", () => SameDomain(row) ? native?.SelectedSource ?? "" : "", value => {
                if (!header || !Expanded(row) || native == null) return;
                native.CommitSource(value); bumper.Bump();
            });
            b.BindReadOnly<string>(Address("source-status"), () => Expanded(row) ? native?.SourceStatus(t) ?? "" : "");
            b.BindReadOnly<string>(Address("source-topic"), () => "us/behavior-source/selection");
            writes.ItemAction<string>("b-action-keys", key, "explain-source", Help);
            b.BindReadOnly<bool>(Address("timing-kind-choice"), () => !Kernel.BuiltInActionKeys.Contains(action));
            b.BindChoices<string>(Address("timing-kind-options"), () => new[] {
                new UiChoice<string>(t.Translate("US.UI.IntervalInteraction"), "Interaction"), new UiChoice<string>(t.Translate("US.UI.IntervalSimulation"), "Simulation") });
            writes.ItemValue<string>("b-action-keys", key, "timing-kind", () => row.TimingKind, value => {
                if (header && (value == "Interaction" || value == "Simulation")) { row.TimingKind = value; bumper.Bump(); }
            });

            // All field callbacks close over this exact action/domain, never the selected-action string.
            // Scope above remains an ordinary setter; these remain separate persist-first transactions.
            b.BindReadOnly<bool>(Address("field-visible"), FieldVisible);
            b.BindReadOnly<string>(Address("field-title"), () => field == "" ? "" : FieldTitle(field));
            b.BindReadOnly<string>(Address("field-note"), () => Contribution(field) ? t.Translate(UsTuningFieldPresentation.For(field).NoteKey) : "");
            b.BindReadOnly<bool>(Address("has-field-note"), () => Contribution(field));
            b.BindReadOnly<string>(Address("field-group"), () => t.Translate(FieldGroup(field)));
            b.BindReadOnly<bool>(Address("field-group-first"), () => field != "" && row.Fields.FirstOrDefault(f => FieldGroup(f) == FieldGroup(field)) == field);
            foreach (string kind in new[] { "interval", "interaction-seconds", "volume", "pitch", "parameter", "boolean" })
            {
                string k = kind;
                b.BindReadOnly<bool>(Address("field-is-" + k), () => field != "" && (k == "parameter" ? field.StartsWith("parameter:", StringComparison.Ordinal)
                    : k == "boolean" ? field is "enabled" or "talking" or "age" or "mood" || field.StartsWith("response:", StringComparison.Ordinal) || field.StartsWith("participation:", StringComparison.Ordinal) : field == k));
            }
            b.BindChoices<string>(Address("field-options"), () => new[] {
                new UiChoice<string>(T("FieldInherit"), ""), new UiChoice<string>(t.Translate("US.UI.Yes"), "True"), new UiChoice<string>(t.Translate("US.UI.No"), "False") });
            writes.ItemValue<string>("b-action-keys", key, "field-value", () => field == "" ? "" : native?.ReadActionField(field, action, race, child) ?? "", value => {
                if (field == "" || native == null) return;
                bool committed = native.TryCommitActionField(field, value, action, race, child); bumper.Bump();
                if (!committed) throw new InvalidOperationException(native.RowResult(action, race, child));
            });
            b.BindReadOnly<string>(Address("field-effective"), () => field == "" ? "" : native?.EffectiveField(field, action, race, child, t) ?? "");
            b.BindReadOnly<string>(Address("field-topic"), () => Contribution(field) ? "us/action-fields/contributions" : "us/action-fields/" + field);
            writes.ItemAction<string>("b-action-keys", key, "explain-field", Help);
            b.BindReadOnly<string>(Address("technical-key"), () => field);
            b.BindReadOnly<bool>(Address("details"), () => Expanded(row) && row.Details);
            writes.ItemCommand("b-action-keys", key, "toggle-details", () => { row.Details = !row.Details; bumper.Bump(); }, () => footer && Expanded(row));
            b.BindReadOnly<string>(Address("explanation"), () => ActionRow(row)?.Explanation ?? "");
            b.BindReadOnly<string>(Address("row-result"), () => {
                string raw = native?.RowResult(action, race, child) ?? "";
                var parts = raw.Split(new[] { '\n' }, 2);
                return parts[0] == "" ? "" : t.Translate(parts[0]) + (parts.Length == 2 ? "\n" + parts[1] : "");
            });
            b.BindReadOnly<bool>(Address("has-row-result"), () => (native?.RowResult(action, race, child) ?? "") != "");
            b.BindReadOnly<string>(Address("preset-anchor"), () => ActionRow(row) is ActionScopeRowView r && r.HasPresetAnchor
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
            writes.ItemCommand("b-action-keys", key, "rules-output", () => { NavigatePage(s, "Output"); bumper.Bump(); }, () => footer && Expanded(row));
        }
        foreach (var tuple in new[] { ("eat-phase", CoreActions.Eat, "phase"), ("eat-nutrition", CoreActions.Eat, "nutritionScope"), ("eat-drug", CoreActions.Eat, "drugScope"),
            ("select-player", CoreActions.Select, "selectRequirePlayerControlled"), ("select-awake", CoreActions.Select, "selectRequireAwake"), ("select-not-downed", CoreActions.Select, "selectRequireNotDowned") })
        {
            var pair = tuple;
            b.BindReadOnly<string>("b-effective-" + pair.Item1, () => native?.EffectiveField(pair.Item3, pair.Item2, Race(), Child(), t) ?? T("Unknown"));
        }
    }
}
