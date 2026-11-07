# 旧 B 行为工作台：source 与 K4 接入依据

首段依据 WO-USFL-OFFLINE-WORKBENCH-20261106 r2／DSP-US-OFFLINE-WORKBENCH-A001 r1；准确5000原生续段依据WO r4／DSP r2，Dev／空行修订r3，最终p5/c4与人类入口修订r4。这里记录 US 实现边界；正式派发、固定 FL 消费许可与产品验收仍由 CO／PM 记录。

## 游戏入口仍走原配置 owner

`IUsBehaviorPageSource` 只提供行为页的动作／域投影与命令，`IUsBehaviorFieldSource` 是可选高级字段能力。真实 `BindBehaviorSheet` 改用这两个能力边界，不再直接 cast 游戏 source。

`UsSettingsBehaviorPageSource` 的每项操作都委托原 `IUsKernelSettingsSource`；高级字段 adapter 委托原 `UsKernelSettingsSource`。普通 Create 的签名、默认嵌入布局、确认窗、来源事务与 persist-first 提交条件不变。没有要求游戏调用者安装 Tools 或改用开发版库。

`UsBehaviorFieldValues` 共用原稀疏字段读取、解析／验证与有效值文字投影。游戏提交仍由 `TryCommitBehaviorOverride` 完成；helper 只修改候选副本，不保存、不发布。

## 隔离 source 使用真实 Core，不伪造运行世界

`UsOfflineBehaviorSource` 接显式 `DefinitionCatalog`、翻译、只读 roster 与可选来源 XML／玩家行。动作取自输入定义，普通默认取自 `UsResponsePolicy.CreateOrdinary()`；有效值沿 `BehaviorConfiguration.ResolveProfile/Resolve`、`BehaviorContract.Defaults/InvalidParameters`，高级编辑使用上述共享字段逻辑。来源绑定／继承沿 `BehaviorSourceState`，包括旧 timing 拒绝项；可用事实只表示输入定义具有该实现，不表示现场 Pawn 条件成立。

所有写入只存本实例内存；没有 GameSettings、运行时 singleton、文件写入或音频发布。样例标识为 `us/offline-core-v1; memory-only; no runtime publication`。输入未声明的 preset anchor 不会产生可执行的预设恢复。确认与跳转由调用方显式提供，缺少能力不会暗中确认或假装导航成功。

该 source 已纳入游戏程序集与独立 provider 编译。首个固定交接为 `dist/offline-workbench-20261106/provider-u1/`，用正式放行的 FL producer-p2 neutral 编译；注册类型、作者目录和实际输入见其 `USE-zh.md`／`IDENTITY.json`。p1只表示合同已收，没有 p1 DLL 实跑。后续同 Host 实际消费使用固定 tools-native5000-c1／producer-p3-native5000，非 p1 DLL 实跑；c2（u2＋m2）保留；r3交付candidate-us-c3-dev（u3＋原m2、完整Dev配对），r4最终工程候选为candidate-us-c4，原二进制不重编。

独立 provider 通过 `Register(UiPreviewContext)` 取得实际 metrics、翻译、作者目录、watch 和启动语言。外置模式注册完整 `Layout.Schema2.xml` 的 `AddProjectedLayout`，从同一已接受 snapshot.Layout 建 Host 后 Attach。prepare 仅设置帮助高度 layout input 并调用共用帮助预算，不重读 embedded；文档服务拥有验证、来源、watch 和 last-good。首次注册时若有独立 `Styles.xml`，它明确覆盖 inline Styles；否则完整布局内 Styles 是权威。无作者目录的旧三参注册保 embedded／无 watch。

游戏和独立构建共用纯 state、roster、帮助预算、widget 与 binding 源码。`US_PREVIEW_NEUTRAL` 隔离原生引用，p2 专属 region／语义 API 只编入 `US_OFFLINE_WORKBENCH` 用法。普通游戏 Create／PreparePage 未加入 p2／Tools 前提，已分别用 p2 net472 和保留 r6 carrier 编译。枚举仅迁到同一生产命名空间的中立文件，值与类型身份不变；原 Log caller 的 Compile 清单同步迁移，没有新增 case。

## K4 仅缓存记录正文

`SqueakResponseEvidence` 新增对象内 `cachedText`：首次 `Text()` 创建字符串，之后复用；没有构造／捕获时主动格式化。

| 内部变更 | 失效路径 |
| --- | --- |
| 构造时初始化标量、复制 observation 并加入默认字段 | 第一次 Text 前完成；字段加入走 Add |
| Add 增加字段 | 字段加入后清缓存 |
| Add 的值／key 超长 | 先走 NoteTruncation，再加入字段并清缓存 |
| Detail 成功、累计明细字符／项数 | 走 Add；计数只控制下一次明细预算，不另输出 |
| Detail 超明细项／字符预算 | 走 NoteTruncation，清缓存后更新截断计数／名称 |
| NoteTruncation 已达到 16 个名称 | 仍清缓存并递增 Truncated，省略名称数随下次 Text 更新 |

