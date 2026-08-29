# Review S4-05 — Fallback / Robustness 功能区

## 结论

已按只读方式核对 HEAD `bf7342f` 下 Fallback / Robustness 重点文件：

- `Source/UniversalSqueaker/UI/VoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/VanillaVoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/Visuals/UsGuard.cs`
- `Source/FerriteLib.UiKit/Widgets/FerriteGuard.cs`
- `Source/UniversalSqueaker/UI/Widgets/*.cs` 的 DrawVanillaFallback

核心结论：

1. **L3 页面级兜底已接通**。`FerriteVoicePacksPage.Draw` 的 `try` 覆盖 `BuildView`、`GetEngine()`、`Layout.xml` 解析、布局测量/绘制和命令执行；任何异常都会进入 catch，先 `Log.Warning`，再调用 `VanillaVoicePacksPage.Draw(rect)`，兜底页自身失败后再画 `EmptyState`。`GetEngine()`/`ReadLayoutXml()` 异常确实能走同一 catch 路径。
2. **VanillaVoicePacksPage 满足“纯 Verse.Widgets 简化功能面”**。文件只引用 `System / Collections.Generic / Globalization / UnityEngine / Verse`，无 FerriteLib；覆盖模式四选、全局音量、衰减快速预设、彩蛋、三个缩放开关、相机指示、域选择、VoicePack 勾选；scope tree / mood tuning / preset import 按任务书省略。未发现它被设计成“第二个正式皮肤”。
3. **L1 Draw fallback 基本齐全**。列出的 US widget 与行级组件普遍有 `DrawOrFallback`，catch 后会先恢复 `Text.Font / Text.Anchor / GUI.color` 再画 fallback；`UsGuard` 的每会话 once log 正确挂在 `BeginSession/EndSession`。
4. **但 Robustness 仍有明显缺口**：`UsGuard.MeasureOrFallback` 的现有调用点没有真正包住可能抛异常的测量逻辑；`FerriteGuard.ResetSessionLog` 从未被调用；多个 widget 的 `Measure` 完全未防护；外层 catch 在持续故障时会每帧重复打日志；Ferrite 与 Vanilla 各持独立的 `VoicePacksPageState`，异常切换/恢复时 UI 状态可能不一致。

未发现 Blocker；共 4 个 Major、5 个 Minor、2 个 Nit。

## 发现

### Major

#### 1. `UsGuard.MeasureOrFallback` 的现有调用点没有保护真正的测量逻辑

- 文件：`Source/UniversalSqueaker/UI/Visuals/UsGuard.cs:21-33`
- 文件：`Source/UniversalSqueaker/UI/Widgets/GlobalVolumeWidget.cs:36-45`
- 文件：`Source/UniversalSqueaker/UI/Widgets/BasicTuningWidget.cs:40-51`
- 文件：`Source/UniversalSqueaker/UI/Widgets/CameraIndicatorWidget.cs:35-44`
- 文件：`Source/UniversalSqueaker/UI/Widgets/FilterBarWidget.cs:32-41`
- 文件：`Source/UniversalSqueaker/UI/Widgets/UsFooterWidget.cs:29-32`

这些 widget 都是先在外面完成全部可能抛异常的测量计算（`UsHelp.ResolveKey`、`UsHelp.BannerHeight`、`VoicePacksLayout.*`、`ctx.Metrics` 等），再调用：

```csharp
return UsGuard.MeasureOrFallback(() => height, height, Kind);
```

lambda 只返回一个已经算好的 `float`，不可能抛异常。也就是说 `MeasureOrFallback` 实际捕获不到任何测量异常，只提供了一个“看似有 fallback”的假保护。若测量阶段抛异常，仍会直接冒泡到页面 catch，降级为 L3 整页兜底，而不是 L2 组件级兜底。

建议改为把完整测量体放入 lambda：

```csharp
return UsGuard.MeasureOrFallback(() => {
    float height = ...;
    if (UsHelp.IsOpen(...)) height += ...;
    return height;
}, fallbackHeight, Kind);
```

