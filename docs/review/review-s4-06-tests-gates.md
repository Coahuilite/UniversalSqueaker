# Review S4-06 — Tests / Gates 功能区

## 结论

已按只读方式核对 HEAD `bf7342f` 下 Tests / Gates 重点文件：

- `scripts/verify-local.ps1`
- `tools/UniversalSqueakerUiLogicTests/Program.cs` / `.csproj`
- `tools/UniversalSqueakerSettingsMigrationTests/Program.cs` / `.csproj`
- `tools/FerriteLib.UiKit.Tests/Program.cs` / `.csproj`

总体结论：

1. **新增功能的“核心断言”基本都已落地**：
   - global volume 默认 / setter clamp / 运行时同步 / 合法值 round-trip：`UniversalSqueakerSettingsMigrationTests/Program.cs:308-352`。
   - distance range clamp + 手动编辑置 `Custom`：`Program.cs:354-378`。
   - filters：`UniversalSqueakerUiLogicTests/Program.cs:91-162`。
   - help keys：`FerriteLib.UiKit.Tests/Program.cs:269-286`。
   - responsive tier：`UniversalSqueakerUiLogicTests/Program.cs:35-43`。
   - InputModeRow 响应式列数：`FerriteLib.UiKit.Tests/Program.cs:245-267`。
2. **verify-local.ps1 门禁结构合理**：包含 Kernel、ConfigCopy、SettingsMigration、Log Release/Dev、FerriteLib.UiKit.Tests、FerriteLib Dev/Release 构建、neutrality grep、主程序集 Dev/Release 构建、产物存在、Layout.xml well-formedness、UiLogicTests。`Invoke-Check` 在失败时 `exit 1`，可确保非零退出。顺序从纯逻辑/迁移到构建再到 UI 逻辑测试，fail-fast 合理。
3. **存在 3 个应补齐的 Major 测试缺口**：`UsGuard`/`FerriteGuard` 注入异常测试缺失、`AttenuationEditorWidget` 拖拽纯函数未测试、`LayoutEngine` 宽度变化后 Draw 缓存失效回归测试缺失（后者是计划中明确列出的补测项）。
4. 未发现 Blocker；部分断言偏弱、部分静态全局状态使测试存在轻度顺序/并发脆弱性，详见发现。

## 发现

### Major

#### 1. `UsGuard` / `FerriteGuard` 注入异常测试缺失

- 文件：`tools/UniversalSqueakerUiLogicTests/UniversalSqueakerUiLogicTests.csproj:19-23`
- 文件：`tools/FerriteLib.UiKit.Tests/Program.cs:39-67`
- 相关计划：`docs/s4-polish-plan-zh.md:286`

S4-P1/P7 的组件级 fallback 是核心鲁棒性承诺，但当前没有任何自动化测试验证：
`MeasureOrFallback` / `DrawOrFallback` 捕获异常后恢复 `Text.Font / Text.Anchor / GUI.color`、调用 vanilla fallback delegate、并按 widget 每会话只 log 一次。`UsGuard` 是 `internal`，也没有任何测试项目通过反射/stub 驱动它。结合 `review-s4-05` 已发现“多个 `MeasureOrFallback` 调用点没有真正包住可能抛异常的测量逻辑”，这一块尤其需要测试来锁住行为。

#### 2. `AttenuationEditorWidget` 拖拽/纯函数未测试

- 文件：`Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs:158-169,276-305`
- 文件：`tools/UniversalSqueakerUiLogicTests/UniversalSqueakerUiLogicTests.csproj:19-23`

`CardColumns`、`SanitizeRange`、`ChartX`、`DistanceFromX`、`FormatRange` 都是确定性纯数学逻辑，但均为 `private static` 且所在文件依赖 Verse/Unity，因此没有被任何测试覆盖。S4-Vol 的核心交互（拖拽端点、5 单位最小间距、15–65 横轴映射、Custom 落盘格式）目前只能靠游戏内人工验证。建议把这类纯函数提取到零 Verse 文件并链接进 `UniversalSqueakerUiLogicTests`。

