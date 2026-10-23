# 0.6.x 测试迁移与显式 Dev 消费

当前r2实际结构、通知新前提、Config与删除映射见[r2合同](us-kernel-0.6-refactor-zh.md)；本页保留各包原有的产品语义／来源／未决与人工关口。

2026-10-24 · r13 · **完整Core常用Work范围已补齐；固定FL A002只读补丁最小消费通过，普通政策／原生／逐包人审独立保留。**

## r2当前迁移：保反例，明确退休的API前提

完整生产Core由`UniversalSqueakerRefactorCoreTests`递归独立编译，无平台引用或替身；Kernel历史选择／回放golden不改。OneShot保同tick预留、同步四收据、失败推进、当前授权和持续保护；全15、20 WorkType／105 Giver／78过程及真实Dev Equip入口仍由生产适配链的明确平台替身验证。没有把四个贯穿例当全包覆盖。

设置／Reset链接当前生产GameSettings、ExposeData、原子文件与Scribe替身，保null／false／0、来源三态、last-good、persist-first失败及Committed后发布修复。旧Config读取前提按获准breaking合同退休；新必需格式marker不是兼容探测。一次配置应用只发布一次；同一发布者修订递增，跨重载实例按快照引用区分。

Host保真实确认窗、作用域命名、身份漂移、一次写入／revision、popup遮挡、导航和EN／ZH度量反例。原数值timing编辑器退休，其取消／确认／旧draft不得覆盖恢复值的反例改用仍在运行的音量字段；F06保当前详情的层／准确域／空目标理由，F07保当前详情按文字实测增长。原两条倍率reset行的固定+60px形状不再是当前界面合同。scope支持／默认／归一只读canonical合同，UI只有标签；按字段来源、依赖、局部暂停与无效模板不可用取自实际fold。

新增作者例由当前整个Core loader解析；`PlayerTemplate.xml`另由生产preset解析器读取；一次C#声明例仅引用最终产品DLL与目标4871编译引用，不链接US源码／游戏替身。各阶段结果必须关联实际输入，不据工程编译或零匹配模式声称运行通过。

## 保留的D-C迁移（r2前输入与API）

- 官方完整ID／metadata／作者目录／界面覆盖分母均15（旧短键enum在r2退休），删除Crying/Giggling、Patch_MentalFit／Notify_MentalFit、专属开关／Scribe／Help／Keyed／日志事件。没有给原版声音／状态或第三方独立合同加黑名单；新增纯例证明independent/Crying与independent/Giggling仍可正向派发。
- Kernel历史语料生成曾因使用新15库存缩小输入而实际失败两次；恢复冻结的17字符串查询及候选池，官方清单仍15，既有语料byte-identical／确定性零差异。不得重录golden以隐藏输入变化。S3旧17枚举矩阵仅移除退休两包装行，保其他期望；纯OneShot新增公开15映射／资格加载及独立根证据后933项。
- 原生形状fixture直接链接生产通知／readiness／UsPawnFacts／配置／单session／输出，不再手写Notify_Select。216项分别覆盖十五包与七Work过程、真实角色、Scope命令来源、重叠及清理；未激活／Equip未选资格49，真实缺Wounded＋近战marker另54。全部是明确平台double和真实Harmony，不是游戏运行。
- Select用户已裁默认三真；删除Unselected枚举及旧资格未选模式。gate21改为真正`--dc-select-settings`，76项覆盖独立调整／缺失／false／来源／合法Xeno／可选依赖／固定拒绝不被取消；gate20保代理／扩展链，显式玩家放宽后65项。旧53项保历史，不再作当前默认依据。
- Settings／Reset原metadata double固定17导致新15库存断言失败，现链接真实SqueakActionModel；SqueakActionConfig仅从Verse Comp容器分出，类型／字段／XML意义不变。没有把期望改成旧double17。生产settings/ExposeData/原子文件与Scribe替身往返验证新三资格的独立has/value、保存重开、失败恢复与准确Reset。
- Host退休断言按真实当前控件迁移：旧baby checkbox删除，保egg／Talking的单写与hit surface；Eat三下拉不再用退役精确checkbox，Select新增独立三下拉、三层写及单字段继承。视图／布局钟一次写一次失效；不重排页面。完整Comp metadata用已构建产品ILSpy核，避免冻结UI平台缺ThingComp时把TypeLoad失败冒充能力缺失。
- Work不再workGiverDef即True；正例迁为明确Research等driver／同Job／有效Toil，命令资格明确输入。官方Work开发scope默认保持ActiveCommand，覆盖事实的夹具显式选Any，继承子自己的ActiveCommand不被父参数取消。实际Comp已解析scope进入同一有效配置视图；不增加额外状态机。
- 日志协议只删除退休的hook.mental_fit.unavailable期望和注册项，fmt2活跃9项逐个完整性校验；其余行不削弱。MentalBreak显式正向基类和三直达override，Humanity经base只通知一次，不把任意精神状态或名称相似当启动。
- 持续维护补齐与启动相同的Select参数适用性：非选择合同收到Select专属字段时，其自有持续槽也结束，不等下一次启动才拒绝。真实Move持续槽修前断言失败、修后通过，216项包含这一保存有效配置后的当前授权反例；不改变无这些字段的Eat／其他合同。

