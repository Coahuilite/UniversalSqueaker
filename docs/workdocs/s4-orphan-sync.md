# 任务书：S4-Orphan-Sync（孤儿控件 Ferrite 路径同步）

## 工作目录
仓库根：<workspace>\UniversalSqueaker（下称 <root>）。
只读本任务书 + 你改动的源文件 + HANDOFF §8 + MEMORY checkpoint。不要读 docs 下其它长篇文档。

## 目标
把三个「数据+运行时活着、UI 死着」的孤儿控件接回 **Ferrite 路径（默认开启）**：
- A7 彩蛋开关（allowEasterEggSounds）
- A3 距离预设（distancePreset，Conservative/Balanced/Strong 三档循环 + Custom 显示）
- A4 三个运行时缩放开关（scaleCooldownWithTimeSpeed / scaleFrequencyWithTalking / scalePeriodicWithAudiblePopulation）

> 注意：旧 UI 路径（VoicePacksPage.cs 的 legacy 分支）已实现这三个控件，但 Ferrite 路径（UseFerriteUi=true 默认）没有。本次只补 Ferrite 路径；**不含距离预览折线图**（那是后续 S4-Polish）。

## 关键事实（已核实，勿重新推导）
- Ferrite 页面入口：Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs 的 Draw()，它把 view 投影进 Dictionary<string,object?> viewState（当前键：BannerText/Mode/Races/XenotypeDomains/SelectedDomain/EmptyText/HelpText），再跑 LayoutEngine。
- US widget 注册：Source/UniversalSqueaker/UI/UsWidgetRegistrar.cs 的 EnsureRegistered()，用 WidgetRegistry.Register(Scope, Widget.Kind, factory)。
- US widget 模式：实现 FerriteLib.UiKit.IWidget（Configure/Measure/Draw），从 ctx.TryGetViewValue(key, out object?) 读值，用 UsWidgetCommandAdapter.For(emit) 把 business UiCommand 转成 neutral Ferrite UiCommand(name, UsCommandPayload)。
- business 命令枚举：Source/UniversalSqueaker/UI/Model/UiCommand.cs 已含 ToggleEgg / SetDistancePreset / ToggleBasic；UsCommandPayload 已含 Arg/Flag 字段承载它们。
- UsWidgetCommandAdapter.For（Source/UniversalSqueaker/UI/Widgets/UsWidgetCommandAdapter.cs）当前只映射 SetMode/SelectDomain/TogglePack/ForgetUnavailable，**缺 ToggleEgg/SetDistancePreset/ToggleBasic**。
- FerriteVoicePacksPage.TryTranslate 的 UsCommandPayload 分支已能把 payload 转回 business UiCommand，**无需改 TryTranslate**。
- 参考 widget：Source/UniversalSqueaker/UI/Widgets/PageTitleWidget.cs（Configure/Measure/Draw 完整范本）、RaceLayerWidget.cs（动态列表 + UsWidgetCommandAdapter.For 发射）。
- 旧 UI 的控件文案与循环逻辑在 VoicePacksPage.cs 的 DrawEasterEggToggle/DrawDistancePresetRow/DrawBasicToggle（line 218-295），文案保持一致。

## 改动清单（精确）

### 1. UsWidgetCommandAdapter.For 补三个映射
Source/UniversalSqueaker/UI/Widgets/UsWidgetCommandAdapter.cs 的 switch 增加：
- UiCommandKind.ToggleEgg => "ToggleEgg"
- UiCommandKind.SetDistancePreset => "SetDistancePreset"
- UiCommandKind.ToggleBasic => "ToggleBasic"

### 2. FerriteVoicePacksPage.Draw 的 viewState 字典补 5 个键
在现有 viewState 初始化里追加：
- ["AllowEasterEggs"] = view.AllowEasterEggs
- ["DistancePreset"] = view.DistancePreset.ToString()   // 用 string，widget 里 Enum.TryParse 回 enum
- ["ScaleCooldownWithTimeSpeed"] = view.ScaleCooldownWithTimeSpeed
- ["ScaleFrequencyWithTalking"] = view.ScaleFrequencyWithTalking
- ["ScalePeriodicWithAudiblePopulation"] = view.ScalePeriodicWithAudiblePopulation

