# UI 视觉现代化评估稿（S4-Polish P0）

> 状态：评估稿（已纳入 S4-Vol 与 S4-Nav 需求），等待维护者批准。不修改任何代码。
> 依据：`docs/s4-polish-plan-zh.md`（权威计划；§1.3 S4-Vol、§1.4 S4-Nav、§4 S4-Vol/S4-Nav、§3.7）、`docs/workdocs/s4-polish-p0-visual-spec.md`、`docs/workdocs/s4-polish-s4-vol.md`、`docs/workdocs/s4-polish-s4-nav.md`、`Source/UniversalSqueaker/UI/Layout.xml`、`Source/UniversalSqueaker/UI/Components/*.cs`、`Source/UniversalSqueaker/UI/Widgets/*.cs`、`Source/FerriteLib.UiKit/Widgets/*.cs`。
> 范围：以纯视觉/UI-UX 评估与规格为主，并纳入 S4-Vol 的全局音量 slider 与相机高度衰减编辑器组件规格，以及 S4-Nav 的左侧导航 + 三页签 + 右侧 sticky footer 窗口外壳布局（功能实现分别由 S4-Vol / S4-Nav 任务书负责）。不涉及运行时路由、Kernel/Pure 音频语义、发布操作。

---

## 1. 现状盘点

### 1.1 US 侧绘制方式

当前 `FerriteVoicePacksPage` 是唯一渲染路径：RimWorld 1.6 mod UI 使用 Unity IMGUI + `Verse.Widgets`，每帧全量重画。FerriteLib.UiKit 提供两层 XML 布局、`Measure`/`Draw` 两阶段和 stateless widget 约定；业务命令通过 `UsWidgetCommandAdapter` 转成 Ferrite `UiCommand`。

US 侧实际绘制手段：

| 机制 | 位置 | 现状 |
|---|---|---|
| 表面填充 | `Components/SectionFrame.cs` | `Widgets.DrawBoxSolid(rect, fill)` + 1px 四边 `DrawBorder` |
| 边框 | `SectionFrame.DrawBorder` | 1px 四条边；默认 `UiPalette.Border`，可传自定义色 |
| 调色板 | `Components/UiPalette.cs` | `Ink/Panel/Raised/Emphasized/Warning/Success/Border/Gold/Muted/Disabled/Selected/Danger`，均为 `UnityEngine.Color` |
| 标题/小节头 | `Widgets/UsWidgetDrawing.cs` | `DrawTitle` 用 `GameFont.Medium` + 白色；`DrawSectionHeader` 用 `GameFont.Small` + `(0.95,0.92,0.84)` |
| 行 | `Components/RaceLayerRow.cs`、`XenotypeLayerRow.cs`、`VoicePackRow.cs` | 每行独立 `DrawBoxSolid + DrawBorder`；hover/selected 各自硬编码判断 |
| 开关 | `Widgets/BasicTuningWidget.cs`、`CameraIndicatorWidget.cs`、`Components/VoicePackRow.cs` | 有的用原版 `Widgets.Checkbox`，有的用自绘 34×18 小开关（knob） |
| 输入框 | `Components/SearchField.cs` | 自绘外壳包 `Widgets.TextField`，focus 时底部 2px 金色线 |
| 帮助 | `Components/HelpToggle.cs` + `Widgets/PageTitleWidget.cs` | 仅页级 `?` + 页级 HelpText；`HelpToggle` 自绘 22px 左右方钮 |
| Banner/Empty | `Components/StatusBanner.cs`、`EmptyState.cs` | `SectionFrame` 表面 + `Widgets.Label` |
| 段按钮 | `Widgets/ScopeTreeWidget.cs` 私有 `DrawSegment` | 自绘；off 态金色边框 + `Selected` 底；普通态 `Raised` 底 |
| mood 行 | `Widgets/ScopeTreeWidget.cs` | `Raised` 底 + 金色边；三因子簇（−/值/＋）与 Auto 按钮 |
| 预设树 | `Widgets/PresetListWidget.cs` | 各行自绘；`Widgets.Checkbox` + `Widgets.ButtonText` 混用 |
| 模式卡 | `FerriteLib.UiKit/Widgets/ModeCardRenderer.cs` + `InputModeRowWidget.cs` | FerriteLib 侧绘制；选中底部 3px 金条 |

