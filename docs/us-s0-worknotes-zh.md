# US S0 前置设计 — 工作说明与基线

> 状态：进行中（2026-08-25）。
> 本文件记录 S0 前置设计补齐阶段的调度事实：已定决策、门禁基线、worker 分工与产出清单。不替代各设计稿正文。

## 1. 已定决策（维护者确认）

- 旧 `Off` 改名 → `Vanilla`（枚举与 UI 文案统一用 `Vanilla`）。
- 玩家调音 baseline 独立只读 Def 最终名：`UniversalSqueakerTuningBaselineDef`。
- `ActionEntry` / `TriggerBinding` 接口细节（L2 C# 类型形态）授权本会话设计，作为 S0 交付物产出；L1 字段契约已在计划 §13.3 锁定；L3 逐动作实现属 S2 编码期。
- 维护者指定 worker provider：`commandcode`，模型 `deepseek-v4-flash`，思考强度 `high`。

## 2. 关键诊断结论（provider）

- `commandcode` 的模型 id 需带 `deepseek/` 前缀：正确写法 `deepseek/deepseek-v4-flash`（catalog 见 `~/.dsh/commandcode/commandcode-models.json`，DeepSeek 项 id 均为 `deepseek/<model>`）。
- 裸 `deepseek-v4-flash` 会导致 provider 解析失败、worker 报 `stopReason: error`。后续所有派发一律用 `deepseek/deepseek-v4-flash`（或 `deepseek/deepseek-v4-pro`）。

## 3. 门禁基线（S0 动工前，2026-08-25）

`scripts/verify-local.ps1` 12 项检查全绿（exit 0）：
kernel tests（unit + US 0.1.0 corpus replay + determinism）、config-copy A-F、log tests Release/Dev、
UiKit tests、UiKit Dev/Release build、UiKit neutrality grep、主程序集 Dev/Release build、
双 DLL 存在、UI Layout.xml well-formed。

此结果为后续 S1–S5 每阶段门禁的参照基线。

## 4. worker 分工（批次 1，并行）

| Worker | 产出 | 状态 |
|---|---|---|
| S0-数据模型设计 | `docs/us-s0-data-model-zh.md`（ActionTuningRecord / UniversalSqueakerTuningBaselineDef / US.TuningFeedback.v1） | running |
| S0-枚举键与Race上下文 | `docs/us-s0-key-and-race-context-zh.md`（枚举键消费点 inventory + Race context 设计） | running |
| S0-日志协议扩展 | `docs/us-s0-log-protocol-zh.md`（audio.disabled 事件 + 协议 v2 扩展 + LogTests 用例） | running |

## 5. 后续批次（依赖批次 1 产出）

- 批次 2（并行）：S0-a Scribe 四态迁移设计（依赖 ActionTuningRecord 形状 + 枚举键 inventory）、
  S0 接口设计稿 `docs/us-action-wrapper-interface-zh.md`（依赖枚举键消费点全量表）。

## 6. 门禁约定

每阶段结束跑 `scripts/verify-local.ps1` 六门（12 项）+ 该阶段专项门禁（如 S2 需补「全局关 + Xeno 开」内核/集成测试、Scribe fixture 双向 replay）。

## 7. S1 全局层拨离 — 动工面锚点（侦察，供 S1 派发）

