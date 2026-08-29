# B2 任务书：FerriteLib.UiKit 值控件状态存储 + Slider / NumberField 原语

> Worker 只读本任务书。前置依赖：B1 已完成（`UiInteract.Protect` 可用）。

## 目标

为 FerriteLib.UiKit 提供“滑条 + 数值输入框”所需的值控件基础设施：

1. 稳定的控件 ID 与跨帧状态存储；
2. `Slider` 原语：拖动滑条，返回当前值；
3. `NumberField` 原语：输入数值，返回显示文本；
4. 两者可共享同一个 `UiControlId`，实现双向联动。

## 范围

只允许修改：

- `Source/FerriteLib.UiKit/` 下新增或修改交互/值控件相关文件
- `tools/FerriteLib.UiKit.Tests/` 下新增或修改测试

禁止修改：

- `Source/UniversalSqueaker/**`
- 不 push、不发布

## 设计

### 新增文件

```text
Source/FerriteLib.UiKit/Interaction/UiControlId.cs
Source/FerriteLib.UiKit/Interaction/UiValueState.cs
Source/FerriteLib.UiKit/Interaction/UiValueStore.cs
```

扩展：

```text
Source/FerriteLib.UiKit/Interaction/UiInteract.cs
```

### UiControlId

```csharp
namespace FerriteLib.UiKit;

public readonly struct UiControlId : IEquatable<UiControlId>
{
    public string Scope { get; }
    public string Name { get; }

    public UiControlId(string scope, string name);
}
```

- `Scope` 通常为 widget 的 XML id 或稳定来源；
- `Name` 为控件内唯一名，例如 `"volume-slider"` / `"volume-field"`；
- 两个字段都为空时视为无效 ID；
- 实现 `Equals` / `GetHashCode`。

### UiValueState

```csharp
namespace FerriteLib.UiKit;

public sealed class UiValueState
{
    public float FloatValue;
    public string EditText = "";
    public bool Dragging;
    public bool Focused;
    public int Cursor;
}
```

### UiValueStore

```csharp
namespace FerriteLib.UiKit;

public static class UiValueStore
{
    public static UiValueState GetOrCreate(UiControlId id);
    public static void ResetFrame();
}
```

- `GetOrCreate` 对相同 ID 返回同一实例；
- `ResetFrame` 清空“仅当前帧有效”的临时状态，但保留 `FloatValue` / `EditText` 等跨帧值；
- 线程模型：与 UiKit 其它部分一致，按主线程使用即可，不要求线程安全。

### UiInteract 扩展

```csharp
public static class UiInteract
{
    // B1 已有
    public static void BeginFrame();
    public static void EndFrame();
    public static void Protect(Rect rect);
    public static void Button(Rect rect, UiLayer layer, Action callback);
    public static void Row(Rect rect, Action callback);
    public static void ProcessEvents();

    // B2 新增
    public static float Slider(
        Rect rect,
        UiControlId id,
        float value,
        float min,
        float max,
        out bool changed);

    public static string NumberField(
        Rect rect,
        UiControlId id,
        float value,
        float min,
        float max,
        string format,
        out bool committed);
}
```

### 实现要求

#### Slider

- 调用 `UiInteract.Protect(rect)`，避免低优先级 `Row` 抢占。
- 使用 Verse `Widgets.HorizontalSlider(rect, value, min, max, middleAlignment: true)` 获取用户拖动后的新值。
- 将新值写入 `UiValueStore.GetOrCreate(id).FloatValue`，并同步 `EditText = FormatValue(newValue, format)`。
- `changed = |newValue - value| > 0.0001f`。
- 返回 `newValue`。

#### NumberField

- 调用 `UiInteract.Protect(rect)`。
- 使用 Verse `Widgets.TextField(rect, displayText)` 获取用户输入文本。
- 解析逻辑：
  - 合法数字：`parsed = float.Parse(text, CultureInfo.InvariantCulture)`，clamp 到 `[min, max]`；
  - 更新 `UiValueState.FloatValue = parsed`，`EditText = text`；
  - `committed = true`。
  - 非法/空：不修改 `FloatValue`，但保留 `EditText = text`（用户输入内容仍显示）；
  - `committed = false`。
- 失焦或 Enter 时：
  - 若当前 `EditText` 非法，回退为 `FormatValue(FloatValue, format)`；
  - 若合法，使用 clamp 后的值更新 `FloatValue`。
- 返回应显示的文本：
  - 聚焦中：返回 `EditText`；
  - 未聚焦：返回 `FormatValue(FloatValue, format)`。

#### 默认交互语义（已确认）

- 滑条拖动：实时更新输入框文本；
- 输入框输入：合法则实时更新滑条并标记 committed；
- 空/非法输入：不更新滑条，不 emit；
- 失焦或 Enter：非法输入回退为当前合法值。

## 测试要求

在 `tools/FerriteLib.UiKit.Tests` 中增加：

1. `UiControlId` 相等性与哈希。
2. `UiValueStore.GetOrCreate` 同 ID 同实例、不同 ID 不同实例。
3. Slider 拖动状态：模拟 `MouseDown` / `MouseDrag` / `MouseUp` 后 `FloatValue` 变化，`EditText` 同步。
4. NumberField 合法输入：输入 `"65"` 后 `FloatValue = 65`，`EditText = "65"`，`committed = true`。
5. NumberField 非法输入：输入 `"abc"` 后 `FloatValue` 不变，`committed = false`。
6. Clamp：输入 `150` 且 `max=100` 时 `FloatValue = 100`。
7. 失焦回退：非法文本失焦后显示回 `FormatValue(FloatValue, format)`。
8. `ResetFrame` 后不残留上一帧临时状态。

测试可使用现有 UnityEngine/UnityEngine stub；若 stub 不足以模拟 `Widgets.HorizontalSlider` / `Widgets.TextField`，可将纯解析/clamp/format 逻辑拆成 internal 静态方法单独测试。

## 验收标准

- [ ] `UiControlId` / `UiValueState` / `UiValueStore` 编译通过。
- [ ] `UiInteract.Slider` / `UiInteract.NumberField` 可用，并调用 `UiInteract.Protect`。
- [ ] 上述 8 类测试全部通过。
- [ ] `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev` 零警告。
- [ ] `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` 输出 `ALL PASS`。
- [ ] `Source/FerriteLib.UiKit` 中无 `UniversalSqueaker` / `SqueakyRatkin` / `Ratkin` / `SR_` / `US_` 字面量。
