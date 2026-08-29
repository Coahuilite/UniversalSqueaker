# S4-Polish P7 — 原版兜底页 + 换肤收口（编码任务书）

> 状态：待派发。执行者：C-Agent（持久 worker）。依赖：P6 验收通过。
> 目标：L3 页面级 vanilla 兜底页；确认 L1/L2 组件级 fallback 已全覆盖；对照 P0 规格做最终换肤收口。

## 先读

- `docs/s4-polish-plan-zh.md` §4 P7、§10。
- `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`、`UI/VoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`、`VoicePacksViewState.cs`、`VoicePacksPageState.cs`、`UiCommand.cs`
- `Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.cs`（只读了解可用写桥）
- `docs/ui-visual-modernization-zh.md`（P0 稿全文）
- 不要读其它 workdocs。

## 改动清单

### 1. 新增 `Source/UniversalSqueaker/UI/VanillaVoicePacksPage.cs`

- 纯 `Verse.Widgets` 的简化功能兜底页；**不引用 FerriteLib**。
- 复用 `VoicePacksPageModel.BuildView(settings, catalog, state)` 与 `VoicePacksPageModel.Execute(settings, command, state)`，但只画原版控件。
- 覆盖关键功能：
  - 模式四选：四个 `Widgets.ButtonText`（选中项前缀 `●`），emit `UiCommand(SetMode, mode)`。
  - 全局音量：原版 `Widgets.HorizontalSlider` + `Widgets.Label`，emit `SetGlobalVolume`。
  - 衰减快速预设：三个 `Widgets.ButtonText`（Conservative/Balanced/Strong），emit `SetDistancePreset`。
  - 三个缩放开关 + 彩蛋 + 相机指示：`Widgets.Checkbox` + `Widgets.Label`，emit `ToggleEgg` / `ToggleBasic`。
  - 域选择：race/xenotype 行用 `Widgets.ButtonText`，emit `SelectDomain`。
  - VoicePack 勾选：`Widgets.Checkbox` 列表，emit `TogglePack`。
  - scope tree / mood 调音 / preset import 在兜底页**省略**（保核心路由可用即可）。
- 自建一个简单的页面滚动（`Widgets.BeginScrollView`）与固定行高常量；不追求美观，追求功能可用。
- 该页必须 catch 自己内部异常？不需要——它是最后兜底，异常会抛给 RimWorld 设置窗口层；但每帧绘制保持简单、避免复杂布局。

### 2. `FerriteVoicePacksPage.Draw` 升级

- 现有整页 catch 从「`EmptyState.Draw` 错误文案」改为：
  - `Log.Warning` 一次（保持现有前缀）。
  - 调用 `VanillaVoicePacksPage.Draw(rect)` 绘制兜底页。
- `GetEngine()` 失败（Layout.xml 缺失/解析失败）时同样走 `VanillaVoicePacksPage`（可复用同一 catch 路径；注意 `GetEngine()` 抛异常要能被外层 catch 接住）。
- 兜底页也失败时：最后画 `EmptyState` 错误文案（现有行为）。

### 3. L1/L2 fallback 覆盖核对与补齐

- 逐一检查 P1 要求过的 fallback 是否都存在：`BasicTuningWidget`、`GlobalVolumeWidget`、`AttenuationEditorWidget`、`CameraIndicatorWidget`、`ScopeTreeWidget`、`PresetListWidget`、`RaceLayerWidget`、`XenotypeLayerWidget`、`VoicePackChecklistWidget`、`PageTitleWidget`、`FilterBarWidget`、`UsFooterWidget`。缺的在本任务补齐。
- `UsGuard` 的每会话 once 清理钩子确认已接在 `VoicePacksPage.BeginSession/EndSession`。

### 4. 最终换肤收口

- 对照 P0 稿逐 widget 走查：hover/selected/danger/focus 三态是否一致；金线位置（行左竖条、卡片底条）是否统一；间距/行高是否与令牌一致。
- 只做视觉微调，不改变交互、命令、高度（如确需改高度，必须在验收说明中列明并同步 `Measure`）。

## 验收

- 构建 Dev 0 警告；kernel 测试全绿；UiLogicTests ALL GREEN；`verify-local.ps1` 14 门全绿。
- 人为在 dev 构建中注入两类故障（可临时改代码验证后还原）：单个 widget Draw 抛异常 → 该区段原版控件可操作；`Layout.xml` 资源缺失/解析失败 → `VanillaVoicePacksPage` 出现且模式/距离/开关/勾选可改。
- 隐私预检通过。

## 提交

```text
feat(S4): vanilla fallback page + final skin sweep
```

允许范围：`UI/VanillaVoicePacksPage.cs`（新）、`UI/FerriteVoicePacksPage.cs`、`UI/VoicePacksPage.cs`（如 BeginSession 清理钩子）、补齐 fallback 的 `UI/Widgets/*`、`UI/Visuals/*`。不得改其它文件。

## 禁止

- 不把兜底页做成第二个正式皮肤（只做简化功能面，不追求完整/美观）。
- 不改 settings schema 与业务写桥。
- 不 push / 配 remote。
