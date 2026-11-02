using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>Bounded option browsing inside the pack query. Query text never adopts an option.</summary>
internal static class UsPackQueryControls
{
    private const float Gap = 4f;
    private static string LabelKey(string id) => id == "race" ? "US.Packs.Filter.Race"
        : id == "xeno" ? "US.Packs.Filter.Xenotype" : "US.Packs.Filter.Author";
    private static string ValueKey(string id) => id == "race" ? "race-filter" : id == "xeno" ? "xenotype-filter" : "pack-filter";
    private static string OptionsKey(string id) => id == "race" ? "race-filter-options" : id == "xeno" ? "xenotype-filter-options" : "author-options";
    private static UsBrowseListState State(UiWidgetContext ctx, string id) => ctx.Bindings.Get<UsBrowseListState>("pack-" + id + "-state");
    private static IReadOnlyList<FilterOptionView> Rows(UiWidgetContext ctx, string id, out int page, out int pages, out int count)
    {
        var state = State(ctx, id);
        var matches = ctx.Bindings.GetOptions<FilterOptionView>(OptionsKey(id))
            .Where(x => UsBrowseListView.Contains(x.DisplayName, state.Query) || UsBrowseListView.Contains(x.Value, state.Query)).ToArray();
        count = matches.Length;
        return UsBrowseListView.Slice(matches, state.Page, 8, out page, out pages);
    }
    private static float Band(UiWidgetContext ctx, string text, float width)
        => UsKernelDraw.TextBandHeight(Math.Max(1, width - 16), ctx, text, UiFont.Small, 28) + Gap;
    private static string Caption(UiWidgetContext ctx, string id)
    {
        string value = ctx.Bindings.Get<string>(ValueKey(id)) ?? "";
        string label = value == "" ? UsKernelDraw.Keyed(ctx, "US.Packs.Filter.All")
            : ctx.Bindings.GetOptions<FilterOptionView>(OptionsKey(id)).FirstOrDefault(x => x.Value == value).DisplayName ?? value;
        return UsKernelDraw.Keyed(ctx, LabelKey(id)) + ": " + label;
    }
    internal static void Validate(IUiBindingAccess bindings, string path)
    {
        foreach (string id in new[] { "race", "xeno", "author" })
        {
            bindings.ValidateValue<UsBrowseListState>("pack-" + id + "-state", path);
            bindings.ValidateValue<string>("pack-" + id + "-query", path);
            bindings.ValidateAction<string>("pack-" + id + "-options", path);
            bindings.ValidateOptions<FilterOptionView>(OptionsKey(id), path);
        }
    }
    internal static float Height(UiWidgetContext ctx, float width)
    {
        float height = 0;
        foreach (string id in new[] { "race", "xeno", "author" })
        {
            height += Band(ctx, Caption(ctx, id), width - 84);
            if (!State(ctx, id).SourcesOpen) continue;
            var rows = Rows(ctx, id, out _, out _, out _);
            height += 64;
            foreach (var row in rows) height += Band(ctx, row.DisplayName, width);
            if (rows.Count == 0) height += Band(ctx, UsKernelDraw.Keyed(ctx, "US.Browse.NoMatches"), width);
        }
        return height;
    }
    internal static void Draw(float x, float y, float width, UiWidgetContext ctx)
    {
        foreach (string id in new[] { "race", "xeno", "author" })
        {
            var state = State(ctx, id);
            void Command(string op) => ctx.Bindings.Invoke("pack-" + id + "-options", op);
            float height = Band(ctx, Caption(ctx, id), width - 84);
            UsKernelDraw.HelpHover(new UiLayoutRect(x, y, width, height - Gap), ctx,
                id == "race" ? "us/filter-bar/race" : id == "xeno" ? "us/filter-bar/xenotype" : "us/filter-bar/author");
            if (UsKernelDraw.SelectionButton(new UiLayoutRect(x, y, Math.Max(1, width - 84), height - Gap), ctx,
                Caption(ctx, id), ctx.Theme, state.SourcesOpen, controlId: "pack-" + id + "-open")) Command("toggle");
            if (UsKernelDraw.SelectionButton(new UiLayoutRect(x + width - 80, y, 80, height - Gap), ctx,
                UsKernelDraw.Keyed(ctx, "US.Browse.Clear"), ctx.Theme, false, controlId: "pack-" + id + "-clear"))
                ctx.Bindings.Set(ValueKey(id), "");
            y += height;
            if (!state.SourcesOpen) continue;
            UsKernelDraw.LiveTextField(new UiLayoutRect(x, y, Math.Max(1, width - 84), 28), ctx, "pack-" + id + "-query");
            if (UsKernelDraw.SelectionButton(new UiLayoutRect(x + width - 80, y, 80, 28), ctx,
                UsKernelDraw.Keyed(ctx, "US.Browse.ClearSearch"), ctx.Theme, false, controlId: "pack-" + id + "-query-clear")) Command("clear");
            y += 32;
            var rows = Rows(ctx, id, out int page, out int pages, out int count);
            foreach (var row in rows)
            {
                height = Band(ctx, row.DisplayName, width);
                if (UsKernelDraw.SelectionButton(new UiLayoutRect(x, y, width, height - Gap), ctx, row.DisplayName, ctx.Theme,
                    ctx.Bindings.Get<string>(ValueKey(id)) == row.Value, controlId: "pack-" + id + "-option", itemKey: row.Value))
                {
                    ctx.Bindings.Set(ValueKey(id), row.Value);
                    Command("close");
                }
                y += height;
            }
            if (rows.Count == 0)
            {
                string empty = UsKernelDraw.Keyed(ctx, "US.Browse.NoMatches");
                height = Band(ctx, empty, width);
                UsKernelDraw.Label(new UiLayoutRect(x, y, width, height - Gap), empty, ctx, UiFont.Small);
                y += height;
            }
            if (UsKernelDraw.SelectionButton(new UiLayoutRect(x, y, 60, 28), ctx, "‹", ctx.Theme, false, controlId: "pack-" + id + "-prev")) Command("prev");
            string summary = string.Format(UsKernelDraw.Keyed(ctx, "US.Browse.OptionsCount"), count, page + 1, pages);
            UsKernelDraw.Label(new UiLayoutRect(x + 64, y, Math.Max(1, width - 128), 28), summary, ctx, UiFont.Small);
            if (UsKernelDraw.SelectionButton(new UiLayoutRect(x + width - 60, y, 60, 28), ctx, "›", ctx.Theme, false, controlId: "pack-" + id + "-next")) Command("next");
            y += 32;
        }
    }
}
