# UiKit 交互路由与值控件任务书索引

> 状态：规划完成，实施延后。
> 范围：仅 FerriteLib.UiKit 本身；**排除 US 迁移**；不 push、不发布。
> 目标：为 FerriteLib.UiKit 建立“分层交互路由 + 值控件状态 + 滑条/数值输入框复合控件”能力。

## 背景

当前 FerriteLib.UiKit 的绘制顺序 == 输入顺序，导致 help 等顶层操作会被内容 surface 覆盖，且大面积 row 按钮可能抢占其它组件输入。规划采用行业常见做法：**每帧收集交互区域，按 layer / z-order 统一派发输入**；同时支持滑条 + 数值输入框双向联动。

## 任务书

| Block | 任务书 | 内容 | 依赖 |
| --- | --- | --- | --- |
| B1 | `uikit-b1-interact-core.md` | 分层交互路由核心：`UiInteract` + `LayoutEngine` 集成 | 无 |
| B2 | `uikit-b2-value-store.md` | 值控件状态存储 + Slider / NumberField 原语 | B1 |
| B3 | `uikit-b3-slider-number-field.md` | `input/number-slider` 复合控件 | B2 |
| B4 | `uikit-b4-verify.md` | FerriteLib.UiKit 自验证 | B1–B3 |

## 串行原因

所有 Block 都修改同一个 `Source/FerriteLib.UiKit` 工程，且 B2/B3 依赖 B1 的 API，B4 依赖前三个 Block 的产物。为避免文件冲突和构建争用，按 B1 → B2 → B3 → B4 串行派发。

## 通用约束

- 只允许修改 `Source/FerriteLib.UiKit` 与 `tools/FerriteLib.UiKit.Tests`。
- FerriteLib 保持中性：不得出现 `UniversalSqueaker`、`SqueakyRatkin`、`Ratkin`、`SR_`、`US_` 字面量。
- 每个 worker 只读自己的任务书，不读本目录其它文件。
- 不 push、不发布、不修改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`。
