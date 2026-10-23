namespace UniversalSqueaker.Authoring;

/// <summary>Opt-in documentation fixture: always true, not a game duty detector. Not discovered automatically.</summary>
public sealed class ControlledExampleProvider : IUsDefinitionProvider
{
    public void Collect(ProviderBatch batch) => batch.AddFactSource(new OnDutyFact());
    private sealed class OnDutyFact : IUsFactSource
    {
        public string Key => "example/OnDuty";
        public Responses.Truth Capture(string subject) => Responses.Truth.True;
    }
}
