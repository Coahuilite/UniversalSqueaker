# 外部 DeepSeek 任务书：最终候选恢复、收口与 Gate 审查

## 0. 任务性质

这是一次**棕地恢复 + 最终候选收口**，不是从零重写，也不是按文件机械补齐。

当前工作区包含：

1. 已通过维护者实机验证的 Gate R 与 Basic Settings Kernel 路径；
2. 尚未提交的完整 Settings Kernel 迁移；
3. 一个被中止的内置通用代理留下的 Tuning/Packs harness 扩展和 Overlay 第二宿主改动；
4. 旧 Settings 页面、旧 `UiInteract` 与命令桥作为实机前 fallback。

被中止代理曾报告构建和 15 门验证通过，但主代理没有独立验收该最终波次。**不得把其报告当作事实；必须从当前文件和可运行结果重新建立证据。**

## 1. 已确认事实

### 1.1 维护者实机事实

维护者已用 Overlay 最终波次之前的 dev 包确认：

- 设置页面正常开启；
- Global Volume 正常修改；
- attenuation graph 可拖动；
- 关闭后重开正常；
- 无红字。

这证明当时的真实 Settings Host、Basic typed 写入、chart 原生拖拽和 session 重开成立。不要重复质疑该事实；但它**不证明**后来新增的 Overlay、Tuning/Packs rich-data harness 或其他最终波次改动。

### 1.2 当前工作区基线

内置代理启动前：

- tracked 修改：19 项；
- untracked 顶层项：16 项。

内置代理中止后：

- tracked 修改：21 项；
- untracked 顶层项：21 项；
- `git diff --stat`：21 个 tracked 文件，约 `+779/-64`。

最终波次新增或修改至少包括：

- `Source/UniversalSqueaker/UI/Layout.Overlay.Schema2.xml`
- `Source/UniversalSqueaker/UI/IUsKernelOverlaySource.cs`
- `Source/UniversalSqueaker/UI/UsKernelOverlaySource.cs`
- `Source/UniversalSqueaker/UI/UsKernelOverlayController.cs`
- `Source/UniversalSqueaker/UI/UsCameraIndicatorOverlay.cs`
- `Source/UniversalSqueaker/UI/Kernel/UsCameraReadoutWidget.cs`
- `Source/UniversalSqueaker/UI/Kernel/UsKernelOverlayHost.cs`
- `Source/UniversalSqueaker/Patches/Patch_GlobalControlsUtility_CameraIndicator.cs`
- `Source/UniversalSqueaker/Patches/Patch_Root_DiagnosticsLifecycle.cs`
- `Source/UniversalSqueaker/UI/Kernel/UsKernelWidgetRegistrar.cs`
- `Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs`
- `Source/UniversalSqueaker/UniversalSqueaker.csproj`
- `tools/UniversalSqueakerKernelHostTests/Program.cs`
- `tools/UniversalSqueakerKernelHostTests/RecordingSettingsSource.cs`
- `tools/UniversalSqueakerKernelHostTests/RecordingOverlaySource.cs`
- `tools/FerriteLib.UiKit.Tests/Stubs/VerseStub/VerseStubs.cs`
- `TODO.md`
- `HANDOFF.md`

当前改动全部未提交。不要 reset、checkout、clean、stash 或覆盖整棵工作区。

## 2. 必读文件

按顺序读取：

1. `AGENTS.md`
2. `MEMORY.md`
3. `TODO.md`
4. `HANDOFF.md`
5. `docs/uikit-rebuild/README.md`
6. `docs/uikit-rebuild/01-product-and-architecture-decisions-zh.md`
7. `docs/uikit-rebuild/02-brownfield-cutover-matrix-zh.md`
8. `docs/uikit-rebuild/03-execution-dag-and-agent-map-zh.md`
9. `docs/uikit-rebuild/04-verification-and-acceptance-zh.md`
10. `docs/uikit-rebuild/05-p0-contract-baseline-zh.md`
11. `docs/uikit-rebuild/07-rebuild-reset-and-execution-contract-zh.md`
12. `docs/uikit-rebuild/tasks/MAIN-ORCHESTRATOR.md`
13. `docs/uikit-rebuild/tasks/DEEPSEEK-US-SETTINGS-MIGRATION.md`
14. `docs/uikit-rebuild/tasks/AGENT-OVERLAY.md`
15. `docs/uikit-rebuild/tasks/DEEPSEEK-GATE-REVIEW.md`

