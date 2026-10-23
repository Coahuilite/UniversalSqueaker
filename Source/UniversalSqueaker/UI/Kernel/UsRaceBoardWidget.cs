using System;
using System.Collections.Generic;

using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// The Overview's all-loaded-race board (UIR-002/003, WO-US-UI-REFACTOR-20261014; layout re-cut by
/// CORRECTIONS-R1 topic 1): one read-only, state-first card per loaded race, drawn from facts the
/// thin adapter collected and the pure projection derived.
///
/// <para>
/// <b>Two axes, independently visible - never one compressed sentence.</b> The SUPPLY/SELECTION axis
/// reads as short fixed chips (packs enabled/total, xeno packs/total, fallback mapped, and the
/// reason chips: dormant, target-not-loaded, saved-choice-missing, unmapped fallback, player table,
/// no fallback). The US-USE axis is its own always-present chip: "packs + fallback" under
/// Fallback/Remix, the host's Vanilla/Disabled word otherwise (Disabled keeps every selection;
/// Vanilla consults only the final fallback). Row.State participates in the scan (dormant /
/// target-unload chips). Chips are fixed short strings and are NEVER ellipsized; only the race NAME
/// truncates (the hover help carries the full identity). When the chips cannot fit one line the row
/// reserves a second line - measure and draw walk the same chip metrics, so a wrapped row is taller
/// by design, never clipped. A row never promises playback and never mentions pawns.
/// </para>
///
/// <para>
/// <b>Quick enable names its exact target.</b> The button (only for the ONE unambiguous not-yet-
/// enabled identity) carries the pack display label - plus the xenotype label for a xeno-scoped
/// target - in its own caption, and presses the SAME <c>race-board-enable</c> typed write the row
/// switch uses. Ambiguous rows show no enable at all: navigation is the only honest action, and
/// neither exit can touch the mode. Both exits are the page's typed bindings; this is one consumer
/// kind over a runtime-variable row set, not a raw IMGUI bypass or a second state engine.
/// </para>
/// </summary>
public sealed class UsRaceBoardWidget : UsSectionWidgetBase
{
    public const string KindName = "us/race-board";

    // Row rhythm: shared constants; the only variable part is the reserved chip lines (measured).
    private const float RowTop = 4f;
    private const float TitleBand = 20f;
    private const float ChipGapY = 2f;
    private const float RowBottom = 6f;
    private const float RowGap = 2f;
    private const float SidePadding = 8f;
    private const float TextInset = 8f;

    // Chips: fixed short strings padded into measured bands; never truncated.
    private const float ChipPadX = 6f;
    private const float ChipSep = 6f;
    private const float ChipBandExtra = 4f;

    // Right action column: [enable?][open]. The row's navigation hit stops at the column.
    private const float OpenWidth = 64f;
    private const float EnableWidth = 200f;
    private const float ButtonGap = 4f;
    private const float ButtonBand = 22f;

