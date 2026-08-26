using System;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker;

/// <summary>
/// 内置 17 动作的 ActionEntry 注册表填充。静态构造一次性把内置项注册进 ActionEntryRegistry。
/// 元数据权威仍是 SqueakActionDefinitions；这里只做「枚举 → ActionKey string + BindingKind」的运行时投影。
/// </summary>
public static class BuiltInActionEntries
{
    private static bool registered;

    public static void EnsureRegistered()
    {
        if (registered) return;
        registered = true;
        foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction)))
        {
            string? key = ActionKey.For(action);
            if (key == null) continue;
            ActionEntryRegistry.Current.Register(new BuiltInActionEntry(action, key));
        }
    }

    /// <summary>内置动作的 BindingKind：周期状态推导 vs 外部事件。</summary>
    private static BindingKind KindFor(SqueakAction action) => action switch
    {
        SqueakAction.Call or SqueakAction.Eat or SqueakAction.Sleep or SqueakAction.Move or
        SqueakAction.Social or SqueakAction.Joy or SqueakAction.Work => BindingKind.PeriodicState,
        _ => BindingKind.VerseEvent,
    };

    private sealed class BuiltInActionEntry : ActionEntry
    {
        private readonly SqueakAction action;
        private readonly string key;
        public BuiltInActionEntry(SqueakAction action, string key) { this.action = action; this.key = key; }
        public override string ActionKey => key;
        public override BindingKind Kind => KindFor(action);
        public override SqueakActionDefinition DefaultPlan => SqueakActionDefinitions.Get(action);
        public override string BuiltInAudioKey => SqueakActionDefinitions.Get(action).AudioKey;
    }
}