其他字段为构造后只读；实际源码没有外部修改 Fields backing list 的调用。缓存只有一份字符串，与原证据对象同寿命。原 64 条 capture 容器覆盖、ClearCapture、game/session 释放不增加任何持有者。

`TechnicalCaptureText` 继续每次组合动态状态头、读失败与时间字段，再从最新记录向前调用 `Text()`，最后加 PreRecording。复制反馈仍走原白名单、最新优先和 UTF-8 上限。ShowSeconds 与查看 B／录制 A 的 owner、缓存 key 和失效规则未改。

采用原因是周期 snapshot 推进 revision 后，稳定历史记录仍可能再次格式化；两个详情窗也有独立读侧缓存。这里消除可重复的纯文本工作，尚无真实 capture 耗时、分配量或 FPS 收益测量。K5 的 Root.Update／Pawn 扫描排序保持延期。

## 紧凑行沿既有 Schema2 收敛

检测结果：C# UiKit Schema2 与原生绘制；没有 Web／Tailwind；使用现有 per-host theme、30 个语义颜色角色与 XML scheme。正文 Small/16、标题 22、技术细节 Tiny/14、控件 30 高及既有间距仍为约束，不换设计系统。

下列小范围修正已在同一真实 Host 4 比较旧 u1 XML 与当前 XML，并读回实际几何和 PNG。c1 尚将数值字号回退到原生 preset；正文声明仍为16，不能据此称像素字号16已验。动作名称／scope／有效值／展开保持原父子身份路径和 Repeat key；没有移动高级字段或草稿节点。

| Lens | 严重度 | 位置 | 规则 | 现在 | 拟收敛 | 规模 |
| --- | --- | --- | --- | --- | --- | --- |
| hierarchy | P1 | b-action-line / b-action-controls | §2.1、§2.4 | 原来短动作名与整组控件均无声明宽度，会均分父行；控件组很早进入纵列 | 动作名使用现有 120 列宽、160 上限；其余空间交选择／有效值／行尾展开，辅助值仍 Muted、正文仍16 | S |
| layout | P1 | b-action-controls | §3.6 | 原组内500断点即使外行够宽，也会将选择、有效值与展开分成纵列 | 组内断点改360，保外行620折行；已验644宽正常单排，560宽先折动作名、340宽再折控件组；保文本字号声明与原身份路径 | S |

结构、真实字体、语义操作、物理输入和游戏证据分层记录。FL负责同屏／F12式树、真实声明／已解析约束／几何及选中框，US消费；本区不另造检查器、watcher、last-good 服务或 runner。

## 当前证据入口

独立输出位于 `dist/offline-workbench-20261106/`，不覆盖 r6。`independent-r1` 保存 source adapter＋K4 的首轮结果；后续共享字段与隔离 source 改动使用可区分目录。`provider-u1` 是已交 FL 的固定编译包，保原布局快照；当前两处紧凑行修正已纳入 u2；c2原工程候选保留；r3独立候选是 `candidate-us-c3-dev/`，包含u3及完整新US Dev／原r6 FL Dev配对，未写回覆盖 u1。每轮具体命令、输入、结果与适用范围见日志和指定 A001 artifacts 的独立阶段记录／`PROVIDER-U1.md`。

r6 的真实操作、连续 Report／32-pass、1.6 native 及 1.7 缺口保持原记录。当前 source 与正文缓存检查不替代这些证据。

## 准确5000实际闭环与交付边界

`real-c1-us-r1/evidence/` 保存 u1旧作者副本→当前紧凑XML的同 Host4比较。窗口1320×900、预览900×840、b-actions644宽不变：首行164→88，普通行124→48；首行多出的分组标题保留。名称120、选择220、行尾按钮100，其余交有效值，长有效值可自然换行。窗口1020时预览600、内容560，先将名称折到上一排；窗口800时预览380、内容340，再将内部控件组折为纵列。没有用拉宽窗或缩正文掩盖问题。

同 Host 实际语义读回：scope关闭为 typed `Disabled`，恢复自动为 typed `null`；高级音量 `SetDraft=0.75` 时 model仍空、effective仍继承1，CancelEdit清草稿且不改model。Race／WorkbenchPawn 的 Work选择 `us.offline/quiet` 后，来源显示已指定／最后有效快照可用，音量继承0.65。收展使用原Repeat/绑定。watch样式RowHeight30→32生效，坏XML拒绝且Host definition留3、hasLastGood=true、usesEmbeddedFallback=false；修复后definition4，resize/prepare仍保外置文件与120名称。Draft名称mount33、Undraft收起按钮mount808跨样式／恢复保留；域切换则按原含域Repeat key退休旧节点。opaque操作目标随重连／definition变更重新capture，不承诺跨寿命复用。

选中节点、父约束、scroll/clip和实际框由固定Tools原检查器提供，`selected-outline.png` 已看；US未另造F12检查器。c2的u2／m2也已在准确p3 native17实际载入；其独立PNG／tree在 `real-c1-us-c2/evidence/`。