### 1.2 FerriteLib 侧绘制方式

| 机制 | 位置 | 现状 |
|---|---|---|
| 调色板 | `Widgets/Palette.cs` | 与 US `UiPalette` 同值，`internal`，额外有 `Hover/TextLight/TextSelected/TextSecondary` |
| 表面 | `Widgets/SurfaceFrame.cs` | 与 US `SectionFrame` 同构：fill + 1px border |
| 模式卡 | `Widgets/ModeCardRenderer.cs` | `Panel/Hover/Selected` + 底部 3px 金条 + 标题/描述两行文本 |
| 模式行 | `Widgets/InputModeRowWidget.cs` | 固定单行均分卡片，无 2×2/1 列自动换行 |
| Chrome | `ChromeBannerWidget.cs`、`ChromeFooterWidget.cs`、`SectionHeaderWidget.cs`、`EmptyStateWidget.cs` | 均为 `SurfaceFrame` + `UiKitGui.Label`，footer 仅一段静态文本 |
| 字体/文本 | `UiKitFonts.cs`、`UiKitGui.cs` | 统一封装 `Text.Font/Anchor/GUI.color` 保存恢复 |

### 1.3 当前组件清单

- **US Components**：`SectionFrame`、`StatusBanner`、`HelpToggle`、`SearchField`、`EmptyState`、`RaceLayerRow`、`XenotypeLayerRow`、`VoicePackRow`、`VoicePackChecklist`。
- **US Widgets**：`PageTitleWidget`、`BasicTuningWidget`、`CameraIndicatorWidget`、`RaceLayerWidget`、`XenotypeLayerWidget`、`VoicePackChecklistWidget`、`ScopeTreeWidget`、`PresetListWidget`。
- **FerriteLib Widgets**：`ChromeBannerWidget`、`ChromeFooterWidget`、`SectionHeaderWidget`、`EmptyStateWidget`、`InputModeCardWidget`、`InputModeRowWidget`。
- **共享底层**：`SurfaceFrame`、`ModeCardRenderer`、`UiKitGui`、`UiKitFonts`、`CoreWidgetRegistrar`。
- **计划新增**（S4-Vol / S4-Nav / P2）：`GlobalVolumeWidget`、`AttenuationEditorWidget`、`UsNavWidget`、`UsFooterWidget`。

### 1.4 问题清单（只列现状，不含修复方案）

1. **调色板双份**：`UiPalette` 与 FerriteLib `Palette` 值重复；US 侧还有多处 `new Color(...)` 硬编码 hover/文本色。
2. **hover 色散落**：`BasicTuningWidget`、`CameraIndicatorWidget`、`ScopeTreeWidget`、`PresetListWidget` 中重复出现 `new Color(.16f,.145f,.12f,.94f)`；mood/clear/段按钮另有多个局部色值。
3. **选中/警示态不统一**：模式卡用底部 3px 金条，race/xeno 行用左侧 4px 竖条，scope 段按钮用金边框，mood 行用 `Raised` 底 + 金边，checklist 用自绘小开关，`VoicePackRow` 无统一 selected 语义。
4. **版式节奏弱 / 缺少导航外壳**：单页滚动，除 section header 外几乎全为同质行；没有左侧导航与三页签分区，footer 不随右侧可视底部 sticky；没有分区表面、分隔线、焦点态分层。
5. **字体/行高魔法数**：Tiny/Small/Medium 混用，行高与间距散落在各 widget 常量（3f/4f/5f/7f/20f/25f 等），无统一行高令牌。
6. **过滤缺失**：仅 checklist 内有文本搜索；无作者/冲突/orphan/已启用过滤。
7. **帮助缺失**：只有一个页级 `?` 与页级 HelpText；widget 无就地帮助。
8. **窄屏不完整**：`ScopeTreeWidget` mood 簇有 86px 最小守卫；其余多处 `rect.width - ...` 未 clamp 或未定义降级行为；无三档宽度规则。
9. **footer 信息不足**：只有静态提示文本，无版本号、保存状态、dirty 标记。
10. **距离无预览**：距离行只显示预设名；`VoicePacksViewState` 未投影 `distanceRange`。
11. **测试缺口**：布局/过滤/窄屏/测量逻辑无单测；`VoicePacksPageModel.BuildView` 存在写回 state 的已知副作用（review-05 M3，记录为后续，不在 S4 范围）。

