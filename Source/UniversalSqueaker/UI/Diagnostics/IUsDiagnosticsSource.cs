using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// The data boundary of every diagnostics page (main master-detail window and detachable
/// detail windows). The production implementations wrap the overlay session; the harness
/// injects fakes, which is what makes "假快照源注入" a source-level contract rather than a
/// game-bound test. All content arrives already projected through
/// <see cref="UsDiagnosticsProjection"/> - the pages draw strings, they never format them.
/// </summary>
public interface IUsDiagnosticsSource
{
    /// <summary>The overlay rebuild clock; every page cache must expire on THIS clock (cache-clock rule).</summary>
    int Revision { get; }

    /// <summary>False once the observed pawn stopped being tracked (dead, despawned, map change): the window closes itself.</summary>
    bool IsValid { get; }

    bool ShowSeconds { get; set; }
    bool Collapsed { get; set; }
    string SearchQuery { get; set; }
    int Page { get; set; }

    IReadOnlyList<UsDiagRow> PageRows { get; }
    int TotalRowCount { get; }
    int PageCount { get; }

    /// <summary>The detail shown in this page's detail column (main: live selection; lock window: the pinned pawn).</summary>
    UsDiagDetail? Detail { get; }

    /// <summary>The collapsed-bar row (main: last CHANGED pawn; lock window: its own pawn).</summary>
    UsDiagRow? MonitorRow { get; }

    /// <summary>
    /// The collapsed bar's three answers, already projected and translated (09 §3.3): what this is,
    /// whether it is on, and what it last did. Null only before a page has anything to say.
    /// </summary>
    UsDiagBarModel? Bar { get; }

    /// <summary>The detail column's raw-values block: folded by default, so evidence never leads.</summary>
    bool RawOpen { get; set; }

    /// <summary>True once the bar's close action asked this page to end. The WINDOW closes in its own pass.</summary>
    bool CloseRequested { get; }

    /// <summary>The bar's close action: a page-level request, never a direct window close.</summary>
    void RequestClose();

    bool CanLockDisplayed { get; }

    void ClickRow(int pawnId);
    void LockDisplayed();
}
