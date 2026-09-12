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

    private static UiBindings BuildBindings(IUsDiagnosticsSource source, DiagRevisionBumper bumper)
    {
        Action bump = bumper.Bump;
        UiBindings b = new();

        b.BindValue<bool>(KeySeconds, () => source.ShowSeconds, value => { source.ShowSeconds = value; bump(); });
        b.BindValue<bool>(KeyCollapsed, () => source.Collapsed, value => { source.Collapsed = value; bump(); });
        b.BindValue<string>(KeySearch, () => source.SearchQuery, value => { source.SearchQuery = value; bump(); });
        b.BindValue<int>(KeyPage, () => source.Page, value => { source.Page = value; bump(); });

        b.BindReadOnly<IReadOnlyList<UsDiagRow>>(KeyRows, () => source.PageRows);
        b.BindReadOnly<int>(KeyTotal, () => source.TotalRowCount);
        b.BindReadOnly<int>(KeyPageCount, () => source.PageCount);
        b.BindReadOnly<UsDiagDetail?>(KeyDetail, () => source.Detail);
        b.BindReadOnly<UsDiagRow?>(KeyMonitor, () => source.MonitorRow);
        b.BindReadOnly<UsDiagBarModel>(KeyBar, () => source.Bar!);
        b.BindReadOnly<bool>(KeyCanLock, () => source.CanLockDisplayed);
        b.BindValue<bool>(KeyRawOpen, () => source.RawOpen, value => { source.RawOpen = value; bump(); });

        b.BindAction<int>(KeyLock, _ => source.LockDisplayed());
        b.BindAction<int>(KeyRowClick, pawnId => source.ClickRow(pawnId));
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
/// Registers the five diagnostics widget kinds under the US scope with their creation-time
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
            UsDiagDetailWidget.Register();
            UsDiagBarWidget.Register();
            registered = true;
        }
    }

    internal static readonly IReadOnlyCollection<string> BaseSchema = new[] { "Id", "Kind", "Height" };
}