---

## 2. 现代简洁设计语言（全量换肤规格）

### 2.1 设计原则

- **扁平**：无渐变、无圆角、无贴图、无投影。
- **1px 边框**：所有表面/控件统一 1px 四边边框；强调线也是实心矩形。
- **间距刻度**：2 / 4 / 6 / 8 / 10（px）。页面 padding=10，section 间 gap=10，控件间 gap=6，行内 gap=4/2。
- **行高刻度**：S=22、M=26、L=32、XL=50（px）。多行内容的高度 = 行高之和 + 间距，不再使用魔法数。
- **选中态统一**：
  - 行（含 list row、toggle row）：左侧 4px `AccentGold` 竖条。
  - 卡片（mode card、banner 类可选中卡）：底部 3px `AccentGold` 条。
  - hover 只提亮表面，不改变边框/强调。
  - danger 语义：`Danger` 底 + 1px `Danger` 边框。
  - focus：1px `BorderStrong` 边框（text field、help button 等可聚焦控件）。
- **文字层级**：主标题 Medium + `TextPrimary`；section header Small + `TextPrimary`；行主标签 Small + `TextPrimary`；次标签 Tiny + `TextSecondary`；辅助状态 Tiny + `TextSecondary`。

### 2.2 色板令牌（RGBA，P1 可直接实现）

| 令牌 | R | G | B | A | 用途 |
|---|---|---|---|---|---|
| `Base` | 0.10 | 0.10 | 0.10 | 1.00 | 页面最底/窗口底色 |
| `Panel` | 0.14 | 0.14 | 0.14 | 1.00 | 普通行/卡片表面 |
| `Raised` | 0.18 | 0.18 | 0.18 | 1.00 | 可交互默认表面/次级卡 |
| `Hover` | 0.22 | 0.22 | 0.22 | 1.00 | hover 表面 |
| `Selected` | 0.20 | 0.17 | 0.10 | 1.00 | 选中表面（暖金黑） |
| `AccentGold` | 0.92 | 0.68 | 0.30 | 1.00 | 主强调、选中条、勾、曲线 |
| `TextPrimary` | 0.92 | 0.92 | 0.90 | 1.00 | 主文本 |
| `TextSecondary` | 0.65 | 0.65 | 0.62 | 1.00 | 次文本/占位/说明 |
| `Danger` | 0.55 | 0.18 | 0.15 | 1.00 | 错误/危险表面与边框 |
| `Success` | 0.16 | 0.35 | 0.22 | 1.00 | 成功表面 |
| `Border` | 0.30 | 0.30 | 0.28 | 1.00 | 普通 1px 边框 |
| `BorderStrong` | 0.45 | 0.45 | 0.42 | 1.00 | focus/强调边框 |

派生文本色（有固定值，非待定）：

| 令牌 | R | G | B | A | 用途 |
|---|---|---|---|---|---|
| `TextOnGold` / `TextSelected` | 1.00 | 0.86 | 0.58 | 1.00 | 选中态主文字 |
| `TextOnDanger` | 1.00 | 0.67 | 0.48 | 1.00 | danger/warning banner 文字 |
| `TextDisabled` | 0.45 | 0.45 | 0.42 | 1.00 | disabled 文字 |
| `AccentGoldAlpha20` | 0.92 | 0.68 | 0.30 | 0.20 | 衰减编辑器填充（纯色 alpha，非渐变） |

> 实现说明：所有令牌以 `UnityEngine.Color(r, g, b, a)` 常量存在；US 侧 `UsVisualTokens` 与 FerriteLib `Palette` 各自持有相同值，保持中性边界。

