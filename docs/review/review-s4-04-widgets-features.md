# Review S4-04 — Feature Widgets 功能区

## 结论

已按只读方式核对 HEAD `46b7d16` 下 Feature Widgets 相关重点文件，覆盖：

- 全局音量 slider 的命令解析 / 写桥 / 百分比显示
- 衰减编辑器拖拽约束、快速预设与 Custom 语义
- FilterBar 空过滤 / 作者过滤 / 组合过滤
- 帮助系统 OpenHelpKeys 生命周期、Measure 预留高度、展开不遮挡、无帮助键时不画按钮
- UiLayoutTier 边界与各 widget 窄屏 clamp / 负宽度
- UiCommand -> VoicePacksPageModel 命令流

核心结论：

1. **全局音量命令流通过**。`GlobalVolumeWidget` 只读 `GlobalVolumeFactor`，变化时发 `SetGlobalVolume`；`VoicePacksPageModel` 解析后走 `UniversalSqueakerSettings.SetGlobalVolume`，clamp 0..1 并同步 `CompSqueaker.GlobalVolumeFactor`。百分比显示使用 `Mathf.RoundToInt(value * 100f) + "%"`，正常值域下正确。
2. **衰减编辑器逻辑约束通过**。开始点固定在 100%（图表顶部）、结束点固定在 0%（底部），x 范围 15–65，拖拽强制 `end >= start + 5`，松手发 `SetDistanceRange` 并由 settings 置 `Custom`；三枚快速预设按钮发 `SetDistancePreset`。逻辑本身符合任务书。
3. **FilterBar 纯函数语义基本通过**。`VoicePacksFilters.DomainMatches` / `PackMatches` 为空过滤、EnabledOnly、ConflictOnly、OrphanOnly、Author、组合过滤提供了确定性的纯函数实现，且测试覆盖了这些语义。UI 侧作者过滤作用于包行，符合帮助文案。
4. **命令流通过**。所有被审 widget 均只 emit `UiCommand`，由 `FerriteVoicePacksPage` 收集后经 `VoicePacksPageModel.ExecuteAll` 写 settings；未发现 widget 直接写 settings 的路径。
5. **发现的主要问题集中在帮助按钮绘制顺序与窄屏响应式**：多数 Feature Widget 的 help “?” 按钮被后续不透明 surface 覆盖；`ScopeTreeWidget` 窄屏 Measure/Draw 高度漂移；`FilterBarWidget` / `ScopeTreeWidget` / `PresetListWidget` 在内容区窄于阈值时溢出或出现负坐标；页面级窄屏 guard 检查的是窗口宽度而非扣除导航后的内容宽度。

未发现 Blocker；共 5 个 Major、5 个 Minor、3 个 Nit。

## 发现

### Major

#### 1. Help “?” 按钮绘制顺序错误，在多数 Feature Widget 中被不透明 surface 覆盖（不可见/不可点）