### c1历史性能读数（只对当时输入有效）

复用Tools `samples/start`，稳定watch关／稳定watch开／watch热更新窗口各32个真实完成pass。每组仅在有仪器连接时取样；初始 `instruments-idle.json` 为events空、geometry=null/NotSampled，断开后Host仍沿原生回调前进。watch是opt-in，关闭不轮询作者文件；关闭连接释放仪器；这不是游戏Root.Update或真实capture成本测量。

`performance-summary.json` 保留实际draw汇总：watch关0.443885ms、watch开0.5368383333333334ms、热更新窗口0.4610083333333333ms，各为保留下来的60-sample聚合，不是32帧逐帧均值。数值字号fallback填充事件ring，三组最大dropped分别13794／91124／216546；不能据这些不完整历史比较watch增量或独立reload成本。hot reload虽已成功提交，采样不分离文件读取、投影、prepare和draw。`cost/read`为Unsupported/no-provider，显式请求未attempt即拒绝；没有冒充原游戏成本provider，也未新建性能平台。K4重复正文格式工作被消除的依据仍是源码与既有检查；K5实机成本unknown。

### c1当时缺口及r4接续

c1对正文Small/16报告Small/preset及Label不支持，作为原输入失败保留。r4已实际接入p5/c4：Label requested/effective/paint为Small/16，原1.7合同允许的Editor仍Small/preset；不把Editor精确数字升为新阻塞。新四组32不同pass、dropped均0，保原60-sample arrange/draw汇总，详candidate-us-c4/evidence/PERFORMANCE-P5-C4.md。物理键鼠／IME、准确1.6游戏、r6连续Report／32-pass等原门保持，不由独立帧代签。
## DSP r3：只收紧空来源行，另交完整Dev配对

已接受的expanded帧中，Pawn总体层source-options为空、来源操作targets为0，但b-source-controls实际仍占20,351,628,30，说明在y387。新增只读source-controls-visible沿原SourceChoices.Count；仅当真实选项为空时隐藏该Row，来源标题／说明／错误、原绑定／owner及字段Repeat key不动。c3实际修后说明y351，上移36逻辑单位；合法Race层保两个选项和300×30下拉，选us.offline/quiet后读回已指定／最后有效快照可用。修后Pawn／Race PNG已看；音量草稿0.75时model仍空，取消仍空且modified=False。

c3游戏候选使用最终当前B源US_DEV构建，真实csc含TRACE;US_DEV;DEV；完整配对原r6 FL producer-r5-dev（FER_DEV，MVID8e0cf5d2-7b38-4cb8-9cb5-6f8c8035e82a），没有把p2 Release重标Dev。US新MVIDc19490fa-d346-4588-b98f-70640eeef28f。原DevPanel、Settings/确认Capture、独立Outline、Report、原位32-pass、诊断主窗专属outline与正常Kernel诊断/录制源码及门均保留；最终配对既有LayoutDiagnosisIsPerHost17、OutlineIsIndependentOfCapture10通过。既有完整575按未变源/输入继续复用，其余native／Report连续／32-pass门不由此关闭。

完整模组根在candidate-us-c3-dev/game-dev-pair，Tools及准确5000库另在独立预览用途，不混入游戏载体。指南区分独立黑画布与游戏Window底色、外框及观感。已知准确源码参考仍为5000；未取得可认证1.6的两方法路径，不新增工具／扫描，保1.6世界投影／UIScale／遮挡／输入门。r3当时等待Small/16和ring修订，未重跑性能；r4已只补过受影响切片，原观察与失败保历史。
## DSP r4：p5/c4与人类入口已接续

当前最终工程候选candidate-us-c4使用固定tools-native5000-c4＋producer-p5-native5000/native17（MVID04617bc7-24af-4432-8d62-32f12edb1c92），不混c1/p3。u3仍为p2 neutral编译的同一DLL，m2及c3新US_DEV／原r6 FER_DEV配对原样带入，未因包名重编或重跑有效检查。

实际最终页Small/16标签和允许的Editor preset已读回并看图；真实数值字体下644宽首行90高、名称120／控件组518，结构仍紧凑。四组各32不同真实pass完成、最大dropped均0，原60-sample arrange/draw保留；热更新组有reload.accepted且全部Host提交。稳定watch历史不增长。有限性能只报实际Host／进程层，reload峰值和未知维度见随包摘要，不宣称FPS或隔离watch增量。

仓库scripts/start-offline-workbench.ps1与包Start-UsWorkbench.ps1默认可见，仅显式-Background追加原Tools --background；helper可Hidden，Engine HideWindow只由flag决定。QUICKSTART／USE先让人可见打开、选树／改XML，再attach同一窗口，不含用户需CO批准的限制。本轮所有实际启动均显式Background，默认可见仅核源码参数链；未占前台／鼠标。独立黑画布仍不等于游戏Window底色、外框或准确1.6观感。