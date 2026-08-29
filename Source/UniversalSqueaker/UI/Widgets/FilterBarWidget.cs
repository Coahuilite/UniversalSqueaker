using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
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

    private const float SingleRowHeight = 24f;
    private const float TwoRowHeight = 48f;
    private const float Gap = 4f;
    private const float NarrowWidth = 320f;

    private UiElementSpec? _spec;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        float height = ctx.ViewWidth < NarrowWidth ? TwoRowHeight : SingleRowHeight;
        return UsGuard.MeasureOrFallback(() => height, height, Kind);
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UsGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit),
            fallback => DrawVanilla(fallback, ctx, emit),
            Kind);
    }

    private static void DrawCore(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        UiDomainFilter domainFilter = ReadDomainFilter(ctx);
        UiPackFilter packFilter = ReadPackFilter(ctx);
        IReadOnlyList<string> authors = ReadAuthors(ctx);
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);

        if (rect.width < NarrowWidth)
        {
            DrawRow(rect, domainFilter, packFilter, authors, businessEmit, includeAuthor: false);
            Rect authorRect = new(rect.x, rect.y + SingleRowHeight, rect.width, SingleRowHeight);
            DrawAuthorButton(authorRect, authors, packFilter.Author, businessEmit);
            return;
        }

        DrawRow(rect, domainFilter, packFilter, authors, businessEmit, includeAuthor: true);
    }

    private static void DrawRow(
        Rect rect,
        UiDomainFilter domainFilter,
        UiPackFilter packFilter,
        IReadOnlyList<string> authors,
        Action<UiCommand> emit,
        bool includeAuthor)
    {
        int count = includeAuthor ? 5 : 4;
        float buttonWidth = (rect.width - Gap * (count - 1)) / count;
        float x = rect.x;

        DrawSegmentButton(new Rect(x, rect.y, buttonWidth, rect.height),
            "All",
            !domainFilter.EnabledOnly && !domainFilter.ConflictOnly && !domainFilter.OrphanOnly,
            () =>
            {
                emit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "EnabledOnly", flag: false));
                emit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "ConflictOnly", flag: false));
                emit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "OrphanOnly", flag: false));
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
        x += buttonWidth + Gap;

        if (includeAuthor)
        {
            DrawAuthorButton(new Rect(x, rect.y, buttonWidth, rect.height), authors, packFilter.Author, emit);
        }
    }

    private static void DrawAuthorButton(Rect rect, IReadOnlyList<string> authors, string currentAuthor, Action<UiCommand> emit)
    {
        string label = "Author: " + (string.IsNullOrEmpty(currentAuthor) ? "All" : currentAuthor);
        bool hasAuthors = authors != null && authors.Count > 0;
        UsSurface.DrawSegment(rect, label, !string.IsNullOrEmpty(currentAuthor));

        if (authors != null && hasAuthors && Widgets.ButtonInvisible(rect))
        {
            string next = NextAuthor(authors, currentAuthor);
            emit(new UiCommand(
                UiCommandKind.SetPackFilter,
                arg: next.Length == 0 ? "Author|" : "Author|" + next,
                flag: next.Length > 0));
        }
    }

    private static void DrawSegmentButton(Rect rect, string label, bool selected, Action onClick)
    {
        UsSurface.DrawSegment(rect, label, selected);
        if (Widgets.ButtonInvisible(rect))
        {
            onClick?.Invoke();
        }
    }

    private static void DrawVanilla(Rect rect, WidgetContext ctx, Action<KitUiCommand> emit)
    {
        Action<UiCommand> businessEmit = UsWidgetCommandAdapter.For(emit);
        UiDomainFilter domainFilter = ReadDomainFilter(ctx);
        UiPackFilter packFilter = ReadPackFilter(ctx);
        IReadOnlyList<string> authors = ReadAuthors(ctx);

        float buttonWidth = (rect.width - Gap * 4f) / 5f;
        float x = rect.x;
        if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "All"))
        {
            businessEmit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "EnabledOnly", flag: false));
            businessEmit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "ConflictOnly", flag: false));
            businessEmit(new UiCommand(UiCommandKind.SetDomainFilter, arg: "OrphanOnly", flag: false));
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
}
