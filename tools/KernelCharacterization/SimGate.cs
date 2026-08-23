using SqueakyRatkin.Kernel;

namespace SqueakyRatkin.KernelCharacterization;

/// <summary>模拟 playability 面：all = 全部可播；partial = 含 "_Muted" 后缀的 key 不可播（覆盖 sound 级过滤）。</summary>
public sealed class SimGate : ISoundGate
{
    public static readonly SimGate All = new(false);
    public static readonly SimGate Partial = new(true);

    private readonly bool muteSome;

    private SimGate(bool muteSome) => this.muteSome = muteSome;

    public bool Playable(string soundKey, SelectionContext ctx)
    {
        if (soundKey == null) return false;
        return !muteSome || !soundKey.EndsWith("_Muted", System.StringComparison.Ordinal);
    }
}
