# Handoff — FerriteLib.UiKit 绘制顺序问题（独立会话）

> 状态：待另一个会话处理。
> 来源：S4-Polish 开发后 review（R4/R3）发现的系统性绘制顺序缺陷。
> 目标：把“widget 内部绘制顺序”提升到 FerriteLib.UiKit 层解决，而不是在 US 各 widget 里逐个打补丁。
> **范围约束：先读 `docs/workdocs/uikit-draw-order-handoff-briefing.md`，只做绘制顺序/help 可见性，禁止顺手修其它问题。**

## 1. 问题

多个 FerriteLib.UiKit 的 US widget 存在同一反模式：

```text
1. 先画 Help “?” 按钮（UsHelp.DrawHelpButton）
2. 再画不透明 surface / row / chart / segment
3. 最后注册行级 ButtonInvisible / 其它交互
```

由于 surface 的 alpha=1，会把已经画好的 help 按钮盖住；后续行级热区还可能抢走点击。这不是单个 widget 的 bug，而是 **widget 绘制顺序缺少统一约定**。

## 2. 为什么上升到 UiKit 层

- FerriteLib.UiKit 是中性布局/绘制框架，US 所有 widget 都通过它绘制。
- “help / header 操作按钮”应当是 **widget 内容 surface 之上的最顶层元素**，或至少不应被内容 surface 覆盖。
- 当前每个 US widget 自行决定绘制顺序，导致 6+ 个 widget 不一致；继续在 US 层修只能逐个打补丁，未来新 widget 仍会重犯。
- 更适合在 FerriteLib 层提供：
  - 明确的 widget 绘制阶段/分层约定；
  - 统一的 header/help 放置与绘制辅助；
  - 可复用的“内容先画、help 后画”模式。

## 3. 受影响范围（已核实）

| Widget | 当前表现 |
|---|---|
| `GlobalVolumeWidget` | help 被整块 Panel 完全盖住 |
| `FilterBarWidget` | help 被整宽 row/segment 完全盖住 |
| `CameraIndicatorWidget` | help 被 RowSurface 完全盖住 |
| `ScopeTreeWidget` | help 被 RowSurface 完全/大部分盖住 |
| `AttenuationEditorWidget` | help 与 chart surface 部分重叠，基本不可见 |
| `BasicTuningWidget` | help 与首行 RowSurface 部分重叠 |
| `PageTitleWidget` / `RaceLayerWidget` / `XenotypeLayerWidget` / `VoicePackChecklistWidget` / `PresetListWidget` | 目前正常（header 无覆盖 surface），但应统一遵守同一约定 |

关键代码位置：

- `Source/UniversalSqueaker/UI/Widgets/UsHelp.cs`
- `Source/UniversalSqueaker/UI/Widgets/UsHelpButton.cs`
- `Source/UniversalSqueaker/UI/Visuals/UsSurface.cs`
- `Source/FerriteLib.UiKit/Layout/LayoutEngine.cs`
- 上述各 US widget 的 `DrawCore`

## 4. 根因

1. widget 的 `DrawCore` 没有“先 surface，后内容，最后顶层操作按钮”的阶段约束。
2. help 按钮作为“头行操作按钮”被当成普通内容绘制，而不是顶层叠加层。
3. 没有公共 helper 强制帮助按钮绘制在内容 surface 之后。
4. `LayoutEngine` 只负责按顺序调用 widget 的 `Draw`，不感知 widget 内部层级。

## 5. 建议方案（供另一个会话选择/细化）

### 方案 A：FerriteLib 提供“绘制阶段”约定

- 在 FerriteLib.UiKit 文档/接口中定义标准绘制顺序：
  1. 背景 surface
  2. 内容控件 / 行 / 图表
  3. 头行操作按钮（help、collapse、更多）
  4. 状态覆盖 / 边框强调
- 提供示例或基类模板，让 US widget 遵循。

### 方案 B：FerriteLib 提供 `DrawHeaderActions` / `DrawTopLayer` helper

- 新增中性 helper：`UiKitGui.DrawHeaderActions(headerRect, actions)` 或类似，统一在 header 内容之后、surface 之上绘制 help 等操作按钮。
- US 的 `UsHelp.DrawHelpButton` 改为调用该 helper，保证绘制顺序正确。

### 方案 C：保留 help 排除区

- 在 widget 布局计算时，为右上角 help 保留固定矩形；所有内容 surface 的宽度/绘制范围主动避开该区域。
- 优点：不依赖绘制顺序；缺点：每个 widget 都要处理排除区，容易漏。

### 推荐

优先 **方案 B + 方案 A 的文档约定**：FerriteLib 提供统一顶层操作按钮绘制入口，并在库文档中明确“内容先画、顶层操作后画”；US 侧统一迁移。

## 6. 验收标准

- 所有带 help 的 widget：help 按钮在展开/收起、普通/hover/active 态下都可见、可点。
- help 按钮不被任何内容 surface 覆盖；点击不会被行级热区抢走。
- 同一套规则同时覆盖现有正常 widget（PageTitle/RaceLayer 等），不引入回归。
- FerriteLib 保持中性：不出现 `UniversalSqueaker`/`SqueakyRatkin`/`Ratkin`/`SR_`/`US_` 产品字面量。
- 新增或更新测试：至少覆盖“help 按钮在 surface 之后绘制”的可验证行为（若能在 stub 环境验证，则加入 `FerriteLib.UiKit.Tests`）。

## 7. 边界

- 本 handoff 只处理绘制顺序/层级问题，不处理 ScopeTree 窄屏 Measure/Draw 高度漂移（那是布局/测量问题，见 review-s4-02/04）。
- 不修改 US 业务命令流；help 仍走 `ToggleHelp`。
- 不 push、不发布。