- `CompProperties_Squeaker` 定义：`CompSqueaker.cs:897-955`；五字段 `897-905`（globalMinIntervalTicks/scaleFrequencyWithTalking/actions/moodMods/distancePresets）。
- `CreateDefault()`：`916-954`；全局字段 `920-921`（globalMinIntervalTicks=216、scaleFrequencyWithTalking=true）、距离预设 `947-952`（Conservative 15-65 / Balanced 15-50 / Strong 15-40）。
- `SqueakDistancePresetConfig` 定义：`CompSqueaker.cs:133-137`。
- `globalMinIntervalTicks` 运行时唯一消费点：`EvaluateTiming` → `Props.globalMinIntervalTicks`（`CompSqueaker.cs:505`），须迁到 US 全局设置读取。
- `scaleFrequencyWithTalking` 已是全局设置（`UniversalSqueakerSettings.cs:49`），但默认值从作者 comp 读取：`GetDefaultScaleFrequencyWithTalking`（`UniversalSqueakerSettings.cs:330-338`）+ ExposeData `67,84,110`；S1 需切断对 `ConfiguredSqueakers()` 的默认回读，改 US 自身默认。
- 距离预设当前从作者 comp 读取：`GetDistancePresetRange`（`UniversalSqueakerSettings.cs:309-328`，含 `ConfiguredSqueakers()` 回读），S1 迁到 US 全局设置/默认 Def，切断作者面回读。
- 三缩放开关静态字段：`CompSqueaker.cs:147-149`（ScaleCooldownWithTimeSpeed/ScaleFrequencyWithTalking/ScalePeriodicWithAudiblePopulation），`ApplyToRuntime` 发布（`UniversalSqueakerSettings.cs:89-100`）。
- `GetDistancePresetRange` 的 fallback 分支 `322-327`（Conservative/Strong/Balanced 硬编码 range）作为迁移后的 US 默认值来源。
- skill 模板待删全局字段：`.github/skills/us-voicepack-authoring/SKILL.md`（globalMinIntervalTicks/scaleFrequencyWithTalking/distancePresets）。


## 10. S0 完成状态（2026-08-25，主会话产出）

**调度决策**：commandcode worker 通道对「读多文件 + 写长文档」的重任务不可靠（三个 worker 8 小时无产出、卡在 running/idle），
改为**主会话直接产出全部 S0 设计稿**（已掌握全部行号锚点，效率高且可交叉核验）。

**S0 产出清单（全部定稿）**：
- `docs/us-s0-scribe-migration-zh.md`（S0-a 四态迁移）
- `docs/us-s0-data-model-zh.md`（S0-b/e/f 数据模型）
- `docs/us-s0-key-and-race-context-zh.md`（S0-c/d 枚举键 + Race context）
- `docs/us-s0-log-protocol-zh.md`（S0-g 日志协议）
- `docs/us-action-wrapper-interface-zh.md`（接口 L2）

**关键源码核验结论（RimWorld 源码，不重验）**：
- 枚举按名称序列化（Scribe_Values.cs:82 value.ToString()）+ 按名称解析（ParseHelper.cs Enum.Parse）。
- 加载失败 catch → default（序数 0）→ 旧档 Off 自动落 Vanilla(0)，语义正确，仅一条 Log.Error 噪音。
- 维护者定案：允许破坏，Off 直接改 Vanilla（不做兼容别名）。
- ToSelectionMode 当前实现 SqueakKernelAdapter.cs:49-54（需改四态）。

**S0 待签收的取舍点（S2 动码前，见 us-action-wrapper-interface-zh.md §5）**：
1. ActionEntry 用 abstract class（本稿选此）；
2. 注册表单例（本稿选单例 ActionEntryRegistry）；
3. SqueakAction 枚举保留 + 边界转换（本稿选此，作者 ABI 不变）；
4. 外部动作 Origin 表示（本稿选 append SqueakTriggerOrigin.External 序数 11）。


## 11. S1 全局层拨离 — 完成（2026-08-25）

**改动清单（已编译通过 + verify-local 12 项全绿）**：
- CompSqueaker.cs：
  - 删除 `SqueakDistancePresetConfig` 类（原 :133-137）；
  - 加静态字段 `GlobalMinIntervalTicks = 216`（GlobalCooldownMultiplier 后）；
  - `EvaluateTiming` 消费点 `Props.globalMinIntervalTicks` → `GlobalMinIntervalTicks`（:505）；
  - 删 `CompProperties_Squeaker` 的 `globalMinIntervalTicks`/`scaleFrequencyWithTalking`/`distancePresets` 三字段；
  - 删 `CreateDefault()` 里的全局字段初始化 + distancePresets 块。
