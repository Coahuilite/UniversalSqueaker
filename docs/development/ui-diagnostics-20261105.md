# B 诊断与帮助实施检查点

在诊断面板中搜索并点击对象，再按“开始新录制”。查看另一个对象不会换掉录制目标；“停止录制”作用于底部状态点名的录制对象。详情先读结论、已保存事件和原因，再进入“技术全文”；返回保留原视图和适用位置。即使录制对象已失效或刚重开窗口，保留记录仍有技术全文与原有复制反馈入口。新录制会替换上次已停止记录。

**当前1.6验收候选：`dist/offline-workbench-20261106/FerriteLib-US-B-Offline-Workbench-1.6-20261106-c3-dev.zip`。** 本地工程已核收、整体仍为部分接受；c3-dev完整承接r6的Dev配对和未验清单，准确1.6 native仍待人验。旧`delivery-r6-dev`保留为回退，旧r5 Release已撤回推荐；不重复验已被完整替代的旧包。

下文是r6实施时的检查点。A009依据`WO-USFL-R5-DEV-STEP-20261105` r2／`DSP-US-R5-DEV-CONSUMPTION-A009` r1，以固定producer-r5-dev编译US Dev并形成两根完整ZIP；该历史包的入口、身份与步骤仍在`dist/ui-diagnostics-20261105/delivery-r6-dev/USE-zh.md`。`delivery-r1..r5`及其source/check保历史／恢复。A008写前574个固定源与r5准确source-form一致；A008运行接线增量、A009 docs增量和最终准确source/check随r6记录，HEAD不冒充全部dirty来源。

## Dev能力：先分清目标与测量条件

以下入口已在r6的US_DEV配置编译，实际调用覆盖与未覆盖分开列在下文。c3配对身份和入口见`dist/offline-workbench-20261106/candidate-us-c4/DEV-PAIR-zh.md`；其“是否替代r6”保留原交付时点，以本文上方现行状态为准。成本／几何流程见本节及保留的r6 USE。不能仅换其中一个DLL或同时加载相同packageId的别名副本。

先开启RimWorld原版开发者模式，让既有开发面板可见；打开US设置，在系统→诊断→开发工具点“打开开发面板”。原Debug Actions入口`Developer: open settings dev panel`还要求设置窗口已存在、日志模式不是Disabled。选择目标不会创建对话框；Remix必须先沿原业务入口打开真实确认窗。普通确认窗本来就共用dialog目标，因此目标标签改为“确认对话框”，而非把所有dialog冒称Remix。

| 功能 | 实际目标与开关前提 | 结果／输出 |
| --- | --- | --- |
| 几何采集 | 面板“设置页”或现存“确认对话框”；US_DEV＋FL Dev仪器；显式Capture | 该Host下一实际pass的capture；不开其他窗、不自动开outline |
| 独立描边 | 同一所选Host，显式Outline；不要求Capture | 只绘该Host；capture关闭仍可OutlineOnly，不自动开日志或成本采样 |
| 显式几何报告 | 同一Host先开Capture，再点Report | 请求后等更新的已完成capture；原audit保存同次snapshot＋dump，面板显示请求目标／request_page／session及结果pass。文本走`[UniversalSqueaker.Kernel] ltrace: geometry`；生成与日志实际输出有区别 |
| 采样当前设置页 | 只支持真实Settings Host；日志Explicit Enabled；当前无排队／活动样本 | 下一BeforeDraw启动原三组Begin/End，32 Host pass；原`ltrace: UI.cost`，手动trigger=dev-panel、creation=NotCaptured |
| 诊断面板专属outline | 先打开US Kernel诊断面板；Debug Actions的`Diagnostics: toggle panel UI outline`；Dev仪器 | 仅诊断主面板实际Host，不通过设置面板目标，不给其他Host开capture |
| US Kernel诊断／录制 | 原正常诊断入口；不要求US_DEV、原版开发者模式或详细日志 | 浏览／录制／技术全文及有界反馈仍由原session/capture owner持有 |

### 纯成本采样：几何关，条件固定

