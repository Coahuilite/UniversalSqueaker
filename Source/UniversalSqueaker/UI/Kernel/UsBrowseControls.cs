using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>Shared bounded controls over independent window-local list states.</summary>
internal static class UsBrowseControls
{
    internal const float Step = 30f;
    internal static void Validate(IUiBindingAccess bindings, string path, string id)
    {
        bindings.ValidateValue<UsBrowseListState>(id + "-browse-state", path);
        bindings.ValidateValue<UsBrowseListView>(id + "-browse-view", path);
        bindings.ValidateValue<string>(id + "-browse-query", path);
        bindings.ValidateValue<string>(id + "-source-query", path);
        bindings.ValidateAction<string>(id + "-browse", path);
    }
    internal static float Height(UiWidgetContext ctx, string id, bool filters, float width = 0)
        => Compose(0, 0, width > 1 ? width : ctx.ViewWidth, ctx, id, id != "sound", filters, false);
    internal static float Draw(float x, float y, float width, UiWidgetContext ctx, string id, bool raceList, bool filters)
        => Compose(x, y, width, ctx, id, raceList, filters, true);
    private static float Compose(float x, float y, float width, UiWidgetContext ctx, string id, bool raceList, bool filters, bool draw)
    {
        var state = ctx.Bindings.Get<UsBrowseListState>(id + "-browse-state");
        var view = ctx.Bindings.Get<UsBrowseListView>(id + "-browse-view");
        float start = y;
        void Command(string op) => ctx.Bindings.Invoke(id + "-browse", op);
        float Band(string text, float w) => UsKernelDraw.TextBandHeight(Math.Max(1, w - 16), ctx, text, UiFont.Small, 28);
        bool Button(float bx, float by, float bw, float bh, string text, string op, bool selected = false) => draw &&
            UsKernelDraw.SelectionButton(new UiLayoutRect(bx, by, Math.Max(1, bw), bh), ctx, text, ctx.Theme, selected,
                controlId: id + "-" + op, itemKey: op);
        void Label(string text)
        {
            float h = Band(text, width);
            if (draw) UsKernelDraw.Label(new UiLayoutRect(x, y, width, h), text, ctx, UiFont.Small);
            y += h + 2;
        }
        string source = state.SourcePackageId == "" ? UsKernelDraw.Keyed(ctx, "US.Browse.AllSources") : view.SelectedSourceLabel + " · " + state.SourcePackageId;
        string group = raceList ? UsKernelDraw.Keyed(ctx, UsBoardBrowsePolicy.GroupLabelKey(state.Group)) + " · " : "";
        string condition = group + source;
        float conditionHeight = Band(condition, width - 84);
        if (draw) UsKernelDraw.HelpHover(new UiLayoutRect(x, y, width, conditionHeight), ctx, "us/race-board/filters");
        if (draw) UsKernelDraw.Label(new UiLayoutRect(x, y, Math.Max(1, width - 84), conditionHeight), condition, ctx, UiFont.Small);
        if (Button(x + width - 80, y, 80, conditionHeight, UsKernelDraw.Keyed(ctx, "US.Browse.Clear"), "clear")) Command("clear");
        y += conditionHeight + 2;
        if (filters)
        {
            Label(UsKernelDraw.Keyed(ctx, id == "directory" ? "US.Browse.List.directory" : id == "table" ? "US.Browse.List.table"
                : id == "candidate" ? "US.Browse.List.candidate" : "US.Browse.List.sound"));
            float queryWidth = raceList ? Math.Max(1, width * .55f) : width;
            if (draw) UsKernelDraw.LiveTextField(new UiLayoutRect(x, y, queryWidth, 28), ctx, id + "-browse-query");
            if (raceList && draw)
            {
                var groups = new List<KeyValuePair<string, string>>();
                foreach (UsBoardBrowsePolicy.BoardGroup candidate in Enum.GetValues(typeof(UsBoardBrowsePolicy.BoardGroup)))
                {
                    if (id != "directory" && (candidate == UsBoardBrowsePolicy.BoardGroup.Xenotypes || candidate == UsBoardBrowsePolicy.BoardGroup.Races)) continue;
                    groups.Add(new KeyValuePair<string, string>(UsKernelDraw.Keyed(ctx, UsBoardBrowsePolicy.GroupLabelKey(candidate)), candidate.ToString()));
                }
                var groupRect = new UiLayoutRect(x + queryWidth + 4, y, Math.Max(1, width - queryWidth - 4), 28);
                UsKernelDraw.HelpHover(groupRect, ctx, "us/race-board/group");
                if (state.Group == UsBoardBrowsePolicy.BoardGroup.Xenotypes) UsKernelDraw.HelpHover(groupRect, ctx, "us/race-board/xenodir");
                UsKernelDraw.Dropdown(new UiLayoutRect(x + queryWidth + 4, y, Math.Max(1, width - queryWidth - 4), 28), id + "-group", ctx,
                    state.Group.ToString(), groups, value => Command("group:" + value));
            }
            y += Step;
            if (Button(x, y, width, 28, UsKernelDraw.Keyed(ctx, "US.Browse.Source"), "sources", state.SourcesOpen)) Command("sources");
            y += Step;
            if (state.SourcesOpen)
            {
                if (draw) UsKernelDraw.LiveTextField(new UiLayoutRect(x, y, Math.Max(1, width - 84), 28), ctx, id + "-source-query");
                if (Button(x + width - 80, y, 80, 28, UsKernelDraw.Keyed(ctx, "US.Browse.ClearSearch"), "sources-clear-search")) Command("sources-clear-search");
                y += Step;
                if (Button(x, y, width, 28, UsKernelDraw.Keyed(ctx, "US.Browse.AllSources"), "source:")) Command("source:");
                y += Step;
                foreach (var item in view.Sources)
                {
                    float h = Band(item.Label, width);
                    if (Button(x, y, width, h, item.Label, "source:" + item.Value, state.SourcePackageId == item.Value)) Command("source:" + item.Value);
                    y += h + 2;
                }
                if (view.Sources.Count == 0) Label(UsKernelDraw.Keyed(ctx, "US.Browse.NoMatches"));
                Pager("sources-", view.SourcePage, view.SourcePages, view.SourceCount + " " + UsKernelDraw.Keyed(ctx, "US.Browse.Sources"));
            }
        }
        if (view.Filtered == 0) Label(UsKernelDraw.Keyed(ctx, view.Eligible == 0 ? "US.Browse.NoCandidates" : "US.Browse.NoMatches"));
        Pager("", view.Page, view.Pages, string.Format(UsKernelDraw.Keyed(ctx, "US.Browse.Counts"), view.Filtered, view.Eligible));
        return y - start;

        void Pager(string prefix, int page, int pages, string summary)
        {
            string text = summary + " · " + (page + 1) + "/" + pages;
            float h = Band(text, width - 136);
            if (draw && prefix == "") UsKernelDraw.HelpHover(new UiLayoutRect(x, y, width, h), ctx, "us/race-board/counts");
            if (Button(x, y, 64, h, "‹", prefix + "prev")) Command(prefix + "prev");
            if (draw) UsKernelDraw.Label(new UiLayoutRect(x + 68, y, Math.Max(1, width - 136), h), text, ctx, UiFont.Small);
            if (Button(x + width - 64, y, 64, h, "›", prefix + "next")) Command(prefix + "next");
            y += h + 2;
        }
    }
}

public sealed class UsDirectoryFiltersWidget : UsSectionWidgetBase
{
    public const string KindName = "us/directory-filters";
    public override string Kind => KindName;
    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope,
        KindName, () => new UsDirectoryFiltersWidget(), UsKernelWidgetRegistrar.SectionSchema));
    public override void Validate(IUiBindingAccess bindings, string path)
    {
        UsBrowseControls.Validate(bindings, path, "directory");
        bindings.ValidateValue<bool>("voice-filters-open", path);
    }
    protected override float MeasureBody(UiWidgetContext ctx) => UsBrowseControls.Height(ctx, "directory", ctx.Bindings.Get<bool>("voice-filters-open"), BodyWidth(ctx));
    protected override float FallbackHeight(UiWidgetContext ctx) => 30;
    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx) => UsBrowseControls.Draw(rect.X, rect.Y, rect.Width, ctx, "directory", true, ctx.Bindings.Get<bool>("voice-filters-open"));
}

