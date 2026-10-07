using System;
using System.Collections.Generic;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// D4: the row title/detail text, extracted verbatim from <c>LayerRowBindings</c> so the SAME strings
/// the rows draw are the strings any later budget measurement reads. The bindings class delegates
/// here; there is one composition of "name (+state)" and one of "enabled/candidate" - a budget
/// measured over different text than the rows draw would be the old constant wearing a
/// measurement's clothes.
/// </summary>
internal static class LayerRowText
{
    public static string Title(
        IUsKernelSettingsSource source, IUiTranslation translation, SqueakVoicePackScope scope, string key)
    {
        if (scope == SqueakVoicePackScope.Race)
        {
            RaceLayerRowView? row = RaceRow(source, key);
            return row.HasValue
                ? UsPacksText.TitleWithState(translation, row.Value.DisplayName, row.Value.State)
                : "";
        }

        VoicePackDomainView? domain = XenotypeRow(source, key);
        if (!domain.HasValue) return "";

        // The xenotype row's title is the name qualified by its race's translated label - the same
        // composition the composite drew, through the same resolver.
        string name = UsPacksText.Format(
            translation, UsPacksText.KeyXenotypeRaceContext, domain.Value.DisplayName, domain.Value.RaceDisplay);
        return UsPacksText.TitleWithState(translation, name, domain.Value.State);
    }

    public static string Detail(
        IUsKernelSettingsSource source, IUiTranslation translation, SqueakVoicePackScope scope, string key)
    {
        if (scope == SqueakVoicePackScope.Race)
        {
            RaceLayerRowView? row = RaceRow(source, key);
            return row.HasValue
                ? UsPacksText.DetailText(translation, row.Value.EnabledCount, row.Value.CandidateCount)
                : "";
        }

        VoicePackDomainView? domain = XenotypeRow(source, key);
        return domain.HasValue
            ? UsPacksText.DetailText(translation, domain.Value.EnabledCount, domain.Value.CandidateCount)
            : "";
    }

    private static RaceLayerRowView? RaceRow(IUsKernelSettingsSource source, string key)
    {
        IReadOnlyList<RaceLayerRowView> rows = source.BuildView().Races;
        if (rows == null) return null;
        for (int i = 0; i < rows.Count; i++)
        {
            if (string.Equals(rows[i].RaceDefName, key, StringComparison.Ordinal)) return rows[i];
        }

        return null;
    }

    private static VoicePackDomainView? XenotypeRow(IUsKernelSettingsSource source, string key)
    {
        int split = key.IndexOf('|');
        if (split < 0) return null;

        string race = key.Substring(0, split);
        string target = key.Substring(split + 1);
        IReadOnlyList<VoicePackDomainView> rows = source.BuildView().XenotypeDomains;
        if (rows == null) return null;
        for (int i = 0; i < rows.Count; i++)
        {
            if (string.Equals(rows[i].RaceDefName, race, StringComparison.Ordinal)
                && string.Equals(rows[i].TargetDefName, target, StringComparison.Ordinal))
            {
                return rows[i];
            }
        }

        return null;
    }
}
