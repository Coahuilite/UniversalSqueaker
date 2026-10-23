# 官方15包合同导读

当前r2实际结构、通知新前提、Config与删除映射见[r2合同](us-kernel-0.6-refactor-zh.md)；本页保留各包原有的产品语义／来源／未决与人工关口。

修订：2026-10-24，r11（r2单语义与当前作者／配置入口）。全部15包逐节可读；实现与证据按下文边界区分。D-C从535d0af接续；下面区分已定目标、当前候选和后续实施。合同人工审阅／成果人工验收均 **pending**。SR 对照版本为0.3.4，tag `v0.3.4-tagfix1`、commit `d06b607c7b1720bb3c32a28db3e57e989a61d434`；复核HEAD `b28f414b5b4cb40e772f29c6d964dec4eb56689e`与该tag的运行源码相同，仅文档不同。可按SR的 `CompSqueaker.IdentityGateAllows/TryTrigger/CurrentMood`、`Kernel/Modulation.ComposeModulation`、`Patches/Patch_Selector_Select.cs`、`Patches/Patch_Verb_Attack.cs` 和 `1.6/Patches/Ratkin_AddSqueakComp.xml`定位下文差异。结论在本文自足说明，不依赖私有工作区或复制SR资源。全部15包均须相同完整关口，见[验收清单](us-kernel-0.6-acceptance-zh.md)。

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

## Select：接受与反馈固定，Pawn资格三项独立可调

**组成合同／主体：** 实际接受选择 AND 本次反馈意图；角色SelectedSubject。入参代理／容器先由Selector解析，观察实际反馈的最终对象，再于正常返回核对IsSelected。仅调用、只入选而无反馈、拒绝、反馈后异常未提交，都不足以发布。通用Select不要求Pawn、清醒、未倒地、玩家控制或Talking；完整NonPawn原生宿主仍不在首版范围。不另加US重复选择去重：4871原体对已选对象不再反馈，若其他实现确实接受重复反馈，US不额外拒绝。

**精确来源／阶段：** 已核安装Assembly-CSharp元数据1.6.9676.17735，RimWorld.VersionControl换算为游戏1.6.4871 rev591。`RimWorld.Selector.SelectInternal(object,bool,bool)`递归处理CompSelectProxy；`PlaySelectionSoundFor(object)`在集合提交前执行，postfix确认最终集合才通知。两接缝共用Prepare；缺任一目标不安装半个观察器，具名报告SelectObservationUnavailable，仅隔离依赖Select的合同。异常作用域清理，不借其他已选Pawn伪造主体。

| 官方Pawn公开资格 | 包装缺省 | false／缺失的意义 |
| --- | --- | --- |
| selectRequirePlayerControlled | true | false只取消玩家控制要求；缺失继承 |
| selectRequireAwake | true | false只取消清醒要求；缺失继承。Awake按RestUtility.Awake：可清醒能力及当前driver.asleep，不用躺床姿势代替 |
| selectRequireNotDowned | true | false只取消未倒地要求；缺失继承。health缺失为Unknown |

**已裁设置／有效配置：** 用户已定默认三真，三项独立presence/value，按作者主域→作者合法子域→玩家Pawn服务→Race→合法Xeno覆盖包装缺省。false不连带取消其他两项，未要求的资格不增加可选事实实现依赖。现有调音层三个下拉各有继承／要求／不要求，Help解释固定证据；生产settings/ExposeData及原子文件保存，Basics只清Pawn服务字段，准确动作行／层Reset清对应Race/Xeno字段，作者快照保留。临时Unselected选择枚举已删除，整体Dev激活门仍在。任何设置都不能删接受、反馈或最终主体。

**音源、机会与调制：** 每次真正接受反馈到该主体同owner，使用完整`us.core/Select`配置、自己的硬保护和准确域Select条目；高优先扩展中选时不借父音源补位。Ratkin p1不进Kiiro p2池，合法Xeno按resolver准入，不直接拿原始genes扩域。Select免器官／Talking固定门，显式Pawn年龄／心情响应可选；A只映射事件p一次，其余硬规则／四结果见共同条款，不继承SR0.3s为发行默认。

**形态、扩展与恢复：** OneShot自然尾，取消选择不裁尾。瞬时接受事件不是“仍被选择”的持续事实；Sustained下次复检无有效依据即结束，持续选择须独立状态合同。InjuredSelect固定继承接受／反馈再加当前伤口；完整子键的参数、优先、冷却与音频独立，不暗继承父三资格，扩展可在自身行为贡献中显式选择它们。缺观察隔离普通／继承／独立依赖者，缺当前伤口只影响该扩展；来源恢复不重放选择。

**SR实际差异：** SR只看原入参Pawn/playSound，再隐藏要求玩家可控／清醒／未倒地；External18 Realtime ticks、ignoreGlobal，但尝试仍更新共享钟，跳过周期p。US保用户已裁的三项资格为可配置默认，修正接受／反馈／最终主体观察；计时、四结果与统一owner不按旧数值宣称等频。SR心情覆盖年龄，新响应是独立年龄×心情，见共同差异。