- 文件：`Source/UniversalSqueaker/UI/Widgets/GlobalVolumeWidget.cs:68-71`
- 文件：`Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs:84-85,119-121,141`
- 文件：`Source/UniversalSqueaker/UI/Widgets/FilterBarWidget.cs:65-66,79-88,142,156`
- 文件：`Source/UniversalSqueaker/UI/Widgets/BasicTuningWidget.cs:75-76,87,146`
- 文件：`Source/UniversalSqueaker/UI/Widgets/CameraIndicatorWidget.cs:66-67,71`
- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:126-127,141,186`
- 文件：`Source/UniversalSqueaker/UI/Visuals/UsSurface.cs:45,58`（不透明填充）
- 文件：`Source/UniversalSqueaker/UI/Visuals/UsVisualTokens.cs:11-18`（alpha 均为 1）

这些 widget 的 `DrawCore` 都是先调用 `UsHelp.DrawHelpButton(helpRect, ...)`，随后才绘制覆盖整个 widget 或延伸到右边缘的 `UsSurface.DrawSurface/DrawRowSurface/DrawChart`。由于填充色 alpha=1，后续 surface 会把 helpRect 盖住：

- `GlobalVolumeWidget`：help 按钮画在整块 Panel 之前，被完全盖住。
- `AttenuationEditorWidget`：chart surface 的右边缘到 `xMax - 10`，与 helpRect 重叠约 12×20px，按钮基本不可见。
- `FilterBarWidget`：最后一个 Author 段覆盖到 `xMax`，且其 `ButtonInvisible` 在 help 之后注册，既遮住又可能抢走点击。
- `BasicTuningWidget` / `CameraIndicatorWidget`：第一行/整行 RowSurface 覆盖 helpRect，且整行 `ButtonInvisible` 会抢点击。
- `ScopeTreeWidget`：LayerRow 的 RowSurface 同样覆盖 helpRect。

结果：帮助系统在 Basic/Tuning/Packs 的主要交互 widget 上实际不可用，只有 PageTitle、RaceLayer、XenotypeLayer、Checklist、PresetList 这类“先画 header 再在下方画行”的 widget 能正常显示 help。建议统一把 help 按钮放在所有内容 surface 之后绘制，或在布局上为右上角 help 保留不被内容 surface 覆盖的排除区；同时补一条绘制顺序/可见性检查（或至少人工游戏内验证）。

#### 2. `ScopeTreeWidget` 窄屏竖排层按钮 Measure/Draw 高度不一致，内容重叠并低估滚动高度

- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:74`
- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:141-142`
- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:199-214`
- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:230-235`

`LayerRowHeightFor` 在按钮放不下时返回 `LayerRowHeight + 22f`（约 50px），但 `DrawCore` 仍用固定 `LayerRowHeight`（28px）调用 `DrawLayerRow` 并只按 28px 推进 y。`DrawLayerRow` 的 stacked 分支实际从 `rect.y + 22f` 开始连续绘制 3 个 20px 按钮，底部约到 `rect.y + 86f`。因此：

- 该行实际绘制高度约 86px，Measure/LayoutEngine 只分配约 50px；
- 后续 domain row、scope header、scope rows、mood rows 会与竖排按钮重叠；
- 页面总内容高度被低估，滚动到底部时末尾行可能被裁切。

触发条件：内容区 innerWidth 小于约 316px 即进入 stacked 分支；由于左侧导航固定 140px，窗口宽度约小于 472px 就可能触发。这正是 S4-Polish 窄屏响应式要求覆盖的区间。

建议：Measure 与 Draw 使用同一高度公式（例如 stacked 分支实际高度约 `(ButtonHeight + RowGap) * 3`），并让 `DrawCore` 按该高度推进 y；同时增加窄宽度下 ScopeTree 测量断言。

#### 3. `ScopeTreeWidget` 窄屏下 DomainRow / ScopeRow 使用固定 96px 按钮宽度，内容区过窄时出现负坐标和越界

- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:237-276`（DomainRow，`ButtonWidth=96`）
- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:279-309`（ScopeRow，`ButtonWidth=96`）
- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:38`（`ButtonWidth = 96f`）

当内容区宽度约 100px（窗口约 240px）时：

- `DrawDomainRow` 的 `buttonRect = rect.xMax - ButtonWidth - 8f` 会得到负 x，显示名 label 也被挤到 1px 宽；
- `DrawScopeRow` 的按钮同样 `rect.xMax - ButtonWidth - 8f`，会画到左边界外，行 label 宽度为负数后取 1px；
- 这两行没有像 MoodRow 那样的“太窄降级为提示行”分支。

建议为 DomainRow / ScopeRow 增加窄屏降级布局（例如按钮换行、缩小按钮宽度或隐藏次要信息），并统一使用 `UiLayoutTier.ClampWidth` / 最小内容宽度保证。

#### 4. `FilterBarWidget` 窄屏按钮行无 clamp，内容区窄于约 236px 时横向溢出

- 文件：`Source/UniversalSqueaker/UI/Widgets/FilterBarWidget.cs:99-101`
- 文件：`Source/UniversalSqueaker/UI/Widgets/FilterBarWidget.cs:80-85`
- 文件：`Source/UniversalSqueaker/UI/Layout/UiLayoutTier.cs:33-36`（`ClampWidth` 未被使用）

`DrawRow` 计算 `buttonWidth = Math.Max(56f, (rect.width - Gap * (count - 1)) / count)`，但未 clamp 到 `rect.width`。窄屏第一行 4 个按钮最少需要 `4*56 + 3*4 = 236px`；在 `FerriteVoicePacksPage` 扣除 140px 导航后，窗口宽度 240–375px 的内容区只有 100–235px，按钮会超出右边界并互相重叠。作者按钮第二行同样使用固定 `SingleRowHeight` 与全宽，没有最小宽度保护。

