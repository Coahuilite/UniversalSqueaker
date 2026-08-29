# Review 06 — Comp / Events / Diagnostics / Sustained Path

审查日期：2026-08-28（本地 6-way 隔离审查之一）
审查范围：`review-06 comp-events-diag`
结论：**存在 1 个 blocker、4 个 major、多个 minor/nit。** 构建通过（Dev，0 警告 0 错误），但 Sustained 周期路径与外部 ActionEntry 元数据存在实质未完成/缺陷。

## Scope

- `Source/UniversalSqueaker/CompSqueaker.cs`
- `Source/UniversalSqueaker/Mod.cs`
- `Source/UniversalSqueaker/Patches/*.cs`
- `Source/UniversalSqueaker/Logging/*.cs`
- `Source/UniversalSqueaker/Diagnostics/*.cs`
- `Source/UniversalSqueaker/Runtime/PeriodicStateBinding.cs`
- `Source/UniversalSqueaker/Runtime/VerseEventBinding.cs`
- `Source/UniversalSqueaker/Runtime/ActionEntry.cs`
- `Source/UniversalSqueaker/Runtime/BuiltInActionEntries.cs`
- `Source/UniversalSqueaker/Runtime/SqueakActionModel.cs`
- `Source/UniversalSqueaker/Runtime/SqueakPeriodicPopulation.cs`

辅助核对（用于双键 `ResolveContext` 集成）：
- `Source/UniversalSqueaker/Runtime/SqueakRuntimeResolver.cs`
- `Source/UniversalSqueaker/Pure/SqueakActionPlan.cs`
- `Source/UniversalSqueaker/Pure/SqueakTuningDomains.cs`
- `Source/UniversalSqueaker/Runtime/SqueakKernelAdapter.cs`
- `Source/UniversalSqueaker/Runtime/SqueakSoundAvailability.cs`

## 已检查文件

- `CompSqueaker.cs`（完整 1089 行）
- `Mod.cs`（完整）
- `Patches/` 全部 12 个文件
- `Logging/SqueakLog.cs`、`Logging/SqueakLogProtocol.cs`
- `Diagnostics/SqueakDebug.cs`、`SqueakDebugActions.cs`、`SqueakDiagnosticsOverlay.cs`、`SqueakDiagnosticsPanel.cs`
- `Runtime/PeriodicStateBinding.cs`、`Runtime/VerseEventBinding.cs`、`Runtime/ActionEntry.cs`、`Runtime/BuiltInActionEntries.cs`、`Runtime/SqueakActionModel.cs`、`Runtime/SqueakPeriodicPopulation.cs`
- 为验证双键集成阅读 `Runtime/SqueakRuntimeResolver.cs` 及 Pure 选择器/计划类型

## Findings

### Blocker

#### B1. 周期性 Sustained 路径完全绕过触发门（enabled/scope/cooldown/probability/startup/diagnostic）

- 位置：`CompSqueaker.CompTick()` 的 `SqueakTriggerMode.Sustained` 分支直接调用 `MaintainSustained()`，而不是先走 `TryTrigger()`。
- `MaintainSustained()` 只在“已有 sustainer 时维持”，否则直接 `TryPlaySustained()`；该方法内部不检查 `RuntimeActionDelta.Enabled`、`Scope`、`Timing`、概率或 startup。
- 代码注释声称“动作门/冷却/身份门由调用方（TryTrigger）负责”，但实际调用方不是 `TryTrigger`。
- 后果：
  1. 玩家把某动作调成 `Disabled` 或 `ActiveCommand` 不匹配时，周期 Sustained 仍会发声；
  2. 若 Sustained 动作配置了非 `sustain` 的 `SoundDef`，`MaintainSustained()` 每个 tick 都会调用 `TryPlaySustained()` → `PlayOneShot()`，形成每 tick 一次的一次性播放（无冷却、无概率）；
  3. Sustained 启动/失败不写 `RecordOutcome`，诊断面板看不到相关 gate/结果。

