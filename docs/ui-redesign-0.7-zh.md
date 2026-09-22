# US UI 重设计（0.7 线 · DarkGold 三栏 + 文件驱动）

> 状态：设计冻结稿，未实施。基线 = FerriteLib `0.7.x` @ `bce1ba4c88e557ee91fc1fbe87391b5107a2ff32`
> （Api 0.7.0，含 Batch 1 与 P1-E 两批；初稿写在 `d82a3ca` 上，事实面已在 §0 逐行复核）。
> US pin `[0.7.0, 0.8.0)`。本文是设计规格，不是实现记录。

## 0. 本次 FL 更新带来的新事实（决定设计的关键项）

> **复核（2026-09-21，task-27 第 0 步）：载体 = FL HEAD `bce1ba4c88e557ee91fc1fbe87391b5107a2ff32`（Api 0.7.0，Release / 249 344 B / 无 PDB）。**
> 本节初稿写在 `d82a3ca` 上；`git -C ../ferritelib log --oneline d82a3ca..bce1ba4` = **14 个提交，其中代码提交 8 个**。
> 本节现在的口径是 **「原文主张 / 是否仍成立 / 依据」**，不再只列变化。**§0.2 与 §0.4 是两条改变结论（而不是措辞）的更新，必须连带重裁 §4.1 与 §5.4。**
> FL 仓内路径一律相对于 `../ferritelib/`。

### 0.1 第 0 版本节的 5 行，逐行复核

| 原文主张（逐字保留） | 现在是否仍成立 | 依据 |
|---|---|---|
| 放置词汇（Batch 1）：`AlignX/OffsetX/AlignY/OffsetY`；`origin + fraction*(parentSpan-selfSpan) + offset`；`Overlay` 是唯一双轴放置容器，flow 子元素只接受**交叉轴 + 像素微调**；模板根拒绝全部四个名字 | **成立，本轮未变** | `Kernel/UiPlacement.cs:138-153`（既非 placement 容器也非 flow → creation 期拒绝）、`:173-190`（flow 只收交叉轴）、`:236-241`（`AlignX="Stretch"` 与显式 `Width` 互斥）；属性名门 `Kernel/UiLayoutEngine.cs:110` 与 `:118`；模板容器表 `:107-111` 含四个名字 |
| 密度触达布局（CP-0，破坏性）：容器未声明 `Padding`/`Gap` 时回落 `UiTheme.Geometry`（**两套 palette 均为 6/6**）；显式属性仍优先，`Padding="0"` 保留旧结果 | **成立，但原文的「6/6」不完整** | `Kernel/UiLayoutEngine.ParsePadding:2557-2563`（未声明 → `theme.Geometry.Padding`）、`ReadGap:2594-2599`；`Padding="0"` 的逃生口写在 `:2546-2549`。基线 `UiGeometry.Default = (Padding 6, Spacing 4, Gap 6, RowHeight 28, Hairline 1)`（`Kernel/UiTheme.cs:65`）；**US 清单已把 `RowHeight` 覆盖为 24**（`Source/UniversalSqueaker/UI/Layout.Schema2.xml:9`），所以 US 的实际 page 密度是 `(6, 4, 6, 24, 1)`。可声明的 metric 只有 `Padding/Spacing/Gap/RowHeight/Hairline`（`Kernel/UiStyleDocument.cs:515-524`） |
| 语气词汇收紧（D6）：可写 `Tone` 只剩 `Neutral/Success/Warning/Danger`；`Active`/`Disabled` 成为状态（本 minor 内仍是重定向，0.8 起拒绝） | **成立** | `Kernel/Widgets/AtomVocabulary.ParseTone:267-`（只认四个 meaning；数值不被当作 tone；`Active`/`Disabled` 重定向到它一直表示的状态 + 一条去重 note） |
| 单一强调色（D7）：`UiTheme.HoverPoint` **删除**，改为只读派生 `UiTheme.AccentHover` | **成立** | `Kernel/UiTheme.cs:159-168`（每次读派生，`HoverLift = 0.3`，alpha 保持）；`Kernel/UiStyleDocument.cs:496` 与 `Kernel/UiStyleResolver.cs:246`（文档里写 `HoverPoint` 报未知 token，永不进 switch） |
| 仍缺（不在本线）：chrome 动作插槽、`input/text-field`、`input/mode-row` 的 `Description1..8` 仍不绘制、`container/tree` 行内无子控件、页面模型无文本对齐轴、**无宽度绑定（只有 `VisibleKey` 这类 bool）** | **2 条已不成立、1 条须改写、3 条仍成立** | 逐项见 §0.1.1 |

#### 0.1.1 「仍缺」那一行的逐项判定

| 项 | 判定 | 依据 / 说明 |
|---|---|---|
| chrome 动作插槽 | **仍缺（但已不影响本设计）** | `Kernel/UiWindowHost.cs` 未改，chrome 仍只有 Title/Subtitle/Close…；维护者裁定 (5)（2026-09-20）已改为「帮助按钮 = 固定页头」，故 §4.2 的落点不变 |
| `input/text-field` | **已不成立——已发** | `Kernel/Widgets/TextFieldWidget.cs`（提交 `d687032`）；US 已在用（`Layout.Schema2.xml:61`）。它是有身份的单行字段：草稿/焦点在元素上；标签集是 `Label/LabelKey`，`Placeholder` **故意不在**标签集里（B8 口径） |
| `input/mode-row` 的 `Description1..8` 仍不绘制 | **字面成立，但角色已变（这句须改写）** | 它仍不画（`Kernel/Widgets/InputModeRowWidget.cs:156-163`），但现在是**选项级 hover 帮助文本**：`DescriptionN` → `UiSession.ClaimHover`，由消费侧的帮助面板读出（`Kernel/Widgets/OptionHelp.cs:16`）。所以它不是死属性；B8 只剩「词表半」（移除 vs 绘制）待维护者 |
| `container/tree` 行内无子控件 | **仍成立** | B9 已裁本线不做；Route A（给 tree 可选按行模板）维护者倾向但明确不在本线 |
| 页面模型无文本对齐轴 | **仍成立** | `AlignX` 是 **placement**（摆整个元素），不是文本对齐轴（B10） |
| **无宽度绑定** | **已不成立——已发（B5 `WidthKey`）** | 见 §0.2。**§4.1 的结论因此需要重新裁定** |

### 0.2 ★ `WidthKey` ⇒ §4.1 的结论要重新裁定，不是重新措辞

**已发能力（提交 `3df7bf5`，P1-E batch 1）**：`WidthKey` 是 `VisibleKey` 的**数值兄弟**。

- 位置：`Kernel/UiLayoutEngine.cs:150`（属性名常量）、`:2200-2244`（`TryFixedWidth` / `TryBoundWidth`）；属性门：容器 `Kernel/UiHost.cs:909`、widget `:884`。
- 语义：**静态声明先赢**（与 `Visible/VisibleKey` 同一优先级形状）——写了可用 `Width` 时不读 `WidthKey`；`WidthKey` 只在没有可用 `Width` 时被问，经 float 值绑定回答。
- 失败形状：键缺失或绑到别的类型 → **fail-soft + 一条去重 appearance note**（不是每帧抛），静态答案原样保留。
- 重排：经 `RecordDeclaredKeys` 注册（`:455`），所以对该键的公告**只重排声明它的节点**。

**为什么这要重裁**：§4.1 当初放弃「右栏可调」的**唯一技术理由是「manifest 的 `Width` 是字面量，可调宽度在当前公开面不可表达」**。该理由已消失，而维护者裁定 (4) 的「两档 collapse / 320」正是在**没有它**的时候定的。所以现在要重裁的是**产品形态**：两档 vs 可调。

> **[2026-09-21 事实更正，U3 量价]** 下面 A 方案里那句「运行期改窗在 Verse 侧没有可靠的公开通路」**只对「声明式缝」成立，对「能不能改窗」是错的**：`UiWindowHost` 暴露 `windowRect`，而 US **已经在运行期改窗**——`UniversalSqueakerSettingsWindow.ApplyDrawerWidth()`（`:107-125`，由 `BeforeDraw` 在 `:162` 调用）在**抽屉状态边沿**上重写它：按 `SettingsWindowWidth` 算目标宽、以自身中心重定位、夹进屏幕，且只在状态变化那一帧动手（所以不与玩家拖动或每帧重排打架）。⇒ 本线**不需要「按最大宽度预留」**；缺的只是引擎的**声明式**改窗缝（`InitialSizePolicy` 只决定初始尺寸）。这条更正直接改变 (乙1) 两条候选的价签：**两者都不必为窗口预留付代价**，代价主战场转移到「谁拥有帮助列的宽度这个数字」。
> 
> **[同一次更正] §4.2 与本条的结论不受影响**（帮助按钮仍在固定页头），但 §4.1 里 (a)/(c) 两个选项按此作废，(b)（内容列被挤）变成唯一仍然存在的那一条 —— 而它正是 (乙1) 要解决的。
> 
**两条路及其真实代价**

- **A｜采纳 `WidthKey`，右栏拖拽可调。** 可表达面：`<Scroll Id="help-scroll" WidthKey="help-drawer-width" MinWidth="260" MaxWidth="480">` + host 侧 `BindValue<float>`；拖拽手柄的命中与几何属 `us/*` 自有契约（US 自绘）。
  **代价不在右栏，在窗口**：今天 `WindowChromeLayout.DrawerWidthDelta = HelpDrawerWidth + BodyRowGap` 是**编译期常量**，而窗口尺寸在 `Verse.Window` 构造时确定（引擎不提供「按内容改窗」的缝，本稿 §3 自己也这么写）。运行期宽度可调 ⇒ 三选一，且都是产品决策：
  (a) 窗口按**最大**宽度预留 → 抽屉关着时右侧留一条最多 ~480px 的空白；
  (b) 接受把内容列挤到下限；
  (c) 运行期改窗 → Verse 侧没有可靠的公开通路（这正是 FL-18 被裁「不会来」的那一类缝）。
  另需新 lane：宽绑定 → 声明宽；公告 → 重排；非 float → fail-soft + 一条 note；`MinWidth`/`MaxWidth` 夹取。
- **B｜维持两档（收起 / 320），把 `WidthKey` 记为「已具备、本线不采纳」。** 代价：长条目仍换行（`MinWidth="260"` 只是保底不是交互）。收益：窗口策略不变（常量成立）、零新增 lane、S3 的几何面小得多。

**本稿的记录**：`WidthKey` 只移除了「不可表达」这个**技术**理由，**没有增加「可调更好」的证据**；而 320 相对今天的 176 已经是 +82%，「读得下」这个需求大半被两档满足。

> **维护者裁定（2026-09-21）：走 B。** 帮助栏维持两档（收起 / 320），`WidthKey` 记为「**已具备、本线不采纳**」，任何可拖机制本轮都不做。
> **改判触发条件（写死）**：若 S3 之后的实机看下来**仍觉得帮助面板读不下**，则改走 A，且顺序固定为 **「先做窗口按最大宽度预留 → 看过那条空带 → 再把栏做成可拖」**，作为 S4 之后的独立切片。
> 于是 §4.1 的 `Width="320" + MinWidth="260"` 仍是本线形态；§4.1 里「不建议回到运行期换根/变体」那条**不变**。

> **维护者裁定（2026-09-21，第二次）：(乙1) = A**（"我同意你的提议 A，必要时可以和底栏的版本号上方公用空间"）。
> **形状（A 的代价被维护者的提示削掉）**：窄态帮助**不做包裹容器**，而是在 `body-row` 与 `footer-band` 之间加**一条独立的全宽带** `<Scroll Id="help-band" Fill="true" Padding="0" VisibleKey="help-open-narrow">`，里面是同一个 `us/help-panel`。⇒ **帧从"三条带"变成"三条带 + 一条条件带"，而不是变形**；`body-row` 自身形状不变（它 `Fill="true"`，条件带出现时自然让出高度）。
> **一个玩家意图、两个互斥呈现**：`help-open` 仍是**唯一**被开关写下的值（可写）；`help-open-wide` / `help-open-narrow` 是宿主派生的**只读** bool，判据是 `WindowChromeLayout.DrawerWidensTheWindow(screenW, screenH)`（纯函数：该屏幕放不放得下展开后的窗）。宽态元素由 `help-open` 改为 `help-open-wide`。
> **窄带的高度与滚动（lead 要求先说清）**：**不写高，用 `Fill="true"`** —— 主体行与窄带**平分剩余高度**，所以窄带**永远不会把页脚挤出窗口**，也不需要任何手写阈值；并且它是 `Scroll`，面板在自留高度内**内部滚动**。这一条是对 `UsHelpPanelWidget` 按**全目录最坏值**测带的直接回应：不设上界的天然高度会等于"最坏那条帮助"，那会把首选观感一起吃光。
> **fit 门与修复同批**：`WidthAndLanguageEvidenceSweep` 现在把 `reports.Count > 0` 记为 violation，并且**声明屏幕**——抽屉开的那一半案例被钉到"放得下它的最小屏幕"，另加一个 `1228`（1920 屏展开窗的页面宽）覆盖**宽态**。理由：736 宽的页面 + 1920 的屏幕是**产物做不出来的组合**，拿它当门会让门永远红。
> **诚实的限制**：A **不能先验保证** 736/开 达 fit=0；后备是给内容列真正的下限、或把窄带做矮/可滚动。且这一整刀**未构建/未验证**（构建冻结），变异证据 PENDING。

### 0.3 第二版新增事实：`d82a3ca` → `bce1ba4` 落地的 8 件词表