建议在 FilterBar 内对按钮宽度做 `Math.Min(buttonWidth, rect.width / count)` 之类 clamp，或在页面层保证内容区不低于某个最小宽度；`UiLayoutTier.ClampWidth` 目前只被测试调用，没有接入任何 widget。

#### 5. 页面级窄屏 guard 检查的是窗口宽度而非内容宽度，导致内容区可低于 Minimal/Fallback 仍继续渲染

- 文件：`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs:57`
- 文件：`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs:127-132`
- 文件：`Source/UniversalSqueaker/UI/Layout/UiLayoutTier.cs:21-30`

`FerriteVoicePacksPage.Draw` 只在 `rect.width < 240` 时显示 “Window too narrow”，但 `rect.width` 是包含 140px 左侧导航的整个窗口内容宽度；实际右侧内容区为 `rect.width - NavWidth`。因此窗口宽度 240px 时内容区只有约 100px，远低于 `MinMinimalWidth = 240`，而页面仍继续用 LayoutEngine 渲染各 widget。这解释了上面 ScopeTree / FilterBar / PresetList 的窄屏溢出。

建议把 guard 改为基于内容宽度，例如 `rect.width < NavWidth + VoicePacksLayout.MinMinimalWidth` 时进入 fallback/EmptyState；或把 `UiLayoutTier` 的宽度语义统一为“扣除导航后的内容宽度”，并在 `FerriteVoicePacksPage` 使用同一口径。

### Minor

#### 1. FilterBar “All” 不清除作者过滤，且 All 选中态忽略作者过滤

- 文件：`Source/UniversalSqueaker/UI/Widgets/FilterBarWidget.cs:103-111`
- 文件：`Source/UniversalSqueaker/UI/Widgets/FilterBarWidget.cs:138-152`

“All” 按钮只把三个 domain flag 置 false，不清除 `UiPackFilter.Author`。若用户先选了某作者，再点 “All”，All 仍显示为选中（因为 domain flag 全空），但包列表仍被作者过滤。语义上容易让用户以为已重置全部过滤。建议把 All 定义为同时清空 `SetPackFilter Author|`（或提供单独的 “Author: All” 提示），并让 All 选中态同时要求 `Author` 为空。

#### 2. 帮助 banner 的 Measure 与 Draw 使用不同内容宽度，可能低估/高估预留高度

- 文件：`Source/UniversalSqueaker/UI/Widgets/GlobalVolumeWidget.cs:43,74`
- 文件：`Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs:63,111`
- 文件：`Source/UniversalSqueaker/UI/Widgets/CameraIndicatorWidget.cs:42,74`
- 文件：`Source/UniversalSqueaker/UI/Widgets/FilterBarWidget.cs:39,71`

这些 widget 在 `Measure` 中帮助 banner 使用 `VoicePacksLayout.InnerWidth(ctx.ViewWidth)`（约 `viewWidth - 16`），但在 `Draw` 中分别使用 `rect.width - LeftPadding - RightPadding`、`rect.width - LeftPadding - 8f` 或直接 `rect.width`。两者可能相差 4–16px；在文本换行临界宽度下，Draw 实际 banner 高度可能大于 Measure 预留高度，导致展开后遮挡后续内容，或反之留下多余空白。建议统一走同一个 `VoicePacksLayout.InnerWidth` 口径。

#### 3. `AttenuationEditorWidget` 窄屏下 help 展开不绘制 banner，只留空白

- 文件：`Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs:87-91`
- 文件：`Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs:109-117`
- 文件：`Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs:58-64`

`Measure` 在 help 打开时会把 banner 高度加入总高，但 `DrawCore` 的窄屏分支（`rect.width < MinWidth`）在绘制 help 按钮后直接 `DrawNarrowSummary` 并 return，跳过了后面的 `UsHelp.DrawBanner`。因此窄屏下 help 打开只会产生一段空白，帮助文本不显示。建议把窄屏分支改为先处理 help banner（与宽屏一致），再绘制窄摘要。

#### 4. 衰减编辑器拖拽状态是 static，且没有在会话/页签生命周期中重置

- 文件：`Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs:40-44`
- 文件：`Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs:99-103`
- 文件：`Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs:168-228`

`_dragging` / `_dragMin` / `_dragMax` / `_originalMin` / `_originalMax` 是 static 字段。若拖拽过程中窗口关闭、页签切换或 MouseUp 丢失，`_dragging` 会残留到下一次绘制，可能把上一次的 `_dragMin/_dragMax` 当作当前值，并需要额外一次 MouseUp 才清除。建议把拖拽状态放到 widget 实例或页面 state，并在 `BeginSession/EndSession` 重置。