#### 2. L2 测量兜底覆盖不完整：多个 US widget 与 FerriteLib 内置 widget 的 `Measure` 完全无防护

- 文件：`Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs:55-66`
- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:56-89`
- 文件：`Source/UniversalSqueaker/UI/Widgets/PresetListWidget.cs:43-78`
- 文件：`Source/UniversalSqueaker/UI/Widgets/RaceLayerWidget.cs:30-52`
- 文件：`Source/UniversalSqueaker/UI/Widgets/XenotypeLayerWidget.cs:29-51`
- 文件：`Source/UniversalSqueaker/UI/Widgets/VoicePackChecklistWidget.cs:30-58`
- 文件：`Source/UniversalSqueaker/UI/Widgets/PageTitleWidget.cs:30-45`
- 文件：`Source/FerriteLib.UiKit/Widgets/InputModeRowWidget.cs:34-67`
- 文件：`Source/FerriteLib.UiKit/Widgets/InputModeCardWidget.cs:34-45`
- 文件：`Source/FerriteLib.UiKit/Widgets/ChromeBannerWidget.cs:31-41`
- 文件：`Source/FerriteLib.UiKit/Widgets/ChromeFooterWidget.cs:28-38`
- 文件：`Source/FerriteLib.UiKit/Widgets/SectionHeaderWidget.cs:29-39`
- 文件：`Source/FerriteLib.UiKit/Widgets/EmptyStateWidget.cs:28-38`

上述 `Measure` 都没有用 `UsGuard.MeasureOrFallback` / `FerriteGuard.MeasureOrFallback` 包裹。`LayoutEngine.Measure`（`Source/FerriteLib.UiKit/Layout/LayoutEngine.cs:35-69`）会直接调用 `widget.Measure(ctx)`，因此任一 widget 的测量异常都会中断整个 Ferrite 页面，直接跳到 L3 整页兜底。这使“交互组件/信息组件”的 L2 覆盖在测量阶段不完整。

建议：为所有 US widget 的 `Measure` 接入有效的 `MeasureOrFallback`（同时修复 Major 1 的 lambda 写法）；FerriteLib 内置 widget 至少为当前 `Layout.xml` 实际使用的 `input/mode-row`、`chrome/banner` 补上测量保护。

#### 3. `FerriteGuard.ResetSessionLog` 从未被调用，“每会话 once log”对核心 widget 不成立

- 文件：`Source/FerriteLib.UiKit/Widgets/FerriteGuard.cs:15-17`
- 文件：`Source/UniversalSqueaker/UI/VoicePacksPage.cs:12-25`
- 文件：`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs:38`

`UsGuard.ResetSessionLog()` 在 `VoicePacksPage.BeginSession/EndSession` 中正确调用；但 `FerriteGuard.ResetSessionLog()` 是全仓库唯一没有被调用的方法，且 `FerriteGuard` 是 `internal`，US 程序集也无法从外部调用。`Layout.xml` 使用 `input/mode-row`（`Source/UniversalSqueaker/UI/Layout.xml:6-11`），其 `DrawOrFallback` 走 `FerriteGuard`。因此核心模式卡片一旦 fallback，日志抑制是“进程级 once”，不是“每会话 once”；跨会话后同一 widget 的第二次故障不会再有警告。

建议：在 FerriteLib 暴露一个 `ResetSessionLog()` 的公开入口（或由 `CoreWidgetRegistrar` 提供会话生命周期方法），并在 `VoicePacksPage.BeginSession/EndSession` 中调用；或在 FerriteLib 内部提供与 US 会话绑定的清理钩子。

#### 4. 外层 catch 在持续故障时每帧重复 `Log.Warning`，不是“一次”

- 文件：`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs:192-203`

`Layout.xml` 缺失/解析失败、`GetEngine` 持续抛异常等场景下，`Draw` 每帧都会进入 catch 并执行 `Log.Warning(...)`；RimWorld 的 `Log.Warning` 不会自动去重，会形成日志刷屏。任务书 P7 写的是“`Log.Warning` 一次”，当前实现没有会话级或故障级节流。

建议：为整页 catch 增加 per-session 的 once 标志（例如 `loggedPageFallbackThisSession`，在 `BeginSession/EndSession` 重置），或改用 `Log.WarningOnce` 类 API。

### Minor

#### 1. Ferrite 与 Vanilla 使用独立的 `VoicePacksPageState`，异常切换/恢复时 UI 状态不一致

- 文件：`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs:38`
- 文件：`Source/UniversalSqueaker/UI/VanillaVoicePacksPage.cs:21`
- 文件：`Source/UniversalSqueaker/UI/VoicePacksPage.cs:16-17,23-24`

两个页面各自持有 `private static readonly VoicePacksPageState State`，只在会话开始/结束时一起重置。若 Ferrite 路径在会话中途失败，用户会在 Vanilla 兜底页中操作（例如切换域、勾选包、搜索），这些命令只写入 Vanilla 自己的 state；若故障是瞬时/可恢复的，下一帧 Ferrite 恢复后仍使用旧的 Ferrite state，兜底页中的 UI 导航状态会丢失或与用户预期不一致。settings 本身不受影响，但属于状态污染/不一致路径。

建议：让 Ferrite 与 Vanilla 共享同一个页面 state 实例（例如由 `VoicePacksPage` 持有并注入），或在切换到兜底页时把 Ferrite state 克隆给 Vanilla、恢复时再合并回 Ferrite。

#### 2. 页面级 catch 没有先恢复 GUI 状态再画 Vanilla 兜底页

- 文件：`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs:192-198`
- 文件：`Source/UniversalSqueaker/UI/Visuals/UsGuard.cs:49-54`
- 文件：`Source/FerriteLib.UiKit/Widgets/FerriteGuard.cs:53-55`

`UsGuard`/`FerriteGuard` 在各自 catch 后会恢复 GUI 状态；但 Ferrite 整页 catch 本身没有做同样的 `Text.Font / Text.Anchor / GUI.color` 恢复。若异常来自未防护的 `Measure`/`Draw`/`DrawNav`/`DrawFooter` 且已改动 GUI 状态，随后绘制的 Vanilla 兜底页可能带着脏字体/锚点/颜色。

建议：在调用 `VanillaVoicePacksPage.Draw(rect)` 前统一执行一次 GUI 状态恢复（可提取为公共 helper），兜底页自身也应保证绘制结束后恢复状态。

#### 3. 部分交互 widget 的 Draw fallback 只是“不可用”文本，不是可操作控件

- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:101`
- 文件：`Source/UniversalSqueaker/UI/Widgets/PresetListWidget.cs:90`
- 文件：`Source/UniversalSqueaker/UI/Widgets/RaceLayerWidget.cs:64`
- 文件：`Source/UniversalSqueaker/UI/Widgets/XenotypeLayerWidget.cs:63`
- 文件：`Source/UniversalSqueaker/UI/Widgets/VoicePackChecklistWidget.cs:70`

