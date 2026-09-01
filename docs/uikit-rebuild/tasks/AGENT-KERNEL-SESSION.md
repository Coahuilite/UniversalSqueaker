# 执行代理：UiKit Session + Native Interaction

## 目标
重写 UiKit 的窗口级会话和原生 IMGUI 交互内核。`Event.current`、Verse 控件返回值、`GUIUtility.hotControl`、`WindowStack` 是唯一输入权威；不得保留第二套 deferred event system。

## 文件 ownership

仅允许修改：

- `Source/FerriteLib.UiKit/Context/UiSession.cs`（可新建）
- `Source/FerriteLib.UiKit/Context/WidgetContext.cs`
- `Source/FerriteLib.UiKit/Context/UiPageState.cs`（若采用迁移删除，必须同步 csproj/调用点变更请求）
- `Source/FerriteLib.UiKit/Interaction/UiInteract.cs`
- `Source/FerriteLib.UiKit/Interaction/UiValueStore.cs`
- 必要时 `UiControlId.cs`、`UiValueState.cs`、`UiLayer.cs`
- `Source/FerriteLib.UiKit/Widgets/UiGuard.cs`

不修改其他 lane 文件或 US 消费者，除非主代理重新分配 ownership。可以新建必要的窄接口/辅助文件，或在发现实际依赖后建议重新切分；不得为了旧 API 形状机械保留实现。

## 目标不变量

以下是结果要求，不是唯一实现步骤：

1. `UiSession` 是每个 Window/Overlay 实例独立拥有的临时状态容器，覆盖 scroll、popup、text edit、drag/focus、展开状态、ContentRevision、熔断等实际需要的状态。
2. 生命周期必须可观察、可测试；关闭后清理临时状态，重开不继承旧 session。
3. 当前 IMGUI 事件只由原生语义消费一次；不恢复全局 deferred registry、第二套 hot-control 或手工坐标事件世界。
4. 保留可测试的纯状态 seam；不得引入进程级控件临时状态。
5. UiGuard 保留 session 级熔断、完整诊断和稳定 fallback；具体 guard API 可重设。
6. Context 提供实现所需的 source/session/metrics/theme/translation/binding/action/diagnostic 信息，不通过反射解释任意业务 DTO。

实现者可以选择 session 是 class、接口组合还是 host-owned state，选择即时返回值或 action sink，选择如何包装 native controls；先说明取舍，满足验收即可。

## 验收

- 用 stub harness 验证两个 session 的 scroll/popup/focus/drag/fallback 完全隔离。
- 验证 MouseDown/Drag/Up 与 button/slider/text/dropdown 的原生语义，不发生二次派发。
- 验证异常日志每 session 一次且含节点 ID、Kind、路径、完整堆栈。
- 验证 `EndSession` 后 hotControl 和临时状态清理。
默认不运行 formatter、lint、build 或项目级测试，除非主代理授权或需要最小 focused check；回传改动、取舍、验证结果和剩余风险。