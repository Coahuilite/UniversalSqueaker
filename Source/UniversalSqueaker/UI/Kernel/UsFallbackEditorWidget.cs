using System;
using System.Collections.Generic;

using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Final native fallback table editor. Independent bounded browsers project the live store and
/// published definitions; selection is page state, while explicit commands keep the existing owner.
///
/// <para>
/// The race picker over the supported tables (maintainer ∪ player), the closed 15-key entry list with
/// per-row state, a query-filtered sound picker labelled with the CURRENT production availability (an
/// unprobed sound says "unknown", never "missing"), create-from-loaded-pawn-races, restore-default and
/// deletion through ONE ordinary UiKit confirmation window. Every binding key it touches is the page
/// table's pre-existing one; nothing here is a second authority, and the writes keep their original
/// persistence chain (business setter → runtime notification → QueuePersistence → debounced save).
/// </para>
///
/// <para>
/// Its return layer (FL-IC2 CancelBind, declared on the manifest Section that wraps it) is
/// <c>cancel-fallback-table</c>: the two visible-branch steps the tuning tree used to own for area 2
/// — entry first, then the table — with the cancelled-table mark retained through a filter clear.
/// Neither press writes settings, routing or persistence.
/// </para>
/// </summary>
public sealed class UsFallbackEditorWidget : UsSectionWidgetBase
{
    public const string KindName = "us/fallback-editor";

    // The rhythm constants the tuning tree's editor drew with; copied with the draw, one source now.
    private const float RowHeight = 28f;
    private const float RowGap = 2f;

