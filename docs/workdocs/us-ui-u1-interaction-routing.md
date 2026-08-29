# U1 任务书：交互路由迁移

> 状态：待执行。
> 目标：让 US 所有 LayoutEngine 内的交互走 `UiInteract`，解决 draw-order/help 可见性问题。
> 依赖：U0（`UiGuard` 已可用；若 U0 未完成 Surface/Theme 不影响本阶段）。
> 依据：`docs/workdocs/us-ui-overhaul-assessment.md` §3 U1。

## 范围

允许修改：

- `Source/UniversalSqueaker/UI/**`
- `tools/UniversalSqueakerUiLogicTests/**`（如需要）
- `tools/FerriteLib.UiKit.Tests/**`（如需要新增集成测试）

禁止：

- 修改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`
- 不 push、不发布
- 不扩大范围去修布局/皮肤/fallback 语义（那些是 U3/U4/U5）

## 任务

### 1. Help 顶层按钮

- `UI/Widgets/UsHelpButton.cs`：
  - 不再调用 `Widgets.ButtonInvisible`。
  - 改为 `UiInteract.Button(rect, UiLayer.TopAction, () => emit?.Invoke(new KitUiCommand("ToggleHelp", helpKey)))`。
- `UI/Widgets/PageTitleWidget.cs`：已通过 `UsHelpButton`，确认迁移后生效。
- `UI/Components/HelpToggle.cs`：若已无调用方，删除；若有调用方，同样改为 `UiInteract.Button(TopAction)`。

### 2. 行级按钮

以下文件中的整行点击从 `Widgets.ButtonInvisible` 改为 `UiInteract.Row(rect, callback)`：

- `UI/Components/VoicePackRow.cs`
- `UI/Components/RaceLayerRow.cs`
- `UI/Components/XenotypeLayerRow.cs`
- `UI/Widgets/BasicTuningWidget.cs`（Egg/Distance/Basic 行）
- `UI/Widgets/CameraIndicatorWidget.cs`
- `UI/Widgets/ScopeTreeWidget.cs`（整行 layer/domain/scope/mood 行按需）
- `UI/Widgets/PresetListWidget.cs`（展开行/race/xeno 行）

### 3. 内嵌按钮 / 段按钮

以下“行内小按钮”从 `Widgets.ButtonInvisible` / `Widgets.ButtonText` 改为 `UiInteract.Button(rect, UiLayer.Content, callback)`（若与整行 Row 重叠，后注册者优先）：

- `ScopeTreeWidget`：layer 段按钮、Next domain、scope 段按钮、mood Auto / − / +
- `FilterBarWidget`：All / Enabled / Conflicts / Orphan / Author 段按钮
- `AttenuationEditorWidget`：Conservative / Balanced / Strong 预设按钮
- `PresetListWidget`：Import 按钮
- `VoicePackChecklist`（`Components/VoicePackChecklist.cs`）：Forget Unavailable 按钮

### 4. 原生输入区 Protect

- `UI/Components/SearchField.cs`：绘制前调用 `UiInteract.Protect(rect)`。
- `UI/Widgets/GlobalVolumeWidget.cs`：slider 区域调用 `UiInteract.Protect` 或改用 `UiInteract.Slider`（U2 细化）。
- `UI/Widgets/AttenuationEditorWidget.cs`：图表拖拽区域调用 `UiInteract.Protect(chartRect)`。

### 5. 清理

- 迁移后，LayoutEngine 内的正常绘制路径不应再直接调用 `Widgets.ButtonInvisible` / `Widgets.ButtonText`（Vanilla fallback 内可保留）。
- 确保 `UiInteract.ProcessEvents` 的 MouseUp 语义不破坏现有点击。

## 测试

- 在 `tools/FerriteLib.UiKit.Tests` 或 `tools/UniversalSqueakerUiLogicTests` 增加/更新：
  1. help 注册 `TopAction`，与行 `Row` 重叠时 help 优先；
  2. 段按钮与整行 Row 重叠时段按钮优先；
  3. `Protect` 区域内行按钮不触发。

## 验收标准

- [ ] `UsHelpButton` 使用 `UiInteract.Button(TopAction)`。
- [ ] LayoutEngine 内正常路径无 `Widgets.ButtonInvisible` / `Widgets.ButtonText`（grep 可验证，Vanilla fallback 除外）。
- [ ] 所有 slider/textfield/拖拽区有 `Protect` 或 `UiInteract.Slider/NumberField`。
- [ ] Dev/Release 构建零警告。
- [ ] `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` → ALL PASS。
- [ ] 相关 UI 逻辑测试通过。

## 提交信息建议

`feat(ui): route US interactions through UiInteract layers`