| # | 能力 | 提交 | 依据（FL 仓内） | 对本设计的作用 |
|---|---|---|---|---|
| 1 | **`WidthKey`（B5）** | `3df7bf5` | 见 §0.2 | 见 §0.2 —— 唯一需要**重裁结论**的一条 |
| 2 | **`SelectedKey`** | `3df7bf5` | `Kernel/Widgets/AtomVocabulary.cs:224-250` | bool 绑定把**元素**解析成 Active 处理（state 胜作者）；**故意不是 `ToneKey`**（回传 "Active" 会把这个本线已退役的名字重新授权）；解不出 → fail-soft + 一条 note。它是**元素级**属性（容器属性表里没有它），所以对 §5.1 里 nav 的「按卡 bool 做选中态」只是换了一种写法——nav 内部的卡片选中仍由该 composite 自绘 |
| 3 | **`WideHidden`** | `3df7bf5` | `Kernel/UiLayoutEngine.cs:2628-2638`；父级要求 `Kernel/UiHost.cs:1056-1057` | `NarrowHidden` 的**精确镜像**；父级无 `Breakpoint` 时 creation 期拒绝。⇒ §2「窄态：`NarrowHidden` 或堆叠（产品二选一）」现在**两侧都能声明**，不必再写两棵互斥子树 |
| 4 | **`chrome/banner` 拿到角色对** | `3df7bf5` | `Kernel/Widgets/ChromeBannerWidget.cs:37-48` | banner 进入 atom 的 `ToneAndEmphasis` 角色对；它读角色的**文字色**，默认 emphasis = `Muted`——这个默认值保证**无色 banner 的墨与从前完全一致**（词表增益、零视觉变化）。**这一条闭合 G5，见 §0.4** |
| 5 | **元素级 `HelpKey`** | `64af720` | `Kernel/UiLayoutEngine.cs:151`、`:610-627`；属性门 `Kernel/UiHost.cs:887-894` vs `:902-913` | 引擎在 widget **画之前**统一 claim（在 `EnterNode` 内，所以 claim 归属该元素）。**精确边界**：`HelpKey` 在 **widget 属性表**里，**容器表里没有** ⇒「只挂在 widget 上、永不挂 `Section` 容器」这条**仍然成立**，§5.4 的写法不用改。**它是 G1 的解药，见 §0.4** |
| 6 | **`input/mode-row` 的 `TitleKey1..8` + 每选项 hover help** | `64af720` / `fcc6a18` | `Kernel/Widgets/InputModeRowWidget.cs:14-27,56-73,228-238`；`Kernel/Widgets/OptionHelp.cs` | 选项标签走翻译缝（`TitleKeyN`），并且 `HoverHelpKey`（**可写 string 绑定**）把 hover 到的选项身份写出去。**G4 闭合**；`DescriptionN` 升格为选项的帮助文本 |
| 7 | **容器 `Tab`** | `ee387f1` | `Kernel/UiLayoutEngine.cs:107`、`:2655-2661`；属性门 `Kernel/UiHost.cs:904-909` | 容器可声明 `Tab`，与 widget 同一门控，**隐藏整棵子树**。US 清单已在用（`checklist-card`）；S3 的页头/三栏也可以用 workspace 门控 |
| 8 | **`UiOption`（FL-16）** | `bce1ba4` | `Kernel/UiOption.cs:17-30`；`Kernel/Widgets/DropdownWidget.cs:61-76,192-196` | `BindOptions<UiOption>` 给动态选项（display, value）；`BindOptions<string>` 语义不变（display == value）。另 **FL-23**（`26c8ac0`）：元素类型不匹配现在**先上报 fail-soft 通道、再照旧抛出**——「先探一种形态、失败再退另一种」的探针**必须走不报错的读** |

### 0.4 ★ 比 §0 更要紧：WAVE-1 的 **G1–G5 五个缺口全部已闭合**

§5.3/§5.4 是**当前**「迁移被挡住、lines deleted = 0」的结论来源，而它列出的五个缺口现在全部有实现：

| 缺口 | 原文结论 | 现状 | 依据 |
|---|---|---|---|
| **G1** 无声明式帮助/悬停钩子 | 「任何涉及帮助的迁移都静默丢覆盖，**这一条 gate 住整个 bucket (i)**」 | **闭合**：元素级 `HelpKey`，引擎统一 claim | `Kernel/UiLayoutEngine.cs:610-627`（提交 `64af720`） |
| **G2** 重复行无法上报 item key | 「layers 保持 composite，不迁移」的全部理由 | **闭合**：`input/button.PayloadKey` —— 命令收到**行自己的 key**，per-item 作用域；创建期契约随形态走（有 payload ⇒ `BindAction<string>`，无 ⇒ `BindCommand`） | `Kernel/Widgets/ButtonWidget.cs:37-41,60-71,118-147`（提交 `b3957dc`） |
| **G3** 无无外观命中区、无法按内容量高 | 同上 | **闭合，但有一条须照实记录的边界**：`Chrome="none"` 不画任何 surface、命中照常；`Height="Auto"` 的高度取**它自己 caption 的测量内容**（空 caption 回落 `RowHeight`）。**边界**：它是按**自身 caption** 量高，不是「按兄弟组合出的两行行高」；caption 仍以 `MiddleCenter` + `singleLine: true` 绘制 | `Kernel/Widgets/ButtonWidget.cs:84-99,169-174`（提交 `b3957dc`） |
| **G4** `input/mode-row` 标题是字面量、绕过翻译缝 | 本地化消费者用不了该 kind | **闭合**：`TitleKey1..8` + 每选项 hover help | `Kernel/Widgets/InputModeRowWidget.cs:56-73,228-238` |
| **G5** `chrome/banner` 没有 `Tone` | §5.4 保留状态带 composite 的**唯一依据**（「保留下来的 composite 就是 G5 的引证」） | **闭合**：banner 进入角色对，默认 `Muted` 保证零视觉变化 | `Kernel/Widgets/ChromeBannerWidget.cs:37-48` |

**这改变了什么**（不改 S3 的工作面，改 S4/Round-4 的结论与价签）：

- §5.3「(i) 的 15 个部件在布局上可表达，但 **0 个**可以在不丢帮助覆盖的前提下迁移」——**前置条件 G1 已解决**，这句话失效。
- §5.4「layers 保持 composite，它们的阻塞是 **G2（已证缺口，带锚点）**」——**该锚点已闭合**；layers 真正的剩余阻塞退回**层级行 × 行内组合**（即 Route A 那一件能力），而不是「行上报不了 key」。
- §5.4 保留状态带 composite 的理由（G5 引证）**已消失**；是否改迁 `chrome/banner` 成为一次**新的**产品选择，而不是「等 FL 补能力」。
- §5.3 的「给维护者/lead 的绕行选项 (a)/(b)/(c)」——**(a) 已经发生了**。
- **本文不改写 §5.3/§5.4 的行文**（那是结论，不是措辞）：就地加推翻横幅指向本节，重裁留给维护者/后续轮次。

### 0.5 仍未落地的（S3/S4 不得依赖）

- **chrome 动作插槽**：仍缺（维护者裁定 (5) 已放弃；帮助按钮走固定页头）。
- **`container/tree` 行内子控件**：仍缺（B9 本线不做）。
- **文本对齐轴（B10）**：仍缺——右对齐文本仍靠 placement 摆整个元素，或控件自带锚点。
- **B8 的「词表半」**：`Description1..8` 的绘制（移除 vs 绘制）仍是维护者的裁定项。
- **`input/mode-row` 的 `TitleN`/`ValueN` 配对**：`TitleN/DescriptionN` 没有对应 `ValueN` 时 creation 期拒绝（`InputModeRowWidget.cs:93-101`）。

## 1. 设计原则

1. **结构 = 容器 + 原子**；`us/*` 只保留“manifest 表达不了”的自有契约（量测/命中/几何）。
2. **外观 = 样式文档**：颜色/字体进 `<Scheme>`，间距进 `<Density>`；C# 只选 scheme，不再逐行选色。
3. **间距 = density token**：页面容器尽量不写死 `Padding`/`Gap`，由 `<Density>` 驱动；需要像素精确处才写显式值。
4. **定位 = placement**：`Overlay` 两轴放页头/页脚；flow 交叉轴做垂直居中；不再用等分 spacer。
5. **配色 = DarkGold + 单一强调色**：`AccentGold` 一个 token，悬停用 `AccentHover`；危险/成功走 `Tone`。
6. **文件驱动**：`Layout.Schema2.xml` + `Style.Schema1.xml` 作为 Mod 目录松散文件（内嵌资源作回退），
   `UiDocumentService` + `HostAttached` 接线；自动监听仅 Dev，生产默认关；破坏性文件保留 last-known-good。

## 2. 目标形态（三栏 + 固定页头）

```xml
<UiPage Schema="2" Source="coahuilite.universalsqueaker">
  <Column Id="page-root" Gap="8" Padding="12">
    <!-- [2026-09-21 更正，S3-2b 实测] 原稿这里是 <Overlay Id="header-band" Height="60"> + AlignX="Right"，
         两处都被证伪，原文保留在此以免后人以为它被凭空改掉：
         (1) Overlay 里不声明 AlignX 的子元素默认 Stretch（`UiPlacement.cs:13,77-84`）⇒ 标题会排到带子
             右缘、被后画的按钮压住；而词表里**没有 MaxWidth、没有「取剩余」装置**，`Width="Auto"` 对无
             label 集的 kind 无效，手写预留常数被 R14 禁止 ⇒ 那个字形在这套词表里**不可实现**。改用 Row。
         (2) `Height="60"` 会裁掉换行的 caption（与 footer「钉死 Height 反而被日志报 needs 33px/28px」
             同源的教训），所以删掉，改由内容量高。稳定性的职责移进 `us/page-title` 的 Measure：
             它按**五个工作区的最坏值**测带，页头才不会被页签切换推动（nav 卡片 bounds 有 lane 钉死）。
         注意 Row 的主轴已有主人 ⇒ `AlignX` 在 Row 子元素上会被创建期拒绝，开关只留 `AlignY="Middle"`；
         「右对齐」由「它是最后一个定宽子元素」自然得到，并仍被断言（右缘 == 带子内右缘）。 -->
    <Row Id="header-band" Padding="0">
      <Widget Id="page-title" Kind="us/page-title" />
      <Widget Id="help-toggle" Kind="input/button" TextKey="US.Help.Drawer.Toggle"
              ActionBind="toggle-help-drawer" Width="128" Height="26" AlignY="Middle" />
    </Row>

    <Row Id="body-row" Fill="true" Gap="12" Breakpoint="720" Narrow="Column">
      <Column Id="nav-column" Width="200" Fill="true">
        <Widget Id="nav" Kind="us/nav" />
      </Column>
      <Scroll Id="content-scroll" Fill="true" Gap="8">
        <!-- 卡片 = Section 容器；行 = 核心原子；列表 = Repeat + Template -->
      </Scroll>
      <Scroll Id="help-scroll" Width="320" MinWidth="260" Fill="true" VisibleKey="help-open">
        <Widget Id="help-panel" Kind="us/help-panel" />
      </Scroll>
    </Row>

    <!-- 页脚：Overlay 左侧状态 + 右侧构建标识（placement 取代 spacer）。
         [2026-09-21 更正，S3-3] `Height="26"` 未实现，且**不应**实现：它与 footer 当年那条教训同源
         （钉死 Height 会盖掉 wrap-aware 的 measure，实机日志报过 needs 33px / has 28px），所以带子与它的
         子元素都不写 Height，高度由内容量。S3-3 只立了**带子**（`Overlay Id="footer-band" Padding="0"`），
         里面仍是保留的 `us/footer` composite —— 把它原子化成下面两个 `text/wrapped` 是 **S4 的迁移**，
         不是这一刀的事。 -->
    <Overlay Id="footer-band" Padding="0">
      <Widget Id="footer" Kind="us/footer" />   <!-- S4: -> footer-status (AlignX=Left) + footer-build (AlignX=Right) -->
    </Overlay>
  </Column>
</UiPage>
```

细则：
- **`Breakpoint="720"` 对真实窗口是空条款（2026-09-21 实测并裁定）**：`Breakpoint` 比对的是**容器自己的内宽**（`UiLayoutEngine.cs:883` `width - padding.Left - padding.Right`，`IsNarrow` `:995-1010`），而 body-row 的内宽 = 窗宽 − 64。**真实窗宽地板是 800 ⇒ 内宽恒 ≥ 736 ⇒ 任何 ≤736 的断点对真实窗口零影响**（只对 480/320 两个人造探针生效）。所以**真正的窄屏保护不来自这个断点**，而来自下面的 (乙1)。
- **这一档不只是"窄"，它会真的溢出文本（S3-4b 实测，harness 度量）**：在 **736（最小真实窗的内宽） + 抽屉开**时中心列是 **168px**（滚动条预留后内容带 152px），`ui.text.overflow` 报 **7 条（EN）/ 2 条（ZH）**：`mode-row` 54/36、`global-volume` 54/18、`basic-tuning` 42.7/22、`timing` 108/90、`camera-indicator` 42.7/22（ZH：`global-volume` 36/18、`timing` 72/54）。**这条要进实机清单的 F-05 项**（"overflow 是否归零"），并且是 (乙1) 那个切片必须解决的对象，不是「窄一点而已」。
- **一个已知的玩家可见状态（裁定 (乙2) 接受，实机待看）**：`WindowChromeLayout.SettingsOpenWidth` 以屏幕宽度封顶（`:119`），所以在**逻辑屏宽 ≤ 800** 时抽屉展开无法把窗口撑宽：内宽 736 − nav 200 − 2×gap 12 − help 320 = **内容列 192px**（今天 176 的右栏是 376）。**1024 屏展开是 416px，已经好于今天**，只有 ≤800 落到 192。**注意「逻辑屏宽」是 UI 缩放后的值**：普通 1920 显示器在 UI scale ≳2.5 时就落到逻辑 768 ⇒ 同样 192px，这不是罕见硬件问题。
- **(乙1) 是 S3 之后立刻做的独立切片**（不挂在「等谁碰到 800 屏」上）：到那一步要**量完再选**两条候选并给价签——宿主算出的 `VisibleKey` 切第二呈现（叠放/覆层）**vs** 用 `WidthKey` 让宿主算出「付得起的帮助列宽」（后者要把 `DrawerWidthDelta` 从常量改成函数，它被 `UniversalSqueakerUiLogicTests/Program.cs` 钉死）。
- **页头固定**：`header-band` 是 `page-root` 的流内子元素，不参与 `content-scroll`，因此滚动时不动。
  这是当前 FL 词汇下“帮助按钮不随内容滚走”的**可实现解**；真正放进 shell chrome 需要 FL 新增插槽（见 §4.3）。
- **帮助抽屉**：`VisibleKey="help-open"` 保留 node 与 `ScrollPosition`（0.6 剪枝语义），收起时整列不占位。
- **窄态**：`body-row Breakpoint="720" Narrow="Column"`；窄态下抽屉走 `NarrowHidden` 或堆叠（产品二选一）。
- **放置的边界**：flow 子元素只接受交叉轴的像素微调；页头/页脚用 `Overlay` 才能左右定位且可用百分比。

