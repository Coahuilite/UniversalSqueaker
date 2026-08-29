# B1 任务书：FerriteLib.UiKit 分层交互路由核心

> Worker 只读本任务书。完成标准：本任务书“验收标准”全部满足，且不修改 US 代码。

## 目标

在 FerriteLib.UiKit 中建立“每帧交互注册 + 分层命中测试”的基础设施，使输入不再依赖绘制调用顺序。

## 范围

只允许修改：

- `Source/FerriteLib.UiKit/` 下新增或修改交互相关文件
- `tools/FerriteLib.UiKit.Tests/` 下新增或修改测试

禁止修改：

- `Source/UniversalSqueaker/**`
- `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`
- 不 push、不发布

## 背景

当前 `LayoutEngine.Draw` 只是顺序调用 `IWidget.Draw`，控件直接使用 Verse `Widgets.ButtonInvisible` 等，绘制顺序即输入顺序。本 Block 提供：

1. 层级（Layer）概念；
2. 每帧交互注册表；
3. 绘制结束后按层级/顺序派发输入；
4. 对“在注册表之外自行处理输入”的控件提供 `Protect` 机制。

## 设计

### 新增文件

```text
Source/FerriteLib.UiKit/Interaction/UiLayer.cs
Source/FerriteLib.UiKit/Interaction/UiInteract.cs
```

### UiLayer

```csharp
namespace FerriteLib.UiKit;

public enum UiLayer
{
    Background = 0,
    Content = 1,
    TopAction = 2,
    Overlay = 3
}
```

### UiInteract

```csharp
namespace FerriteLib.UiKit;

public static class UiInteract
{
    // 每帧作用域
    public static void BeginFrame();
    public static void EndFrame();

    // 注册一个“外部自行处理输入”的高优先级热区
    // 例如：原生 Slider / TextField / 已直接调用 Widgets.ButtonInvisible 的 help
    public static void Protect(Rect rect);

    // 注册一个点击型交互区域；不立即处理输入
    public static void Button(Rect rect, UiLayer layer, Action callback);

    // 等价于 Button(rect, UiLayer.Background 或 Content, callback) 的大面积行按钮
    public static void Row(Rect rect, Action callback);

    // 在绘制结束后统一派发当前帧输入
    public static void ProcessEvents();
}
```

### 行为要求

- `BeginFrame` 清空上一帧注册表。
- `Button` / `Row` 只登记，不调用 `Widgets.ButtonInvisible`。
- `ProcessEvents`：
  - 先检查所有 `Protect` 热区：如果鼠标位于某个 protect rect 内，则**不触发任何 Button/Row**，也不消费事件（留给外部原生控件处理）。
  - 再在非 protect 的 Button/Row 中，按 `UiLayer` 从高到低查找鼠标命中的元素。
  - 同一 Layer 内，后注册者优先。
  - 只触发命中且优先级最高的一个 callback，并消费事件。
- `EndFrame` 必须能被安全重复调用；异常时也要保证清理。

### LayoutEngine 集成

修改 `Source/FerriteLib.UiKit/Layout/LayoutEngine.cs`：

- `Draw` 方法开头调用 `UiInteract.BeginFrame()`。
- `Draw` 方法末尾（所有 widget 绘制完成后）调用 `UiInteract.ProcessEvents()`，然后 `UiInteract.EndFrame()`。
- 使用 `try/finally` 保证异常时也执行 `EndFrame`。
- 注意：`LayoutEngine.Draw` 当前在 `FerriteVoicePacksPage` 的 `BeginScrollView` 内被调用，因此 `ProcessEvents` 仍在 ScrollView 上下文中执行，坐标语义与 widget 绘制时一致。

## 测试要求

在 `tools/FerriteLib.UiKit.Tests/Program.cs` 或同项目新增测试文件中增加：

1. **层级命中**：`Background` 大面积 rect 与 `TopAction` 小 rect 重叠时，鼠标在重叠区域只触发 `TopAction`。
2. **同层顺序**：同 Layer 两个重叠 rect，后注册者优先。
3. **Protect 优先**：鼠标在 protect rect 内时，重叠的 `Row` 不触发。
4. **无命中**：鼠标不在任何 rect 内，不触发任何 callback。
5. **清理**：`BeginFrame` / `EndFrame` 后注册表为空；连续两帧不残留。
6. **LayoutEngine 集成**：通过 `LayoutEngine.Draw` 驱动一个注册 `Button` 的测试 widget，验证 `ProcessEvents` 被调用且顺序正确。

测试可以使用现有 UnityEngine/UnityEngine stub，不要求真实游戏运行。

## 验收标准

- [ ] `UiLayer` / `UiInteract` 编译通过。
- [ ] `LayoutEngine.Draw` 接入 `BeginFrame` / `ProcessEvents` / `EndFrame`。
- [ ] 上述 6 类测试全部通过。
- [ ] `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev` 零警告。
- [ ] `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` 输出 `ALL PASS`。
- [ ] `Source/FerriteLib.UiKit` 中无 `UniversalSqueaker` / `SqueakyRatkin` / `Ratkin` / `SR_` / `US_` 字面量。
