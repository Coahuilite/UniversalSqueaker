# 首批合同导读：Select、Eat、Attack

修订：2026-10-23，r6。供维护者带用户逐包审阅。D-B基于D-A最终26ce46b继续；下面区分已定目标、当前候选和后续实施。合同人工审阅／成果人工验收均 **pending**。SR 对照版本为0.3.4，tag `v0.3.4-tagfix1`、commit `d06b607c7b1720bb3c32a28db3e57e989a61d434`；复核HEAD `b28f414b5b4cb40e772f29c6d964dec4eb56689e`与该tag的运行源码相同，仅文档不同。可按SR的 `CompSqueaker.IdentityGateAllows/TryTrigger/CurrentMood`、`Kernel/Modulation.ComposeModulation`、`Patches/Patch_Selector_Select.cs`、`Patches/Patch_Verb_Attack.cs` 和 `1.6/Patches/Ratkin_AddSqueakComp.xml`定位下文差异。结论在本文自足说明，不依赖私有工作区或复制SR资源。其余12包仍须相同完整关口，见[验收清单](us-kernel-0.6-acceptance-zh.md)。

## 先读共同规则，再逐包判断

本节是下面每包合同的组成部分。

1. 事实适配先解析“谁发生什么、谁发声”，再以实例＋服务对象族＋准确域进入单 owner。Pawn 按真实 Race／合法 Xenotype；NonPawn 按 Family／Namespace／Target。共享 profile、合同或 AudioId 不合声音池。当前只有 Pawn 完整宿主，NonPawn 验纯链与目录边界。
2. 固定条件 All/Any/Not 三态：仅 True 有资格；必要事实 Unknown 不当 True；实现缺失在注册／所选依赖处隔离。定义不可变，扩展只能增加固定条件；玩家只能覆盖公开参数，不能删固定条件。
3. 每主体一份状态机会；真实事件即到同一 owner。显式优先序选一，平级按完整键序；选后无音、拒绝或概率未中都不改选另一包补播。同 tick 一次新尝试，多个声音包不加机会。父子关系不决定优先／抢占。
4. 节奏：游戏 tick；动作／共用硬截止取较晚，不缩放。状态等待 ceil(W_base/A)，事件概率 min(1,p_base×A)，A>0、1 中性；失败状态重检 R=60。候选数值 W20/H40/I120、p1、N/speed1 是隔离非持久开发输入，不是发行默认。15 包总排序、等待分布／数值、A 界面范围、关键例外、人口／速度、首次唯一作者来源政策由 PM 集中交用户。
5. 行为层目标为包装缺省 → 已选主域作者 → 已选合法子域作者 → 玩家服务对象 profile → 玩家主域 → 玩家合法子域。省略不贡献，false／0 显式；作者整体先于玩家。system 总音量、活动、彩蛋总许可与距离独立，不被子域抵消。D-B已把完整分层与Eat参数接入生产settings／session；三类profile与当前入口见主规格§22。
6. 可用音源：同 Race 的 Xeno 常规 → Race 常规 → 当前精确域 PackFallback → 最终 Race 表；NonPawn 无 Race 后备。Fallback 首层可用，Remix 非空层等权后层内选择，Vanilla 仅最终表，Disabled 零派发。Xeno 不能借 Race PackFallback。先包权重、后包内变体权重；精确年龄存在但不可播不退本包全龄；彩蛋独立许可。保条目、资源 AudioId、实际供音域／层，不能只写 fallback/动作。
7. 局部音高／音量从条目校准和有效响应一次合成；年龄先、心情后、一次 jitter；省略均中性 1，年龄／心情缺省不启用，参与 Apply/Skip 不等于启用。包→本包动作→条目→玩家参与覆盖只解析一次。音量允许0、音高有限正值，非法参数在副作用前拒绝。输出 L×当前 system G(t)，不写共享 Def；共享资源的非 US 实例不受影响。
8. OneShot 自然收尾，不因后续优先动作或状态退出精确裁剪，可与旧声音重叠。Sustained 自有单槽：同键 AlreadyActive 不抽样、不重启、不刷冷却；异键严格更高才可替换，先准备后 End，旧结束与新启动分记，不恢复／补播。活跃槽每60tick复检当前合同和权限，失效／Unknown 结束；离图、销毁、服务关闭释放自有句柄；概率未中／未中选不是依据失效。
9. 四回执：Dispatched 推硬保护并记确认；AlreadyActive 仅维护；Rejected 不加派发硬保护；Indeterminate 保守保护但不记确认，不立即重试。原生 void 返回不代表听见，诊断不重抽。坏更新保有效作者快照；缺 A 不让 B 接管，同身份 A 可重连；不从快照恢复事实真假或句柄。原生加载、听感均待用户。