---

## 3. 自绘组件库规格

> 每个组件给出 normal / hover / selected（或 checked / active / focus）三态与最小尺寸。所有状态均为扁平 1px 边框；文本对齐除说明外默认 MiddleLeft。

### 3.1 现代行（Modern Row）

用于 race/xeno 行、VoicePack 行、预设行、调音行、toggle 行。

| 状态 | 表面 | 边框 | 文本 |
|---|---|---|---|
| normal | `Panel` | `Border` | 主 `TextPrimary`，次 `TextSecondary` |
| hover | `Hover` | `Border` | 主 `TextPrimary`，次 `TextSecondary` |
| selected | `Selected` | `BorderStrong` | 主 `TextOnGold`，次 `TextSecondary`；左侧 4px `AccentGold` 竖条 `(x+1, y+1, 4, h-2)` |
| danger | `Danger` | `Danger` | `TextOnDanger` |
| disabled | `Base` | `Border` | `TextDisabled` |

- 最小尺寸：S=22 / M=26 / L=32 / XL=50；行内左 padding=10，右侧操作区宽度固定（默认 76–132，见具体组件）。
- 整行点击热区为整行；右侧按钮/开关优先于行点击。
- 多行行高：两行 = 26 + 22 + 2，三行 = 26 + 22 + 22 + 4，不使用 74 之类的魔法常量（现有 VoicePackRow 74px 需按此换算）。

### 3.2 段按钮（Segmented Button）

用于 layer 段（Global/Race/Xenotype）、scope 环、过滤段、mood −/＋。

| 状态 | 表面 | 边框 | 文本 |
|---|---|---|---|
| normal | `Raised` | `Border` | `TextSecondary` |
| hover | `Hover` | `BorderStrong` | `TextPrimary` |
| selected / active | `Selected` | `AccentGold` | `AccentGold`（或 `TextOnGold`） |
| danger（Off/清除类） | `Danger` | `Danger` | `TextOnDanger` |
| disabled | `Base` | `Border` | `TextDisabled` |

- 最小尺寸：宽 56px，高 22px（S）。
- 同组按钮间距 2px；选中项不显示额外竖条/底条，用金色边框 + 金色文本表达。

### 3.3 现代复选框（Modern Checkbox）

用于 preset 树、toggle 行、开关类。

| 状态 | 表面 | 边框 | 勾 |
|---|---|---|---|
| unchecked normal | `Panel` | `Border` | 无 |
| unchecked hover | `Hover` | `BorderStrong` | 无 |
| checked | `Selected` | `AccentGold` | 金色勾 |
| disabled | `Base` | `Border` | `TextDisabled` 勾 |

- 视觉尺寸：18×18；点击热区建议 22×22（视觉外扩 2px 不绘制）。
- 金勾：2px 粗的两段实心矩形，近似 `✓`；不依赖字体符号。
- 最小尺寸：18×18。

### 3.4 模式卡（Mode Card）

用于 Vanilla / Fallback / Remix / Disabled 四模式。

| 状态 | 表面 | 边框 | 标题 | 描述 |
|---|---|---|---|---|
| normal | `Panel` | `Border` | `TextPrimary` | `TextSecondary` |
| hover | `Hover` | `Border` | `TextPrimary` | `TextSecondary` |
| selected | `Selected` | `BorderStrong` | `TextOnGold` | `TextSecondary`；底部 3px `AccentGold` 条 `(x+1, yMax-4, w-2, 3)` |
| danger（Disabled 卡若需警示） | `Danger` | `Danger` | `TextOnDanger` | `TextOnDanger` |

- 最小尺寸：宽 140px，高 64px；内 padding 10px，标题行高 24px，描述 Tiny。
- 卡片间距 6px。

### 3.5 文本输入皮肤（Text Field）

外壳包 `Widgets.TextField`；仅换皮肤，不改变输入逻辑。

