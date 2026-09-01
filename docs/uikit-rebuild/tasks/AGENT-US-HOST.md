# 执行代理：Universal Squeaker Settings Host

## 目标
在 UiKit kernel 与 core widgets 验收后，重写 US 设置页为单一 Settings Host；保留业务 model 和纯逻辑，删除旧页面编排、重复状态、视觉 shim 与二次命令翻译。

必须在 kernel 与 core widgets 验收后开始。前置是行为契约和可用 Kind，而不是某组固定类名；若实现者提出更安全的垂直迁移切片，主代理可以调整顺序。

## 文件 ownership

## 默认 ownership

以下文件由本代理负责，防止并发冲突；若实际依赖要求新增窄辅助文件或重新分配，先与主代理同步，不要被清单机械阻塞：

- `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/VoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/VanillaVoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/UniversalSqueakerSettingsWindow.cs`
- `Source/UniversalSqueaker/UI/UsWidgetRegistrar.cs`
- `Source/UniversalSqueaker/UI/Layout.xml`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`
- `VoicePacksPageState.cs`、`VoicePacksViewState.cs`
- US composite widgets and their components, excluding `CameraIndicatorWidget.cs`（归 Overlay owner）
- `Source/UniversalSqueaker/UI/Widgets/UsHelpPanel.cs`
- US visual forwarding files；迁移调用点后再删除 command/metric/visual shims。

不要求逐文件逐行迁移；只要求最终调用图收敛为单一 Host 和单一事件路径。

迁移调用点后可删除：

- `UI/Widgets/UsWidgetCommandAdapter.cs`
- `UI/Widgets/UsCommandPayload.cs`
- `UI/Widgets/FerriteTextMetricsAdapter.cs`
- `UI/Components/UiPalette.cs`
- `UI/Components/SectionFrame.cs`
- `UI/Widgets/UsWidgetDrawing.cs`

## 目标不变量

1. 单一 Settings Host 在创建阶段验证 manifest、Kind、静态属性和 typed binding；一个 Window 一个 session，关闭释放、重开新建。
2. Basic/Tuning/Packs 的现有业务能力和布局目标保留；动态列表仍由 C# 复合组件处理。
3. 控件结果直接进入 US 业务类型或一次性 Host action 边界；不保留双重字符串命令翻译。
4. scroll、popup、搜索、focus、drag、fallback 由 session 管理；业务 model 继续拥有持久数据和业务选择。
5. 统一 footer/metrics/窄屏约束，并以真实消费者验证 ScopeTree、chart、dropdown 等高风险路径。
6. Vanilla 页可以继续作为明确 fallback，直到新路径完成游戏内验收；之后 clean cutover。
7. 重复视觉 shim 在调用点迁移后删除，不形成长期双实现。

具体迁移顺序、是否短期保留某个 adapter、以及如何把业务 model 映射到 typed binding，由本代理根据源码和 focused check 选择并记录。

## 验收

- 无真实游戏先通过 US Dev/Release build 和现有门禁。
- 游戏内 800×600、1280×720、1920×1080 验证首开、关闭重开、Basic/Tuning/Packs、dropdown、stepper、chart、filter、fallback、翻译和低分辨率布局。
- 记录所有迁移/删除文件与残留调用点；没有兼容 shim 或第二套事件框架。
默认不运行 formatter、lint、build 或项目级测试，除非主代理授权或需要最小 focused check；验证由主代理在 gate 统一收口。