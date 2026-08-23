namespace SqueakyRatkin.Kernel;

// 调制合成（§4.1，0.3.2 接入生产链；0.3.0 骨架 = 完整纯逻辑，未接线）。
// 语义：mood 显式优先 → age 继承 → 恒等。

public readonly struct ModulationAxis
{
    public readonly bool HasPitch;
    public readonly float Pitch;
    public readonly bool HasVolume;
    public readonly float Volume;
    public readonly bool HasJitter;
    public readonly (float Min, float Max) Jitter;

    public ModulationAxis(bool hasPitch, float pitch, bool hasVolume, float volume, bool hasJitter, (float Min, float Max) jitter)
    {
        HasPitch = hasPitch;
        Pitch = pitch;
        HasVolume = hasVolume;
        Volume = volume;
        HasJitter = hasJitter;
        Jitter = jitter;
    }

    public static ModulationAxis Identity => default;
}

public static class Modulation
{
    public static ModulationAxis ComposeModulation(ModulationAxis mood, ModulationAxis age)
    {
        return new ModulationAxis(
            mood.HasPitch ? true : age.HasPitch,
            mood.HasPitch ? mood.Pitch : age.Pitch,
            mood.HasVolume ? true : age.HasVolume,
            mood.HasVolume ? mood.Volume : age.Volume,
            mood.HasJitter ? true : age.HasJitter,
            mood.HasJitter ? mood.Jitter : age.Jitter);
    }
}