## 3. 样式文档（配色与密度全部文件化）

```xml
<Styles Schema="1" Scheme="us-root" Density="regular">
  <Scheme Name="us-root">
    <Color Token="Base"          Value="#0f1116" />
    <Color Token="WorkspacePlane" Value="#0f1116" />
    <Color Token="Panel"         Value="#171a21" />
    <Color Token="SectionBand"   Value="#171a21" />
    <Color Token="Raised"        Value="#1f232c" />
    <Color Token="Hover"         Value="#1f232c" />
    <Color Token="Selected"      Value="#2a2312" />
    <Color Token="Border"        Value="#333a46" />
    <Color Token="BorderStrong"  Value="#3d4452" />
    <Color Token="Divider"       Value="#232833" />
    <Color Token="TextPrimary"   Value="#e6e9ee" />
    <Color Token="TextSecondary" Value="#98a1af" />
    <Color Token="TextDisabled"  Value="#98a1af" />
    <Color Token="AccentGold"    Value="#d19a38" />
    <Color Token="Danger"        Value="#3a1f1f" />
    <Font  Token="DefaultFont"   Value="Small" />
  </Scheme>
  <Scheme Name="us-attention"><Color Token="AccentGold" Value="#86e7d8" /></Scheme>
  <Density Name="regular">
    <Metric Token="Padding" Value="12" /><Metric Token="Gap" Value="8" />
    <Metric Token="Spacing" Value="4"  /><Metric Token="RowHeight" Value="24" />
    <Metric Token="Hairline" Value="1" />
  </Density>
  <Density Name="dense">
    <Metric Token="Padding" Value="8" /><Metric Token="Gap" Value="6" />
    <Metric Token="RowHeight" Value="20" />
  </Density>
</Styles>
```

> **页面节奏的落地方式：维护者裁定 (丁)（2026-09-21）——照 §2 的 XML 实现。** 页面容器用**显式** `Padding`/`Gap`（就是 §2 里 `<Column Id="page-root" Gap="8" Padding="12">` 那样），`<Density>` 的 token **保持 6/6/4/24/1 不动**。这样唯一的可见 delta 是页面边距本身的取值，**控件内缘零变化**，于是这一步不需要逐条重导原子的几何 lane。
> 与本文开头原则 3「页面容器尽量不写死」**存在张力，这是有意的**：`Padding` 一个 token 同时服务两种角色（页面节奏与控件内缘），把它整体抬到 12/8 会一次移动**每一个控件**的内缘。(甲)（全局 `12/8/4/24/1`）**没有被否掉，很可能是最终形态**，但它是 **S3 之后的独立切片**——「样式文档一处重调整套界面」是 S2 花力气做的事，必须在实机上专门看它把每个控件改成什么样。
> 下面是 §3 的**目标**样式文档（S2 的形态），不是 S3 的落地形态。

- `UsTheme.cs` 退化为 `UsTheme.Surface() => UiTheme.DarkGold`（+ US accent），**26 色表整体移入文档**；
  `HoverPoint` 已不存在，悬停步统一读 `theme.AccentHover`。
- 旧 `WindowChromeLayout`/`UsCardLayout` 的几何常量改由 `<Density>` 承载；窗口尺寸策略（开/合宽度、4:3）保留在 C#，
  因为它驱动 `Verse.Window`（引擎不提供“按内容改窗”的缝）。

## 4. 三项待决设计的技术结论

### 4.1 右栏宽度：`Auto` 不可用；可调需要 FL 的宽度绑定
- `Width="Auto"` 的定义是“该 **kind 注册的 label 集**的文本自然宽”（`UiLayoutEngine` → `UiWidgetRegistry.GetLabelAttributes`）。
  容器默认没有 label 集；给容器注册同名 kind 是 FL 自己（X-30）标注“**不作为正式补救**”的旁路。
  即使能给 label 集，帮助面板的宽度是产品参数，不该由某条字符串的像素宽决定。
- 本设计采用：**固定 `Width="320"` + `MinWidth="260"` 保底 + `VisibleKey` 开合**。
- 若必须“拖拽可调”：FL 目前**没有宽度/数值绑定**（只有 bool 的 `VisibleKey`），manifest 的 `Width` 是字面量，
  因此可调宽度在当前公开面**不可表达**。可行路径二选一：
  (a) 向 FL 提 `WidthKey`（bool 的 `VisibleKey` 的数值兄弟，把“已声明元素的可绑定宽度”补成对称能力）；
  (b) 接受两档（收起 / 320）作为产品形态。
  **不建议**回到“运行期换根/变体”的旧写法（那正是 0.6 剪枝之下已失效并被删除的机制）。

### 4.2 帮助按钮位置
- shell chrome 插槽仍不存在（本轮 FL 未改 `UiWindowHost`）；`DoWindowContents` 仍 sealed，chrome 只有
  Title/Subtitle/CloseText/TitleBarHeight/SidePadding/CloseButtonSize/AccentBarHeight。
- 因此落点为 **固定页头 `Overlay`**（§2）：不随内容滚动、可右对齐、可用 `input/button` + action 绑定。
- 若你坚持“在关闭按钮左侧的 chrome 带”：需要 FL 新增声明式不滚动区域/动作插槽（台账 FL-18），
  届时把 `help-toggle` 从 `header-band` 移到该区域即可，绑定与动作键不变。

### 4.3 文件驱动
- FL 侧零改动：`UiDocumentService.Add(source, embeddedFallbackXml)` / `Attach(host, layoutId, styleId)` /
  `Signal` / `Reload` / `Pump`（由 `UiHost.BeginFrame` 驱动）+ `UiReloadPolicy` + last-known-good。
- US 侧：两个 `UiDocumentSource`（Layout/Style 松散文件路径）+ 内嵌回退 + `HostAttached` 挂载 + 失败告警；
  自动监听默认 Dev。验收三条（改宽度/颜色/Breakpoint → 关窗重开可见且 DLL 未重编译；坏文件 → 旧页仍在 + 一条告警；同进程内）。

## 5. `us/*` kind 盘点（25 → 目标 ≤7）

| 处理 | 对象 | 依据 |
|---|---|---|
| **删除（原子/容器取代）** | `us/global-volume`、`us/camera-indicator`、`us/basic-tuning` 行、`us/race-layer`、`us/xenotype-layer`、`us/filter-bar`、`us/diagnostics`、`us/mode-row`、`us/camera-readout`、`us/footer`（拆成 `Overlay` + 原子）、`us/page-title` 的帮助开关部分 | 行=label+checkbox/slider/field/rule，卡片=`Section`，banner=`chrome/banner`，空态=`state/empty` |
| **保留（自有契约）** | `us/nav`（等高三卡 + 省略）、`us/help-panel`（hover 不变最坏 band）、`us/scope-tree`（mood 三因子同宽网格）、`us/preset-list`（两层复选框树；tree 无行内控件）、`us/timing`（单位换算 + 预留 caption band）、`us/attenuation-editor`（chart + 预设 + 窄态）、`us/page-title`（标题量测，去掉开关预留） | 各自拥有 manifest 表达不了的量测/命中/几何规则 |
| **~~被 FL 缺口挡住~~（2026-09-20 复核后重写）** | `us/voice-pack-checklist` 的搜索框：`input/text-field` **已发**（本轮载体）；`us/diagnostics` 分段行：`input/mode-row` **已存在**，只有 Description 的**绘制**（B8 词表半）仍待维护者；`us/preset-list`：FL 已裁定 tree 行内子控件**本线不做**（B9），故保留自有 kind。真正的剩余缺口见 §5.1 | 台账 FL-15/FL-17/FL-19；实测 2026-09-20 |

### 5.1 可行性盘点（2026-09-20 实测）：18 个手写部件有多少能被现有词表取代

**先修正一个已失效的前提。** 本文 §5 与 `MEMORY.md` 曾写「FL 只有 12 个核心 kind、没有 checkbox 原子」——**这是错的**，它把「US 布局粒度止步于卡片」这个结论的一部分建立在假前提上；按维护者的 (A)/(B) 规则，**那一部分属于 (A) US 自己的欠账，不是 FL 缺口**（不需要向 FL 提请求，也不需要抬 minor）。

实测（载体 `0.7.x`）：FL 发 **17 个 widget kind** —— `text/wrapped`、`container/tree`、`input/text-field`、`input/stepper-slider`、`input/slider`、`section/header`、`chrome/rule`、`Repeat`、`display/progress`、`input/number-field`、`chart/line`、`input/mode-row`、`state/empty`、`input/dropdown`、`chrome/banner`、`input/checkbox`、`input/button` —— 外加 11 个容器 kind。`input/checkbox`（0.5.0 / P3，带自动验证）、`Repeat` + `<Templates>`、`container/tree`、`state/empty`、`display/progress`、`input/text-field` **全部已发**。US 侧 `Source/UniversalSqueaker/UI/*.xml` 里 `Repeat` / `Templates` 的出现次数是 **0**。

**词表已经能做什么（逐条读 FL 源码的注册表）。** `Repeat` 的 `Items` 是 `IReadOnlyList<string>` 值绑定、`Template` 指向 `<Templates>` 中的子树；引擎按 item key 物化声明的子树（item-local 键 `<Items>.<key>.<declaredKey>`），行内命中照常路由到内层控件，`input/checkbox` 在模板里解析 item-local 键且未绑定时 fail-soft。`text/wrapped` 有 `Bind`（字符串值绑定）＋ `Text`/`TextKey`，`input/checkbox` 有 `Bind` ＋ `ActionBind` ＋ `Label`/`LabelKey`，所以**行内文本与状态都可以数据驱动**，不必为每行写 C#。

**边界同样实测（这就是 (ii) 的全部内容）。** `container/tree` 的 `Bind` 是 `IReadOnlyList<UiTreeRow>`（key / depth / text / expandable / expanded）：**一行只画一个 label band，属性表里没有 Template，不接受行内子控件**，只有一个 `ActionBind`（payload = 行 key）；`Repeat` 的属性表是 `Items` / `Template` / `Padding` / `Gap` / `Height` ＋ 可见性，**没有 level / indent / expansion**。因此「**层级行 ＋ 行内组合子树**」今天确实不可表达，而且不是给 tree 打个补丁能解决的——那是「控件不能组合自己的部件」这个部件能力缺口。

计数口径：**code** ＝ 非空且非 `//`、`///` 注释的行；**draw** ＝ `Draw*` 方法体行数（声明式化后消失的绘制/几何代码；模型、绑定与适配器仍是 C#，所以这不是整文件行数）。18 个部件合计 **3,027 code / 1,646 draw**。

| 部件 | code | draw | 桶 | 依据 / 代价（一句） |
|---|---:|---:|---|---|
| `UsScopeTreeWidget` | 678 | 405 | **(ii)** | 层级 scope 行（每行内容是循环按钮/下拉）＋ 4 条 mood 行；tree 无 template、Repeat 无 level，两者无法组合。附带第二个独立需求：mood 三因子**跨行等宽网格**（tree 与 Repeat 都不提供） |
| `UsPresetListWidget` | 255 | 145 | **(ii)** | preset → race → xenotype 的复选树，每行含 checkbox、preset 行含 Import 按钮：层级行需要组合子树 |
| `UsHelpPanelWidget` | 125 | 36 | **(iii)** | 内容是按**当前 hover claim 索引的目录**，band 按**全目录最坏值**测量以免 hover 时布局跳动；绑定文本量的是当前字符串，任何声明都表达不了这条规则 |
| `UsVoicePackChecklistWidget` | 322 | 226 | (i) | 扁平行集：`Repeat` ＋ 模板（`input/checkbox` ＋ 绑定 label）＋ `input/text-field`（已发）做搜索 ＋ `chrome/banner` ＋ `state/empty`；角色化 banner 落 Tone/可见性声明 |
| `UsFilterBarWidget` | 203 | 138 | (i) | 4 个互斥 chip ＝ `input/mode-row`（`Bind` 是 string）＋ 3 个嵌套 `input/dropdown`（`OptionsBind`） |
| `UsTimingWidget` | 176 | 117 | (i) | `input/slider` ＋ `input/number-field` ＋ 按钮；**代价**：今天「按最坏值预留 caption band」要退化成声明的 `Height`（词表没有 `MinHeight`），失去按内容计算那一条 |
| `UsBasicTuningWidget` | 172 | 95 | (i) | 6 行静态 label+checkbox；F6 的父子两行 = 子行 `VisibleKey` 直接读父行的 bool 绑定 |
| `UsRaceLayerWidget` | 153 | 53 | (i) | 扁平行集：`Repeat` ＋ 模板行（绑定 label ＋ `ActionBind` 回传 key） |
| `UsAttenuationEditorWidget` | 148 | 86 | (i) | `chart/line` **本身已拥有拖拽命中与几何**；两端锁定与 15..65→0..100 的域映射是 C# 业务/数据投影（声明式化不会删掉），预设是 `input/button` |
| `UsXenotypeLayerWidget` | 128 | 56 | (i) | 同 Race Layer，行集来自 `xenotype-domains` 绑定 |
| `UsPageTitleWidget` | 125 | 43 | (i) | `text/wrapped`（`TextKey` 或 `Bind`）＋ 每个 workspace 一个 `Tab` 门控标题；帮助开关按 §4.2 移到固定页头后不再属于它 |
| `UsDiagnosticsWidget` | 118 | 64 | (i) | 三选一 = `input/mode-row`；开关 = `input/checkbox`（Description 的绘制仍是 B8 词表半） |
| `UsNavWidget` | 106 | 54 | (i) | 静态 5 元素导航：`input/button` ＋ 绑定 label/subtitle ＋ 声明的共享卡高；**代价**：今天「取最高卡」的计算规则变成声明常量 |
| `UsFooterWidget` | 99 | 42 | (i) | `Overlay` ＋ 两个 `text/wrapped`（左/右由 `AlignX` 摆放），非交互 |
| `UsGlobalVolumeWidget` | 67 | 37 | (i) | `input/slider` ＋ `input/number-field`（百分比） |
| `UsModeRowWidget` | 62 | 18 | (i) | 核心 kind `input/mode-row` 已存在；本部件存在的原因只是「枚举↔字符串转换不过 Host 边界」，那是 C# 数据层 |
| `UsCameraIndicatorWidget` | 61 | 25 | (i) | 一行开关：`input/checkbox` ＋ `ActionBind` |
| `UsCameraReadoutWidget` | 34 | 6 | (i) | 一行右对齐只读文本：`Overlay` ＋ `AlignX=Right` ＋ `text/wrapped`（`Bind`） |