#### 3. `LayoutEngine` 宽度变化后的 Draw 缓存失效回归测试缺失

- 文件：`tools/FerriteLib.UiKit.Tests/Program.cs:155-267`
- 文件：`Source/FerriteLib.UiKit/Layout/LayoutEngine.cs:108-113`
- 相关计划：`docs/s4-polish-plan-zh.md:285`

`LayoutEngine.HasUsableCache` 在 `measuredViewWidth != viewWidth` 时会重新 `Measure`，这是 P6 响应式的关键路径。当前测试只做了“同一宽度 Measure → Draw”和“连续不同宽度 Measure”，没有覆盖“先 Measure(600) 再 Draw(300)”时 Draw 内部自动重测并使用新宽度矩形。计划明确要求补这一条 review-05 Nit，当前未落地。

### Minor

#### 1. distance range clamp 断言只查不变量，未锁精确值

- 文件：`tools/UniversalSqueakerSettingsMigrationTests/Program.cs:361-377`

`SetDistanceRange` 的断言只检查 `min >= 15 && max <= 65 && max >= min + 5`。对于 `(10, 80)`、`(70, 70)`、`(20, 22)`，即使实现变成 `(15, 65)` 或 `(60, 65)` 也会通过。建议对典型输入断言精确 `FloatRange`，例如 `(10,80) -> (15,65)`、`(70,70) -> (60,65)`、`(20,22) -> (20,25)`，以真正锁住 clamp 行为。

#### 2. 持久化加载时的 clamp 没有测试

- 文件：`Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.ExposeData.cs:108-112`
- 文件：`tools/UniversalSqueakerSettingsMigrationTests/Program.cs:332-352`

当前 round-trip 只保存并加载合法值 `0.42`，没有测试“磁盘里已有 `globalVolumeFactor=2` / `distanceRange=(0,100)` 这类越界旧值时，`PostLoadInit` 会 clamp”。且 stub 的 `FinalizeLoading` 只对深对象跑 `PostLoadInit`，根 `UniversalSqueakerSettings` 的 PostLoadInit 路径没有被驱动，因此加载侧 clamp 实际未覆盖。

#### 3. SettingsMigration 测试共享静态全局状态，且使用固定临时文件路径

- 文件：`tools/UniversalSqueakerSettingsMigrationTests/Program.cs:315-351`
- 文件：`tools/UniversalSqueakerSettingsMigrationTests/Program.cs:337`
- 文件：`tools/UniversalSqueakerSettingsMigrationTests/Stubs/VerseStubs.cs:180-276`

`CompSqueaker.GlobalVolumeFactor`、`Scribe.mode`、`Scribe.loader`、`ScribeExtractor.PostLoadInitQueue` 都是进程级静态状态；当前用例顺序固定所以能跑，但新增测试时容易产生顺序依赖。`GlobalVolumeScribeRoundTrip` 使用固定临时路径 `us-settings-global-volume-roundtrip.xml`，若多个测试进程并发执行会互相覆盖。

#### 4. `verify-local.ps1` 对测试项目的 warning/restore 控制不完整

- 文件：`scripts/verify-local.ps1:35-36,59-138`

`-NoRestore` 只传给 `dotnet build`，`dotnet run` 的测试门仍可能隐式 restore；测试项目也没有以 `TreatWarningsAsErrors` 构建。主程序集 0 警告有门禁，但测试代码自身的警告不会导致门禁失败。

#### 5. 第 14 门描述已过时

- 文件：`scripts/verify-local.ps1:25,136`

门禁名仍写“pure UI filters + distance preview”，但实际已包含 `UiLayoutTier` / responsive tier 测试。建议更新为“pure UI filters + distance preview + layout tier”。

#### 6. Vanilla 页“无法单测”的说明没有沉淀到测试/门禁侧

- 文件：`scripts/verify-local.ps1:126-138`
- 相关计划：`docs/s4-polish-plan-zh.md:395-396`

