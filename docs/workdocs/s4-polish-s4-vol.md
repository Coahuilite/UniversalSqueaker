# S4-Polish S4-Vol — 全局音量 + 相机高度衰减编辑器（编码任务书）

> 状态：待派发。执行者：C-Agent（持久 worker）。依赖：无（P纯 已验收通过；可与 P0 修订并行设计，但编码 lane 串行）。
> 目标：新增全局音量与可拖拽的相机高度衰减编辑器。这是 S4-Polish 主视觉链之前的独立小功能块，不是纯视觉。

## 先读

- `docs/s4-polish-plan-zh.md` §1.3、§4 S4-Vol、§3.7、§6.2。
- `Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.cs`
- `Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.ExposeData.cs`
- `Source/UniversalSqueaker/CompSqueaker.cs`（重点 `ResolveMoodMod`、cheap runtime 静态区）
- `Source/UniversalSqueaker/UI/Model/UiCommand.cs`
- `Source/UniversalSqueaker/UI/Model/VoicePacksViewState.cs`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`
- `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/Widgets/UsWidgetCommandAdapter.cs`
- `Source/UniversalSqueaker/UI/Widgets/BasicTuningWidget.cs`（基础设置区现状）
- `Source/UniversalSqueaker/UI/Layout/DistancePreview.cs`（P纯 已存在，复用曲线语义）
- `tools/UniversalSqueakerSettingsMigrationTests/`（测试范式）
- 不要读其它 workdocs、不要读其它 docs 长篇。

## 语义

- **全局音量**：`0%–100%`，默认 `100%`（`globalVolumeFactor = 1f`）。
  - `0%` 不是 `Disabled` 短路：发声流程照常执行，只是最终音量乘 0。
- **衰减编辑器**：
  - 横轴：相机高度，固定 `15–65`。
  - 纵轴：音量百分比 `0–100`。
  - 开始点：y 锁死 `100%`，x 可水平拖拽。
  - 结束点：y 锁死 `0%`，x 可水平拖拽。
  - 两点连线 = 线性衰减；高度 ≤ 开始点 → 100%，≥ 结束点 → 0%。
  - 最终音量 = 全局音量 × 衰减系数。
  - 拖拽后 `distancePreset = Custom`。
- **快速预设**：三个按钮 `Conservative / Balanced / Strong`，对应 `15–65 / 15–50 / 15–40`；点击写 `SetDistancePreset`。

## 改动清单

### 1. Settings 数据层

- `UniversalSqueakerSettings.cs`：
  - 新增 `public float globalVolumeFactor = 1f;`
  - 新增 `internal void SetGlobalVolume(float value)`：`Mathf.Clamp(value, 0f, 1f)`；写字段、`CompSqueaker.GlobalVolumeFactor = ...`、`QueuePersistence()`。
  - 新增 `internal void SetDistanceRange(float start, float end)`：clamp 到 15–65、强制 `end >= start + 5f`（沿用 `ClampDistanceRange` 约束）；写 `distanceRange`、`distancePreset = SqueakDistancePreset.Custom`、`NotifyDistanceRuntimeChanged()`、`QueuePersistence()`。
  - `ApplyToRuntime()` / `NotifyCheapRuntimeChanged()`：同步 `CompSqueaker.GlobalVolumeFactor = Mathf.Clamp(globalVolumeFactor, 0f, 1f);`
- `UniversalSqueakerSettings.ExposeData.cs`：
  - `Scribe_Values.Look(ref globalVolumeFactor, "globalVolumeFactor", 1f);`
  - `PostLoadInit` 中 clamp `globalVolumeFactor` 到 0–1。
  - 不需要 bump `settingsSchemaVersion`。

### 2. 运行时

- `CompSqueaker.cs`：
  - 新增 `public static float GlobalVolumeFactor = 1f;`
  - `ResolveMoodMod()` 返回前：`mod.volumeFactor *= GlobalVolumeFactor;`
  - 覆盖 `PlayOneShot` / `TryPlaySustained` / `PreviewFinal` 三个播放路径。

### 3. UI 命令与投影

- `UiCommand.cs`：`UiCommandKind` 新增 `SetGlobalVolume`、`SetDistanceRange`。
- `VoicePacksViewState.cs`：根 DTO 新增只读 `float GlobalVolumeFactor`、`float DistanceRangeMin`、`float DistanceRangeMax`；构造器加参数。
- `VoicePacksPageModel.cs`：
  - `BuildView` 投影三值（settings 为 null 时 `1f / 15f / 50f`）。
  - `Execute` 处理：
    - `SetGlobalVolume`：`float.TryParse(command.Arg, out float v)` → `settings.SetGlobalVolume(v)`。
    - `SetDistanceRange`：解析 `start|end`（或两个 Arg 字段）→ `settings.SetDistanceRange(start, end)`。
- `FerriteVoicePacksPage.cs`：view state 字典加 `"GlobalVolumeFactor"`、`"DistanceRangeMin"`、`"DistanceRangeMax"`。
- `UsWidgetCommandAdapter.cs`：命令名映射加 `SetGlobalVolume`、`SetDistanceRange`。

### 4. 新 Widgets

- `Source/UniversalSqueaker/UI/Widgets/GlobalVolumeWidget.cs`（新，Kind=`us/global-volume`）：
  - 读取 `"GlobalVolumeFactor"`。
  - 绘制 0–100% slider（可用 `Widgets.HorizontalSlider` 或自绘扁平条；本块不要求现代皮肤）。
  - 拖动/点击 emit `UiCommandKind.SetGlobalVolume`，Arg 为 `0f–1f` 字符串。
  - 显示当前百分比文本。
- `Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs`（新，Kind=`us/attenuation-editor`）：
  - 读取 `"DistanceRangeMin"`、`"DistanceRangeMax"`、`"DistancePreset"`。
  - 绘制 64px 高图表：横轴 15–65、纵轴 0–100；复用 `DistancePreview.SampleAudibilityCurve(min, max, 96, 15f, 65f)` 画填充曲线。
  - 两个可水平拖拽点：开始点 y 固定顶部 100%，结束点 y 固定底部 0%；用鼠标拖拽（`Input`/`Widgets` 命中）更新 x。
  - 拖拽结束 emit `UiCommandKind.SetDistanceRange`（Arg 如 `"15|50"`）。
  - 下方三枚按钮 `Conservative / Balanced / Strong`，emit `UiCommandKind.SetDistancePreset`。
  - 当前状态显示 `Custom` 或预设名。
  - 窄于 200px 时降级为文本摘要（本块可先简单处理，P6 再完整响应式）。
- `UsWidgetRegistrar.cs`：注册两个新 Kind。
- `Layout.xml`：在基础设置区插入 `us/global-volume` 与 `us/attenuation-editor`（可放在 `us/basic-tuning` 附近）。

### 5. 测试

- `tools/UniversalSqueakerSettingsMigrationTests`：
  - 默认 `globalVolumeFactor == 1f`。
  - `SetGlobalVolume` clamp 到 0–1。
  - Scribe round-trip。
  - `SetDistanceRange` clamp 15–65、`end >= start + 5`、置 `Custom`。
- 如需要，可在 `tools/UniversalSqueakerUiLogicTests` 增加拖拽坐标→min/max 的纯函数测试；不强求。

## 验收（自己先跑，全绿再提交）

- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev`（0 警告）
- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release`（0 警告）
- `dotnet run --project tools/UniversalSqueakerKernelTests -c Release`（全绿）
- `dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release`（ALL GREEN）
- `dotnet run --project tools/UniversalSqueakerSettingsMigrationTests -c Release`（全绿）
- `pwsh -File scripts/verify-local.ps1`（14 门全绿）
- 隐私预检：无 `PublishedFileId`/个人绝对路径/凭据。

## 提交

一条提交：

```text
feat(S4-Vol): global volume + camera-height attenuation editor
```

只允许包含：两个 Settings 文件、`CompSqueaker.cs`、`UiCommand.cs`、`VoicePacksViewState.cs`、`VoicePacksPageModel.cs`、`FerriteVoicePacksPage.cs`、`UsWidgetCommandAdapter.cs`、`GlobalVolumeWidget.cs`、`AttenuationEditorWidget.cs`、`UsWidgetRegistrar.cs`、`Layout.xml`、`tools/UniversalSqueakerSettingsMigrationTests/*`。不得改其它文件。

## 禁止

- 不做现代皮肤（P1 做）；不做帮助/窄屏/fallback（P5/P6/P7 做）。
- 不 bump settings schema。
- 不改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`。
- 不 push / 配 remote。
