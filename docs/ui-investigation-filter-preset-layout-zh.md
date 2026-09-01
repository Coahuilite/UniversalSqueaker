# US 设置 UI 调查记录：筛选、语音包、预设与调音布局

状态：仅调查，未修改源代码，未打包。

本记录覆盖维护者在游戏内截图反馈的四组问题：

1. Race 筛选无法切回全部；
2. Race 筛选后 VoicePack Checklist 看不到包；
3. 调音基线预设导入/导出模块不可用；
4. Tuning 页面出现横向滚动条，Mood Tuning 控件疑似贴边或越界。

## 1. 证据范围

### 实机截图可见事实

- Race 筛选下拉能够列出具体 race，但界面没有明确的 `All`/`全部` race 选项。
- 点击顶部 `All` 后，维护者仍观察到之前的 Race 筛选状态；退出设置页再打开后状态恢复。
- 某些 Race 行显示存在候选包，例如 `1 / 1 enabled`，但 VoicePack Checklist 显示空状态。
- Tuning 页面同时可见右侧垂直滚动条和底部横向滚动条。
- Mood Tuning 行的滑条、数值框、加减按钮和 `Auto` 按钮排列到容器右端，截图中至少表现为严重贴边；仅凭单帧不能断言每种分辨率都发生像素级裁剪。
- 预设区没有可供选择的实际预设树/行，因此看不到可操作的 Import 对象。

### 代码证据

- `Source/UniversalSqueaker/UI/Kernel/UsFilterBarWidget.cs`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`
- `Source/UniversalSqueaker/UI/Kernel/UsVoicePackChecklistWidget.cs`
- `Source/UniversalSqueaker/UI/Kernel/UsPresetListWidget.cs`
- `Source/UniversalSqueaker/Runtime/UniversalSqueakerTuningBaselineDef.cs`
- `Source/UniversalSqueaker/Settings/BaselinePresetImporter.cs`
- `Source/FerriteLib.UiKit/Kernel/UiLayoutEngine.cs`
- `Source/UniversalSqueaker/UI/Kernel/UsScopeTreeWidget.cs`
- `1.6/Defs/` 当前只有 `.gitkeep`，没有实际 `UniversalSqueakerTuningBaselineDef` XML。

## 2. Race 筛选无法切回全部

### 结论

**根因已确认：顶部 `All` 按钮没有清除 `RaceFilter` 和 `XenotypeFilter`。**

### 证据

`UsFilterBarWidget.DrawDomainRow` 的 `All` 点击分支只执行：

- `EnabledOnly = false`；
- `ConflictOnly = false`；
- `OrphanOnly = false`；
- `set-pack-filter` 为空。

它没有写入：

- `race-filter = ""`；
- `xenotype-filter = ""`。

对应代码：`UsFilterBarWidget.cs:88-96`。

业务模型明确把空字符串定义为全部：

- `VoicePacksPageState.cs:35-37`：Race/Xenotype filter 空字符串表示 All；
- `VoicePacksFilters.cs:82-99`：空 Race/Xenotype filter 匹配所有域；
- `VoicePacksPageModel.cs:243-249`：Race filter 只在收到具体 Race setter 时更新，`All` 按钮当前没有调用该 setter。

因此当前状态可能变成：

```text
DomainFilter = empty
PackFilter = empty
RaceFilter = Kiiro_Race
XenotypeFilter = empty
```

UI 上 `All` 的高亮状态只检查 DomainFilter 和作者过滤器：

```text
allActive = no domain flags && packAuthor.Length == 0
```

它也没有检查 `RaceFilter`/`XenotypeFilter`，所以按钮可能显示为 All，但实际 Race 筛选仍然生效。

### 为什么退出重开能恢复

`VoicePacksPageState.Reset()` 会把 `RaceFilter` 和 `XenotypeFilter` 都设为空。设置窗口关闭后重建会话，因此旧筛选状态被清除。这是状态重置带来的恢复，不是筛选控件本身正确处理了 All。

## 3. Race 筛选后 VoicePack 不显示

### 结论

**已确认一个独立的高概率根因：Kernel 搜索框把 placeholder 当成真实搜索值写回了 `SearchText`。**

这不是 catalog 一定没有包；它可以同时出现：

```text
Race row: 1 / 1 enabled
Domain.Packs.Count: > 0
Checklist: No VoicePacks match the current search.
```

### 证据

`UsVoicePackChecklistWidget.DrawSearchField` 在当前搜索为空时，把下列文字直接传给原生 TextField：

```csharp
state.Focused ? state.EditText : (current.Length > 0 ? current : "Search VoicePacks…")
```

对应代码：`UsVoicePackChecklistWidget.cs:191-205`。

随后它比较原生返回值和空的 `state.EditText`：

```csharp
if (!string.Equals(typed, state.EditText, StringComparison.Ordinal))
{
    state.EditText = typed;
    ctx.Bindings.Set("search-text", typed);
}
```

对应代码：`UsVoicePackChecklistWidget.cs:207-212`。

在第一次未聚焦绘制时，原生 TextField 可能返回传入文本 `Search VoicePacks…`，于是该 placeholder 被写入 `search-text`。模型随后按这个字符串筛选：

```csharp
VoicePacksPageModel.BuildView
    -> FilterDomainPacks(...)
    -> UsVoicePackChecklistWidget.MatchesSearch