### 共同公开参数表

| 字段 | 缺省／单位／范围 | 谁能改；当前落点 |
| --- | --- | --- |
| enabled／scope | 包装启用；scope=AnyOccurrence 或显式 ActiveCommand/Disabled；无单位 | 作者贡献与玩家合法层；ActiveCommand 另需实际命令与控制事实。D-A 按完整 ActionId 解析，不拿父尾名 |
| hardIntervalTicks | 包装显式值；缺失用隔离 Dev 候选；≥0 tick | 作者／玩家全部合法层均已持久化；0 不等缺失 |
| talkingEnabled | 通用包缺省不要求 Pawn Talking；Pawn 包按显式资格选择 | 作者／玩家允许层；不得让通用 Select／NonPawn 暗接 Pawn 器官门。其余包默认资格还须逐包审阅 |
| volume／pitch | 局部1；volume≥0、pitch>0，有限且原生float可表示 | 作者与允许玩家层；不当 system 总控 |
| ageEnabled／moodEnabled／response／parameter | 默认关闭；数值倍率无单位，mood阈值0..1；jitter=[1,1] | 已有稀疏行为来源／玩家响应行；逐响应实际依赖，未配置不增加事实要求 |
| playback | OneShot；可选 Sustained | 包装定义者显式指定，不因继承或 WholeProcess 自动变为持续 |
| priority／chance／shared权限 | 独立包装明确声明或中性缺省；chance 0..1、间隔 tick | 定义者；继承固定条件不暗复制路由／政策。官方发行总序与节奏尚待用户 |

## Select：已接受的选择，向最终被选择者回应

**组成合同：** 选择被实际接受 AND 反馈意图允许。通用包本身不要求 Pawn、清醒、未倒地、玩家控制或 Talking；实例、域、定位、服务开关及上述共同硬规则仍须成立。主体角色为 SelectedSubject，输入代理／容器须解析后再核对最终集合。仅有方法调用不足为据；重复选择是否被原版接受并具有反馈须依据原版接缝，不另加去重政策。被拒绝选择不成立；必要证据不足保留 Unknown。

**触发／阶段／结果：** D-A 改为观察 SelectInternal 的本次调用、实际 PlaySelectionSoundFor 的最终对象，并在返回后核对 IsSelected；嵌套代理各自作用域清理，异常不产生已接受事件。不对入参、当前其他被选对象或独立重复状态猜主体。方法体来源为 RimSage 程序集1.6.9655.19392（1.6.4850），精确目标1.6.4871方法体尚未取得，已做目标引用编译，原生适配确认仍pending。SelectEvent 表示已接受选择反馈，不表示 Pawn 执行动作。Select 适用于 NonPawn 的合同／纯身份链，完整 NonPawn 原生宿主首版不实现。

**正反例：** Ratkin p1 被接受时只能从其域选音，Kiiro p2 包不进池；通用合同不因隐藏 Responsive 门拒绝倒地／睡眠主体；官方 Pawn 回应是否显式要求清醒／未倒地／玩家控制，仍是独立的待用户资格政策。未决前不将全放开固化为发行默认。方法被调用却未入选是 False；无法证明最终主体则 Unknown，不拿另一个当前被选 Pawn 代替。Select 的一次事件不能作为攻击者或伤者事实。

