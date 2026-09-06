# Review S4-01 — Settings / Runtime 功能区

## 结论

已核对 HEAD `46b7d16` 下 Settings / Runtime 相关文件，并执行：

```bash
dotnet run --project tools/UniversalSqueakerSettingsMigrationTests/UniversalSqueakerSettingsMigrationTests.csproj -c Release --no-restore
```

全部场景通过（含新增的 global volume 与 distance range 断言）。

核心验收点均满足：

1. `globalVolumeFactor` 默认 `1f`，Scribe 默认 `1f`，`PostLoadInit`/`ApplyToRuntime`/`NotifyCheapRuntimeChanged`/`SetGlobalVolume` 均 clamp 到 `0..1`；`0%` 只是音量乘 0，不短路 `Disabled` 旁路。
2. `SetGlobalVolume` 同步写 `CompSqueaker.GlobalVolumeFactor` 并 `QueuePersistence`；`NotifyCheapRuntimeChanged` 对同一静态值有等价同步。
3. `SetDistanceRange` 经 `ClampDistanceRange` 限制 `15..65`、强制 `end >= start + 5`，置 `distancePreset = Custom`，调用 `NotifyDistanceRuntimeChanged()` 并 `QueuePersistence()`。
4. `CompSqueaker.GlobalVolumeFactor` 在 `ResolveMoodMod` 返回前乘入，`PlayOneShot` / `TryPlaySustained` / `PreviewFinal` 三条播放路径都经 `ResolveMoodMod`，覆盖完整。
5. 未 bump settings schema；旧档无 `globalVolumeFactor` 节点时由 Scribe 默认值落到 `1f`，`distanceRange` 继续走既有默认/ clamp 逻辑。

未发现 Blocker / Major。存在 2 个 Minor 和 3 个 Nit，集中在命令解析对非有限浮点的防御、测试对运行时副作用覆盖不足，以及少量重复写入/维护性问题。

## 发现

### Minor

#### 1. 命令解析接受 `NaN`/`Infinity`，setter 未做有限性防御，可能把 NaN 写入运行时衰减范围

- 文件：`Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs:124-127`、`Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs:259-268`
- 文件：`Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.cs:133-139`、`Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.cs:143-150`
- 文件：`Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.cs:446-455`

`float.TryParse(..., NumberStyles.Float, CultureInfo.InvariantCulture, ...)` 在 .NET 下会接受 `"NaN"` / `"Infinity"` / `"-Infinity"`（已用 pwsh 验证）。当前 UI 只发出 `ToString("0.###")` 的有限小数，所以正常玩家路径不可达；但 `UiCommand` 与 `VoicePacksPageModel` 都是 public 模型，任何程序化/调试/未来文本输入路径都可能传入这些字符串。

- `SetGlobalVolume(NaN)`：`Mathf.Clamp(NaN, 0f, 1f)` 在 Unity 语义下返回 `NaN`，`globalVolumeFactor` 与 `CompSqueaker.GlobalVolumeFactor` 都会变成 NaN。
- `SetDistanceRange(NaN, end)`：`ClampDistanceRange` 中的 `if (max < min + 5f)` 对 NaN 为 false，可能产生 `FloatRange(NaN, ...)`，随后 `NotifyDistanceRuntimeChanged()` 会把 NaN 写入 `activeDistanceRange` 与 `SubSoundDef.distRange`，影响音频衰减。

建议在命令层或 setter 层增加 `float.IsFinite(value)` 检查（或至少拒绝 `IsNaN`/`IsInfinity`）后再 clamp/写入。

#### 2. 新增测试未覆盖运行时副作用与“0% 不短路”关键语义

- 文件：`tools/UniversalSqueakerSettingsMigrationTests/Program.cs:308-378`

现有 `GlobalVolumeDefaultsAndClamps`、`GlobalVolumeScribeRoundTrip`、`SetDistanceRangeClampsAndMarksCustom` 验证了字段值、clamp、Custom 标记和 Scribe round-trip，但没有覆盖：

