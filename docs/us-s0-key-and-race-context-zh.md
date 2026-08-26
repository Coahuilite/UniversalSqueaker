# S0 枚举键消费点 inventory + Race context 设计

> 状态：定稿（2026-08-25，主会话产出）。
> 范围：S2 的 H4（ActionKey 字符串化波及面）与 H3（Race 层 context）硬前置。

## 1. SqueakAction 枚举 → ActionKey 字符串消费点 inventory（H4）

内核侧已存在双向映射：Kernel/ActionKey.cs（ActionKey.For / TryParseBuiltIn）、Kernel/BuiltInActionKeys.cs（17 键 append-only，序数对齐 SqueakAction）。
以下为 `SqueakAction` 作为键的全部消费点及字符串化改造方案。

### 1.1 定长数组（CompSqueaker，按 SqueakActionDefinitions.Count 索引）

| 字段 | 位置 | 改造方案 | 风险 |
|---|---|---|---|
| actionPlans[] | CompSqueaker.cs:158 | 保留定长数组（计划按 17 内置动作预分配），仅边界转换：外部动作键不入数组，走 Dictionary<ActionKey,...> 分支 | 低 |
| lastTriggerTick[] | CompSqueaker.cs:159 | 同上 | 低 |
| lastTriggerRealTime[] | CompSqueaker.cs:160 | 同上 | 低 |
| periodicStartupPhaseMaterialized[] | CompSqueaker.cs:171 | 同上 | 低 |
| periodicStartupReadyTicks[] | CompSqueaker.cs:172 | 同上 | 低 |

**结论**：内置动作仍可保留定长数组（性能 + 17 动作已定型），外部动作走 Dictionary<ActionKey,...> 增量存储。这是「保留枚举仅边界转换」的混合方案。

### 1.2 Dictionary<SqueakAction,...> 与 List 按 (int)action 索引

| 消费点 | 位置 | 改造方案 |
|---|---|---|
| SoundCacheMixed | CompSqueaker.cs:153 | 改 Dictionary<string,...>（键=ActionKey） |
| MissingSoundWarnings | CompSqueaker.cs:154 | 改 HashSet<string>（ActionKey） |
| moodModMap | CompSqueaker.cs:161 | 不涉及 action（键=SqueakMood），不动 |
| SqueakGlobalActionPolicy.scopes[] | SqueakGlobalActionPolicy.cs:13,21,30 | S2 整体降级为分层快照（见 S0 数据模型稿），此定长数组删除 |
| SqueakActionDefinitions.definitions[] | SqueakActionModel.cs:12 | 内置 17 项，保留（这是 ActionKey 的内置元数据权威，不是消费点） |

### 1.3 Enum.GetValues(typeof(SqueakAction)) 循环

| 位置 | 用途 | 改造方案 |
|---|---|---|
| SqueakRuntimeResolver.cs:138 | 收集内置 SoundDef known 集 | 改遍历 BuiltInActionKeys.All |
| SqueakRuntimeResolver.cs:194 | BuildFallback 收集 known | 同上 |
| CompSqueaker.cs:830 | EnsureSoundCache 填 SoundCacheMixed | 改遍历 BuiltInActionKeys.All |

### 1.4 Scribe 序列化字段

| 字段 | 位置 | 改造方案 |
|---|---|---|
| GlobalActionEnabledRecord.action | SqueakXenotypePresetModels.cs:58,66 | 随 S2 迁移到 ActionTuningRecord.actionKey(string)，旧字段删除 |
| XenotypeActionBehaviorOverride.action | SqueakXenotypePresetModels.cs:33,43 | 同上 |
| SqueakActionConfig.action | CompSqueaker.cs:125 | 作者 XML 面，保留枚举（作者 ABI 不变，Kernel 边界转换） |
| SqueakVoicePackAction.action | SqueakVoicePackModels.cs | 作者 XML 面，保留枚举 |

### 1.5 日志/统计字段

| 消费点 | 位置 | 改造方案 |
|---|---|---|
| SqueakLogData.Action | SqueakLogProtocol.cs:16 | 已是 string，无需改（调用侧 ActionKey.For 传入） |
| SqueakRecentOutcome（若含 SqueakAction） | 需 grep 确认 | 改 ActionKey string |
| SqueakActionPlan.Definition.Action | Pure/SqueakActionPlan.cs | 纯数据 struct，S2 改 Definition 携带 ActionKey string（内置元数据仍由 ActionKey.For 注入） |

### 1.6 外部动作 Origin/Source 表示（SqueakTriggerOrigin 是闭合枚举，11 值，无法表示外部动作）

