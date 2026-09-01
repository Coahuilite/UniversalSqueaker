# 执行代理：UiKit Core Widgets

## 目标
在 kernel contract、session/native interaction、layout/schema、theme 全部冻结后，重写 UiKit core widgets，覆盖 XML 直接消费和 US 运行时实例化的控件。

## 前置

仅允许修改任务涉及的 core widget 文件；如实现证明需要窄辅助文件，先由主代理调整 ownership。不得修改 US 文件、测试入口或注册表，除非主代理重新分配。

## 文件 ownership

仅允许修改：

- `Source/FerriteLib.UiKit/Widgets/ChromeBannerWidget.cs`
- `ChromeFooterWidget.cs`
- `EmptyStateWidget.cs`
- `SectionHeaderWidget.cs`
- `InputModeCardWidget.cs`
- `InputModeRowWidget.cs`
- `SliderNumberFieldWidget.cs`
- `StepperSliderWidget.cs`
- `DropdownWidget.cs`
- `LineChartWidget.cs`

`CoreWidgetRegistrar.cs` 由 Contract/Registry owner 或集成主代理单写；不得修改。

## 目标不变量

1. core Kind 能被注册、测量和绘制；玩家可见文本走翻译或宿主翻译服务。
2. 动态值通过已冻结的 typed binding/action，临时状态归当前 session。
3. Button、slider、text field、dropdown、range graph 遵守原生 IMGUI/Verse 语义；不引入第二套输入系统。
4. 关键交互异常能进入稳定的 session fallback；叶组件不任意管理结构 scope。
5. 文本自然高度、响应式 mode row 和 chart/dropdown 的真实消费者行为得到覆盖。

控件内部可以自主选择绘制顺序、辅助类型、native API 封装、popup 组织和错误处理细节。若旧控件的最佳迁移方式不是逐行重写，允许重新组织实现；只要记录取舍并满足 gate。

## 验收

- 每个 Kind 可注册、可 Measure/Draw，stub harness 覆盖边界和错误路径。
- dropdown popup 滚动/窗口坐标正确；stepper、number field、chart 拖拽事件不二次派发。
- 文本自然高度与响应式 mode row 不截断、不重叠。
- 回传每个 Kind 的 binding/property schema 和需要 US 迁移的实例化调用点。
默认不运行 formatter、lint、build 或项目级测试，避免与其他波次互相阻塞；若主代理授权或实现确实需要，代理可以运行最小 focused check，并报告命令、结果和局限。
不修改 US 文件、测试入口或注册表，除非主代理重新分配 ownership。