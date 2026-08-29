# B3 任务书：FerriteLib.UiKit `input/number-slider` 复合控件

> Worker 只读本任务书。前置依赖：B2 已完成（`UiInteract.Slider` / `UiInteract.NumberField` 可用）。

## 目标

实现一个中性的 FerriteLib 核心控件：**滑条 + 数值输入框双向联动**。玩家可以拖动滑条，输入框数字实时跟随；也可以在输入框输入数字，滑条位置随之改变。

## 范围

只允许修改：

- `Source/FerriteLib.UiKit/` 下新增或修改控件/注册文件
- `tools/FerriteLib.UiKit.Tests/` 下新增或修改测试

禁止修改：

- `Source/UniversalSqueaker/**`
- 不 push、不发布

## 设计

### 新增文件

```text
Source/FerriteLib.UiKit/Widgets/SliderNumberFieldWidget.cs
```

修改：

```text
Source/FerriteLib.UiKit/Widgets/CoreWidgetRegistrar.cs
```

### Kind

```csharp
public const string Kind = "input/number-slider";
```

### XML 属性

| 属性 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Bind` | string | 空 | 从 `WidgetContext.TryGetViewValue(Bind)` 读取当前值 |
| `Label` | string | 空 | 左侧文字 |
| `Min` | float | `0` | 最小值 |
| `Max` | float | `1` | 最大值 |
| `Format` | string | `"0.##"` | 数值格式化，用于输入框显示 |
| `Step` | float | 可选 | 暂不强制使用；可留给后续 |
| `EmitName` | string | `"ValueChanged"` | 值变化时发出的中性命令名 |
| `Live` | bool | `true` | 输入框是否实时提交；`false` 时 Enter/失焦提交 |
| `Height` | float | `28` | 建议固定高度，可由 LayoutEngine 覆盖 |

### Widget 行为

- `Measure`：返回固定高度（默认 `28`），或根据 `Height` 属性。
- `Draw`：
  - 布局：左侧 Label、中间 Slider、右侧 NumberField。
  - 使用同一个 `UiControlId`（例如 `scope = spec.Id`，`name = "value"`）调用 B2 的 `UiInteract.Slider` 和 `UiInteract.NumberField`，保证共享同一份 `UiValueState`。
  - 当前值优先来自 `ctx.TryGetViewValue(Bind)`；若 `Bind` 为空，则使用 `UiValueState.FloatValue`。
  - 当 Slider 或 NumberField 返回 `changed` / `committed` 时，发出：
    ```csharp
    emit(new UiCommand(EmitName, newValue));
    ```
  - 值变化后同步写回 `UiValueState`（由 B2 原语完成）。
- 中性要求：不得出现任何 US / SR 字面量。

### 注册

在 `CoreWidgetRegistrar.RegisterAll()` 中注册：

```csharp
WidgetRegistry.Register(WidgetRegistry.CoreScope, SliderNumberFieldWidget.Kind, () => new SliderNumberFieldWidget());
```

## 测试要求

在 `tools/FerriteLib.UiKit.Tests` 中增加：

1. **纯函数**：数值格式化、解析、clamp。
   - `FormatValue(0.65f, "0%") == "65%"`
   - `ParseNumber("65") == 65f`
   - `ParseNumber("abc")` 非法
   - `Clamp(150f, 0f, 100f) == 100f`
2. **Measure**：`SliderNumberFieldWidget.Measure` 返回固定高度。
3. **Draw 发出命令**：通过 stub 环境驱动 Draw，模拟 Slider 返回新值后，`emit` 收到 `ValueChanged` 命令。
4. **双向联动**：
   - Slider 更新 → `UiValueState.EditText` 同步；
   - NumberField 合法输入 → `UiValueState.FloatValue` 同步。
5. **非法输入不破坏值**：输入非法文本后 `FloatValue` 不变，且不 emit。
6. **注册表集成**：通过 `LayoutEngine.Draw` 能正常绘制该控件，且 `UiInteract.ProcessEvents` 不抛异常。

测试可使用现有 stub；如果 Verse 控件难以在 stub 中模拟，则将可测逻辑拆为 internal 静态方法并测试这些方法。

## 验收标准

- [ ] `SliderNumberFieldWidget` 编译通过并在 `CoreWidgetRegistrar` 注册。
- [ ] `input/number-slider` 可通过 XML manifest 解析并绘制。
- [ ] 滑条与输入框共享同一 `UiControlId`，双向联动。
- [ ] 默认交互语义（实时拖动、合法即提交、非法不破坏）满足。
- [ ] 上述 6 类测试全部通过。
- [ ] `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev` 零警告。
- [ ] `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` 输出 `ALL PASS`。
- [ ] `Source/FerriteLib.UiKit` 中无 `UniversalSqueaker` / `SqueakyRatkin` / `Ratkin` / `SR_` / `US_` 字面量。
