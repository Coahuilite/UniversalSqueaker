using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Widgets;
using UnityEngine;
using Verse;
using KitUiCommand = FerriteLib.UiKit.UiCommand;

namespace UniversalSqueaker.UI;

/// <summary>
/// US filter bar for race/xenotype domains and VoicePack rows. Emits <see cref="UiCommandKind.SetDomainFilter"/>
/// and <see cref="UiCommandKind.SetPackFilter"/> only; it never touches settings or persistence.
/// </summary>
public sealed class FilterBarWidget : IWidget
{
    public const string Kind = "us/filter-bar";

    private const string Title = "Quick filters";
    private const float SingleRowHeight = 24f;
    private const float TwoRowHeight = 48f;
    private const float Gap = 4f;

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        // Packs filter area: row 1 = domain chips; row 2 = Race / Xenotype / Author dropdowns.
        float bodyHeight = TwoRowHeight;
        return UiGuard.MeasureOrFallback(
            () => UsCard.Measure(bodyHeight, ctx),
            UsCard.Measure(bodyHeight, ctx),
            Kind, "UniversalSqueaker");
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit),
            fallback => DrawVanilla(fallback, ctx, emit),
            Kind, "UniversalSqueaker");
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        UsCard.Draw(rect, Title, ctx, body => DrawFilterBody(body, ctx, emit));
    }

    private static void DrawFilterBody(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        UiDomainFilter domainFilter = ReadDomainFilter(ctx);
        UiPackFilter packFilter = ReadPackFilter(ctx);
        IReadOnlyList<string> authors = ReadAuthors(ctx);
        string raceFilter = ReadString(ctx, "RaceFilter");
        string xenotypeFilter = ReadString(ctx, "XenotypeFilter");
        IReadOnlyList<FilterOptionView> raceOptions = ReadFilterOptions(ctx, "RaceFilterOptions");
        IReadOnlyList<FilterOptionView> xenotypeOptions = ReadFilterOptions(ctx, "XenotypeFilterOptions");
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);

        Rect domainRow = new(rect.x, rect.y, rect.width, SingleRowHeight);
        DrawDomainRow(domainRow, domainFilter, packFilter, businessEmit);

        Rect filterRow = new(rect.x, rect.y + SingleRowHeight, rect.width, SingleRowHeight);
        DrawFilterDropdownRow(filterRow, raceFilter, xenotypeFilter, raceOptions, xenotypeOptions, authors, packFilter.Author, ctx, businessEmit);
    }

    private static void DrawDomainRow(
        Rect rect,
        UiDomainFilter domainFilter,
        UiPackFilter packFilter,
        Action<UiCommand> emit)
    {
        const int count = 4;
        float buttonWidth = Math.Max(1f, (rect.width - Gap * (count - 1)) / count);
        float x = rect.x;

        DrawSegmentButton(new Rect(x, rect.y, buttonWidth, rect.height),
            "All",
            !domainFilter.EnabledOnly && !domainFilter.ConflictOnly && !domainFilter.OrphanOnly
                && string.IsNullOrEmpty(packFilter.Author),
            () =>
            {
                emit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "EnabledOnly", flag: false));
                emit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "ConflictOnly", flag: false));
                emit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "OrphanOnly", flag: false));
                emit(new UiCommand(UiCommandKind.SetPackFilter, arg: "Author|", flag: false));
            });
        x += buttonWidth + Gap;

        DrawSegmentButton(new Rect(x, rect.y, buttonWidth, rect.height),
            "Enabled only",
            domainFilter.EnabledOnly,
            () => emit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "EnabledOnly", flag: !domainFilter.EnabledOnly)));
        x += buttonWidth + Gap;

        DrawSegmentButton(new Rect(x, rect.y, buttonWidth, rect.height),
            "Conflicts",
            domainFilter.ConflictOnly,
            () => emit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "ConflictOnly", flag: !domainFilter.ConflictOnly)));
        x += buttonWidth + Gap;

        DrawSegmentButton(new Rect(x, rect.y, buttonWidth, rect.height),
            "Orphan only",
            domainFilter.OrphanOnly,
            () => emit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "OrphanOnly", flag: !domainFilter.OrphanOnly)));
    }

    private static void DrawFilterDropdownRow(
        Rect rect,
        string raceFilter,
        string xenotypeFilter,
        IReadOnlyList<FilterOptionView> raceOptions,
        IReadOnlyList<FilterOptionView> xenotypeOptions,
        IReadOnlyList<string> authors,
        string currentAuthor,
        WidgetContext ctx,
        Action<UiCommand> emit)
    {
        const int count = 3;
        float dropdownWidth = Math.Max(1f, (rect.width - Gap * (count - 1)) / count);
        float x = rect.x;

        DrawFilterDropdown(
            new Rect(x, rect.y, dropdownWidth, rect.height),
            "race-filter",
            "Race",
            raceFilter,
            ToDropdownOptions(raceOptions),
            ctx,
            value => emit(new UiCommand(UiCommandKind.SetRaceFilter, arg: value)));
        x += dropdownWidth + Gap;

        DrawFilterDropdown(
            new Rect(x, rect.y, dropdownWidth, rect.height),
            "xenotype-filter",
            "Xenotype",
            xenotypeFilter,
            ToDropdownOptions(xenotypeOptions),
            ctx,
            value => emit(new UiCommand(UiCommandKind.SetXenotypeFilter, arg: value)));
        x += dropdownWidth + Gap;

        DrawAuthorDropdown(
            new Rect(x, rect.y, dropdownWidth, rect.height),
            currentAuthor,
            authors,
            ctx,
            emit);
    }

    private static void DrawFilterDropdown(
        Rect rect,
        string idSuffix,
        string label,
        string current,
        IReadOnlyList<KeyValuePair<string, string>> options,
        WidgetContext ctx,
        Action<string> onSelected)
    {
        if (options.Count == 0) return;

        string id = "filter-bar-" + idSuffix;
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Label"] = label,
            ["Bind"] = "Current",
            ["OptionsBind"] = "Options",
            ["EmitName"] = "Select",
            ["Height"] = rect.height.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var spec = new UiElementSpec(id, DropdownWidget.Kind, attributes);
        var view = new Dictionary<string, object?>
        {
            ["Current"] = current,
            ["Options"] = options
        };
        var dropdownCtx = new WidgetContext(ctx.Source, view, ctx.Metrics, ctx.State);
        var dropdown = new DropdownWidget();
        dropdown.Configure(spec);
        dropdown.Draw(rect, dropdownCtx, cmd =>
        {
            if (cmd.Name == "Select" && cmd.Payload is string selected)
                onSelected(selected);
        });
    }

    private static void DrawAuthorDropdown(
        Rect rect,
        string currentAuthor,
        IReadOnlyList<string> authors,
        WidgetContext ctx,
        Action<UiCommand> emit)
    {
        var options = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("All", "")
        };
        if (authors != null)
        {
            foreach (string author in authors)
            {
                if (string.IsNullOrEmpty(author)) continue;
                options.Add(new KeyValuePair<string, string>(author, author));
            }
        }

        DrawFilterDropdown(rect, "author", "Author", currentAuthor ?? "", options, ctx, value =>
        {
            emit(new UiCommand(
                UiCommandKind.SetPackFilter,
                arg: value.Length == 0 ? "Author|" : "Author|" + value,
                flag: value.Length > 0));
        });
    }

    private static List<KeyValuePair<string, string>> ToDropdownOptions(IReadOnlyList<FilterOptionView> options)
    {
        var result = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("All", "")
        };
        if (options == null) return result;
        foreach (FilterOptionView option in options)
        {
            result.Add(new KeyValuePair<string, string>(option.DisplayName, option.Value));
        }

        return result;
    }

    private static void DrawSegmentButton(Rect rect, string label, bool selected, Action onClick)
    {
        SelectionButton.Draw(rect, label, selected, font: UiFont.Tiny);
        UiInteract.Button(rect, UiLayer.Content, () => onClick?.Invoke());
    }

    private static void DrawVanilla(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        UiDomainFilter domainFilter = ReadDomainFilter(ctx);
        UiPackFilter packFilter = ReadPackFilter(ctx);
        IReadOnlyList<string> authors = ReadAuthors(ctx);

        float buttonWidth = Math.Max(1f, (rect.width - Gap * 4f) / 5f);
        float x = rect.x;
        if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "All"))
        {
            businessEmit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "EnabledOnly", flag: false));
            businessEmit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "ConflictOnly", flag: false));
            businessEmit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "OrphanOnly", flag: false));
            businessEmit(new UiCommand(UiCommandKind.SetPackFilter, arg: "Author|", flag: false));
        }
        x += buttonWidth + Gap;
        if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "Enabled"))
        {
            businessEmit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "EnabledOnly", flag: !domainFilter.EnabledOnly));
        }
        x += buttonWidth + Gap;
        if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "Conflicts"))
        {
            businessEmit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "ConflictOnly", flag: !domainFilter.ConflictOnly));
        }
        x += buttonWidth + Gap;
        if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "Orphan"))
        {
            businessEmit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "OrphanOnly", flag: !domainFilter.OrphanOnly));
        }
        x += buttonWidth + Gap;
        string authorLabel = "Author: " + (string.IsNullOrEmpty(packFilter.Author) ? "All" : packFilter.Author);
        if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), authorLabel))
        {
            string next = NextAuthor(authors, packFilter.Author);
            businessEmit(new UiCommand(
                UiCommandKind.SetPackFilter,
                arg: next.Length == 0 ? "Author|" : "Author|" + next,
                flag: next.Length > 0));
        }
    }

    private static string NextAuthor(IReadOnlyList<string> authors, string current)
    {
        if (authors == null || authors.Count == 0) return "";
        if (string.IsNullOrEmpty(current)) return authors[0];

        int index = -1;
        for (int i = 0; i < authors.Count; i++)
        {
            if (string.Equals(authors[i], current, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0) return authors[0];
        return index + 1 >= authors.Count ? "" : authors[index + 1];
    }

    private static UiDomainFilter ReadDomainFilter(WidgetContext ctx)
    {
        return ctx.TryGetViewValue("DomainFilter", out object? value) && value is UiDomainFilter filter
            ? filter
            : default;
    }

    private static UiPackFilter ReadPackFilter(WidgetContext ctx)
    {
        return ctx.TryGetViewValue("PackFilter", out object? value) && value is UiPackFilter filter
            ? filter
            : default;
    }

    private static IReadOnlyList<string> ReadAuthors(WidgetContext ctx)
    {
        return ctx.TryGetViewValue("Authors", out object? value) && value is IReadOnlyList<string> authors
            ? authors
            : Array.Empty<string>();
    }

    private static string ReadString(WidgetContext ctx, string key)
    {
        return ctx.TryGetViewValue(key, out object? value) && value is string text ? text : "";
    }

    private static IReadOnlyList<FilterOptionView> ReadFilterOptions(WidgetContext ctx, string key)
    {
        return ctx.TryGetViewValue(key, out object? value) && value is IReadOnlyList<FilterOptionView> options
            ? options
            : Array.Empty<FilterOptionView>();
    }
}