#### 5. 过滤导致选中域 fallback 时可能返回 Race 域却不更新 `SelectedScope`

- 文件：`Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs:641-668`

当当前选中的 Xenotype 域被过滤掉、但仍有 Race 域时，`ResolveSelectedDomain` 会返回第一个 Race 域，却没有同步把 `state.SelectedScope` 改为 `Race`。虽然绘制时各 Layer 用 `SelectedDomain.Scope` 判断选中，视觉可能正常，但状态机里 `SelectedScope` 与 `SelectedRaceDefName/TargetName` 不一致，后续过滤清除或页签切换可能跳到意外域。建议在 fallback 分支同步写回 `state.SelectedScope`。

### Nit

#### 1. `UiPackFilter.EnabledOnly` 与 `UiDomainFilter.Author` 是纯函数/测试中的能力，但 UI 从未设置

- 文件：`Source/UniversalSqueaker/UI/Layout/VoicePacksFilters.cs:28-38,77-96`
- 文件：`Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs:175-216`

`SetPackFilter` 支持 `EnabledOnly`，`SetDomainFilter` 支持 `Author` 字段，但 `FilterBarWidget` 只发 domain 三键和 `Author|...`。当前这些能力是死 API（测试覆盖了但玩家不可达）。如果产品不需要，建议删除以减少双轨；如果需要，应补 UI 入口。

#### 2. `UiLayoutTier.ClampWidth` 只有测试调用，没有任何 widget 使用

- 文件：`Source/UniversalSqueaker/UI/Layout/UiLayoutTier.cs:33-36`
- 文件：`tools/UniversalSqueakerUiLogicTests/Program.cs:41-42`

这与 Major 4/5 直接相关：工具函数存在但没有接入窄屏 clamp，导致实际溢出无人防护。建议在 FilterBar、ScopeTree、PresetList 等固定宽度控件中实际使用该 helper（或移除它）。

#### 3. 衰减拖拽过程中状态行仍显示旧 preset 名

- 文件：`Source/UniversalSqueaker/UI/Widgets/AttenuationEditorWidget.cs:96,124,230-239`

拖拽期间 `min/max` 已用 `_dragMin/_dragMax` 更新，但 `preset` 仍来自 view state；只有松手发 `SetDistanceRange` 后下一帧才变为 Custom。因此拖拽中可能显示 “Balanced 15-60” 之类的瞬时不一致。属于小 UX 瑕疵，可在拖拽状态中临时显示 “Custom” 或暂不显示 preset。

## 建议

1. **修复 help 绘制顺序**：把 `UsHelp.DrawHelpButton` 移到所有内容 surface 之后绘制，或让所有内容 surface 在右上角 helpRect 处留出排除区；建议用一个公共 helper 统一“先内容、后 help”顺序，避免每个 widget 各自维护。
2. **统一窄屏宽度口径**：`FerriteVoicePacksPage` 的 “Window too narrow” guard 改为基于 `rect.width - NavWidth`（至少 `NavWidth + MinMinimalWidth`）；`UiLayoutTier` 的阈值注释明确是内容宽度还是窗口宽度。
3. **修复 ScopeTree 窄屏布局**：统一 stacked LayerRow 的 Measure/Draw 高度；为 DomainRow/ScopeRow 增加窄屏降级布局；补 UiLogic 测试覆盖 480/320/240 窗口宽度。
4. **修复 FilterBar 横向溢出**：按钮宽度 clamp 到可用宽度，或窄屏改为可换行的两行布局；All 按钮同时清除作者过滤并正确反映组合过滤状态。
5. **修复 Attenuation 窄屏 help**：窄屏分支先绘制 help banner 再绘制摘要；同时将拖拽状态从 static 改为可重置状态。
6. **统一帮助 banner 的 Measure/Draw 宽度**：所有 widget 的 `Measure` 与 `Draw` 都使用同一个 `VoicePacksLayout.InnerWidth` 计算 banner 高度。
7. **命令层防御**：在 `VoicePacksPageModel` 或 settings setter 对 `SetGlobalVolume` / `SetDistanceRange` 增加 `float.IsFinite` 校验，避免未来程序化命令写入 NaN/Infinity。
8. **清理死 API**：若 `UiPackFilter.EnabledOnly` / `UiDomainFilter.Author` 不打算暴露，删除对应纯函数字段；若保留，补 UI 入口或至少文档说明。
