# US 设置页 UI/UX 行业调研与页面布局评估

> 任务：T0 —— UI/UX 行业最佳实践调研 + 当前 US 设置页面布局评估
> 日期：2026-08-30（与现有 UI 反馈/任务书同步）
> 范围：只读调研 + 报告；不修改代码、不运行构建。
> 依据源码：
> - `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`
> - `Source/UniversalSqueaker/UI/Layout.xml`
> - `Source/UniversalSqueaker/UI/Layout/VoicePacksLayout.cs`
> - `Source/UniversalSqueaker/UI/Components/*.cs`
> - `Source/UniversalSqueaker/UI/Widgets/*.cs`
> - `Source/FerriteLib.UiKit/`
> - 现有文档：`docs/ui-feedback-consolidated-zh.md`、`docs/ui-fix-task-books-zh.md`、`docs/ui-ux-research-zh.md`、`docs/us-ui-review-requirements-zh.md`

---

## 1. 摘要

US 当前设置页已经具备一个相当好的基础形态：左侧导航 + 中间内容 + 右侧固定帮助的三栏骨架、按 Basic/Tuning/Packs 分页签、800×600 硬下限、深色 + 金色主题，均符合“桌面软件设置页 + RimWorld 热门模组”的主流方向。

但从行业最佳实践和现有维护者反馈看，主要问题不在“三栏是否应该存在”，而在**分层、可发现性、控件一致性和窄屏细节**：

1. **帮助系统只有“单条段落”，缺少 Camera+ 式的“分区总览 + 单项高亮 + 单项独立帮助”**，也没有随悬停/焦点切换。
2. **Tuning Editor 在 800×600 下会把 Mood 控制整体降级为一行提示**，最低分辨率下核心调音能力不可用。
3. **自绘控件状态不统一**：下拉、步进按钮、图表拖点缺少 hover/focus/可拖拽提示；部分按钮高度只有 20px，低于桌面最小可点击尺寸。
4. **内容结构仍是扁平 Widget 列表**，没有用容器表达“功能块”，左侧导航与内容标题/锚点体系不够强。
5. **1440p 多列布局的列宽由固定比例决定**，会造成 Basic 右侧只放一个衰减编辑器时大量留白，列高度不均衡。

最重要的 5 条建议（详见 §4）：

- **P0-A**：把帮助从“单段文本”升级为“分区总览 + 当前项帮助 + 悬停/选中联动”，并让帮助面板标题跟随当前区块。
- **P0-B**：修复 800×600 下 Mood 控制不可用；Mood 行在窄宽度下应纵向换行/降级为可用的紧凑控件，而不是整行失效。
- **P0-C**：统一“可交互控件”的可发现性：下拉触发框、步进 ±、图表拖点必须至少有 hover/选中/可拖拽视觉；所有可点目标最小高度提到 24–26px（推荐 28–32px）。
- **P1-D**：用容器（Block/Section/Column）重建 Layout.xml 的功能块结构，并让页面过滤器递归处理容器；内容区顶部显示当前功能块标题。
- **P1-E**：重构 1440p 多列分配：由内容高度/列内容决定列宽，或至少让 Basic 右侧与左侧一样容纳多个相关卡片，避免“宽列只有一个控件”的浪费。

---

## 2. 行业最佳实践

### 2.1 桌面软件设置页

主流桌面平台 HIG 对设置页的共识是：

- **左侧导航 + 右侧内容**是复杂设置页的标准形态；导航项应保持少量、稳定、高层级，不做过深嵌套。
- 设置项应按**用户任务**分组，而不是按实现/内部模块分组；每组有一个清晰标题。
- 常用设置优先展示，高级/危险设置渐进披露（折叠、次级页面或“高级”分组）。
- 每个设置项应有即时反馈，修改后状态可见（保存中/已保存/失败）。
- 帮助/说明应放在用户当前关注点附近；不打断主流程，不要求用户主动查找。