`ScopeTreeWidget`（调音编辑器）与 `PresetListWidget`（预设导入）的 fallback 只是 `Label(... unavailable)`，交互能力完全丢失；容器级 `RaceLayerWidget`/`XenotypeLayerWidget`/`VoicePackChecklistWidget` 的整块 fallback 也只是文字，真正的可操作 fallback 只存在于更细的 `RaceLayerRow`/`XenotypeLayerRow`/`VoicePackRow` 层。若 L1/L2 的要求是“交互组件 fallback 后仍可操作”，这部分不达标；若按 P7“scope tree / mood / preset import 在兜底页省略”的设计意图，则应在文档/验收中明确这些组件只保证“不崩溃并给出 unavailable 提示”，而不是可操作 fallback。

#### 4. Vanilla 兜底页在 `BeginScrollView` 内使用 `rect.x + Padding`，依赖 `inRect.x == 0` 才正确

- 文件：`Source/UniversalSqueaker/UI/VanillaVoicePacksPage.cs:42-47`

`contentRect` 使用 `new Rect(0f, 0f, rect.width, ...)`，而内部绘制却用 `x = rect.x + Padding`。Unity/RimWorld 的 scroll view 内容坐标系通常以 0 为原点；当前 settings 传入的 `inRect` 一般 x=0 所以可用，但一旦调用方传入非零 x（或未来把兜底页嵌入子区域），所有控件会向右偏移并可能被裁剪。建议内部统一使用 `x = Padding`（内容坐标），与 `contentRect` 的 0 原点一致。

