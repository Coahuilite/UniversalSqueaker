using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public static partial class UsKernelSettingsHost
{
    private static void BindFinalSheet(IUsKernelSettingsSource source, UsWriteBindings writes, SessionRevisionBumper bumper, IUiTranslation t)
    {
        var b = writes.Bindings; var s = source.ViewState; var registered = new HashSet<string>(StringComparer.Ordinal);
        string T(string key) => t.Translate("US.B." + key);
        b.BindReadOnly<bool>("b-final-list-visible", () => s.WideLayout || !s.FallbackDetailOpen);
        b.BindReadOnly<bool>("b-final-sheet-visible", () => s.WideLayout || s.FallbackDetailOpen);
        b.BindReadOnly<bool>("b-final-back-visible", () => !s.WideLayout && s.FallbackDetailOpen);
        b.BindReadOnly<bool>("b-final-existing", () => !s.FallbackCreating);
        b.BindReadOnly<bool>("b-final-creating", () => s.FallbackCreating);
        writes.Command("b-final-existing", () => { s.FallbackCreating = false; s.FallbackDetailOpen = false; bumper.Bump(); });
        writes.Command("b-final-new", () => { s.FallbackCreating = true; s.FallbackDetailOpen = false; bumper.Bump(); });
        writes.Command("b-final-back", () => { s.FallbackDetailOpen = false; bumper.Bump(); });
        writes.Command("b-final-cancel", () => {
            if (s.FallbackSelectedEntryAction != "") source.SetFallbackSelection(null, "");
            else if (!s.WideLayout && s.FallbackDetailOpen) s.FallbackDetailOpen = false;
            else { source.SetFallbackSelection("", ""); s.PendingFallbackRace = ""; s.FallbackDetailOpen = false; }
            bumper.Bump();
        }, () => !s.HelpPanelOpen && (s.FallbackSelectedEntryAction != "" || s.FallbackDetailOpen || s.FallbackSelectedRace != "" || s.PendingFallbackRace != ""));
        b.BindReadOnly<string>("b-final-title", () => s.FallbackCreating ? s.PendingFallbackRace : source.BuildView().FallbackSelectedRace);
        b.BindReadOnly<string>("b-final-status", () => {
            var view = source.BuildView(); string text = view.FallbackStatusKey == "" ? "" : t.Translate(view.FallbackStatusKey);
            if (s.PendingFallbackRace != "" && s.FallbackCreating) text += "\n" + T("AdmissionNote");
            return text;
        });
        b.BindReadOnly<bool>("b-final-can-create", () => s.FallbackCreating && s.PendingFallbackRace != ""
            && RaceDefinitionCatalog.Current.Races.Any(r => r.DefName == s.PendingFallbackRace) && !source.BuildView().FallbackRaces.Any(r => r.DefName == s.PendingFallbackRace));
        writes.Command("b-final-create", () => {
            string race = s.PendingFallbackRace; if (race == "") return;
            source.CreateFallbackTable(race); bumper.Bump();
            if (source.BuildView().FallbackRaces.Any(r => r.DefName == race)) { s.FallbackCreating = false; source.SetFallbackSelection(race, ""); bumper.Bump(); }
        });
        b.BindReadOnly<bool>("b-final-has-table", () => !s.FallbackCreating && source.BuildView().FallbackSelectedRace != "");
        b.BindReadOnly<bool>("b-final-has-entry", () => !s.FallbackCreating && source.BuildView().FallbackSelectedRace != "" && s.FallbackSelectedEntryAction != "");
        b.BindReadOnly<string>("b-final-entry", () => s.FallbackSelectedEntryAction);
        writes.Command("b-final-close-sound", () => { source.SetFallbackSelection(null, ""); bumper.Bump(); });
        void WriteSound(string? sound)
        {
            string race = source.BuildView().FallbackSelectedRace, action = s.FallbackSelectedEntryAction;
            if (race == "" || action == "") return;
            source.SetFallbackSelection(race, action); source.SetFallbackEntry(action, sound); bumper.Bump();
        }
        writes.Command("b-final-silence", () => WriteSound(""));
        writes.Command("b-final-inherit", () => WriteSound(null));
        writes.Command("b-final-restore", () => { source.RestoreFallbackDefault(); bumper.Bump(); });
        writes.Command("b-final-delete", () => {
            string race = source.BuildView().FallbackSelectedRace; if (race == "") return;
            UsConfirmWindow.Open(t.Translate("US.VF1.Delete"), t.Translate("US.VF1.DeleteQuestion") + " " + race,
                t.Translate("US.VF1.DeleteConfirm"), () => { source.DeleteFallbackTable(race); bumper.Bump(); });
        });
        foreach (string listId in new[] { "table", "candidate", "sound" })
        {
            string id = listId, family = "b-final-" + id + "-keys";
            UsBrowseListState State() => id == "table" ? s.TableBrowse : id == "candidate" ? s.CandidateBrowse : s.SoundBrowse;
            UsBrowseListView View() { source.BuildView(); return id == "table" ? s.TableList : id == "candidate" ? s.CandidateList : s.SoundList; }
            void Op(string op) => b.Invoke(id + "-browse", op);
            b.BindReadOnly<string>("b-final-" + id + "-count", () => View().Filtered + "/" + View().Eligible + " · " + T("Page") + " " + (View().Page + 1) + "/" + View().Pages
                + (State().SourcePackageId == "" ? "" : " · " + State().SourcePackageId));
            writes.Command("b-final-" + id + "-prev", () => Op("prev")); writes.Command("b-final-" + id + "-next", () => Op("next"));
            writes.Command("b-final-" + id + "-clear", () => Op("clear"));
            writes.Value<string>("b-final-" + id + "-range", () => State().Group.ToString(), value => Op("group:" + value));
            b.BindChoices<string>("b-final-" + id + "-ranges", () => new[] { UsBoardBrowsePolicy.BoardGroup.PlayerRaces, UsBoardBrowsePolicy.BoardGroup.HarHumanlike, UsBoardBrowsePolicy.BoardGroup.Human,
                UsBoardBrowsePolicy.BoardGroup.Creatures, UsBoardBrowsePolicy.BoardGroup.Other, UsBoardBrowsePolicy.BoardGroup.All }.Select(g => new UiChoice<string>(t.Translate(UsBoardBrowsePolicy.GroupLabelKey(g)), g.ToString())).ToArray());
            b.BindReadOnly<bool>("b-final-" + id + "-sources-open", () => State().SourcesOpen);
            writes.Command("b-final-" + id + "-sources-toggle", () => Op("sources"));
            writes.Command("b-final-" + id + "-source-clear", () => Op("source:"));
            writes.Command("b-final-" + id + "-source-prev", () => Op("sources-prev"));
            writes.Command("b-final-" + id + "-source-next", () => Op("sources-next"));
            b.BindReadOnly<string>("b-final-" + id + "-source-count", () => View().SourceCount + " · " + (View().SourcePage + 1) + "/" + View().SourcePages);
            string sourcesFamily = "b-final-" + id + "-source-keys";
            b.BindReadOnly<IReadOnlyList<string>>(sourcesFamily, () => View().Sources.Select(item => {
                string value = item.Value, key = RosterKey(value);
                if (registered.Add(sourcesFamily + key))
                {
                    string A(string leaf) => UsWriteBindings.ItemKey(sourcesFamily, key, leaf);
                    b.BindReadOnly<string>(A("title"), () => item.Label + " · " + value);
                    b.BindReadOnly<string>(A("payload"), () => value);
                    b.BindReadOnly<bool>(A("selected"), () => State().SourcePackageId == value);
                    writes.ItemAction<string>(sourcesFamily, key, "choose", _ => Op("source:" + value));
                }
                return key;
            }).ToArray());
            b.BindReadOnly<IReadOnlyList<string>>(family, () => View().Rows.Select(item => {
                string value = item.Value, key = RosterKey(value);
                if (registered.Add(family + key))
                {
                    string A(string leaf) => UsWriteBindings.ItemKey(family, key, leaf);
                    b.BindReadOnly<string>(A("title"), () => item.Label + " · " + value);
                    b.BindReadOnly<string>(A("payload"), () => value);
                    b.BindReadOnly<bool>(A("selected"), () => id == "table" ? s.FallbackSelectedRace == value : id == "candidate" && s.PendingFallbackRace == value);
                    writes.ItemAction<string>(family, key, "choose", _ => {
                        if (id == "table") { source.SetFallbackSelection(value, ""); s.FallbackDetailOpen = true; }
                        else if (id == "candidate") { s.PendingFallbackRace = value; s.FallbackDetailOpen = true; }
                        else WriteSound(value);
                        bumper.Bump();
                    });
                }
                return key;
            }).ToArray());
        }
        b.BindReadOnly<IReadOnlyList<string>>("b-final-entry-keys", () => {
            string race = source.BuildView().FallbackSelectedRace;
            return source.BuildView().FallbackEntries.Select(entry => {
                string action = entry.ActionKey, key = RosterKey(race + "\n" + action);
                FallbackEntryView Entry() => source.BuildView().FallbackEntries.FirstOrDefault(e => e.ActionKey == action);
                if (registered.Add("entry:" + key))
                {
                    string A(string leaf) => UsWriteBindings.ItemKey("b-final-entry-keys", key, leaf);
                    b.BindReadOnly<string>(A("title"), () => Entry().ActionLabel + " · " + Entry().SoundDefName + " · " + t.Translate(Entry().State switch {
                        FallbackEntryView.PlayerOverride => "US.VF1.State.Override", FallbackEntryView.Maintainer => "US.VF1.State.Maintainer", FallbackEntryView.NoSound => "US.VF1.State.NoSound", _ => "US.VF1.State.Unset" }));
                    b.BindReadOnly<bool>(A("selected"), () => s.FallbackSelectedRace == race && s.FallbackSelectedEntryAction == action);
                    b.BindReadOnly<string>(A("payload"), () => action);
                    writes.ItemAction<string>("b-final-entry-keys", key, "choose", _ => { source.SetFallbackSelection(race, action); bumper.Bump(); });
                }
                return key;
            }).ToArray();
        });
    }
}
