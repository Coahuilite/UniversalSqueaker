# S4-Polish 视觉 / UI-UX 规划与开发计划

> 状态：规划稿（2026-08-28）。**维护者已拍板：视觉路线 = 全量换肤（简洁现代 RimWorld 配色组件）；原版 fallback 策略见 §10（当前仍在开工前评估）。**
> 入口依据：`docs/s4-polish-brief.md`、`HANDOFF.md` §5 第 1 项、`docs/us-ui-migration-plan-zh.md` §10/§12 S4、`docs/review/review-05-ui-ferrite.md`。
> 性质：纯视觉/UI-UX 块。不碰运行时路由、不碰 settings schema、不碰 Kernel/Pure 音频语义（仅新增零 Verse 的可测 UI 纯函数）。
> 术语：本计划所有「换肤/重做」均指视觉皮肤与绘制代码的替换程度；RimWorld 1.6 mod UI 只有 Unity IMGUI + `Verse.Widgets` 一条路，每帧全量重画，不存在新的保留模式 GUI 系统。FerriteLib.UiKit 只是 IMGUI 之上的布局/测量结构，不改变底层绘制方式。

---

## 1. 目标与范围

### 1.1 目标

把当前「功能已完整、视觉仍粗放」的 Ferrite VoicePacks 设置页，**全量换肤**为一套简洁、现代、RimWorld 配色语境的设置 UI：扁平表面、清晰层级、统一状态表达、窄屏可降级、就地帮助、build 身份 footer、距离预览图，并且在任何自绘组件异常时仍有原版功能兜底。

### 1.2 六项范围（与 brief 对齐）

| # | 项目 | 一句话范围 |
|---|---|---|
| S4a | 过滤 | 作者 / 种族 / 冲突 / orphan / 已启用 快速过滤 |
| S4b | 组件化帮助 | widget-attached help content，就地展开，不点穿 |
| S4c | 窄屏响应式 | 全页面宽度守卫 + 布局降级，覆盖每个 Ferrite widget |
| S4d | 视觉现代化 | 全量换肤：现代简洁 RimWorld 配色 + 自绘组件库 + FerriteLib 中性现代 skin |
| S4e | Footer build identity | 左版本 / 右保存状态 + dirty 标记 |
| S4f | 距离预览折线图 | `distancePreset` / `distanceRange` 的可视化预览 |
| S4g | 原版 fallback | 自绘组件异常时降级到原版 `Widgets.*` 对应物，保持设置页可用（§10） |

### 1.3 非目标

