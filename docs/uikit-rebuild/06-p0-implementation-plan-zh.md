# UiKit 重建：P0 初始实现方案（历史记录）

> 状态：本文记录第一轮实现前的初始方案，已被源码审查证明不足以作为交付判断。当前执行必须遵循 `07-rebuild-reset-and-execution-contract-zh.md`、`tasks/DEEPSEEK-KERNEL-DELIVERY.md` 和 `tasks/DEEPSEEK-REAL-CONSUMER-SLICE.md`。
> 重要修订：本文中的“先完成 Kernel、再进入 US”仍然成立，但“Kernel core fixture 通过即可进入下一阶段”不成立。必须先通过 Gate R 真实 US Consumer Slice。

## 1. Host / Session / Widget 组织

新内核放在独立命名空间 `FerriteLib.UiKit.Kernel`，与旧棕地实现共存；旧路径保留为 fallback，
clean cutover 时删除。这样避免一次重写破坏旧路径，也避免为旧 API 增加兼容 shim。

- `UiHost`：每个 Settings Window / Overlay 一个实例。持有 manifest、widget registry、bindings、
  theme、metrics、translation、`UiSession` 和 layout engine/snapshot 缓存。
  生命周期：`Create -> Validate -> BeginFrame -> MeasureArrange -> Draw -> EndFrame -> Close`。
- `UiSession`：Host 实例拥有的临时交互状态容器。持有 scroll、popup、text edit、drag/focus、
  展开状态、ContentRevision、熔断槽位。关闭 Host 即 Dispose，重开创建新 session。
- `IUiWidget`：三阶段能力 `Configure -> Validate -> Measure -> Draw`。widget 不持有窗口状态，
  只通过 `UiWidgetContext` 访问 session/bindings/theme/metrics/translation/诊断路径。
- `UiWidgetContext`：每帧值对象/只读上下文，提供 Source、Session、Metrics、Theme、Translation、
  Bindings、ViewWidth、ElementPath。
- `UiBindings`：显式泛型 descriptor，支持 `BindValue<T>`、`BindReadOnly<T>`、`BindOptions<T>`、
  `BindAction<T>`。创建期验证 XML ID 与 binding 类型匹配。
- `UiWidgetRegistry`：显式 `InitializeCore()`，无 static ctor 自动注册；scope 精确优先、core fallback。

## 2. 原生输入、popup、scroll、结构 scope

- 输入权威仍是 `Event.current`、Verse 返回值、`GUIUtility.hotControl`、`WindowStack`。
- `UiNative` 只做无状态原生包装，状态一律从 `UiSession` 读取/写入；不存在全局 deferred registry。
- Button/slider/text field 在 widget 的 `Draw` 中同步调用 Verse 原生 API，并把已确认结果写入
  `ctx.Bindings` 或 `ctx.Actions`。
- Dropdown 使用 session-owned popup：`UiSession` 记录当前打开的下拉 id/rect/options；Host 在
  内容绘制后、同一帧内绘制 popup 并处理原生点击。没有全局 popup draw queue。
- Scroll/Clip/Overlay 只能由 Host 或 layout engine 中的结构容器调用 `Begin/EndScrollView`、
  `Begin/EndGroup`；所有 Begin/End 用 `try/finally` 成对管理。普通 leaf widget 不得创建结构 scope。
- 同一 `OnGUI` 事件不会被 UiKit 二次扫描/二次派发。

## 3. Schema=2 与 US Layout.xml 原子迁移

- 新 `UiLayoutManifest` 只接受 `Schema="2"`，支持 `UiPage/Stack/Row/Column/Wrap/Overlay/Section/
  Surface/Scroll/Clip/Widget`；安全解析（DTD 禁止、resolver null、深度/节点上限、行号）。
- 新 US Host 使用新的 `Layout.Schema2.xml`（独立 EmbeddedResource），旧 `Layout.xml` 保持
  Schema=1 供旧 fallback 继续使用。新路径游戏内验收通过后，把 `Layout.Schema2.xml` 原子替换为
  `Layout.xml` 并删除旧路径；不存在半迁移状态。
- 新 XML 把当前 `Tab`/`Column` 属性保留为静态分组属性，Host 按 tab 渲染；动态列表仍由 C# 复合组件迭代。

## 4. Typed binding/action 覆盖真实 US 消费者

- US Host 创建时注册：
  - `BindValue<float>("global-volume", get, settings.SetGlobalVolume)`
  - `BindValue<bool>` 用于 easter egg / scale toggles / camera indicator
  - `BindValue<SqueakDistancePreset>` 用于 distance preset
  - `BindOptions<TuningDomainOptionView>`、`BindReadOnly<IReadOnlyList<...>>` 用于列表/下拉
  - `BindAction<...>` 用于 mode、toggle pack、scope、mood、filter、baseline 等业务动作
- 新 US widgets 直接调用 `ctx.Bindings.Get/Set` 或 `ctx.Actions.Invoke`，不再经过
  `UsWidgetCommandAdapter`/`UsCommandPayload` 的中性字符串命令桥。
- 旧 `VoicePacksPageModel` 的业务 setter/action 作为 Host 注册 binding/action 的目标，保留业务真相。

## 5. 首个垂直 slice

先证明一条真实链路，再扩展：

```text
Schema=2 UiPage
  -> Stack
  -> Section/Surface
  -> Widget input/stepper-slider (Bind=global-volume, typed float)
  -> UiSession + UiNative slider/number field
  -> UiGuard session fallback
  -> stub harness evidence
```

该 slice 暴露 Session、Layout、Registry、Theme、Binding 和 Native 交互的跨 lane 契约问题。

## 6. 自动化与游戏内证据路径

- 扩展 `tools/FerriteLib.UiKit.Tests`：新 Kernel 测试（schema 安全、layout snapshot、session 隔离、
  native stub、fallback、binding 类型错误）。
- `scripts/verify-local.ps1` 继续跑既有门禁；新内核测试通过 `FerriteLib.UiKit.Tests` 入口统一收口。
- 游戏内验收按 `04-verification-and-acceptance-zh.md` 执行；stub/build 成功不冒充游戏内验证。
  当前会话如无法启动真实 RimWorld，只声称自动验证成立，不声称游戏内 UI 已验收。

## 7. 与推荐基线的差异及理由

- 使用 `FerriteLib.UiKit.Kernel` 子命名空间和 `IUiWidget`/`UiHost` 等新名字：为了与旧 fallback
  共存，避免同名类冲突；clean cutover 时再删除旧类型。
- 用 `ctx.Bindings.Set/Invoke` 取代独立 `IUiActionSink`：真实消费者是直接 setter/action，少一层
  间接对象，创建期类型验证更直接。
- 复用现有 `UiElementSpec` 作为不可变节点模型：它已具备 Id/Kind/Attributes/Children，不需要复制
  第二套节点类型。
- 新 `UiLayoutEngine` 直接产出 `UiLayoutSnapshot` 并管理 Scroll/Clip 结构 scope，而不是旧 flat list。
