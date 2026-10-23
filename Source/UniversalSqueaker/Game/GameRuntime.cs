using System;
using System.Threading;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>1.6 serial publisher and canonical native-domain lookup. No second action/mood resolver or audio pool.</summary>
internal static class GameRuntime
{
    private static int ownerThread;
    internal static GameRuntimeView Current { get; private set; } = new(SqueakXenotypeCatalogSnapshot.Empty);
    internal static void InitializeMainThread() => ownerThread = Thread.CurrentThread.ManagedThreadId;
    internal static void PublishConfiguration(GameSettings settings)
    {
        if (ownerThread != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("GameRuntimeOwnerThreadRequired");
        settings.PublishBehaviorConfiguration();
        Current = new GameRuntimeView(SqueakXenotypeCatalog.Current);
    }
}

internal sealed class GameRuntimeView
{
    private readonly SqueakXenotypeCatalogSnapshot definitions;
    internal GameRuntimeView(SqueakXenotypeCatalogSnapshot definitions) { this.definitions = definitions; }

    internal NativeSubjectContext ResolveContext(Pawn pawn, BehaviorConfiguration configuration)
    {
        if (!ModsConfig.BiotechActive) return new NativeSubjectContext(null);
        var actual = pawn.genes?.Xenotype;
        if (actual == null) return new NativeSubjectContext(null);
        if (!definitions.XenotypeByDefName.TryGetValue(actual.defName, out var canonical) || !ReferenceEquals(actual, canonical))
            return new NativeSubjectContext(null);
        var domain = new Kernel.AudioDomain(new Kernel.RaceKey(pawn.def.defName), new Kernel.XenotypeKey(canonical.defName));
        if (!configuration.HasDomain(domain) && !SqueakerRuntime.Audio.HasDomain(domain) && !SqueakerRuntime.BehaviorSources.HasDomain(domain))
            return new NativeSubjectContext(null);
        return new NativeSubjectContext(canonical);
    }
}

internal readonly struct NativeSubjectContext
{
    internal readonly XenotypeDef? Xenotype;
    internal NativeSubjectContext(XenotypeDef? xenotype) { Xenotype = xenotype; }
}

public enum SqueakSoundSource { None, XenotypePack, RacePack, Vanilla }
