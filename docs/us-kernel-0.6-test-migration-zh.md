# 0.6.x 测试迁移与显式 Dev 消费

2026-10-18 · r1 · **测试迁移总图；OneShot S1已运行纯层/作者装配测试，完整平台/后续切片未验收。**

本页配合[首版规格 r2](us-kernel-0.6-spec-zh.md)和[合同—验收附页](us-kernel-0.6-acceptance-zh.md)，只安排真实入口如何迁移，不另定义 C00–C12、T01–T12 或 S1–S5。OneShot S1已批准部分见主规格与实施页，D项其余候选仍须在依赖切片前裁定；下文“新增”是后续实施责任，不表示已有测试。基线为 US `d35dee7ca9bb790f097f00537477b363c9a06565`；checkpoint已包含获准UI/目录源码与测试；旧载荷独立保留。

## 1. 从现有入口推进，不另建平台

- [verify-local.ps1](../scripts/verify-local.ps1)的实际入口是16门：gate 1 Kernel；2 ConfigCopy；3 SettingsMigration；4/5 Release/Dev Log；6选定载体身份；7/8产品Dev/Release构建；9单载体；10许可证；11 manifest；12 UiLogic；13真实Host消费；14 UI边界；15 stub覆盖；16冻结stub来源。编号保持追加规则，不把源码注释或CI步骤标题中的旧门数当实际覆盖。
- [CI](../.github/workflows/ci.yml)恢复 Source/tools 工程后调用同一脚本；[release workflow](../.github/workflows/release.yml)是发布入口。当前两者仍绑定 `v0.7.0-rc1`，不是0.8验证；本页不更改流水线。
- [KernelTests工程](../tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj)链接生产 Kernel、Pure 和 `VerseEventBinding.cs`，net472，无游戏引用；0.6继续使用这一纯测试边界，增加生产转换用例，不复制一套状态机。核对完整编译集、解析/传递引用及公共参数、返回、泛型约束，不能只grep命名空间，也不能加入Verse/Unity同名stub后仍称纯层。
- [ConfigCopy](../tools/UniversalSqueakerConfigCopyTests/Program.cs)、[SettingsMigration](../tools/UniversalSqueakerSettingsMigrationTests/Program.cs)、[Reset](../tools/UniversalSqueakerResetTests/Program.cs)链接生产持久化代码并使用stub，属于Config适配证据。Reset不是当前16门中的独立执行项；获准实施包需显式执行其工程，不可因CI恢复了项目就称已运行。
- [Host工程](../tools/UniversalSqueakerKernelHostTests/UniversalSqueakerKernelHostTests.csproj)加载产品和FL载体并复制runtime stubs；[DefinitionDirectory工程](../tools/UniversalSqueakerDefinitionDirectoryTests/UniversalSqueakerDefinitionDirectoryTests.csproj)用独立扩展stub执行生产collector及真实Source/Host，当前也不是独立门禁。二者证明适配/消费轨迹，不证明C00纯层或真实游戏方法体。

## 2. 按合同决定保留什么

“稳定合同”保留结果并适配入口；“旧政策”在获批变化后定向替换；“实现探针”重审所测机制；“平台证据”保留边界且补目标1.6观察。同一lane可含多类断言，不能整批删除或整批照搬。

