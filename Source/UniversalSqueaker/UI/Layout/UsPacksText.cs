using System;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Keyed-text outlet for the Packs workspace: the domain row's title (name plus its state) and its
/// "n / m enabled" detail line are composed here once, so both layer cards and the host's per-item row
/// projections resolve one text through one implementation.
///
/// <para>
/// Moved out of <c>UsRaceLayerWidget.cs</c> when S4-2 dissolved the two layer composites: the composing
/// callers are now the host's item-local binding registrations rather than a widget, so the class outlives
/// the widgets that used to own it. The <see cref="UiWidgetContext"/> overloads went with them - the host
/// holds the translation seam, not a widget context, and keeping a second context-shaped entry point
/// nothing calls would be the "one text, two resolvers" shape this type exists to prevent.
/// </para>
/// <para>
/// Word order is part of the key text, never of layout code: a translation is free to put the state before
/// or after the name, and the en/zh pair then shares one layout (measured band, one label).
/// </para>
/// </summary>
internal static class UsPacksText
{
    internal const string KeyEnabledSummary = "US.Packs.Domain.EnabledSummary";

    /// <summary>Whole-sentence template for "object plus its state"; the placeholder order IS the word order.</summary>
    internal const string KeyNameWithState = "US.Packs.Domain.NameWithState";

    internal const string KeyStateOrphan = "US.Packs.Domain.State.Orphan";

    internal const string KeyStateTargetUnavailable = "US.Packs.Domain.State.TargetUnavailable";

    internal const string KeyStateDormant = "US.Packs.Domain.State.Dormant";

    internal const string KeyXenotypeRaceContext = "US.Packs.Domain.XenotypeRaceContext";

    /// <summary>Formats a keyed template with invariant culture so digits stay as plain as before.</summary>
    internal static string Format(IUiTranslation translation, string key, params object[] args)
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, translation.Translate(key), args);
    }

    /// <summary>The Tiny status line under a domain row: enabled/candidate counts, no state.</summary>
    internal static string DetailText(IUiTranslation translation, int enabled, int candidate)
    {
        return Format(translation, KeyEnabledSummary, enabled, candidate);
    }

    /// <summary>
    /// The row title: the object's name followed by its state, composed through one keyed template (the
    /// state is postposed in Chinese and the pair shares a single label rect in both languages). A domain
    /// without a state keeps its bare name, so the template is never asked to render a hole.
    /// </summary>
    internal static string TitleWithState(
        IUiTranslation translation, string name, SqueakVoicePackDomainState state)
    {
        string stateKey = StateKey(state);
        return stateKey.Length == 0
            ? name
            : Format(translation, KeyNameWithState, name, translation.Translate(stateKey));
    }

    /// <summary>Translation key of the row state, or empty when the domain carries no state.</summary>
    internal static string StateKey(SqueakVoicePackDomainState state)
    {
        return state switch
        {
            SqueakVoicePackDomainState.Orphan => KeyStateOrphan,
            SqueakVoicePackDomainState.TargetUnavailable => KeyStateTargetUnavailable,
            SqueakVoicePackDomainState.Dormant => KeyStateDormant,
            _ => "",
        };
    }
}
