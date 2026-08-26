using System;
using System.Collections.Generic;
using Verse;

namespace UniversalSqueaker;

/// <summary>Trigger binding kind: how a trigger is sampled/derived from the game world.</summary>
public enum BindingKind { PeriodicState, VerseEvent, Sustainer }

/// <summary>
/// 中立动作入口元数据（零 Verse 依赖）。一个动作一项；ActionKey 是字符串（内置键 = BuiltInActionKeys）。
/// 元数据中立；绑定实现（StateProbe/VersePatch/Sustainer）外置在 TriggerBinding 派生类。
/// </summary>
public abstract class ActionEntry
{
    public abstract string ActionKey { get; }
    public abstract BindingKind Kind { get; }
    /// <summary>内置动作的默认触发协议元数据；外部动作无 SqueakActionDefinition 时返回 default。</summary>
    public abstract SqueakActionDefinition DefaultPlan { get; }
    /// <summary>内置 SoundDef 键（无内置则空字符串）。</summary>
    public abstract string BuiltInAudioKey { get; }
    /// <summary>外置触发绑定（外部动作携带；内置动作的绑定由 static 聚合绑定覆盖）。</summary>
    public virtual TriggerBinding? Binding => null;
    public bool IsBuiltIn => UniversalSqueaker.Kernel.BuiltInActionKeys.Contains(ActionKey);
}

/// <summary>
/// 外置触发绑定（引用 Verse/RimWorld）。只在采样/让位时被调用；底层忠实路由原版通路。
/// 内置动作的派生类逐条保留现有 Harmony patch 的判断条件（行为等价清单见 us-s0-worknotes-zh.md §8）。
/// </summary>
public abstract class TriggerBinding
{
    public abstract BindingKind Kind { get; }
    /// <summary>PeriodicState：从 pawn 推导当前动作（谓词集合），替代 CurrentAction 硬编码 switch。</summary>
    public virtual SqueakAction? ProbePeriodic(Pawn pawn) => null;
    /// <summary>VerseEvent：patch 已由 Harmony 注入，此处派生 Origin/Source。</summary>
    public virtual SqueakTriggerInvocation DeriveInvocation() => default;
    /// <summary>Sustainer：维护 TrySpawnSustainer/Maintain/End（S3 落地）。</summary>
    public virtual void Maintain(CompSqueaker comp) { }
    /// <summary>Disabled 让位：true = 短路不介入，原版通路原样发声。</summary>
    public virtual bool OnDisabled => true;
}

/// <summary>
/// 动作入口注册表（单例）。替代 SqueakActionDefinitions 硬编码 17 项；内置项由内置 ActionEntry 子类注册，
/// 外部项由 mod 运行时注册。allowExternalActions 总闸控制非内置项是否生效（动作门）。
/// </summary>
public sealed class ActionEntryRegistry
{
    public static ActionEntryRegistry Current { get; } = new ActionEntryRegistry();
    private readonly Dictionary<string, ActionEntry> byKey = new(StringComparer.Ordinal);
    private bool allowExternalActions;

    private ActionEntryRegistry() { }

    public void Register(ActionEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.ActionKey)) return;
        byKey[entry.ActionKey] = entry;
    }

    public ActionEntry? Get(string actionKey) => !string.IsNullOrEmpty(actionKey) && byKey.TryGetValue(actionKey, out ActionEntry? e) ? e : null;
    public IEnumerable<ActionEntry> All => byKey.Values;
    public int Count => byKey.Count;

    /// <summary>动作门总闸：非内置 ActionEntry 是否生效。开时外部项参与触发，关时仅内置项。</summary>
    public bool AllowExternalActions { get => allowExternalActions; set => allowExternalActions = value; }

    /// <summary>内置键（BuiltInActionKeys）是否已注册。</summary>
    public bool IsRegistered(string actionKey) => !string.IsNullOrEmpty(actionKey) && byKey.ContainsKey(actionKey);
}