**实际证据／人审：** `Patch_Selector_Select→Notify_Select→PawnResponseAdapter→配置／事实／选音→PlaybackRequest／回执`。DcSelectSettingsTests目标模式76项覆盖三真、逐项false与其他资格、Unknown／缺可选实现、作者／玩家／合法Xeno优先、固定拒绝／无反馈不被放宽、同owner准确条目；DaSelectCallerTests迁为显式玩家放宽后65项保代理、重复反馈、伤口扩展及子缺声；生产settings/ExposeData/原子文件与Scribe替身往返覆盖保存重开、失败恢复及准确Reset。成对缺目标历史两种各13项在原输入下成立，当前受改检查见验收页。原生挂接／Config／屏幕／听感及本包两项人工关口仍pending。默认已定，不再请用户重批；需审实际资格调整体验和完整成果。
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

## Attack：显式执行机制候选，执行不等命中

**组成合同／主体：** 受支持的实际攻击执行事件，角色Attacker，取真实Pawn caster；受害者的Wounded另属另一owner、Race和保护。选择目标、尝试、命中及伤害分别需要自己的事实；本合同不从声音名或方法名推断结果。当前完整宿主仅Pawn，不借炮塔操作者或选中Pawn替代真实caster。

**4871已核开发支持集：** 下表是隔离Dev候选，不是获批普通覆盖。旧扫描已移除，不保名字扫描双轨；整体Dev未激活时实际callback不创建owner／输出。

| 机制／实际接缝 | 执行证据与返回意义 |
| --- | --- |
| RimWorld.Verb_MeleeAttack.TryCastShot ＋ Pawn_DrawTracker.Notify_MeleeAttackOn | 成对scope内观察真实挥击marker，正常返回后发布；命中／闪避／落空可返回不同bool。未Spawned、FullBodyBusy早退没有marker，false不统一算挥击；异常不发布 |
| Verse.Verb_LaunchProjectile | true表示本链实际成功发射，不保证命中；Shoot、AbilityShoot、ShootOneUse及StaticOneUse经实际base链覆盖，只挂根以免重复 |
| Verse.Verb_ShootBeam | true对应已核束流执行链；ArcSprayIncinerator实际继承此链，不靠类型名归类 |
| Verse.Verb_Spray／Verb_SpewFire | 已核喷射／喷火实现正常返回true；不声称所有生成物都是攻击 |
| RimWorld.Verb_Ignite | 已核点火执行成功true，非任意火焰相关Verb |
| RimWorld.Verb_PowerBeam／Verb_Bombardment | true对应明确轰击对象启动，不是后续落点命中或伤害完成 |

**排除／待扩展：** BeatFire灭火、Jump／CastAbilityJump、通用CastAbility／CastTargetEffect、Broadshield／Tox／Deadlife部署、Firefoam／Smoke、任意Spawn、MechCluster均不凭旧扫描纳入。任意自定义override不调用已核根也未覆盖。相对本推荐支持集，旧过滤漏掉AbilityShoot实际投射链和近战已挥击但返回false，又多含工具／移动／支援生成；新清单根据主体和执行机制提出修正，正式范围pending，推荐采用上表并为其余机制另设明确合同。

**可调、来源与机会：** 固定执行及真实攻击者不能删除。完整`us.core/Attack`的enabled、scope、硬tick、局部倍率、响应按共同合法层序；ActiveCommand仅回应有正向受控命令来源者，不把AI执行事实改成false。事件A只映射当次p，动作／共享硬截止、同tick及选后不补位生效。只用攻击者准确域Attack条目与已声明后备；与受害者直接复用AudioId不共享配置或时钟。

**调制、形态、恢复与扩展：** 一次局部年龄×心情／jitter与实时G照共同规则，不因攻击自动套Break。OneShot自然尾，不承诺下一击剪尾；瞬时执行不提供持续攻击依据，Sustained下次合同失效复检结束。命中反应／伤害结果须独立结果事实，不能只继承Attack便取得命中保证。近战两目标成对readiness，其他机制逐目标隔离，缺一个不吞全局PatchAll异常；未核机制不猜True，来源重连不重放旧击。

**SR实际与验证：** SR按Verb子类的无参bool TryCastShot扫描、名字排除Ability、true取Pawn caster；External216 GameTicks、共享216，外部跳过周期p，无声／失败尝试也收钟。US修上述误含／漏项、保准确攻击者域和自然尾，四结果及节奏另有合同。实际`Patch_Verb_Attack→Notify_Attack→同session→准确条目／请求`由DcNativeTests覆盖近战早退／挥击false／异常、投射包装只通知一次、支持机制、工具／NonPawn排除、未激活门与攻击者／受害者分离；不是游戏命中或听感测量。4871接缝已定点读回，普通范围、完整合同审阅和成果人验仍pending。
## 三包共同的SR体验差异与比较条件

SR CurrentMood把任意InMentalState当Break，并采用心情显式值覆盖年龄；US当前是年龄×心情独立单次乘算，不能称旧心情和新年龄表现等价。精神状态不等于崩溃是已定修正，不重新复制SR错误。SR官方素材为OneShot自然尾，无US持续单槽；其共享SubSoundDef距离写入不移植，US只改自有实例。

Select/Eat/Attack在SR均用各自准确域映射与共同年龄／彩蛋／来源选择，US保准确域及选择语义；音频本身、局部倍率、年龄／彩蛋、距离和配置必须在对比时固定。SR资源不进入US；如要同素材听感对照，由PM提供有权使用的独立测试包。频率对照须实际轨迹，不能把SR旧概率／间隔原值当作US已批准默认。

以上差异分为已定改变（接受身份、精确Unknown、执行≠命中、调制、四结果）、体验保留目标（准确域、自然尾、Whole过程资格）和未决（除已裁Select三默认之外的资格、普通节奏／排序／例外／首次来源）；后两者不因本轮工程自动接受。