    // Every player-facing string is a Keyed entry; row keys and binding names stay machine tokens.
    private const string KeyPacks = "US.Board.Packs";
    private const string KeyChipXeno = "US.Board.ChipXeno";
    private const string KeyChipFallback = "US.Board.ChipFallback";
    private const string KeyFallbackEmpty = "US.Board.StFallbackEmpty";
    private const string KeyFallbackPlayer = "US.Board.StFallbackPlayer";
    private const string KeyNoFallback = "US.Board.StNoFallback";
    private const string KeyNoSupply = "US.Board.NoSupply";
    private const string KeyDormant = "US.Board.StDormant";
    private const string KeyTargetUnloaded = "US.Board.StTargetUnloaded";
    private const string KeySavedMissing = "US.Board.StSavedMissing";
    private const string KeyAxisUsesPacks = "US.Board.AxisUses";
    private const string KeyOpen = "US.Board.Open";
    private const string KeyEnable = "US.Board.Enable";
    private const string KeyEnablePack = "US.Board.EnablePack";
    private const string KeyEnablePackTarget = "US.Board.EnablePackTarget";
    private const string KeyEmpty = "US.Board.Empty";
    // OVT-002: the identity chips every row carries (source + framework axes) and the xenotype
    // directory band. Fixed short strings like the rest - never ellipsized.
    private const string KeySrcCore = "US.Board.Src.Core";
    private const string KeySrcDlc = "US.Board.Src.Dlc";
    private const string KeySrcMod = "US.Board.Src.Mod";
    private const string KeySrcUnknown = "US.Board.Src.Unknown";
    private const string KeyChipHar = "US.Board.Filter.Har";
    private const string KeyXenoEmpty = "US.Board.XenoEmpty";

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsRaceBoardWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<UsRaceBoardView>("race-board-view", elementPath);
        bindings.ValidateAction<string>("race-board-xeno-open", elementPath);
        // The two exits. open = pure navigation intent (no settings write); enable = the exact
        // per-domain identity through the Packs page's own typed toggle write.
        bindings.ValidateAction<string>("race-board-open", elementPath);
        bindings.ValidateAction<UsPackToggle>("race-board-enable", elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return RowHeight(ctx, oneLine: true);
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        UsRaceBoardView view = View(ctx);
        float oneLine = RowHeight(ctx, oneLine: true) + RowGap;
        if (view.Rows.Count == 0 && view.Xenotypes.Count == 0)
        {
            return UsKernelDraw.LineHeightOf(ctx, UiFont.Tiny) + RowTop + RowBottom;
        }

        float width = Math.Max(1f, BodyWidth(ctx) - SidePadding * 2f - ActionsWidth());
        float total = 0f;
        for (int i = 0; i < view.Rows.Count; i++)
        {
            total += RowHeight(ctx, view.Rows[i], width) + RowGap;
        }

        // OVT-003: the xenotype directory band - one fixed line per loaded XenotypeDef (its own
        // chips are short by construction), preceded by a section line when races are also shown.
        if (view.XenotypeDirectoryExpanded && view.Xenotypes.Count > 0)
        {
            // The section line rides EXACTLY when the draw draws it (band index > 0, i.e. rows
            // precede the band) - PM geometry addendum: one condition for measure and draw.
            if (view.XenotypeBandIndex > 0) total += UsKernelDraw.LineHeightOf(ctx, UiFont.Tiny) + RowGap;
            total += view.Xenotypes.Count * oneLine;
        }

        return total - RowGap;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        if (TitleHidden)
        {
            DrawRows(rect, ctx);
            return;
        }

        DrawCard(rect, ctx, body => DrawRows(body, ctx));
    }

    private void DrawRows(Rect body, UiWidgetContext ctx)
    {
        UsRaceBoardView view = View(ctx);
        IReadOnlyList<RaceBoardRow> rows = view.Rows;
        if (rows.Count == 0 && view.Xenotypes.Count == 0)
        {
            UsKernelDraw.Label(
                new Rect(body.x, body.y, body.width, UsKernelDraw.LineHeightOf(ctx, UiFont.Tiny) + 2f),
                UsKernelDraw.Keyed(ctx, KeyEmpty),
                ctx,
                ctx.Theme[UiPaletteRole.SecondaryForeground],
                UiFont.Tiny);
            return;
        }

        float inner = Math.Max(1f, body.width - SidePadding * 2f);
        float chipsWidth = Math.Max(1f, inner - ActionsWidth() - TextInset - ButtonGap);
        float y = body.y;
        // CO review r1 item 7: DRAW order == the view's band index == the measure total (the total
        // is order-independent, so MeasureBody above needs no change): humanlike rows, then the
        // xenotype band, then creatures - never the band dumped behind every animal/mech/entity.
        int band = view.XenotypeDirectoryExpanded && view.Xenotypes.Count > 0
            ? Math.Min(view.XenotypeBandIndex, rows.Count)
            : -1;
        for (int i = 0; i < rows.Count; i++)
        {
            if (i == band)
            {
                y = DrawXenoBand(body, inner, y, view, ctx);
            }

            RaceBoardRow row = rows[i];
            float rowHeight = RowHeight(ctx, row, chipsWidth);
            var rowRect = new Rect(body.x + SidePadding, y, inner, rowHeight);
            DrawRow(rowRect, row, chipsWidth, ctx);
            y += rowHeight + RowGap;
        }

        if (band >= rows.Count)
        {
            DrawXenoBand(body, inner, y, view, ctx);
        }
    }

