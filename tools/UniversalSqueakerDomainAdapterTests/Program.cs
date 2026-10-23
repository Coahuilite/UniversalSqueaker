using System;
using System.Collections.Generic;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;
using RimWorld;
using Verse;

internal static class Program
{
    private static int count;
    private static void Check(bool value,string label){if(!value)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
    private static void Main()
    {
        var child=new XenotypeDef{defName="Shared"};
        var snapshot=new SqueakXenotypeCatalogSnapshot();snapshot.XenotypeByDefName.Add(child.defName,child);
        var view=new GameRuntimeView(snapshot);
        var pawn=new Pawn{def=new ThingDef{defName="RaceA"},genes=new PawnGenes{Xenotype=child}};
        BehaviorConfiguration Config(params BehaviorPlayerOverride[] rows)=>new("",rows,true,1,1);
        BehaviorPlayerOverride Row(string race)=>new("Pawn","",race,"Shared",CoreActions.Work,new BehaviorValues{Volume=0});
        ModsConfig.BiotechActive=false;
        Check(view.ResolveContext(pawn,Config(Row("RaceA"))).Xenotype==null,"DLC unavailable has no child domain");
        ModsConfig.BiotechActive=true;
        Check(view.ResolveContext(pawn,Config()).Xenotype==null,"canonical loaded definition alone is not an exact input domain");
        Check(view.ResolveContext(pawn,Config(Row("RaceB"))).Xenotype==null,"same child name configured on another race cannot admit this race");
        Check(ReferenceEquals(view.ResolveContext(pawn,Config(Row("RaceA"))).Xenotype,child),"exact configuration domain admits the canonical native object");
        pawn.genes.Xenotype=new XenotypeDef{defName="Shared"};
        Check(view.ResolveContext(pawn,Config(Row("RaceA"))).Xenotype==null,"same spelling but noncanonical object is rejected");pawn.genes.Xenotype=child;
        SqueakerRuntime.Audio=new AudioCatalog(new[]{new UniversalSqueaker.Responses.AudioCandidate("entry","sound",CoreActions.Work,"RaceA",1,childDomain:"Shared")});
        Check(ReferenceEquals(view.ResolveContext(pawn,Config()).Xenotype,child),"exact audio input domain independently admits canonical child");
        SqueakerRuntime.Audio=AudioCatalog.Empty;
        SqueakerRuntime.BehaviorSources=new BehaviorSourceDirectory(new[]{new BehaviorSource{Id="author/source",Domain=new BehaviorDomain("Pawn","","RaceA","Shared")}});
        Check(ReferenceEquals(view.ResolveContext(pawn,Config()).Xenotype,child),"exact author source domain independently admits canonical child");
        pawn.genes=null;Check(view.ResolveContext(pawn,Config(Row("RaceA"))).Xenotype==null,"missing live genes do not reuse an earlier child");
        GameRuntime.InitializeMainThread();SqueakXenotypeCatalog.Current=snapshot;var settings=new GameSettings();GameRuntime.PublishConfiguration(settings);
        Check(settings.Publications==1,"actual native publication entry publishes configuration once");
        Console.WriteLine("Actual GameRuntime canonical-domain adapter with explicit platform doubles PASS "+count);
    }
}
// Only these native boundary shapes are doubled. The class under test is linked production GameRuntime.
namespace RimWorld { public sealed class XenotypeDef { public string defName=""; } public static class ModsConfig { public static bool BiotechActive; } }
namespace Verse { public sealed class ThingDef { public string defName=""; } public sealed class PawnGenes { public XenotypeDef? Xenotype; } public sealed class Pawn { public ThingDef def=new(); public PawnGenes? genes; } }
namespace UniversalSqueaker
{
    public sealed class GameSettings { internal int Publications; internal void PublishBehaviorConfiguration()=>Publications++; }
    public sealed class SqueakXenotypeCatalogSnapshot { public static readonly SqueakXenotypeCatalogSnapshot Empty=new(); public readonly Dictionary<string,XenotypeDef> XenotypeByDefName=new(); }
    public static class SqueakXenotypeCatalog { public static SqueakXenotypeCatalogSnapshot Current=SqueakXenotypeCatalogSnapshot.Empty; }
    public static class SqueakerRuntime { public static AudioCatalog Audio=AudioCatalog.Empty; public static BehaviorSourceDirectory BehaviorSources=new(Array.Empty<BehaviorSource>()); }
}
