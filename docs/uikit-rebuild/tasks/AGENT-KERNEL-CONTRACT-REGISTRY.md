# 执行代理：UiKit Widget Contract + Registry

## 目标
冻结并实现 `IWidget`、Kind 注册与 Host 显式初始化契约，使 XML kind 解析不依赖静态构造器隐式拉起全部控件。

## 文件 ownership

仅允许修改：

- `Source/FerriteLib.UiKit/Widgets/IWidget.cs`
- `Source/FerriteLib.UiKit/Registry/WidgetRegistry.cs`
- `Source/FerriteLib.UiKit/Widgets/CoreWidgetRegistrar.cs`
- 必要时 `Registry/UnknownWidgetKindException.cs`
- 必要时新建 `Source/FerriteLib.UiKit/Widgets/UiAction.cs` 或 typed binding contract 文件

不修改其他 lane 文件或 US 消费者，除非主代理重新分配 ownership。可新建必要的窄契约文件；可以调整旧接口或拆分 registry，只要先说明对 Layout/Session/Core consumers 的影响。

## 目标不变量

1. Widget 具备配置、测量、绘制/输入三阶段能力，且不隐式拥有 Window 状态。
2. Kind registry 的 scope/fallback/初始化顺序明确；不得依靠不可控的静态副作用。
3. 动态业务数据使用类型化 binding/action；XML 不产生字符串业务命令。
4. 缺失/重复 Kind、ID、属性或 binding 能在创建期诊断。
5. 测试可观察 seam 得以保留或由等价 seam 替代并说明理由。

具体接口形状、registry 容器、初始化 API、错误类型和文件组织由实现者选择。不能让旧消费者方便性凌驾于新不变量；也不必为了“看起来绿地”而重命名一切。

## 验收

- 验证 core/US scope 注册、fallback、重复/未知 kind 错误和显式初始化顺序。
- 验证同一组件实例可被不同 session 安全使用，窗口状态不藏在组件对象。
- 向主代理回传冻结的接口清单、调用约束、未决 binding 类型和与 LayoutManifest 的集成点。
默认不运行 formatter、lint、build 或项目级测试，除非主代理授权或需要最小 focused check；不得修改消费者来掩盖契约问题。先回报影响和建议。