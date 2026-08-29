# Briefing — FerriteLib.UiKit Draw-Order Handoff（范围约束）

> 给处理 `docs/workdocs/uikit-draw-order-handoff.md` 的独立会话。
> 目的：明确本次只修 **绘制顺序 / help 可见性**，不要顺手修其它问题。

## 本次必须做

- 建立/落实 widget 绘制顺序约定，使 help 等顶层操作按钮不被内容 surface 覆盖。
- 统一 US widget 的 help 绘制顺序（内容 surface 之后绘制，或提供排除区）。
- 可选的 FerriteLib 中性 helper / 文档约定。
- 保证现有正常 widget（PageTitle/RaceLayer 等）不回归。

## 本次禁止顺手修

以下问题**不属于本 handoff**，已写入 `docs/workdocs/s4-polish-review-backlog.md`，不要在本会话修改：

1. ScopeTree 窄屏 Measure/Draw 高度不一致、DomainRow/ScopeRow 固定 96px 越界。
2. FilterBar 窄屏横向溢出。
3. 页面级窄屏 guard 使用窗口宽度而非内容宽度。
4. `UsGuard.MeasureOrFallback` 假保护、各 widget Measure 未防护。
5. `FerriteGuard.ResetSessionLog` 未接线。
6. 外层 catch 日志刷屏、页面级 GUI 状态恢复。
7. 左导航旧皮肤/硬编码颜色（DrawNav 未迁移）。
8. 所有测试缺口（Guard 注入测试、Attenuation 纯函数、LayoutEngine 缓存回归等）。
9. 所有 Minor/Nit（footer 常量、页签滚动、FilterBar All 语义、checkbox 边框、PresetList 令牌化、Sustained 音量语义等）。

## 边界

- 可以修改 `FerriteLib.UiKit` 与 US widget 中**仅与绘制顺序/help 可见性相关**的代码。
- 如果发现某个问题与绘制顺序强耦合、无法在不改其它逻辑的情况下完成，请记录到 backlog，不要扩大范围。
- 不 push、不发布、不改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`。
