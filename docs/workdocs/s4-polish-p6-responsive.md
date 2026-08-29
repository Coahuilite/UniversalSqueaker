# S4-Polish P6 — 窄屏响应式加固（编码任务书）

> 状态：待派发。执行者：C-Agent（持久 worker）。依赖：P5 验收通过。
> 这是所有 widget 的最终布局守卫，做完后每个 widget 的 Measure/Draw 在窄宽度下不得产生负 rect 或重叠。

## 先读

- `docs/s4-polish-plan-zh.md` §4 P6、§3.4。
- `Source/UniversalSqueaker/UI/Layout/VoicePacksLayout.cs`
- 全部 US widget（重点 `ScopeTreeWidget`、`BasicTuningWidget`、`CameraIndicatorWidget`、`RaceLayerRow`、`XenotypeLayerRow`、`PresetListWidget`、`FilterBarWidget`、`DistanceChartWidget`、`UsFooterWidget`、`VoicePackChecklist`、`VoicePackRow`）
- `Source/FerriteLib.UiKit/Widgets/InputModeRowWidget.cs`
- `docs/ui-visual-modernization-zh.md` 响应式三档规格（P0 稿）。
- 不要读其它 workdocs。

## 改动清单

### 1. 布局三档

- `VoicePacksLayout` 新增：
  - `public enum LayoutTier { Comfortable, Compact, Minimal, Fallback }`
  - `public static LayoutTier ForWidth(float width)`：`>=480` Comfortable；`>=320` Compact；`>=240` Minimal；否则 Fallback。
  - `public static float ClampWidth(float width, float min)`：`Math.Max(min, width)`。
  - 常量：`MinComfortableWidth=480f`、`MinCompactWidth=320f`、`MinMinimalWidth=240f`。

### 2. US widget 宽度守卫（逐项）

- `ScopeTreeWidget`：
  - `DrawLayerRow`：层段按钮宽 `Math.Max(56f, ...)`；总宽不足时三按钮改为竖排（行高相应增加并在 `Measure` 反映）。
  - `DrawDomainRow`：域文本宽 `Math.Max(1f, ...)`；`Next domain >` 按钮宽固定 96f，放不下时移到第二行（`Measure` 加高）。
  - `DrawScopeRow`：Compact 下隐藏「→ effective」提示；主标签宽 clamp。
  - mood 因子簇保持 86px 守卫不变。
- `BasicTuningWidget` / `CameraIndicatorWidget`：label 宽 `Math.Max(1f, ...)`；Minimal 下 checkbox 移到左侧第二列（不重叠）。
- `RaceLayerRow` / `XenotypeLayerRow`：Compact 下隐藏 detail 行；主标签宽 clamp。
- `PresetListWidget`：xeno 缩进 Compact 下 12px；Import 按钮固定 76f；label 宽 clamp。
- `FilterBarWidget`：<320px 两行（域过滤一行、作者一行）；段按钮最小 56f。
- `DistanceChartWidget`：<200px 文本降级已实现；确认无负 rect。
- `UsFooterWidget`：两槽各自 `Math.Max(1f, rect.width/2-8f)`。
- `VoicePackChecklist`/`VoicePackRow`：开关与文本 rect 全部 clamp；Compact 下隐藏 coverage 行。

### 3. FerriteLib.UiKit 中性响应式

- `InputModeRowWidget`：卡片行在 `cardWidth < 140f` 时改为 2×2 网格；`cardWidth < 100f` 时 1 列；`Measure` 与 `Draw` 同步。
- `tools/FerriteLib.UiKit.Tests` 追加：三种宽度下的行数/列数测量断言（用 stub metrics）。

### 4. 页面级兜底

- `FerriteVoicePacksPage.Draw`：`rect.width < 240f` 时直接 `EmptyState.Draw(rect, "Window too narrow")` 返回（在 scrollbar 计算之前）。

## 验收

- 构建 Dev 0 警告；kernel 测试全绿；UiLogicTests ALL GREEN；`verify-local.ps1` 14 门全绿。
- 给 `VoicePacksLayout.ForWidth` 在 UiLogicTests 追加 480/320/240/239 边界断言（零 Verse，可编译进 UiLogicTests——若 `VoicePacksLayout` 引用了 DTO 无法链接，则把 `ForWidth`/`ClampWidth` 放到一个不依赖 DTO 的纯静态类中，或直接在 UiLogicTests 里链接 `VoicePacksLayout.cs` 并补齐其依赖的 DTO 源文件；**推荐前者：新增 `UI/Layout/UiLayoutTier.cs` 零 Verse 只含 tier 函数，`VoicePacksLayout` 调用它**）。
- 隐私预检通过。

## 提交

```text
feat(S4): narrow responsive hardening
```

允许范围：`VoicePacksLayout.cs`（或新 `UiLayoutTier.cs`）、全部 US widget/component 相关文件、`FerriteLib.UiKit/Widgets/InputModeRowWidget.cs`、`tools/FerriteLib.UiKit.Tests/*`、`tools/UniversalSqueakerUiLogicTests/*`（如需）、`FerriteVoicePacksPage.cs`。不得改其它文件。

## 禁止

- 不隐藏整个区块（空 catalog 除外），只降级。
- 不引入滚动条以外的横向滚动。
- 不 push / 配 remote。