    /// <summary>The expanded xenotype directory band (section line + one row per loaded
    /// XenotypeDef). Returns the y after the band. Empty when the view carries no rows.</summary>
    private static float DrawXenoBand(Rect body, float inner, float y, UsRaceBoardView view, UiWidgetContext ctx)
    {
        if (view.Xenotypes.Count == 0) return y;
        if (view.XenotypeBandIndex > 0)
        {
            UsKernelDraw.Label(
                new Rect(body.x + SidePadding, y, inner, UsKernelDraw.LineHeightOf(ctx, UiFont.Tiny)),
                UsKernelDraw.Keyed(ctx, "US.Board.XenoDir"),
                ctx,
                ctx.Theme[UiPaletteRole.SecondaryForeground],
                UiFont.Tiny,
                singleLine: true);
            y += UsKernelDraw.LineHeightOf(ctx, UiFont.Tiny) + RowGap;
        }

        float xenoLine = RowHeightStatic(ctx);
        for (int i = 0; i < view.Xenotypes.Count; i++)
        {
            DrawXenoRow(new Rect(body.x + SidePadding, y, inner, xenoLine), view.Xenotypes[i], ctx);
            y += xenoLine + RowGap;
        }

        return y;
    }

    private static void DrawXenoRow(Rect rowRect, XenotypeBoardRow row, UiWidgetContext ctx)
    {
        bool hovered = UsKernelDraw.HelpHover(rowRect, ctx, "us/race-board/xenorow");
        UiSurfaceStyle plane = hovered ? ctx.Theme.HoverSurface : ctx.Theme.RaisedSurface;
        UiThemeDraw.Surface(rowRect, ctx.Theme, plane.Fill, plane.Border);

        // PM narrow-geometry addendum: the SAME actionsW/columnX that places the buttons bounds the
        // title AND the chips - with a unique quick-enable the action column is wider than OpenWidth
        // and a long name must never intrude on it (measure/draw/hit share one boundary).
        float xenoActionsW = row.QuickEnablePackKey.Length > 0 ? EnableWidth + ButtonGap + OpenWidth : OpenWidth;
        float xenoColumnX = rowRect.xMax - xenoActionsW - 2f;
        float labelWidth = Math.Max(1f, xenoColumnX - rowRect.x - TextInset - 4f);
        UsKernelDraw.Label(
            new Rect(rowRect.x + TextInset, rowRect.y + RowTop, labelWidth, TitleBand),
            UsKernelDraw.Ellipsized(row.Label, ctx, UiFont.Small, labelWidth),
            ctx,
            ctx.Theme[UiPaletteRole.Foreground],
            UiFont.Small,
            TextAnchor.MiddleLeft,
            singleLine: true);

        // Two fixed chips, drawn DIRECTLY (PM item 2: no per-GUI-line List allocation), bounded by
        // the same columnX. The source chip carries the REAL package name (PM item 3: display, not
        // only search) - the name ellipsizes into the chip budget, the word prefix never does.
        float chipLineHeight = UsKernelDraw.LineHeightOf(ctx, UiFont.Tiny) + ChipBandExtra;
        float chipX = rowRect.x + TextInset;
        float chipY = rowRect.y + RowTop + TitleBand + ChipGapY;
        float chipBudget = Math.Max(1f, xenoColumnX - chipX - 4f);
        string sourceChip = SourceChip(row.Source, row.SourcePackageName, row.SourcePackageId,
            ctx, chipBudget * 0.55f);
        float sourceW = ChipWidth(sourceChip, ctx, chipBudget);
        DrawChip(new Rect(chipX, chipY, sourceW, chipLineHeight), sourceChip, ctx);
        chipX += sourceW + ChipSep;
        string supplyChip = row.PackCount > 0
            ? Num2(ctx, KeyChipXeno, row.EnabledCount, row.PackCount)
            : UsKernelDraw.Keyed(ctx, KeyXenoEmpty);
        float supplyW = ChipWidth(supplyChip, ctx, Math.Max(1f, xenoColumnX - chipX - 2f));
        DrawChip(new Rect(chipX, chipY, supplyW, chipLineHeight), supplyChip, ctx);

        var bandRect = new Rect(xenoColumnX, rowRect.y + (rowRect.height - ButtonBand) / 2f, xenoActionsW, ButtonBand);
        float cursor = bandRect.x;
        if (row.QuickEnablePackKey.Length > 0)
        {
            var enableRect = new Rect(cursor, bandRect.y, EnableWidth, bandRect.height);
            UsKernelDraw.HelpHover(enableRect, ctx, "us/race-board/enable");
            string caption = UsKernelDraw.Ellipsized(
                string.Format(UsKernelDraw.Keyed(ctx, KeyEnablePack), row.QuickEnablePackLabel),
                ctx, UiFont.Tiny, EnableWidth - 16f);
            if (UsKernelDraw.SelectionButton(enableRect, ctx, caption, ctx.Theme, false))
            {
                ctx.Bindings.Invoke("race-board-enable", new UsPackToggle(
                    SqueakVoicePackScope.Xenotype, row.QuickEnableRaceDefName, row.DefName, row.QuickEnablePackKey, true));
            }

            cursor += EnableWidth + ButtonGap;
        }

        var openRect = new Rect(cursor, bandRect.y, OpenWidth, bandRect.height);
        if (UsKernelDraw.SelectionButton(openRect, ctx, UsKernelDraw.Keyed(ctx, KeyOpen), ctx.Theme, false))
        {
            ctx.Bindings.Invoke("race-board-xeno-open", row.DefName);
        }

        var hit = new Rect(rowRect.x + TextInset, rowRect.y, Math.Max(1f, bandRect.x - rowRect.x - TextInset - 4f), rowRect.height);
        if (UiNative.Button(hit, ctx))
        {
            ctx.Bindings.Invoke("race-board-xeno-open", row.DefName);
        }
    }