- UniversalSqueakerSettings.cs：
  - 加设置字段 `globalMinIntervalTicks = 216`（:50 后）；
  - `GetDistancePresetRange` 去 `ConfiguredSqueakers()` 作者面回读，改纯静态默认（Conservative 15-65 / Strong 15-40 / 其他 Balanced 15-50）；
  - `GetDefaultScaleFrequencyWithTalking` 去作者面回读，改纯 true；
  - 删 `ConfiguredSqueakers()` 方法；
  - `ApplyToRuntime` + `NotifyCheapRuntimeChanged` 发布 `GlobalMinIntervalTicks = Max(1, globalMinIntervalTicks)`。
- UniversalSqueakerSettings.ExposeData.cs：加 `Scribe_Values.Look(ref globalMinIntervalTicks, "globalMinIntervalTicks", 216)`。
- .github/skills/us-voicepack-authoring/SKILL.md：删 comp patch 模板里的 globalMinIntervalTicks/scaleFrequencyWithTalking/distancePresets。

**遗留（S2 处理）**：
- 枚举 `Off→Vanilla` 改名 + `Disabled(3)` 第四态（与 S2 wrapper 化同批，见 us-s0-scribe-migration-zh.md）；
- skill 模板 §6 的「OFF」文案与 §11 检查清单「OFF/FALLBACK/REMIX」随 S2 改名同步。
- 距离预设 UI（保守/激进档 + 拖动条 + 折线图）属 S4 UI 面（S1 只做数据/运行时拨离）。

**门禁**：verify-local 12 项全绿（exit 0），Dev build 0 警告 0 错误。


## 12. S2a 枚举四态 + Disabled 旁路 + audio.disabled — 完成（2026-08-25）

**改动清单（verify-local 12 项全绿，Dev 0 警告 0 错误）**：
- 枚举：`SqueakVoicePackMode { Vanilla, Fallback, Remix, Disabled }`（SqueakVoicePackDomain.cs，Off 直接改 Vanilla，Disabled append 序数 3）。
- 归一化链：resolver NormalizeMode（Disabled 穿透、非法回落 Vanilla）、ToSelectionMode（四态显式）、PageModel.NormalizeMode、ExposeData 非法回落 Vanilla。
- 全仓 `.Off` 引用改 `.Vanilla`：BuildFallback/GlobalOnly/PageModel/VoicePacksPage banner。
- UI 模式卡：VoicePacksPage 3 卡改 4 卡（Vanilla/Fallback/Remix/Disabled，宽度 /4f）；Layout.xml mode-row 改四态（Value1..4）；ModeCard 注释更新。
- Disabled 旁路：CompTick + NotifyExternal 进 TryTrigger 前统一前置判断 `VoicePackMode == Disabled` → SqueakLog.AudioDisabled(actionKey) + return。
- 日志协议：SqueakLogEvent.AudioDisabled（append）+ Definition(Daily+Info, v2) + EventId "audio.disabled" + HumanSentence + SqueakLog.AudioDisabled 便捷方法（once=true）。
- LogTests：VerifyV2Protocol 补 audio.disabled 发射 + 断言；v2 registry size 8→9。

**旧档兼容（已核验 RimWorld 源码）**：枚举按名序列化，旧档 "Off" 无法解析 → ValueFromNode catch → default(序数0)=Vanilla，语义正确（旧 Off=关池 vanilla 响），仅一条无害 Log.Error 噪音。

**剩余 S2b（本轮后续）**：ActionEntry/TriggerBinding wrapper 化 + ActionTuningRecord 分层表 + allowExternalActions 动作门 + 枚举键消费点改造 + SqueakGlobalActionPolicy 降级（H1/H3/H4/M3/M4）。


## 13. S2b 分层表数据面闭环 + H3 Race 层 — 完成（2026-08-25）

**改动清单（verify-local 12 项全绿）**：