## Wounded：受损处理返回，不是当前伤口

**合同／主体／阶段：** `us.core/WoundedEvent`表示实际受损Pawn的`PostApplyDamage(DamageInfo,float)`正常返回；不是攻击者、当前选中者或仍有伤口。4871方法体先处理记录／血迹／health，已死分支也可正常返回。0值通知不因名字被宣称为正伤害，异常不造正常返回；当前伤口另用HasCurrentInjury。本能力包目前支持Pawn该方法，不承诺任意Thing.TakeDamage／建筑受损。

**固定、资格与设置：** 固定受损者和返回阶段不可被玩家改写。共同表全部适用：完整键`us.core/Wounded`的enabled、scope、硬tick、局部音量／音高及响应由作者先、玩家profile→Race→合法Xeno覆盖；无专属正伤害阈值参数。开发候选声明需要器官、Talking可依显式配置，未添加玩家控制／清醒门；这不是发行资格已接受。推荐保受损者0值通知原义，要“正伤害”则另建明确有数值证据的根／细化合同，不把Wounded悄悄改成它。

**机会、竞争与声音：** 每次真实返回给同一受损者owner事件机会，A调当次p，不创造重试，硬保护及同tick照常；高频连伤不承诺每击必叫。与攻击者Attack分别结算，致死调用中Death可能先到，排序不追溯已发生调用。只选受损者准确域Wounded条目／合法后备，保实际包、条目、供音域；声音与Select可显式复用AudioId，不能把受伤者的声音借给攻击者。

**调制、两形态、故障：** 年龄×心情／一次jitter和实时G照共同规则；被伤不自动启用Break。OneShot不在伤口消失时裁尾；把瞬时事件定义为Sustained时，下次当前合同复检不再有事件依据便结束，不等于“伤口存在期间持续”。持续疼痛扩展须明示当前伤口合同。缺观察实现隔离依赖，运行Unknown不冒充返回；缺声／拒绝不补播，来源A恢复只恢复配置不重放旧伤害。

**SR实际及体验差异：** SR同方法postfix直接取受损者，不查实伤值；External216、共享216，外部不抽周期p，不走玩家控制门但有器官／Talking。无音／门拒也写旧时钟；US四结果与事件A机制已定不同，不能拿216宣称等频。SR任意mental state可给此音套Break，US不继承该误分类；原版伤害／受伤声音不被US替换。原调用见`Patch_Pawn_PostApplyDamage→Notify_Wounded→同session`；本包测试及精确版本覆盖见事实页，原生及听感pending。

**请用户判断：** 推荐保“受损处理返回”而非暗加正伤害筛选，审连伤被硬门挡住及与Death同序列的体验；发行资格／节奏由集中政策表裁定。合同人审／成果人验均pending。

## Death：进入Kill的临终反馈，不是死亡完成证明

**合同／角色／覆盖：** `us.core/DeathEvent`当前是Pawn.Kill进入前通知，主体为将进入Kill的Pawn，非杀手或尸体。4871体内SetDead、DeSpawnOrDeselect及尸体创建都在其后，且方法会捕获内部异常；prefix不能证明这些已完成。支持进入该Kill路径，不纳入任意Destroy、物品消耗／SelfConsume、丢弃装备；能力分类不等于已有NonPawn宿主。

**固定、参数与候选资格：** 固定Kill进入阶段、Pawn身份与实际位置不可被参数替换。`us.core/Death`共同enabled／scope、硬tick、局部倍率及响应按同层序解析，无“死亡完成”或“无条件必播”开关。开发候选保需要器官、免Talking，与SR分清这两种门；不暗加清醒／玩家控制。推荐保临终即时反馈、用OneShot；器官门及关键事件节奏例外仍给用户明确裁定，不把I=0当无所有保护。

**机会、声音与重叠：** prefix到同一owner事件机会，使用当时准确Race／合法Xeno、Death映射与合法后备，带完整身份及来源。可与Wounded的旧尾并存，不挪到凶手域、不选尸体新域。事件A、同tick与四结果照共同规则；Death是否豁免某项硬门／p不是提高优先级就能隐含实现。来源／素材缺失不借别种声音，也不因“死亡重要”强行补播。

**调制、退出与恢复：** 局部年龄／心情及G正常作用；不以死亡事件强制Break。OneShot在仍有位置时发起，已播自然尾；Kill随后离图／销毁释放这个owner的自有持续槽，因此Sustained不承诺死后循环或等待尸体复活，不能结束其他模组音频。异常不会把prefix改成完成回执；来源恢复不重放旧Kill。缺实现只隔离该观察，实际不在场／未服务的Pawn不虚构位置。

**SR实际差异及调用：** SR也是Kill prefix，External0、ignoreGlobal但尝试仍写共享钟，免Talking仍查器官；准确主体Death音源和OneShot自然尾。US保临终阶段／准确域，四结果保护与未定关键例外不按SR旧值直接复制。实际链`Patch_Pawn_Kill→Notify_Death→session`，后续DeSpawn／PostDestroy释放owner；方法体、替身和原生分别列事实页。原生临终位置、尾音、Kill后清理及听感仍pending。

**请用户判断：** 推荐把官方Death明确命名为临终Kill进入反馈；若要“已死亡完成”须新结果合同，不改本事实含义。需审与致死Wounded先后、器官拒绝、OneShot尾音；合同／成果均pending。

