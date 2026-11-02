# 实现与采用映射
状态：开发记录，不替代[现行规格](../../specification.md)。源码HEAD仍3e42cc180f91f5f58b040724e4ad76ea060802e6，2026-10-29合同修复为其上未提交实现；继承的规格重建成果保留。当前技术配对与运行证据不改签旧接受身份。

| 能力／连接 | 实际caller → owner → consumer（路径相对Source/UniversalSqueaker） | 寿命／证据边 |
| --- | --- | --- |
| 定义 CORE | Game启动／作者Collect → Core/Definitions/DefinitionCatalog → 固定条件求值 | 完整批次、修订后只读；完整Core可独立于游戏编译 |
| 稳定响应 RESP | CompSqueaker／原生事件／声明TryProcess → Game/SqueakerRuntime的PawnResponseAdapter → Core/Responses/ResponseSession／ResponseDecision | 数字Pawn身份单owner；配置发布不清截止／W |
| 输入发布 CORE／PUB | 定义、Settings.CaptureBehaviorConfiguration、SqueakerRuntime.Audio → RuntimeView三引用 → 本次决策／持续授权复核 | revision仅发布者内；对象身份变化也触发复核 |
| 普通政策 POL | GameBootstrap定义／音源／comp／配置就绪 → InstallOrdinaryPolicy／UsResponsePolicy.CreateOrdinary → 新及已有owner输入 | 普通Release自动，不要求Dev或UI；Dev仍显式单次 |
| 公共通知 CORE-05 | Authoring/SqueakerNotifications.Declare → TryProcess → 注册事实／同owner | 返回处理，不等派发；XML与外部C#例在examples/0.6 |
| 行为配置 CFG | GameSettings.BehaviorSources／ActionTuningRecord → BehaviorConfiguration.Read → 同次有效值／字段归因 | 作者层先玩家；视图和Read防外部写入 |
| 主体族默认 CORE-05／CFG-02 | ResponseSession的Respond／Review → BehaviorContract.Defaults(action, policy, frame.Domain.Family) → 原BehaviorConfiguration.Resolve／授权／决策 | Pawn保原默认；NonPawn不注入Pawn专属默认且不运行Pawn Talking门，显式非法字段仍由原校验拒绝；旧无family公开重载保Pawn语义 |
| 原子来源／行 PUB | SqueakerMod.TryCommit… → GameSettingsPersistence → UsAtomicConfigFile／同保存owner → ApplyToRuntime | persist-first；Committed后发布失败只修发布 |
| 普通保存 PUB | setter → QueuePersistence → SettingsSaveOwner的Tick／Flush → base.WriteSettings | 实时0.35秒、代次、关闭一次重试；Scribe临时值finally恢复 |
| 旧绑定恢复 CFG-06 | UsKernelSettingsSource.Voices安全读 → 选定域继承／同ID重绑 → 原子owner | 159f651修复已技术关闭P1；精确旧快照／同ID重绑／另拒绝域／原生点击仍有原证据缺口 |
| 音频输入 AUDIO | 启动AudioInputBuilder／UsAudioBindings → AudioCatalog／SqueakPoolRegistry → 已选AudioCandidate | 选包只配置过滤，不重建AudioCatalog |
| VF1 FA | VoicePacksPageModel／同Store非UI保存／恢复／删除 → 独立文件提交 → Store.PublishCommitted → SqueakerRuntime.PublishFallbackAudio → AudioCatalog → 原ResponseSession选音 | 原子候选先构造，成功不重写；Audio最后替换，提交后采用失败保last-good；不清Session／W或重挂comp |
| 原生输出 OUT | 已选PlaybackRequest → PawnResponseAdapter原生输出 → UsInstanceOutput／Sample／Sustainer → PlaybackReceipt → 原owner结算 | 不重选；四结果，局部L与当前G分开 |
| 域准入 GAME | GameBootstrap → VoicePackCompAttach／SqueakXenotypeCatalog／GameRuntime.ResolveContext | 全部loaded canonical事实独立收集；原resolver保Biotech／同对象／歧义及准确Race域授权；浏览不授准入 |
| 界面 UI | SettingsWindowOwner → UsKernelSettingsHost的真实manifest／binding → UsKernelSettingsSource → 上述业务入口 | 每窗一host；颜色、帮助、页面state不成为设置权威 |
| 诊断 DIAG | owner已保存结果 → 内存RecordOutcome／详细出口 → UI与原生日志 | 具名事件／取证→安全门面→唯一sink；先内存记录，日志普通失败不改业务，见下文 |
| 普通稳定相位（已采用） | GameComponent世界就绪 → adapter供seed／稳定ID／独立目标倍率 → StatePhaseInput／ResponseFrame → 原ResponseSession／DecisionState | 普通CreateOrdinary实际安装us.state-phase/1；Kernel按POL-03算归一化窗口并保存H抬期输入。零seed有效，未ready为null，跨Game清理；无seed旧CLR入口保持 |