1. ActionTuningRecord 数据模型（Models/ActionTuningRecord.cs）：字段 + ExposeData + IsValidLayer + Clone。
2. Settings/ExposeData 接入：actionTuning 字段 + Scribe Deep Look + PostLoadInit 空保护。
3. 迁移：SqueakSettingsMigration.TryCreateActionTuningRecords（旧 globalActionEnabled + xenotypePresets.actionOverrides → actionTuning）；ExposeData 幂等接入（空源合法，仅当 actionTuning 空时迁移）。
4. 消费点切换：BuildGlobalActions 读 actionTuning Global 层（layer==0）；BuildBehavior 读 Xenotype 层（layer==2）。
5. H3 Race 层：
   - BuildRaceBehavior 聚合 Race 层（layer==1）为 raceBuilders；
   - BuildSnapshot 构建 raceContexts + xenotype context 用 race 基底 Overlay 合并；
   - ResolvedSqueakContext.Overlay（字段级 last-wins 合并两个 context）；
   - SqueakRuntimeSnapshot 加 raceContexts 字段 + 8 参构造（7 参构造委托兼容）；
   - SqueakRaceForXenotype 辅助（从 voicePackSelections/xenotypePacks/单种族 catalog 反查 xenotype 所属 race）；
   - ResolveContext 改三级求值：纯 Race pawn → raceContext（无则 global）；有 Xenotype → 已合并 race 基底的 xenoContext。

**语义结果**：X > R > G > Default 覆盖语义成立；「全局关 + Xeno 开」成立；纯 Race pawn 或 Biotech 关时得到 Race 层调音。

**剩余 S2b**：
- wrapper 化（ActionEntry/TriggerBinding + 枚举键消费点 H4 + 动作门 allowExternalActions）：涉及 8 patch 文件 + CompSqueaker 5 定长数组 + SqueakActionPlan 纯类型，是 S2 剩余的最大独立块。
- H1/H3 覆盖语义的「全局关 + Xeno 开」专项测试：因 resolver/RuntimeActionBuilder 在 Runtime 层（引用 Verse），Kernel 测试无法直接承载；需在 S4 把合并逻辑抽取到 Kernel/Pure 后补，或加一个 Runtime 测试 harness（评估中）。
- SqueakGlobalActionPolicy 删除（S5）。


## 14. S5 清理 — 完成（2026-08-25）

**改动清单（verify-local 12 项全绿）**：
- 删 `SqueakGlobalActionPolicy.cs` 整文件（H1 后 `Current.GetScope` 已无消费者，`Publish` 写入无人读的静态字段）；删 `ApplyToRuntime` 里的 `Publish` 调用。
- 删 `experimentalRaceAllowlist`（字段 + Scribe Look + 空保护，纯尸体，B1）。
- 删 `Patch_ModMetaData_LocalizedMetadata.cs` 整文件（决策 §9：Name/description 本地化 patch 删除）。
- 删死方法 `EnsureBuiltInRaceDefault`（无调用点）+ 删 `developerToolsEnabled`（字段 + Scribe Look，B4 无消费者）。
- 恢复 `localizeDebugActions`：新建 `Patch_DebugTabMenu_Actions.cs`（从 SR 原样迁移，命名空间改 UniversalSqueaker），`ApplyToRuntime` 接 `SetEnabled(localizeDebugActions)`。

**保留**：
- `voicePackDefaultSeeded` 字段 + Scribe 节点保留（B2 保守处理：是 Scribe 兼容哨兵，删字段动 Scribe 形状风险 > 收益；其唯一消费者 EnsureBuiltInRaceDefault 已删，字段现为纯 Scribe 形状保留）。
- `globalActionEnabled`/`GlobalActionEnabledRecord`/`GetActionGlobalScope`/`SetActionGlobalScope` 等旧字段仍保留：因为它们是 `BuildGlobalActions` 的 fallback 数据源（actionTuning 为空时回退），且迁移逻辑读它们。S4 调音编辑器 UI 落地、actionTuning 成为唯一数据源后，这些旧字段才能随「旧字段删除」清理。