| 真实入口／断言 | 分类与迁移动作 | 原因／合同／切片 |
| --- | --- | --- |
| [UnitTests](../tools/UniversalSqueakerKernelTests/UnitTests.cs)：`PerRacePoolIsolation`、`XenoDoubleKeyDomains`、`PackFallbackExactDomainOnly`、`DomainKeys` | 稳定合同：保留准确域、同Race合法父路径；适配完整身份，新增两个不同NonPawn目录身份及对象运行状态隔离 | C01/C00；V01；S1/S2。无需为目录用例实现完整NonPawn宿主 |
| 同文件：`SelectOff`、`SelectFallbackChain`、`SelectRemix`、`VariantWeightMixing`、`PackWeight`、`EggFiltering`、`EntryLevelFilter`；[RemixEmptyTierRegression](../tools/UniversalSqueakerKernelTests/RemixEmptyTierRegression.cs) | 稳定合同：保留四模式、空层等权、层内权重、年龄/彩蛋独立；适配AudioId及稳定条目键，新增带标签roll消费断言 | C07/C01；V10；S1/S2。`SelectOff`检验的是Vanilla→Kernel Off的最终后备，不是Disabled；Disabled另测生产入口提前旁路、零派发 |
| `FailurePathFallback` | 稳定的动作内音源后备：保留；新增“选Work后失败不得改Call”的反例，不能把音源fallback误作跨动作fallback | C02/C04/C07；V02/05/10；S1 |
| `ActionKeyMapping`、`ActionSync`、`BuiltInTable`、[ActionKeyMirror](../tools/UniversalSqueakerKernelTests/ActionKeyMirror.cs)、`VerseEventBindingContract` | 实现探针＋稳定旧入口：17动作映射保留为兼容适配检查；替换“中央枚举就是全部动作”的限制，新增公开包装/提供者及XML组合fixture | C05/C02；V07/09/15；S1/S3。D6冻结后才能写可加载新语法 |
| `TimingModelRules`、`TriggerInvocationRules`、`EatOccurrenceRules`；[CompSqueaker](../Source/UniversalSqueaker/CompSqueaker.cs) | 旧政策＋入口合同：保留旧clock、主动scope、Select响应性、Eat细分及外部许可的逐项对账；新线定向替换时速/人口乘硬间隔、概率/冷却顺序与统一尝试结算断言 | C03/C04/C05；V03–06/15；S1/S3/S4。新单机会、硬门先于随机依D2；不把所有trigger或Wounded默认设为共用豁免 |
| `ModulationRules`、`AgeDefault`、`AgePriority`、`LayeredTuning` | 稳定年龄变体选择、字段存在性/继承与旧心情覆盖年龄调制政策分开；保留`AgeDefault`/`AgePriority`选音语义、false/0及玩家覆盖，D4获批后替换覆盖式合成为独立响应；新增恒等、改倍率不启用、Apply/Skip及去重/一次抖动 | C08/C10；V11/13；S3/S4。局部启用不能覆盖独立全局活跃度 |
| [ConfigCopy A–H](../tools/UniversalSqueakerConfigCopyTests/Program.cs)：`VersionBumpKeepsDelta`、`SingleEntryEditKeepsOtherMarkers`等 | 稳定合同：保留临时Config、原子写、版本更新不丢玩家delta、删除标记与重启幂等；新增来源快照独立fixture，不重解释旧fallback副本为新行为来源 | C09/C08；V12/15；S3 |
| [SettingsMigration](../tools/UniversalSqueakerSettingsMigrationTests/Program.cs)：`MigrationFailureBlocksAndRetries`、`VoiceSchemaStaleDoesNotOverwriteMoodTuning`、字段clear及provenance案例 | 稳定合同：保留失败可重试、旧schema不偷进版本、字段级clear不丢来源；适配新域/来源三态；新增缺字段与显式false/0、Inherit与来源选择分离 | C08/C09；V11/12/15；S3。不以新来源要求阻断原来合法的旧配置读取 |
| [Reset](../tools/UniversalSqueakerResetTests/Program.cs)：`ActionRowRestoreExactIdentity`、`ActionAreaLayerZeroIsNotGlobal`、全局污染/NoChange/round-trip | 稳定合同：保留准确范围、身份、一次publish/持久化、迁移阻塞保留和fallback不误删；新增单域遗忘来源、不清其他域与重新自动采用禁令 | C09/C08；V12/15；S3；“恢复默认”不等于“遗忘作者来源” |
| [UiLogic源码不变量](../tools/UniversalSqueakerUiLogicTests/UiSourceInvariantTests.cs)、[Kernel合同探针](../tools/UniversalSqueakerUiLogicTests/UsKernelContractInvariantTests.cs) | 实现探针：保留有效边界/词表检查；随生产接缝定向适配源码字面匹配，新增运行转换与引用检查 | C00；S1–S5。文字匹配不能替代真实隔离 |
| [Host/Program](../tools/UniversalSqueakerKernelHostTests/Program.cs)、[ResetUiLane](../tools/UniversalSqueakerKernelHostTests/ResetUiLaneTests.cs)、[ThemeSwitchLane](../tools/UniversalSqueakerKernelHostTests/ThemeSwitchLaneTests.cs)、[DefinitionDirectory](../tools/UniversalSqueakerDefinitionDirectoryTests/Program.cs) | 消费/平台证据：保留生产Source→Host动作链、身份、无多余写入、目录不扩大声音准入；按FL0.8迁移实际调用，不重写页面/复制行为核心 | C00/C05/C08/C09；S3/S4。旧UI短测不关闭新Kernel V项 |
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

