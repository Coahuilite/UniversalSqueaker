# U4 任务书：Fallback / 健壮性

> 状态：待执行。
> 目标：在 U0 已建立 `UiGuard` 的基础上，补齐 fallback 语义、状态一致性和页面级兜底。
> 依赖：U0（`UiGuard` 已接入）、U1（交互路由）。
> 依据：`docs/workdocs/us-ui-overhaul-assessment.md` §3 U4；backlog B-05..B-08、B-35、B-44..B-49。

## 范围

允许修改：

- `Source/UniversalSqueaker/UI/**`
- `Source/FerriteLib.UiKit/**`（如需要小改公共 fallback 行为）
- `tools/UniversalSqueakerUiLogicTests/**`
- `tools/FerriteLib.UiKit.Tests/**`

禁止：

- 修改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`
- 不 push、不发布

## 任务

### 1. 确认 U0 已完成的 guard 收敛

- 若 U0 已删除 `UsGuard`/`FerriteGuard`，本阶段只需验证 grep 无残留。
- 若 U0 未完成，先补齐（调度者应确保 U0 先行）。

### 2. Fallback 语义

- **B-05/B-06**：所有 US widget 的 Measure 都通过 `UiGuard.MeasureOrFallback` 保护真实测量逻辑；没有未防护 Measure。
- **B-08**：外层 catch 日志节流：页面级 fallback 也通过 `UiGuard.LogFallback`（per-session once）。
- **B-35**：页面级 catch 进入 Vanilla 前恢复 GUI 状态。
- **B-44**：Ferrite 与 Vanilla 使用独立 `VoicePacksPageState` 导致状态不一致 —— 统一状态源或在切换时同步。
- **B-45**：ScopeTree/PresetList 的 Draw fallback 不只是 unavailable 文本，提供可操作/可读 fallback。
- **B-46**：`VanillaVoicePacksPage` 滚动坐标不依赖 `inRect.x == 0`。
- **B-47**：FerriteLib 内置信息组件补 Draw/Measure fallback（若仍缺失）。
- **B-48**：`EmptyState.Draw` 恢复 `Text.Font`。
- **B-49**：`VanillaVoicePacksPage` settings==null 时给出可读提示而不是静默空白。

## 测试

- `UiGuard` 注入异常测试（若 U0 未覆盖）。
- 页面级 catch 节流测试（可通过 log override 断言）。
- Vanilla 页 settings==null 行为测试（若可测）。

## 验收标准

- [ ] `Source/UniversalSqueaker/UI` 无 `UsGuard` 残留。
- [ ] 所有 US Measure 有 `UiGuard` 保护。
- [ ] 页面级 fallback 日志走 `UiGuard.LogFallback` 且 per-session once。
- [ ] Vanilla fallback 在 settings==null、坐标非 0、GUI 状态污染等情况下仍可用。
- [ ] Dev/Release 构建零警告。
- [ ] `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` → ALL PASS。
- [ ] `dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release` → 通过。

## 提交信息建议

`fix(ui): fallback robustness and state consistency`