| 状态 | 表面 | 边框 | 提示/文本 |
|---|---|---|---|
| normal | `Panel` | `Border` | 文本 `TextPrimary`，placeholder `TextSecondary` |
| hover | `Hover` | `Border` | 同上 |
| focus | `Panel` | `BorderStrong` | 同上；可选底部 2px `AccentGold` 聚焦线（与 `BorderStrong` 并存） |
| disabled | `Base` | `Border` | `TextDisabled` |

- 最小尺寸：高 26px（M），宽 120px；内 padding 7px。
- placeholder 仅在值为空时绘制。

### 3.6 Banner

用于页级/区级状态、dormant/target unavailable/canonical conflict/orphan 提示。

| 状态 | 表面 | 边框 | 文本 |
|---|---|---|---|
| base | `Panel` | `Border` | `TextSecondary` |
| warning/danger | `Danger` | `Danger` | `TextOnDanger` |
| success | `Success` | `BorderStrong` | `TextPrimary` 或 `TextOnGold` |
| info | `Raised` | `Border` | `TextSecondary` |

- 最小尺寸：高 34px，宽自适应；内 padding 8×4。
- 非交互：不绘制 `ButtonInvisible`，天然不拦截点击。

### 3.7 窗口外壳 + 左侧导航 + 右侧 sticky footer（S4-Nav）

S4-Nav 的窗口外壳不是换肤项，但它是所有现代组件的承载结构；P0 规格一并纳入，作为 P1/P6 的布局约束。

**窗口外壳约束**：

- 保留 RimWorld `Window` 标题栏、关闭按钮与内容视口；自定义内容只绘制在窗口内容视口内，不溢出、不遮挡、不超出可视区。
- 内容区采用「左侧导航 + 右侧内容 + 右侧底部 sticky footer」三段式：
  - 左侧导航：固定宽度建议 160px（响应式降级见 §4），垂直排列三枚页签按钮。
  - 右侧内容区：独立滚动区；只渲染当前 `ActiveTab` 对应的页签内容；滚动不带动 footer。
  - 右侧底部 footer：钉在右侧可视区底部，宽度与右侧内容区一致，顶部 1px `Border` 分隔线；不随内容滚走。
- 三页签固定：`基础设置` / `调音` / `包清单`；页签状态为 ephemeral（`VoicePacksPageState.ActiveTab`），Reset 回基础设置。

**页签按钮规格**（`us/nav` 或 `FerriteVoicePacksPage` 直接绘制）：

| 状态 | 表面 | 边框 | 文本 |
|---|---|---|---|
| normal | `Base` | `Border` | `TextSecondary` |
| hover | `Hover` | `BorderStrong` | `TextPrimary` |
| active | `Selected` | `AccentGold` | `TextOnGold`；左侧 4px `AccentGold` 竖条 |

- 最小尺寸：高 32px（L），宽 140px（在 160px 左栏内左右 padding 10px）；三枚按钮纵向间距 4px。
- 交互：点击 emit `SetActiveTab`（`Basic` / `Tuning` / `Packs`）。

**右侧 sticky footer（双槽内容见 §3.8）**：

- footer 属于右侧内容区布局，不进入滚动容器；始终位于右侧可视区底部。
- 若右侧可视高度不足，内容滚动区收缩，footer 高度保持 26px 不被压缩。

### 3.8 Footer 双槽（Footer Dual Slot，sticky）

位于右侧底部 sticky 容器（外壳见 §3.7）；左槽版本/build 身份，右槽保存状态。

| 槽 | 内容 | 颜色 |
|---|---|---|
| 左 | `dev-<rev>/<informational>` 或版本号 | `TextSecondary` |
| 右 | Idle/Saving/Saved/Failed；dirty 加 `●` | Idle/Saved=`TextSecondary`，Saving/Dirty=`AccentGold`，Failed=`Danger` |

- 最小尺寸：高 26px（M），顶部 1px `Border` 分隔线，左/右 padding 10px。
- 右槽仅在空间不足时截断文本，不换行。

### 3.9 全局音量 Slider + 相机高度衰减编辑器

**全局音量 Slider**（`us/global-volume`）：