- 现状：SqueakTriggerOrigin（Pure/SqueakActionPlan.cs:44）= {Periodic, Wounded, Select, Death, Draft, Undraft, Attack, Equip, MentalBreak, Crying, Giggling}。
- 外部动作（非内置 ActionEntry）需要一种 Origin/Source 表示，方案：
  - 新增 `SqueakTriggerOrigin.External`（append，序数 11）+ 用 TriggerBinding.BindingKind 区分具体通路（PeriodicState/VerseEvent/Sustainer）。
  - 或：Origin 改 string（与 ActionKey 同化），但 Origin 是纯数据 enum 且进 Scribe（SqueakTriggerInvocation 不 Scribe，但 Origin 参与日志），改 string 波及面小。
  - **推荐**：Origin 保持闭合枚举 + append `External`（序数 11），Source 保持 {Periodic, StateEvent, PlayerSelection, ActiveCommand}；外部动作经 TriggerBinding 注册时把 BindingKind 映射到 Origin=External + Source=StateEvent/ActiveCommand。

## 2. Race 层 context 设计（H3）

### 2.1 现状

- ResolveContext(pawn)（SqueakRuntimeResolver.cs:224-235）只有 xenotype/global 两类：Biotech 关 → globalContext；有 xenotype → xenotype context；否则 globalContext。
- 纯 Race pawn 或 Biotech 关时只能拿到 globalContext，Race 层调音无消费点。
- BuildContext（:179-187）用 `global.Enabled && item.Value.Enabled` 的「与」合并，非覆盖（H1 冲突）。

### 2.2 引入 Race 维度

- `ResolvedSqueakContext` 增加 Race 维度标识（raceDefName），并让 context 字典键从单一 xenotypeDefName 扩展为两级：race → xenotype。
- 数据源：BuildBehavior 需新增 Race 层 builder（从 settings.actionTuning 的 Race 层记录聚合），Xenotype 层在 Race 层基础上合并。
- ResolveContext(pawn) 求值链：
  1. 取 pawn.def.defName = raceKey；
  2. Race context = raceContexts[raceKey]（若有 Race 层记录则覆盖 global，否则=global）；
  3. 若 Biotech 开且 pawn.genes.Xenotype 存在，Xenotype context = Race context 上字段级合并 xenotype 记录（last-wins）；
  4. 返回最终 context。

### 2.3 求值顺序 X → R → G → Default（覆盖语义，解 H1）

```csharp
// 伪代码：字段级 last-wins，presence 决定是否覆盖
RuntimeActionDelta ResolveAction(ActionKey key, RaceKey? race, XenotypeKey? xeno)
{
    // 从 Default（动作默认 DefaultScope + 1.0 乘数）起
    RuntimeActionDelta d = DefaultFor(key);
    // Global 层覆盖
    d = ApplyLayer(d, globalLayer[key]);   // hasX=true 才覆盖
    // Race 层覆盖
    if (race != null) d = ApplyLayer(d, raceLayer[race][key]);
    // Xenotype 层覆盖
    if (xeno != null) d = ApplyLayer(d, xenoLayer[race][xeno][key]);
    return d;
}
```

关键：**删除/降级 SqueakGlobalActionPolicy 的全局 Disabled 早退**（TryTrigger:396-397 与 BuildContext:184 的「与」合并），改由上述分层快照统一求值。
「全局关 + Xeno 开」成立：Global 层 hasScope=true + scope=Disabled，Xenotype 层 hasScope=true + scope=AnyOccurrence，求值后 Xeno 覆盖 Global → 开。

### 2.4 ResolveMoodMod 同分层

- 当前 moodOverrides 是全局字典（CompSqueaker.cs:561-573 合并）。
- 改为：mood 调音同样走 ActionTuningRecord 式的分层（G/R/X），或独立 MoodTuningRecord（mood + race/xeno + hasX presence）。
- 求值链同 §2.3：Default(作者 baseline moodMods) → Global → Race → Xenotype。

## 3. 落地文件清单（S2 编码）

1. Models/ 新增 ActionTuningRecord（见 S0 数据模型稿）+ 迁移；
2. SqueakSettingsMigration.cs 新增 TryCreateV5Records（迁移两处旧字段）；
3. SqueakRuntimeResolver.cs 引入 Race context + X→R→G 求值；
4. SqueakGlobalActionPolicy.cs + CompSqueaker.TryTrigger 降级早退；
5. Pure/SqueakActionPlan.cs 的 SqueakActionDefinition 携带 ActionKey string；
6. FerriteLib.UiKit/Widgets 新 TreeRowWidget（S4 UI 层）。