    // A value no SoundDef can have: the display-only pair that lets an UNSET entry read as "unset"
    // instead of masquerading as the explicit no-sound marker.
    private const string DisplayOnlyCurrentValue = "\u0000vf1-display-only";

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, KindName, () => new UsFallbackEditorWidget(), UsKernelWidgetRegistrar.SectionSchema));
    }

    public override void Validate(IUiBindingAccess bindings, string elementPath)
    {
        foreach (string id in new[] { "table", "candidate", "sound" }) UsBrowseControls.Validate(bindings, elementPath, id);
        bindings.ValidateValue<string>("fallback-pending-race", elementPath);
        bindings.ValidateAction<string>("select-fallback-candidate", elementPath);
        // The full fallback surface moved with the editor: a missing key must fail at creation, not
        // half-draw the table (the same rule the scope-tree comment recorded for these keys).
        bindings.ValidateValue<IReadOnlyList<FallbackRaceView>>("fallback-races", elementPath);
        bindings.ValidateValue<string>("fallback-selected-race", elementPath);
        bindings.ValidateValue<IReadOnlyList<FallbackEntryView>>("fallback-entries", elementPath);
        bindings.ValidateValue<string>("fallback-selected-entry", elementPath);
        bindings.ValidateValue<string>("fallback-status", elementPath);
        bindings.ValidateValue<string>("fallback-new-race-query", elementPath);
        bindings.ValidateValue<string>("fallback-sound-query", elementPath);
        bindings.ValidateOptions<FilterOptionView>("fallback-sound-options", elementPath);
        bindings.ValidateOptions<FilterOptionView>("fallback-candidate-options", elementPath);
        bindings.ValidateAction<UsFallbackSelection>("set-fallback-selection", elementPath);
        bindings.ValidateAction<UsFallbackEntryWrite>("set-fallback-entry", elementPath);
        bindings.ValidateAction<string>("create-fallback-table", elementPath);
        bindings.ValidateCommand("restore-fallback-default", elementPath);
        bindings.ValidateAction<string>("delete-fallback-table", elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return 3 * (RowHeight + RowGap);
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        return EditorHeight(ctx, BodyWidth(ctx));
    }

    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (TitleHidden)
        {
            DrawEditor(rect.Y, rect.X, rect.Width, ctx);
            return;
        }

        DrawCard(rect, ctx, body => DrawEditor(body.Y, body.X, body.Width, ctx));
    }

    private static float EditorHeight(UiWidgetContext ctx, float width)
    {
        // Mirrors the bounded table/candidate browsers, selected-table status and optional editor.
        // UIR-004: the status line is the one band whose TEXT is a full sentence (the no-tables and
        // restart notices), so its height is wrap-aware - measured through the same seam and font that
        // DrawEditor uses, never a fixed band, because the card page fit audit runs this editor at the
        // game's minimum box in both shipped languages.
        float height = 2 * (RowHeight + RowGap) + StatusBandHeight(ctx, width) + RowGap;
        height += UsBrowseControls.Height(ctx, "table", true) + UsBrowseControls.Height(ctx, "candidate", true);
        string selectedRace = ctx.Bindings.TryGet("fallback-selected-race", out string sr) ? sr ?? "" : "";
        if (selectedRace.Length > 0)
        {
            height += UniversalSqueaker.Kernel.BuiltInActionKeys.All.Count * RowHeight + RowGap;
            height += RowHeight + RowGap; // restore/delete
            string selectedEntry = ctx.Bindings.Get<string>("fallback-selected-entry") ?? "";
            if (selectedEntry.Length > 0) height += UsBrowseControls.Height(ctx, "sound", true) + RowHeight + RowGap;
        }

        return height;
    }

    /// <summary>The status sentence DrawEditor will paint for THIS frame - resolved once so measure
    /// and draw answer the same string through the same translation seam.</summary>
    private static string StatusText(UiWidgetContext ctx)
    {
        IReadOnlyList<FallbackRaceView> races = ctx.Bindings.TryGet("fallback-races", out IReadOnlyList<FallbackRaceView> rs)
            ? rs : Array.Empty<FallbackRaceView>();
        string status = ctx.Bindings.TryGet("fallback-status", out string st) ? st ?? "" : "";
        string selected = ctx.Bindings.Get<string>("fallback-selected-race") ?? "";
        var tableView = ctx.Bindings.Get<UsBrowseListView>("table-browse-view");
        bool hidden = selected.Length > 0;
        foreach (var row in tableView.Rows) if (row.Value == selected) hidden = false;
        string selection = selected.Length == 0 ? "" : UsKernelDraw.Keyed(ctx, "US.Browse.Editing") + ": " + selected
            + (hidden ? " · " + UsKernelDraw.Keyed(ctx, "US.Browse.HiddenSelection") : "");
        string notice = status.Length > 0 ? UsKernelDraw.Keyed(ctx, status)
            : races.Count == 0 ? UsKernelDraw.Keyed(ctx, "US.VF1.NoTable") : "";
        return selection + (notice.Length > 0 ? "\n" + notice : "");
    }

    private static float StatusBandHeight(UiWidgetContext ctx, float width)
    {
        return UsKernelDraw.TextBandHeight(Math.Max(1f, width), ctx, StatusText(ctx), UiFont.Tiny, RowHeight);
    }

    /// <summary>The VF1 editor body, line-identical to the tuning tree's DrawFallbackEditor.</summary>
    private void DrawEditor(float y, float x, float width, UiWidgetContext ctx)
    {
        IReadOnlyList<FallbackRaceView> races = ctx.Bindings.TryGet("fallback-races", out IReadOnlyList<FallbackRaceView> rs)
            ? rs : Array.Empty<FallbackRaceView>();
        string selectedRace = ctx.Bindings.TryGet("fallback-selected-race", out string sr) ? sr ?? "" : "";

        var racePairs = new List<KeyValuePair<string, string>>();
        y += UsBrowseControls.Draw(x, y, width, ctx, "table", true, true);
        foreach (UsBrowseItem race in ctx.Bindings.Get<UsBrowseListView>("table-browse-view").Rows)
        {
            racePairs.Add(new KeyValuePair<string, string>(
                race.Label + "  " + race.Value, race.Value));
        }

        UsKernelDraw.Label(ctx.Drawing, new UiLayoutRect(x, y, 60f, RowHeight), UsKernelDraw.Keyed(ctx, "US.VF1.Race"),
            ctx.Theme, ctx.Theme[UiPaletteRole.Foreground], UiFont.Tiny, UiTextAnchor.MiddleLeft);
        UsKernelDraw.Dropdown(new UiLayoutRect(x + 64f, y, Math.Max(1f, width - 64f), RowHeight), "vf1-race", ctx, selectedRace, racePairs,
            selected => ctx.Bindings.Invoke("set-fallback-selection", new UsFallbackSelection(selected, null)));
        y += RowHeight + RowGap;

        // UIR-004: the status sentence keeps the wrap-aware band the measure reserved - the same
        // StatusText/StatusBandHeight outlet, so the drawn band can never disagree with the reserved one.
        string statusText = StatusText(ctx);
        float statusBand = StatusBandHeight(ctx, width);
        UsKernelDraw.Label(ctx.Drawing, new UiLayoutRect(x, y, width, statusBand),
            statusText,
            ctx.Theme, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.UpperLeft);
        y += statusBand + RowGap;

        // Create row: query + candidate dropdown (candidates = loaded pawn races, so a race with no
        // VoicePack at all is a legal new table).
        y += UsBrowseControls.Draw(x, y, width, ctx, "candidate", true, true);

        IReadOnlyList<FilterOptionView> candidates = ctx.Bindings.GetOptions<FilterOptionView>("fallback-candidate-options");
        var candidatePairs = new List<KeyValuePair<string, string>>();
        foreach (FilterOptionView option in candidates) candidatePairs.Add(new KeyValuePair<string, string>(option.DisplayName, option.Value));
        string pending = ctx.Bindings.Get<string>("fallback-pending-race") ?? "";
        string candidateDisplay = pending;
        if (pending.Length > 0 && !candidatePairs.Exists(pair => pair.Value == pending))
        {
            candidateDisplay = DisplayOnlyCurrentValue;
            candidatePairs.Add(new KeyValuePair<string, string>(pending + " · " + UsKernelDraw.Keyed(ctx, "US.Browse.OffPage"), candidateDisplay));
        }
        UsKernelDraw.Dropdown(new UiLayoutRect(x, y, Math.Max(1f, width - 124f), RowHeight), "vf1-candidate", ctx, candidateDisplay, candidatePairs,
            selected => { if (selected != DisplayOnlyCurrentValue) ctx.Bindings.Invoke("select-fallback-candidate", selected); });
        if (UsKernelDraw.SelectionButton(new UiLayoutRect(x + width - 120f, y, 120f, RowHeight), ctx,
            UsKernelDraw.Keyed(ctx, "US.VF1.Create"), ctx.Theme, false, controlId: "create-fallback-table", itemKey: pending)
            && pending.Length > 0) ctx.Bindings.Invoke("create-fallback-table", pending);
        y += RowHeight + RowGap;

        if (races.Count == 0 || selectedRace.Length == 0) return;

        IReadOnlyList<FallbackEntryView> entries = ctx.Bindings.TryGet("fallback-entries", out IReadOnlyList<FallbackEntryView> en)
            ? en : Array.Empty<FallbackEntryView>();
        // UIR-004: the entry-list band claims the state-vocabulary help item once per pass;
        // the row loop below re-claims on hover through the same key.
        string selectedEntry = ctx.Bindings.TryGet("fallback-selected-entry", out string fs) ? fs ?? "" : "";
        foreach (FallbackEntryView entry in entries)
        {
            var rowRect = new UiLayoutRect(x, y, width, RowHeight - 2f);
            UsKernelDraw.HelpHover(rowRect, ctx, "us/fallback-editor/states");
            bool isSel = string.Equals(entry.ActionKey, selectedEntry, StringComparison.Ordinal);
            string stateWord = UsKernelDraw.Keyed(ctx, entry.State switch
            {
                FallbackEntryView.PlayerOverride => "US.VF1.State.Override",
                FallbackEntryView.Maintainer => "US.VF1.State.Maintainer",
                FallbackEntryView.NoSound => "US.VF1.State.NoSound",
                _ => "US.VF1.State.Unset",
            });
            string text = entry.ActionLabel + "   " + (entry.SoundLabel.Length > 0 ? entry.SoundLabel : "-") + "   " + stateWord;
            if (UsKernelDraw.SelectionButton(rowRect, ctx, text, ctx.Theme, isSel, controlId: "select-fallback-entry", itemKey: (selectedRace, entry.ActionKey)))
            {
                ctx.Bindings.Invoke("set-fallback-selection", new UsFallbackSelection(null, entry.ActionKey));
            }

            y += RowHeight;
        }

        y += RowGap;

        if (selectedEntry.Length > 0)
        {
            // Filter the complete Core inventory before paging; every matching sound remains reachable.
            y += UsBrowseControls.Draw(x, y, width, ctx, "sound", false, true);

            // Three distinct answers (r5): pick a sound = override; "Unset (no sound)" = the EXPLICIT
            // silent marker (the key stays present with the delete value); "Restore inheritance"
            // removes the key so the entry follows future shipped updates again. Explicit silence
            // and inheritance are different states and get different controls.
            FallbackEntryView selectedRow = default;
            foreach (FallbackEntryView entry in entries)
            {
                if (string.Equals(entry.ActionKey, selectedEntry, StringComparison.Ordinal)) { selectedRow = entry; break; }
            }

            IReadOnlyList<FilterOptionView> sounds = ctx.Bindings.GetOptions<FilterOptionView>("fallback-sound-options");
            var soundPairs = new List<KeyValuePair<string, string>>();
            foreach (FilterOptionView option in sounds) soundPairs.Add(new KeyValuePair<string, string>(option.DisplayName, option.Value));
            soundPairs.Add(new KeyValuePair<string, string>(UsKernelDraw.Keyed(ctx, "US.VF1.Unset"), ""));
            // The CURRENT reading is unambiguous per state: an override/shipped sound shows its own
            // defName (so the label resolves), the explicit marker shows "Unset (no sound)", and an
            // unset entry gets a display-only pair so it never masquerades as the marker.
            string currentSound = selectedRow.State switch
            {
                FallbackEntryView.Unset => DisplayOnlyCurrentValue,
                FallbackEntryView.NoSound => "",
                _ => selectedRow.SoundDefName ?? "",
            };
            if (selectedRow.State == FallbackEntryView.Unset)
                soundPairs.Add(new KeyValuePair<string, string>(UsKernelDraw.Keyed(ctx, "US.VF1.State.Unset"), DisplayOnlyCurrentValue));
            else if (currentSound.Length > 0 && !soundPairs.Exists(pair => pair.Value == currentSound))
            {
                soundPairs.Add(new KeyValuePair<string, string>(selectedRow.SoundLabel + "  " + currentSound, DisplayOnlyCurrentValue));
                currentSound = DisplayOnlyCurrentValue;
            }
            UsKernelDraw.Dropdown(new UiLayoutRect(x, y, Math.Max(1f, width - 150f - RowGap), RowHeight), "vf1-sound", ctx, currentSound, soundPairs,
                selected =>
                {
                    if (selected == DisplayOnlyCurrentValue) return; // re-picking the display-only unset row writes nothing
                    ctx.Bindings.Invoke("set-fallback-entry", new UsFallbackEntryWrite(selectedEntry, selected));
                });
            if (UsKernelDraw.SelectionButton(new UiLayoutRect(x + Math.Max(1f, width - 146f), y, 146f, RowHeight), ctx,
                    UsKernelDraw.Keyed(ctx, "US.VF1.RestoreInherit"), ctx.Theme, false, controlId: "restore-fallback-inheritance", itemKey: (selectedRace, selectedEntry)))
            {
                ctx.Bindings.Invoke("set-fallback-entry", new UsFallbackEntryWrite(selectedEntry, null));
            }

            y += RowHeight + RowGap;
        }

        float half = Math.Max(1f, (width - RowGap) / 2f);
        if (UsKernelDraw.SelectionButton(new UiLayoutRect(x, y, half, RowHeight), ctx, UsKernelDraw.Keyed(ctx, "US.VF1.RestoreDefault"), ctx.Theme, false, controlId: "restore-fallback-default", itemKey: selectedRace))
        {
            ctx.Bindings.Invoke("restore-fallback-default");
        }

        // VF1 (r5): deletion asks ONCE through the ordinary UiKit confirmation window - not the
        // inline two-click arm, and not the Remix double-swap (that ceremony belongs to a mode
        // change across pages, not to deleting one's own table).
        if (UsKernelDraw.SelectionButton(new UiLayoutRect(x + half + RowGap, y, half, RowHeight), ctx,
                UsKernelDraw.Keyed(ctx, "US.VF1.Delete"), ctx.Theme, false, controlId: "delete-fallback-table", itemKey: selectedRace))
        {
            IUiBindingAccess editorBindings = ctx.Bindings;
            string requestedRace = selectedRace;
            UsConfirmWindow.Open(
                UsKernelDraw.Keyed(ctx, "US.VF1.Delete"),
                UsKernelDraw.Keyed(ctx, "US.VF1.DeleteQuestion") + " " + selectedRace,
                UsKernelDraw.Keyed(ctx, "US.VF1.DeleteConfirm"),
                () => editorBindings.Invoke("delete-fallback-table", requestedRace));
        }
    }
}