```

`MatchesSearch` 对 `Search VoicePacks…` 执行普通文本匹配，正常 VoicePack 通常不会包含该字符串，因此所有包都被过滤掉。

这也解释了截图中看似只是 placeholder、实际却显示空列表的矛盾。

### 相关显示误导

空状态判断在 `UsVoicePackChecklistWidget.cs:135-145`：

- `domain.Packs.Count == 0` 时显示 `No VoicePacks are installed for this domain.`；
- `domain.Packs.Count > 0` 但搜索后无行时显示 `No VoicePacks match the current search.`。

因此截图显示后者，反而证明原始 domain 很可能存在包，问题在搜索/包过滤投影，而不是单纯 catalog 为空。

### 仍需实机确认的边界

代码还允许 `UiPackFilter.Author` 过滤当前选中域的包；但当前截图无法可靠证明作者过滤是否残留。因此需要修复搜索 placeholder 后，再单独验证作者筛选状态。当前已确认的首要问题是 placeholder 写回。

## 4. 预设导入/导出模块不可用

### 结论 A：导出功能当前不存在

**代码中没有预设导出 API、命令、binding 或 UI。**

当前接口只有：

- `ToggleBaselinePreset`；
- `ToggleBaselineRace`；
- `ToggleBaselineXenotype`；
- `ImportBaselinePreset`。

对应：

- `IUsKernelSettingsSource.cs:53-57`；
- `UsKernelSettingsHost.cs:131-140`；
- `UiCommand.cs` 中只有 `ImportBaselinePreset`，没有 Export 命令；
- `UsPresetListWidget.cs:42-48` 只验证 Import 和树节点切换 action。

`BaselinePresetImporter` 也只有 `Import(...)`，没有反向序列化/写 Def/写配置文件的导出路径。

所以“导出不可用”不是点击热区失效，而是功能尚未实现。当前 UI 设计实际是只读 Def 预设导入器，不是双向预设编辑器。

### 结论 B：当前仓库没有实际预设 Def，因此 Import 也没有对象可操作

`VoicePacksPageModel.BuildBaselinePresets` 只遍历：

```csharp
DefDatabase<UniversalSqueakerTuningBaselineDef>.AllDefs
```

对应：`VoicePacksPageModel.cs:575-627`。

`UsPresetListWidget` 在 `baseline-presets.Count == 0` 时直接返回，不绘制任何预设行和 Import 按钮：

- `MeasureBody`：`UsPresetListWidget.cs:56-61`；
- `DrawContent`：`UsPresetListWidget.cs:94-99`。

当前 `1.6/Defs/` 没有预设 XML，只有 `.gitkeep`。因此生产页面没有实际预设数据时，预设卡片可以存在标题，但内部没有：

- 预设 header；
- race/xenotype 复选树；
- Import 按钮。

右侧帮助面板仍能显示静态 `Import` 帮助文字，因为帮助来自 `UsHelpCatalog`，不代表当前存在可导入的 Def。

### 结论 C：有 Def 时的 Import 路径代码上存在

若第三方或 US 自己提供 `UniversalSqueakerTuningBaselineDef`，当前路径是：

```text
UsPresetListWidget
 -> import-baseline typed action
 -> UsKernelSettingsHost
 -> UsKernelSettingsSource.ImportBaselinePreset
 -> VoicePacksPageModel.ImportBaselinePreset
 -> UniversalSqueakerSettings.ImportBaselinePreset
 -> BaselinePresetImporter.Import
```

导入器会把选中的 race/xenotype 行写入 `actionTuning` 和 `moodTuning`，并触发 resolver rebuild/persistence：

- `VoicePacksPageModel.cs:677-687`；
- `BaselinePresetImporter.cs:31-85`；
- `UniversalSqueakerSettings.cs:351-357`。

但当前没有实际 Def，所以这条路径没有办法在本次游戏截图中被真正操作验证。

## 5. 横向滚动条

### 结论

**横向滚动条高度疑似由 UiKit Kernel Scroll 的内容宽度没有为垂直滚动条预留空间造成。**

这是比单个 Mood 控件更上游的容器问题。

### 证据

Kernel 页面 Schema 使用：

```xml
<Scroll Id="content-scroll" Fill="true" Gap="10">
```

对应：`Source/UniversalSqueaker/UI/Layout.Schema2.xml:8-22`。

UiKit 的 `MeasureScroll` 将滚动内容矩形宽度直接设为传入宽度：

```csharp
ContentRect = new Rect(0f, 0f, width, naturalHeight)
```

对应：`Source/FerriteLib.UiKit/Kernel/UiLayoutEngine.cs:622-674`，尤其是 `:670`。

绘制时使用：

```csharp
VerseWidgets.BeginScrollView(outRect, ref scrollPosition, contentRect)
```

对应：`UiLayoutEngine.cs:205-229`。

当内容高度超过 viewport 时，Unity 会显示垂直滚动条并压缩可用 viewport 宽度；但当前 `contentRect.width` 仍等于原 viewport 宽度，没有预留垂直滚动条宽度。于是内容相对实际可用宽度多出约一个 scrollbar 宽度，Unity 再显示横向滚动条。

截图同时出现右侧垂直条和底部横向条，与这个宽度关系一致。

### 对照证据

旧的 `FerriteVoicePacksPage` 手工滚动路径已经显式处理过该问题：

```csharp
if (contentHeight > contentRect.height + 0.01f)
    layoutWidth = contentRect.width - ScrollbarWidth;
