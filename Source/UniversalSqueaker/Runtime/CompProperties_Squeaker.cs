using Verse;

namespace UniversalSqueaker;

/// <summary>Native component attachment only. Contracts and defaults have one definition authority.</summary>
public sealed class CompProperties_Squeaker : CompProperties
{
    public CompProperties_Squeaker() { compClass = typeof(CompSqueaker); }
    public static CompProperties_Squeaker CreateDefault() => new();
}
