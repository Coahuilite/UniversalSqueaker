# Design — 真实生产 F09 的来源闭包与三条执行边

状态：**已采用的消费接线设计；r3实现候选待验证**；唯一规范正文为 [UI-10](../../specs/ui-consumption/spec.md#requirement-ui-10-生产-f09-显式保留准备原生操作保持实时)。依据 WO-FL09-RETAINED-DISPLAY-20261111 r2、原 SCOPE-GATE／NATIVE-C 与 PM 核定的 FL `bb7da49`；代码基底仍为 US `1ee2ae0`，原 FL0.9 工厂只读基准为 `d07cdc0`。本设计不定义 FL 公共接口，也不等待产品提交才建立消费保证。

## 选择：一份生产数据准备，加独立 native/UI 变化边

不把整页普通 getter 宣告为 pure。采用位置是 `UsKernelSettingsHost.Create` 中的具体 `UsKernelSettingsSource`／`UsSettingsBehaviorPageSource` 和 `BindBehaviorSheet`；以每 Host 的生产 F09 准备 owner 组合下面的实际来源。外来 `IUsKernelSettingsSource`／`IUsBehaviorPageSource`、未知 widget 和未采用页面不取得该资格。

保留原 `Layout.Schema2.xml` 的 `b-actions-scroll → Repeat b-actions → b-action-row`、canonical／贡献 fields、原 action/domain/field 地址、注册和编辑器身份。`UsSparseFieldWidget` 在该分支按实际 capability 接入已选定的可选 split 消费能力；原 `IUiWidget.Draw` 的同步含义和无 opt-in 工厂仍保留。不能把整个 F09 搬成另一个自绘 widget 来规避身份、首次 materialize 或真实字段。

### 1. 数据／几何准备边

生产数据准备 owner 在每个合同规定的合法边界观察 root 输入头／必要原始值；只有相关事实改变时才构造 immutable 展示数据、字段分组、formatted own/effective、measure／geometry 与展示描述。记录成功后未变字段不再走原逐字段 `LabelBind/EffectiveBind → CompactLayout`、Measure／PointerRegions 派生或 widget 展示准备。

`BuildView` identity 只是闭包的一部分：它检查 content revision、configuration ID、Definitions 和 CurrentPolicy；`ReadEffectiveFields` 又依赖此 view。`rowResults`、raw source XML、公开 page state 和原生 draft 不全在里面。不得仅包一层 view token。

**完整来源的具体边界：** US 自有写边提供自动变化凭据；仍可由旧公开可变记录／state 直接影响原读面的输入，在合法边界作一次真实 root 观察／值比较，并保独立 immutable capture。对可变玩家记录需要覆盖所读的嵌套值与成员，不能只看 List 引用／Count。变更后才 fold／格式化；观察成本仍计入准备，不伪称零成本。这样不要求外部调用者改成 Notify，也不需逐 21 个字段执行展示 getter。完成该观察后才可把数据头作为正面资格；漏一条旧可见输入，该子域就不封闭，不能带旧 token 复用。

不可变 choices 与当前 value 是两个独立的只读 snapshot 通道，可使用同一 key；选项的成员／顺序／caption／help 随来源进入对应 root／item。纯准备只取明确映射的完整 snapshot，不用普通 Get／Choices 补项。缺项／错型按定位失败处理，不能借普通 getter 隐藏错误。来源观察仍按真实父可见性、tab／Repeat 活动域进行，不在父隐藏后探读旧子 getter。

### 2. 必须运行的 native/editing/hit 边

FL 原生 owner 从保留的控件描述／几何执行必要 native 操作，而不是重新跑每个 US widget 的完整 Draw 才发现无变化。操作仍取当前资格、draft／IME／focus／conflict、按键、模型读回、help、popup／capture 与 hit／scroll。确认／Live 的 readModel 仍读原当前 owner，不能把 prepared 旧字符串或 request 当作已采用值。

US 数据不变不能替代这条边。FL 需要能将本地 UI 变化及时归属到对应 field／作用域：例如只有活跃 editor draft 变动时，改变该 field 的反馈／错误几何，不让 US 轮询全部 widget 的 observedEditor。本分支消费 FL 已核定的 editor／native 局部失效及 participant 能力，类型／签名与兼容等级只由 FL 维护。即时资格、业务读回、请求和未映射普通读不切入纯表示通道。

### 3. Repaint 提交边

以当前成功 prepared data 和当前交互／geometry 提交应呈现内容。Verse IMGUI 可以复用准备结果，但仍须实际重画；不能默认有 GPU retained surface。数据、native operations 与 paint submission 分开计数。后端不具备减少实际提交的能力时，仍可获得前两条中合法的准备复用，不把它假装成少画。

只有有效 clip 为空，或全部实际绘制范围已被证明／强制裁尽，才延期该纯 Paint；自定义 widget 的 layout rect 不自动界定绘制范围，不能证明就照常 Paint。输入／native IDs／帮助等原操作和几何所需首次准备不随其省略；滚入首个适用 Repaint 实画当前完整内容。

操作使相关准备失效；按原同轮一致／下一合法同步采用新业务表示，不能让较晚 sibling 混入半份新源。editor 当前 draft／caret／IME 仍即时显示；Repaint 前已发生的错误／UI 状态在该次同步采用，不拖到第二个 Repaint。手动 MeasureAndArrange→Draw 保同一成功版本，间隙新失效待原下一合法配对，不私插排列。重准备不重放 native input／命令；捕获／Build 失败不得改成 Unknown 或成功凭据，沿原故障／恢复，不能用不相符旧几何提供可操作命中。

## 实际变化归属：给 FL owner 的有限事实

| 实际来源／原入口 | 拥有变化及生产凭据的责任 | 影响的最窄结果 |
| --- | --- | --- |
| `GameSettings.CaptureBehaviorConfiguration().Id`；`UsKernelSettingsSource.BuildView`／`ReadEffectiveFields` | 配置发布 owner；source 验证发布 identity。原读面仍读取的 raw own／可变记录须 root 值观察，不以 published ID 掩盖缺口 | 准确 action/domain 的 own、effective、继承／显式值、支持／暂停原因；变化才重新投影 |
| 已发布 Definitions／BehaviorSourceDirectory、CurrentPolicy、roster/catalog 与 Explain 所用 implementation availability | Definitions 发布后 RequireBuilding 禁止同对象继续登记，directory 复制封存，policy 是 immutable input；source 捕获这些发布身份及实际可用性变化（现有可用性若无发布时钟，边界观察其所需有限状态） | action／贡献成员、条件、来源候选和语义。不能把外部任意作者回调纳入无凭据封闭域 |
| `SetActionExpansion`／直接 `SetTuningSelectedAction`、TuningLayer／Race／Child，以及公开 `ViewState` | source／页状态 owner；边界读取真实 selector 值，不只看 Bump。保留 action/domain/key 身份 | 选中态、visible domain、header／footer 和当前贡献 fields；首个新成员首次准备并呈现 |
| `rowResults` 私有字典：`Voices.cs` 解析失败、提交成功／拒绝结果；`Source.cs` 行恢复结果 | **source 自动推进结果失效，覆盖全部三处写边，包括解析失败的早退；只在值变化时失效**。旧公开调用无额外通知 | 对应 footer 的 has-row-result、文本及高度，不能无端重建 21 个字段 |
| `behaviorSourcesXml` 的 TryReadSource；`ViewState.SourceChangeDiagnostic` | source 边界观察当前 XML／准确域／结果；字符串相同才可复用解析。配置 ID 不是此 raw 读面的替身 | 来源选择／状态、拒绝原因及相关分支 |
| `BindBehaviorSheet` 的 contextControlsOpen、TuningActionRow.Details／TimingKind、发现的 Fields／retainedKeys | 本 Host sheet owner；这些是私有闭合写边，自动使相关准备失效；旧 Bump 仍合法 | 本地可见组、技术详情、timing 选择、字段集合；未变集合不逐项重建 |
| `UsSparseFieldWidget.observedEditor`；原 FL `DriveEditor`／UiEditSession | **FL native/editor owner**及时给出局部 draft、valid／modified、focus／IME、冲突、确认／取消变化；US parser／错误文案保持原义 | 活跃字段的 draft／标记／错误高度，独立于业务 view。数值 live:false，dropdown 和已有 Live 不变 |
| scroll、clip、window origin、capture、popup、资格、help hover | FL 交互／scroll owner；原 `CanExecute`、exact-domain setters 与 help 发布保持当前，不缓存业务命令结果 | 当前操作 rect、hit／help、合法夹限；滚动不必重新格式化所有字段，屏外不许可删 native 操作 |
| width、breakpoint、scrollbar、theme／font／metrics environment、translation／格式文化 | FL 几何／后端提供当前环境；US 原 UsKernelTranslation／资源提供译文。unknown environment 走原鲜读条件 | 只失效对应几何或 paint。颜色与布局分开；语言／字体改变不得将新长文塞进旧空间 |
| author definition／template、widget schema／注册及 binding backing 替换 | FL definition／registry／binding owner；新可选能力要重新验证 source／widget 资格，US 不以旧 view token 覆盖替换 | 新节点／工厂／声明重建或原 fallback，不向未知 widget 传旧成功凭据 |
| hide／重开、key 删除、Host／window close、source 替换／故障 | 原 Host／session 及 source owner；关闭／替换退役本作用域，隐藏保兼容节点。读失败不承认新成功凭据 | 不串草稿、命中或结果，不回放提交；释放 UI 订阅／描述，不 Dispose 业务 settings／runtime |

### 已证反例不能由复用吞掉

`UsKernelSettingsSource.TryCommitActionField("volume", "not-a-number", "us.core/Draft", "", "")` 在解析失败分支写 rowResults 后直接 false；没有清 cachedView 或 Bump。模板 `b-row-result` 以 has-row-result 控制可见，占实际高度。它须通过 source 自有结果失效被消费，而不是让调用者补发通知。

`DrawCompact` 原来在 `observedEditor.Valid` 改变时 Invalidate；`CaptureFieldInputs` 又把 Invalid/Error 计入高度。数据 view 不变仍可改变 draft、标记与几何。若 FL 只给 model revision，没有该 UI 通道，不能声称生产 F09 的闭包完整。

正面停手路径：完成一次成功准备后，root 观察的真实输入、source/sheet 结果凭据、editor/native 状态与布局环境都未变；21 个已知字段不再重新读取展示 getter或派生几何，仍执行后端要求的 native ID／编辑／命中和实际 Repaint。root 观察与 native replay 有真实成本，须按外层帧口径测量。

## 真实1.6接线及r3迁移

- 真实工厂：`SettingsWindowOwner → UI.GameSettingsWindow : UiWindowHost → CreateHost → UsKernelSettingsHost.Create`。该方法注册真实 widgets，用 `VerseUiDiagnosticLog.CreateHost` 建同一 manifest／bindings 的 Host，并 attach source／Bumper；不从 offline region 或另一个 US 工程造替代页面。
- 此 US 项目固定 net472/x64、Krafs.Rimworld.Ref 1.6.4871、Lib.Harmony 编译引用，FL 引用 `Private=false`。显式 `FerriteLibArtifactPath` 经 Directory.Build.props 解析；原 RecordFerriteLibReference 把精确引用收据写入 US。
- FL0.9 `d07cdc0` 项目：VersionPrefix／API 均 0.9.0，**UiKitTarget 空值是 net472／RW1.6**；Neutral 和 RimWorld17 是 net10.0，不能拿来作此载体。普通候选由 FL owner 用 Release、空 VersionSuffix、无 FerriteTestHooks 产生并固定实际 MVID／参考身份，再交 US 显式引用。
- 该 FL 基准仍有 RimWorld16 的 `UiWindowHost.CreateHost`、`VerseUiDiagnosticLog.CreateHost`／UiHost 工厂及原版本 Require 形状。已选定 retained 能力仍须由实际 adapter 声明并实现；消费规格与源码现已接入，但准确载体构建和实际新路径接受仍分别供证。
- r3仅在本分支把 `GameBootstrap`、`scripts/resolve-carrier.ps1`／readiness 迁到 **[0.9.0,0.10.0)**；原编译引用收据与显式DLL选择保留，不在旧分支放宽成“两个都行”。About／LoadFolders只声明本次1.6载体；独立预览仅补共享源码的直接Compile连接，不在此签net10接受。
- `About/About.xml` 实际硬依赖为 Harmony 与 `coahuilite.ferritelib`，loadAfter 同步；ModRequirement 不能表达 API 版本，真实范围由上述 code guard 拥有。LoadFolders 的 1.7→1.6 只是旧 basic candidate，不给本任务 1.7 接入／验收许可。新分支真正包装前明确其 1.6 支持／依赖描述，勿把 0.8 对和 0.9 对混装，US 始终不打包 FL。
- 原普通 Release 入口：`dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release -p:FerriteLibArtifactPath=<fixed-FL09-net472-DLL>`；其后原 Host 工程显式引用同一 FL／US，跑实际相关 `ordinary getters`、`manifest`、`dynamic` 和 shell／editing／diagnostic 范围。现有 harness 叫 FerriteLib08.Stubs，包装 guard 仍为 0.8；先在本分支采用真实 0.9 签名／工厂映射再用，不能改名就签通过。无新单测／等价 probe。

## 已选 FL capability 与实施门

PM 已选 FL 的 FL09-REGION-SOURCE／ENROLL／UPDATE／PARTICIPANT／LIFETIME 与 FL16-REGION-PAINT：来源映射、原 Host 区域事务、局部纯结果／调度复用、Build／Operate／Paint 分界、故障／退出及1.6实际提交。FL 唯一规范归属分别为其 binding-reactivity、structure、layout、presentation-execution、lifetime-recovery 与 adapters/rimworld16 正文；US 不复制签名、另创接口或放宽等级。两项采用澄清已进入本库 UI-10。

r3已放行。US候选用 `UsBehaviorDisplayCapture` 在真实工厂登记：`ObserveBehaviorDisplayInputs` 比较完整raw玩家行／嵌套成员、已发布配置／目录／policy／可用性与真实域；source自有 `rowResultRevision` 覆盖全部三写边，sheet自有选择／details／timing凭据。未变CaptureCurrent返回同一包，不逐字段投影。更改候选只有完整捕获成功后才确认；不可达项／field／details不读旧后代。普通Release／实际MVID／准确FL编译引用及原有consumer lanes由工程交付记录，不以API-only fallback载体签RETAIN或native。

### 直接调用、切线与同代 off

生产调用仍为 SettingsWindowOwner → GameSettingsWindow.CreateHost → UsKernelSettingsHost.Create → 原 manifest／bindings／b-actions。只在具体生产 source＋sheet、已闭合映射及实际后端能力处加入区域；UsSparseFieldWidget 的已选纯准备与 Paint 从原 Draw 拆出，原操作／editor／help 留在原顺序，完整 Draw／Measure／PointerRegions 继续可用。direct ReadActionField／TryCommitActionField 仍直接进入原 source；失败早退与未 Bump 由该 owner 自动观察／失效，不加旁路入口。

实施验收的 off 是**同一固定0.9 FL／US、同一真实窗口／字段／业务条件**，只不登记本次区域 attachment，或按 FL 已采用退出能力安全解除并重新验证原完整路径；默认未登记并不省掉原 ordinary／sparse 调用。由获授权 owner 在真实工厂采用边控制该配对，固定方式随工程收据交付，不为它新增玩家开关或假菜单，不改版本检查。同一次采样不得混入挂接／退出过渡帧；输入／关闭等功能路径单独记过渡结果。候选已在同一私有 `CreateCore(..., retainBehaviorDisplay)` 留下不登记边；公开工厂默认采用，参数不是玩家开关。真实窗口的source另持私有 `behaviorDisplayAttachment`，运行owner可按FL公开Dispose合法退出后完整验证；不把未运行的切线当native通过。

### 同代端到端验收

Terra 以 165 FPS 的整帧 6.060606 ms 为目标，使用同代 0.9 **on/off** 或功能等价固定基线，原真实 F09 开合与展开停手，既报 per-scope／per-field preparation 次数，也报 native 操作／paint 提交、首次实画、Host／整帧及超额分母。覆盖 silent row-result、未提交 invalid draft、choices、Live／确认、父隐藏、滚动／重开、环境／语言和关闭；复用原脚本和证据，必要原生反例归原运行 owner，不新增单测框架。此前 A／C-r2 只作历史机制证据；r2 所引新 IMGUI 分摊来自独立60Hz现场，不与较早144Hz混为一组，也不作新0.9对照。同代对照按实际读到的节奏配对、不改显示设置；Core次数、模拟值、60／144Hz结果均不能签实际165Hz。

## sparse 的本次边界

UsSparseFieldWidget.Build只读快照、当前环境及已持有editor事实，共享自然高／局部pointer／标签和错误布局；Operate保原即时模型／choices、native editor、underline、帮助与ID顺序，Paint只画prepared文字。原Draw／Measure／PointerRegions不删改。此次不声明PaintClip：自定义文字实际输出范围没有额外证明时照常Repaint，不以layout rect跳过。这个保守部分仍有实际绘制成本；新区域的工作减法须由完整FL载体与Terra同代计数确认。