```

对应：`FerriteVoicePacksPage.cs:362-369` 以及多栏路径 `:416-430`。

Kernel UiLayoutEngine 没有等价的横向宽度收缩处理。这说明问题属于 UiKit Scroll 容器契约，而不是预设或 Mood 业务数据。

## 6. Mood Tuning 控件贴边/越界

### 结论

**已确认 `DrawMoodStepper` 的内部加号按钮几何会超出其分配的 stepper rect 4px。**

这是真实的局部几何缺陷；在宽度较紧时会导致控件贴边、间距不足，可能表现为越界或覆盖。截图中的右端贴边现象与该问题相符，但本次没有实机像素测量，因此不把“已经发生裁剪”作为绝对结论。

### 证据

`DrawMoodRow` 将每行划分为三个 stepper group 和一个 Auto 清除按钮：

- `UsScopeTreeWidget.cs:352-378`。

`DrawMoodStepper` 内部顺序为：

```text
label 14
minus button 20 + gap 4
slider
field 40 + gap 4
plus button 20
```

对应：`UsScopeTreeWidget.cs:401-423`。

但 slider 宽度以 `rect.xMax` 作为右边界计算后，最后的 plus button 仍从 `x0 + groupWidth - 16` 开始，宽度 20，结束于：

```text
x0 + groupWidth + 4
```

也就是超出该 stepper 分配矩形的右边界 4px。

`DrawMoodStepper` 最后却只返回分配矩形边界：

```csharp
return rect.x + rect.width;
```

对应：`UsScopeTreeWidget.cs:450`。

因此布局计算认为 group 之间保留了 `MoodGap=6`，实际绘制只剩约 2px，最后一组还会逼近 Auto 按钮和行右边界。

### 容器宽度关系

`UsSectionWidgetBase.BodyWidth` 和 `DrawCard` 都使用卡片左右 padding 后的 body 宽度：

- `UsSectionWidgetBase.cs:93-101`；
- `UsScopeTreeWidget.cs:83-99` 与 `:117-190`。

所以当前主要问题不是明显的 body width 双重计算，而是 stepper 内部控件总宽度超过其 group 分配宽度，以及外层 Scroll 横向宽度契约缺失。两者叠加后更容易在游戏内表现为“控件超过容器”。

## 7. 问题分类与修复边界

| 问题 | 状态 | 所属层 | 是否已确认根因 |
| --- | --- | --- | --- |
| All 无法清除 Race/Xenotype | 功能缺陷 | US FilterBar + PageModel | 是 |
| Checklist 包被 placeholder 过滤 | 功能缺陷 | US Kernel checklist input state | 是，高置信 |
| 导出不可用 | 缺失功能 | US preset API/UI | 是，当前不存在 |
| Import 没有可操作对象 | 数据/产品配置缺口 | `1.6/Defs` + preset projection | 是 |
| Kernel 横向滚动条 | 容器布局缺陷 | FerriteLib.UiKit Scroll | 高置信 |
| Mood stepper 右端超出 group rect | 控件几何缺陷 | US ScopeTreeWidget | 是 |
| 真实 catalog 是否仍返回包 | 实机数据项 | Catalog/Def 加载 | 本次未单独重采样 |
| 作者筛选是否残留 | 状态项 | PageState/FilterBar | 截图不足，待复核 |

## 8. 本次明确不做的工作

- 不修改源代码；
- 不增加预设 Def；
- 不设计或实现导出协议；
- 不打包；
- 不以“退出重开有效”作为修复证据；
- 不把静态 Help 面板显示的 `Import` 文字当成 Import 功能已存在的证据。

下一次进入修复阶段时，应先做最小、可验证的切片：

1. All action 同时清除 Race/Xenotype filter；
2. 搜索框使用真实空字符串绘制，placeholder 只作为视觉层，不写回 binding；
3. UiKit Scroll 在垂直滚动时为内容宽度预留 scrollbar；
4. 收紧 Mood stepper 的内部宽度，使所有子矩形严格包含在 group rect 内；
5. 重新决定预设产品契约：只提供可用的 Import + 示例 Def，还是正式增加 Export；两者不是同一项修复。
