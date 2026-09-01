# FerriteLib.UiKit 重建：产品与架构决策

> 状态：已由维护者确认，作为本轮重建的权威决策基线。
> 范围：FerriteLib.UiKit 内核、Universal Squeaker 设置 UI 接入、US 相机指示器 Overlay 试点。
> 原则：先完成 UiKit 内核，再让 US 按新内核的实现方式接入；允许从棕地推倒为绿地，不为旧 UiKit 公共 API 保留兼容层。

## 1. 产品定义

FerriteLib.UiKit 是一个面向 RimWorld 的、IMGUI-native、XML 声明式、可组合且响应式的 UI 框架。

- C# 注册类型化组件。
- XML 描述不可变组件树、容器、静态属性、RimWorld 翻译键和响应式约束。
- 布局引擎执行 Measure / Arrange，并产出稳定的 `LayoutSnapshot`。
- 组件在当前 `OnGUI` 调用中直接绘制并处理当前 `Event.current`。
- 窗口级 `UiSession` 管理通用临时交互状态。
- US 业务 model/settings 管理业务状态。
- 关键交互组件故障后，会话级熔断并切换 Verse fallback。
- US 设置页是第一个完整消费者；US 相机指示器是第一个 Overlay 验收面。
- 公共 API 在设置页和 Overlay 试点均通过游戏内验证前不冻结。

## 2. 已冻结的产品边界

| 维度 | 决策 |
| --- | --- |
| 产品策略 | US-first，可复用；没有真实 US/HUD 用例的能力不进入 0.1 |
| 内核优先级 | 先完成 UiKit 内核，再接入 US；US 不再手搓第二套布局/事件框架 |
| 重建方式 | 允许棕地推倒为绿地；不保留旧架构兼容 shim |
| XML 权限 | 结构、容器、静态组件属性、响应式约束、视觉 variant、RimWorld 翻译键 |
| 组件实现 | C# 注册、类型化属性、类型化 binding、同步 IMGUI render |
| 组合粒度 | 页面/功能区由 XML 组合；紧密交互复合控件由 C# 原子实现 |
| 动态列表 | 0.1 使用 C# 列表复合组件；不实现 XML `Repeat` |
| 临时状态 | 每个 Window/Overlay 实例拥有独立 `UiSession` |
| 事件权威 | `Event.current`、Verse 控件返回值、`GUIUtility.hotControl`、`WindowStack` |
| 布局 | 受限约束布局；不实现 CSS 或通用表达式语言 |
| 主题 | Theme-ready；0.1 只交付 Dark Gold + 有限 Variant |
| 动画 | 0.1 不实现动画；现代感来自排版、层级、视觉 token 和明确状态反馈 |
| fallback | 关键交互复合组件使用会话熔断后降级；不是所有叶节点都必须 fallback |
| 本地化 | 所有玩家可见文本使用 RimWorld 翻译机制；UiKit 不建私有语言系统 |
| 输入 | 0.1 支持鼠标与键盘文本输入；不承诺完整键盘导航或手柄导航 |
| HUD | 内核完成后以 US 相机指示器做一个真实 Overlay 试点 |
| API 稳定性 | 设置页 + Overlay 实机验证完成后再冻结 |

## 3. XML 与 C# 的职责

### 3.1 XML 负责

- `UiPage`、`Stack`、`Row`、`Column`、`Wrap`、`Overlay`、`Section`、`Scroll`、`Widget` 等结构。
- 节点稳定 ID、组件 Kind、父子关系。
- padding、gap、方向、alignment。
- fixed / auto / weight、min / max、wrap。
- 有限 breakpoint 下的方向、列数或可见性变化。
- `Variant`、`Density` 等有限视觉选择。
- `TitleKey`、`LabelKey`、`TooltipKey` 等 RimWorld 翻译键。

### 3.2 C# 负责

- 注册组件 Kind 与类型化属性 schema。
- 类型化 getter/setter/callback binding。
- 动态业务数据和业务命令。
- 紧密交互控件内部状态机。
- 当前 `OnGUI` 事件中的同步绘制和输入处理。
- 业务列表内部迭代。

### 3.3 XML 不负责

- `Event.current`、focus、drag、popup 生命周期。
- 任意业务对象或 `Dictionary<string, object?>` view-state。
- 字符串业务命令与二次命令翻译。
- 条件表达式、循环、脚本、样式级联。
- 进程级持久控件状态。

## 4. 类型化 binding

XML 只声明布局槽位与静态属性：

```xml
<Widget
    Id="global-volume"
    Kind="input/stepper-slider"
    LabelKey="US.Settings.GlobalVolume"
    Min="0"
    Max="1"
    Step="0.05" />
```

US 在 Host 创建时提供类型化 binding：

```csharp
bindings.BindValue<float>(
    "global-volume",
    get: () => settings.globalVolumeFactor,
    set: settings.SetGlobalVolume);
```

Host 创建时必须验证：

- XML ID 存在且唯一。
- 组件 Kind 已注册。
- XML 属性满足组件 schema。
- 组件要求的 binding 类型与页面 binding 一致。
- 必需 binding 不缺失、不重复。

验证错误必须发生在窗口/Host 创建阶段，而不是绘制到一半才发现。

## 5. UiSession 状态所有权

### 5.1 UiSession 持有

- scroll position。
- dropdown/FloatMenu 相关临时状态。
- 尚未提交的文本编辑缓冲。
- drag/focus。
- 展开/折叠状态。
- 布局 `ContentRevision`。
- 组件会话熔断状态。

### 5.2 US 持有

- 模式、音量、VoicePack 选择、调音和所有持久设置。
- 当前业务页面、筛选和业务选择状态；是否持久化由 US 产品决定。

### 5.3 禁止

