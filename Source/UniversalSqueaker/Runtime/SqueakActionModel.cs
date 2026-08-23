namespace UniversalSqueaker;

// 适配层产品元数据（0.3.1 波 4a 结构：SqueakActionPlan/SqueakTriggerInvocation/SqueakActionDefinition
// 等纯类型位于 Pure/，本文件只保留 US 动作常量与绑定 SqueakActionConfig 的工厂，不入内核编译单元）。

/// <summary>Single source for the 17 built-in actions and their US_ SoundDef keys.
/// Crying/Giggling AudioKeys point at SoundDefs that a VoicePack may declare (GetNamedSilentFail
/// degrades silently when absent); the built-in fallback table is data-driven and carries no seed.</summary>
public static class SqueakActionDefinitions
{
    public const int Count = 17;
    private static readonly SqueakActionDefinition[] definitions =
    {
        new(SqueakAction.Call, "US.Action.Call", "US_Call", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Eat, "US.Action.Eat", "US_Eat", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Sleep, "US.Action.Sleep", "US_Sleep", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Wounded, "US.Action.Wounded", "US_Wounded", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Select, "US.Action.Select", "US_Select", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Move, "US.Action.Move", "US_Move", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Social, "US.Action.Social", "US_Social", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Joy, "US.Action.Joy", "US_Joy", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Death, "US.Action.Death", "US_Death", SqueakVocalGatePolicy.ExemptTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Draft, "US.Action.Draft", "US_Draft", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.ActiveCommand, SqueakActionScope.ActiveCommand),
        new(SqueakAction.Undraft, "US.Action.Undraft", "US_Undraft", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.ActiveCommand, SqueakActionScope.ActiveCommand),
        new(SqueakAction.Attack, "US.Action.Attack", "US_Attack", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence | SqueakActionScopeSupport.ActiveCommand, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Work, "US.Action.Work", "US_Work", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence | SqueakActionScopeSupport.ActiveCommand, SqueakActionScope.ActiveCommand),
        new(SqueakAction.Equip, "US.Action.Equip", "US_Equip", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.ActiveCommand, SqueakActionScope.ActiveCommand),
        new(SqueakAction.MentalBreak, "US.Action.MentalBreak", "US_MentalBreak", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Crying, "US.Action.Crying", "US_Crying", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
        new(SqueakAction.Giggling, "US.Action.Giggling", "US_Giggling", SqueakVocalGatePolicy.ApplyTalkingGate, SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence),
    };
    public static SqueakActionDefinition Get(SqueakAction action) => definitions[(int)action];
    public static bool IsKnown(SqueakAction action) => (uint)action < Count;
    public static SqueakActionScope NormalizeScope(SqueakAction action, SqueakActionScope scope)
    {
        if (scope == SqueakActionScope.Disabled) return scope;
        SqueakActionDefinition definition = Get(action);
        SqueakActionScopeSupport needed = scope == SqueakActionScope.ActiveCommand ? SqueakActionScopeSupport.ActiveCommand : SqueakActionScopeSupport.AnyOccurrence;
        return (definition.SupportedScopes & needed) != 0 ? scope : definition.DefaultScope;
    }
}

/// <summary>Adapter-side plan factories: bind the pure SqueakActionPlan shape to US action metadata
/// and the XML-driven SqueakActionConfig. This file is not part of the kernel compile set.</summary>
public static class SqueakActionPlanFactory
{
    public static SqueakActionPlan Unconfigured(SqueakAction action) => new(SqueakActionDefinitions.Get(action), false, SqueakTriggerMode.RandomOneShot, 300, .02f, false, SqueakCooldownClock.GameTicks);

    public static SqueakActionPlan FromLegacy(SqueakActionConfig config) => new(SqueakActionDefinitions.Get(config.action), true, config.mode, config.minIntervalTicks, config.probabilityPerCheck, config.ignoreGlobalCooldown, config.cooldownClock);
}
