# FerriteLib.UiKit 重建：棕地到绿地切割矩阵

> 目的：把旧实现拆成可执行的 ownership 边界，避免在不清楚公共契约时继续堆补丁。
> 适用范围：`Source/FerriteLib.UiKit/**`、`tools/FerriteLib.UiKit.Tests/**`、`Source/UniversalSqueaker/UI/**`。
> 本文件只定义切割，不代表本轮立即修改代码。

## 1. 切割原则

1. **先冻结契约，再并行实现**：`IWidget`、`UiSession`/原生交互、`UiElementSpec`/XML schema 是串行门；门未通过不得让消费者自行猜 API。
2. **单文件单 owner**：同一波次内一个文件只能由一个执行者修改。
3. **US 是真实约束，不是旧 API 的兼容理由**：旧 API 可以被删除；但所有 US 调用点必须在最终集成波一次性迁移完毕。
4. **旧实现不与新实现长期并存**：验证新路径后 clean cutover，删除旧布局、旧事件队列和仅服务旧路径的转发层。
5. **不把失败藏在视觉层**：schema、binding、容器和 host 生命周期错误在创建阶段诊断；关键交互控件才使用 session 级 fallback。

## 2. UiKit 文件矩阵

### 2.1 直接保留（仅必要时修订）

| 文件 | 决策 | 说明 |
| --- | --- | --- |
| `Metrics/ITextMetrics.cs` | 保留 | `MeasureText(string, UiFont, float)` 是 US 文本适配 seam。 |
| `Metrics/UiFont.cs` | 保留 | `Tiny/Small/Medium`，由宿主映射到 `GameFont`。 |
| `Properties/AssemblyInfo.cs` | 保留 | 保留测试程序集可见性。 |
| `Commands/UiCommand.cs` | 保留 | 中性命令载荷；不得让 XML 产生字符串业务命令。 |
| `Interaction/UiControlId.cs` | 保留 | 稳定控件身份；继续用于值控件状态键。 |
| `Interaction/UiLayer.cs` | 保留或内联 | 仅当新原生事件模型仍需要分层语义时保留；禁止恢复全局 deferred layer queue。 |
| `Interaction/UiValueState.cs` | 保留概念 | 状态归入 `UiSession`，不得成为进程级静态状态。 |
| `Interaction/UiValueStore.cs` | 保留概念 | 改为 session-owned store；验证 frame reset 不跨窗口泄漏。 |
| `Registry/UnknownWidgetKindException.cs` | 保留 | 创建阶段报告 scope/kind。 |
| `Widgets/UiGuard.cs` | 保留概念 | 重接新 session；保留每 session 去重、GUI 恢复和完整堆栈。 |
| `Widgets/LineChartPointChange.cs` | 保留 | 中性 range graph 变更载荷。 |
| `FerriteLib.UiKit.csproj` | 保留 | `net472`、RimWorld 1.6 引用和 warning-as-error 约束不变。 |

`Registry/WidgetRegistry.cs` 不列入无条件保留：保留 scope registry 概念，但删除静态构造函数自动拉起全部核心控件，改为显式初始化。

### 2.2 UiKit 内核重写

| 文件/新符号 | owner | 必须消灭的旧假设 |
| --- | --- | --- |
| `Widgets/IWidget.cs` | Contract/Widget owner | 从无主题、无翻译、flat context 改为 session/theme/translation-aware；契约先冻结。 |
| `Context/UiSession.cs` | Session owner | 替代 page bag；持有临时交互、scroll、popup、focus、drag、revision、熔断。 |
| `Context/WidgetContext.cs` | Session/Host owner | 增加 source scope、theme、translation、typed binding 访问；不让业务字典成为 XML DSL。 |
| `Interaction/UiInteract.cs` | Native-controls owner | 删除全局 deferred click registry、手工 layer dispatch、末尾二次派发和手工 page-space 世界。 |
| `Layout/UiElementSpec.cs` | Schema owner | 增加稳定 ID、静态属性、translation key、有限 responsive vocabulary。 |
| `Layout/LayoutManifest.cs` | Schema owner | `Schema=1` 升级为严格 `Schema=2`；保留安全解析和节点上限。 |
| `Layout/LayoutEngine.cs` | Layout owner | flat rect emitter 改为受限 Measure/Arrange + `LayoutSnapshot`。 |
| `Widgets/Palette.cs` | Theme owner | hardcoded static colors 改成 Host 注入的 `UiTheme`，0.1 仅 Dark Gold。 |
| `Widgets/UiKitGui.cs`、`UiKitFonts.cs` | Theme owner | 所有绘制读取主题 token，不再散落字体/颜色副作用。 |
| `Widgets/SurfaceFrame.cs`、`UiPanel.cs`、`UiText.cs` | Theme owner | 保留语义原语，改为主题输入；不得增加第二套 panel/layout 系统。 |

### 2.3 组件重写

由 Widget owner 在内核契约和 theme 契约冻结后处理：

