# FerriteLib.UiKit 重建：P0 契约基线

> 供下一主代理和执行代理使用。
> 本文件冻结行为不变量、边界和验收方向；不规定唯一的类名、文件拆分、参数排列或内部算法。

## 1. 执行原则

本轮要交付的是可验证的内核和 US UI，而不是照抄某个预设实现。主代理应先提出方案，再根据现实代码调整局部设计。

- **MUST** 满足本文件的行为不变量和安全边界。
- **SHOULD** 遵循推荐方向；若实际消费者、RimWorld API 或测试 harness 证明另一方案更稳，可以偏离，但要记录理由和影响。
- **MAY** 自主决定类名、接口拆分、文件布局、集合类型、缓存算法、测试组织和局部迁移顺序。
- 旧 API 不需要兼容；但不得把“删除旧 API”误解成一次性删除所有 fallback。旧路径可在新路径通过游戏内验收前保留为明确 fallback。

代理遇到冲突时，优先级为：产品边界 > IMGUI/Unity 真实语义 > 数据/状态所有权 > 可维护性 > 旧代码形状。

## 2. 必须保持的行为不变量

### 2.1 原生 IMGUI 是唯一事件权威

1. `Event.current`、Verse 控件返回值、`GUIUtility.hotControl` 和 `WindowStack` 是唯一输入权威。
2. 同一个 `OnGUI` 事件不得被 UiKit 再次扫描和二次派发。
3. 允许使用 action/change sink 把已经确认的输入结果交给 Host 或业务 model；这不是第二个事件系统。
4. 不得引入进程级 deferred click registry、全局 popup draw queue、全局 layer dispatcher 或第二套 hot-control 系统。
5. ScrollView、Group、Clip 等结构作用域由 Host 或明确的结构容器管理，必须成对关闭并在异常时清理。
6. 视觉 helper 可以自绘，但不能拥有脱离原生控件的输入权威。

### 2.2 状态按 Host/业务分离

1. 每个 Settings Window 和 OverlayHost 实例拥有独立的临时 UI session。
2. session 至少覆盖 scroll、popup/浮动菜单关联、文本编辑缓冲、focus/drag、展开状态、内容 revision 和组件熔断。
3. 关闭 Host 后不得遗留 hotControl、popup、drag/focus 或 fallback 状态；重开必须得到新的 session。
4. 持久设置、业务选择、过滤器和模型数据归 US；UiKit 不持有业务真相。
5. 进程级静态字段不能承载控件临时状态；只读常量、无状态 helpers 和测试覆盖点不受此限制。

### 2.3 XML 是结构声明，不是业务脚本

XML 可以声明：结构容器、节点 ID、Kind、padding/gap/alignment、有限尺寸约束、有限 breakpoint、静态 variant/density 和 RimWorld 翻译键。

XML 不可以声明：业务对象访问、循环/Repeat、条件表达式、脚本、任意计算、字符串业务命令、私有语言系统或样式级联。动态业务列表由 C# 复合组件迭代。

## 3. Host、Session、Context、Widget

### 3.1 生命周期

实现必须表达以下可观察生命周期，但方法名和封装方式由主代理决定：

```text
create → validate manifest/bindings/registry → begin session
      → per-frame begin → measure/arrange if needed → draw + native input
      → per-frame end → ...
close → end session and release transient state
```

必须能测试两个 Host 的状态隔离和关闭后的清理。

### 3.2 Context

组件上下文必须能取得：Host source/scope、当前 session、文本测量、theme/token、翻译解析或宿主回调、类型化 binding/action、可用布局宽度以及节点 ID/路径诊断信息。

推荐保留中性的 `ITextMetrics.MeasureText(string, UiFont, float)` seam。若实际迁移证明其他 seam 更合理，允许替换，但不得无谓破坏 US 纯布局逻辑，也不得通过反射解释任意业务 DTO。

### 3.3 Widget

每个 widget 必须具备等价的三种能力：

1. 读取一次 XML 静态 spec；
2. 在当前宽度/context/theme 下报告自然尺寸；
3. 在 Host 提供的 arranged Rect 中同步绘制并处理当前 IMGUI 事件，报告 action/change。

是否继续使用 `Configure/Measure/Draw`、是否引入 host-owned node renderer、是否通过返回值而非 callback 报告结果，由主代理根据真实消费者决定。不能牺牲上述三阶段职责和 session 隔离。

Widget 实例不得隐式承载 Window 状态。测量缓存必须按真正影响布局的输入失效。

## 4. Typed binding/action 边界

