# Review 05 — UI / Ferrite 审查报告

- 审查对象：`Source/UniversalSqueaker/UI/**`、`Source/UniversalSqueaker/UI/Model/**`、`Source/UniversalSqueaker/UI/Widgets/**`、`Source/FerriteLib.UiKit/**`、`tools/FerriteLib.UiKit.Tests/**`
- 审查基准：本地提交链至 `ca71d96`（双键 xeno context）；该提交未改 UI，本次重点核查 UI 与运行时 `(race, xeno)` 双键语义的一致性。
- 审查方式：静态阅读 + 交叉比对运行时 resolver/Pure 折叠/Settings 写桥/迁移逻辑；未执行长时间构建（避免并行审查锁冲突）。

## Scope

- UI 命令流：`UiCommand` → `VoicePacksPageModel` → settings 写桥 → resolver rebuild。
- 状态/视图投影：`VoicePacksPageState`、`VoicePacksViewState`、`VoicePacksPageModel`。
- Widget：`ScopeTreeWidget` 调音编辑器、`PresetListWidget`、相机指示器、布局、空态、窄窗守卫。
- 崩溃安全矩阵：空 catalog、无 Biotech、无 HAR、损坏设置、极窄矩形。
- FerriteLib.UiKit 中立性：无 US/SR 产品字面量。
- 测试：FerriteLib.UiKit.Tests 及可测 UI 逻辑。

## Files Checked

- `Source/UniversalSqueaker/UI/VoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/UsWidgetRegistrar.cs`
- `Source/UniversalSqueaker/UI/Layout.xml`
- `Source/UniversalSqueaker/UI/Model/UiCommand.cs`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageState.cs`
- `Source/UniversalSqueaker/UI/Model/VoicePacksViewState.cs`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`
- `Source/UniversalSqueaker/UI/Widgets/*.cs`
- `Source/UniversalSqueaker/UI/Components/*.cs`
- `Source/UniversalSqueaker/UI/Layout/*.cs`
- `Source/FerriteLib.UiKit/**/*.cs` 与 `FerriteLib.UiKit.csproj`
- `tools/FerriteLib.UiKit.Tests/*.csproj`、`Program.cs`
- 交叉引用：`Settings/UniversalSqueakerSettings.cs`、`Settings/UniversalSqueakerSettings.ExposeData.cs`、`Settings/BaselinePresetImporter.cs`、`Models/ActionTuningRecord.cs`、`Models/MoodTuningRecord.cs`、`Models/SqueakXenotypePresetModels.cs`、`Runtime/SqueakRuntimeResolver.cs`、`Pure/SqueakTuningDomains.cs`、`Catalog/SqueakXenotypeCatalog.cs`

## Findings

### Blocker

未发现会导致默认 Ferrite 路径必然崩溃的 blocker。

### Major

1. **Xenotype 域选择/高亮/状态仍是 race-blind，与运行时双键 `(race,xeno)` 不一致。**
   - 根因：`VoicePacksPageState.SelectedTargetName` 只保存 xenotype 名；`Execute(SelectDomain)` 丢弃 `RaceDefName`；`ResolveSelectedDomain` 的 Xenotype 分支只比较 `TargetDefName`；`XenotypeLayerWidget` 的高亮也只比较 `TargetDefName`。
   - 后果：同一 xenotype 被多个 race 声明时，UI 无法区分/导航到具体 `(race,xeno)` 域；多个 race 行会同时高亮，只能选中排序第一的 race。
   - 另一个 race-blind 点在 `UniversalSqueakerSettings.GetVoicePackSelectionStatus`：Xenotype 分支调用 `GetVoicePackDomainPacks(scope, target)` 时不按 `raceDefName` 过滤，导致同 xenotype 跨 race 的包会被当成当前域的候选包，Orphan 状态判断错误。
   - 建议：
     - `VoicePacksPageState` 增加 `SelectedRaceDefName`，或改用 `(scope, raceDefName, targetDefName)` 复合选择键。
     - `SelectDomain` / `ResolveSelectedDomain` / `XenotypeLayerWidget` / `XenotypeLayerRow` 全部按 race+target 匹配。
     - Xenotype 行显示名追加 race 后缀（如 `XenotypeLabel (defName) @ RaceLabel`）。
     - `GetVoicePackSelectionStatus` 增加 race 过滤，或提供 race-aware 的候选包查询。

2. **Race/Xenotype 调音层在“无域可选”时会误写 Global 层记录。**
   - 根因：`BuildTuningDomains` 在空 catalog/无层域时把 `TuningRaceDefName`/`TuningXenotypeDefName` 归一为空串；`ScopeTreeWidget` 仍渲染 scope/mood 行；点击后发出的命令 `race=""`、`xeno=""`，`SetActionTuningScope`/`SetMoodTuning` 会按“全空=Global”写入。
   - 后果：空 catalog 或损坏状态下游玩者可能把本应无效的 Race/Xenotype 编辑变成 Global 覆盖，甚至误清 Global 行。
   - 建议：
     - 当 `layer > 0` 且当前域不存在时，不渲染/禁用 scope 与 mood 交互；或在命令中携带 layer，`VoicePacksPageModel` 对非 Global 层要求 `raceDefName` 非空。
     - 至少应在 `ExecuteSetActionTuningScope` / `ExecuteSetMoodTuning` 中对“非 Global 命令但 race 为空”做 no-op 或日志。

