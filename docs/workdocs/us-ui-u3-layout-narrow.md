# U3 任务书：布局 / 窄屏修复

> 状态：待执行。
> 目标：修复 S4-Polish review 中的布局与窄屏问题。
> 依赖：U1（交互路由）完成，避免在改布局时同时改输入。
> 依据：`docs/workdocs/s4-polish-review-backlog.md` B-01..B-04、B-24..B-27、B-36..B-43。

## 范围

允许修改：

- `Source/UniversalSqueaker/UI/**`
- `tools/UniversalSqueakerUiLogicTests/**`

禁止：

- 修改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`
- 不 push、不发布
- 不顺手改 skin（U5）或 fallback（U4）

## 任务

### Must Fix

- **B-01** ScopeTree 窄屏 stacked 层按钮 Measure/Draw 高度不一致：统一 `LayerRowHeightFor` 与 `DrawLayerRow` 的实际高度。
- **B-02** ScopeTree DomainRow/ScopeRow 固定 96px 按钮越界：窄屏下改用可用宽度比例或换行。
- **B-03** FilterBar 窄屏横向溢出：接入 `UiLayoutTier.ClampWidth`，窄屏两行布局不溢出。
- **B-04** 页面级窄屏 guard 使用窗口宽度而非扣除导航后的内容宽度：`FerriteVoicePacksPage.Draw` 的窄屏判断用 `rect.width`（已扣导航）而非窗口宽度。

### Should Fix

- **B-24** footer 高度常量统一（28/24/26），`UsFooterWidget.Measure` 参与布局。
- **B-25** 切换页签清空滚动位置。
- **B-26** `Layout.xml` footer 死声明与手工 `DrawFooter` 对齐（移除死声明或改由 LayoutEngine 绘制）。
- **B-27** 左导航命令统一走 `UsWidgetCommandAdapter`（或等价单一命令路径）。
- **B-36** FilterBar “All” 同时清除作者过滤，All 选中态考虑作者过滤。
- **B-37** 帮助 banner Measure 与 Draw 使用同一内容宽度。
- **B-38** AttenuationEditor 窄屏下 help 展开也要绘制 banner。
- **B-40** `ResolveSelectedDomain` fallback 时同步 `SelectedScope`。
- **B-41** `UiPackFilter.EnabledOnly` / `UiDomainFilter.Author` 死 API 清理或补 UI。
- **B-42** `UiLayoutTier.ClampWidth` 接入实际 widget。
- **B-43** 衰减拖拽过程中状态行显示 `Custom`。

## 测试

- 为 `VoicePacksLayout` 纯函数补测试（B-56 可在此或 U6）。
- ScopeTree 窄屏 Measure/Draw 高度一致性测试（若可测）。
- FilterBar 窄屏宽度计算测试。

## 验收标准

- [ ] B-01..B-04 修复。
- [ ] B-24..B-27、B-36..B-43 修复或明确记录为 U6 测试项。
- [ ] Dev/Release 构建零警告。
- [ ] `dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release` → 通过。
- [ ] `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` → ALL PASS（如受影响）。

## 提交信息建议

`fix(ui): layout and narrow-window hardening`
