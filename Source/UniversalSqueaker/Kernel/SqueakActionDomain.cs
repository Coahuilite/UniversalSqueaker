namespace UniversalSqueaker;

// Official 0.6 package inventory; no platform references. Third-party contracts use independent keys.

public enum SqueakMood { Good, Neutral, Bad, Break }

public enum SqueakAction { Call, Eat, Sleep, Wounded, Select, Move, Social, Joy, Death, Draft, Undraft, Attack, Work, Equip, MentalBreak }

public enum SqueakCooldownClock { GameTicks, Realtime }

/// <summary>触发模式,由 XML 配置驱动,C# 通用适配。</summary>
public enum SqueakTriggerMode
{
    EachTime,
    RandomOneShot,
    External,
    Sustained
}
