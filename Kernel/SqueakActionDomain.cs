namespace SqueakyRatkin;

// 动作域枚举（0.3.1 波 1 移入 Kernel/；零游戏运行时引用，namespace 不变以维持存档按名序列化 ABI）。
// 红线：SqueakAction 为 append-only，序数稳定。0.3.1 波 3c 已 append Crying(15)/Giggling(16)（0–14 不动，
// 17 动作 ABI 定型）；内置表不列这两项（无内置 SoundDef = 默认静默，pack 声明才发声）。

public enum SqueakMood { Good, Neutral, Bad, Break }

// Keep the original nine serialized enum values stable. New built-ins are append-only.
public enum SqueakAction { Call, Eat, Sleep, Wounded, Select, Move, Social, Joy, Death, Draft, Undraft, Attack, Work, Equip, MentalBreak, Crying, Giggling }

public enum SqueakCooldownClock { GameTicks, Realtime }

/// <summary>触发模式,由 XML 配置驱动,C# 通用适配。</summary>
public enum SqueakTriggerMode
{
    EachTime,
    RandomOneShot,
    External,
    Sustained
}