Host 创建/初始化阶段必须验证：

- XML ID 存在且唯一；
- Kind 已注册且 scope 解析明确；
- 静态属性符合该 Kind 的 schema；
- 所需 binding 存在、唯一且类型匹配；
- action/change 目标存在且类型匹配。

错误必须包含节点 ID、Kind 和布局路径，并在绘制前可观察。

0.1 至少覆盖 US 的 bool、float、枚举/候选选择、只读业务列表输入和文本编辑缓冲。推荐显式泛型 binding，而非任意反射 binding；具体 API 由实现者选择。

业务结果必须是类型化 action/change，或直接调用已经验证的类型化 setter。若暂时保留中性 UiKit action，它只能在 Host 边界一次性映射到 US 业务类型，不能形成“中性字符串命令 → 业务字符串命令”的双重翻译。

## 5. Schema=2 与布局

根节点目标形态：

```xml
<UiPage Schema="2" Source="coahuilite.universalsqueaker">
  <Stack Id="settings" Gap="8" Padding="12">
    <Widget Id="global-volume" Kind="input/stepper-slider"
            LabelKey="US.Settings.GlobalVolume" />
  </Stack>
</UiPage>
```

0.1 vocabulary 至少覆盖：`UiPage`、`Stack`、`Row`、`Column`、`Wrap`、`Overlay`、`Section`、`Surface`、`Scroll`、`Clip`、`Widget`。内部可以归一为更小的枚举，但普通叶组件不能自由创建结构作用域。

属性由结构、尺寸和组件三类组成：ID/Kind、方向/gap/padding/alignment、fixed/auto/weight、min/max、有限 breakpoint、Kind-specific typed properties、`TitleKey`/`LabelKey`/`TooltipKey`、有限 `Variant`/`Density`。未知属性是否在 parser 或 Host validator 报错由实现者决定，但必须在创建阶段拒绝不被支持的契约。

保留安全解析底线：DTD/external entity 禁止、resolver 关闭、深度/节点数上限、根节点/结束标签严格校验和行号诊断。

## 6. LayoutSnapshot 与缓存

Measure/Arrange 必须产出等价于稳定 snapshot 的结果，至少包含：ContentSize、节点 ID 到 arranged Rect、可见节点序列、viewport/content rect 和布局定义 revision。

缓存只能依赖真正影响布局的输入：可用尺寸、布局 revision、内容 revision、本地化/文本测量 revision、主题尺寸 revision，以及实现确实需要的有限输入。不得用 context 对象引用作为永久有效性依据，也不得在稳定输入下重复解析 XML、重建组件树或重复布局。

## 7. Theme

Host 注入 theme；widget 和视觉原语读取语义 token。0.1 只交付 Dark Gold，但 API 应允许未来替换 theme，不需要现在实现主题选择器或第三方主题注册。

至少覆盖 canvas/base/panel/raised/hover/selected/warning/success/danger、主要/次要/禁用文本、金色强调、边框/分割线、字体映射、间距和控件高度。token 的具体组织方式自由设计。

GUI color、font、anchor、matrix 等全局状态必须恢复；这不能替代结构 scope 和 hotControl 清理。

## 8. Fallback

关键交互控件可以在 session 内熔断：记录节点 ID/Kind/路径/完整堆栈，标记当前槽位，停止不安全主绘制，后续帧在同一槽位使用稳定尺寸 fallback，结束 session 后清除并重新尝试。

Schema、binding、容器和 Host 创建错误属于整页诊断/fallback，不能等到绘制中途才发现。fallback 不得继续污染未知的 group/clip/hotControl 状态。

## 9. DeepSeek 主代理的自主权

主代理必须先给出一份短方案，至少说明：

- 它选择的 Host/session/widget 组织方式；
- 原生输入与 popup/scroll 的处理方式；
- Schema=2 如何与现有 US `Layout.xml` 原子迁移；
- typed binding/action 如何覆盖当前 US 消费者；
- 如何让 stub harness 和真实游戏验证分别证明自己的主张。

方案获得事实支持后，主代理可以：

- 合并或拆分类和文件；
- 选择 callback、结果对象或直接 setter；
- 保留某个旧概念的不同名称或内联实现；
- 调整 lane 内部顺序；
- 为降低风险先实现一个垂直 slice，再扩展到其余控件；
- 在发现旧审计与源码不一致时，以源码和可运行证据为准修订计划。

不需要为每个局部实现细节请求许可。只有以下情况必须暂停并提交变更请求：