具体原版方法／参数／事实与Unknown见[facts](facts.md)；Core完整原版工作清单在[GAMEWORK附录](../../openspec/specs/game-work/spec.md)。逐源码删除／替代映射继续保留[refactor-removals.csv](../../refactor-removals.csv)，它不是现行规则清单。

## 保留名称的不同含义
ActionPlan是不可变动作输入，不是会话／请求／成立证明。BehaviorValues是稀疏贡献，EffectiveBehavior是fold结果与归因，BehaviorConfiguration是配置修订，RuntimeView是三个引用。PlaybackRequest与Receipt分别是输出申请与四结果。XML sound／sounds是SoundDef引用，EntryKey保条目身份，AudioId保真实资源身份。UsBehaviorPresetDef是玩家显式复制模板，不是绑定作者来源。

通知原Notify已为TryProcess；Mod Change／Override现为TryCommit…，GameSettings普通行现为TryReplace…。Help目录使用Ref／TitleKey／OverviewKey／LabelKey／TextKey；HelpPanelDisplay才含TitleText／LabelText／BodyText。当前public FL消费用HelpRef／CurrentHelpRef／CurrentHelpSource／PublishHelp／HelpGracePasses／BeginHelpPass／ActiveTabBind与*Bind，详见[固定消费者指南](../../fl08-consumer-migration-zh.md)。

2026-10-31对象与动作修复保原CLR入口：DefinitionCatalog在XML／C#成功声明时登记通知ActionId→FactId，并跨定义修订保留；原字符串TryProcess仅消费真实声明，不拿HasAction或C#私有字典代替。已声明通知不再根key过滤，独立根、子合同和嵌套混合显式事实消费者参与同owner择一。状态Stop在已有选约时与四回执结算使用同一CheckShared；选约前失败保既有共享下界。

普通相位仅由Kernel计算并排程：首次、成功、未请求／资格／概率／无音重试、四回执及真正提高H且超过旧机会后的抬期均只向后对齐。每个新排程保存其窗口与主体pattern，后到H不采新速度、不追加W；首次H抬期用初次采样倍率的短重试预算，不能复用600。adapter另供独立TargetSpeedFactor，原StateRhythmFactor→StateWaitFactor→W计算仍只乘一次速度。已采用预算与单位以POL-02/03为准，固定uint32混合和精确归一化映射以RESP-07为准；新输入晚就绪只影响后续排程，Stop只管资源。原四窗口显式构造和旧无seed入口均保留。

A002按CO明确许可仅补既有AudioBinding RuntimeDoubles的Current.Game／Find.World／World.info／WorldInfo.persistentRandomValue与TickRateMultiplier形状，默认null／1f，不造世界、seed场景或新case，原through场景和断言保持。最终Release、既有Core和同一through已重新运行；证据与新的固定工程接口在`dist/rw16-17-action-ux-20261031/INTERFACE-r4.md`。through默认null只证明legacy无ready路径，真实seed输入、归一化窗口分支和原生听感不由这些旧绿项代签；A001固定r3及原失败保留原依据。