### 适用检查入口

GLOBAL Dev入口定向检查：以`-c Dev`构建UniversalSqueakerAudioBindingTests，运行`--equip-dev-entry`。工程直接编译生产UsOneShotDebugActions，不重新手构选择policy；通过实际菜单登记的SelectCandidate委托，进入生产SelectDevelopmentPolicy，再由真实Harmony接缝观察EquipmentAdded→Notify_Equip→同owner准确Equip条目。断言未选整体门零owner／输出、开发模式守门、玩家Core成功、AI／非Core／无关系／throw反例以及重复菜单不重置保护。LudeonTK菜单登记、Verse物体与音频是明确替身，不声称游戏菜单点击。七参数ctor缺省Unselected保持，原定向适配矩阵仍按未变输入沿用。

本轮Core Work在原七组之上补71组准确Job/driver，共78组；20 WorkType/105具体WorkGiver完整分类见[范围表](us-core-work-scope-zh.md)。--work-core逐新增组执行实际捕获/请求/主体/Job/driver/Toil/ended反例及重叠/继承/持续链；--work-missing配WorkFixtureTarget=MissingSow真缺实现；--work-care-lifetime只执行实际喂食主体/OneShot自然尾的增量判别。Job/driver/调度/音频为明确替身，原七组和其他15包未变矩阵按原身份复用。

verify-local保原门20代理、22/23真缺Selector目标、24/25 Eat／缺Chew及26结果消费者，21转新Select资格；追加27 `--dc-native`、28分进程执行Release `--dc-unselected`及Dev `--equip-dev-entry`（整体门／未选资格与真实菜单入口）、29 `DcFixtureTarget=MissingWoundedAndMeleeMarker`＋`--dc-missing`。Host用既有`--lane`选DC Select、退休库存、typed、Overview composites、US switch、current navigation、DB Eat及text-fit等受改链；匹配零lane必须失败。

实际执行仅取受改必要链，不机械全31门。完整输出复制到独立已获准运行区域，一次核MyComputer再原入口运行；保原输出和失败日志，不改CAS/ADS/ACL/owner。生成新候选后按改变的输入决定重跑范围，不以文件名或exit0代模式命中。当前结果与最终源码／Dev身份由交付记录绑定。精确4871定点来源已取得，真实Scribe／游戏Config、挂接／音频、屏幕／听感及15包两个人工关口仍pending。

## 历史D-A/B迁移（原输入）


### D-B新增的定向迁移

当前配置／作者入口见主规格§22。删除旧SqueakEatOccurrence、精确／包含药物bool及debugName宽化，旧XML/ABI不保双轨。S3旧Eat断言改为过程与真实阶段；Kernel旧bool规则改为独立枚举合法性与NonPawn边界；稳定路由／roll corpus不改。