**参数／玩家设置：** 消费共同表；固定接受／反馈条件不可删除。玩家 Race 或合法 Xeno 的 Select 配置不改变 InjuredSelect；旧 Global 行仅 Pawn 服务层，不能影响 NonPawn。新面板待设计，当前 actionTuning／行为来源 API 的完整键入口为实际路径。

**音源／调制：** 共同来源规则，Select→AudioId 独立映射。example/InjuredSelect 可直接引用相同 AudioId 的另一条目，其校准／参与与动作冷却独立；父无子映射时不借父音源。通用包不自动启用 Pawn 年龄／心情，但 Pawn 实例显式配置适用响应可用。

**两种形态／恢复：** 默认 OneShot 自然尾；Sustained 只按声明的持续依据维护，瞬时事件不能冒充持续选择状态。缺选择适配隔离该能力；Unknown 不发声；来源／输出故障按共同规则。扩展伤口条件必须读取“当前约定伤口”，不从旧 Wounded 通知推断，缺实现只隔离依赖扩展。

**实际链与证据：** Patch_Selector_Select → Comp.Notify_Select → UsOneShotRuntime → 同一 UsPawnPlaybackSession → 完整动作配置／合同 → 准确域选音 → PlaybackRequest／四回执／诊断。原生通知发布事实，让普通与扩展合同共同仲裁；不强制只运行父动作。DaContractTests.cs覆盖生产目录／核心的独立根、多级图与只读隔离；DaSelectCallerTests.cs连接实际Harmony观察、真实行为来源事务／解析、session、选音和请求后端。原版Selector、Comp平台门、音频及Scribe／保存调度为明确替身；配置往返使用生产settings/ExposeData/原子文件与Scribe替身，原生Scribe／真实游戏Config未验。定向Select模式已运行63项，含代理最终主体、重复反馈无额外去重、失败提交、伤口扩展、合法Xeno及父音源不补子映射；资格未选择模式另53项。精确目标原生未跑。

**实现缺失隔离：** SelectInternal与反馈方法共用成对Prepare；缺任一目标时两端都不挂接，一次具名报告SelectObservationUnavailable及目标签名。没有全局PatchAll吞异常。Select事实缺实现时不成立，普通／继承／独立依赖它的合同保持静默，相关来源贡献暂停；无关Work仍可派发。真正移除两个目标的两种编译替身各13项通过，两目标存在的原63项通过。

**SR 实际差异：** SR patch只看原入参Pawn/playSound，另隐藏要求玩家可控、清醒且未倒地；Select出厂External／18 ticks／Realtime／ignoreGlobalCooldown。它忽略共享检查但进入尝试仍更新共享钟，并跳过周期随机。US改为有证据的接受／最终主体；资格显式且待用户，0.3s与共享例外未自动继承。不能仅复制XML数值宣称等频。

**请用户判断：** 通用“实际接受＋反馈意图”定义已定，不再重批。尚待选择的是官方 Pawn 回应资格：推荐将 SR 的玩家可控、清醒且未倒地保留为显式可配置 Pawn 资格，而不是通用合同隐藏门；另一选择是官方 Pawn 缺省不限这些资格（会扩大敌方／倒地／睡眠回应范围）。PM 应携 SR 实际差异请用户明确裁定；本轮隔离 Dev 输入只验证两种显式组合，不决定发行默认。合同审阅 pending；D-A 成果技术候选待固定，原生／听感 pending。

## Eat：过程、执行、属性、结果分别表达

**组成合同：** Pawn 实际摄入者 AND 选中 phase 的依据 AND 所选 nutritionScope AND 所选 drugScope；Any 对应不施加该筛选。固定过程事实不允许玩家通过参数删除。结果回应 AfterIngestion 用独立结果事件根，不要求已经结束后仍在 chewing。事实依据、方法体与当前差距详见[Eat记录](us-kernel-0.6-fact-adapters-zh.md)。

**主体／范围：** Ingest 自身摄入与 FoodFeedPatient 的 Deliveree；喂食者≠摄入者。允许受支持的饮用／服药，不泛化到治疗。dispenser 的目标必须跟踪到食物；Corpse 同 Job 可复入执行。阶段工厂调用不是阶段进入，Finalize 不是全过程同义词；Thing.Ingested 返回0不统一失败，Job结束不统一成功。

