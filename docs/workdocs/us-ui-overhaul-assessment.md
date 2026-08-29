# US UI 大修评估（基于 FerriteLib.UiKit 当前状态）

> 状态：评估完成，待实施。
> 前置：FerriteLib.UiKit 已完成 B1–B4（分层交互路由 + 值控件 + 滑条/数值复合控件），当前为未提交工作树改动；`FerriteLib.UiKit.Tests` ALL PASS，Dev/Release 零警告，US Dev 构建零警告。
> 结论：US UI 不需要推倒重写，但需要一次**系统性迁移 + backlog 修复**。核心是把 US 侧仍直接调用 Verse IMGUI 输入控件的地方迁到 `UiInteract`/`UiValueStore`，同时处理 S4-Polish review backlog。

## 1. UiKit 当前能力（已核实）

| 能力 | 位置 | 状态 |
|---|---|---|
| 每帧交互注册 + 延迟派发 | `Source/FerriteLib.UiKit/Interaction/UiInteract.cs` | ✅ 已实现 |
| 层级命中（Background/Content/TopAction/Overlay） | `UiLayer.cs` | ✅ 已实现 |
| `Protect` 热区（原生 Slider/TextField 防抢占） | `UiInteract.Protect` | ✅ 已实现 |
| 值控件状态存储（跨帧 Float/EditText/Focus） | `UiValueStore` / `UiValueState` / `UiControlId` | ✅ 已实现 |
| `UiInteract.Slider` / `UiInteract.NumberField` 原语 | `UiInteract.cs` | ✅ 已实现 |
| `input/number-slider` 复合控件 | `Widgets/SliderNumberFieldWidget.cs` | ✅ 已注册 |
| LayoutEngine 接入 BeginFrame/ProcessEvents/EndFrame | `Layout/LayoutEngine.cs` | ✅ 已实现 |

**关键点**：UiKit 只解决了“框架层有分层输入路由”的能力，**US 侧尚未迁移**。当前 US UI 仍大量直接使用 `Widgets.ButtonInvisible` / `Widgets.ButtonText` / `Widgets.HorizontalSlider` / `Event.current`，因此 draw-order/help 可见性问题在 US 侧仍未真正修复。

## 2. US UI 现状差距

### 2.1 交互路由未接入（最大项）

`Source/UniversalSqueaker/UI` 下仍有 60+ 处直接 IMGUI 输入调用，且全部不在 `UiInteract` 注册表内：

- Help 按钮：`UsHelpButton.Draw` 仍用 `Widgets.ButtonInvisible`，没有注册为 `UiLayer.TopAction`，所以仍会被后画的 surface/row 覆盖或抢点击。
- 行级按钮：`ScopeTreeWidget`、`FilterBarWidget`、`BasicTuningWidget`、`CameraIndicatorWidget`、`PresetListWidget`、`VoicePackRow`、`RaceLayerRow`、`XenotypeLayerRow` 等均直接 `ButtonInvisible` / `ButtonText`。
- 滑块/输入框：`GlobalVolumeWidget` 直接 `Widgets.HorizontalSlider`；`SearchField` 直接 `Widgets.TextField`；都没有 `UiInteract.Protect`。
- 图表拖拽：`AttenuationEditorWidget` 直接消费 `Event.current` 且拖拽状态是 static。

### 2.2 值控件未利用

- 全局音量目前只有 slider，没有数值输入框；UiKit 已提供 `UiInteract.NumberField` / `input/number-slider`。
- 衰减编辑器拖拽状态仍是 static（B-39），可用 `UiValueStore` + 稳定 `UiControlId` 替代。

### 2.3 框架集成缺口

- `FerriteGuard` 是 `internal`，US 无法调用 `ResetSessionLog()`；若要在会话边界重置 Ferrite 内置 fallback 日志，需要 UiKit 暴露 public reset 或由 LayoutEngine 提供会话 API。
- `UiInteract` 目前只在 `LayoutEngine.Draw` 内生效；`FerriteVoicePacksPage` 的 nav/footer 在 LayoutEngine 之外，仍走旧路径（可接受，但需明确边界）。
- `SliderNumberFieldWidget` 发出中性 `ValueChanged(float)`；US 命令翻译层目前只认 `UsCommandPayload`，需要新增 float 命令映射或继续使用 US 自有 widget 封装原语。

