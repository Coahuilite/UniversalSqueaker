# Review S4-03 — Visual Skin / Neutrality 功能区

## 结论

已只读核对 HEAD `bf7342f` 下 Visual Skin / Neutrality 相关重点文件：

- `Source/UniversalSqueaker/UI/Visuals/*.cs`
- `Source/UniversalSqueaker/UI/Components/*.cs`
- `Source/UniversalSqueaker/UI/Widgets/*.cs`
- `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/VoicePacksPage.cs`
- `Source/FerriteLib.UiKit/Widgets/*.cs`

核心验收点结论：

1. **UsVisualTokens 与 P0 一致**：Base/Panel/Raised/Hover/Selected/Warning/Success/Danger、TextPrimary/TextSecondary/TextOnGold/TextOnDanger/TextDisabled、AccentGold/Border/BorderStrong 以及 `AccentGoldAlpha20` 的值均与 `docs/ui-visual-modernization-zh.md` §2.2 一致；间距 2/4/6/8/10、行高 22/26/32/50 也一致。FerriteLib `Palette` 与 US 令牌同值，保持中性边界。
2. **US 仍存在直接 `new Color(...)` 画表面**：`FerriteVoicePacksPage.cs:275` 的左导航选中底色是硬编码 `new Color(.20f, .17f, .10f, .8f)`，违反 P1/P7 验收“US widget 不再直接 new Color 画表面”。另在开发诊断面板 `SqueakDiagnosticsPanel.cs:322`（行底色）和 `:274`（分隔线）仍有硬编码颜色。
3. **UiPalette/SectionFrame 转发本身安全**：均为只读静态转发，无状态、无额外行为；但仍有新页面壳代码引用旧入口（`FerriteVoicePacksPage.cs:261-262`），未完全迁移到 `UsVisualTokens`/`UsSurface`。
4. **FerriteLib 中性通过**：对 `Source/FerriteLib.UiKit` 全部源码搜索 `UniversalSqueaker`/`SqueakyRatkin`/`Ratkin`/`SR_`/`US_` 无命中。
5. **Guard 恢复顺序正确，Ferrite once-log 未按会话清理**：`UsGuard`/`FerriteGuard` 都在 catch 后先恢复 `Text.Font`/`Text.Anchor`/`GUI.color` 再执行 fallback；`UsGuard` 在 `VoicePacksPage.BeginSession/EndSession` 清理，但 `FerriteGuard.ResetSessionLog()` 没有任何调用点。
6. **P1 未发现 Measure/命令流/布局高度回归**：P1 commit `b166ca9` diff 中所有 Measure 只是包上 `UsGuard.MeasureOrFallback` 且 fallback 高度相同，命令 payload 未变；未发现 P1 引入的高度或命令流改变（P6 的 `ScopeTreeWidget` 窄屏 Measure/Draw 漂移属另一功能区，已在 R2 报告）。
7. **皮肤状态一致性未完全收口**：左导航 active、复选框 checked、help hover、ScopeTree 段按钮/清除钮、SearchField、PresetList 等仍与 P0 状态表存在偏差，P7 “final skin sweep” 实际只提交了 vanilla fallback 页面，没有完成视觉收口。

未发现 Blocker。共 1 个 Major、6 个 Minor、3 个 Nit。

## 发现

### Major

#### 1. 左导航仍手写表面色且 active 态不符合 P0；新页面壳仍引用旧调色板入口

- 文件：`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs:255-285`（尤其 `:261-262`、`:275`）

`DrawNav` 是 S4-Nav 之后新增的页面壳代码，但没有迁移到现代 skin：

- `:261-262` 用 `UiPalette.Panel` + `SectionFrame.DrawBorder` 画导航栏，而不是 `UsVisualTokens`/`UsSurface`。
- `:275` 直接 `Widgets.DrawBoxSolid(buttonRect, new Color(.20f, .17f, .10f, .8f))` 画 active 底色，违反“US widget 不再直接 new Color 画表面”的硬验收。
- active 态只有半透明硬编码底色，随后 `Widgets.ButtonText(..., drawBackground:false)`；没有 P0 要求的 `Selected` 面、`AccentGold` 边框、`TextOnGold` 文本、左侧 4px 金色竖条（`docs/ui-visual-modernization-zh.md` §3.7）。