- 不修改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`。
- 不引入 Legacy UI；Ferrite 是唯一渲染路径（vanilla fallback 只是 Ferrite 内部的降级绘制，不是第二条产品路径）。
- 不把 US 产品字面量、文案、帮助内容放进 `FerriteLib.UiKit`（库保持中性；库内只允许中性现代 skin 与中性能力）。
- 不改 `voicePackSelections` / `voicePackMode` / `actionTuning` / `moodTuning` 的 Scribe 形状；只读展示。
- 不做运行时音频逻辑改动；距离图只是可视化，不改变 `CompSqueaker.ApplyDistanceRange`。
- 不发布、不 push、不碰 remote。

---

## 2. 现状盘点（已按文件级核实）

### 2.1 视觉现状

- 调色板双份：US 侧 `UI/Components/UiPalette.cs`（Ink/Panel/Raised/Emphasized/Warning/Success/Border/Gold/Muted/Disabled/Selected/Danger）与 FerriteLib 内部 `Widgets/Palette.cs`（同色值，internal）。双份是中性边界的结果；全量换肤后 US 与 FerriteLib 各维护一套一致的中性现代令牌（库侧无产品字面量）。
- 行/卡片绘制分散：`RaceLayerRow`、`XenotypeLayerRow`、`VoicePackRow`、`BasicTuningWidget`、`ScopeTreeWidget`、`PresetListWidget` 各自 `Widgets.DrawBoxSolid + SectionFrame.DrawBorder`，hover 色值多处硬编码（`new Color(.16f,.145f,.12f,.94f)` 等）。
- 选中态/警示态表达不统一：模式卡用底部 3px 金条，race/xeno 行用左侧 4px 竖条，checklist 用自绘小开关，scope 段按钮用金边框，mood 行用 `Raised` 底 + 金边。
- 版式：单页滚动；标题/模式卡/basic tuning/camera/scope tree/preset list/race layer/xenotype layer/checklist/footer 顺序见 `UI/Layout.xml`。无分区视觉节奏，除 section header 外全是同质行。
- 字体：Tiny/Small 混用，行高/基线靠魔法数（3f/4f/5f/7f），没有统一行高常量。

### 2.2 UX 现状

- 过滤：只有 checklist 内的文本搜索；无作者/冲突/orphan/已启用过滤。
- 帮助：只有一个全局 `?`（`PageTitleWidget`）+ 一段页级 HelpText；widget 没有就地帮助。
- 窄屏：`ScopeTreeWidget` mood 因子簇有 86px 最小守卫；其余多处 `rect.width - ...` 未 clamp（如 `BasicTuningWidget` label、`CameraIndicatorWidget` label、`ScopeTreeWidget.DrawLayerRow` 按钮宽、`DrawDomainRow` 文本宽）。
- footer：`chrome/footer` 只有一段静态文案，无版本、无保存状态。
- 距离：`BasicTuningWidget` 距离行只循环预设名，无图形预览；`VoicePacksViewState` 只投影 `DistancePreset`，未投影 `distanceRange`。
- 测试：`VoicePacksLayout` 的窄屏/过滤/测量逻辑无单测；`VoicePacksPageModel.BuildView` 有写回 state 的副作用（review-05 M3）。

---

## 3. 对 brief 第 6 节 8 个问题的回答（规划决策）

1. **依赖顺序**：`视觉评估稿 → 现代皮肤地基（含 fallback guard）→ footer → 距离图 → 过滤 → 组件化帮助 → 窄屏响应式 → 全量换肤收口（含页面级 vanilla 兜底页）`。
   - 现代皮肤地基必须先做（否则每个后续块都要返工）。
   - footer、距离图、过滤、帮助四块之间文件重叠少，但都触碰 `FerriteVoicePacksPage.cs` 的 view state 字典与 `Layout.xml`。**建议串行提交、小步合入**；若要并行，只把「距离图」与「过滤」拆给两个 subagent，footer/help/responsive/fallback 必须串行（都改全部 widget）。
   - 窄屏响应式最后做：它要为每个 widget 的 Measure/Draw 增加宽度守卫，等其它块的最终高度逻辑（尤其帮助展开高度）稳定后再做，避免二次返工。
2. **过滤放哪层**：**投影层 `VoicePacksPageModel.BuildView`**。过滤是视图投影的一部分，widget 保持无状态渲染。过滤状态（ephemeral）放 `VoicePacksPageState`。判定谓词抽为零 Verse 纯函数（`UI/Layout/VoicePacksFilters.cs`，基于 primitive 输入），由新测试项目锁住；`BuildView` 只做薄适配调用。
3. **组件化帮助数据模型**：**独立帮助表 + widget 元数据键**。
   - 帮助内容放 US 层静态目录 `UI/Help/UsHelpCatalog.cs`（`Dictionary<string,string>`，键如 `us/race-layer`）。
   - `Layout.xml` 每个 widget 增加可选 `HelpKey="..."` 属性；widget 用它查目录，缺省无帮助按钮。
   - 展开状态放 `FerriteLib.UiKit.UiPageState` 的通用容器（`HashSet<string> OpenHelpKeys`，中性、无产品字面量）；不放 view state、不放业务 model。
4. **窄屏统一策略**：三档宽度 + 全页面兜底。
   - `Comfortable ≥ 480px`：完整双列/左右布局。
   - `Compact 320–479px`：行内次级文本截断/省略；mood 因子簇保持 86px 守卫；层段按钮最小 56px；行尾按钮宽度固定但左区收缩。
   - `Minimal < 320px`：widget 内部只画主标签 + 单操作，次级文本与预览图让位；`< 240px` 页面级画 `EmptyState`「Window too narrow」。
   - 不隐藏整个区块（空 catalog 除外），优先降级而非删除信息。
5. **视觉现代化评估稿先覆盖**：全页面全部 root widget + 共享绘制助手 + 令牌表 + 自绘组件库（现代行/段按钮/checkbox/模式卡/文本输入皮肤/图表/footer）。与 FerriteLib 中性边界的解法：**FerriteLib 同步换一套中性现代 skin（core chrome：banner/footer/mode-row/section-header/empty-state），不含任何 US/SR 产品字面量**；US 侧组件在其上叠加产品语义（金色强调、danger/success、行范式）。两侧令牌值保持一致，但代码各自持有（中性边界不变）。
6. **Footer build identity 数据**：
   - 版本：`UniversalSqueakerMod.BuildIdentity()`（当前 private static，需提为 `internal static`）。
   - 保存状态：`UniversalSqueakerMod.SaveState`（internal enum，已存在）、dirty = `requestedSaveGeneration > persistedSaveGeneration`（需暴露 `internal bool IsSettingsDirty`）。
   - 现有 settings 无「版本」概念，版本来自程序集；保存状态来自 Mod 的防抖写桥。全部只读，S4e 不改持久化。
7. **距离预览折线图数据输入**：`settings.distanceRange`（`FloatRange`，15–65 clamp 后）+ `settings.distancePreset`。纯函数画曲线：新 `UI/Layout/DistancePreview.cs`（零 Verse，输入 `(min,max)` 输出归一化采样点），绘制用 Verse `Widgets.DrawBoxSolid` 像素条。**不读 `SubSoundDef.distRange`、不碰 Unity 音频回滚**；文档注明这是示意曲线（min 内全响、min→max 线性衰减、max 外静默），不等于引擎实际 rolloff。需要测试：是，零 Verse 纯函数进新 UI 逻辑测试项目。
8. **验收标准**：见 §6 每块验收；总闸 = `verify-local` 全绿 + Dev/Release 0 警告 + kernel golden-corpus 零差 + 新增 UI 逻辑测试全绿 + maintainer 游戏内矩阵（见 §6.3，含 fallback 触发项）。

---

## 4. 拆解顺序与文件范围

> 每块一个原子提交。提交前跑 §6.1 门禁；提交说明用 `feat(S4-*)` / `docs(S4-*)`。

### P0 — 视觉现代化评估稿（纯文档，先行门）

- **产出**：`docs/ui-visual-modernization-zh.md`。
- **内容**：
  1. 现状盘点（§2.1 视觉、§2.2 UX）。
  2. **现代简洁设计语言**（全量换肤规格）：
     - 色板：中性炭黑基面（Base/Panel/Raised/Hover/Selected）+ 单一金色强调 + Danger/Success/Warning 语义色；扁平、1px 边框、无渐变/圆角/贴图。
     - 参考值（P0 可微调）：Base `(0.10,0.10,0.10)`、Panel `(0.14,0.14,0.14)`、Raised `(0.18,0.18,0.18)`、Hover `(0.22,0.22,0.22)`、Selected `(0.20,0.17,0.10)`、AccentGold `(0.92,0.68,0.30)`、TextPrimary `(0.92,0.92,0.90)`、TextSecondary `(0.65,0.65,0.62)`、Danger `(0.55,0.18,0.15)`、Success `(0.16,0.35,0.22)`、Border `(0.30,0.30,0.28)`、BorderStrong `(0.45,0.45,0.42)`。
     - 间距：2/4/6/8/10；行高 S=22，M=26，L=32，XL=50。
  3. **自绘组件库规格**：现代行（hover/selected/左竖条/右操作）、segmented button、modern checkbox（18px 方框 + 金勾）、mode card（下金线选中）、text field 皮肤（外壳包 `Widgets.TextField`）、banner、footer 双槽、折线图、help `?`。
  4. 响应式三档规则（§3.4）。
  5. 原版 fallback 矩阵（§10）。
  6. 维护者决策项（§5 D1–D6）。
- **验收**：文档覆盖以上 6 点；维护者批准后进入 P1。

### P1 — 现代皮肤地基（令牌 + 组件库 + fallback guard）

- **文件范围**：
  - `Source/UniversalSqueaker/UI/Visuals/UsVisualTokens.cs`（新）：角色化令牌（`SurfaceBase/Panel/Raised/Hover/Selected/Warning/Success/Danger`、`TextPrimary/Secondary/Accent/Muted/Disabled`、`AccentGold/Border/BorderStrong`、间距/行高常量）。
  - `Source/UniversalSqueaker/UI/Visuals/UsSurface.cs`（新）：`DrawSurface`、`DrawRowSurface(rect, hover, selected, danger, leftAccent)`、`DrawSegment(rect, label, state)`、`DrawCheckbox(rect, value)`、`DrawHeader(rect, text, helpOpen?)`——统一收编现有 `DrawBoxSolid + DrawBorder` 组合，并成为后续所有 widget 的唯一表面入口。
  - `Source/UniversalSqueaker/UI/Visuals/UsGuard.cs`（新）：`MeasureOrFallback` / `DrawOrFallback`，catch 后先恢复 `Text.Font/Text.Anchor/GUI.color` 再画原版 fallback；按 widget+session 只 log 一次。
  - `Source/UniversalSqueaker/UI/Components/UiPalette.cs`：改为 `[Obsolete]` 薄转发到 `UsVisualTokens`（过渡期），随后删除。
  - 切换范围：`BasicTuningWidget`、`CameraIndicatorWidget`、`ScopeTreeWidget`、`PresetListWidget`、`RaceLayerWidget`、`XenotypeLayerWidget`、`VoicePackChecklistWidget`、`PageTitleWidget`、`RaceLayerRow`、`XenotypeLayerRow`、`VoicePackRow`、`VoicePackChecklist`、`SearchField`、`EmptyState`、`StatusBanner`、`HelpToggle`、`SectionFrame`。
  - **FerriteLib.UiKit 中性现代 skin**：`Widgets/Palette.cs` 重命名角色化并更新色值；`SurfaceFrame` / `ModeCardRenderer` / `ChromeBannerWidget` / `ChromeFooterWidget` / `SectionHeaderWidget` / `EmptyStateWidget` / `InputModeRowWidget` 跟随新 skin（仍无 US/SR 产品字面量）。
- **不做**：不改变任何交互/命令/布局高度。
- **验收**：Dev/Release 0 警告；`verify-local` 13 门全绿 + neutrality grep 通过；grep 确认 US widget 不再直接 `new Color(...)` 画表面（允许图表/文本特殊色）；行高/间距与 P0 令牌一致。

### P2 — Footer build identity（最小端到端验证）

- **文件范围**：
  - `Source/UniversalSqueaker/Mod.cs`：`BuildIdentity()` 由 private static 提为 `internal static`；新增 `internal bool IsSettingsDirty => requestedSaveGeneration > persistedSaveGeneration;`；`SaveState` 已 internal。
  - `Source/UniversalSqueaker/UI/Model/VoicePacksViewState.cs`：新增 `BuildIdentity`、`SaveStatus`（string）、`IsDirty`（bool）只读字段。
  - `Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`：投影上述三键（SaveState → "Idle"/"Saving"/"Saved"/"Failed"，`UniversalSqueakerMod.Instance` 为空时 "Unknown"）。
  - `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`：view state 字典加三键。
  - `Source/UniversalSqueaker/UI/Widgets/UsFooterWidget.cs`（新，Kind=`us/footer`）：左槽版本、右槽保存状态；颜色：Saving=Accent，Saved=Muted，Failed=Danger，Dirty=Accent 加 `●`。
  - `Source/UniversalSqueaker/UI/UsWidgetRegistrar.cs`：注册 `us/footer`。
  - `Source/UniversalSqueaker/UI/Layout.xml`：`chrome/footer` → `us/footer`。
- **验收**：footer 显示 `dev-<rev>/<informational>` 或版本号；编辑任一开关后右槽进入 Saving→Saved，0 警告；手动把写桥制造失败时显示 Failed（maintainer 游戏内可选项，不强制）。

### P3 — 距离预览折线图

- **文件范围**：
  - `Source/UniversalSqueaker/UI/Layout/DistancePreview.cs`（新，零 Verse）：`SampleAudibilityCurve(float min, float max, int samples, float graphMin, float graphMax)` → `IReadOnlyList<Point>`（归一化 0..1）；语义：`d≤min` 全响、`min→max` 线性衰减、`d≥max` 静默。
  - `Source/UniversalSqueaker/UI/Widgets/DistanceChartWidget.cs`（新，Kind=`us/distance-chart`）：64px 高图表，画 min/max 刻度、填充曲线、当前预设名；点击图表 = 循环预设（复用 `UiCommandKind.SetDistancePreset`）；宽度 < 200px 时降级为一行文本「Too narrow for chart」；fallback = 文本摘要。
  - `Source/UniversalSqueaker/UI/Model/VoicePacksViewState.cs`：新增 `DistanceRangeMin/Max`（或 `FloatRange DistanceRange`；本块只读）。
  - `VoicePacksPageModel.cs` / `FerriteVoicePacksPage.cs`：投影 `DistanceRangeMin`、`DistanceRangeMax`。
  - `BasicTuningWidget.cs`：在距离行下方嵌入 `DistanceChartWidget` 的绘制（或 `Layout.xml` 独立 root——**推荐独立 root `us/distance-chart` 插在 basic-tuning 与 camera-indicator 之间**，widget 内部自绘，不膨胀 BasicTuningWidget）。
  - `UsWidgetRegistrar.cs` / `Layout.xml` 注册与排版。
- **验收**：`DistancePreview` 纯函数测试通过（端点、单调非增、min=max 退化、样本数 2 和 200）；三档预设显示 15–65 / 15–50 / 15–40 对应曲线；Custom 显示当前 range。

### P4 — 过滤系统

- **文件范围**：
  - `Source/UniversalSqueaker/UI/Layout/VoicePacksFilters.cs`（新，零 Verse，primitive 判定）：
    - `DomainMatches(bool hasConflict, bool isDormant, bool isTargetUnavailable, bool isOrphan, bool isEnabled, string author, UiDomainFilter filter)`
    - `PackMatches(string author, string modName, bool isSelected, string search, UiPackFilter filter)`
    - `UiDomainFilter` / `UiPackFilter` 纯 struct（`EnabledOnly/ConflictOnly/OrphanOnly/Author`）。
  - `Source/UniversalSqueaker/UI/Model/VoicePacksPageState.cs`：新增 `UiDomainFilter DomainFilter`、`UiPackFilter PackFilter`（ephemeral，Reset 清空）。
  - `Source/UniversalSqueaker/UI/Model/UiCommand.cs`：新增 `SetDomainFilter` / `SetPackFilter`（Arg=过滤键，Flag=开/关；作者用 `SetPackFilter, Arg="Author|<name>"`）。
  - `Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`：`BuildView` 用 `VoicePacksFilters` 过滤 `Races` / `XenotypeDomains` / `SelectedDomain.Packs`（选中域被过滤掉时回退到过滤后首个域）；`Execute` 处理两个新命令。
  - `Source/UniversalSqueaker/UI/Widgets/FilterBarWidget.cs`（新，Kind=`us/filter-bar`）：
    - 一行三枚段按钮「All / Enabled only / Conflicts」+ 一枚「Orphan only」+ 作者循环按钮（`Author: <name>/All`）。
    - 冲突定义：`HasCanonicalConflict || IsTargetUnavailable || State == Orphan` 之外，单独 `Orphan only` 只看 `OrphanCount>0`。Race 行只看 `Enabled only` 与 `Orphan only`。
  - `UsWidgetRegistrar.cs` / `Layout.xml`：`us/filter-bar` 插在 banner 与 mode-row 之间（或 mode-row 后）。
  - `VoicePackChecklist` 作者过滤由 model 过滤后的 `SelectedDomain.Packs` 承担，widget 不改。
- **验收**：纯函数测试覆盖各组合；UI 过滤后 race/xeno 列表与 checklist 同步收窄；过滤不影响任何业务写入；空过滤结果显示「No domains match filter」（不是崩溃）。

### P5 — 组件化帮助

- **文件范围**：
  - `Source/FerriteLib.UiKit/Context/UiPageState.cs`：新增 `public readonly HashSet<string> OpenHelpKeys = new(StringComparer.Ordinal);`（中性，无产品字面量）+ `ToggleHelpKey(key)`；`Reset` 清空。
  - `Source/UniversalSqueaker/UI/Help/UsHelpCatalog.cs`（新）：`Get(key)`；首批键：`us/mode-row`、`us/basic-tuning`、`us/distance-chart`、`us/camera-indicator`、`us/scope-tree`、`us/preset-list`、`us/filter-bar`、`us/race-layer`、`us/xenotype-layer`、`us/voice-pack-checklist`。
  - `Source/UniversalSqueaker/UI/Widgets/UsHelpButton.cs`（新）：`Draw(Rect, string helpKey, UiPageState state, Action<KitUiCommand> emit)`——无帮助键则返回 false。
  - 各 widget 头行：`RaceLayerWidget`、`XenotypeLayerWidget`、`VoicePackChecklistWidget`、`BasicTuningWidget`、`CameraIndicatorWidget`、`ScopeTreeWidget`、`PresetListWidget`、`FilterBarWidget`、`DistanceChartWidget`、`PageTitleWidget`（page title 改用新机制，迁移到 `us/page-title` 键）。
  - `UiElementSpec` 已支持任意属性；`Layout.xml` 各 widget 增加 `HelpKey`。
  - `Measure`：各 widget 在 `HelpOpen` 时加 `BannerHeight(helpText, width, metrics) + Gap`；`Draw` 就地渲染帮助 banner（在 section header 之下、内容之上）。帮助 banner 使用现代 banner 表面，不可点击、不拦截按钮（无 `ButtonInvisible`，天然不点穿）。
  - `FerriteVoicePacksPage.cs`：处理 `ToggleHelp` 命令时优先按 payload 的 key 调 `UiPageState.ToggleHelpKey`；旧的页级 `HelpOpen` 保留兼容或迁移。
- **验收**：每个带帮助键的 widget 头出现 `?`；点击就地展开，下一帧 `Measure` 预留高度，内容不压到后续区块；再点收起；刷新/重开窗口状态复位；FerriteLib 测试补 `UiPageState` 帮助键集合默认空/Reset 清空。

### P6 — 窄屏响应式加固（最后做）

- **文件范围**：
  - `Source/UniversalSqueaker/UI/Layout/VoicePacksLayout.cs`：新增 `LayoutTier ForWidth(float)`（`Comfortable/Compact/Minimal/Fallback`）、`ClampContentWidth`、`IsUltraCompact`；统一 min/max 数学。
  - `ScopeTreeWidget`：层段按钮最小 56px；域行文本宽 clamp；scope 行在 Compact 下隐藏「→ effective」次级提示；mood 簇保持 86px 守卫。
  - `BasicTuningWidget` / `CameraIndicatorWidget`：label 宽 `Math.Max(1f, ...)`，checkbox 在 Minimal 下移到左列，行高不变。
  - `RaceLayerRow` / `XenotypeLayerRow`：Compact 下隐藏 detail 行（或截断），保留主标签 + 状态点。
  - `PresetListWidget`：xeno 缩进从 18px 降到 12px；Import 按钮宽固定 76px，标签宽 clamp。
  - `FilterBarWidget` / `DistanceChartWidget` / `UsFooterWidget`：按 P6 规则自适配。
  - `FerriteLib.UiKit/Widgets/InputModeRowWidget.cs`（中性增强）：卡片行在宽度不足以容纳 4 张卡（每张 < 140px）时自动 2×2 网格；仍不够则 1 列。属库的中性响应式改进，不含产品字面量。
  - `FerriteVoicePacksPage.cs`：当 `rect.width < 240px` 时直接 `EmptyState.Draw("Window too narrow")` 早退（scrollbar 计算之前）。
- **验收**：新 UI 逻辑测试锁定 `ForWidth` 与 tier 边界；在 480/360/300/240 四档宽度下人工走查矩阵（maintainer 游戏内）；任何 widget 不产生负宽度绘制。

### P7 — 全量换肤收口 + 页面级 vanilla 兜底页

- **文件范围**：
  - `Source/UniversalSqueaker/UI/VanillaVoicePacksPage.cs`（新）：纯 `Verse.Widgets` 的简化功能兜底页。复用 `VoicePacksPageModel.BuildView/Execute` 投影与命令，只换绘制层（原版 checkbox / button / text field / label）。覆盖关键功能：模式 4 选、距离预设循环、三个缩放开关、彩蛋、相机指示、race/xeno 域选择 + VoicePack 勾选。
  - `FerriteVoicePacksPage.Draw`：把现有整页 catch 从「画 EmptyState 错误」升级为「log + 绘制 `VanillaVoicePacksPage`」；Layout.xml 解析/引擎构建失败同样走兜底页。
  - 全量换肤收口：对照 P0 规格逐 widget 走查，补齐 hover/selected/danger/focus 细节。
- **验收**：人为在 dev 构建中注入「某 widget 抛异常」与「Layout.xml 损坏」两类故障，确认兜底页出现且可完成核心设置操作；恢复正常后 Ferrite 页面照常。

---

## 5. 风险与维护者决策项

| # | 决策项 | 状态 / 推荐 | 影响 |
|---|---|---|---|
| D1 | 视觉路线 | **已拍板：全量换肤（简洁现代 RimWorld 配色组件）** | P0 规格将直接按全量换肤编写；P1/P7 范围扩大 |
| D2 | FerriteLib 中性边界 | **FerriteLib 只做中性现代 skin + 中性能力**（帮助键集合、响应式），不引入 US/SR 产品字面量 | 保持库可复用 |
| D3 | 帮助展开状态归属 | **`UiPageState.OpenHelpKeys`（中性通用容器）**，不放 US 业务 state | 库仍中性，US 持有键名 |
| D4 | 距离图曲线语义 | **示意曲线**（min 内 1、min→max 线性衰减、max 外 0），文档注明非引擎物理 | 真实 rolloff 属 Verse/Unity，无预览必要 |
| D5 | `VoicePacksPageModel.BuildView` 写回 state | 本计划保持现有写回（避免扩大改动）；过滤/帮助不新增写回，只读投影 | review-05 M3 的纯化不在 S4 范围内，记录为后续 |
| D6 | 原版 fallback 粒度 | **推荐 L1+L2+L3 三级（§10）**：交互组件级 fallback + 信息组件文本降级 + 页面级 vanilla 兜底页 | 保证任何自绘故障下设置页仍可用；成本约 +450–600 行 |
| R1 | 皮肤地基是跨文件重构 | 用 `[Obsolete]` 转发 + 逐文件切换，不一次删 `UiPalette` | 每步可编译、可回滚 |
| R2 | 帮助展开高度影响所有 `Measure` | 所有带帮助的 widget 必须同时改 `Measure` 与 `Draw`，忘记会导致内容被截 | 验收强制「展开后完整可见」 |
| R3 | 过滤改变 race/xeno 列表，选中域可能被滤掉 | `ResolveSelectedDomain` 在过滤后列表回退到首个可用域 | 避免选中幽灵域 |
| R4 | `FerriteVoicePacksPage` 的 view state 字典键增多 | 键名集中在常量类，避免字符串漂移 | 可读性/防拼错 |
| R5 | 游戏内无法在本环境验证 | 所有视觉块只编译+纯逻辑测试；渲染正确性进 maintainer 游戏内矩阵 | 已知限制，不阻塞合入 |
| R6 | fallback 被异常打断后的 GUI 状态 | `UsGuard` 在 catch 后必须先恢复 `Text.Font/Text.Anchor/GUI.color` 再画原版控件 | 否则 fallback 本身会画脏 |

---

## 6. 验收标准与测试策略

### 6.1 每块通用门禁

- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev`（0 警告）
- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release`（0 警告）
- `dotnet run --project tools/UniversalSqueakerKernelTests -c Release`（kernel golden-corpus 零差）
- `pwsh -File scripts/verify-local.ps1`（当前 13 门，预计 P4 后 14 门）
- 隐私预检：无 `PublishedFileId`/个人路径/凭据。
- 每块原子提交。

### 6.2 新增测试项目（P3/P4 落地）

- **`tools/UniversalSqueakerUiLogicTests`**（新，仿 kernel-tests 的零 Verse 链接模式）：
  - 链接 `Source/UniversalSqueaker/UI/Layout/DistancePreview.cs`、`UI/Layout/VoicePacksFilters.cs`（以及未来新增的零 Verse UI 布局文件）。
  - 断言：
    - `DistancePreview`：端点值、单调非增、min=max 退化、样本数 2/200、越界输入 clamp。
    - `VoicePacksFilters`：EnabledOnly / ConflictOnly / OrphanOnly / Author / 组合过滤 / 空过滤恒真。
  - `scripts/verify-local.ps1` 增加第 14 门：`dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release`。
- **`tools/FerriteLib.UiKit.Tests`** 追加：
  - `UiPageState.OpenHelpKeys` 默认空 / Toggle / Reset。
  - `InputModeRowWidget` 窄宽自动 2×2 / 1 列（如 P6 实现）。
  - 既有「measure 后 view width 变化再 draw」缓存失效用例补一条（review-05 Nit）。
- **`UsGuard` 逻辑**：在 stub 环境用可注入的抛异常 delegate 测「异常 → 恢复 GUI 状态 → 调用 fallback delegate → 只 log 一次」。

### 6.3 游戏内人工矩阵（maintainer step）

- 空 catalog：过滤条、帮助、footer、距离图均不崩溃，空态优雅。
- 无 Biotech：Xenotype 域 dormant，帮助/过滤与 Dormant banner 并存。
- 同 xeno 多 race：Xenotype 行带 race 标识（现有）且过滤/帮助/窄屏不破坏其可读性。
- 极窄窗口：480 / 360 / 300 / 240 四档，ScopeTreeWidget / BasicTuning / FilterBar / DistanceChart 不重叠、不异常。
- 编辑任一设置：footer 右侧出现 Saving→Saved；重开窗口持久化正常。
- 每个带 `?` 的 widget：展开帮助后滚动到底部，确认帮助 banner 不覆盖下一区块。
- **fallback 触发**：dev 构建注入单 widget 异常 → 该区段降级为原版控件且可操作；注入整页/Layout.xml 异常 → `VanillaVoicePacksPage` 兜底页出现且核心设置可改。

---

## 7. 预计改动面

| 层 | 文件 | 改动类型 |
|---|---|---|
| FerriteLib.UiKit | `Widgets/Palette.cs`、`SurfaceFrame.cs`、`ModeCardRenderer.cs`、`ChromeBannerWidget.cs`、`ChromeFooterWidget.cs`、`SectionHeaderWidget.cs`、`EmptyStateWidget.cs`、`InputModeRowWidget.cs`、`Context/UiPageState.cs`、`tools/FerriteLib.UiKit.Tests/*` | 中性现代 skin + 中性能力 + 测试 |
| US UI 新文件 | `UI/Visuals/UsVisualTokens.cs`、`UI/Visuals/UsSurface.cs`、`UI/Visuals/UsGuard.cs`、`UI/Help/UsHelpCatalog.cs`、`UI/Widgets/UsHelpButton.cs`、`UI/Widgets/UsFooterWidget.cs`、`UI/Widgets/FilterBarWidget.cs`、`UI/Widgets/DistanceChartWidget.cs`、`UI/Layout/VoicePacksFilters.cs`、`UI/Layout/DistancePreview.cs`、`UI/VanillaVoicePacksPage.cs` | 新增 |
| US UI 修改 | `FerriteVoicePacksPage.cs`、`VoicePacksPageModel.cs`、`VoicePacksViewState.cs`、`VoicePacksPageState.cs`、`UiCommand.cs`、`UsWidgetRegistrar.cs`、`Layout.xml`、`VoicePacksLayout.cs`、全部现有 widget/component | 修改 |
| US 其它 | `Mod.cs`（BuildIdentity/IsSettingsDirty 暴露） | 只读暴露 |
| 工具 | `tools/UniversalSqueakerUiLogicTests/**`、`scripts/verify-local.ps1`（+1 门） | 新增 |
| 文档 | `docs/ui-visual-modernization-zh.md`、本计划 | 新增 |

预计新增 C# 约 1300–1700 行（含测试），修改约 800–1100 行。

---

## 8. 建议执行顺序汇总

```text
P0 视觉评估稿（全量换肤规格 + fallback 矩阵，维护者批准 D1–D6）
  │
P1 现代皮肤地基（UsVisualTokens + UsSurface + UsGuard + FerriteLib 中性现代 skin）
  │
P2 footer build identity（小，端到端验证 view 键 + Mod 暴露）
  │
P3 距离预览折线图（纯函数 + DistanceChartWidget + 测试项目）
  │
P4 过滤系统（纯函数 + FilterBarWidget + 模型投影 + 测试）
  │
P5 组件化帮助（UiPageState 帮助键 + UsHelpCatalog + 每 widget 就地帮助）
  │
P6 窄屏响应式（三档规则 + 全 widget 加固 + FerriteLib 中性响应式）
  │
P7 全量换肤收口 + VanillaVoicePacksPage 兜底页
```

- 串行主链：P0 → P1 → P2 → P3 → P4 → P5 → P6 → P7。
- 可并行窗口：P3 与 P4 在 P2 后可由两个 subagent 并行（文件冲突仅 `Layout.xml`/`UsWidgetRegistrar.cs`/view state 字典，需协调或串行收口）。
- P5、P6 必须串行且 P6 最后（所有 widget 的最终 Measure 逻辑稳定后再加固）。
- P7 的页面级兜底页可与 P1 的 `UsGuard` 并行设计，但代码合入放在最后（避免早期分心）。

---

## 9. 与后续工作的边界

- 本计划完成后，`docs/workdocs/` 移除与 Ferrite UI 游戏内稳定化（maintainer）仍按 `TODO.md` 顺序执行，不在本计划内。
- 可选 Runtime harness（ReviewResolverFold P3 残留）与本计划无文件交集，可并行。
- 术语说明：RimWorld 1.6 mod UI 只有 Unity IMGUI + `Verse.Widgets` 一条路，每帧全量重画，不存在新的保留模式 GUI 系统。「全量换肤」= 视觉皮肤与绘制代码整体替换，不是渲染方式切换。

---

## 10. 原版 fallback 评估（维护者追加评估项）

### 10.1 结论

**需要，分三级做，不是每个像素级组件都做。** 核心原则：**玩家永远不能因为 UI 皮肤挂了而失去设置能力。**

| 级别 | 覆盖 | 触发 | 降级形态 |
|---|---|---|---|
| L1 交互组件级 | 有原版对应物的交互控件（checkbox 行、段按钮、mode 卡、mood 步进、VoicePack 开关、搜索框、帮助 `?`、Import 按钮） | 该 widget 的 `Measure`/`Draw` 抛异常 | 同一 rect 内改画原版 `Widgets.Checkbox` / `ButtonText` / `TextField`，命令流不变 |
| L2 信息组件级 | 无交互的自绘内容（距离图、footer、banner、section header、empty state） | 同上 | 文本摘要 / 原版 `Widgets.Label` / 简单 `DrawBoxSolid` |
| L3 页面级 | 整页（Ferrite 引擎、注册表、Layout.xml 解析、未知异常） | `FerriteVoicePacksPage.Draw` 外层 catch | 新 `VanillaVoicePacksPage`：纯 `Verse.Widgets` 的简化功能页，复用 `VoicePacksPageModel.BuildView/Execute` |

### 10.2 组件 → 原版 fallback 映射

| 自绘组件 | 原版对应物 | 级别 | 说明 |
|---|---|---|---|
| 现代 toggle 行（BasicTuning / CameraIndicator） | `Widgets.Checkbox` + `Widgets.Label` | L1 | 命令 `ToggleEgg`/`SetDistancePreset`/`ToggleBasic` 不变 |
| 现代段按钮（layer / scope / filter） | `Widgets.ButtonText`，选中项前缀 `●` | L1 | 命令不变 |
| 现代 mode 卡 | 2×2 或 1 列 `Widgets.ButtonText`，选中项前缀 `●` | L1 | `InputModeRowWidget` 在 FerriteLib 内实现，保持中性 |
| 现代 VoicePack 开关 | `Widgets.Checkbox` + 两行 `Widgets.Label` | L1 | `TogglePack` 命令不变 |
| 现代搜索框 | `Widgets.TextField`（去掉皮肤外壳） | L1 | 本就可用的原版路径 |
| 现代 help `?` | `Widgets.ButtonText("?")` | L1 | `ToggleHelp` 命令不变 |
| mood −/＋ 步进 | 两个 `Widgets.ButtonText`（−/＋）+ `Widgets.Label` 值 | L1 | `SetMoodTuning` 命令不变 |
| Import 按钮 | 已用 `Widgets.ButtonText`，无额外风险 | L1 | 无需 fallback |
| 距离折线图 | `Widgets.Label` 文本摘要（`Conservative 15–65` 等） | L2 | 非交互 |
| footer 双槽 | 两个 `Widgets.Label` | L2 | 非交互 |
| banner / section header / empty state | `Widgets.Label`（banner 加简单 `DrawBoxSolid`） | L2 | 非交互 |
| 整页 | `VanillaVoicePacksPage` | L3 | 见 P7 |

### 10.3 实现要点

- `UsGuard.MeasureOrFallback(customMeasure, fallbackHeight, widgetId)` / `UsGuard.DrawOrFallback(rect, customDraw, vanillaDraw, widgetId)`：
  - catch 后**先恢复 GUI 状态**（`Text.Font = GameFont.Small`、`Text.Anchor = UpperLeft`、`GUI.color = Color.white`）再画 fallback；否则 fallback 会继承异常帧的脏状态。
  - 每个 widget 每会话只 log 一次（复用/仿照 `SqueakLogOnce` 的 once 门控）。
  - fallback 不吞业务异常：命令执行（`ExecuteAll`）仍在渲染循环外，异常照常抛给页面级。
- `VanillaVoicePacksPage` 只做简化功能面，不追求完整：模式、距离、三开关、彩蛋、相机、域选择 + pack 勾选即可；scope tree / mood 调音 / preset import 在兜底页可省略（皮肤故障是低频事件，保核心路由可用）。
- FerriteLib 侧的中性组件（mode row / banner / footer / section header / empty state）同样按 L1/L2 在库内实现 fallback，不出现产品字面量。

### 10.4 成本与测试

- L1 + L2：约 +250–350 行（每个 widget 一个 `DrawVanillaFallback` 小方法 + `UsGuard` 两个方法）。
- L3：约 +200–250 行（`VanillaVoicePacksPage`）。
- 单测：`UsGuard` 用可注入抛异常 delegate 测恢复/调用/once；fallback 页面无法单测渲染，进 maintainer 游戏内矩阵。
- 游戏内验证：dev 构建注入单 widget 异常 → 该区段原版控件可操作；注入 Layout.xml 损坏 → 兜底页可改核心设置。

### 10.5 不建议的过度方案

- **不做**「每个像素都保留一套 vanilla 皮肤切换开关」：双皮肤长期并存会退化成 Legacy UI 双路径漂移，正是 Phase 0 删掉的东西。
- **不做**「fallback 自动恢复重试自定义皮肤」：同一帧内重试同一条失败代码没有收益，且 IMGUI 下容易把异常帧状态带进第二遍。