## 3. 大修范围（建议 Block）

### U0 — UiKit 公共 API 收敛（架构纠偏）

目标：把本应由 UiKit 提供的通用基础设施公开化，消除 US 重复实现；详见 §6。

- 暴露 public `UiGuard`（或 `UiKitSession.Reset()`），**直接删除 `UsGuard`**，不做薄包装。
- 暴露 public `UiSurface` / `UiTheme`，US 注入 tokens，**直接删除 `UsSurface`/`UsVisualTokens`**。
- 暴露 `UiKitGui.Label` 等通用绘制 helper，**直接删除 `UsWidgetDrawing`**。
- 让 US 复用 `chrome/*` 内置 widget，替代 `StatusBanner`/`EmptyState`/手写 footer。
- 同步补齐 B-07 所需 public 会话 reset。
- Fallback 警告日志统一走 UiKit 通道（如 `[FerriteLib.UiKit]`），并在消息中标明是 **US 的哪个组件**触发 fallback（如 `component='us/scope-tree'` / `scope=UniversalSqueaker`）。US 侧不再维护自己的日志去重集合。

**U0 API 契约（建议）**：

```csharp
// FerriteLib.UiKit
public static class UiGuard
{
    public static float MeasureOrFallback(
        Func<float> custom, float fallbackHeight,
        string componentId, string? logScope = null);

    public static void DrawOrFallback(
        Rect rect, Action custom, Action<Rect> fallback,
        string componentId, string? logScope = null);

    // 供页面级整体 fallback 也走同一日志通道
    public static void LogFallback(
        string componentId, string? logScope, Exception ex);

    public static void ResetSessionLog();
}
```

- `componentId`：US 传 `Kind`（如 `us/scope-tree`）或组件名。
- `logScope`：US 传 `"UniversalSqueaker"`，用于日志标明来源。
- 日志示例：`[FerriteLib.UiKit] fallback triggered for component 'us/scope-tree' (UniversalSqueaker): ...`
- 内置 widget 的 `FerriteGuard` 也统一改为 `UiGuard`，删除 `FerriteGuard.cs`，避免 UiKit 内部再留一套重复实现。

**U0 迁移清单（已 grep 核实）**：

- `Source/UniversalSqueaker/UI/Visuals/UsGuard.cs` → 删除。
- 24 处 `UsGuard.MeasureOrFallback/DrawOrFallback` 调用 → 改 `UiGuard.*`，`componentId=Kind`、`logScope="UniversalSqueaker"`。
- `VoicePacksPage.BeginSession/EndSession` 的 `UsGuard.ResetSessionLog()` → `UiGuard.ResetSessionLog()`。
- `FerriteVoicePacksPage.Draw` 页面级 catch 的 `Log.Warning(...)` → `UiGuard.LogFallback("us/ferrite-page", "UniversalSqueaker", ex)`（保持“fallback 日志走 UiKit 通道”的一致约定）。
- `Source/FerriteLib.UiKit/Widgets/FerriteGuard.cs` → 删除，`InputModeRowWidget`/`InputModeCardWidget` 改 `UiGuard`。
- 新增/更新测试：`UiGuard` 注入异常、fallback 生效、per-session 去重、component id 日志断言。

### U1 — 交互路由迁移（最高优先，直接对应 draw-order handoff 的 US 侧）

目标：让所有 LayoutEngine 内的交互都走 `UiInteract`，help/顶层操作按钮注册 `TopAction`，行级按钮注册 `Row`/`Content`，原生输入区调用 `Protect`。

涉及文件（不限于）：