**合计。** (i) **15 个部件 / 1,969 code / 1,060 draw** —— 用今天已发的词表就能做，**不需要 FL 任何改动**；(ii) **2 个部件 / 933 code / 550 draw** —— 全部卡在同一个能力（层级行 ＋ 组合子树）；(iii) **1 个部件 / 125 code / 36 draw** —— 自有量测契约。

> **2026-09-20 实测修正（步骤 B 落地后）**：checklist 一件已实际迁移（§5.7）。它的 code 实测 **322 @46a580b**（原表 317 是更早一次测量，其间 step A 加了 TitleHidden 分支），迁移后 **154 code / 236 行**，widget 侧删 **230 行**，但 `Source/**` **净 +68 行**。所以 (i) 的 **1,969 code 是「可搬走的绘制代码上限」，不是「可删除的净行数」**；与「删除」直接对应的是 **1,060 draw** 那一列，而每件还要另付模板、per-item 投影、lane 与保留 composite 的代价。第一件付出的脚手架里约六成是一次性的（后续继承），因此「迁移 15 件 = 15 × 第一件」不成立；但第二件旗舰（race/xenotype 行集）的摩擦类别不同（G2/G3 整行 hover 命中），它的边际成本由交互形状决定，**Route A 不宜用一个数据点拍板**。

**这张表给维护者的价签**：(i) 是 US 自己的迁移工作量（0 FL 依赖，约占 18 部件 code 行的 65%、可删 draw 代码的 64%）；要价的缺口只服务 (ii) 的 **933 行**（两个部件），并且它的正确形状是「部件可组合自身部件」而不是「tree 支持行内控件」。是否值这个价由维护者定，本盘点不下结论。

### 5.2 采纳计划（bucket (i) 的 15 个部件）：每个采纳**验证哪个 FL 组件**、预期摩擦属于 (A) 还是 (B)

**本阶段的成功判据不是「US 把自己那块屏做完」，而是「FL 的组件被真实页面用过，摩擦被分类并回传」。** 维护者 2026-09-20 明说：把 FL 与 US 排在同一轮、允许钉 `FL 0.7.x` × `US 0.5.x`，就是为了**用 US 的实践验证 FL 的组件**。因此下表按「验证价值」排序，而不是按部件大小：`Repeat` + `<Templates>` 目前**消费侧证据为零**，FL 自己的晋升门要求「真实消费者被逼着用过」才算证明——bucket (i) 的采纳就是那份证据。

| 部件 | 验证的 FL 组件 | 预期摩擦 | 归类 | 建议轮次 |
|---|---|---|---|---|
| `UsVoicePackChecklistWidget` | `Repeat` + `<Templates>`（旗舰，零消费证据）、`input/checkbox`（item-local）、`input/text-field`、`state/empty`、`chrome/banner`、`chrome/rule` | ① 每个行字段都要在 item-local 命名空间里注册一个绑定访问器（今天部件直接读模型）；② `chrome/banner` **没有 Tone**、`state/empty` **不接受 `Bind`** —— 角色化 banner 与动态空态文本要退化成 `text/wrapped` + `chrome/rule`（丢掉这两个 kind 自带的 band 契约） | ① (A) 用法改动；② **(B) 候选**（待采纳时证实：`Tone` 与 `Bind` 是否该对这两个 kind 可用） | 1 |
| `UsRaceLayerWidget` / `UsXenotypeLayerWidget` | `Repeat` + `<Templates>` + `ActionBind`（payload = 行 key） | ① 同上 item 投影；② 每行「选中」高亮若要走 `Tone`/`Emphasis`，二者都是**字面量属性**（无 `ToneBind`）→ 只能用「每个 tone 一份模板 + item-local `VisibleKey`」复制模板 | ① (A)；② **(B) 候选**（per-item tone/emphasis 绑定；任何数据驱动列表都需要） | 1 |
| `UsBasicTuningWidget` | `input/checkbox`、`section/header`、`chrome/rule`、`text/wrapped`、`VisibleKey`（F6 的父子规则） | 几乎没有：父子两行是静态声明的，子行 `VisibleKey` 直接读父行 bool | (A) 纯用法 | 2 |
| `UsCameraIndicatorWidget` | `input/checkbox` + `ActionBind`（command/disabled 漏斗） | 无 | (A) | 2 |
| `UsGlobalVolumeWidget` | `input/slider`、`input/number-field`（已有 `Format`） | 0..1 ↔ 百分比是绑定侧的投影（C#，不删） | (A) | 2 |
| `UsModeRowWidget` | `input/mode-row`（`Bind` 为 string）**＋ `HoverHelpKey`**（本轮载体新增：hover 到的选项把它的帮助标识写入一个可写 string 绑定，未 hover 时空串） | 关闭的枚举 ↔ string 转换落在 Host 边界（今天正是为了不给边界引入字符串才自造 kind）。**价值上升**：US 的 help claim 通道（`UsKernelDraw.HelpHover`）可以在声明式模式下直接驱动，不必再自造行 | (A) | 3 |
| `UsDiagnosticsWidget` | `input/mode-row`、`input/checkbox` | 同上枚举↔string；`Description1..8` 的**绘制**仍是 B8 词表半（维护者裁定） | (A) ＋ 待裁定 | 3 |
| `UsFilterBarWidget` | `input/mode-row`（4 个互斥 chip）、`input/dropdown` + `OptionsBind` | 4 值过滤器是封闭枚举 → string；三个下拉的联动是 OptionsBind 数据（可声明） | (A) | 3 |
| `UsTimingWidget` | `input/slider`、`input/number-field`、`input/button`、`text/wrapped` | 「按最坏值预留 caption band」无处声明（**没有 `MinHeight`／预留带属性**）→ 要么声明固定 `Height`，要么接受跳动 | **(B) 候选**（预留带；采纳时用真实跳动证实） | 3 |
| `UsNavWidget` | `input/button`、`text/wrapped`、（`Tab` 或按卡 bool 做选中态） | 「取最高卡 + 预留一行 subtitle」从计算规则变成声明常量；若必须按内容取高，则同样缺预留带 | (A) ＋ (B) 候选同 Timing | 3 |
| `UsPageTitleWidget` | `text/wrapped`（`TextKey`/`Bind`）、`Tab` 门控 | 每个 workspace 一个 Tab 门控标题（或一个绑定标题）；同样缺预留带；帮助开关按 §4.2 移出 | (A) ＋ 同上 | 3 |
| `UsFooterWidget` | `Overlay` + `AlignX`（Batch 1 落位词表） | 无 —— 这是 Batch 1 落位词表在真实页面上的复现证据 | (A) | 3 |
| `UsCameraReadoutWidget` | `Overlay` + `AlignX=Right` + `text/wrapped`（`Bind`） | 若读数必须在**流式容器内**右对齐，则回到 B10（无文本对齐轴）；放在 `Overlay` 里则今天可做 | (A)，或 (B) 候选 = B10 | 3 |
| `UsAttenuationEditorWidget` | `chart/line`（今天经 US 自己的包装使用）、`input/button` | 两端锁定与 15..65→0..100 域映射是 C# 业务/数据（不删）；采纳验证的是**不套 US 包装**直接用 `chart/line` | (A) | 3 |

**建议顺序**：第 1 轮先做 `Repeat` 的两个旗舰（checklist、race/xenotype 行集）——它们产生 FL 最缺的消费侧证据，并且是唯一会触发上面两个 (B) 候选的采纳；第 2 轮做静态行集（basic-tuning、camera-indicator、global-volume）；第 3 轮做原子对换（mode-row、diagnostics、filter-bar、timing、nav、page-title、footer、camera-readout、attenuation-editor）。**每个采纳都要按 FL 的格式回报：验证了哪个组件、摩擦是什么、必须修的是 (A) 还是 (B)，并附失败敏感的 lane。**

**维护者倾向（记录，不在本轮执行）**：对 bucket (ii) 的缺口，维护者倾向 **Route A —— 给 `container/tree` 一个可选的按行模板**，但明确要求**先等 US 真正用过现有组件**再决定；本节的 (ii) 数字（2 个部件 / 933 code 行）就是那个决定的价格依据。本轮不实现、不给 FL 提交请求。
### 5.3 WAVE 1 采纳实测（2026-09-20）：迁移被三个**实测缺口**挡住，bucket (i) 必须修正

> **⚠ 已被 §0.4 推翻（2026-09-21 复核，载体 `bce1ba4`）：G1–G5 五个缺口全部已闭合，本节据此得出的结论需要重裁。**
> 本节**逐字保留**，因为「为何曾经如此」是证据的一部分：当时的三个缺口（G1 无声明式帮助/悬停钩子、G2 重复行无法上报自己的 item key、G3 无无外观命中区 / 无法按内容量高）**确实存在**，`Repeat` 的采纳**确实**被依赖面挡住，而且当时**还没有** `WidthKey`、元素级 `HelpKey`、`input/text-field`、容器的 `Tab`。重裁（bucket (i) 是否解锁、S4 的价签）由维护者/PM 进行，本文不改写本节结论。

**结论先说**：checklist 与 race/xenotype 行集的 `Repeat` + `<Templates>` 采纳**在动一行代码之前就撞墙**，而且是结构性的，不是排版细节。撞墙点不是 `Repeat` 本身——`Repeat` 的契约（`Items` = `IReadOnlyList<string>` 值绑定、`Template` 指向 `<Templates>`、item 局部绑定 `<Items>.<key>.<declaredKey>`、身份 `<declaredId>#<key>`）**实测可用**；挡住迁移的是它的**依赖面**：US 的悬停帮助、行交互与本地化。

**G1（B，通用）没有任何声明式的帮助/悬停钩子。** FL 全库只有一个帮助形状的属性：`input/mode-row` 的 `HoverHelpKey`（且它是**选项级**的，不是元素级）；`Section` 的 schema 只有 `Title`/`TitleKey`。US 的悬停帮助由 **`UsKernelDraw.HelpHover` 调用 43 处 / 22 个 UI 文件**声明（本会话口径：grep `HelpHover` over `Source/**`；lead 独立复测为 **46 处 / 23 个文件**，口径不同、结论不变），另加清单里 **12 处 `HelpKey=`** 区块声明（`Layout.Schema2.xml:33-44`，由 `UsSectionWidgetBase:109` 消费），并且**双向**钉在 46 项帮助目录上（`Program.cs:306`）。**18 个部件文件里每一个都至少带一条 claim。**推论：**把任何区块/行迁成声明式元素都会静默丢掉它的帮助覆盖**——双向测试会红，而删掉目录项等于发布一次帮助功能回退。通用性：任何带 inspect/帮助浮层的消费者都要把帮助身份挂到声明式控件上；与既有的 `Bind`/`ActionBind`/`LabelKey` 词表对称；FL 自己已经为 `input/mode-row` 单独发了 `HoverHelpKey`，那就是需求存在的证据。
**G2（B，通用）重复行无法上报自己的 item key。** `input/button` 触发的是**无 payload 的命令**（`ButtonWidget.cs:83`）；`input/checkbox` 只能写自己的 item 局部 bool；`container/tree` 能上报行 key，但**不吃模板**、一行只画一个 label band（无两行、无换行生长）。可用绕法（属 (A)）：**item 限定绑定键本身就是身份载体**（每项一个 bool setter / 每项一个命令键），所以 `Repeat` + checkbox/button 行能为「自己那一项」动作；但点击目标必须是自带外观的控件，「点在一整行空白处」不可表达。通用性：任何「非按钮行」的列表（时间线、文件列表、域列表）都要；与 `container/tree` 既有的「ActionBind 上报行 key」对称，缺的只是**模板行做不到同样的事**。
**G3（B，通用）没有无外观的命中区，也无法拉伸到内容测高的整行。** 所有可点击的核心 kind 都自绘外观；`input/button` 的高度是 `RowHeight` 或声明的 `Height`，所以「两行文本测高而成的行」之上铺一个整行命中区不可表达。通用性与对称性同 G2。
**G4（B，通用，本轮新发现）`input/mode-row` 的选项标题是字面量，绕过翻译缝。** schema 只有 `Title1..8`/`Description1..8`（没有 `TitleKeyN`），源码里 `DrawOption` 直接用 `option.Title`，**全文件没有一处 `Translate`**。所以**本地化消费者用不了这个 kind**：US 的 `us/mode-row` 四个选项在 EN/ZH 两套 Keyed 表里。与每个 kind 上的 `LabelKey`/`TextKey` 对称。
**G5（B，源码确认）`chrome/banner` 没有 `Tone`。** 它的 schema 是 `Bind/Text/TextKey/Height/Tab/Hidden`，没有 `ToneAndEmphasis`；US 的四条 banner 是**角色映射**的（冲突/目标缺失 = attention 青色带；dormant = 不可用 hatch），声明式表达不了角色。绕法：`text/wrapped Tone=…` + `chrome/rule` 组合，代价是丢掉 banner 自己的 band 契约。

**四个候选的实测判定（本轮，而不是假设）**：

| 候选 | 判定 | 依据 |
|---|---|---|
| `Tone`/`Emphasis` 只有字面量（无 `ToneBind`） | **CONFIRMED** | 三处都需要数据驱动的颜色：footer 的 save-status 颜色（`UsFooterWidget.cs:81-90`）、race 行的选中态、banner 角色；`AtomVocabulary` 只有 `ToneAttribute`/`EmphasisAttribute`，没有绑定形态 |
| `chrome/banner` 没有 `Tone` | **CONFIRMED（源码）** | schema 无 `ToneAndEmphasis`（`ChromeBannerWidget.Register`）；未写 lane，因为迁移未开始 |
| `state/empty` 不接受 `Bind` | **NOT PROVEN** | 它的两个句子（空域 / 空搜索）可以用两个 `VisibleKey` 门控的字面量元素表达，不构成阻塞 |
| 没有 `MinHeight`/预留带 | **NOT PROVEN** | 本轮没有任何迁移走到「按最坏值预留 band」那一步；不重复上一轮的假设 |