这是玩家最常看到的页面外壳，且正是 R3 功能区的主要验收点，因此列为 Major。

### Minor

#### 1. 现代复选框 checked 边框不是 AccentGold

- 文件：`Source/UniversalSqueaker/UI/Visuals/UsSurface.cs:115`

```csharp
Color border = value || hovered ? UsVisualTokens.BorderStrong : UsVisualTokens.Border;
```

P0 §3.3 规定 checked 态为 `Selected` 面 + `AccentGold` 边框；当前 checked 使用 `BorderStrong`，与选中语义不一致。该 helper 被 `BasicTuningWidget`、`CameraIndicatorWidget`、`VoicePackRow` 共用。

#### 2. Help `?` 的 hover 态被画成 active 态

- 文件：`Source/UniversalSqueaker/UI/Components/HelpToggle.cs:21-26`
- 文件：`Source/UniversalSqueaker/UI/Widgets/UsHelpButton.cs:24-35`

P0 §3.10 要求 hover 为 `Hover` 面 + `BorderStrong` + `TextPrimary`，active 才是 `AccentGold` 边框/文字。当前两处把 hover 与 active 一样处理成 `AccentGold` 边框和 `AccentGold` 文字，hover 和展开态无法区分。

#### 3. ScopeTree 的段按钮与 mood “Auto” 清除钮未按 P0 danger/focus 状态绘制

- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:389-394`（Auto 清除钮）
- 文件：`Source/UniversalSqueaker/UI/Widgets/ScopeTreeWidget.cs:445-455`（私有 `DrawSegment`）

私有 `DrawSegment` 对所有状态都强制 `UsVisualTokens.AccentGold` 边框（`:451`），非 off 文本也统一用 `AccentGold`（`:452`），而 P0 §3.2 规定 normal 为 `Raised`/`Border`/`TextSecondary`、hover 为 `Hover`/`BorderStrong`/`TextPrimary`、danger 为 `Danger`/`Danger`/`TextOnDanger`。Auto 清除钮同样用 `Selected`/`Danger` 面 + `AccentGold` 边框 + `TextSecondary`，不是 danger 的 `Danger`/`Danger`/`TextOnDanger`。这会与 FilterBar 使用的 `UsSurface.DrawSegment` 观感不一致。

#### 4. FerriteGuard 的 once-log 没有按设置会话清理

- 文件：`Source/FerriteLib.UiKit/Widgets/FerriteGuard.cs:15`
- 文件：`Source/UniversalSqueaker/UI/VoicePacksPage.cs:12-25`

`FerriteGuard.ResetSessionLog()` 已定义但全仓库无调用点；`VoicePacksPage.BeginSession/EndSession` 只清理 `UsGuard`。因此 Ferrite 内置 widget（`input/mode-card`、`input/mode-row`）的 fallback 警告在整个进程生命周期内只记一次，而不是“每设置会话一次”，与 P0 §5.3 的表述不一致。

#### 5. SearchField 表面用了 Base 而非 P0 的 Panel

- 文件：`Source/UniversalSqueaker/UI/Components/SearchField.cs:24`

P0 §3.5 文本输入框 normal 为 `Panel` + `Border`；当前 `UsSurface.DrawSurface(rect, UsSurface.SurfaceKind.Base)` 画成页面最底 `Base`，在页面内层次感偏弱。

#### 6. PresetList 仍使用原版 Checkbox 和 `Color.white`，未完全令牌化

- 文件：`Source/UniversalSqueaker/UI/Widgets/PresetListWidget.cs:164`、`:193`、`:199`、`:215`、`:222`

预设树头部/race/xenotype 行标签仍用 `Color.white`，复选框仍用 `Widgets.Checkbox`，没有切到 `UsVisualTokens`/`UsSurface.DrawCheckbox`；与 P0 §3.1/§3.3 的现代行/现代复选框不一致。

### Nit

#### 1. `UsSurface.DrawSegment` / `DrawHeader` 恢复锚点时写死 `UpperLeft`，未恢复调用前值

- 文件：`Source/UniversalSqueaker/UI/Visuals/UsSurface.cs:106-107`、`:141-142`

两处保存了 `oldColor`/`oldFont`，但没有保存/恢复 `Text.Anchor`，而是直接设为 `TextAnchor.UpperLeft`。在非默认锚点上下文中调用会改变调用方状态。

#### 2. 开发诊断面板仍残留硬编码颜色

- 文件：`Source/UniversalSqueaker/Diagnostics/SqueakDiagnosticsPanel.cs:274`、`:322`

`:322` 用 `new Color(.08f, .075f, .067f, .65f)` 画隔行底色，`:274` 用 `new Color(UiPalette.Gold.r, UiPalette.Gold.g, UiPalette.Gold.b, .25f)` 画分隔线。该文件不在 R3 重点清单内，但若 neutrality/皮肤 grep 覆盖整个 US 源码，仍不算干净。

#### 3. 页面级 catch 未先恢复 GUI 状态再画 vanilla fallback

- 文件：`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs:192-203`

组件级 guard 会恢复状态，但 `FerriteVoicePacksPage.Draw` 外层 catch 直接调用 `VanillaVoicePacksPage.Draw(rect)`，没有像 `UsGuard`/`FerriteGuard` 一样先恢复 `Text.Font`/`Text.Anchor`/`GUI.color`。若异常发生在自绘代码尚未恢复状态处，兜底页可能以错误字体/颜色渲染。

## 建议

1. **优先迁移左导航**：将 `FerriteVoicePacksPage.DrawNav` 改为 `UsSurface`/`UsVisualTokens`；active 态使用 `UsSurface.DrawRowSurface` 或按 P0 §3.7 手绘 `Selected` 面 + `AccentGold` 边框 + 左侧 4px 金色条 + `TextOnGold`；同时删除 `UiPalette`/`SectionFrame` 在新页面壳中的引用。完成后再评估是否可移除两个兼容转发类。
2. **修正共享 helper 状态表**：`UsSurface.DrawCheckbox` checked 边框改 `AccentGold`；Help hover 改 `BorderStrong`/`TextPrimary`；`SearchField` 表面改 `Panel`；`UsSurface.DrawSegment`/`DrawHeader` 保存并恢复原 `Text.Anchor`。
3. **统一 ScopeTree 段按钮**：直接用 `UsSurface.DrawSegment`，或让私有 `DrawSegment` 严格按 P0 §3.2 状态表（normal/hover/selected/danger），并把 mood “Auto” 清除钮的 danger 态改为 `Danger` 面 + `Danger` 边框 + `TextOnDanger`。
4. **接通 FerriteGuard 会话清理**：为 FerriteLib 提供可被 US 会话调用的 reset 入口（例如公开 `ResetSessionLog` 或经 `CoreWidgetRegistrar` 暴露），或在文档中明确库内 once-log 是“进程级”而非“设置会话级”；若按 P0 验收则必须接上。
5. **完成 PresetList/衰减编辑器/滑条收口**：PresetList 改用 `UsVisualTokens`/`UsSurface.DrawCheckbox`；衰减曲线补充 `AccentGoldAlpha20` 填充（当前令牌存在但未使用）；全局音量滑条如需符合 P0，补充扁平轨道/金色游标皮肤或明确记录为遗留偏差。
6. **补页面级状态恢复**：在 `FerriteVoicePacksPage.Draw` 外层 catch 进入 `VanillaVoicePacksPage.Draw` 前，先恢复 `Text.Font = GameFont.Small`、`Text.Anchor = TextAnchor.UpperLeft`、`GUI.color = Color.white`。
7. **清理 Diagnostics 硬编码或调整 grep 范围**：将 `SqueakDiagnosticsPanel` 的 `new Color` 底色/分隔线改为令牌，或把该 dev-only 面板明确排除在“US widget 皮肤 grep”范围外，避免验收歧义。
