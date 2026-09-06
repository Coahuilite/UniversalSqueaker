# FerriteLib.UiKit 重建任务入口

> 本目录是 FerriteLib.UiKit 绿地重建、US 设置 UI 重接入与 Overlay 试点的专用决策和任务书入口。
- 当前状态：已完成第一轮架构调查，确认现有 Kernel 只是孤立垂直切片；`UsKernelSettingsHost` 尚未接入生产设置路径，完整 Schema2 US 页面尚不能创建。此前“P1 Kernel 已完成”的表述已被事实推翻。
  - **修正（2026-09-04，历史证据保留原文；hash 于 2026-09-07 重定位）**：上一行是调查期口径，现已反相——`UsKernelSettingsHost` 是**唯一**生产设置路径（Schema=2 清单 + 16 个 `us/*` 控件 + 库前置模组 `coahuilite.ferritelib`），旧页面链与旧 `Layout.xml` 已在 `38b1247` 删除，UiKit 本体已拆到 `../ferritelib`。本目录"Gate U 通过前不删除旧路径"一类约束（含下方第 49 行与 07 号契约 §C1）同批失效：clean cutover 已于 2026-09-02 执行。当前事实以 `HANDOFF.md`（维护者本地）与代码为准，本目录仅作历史任务书保留。
- 新执行入口：`07-rebuild-reset-and-execution-contract-zh.md`。
- 新任务入口：`tasks/DEEPSEEK-KERNEL-DELIVERY.md`、`tasks/DEEPSEEK-REAL-CONSUMER-SLICE.md`、`tasks/DEEPSEEK-US-SETTINGS-MIGRATION.md`、`tasks/DEEPSEEK-GATE-REVIEW.md`。
- 新门禁要求先证明真实 US consumer slice，再允许全量设置 UI 迁移；stub/build 结果不得冒充游戏内验收。
- 本目录中的旧 P1/P2/P3 任务书保留为背景和 ownership 参考；凡与 07 及新 DeepSeek 任务书冲突，以新文件为准。

## 权威文件

1. `01-product-and-architecture-decisions-zh.md` — 已冻结的产品与架构决策。
2. `02-brownfield-cutover-matrix-zh.md` — 旧 UiKit/US 文件的保留、重写、删除、迁移矩阵。
3. `03-execution-dag-and-agent-map-zh.md` — 原始执行图、ownership 和 gate 背景。
4. `04-verification-and-acceptance-zh.md` — 自动化、构建、游戏内设置页与 Overlay 验收证据链。
5. `05-p0-contract-baseline-zh.md` — P0 行为不变量、契约边界和实现自主权。
6. `06-p0-implementation-plan-zh.md` — 已执行但未通过真实消费者门的初始方案。
7. `07-rebuild-reset-and-execution-contract-zh.md` — 基于当前源码事实修订的执行契约、阶段门和停止条件。
8. `tasks/DEEPSEEK-KERNEL-DELIVERY.md` — Kernel 交付目标任务书。
9. `tasks/DEEPSEEK-REAL-CONSUMER-SLICE.md` — 第一条真实 US consumer slice 任务书。
10. `tasks/DEEPSEEK-US-SETTINGS-MIGRATION.md` — 全量 US Settings 迁移任务书。
11. `tasks/DEEPSEEK-GATE-REVIEW.md` — 主代理每道门的审查与证据任务书。
12. `tasks/MAIN-ORCHESTRATOR.md` — 主代理调度、集成和最终验收任务书。
13. `tasks/AGENT-*.md` — 旧 lane 的局部 ownership 参考；不得覆盖新执行契约。

## 读取顺序

后续主代理必须按以下顺序读取：

1. 本文件。
2. `01-product-and-architecture-decisions-zh.md`。
3. `02-brownfield-cutover-matrix-zh.md`。
4. `03-execution-dag-and-agent-map-zh.md`。
5. `04-verification-and-acceptance-zh.md`。
6. `05-p0-contract-baseline-zh.md`。
7. `07-rebuild-reset-and-execution-contract-zh.md`。
8. `tasks/MAIN-ORCHESTRATOR.md`。
9. 派发第一条 slice 前读取 `tasks/DEEPSEEK-REAL-CONSUMER-SLICE.md`；进入全量迁移前再读取 `tasks/DEEPSEEK-US-SETTINGS-MIGRATION.md`。
10. 需要阶段审查时读取 `tasks/DEEPSEEK-GATE-REVIEW.md` 和具体旧 lane 任务书。

## 最高约束

- 原生 IMGUI 事件是唯一输入权威。
- XML 是结构/布局/静态属性/翻译键的权威，不是业务脚本或数据 DSL。
- 业务数据使用类型化 binding；临时交互状态归 Host 实例级 `UiSession`。
- 结构作用域只归 Host 与明确结构容器。
- UiKit 内核完成并验收前，不迁移 US 设置页。
- 新 US 三页和 Overlay 试点均通过游戏内验证前，不删除旧路径；通过后一次性 clean cutover，不长期保留双实现。
- 不配置 remote、不 push、不发布。

## 交付状态解释

`FerriteLib.UiKit.Tests` 全绿只代表当前测试覆盖的 stub/纯逻辑主张成立。它不代表完整 `Layout.Schema2.xml` 可由 US Host 创建，不代表新 Host 已被设置窗口调用，也不代表真实 RimWorld 的 IMGUI、WindowStack、popup、hotControl 或分辨率行为成立。任何阶段报告必须明确“证明了什么”和“没有证明什么”。