- `UI/Widgets/UsHelpButton.cs` — `UiInteract.Button(rect, UiLayer.TopAction, ...)`
- `UI/Widgets/PageTitleWidget.cs`、`UI/Components/HelpToggle.cs` — help 顶层按钮
- `UI/Widgets/GlobalVolumeWidget.cs` — slider/field 用 `UiInteract.Protect` + `UiInteract.Slider`/`NumberField`
- `UI/Widgets/AttenuationEditorWidget.cs` — chart `Protect`，preset 按钮 `Button`
- `UI/Widgets/FilterBarWidget.cs`、`BasicTuningWidget.cs`、`CameraIndicatorWidget.cs`、`ScopeTreeWidget.cs`、`PresetListWidget.cs`
- `UI/Components/VoicePackRow.cs`、`RaceLayerRow.cs`、`XenotypeLayerRow.cs`、`VoicePackChecklist.cs`（含 Forget Unavailable）
- `UI/Components/SearchField.cs` — `UiInteract.Protect(rect)`（或保持原生 TextField 但注册 protect）

注意：`UiInteract` 只处理 `MouseUp` 派发；行级按钮从 `ButtonInvisible` 迁移后点击语义变化需在游戏内/测试中确认。

### U2 — 值控件与状态

- `GlobalVolumeWidget`：使用同一 `UiControlId` 的 `UiInteract.Slider` + `UiInteract.NumberField`，支持 0–100% 双向联动；保留 US help/banner/skin。
- `AttenuationEditorWidget`：拖拽状态从 static 迁到 `UiValueStore`（或 widget 实例状态 + 会话重置）；图表区域 `Protect`，preset 按钮 `Button`。
- 抽取纯函数（chart x↔distance、sanitize、format）供单测（对应 B-11/B-50）。
- 若使用 `input/number-slider`，在 `FerriteVoicePacksPage.TryTranslate` 增加 float payload → `UiCommandKind.SetGlobalVolume` 映射。

### U3 — 布局 / 窄屏修复

对应 backlog：

- B-01 ScopeTree 窄屏 Measure/Draw 高度不一致
- B-02 ScopeTree 固定 96px 越界
- B-03 FilterBar 窄屏横向溢出
- B-04 页面级窄屏 guard 用内容宽度
- B-24 footer 高度常量统一
- B-25 切页签清滚动
- B-26 Layout.xml footer 死声明与手工 DrawFooter 对齐
- B-27 左导航命令统一走 adapter
- B-36 FilterBar All 语义
- B-37 帮助 banner Measure/Draw 宽度一致
- B-38 窄屏 help 展开不画 banner
- B-40 `ResolveSelectedDomain` fallback 同步 `SelectedScope`
- B-41 死 API 清理或补 UI
- B-42 `UiLayoutTier.ClampWidth` 接入
- B-43 拖拽中状态行显示 Custom

### U4 — Fallback / 健壮性

对应 backlog：

- B-05 由 U0 的 public `UiGuard` 真保护测量逻辑（US 删除 `UsGuard` 后全部走 UiKit）
- B-06 未防护 Measure 补 guard（统一用 `UiGuard`）
- B-07 `UiGuard.ResetSessionLog()` 接线到 `VoicePacksPage.BeginSession/EndSession`（不再有 US 侧日志集合）
- B-08 外层 catch 日志节流
- B-35 页面级 catch 前恢复 GUI 状态
- B-44 Ferrite/Vanilla 状态一致性
- B-45 ScopeTree/PresetList fallback 口径
- B-46 Vanilla 滚动坐标不依赖 inRect.x==0
- B-47 Ferrite 内置信息组件 fallback
- B-48 EmptyState 恢复 Text.Font
- B-49 Vanilla settings==null 行为

**U4 硬性要求**：删除 `Source/UniversalSqueaker/UI/Visuals/UsGuard.cs`，替换全部 24 处 `UsGuard.*` 调用为 UiKit `UiGuard`；`FerriteVoicePacksPage.Draw` 页面级 catch 也改走 `UiGuard.LogFallback`；所有 fallback 日志带 US 组件 id 并统一走 UiKit 通道。

### U5 — Skin / 令牌化

对应 backlog：

