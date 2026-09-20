using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Host factory for the diagnostics pages. Same shape as the settings host (parser, validation,
/// DarkGold theme, translation seam), deliberately independent bindings table: the panel's keys
/// are its own contract and no settings binding leaks in. Every display-write binding bumps the
/// session content revision (the D1/D6 contract), and the source-side caches expire on the overlay
/// revision - two clocks, each owning exactly what it must.
/// </summary>
public static class UsDiagnosticsHost
{
    /// <summary>Production entry for the main master-detail window.</summary>
    public static UiHost CreateMain(IUsDiagnosticsSource source)
        => Create(source, UsDiagnosticsSpec.MainXml, VerseFerriteTextMetrics.Instance);

    /// <summary>Production entry for one detachable detail window.</summary>
    public static UiHost CreateDetail(IUsDiagnosticsSource source)
        => Create(source, UsDiagnosticsSpec.DetailXml, VerseFerriteTextMetrics.Instance);

    /// <summary>
    /// Feeds the width the shell is about to arrange in, and moves the layout clock when that width
    /// crossed the presentation threshold. The VisibleKey swap is read live by the arrange pass, but a
    /// read-only binding announces no revision of its own - so without this bump the swap would depend
    /// on the arrange size changing in the same frame (true of today's shell, false of any page that
    /// arranges at a fixed size) and the two presentations could both be on screen for a frame.
    /// Returns true when the presentation flipped. A null host (the shell's first pass draws chrome
    /// before it creates one) still records the width; there is no arrangement to move yet.
    /// </summary>
    public static bool ApplyContentWidth(IUsDiagnosticsSource source, UiHost? host, float contentWidth)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        bool wasNarrow = source.Narrow;
        source.SetContentWidth(contentWidth);
        bool flipped = source.Narrow != wasNarrow;
        if (flipped)
        {
            host?.Session.BumpContentRevision();
        }

        return flipped;
    }

    public static UiHost Create(IUsDiagnosticsSource source, string pageXml, ITextMetrics metrics)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (metrics == null) throw new ArgumentNullException(nameof(metrics));

        UiNative.Trace = SqueakLog.PopupTrace;
        UsDiagnosticsWidgetRegistrar.EnsureRegistered();

        UiLayoutManifest manifest = UiLayoutManifest.Parse(pageXml);
        DiagRevisionBumper bumper = new();
        UiBindings bindings = BuildBindings(source, bumper);
        UiHost host = new(
            UsKernelWidgetRegistrar.Scope,
            manifest,
            bindings,
            UsTheme.Surface(),
            metrics,
            new UsKernelTranslation());
        bumper.Attach(host.Session);
        return host;
    }

    public const string KeySeconds = "diag-seconds";
    public const string KeyCollapsed = "diag-collapsed";
    public const string KeySearch = "diag-search";
    public const string KeyPage = "diag-page";
    public const string KeyRows = "diag-rows";
    public const string KeyTotal = "diag-total";
    public const string KeyPageCount = "diag-page-count";
    public const string KeyDetail = "diag-detail";
    public const string KeyMonitor = "diag-monitor";
    public const string KeyCanLock = "diag-can-lock";
    public const string KeyLock = "diag-lock";
    public const string KeyRowClick = "diag-row-click";
    public const string KeyBar = "diag-bar";
    public const string KeyRawOpen = "diag-raw-open";
    public const string KeyClose = "diag-close";

    /// <summary>Narrow-mode navigation: which in-window view is showing (writable, bumps the clock).</summary>
    public const string KeyNavView = "diag-navview";

    /// <summary>
    /// The two mutually exclusive presentations of the main page, read-only: wide = master list +
    /// detail column, narrow = the single in-window navigation column. Each presentation's subtree
    /// declares one of these as its VisibleKey, so exactly one of them is arranged (a hidden element
    /// owns no geometry at all - the supported replacement for the retired "an Auto column collapses
    /// to a 1px stub" idiom). The source owns the decision; the page never writes these.
    /// </summary>
    public const string KeyWide = "diag-wide";

    /// <inheritdoc cref="KeyWide"/>
    public const string KeyNarrow = "diag-narrow";

    /// <summary>Read-only: the narrow detail view is showing, so the Back control exists.</summary>
    public const string KeyShowBack = "diag-showback";

    /// <summary>One writable switch per condition group: all default open, only a click folds one.</summary>
    public const string KeyGroupGame = "diag-group-game";
    public const string KeyGroupRules = "diag-group-rules";
    public const string KeyGroupAudio = "diag-group-audio";

    /// <summary>The binding key of one condition group's fold switch.</summary>
    public static string GroupKey(UsDiagGateGroup group) => group switch
    {
        UsDiagGateGroup.Game => KeyGroupGame,
        UsDiagGateGroup.Rules => KeyGroupRules,
        _ => KeyGroupAudio,
    };

    private static UiBindings BuildBindings(IUsDiagnosticsSource source, DiagRevisionBumper bumper)
    {
        Action bump = bumper.Bump;
        UiBindings b = new();

        b.BindValue<bool>(KeySeconds, () => source.ShowSeconds, value => { source.ShowSeconds = value; bump(); });
        b.BindValue<bool>(KeyCollapsed, () => source.Collapsed, value => { source.Collapsed = value; bump(); });
        b.BindValue<string>(KeySearch, () => source.SearchQuery, value => { source.SearchQuery = value; bump(); });
        b.BindValue<int>(KeyPage, () => source.Page, value => { source.Page = value; bump(); });

        // Narrow navigation. The write goes through the binding so the layout cache sees it (D1/D6):
        // the widget never flips a private flag.
        b.BindValue<UsDiagNavView>(KeyNavView, () => source.NavigationView,
            value => { source.NavigationView = value; bump(); });
        b.BindReadOnly<bool>(KeyShowBack, () => source.ShowBackControl);

        // The presentation pair. One authority (the source's own narrow decision), two keys: the
        // read-only getters are evaluated by the arrange pass, and ApplyContentWidth moves the content
        // revision when the width crosses the threshold.
        b.BindReadOnly<bool>(KeyWide, () => !source.Narrow);
        b.BindReadOnly<bool>(KeyNarrow, () => source.Narrow);

        // One fold switch per group. A write goes through the binding, so the switch is page state on
        // the same clock as every other display write - and NOTHING in the data path touches it, which
        // is what makes "no automatic group collapse when values update" structural rather than a rule.
        b.BindValue<bool>(KeyGroupGame, () => source.IsGroupOpen(UsDiagGateGroup.Game),
            value => { source.SetGroupOpen(UsDiagGateGroup.Game, value); bump(); });
        b.BindValue<bool>(KeyGroupRules, () => source.IsGroupOpen(UsDiagGateGroup.Rules),
            value => { source.SetGroupOpen(UsDiagGateGroup.Rules, value); bump(); });
        b.BindValue<bool>(KeyGroupAudio, () => source.IsGroupOpen(UsDiagGateGroup.Audio),
            value => { source.SetGroupOpen(UsDiagGateGroup.Audio, value); bump(); });

        b.BindReadOnly<IReadOnlyList<UsDiagRow>>(KeyRows, () => source.PageRows);
        b.BindReadOnly<int>(KeyTotal, () => source.TotalRowCount);
        b.BindReadOnly<int>(KeyPageCount, () => source.PageCount);
        b.BindReadOnly<UsDiagDetail?>(KeyDetail, () => source.Detail);
        b.BindReadOnly<UsDiagRow?>(KeyMonitor, () => source.MonitorRow);
        b.BindReadOnly<UsDiagBarModel>(KeyBar, () => source.Bar!);
        b.BindReadOnly<bool>(KeyCanLock, () => source.CanLockDisplayed);
        b.BindValue<bool>(KeyRawOpen, () => source.RawOpen, value => { source.RawOpen = value; bump(); });

        b.BindAction<int>(KeyLock, _ => source.LockDisplayed());
        // A row click can also change the narrow navigation view, so it bumps like any display write;
        // the source's own drill-in/lock decision is untouched.
        b.BindAction<int>(KeyRowClick, pawnId => { source.ClickRow(pawnId); bump(); });
        b.BindCommand(KeyClose, source.RequestClose);

        return b;
    }

    private sealed class DiagRevisionBumper
    {
        private UiSession? session;

        public void Attach(UiSession value) => session = value;

        public void Bump()
        {
            session?.ClosePopup();
            session?.BumpContentRevision();
        }
    }
}

/// <summary>
/// Registers the six diagnostics widget kinds under the US scope with their creation-time
/// attribute contracts (unknown attributes are rejected at Host creation, same as settings).
/// </summary>
public static class UsDiagnosticsWidgetRegistrar
{
    private static readonly object Gate = new();
    private static bool registered;

    public static void EnsureRegistered()
    {
        if (registered && UiWidgetRegistry.KnownKinds(UsKernelWidgetRegistrar.Scope).Contains(UsDiagToolbarWidget.KindName)) return;

        lock (Gate)
        {
            if (registered) return;
            UsDiagToolbarWidget.Register();
            UsDiagListWidget.Register();
            UsDiagPagerWidget.Register();
            UsDiagNavWidget.Register();
            UsDiagNavBodyWidget.Register();
            UsDiagDetailWidget.Register();
            UsDiagBarWidget.Register();
            registered = true;
        }
    }

    internal static readonly IReadOnlyCollection<string> BaseSchema = new[] { "Id", "Kind", "Height" };
}
