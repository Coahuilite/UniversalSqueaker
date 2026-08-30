# Camera+ 分层帮助 + 标题隐藏实施计划

> 状态：已完成
> 日期：2026-08-30
> 依据：维护者确认（隐藏整个导航品牌区；Global Volume 隐藏卡片体内标签；分层帮助做完整版：总览+单项列表+悬停联动）

## 1. 目标

1. **Camera+ 分层帮助**：右侧帮助面板从“单段文本”升级为“分区总览 + 单项列表 + 悬停/选中联动高亮”。
2. **标题去重**：
   - 左侧导航隐藏整个品牌区（不再显示 Universal Squeaker / VoicePack Routing）。
   - Global Volume 隐藏卡片体内的 “Global volume” 标签，保留卡片头部标题。
   - 为未来复用，UiKit/US 增加可选的标题隐藏能力。

## 2. 隐藏机制

### 2.1 已有能力
- UiKit `LayoutEngine` 已支持元素级 `Hidden="true"`：隐藏整个 Widget/Block/Section/Column，Measure 与 Draw 都跳过。

### 2.2 新增能力
- `UsCard` 支持 `TitleHidden="true"`（隐藏卡片头部，Measure/Draw 同步调整）。用于未来“只要正文不要卡头”的场景。
- `GlobalVolumeWidget` 支持 `HideBodyLabel="true"`（隐藏卡片体内重复标签），在 `Layout.xml` 上声明。
- 左侧导航品牌区直接在 `FerriteVoicePacksPage.DrawNav` 中移除（导航不属于 Layout.xml，无需 XML 开关；如未来需要可加 `State.ShowNavBrand`）。

## 3. 分层帮助数据模型

```text
HelpSection
  Key          : string          // 例如 us/scope-tree
  Title        : string          // 分区名，例如 "Tuning Editor"
  Overview     : string          // 分区总览
  Items        : List<HelpItem>  // 单项列表

HelpItem
  Key          : string          // 例如 us/scope-tree/action-scope
  Label        : string          // 显示名，例如 "Action Scope"
  Text         : string          // 单项帮助
```

- `UsHelpCatalog` 改为按 section key 返回 `HelpSection`，保留旧 `Get(string)` 兼容或直接替换调用方。
- 新增 `TryGetSection` / `TryGetItem`。

## 4. 右侧帮助面板行为

- 标题显示当前分区名（不再固定 “Help”）。
- 默认显示 `Overview`。
- 当 `UiPageState.HelpHoverKey` 或 `HelpSelectionKey` 命中当前分区某个 item 时，面板主体切换为该项的 `Label + Text`。
- 面板底部/中部显示当前分区所有 item 的紧凑列表：
  - 悬停 item 行：设置 `HelpHoverKey`，行高亮。
  - 点击 item 行：设置 `HelpSelectionKey`，行选中。
  - 点击“分区总览”区域或空白：清除 selection，回到 overview。
- 面板内部继续使用 `HelpScrollPosition` 滚动。

## 5. 悬停联动与控件高亮

- `UiPageState` 新增：
  - `string HelpHoverKey`
  - `string HelpSelectionKey`
- 新增中性/ US 侧高亮助手：
  - 当某控件绘制时，若其 help key 等于 `HelpHoverKey` 或 `HelpSelectionKey`，在其 rect 外画高亮边框（金色/强调色）。
- US 控件在绘制可交互行/控件时设置 `HelpHoverKey`：
  - 模式卡、Global Volume、Attenuation（图表/预设）、Basic toggles、Camera indicator、ScopeTree（layer/domain/action rows/mood rows）、PresetList、FilterBar、Race/Xenotype rows、VoicePackChecklist rows。
- 具体 help key 命名：`<section-key>/<item-slug>`，例如：
  - `us/mode-row/vanilla`
  - `us/scope-tree/layer`
  - `us/scope-tree/action-scope`
  - `us/scope-tree/mood-tuning`
  - `us/attenuation-editor/chart`
  - `us/attenuation-editor/presets`
  - `us/global-volume/slider`
  - `us/basic-tuning/egg`
  - `us/basic-tuning/distance`
  - `us/basic-tuning/scaling`
  - `us/camera-indicator/toggle`
  - `us/filter-bar/domain`
  - `us/filter-bar/author`
  - `us/race-layer/row`
  - `us/xenotype-layer/row`
  - `us/voice-pack-checklist/search`
  - `us/voice-pack-checklist/row`
  - `us/voice-pack-checklist/forget`
  - `us/preset-list/import`
  - `us/preset-list/tree`

## 6. 实施步骤

1. 隐藏导航品牌区。
2. Global Volume 隐藏体内标签 + `UsCard.TitleHidden` 支持。
3. `UsHelpCatalog` 结构化改造（保留现有文本内容作为 Overview/Items 基础，补充单项文案）。
4. `UiPageState` 增加 HelpHoverKey/HelpSelectionKey。
5. `UsHelpPanel` 升级为分层面板。
6. US 控件接入 help key + hover 高亮。
7. 补测试：
   - `UsHelpCatalog` 结构化解析/查找。
   - `UsHelpPanel` 逻辑（overview/ hover/ selection 切换）。
   - 隐藏机制：`UsCard` TitleHidden 的 Measure/Draw 高度；GlobalVolume 体内标签隐藏源码断言；导航品牌区不再绘制。
   - 至少一个控件的 hover help key 设置与高亮调用断言。

## 7. 验证

- `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev`
- `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release`
- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev`
- `pwsh -File scripts/verify-local.ps1`