- `CoreWidgetRegistrar.cs`：显式 `InitializeCore()`，不在 registry static ctor 中自注册。
- `ChromeBannerWidget.cs`、`ChromeFooterWidget.cs`、`EmptyStateWidget.cs`、`SectionHeaderWidget.cs`：翻译键 + 静态文本 + theme。
- `InputModeCardWidget.cs`、`InputModeRowWidget.cs`：有限 responsive columns；不在叶组件创建结构 scope。
- `SliderNumberFieldWidget.cs`、`StepperSliderWidget.cs`、`DropdownWidget.cs`：原生控件语义 + typed binding + session 状态。
- `LineChartWidget.cs`：稳定 control ID、原生 hotControl 生命周期、拖拽变更；不得恢复全局 popup/draw queue。
- `SelectionButton.cs`、`ModeCardRenderer.cs`：作为主题化视觉 helper，不拥有输入权威。

## 3. US 文件矩阵

### 3.1 保留的业务层

| 文件 | 决策 |
| --- | --- |
| `UI/Model/VoicePacksPageModel.cs` | 保留业务真相；去除为旧 UiKit view dictionary/字符串命令服务的胶水。 |
| `UI/Model/VoicePacksViewState.cs` | 保留只读投影；改由 typed binding/Host 消费。 |
| `UI/Model/VoicePacksPageState.cs` | 仅保留业务页面选择/筛选；scroll、help、popup、focus 移入 `UiSession`。 |
| `UI/Model/UiCommand.cs`、`UsCommandPayload.cs` | 过渡期间转换；最终删除纯二次命令翻译层。 |
| `UI/Widgets/ScopeTreeWidget.cs`、`PresetListWidget.cs`、`VoicePackChecklistWidget.cs` | 保留为 C# 业务复合列表；XML 不实现 Repeat/业务循环。 |
| `UI/Widgets/BasicTuningWidget.cs`、`GlobalVolumeWidget.cs`、`FilterBarWidget.cs`、`AttenuationEditorWidget.cs` | 保留功能，改接新 Host/binding/control contract。 |
| `UI/Widgets/CameraIndicatorWidget.cs` | Overlay 共享 binding/registry 的试点消费者；不得依赖 Window。 |
| `UI/FerriteTextMetricsAdapter.cs`、`VerseFerriteTextMetrics.cs` | 保留宿主测量 seam。 |

### 3.2 必须迁移或删除的 US 胶水

| 文件/符号 | 决策 | 触发删除条件 |
| --- | --- | --- |
| `UI/FerriteVoicePacksPage.cs` | 重写为 Settings Host | 新 UiKit 设置页和 fallback 游戏内通过后 clean cutover。 |
| `UI/VanillaVoicePacksPage.cs` | 迁为整页诊断 fallback 或删除 | 新 Host 能在 schema/binding/容器故障时提供稳定整页 fallback。 |
| `UI/UsWidgetRegistrar.cs` | 重写 | 仅注册 US 业务复合控件；不再重复注册 UiKit core。 |
| `UI/UsWidgetCommandAdapter.cs`、`UsCommandPayload.cs` | 删除或极薄化 | 新 typed binding/action 已覆盖所有调用点后删除。 |
| `UI/Components/UsCard.cs`、`SectionFrame.cs`、`UiPalette.cs`、`UsWidgetDrawing.cs` | 删除重复视觉/容器层 | UiKit `Surface`/theme/primitive 覆盖并通过视觉验收后删除。 |
| `UniversalSqueakerSettingsWindow` 中旧 session retry/engine dictionary | 重写 | Host 自己拥有一个 session、manifest/tree/snapshot，不按页静态缓存。 |
| `Layout.xml` | 原子迁移 `Schema=2` | schema owner + US owner 联合验证后替换，禁止半迁移。 |

## 4. 依赖与切割禁区

以下关系不可并行修改：

- `LayoutManifest` 与 `WidgetRegistry.Resolve` 的 kind/schema 语义。
- `UiSession` 的定义与所有 `UiInteract`/`UiGuard` 使用点。
- `IWidget` 的签名与所有 core/US widget 实现。
- `Palette`、`SurfaceFrame`、`SelectionButton` 的唯一皮肤入口。
- `VoicePacksPageModel.cs`、US `Layout.xml`、`UsCard`/`UsWidgetDrawing`：同一迁移 owner。
- 每一个 RimWorld/Unity 原生输入调用点：不得由多个代理同时包装。
- Settings Window 与 Overlay 的 session 实例：必须确认不共享。

## 5. 完成定义

切割只有在以下事实全部成立时才算完成：

1. UiKit 测试 harness 可在无真实游戏 UI 的 runtime stubs 下构建并运行。
2. `Schema=2` XML 的 ID/kind/属性/binding 在 Host 创建时一次性验证。
3. 同一 `OnGUI` 事件只由原生 IMGUI/Verse 控件消费一次。
4. 新设置页和相机 Overlay 均不依赖进程级控件状态。
5. 旧路径、旧 deferred 事件队列、重复视觉 helper 和二次命令桥均已删除或有明确保留理由。
6. 设置页、窗口关闭重开、低分辨率和 Overlay 生命周期均有可观察验证证据。
