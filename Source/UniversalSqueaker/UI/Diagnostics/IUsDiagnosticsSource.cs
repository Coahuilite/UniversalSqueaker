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

    /// <summary>
    /// The arranged content width the shell hands the page each pass (09 §3.5). The narrow
    /// presentation decision is a pure function of it; setting it must NOT bump the session clock,
    /// because the width is already part of the layout cache key.
    /// </summary>
    void SetContentWidth(float width);

    /// <summary>True while the page must present the in-window list/detail navigation.</summary>
    bool Narrow { get; }

    /// <summary>Which narrow view is showing. The wide presentation shows both and ignores it.</summary>
    UsDiagNavView NavigationView { get; set; }

    /// <summary>True only where a Back control has somewhere to go (narrow detail with an owning list).</summary>
    bool ShowBackControl { get; }

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

    /// <summary>
    /// Whether one of the three condition groups is expanded. Default TRUE for all three: the ruling
    /// makes every condition discoverable by default, and only the developer folds a group away. The
    /// value is page state, so an update to the data never changes it (no automatic collapse).
    /// </summary>
    bool IsGroupOpen(UsDiagGateGroup group);

    /// <summary>Folds one group away or expands it. Never called by anything but the user's click.</summary>
    void SetGroupOpen(UsDiagGateGroup group, bool open);

    /// <summary>True once the bar's close action asked this page to end. The WINDOW closes in its own pass.</summary>
    bool CloseRequested { get; }

    /// <summary>The bar's close action: a page-level request, never a direct window close.</summary>
    void RequestClose();

    bool CanLockDisplayed { get; }

    void ClickRow(int pawnId);
    void LockDisplayed();
}
