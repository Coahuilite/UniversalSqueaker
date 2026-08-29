# US UI 大修最终 Review

> 状态：完成。
> 范围：U0–U6 全部执行完毕，对照各任务书逐项验收。
> 最终门禁：`pwsh -File scripts/verify-local.ps1` 全绿（14 门）。

## 执行记录

| Stage | 任务书 | 提交 | 结果 |
|---|---|---|---|
| U0 | `us-ui-u0-api-convergence.md` | `2ac2b9b` | ✅ |
| U1 | `us-ui-u1-interaction-routing.md` | `4e20f84` | ✅ |
| U2 | `us-ui-u2-value-controls.md` | `0f13322` | ✅ |
| U3 | `us-ui-u3-layout-narrow.md` | `f84fe8a` | ✅ |
| U4 | `us-ui-u4-fallback-robustness.md` | `44b45ee` | ✅ |
| U5 | `us-ui-u5-skin-tokens.md` | `74ccfe2` | ✅ |
| U6 | `us-ui-u6-tests-gates.md` | `4fefba4` | ✅ |

## 逐项验收

### U0 — UiKit 公共 API 收敛

- [x] `UiGuard` public，Measure/Draw/LogFallback/ResetSessionLog 可用。
- [x] `FerriteGuard` 删除，内置 widget 改用 `UiGuard`。
- [x] `UsGuard` 删除，US 24 处调用改 `UiGuard` 并显式传 `logScope="UniversalSqueaker"`。
- [x] 页面级 fallback 走 `UiGuard.LogFallback`。
- [x] `UiGuard` 测试覆盖注入异常、fallback、去重、component id 日志。
- [x] 中性 grep 通过。
- ⚠️ Surface/Theme 公共 API 收敛未在本轮完成（`UsSurface`/`UsVisualTokens` 仍存在）；按 U0 任务书允许延后，记录为后续项。

### U1 — 交互路由迁移

- [x] `UsHelpButton` 使用 `UiInteract.Button(TopAction)`。
- [x] 行级按钮使用 `UiInteract.Row`，内嵌按钮使用 `UiInteract.Button(Content)`。
- [x] SearchField/GlobalVolume slider/Attenuation chart 使用 `Protect`。
- [x] LayoutEngine 正常路径无直接 `Widgets.ButtonInvisible/ButtonText`（Vanilla fallback/页面外 nav 除外）。
- [x] FerriteLib 测试新增 TopAction beats Row。

### U2 — 值控件与状态

- [x] `GlobalVolumeWidget` 滑条 + 数值输入框双向联动，0% 仍 emit。
- [x] `AttenuationEditorWidget` 拖拽状态从 static 迁到专用 state store，并在会话边界 reset。
- [x] `AttenuationMath` 纯函数提取并测试。
- [x] `SetGlobalVolume`/`SetDistanceRange` NaN/Infinity/no-op 防御。

### U3 — 布局 / 窄屏修复

- [x] B-01/B-02 ScopeTree 窄屏 Measure/Draw 一致、动态按钮宽度。
- [x] B-03 FilterBar 不强制最小 56px，避免溢出。
- [x] B-04 页面窄屏 guard 用内容宽度。
- [x] B-24 footer 高度统一、B-25 切页清滚动、B-26 移除死 footer、B-27 nav 命令统一 adapter。
- [x] B-36 All 清作者过滤、B-37 banner 宽度一致、B-38 窄屏 help banner、B-40 scope fallback 同步、B-41 死 API 清理、B-43 拖拽状态显示 Custom。
- ⚠️ B-42 `UiLayoutTier.ClampWidth` 仍未接入实际 widget（仅测试调用），记录为后续项。

### U4 — Fallback / 健壮性

- [x] 所有 US Measure 经 `UiGuard.MeasureOrFallback` 保护。
- [x] `FerriteVoicePacksPage`/`VanillaVoicePacksPage` 共享同一 `VoicePacksPageState`。
- [x] ScopeTree/PresetList fallback 文案可读。
- [x] `EmptyState.Draw` 恢复 `Text.Font`。
- [x] Vanilla settings==null 显示可读提示。
- [x] 页面级 catch 走 `UiGuard.LogFallback`。

### U5 — Skin / 令牌化

- [x] `DrawNav` 迁到 `UsSurface`/`UsVisualTokens`，移除硬编码 active 色。
- [x] checkbox checked 边框 AccentGold。
- [x] Help hover/active 可区分。
- [x] ScopeTree 段按钮 danger 边框。
- [x] SearchField 表面 Panel。
- [x] PresetList 原版 Checkbox/Color.white 令牌化。
- [x] `UsSurface.DrawSegment`/`DrawHeader` 恢复 `Text.Anchor`。

### U6 — 测试 / 门禁

- [x] B-10 `UiGuard` 注入异常测试。
- [x] B-11 `AttenuationMath` 纯函数测试。
- [x] B-12 `LayoutEngine` 宽度变化缓存失效测试。
- [x] B-50 distance clamp 精确断言。
- [x] B-52 固定临时路径改 GUID。
- [x] B-53 verify-local `dotnet run --no-restore` + 测试项目 TreatWarningsAsErrors。
- [x] B-54 第 14 门名称更新。
- [x] B-55 Vanilla 不自动单测说明沉淀。
- ⚠️ B-51 加载侧越界 clamp 未新增显式测试（`ClampDistanceRange` 已有防御代码）。
- ⚠️ B-56 `VoicePacksLayout` 纯函数测试未新增。

## 最终验证

- `pwsh -File scripts/verify-local.ps1` → **14 门全绿**。
- Dev/Release 构建零警告。
- FerriteLib.UiKit.Tests ALL PASS。
- UniversalSqueakerUiLogicTests ALL GREEN。
- UniversalSqueakerSettingsMigrationTests 通过。
- 中性 grep 通过。

## 结论

U0–U6 按任务书执行完成，**验收通过**。存在 4 个非阻塞遗留项，建议写入后续 backlog：

1. UiKit Surface/Theme 公共 API 收敛（消除 `UsSurface`/`UsVisualTokens` 与 UiKit 内部 `Palette`/`SurfaceFrame` 的重复）。
2. B-42 `UiLayoutTier.ClampWidth` 接入实际 widget。
3. B-51 加载侧越界 clamp 显式测试。
4. B-56 `VoicePacksLayout` 纯函数测试。
