using System.Collections.Generic;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Data-driven fallback-profile source. XML Defs of this class inject per-race fallback tables into the
/// kernel; the kernel and the C# assembly carry no product seed. Empty source data is a valid startup.
/// </summary>
public class UniversalSqueakerFallbackProfileDef : Def
{
    public string raceDefName = "";
    public int profileVersion = 1;
    public List<UniversalSqueakerFallbackEntry> entries = new();
}

public class UniversalSqueakerFallbackEntry
{
    public string actionKey = "";
    public string soundKey = "";
}
