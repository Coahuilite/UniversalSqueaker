# US UI 排查报告（T1）

> 状态：排查完成，未修改业务代码。
> 依据：`docs/ui-fix-triage-plan-zh.md`、`docs/ui-feedback-consolidated-zh.md`、`docs/ui-fix-task-books-zh.md`。
> 基线：`dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` 当前全部通过（仅证明现有测试未覆盖下文风险）。

---

## 1. 排查方法

1. 使用 `grep` 在 `Source/` 下枚举 `UiInteract.Button/Row/Protect` 静态注册点，共 **26 处**（运行时 `DropdownWidget` 选项行、`StepperSliderWidget` 等会按数据展开为多个实例）。
2. 对每个注册点检查：
   - 注册前是否绘制了可见表面/文字/图标；
   - 是否可能被后续不透明 surface 覆盖；
   - 是否位于滚动视图内、命中测试是否经过 `UiInteract.ToPageSpace`。
3. 阅读 `UiInteract.cs`、`DropdownWidget.cs`、`FerriteVoicePacksPage.cs`、`LayoutEngine.cs`，核实 `PushScrollView/PopScrollView`、`DrawPopups`、`ToPageSpace` 调用时序。
4. 审计所有涉及硬编码行高（24/26/28/30/50/74 等）和多行文本的 widget/component，对照 `ITextMetrics`/`FerriteTextMetricsAdapter` 使用情况。
5. 收集所有“切换选取项”控件，与只读证据 `../squeaky_ratkin/Source/SqueakyRatkin/UI/SqueakySettingsUI.cs` 对照。

---

## 2. 问题清单

### 2.1 G2 交互通路问题（全局高风险）

#### 已确认问题

| # | 文件:行 | 原因 | 严重度 | 建议 |
|---|---|---|---|---|
| G2-1 | `Source/UniversalSqueaker/UI/Components/VoicePackChecklist.cs:67-86` | `DrawOrphanBanner` 只画了 warning 底和提示文字，`UiInteract.Button(button, ...)` 注册的 Forget Unavailable 按钮**没有绘制任何表面/文字/边框**。玩家看不到可点击按钮，反馈中的“红色提示无法消掉”因此成立。 | 高 | 在按钮 rect 上绘制 `SurfaceFrame`/`UsSurface.DrawSegment` 并写 “Forget Unavailable” 文本；保留 `UiInteract.Button` 注册。 |
| G2-2 | `Source/UniversalSqueaker/UI/Widgets/PresetListWidget.cs:155-174` | `DrawPresetHeader` 注册 `UiInteract.Button(importRect, ...)`，但 `importRect` **没有可见绘制**；只有预设标题/摘要。Import 按钮不可见、不可发现。 | 高 | 给 importRect 绘制按钮表面+文字（建议原版/SR Primary 风格），或改用现有按钮组件。 |
| G2-3 | `Source/FerriteLib.UiKit/Widgets/LineChartWidget.cs:157-205` | `HandleDrag` 直接使用 `UiInteract.PointerPosition()`（页面/屏幕坐标）与 `plotRect`（内容局部坐标）做距离判断；当图表位于滚动视图内且 `ScrollPosition != 0` 时，命中坐标没有做 `ToPageSpace`/逆变换，拖拽节点会错位。这是“衰减折线图破碎且无法使用”的代码级根因候选。 | 高 | 在 `LineChartWidget` 内把 `plotRect` 用 `UiInteract.ToPageSpace` 转为页面坐标后再做指针命中；或把指针转换为内容坐标。需暴露 `ToPageSpace`。 |
| G2-4 | `Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:381-389` | `DrawMoodRowBody` 在 `groupWidth < 96f` 时只画 “Window too narrow” 提示并 `return`，**Mood Auto 清空按钮未绘制也未注册**。窄屏下该控件通路消失。 | 中 | 窄屏也至少保留 Auto 按钮，或把整个 mood 行换行/换列而不是直接隐藏操作。 |

#### 排查后判定“代码上可用”的特别项

- **相机高度指示器**（`CameraIndicatorWidget.cs:83`）：Row 有表面、文字、checkbox，且在滚动视图内通过 `UiInteract.Row` 注册（`ToPageSpace` 生效），代码上可点击。
- **相机高度预设按钮**（`AttenuationEditorWidget.cs:184-188`）：三个 preset 按钮都调用 `UsSurface.DrawSegment` 后注册 `UiInteract.Button`，代码上可点击；但选中态未标识（见 G4）。
- **Tuning layer 按钮**（`ScopeTreeWidget.cs:198-215`）：可见、可点击。
- **预设行 / 种族 / 异种行**（`PresetListWidget`、`RaceLayerRow`、`XenotypeLayerRow`）：可见、可点击。

