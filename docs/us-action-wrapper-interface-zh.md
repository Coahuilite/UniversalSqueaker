# S0 ActionEntry / TriggerBinding 接口设计稿（L2）

> 状态：定稿（2026-08-25，主会话产出）。
> 范围：S2 动作入口 wrapper 化的 C# 类型形态（L2）；L1 字段契约已在计划 §13.3 锁定；L3 逐动作实现属 S2 编码期。
> 依据：SqueakActionPlan.cs（纯数据 struct）、SqueakActionModel.cs（17 动作工厂）、SqueakGlobalActionPolicy.cs（Publish/GetScope）、CompSqueaker.cs（Notify_* + TryTrigger + 5 定长数组）、8 个 Harmony patch（行为等价清单见 docs/us-s0-worknotes-zh.md §8）。

## 1. 定位与边界

- ActionEntry/TriggerBinding 是**内部运行时重构**，不是对外 ABI（作者 XML 面仍是 CompProperties_Squeaker，不变）。
- 唯一进存档的部分是 ActionKey(string)，内置键 = BuiltInActionKeys（枚举名），已锁定。
- 跨层：ActionEntry 元数据（ActionKey/BindingKind/DefaultPlan/BuiltInAudioKey）中立；绑定实现（StateProbe/VersePatch/Sustainer 维护）外置（Verse 适配层）。

## 2. 类型形态决策

### 2.1 ActionEntry：abstract class（非 interface）

理由：
- 需要共享字段（ActionKey/Binding/DefaultPlan/BuiltInAudioKey）+ 统一构造/校验逻辑。
- 内置 17 项 + 外部项都需要实例，abstract class 带 protected 构造 + 派生项更清晰。
- net472 无 record，interface 无法携带字段默认值。

```csharp
// 中立元数据（Kernel 可引用，零 Verse）。
public abstract class ActionEntry
{
    public abstract string ActionKey { get; }          // 内置键 = 枚举名，外部键 = 注册方声明
    public abstract BindingKind Kind { get; }          // PeriodicState | VerseEvent | Sustainer
    public abstract SqueakActionDefinition DefaultPlan { get; } // 默认触发协议（进 baseline）
    public abstract string BuiltInAudioKey { get; }    // 内置 SoundDef 键（无内置则 null/空）
    public bool IsBuiltIn => BuiltInActionKeys.Contains(ActionKey);
}
```

### 2.2 TriggerBinding：外置绑定（Verse 适配层）

```csharp
// 外置层（引用 Verse/RimWorld），只在采样/让位时被调用。
public abstract class TriggerBinding
{
    public abstract BindingKind Kind { get; }
    // PeriodicState：从 pawn 推导当前动作（谓词集合），替代 CurrentAction 硬编码。
    public virtual SqueakAction? ProbePeriodic(Pawn pawn) => null;
    // VerseEvent：patch 已由 Harmony 注入，此处只负责派生 Origin/Source。
    public virtual SqueakTriggerInvocation DeriveInvocation(object[] args) => default;
    // Sustainer：维护 TrySpawnSustainer/Maintain/End（S3）。
    public virtual void Maintain(CompSqueaker comp) { }
    // Disabled 让位：短路不介入 → 原版通路原样发声。
    public virtual bool OnDisabled => true;
}

public enum BindingKind { PeriodicState, VerseEvent, Sustainer }
```

### 2.3 注册表：ActionEntryRegistry（单例，替代 SqueakActionDefinitions 硬编码）

```csharp
public sealed class ActionEntryRegistry
{
    public static ActionEntryRegistry Current { get; } = new();
    private readonly Dictionary<string, ActionEntry> byKey = new(StringComparer.Ordinal);
    public void Register(ActionEntry entry);          // 幂等，重复注册覆盖
    public ActionEntry? Get(string actionKey);        // 内置键必定可查，外部键按注册
    public IEnumerable<ActionEntry> All { get; }
    public int Count { get; }                          // 内置 17 + 外部 N
}
```

## 3. 与现有代码的替换关系

