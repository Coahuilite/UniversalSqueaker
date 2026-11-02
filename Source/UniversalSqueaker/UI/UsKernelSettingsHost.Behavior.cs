using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.Authoring;

namespace UniversalSqueaker.UI;

public static partial class UsKernelSettingsHost
{
    private static void BindBehaviorSheet(IUsKernelSettingsSource source, UsWriteBindings writes, SessionRevisionBumper bumper, IUiTranslation t)
    {
        var b = writes.Bindings; var s = source.ViewState; var native = source as UsKernelSettingsSource;
        string T(string key) => t.Translate("US.B." + key);
        string Race() => s.TuningLayer == 0 ? "" : s.TuningRaceDefName;
        string Child() => s.TuningLayer == 2 ? s.TuningXenotypeDefName : "";
        bool Valid() => s.TuningLayer == 0 || source.BuildView().Roster.Any(r => r.Race == s.TuningRaceDefName
            && (s.TuningLayer == 1 ? r.Xeno == "" && r.LoadedRace : r.Xeno == s.TuningXenotypeDefName && r.Editable));
        ActionScopeRowView? ActionRow()
        {
            foreach (var r in source.BuildView().ActionScopes) if (r.ActionKey == s.TuningSelectedAction) return r;
            return null;
        }
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
        b.BindReadOnly<bool>("b-behavior-list", () => s.WideLayout || s.TuningSelectedAction == "");
        b.BindReadOnly<bool>("b-behavior-sheet", () => s.WideLayout || s.TuningSelectedAction != "");
        b.BindReadOnly<bool>("b-action-selected", () => s.TuningSelectedAction != "" && Valid());
        b.BindReadOnly<bool>("b-action-invalid", () => !Valid());
        b.BindReadOnly<bool>("b-action-back", () => !s.WideLayout && s.TuningSelectedAction != "");
        writes.Command("b-action-back", () => { source.SetTuningSelectedAction(""); bumper.Bump(); });
        b.BindReadOnly<string>("b-action-title", () => ActionRow()?.DisplayName ?? T("ChooseAction"));
        b.BindReadOnly<string>("b-action-explanation", () => ActionRow()?.Explanation ?? "");
        b.BindReadOnly<string>("b-preset-anchor", () => ActionRow() is ActionScopeRowView r && r.HasPresetAnchor ? T("PresetAnchor") + ": " + r.ResetPresetTarget : T("NoAnchor"));
        writes.Command("b-area-reset", () => {
            string token = s.TuningLayer + "\n" + Race() + "\n" + Child();
            UsConfirmWindow.Open(t.Translate("US.Reset.Area"), t.Translate("US.Reset.Area.Action.Confirm") + "\n" + (s.TuningLayer == 0 ? t.Translate("US.UI.PawnProfile") : Race() + (Child() == "" ? "" : " / " + Child())),
                t.Translate("US.Reset.Confirm"), () => {
                    if (token == s.TuningLayer + "\n" + Race() + "\n" + Child()) b.TryInvokeCommand("reset-action-area");
                });
        }, Valid);
        writes.Command("b-row-reset", () => {
            if (source.ResetActionTuningRow(s.TuningSelectedAction) == SqueakResetOutcome.Applied)
            { source.EndAffectedEdit(id => id.StartsWith("sparse-", StringComparison.Ordinal) || id.StartsWith("contribution-", StringComparison.Ordinal)); bumper.Bump(); }
        }, () => Valid() && s.TuningSelectedAction != "");
        writes.Command("b-preset-reset", () => { source.ResetActionToPreset(s.TuningSelectedAction); bumper.Bump(); }, () => Valid() && ActionRow()?.PresetResetReady == true);
        b.BindChoices<BehaviorScope?>("b-scope-options", () => {
            string action = s.TuningSelectedAction;
            var options = new List<UiChoice<BehaviorScope?>> { new(t.Translate("US.Tuning.Auto"), null), new(t.Translate("US.Tuning.Scope.Off"), BehaviorScope.Disabled) };
            bool official = Kernel.BuiltInActionKeys.Contains(action);
            var supported = official ? BuiltInPawnContracts.SupportedScopes(action) : ActionScopeSupport.AnyOccurrence | ActionScopeSupport.ActiveCommand;
            foreach (var scope in new[] { BehaviorScope.AnyOccurrence, BehaviorScope.ActiveCommand })
                if ((supported & (scope == BehaviorScope.AnyOccurrence ? ActionScopeSupport.AnyOccurrence : ActionScopeSupport.ActiveCommand)) != 0)
                    options.Add(new UiChoice<BehaviorScope?>(t.Translate(ActionScopeRules.ScopeLabelKey(action, scope)), scope));
            return options;
        });
        writes.Value<BehaviorScope?>("b-scope", () => ActionRow() is ActionScopeRowView r && r.HasOwnScope ? r.Scope : null,
            value => { if (Valid() && s.TuningSelectedAction != "") { source.SetActionScope(s.TuningSelectedAction, value); bumper.Bump(); } });
        b.BindReadOnly<string>("b-scope-effective", () => native?.EffectiveField("scope", s.TuningSelectedAction, Race(), Child(), t) ?? T("Unknown"));
        b.BindReadOnly<string>("b-row-result", () => {
            string raw = native?.RowResult(s.TuningSelectedAction, Race(), Child()) ?? "";
            var parts = raw.Split(new[] { '\n' }, 2); return parts[0] == "" ? "" : t.Translate(parts[0]) + (parts.Length == 2 ? "\n" + parts[1] : "");
        });
        var knownActions = new HashSet<string>(StringComparer.Ordinal);
        b.BindReadOnly<IReadOnlyList<string>>("b-action-keys", () => source.BuildView().ActionScopes.OrderBy(r => Kernel.BuiltInActionKeys.Contains(r.ActionKey) ? r.Group == ActionScopeGroup.PlayerTriggered ? 0 : 1 : 2)
            .Select(r => {
                string action = r.ActionKey, key = RosterKey(action); ActionScopeRowView? Row() { foreach (var x in source.BuildView().ActionScopes) if (x.ActionKey == action) return x; return null; }
                if (knownActions.Add(key))
                {
                    string Address(string leaf) => UsWriteBindings.ItemKey("b-action-keys", key, leaf);
                    b.BindReadOnly<string>(Address("title"), () => Row()?.DisplayName ?? action);
                    b.BindReadOnly<string>(Address("group"), () => !Kernel.BuiltInActionKeys.Contains(action) ? T("Custom") : t.Translate(Row()?.Group == ActionScopeGroup.PlayerTriggered ? "US.Tuning.Group.PlayerTriggered" : "US.Tuning.Group.SystemOrEvent"));
                    b.BindReadOnly<bool>(Address("selected"), () => s.TuningSelectedAction == action);
                    b.BindReadOnly<string>(Address("payload"), () => action);
                    writes.ItemAction<string>("b-action-keys", key, "choose", _ => { source.SetTuningSelectedAction(action); bumper.Bump(); });
                }
                return key;
            }).ToArray());
        var contributionKeys = new List<string>(); var contributions = new HashSet<string>(StringComparer.Ordinal);
        b.BindReadOnly<IReadOnlyList<string>>("b-contribution-keys", () => {
            if (native == null || !Valid() || s.TuningSelectedAction == "") return contributionKeys;
            string action = s.TuningSelectedAction, race = Race(), child = Child();
            foreach (string field in native.ContributionFields(action, race, child))
            {
                string f = field, key = RosterKey(action + "\n" + race + "\n" + child + "\n" + f);
                if (!contributions.Add(key)) continue;
                contributionKeys.Add(key); string Address(string leaf) => UsWriteBindings.ItemKey("b-contribution-keys", key, leaf);
                b.BindReadOnly<bool>(Address("current"), () => s.TuningSelectedAction == action && Race() == race && Child() == child);
                b.BindReadOnly<string>(Address("title"), () => f);
                b.BindReadOnly<bool>(Address("number"), () => f.StartsWith("parameter:", StringComparison.Ordinal));
                b.BindReadOnly<bool>(Address("boolean"), () => !f.StartsWith("parameter:", StringComparison.Ordinal));
                b.BindChoices<string>(Address("options"), () => new[] { new UiChoice<string>(t.Translate("US.UI.SourceInherit"), ""), new UiChoice<string>(t.Translate("US.UI.Yes"), "True"), new UiChoice<string>(t.Translate("US.UI.No"), "False") });
                writes.ItemValue<string>("b-contribution-keys", key, "value", () => native.ReadActionField(f, action, race, child), value => {
                    bool committed = native.TryCommitActionField(f, value, action, race, child); bumper.Bump();
                    if (!committed) throw new InvalidOperationException(native.RowResult(action, race, child)); });
                b.BindReadOnly<string>(Address("effective"), () => native.EffectiveField(f, action, race, child, t));
            }
            return contributionKeys;
        });
        foreach (var tuple in new[] { ("eat-phase", CoreActions.Eat, "phase"), ("eat-nutrition", CoreActions.Eat, "nutritionScope"), ("eat-drug", CoreActions.Eat, "drugScope"),
            ("select-player", CoreActions.Select, "selectRequirePlayerControlled"), ("select-awake", CoreActions.Select, "selectRequireAwake"), ("select-not-downed", CoreActions.Select, "selectRequireNotDowned") })
        {
            var pair = tuple;
            b.BindReadOnly<string>("b-effective-" + pair.Item1, () => native?.EffectiveField(pair.Item3, pair.Item2, Race(), Child(), t) ?? T("Unknown"));
        }
    }
}