如文档状态与源码或运行证据冲突，以源码和可重复运行证据为准，并修正文档；不得反过来修改代码以迎合过时文档。

## 3. 总目标

把当前恢复现场收敛为一个可由主代理独立验收和打包的最终实机候选：

```text
Settings Window
  -> one Settings Host
  -> one Settings UiSession
  -> validated real Schema2 tree
  -> synchronous native IMGUI
  -> typed US settings/model boundary

Camera Indicator / diagnostics surface
  -> independent Overlay lifecycle
  -> one Overlay Host
  -> one independent Overlay UiSession
  -> real Overlay Schema2 tree
  -> same registry / binding / theme contract
  -> native IMGUI
```

必须同时完成：

- 恢复和审计被中止代理留下的全部改动；
- Tuning/Packs 动态业务路径收口；
- Camera Overlay 第二宿主收口；
- 自动证据补齐；
- 旧路径 clean-cutover inventory；
- 最终 Gate 报告。

## 4. 第一阶段：恢复审计，先于继续实现

### 4.1 检查文件完整性

被中止代理历史中发生过多次失败编辑和随后修复。必须重点检查：

- `UsKernelSettingsHost.cs` 是否存在重复/残缺翻译类、错误构造参数或丢失尾部；
- `UsKernelWidgetRegistrar.cs` 是否完整注册 Settings 的 footer/help panel 和 Overlay 的 camera readout，且无重复注册；
- 两个 Camera/Root Harmony patch 是否存在重复类、重复方法、同帧双绘制或丢失原有 guard；
- `UniversalSqueaker.csproj` 是否同时嵌入 Settings Schema2 与 Overlay Schema2，未误删旧 fallback resource；
- `RecordingSettingsSource.cs`、`Program.cs`、`VerseStubs.cs` 是否有重复块、孤立代码、丢失方法头、测试专用生产分支或过量异常打印；
- 所有新增 Overlay 文件的 namespace、可见性、nullable、生命周期和资源名是否一致。

### 4.2 先运行事实基线

在继续修改前运行 focused 基线：

```text
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev --no-restore --nologo -v minimal
dotnet run --project tools/UniversalSqueakerKernelHostTests -c Release --no-restore
```

若失败，先修复恢复现场；不要扩大范围。记录原始失败、根因和修复。

## 5. 第二阶段：Tuning/Packs 最终收口

### 5.1 Tuning 必须覆盖

- layer 切换；
- race/xenotype domain 选择；
- action scope；
- mood factor stepper/slider；
- baseline preset 展开、race/xenotype 选择和 import；
- 所有会改变自然高度或可见列表的写入触发 `ContentRevision`；
- 写入正确 `(race, xenotype, layer, domain)` 业务层级。

### 5.2 Packs 必须覆盖

- race/xenotype/author filter；
- dropdown 打开、选择、关闭与 scroll/window 坐标；
- domain selection；
- pack toggle；
- forget unavailable；
- race/xenotype 动态行；
- checklist 动态列表；
- search/filter 后布局缓存失效。

### 5.3 强制边界

- 所有持久业务真相归 US settings/model；
- `UiSession` 只持有 scroll、popup、focus、drag、编辑缓冲、revision 和 fallback；
- XML 不得新增 Repeat、条件、表达式或业务脚本；
- 新路径不得调用旧 `UiInteract`、`UiCommand` 或 `UsCommandPayload`；
- 不得把 recording source 的行为复制进生产实现；
- 动态列表必须由 C# composite widget 迭代。

## 6. 第三阶段：Overlay 第二宿主收口

### 6.1 必须成立

- 无 Settings Window 时 Overlay 可独立创建和绘制；
- Overlay 拥有独立 `UiHost/UiSession`；
- Settings 关闭不影响 Overlay；
- Overlay 禁用、无 map、地图切换或宿主结束时完整 Dispose；
- Overlay 使用真实嵌入 `Layout.Overlay.Schema2.xml`；
- 复用相同 registry、typed binding、theme、translation 与 native input contract；
- 800×600、1280×720、1920×1080 安全区布局可解释；
- 保留原有 `Find.CurrentMap == null`、Layout event、GUI state 恢复等安全 guard。

### 6.2 纯 Verse fallback