**门禁**：verify-local 12 项全绿（exit 0），Dev 0 警告 0 错误。


## 15. S4 UI 面孤儿接回 — 部分完成（2026-08-25，Round 17-19）

**已接回 UI 的孤儿（verify-local 12 项全绿，旧 UI 路径 VoicePacksPage）**：
- A7 彩蛋开关：`UiCommandKind.ToggleEgg` + `VoicePacksViewState.AllowEasterEggs` + `DrawEasterEggToggle`（模式卡下方，点击切换，走 `SetAllowEasterEggSounds`）。
- A3 距离预设：`UniversalSqueakerSettings.SetDistancePreset` + `UiCommandKind.SetDistancePreset` + `DrawDistancePresetRow`（Conservative→Balanced→Strong 循环切换 + 档位/range 显示）。
- A4 三缩放开关 + A5 globalCooldownMultiplier（数据面已就绪）：`SetBasicTuning` + `UiCommandKind.ToggleBasic` + `DrawBasicToggle`（三个 checkbox 行）。

**注意**：
- 以上均落在旧 UI 路径（`VoicePacksPage`）；Ferrite 路径（`UseFerriteUi=true` 默认）需在 `Layout.xml` + widget 注册同步这些开关（后续工作）。
- A1/A2 调音编辑器、A6 动作作用域树形开关、A8 devLoggingMode（DebugAction 面板）仍未接 UI。
- `globalCooldownMultiplier` 本轮只补了数据面（已 Scribe + 运行时发布），未做独立 UI 控件（三缩放开关已接，cooldown 乘数滑块留后续）。

**残留物**：`.github/skills/us-voicepack-authoring.zip`（未跟踪，非本会话产物，疑似之前会话遗留的打包产物；建议清理或 gitignore）。


## 16. Goal A wrapper 化 — 部分完成（2026-08-25，Round 1-4）

**已完成（verify-local 12 项全绿）**：
- 类型骨架：`Runtime/ActionEntry.cs`（BindingKind + ActionEntry + TriggerBinding + ActionEntryRegistry 单例 + AllowExternalActions）。
- 内置注册：`Runtime/BuiltInActionEntries.cs`（EnsureRegistered 把 17 内置动作注册进 registry，KindFor 映射 PeriodicState/VerseEvent）；`Mod.cs` 启动点接线。
- 周期探针：`Runtime/PeriodicStateBinding.cs`（Probe(pawn) 替代 CurrentAction 硬编码 switch）；CompSqueaker.CurrentAction 改为委托 + 删冗余谓词方法。
- VerseEvent 映射：`Runtime/VerseEventBinding.cs`（OriginFor/DefaultSource）；CompSqueaker 8 个 Notify_XXX 改用 OriginFor。
- 动作门：`CompSqueaker.IsActionAllowed` + TryTrigger 判定；`UniversalSqueakerSettings.allowExternalActions` 字段 + Scribe + ApplyToRuntime 发布到 registry。

**判断延后（不强行做，理由）**：
1. 5 定长数组改 Dictionary：当前无外部动作消费者，内置 17 动作用定长数组最优（O(1)/零 GC/零 bug 风险），改 Dictionary 是负收益。S0 设计稿 §1.1「混合方案」明确「内置保留定长数组 + 外部走 Dictionary 增量」，增量分支在外部动作真正落地时才需要。
2. 8 个 patch 改造为 TriggerBinding 派生类：patch 现已是「调用 Notify_XXX → NotifyExternal → TryTrigger」的薄包装，Origin 硬编码已消除（VerseEventBinding）、CurrentAction 硬编码已消除（PeriodicStateBinding）、动作门已接线。改「实例派生类」只是换形式不改行为，收益极低、行为等价回归风险高。