> 这些项仍建议游戏内实机确认一次；若实机不可用，重点检查运行时是否被其他 `BeginScrollView` 或全屏绘制覆盖。

#### 全部注册点速查表

| 注册点 | 可见绘制 | 滚动命中 | 结论 |
|---|---|---|---|
| `StepperSliderWidget.cs:120/127` −/+ | 有 | 正常 | OK |
| `LineChartWidget.cs:82` Protect | 有 | 拖动命中坐标有风险 | G2-3 |
| `ScopeTreeWidget.cs:200/214/410` | 有 | 正常（窄屏 Auto 除外） | G2-4 / OK |
| `DropdownWidget.cs:102/108/138` | 有（108 为关闭用隐形层，有意） | popup 坐标错误 | G3 |
| `PresetListWidget.cs:173/177/197/218` | import 无绘制，其余有 | 正常 | G2-2 / OK |
| `FilterBarWidget.cs:137/151` | 有 | 正常 | OK |
| `FerriteVoicePacksPage.cs:557` nav | 有 | 正常 | OK |
| `CameraIndicatorWidget.cs:83` | 有 | 正常 | 需实机确认 |
| `BasicTuningWidget.cs:140/162/198` | 有 | 正常 | G1 文本 |
| `AttenuationEditorWidget.cs:187` | 有 | 正常 | G4 |
| `VoicePackChecklist.cs:79` | **无** | 正常 | G2-1 |
| `SearchField.cs:14` Protect | 有 | 正常 | OK |
| `XenotypeLayerRow.cs:45` / `RaceLayerRow.cs:45` / `VoicePackRow.cs:49` | 有 | 正常 | G1 候选 |

### 2.2 G3 下拉弹层定位（全局高风险）

#### 根因结论

1. `UiInteract.ToPageSpace` 是 `private`，只在 `Button`/`Protect` 注册时使用（`UiInteract.cs:298-311`）。
2. `DropdownWidget.Draw` 在滚动视图内被调用时，`rect` 是内容局部坐标；`RegisterPopup` 的闭包捕获了这个 `rect`（`DropdownWidget.cs:106-145`）。
3. `FerriteVoicePacksPage.DrawSingleColumn/DrawMultiColumn` 在 `engine.Draw` 后先 `UiInteract.PopScrollView()` 再 `Widgets.EndScrollView()`（`FerriteVoicePacksPage.cs:350-359`、`414-426`）。
4. `UiInteract.DrawPopups()` 在 `DrawContent` 返回之后执行（`FerriteVoicePacksPage.cs:153`）。此时 `ScrollTransforms` 已清空，闭包内绘制 `listRect`/`rowRect` 和注册按钮时 `ToPageSpace` 变成恒等变换，因此：
   - popup 画在内容坐标位置，而不是滚动视图页面坐标位置；
   - popup 按钮的命中测试同样错位。
5. 结论：**已知线索成立**。修复应让 `DropdownWidget` 在 `RegisterPopup` 前把 `rect` 转换为页面坐标（或捕获当时的 `ScrollTransforms` 快照），闭包内直接使用页面坐标。

#### 所有 dropdown 使用点

| 位置 | 类型 | 说明 |
|---|---|---|
| `ScopeTreeWidget.cs:265-272` `DrawDomainRow` | 动态构造 `DropdownWidget` | Tuning Editor 的 Layer domain 下拉，受 G3 影响 |
| `ScopeTreeWidget.cs:307-312` `DrawScopeRow` | 动态构造 `DropdownWidget` | 每个 Action Scope 下拉，受 G3 影响 |
| `Layout.xml` | 无 `input/dropdown` | 当前 XML 静态布局没有 dropdown |
| 包管理作者筛选 | 尚未实现 | 反馈要求做成下拉；若沿用 `DropdownWidget`，需先修 G3 |

### 2.3 G1 文本高度/裁剪问题（全局高风险）

#### 已确认裁剪点

| # | 文件:行 | 原因 | 建议 |
|---|---|---|---|
| G1-1 | `BasicTuningWidget.cs:138` + `DrawLabel:217-229`（EggRow） | 行高 28，但 `DrawLabel` 画两行：label 在 `y+4..24`，subLabel 在 `y+20..40`，第二行超出行高/卡片 body。 | 两行行高改为动态 `CalcHeight`，至少 ≥40；或改成单行 + 省略。 |
| G1-2 | `BasicTuningWidget.cs:160` + `DrawLabel:217-229`（DistanceRow） | 同上，Distance 行高 28 放两行。 | 同上。 |

#### 固定行高未做文本测量（高风险候选）