1. 在同一完整Dev配对、同一游戏负载中，先把设置Host的Capture和Outline均关闭；诊断面板等其他几何工具也关闭。不要在同一段样本内取几何报告。
2. 日志选Explicit Enabled，回到要测的设置页，保持域／动作／展开字段／草稿／scroll／语言／主题／字号和窗口尺寸。等开窗或Enabled变化触发的原自动样本结束，再在开发面板选“设置页”，点一次“采样当前设置页”。
3. 按sample/session/first_pass/trigger读取32-pass首尾metadata与原phase/view/metrics输出。几何关闭只剔除几何工具成本：Explicit Enabled仍使现有详细日志及fit策略有效，面板本身也有开销，应记录这些状态。它不是无工具基线。

采样不导航、不换Host、不重建草稿／展开／scroll；Remix、普通确认、未知或失效目标均无此能力。mode关闭、关窗／失败沿原partial和三End清理；按钮与owner均拒绝重入。Host pass不是FPS；不能把本Dev与旧Release样本直接当作优化前后差。

### 几何取证：另做一段

在开发面板选择真实Settings或已打开的确认窗，分别开Capture／Outline；仅看边界可以只开Outline。点Report后，让目标窗口继续完成实际pass。报告完成由目标audit推进，面板关闭不留下无人消费的请求；目标关闭则取消并释放。设置页原几何控件读取同一audit的实际开关，面板改动不会留下旧勾选。报告状态“已生成”表示已保留结构化snapshot和同次dump，不保证日志门或预算允许每行落到原生日志。取证结束分别关闭两个开关；关窗最终都清理。

Dev下日志Auto默认详细开启，Release下Auto默认关闭；成本样本仍只认Explicit Enabled。`ltrace`为进程session内512条预算，`ResetSession`恢复，不随每次点Report重置；`ptrace`另有512个不同key上限。几何dump和成本汇总共用ltrace门／预算，Disabled或预算耗尽不强制补印，内存事实／清理不依赖输出成功。固定FL Dev源码已核原生输出仍按Host/session每60真实秒限normal12／warning24／error24、去重key容量64，Configure不清预算；US不添加第二个FL打印器。另有FL每pass的512 nodes／128 inputs及各自drop counts，它们是capture容量，不是ltrace行预算；outline-only不建snapshot／dump。

`US_DEV`仍只开放已有开发能力；普通启动继续`UsResponsePolicy.CreateOrdinary()`。开发播放实验只经既有候选DebugAction显式选择且要求原版开发者模式，编译Dev不会自动选择实验方案。

## A008独立修复与当前证据

源级发现并修复：开发面板Report原来只请求／即时尝试一次，后续无人消费；现在由真实目标audit在既有Publish推进，仅显式pending才生成一次。设置页旧报告入口读回实际更新的receipt，不把上次报告当本次完成。目标解析拒绝失效／未知Host，页名取当前目标的真实binding，状态明确request_page而不读取另一窗口；Settings开关取消重复本地状态。availability初探改为重写既有Outline值，依据固定0.8 setter合同，不再短暂开关Capture或清空已有capture；终止释放同时关outline，诊断主窗HostDetached也释放本窗audit。未改Kernel录制、采样算法、播放policy或任何检查源码。

A008完整原报告在US区域同TASK的`A008/REPORT-r2.md`，能力定位／相对r5增量随该报告存放。A008本身未编译、未运行产品动态检查、未打包；下面A009才记录实际固定Dev消费，不回填历史报告。

## A009实际Dev消费与完整本地配对

US Dev实际MVID为`362f9e2f-3c28-4e61-b743-f0a9508e07ea`，1095168 bytes；固定FL Dev为`8e0cf5d2-7b38-4cb8-9cb5-6f8c8035e82a`，711168 bytes。两者均net472／x64，US及Host实际csc均含`TRACE;US_DEV;DEV`，FL生产者实际`TRACE;FER_DEV;DEV`。US版本仍0.6.0，ProductVersion不含-dev不能据此误判配置；AssemblyConfiguration与真实编译命令均为Dev。Private=false、原编译引用SHA收据、Host实际载入FL比对及stage完整性门未改。