3. **`SetActionTuningScope` 的 clear 只移除一条匹配记录，不能清掉重复/陈旧记录。**
   - 根因：`UniversalSqueakerSettings.SetActionTuningScope` 在 `scope == null` 时只 `Remove(record)`（最后一次扫描到的匹配项），而 `SetMoodTuning` 已改为 `RemoveAll`。
   - 后果：存在重复 `(actionKey, race, xeno)` 记录时，UI 点 Auto 只删最后一条，旧记录仍会生效，表现为“清不掉”。
   - 建议：clear 分支改为 `RemoveAll(...)`，与 `SetMoodTuning` 对齐；同步审查 `BaselinePresetImporter.UpsertAction` 的“替换第一条”策略。

4. **迁移后的 `xenotypePresets.actionOverrides` 仍被运行时消费，UI 清空/编辑无法真正移除旧覆盖。**
   - 根因：`SqueakRuntimeResolver.BuildBehavior` 仍遍历 `settings.xenotypePresets` 的 `actionOverrides` 作为 Xenotype 层源；迁移只把数据复制进 `actionTuning`，并未清空或停用旧字段。`SetActionTuningScope` 只操作 `actionTuning`。
   - 后果：pre-v4 老档迁移后，如果玩家在 UI 清掉某条 xeno action 调音，旧 `xenotypePresets.actionOverrides` 仍会让该动作在运行时保持旧覆盖；同时 `BuildActionScopes` 的投影不读 `xenotypePresets`，UI 显示“已清/继承”但运行时不生效。
   - 建议：
     - 运行时不再消费 `xenotypePresets.actionOverrides`（`actionTuning` 是唯一权威层表）。
     - 迁移时一次性清空或删除 `actionOverrides`，避免双写；若需保留加载兼容，至少在 schema v5+ 中忽略该字段。
     - 若决定继续兼容旧字段，则 UI 投影和写桥都必须把 `xenotypePresets.actionOverrides` 作为 base 层处理。

5. **调音域集合遗漏“仅存在于调音记录/旧 preset”的 `(race,xeno)` 域。**
   - 根因：`BuildTuningDomains` 只收集 catalog 包声明 + voicePackSelections；而运行时 `CollectXenoDomains` 还包含 `behavior.Keys`（即 `actionTuning`/`moodTuning`/`xenotypePresets` 产生的域）。
   - 后果：已有 xeno 调音记录但无对应 VoicePack/selection 时，运行时存在该 context，UI 的 Xenotype 层却看不到该域，无法编辑或清空。
   - 建议：`BuildTuningDomains`（以及可能需要的 `BuildXenotypeDomains`）把 `actionTuning`、`moodTuning`、`xenotypePresets` 的 `(race,xeno)` 键并入源集合。

6. **Legacy `UseFerriteUi=false` 路径的 `MeasureContentHeight` 漏算多块内容。**
   - 根因：`VoicePacksLayout.MeasureContentHeight` 未包含 Help 展开、Easter egg、Distance preset、三行 basic tuning（也未包含 Xenotype layer 列表）。
   - 后果：`VoicePacksPage.Draw` 在 legacy 路径用该高度设置 scroll content，实际绘制内容更高，底部会被裁切/滚动区错误。Ferrite 路径不受影响，但旧路径仍被保留。
   - 建议：同步更新 `MeasureContentHeight`，或删除 legacy 路径；若保留，最好与 Ferrite widget 高度计算共用同一套行高常量。

7. **Baseline preset 的 xenotype 勾选是 race-blind。**
   - 根因：`BaselinePresetSelection.SelectedXenotypeDefNames` 只存 xenotype 名；`BuildBaselinePresets` 用同一名字标记所有 race 下的同 xeno 行；`ImportBaselinePreset` 也只把 xenotype 名传给 importer。
   - 后果：同一 xenotype 出现在多个 race 下时，勾选一个 race 的行会让所有同 xeno 行同时显示已选，导入也会应用到所有含该 xeno 的 race；无法按 `(race,xeno)` 增量导入。
   - 建议：将 `BaselinePresetSelection` 与 `BaselinePresetImporter.Selection` 的 Xenotype 选择改为 `(raceDefName, xenotypeDefName)` 复合键（或明确 UI 文案为“应用到所有 race”，并去掉按 race 展开的误导）。

### Minor