- `SetGlobalVolume(0f)` 后 `CompSqueaker.GlobalVolumeFactor == 0f` 且发声流程仍正常 dispatch（不进入 `Disabled` 旁路）。
- `SetDistanceRange` 后 `CompSqueaker.ApplyDistanceRange` 的真实副作用（`activeDistanceRange` 更新、`SubSoundDef.distRange` 被改写）。
- `QueuePersistence()` 被触发（当前测试环境无 `UniversalSqueakerMod.Instance`，至少可用注入/桩断言调用计数）。

这些是 S4-Vol 的验收核心，建议补测试或至少由人工验证清单显式覆盖。

### Nit

#### 3. `SetGlobalVolume` / `SetDistanceRange` 未做 no-op 守卫，重复命令也会写运行时并排队保存

- 文件：`Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.cs:133-150`

`SetBasicTuning`、`SetCameraIndicator`、`SetDistancePreset` 都在无变化时提前返回；这两个新 setter 总是写字段、写运行时、`QueuePersistence()`。当前 UI 在值变化超过阈值才发命令，所以实际影响很小，但若未来有重复/批量命令会产生无谓保存。

#### 4. `SetGlobalVolume` 与 `NotifyCheapRuntimeChanged` 重复维护同一静态写入

- 文件：`Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.cs:116`、`UniversalSqueakerSettings.cs:137`

`SetGlobalVolume` 直接写 `CompSqueaker.GlobalVolumeFactor`，与 `NotifyCheapRuntimeChanged` 中那行重复。目前行为一致，没有 bug；但未来若“全局音量”还伴随其它 cheap runtime 副作用，两处容易漂移。可让 `SetGlobalVolume` 走同一 cheap 通知入口（或提取 `ApplyGlobalVolumeToRuntime()` 小函数）。

#### 5. 已激活的 Sustained 声音不会立即应用新的全局音量

- 文件：`Source/UniversalSqueaker/CompSqueaker.cs:743-746`、`CompSqueaker.cs:763-773`

`TryPlaySustained` 对已激活的 sustainer 直接返回 `Dispatched`，`ResolveMoodMod` 只在 spawn 时计算一次音量；因此拖动全局音量滑块时，当前正在播放的 Sustained 声音会保持旧音量直到该 sustainer 结束/重启。这与现有 mood/年龄调制行为一致，且任务书未要求实时更新，故不阻塞验收；建议确认产品语义并记录。

## 建议

1. **防御非有限浮点**：在 `VoicePacksPageModel.Execute` 的 `SetGlobalVolume` / `SetDistanceRange` 解析后，或在 `UniversalSqueakerSettings.SetGlobalVolume` / `SetDistanceRange` 入口，增加 `float.IsFinite` 校验；距离范围还可在 `ClampDistanceRange` 内对非有限输入回退到 `15..50` 默认值。
2. **补测试**：在 `tools/UniversalSqueakerSettingsMigrationTests` 中增加：
   - `SetGlobalVolume(0f)` 后 runtime 为 0、不改变 `voicePackMode`、不触发 `Disabled` 旁路的断言（可用桩/静态检查）；
   - `SetDistanceRange` 后 `CompSqueaker.ApplyDistanceRange` 生效的断言（反射读 `activeDistanceRange` 或检查已知 `SoundDef.subSounds[].distRange`）；
   - 可选：用注入桩断言 `QueuePersistence` 被调用。
3. **保持 no-op 幂等**：为 `SetGlobalVolume` / `SetDistanceRange` 增加“值未变化则早退”的守卫，与其它 setter 风格一致；若担心 `distancePreset` 语义，只对真正的范围变化置 `Custom` 并通知/保存。
4. **合并重复写入路径**：将 `CompSqueaker.GlobalVolumeFactor = Mathf.Clamp(...)` 提取为私有 helper，`ApplyToRuntime`、`NotifyCheapRuntimeChanged`、`SetGlobalVolume` 三处共用。
5. **确认 Sustained 音量语义**：若期望全局音量即时作用于当前 sustainer，需在 `MaintainSustainer` 中 End + 重启或查找 RimWorld `Sustainer` 的运行时音量更新 API；若不期望，则在 S4-Vol 文档/验收记录中写明“对已激活持续音在下一次触发时生效”。
