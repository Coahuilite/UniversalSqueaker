using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.OneShot;
using Verse.Sound;

internal static partial class Program
{
    private static void DaSelectMissingTarget()
    {
        var selection = AccessTools.Method(typeof(Selector), "SelectInternal", new[] { typeof(object), typeof(bool), typeof(bool) });
        var feedback = AccessTools.Method(typeof(Selector), "PlaySelectionSoundFor", new[] { typeof(object) });
        Check((selection == null) != (feedback == null), "exactly one actual Selector target is absent from this compiled platform double");
        Console.WriteLine("Actual target presence: SelectInternal=" + (selection != null) + " PlaySelectionSoundFor=" + (feedback != null));
        Exception? patchFailure = null;
        var harmony = new Harmony("us.s2.controlled.adapter.fixture");
        try { harmony.PatchAll(Assembly.GetExecutingAssembly()); }
        catch (Exception error)
        {
            patchFailure = error;
            // Preserve each original type/message; this host cannot reliably format Exception.ToString().
            for (Exception? current = error; current != null; current = current.InnerException)
                Console.Error.WriteLine("PATCHALL_FAILURE " + current.GetType().FullName + ": " + current.Message);
        }
        foreach (var target in new[] { selection, feedback }.Where(x => x != null))
            Console.WriteLine("Select target patch state: " + target!.Name + " owners="
                + string.Join(",", Harmony.GetPatchInfo(target)?.Owners.AsEnumerable() ?? Enumerable.Empty<string>()));
        Check(patchFailure == null, "a missing Select target must not abort the actual Harmony.PatchAll startup call");
        Check(!UsSelectObservation.Ready, "paired readiness rejects the incomplete Select capability");
        foreach (var target in new[] { selection, feedback }.Where(x => x != null))
            Check(Harmony.GetPatchInfo(target)?.Owners.Contains(harmony.Id) != true,
                "the surviving Select target is not left with a half observer");
        string missingName = selection == null ? "SelectInternal" : "PlaySelectionSoundFor";
        var notices = Log.Lines.Where(x => x.Contains("capability=SelectObservation")).ToArray();
        Check(notices.Length == 1 && notices[0].Contains("MissingTarget") && notices[0].Contains(missingName),
            "both Prepare calls share one named missing-target diagnostic");
        Console.WriteLine(notices.Single());

        // One real binding, not a replay of the ordinary binding matrix.
        var owner = new ModContentPack { PackageId = "fixture.readiness" };
        var document = new XmlDocument(); document.LoadXml("<Defs/>");
        var node = Add(document, "ReadinessAudio");
        var asset = new LoadableXmlAsset { mod = owner };
        LoadedModManager.FixtureDocument = document;
        LoadedModManager.CombineIntoUnifiedXML(new List<LoadableXmlAsset>(), new Dictionary<XmlNode, LoadableXmlAsset> { [node] = asset });
        var sound = (SoundDef)DirectXmlLoader.DefFromNode(node, asset)!;
        DefDatabase<SoundDef>.AllDefsListForReading.Add(sound);
        UsAudioBindings.Publish();
        Check(UsAudioBindings.KeyForName(sound.defName) == "fixture.readiness/ReadinessAudio",
            "unrelated production binding patches still run after the missing Select pair is skipped");

        var pawn = new Pawn { ThingID = "ReadinessPawn", def = new ThingDef { defName = "ReadinessRace", race = new object() },
            IsPlayerControlled = true, CurJob = new Verse.AI.Job { def = new JobDef { defName = "Research" }, playerForced = true } };
        var work = new JobDriver_Research { pawn = pawn, job = pawn.CurJob, FixtureToil = new Toil() };
        pawn.jobs = new Pawn_JobTracker { curDriver = work };
        pawn.Comp = new CompSqueaker { Pawn = pawn };
        pawn.health!.hediffSet!.hediffs.Add(new Hediff_Injury { Severity = 1 });
        DefDatabase<ThingDef>.AllDefsListForReading.Add(pawn.def);
        const string child = "fixture/InjuredSelect", root = "fixture/IndependentSelect";
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = child, BaseAction = "us.core/Select", Priority = 40, Condition = new Condition("ref", "us.core/HasCurrentInjury") } });
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = root, Priority = 50, Condition = new Condition("ref", UsSelectObservation.FactKey) } });
        var pack = new UsVoicePackDef { defName = "ReadinessPack", modContentPack = owner };
        pack.declaration.Race = pawn.def.defName;
        foreach (string action in new[] { "us.core/Select", child, root, "us.core/Work" })
            pack.declaration.Entries.Add(new VoiceEntry { Key = action, Action = action, Sound = sound.defName });
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Add(pack);
        var dependentSource = new UsBehaviorSourceDef { defName = "RequiresSelect", modContentPack = owner };
        var behaviorDomain = new BehaviorDomain("Pawn", "", pawn.def.defName);
        dependentSource.declaration.Sources.Add(new BehaviorSource { Domain = behaviorDomain,
            Contributions = new List<BehaviorContribution> { new() { Action = "us.core/Work",
                Values = new BehaviorValues { Enabled = true }, RequiredFacts = new[] { UsSelectObservation.FactKey } } } });
        DefDatabase<UsBehaviorSourceDef>.AllDefsListForReading.Add(dependentSource);
        UsOneShotRuntime.Initialize();
        Check(UniversalSqueakerMod.Settings.TryChangeBehaviorSource(behaviorDomain, "fixture.readiness/RequiresSelect",
            UsOneShotRuntime.BehaviorSources, _ => true, out _), "source with an explicit Select implementation dependency retains its binding");
        var directory = new SqueakXenotypeCatalogSnapshot(); directory.RacePacks.AddRange(UsAuthorVoicePacks.Directory);
        directory.RacePacks.Single().TryGetPackKey(out string packKey);
        var domain = AudioDomains.RaceOnly(pawn.def.defName);
        SqueakRuntimeResolver.Current.Registry = new SqueakPoolRegistry(SqueakKernelAdapter.BuildEntries(directory,
            new Dictionary<AudioDomain, HashSet<string>> { [domain] = new HashSet<string> { packKey } }), BuiltInFallbackTable.Empty);
        UsOneShotRuntime.SelectDevelopmentPolicy(new UsDevelopmentPolicy(20, 0, 0, 0, 0, 1, 1));
        UsInstanceOutput.Initialize(harmony);
        var selector = new Selector(); selector.Select(pawn);
        Check(selector.IsSelected(pawn) && !UsOneShotRuntime.IsControlled(pawn),
            "vanilla selection remains intact but an incomplete observer emits no Select event or owner");
        pawn.Comp.Notify_Select();
        var session = RuntimeSession(pawn);
        Check(session.LastOutcome.Receipt == null && SoundStarter.Played.Count == 0
            && session.LastOutcome.Diagnostics.Any(x => x.Key == UsSelectObservation.FactKey && x.Reason == UsSelectObservation.FailureReason),
            "direct Select notification cannot manufacture the unavailable fact; ordinary, child and independent Select contracts stay silent with named evidence");
        Find.TickManager.TicksGame += 200;
        pawn.Comp.NotifyExternalByKey(root, SqueakTriggerOrigin.External, SqueakInvocationSource.StateEvent);
        Check(session.LastOutcome.Receipt == null && SoundStarter.Played.Count == 0,
            "an explicit registered Select-dependent author call cannot bypass the unavailable capability");
        Find.TickManager.TicksGame += 200;
        pawn.CurJob!.workGiverDef = new RimWorld.WorkGiverDef { defName = "FixtureLabelOnly" }; pawn.Comp.CompTick();
        Check(session.LastOutcome.Receipt == null && session.LastOutcome.Reason.StartsWith("MissingRequiredImplementation:", StringComparison.Ordinal)
            && SoundStarter.Played.Count == 0,
            "production source resolution pauses the explicitly dependent contribution, not a fabricated available implementation");
        Check(UniversalSqueakerMod.Settings.TryChangeBehaviorSource(behaviorDomain, null,
            UsOneShotRuntime.BehaviorSources, _ => true, out _), "forgetting only the dependent source leaves other capability state intact");
        Find.TickManager.TicksGame += 200; pawn.Comp.CompTick();
        Check(session.LastOutcome.Action == "us.core/Work" && session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched
            && SoundStarter.Played.Count == 1,
            "unrelated Work still reaches the same production owner, bound audio and dispatch despite higher Select-dependent contracts");
    }
}
