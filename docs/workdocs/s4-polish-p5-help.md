# S4-Polish P5 — 组件化帮助（编码任务书）

> 状态：待派发。执行者：C-Agent（持久 worker）。依赖：P4 验收通过。

## 任务

把单一页级帮助改为 **widget-attached 就地帮助**：每个 US widget 头带 `?`，点击后在该 widget 内部展开帮助 banner，不点穿、不遮挡后续区块。

## 先读

- `docs/s4-polish-plan-zh.md` §4 P5、§3.3。
- `Source/FerriteLib.UiKit/Context/UiPageState.cs`、`Source/FerriteLib.UiKit/Layout/LayoutEngine.cs`
- `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`、`UI/UsWidgetRegistrar.cs`、`UI/Layout.xml`
- 现有全部 US widget（`UI/Widgets/*.cs`）与 `PageTitleWidget`
- `docs/ui-visual-modernization-zh.md` help 组件规格（P0 稿）。
- 不要读其它 workdocs。

## 改动清单

### 1. FerriteLib.UiKit 中性能力

- `UiPageState` 新增：
  - `public readonly HashSet<string> OpenHelpKeys = new(StringComparer.Ordinal);`
  - `public void ToggleHelpKey(string key)`（有则移除、无则加入；空 key no-op）。
  - `Reset()` 清空 `OpenHelpKeys`。
- `tools/FerriteLib.UiKit.Tests` 追加断言：默认空、Toggle 加入/移除、Reset 清空、空 key no-op。

### 2. 帮助内容目录（US 层）

- 新增 `Source/UniversalSqueaker/UI/Help/UsHelpCatalog.cs`：`internal static string? Get(string key)`，字典键与内容如下（英文文案，后续本地化不属本任务）：
  - `us/mode-row`：解释四种模式的语义（Vanilla/Fallback/Remix/Disabled）。
  - `us/filter-bar`：解释域过滤与作者过滤。
  - `us/basic-tuning`：解释彩蛋、距离预设、三个缩放开关。
  - `us/global-volume`：解释全局音量 0–100% 与 `Disabled` 的区别。
  - `us/attenuation-editor`：解释相机高度 15–65、开始点 100%、结束点 0%、线性衰减、三快速预设。
  - `us/camera-indicator`：解释相机指示器。
  - `us/scope-tree`：解释 Global/Race/Xenotype 三层与 Auto/Off/Any/Command。
  - `us/preset-list`：解释 baseline 预设导入。
  - `us/race-layer` / `us/xenotype-layer`：解释域行与 `(race,xeno)` 双键。
  - `us/voice-pack-checklist`：解释勾选/搜索/orphan/Forget Unavailable。
  - `us/page-title`：页级帮助（原 HelpText 迁移至此）。

### 3. 帮助按钮组件

- 新增 `Source/UniversalSqueaker/UI/Widgets/UsHelpButton.cs`：
  - `public static bool Draw(Rect rect, string helpKey, UiPageState state, Action<KitUiCommand> emit)`：画现代 `?` 按钮；点击 emit `new KitUiCommand("ToggleHelp", helpKey)`；返回是否点击。
  - `helpKey` 为空/查无内容时返回 false 且不画。

### 4. 各 widget 接入

- `Layout.xml` 给以下根节点加 `HelpKey="..."`：`us/page-title`（`us/page-title`）、`input/mode-row`（`us/mode-row`）、`us/filter-bar`、`us/basic-tuning`、`us/global-volume`、`us/attenuation-editor`、`us/camera-indicator`、`us/scope-tree`、`us/preset-list`、`us/race-layer`、`us/xenotype-layer`、`us/voice-pack-checklist`。
- 每个 widget 的 `Measure`：当 `ctx.State.OpenHelpKeys.Contains(helpKey)` 时，在头高基础上加 `VoicePacksLayout.BannerHeight(helpText, width, metrics) + Gap`。
- 每个 widget 的 `Draw`：在 section header 行右侧画 `?`（复用 `UsHelpButton`）；展开时在 header 下方、内容上方画帮助 banner（现代 banner 表面，不可点击、不拦截）。
- `PageTitleWidget`：迁移到新机制（移除旧的页级 `HelpOpen` 使用；`FerriteVoicePacksPage` 的页级 `HelpOpen` 键可保留兼容但不再被新 UI 使用）。
- `UsWidgetCommandAdapter` 不涉及帮助命令（帮助走 Ferrite 层 `KitUiCommand("ToggleHelp", key)`，不进业务命令流）。

### 5. `FerriteVoicePacksPage` 命令处理

- 现有 `ToggleHelp` 分支改为：`kitCommand.Payload` 是 string key 时调 `uiState.ToggleHelpKey(key)`；无 payload 保持旧行为（兼容）。
- `BeginSession`/`EndSession` 确保 `OpenHelpKeys` 随 `UiPageState` 重建（当前每帧 new `UiPageState`，需在 `State` 里保存帮助键集合或重建时回填——**实现选择：在 `VoicePacksPageState` 增加 `HashSet<string> OpenHelpKeys` 作为持久载体，每帧构造 `UiPageState` 时回填；`Reset()` 清空。** 若你选择其它等价实现，必须保证帮助展开状态跨帧存活、重开窗口清空）。

## 验收

- 每个带 HelpKey 的 widget 头出现 `?`；点击就地展开，`Measure` 预留高度，不压到后续区块；再点收起；重开窗口状态复位。
- FerriteLib.UiKit.Tests 新增断言全绿。
- 构建 Dev 0 警告；kernel 测试全绿；UiLogicTests ALL GREEN；`verify-local.ps1` 14 门全绿。
- 隐私预检通过。

## 提交

```text
feat(S4): componentized inline help
```

允许范围：`FerriteLib.UiKit/Context/UiPageState.cs`、`tools/FerriteLib.UiKit.Tests/*`、`Source/UniversalSqueaker/UI/Help/UsHelpCatalog.cs`（新）、`UI/Widgets/UsHelpButton.cs`（新）、全部接入帮助的 `UI/Widgets/*.cs`、`UI/FerriteVoicePacksPage.cs`、`UI/Model/VoicePacksPageState.cs`（若按推荐实现）、`UI/Layout.xml`。不得改其它文件。

## 禁止

- 不把帮助文案放进 FerriteLib。
- 不新增业务命令写 settings。
- 不 push / 配 remote。
