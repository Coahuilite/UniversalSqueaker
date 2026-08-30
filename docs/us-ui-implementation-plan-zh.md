# US UI 实施计划

> 来源：2026-08-30 UI 审阅需求（`docs/us-ui-review-requirements-zh.md`）与设计调研（`docs/ui-ux-research-zh.md`）。
> 状态：任务规划中，待维护者确认后开始实施。

## 目标

- 移除内联 `?` 帮助按钮/help banner，解放布局排版能力。
- 右侧帮助面板成为唯一帮助入口，并始终保留（含 800×600）。
- 三栏布局：左导航 + 内容 + 右帮助，按窗口宽度响应式收缩。
- 三个功能块各自独立连续滚动，功能块标题作为导航锚点。
- UiKit 容器化 + 控件基础设施为最高优先，支撑后续组合式功能块与复合控件。
- 衰减折线图重做为可复用 UiKit 控件。

## 约束

- 最低分辨率：800×600（硬下限）。
- 常规支持：720p / 1080p / 1440p。
- 不使用内联 `?` 帮助按钮。
- 帮助面板在 800×600 下也保留。
- 深色 + 金色，功能清晰度 > 操作效率 > 美观性。

## 阶段规划

### Phase 0-A — 移除内联帮助（快速先行，释放布局）

- 移除所有 widget 中的 `UsHelpButton` / `UsHelp.DrawHelpButton` 调用。
- 移除 `UsCard` 头部帮助按钮与 help banner 绘制逻辑。
- 移除 `UsHelp.IsOpen` / `OpenHelpKeys` 相关 UI 渲染路径（状态字段可保留，避免大范围破坏）。
- 保留 `UsHelpCatalog` 供右侧帮助面板使用。
- 验收：
  - 所有区块不再出现 `?` 按钮。
  - 布局不再因帮助展开/收起而跳动。
  - 右侧帮助面板仍能显示当前功能块/区块帮助文本。

### Phase 0-B — UiKit 容器化 + 控件基础设施（最高优先）

- `LayoutManifest` 支持嵌套容器：
  - 允许 `<Block>` / `<Section>` / `<Column>` 等容器节点。
  - 容器可包含子 `<Widget>` / `<Block>`。
  - Measure/Draw/交互递归处理子节点。
- 新增/整理基础控件原语：
  - `input/dropdown`
  - `input/stepper-slider`（滑条 + 数值输入 + `−/+` 微调）
  - `chart/line`（可拖拽控制点折线图）
- 保持 UiKit 中性：不出现 US/SR 产品字面量。
- 补充容器、复合控件、图表控件的单元测试。
- 验收：
  - XML 可表达层级结构。
  - 容器 Measure/Draw/交互测试通过。
  - 新控件测试通过。
  - verify-local 全绿。

### Phase 0-C — 三栏布局始终保留 + 响应式宽度

- 移除 `HelpPanelMinTotalWidth = 1200` 的隐藏阈值。
- 三栏宽度按窗口宽度响应式收缩：

  | 窗口宽度 | 左导航 | 右帮助 | 中间内容 |
  |---|---|---|---|
  | ≥1200 | 176 | 200 | 800+ |
  | ≥1000 | 168 | 180 | 约 640 |
  | 800 | 152 | 160 | 约 466 |

- 右侧帮助面板紧凑化：Tiny 字号、减少内边距、内部滚动。
- 验收：
  - 800×600 下三栏均可见。
  - 内容区仍可单列连续滚动。
  - 无内联 `?` 按钮。

### Phase 1 — 功能块独立滚动 + 标题导航 + 包过滤器归位

- 三个功能块各自独立滚动区域，左侧导航切换时只显示当前块。
- 每个功能块标题作为导航锚点。
- 包过滤器移入“包管理 / 包清单”块内部。
- 验收：
  - 导航到某功能块时，只看到该块内容。
  - 包过滤器不再出现在全局顶部。
  - 每个块标题在导航/内容中一致。

### Phase 1-B — Tuning Editor 交互升级

- Domain 选择改为下拉菜单。
- Action Scope 改为下拉菜单。
- Mood 因子控件改为 `input/stepper-slider` 复合控件。
- 验收：
  - 不再有 “Next domain >” 循环切换。
  - Action Scope 不再靠点击循环。
  - Mood 可滑条、可输入、可 `−/+` 微调。

### Phase 1-C — 衰减折线图作为 UiKit 控件重做

- 实现 `chart/line` 控件，支持数据曲线与可拖拽控制点。
- US 衰减编辑器改用该控件。
- 验收：
  - 图表在 800×600 下可用、不破碎。
  - 可拖拽起点/终点。
  - 至少达到旧 SR 可用水平。

### Phase 2 — 收尾

- 移除 `docs/workdocs/` 临时任务书目录（若已全部落地）。
- 更新 HANDOFF / TODO / MEMORY。
- 重新打 dev 包并跑 verify-local。

## 依赖关系

- Phase 0-A 不依赖其它阶段，可立即开始。
- Phase 0-C 依赖 Phase 0-A（帮助入口确定）。
- Phase 1 依赖 Phase 0-B（容器）与 Phase 0-C（三栏）。
- Phase 1-B / 1-C 依赖 Phase 0-B（控件基础设施）。
- Phase 2 最后执行。

## 待确认

- Phase 0-B 容器节点命名与 XML 语法（`Block` / `Section` / `Column` 等）。
- Phase 1-B 下拉控件交互形态（原生 `Widgets.Dropdown` 还是自绘）。
- 是否在 Phase 0-A 直接删除 `OpenHelpKeys` 状态，还是先保留兼容。