- B-09 左导航 `DrawNav` 迁到 `UsVisualTokens`/`UsSurface`
- B-28 checkbox checked 边框 AccentGold
- B-29 Help hover 态区分
- B-30 ScopeTree 段按钮/Auto 清除钮 danger/focus 状态
- B-31 SearchField 表面 Base → Panel
- B-32 PresetList 原版 Checkbox/Color.white 令牌化
- B-33 `UsSurface.DrawSegment`/`DrawHeader` 恢复 Text.Anchor
- B-34 Diagnostics 硬编码颜色 grep 范围确认

### U6 — 测试 / 门禁

对应 backlog：

- B-10 `UiGuard` 注入异常测试（含 US component id 日志断言）
- B-11 Attenuation 拖拽/纯函数测试
- B-12 LayoutEngine 宽度变化缓存失效回归测试
- B-50 distance range clamp 精确断言
- B-51 加载侧越界 clamp 测试
- B-52 SettingsMigration 共享状态/固定路径
- B-53 verify-local `--no-restore` + 测试项目 warnings-as-errors
- B-54 第 14 门名称更新
- B-55 Vanilla 页不自动单测的说明沉淀
- B-56 `VoicePacksLayout` 纯函数测试

新增 US UI 集成测试建议：

- help 按钮注册 `TopAction` 且不被行 `Row` 抢点击（可用 FerriteLib stub 驱动 LayoutEngine）
- `GlobalVolumeWidget` slider/field 双向联动 + 0% 仍 emit
- `AttenuationEditorWidget` 拖拽状态不跨会话残留
- `FerriteVoicePacksPage.TryTranslate` float payload 映射

## 4. Backlog 状态映射（摘要）

| 类别 | 现在可处理 | 说明 |
|---|---|---|
| draw-order/help 可见性（handoff） | ✅ US 侧 U1 | UiKit 已提供分层派发；US 必须迁移 help/row 注册 |
| B-39 静态拖拽 | ✅ U2 | 用 `UiValueStore` 替代 static |
| B-01..B-04 布局 Major | ✅ U3 | 纯 US 侧 |
| B-05..B-08 fallback Major | ✅/⚠️ U4 | B-07 需 UiKit 暴露 public reset |
| B-09 skin Major | ✅ U5 | 纯 US 侧 |
| B-10..B-12 测试 Major | ✅ U6 | B-12 可补充在 FerriteLib 或 US 测试工程 |
| B-20..B-56 其它 | ✅ U3–U6 | 按区块吸收 |

## 5. 新增风险 / 注意点

1. **`UiInteract` 的点击在 MouseUp 派发**：从 `ButtonInvisible`（MouseDown/Click）迁移后，需要确认行按钮、Help 按钮在 RimWorld 实际输入下的手感无回归。
2. **`FerriteGuard` internal**：若 US 要重置 Ferrite 内置 fallback 日志，需要先给 UiKit 加一个 public 会话 reset API（小改动，建议纳入 U4 前置）。
3. **`UiValueStore` 静态字典不清理**：动态列表控件若用逐行 `UiControlId`，可能随会话累积。建议 US 侧使用稳定、可枚举的 ID（如 `scope + row.Key`），并评估是否需要在 `EndSession` 清理。
4. **Nav/Footer 在 LayoutEngine 之外**：本次大修可保留 nav/footer 旧路径；若后续要统一，需把 `BeginFrame/ProcessEvents` 提升到整个页面 Draw 外围。
5. **中性边界**：FerriteLib 不能出现 US/SR 字面量；US 侧 Layout.xml 中的 `EmitName="SetGlobalVolume"` 属 US 内容，不违反中性。
6. **Guard 重复设计**：`FerriteGuard` 与 `UsGuard` 从 US+UiKit 联合视角看是同一模式的重复实现（try/catch → 恢复 GUI → log once → fallback），只是分别保护内置 widget 与 US widget、且都因 `internal` 无法互相复用。建议后续在 FerriteLib 暴露一个 public 中性 `UiGuard`（或 `UiKitSession.Reset()`），让 US 复用它，`UsGuard` 退化为薄包装或删除；这同时解决 B-07。

## 6. 架构方向评估：US 自实现是否重复设计

用户方向判断正确：在“UiKit 是 US 依赖的框架层”这一设计决策下，US 自己实现通用 UI 基础设施属于重复设计。当前因为 UiKit 把通用原语设为 `internal`，US 被迫复制了一份：