### 3. 新增一个 composite widget：Source/UniversalSqueaker/UI/Widgets/BasicTuningWidget.cs
- public const string Kind = "us/basic-tuning";
- 实现 FerriteLib.UiKit.IWidget。
- Measure：固定 5 行高度（每行 28f + 2f 间距，共 5*28 + 4*2 = 148f；用与旧 UI 一致的 eggRowHeight=28f、distRowHeight=28f、basicRowHeight=26f 也行，保持稳定即可，返回常量 + 少量 padding）。
- Draw：从上到下画 5 行，全部读 ctx 值 + 用 UsWidgetCommandAdapter.For(emit) 发射：
  1. 彩蛋开关行：读 AllowEasterEggs(bool)，文案 "Easter egg sounds" / 副文案 On(eggs join the pool)/Off(ordinary entries only)，点击发射 UiCommand(UiCommandKind.ToggleEgg, flag: !current)。
  2. 距离预设行：读 DistancePreset(string)，Enum.TryParse 回 SqueakDistancePreset，文案 "Distance preset" + 副文案 Conservative(15~65)/Strong(15~40)/Balanced(15~50)/Custom；点击循环 Conservative→Balanced→Strong→Conservative（Custom → Balanced），发射 UiCommand(UiCommandKind.SetDistancePreset, arg: next.ToString())。
  3. ScaleCooldown 行：读 ScaleCooldownWithTimeSpeed(bool)，label "Scale cooldown with time speed"，发射 UiCommand(UiCommandKind.ToggleBasic, arg:"ScaleCooldown", flag:!current)。
  4. ScaleTalking 行：读 ScaleFrequencyWithTalking(bool)，label "Scale frequency with talking"，arg:"ScaleTalking"。
  5. ScalePopulation 行：读 ScalePeriodicWithAudiblePopulation(bool)，label "Scale periodic with audible population"，arg:"ScalePopulation"。
- 画法复用 VoicePacksPage 旧分支的视觉（DrawBoxSolid + SectionFrame.DrawBorder + label + ButtonInvisible + 右侧 checkbox for toggle），或简化用 SurfaceFrame/UiKitGui，只要交互正确、不点穿、有 hover 反馈即可。行高稳定、无黑灰框更佳。
- 用 Widgets.ButtonInvisible(rect) 做整行点击；checkbox 用 Widgets.Checkbox 仅作显示态（toggle 行）。
- 文案硬编码英文即可（与旧 UI 一致），不要碰 1.6/Languages/**。

### 4. UsWidgetRegistrar 注册
EnsureRegistered() 里加：WidgetRegistry.Register(Scope, BasicTuningWidget.Kind, () => new BasicTuningWidget());

### 5. Layout.xml 插入一行
Source/UniversalSqueaker/UI/Layout.xml 在 mode-row 之后、race-layer 之前插入：
  <Widget Id="basic-tuning" Kind="us/basic-tuning" />
（Widget 从 viewState 读值，无需额外属性。）

## 红线
- 不新增/删除 Scribe 字段；不改 business 模型 VoicePacksViewState/VoicePacksPageModel/UiCommand 的既有字段语义（只新增 widget + adapter 映射 + viewState 键 + 注册 + XML 行）。
- 不碰 8 个 Harmony patch、S3 Sustainer/外部动作、S4-Tuning-Backend 的 baseline Def。
- 不碰 FerriteLib 库源码（只用其公开 IWidget/UiCommand/WidgetRegistry/WidgetContext）。
- 不改旧 UI legacy 分支（VoicePacksPage.cs 的非 Ferrite 路径保持不动）。

## 门禁
1. dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev（零警告）
2. dotnet run --project tools/UniversalSqueakerKernelTests -c Release（全绿）
3. pwsh -File scripts/verify-local.ps1（12 门全绿，含 UI layout XML well-formedness）

## 完成报告（return 输出）
1. 改动文件清单（相对路径）。
2. 三条门禁退出结果。
3. 设计取舍或遗留（尤其：widget 行高/视觉是否与旧 UI 一致、是否复用 DrawBoxSolid/SectionFrame）。
