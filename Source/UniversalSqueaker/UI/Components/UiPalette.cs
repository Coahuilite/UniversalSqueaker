using UnityEngine;

namespace UniversalSqueaker.UI;

/// <summary>Shared visual palette for the US settings surface. Pure presentation, no business state.</summary>
internal static class UiPalette
{
    internal static readonly Color Ink = new(.06f, .055f, .05f, .94f);
    internal static readonly Color Panel = new(.09f, .085f, .075f, .92f);
    internal static readonly Color Raised = new(.115f, .107f, .095f, .96f);
    internal static readonly Color Emphasized = new(.13f, .112f, .082f, .92f);
    internal static readonly Color Warning = new(.17f, .095f, .073f, .94f);
    internal static readonly Color Success = new(.075f, .135f, .095f, .94f);
    internal static readonly Color Border = new(.34f, .32f, .28f, .82f);
    internal static readonly Color Gold = new(.92f, .68f, .30f);
    internal static readonly Color Muted = new(.68f, .66f, .61f, .92f);
    internal static readonly Color Disabled = new(.43f, .42f, .39f, .82f);
    internal static readonly Color Selected = new(.235f, .195f, .125f, .96f);
    internal static readonly Color Danger = new(.58f, .19f, .16f, .96f);
}
