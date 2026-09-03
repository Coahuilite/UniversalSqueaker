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

    // Keyed display labels. The machine tokens these chips/dropdowns write ("race-filter",
    // "xenotype-filter", "pack-filter", "search-text" and the SqueakDomainFilterKind values) stay
    // untranslated; only what the player reads goes through the translation seam.
    private const string KeyChipAll = "US.Packs.Filter.All";
    private const string KeyChipEnabledOnly = "US.Packs.Filter.EnabledOnly";
    private const string KeyChipConflicts = "US.Packs.Filter.Conflicts";
    private const string KeyChipOrphanOnly = "US.Packs.Filter.OrphanOnly";
    private const string KeyLabelRace = "US.Packs.Filter.Race";
    private const string KeyLabelXenotype = "US.Packs.Filter.Xenotype";
    private const string KeyLabelAuthor = "US.Packs.Filter.Author";

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
        float bodyWidth = BodyWidth(ctx);
        float domain = DomainRowHeight(ctx, bodyWidth);
        float body = UsFilterBarLayout.BodyHeight(bodyWidth, domain);
        TraceLayout("measure", bodyWidth, body, domain);
        return body;
    }

    protected override void DrawBody(Rect rect, UiWidgetContext ctx)
    {
        DrawCard(rect, ctx, body => DrawContent(body, ctx));
    }

    private void DrawContent(Rect rect, UiWidgetContext ctx)
    {
        float domainHeight = DomainRowHeight(ctx, rect.width);
        TraceLayout("draw", rect.width, rect.height, domainHeight);
        DrawDomainRow(new Rect(rect.x, rect.y, rect.width, domainHeight), ctx);

        float dropdownAreaTop = rect.y + domainHeight;
        float dropdownAreaHeight = rect.height - domainHeight;
        if (UsFilterBarLayout.DropdownsStack(rect.width))
        {
            DrawFilterDropdownRows(new Rect(rect.x, dropdownAreaTop, rect.width, dropdownAreaHeight), ctx);
        }
        else
        {
            DrawFilterDropdownRow(new Rect(rect.x, dropdownAreaTop, rect.width, RowHeight), ctx);
        }
    }

    /// <summary>
    /// Height of the chip row: the tallest chip label as it actually wraps at the chip width each chip
    /// is drawn at, never below the layout default. Measure and Draw call this once each with the same
    /// body width, so the dropdown area below starts where the chips end.
    /// </summary>
    private float DomainRowHeight(UiWidgetContext ctx, float bodyWidth)
    {
        const int count = 4;
        float chipWidth = Math.Max(1f, (bodyWidth - Gap * (count - 1)) / count);
        float band = RowHeight;
        string[] keys = { KeyChipAll, KeyChipEnabledOnly, KeyChipConflicts, KeyChipOrphanOnly };
        foreach (string key in keys)
        {
            band = Math.Max(band, ctx.Metrics.MeasureText(ctx.Translation.Translate(key), UiFont.Tiny, chipWidth));
        }

        return band;
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
        if (UsKernelDraw.SelectionButton(new Rect(x, rect.y, buttonWidth, rect.height), ctx.Translation.Translate(KeyChipAll), ctx.Theme, allActive))
        {
            ctx.Bindings.Invoke("clear-pack-filters", "");
        }
        x += buttonWidth + Gap;

        if (UsKernelDraw.SelectionButton(new Rect(x, rect.y, buttonWidth, rect.height), ctx.Translation.Translate(KeyChipEnabledOnly), ctx.Theme, domainFilter.EnabledOnly))
        {
            ctx.Bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.EnabledOnly, !domainFilter.EnabledOnly));
        }
        x += buttonWidth + Gap;

        if (UsKernelDraw.SelectionButton(new Rect(x, rect.y, buttonWidth, rect.height), ctx.Translation.Translate(KeyChipConflicts), ctx.Theme, domainFilter.ConflictOnly))
        {
            ctx.Bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.ConflictOnly, !domainFilter.ConflictOnly));
        }
        x += buttonWidth + Gap;

        if (UsKernelDraw.SelectionButton(new Rect(x, rect.y, buttonWidth, rect.height), ctx.Translation.Translate(KeyChipOrphanOnly), ctx.Theme, domainFilter.OrphanOnly))
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
            KeyLabelRace,
            ctx);
        x += dropdownWidth + Gap;

        DrawNestedDropdown(
            new Rect(x, rect.y, dropdownWidth, rect.height),
            "xenotype-filter",
            KeyLabelXenotype,
            ctx);
        x += dropdownWidth + Gap;

        DrawNestedDropdown(
            new Rect(x, rect.y, dropdownWidth, rect.height),
            "pack-filter",
            KeyLabelAuthor,
            ctx);
    }

    /// <summary>Narrow layout: three full-width dropdown rows so each keeps a usable hit area.</summary>
    private void DrawFilterDropdownRows(Rect rect, UiWidgetContext ctx)
    {
        float y = rect.y;
        DrawNestedDropdown(new Rect(rect.x, y, rect.width, RowHeight), "race-filter", KeyLabelRace, ctx);
        y += RowHeight + Gap;
        DrawNestedDropdown(new Rect(rect.x, y, rect.width, RowHeight), "xenotype-filter", KeyLabelXenotype, ctx);
        y += RowHeight + Gap;
        DrawNestedDropdown(new Rect(rect.x, y, rect.width, RowHeight), "pack-filter", KeyLabelAuthor, ctx);
    }

    private void DrawNestedDropdown(Rect rect, string elementId, string labelKey, UiWidgetContext ctx)
    {
        string optionsKey = elementId switch
        {
            "race-filter" => "race-filter-options",
            "xenotype-filter" => "xenotype-filter-options",
            _ => "author-options"
        };

        // The nested dropdown resolves the key through the Host translation seam, so the label is
        // translated in exactly one place for both the wide and the stacked layout.
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LabelKey"] = labelKey,
            ["Bind"] = elementId,
            ["OptionsBind"] = optionsKey,
            ["Height"] = rect.height.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var spec = new UiElementSpec(elementId, DropdownWidget.Kind, attributes);
        var dropdown = new DropdownWidget();
        dropdown.Configure(spec);
        dropdown.Draw(rect, ctx);
    }

    /// <summary>
    /// Dev-only parity trace: Measure and Draw must make the stack/height decisions from the same
    /// width. If a reserved-vs-drawn divergence ever reaches a player log again, the paired ltrace
    /// lines name the width and the decisions each side used.
    /// </summary>
    private void TraceLayout(string pass, float width, float body, float domain)
    {
        if (!SqueakLog.ShouldEmitDev) return;
        string line = pass
            + " width=" + width.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            + " body=" + body.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            + " domain=" + domain.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            + " stack=" + (UsFilterBarLayout.DropdownsStack(width) ? "true" : "false");
        if (string.Equals(line, lastLayoutTrace, System.StringComparison.Ordinal)) return;
        lastLayoutTrace = line;
        SqueakLog.LayoutTrace(line);
    }

    private string? lastLayoutTrace;
}