本次US／Host／UiLogic构建零警告／错误，完整UiLogic通过。Dev诊断lane通过575项，明确执行`layout diagnosis is per host`与`outline switch is independent of capture`，覆盖原真实binding→per-Host audit→固定FL仪器、独立开关／状态、outline-only报告拒绝及隔离释放；普通shell13、真实确认／Remix11通过。平台double不是native；这些原方法没有执行开发面板Report待完成→自动Publish的连续链，也没执行新的原位32-pass队列、重入和partial组合。未重纳旧audit/report lane或加case。

两根ZIP只含完整FerriteLib5文件与US10文件，恰有本次两个DLL，无PDB／ref／test／游戏／Harmony DLL；所有15文件直接读回与目录一致，FL完整根与正式固定输入一致。最终source-form包含574个准确源码／docs／检查文件、累计dirty/check patch、A008相对r5增量及A009相对A008的docs增量；固定FL source/interface/USE/identity与原604项生产者证据一并给出。完整回执和报告在US区域同TASK的`A009/REPORT-r1.md`，未重建FL、未开游戏、未安装或提交。

原作者红门、来源列表直接交互/native、O5、新Report／采样连续动态、新面板native、Pawn生命周期和独立1.7缺口保持。r6与旧r5配置条件不同，不作优化前后比较；A009本地交付后停写。

## 已接通的生产路径

- `SqueakDiagnosticsOverlay.Browse` 用原 session model 的浏览角色持有 Pawn；`UsDiagnosticsSessionSource.ClickRow` 不调用原版 Select，也不自动锁定。`CanStartCapture` / `StartCapture` 直接取 browsing Pawn，不要求成功快照。既有 `recordingPawn` 是唯一 capture owner，离开视口或浏览 B 后继续保留 A 的读侧条目；64 条 ring、覆盖、停止、清空、game/map 身份和退出链沿原 owner。
- `CapturedRecords` 只读投影 → `UsDiagnosticsDetailCache.History` → 原详情控件，展示真实已结算事件、序号、原因及回执。没有新 ring 或业务判定。当前缓存证据与录制 A 的历史在技术全文中分别标明。
- `UsDiagnosticsSpec` 仍经过 UiKit Schema 2。窄屏列表、详情、技术全文各有保留的独立 Scroll；技术正文是未禁用的只读多行文本。底部工具栏按实际文字自然宽度换行，Measure/Draw 采用同一展示快照，操作在绘制后执行。空详情沿原紧凑高度起步，再按搜索、范围、前三个完整候选行、分页和 footer 的实际测量求下限，最后受真实缩放屏幕钳制。
- 完整 Evidence 不再常驻普通详情；技术全文包含所有已提供的 gate/basis、当前 evidence、捕获初态与最多64条保留记录。Rules/PreviousEvaluation 不再漏绘，PreviousDispatch/Unknown 也不冒称当前。时间单位贴在各时间值上；换算开关移入技术阅读，不改变交互真实秒。原反馈出口和64 KiB白名单上限未改。
- A003更深现有检查发现条件行中性引导线丢失、状态并进名称的回退。详情恢复名称／状态各自测绘、同一行正常字号换行；状态独立着色，值与标记不混合，首个当前block沿原attention rail，其余条件保中性rail。没有改数据或折叠owner。
- B 全页继续原导航和22/16/14角色。表单限宽660、主要组间节奏、短值编辑与有效／继承状态对齐；保留名册300、详情660、查看包124。稀疏编辑仍读本层值，空白、显式 false/0、拒绝后的草稿及 native editor owner 不变。完整来源进入技术披露，错误和暂停理由仍在现场。
- 页头帮助可开关；逐字段／组纯说明按钮退出。正常帮助不再写隐式 pin；稀疏字段的真实 label/input/effective 区域发布准确 topic、字段名和动作／域，随后 reading-hold。同语境重开保位置，新对象重置。标题、正文、context和预算共同使用当前展示状态；独立显式 pin 与原无 page-state consumer 的15-pass grace保留。
- 动作排序／索引、group-first、Repeat keys、展开字段 effective fold/copy、来源 XML 和来源列表按实际 view／域／XML／查询失效。BuildView 原有缓存保留。US 窗口详细请求实时同步，关闭 detailed 后停止 timing/fit 请求、ring 读取和 Trace 字符串入口；关闭释放本窗。仅管理 US 自身请求，不裁决第三方全局开关。
- US 业务前缀改为 `[UniversalSqueaker.Kernel]`。必要故障账本保原1024容量，滚动淘汰最旧 claim，新故障不被旧满额永久吞掉，溢出后给淘汰／重复抑制摘要；与内存事实 ring 独立。