| 元素 | 规格 |
|---|---|
| 范围 | 0%–100%，默认 100%；不提供 200%（YAGNI） |
| 视觉 | 扁平轨道 + 金色游标；normal/hover/focus 三态 |
| 最小尺寸 | 高 26px（M），宽 120px |
| 文本 | 当前百分比 `TextPrimary` Tiny |
| 交互 | 拖动/点击 emit `SetGlobalVolume` |
| 语义 | `0%` 不是 `Disabled` 短路，只是最终音量 0 |

**相机高度衰减编辑器**（`us/attenuation-editor`）：

| 元素 | 规格 |
|---|---|
| 横轴 | 相机高度，固定 15–65 |
| 纵轴 | 音量百分比 0–100 |
| 高度 | 64px |
| 最小宽度 | 200px；<200 时降级为文本摘要 |
| 网格/坐标线 | 1px `Border` |
| 开始点 | y 锁死 100%，x 可水平拖拽 |
| 结束点 | y 锁死 0%，x 可水平拖拽 |
| 曲线 | 两点连线线性衰减；`AccentGold` 2px 实心条序列 + `AccentGoldAlpha20` 填充 |
| 快速预设 | 三个按钮 `Conservative / Balanced / Strong`（15–65 / 15–50 / 15–40） |
| 拖拽后 | `distancePreset = Custom`，写 `SetDistanceRange` |
| 最终音量 | 全局音量 × 衰减系数 |

- 语义为玩家可调的线性衰减模型，不等于引擎物理 rolloff；UI 文案注明。

### 3.10 Help `?`

用于 widget 头行就地帮助入口。

| 状态 | 表面 | 边框 | 文本 |
|---|---|---|---|
| normal | `Panel` | `Border` | `TextSecondary` `?` |
| hover | `Hover` | `BorderStrong` | `TextPrimary` `?` |
| active（帮助展开） | `Selected` | `AccentGold` | `AccentGold` `?` |

- 最小尺寸：22×22；点击热区同视觉尺寸。

### 3.11 小节头 / 空态（复用现有语义）

- **SectionHeader**：无表面，文本 `Small` + `TextPrimary`，最小高 22px（S），底部 1px `Border` 分隔线。
- **EmptyState**：`Panel` 表面 + `Border`，文本 `Small` + `TextSecondary`，最小高 44px，文本居中。

---

## 4. 响应式三档规则

### 4.1 宽度档位

| 档位 | 页面宽度 | 行为 |
|---|---|---|
| Comfortable | ≥ 480px | 完整布局；模式卡按可用宽度自动 1 行 4 卡（每卡 ≥140px）或 2×2 |
| Compact | 320–479px | 行内次文本截断/省略；固定右操作；mood 簇保持 86px 守卫；层段按钮最小 56px |
| Minimal | < 320px | 只画主标签 + 单操作；次文本/预览图/详情行让位；模式卡 1 列 |
| Fallback | < 240px | 页面级早退，画 `EmptyState`「Window too narrow」，不计算 scrollbar |

边界判定：`≥480` Comfortable；`320–479` Compact；`<320` Minimal；`<240` Fallback。边界值本身属于布局纯函数 `VoicePacksLayout.ForWidth(float)`，P1/P6 锁定。

### 4.2 逐组件降级行为

