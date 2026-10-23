namespace UniversalSqueaker;

/// <summary>Existing XML-driven action configuration; separate from the Verse Comp container.</summary>
public class SqueakActionConfig
{
    public SqueakAction action = SqueakAction.Call;
    public SqueakTriggerMode mode = SqueakTriggerMode.RandomOneShot;
    public int minIntervalTicks = 300;
    public float probabilityPerCheck = 0.02f;
    public bool ignoreGlobalCooldown = false;
    public SqueakCooldownClock cooldownClock = SqueakCooldownClock.GameTicks;
}
