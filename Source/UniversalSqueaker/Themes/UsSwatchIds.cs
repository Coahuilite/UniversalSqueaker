using System;

namespace UniversalSqueaker;

/// <summary>
/// The closed set of official swatch ids (UIR-006) as PURE data. Settings persistence, PostLoadInit
/// repair and the global restore validate against this list without pulling the theme engine or the
/// FL reference into the settings-slice harnesses (Kernel/Pure/Models/Settings compile alone there);
/// <see cref="UsSwatches"/> builds its role tables on these same ids, so the set cannot drift.
/// The ids are machine tokens - never translated, compared ordinally.
/// </summary>
public static class UsSwatchIds
{
    public const string Default = "core";

    /// <summary>Registration order == the picker order == the <see cref="UsSwatches"/> table order.</summary>
    public static readonly string[] All = { "core", "royalty", "ideology", "biotech", "anomaly", "odyssey" };

    public static bool IsKnown(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        for (int i = 0; i < All.Length; i++)
        {
            if (string.Equals(All[i], id, StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