| 专属公开参数 | 已定缺省／单位／范围 | 覆盖／依赖 |
| --- | --- | --- |
| phase | WholeProcess；或 Execution；无单位 | 作者／玩家Pawn／Race／合法Xeno；Execution 才要求执行事实能力，Unknown不回Whole |
| nutritionScope | Any；NutritionGiving／NonNutritionGiving；无单位 | 同层序；按 IsNutritionGivingIngestible，不按实际营养结果。Any不要求属性已知 |
| drugScope | Any；ExcludeDrugs／OnlyDrugs；无单位 | 同层序；按 IsDrug，独立于营养；Any不要求药物事实已知 |

以上就是当前可加载schema，字段与枚举严格区分大小写。普通音频包最少包身份、准确域、Eat→可解析AudioId及基本声明；不用填上述参数、不造“精确Eat”动作、不补适配或重录声音。

**正反例／Unknown：** p1走向饭：Whole可成立，Execution不成立；啤酒营养与药物均True，Any/Any纳入，营养＋排药排除；非营养药品仍有执行阶段。阶段未知不拓宽；仅营养筛选但属性未知则Unknown。中断不能造结果，喂食者a不借b的摄入声音。详细反例及测试状态在共享记录逐项列明。

**机会／竞争／音源／调制：** 使用共同规则及同一 Eat 映射，精细模式不增加机会、不保证短阶段必播；Eat可与Move/Work/Joy重叠，事实不必互斥，由显式仲裁择一。营养／药物不自动决定素材或调制，作者选择声音适用范围；声音内容不是Kernel判据。

**结果通知的消费者：** 普通Eat仍是状态合同，正常0或正营养返回不额外给它一次机会。只有编译后固定条件树显式引用IngestionResultEvent的合同（含嵌套／继承）进入该结果通知的同owner仲裁；独立AfterIngestion即使优先低于普通Eat也可响应，仍受自身及共享硬规则约束，失败不补位普通Eat。无结果消费者时不建owner、不改机会／保护或伪造回应。它修正的是结果接入越界，不改变Whole/Any/Any、阶段或尾音政策。

**退出／清理：** OneShot 正常过程／阶段退出只停新起播，既有自然尾；Sustained Whole按全过程、Execution按实际执行依据失效结束并淡出。阶段复入不能把 Whole一律停掉。Unknown 复检结束自有槽；其余故障／来源恢复按共同规则。

**扩展隔离／设置／实现：** InjuredEat增加当前伤口且使用自己的完整动作配置；AfterIngestion是独立结果根。D-B已移除旧精确／包含药物bool、CurToilString判阶段和Unknown宽化。玩家在已有调音层选择Pawn／Race／合法Xeno后，使用三个独立下拉框；「继承」只清该层该字段。Basics恢复只清Pawn服务级Eat参数，准确域由动作行／层恢复。完整字段、原子事务与可加载例见主规格§22。

**SR 实际差异：** SR缺省父子开关false/false走WholeJob；精确父开子关读GainingNutritionNow，子开且名称尚未确认时会拓宽整Job。US已定phase／营养／药物独立，精确Unknown不拓宽；Whole/Any/Any保持过程资格，不保证相同频率。SR Eat出厂EachTime144ticks但共享216、人口／速度及启动相位都会改变机会，不能把144直接当US节奏。被喂食／dispenser／Corpse已由生产适配与明确平台替身执行，原生仍待核。

**已定与后续验收：** WholeProcess/Any/Any、营养属性与尾音策略已定。D-B定向生产链已运行准备、工厂未进入、异常init、实际进入／退出、营养×药物、0结果、被喂食、目标切换和尸体形状复入；原生方法体复核／听感及成果人工验收仍pending。缺Chew目标的独立编译fixture证明精确暂停、Whole保留，无全局PatchAll吞异常。

## Attack：攻击执行不是命中

**组成合同：** 适用攻击能力 AND 已明确核对的攻击执行事件；主体为真实 caster/attacker，不是被攻击者或当前被选择Pawn。命中、伤害、攻击尝试各有独立事实；普通Attack不声称命中。受害者Wounded是另一主体，不能直接归入攻击者。

