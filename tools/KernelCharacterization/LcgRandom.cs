using SqueakyRatkin.Kernel;

namespace SqueakyRatkin.KernelCharacterization;

/// <summary>确定性随机源（语料可复现）：SplitMix64，Next01 ∈ [0,1)。</summary>
public sealed class LcgRandom : IRollSource
{
    private ulong state;

    public LcgRandom(long seed) => state = (ulong)seed + 0x9E3779B97F4A7C15UL;

    public double Next01()
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (z >> 11) * (1.0 / 9007199254740992.0);
    }
}