## 固定 FL 已接入创建、实时策略与关闭

- 设置、诊断主／独立窗、camera overlay、开发面板、普通确认与 Remix 均在真实 Host 构造前调用 `VerseUiDiagnosticLog.CreateHost("universalsqueaker", originalSource, ...)`。原工厂签名、metrics、bindings及post-create设置保留。Remix沿同catalog/key/manifest改用小型 `UiWindowHost` 适配器，以免密封 `UiPageWindow` 在创建时取到旧consumer。
- `UsUiDiagnostics.Adopt` 用 Attach 取回同一binding；Session-root仅存采用标记，不另建logger索引。实际绘制／窗口策略同步 Configure DetailEnabled，关闭详细时 Trace=null。ptrace保其原具名有界出口，不同时接第二个FL Trace打印器。fit、timing、geometry仍各由原请求开关控制，logger不启动采样。
- 生产Host的fit/style/recovery等框架native输出归FL；US不从ring再打印。事实subscription、capture、feedback、显式geometry和cost aggregate保原owner。独立旧audit/attach调用不被强制采用US logger，原Warning observer不替换；生产源码没有自定义Session.Warning打印回调要迁移。Host/Session最后释放binding及摘要，US不提前Dispose，execution仍借用。
- 设置页请求 Session grace=0，完成pass的CurrentHelpRef/CurrentHelpSource因此是真实发布；US自身Follow状态承担reading-hold，不再把grace恢复当新hover。标准HelpRef、选项HelpRef和自定义稀疏anchor共用此路径。原独立help消费者可继续自行选15-pass，显式pin仍优先。自定义HelpHover改用context-aware裁剪／层级命中；自己的popup空HelpRef沿当前composite已声明的general topic，不从label/value猜topic，也不借下方被遮内容发布。

## 实际证据与未覆盖项

r5历史报告在US区域同TASK的 `A007/REPORT-r1.md`；A006 r2及更早原件保实际输入／范围。原位入口实施后，`dist/ui-diagnostics-20261105/a007/build/{Release,Dev}`及Host构建均零警告／错误，实际FL compiler receipt通过。Release MVID `0c4fc822-edb6-49e3-a9e3-44728c43de6c`。完整UiLogic通过，开发面板写入口精确pin由9迁10（仅dev-sample），现有shell生命周期13与Remix确认11通过；未新增case。成熟stage/readiness针对该历史配对载荷执行；USE、source-form和source/check随原候选保留，不证明A008新源码已编译。

经r4精确许可迁移：普通factory fresh0／无隐式pin、raw独立技术reader、窄版独立scroll、真实分组标题、paired run正文量高守卫、Log单一前缀及Close30。保原断言数量、点击／一次写、非目标组、负向／正向控制和精度。现有Release诊断lane整组通过（550 assertions），Close30双语及正向溢出控制通过，Log Release／Dev suite均通过；独立15-pass、帮助滚动／主题仍通过。覆盖为产品DLL＋平台double层。

WO r6的两个scope场景已改走当前b-action-scope物化下拉的可见press/release，不再注入旧popup或固定滚动700。现有观察器只对该scope物化节点用公开Host popup-owner事实识别真实触发器；不猜label/index。覆盖／遮挡／实际anchor与原精度、typed Disabled／null继承、外部点击零写均通过（18／20 assertions）。最后仍需观察真实游戏输入。