**来源／阶段／覆盖：** 当前Patch_Verb_Attack发现TryCastShot返回bool的方法，以类型名排除Ability，并在true时通知Pawn caster。它证明当前支持机制的调用返回，不统一证明命中或覆盖全部攻击／能力；D-C须逐机制维护。默认资格、覆盖新增／收窄若影响体验，由PM提交用户，不将现有名字过滤或Pawn caster限制升级为永久能力合同。

**正反例：** p1/Ratkin发射但未击中：若该机制true证明执行，可有Attack；HitReaction/AfterDamage不能因此成立。p2/Kiiro受损：Wounded属于p2，不借p1的音源／冷却。返回false不制造执行，未核机制Unknown，不以类型名相似放行。反射可发现方法只证明技术可挂接，不证明业务语义。

**固定／可调／设置：** 固定主体／执行证据不可删除；共同表的scope、启用、硬间隔、局部调制逐完整ActionId与合法层解析。ActiveCommand需要真实命令来源，AI攻击执行与是否响应是分开政策。新面板待设计，当前真实通知与settings路径维持单owner；新各机制参数和发行默认尚未冻结。

**机会／竞争／音源：** 真实事件即到同owner，p按min(1,p_base×A)一次映射，硬截止不受A；事件失败不排队。不因目标也受伤而合并两主体。遵守共同来源与条目规则；攻击音频可显式与别的动作引用同AudioId，不继承父音源或混Race池。

**调制／两形态／恢复：** 按共同局部与system规则，Attack事件不自动启用Break调制。默认OneShot自然尾，不承诺下一攻击打断；Sustained需独立当前持续依据，不用瞬时成功事件无限维护。缺所选机制隔离；Unknown不伪装命中；四回执与清理适用共同规则。

**扩展与证据：** HitReaction／AfterDamage须相应结果适配，不能只继承Attack；新增固定条件不污染普通Attack。实际链为Patch_Verb_Attack → Comp.Notify_Attack → runtime/session → 配置/合同/准确选音/请求/回执。现有平台替身说明调用协议；当前源码覆盖差距与目标版本机制核对在D-C扩齐。原生、听感与完整Attack人审均pending。

**SR 实际差异：** SR扫描特定程序集Verb子类无参bool TryCastShot、按名字排除Ability，在true时通知实际Pawn caster；不证明命中或全部攻击。出厂External216ticks／GameTicks，遵守共享216，外部事件跳过周期随机；尝试失败也收钟，与US四回执不同。US须逐机制列执行依据，新增／缩窄覆盖由PM处理。

**已定与待审覆盖：** 普通Attack回应有证据的执行，命中／伤害留给结果扩展。D-C须提供实际机制覆盖清单及SR差异；若新增或收窄回应范围，再由PM请用户选择。推荐保留已证机制、未知不全开，不自动废除AI／资格政策。

## 三包共同的SR体验差异与比较条件

SR CurrentMood把任意InMentalState当Break，并采用心情显式值覆盖年龄；US当前是年龄×心情独立单次乘算，不能称旧心情和新年龄表现等价。精神状态不等于崩溃是已定修正，不重新复制SR错误。SR官方素材为OneShot自然尾，无US持续单槽；其共享SubSoundDef距离写入不移植，US只改自有实例。

Select/Eat/Attack在SR均用各自准确域映射与共同年龄／彩蛋／来源选择，US保准确域及选择语义；音频本身、局部倍率、年龄／彩蛋、距离和配置必须在对比时固定。SR资源不进入US；如要同素材听感对照，由PM提供有权使用的独立测试包。频率对照须实际轨迹，不能把SR旧概率／间隔原值当作US已批准默认。

以上差异分为已定改变（接受身份、精确Unknown、执行≠命中、调制、四结果）、体验保留目标（准确域、自然尾、Whole过程资格）和未决（官方Pawn资格、普通节奏／排序／例外／首次来源）；后两者不因本轮工程自动接受。