## Move：自身路径移动，不是被搬运的位移

**合同与事实：** `us.core/MoveState`读取服务Pawn自己的pather.Moving；无pather为Unknown，有且未移动为False。不是位置每帧变化、被搬运、车辆载客或世界旅行；当前原生范围只有受服务地图Pawn。自身移动和是否玩家命令分开，停止移动不制造一个“停止事件”。

**固定、参数、设置：** 固定自身pathing不可被scope伪造。共同enabled、scope、硬tick、局部倍率、响应按完整Move键和作者／玩家合法层；没有速度→音量或脚步替换专属参数。开发候选需要器官，未暗加只限玩家／清醒；ActiveCommand若显式选用仍要求真实受控命令。推荐默认候选允许AI／玩家的自身移动，正式资格仍待用户，不把移动事实改成玩家命令事实。

**机会、竞争、音源：** 只用已有状态机会，移动更快／多个声音包不会增加调度；A改下一软等待，硬截止不缩放。可与Eat／Work／Joy同时True，按明确候选总序选一，不因Move没音退Work。音源为移动者准确域Move条目及合法层；共享Pawn profile不混其他Race，显式AudioId复用不继承父动作设置或后备。

**调制、退出、失败：** 本次局部年龄×心情／一次jitter，G在自有实例实时作用。OneShot停止新起播后自然尾；Sustained在原60tick复检中自身移动失效／Unknown时End，离图／销毁即走已有释放，不承诺零tick无缝脚步系统。状态机会失败按R60推进，不补播。缺pather的本次Unknown与所选必需实现缺失分开；来源恢复不恢复“上次移动True”。

**SR实际与当前链：** SR同pather判据，Sleep/Eat/Social/Joy先于Move，Move先于Work；Random504／p.012及共享216，周期相位、人口、速度、Talking均会改变实际密度。US保自身移动与准确域／自然尾；统一状态等待和总序另候选，不承诺旧504等频。`CompTick→UsPawnFacts.Capture→同session→Move条目／请求／输出`是本包实际链；纯核心／平台替身／原生证据见事实页，实际搬运反例及听感待相应验收。

**请用户判断：** 推荐保自身移动资格，审与移动中工作／摄入重叠时选哪包及停止后的旧尾；总序／频率集中裁定，合同／成果均pending。

## Equip：装备加入通知，装备者与物品不换位

**合同／主体／范围：** 观察`Pawn_EquipmentTracker.Notify_EquipmentAdded(ThingWithComps)`正常返回，主体为tracker.pawn装备者，eq为加入的装备；方法给装备verb设置caster、通知PickedUp和Equipped，非地上物品自己发声。此来源不是装备尝试、库存拾取、服装穿戴、卸下或所有关系变化；这些要有各自证据，不能统称已装备。当前只支持Pawn设备tracker，不宣称所有可装备对象宿主。

**事实与回应资格分开：** 旧Core Equip玩家Job限制是回应政策，不是tracker关系定义。当前适配观察正常返回且物品确在equipment集合；官方Equip另选显式PlayerCoreEquip开发资格：playerForced＋准确JobDefOf.Equip＋玩家控制，未选择资格则具名拒绝，不因范围未定自动开放AI／加载回应。独立根可明确消费关系事实。推荐先保用户熟悉的玩家Core换装回应，但把关系事实与资格分开供独立根／扩展使用；扩大到自动换装须由PM／用户决定。

GLOBAL Dev菜单已显式传此待裁资格，标签注明`Equip=PlayerCoreEquip (pending)`；完整入口与同参数示例见[规格§20](us-kernel-0.6-spec-zh.md)。七参数构造器仍为Unselected，普通激活门不变。实际菜单回调到policy、真实EquipmentAdded经Harmony到同owner有独立定向fixture；菜单登记／原生物体／音频为明确平台替身，未据此签游戏菜单操作通过。

**可调、有效配置、来源：** 共同enabled／scope、硬tick、局部音量／音高、响应按完整`us.core/Equip`及合法层解析；没有通过scope=Any偷删固定来源证据。来源A快照保作者值，缺A不选B；关系物品不进配置。玩家在既有动作行改本包与层级，真实保存／恢复走同生产settings；不新增面板结构或自动加载回应默认。

**机会、重叠及供音：** 真实加入且所选显式资格满足后到装备者同owner，事件p／同tick／硬保护照共同规则；与Draft、Select或Work同批不额外多选，选后缺声不补其他动作。只用装备者准确Race／合法Xeno的Equip条目和合法后备，不用eq.Def当Race或发到物品位置。复用Attack同AudioId可以显式声明，不合并两个动作冷却或配置。

**调制、两形态、故障：** 局部年龄／心情／jitter及G照共同规则；穿上某物不会自动启用某种情绪响应。OneShot自然尾，不因马上卸下精准裁剪；瞬时加入事件本身不构成“持续持有装备”，Sustained到下次事件条件失效复检结束，持有期间持续须另有关系状态合同。原方法抛异常不发布正常返回；缺观察实现隔离该能力，未知关系不猜成功。来源恢复不重放旧装备加入。

**SR实际对照：** SR同postfix取tracker.pawn，Notify_Equip强制玩家Core Equip job；ActiveCommand／玩家控制、器官／Talking和地图视野门。External216／共享216，外部不抽周期p、尝试仍消耗旧钟；US四结果与A机制有意不同，不把216当实频。准确装备者域和自然尾保留目标。实际链`Patch_Pawn_EquipmentAdded→Notify_Equip→session`；真实原生加载／换装／声音及新资格候选的成果仍pending。