1. 改变上述行为不变量；
2. 扩大 0.1 范围（XML 脚本、通用 HUD、完整多主题等）；
3. 改变跨 lane 公共契约并会影响已完成消费者；
4. 需要修改别的 lane 正在拥有的文件；
5. 发现安全、数据持久化或卸载安全风险。

变更请求需要说明：事实、提议、受影响消费者、替代方案和验证方式。主代理可以接受合理偏离，也可以重新切 lane；不能要求代理机械回到已被事实否定的方案。

## 10. 阶段 gate 的含义

Gate 是证据门，不是实现风格门：

- P1 证明 kernel 的状态、事件、schema、布局和主题不变量；
- P2 证明 core widgets 可用；
- P3 证明 US Settings Host 端到端可用；
- P4 证明 Overlay 是独立第二宿主；
- P5 证明自动化、审阅、clean cutover 和证据记录完整。

Gate 失败时应定位真实原因、修复或调整方案；不得通过删测试、放宽不变量或把失败路径隐藏起来。

## 11. 推荐参考设计（非强制签名）

以下是给主代理的起始设计，不是必须逐字实现的 API。选择其他等价设计时，只需说明它如何保持第 2–8 节不变量以及如何迁移当前 US 消费者。

### 11.1 Host 与 session

推荐由 Host 实例持有以下对象：

```text
UiHost
  Manifest / component tree（创建期构建一次）
  UiSession（一个 Window/Overlay 一个）
  UiBindings（创建期注册并验证）
  UiTheme
  LayoutSnapshot（按有效输入缓存）
```

推荐生命周期接口语义：

```text
Create(manifest, bindings, theme) -> validated host
BeginFrame(frame input)
MeasureArrange(available size) -> snapshot
Draw(snapshot, host context)
EndFrame()
Dispose()/EndSession()
```

可以合并 `UiHost` 与 Window adapter，也可以把这些操作拆到窄接口；关键是 session 不得由静态类或 widget 实例隐式拥有。

### 11.2 Widget 与 action

推荐保留简单、可测试的三阶段 widget 形状：

```csharp
Configure(UiElementSpec spec)
Measure(UiContext context) -> Size/float
Draw(Rect rect, UiContext context, IUiActionSink actions)
```

`UiContext` 推荐通过窄接口暴露 session、metrics、theme、translation、bindings 和诊断路径。`IUiActionSink` 可以提交类型化结果；如果实际 US 迁移更适合返回 `UiChange<T>` 或由 binding setter 直接提交，允许替换。

不要为了抽象而强制所有 US 列表组件变成 XML 节点。`ScopeTree`、`PresetList`、`VoicePackChecklist` 等动态业务列表可以继续由 C# 复合组件实现，并使用同一 context/session/native input 规则。

### 11.3 Binding 范围

推荐从显式 descriptor 开始，而不是反射：

```text
BindValue<T>(id, getter, setter)
BindReadOnly<T>(id, getter)
BindOptions<T>(id, getter)
BindAction<TPayload>(id, action)
```

实际 API 可以不同。P0 只要求能验证以下类别：bool、float、enum/choice、只读列表和文本编辑缓冲。类型不匹配必须在 Host 创建阶段失败；不能依靠 `Convert.ToString`、任意 object dictionary 或运行时猜测把错误推迟到 Draw。

### 11.4 原生控件规则

推荐每个控件在自己的 Draw 调用中直接调用对应 Verse/Unity 原生 API，并将已确认的变化提交给 action sink：

```text
button: draw surface -> native button result -> action
slider: native slider -> clamp -> typed setter/change
text: native text field -> edit buffer/focus -> commit policy
dropdown: native FloatMenu/WindowStack or one host-owned popup
range graph: stable control id + native hotControl lifecycle
```

“一个 host-owned popup”不等于全局 popup 队列；其状态仍归当前 session，输入仍由当前原生事件决定。

### 11.5 Layout 参考模型

推荐内部表示为不可变节点树 + 每次有效布局生成 snapshot：

```text
LayoutDefinition
  Node(id, kind, static props, children)
LayoutSnapshot
  ContentSize
  RectById
  VisibleIds
  Viewports / content rects
```

布局实现可以采用递归树、预编译 plan 或其他等价模型。不得继续让每个 widget 自己决定全局坐标和结构 scope。

### 11.6 P0 的最小垂直 slice

为降低一次性重写风险，推荐先证明一条真实链路，而不是先实现全部控件：

```text
Schema=2 UiPage
  -> one Section/Stack
  -> one typed float binding
  -> one native slider/number field
  -> one session
  -> one fallback path
  -> stub evidence
```