**§5.1 的修正（按维护者要求，接触现实后改表）**：bucket (i) 的 15 个部件在**布局**意义上确实可用现有词表表达，但**在 G1 未解决之前，没有一个可以无损迁移**——18 个部件全部带帮助 claim（22 个 UI 文件、43 处）。所以正确的表述是：**(i) = 15 个部件 / 1,969 code 行在布局上可表达，其中 0 个可以在不丢帮助覆盖的前提下迁移**；真正的前置条件是 G1，而不是「US 愿不愿意做」。这也解释了为什么 FL 的 `Repeat` 至今消费侧证据为零：**采纳被依赖面挡住，不是被 US 的意愿挡住**。

**给维护者/lead 的绕行选项（本轮不自行决定）**：(a) FL 增加一个**元素级帮助/悬停身份钩子**（例如 `HelpKey` 走一个消费者绑定，与 `VisibleKey` 对称）→ 一次解锁全部 15 个；(b) US 接受迁移区块的帮助回退（删除对应目录项与两套语言键，46 → 更少）；(c) US 只在**新的、无帮助覆盖的页面**上用 `Repeat`（本轮不产生证据）。

**本轮没有提交任何迁移（lines deleted = 0），这是有意的**：在 (a)/(b)/(c) 里选一条之前，任何迁移要么发布帮助回退，要么产出假证据。三个缺口的证据（FL 源码行 + US 的 43 处 claim + 46 项目录钉）已经写进本节，可供 FL 转录入 `MEMORY.md`。
### 5.4 维护者裁定（2026-09-20）：checklist 的**部分**迁移；layers 保持 composite

> **⚠ 已被 §0.4 推翻（2026-09-21 复核，载体 `bce1ba4`）：本节的两条依据——保留状态带 composite 的 G5、layers 保持 composite 的 G2——都已闭合。**
> 本节**逐字保留**以记录「为何曾经如此」：G2 当时是**已证缺口**（`ButtonWidget.cs:83` 触发无 payload 命令；`container/tree` 不吃模板且一行只画一个 band），G5 当时是**源码确认**的缺口（`ChromeBannerWidget.Register` 的 schema 没有 `ToneAndEmphasis`）。因此「是否改迁 `chrome/banner`」「layers 是否已具备迁移条件」现在是**新的产品裁定**，不是「等 FL 补能力」；layers 真正剩下的阻塞退回**层级行 × 行内组合**（Route A）。

**裁定形态**：checklist 卡片改为声明式——`Section` + `section/header`（`HelpKey` 挂在 widget 上，**永不挂在 `Section` 容器上**）+ `input/text-field`（搜索）+ `Repeat` + `<Templates>`（`input/checkbox` 行）+ `state/empty`（两个空态，`VisibleKey` 门控）。**一次验证四个 FL 组件**：`Repeat`（今天消费侧证据为零）、`input/checkbox`、`input/text-field`、`state/empty`。

**角色化状态带保持 US 自有 composite kind，本轮不迁 `chrome/banner`。** 理由要写进证据、而不是当作妥协：**G5 是已记录、源码确认的缺口**（`ChromeBannerWidget.Register` 的 schema 没有 `ToneAndEmphasis`），对已记录缺口的诚实回应是**在 FL 提供能力之前保留消费侧 kind**，而不是为了让迁移看起来完整而发布一次玩家可见的颜色回退。**保留下来的 composite 就是 G5 的引证。**（选择：保留整条状态带 composite，而不是切出更小的 kind——代码更少、语义边界更清楚。）

**layers 保持 composite，不迁移。** 它们的阻塞是 **G2（已证缺口，带锚点）**：`Repeat` 的行无法上报 item key——`input/button` 触发的是无 payload 命令（`ferritelib .../Widgets/ButtonWidget.cs:83`），`input/checkbox` 只能写自己的 item 局部 bool，`container/tree` 不吃模板且一行只画一个 band（`.../Widgets/TreeWidget.cs:41-47`）。**已证缺口的正确处置是记录并作为后续轮次候选**，而不是为了让迁移完整而扭曲 UI（加一个点击目标、改交互语义）。

**被否决的两条路（记录理由）**：(i) 给行加 `input/button` = 玩家可见的外观回退（按钮表面盖住纯悬停行），架构目标不值得用它换；(ii) 把行改成 `input/checkbox` = **交互语义**变化（点选变勾选、点已选行由重选变取消选择），比外观变化更重。两条都不做。

**顺序**：迁移**不必**在本轮落地。若 freeze 时 checklist 迁移未完成且不干净，就只验证并提交已就绪的部分（S2 increment 1 + `IsHelpSelected` 的裸键等值修复），迁移带着自己的验证进入下一轮。**半迁移的清单不会靠近任何一次门禁运行。**

**本轮已就绪（未提交）**：S2 increment 1（`UsTheme` 的 16 色表改为样式文档 + `UiStyleResolver.ApplyTo`）；(A) 修复（`UsSectionWidgetBase.IsHelpSelected` 接受裸区块键）。
### 5.5 S2 第三条（清掉逐行选色）的实测结论：**它依赖迁移，不是迁移的前置**

**测量**：US 侧逐行读主题色的点共 **77 处 / 20 个文件**（`ctx.Theme.TextPrimary|TextSecondary|TextDisabled|TextOnGold|TextOnDanger|Danger|Selected|Hover|Raised|Panel`；最多的是 `UsDiagnosticsWidgets.cs` 18、`UsScopeTreeWidget.cs` 11、`UsVoicePackChecklistWidget.cs` 10、`UsPresetListWidget.cs` 6、`UsFooterWidget.cs` 5）。清单里今天声明的 `Tone=` 属性数量是 **0**。

**为什么这 77 处不是「顺手换成语义角色」就能清掉的**：它们绝大多数是**行状态**驱动的——选中行的墨、不可用行的墨、danger 状态的墨——而 `Tone`/`Emphasis` 是**元素级静态属性**：同一个元素的所有行共用一条角色，行与行之间的状态差异表达不了。这正是 §5.3 里已 CONFIRMED 的 `Tone`/`Emphasis` 只有字面量那条缺口的第二个后果。

**可用的角色缝确实存在**（对消费侧公开）：`UiResolvedStyleTable.Resolve(tone, emphasis, writable)`（`UiResolvedStyle.cs:107`）与 `UiStyleResolver.Resolve(theme, nearestFirst, writable)`（`UiStyleResolver.cs:141`）；`UiWidgetContext` 也公开 `Theme` 与 `StyleChain`。但**缝存在不等于能表达行状态**：它解析的是「这个元素是什么角色」，不是「这一行处于什么状态」。

**结论与改序**：S2 第三条**只在行已经声明式之后才可执行**——那时每一行的状态可以用「按状态一份模板 + item 局部 `VisibleKey`」或 per-item 的 Tone 变体表达；对**保留为 composite 的 kind**（例如 §5.4 保留的状态带）它**永远不可执行**，因为那条缺口没有被补。所以正确顺序是：**先迁移（§5.4），行变成声明式之后再做逐行选色清理**；把它当迁移的前置会得到一个无法验证的 77 处大扫除。维护者 2026-09-20 的裁定（「S2 第三条作为独立已验证增量」）因此**改序为迁移之后的增量**，这一改动记录在此。

**顺带记录（供 FL 侧计价）**：如果 FL 想要「消费侧自定义 kind 也能按角色取墨」，缺口不是「没有 Resolve」，而是**没有 per-row 的角色输入面**（元素级 Tone 无法表达行状态，而 per-item 的角色绑定不存在）。这与 §5.3 的 Tone 条目是同一条，不另开请求。
### 5.6 步骤 B 的两份证据规格（先写规格，再写代码；避免事后补叙事）

**B-1 「过滤后的 key 列表不说谎」lane 的设计说明**（lead 点名要保留的性质，未来每一个声明式列表都依赖它）：

- **要证明的性质**：`Items` 绑定给出的 key 列表，与同一帧里搜索框的文本**必然一致**——即「列表里出现的每一项都通过当前搜索谓词，且每一个通过谓词的项都出现在列表里」。两个方向都要断言：只测一个方向会漏掉另一半（多列 = 说谎；少列 = 静默丢数据）。
- **为什么必须有这条 lane**：投影与搜索框分属两个时钟——搜索写入 bump 内容修订，`Items` 由 host 投影重新计算。如果投影用了**上一帧**的搜索文本（或忘了在搜索变化时重新投影），屏幕上是**完全看不出来**的：列表画得整整齐齐，只是内容是旧的。这正是「看起来正确」的 bug 类别，也是为什么它值得自己的 lane 而不是搭在别处。
- **必须覆盖的失败模式**：① 搜索变化后 `Items` 未重算（陈旧列表）；② `Items` 里含被搜索拒绝的 key（多列）；③ 通过谓词的 key 缺失（少列）；④ 同一 key 在列表里出现两次（重复身份，`Repeat` 会拒绝重复 key，但那会让整行消失而不是报错）；⑤ key 与 per-item 绑定不同步（列表里有 key，但 `enabled`/`label` 等读不到——`input/checkbox` 对缺失的 item-local 键是 fail-soft 的，所以它会画成默认态而不报错，这条只能由 lane 抓）。
- **形状**：先断言基线（无搜索时 `Items` == 选中域的全部 pack key，数量与顺序都与源一致）；写入一个只匹配子集的搜索串；再断言两个方向 + 无重复 + 每个 key 的 per-item 绑定都能读出非默认值；最后清空搜索并断言回到基线（可逆性）。**顺序与数量都断言**，因为 `Repeat` 按 key 复用节点，顺序错误是状态错位的来源。

**B-2 G1 的记账方法**（迁移落地时逐条数出来，不估算）：

- 迁移前对每个 claim 站点记录：`path:line` + 该处的 key 表达式（字面量 / 计算式）。迁移后重新 grep `HelpHover(`，对**每一个被删除的站点**记一行「删除了哪个、由哪个元素的 `HelpKey` 接管」。
- 三个必须给出的数：① **删掉的站点数**（预期 30 个静态站点中的大多数，具体数以 grep 为准）；② **有没有站点其实无法用字面量 `HelpKey` 表达**（已预判的例外：`UsScopeTreeWidget.cs:388` 与 `:616` 两处按行状态取键，保持 imperative；`UsModeRowWidget.cs:64` 与 `UsKernelDraw.cs:499` 两处是**选项级**，由 `HoverHelpKey` 接管，不算站点删除）；③ **帮助目录或其 46 项 pin 是否需要改动**——预期**不需要**（键名不变，只是声明位置从 C# 移到 manifest），若需要改动必须写明改了什么、为什么。
- 记账的判定口径：**键名集合在迁移前后必须完全一致**（迁移是搬家，不是改名）。任何键名变化都要单独列出并说明，否则「帮助目录没动」这句话就没有意义。
### 5.7 步骤 B 摩擦报告（2026-09-20 实测：checklist 迁移已落地）

**提交**：`cdcc675`（B-1 过滤后的 key 投影 + 不说谎 lane）、`340526f`（B-2 声明式卡体 + `Tab="Packs"` 门控 + 删一 bool shim）。证据：harness **ALL PASS / EXIT 0**、`verify-local` **15/15 EXIT 0**（载体 `c898a6b3`，Release，无 PDB）；6 个变异各自红，且红在预期断言上（见末段）。

**行数（`git numstat` 实测，非估算）**

| 范围 | 增 | 删 |
|---|---:|---:|
| `UsVoicePackChecklistWidget.cs` | 35 | **230**（431 → 236 行） |
| `Source/**` 全量 | 311 | 243 → **净 +68** |
| `UsChecklistFilter.cs`（新） | 69 | 0 |
| host 侧 per-item 绑定命名空间（新） | ~80 | 0 |
| `Layout.Schema2.xml` | 36 | 5 |
| 工具侧（新 lane 409、几何 lane 重导、UI 逻辑门） | 488 | 17 |

按本盘点的 code 口径（非空且非注释行）：widget **322 → 154**（−168）。

**结论一：删除是真的，节省是假的（尚未摊销）。** bucket (i) 说「1,969 code 行今天可表达」——本件证明的是**可表达**，不是**可省**：把一张卡体的绘制搬进 manifest 要先付一次脚手架，所以第一件的 Source 净行数是 **+68**。真正消失的是 **draw** 那一列（本件约 226 draw 行的大部分）；模型/绑定/适配器照旧是 C#。

**结论二：成本两桶（lead 指定口径）**

- **一次性脚手架（后续 widget 继承）**：① 投影函数形状（一个谓词 + 一个有序 key 投影，两个消费者共用，`UsChecklistFilter`）；② **per-item 绑定命名空间的按需注册**（投影负责它刚列出的行的 item-local 键——注册表没有前缀解析，pack 集是运行期投影，所以「注册恰好引擎即将物化的那几行」是唯一诚实的形状）；③ 「不说谎」lane 的设计与骨架（283 code 中约七成可复用）；④ 卡高关系的多子元素重导形状；⑤ **两条 help 门的加宽**（manifest `HelpKey` 可以是 item 键，且算作 claim）——这一条一次性解锁后续所有 widget 的声明式帮助；⑥ `UsPacksText.Format(IUiTranslation,…)` 这类「host 需要翻译缝」的形状；⑦ `Tab`-on-container 一致性修复（FL 侧，免费）。
- **每件都要重付（widget 专属）**：① 模板的具体形状与随之而来的**视觉/交互 delta 取舍**；② per-item 键集与取值器（本件 label/meta/coverage/enabled）；③ 该件的 fixture 与 lane 断言；④ 状态可见性 bool（has-domain / 两种空态）；⑤ 任何 pin 了旧 composite 几何的 lane 都要重导（本件 1 条）；⑥ 保留 composite 的切分（哪些部分因 G5/G2/G3 留下）；⑦ 删旧 kind 的绘制代码 + 更新 kind 清单与源码不变式。

**结论三：widget #2 的代价——诚实答案是「管道那半已摊销，交互那半还不知道」，而后者才是决定项。**

- 可复用部分约占非 lane 新增的 60%，lane 骨架约七成。因此 #2 的**管道**边际成本是「模板 ~15 行 + per-item 键集 ~25 行 + fixture/lane ~60 行 + 状态绑定 ~10 行」。
- 但 §5.2 排在第 1 轮的第二个旗舰是 race/xenotype 行集，它的摩擦**类别不同**：那是一整行纯 hover 命中区（G2/G3），没有 `input/checkbox` 这种「行本身就是该语义」的原子可用。用 `input/button` 会发布按钮外观回退（§5.4 已否决），用 US 行 composite 则一行绘制代码都不删。所以 #2 的边际成本由**交互形状的决定**支配，不由管道支配。
- 对 Route A 的直接含义：**不要用一个数据点拍板**；同时可以确定，Route A 要买的不是「管道」——管道已被本轮证明是一次性的，要买的是「层级行 × 行内组合」这一件能力。

