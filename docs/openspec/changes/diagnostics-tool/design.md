# Design — 阅读继续，记录有界，反馈可交

状态：本方案已由 WO r2／DSP A002 采用并完成本地源码实施；[现行 diagnostics](../../specs/diagnostics/spec.md)为唯一合同。以下保存设计与采用依据，不是原生验收证明；当前证据见[实现状态](../../../development/specification/open-issues.md#d06-玩家诊断状态录制与反馈)。[source-map](source-map.md)保阶段 A 修复前源码观察，[spec delta](specs/diagnostics/spec.md)保实际差量。

## 1. 玩家只需做这条路

1. US 语音 → 设置与工具 → 诊断与依赖 → **打开回应诊断**，再正常关闭设置窗，留下不暂停的诊断条。有地图即可用，不改日志模式、不启 Dev；无地图时给“进入地图后可观察回应”。设置窗原本 forcePause，工具不暗关它或丢弃其草稿；入口旁给这一步必要提示，主窗仍可缩为 bar、与游戏并行。
2. 查看已选 Pawn，或用原搜索／锁定找到目标。点击 **开始记录此对象**；状态固定显示稳定对象、正在记录、起点与保留数量。开始不选择 Pawn、不发回应。
3. 按平常玩法复现。玩家切选另一对象、移动镜头或折叠窗口，不会暗中换录制目标。bar 始终说明“正在记录谁／最近保留数／已覆盖数”；不因日志限流显示为“无回应”。
4. 点 **停止记录**，再点 **复制反馈**，把文本与“发生了什么／预期什么”交给维护者。无需找 Player.log、定位 Config、核内部协议或制造异常。

打开窗口只是实时阅读，不自动录制历史。未开始也可复制当前已知状态报告，明确 `NotRecording`／无本次历史；不能补造打开前的事件。已有 Debug Action 保留作开发快捷入口。

## 2. 先恢复 DIAG-01，不扩展业务合同

新 native 只读 envelope 由 `PawnResponseAdapter` 提供；Runtime 只用已有 sessions.TryGetValue。它与 `Core.ResponseSession` 的关系仍是投影，不创建第二结果权威。

| 数据状态 | 判据／来源 | UI 与反馈应说什么 |
| --- | --- | --- |
| NoOwner | 没有现存 adapter；另带原 PolicyStatus | 尚无回应 owner／未观察到评估；不创建 owner，也不把 OrdinaryActive 当成已评估。 |
| NotEvaluated | adapter 已建立，尚未正常完成 Respond | 尚未首次评估。保已知主体／初始 NextStateTick，动作截止／回执为不可用；不能用 tick=0、空动作或 receipt=null 猜测此状态。 |
| Evaluated / NotRequested | 正常完成的 DecisionOutcome，无 Receipt | 展示真实 Reason，即使 Action 为空。HardDeadline／UnknownFacts 等不等于原生播放失败。 |
| Evaluated / 四回执 | 原 Receipt.Result／Reason | Dispatched、AlreadyActive、Rejected、Indeterminate 原义保留；Dispatched 不称“听见”，AlreadyActive 不称新播一次。 |
| ReadFailed | 本次读取普通异常 | 保存失败阶段／异常类型、尝试时间／次数；有 last-good 则标旧值及其成功时间，没有则无值。不能以空 snapshot／0 代成功。后续刷新继续尝试。 |
| Stale / Previous | 已知读失败、owner／游戏代次失效，或展示历史评估／派发 | 旧数据保自己的时间和配置身份，不用当前设置重算历史。成功读到了旧决定与“本次读失败沿用旧 snapshot”分别标记。 |

adapter 增加纯诊断 `HasEvaluation`／递增 observation sequence：只在原 Respond 正常完成、LastOutcome 已保存处更新，放在可选 publication 之前；不新增机会或复位 state。无动作时 native DiagnosticText／CaptureResponseLog 不调用动作截止字典；局部初态文本准确说未评估。Core.DecisionState 的字符串键合同不改。

UI 消费同一成功 envelope，不再先 Observe 再独立拼一份可能失败的全文。保 `DiagnosticText` 旧入口及已评估后既有标识／含义，供现有合法 caller 使用；新 UI／报告不解析这段文本。

**必要相邻修复：** `GetDiagnosticSnapshot` 不再重采 Vocal health，Overlay 不再维护生产 population。若要显示人口，只读现有 GetSnapshot 并标旧值。当前“Ready = 有 ActiveAction”不能表示 OneShot 可回应，改显示可证的“尚未评估／上次结果／当前持续槽”；不推测下一次资格。模拟 tick 截止与 accepted-interaction 的真实秒截止分开；对交互只展示原已捕获机会秒数／截止和“评估时”的关系，不用 game tick 或新业务时钟伪算当前冷却。

## 3. 两种观察、一个现有 owner

### 阅读会话

Overlay 继续拥有原视口／选中／锁定角色、0.25 秒详情刷新／0.5 秒视口扫、原稳定分页与窗口级联。它们是发现和低频读面，不是录制目标集合。保当前地图搜索最多 64 个结果，同时最小补一个 `HasMore/Truncated` 事实；可在遇到下一匹配时停止，不为精确总数再全表遍历。搜索前缀与 ring、日志配额是三个不同边界。

开窗不再用全局 `Comp.DiagnosticsEnabled` 驱动全地图详细 payload。明确保留该公开字段及显式外部置 true 的原诊断用途；Overlay 的 Begin／Close 不再代写它，而以自己的 sessionActive／per-Pawn 角色服务新工具。轻量最近槽在“旧显式 flag 或新工具正在跟踪此 Pawn”时写；详细 payload 在“旧显式 flag 或正在录制此 Pawn 或原详细日志本次配额可用”时准备。新窗口默认不会把旧全局 flag 打开，也不会在关闭时撤销外部显式请求。旧读入口不增采用条件；普通 Release Auto 不全地图高频详细捕获。

### 显式记录

在**现有 Overlay 内**加一个可选 recording slot，不建总管、订阅总线或业务 queue。它保一个目标引用（仅录制期间）、稳定 `Pawn/<thingIDNumber>`、本次捕获 ID、游戏代次／map ID、开始／结束时间、状态和有界 detached 记录。它不改 SessionModel 原三个角色含义；录制目标不要求永久占一个详情窗。

状态只有 `Idle → Recording → Stopped`，停止原因单列：User、PanelClosed、TargetUnavailable、MapChanged、GameEnded／GameReplaced。开始固定此刻显示的合法同图目标；无目标给可用提示。已有记录时按钮明确为“开始新记录（替换上次）”；该显式动作清上次 slot 后开始。活动期间不能改目标，必须先停止再开始新记录。停止幂等；清空只在非录制状态可用，只清诊断 slot。所有按钮到达 owner 时复核其状态，不依赖渲染时的旧可用性。

唯一事件来源是原 `PawnResponseAdapter.Tick → Comp.RecordKernelOutcome` 的**已结算**边：先保原内存结果，再把目标相符的 detached 结果追加到 slot，最后才到可选日志。计数／格式化／ring 的普通失败只影响自己的诊断分支；不重试 Respond。读刷新不追加事件，相同 observation sequence 不重复计为新决定。复制、刷新、开始和停止均不调用 SessionFor／Tick／Maintain／Select／Rand／输出／存盘。

### 最小有限策略

- 一个 recording slot，只收一个目标；ring 保**最近 64 条**，不按内容去重或主动采样略过。相同内容的两次真实结算是两条，不把刷新时重复读同一序号当新结算。满后覆盖最旧，`observed`／`retained`／`overwritten` 分列，观察不停止。无目标原生机会产生时只会没有新结果，不能承诺所有游戏动作都进 owner。
- 每条记录保必需 identity／时钟／结果 primitives；可变文本每字段最多 256 个字符，集合最多 16 项、每条可选详细文本最多 4096 个字符；截断有字段名／原因，不截断稳定 Pawn 数字身份。不复制整份 EffectiveActions 或 FactSnapshot。超长 Action／AudioId 显式截断，不能把截断值当完整可解析身份。
- 开始时可带一份**起点以前**的最后已知结果，标 `PreRecording`，不算 ring 新事件。没有事前详细 payload 的字段写 NotCaptured，不回放事实／随机来补。
- 捕获失败有 `captureFailed` 计数和最后失败阶段／类型；不清现有记录，下次已结算结果继续。若无法证明完整覆盖，报告说 incomplete，不以 ring 未满称完整。
- 环形容量限制内存保留，不保证录制覆盖固定时长；开始／结束、最早／最晚保留、覆盖和缺失事实共同定义本次证据范围。

## 4. 回应日志：改前缀，不重做日志系统

保原 Auto／Enabled／Disabled 与必要 Daily 故障。可选 response trace 仍可在不开窗口、不开始录制的情况下使用；不把新工具采用条件强加给旧入口。

将 Comp 的终身 64 前缀改成**每 Comp、每 60 个真实秒最多 64 次详细输出尝试**。采用首个详细输出尝试锚定的固定窗；到期后下一个真实结果以当时单调时间开新窗，不追赶空窗。用现有原生单调时间，仅管诊断配额；无新 Timer、每 tick 输出或业务资格时钟。到额后计 `rateSuppressed`，下一个时间窗的首个真实结果自动重开并携前窗抑制摘要；没有新结果时不凭空造日志。达到限流的末条标可恢复的截止，而非 exhausted-until-unknown。内存 ring／快照继续，下一窗不重放跳过的日志。

在 Comp 保窗起点、attempted、抑制数、累计抑制数和当前状态；目标报告可读这些计数，不遍历全地图汇总。详细无效期间发生结果仍按原语义重开该 Comp 的下次详细预算；快照清空／录制起止不重置它。原生 Comp 生命周期释放其计数。

格式化仍在有效 mode 和配额门后。为准确报告日志出口状态，给既有安全门面增加 internal 结果回传，保原 public `KernelResponse` 签名作委托：区分格式化失败、sink 调用返回、普通 sink 异常；“调用返回”不称磁盘持久送达。门面失败计数不能影响先完成的内存记录。不要复制 raw sink 或新增任意日志入口。

ptrace／ltrace 512、必要故障 once 1024、geometry 及 settings 32 Host-pass 完全独立，不借本改动修改其额度／reset。64／60 秒是有界起始推荐；若今后有实测需要，调整只属诊断政策，不触碰声音频率。

## 5. 窗口关闭、对象丢失与退出

| 事件 | 阅读与录制 | 保留／释放 |
| --- | --- | --- |
| 切选／搜索／镜头／折叠 | 阅读沿原角色；正在录制的目标不变 | 不清 ring，不为录制强制选择或新开锁窗。 |
| 关闭详情／解锁 | 按原规则释放该阅读角色 | 若该 Pawn 正在录制，单目标 recording slot 继续；不借录制重建该详情 entry。 |
| 停止／清空 | 停止冻结；清空只在停止／Idle 有效 | 停止即释放目标强引用；保 detached 记录和元数据，清空释放这些数据。 |
| 主窗关闭 | 原阅读会话结束、级联关详情；活动录制转 Stopped(PanelClosed) | 保**一份当前 Game 内**的 detached 记录，重新打开可复制但不自动继续；不留 Pawn／Map／Host 引用。 |
| Pawn 死亡／despawn／destroy／离开本图 | 停止为 TargetUnavailable | 已记录证据保留；业务释放仍由原生命周期处理。 |
| 切地图／失去当前图 | 按原路径关阅读窗，录制停止 MapChanged | 当前 Game 内保旧 map 标记的 frozen 证据；下一次打开清楚说“上次地图”，不冒充新图状态。 |
| 退主菜单／新游戏／载入另一游戏 | 结束并清除当前 Game 的保留 slot | 以现有 native 生命周期明确通知，必要时 Root guard 检查；即使主窗已关也释放保留数据。不得调用业务 ResetSession 来清观察。 |

这是新工具的内存寿命保证，不是持久化／跨存档恢复。具体接入点为 `ResponseSessionLifecycle` 的真实 game-start/load 通知及 Root 现有 guard：通知只标记，Root 主线程边界使 Overlay 的诊断 game 代次前进并关窗／清 slot；Game 构造／加载不直接操作窗口。Root 在窗口已关闭但有保留 slot 时仍检查 Game 退出，不刷新快照。代次是诊断归因，不成为业务时钟。非游戏用途的 Runtime.ResetSession 不被重新定义成 UI 命令。窗口关闭后无刷新、事件捕获或强 native 引用，只留有界 plain data。无需保留多个历史会话或地图档案。

## 6. 复制的内容与最小接线

新增反馈格式 `usdiag-feedback/1`，与现有日志 fmt=3、geometry report 分开。一次点击从当前同一数据修订冻结副本并生成文本，不刷新业务；记录中复制标 Recording／CopiedAt，停止后复制含真实结束原因。未记录时给状态摘要／NotCaptured。报告 UTF-8 上限 **64 KiB**；先保头部和缺失／截断说明，再优先最新记录，省略的记录数明确标注，不写半条结果或不闭合结构。

| 报告部分 | 允许来源／含义 |
| --- | --- |
| 实际版本 | 当前载入 US／FL assembly 的 informational version、AssemblyVersion、MVID、可用的 AssemblyConfiguration；US build 展示沿既有入口，但 `build=dev` 不推断编译配置或 clean source。固定引用信息若未独立比对，不称与载入 FL 完全一致。游戏版本／DLC只从可用公共 runtime fact 读取；尚无接入的项为 Unavailable，不把 csproj 的目标版本冒充运行版本。 |
| 数据阶段／身份 | capture ID、game 代次、map ID、稳定 Pawn 数字身份、Race／合法 child、初态／评估序号、读取成功时间与本次生成时间。界面可显示 Pawn 名，报告默认不含自定义姓名或 LabelShort。 |
| 范围与健康 | Idle／Recording／Stopped、目标、起止／停止原因、观察总量、保留首尾、覆盖／捕获失败／字段截断／报告省略、快照失败／stale、mode／effective 和可用的目标日志限流计数。日志送达不可确认时如实说明。 |
| 回应结果 | requested action／actual action、root（当前 native 保存面未单列，未接入时 NotCaptured）、机会／event fact／operation、模拟 tick 或真实秒、Reason、无请求或原四回执、receipt reason、确认／截止／W／持续槽中实际已有部分；请求存在才带角色／准确域／tier／pack／entry／AudioId／配置身份及当次来源。不能从缺请求猜一次播放失败。 |
| 详细解释 | 仅该次已捕获、受界限的字段来源／缺事实／随机记录／局部参数；未捕获、旧值、未支持各有标签。不要求为缺字段重采或修改 Core 决策。 |

默认白名单不收原始 `DiagnosticText.Details`、异常 message／stack、原生对象 ToString、用户文件路径、原始 Player.log、凭据或完整设置／mod 列表。稳定身份按其实际协议去控制字符／编码，保合法 `packageId:DefName` 和斜线；自由原因用已有安全消息清洗，不把身份冒作盘符路径切碎。来源可控字段若只有未知自由文本或不能安全归类则不导出，标 Redacted／Unavailable。不把日志的百分号编码误当通用脱敏。

native 主线程通过 Unity 的公开 clipboard 写边接到**显式复制按钮**，不读剪贴板、不写文件、不上传。赋值正常返回才显示“已复制”；失败给阶段／异常类型与“复制未确认”，不声称剪贴板未改变或已发送给维护者，不丢记录。是否目标引用提供该 setter 由阶段 B 的实际引用编译确认；当前仓库没有可复用的 US 复制实现。

接线位置：原设置诊断 Section 加入口；原生成 MainXml／DetailXml 增必要状态／控制；新控制使用原 Button／Wrap／Scroll、US 普通字号与测量，不能挤进原 250 宽 toolbar 的固定一行。collapsed bar 继续只显示精简状态，展开可操作。复制和停止可从主窗访问，即使选中别的对象也不会丢失 recording slot。

为保现有公开 `IUsDiagnosticsSource` 的合法实现，不新增其必实现成员；增加同子系统可选 `IUsDiagnosticsCaptureSource`，只由生产 main／detail source 实现。原 Host 以可选类型检测绑定新状态／控制，旧 Create/CreateMain/CreateDetail 与原 source 仍合法，未采用者显示 unavailable 且新操作不可执行。新增写绑定用现成 `UsWriteBindings` 和原 session content revision；不扩大 `UsDiagnosticsHost` 原 raw-site 豁免。业务保存 source 与 recording source 不互相代管。

## 7. 采用、检查与剩余证据

采用后只更新 diagnostics baseline 的 DIAG-04／06／07，UI-04 加到新入口的导航说明；source/status 映射、普通使用帮助和 MEMORY 旧 debug-only 记录同步改为单一当前说法。DIAG-01 恢复不增加 spec delta。旧 64-prefix 日志含义留历史，不改旧收据。

阶段 B 的必要自动／半自动证据以现有入口为先：实际 product 引用编译；现有 UiLogic／source invariant；当前 Host 的 existing-diagnostics 子项及受影响 workspace／资源检查；现有 LogTests；native adapter 改动时现有 AudioBinding caller 的 owner／DiagnosticText 断言。现有 Comp double／Host fake 的覆盖边界见 source-map，不新增 case／probe／fixture／runner，也不把禁止单测换成全矩阵 E2E。

**目前没有能自动证明新 ring 满额继续、60 秒续期、首决策前真实 Comp 读取、clipboard 或完整退出链的现成用例。** 这些不能由旧绿签发。工程 owner 先以允许的源码／实际调用证据关闭确定的接线错误；有获准普通原生环境时观察正常打开／开始／复现／停止／复制、不同窗口态和常规离图／重开。普通玩家只看可达交互和报告，不负责造异常或凑溢出次数。精确满额／续期与异常组合若仍无允许方法，保为工程未覆盖项，由 CO 决定技术接受范围；不称全合同已动态验证。

阶段 A 的停止点已履行，CO 采用后完成阶段 B。无 FL 通用缺口或外部上游待实现依赖；本地技术候选交 CO，原生与精确动态未覆盖项按上节保留。复制的唯一 native setter 经本轮 PM／CO 窄裁定，审计单列并核完整成员，不扩大 renderer 白名单；公共 clipboard 接缝采用或功能退休时回收。