OneShot新增DbEatAuthorTests，运行可加载的EatMinimal／EatExtensions；AudioBinding用明确参数--db-eat运行生产链，--db-eat-missing运行EatFixtureTarget=MissingChew真正缺目标构建。结果Root、实际摄入者、当前物品及0值进入同一生产协议。settings新增DbEffectiveConfigTests和真实Mod保存调用失败反例，Reset继续检验准确字段与恢复；Scribe平台仍为替身。

结果消费者回归用`--db-result-routing`独立模式（gate26），不再只靠总有高优先After根的DbEatTests：先真实加载仅Eat音频、零Result合同，原状态已成功且下一机会未到期时正常返回0／正值；再加低于Eat优先级的独立Result根及继承子合同。修复只筛选本次结果通知的固定合同消费者，未变通用仲裁、软／硬规则或golden。修前／修后日志分存，未变纯层／Config／UI矩阵复用有效证据。

既有Host的旧父子checkboxfixture定向换成三个独立dropdown、三准确层及单字段继承；保行高、修订钟和其他按钮保护。Host可用--lane "DB Eat"、"Overview composites"及"typed"运行受改面，不因其中一个通过声称全Host套件通过。独立输出运行时优先从明确cwd定位项目，避免把私有管理目录当源码根。

独立根／多级继承、不可变固定条件、完整ActionId和实际来源条目已进入原生产目录、session、选音、请求及诊断；作者入口见[主规格§21](us-kernel-0.6-spec-zh.md#21-d-a-当前作者入口)。官方目标为15包；当前代码仍有17旧包装，Crying/Giggling与专属设置／响应完整退役在D-C。

- 原生事件发布事实，让独立根和扩展合同在同owner仲裁；显式公共作者动作调用仍只求该完整合同。普通Select与受伤扩展已过真实观察器／配置解析／session链正反例，原版Selector、Comp门及音频为明确替身。
- `ActionPlan`的器官／Talking为显式选择，通用缺省不带Pawn门。旧手写Work夹具须明确`requiresVocal:true`及所需Talking政策，保留NoVocal、诊断零重采样和随机次序原断言。
- `ChainResult.SelectedContext.Domain`表示**实际供音域**，请求主体域另存。Xeno请求用最终Race表时须记录Race供音。S2定向断言迁移；稳定选音／roll／请求golden不重生。
- `DaContractConfigTests`以生产settings/ExposeData/原子文件与Scribe替身往返，重读完整父／子键、false/0、来源快照和子行Reset；Pawn Global不能覆盖两NonPawn命名空间。D-B已通过相同生产settings行接入完整稀疏分层与Eat参数。
- verify-local保留原19门，追加20（`--da-select`）和21（另加`--unselected-qualification`）。每模式独立进程，凭目标断言而非exit0／文件名判断命中。当前本地测试复制完整输出到获准MyComputer目录运行相同入口；不作为原生游戏证据。

缺目标回归位于DaSelectMissingTargetTests。工程属性SelectFixtureTarget=MissingInternal或MissingFeedback在明确平台double中真正移除对应方法，并非覆盖生产readiness布尔值；构建使用各自独立OutputPath/IntermediateOutputPath，运行参数为--da-select-missing-target。verify-local追加22/23执行两种缺失形状，每种必须检查目标确实缺失、PatchAll正常返回、无半套观察、单次具名诊断、依赖隔离与无关Work派发；存在两目标继续用gate20的原63项。

以下S1–S5表保留历史迁移脉络。旧17项映射、Select响应性、Eat联动和覆盖式心情按已批准新合同定向迁移，不作为新线不变政策。

S3实际17动作/来源三态/Config事务/同owner兼容时钟与证据分层见[S3实施页](us-kernel-0.6-s3-zh.md)。gate17覆盖新增纯S3轨迹，gate18覆盖真实事实适配及自动Comp/新作者资格，gate3追加来源保存故障与Scribe重读；Reset继续单独执行。

S2的实际证据、旧→新保留含义、独立路由/请求golden及目标1.6接缝见[S2实施页](us-kernel-0.6-s2-zh.md)。verify-local现保留原16门并追加17纯S1/S2、18正常适配和19失败观察批次。以下总图继续保留后续责任，不能把计划表当完成表。

本页配合[首版规格 r11](us-kernel-0.6-spec-zh.md)和[合同—验收附页](us-kernel-0.6-acceptance-zh.md)，只安排真实入口如何迁移，不另定义 C00–C12、T01–T12 或 S1–S5。下面旧切片表保留历史责任，“新增”不单独证明已经执行；当前D-C执行边界以上文为准。原迁移基线为 US `d35dee7ca9bb790f097f00537477b363c9a06565`；当前D-C从535d0af接续，旧载荷独立保留。

## 1. 从现有入口推进，不另建平台

- [verify-local.ps1](../scripts/verify-local.ps1)的当前入口是31门：gate 1 Kernel；2 ConfigCopy；3 SettingsMigration；4/5 Release/Dev Log；6选定载体身份；7/8产品Dev/Release构建；9单载体；10许可证；11 manifest；12 UiLogic；13真实Host消费；14 UI边界；15 stub覆盖；16冻结stub来源；17纯S1/S2；18正常资源适配fixture；19独立失败观察批次；20为D-A Select，21为已裁Select独立三资格，22/23为真正缺少Select目标的两个编译fixture。24为D-B Eat生产链，25为真缺Chew的编译fixture，26为无Result合同／独立Result消费者路由；27–29分别为D-C十五包通知、未激活／Equip资格候选、真实缺Wounded与近战marker。30为完整Core Work增量，31为真缺Sow局部隔离。编号保持追加规则，不把源码注释或CI步骤标题中的旧门数当实际覆盖。
- [CI](../.github/workflows/ci.yml)恢复 Source/tools 工程后调用同一脚本；[release workflow](../.github/workflows/release.yml)是发布入口。当前两者仍绑定 `v0.7.0-rc1`，不是0.8验证；本页不更改流水线。
- [KernelTests工程](../tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj)链接生产 Kernel、Pure 和 `VerseEventBinding.cs`，net472，无游戏引用；0.6继续使用这一纯测试边界，增加生产转换用例，不复制一套状态机。核对完整编译集、解析/传递引用及公共参数、返回、泛型约束，不能只grep命名空间，也不能加入Verse/Unity同名stub后仍称纯层。
- [ConfigCopy](../tools/UniversalSqueakerConfigCopyTests/Program.cs)、[SettingsMigration](../tools/UniversalSqueakerSettingsMigrationTests/Program.cs)、[Reset](../tools/UniversalSqueakerResetTests/Program.cs)链接生产持久化代码并使用stub，属于Config适配证据。Reset不是verify-local的独立执行项；获准实施包需显式执行其工程，不可因CI恢复了项目就称已运行。
- [Host工程](../tools/UniversalSqueakerKernelHostTests/UniversalSqueakerKernelHostTests.csproj)加载产品和FL载体并复制runtime stubs；[DefinitionDirectory工程](../tools/UniversalSqueakerDefinitionDirectoryTests/UniversalSqueakerDefinitionDirectoryTests.csproj)用独立扩展stub执行生产collector及真实Source/Host，当前也不是独立门禁。二者证明适配/消费轨迹，不证明C00纯层或真实游戏方法体。

## 2. 按合同决定保留什么

“稳定合同”保留结果并适配入口；“旧政策”在获批变化后定向替换；“实现探针”重审所测机制；“平台证据”保留边界且补目标1.6观察。同一lane可含多类断言，不能整批删除或整批照搬。

| 真实入口／断言 | 分类与迁移动作 | 原因／合同／切片 |
| --- | --- | --- |
| [UnitTests](../tools/UniversalSqueakerKernelTests/UnitTests.cs)：`PerRacePoolIsolation`、`XenoDoubleKeyDomains`、`PackFallbackExactDomainOnly`、`DomainKeys` | 稳定合同：保留准确域、同Race合法父路径；适配完整身份，新增两个不同NonPawn目录身份及对象运行状态隔离 | C01/C00；V01；S1/S2。无需为目录用例实现完整NonPawn宿主 |
| 同文件：`SelectOff`、`SelectFallbackChain`、`SelectRemix`、`VariantWeightMixing`、`PackWeight`、`EggFiltering`、`EntryLevelFilter`；[RemixEmptyTierRegression](../tools/UniversalSqueakerKernelTests/RemixEmptyTierRegression.cs) | 稳定合同：保留四模式、空层等权、层内权重、年龄/彩蛋独立；适配AudioId及稳定条目键，新增带标签roll消费断言 | C07/C01；V10；S1/S2。`SelectOff`检验的是Vanilla→Kernel Off的最终后备，不是Disabled；Disabled另测生产入口提前旁路、零派发 |
| `FailurePathFallback` | 稳定的动作内音源后备：保留；新增“选Work后失败不得改Call”的反例，不能把音源fallback误作跨动作fallback | C02/C04/C07；V02/05/10；S1 |
| `ActionKeyMapping`、`ActionSync`、`BuiltInTable`、[ActionKeyMirror](../tools/UniversalSqueakerKernelTests/ActionKeyMirror.cs)、`VerseEventBindingContract` | 实现探针＋稳定旧入口：当前官方15动作映射，历史语料17字符串仅保路由输入；替换“中央枚举就是全部动作”的限制，新增公开包装/提供者及XML组合fixture | C05/C02；V07/09/15；S1/S3至D-C。实际可加载XML例见主规格§21–23 |
| `TimingModelRules`、`TriggerInvocationRules`、`EatOccurrenceRules`；[CompSqueaker](../Source/UniversalSqueaker/CompSqueaker.cs) | 旧政策＋入口合同：保留旧clock、主动scope、Select响应性、Eat细分及外部许可的逐项对账；新线定向替换时速/人口乘硬间隔、概率/冷却顺序与统一尝试结算断言 | C03/C04/C05；V03–06/15；S1/S3/S4。新单机会、硬门先于随机依D2；不把所有trigger或Wounded默认设为共用豁免 |
| `ModulationRules`、`AgeDefault`、`AgePriority`、`LayeredTuning` | 稳定年龄变体选择、字段存在性/继承与旧心情覆盖年龄调制政策分开；保留`AgeDefault`/`AgePriority`选音语义、false/0及玩家覆盖，D4获批后替换覆盖式合成为独立响应；新增恒等、改倍率不启用、Apply/Skip及去重/一次抖动 | C08/C10；V11/13；S3/S4。局部启用不能覆盖独立全局活跃度 |
| [ConfigCopy A–H](../tools/UniversalSqueakerConfigCopyTests/Program.cs)：`VersionBumpKeepsDelta`、`SingleEntryEditKeepsOtherMarkers`等 | 稳定合同：保留临时Config、原子写、版本更新不丢玩家delta、删除标记与重启幂等；新增来源快照独立fixture，不重解释旧fallback副本为新行为来源 | C09/C08；V12/15；S3 |
| [SettingsMigration](../tools/UniversalSqueakerSettingsMigrationTests/Program.cs)：`MigrationFailureBlocksAndRetries`、`VoiceSchemaStaleDoesNotOverwriteMoodTuning`、字段clear及provenance案例 | 稳定合同：保留失败可重试、旧schema不偷进版本、字段级clear不丢来源；适配新域/来源三态；新增缺字段与显式false/0、Inherit与来源选择分离 | C08/C09；V11/12/15；S3。不以新来源要求阻断原来合法的旧配置读取 |
| [Reset](../tools/UniversalSqueakerResetTests/Program.cs)：`ActionRowRestoreExactIdentity`、`ActionAreaLayerZeroIsNotGlobal`、全局污染/NoChange/round-trip | 稳定合同：保留准确范围、身份、一次publish/持久化、迁移阻塞保留和fallback不误删；新增单域遗忘来源、不清其他域与重新自动采用禁令 | C09/C08；V12/15；S3；“恢复默认”不等于“遗忘作者来源” |
| [UiLogic源码不变量](../tools/UniversalSqueakerUiLogicTests/UiSourceInvariantTests.cs)、[Kernel合同探针](../tools/UniversalSqueakerUiLogicTests/UsKernelContractInvariantTests.cs) | 实现探针：保留有效边界/词表检查；随生产接缝定向适配源码字面匹配，新增运行转换与引用检查 | C00；S1–S5。文字匹配不能替代真实隔离 |
| [Host/Program](../tools/UniversalSqueakerKernelHostTests/Program.cs)、[ResetUiLane](../tools/UniversalSqueakerKernelHostTests/ResetUiLaneTests.cs)、[ThemeSwitchLane](../tools/UniversalSqueakerKernelHostTests/ThemeSwitchLaneTests.cs)、[DefinitionDirectory](../tools/UniversalSqueakerDefinitionDirectoryTests/Program.cs) | 消费/平台证据：保留生产Source→Host动作链、身份、无多余写入、目录不扩大声音准入；当前按固定FL0.8实际release/edit/Palette条件迁移，保全部现有用途，不重写页面/复制行为核心 | C00/C05/C08/C09；S3/S4。旧UI短测不关闭新Kernel V项 |
| [LogTests](../tools/UniversalSqueakerLogTests/Program.cs) Release/Dev；[运行适配](../Source/UniversalSqueaker/Runtime/SqueakKernelAdapter.cs) | 日志合同保留，诊断投影适配新原因/结果；新增诊断异常不撤销已派发、读取不重抽roll、历史请求与实例证据分离 | C04/C11；V05/14；S1/S5 |
| [Kernel corpus入口](../tools/UniversalSqueakerKernelTests/Program.cs)与[Scenarios](../tools/UniversalSqueakerKernelTests/Scenarios.cs) | 稳定确定性＋旧政策语料：保留默认只比较、两次字节重放；逐条登记有意变化，分离0.6固定轨迹 | C00/C03/C04/C07/C10；S1–S4。现有17动作、多种子、多域语料不是新机会/原生播放覆盖 |

不得为了转绿全量执行`--update-corpus`。旧语料继续绑定旧政策；需要观察候选时只用`--emit-corpus <明确候选路径>`，差异审阅列明旧含义→新合同、批准D项、替换断言及反例，再决定具体基线更新。该参数确会写指定路径，不能把冻结基线当候选路径。

## 3. 固定输入可以重放，适配不另写决策

纯驱动直接调用生产核心。采用附页自持fixture与T01–T12，不在此复制数值或改公式：固定准确对象/域、事实及缺口、有效配置、初始H/W/R和同tick名额、带标签的随机序列、后端四结果。每案独立复位；连续案例明确引用前一快照。记录：输入tick与事件顺序→所选动作/AudioId→每次roll标签和值→后端调用及次数→确认派发/硬截止/下一机会→原因。缺少或多消费一次roll均失败；仅固定seed不够。

- T01–T04、T08校验成功/拒绝/不明/已有实例及同tick竞争；不明推进保护、不写确认、不马上重试，失败占过的名额不释放。
- T05–T09校验硬门前零roll、事件不受软等待阻断、事件自身限制与不排队、状态改变只在下一机会读新事实；新增概率未中、无成立动作、无候选、调制失败各自原因与未来重检断言。
- T10–T12校验局部坏绑定不跨动作补位、三态与缺实现不同、不同对象不共享机会/冷却；额外用1包/3包相同输入比较机会与决策次数。
- 调制新增规则解析去重/抖动次数；Config新增A关闭/false/0→缺源且B存在→A同身份归来→必需实现仍缺→单域遗忘轨迹。快照存贡献及存在性，不存上轮true、计时、句柄。

游戏适配fixture送真实协议形状，只观察事实/绑定/回执翻译并进入同一生产核心；禁止在夹具或适配器再实现选择、概率、冷却、重试。纯工程验证移除平台引用仍能编译运行；Config/Host/runtime stub证据另列，不拿它们补纯核心依赖。

## 4. V01–V15必须各有责任证据

责任指后续实施包所有者；完整V项仍按切片关闭；S1自动覆盖见OneShot S1实施页，不能以旧绿色或部分覆盖关闭整项。每项记录纯层、适配fixture、1.6原生、听感的实际状态；不适用项说明理由，不把未运行写成通过。

| V | 责任测试／原生观察 | 合同与切片 |
| --- | --- | --- |
| V01 | 核心所有者保留域隔离并补NonPawn目录、T12；资源适配者核对AudioId绑定与合法父路径 | C01；S1/S2 |
| V02 | 核心所有者T01/T09/T11及无成立合同零Call；动作适配者核对Call真实idle语义 | C02；S1/S3 |
| V03 | 核心所有者T01/T02/T09/T12、1包/3包机会数、所有失败未来推进、停止/恢复排程 | C03；S1/S4 |
| V04 | 核心所有者T05–T07/T09；动作适配者核对真实受伤通知与17动作阶段/原条件 | C03/C04/C05；S1/S3/S4 |
| V05 | 核心所有者T01–T05及诊断异常；后端适配者注入副作用前拒绝/后不明，保存实际派发证据 | C04；S1/S5 |
| V06 | 核心所有者T07/T08；持续适配者观察低优先不替换、高优先许可及OneShot不伪称抢停 | C04/C12；S1/S5 |
| V07 | 作者入口所有者运行普通Ratkin XML、组合XML与最小C#事实例；旧未预注册`NotifyExternalByKey`仍合法且原门控生效 | C05；S1/S3 |
| V08 | 资源适配者1.6启动观察15个Ratkin SoundDef（41 OGG不算41候选）、字段/整根patch、只撞Work、缺绑定零裸名退路 | C06；S2 |
| V09 | 核心T10/T11；初始化适配者缺Biotech、坏Work、缺必需合同/调制依赖只隔离实际依赖 | C02/C05/C06；S1/S2/S3 |
| V10 | 核心保留四模式/Remix/权重/年龄/egg并记录roll；资源适配者消费同域实际绑定 | C01/C07；S1/S2/S4 |
| V11 | Config所有者单源初次采用/后加冲突/Inherit、玩家父域权威、全局活跃度独立；保存重读结果一致 | C08；S3/S4 |
| V12 | Config所有者A缺源/B存在/重连/实现缺失/单域遗忘轨迹与旧delta回归；1.6真实Config重开核对 | C09；S3 |
| V13 | 核心所有者恒等、倍率不启用、Apply/Skip、去重/一次抖动、非法贡献定位；适配者核对本次参数 | C10；S4 |
| V14 | 输出适配者同SoundDef US/非US同播、音量1→0→0.5、距离、首帧/辅助音/池复用及共享定义不变；人类听感独立签收 | C11；S2接缝/S5完整 |
| V15 | 动作/持续适配者17动作、旧入口scope/许可、同键不刷冷却、状态失效/复检故障/离图清理；Config所有者旧读取/保存，无新save负担 | C05/C09/C12；S3/S5 |

## 5. 1.6资源与输出先证实接缝

产品[csproj](../Source/UniversalSqueaker/UniversalSqueaker.csproj)当前为net472、Krafs.Rimworld.Ref `1.6.4871`；[About](../About/About.xml)与[LoadFolders](../LoadFolders.xml)指向1.6。引用编译只证明API形状。主规格所用1.7研究材料不是1.6方法体或运行证据。

S2先由资源/输出适配所有者在获准的1.6环境记录实际游戏build、DLC/模组集合、US/FL候选身份和配置；观察patch前来源→最终声明→解析/内部资源就绪→AudioId与最终SoundDef对象一致。覆盖字段patch、合法整根替换、重复声明、删除、坏内部资源及未受影响候选。不能只查覆盖后的DefDatabase，或用“枚举到一个好clip”证明内部grain池安全。

S2/S5原生输出须记录实例创建/副作用边界、四结果、US归属的建立与清除、首帧到原版更新后的有效贡献、附属音与池化复用；同SoundDef非US对照不变，共享SoundDef不写入。总音量从0恢复、在播距离/音量变化、持续同键复用/高优先替换/状态失效与离图均需真实观察。听感另核对声源、音量/距离过渡、重叠和残留声音；回执Dispatched不等于“已听见”。stub、1.7或无错误日志不能签收这些项。接缝失败回到适配修复或明确取舍，不退回共享Def改写。

## 6. 当前固定FL0.8消费者迁移

现已实际消费fc010bd A002固定0.8 Dev只读补丁，范围[0.8.0,0.9.0)，Private=false；生产/Host/三Manifest/主窗口/Overlay/确认/主题/编辑与实际绑定均迁移，未重做页面或普通政策。显式构建命令、完整颜色映射、Live时机、native release、生命周期与原生边界见[固定FL0.8消费说明](fl08-consumer-migration-zh.md)。

原0.7快照保历史，新Host平台double单独来源化至tools/FerriteLib08.Stubs；公开只读观测及实际事件替代旧私表计数。verify-local现31门，Host另有FL08 readonly最小消费目标（旧FL08 actual证据按适用输入复用）；gate15扫当前0.8替身，gate16只保旧0.7冻结快照完整性，不冒充当前0.8运行证据。定向执行受改消费者链；D-C纯层/配置/音频输入未变的有效证据复用，不机械全测或重录golden。

CI/release仍获取旧已发布0.7，目前不能构建当前0.8消费者；维护者需提供真实0.8发布载体后再更新远端获取配置。本地固定Dev技术迁移与该发布依赖分开。

## S5已替代共享距离路径，原生证据仍待完成

原活动`CompSqueaker.ApplyDistanceRange → SubSoundDef.distRange`写入已移除。Settings现在发布实例距离，`UsInstanceOutput`沿原生Sample更新只改准确归属的AudioSource；L固定、G实时，非US对照不变。首帧/后续Sample/起止辅助音/池复用及End后延迟grain已有生产适配反例，真实1.6与听感仍未运行，C11/V14不能凭fixture关闭。候选仅用于隔离Dev技术验证，不作为一般安装/发布建议。

## 历史S4AB/S5实际迁移范围（旧0.7输入，非r2）

- `CompSqueaker.EntryPoints`的常规tick与公共通知进入唯一`PawnResponseAdapter`，17动作及已注册作者组合共用决策；真正未注册字符串保留合法调用能力，不要求新注册。临时全局开发候选选前零派发，选后实际调用消费，不保旧决策链。
- 独立A只映射状态W与普通真实事件p；显式候选StateWaitFactor只进入W_base。新`S4S5ReplayTests`连续两次记录固定事实、tick、roll用途和值、四结果、硬截止及未来机会并比较；稳定旧golden未更新。
- `S4AuthorResponseTests`用普通`Responses.xml`和生产loader验证响应启用、逐轴年龄覆盖、参数、包→本包动作→条目→玩家参与、跨包不泄漏及严格形状。行为来源不拥有参与权威；缺依赖/非法贡献明确拒绝。Config fixture重读真实Scribe字段，验证A、独立响应和准确域层级；Reset清新玩家字段而不遗忘作者来源。
- gate18链接实际Runtime/Comp入口和实例输出，原生平台为明确替身，Harmony patch与辅助音transpiler为真实执行。覆盖构造时首Sample、实时G、同Def非US、aux重建info、池复用、End/Cleanup后延迟grain、缺patch owner拒绝，以及持续同键/高低替换/未知启动保护。它不证明真实Unity混音。
- Host仅定向迁移退役概率/时钟/覆盖式心情编辑探针；仍检验五工作区、许可/scope/Baby/Eat、当前说明、动作行恢复、真实确认窗及身份漂移。移除的旧numeric timing draft不再声称已执行；没有批量重生golden或改冻结FL stub。
- 完整结果和每门适用性由`dist/evidence/s4ab-s5/REPORT.md`记录。S1–S3实施页是其历史交付，不回填为本轮成果。

上述S4AB/S5段保其历史0.7输入；当前固定0.8消费与验证边界见§6，不将旧运行报告改写成0.8结果。
