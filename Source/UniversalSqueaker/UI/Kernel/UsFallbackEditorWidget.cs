using System;
using System.Collections.Generic;

using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// The final native fallback table editor (UIR-004, WO-US-UI-REFACTOR-20261014). Extracted VERBATIM in
/// behaviour from the tuning tree's third area when the fallback moved to the audio-packs page - the
/// last audible answer belongs beside the packs it falls back from - so the editor and the routing
/// still read ONE answer from the live store.
///
/// <para>
/// The race picker over the supported tables (maintainer ∪ player), the closed 17-key entry list with
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
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsFallbackEditorWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
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

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (TitleHidden)
        {
            DrawEditor(rect.y, rect.x, rect.width, ctx);
            return;
        }

        DrawCard(rect, ctx, body => DrawEditor(body.y, body.x, body.width, ctx));
    }

    private static float EditorHeight(UiWidgetContext ctx, float width)
    {
        // Mirrors DrawEditor line for line: race line + status line + create line are drawn for every
        // shape; the entry list, the SOUND QUERY field, the picker row and the button row only when a
        // race is selected. A measure that forgets a drawn row clips it.
        // UIR-004: the status line is the one band whose TEXT is a full sentence (the no-tables and
        // restart notices), so its height is wrap-aware - measured through the same seam and font that
        // DrawEditor uses, never a fixed band, because the card page fit audit runs this editor at the
        // game's minimum box in both shipped languages.
        float height = 2 * (RowHeight + RowGap) + StatusBandHeight(ctx, width) + RowGap;
        string selectedRace = ctx.Bindings.TryGet("fallback-selected-race", out string sr) ? sr ?? "" : "";
        if (selectedRace.Length > 0)
        {
            height += UniversalSqueaker.Kernel.BuiltInActionKeys.All.Count * RowHeight + RowGap;
            height += 2 * (RowHeight + RowGap) + RowHeight + RowGap; // query + picker + buttons
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
        return status.Length > 0 ? UsKernelDraw.Keyed(ctx, status)
            : races.Count == 0 ? UsKernelDraw.Keyed(ctx, "US.VF1.NoTable") : "";
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
        foreach (FallbackRaceView race in races)
        {
            racePairs.Add(new KeyValuePair<string, string>(
                race.Label + "  " + race.DefName + (race.IsPlayerTable ? "  (*)" : ""), race.DefName));
        }

        UsKernelDraw.Label(new Rect(x, y, 60f, RowHeight), UsKernelDraw.Keyed(ctx, "US.VF1.Race"),
            ctx.Theme, ctx.Theme[UiPaletteRole.Foreground], UiFont.Tiny, TextAnchor.MiddleLeft);
        UsKernelDraw.Dropdown(new Rect(x + 64f, y, Math.Max(1f, width - 64f), RowHeight), "vf1-race", ctx, selectedRace, racePairs,
            selected => ctx.Bindings.Invoke("set-fallback-selection", new UsFallbackSelection(selected, null)));
        y += RowHeight + RowGap;

        // UIR-004: the status sentence keeps the wrap-aware band the measure reserved - the same
        // StatusText/StatusBandHeight outlet, so the drawn band can never disagree with the reserved one.
        string statusText = StatusText(ctx);
        float statusBand = StatusBandHeight(ctx, width);
        UsKernelDraw.Label(new Rect(x, y, width, statusBand),
            statusText,
            ctx.Theme, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, TextAnchor.UpperLeft);
        y += statusBand + RowGap;

        // Create row: query + candidate dropdown (candidates = loaded pawn races, so a race with no
        // VoicePack at all is a legal new table).
        UiValueState queryState = ctx.State("vf1-new-race-query");
        string query = ctx.Bindings.TryGet("fallback-new-race-query", out string q) ? q ?? "" : "";
        if (!queryState.Focused) queryState.EditText = query;
        UsKernelDraw.Label(new Rect(x, y, 60f, RowHeight), UsKernelDraw.Keyed(ctx, "US.VF1.Create"),
            ctx.Theme, ctx.Theme[UiPaletteRole.Foreground], UiFont.Tiny, TextAnchor.MiddleLeft);
        string typed = UiNative.TextField(new Rect(x + 64f, y, 120f, RowHeight), queryState.EditText);
        if (typed != queryState.EditText)
        {
            queryState.EditText = typed;
            ctx.Bindings.Set("fallback-new-race-query", typed);
        }

        IReadOnlyList<FilterOptionView> candidates = ctx.Bindings.GetOptions<FilterOptionView>("fallback-candidate-options");
        var candidatePairs = new List<KeyValuePair<string, string>>();
        foreach (FilterOptionView option in candidates) candidatePairs.Add(new KeyValuePair<string, string>(option.DisplayName, option.Value));
        UsKernelDraw.Dropdown(new Rect(x + 190f, y, Math.Max(1f, width - 190f), RowHeight), "vf1-candidate", ctx, "", candidatePairs,
            selected => ctx.Bindings.Invoke("create-fallback-table", selected));
        y += RowHeight + RowGap;

        if (races.Count == 0 || selectedRace.Length == 0) return;

        IReadOnlyList<FallbackEntryView> entries = ctx.Bindings.TryGet("fallback-entries", out IReadOnlyList<FallbackEntryView> en)
            ? en : Array.Empty<FallbackEntryView>();
        // UIR-004: the entry-list band claims the state-vocabulary help item once per pass;
        // the row loop below re-claims on hover through the same key.
                string selectedEntry = ctx.Bindings.TryGet("fallback-selected-entry", out string fs) ? fs ?? "" : "";
        foreach (FallbackEntryView entry in entries)
        {
            var rowRect = new Rect(x, y, width, RowHeight - 2f);
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
            if (UsKernelDraw.SelectionButton(rowRect, ctx, text, ctx.Theme, isSel))
            {
                ctx.Bindings.Invoke("set-fallback-selection", new UsFallbackSelection(null, entry.ActionKey));
            }

            y += RowHeight;
        }

        y += RowGap;

        if (selectedEntry.Length > 0)
        {
            // The sound query is the user's REAL search input (r5 wiring gap): the candidate list is
            // display-capped at 40 AFTER filtering, so typing a name or defName is how any legal Core
            // sound becomes reachable. The value binding already existed on the Host; this is the
            // control, with the same focus seam as the race-query field.
            UiValueState soundQueryState = ctx.State("vf1-sound-query");
            string soundQuery = ctx.Bindings.TryGet("fallback-sound-query", out string sq) ? sq ?? "" : "";
            if (!soundQueryState.Focused) soundQueryState.EditText = soundQuery;
            UsKernelDraw.Label(new Rect(x, y, 60f, RowHeight), UsKernelDraw.Keyed(ctx, "US.VF1.SoundSearch"),
                ctx.Theme, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, TextAnchor.MiddleLeft);
            string typedSoundQuery = UiNative.TextField(new Rect(x + 64f, y, Math.Max(1f, width - 64f), RowHeight), soundQueryState.EditText);
            if (typedSoundQuery != soundQueryState.EditText)
            {
                soundQueryState.EditText = typedSoundQuery;
                ctx.Bindings.Set("fallback-sound-query", typedSoundQuery);
            }

            y += RowHeight + RowGap;

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
            UsKernelDraw.Dropdown(new Rect(x, y, Math.Max(1f, width - 150f - RowGap), RowHeight), "vf1-sound", ctx, currentSound, soundPairs,
                selected =>
                {
                    if (selected == DisplayOnlyCurrentValue) return; // re-picking the display-only unset row writes nothing
                    ctx.Bindings.Invoke("set-fallback-entry", new UsFallbackEntryWrite(selectedEntry, selected));
                });
            if (UsKernelDraw.SelectionButton(new Rect(x + Math.Max(1f, width - 146f), y, 146f, RowHeight), ctx,
                    UsKernelDraw.Keyed(ctx, "US.VF1.RestoreInherit"), ctx.Theme, false))
            {
                ctx.Bindings.Invoke("set-fallback-entry", new UsFallbackEntryWrite(selectedEntry, null));
            }

            y += RowHeight + RowGap;
        }

        float half = Math.Max(1f, (width - RowGap) / 2f);
        if (UsKernelDraw.SelectionButton(new Rect(x, y, half, RowHeight), ctx, UsKernelDraw.Keyed(ctx, "US.VF1.RestoreDefault"), ctx.Theme, false))
        {
            ctx.Bindings.Invoke("restore-fallback-default");
        }

        // VF1 (r5): deletion asks ONCE through the ordinary UiKit confirmation window - not the
        // inline two-click arm, and not the Remix double-swap (that ceremony belongs to a mode
        // change across pages, not to deleting one's own table).
        if (UsKernelDraw.SelectionButton(new Rect(x + half + RowGap, y, half, RowHeight), ctx,
                UsKernelDraw.Keyed(ctx, "US.VF1.Delete"), ctx.Theme, false))
        {
            IUiBindings editorBindings = ctx.Bindings;
            string requestedRace = selectedRace;
            UsConfirmWindow.Open(
                UsKernelDraw.Keyed(ctx, "US.VF1.Delete"),
                UsKernelDraw.Keyed(ctx, "US.VF1.DeleteQuestion") + " " + selectedRace,
                UsKernelDraw.Keyed(ctx, "US.VF1.DeleteConfirm"),
                () => editorBindings.Invoke("delete-fallback-table", requestedRace));
        }
    }
}