参考：
- [Windows 应用设置指南（Microsoft Learn）](https://learn.microsoft.comzh-cn/windows/apps/design/app-settings/guidelines-for-app-settings)
- [Apple 人机界面指南](https://developer.apple.com/design/human-interface-guidelines/)
- [KDE Human Interface Guidelines](https://develop.kde.org/hig/)
- [GNOME HIG](https://developer.gnome.org/hig/)

### 2.2 游戏模组设置界面

- RimWorld 生态没有官方统一 UI 规范；社区事实标准是 **Camera+ 的三栏 master-detail + 上下文帮助**。
- Camera+ 的关键做法：
  - 左栏是 topic 导航，包含 “All” 与各分区；点击后重置滚动位置；
  - 中栏只显示当前 topic 的连续内容；
  - 右栏固定帮助面板；帮助文本跟随当前 topic，甚至跟随悬停项；
  - 大标题处有“整个功能区的帮助总览”，单项又有高亮与独立说明。
- 这对 US 是直接可借鉴的参考，仓库内 `docs/ui-ux-research-zh.md` 已有证据整理，本报告不重复造轮子，只在此衔接。

参考：
- [CameraPlus 仓库](https://github.com/pardeike/CameraPlus)
- [LukeW — Unified Settings & Tutorial](https://lukew.com/ff/entry.asp?936)
- [Contextual placement patterns](https://raw.githubusercontent.com/rampstackco/claude-skills/e11b5eaa26ad42009788eaaad4228998507f44e5/skills/interactive-product-tour/references/contextual-placement-patterns.md)

### 2.3 表单与交互控件

行业对常用控件的选择有明确建议：

| 场景 | 推荐控件 | 理由 |
|---|---|---|
| 2–5 个互斥选项 | 分段控件 / 单选卡 | 一眼可见全部选项，点击成本低 |
| 较多选项或空间不足 | 下拉菜单 | 节省空间；长列表应支持分组/搜索 |
| 数值连续调节 | 滑条 + 数值输入 + 步进 | 拖拽直观、输入精确、步进微调 |
| 独立开关 | 复选框 / 开关 | 状态可扫读 |
| 危险/破坏性操作 | 显式按钮 + 必要时确认 | 防止误操作 |

控件通用要求：

- **可发现性**：可点控件必须有可见表面、hover 态、选中态；拖拽点必须暗示“可拖动”。
- **点击目标**：桌面端建议高度 ≥24–32px；游戏手柄/触屏更高（44px）。当前多处 16–20px 目标偏小。
- **状态反馈**：错误、空状态、无候选、冲突、孤儿都应可视化，并给出可执行的下一步。
- **键盘可访问性**：即使游戏以鼠标为主，也应保证 Tab/方向键/Enter/Esc 至少覆盖导航、下拉、按钮。

### 2.4 可访问性

- 文本对比度建议参考 WCAG：正文 4.5:1、大字/强调 3:1；不应只靠颜色表达状态（如选中还需边框/形状/勾选）。
- 小字号（RimWorld `GameFont.Tiny`）只适合辅助说明；核心操作标签尽量使用 `Small`。
- 错误/空状态不应只显示一段灰色文字；应配合状态色、图标、动作按钮。
- 所有交互控件应有关键盘焦点可见态，而不仅是鼠标 hover。

### 2.5 与仓库内既有调研的衔接

`docs/ui-ux-research-zh.md` 已确认：

- 三栏布局在宽屏下成立；
- 窄屏应隐藏/降级右帮助、左导航变 tab/抽屉；
- 但维护者后续锁定 **800×600 仍保留右帮助面板、不使用帮助按钮**（`docs/us-ui-review-requirements-zh.md` R18）。

因此本报告在“窄屏”上不再建议“隐藏右帮助”，而是评估“在保留右帮助的前提下如何保住内容可用性”。

---

## 3. 当前页面布局评估

### 3.1 信息架构与导航

**现状**

- 左侧导航按 `NavGroups` 分为三组：基础设置 / 调音 / 包清单，组内列出小节项（`FerriteVoicePacksPage.cs:572-604`）。
- 点击小节项会执行 `ScrollToSection`，同时把 `ActiveTab` 切到对应组（`VoicePacksPageModel.cs:235-241`）。
- 每个页签显示独立连续滚动内容；切页签时 `ScrollPosition` 会归零（`VoicePacksPageModel.cs:228-232`，已实现）。
- `Layout.xml` 仍是扁平根 `<Widget>` 列表；每个 widget 自带卡片标题，但内容区没有“当前功能块”的容器标题。

**问题**

1. **“功能块标题”缺位**：左导航有“基础设置/调音/包清单”，但内容区顶部始终是全局 `VoicePack Routing`（`Layout.xml:3`），没有每个功能块自己的大标题/总览。导航锚点实际锚到卡片/小节，而不是功能块。
2. **Tuning 组信息层级不足**：`scope-tree` 一个 widget 同时包含 Action Scope 和 Mood Tuning（`ScopeTreeWidget.cs:29-30`），但导航项只叫“动作作用域”；用户无法从导航判断里面还有 Mood 调音。
3. **长列表缺少查找/分组**：Action Scope 有 17 行（`VoicePacksPageModel.BuildActionScopes` 遍历 `Enum.GetValues`），没有搜索、没有“自主/可操作”分组、没有隐藏 Biotech 防御性动作。这与维护者反馈 M2 一致。
4. **没有滚动联动高亮**：导航高亮只在点击锚点时更新；用户手动滚动后，左侧“当前小节”不会跟随内容位置变化，缺少位置感。
5. **容器能力未接入页面过滤**：`LayoutManifest` 已支持 `Block/Section/Column`，但 `FerriteVoicePacksPage.GetEngine/GetEngineAll` 只遍历根级 `<Widget>` 并按 `Tab/Column` 过滤（`FerriteVoicePacksPage.cs:199-219`）。一旦把 XML 改成嵌套容器，现有过滤逻辑不会递归处理子节点。

### 3.2 三栏布局与响应式

**现状**

- 窗口：`UniversalSqueakerSettingsWindow.cs` 默认 72%×66%，60%–75% 浮动，硬下限 800×600。
- 三栏宽度（`FerriteVoicePacksPage.cs:280-292`）：
  - ≥1200：左 176 / 右 200；
  - ≥1000：左 168 / 右 180；
  - 800：左 152 / 右 160。
- 800×600 实际数据：窗口 800×600 → 内容区约 760×524 → 左 152 + 中 448 + 右 160（帮助面板实际宽 148，含 8px padding 后文本宽约 132）。
- 1080p 默认：窗口约 1382×713 → 内容区约 1342 → 左 176 + 中 966 + 右 200；由于 `DrawContent` 的 `contentRect.width >= 1000` 才多列（`FerriteVoicePacksPage.cs:320-327`），1080p 默认走**单列**。
- 1440p：内容区约 1803 → 中 1427 → 走多列；列宽按 `leftRatio = 0.38`、`minColumnWidth = 260` 分配（`FerriteVoicePacksPage.cs:370-382`）。

**问题**

1. **800×600 内容区偏窄，且 Tuning 的 Mood 控制直接失效**：详见 §3.4。内容 448px 本身可容纳单列，但 `ScopeTreeWidget` 的 Mood 行在 `groupWidth < 96` 时只显示“Window too narrow for mood controls”（`ScopeTreeWidget.cs:381-389`）。按当前行宽推算，800×600 下约 82px/组，确实进入该分支。
2. **1080p 和 1440p 之间有一道 1000px 阈值跳变**：1080p 默认单列、1440p 默认多列；窗口尺寸固定不可由玩家连续调节时，这种“刚好跨过阈值”的切换会造成不同分辨率下信息密度突变。
3. **1440p 多列浪费**：Basic 右侧列只有 `attenuation-editor`（`Layout.xml:13`），按 0.38/0.62 分配后右列约 877px，但只有一个卡片；左侧三张卡片堆叠，右列下方大片空白。列宽应由内容高度/数量决定，而不是固定比例。
4. **帮助面板在 800×600 极窄**：实际帮助面板约 148px，文本宽约 132px；这可以接受，但必须配合 Tiny 字号 + 内部滚动。当前已实现内部滚动（`UsHelpPanel.cs:48`），方向正确。
5. **footer 横跨内容 + 帮助两栏**：`footerRect` 从导航右侧一直画到窗口右缘（`FerriteVoicePacksPage.cs:150`），与“右侧内容区 sticky footer”的直觉略有不一致，但不是高优先级。

### 3.3 帮助系统

**现状**

- 右侧 `UsHelpPanel` 始终绘制，不按宽度隐藏（`FerriteVoicePacksPage.cs:139-144`），满足“800×600 也保留右帮助”的需求。
- 帮助内容来自 `UsHelpCatalog`，每个 section key 一段文字（`UsHelpCatalog.cs:12-26`）。
- 帮助面板标题固定为 “Help”（`UsHelpPanel.cs:32`），没有显示当前区块名。
- 未选中任何小节时显示 “Select a section to see its help here.”（`UsHelpPanel.cs:17`）。

**问题**

1. **缺少“分区总览”**：没有在内容区顶部显示当前功能块的总体说明；右帮助只展示当前小节单条文本。
2. **缺少“单项高亮/单项帮助”**：Camera+ 能对具体设置项做高亮并显示该项帮助；当前只有卡片级 help key，没有行级/控件级 help，也没有 hover 联动。
3. **帮助与当前焦点脱节**：用户手动滚动或使用控件时，右侧帮助不会跟随；必须点左导航才更新。
4. **帮助面板不显示当前区块名**：只写 “Help”，玩家需要靠内容判断自己在哪个区；应显示“帮助 — 路由模式”之类标题。
5. **帮助文案只有英文**：`UsHelpCatalog` 注释也说明 localize 不在当前范围，但中文报告仍应标记为后续可访问性/本地化风险。

### 3.4 表单与交互

**现状**

- 已落地：模式卡（`InputModeRowWidget`/`ModeCardRenderer`）、下拉（`DropdownWidget`）、滑条 + 数值输入 + 步进（`StepperSliderWidget`）、搜索框（`SearchField`）、筛选段按钮（`FilterBarWidget`）、衰减图（`LineChartWidget`）。
- 状态反馈：多数行有 hover/selected（`UsSurface.DrawRowSurface`）；段按钮有 hover/active（`UiPanel.DrawPill`）；搜索框有 focus 金线（`SearchField.cs:33-35`）。

**问题**

1. **下拉控件缺少 hover/focus/disabled 态**：`DropdownWidget.DrawField` 只有 Raised/Selected 两种表面（`DropdownWidget.cs:315-328`），鼠标悬停无反馈；也没有键盘操作（方向键选择/Esc 关闭）。滚动视图内 popup 定位是已知 G3。
2. **步进按钮缺少 hover 反馈**：`StepperSliderWidget.DrawButton` 只画 `Raised`（`StepperSliderWidget.cs:177-181`），−/＋ 是否可点只能靠经验判断。
3. **Mood 控制点击目标过小**：步进按钮 `ButtonWidth = 16`（`ScopeTreeWidget.cs:448`），字段 `FieldWidth = 36`；16px 宽的按钮低于桌面最小点击目标。
4. **Tuning layer / Import 等按钮高度偏低**：`ButtonHeight = 20`（`ScopeTreeWidget.cs:41`）、预设 Import 高度 `20`（`PresetListWidget.cs:31`）。建议至少 24px，最好 28–32px。
5. **作者筛选仍是“点击循环”**：`FilterBarWidget.DrawAuthorButton` 用 `NextAuthor` 循环切换作者（`FilterBarWidget.cs:129-146`），不是维护者要求的下拉。
6. **衰减图拖点缺少可拖拽提示**：节点仅 5px 方块、命中半径 6px（`LineChartWidget.cs:28-29`），无 hover 包边/放大/光标变化；正是维护者反馈“可拖节点需要包边/悬停高亮”。
7. **Basic toggles 两行文字放在 28px 行高内被裁剪**：`BasicTuningWidget.cs:217-228` 在 y+20 再画第二行，底部超出 28px；这是 G1 的直接证据。
8. **VoicePackRow 固定 74px 放三行文本**：`VoicePackRow.cs:39-45` 使用硬编码 20px/行，未按 `ITextMetrics.CalcHeight` 动态计算；本地化/长作者名时仍有裁剪风险。
9. **筛选段按钮语义不明确**：Enabled/Conflict/Orphan 是可独立开关的 toggle，而 “All” 是清除；玩家可能误以为这是单选的 segmented control。
10. **数值输入无可见校验错误**：非法输入在失焦时静默回退（`UiInteract.NumberField`），没有错误提示；可接受，但不算最佳实践。

### 3.5 可访问性

**现状**

- 深色底 + 金色强调在 RimWorld 生态中视觉协调；选中行有左侧金条、选中卡有底部金条，提供非纯颜色辅助。
- 搜索框有 focus 金线；页面有全局 fallback 和 EmptyState。
- 鼠标为主要输入，`UiInteract` 只处理鼠标点击，无键盘导航。

**问题**

1. **键盘路径缺失**：左导航、下拉、段按钮、图表拖点均无键盘操作；对键盘/辅助输入用户不可达。
2. **焦点可见态不完整**：下拉、步进按钮、图表拖点没有 focus 态；只有原生 TextField/Slider 部分依赖引擎默认。
3. **点击目标偏小**：20px 按钮、16px 步进按钮、18px 复选框视觉尺寸均接近或低于 24px 建议。
4. **Tiny 字号使用广泛**：帮助、次级标签、状态、预设描述大量 Tiny；在 1080p 下可读，但长时间使用容易疲劳；应尽量把主操作标签提升到 Small。
5. **空/错误状态偏“静态文字”**：`EmptyState` 只有文字；无安装指引动作；冲突/孤儿 banner 有颜色但没有图标；依赖颜色表达状态，需补形状/文本前缀。
6. **本地化/长文本风险**：英文 UI 文案目前硬编码在控件/帮助目录；中文或其他语言环境下行高与宽度都会变化，进一步放大 G1。

### 3.6 视觉一致性

**现状**

- 整体深色 + 金色主题与 US 品牌一致；`Palette` 已是中性 token，US 侧通过 `UsVisualTokens` 转发（`UsVisualTokens.cs`）。
- 已有部分统一：行选中左金条、模式卡底部金条、段按钮金边框、搜索 focus 金线。

**问题**

1. **选中态语言不统一**：
   - 行/导航：左金条；
   - 模式卡：底部金条；
   - 段按钮：金色边框 + 金色文字；
   - 复选框：金色填充 + 金勾。
   四种“选中”表达并存，用户需要重新学习。
2. **自绘控件与原版/ SR 风格脱节**：维护者已反馈“切换/选取按钮很丑，甚至不如原版”，`UsSurface.DrawSegment`/`ScopeTreeWidget.DrawSegment`/`ModeCardRenderer` 都是自绘；SR 的底部发光条/左侧高亮条只在部分控件保留。
3. **fallback 双轨视觉**：`VanillaVoicePacksPage` 使用原版 `Widgets.ButtonText/Checkbox/Label`，与主路径自绘风格不同；一旦某控件 fallback，同一页面会出现两套视觉语言。
4. **卡片/区块缺少层级容器**：所有 widget 都是同级卡片，页面缺乏“功能块 > 小节 > 控件”的视觉层级；没有用容器标题/边框把 Basic/Tuning/Packs 的内容组织起来。

---

## 4. 改进建议

优先级定义：

- **P0**：直接破坏可用性、核心功能不可达或严重违背可访问性。
- **P1**：显著改善可发现性/一致性/效率，属于本次任务书主要批次。
- **P2**：体验打磨、局部优化，可放在后续迭代。

| ID | 优先级 | 建议 | 关联问题 |
|---|---|---|---|
| A1 | P0 | **修复 800×600 下 Mood 控制不可用**：`ScopeTreeWidget` 在窄宽度下不要整行失效；改为纵向堆叠/每因子一行紧凑控件，或允许横向滚动。至少保留滑条 + 数值输入，步进可降级。 | §3.2-1、§3.4-3 |
| A2 | P0 | **帮助系统升级为“分区总览 + 当前项帮助 + 悬停/选中联动”**：右帮助标题显示当前区块名；内容区顶部显示当前功能块总览；鼠标悬停/聚焦到具体行或控件时，右面板切换为该项独立帮助。可先实现“卡片级 + 行级 help key”，再逐步补控件级。 | §3.3、反馈 M3 |
| A3 | P0 | **所有可交互控件必须有可见 hover/focus/选中态**：下拉触发框、步进 ±、图表拖点、预设 Import 等补齐；图表节点加包边/悬停放大；拖点可显示“⇔”或光标提示。 | §3.4、反馈 G2/G4 |
| A4 | P0 | **统一最小点击目标**：按钮/下拉/步进高度至少 24px，推荐 28–32px；Mood 步进宽度至少 24px；复选框点击热区扩到 22–24px。 | §3.4、§3.5 |
| B1 | P1 | **用容器重建 Layout.xml**：把 `Block/Section/Column` 用于功能块和小节；内容区顶部渲染“基础设置/调音/包清单”功能块标题；同步让 `GetEngine`/`GetEngineAll` 递归处理容器内 `Tab/Column` 过滤。 | §3.1-1、§3.1-5、R11 |
| B2 | P1 | **重构多列布局**：不要用固定 0.38/0.62 比例；改为按左右列 Measure 高度平衡列宽，或至少把 Basic 右侧的衰减编辑器与左侧卡片重新分组，避免单卡宽列留白。 | §3.2-3 |
| B3 | P1 | **左导航支持“分组即页签”**：点击组标题直接切换功能块；小节锚点保留；增加滚动联动高亮（根据当前滚动位置更新 `ActiveSectionKey`）。 | §3.1-2、§3.1-4 |
| B4 | P1 | **作者筛选改为下拉**；筛选段按钮明确“单选/多选”语义，必要时改为互斥单选（All / Enabled / Conflicts / Orphans）。 | §3.4-5、§3.4-9 |
| B5 | P1 | **Action Scope 分组与过滤**：按“自主行为 / 可操作行为”分组；隐藏 Biotech 防御性动作（哭泣、咯咯笑）；提供搜索或快速定位。 | §3.1-3、反馈 M2 |
| B6 | P1 | **G1 文本高度系统修复**：所有多行控件用 `ITextMetrics.CalcHeight` 或统一行高基线；重点修复 `BasicTuningWidget` 两行行、`VoicePackRow` 三行行、帮助面板长文本。 | §3.4-7/8 |
| B7 | P1 | **Tuning Layer sticky**：把 Global/Race/Xenotype 层选择在 Tuning 滚动区固定置顶，滚动时保持可见。 | 反馈 M1 |
| C1 | P2 | **下拉控件补齐键盘与 hover**：方向键选择、Enter 确认、Esc 关闭、悬停高亮、滚动内 popup 正下方定位（G3）。 | §3.4-1 |
| C2 | P2 | **空/错误状态增加动作**：无域时空态提供“如何安装 VoicePack”说明或链接；冲突/孤儿 banner 增加图标与“修复”动作。 | §3.5-5 |
| C3 | P2 | **视觉选中态统一**：在 UiKit 层定一套“原版语言包装”的 selectable 控件，选中态统一为 SR 底部/左侧金条；减少多种选中表达并存。 | §3.6 |
| C4 | P2 | **键盘导航基础支持**：至少为左导航、下拉、段按钮提供 Tab/方向键/Enter 路径；UI 测试覆盖 focus 态。 | §3.5-1/2 |
| C5 | P2 | **帮助文案本地化准备**：把 help key 与文本分离，预留翻译接口；避免未来中文/长文本再次破坏行高。 | §3.5-6 |
| C6 | P2 | **页签滚动位置策略**：当前切页签已归零；可进一步为每个页签独立记忆滚动位置，减少用户重复滚动。 | §3.1 现状 |

---

## 5. 与现有反馈/任务书的衔接

本报告不是孤立评估，而是对以下文档的衔接与补充：

### 5.1 与 `docs/ui-feedback-consolidated-zh.md` 的对应

| 反馈/风险 | 本报告落点 |
|---|---|
| G1 文本裁剪 | §3.4-7/8，建议 B6 |
| G2 控件通路不可用 | §3.4-1/2/6，建议 A3 |
| G3 下拉定位 | §3.4-1，建议 C1 |
| G4 视觉统一 | §3.6，建议 C3 |
| M1 Tuning Layer sticky | 建议 B7 |
| M2 Action Scope 分组/过滤 | 建议 B5 |
| M3 帮助分层 | §3.3，建议 A2 |

### 5.2 与 `docs/ui-fix-task-books-zh.md` 的对应

- T0（本文档）为后续 T1–T5 提供 UI/UX 依据。
- T2 修复批次 A（G2/G3）：本报告 A3、C1 补充了“哪些控件缺 hover/focus/可拖拽提示”。
- T3 修复批次 B（G1）：本报告 B6 给出具体裁剪证据与修复范围。
- T4 修复批次 C（G4）：本报告 C3 给出“选中态语言不统一”的清单。
- T5 修复批次 D（M1/M2/低风险）：本报告 B5、B7、B4 与任务书一致。

### 5.3 与 `docs/us-ui-review-requirements-zh.md` 的对应

- R4/R5/R6（功能块独立滚动、标题锚点、过滤器归位）：当前已基本满足；建议 B1/B3 进一步强化“功能块标题”和容器结构。
- R17/R18（窄屏降级、800×600 保留右帮助）：当前已满足右帮助保留；本报告新增 **Mood 控制 800×600 不可用**这一必须解决的问题。
- R7/R8/R9（下拉、Mood 复合控件）：大部分已实现；本报告指出下拉/步进的 affordance 仍不足。
- R10/R15（衰减图重做）：已做成 `chart/line`；本报告补充拖点可发现性（A3）。
- R11/R14（容器化 + 复合控件）：`LayoutManifest` 已支持容器，但页面过滤未递归；建议 B1。

---

## 6. 结论

US 设置页的骨架选择是正确的，和 Camera+ 及桌面 HIG 方向一致。当前最值得投入的不是推翻三栏，而是**把“看起来有”变成“真正可用且可发现”**：

1. 先保住 800×600 下所有核心功能可用（Mood 控制）。
2. 再让帮助系统分层、跟随上下文。
3. 再统一控件状态与点击目标。
4. 然后用容器重构内容结构，解决导航/标题/多列布局。
5. 最后做键盘、本地化、空状态等可访问性打磨。

这样能与现有 T1–T5 修复批次自然衔接，避免重复劳动。