WO r7沿同一Workspace场景迁到既定四个调参／三个系统分组，按XML声明的Cols／MaxWidth／Gap及当前父区域推等宽、位置和边界，逐项真实点击再返回原目的地。双语、宽窄、帮助开关／hold和返回trail通过（479 assertions）；没有恢复七项同屏。原几何比较精度不变，实际helper因更多真实点击执行其原有可见性断言，没有新增断言源码。

现有claim守卫沿XML的HelpRefBind追到Behavior的field-topic声明及实际字段集合，贡献字段沿原ContributionFields生成链；不从catalog生成claimed。未知绑定／无法识别声明失败，双向解析和catalog冲突保护保留。随后修复真实缺失的帮助anchor：百分比输入、页标题上下文、规则输出、查询／筛选说明、计数、包名／查看／域选择／开关和既有列表查询。只加真实字段／标签／只读上下文的HelpRef，不加纯i按钮；关键词与计数两种译文跟当前B语义校准，原技术内容保留。帮助Close不认领新topic，避免关闭时重置阅读位置；现有独立滚动／theme检查通过17，双语Close30／fit-hit与正向溢出控制通过11，独立显式topic／typed／15-pass通过29。

N1后方实际运行到完整UiLogic通过：同Host诊断新增技术打开／返回与事件选择的精确写入口pin为15；帮助单一逻辑toggle是两个互斥的开／关caption，当前返回为nav-Voices；home的真实列表位置为b-roster-split/b-list-column。原双向／旁路／纯读等断言保留，不增加case或断言。

作者两几何场景**无同合同映射**：只有已不在当前registrar／manifest中的UsFilterBar先按半窗口截断作者显示，再用最宽项＋12、trigger floor和viewport钳制；现有标准dropdown及StyledDropdown不采用这个截断上限。不能拿当前scope等不同合同控件求绿。两个原方法和长选项／glyph／cap／viewport／owner断言原样保留且实际失败，零legacy回调不作无overflow证据。另一个既有LongAuthor列表场景仍停在已不存在的voice-filter-body／pack-author控件，未改成不同来源数据求绿。

当前b-source-query／b-source-rows来源列表按真实来源package ID选过滤项（对象Def来源或语音包来源），不是旧作者字符串popup。Workspace与UiLogic只覆盖home布局／双语结构和通用draw；RichDynamic覆盖原绑定及绘制不泄露scope，不证明当前来源搜索／点击。没有现有直接场景执行b-source-query的输入、b-source-row的选择／分页或长来源可读性，这些仍未覆盖；旧RaceBoard纯投影／author-options绑定检查不代签当前列表。普通editor注入即O5保A002失败／A001旧r2对照，不自动销号。

未运行游戏、真实Config/Saves、native绘制／选择／IME／剪贴板、完整浏览B录制A及失效／换图／退出动态链，没有新FPS数据。N1／Workspace／Close30及本轮最终docs／locator按上述结果收口；O5、无映射旧popup门、当前来源列表未覆盖及native／1.7分别保留，整套Host gate未全绿。既定截图轮次不重置。

## 1.7 仍消费共享载荷

1.6配对后核对两个Mod根：1.6/1.7 LoadFolders均加载 `/` 与 `1.6`，About均声明两版本，无第二份FL或隐藏1.7 DLL。项目仍为net472／Krafs.Rimworld.Ref1.6.4871，没有独立1.7引用编译入口；未下载引用或把重复1.6编译称为1.7。r4 producer的reference/JIT/native限制保留，真实1.7加载、字形、IME、输入与声音交有权限的原生验收。A007交付后停写；A004方法／架构判断、A005快照去重及其他未变证据复用，UiLayoutWork累计含义不变。

## 同次刷新只读一次，不改变刷新节拍

`CompSqueaker.GetDiagnosticSnapshot`只读取已保存的回应证据和评价，不选音频、耗Rand、推进业务时间或更新生产context。`RefreshIfDue`在同一同步调用内先跑到期detail角色，再跑到期viewport；浏览／锁定／视口重叠原会对同entry重复构造相同快照。