建议修复方向（二选一，需 maintainer 确认）：
- 方案 A：周期 Sustained 状态首次出现时走 `TryTrigger()`（含全部 gate + 冷却 + outcome），之后每 tick 只调 `MaintainSustainer()` 维持已存在 sustainer；`TryTrigger` 内部对 `Sustained` 模式先 `TryPlaySustained` 再 `ConsumeAttemptCooldowns`。
- 方案 B：保留 `MaintainSustained()` 作为启动入口，但启动前显式检查 `GetRuntimeContext()` 的 `actionDelta.Enabled`、`Scope` 与 `EvaluateTiming()` 冷却，并为非 sustain 降级路径增加“已播放/冷却中”状态，避免每 tick 重复播放。
- 至少：补充一个 runtime/manual 测试：Sustained + 非 sustain SoundDef，确认不会每 tick 播放；Sustained + action disabled，确认不发声。

### Major

#### M1. `TryPlaySustained()` 丢弃已调制的 `SoundInfo`，sustainer 无视 mood/age 调制

- 位置：`CompSqueaker.TryPlaySustained()`。
- 代码先 `TryCreateProductionInfo(def, Pawn, out SoundInfo info, out _)`，再设置 `info.pitchFactor/volumeFactor`，随后却用全新 `SoundInfo.InMap(new TargetInfo(Pawn), MaintenanceType.PerTick)` 调用 `TrySpawnSustainer()`，`info` 被完全丢弃。
- 后果：Sustained 声音始终以默认 pitch/volume 播放，`ResolveMoodMod` 的 mood/age 调制不生效；与一次性 `PlayOneShot(info)` 路径不一致。

修复建议：
```csharp
info = SoundInfo.InMap(new TargetInfo(Pawn), MaintenanceType.PerTick);
info.pitchFactor = mod.pitchFactor * mod.pitchJitter.RandomInRange;
info.volumeFactor = mod.volumeFactor;
Sustainer? sustainer = def.TrySpawnSustainer(info);
```
或保留 `TryCreateProductionInfo` 返回值后设置 `info.Maintenance = MaintenanceType.PerTick` 再传入。

#### M2. 外部 ActionEntry 的 `DefaultPlan`/`TriggerBinding` 元数据未接入，外部动作被硬编码为同一默认 plan

- 位置：`Runtime/ActionEntry.cs`、`Runtime/BuiltInActionEntries.cs`、`CompSqueaker.TryGetPlan()`、`SqueakActionPlanFactory.External()`。
- `ActionEntry.DefaultPlan` 和 `BuiltInAudioKey` 全仓库无消费点；`TriggerBinding.ProbePeriodic/DeriveInvocation/Maintain/OnDisabled` 和 `ActionEntry.Binding` 也均无消费点。
- `TryGetPlan()` 对非内置 entry 无条件使用 `SqueakActionPlanFactory.External(actionKey)`，固定为 `RandomOneShot / 300 ticks / 0.02 / AnyOccurrence / ApplyTalkingGate / GameTicks`。
- 后果：外部动作无法通过 ActionEntry 自定义触发模式、冷却、作用域、talking gate 等；`TriggerBinding` 抽象目前是死代码，与“Goal A wrapper”的设计意图不符。

建议修复方向：
- 让 `SqueakActionPlanFactory.External(actionKey, SqueakActionDefinition defaultPlan)` 消费 `entry.DefaultPlan`（至少 mode/minInterval/probability/scope/vocal policy）；
- 或者明确降级：删除/标记未使用的 `TriggerBinding` 成员，文档写明外部 mod 通过 `NotifyExternalByKey` 自行触发，ActionEntry 仅作为 action-gate 注册表。
- 需要 maintainer 决定：外部动作生态是否应支持自定义 plan，还是 YAGNI。

#### M3. `TryPlaySustained()` 外部触发路径可能覆盖/遗留旧 sustainer，造成重叠发声

- 位置：`CompSqueaker.TryPlaySustained()`。
- 当 `plan.Mode == Sustained` 从 `TryTrigger()`（外部事件）进入时，方法不检查 `activeSustainer` 是否已存在/同键，直接 `TrySpawnSustainer()` 并覆盖 `activeSustainer`。
- 旧 sustainer 没有被 `End()`，也没有被复用；若外部事件在冷却窗口内重复触发（或 `minIntervalTicks=0`），可能多个 sustainer 同时播放。
- 周期路径由 `MaintainSustained()` 避免重复，但外部路径没有同等保护。

