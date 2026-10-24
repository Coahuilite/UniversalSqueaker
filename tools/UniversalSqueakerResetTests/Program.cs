using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;
using Verse;
namespace UniversalSqueaker.ResetTests;
/// <summary>Production current settings, reset, ExposeData and atomic files; Scribe/game boundaries are doubles.</summary>
internal static class Program
{
    private static int checks;
    private static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
    private static int Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"us-r2-reset-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);GenFilePaths.ConfigFolderPath=root;
        try
        {
            var settings=new GameSettings();SqueakerMod.Settings=settings;
            var mod=new SqueakerMod(new ModContentPack{FolderName="ResetFixture"});
            void Single(Func<SqueakResetOutcome> reset,string label,bool publishes=true)
            {
                long before=settings.CaptureBehaviorConfiguration().Id,queued=mod.SaveQueueRequestCount;
                Check(reset()==SqueakResetOutcome.Applied,label+" applied");
                Check(settings.CaptureBehaviorConfiguration().Id==before+(publishes?1:0) && mod.SaveQueueRequestCount==queued+1,label+" one publication/queue shape");
                before=settings.CaptureBehaviorConfiguration().Id;queued=mod.SaveQueueRequestCount;
                Check(reset()==SqueakResetOutcome.NoChange && settings.CaptureBehaviorConfiguration().Id==before && mod.SaveQueueRequestCount==queued,label+" repeat has no side effects");
            }
            settings.voicePackMode=SqueakVoicePackMode.Remix;settings.globalVolumeFactor=0;settings.activity=2;
            settings.scaleFrequencyWithTalking=false;settings.allowEasterEggSounds=true;settings.showCameraIndicator=true;
            settings.localizeDebugActions=true;settings.devLoggingMode=SqueakDevLoggingMode.Enabled;
            settings.distancePreset=SqueakDistancePreset.Custom;settings.distanceRange=new FloatRange(20,60);
            settings.uiSwatchId="royalty";settings.uiIntroCollapsed=true;
            settings.voicePackSelections.Add(new VoicePackSelectionRecord{raceDefName="RaceA"});
            settings.actionTuning.Add(new ActionTuningRecord{actionKey=CoreActions.Work,raceDefName="RaceA",hasVolume=true,volume=0,sourcePresetDefName="ResetOnly"});
            var domain=new BehaviorDomain("Pawn","","RaceA");
            var author=new BehaviorSource{Id="author/Source",Domain=domain,Contributions=new(){new(){Action=CoreActions.Work,Values=new BehaviorValues{Volume=.7}}}};
            settings.TryChangeBehaviorSource(domain,author.Id,new BehaviorSourceDirectory(new[]{author}),_=>true,out _);
            string authorSnapshot=settings.behaviorSourcesXml;
            Single(settings.ResetAllSettings,"full current player settings pollution");
            Check(settings.behaviorSourcesXml==authorSnapshot && BehaviorSourceState.FromXml(authorSnapshot).Read(domain).SourceId==author.Id,
                "global reset preserves original author binding and last-good snapshot bytes");
            var expected=new GameSettings();
            foreach(var field in typeof(GameSettings).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
            {
                var actual=field.GetValue(settings);var baseline=field.GetValue(expected);
                if(actual is System.Collections.ICollection collection)Check(collection.Count==0,"global reset clears "+field.Name);
                else Check(Equals(actual,field.Name=="behaviorSourcesXml" ? authorSnapshot : baseline),"global reset field ownership "+field.Name);
            }
            settings.globalVolumeFactor=.4f;settings.distancePreset=SqueakDistancePreset.Strong;settings.distanceRange=new FloatRange(15,65);
            Single(settings.ResetDistanceDefaults,"distance-only reset",false);Check(settings.globalVolumeFactor==.4f,"distance-only preserves volume");
            Single(()=>settings.ResetNormalArea(SqueakNormalResetArea.Distance),"visible output-area reset");
            ActionTuningRecord Row(string key,string race,string child="")=>new(){actionKey=key,raceDefName=race,xenotypeDefName=child,hasPitch=true,pitch=2,sourcePresetDefName="Template"};
            settings.actionTuning.AddRange(new[]{Row(CoreActions.Work,"RaceA"),Row(CoreActions.Work,"RaceA"),Row(CoreActions.Eat,"RaceA"),Row(CoreActions.Work,"RaceB"),Row(CoreActions.Work,"RaceA","Child"),Row(CoreActions.Work,"")});
            settings.behaviorSourcesXml="<behaviorSources format='1'/>";
            Single(()=>settings.ResetActionTuningRow(CoreActions.Work,"RaceA",""),"exact row including duplicates and template anchor");
            Check(settings.actionTuning.Count==4 && settings.actionTuning.Any(x=>x.raceDefName=="RaceB") && settings.actionTuning.Any(x=>x.xenotypeDefName=="Child"),"row preserves other action/race/child/profile");
            Single(()=>settings.ResetActionTuningArea("RaceA",""),"exact action area");
            Single(()=>settings.ResetActionTuningArea("",""),"legitimate profile area");
            Check(settings.actionTuning.Count==2 && settings.behaviorSourcesXml!="","local area preserves author source intent and other exact domains");
            Check(settings.ResetActionTuningRow("Work","","")==SqueakResetOutcome.Rejected && settings.ResetActionTuningArea("","Child")==SqueakResetOutcome.Rejected,"invalid ID/child never fall through to profile");
            settings.SetSelectParameter("awake",false);settings.SetEatParameter("drugScope",(int)EatDrugScope.OnlyDrugs);
            settings.allowEasterEggSounds=true;settings.scaleFrequencyWithTalking=false;
            Single(()=>settings.ResetNormalArea(SqueakNormalResetArea.Basics),"basic system and profile-parameter reset");
            Check(settings.actionTuning.Count(x=>x.raceDefName!="")==2,"Basics does not clear Race/child player data");
            settings.devLoggingMode=SqueakDevLoggingMode.Enabled;settings.localizeDebugActions=true;
            Single(()=>settings.ResetNormalArea(SqueakNormalResetArea.Diagnostics),"diagnostics-only reset",false);
            Check(settings.ResetNormalArea((SqueakNormalResetArea)999)==SqueakResetOutcome.Rejected,"unknown reset area is rejected");
            string fallback=Path.Combine(root,"separate-final-fallback.xml");File.WriteAllText(fallback,"<preserved/>");byte[] fallbackBytes=File.ReadAllBytes(fallback);
            string file=Path.Combine(root,"settings.xml");SafeSaver.Save(file,"SettingsBlock",()=>{var value=settings;Scribe_Deep.Look(ref value,"ModSettings");});
            GameSettings loaded=null!;Scribe.loader.InitLoading(file);Scribe_Deep.Look(ref loaded,"ModSettings");Scribe.loader.FinalizeLoading();
            Check(loaded.actionTuning.Count==settings.actionTuning.Count && loaded.behaviorSourcesXml==settings.behaviorSourcesXml,"production ExposeData with Scribe double restores exact surviving data");
            loaded.ResetAllSettings();SafeSaver.Save(file,"SettingsBlock",()=>Scribe_Deep.Look(ref loaded,"ModSettings"));
            Check(File.ReadAllBytes(fallback).SequenceEqual(fallbackBytes),"settings reset/save never touches separate fallback bytes");
            File.WriteAllText(file,"<SettingsBlock><ModSettings><responseConfigurationFormat>99</responseConfigurationFormat><outputVolume>0.2</outputVolume></ModSettings></SettingsBlock>");
            GameSettings unreadable=null!;Scribe.loader.InitLoading(file);Scribe_Deep.Look(ref unreadable,"ModSettings");Scribe.loader.FinalizeLoading();unreadable.ResetAllSettings();
            Check(unreadable.IsConfigurationUnreadable && !unreadable.TryReplaceBehaviorOverride("Pawn","","RaceA","",CoreActions.Work,new BehaviorValues{Volume=0},()=>true,out _),"reset does not disguise unreadable format as a migration");
            Console.WriteLine("R2 production reset/ExposeData/atomic-file with Scribe doubles PASS "+checks);return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{Directory.Delete(root,true);}
    }
}
