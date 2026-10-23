using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Responses;
using Verse;

internal static partial class Program
{
    private const string MaintenanceFact = "fixture.r2/MaintainedSubject";
    private static readonly HashSet<string> MaintainedSubjects = new();
    public sealed class MaintainedSubjectProvider : IUsDefinitionProvider
    {
        public void Collect(ProviderBatch batch) => batch.AddFactSource(new MaintainedSubjectFact());
    }
    private sealed class MaintainedSubjectFact : IUsFactSource
    {
        public string Key => MaintenanceFact;
        public Truth Capture(string subject) => MaintainedSubjects.Contains(subject) ? Truth.True : Truth.False;
    }
    private static void DeclareMaintainedChild(UsVoicePackDef pack, string id, string parent, int priority = 100)
    {
        if (!DefDatabase<UsProviderDef>.AllDefsListForReading.Any(x => x.providerClass == typeof(MaintainedSubjectProvider)))
            DefDatabase<UsProviderDef>.AllDefsListForReading.Add(new UsProviderDef { providerClass = typeof(MaintainedSubjectProvider) });
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = id, BaseAction = parent, Priority = priority, Interval = 120, Sustained = true, Condition = new Condition("ref", MaintenanceFact) } });
        pack.declaration.Entries.Add(new VoiceEntry { Key = id, Action = id, Sound = "Ratkin1" });
    }
    private static void MaintainSubject(Pawn pawn, bool enabled)
    {
        if (enabled) MaintainedSubjects.Add(SqueakerNotifications.SubjectKey(pawn));
        else MaintainedSubjects.Remove(SqueakerNotifications.SubjectKey(pawn));
    }
}
