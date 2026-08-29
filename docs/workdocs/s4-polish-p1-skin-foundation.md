# S4-Polish P1 — 现代皮肤地基（编码任务书）

> 状态：待派发。执行者：C-Agent（持久 worker，第二棒）。
> 依赖：维护者已批准 `docs/ui-visual-modernization-zh.md`（P0），且 S4-Nav 已验收通过。
> 目标：全量换肤地基——US 层令牌/表面/守卫助手 + FerriteLib 中性现代 skin + 全部现有 widget 切换到新皮肤 + 每 widget 原版 fallback（L1/L2）。**不改变任何交互、命令、布局高度。**

## 先读

- `docs/ui-visual-modernization-zh.md`（P0 已批准稿，色板/组件库/fallback 矩阵以此为准）
- `docs/s4-polish-plan-zh.md` §4 P1、§10（L1/L2 规则）
- 源文件：`Source/UniversalSqueaker/UI/**` 全部（含 Components/Widgets/Layout/Model 中现有绘制相关文件）、`Source/FerriteLib.UiKit/Widgets/**`、`Source/FerriteLib.UiKit/Context/UiPageState.cs`
- 不要读其它 workdocs、不要读其它 docs 长篇。

## 任务

### 1. US 层新助手（新增）

- `Source/UniversalSqueaker/UI/Visuals/UsVisualTokens.cs`：
  - 按 P0 色板给出角色化令牌（`SurfaceBase/Panel/Raised/Hover/Selected/Warning/Success/Danger`、`TextPrimary/Secondary/Accent/Muted/Disabled`、`AccentGold/Border/BorderStrong`、间距 2/4/6/8/10、行高 S=22/M=26/L=32/XL=50）。
  - 全部 `internal static readonly`。
- `Source/UniversalSqueaker/UI/Visuals/UsSurface.cs`：
  - `DrawSurface(Rect, SurfaceKind)`、`DrawRowSurface(Rect, hover, selected, danger)`（行 = 填充 + 1px 边框 + 选中时左侧 4px AccentGold 竖条）、`DrawCardSurface(Rect, hover, selected)`（卡片 = 填充 + 1px 边框 + 选中时底部 3px AccentGold 条）、`DrawSegment(Rect, label, selected)`、`DrawCheckbox(Rect, value)`（18px 方框 + 金勾）、`DrawHeader(Rect, text)`。
  - 统一替换现有散落的 `Widgets.DrawBoxSolid + SectionFrame.DrawBorder` 组合；不再允许各 widget 手写 `new Color(.16f,.145f,.12f,.94f)` 这类表面色（文本特殊色除外）。
- `Source/UniversalSqueaker/UI/Visuals/UsGuard.cs`：
  - `MeasureOrFallback(Func<float> custom, float fallbackHeight, string widgetId)`。
  - `DrawOrFallback(Rect rect, Action custom, Action<Rect> vanillaFallback, string widgetId)`。
  - catch 后**先恢复** `Text.Font = GameFont.Small`、`Text.Anchor = TextAnchor.UpperLeft`、`GUI.color = Color.white`，再执行 fallback。
  - 每个 widgetId 每会话只 log 一次（静态 `HashSet<string>`，`VoicePacksPage.BeginSession` 时清空——允许你在 `VoicePacksPage.BeginSession/EndSession` 增加清理钩子）。

### 2. US 层现有组件/ widget 全部切换