| 组件 | Comfortable | Compact | Minimal |
|---|---|---|---|
| 现代行 | 主标签 + 次标签 + 右操作 | 次标签截断/省略；右操作宽度固定 | 隐藏次标签；仅主标签 + 右操作 |
| 段按钮组 | 全部按钮完整 | 按钮最小 56px，文本可省略号 | 只保留当前选中段按钮 + 一个循环操作 |
| 复选框 | 18×18 + 标签 | 同左 | 同左 |
| 模式卡 | 4 卡 1 行（每卡 ≥140） | 2×2 网格 | 1 列 |
| 文本输入 | 完整宽 | 宽度随容器收缩，placeholder 可省略 | 宽度随容器收缩，placeholder 省略 |
| Banner | 完整文本 | 文本自动换行 | 只显示第一段/关键短语 |
| 左侧导航/页签 | 160px 左栏，三枚全宽标签 | 左栏缩至 56px，显示短标签/图标 | 改为顶部横排页签条（或极窄 40px 左栏仅图标） |
| Footer | 左版本 / 右状态同槽（右侧底部 sticky） | 右状态截断 | 右状态显示为单字符状态点（如 `●`） |
| 全局音量 Slider | 完整滑条 + 百分比 | 滑条收缩，百分比保留 | 只显示百分比文本 + 单步按钮 |
| 衰减编辑器 | ≥200px 可拖拽两点图 | ≥200px 可拖拽两点图；<200px 文本摘要 | 一律文本摘要 + 三快速预设按钮 |
| Help `?` | 头行右端 | 头行右端 | 头行右端 |
| ScopeTree mood 行 | 三因子簇完整 | 三因子簇 86px 守卫，不重叠 | 显示「Window too narrow for mood controls」文本 |
| Race/Xeno 行 | 主标签 + enabled 统计 + 状态后缀 | 主标签 + 状态点；统计截断 | 主标签 |
| Preset 树 | xeno 缩进 18px | xeno 缩进 12px | 隐藏描述行，Import 宽 76px |
| 空态 | 完整文本 | 完整文本 | 完整文本（自动换行） |

规则：**不隐藏整个区块**（空 catalog 除外），优先降级信息密度，不删除功能入口。

---

## 5. 原版 fallback 矩阵

### 5.1 分级策略

| 级别 | 覆盖 | 触发 | 降级形态 |
|---|---|---|---|
| L1 交互组件级 | 有原版对应物的交互控件 | 该 widget 的 `Measure`/`Draw` 抛异常 | 同一 rect 内改画原版 `Widgets.Checkbox` / `ButtonText` / `TextField`，命令流不变 |
| L2 信息组件级 | 无交互的自绘内容 | 同上 | 文本摘要 / 原版 `Widgets.Label` / 简单 `DrawBoxSolid` |
| L3 页面级 | 整页（引擎、注册表、Layout.xml 解析、未知异常） | `FerriteVoicePacksPage.Draw` 外层 catch | 新 `VanillaVoicePacksPage`：纯 `Verse.Widgets` 简化功能页，复用 `VoicePacksPageModel.BuildView/Execute` |

### 5.2 自绘组件 → 原版对应物

| 自绘组件 | 原版对应物 | 级别 | 说明 |
|---|---|---|---|
| 现代 toggle 行（BasicTuning / CameraIndicator） | `Widgets.Checkbox` + `Widgets.Label` | L1 | 命令 `ToggleEgg` / `SetDistancePreset` / `ToggleBasic` 不变 |
| 现代段按钮（layer / scope / filter） | `Widgets.ButtonText`，选中项前缀 `●` | L1 | 命令不变 |
| 现代复选框 | `Widgets.Checkbox` | L1 | 命令不变 |
| 现代模式卡 | 2×2 或 1 列 `Widgets.ButtonText`，选中项前缀 `●` | L1 | `InputModeRowWidget` 在 FerriteLib 内实现，保持中性 |
| 现代 VoicePack 行/开关 | `Widgets.Checkbox` + 两行 `Widgets.Label` | L1 | `TogglePack` 命令不变 |
| 现代搜索框 | `Widgets.TextField`（去掉皮肤外壳） | L1 | 本就可用的原版路径 |
| 现代 help `?` | `Widgets.ButtonText("?")` | L1 | `ToggleHelp` 命令不变 |
| mood −/＋ 步进 | 两个 `Widgets.ButtonText`（−/＋）+ `Widgets.Label` 值 | L1 | `SetMoodTuning` 命令不变 |
| Import 按钮 | 已用 `Widgets.ButtonText`，无额外风险 | L1 | 无需 fallback |
| 全局音量 Slider | `Widgets.HorizontalSlider` + `Widgets.Label` | L1 | 命令 `SetGlobalVolume` 不变 |
| 衰减编辑器 | `Widgets.Label` 文本摘要（`Conservative 15–65` 等） + 三快速预设按钮 | L2 | 非拖拽，保留预设入口 |
| 左侧导航/三页签 | 三个 `Widgets.ButtonText`，选中项前缀 `●` | L1 | `SetActiveTab` 命令不变 |
| Footer 双槽 | 两个 `Widgets.Label` | L2 | 非交互 |
| Banner / SectionHeader / EmptyState | `Widgets.Label`（banner 加简单 `DrawBoxSolid`） | L2 | 非交互 |
| 整页 | `VanillaVoicePacksPage` | L3 | 见 P7 |

