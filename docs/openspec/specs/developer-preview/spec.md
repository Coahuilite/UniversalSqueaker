# 同源开发预览与工具边界
状态：现行，US0.6.x的开发辅助合同。区域预览已有实现；完整窗口为已批准待实施目标。各消费线的实际覆盖见[实现状态](../../../development/specification/open-issues.md)，不把一线的成果自动算作另一线已交付。

## Purpose
让人类和agent在游戏外迭代正式UI，减少为布局差异反复启动游戏；保留原业务入口、数据含义和游戏内诊断。

## Requirements

### Requirement: PREVIEW-01 正式页面与隔离样例
预览 SHALL复用正式XML、模板、Styles、主题、Keyed、控件注册、绑定、字段解析与状态组合，不另画简化页或硬编码近似业务。样例来源显式说明；已具备真实Core定义／默认值／有效值解析的页面复用这些生产逻辑，样例不冒充加载存档的完整目录或真实Pawn状态。
预览数据边界和当前区域构造API为public-unstable。正常Settings源、提交／保存owner及合法原入口 SHALL保持。样例只更新自己的内存，不接真实Settings单例、文件store、runtime publisher、音频、Pawn／Map或存档。缺少真实确认／导航宿主时准确Unsupported，不自动确认或假报保存。
#### Scenario: agent提交一个样例值
- **WHEN** 在独立页面完成合法字段编辑
- **THEN** 读回该样例实例的新值，真实Config和运行时发布不变；拒绝与无效稿走原解析路径

### Requirement: PREVIEW-02 从区域预览扩为完整同源窗口
完整预览 SHALL从正式窗口构造与寿命接缝进入，包含外框、标题、关闭、三组导航、页签、正文、帮助、页脚及相应弹层；使用虚拟游戏视口提供的逻辑空间、有效UI缩放和原生绘制链。Tools不拥有US页签、字号、业务断点或另一个GameSettingsWindow副本。
样例允许到达正式页面的布局状态；游戏依赖动作保明确不可用原因。页面切换、窗口缩放、关闭及重开沿原会话／节点寿命，不通过重建整个窗口假装热更新。观看Fit／平移不是US布局输入。窗口所需窄接缝由US提供，中立缺口交FL，不要求虚拟地图／游戏world。
#### Scenario: 在窄游戏视口中切换调音页
- **WHEN** 完整窗口按UI-07预算采用窄编排，再切行为与输出
- **THEN** 显示正式导航及原页面，兼容上下文和草稿保持；工具检查器开合不改变这份预算

### Requirement: PREVIEW-03 热改与原编辑路径
完整作者文档 SHALL经同一事务选择区域／模板／Styles，失败保last-good全文与定义；成功后工具来源、页面和语义读取指向同一有效修订。临时布局输入与XML保存分开；C#改动需重建。
采用prepare的preview SHALL仅在同Host显式owner边界准备布局，初排与语义后重排用原预算函数，读取不prepare；普通BeforeDraw已准备的路径不双挂。custom数字字段的agent操作 SHALL使用普通Edit同一Controls／UiEditSession、formatter／parser、Live政策、typed request及readModel。SetDraft只设本地稿，Commit一次原请求，无效稿保留纠正，Cancel还原当前模型且不写业务；描述读取不创建editor；未有editor与已观察的draft／valid／conflict分开标明。操作能力不等于已采到native焦点或IME；原owner在执行时继续核当前输入资格与目标寿命。
#### Scenario: 热改时字段保留未提交稿
- **WHEN** 作者保存兼容布局并reload
- **THEN** 同Host与兼容字段保draft／展开／滚动；被移除节点失效，旧opaque地址不复活

### Requirement: PREVIEW-04 UiKit工具与US诊断分工
UiKit检查器、布局描边、工具快捷键与agent传输 SHALL归独立UiKit Tools；新1.7消费线不内嵌其工具UI或保空壳。0.8／1.6维护配对已有Dev入口保原条件，迁出须完成替代入口与恢复路径，不能由规格编辑删除当前验收能力。
US Kernel诊断、查看B录制A、技术全文、反馈复制、业务日志和US自有只读屏幕标记 SHALL留在US，按DIAG合同工作，不要求Tools。屏幕标记样例复用同一控件与作者定义；仅提供绘制样例，不声称已覆盖世界投影或真实Pawn生命周期。
#### Scenario: 用户没有安装UiKit Tools
- **WHEN** 玩家打开普通US回应诊断并录制A、查看B
- **THEN** 原诊断可用且录制对象不变；工具缺席不禁用业务诊断

### Requirement: PREVIEW-05 性能观察不复制业务采样器
可选工具成本provider SHALL只投影原窗口sampler，有限请求沿原gate／queue／32 Host pass及关闭cleanup；读取不启停或推进采样。没有原窗口sampler的区域预览明确Unsupported／nullable，不造另一个采样owner。稳定页读取、展开／收起、热改更新期和工具观察成本分别说明；原pass非FPS，离线数据不代签游戏热点。
#### Scenario: 在独立行为区域请求窗口成本
- **WHEN** 该实例未挂真实窗口成本provider
- **THEN** 如实不支持，不以工具geometry或协议往返时间替代原窗口成本

### Requirement: PREVIEW-06 1.7持续适配与证据边界
新开发 SHALL以RimWorld 1.7为支持目标，随当前namespace／API／资源更新适配，不按小构建号锁定路线。FL的0.8／0.9 API合同、真实加载载荷身份和单一库载体仍须满足；自动输入记录只为复现与诊断。
headless结构、原生脱机图、游戏内窗口／物理输入／IME、Kernel回应与听感 SHALL分层说明。完整虚拟窗口缩短布局迭代，但准确1.6维护线仍需其原生验收；旧证据保原身份。
#### Scenario: 1.7改变窗口接口
- **WHEN** 当前游戏API与旧引用不同
- **THEN** 修订US适配和受影响调用并取得相关证据，不继续用旧小版本锁阻止开发，也不重标旧验证为新验证