计划中已说明 `VanillaVoicePacksPage` 的渲染正确性进 maintainer 游戏内矩阵，但 `verify-local.ps1` 和测试项目里没有任何注释/占位说明“该页面不做自动单测、依赖游戏内矩阵”。后续维护者容易误以为测试缺口。

#### 7. 零 Verse 的 `VoicePacksLayout` 纯函数仍未被测试

- 文件：`Source/UniversalSqueaker/UI/Layout/VoicePacksLayout.cs:30-81`
- 文件：`tools/UniversalSqueakerUiLogicTests/UniversalSqueakerUiLogicTests.csproj:19-23`

`BannerHeight`、`CountShownPacks`、`ChecklistHeight`、`InnerWidth` 等是零 Verse 纯函数且被大量 UI 高度计算使用，但没有链接进 `UniversalSqueakerUiLogicTests`。它们不是 S4 新增的核心项，但属于明显的可测未测区域。

### Nit

#### 1. 成功后临时日志文件未清理

- 文件：`scripts/verify-local.ps1:53-56`

`Invoke-Check` 只在失败时 `Remove-Item $tempLog`，全部成功后会在 `%TEMP%` 留下一个 `us-verify-*.log`。

#### 2. neutrality grep 的 retry 提示不准确

- 文件：`scripts/verify-local.ps1:92`

“FerriteLib.UiKit neutrality grep”的 retry 建议是 `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release`，但该命令并不会重新执行 grep。retry 文案应改为重新运行整个 `verify-local.ps1`。

#### 3. FerriteLib 测试遇到未捕获异常后会静默跳过剩余用例

- 文件：`tools/FerriteLib.UiKit.Tests/Program.cs:17-37`

`RunAll` 中途抛异常时 `Main` 只打印 `UNHANDLED` 并累计 1 个 failure，后续测试全部不执行，但输出没有明确“aborted”提示。建议在 catch 中打印 `RunAll aborted after ...`。

## 建议

1. **补齐 `UsGuard` / `FerriteGuard` 注入异常测试**：在 stub 环境用可抛异常 delegate 验证异常 → GUI 状态恢复 → fallback delegate 被调用 → 同一 widget 第二次异常不再 log；同时覆盖 `MeasureOrFallback` 与 `DrawOrFallback`。
2. **提取并测试 `AttenuationEditorWidget` 的纯数学**：将 `SanitizeRange` / `ChartX` / `DistanceFromX` / `CardColumns` / `FormatRange` 移入零 Verse 文件，链接进 `UniversalSqueakerUiLogicTests`，并对典型边界（min/max/交换/NaN/最小间距/极窄宽度）做精确断言。
3. **补 `LayoutEngine` 缓存失效回归**：`Measure(600)` 后直接 `Draw(300)`，断言 draw rect 的宽度和重新计算后的高度都是 300 对应的值。
4. **加强 SettingsMigration 断言**：distance range 对代表输入断言精确 `FloatRange`；增加一个通过 Scribe 加载越界 `globalVolumeFactor` / `distanceRange` 并执行根 PostLoadInit clamp 的用例。
5. **隔离测试全局状态**：为 `CompSqueaker` 静态字段、`Scribe`/`ScribeExtractor` 提供 reset helper；round-trip 临时文件改用 GUID 路径，避免并发/顺序污染。
6. **完善 verify-local.ps1**：给 `dotnet run` 也传递 `--no-restore`（当 `-NoRestore` 时），考虑测试项目也启用 warnings-as-errors；更新第 14 门名称；成功后删除临时日志；修正 neutrality grep retry 文案。
7. **记录已知测试边界**：在 `verify-local.ps1` 或测试项目注释中明确 `VanillaVoicePacksPage` 不做自动单测、由 maintainer 游戏内矩阵覆盖，避免后续误判。
8. **可选扩展**：把 `VoicePacksLayout` 的零 Verse 纯函数纳入 `UniversalSqueakerUiLogicTests`，提升页面高度计算的回归保护。
