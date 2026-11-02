# 当前真实能力与采用位置

状态：阶段 A 源码观察；行号对应 2026-10-29 继承 dirty 工作树，未运行诊断、构建或测试。生产源码缩写路径以 `Source/UniversalSqueaker/` 为根；`tools/`／`docs/` 以仓库根为根。规范入口为 [diagnostics](../../specs/diagnostics/spec.md) 与 [UI 消费](../../specs/ui-consumption/spec.md)。

本表保修复前观察，不随阶段 B 回写历史行号。方案已被 WO r2 采用并实施；当前连接／证据在[实现映射](../../../development/specification/implementation.md#玩家诊断的真实采用链)与[状态](../../../development/specification/open-issues.md#d06-玩家诊断状态录制与反馈)，不是本表的旧“尚不存在”状态。

## 入口、控制与数据：哪些已经存在

| 当前能力 | 实际路径／符号 | 已有含义和限制 |
| --- | --- | --- |
| 打开回应诊断 | `Source/UniversalSqueaker/Diagnostics/SqueakDebugActions.cs:15` `DiagnosticsOpen` → `SqueakDebug.OpenDiagnostics` → Overlay `BeginSession:121` | Debug Action，PlayingOnMap；BeginSession 无地图直接返回。主窗／详情窗构造器分别 `:56`／`:52` 设 `onlyDrawInDevMode=true`。未发现普通设置页打开此窗的绑定。 |
| 设置诊断页 | `UI/Layout.Schema2.xml:237–278`；`UI/UsKernelSettingsHost.Voices.cs:279–280`；`UI/GameSettingsWindow.cs:63–72` | 有实际 US build／载入 FL informational version、缺失选择／事务诊断、日志模式、Debug 菜单本地化、几何工具。没有回应记录的开始／停止／清空／复制。设置窗口本身 forcePause／absorb 周围输入；回应诊断窗不暂停、不吸收周围输入。新入口打开诊断后，正常关闭设置窗才能恢复模拟玩法，不应偷偷关闭带草稿的设置窗。 |
| 主体角色 | `Diagnostics/SqueakDiagnosticsSessionModel.cs`；`SqueakDiagnosticsOverlay.cs:301–364` | 视口、live selection、锁定三种角色并集。选中读取可在屏外；锁定允许多窗／屏外跟踪。视口按 thingIDNumber 排序，不按人数停止。主窗详情跟随游戏单选，不是独立业务 owner。 |
| 列表／搜索 | Overlay `SearchCurrentMap:235`；`UI/Diagnostics/UsDiagnosticsSources.cs:145–208` | 无查询时列视口，八行分页。非空搜索只走当前地图已生成、非死亡、带 Comp 的 Pawn；到 64 个匹配即停，再排序所收前缀。未统计余项／未返回截断标记。屏内行调用 `Find.Selector.Select`，屏外搜索行直接锁定开详情；搜索本身不建快照。 |
| 锁定／解锁 | Overlay `LockAndOpenDetail:140`／`NotifyDetailWindowClosed:162` | 现有锁定与详情窗同寿命；关闭详情即解锁；失去其他角色才释放该 entry。主窗关闭结束整个会话并级联关详情。 |
| 刷新／时钟 | `Patch_Root_DiagnosticsLifecycle.cs:18–23` → Overlay `RefreshIfDue:191` | Root.Update 驱动；详情角色按真实时间 0.25 秒、视口扫按 0.5 秒；不同角色重叠可重复读取同一 Pawn。不是每次 Draw 触发业务机会，没有手动刷新或暂停观察按钮。每成功快照都 bump revision；行缓存按 revision／查询／单位失效。 |
| 历史 | `CompSqueaker.cs:158–189` `RecordOutcome`／`ResetDiagnosticState` | 只有 lastEvaluation／lastSignificantOutcome／lastDispatched 三槽，不是 64 条历史；仅 `DiagnosticsEnabled` 时写。开窗将此全局 flag 置 true，因此未跟踪 Comp 也可能写三槽和准备详细观测文本。entry 首建、失去所有角色、会话结束会清这三槽。只清诊断，不清业务截止；不重置回应日志预算。 |
| 实际结果 | `Game/SqueakerRuntime.cs:176–203` `PawnResponseAdapter.Tick` | 原 owner 完成 Respond 后保存 LastOutcome／lastObservation，再请求 Comp.RecordKernelOutcome；日志／内存 publication 在此之后。Comp 将所有非 Dispatched 粗映为 EligibilityRejected；精确四回执／无请求必须读原 DecisionOutcome，不从这个旧 enum 反推。 |
| 读面与初态 | `SqueakerRuntime.cs:64–110,491–493`；`Core/Responses/ResponseSession.cs:24–26` | Runtime 的 Observe／DiagnosticText 用已有 sessions.TryGetValue，不调用 SessionFor。Core Observe 已把 null 动作局部转为空串；native DiagnosticText 的 `ActionDeadline(LastOutcome.Action)`／`InteractionDeadline(...)` 未转。adapter 已建立而尚无 Respond 时 LastOutcome 是默认 struct；这是相符的 null 键源码路径，不是本阶段动态复现。 |
| UI 投影 | `UsDiagnosticsSources.cs:308–421`；Overlay `ReadyFor:118`／`ToneFor:413` | 当前 factory 实际只构造冷却、上次决定、未知 playability，未采用纯投影中完整旧 16 行链。Ready 只看 ActiveAction，不能表示 OneShot 可发声；Action 文本实为最后评估动作。CooldownText 一律按模拟 tick／60 换算，未携交互秒截止。不能把纯投影测试当作当前原生 factory 覆盖。 |
| 读失败 | Overlay `UpdateSnapshot:385–408` | 每 Pawn catch 并走具名 Daily `DiagnosticsUiFailed`，后续 Pawn 继续。entry 留默认／旧 snapshot，未存 LastSuccess／ReadFailed；UI／报告不能辨读失败。整轮 Refresh 也有观察 catch。 |
| 日志模式 | `Logging/SqueakLog.cs:26–50`；`Settings/GameSettings.cs:67–82` | Auto：US_DEV 有效、非 US_DEV 无效；Enabled 始终详细有效，Disabled 无效。与 Prefs.DevMode 是两轴。设置调用还排队保存，并在有效模式变化时重置日志 once／512 通道；不重建响应 owner。必要 warning/error 不依赖详细模式。 |
| 回应 trace 预算 | `CompSqueaker.cs:198–220` → `SqueakerRuntime.LogResponse:590` → `SqueakLog.KernelResponse:179` | 每 Comp 64 次；末条 fmt=3 `diagnostic_budget=exhausted`。仅在详细无效期间**发生回应记账**才把 Comp 预算恢复为 64；切模式／开关窗口本身不保证恢复。全局 SqueakLog.ResetSession 不遍历 Comp。没有续期、丢弃总数或玩家重启控制。 |
| 其他有界通道 | `SqueakLog.cs:119–153`；`SqueakLogProtocol.cs:405–417` | ptrace 去重／ltrace 各 512；fmt=3 故障 once 域最多 1024 键。这些并非回应历史、不得随 DIAG-04 改动一起删除或改成循环 buffer。 |
| 成本采样 | `UI/GameSettingsWindow.cs:52,126–152,202–206,245–257` | 只在该 settings Host 创建时显式 Enabled 开始，32 Host passes 后结束；提前 Close 先 drain，给 partial／真实 passes。不是 FPS、不是 32 samples。与回应 64 条无共同预算。 |
| “报告”的真实对象 | `UsKernelSettingsSource.cs:420–498` → `UI/Kernel/UsTextFitAudit.cs:462,551–624` | 这是每 Host 显式 Dev geometry report：等新完成 pass，保同次 snapshot／text，写 ltrace；Release carrier 如实 unavailable。无回应报告、文件导出、剪贴板命令或玩家反馈提交入口。已检索 US Source 及 FL Source，未发现 systemCopyBuffer／clipboard 写实现；FL TextField 仅说明原生文本框拥有剪贴板。 |
| 结束／退出 | Overlay `MaintainLifecycle:176`／`CloseSession:483`；DetailWindow `PreClose:165` | 换地图／无当前地图关闭整个会话；主窗关闭同上。窗口缩为 bar 不停止观察。业务 despawn／destroy／map removal／游戏重建由 Runtime.ReleasePawn／ReleaseMap／ResetSession 原路径释放，诊断不应调用它们。 |

## 名称像只读，但当前实际会做什么

| 读面／入口 | 实际成本或写入 | 候选采用 |
| --- | --- | --- |
| `Comp.GetDiagnosticSnapshot` | 重算发声器官／Talking health 能力；拼完整 native DiagnosticText，含 LINQ／string.Join 分配。 | 新 UI 只读同一已保存结果的 typed snapshot；不为诊断重采健康业务事实。旧 DiagnosticText 保既有入口和已评估文本语义，仅修初态。 |
| Overlay.RefreshIfDue | 显式调用 `MaintainPeriodicPopulationDiagnostics`；后者进入 population.Maintain，可能 EnsureOwner、O(N) Rebuild，更新生产也会消费的 population snapshot。 | 删除诊断对生产维护的驱动；需要人口事实时只用现有 `SqueakPeriodicPopulation.GetSnapshot` 并保 Stale／尚无值。生产 StateRhythmFactor 的维护入口不动。 |
| `UsDiagnosticsSessionSource.Detail`／`PageRows`／Bar | Detail 每读分配 gate list／详情；PageRows 每读建页列表；搜索缓存重建时遍历当前地图。 | 显示 getter 不拿自己名字担保零分配。复用成功快照及现有 revision 缓存；只在修订变化重建有界显示／反馈投影，不为本轮做泛用性能平台。 |
| `Find.Selector.Select` 行点击 | 真实游戏选择，可能进入已挂接 Select 回应。 | 保普通选择语义，不称“零业务副作用”。想避免为选目标额外选择时，可用既有屏外搜索→锁定或已选 Pawn；记录按钮／复制不能调用 Select 或补触发。 |
| `SqueakerRuntime.SessionFor`／TryTick／TryObservedEvent | 可创建 owner、采事实、随机与派发。 | 不是读面；禁止诊断调用。只用 sessions.TryGetValue 的新增窄读入口。 |
| `SetDevLoggingMode` | 修改设置并排队持久化；有效变化重置日志 once。 | 玩家记录不调用它；模式由原设置独立持有。 |
| geometry 的 capability/status | 现有 owner 缓存 capability 探测，有自己 scope／instrument 条件。 | 不借用为回应报告前提，也不为 Release 玩家启 Dev 或新建 scope。 |

## 责任与直接检查映射

- **US native adapter**：结算事实、初态、序号与 typed read；**US Overlay**：阅读角色、录制、快照失败状态与生命期；**US UI**：可达入口、状态、复制动作；**US Logging**：既有 mode、保护、可续期限流。Core／FL 均不新增写需求。
- FL [有界观察](../../../../../ferritelib/docs/openspec/specs/core/observation/spec.md)／[出站边界](../../../../../ferritelib/docs/openspec/specs/contracts/observation-boundary/spec.md)是通用边界参考，不提供 US 回应会话、业务事实或报告。此处 repo 外链接仅用于本机阶段 A 审阅；不作为产品发布导航。
- `tools/UniversalSqueakerUiLogicTests/UsDiagnosticsLogicTests.cs`：现有 SessionModelRules、ProvenanceRules、PreviousBandRules、CooldownPairRules、CollapsedBarRules 等只覆盖中立 model／投影。`UiSourceInvariantTests.cs:119–126` 对原 Host raw 写绑定精确 pin=12；新控制优先走已有 `UsWriteBindings`，不扩大 raw 豁免或改 pin 来绕过。
- `tools/UniversalSqueakerKernelHostTests/Current/Program.cs:69` 实际包含 DiagnosticsPanelLaneTests；`--lane=existing-…` 可选既有子项。覆盖原 Host 构造、写路由、collapse、窄导航、原 shell 几何／已种植投影失败；fake source 不是 Overlay／PawnResponseAdapter。旧 `DeclarativeDiagnosticsLaneTests.cs` 未列入当前 csproj 的 root 编译清单，不能计作当前可执行覆盖。
- AudioBinding csproj 链入真实 `Game/SqueakerRuntime.cs`／Logging／Core，但 `RuntimeDoubles.cs` 的 Comp 只是保存 LastCandidateRequest；没有真实 Comp 快照／Overlay／64 条限流。已有 `--refactor-through`、S4 caller 和 DaSelect 的 DiagnosticText／请求／owner 断言可保其层次，不能据此签新 ring 或初态。若新增 native 诊断读门使该 double 缺编译成员，阶段 B 只可作必要形状接续，不能因此新增场景／改变行为断言或声称覆盖真实 Comp。
- LogTests 的既有 VerifyDisabledModeGate、VerifySilentFailureBoundary、编码／once 等可以保旧门面保证；没有实际 Comp 续期、ring、复制或初态 case。当前 Host 的 actual DLL 证据与 r4原生普通观察保各自原身份，不重跑十五动作或性能矩阵来冒充本轮覆盖。