修复建议：`TryPlaySustained()` 开头增加：
- 若 `activeSustainer != null && !activeSustainer.Ended && activeSustainerKey == actionKey` → 直接返回 `Dispatched`（或复用）；
- 若存在其他键的存活 sustainer → 先 `End()` 再生成；
- 生成失败时保留/清理旧状态语义要明确。

#### M4. 诊断面板 Visible 模式音频列被 Ready/Blocked 文本覆盖

- 位置：`SqueakDiagnosticsPanel.DrawVisible()`。
- `row.Audio` 与 `row.Ready/Blocked` 被绘制在同一个 `stateX` 矩形内；后绘制的状态文本覆盖了音频文本，音频列实际不可见。
- 与列注释“dot | PawnText | Action | Cooldown | Audio | Ready/Blocked”不符。

修复建议：为 Audio 与状态分配不同 rect，例如：
- `statusX = rowRect.xMax - StateTextWidth`
- `audioX = statusX - 96f`
- `cooldownX = audioX - 66f`
- 先画 `Cooldown` 在 `audioX`？当前是把 Cooldown 画在 `audioX`、把 Audio 画在 `statusX`；应调整为 Cooldown 在 `cooldownX`、Audio 在 `audioX`、Status 在 `statusX`，并同步调整 `actionW`。

### Minor

#### m1. 诊断 Gate G7 “Scope match” 使用默认作用域而非运行时解析作用域，且外部 ActiveCommand 判定不准确

- `SqueakDiagnosticsPanel.BuildGates()` 用 `SqueakActionDefinitions.Get(...).DefaultScope == ActiveCommand` 判断是否展示该 gate，用 `pawn.Drafted || pawn.CurJob?.playerForced` 判定匹配。
- 生产逻辑使用 `RuntimeActionDelta.Scope` 和 `SqueakTriggerInvocation.IsActiveCommand`（如 Undraft 外部事件在 undraft 后 `pawn.Drafted=false` 仍可通过）。
- 结果：Work 被调成 AnyOccurrence 时诊断误报 Blocked；Attack 被调成 ActiveCommand 时诊断误报 N/A；Undraft/Equip 外部路径可能误报 Blocked。

#### m2. 诊断 Gate G16 “Dispatch success” 永远 Pass

- 即使 `lastEvaluation` 是 `PlaybackFailed`/`Exception`，G16 仍 `GateState.Pass`，仅把文本从 `Pass` 换成失败动作名。
- 建议：`Dispatched` → Pass；`PlaybackFailed`/`Exception` → Block；其他未发生 → N/A/Pass。

#### m3. 诊断 Gate G14/G15/G16 使用 `lastEvaluation`，可能反映其他动作的旧结果

- 当前诊断只缓存全局 `lastEvaluation`，不区分 action key；若当前显示 Sleep，而最近一次触发是 Wounded，Audio pool/Playability/Dispatch 三行会显示 Wounded 的结果。
- 建议在 `SqueakRecentOutcome` 已带 `Action` 的前提下，按当前 action key 过滤，或显示“最后评估动作名”。

#### m4. `AudioVanillaFallback` 的 usdiag v2 机器字段与 human 文本不一致

- `SqueakLogRegistry.HumanSentence()` 对 vanilla fallback 会显示 `nonplayer` 标记，但 `SqueakLogFormatter.SuffixV2()` 的 `AudioVanillaFallback` 分支没有输出 `pawn_ctrl`/`pawn_faction`；`AudioRouteSelected` 则有。
- 协议消费者无法从 fmt=2 记录还原 human 文本中的 nonplayer 信息。

#### m5. `SqueakLogOnce` 去重键未包含 Race/Xenotype/Sound 等事件关键字段

- 例如 `FallbackProfileStoreFailed(race, ex)` 使用 once=true，但 Claim key 不含 `Race`；同一异常类型在不同 race 上只记第一条。
- `AudioDispatchFailed(action, sound, ex)` 的 key 不含 `Sound`，同 action 不同 sound 的失败会被抑制。
- 建议把 once 事件真正需要的维度加入 key，或对这些事件改为非 once + 频率限制。

#### m6. 诊断刷新/绘制没有异常隔离

- `SqueakDiagnosticsOverlay.RefreshIfDue/RefreshSnapshot` 和 `CompSqueaker.PostDraw` 均无 try/catch；`Patch_Root_DiagnosticsLifecycle` 每帧调用它们。
- 若某个 modded pawn 的 snapshot 或 draw 抛异常，会直接冒泡到 `Root.Update`，可能打断游戏帧。诊断功能应 fail-closed 且不影响生产。