**请用户判断：** 推荐先以显式候选维持玩家Core Equip体验；是否把AI、系统或加载加入也作为普通回应，以及服装／卸下是否另供合同，均不能从本hook自动获准。合同人审／成果人验各pending。

## Call：正向闲置并原地等待，不做兜底叫声

**组成合同／主体／范围：** `us.core/CallState`要求本Pawn的mindState.IsIdle、自己的pather未移动、准确JobDef为Wait或Wait_Wander。4871 IsIdle本身拒绝倒地／未Spawned，再读lastJobTag==Idle；这是闲置事实定义，不推广成所有包的隐藏资格。Wait_Combat、未知Job、仅无其他动作或缺mindState/pather都不冒充True；缺依赖为Unknown。角色IdlePawn，非地图中所有听众，不提供NonPawn闲置宿主。

**固定／可调与来源：** 正向闲置／原地等待不可删。`us.core/Call`共同enabled、scope、硬tick、局部倍率及响应按作者→玩家服务／Race／合法Xeno；无“无别包就叫”参数。开发候选需要器官／Talking，默认AnyOccurrence，不额外限玩家命令；若选择ActiveCommand仍须真实命令资格。玩家可独立关Call或恢复本层，不关闭未知Job本身。来源A缺失照快照恢复，不让B接管。

**机会／声音／调制：** 同owner状态机会，不因Call处于列表尾而额外检查；A改变软等待，失败R60，硬保护独立。和其他状态同时True由候选总序仲裁，选后无声不补位。准确闲置Pawn域Call条目／声明后备，音频包数量不加机会。局部年龄×心情／一次jitter与实时G适用，不把闲置推成特殊mood。

**形态／退出／扩展：** OneShot转去工作或移动后自然尾；Sustained在原60tick维护时闲置依据失效／Unknown结束，离图释放，不承诺瞬时停止。扩展可继承正向闲置再增加固定条件，参数／音频／冷却按自己的完整键；自定义闲置根须提供明确正向事实，不借Call名自动纳入。缺声／拒绝／输出不确定按共同四结果，不缓存上次闲置True恢复。

**SR实际差异及证据：** SR排除Sleep/Eat/Social/Joy/Move/Work后无条件Call，未知Job也会落入；Random864、p.012、共享216、启动相位／人口／速度／Talking都影响密度。US有意收紧为正向闲置，不回退消极兜底。实际`CompTick→UsPawnFacts→同session→Call请求`；DcNativeTests正向Wait/Wait_Wander、Wait_Combat反例与准确角色进入输出，4871 Wait Def和IsIdle方法体为来源。原生／听感及合同／成果人工关口pending；推荐保已定正向定义，只将新的回应密度交集中政策审阅。

## Sleep：实际driver.asleep，不是躺床或无法清醒

**组成合同／角色／阶段：** `us.core/SleepState`取本Pawn当前driver.asleep，角色Sleeper；driver缺失Unknown，存在但未睡False。床上清醒、持有睡觉Job、!Awake（还含无法清醒）均不替代该事实；事实本身不要求床，不宣称覆盖所有第三方不写asleep的睡眠机制。目标是实际正在睡，不是睡眠完成结果。

**参数／资格／配置：** `us.core/Sleep`共同enabled、scope、硬tick、局部倍率和响应按同合法层序，无床姿势／休息need资格参数。开发候选需要器官／Talking，AnyOccurrence，不暗加清醒门，否则与睡眠合同矛盾。玩家设置是回应资格／声音选择，不把原版awake改为睡眠；单包Reset保其他动作及作者来源。

**机会／音源／调制：** 已有状态机会和显式总序，不因睡眠更长加独立轮询；被喂食时Sleep和Eat可同时True，所选Sleep请求角色仍Sleeper，不误标ActualIngester。精确睡眠者域Sleep条目，可显式复用AudioId但不借Eat条目；一次局部年龄×心情与G照共同规则，睡着不自动等于某个mood。

**两形态／故障／继承：** OneShot醒来后自然尾；Sustained当前asleep变False／Unknown在维护期结束、离图释放。失败不会补低优先动作或恢复上次睡眠True。继承可加地点等正向固定条件，但自己声明声音／参数／优先；没有“父包会替子提供鼾声”。缺driver的本次Unknown与缺必需实现分开诊断。

**SR实际与验证：** SR用LayingInBed且rest need非空，可能把醒着休息算Sleep；Sleep先于所有周期包，Random1080/p.008、共享216及人口／速度／Talking。US修正为asleep，不按1080保证等频。实际`CompTick→UsPawnFacts→session→Sleep输出`，DcNativeTests覆盖asleep、清醒、被喂睡眠重叠及真实角色；源为4871 JobDriver/RestUtility，平台明确double。推荐保实际睡眠与自然尾，具体床／地面场景和密度供用户验；合同／成果／原生听感均pending。

## Social：明确社交过程，双方不共享发声owner

**固定事实／参与者：** `us.core/SocialState`当前支持joyKind==Social，或准确JobDef StandAndBeSociallyActive、GotoAndBeSociallyActive、Lovin；Stand/Lovin的4871 Core Def与当前driver依据已核；GotoAndBeSociallyActive的Def依据仍为4850记录，4871 Core Def检索未找到该条，保留此具体内容版本缺口。这是当前Job声明的整个社交过程，包括走向社交点／准备，不等于已说一句话、成功互动或关系改变。角色SocialParticipant，只给当前这个当事Pawn；没有自动替对方也建一次回应。名字包含Chat/Visit/Entertain的其他Job不自动承认。

