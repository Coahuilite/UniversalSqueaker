using System;

namespace UniversalSqueaker;

/// <summary>
/// 外部事件动作的 Origin 固定映射（VerseEvent 绑定）。替代每个 Notify_XXX 里硬编码的 SqueakTriggerOrigin。
/// Source 派生（StateEvent/PlayerSelection/ActiveCommand）保留在调用侧，因为它依赖 pawn 运行态。
/// </summary>
public static class VerseEventBinding
{
    public static SqueakTriggerOrigin OriginFor(SqueakAction action) => action switch
    {
        SqueakAction.Wounded => SqueakTriggerOrigin.Wounded,
        SqueakAction.Select => SqueakTriggerOrigin.Select,
        SqueakAction.Death => SqueakTriggerOrigin.Death,
        SqueakAction.Draft => SqueakTriggerOrigin.Draft,
        SqueakAction.Undraft => SqueakTriggerOrigin.Undraft,
        SqueakAction.Attack => SqueakTriggerOrigin.Attack,
        SqueakAction.Equip => SqueakTriggerOrigin.Equip,
        SqueakAction.MentalBreak => SqueakTriggerOrigin.MentalBreak,
        SqueakAction.Crying => SqueakTriggerOrigin.Crying,
        SqueakAction.Giggling => SqueakTriggerOrigin.Giggling,
        _ => SqueakTriggerOrigin.External,
    };

    /// <summary>默认 Source：大多数外部事件是 StateEvent；点选/主动指令由调用侧显式覆盖。</summary>
    public static SqueakInvocationSource DefaultSource(SqueakAction action) => action switch
    {
        SqueakAction.Select => SqueakInvocationSource.PlayerSelection,
        SqueakAction.Draft or SqueakAction.Undraft or SqueakAction.Equip => SqueakInvocationSource.ActiveCommand,
        _ => SqueakInvocationSource.StateEvent,
    };
}