    private void DrawRow(Rect rowRect, RaceBoardRow row, float chipsWidth, UiWidgetContext ctx)
    {
        bool hovered = UsKernelDraw.HelpHover(rowRect, ctx, "us/race-board/row");
        UiSurfaceStyle plane = hovered ? ctx.Theme.HoverSurface : ctx.Theme.RaisedSurface;
        UiThemeDraw.Surface(rowRect, ctx.Theme, plane.Fill, plane.Border);

        // Title band: the name may ellipsize (the full identity rides the row's help entry).
        float labelWidth = Math.Max(1f, rowRect.width - ActionsWidth() - TextInset - ButtonGap);
        UsKernelDraw.Label(
            new Rect(rowRect.x + TextInset, rowRect.y + RowTop, labelWidth, TitleBand),
            UsKernelDraw.Ellipsized(row.Label, ctx, UiFont.Small, labelWidth),
            ctx,
            ctx.Theme[UiPaletteRole.Foreground],
            UiFont.Small,
            TextAnchor.MiddleLeft,
            singleLine: true);

        // Chip bands: fixed short strings, never ellipsized; wrapped lines were reserved by Measure.
        List<string> chips = Chips(row, ctx, chipsWidth);
        float chipLineHeight = UsKernelDraw.LineHeightOf(ctx, UiFont.Tiny) + ChipBandExtra;
        float chipY = rowRect.y + RowTop + TitleBand + ChipGapY;
        float cursorX = rowRect.x + TextInset;
        float lineRight = rowRect.x + TextInset + chipsWidth;
        for (int c = 0; c < chips.Count; c++)
        {
            float chipWidth = ChipWidth(chips[c], ctx, chipsWidth);
            if (cursorX + chipWidth > lineRight + 0.5f && cursorX > rowRect.x + TextInset + 0.5f)
            {
                chipY += chipLineHeight + ChipGapY;
                cursorX = rowRect.x + TextInset;
            }

            var chipRect = new Rect(cursorX, chipY, chipWidth, chipLineHeight);
            DrawChip(chipRect, chips[c], ctx);
            cursorX += chipWidth + ChipSep;
        }

        // Action column: [enable (names the EXACT pack, and target when xeno)][open].
        float actionsW = ActionsWidth(row);
        float columnX = rowRect.xMax - actionsW - 2f;
        var bandRect = new Rect(columnX, rowRect.y + (rowRect.height - ButtonBand) / 2f, actionsW, ButtonBand);

        float cursor = bandRect.x;
        if (row.QuickEnablePackKey.Length > 0)
        {
            var enableRect = new Rect(cursor, bandRect.y, EnableWidth, bandRect.height);
            string caption = EnableCaption(row, ctx, EnableWidth - 16f);
            UsKernelDraw.HelpHover(enableRect, ctx, "us/race-board/enable");
            if (UsKernelDraw.SelectionButton(enableRect, ctx, caption, ctx.Theme, false))
            {
                ctx.Bindings.Invoke("race-board-enable", new UsPackToggle(
                    row.QuickEnableScope == "Xenotype" ? SqueakVoicePackScope.Xenotype : SqueakVoicePackScope.Race,
                    row.RaceDefName, row.QuickEnableTarget, row.QuickEnablePackKey, true));
            }

            cursor += EnableWidth + ButtonGap;
        }

        var openRect = new Rect(cursor, bandRect.y, OpenWidth, bandRect.height);
        if (UsKernelDraw.SelectionButton(openRect, ctx, UsKernelDraw.Keyed(ctx, KeyOpen), ctx.Theme, false))
        {
            ctx.Bindings.Invoke("race-board-open", row.RaceDefName);
        }

        // The whole remaining band is the atomic jump too; it stops before the action column, so one
        // press can never mean two intents.
        var hit = new Rect(
            rowRect.x + TextInset,
            rowRect.y,
            Math.Max(1f, bandRect.x - rowRect.x - TextInset - 4f),
            rowRect.height);
        if (UiNative.Button(hit, ctx))
        {
            ctx.Bindings.Invoke("race-board-open", row.RaceDefName);
        }
    }

