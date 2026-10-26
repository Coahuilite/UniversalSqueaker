# 使用固定 FL 0.8 构建当前 US

当前增量管线联合候选显式配对 FerriteLib `833c14f4dcfd07daaf81532a3d90701888e75c9e`，ProductVersion `0.8.0-local.incremental4+833c14f4dcfd07daaf81532a3d90701888e75c9e`，配置 Release；继承事件映射、Repeat 与后续热路径修复。`704c4c1 / local.incremental3` 是未放行历史输入，不作最终配对。用户正在体验的 A009 r2（US `68b30e3`／FL `36fa7ad`、`local.residualcost1`）及其包／收据保持独立，不改签为新管线结果。原已发布 [v0.8.0-rc1](https://github.com/Coahuilite/FerriteLib/releases/tag/v0.8.0-rc1) 仍为 `a0b5b983c3c13a099c62d936305eaebaf1deb5a8`，本地候选不改变 CI 的维护者固定输入。API0.8.0、AssemblyVersion0.8.0.0、生产 Require 范围 `[0.8.0,0.9.0)` 与 `Private=false` 不变。生产只核 API 范围；完整 SHA 编译收据核对属于 Host runner／stage，不是普通游戏启动门。该输入提供 1.6 adapter，未交付 1.7 adapter；原生性能／视觉、声音和全部十五包装人验仍开放。

## 安装运行前置

独立安装并启用 [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)（`brrainz.harmony`）和配对 FerriteLib，将两者排在 Universal Squeaker 之前。US 只分发自身 DLL，不带 `0Harmony.dll` 或 `FerriteLib.UiKit.dll`；编译引用和 About 依赖声明不能代替实际安装。

## 显式选择交付包

将 `$carrier` 指向固定包的 `1.6/Assemblies/FerriteLib.UiKit.dll`，不追邻库移动输出：

```powershell
$carrier = '<fixed-package>/1.6/Assemblies/FerriteLib.UiKit.dll'
./scripts/build-dev.ps1 -FerriteLibArtifactPath $carrier
# 直接构建同样选择这一个输入：
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev "-p:FerriteLibArtifactPath=$carrier"
```

Directory.Build.props 将输入传给生产与 Host 工程。生产程序集记录实际编译引用，标准 stage 核对该收据和所选 DLL。US 包不带 FL DLL；测试输出里的 FL 和平台替身不是模组载荷。缺少输入、文件不存在或 API 范围不符时拒绝，不回退旧 0.7／fc010bd。

## 注册、访问和项身份分开

- 注册者仍为真实 `UiBindings`；widget 校验、Host 和代理接完整 `IUiBindingAccess`。合法普通 getter 不必换成 tracked 属性。代理的 `BindingOwner / Value / Choices`、能力与读写必须随同一个 backing；已取得的旧 endpoint 仍归旧 registry。
- 声明式下拉注册 `BindChoices<T>`，传 `UiChoice<T>`。不要把 choices 当普通 `BindOptions<UiChoice<T>>` 注册。自定义模型列表仍可用真正的 `BindOptions<T>`。
- 动态重复项的运行时地址由 `new UiBindingAddress(itemsBind, itemKey, leaf).Key` 生成；US 的 `UsWriteBindings.ItemKey` 使用这个入口。点分模板只作声明元数据，不自行拼出运行地址，也不按列表序号寻找业务对象。
- 自绘复合按钮通过同一 Host 的 `ctx.Controls.Activation`，分别给动作、part 和稳定业务项身份。US `SelectionButton` 的 `controlId / itemKey` 表达这些信息；显示标签和矩形位置不是身份。复合下拉／编辑器各用按 popupId／binding 创建的 `ctx.Child`，不让相邻控件取消或消费彼此的捕获和草稿。

## 一个执行器对应一个原生所有者

widget 只接 `UiLayoutRect / UiLayoutPoint / UiColour / UiTextAnchor / UiImage`，通过 `ctx.Drawing / ctx.Controls` 绘制与输入。平台转换留在 Game/UI 原生边界，Core 不增加 Verse／Unity／RimWorld 引用。

窗口适配器已经持有 `VerseUiExecution`；通知条使用其 `Execution`，不每帧重建。直接 Overlay 自己持有一个执行器，每次捕获一份 frame 交给 `DrawFrame(viewport, frame, execution)`；禁用、离图、失败或 Dispose 都先关 Host 再释放执行器，重开创建新所有者。

Xenotype 图像由窗口的 `Execution.Images` 注册为 `UiImage`。无图像作用域的无屏 source 返回空 handle；窗口释放自己的 handle，不销毁 Def／内容提供者的纹理。设置原生壳的单一 opener 保留原批准的树外入口，不复制页内输入系统。

## 帮助的三种身份不混用

`目录 HelpRef → HelpItem.LabelKey/TextKey → Host 翻译 → 实际文字度量／绘制`

XML 的 `HelpKey` 改为 `HelpRef`，运行时 binding 地址的 `VisibleKey / WidthKey / SelectedKey / PayloadKey / ActiveTabKey` 改为相应 `*Bind`；`TextKey / LabelKey` 仍是翻译 key，不机械更名。

当前页头或字段说明打开底栏时显式固定该页／字段的 topic，不取之前的 `Session.CurrentHelpRef`，随后不被 hover 替换；未固定的帮助控件直接读该 session 值，不依赖 `HoveredOptionHelpOutputBind`。`PublishHelp(ref)` 归于当前实际发布节点，`CurrentHelpSource` 保留该归属；后发布覆盖先发布。首 gap reset 后保留 15 个 pass，再过期回到 section 概述，不是秒数，也不是每个原生渲染帧。

四个 mode 明确给各自的 `UiChoice<T>.HelpRef`。空／省略的选项 ref 不发布，不猜 Value、Text、标签或 index；元素一般帮助仍在。未知条目回 section 概述，无 section 时保持原空面板规则。目录 ref 和译文 key 分开，同一译文入口用于测量与绘制。底部帮助与业务页各自保留 scroll 节点，关闭零占位、打开最多三分之一正文预算；收起恢复空间，见[当前 UI 合同](ui-contract-0.6-zh.md)。

## 页面更新，保存与业务身份保留

三个嵌入 Manifest 和一份命名角色 Palettes.xml 数据、准确音频包启用／选择身份、Remix 两步确认、fallback 独立 Cancel 层保持；当前 UIUX 将五工作区重组为语音管理首页与二级工具。三套配色通过一个同源 Dropdown 选择，使用 30 个显式语义角色，只更新颜色；`SelectedBorder` 明确为原 Accent，不恢复未声明 fallback。主题不改几何、scroll、焦点或草稿。

搜索和原即时数值控件保持显式 Live。合格 primary release 请求一次，确认取消零写；已有草稿在隐藏／恢复和外部模型冲突时仍由原 editor 持有。普通 Select／Eat／scope／response 立即发布，并由单一保存 owner 按 0.35 秒 realtime debounce 写盘；真正原子 TryCommit 仍 persist-first，Committed 后的发布修复不重复写文件。详见[公开合同](us-public-contracts-and-help-zh.md)。

固定 `local.incremental4` 将同步验证、候选准备与几何发布串在同一管线；普通 Release 默认使用，不依赖 Dev／诊断页。US 保持原 Host、manifest、getter／legacy Measure 和显式同步调用，不增加 pure／订阅声明前提，也不跳过 Repaint 或原生控件。有效稳态可复用完整结果；定义／成员／尺寸／服务、实际输入、滚动范围及不支持的传播变化仍走必要完整事务，失败与生命周期沿原合同恢复。合法多根页若任一根／子树消费剩余高度，实际增高／缩小时保守返回完整事务，重新分配后根 Fill 预算；idle／等高不触发此保护，不要求作者改单根 XML。

现有 manifest 场景保留原调用序列与断言，用于真实 US Host／嵌入页面的固定输入对照。各次运行证据保留实际载荷身份；旧 `704c4c1` 日志不重标为 `833c14f4` 通过。未观察到的 sparse 候选不以库内小例子代签 US 稀疏修改收益。旧 `36fa` 没有新 `work-total` 字段，不能伪造其 full／copy 前值；该限制与原生日志／FPS 边界分别保留。

## 当前验证入口与边界

`tools/UniversalSqueakerKernelHostTests` 当前入口为 `--fixed-fl`，或 `--lane=<分组关键词>`，例如 `--lane=preservation`、`--lane=confirmation`、`--lane=proxy`。原有检查迁回的入口为 `--lane=existing`（诊断与R4-B预设）、`--lane=existing-GroupFold`（按原诊断方法筛选）、`--lane=roots`（受改旧根反例）、`--lane=old-v4`（原双语真实页面尺寸检查）。调用时同时显式选择 `FerriteLibArtifactPath` 与已构建的 `UsProductArtifactPath`；`UsProductAlreadyBuilt=true` 避免覆写候选。

RC 迁移阶段的 runner 使用真实产品 DLL、固定 FL DLL、嵌入 XML 和真实 `VerseUiExecution`；native 文字／鼠标／绘制由[来源明确的平台替身](../tools/FerriteLib08.Stubs/README.md)提供。覆盖实际确认取消／一次写入、Live／冲突／hide-show、typed choices／无 sink 帮助／15pass、独立滚动／主题几何、列表重排和第二项真实动作、fallback Cancel 和关闭清理、双语长文字／命中。旧根 Program 中依赖私有 UiNative 的旧形状保留源码但不计当前通过；迁回的方法在 Current/ 和显式重纳文件中编入，未逐个迁回其 76 lane，不能以创建／layout 无 throw 代替各行为。原诊断生成页／读面／写入修订／组折叠／窄导航／恢复、预设图像／绘制复选框／独立展开／原回调和双语页面尺寸检查已迁至公开执行链，保原断言。另迁回原scope popup覆盖／类型值、作者显示截断与原token、弹层尺寸／溢出归属、Overlay失败游标及窗口通知／关闭反例。原toolbar／pager／lock／close和预设import的部分旧检查是直接binding操作，只证明原路由／读回；它们不含逐个按钮的原生指针场景，不能凭迁移补称那一层已过。

Core、完整十五合同、CoreWork、通知／四回执、稀疏配置、生产 settings／ExposeData／原子文件与 Scribe 替身往返的未变证据继续按原范围适用，不重录 golden。真实游戏 Config／Scribe、IME／字体像素、屏幕外观、窗口栈、音频／听感及逐包人工验收仍待验；当前 UIUX 已授权改编排，普通政策未改变。旧五区／800×600／140帮助几何检查仍保留，不能直接签新 UI；适用检查按本次候选与输入分别记录。

Release 夹具不启用 Dev-only `GeometryEnabled`：既有控件定位读取公开 arranged 节点与 scroll 状态，原生 press/release 仍走同一 `VerseUiExecution`。目录／fallback 的既有夹具仅在同一执行器外记录真实 text paint 和 `PrepareControl` 分配；该记录不是 Dev input-query 矩形／命中快照，也不扩大原生游戏覆盖。并行的文字／矩形／颜色平台记录必须一起重置，不能用错位索引判定产品颜色。

仅需读取既有场景工作摘要时，可为该 runner 进程设置 `US_TRACE_EXISTING_LAYOUT=1`，再使用原 `--lane=manifest` 入口。它只在原 Page.Draw 调用之后输出已有 arrange timing 事件，不增加页面、操作、断言或测量调用。`work-total` 是同 Host/session 累计同步工作，包含原显式 MeasureAndArrange；相邻值作差，不把它当 GUI 帧、分配字节或 FPS。旧载荷无计数时明确输出 unavailable。正常检查与普通玩家运行不自动启用该 trace。

## 真实 RC 获取与远端 CI 分工

两个 workflow 已移除旧 0.7 固定值，先要求维护者配置 `FERRITELIB_CARRIER_REPOSITORY / FERRITELIB_CARRIER_TAG / FERRITELIB_CARRIER_COMMIT`；空输入在 checkout 前拒绝，源码按完整 commit 获取。既有 acquire 流程仍检查明确 tag 对应的 `FerriteLib-<tag>.zip`、平台 digest、闭集、版本、Release 配置和源码配对，不从默认分支再造 runtime。

实际 `v0.8.0-rc1` 资产已沿该入口下载并完成完整性／Release／源码配对检查。本地以该 Release DLL 跑现有 37 门并标准 stage；本地结果与维护者随后执行的远端 CI、原生及人工验收分开。CI 保留 main，并在 0.6.x push／PR-base 上运行同一 gates job；隐私 workflow 已覆盖所有分支。US 的远端推送、tag 和发布仍由维护者独占。

该 RC 阶段记录不代表本次 `local.incremental4` 跑过完整 37 门。当前已知旧 V4 `Missing content-scroll` 夹具缺口单列保留；本次只运行实际受影响的既有消费者入口，不以其他入口成功覆盖它。