| 通用能力 | UiKit 内部实现 | US 重复实现 |
|---|---|---|
| fallback guard | `FerriteGuard` | `UsGuard` |
| 调色板 | `Palette` | `UsVisualTokens` |
| surface/border | `SurfaceFrame` | `UsSurface` |
| 文本绘制 helper | `UiKitGui` | `UsWidgetDrawing` / 各 widget 手写 |
| chrome 组件 | `SectionHeaderWidget` / `ChromeBannerWidget` / `ChromeFooterWidget` / `EmptyStateWidget` | `UsWidgetDrawing.DrawSectionHeader` / `StatusBanner` / `UsFooterWidget` / `EmptyState` |

这不是产品逻辑重复，而是**框架层本该提供、却因 `internal` 被 US 重造**的重复。甚至 UiKit 的 `Palette` 直接硬编码了 P0 配色，说明它并没有真正中性化皮肤，而是把 US 的皮肤值内嵌进库，US 又复制了一份。

### 评估结论

- 认同：US 自实现的通用部分应尽量收敛到 UiKit，US 只保留产品特定内容（view model、business command、help catalog、US 复合 widget、US 主题值注入）。
- 但并非 US 所有代码都是重复：`VoicePacksLayout` 的领域高度/过滤/域统计是产品逻辑；`UsHelp` / `UsHelpCatalog` 是产品内容；`VoicePacksPageModel` / `ViewState` 是产品状态。
- 建议新增 **U0 — UiKit 公共 API 收敛**，在 U1 之前或与 U4/U5 合并：
  1. 暴露 `UiGuard`（或 `UiKitSession.Reset()`），**直接删除 `UsGuard`**，US 不再保留 guard 类设施；
  2. 暴露 `UiSurface` / `UiTheme`，让 US 注入 tokens，**直接删除 `UsSurface` / `UsVisualTokens`**；
  3. 暴露 `UiKitGui.Label` 等通用绘制 helper，**直接删除 `UsWidgetDrawing`**；
  4. 让 US 尽量复用 `chrome/*` 内置 widget，替代手写 `StatusBanner` / `EmptyState` / footer 绘制。
- 该收敛会同时解决 B-07，并让后续 U1 迁移建立在统一公共 API 上，避免在重复代码上再迁移一遍。
- **日志约定（用户已拍板）**：US 组件触发 fallback 时，警告日志走 UiKit 通道（`[FerriteLib.UiKit]`），消息中明确标注是哪个 US 组件（例如 `component='us/scope-tree'`、`scope=UniversalSqueaker`）。US 侧不自行 `Log.Warning` fallback，不维护自己的 per-session 去重集合。

## 7. 建议执行顺序

1. **U0（UiKit 公共 API 收敛）** — 先消除重复基础设施，再迁移交互；否则 U1 会在即将删除的 `UsSurface`/`UsGuard` 上重复劳动。
2. **U1（交互路由迁移）** — 直接解决 draw-order handoff 的 US 侧落地，先做 `UsHelpButton` + 最受影响的 3–4 个 widget。
3. **U2（值控件）** — GlobalVolume + Attenuation，依赖 U1 的 Protect 原语。
4. **U3（布局/窄屏）** — 修复 review Major 布局项。
5. **U4（Fallback）** — 在 U0 暴露 `UiGuard` 后接线会话 reset。
6. **U5（Skin）** — 在 U0 暴露 `UiSurface`/`UiTheme` 后完成令牌化。
7. **U6（测试/门禁）** — 每块完成后补对应测试，最后统一 verify-local 14 门 + FerriteLib tests。

## 8. 验收门禁

- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev` 零警告
- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release` 零警告
- `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` → ALL PASS
- `dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release` → 通过
- `dotnet run --project tools/UniversalSqueakerKernelTests -c Release` → 通过
- `pwsh -File scripts/verify-local.ps1` → 14 门全绿
- 中性 grep：`Source/FerriteLib.UiKit` 无 US/SR 产品字面量
- 工作树干净（本次评估仅新增本评估文档，不包含代码改动）
