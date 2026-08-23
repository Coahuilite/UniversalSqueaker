namespace SqueakyRatkin;

// 触发模型纯类型（0.3.1 波 4a 漏斗纯逻辑提取：SqueakActionPlan/SqueakTriggerInvocation → 纯文件 + harness 链接）。
// 本文件零 Verse 引用；SqueakActionModel 出 harness（SR 产品常量不入内核编译单元），
// 产品元数据 SqueakActionDefinitions 与绑定 SqueakActionConfig 的工厂留在适配层 SqueakActionModel.cs。
// namespace 保持 SqueakyRatkin（存档按名序列化 ABI 不变）。

public enum SqueakVocalGatePolicy { ApplyTalkingGate, ExemptTalkingGate }
public enum SqueakActionScope { Disabled, AnyOccurrence, ActiveCommand }

[System.Flags]
public enum SqueakActionScopeSupport { None = 0, AnyOccurrence = 1, ActiveCommand = 2 }

/// <summary>Fixed built-in action metadata shape; this deliberately is not an extensible registry.
/// The type is pure data; the authoritative values live in adapter-side <c>SqueakActionDefinitions</c>.</summary>
public readonly struct SqueakActionDefinition
{
    public readonly SqueakAction Action;
    public readonly string DisplayKey;
    public readonly string AudioKey;
    public readonly SqueakVocalGatePolicy VocalGatePolicy;
    public readonly SqueakActionScopeSupport SupportedScopes;
    public readonly SqueakActionScope DefaultScope;

    public SqueakActionDefinition(SqueakAction action, string displayKey, string audioKey, SqueakVocalGatePolicy vocalGatePolicy, SqueakActionScopeSupport supportedScopes, SqueakActionScope defaultScope)
    { Action = action; DisplayKey = displayKey; AudioKey = audioKey; VocalGatePolicy = vocalGatePolicy; SupportedScopes = supportedScopes; DefaultScope = defaultScope; }
}

/// <summary>Per-Comp fixed action plan populated from XML. Missing actions remain unconfigured.
/// 纯数据形状；绑定产品元数据的工厂（Unconfigured/FromLegacy）在适配层 <c>SqueakActionPlanFactory</c>。</summary>
public readonly struct SqueakActionPlan
{
    public readonly SqueakActionDefinition Definition;
    public readonly bool Configured;
    public readonly SqueakTriggerMode Mode;
    public readonly int MinIntervalTicks;
    public readonly float ProbabilityPerCheck;
    public readonly bool IgnoreGlobalCooldown;
    public readonly SqueakCooldownClock CooldownClock;

    public SqueakActionPlan(SqueakActionDefinition definition, bool configured, SqueakTriggerMode mode, int minIntervalTicks, float probabilityPerCheck, bool ignoreGlobalCooldown, SqueakCooldownClock cooldownClock)
    { Definition = definition; Configured = configured; Mode = mode; MinIntervalTicks = minIntervalTicks; ProbabilityPerCheck = probabilityPerCheck; IgnoreGlobalCooldown = ignoreGlobalCooldown; CooldownClock = cooldownClock; }
}

public enum SqueakTriggerOrigin { Periodic, Wounded, Select, Death, Draft, Undraft, Attack, Equip, MentalBreak, Crying, Giggling }
public enum SqueakInvocationSource { Periodic, StateEvent, PlayerSelection, ActiveCommand }

/// <summary>Explicit trigger source; non-periodic hooks preserve legacy probability skipping.
/// 0.3.2 身份门控纯标志：PlayerSelection/ActiveCommand 派生 IsPlayerInitiated（需玩家可控 pawn），
/// PlayerSelection 另派生 RequiresResponsivePawn（额外要求 !Downed && Awake）。Verse 采样留在适配层。</summary>
public readonly struct SqueakTriggerInvocation
{
    public readonly SqueakTriggerOrigin Origin;
    public readonly SqueakInvocationSource Source;
    public bool SkipsRandomOneShotProbability => Origin != SqueakTriggerOrigin.Periodic;
    public bool IsExternal => Origin != SqueakTriggerOrigin.Periodic;
    public bool IsActiveCommand => Source == SqueakInvocationSource.ActiveCommand;
    public bool IsPlayerInitiated => Source == SqueakInvocationSource.PlayerSelection || Source == SqueakInvocationSource.ActiveCommand;
    public bool RequiresResponsivePawn => Source == SqueakInvocationSource.PlayerSelection;

    public SqueakTriggerInvocation(SqueakTriggerOrigin origin, SqueakInvocationSource source) { Origin = origin; Source = source; }
}
