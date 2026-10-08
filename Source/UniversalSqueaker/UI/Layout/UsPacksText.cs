using System;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Keyed-text outlet for the Packs workspace: the composed names (axis + domain, xenotype + race
/// context) resolve here once so the host's per-item projections and the scope line share one
/// implementation.
///
/// <para>
/// US-PACK1 pruned the rest: the browse rows' "n / m enabled" detail and the state-suffixed title
/// retired WITH the browse cards (their only caller, LayerRowText, is gone), so the counts-shaped and
/// state-shaped helpers left with them rather than surviving as a second vocabulary nothing draws.
/// The card rows compose their titles through <see cref="KeyXenotypeRaceContext"/> exactly as the
/// scope line does - one name for one domain, wherever it shows.
/// </para>
/// <para>
/// Word order is part of the key text, never of layout code: a translation is free to put the state before
/// or after the name, and the en/zh pair then shares one layout (measured band, one label).
/// </para>
/// </summary>
internal static class UsPacksText
{
    /// <summary>Whole-sentence template for "object plus its state"; the placeholder order IS the word order.</summary>
    internal const string KeyNameWithState = "US.Packs.Domain.NameWithState";

    internal const string KeyXenotypeRaceContext = "US.Packs.Domain.XenotypeRaceContext";

    /// <summary>Formats a keyed template with invariant culture so digits stay as plain as before.</summary>
    internal static string Format(IUiTranslation translation, string key, params object[] args)
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, translation.Translate(key), args);
    }

}
