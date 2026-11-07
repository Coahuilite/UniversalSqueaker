using System;
using System.IO;
using System.Text;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public static partial class UsKernelSettingsHost
{
    private const string Source = "coahuilite.universalsqueaker";
    private const string ManifestResourceName = "UniversalSqueaker.UI.Layout.Schema2.xml";
    private const string ContentScrollId = "content-scroll";
    private const string HelpPanelScrollId = "help-scroll";
    private static string RosterKey(string value) => BitConverter.ToString(Encoding.UTF8.GetBytes(value)).Replace("-", "");
    private static string PageHelpRef(VoicePacksPageState state) => state.Page switch
    {
        "Voices" => "us/roster",
        "Behavior" => "us/scope-tree", "Presets" => "us/preset-list", "Output" => "us/basic-tuning",
        "Fallback" => "us/fallback-editor", "Appearance" => "us/theme-picker",
        "Diagnostics" => "us/diagnostics", "Restore" => "us/reset-center", _ => "us/page-title"
    };

    /// <summary>US-RESET1 review1: end this host's open edit ONLY when the caller's predicate says
    /// the edit belongs to the fields a restore just WROTE. The review-1 boundary: an unrelated
    /// open edit (a volume field while a diagnostics-area restore answers NoChange) must survive
    /// untouched, and NoChange/Rejected must never clear anything - so the command runs the restore
    /// FIRST, and reaches this only on Applied with a target-matching predicate. The mechanism is
    /// exactly ONE rung of the cancel ladder, entered only while an edit is open: with a live field
    /// edit the ladder answers the EDIT itself (a popup or a held capture cannot coexist with it -
    /// the funnel's own outside-click press ends the edit before either can open), so this never
    /// reaches a business CancelBind, never loops the ladder, and never touches another window's
    /// native capture. The marked discard applies at the field's own next draw - before any later
    /// Enter or focus-out - so a stale draft cannot overwrite the restored value, while the
    /// existing outside-click commit contract for ordinary edits stays untouched.</summary>
    public static void EndAffectedEdit(UiHost? host, Func<string, bool> editIsAffected)
    {
        if (host == null) return;
        FerriteLib.UiKit.Kernel.UiNode? edit = host.Session.ActiveEditNode;
        if (edit != null && editIsAffected(edit.ElementId)) host.TryHandleCancel();
    }
    /// <summary>
    /// Delayed session revision bump for the Host binding boundary. Bindings are built before the
    /// Host (and its session) exists, so the bumper starts detached and attaches once the Host is
    /// created; invocation only ever happens during Draw, after attachment.
    /// </summary>
    private sealed class SessionRevisionBumper
    {
        private UiHost? host;
        public string CurrentHelpRef => host?.Session.CurrentHelpRef ?? "";

        public void Attach(UiHost value)
        {
            host = value;
        }

        /// <summary>
        /// Advances the clock the engine's snapshot cache is keyed on, so the next arrange re-reads the
        /// page's declared visibility. This is REQUIRED for the declarative drawer: the cache compares
        /// <c>cachedContentRevision == ctx.Session.ContentRevision</c> and returns the previous snapshot
        /// when every term matches, so without a bump a closed drawer keeps its old geometry and the
        /// "no reserved column" assertions fail on the very next measure.
        /// </summary>
        public void Bump()
        {
            host?.Session.ClosePopup();
            host?.Invalidate();
        }

        /// <summary>
        /// Resets the session-owned scroll positions for the two page scroll containers. Used on
        /// workspace switches so the centre content and the bottom help panel start at their top; other
        /// layout-affecting writes only bump the revision and keep the user's scroll place.
        /// </summary>
        public void ResetScroll()
        {
            if (host == null) return;

            // FL 0.4.0 keys scroll positions by node identity, not by the string a page declares:
            // SetScrollPosition takes a UiNode and ScrollPositions is keyed by node (the string
            // overload is gone with the batch-C node work, bd4d1b5). GetNodeByElementId is the
            // carrier's own bridge from the one string this page owns to that identity.
            //
            // A null lookup means the element has not been arranged in this session yet, so there is
            // no scroll state to reset. Skipping is equivalent to the old write, not a silent
            // behaviour change: UiSession.GetScrollPosition answers UiLayoutPoint.Zero for a node that
            // holds nothing, so "no entry" and "entry = zero" are indistinguishable to every reader,
            // and these two calls never wrote anything but zero.
            UiNode? contentNode = host.Session.GetNodeByElementId(ContentScrollId);
            if (contentNode != null)
            {
                host.SetScrollPosition(ContentScrollId, UiLayoutPoint.Zero);
            }

            UiNode? helpNode = host.Session.GetNodeByElementId(HelpPanelScrollId);
            if (helpNode != null)
            {
                host.SetScrollPosition(HelpPanelScrollId, UiLayoutPoint.Zero);
            }
        }

        public void SetScrollTarget(string elementId)
        {
            host?.Session.SetScrollTarget(elementId);
        }

        public void ResetHelpScroll()
        {
            if (host?.Session.GetNodeByElementId(HelpPanelScrollId) != null)
                host.SetScrollPosition(HelpPanelScrollId, UiLayoutPoint.Zero);
        }

        /// <summary>
        /// CORRECTIONS-R1 topic 2: the navigation arrival's scroll arming, in one place. Reset the
        /// page scrolls first (the arrival must not inherit the PREVIOUS workspace's offset - the
        /// same behavior a plain set-tab gives), CLEAR any pending target request left by an
        /// earlier open (a stale request could be consumed by the wrong arrange later), then arm
        /// the new destination: an FL-materialized "pack-card-row#<key>" identity or a static
        /// section id. An empty destination arms nothing and leaves the cleared state. No manual
        /// offsets, no copied scroll policy - the engine resolves declared ids its own way
        /// (UiSession.SetScrollTarget); parallel scroll containers are the engine's own
        /// content-local containment decision, observed by the real-host lane below.
        /// </summary>
        public void ArmScrollTarget(string destination)
        {
            ResetScroll();
            host?.Session.ClearScrollTarget();
            if (!string.IsNullOrEmpty(destination)) host?.Session.SetScrollTarget(destination);
        }
    }
    private static string ReadManifest()
    {
        Stream? stream = typeof(UsKernelSettingsHost).Assembly.GetManifestResourceStream(ManifestResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Embedded Schema=2 layout resource '{ManifestResourceName}' was not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