- 进程级静态控件状态。
- 跨 Window 共享交互 session。
- 以组件实例对象隐式承载窗口状态。

## 6. IMGUI-native 交互契约

- UiKit 可以自绘现代视觉，但点击使用 Verse/Unity 原生同步语义。
- Button：自绘 surface/label + `Widgets.ButtonInvisible` 或等价原生行为。
- Text field：使用 Verse 文本输入与原生焦点。
- Slider：使用原生 slider 行为。
- Dropdown：优先使用 RimWorld `FloatMenu` / `WindowStack`。
- Range graph：稳定 control ID + `GUIUtility.hotControl`；MouseDown 捕获、MouseDrag 更新、MouseUp 释放。
- UiKit 可返回类型化 `UiAction` / `UiChange<T>`，但不得在页面末尾重新派发输入。
- 不得保留全局 deferred click registry、layer 排序、popup draw queue 或手工 `ToPageSpace` 坐标世界。

## 7. 容器分类与结构作用域

| 类型 | 示例 | 职责 |
| --- | --- | --- |
| 布局容器 | Stack、Row、Column、Wrap | 计算并分配子 Rect |
| 视觉容器 | Section、Surface | 背景、边框、标题、padding |
| 结构容器 | Scroll、Clip、Overlay | 改变 GUI 坐标、裁剪或 Host 作用域 |
| 叶/复合组件 | Button、List、RangeGraph | 在给定 Rect 内同步绘制/交互 |

只有 Host 和明确注册的结构容器可以调用 `Begin/EndScrollView`、`Begin/EndGroup`、clip 等结构 API。普通叶组件和业务复合组件不得自由建立结构作用域。所有 Begin/End 必须由框架以 `try/finally` 成对管理。

## 8. 布局与缓存

### 8.1 0.1 布局能力

必须支持：

- Stack / Row / Column / Wrap / Overlay。
- Section / Surface / Scroll。
- fixed / auto / weight。
- min / max。
- padding / gap。
-水平/垂直 alignment。
-文本自然高度。
-有限 breakpoint 切换方向、列数或可见性。

暂不支持：

- CSS selector/cascade。
-任意绝对定位体系。
-复杂 Grid DSL。
-margin collapsing。
-XML 条件、循环或表达式语言。
-布局动画。

### 8.2 布局快照

Measure / Arrange 必须产出 Host/session 级 `LayoutSnapshot`：

- `ContentSize`。
-节点 ID → `Rect`。
-可见节点序列。
-结构容器所需 viewport/content rect。

缓存键只包含真正影响布局的输入：

-可用尺寸。
-布局定义版本。
-内容尺寸版本。
-当前 RimWorld 语言/本地化版本。
-主题尺寸版本。

禁止使用 context 对象引用作为缓存有效性依据。稳定输入下，不得在同一帧的 Layout/Mouse/Repaint 事件重复解析 XML、实例化组件树或 Measure/Arrange。

## 9. Theme-ready，0.1 单主题

- Host 注入 `UiTheme`。
- 组件只读取语义 token：颜色、排版、间距、控件尺寸。
- 0.1 只发布 Dark Gold。
- XML 只允许有限 `Variant` / `Density`。
- 不实现主题选择 UI、第三方主题注册、XML 局部颜色覆盖或样式级联。
- `UiTheme` 公共 API 在第二个真实主题出现前不冻结。

## 10. 本地化

- 所有玩家可见业务文本使用 RimWorld 翻译键。
- XML 使用明确的 `TitleKey` / `LabelKey` / `TooltipKey`。
- UiKit 通过宿主本地化服务或 RimWorld `.Translate()` 解析。
- UiKit 不实现私有语言表或插值语法。
- UiKit fallback 文本使用 Ferrite 自有翻译键。
- 节点 ID、组件 Kind、诊断代码不翻译。

## 11. 会话熔断 fallback

关键交互组件（dropdown、stepper-slider、range graph、关键交互列表）应支持：

1. 记录组件 ID、Kind、布局路径和完整异常堆栈。
2. 在当前 `UiSession` 标记该组件熔断。
3. 中止当前不安全绘制。
4. 后续 `OnGUI` 调用在相同布局槽位使用稳定尺寸的 Verse fallback。
5. 关闭 Host 后清除熔断；下次重新尝试主组件。

XML/schema/binding/容器级故障进入整页诊断 fallback。不得假设仅恢复 `GUI.color` / `Text.Font` 就能安全恢复未知 clip/group/hotControl 状态。

## 12. OverlayHost 试点边界

US 相机指示器用于验证：

- Core 不依赖 `Window`。
-屏幕锚定与安全区。
-分辨率/缩放变化。
-输入可选穿透。
-Overlay session 创建和释放。
-同一 XML、binding、theme、component registry 机制。

0.1 不实现：可拖动 HUD、世界坐标投影、吸附编辑器、完整 HUD 管理器或动画。

## 13. 明确禁止进入 0.1 的范围

- XML `Repeat`、条件、循环、表达式。
-字符串数据绑定、字符串业务命令。
-动画系统。
-完整多主题。
-第三方稳定 API 或兼容 shim。
-通用 HUD 管理器。
-自建统一事件树或第二套 hot-control。
-旧 UiKit 行为兼容层。

## 14. 冻结后的变更纪律

任何新增内核能力必须回答：

1. 哪个已冻结的 US 设置页或相机指示器场景需要它？
2. 为什么现有 Stack/Row/Column/Wrap/Overlay/Section/Scroll/Widget 与类型化 binding 不能表达？
3. 它是否把 UiKit 推向 CSS、MVVM DSL、retained GUI 或 HUD 管理器？
4. 能否推迟到出现第二个真实消费者后再设计？

无法给出具体用例与必要性时，不进入 0.1。