1. **Checklist 测量与绘制使用不同 metrics 实现。**
   `VoicePackChecklistWidget.Measure` 使用 `FerriteTextMetricsAdapter`，而 `VoicePackChecklist.Draw` 内 banner 高度使用 `VerseTextMetrics.Instance`；字体/当前 Text.Font 状态不同可能导致 measure/draw 高度不一致。
   建议统一传入同一 `ITextMetrics`。

2. **窄窗守卫不完整。**
   `ScopeTreeWidget.DrawLayerRow`/`DrawDomainRow`/`DrawScopeRow` 多处用未 clamp 的 `rect.width - ...` 计算负宽度；`BasicTuningWidget`/`CameraIndicatorWidget` 的 label 宽度也未 `Math.Max(1f, ...)`。极窄 rect 下可能绘制异常。
   建议为所有内部 rect 宽度做 `Math.Max(1f, ...)`，并在 `ScopeTreeWidget` 增加最小宽度早退（mood 簇已有 86px 守卫，可推广）。

3. **`VoicePacksPageModel.BuildView` 在“只读投影”中写回 `state.TuningRaceDefName`/`TuningXenotypeDefName`。**
   这让投影带副作用，难以测试，也可能在异常帧留下半更新状态。
   建议将归一化结果作为返回值/独立纯函数，调用方再回写 state。

4. **无 Biotech 时仍允许编辑 Xenotype 调音层。**
   运行时在无 Biotech 下不会构建 xeno context；UI 仍可写 layer-2 记录，虽然不 crash，但玩家会以为生效。建议显示 dormant 提示或禁用该层编辑。

5. **`VoicePacksPage.EndSession` 无条件重置 Ferrite 状态。**
   即使 `UseFerriteUi=false` 也会调用 `FerriteVoicePacksPage.EndSession()`；当前无实际危害，但路径耦合不干净。

6. **UI 逻辑缺少单元测试。**
   `FerriteLib.UiKit.Tests` 只覆盖 registry/manifest/layout engine；`VoicePacksPageModel` 的层折叠投影、`SetActionTuningScope`/`SetMoodTuning` 写桥、命令翻译、窄窗行为都没有测试。
   建议把 `BuildActionScopes`/`BuildMoodTuningRows`/`BuildTuningDomains` 中不依赖 Verse 的部分提取为纯函数并加测试。

### Nit

- `VoicePacksViewState.ActionScopeRowView` 注释“HasOwnScope=false 时 Scope 无意义”与代码中 `Scope` 仍保留默认值并存，建议注释更精确。
- `InputModeRowWidget` 注释写“two or three”，实现支持任意数量，建议改注释。
- `XenotypeLayerRow` 的 `arg: domain.TargetDefName` 未被使用，可清理。

## FerriteLib.UiKit 中立性

- 对 `Source/FerriteLib.UiKit` 与 `tools/FerriteLib.UiKit.Tests` 做了产品字面量 grep：未发现 `UniversalSqueaker`、`SqueakyRatkin`、`Ratkin`、`SR_`、`US_` 等命中。
- 库仍按设计引用 `Verse/UnityEngine` IMGUI 类型，`csproj` 使用 `Krafs.Rimworld.Ref`；这与 MEMORY 中“生产用 Verse、测试用 stub”的边界一致。
- 测试覆盖了 registry 回退、未知 kind、manifest 安全解析（XXE/畸形 XML）、layout engine 固定高度/隐藏元素/scroll clamp；未覆盖“measure 后 view width 变化再 draw”的缓存失效场景，建议补一条。

## Maintainer Decisions Needed

1. **旧 `xenotypePresets.actionOverrides` 的最终处置**：是否在 v5/v6 迁移中彻底停用/清空？推荐停用并让 `actionTuning` 成为唯一 Xenotype 层动作源。
2. **Legacy UI 去留**：`UseFerriteUi=false` 路径是否继续维护？若继续，需要补齐 `MeasureContentHeight`；否则建议删除以降低双路径漂移。
3. **Baseline preset 的 Xenotype 选择语义**：按 `(race,xeno)` 独立勾选，还是“按 xeno 名全局应用”？推荐前者，与运行时双键一致。
4. **同 xeno 多 race 的 UI 呈现**：确认 Xenotype Layer 应显示为独立 `(race,xeno)` 行并带 race 后缀。

## Suggested Verification Commands

- `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release`
- `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev`
- `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release`
- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev`
- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release`
- `pwsh -File scripts/verify-local.ps1`（含 neutrality grep、UiKit tests/build、layout XML 检查）
- 若新增 UI 纯函数测试后：`dotnet run --project tools/UniversalSqueakerKernelTests -c Release`（确认 Pure 折叠未回归）
- 游戏内手动矩阵（maintainer step）：
  - 空 catalog（无任何 VoicePack）
  - 无 Biotech 但有 xeno selection/调音记录
  - 同一 xenotype 被多个 race 声明
  - pre-v4 迁移配置后在 Xenotype 层点 Auto 清动作 scope，确认运行时不再发声/恢复继承
  - 极窄设置窗口（< 400px）查看 ScopeTreeWidget/BasicTuning 是否重叠或异常