| 现有 | 目标 | 说明 |
|---|---|---|
| SqueakActionDefinitions（17 项 + NormalizeScope） | ActionEntryRegistry（内置 17 项由内置 ActionEntry 子类注册）+ ActionKey 映射 | NormalizeScope 逻辑并入 ActionEntry.DefaultPlan 校验 |
| SqueakActionPlanFactory.Unconfigured/FromLegacy | ActionEntry.BuildPlan(config) | 从作者 XML SqueakActionConfig 构造 plan |
| SqueakGlobalActionPolicy（Current/Publish/GetScope + 定长 scopes[]） | 分层快照（ActionTuningRecord 求值，见 S0 数据模型稿） | S2 降级删除，TryTrigger 不再查全局 policy 早退 |
| CompSqueaker 5 定长数组 | 内置仍用定长数组（17 动作），外部动作走 Dictionary<ActionKey,...> | 混合方案 |
| Notify_* + 8 个 Harmony patch | TriggerBinding 派生类 + patch 只做「调用入口替换」 | 判断条件逐条保留（行为等价清单） |
| CurrentAction（CompSqueaker.cs:225-261 硬编码谓词链） | TriggerBinding.StateProbe 集合 | 每个内置动作一个 Probe，替代 switch |

## 4. 内置 17 动作的 BindingKind 映射

| ActionKey | BindingKind | 触发通路（patch/谓词） |
|---|---|---|
| Call | PeriodicState | CurrentAction 默认（无谓词命中） |
| Eat | PeriodicState | IsEating（CurJob.def == Ingest） |
| Sleep | PeriodicState | IsSleeping（LayingInBed && rest!=null） |
| Move | PeriodicState | IsMoving（pather.Moving） |
| Social | PeriodicState | IsSocializing（job defName 含标记） |
| Joy | PeriodicState | IsJoyJob（joyKind!=null） |
| Work | PeriodicState | IsWorking（workGiverDef!=null） |
| Wounded | VerseEvent | Patch_Pawn_PostApplyDamage |
| Select | VerseEvent | Patch_Selector_Select |
| Death | VerseEvent | Patch_Pawn_Kill（Prefix） |
| Draft/Undraft | VerseEvent | Patch_DraftGizmo_Toggle |
| Attack | VerseEvent | Patch_Verb_Attack |
| Equip | VerseEvent | Patch_Pawn_EquipmentAdded |
| MentalBreak | VerseEvent | Patch_MentalBreak |
| Crying/Giggling | VerseEvent | Patch_MentalFit |

## 5. 待签收的取舍点（S2 动码前）

1. **ActionEntry 用 abstract class 还是 interface + 字段默认**：本稿选 abstract class（带字段）。若你偏好 interface + 扩展方法，S2 前可改。
2. **注册表单例 vs 每 Comp 实例**：本稿选单例 ActionEntryRegistry（内置项全局只注册一次）。若未来要 per-save 动作集，需改。
3. **SqueakAction 枚举是否保留**：本稿选「保留枚举 + 边界转换」（作者 XML 面 SqueakActionConfig.action 仍枚举），内部运行统一 ActionKey string。若你希望彻底删枚举，波及作者 ABI，需另评估。
4. **外部动作 Origin 表示**：本稿选 append `SqueakTriggerOrigin.External`（序数 11）+ BindingKind 区分（见 S0 key-and-race-context 稿 §1.6）。

## 6. S2 编码顺序（本稿落地时）

1. ActionEntry/TriggerBinding/ActionEntryRegistry 类型骨架（中立元数据 + 外置绑定）。
2. 内置 17 ActionEntry 子类 + 8 个 TriggerBinding 派生类（逐条保留 patch 判断条件）。
3. CompSqueaker 触发入口改走 Registry（TryTrigger/NotifyExternal/CompTick），5 定长数组改混合存储。
4. SqueakGlobalActionPolicy 降级 + 分层快照求值（X→R→G→Default）。
5. 枚举键消费点全量改造（见 S0 key-and-race-context 稿 §1）。
6. 动作门 allowExternalActions 总闸（非内置 ActionEntry 是否生效）。
7. Disabled 旁路（触发入口前置 + SqueakLogOnce）。
