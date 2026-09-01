# DeepSeek US UI Rebuild Completion Report

> 本文件是 `docs/uikit-rebuild/tasks/DEEPSEEK-US-UI-REBUILD-COMPLETION.md` 的执行回报。
> 结果：自动/源码范围 `PASS`；真实 RimWorld 未运行，整体为 `LIMITED`。

## Implemented goal

完成 US 设置 UI 重做的代码层收口：

- Schema2 五工作区（Overview / Distance / Packs / Tuning / Presets）入口与状态切换成立。
- 左侧导航、中栏功能区、右侧上下文帮助、footer 均走单一 Kernel Host。
- Tuning/Packs/Presets/Distance 的 typed binding/action 已覆盖业务写入。
- 补齐了工作区切换时的 session scroll 重置、`All` 清空全部 UI 过滤器（含 search）、`scroll-to` 的 session scroll-target 接线，以及若干 Measure/Draw 与窄宽安全修正。
- 旧 `FerriteVoicePacksPage` / `VanillaVoicePacksPage` fallback 未删除。

## Production call graph

```text
UniversalSqueakerSettingsWindow
  -> UsKernelSettingsHost.Create(new UsKernelSettingsSource(settings))
  -> UiHost.DrawFrame(contentRect)
       BeginFrame
       MeasureAndArrange
       ApplyScrollTarget
       Draw + session popups
       EndFrame
  -> PreClose / failure path -> UiHost.Dispose
  -> legacy page only on kernel creation/frame failure

Camera Overlay (第二宿主)
  -> UsCameraIndicatorOverlay
       -> UsKernelOverlayController
            -> UsKernelOverlayHost.Create(source)
            -> UiHost.DrawFrame
       -> legacy pure-Verse readout only when kernel did not draw
```

## Business ownership

- 持久业务真相：`UniversalSqueakerSettings` / `VoicePacksPageModel` typed facade / `VoicePacksPageState`（US-owned view state）。
- 临时 UI 状态：`UiSession` 持有 scroll、popup、focus、drag、编辑缓冲、content revision、fallback 槽位。
- Overlay 使用独立 `UsKernelOverlaySource`，不依赖 Settings Window。

## Contract decisions

- 原生 `Event.current` / Verse IMGUI 是唯一输入权威；未恢复 deferred queue、第二事件树、第二 hot-control。
- XML 只声明结构/静态属性/Tab/翻译键；无 Repeat、条件、表达式或业务脚本。
- 所有 Kind、属性、binding/action 在 Host 创建期验证。
- 动态列表由 C# composite widget 迭代。
- 工作区切换会重置 session 持有的 content-scroll 与 help-scroll，保证“默认主题、活动项、滚动定位、帮助主题一致”。
- `All` 清空 domain chips、race/xenotype/author 和 search 全部 UI 过滤器。
- `scroll-to` 同时写入 `VoicePacksPageState.ScrollTargetKey` 与 `UiSession.ScrollTargetElementId`。

## Changed files

本次任务修改：

- `Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs`
  - `set-tab` 在切换工作区时重置 session 的 `content-scroll` / `help-scroll`。
  - `scroll-to` 转发到 `UiSession.SetScrollTarget`。
  - `clear-pack-filters` 增加 `SetSearchText("")`。
- `Source/UniversalSqueaker/UI/Kernel/UsNavWidget.cs`
  - `Measure` 不再重复计算尾部 `TopPadding`，与 `Draw` 几何一致。
- `Source/UniversalSqueaker/UI/Kernel/UsFilterBarWidget.cs`
  - `All` 按钮 active 状态纳入 `search-text` 为空。
  - `Validate` 增加 `search-text` binding 校验。
- `Source/UniversalSqueaker/UI/Kernel/UsScopeTreeWidget.cs`
  - 窄宽 mood 提示文本 rect 宽度 clamp，避免负宽。
