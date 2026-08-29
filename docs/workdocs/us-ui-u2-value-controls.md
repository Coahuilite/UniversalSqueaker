# U2 任务书：值控件与状态

> 状态：待执行。
> 目标：让全局音量支持滑条 + 数值输入框双向联动；衰减编辑器拖拽状态去 static；补齐相关命令防御。
> 依赖：U1（`UiInteract` 已接入）。
> 依据：`docs/workdocs/us-ui-overhaul-assessment.md` §3 U2；backlog B-11/B-20/B-21/B-22/B-23/B-39/B-50。

## 范围

允许修改：

- `Source/UniversalSqueaker/UI/**`
- `Source/UniversalSqueaker/Settings/**`
- `Source/UniversalSqueaker/CompSqueaker.cs`（如命令防御需要）
- `tools/UniversalSqueakerUiLogicTests/**`
- `tools/FerriteLib.UiKit.Tests/**`（如需要）

禁止：

- 修改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`
- 不 push、不发布

## 任务

### 1. GlobalVolumeWidget 双向联动

- 使用同一个 `UiControlId`（例如 scope=`_spec.Id`、name=`"global-volume"`）调用：
  - `UiInteract.Slider(rect, id, value, 0f, 1f, out changed)`
  - `UiInteract.NumberField(rect, id, value, 0f, 1f, "0%", out committed)`
- 保留现有 US help/banner/skin。
- 值变化时 emit `UiCommandKind.SetGlobalVolume`，arg 为规范化 0..1 的浮点字符串。
- 0% 仍要 emit，不短路为 Disabled（保持现有语义）。

### 2. AttenuationEditorWidget 拖拽状态去 static

- 将 `_dragging` / `_dragMin` / `_dragMax` / `_originalMin` / `_originalMax` 从 static 改为：
  - 使用 `UiValueStore` 中的 `UiValueState`（扩展字段或专用 state），或
  - widget 实例字段 + 会话重置。
- 图表区域调用 `UiInteract.Protect(chartRect)`。
- 预设按钮改用 `UiInteract.Button`（若 U1 未完成则一并完成）。
- 拖拽结束 emit `SetDistanceRange`；拖拽中状态行显示 `Custom`（B-43 可在此处理或留 U3）。

### 3. 抽取纯函数

- 将图表 x↔distance、`SanitizeRange`、`FormatRange`、`FormatRangeDisplay` 提取为 `internal static` 纯函数，供单测（B-11/B-50）。
- 距离范围 clamp 语义保持：min∈[15,60]、max∈[20,65]、minRange=5、start=100%、end=0%。

### 4. 命令层防御（B-20/B-22/B-23）

- `SetGlobalVolume`：NaN/Infinity 拒绝或 clamp 到 0..1；与当前值相同则 no-op。
- `SetDistanceRange`：NaN/Infinity 拒绝或 sanitize；与当前值相同则 no-op。
- 提取公共 helper 避免 `SetGlobalVolume` 与 `NotifyCheapRuntimeChanged` 重复维护同一静态写入。

## 测试

- GlobalVolume：0% emit、100% emit、非法输入不破坏值、双向联动。
- Attenuation：纯函数断言、拖拽状态不跨会话残留。
- Settings：NaN/Infinity/no-op 防御测试（B-21/B-51）。

## 验收标准

- [ ] GlobalVolume 有 slider + number field 双向联动。
- [ ] AttenuationEditor 无 static 拖拽状态。
- [ ] 命令层 NaN/Infinity/no-op 防御测试通过。
- [ ] Dev/Release 构建零警告。
- [ ] `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` → ALL PASS。
- [ ] `dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release` → 通过。
- [ ] 相关 SettingsMigrationTests / KernelTests 通过。

## 提交信息建议

`feat(ui): value controls for global volume + attenuation state cleanup`