**结论四：G1 记账（5.6 口径，逐条）**

- **删除的站点：恰好 2 个**（`Source/**` 的 `HelpHover(` 代码行 35 → 33）。
  1. `Source/UniversalSqueaker/UI/Kernel/UsVoicePackChecklistWidget.cs:281`（迁移前）键字面量 `us/voice-pack-checklist/search` → 由 `Layout.Schema2.xml` 的 `checklist-search`（`input/text-field`）元素 `HelpKey` 接管。
  2. 同文件 `:335` 键字面量 `us/voice-pack-checklist/row` → 由模板的四个 widget 元素（`checklist-row-label`/`-meta`/`-coverage`/`-check`）`HelpKey` 接管。
- **保持 imperative、未删除**：同文件 `:261` `us/voice-pack-checklist/forget`（保留 composite 内的破坏性按钮）。
- **无法用字面量 `HelpKey` 表达的站点：本轮 0 个。** 既有例外清单不变：`UsScopeTreeWidget.cs:388`/`:616`（按行状态取键）保持 imperative；`UsModeRowWidget.cs:64` 与 `UsKernelDraw.cs:499` 是选项级，由 `HoverHelpKey` 接管，不算删除。
- **键名集合前后完全一致**：`{us/voice-pack-checklist, /search, /row, /forget}` → 同一集合（只是声明位置从 C# 移到 manifest）。**目录与 46 项 pin 无需改动**（gate 12 绿即为证）。
- 唯一「目录邻近」的改动是**门本身**：manifest `HelpKey` 校验从「必须是 section」放宽为「item 或 section 皆可」，claim↔item 双向钉把 manifest `HelpKey` 计入 claim 来源（合计 39 行）。改的是门的口径，不是目录内容；`PlaceholderKey` 同时加入被收集的 manifest key 属性表（搜索提示从 C# 移入 manifest 后必须仍被断言存在）。

**结论五：五条被点名的行为变化（写进提交信息，不是隐含）**
1. orphan 带从行列表**下方移到上方**（widget 元素是一个矩形，无法跨 manifest 兄弟节点）。
2. 行的点击目标从**整行**缩到 **checkbox 带**（G2/G3 的活标本：记录，不绕开）。
3. 行的 surface / hover / 选中轨消失——逐行状态选色正是 §5.5 排在迁移**之后**的条款。
4. meta/coverage 带改用原子的主题字号（Small），行略高。
5. 搜索占位符改用 `TextSecondary`（原子的既定选择）而非 `TextDisabled`。

**变异证据（每条都红，且红在预期断言上）**：M1 谓词多收一行 → `the list contains a key the search rejects`；M2 投影不跟随搜索写入 → `the probe query must narrow the list`；M3 去重移除 → `a blank key and a repeated key must collapse to one entry each`；M4 不注册 per-item `enabled` → `the projection must have registered 'checklist-pack-keys.us.alpha.enabled'`（**屏幕上看不出来**，fail-soft）；M5 卡片 `Tab` 改错 → `Packs workspace hidden by default`；M6 丢掉一个通过谓词的 key → 基线数量断言红。

**盘点修正（§5.1 表）**：`UsVoicePackChecklistWidget` 的 code 实测为 **322 @46a580b**（原表 317 是更早一次测量，其间 step A 给它加了 TitleHidden 分支）；draw 未复测（本轮不再宣称该列数字）。更重要的一句：**bucket (i) 的 1,969 code 是「可搬走的绘制代码上限」，不是「可删除的净行数」**——每件还要付模板、per-item 投影、lane 与保留 composite 的代价，第一件的 Source 净行数是 +68。真正与「删除」直接对应的是 **1,060 draw** 那一列。

### 5.8 S4-1 摩擦报告（2026-09-21 实测：Overview 三张卡声明式化，三个 kind 同批退役）

**提交**：代码 `4d67199`（本报告与它同批，文档提交紧随其后）。证据：harness **ALL PASS / EXIT 0**、
`verify-local -NoRestore` **15/15 EXIT 0**（载体 Release）。**7 个变异**逐个实测、各自红且红在预期断言上（见末段）。

**消解对象与「同批退役」**

| 对象 | code / draw | 退役物 |
|---|---:|---|
| `UsGlobalVolumeWidget` | 67 / 37 | kind + 文件（167 行） |
| `UsBasicTuningWidget` | 172 / 95 | kind + 文件（244 行） |
| `UsCameraIndicatorWidget` | 61 / 25 | kind + 文件（82 行） |

