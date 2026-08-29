# U5 任务书：Skin / 令牌化

> 状态：待执行。
> 目标：完成 P0 皮肤在 US UI 的全面落地，消除硬编码颜色/旧 palette 残留。
> 依赖：U0（若已做 Surface/Theme 收敛则基于 UiTheme；否则基于 `UsVisualTokens`/`UsSurface` 现状修复）。
> 依据：`docs/workdocs/s4-polish-review-backlog.md` B-09、B-28..B-34。

## 范围

允许修改：

- `Source/UniversalSqueaker/UI/**`
- `Source/FerriteLib.UiKit/**`（如需要暴露/调整 theme API）

禁止：

- 修改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`
- 不 push、不发布

## 任务

- **B-09**：左导航 `DrawNav` 迁到 `UsVisualTokens`/`UsSurface`（或 UiTheme），移除 `UiPalette`/`SectionFrame` 旧路径与硬编码 `new Color(.20f,.17f,.10f,.8f)`。
- **B-28**：现代复选框 checked 边框改为 `AccentGold`（当前 `BorderStrong`）。
- **B-29**：Help `?` hover 态与 active 态区分（当前 hover 被画成 active）。
- **B-30**：ScopeTree 段按钮/mood Auto 清除钮按 P0 danger/focus 状态绘制。
- **B-31**：SearchField 表面由 `Base` 改为 `Panel`。
- **B-32**：PresetList 原版 `Widgets.Checkbox` 和 `Color.white` 全面令牌化。
- **B-33**：`UsSurface.DrawSegment` / `DrawHeader` 恢复调用前 `Text.Anchor`。
- **B-34**：Diagnostics 面板残留硬编码颜色清理，或明确 grep 范围并记录。

## 测试

- 无强制新测试，但需保证 Dev/Release 构建零警告。
- 若 U0 引入 UiTheme，补一个“主题令牌可注入”的测试。

## 验收标准

- [ ] `DrawNav` 不再使用 `UiPalette` / `SectionFrame` / 硬编码 active 色。
- [ ] 复选框、Help hover、ScopeTree 段按钮、SearchField、PresetList 全部走令牌/表面 API。
- [ ] `UsSurface.DrawSegment` / `DrawHeader` 恢复 `Text.Anchor`。
- [ ] Dev/Release 构建零警告。
- [ ] `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` → ALL PASS（如受影响）。

## 提交信息建议

`style(ui): complete P0 skin tokenization`