    private static void DrawChip(Rect chipRect, string text, UiWidgetContext ctx)
    {
        UiThemeDraw.Surface(chipRect, ctx.Theme, ctx.Theme.RaisedSurface.Fill, ctx.Theme[UiPaletteRole.Border]);
        UsKernelDraw.Label(
            new Rect(chipRect.x + ChipPadX, chipRect.y, Math.Max(1f, chipRect.width - ChipPadX * 2f), chipRect.height),
            text,
            ctx,
            ctx.Theme[UiPaletteRole.SecondaryForeground],
            UiFont.Tiny,
            TextAnchor.MiddleLeft,
            singleLine: true);
    }

    /// <summary>
    /// The row's chips in fixed order: SUPPLY axis first (packs / xeno / no-supply), then FALLBACK
    /// (mapped count | unmapped | none, plus the player-table marker), then REASONS (saved-missing
    /// with its count, dormant, target-not-loaded), and FINALLY the US-USE axis as its own chip -
    /// visible in EVERY mode, which is what the correction asked for.
    /// </summary>
    private static List<string> Chips(RaceBoardRow row, UiWidgetContext ctx, float chipsWidth)
    {
        var chips = new List<string>();
        // OVT-002: the identity axes ride fixed chips - source (Core/DLC/MOD/?) and the HAR
        // framework marker - SEPARATE facts, both visible on every row, a mechanoid-HAR keeps
        // both. Never inferred from the label, never a whitelist.
        chips.Add(SourceChip(row.Identity.Source, row.Identity.SourcePackageName, row.Identity.SourcePackageId,
            ctx, Math.Max(120f, chipsWidth * 0.62f)));
        if (row.Identity.IsHar) chips.Add(UsKernelDraw.Keyed(ctx, KeyChipHar));
        if (row.RacePackCount > 0)
        {
            chips.Add(Num2(ctx, KeyPacks, row.EnabledCount, row.RacePackCount));
        }
        else if (row.XenoPackCount > 0)
        {
            chips.Add(UsKernelDraw.Keyed(ctx, KeyNoSupply));
        }
        else
        {
            chips.Add(UsKernelDraw.Keyed(ctx, KeyNoSupply));
        }

        if (row.XenoPackCount > 0)
        {
            chips.Add(Num2(ctx, KeyChipXeno, row.XenoEnabledCount, row.XenoPackCount));
        }

        if (row.HasFinalFallback)
        {
            if (row.FallbackSoundCount > 0) chips.Add(Num1(ctx, KeyChipFallback, row.FallbackSoundCount));
            else chips.Add(UsKernelDraw.Keyed(ctx, KeyFallbackEmpty));
            if (row.FallbackIsPlayerTable) chips.Add(UsKernelDraw.Keyed(ctx, KeyFallbackPlayer));
        }
        else
        {
            chips.Add(UsKernelDraw.Keyed(ctx, KeyNoFallback));
        }

        int savedMissing = row.OrphanCount + row.XenoOrphanCount;
        if (savedMissing > 0) chips.Add(Num1(ctx, KeySavedMissing, savedMissing));
        if (row.State == SqueakVoicePackDomainState.Dormant) chips.Add(UsKernelDraw.Keyed(ctx, KeyDormant));
        if (row.State == SqueakVoicePackDomainState.TargetUnavailable || row.XenoTargetUnavailable)
        {
            chips.Add(UsKernelDraw.Keyed(ctx, KeyTargetUnloaded));
        }

        // US-USE axis: an independent, always-present chip.
        if (row.ModeUsesPacks)
        {
            chips.Add(UsKernelDraw.Keyed(ctx, KeyAxisUsesPacks));
        }
        else
        {
            string modeWord = ctx.Bindings.TryGet("race-board-mode-word", out string word) ? word ?? "" : "";
            chips.Add(modeWord.Length > 0 ? modeWord : UsKernelDraw.Keyed(ctx, "US.Board.ModeOff"));
        }

        return chips;
    }