**参数／覆盖候选：** 共同`us.core/Social`字段及合法来源层适用，Dev候选AnyOccurrence、器官／Talking，无隐藏玩家命令或仅友方限制；暂无参与者选择、互动成功或具体阶段专属开关。joyKind Social是内容的正向语义声明，不能由音频名称补证。上述准确支持集是提交审阅的开发范围；未知机制要扩事实和证据，不用名称片段自动拓宽。

**机会／重叠／音源：** 只消费同owner状态机会。Social和Joy可同时True，由候选优先仲裁，不故意把Joy事实改False；Social没声不临时补Joy。音源属本参与者准确域Social条目，另一参与者若实际有自己的机会独立结算；共享profile／同AudioId不混域。局部年龄×心情／jitter和G不从对方心情取值。

**形态／退出／恢复与扩展：** OneShot社交结束自然尾；Sustained在当前支持过程失效／Unknown复检结束，不承诺按对话句子同步。独立互动结果根须真实结果事实；继承Social只增加固定条件，不继承双方同步、父音源或参数。缺依赖只暂停使用者，来源恢复不恢复上次聊天状态；四结果／硬保护照共同规则。

**SR实际与验收：** SR用Chat/Social/Visit/Lovin/Entertain片段（不分大小写），可能误含／漏掉第三方；Social先Joy，Random504/p.016、共享216及周期相位／人口／速度。US换明确过程定义且保重叠交同owner处理，范围收紧／扩展需用户审，不以“架构必然”解释少响。DcNativeTests覆盖准确Def、未知名称反例、joyKind Social同时Joy及准确参与者输出；原生各社交场景、完整合同／成果人验pending。推荐先用已核过程清单，另列新互动而不恢复片段扫描。

## Joy：娱乐Job过程，不伪称已经获得娱乐值

**合同／角色／范围：** `us.core/JoyState`当前正向依据是CurJob.def.joyKind非空；角色JoyParticipant。支持声明娱乐kind的当前Job整个过程，包括准备／走向目标，不等于某种娱乐执行Toil或实际Joy gain；没有Job或未声明为False，不从“非工作”推导。Social kind同时属于Joy，当前不互斥。未声明kind的第三方娱乐机制需独立适配，不声称全娱乐覆盖。

**参数／资格与有效配置：** 完整Joy键消费共同enabled、scope、硬tick、局部倍率／响应，作者和玩家合法层各字段稀疏覆盖。开发候选AnyOccurrence、器官／Talking，无清醒／玩家控制隐藏门，无按joyKind自动换素材、进度阈值或成功结果参数。服务关闭或精确禁用只停止新的本包回应及自有持续槽；Reset不删作者来源。

**机会／音源／调制：** 使用已有状态机会、A软等待与硬截止，不能因Social重叠再多抽一次。候选排序先Social时只取其一；正式顺序未裁。音源是娱乐者准确域Joy条目及声明后备，不是娱乐设施的域；复用Social AudioId仍是不同条目／配置。局部年龄×心情／jitter一次，G实时作用，不拿娱乐声音证明心情高涨。

**形态／退出／扩展／失败：** OneShot任务中断自然尾；Sustained到当前娱乐过程不成立／Unknown复检结束并释放自有槽。若要“实际正在玩”或“获得娱乐值”须新增明确阶段／结果事实，继承Joy不自动提供。未知适配不当True，缺声不回Call；作者来源归来不重放旧娱乐或补播。

**SR实际及证据：** SR同joyKind非空，Social优先Joy、Joy优先Move/Work/Call；Random504/p.016、共享216、周期相位／人口／速度／Talking适用。US保正向声明及准确音源／自然尾，新增统一机会和四结果不同，不保证旧频率。DcNativeTests覆盖Social+Joy同真、改非Social joyKind后Joy中选及准确角色，4871 JobDef/调用依据见事实页。推荐先明确全过程，不用执行措辞暗承诺；完整合同／成果、原生和听感pending。

## Work：完整Core常用清单，工作事实与回应资格分离

**范围／主体：** 用户已批准补齐1.6 Core常用工作，原七过程不再是全集。指定4871 Core的20 WorkType、105具体WorkGiver逐行盘点，102个做工入口的实际返回／帮助分支落到78组准确Job/driver，另3个病人休息入口排除。[完整Core清单](us-core-work-scope-zh.md)自足列明种植／收获／砍伐、医疗／救援／喂食／手术、驯兽／训练／动物照护、施工清障／搬运／制作／资源采集／研究等每入口、分支、driver及条件。角色Worker是执行Pawn，不是患者、动物、物品或被搬运者；自我治疗的执行者仍可工作。普通作者继续映射同一个us.core/Work音频，不需每Job声音或临时完整版开关。

**固定过程／阶段：** 当前driver须属同Pawn、同一currentJob、准确Job/driver且当前Toil有效；包括准备、移动、取材料和执行，非挥工具／产出／完成保证。原Pawn_JobTracker先置ended再Cleanup/ClearDriver，退出窗口即使保旧Toil也不是真。TakeInventory须准确Train来源，VisitSickPawn须准确同名WorkGiver来源；没有泛用Goto/Wait、workGiver非空、名字片段或仅反射存在即True的兜底。具体Core中的DLC条件和地图出行准备按完整表说明，不外推未查DLC／世界任务／第三方库存。