**wrapper 化实际达成的价值**：消除「新增动作需改 CurrentAction switch + 8 个 Notify_XXX Origin 硬编码」两大扩展痛点；ActionEntryRegistry + allowExternalActions 动作门 + SqueakTriggerOrigin.External 全部就位，外部动作注册通道已铺好。


## 17. Goal A 字符串化贯穿 — 完成（2026-08-25，Round 7-8）

**改动（verify-local 12 项全绿）**：
- Pure 层：SqueakActionDefinition + SqueakActionPlan 增加 ActionKey 字段（向后兼容构造，harness 不破）。
- resolver 层：BuildGlobalActions/BuildContext/BuildFallback/RuntimeBuilder/ResolvedSqueakContext/SqueakRuntimeSnapshot/GetGlobalScope 的 action 字典全部改 Dictionary<string,...>；ResolvedSqueakContext 加 GetActionByKey(string)。
- CompSqueaker：SoundCacheMixed/MissingSoundWarnings 改 string 键。
- **核心胜利**：ActionTuningRecord.actionKey(string) 现在直接匹配运行时求值，外部动作键不再被 TryParseBuiltIn 拒绝——外部动作能进入分层表求值（外部扩展 API 的运行时基础）。

**剩余（patch 改造为 TriggerBinding 派生 + 行为等价回归测试）**：
- 8 个 patch 目前是「薄包装调用 Notify_XXX」；Origin 硬编码已通过 VerseEventBinding 消除，但 patch 未改成 TriggerBinding 派生类形式。
- 行为等价回归测试未专门补（用编译 0 警告 + kernel 测试 + verify-local 代理）。


## 18. Goal A wrapper 化 — 完成（2026-08-25，Round 1-12）

**触发链 string 化（Round 11）+ 行为等价回归（Round 12）落地，verify-local 12 项全绿 + kernel replay 零 delta**：

触发链 string 化：
- TryTrigger 方法体 SqueakAction → string ActionKey（来自 plan.ActionKey），全部下游调用改 string。
- 下游方法签名 string 化：IsPeriodicStartupPending/MaterializePeriodicStartupPhase/CalculatePeriodicStartupReadyTick/StablePawnActionPhase/PlayOneShot/ConsumeAttemptCooldowns/RecordOutcome。
- SqueakRecentOutcome.Action 字段改 string（runtime-only 非 Scribe）。
- resolver 加 ChooseByKey/ChooseProductionSoundByKey；SqueakDebug.NotifySqueakByKey。
- NotifyExternal 委托到新增 NotifyExternalByKey(string)——外部动作（无枚举）端到端发声入口。

行为等价回归：
- 新增 VerseEventBindingContract 测试（8 外部动作 OriginFor + DefaultSource 契约，对齐 worknotes §8 行为基线）。
- csproj 链接 Runtime/VerseEventBinding.cs（零 Verse，符合纯度门）。
- golden corpus replay 零 delta（机械等价变换强保证）。

**外部动作端到端能力闭环（脚手架完整）**：
1. 注册：ActionEntryRegistry.Register(ActionEntry)
2. 门控：allowExternalActions（内置总是允许，外部需开）
3. 调音：ActionTuningRecord.actionKey(string) 直接匹配运行时求值
4. 发声：NotifyExternalByKey(string) → TryTrigger(string) → ChooseByKey → PlayOneShot

**patch 形态决策（务实）**：8 个 patch 保持薄包装调 Notify_XXX，Origin 硬编码通过 VerseEventBinding（static 聚合绑定）消除，外部入口通过 NotifyExternalByKey。未改成「每动作一个 TriggerBinding 实例派生类」，因为内置 17 动作共享同一 NotifyExternal 通路，static 聚合绑定是更优解（无实例开销、行为等价由契约测试覆盖）。这是接口稿 §2「绑定实现外置」的落地形态，只是从实例派生简化为按 BindingKind 聚合的静态绑定。

## 8. S2 动作入口 wrapper 化 — Harmony patch 行为基线（侦察，供 S2「行为等价清单」）