现在用overlay拥有的可复用HashSet记录本波已尝试的entry，RefreshIfDue的finally清空引用，不留跨帧快照缓存。0.25／0.5秒门和角色／viewport更新不动；重复角色请求仍发原revision通知，但不重读或重放失败。LastSuccess是真实首次成功读取时间，ReadFailures只计实际失败尝试，mark／fingerprint按该结果更新，录制读状态沿原RecordReadStatus；下个到期波次重新尝试。capture的settled事件、64条ring、浏览B录制A及map/game/关窗owner不变。

源码可证明重叠请求被收敛；现有diagnostics550与overlay生命周期／失败入口通过，真实Pawn同波次成功／失败和实机耗时仍未观测，不给FPS收益。

## 原位采样当前设置页

想测高级参数大量展开后的稳定成本，应先保留同一窗口、域、动作、展开字段、滚动、语言、主题／字号与游戏负载。当前 `Expanded` 只允许当前动作展开；先前创建的其他动作字段可保留节点但隐藏，不等于所有动作同时展开。

1. 先在系统→诊断→开发工具将日志设为“启用”（Explicit Enabled），并打开原开发面板。
2. 回到目标设置页，展开需要的动作参数，保留域／对象／草稿及滚动，等待现场稳定。原开窗或模式false→true自动32-pass样本若仍在进行，先等它完成。
3. 面板目标选“设置页”，原位点击“采样当前设置页”。只排一个请求；下一次该目标BeforeDraw启动三组原Begin。排队／采样中不显示可重复触发的按钮，状态显示当前目标session、起始页／pass、触发来源及进度。
4. 32个Host pass完成后，读取原日志出口 `[UniversalSqueaker.Kernel] ltrace: UI.cost`；按sample／session／first_pass／trigger关联首尾metadata及中间phase、view、metrics／US draw结果。手动样本trigger=dev-panel，creation=NotCaptured，原source的create_ms也为NotCaptured；不是测得0。原日志实时门和512条ltrace预算仍生效，关日志后的partial不强制绕门输出。

面板仍为原360宽窗口：正文16、30高操作、三列换行页入口，单一可滚动内容区，采样状态正常正文换行，报告状态不再与固定120＋220宽同时挤一行。没有新窗口／自定义控件或纯i按钮。Remix／未知／失效目标明确不可用，不偷偷采Settings；普通无采样能力的旧factory／DevPanel入口仍可用于原功能。

GameSettingsWindow仍是唯一采样owner。HostAttached在实际Host的root state注册可选Owner，查找必须匹配同一window.Host且session有效；HostDetached在Close和失败释放时清空能力／原Settings target、partial结束及dispose audit，早于FL实际dispose。面板按所选目标取能力，命令与排队方法双重实时拒绝重入。排队动作不调用Begin、不invalidate目标、不改页／域／展开／草稿／scroll或control ID；下一BeforeDraw先drain上一pass，再启动新32-pass。模式关闭、关窗和失败沿原三End清理；metrics.active先复位，输出门关闭也不会留下活动采样。开窗和false→true自动行为保留，trigger分别为open／enabled。

样本保留起始和结束的developer-panel／detail／fit／geometry状态；scene_changed跟踪content／definition revision、页、窗口尺寸、所记录主scroll／help scroll和这些工具状态。它只表示这些已观察维度是否变化，不是稳定性证明：未逐一追踪所有辅助scroll、草稿内部变化或游戏负载。不可把自动导航混合样本称为稳定展开采样；准备记录中仍须写当前动作、数据规模、主题／字号、游戏负载及其他诊断开关。

现有UiLogic／shell／确认检查没有操作新采样分支，也没有当前原生开发面板交互case。只读取生产PageXml作XML语法／结构检查，未渲染／点击新面板；排队→32-pass、重入、模式partial／Close／失败组合与真实草稿保留仍待有权限的native现场。没有新增单测／case／probe替代。Host pass不是FPS、Root.Update总帧或GC／分配数据，phase inclusive时间不相加；新增入口不证明瓶颈／架构收益，亦不假设未确认可用的Profiler。
