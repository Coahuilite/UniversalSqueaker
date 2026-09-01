using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US filter bar: row 1 = All/Enabled/Conflicts/Orphan-only segment chips writing the
/// typed "set-domain-filter" action; row 2 = Race / Xenotype / Author dropdowns (nested kernel
/// dropdown widgets over typed OptionsBind lists) writing "race-filter", "xenotype-filter" and
/// "set-pack-filter".
/// </summary>
public sealed class UsFilterBarWidget : UsSectionWidgetBase
{
    public const string KindName = "us/filter-bar";

    private const float RowHeight = UsFilterBarLayout.RowHeight;
    private const float Gap = UsFilterBarLayout.Gap;

    public override string Kind => KindName;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            KindName,
            () => new UsFilterBarWidget(),
            UsKernelWidgetRegistrar.SectionSchema);
    }

    public override void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<string>("race-filter", elementPath);
        bindings.ValidateValue<string>("xenotype-filter", elementPath);
        bindings.ValidateValue<string>("pack-filter", elementPath);
        bindings.ValidateValue<string>("search-text", elementPath);
        bindings.ValidateOptions<string>("race-filter-options", elementPath);
        bindings.ValidateOptions<string>("xenotype-filter-options", elementPath);
        bindings.ValidateOptions<string>("author-options", elementPath);
        bindings.ValidateValue<UiDomainFilter>("domain-filter", elementPath);
        bindings.ValidateAction<UsDomainFilterWrite>("set-domain-filter", elementPath);
        bindings.ValidateAction<string>("clear-pack-filters", elementPath);
    }

    protected override float FallbackHeight(UiWidgetContext ctx)
    {
        return UsFilterBarLayout.BodyHeight(BodyWidth(ctx));
    }

    protected override float MeasureBody(UiWidgetContext ctx)
    {
        return UsFilterBarLayout.BodyHeight(BodyWidth(ctx));
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        DrawDomainRow(new Rect(rect.x, rect.y, rect.width, RowHeight), ctx);

        float dropdownAreaTop = rect.y + RowHeight;
        float dropdownAreaHeight = rect.height - RowHeight;
        if (UsFilterBarLayout.DropdownsStack(rect.width))
        {
            DrawFilterDropdownRows(new Rect(rect.x, dropdownAreaTop, rect.width, dropdownAreaHeight), ctx);
        }
        else
        {
            DrawFilterDropdownRow(new Rect(rect.x, dropdownAreaTop, rect.width, RowHeight), ctx);
        }
    }

    private void DrawDomainRow(Rect rect, UiWidgetContext ctx)
    {
        UiDomainFilter domainFilter = ctx.Bindings.TryGet("domain-filter", out UiDomainFilter f) ? f : default;
        string race = ctx.Bindings.TryGet("race-filter", out string raceFilter) ? raceFilter : "";
        string xenotype = ctx.Bindings.TryGet("xenotype-filter", out string xenotypeFilter) ? xenotypeFilter : "";
        string packAuthor = ctx.Bindings.TryGet("pack-filter", out string a) ? a : "";
        string searchText = ctx.Bindings.TryGet("search-text", out string search) ? search : "";

        const int count = 4;
        float buttonWidth = Math.Max(1f, (rect.width - Gap * (count - 1)) / count);
        float x = rect.x;

        bool allActive = !domainFilter.EnabledOnly && !domainFilter.ConflictOnly && !domainFilter.OrphanOnly
            && race.Length == 0 && xenotype.Length == 0 && packAuthor.Length == 0 && searchText.Length == 0;
        if (UsKernelDraw.SelectionButton(new Rect(x, rect.y, buttonWidth, rect.height), "All", ctx.Theme, allActive))
        {
            ctx.Bindings.Invoke("clear-pack-filters", "");
        }
        x += buttonWidth + Gap;

        if (UsKernelDraw.SelectionButton(new Rect(x, rect.y, buttonWidth, rect.height), "Enabled only", ctx.Theme, domainFilter.EnabledOnly))
        {
            ctx.Bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.EnabledOnly, !domainFilter.EnabledOnly));
        }
        x += buttonWidth + Gap;

        if (UsKernelDraw.SelectionButton(new Rect(x, rect.y, buttonWidth, rect.height), "Conflicts", ctx.Theme, domainFilter.ConflictOnly))
        {
            ctx.Bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.ConflictOnly, !domainFilter.ConflictOnly));
        }
        x += buttonWidth + Gap;

        if (UsKernelDraw.SelectionButton(new Rect(x, rect.y, buttonWidth, rect.height), "Orphan only", ctx.Theme, domainFilter.OrphanOnly))
        {
            ctx.Bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.OrphanOnly, !domainFilter.OrphanOnly));
        }
    }

    private void DrawFilterDropdownRow(Rect rect, UiWidgetContext ctx)
    {
        const int count = 3;
        float dropdownWidth = Math.Max(1f, (rect.width - Gap * (count - 1)) / count);
        float x = rect.x;

        DrawNestedDropdown(
            new Rect(x, rect.y, dropdownWidth, rect.height),
            "race-filter",
            "Race",
            ctx);
        x += dropdownWidth + Gap;

        DrawNestedDropdown(
            new Rect(x, rect.y, dropdownWidth, rect.height),
            "xenotype-filter",
            "Xenotype",
            ctx);
        x += dropdownWidth + Gap;

        DrawNestedDropdown(
            new Rect(x, rect.y, dropdownWidth, rect.height),
            "pack-filter",
            "Author",
            ctx);
    }

    /// <summary>Narrow layout: three full-width dropdown rows so each keeps a usable hit area.</summary>
    private void DrawFilterDropdownRows(Rect rect, UiWidgetContext ctx)
    {
        float y = rect.y;
        DrawNestedDropdown(new Rect(rect.x, y, rect.width, RowHeight), "race-filter", "Race", ctx);
        y += RowHeight + Gap;
        DrawNestedDropdown(new Rect(rect.x, y, rect.width, RowHeight), "xenotype-filter", "Xenotype", ctx);
        y += RowHeight + Gap;
        DrawNestedDropdown(new Rect(rect.x, y, rect.width, RowHeight), "pack-filter", "Author", ctx);
    }

    private void DrawNestedDropdown(Rect rect, string elementId, string label, UiWidgetContext ctx)
    {
        string optionsKey = elementId switch
        {
            "race-filter" => "race-filter-options",
            "xenotype-filter" => "xenotype-filter-options",
            _ => "author-options"
        };

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Label"] = label,
            ["Bind"] = elementId,
            ["OptionsBind"] = optionsKey,
            ["Height"] = rect.height.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var spec = new UiElementSpec(elementId, DropdownWidget.Kind, attributes);
        var dropdown = new DropdownWidget();
        dropdown.Configure(spec);
        dropdown.Draw(rect, ctx);
    }
}
