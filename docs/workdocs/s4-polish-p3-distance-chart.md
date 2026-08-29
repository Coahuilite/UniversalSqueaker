# S4-Polish P3 — 距离预览折线图（编码任务书）

> 状态：待派发。执行者：C-Agent（持久 worker）。依赖：P2 验收通过。
> 纯函数 `DistancePreview` 已在 P纯 落地，本任务只做 UI 消费。

## 先读

- `docs/s4-polish-plan-zh.md` §4 P3、§3.7。
- `Source/UniversalSqueaker/UI/Layout/DistancePreview.cs`（P纯 已存在）
- `Source/UniversalSqueaker/UI/Widgets/BasicTuningWidget.cs`（距离行现状）
- `Source/UniversalSqueaker/UI/Model/VoicePacksViewState.cs`、`VoicePacksPageModel.cs`、`FerriteVoicePacksPage.cs`、`UsWidgetRegistrar.cs`、`Layout.xml`
- `docs/ui-visual-modernization-zh.md` 折线图规格（P0 稿）。
- 不要读其它 workdocs。

## 改动清单

### 1. View state 与投影

- `VoicePacksViewState` 根 DTO 新增只读 `float DistanceRangeMin`、`float DistanceRangeMax`。
- `VoicePacksPageModel.BuildView` 从 `settings.distanceRange` 投影（`distanceRange.min`/`.max`；settings 为 null 时用 15f/50f）。
- `FerriteVoicePacksPage` 字典加 `"DistanceRangeMin"`、`"DistanceRangeMax"`。

### 2. 新增 `Source/UniversalSqueaker/UI/Widgets/DistanceChartWidget.cs`

- Kind = `us/distance-chart`。
- `Measure`：宽 ≥200px 返回 64f；否则返回 22f（降级文本行）。
- `Draw`：
  - 读 `"DistancePreset"`、`"DistanceRangeMin"`、`"DistanceRangeMax"`。
  - 宽 ≥200px：用 `DistancePreview.SampleAudibilityCurve(min, max, 96, 15f, 65f)` 画折线填充图；横轴 15–65，纵轴 0–1；画 min/max 刻度竖线（AccentGold 半透明）；左上角画预设名与 `min–max`（Tiny）。全部用 `Widgets.DrawBoxSolid` 像素条，不引外部贴图。
  - 宽 <200px：降级为一行文本 `"PresetName (min–max)"`。
  - 点击图表（`Widgets.ButtonInvisible`）循环 `UiCommandKind.SetDistancePreset`，顺序与 `BasicTuningWidget` 距离行一致：Conservative→Balanced→Strong→Conservative。
  - 用 `UsGuard.DrawOrFallback`，fallback = 文本摘要（`Conservative 15–65` 等，即使宽 ≥200px 也只画文本）。
- 不改变 `BasicTuningWidget` 的距离行逻辑。

### 3. 注册与布局

- `UsWidgetRegistrar`：注册 `us/distance-chart`。
- `Layout.xml`：在 `us/basic-tuning` 与 `us/camera-indicator` 之间插入 `<Widget Id="distance-chart" Kind="us/distance-chart" />`。

## 验收

- 构建 Dev 0 警告；kernel 测试全绿；`dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release` ALL GREEN；`verify-local.ps1` 14 门全绿。
- 三档预设显示 15–65 / 15–50 / 15–40；Custom 显示 settings 当前 range。
- 隐私预检通过。

## 提交

```text
feat(S4): distance preview chart
```

允许范围：`DistanceChartWidget.cs`（新）、`VoicePacksViewState.cs`、`VoicePacksPageModel.cs`、`FerriteVoicePacksPage.cs`、`UsWidgetRegistrar.cs`、`Layout.xml`。不得改其它文件。

## 禁止

- 不碰 `CompSqueaker.ApplyDistanceRange` / `SubSoundDef` / 音频运行时。
- 不新增设置写桥。
- 不 push / 配 remote。
