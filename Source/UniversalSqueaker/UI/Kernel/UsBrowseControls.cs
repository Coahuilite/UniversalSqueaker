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
    internal static float Height(UiWidgetContext ctx, string id, bool filters)
    {
        var state = ctx.Bindings.Get<UsBrowseListState>(id + "-browse-state");
        var view = ctx.Bindings.Get<UsBrowseListView>(id + "-browse-view");
        return Step * (1 + (filters ? 3 : 0) + (filters && state.SourcesOpen ? 3 + view.Sources.Count : 0));
    }
    internal static float Draw(float x, float y, float width, UiWidgetContext ctx, string id, bool raceList, bool filters)
    {
        var state = ctx.Bindings.Get<UsBrowseListState>(id + "-browse-state");
        var view = ctx.Bindings.Get<UsBrowseListView>(id + "-browse-view");
        float start = y;
        void Command(string op) => ctx.Bindings.Invoke(id + "-browse", op);
        bool Button(float bx, float by, float bw, string text, string op, bool selected = false) =>
            UsKernelDraw.SelectionButton(new UiLayoutRect(bx, by, Math.Max(1, bw), 28), ctx,
                UsKernelDraw.Ellipsized(text, ctx, UiFont.Tiny, Math.Max(1, bw - 12)), ctx.Theme, selected,
                controlId: id + "-" + op, itemKey: op);
        if (filters)
        {
            UsKernelDraw.Label(ctx.Drawing, new UiLayoutRect(x, y, width, 28), UsKernelDraw.Keyed(ctx, "US.Browse.List." + id),
                ctx.Theme, ctx.Theme[UiPaletteRole.Foreground], UiFont.Small, UiTextAnchor.MiddleLeft); y += Step;
            float queryWidth = raceList ? Math.Max(80, width * .55f) : width;
            UsKernelDraw.LiveTextField(new UiLayoutRect(x, y, queryWidth, 28), ctx, id + "-browse-query");
            if (raceList)
            {
                var groupRect = new UiLayoutRect(x + queryWidth + 4, y, width - queryWidth - 4, 28);
                UsKernelDraw.HelpHover(groupRect, ctx, "us/race-board/group");
                if (state.Group == UsBoardBrowsePolicy.BoardGroup.Xenotypes) UsKernelDraw.HelpHover(groupRect, ctx, "us/race-board/xenodir");
                var groups = new List<KeyValuePair<string, string>>();
                foreach (UsBoardBrowsePolicy.BoardGroup group in Enum.GetValues(typeof(UsBoardBrowsePolicy.BoardGroup)))
                {
                    if (id != "directory" && (group == UsBoardBrowsePolicy.BoardGroup.Xenotypes || group == UsBoardBrowsePolicy.BoardGroup.Races)) continue;
                    string key = "US.Browse.Group." + (id != "directory" && group == UsBoardBrowsePolicy.BoardGroup.PlayerRaces ? UsBoardBrowsePolicy.BoardGroup.Races : group);
                    groups.Add(new KeyValuePair<string, string>(UsKernelDraw.Keyed(ctx, key), group.ToString()));
                }
                UsKernelDraw.Dropdown(groupRect, id + "-group", ctx,
                    state.Group.ToString(), groups, value => Command("group:" + value));
            }
            y += Step;
            string source = state.SourcePackageId == "" ? UsKernelDraw.Keyed(ctx, "US.Browse.AllSources") : view.SelectedSourceLabel + " · " + state.SourcePackageId;
            UsKernelDraw.HelpHover(new UiLayoutRect(x, y, width, 28), ctx, "us/race-board/filters");
            if (Button(x, y, width - 84, UsKernelDraw.Keyed(ctx, "US.Browse.Source") + ": " + source, "sources", state.SourcesOpen)) Command("sources");
            if (Button(x + width - 80, y, 80, UsKernelDraw.Keyed(ctx, "US.Browse.Clear"), "clear")) Command("clear");
            y += Step;
            if (state.SourcesOpen)
            {
                UsKernelDraw.LiveTextField(new UiLayoutRect(x, y, width, 28), ctx, id + "-source-query"); y += Step;
                if (Button(x, y, width, UsKernelDraw.Keyed(ctx, "US.Browse.AllSources"), "source:")) Command("source:"); y += Step;
                foreach (var item in view.Sources)
                {
                    if (Button(x, y, width, item.Label, "source:" + item.Value, state.SourcePackageId == item.Value)) Command("source:" + item.Value);
                    y += Step;
                }
                Pager(x, y, width, ctx, id, "sources-", view.SourcePage, view.SourcePages, view.SourceCount + " " + UsKernelDraw.Keyed(ctx, "US.Browse.Sources"));
                y += Step;
            }
        }
        string summary = string.Format(UsKernelDraw.Keyed(ctx, "US.Browse.Counts"), view.Filtered, view.Eligible);
        UsKernelDraw.HelpHover(new UiLayoutRect(x, y, width, 28), ctx, "us/race-board/counts");
        if (!filters) summary += " · " + UsKernelDraw.Keyed(ctx, "US.Browse.Group." + state.Group)
            + (state.SourcePackageId == "" ? "" : " · " + view.SelectedSourceLabel);
        Pager(x, y, width, ctx, id, "", view.Page, view.Pages, summary);
        return y + Step - start;
    }
    private static void Pager(float x, float y, float width, UiWidgetContext ctx, string id, string prefix, int page, int pages, string summary)
    {
        void Button(float bx, string label, string op)
        {
            if (UsKernelDraw.SelectionButton(new UiLayoutRect(bx, y, 64, 28), ctx, label, ctx.Theme, false, controlId: id + "-" + op, itemKey: op))
                ctx.Bindings.Invoke(id + "-browse", op);
        }
        Button(x, "‹", prefix + "prev"); Button(x + width - 64, "›", prefix + "next");
        UsKernelDraw.Label(ctx.Drawing, new UiLayoutRect(x + 68, y, Math.Max(1, width - 136), 28),
            UsKernelDraw.Ellipsized(summary + " · " + (page + 1) + "/" + pages, ctx, UiFont.Tiny, Math.Max(1, width - 144)),
            ctx.Theme, ctx.Theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.MiddleCenter);
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
    protected override float MeasureBody(UiWidgetContext ctx) => UsBrowseControls.Height(ctx, "directory", ctx.Bindings.Get<bool>("voice-filters-open"));
    protected override float FallbackHeight(UiWidgetContext ctx) => 30;
    protected override void DrawBody(UiLayoutRect rect, UiWidgetContext ctx) => UsBrowseControls.Draw(rect.X, rect.Y, rect.Width, ctx, "directory", true, ctx.Bindings.Get<bool>("voice-filters-open"));
}