## 固定依赖与公开边界
2026-11-01发布门窄修显式消费FL的`dist/release-gate-20261101/producer-release-r1/FerriteLib`（MVID `f9235665-740f-4946-a45a-fa1c52512c12`）。US保持Private=false，实际Release与Host运行核同一编译引用收据；新固定产品、源码形式及相关检查边界统一见`dist/release-gate-20261101/INTERFACE-r1.md`。这是HEAD+dirty本地工程交付，不是正式rc资产；下列历史配对不被改签。

US引用FL API范围[0.8.0,0.9.0)、Private=false，产品不夹FL或Harmony DLL，实际编译输入以该候选嵌入收据为准。3e42原本地Release准备使用固定6ec0c31生产者；它不是未来已发布rc2/mainSHA。2026-10-29合同修复技术配对为US3e42+dirty／FLcf1cf64+dirty：US显式消费FL固定producer-release-r1，实际Dev／Release编译、完整当前Host1747项及DLL-only作者编译通过，staging带同一FL目录；这不是clean release或已发布资产。此前ea610fa／FL833c14f4总体内部试玩与增量性能接受保持原身份；A009r2 US68b30e3／FL36fa恢复包亦不改签。此处不由移动FL HEAD推断载荷。


## 当前日志协议与读取方

- 生产原生出口只有`Logging/SqueakLogProtocol.cs`的`SqueakLogSink.Emit`。`SqueakLog`保护各具名入口的模式判断、详细取数／格式化和sink；普通managed异常在此止步，不递归报告同一失败sink，不宽catch业务函数。
- 当前响应唯一行：`usdiag fmt=3 evt=kernel.response`，在原owner结算后从已捕获事实输出。固定身份字段为`build/build_id/subject/requested_action/action`；机会字段为`opportunity/event_fact/operation/tick/monotonic_seconds`。模拟机会不伪造交互root／秒。
- 四结果字段`receipt/receipt_reason/reason`分别是实际输出回执／输出原因／决策原因；没有输出申请时`receipt=NotRequested`。`confirmed_tick/shared_until_tick/action_until_tick/interaction_until_seconds/next_state_tick/simulation_attempt_tick`保原时钟单位，未设置的可选值省略，交互无截止时原`-Infinity`哨兵不改为零。
- `role/domain/child/source_domain/tier/pack/entry/audio/configuration/active/end_reason`保本次主体域、真实供给与owner槽事实。`details`为百分号编码的原诊断文本，含有效参数／字段来源、门、观测证据、随机记录和缺事实；关闭详细时不读取或构造。回应 trace 现每 Comp 每 60 真实秒最多尝试 64 条，末条标 `rate_limited_until_window_renewal`，携 `rate_window_until_seconds`；新窗首个真实结果带 `previous_window_suppressed`，不回放略过行。详细无效期间发生回应仍重开该 Comp 的下次预算。内存 RecordOutcome／目标 ring 在可选日志之前执行。
- 诊断 UI 读同一 owner 的 typed `CaptureDiagnosticEvidence`／Observation 与 Comp 内存 snapshot，不解析日志或全文 DiagnosticText、不重采健康事实或维护 population。旧 DiagnosticText 入口保留，首次 Respond 前准确报 NotEvaluated，不把 null 送入业务字典。仓库没有旧裸 `usdiag response`／OneShot 机器 parser，不建双解析器。fmt=1／2 保未改义字段；`fallback.profile.store_failed` 为 Daily warning，带 target、六具名 store stage、异常类型和清洗原因；未知 payload 只给文件名，不输出 Config 目录，旧两参数入口仍合法。

## 玩家诊断的真实采用链

