using System;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

// Metadata belongs to the real anchor node. It never opens help or changes an editor/business value.
internal sealed class UsHelpAnchor
{
    internal const string StateKey = "us/help-anchor";
    internal string Topic = "", Context = "", Label = "";
    internal int PublishedPass = -1;
}

public static partial class UsKernelSettingsHost
{
    private static string DisplayHelpRef(VoicePacksPageState state)
        => state.PinnedHelpRef.Length > 0 ? state.PinnedHelpRef : state.FollowHelpRef;
    private static string DisplayHelpContext(VoicePacksPageState state)
        => state.PinnedHelpRef.Length > 0 ? state.PinnedHelpContext : state.FollowHelpContext;

#if !US_PREVIEW_NEUTRAL
    private static string HelpScope(IUsKernelSettingsSource source)
        => HelpScope(source.ViewState);

#endif
    private static string HelpScope(VoicePacksPageState s)
    {
        return s.Page + "\n" + (s.Page == "Behavior" || s.Page == "Output"
            ? s.TuningLayer + "\n" + s.TuningRaceDefName + "\n" + s.TuningXenotypeDefName + "\n" + s.TuningSelectedAction
            : s.Page == "Voices" ? s.BrowseDefinitions + "\n" + s.HighlightedTarget + "\n" + s.SelectedPackKey
            : s.Page == "Fallback" ? s.FallbackSelectedRace + "\n" + s.FallbackSelectedEntryAction : "");
    }

#if !US_PREVIEW_NEUTRAL
    private static string HelpPageContext(IUsKernelSettingsSource source, IUiTranslation translation)
    {
        var s = source.ViewState;
        if (s.Page == "Behavior" || s.Page == "Output")
            return TuningContextLabel(source, translation, s.TuningLayer, s.TuningRaceDefName, s.TuningXenotypeDefName);
        if (s.Page == "Voices") return s.BrowseDefinitions
            ? source.BuildView().Roster.FirstOrDefault(r => r.Key == s.HighlightedTarget)?.Label ?? translation.Translate("US.UI.Voices")
            : source.BuildView().AllPackCards.FirstOrDefault(p => p.Key == s.SelectedPackKey).Label ?? translation.Translate("US.UI.Voices");
        if (s.Page == "Fallback") return s.FallbackSelectedRace;
        return translation.Translate("US.UI." + s.Page);
    }

#endif
    private static void SynchronizeHelpPresentation(UiHost host, VoicePacksPageState state, float width)
    {
        string scope = host.Bindings.Get<string>("help-scope");
        bool changed = false;
        bool scopeChanged = state.HelpScope != scope;
        if (scopeChanged)
        {
            state.HelpScope = scope;
            state.FollowHelpRef = PageHelpRef(state);
            state.FollowHelpContext = host.Bindings.Get<string>("help-page-context");
            state.FollowHelpLabel = "";
            // Explicit pin consumers retain precedence and their captured context/lifetime.
            changed = state.PinnedHelpRef.Length == 0;
        }
        string topic = host.Session.CurrentHelpRef ?? "";
        if (!scopeChanged && state.PinnedHelpRef.Length == 0
            && (UsHelpCatalog.TryFindItem(topic, out _, out _) || UsHelpCatalog.TryGetSection(topic, out _)))
        {
            // This settings Host requests zero Session grace. The completed pass therefore carries
            // real standard/custom/popup publications only; the US state supplies reading-hold.
            string context = host.Bindings.Get<string>("help-page-context"), label = "";
            if (host.Session.CurrentHelpSource is UiNodeId id && host.Session.GetNode(id) is UiNode node)
            {
                var anchor = host.CreateContext(width).WithNode(node).State(UsHelpAnchor.StateKey, () => new UsHelpAnchor());
                if (anchor.PublishedPass == host.Session.Frame && anchor.Topic == topic)
                {
                    context = anchor.Context; label = anchor.Label;
                }
            }
            if (state.FollowHelpRef != topic || state.FollowHelpContext != context || state.FollowHelpLabel != label)
            {
                state.FollowHelpRef = topic; state.FollowHelpContext = context; state.FollowHelpLabel = label; changed = true;
            }
        }
        if (!changed) return;
        if (host.Session.GetNodeByElementId(HelpPanelScrollId) is UiNode scroll)
            host.Session.SetScrollPosition(scroll, UiLayoutPoint.Zero);
        host.Invalidate();
    }
}