| # | 文件:行 | 固定高度 | 风险 |
|---|---|---|---|
| G1-3 | `VoicePacksLayout.cs:19` + `VoicePackRow.cs:35-45` | 74 | 三行固定位置（25/20/20）。长 Label/ModName/Coverage 换行时会裁切或重叠。 |
| G1-4 | `BasicTuningWidget.cs:22` + `:191` | 26 | 三个 Basic toggle 单行；窄屏下 label 若换行会裁切。 |
| G1-5 | `ScopeTreeWidget.cs:34` + `:285` | 24 | Action Scope 行 label 固定 20 高；长动作名换行会裁切。 |
| G1-6 | `PresetListWidget.cs:25/26` + `:190/211` | 24/22 | Race/Xenotype 行 label 固定高度，长名称换行会裁切。 |
| G1-7 | `RaceLayerRow.cs:32-39` / `XenotypeLayerRow.cs:32-39` + `VoicePacksLayout.cs:17` | 50 | 两行固定 25+18；长名称换行会裁切。 |

#### 已使用动态文本测量的位置（无需改）

- `VoicePacksLayout.BannerHeight` / `SectionHeaderHeightFor`：使用 `ITextMetrics.CalcHeight`。
- `PresetListWidget` 的预设描述：使用 `metrics.CalcHeight`。
- `UsHelpPanel`：使用 `ctx.Metrics.MeasureText` + 滚动视图。
- `InputModeRowWidget` / `InputModeCardWidget`：使用 `MeasureText`。

### 2.4 G4 视觉语言（全局高风险）

#### SR 证据摘要（`../squeaky_ratkin/.../SqueakySettingsUI.cs`）

- Primary/Danger 按钮：底部金色/红色条 `rect.yMax - 3f`（第 90-93 行）。
- `SettingSelector`：左侧金色竖条 `x+1, y+1, 3, height-2`（第 166-167 行）。
- `SelectableCard`：选中时底部金色条（第 47-50 行）。
- `FilterChip`：选中态使用金色文字/底色的“chip”（第 221-238 行）。

#### 当前“切换选取项”控件清单

| # | 控件 | 文件:行 | 当前视觉 | 建议 |
|---|---|---|---|---|
| G4-1 | 模式卡 `InputModeRowWidget` / `ModeCardRenderer` | `ModeCardRenderer.cs:11-41` | 已有选中底部金色条，但整体仍是自绘扁平卡，维护者认为不如原版/SR | 换用 SR `SelectableCard` 语言或原版按钮包装；保留底部金色条。 |
| G4-2 | Tuning layer segment | `ScopeTreeWidget.cs:174-218`、`:471-481` | `UsSurface.DrawSegment`（pill），无 SR 左/底高亮条 | 恢复 SR `SettingSelector` 左侧金色条，或 Primary 底部金色条。 |
| G4-3 | Action Scope 下拉触发框 | `ScopeTreeWidget.cs:307` + `DropdownWidget.cs:90-100` | 自绘 field + caret | 换成原版 `Widgets.Dropdown` 视觉包装，或 SR `SettingSelector` 语言。 |
| G4-4 | Domain 下拉触发框 | `ScopeTreeWidget.cs:265` + `DropdownWidget.cs:90-100` | 同上 | 同上。 |
| G4-5 | FilterBar chip/segment | `FilterBarWidget.cs:92-126`、`:148-152` | `UsSurface.DrawSegment`（pill） | 换成 SR `FilterChip`/原版按钮；选中态金色文字+底。 |
| G4-6 | AttenuationEditor 预设按钮 | `AttenuationEditorWidget.cs:173-188` | `UsSurface.DrawSegment`，且未标识当前选中 preset | 换成 SR/原版按钮，并显示当前选中态。 |

#### 已符合 SR 语言、应保留

- 左侧导航 active item：`FerriteVoicePacksPage.cs:541-546` 已使用左侧金色 accent bar。
- Race/Xenotype 层选中行：`UsSurface.DrawRowSurface` selected 分支已有左侧金色 bar（`UsSurface.cs:53-58`）。

---

## 3. 回归测试清单

> 以下为建议新增/扩展的自动化测试；当前仓库已有 `tools/FerriteLib.UiKit.Tests`，可优先放入该工程（需要 stub Verse/Unity 已具备）。