**False／Unknown与资格：** 无Job、已结束或已核过程未到当前Toil为False；未知Job／缺driver实现／当前driver或主体不匹配为具名Unknown，未覆盖工作不因此算Call。患者LayDown三入口不当Worker，未审的其他LayDown用途也不猜。是否只回应玩家强制工作是独立scope政策，不是“自动工作不算工作”；沿用ActiveCommand为Dev候选，AnyOccurrence可显式放宽已核工作来源。范围补齐不批准普通scope／总序／节奏，整体Dev门不变。

**参数、配置与来源：** 完整Work键的共同字段按作者及玩家服务／Race／合法Xeno层解析；ActiveCommand须实际playerForced且玩家可控，继承子也检查同次捕获命令。玩家可选scope、禁用和恢复精确行，不用scope标签新增工作机制。作者快照恢复配置，不缓存上次工作True。没有每Job另一个引擎或永久存档工作状态。

**机会、重叠与供音／调制：** 同owner状态机会，Move与工作准备可同时True；当前Dev仍Move先Work，Move中选但无声不补Work。VisitSickPawn原joyKind=Social，也可能Social先中选，不提高Work优先掩盖重叠。准确Worker域Work条目／合法后备、局部年龄×心情／一次jitter与G保留；不借工作台或患者域。子Work独立完整键有自己的scope／参数／条目／硬保护，直接共享AudioId不共用冷却。

**形态、退出与证据：** OneShot正常结束／中断保自然尾；Sustained在当前合同失效／Unknown、宿主退出时复检释放自有槽。新增71组分别经UsWorkObservation→UsPawnFacts→实际CompTick→同session→准确请求及同主体／Job／driver／Toil反例，旧七组有效证据沿用；真缺Sow局部Unknown而CutPlant仍派发。额外真实feed观察分离Worker和ActualIngester，保OneShot两种退出自然尾及Sustained正常／中断结束。Job/driver/Toil/调度／音频是明确平台double，精确方法体／Def来自4871；不称游戏内工作或声音已验。

**SR差异／后续关口：** SR只认workGiverDef、Move先Work；Random720/p.012、共享216，出厂ActiveCommand，playerForced才给该来源，自动工作默认静默。US用准确过程清单替代标签，保事实／回应资格分离，不按720宣称等频。原七过程造成的Core范围工程缺口已在本轮补齐；普通scope／竞争／节奏及合同／成果人审、原生听感仍pending，未据此关闭整个US-C3。
## Draft：实际玩家gizmo由未征召变为征召

**固定合同／主体／阶段：** 4871 Pawn.GetGizmos经draft controller给出准确Command_ColonistDraft hotKey、Draft/Undraft tutorTag的Command_Toggle。包装原toggleAction，先记Drafted、正常执行后比较；false→true才发布`us.core/DraftEvent`，角色DraftedPawn。重复包装用弱引用表避免同Command嵌多层；setter、加载已征召、无变化／异常不是本事件。不是命令下达成功便假定状态已变化。

**可调与来源／资格：** 完整Draft键的共同enabled／scope、硬tick、局部倍率／响应按合法层；实际命令方向和主体不可删除。开发候选scope ActiveCommand，玩家可控及正向命令事实；不能以Any标签制造setter事件。玩家独立关Draft不关闭Undraft，可恢复精确行，不改原版征召操作。新UI仍沿现有动作行，不重排页面。

**机会／音源／调制：** 实际变更给同owner事件机会，A映射一次p，自身／共享硬截止及同tick仍生效，多个选中Pawn各有自己owner。用该被征召Pawn准确域Draft条目，不借Select或Undraft音频补位；局部年龄×心情／jitter及G正常作用。和Select同批的密度、例外和总序仍由政策候选审阅，不因提高priority假定必响。

**形态／失败／扩展：** OneShot马上解除也自然尾；瞬时征召事件不等于“当前仍被征召”，Sustained下次复检无事件依据结束，持续状态扩展须独立正向事实。缺GetGizmos实现只隔离命令观察，不挂全局setter，不吞PatchAll；来源恢复不重播旧征召。继承只保固定方向证据，参数／音频／时钟用新完整键。

**SR实际及验证：** SR同gizmo前后比较，ActiveCommand，External36 Realtime ticks=0.6s、ignoreGlobal但仍更新共享钟，器官／Talking门适用。US保实际操作方向与准确域，改统一游戏tick／四结果，不暗继承0.6s及共享例外。DcNativeTests实际gizmo多次获取、一次变化准确Draft输出、无变化／setter反例；4871 controller与GetGizmos已核。推荐保成功变化反馈，批量征召、Select同序列及尾音待用户验；本包合同／成果／原生pending。

## Undraft：同一命令接缝的独立反向事件

**固定合同／角色：** 原gizmo正常执行后的true→false才发布`us.core/UndraftEvent`，角色UndraftedPawn。取同一实际Pawn，不是“当前未征召”周期状态；存档加载、脚本setter、重复点击无变化都不造新事件。与Draft共享观察实现但有独立完整ActionId、条目、参数与动作截止，不是把Draft反向播放。

**可调／来源／资格：** 共同Undraft enabled、scope、硬tick、局部倍率及响应按作者→玩家服务／Race／合法Xeno；开发候选ActiveCommand，正向命令和玩家控制仍须成立。不能通过关Draft关闭Undraft或以Any删除固定方向证据。单行／层Reset只清该身份玩家覆盖，作者来源／快照保留。