### 5.3 实现要点

- `UsGuard.MeasureOrFallback(customMeasure, fallbackHeight, widgetId)` / `UsGuard.DrawOrFallback(rect, customDraw, vanillaDraw, widgetId)`。
- catch 后**先恢复 GUI 状态**（`Text.Font = GameFont.Small`、`Text.Anchor = UpperLeft`、`GUI.color = Color.white`）再画 fallback。
- 每个 widget 每会话只 log 一次。
- fallback 不吞业务异常：命令执行（`ExecuteAll`）仍在渲染循环外。
- `VanillaVoicePacksPage` 只做简化功能面：模式、全局音量、衰减快速预设、三开关、彩蛋、相机、域选择 + pack 勾选；scope tree / mood 调音 / preset import 可省略。
- FerriteLib 侧中性组件同样按 L1/L2 在库内实现 fallback，不出现产品字面量。

---

## 6. 维护者决策摘要

| # | 决策项 | 状态 / 推荐 | 本文档落点 |
|---|---|---|---|
| D1 | 视觉路线 | **已拍板：全量换肤（简洁现代 RimWorld 配色组件）** | 全文按全量换肤编写；§2 色板/间距/行高、§3 组件库、§4 响应式、§5 fallback 均直接供 P1/P7 引用 |
| D2 | FerriteLib 中性边界 | **FerriteLib 只做中性现代 skin + 中性能力**（帮助键集合、响应式），不引入 US/SR 产品字面量 | §2.2 说明两侧各自持有相同令牌；§3 组件规格对库内组件保持中性；§4 的 2×2/1 列模式卡为库中性能力 |
| D3 | 帮助展开状态归属 | **`UiPageState.OpenHelpKeys`（中性通用容器）**，不放 US 业务 state | §3.10 Help `?` 与 §5.2 映射；US 持有 `HelpKey` 键名，展开状态由库通用容器管理 |
| D4 | 衰减编辑器曲线语义 | **可拖拽两点衰减**：开始点 y=100%、结束点 y=0%，两点连线线性衰减；最终音量 = 全局音量 × 衰减系数 | §3.9 明确定义编辑器规格与标注要求；不读 `SubSoundDef.distRange`、不碰 Unity 音频 |
| D5 | `VoicePacksPageModel.BuildView` 写回 state | 本计划保持现有写回（避免扩大改动）；过滤/帮助不新增写回 | 本文档不修改该行为；§1.4 问题 11 记录为后续，S4 范围内不做纯化 |
| D6 | 原版 fallback 粒度 | **推荐 L1+L2+L3 三级** | §5 完整矩阵与实现要点，供 P1/P7 落地 |
| D7 | S4-Nav 窗口外壳布局 | **已拍板：B 方案（左侧导航 + 右侧内容 + 右侧底部 sticky footer）** | §3.7 定义外壳/页签/导航规格；§4 纳入响应式；§5.2 纳入页签 fallback |

---

## 7. 验收自检

- [x] 覆盖任务书 6 点：现状盘点（§1）、现代简洁设计语言（§2）、自绘组件库规格（§3）、响应式三档规则（§4）、原版 fallback 矩阵（§5）、维护者决策摘要（§6）。
- [x] 已纳入 S4-Vol：全局音量 slider（0–100%）与相机高度衰减编辑器（可拖拽两点、开始 100%/结束 0%、横轴 15–65）。
- [x] 已纳入 S4-Nav：左侧导航 + 三页签（基础设置/调音/包清单）+ 右侧 sticky footer 的窗口外壳布局。
- [x] 色板值、组件规格均为具体值，无「待定」。
- [x] 全文不含 Workshop ID、个人路径、凭据。
- [x] 未修改任何代码；仅修订本文档；未提交。