- `tools/UniversalSqueakerKernelHostTests/Program.cs`
  - 新增 `WorkspaceSwitchResetsSessionScroll` 测试。
  - 扩展 `clear-pack-filters` 断言覆盖 search 清空。
  - 扩展 `scroll-to` 断言覆盖 session scroll-target。

工作树中还有上一恢复轮次保留的修复（overlay 行光标、Measure/Draw 宽度、host tests csproj 等），未在本轮回滚。

## Evidence

- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev --no-restore --nologo -v minimal` → 0 warnings / 0 errors。
- `dotnet run --project tools/UniversalSqueakerKernelHostTests -c Release --no-restore` → `ALL PASS`。
- `dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release --no-restore` → `ALL GREEN`。
- `pwsh -NoLogo -NoProfile -File scripts/verify-local.ps1 -NoRestore` → 15 门全部 `OK`。

新增/扩展的 focused evidence：

- 工作区切换后 session `content-scroll` / `help-scroll` 归零。
- `clear-pack-filters` 同时清空 `search-text`，且只 bump 一次 content revision。
- `scroll-to` 将目标写入 session scroll-target。
- 既有五工作区 × 三视口、rich 动态数据、typed writes、popup/session 隔离、disposed host、未知 Kind/属性/错型 binding 创建期失败均继续通过。

## Deleted or retained old paths

- 未删除旧 `FerriteVoicePacksPage` / `VanillaVoicePacksPage` / 旧 `UiInteract` / 旧命令桥。
- 旧路径仍仅作为 Settings kernel 创建/整帧失败时的明确 fallback。
- Overlay 的纯 Verse Camera Indicator 仍作为 kernel overlay 失败/不可用时的互斥 fallback。
- 未宣称 clean cutover。

## Remaining uncertainty

- 真实 RimWorld 中五工作区交互、popup/chart 热区、滚动坐标、hotControl、fallback 恢复未验证。
- 真实 catalog、翻译和数据路径未验证。
- 800×600 / 1280×720 / 1920×1080 实机截图/日志未产生。
- Overlay 真实引擎坐标、输入穿透、地图切换未实机验证。
- 因此不得将本报告写成 Gate U 或游戏内 `PASS`。

## Next smallest gate

主代理独立运行：

```text
pwsh -NoLogo -NoProfile -File scripts/verify-local.ps1 -NoRestore
pwsh -NoLogo -NoProfile -File scripts/build-dev.ps1
```

随后安排真实 RimWorld 实机验收：五工作区切换、Tuning/Packs/Presets 业务写入、Distance chart 拖拽、Packs 下拉/搜索/Forget、右侧帮助 hover/selection、Overlay 生命周期与 fallback 恢复。

## Main-agent acceptance review (2026-09-01)

### Result

- Automated/build evidence: `PASS`.
- This completion task: `FAIL`; one functional blocker requires a focused rework.
- Overall Gate U: `LIMITED/未通过`; real RimWorld evidence is still absent.

### Independently reproduced evidence

- `pwsh -NoLogo -NoProfile -File scripts/verify-local.ps1 -NoRestore` → all 15 checks passed.
- `pwsh -NoLogo -NoProfile -File scripts/build-dev.ps1` → Dev/Release builds succeeded with 0 warnings / 0 errors; candidate package regenerated at `dist/dev/UniversalSqueaker-dev-v0.1.0-dev-5f0c522-dirty.zip`.
- Source review confirmed the reported Host actions, nav measure change, filter binding/active-state change, narrow Mood branch, and focused assertions are present in the current working tree.

### Blocking finding

`Source/UniversalSqueaker/UI/Kernel/UsScopeTreeWidget.cs:352-380` removes the Mood slider/number/stepper controls whenever `groupWidth < 110f`. At the required 800-wide three-column layout, the center Scroll is approximately 328 px before scrollbar reservation and the card body is approximately 288–304 px; the computed Mood group width is only about 45–51 px. Therefore every Mood row takes the narrow branch and exposes only `Auto`, so pitch/volume/jitter cannot be edited at 800×600.

The same branch draws its warning label through `rect.xMax - 8`, then draws the 52 px `Auto` button ending at the same X coordinate. The warning text rect therefore overlaps the complete `Auto` button rect. Clamping the width to a positive value prevents an invalid Rect but does not satisfy the non-overlap or feature-availability contract.

Required rework: provide a compact/stacked narrow Mood layout that preserves slider, number field, minus/plus, and Auto access at the 800-wide three-column body width. Add focused geometry/interaction evidence that fails if any Mood control is omitted or overlaps another rect.

### Non-blocking findings

1. `set-tab` unconditionally resets both scroll positions and bumps revision, even when the active navigation item is clicked and `VoicePacksPageModel` performs no tab change. Restrict the reset to an actual workspace change, or explicitly document active-tab click as “scroll to top” and test that contract.
2. `UiHost.ApplyScrollTarget` identifies a target's Scroll owner from its Y range only. Side-by-side `content-scroll` and `help-scroll` share vertical ranges, so the generic API cannot reliably resolve a future help-panel target. Current US callers only request content-section IDs, so this is not the present blocker; either constrain the API to an explicit scroll key or add parent/owner data to the snapshot before general reuse.
3. The report lists five files changed in this wave, but four are untracked in the current Git baseline. The review could verify current source and behavior, not reconstruct a reliable before/after diff for those files.

### What the current evidence proves

- The current tree builds, all 15 automated gates pass, Schema2 Host creation and typed actions remain intact, and the dev package can be produced.
- The reported filter clearing, session scroll reset/target state, navigation measure parity, and positive-width Mood fallback branch exist.

### What it does not prove

- Functional Tuning operation at 800×600.
- Real RimWorld popup/chart/hotControl, catalog/translation, multi-resolution, Overlay lifecycle, or fallback recovery behavior.
- Gate U or clean cutover readiness.

### Next smallest action

External DeepSeek performs only the narrow Mood-layout rework and focused regression test, then returns an amended report. The main agent reruns the 15 gates, rebuilds the dev package, and re-reviews the affected geometry before any game-side acceptance.

## Main-agent re-review after narrow Mood rework (2026-09-01)

### Result

- Focused narrow Mood rework: `PASS`.
- Automated/build scope: `PASS`.
- Overall Gate U: `LIMITED`; real RimWorld acceptance remains outstanding.

### Verified implementation

- `UsScopeTreeWidget` now uses one `UsesStackedMoodRows` / `MoodRowHeightFor` geometry contract in Measure and Draw.
- At the required 800-wide three-column viewport, each Mood row renders a separate label/Auto header followed by full-width Pitch, Volume, and Jitter stepper lines.
- Every line retains minus, slider, number field, and plus controls; all writes continue through typed `set-mood-tuning`.
- `MoodLayoutFocusedTests` drives the real production Host, captures actual native-control rects, and verifies two rich Mood rows produce 26 controls, remain inside the scope-tree card, and do not overlap.
- Focused interactions independently cover minus, plus, slider, number commit, and Auto clear writes.

### Independent evidence

- `dotnet run --project tools/UniversalSqueakerKernelHostTests/UniversalSqueakerKernelHostTests.csproj -c Release --no-restore` → `MoodLayoutFocusedTests ALL PASS`, `ALL PASS`.
- `pwsh -NoLogo -NoProfile -File scripts/verify-local.ps1 -NoRestore` → all 15 checks passed.
- `pwsh -NoLogo -NoProfile -File scripts/build-dev.ps1` → Dev/Release builds succeeded with 0 warnings / 0 errors and produced the dev candidate.

### Remaining boundaries

- `UiNative` contains internal, null-by-default control override seams used only by the runtime Host harness; production calls still use Verse native controls when the seams are unset.
- Real RimWorld Tuning/Packs/Camera Indicator behavior, popup/chart/hotControl coordinates, catalog/translation/data, three-resolution screenshots/logs, and controlled fallback recovery remain unverified.
- Legacy Settings and Overlay fallbacks remain intentionally reachable; clean cutover is not approved.