#### m7. `Patch_GlobalControlsUtility_CameraIndicator` 未保存/恢复 `Text.Anchor`

- 该 patch 设置 `UpperRight` 后恢复为 `UpperLeft`，不是恢复调用前值；可能轻微影响后续 GUI 绘制。

#### m8. `SqueakPeriodicPopulation.Maintain()` 同 tick 早退可能返回旧地图快照

- 若 `Find.CurrentMap` 在同一 game tick 内切换且 `lastMaintenanceTick == now`，会直接返回上一个地图的 snapshot。通常极罕见，但建议早退前检查 `snapshotMap == map`。

#### m9. `SqueakRuntimeResolver.GlobalOnly` 兜底丢失 Disabled 模式

- `BuildFallback()` 内部再 catch 时返回 `SqueakRuntimeSnapshot.GlobalOnly`（mode=Vanilla）。双重故障下 Disabled 真旁路会被重置为 Vanilla。
- 这是极窄的“兜底的兜底”，但既然 M1/A3 专门保护 Disabled，建议为 GlobalOnly 增加一个可表达 Disabled 的构造或至少记录风险。

### Nit

- `SqueakDiagnosticsOverlay.DrawMark()` 与 `CompSqueaker.PostDraw()` 重复实现，`DrawMark` 未被使用。
- `SqueakDebug.NotifySqueak(Pawn, SqueakAction, ...)` 未被使用（仅 `NotifySqueakByKey` 被调用）。
- `ActionEntry.BuiltInAudioKey`、`BindingKind`、`TriggerBinding` 当前无消费点，属于死/半死代码（见 M2）。
- `SqueakPeriodicPopulation.Snapshot.Stale` 字段无读取点。
- `SqueakDiagnosticsPanel.BuildGates()` G3 `hasAction ? Pass : Pass` 冗余。
- `SqueakDiagnosticsPanel` 中 `Cooldown()` 局部函数与 `FormatCooldownDisplay()` 重复。
- `SqueakLogRegistry` 注释“28-event v1”与当前事件数不一致（文档性 nit）。

## 与双键 `ResolveContext` 集成核对

- `CompSqueaker.GetRuntimeContext()` 以 `(snapshot, xenotype)` 做缓存；`SqueakRuntimeSnapshot.ResolveContext(Pawn)` 在双键下返回 race/xeno/global context，缓存条件对同一 comp 是充分的（race 不变、snapshot 不可变）。
- `PreviewFinal()`、`GetDiagnosticSnapshot()`、`TryTrigger()` 都通过 `ResolveContext` 获取 context，外部动作 `ChooseProductionSoundByKey` 也走 `context.Xenotype` 构造 `AudioDomain`，未发现双键导致的路由错乱。
- 唯一集成注意点：`SqueakRuntimeResolver.BuildFallback()` 返回空 `raceContexts/contexts`，崩溃兜底后所有 race/xeno 调音退化为 global；这是既有 fallback 语义，不是本次回归。

## 验证命令

已在本次审查运行：

```powershell
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev --no-restore -v:minimal
# 结果：成功，0 警告 0 错误
```

建议后续验证：

```powershell
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release --no-restore -v:minimal
dotnet test tools/UniversalSqueakerLogTests/UniversalSqueakerLogTests.csproj --no-restore
dotnet test tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj --no-restore
dotnet test tools/UniversalSqueakerConfigCopyTests/UniversalSqueakerConfigCopyTests.csproj --no-restore
git diff --check
```

游戏内手动矩阵（maintainer/有 RimWorld 运行时者执行）：
1. 配置一个 `Sustained` 动作 + 非 sustain `SoundDef`，确认不会每 tick 重复播放。
2. 配置 `Sustained` 动作 + sustain `SoundDef`，确认启用/禁用该 action、切地图、离屏、Destroy 时 sustainer 正确 End。
3. 在 Disabled 全局模式下确认已激活 sustainer 尾音结束后不再重启。
4. 打开诊断 Visible 面板，确认 Audio 列可读、不被 Ready/Blocked 覆盖。
5. 将 Work 调成 AnyOccurrence、将 Attack 调成 ActiveCommand，确认 Gate G7 与生产一致。