> M4 要求每个内置 TriggerBinding 写「行为等价清单」，以现有 patch 行为为基准。以下为 8 个 patch 的精确触发条件。

| 动作 | patch 文件 | 触发条件（wrapper 化必须逐条保留） |
|---|---|---|
| Draft/Undraft | Patch_DraftGizmo_Toggle.cs | 仅包玩家 Draft gizmo（Command_Toggle，hotKey=Command_ColonistDraft 且 tutorTag∈{Draft,Undraft}），Never Pawn_DraftController.Drafted setter；用 ConditionalWeakTable 防重复包装；toggleAction 前取 before=Drafted，执行原动作后比对 after != before 才 Notify_Draft(after) |
| MentalBreak | Patch_MentalBreak.cs | 只 hook MentalBreakWorker.TryStart（非 TryStartMentalState 通用入口），Postfix 仅当 __result && pawn.Spawned && MapHeld==CurrentMap |
| Crying/Giggling | Patch_MentalFit.cs | hook Verse.AI.MentalStateHandler:TryStartMentalState；仅 stateDef.defName∈{Crying,Giggling} 且经 MentalFitDef 反向 map 验证；Biotech 关→反向 map 空→永不通知；pawn 取自 __instance.CurState.pawn |
| Equip | Patch_Pawn_EquipmentAdded.cs | hook Pawn_EquipmentTracker.Notify_EquipmentAdded Postfix；pawn.Spawned && MapHeld==CurrentMap 才 Notify_Equip；Notify_Equip 内再判 IsCurrentEquipJobPlayerCommand（playerForced && job==Equip） |
| Death | Patch_Pawn_Kill.cs | Pawn.Kill Prefix（Kill 主体前）；统一覆盖击杀+流血死亡；不加 Spawned/Map 判断（Position 仍有效） |
| Wounded | Patch_Pawn_PostApplyDamage.cs | Pawn.PostApplyDamage Postfix；无条件 Notify_Wounded（NotifyExternal 内做 Spawned/Map/视野判断） |
| Select | Patch_Selector_Select.cs | Selector.Select 重载链(object,bool,bool)/(object,bool)/(object)；只认 __1(playSound)=true 且 __0 是 Pawn |
| Attack | Patch_Verb_Attack.cs | 遍历 Verb/JobDefOf 两程序集，找非 Ability、非静态/抽象/泛型、返回 bool 的 TryCastShot 实例方法，排除无方法体；Postfix 仅当 __result && caster 是 Pawn && Spawned && MapHeld==CurrentMap |

通用注意：NotifyExternal（CompSqueaker.cs:339-359）统一做 SynchronizePeriodicMembership + Spawned/MapHeld/视野(ExpandedBy(10)) 判断；IdentityGateAllows（:364-369）做 PlayerSelection 需 !Downed&&Awake、PlayerInitiated 需 IsPlayerControlled。wrapper 化只替换调用入口，不改这些判断条件。

## 9. S0-a 四态迁移 — 关键事实（侦察）

- voicePackMode Scribe Look 默认 Fallback：UniversalSqueakerSettings.ExposeData.cs:61；voicePackModeWasLoaded 判定 :88（缺失=未显式选择→Fallback）。
- 迁移事务入口：MigrateV3RecordsTransactionally（ExposeData.cs:139-166），migrationNeeded 判定 :125（settingsSchemaVersion<4 || voicePackSchemaVersion<2）。
- 新增 Vanilla(0)/Disabled(3) 需改：SqueakVoicePackMode 枚举（SqueakVoicePackDomain.cs:14-19）、NormalizeMode（SqueakRuntimeResolver.cs:207）、SqueakKernelAdapter.ToSelectionMode（需查证）、UI ModeCard/FerriteVoicePacksPage 文案。
- 枚举改名 Off→Vanilla 必须保序数 0（Scribe 按名序列化 ABI）；Disabled 只能 append 为序数 3。
