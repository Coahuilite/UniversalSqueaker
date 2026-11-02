# 原版事实、来源与证据映射

本页是基线3e42的定点来源／实现／证据记录，不重复持有规范。当前范围与合法条件以[GAME](../../openspec/specs/game-facts/spec.md)、[ACT](../../openspec/specs/official-actions/spec.md)为准。4871方法体依据、目标引用、Harmony／平台double及原生层分别记录；这里的历史执行数量不是本次新运行。

## Eat：过程、执行、物品属性与返回结果分别维护

维护者：US 1.6适配维护者。消费者：内置Eat、InjuredEat固定扩展及独立AfterIngestion根；正式字段与入口以[现行Eat合同](../../openspec/specs/official-actions/spec.md#requirement-act-eat-三轴与实际摄入者)为准。

**来源版本：** 继承的既有项目定点源码核对记录为目标1.6.4871、Assembly-CSharp 1.6.9676.17735，Version.txt rev590／算法rev591差异保留。本轮直接读回RimSage 1.6.4850（程序集1.6.9655.19392）的下列方法体／类型头，并按Krafs 1.6.4871 API编译；4850不是4871同版本复核。A002已获指定安装只读许可并实际核到Assembly-CSharp 1.6.9676.17735；RimWorld.VersionControl将build减4805、revision乘2再整除60，得到1.6.4871 rev591。只读定点方法体已核，不运行游戏，也未读取未授权Version.txt来消解继承rev590记法。

| 正式事实／数据 | 原始来源与主体 | 观察和Unknown／依赖 | 生产消费者 |
| --- | --- | --- | --- |
| us.core/IngestionProcess | JobDriver.SetupToils／Cleanup；JobDriver_Ingest自身，FoodFeedPatient.job.targetB.Pawn即Deliveree | 仅支持这两种明确driver；driver、当前Job一致才活跃。自己Ingest另有正向Job事实；feed接缝缺失则未知，不猜喂食者 | Eat固定过程；Whole保持至全过程退出 |
| us.core/IngestionExecution | Toils_Ingest.ChewIngestible(Pawn,float,TargetIndex,TargetIndex)，参数chewer为实际摄入者 | 工厂只挂观察；原init正常返回、driver／Job／CurToil同一才进入，finish关闭；每次真实进入增代。依赖成组Setup/Cleanup及Chew/CurToil；缺Chew不安装半套精确观察 | phase=Execution及固定精确扩展 |
| us.core/IngestibleNutrition | 实际物品ThingDef.IsNutritionGivingIngestible | 物品属性，不是IEatingDriver标记、不是当次营养值；缺实际可摄入物为Unknown。Any不加此依赖 | nutritionScope非Any时 |
| us.core/IngestibleDrug | 同一实际物品ThingDef.IsDrug | 与营养独立；Any不加此依赖。支持的原版准备过程从actor当前Job targetA读取；实际Chew进入后改用工厂捕获的itemIndex（不限A），退出恢复准备A。营养属性用同一物品规则；机器→食物后不沿用机器属性 | drugScope非Any时 |
| us.core/IngestionResultEvent及IngestionResultFact | FinalizeIngest调用Thing.Ingested(Pawn,float)；捕获实际ingester及item，正常返回营养值 | 原版Destroyed／!IngestibleNow的提前返回不构造成功摄入；合法0结果保留。异常不发结果，Job Cleanup也不发结果；事件只存本次快照，结果身份／0值进入ResponseContext和PlaybackRequest | 编译后固定条件显式引用Result事实的合同（含嵌套／继承）；独立AfterIngestion根不要求仍在执行，普通Eat不消费该通知 |

**实际数据流：** FoodFeedPatient.MakeNewToils调用ChewIngestible(Deliveree,1.5,A)，但Toil.actor仍是喂食者；物品从actor的Job取、声音归Deliveree的唯一session。FinalizeIngest读取actor.jobs.curJob实际目标并调用Thing.Ingested；FoodFeedPatient会替换Finalize Toil.finishActions，所以结果不挂在该易被覆盖的finish列表。普通Ingest的Corpse分支可跳回同一chewing Toil；一次chewing退出不等于全过程退出。多个同时有效的feed来源指向同一接收者时，执行／物品保持Unknown，不猜一个。

| 判别输入 | 本轮实际检查及结果边界 |
| --- | --- |
| 准备、工厂已构造未进入、原init抛异常 | Whole可成立；Execution不成立，不以工厂调用造进入 |
| 真实进入→退出→同Job再进入 | 精确True→False→True，代次随真实进入增加；诊断读已捕获值，不重采事实／随机 |
| 营养×药物四种物品组合×各筛选值 | 生产采事实与条件组合相符；两属性不互斥，Any不因属性Unknown失效 |
| dispenser→实际食物 | 缺可摄入物为Unknown；目标替换后读取食物属性 |
| 准备A→实际Chew index B→退出准备A | DbEatTests实际执行非A工厂索引，执行营养／药物随B，退出还原支持的准备A；不是所有阶段硬编码A |
| 喂食者a、接收者b | b拥有过程／执行及其声音，a不被当摄入者；不要求b自己运行Ingest |
| 正常0返回、无效物品0提前返回、摄入异常、中断Cleanup | 仅正常摄入返回产生独立结果；0不统一失败，后三者不伪造结果 |
| 真正无Result合同的最小Eat音频包，过程仍True且状态未到期 | DbResultRoutingTests修前复现0／0.5各多起播一次、改写确认／冷却且冷owner被创建；修后均零额外播放、原机会／尝试／保护／roll／诊断不变，原到期机会仍可用 |
| 低于普通Eat优先级的独立Result根及继承子合同 | 同一owner正确接收0／正结果，实际共享／自身硬截止生效；普通Eat不抢结果机会，硬拒绝不退普通Eat，下一状态机会保留 |
| Whole／Execution持续退出 | 同一生产owner／持续槽；Whole不因一次chewing退出停，Execution按阶段退出End，Cleanup关闭Whole |
| OneShot退出 | 不再新起播；不调用旧OneShot裁尾或停止他方声音 |
| 真正编译移除ChewIngestible | PatchAll完成；Whole仍可用，Execution为Unknown／所选精确贡献暂停，具名缺目标；其他过程／结果／Select能力仍可用 |

实现位置：UsIngestionObservation的短生命周期记录与四个窄Harmony hook；UsPawnFacts一次采样；PawnResponseAdapter当前有效配置／同一仲裁和持续槽。记录在session reset清除，不Scribe、不写save、不造第二时钟／调度器。Any/Whole的参数不会添加未选可选依赖；固定扩展条件始终保留。

**结果路由修订：** Thing.Ingested窄hook继续发布真实正常返回。SqueakerRuntime在没有固定结果消费者时直接结束，不创建session；有消费者时仍用实际摄入者的同一session，只让固定条件树引用结果事实的候选进入本次事件。修前错误是把普通Eat的过程True当成本次结果的消费资格，绕过状态软等待，不是返回0的判据或阶段事实错误。没有修改通用决策／golden、共享保护、Whole/Any/Any或OneShot自然尾。

证据：DbEatTests的真实Harmony＋生产适配／settings解析／session／选音／输出链；DbEatAuthorTests运行公开XML和真实loader；DbEffectiveConfigTests及ModSaveCallerTests运行生产settings/ExposeData/原子文件与Scribe替身往返及失败恢复；DbEatUiTests运行真实Source／Host、平台替身。原版Job/Toil/物品/音频为明确double，尸体是复入机制形状而非真实Corpse运行；4871 SetupToils/Cleanup/CurToil、ChewIngestible/FinalizeIngest、Ingest/FoodFeedPatient.MakeNewToils及Thing.Ingested已定点核对；原生Scribe、真实游戏Config、挂接、屏幕或听感仍未验。改源时同步事实／消费者／反例，不按新方法名猜合同。
## Select：最终反馈主体与已提交选择分别观察

**来源版本：** RimSage返回的 `Source/RimWorld/Selector.cs`，程序集1.6.9655.19392（1.6.4850）。另已直接核对指定安装的1.6.4871 rev591 Selector两方法体；4850保留为继承来源，目标API编译仍分别记录。

`Selector.Select`调用私有`SelectInternal(object,bool,bool)`；代理经`CompSelectProxy`递归到最终对象。所核版本在实际接受分支按`playSound`调用`PlaySelectionSoundFor(obj)`，随后加入选择集合。只看公共方法入参或已有集合无法证明本次反馈接受。

生产观察器在每个SelectInternal调用建立线程局部嵌套上下文，捕获实际反馈调用的最终对象；正常返回后核对同一个Selector的`IsSelected`。只有反馈意图、最终Pawn和提交证据相符才发布`us.core/SelectEvent`。异常经finalizer清理；不知道最终主体时不借别的已选Pawn补足。未观察到本次反馈不发布事件。重复选择是否获得反馈由上游实际接受路径决定，US不另加去重政策。

| 反例／输入 | 消费与证据 |
| --- | --- |
| 原调用被拒、无反馈意图、反馈后提交失败 | DaSelectCallerTests实际Harmony路径证明零错误事件／owner；原版Selector为替身 |
| 代理解析为Ratkin、另有Kiiro包 | 最终Ratkin进入同一session，只选准确Ratkin条目 |
| 上游对已有选择再发反馈 | 无US额外去重；仍按同owner的真实root／适用秒门处理 |
| 普通与InjuredSelect同时成立 | 原生通知发布事实而非强制父ActionId，显式优先级仲裁一次，完整子键进配置／条目／截止／诊断 |

以上定向适配检查已执行，4871方法体已核；真实游戏Harmony挂接、原生音频与听感仍待观察。通用Select不含隐藏Pawn门。官方Pawn三默认已裁为玩家可控／清醒／未倒地各true，三个独立presence-value参数；false仅放宽本项，缺失继承，未要求的可选资格不添加依赖。DcSelectSettingsTests与DcSelectConfigTests验证实际调用、分层／false／缺失、保存重开／失败恢复／准确Reset；整体Dev门保持。

### 缺少目标方法：成对停用Select观察，不中断模组启动

SelectInternal与PlaySelectionSoundFor共同实现一个观察能力。仅发现其中一个不能装半套patch。原实现直接返回AccessTools.Method：在明确缺少反馈方法的编译替身上，实际Harmony2.4.2 PatchAll抛HarmonyException（TargetMethod返回null），且另一端已经挂接。Mod构造未捕获该调用；从调用位置可知该异常会传播，未运行原生Mod构造。

现在两patch共用缓存目标与Prepare：任一目标缺失时两者都跳过，一次记录`capability=SelectObservation`、`us.core/SelectEvent`、`SelectObservationUnavailable:MissingTarget:<完整目标>`。没有全局PatchAll异常吞掉或对其他能力的停用。缺实现时SelectEvent保留Unknown并携带具名缺口，不能被直接通知造为True；显式requires该事实的行为贡献暂停，意图／快照不删除。普通Select、继承子合同和独立引用该事实的根均不能起播，无关合同继续成立。

`DaSelectMissingTargetTests`用编译时真正移除的方法，分别验证缺SelectInternal、缺PlaySelectionSoundFor，各13项通过：PatchAll返回、无半套挂接、单次具名诊断、原版选择保留、直接通知／作者调用无伪造、相关来源暂停及无关Work仍派发。两端存在的原63项另通过。这里使用真实Harmony与生产源、明确Selector／音频double，未据此关闭目标1.6.4871原生证据。

## 当前伤口：不是历史受伤通知

`us.core/HasCurrentInjury`由`UsPawnFacts.Capture`读取该Pawn当前`health.hediffSet.hediffs`：存在`Hediff_Injury`且`Severity>0`为True，包括仍存在的伤疤；无此项为False，缺health或hediffSet为Unknown。消费者为示例`example/InjuredSelect`固定附加条件；它不改普通Select，也不从先前Wounded事件缓存True。

生产事实读取和普通／受伤扩展调用已由DaSelectCallerTests的加伤、移除、缺health序列执行；类型／API经目标1.6.4871引用编译，健康集合为明确平台替身。真实游戏伤口／伤疤样本尚未运行。此判据是示例明示的合同，不给全部“受伤”扩展规定永久统一定义。

## 全15包事实登记：来源、阶段与消费者不混写

维护者均为US 1.6适配维护者；每次源机制／版本变动须更新本表、对应导读和正反fixture。生产消费者是同一个PawnResponseAdapter及其普通／继承／独立固定合同，按完整事实键显式引用。表内“4871”是上述指定安装只读依据；运行证据统一为真实Harmony＋生产源＋明确平台double，不是游戏挂接。共享事实可复用，但本包语义不能从其他包的通过推断。

| 包／稳定事实 | 真实主体与阶段／源码 | False、Unknown、缺实现与覆盖边界 | 实际消费者／检查 |
| --- | --- | --- | --- |
| Select／us.core/SelectEvent | SelectedSubject；4871 Selector.SelectInternal＋PlaySelectionSoundFor，反馈最终对象且正常返回后集合提交 | 无接受／反馈不发布；缺任一目标成对隔离并Unknown。无US独立去重，NonPawn仅纯身份链 | 官方Select及显式扩展；DaSelectCallerTests、DaSelectMissingTargetTests、DcSelectSettingsTests |
| Wounded／us.core/WoundedEvent | DamageRecipient；4871 Pawn.PostApplyDamage正常返回，捕获totalDamageDealt | 0不是正伤害保证；异常不发布。不是当前伤口；缺方法Unknown并只隔离该能力，不扩所有Thing伤害 | Wounded及正向受损根；DcNativeTests正常0／正、throw与编译真缺目标 |
| Death／us.core/DeathEvent | DyingSubject；4871 Pawn.Kill prefix进入，SetDead／DeSpawn在其后 | 不是死亡完成或任意Destroy／消耗；异常不倒推prefix完成。缺方法隔离，不造尸体／凶手主体 | Death；DcNativeTests检查请求在Kill体前，离图清理owner |
| Attack／us.core/AttackEvent | Attacker；4871显式近战／发射／束流喷射／点火／明确轰击根 | 近战成对scope＋实际挥击marker，正常返回可false；早退false无marker不成立。其他根按已核true含义。工具／跳跃／支援／任意生成排除，完整清单见导读；普通支持集，逐项原生未签 | Attack及明确执行根；DcNativeTests包装单通知、false早退／挥击、throw、工具／NonPawn与未激活反例 |
| Move／us.core/MoveState | SelfMover；自己Pawn.pather.Moving，当前状态采样 | pather缺失Unknown；未动False。被搬运位移、世界旅行不算；无按位置差猜测 | Move；DcNativeTests自身移动／被动未动、Sustained失效 |
| Equip／us.core/EquipEvent | EquipmentWearer＋实际eq；4871 tracker.Notify_EquipmentAdded正常返回，eq仍在该equipment列表 | 失败／throw／非成员不发布；非服装、卸下、库存拾取。关系事实与显式PlayerCoreEquip回应资格分开，缺目标隔离 | Equip普通／显式Dev合法资格及独立关系根；DcNativeTests添加／失败／已移除、未选资格、准确主体／物品 |
| Call／us.core/CallState | IdlePawn；4871 Pawn_MindState.IsIdle＋!pather.Moving＋Wait/Wait_Wander准确Job | 缺mindState/pather Unknown；Wait_Combat／其他Job False，不是排除所有别包后的兜底。IsIdle的原版Downed／Spawned条件仅属该事实 | Call；DcNativeTests正向Wait两种及战斗等待反例，S3PawnFactTests保未知Job不落Call |
| Eat／us.core/IngestionProcess | ActualIngester；4871 Ingest自身或FoodFeedPatient.Deliveree，过程／阶段／属性／结果分列上表 | Whole/Any/Any不要求未选可选事实；精确Unknown不宽化。结果事件仅显式Result合同消费，无消费者不建owner | Eat、InjuredEat、AfterIngestion；DbEatTests127、DbResultRoutingTests68及缺Chewfixture |
| Sleep／us.core/SleepState | Sleeper；当前JobDriver.asleep，4871 JobDriver字段／RestUtility相互区分 | driver缺失Unknown、未睡False；姿势／Job名称／!Awake不足为据。不是所有第三方睡眠系统 | Sleep；DcNativeTests清醒／睡眠及被喂重叠正确角色 |
| Social／us.core/SocialState | SocialParticipant；joyKind Social或精确StandAndBeSociallyActive/GotoAndBeSociallyActive/Lovin当前Job全过程 | 未覆盖名称片段不变True，不是互动成功或双人事务；与Joy可同真。Stand/Lovin的4871 Core Def已核，Goto Def仍为4850依据：指定4871 Core Def中未找到，不能称同版本全内容已核 | Social及显式过程扩展；DcNativeTests准确Def、未知片段、Social+Joy。Goto当前内容缺口保留，不自行扩大安装读取 |
| Joy／us.core/JoyState | JoyParticipant；当前Job.def.joyKind非空的正向声明，包含准备过程 | 无Job／无kind False；不是Joy gain或实际游玩Toil。Social kind也为True，未声明kind机制不自动覆盖 | Joy；DcNativeTests重叠与非Social kind实际中选 |
| Work／us.core/WorkState | Worker；4871 Core完整20 WorkType/105具体WorkGiver的返回及帮助分支，78准确Job/driver（73类型），3患者休息入口不作Worker；见[完整清单](../../openspec/specs/game-work/spec.md) | 同Pawn/currentJob/driver且未ended、CurToil有效，包括准备；无Job/已结束/无Toil False。未知/缺实现/主体不符具名Unknown，TakeInventory/VisitSickPawn有准确来源限制；不是工作完成 | Work及继承子同owner；新增71组逐生产捕获/请求/退出及反例，物理缺Sow仅局部Unknown，实际feed主体分离、Move/Social不改仲裁、两形态退出；原七组证据复用 |
| Draft／us.core/DraftEvent | DraftedPawn；4871 Pawn.GetGizmos/draft controller准确gizmo，原toggle正常后false→true | 无变化／异常／setter／加载不发布；缺GetGizmos只隔离该观察。当前值true不等事件 | Draft；DcNativeTests重复获取不重复包装、实际方向／无变化／setter |
| Undraft／us.core/UndraftEvent | UndraftedPawn；同一准确gizmo正常后true→false，独立ActionId | 与Draft共享接缝、不共享动作身份／时钟／音源；当前false不刷事件，缺实现同组隔离 | Undraft；DcNativeTests独立反向请求／条目／角色与无变化反例 |
| MentalBreak／us.core/MentalBreakEvent | BreakSubject；4871基类TryStart成功，以及Catatonic/RunWild/WildDecree直达成功；HumanityBreak调用base，不重复挂包装 | false／throw不发布；基类不代表所有override。Catatonic不是MentalState；WildDecree true不保证Wander状态。任意MentalState／MentalFit不算；逐机制readiness，未列机制须显式扩展 | MentalBreak及明确启动根；DcNativeTests四实际入口、Humanity包装一次、失败／throw、来源诊断，更新后216项fixture |

### 参数／共享事实的准确依赖

- PlayerControlled：Pawn.IsPlayerControlled；ActiveCommand：当前Job.playerForced且玩家可控。状态捕获为所有完整ActionId带真实命令来源，不只识别Work尾名；事件由实际调用来源传入。scope=ActiveCommand是额外回应资格，不修改事实本体。
- Awake：4871 RestUtility.Awake先查health.capacities.CanBeAwake，再在有当前Job/driver时取!driver.asleep，否则true。health缺失Unknown，不把“缺jobs”额外规定为Unknown。NotDowned：health存在时!Pawn.Downed，否则Unknown。只在有效Select参数true时成为所选必需依赖；false／缺省不适用扩展不会借其缺口暂停。
- Responsive保留供已有明确作者扩展使用，不再作为官方Select三项合并开关。HasCurrentInjury为当前Hediff_Injury且Severity>0，含仍存在伤疤；不从Wounded事件取值。
- PlayerEquipCommand：玩家可控且当前准确JobDefOf.Equip/playerForced；仅所选Equip资格候选消费，原tracker关系通知不因该政策被预过滤。
- 年龄／心情数值取当前Pawn life stage与mood need；缺数据按响应的显式required/optional规则，不由声音名字决定，也不把任意MentalState分类Break。未启用响应恒等，不因退休两专属包装删除他包仍需的共同事实。

### 版本与覆盖缺口的实际影响

4871版本及Select、Eat、近战和其他显式攻击根、Wounded/Kill/Equip、gizmo、JobDriver/CurToil、完整Core WorkGiver/Job-driver过程与正向MentalBreak方法体已取得。源码可挂接不等于游戏中实际加载／声播放通过；引用编译和double仍分别列证据。Social的Goto准确Def为跨版本继承依据、当前Core内容未找到是具名缺口，不读未批DLC目录补齐；不据此把所有Goto或名字含Social的Job纳入。

## 退休不删除第三方正向合同

官方清单、metadata、作者目录、设置／Help／Keyed及实际通知移除Crying/Giggling专属能力；不保失效enum槽或永久关掉的旧开关。编译产品metadata核对旧Patch_MentalFit与Notify_MentalFit已不存在，Host／Settings检查实际15库存及持久字段缺失，纯作者fixture仍能注册并派发independent/Crying和independent/Giggling正向独立根。原版婴儿状态／声音未修改，其他合同仍可服务这些Pawn。冻结历史17字符串语料是历史测试输入，不是活跃产品库存。