主代理可以选择不同 slice，例如先做 `chrome/banner` 或 `input/mode-row`；只要 slice 能暴露跨 lane 契约问题，并且不把临时 spike 当作最终交付。

### 11.7 何时算作合理偏离

以下偏离通常合理：

- 用 `UiPageHost` 代替 `UiHost`；
- 保留 `IWidget` 名称但将 `WidgetContext` 拆成多个接口；
- 用 `UiResult<T>` 代替 action sink；
- 用单个 session-owned popup host 代替 `FloatMenu` wrapper；
- 将 snapshot/cache 融入 LayoutEngine，而不是新建 `LayoutSnapshot.cs`；
- 在第一条垂直 slice 中暂时保留一个明确标记、可删除的 adapter，以完成原子迁移。

以下不属于合理偏离：

- 为了少改调用点恢复全局 deferred dispatch；
- 用静态字典或 widget 字段跨 Window 保存交互状态；
- 将 XML 扩展为业务循环/条件/脚本语言；
- 通过弱类型 object/string conversion 绕过 binding validation；
- 以 stub 或编译成功代替游戏内 IMGUI 验证。

## 12. 推荐的可编译契约基线

如果主代理没有更好的、由真实消费者支持的方案，默认可以采用以下基线。这里的“推荐”不是强制逐字照抄；任何偏离应在 P0 方案中给出一段简短理由。

### 12.1 Session 与 Context

```csharp
public sealed class UiSession : IDisposable
{
    public int ContentRevision { get; }
    public UiValueStore Values { get; }
    public bool IsActive { get; }
    public void BeginFrame();
    public void EndFrame();
    public void Dispose();
}

public sealed class WidgetContext
{
    public string Source { get; }
    public UiSession Session { get; }
    public ITextMetrics Metrics { get; }
    public UiTheme Theme { get; }
    public IUiTranslation Translation { get; }
    public IUiBindings Bindings { get; }
    public float ViewWidth { get; }
    public string ElementPath { get; }
}
```

实现可以把 `UiTheme`、`IUiTranslation` 或 `IUiBindings` 拆成其他窄接口，也可以把 `WidgetContext` 做成 readonly struct；不能缺失 session、测量、主题、翻译、binding 和诊断路径这些能力。

### 12.2 Widget 与 action

```csharp
public interface IWidget
{
    string Kind { get; }
    void Configure(UiElementSpec spec);
    float Measure(WidgetContext context);
    void Draw(Rect rect, WidgetContext context, IUiActionSink actions);
}

public interface IUiActionSink
{
    void Submit<T>(string elementId, T value);
}
```

`Action<T>`、`UiResult<T>` 或 binding setter 可以替代 `IUiActionSink`。关键条件是：控件在当前原生事件中产生结果，结果只有一个明确的 Host/业务边界，不能再经过双重字符串命令翻译。

### 12.3 Typed binding

```csharp
public interface IUiBindings
{
    void BindValue<T>(string elementId, Func<T> get, Action<T> set);
    void BindReadOnly<T>(string elementId, Func<T> get);
    void BindOptions<T>(string elementId, Func<IReadOnlyList<T>> get);
}
```

这是表达能力基线，不要求所有业务都使用同一方法名。禁止使用任意反射、隐式字符串转换或 object dictionary 代替创建期类型验证。

### 12.4 Layout snapshot

```csharp
public sealed class LayoutSnapshot
{
    public Vector2 ContentSize { get; }
    public IReadOnlyDictionary<string, Rect> RectById { get; }
    public IReadOnlyList<string> VisibleIds { get; }
    public IReadOnlyDictionary<string, Rect> Viewports { get; }
    public int DefinitionRevision { get; }
}
```

集合类型可以替换；这些可观察信息不能消失。`LayoutEngine` 可以继续存在，也可以由 Host 持有等价的 layout service；不得回到只在 draw 时临时计算、无法定位和无法测试的 flat rect 状态。

### 12.5 Registry 与 Host 创建期验证

推荐 `WidgetRegistry.Register(scope, kind, factory)` 和 `Resolve(scope, kind)` 的 scope/fallback 语义，并将 core 初始化改成显式步骤。Host 创建时调用等价的 `Validate()`，一次性验证 manifest、Kind、属性和 bindings；错误对象应携带 source、element ID、Kind、路径和根因。

如果主代理发现 registry 与 Layout parser 必须合并为一个 definition catalog，可以采用合并设计；必须保留 scope、重复注册诊断和创建期失败语义。