    /// <summary>The enable caption: the exact pack (and xeno target) the press turns on. The pack
    /// NAME may ellipsize into the button - the hover help carries the whole identity - but the
    /// structure "Enable &lt;pack&gt; · &lt;target&gt;" is always there when a target exists.</summary>
    private static string EnableCaption(RaceBoardRow row, UiWidgetContext ctx, float maxWidth)
    {
        string caption = row.QuickEnableTarget.Length > 0
            ? string.Format(UsKernelDraw.Keyed(ctx, KeyEnablePackTarget), row.QuickEnablePackLabel, row.QuickEnableTargetLabel)
            : string.Format(UsKernelDraw.Keyed(ctx, KeyEnablePack), row.QuickEnablePackLabel);
        return UsKernelDraw.Ellipsized(caption, ctx, UiFont.Tiny, maxWidth);
    }

    private static float ChipWidth(string text, UiWidgetContext ctx, float maxWidth)
    {
        return Math.Min(maxWidth,
            ctx.Metrics.MeasureWidth(text ?? "", UiFont.Tiny) + ChipPadX * 2f);
    }

    private static float RowHeight(UiWidgetContext ctx, RaceBoardRow row, float chipsWidth)
    {
        return RowTop + TitleBand + ChipGapY + ChipLines(row, ctx, chipsWidth) * (ChipLineHeight(ctx) + ChipGapY) - ChipGapY + RowBottom;
    }