- 切换范围：`BasicTuningWidget`、`GlobalVolumeWidget`、`AttenuationEditorWidget`、`CameraIndicatorWidget`、`ScopeTreeWidget`、`PresetListWidget`、`RaceLayerWidget`、`XenotypeLayerWidget`、`VoicePackChecklistWidget`、`PageTitleWidget`、`RaceLayerRow`、`XenotypeLayerRow`、`VoicePackRow`、`VoicePackChecklist`、`SearchField`、`EmptyState`、`StatusBanner`、`HelpToggle`、`SectionFrame`。
- 每个文件：用 `UsVisualTokens`/`UsSurface` 重写绘制；**不改变行高、间距、命令流、Measure 结果**（P1 是纯视觉替换）。
- `SectionFrame`/`UiPalette` 处理：先让 `UiPalette` `[Obsolete]` 转发到 `UsVisualTokens`，全部切换完成后本提交内删除 `UiPalette` 与旧 `SectionFrame`（若已无引用）。
- 每个交互 widget 增加 `DrawVanillaFallback(Rect, WidgetContext, Action<KitUiCommand>)`（按 P0 fallback 矩阵）：
  - toggle 行 → `Widgets.Checkbox` + `Widgets.Label`（命令不变）。
  - 段按钮 → `Widgets.ButtonText`，选中项前缀 `●`（命令不变）。
  - VoicePack 开关 → `Widgets.Checkbox` + 两行 `Widgets.Label`（`TogglePack` 命令不变）。
  - 搜索框 → 原版 `Widgets.TextField`（`SearchField` 的 fallback 就是去掉皮肤外壳）。
  - help `?` → `Widgets.ButtonText("?")`。
  - 非交互组件 fallback：banner/header/empty/footer 前身 → `Widgets.Label`（banner 加简单 `DrawBoxSolid`）；全局音量 slider → `Widgets.HorizontalSlider` + `Widgets.Label`；衰减编辑器 → `Widgets.Label` 文本摘要（`Conservative 15–65` 等）。
  - 在 `Draw` 里用 `UsGuard.DrawOrFallback` 包住自定义绘制与 fallback。

### 3. FerriteLib.UiKit 中性现代 skin

- `Widgets/Palette.cs`：按 P0 色板更新为角色化令牌（仍 `internal`，仍无产品字面量）。
- `SurfaceFrame.cs`、`ModeCardRenderer.cs`、`ChromeBannerWidget.cs`、`ChromeFooterWidget.cs`、`SectionHeaderWidget.cs`、`EmptyStateWidget.cs`、`InputModeRowWidget.cs`：跟随新 `Palette` 绘制；`ModeCardRenderer` 用底部 3px 金条选中；`ChromeBannerWidget` 支持 `ColorHint` 的 warning/success 语义色。
- **FerriteLib 内不得出现 `UniversalSqueaker`/`SqueakyRatkin`/`Ratkin`/`SR_`/`US_`。**
- 同样给库内交互 widget（`InputModeRowWidget`/`InputModeCardWidget`）加中性 fallback（`Widgets.ButtonText` + 选中前缀 `●`），用库内自己的一套 guard（可仿 `UsGuard`，但不引用 US 类型）。

### 4. 不得改

- 不改变 `Layout.xml`（P1 不动布局清单）。
- 不改变 `VoicePacksPageModel` / `VoicePacksViewState` / `VoicePacksPageState` / `UiCommand`。
- 不改变 FerriteLib 的公开 API（`Palette` 等保持 internal；`SurfaceKind` 枚举名可保留）。
- 不改变任何 Measure 高度值。

## 验收

- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev`（0 警告）
- `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev`（0 警告）
- `dotnet run --project tools/UniversalSqueakerKernelTests -c Release`（全绿）
- `dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release`（ALL GREEN）
- `pwsh -File scripts/verify-local.ps1`（14 门全绿，含 neutrality grep）
- grep 自检：US widget 不再直接 `new Color(...)` 画表面；FerriteLib 无产品字面量。

## 提交

一条提交：

```text
feat(S4): modern skin foundation — US tokens/surface/guard + FerriteLib neutral skin
```

允许范围：`Source/UniversalSqueaker/UI/Visuals/*`（新）、`Source/UniversalSqueaker/UI/Components/*`、`Source/UniversalSqueaker/UI/Widgets/*`（切换与 fallback）、`Source/UniversalSqueaker/UI/VoicePacksPage.cs`（若加了 guard 清理钩子）、`Source/FerriteLib.UiKit/Widgets/*`。不得改其它目录。

## 禁止

- 不写新 widget（footer/chart/filter/help 后续任务书）。
- 不改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`。
- 不 push / 配 remote。
