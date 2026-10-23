using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;
namespace UniversalSqueaker.KernelHostTests;
/// <summary>The current scope editor must not offer a write without its exact child target.
/// Old mood sliders are retired; this observes real scope controls, navigation, reason text and actual source guards.</summary>
internal static class XenotypeEmptyTargetLaneTests
{
    public static int RunAll()
    {
        var field=typeof(UiHost).Assembly.GetType("FerriteLib.UiKit.Kernel.UiNative",true)!.GetField("ButtonOverride",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public)!;
        foreach(string language in new[]{"English","ChineseSimplified"})
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                foreach(float width in new[]{760f,480f})
                {
                    var valid=new RecordingSettingsSource{RichData=true,MirrorTuningWrites=true,TuningDomains=new[]{new TuningDomainOptionView("human","Child","Child")}};
                    valid.SetTuningLayer(2);valid.SetTuningDomain("human","Child");
                    var empty=new RecordingSettingsSource{RichData=true,EmptyXenotypeTarget=true};
                    int Capture(RecordingSettingsSource source)
                    {
                        using var host=UsKernelSettingsHost.Create(source,new Program.StubMetrics());
                        host.Bindings.Invoke("set-tab","Tuning");
                        var snapshot=host.MeasureAndArrange(new Vector2(width,720));
                        int buttons=0;
                        field.SetValue(null,(Func<Rect,bool>)(rect=>{if(host.Session.ActiveNode.ElementId=="scope-tree")buttons++;return false;}));
                        try { host.DrawChecked(new Rect(0,0,width,720)); }
                        finally {field.SetValue(null,null);}
                        if(source.EmptyXenotypeTarget)
                        {
                            if(Fl08PublicObservation.Popup(host.Session,out _))throw new Exception("empty scope target opened a popup");
                            host.Bindings.Set("tuning-area",1);
                            var details=host.MeasureAndArrange(new Vector2(width,720));host.DrawChecked(new Rect(0,0,width,720));
                            if(details.RectById["scope-tree"].height<=0)throw new Exception("details loses current context and measured body");
                        }
                        return buttons;
                    }
                    int active=Capture(valid),inert=Capture(empty);
                    if(inert>=active || inert<5)throw new Exception("empty target must lose scope interactions but retain layer/area navigation: "+active+"/"+inert);
                    Console.WriteLine("XG current controls "+language+"/"+width+" valid="+active+" empty="+inert);
                }
            }
            finally{Program.SetTranslatorResolver(null);}
        }
        var settings=new GameSettings();var actual=new UsKernelSettingsSource(settings);
        actual.ViewState.TuningLayer=2;actual.ViewState.TuningRaceDefName="";actual.ViewState.TuningXenotypeDefName="";
        actual.SetActionScope(CoreActions.Work,BehaviorScope.Disabled);
        actual.SetSelectParameter("awake","False");actual.SetEatParameter("phase","Execution");
        if(settings.actionTuning.Count!=0)throw new Exception("empty child identity silently wrote profile");
        actual.SetTuningLayer(0);actual.SetActionScope(CoreActions.Work,BehaviorScope.Disabled);
        if(settings.ReadPlayerActionOverride(CoreActions.Work,"","").Scope!=BehaviorScope.Disabled)throw new Exception("legitimate profile write was blocked with empty targets");
        Console.WriteLine("XG production source rejects empty child while legitimate profile stays writable PASS");return 0;
    }
}