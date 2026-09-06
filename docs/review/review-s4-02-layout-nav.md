# Review S4-02 — Layout / Navigation / Footer 功能区

## 结论

已核对 HEAD `46b7d16` 下 Layout / Navigation / Footer 相关重点文件：

- `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/Layout.xml`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageState.cs`
- `Source/UniversalSqueaker/UI/Model/UiCommand.cs`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`
- `Source/UniversalSqueaker/UI/Model/VoicePacksViewState.cs`
- `Source/UniversalSqueaker/UI/Widgets/UsFooterWidget.cs`
- `Source/UniversalSqueaker/Mod.cs`

核心验收点结论：

1. **三页签切换 / ActiveTab**：通过。`VoicePacksPageState.ActiveTab` 默认 `"Basic"`，`Reset()` 回 `"Basic"`；`FerriteVoicePacksPage.BeginSession()` 在会话首帧 `Reset()`；`ExecuteSetActiveTab` 只接受 `Basic/Tuning/Packs` 并写回 state。
2. **右侧滚动与 sticky footer 分离**：通过。`contentRect.height = rect.height - FooterHeight`，`Widgets.BeginScrollView` 只包住内容区；footer 在 `EndScrollView()` 之后按 `footerRect` 单独绘制，因此不随内容滚走，且始终位于右侧可视底部。
3. **内容溢出/裁剪**：**发现 1 个 Major**。窄宽度下 `ScopeTreeWidget` 的竖排层按钮 Measure/Draw 高度不一致，会重叠后续内容并使滚动高度低估（详见发现 1）。
4. **SetActiveTab 命令流**：通过。左导航由 `FerriteVoicePacksPage.DrawNav` 直接 emit `UiCommandKind.SetActiveTab`，经 `businessCommands` → `VoicePacksPageModel.ExecuteAll` → `ExecuteSetActiveTab` → `state.ActiveTab`；`UsWidgetCommandAdapter` 也预留了 `SetActiveTab` 映射，Ferrite widget 路径同样可走通。
5. **BuildIdentity / SaveStatus / IsDirty 投影与 footer 颜色**：通过。`VoicePacksPageModel.BuildView` 投影三键，`FerriteVoicePacksPage` 写入 view dict，`UsFooterWidget` 按 `Saving/Dirty=AccentGold`、`Failed=Danger`、其余 `TextSecondary` 渲染；与 Mod.cs 保存状态机一致。
6. **Layout.xml 页签归属**：通过。Basic = mode/global-volume/attenuation/basic-tuning/camera-indicator；Tuning = scope-tree/preset-list；Packs = filter-bar/race-layer/xenotype-layer/checklist。title/banner 无 Tab 作为全局页眉合理。

未发现 Blocker。共 1 个 Major、2 个 Minor、2 个 Nit。

## 发现

### Major

#### 1. `ScopeTreeWidget` 窄屏竖排层按钮的 Measure/Draw 高度不一致，内容重叠并低估滚动高度

- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:56-89`、`:141-142`、`:199-214`、`:230-235`

`LayerRowHeightFor` 在按钮放不下时只返回 `LayerRowHeight + 22f`（约 50px），但 `DrawCore` 仍以 `LayerRowHeight`（28px）调用 `DrawLayerRow`，随后仅按 `LayerRowHeight + Gap`（34px）推进 y。`DrawLayerRow` 的 stacked 分支实际从 `rect.y + 22f` 开始连续绘制 3 个 `20px` 高按钮，每个间隔 `2px`，底部到达 `rect.y + 86f` 左右。也就是说：

- 该行实际绘制高度约 86px；
- Measure / LayoutEngine 只分配约 50px；
- 后续 domain row、scope header、scope rows、mood rows 会与竖排按钮重叠；
- 页面总内容高度被低估，滚动条范围偏小，滚动到底部时最后的 mood/scope 行可能被裁切。

触发条件：内容区 innerWidth 小于约 316px 时进入 stacked 分支（考虑左侧导航 140px 与 padding，约窗口宽度 < 472px 即可能触发）。这正是 P6 “窄屏响应式加固”要求覆盖的 480/320/240 宽度区间，因此属于布局验收缺口。

建议：让 stacked 分支的 Measure 与 Draw 使用同一高度公式，例如 `LayerRowHeight + (ButtonHeight + RowGap) * 3`（或按实际绘制布局计算），并把该计算高度同时用于 `DrawLayerRow` 的 rect 与 `DrawCore` 的 y 推进；同时补一条窄宽度 UiLogic 测量断言，防止 Measure/Draw 再次漂移。

### Minor

#### 1. Footer 高度常量不统一，`UsFooterWidget.Measure` 实际未被使用

- 文件：`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs:36`、`:131`、`:163`
- 文件：`Source/UniversalSqueaker/UI/Widgets/UsFooterWidget.cs:17`、`:29-33`

页面预留 `FooterHeight = 28f`，而 `UsFooterWidget` 自身 `FooterHeight = 24f`，`Measure()` 也返回 24f。footer 目前由 `FerriteVoicePacksPage.DrawFooter` 手工用 28px rect 绘制，因此 `UsFooterWidget.Measure()` 不会参与布局，内容区实际被 footer 占掉 28px 而不是 widget 自报的 24px；文档中还有 26px 的规格（`docs/ui-visual-modernization-zh.md:233`、`:244`）。

这不是当前可见裁剪，但三处常量不一致会让后续维护者误用：若未来把 footer 放回 LayoutEngine 或调整高度，布局会与 widget 测量脱节。建议以 `UsFooterWidget` 作为唯一高度来源，或抽出一个公共常量，并统一到规格值。

#### 2. 切换页签时不清空滚动位置，可能在新页签打开时停留在中间/底部

- 文件：`Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs:218-226`
- 文件：`Source/UniversalSqueaker/UI/Model/VoicePacksPageState.cs:20`、`:43`

`ExecuteSetActiveTab` 只更新 `ActiveTab`，没有重置 `ScrollPosition`。由于三个页签共用同一个 `VoicePacksPageState.ScrollPosition`，用户在“包清单”滚动较深后切到“调音”时，会带着旧滚动偏移打开；若目标页签较短，`ClampScroll` 会把滚动钳到底部而非顶部。当前 spec 只要求 ActiveTab 在会话 Reset 回 Basic，未明确要求切页签回顶，但“三页签切换正确”的直觉体验建议每次切页签回到顶部。

建议在 `ExecuteSetActiveTab` 成功切换后执行 `state.ScrollPosition = Vector2.zero`（或按页签分别保存滚动位置）。

### Nit

#### 1. `Layout.xml` 中的 footer 是“死声明”，实际 footer 由代码手工绘制

- 文件：`Source/UniversalSqueaker/UI/Layout.xml:21`
- 文件：`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs:207-233`、`:287-293`

`Layout.xml` 声明了 `<Widget Id="footer" Kind="us/footer" Tab="Footer" />`，但 `GetEngine` 会把所有 `Tab != activeTab` 的 widget 移除，因此 footer 永远不会通过 LayoutEngine 渲染；实际渲染走 `DrawFooter` 手工创建 spec/widget。这造成 XML 清单与真实布局不一致，未来若有人调整 Tab 过滤或忘记手工绘制，footer 可能消失或重复。建议从 `Layout.xml` 移除 footer 声明（因为 sticky footer 本就不属于滚动布局），或在代码中明确注释它是“声明式参考，实际由 DrawFooter 绘制”。

#### 2. 左导航命令直接走 business command，未经过 `UsWidgetCommandAdapter` / Kit 命令层

- 文件：`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs:255-285`

`DrawNav` 直接 `addCommand(new UiCommand(UiCommandKind.SetActiveTab, ...))`，而其它 US widget 的命令统一经 `UsWidgetCommandAdapter.For(emit)` 转成 Kit `UiCommand` 再翻译回 business command。S4-Nav 任务书允许“由 `FerriteVoicePacksPage` 直接绘制三枚按钮；点击 emit SetActiveTab”，所以这不是功能缺陷，命令最终仍完整到达 model/state。但从命令流一致性看，可考虑把导航也做成 `us/nav` widget 或至少统一经过 adapter，减少两条命令路径。

## 建议

1. **优先修复 ScopeTree 窄屏 Measure/Draw 漂移**：统一 stacked 层按钮的实际高度；让 `DrawLayerRow` 使用 Measure 计算出的行高，并让 `DrawCore` 按同一高度推进 y；在 UiLogicTests 中增加 480/320/240 宽度下的 ScopeTree 测量断言。
2. **统一 footer 高度来源**：删除 `FerriteVoicePacksPage.FooterHeight` 魔法值，改为读取 `UsFooterWidget` 的固定高度（或抽公共常量），并与视觉规格（26px）对齐；同时移除 `Layout.xml` 中永远不会被 layout 渲染的 footer 声明，或补注释说明它是手工绘制的参考节点。
3. **页签切换回顶**：在 `ExecuteSetActiveTab` 内把 `ScrollPosition` 重置为零，或为每个页签维护独立滚动位置；如果产品希望保留滚动位置，请在文档中明确该行为。
4. **保持单命令路径**：如后续继续扩展导航，建议把左导航迁入 `us/nav` widget 或统一走 `UsWidgetCommandAdapter`，避免 native 与 Ferrite 两条命令路径长期并存。
5. **回归验证**：修复后建议在游戏内最小宽度窗口手动检查 Basic / Tuning / Packs 三个页签：footer 始终可见、滚动到底部无裁切、Tuning 页竖排层按钮不与 scope/mood 行重叠。