**机会／声音／调制：** 真实变更入原owner，A事件p、同tick与共享／自身硬保护生效；快速来回不会因两个声音包增加机会，也不从两包各自间隔推出统一往返频率。声音来自被解除Pawn准确域Undraft映射，显式与Draft共享AudioId不共享动作配置。局部年龄×心情、一次jitter及G取本Pawn，不取操作玩家或另一Pawn。

**形态／扩展／恢复：** OneShot可与已播Draft尾重叠；Sustained瞬时事件不维持“未征召状态”，下一依据复检失效结束，离图释放。继承须自己声明额外固定条件／来源参数和音源；独立状态根须提供当前状态事实。观察缺失隔离Draft/Undraft来源，坏来源不替换B、不重播上次解除，输出四结果无补位。

**SR实际／证据／待验：** SR同gizmo反向通知，External0.6s Realtime、ignoreGlobal仍写共享钟；独立Undraft动作钟可与Draft交替，均有器官／Talking。US保独立身份与自然尾，统一tick／仲裁和四结果差异须实际轨迹比较。DcNativeTests真实toggle→Undraft准确条目／角色、无变化反例；不可用Draft通过替本包人工接受。推荐保独立解除反馈，快速往返与批量密度／旧尾需验；合同、成果、原生／听感pending。

## MentalBreak：正向崩溃启动，不以任意MentalState代替

**固定来源／主体／结果：** 4871已核四个实际执行入口：MentalBreakWorker.TryStart基类返回mentalStateHandler启动结果；Catatonic override添加崩溃Hediff后true；RunWild override改变Pawn种类／阵营和相关状态后true；WildDecree override发出崩溃命令后true，即使没有成功进入附带Wander状态仍是该正向崩溃执行。HumanityBreak经base启动，成功后加Inhumanized，观察base一次，不把包装再记一遍。角色BreakSubject，实际pawn正常返回true且在当前地图时通知；不是周围听众。

**支持候选／排除：** 仅基类会漏三个不调用base的直达override，D-C已按明确目标补齐隔离Dev候选；不扫描所有名称。false／异常不发布。任意TryStartMentalState、SocialFighting、婴儿MentalFit均不是本事件；Crying/Giggling专属回应已退役，但不禁原版状态／婴儿声音或这些Pawn的其他合同。若第三方override不经已核目标，要显式提供自己的正向来源。

**参数／资格／来源：** `us.core/MentalBreak`共同enabled、scope、硬tick、局部倍率及响应按合法层；Dev候选AnyOccurrence、器官／Talking，不暗加清醒／玩家控制，无“任意精神状态算崩溃”开关。固定成功来源不可被设置删除。需持续崩溃者另提明确状态依据，不能把瞬时启动永远保True；已取消的Crying/Giggling也不作为全局禁止第三方合同的名字黑名单。

**机会／音源／调制：** 正常成功事件进入崩溃者同owner，A当次p、自身／共享硬截止、同tick适用；不因关键事件I=0就推断全部保护豁免。只选崩溃者准确域MentalBreak条目／声明后备，不按任意精神状态切音源。US当前年龄×心情独立响应，不把任意InMentalState当Break覆盖所有动作的年龄；局部jitter一次，G实时。

**两形态／故障／实际链：** OneShot状态随后结束仍自然尾；Sustained启动事实下一复检失效便结束，不是崩溃全程循环。目标逐机制readiness，缺目标具名隔离该实现，其余正向机制可用；来源快照重连不恢复事件或旧句柄。`Patch_MentalBreak→Notify_MentalBreak(实际类型/方法)→同session→完整配置/条目/请求`；DcNativeTests涵盖基类、三个直达override、Humanity包装一次、false／throw与准确机制诊断，已执行于更新后的216项候选fixture。方法体来源精确4871，运行仍为平台double。

**SR差异／用户审阅：** SR只挂基类成功，漏三个直达override；External0/ignoreGlobal但尝试仍写共享钟、有器官／Talking。它的CurrentMood另把任意InMentalState分类Break，不能因hook正向便证明调制也正确。US修正分类、补明确来源为Dev候选，正式扩围与关键例外由PM用户裁定。推荐审真实崩溃即时反馈及非崩溃音色回归，合同／成果与原生听感pending；退休两专属包装边界已定，不重复投票。

## 全15包审阅入口与退役边界

以上15节都包含共同合同及本包实际效果；官方分类为1通用Select、5能力限定Wounded/Death/Attack/Move/Equip、9 Pawn专用其余包，不是继承树或15个执行引擎。除已定Select三默认、Eat Whole/Any/Any等决定外，显式Dev覆盖／资格／总序／节奏尚不构成普通默认。逐包合同人工审阅、成果人工验收在[验收页](us-kernel-0.6-acceptance-zh.md)分别保pending。

退休只删除官方Crying/Giggling及专属MentalFit回应链、枚举／目录／设置／Help和无消费者残余；不删除原版声音／状态，不禁某类Pawn全部声音，不禁独立作者定义自己的正向合同。历史语料的17个字符串输入用于冻结旧路由测试，不是当前15包权威；golden不重录。可加载作者例、完整来源和字段见[规格](us-kernel-0.6-spec-zh.md)，精确版本／事实／反例见[事实页](us-kernel-0.6-fact-adapters-zh.md)，检查与明确fixture迁移见[迁移页](us-kernel-0.6-test-migration-zh.md)。
