# US UI/UX 设计调研

> 日期：2026-08-30
> 触发：维护者希望探索“Camera+ 三栏布局为什么成功”以及是否存在相关 UI/UX 设计指导。
> 方法：联网搜索 + `gh`（GitHub CLI）搜索/读取源码。

## 结论摘要

- Camera+ 的设置界面是典型的 **三栏 master-detail + 上下文帮助** 布局：
  - 左栏：topic 导航（含 “All”）
  - 中栏：当前 topic 的内容（可滚动）
  - 右栏：固定帮助面板
- 帮助之所以有天然视觉引导，是因为它在固定第三栏中持续可见；玩家不需要主动点击/悬停去“找”帮助。
- 没有找到 RimWorld 社区通用的、成文的 UI/UX 设计规范；**Camera+ 本身是目前生态内最强的同类型参考实现**。
- 通用 UI/UX 资料里有几个可借鉴的模式，尤其关于 **窄屏降级** 和 **上下文帮助**。

## Camera+ 源码证据

- 仓库：<https://github.com/pardeike/CameraPlus>
- 关键源码：`Source/Settings.cs`
- 设计事实：
  - `topics` 数组定义左侧导航项，包含 `All`、`Zoom`、`Movement`、`Audio`、`Camera`、`Labels`、`Markers`、`Animals`、`Edges`、`Appearance`、`Keyboard`。
  - `DoWindowContents` 将窗口拆为三栏：左导航、中间设置内容、右侧帮助。
  - `DrawTopicNavigation`：每项可选图标，选中/悬停有半透明填充，点击后重置滚动位置。
  - `DrawSettingsContent`：按 `selectedTopic` 过滤 group；`All` 显示全部 group；每个 group 有标题。
  - `DrawHelp`：右侧面板显示当前 topic 或悬停项的帮助文本，不依赖点击。
- README 也明确说明：
  > Use **All** to see everything at once, or pick a left-side topic ... for a shorter page.
  > For detailed configuration, use the in-game settings menu and its help panel.

## 通用 UI/UX 资料（可借鉴）

### 1. 三栏/上下文帮助模式

- 三栏设置界面本质上是 **master-detail + contextual help**：
  - 左栏是 master（导航）
  - 中栏是 detail（内容）
  - 右栏是 context（解释/帮助）
- 参考：
  - [LukeW — UI Pattern: Unified Settings & Tutorial](https://lukew.com/ff/entry.asp?936)：设置与教学/帮助统一，减少玩家跳出。
  - [Contextual placement patterns](https://raw.githubusercontent.com/rampstackco/claude-skills/e11b5eaa26ad42009788eaaad4228998507f44e5/skills/interactive-product-tour/references/contextual-placement-patterns.md#1)：帮助/提示放在用户当前关注点附近，而不是藏在按钮后面。

### 2. 窄屏降级

- 三栏在窄屏不能直接硬缩放，需要降级：
  - 右栏帮助优先隐藏/折叠。
  - 左栏导航可降级为顶部 tab 或抽屉（drawer）。
  - 内容区保持单列连续滚动。
- 参考：
  - [fix(desktop): prevent settings sidebar from flipping to horizontal tabs on narrow windows / 修复窄窗口下设置侧边栏突变为横向排列 — DeepSeek-Reasonix PR #6019](https://github.com/esengine/DeepSeek-Reasonix/pull/6019#1)：讨论设置侧边栏在窄窗口下的行为。
  - [Mobile Game Settings UX Inspired by RPCS3](https://reactnative.live/designing-emulation-like-config-uis-for-mobile-games-lessons)：设置页在有限宽度下的组织与降级思路。

## 对 US UI 的启示

1. **宽屏（≥1200px）**：保留三栏：
   - 左：功能块导航
   - 中：当前功能块连续滚动内容
   - 右：固定帮助面板
2. **窄屏**：
   - 隐藏右侧帮助面板。
   - 左导航降级为顶部 tab / 抽屉。
   - 每个功能块保持独立滚动。
3. **帮助入口**：
   - 宽屏以右侧面板为主，不需要内联 `?` 按钮。
   - 窄屏再考虑轻量内联折叠帮助。
4. **组合式功能块**：
   - 与三栏结构天然契合：导航项 = 功能块，内容 = 功能块组合，帮助 = 功能块说明。

## 参考链接

- Camera+ GitHub：<https://github.com/pardeike/CameraPlus>
- LukeW — Unified Settings & Tutorial：<https://lukew.com/ff/entry.asp?936>
- Contextual placement patterns：<https://raw.githubusercontent.com/rampstackco/claude-skills/e11b5eaa26ad42009788eaaad4228998507f44e5/skills/interactive-product-tour/references/contextual-placement-patterns.md>
- Mobile Game Settings UX Inspired by RPCS3：<https://reactnative.live/designing-emulation-like-config-uis-for-mobile-games-lessons>
- DeepSeek-Reasonix settings sidebar narrow PR：<https://github.com/esengine/DeepSeek-Reasonix/pull/6019>