    private static float RowHeight(UiWidgetContext ctx, bool oneLine)
    {
        return RowTop + TitleBand + ChipGapY + ChipLineHeight(ctx) + RowBottom;
    }

    private static float RowHeightStatic(UiWidgetContext ctx) => RowHeight(ctx, oneLine: true);

    private static float ChipLineHeight(UiWidgetContext ctx)
    {
        return UsKernelDraw.LineHeightOf(ctx, UiFont.Tiny) + ChipBandExtra;
    }

    /// <summary>How many chip lines the row needs - the SAME greedy walk Measure reserves and Draw
    /// follows, over the SAME chip strings and metrics, so a wrapped row can never clip.</summary>
    private static int ChipLines(RaceBoardRow row, UiWidgetContext ctx, float chipsWidth)
    {
        List<string> chips = Chips(row, ctx, chipsWidth);
        int lines = 1;
        float x = 0f;
        for (int i = 0; i < chips.Count; i++)
        {
            float w = ChipWidth(chips[i], ctx, chipsWidth);
            if (x > 0f && x + ChipSep + w > chipsWidth + 0.5f)
            {
                lines++;
                x = w;
            }
            else
            {
                x = x > 0f ? x + ChipSep + w : w;
            }
        }

        return lines;
    }

    private static float ActionsWidth() => EnableWidth + ButtonGap + OpenWidth;

    private static float ActionsWidth(RaceBoardRow row)
        => row.QuickEnablePackKey.Length > 0 ? EnableWidth + ButtonGap + OpenWidth : OpenWidth;

    private static string Num1(UiWidgetContext ctx, string key, int a)
        => string.Format(UsKernelDraw.Keyed(ctx, key), a.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string Num2(UiWidgetContext ctx, string key, int a, int b)
        => string.Format(UsKernelDraw.Keyed(ctx, key),
            a.ToString(System.Globalization.CultureInfo.InvariantCulture),
            b.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>The source chip: the kind word PLUS the REAL package name (falling back to the
    /// package id) - PM source-display addendum: the row must SHOW the actual source, not only
    /// search it. The name ellipsizes into the chip budget; the word never does.</summary>
    private static string SourceChip(UsRaceSourceKind source, string packageName, string packageId,
        UiWidgetContext ctx, float nameBudget)
    {
        string word = source switch
        {
            UsRaceSourceKind.Core => UsKernelDraw.Keyed(ctx, KeySrcCore),
            UsRaceSourceKind.OfficialDlc => UsKernelDraw.Keyed(ctx, KeySrcDlc),
            UsRaceSourceKind.ThirdParty => UsKernelDraw.Keyed(ctx, KeySrcMod),
            _ => UsKernelDraw.Keyed(ctx, KeySrcUnknown),
        };
        string name = !string.IsNullOrEmpty(packageName) ? packageName : packageId;
        if (string.IsNullOrEmpty(name)) return word;
        return word + " \u00b7 " + UsKernelDraw.Ellipsized(name, ctx, UiFont.Tiny, Math.Max(24f, nameBudget));
    }

    private static UsRaceBoardView View(UiWidgetContext ctx)
    {
        return ctx.Bindings.TryGet("race-board-view", out UsRaceBoardView view) && view != null
            ? view
            : UsRaceBoardView.Empty;
    }
}