实机验证前允许保留原 Camera Indicator Verse 路径，但必须：

- Kernel 与 Verse fallback 同一帧互斥；
- fallback 只在 Host 创建/绘制失败或明确不可用时进入；
- 不形成长期并行视觉层；
- 关闭/禁用后不遗留 Host/session；
- 文件级说明保留理由和删除条件。

不得猜测 RimWorld 坐标偏移。真实引擎才能裁决的问题标记 `LIMITED`。

## 7. 第四阶段：失败敏感自动证据

扩展或修复 `tools/UniversalSqueakerKernelHostTests`，但必须直接使用生产代码：

- 真实 `UsKernelSettingsHost.Create`；
- 真实 `UsKernelOverlayHost`/controller 创建路径；
- 真实嵌入 Settings/Overlay Schema2；
- 真实 widget registry；
- 生产 typed binding/action 表。

最低覆盖：

1. Basic/Tuning/Packs 三 Tab × 800/1280/1920；
2. rich 动态 Tuning/Packs 数据进入真实 widgets；
3. 所有高风险 typed writes 到达 recording 业务边界；
4. popup/scroll/hotControl/session 隔离和 Dispose 清理；
5. Overlay 无 Settings Window 创建；
6. Settings 与 Overlay session 隔离；
7. Overlay 显示/隐藏、无 map、安全区和 Dispose；
8. 未知 Kind/属性、缺失或错型 binding 创建期失败；
9. 两个 Schema2 resource 都实际嵌入生产 DLL。

Stub 只能证明调用契约和纯逻辑。不得声称它证明真实 RimWorld 视觉、翻译、FloatMenu 或热区。

## 8. 第五阶段：clean-cutover inventory

本任务**不直接删除**旧 fallback。输出精确 inventory：

- `UI/Layout.xml`
- `FerriteVoicePacksPage`
- `VanillaVoicePacksPage`
- 旧 `UiInteract`
- `UiCommand` / `UsCommandPayload` / adapter
- 旧 page session/state 中只服务旧路径的字段
- 重复视觉 helper、card、palette、layout engine glue
- `DoSettingsWindowContents` 与 legacy fallback 调用点
- 旧 tests/stubs/docs

对每项给出：

```text
File/symbol:
Current callers:
Why retained now:
Deletion prerequisite:
Deletion order:
Replacement:
Post-delete verification:
```

目标是最终实机通过后一次 clean cutover，不保留兼容 shim。

## 9. 验证

实现完成后按顺序运行：

```text
dotnet run --project tools/UniversalSqueakerKernelHostTests -c Release --no-restore
dotnet run --project tools/FerriteLib.UiKit.Tests -c Release --no-restore
dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release --no-restore
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev --no-restore --nologo -v minimal
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release --no-restore --nologo -v minimal
pwsh -File scripts/verify-local.ps1
```

不得只报告末尾摘要。若任何门失败，修根因后重跑受影响门，最终再跑完整 `verify-local.ps1`。

不要打包、提交、配置 remote、push 或发布；主代理负责最终验收和打包。

## 10. 停止条件

出现以下任一情况，停止扩大范围并报告：

- 当前恢复现场无法确定哪个实现是权威；
- 一个修复需要恢复全局 deferred dispatch、字符串二次命令桥或第二套 hot-control；
- Overlay 只能通过猜坐标或复制旧路径完成；
- Settings 与 Overlay 被迫共享 session；
- 测试只能通过测试专用生产分支成立；
- fallback 可能同帧双绘制或遗留 group/clip/hotControl；
- 真实 RimWorld 才能裁决的问题被误写为自动 PASS。

## 11. 回报格式

严格按以下格式返回：

```text
Implemented goal:
Recovered files and defects:
Settings production call graph:
Overlay production call graph:
Business ownership:
Contract decisions:
Tuning/Packs evidence:
Overlay evidence:
Commands and exact results:
Clean-cutover inventory:
Retained paths with reasons:
Result: PASS / LIMITED / FAIL
Remaining uncertainty:
Next real-game acceptance checklist:
Files changed:
```

其中：

- `PASS` 只能用于自动/源码范围；
- 没有真实 RimWorld 证据时，整体结果必须为 `LIMITED`；
- 不得把被中止代理的历史自报结果复制为新证据；
- 每项证据必须来自本次重新运行或当前源码审计。