- **Registrar 的 us/* kind 集 18 → 15**，`UiSourceInvariantTests` 的基数钉与「注册集 == manifest 集」
  双向不变式同批改完（前者 18→15，后者不动）。
- **7 个 `toggle-*` action 绑定同时退役**（`toggle-egg`、`toggle-scale-cooldown|talking|population`、
  `toggle-camera-indicator`、`toggle-eat-precision[-include-drugs]`）。理由不是精简：`input/checkbox`
  把「写入所读 bool 的反值」写在自己的 value 绑定上，**value 绑定本身就是开关**，留着 action 等于给同一个
  值留第二条写通道 —— 这正是本项目反复付学费的「一个意图两个写者」。
- 净删除：三个 widget 文件 **493 行**（含 157 行 draw/几何代码）。

**新增的是绑定侧投影，不是新 kind**（§5.2 预判的 (A) 类，C# 不删）：`global-volume-percent`
（0..100 ↔ 0..1；与 slider 走**同一个**业务 setter）、`global-volume-caption`（只读、格式化）、
`easter-egg-on` / `easter-egg-off`（On/Off 两个 atom 的 `VisibleKey` 门）。

**声明形态**（每张卡都是 `Section` + `section/header` + 声明行）

```xml
<Section Id="basic-tuning" Tab="Overview" Padding="12" Gap="6">
  <Widget Id="basic-tuning-header" Kind="section/header" TitleKey="US.Section.PlaybackBehaviour"
          Height="26" HelpKey="us/basic-tuning" />
  <Column Id="basic-tuning-body" Gap="2" Padding="0">
    <Row Id="basic-cooldown-row" Gap="8" Padding="0">
      <Widget Id="basic-cooldown-label" Kind="text/wrapped" TextKey="US.Tuning.ScaleCooldown"
              HelpKey="us/basic-tuning/scale-cooldown" />
      <Widget Id="basic-cooldown-check" Kind="input/checkbox" Bind="scale-cooldown"
              Width="24" Height="30" AlignY="Middle" HelpKey="us/basic-tuning/scale-cooldown" />
    </Row>
    ...
  </Column>
</Section>
```

三个被验证的**词表事实**（都是本次实测，不是预判）：

1. **`Height="30"` 不是行高常量，是让 atom 画出 18px 视觉框的算术输入。**
   `input/checkbox` 的画法是 `side = max(8, rect.height - theme.Geometry.Padding * 2)`，而 Padding 是 6
   ⇒ 30 才得到 18（与 shipped 的 `UsKernelDraw.CheckboxVisual` 同值）。声明 `Height="24"` 会得到 12px
   的框（**视觉回退**，未采用）。
2. **`text/wrapped` 没有对齐轴、也没有字号属性**：它固定 `UpperLeft` + 主题字号（Small）+
   `Padding*2` 的上下留白，且 Measure 用的是**自己那一格的宽**（引擎 `WithViewWidth(width)`），所以
   行内量测是准的，但行标签的**垂直居中与字号**与 composite 不同（见下面的玩家可见差异）。
3. **`HelpKey` 是元素级的、引擎代为 claim**（widget 命中即写 session 的 hover claim）。因此 8 个
   `UsKernelDraw.HelpHover` 站点被删除、键名一个不改，manifest 的 `HelpKey` 接管；`UiSourceInvariantTests`
   的双向钉（每条 claim 都有目录项、每个目录项都被 claim）与 `SectionHelpKeyOf` 可达性钉都**不需要改**。

**行数（`git numstat` 实测）**

| 范围 | 增 | 删 |
|---|---:|---:|
| `Kernel/Us{GlobalVolume,BasicTuning,CameraIndicator}Widget.cs`（三个文件删除） | 0 | **493** |
| `Kernel/UsKernelWidgetRegistrar.cs` | 0 | 3 |
| `UI/Layout.Schema2.xml` | 78 | 3 |
| `UI/UsKernelSettingsHost.cs` | 20 | 8 |
| `tools/.../DeclarativeOverviewLaneTests.cs`（新） | **737** | 0 |
| `tools/.../MoodLayoutFocusedTests.cs`（重裁） | 65 | 44 |
| `tools/.../SettingsGeometryLaneTests.cs`（重裁） | 71 | 81 |
| `tools/.../Program.cs` + `RecordingSettingsSource.cs` | 45 | 20 |
| `tools/UniversalSqueakerUiLogicTests/`（两条不变式重裁） | 46 | 12 |

**结论一：可表达 ≠ 可省，这一件把价签验证了。** `Source/**` 不是净删：删掉 493 行绘制代码的同时，
manifest 与绑定侧投影把行数加了回来。真正消失的是 **draw 那一列**（三件 157 行里的绝大部分）。

**结论二：G1 记账（5.6 口径，逐条）**

- **删除的 claim 站点：5 个代码站点**（`git grep -c 'HelpHover('` over `Source/**`：**32 → 27**）：
  `UsBasicTuningWidget.cs:188`（egg 行）与 `:222`（`DrawBasicRow`，一个站点服务 5 行）、
  `UsCameraIndicatorWidget.cs:62`、`UsGlobalVolumeWidget.cs:140`（slider）与 `:141`（number）。
  被这 5 个站点覆盖、现在由 manifest `HelpKey` 接管的**键是 9 个**：`us/basic-tuning/egg`、
  `/scale-cooldown`、`/scale-talking`、`/scale-population`、`/eat-precision`、
  `/eat-precision-include-drugs`、`us/camera-indicator/toggle`、`us/global-volume/slider`、
  `us/global-volume/number`（一个站点 ≠ 一个键，这正是 5 与 9 的差）。
- **无法用字面量 `HelpKey` 表达的站点：0 个。** 既有例外清单不变（`UsScopeTreeWidget` 两处按行状态取键、
  `UsModeRowWidget`/`UsKernelDraw` 两处选项级）。
- **键名集合前后完全一致**：9 个键由 manifest `HelpKey` 接管，三个 section 键由 `section/header` 的
  `HelpKey` 接管。**目录 46 项与 gate 12 都不需要改。**

**结论三：一条常驻不变式换了形式，必须记录。** composite 用「On/Off 两个字符串里更长的那个」量 egg 的
状态带，好让行高**不随当前值变化**；声明式没有预留带（没有 `MinHeight`，没有权重），所以这条性质不再由
构造保证，而是由**测量**保证：`DeclarativeOverviewLaneTests.TheEggStateBandDoesNotDependOnTheToggle` 在
4 宽 × 2 语言下断言开/关两态行高相等（实测全为 67.67）。这是 §5.3 里被标为 **NOT PROVEN** 的
「预留带」缺口在本件上的**第一次真实接触**：本次没有走到必须退化那一步（两个 atom 的实际测量相等），
但它已经不是假设。

**实测几何（harness，StubMetrics；非真实像素）**

| 量 | shipped composite | 声明式实测 |
|---|---|---|
| basic-tuning 单行高（EN/ZH、1024/736/480） | 24（密度 token） | **33.33** |
| egg 行高 | 52（floor） | **67.67** |
| 320 EN 换行的行高 | 随标签量测 | **54.67** |
| 卡片高（parent ON / OFF） | — | **315.33 / 280.00** |
| checkbox 命中带 | 24×24 | **24×30** |
| 18px 视觉框右缘 | 内容右缘 −10 | **内容右缘 −6** |
| 行标签左缘 | 卡片左缘 +12 +**10** | 卡片左缘 **+12** |

**五条玩家可见差异（不许声称观感等价；全部「仍需实机」）**

1. **行高整体 +9.33px（egg 行 +15.67）**。原因是 atom 的 band = 文本 + `Padding*2`(12) + 其字号（Small）
   的行高（21.33），而 composite 的 floor 是密度 token 24 且不带上下留白。320 EN 下多行换行的行到 54.67。
2. **行标签**：左移 10px（不再有 `RowLeftPadding`）、**顶对齐**（`text/wrapped` 固定 UpperLeft，不再
   MiddleLeft 垂直居中），字号仍是 Small（未变）。
3. **整行命中区消失**：composite 的「标签区可点」没了，只有 24×30 的 checkbox 带是命中面；同时行的
   surface / hover 高亮 / 底部分隔线（`RowBottomLine`，Divider 墨）消失，改由 `chrome/rule` 画分隔线 ——
   而 `chrome/rule` 的 Neutral 走的是**主题 Border**（#333a46），比 Divider（#232833）亮。
4. **18px 视觉框右移 4px**（不再与 `ControlColumnRightInset` 对齐），且命中带从 24×24 变成 24×30；
   分段控件（us/diagnostics）的共享控制列**不再与这三张卡共享**（§5.1 的「共享控制列」契约降为
   timing/diagnostics 自己的契约，`SettingsGeometryLaneTests.UniformControlColumn` 同批重裁）。
5. **egg 状态带与 global-volume caption 的字号 Tiny → Small**（atom 只有主题字号），墨色保留
   （`Emphasis="Muted"`）但 `global-volume` 的 caption 从 Tiny+TextSecondary+MiddleLeft 变为
   Small+TextSecondary+UpperLeft。另外 `global-volume` 的数值框、slider 现在由 atom 自绘，
   拖动/输入的手感与焦点环**从未在实机看过**（`input/slider`、`input/number-field` 在本页第一次被真实使用）。

**变异证据（7 个，逐个实测；每条都真的跑了构建与 harness）**

| 变异 | 实测红在哪（断言原文摘录） |
|---|---|
| **M1** 把 `Kind="us/camera-indicator"` 放回 manifest —— 即「旧 kind 恢复参与」 | `real embedded resource + schema shape`：*the retired composite kind must not survive as a manifest Kind: us/camera-indicator*（且该 kind 已不在 Registrar，Host 创建同样会拒） |
| **M2** 子行 `VisibleKey="eat-precision"` → `scale-talking` | 本 lane `CardsAreDeclaredSections`：*the eat-precision child row must be gated by VisibleKey reading the parent's own bool binding* |
| **M3** `basic-cooldown-check` 的 `Height="30"` → `24` | 本 lane `DeclaredRowsFollowTheManifestBandRule`：声明带 `24 x 30` 不成立（`MoodLayoutFocusedTests` 的「一行一个命中面」24×30 过滤同样会红） |
| **M4** 删掉 cooldown 行 label 与 checkbox 两处 manifest `HelpKey` | UiLogicTests（gate 12）：*catalog item 'us/basic-tuning/scale-cooldown' is claimed by no control* |
| **M5** 把 egg 的 On 文案拉长到换行 | 本 lane `TheEggStateBandDoesNotDependOnTheToggle`：*On=89 Off=67.67*（1024 English）—— 开/关行高不再相等 |
| **M6** camera checkbox `Bind="camera-indicator"` → `allow-eggs` | 本 lane `CardsAreDeclaredSections`：（卡片的一个控件不再拥有它声明的值绑定） |
| **M7** `global-volume-number` 的 `Bind` → `global-volume`（百分比控件改绑归一值） | 本 lane `CardsAreDeclaredSections`：*the number field must own the percent projection 'global-volume-percent', got global-volume* |

**M5 的实测值同时说明了 shipped 形态是安全的**：两个状态句在 4 宽 × 2 语言下的行高都是 **67.67**
（相等），所以那条常驻不变式成立；M5 是把它拉断（改完夹具后复跑，数值与断言不变）。

**这一步的两态由夹具自己那两个 shipped 视图驱动**（rich 视图 `allowEasterEggs: true`、空视图
`false`），**没有**给 `RecordingSettingsSource` 加种子字段。理由不是省事：该夹具被本套件 **9 个文件**
构造，加一个默认值就会移动**所有**这些 lane 的输入，而「套件仍然全绿」证明不了那些 lane 还在测它们本来
要测的东西。全量核对：每一处构造都只使用 `RichData` / `Authors` / `ChecklistPacks` /
`WrappingDomainText` / `EatPrecisionEnabled`，egg 输入恒为 `RichData ? true : false`，**改动前后逐字节
一致**（`git diff 1ac4bd0 -- tools/.../RecordingSettingsSource.cs` 为空）。

> **变异实验本身的一个仪器教训（值得记，正是「先查量具的输入」那条纪律的标本）**：manifest 与语言表都是
> **主程序集的嵌入资源**，而 `Copy-Item` 恢复文件会保留**旧 mtime**；增量构建因此可以复用「上一次变异构建
> 出来的」主程序集，让下一次变异根本观测不到自己的改动。实测症状很好认也很误导：M5 第一次跑出的是 M3 的
> 行高症状（`got 5`），第二次跑出的是 M7 的绑定症状。修法是**每次变异构建前把 manifest 的 mtime 顶到当前**
> （`(Get-Item …).LastWriteTime = Get-Date`）；顶了之后 M5 才红在自己的断言上（On=89 / Off=67.67）。
> 结论：变异实验里，**构建的输入**就是量具的输入，改完文件不等于改了量具看到的东西。

### 5.9 S4-2 摩擦报告（2026-09-21 实测：Packs 两个 layer 卡声明式化；G2/G3 第一次被真实页面使用）

**提交**：代码 `7f54ea7`（本报告与它同批），B1 修复后续另有一提交（见下）。证据：harness **ALL PASS /
EXIT 0**、`verify-local -NoRestore` **15/15 EXIT 0**（载体 Release `e929fa11`）；**8 个变异**逐个实测、
各自红在预期断言上。

> **B1 修复后续（2026-09-21，载体 `e929fa11`）。** 维护者裁定「不能让技术债在可见且可修复的时候放任
> 扩散」，fl-dev 把 `SelectedKey` 加进 `UiLayoutEngine.QualifyItemBinding` 的作用域名表，US 侧同一批：
> 两个模板的行标题声明 `SelectedKey="selected"`、host 注册 per-row `<items>.<key>.selected`、§5.9 那条
> 「模板里不许出现 SelectedKey」的钉**翻转成正向断言**（正好一行为真且是模型选中的那一行），
> `Tone`/`Emphasis` 禁令保留。证据同上（harness ALL PASS + 15/15）。详见 §5.9.4 第 2 条与 §5.9.6。
>
> **门 6 的状态（如实记录）**：FL 在冻结之后又落了**一个纯文档提交**（`5052440`，只改 `docs/api-tiers.md`
> 与两处 `docs/development/**`），而载体内嵌的构建修订仍是 `e929fa11` ⇒ 门 6 那句「payload 内嵌 commit
> == carrier HEAD」机械比较会红。这是**文档先于载体重建的必然结果，不是缺陷**（规则在册：*A doc-only commit
> reddens "embedded commit == HEAD" until the carrier is rebuilt. That is expected rather than a defect.*）。
> 本轮的验收身份以 FREEZE NOTICE 为准：commit `e929fa11fd20473f20775e5902a761cb69d6d404`、Release、
> SHA-256 `C3B36923400DF12E6143B1EA8DC76D9D4B65E2028BAE03CEB8402CBDEF0CA6FB`（两者均已复测一致）。
> **实测偏离**：NOTICE 写「无 PDB」，而 `FerriteLib.UiKit.pdb` **存在**（135 960 B，12:39:58；
> DLL 12:40:14）——正是 FL 自己 `AGENTS.md` 记的那起事故（冻结后一次只做验证的门链运行把陈旧的 Dev PDB
> 留回了冻结载体旁）。它不影响 DLL 的身份（SHA/大小/Release 配置三者均与 NOTICE 一致），但它是 FL 侧的
> 卫生项，**本会话按规矩没有构建 ferritelib、也没有触碰该文件**。

**消解对象与同批退役**

| 对象 | code / draw | 处置 |
|---|---:|---|
| `UsRaceLayerWidget` | 153 / 53 | kind + 文件（221 行，含被移出的 `UsPacksText`） |
| `UsXenotypeLayerWidget` | 128 / 56 | kind + 文件（167 行） |
| `UsDomainSelection` | 15 行 | 退役：**行自己的 key 就是 payload**，三字段结构体没有存在理由 |

- **Registrar 的 us/* kind 集 15 → 13**（`UiSourceInvariantTests` 基数钉同批改 13）。
- `UsPacksText` **移出**到 `UI/Layout/UsPacksText.cs`（75 行）：它的调用者现在是 host 的 per-item 投影，
  不是部件；随之删掉两个 `UiWidgetContext` 重载（没有调用者的入口就是「一段文本两个解析者」）。
- `select-domain` 的动作键名不变（不变式按名字钉着它），**payload 类型 `UsDomainSelection` → `string`**：
  行 key 本身就是身份，host 负责解码（race = defName；xenotype = `<race>|<target>`，`|` 安全——
  引擎拒绝含 `/` 或 `#` 的 item key）。

**声明形态**

```xml
<Section Id="race-layer" Tab="Packs" Padding="12" Gap="6">
  <Widget Id="race-layer-header" Kind="section/header" TitleKey="US.Section.RaceDomain" Height="26" HelpKey="us/race-layer" />
  <Repeat Id="race-layer-rows" Items="race-rows" Template="race-layer-row" Padding="0" Gap="2" />
</Section>
<Templates>
  <Overlay Id="race-layer-row" Padding="0">
    <Column Id="race-layer-row-hit" Gap="0" Padding="0">      <!-- 命中带：两格裸按钮，画在最底层 -->
      <Widget Id="race-layer-row-hit-a" Kind="input/button" Chrome="none" Height="Auto" ActionBind="select-domain" PayloadKey="payload" HelpKey="us/race-layer/row" />
      <Widget Id="race-layer-row-hit-b" Kind="input/button" Chrome="none" Height="Auto" ActionBind="select-domain" PayloadKey="payload" HelpKey="us/race-layer/row" />
    </Column>
    <Column Id="race-layer-row-text" Gap="2" Padding="0">     <!-- 两行文本画在命中带之上 -->
      <Widget Id="race-layer-row-title" Kind="text/wrapped" Bind="title" HelpKey="us/race-layer/row" />
      <Widget Id="race-layer-row-detail" Kind="text/wrapped" Bind="detail" Emphasis="Muted" HelpKey="us/race-layer/row" />
    </Column>
  </Overlay>
</Templates>
```

### 5.9.1 本片验证了哪些 FL 组件（这就是本片的产出）

| 组件 | 验证了什么 | 结果 |
|---|---|---|
| `Repeat` + `<Templates>` | 第二个真实消费者（第一个是 checklist）；item key 进身份（`race-layer-row#testrace`），per-item 绑定命名空间成立 | **成立**。行 key 直接可用作 binding 段与 payload |
| `input/button.PayloadKey`（**G2**） | 命令收到**行自己的 key**，且在 item 作用域内解析 | **成立且是本片的证据核心**：lane 逐行按下、逐行断言模型收到的域名（M3 证明这个 payload 是**承重**的，不是装饰） |
| `input/button Chrome="none" + Height="Auto"`（**G3**） | 无外观命中区、按自身内容量高 | **成立，但边界是实测的**（见 5.9.2 B2）：命中区**覆盖不了**内容量高的整行 |
| `text/wrapped` + per-item `Bind` | 数据驱动的两行文本，按自身槽宽量测 | **成立** |
| 容器 `Tab` / `section/header` / `Overlay` 作为模板根 | 声明式卡片与行封装 | **成立**（Overlay 作模板根可用，子元素同一内宽、同一上缘） |
| `VisibleKey`（未被本片使用但对照） | item 作用域内唯一的可见性/状态钩子 | 见 B1：它是**唯一**被 scoped 的状态类属性 |

### 5.9.2 摩擦：哪条是 (A)、哪条是 (B)

**(A) US 自己的用法 —— `ActionBind` 在模板里是 item 作用域的，一条声明服务不了所有行。**
元素实际索取的是 `<items>.<itemKey>.select-domain`，所以 host 必须**逐行注册一个命令**。这不是缺口
（PayloadKey 已给出行身份，逐行命令只是引擎的作用域规则），但意味着「per-item action key」与
「per-item payload」在用了 PayloadKey 之后**互为冗余**。第一版正是死在这里，trip 日志把答案写在脸上：
`No action binding registered for 'race-rows.human.select-domain'`。

**(B1) 真正的通用缺口：`SelectedKey` 没有被 item 作用域化 ⇒ 数据驱动列表无法表达「选中的是哪一行」。**
`QualifyItemBinding`（`ferritelib/Source/FerriteLib.UiKit/Kernel/UiLayoutEngine.cs:1876-1891`）只作用域化
`Bind` / `ActionBind` / `OptionsBind` / `VisibleKey` / `PayloadKey`；而 `SelectedKey` 是**元素级**的
（`Kernel/Widgets/AtomVocabulary.cs:53`），`Tone`/`Emphasis` 又只有字面量。三者相加的后果是：
**模板里的行说不出「我是被选中的那一行」**。

- **最小复现**：在 `<Templates>` 的任意元素上加 `SelectedKey="selected"`。引擎给**每一行**都解析同一个
  页级键 `selected`（不是 `<items>.<key>.selected`），于是要么所有行同色，要么每行各记一条
  `unresolvable binding` 报告。
- **一行修法候选**：把 `SelectedKey` 加进 `QualifyItemBinding` 的作用域名表。**未向 FL 提请求**（按本轮规则报 lead）。
- **本片的处置**：不声明 `SelectedKey`（声明了就是每行一条无效绑定），并把这份缺失**钉成 lane 的断言**
  ——`DeclarativePacksLaneTests` 断言两个模板里**没有**任何元素带 `SelectedKey` 或 `Tone`。缺口变成证据，
  不再是注释。

**(B2) 真正的通用缺口（已在 §0.4 G3 记录，这里是它的第一个真实消费者）：`Height="Auto"` 量的是「它自己
那个 caption」，所以裸命中区**无法**拉伸到它盖着的那一行的内容高度。**
`ButtonWidget.Measure`（`Kernel/Widgets/ButtonWidget.cs:84-99`）：Auto ⇒ 量自己的 caption，
caption 为空 ⇒ 回落 `RowHeight`（本页 24）。而 `Overlay` 的子元素**保持自己的量测高度**、容器取最高子元素
（`Kernel/UiLayoutEngine.cs:1423,1433,1436`）；`AlignY="Stretch"` 在 placement 规则里只是「分数 0 的位置」，
**不是**一个延展（`Kernel/UiPlacement.cs:71-74`）。

- **最小复现**：`<Overlay><Widget Kind="input/button" Chrome="none" Height="Auto" ActionBind=… PayloadKey=…/>
  <Column><Widget Kind="text/wrapped" Bind="a"/><Widget Kind="text/wrapped" Bind="b"/></Column></Overlay>`
  ⇒ 命中带 24px，行 68.67px。**没有任何声明属性**能让这条带子取到兄弟或父的高度。
- **本片的处置（不是绕开，是照实记录）**：命中区做成**两条叠放的裸带**（2 × 24 = 48px），它服务的正是
  行的两行文本；`Height="Auto"` 的空 caption 就是「一格密度 token」的忠实用法。边界因此可测量、可断言。

### 5.9.3 实测几何（harness，StubMetrics；非真实像素）

| 量 | 值 |
|---|---|
| 单行（不换行）行高 | **68.67px**（shipped floor 48） |
| 命中带 | **48px**（2 × 24）⇒ 覆盖 **69.9%** |
| 换行（`WrappingDomainText`）行高 | **90px** ⇒ 覆盖 **53.3%**，**42px 不可点** |
| race 卡（3 行） | **266px**（header 26 + 行集 210 + 卡 padding 24） |
| xenotype 卡（1 行） | **124.67px** |
| Repeat 行集 | 行和 + 声明 `Gap` 2 × (n−1) + 声明 `Padding` 0 × 2 |

> `Repeat` 的 `Padding` 必须**显式声明 0**：不声明会回落到密度默认的每侧 6（S3-5 那条「每个容器自己声明
> 节奏」），行集凭空多 12px。这是本轮实测的第二次同类事故（S3-5 之后），lane 现在按声明值算这条关系。

### 5.9.4 玩家可见差异（六条；全部**仍需实机**）

1. **整行命中区的形状变了（本片核心观感差异）。** shipped：整行整宽、整高可点（内容量高，≥48px）。
   现在：整宽的一摞 **2×24px** 落在行顶部 —— 不换行行覆盖 **69.9%**（48/68.67），换行行掉到 **53.3%**
   （90px 行里 42px 是死的）。**detail 行的下半部分不再可点**。这是 B2 的直接后果，不是排版选择。
2. **选中态：拆成两半记录（B1 修复后重写，2026-09-21）。** shipped 的选中态是**三件**——金色标题墨、
   行面 Selected 填充、左侧 3px 选中轨。载体 `e929fa11` 之后：
   - **已恢复的一半 —— 选中行标题墨色。** `SelectedKey` 现在是 item 作用域的，行标题声明
     `SelectedKey="selected"` ⇒ 解析到 `Active` 角色 ⇒ `text/wrapped` 的 Text = `TextOnGold`，
     与 shipped 选中标题同色。**只声明在标题上、detail 不声明**：`Active` 对文本**一律**取
     `TextOnGold` 而**忽略 `Emphasis`**，若 detail 也声明，它的墨会被一起拉成金色，而 shipped 的 detail
     是 `TextSecondary`（现 `Emphasis="Muted"`）——所以只落在标题上是对 shipped 的忠实还原，不是漏了。
     【仍需实机】
   - **未恢复的一半 —— 独立成条已知限制，见下面的 §5.9.6。** 行面填充与左侧选中轨仍表达不了。
   玩家仍能通过下方 checklist 的内容推断选中的域。
3. **行面 / hover 高亮消失**：`Chrome="none"` 什么都不画、`text/wrapped` 不画面（与 checklist / S4-1 同类差异）。
4. **行高 +20.67px**（68.67 vs shipped 48 的 floor）：atom 每行带 `Padding*2`=12px 的纵向留白。
   卡片随之变高（race 266 / xenotype 124.67）。
5. **detail 行字号 Tiny → Small**（atom 只有主题字号），墨色保留（`Emphasis="Muted"` → TextSecondary）；
   两行都从卡片 12px 内边距起排（shipped 另加 `RowLeftPadding` 10）⇒ 文本**左移 10px**；
   两行都是 **UpperLeft**（shipped MiddleLeft 垂直居中）。
6. **空列表**：shipped 与现在都只画卡片壳（body 0）—— 无差异，记录以说明这一点被核对过。

### 5.9.5 变异证据（8 个，逐个实测）

| 变异 | 实测红在哪（断言原文摘录） |
|---|---|
| **M1** 把 `Kind="us/race-layer"` 放回 manifest（旧 kind 恢复参与） | `real embedded resource + schema shape`：*the retired composite kind must not survive as a manifest Kind: us/race-layer* |
| **M2** 去掉命中带的 `PayloadKey`（G2 之前那种「无 payload 命令」形态） | 本 lane G2 步：*G2: the hit band must carry the row's own key (PayloadKey="payload"), got ''* |
| **M3** payload 从「行自己的 key」改成常量 `"human"` | 本 lane G2 步：*pressing band #3 must select row 1 ('testrace'); the model received scope=Race race='human'* —— **payload 承重的证明** |
| **M4** 删掉两条命中带中的一条 | 本 lane G3 步：*the arranged snapshot must carry 'race-layer-row-hit-b#human'* |
| **M5** 去掉 `Repeat` 的 `Padding="0"`（回落密度默认 6） | 本 lane G3 步的命中带普查：*must draw two bare bands per row (4 rows), got 6* |
| **M6**（B1 之前）在模板里声明 `SelectedKey="selected"`：当时它**跳过了**那条「模板里不许出现 SelectedKey」的钉 | 本 lane 声明形态步：*race-layer-row-title declares SelectedKey inside a template…*（该钉已在 B1 修复后**翻转成正向断言**） |
| **M-A**（B1 修复后）删掉 per-row 的 `<items>.<key>.selected` 注册 | 本 lane 选中步：*exactly ONE row may answer selected=true while a domain is selected, got []* |
| **M-B**（B1 修复后）让每行都回答 selected=true | 本 lane 选中步：*got [race-rows.human,race-rows.testrace,race-rows.sanguophage,xenotype-rows.human\|sanguophage]* —— 同一条断言的两个方向 |

**零选中态**：**可达性未证** —— 没有任何证据表明真实页面能出现 `selected-domain == null`；本夹具也到不了
（`SetRaceFilter` 只记录写入、不移动 `ViewState.RaceFilter`，空视图又没有行）。所以该状态**目前只作为变异态被
触达**（M-A 删掉 per-row 注册即得到「全行皆假」）。**一旦有人证明它在产品里可达，再补那条 lane**；在证明之前
「未证」就是它的正确记法——不为一个可能不存在的情景补输入。判别力本身不缺：两个方向已由同一条断言覆盖。

**哪条断言是变异证明、哪条只是守卫**：步骤 2 由 M2/M6 证明；B1 选中步由 M-A/M-B **双向**证明（少一个注册 ⇒
`[]`；多回答 ⇒ 四行全真）；步骤「G3」由 M4/M5 证明；步骤「G2」由 M3 证明；
**最后一步（清单带高算术）只是守卫**——它在基线绿，但 M5 先被 G3 步的普查抓住，所以这条关系没有被变异
单独证明过。`CountBands` 这一层之所以被特意挪到 G3 步，就是让「少了一条带子」红在拥有那条带的步骤上。

### 5.9.6 已知限制：选中行的**行面填充**与**左侧 3px 选中轨**仍不可表达（独立记档，带引证）

**不是什么**：这不是「漏了一个属性值」。

**机制（为什么表达不了）**：`text/wrapped` **不画 surface**（它只写文本），而这一行里**没有任何画表面的
atom** —— 两个命中带是 `Chrome="none"`（什么都不画），其余全是文本。所以缺的是**行状态的承载面**
（一个能被 `SelectedKey` 点亮的面），不是缺一个能填值的属性。

**引证（升级规则：请求不是证据，引证才是）**：**S4-2/S4-3 的数据驱动行集**是它的第一个真实消费者 ——
列表**说不出「选中的是哪一行」的可见状态**。因此此前记录的 **「(B) 候选：per-item `Tone`/`Emphasis` 值绑定」**
按引证**从候选升级为已证 (CONFIRMED)**，锚点：
- spec **§5.2** 的 layers 行：*「每行『选中』高亮若要走 `Tone`/`Emphasis`，二者都是**字面量属性**（无
  `ToneBind`）→ 只能用『每个 tone 一份模板 + item-local `VisibleKey`』复制模板」*（**(B) 候选**）；
- spec **§5.5**：*「缺口不是『没有 Resolve』，而是**没有 per-row 的角色输入面**（元素级 Tone 无法表达行状态，
  而 per-item 的角色绑定不存在）」*。

**判债分界**（维护者原则：技术债在**可见且可修复**时不许放任扩散）：
- B1（`SelectedKey` 未 item 作用域化）属**可见且便宜可修** ⇒ 当场修（载体 `e929fa11`）。
- 本条属**可见但不便宜可修**：要的是一个新的**行状态承载能力**（per-item 的角色/面绑定，或一个能画表面的
  per-item 原子），不是一行改动 ⇒ **记录清楚 + 带引证 + 等第二个消费者或维护者要求时再升级为能力**。

## 6. 实施切片（0.5.x 线，短命分支）

| 切片 | 内容 | 门 |
|---|---|---|
| S1 | 载入 Release 载体的 0.7 载荷；pin 已抬 `[0.7.0, 0.8.0)`；绑定写入注册表 + `UiInvalidation` 分类 | 15 门 + 既有 lane |
| S2 | 样式文档落地（26 色 + 5 尺寸）+ `UsTheme` 退化 + 逐行选色清理 | token 相等 lane 改为断言文档值 |
| S3 | 新清单骨架（页头 `Overlay` + 三栏 + 页脚 `Overlay`）+ density 间距 | 五视口几何/无重叠/负宽（**口径见 §6.1**） |
| S4 | 逐工作区原子化（Overview/Distance/Packs/Tuning/Presets） | 每区一套失败敏感 lane |
| S5 | 文件驱动接线（Layout/Style 松散文件 + 内嵌回退 + 失败保留） | 三条机械验收 + 坏文件 lane |
| S6 | Diagnostics 面板 + 详情窗 + overlay 同批重构 | 既有 mood/几何 lane 按新几何重导 |
| S7 | 清理门（kind 白名单、旧写法零命中）+ 实机矩阵 | 新扫描门 + 维护者实机 |

### 6.1 S3 的「五视口几何」口径（可复算，2026-09-21 定）

「五视口几何」是一道门，所以它必须**枚举**，不能靠会话记忆。五个**页面视口宽**（喂给 `UiHost.MeasureAndArrange` 的逻辑宽，不是窗宽）：

| 视口 | 代表什么 | 来源 |
|---|---|---|
| **1280** | 最宽的**真实**窗 | 2560×1440 屏：`SettingsClosedWidth = min(1280, 1440×0.9×4/3) = 1280` |
| **1024** | 一个宽参考点（该宽下 body-row 必为 Row） | 套件既有探针 |
| **736** | **最小真实窗的内宽** | 800（`SettingsWidthFloor`）− 2×20 chrome − 2×12 page padding = 736 ⇒ 三栏态最紧的真实情形 |
| **480** | 窄态人工探针（真实窗口到不了） | 套件既有探针 |
| **320** | 最窄探针 | 套件既有探针 |

每个视口 × {English, ChineseSimplified} × {抽屉开, 抽屉关} × {五个工作区} 各断言四件：① 每个 rect 有限且**宽高非负**、每个 scroll viewport 是真的盒子；② 每个元素**落在其父 rect 内**（`Scroll` 的子元素用**移到视口原点的 content box** 作参照，直接用 (0,0,..) 的 content box 是经典假红）；③ **同一坐标空间内**任意两个可见元素不重叠（排除祖先与 `Overlay` 子树）——**不是**「兄弟不重叠」（引擎的 flow 分配让兄弟不可能重叠，只测兄弟等于没测），而是「子元素溢出自己的槽、压到堂兄弟上」，S3-1 第一次运行抓到的正是这一条；④ 骨架形状：三列要么**全部并排**、要么**全部堆叠**，堆叠列与中心列同 x 同宽，wide 态用声明的 `Width`；两个端点（1280 必三栏 / 320 必堆叠）作为**产品不变量**单独断言。
**分账（哪些是变异证明、哪些只是守卫）写在 lane 自己的文档注释里**：containment 与跨父级 overlap 有变异证明，finite/非负 与「scroll box 是真的」两条**只是守卫**（引擎两边都夹取，没有 manifest 改动能触达），不计作证据。

**S3 自身的切片**（每一步一个提交、每一步之后页面都可用）：

| 步 | 内容 | 状态 |
|---|---|---|
| S3-0 | 刷新本文 §0（本文件） | 落地 |
| S3-1 | 五视口守护 lane（对**当前**几何先绿）+ 它抓到的窄态 nav-column 冗余 `Fill` | 落地 |
| S3-2a | 页头 `Overlay` 带 + 右对齐帮助开关（`input/button`）+`toggle-help-drawer` 改 `BindCommand`；`us/page-title` 交出开关 | 落地 |
| S3-2b | 把 `us/page-title` 搬进页头带（标题不再滚走）；**形状更正**：带子由 `Overlay` 改 `Row`、删 `Height`、标题按最坏值测带 | 落地 |
| S3-3 | 页脚 `Overlay` 带（band-only，原子化留 S4；`Height="26"` 更正为不写） | 落地 |
| S3-4a | `nav-column` 160→200 | 落地 |
| S3-4b | **原子步**：`help-scroll` 176→320（+`MinWidth=260`）+ `WindowChromeLayout` 常量 + 三条 lane 常量（断点**不动**，裁 500） | 落地 |
| S3-5 | density：(丁) —— 页面容器显式 `Padding`/`Gap`，token 不动；新增「每个容器必须自己声明节奏」的 lane | 落地 |
| S3-6 | 收尾：本表、TODO/MEMORY 指针、**一次收齐的实机清单** | 落地 |
| (乙1) | 窄屏帮助呈现 = A：\(body-row 与 footer 之间的条件带\) + 宿主派生两个只读呈现键 + **fit 变硬门** | 落地（**已验证**：harness ALL PASS + 15/15） |
| U1 | `global-volume` 的 18px 硬写带高 → 测量 | 落地（**已验证**）；S4-1 又把它换成 `text/wrapped` 的自量测 band，`GlobalVolumeBandLaneTests` 保持失败敏感（见 §5.8） |
| S4-1 | Overview 三张卡原子化：`us/global-volume` + `us/basic-tuning` + `us/camera-indicator` 消解为 manifest 子树并**同批退役** | 落地（harness ALL PASS + 15/15，7 个变异红）—— **§5.8 是它的摩擦报告** |
| S4-2 | Packs 两个 layer 卡：`us/race-layer` + `us/xenotype-layer` 消解为 `Repeat` + 模板行，**G2/G3 第一次被真实页面使用** | 落地（harness ALL PASS + 15/15，6 个变异红）—— **§5.9 是它的摩擦报告**（含 (B) 两条） |

## 7. 风险

- 载体当前是 **Dev 配置**，US 门 6 要求 Release 载体（见 §8）。
- Batch 1 的 density 回落会让未声明 `Padding`/`Gap` 的容器从 0 变 6：新清单必须显式声明或用 density 语义。
- 页面模型无文本对齐轴 → 右对齐文本仍需控件自带锚点（`us/camera-readout` 类）或 `AlignX` 摆放整个元素。
- FL 未实机验收；本设计的几何主张仍需实机矩阵。

## 8. 当前阻塞

US 门 6 失败：`ferritelib/1.6/Assemblies/FerriteLib.UiKit.dll` 是 **Dev** 配置
（`ProductVersion 0.7.0-dev+d82a3ca8db83`），US 构建/发布要求 Release 载体。
FL 侧命令（在其仓库内）：
```powershell
dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release --no-incremental
```
之后 US 侧重跑 `scripts/verify-local.ps1 -NoRestore`。