## 6. FL0.8只以固定身份Dev产物联调

FL基线为`bc1f200e0d47ba9beee9703547a9db3ba77c1471`上的[0.8规格](../../ferritelib/docs/development/0.8/README.md)及[合同验收/测试迁移§4–6](../../ferritelib/docs/development/0.8/acceptance-and-test-migration.md)。能力、纯层证明、三路共用行为、1.6文本/宿主证据及迁移规则直接遵循FL文档，不在US再定义。FL文档本身尚不是实现产物。

当前[Mod构造入口](../Source/UniversalSqueaker/Mod.cs)的`[0.7.0,0.8.0)`不包含0.8。[Directory.Build.props](../Directory.Build.props)仍有旧兼容默认路径；不能依默认值、移动HEAD或旧payload冒充0.8消费，也不在本文直接放宽pin。

后续获准的新线联调包必须一起交付并核对：

1. **FL输入身份**：明确Dev DLL/包位置、源commit及dirty事实、构建配置、API/ABI与支持入口/依赖；交付清单能确认选中的字节，不能只写“最新0.8”。US源码、Config副本、候选输出均独立于旧checkpoint及dist/payload。
2. **US显式决定**：依实际0.8公开合同审定新线API范围、C#/XML/行为迁移；新能力的注册/依赖不得追加给旧合法入口。保留原线原范围，不能把新范围验收投射为旧线兼容。
3. **构建与隔离**：复用[resolve-carrier](../scripts/resolve-carrier.ps1)、[build-dev](../scripts/build-dev.ps1)、verify-local的显式`-FerriteLibArtifactPath`及`-DevelopmentCarrier`入口；所有生产/Host/DefDir工程必须解析到同一固定输入。旧stub快照/扫描器依赖和API需在新线定向迁移，不能修改冻结快照伪装兼容或无依据免检。Dev与Release输出、构建顺序遵循既有隔离约定。
4. **消费证明**：记录实际解析引用及US内嵌载体收据，编译并运行真实Source→Host关键动作/恢复/身份链；确认运行加载同一FL身份且无第二载体。US包继续不携带`FerriteLib.UiKit.dll`。Dev受控测试不等于Release载体放行。
5. **实际1.6配对观察**：用该US/FL固定候选验证FL要求的输入归属、IME/字体、宿主布局与US设置保存/重开；Kernel音频仍按本页V表单独验收。列明未完成证据与消费者接收结果，不能只凭FL测试绿或US编译绿称消费完成。

当前CI取得的固定0.7 release路线继续服务旧线；新Dev验证需后续任务书授权源码/pin/脚本与工作副本改动，复用同一CI/门禁体系而不是建立第二验证平台。D项裁定、FL真实Dev交付和1.6运行环境分别由对应所有者提供；本次文档工作不等待它们，也不据此关闭V项。
