using System;
using HarmonyLib;
using UniversalSqueaker;
using Verse;
using Verse.Sound;
using UnityEngine;

internal static partial class Program
{
    private static void S5InstanceOutputWithRealPatches()
    {
        UsInstanceOutput.Initialize(new Harmony("us.s2.controlled.adapter.fixture"));
        Check(UsInstanceOutput.Available, "S5 actual required Harmony patch inventory and two-call auxiliary IL shape validated");
        var definition = new SubSoundDef(new System.Collections.Generic.List<ResolvedGrain>(), 0);
        var sound = new SoundDef(); sound.subSounds.Add(definition);
        var originalDistance = definition.distRange;
        UsInstanceOutput.Distance = new FloatRange(15, 50);
        CompSqueaker.GlobalVolumeFactor = 1;
        var info = SoundInfo.OnCamera();
        int request = UsInstanceOutput.Mark(ref info, new UsOutputContribution(2));
        var owned = SampleOneShot.TryMakeAndPlay(definition, info, new AudioSource());
        UsInstanceOutput.ReleaseRequest(request);
        Check(Math.Abs(owned.source.volume - 1.6f) < .0001 && owned.source.minDistance == 15 && owned.source.maxDistance == 50 && owned.source.Playing,
            "S5 real patches affect first native-shape Update before Play without writing shared distance");
        var unowned = SampleOneShot.TryMakeAndPlay(definition, SoundInfo.OnCamera(), new AudioSource());
        Check(Math.Abs(unowned.source.volume - .8f) < .0001 && unowned.source.maxDistance == originalDistance.max,
            "S5 same SubSoundDef independent non-US request retains native mapping and distance");
        CompSqueaker.GlobalVolumeFactor = 0; owned.Update(); unowned.Update();
        Check(owned.source.volume == 0 && Math.Abs(unowned.source.volume - .8f) < .0001, "S5 current global zero affects only owned instance");
        CompSqueaker.GlobalVolumeFactor = .5f; owned.Update(); owned.Update();
        Check(Math.Abs(owned.source.volume - .8f) < .0001, "S5 one-to-zero-to-half restores original local L, not cumulative or division-by-old-G");
        UsInstanceOutput.Distance = new FloatRange(15, 65); owned.Update();
        Check(owned.source.maxDistance == 65 && definition.distRange.max == 20, "S5 live instance distance updates while shared definition remains unchanged");
        owned.SampleCleanup();
        var reused = SampleOneShot.TryMakeAndPlay(definition, SoundInfo.OnCamera(), owned.source);
        Check(Math.Abs(reused.source.volume - .8f) < .0001 && reused.source.maxDistance == 20,
            "S5 pooled AudioSource reuse by non-US cannot inherit sample ownership");

        info = SoundInfo.OnCamera();
        request = UsInstanceOutput.Mark(ref info, new UsOutputContribution(3));
        var parent = new Sustainer(sound, info);
        UsInstanceOutput.ReleaseRequest(request);
        Check(Math.Abs(parent.Initial.source.volume - .3f) < .0001 && parent.Initial.source.maxDistance == 65,
            "S5 constructor-time first sustained sample captures info token before parent postfix and preserves native fade");
        var later = new SampleSustainer(new SubSustainer(parent, definition)) { source = new AudioSource() };
        later.Update();
        Check(Math.Abs(later.source.volume - .3f) < .0001, "S5 later sustained sample follows explicit parent ownership after request token release");
        SoundStarter.NestedCall = () => sound.PlayOneShot(SoundInfo.OnCamera());
        int beforeAux = SoundStarter.Played.Count;
        parent.End();
        Check(SoundStarter.Played.Count == beforeAux + 2 && Math.Abs(SoundStarter.Played[beforeAux].source.volume - .8f) < .0001
            && Math.Abs(SoundStarter.Played[beforeAux + 1].source.volume - 1.2f) < .0001,
            "S5 precise native auxiliary call-site marking leaves nested independent non-US request unowned");
        var afterEnd = SampleSustainer.TryMakeAndPlay(new SubSustainer(parent, definition), new AudioClip(), 10);
        parent.Initial.Update();
        Check(afterEnd == null && Math.Abs(parent.Initial.source.volume - .3f) < .0001,
            "S5 End rejects a delayed fresh grain while existing native fade sample keeps current owned contribution until Cleanup");
        parent.FixtureCleanup();
        Check(UsInstanceOutput.OwnerOf(parent) == null && Math.Abs(SoundStarter.Played[SoundStarter.Played.Count - 1].source.volume - 1.2f) < .0001,
            "S5 cleanup retains parent through stop auxiliary creation then releases parent ownership");
        var delayed = SampleSustainer.TryMakeAndPlay(new SubSustainer(parent, definition), new AudioClip(), 10);
        var nativeParent = new Sustainer(sound, SoundInfo.OnCamera());
        nativeParent.End();
        nativeParent.FixtureCleanup();
        var nativeDelayed = SampleSustainer.TryMakeAndPlay(new SubSustainer(nativeParent, definition), new AudioClip(), 10);
        Check(delayed == null && nativeDelayed != null && nativeDelayed.source.Playing,
            "S5 delayed grain callback after owned Cleanup cannot emit an unowned tail; unrelated native parent unchanged");
        CompSqueaker.GlobalVolumeFactor = 1;
        UsInstanceOutput.Distance = new FloatRange(15, 50);
    }
}