#### 5. FerriteLib 内置信息组件没有任何 `FerriteGuard` 包裹

- 文件：`Source/FerriteLib.UiKit/Widgets/ChromeBannerWidget.cs:31-63`
- 文件：`Source/FerriteLib.UiKit/Widgets/ChromeFooterWidget.cs:28-49`
- 文件：`Source/FerriteLib.UiKit/Widgets/SectionHeaderWidget.cs:29-51`
- 文件：`Source/FerriteLib.UiKit/Widgets/EmptyStateWidget.cs:28-51`

这些“信息组件”没有 Draw/Measure fallback。当前 US 页面只用到 `chrome/banner`，但库级组件一旦在其它宿主中抛异常，会直接破坏整个布局。若 L2 覆盖范围包含信息组件，这里应至少为 Draw 提供 vanilla 文本 fallback。

### Nit

#### 1. `EmptyState.Draw` 修改 `Text.Font` 后不恢复

- 文件：`Source/UniversalSqueaker/UI/Components/EmptyState.cs:13-20`

它保存并恢复了 `Text.Anchor` 和 `GUI.color`，但没有保存/恢复 `Text.Font`。在最终错误路径后可能把 `Text.Font` 留在 `GameFont.Small`。建议与其它 helper 一致地保存并恢复旧字体。

#### 2. `VanillaVoicePacksPage` 在 `settings == null` 时静默返回

- 文件：`Source/UniversalSqueaker/UI/VanillaVoicePacksPage.cs:33-34`

如果 Ferrite 路径因其它原因失败而 settings 又恰好为 null，兜底页会直接返回空白，而不是显示错误文案。建议与 Ferrite 路径一致地画 `EmptyState`。

## 建议

1. **修复 `UsGuard.MeasureOrFallback` 的调用方式**：把真实测量体放进 lambda，让 L2 测量 fallback 真正生效；同时为所有 US widget 的 `Measure` 统一接入。
2. **打通 FerriteGuard 会话生命周期**：在 FerriteLib 暴露 `ResetSessionLog()` 公开入口，并在 `VoicePacksPage.BeginSession/EndSession` 中调用；或让 FerriteLib 自身提供会话钩子。
3. **给整页 catch 加 once 节流**：`FerriteVoicePacksPage.Draw` 的 `Log.Warning` 应 per-session 只打一次，持续故障时只保留每帧 Vanilla 兜底绘制，不打日志刷屏。
4. **共享或同步页面 state**：让 Ferrite/Vanilla 使用同一个 `VoicePacksPageState`，或在切换兜底/恢复时双向合并，避免用户操作在两条路径间丢失。
5. **页面级 fallback 前恢复 GUI 状态**：在 `FerriteVoicePacksPage` catch 调 `VanillaVoicePacksPage.Draw` 前统一恢复 `Text.Font / Text.Anchor / GUI.color`；`EmptyState.Draw` 也补上字体恢复。
6. **明确 L2 交互 fallback 的验收口径**：若 ScopeTree/PresetList 的 fallback 允许只是 “unavailable” 提示，写入文档；若要求可操作，则补 vanilla 交互 fallback。
7. **修正 Vanilla 滚动坐标**：`VanillaVoicePacksPage` 内部绘制使用内容坐标 0 原点，避免依赖 `inRect.x == 0`。
8. **补齐 FerriteLib 内置信息组件的 Draw fallback**，至少覆盖 `chrome/banner` 与 `chrome/section-header`。