设置诊断 Section 的 `open-response-diagnostics` → native source facade → 原 Overlay.BeginSession／主窗；普通 Release／Dev 均可，不开详细日志。设置窗仍按原合同暂停／持有草稿，玩家正常关闭后留诊断条。旧 Debug Action、IUsDiagnosticsSource、Create/CreateMain/CreateDetail 和旧 row/detail 构造入口保留；新控制是可选 IUsDiagnosticsCaptureSource，不强加给旧 source。

原 PawnResponseAdapter.Respond 正常完成 → 保存 HasEvaluation／ObservationSequence → Comp.RecordOutcome → Overlay.RecordSettled 单目标最近 64 条 ring → 可选安全日志；Snapshot 只读已保存字段，读失败保 last-good／时间／类型／次数并标 stale。没有 owner 时不创建，尚未首次评估与空动作的真实 NoRequest 分开。当前 native factory 报已保存结果／历史截止，不把 ActiveAction 或旧 16 行纯投影 fixture 当作当前全部发声资格。

Overlay 独占一个录制 slot；选中／镜头不改录制目标，满额覆盖并计数。停止／主窗关闭释放 native 目标，保当前 Game 的 detached 数据；换图标原 map，新 Game／载入／退出清除。复制只格式化该修订白名单 `usdiag-feedback/1`，上限 64 KiB，保实际载入 US／FL version／MVID 与缺失／限流，不导出 DiagnosticText 原文、姓名、配置或日志。原几何报告与 32 Host-pass 成本样本独立。

Game 构造／加载只向诊断标记 pending，由原 Root 主线程 guard 关闭窗口并清数据；不从可能的加载边直接调用 native 窗口栈。实际 despawn／destroy 同步通知目标失效，关闭还清 scratch／lastChangedEntry 的 native 引用。

显式 clipboard 平台输出集中于 `SqueakDiagnosticsClipboard.Write` 唯一 setter。PM／CO 在本轮 DIAG-07 采用中裁定其不是页面 renderer；边界审计仍计 `GUIUtility`，单列并核该完整单成员接缝，不加文件／目录白名单、不许 getter 或其他 GUI/input/paint。后续采用公共 clipboard 接缝或复制功能退休时回收此分类；不是旧门禁原已允许或用户逐行批准。

反馈公共投影对尚未采集的完整 W／持续槽细节始终输出 `window_w_details=NotCaptured` 与 `sustained_slot_details=NotCaptured`，包括 NoOwner／NotEvaluated；已有 next_state_tick、active、end_reason 只保自己的含义。没有为此新增业务 owner 读面。
- 现行Daily warning/error在人读句中保必要的已捕获action／target／pack／race／sound／reason／count及异常类型／清洗原因；包括SettingsOpenFailed、CatalogRefreshFailed、ResolverRebuildFailed、CompAttachFailed、PackRejected、TargetRejected、DiagnosticsMarkDrawFailed等。已有具名能力／API不可用且没有额外事实的事件保准确阶段，不编造原因。普通Daily info和详细trace不扩大；`HumanSentence`始终在安全Emit内先执行，只有机器详细suffix受有效详细模式控制。稳定ID只去控制字符，保合法packageId:DefName／斜线；自由原因和异常消息用原路径清洗。
- fmt=3必要故障为Daily，含具名`evt`、准确`target/source/race/sound/count/reason`及已清洗异常元数据（存在才输出）。覆盖kernel诊断、Sustained End、作者声明、音频观测／绑定／volume、capability、前置依赖、行为刷新、保存／repair／candidate／format、preset、诊断UI、native entry、overlay、save status、theme、VF1音频发布和canonical采集。原始异常不被日志异常替换。
- `ptrace`保FL原始click-routing字段及忽略pointer坐标的去重；`ltrace`保原布局／geometry／UI.cost字段与Host-pass单位，不称FPS。各session最多512行，末行标exhausted；fmt=3故障once域最多1024不同键，满额行标`once_budget=exhausted_until_reset`。游戏session释放或有效日志模式切换经`SqueakLog.ResetSession`统一重开；无每tick队列或异步bus。
