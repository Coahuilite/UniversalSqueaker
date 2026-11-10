# 现行保证与开放实现偏差
本页是[规格](../../specification.md)的实现与证据状态；技术修复、原生运行与玩家听感分别记录。

## US06 Kernel维护（2026-11-09）

D1已在本B维护线接通：启动就绪的单一保存owner由`Verse.Root.Update`主线程postfix独立维护，不再由设置窗绘制驱动；暂停／无地图仍用原0.35真实秒，长事件／Scribe占用等到安全更新。显式关闭Flush／Retry、物理失败代次、写中编辑与PublicationPending分离保持，K1精确原供给复核／K2每次维护一次发布修复不变。

O1／O2现分开保存最近决定、当前槽、最近维护／实际复核采用身份及终止观察时点，原Align保当次W／phase窗口／pattern，Audio引用有发布者内进程编号。原64条决定ring不回填；同录制owner另保一份可替换的最近维护快照，不制造决定序列。具体字段和原公共用途见[实现映射](implementation.md#玩家诊断的真实采用链)。

固定已发布FL rc2的新隔离Release与源码、既有受影响检查在[维护交付入口](../../build-and-debug.md#us06-kernel-maintenance-candidate)。1.6.4871引用API／字段、1.6.4850真实Root方法体、构建和平台double证据分别使用；真实1.6无UI自动防抖、暂停／无地图／场景交接、Harmony挂接、新维护快照／复制和完整退出仍需现场核验。没有新增case／probe，不据此关闭既有听感、输入／IME、B浏览A录制或Report／32-pass接受条件。

CO核出A001 r1把冻结决定传入原当前Response，导致持续槽维护结束而无新决定时ReadyFor／标记仍活跃；该候选保留但撤回推荐。r2恢复原owner.Observe当前槽／截止，独立DecisionObservation供历史；当前槽有／无改变也进入原行内容fingerprint。原D1／Core／保存证据按未变输入复用，修订只重编／检查受影响生产诊断与对应交付；具体新证据由r2交付持有。

## 2026-11-06 规格对账与当前维护候选

已补录三组导航、真实780／440与900区域断点、紧凑动作行、同源开发预览和Core性能边界；UI-07纠正旧文档漂移，不改生产政策。0.8工作台、真实Core样例、文档热改、K4正文缓存与US屏幕标记已实现，范围分别见[工作台](../offline-workbench-20261106.md)与[屏幕控件](../diagnostics-screen-mark-20261106.md)。完整同源游戏窗口／虚拟舞台未移植到本B／FL0.8维护线；独立FL0.9线已有US r6／FL v3经Tools r13实际消费与PM技术核收，用户接受仍待，不算本线运行证据；新1.7开发跟随当前接口，不锁小构建号，不能据此删改1.6维护条件。

PM已接受c3-dev完整接替r6当前候选；原r6留回退，UI／查看B录制A／展开收起采样、Report连续／原位32-pass及准确1.6输入投影／生命周期未验项转入新候选。下文r6阶段“待核收”等措辞保其历史时点，不是当前包推荐。此次仅文档修订，未新增运行证据。K5 Root.Update／Pawn扫描架构改造仍缺热点依据，未采用。

**2026-11-05 B Dev本地交付：** A009已用正式固定producer-r5-dev编译US Dev，形成`delivery-r6-dev`两根完整成品，待CO／PM核收；旧r5 Release已撤回推荐但不覆盖。实际Dev诊断575（含per-Host／独立outline分支）、shell13／Remix11、UiLogic及准确source/check见[实施检查点](../ui-diagnostics-20261105.md)。A008目标／报告／释放修复已编译，Report连续pending／新原位32-pass组合仍无动态覆盖；作者旧popup无同合同映射、来源列表直接查选／长来源、O5及native／独立1.7缺口保留。更早未变证据只按原输入层复用，以下旧候选不重标新Dev。

## F01 日志出口与失败隔离
**实现：** 2026-10-29已恢复DIAG-02／03。原36个出口沿具名`SqueakLog`事件／取证入口进入受保护格式化与唯一`SqueakLogSink.Emit`；原生Log不再位于业务／UI。当前`kernel.response`使用明确fmt=3，不双发旧裸response／OneShot文本。未改义的fmt=1／2 API仍保原字段，VF1必要store failure按现行DIAG-02更正为Daily warning。r4收取纠正其及其他现行Daily故障在Auto／Disabled下缺必要上下文的遗漏：安全HumanSentence保已捕获身份／阶段、异常类型与清洗原因，不依赖详细suffix；VF1六出口具名stage并参与once键。

**真实链：** Comp先保 `RecordOutcome`／目标录制 ring，再按有效模式和本 Comp 配额请求 owner 已保存事实；格式化／sink 普通失败不越过边界。OneShot 实际收据只由原输出路径决定。保存反馈／publication repair、Sustained End后的剩余清理、UI降级与成本关停不依赖日志成功。`ptrace`／`ltrace` 各 512 不改；回应现沿 DIAG-04 每 Comp 每 60 真实秒 64 次尝试自动续期，不再是永久前缀。

**证据：** r4现有LogTests Release／Dev通过，覆盖保留fmt=1／2字段、Daily必要人读上下文、模式、编码与once；既有Disabled方法实际覆盖PackRejected身份／原因，精确VF1 Disabled异常组合只有同一安全HumanSentence分支的源码依据、未新增case；AudioBinding现有caller使用实际Logging源。显式消费本次FL固定Release后，完整当前Host1747项通过，仍是生产DLL＋平台double层。完整出口增量对账及当前字段在[实现映射](implementation.md#当前日志协议与读取方)。精确throwing native sink、关闭日志时真实Comp内存／原生音频及退出组合未在游戏执行；旧组件绿不代签这些原生故障。

## D06 玩家诊断状态、录制与反馈

**实现：** 已采用 DIAG-04／06／07，并恢复 DIAG-01 初态与只读边。native adapter 在原结算边保存 HasEvaluation／序号；typed 投影不查询 null 动作字典、不重采健康／随机或维护生产 population。原窗口提供普通入口、单目标最近 64 条 ring、可见覆盖／捕获／读取失败、停止／清空及有界反馈复制；回应日志按 60 真实秒续期，其他日志／成本预算不变。未采到的 root、游戏版本等如实标记。US06维护已补原排程采用量和最近持续维护事实，取代本段原先统一的W／槽NotCaptured说明；NoOwner仍NotCaptured，未排程不虚构W。原2026-10-29运行证据保原输入，不由本次字段扩充改签。

**证据：** 独立诊断候选消费既有 FL producer-release-r1；Dev／Release 引用编译、现有 UiLogic、LogTests 两配置、AudioBinding 既有 through／Select 入口、当前 Release Host 的既有 diagnostics／双语 workspace 均按实际层记录在 `dist/diagnostics-tool-20261029/REPORT-B.md`。Host 的 native Comp／录制／clipboard 并非这些 fake-source 用例的自动覆盖。错误地用 Dev Host 要求 Release FL 提供 Dev geometry 的尝试保留为环境不匹配，不放宽断言。

**仍未运行：** 新 ring 满额继续、60 秒续期、真实 Comp 首次评估前读取、精确异常组合及 clipboard／真实字体／完整离图退出链。未新增 case／probe／runner；普通原生人验只需不用 Dev／详细日志，打开、开始、正常复现、停止、复制、关窗重开。技术候选不冒称这些精确路径已动态通过；历史 r4 与性能／十五动作接受不重签。

## F02 VF1音频发布缺失
**实现：** 2026-10-29已接通FA1至5。启动在初始AudioCatalog构造后把Store的唯一native publisher绑定到`SqueakerRuntime.PublishFallbackAudio`。所有Save／Restore／Delete（包括非UI调用同一Store入口）先构造完整候选，独立文件成功后发布Current，再构造完整新AudioCatalog并一次替换Audio；下一次原`PawnResponseAdapter.Tick`捕获该引用，原selector读取新表。普通选包不走此入口。

**结果：** Written为物理提交；native publisher绑定时也表示采用。`WrittenRuntimeNotAdopted`表示文件已提交、音频未采用，UI独立提示保存／删除已成功而运行保last-good，不重写成功事务。未绑定的独立Store仍是原持久化用途，不声明运行采用。成功路径不再reload／restamp已提交文件；单项编辑若delta读取失败则拒绝写入，不以空delta丢掉其他动作。取消与原Race确认不改；首次建表按实际comp准入区分已准入与新table-only重启。

**证据：** 现有ConfigCopy、SettingsMigration、Reset及Core检查通过；已追真实Store→publisher→AudioCatalog→原selector源码。既有入口没有覆盖完整VF1物理提交→新catalog→下一选择以及提交后publisher抛错的连续执行组合；未新增case／probe。用户最短原生步骤是同一已准入Race改最终回退且避免上层包覆盖，等下一合法机会核新映射；新table-only准入另需完整重启。

**未采纳：** 新reapply按钮、持久pending任务和跨窗恢复协议不是本轮实现或前置条件。

## F03 canonical异种被音频目录过滤
**实现：** 2026-10-29解除过滤。`SqueakXenotypeCatalog.CollectCanonical`在装配声音目录前遍历全部当前loaded XenotypeDef，记录准确对象和重名歧义，不读pack target／配置／来源来伪造身份。声音目录故障不抹掉独立事实；canonical采集故障清空半成品并安全日报。原`GameRuntime.ResolveContext`仍先核Biotech和ReferenceEquals／无歧义，再核该Race准确音频、配置或作者来源域，跨Race同名声音包不授权。

**证据：** 现有DomainAdapter九项通过，仍只证明手填snapshot后的授权；生产collector独立于音频目录的循环／失败边及其到consumer由源码闭合。独立旧DefinitionDirectory夹具本轮编译因HEAD已有九处0.7几何／私有ButtonOverride接缝形状失败，未执行运行；CO明确不在本轮迁移，不将其算F03复现或新FL回归。未执行真实RaceA/X仅配置或来源域、再增删RaceB/X包的原生组合；不冒称HAR反射、raw genes或浏览目录授准入。

## RW17 基础兼容候选

1.7.4990 rev1069 的实际源码确认，SubSoundDef 仍在原 LongEvent 完成队列中构造私有 resolvedGrains 并设置 distinctResolvedGrainsCount；原池完整性检查和双层入队顺序继续适用。Waylith 的 AudioClip 已移除 loadState，原直接调用会在 JIT 时失败，经资源可用性缓存变成 Failed，进而使包与最终回退共享的绑定入口拒绝声音。

UsNativeAudioPool 现按实际加载的 API 缓存片段就绪委托。1.6 继续要求 loadState=Loaded；仅对 UnityStubs 的已构造后端 Sound 资源检查非空及 samples／channels／frequency 为正。未知 API 形状仍拒绝。最终 native 池计数、clip／silence 白名单与坏片段隔离保留，不枚举文件、读取 PCM、重新解析 grains 或改变来源／准入／静音／OneShot／Sustained 语义。

CO 代理补齐的 Sustainer／SampleSustainer／SoundInfo 源码与本 worker 对授权目标 DLL 的定点 IL 核对已闭合持续输出合同。PlaySoundOnMapOrCamera 的两个准确 PlayOneShot 调用位于 IL_004a／IL_0057，符合原 transpiler 的双调用核验；SampleSustainer 的 Volume 仍调用 base.Volume 后叠加原生淡出，Update 继承 Sample；SoundInfo 保 Dictionary<string,float> 参数与枚举。当前无需追加输出 patch。模块身份、调用点与取证边界见 `dist/rw17-basic-compat-20261030/evidence-r1/native-audio-contract-r1.json`；这不是实机 Harmony 安装或发声证据。

前轮基础兼容任务已用原固定 FL producer 作为开发输入完成 net472／1.6 引用的 Dev 与 Release 编译，均为零警告／错误；既有 AudioBinding through 入口 67 项通过。当时完整入口停于 S4RuntimeCallerTests 的生命周期日志联合断言，旧人读短语期望的失败日志及旧交付保留原字节；当前窄修复的独立结果见下节。现有 double 只覆盖原加载状态分支，新的 Waylith 资源分支目前为真实源码核对与引用编译证据，没有 native 执行证据。

诊断冻结已关联到 FL 字体环境度量调用缺失的 Font.fontNames，本轮已实际消费获放行的 `ferritelib/dist/rw17-basic-compat-20261030/producer-release-r1/FerriteLib` 固定 Release 输入。新 FL 把 1.6 字体提供者隔离在 NoInlining 私有方法中，UnityStubs 沿既有 null 环境合同撤销可选文本度量复用；普通测量／绘制保持原路。US 没有增加 catch 或旁路，仍以 Private=false 引用唯一 FL 载体。

新输入下的 US Dev／Release 和 Release Host 构建均为零警告／错误；既有 Host 的双语页面、诊断和窗口故障合同分别通过 223／647／13 项，实际加载 FL 与产品编译引用收据一致。About 已声明 1.6／1.7，LoadFolders 显式复用同一 1.6 载荷，玩家／作者／构建说明已同步基础兼容范围。固定输入身份、构建和配对证据位于 `dist/rw17-basic-compat-20261030/evidence-final-r1/`。这些仍为 net472／1.6 引用及平台 double 层，不能写成 1.7 引用编译、实机声音或渲染通过；实机均为 awaiting_human。

## US16／17 AudioBinding与作者日志保真

现行 `audio.sustained.end_uncertain` 具名事件和 OUT-07／08、DIAG-02／03 是校正依据。既有 S5ActualEndFaults 场景仅把末尾联合断言拆开，保两个对象不同、故障／健康对象各 End 一次、两项受控登记释放及报告事实；报告条件改核当前事件和故障主体，要求本次清理恰好报告一次。生产 Stop 仍先脱离持有再尝试 End，普通异常不阻断其他对象释放；未修改音频算法、guard或清理策略。完整既有 Release AudioBinding 入口现通过138项，原失败证据不覆盖。

作者 issue 的原文本继续保留在原目录 owner 中，同处记录稳定身份、关联身份、动作、固定原因和自由详情；native consumer 经原具名门面传递。`authoring.rejected` 将身份和原因分别编码，自由详情继续调用原清洗函数。once 键采用带长度的完整字段，在清洗／256字符截断前区分不同事实；未知来源的公共 issue 文本只作为清洗详情，不推断为可信字段。现有1024项预算、reset、必要故障模式和唯一受保护 sink 均保留。

本轮现有 LogTests Release／Dev 与固定 FL 下产品 Release 编译均通过。已有日志入口覆盖保留协议、模式、编码、清洗与既有 once，但没有直接覆盖新作者字段与碰撞／长详情的全部组合；这些新语义只有生产者→门面→Claim／Failure的源码依据，没有新增case／probe／runner。固定 US producer、源码形式和交接接口位于 `dist/rw16-17-prepublication-20261030/`；下游六包由独立任务消费，本轮不生成六包归档。两版本真实声音、包／最终回退／静音及UI仍按原反馈与人验分项记录，不由这些技术结果扩签。

C1恢复原`public static void AuthoringRejected(string issue)`的独立CLR入口，使已编译消费者仍可解析单string MethodRef；optional参数不作为二进制兼容依据。该入口固定使用UnstructuredIssue，并只把原文本作为清洗详情。结构化重载要求显式传入身份和原因，原五参生产调用继续采用它。仅门面源码发生本轮生产修改，原Claim／Failure、清理算法、guard和作者准入均不变。既有LogTests Release／Dev及实际Release构建重新通过；完整AudioBinding138项沿未变输入复用，不冒称r2重跑。新的固定交接入口为`dist/rw16-17-prepublication-20261030/INTERFACE-r2.md`，r1接口、producer和证据保持原字节。

C2闭合作者字段迁移的信任边界：公共AddPack的原始package／Def输入、待解析或无效动作、缺失fact／action及未绑定sound不因ExactKey／ActionId通过而获信任。它们只进入清洗详情及不输出的带长度去重键，定位采用VoicePack／Action／BehaviorSource等固定标识。原生adapter只从实际loaded Def／mod owner传递诊断身份；已登记动作只沿DefinitionCatalog.KnownAction读取实际ActionPlan.Key，未登记值不进入结构action／target。CLR provider类型及Notification固定source亦保独立来源。

公开五参AuthoringRejected保原CLR签名，但不接受调用方自称可信：其未确认字段移入清洗详情，采用固定AuthoringInput／UnstructuredIssue，且不输出source／action。原单参入口保持C1语义；内部AuthoringRejectedKnown只采用上述已核来源字段。Claim同时区分完整原输入、清洗前详情及known／free来源，保原预算／模式／sink和业务结果，不修改作者语法或增加准入条件。本轮现有Core932项、AudioBinding through67项、日志Release／Dev及实际Release构建通过；完整AudioBinding138项寿命证据沿未变路径复用。精确新隐私反例／字段组合仍为静态调用依据，没有新增case／probe／runner。当前交接为`dist/rw16-17-prepublication-20261030/INTERFACE-r3.md`；r1／r2文件保原件，不能据旧技术结果推荐其缺陷字节。

## 条件风险与证据盲区
US-R1的纯模型高层默认阻断已作源码窄修：Respond与持续Review均用准确主体族构造默认，普通policy不向NonPawn补scope，NonPawn组合不运行Pawn Talking门。原DeclaredDefaults及两类policy的Defaults公开CLR重载保留Pawn语义；BehaviorValues.ApplicableTo、作者／玩家非法字段拒绝及ResponseDecision择一／冷却／结算未放松。现有OneShot入口编译整个生产Core（无平台引用或double），932项通过；AudioBinding原`--refactor-through`入口67项通过，实际生产调用覆盖Pawn普通Select／Eat／Attack、声明通知的原owner与硬冷却、无音／输出拒绝以及配置关闭后的持续授权复核与释放。后者为平台double层，前者的NonPawn走低层DecisionInput／音频域，均不到本次NonPawn RuntimeView／ResponseFrame故障组合，故没有该高层分支的动态前失败／后成功证据，也未新增case填空。A002显式消费新FL release-gate固定Release，产品Release及既有Host页面、编辑／绑定、确认、窗口／overlay失败恢复和Failed页脚检查通过；932／67项沿未变输入复用。固定交付与实际依赖收据见`dist/release-gate-20261101/INTERFACE-r1.md`；上游三个精确故障组合、NonPawn高层和原生验收不由这些Host绿项代签。

FallbackDelta／FallbackProfile的公开字典及DomainPool条目已在构造边界只读包装／复制，保原内容与顺序；本轮未发现生产下转型写入，也未新增反例case。Social GotoAndBeSociallyActive当前Core Def未在4871指定内容中找到，仍为4850继承依据，必须点名保留，不猜名称／读取未授权DLC补齐。

真实Scribe／Config、字体／IME／窗口盖层、原生挂接／Sample／听感及逐包记录未细化的范围保留原证据粒度；未逐项记录不是PASS或FAIL。2026-10-28的RC级总体接受及已接受性能不被这些缺口撤销，也不外推为每条原生结果；不因缺该粒度记录新增本次文档／RC准备前置门或要求重跑十五矩阵。未来真实变更／回归仍保具体影响验证。

旧文档普通policy pending、Attack／MentalBreak仅Dev、旧tick交互、唯一来源自动采用候选与当前已接受合同冲突，已在本次来源覆盖中整合；这是文档状态更正，不是上述代码缺陷修复。