| 风险 | 测试文件（建议） | 模拟步骤 | 断言 |
|---|---|---|---|
| G3 | `tools/FerriteLib.UiKit.Tests/DropdownWidgetTests.cs` | `BeginFrame` → `PushScrollView(new Rect(100,50,300,300), new Vector2(0,20))` → 在内容坐标 `(0,0,200,28)` 绘制 dropdown → 用页面坐标点击 field 打开 → `DrawPopups` → 用页面坐标点击第一个选项 | 选项行按钮回调触发，且 `state.Open == false`；预期修复前该点击落在错误坐标，测试失败 |
| G3 | `tools/FerriteLib.UiKit.Tests/InteractionTests.cs` | 暴露 `UiInteract.ToPageSpace` 后，在 scroll transform 激活时断言 `ToPageSpace(contentRect)` 与 `outRect + content - scroll` 一致 | 返回页面坐标；`DrawPopups` 后仍可命中 |
| G2 | `tools/FerriteLib.UiKit.Tests/InteractionTests.cs` | 模拟 `LineChartWidget` 在 `PushScrollView(outRect, scroll)` 内绘制，设置鼠标为页面坐标下某节点位置，`DebugMouseDown` 后拖到新位置 | `PointChanged` 命令发出；修复前因坐标差一个 scroll offset 不发/错发 |
| G2 | `tools/UniversalSqueakerUiLogicTests` 或新增 `VoicePackChecklistTests` | 构造 `OrphanCount>0` 的 domain，绘制 `VoicePackChecklist.Draw`，把鼠标放在 Forget 按钮 rect，`UiInteract.DebugClick = true` | 发出 `ForgetUnavailable` 命令；同时可加“按钮 rect 必须被绘制表面覆盖”的结构断言 |
| G2 | 同上或 `PresetListWidgetTests` | 构造含一个 preset 的 view state，绘制 `PresetListWidget`，点击 importRect | 发出 `ImportBaselinePreset` 命令；并断言 importRect 有可见绘制（如绘制调用计数/截图桩） |
| G2 | `ScopeTreeWidgetTests`（若可脱离 Verse）或布局测试 | 窄宽度下绘制 Mood row | 断言 Auto 按钮 rect 仍注册且可点击，而不是早退隐藏 |
| G1 | 新增 `TextHeightRegressionTests` | 用 stub metrics 返回两行高度，绘制 `BasicTuningWidget` 的 Egg/Distance row | 行高 ≥ `labelHeight + subLabelHeight + padding`；或 `Measure(ctx)` 高度 ≥ 两行基线 40/行 |
| G1 | 新增 `VoicePackRowHeightTests` | 用长文本构造 `VoicePackRowView`，调用 `VoicePacksLayout.ChecklistHeight` 或绘制 row | 行高 ≥ 三行 `CalcHeight` 之和；修复前固定 74 在长文本下不足 |
| G4 | 新增 `SelectionVisualRegressionTests` | 对每个 G4 控件绘制选中态/非选中态，收集绘制调用（可注入 `VerseWidgets.DrawBoxSolid` 桩） | 选中态包含左侧/底部金色条绘制；或至少不再直接调用 `UsSurface.DrawSegment` 作为最终选中样式 |

---

## 4. 批次映射

| 批次 | 内容 | 关联问题 |
|---|---|---|
| **A**（全局阻断） | G2 不可用/不可命中控件 + G3 下拉定位 | G2-1、G2-2、G2-3、G2-4、G3 |
| **B**（可读性） | G1 文本高度/行高统一 | G1-1、G1-2、G1-3～G1-7 |
| **C**（视觉统一） | G4 原版/SR 语言替换 | G4-1～G4-6 |
| **D**（中/低风险） | sticky layer、Action Scope 分组/过滤、包管理联动、作者下拉、黯淡显示、图表节点 hover 等 | M1、M2、低风险项 |

---

## 5. 仍无法复现 / 需游戏内确认的项

| 项 | 状态 |
|---|---|
| 相机高度指示器（`CameraIndicatorWidget`） | 代码上可见可点；实机“不可用”反馈无法从静态代码复现，需游戏内确认是否被覆盖或另有通路。 |
| 相机高度预设按钮（`AttenuationEditorWidget`） | 代码上可见可点；但无选中态，可能是“看起来可用却不知当前状态”而非完全不可点，需实机确认。 |
| LineChartWidget 拖拽坐标问题 | 根据 `UiInteract` 测试桩推断为内容/页面坐标不一致；若 Unity IMGUI 在 `BeginScrollView` 内对 `Event.mousePosition` 有额外变换，行为需实机复核。 |
| Tuning layer / Mood Auto 常规宽度 | 代码上可见可点；Mood Auto 仅在窄屏分支确认缺失。 |
| 下拉“出现在左侧某个位置” | 与代码根因一致，但具体偏移方向/数值需游戏内截图进一步确认。 |

---

## 6. 汇总数字

- G2 确认问题控件：**4 处**（Forget 按钮、Preset Import 按钮、LineChart 滚动拖拽、窄屏 Mood Auto）。
- G3 根因：**确认已知线索**（popup 闭包捕获内容坐标，`DrawPopups` 在 `PopScrollView` 后执行，`ToPageSpace` 不可用导致坐标未转换）。
- G1 确认裁剪点：**2 处**（BasicTuningWidget Egg/Distance 两行行）；另记录 **5 处**固定行高候选。
- G4 替换清单：**6 项**（模式卡、Tuning layer、Action Scope 下拉、Domain 下拉、FilterBar chip、Attenuation preset 按钮）。
